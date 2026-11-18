using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

// 仅显式命令行激活。全部画面来自当前场景与既有玩法连续实机运动，严禁静态图缩放。
[DefaultExecutionOrder(9000)]
public sealed class TrailerCaptureRunner : MonoBehaviour
{
    [Serializable] public sealed class Scene { public string scene_tag; public float start_sec, end_sec; }
    [Serializable] sealed class Manifest { public Scene[] scenes; }
    [Serializable] sealed class SegmentExport { public int index; public string scene_tag; public int start_frame, end_frame; public float start_sec, end_sec; }
    [Serializable] sealed class SegmentExportList { public int total_frames; public float fps, duration_sec; public List<SegmentExport> segments = new List<SegmentExport>(); }

    GameBootstrap b;
    Camera cam;
    Manifest manifest;
    Process encoder;
    Stream pipe;
    RenderTexture capture;
    Texture2D pixels;
    string output;
    string manifestPathArg;
    string outFileName = "raw_150s_1080p.mp4";
    int frame, totalFrames, sceneIndex = -1;
    float t, u, totalDuration;

    Vector3 focus, focusStart, focusEnd, actor, actorEnd;
    float radius = 5f, radiusEnd = 5f, height = 2f, heightEnd = 2f, orbit = 210f, orbitDelta = 18f;
    bool[] events = new bool[16];

    Text title;
    Image paper;
    CanvasGroup titleGroup, endGroup;
    CanvasGroup wishCardGroup;
    Text wishTypedLabel;
    Transform[] turntables = new Transform[3];
    CanvasGroup assetsGroup;
    Light trailerFill;

    WorldMechanismDirector mech;
    WordMaterializerSystem words;
    PerspectivePrintSystem viewfinder;
    float printYaw, printPitch, printHold, printHeldYaw;
    bool printAim;
    Vector3 bridgeWalkA, bridgeWalkB;

    RectTransform cachedLetterOverlay, cachedLetterPaper, cachedLetterEnvelope;
    Image cachedLetterBackdrop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.FindIndex(args, a => a.Equals("-trailerRecord", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= args.Length || FilmingStageJumper.AuditArguments()) return;
        if (FindObjectOfType<TrailerCaptureRunner>() != null) return;
        var go = new GameObject("Trailer engine capture 1080p30");
        DontDestroyOnLoad(go);
        var runner = go.AddComponent<TrailerCaptureRunner>();
        runner.output = Path.GetFullPath(args[i + 1]);
        int m = Array.FindIndex(args, a => a.Equals("-trailerManifest", StringComparison.OrdinalIgnoreCase));
        if (m >= 0 && m + 1 < args.Length) runner.manifestPathArg = Path.GetFullPath(args[m + 1]);
        int f = Array.FindIndex(args, a => a.Equals("-trailerOutFile", StringComparison.OrdinalIgnoreCase));
        if (f >= 0 && f + 1 < args.Length) runner.outFileName = args[f + 1];
    }

    IEnumerator Start()
    {
        while ((b = FindObjectOfType<GameBootstrap>()) == null || b.director == null || b.state == null) yield return null;
        while (BootLoader.Opaque || BootLoader.WarmupBusy) yield return null;
        var bl = FindObjectOfType<BootLoader>();
        if (bl != null) Destroy(bl.gameObject);
        if (TitleScreen.Current != null) Destroy(TitleScreen.Current.gameObject);
        var tut = FindObjectOfType<BasicsTutorial>();
        if (tut != null) { tut.enabled = false; Destroy(tut); }
        var qg = FindObjectOfType<QuestGuide>();
        if (qg != null) { qg.enabled = false; Destroy(qg); }
        yield return null;
        Directory.CreateDirectory(output);
        Screen.SetResolution(1920, 1080, false);
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
        TownVoiceover.StopAll();
        AudioListener.volume = 0f;

        b.ui.SendMessage("StartFromTitle", SendMessageOptions.DontRequireReceiver);
        if (b.intro != null) b.intro.SendMessage("finish", true, SendMessageOptions.DontRequireReceiver);
        yield return null;
        b.ui.SetPaused(false);
        b.ui.CloseLetter();
        b.ui.SetCinematic(true, "trailer");
        b.state.flags.Clear();
        b.state.stamps.Clear();
        b.state.flags.Add("letters_taken");
        b.state.step = "P1_mailbox";

        var dimmer = FindObjectOfType<SkyDimmer>();
        if (dimmer != null) dimmer.enabled = false;
        TownLighting.SetFilmingPhase(0f);

        cam = Camera.main;
        var chase = FindObjectOfType<ChaseCamera>();
        if (chase != null) chase.enabled = false;
        var proto = FindObjectOfType<PrototypeGame>();
        if (proto != null) proto.enabled = false;
        mech = FindObjectOfType<WorldMechanismDirector>();
        words = FindObjectOfType<WordMaterializerSystem>();
        viewfinder = FindObjectOfType<PerspectivePrintSystem>();

        // 隐藏船坞绞盘底部多余的白色方底座，避免角色摇绞盘时与底座穿模
        var winchRootGo = GameObject.Find("Scene_M3_潮汐船坞齿轮绞盘_总成");
        if (winchRootGo != null)
        {
            foreach (Transform child in winchRootGo.transform)
                if (child.name.Contains("救生圈") || child.name.Contains("D6_"))
                    child.gameObject.SetActive(false);
        }

        var fillGo = new GameObject("TrailerWarmFillLight");
        trailerFill = fillGo.AddComponent<Light>();
        trailerFill.type = LightType.Directional;
        trailerFill.color = new Color(1f, 0.90f, 0.76f);
        trailerFill.intensity = 0f;
        trailerFill.shadows = LightShadows.None;

        string manifestJson;
        if (!string.IsNullOrEmpty(manifestPathArg) && File.Exists(manifestPathArg))
            manifestJson = File.ReadAllText(manifestPathArg);
        else
            manifestJson = Resources.Load<TextAsset>("Trailer/timing_manifest_150s").text;

        manifest = JsonUtility.FromJson<Manifest>(manifestJson);
        if (manifest == null || manifest.scenes == null || manifest.scenes.Length < 16)
            throw new InvalidDataException("Invalid trailer timing manifest.");

        int lastIdx = manifest.scenes.Length - 1;
        totalDuration = manifest.scenes[lastIdx].end_sec;
        totalFrames = Mathf.Max(30, Mathf.RoundToInt(totalDuration * 30f));

        MakeTitles();
        MakeWishCard();

        var segExport = new SegmentExportList { total_frames = totalFrames, fps = 30f, duration_sec = totalDuration };
        for (int idx = 0; idx < manifest.scenes.Length; idx++)
        {
            var s = manifest.scenes[idx];
            segExport.segments.Add(new SegmentExport
            {
                index = idx,
                scene_tag = s.scene_tag,
                start_sec = s.start_sec,
                end_sec = s.end_sec,
                start_frame = Mathf.RoundToInt(s.start_sec * 30f),
                end_frame = Mathf.RoundToInt(s.end_sec * 30f),
            });
        }
        File.WriteAllText(Path.Combine(output, "scene_segments.json"), JsonUtility.ToJson(segExport, true));

        capture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        capture.Create();
        pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

        string outVideoPath = Path.Combine(output, outFileName);
        var psi = new ProcessStartInfo("ffmpeg",
            "-hide_banner -y -f rawvideo -pixel_format rgb24 -video_size 1920x1080 -framerate 30 -i pipe:0 -an -c:v libx264 -preset fast -crf 15 -pix_fmt yuv420p -movflags +faststart \"" + outVideoPath + "\"");
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardError = true;
        encoder = new Process { StartInfo = psi };
        encoder.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) lock (this) File.AppendAllText(Path.Combine(output, "ffmpeg.log"), e.Data + "\n");
        };
        encoder.Start();
        encoder.BeginErrorReadLine();
        pipe = encoder.StandardInput.BaseStream;

        Time.captureFramerate = 30;
        for (frame = 0; frame < totalFrames; frame++)
        {
            yield return null;
            t = frame / 30f;
            int si = 0;
            while (si < lastIdx && t >= manifest.scenes[si].end_sec) si++;
            if (si != sceneIndex) { sceneIndex = si; Enter(si); }
            var sc = manifest.scenes[si];
            u = Mathf.Clamp01((t - sc.start_sec) / Mathf.Max(0.05f, sc.end_sec - sc.start_sec));
            Direct();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
            var previous = RenderTexture.active;
            RenderTexture.active = capture;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
            pixels.Apply(false);
            RenderTexture.active = previous;
            byte[] bytes = pixels.GetRawTextureData();
            pipe.Write(bytes, 0, bytes.Length);
            if (frame % 90 == 0 || frame == totalFrames - 1)
            {
                byte[] preview = (byte[])bytes.Clone();
                const int stride = 1920 * 3;
                for (int row = 0; row < 1080; row++) Buffer.BlockCopy(bytes, row * stride, preview, (1079 - row) * stride, stride);
                pixels.LoadRawTextureData(preview);
                pixels.Apply(false);
                File.WriteAllBytes(Path.Combine(output, "frame_" + frame.ToString("D4") + ".jpg"), pixels.EncodeToJPG(85));
                File.WriteAllText(Path.Combine(output, "progress.json"),
                    "{\"frame\":" + frame + ",\"total\":" + totalFrames + ",\"scene\":" + (si + 1) + "}");
                Debug.Log("TRAILER_FRAME " + frame + "/" + totalFrames + " scene=" + (si + 1));
            }
        }
        pipe.Close();
        pipe = null;
        while (!encoder.HasExited) yield return null;
        Time.captureFramerate = 0;
        File.WriteAllText(Path.Combine(output, "capture-result.json"),
            "{\"frames\":" + totalFrames + ",\"fps\":30,\"duration\":" + totalDuration.ToString("F3") + ",\"encoderExit\":" + encoder.ExitCode + "}");
        Application.Quit(encoder.ExitCode);
    }

    void Mark(string flag) { if (!b.state.flags.Contains(flag)) b.state.flags.Add(flag); }
    Vector3 Ground(Vector3 p) { p.y = b.world.GroundAt(p.x, p.z); return p; }

    Vector3 SafeStand(Vector3 p)
    {
        for (float r = 0f; r <= 4.5f; r += 0.35f)
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI / 12f;
            var q = Ground(p + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            if (b.world.Walkable(q.x, q.z) && q.y > WaterLevel.StaticY + 0.1f) return q;
        }
        return Ground(p);
    }

    Vector3 Stove()
    {
        var s = Array.Find(BakeryData.Load(), x => x.id == "stove");
        return s != null ? new Vector3(s.x, s.y, s.z) : new Vector3(-9.73f, 1.3f, -5.85f);
    }

    void Actor(Vector3 at, Vector3 look)
    {
        actor = actorEnd = SafeStand(at);
        b.player.locked = true;
        b.player.transform.position = actor;
        b.player.vel = Vector3.zero;
        b.player.FaceTowards(look);
    }

    void StepActorAlong(Vector3 from, Vector3 to, float progress01, Vector3? finalLook = null)
    {
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress01));
        Vector3 raw = Vector3.Lerp(from, to, k);
        raw.y = b.world.GroundAt(raw.x, raw.z, Mathf.Max(from.y, to.y) + 1.5f);
        Vector3 dir = to - from;
        dir.y = 0f;
        bool moving = progress01 > 0.005f && progress01 < 0.992f && dir.sqrMagnitude > 0.02f;
        float yaw = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(dir.x, dir.z) : b.player.yaw;
        if (!moving && finalLook.HasValue)
        {
            Vector3 lk = finalLook.Value - raw;
            lk.y = 0f;
            if (lk.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(lk.x, lk.z);
        }
        Vector3 vel = moving ? dir.normalized * 3.1f : Vector3.zero;
        b.player.PuppetStep(raw, yaw, vel, true, 1f / 30f);
    }

    void Pose(string clip, ExprId expr)
    {
        b.player.PlayEmote(clip);
        FindObjectOfType<PrototypeGame>()?.SetExpression(expr);
    }

    void StyleLetterForTrailer(bool compactRight)
    {
        if (b.ui != null && b.ui.LetterOpen)
            b.ui.SendMessage("FinishLetterOpening", SendMessageOptions.DontRequireReceiver);
        if (cachedLetterPaper == null)
        {
            cachedLetterOverlay = TideTownStyleUI.FindLetterOverlay(b.ui);
            cachedLetterPaper = TideTownStyleUI.FindLetterPaper(b.ui);
            if (cachedLetterOverlay != null)
            {
                cachedLetterBackdrop = cachedLetterOverlay.GetComponent<Image>();
                var envTr = cachedLetterOverlay.Find("LetterEnvelope");
                if (envTr != null) cachedLetterEnvelope = envTr.GetComponent<RectTransform>();
            }
        }
        if (cachedLetterBackdrop != null && b.ui.LetterOpen)
            cachedLetterBackdrop.color = new Color(0f, 0f, 0f, 0f);
        if (cachedLetterEnvelope != null && b.ui.LetterOpen)
        {
            cachedLetterEnvelope.gameObject.SetActive(false);
        }
        if (cachedLetterPaper != null && b.ui.LetterOpen)
        {
            cachedLetterPaper.anchoredPosition = compactRight ? new Vector2(392f, 20f) : Vector2.zero;
            cachedLetterPaper.localScale = Vector3.one * (compactRight ? 0.70f : 1f);
        }
    }

    void Enter(int s)
    {
        Array.Clear(events, 0, events.Length);
        ObjectInspectModal.DebugClose();
        b.ui.CloseLetter();
        TownVoiceover.StopAll();
        AIAgentCalibrationDirector.DebugSetConsole(false);
        if (wishCardGroup != null) wishCardGroup.alpha = 0f;
        if (viewfinder != null && viewfinder.IsViewfinderActive) viewfinder.CloseViewfinder();
        b.player.locked = true;
        if (s != 11 && s != 12 && s != 13) b.director.StopAllCoroutines();

        trailerFill.intensity = 0f;
        radius = radiusEnd = 5f;
        height = heightEnd = 2.4f;
        orbit = 200f;
        orbitDelta = 18f;
        focus = focusStart = focusEnd = Stove();

        var postOffice = GameObject.Find("B1_post_office_visual");
        var postBounds = postOffice != null ? Lux3DModelFit.WorldBounds(postOffice) : new Bounds(b.world.MailboxPos, Vector3.one);
        var postDoor = new Vector3(postBounds.center.x - 0.8f, 0f, postBounds.min.z - 1.3f);

        switch (s)
        {
            case 0:
                // 第一幕开场：贴近海岸石阶与空旷街道的横向推轨实机镜头，潮水上涨、草木随风摇曳
                TownLighting.SetFilmingPhase(0f);
                b.water.SetInstant(WaterLevel.LevelYs[0]);
                b.water.SetLevel("L1", 4.5f);
                focusStart = new Vector3(-16.5f, 2.0f, -11.5f);
                focusEnd = new Vector3(-4.5f, 2.5f, -8.2f);
                focus = focusStart;
                radius = 12.5f; radiusEnd = 10.5f;
                height = 4.6f; heightEnd = 3.8f;
                orbit = 168f; orbitDelta = 24f;
                Actor(postDoor, postDoor + Vector3.back * 4f);
                break;

            case 1:
                // EGGBOY 在邮局门前醒来 -> 开心跃起 -> 沿木栏杆内侧宽敞石板路连续跑向街心（零穿栏杆）
                b.water.SetInstant(WaterLevel.LevelYs[0]);
                Actor(postDoor + new Vector3(0.35f, 0f, -0.35f), postDoor + new Vector3(4.2f, 0f, -0.45f));
                actorEnd = Ground(actor + new Vector3(3.8f, 0f, -0.25f));
                focusStart = actor + Vector3.up * 0.55f;
                focusEnd = actorEnd + Vector3.up * 0.55f;
                focus = focusStart;
                radius = 3.5f; radiusEnd = 3.9f;
                height = 1.45f; heightEnd = 1.65f;
                orbit = 176f; orbitDelta = 18f;
                Pose("look", ExprId.sleepy);
                break;

            case 2:
                // 跑向面包店冷烤炉，右侧展开林阿婆的信（镜头置于东南开阔石板广场，零墙体遮挡与桌椅穿模）
                b.state.step = "B1_matches";
                Mark("matches_taken"); Mark("matches_packed");
                actor = Ground(Stove() + new Vector3(0.4f, 0f, -3.4f));
                actorEnd = Ground(Stove() + new Vector3(-0.95f, 0f, -1.55f));
                b.player.transform.position = actor;
                focusStart = Vector3.Lerp(actor, Stove(), 0.4f) + Vector3.up * 0.55f;
                focusEnd = Vector3.Lerp(actorEnd, Stove(), 0.55f) + Vector3.up * 0.55f;
                focus = focusStart;
                radius = 4.4f; radiusEnd = 3.9f;
                height = 1.95f; heightEnd = 1.75f;
                orbit = 160f; orbitDelta = 14f;
                break;

            case 3:
                // 3D 旧物台秤受潮盐霜翻转 + 吹散海盐霜特写（机位与角色位置承接上一镜，零跳切）
                actor = actorEnd = Ground(Stove() + new Vector3(-0.95f, 0f, -1.55f));
                b.player.transform.position = actor;
                b.player.FaceTowards(Stove());
                focus = focusStart = focusEnd = Vector3.Lerp(actor, Stove(), 0.55f) + Vector3.up * 0.55f;
                radius = 3.9f; radiusEnd = 3.7f;
                height = 1.75f; heightEnd = 1.68f;
                orbit = 174f; orbitDelta = 4f;
                b.state.seenMemories.Remove("scale_detail");
                ObjectInspectModal.Open("scale", "老台秤", "海盐霜下藏着三枚黄铜暗扣。", b.state, b.store, b.player, b.ui, b.gate);
                ObjectInspectModal.PrepareFilmingBack();
                break;

            case 4:
                // 撕下「柴火」抛出弧线 -> 地上长出滴水湿柴 -> 点火冒白烟失败（机位与角色位置 100% 连续承接）
                actor = Ground(Stove() + new Vector3(-0.95f, 0f, -1.55f));
                actorEnd = Ground(Stove() + new Vector3(-0.85f, 0f, -1.30f));
                b.player.transform.position = actor;
                b.player.FaceTowards(Stove());
                focusStart = Vector3.Lerp(actor, Stove(), 0.55f) + Vector3.up * 0.55f;
                focusEnd = Vector3.Lerp(actorEnd, Stove(), 0.60f) + Vector3.up * 0.50f;
                focus = focusStart;
                radius = 3.7f; radiusEnd = 3.6f;
                height = 1.68f; heightEnd = 1.62f;
                orbit = 178f; orbitDelta = 8f;
                words.OpenSpecificLetter(0);
                StyleLetterForTrailer(true);
                break;

            case 5:
                // 翻到另一封信撕「干」拼成「干木柴」再抛出 -> 烤炉轰然燃起暖火、烟囱冒炊烟（机位与角色位置 100% 连续承接）
                actor = actorEnd = Ground(Stove() + new Vector3(-0.85f, 0f, -1.30f));
                b.player.transform.position = actor;
                b.player.FaceTowards(Stove());
                focusStart = Vector3.Lerp(actor, Stove(), 0.60f) + Vector3.up * 0.50f;
                focusEnd = Stove() + Vector3.up * 1.15f;
                focus = focusStart;
                radius = 3.6f; radiusEnd = 4.6f;
                height = 1.62f; heightEnd = 2.25f;
                orbit = 186f; orbitDelta = -14f;
                words.OpenSpecificLetter(1);
                StyleLetterForTrailer(true);
                break;

            case 6:
                // 沿学校后院开阔草坪跑向空狗碗（彻底避开左侧校舍墙角，与后续两镜机位完全连续）
                b.state.step = "S1_slipper";
                b.water.SetLevel("L1", 3f);
                var bowl = mech.GetDogBowlPosition();
                var dogEarly = FindObjectOfType<DogFollower>();
                if (dogEarly != null) dogEarly.Hide();
                actor = Ground(bowl + new Vector3(-2.1f, 0f, -1.9f));
                actorEnd = Ground(bowl + new Vector3(-1.15f, 0f, -1.15f));
                b.player.transform.position = actor;
                focusStart = bowl + new Vector3(0.10f, 0.32f, 0.10f);
                focusEnd = bowl + new Vector3(0.15f, 0.32f, 0.15f);
                focus = focusStart;
                radius = 3.4f; radiusEnd = 3.3f;
                height = 1.68f; heightEnd = 1.62f;
                orbit = 216f; orbitDelta = 4f;
                break;

            case 7:
                // 心愿造物笺逐字写下「一根香喷喷的骨头」 -> 金光一闪骨头在空碗旁长出来（机位与角色 100% 连续承接）
                var bowlPos = mech.GetDogBowlPosition();
                actor = actorEnd = Ground(bowlPos + new Vector3(-1.15f, 0f, -1.15f));
                b.player.transform.position = actor;
                b.player.FaceTowards(bowlPos);
                focusStart = bowlPos + new Vector3(0.15f, 0.32f, 0.15f);
                focusEnd = bowlPos + new Vector3(0.18f, 0.28f, 0.18f);
                focus = focusStart;
                radius = 3.3f; radiusEnd = 3.15f;
                height = 1.62f; heightEnd = 1.48f;
                orbit = 220f; orbitDelta = 6f;
                if (wishCardGroup != null) wishCardGroup.alpha = 1f;
                break;

            case 8:
                // 小黑从滑梯底下探头、欢快跑向骨头与 EGGBOY、摇尾巴跟随（机位与角色 100% 连续承接，零墙体穿模）
                var bPos = mech.GetDogBowlPosition();
                actor = actorEnd = Ground(bPos + new Vector3(-1.15f, 0f, -1.15f));
                b.player.transform.position = actor;
                b.player.FaceTowards(bPos + new Vector3(1.2f, 0f, 1.2f));
                var dog = FindObjectOfType<DogFollower>();
                if (dog != null) { dog.ShowAt(dog.PeekPoint); dog.Follow(); }
                focusStart = bPos + new Vector3(0.18f, 0.28f, 0.18f);
                focusEnd = bPos + new Vector3(0.25f, 0.34f, 0.25f);
                focus = focusStart;
                radius = 3.15f; radiusEnd = 3.45f;
                height = 1.48f; heightEnd = 1.68f;
                orbit = 226f; orbitDelta = 6f;
                Pose("wave", ExprId.happy);
                break;

            case 9:
                // 涨潮渡口：一个人摇不动齿轮绞盘 -> 按下怀表 -> 金色十秒前残影跑来合力摇起浮桥
                b.state.step = "L0_cross";
                Mark("school_read");
                b.water.SetInstant(WaterLevel.LevelYs[2]);
                mech.DebugResetCrossing();
                var wPos = mech.WinchPosition;
                actor = actorEnd = Ground(wPos + new Vector3(-0.92f, 0f, -0.58f));
                b.player.locked = true;
                b.player.transform.position = actor;
                b.player.vel = Vector3.zero;
                b.player.FaceTowards(wPos);
                focusStart = wPos + Vector3.up * 0.55f;
                focusEnd = Vector3.Lerp(wPos, mech.GetCrossingChannelCenter(), 0.45f) + Vector3.up * 0.45f;
                focus = focusStart;
                radius = 5.2f; radiusEnd = 6.2f;
                height = 2.8f; heightEnd = 3.3f;
                orbit = 194f; orbitDelta = 20f;
                Pose("give", ExprId.nervous);
                mech.DebugPlayerHoldsWinch = true;
                break;

            case 10:
                // 举起小木尺第一人称取景框对准渡口断崖 -> 咔嗒拓印成桥 -> EGGBOY 迈步跑过木尺桥
                mech.DebugPlayerHoldsWinch = false;
                FindObjectOfType<TidalEchoCloneSystem>()?.Dismiss();
                SetupPrint();
                break;

            case 11:
                // 黄昏渐染：仰拍海岸高耸灯塔，EGGBOY 在塔顶露台展开守塔人老陈的信
                b.director.PrepareFilmingStage(9);
                b.director.StopAllCoroutines();
                var beaconOff = FindObjectOfType<BeaconLight>();
                if (beaconOff != null) beaconOff.SetEnabled(false);
                var lightData = LighthouseData.Load();
                Vector3 lampPos = LighthouseData.Point(lightData.lamp);
                if (beaconOff != null && beaconOff.pivot != null) lampPos = beaconOff.pivot.position;
                actor = actorEnd = b.player.transform.position;
                focusStart = lampPos + Vector3.down * 2.2f;
                focusEnd = lampPos + Vector3.down * 0.4f;
                focus = focusStart;
                radius = 10.5f; radiusEnd = 7.4f;
                height = 1.6f; heightEnd = 3.2f;
                orbit = 178f; orbitDelta = 26f;
                TownLighting.SetFilmingPhase(0.25f);
                trailerFill.intensity = 0.35f;
                Pose("look", ExprId.thinking);
                break;

            case 12:
                // 灯亮了！金色体积光束划破暮色海面，扫向远方山岸，小镇路灯齐亮
                TownLighting.SetFilmingPhase(0.92f);
                trailerFill.intensity = 0.42f;
                var beacon = FindObjectOfType<BeaconLight>();
                Vector3 cityTarget = EndingData.Point(EndingData.Load().city);
                if (beacon != null)
                {
                    beacon.SetEnabled(false);
                    beacon.ManualMode = true;
                    beacon.SetYawOffsetFrom(cityTarget, -52f);
                }
                Vector3 bLamp = LighthouseData.Point(LighthouseData.Load().lamp);
                if (beacon != null && beacon.pivot != null) bLamp = beacon.pivot.position;
                focusStart = bLamp + Vector3.down * 0.6f;
                focusEnd = bLamp + Vector3.down * 0.2f;
                focus = focusStart;
                radius = 11.5f; radiusEnd = 14.5f;
                height = 3.4f; heightEnd = 4.6f;
                orbit = 122f; orbitDelta = 34f;
                Pose("wave", ExprId.excited);
                break;

            case 13:
                // 对岸山头四十盏手电与新城窗灯一盏接一盏亮起，灯塔光束扫过暮色海湾遥相呼应
                TownLighting.SetFilmingPhase(0.96f);
                trailerFill.intensity = 0.45f;
                var beaconReply = FindObjectOfType<BeaconLight>();
                Vector3 cPos = EndingData.Point(EndingData.Load().city);
                if (beaconReply != null)
                {
                    beaconReply.TurnOn();
                    beaconReply.ManualMode = true;
                    beaconReply.SetYawOffsetFrom(cPos, -14f);
                    beaconReply.PulseFlash();
                }
                var reply = FindObjectOfType<FlashlightReply>();
                if (reply != null) reply.ResetForFilming();
                FindObjectOfType<CityGlow>()?.Show();
                Vector3 centerLights = cPos;
                float spanRadius = 21f;
                if (reply != null && reply.dots.Length > 0 && reply.dots[0] != null)
                {
                    var lb = reply.dots[0].bounds;
                    foreach (var dot in reply.dots) if (dot != null) lb.Encapsulate(dot.bounds);
                    centerLights = lb.center;
                    spanRadius = Mathf.Max(18f, lb.size.magnitude * 0.82f);
                }
                focusStart = centerLights + new Vector3(-2.5f, 0f, -1.5f);
                focusEnd = centerLights + new Vector3(2.5f, 0.4f, 1.5f);
                focus = focusStart;
                radius = spanRadius; radiusEnd = spanRadius * 0.88f;
                height = 8.6f; heightEnd = 7.2f;
                orbit = 208f; orbitDelta = 24f;
                break;

            case 14:
                // 黎明暖光：回到邮局前木桌读完给 EGGBOY 自己的最后一封信，在开阔庭院转身回望（零建筑穿模）
                b.director.StopAllCoroutines();
                b.state.step = "E1_read";
                Mark("dawn_shown");
                TownLighting.SetFilmingPhase(0.55f);
                trailerFill.intensity = 0.52f;
                var deskPos = EndingData.Point(EndingData.Load().desk);
                Actor(deskPos + new Vector3(-0.85f, 0f, -1.15f), deskPos);
                var dogEnd = FindObjectOfType<DogFollower>();
                if (dogEnd != null) { dogEnd.ShowAt(SafeStand(actor + new Vector3(-0.9f, 0f, 0.35f))); dogEnd.Follow(); }
                focusStart = actor + new Vector3(0.2f, 0.65f, 0.2f);
                focusEnd = actor + new Vector3(0.1f, 0.85f, 0.1f);
                focus = focusStart;
                radius = 3.5f; radiusEnd = 4.6f;
                height = 1.45f; heightEnd = 2.05f;
                orbit = 146f; orbitDelta = 16f;
                var letter = b.director.GetLetter("zhou_eggboy");
                if (letter != null) b.ui.ShowLetterDetail(letter.title, letter.body, letter.signature);
                StyleLetterForTrailer(true);
                Pose("read_letter", ExprId.surprised);
                break;

            case 15:
                // 全岛晨曦环绕航拍 + 右侧三件 Lux3D 生成资产实时 3D 旋转展示窗
                TownLighting.SetFilmingPhase(0.22f);
                trailerFill.intensity = 0.35f;
                var beaconAll = FindObjectOfType<BeaconLight>();
                if (beaconAll != null) { beaconAll.TurnOn(); beaconAll.ManualMode = false; }
                focusStart = new Vector3(-4f, 2.8f, -4f);
                focusEnd = new Vector3(1f, 3.2f, -2f);
                focus = focusStart;
                radius = 40f; radiusEnd = 43f;
                height = 26f; heightEnd = 29f;
                orbit = 172f; orbitDelta = 28f;
                Pose("wave", ExprId.happy);
                MakeTurntables();
                break;

            case 16:
                // 片尾暖纸铭牌柔和浮现（延续航拍轨道，无镜头跳切）
                TownLighting.SetFilmingPhase(0.20f);
                focusStart = new Vector3(1f, 3.2f, -2f);
                focusEnd = new Vector3(2f, 3.3f, -1.5f);
                focus = focusStart;
                radius = 43f; radiusEnd = 44f;
                height = 29f; heightEnd = 30f;
                orbit = 200f; orbitDelta = 8f;
                break;
        }

        if (viewfinder == null || !viewfinder.IsViewfinderActive)
            b.ui.SetCinematic(true, "trailer");
        Debug.Log("TRAILER_SCENE " + (s + 1) + " focus=" + focus + " actor=" + actor);
    }

    bool Once(int index, float at) { if (events[index] || u < at) return false; return events[index] = true; }

    void Direct()
    {
        b.player.idle = 0f;
        float ease = Mathf.SmoothStep(0f, 1f, u);
        focus = Vector3.Lerp(focusStart, focusEnd, ease);

        if (sceneIndex == 1)
        {
            if (Once(0, 0.10f)) Pose("hop", ExprId.happy);
            if (Once(1, 0.22f)) Pose("open_lighthouse_door", ExprId.happy);
            float walkU = Mathf.Clamp01((u - 0.24f) / 0.74f);
            StepActorAlong(actor, actorEnd, walkU);
            focus = b.player.transform.position + Vector3.up * 0.58f;
        }
        else if (sceneIndex == 2)
        {
            float walkU = Mathf.Clamp01(u / 0.50f);
            StepActorAlong(actor, actorEnd, walkU, Stove());
            if (Once(0, 0.52f))
            {
                words.OpenSpecificLetter(0);
                StyleLetterForTrailer(true);
                Pose("read_letter", ExprId.curious);
            }
            if (u >= 0.52f && u < 0.94f) StyleLetterForTrailer(true);
            if (Once(1, 0.94f)) b.ui.CloseLetter();
            focus = Vector3.Lerp(b.player.transform.position + Vector3.up * 0.55f, Stove() + Vector3.up * 0.65f, 0.45f * ease);
        }
        else if (sceneIndex == 3)
        {
            if (Once(0, 0.48f)) ObjectInspectModal.ExternalClearDust(true);
            if (Once(1, 0.94f)) ObjectInspectModal.DebugClose();
        }
        else if (sceneIndex == 4)
        {
            StyleLetterForTrailer(true);
            if (Once(0, 0.30f))
            {
                b.ui.CloseLetter();
                words.ThrowSlip("wood", Ground(Stove() + new Vector3(-0.75f, 0f, -0.85f)));
            }
            if (u >= 0.55f)
                StepActorAlong(actor, actorEnd, Mathf.Clamp01((u - 0.55f) / 0.24f), Stove());
            if (Once(1, 0.72f))
            {
                b.director.LightOven(FindObjectOfType<BakeryEffects>());
                Pose("look", ExprId.sad);
            }
        }
        else if (sceneIndex == 5)
        {
            StyleLetterForTrailer(true);
            if (Once(0, 0.30f))
            {
                words.Combine("dry", "wood");
                words.OpenSpecificLetter(1);
                StyleLetterForTrailer(true);
            }
            if (Once(1, 0.48f))
            {
                b.ui.CloseLetter();
                words.ThrowSlip("dry_wood", Ground(Stove() + new Vector3(-0.6f, 0f, -0.65f)));
            }
            if (Once(2, 0.66f))
            {
                Mark("oven_lit");
                var fx = FindObjectOfType<BakeryEffects>();
                fx?.Light();
                b.director.BoostOvenBellows(fx);
                Pose("hop", ExprId.excited);
            }
        }
        else if (sceneIndex == 6)
        {
            var bowl = mech.GetDogBowlPosition();
            float walkU = Mathf.Clamp01(u / 0.66f);
            StepActorAlong(actor, actorEnd, walkU, bowl);
            if (Once(0, 0.68f))
            {
                words.OpenSpecificLetter(1);
                StyleLetterForTrailer(true);
                Pose("inspect_memory", ExprId.curious);
            }
            if (u >= 0.68f && u < 0.95f) StyleLetterForTrailer(true);
            if (Once(1, 0.95f)) b.ui.CloseLetter();
        }
        else if (sceneIndex == 7)
        {
            string wish = "一根香喷喷的骨头";
            int nChars = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01((u - 0.10f) / 0.44f) * (wish.Length + 1)), 0, wish.Length);
            if (wishTypedLabel != null)
                wishTypedLabel.text = nChars > 0 ? ("「 " + wish.Substring(0, nChars) + (u < 0.56f ? " | 」" : " 」")) : "「 | 」";
            if (wishCardGroup != null)
                wishCardGroup.alpha = u < 0.58f ? Mathf.Clamp01(u * 6f) : Mathf.Clamp01(1f - (u - 0.58f) * 8f);
            if (Once(0, 0.58f))
            {
                var agent = FindObjectOfType<AIAgentCalibrationDirector>();
                agent.SpawnLiveWishObjectInWorld(wish);
                b.director.StopAllCoroutines();
                if (agent.LastWishSpawn != null && agent.LastWishSpawn.root != null)
                {
                    var glow = new GameObject("WishBoneWarmGlow").AddComponent<Light>();
                    glow.transform.SetParent(agent.LastWishSpawn.root.transform, false);
                    glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                    glow.type = LightType.Point;
                    glow.color = new Color(1f, 0.82f, 0.42f);
                    glow.range = 4.2f;
                    glow.intensity = 2.2f;
                }
                Pose("hop", ExprId.excited);
            }
        }
        else if (sceneIndex == 8)
        {
            // Keep focus centered on bowl (focusStart/focusEnd) so school wall stays out of frame
        }
        else if (sceneIndex == 9)
        {
            if (Once(0, 0.34f))
            {
                var echo = FindObjectOfType<TidalEchoCloneSystem>();
                echo.DebugClear();
                Vector3 targetHold = Ground(mech.WinchPosition + new Vector3(0.85f, 0f, 0.28f));
                Vector3 startWalk = Ground(targetHold + new Vector3(1.5f, 0f, -1.4f));
                Vector3 walkDir = targetHold - startWalk;
                walkDir.y = 0f;
                float walkYaw = walkDir.sqrMagnitude > 0.01f ? Mathf.Atan2(walkDir.x, walkDir.z) : b.player.yaw;
                for (int k = 0; k < 24; k++)
                {
                    float fk = k / 23f;
                    Vector3 p = Vector3.Lerp(startWalk, targetHold, fk);
                    p.y = b.world.GroundAt(p.x, p.z, targetHold.y + 1f);
                    echo.DebugPushFrame(p, walkYaw, walkDir.normalized * 2.8f, true, "");
                }
                Vector3 faceWinch = mech.WinchPosition - targetHold;
                float holdYaw = Mathf.Atan2(faceWinch.x, faceWinch.z);
                for (int k = 0; k < 140; k++)
                    echo.DebugPushFrame(targetHold, holdYaw, Vector3.zero, true, "winch");
                echo.Summon();
                Pose("give", ExprId.happy);
            }
            if (Once(1, 0.68f))
            {
                mech.RaiseDockWinch(true, false);
                Pose("hop", ExprId.excited);
            }
        }
        else if (sceneIndex == 10)
        {
            if (u < 0.15f)
            {
                StepActorAlong(actor, actorEnd, u / 0.15f, bridgeWalkB);
            }
            else if (Once(0, 0.15f))
            {
                OpenRulerViewfinder();
            }
            if (printAim && viewfinder != null && viewfinder.IsViewfinderActive && u >= 0.15f && u < 0.62f)
            {
                float aimU = (u - 0.15f) / 0.47f;
                float offset = (1f - Mathf.SmoothStep(0f, 1f, aimU)) * 3.2f * Mathf.Cos(aimU * 4.5f);
                viewfinder.DebugSetView(printYaw + offset, printPitch, printHold, printHeldYaw);
            }
            if (Once(1, 0.62f) && printAim)
            {
                viewfinder.DebugSetView(printYaw, printPitch, printHold, printHeldYaw);
                var printed = viewfinder.Shutter();
                if (printed != null)
                {
                    var pb = Lux3DModelFit.WorldBounds(printed);
                    mech.GapEndpoints(out var ga, out var gbb);
                    Vector3 gdir = gbb - ga; gdir.y = 0f;
                    if (gdir.sqrMagnitude > 0.01f) gdir.Normalize();
                    float halfLen = Mathf.Max(1.1f, Mathf.Max(pb.extents.x, pb.extents.z) * 0.72f);
                    float topY = pb.max.y + 0.02f;
                    bridgeWalkA = new Vector3(pb.center.x, topY, pb.center.z) - gdir * halfLen;
                    bridgeWalkB = new Vector3(pb.center.x, topY, pb.center.z) + gdir * halfLen;
                }
                viewfinder.CloseViewfinder();
                b.ui.SetCinematic(true, "trailer");
                b.player.locked = true;
            }
            if (u >= 0.64f)
            {
                float crossU = Mathf.Clamp01((u - 0.64f) / 0.34f);
                Vector3 raw = Vector3.Lerp(bridgeWalkA, bridgeWalkB, Mathf.SmoothStep(0f, 1f, crossU));
                Vector3 dir = bridgeWalkB - bridgeWalkA;
                dir.y = 0f;
                float yaw = dir.sqrMagnitude > 0.01f ? Mathf.Atan2(dir.x, dir.z) : b.player.yaw;
                b.player.PuppetStep(raw, yaw, crossU < 0.98f ? dir.normalized * 2.6f : Vector3.zero, true, 1f / 30f);
                focus = Vector3.Lerp(bridgeWalkA, bridgeWalkB, 0.5f) + Vector3.up * 0.35f;
            }
        }
        else if (sceneIndex == 11)
        {
            TownLighting.SetFilmingPhase(Mathf.Lerp(0.25f, 0.88f, ease));
            if (Once(0, 0.50f))
            {
                words.OpenSpecificLetter(2);
                StyleLetterForTrailer(true);
                Pose("read_letter", ExprId.thinking);
            }
            if (u >= 0.50f && u < 0.94f) StyleLetterForTrailer(true);
            if (Once(1, 0.94f)) b.ui.CloseLetter();
        }
        else if (sceneIndex == 12)
        {
            var beacon = FindObjectOfType<BeaconLight>();
            if (Once(0, 0.06f) && beacon != null)
            {
                beacon.TurnOn();
                beacon.PulseFlash();
            }
            if (beacon != null && beacon.On)
                beacon.RotateYaw(56f / Mathf.Max(30f, (manifest.scenes[12].end_sec - manifest.scenes[12].start_sec) * 30f));
        }
        else if (sceneIndex == 13)
        {
            var reply = FindObjectOfType<FlashlightReply>();
            if (reply != null)
            {
                int lit = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(u * 1.25f) * 40f), 1, 40);
                reply.RevealUpTo(lit);
                for (int i = 0; i < reply.dots.Length; i++)
                {
                    var d = reply.dots[i];
                    if (d != null && d.enabled)
                    {
                        float pulse = 0.85f + 0.22f * Mathf.Sin(t * 3.4f + i * 1.3f);
                        d.transform.localScale = Vector3.one * (0.95f * pulse);
                    }
                }
            }
            FindObjectOfType<BeaconLight>()?.RotateYaw(0.22f);
        }
        else if (sceneIndex == 14)
        {
            StyleLetterForTrailer(true);
            if (Once(0, 0.52f))
            {
                b.ui.CloseLetter();
                b.player.FaceTowards(new Vector3(0f, 2f, -6f));
                Pose(b.player.HasClip("signal_wave") ? "signal_wave" : "wave", ExprId.happy);
            }
        }

        foreach (var tab in turntables)
            if (tab != null) tab.Rotate(0f, 0.85f, 0f, Space.World);
        if (assetsGroup != null)
            assetsGroup.alpha = sceneIndex == 15 ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 3.5f)) : 0f;

        if (manifest.scenes.Length >= 1)
        {
            float s0End = manifest.scenes[0].end_sec;
            float titleStart = Mathf.Max(0.8f, s0End - 3.6f);
            titleGroup.alpha = (t >= titleStart && t < s0End)
                ? Mathf.Clamp01(Mathf.Min((t - titleStart) * 2.2f, (s0End - t) * 2.5f))
                : 0f;
        }

        int endIdx = manifest.scenes.Length - 1;
        float endStart = manifest.scenes[endIdx].start_sec;
        endGroup.alpha = t >= endStart ? Mathf.SmoothStep(0f, 1f, (t - endStart) * 2.2f) : 0f;

        CleanHud();
    }

    void LateUpdate()
    {
        if (cam == null || sceneIndex < 0) return;
        if (sceneIndex == 3 && ObjectInspectModal.IsOpen) return;
        if (sceneIndex == 10 && viewfinder != null && viewfinder.IsViewfinderActive) return;

        float ease = Mathf.SmoothStep(0f, 1f, u);
        float a = (orbit + orbitDelta * ease) * Mathf.Deg2Rad;
        float r = Mathf.Lerp(radius, radiusEnd, ease);
        float h = Mathf.Lerp(height, heightEnd, ease);
        Vector3 camPos = focus + new Vector3(Mathf.Sin(a) * r, h + Mathf.Sin(t * 0.35f) * 0.06f, Mathf.Cos(a) * r);
        Vector3 lookAt = focus + Vector3.up * 0.35f;
        cam.transform.position = camPos;
        cam.transform.LookAt(lookAt);
        cam.fieldOfView = (sceneIndex == 15 || sceneIndex == 16) ? 43f : 47f;
        if (trailerFill != null && trailerFill.intensity > 0.01f)
            trailerFill.transform.rotation = Quaternion.LookRotation(lookAt - camPos, Vector3.up);
    }

    void SetupPrint()
    {
        mech.DebugResetCrossing();
        mech.GapEndpoints(out var a, out var bb);
        var ab = bb - a;
        ab.y = 0f;
        Vector3 dir = ab.sqrMagnitude > 0.01f ? ab.normalized : Vector3.forward;
        actorEnd = SafeStand(a - dir * 1.05f);
        actor = SafeStand(a - dir * 2.4f);
        bridgeWalkA = actorEnd;
        bridgeWalkB = SafeStand(bb + dir * 0.9f);
        b.player.transform.position = actor;
        b.player.FaceTowards(bb);
        focusStart = actor + Vector3.up * 0.5f;
        focusEnd = (a + bb) * 0.5f + Vector3.up * 0.4f;
        focus = focusStart;
        radius = 4.6f; radiusEnd = 5.4f;
        height = 2.3f; heightEnd = 2.6f;
        orbit = 205f; orbitDelta = 22f;
    }

    void OpenRulerViewfinder()
    {
        mech.GapEndpoints(out var a, out var bb);
        var ab = bb - a;
        ab.y = 0f;
        b.player.transform.position = actorEnd;
        b.player.locked = false;
        b.player.FaceTowards(bb);
        b.ui.SetCinematic(false, "trailer");
        viewfinder.OpenViewfinder();
        viewfinder.DebugSelect("ruler");
        printYaw = Mathf.Atan2(ab.x, ab.z) * Mathf.Rad2Deg;
        printAim = false;
        for (float hy = 0f; hy <= 90f && !printAim; hy += 90f)
        for (float hold = 0.35f; hold <= 1.1f && !printAim; hold += 0.05f)
        for (float pitch = 5f; pitch <= 50f && !printAim; pitch += 1.5f)
        {
            viewfinder.DebugSetView(printYaw, pitch, hold, hy);
            if (!viewfinder.AimValid) continue;
            var hb = Lux3DModelFit.WorldBounds(viewfinder.DebugHeldModel);
            var cp = cam.transform.position;
            var pb = new Bounds(cp + (hb.center - cp) * viewfinder.AimScale, hb.size * viewfinder.AimScale);
            pb.Expand(new Vector3(1.1f, 8f, 1.1f));
            if (!pb.Contains(new Vector3(a.x, pb.center.y, a.z)) || !pb.Contains(new Vector3(bb.x, pb.center.y, bb.z))) continue;
            printAim = true;
            printPitch = pitch;
            printHold = hold;
            printHeldYaw = hy;
        }
        Debug.Log("TRAILER_PRINT aim=" + printAim);
    }

    void CleanHud()
    {
        foreach (var rt in b.ui.GetComponentsInChildren<RectTransform>(true))
        {
            string n = rt.name.ToLowerInvariant();
            if (n == "wishpromptinputbg") continue;
            if (n.Contains("prompt") || n.Contains("tutorial") || n.Contains("toast") || n.Contains("goal") ||
                n == "keys" || n.Contains("debug") || n.Contains("signalcard") ||
                n.Contains("achievementdiducard") || n.Contains("achievementgallery"))
                rt.gameObject.SetActive(false);
        }
        Cursor.visible = false;
    }

    Text Label(Transform parent, string text, int size, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject("Trailer typography", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = dimensions;
        var label = go.GetComponent<Text>();
        label.font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
        label.fontSize = size;
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.26f, 0.19f, 0.12f);
        label.raycastTarget = false;
        return label;
    }

    void MakeTitles()
    {
        var go = new GameObject("Trailer film titles", typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        title = Label(go.transform, "记忆邮局", 64, new Vector2(0f, 330f), new Vector2(1500f, 120f));
        title.color = new Color(1f, 0.94f, 0.80f);
        var sh = title.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0.18f, 0.11f, 0.05f, 0.85f);
        sh.effectDistance = new Vector2(2f, -3f);
        titleGroup = title.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;

        var end = new GameObject("Parchment end card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        end.transform.SetParent(go.transform, false);
        var rt = end.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        paper = end.GetComponent<Image>();
        paper.color = new Color(0.94f, 0.88f, 0.75f, 0.96f);
        endGroup = end.GetComponent<CanvasGroup>();
        endGroup.alpha = 0f;
        Label(end.transform, "记忆邮局", 74, new Vector2(0f, 120f), new Vector2(1600f, 140f));
        Label(end.transform, "队名：收信人    ·    #Aholo黑客松", 32, new Vector2(0f, -24f), new Vector2(1600f, 80f));
        Label(end.transform, "https://github.com/Abigale365/eggboy-still-delivering", 27, new Vector2(0f, -140f), new Vector2(1800f, 80f));
    }

    void MakeWishCard()
    {
        var go = new GameObject("Trailer wish card", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        wishCardGroup = go.GetComponent<CanvasGroup>();
        wishCardGroup.alpha = 0f;

        var card = new GameObject("CardBg", typeof(RectTransform), typeof(Image), typeof(Shadow));
        card.transform.SetParent(go.transform, false);
        var rt = card.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(470f, 165f);
        rt.sizeDelta = new Vector2(560f, 210f);
        var img = card.GetComponent<Image>();
        img.sprite = TideTownStyleUI.RoundedBorderSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.96f, 0.91f, 0.80f, 0.96f);
        var sh = card.GetComponent<Shadow>();
        sh.effectColor = new Color(0.2f, 0.12f, 0.05f, 0.45f);
        sh.effectDistance = new Vector2(4f, -6f);

        var header = Label(card.transform, "心愿 3D 造物笺  ·  Lux3D", 24, new Vector2(0f, 54f), new Vector2(500f, 44f));
        header.color = new Color(0.56f, 0.36f, 0.16f);
        wishTypedLabel = Label(card.transform, "「 | 」", 34, new Vector2(0f, -14f), new Vector2(520f, 68f));
        wishTypedLabel.color = new Color(0.24f, 0.16f, 0.09f);
        var sub = Label(card.transform, "写下心愿，从纸上长成 3D 实物", 19, new Vector2(0f, -68f), new Vector2(500f, 34f));
        sub.color = new Color(0.52f, 0.40f, 0.27f);
    }

    void MakeTurntables()
    {
        if (assetsGroup != null) return;
        var root = new GameObject("Lux3D rotating artifacts", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 900;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        assetsGroup = root.GetComponent<CanvasGroup>();

        string[] names = { "B1_post_office_visual", "B4_lighthouse_visual", "" };
        string[] captions = { "Lux3D · 潮汐邮局", "Lux3D · 海角灯塔", "Lux3D · 心愿骨头" };
        for (int i = 0; i < 3; i++)
        {
            GameObject model = null;
            if (i == 2)
            {
                var wish = FindObjectOfType<AIAgentCalibrationDirector>()?.LastWishSpawn;
                if (wish != null && wish.root != null) model = Instantiate(wish.root);
            }
            else
            {
                var source = GameObject.Find(names[i]);
                if (source != null) model = Instantiate(source);
            }
            if (model == null) continue;

            var holder = new GameObject("Lux3D studio " + i).transform;
            holder.position = new Vector3(1000f + i * 14f, 1000f, 1000f);
            model.transform.SetParent(holder, false);
            model.transform.localPosition = Vector3.zero;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>()) Destroy(script);
            foreach (var l in model.GetComponentsInChildren<Light>()) Destroy(l);
            var bounds = Lux3DModelFit.WorldBounds(model);
            float maxDim = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            model.transform.localScale *= 1.85f / maxDim;
            bounds = Lux3DModelFit.WorldBounds(model);
            model.transform.position += holder.position - bounds.center;
            turntables[i] = holder;

            var cc = new GameObject("Artifact camera " + i).AddComponent<Camera>();
            cc.transform.position = holder.position + new Vector3(0f, 0.55f, -3.4f);
            cc.transform.LookAt(holder.position);
            cc.clearFlags = CameraClearFlags.SolidColor;
            cc.backgroundColor = new Color(0.95f, 0.90f, 0.79f);
            cc.fieldOfView = 38f;
            var tex = new RenderTexture(340, 224, 16);
            cc.targetTexture = tex;

            var frameCard = new GameObject("Artifact card " + i, typeof(RectTransform), typeof(Image), typeof(Shadow));
            frameCard.transform.SetParent(root.transform, false);
            var frt = frameCard.GetComponent<RectTransform>();
            frt.anchorMin = frt.anchorMax = new Vector2(1f, 0.5f);
            frt.pivot = new Vector2(1f, 0.5f);
            frt.anchoredPosition = new Vector2(-42f, 260f - i * 252f);
            frt.sizeDelta = new Vector2(356f, 238f);
            frameCard.GetComponent<Image>().color = new Color(0.97f, 0.92f, 0.82f, 0.96f);
            var fsh = frameCard.GetComponent<Shadow>();
            fsh.effectColor = new Color(0.2f, 0.12f, 0.05f, 0.40f);
            fsh.effectDistance = new Vector2(3f, -5f);

            var panel = new GameObject("Artifact view", typeof(RectTransform), typeof(RawImage));
            panel.transform.SetParent(frameCard.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 12f);
            rt.sizeDelta = new Vector2(336f, 194f);
            panel.GetComponent<RawImage>().texture = tex;

            var cap = Label(frameCard.transform, captions[i], 20, new Vector2(0f, -98f), new Vector2(330f, 32f));
            cap.color = new Color(0.35f, 0.24f, 0.14f);

            var fill = new GameObject("Artifact softbox " + i).AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 8f;
            fill.intensity = 3.1f;
            fill.transform.position = holder.position + new Vector3(-1.2f, 2.2f, -2.2f);
        }
    }

    void OnDestroy()
    {
        Time.captureFramerate = 0;
        AudioListener.volume = 1f;
        try { pipe?.Close(); } catch (Exception) { }
        if (trailerFill != null) Destroy(trailerFill.gameObject);
        if (capture != null) { capture.Release(); Destroy(capture); }
        if (pixels != null) Destroy(pixels);
    }
}
