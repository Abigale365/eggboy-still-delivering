using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

internal static class TownMusic
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartMusic()
    {
        if (UnityEngine.Object.FindObjectOfType<TownMusicPlayer>() != null) return;
        var player = new GameObject("TideTown background music");
        UnityEngine.Object.DontDestroyOnLoad(player);
        player.AddComponent<TownMusicPlayer>();
    }
}

// Kept in the music module so story/interaction files need no edits.
internal sealed class TownMusicPlayer : MonoBehaviour
{
    const float MusicGain = 0.55f;
    float duckGain = MusicGain;
    AudioListener listener;
    public float CurrentMusicGain => duckGain;
    public bool IsPreloading { get; private set; }
    public bool HasClip(string name) => clips.TryGetValue(name, out var c) && c != null;
    public void CheckTheme() => SwitchTo("theme");
    const float FadeSeconds = 2.5f;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly HashSet<string> knownFlags = new HashSet<string>();
    AudioSource[] music;
    AudioSource effects, ambience;
    GameBootstrap bootstrap;
    GameState observedState;
    string current;
    int active;
    float blend = 1f;
    float nextLookup;

    static readonly string[] AllTrackNames = {
        "theme", "bakery", "school", "lighthouse", "ending", "ambient_explore",
        "sfx_letter", "sfx_mechanism", "sfx_achievement", "sfx_tide"
    };

    void Awake()
    {
        EnsureListener();
        gameObject.AddComponent<TownVoiceover>();
        music = new[] { MakeSource(true), MakeSource(true) };
        effects = MakeSource(false);
        ambience = MakeSource(true);
        bool anyMissing = false;
        foreach (string name in AllTrackNames)
        {
            clips[name] = Resources.Load<AudioClip>("Music/" + name);
            if (clips[name] == null) anyMissing = true;
        }
        if (clips["theme"] == null) clips["theme"] = Resources.Load<AudioClip>("TideTownMusic");
        ambience.clip = clips["sfx_tide"];
        ambience.volume = 0.09f;
        effects.volume = 1f;
        if (ambience.clip != null) ambience.Play();
        SwitchTo("theme");
        if (anyMissing) StartCoroutine(LoadExternalMusicFallback());
    }

    IEnumerator LoadExternalMusicFallback()
    {
        IsPreloading = true;
        string dir = Path.Combine(Application.streamingAssetsPath, "Music");
        if (!Directory.Exists(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "MusicOverrides"));
        if (!Directory.Exists(dir)) { IsPreloading = false; yield break; }
        foreach (string name in AllTrackNames)
        {
            if (clips.TryGetValue(name, out var existing) && existing != null) continue;
            string p = Path.Combine(dir, name + ".ogg");
            if (!File.Exists(p)) continue;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.GetFullPath(p)).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var loaded = DownloadHandlerAudioClip.GetContent(req);
                    if (loaded != null && loaded.length > 0.01f)
                    {
                        loaded.name = name;
                        clips[name] = loaded;
                        if (name == "sfx_tide" && ambience.clip == null) { ambience.clip = loaded; ambience.Play(); }
                        if (name == (current ?? "theme") && !music[active].isPlaying) SwitchTo(name);
                    }
                }
            }
        }
        IsPreloading = false;
    }

    void EnsureListener()
    {
        AudioListener.pause = false;
        var camera = Camera.main;
        var target = camera != null ? camera.gameObject : gameObject;

        // 先禁用旧监听器，再在主相机启用，避免同帧出现多个激活监听器。
        foreach (var other in FindObjectsOfType<AudioListener>())
        {
            if (other.gameObject == target) continue;
            other.enabled = false;
            Destroy(other);
        }
        listener = target.GetComponent<AudioListener>();
        if (listener == null) listener = target.AddComponent<AudioListener>();
        listener.enabled = true;
    }

    AudioSource MakeSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        return s;
    }

    void Update()
    {
        EnsureListener();
        if (bootstrap == null && Time.unscaledTime >= nextLookup)
        {
            bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            nextLookup = Time.unscaledTime + 1f;
        }
        var state = bootstrap != null ? bootstrap.state : null;
        if (state != null)
        {
            var step = state.step ?? "";
            string wanted = step.StartsWith("B") ? "bakery" : step.StartsWith("S") ? "school" :
                step.StartsWith("L") ? "lighthouse" : step.StartsWith("E") ? "ending" : "theme";
            // 通关后解除剧情、开放寄件处即进入自由探索，继续沿用等功率交叉淡化。
            if (state.flags.Contains("ending_done") && !bootstrap.ui.LetterOpen &&
                (current != "ending" || !music[active].isPlaying ||
                 music[active].time >= music[active].clip.length - FadeSeconds))
                wanted = "ambient_explore";
            if (wanted != current) SwitchTo(wanted);
            if (!ReferenceEquals(observedState, state))
            {
                observedState = state;
                knownFlags.Clear();
                if (state.flags != null) foreach (var f in state.flags) knownFlags.Add(f);
            }
            else if (state.flags != null)
            {
                foreach (var flag in state.flags)
                {
                    if (!knownFlags.Add(flag)) continue;
                    // Existing achievement system already supplies its own two-note chime.
                    if (flag.StartsWith("ach_")) continue;
                    else if (flag.StartsWith("puzzle_") || flag == "beam_lit") PlayEffect("sfx_mechanism", 0.60f);
                    else if (flag.Contains("letter") && flag.Contains("read")) PlayEffect("sfx_letter", 0.55f);
                }
            }
        }
        float target = TownVoiceover.Speaking ? 0.18f : MusicGain;
        duckGain = Mathf.MoveTowards(duckGain, target, (MusicGain - 0.18f) * Time.unscaledDeltaTime / (TownVoiceover.Speaking ? 0.25f : 0.6f));
        blend = Mathf.Min(1f, blend + Time.unscaledDeltaTime / FadeSeconds);
        music[active].volume = duckGain * Mathf.Sin(blend * Mathf.PI * 0.5f);
        music[1 - active].volume = duckGain * Mathf.Cos(blend * Mathf.PI * 0.5f);
        if (blend >= 1f && music[1 - active].isPlaying) music[1 - active].Stop();
    }

    void SwitchTo(string name)
    {
        if (!clips.TryGetValue(name, out var clip) || clip == null) return;
        current = name;
        active = 1 - active;
        music[active].Stop();
        music[active].clip = clip;
        music[active].loop = name != "ending";
        music[active].volume = 0f;
        music[active].Play();
        blend = 0f;
        Debug.Log("TOWNMUSIC track=" + name + " seconds=" + clip.length + " playing=" + music[active].isPlaying);
    }

    void PlayEffect(string name, float gain)
    {
        if (clips.TryGetValue(name, out var clip) && clip != null) effects.PlayOneShot(clip, gain);
    }
}
