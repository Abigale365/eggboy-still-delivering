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

    public void Start()
    {
        try
        {
            var devices = Microphone.devices;
            if (devices == null || devices.Length == 0) return;
            device = devices[0];
            clip = Microphone.Start(device, true, 1, 16000);
        }
        catch (Exception) { Stop(); }
    }

    public float ReadRms()
    {
        if (clip == null) return -1f;
        try
        {
            int offset = Microphone.GetPosition(device) - samples.Length;
            if (offset < 0 || !clip.GetData(samples, offset)) return -1f;
        }
        catch (Exception)
        {
            Stop();
            return -1f;
        }

        float sum = 0f;
        foreach (float sample in samples) sum += sample * sample;
        return Mathf.Sqrt(sum / samples.Length);
    }

    public void Stop()
    {
        if (clip == null) return;
        try { Microphone.End(device); }
        catch (Exception) { }
        UnityEngine.Object.Destroy(clip);
        clip = null;
        device = null;
    }
#endif
}
