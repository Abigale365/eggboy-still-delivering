// Lux3D 服务：保底库 + 任务队列 + 缓存 + 运行时 GLB 加载 + "先保底、生成好再原位替换"。
// 玩法代码只调 Lux3DService.Spawn(...)：立刻拿到一个放着保底模型的父节点（碰撞、功能都挂在父节点上），
// 后台生成成功后只换父节点里的 "Model" 子节点，所以生成失败、超时、断网都不影响玩法。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable] public class Lux3DLibraryEntry
{
    public string id;          // 也是 Resources/Lux3DLibrary/<id> 或场景物体名
    public string source;      // "resource"：Resources/Lux3DLibrary；"scene"：CloneSceneLux3D 的名字
    public string sceneName;
    public string category;    // food / toy / boat / fire / keepsake / tool
    public string[] keywords;
    public float size = 0.5f;  // 最长边（米）
    public bool walkable;
    public string up = "auto"; // auto / thin（最薄的轴朝上，适合船、台） / long（最长的轴朝上）
}
[Serializable] class Lux3DLibraryFile { public Lux3DLibraryEntry[] entries; }
[Serializable] class Lux3DConfigFile
{
    public string mode = "offline";
    public string relayUrl = "";
    public int maxPerSession = 6;
    public float timeoutSec = 330f;
    public float mockDelaySec = 8f;
}

public sealed class Lux3DSpawn
{
    public GameObject root;        // 功能父节点（位置、碰撞、交互都在这）
    public Lux3DLibraryEntry entry;
    public string prompt;
    public string state = "fallback"; // fallback / generating / swapped / failed / capped / offline / cached
    public float seconds;
    public Action<Lux3DSpawn> onSwapped;
}

public sealed class Lux3DService : MonoBehaviour
{
    public static Lux3DService Instance { get; private set; }

    public ILux3DGateway Gateway { get; private set; }
    public string Mode { get; private set; } = "offline";
    public int GeneratedThisSession { get; private set; }
    public int MaxPerSession => cfg.maxPerSession;
    public IReadOnlyList<Lux3DLibraryEntry> Library => library;

    Lux3DConfigFile cfg = new Lux3DConfigFile();
    readonly List<Lux3DLibraryEntry> library = new List<Lux3DLibraryEntry>();
    readonly Queue<Lux3DSpawn> queue = new Queue<Lux3DSpawn>();
    int running;
    const int MaxParallel = 4; // Aholo 并发上限 5，留 1 个余量

    public static Lux3DService Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("Lux3DService");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<Lux3DService>();
        Instance.Init();
        return Instance;
    }

    void Init()
    {
        var cfgAsset = Resources.Load<TextAsset>("Lux3D/lux3d_config");
        if (cfgAsset != null) cfg = JsonUtility.FromJson<Lux3DConfigFile>(cfgAsset.text) ?? cfg;
        var libAsset = Resources.Load<TextAsset>("Lux3D/library");
        if (libAsset != null)
        {
            var f = JsonUtility.FromJson<Lux3DLibraryFile>(libAsset.text);
            if (f != null && f.entries != null) library.AddRange(f.entries);
        }
        // 命令行覆盖：-lux3d mock|dev-direct|relay|offline，-lux3ddelay 秒
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-lux3d") cfg.mode = args[i + 1];
            if (args[i] == "-lux3ddelay" && float.TryParse(args[i + 1], out var d)) cfg.mockDelaySec = d;
        }
        SetMode(IsInnovationAudit ? "mock" : cfg.mode);
    }

    // 审计用：一局里要生成很多次，放开每局上限
    public void OverrideSessionCap(int n) { cfg.maxPerSession = n; }

    public void SetMode(string mode)
    {
        Mode = mode;
        if (mode == "mock") Gateway = new MockGateway { delaySeconds = cfg.mockDelaySec };
        else if (mode == "relay") Gateway = new RelayGateway(cfg.relayUrl, cfg.timeoutSec);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else if (mode == "dev-direct") Gateway = new DevDirectGateway(cfg.timeoutSec);
#endif
        else { Gateway = new OfflineGateway(); Mode = "offline"; }
        if (!Gateway.CanGenerate && Mode != "offline") { Debug.LogWarning("LUX3D " + mode + " 不可用，退回 offline"); Gateway = new OfflineGateway(); Mode = "offline"; }
        Debug.Log("LUX3D mode=" + Mode + " library=" + library.Count);
    }

    // ---- 保底库 ----

    public Lux3DLibraryEntry Find(string id)
    {
        foreach (var e in library) if (e.id == id) return e;
        return null;
    }

    // 按文字挑最像的保底：关键词命中数最多的；一个都没中就用这个类别的第一个
    public Lux3DLibraryEntry Match(string text, string category)
    {
        Lux3DLibraryEntry best = null, firstOfCategory = null;
        int bestScore = 0;
        foreach (var e in library)
        {
            if (!string.IsNullOrEmpty(category) && e.category != category) continue;
            if (firstOfCategory == null) firstOfCategory = e;
            int score = 0;
            if (e.keywords != null && !string.IsNullOrEmpty(text))
                foreach (var k in e.keywords) if (!string.IsNullOrEmpty(k) && text.Contains(k)) score += k.Length;
            if (score > bestScore) { bestScore = score; best = e; }
        }
        return best ?? firstOfCategory;
    }

    public GameObject InstantiateFallback(Lux3DLibraryEntry e, float sizeOverride = 0f)
    {
        if (e == null) return null;
        float size = sizeOverride > 0f ? sizeOverride : e.size;
        if (e.source == "scene")
        {
            var holder = TideTownStyleUI.CloneSceneLux3D(e.sceneName, size, false);
            if (holder != null) holder.name = "Model";
            return holder;
        }
        var prefab = Resources.Load<GameObject>("Lux3DLibrary/" + e.id);
        if (prefab == null) return null;
        var model = Instantiate(prefab);
        model.name = "Model";
        Lux3DModelFit.Fit(model, size, e.up);
        return model;
    }

    // ---- 对外主入口 ----

    // 在 position 放一个保底模型并（在允许时）后台生成 prompt 对应的真模型，好了原位替换。
    // generate=false 时只放保底（比如同一个词这局已经生成过）。
    public Lux3DSpawn Spawn(string prompt, Lux3DLibraryEntry entry, Vector3 position, Quaternion rotation, Transform parent,
        bool generate = true, float sizeOverride = 0f)
    {
        var spawn = new Lux3DSpawn { entry = entry, prompt = prompt };
        var root = new GameObject("Lux3DSpawn_" + (entry != null ? entry.id : "none"));
        if (parent != null) root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(position, rotation);
        spawn.root = root;
        var marker = root.AddComponent<Lux3DSpawnMarker>();
        marker.spawn = spawn;
        marker.size = sizeOverride > 0f ? sizeOverride : (entry != null ? entry.size : 0.5f);

        var model = InstantiateFallback(entry, sizeOverride);
        if (model != null) { model.transform.SetParent(root.transform, false); model.transform.localPosition = Vector3.zero; }
        if (entry != null && entry.walkable) Lux3DModelFit.AddWalkableBox(root, model);

        if (!generate || string.IsNullOrWhiteSpace(prompt)) return spawn;

        // 1) 本地缓存：同一句话以前生成过，直接换，不扣积分
        string cachePath = CachePath(prompt);
        if (File.Exists(cachePath))
        {
            spawn.state = "cached";
            StartCoroutine(SwapFromBytes(spawn, File.ReadAllBytes(cachePath)));
            return spawn;
        }
        if (!Gateway.CanGenerate) { spawn.state = "offline"; return spawn; }
        if (GeneratedThisSession >= cfg.maxPerSession) { spawn.state = "capped"; return spawn; }

        GeneratedThisSession++;
        spawn.state = "generating";
        marker.ShowStamp(true);
        queue.Enqueue(spawn);
        return spawn;
    }

    void Update()
    {
        while (running < MaxParallel && queue.Count > 0)
        {
            var s = queue.Dequeue();
            if (s.root == null) continue; // 物体已经被收回
            running++;
            StartCoroutine(Run(s));
        }
    }

    // 开发用：下一次生成不下新单，接着等服务器上这个已有任务（不花积分）
    public static string DebugResumeTaskId;

    IEnumerator Run(Lux3DSpawn s)
    {
        var req = new Lux3DRequest
        {
            prompt = BuildPrompt(s.prompt),
            fallbackId = s.entry != null ? s.entry.id : null,
            resumeTaskId = DebugResumeTaskId,
        };
        DebugResumeTaskId = null;
        var res = new Lux3DResult();
        yield return Gateway.Generate(req, res, () => s.root == null);
        running--;
        s.seconds = res.seconds;
        if (s.root == null) yield break;
        if (!res.ok)
        {
            s.state = "failed";
            Debug.LogWarning("LUX3D failed: " + res.error + " prompt=" + s.prompt);
            var m = s.root.GetComponent<Lux3DSpawnMarker>();
            if (m != null) m.ShowStamp(false);
            yield break;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath(s.prompt)));
            File.WriteAllBytes(CachePath(s.prompt), res.glb);
        }
        catch (Exception e) { Debug.LogWarning("LUX3D cache write: " + e.Message); }
        yield return SwapFromBytes(s, res.glb);
    }

    public static string BuildPrompt(string text) =>
        (text ?? "").Trim() + "，单个物体，温暖童话海边小镇风格，无底座无背景";

    static readonly bool IsInnovationAudit = Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-innovaudit");
    static string CacheDirectory => IsInnovationAudit
        ? Path.Combine(Application.dataPath, "..", "lux3d-audit-cache")
        : Path.Combine(Application.persistentDataPath, "lux3d");

    internal static void ClearInnovationAuditCache()
    {
        if (!IsInnovationAudit || !Directory.Exists(CacheDirectory)) return;
        foreach (string file in Directory.EnumerateFiles(CacheDirectory, "*.glb")) File.Delete(file);
    }

    static string CachePath(string prompt)
    {
        unchecked
        {
            ulong h = 1469598103934665603UL;
            foreach (char c in prompt.Trim()) { h ^= c; h *= 1099511628211UL; }
            return Path.Combine(CacheDirectory, h.ToString("x16") + ".glb");
        }
    }

    IEnumerator SwapFromBytes(Lux3DSpawn s, byte[] glb)
    {
        GameObject loaded = null;
        string err = null;
        yield return RuntimeGlbLoader.Load(glb, go => loaded = go, e => err = e);
        if (s.root == null) { if (loaded != null) Destroy(loaded); yield break; }
        var marker = s.root.GetComponent<Lux3DSpawnMarker>();
        if (loaded == null)
        {
            s.state = "failed";
            Debug.LogWarning("LUX3D glb load failed: " + err);
            if (marker != null) marker.ShowStamp(false);
            yield break;
        }
        loaded.name = "Model";
        float size = marker != null ? marker.size : (s.entry != null ? s.entry.size : 0.5f);
        Lux3DModelFit.Fit(loaded, size, s.entry != null ? s.entry.up : "auto");
        if (marker != null) yield return marker.SwapTo(loaded);
        else { loaded.transform.SetParent(s.root.transform, false); }
        if (s.state != "cached") s.state = "swapped";
        Debug.Log($"LUX3D swapped prompt={s.prompt} seconds={s.seconds:F1} state={s.state}");
        s.onSwapped?.Invoke(s);
    }
}

// 挂在 Spawn 的父节点上：管"还在送"的小邮票和原位替换的过渡。
public sealed class Lux3DSpawnMarker : MonoBehaviour
{
    public Lux3DSpawn spawn;
    public float size = 0.5f;
    GameObject stamp;

    public void ShowStamp(bool on)
    {
        if (!on) { if (stamp != null) Destroy(stamp); stamp = null; return; }
        if (stamp != null) return;
        stamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        stamp.name = "StillDeliveringStamp";
        Destroy(stamp.GetComponent<Collider>());
        stamp.transform.SetParent(transform, false);
        stamp.transform.localPosition = new Vector3(0f, size + 0.35f, 0f);
        stamp.transform.localScale = new Vector3(0.16f, 0.2f, 1f);
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var mat = new Material(sh);
        var col = new Color(0.93f, 0.48f, 0.2f, 1f); // 邮戳橘
        mat.color = col;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        stamp.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void Update()
    {
        if (stamp == null) return;
        stamp.transform.localRotation = Quaternion.Euler(0f, Time.time * 140f, 0f);
        stamp.transform.localPosition = new Vector3(0f, size + 0.35f + Mathf.Sin(Time.time * 2.4f) * 0.04f, 0f);
    }

    // 旧模型缩下去、新模型弹出来（0.6 秒），父节点上的碰撞和功能不动
    public IEnumerator SwapTo(GameObject next)
    {
        ShowStamp(false);
        var old = transform.Find("Model");
        next.transform.SetParent(transform, false);
        next.transform.localPosition = Vector3.zero;
        Vector3 target = next.transform.localScale;
        next.transform.localScale = Vector3.zero;
        Vector3 oldScale = old != null ? old.localScale : Vector3.one;
        float t = 0f;
        const float half = 0.3f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            if (old != null) { old.localScale = oldScale * (1f - k); old.Rotate(0f, 540f * Time.deltaTime, 0f, Space.World); }
            yield return null;
        }
        if (old != null) { old.name = "ModelOld"; Destroy(old.gameObject); }
        ShadowAndChimeSystem.PlayProceduralChime(1046.5f, 0.22f);
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            float overshoot = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
            next.transform.localScale = target * (k * overshoot);
            yield return null;
        }
        next.transform.localScale = target;
    }
}
