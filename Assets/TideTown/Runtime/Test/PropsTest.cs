// 2026-11-16 -feeldemo props：持物 / 炉火 / 门铃实机验收（真人路径：真实接触拾取，非审计模式）。
// 抱柴（原地柴堆消失、柴捆居中贴胸不穿壳）→ 塞进炉膛（炉膛里出现柴捆）→ 湿柴 / 白烟 / 点火（火在炉膛里、从炉门看得见）→
// 提油壶（提手对准掌心、不进蛋壳、不擦地、走跑时摆动）→ 塔顶倒油（倾倒后收起）→ 门铃（伸手碰响、铃身摆）。
// 图存 shots/prop_fix_*.png，结果 feel-demo-props.json。
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class PropsTest
{
    public static IEnumerator Run(MonoBehaviour host, EggboyPlayer player, IslandWorld world)
    {
        var boot = Object.FindObjectOfType<GameBootstrap>();
        var director = boot != null ? boot.director : null;
        var pk = PhysicalPickupController.Instance;
        var fx = Object.FindObjectOfType<BakeryEffects>();
        var cam = Camera.main;
        var chase = cam != null ? cam.GetComponent<ChaseCamera>() : null;
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        var sb = new StringBuilder(); int pass = 0, total = 0;
        void Check(string name, bool ok, string note) { total++; if (ok) pass++; sb.Append(name).Append(ok ? "=ok " : "=FAIL ").Append(note).Append(" | "); Debug.Log("PROPSTEST " + name + "=" + ok + " " + note); }
        void Done()
        {
            string json = "{\"status\":\"" + (pass == total && total > 0 ? "passed" : "failed") + "\",\"mode\":\"props\",\"note\":\"" + pass + "/" + total + " " + sb.ToString().Replace("\"", "'") + "\"}";
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "feel-demo-props.json"), json);
            Debug.Log("FEELDEMO " + json);
            Time.captureFramerate = 0;
            Application.Quit(pass == total ? 0 : 1);
        }
        if (boot == null || director == null || pk == null || fx == null || cam == null || !pk.Active("pickup_wood"))
        {
            Check("setup", false, $"boot={boot != null} director={director != null} pk={pk != null} fx={fx != null} active={(pk != null && pk.Active("pickup_wood"))}");
            Done(); yield break;
        }
        director.StopAllCoroutines(); director.enabled = false;
        var st = boot.state;
        Time.captureFramerate = 30;
        string F(float v) => v.ToString("F3", CultureInfo.InvariantCulture);

        void Place(Vector3 at, Vector3 face)
        {
            at.y = world.GroundAt(at.x, at.z, at.y + 1f);
            player.ResetJumpInput();
            player.transform.position = at + Vector3.up * 0.01f;
            player.vel = Vector3.zero; player.grounded = true; player.jump.phase = JumpPhase.none; player.locked = false;
            player.FaceTowards(face);
            player.transform.rotation = Quaternion.Euler(0f, player.yaw * Mathf.Rad2Deg, 0f);
            if (chase != null) chase.SnapBehind(player.transform.position, player.yaw);
        }
        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) { if (!pk.Busy) player.Move(1f / 30f, 0f, 0f, false, false); yield return null; } }
        IEnumerator WaitIdle(float max, System.Func<bool> onFrame = null)
        {
            float t0 = Time.time; yield return null;
            while ((pk.Busy || player.ActionLocked) && Time.time - t0 < max) { onFrame?.Invoke(); yield return null; }
        }
        IEnumerator Shot(string name, Vector3 focus, float yawDeg, float pitch = 22f, float dist = 1.6f, float fov = 45f)
        {
            bool on = chase != null && chase.enabled; if (chase != null) chase.enabled = false;
            var q = Quaternion.Euler(pitch, yawDeg, 0f);
            cam.transform.SetPositionAndRotation(focus - q * Vector3.forward * dist, q);
            cam.fieldOfView = fov;
            yield return ShotTool.Capture(cam, 1920, 1080, Path.Combine(dir, "prop_fix_" + name + ".png"));
            if (chase != null) chase.enabled = on;
        }
        Vector3 Item(string id)
        {
            foreach (var it in BakeryData.Load()) if (it.id == id) return new Vector3(it.x, it.y, it.z);
            return Vector3.zero;
        }
        float PlayerYawDeg() => player.transform.eulerAngles.y;

        // ---------- 1. 抱柴 ----------
        st.flags.Add("letters_taken"); st.flags.Add("matches_taken"); st.flags.Add("matches_packed");
        st.flags.Remove("wood_loaded"); st.flags.Remove("wood_wet"); st.flags.Remove("oven_lit");
        st.carrying = ""; st.step = "B2_wood";
        director.DebugSyncProps();
        yield return Frames(3);
        var wood = Item("wood");
        Check("wood_pile_visible_before", director.WoodPileVisible, "");
        Place(wood + new Vector3(0.2f, 0f, -1.3f), wood);
        yield return Frames(5);
        director.TakeWoodAt(wood);
        yield return WaitIdle(8f);
        yield return Frames(10);
        var cv = director.CarriedVisual;
        Check("wood_taken", st.carrying == "wood" && cv != null, "carry=" + st.carrying);
        Check("wood_pile_hidden", !director.WoodPileVisible, "");
        if (cv != null)
        {
            var lb = HandCarry.LocalBounds(cv, player.transform);
            float front = pk.Grip.ShellFrontAt(player.transform, lb.center.y, lb.size.x * 0.4f, lb.size.y * 0.5f);
            Check("wood_centered", Mathf.Abs(lb.center.x) < 0.03f, "x=" + F(lb.center.x));
            Check("wood_clear_of_shell", lb.min.z >= front - 0.005f, "back=" + F(lb.min.z) + " shellFront=" + F(front));
            Check("wood_follows_body", cv.transform.parent == player.CarryAnchor, "parent=" + (cv.transform.parent != null ? cv.transform.parent.name : "null"));
        }
        yield return Shot("05_wood_carry_front", player.transform.position + Vector3.up * 0.45f, PlayerYawDeg() + 180f, 14f, 1.5f);
        // 抱着走两秒
        var stove = Item("stove");
        for (int i = 0; i < 40; i++)
        {
            var d = new Vector3(stove.x - player.transform.position.x, 0f, stove.z - 1.9f - player.transform.position.z);
            if (d.magnitude > 0.2f) d.Normalize(); else d = Vector3.zero;
            player.Move(1f / 30f, d.x, d.z, false, false);
            if (i == 24) yield return Shot("06_wood_carry_walk", player.transform.position + Vector3.up * 0.45f, PlayerYawDeg() - 70f, 12f, 1.6f);
            yield return null;
        }

        // ---------- 2. 塞进炉膛 ----------
        Place(new Vector3(fx.LoadPoint.x, stove.y, fx.LoadPoint.z - 1.2f), fx.LoadPoint);
        yield return Frames(5);
        director.LoadWoodAt(stove);
        bool shotLoad = false;
        float t1 = Time.time;
        float loadStartedAt = float.NaN;
        yield return null;
        while ((pk.Busy || player.ActionLocked) && Time.time - t1 < 8f)
        {
            if (pk.LastEvent.StartsWith("act load_wood") && float.IsNaN(loadStartedAt)) loadStartedAt = Time.time;
            if (!shotLoad && Time.time - loadStartedAt >= .33f && director.CarriedVisual != null)
            {
                shotLoad = true;
                var hero = player.transform.position + Vector3.up * .48f;
                var focus = Vector3.Lerp(hero, fx.LoadPoint + Vector3.up * .15f, .50f);
                yield return Shot("07_wood_into_oven", focus, PlayerYawDeg() - 42f, 10f, 2.15f, 50f);
            }
            yield return null;
        }
        yield return Frames(6);
        Check("wood_loaded", st.flags.Contains("wood_loaded") && st.carrying != "wood" && director.CarriedVisual == null, "step=" + st.step + " event=" + pk.LastEvent);
        var loaded = fx.transform.Find("OvenLoadedWood");
        bool woodRendererOn = loaded != null && System.Array.Exists(loaded.GetComponentsInChildren<Renderer>(), r => r.enabled);
        float woodInset = loaded != null ? HandCarry.LocalBounds(loaded.gameObject, fx.transform).min.z - fx.mouthLocal.z : 0f;
        Check("oven_has_wood_inside", fx.HasLoadedWood && !fx.LoadedWoodWet && woodRendererOn && woodInset > 0.01f,
            "renderer=" + woodRendererOn + " inset=" + F(woodInset));
        Check("wood_pile_still_hidden", !director.WoodPileVisible, "");
        var mouth = fx.LoadPoint;
        yield return Shot("08_oven_loaded_wood", mouth, 0f, 6f, 0.9f, 50f);
        fx.SetLoadedWood(true, true);
        yield return Frames(2);
        Check("oven_wet_wood_swaps", fx.HasLoadedWood && fx.LoadedWoodWet, "");
        fx.WetPuff();
        yield return Frames(14);
        yield return Shot("09_oven_wet_puff", mouth, 0f, 8f, 1.2f, 50f);
        fx.SetLoadedWood(true, false);

        // ---------- 3. 炉膛里点火 ----------
        fx.Light();
        yield return Frames(45);
        var local = fx.transform.InverseTransformPoint(fx.FireMouth);
        Check("fire_inside_chamber", local.z > fx.mouthLocal.z + 0.05f && local.y > fx.chamberFloorLocal.y - 0.01f, "fireLocal=" + local.ToString("F3") + " mouthZ=" + F(fx.mouthLocal.z));
        var q = Quaternion.Euler(8f, 0f, 0f);
        cam.transform.SetPositionAndRotation(mouth - q * Vector3.forward * 1.4f, q); cam.fieldOfView = 50f;
        yield return null;
        Check("fire_visible_from_front", fx.FireVisibleFrom(cam) && fx.FireCount > 0, "count=" + fx.FireCount);
        yield return Shot("10_oven_fire_inside", mouth, 0f, 8f, 1.1f, 50f);
        yield return Shot("11_oven_fire_glow_street", mouth + Vector3.down * 0.3f, 0f, 20f, 3.2f, 50f);
        var chimneyW = fx.transform.TransformPoint(fx.chimneyLocalPosition);
        yield return Shot("12_chimney_smoke", chimneyW, 20f, 10f, 4.5f, 50f);

        // ---------- 4. 提油壶 ----------
        LighthouseItem oilItem = null; foreach (var it in LighthouseData.Load().items) if (it.id == "oil") oilItem = it;
        var oil = new Vector3(oilItem.x, oilItem.y, oilItem.z);
        st.flags.Remove("oil_taken"); st.carrying = ""; st.step = "L1_shed";
        director.DebugSyncProps();
        yield return Frames(3);
        Check("oil_can_visible_before", director.OilCanVisible, "");
        Place(oil + new Vector3(-1.1f, 0f, 0.2f), oil);
        yield return Frames(5);
        director.TakeOilAt(oil);
        yield return WaitIdle(8f);
        yield return Frames(15);
        var hc = director.CarriedVisual != null ? director.CarriedVisual.GetComponent<HandCarry>() : null;
        Check("oil_taken", st.carrying == "oil" && hc != null, "carry=" + st.carrying);
        Check("oil_can_hidden", !director.OilCanVisible, "");
        if (hc != null)
        {
            Check("oil_handle_in_palm", hc.PalmError < 0.03f, "err=" + F(hc.PalmError));
            yield return Shot("13_oil_carry_side", player.transform.position + Vector3.up * 0.32f, PlayerYawDeg() - 90f, 10f, 1.4f);
            // 走一圈 / 跑一圈：提手不离手、壶不进壳、不擦地，壶身跟着晃
            float maxErr = 0f, minShell = 1f, minGround = 1f, maxTilt = 0f;
            for (int i = 0; i < 90; i++)
            {
                float a = i / 90f * Mathf.PI * 2f;
                player.Move(1f / 30f, Mathf.Cos(a), Mathf.Sin(a), i > 45, false);
                yield return null;
                maxErr = Mathf.Max(maxErr, hc.PalmError); minShell = Mathf.Min(minShell, hc.ShellGap); minGround = Mathf.Min(minGround, hc.GroundGap);
                maxTilt = Mathf.Max(maxTilt, Vector3.Angle(hc.transform.up, Vector3.up));
                if (i == 30) yield return Shot("14_oil_walk", player.transform.position + Vector3.up * 0.32f, PlayerYawDeg() - 90f, 30f, 1.5f);
                if (i == 52) yield return Shot("15_oil_run", player.transform.position + Vector3.up * 0.25f, PlayerYawDeg() - 90f, 62f, 1.8f, 50f);
            }
            // 跑起来为了不穿壳 / 不擦地会把壶往外、往上让一点，提手离掌心允许到 6 cm
            Check("oil_walk_handle_stays", maxErr < 0.06f, "maxErr=" + F(maxErr));
            Check("oil_walk_no_shell_clip", minShell >= -0.01f, "minShellGap=" + F(minShell));
            Check("oil_walk_off_ground", minGround >= 0.015f, "minGroundGap=" + F(minGround));   // HandCarry 把壶底托在离地 ≥ 2 cm
            Check("oil_swings", maxTilt > 2f, "maxTilt=" + F(maxTilt));
        }

        // ---------- 5. 塔顶倒油 ----------
        var lhd = LighthouseData.Load();
        var lamp = LighthouseData.Point(lhd.lamp); var deck = LighthouseData.Point(lhd.deck);
        st.step = "L3_oil"; st.flags.Remove("lamp_oiled");
        Place(deck, lamp);
        yield return Frames(10);
        director.OilLampAt(lamp);
        bool pourShot = false; float maxPour = 0f;
        float t2 = Time.time;
        while (Time.time - t2 < 4f)
        {
            if (hc != null) maxPour = Mathf.Max(maxPour, Vector3.Angle(hc.transform.up, Vector3.up));
            if (!pourShot && pk.LastEvent.StartsWith("contact") && hc != null && Vector3.Angle(hc.transform.up, Vector3.up) > 40f)
            {
                pourShot = true;
                var outDir = player.transform.position - lamp; outDir.y = 0f; outDir.Normalize();
                var side = Vector3.Cross(Vector3.up, outDir);
                // 用灯笼间内的矮机位，避开上方灯罩。
                var camPos = player.transform.position + outDir * .17f + side * .83f + Vector3.up * .70f;
                var look = Vector3.Lerp(player.transform.position + Vector3.up * .42f, hc.transform.position, .48f);
                bool on = chase != null && chase.enabled; if (chase != null) chase.enabled = false;
                cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(look - camPos)); cam.fieldOfView = 62f;
                yield return ShotTool.Capture(cam, 1920, 1080, Path.Combine(dir, "prop_fix_16_oil_pour.png"));
                if (chase != null) chase.enabled = on;
            }
            if (director.CarriedVisual == null && Time.time - t2 > 1f) break;
            yield return null;
        }
        Check("oil_pours_then_removed", maxPour > 45f && director.CarriedVisual == null && st.flags.Contains("lamp_oiled"), "maxPour=" + F(maxPour));
        if (boot.ui.LetterOpen) boot.ui.CloseLetter();
        yield return Frames(5);

        // ---------- 6. 门铃 ----------
        var bell = Item("bell");
        var swing = GameObject.Find("Bakery_bell")?.GetComponent<BellSwing>();
        Place(bell + new Vector3(0f, 0f, -1.3f), bell);
        yield return Frames(6);
        yield return Shot("01_bell_on_door", swing != null ? swing.TouchPoint + Vector3.up * 0.1f : bell, 0f, 4f, 1.6f, 45f);
        int rings0 = swing != null ? swing.RingCount : 0;
        foreach (var it in BakeryData.Load()) if (it.id == "bell") director.RingBellAt(bell, it.label, it.text);
        bool bellShot = false; float t3 = Time.time; yield return null;
        while ((pk.Busy || player.ActionLocked) && Time.time - t3 < 8f)
        {
            if (!bellShot && pk.LastEvent.StartsWith("contact")) { bellShot = true; yield return Shot("02_bell_ring", swing != null ? swing.TouchPoint : bell, PlayerYawDeg() - 65f, 12f, 1.5f); }
            yield return null;
        }
        yield return Frames(4);
        Check("bell_rings_on_touch", swing != null && swing.RingCount == rings0 + 1 && pk.LastEvent.StartsWith("contact"), "event=" + pk.LastEvent + " rings=" + (swing != null ? swing.RingCount : -1));
        if (ObjectInspectModal.IsOpen) ObjectInspectModal.DebugClose();
        Done();
    }
}
