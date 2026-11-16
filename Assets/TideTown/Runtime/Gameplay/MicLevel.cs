using System;
using System.Runtime.InteropServices;
using UnityEngine;

internal interface IMicLevel
{
    void Start();
    float ReadRms();  // 负值表示尚未取得音频采样。
    void Stop();
}

internal sealed class PlatformMicLevel : IMicLevel
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void TideTown_StartMic();
    [DllImport("__Internal")] static extern float TideTown_ReadMicRms();
    [DllImport("__Internal")] static extern void TideTown_StopMic();

    public void Start() => TideTown_StartMic();
    public float ReadRms() => TideTown_ReadMicRms();
    public void Stop() => TideTown_StopMic();
#else
    readonly float[] samples = new float[512];
    AudioClip clip;
    string device;
    string[] candidates = new string[0];
    int candidate;
    float lastSignal, retryAt;
    bool started;
    static string preferred;

    static int Rank(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("virtual") || n.Contains("broadcast") || n.Contains("voicemeeter") || n.Contains("stereo mix") || n.Contains("立体声混音")) return 20;
        if (n.Contains("array") || n.Contains("阵列") || n.Contains("usb") || n.Contains("microphone") || n.Contains("麦克风")) return 0;
        return 5;
    }

    void NextDevice()
    {
        ReleaseDevice();
        candidates = Microphone.devices ?? new string[0];
        Array.Sort(candidates, (a, b) => Rank(a).CompareTo(Rank(b)));
        retryAt = Time.realtimeSinceStartup + 3f;
        if (candidates.Length == 0) return;
        candidate = (candidate + 1) % candidates.Length;
        device = candidates[candidate];
        try { clip = Microphone.Start(device, true, 1, 16000); }
        catch (Exception e) { Debug.LogWarning("MIC_DEVICE " + device + ": " + e.GetType().Name); }
        lastSignal = Time.realtimeSinceStartup;
        Debug.Log("MIC_DEVICE sampling=" + device);
    }

    public void Start()
    {
        try
        {
            started = true;
            candidates = Microphone.devices ?? new string[0];
            Array.Sort(candidates, (a, b) => Rank(a).CompareTo(Rank(b)));
            candidate = Array.IndexOf(candidates, preferred) - 1;
            if (candidate < -1) candidate = -1;
            NextDevice();
        }
        catch (Exception) { Stop(); }
    }

    public float ReadRms()
    {
        float now = Time.realtimeSinceStartup;
        if (started && now >= retryAt && (clip == null || now - lastSignal >= 3f)) NextDevice();
        if (clip == null) return -1f;
        try
        {
            int pos = Microphone.GetPosition(device);
            if (pos <= 0) return -1f;
            int offset = (pos - samples.Length + clip.samples) % clip.samples;
            if (!clip.GetData(samples, offset)) return -1f;
        }
        catch (Exception)
        {
            ReleaseDevice();
            return -1f;
        }

        float sum = 0f;
        foreach (float sample in samples) sum += sample * sample;
        float rms = Mathf.Sqrt(sum / samples.Length);
        if (rms > 0.0007f) { lastSignal = now; preferred = device; }
        return rms;
    }

    public void Stop()
    {
        started = false;
        ReleaseDevice();
    }

    void ReleaseDevice()
    {
        try { if (device != null) Microphone.End(device); }
        catch (Exception) { }
        if (clip != null) UnityEngine.Object.Destroy(clip);
        clip = null;
        device = null;
    }
#endif
}
