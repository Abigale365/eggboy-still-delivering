// 单手柄 + 键鼠双轨无缝兼容 + 麦克风吹气 + 摄像头 AR 手势中枢（HardwareAndSensorHub）。
// 严格遵循「全设备无死角兼容 · 单手柄不接开发板」设计：
//   1. 单游戏手柄（1P 手柄）与键鼠全程无缝共存，拔掉手柄仅用键鼠、或全程只用 1 个手柄均可 100% 丝滑通关；
//   2. 麦克风实时吹气检测（用于拂去 3D 旧物背面海盐霜、给面包房烤炉鼓风助燃、稳住海风重心），无麦克风时随时可用鼠标擦拭 / F 键 / 手柄 RB 键替代；
//   3. 摄像头 AR 手势感应（内置原生 WebCamTexture 动作捕捉 + UDP 18888 手势扩展），用于灯塔顶层挥手闪灯呼唤山那边的人；无摄像头时用空格 / E 键 / 手柄按键无缝替代。
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

public sealed class HardwareAndSensorHub : MonoBehaviour
{
    public static HardwareAndSensorHub Instance { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    struct JOYINFOEX
    {
        public uint dwSize;
        public uint dwFlags;
        public uint dwXpos;
        public uint dwYpos;
        public uint dwZpos;
        public uint dwRpos;
        public uint dwUpos;
        public uint dwVpos;
        public uint dwButtons;
        public uint dwButtonNumber;
        public uint dwPOV;
        public uint dwReserved1;
        public uint dwReserved2;
    }

    [DllImport("winmm.dll")]
    static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;
    InteractionService interactionSvc;

    // 单手柄状态（与键鼠双轨并行）
    public bool Gamepad1Connected { get; private set; }
    public Vector2 Pad1LeftStick { get; private set; }
    public Vector2 Pad1RightStick { get; private set; }
    public uint Pad1Buttons { get; private set; }
    uint prevPad1Buttons;
    float pad1RingCooldown;

    // 摄像头 AR 手势状态（支持原生 WebCamTexture 动态捕捉 + UDP 手势流）
    public bool WebcamGestureActive { get; private set; }
    bool builtInCamActive;
    WebCamTexture webCamTex;
    Color32[] prevCamPixels;
    public bool CamOpen => WebcamGestureActive || (builtInCamActive && webCamTex != null && webCamTex.isPlaying);
    public Vector2 HandNormalizedPos { get; private set; } = new Vector2(0.5f, 0.5f);
    public float HandCenterX => HandNormalizedPos.x;
    public float HandWaveSpeed { get; private set; }
    public bool HandPinching { get; private set; }
    bool prevHandPinch;
    Vector2 prevHandPos = new Vector2(0.5f, 0.5f);
    float lastWebcamPacketTime;

    // 麦克风实时吹气状态
    public bool MicActive { get; private set; }
    public float MicLoudness { get; private set; }
    AudioClip micClip;
    string micDeviceName;
    readonly float[] micSamples = new float[512];
    float blowHoldTimer;

    // 外部稳定加成（供 TidalWindChallenge 读取）
    public float ExternalWindStabilizeBoost { get; private set; }

    readonly ConcurrentQueue<string> incomingPackets = new ConcurrentQueue<string>();
    UdpClient udpListener;
    Thread ioThread;
    volatile bool running;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        Instance = this;
        player = p;
        ui = u;
        world = w;
        director = d;
        state = s;
        interactionSvc = FindObjectOfType<InteractionService>();

        StartMicrophoneCapture();
        StartBackgroundUdpThread();
    }

    void StartMicrophoneCapture()
    {
        try
        {
            if (Microphone.devices != null && Microphone.devices.Length > 0)
            {
                micDeviceName = Microphone.devices[0];
                micClip = Microphone.Start(micDeviceName, true, 1, 16000);
                MicActive = micClip != null;
            }
        }
        catch (Exception)
        {
            MicActive = false;
        }
    }

    public bool ToggleBuiltInWebcam()
    {
        try
        {
            if (builtInCamActive && webCamTex != null)
            {
                webCamTex.Stop();
                builtInCamActive = false;
                HandWaveSpeed = 0f;
                return false;
            }

            if (WebCamTexture.devices != null && WebCamTexture.devices.Length > 0)
            {
                if (webCamTex == null)
                    webCamTex = new WebCamTexture(WebCamTexture.devices[0].name, 160, 120, 15);
                webCamTex.Play();
                builtInCamActive = true;
                prevCamPixels = null;
                return true;
            }
        }
        catch (Exception)
        {
            builtInCamActive = false;
        }
        return false;
    }

    void StartBackgroundUdpThread()
    {
        running = true;
        try
        {
            udpListener = new UdpClient(18888);
            udpListener.Client.ReceiveTimeout = 25;
        }
        catch (Exception)
        {
            udpListener = null;
        }

        ioThread = new Thread(UdpPollLoop) { IsBackground = true, Name = "TideTownSensorUdp" };
        ioThread.Start();
    }

    void UdpPollLoop()
    {
        while (running)
        {
            try
            {
                if (udpListener != null && udpListener.Available > 0)
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpListener.Receive(ref ep);
                    string text = Encoding.UTF8.GetString(data);
                    foreach (var line in text.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            incomingPackets.Enqueue(trimmed);
                    }
                }
            }
            catch (Exception) { }
            Thread.Sleep(12);
        }
    }

    void OnDestroy()
    {
        running = false;
        try { udpListener?.Close(); } catch (Exception) { }
        try { if (webCamTex != null && webCamTex.isPlaying) webCamTex.Stop(); } catch (Exception) { }
    }

    void Update()
    {
        if (ui == null || !ui.inputClose) return;
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f || dt > 0.25f) return;

        ExternalWindStabilizeBoost = Mathf.MoveTowards(ExternalWindStabilizeBoost, 0f, dt * 1.6f);

        // 0. 塔顶随时按 B 键快速下塔（彻底杜绝困在灯塔顶层）
        if (player != null && player.transform.position.y > 10f && !player.locked && !WoodenMechanismPuzzleModal.IsOpen && !ObjectInspectModal.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (director != null) director.DescendTower();
            }
        }

        // 1. 处理摄像头 AR 手势（内置 WebCamTexture 动作感应 + UDP 手势包）
        ProcessIncomingGesturePackets();
        UpdateBuiltInWebcamMotion(dt);
        if (WebcamGestureActive && Time.unscaledTime - lastWebcamPacketTime > 2.5f)
            WebcamGestureActive = false;

        // 2. 轮询单游戏手柄（1P 手柄与键鼠无缝双轨共存）
        PollSingleGamepad(dt);

        // 3. 实时分析麦克风吹气声压（以及无麦克风时的 F 键 / 手柄 RB 吹气平替）
        AnalyzeMicrophoneBlow(dt);
    }

    void UpdateBuiltInWebcamMotion(float dt)
    {
        if (!builtInCamActive || webCamTex == null || !webCamTex.isPlaying || !webCamTex.didUpdateThisFrame)
            return;

        Color32[] pixels = webCamTex.GetPixels32();
        int w = webCamTex.width;
        int h = webCamTex.height;
        if (pixels == null || pixels.Length != w * h) return;

        if (prevCamPixels == null || prevCamPixels.Length != pixels.Length)
        {
            prevCamPixels = (Color32[])pixels.Clone();
            return;
        }

        long sumX = 0;
        long sumY = 0;
        int changedCount = 0;

        for (int y = 0; y < h; y += 2)
        {
            int row = y * w;
            for (int x = 0; x < w; x += 2)
            {
                int idx = row + x;
                Color32 c = pixels[idx];
                Color32 p = prevCamPixels[idx];
                int diff = Mathf.Abs(c.r - p.r) + Mathf.Abs(c.g - p.g) + Mathf.Abs(c.b - p.b);
                if (diff > 72)
                {
                    // 镜像 X 坐标，使挥手方向与画面一致
                    sumX += (w - 1 - x);
                    sumY += y;
                    changedCount++;
                }
                prevCamPixels[idx] = c;
            }
        }

        int totalSampled = (w / 2) * (h / 2);
        float motionRatio = totalSampled > 0 ? (float)changedCount / totalSampled : 0f;
        HandWaveSpeed = Mathf.Lerp(HandWaveSpeed, Mathf.Clamp01(motionRatio * 5.5f), dt * 10f);

        if (changedCount > 18)
        {
            float cx = Mathf.Clamp01((float)sumX / (changedCount * w));
            float cy = Mathf.Clamp01((float)sumY / (changedCount * h));
            HandNormalizedPos = Vector2.Lerp(HandNormalizedPos, new Vector2(cx, cy), dt * 10f);
        }
    }

    void ProcessIncomingGesturePackets()
    {
        int processed = 0;
        while (processed++ < 24 && incomingPackets.TryDequeue(out string pkt))
        {
            if (pkt.StartsWith("HAND:"))
            {
                WebcamGestureActive = true;
                lastWebcamPacketTime = Time.unscaledTime;
                string[] parts = pkt.Substring(5).Split(',');
                if (parts.Length >= 5 &&
                    float.TryParse(parts[0], out float hx) &&
                    float.TryParse(parts[1], out float hy) &&
                    int.TryParse(parts[2], out int pinchInt) &&
                    float.TryParse(parts[3], out float wave))
                {
                    Vector2 newPos = new Vector2(Mathf.Clamp01(hx), Mathf.Clamp01(hy));
                    Vector2 delta = newPos - prevHandPos;
                    prevHandPos = newPos;
                    HandNormalizedPos = newPos;
                    HandPinching = pinchInt > 0;
                    HandWaveSpeed = wave;

                    if (ObjectInspectModal.IsOpen)
                    {
                        ObjectInspectModal.ExternalRotate(-delta.x * 220f, delta.y * 180f);
                        if (HandPinching && !prevHandPinch)
                        {
                            int pinIdx = hx < 0.38f ? 0 : (hx > 0.62f ? 2 : 1);
                            ObjectInspectModal.ExternalPressPin(pinIdx);
                        }
                    }
                    else if (WoodenMechanismPuzzleModal.IsOpen)
                    {
                        if (Mathf.Abs(delta.x) > 0.045f)
                            WoodenMechanismPuzzleModal.ExternalRotateRing(WoodenMechanismPuzzleModal.ActiveRingSelection, delta.x > 0f ? 1 : -1);
                        if (HandPinching && !prevHandPinch)
                            WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
                    }
                    prevHandPinch = HandPinching;
                }
            }
        }
    }

    void PollSingleGamepad(float dt)
    {
        Gamepad1Connected = ReadWinMMJoystick(0, out Vector2 p1L, out Vector2 p1R, out uint b1);
        Pad1LeftStick = p1L;
        Pad1RightStick = p1R;
        Pad1Buttons = b1;

        uint down1 = b1 & ~prevPad1Buttons;
        prevPad1Buttons = b1;

        if (pad1RingCooldown > 0f) pad1RingCooldown -= dt;

        // 1. 在 3D 旧物端详界面中：单手柄左/右摇杆 360° 翻转模型，RB 键拂去盐霜，A/B/X 键按压三枚铜扣
        if (ObjectInspectModal.IsOpen)
        {
            Vector2 stick = p1L.sqrMagnitude > p1R.sqrMagnitude ? p1L : p1R;
            if (stick.sqrMagnitude > 0.04f)
                ObjectInspectModal.ExternalRotate(-stick.x * 125f * dt, stick.y * 105f * dt);
            if ((down1 & 32u) != 0) ObjectInspectModal.ExternalClearDust(false);
            if ((down1 & 1u) != 0) ObjectInspectModal.ExternalPressPin(0);
            if ((down1 & 2u) != 0) ObjectInspectModal.ExternalPressPin(1);
            if ((down1 & 4u) != 0) ObjectInspectModal.ExternalPressPin(2);
            return;
        }

        // 2. 在 3D 鲁班机关界面中：单手柄 LB/RB 切换外中内环，摇杆拨动齿轮，X 键拨木栓，A 键推入主轴开锁
        if (WoodenMechanismPuzzleModal.IsOpen)
        {
            float s1 = Mathf.Abs(p1L.x) > Mathf.Abs(p1R.x) ? p1L.x : p1R.x;
            if (Mathf.Abs(s1) > 0.55f && pad1RingCooldown <= 0f)
            {
                WoodenMechanismPuzzleModal.ExternalRotateRing(WoodenMechanismPuzzleModal.ActiveRingSelection, s1 > 0f ? 1 : -1);
                pad1RingCooldown = 0.24f;
            }

            if ((down1 & 1u) != 0) WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
            if ((down1 & 4u) != 0)
            {
                // 单手柄 X 键依次拨开三枚外层木栓
                WoodenMechanismPuzzleModal.ExternalUnlockAllLatchesByBlowOrGesture("手柄机关拨栓");
            }
            if ((down1 & 16u) != 0) WoodenMechanismPuzzleModal.ActiveRingSelection = (WoodenMechanismPuzzleModal.ActiveRingSelection + 2) % 3;
            if ((down1 & 32u) != 0) WoodenMechanismPuzzleModal.ActiveRingSelection = (WoodenMechanismPuzzleModal.ActiveRingSelection + 1) % 3;
            return;
        }

        // 3. 大世界中：单手柄摇杆移动与稳住海风重心，A/X 键互动，B 键塔顶下塔，Y 键打开心愿 3D 造物笺，RB 键鼓风
        if (p1L.sqrMagnitude > 0.12f)
        {
            ExternalWindStabilizeBoost = 1f;
            if (player != null && !player.locked && world != null && !AIAgentCalibrationDirector.IsOpen)
            {
                var cam = Camera.main != null ? Camera.main.transform : player.transform;
                Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
                Vector3 moveDir = (fwd * p1L.y + right * p1L.x).normalized;
                Vector3 next = player.transform.position + moveDir * (4.2f * dt);
                if (world.Walkable(next.x, next.z))
                {
                    next.y = world.GroundAt(next.x, next.z, next.y);
                    player.transform.position = next;
                    player.yaw = Mathf.Atan2(moveDir.x, moveDir.z);
                    player.transform.rotation = Quaternion.Euler(0f, player.yaw * Mathf.Rad2Deg, 0f);
                }
            }
        }

        if ((down1 & 1u) != 0 || (down1 & 4u) != 0)
        {
            if (!AIAgentCalibrationDirector.IsOpen && interactionSvc != null)
                interactionSvc.TryInteractNearest();
        }
        if ((down1 & 2u) != 0 && player != null && player.transform.position.y > 10f && director != null)
        {
            director.DescendTower();
        }
        if ((down1 & 8u) != 0)
        {
            AIAgentCalibrationDirector.ToggleConsole();
        }
        if ((down1 & 32u) != 0)
        {
            TriggerBlowEffect(false);
        }
    }

    static bool ReadWinMMJoystick(int joyId, out Vector2 leftStick, out Vector2 rightStick, out uint buttons)
    {
        leftStick = Vector2.zero;
        rightStick = Vector2.zero;
        buttons = 0u;
        var info = new JOYINFOEX { dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX)), dwFlags = 0xFFu };
        if (joyGetPosEx(joyId, ref info) != 0) return false;

        float lx = (info.dwXpos - 32767f) / 32768f;
        float ly = -(info.dwYpos - 32767f) / 32768f;
        float rx = (info.dwZpos - 32767f) / 32768f;
        float ry = -(info.dwRpos - 32767f) / 32768f;
        if (Mathf.Abs(lx) < 0.18f) lx = 0f;
        if (Mathf.Abs(ly) < 0.18f) ly = 0f;
        if (Mathf.Abs(rx) < 0.18f) rx = 0f;
        if (Mathf.Abs(ry) < 0.18f) ry = 0f;
        leftStick = new Vector2(lx, ly);
        rightStick = new Vector2(rx, ry);
        buttons = info.dwButtons;
        return true;
    }

    void AnalyzeMicrophoneBlow(float dt)
    {
        // 无麦克风平替：大世界中按 F 键同样可在面包房烤炉旁扇风助燃
        if (Input.GetKeyDown(KeyCode.F) && !ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen && !AIAgentCalibrationDirector.IsOpen)
        {
            TriggerBlowEffect(false);
        }

        if (!MicActive || micClip == null) return;
        int pos = Microphone.GetPosition(micDeviceName) - micSamples.Length;
        if (pos < 0) return;

        micClip.GetData(micSamples, pos);

        float sumSq = 0f;
        for (int i = 0; i < micSamples.Length; i++)
        {
            float s = micSamples[i];
            sumSq += s * s;
        }

        float rms = Mathf.Sqrt(sumSq / micSamples.Length);
        MicLoudness = Mathf.Clamp01(rms * 14f);

        if (MicLoudness > 0.16f)
        {
            ExternalWindStabilizeBoost = 1f;
            blowHoldTimer += dt;
            if (blowHoldTimer >= 0.75f)
            {
                blowHoldTimer = 0f;
                TriggerBlowEffect(true);
            }
        }
        else
        {
            blowHoldTimer = Mathf.Max(0f, blowHoldTimer - dt * 0.5f);
        }
    }

    void TriggerBlowEffect(bool byMic)
    {
        if (ObjectInspectModal.IsOpen)
        {
            ObjectInspectModal.ExternalClearDust(byMic);
            return;
        }

        if (player != null && Vector3.Distance(player.transform.position, new Vector3(-11.5f, 1.3f, -7.8f)) < 9.5f)
        {
            var fx = FindObjectOfType<BakeryEffects>();
            if (fx != null && director != null && state != null && state.flags.Contains("oven_lit"))
            {
                director.BoostOvenBellows(fx);
            }
        }
    }
}
