// 2026-11-14 体感演示（-feeldemo bush | grass | beach）：没人操作时，让真的 EGGBOY 自己走一遍，
// 固定机位每 3 帧存一张图（shots/feel_<模式>_NNN.png，30 帧/秒的步长），用来看花丛、草地、水面的反应。
//   bush  ：挑一丛两边都走得通的花丛，走过去 → 停 → 跑回来（中途跳一下）→ 停
//   grass ：挑一块草坪，同样走一遍，看踩倒的痕迹
//   beach ：水位设到 L1（沙滩没过脚 15 厘米），同样走一遍，最后原地跳一下，看涟漪和水花
// 结果写 feel-demo-<模式>.json：涟漪 / 水花 / 花瓣数、最大水深、痕迹图在主角脚下读到的值（不为零才说明盖印的位置是对的）。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FeelDemo : MonoBehaviour
{
    string mode;
    EggboyPlayer player;
    IslandWorld world;
    Camera cam;
    RenderTexture rt;
    Texture2D grab;
    string dir;
    int frame, shot;
    float maxDepth, maxTrail, maxSpring;
    int bushFrames, stuckPhases;
    readonly List<string> phases = new List<string>();
    readonly List<EggboyCollider> near = new List<EggboyCollider>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-feeldemo");
        if (i < 0 || i + 1 >= args.Length) return;
        var d = new GameObject("FeelDemo").AddComponent<FeelDemo>();
        d.mode = args[i + 1];
        DontDestroyOnLoad(d.gameObject);
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return new WaitForSecondsRealtime(3f);

        var heroGo = GameObject.Find("EGGBOY");
        player = heroGo != null ? heroGo.GetComponent<EggboyPlayer>() : null;
        world = FindObjectOfType<IslandWorld>();
        if (player == null || world == null) { Finish("setup_failed", "缺 EGGBOY / IslandWorld"); yield break; }
        if (mode.StartsWith("playinput:"))
        {
            yield return PlayInput(mode.Substring(10));
            yield break;
        }
        var game = FindObjectOfType<PrototypeGame>();
        if (game != null) game.enabled = false;
        foreach (var c in FindObjectsOfType<Canvas>()) c.enabled = false;
        var safe = FindObjectOfType<SafePoints>();
        if (safe != null) safe.Enabled = false;

        if ((mode == "beach" || mode == "beachentry") && WaterLevel.Main != null && !WaterLevel.Main.CliOverride) WaterLevel.Main.SetInstant(WaterLevel.LevelYs[1]);
        yield return null;
        if (mode == "beachentry") { yield return BeachEntry(); yield break; }
        if (mode == "ferry") { yield return Ferry(); yield break; }
        if (mode == "jump") { yield return JumpWindows(); yield break; }
        if (mode == "camera") { yield return CameraRoute(); yield break; }
        if (mode == "map-alignment") { yield return MapAlignment(); yield break; }
        if (mode.StartsWith("spots:")) { yield return Spots(mode.Substring(6)); yield break; }
        if (mode == "dogplate") { yield return DogPlateTrace(); yield break; }
        if (mode == "pickup") { yield return PickupTest(); yield break; }
        if (mode == "wishwheel") { yield return WishWheelTest(); yield break; }
        if (mode == "props") { yield return PropsTest.Run(this, player, world); yield break; }
        if (mode == "bottle-bank") { yield return BottleBank(); yield break; }
        if (mode.StartsWith("probe:"))
        {
            // -feeldemo probe:x,z[;x,z…]（配 -water L2 等）：打印这些点的地面高度和能不能走，查"这里为什么能走 / 不能走"
            var sb = new System.Text.StringBuilder();
            foreach (var pair in mode.Substring(6).Split(';'))
            {
                var xz = pair.Split(',');
                if (xz.Length != 2) continue;
                float x = float.Parse(xz[0], CultureInfo.InvariantCulture), z = float.Parse(xz[1], CultureInfo.InvariantCulture);
                float g = world.GroundAt(x, z);
                var hits = Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f, world.groundMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (h1, h2) => h2.point.y.CompareTo(h1.point.y));
                sb.Append($"({x:F2},{z:F2}) ground={g:F2} walkable={world.Walkable(x, z)} road={world.InWalkArea(x, z)} beach={world.IsBeachSurface(x, z, g)} hits=");
                for (int i = 0; i < hits.Length && i < 4; i++) sb.Append($"{hits[i].point.y:F2}:{hits[i].collider.name} ");
                sb.Append("| ");
            }
            mode = "probe";
            Finish("passed", sb.ToString());
            yield break;
        }

        if (!PickPath(out Vector3 a, out Vector3 b, out string what)) { Finish("no_path", "没找到能走的演示路线：" + mode); yield break; }
        Debug.Log($"FEELDEMO {mode} {what} from ({a.x:F2},{a.y:F2},{a.z:F2}) to ({b.x:F2},{b.y:F2},{b.z:F2}) water={WaterLevel.StaticY:F2}");

        SetupCamera(a, b);
        dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "feel_" + mode + "_*.png")) File.Delete(old);
        Time.captureFramerate = 30;

        Place(a, b);
        yield return Idle(0.6f, "start");
        yield return Go(b, false, false, "walk");
        yield return Idle(1.3f, "rest1");
        yield return Go(a, true, true, "run_jump");
        yield return Idle(1.6f, "rest2");
        if (mode == "beach")
        {
            yield return Step(0f, 0f, false, true);
            yield return Idle(2.2f, "jump_in_place");
        }
        Finish("passed", what);
    }

    // 只准备玩家站位和 QA 存档。从这一帧起由窗口收到的键鼠输入推进玩法。
    IEnumerator PlayInput(string scenario)
    {
        mode = "playinput-" + scenario;
        var boot = FindObjectOfType<GameBootstrap>();
        if (boot == null || boot.ui == null || boot.state == null)
        {
            Finish("setup_failed", "找不到游戏入口");
            yield break;
        }
        Vector3 position = new Vector3(-15f, 3f, -9.4f);
        Vector3 direction = Vector3.forward;
        System.Func<bool> reacted;
        System.Func<string> probe = () => "";
        boot.state.step = "B2_wood";
        boot.state.flags.Add("letters_taken");
        switch (scenario)
        {
            case "wood":
                if (!WoodenMechanismPuzzleModal.DebugOpen("bakery_lock", boot.state, boot.store, player, boot.ui, boot.gate, () => { }))
                { Finish("failed", "找不到面包店木匣"); yield break; }
                WoodenMechanismPuzzleModal.DebugOpenAllLatches();
                int initialRing = WoodenMechanismPuzzleModal.DebugRing(0);
                reacted = () => WoodenMechanismPuzzleModal.IsOpen && WoodenMechanismPuzzleModal.DebugRing(0) != initialRing;
                break;
            case "wind":
                var wind = FindObjectOfType<TidalWindChallenge>();
                var promenade = GameObject.Find("P_22");
                if (wind == null || promenade == null) { Finish("failed", "找不到海风步道"); yield break; }
                position = promenade.transform.position;
                boot.state.step = "L0_cross";
                boot.state.carrying = "ball";
                reacted = () => wind.Survived > 0;
                break;
            case "dog":
                var dog = FindObjectOfType<DogFollower>();
                var yard = GameObject.Find("P_20");
                if (yard == null || dog == null) { Finish("failed", "找不到校园后院或小黑"); yield break; }
                position = yard.transform.position;
                boot.state.step = "S2_ball";
                dog.ShowAt(position + Vector3.left);
                dog.Follow();
                reacted = () => dog.LastCommand.StartsWith("sniff:");
                break;
            case "word":
                var word = FindObjectOfType<WordMaterializerSystem>();
                reacted = () => word != null && word.LastSpawn != null && word.LastSpawn.root != null;
                probe = () => $"letter={boot.ui.LetterOpen} action={word?.LastAction}";
                break;
            case "print":
                var print = FindObjectOfType<PerspectivePrintSystem>();
                var plaza = GameObject.Find("P_FOUNTAIN");
                if (plaza != null) position = plaza.transform.position + Vector3.right * 1.6f;
                direction = Vector3.right;
                reacted = () => print != null && print.IsViewfinderActive && print.LastPrint != null;
                probe = () => $"viewfinder={print?.IsViewfinderActive} aim={print?.AimValid} scale={print?.AimScale:F2}";
                break;
            case "echo":
                var echo = FindObjectOfType<TidalEchoCloneSystem>();
                reacted = () => echo != null && echo.EchoActive;
                break;
            case "shadow":
                var shade = ShadowLanternPuzzle.Instance;
                if (shade == null || !shade.enabled) { Finish("failed", "找不到船棚石台上的布幕"); yield break; }
                position = FindFixtureStand(shade.TurntablePos, 1.28f);
                direction = shade.TurntablePos - position;
                reacted = () => shade.Solved;
                probe = () => $"angle={shade.Angle:F1} near={shade.PlayerNear} pos={player.transform.position:F2}";
                break;
            case "music":
                var music = SoundBottleSystem.Instance;
                if (music == null) { Finish("failed", "找不到广场八音盒"); yield break; }
                boot.state.flags.Add("has_sound_bottle");
                foreach (var sound in SoundBottleSystem.Order) boot.state.flags.Add("sound_" + sound);
                position = FindFixtureStand(music.MusicBoxPos, .95f);
                direction = music.MusicBoxPos - position;
                reacted = () => music.Playing && music.MaxHouseLift > .10f;
                break;
            default:
                Finish("failed", "未知交互 " + scenario);
                yield break;
        }
        if (scenario != "wood")
        {
            position.y = world.GroundAt(position.x, position.z, position.y + .5f);
            Place(position, position + direction);
            var chase = Camera.main != null ? Camera.main.GetComponent<ChaseCamera>() : null;
            if (chase != null) chase.SnapBehind(position, player.yaw);
        }
        string folder = Path.Combine(Application.dataPath, "..");
        File.WriteAllText(Path.Combine(folder, mode + "-ready.txt"), "ready");
        float deadline = Time.realtimeSinceStartup + 18f;
        bool wrotePointer = false;
        while (Time.realtimeSinceStartup < deadline && !reacted())
        {
            if (scenario == "word" && !wrotePointer)
            {
                var card = GameObject.Find("WordSlip_wood");
                if (card != null)
                {
                    var point = RectTransformUtility.WorldToScreenPoint(null, card.transform.position);
                    File.WriteAllText(Path.Combine(folder, mode + "-pointer.txt"),
                        $"{(int)point.x} {Screen.height - (int)point.y} {Screen.width} {Screen.height}");
                    wrotePointer = true;
                }
            }
            yield return null;
        }
        bool passed = reacted();
        if (passed)
        {
            string dir = Path.Combine(folder, "shots");
            Directory.CreateDirectory(dir);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, mode + ".png"));
            yield return new WaitForSecondsRealtime(.6f);
            shot++;
        }
        Finish(passed ? "passed" : "failed", scenario + (passed ? " 收到了键鼠输入并推进了交互 " : " 未观察到预期交互 ") + probe());
    }

    Vector3 FindFixtureStand(Vector3 target, float radius)
    {
        for (int i = 0; i < 16; i++)
        {
            Vector3 candidate = target + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * radius;
            if (world.Walkable(candidate.x, candidate.z) &&
                Mathf.Abs(world.GroundAt(candidate.x, candidate.z, target.y + .5f) - target.y) < .6f) return candidate;
        }
        return target;
    }

    IEnumerator BottleBank()
    {
        var bottle = GameObject.Find("TidalDriftBottle_2");
        var ferry = FindObjectOfType<WorldMechanismDirector>();
        if (bottle == null || ferry == null || WaterLevel.Main == null)
        {
            Finish("failed", "找不到第三只漂流瓶、绞盘或水位组件。");
            yield break;
        }
        WaterLevel.Main.SetInstant(WaterLevel.LevelYs[3]);
        var p = bottle.transform.position;
        var glow = bottle.GetComponentInChildren<Light>();
        var ground = world.GroundAt(p.x, p.z, p.y + .10f);
        bool dry = ground >= WaterLevel.LevelYs[3] + .149f &&
            p.y > ground && world.Walkable(p.x, p.z);
        bool quiet = glow != null && glow.range <= 1f && glow.intensity <= .30f;
        bool clean = GameObject.Find("DynamicGroundGuide3D") == null;
        yield return null;
        cam = Camera.main;
        if (cam != null)
        {
            var chase = cam.GetComponent<ChaseCamera>();
            if (chase != null) chase.enabled = false;
            // Approach from the inland path, on the same side of the railing as the player.
            cam.transform.position = p + new Vector3(.75f, 2.15f, 2.25f);
            cam.transform.LookAt(Vector3.Lerp(p, ferry.WinchPosition, .21f) + Vector3.up * .18f);
            cam.fieldOfView = 55f;
            var folder = Path.Combine(Application.dataPath, "..", "shots");
            Directory.CreateDirectory(folder);
            yield return ShotTool.Capture(cam, 1920, 1080, Path.Combine(folder, "bottle_bank_L3.png"), true);
            shot++;
        }
        Finish(dry && quiet && clean ? "passed" : "failed",
            $"L3={WaterLevel.StaticY:F2} bottle={p:F2} winch={ferry.WinchPosition:F2} dry={dry} softLight={quiet} guideHidden={clean}");
    }

    // 2026-11-15 地形修复验收：-feeldemo spots:<json 路径>。每个机位要么把 EGGBOY 摆到 (x,z)、游戏相机按 yaw/pitch 跟拍（小黑跟着），
    // 要么给 cam/euler/fov 自由机位。图存 shots/spot_<name>.png；结果里记小黑脚下离地面多少（陷地检查）。
    [Serializable] class Spot { public string name; public float x, z, yaw, pitch = 14f, fov = 55f; public int water = -1; public float[] cam, euler; }
    [Serializable] class SpotList { public Spot[] spots; }

    IEnumerator Spots(string path)
    {
        mode = "spots";
        if (!File.Exists(path)) { Finish("setup_failed", "找不到机位文件 " + path); yield break; }
        var list = JsonUtility.FromJson<SpotList>(File.ReadAllText(path));
        cam = Camera.main;
        var chase = cam != null ? cam.GetComponent<ChaseCamera>() : null;
        if (list?.spots == null || chase == null) { Finish("setup_failed", "机位文件为空或缺 ChaseCamera"); yield break; }
        var director = FindObjectOfType<StoryDirector>();
        if (director != null) { director.StopAllCoroutines(); director.enabled = false; }
        var dog = FindObjectOfType<DogFollower>();
        dir = Path.Combine(Application.dataPath, "..", "shots"); Directory.CreateDirectory(dir);
        Time.captureFramerate = 30;
        var sb = new System.Text.StringBuilder();
        foreach (var s in list.spots)
        {
            if (s.water >= 0 && WaterLevel.Main != null) WaterLevel.Main.SetInstant(WaterLevel.LevelYs[Mathf.Clamp(s.water, 0, WaterLevel.LevelYs.Length - 1)]);
            if (s.cam != null && s.cam.Length == 3)
            {
                chase.enabled = false;
                cam.transform.SetPositionAndRotation(new Vector3(s.cam[0], s.cam[1], s.cam[2]),
                    Quaternion.Euler(s.euler != null && s.euler.Length == 3 ? new Vector3(s.euler[0], s.euler[1], s.euler[2]) : Vector3.zero));
                cam.fieldOfView = s.fov;
                for (int i = 0; i < 3; i++) { Shader.SetGlobalVector("_TideCamFade", Vector4.zero); yield return null; }
                yield return ShotTool.Capture(cam, 1774, 887, Path.Combine(dir, "spot_" + s.name + ".png")); shot++;
                chase.enabled = true;
                sb.Append($"{s.name}: free cam | ");
                continue;
            }
            float yawRad = s.yaw * Mathf.Deg2Rad;
            var at = new Vector3(s.x, world.GroundAt(s.x, s.z), s.z);
            Place(at, at + new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad)));
            chase.SnapBehind(at, yawRad); chase.DebugOrbit(yawRad, s.pitch);
            if (dog != null && dog.Bound)
            {
                // 小黑从主角身后 1.6 米处起步（多半在低一级的地面上），自己跟上来：检查它能不能迈上台阶、会不会陷进地板
                if (!dog.Following) dog.Follow();
                var behind = at - new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad)) * 1.6f;
                dog.transform.position = new Vector3(behind.x, world.GroundAt(behind.x, behind.z, at.y + 0.05f), behind.z);
            }
            for (int tick = 0; tick < 50; tick++)
            {
                player.locked = false; player.Move(1f / 30f, 0f, 0f, false, false);
                chase.Tick(1f / 30f, player.camFocus, player.yaw, false, false);
                chase.SetFadeActive(true);
                yield return null;
            }
            yield return ShotTool.Capture(cam, 1774, 887, Path.Combine(dir, "spot_" + s.name + ".png")); shot++;
            string dogNote = "";
            if (dog != null)
            {
                var d = dog.transform.position;
                float top = float.NegativeInfinity;
                foreach (var hit in Physics.RaycastAll(new Vector3(d.x, d.y + 0.6f, d.z), Vector3.down, 0.6f + 0.6f, world.groundMask, QueryTriggerInteraction.Ignore))
                    top = Mathf.Max(top, hit.point.y);
                dogNote = float.IsNegativeInfinity(top) ? $" dog=({d.x:F2},{d.y:F2},{d.z:F2}) noGround" : $" dog=({d.x:F2},{d.y:F2},{d.z:F2}) sunk={top - d.y:F2}";
            }
            var p = player.transform.position;
            sb.Append($"{s.name}: hero=({p.x:F2},{p.y:F2},{p.z:F2}){dogNote} | ");
        }
        Finish("passed", sb.ToString());
    }

    // 2026-11-16 真实接触式拾取验收（docs/PHYSICAL_PICKUP_IK_BOOK_20261116.md 第三节）：-feeldemo pickup
    // 真人游玩路径（不是审计模式）：从红球正面 / 侧面 / 背面、近 / 远按 E（直接调 TakeBallAt），量站位、接触误差、掌心前伸、外壳间隙；
    // 持球走、跳；放进狗碗；读档恢复持球；海风掉球再捡；再走一遍火柴、柴火、塞柴、油壶、拖鞋。图存 shots/pickup_*.png。
    IEnumerator PickupTest()
    {
        var pk = PhysicalPickupController.Instance;
        var director = FindObjectOfType<StoryDirector>();
        var boot = FindObjectOfType<GameBootstrap>();
        var ball = FindObjectOfType<SchoolBall>();
        var ui = FindObjectOfType<EggboyUI>();
        if (pk == null || director == null || boot == null || ball == null) { Finish("setup_failed", "缺拾取控制器 / 剧情 / 红球"); yield break; }
        if (!pk.Active("pickup_ball")) { Finish("setup_failed", "拾取控制器没启用（审计模式？）"); yield break; }
        PhysicalPickupController.Trace = true;
        director.StopAllCoroutines(); director.enabled = false;
        var st = boot.state;
        cam = Camera.main;
        var chase = cam != null ? cam.GetComponent<ChaseCamera>() : null;
        dir = Path.Combine(Application.dataPath, "..", "shots"); Directory.CreateDirectory(dir);
        Time.captureFramerate = 30;
        var sb = new System.Text.StringBuilder();
        int pass = 0, total = 0;
        void Check(string name, bool ok, string note) { total++; if (ok) pass++; sb.Append($"{name}={(ok ? "ok" : "FAIL")} {note} | "); Debug.Log($"PICKUPTEST {name}={ok} {note}"); }
        Vector3 ballHome = ball.transform.position;

        IEnumerator WaitIdle(float max)
        {
            float t0 = Time.time;
            yield return null;
            while ((pk.Busy || player.ActionLocked) && Time.time - t0 < max)
            {
                if (chase != null) { chase.Tick(1f / 30f, player.camFocus, player.yaw, false, false); }
                yield return null;
            }
        }
        IEnumerator Shot(string name, Vector3 focus, float yawDeg)
        {
            if (cam == null) yield break;
            bool chaseOn = chase != null && chase.enabled;
            if (chase != null) chase.enabled = false;
            var q = Quaternion.Euler(34f, yawDeg, 0f);
            cam.transform.SetPositionAndRotation(focus - q * Vector3.forward * 1.6f, q);
            cam.fieldOfView = 45f;
            yield return ShotTool.Capture(cam, 1280, 720, Path.Combine(dir, "pickup_" + name + ".png")); shot++;
            if (chase != null) chase.enabled = chaseOn;
        }
        void ResetBall()
        {
            pk.ReleaseCarryBall();
            ball.ReturnToSwing();
            st.flags.Remove("ball_taken"); st.flags.Remove("ball_in_bowl"); st.carrying = ""; st.step = "S2_ball";
            if (!st.flags.Contains("slipper_seen")) st.flags.Add("slipper_seen");
            player.SetCarryPose(EggboyPlayer.CarryPose.None);
        }

        // 1) 红球：三个方向 × 两个距离
        // 球在秋千正前方（南边），北面是秋千架：背面取斜后方（玩家站不进秋千架里）
        // back_turned：站在球正前方、背对着球按 E（要先转身）
        string[] names = { "front_near", "front_mid", "left_near", "left_far", "right_near", "back_turned" };
        float[] angles = { 0f, 0f, 90f, 90f, -90f, 0f };
        float[] dists = { 0.65f, 0.95f, 0.65f, 1.4f, 0.9f, 0.8f };
        for (int i = 0; i < names.Length; i++)
        {
            ResetBall();
            yield return null;
            Vector3 c = ball.transform.position;
            // 起点：在球的南边（操场朝海一侧）按角度转，脸朝随机方向（不一定朝球）
            Vector3 off = Quaternion.Euler(0f, angles[i], 0f) * Vector3.back * dists[i];
            Vector3 start = new Vector3(c.x + off.x, 0f, c.z + off.z);
            // 起点要是真人站得到的地方：能走、离障碍物 0.45 m 以上；不行就沿同一方向再往外挪
            for (int k = 0; k < 8 && (!world.Walkable(start.x, start.z) || Clear(start, c.y) < 0.45f); k++) start += off.normalized * 0.15f;
            if (!world.Walkable(start.x, start.z)) { Check("ball_" + names[i], false, "起点不能走 " + start); continue; }
            start.y = world.GroundAt(start.x, start.z, c.y + 0.5f);
            if (names[i] == "back_turned") Place(start, start + (start - c));   // 背对球
            else Place(start, start + Quaternion.Euler(0f, 37f * i, 0f) * Vector3.forward);
            if (chase != null) chase.SnapBehind(start, player.yaw);
            yield return null;
            director.TakeBallAt();
            bool began = pk.Busy;
            float t0 = Time.time; bool shotTaken = false;
            while ((pk.Busy || player.ActionLocked) && Time.time - t0 < 8f)
            {
                if (!shotTaken && pk.LastEvent.StartsWith("contact"))
                {
                    shotTaken = true;
                    yield return Shot(names[i], c + Vector3.up * 0.25f, player.transform.eulerAngles.y - 75f);
                }
                yield return null;
            }
            var p = player.transform.position;
            float standDist = new Vector2(p.x - c.x, p.z - c.z).magnitude;
            float yawErr = Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, Mathf.Atan2(c.x - p.x, c.z - p.z) * Mathf.Rad2Deg));
            // 真接触才算过：强制贴上（"forced"，手没伸到球面）算失败
            bool ok = began && st.flags.Contains("ball_taken") && pk.HoldsBall && pk.LastShellGap >= 0f && !pk.LastEvent.Contains("forced");
            Check("ball_" + names[i], ok, $"began={began} stand={standDist:F3} yawErr={yawErr:F1} {pk.LastEvent} shellGap={pk.LastShellGap:F3} held={pk.HoldsBall}");
        }

        // 2) 持球走、跳：球一直贴在手心、不进蛋壳
        float worstErr = 0f, worstGap = 1f;
        if (pk.HoldsBall)
        {
            Vector3 dirWalk = -player.transform.forward;
            for (int f = 0; f < 50; f++)
            {
                player.locked = false;
                player.Move(1f / 30f, dirWalk.x, dirWalk.z, f > 25, f == 20);
                yield return null;
                worstErr = Mathf.Max(worstErr, pk.ContactError);
                worstGap = Mathf.Min(worstGap, pk.LastShellGap);
                if (f == 24) yield return Shot("carry_jump", player.transform.position + Vector3.up * 0.45f, player.transform.eulerAngles.y - 90f);
            }
            for (int f = 0; f < 30; f++) { player.Move(1f / 30f, 0f, 0f, false, false); yield return null; }
        }
        Check("carry_walk_jump", pk.HoldsBall && worstErr < 0.05f && worstGap >= 0f, $"maxPalmError={worstErr:F3} minShellGap={worstGap:F3}");

        // 3) 读档恢复持球：模拟 GameBootstrap 读档（SchoolBall.Take）
        pk.ReleaseCarryBall();
        ball.Take(player.transform);
        yield return null; yield return null;
        Check("reload_carry", pk.HoldsBall && Vector3.Distance(ball.transform.position, player.transform.position) < 1f, $"held={pk.HoldsBall} parent={(ball.transform.parent != null ? ball.transform.parent.name : "none")}");

        // 4) 放进狗碗（木锁已解，直接走 CompletePutBallInBowl）
        Vector3 bowl = ball.BowlPoint;
        Vector3 nearBowl = FindFree(bowl, 1.1f);
        Place(nearBowl, bowl);
        yield return null;
        var complete = typeof(StoryDirector).GetMethod("CompletePutBallInBowl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        complete?.Invoke(director, null);
        bool putBegan = pk.Busy; float t1 = Time.time; bool putShot = false;
        while ((pk.Busy || player.ActionLocked) && Time.time - t1 < 8f)
        {
            if (!putShot && pk.LastEvent.StartsWith("contact")) { putShot = true; yield return Shot("bowl_release", bowl + Vector3.up * 0.25f, player.transform.eulerAngles.y - 75f); }
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);
        float inBowl = Vector3.Distance(ball.transform.position, bowl);
        Check("put_in_bowl", putBegan && ball.InBowl && inBowl < 0.05f && !pk.HoldsBall, $"began={putBegan} ballToBowl={inBowl:F3} {pk.LastEvent}");
        director.StopAllCoroutines();
        if (ui != null && ui.LetterOpen) ui.CloseLetter();
        player.locked = false; player.SetStoryCrouch(false);

        // 5) 海风掉球再捡
        ResetBall();
        ball.Take(player.transform);
        st.flags.Add("ball_taken"); st.carrying = "ball";
        Vector3 here = FindFree(ballHome, 2.5f);
        Place(here, here + Vector3.forward);
        yield return null;
        director.DropCarriedNear(here + Vector3.right * 0.8f);
        yield return new WaitForSeconds(1.8f);
        bool dropped = !pk.HoldsBall && st.carrying == "";
        director.TakeBallAt();
        yield return WaitIdle(8f);
        Check("wind_drop_repick", dropped && st.flags.Contains("ball_taken") && pk.HoldsBall && !pk.LastEvent.Contains("forced"), $"dropped={dropped} {pk.LastEvent}");
        ResetBall();

        // 6) 其他拾取：火柴、柴火、塞柴、油壶、拖鞋（都要先走到物体前、接触帧才提交）
        IEnumerator Item(string name, Vector3 at, System.Action prepare, System.Action call, System.Func<bool> done)
        {
            prepare();
            Vector3 s = FindFree(at, 1.3f);
            Place(s, s + Vector3.left);
            yield return null;
            call();
            bool b = pk.Busy;
            float t2 = Time.time; bool sh = false;
            while ((pk.Busy || player.ActionLocked) && Time.time - t2 < 8f)
            {
                if (!sh && pk.LastEvent.StartsWith("contact")) { sh = true; yield return Shot(name, at + Vector3.up * 0.4f, player.transform.eulerAngles.y - 75f); }
                yield return null;
            }
            var pp = player.transform.position;
            Check(name, b && done() && !pk.LastEvent.Contains("forced"), $"began={b} toTarget={new Vector2(pp.x - at.x, pp.z - at.z).magnitude:F2} {pk.LastEvent}");
            director.StopAllCoroutines();
            if (ui != null && ui.LetterOpen) ui.CloseLetter();
            player.locked = false;
        }
        Vector3 stall = Vector3.zero, wood = Vector3.zero, stove = Vector3.zero, oil = Vector3.zero, slipper = Vector3.zero;
        foreach (var it in BakeryData.Load()) { var v = new Vector3(it.x, it.y, it.z); if (it.id == "stall") stall = v; else if (it.id == "wood") wood = v; else if (it.id == "stove") stove = v; }
        foreach (var it in LighthouseData.Load().items) if (it.id == "oil") oil = new Vector3(it.x, it.y, it.z);
        foreach (var it in SchoolData.Load().items) if (it.id == "slipper") slipper = new Vector3(it.x, it.y, it.z);
        yield return Item("matches", stall, () => { st.step = "B1_matches"; st.flags.Remove("matches_taken"); st.carrying = ""; }, () => director.TakeMatchesAt(stall), () => st.flags.Contains("matches_taken"));
        yield return Item("wood", wood, () => { st.step = "B2_wood"; st.flags.Remove("wood_loaded"); }, () => director.TakeWoodAt(wood), () => st.carrying == "wood");
        yield return Item("load_wood", stove, () => { }, () => director.LoadWoodAt(stove), () => st.flags.Contains("wood_loaded"));
        yield return Item("oil", oil, () => { st.step = "L1_shed"; st.flags.Remove("oil_taken"); st.carrying = ""; }, () => director.TakeOilAt(oil), () => st.carrying == "oil");
        yield return Item("slipper", slipper, () => { st.step = "S1_slipper"; st.flags.Remove("slipper_seen"); }, () => director.InspectSlipperAt(slipper), () => st.flags.Contains("slipper_seen"));
        Finish(pass == total ? "passed" : "failed", $"{pass}/{total} | " + sb);
    }

    // 目标周围 r 米处找一个能站、和目标差不多高的点
    Vector3 FindFree(Vector3 c, float r)
    {
        for (float rr = r; rr <= r + 1.5f; rr += 0.25f)
            for (int a = 0; a < 24; a++)
            {
                float ang = a * Mathf.PI / 12f;
                var q = c + new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr);
                if (!world.Walkable(q.x, q.z)) continue;
                q.y = world.GroundAt(q.x, q.z, c.y + 0.6f);
                if (Mathf.Abs(q.y - c.y) < 0.6f && Clear(q, q.y) >= 0.45f) return q;
            }
        return c;
    }

    // 离最近的迈不上去的碰撞体多远（和 EggboyPlayer 推人的规则一致：顶面比脚下高 0.22 m 以上才挡）
    float Clear(Vector3 p, float ground)
    {
        var list = new List<EggboyCollider>();
        world.QueryColliders(p.x, p.z, list);
        float best = 9f;
        foreach (var c in list)
        {
            if (c.water || (c.top.HasValue && c.top.Value - ground <= 0.22f)) continue;
            if (c.r.HasValue) best = Mathf.Min(best, new Vector2(p.x - c.x, p.z - c.z).magnitude - c.r.Value);
            else if (c.hw.HasValue && c.hd.HasValue) best = Mathf.Min(best, Mathf.Max(Mathf.Abs(p.x - c.x) - c.hw.Value, Mathf.Abs(p.z - c.z) - c.hd.Value));
        }
        return best;
    }

    // 2026-11-15 诊断：照创新审计的做法，L2 时站到爪印踏板东边，让小黑去踩，每 0.25 秒记一次小黑在哪、脚下多高、能不能走
    IEnumerator DogPlateTrace()
    {
        var plate = DogPawPlate.Instance; var dog = FindObjectOfType<DogFollower>();
        var state = FindObjectOfType<GameBootstrap>()?.state;
        if (plate == null || dog == null) { Finish("setup_failed", "no plate/dog"); yield break; }
        if (state != null) state.step = "L1_shed";
        WaterLevel.Main?.SetInstant(WaterLevel.LevelYs[2]);
        Vector3 at = plate.Center;
        for (float r = 2.5f; r <= 4f && at == plate.Center; r += 0.5f)
            for (int a = 0; a < 16; a++) { float ang = a * Mathf.PI / 8f; var q = plate.Center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r); if (world.Walkable(q.x, q.z)) { at = q; break; } }
        at.y = world.GroundAt(at.x, at.z);
        Place(at, plate.Center);
        dog.Follow();
        yield return new WaitForSeconds(0.8f);
        dog.CommandOrPet();
        var sb = new System.Text.StringBuilder($"plate=({plate.Center.x:F2},{plate.Center.y:F2},{plate.Center.z:F2}) hero=({at.x:F2},{at.y:F2},{at.z:F2}) cmd={dog.LastCommand} | ");
        for (int i = 0; i < 32 && !plate.Done; i++)
        {
            var d = dog.transform.position;
            sb.Append($"{i * 0.25f:F2}s ({d.x:F2},{d.y:F2},{d.z:F2}) w={world.Walkable(d.x, d.z)} g={world.GroundAt(d.x, d.z):F2} guide={dog.Guiding} | ");
            yield return new WaitForSeconds(0.25f);
        }
        dir = Path.Combine(Application.dataPath, "..", "shots"); Directory.CreateDirectory(dir);
        cam = Camera.main;
        Finish(plate.Done ? "passed" : "failed", sb.ToString());
    }

    [Serializable] class MapAlignmentCheck
    {
        public string site;
        public Vector2 cameraPixel, playerPixel;
        public float pixelError;
        public bool opened, walkable;
    }

    [Serializable] class MapAlignmentReport
    {
        public List<MapAlignmentCheck> points = new List<MapAlignmentCheck>();
    }

    IEnumerator MapAlignment()
    {
        var map = FindObjectOfType<TownMap>();
        dir = Path.Combine(Application.dataPath, "..", "shots");
        var file = Path.Combine(dir, "mapbase_points.json");
        if (map == null || !map.HasOrthographicMap || !File.Exists(file))
        {
            Finish("setup_failed", "需要已导入实拍底图和 map_bounds 的预览包，以及 shots/mapbase_points.json");
            yield break;
        }
        var calibration = JsonUtility.FromJson<ShotTool.MapCalibration>(File.ReadAllText(file));
        if (calibration == null || calibration.points == null || calibration.points.Count != 4)
        { Finish("setup_failed", "mapbase_points 必须包含四处校准点"); yield break; }

        var story = FindObjectOfType<StoryDirector>();
        if (story != null) story.enabled = false;
        var report = new MapAlignmentReport();
        bool passed = true;
        foreach (var site in calibration.points)
        {
            var position = site.position;
            position.y = world.GroundAt(position.x, position.z);
            bool walkable = world.Walkable(position.x, position.z);
            Place(position, position + Vector3.forward);
            yield return null;
            map.Toggle();
            yield return new WaitForSecondsRealtime(.25f);
            var pixel = map.ProjectUV(player.transform.position);
            float error = Vector2.Distance(pixel, site.pixel);
            bool opened = map.IsOpen;
            passed &= walkable && opened && error <= 1f;
            report.points.Add(new MapAlignmentCheck { site = site.site, cameraPixel = site.pixel,
                playerPixel = pixel, pixelError = error, opened = opened, walkable = walkable });
            if (opened)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "map_alignment_" + site.site + ".png"));
                yield return new WaitForSecondsRealtime(.6f);
                shot++;
                map.Close();
            }
        }
        Finish(passed ? "passed" : "failed", "four in-game map pins versus the orthographic camera projection",
            JsonUtility.ToJson(report));
    }

    // PL-02：真实 Move + 可重复地面夹具，0.01s 固定输入步长；路线回归另跑真实场景。
    sealed class JumpFixture : IEggboyWorld
    {
        public float floor = 20f;
        public IReadOnlyList<EggboyCollider> Colliders => Array.Empty<EggboyCollider>();
        public void QueryColliders(float x, float z, List<EggboyCollider> results) => results.Clear();
        public bool Walkable(float x, float z) => true;
        public float GroundAt(float x, float z) => floor;
        public float GroundAt(float x, float z, float aboveY) => floor;
        public Vector3 MailboxPos => Vector3.zero;
        public Vector3 SpawnPos => new Vector3(0f, floor, 0f);
    }
    [Serializable] sealed class JumpReport
    {
        public string fixture = "real EggboyPlayer.Move, isolated flat/drop floor, dt=0.01s";
        public int bufferedSuccess, coyoteSuccess, expiredUnexpected;
        public List<string> trials = new List<string>();
    }

    void ResetJumpTrial(float y, bool grounded)
    {
        player.ResetJumpInput(); player.locked = false; player.speedScale = 1f;
        player.transform.position = new Vector3(0f, y, 0f); player.vel = Vector3.zero;
        player.grounded = grounded; player.jump = new JumpState();
        player.jump.phase = grounded ? JumpPhase.none : JumpPhase.air;
    }

    IEnumerator JumpWindows()
    {
        var originalWorld = player.world; var originalPosition = player.transform.position;
        var fixture = new JumpFixture(); var report = new JumpReport();
        // 让之前互动的动作锁自然结束，不修改步态链或跳跃动力学常量。
        while (player.ActionLocked) yield return null;
        player.world = fixture;
        try
        {
            for (int trial = 0; trial < 10; trial++)
            {
                float height = 20.5f + trial * .03f; fixture.floor = 20f;
                ResetJumpTrial(height, false);
                int landing = 0;
                while (!player.grounded && landing < 150) { player.Move(.01f, 0, 0, false, false); landing++; }
                ResetJumpTrial(height, false);
                bool success = false;
                for (int i = 1; i < landing + 60; i++)
                {
                    player.Move(.01f, 0, 0, false, i == landing - 8); frame++;
                    if (player.jump.phase == JumpPhase.air && player.jump.vy > 0f) { success = true; break; }
                }
                if (success) report.bufferedSuccess++;
                report.trials.Add($"buffer_{trial}: press 0.08s before measured landing, success={success}");
                yield return null;
            }
            foreach (int delay in new[] { 6, 20 })
            for (int trial = 0; trial < 10; trial++)
            {
                fixture.floor = 20f; ResetJumpTrial(20f, true);
                player.Move(.01f, 0, 0, false, false);
                fixture.floor = 18f; player.Move(.01f, 0, 0, false, false); // 自然迈出边缘
                bool success = false;
                for (int i = 1; i <= delay + 19; i++)
                {
                    player.Move(.01f, 0, 0, false, i == delay); frame++;
                    if (player.jump.phase == JumpPhase.air && player.jump.vy > 0f) { success = true; break; }
                }
                if (success) { if (delay == 6) report.coyoteSuccess++; else report.expiredUnexpected++; }
                report.trials.Add($"coyote_{delay * 10}ms_{trial}: success={success}");
                yield return null;
            }
        }
        finally { player.world = originalWorld; Place(originalPosition, originalPosition + Vector3.forward); }
        bool passed = report.bufferedSuccess == 10 && report.coyoteSuccess == 10 && report.expiredUnexpected == 0;
        Finish(passed ? "passed" : "failed", "jump input windows; scene route regression remains separate", JsonUtility.ToJson(report));
    }

    [Serializable] sealed class CameraWaypoint { public string id; public float x, z; public bool stop; }
    [Serializable] sealed class CameraRouteData { public CameraWaypoint[] route; }
    [Serializable] sealed class CameraSample
    {
        public int routeIndex, yawStep, referencePixels, visiblePixels;
        public Vector3 player, camera; public float visibility; public bool inside;
    }
    [Serializable] sealed class CameraReport
    {
        public int sampledFrames, lowVisibilityFrames, insideSolidFrames, reachedWaypoints, plannedWaypoints;
        public float lowVisibilityFraction;
        public string sampling = "moving: every 10 frames; six fixed stops: all 12 headings at 30 degrees; original scene shaders retained";
        public List<CameraSample> samples = new List<CameraSample>();
    }

    IEnumerator CameraRoute()
    {
        cam = Camera.main;
        var chase = cam != null ? cam.GetComponent<ChaseCamera>() : null;
        var resource = Resources.Load<TextAsset>("World/route");
        var route = resource != null ? JsonUtility.FromJson<CameraRouteData>(resource.text)?.route : null;
        if (chase == null || route == null) { Finish("setup_failed", "缺 ChaseCamera / World/route"); yield break; }
        // 沿正式路线走邮筒、面包店、两排集市、学校后院和船棚。住宅台地绕行另由 route 全量审计覆盖。
        int end = Array.FindIndex(route, p => p.id == "25");
        if (end < 0) { Finish("setup_failed", "标准路线缺船棚站点 25"); yield break; }
        WaterLevel.Main?.SetInstant(WaterLevel.LevelYs[0]);
        var director = FindObjectOfType<StoryDirector>();
        if (director != null) { director.StopAllCoroutines(); director.enabled = false; }
        dir = Path.Combine(Application.dataPath, "..", "shots"); Directory.CreateDirectory(dir);
        Time.captureFramerate = 30;
        chase.enabled = true; chase.extraBlock = world.CameraBlockDistance;
        int market = Array.FindIndex(route, p => p.id == "13");
        int townReturn = Array.FindIndex(route, market + 1, p => p.z > -2f && p.z < 0f);
        if (market < 0 || townReturn <= market || townReturn > end) { Finish("setup_failed", "标准路线缺集市出口"); yield break; }
        var report = new CameraReport { plannedWaypoints = end + 1 - (townReturn - market - 1) };
        Vector3 start = new Vector3(route[0].x, world.GroundAt(route[0].x, route[0].z), route[0].z);
        Place(start, start + Vector3.forward); chase.SnapBehind(start, 0f);
        int yawStep = 0; var photographed = new HashSet<string>();
        string[] stops = { "07", "03", "10", "13", "21", "25" };
        string[] names = { "story_wake", "mailbox", "bakery", "market", "school_backyard", "boathouse" };
        using (var probe = new ShotTool.HeroVisibilityProbe())
        {
            for (int index = 0; index <= end; index++)
            {
                if (index > market && index < townReturn) continue;
                var waypoint = route[index]; var goal = new Vector2(waypoint.x, waypoint.z);
                int stagnant = 0; float best = float.MaxValue; bool reached = false;
                for (int tick = 0; tick < 2400; tick++)
                {
                    var p = player.transform.position; var delta = goal - new Vector2(p.x, p.z);
                    if (delta.magnitude < (waypoint.stop ? .5f : .35f)) { reached = true; break; }
                    if (delta.magnitude < best - .004f) { best = delta.magnitude; stagnant = 0; }
                    else if (++stagnant > 100) break;
                    delta.Normalize(); player.locked = false; player.Move(1f / 30f, delta.x, delta.y, true, false); frame++;
                    chase.Tick(1f / 30f, player.camFocus, player.yaw, true, true);
                    chase.SetFadeActive(true);
                    yield return null;
                    if (tick % 10 == 0)
                    {
                        CameraMeasure(probe, report, index, yawStep);
                        yawStep = (yawStep + 1) % 12; chase.DebugOrbit(yawStep * 30f * Mathf.Deg2Rad);
                    }
                }
                if (!reached)
                {
                    stuckPhases++;
                    Debug.LogWarning($"CAMERA_ROUTE blocked at waypoint {index} {waypoint.id}: {player.transform.position} -> ({waypoint.x}, {waypoint.z})");
                    break;
                }
                report.reachedWaypoints++;
                int named = Array.IndexOf(stops, waypoint.id);
                if (named < 0 || !photographed.Add(waypoint.id)) continue;
                chase.SnapBehind(player.transform.position, 0f); chase.DebugOrbit(0f);
                for (int tick = 0; tick < 30; tick++) chase.Tick(1f / 30f, player.transform.position, player.yaw, false, false);
                chase.SetFadeActive(true);
                yield return ShotTool.Capture(cam, 1774, 887, Path.Combine(dir, "feel_camera_" + names[named] + ".png")); shot++;
                for (int angle = 0; angle < 12; angle++)
                {
                    chase.DebugOrbit(angle * 30f * Mathf.Deg2Rad);
                    chase.Tick(1f / 30f, player.transform.position, player.yaw, false, false); chase.SetFadeActive(true);
                    CameraMeasure(probe, report, index, angle); yield return null;
                }
            }
        }
        report.lowVisibilityFraction = report.sampledFrames > 0 ? report.lowVisibilityFrames / (float)report.sampledFrames : 1f;
        bool passed = report.reachedWaypoints == report.plannedWaypoints && photographed.Count == 6
            && report.lowVisibilityFraction <= .02f && report.insideSolidFrames == 0;
        Finish(passed ? "passed" : "failed", "camera visibility and collision; performance must be measured separately without readback", JsonUtility.ToJson(report));
    }

    void CameraMeasure(ShotTool.HeroVisibilityProbe probe, CameraReport report, int routeIndex, int yawStep)
    {
        var pos = player.transform.position;
        Shader.SetGlobalVector("_TidePlayerPos", new Vector4(pos.x, pos.y, pos.z, 1f));
        float visibility = probe.Sample(cam); bool inside = world.CameraPointBlocked(cam.transform.position);
        foreach (var collider in Physics.OverlapSphere(cam.transform.position, .005f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (collider is BoxCollider && (collider.ClosestPoint(cam.transform.position) - cam.transform.position).sqrMagnitude < .000001f) inside = true;
        report.sampledFrames++; if (visibility < .6f) report.lowVisibilityFrames++; if (inside) report.insideSolidFrames++;
        report.samples.Add(new CameraSample { routeIndex = routeIndex, yawStep = yawStep, referencePixels = probe.ReferencePixels,
            visiblePixels = probe.VisiblePixels, player = pos, camera = cam.transform.position, visibility = visibility, inside = inside });
    }

    // ---------- 渡口还是不是渡口（-feeldemo ferry，约 10 秒，不截图） ----------
    // 重新生成场景以后先跑这个：渡口那段路在 L2 必须被水淹、缺口必须挡路，不然"想办法过海"的玩法就没了。
    // 创新审计里也验这件事，但要跑两分多钟；这个只看地形，改场景时可以反复跑。结果写 feel-demo-ferry.json。
    IEnumerator Ferry()
    {
        var water = WaterLevel.Main;
        var mech = FindObjectOfType<WorldMechanismDirector>();
        var crates = new List<FloatingCrate>(FindObjectsOfType<FloatingCrate>());
        crates.Sort((p, q) => CrateNo(p.name).CompareTo(CrateNo(q.name)));
        if (water == null || mech == null || crates.Count < 5) { Finish("failed", "缺 WaterLevel / WorldMechanismDirector，或浮箱少于 5 个：" + crates.Count); yield break; }

        var sb = new System.Text.StringBuilder();
        int bad = 0;
        void Check(string name, bool ok, string note)
        {
            if (!ok) bad++;
            sb.Append(name).Append(ok ? "=ok " : "=FAIL ").Append(note).Append(" | ");
            Debug.Log($"FERRY {name}={(ok ? "ok" : "FAIL")} {note}");
        }
        // 箱子底下真正的地面（不算箱子自己）
        float Floor(Vector3 at, out string what)
        {
            float best = -99f; what = "none";
            foreach (var h in Physics.RaycastAll(new Vector3(at.x, 200f, at.z), Vector3.down, 400f, world.groundMask, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponent<FloatingCrate>() != null) continue;
                if (h.point.y > best) { best = h.point.y; what = h.collider.name; }
            }
            return best;
        }
        string F(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

        // L1：这段路是干的，照常走
        water.SetInstant(WaterLevel.LevelYs[1]);
        for (int i = 0; i < 4; i++) yield return null;
        int dry = 0;
        foreach (var c in crates) if (world.Walkable(c.transform.position.x, c.transform.position.z)) dry++;
        Check("L1_road_dry", dry == crates.Count, $"{dry}/{crates.Count} 个箱位能走");

        // L2：整段被淹，中间三只箱子不出现，缺口走不过去
        water.SetInstant(WaterLevel.LevelYs[2]);
        for (int i = 0; i < 6; i++) yield return null;
        // 2026-11-15：箱子在 Update 里直接跟到水面，碰撞体要等下一次物理步才跟上。帧率高时 6 帧内可能一次物理步都没跑，
        // 射线还打在河床上，L2_crates_stand 时过时不过。量之前先同步一次。
        Physics.SyncTransforms();
        float w = water.Y, limit = w - IslandWorld.RoadWadeDepth;
        int g0 = crates.Count / 2 - 1, g1 = crates.Count / 2 + 1;
        var floors = new System.Text.StringBuilder();
        float highest = -99f; string highestName = "";
        for (int i = 0; i < crates.Count; i++)
        {
            float f = Floor(crates[i].transform.position, out string what);
            floors.Append(F(f)).Append(i >= g0 && i <= g1 ? "* " : " ");
            if (f > highest) { highest = f; highestName = what; }
        }
        Check("L2_bed_flooded", highest < limit - 0.01f, $"箱位地面 {floors}（* 是缺口）最高 {F(highest)} 来自 {highestName}，要低于 {F(limit)}；箱子记的河床 {F(crates[0].bedY)}");

        var approach = crates[g0 - 1].transform.position;
        approach.y = world.GroundAt(approach.x, approach.z);
        var gap = crates[g0].transform.position;
        var heading = new Vector3(gap.x - approach.x, 0f, gap.z - approach.z);
        float gapStart = heading.magnitude;
        heading.Normalize();
        int blocked = 0, runtimeBlocked = 0;
        for (int i = g0; i <= g1; i++)
        {
            var position = crates[i].transform.position;
            if (!world.Walkable(position.x, position.z)) blocked++;
            if (!world.CanTraverse(approach, position.x, position.z, false)) runtimeBlocked++;
        }
        Place(approach, approach + heading);
        player.locked = false;
        float farthest = 0f;
        float lowestSupport = approach.y;
        for (int i = 0; i < 90; i++)
        {
            player.Move(1f / 30f, heading.x, heading.z, true, false);
            var feet = player.transform.position;
            farthest = Mathf.Max(farthest, Vector3.Dot(feet - approach, heading));
            float floor = world.SurfaceBelow(feet.x, feet.z, feet.y + 0.15f);
            lowestSupport = float.IsNaN(floor) ? float.NegativeInfinity : Mathf.Min(lowestSupport, floor);
            if (i % 6 == 0) yield return null;
        }
        int gapCount = g1 - g0 + 1;
        bool physicallyStopped = gapStart > 0.15f && farthest < gapStart && lowestSupport >= limit &&
            player.transform.position.y >= limit;
        Check("L2_gap_blocks", mech.GapActive && world.FreeRoam && blocked == gapCount &&
            runtimeBlocked == gapCount && physicallyStopped,
            $"缺口 {blocked}/{gapCount}，漫游判定 {runtimeBlocked}/{gapCount}，实走 {F(farthest)}m/首个缺失箱位 {F(gapStart)}m，最低支撑面 {F(lowestSupport)}m/涉水线 {F(limit)}m，gapActive={mech.GapActive}");

        int afloat = 0;
        for (int i = 0; i < crates.Count; i++)
        {
            if (i >= g0 && i <= g1) continue;
            var p = crates[i].transform.position;
            // 箱顶在水面上 18 厘米，站得上去，而且没被别的路面盖住
            if (world.Walkable(p.x, p.z) && Mathf.Abs(world.GroundAt(p.x, p.z) - (w + 0.18f)) < 0.08f) afloat++;
        }
        Check("L2_crates_stand", afloat == crates.Count - (g1 - g0 + 1), $"{afloat}/{crates.Count - (g1 - g0 + 1)} 只箱子浮在水面上能踩");

        Vector3 first = crates[0].transform.position, last = crates[crates.Count - 1].transform.position;
        Vector3 along = last - first; along.y = 0f; along.Normalize();
        Vector3 a = first - along * 1.3f, b = last + along * 1.3f;
        float ga = world.GroundAt(a.x, a.z), gb = world.GroundAt(b.x, b.z);
        Check("L2_banks_dry", world.Walkable(a.x, a.z) && world.Walkable(b.x, b.z) && ga >= limit && gb >= limit,
            $"来路岸 ({F(a.x)},{F(a.z)}) 高 {F(ga)}，对岸 ({F(b.x)},{F(b.z)}) 高 {F(gb)}");

        var wp = mech.WinchPosition;
        float gw = world.GroundAt(wp.x, wp.z, wp.y + 1f);
        Check("L2_winch_dry", gw >= w + 0.15f, $"绞盘 ({F(wp.x)},{F(wp.z)}) 地面 {F(gw)}");

        water.SetInstant(WaterLevel.LevelYs[0]);
        Finish(bad == 0 ? "passed" : "failed", sb.ToString());
    }

    static int CrateNo(string name)
    {
        int us = name.LastIndexOf('_');
        return us >= 0 && int.TryParse(name.Substring(us + 1), out int n) ? n : 0;
    }

    // ---------- 沙滩进得去、出得来吗（-feeldemo beachentry，不截图） ----------
    // 水位 L1 时沙滩没过脚 15 厘米。真用 Move 试：从沙滩边上高一层的路面跑过去跳下来，能不能落到沙滩上；
    // 再从沙滩往回走 / 跳，能不能回到高处。只看结果，写进 feel-demo-beachentry.json。
    IEnumerator BeachEntry()
    {
        Time.captureFramerate = 30;
        var beach = world.beachPoly;
        if (beach == null || beach.points == null) { Finish("no_path", "没有沙滩"); yield break; }
        var inside = new List<Vector2>();
        foreach (var c in Grid(beach.points, 0.5f)) if (world.Walkable(c.x, c.y) && world.IsBeachSurface(c.x, c.y, world.GroundAt(c.x, c.y))) inside.Add(c);
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        foreach (var v in beach.points) { x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); z0 = Mathf.Min(z0, v.y); z1 = Mathf.Max(z1, v.y); }

        // 起跳点：沙滩外 3.5 米以内、能站、比沙滩高出半米到四米的地面（沙滩上面那层平台高 3 米，入口就是从那儿助跑跳下来）
        var starts = new List<Vector3>(); var goals = new List<Vector2>();
        for (float x = x0 - 3.5f; x <= x1 + 3.5f; x += 0.5f)
            for (float z = z0 - 3.5f; z <= z1 + 3.5f; z += 0.5f)
            {
                if (!world.Walkable(x, z)) continue;
                float g = world.GroundAt(x, z);
                if (g < beach.y + 0.55f || g > beach.y + 4f) continue;
                float bestD = 3.5f * 3.5f; Vector2 goal = Vector2.zero; bool found = false;
                foreach (var c in inside) { float d = (c.x - x) * (c.x - x) + (c.y - z) * (c.y - z); if (d < bestD) { bestD = d; goal = c; found = true; } }
                if (found) { starts.Add(new Vector3(x, g, z)); goals.Add(goal); }
            }

        var safe = FindObjectOfType<SafePoints>();
        Vector2 open = Vector2.zero;
        foreach (var c in inside) open += c;
        if (inside.Count > 0) open /= inside.Count;
        int tried = 0, landed = 0, backOut = 0, walkedOff = 0, roamed = 0;
        var spots = new System.Text.StringBuilder();
        for (int i = 0; i < starts.Count && tried < 80; i += Mathf.Max(1, starts.Count / 80))
        {
            tried++;
            var s = starts[i]; var goal = new Vector3(goals[i].x, beach.y, goals[i].y);
            Vector3 far = goal + (goal - new Vector3(s.x, goal.y, s.z)).normalized * 1.5f;   // 瞄着沙滩里面再往前一点跑

            // 不跳、只是走到边上：不该掉下去
            Place(s, goal);
            bool fell = false;
            for (int f = 0; f < 75 && !fell; f++)
            {
                var p = player.transform.position;
                var d = new Vector2(far.x - p.x, far.z - p.z).normalized;
                player.locked = false;
                player.Move(1f / 30f, d.x, d.y, true, false);
                p = player.transform.position;
                fell = p.y < s.y - 0.6f;
                if (f % 5 == 0) yield return null;
            }
            if (fell) walkedOff++;

            // 助跑，第 12 帧起跳
            Place(s, goal);
            bool on = false;
            for (int f = 0; f < 150 && !on; f++)
            {
                var p = player.transform.position;
                var d = new Vector2(far.x - p.x, far.z - p.z).normalized;
                player.locked = false;
                player.Move(1f / 30f, d.x, d.y, true, f == 12);
                p = player.transform.position;
                on = player.grounded && world.IsBeachSurface(p.x, p.z, p.y);
                if (f % 5 == 0) yield return null;
            }
            if (!on) continue;
            landed++;

            // 落地以后走不走得开：朝沙滩中间走 6 秒，看能走出去多远（落在礁石堆里出不来的不算好入口）
            var land = player.transform.position;
            for (int f = 0; f < 180; f++)
            {
                var p = player.transform.position;
                var d = new Vector2(open.x - p.x, open.y - p.z);
                if (d.magnitude < 0.6f) break;
                d.Normalize();
                player.locked = false;
                player.Move(1f / 30f, d.x, d.y, true, false);
                if (f % 5 == 0) yield return null;
            }
            var out1 = player.transform.position;
            float walked = new Vector2(out1.x - land.x, out1.z - land.z).magnitude;
            float left = new Vector2(open.x - out1.x, open.y - out1.z).magnitude;
            if (left < 1.0f || walked > 2.5f) roamed++;
            if (landed <= 14) spots.Append(spots.Length == 0 ? "" : " ").Append($"[{s.x:F1},{s.z:F1}>{land.x:F1},{land.z:F1} walked {walked:F1}]");

            // 回去：顶着岸往回走，3 秒后安全点把人送回岸上（原来的设计，SafePoints 的 beach 救援）
            if (safe == null) continue;
            int before = safe.RescueCount;
            safe.Enabled = true;
            for (int f = 0; f < 420 && safe.RescueCount == before; f++)   // 走回崖底最多 10 秒，再顶 3 秒
            {
                var p = player.transform.position;
                var d = new Vector2(s.x - p.x, s.z - p.z).normalized;
                player.locked = false;
                player.Move(1f / 30f, d.x, d.y, true, false);
                yield return null;
            }
            safe.Enabled = false;
            var end = player.transform.position;
            if (safe.RescueCount > before && end.y >= WaterLevel.StaticY - 0.05f && !world.IsBeachSurface(end.x, end.z, end.y)) backOut++;
        }
        maxDepth = Mathf.Max(0f, WaterLevel.StaticY - beach.y);
        Finish(landed > 0 && roamed > 0 && backOut == landed && walkedOff == 0 ? "passed" : "failed",
            $"beach y={beach.y:F2} open=({open.x:F1},{open.y:F1}) startSpots={starts.Count} tried={tried} walkedOff={walkedOff} landed={landed} roamed={roamed} backOut={backOut} launch={spots}");
    }

    // ---------- 路线 ----------

    bool PickPath(out Vector3 a, out Vector3 b, out string what)
    {
        a = b = Vector3.zero; what = "";
        var dirs = new List<Vector2>();
        for (int k = 0; k < 8; k++) dirs.Add(new Vector2(Mathf.Cos(k * Mathf.PI / 8f), Mathf.Sin(k * Mathf.PI / 8f)));

        if (mode == "bush")
        {
            var root = GameObject.Find("Bushes");
            if (root == null) return false;
            float best = float.MaxValue;
            foreach (Transform t in root.transform)
            {
                var c = t.position;
                // 草坪上的花坛排最前（开满花，花瓣看得清，周围是草）；石板路边的排最后
                float rank = (t.name == "LawnBed" ? 0f : 20f) + (world.InWalkArea(c.x, c.z) ? 100f : 0f);
                if (rank >= best) continue;
                foreach (var d in dirs)
                    if (Clear(c, d, 2.2f, 0f))
                    {
                        best = rank;
                        a = At(c.x - d.x * 2.2f, c.z - d.y * 2.2f); b = At(c.x + d.x * 2.2f, c.z + d.y * 2.2f);
                        what = t.name + "@" + c.x.ToString("F1", CultureInfo.InvariantCulture) + "," + c.z.ToString("F1", CultureInfo.InvariantCulture);
                        break;
                    }
            }
            return best < float.MaxValue;
        }

        if (mode == "grass")
        {
            if (world.lawnPolys == null) return false;
            foreach (var poly in world.lawnPolys)
                foreach (var c in Grid(poly.points, 1f))
                    foreach (var d in dirs)
                        if (world.InLawnArea(c.x, c.y) && Clear(new Vector3(c.x, 0f, c.y), d, 2.4f, 1.2f, true))
                        {
                            a = At(c.x - d.x * 2.4f, c.y - d.y * 2.4f); b = At(c.x + d.x * 2.4f, c.y + d.y * 2.4f);
                            what = "lawn " + poly.id;
                            return true;
                        }
            return false;
        }

        if (mode == "beach")
        {
            if (world.beachPoly == null || world.beachPoly.points == null) return false;
            Vector2 mid = Vector2.zero;
            foreach (var p in world.beachPoly.points) mid += p;
            mid /= world.beachPoly.points.Length;
            float best = float.MaxValue;
            foreach (var c in Grid(world.beachPoly.points, 0.5f))
            {
                float score = (c - mid).sqrMagnitude;
                if (score >= best) continue;
                foreach (var d in dirs)
                    if (Clear(new Vector3(c.x, 0f, c.y), d, 2.3f, 0f))
                    {
                        best = score;
                        a = At(c.x - d.x * 2.3f, c.y - d.y * 2.3f); b = At(c.x + d.x * 2.3f, c.y + d.y * 2.3f);
                        what = "beach depth=" + (WaterLevel.StaticY - a.y).ToString("F2", CultureInfo.InvariantCulture);
                        break;
                    }
            }
            return best < float.MaxValue;
        }
        return false;
    }

    Vector3 At(float x, float z) => new Vector3(x, world.GroundAt(x, z), z);

    static IEnumerable<Vector2> Grid(Vector2[] poly, float step)
    {
        if (poly == null || poly.Length < 3) yield break;
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        foreach (var v in poly) { x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); z0 = Mathf.Min(z0, v.y); z1 = Mathf.Max(z1, v.y); }
        for (float x = x0 + step; x < x1; x += step)
            for (float z = z0 + step; z < z1; z += step)
                yield return new Vector2(x, z);
    }

    // 沿 d 方向前后各 half 米：每 0.2 米都可走、地面是平的、0.6 米内没有挡路的东西；bushFree > 0 时附近还不能有花丛
    bool Clear(Vector3 c, Vector2 d, float half, float bushFree, bool grassOnly = false)
    {
        float y0 = world.GroundAt(c.x, c.z);
        for (float s = -half; s <= half + 0.01f; s += 0.2f)
        {
            float x = c.x + d.x * s, z = c.z + d.y * s;
            if (!world.Walkable(x, z)) return false;
            if (grassOnly && world.InWalkArea(x, z)) return false;   // 石板路 / 广场不长草
            if (Mathf.Abs(world.GroundAt(x, z) - y0) > 0.08f) return false;
            world.QueryColliders(x, z, near);
            foreach (var col in near)
            {
                if (col.water) continue;
                float reach = (col.r ?? Mathf.Max(col.hw ?? 0f, col.hd ?? 0f)) + 0.6f;
                if ((new Vector2(x - col.x, z - col.z)).sqrMagnitude < reach * reach) return false;
            }
        }
        if (bushFree > 0f)
        {
            var root = GameObject.Find("Bushes");
            if (root != null)
                foreach (Transform t in root.transform)
                {
                    var p = t.position;
                    float along = Mathf.Clamp((p.x - c.x) * d.x + (p.z - c.z) * d.y, -half, half);
                    if (new Vector2(p.x - c.x - d.x * along, p.z - c.z - d.y * along).sqrMagnitude < bushFree * bushFree) return false;
                }
        }
        return true;
    }

    // ---------- 机位和截图 ----------

    void SetupCamera(Vector3 a, Vector3 b)
    {
        var main = Camera.main;
        var go = new GameObject("FeelCam");
        cam = go.AddComponent<Camera>();
        if (main != null) { cam.CopyFrom(main); main.enabled = false; }
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        cam.fieldOfView = 38f;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 600f;

        Vector3 mid = (a + b) * 0.5f, along = (b - a).normalized, side = new Vector3(-along.z, 0f, along.x);
        cam.fieldOfView = 46f;
        float back = mode == "bush" ? 3.2f : 4.2f, up = mode == "bush" ? 1.5f : 2.2f;
        Vector3 look = mid + Vector3.up * 0.32f;
        // 树和花丛没有碰撞体，射线看不见它们：拿包围盒挡一下，别让树冠糊在镜头前
        // 房子的模型也没有物理碰撞体（碰撞走的是外轮廓小圆），一样拿包围盒挡；地面、海面、草叶、主角自己不算
        var blockers = new List<Bounds>();
        foreach (var r in FindObjectsOfType<MeshRenderer>())
        {
            var bb = r.bounds;
            if ((bb.center - mid).sqrMagnitude > 196f || Mathf.Max(bb.size.x, bb.size.z) > 25f) continue;
            if (((1 << r.gameObject.layer) & world.groundMask.value) != 0 || r.name == "Sea") continue;
            bool skip = false;
            for (var t = r.transform; t != null && !skip; t = t.parent) skip = t.name == "GrassField" || t.name == "EGGBOY" || t.name == "XIAOHEI";
            if (!skip) blockers.Add(bb);
        }
        // 绕着路线中点试一圈机位（两种高度），挑看得见起点、中点、终点最多的；同分时优先从侧面看
        Vector3 pick = mid + side * back + Vector3.up * up;
        float bestScore = float.MinValue;
        for (int hgt = 0; hgt < 2; hgt++)
            for (int k = 0; k < 8; k++)
            {
                float ang = k * Mathf.PI / 4f;
                Vector3 dir = side * Mathf.Cos(ang) + along * Mathf.Sin(ang);
                Vector3 c = mid + dir * back + Vector3.up * (up + hgt * 1.4f);
                float score = -Mathf.Abs(Vector3.Dot(dir, along)) * 3f - hgt;
                if (world.GroundAt(c.x, c.z) > c.y - 0.4f) score -= 100f;   // 机位在山体里
                foreach (var bb in blockers) if (bb.Contains(c)) { score -= 100f; break; }   // 机位在房子 / 树冠里
                foreach (var target in new[] { a, mid, b })
                {
                    Vector3 to = target + Vector3.up * 0.4f;
                    // 两个方向都打一遍：从模型里面往外打的射线碰不到背面
                    if (Physics.Linecast(c, to, ~0, QueryTriggerInteraction.Ignore) || Physics.Linecast(to, c, ~0, QueryTriggerInteraction.Ignore)) continue;
                    score += 10f;
                    // 路线上的那丛花本身不算挡（它离目标点很近）
                    float len = (to - c).magnitude;
                    var ray = new Ray(c, (to - c) / len);
                    foreach (var bb in blockers)
                        if (bb.IntersectRay(ray, out float hitAt) && hitAt < len - 1.4f) { score -= 6f; break; }
                }
                if (score > bestScore) { bestScore = score; pick = c; }
            }
        Debug.Log($"FEELDEMO cam ({pick.x:F1},{pick.y:F1},{pick.z:F1}) score={bestScore:F1}");
        cam.transform.position = pick;
        cam.transform.LookAt(look);

        rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        cam.allowMSAA = false;
        cam.targetTexture = rt;
        cam.enabled = true;
        grab = new Texture2D(960, 540, TextureFormat.RGB24, false, false);
    }

    void Place(Vector3 a, Vector3 b)
    {
        player.ResetJumpInput();
        player.transform.position = a + Vector3.up * 0.01f;
        player.vel = Vector3.zero;
        player.grounded = true;
        player.jump.phase = JumpPhase.none;
        player.FaceTowards(b);
    }

    // 2026-11-16 -feeldemo wishwheel：心愿玩具（碰撞 / 不堵路 / 拿起端详 / 放回 / 邮差包容量 / 取出）+ 表情动作轮盘（每一格都能触发）
    IEnumerator WishWheelTest()
    {
        var agent = FindObjectOfType<AIAgentCalibrationDirector>();
        var wheelSys = EmoteWheel.Instance;
        var game = FindObjectOfType<PrototypeGame>();
        if (agent == null || wheelSys == null) { Finish("setup_failed", "缺心愿笺 / 轮盘"); yield break; }
        foreach (var c in FindObjectsOfType<Canvas>()) c.enabled = true;
        cam = Camera.main;
        var chase = cam != null ? cam.GetComponent<ChaseCamera>() : null;
        dir = Path.Combine(Application.dataPath, "..", "shots"); Directory.CreateDirectory(dir);
        var sb = new System.Text.StringBuilder(); int pass = 0, total = 0;
        void Check(string name, bool ok, string note) { total++; if (ok) pass++; sb.Append($"{name}={(ok ? "ok" : "FAIL")} {note} | "); Debug.Log($"WISHTEST {name}={ok} {note}"); }
        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) { player.Move(1f / 30f, 0f, 0f, false, false); if (chase != null) chase.Tick(1f / 30f, player.camFocus, player.yaw, false, false); yield return null; } }
        IEnumerator Screen(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir, "wish_" + name + ".png")); yield return null; yield return null; shot++; }

        // 站到中央广场一块空地上（离出生点和路线都有距离）
        Vector3 start = world.SpawnPos;
        Place(start, start + Vector3.forward);
        if (chase != null) chase.SnapBehind(start, player.yaw);
        yield return Frames(10);

        // 1) 造一件比迈步高度高的玩具（玩偶）：有碰撞、不压在路线上
        agent.SpawnLiveWishObjectInWorld("毛绒熊玩偶");
        yield return Frames(20);
        var toy = agent.LastWishToy;
        Check("toy_spawned", toy != null, toy != null ? toy.description : "null");
        if (toy == null) { Finish("failed", sb.ToString()); yield break; }
        var tp = toy.transform.position;
        float routeD = WishToy.RouteDistance(new Vector2(tp.x, tp.z));
        Check("toy_off_route", routeD >= 0.95f, $"routeDist={routeD:F2} radius={toy.Radius:F2}");
        Check("toy_colliders", toy.Kickable ? toy.ColliderCount == 0 : toy.ColliderCount > 0, $"height={toy.Height:F2} circles={toy.ColliderCount}");
        // 2) 朝它走 3 秒：身体不穿进去
        var b = toy.WorldBounds();
        Vector3 from = b.center + (start - b.center).normalized * (toy.Radius + 1.2f); from.y = world.GroundAt(from.x, from.z, b.min.y + 0.5f);
        Place(from, b.center);
        float minGap = float.MaxValue;
        for (int i = 0; i < 90; i++)
        {
            var d = new Vector2(b.center.x - player.transform.position.x, b.center.z - player.transform.position.z).normalized;
            player.locked = false;
            player.Move(1f / 30f, d.x, d.y, false, false);
            yield return null;
            foreach (var c in world.Colliders) if (c.r.HasValue && c.r.Value < 0.2f && Vector2.Distance(new Vector2(c.x, c.z), new Vector2(b.center.x, b.center.z)) <= toy.Radius + 0.1f)
                    minGap = Mathf.Min(minGap, new Vector2(c.x - player.transform.position.x, c.z - player.transform.position.z).magnitude - c.r.Value);
        }
        Check("toy_blocks_body", toy.Kickable || minGap >= 0.28f, $"closest={minGap:F3}（身体撞台子半径 0.30）");
        yield return Screen("toy_world");

        // 3) 拿起 → 端详 → 放回
        Vector3 home = toy.transform.position; int circles = toy.ColliderCount;
        toy.BeginPick();
        float t0 = Time.time;
        while (!WishInspectModal.IsOpen && Time.time - t0 < 10f) yield return null;
        Check("pick_opens_inspect", WishInspectModal.IsOpen && toy.Held && toy.ColliderCount == 0, $"open={WishInspectModal.IsOpen} held={toy.Held} event={PhysicalPickupController.Instance?.LastEvent}");
        yield return Frames(20);
        yield return Screen("inspect");
        WishInspectModal.DebugPutBack();
        t0 = Time.time;
        while (toy.Held && Time.time - t0 < 3f) yield return Frames(1);   // 放回是 0.45 s 的飞回动画，按时间等，不按帧数
        yield return Frames(5);
        Check("put_back", !toy.Held && (toy.transform.position - home).magnitude < 0.05f && toy.ColliderCount == circles && !player.locked, $"moved={(toy.transform.position - home).magnitude:F3} circles={toy.ColliderCount}/{circles}");

        // 4) 收进邮差包 / 取出
        toy.BeginPick(); t0 = Time.time;
        while (!WishInspectModal.IsOpen && Time.time - t0 < 10f) yield return null;
        bool stored = WishInspectModal.DebugStore();
        yield return Frames(5);
        Check("store_in_bag", stored && toy.InBag && !toy.gameObject.activeSelf && toy.ColliderCount == 0 && WishBag.Count == 1, $"bag={WishBag.Count}");
        var back = WishBag.TakeOutLatest(player);
        yield return Frames(5);
        Check("take_out", back == toy && toy.gameObject.activeSelf && !toy.InBag && WishBag.Count == 0 && (toy.Kickable || toy.ColliderCount > 0), $"circles={toy.ColliderCount}");

        // 5) 容量：装满 6 件以后再收不进去
        for (int i = 0; i < WishBag.Capacity; i++)
        {
            agent.SpawnLiveWishObjectInWorld("小木马玩具" + i);
            yield return Frames(3);
            if (agent.LastWishToy != null && agent.LastWishToy != toy) agent.LastWishToy.StoreInBag();
        }
        Check("bag_full", WishBag.Full && WishBag.Count == WishBag.Capacity, $"bag={WishBag.Count}");
        toy.BeginPick(); t0 = Time.time;
        while (!WishInspectModal.IsOpen && Time.time - t0 < 10f) yield return null;
        yield return Frames(10);
        yield return Screen("inspect_bag_full");
        Check("bag_refuses_when_full", !WishInspectModal.DebugStore() && WishInspectModal.IsOpen, "");
        WishInspectModal.DebugPutBack();
        t0 = Time.time;
        while (toy.Held && Time.time - t0 < 3f) yield return Frames(1);

        // 6) 轮盘：8 个动作 + 16 个表情都能触发；小黑不在身边时"摸小黑"不可用
        wheelSys.DebugShow(2);
        yield return Frames(3);
        yield return Screen("wheel_action");
        wheelSys.DebugShow(8 + 12);
        yield return Frames(3);
        yield return Screen("wheel_expr");
        wheelSys.DebugHide();
        int actOk = 0; var bad = new List<string>();
        foreach (var a in new[] { "wave", "hop", "nod", "shake", "look", "give", "signal_wave" })
        {
            yield return Frames(45);
            bool fired = wheelSys.FireByName(a);
            yield return Frames(2);
            if (fired && player.CurrentEmote == a) actOk++; else bad.Add(a + ":" + wheelSys.LastFired + "/" + player.CurrentEmote);
        }
        Check("wheel_actions", actOk == 7, $"{actOk}/7 {string.Join(",", bad)}");
        bool petRefused = !wheelSys.FireByName("pet_dog");
        Check("wheel_pet_dog_needs_dog", petRefused, "");
        int exprOk = 0;
        foreach (var e in EggboyExpressions.All)
        {
            wheelSys.FireByName("expr:" + e);
            if (game != null && game.BaseExpression == e) exprOk++;
        }
        Check("wheel_expressions", exprOk == 16, $"{exprOk}/16");

        Finish(pass == total ? "passed" : "failed", $"{pass}/{total} " + sb.ToString());
    }

    // 走一帧，每 3 帧存一张图
    IEnumerator Step(float dx, float dz, bool run, bool jump)
    {
        player.locked = false;
        player.Move(1f / 30f, dx, dz, run, jump);
        frame++;
        var feel = TideFeel.Main;
        if (feel != null)
        {
            maxDepth = Mathf.Max(maxDepth, feel.HeroDepth);
            if (feel.HeroInBush) bushFrames++;
        }
        if (frame % 3 == 0)
        {
            yield return new WaitForEndOfFrame();
            ProbeTrail();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            grab.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            grab.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, $"feel_{mode}_{shot:000}.png"), grab.EncodeToPNG());
            shot++;
        }
        yield return null;
    }

    IEnumerator Idle(float seconds, string name)
    {
        phases.Add($"\"{name}\":{shot}");
        Debug.Log($"FEELDEMO phase {name} shot={shot}");
        int n = Mathf.RoundToInt(seconds * 30f);
        for (int i = 0; i < n; i++) yield return Step(0f, 0f, false, false);
    }

    IEnumerator Go(Vector3 to, bool run, bool jumpMidway, string name)
    {
        phases.Add($"\"{name}\":{shot}");
        Debug.Log($"FEELDEMO phase {name} shot={shot}");
        Vector3 from = player.transform.position;
        float total = new Vector2(to.x - from.x, to.z - from.z).magnitude;
        bool jumped = !jumpMidway;
        float best = float.MaxValue; int since = 0;
        for (int i = 0; i < 30 * 14; i++)
        {
            var p = player.transform.position;
            var d = new Vector2(to.x - p.x, to.z - p.z);
            float dist = d.magnitude;
            if (dist < 0.15f) yield break;
            if (dist < best - 0.005f) { best = dist; since = 0; }
            else if (++since > 45) { stuckPhases++; Debug.Log($"FEELDEMO stuck in {name} at ({p.x:F2},{p.z:F2}) dist={dist:F2}"); yield break; }
            d /= dist;
            bool jump = !jumped && dist < total * 0.55f;
            if (jump) jumped = true;
            yield return Step(d.x, d.y, run, jump);
        }
    }

    // 痕迹图在主角脚下那一格的值：盖印位置对不对、有没有被镜像，看这里
    void ProbeTrail()
    {
        var feel = TideFeel.Main;
        if (feel == null || !feel.TrailOn) return;
        var p = player.transform.position;
        maxTrail = Mathf.Max(maxTrail, feel.Probe(feel.SlowTexture, p).magnitude);
        // 弹簧图看身体旁边 0.3 米（正中心没有往外的方向，值本来就小）
        maxSpring = Mathf.Max(maxSpring, feel.Probe(feel.SpringTexture, p + player.transform.right * 0.3f).magnitude);
    }

    void Finish(string status, string note, string checks = null)
    {
        var feel = TideFeel.Main;
        string F(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
        string json = "{\"status\":\"" + status + "\",\"mode\":\"" + mode + "\",\"note\":\"" + note.Replace("\"", "'") + "\"" +
            ",\"frames\":" + frame + ",\"shots\":" + shot + ",\"phases\":{" + string.Join(",", phases) + "}" +
            ",\"stuckPhases\":" + stuckPhases +
            ",\"trail\":" + (feel != null && feel.TrailOn ? "\"" + feel.TrailFormat + "\"" : "null") +
            ",\"maxSlowUnderHero\":" + F(maxTrail) + ",\"maxSpringBesideHero\":" + F(maxSpring) +
            ",\"ripples\":" + (feel != null ? feel.Ripples : 0) + ",\"splashes\":" + (feel != null ? feel.Splashes : 0) +
            ",\"petals\":" + (feel != null ? feel.Petals : 0) + ",\"slams\":" + (feel != null ? feel.Slams : 0) +
            ",\"bushes\":" + (feel != null ? feel.BushCount : 0) + ",\"framesInBush\":" + bushFrames +
            ",\"maxDepth\":" + F(maxDepth) + ",\"water\":" + F(WaterLevel.StaticY) +
            ",\"speedScale\":" + F(player != null ? player.speedScale : 1f) + (checks != null ? ",\"checks\":" + checks : "") + "}";
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "feel-demo-" + mode + ".json"), json);
        Debug.Log("FEELDEMO " + json);
        Time.captureFramerate = 0;
        Application.Quit(status == "passed" ? 0 : 1);
    }
}
