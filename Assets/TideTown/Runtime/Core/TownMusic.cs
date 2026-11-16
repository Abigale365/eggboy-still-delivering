using UnityEngine;
using System.Collections.Generic;

internal static class TownMusic
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartMusic()
    {
        if (Object.FindObjectOfType<TownMusicPlayer>() != null) return;
        var player = new GameObject("TideTown background music");
        Object.DontDestroyOnLoad(player);
        player.AddComponent<TownMusicPlayer>();
    }
}

// Kept in the music module so story/interaction files need no edits.
internal sealed class TownMusicPlayer : MonoBehaviour
{
    const float MusicGain = 0.36f;
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

    void Awake()
    {
        music = new[] { MakeSource(true), MakeSource(true) };
        effects = MakeSource(false);
        ambience = MakeSource(true);
        foreach (string name in new[] { "theme", "bakery", "school", "lighthouse", "ending", "ambient_explore",
            "sfx_letter", "sfx_mechanism", "sfx_achievement", "sfx_tide" })
        {
            clips[name] = Resources.Load<AudioClip>("Music/" + name);
            if (clips[name] == null) Debug.LogWarning("TOWNMUSIC missing Music/" + name);
        }
        if (clips["theme"] == null) clips["theme"] = Resources.Load<AudioClip>("TideTownMusic");
        ambience.clip = clips["sfx_tide"];
        ambience.volume = 0.055f;
        if (ambience.clip != null) ambience.Play();
        SwitchTo("theme");
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
        if (bootstrap == null && Time.unscaledTime >= nextLookup)
        {
            bootstrap = Object.FindObjectOfType<GameBootstrap>();
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
                    else if (flag.StartsWith("puzzle_") || flag == "beam_lit") PlayEffect("sfx_mechanism", 0.42f);
                    else if (flag.Contains("letter") && flag.Contains("read")) PlayEffect("sfx_letter", 0.32f);
                }
            }
        }
        blend = Mathf.Min(1f, blend + Time.unscaledDeltaTime / FadeSeconds);
        music[active].volume = MusicGain * Mathf.Sin(blend * Mathf.PI * 0.5f);
        music[1 - active].volume = MusicGain * Mathf.Cos(blend * Mathf.PI * 0.5f);
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
