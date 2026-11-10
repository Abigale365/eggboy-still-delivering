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
        TidalWindChallenge.Suppress = true; // 只在测海风那一段放开
        var steps = new IEnumerator[] { AuditWooden(), AuditWind(), AuditDog(), AuditPrint(), AuditShadowSound(), AuditHints(), AuditWordSlips(), AuditEcho(), AuditLux3DAndWish() };
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

    // ---- WP-B：海风（只在海边迎风路上刮；蹲住扛过去有顺风；没扛住东西掉在脚边能捡回来） ----
    IEnumerator AuditWind()
    {
        var wind = FindObjectOfType<TidalWindChallenge>();
        Check("wind_present", wind != null);
        if (wind == null) yield break;
        var st = boot.state;
        TidalWindChallenge.Suppress = false;

        // 面包店不刮
        Vector3 stove = Vector3.zero;
        foreach (var it in BakeryData.Load()) if (it.id == "stove") stove = new Vector3(it.x, it.y, it.z);
        Teleport(FindWalkableNear(stove, 1.5f, 4f));
        yield return new WaitForSeconds(0.3f);
        Check("wind_calm_inland", !wind.InWindZone && wind.Phase == "calm");

        // 低洼路（LOW → 24）中间
        Vector3 low = Vector3.zero, p24 = Vector3.zero;
        var asset = Resources.Load<TextAsset>("World/route");
        var list = asset != null ? JsonUtility.FromJson<RouteList>(asset.text) : null;
        if (list != null) foreach (var r in list.route) { if (r.stop && r.id == "LOW") low = new Vector3(r.x, 0f, r.z); if (r.stop && r.id == "24") p24 = new Vector3(r.x, 0f, r.z); }
        Vector3 mid = FindWalkableNear((low + p24) * 0.5f, 0f, 2f);
        st.step = "B3_load"; st.carrying = "wood";
        Teleport(mid);
        yield return new WaitForSeconds(0.3f);
        Check("wind_zone_on_coast", wind.InWindZone, "mid=" + mid.ToString("F1"));
        // 风是从海那边吹过来的：顺着风往前几米，地面不应该比现在更低（往陆地上吹）
        Vector3 downwind = mid + wind.WindDir * 5f;
        Check("wind_blows_from_sea", !boot.world.InWalkArea(mid.x - wind.WindDir.x * 5f, mid.z - wind.WindDir.z * 5f)
            || boot.world.GroundAt(mid.x - wind.WindDir.x * 5f, mid.z - wind.WindDir.z * 5f) <= boot.world.GroundAt(downwind.x, downwind.z) + 0.05f,
            "wind=" + wind.WindDir.ToString("F2"));

        // 蹲住：扛过去，有顺风，东西还在手上
        wind.DebugBrace = true;
        wind.DebugStartGust();
        yield return new WaitForSeconds(2.2f);
        Check("wind_gust_brace_crouches", wind.Phase == "gust" && boot.player.IsStoryCrouched, "phase=" + wind.Phase);
        yield return Snap("innov_wind_gust.png", boot.player.transform.position, 4.0f, 200f);
        float t0 = Time.realtimeSinceStartup;
        while (wind.Phase == "gust" && Time.realtimeSinceStartup - t0 < 5f) yield return null;
        Check("wind_brace_survives", wind.Survived == 1 && wind.Failures == 0 && st.carrying == "wood" && wind.Phase == "boost", "phase=" + wind.Phase);
        wind.DebugBrace = false;
        yield return new WaitForSeconds(3.3f);

        // 不蹲：被吹得踉跄，柴掉在脚边，剧情退回"去抱柴"
        Teleport(mid);
        Vector3 before = boot.player.transform.position;
        wind.DebugStartGust();
        t0 = Time.realtimeSinceStartup;
        while (wind.Failures == 0 && Time.realtimeSinceStartup - t0 < 6f) yield return null;
        float moved = Vector3.Distance(boot.player.transform.position, before);
        Check("wind_fail_happens", wind.Failures == 1);
        Check("wind_fail_short_stumble", moved < 7f, "moved=" + moved.ToString("F1"));
        Check("wind_fail_drops_item_nearby", st.carrying != "wood" && boot.director.LastDroppedItem == "wood"
            && Vector3.Distance(boot.director.LastDropPosition, boot.player.transform.position) < 5f
            && boot.world.Walkable(boot.director.LastDropPosition.x, boot.director.LastDropPosition.z),
            $"carry={st.carrying} step={st.step} drop={boot.director.LastDropPosition:F1}");
        Check("wind_fail_no_text_banner", GameObject.Find("TidalWindBanner") == null);
        yield return new WaitForSeconds(1.6f);
        yield return Snap("innov_wind_dropped.png", boot.director.LastDropPosition, 3.5f, 290f);
        TidalWindChallenge.Suppress = true;
        Teleport(boot.director.LastDropPosition);
        yield return null;
        bool picked = boot.service.TryInteract("dropped_wood");
        Check("wind_pickup_restores", picked && st.carrying == "wood" && st.step == "B3_load", "step=" + st.step);

        st.carrying = ""; st.step = "P1_mailbox";
        boot.player.SetStoryCrouch(false);
        yield return new WaitForSeconds(0.3f);
    }

    // ---- WP-G：影子钥匙（布幕上真的拼出钥匙）+ 装声音的瓶子 + 广场八音盒（房子起伏后精确复位） ----
    IEnumerator AuditShadowSound()
    {
        var sp = ShadowLanternPuzzle.Instance;
        var snd = SoundBottleSystem.Instance;
        Check("shadow_present", sp != null && sp.enabled && snd != null);
        if (sp == null || !sp.enabled || snd == null) yield break;
        var st = boot.state;
        Check("shadow_off_route", true, "turntable=" + sp.TurntablePos.ToString("F1"));
        Teleport(FindWalkableNear(sp.TurntablePos, 1.2f, 1.8f));
        yield return new WaitForSeconds(0.3f);

        // 没转对：不是钥匙（钥匙头的孔这时候不在亮处，或者杆不在）
        Check("shadow_start_unsolved", !sp.Solved && Mathf.Abs(Mathf.DeltaAngle(0f, sp.Angle)) > 60f);
        yield return Snap("innov_shadow_start.png", sp.ScreenCenter, 3.2f, Mathf.Atan2(-sp.ScreenCenter.x + sp.TurntablePos.x, -sp.ScreenCenter.z + sp.TurntablePos.z) * Mathf.Rad2Deg + 40f);
        // 按住 E 转（审计用 DebugHold 代替）
        sp.DebugHold = true;
        float t0 = Time.realtimeSinceStartup;
        while (!sp.Solved && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        sp.DebugHold = false;
        Check("shadow_turn_solves", sp.Solved && Mathf.Abs(sp.Angle) < 0.01f, "angle=" + sp.Angle.ToString("F1"));
        yield return new WaitForSeconds(0.8f);

        // 读布幕上的像素：钥匙头的孔是亮的，钥匙头的圈、杆、两颗齿是暗的，齿下面是亮的
        var ring = sp.DebugJunk("ring");
        var shaft = sp.DebugJunk("shaft");
        bool shapeOk = false; string shapeNote = "missing parts";
        if (ring != null && shaft != null)
        {
            var rb = Lux3DModelFit.WorldBounds(ring.gameObject);
            var sb = Lux3DModelFit.WorldBounds(shaft.gameObject);
            Vector2 hole = sp.DebugScreenUV(rb.center);
            Vector3 side = Vector3.Cross(Vector3.up, (sp.ScreenCenter - sp.TurntablePos).normalized);
            float ringR = Mathf.Max(rb.extents.x, rb.extents.z) * 0.8f;
            Vector2 rim = sp.DebugScreenUV(rb.center + Vector3.up * ringR);
            Vector2 shaftMid = sp.DebugScreenUV(sb.center);
            Vector2 belowShaft = sp.DebugScreenUV(sb.center + Vector3.down * 0.18f + Vector3.Cross(Vector3.up, (sp.ScreenCenter - sp.TurntablePos).normalized) * -0.05f);
            bool holeLit = !sp.DebugIsShadow(hole.x, hole.y);
            bool rimDark = sp.DebugIsShadow(rim.x, rim.y);
            bool shaftDark = sp.DebugIsShadow(shaftMid.x, shaftMid.y);
            bool belowLit = !sp.DebugIsShadow(belowShaft.x, belowShaft.y);
            shapeOk = holeLit && rimDark && shaftDark && belowLit;
            shapeNote = $"holeLit={holeLit} rimDark={rimDark} shaftDark={shaftDark} belowLit={belowLit} hole={hole} shaft={shaftMid}";
        }
        Check("shadow_screen_shows_key", shapeOk, shapeNote);
        yield return Snap("innov_shadow_key.png", sp.ScreenCenter, 3.2f, Mathf.Atan2(-sp.ScreenCenter.x + sp.TurntablePos.x, -sp.ScreenCenter.z + sp.TurntablePos.z) * Mathf.Rad2Deg + 40f);

        // 拿空瓶
        Teleport(sp.BottlePos + (boot.player.transform.position - sp.BottlePos).normalized * 0.6f);
        yield return null;
        bool took = boot.service.TryInteract("sound_bottle_pickup");
        yield return new WaitForSecondsRealtime(0.2f);
        if (boot.ui.LetterOpen) boot.ui.CloseLetter();
        Check("shadow_gives_bottle", took && snd.HasBottle);

        // 装四种声音
        var dog = FindObjectOfType<DogFollower>();
        foreach (var id in SoundBottleSystem.Order)
        {
            Vector3 at;
            if (id == "bark") { dog.Follow(); yield return new WaitForSeconds(0.3f); at = dog.transform.position; }
            else
            {
                var go = GameObject.Find(id == "bell" ? "Bakery_bell" : id == "swing" ? "S_swing" : "Bakery_wood");
                at = go != null ? go.transform.position : Vector3.zero;
            }
            Teleport(FindWalkableNear(at, 0.6f, 1.6f));
            yield return null;
            if (id == "bark") { yield return new WaitForSeconds(0.4f); }
            boot.service.TryInteract("sound_" + id);
            yield return new WaitForSeconds(0.2f);
        }
        Check("sound_all_four", snd.Count == 4, "count=" + snd.Count);

        // 八音盒：房子跟着起伏，演完精确复位
        Teleport(FindWalkableNear(snd.MusicBoxPos, 0.8f, 1.6f));
        yield return null;
        bool played = boot.service.TryInteract("music_box");
        Check("music_box_starts", played && snd.Playing && snd.HouseCount >= 6, "houses=" + snd.HouseCount);
        yield return new WaitForSeconds(3.5f);
        Check("music_box_houses_move", snd.MaxHouseLift > 0.1f, "lift=" + snd.MaxHouseLift.ToString("F2"));
        yield return Snap("innov_music_box.png", snd.MusicBoxPos + Vector3.up * 1.5f, 9f, 200f);
        t0 = Time.realtimeSinceStartup;
        while (snd.Playing && Time.realtimeSinceStartup - t0 < 15f) yield return null;
        float err = snd.DebugHouseError();
        Check("music_box_restores_exactly", !snd.Playing && snd.Done && err < 0.001f && !boot.player.locked, "err=" + err.ToString("F5"));

        st.step = "P1_mailbox";
        yield return new WaitForSeconds(0.3f);
    }

    // ---- WP-E：透视拓印（快门前后画面不变；对准渡口缺口拍下小木尺就成了桥） ----
    IEnumerator AuditPrint()
    {
        var ps = FindObjectOfType<PerspectivePrintSystem>();
        var mech = FindObjectOfType<WorldMechanismDirector>();
        Check("print_present", ps != null && mech != null);
        if (ps == null || mech == null) yield break;
        var st = boot.state;
        var p = boot.player;

        // 1) 面包店门口：快门前后，物件在屏幕上的框不变
        Vector3 stove = Vector3.zero;
        foreach (var it in BakeryData.Load()) if (it.id == "stove") stove = new Vector3(it.x, it.y, it.z);
        Teleport(FindWalkableNear(stove, 3f, 6f));
        yield return null;
        ps.OpenViewfinder();
        yield return null;
        Check("print_open", ps.IsViewfinderActive && p.locked);
        bool hidden = true; foreach (var r in p.GetComponentsInChildren<Renderer>()) if (r.enabled) hidden = false;
        Check("print_hides_player_in_fp", hidden);
        ps.DebugSelect("ruler");
        ps.DebugSetView(p.yaw * Mathf.Rad2Deg, 25f, 0.6f, 0f);
        yield return null;
        ps.DebugSetView(p.yaw * Mathf.Rad2Deg, 25f, 0.6f, 0f);
        yield return SnapScreen("innov_print_viewfinder.png");
        Check("print_aim_valid", ps.AimValid, "k=" + ps.AimScale.ToString("F2"));
        Rect before = ps.DebugScreenRect(ps.DebugHeldModel);
        var go = ps.Shutter();
        Rect after = go != null ? ps.DebugScreenRect(go) : new Rect();
        float tol = Mathf.Max(before.width, before.height) * 0.03f;
        bool same = go != null && Mathf.Abs(before.xMin - after.xMin) <= tol && Mathf.Abs(before.xMax - after.xMax) <= tol
            && Mathf.Abs(before.yMin - after.yMin) <= tol && Mathf.Abs(before.yMax - after.yMax) <= tol;
        Check("print_screen_rect_unchanged", same, $"before={before} after={after}");
        Check("print_scaled_up", go != null && go.transform.lossyScale.x > 1.25f, go != null ? "scale=" + go.transform.lossyScale.x.ToString("F2") : "null");
        Check("print_not_bridge_inland", !ps.LastPrintSnapped);
        // 对着天上：够不着，拍不出来
        ps.DebugSetView(p.yaw * Mathf.Rad2Deg, -30f, 0.6f, 0f);
        Check("print_sky_invalid", !ps.AimValid && ps.Shutter() == null);
        ps.CloseViewfinder();
        yield return null;
        bool shown = false; foreach (var r in p.GetComponentsInChildren<Renderer>()) if (r.enabled) shown = true;
        Check("print_close_restores", !ps.IsViewfinderActive && !p.locked && shown);
        if (go != null) yield return Snap("innov_print_inland.png", go.transform.position, 3.5f, 200f);
        ps.DebugClearPrints();

        // 2) 涨潮的渡口：站在缺口这一头，对准那一头拍下小木尺
        st.step = "L0_cross";
        if (WaterLevel.Main != null) WaterLevel.Main.SetInstant(WaterLevel.LevelYs[2]);
        mech.DebugResetCrossing();
        yield return new WaitForSeconds(0.4f);
        mech.GapEndpoints(out Vector3 a, out Vector3 b);
        Vector3 ab = b - a; ab.y = 0f;
        Vector3 stand = FindWalkableNear(a - ab.normalized * 1.0f, 0f, 1.2f);
        Teleport(stand);
        yield return null;
        Check("print_gap_blocks", !boot.world.Walkable(((a + b) * 0.5f).x, ((a + b) * 0.5f).z));
        ps.OpenViewfinder();
        yield return null;
        ps.DebugSelect("ruler");
        // 找一个拍下去能同时盖住缺口两端的姿势（真人就是这样慢慢调的）
        float yaw = Mathf.Atan2(ab.x, ab.z) * Mathf.Rad2Deg;
        bool found = false; string tried = "";
        for (float hy = 0f; hy <= 90f && !found; hy += 90f)
            for (float hold = 0.35f; hold <= 1.1f && !found; hold += 0.05f)
                for (float pitch = 5f; pitch <= 50f && !found; pitch += 1.5f)
                {
                    ps.DebugSetView(yaw, pitch, hold, hy);
                    if (!ps.AimValid) continue;
                    var hb = Lux3DModelFit.WorldBounds(ps.DebugHeldModel);
                    Vector3 cp = Camera.main.transform.position;
                    var pb = new Bounds(cp + (hb.center - cp) * ps.AimScale, hb.size * ps.AimScale);
                    pb.Expand(new Vector3(1.2f, 8f, 1.2f));
                    if (pb.Contains(new Vector3(a.x, pb.center.y, a.z)) && pb.Contains(new Vector3(b.x, pb.center.y, b.z)))
                    { found = true; tried = $"heldYaw={hy} hold={hold:F2} pitch={pitch:F1} k={ps.AimScale:F1}"; }
                }
        Check("print_gap_aim_found", found, tried);
        yield return SnapScreen("innov_print_gap_aim.png");
        var bridge = ps.Shutter();
        ps.CloseViewfinder();
        yield return new WaitForSeconds(0.3f);
        Vector3 mid = (a + b) * 0.5f;
        Check("print_snaps_to_gap", bridge != null && ps.LastPrintSnapped && mech.CrossingSolved, "snapped=" + ps.LastPrintSnapped);
        Check("print_bridge_walkable", boot.world.Walkable(mid.x, mid.z) && boot.world.Walkable(Vector3.Lerp(a, b, 0.25f).x, Vector3.Lerp(a, b, 0.25f).z)
            && boot.world.Walkable(Vector3.Lerp(a, b, 0.75f).x, Vector3.Lerp(a, b, 0.75f).z),
            $"ground={boot.world.GroundAt(mid.x, mid.z):F2} water={WaterLevel.StaticY:F2}");
        yield return Snap("innov_print_bridge.png", mid, 4.0f, yaw + 100f);

        ps.DebugClearPrints();
        mech.DebugResetCrossing();
        if (WaterLevel.Main != null) WaterLevel.Main.SetInstant(0f);
        st.step = "P1_mailbox";
        yield return new WaitForSeconds(0.5f);
    }

    // ---- WP-C：小黑（F：嗅附近的线索旧物 / 踩浅滩爪印踏板捞木匣 / 摸摸） ----
    IEnumerator AuditDog()
    {
        var dog = FindObjectOfType<DogFollower>();
        var plate = DogPawPlate.Instance;
        Check("dog_present", dog != null && plate != null);
        if (dog == null || plate == null) yield break;
        var st = boot.state;

        // 1) 面包店：小黑跑到最近的旧物旁嗅，地上留粉笔爪印
        st.step = "B1_matches";
        Vector3 recipe = Vector3.zero;
        foreach (var it in BakeryData.Load()) if (it.id == "recipe") recipe = new Vector3(it.x, it.y, it.z);
        Teleport(FindWalkableNear(recipe, 1.5f, 3.5f));
        dog.Follow();
        yield return new WaitForSeconds(0.6f);
        dog.CommandOrPet();
        string cmd = dog.LastCommand;
        Check("dog_sniff_command", cmd.StartsWith("sniff:"), cmd);
        string id = cmd.StartsWith("sniff:") ? cmd.Substring(6) : "";
        Vector3 dogFrom = dog.transform.position;
        float t0 = Time.realtimeSinceStartup;
        while (!st.flags.Contains("dog_sniff_" + id) && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        Check("dog_sniff_runs_and_marks", st.flags.Contains("dog_sniff_" + id) && GameObject.Find("DogClueMark_" + id) != null,
            $"id={id} ran={Vector3.Distance(dogFrom, dog.transform.position):F1}");
        var mark = GameObject.Find("DogClueMark_" + id);
        var tm = mark != null ? mark.GetComponentInChildren<TextMesh>() : null;
        Check("dog_mark_shows_ring_number", tm != null && tm.text.Contains("环"), tm != null ? tm.text : "null");
        if (mark != null) yield return Snap("innov_dog_sniff.png", mark.transform.position, 2.4f, 200f);

        // 2) 附近没东西：就是摸摸
        yield return new WaitForSeconds(2.5f);
        Vector3 far = Vector3.zero;
        var asset = Resources.Load<TextAsset>("World/route");
        var list = asset != null ? JsonUtility.FromJson<RouteList>(asset.text) : null;
        if (list != null) foreach (var r in list.route) if (r.stop && r.id == "03") far = new Vector3(r.x, 0f, r.z);
        Teleport(FindWalkableNear(far, 0f, 3f));
        yield return new WaitForSeconds(0.6f);
        dog.CommandOrPet();
        Check("dog_pet_when_nothing_near", dog.LastCommand == "pet", dog.LastCommand);
        yield return new WaitForSeconds(1.0f);

        // 3) 船棚浅滩：小黑踩踏板，水里升起木匣，打开拿到灯塔星盘的三个刻度
        st.step = "L1_shed";
        foreach (var x in new[] { "hat", "scope", "log" }) st.flags.Remove("dog_sniff_" + x);
        if (WaterLevel.Main != null) WaterLevel.Main.SetInstant(WaterLevel.LevelYs[2]);
        Check("dog_plate_dry_at_L2", boot.world.Walkable(plate.Center.x, plate.Center.z), "plate=" + plate.Center.ToString("F1"));
        Teleport(FindWalkableNear(plate.Center, 2.5f, 4f));
        dog.Follow();
        yield return new WaitForSeconds(0.8f);
        Check("dog_plate_not_yet", !plate.Done);
        dog.CommandOrPet();
        Check("dog_plate_command", dog.LastCommand == "plate", dog.LastCommand);
        t0 = Time.realtimeSinceStartup;
        while (!plate.Done && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        Check("dog_steps_on_plate", plate.Done);
        t0 = Time.realtimeSinceStartup;
        while (!plate.ChestReady && Time.realtimeSinceStartup - t0 < 5f) yield return null;
        Check("dog_chest_rises", plate.ChestReady && boot.world.Walkable(plate.ChestRest.x, plate.ChestRest.z));
        yield return Snap("innov_dog_chest.png", (plate.Center + plate.ChestRest) * 0.5f, 3.2f, 200f);
        Teleport(plate.ChestRest + (boot.player.transform.position - plate.ChestRest).normalized * 0.8f);
        yield return null;
        bool opened = boot.service.TryInteract("paw_chest");
        yield return new WaitForSecondsRealtime(0.3f);
        Check("dog_chest_gives_lighthouse_code", opened && st.flags.Contains("dog_sniff_hat") && st.flags.Contains("dog_sniff_scope") && st.flags.Contains("dog_sniff_log"));
        yield return SnapScreen("innov_dog_chest_note.png");
        if (boot.ui.LetterOpen) boot.ui.CloseLetter();

        // 星盘的邮差笔记里能看到这三个刻度
        WoodenMechanismPuzzleModal.DebugOpen("lighthouse_lock", st, null, boot.player, boot.ui, boot.gate, null);
        yield return new WaitForSecondsRealtime(0.3f);
        var note = GameObject.Find("WoodenMechanismNote");
        string all = ""; if (note != null) foreach (var tx in note.GetComponentsInChildren<UnityEngine.UI.Text>()) all += tx.text;
        Check("dog_code_in_lighthouse_note", all.Contains("外环 6") && all.Contains("中环 2") && all.Contains("内环 3"), all.Replace("\n", " / "));
        WoodenMechanismPuzzleModal.DebugClose();

        st.step = "P1_mailbox";
        if (WaterLevel.Main != null) WaterLevel.Main.SetInstant(0f);
        yield return new WaitForSeconds(0.5f);
    }

    // ---- WP-A：三环木匣（直接摆弄 3D 部件；环上的数字和指针要和真实刻度一致） ----
    IEnumerator AuditWooden()
    {
        var st = boot.state;
        bool solvedCb = false;
        bool opened = WoodenMechanismPuzzleModal.DebugOpen("bakery_lock", st, null, boot.player, boot.ui, boot.gate, () => solvedCb = true);
        yield return new WaitForSecondsRealtime(0.8f);
        Check("wood_open", opened && WoodenMechanismPuzzleModal.IsOpen);
        var note = GameObject.Find("WoodenMechanismNote");
        Check("wood_only_small_note", note != null && note.activeInHierarchy && GameObject.Find("WoodenMechanismParchmentCard") == null);

        bool pointerOk = true; string pointerNote = "";
        for (int r = 0; r < 3; r++)
        {
            int v = WoodenMechanismPuzzleModal.DebugRing(r), seen = WoodenMechanismPuzzleModal.DebugNumberUnderPointer(r);
            pointerNote += $"r{r}={v}/{seen} ";
            if (v != seen) pointerOk = false;
        }
        Check("wood_numbers_match_rings_at_open", pointerOk, pointerNote);
        yield return SnapScreen("innov_wood_open.png");

        // 木栓没拨开：环转不动
        int r0 = WoodenMechanismPuzzleModal.DebugRing(0);
        WoodenMechanismPuzzleModal.ExternalRotateRing(0, 1);
        Check("wood_locked_by_latches", WoodenMechanismPuzzleModal.DebugRing(0) == r0 && !WoodenMechanismPuzzleModal.DebugStage1);
        // 推错一次（木栓还锁着）不扣次数，只震一下
        WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
        Check("wood_push_while_locked_free", WoodenMechanismPuzzleModal.DebugAttemptsLeft == 3);

        // 乙栓一拨，三根全开
        WoodenMechanismPuzzleModal.ExternalToggleLatch(1);
        Check("wood_latch_b_opens_all", WoodenMechanismPuzzleModal.DebugStage1);

        // 拖外环 60°：外环 +1，中环跟着 +1，内环不动
        int a0 = WoodenMechanismPuzzleModal.DebugRing(0), a1 = WoodenMechanismPuzzleModal.DebugRing(1), a2 = WoodenMechanismPuzzleModal.DebugRing(2);
        WoodenMechanismPuzzleModal.DebugDragRing(0, 62f);
        int b0 = WoodenMechanismPuzzleModal.DebugRing(0), b1 = WoodenMechanismPuzzleModal.DebugRing(1), b2 = WoodenMechanismPuzzleModal.DebugRing(2);
        Check("wood_drag_outer_couples_middle", b0 == a0 % 6 + 1 && b1 == a1 % 6 + 1 && b2 == a2, $"{a0}{a1}{a2}->{b0}{b1}{b2}");
        // 拖得不够半格不算
        WoodenMechanismPuzzleModal.DebugDragRing(0, -20f);
        Check("wood_small_drag_ignored", WoodenMechanismPuzzleModal.DebugRing(0) == b0);
        WoodenMechanismPuzzleModal.DebugDragRing(0, 20f);
        yield return new WaitForSecondsRealtime(0.8f);
        pointerOk = true; pointerNote = "";
        for (int r = 0; r < 3; r++)
        {
            int v = WoodenMechanismPuzzleModal.DebugRing(r), seen = WoodenMechanismPuzzleModal.DebugNumberUnderPointer(r);
            pointerNote += $"r{r}={v}/{seen} ";
            if (v != seen) pointerOk = false;
        }
        Check("wood_numbers_follow_rotation", pointerOk, pointerNote);

        // 刻度不对推主轴：扣一次、乙栓弹回
        WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
        Check("wood_wrong_push_costs_one", WoodenMechanismPuzzleModal.DebugAttemptsLeft == 2 && !WoodenMechanismPuzzleModal.DebugStage1);
        var noteText = note != null ? note.GetComponentsInChildren<UnityEngine.UI.Text>() : new UnityEngine.UI.Text[0];
        string allNote = ""; foreach (var tx in noteText) allNote += tx.text;
        Check("wood_note_shows_attempts", allNote.Contains("●●○") && allNote.Contains("？"), allNote.Replace("\n", " / "));

        // 反震后三根栓是"开、关、开"：只拨乙栓已经不够了，要换个拨法
        WoodenMechanismPuzzleModal.ExternalToggleLatch(1);
        Check("wood_rebound_needs_new_order", !WoodenMechanismPuzzleModal.DebugStage1);
        WoodenMechanismPuzzleModal.DebugOpenAllLatches();
        Check("wood_rebound_solvable", WoodenMechanismPuzzleModal.DebugStage1);
        // 按"外 → 中 → 内"的顺序拨到 3-5-2，推进去
        int[] target = { 3, 5, 2 };
        for (int r = 0; r < 3; r++)
        {
            int guard = 0;
            while (WoodenMechanismPuzzleModal.DebugRing(r) != target[r] && guard++ < 8) WoodenMechanismPuzzleModal.ExternalRotateRing(r, 1);
        }
        yield return new WaitForSecondsRealtime(0.7f);
        yield return SnapScreen("innov_wood_aligned.png");
        Check("wood_aligned", WoodenMechanismPuzzleModal.DebugRing(0) == 3 && WoodenMechanismPuzzleModal.DebugRing(1) == 5 && WoodenMechanismPuzzleModal.DebugRing(2) == 2);
        WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
        Check("wood_solves", WoodenMechanismPuzzleModal.DebugSolved);
        float t0 = Time.realtimeSinceStartup;
        while (!solvedCb && Time.realtimeSinceStartup - t0 < 3f) yield return null;
        Check("wood_callback_and_closes", solvedCb && !WoodenMechanismPuzzleModal.IsOpen && st.flags.Contains("puzzle_bakery_lock") && !boot.player.locked);
        st.flags.Remove("puzzle_bakery_lock");

        // 第三次推错：反震（关掉机关、退回拿火柴那一步）
        st.step = "B4_light";
        if (!st.flags.Contains("matches_taken")) st.flags.Add("matches_taken");
        WoodenMechanismPuzzleModal.DebugOpen("bakery_lock", st, null, boot.player, boot.ui, boot.gate, null);
        yield return null;
        for (int i = 0; i < 3; i++) { WoodenMechanismPuzzleModal.DebugOpenAllLatches(); WoodenMechanismPuzzleModal.ExternalAttemptUnlock(); }
        yield return null;
        Check("wood_third_failure_jams", !WoodenMechanismPuzzleModal.IsOpen && !st.flags.Contains("matches_taken") && st.step == "B1_matches", "step=" + st.step);
        WoodenMechanismPuzzleModal.DebugClose();
        st.step = "P1_mailbox";
        yield return new WaitForSecondsRealtime(0.3f);
    }

    // ---- WP-F：潮汐残影（绞盘一个人摇不动；你 + 10 秒前的你一起按住才摇得起来） ----
    IEnumerator AuditEcho()
    {
        var echo = FindObjectOfType<TidalEchoCloneSystem>();
        var mech = FindObjectOfType<WorldMechanismDirector>();
        Check("echo_system_present", echo != null && mech != null);
        if (echo == null || mech == null) yield break;
        var st = boot.state;
        st.step = "L0_cross";
        if (WaterLevel.Main != null) WaterLevel.Main.SetInstant(WaterLevel.LevelYs[2]);
        mech.DebugResetCrossing();
        yield return new WaitForSeconds(0.3f);
        Vector3 ch = mech.GetCrossingChannelCenter();
        Vector3 winch = mech.WinchPosition;
        Check("echo_gap_blocks", mech.GapActive && !boot.world.Walkable(ch.x, ch.z), "gap=" + mech.GapActive);
        Check("echo_winch_on_dry_land", boot.world.Walkable(winch.x, winch.z), "winch=" + winch.ToString("F2"));

        // 一个人：怎么摇都只是晃，不会升桥
        Teleport(winch + new Vector3(0.3f, 0f, 0.3f));
        yield return null;
        mech.DebugPlayerHoldsWinch = true;
        yield return new WaitForSeconds(3.5f);
        Check("echo_alone_cannot_crank", !mech.CrossingSolved && mech.WinchHolders == 1 && mech.WinchStrained, $"holders={mech.WinchHolders} solved={mech.CrossingSolved}");
        mech.DebugPlayerHoldsWinch = false;
        yield return new WaitForSeconds(0.3f);

        // 攒的时间不够叫不出来
        echo.DebugClear();
        for (int i = 0; i < 20; i++) echo.DebugPushFrame(winch, 0f, Vector3.zero, true, "winch");
        Check("echo_needs_buffer", !echo.Summon() && !echo.EchoActive, "buffered=" + echo.BufferedSeconds.ToString("F1"));

        // 录一段"10 秒前"：先从 3 米外走到绞盘（2 秒），再在绞盘旁按住 E（4 秒）
        echo.DebugClear();
        Vector3 start = FindWalkableNear(winch, 2.5f, 3.5f);
        start.y = boot.world.GroundAt(start.x, start.z, winch.y + 1f);
        float yawWalk = Mathf.Atan2(winch.x - start.x, winch.z - start.z);
        for (int i = 0; i <= 40; i++)
        {
            float k = i / 40f;
            Vector3 p = Vector3.Lerp(start, winch, k);
            p.y = boot.world.GroundAt(p.x, p.z, winch.y + 1f);
            Vector3 v = (winch - start) / 2f; v.y = 0f;
            echo.DebugPushFrame(p, yawWalk, i < 40 ? v : Vector3.zero, true, "");
        }
        for (int i = 0; i < 80; i++) echo.DebugPushFrame(winch, yawWalk, Vector3.zero, true, "winch");
        Check("echo_summon", echo.Summon() && echo.EchoActive);

        // 走路那一段：残影真的在走，腿在动（步态层在干活），不是平移
        Vector3 e0 = echo.EchoPosition;
        float maxSpeed = 0f, maxLegSwing = 0f;
        var puppet = GameObject.Find("EGGBOY_TidalEcho");
        var pp = puppet != null ? puppet.GetComponent<EggboyPlayer>() : null;
        Transform footL = pp != null && pp.Bones != null ? pp.Bones.Bone("footL") : null;
        Vector3 footStart = Vector3.zero;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 1.6f)
        {
            // 要在一帧的末尾量：残影在 Update 里先把骨头还原成站姿，步态层到 LateUpdate 才迈腿
            yield return new WaitForEndOfFrame();
            maxSpeed = Mathf.Max(maxSpeed, echo.EchoSpeed);
            if (footL != null)
            {
                Vector3 rel = Quaternion.Inverse(puppet.transform.rotation) * (footL.position - puppet.transform.position);
                if (footStart == Vector3.zero) footStart = rel;
                maxLegSwing = Mathf.Max(maxLegSwing, Vector3.Distance(rel, footStart));
            }
        }
        Check("echo_walks", Vector3.Distance(echo.EchoPosition, e0) > 1.0f && maxSpeed > 1.0f, $"moved={Vector3.Distance(echo.EchoPosition, e0):F2} speed={maxSpeed:F2}");
        Check("echo_legs_move", maxLegSwing > 0.03f, $"footSwing={maxLegSwing:F3}");
        bool ghostLook = false;
        if (puppet != null)
        {
            var r = puppet.GetComponentInChildren<SkinnedMeshRenderer>();
            ghostLook = r != null && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("TidalEchoGhost");
        }
        Check("echo_ghost_material", ghostLook);
        yield return Snap("innov_echo_walk.png", echo.EchoPosition, 3.0f, 200f);

        // 残影走到绞盘旁按住了 E：玩家也按住，两个人一起摇
        t0 = Time.realtimeSinceStartup;
        while (echo.EchoHoldZone != "winch" && Time.realtimeSinceStartup - t0 < 3f) yield return null;
        Check("echo_holds_winch", echo.EchoHoldZone == "winch");
        Teleport(winch + new Vector3(-0.35f, 0f, 0.25f));
        mech.DebugPlayerHoldsWinch = true;
        yield return new WaitForSeconds(0.4f);
        Check("echo_two_holders", mech.WinchHolders == 2, "holders=" + mech.WinchHolders);
        yield return Snap("innov_echo_crank.png", winch, 3.2f, 200f);
        t0 = Time.realtimeSinceStartup;
        while (!mech.CrossingSolved && Time.realtimeSinceStartup - t0 < 5f) yield return null;
        mech.DebugPlayerHoldsWinch = false;
        Check("echo_crank_raises_bridge", mech.CrossingSolved, "crank=" + mech.WinchCrank01.ToString("F2"));
        yield return new WaitForSeconds(2.0f); // 栈桥浮上来
        Check("echo_bridge_walkable", boot.world.Walkable(ch.x, ch.z), $"ground={boot.world.GroundAt(ch.x, ch.z):F2} water={WaterLevel.StaticY:F2}");
        yield return Snap("innov_echo_bridge.png", ch, 5.0f, 200f);

        // 演完会自己化掉；再按一次 R 也能提前收起
        t0 = Time.realtimeSinceStartup;
        while (echo.EchoActive && Time.realtimeSinceStartup - t0 < 6f) yield return null;
        Check("echo_fades_out", !echo.EchoActive && (puppet == null || !puppet.activeSelf));

        // 还原，给后面的心愿造船测试留一个没解决的渡口
        mech.DebugResetCrossing();
        echo.DebugClear();
        yield return new WaitForSeconds(2.0f);
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
