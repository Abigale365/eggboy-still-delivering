using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

internal sealed class TownVoiceover : MonoBehaviour
{
    static TownVoiceover instance;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, AudioClip> overrideClips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, string> overrideSignatures = new Dictionary<string, string>();
    AudioSource voiceSource;
    string currentVoiceKey;
    string pendingKey;
    Coroutine fade;
    Coroutine activeLoad;
    bool loadingExternal;
    bool preloadingOverrides;

    static readonly Dictionary<string, string[]> KeyAliases = new Dictionary<string, string[]>
    {
        { "vo_letter_lin_oven", new[] { "vo_letter_lin_oven", "letter_1", "lin_oven" } },
        { "vo_letter_ying_dog", new[] { "vo_letter_ying_dog", "letter_2", "ying_dog" } },
        { "vo_letter_chen_lighthouse", new[] { "vo_letter_chen_lighthouse", "letter_3", "chen_lighthouse" } },
        { "vo_letter_zhou_eggboy", new[] { "vo_letter_zhou_eggboy", "letter_4", "zhou_eggboy" } },
        { "vo_intro_wake", new[] { "vo_intro_wake", "intro_wake", "voice_intro" } },
        { "vo_mailbox_take", new[] { "vo_mailbox_take", "mailbox_take", "voice_mailbox" } },
        { "vo_stamp_bakery", new[] { "vo_stamp_bakery", "stamp_bakery" } },
        { "vo_stamp_school", new[] { "vo_stamp_school", "stamp_school" } },
        { "vo_lighthouse_lit", new[] { "vo_lighthouse_lit", "lighthouse_lit" } },
        { "vo_lighthouse_reply", new[] { "vo_lighthouse_reply", "lighthouse_reply" } },
        { "vo_ending_sub1", new[] { "vo_ending_sub1", "ending_1", "ending_sub1" } },
        { "vo_ending_sub2", new[] { "vo_ending_sub2", "ending_2", "ending_sub2" } },
    };

    static readonly (string ext, AudioType type)[] SupportedAudioTypes =
    {
        (".wav", AudioType.WAV),
        (".ogg", AudioType.OGGVORBIS),
        (".mp3", AudioType.MPEG),
    };

    public static bool Speaking =>
        instance != null && (instance.loadingExternal || (instance.voiceSource != null && instance.voiceSource.isPlaying));

    public static bool AuditMode => FilmingStageJumper.AuditArguments();

    public static string LetterKey(string title)
    {
        switch (title)
        {
            case "致 面包店的烤炉": return "vo_letter_lin_oven";
            case "致 小黑": return "vo_letter_ying_dog";
            case "致 灯塔": return "vo_letter_chen_lighthouse";
            case "致 EGGBOY": return "vo_letter_zhou_eggboy";
            default: return null;
        }
    }

    static string[] GetOverrideDirectories()
    {
        string exeAdjacent = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "VoiceOverrides"));
        string streaming = Path.Combine(Application.streamingAssetsPath, "VoiceOverrides");
        return new[] { exeAdjacent, streaming };
    }

    static bool TryResolveExternalFile(string key, out string filePath, out AudioType audioType)
    {
        filePath = null;
        audioType = AudioType.UNKNOWN;
        if (string.IsNullOrEmpty(key)) return false;

        string[] candidates = KeyAliases.TryGetValue(key, out var aliases) ? aliases : new[] { key };
        foreach (string dir in GetOverrideDirectories())
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
            foreach (string name in candidates)
            {
                foreach (var item in SupportedAudioTypes)
                {
                    string candidatePath = Path.Combine(dir, name + item.ext);
                    if (File.Exists(candidatePath))
                    {
                        filePath = Path.GetFullPath(candidatePath);
                        audioType = item.type;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    static string BuildFileSignature(string fullPath)
    {
        try
        {
            var info = new FileInfo(fullPath);
            return fullPath + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
        }
        catch
        {
            return fullPath;
        }
    }

    void Awake()
    {
        instance = this;
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.volume = 0.95f;
        foreach (var clip in Resources.LoadAll<AudioClip>("Voice")) clips[clip.name] = clip;
        StartCoroutine(PreloadExternalOverrides());
        if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "-audiocheck")) StartCoroutine(AudioCheck());
    }

    void OnDestroy()
    {
        foreach (var kv in overrideClips)
            if (kv.Value != null) Destroy(kv.Value);
        overrideClips.Clear();
        overrideSignatures.Clear();
        if (instance == this) instance = null;
    }

    static readonly string[] AllVoiceKeys = {
        "vo_bottle_0", "vo_bottle_1", "vo_bottle_2",
        "vo_ending_sub1", "vo_ending_sub2", "vo_intro_wake",
        "vo_letter_chen_lighthouse", "vo_letter_lin_oven", "vo_letter_ying_dog", "vo_letter_zhou_eggboy",
        "vo_lighthouse_lit", "vo_lighthouse_reply", "vo_mailbox_take",
        "vo_mem_basket", "vo_mem_bell", "vo_mem_blackboard", "vo_mem_desk", "vo_mem_doghouse",
        "vo_mem_flour", "vo_mem_hat", "vo_mem_log", "vo_mem_recipe", "vo_mem_scale", "vo_mem_scope", "vo_mem_slipper",
        "vo_stamp_bakery", "vo_stamp_school"
    };

    IEnumerator PreloadExternalOverrides()
    {
        preloadingOverrides = true;
        var knownKeys = new HashSet<string>(clips.Keys);
        foreach (var k in KeyAliases.Keys) knownKeys.Add(k);
        foreach (var k in AllVoiceKeys) knownKeys.Add(k);
        foreach (string key in knownKeys)
        {
            if (!TryResolveExternalFile(key, out string path, out AudioType audioType)) continue;
            yield return LoadExternalClipCoroutine(key, path, audioType, BuildFileSignature(path));
        }
        preloadingOverrides = false;
    }

    IEnumerator LoadExternalClipCoroutine(string key, string fullPath, AudioType audioType, string signature)
    {
        string uri = new Uri(fullPath).AbsoluteUri;
        using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var loaded = DownloadHandlerAudioClip.GetContent(req);
                if (loaded != null && loaded.length > 0.01f)
                {
                    loaded.name = key + "_override";
                    if (overrideClips.TryGetValue(key, out var prev) && prev != null && prev != loaded)
                        Destroy(prev);
                    overrideClips[key] = loaded;
                    overrideSignatures[key] = signature;
                    Debug.Log("VOICE override loaded=" + key + " path=" + fullPath + " seconds=" + loaded.length.ToString("F2"));
                    yield break;
                }
            }
            Debug.LogWarning("VOICE override failed=" + key + " path=" + fullPath + " err=" + req.error);
        }
    }

    AudioClip GetBuiltInClip(string key)
    {
        if (clips.TryGetValue(key, out var clip) && clip != null) return clip;
        var loaded = Resources.Load<AudioClip>("Voice/" + key);
        if (loaded != null) clips[key] = loaded;
        return loaded;
    }

    public static float Length(string key)
    {
        if (instance == null || string.IsNullOrEmpty(key)) return 0f;
        if (TryResolveExternalFile(key, out _, out _) &&
            instance.overrideClips.TryGetValue(key, out var ext) && ext != null)
            return ext.length;
        var builtIn = instance.GetBuiltInClip(key);
        return builtIn != null ? builtIn.length : 0f;
    }

    public static void Play(string key, bool replay = false)
    {
        if (instance == null || string.IsNullOrEmpty(key)) return;
        instance.PlayClip(key, replay);
    }

    void PlayClip(string key, bool replay)
    {
        // 学校逐行刷新同一封信时，即使已经读完，也只在点击重听后重新开始。
        if (!replay && key == currentVoiceKey && (Speaking || key.StartsWith("vo_letter_"))) return;
        if (fade != null) { pendingKey = key; return; }

        if (TryResolveExternalFile(key, out string extPath, out AudioType extType))
        {
            string sig = BuildFileSignature(extPath);
            if (overrideClips.TryGetValue(key, out var cached) && cached != null &&
                overrideSignatures.TryGetValue(key, out var oldSig) && oldSig == sig)
            {
                StartClipPlayback(key, cached, "override");
                return;
            }
            if (activeLoad != null) StopCoroutine(activeLoad);
            activeLoad = StartCoroutine(HotLoadAndPlay(key, extPath, extType, sig));
            return;
        }

        if (overrideClips.TryGetValue(key, out var stale))
        {
            if (stale != null) Destroy(stale);
            overrideClips.Remove(key);
            overrideSignatures.Remove(key);
        }

        var builtIn = GetBuiltInClip(key);
        if (builtIn == null) { Debug.LogWarning("VOICE missing " + key); return; }
        StartClipPlayback(key, builtIn, "builtin");
    }

    IEnumerator HotLoadAndPlay(string key, string extPath, AudioType extType, string sig)
    {
        loadingExternal = true;
        currentVoiceKey = key;
        yield return LoadExternalClipCoroutine(key, extPath, extType, sig);
        loadingExternal = false;
        activeLoad = null;
        if (currentVoiceKey != key) yield break;

        if (overrideClips.TryGetValue(key, out var extClip) && extClip != null)
        {
            StartClipPlayback(key, extClip, "override-hot");
            yield break;
        }
        var fallback = GetBuiltInClip(key);
        if (fallback != null) StartClipPlayback(key, fallback, "builtin-fallback");
        else Debug.LogWarning("VOICE missing " + key);
    }

    void StartClipPlayback(string key, AudioClip clip, string sourceTag)
    {
        currentVoiceKey = key;
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.volume = 0.95f;
        voiceSource.Play();
        Debug.Log("VOICE play=" + key + " source=" + sourceTag + " seconds=" + clip.length.ToString("F2"));
    }

    public static void StopLetter()
    {
        if (instance != null && instance.currentVoiceKey != null && instance.currentVoiceKey.StartsWith("vo_letter_")) StopAll();
    }

    public static void StopAll()
    {
        if (instance == null) return;
        if (instance.activeLoad != null) { instance.StopCoroutine(instance.activeLoad); instance.activeLoad = null; }
        instance.loadingExternal = false;
        if (instance.fade != null) instance.StopCoroutine(instance.fade);
        instance.pendingKey = null;
        instance.currentVoiceKey = null;
        instance.fade = instance.StartCoroutine(instance.FadeOut());
    }

    IEnumerator FadeOut()
    {
        float volume = voiceSource.volume;
        for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            voiceSource.volume = volume * (1f - t / 0.25f);
            yield return null;
        }
        voiceSource.Stop();
        voiceSource.volume = 0.95f;
        fade = null;
        string next = pendingKey;
        pendingKey = null;
        if (next != null) PlayClip(next, true);
    }

    [Serializable] sealed class CheckResult
    {
        public string status;
        public int listeners, missing, voiceClips, overrideClipsLoaded;
        public bool paused, ducking, overrideTestPassed;
        public float volume, musicPeak, musicRms, voicePeak, voiceRms, duckGain;
        public List<string> missingPaths = new List<string>();
    }

    void Sample(ref float peak, ref float rms)
    {
        var samples = new float[2048];
        AudioListener.GetOutputData(samples, 0);
        double energy = 0;
        foreach (float sample in samples) { peak = Mathf.Max(peak, Mathf.Abs(sample)); energy += sample * sample; }
        rms = Mathf.Max(rms, (float)Math.Sqrt(energy / samples.Length));
    }

    IEnumerator AudioCheck()
    {
        Application.runInBackground = true;
        var result = new CheckResult();
        var music = GetComponent<TownMusicPlayer>();
        float waitStart = Time.realtimeSinceStartup;
        while ((preloadingOverrides || (music != null && music.IsPreloading)) && Time.realtimeSinceStartup - waitStart < 15f)
            yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
        AudioListener.volume = 1f;
        StopAll();
        foreach (string key in new[] { "theme", "bakery", "school", "lighthouse", "ending", "ambient_explore", "sfx_letter", "sfx_mechanism", "sfx_achievement", "sfx_tide" })
            if (Resources.Load<AudioClip>("Music/" + key) == null && (music == null || !music.HasClip(key))) result.missingPaths.Add("Music/" + key);
        foreach (string key in AllVoiceKeys)
            if (Resources.Load<AudioClip>("Voice/" + key) == null && !overrideClips.ContainsKey(key)) result.missingPaths.Add("Voice/" + key);
        result.voiceClips = Mathf.Max(clips.Count, overrideClips.Count);
        result.overrideClipsLoaded = overrideClips.Count;
        music.CheckTheme();
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 3f)
        {
            if (Time.realtimeSinceStartup - start > 1f) Sample(ref result.musicPeak, ref result.musicRms);
            yield return null;
        }
        Play("vo_letter_lin_oven", true);
        start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 4f)
        {
            if (Time.realtimeSinceStartup - start > 0.5f) Sample(ref result.voicePeak, ref result.voiceRms);
            yield return null;
        }
        result.overrideTestPassed = !TryResolveExternalFile("vo_letter_lin_oven", out _, out _) || overrideClips.ContainsKey("vo_letter_lin_oven");
        result.listeners = FindObjectsOfType<AudioListener>().Length;
        result.paused = AudioListener.pause;
        result.volume = AudioListener.volume;
        result.duckGain = music.CurrentMusicGain;
        result.ducking = Speaking && result.duckGain <= 0.181f;
        result.missing = result.missingPaths.Count;
        result.status = result.listeners == 1 && !result.paused && result.volume >= 0.8f && result.missing == 0 &&
            result.musicPeak > 0.005f && result.musicRms > 0.005f && result.voiceRms > 0.01f && result.ducking && result.overrideTestPassed ? "passed" : "failed";
        string path = Path.Combine(Application.dataPath, "..", "audio-check.json");
        File.WriteAllText(path, JsonUtility.ToJson(result, true));
        Debug.Log("AUDIOCHECK " + result.status + " musicRms=" + result.musicRms + " voiceRms=" + result.voiceRms + " overrides=" + result.overrideClipsLoaded);
        Application.Quit(result.status == "passed" ? 0 : 1);
    }
}
