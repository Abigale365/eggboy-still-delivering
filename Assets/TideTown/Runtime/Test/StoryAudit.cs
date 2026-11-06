// WP-07 自动剧情审计；WP-10 覆盖序章，WP-11 覆盖面包店（到点读信、火柴→柴火→烤炉、第一枚邮戳、L1），
// WP-12 检查小黑跟随。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class StoryAudit : MonoBehaviour
{
    [Serializable] class Wp { public string id; public bool stop; public float x, z; }
    [Serializable] class RouteList { public Wp[] route; }

    GameBootstrap boot;
    float sumFps; int frames; string error = "";
    string status = "passed";
    string walkFail = "";
    DogFollower dog;
    float dogMax, dogMinClear = 999f;

    public void Begin(GameBootstrap b) { boot = b; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(0.5f);
        Time.timeScale = 1f;
        var p = boot.player;
        dog = FindObjectOfType<DogFollower>();
        float bootAt = boot.intro != null ? boot.intro.StartTime : Time.realtimeSinceStartup;

        bool introRan = boot.intro != null;
        bool introDone = !introRan;
        bool faceOk = true;
        float introSeconds = 0f;
        string exprStart = p.pose != null ? p.pose.expr.ToString() : "";
        string exprEnd = exprStart;

        // 开场：先等镜头飞下来，再开始走路
        if (introRan)
        {
            yield return new WaitForSeconds(1.5f);
            yield return Snap("story_boot.png");
            while (!boot.intro.Done && Time.realtimeSinceStartup - bootAt < 12f) yield return null;
            introDone = boot.intro.Done;
            introSeconds = Time.realtimeSinceStartup - bootAt;
            exprEnd = p.pose != null ? p.pose.expr.ToString() : "";
            faceOk = exprStart == ExprId.sleepy.ToString() && exprEnd == ExprId.happy.ToString();
            if (!introDone) { status = "intro_timeout"; error = "intro did not finish in 12 s"; }
            else if (!faceOk) { status = "intro_face_wrong"; error = exprStart + " -> " + exprEnd; }
            yield return Snap("story_wake.png");
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        var asset = Resources.Load<TextAsset>("World/route");
        var route = JsonUtility.FromJson<RouteList>(asset.text).route;
        int i03 = -1, i11 = -1, i12 = -1, i13 = -1;
        for (int i = 0; i < route.Length; i++)
        {
            if (i03 < 0 && route[i].id == "03") i03 = i;
            else if (i11 < 0 && route[i].id == "11") i11 = i;
            else if (i12 < 0 && route[i].id == "12") i12 = i;
            else if (i13 < 0 && route[i].id == "13") i13 = i;
        }

        // 07 → 03 取信
        if (status == "passed")
        {
            yield return WalkRoute(route, 0, i03);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
        }

        bool opened = false;
        float pickupSeconds = 0f;
        if (status == "passed")
        {
            yield return new WaitForSeconds(0.4f);
            if (!boot.service.TryInteract("mailbox_03")) { status = "interact_failed"; error = "no mailbox in range"; }
            else
            {
                for (int i = 0; i < 90; i++) { if (boot.ui.LetterOpen) { opened = true; break; } yield return null; }
                if (!opened) { status = "letter_not_open"; error = "ui.LetterOpen false"; }
            }
        }

        pickupSeconds = Time.realtimeSinceStartup - bootAt;
        bool within30 = pickupSeconds <= 30f;
        if (status == "passed") yield return Snap("story_pickup.png");
        if (status == "passed" && !within30) { status = "pickup_too_slow"; error = pickupSeconds.ToString("F1") + " s"; }

        bool stepOk = false;
        bool lockedMove = false;
        bool noRepeat = false;
        if (status == "passed")
        {
            stepOk = boot.state.step == "P1_mailbox" && boot.state.flags.Contains("letters_taken");
            var before = p.transform.position;
            p.Move(0.05f, 1f, 0f, false, false);
            lockedMove = (p.transform.position - before).magnitude < 0.01f;
            boot.ui.CloseLetter();
            yield return new WaitForSeconds(0.3f);
            noRepeat = !boot.service.TryInteract("mailbox_03");
            if (!stepOk) { status = "state_wrong"; error = "step=" + boot.state.step; }
            else if (p.locked) { status = "still_locked"; error = "player.locked true"; }
            else if (!lockedMove) { status = "move_while_reading"; error = "player moved with letter open"; }
            else if (!noRepeat) { status = "repeat_interact"; error = "mailbox still interactable"; }
            else if (boot.state.step != "B0_arrive") { status = "b0_missing"; error = "step=" + boot.state.step; }
        }

        // 面包店：B0 到点自动读信 → 回忆线索 → 火柴 → 柴火 → 装炉 → 点火
        if (status == "passed") yield return SnapDog("story_dog.png");
        float bakeryAt = Time.realtimeSinceStartup;
        bool b0Auto = false, memoryOk = false, matchesOk = false, woodOk = false, loadOk = false;
        bool litOk = false, stampOk = false, waterOk = false, smokeOk = false;
        BakeryEffects fx = null;
        if (status == "passed")
        {
            yield return WalkRoute(route, i03 + 1, i11);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }

            for (int i = 0; i < 120 && status == "passed"; i++)
            {
                if (boot.ui.LetterOpen) { b0Auto = true; break; }
                yield return null;
            }
            if (status == "passed" && !b0Auto) { status = "b0_not_auto"; error = "letter did not open at 11 [" + boot.director.DebugAuto + "]"; }
            else if (b0Auto)
            {
                yield return Snap("story_bakery_letter.png");
                boot.ui.CloseLetter();
                yield return new WaitForSeconds(0.3f);
                if (boot.state.step != "B1_matches") { status = "b1_missing"; error = "step=" + boot.state.step; }
            }
        }

        if (status == "passed")
        {
            var basket = Find("basket");
            if (basket == null) { status = "bakery_data"; error = "basket missing"; }
            else
            {
                yield return WalkTo(basket.x, basket.z, "basket");
                if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
                else if (!boot.service.TryInteract("basket")) { status = "memory_failed"; error = "basket not in range"; }
                else
                {
                    for (int i = 0; i < 30 && !boot.ui.LetterOpen; i++) yield return null;
                    memoryOk = boot.state.seenMemories.Contains("basket") && boot.ui.LetterOpen;
                    boot.ui.CloseLetter();
                    yield return new WaitForSeconds(0.25f);
                    if (!memoryOk) { status = "memory_failed"; error = "basket memory not opened"; }
                }
            }
        }

        if (status == "passed")
        {
            var stall = Find("stall");
            yield return WalkTo(stall.x, stall.z, "stall");
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            else if (!boot.service.TryInteract("stall")) { status = "stall_failed"; error = "stall not in range"; }
            else
            {
                yield return new WaitForSeconds(0.25f);
                matchesOk = boot.state.step == "B2_wood" && boot.state.carrying == "matches";
                if (!matchesOk) { status = "matches_failed"; error = "step=" + boot.state.step + " carry=" + boot.state.carrying; }
            }
        }

        if (status == "passed")
        {
            yield return WalkRoute(route, i11 + 1, i12);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            else if (!boot.service.TryInteract("wood")) { status = "wood_failed"; error = "wood not in range"; }
            else
            {
                yield return new WaitForSeconds(0.25f);
                woodOk = boot.state.step == "B3_load" && boot.state.carrying == "wood";
                if (!woodOk) { status = "wood_failed"; error = "step=" + boot.state.step + " carry=" + boot.state.carrying; }
            }
        }

        if (status == "passed")
        {
            yield return WalkRoute(route, i12 + 1, i13);
            var stove = Find("stove");
            yield return WalkTo(stove.x, stove.z, "stove");
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            else if (!boot.service.TryInteract("stove")) { status = "stove_failed"; error = "stove not in range"; }
            else
            {
                yield return new WaitForSeconds(0.25f);
                loadOk = boot.state.step == "B4_light" && boot.state.carrying == "";
                if (loadOk && boot.service.TryInteract("stove"))
                {
                    yield return new WaitForSeconds(0.4f);
                    litOk = boot.state.flags.Contains("oven_lit");
                    stampOk = boot.state.stamps.Contains("bakery");
                    fx = FindObjectOfType<BakeryEffects>();
                    smokeOk = fx != null && fx.Lit && fx.HasSmoke;
                    yield return new WaitForSeconds(2.6f);
                    waterOk = boot.water != null && Mathf.Abs(boot.water.Y - 0.45f) <= 0.06f;
                    yield return new WaitForSeconds(2.4f); // 让烟爬出烟囱，再拍远景
                }
                else if (loadOk) { status = "stove_failed"; error = "second E did nothing"; }
                else { status = "load_failed"; error = "step=" + boot.state.step + " carry=" + boot.state.carrying; }
            }
            if (status == "passed") yield return SnapFrom("story_oven.png", new Vector3(0.5f, 6.2f, -11.5f), new Vector3(-2.2f, 4.8f, -1.5f));
        }

        if (status == "passed" && !(litOk && stampOk && waterOk && smokeOk))
        {
            status = "fire_failed";
            error = $"lit={litOk} stamp={stampOk} water={waterOk} smoke={smokeOk}";
        }

        bool saveOk = false;
        if (status == "passed")
        {
            var loaded = new SaveStore().Load();
            saveOk = loaded != null && loaded.step == "B4_light" && loaded.flags.Contains("oven_lit")
                && loaded.flags.Contains("letters_taken") && loaded.stamps.Contains("bakery")
                && loaded.seenMemories.Contains("basket");
            if (!saveOk) { status = "save_failed"; error = "reload mismatch"; }
            // 恢复现场，避免审计把玩家存档带偏
            new SaveStore().Clear();
        }

        // WP-12：小黑全程跟随、贴地、收尾时在身后
        float dogEnd = 999f;
        bool dogBehind = false;
        bool dogOk = false;
        if (dog != null && dog.Bound && p != null)
        {
            Vector3 dp = dog.transform.position, pp = p.transform.position;
            dogEnd = new Vector2(dp.x - pp.x, dp.z - pp.z).magnitude;
            var fwd = new Vector3(Mathf.Sin(p.yaw), 0f, Mathf.Cos(p.yaw));
            dogBehind = Vector3.Dot(new Vector3(dp.x - pp.x, 0f, dp.z - pp.z), fwd) < 0.05f;
            dogOk = dogMax <= 4.0f && dogMinClear >= -0.05f && dogEnd <= 3.0f && dogBehind;
        }
        if (status == "passed" && !dogOk)
        {
            status = "dog_failed";
            error = $"max={dogMax:F2} end={dogEnd:F2} clear={dogMinClear:F2} behind={dogBehind}";
        }

        float secs = Time.realtimeSinceStartup - bakeryAt;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"status\": \"" + status + "\",");
        sb.AppendLine("  \"steps\": { \"P0_boot\": " + (introDone ? "true" : "false")
                  + ", \"P1_mailbox\": " + (stepOk ? "true" : "false")
                  + ", \"B0_arrive\": " + (b0Auto ? "true" : "false")
                  + ", \"B1_matches\": " + (matchesOk ? "true" : "false")
                  + ", \"B2_wood\": " + (woodOk ? "true" : "false")
                  + ", \"B3_load\": " + (loadOk ? "true" : "false")
                  + ", \"B4_light\": " + (litOk ? "true" : "false") + " },");
        sb.AppendLine("  \"intro\": { \"ran\": " + (introRan ? "true" : "false")
                  + ", \"seconds\": " + introSeconds.ToString("F1")
                  + ", \"start_expr\": \"" + exprStart + "\""
                  + ", \"end_expr\": \"" + exprEnd + "\""
                  + ", \"face_ok\": " + (faceOk ? "true" : "false")
                  + ", \"shots\": [\"story_boot.png\", \"story_wake.png\"] },");
        sb.AppendLine("  \"pickup_seconds\": " + pickupSeconds.ToString("F1") + ",");
        sb.AppendLine("  \"pickup_within_30s\": " + (within30 ? "true" : "false") + ",");
        sb.AppendLine("  \"bakery\": { \"seconds\": " + secs.ToString("F1")
                  + ", \"auto_letter\": " + (b0Auto ? "true" : "false")
                  + ", \"memory\": " + (memoryOk ? "true" : "false")
                  + ", \"matches\": " + (matchesOk ? "true" : "false")
                  + ", \"wood\": " + (woodOk ? "true" : "false")
                  + ", \"loaded\": " + (loadOk ? "true" : "false")
                  + ", \"lit\": " + (litOk ? "true" : "false")
                  + ", \"stamp\": " + (stampOk ? "true" : "false")
                  + ", \"water_l1\": " + (waterOk ? "true" : "false")
                  + ", \"smoke\": " + (smokeOk ? "true" : "false")
                  + ", \"fire_particles\": " + (fx != null ? fx.FireCount : 0)
                  + ", \"smoke_particles\": " + (fx != null ? fx.SmokeCount : 0)
                  + ", \"fire_visible\": " + (fx != null && fx.FireVisible ? "true" : "false")
                  + ", \"smoke_visible\": " + (fx != null && fx.SmokeVisible ? "true" : "false")
                  + ", \"shots\": [\"story_bakery_letter.png\", \"story_oven.png\"] },");
        sb.AppendLine("  \"save_reload\": " + (saveOk ? "true" : "false") + ",");
        var visual = dog != null ? dog.GetComponent<DogVisual>() : null;
        sb.AppendLine("  \"dog\": { \"followed\": " + (dog != null && dog.Bound ? "true" : "false")
                  + ", \"follow_max\": " + dogMax.ToString("F2")
                  + ", \"distance_end\": " + dogEnd.ToString("F2")
                  + ", \"behind\": " + (dogBehind ? "true" : "false")
                  + ", \"ground_error_min\": " + dogMinClear.ToString("F2")
                  + ", \"wag_hz\": " + (dog != null ? dog.WagHz.ToString("F1") : "0")
                  + ", \"visual\": \"" + (visual != null ? visual.GetType().Name : "none") + "\""
                  + ", \"shots\": [\"story_dog.png\"] },");
        sb.AppendLine("  \"letter_open\": " + (opened ? "true" : "false") + ",");
        sb.AppendLine("  \"move_locked\": " + (lockedMove ? "true" : "false") + ",");
        sb.AppendLine("  \"no_repeat\": " + (noRepeat ? "true" : "false") + ",");
        sb.AppendLine("  \"avg_fps\": " + (frames > 0 ? (sumFps / frames).ToString("F1") : "0") + ",");
        sb.AppendLine("  \"errors\": \"" + error + "\"");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "story-audit.json"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log("STORYAUDIT " + status + " intro=" + introSeconds.ToString("F1") + " pickup=" + pickupSeconds.ToString("F1")
            + " bakery=" + secs.ToString("F1") + " lit=" + litOk + " save=" + saveOk);
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    BakeryItem Find(string id)
    {
        foreach (var it in BakeryData.Load()) if (it.id == id) return it;
        return null;
    }

    IEnumerator WalkRoute(Wp[] route, int from, int to)
    {
        if (to < 0) { walkFail = "route_index"; yield break; }
        for (int i = Mathf.Max(0, from); i <= to; i++)
        {
            yield return WalkTo(route[i].x, route[i].z, route[i].id);
            if (walkFail != "" || boot.ui.LetterOpen) yield break;
        }
    }

    IEnumerator WalkTo(float x, float z, string label)
    {
        float t0 = Time.realtimeSinceStartup;
        while (true)
        {
            // 到点自动读信会锁人，停在半径外也算到达
            if (boot.ui.LetterOpen) yield break;
            var pos = boot.player.transform.position;
            float dx = x - pos.x, dz = z - pos.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < 0.8f) yield break;
            if (Time.realtimeSinceStartup - t0 > 30f) { walkFail = label; yield break; }
            boot.player.Move(0.05f, dx / Mathf.Max(0.01f, d), dz / Mathf.Max(0.01f, d), false, false);
            yield return null;
        }
    }

    // 从玩家背后拍，小黑在玩家身后 1.2 米，正好进画面
    IEnumerator SnapDog(string file)
    {
        var p = boot.player;
        var fwd = new Vector3(Mathf.Sin(p.yaw), 0f, Mathf.Cos(p.yaw));
        var from = p.transform.position - fwd * 4.4f + Vector3.up * 1.8f;
        var look = p.transform.position + Vector3.up * 0.55f;
        yield return SnapFrom(file, from, look);
    }

    // 全屏截图带 HUD，给评审看目标、教学提示、信纸和炉火
    IEnumerator Snap(string file)
    {
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    // 固定机位拍证据，拍完把镜头还给 ChaseCamera
    IEnumerator SnapFrom(string file, Vector3 from, Vector3 look)
    {
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        var chase = FindObjectOfType<ChaseCamera>();
        bool chaseOn = chase != null && chase.enabled;
        if (chase != null) chase.enabled = false;
        var p0 = cam.transform.position;
        var r0 = cam.transform.rotation;
        float f0 = cam.fieldOfView;
        cam.transform.position = from;
        cam.transform.rotation = Quaternion.LookRotation(look - from, Vector3.up);
        cam.fieldOfView = 42f;
        yield return null;
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
        yield return new WaitForSecondsRealtime(0.6f);
        cam.transform.SetPositionAndRotation(p0, r0);
        cam.fieldOfView = f0;
        if (chase != null) chase.enabled = chaseOn;
    }

    void Update()
    {
        if (Time.deltaTime > 0f) { sumFps += 1f / Time.deltaTime; frames++; }
        if (dog != null && dog.Bound && boot != null && boot.player != null && boot.world != null)
        {
            var dp = dog.transform.position;
            var pp = boot.player.transform.position;
            float gap = new Vector2(dp.x - pp.x, dp.z - pp.z).magnitude;
            if (Time.timeScale > 0f) dogMax = Mathf.Max(dogMax, gap);
            dogMinClear = Mathf.Min(dogMinClear, dp.y - boot.world.GroundAt(dp.x, dp.z));
        }
    }
}
