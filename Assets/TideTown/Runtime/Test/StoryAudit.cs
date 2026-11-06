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

    public void Begin(GameBootstrap b)
    {
        boot = b;
        if (boot.ui != null) boot.ui.SetAuditMode(true);
        StartCoroutine(Run());
    }

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
        int i03 = -1, i11 = -1, i12 = -1, i13 = -1, i20 = -1, i21 = -1;
        int i22 = -1, i22b = -1, i25 = -1, i30 = -1, i30b = -1, i07 = -1, i06 = -1;
        for (int i = 0; i < route.Length; i++)
        {
            if (i03 < 0 && route[i].id == "03") i03 = i;
            else if (i11 < 0 && route[i].id == "11") i11 = i;
            else if (i12 < 0 && route[i].id == "12") i12 = i;
            else if (i13 < 0 && route[i].id == "13") i13 = i;
            else if (i20 < 0 && route[i].id == "20") i20 = i;
            else if (i21 < 0 && route[i].id == "21") i21 = i;
            // 回程用第二个 07（路线末尾那个），不是起点
            if (route[i].id == "07") i07 = i;
            if (route[i].id == "06") i06 = i;
            if (route[i].id == "22") { if (i22 < 0) i22 = i; else if (i22b < 0) i22b = i; }
            if (i25 < 0 && route[i].id == "25") i25 = i;
            if (route[i].id == "30") { if (i30 < 0) i30 = i; else if (i30b < 0) i30b = i; }
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
        float bakeryAt = Time.realtimeSinceStartup;
        bool b0Auto = false, memoryOk = false, matchesOk = false, woodOk = false, loadOk = false;
        bool litOk = false, stampOk = false, waterOk = false, smokeOk = false;
        bool fireVisibleOven = false, smokeVisibleOven = false;
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
            // 火苗/烟的可见性必须在对准烤炉的镜头下采样；放到结尾采样时相机已离开，isVisible 恒为 false
            if (fx != null) { fireVisibleOven = fx.FireVisible; smokeVisibleOven = fx.SmokeVisible; }
        }

        if (status == "passed" && !(litOk && stampOk && waterOk && smokeOk))
        {
            status = "fire_failed";
            error = $"lit={litOk} stamp={stampOk} water={waterOk} smoke={smokeOk}";
        }

        // WP-13：学校后门读信 → 拖鞋 → 操场红球 → 放回狗碗 → 逐行再读 → 小黑跟随
        float schoolAt = Time.realtimeSinceStartup;
        bool s0Auto = false, slipperOk = false, ballOk = false, bowlOk = false, schoolRead = false;
        bool stamp2 = false, waterL2 = false, ballInBowl = false, dogPeek = false;
        int linesShown = 0, linesTotal = 0;
        string exprTrace = "";

        if (status == "passed")
        {
            yield return WalkRoute(route, i13 + 1, i20);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }

            for (int i = 0; i < 120 && status == "passed"; i++)
            {
                if (boot.ui.LetterOpen) { s0Auto = true; break; }
                yield return null;
            }
            if (status == "passed" && !s0Auto) { status = "s0_not_auto"; error = "letter did not open at 20 [" + boot.director.DebugAuto + "]"; }
            else if (s0Auto)
            {
                yield return Snap("story_school_letter.png");
                if (boot.state.step != "S0_arrive" || boot.state.flags.Contains("ball_in_bowl"))
                { status = "s0_wrong"; error = "step=" + boot.state.step; }
                boot.ui.CloseLetter();
                yield return new WaitForSeconds(0.3f);
                if (status == "passed" && boot.state.step != "S1_slipper") { status = "s1_missing"; error = "step=" + boot.state.step; }
            }
        }

        if (status == "passed") yield return SnapFrom("story_pawprints.png", new Vector3(11.8f, 6.1f, 6.9f), new Vector3(14.3f, 3.3f, 0.6f));

        // S1：爪印尽头的拖鞋
        if (status == "passed")
        {
            var slipper = SchoolFind("slipper");
            if (slipper == null) { status = "school_data"; error = "slipper missing"; }
            else
            {
                yield return WalkTo(slipper.x, slipper.z, "slipper");
                if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
                else if (!boot.service.TryInteract("slipper")) { status = "slipper_failed"; error = "slipper not in range"; }
                else
                {
                    yield return new WaitForSeconds(0.25f);
                    slipperOk = boot.state.step == "S2_ball";
                    if (!slipperOk) { status = "slipper_failed"; error = "step=" + boot.state.step; }
                }
            }
        }

        // S2：操场秋千下的红球
        if (status == "passed")
        {
            yield return WalkRoute(route, i20, i21);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            else
            {
                var ball = SchoolFind("ball");
                if (ball == null) { status = "school_data"; error = "ball missing"; }
                else
                {
                    yield return WalkTo(ball.x, ball.z, "ball");
                    if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
                    else
                    {
                        // 先拍秋千下的红球，再拿走，截图才有“球在秋千下”的证据
                        if (status == "passed") yield return SnapFrom("story_playground.png", new Vector3(13.8f, 5.8f, -8.6f), new Vector3(9.8f, 3.4f, -4.8f));
                        if (!boot.service.TryInteract("ball")) { status = "ball_failed"; error = "ball not in range"; }
                        else
                        {
                            yield return new WaitForSeconds(0.25f);
                            ballOk = boot.state.step == "S3_bowl" && boot.state.carrying == "ball";
                            if (!ballOk) { status = "ball_failed"; error = "step=" + boot.state.step + " carry=" + boot.state.carrying; }
                        }
                    }
                }
            }
        }

        // S3：把球放回学校后门的狗碗
        if (status == "passed")
        {
            yield return WalkRouteBack(route, i21, i20);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            else
            {
                var bowl = SchoolFind("bowl");
                if (bowl == null) { status = "school_data"; error = "bowl missing"; }
                else
                {
                    yield return WalkTo(bowl.x, bowl.z, "bowl");
                    if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
                    else if (!boot.service.TryInteract("bowl")) { status = "bowl_failed"; error = "bowl not in range"; }
                    else
                    {
                        yield return new WaitForSeconds(0.25f);
                        bowlOk = boot.state.step == "S4_read" && boot.state.carrying == "" && boot.state.flags.Contains("ball_in_bowl");
                        var ballComp = FindObjectOfType<SchoolBall>();
                        ballInBowl = ballComp != null && ballComp.InBowl;
                        dogPeek = dog != null && dog.Visible && !dog.Following;
                        // 逐行读信：等信纸打开、前两行出现后再拍
                        yield return new WaitForSecondsRealtime(2.8f);
                        if (status == "passed") yield return Snap("story_school_read.png");
                    }
                }
            }
        }

        if (status == "passed" && !(bowlOk && ballInBowl && dogPeek))
        {
            status = "school_failed";
            error = $"bowl={bowlOk} ball={ballInBowl} peek={dogPeek}";
        }

        // S4：读完信、盖第二枚邮戳、水位 L2、小黑开始跟随
        if (status == "passed")
        {
            float t0 = Time.realtimeSinceStartup;
            while (!boot.state.flags.Contains("school_read") && Time.realtimeSinceStartup - t0 < 22f) yield return null;
            schoolRead = boot.state.flags.Contains("school_read");
            stamp2 = boot.state.stamps.Contains("school");
            linesShown = boot.director.SchoolLinesShown;
            linesTotal = boot.director.SchoolLinesTotal;
            exprTrace = boot.director.SchoolExprTrace;
            yield return new WaitForSecondsRealtime(3.2f);
            waterL2 = boot.water != null && Mathf.Abs(boot.water.Y - 1.15f) <= 0.06f;
            if (status == "passed" && !(schoolRead && stamp2 && waterL2)) { status = "s4_failed"; error = $"read={schoolRead} stamp={stamp2} water={waterL2}"; }
        }

        if (status == "passed") yield return SnapDog("story_dog.png");

        // WP-12：小黑从 S4 开始跟随、贴地、收尾时在身后（上塔前结算，塔顶小黑留在塔下）
        float dogEnd = 999f;
        bool dogBehind = false;
        bool dogOk = false;
        bool dogFollowed = false;
        float dogMaxCheck = 0f;
        if (dog != null && dog.Bound && dog.Following && p != null)
        {
            Vector3 dp = dog.transform.position, pp = p.transform.position;
            dogEnd = new Vector2(dp.x - pp.x, dp.z - pp.z).magnitude;
            var fwd = new Vector3(Mathf.Sin(p.yaw), 0f, Mathf.Cos(p.yaw));
            dogBehind = Vector3.Dot(new Vector3(dp.x - pp.x, 0f, dp.z - pp.z), fwd) < 0.05f;
            dogMaxCheck = dogMax;
            dogOk = dog.Visible && dogMax <= 4.0f && dogMinClear >= -0.05f && dogEnd <= 3.0f && dogBehind;
            dogFollowed = dog.Visible;
        }
        if (status == "passed" && !dogOk)
        {
            status = "dog_failed";
            error = $"follow={dog != null && dog.Following} max={dogMax:F2} end={dogEnd:F2} clear={dogMinClear:F2} behind={dogBehind}";
        }

        float schoolSecs = Time.realtimeSinceStartup - schoolAt;

        // WP-14：踩木箱到船棚 → 油壶 → 塔门 → 加油读信 → 点灯 → 天暗/光束 → 手电回应
        float lightAt = Time.realtimeSinceStartup;
        bool l0Auto = false, oilOk = false, climbOk = false, oilLetter = false;
        bool beamOk = false, dimOk = false, beaconOk = false, replyOk = false, stamp3 = false, waterL3 = false;
        int replyCount = 0;

        if (status == "passed")
        {
            // 学校后门原路回 20，再沿路线穿过低洼段的木箱到船棚
            yield return WalkTo(route[i20].x, route[i20].z, "20_back");
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            else yield return WalkRoute(route, i20, i25);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            l0Auto = boot.state.step == "L1_shed";
            if (status == "passed" && !l0Auto) { status = "l0_failed"; error = "step=" + boot.state.step + " [" + boot.director.DebugAuto + "]"; }
            if (status == "passed") yield return SnapFrom("story_lighthouse_crates.png",
                new Vector3(15.5f, 5.8f, -8.9f), new Vector3(15.5f, 1.3f, -6.5f));
        }

        if (status == "passed")
        {
            if (!boot.service.TryInteract("oil")) { status = "oil_failed"; error = "oil not in range"; }
            else
            {
                yield return new WaitForSeconds(0.25f);
                oilOk = boot.state.step == "L2_door" && boot.state.carrying == "oil";
                if (!oilOk) { status = "oil_failed"; error = "step=" + boot.state.step + " carry=" + boot.state.carrying; }
            }
        }

        // 原路回 22，再走台阶到 30 号塔门
        if (status == "passed")
        {
            yield return WalkRouteBack(route, i25 + 1, i22);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            else yield return WalkRoute(route, i22b + 1, i30);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            if (status == "passed") yield return SnapFrom("story_lighthouse_door.png",
                new Vector3(19.6f, 8.0f, -1.4f), new Vector3(21.5f, 5.9f, 1.9f));
        }

        if (status == "passed")
        {
            if (!boot.service.TryInteract("lighthouse_door")) { status = "door_failed"; error = "door not in range"; }
            else
            {
                float t0 = Time.realtimeSinceStartup;
                while (boot.state.step != "L3_oil" && Time.realtimeSinceStartup - t0 < 10f) yield return null;
                // 等淡入结束，否则信纸会被黑色过场盖住
                while (boot.director.Climbing && Time.realtimeSinceStartup - t0 < 12f) yield return null;
                yield return new WaitForSecondsRealtime(0.2f);
                climbOk = boot.state.step == "L3_oil" && boot.player.transform.position.y > 16f;
                if (!climbOk) { status = "climb_failed"; error = $"step={boot.state.step} y={boot.player.transform.position.y:F2}"; }
            }
        }

        if (status == "passed")
        {
            if (!boot.service.TryInteract("lamp_head")) { status = "oilletter_failed"; error = "lamp not in range"; }
            else
            {
                for (int i = 0; i < 90 && !boot.ui.LetterOpen; i++) yield return null;
                oilLetter = boot.state.flags.Contains("lamp_oiled") && boot.ui.LetterOpen;
                if (!oilLetter) { status = "oilletter_failed"; error = "letter not open"; }
                else
                {
                    yield return Snap("story_lighthouse_letter.png");
                    boot.ui.CloseLetter();
                    yield return new WaitForSeconds(0.3f);
                    if (status == "passed" && boot.state.step != "L4_light") { status = "l4_missing"; error = "step=" + boot.state.step; }
                }
            }
        }

        if (status == "passed")
        {
            if (!boot.service.TryInteract("lamp_head")) { status = "light_failed"; error = "lamp not in range"; }
            else
            {
                yield return new WaitForSecondsRealtime(10f);
                beamOk = boot.state.flags.Contains("beam_lit");
                var dimmer = FindObjectOfType<SkyDimmer>();
                var beacon = FindObjectOfType<BeaconLight>();
                dimOk = dimmer != null && dimmer.Lit && dimmer.Progress > 0.3f;
                beaconOk = beacon != null && beacon.On;
                if (!(beamOk && dimOk && beaconOk)) { status = "light_failed"; error = $"beam={beamOk} dim={dimOk} beacon={beaconOk}"; }
                else yield return SnapFrom("story_lighthouse_beam.png",
                    new Vector3(10.0f, 13.5f, 12.0f), new Vector3(21.58f, 17.6f, 2.7f));
            }
        }

        if (status == "passed")
        {
            var reply = FindObjectOfType<FlashlightReply>();
            float t0 = Time.realtimeSinceStartup;
            while (boot.state.step != "L5_reply" && Time.realtimeSinceStartup - t0 < 34f) yield return null;
            replyOk = boot.state.step == "L5_reply" && reply != null && reply.Done && reply.LitCount == 40;
            replyCount = reply != null ? reply.LitCount : 0;
            stamp3 = boot.state.stamps.Contains("lighthouse");
            if (!(replyOk && stamp3)) { status = "reply_failed"; error = $"step={boot.state.step} lit={replyCount} stamp={stamp3}"; }
            else
            {
                yield return new WaitForSecondsRealtime(3.2f);
                waterL3 = boot.water != null && Mathf.Abs(boot.water.Y - 1.60f) <= 0.06f;
                if (!waterL3) { status = "l3_failed"; error = "water=" + (boot.water != null ? boot.water.Y.ToString("F2") : "-"); }
                else yield return SnapFrom("story_lighthouse_reply.png",
                    new Vector3(21.6f, 19.4f, 5.6f), new Vector3(-12f, 11.6f, 25f));
            }
        }

        float lightSecs = Time.realtimeSinceStartup - lightAt;

        bool saveOk = false;
        if (status == "passed")
        {
            var loaded = new SaveStore().Load();
            saveOk = loaded != null && loaded.step == "L5_reply" && loaded.flags.Contains("school_read")
                && loaded.flags.Contains("lighthouse_done") && loaded.flags.Contains("beam_lit")
                && loaded.flags.Contains("lamp_oiled") && loaded.flags.Contains("ball_in_bowl")
                && loaded.flags.Contains("letters_taken") && loaded.stamps.Contains("bakery")
                && loaded.stamps.Contains("school") && loaded.stamps.Contains("lighthouse")
                && loaded.seenMemories.Contains("basket") && Mathf.Abs(loaded.waterLevel - 1.60f) <= 0.06f;
            if (!saveOk) { status = "save_failed"; error = "reload mismatch"; }
            // 恢复现场，避免审计把玩家存档带偏
            new SaveStore().Clear();
        }

        // WP-15：下塔回邮局 → 老周的信 → 上山回望 → 新城灯光与字幕
        float endingAt = Time.realtimeSinceStartup;
        bool downOk = false, e0Ok = false, e1Open = false, exprSurprised = false, exprHappy = false;
        bool lookBack = false, waterL4 = false, ended = false, cityShown = false, subtitleOk = false;
        string subtitleText = "";
        float dogFinal = 999f;

        if (status == "passed")
        {
            if (!boot.service.TryInteract("lighthouse_down")) { status = "down_failed"; error = "descent not in range"; }
            else
            {
                float t0 = Time.realtimeSinceStartup;
                while (boot.director.Cutscene && Time.realtimeSinceStartup - t0 < 8f) yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
                var pp = boot.player.transform.position;
                downOk = pp.y < 6f;
                if (!downOk) { status = "down_failed"; error = $"y={pp.y:F2} step={boot.state.step}"; }
                else
                {
                    yield return WalkRoute(route, i30b + 1, i07);
                    if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
                }
            }
        }

        if (status == "passed")
        {
            float t0 = Time.realtimeSinceStartup;
            while (boot.state.step != "E1_read" && Time.realtimeSinceStartup - t0 < 5f) yield return null;
            e0Ok = boot.state.step == "E1_read" && boot.state.flags.Contains("dawn_shown");
            if (!e0Ok) { status = "e0_failed"; error = "step=" + boot.state.step + " [" + boot.director.DebugAuto + "]"; }
            else
            {
                yield return new WaitForSecondsRealtime(3.6f); // 让天亮一点再拍课桌
                yield return SnapFrom("story_ending_desk.png",
                    new Vector3(-17.0f, 5.4f, -8.6f), new Vector3(-17.0f, 3.4f, -5.6f));
            }
        }

        if (status == "passed")
        {
            if (!boot.service.TryInteract("ending_desk")) { status = "e1_failed"; error = "desk not in range"; }
            else
            {
                for (int i = 0; i < 90 && !boot.ui.LetterOpen; i++) yield return null;
                e1Open = boot.ui.LetterOpen;
                exprSurprised = p.pose != null && p.pose.expr == ExprId.surprised;
                if (!(e1Open && exprSurprised)) { status = "e1_failed"; error = $"open={e1Open} expr={(p.pose != null ? p.pose.expr.ToString() : "-")}"; }
                else
                {
                    yield return Snap("story_ending_letter.png");
                    yield return new WaitForSecondsRealtime(4.6f);
                    exprHappy = p.pose != null && p.pose.expr == ExprId.happy;
                    boot.ui.CloseLetter();
                    yield return new WaitForSeconds(0.3f);
                    if (!exprHappy) { status = "e1_mood_failed"; error = p.pose != null ? p.pose.expr.ToString() : "-"; }
                    else if (boot.state.step != "E2_climb") { status = "e2_missing"; error = "step=" + boot.state.step; }
                }
            }
        }

        if (status == "passed")
        {
            yield return WalkRoute(route, i07, i06);
            if (walkFail != "") { status = "blocked_walk_" + walkFail; error = walkFail; }
            float t0 = Time.realtimeSinceStartup;
            while (!boot.director.Cutscene && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            lookBack = boot.director.Cutscene;
            yield return new WaitForSecondsRealtime(5f);
            waterL4 = boot.water != null && Mathf.Abs(boot.water.Y - 3.05f) <= 0.08f;
            if (lookBack) yield return Snap("story_ending_lookback.png");
            if (status == "passed" && !(lookBack && waterL4)) { status = "e2_failed"; error = $"lookback={lookBack} water={waterL4} dbg={boot.director.EndingDebug} step={boot.state.step} t={(Time.realtimeSinceStartup - endingAt):F1}"; }
        }

        if (status == "passed")
        {
            float t0 = Time.realtimeSinceStartup;
            while (!boot.director.Ended && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            ended = boot.director.Ended;
            subtitleText = boot.ui.SubtitleText;
            subtitleOk = subtitleText == "所有的信，都送到了。";
            var city = FindObjectOfType<CityGlow>();
            cityShown = city != null && city.Shown;
            yield return new WaitForSecondsRealtime(1.2f);
            var dp = dog != null ? dog.transform.position : Vector3.zero;
            var pp = p != null ? p.transform.position : Vector3.zero;
            dogFinal = new Vector2(dp.x - pp.x, dp.z - pp.z).magnitude;
            if (status == "passed")
            {
                if (!(ended && subtitleOk && cityShown)) { status = "e3_failed"; error = $"ended={ended} subtitle={subtitleText} city={cityShown} dbg={boot.director.EndingDebug}"; }
                else if (dog == null || !dog.Following || !dog.Visible || dogFinal > 5.0f) { status = "e3_dog_failed"; error = $"dog={dogFinal:F2}"; }
                else yield return Snap("story_ending_city.png");
            }
        }

        float endingSecs = Time.realtimeSinceStartup - endingAt;
        // 结局会再写一次存档（水位 L4），别留给后面的路线审计
        new SaveStore().Clear();

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
                  + ", \"B4_light\": " + (litOk ? "true" : "false")
                  + ", \"S0_arrive\": " + (s0Auto ? "true" : "false")
                  + ", \"S1_slipper\": " + (slipperOk ? "true" : "false")
                  + ", \"S2_ball\": " + (ballOk ? "true" : "false")
                  + ", \"S3_bowl\": " + (bowlOk ? "true" : "false")
                  + ", \"S4_read\": " + (schoolRead ? "true" : "false")
                  + ", \"L0_cross\": " + (l0Auto ? "true" : "false")
                  + ", \"L1_shed\": " + (oilOk ? "true" : "false")
                  + ", \"L2_door\": " + (climbOk ? "true" : "false")
                  + ", \"L3_oil\": " + (oilLetter ? "true" : "false")
                  + ", \"L4_light\": " + (beamOk ? "true" : "false")
                  + ", \"L5_reply\": " + (replyOk ? "true" : "false")
                  + ", \"E0_back\": " + (e0Ok ? "true" : "false")
                  + ", \"E1_read\": " + (e1Open && exprHappy ? "true" : "false")
                  + ", \"E2_climb\": " + (lookBack && waterL4 ? "true" : "false")
                  + ", \"E3_end\": " + (ended ? "true" : "false") + " },");
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
                  + ", \"fire_visible\": " + (fireVisibleOven ? "true" : "false")
                  + ", \"smoke_visible\": " + (smokeVisibleOven ? "true" : "false")
                  + ", \"shots\": [\"story_bakery_letter.png\", \"story_oven.png\"] },");
        sb.AppendLine("  \"school\": { \"seconds\": " + schoolSecs.ToString("F1")
                  + ", \"auto_letter\": " + (s0Auto ? "true" : "false")
                  + ", \"slipper\": " + (slipperOk ? "true" : "false")
                  + ", \"ball\": " + (ballOk ? "true" : "false")
                  + ", \"bowl\": " + (bowlOk ? "true" : "false")
                  + ", \"ball_in_bowl\": " + (ballInBowl ? "true" : "false")
                  + ", \"peek\": " + (dogPeek ? "true" : "false")
                  + ", \"read\": " + (schoolRead ? "true" : "false")
                  + ", \"line_by_line\": " + (linesShown == linesTotal && linesTotal > 0 ? "true" : "false")
                  + ", \"lines_shown\": " + linesShown + "/" + linesTotal
                  + ", \"expr_trace\": \"" + exprTrace + "\""
                  + ", \"stamp\": " + (stamp2 ? "true" : "false")
                  + ", \"water_l2\": " + (waterL2 ? "true" : "false")
                  + ", \"shots\": [\"story_school_letter.png\", \"story_pawprints.png\", \"story_playground.png\", \"story_school_read.png\"] },");
        sb.AppendLine("  \"lighthouse\": { \"seconds\": " + lightSecs.ToString("F1")
                  + ", \"crates\": " + (l0Auto ? "true" : "false")
                  + ", \"oil\": " + (oilOk ? "true" : "false")
                  + ", \"climb\": " + (climbOk ? "true" : "false")
                  + ", \"read\": " + (oilLetter ? "true" : "false")
                  + ", \"beam\": " + (beamOk ? "true" : "false")
                  + ", \"dim\": " + (dimOk ? "true" : "false")
                  + ", \"beacon\": " + (beaconOk ? "true" : "false")
                  + ", \"reply\": " + (replyOk ? "true" : "false")
                  + ", \"reply_lights\": " + replyCount
                  + ", \"stamp\": " + (stamp3 ? "true" : "false")
                  + ", \"water_l3\": " + (waterL3 ? "true" : "false")
                  + ", \"shots\": [\"story_lighthouse_crates.png\", \"story_lighthouse_door.png\", \"story_lighthouse_letter.png\", \"story_lighthouse_beam.png\", \"story_lighthouse_reply.png\"] },");
        sb.AppendLine("  \"ending\": { \"seconds\": " + endingSecs.ToString("F1")
                  + ", \"descent\": " + (downOk ? "true" : "false")
                  + ", \"back_at_post\": " + (e0Ok ? "true" : "false")
                  + ", \"letter\": " + (e1Open ? "true" : "false")
                  + ", \"mood_surprised\": " + (exprSurprised ? "true" : "false")
                  + ", \"mood_happy\": " + (exprHappy ? "true" : "false")
                  + ", \"look_back\": " + (lookBack ? "true" : "false")
                  + ", \"water_l4\": " + (waterL4 ? "true" : "false")
                  + ", \"ended\": " + (ended ? "true" : "false")
                  + ", \"city\": " + (cityShown ? "true" : "false")
                  + ", \"subtitle\": \"" + subtitleText + "\""
                  + ", \"dog_end\": " + dogFinal.ToString("F2")
                  + ", \"shots\": [\"story_ending_desk.png\", \"story_ending_letter.png\", \"story_ending_lookback.png\", \"story_ending_city.png\"] },");
        sb.AppendLine("  \"save_reload\": " + (saveOk ? "true" : "false") + ",");
        var visual = dog != null ? dog.GetComponent<DogVisual>() : null;
        sb.AppendLine("  \"dog\": { \"followed\": " + (dogFollowed ? "true" : "false")
                  + ", \"visible\": " + (dogFollowed ? "true" : "false")
                  + ", \"left_at_tower\": " + (dog != null && !dog.Visible ? "true" : "false")
                  + ", \"follow_max\": " + dogMaxCheck.ToString("F2")
                  + ", \"follow_max_all\": " + dogMax.ToString("F2")
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
        sb.AppendLine("  \"total_seconds\": " + (Time.realtimeSinceStartup - bootAt).ToString("F1") + ",");
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

    SchoolItem SchoolFind(string id)
    {
        foreach (var it in SchoolData.Load().items) if (it.id == id) return it;
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

    IEnumerator WalkRouteBack(Wp[] route, int from, int to)
    {
        for (int i = from - 1; i >= to; i--)
        {
            yield return WalkTo(route[i].x, route[i].z, route[i].id + "r");
            if (walkFail != "" || boot.ui.LetterOpen) yield break;
        }
    }

    IEnumerator WalkTo(float x, float z, string label)
    {
        float t0 = Time.realtimeSinceStartup;
        while (true)
        {
            // 到点自动读信或进过场会锁人，停在半径外也算到达
            if (boot.ui.LetterOpen || boot.director.Cutscene) yield break;
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
        if (dog != null && dog.Bound && dog.Following && boot != null && boot.player != null && boot.world != null)
        {
            var dp = dog.transform.position;
            var pp = boot.player.transform.position;
            float gap = new Vector2(dp.x - pp.x, dp.z - pp.z).magnitude;
            if (Time.timeScale > 0f) dogMax = Mathf.Max(dogMax, gap);
            dogMinClear = Mathf.Min(dogMinClear, dp.y - boot.world.GroundAt(dp.x, dp.z, dp.y));
        }
    }
}
