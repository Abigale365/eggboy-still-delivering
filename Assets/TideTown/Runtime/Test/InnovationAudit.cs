// 创新方向审计（工程书 INNOVATION_BOOK_20261110 的 WP-90）：-innovaudit
// 用代码把每个创新方向走一遍，写 innov-audit.json（每个检查一个布尔值）和 shots/innov_*.png。
// Lux3D 一律用 mock 网关（不联网、不花积分），但会真的走一遍运行时 GLB 加载和原位替换。
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class InnovationAudit : MonoBehaviour
{
    GameBootstrap boot;
    readonly List<KeyValuePair<string, bool>> checks = new List<KeyValuePair<string, bool>>();
    readonly List<string> notes = new List<string>();
    float sumFps; int frames;

    public void Begin(GameBootstrap b)
    {
        boot = b;
        if (boot.ui != null) boot.ui.SetAuditMode(true);
        StartCoroutine(Run());
    }

    void Check(string name, bool ok, string note = "")
    {
        checks.Add(new KeyValuePair<string, bool>(name, ok));
        if (!string.IsNullOrEmpty(note)) notes.Add(name + ": " + note);
        Debug.Log("INNOVAUDIT " + name + "=" + ok + (note != "" ? " " + note : ""));
    }

    void Update() { if (Time.deltaTime > 0f) { sumFps += 1f / Time.deltaTime; frames++; } }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(1.0f);
        Time.timeScale = 1f;
        string fatal = "";
        var steps = new IEnumerator[] { AuditLux3DAndWish() };
        foreach (var s in steps)
        {
            // 一个方向出错不影响后面的方向
            while (true)
            {
                bool more;
                try { more = s.MoveNext(); }
                catch (System.Exception e) { fatal += e.GetType().Name + ": " + e.Message + "; "; Debug.LogException(e); break; }
                if (!more) break;
                yield return s.Current;
            }
        }

        bool all = fatal == "";
        var sb = new StringBuilder();
        sb.Append("{\n  \"checks\": {");
        for (int i = 0; i < checks.Count; i++)
        {
            all &= checks[i].Value;
            sb.Append(i == 0 ? "\n" : ",\n").Append("    \"").Append(checks[i].Key).Append("\": ").Append(checks[i].Value ? "true" : "false");
        }
        sb.Append("\n  },\n  \"notes\": [");
        for (int i = 0; i < notes.Count; i++)
            sb.Append(i == 0 ? "\n" : ",\n").Append("    \"").Append(notes[i].Replace("\\", "/").Replace("\"", "'")).Append("\"");
        sb.Append("\n  ],\n  \"avg_fps\": ").Append((frames > 0 ? sumFps / frames : 0f).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(",\n  \"errors\": \"").Append(fatal.Replace("\"", "'")).Append("\"");
        sb.Append(",\n  \"status\": \"").Append(all ? "passed" : "failed").Append("\"\n}\n");
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "innov-audit.json"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log("INNOVAUDIT status=" + (all ? "passed" : "failed"));
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    // ---- WP-02 / WP-W：Lux3D 网关、运行时加载、先保底再替换、心愿造物笺四种用途 ----
    IEnumerator AuditLux3DAndWish()
    {
        // 上一轮留下的生成缓存先清掉，否则同一句话会直接走缓存，测不到"生成中 → 替换"
        string cacheDir = Path.Combine(Application.persistentDataPath, "lux3d");
        if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        var lux = Lux3DService.Ensure();
        lux.SetMode("mock");
        Check("lux_mock_mode", lux.Mode == "mock");
        Check("lux_library_loaded", lux.Library.Count >= 8, "entries=" + lux.Library.Count);
        int missing = 0; string missingIds = "";
        foreach (var e in lux.Library)
        {
            var m = lux.InstantiateFallback(e);
            if (m == null) { missing++; missingIds += e.id + " "; }
            else Destroy(m);
        }
        Check("lux_library_all_models", missing == 0, missingIds);

        var wish = FindObjectOfType<AIAgentCalibrationDirector>();
        var mech = FindObjectOfType<WorldMechanismDirector>();
        Check("wish_system_present", wish != null && mech != null);
        if (wish == null || mech == null) yield break;
        var p = boot.player;

        // 1) 学校：给小黑造吃的。保底立刻出现 → 剧情推进 → mock 生成完原位替换，父节点和旗标不变
        Vector3 bowl = mech.GetDogBowlPosition();
        boot.state.step = "S1_slipper";
        Teleport(bowl + new Vector3(-1.2f, 0f, -0.6f));
        yield return null;
        wish.SpawnLiveWishObjectInWorld("香喷喷的烤肉骨头");
        var s1 = wish.LastWishSpawn;
        Check("wish_food_kind", wish.LastWishKind == "food", wish.LastWishKind);
        Check("wish_food_fallback_instant", s1 != null && s1.root != null && s1.root.transform.Find("Model") != null);
        Check("wish_food_generating", s1 != null && s1.state == "generating", s1 != null ? s1.state : "null");
        Check("wish_food_story", boot.state.flags.Contains("ball_in_bowl"));
        yield return Snap("innov_wish_food_fallback.png", s1 != null ? s1.root.transform.position : bowl, 2.2f);
        var rootBefore = s1 != null ? s1.root : null;
        float t0 = Time.realtimeSinceStartup;
        while (s1 != null && s1.state == "generating" && Time.realtimeSinceStartup - t0 < 40f) yield return null;
        yield return new WaitForSeconds(1.0f);
        Check("wish_food_swapped", s1 != null && s1.state == "swapped", s1 != null ? s1.state + " " + s1.seconds.ToString("F1") + "s" : "null");
        bool sameRoot = s1 != null && s1.root == rootBefore && s1.root != null;
        Transform model = sameRoot ? s1.root.transform.Find("Model") : null;
        Check("wish_food_same_root", sameRoot && model != null && s1.root.transform.Find("ModelOld") == null);
        if (model != null)
        {
            // 在父节点自己的坐标系里量（父节点带随机朝向，直接量世界包围盒会偏大）
            var keepRot = s1.root.transform.rotation;
            s1.root.transform.rotation = Quaternion.identity;
            var b = Lux3DModelFit.WorldBounds(model.gameObject);
            s1.root.transform.rotation = keepRot;
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float want = s1.entry != null ? s1.entry.size : 0.4f;
            Check("wish_food_size_fit", Mathf.Abs(maxDim - want) < want * 0.15f, $"max={maxDim:F2} want={want:F2}");
            Check("wish_food_on_ground", Mathf.Abs(b.min.y - s1.root.transform.position.y) < 0.05f, $"minY={b.min.y:F2} rootY={s1.root.transform.position.y:F2}");
            Check("wish_food_has_mesh", model.GetComponentsInChildren<Renderer>().Length > 0);
        }
        Check("wish_food_story_kept", boot.state.flags.Contains("ball_in_bowl"));
        yield return Snap("innov_wish_food_swapped.png", s1 != null && s1.root != null ? s1.root.transform.position : bowl, 2.2f);
        // 信读完之前别往下走（读信会锁人）
        t0 = Time.realtimeSinceStartup;
        while (!boot.state.flags.Contains("school_read") && Time.realtimeSinceStartup - t0 < 25f)
        {
            if (boot.ui.LetterOpen && Time.realtimeSinceStartup - t0 > 3f) boot.ui.CloseLetter();
            yield return null;
        }
        yield return new WaitForSecondsRealtime(4.5f); // 等水涨到 L2
        if (s1 != null && s1.root != null) yield return Snap("innov_wish_food_swapped.png", s1.root.transform.position, 1.6f);

        // 2) 渡口：涨潮后造船，海面上要真的能走
        Vector3 ch = mech.GetCrossingChannelCenter();
        float waterY = WaterLevel.StaticY;
        Check("wish_boat_water_L2", Mathf.Abs(waterY - 1.15f) < 0.08f, "waterY=" + waterY.ToString("F2"));
        bool before = boot.world.Walkable(ch.x, ch.z);
        Teleport(FindWalkableNear(ch, 3.5f, 6.5f));
        yield return null;
        wish.SpawnLiveWishObjectInWorld("戴草帽的海龟船");
        var s2 = wish.LastWishSpawn;
        Check("wish_boat_kind", wish.LastWishKind == "boat", wish.LastWishKind);
        yield return new WaitForSeconds(0.5f);
        bool after = boot.world.Walkable(ch.x, ch.z);
        float gy = boot.world.GroundAt(ch.x, ch.z);
        Check("crossing_gap_blocks_at_L2", !before, "channel=" + ch.ToString("F2"));
        Check("wish_boat_walkable", after, $"before={before} after={after} ground={gy:F2} water={WaterLevel.StaticY:F2}");
        Check("wish_boat_flag", boot.state.flags.Contains("dock_winch_raised"));
        yield return Snap("innov_wish_boat.png", ch, 6.5f);
        t0 = Time.realtimeSinceStartup;
        while (s2 != null && s2.state == "generating" && Time.realtimeSinceStartup - t0 < 40f) yield return null;
        yield return new WaitForSeconds(1.0f);
        Check("wish_boat_swapped", s2 != null && s2.state == "swapped", s2 != null ? s2.state : "null");
        Check("wish_boat_still_walkable", boot.world.Walkable(ch.x, ch.z));
        yield return Snap("innov_wish_boat_swapped.png", ch, 6.5f);

        // 3) 展台：纪念物；4) 同一句话第二次走缓存，不再计入本局次数
        int used = lux.GeneratedThisSession;
        Teleport(FindWalkableNear(bowl, 2f, 5f));
        yield return null;
        wish.SpawnLiveWishObjectInWorld("星空海螺八音盒");
        var s3 = wish.LastWishSpawn;
        Check("wish_keepsake_kind", wish.LastWishKind == "keepsake", wish.LastWishKind);
        t0 = Time.realtimeSinceStartup;
        while (s3 != null && s3.state == "generating" && Time.realtimeSinceStartup - t0 < 40f) yield return null;
        Check("wish_keepsake_swapped", s3 != null && s3.state == "swapped", s3 != null ? s3.state : "null");
        if (s3 != null && s3.root != null) yield return Snap("innov_wish_keepsake.png", s3.root.transform.position, 2.6f);
        int usedAfter = lux.GeneratedThisSession;
        wish.SpawnLiveWishObjectInWorld("星空海螺八音盒");
        var s4 = wish.LastWishSpawn;
        Check("wish_cache_hit", s4 != null && s4.state == "cached" && lux.GeneratedThisSession == usedAfter, s4 != null ? s4.state : "null");
        Check("wish_session_count", usedAfter == used + 1, $"{used}->{usedAfter}");

        // 5) offline：只有保底，不报错
        lux.SetMode("offline");
        wish.SpawnLiveWishObjectInWorld("招财橘猫雕塑" + Random.Range(1000, 9999));
        var s5 = wish.LastWishSpawn;
        Check("wish_offline_fallback_only", s5 != null && s5.state == "offline" && s5.root.transform.Find("Model") != null, s5 != null ? s5.state : "null");
        lux.SetMode("mock");
    }

    void Teleport(Vector3 pos)
    {
        var p = boot.player;
        pos.y = boot.world.GroundAt(pos.x, pos.z, pos.y + 2f);
        p.transform.position = pos;
        p.vel = Vector3.zero;
    }

    Vector3 FindWalkableNear(Vector3 c, float rMin, float rMax)
    {
        for (float r = rMin; r <= rMax; r += 0.5f)
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.PI / 8f;
                var q = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (boot.world.Walkable(q.x, q.z)) return q;
            }
        return c;
    }

    // 从斜上方看向 target 拍一张，拍完把镜头还给 ChaseCamera
    IEnumerator Snap(string file, Vector3 target, float dist)
    {
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        var chase = FindObjectOfType<ChaseCamera>();
        bool chaseOn = chase != null && chase.enabled;
        if (chase != null) chase.enabled = false;
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        Vector3 from = target + new Vector3(dist * 0.6f, dist * 0.55f, -dist * 0.7f);
        cam.transform.position = from;
        cam.transform.rotation = Quaternion.LookRotation(target + Vector3.up * 0.2f - from, Vector3.up);
        cam.fieldOfView = 42f;
        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
        yield return new WaitForSecondsRealtime(0.6f);
        cam.transform.SetPositionAndRotation(p0, r0);
        cam.fieldOfView = f0;
        if (chase != null) chase.enabled = chaseOn;
    }
}
