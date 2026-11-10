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
        var steps = new IEnumerator[] { AuditHints(), AuditWordSlips(), AuditLux3DAndWish() };
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

    // ---- WP-03：卡住才给提示（面包店烤炉旁站着不动，四层依次触发；有进展就清零） ----
    IEnumerator AuditHints()
    {
        var hint = HintDirector.Instance;
        Check("hint_system_present", hint != null);
        if (hint == null) yield break;
        var st = boot.state;
        if (!st.flags.Contains("letters_taken")) st.flags.Add("letters_taken");
        st.step = "B1_matches";
        Vector3 stove = Vector3.zero;
        foreach (var it in BakeryData.Load()) if (it.id == "stove") stove = new Vector3(it.x, it.y, it.z);
        Teleport(FindWalkableNear(stove, 1.5f, 4f));
        yield return null;

        hint.DebugAdvance(0.1f);
        hint.DebugAdvance(44f);
        Check("hint_quiet_before_45s", hint.CurrentNodeId == "bakery_fire" && hint.CurrentTier == 0, $"node={hint.CurrentNodeId} tier={hint.CurrentTier}");
        hint.DebugAdvance(2f);
        Check("hint_tier1_at_45s", hint.CurrentTier == 1);
        hint.DebugAdvance(45f);
        Check("hint_tier2_at_90s", hint.CurrentTier == 2 && GameObject.Find("HintBeacon") != null);
        yield return Snap("innov_hint_tier2.png", stove, 5.0f, 30f);
        hint.DebugAdvance(60f);
        Check("hint_tier3_letter", hint.CurrentTier == 3 && hint.LetterHintId == "lin_oven" && hint.LetterHintPhrase == "后院的木柴",
            hint.LetterHintId + "/" + hint.LetterHintPhrase);
        var words = FindObjectOfType<WordMaterializerSystem>();
        if (words != null)
        {
            words.OpenSpecificLetter(0);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("hint_tier3_phrase_marked", boot.ui.LetterBodyText != null && boot.ui.LetterBodyText.text.Contains("#2F7D6B"));
            boot.ui.CloseLetter();
            yield return new WaitForSecondsRealtime(0.2f);
        }
        hint.DebugAdvance(90f);
        Check("hint_tier4_at_240s", hint.CurrentTier == 4, "stuck=" + hint.StuckSeconds.ToString("F0"));
        yield return SnapScreen("innov_hint_tier4.png");

        // 有进展（拿到火柴）就清零
        st.flags.Add("matches_taken");
        hint.DebugAdvance(0.1f);
        Check("hint_reset_on_progress", hint.CurrentTier == 0 && hint.StuckSeconds < 1f && hint.LetterHintPhrase == "");
        st.flags.Remove("matches_taken");
        hint.DebugAdvance(0.1f);

        // 离开谜题范围就不计时
        Teleport(FindWalkableNear(stove + new Vector3(40f, 0f, 0f), 0f, 12f));
        yield return null;
        hint.DebugAdvance(300f);
        Check("hint_silent_when_away", hint.CurrentTier == 0 && hint.CurrentNodeId == "", $"node={hint.CurrentNodeId} tier={hint.CurrentTier}");
    }

    // ---- WP-D：信纸揭字 + 拼字（面包店：湿柴点不着 → 拼出干木柴 → 点着） ----
    IEnumerator AuditWordSlips()
    {
        string cacheDir = Path.Combine(Application.persistentDataPath, "lux3d");
        if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        var lux = Lux3DService.Ensure();
        lux.SetMode("mock");
        lux.OverrideSessionCap(50);

        var words = FindObjectOfType<WordMaterializerSystem>();
        var fx = FindObjectOfType<BakeryEffects>();
        Check("word_system_present", words != null && fx != null);
        if (words == null) yield break;
        var st = boot.state;

        st.flags.Remove("letters_taken"); // 上一段（提示审计）加过，这里先拿掉，测"没取信就没有字"
        Check("word_no_slips_before_letters", words.PocketSlips().Count == 0, "count=" + words.PocketSlips().Count);
        if (!st.flags.Contains("letters_taken")) st.flags.Add("letters_taken");
        st.step = "B1_matches";
        var pocket = words.PocketSlips();
        Check("word_pocket_has_three_letters", pocket.Count == 8, "count=" + pocket.Count);

        // 正文里的字要真的在信里（数据和信对得上）
        int inLetter = 0; string missing = "";
        foreach (var s in pocket)
        {
            var letter = boot.director.GetLetter(s.letter);
            if (letter != null && letter.body.Contains(s.word)) inLetter++; else missing += s.word + " ";
        }
        Check("word_all_in_letter_text", inLetter == pocket.Count, missing);

        Vector3 stove = Vector3.zero;
        foreach (var it in BakeryData.Load()) if (it.id == "stove") stove = new Vector3(it.x, it.y, it.z);
        Vector3 stand = FindWalkableNear(stove, 1.5f, 4f);
        Teleport(stand);
        yield return null;
        Vector3 land = Vector3.Lerp(stand, stove, 0.6f);
        land.y = boot.world.GroundAt(land.x, land.z, stove.y + 1f);

        // 「火」= 火种，等于拿到火柴；不点火
        yield return words.ThrowSlip("fire", land);
        yield return new WaitForSeconds(0.3f);
        Check("word_fire_gives_matches", st.flags.Contains("matches_taken") && !st.flags.Contains("oven_lit"), "step=" + st.step);

        // 「木柴」是湿的：进了炉膛，但点不着
        yield return words.ThrowSlip("wood", land);
        yield return new WaitForSeconds(0.3f);
        Check("word_wet_wood_loaded", st.flags.Contains("wood_loaded") && st.flags.Contains("wood_wet") && words.LastActionApplied, "step=" + st.step);
        Check("word_wet_wood_spawn", words.LastSpawn != null && words.LastSpawn.root != null && words.LastSpawn.state == "generating",
            words.LastSpawn != null ? words.LastSpawn.state : "null");
        var wetRoot = words.LastSpawn != null ? words.LastSpawn.root : null;
        yield return Snap("innov_word_wet_wood.png", land, 2.4f, 30f);
        boot.director.LightOven(fx);
        yield return new WaitForSeconds(0.5f);
        Check("word_wet_wood_wont_light", !st.flags.Contains("oven_lit") && fx.WetPuffCount == 1 && st.flags.Contains("wet_fizzled"), "puffs=" + fx.WetPuffCount);

        // v3 的跳关 bug：按 Tab 重读信再合上，步骤不能往前跳
        string stepBefore = st.step;
        words.OpenSpecificLetter(0);
        yield return new WaitForSecondsRealtime(0.3f);
        bool opened = boot.ui.LetterOpen;
        bool highlighted = boot.ui.LetterBodyText != null && boot.ui.LetterBodyText.text.Contains("<color=");
        yield return SnapScreen("innov_word_letter.png");
        boot.ui.CloseLetter();
        yield return new WaitForSecondsRealtime(0.3f);
        Check("word_reread_no_skip", opened && st.step == stepBefore && !boot.player.locked, $"before={stepBefore} after={st.step}");
        Check("word_letter_highlight", highlighted);

        // 单个「干」扔出去什么都不会发生
        yield return words.ThrowSlip("dry", land);
        Check("word_single_char_noop", st.flags.Contains("wood_wet") && words.LastSpawn == null);

        // 「干」+「木柴」=「干木柴」：换掉湿柴，点得着了
        var made = words.Combine("dry", "wood");
        Check("word_combo_dry_wood", made != null && made.id == "dry_wood" && st.flags.Contains("combo_dry_wood")); // 成就在审计模式下不记录，这里不查
        Check("word_combo_in_pocket", words.PocketSlips().Count == 9, "count=" + words.PocketSlips().Count);
        yield return words.ThrowSlip("dry_wood", land);
        yield return new WaitForSeconds(0.3f);
        Check("word_dry_wood_replaces_wet", st.flags.Contains("wood_loaded") && !st.flags.Contains("wood_wet") && words.LastActionApplied && wetRoot == null);
        boot.director.LightOven(fx);
        float t0 = Time.realtimeSinceStartup;
        while (!st.flags.Contains("oven_lit") && Time.realtimeSinceStartup - t0 < 5f) yield return null;
        Check("word_oven_lights_with_dry_wood", st.flags.Contains("oven_lit") && fx.Lit, "step=" + st.step);
        yield return Snap("innov_word_oven_lit.png", stove, 4.0f, 30f);

        // 表里没有的组合：变成一件纪念物，不动剧情
        string stepNow = st.step;
        var free = words.Combine("warm", "ball");
        Check("word_free_combo", free != null && free.action == "keepsake" && free.word == "暖球", free != null ? free.word : "null");
        if (free != null)
        {
            yield return words.ThrowSlip(free.id, land + new Vector3(0.8f, 0f, 0.4f));
            Check("word_free_combo_keepsake", words.LastSpawn != null && words.LastSpawn.root != null && st.step == stepNow);
        }
        // 等这一段的替换都做完，再交给下一段
        t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 6f) yield return null;
        Check("word_swaps_done", words.LastSpawn == null || words.LastSpawn.state == "swapped", words.LastSpawn != null ? words.LastSpawn.state : "null");
        yield return new WaitForSecondsRealtime(4.5f); // 点火后水涨到 L1
    }

    // 带界面的全屏截图
    IEnumerator SnapScreen(string file)
    {
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
        yield return new WaitForSecondsRealtime(0.6f);
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

        // 2a) 揭字拼出的「木船」先补一次缺口，再撤掉，换心愿船来补
        var words = FindObjectOfType<WordMaterializerSystem>();
        if (words != null)
        {
            var made = words.Combine("wood", "boat");
            Check("word_boat_combo", made != null && made.id == "wood_boat" && boot.state.flags.Contains("combo_wood_boat"));
            Check("word_boat_gap_before", !boot.world.Walkable(ch.x, ch.z));
            yield return words.ThrowSlip("wood_boat", ch);
            yield return new WaitForSeconds(0.6f);
            Check("word_boat_walkable", boot.world.Walkable(ch.x, ch.z) && words.LastActionApplied,
                $"applied={words.LastActionApplied} ground={boot.world.GroundAt(ch.x, ch.z):F2} water={WaterLevel.StaticY:F2}");
            Check("word_boat_solved", mech.CrossingSolved && boot.state.flags.Contains("dock_winch_raised"));
            yield return Snap("innov_word_boat.png", ch, 5.0f, 200f);
            if (words.LastSpawn != null && words.LastSpawn.root != null) Destroy(words.LastSpawn.root);
            mech.DebugResetCrossing();
            yield return new WaitForSeconds(0.3f);
            before = boot.world.Walkable(ch.x, ch.z);
        }

        wish.SpawnLiveWishObjectInWorld("戴草帽的海龟船");
        var s2 = wish.LastWishSpawn;
        Check("wish_boat_kind", wish.LastWishKind == "boat", wish.LastWishKind);
        yield return new WaitForSeconds(0.5f);
        bool after = boot.world.Walkable(ch.x, ch.z);
        float gy = boot.world.GroundAt(ch.x, ch.z);
        Check("crossing_gap_blocks_at_L2", !before, "channel=" + ch.ToString("F2"));
        Check("wish_boat_walkable", after, $"before={before} after={after} ground={gy:F2} water={WaterLevel.StaticY:F2}");
        Check("wish_boat_flag", boot.state.flags.Contains("dock_winch_raised"));
        yield return Snap("innov_wish_boat.png", ch, 5.0f, 200f);
        t0 = Time.realtimeSinceStartup;
        while (s2 != null && s2.state == "generating" && Time.realtimeSinceStartup - t0 < 40f) yield return null;
        yield return new WaitForSeconds(1.0f);
        Check("wish_boat_swapped", s2 != null && s2.state == "swapped", s2 != null ? s2.state : "null");
        Check("wish_boat_still_walkable", boot.world.Walkable(ch.x, ch.z));
        yield return Snap("innov_wish_boat_swapped.png", ch, 5.0f, 200f);

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
    IEnumerator Snap(string file, Vector3 target, float dist, float yawDeg = -40f)
    {
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        var chase = FindObjectOfType<ChaseCamera>();
        bool chaseOn = chase != null && chase.enabled;
        if (chase != null) chase.enabled = false;
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        // yawDeg：镜头在目标的哪个方向（0 = 北边 +Z，顺时针），斜上方约 40° 俯视
        float yr = yawDeg * Mathf.Deg2Rad;
        Vector3 from = target + new Vector3(Mathf.Sin(yr) * dist * 0.85f, dist * 0.6f, Mathf.Cos(yr) * dist * 0.85f);
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
