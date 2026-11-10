// 程序合成的短音效（全游戏都在用）。
// 2026-11-11：v3 的"光影 + 八音盒"弹窗已拆掉，换成世界里的 ShadowLanternPuzzle 和 SoundBottleSystem（见 ShadowSoundPuzzles.cs）。
// 这个类只保留音效和给旧入口用的空接口。音效按音高 / 时长缓存，不再每响一声都新建一段 AudioClip。
using System.Collections.Generic;
using UnityEngine;

public sealed class ShadowAndChimeSystem : MonoBehaviour
{
    static AudioSource sharedAudio;
    static readonly Dictionary<int, AudioClip> chimeCache = new Dictionary<int, AudioClip>();

    static AudioSource Audio()
    {
        if (sharedAudio == null)
        {
            var go = new GameObject("ProceduralMusicBoxAudio");
            DontDestroyOnLoad(go);
            sharedAudio = go.AddComponent<AudioSource>();
            sharedAudio.spatialBlend = 0f;
            sharedAudio.volume = 0.42f;
        }
        return sharedAudio;
    }

    public static void PlayProceduralChime(float freqHz, float durationSec = 0.42f)
    {
        try
        {
            int key = Mathf.RoundToInt(freqHz * 10f) * 1000 + Mathf.RoundToInt(durationSec * 100f);
            if (!chimeCache.TryGetValue(key, out var clip) || clip == null)
            {
                int sampleRate = 22050;
                int samples = Mathf.Max(512, Mathf.RoundToInt(sampleRate * durationSec));
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float env = Mathf.Exp(-t * 6.5f) * Mathf.Min(1f, t * 80f);
                    float wave = Mathf.Sin(2f * Mathf.PI * freqHz * t) * 0.65f
                               + Mathf.Sin(2f * Mathf.PI * (freqHz * 2f) * t) * 0.25f
                               + Mathf.Sin(2f * Mathf.PI * (freqHz * 3f) * t) * 0.10f;
                    data[i] = wave * env;
                }
                clip = AudioClip.Create("Chime_" + key, samples, 1, sampleRate, false);
                clip.SetData(data, 0);
                chimeCache[key] = clip;
            }
            Audio().PlayOneShot(clip);
        }
        catch
        {
        }
    }

    public static void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        try { Audio().PlayOneShot(clip, volume); } catch { }
    }

    // ---- 旧入口（v3 的弹窗，已拆掉） ----
    public bool IsStudioOpen => false;
    public void Init(EggboyPlayer p, EggboyUI u, GameState s) { }
    public void ToggleStudio() { }
    public void OpenStudio() { }
    public void CloseStudio() { }
}
