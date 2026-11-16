#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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

    void Awake()
    {
        actions = gameObject.AddComponent<EasyInputBridge>();
        focused = Application.isFocused;
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
    void OnApplicationQuit() { StopHelper(); }
}
#endif
