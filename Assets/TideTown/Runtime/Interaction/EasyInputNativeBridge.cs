#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UnityEngine;

// The helper owns Windows BLE/USB APIs. Only this main-thread component touches game input.
[DefaultExecutionOrder(-10000)]
public sealed class EasyInputNativeBridge : MonoBehaviour
{
    UdpClient receiver;
    Process helper;
    EventWaitHandle stopSignal;
    EasyInputBridge actions;
    readonly EasyInputReportDecoder decoder = new EasyInputReportDecoder();
    byte[] token;
    float lastHeartbeat;
    float retryAt;
    bool focused;
    bool connected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        var root = new GameObject("EasyInputNativeBridge");
        DontDestroyOnLoad(root);
        root.AddComponent<EasyInputNativeBridge>();
    }

    static bool IsBatchOrCaptureRun()
    {
        var args = Environment.GetCommandLineArgs();
        return Array.Exists(args, a =>
            a.Equals("-trailerRecord", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-audiocheck", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-batchmode", StringComparison.OrdinalIgnoreCase));
    }

    void Awake()
    {
        actions = gameObject.AddComponent<EasyInputBridge>();
        focused = Application.isFocused;
        if (!IsBatchOrCaptureRun())
            TryPushUsbConfig(TideTownHoldConfigJson);
        StartHelper();
    }

    void StartHelper()
    {
        retryAt = Time.unscaledTime + 10f;
        string executable = Path.Combine(Application.streamingAssetsPath, "EasyInputNative", "EasyInputRelay.exe");
        if (!File.Exists(executable))
        {
            UnityEngine.Debug.LogWarning("EasyInput helper missing: " + executable);
            return;
        }
        try
        {
            token = Guid.NewGuid().ToByteArray();
            receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            receiver.Client.Blocking = false;
            int port = ((IPEndPoint)receiver.Client.LocalEndPoint).Port;
            string secret = BitConverter.ToString(token).Replace("-", "");
            string stopEvent = "Local\\TideTown-EasyInput-" + secret;
            stopSignal = new EventWaitHandle(false, EventResetMode.ManualReset, stopEvent);
            string address = Environment.GetEnvironmentVariable("TIDETOWN_EASYINPUT_ADDRESS");
            string addressArgument = "";
            if (!string.IsNullOrEmpty(address))
            {
                // A Bluetooth address is the only accepted environment override.
                string compact = address.Replace(":", "");
                if (compact.Length == 12 && ulong.TryParse(compact,
                    System.Globalization.NumberStyles.HexNumber, null, out ulong ignored))
                    addressArgument = " --address " + compact;
            }
            string log = Path.Combine(Application.persistentDataPath, "easyinput-native.log");
            helper = Process.Start(new ProcessStartInfo(executable,
                "--port " + port + " --token " + secret + " --parent " + Process.GetCurrentProcess().Id +
                " --stop-event " + stopEvent + " --log \"" + log + "\"" + addressArgument)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executable)
            });
            lastHeartbeat = Time.unscaledTime;
        }
        catch (Exception error)
        {
            UnityEngine.Debug.LogWarning("EasyInput helper: " + error.Message);
            StopHelper();
        }
    }

    void Update()
    {
        if (helper == null || helper.HasExited)
        {
            if (Time.unscaledTime >= retryAt)
            {
                StopHelper();
                StartHelper();
            }
            return;
        }
        // Drop traffic while unfocused. Limit work per frame even under a faulty sender.
        for (int i = 0; i < 128 && receiver.Available > 0; i++)
        {
            IPEndPoint sender = null;
            byte[] packet;
            try { packet = receiver.Receive(ref sender); }
            catch (SocketException) { break; }
            if (packet.Length < 17 || !IPAddress.IsLoopback(sender.Address)) continue;
            bool authenticated = true;
            for (int j = 0; j < token.Length; j++)
                authenticated &= packet[j] == token[j];
            if (!authenticated) continue;
            if (packet[16] == 0)
            {
                ClearInput();
                continue;
            }
            if (packet[16] == 1)
            {
                connected = true;
                lastHeartbeat = Time.unscaledTime;
                continue;
            }
            if (packet[16] != 2 || !connected || !focused) continue;
            byte[] report = new byte[packet.Length - 17];
            Buffer.BlockCopy(packet, 17, report, 0, report.Length);
            string action = decoder.Decode(report, report.Length);
            if (action != null) actions.OnAction(action);
        }
        if (connected && Time.unscaledTime - lastHeartbeat > 1.5f) ClearInput();
    }

    void ClearInput()
    {
        connected = false;
        decoder.Reset();
        if (actions != null) actions.ResetInput("");
    }

    void OnApplicationFocus(bool value)
    {
        focused = value;
        // Empty queued input on both focus transitions before accepting fresh events.
        if (receiver != null)
        {
            IPEndPoint sender = null;
            for (int i = 0; i < 1024 && receiver.Available > 0; i++)
                receiver.Receive(ref sender);
        }
        ClearInput();
    }

    void StopHelper()
    {
        ClearInput();
        if (helper != null)
        {
            stopSignal?.Set();
            try { if (!helper.HasExited && !helper.WaitForExit(1000)) helper.Kill(); }
            catch (InvalidOperationException) { }
            helper.Dispose();
            helper = null;
        }
        stopSignal?.Dispose();
        stopSignal = null;
        receiver?.Close();
        receiver = null;
    }

    void OnDestroy() { StopHelper(); }
    void OnApplicationQuit()
    {
        StopHelper();
        if (!IsBatchOrCaptureRun())
            TryPushUsbConfig(DefaultShortPressConfigJson);
    }

    const string TideTownHoldConfigJson =
        "{\"schema\":\"ai_keyboard.v1\",\"target_platform\":\"windows\",\"audio_enabled\":true,\"ptt_hotkey\":\"Fn\",\"ptt_hotkey_source\":\"custom\",\"profiles\":[{\"id\":\"default\",\"keys\":{\"KEY1\":{\"press\":{\"hotkey\":\"W\"}},\"KEY2\":{\"press\":{\"hotkey\":\"A\"}},\"KEY3\":{\"press\":{\"hotkey\":\"S\"}},\"KEY4\":{\"press\":{\"hotkey\":\"D\"}},\"KEY5\":{\"press\":{\"hotkey\":\"E\"}},\"KEY6\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000006\"},\"KEY7\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000007\"},\"KEY8\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000008\"}},\"encoder\":{\"left\":\"host_action:c0ffee00-0000-4000-8000-000000000009\",\"right\":\"host_action:c0ffee00-0000-4000-8000-00000000000a\",\"press\":\"voice_ptt_hold\"}}]}";

    const string DefaultShortPressConfigJson =
        "{\"schema\":\"ai_keyboard.v1\",\"target_platform\":\"windows\",\"audio_enabled\":false,\"profiles\":[{\"id\":\"default\",\"keys\":{\"KEY1\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000001\"},\"KEY2\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000002\"},\"KEY3\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000003\"},\"KEY4\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000004\"},\"KEY5\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000005\"},\"KEY6\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000006\"},\"KEY7\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000007\"},\"KEY8\":{\"press\":\"host_action:c0ffee00-0000-4000-8000-000000000008\"}},\"encoder\":{\"left\":\"host_action:c0ffee00-0000-4000-8000-000000000009\",\"right\":\"host_action:c0ffee00-0000-4000-8000-00000000000a\",\"press\":\"host_action:c0ffee00-0000-4000-8000-00000000000b\"}}]}";

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] static extern bool HidD_SetFeature(SafeFileHandle handle, byte[] data, int length);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr hwndParent, int flags);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int detailSize, out int requiredSize, IntPtr deviceInfoData);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    static ushort ComputeCrc16(byte[] bytes)
    {
        ushort crc = 0xFFFF;
        for (int i = 0; i < bytes.Length; i++)
        {
            crc ^= (ushort)(bytes[i] << 8);
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    static void TryPushUsbConfig(string json)
    {
        try
        {
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
            if (set == new IntPtr(-1)) return;
            string foundPath = null;
            try
            {
                for (int index = 0; ; index++)
                {
                    var data = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA)) };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref data))
                    {
                        if (Marshal.GetLastWin32Error() == 259) break;
                        continue;
                    }
                    SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out int required, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        string lower = path.ToLowerInvariant();
                        if (lower.Contains("vid_303a&pid_1006") && lower.Contains("col03")) { foundPath = path; break; }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            if (string.IsNullOrEmpty(foundPath)) return;

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ushort crc = ComputeCrc16(bytes);
            int totalChunks = (bytes.Length + 51) / 52;
            using (var handle = CreateFile(foundPath, 0xC0000000u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero))
            {
                if (handle.IsInvalid) return;
                for (int index = 0; index < totalChunks; index++)
                {
                    int offset = index * 52;
                    int chunkLen = Math.Min(52, bytes.Length - offset);
                    byte[] report = new byte[64];
                    report[0] = 0x10;
                    report[1] = (byte)'S';
                    report[2] = (byte)'3';
                    report[3] = (byte)'C';
                    report[4] = 1;
                    report[5] = (byte)index;
                    report[6] = (byte)totalChunks;
                    report[7] = (byte)(bytes.Length & 0xFF);
                    report[8] = (byte)((bytes.Length >> 8) & 0xFF);
                    report[9] = (byte)chunkLen;
                    report[10] = (byte)(crc & 0xFF);
                    report[11] = (byte)((crc >> 8) & 0xFF);
                    Buffer.BlockCopy(bytes, offset, report, 12, chunkLen);
                    HidD_SetFeature(handle, report, report.Length);
                }
            }
        }
        catch (Exception) { }
    }
}
#endif
