// 场景级 3D 机关、三站「心愿展台」、退潮漂流瓶与渡口多解过海总控（WorldMechanismDirector）。
// 包含：
//   1. 四大可动 3D 机关（M1 鲁班三环风门、M2 小黑爪印木匣、M3 潮汐船坞齿轮绞盘、M4 守塔人黄铜三轴星盘）；
//   2. 涨潮渡口三种过海解法与对应“嘀嘟！”成就：
//      - 方案 A：摇动岸边【3D 潮汐船坞齿轮绞盘】升起木码头与浮桥小船 -> 成就【老船坞工 · 齿轮绞盘升桥】；
//      - 方案 B：在「心愿 3D 造物笺」按个人喜好实时生成 3D 渡海载具（大黄鸭浮船/海龟浮台/木筏） -> 成就【凭空造舟 · 乘心愿渡海】；
//      - 方案 C：唤出十秒前的潮汐残影协力摇住绞盘 -> 成就【时空旅人 · 与过去的自己接力】；
//   3. 三大站点「潮汐心愿展台」（面包房橱窗、学校操场、灯塔观景台）：玩家按喜好实时生成的 3D 作品将永久陈列在展台上并保留至结局镜头 -> 成就【筑梦邮差 · 焕然一新的潮汐镇】；
//   4. 退潮浅滩 3 只发光潮汐漂流瓶：拾取阅读镇民留下的海边手札 -> 成就【拾潮之人 · 听见大海的回信】。
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldMechanismDirector : MonoBehaviour
{
    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;

    Transform bakeryMechRoot;
    readonly Transform[] bakeryRings = new Transform[3];

    Transform schoolMechRoot;
    readonly Transform[] schoolRings = new Transform[3];

    Transform lighthouseMechRoot;
    readonly Transform[] lighthouseRings = new Transform[3];

    Transform winchRoot;
    Transform winchRing;
    Transform winchSpindle;
    Transform pontoonBridgeRoot;
    Vector3 winchWorldPos;
    Vector3 crossingChannelCenter;
    float pontoonBaseY;
    float winchSpinAngle;
    bool winchRaised;
    bool nearWinchPromptShown;
    float surgePenaltyCooldown;
    readonly List<FloatingCrate> gapCrates = new List<FloatingCrate>();
    Vector3 crossingDir = Vector3.forward;
    public Vector3 CrossingDirection => crossingDir;
    public bool GapActive => Features.crossingGap && !Features.classicAudit && gapCrates.Count > 0;
    public bool CrossingSolved => winchRaised;

    static int CrateIndex(string name)
    {
        int i = name.LastIndexOf('_');
        return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 999;
    }
    Collider[] pontoonColliders;
    bool pontoonCollidersOn, pontoonCollidersInit;
    bool boatBridge;

    // 三站心愿展台
    readonly Transform[] wishPedestals = new Transform[3];
    readonly Vector3[] wishPedestalPositions = new Vector3[3];

    // 退潮浅滩漂流瓶
    readonly List<Transform> driftBottles = new List<Transform>();
    readonly string[] driftBottleNotes =
    {
        "漂流瓶里的第一张海盐纸条：\n『阿婆烤炉的风门最怕急脾气。先把上面那根牵一发而动全身的中间木栓拨开，再顺着菜谱、台秤和藤篮底下的数字慢慢对。炉膛点着后，给它吹一口暖气，火就旺了。』",
        "漂流瓶里的第二张折角信笺：\n『学校的小黑其实最好哄了。要是你懒得去秋千底下找那颗旧红球，不妨翻开随身的信笺，画一根香喷喷的烤骨头、或者一只新飞盘送进它的碗里。』",
        "漂流瓶里的第三张航标残页：\n『涨潮的时候渡口水急，摇动岸边的黄铜齿轮绞盘就能把旧木船拉起来；当然，你要是愿意在心愿笺上造一艘属于自己的船渡海，大海也会托起你的心意。』"
    };

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        player = p;
        ui = u;
        world = w;
        director = d;
        state = s;
        winchRaised = state != null && state.flags.Contains("dock_winch_raised");

        SpawnBakerySceneMechanism();
        SpawnSchoolSceneMechanism();
        SpawnFloodedDockWinchAndPontoon();
        SpawnLighthouseSceneMechanism();
        SpawnStationWishPedestals();
        SpawnTidalDriftBottles();
    }

    void SpawnBakerySceneMechanism()
    {
        var ovenGo = GameObject.Find("Bakery_oven");
        Vector3 basePos = ovenGo != null ? ovenGo.transform.position : new Vector3(-11.8f, 1.3f, -7.8f);
        if (world != null) basePos.y = world.GroundAt(basePos.x, basePos.z, basePos.y);

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M1_鲁班三环风门机关", 0.68f);
        mech.name = "Scene_M1_鲁班三环风门机关";
        mech.transform.SetParent(transform, false);
        mech.transform.position = basePos + new Vector3(1.08f, 0.92f, 0.42f);
        mech.transform.rotation = Quaternion.Euler(0f, 145f, 0f);
        bakeryMechRoot = mech.transform;
        for (int i = 0; i < 3; i++)
            bakeryRings[i] = TideTownStyleUI.FindDeepChild(bakeryMechRoot, "Ring_" + i);
        if (world != null)
            world.AddCollider(new EggboyCollider { x = mech.transform.position.x, z = mech.transform.position.z, r = 0.12f, top = basePos.y + 1.35f });

        var rack = TideTownStyleUI.CloneSceneLux3D("S11_晾衣架_分享包版", 1.65f, false);
        if (rack != null)
        {
            Vector3 rp = basePos + new Vector3(-2.15f, 0f, 1.15f);
            if (world != null) rp.y = world.GroundAt(rp.x, rp.z, basePos.y);
            rack.transform.SetParent(transform, false);
            rack.transform.position = rp;
            rack.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            if (world != null)
                world.AddCollider(new EggboyCollider { x = rp.x, z = rp.z, r = 0.14f, top = rp.y + 1.45f });
        }

        var buoy = TideTownStyleUI.CloneSceneLux3D("D6_救生圈_v2", 0.58f, false);
        if (buoy != null)
        {
            buoy.transform.SetParent(mech.transform, false);
            buoy.transform.localPosition = new Vector3(0f, -0.58f, -0.08f);
        }

        wishPedestalPositions[0] = basePos + new Vector3(2.4f, 0f, 1.8f);
        if (world != null) wishPedestalPositions[0].y = world.GroundAt(wishPedestalPositions[0].x, wishPedestalPositions[0].z, basePos.y);
    }

    void SpawnSchoolSceneMechanism()
    {
        var bowlGo = GameObject.Find("S_bowl");
        if (bowlGo == null) bowlGo = GameObject.Find("S_doghouse");
        Vector3 basePos = bowlGo != null ? bowlGo.transform.position : new Vector3(17.5f, 3.05f, -5.2f);
        if (world != null) basePos.y = world.GroundAt(basePos.x, basePos.z, basePos.y);

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M2_小黑爪印转盘木匣", 0.62f);
        mech.name = "Scene_M2_小黑爪印转盘木匣";
        mech.transform.SetParent(transform, false);
        mech.transform.position = basePos + new Vector3(0.76f, 0.55f, -0.24f);
        mech.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
        schoolMechRoot = mech.transform;
        for (int i = 0; i < 3; i++)
            schoolRings[i] = TideTownStyleUI.FindDeepChild(schoolMechRoot, "Ring_" + i);
        if (world != null)
            world.AddCollider(new EggboyCollider { x = mech.transform.position.x, z = mech.transform.position.z, r = 0.12f, top = basePos.y + 1.10f });

        wishPedestalPositions[1] = basePos + new Vector3(-2.6f, 0f, 2.2f);
        if (world != null) wishPedestalPositions[1].y = world.GroundAt(wishPedestalPositions[1].x, wishPedestalPositions[1].z, basePos.y);
    }

    [System.Serializable] sealed class RampLayout { public RampRoad[] roads; }
    [System.Serializable] sealed class RampRoad { public string id; public float[] x, z; }

    static RampRoad FindShedRamp()
    {
        var layout = Resources.Load<TextAsset>("World/world_layout");
        var roads = layout != null ? JsonUtility.FromJson<RampLayout>(layout.text)?.roads : null;
        return roads == null ? null : System.Array.Find(roads, road => road.id == "to_shed");
    }

    static float RampDistance(RampRoad road, Vector3 point)
    {
        float distanceSquared = float.PositiveInfinity;
        var p = new Vector2(point.x, point.z);
        for (int i = 1; i < road.x.Length; i++)
        {
            var a = new Vector2(road.x[i - 1], road.z[i - 1]);
            var edge = new Vector2(road.x[i], road.z[i]) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(0.0001f, edge.sqrMagnitude));
            distanceSquared = Mathf.Min(distanceSquared, (p - (a + edge * t)).sqrMagnitude);
        }
        return Mathf.Sqrt(distanceSquared);
    }

    void SpawnFloodedDockWinchAndPontoon()
    {
        var crate0 = GameObject.Find("crate_0");
        var oilGo = GameObject.Find("LH_oil");
        Vector3 cPos = crate0 != null
            ? crate0.transform.position
            : (oilGo != null ? oilGo.transform.position + new Vector3(-3.2f, 0f, -2.5f) : new Vector3(14.5f, 1.35f, 10.5f));

        crossingChannelCenter = cPos + new Vector3(0.8f, 0f, 1.2f);
        winchWorldPos = cPos + new Vector3(-1.55f, 0f, -1.35f);

        // 2026-11-10：渡口挪到真正被水淹的那段路上。浮箱按编号排成一串，中间三只是"缺口"：
        // 真人游玩时它们不出现，要靠绞盘 / 心愿船 / 残影等办法补上；主线审计时照旧全部出现。
        var crates = new List<FloatingCrate>(FindObjectsOfType<FloatingCrate>());
        crates.Sort((a, b) => CrateIndex(a.name).CompareTo(CrateIndex(b.name)));
        if (crates.Count >= 5)
        {
            int g0 = crates.Count / 2 - 1, g1 = crates.Count / 2 + 1;
            Vector3 sum = Vector3.zero;
            for (int i = g0; i <= g1; i++) { gapCrates.Add(crates[i]); sum += crates[i].transform.position; }
            gapA = crates[Mathf.Max(0, g0 - 1)].transform.position;
            gapB = crates[Mathf.Min(crates.Count - 1, g1 + 1)].transform.position;
            hasGapEnds = true;
            crossingChannelCenter = sum / gapCrates.Count;
            crossingChannelCenter.y = crates[g0].bedY;
            crossingDir = crates[crates.Count - 1].transform.position - crates[0].transform.position;
            crossingDir.y = 0f; crossingDir.Normalize();
            // 绞盘放在来路这一侧的岸上：从第一只箱子往回找一块 L2 时不会被淹的可走地面
            Vector3 first = crates[0].transform.position;
            winchWorldPos = first - crossingDir * 1.6f;
            if (world != null)
            {
                var ramp = FindShedRamp();
                Vector3 winchQuay = new Vector3(13.4f, 1.4f, -13.1f);
                float quayGy = world.InWalkArea(winchQuay.x, winchQuay.z) ? world.GroundAt(winchQuay.x, winchQuay.z, 3.0f) : -99f;
                float quayClr = ramp != null ? RampDistance(ramp, winchQuay) : float.MaxValue;
                if (quayGy >= WaterLevel.LevelYs[2] + 0.15f && quayClr >= 1.15f)
                {
                    winchWorldPos = new Vector3(winchQuay.x, quayGy, winchQuay.z);
                }
                else
                {
                    Vector3 side = new Vector3(-crossingDir.z, 0f, crossingDir.x);
                    float bestScore = float.MaxValue;
                    for (float back = 0.0f; back <= 3.6f; back += 0.4f)
                        for (float lat = -2.4f; lat <= 2.4f; lat += 0.4f)
                        {
                            Vector3 q = first - crossingDir * back + side * lat;
                            if (!world.InWalkArea(q.x, q.z)) continue;
                            float gy = world.GroundAt(q.x, q.z, first.y + 3f);
                            if (gy < WaterLevel.LevelYs[2] + 0.15f) continue; // L2 也不淹
                            // The floating crates are diagonal to the ramp. Measure clearance from the
                            // actual ramp centerline so the solid winch does not block the uphill lane.
                            float clearance = ramp != null ? RampDistance(ramp, q) : Mathf.Abs(lat);
                            float score = back + Mathf.Abs(clearance - 1.25f);
                            if (clearance < 0.85f) score += 10f;
                            if (score < bestScore) { bestScore = score; winchWorldPos = new Vector3(q.x, gy, q.z); }
                        }
                }
            }
        }
        if (world != null)
            winchWorldPos.y = world.GroundAt(winchWorldPos.x, winchWorldPos.z, winchWorldPos.y + 1.5f);
        HoldZones.Register("winch", winchWorldPos, WinchRadius);
        game = FindObjectOfType<PrototypeGame>();
        var svc = FindObjectOfType<InteractionService>();
        if (svc != null) svc.Register(new WinchInteractable(this));

        var winchGo = new GameObject("Scene_M3_潮汐船坞齿轮绞盘_总成");
        winchGo.transform.SetParent(transform, false);
        winchGo.transform.position = winchWorldPos;
        Vector3 winchFwd = new Vector3(crossingDir.x, 0f, crossingDir.z);
        winchGo.transform.rotation = winchFwd.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(winchFwd.normalized, Vector3.up)
            : Quaternion.Euler(0f, 40f, 0f);
        winchRoot = winchGo.transform;
        if (world != null)
            world.AddCollider(new EggboyCollider { x = winchWorldPos.x, z = winchWorldPos.z, r = 0.10f, top = winchWorldPos.y + 1.15f });

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M3_潮汐船坞齿轮绞盘", 0.88f);
        mech.transform.SetParent(winchRoot, false);
        mech.transform.localPosition = new Vector3(0f, 0.78f, 0f);
        winchRing = TideTownStyleUI.FindDeepChild(mech.transform, "Ring_0");
        winchSpindle = TideTownStyleUI.FindDeepChild(mech.transform, "Core_Spindle");

        var buoy = TideTownStyleUI.CloneSceneLux3D("D6_救生圈_v2", 0.55f, false);
        if (buoy != null)
        {
            buoy.transform.SetParent(winchRoot, false);
            buoy.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        }

        var pontoonGo = new GameObject("Scene_DockPontoonBridge");
        pontoonGo.transform.SetParent(transform, false);
        pontoonBaseY = crossingChannelCenter.y;
        pontoonGo.transform.position = new Vector3(crossingChannelCenter.x, pontoonBaseY - 1.35f, crossingChannelCenter.z);
        pontoonGo.transform.rotation = Quaternion.LookRotation(crossingDir, Vector3.up);
        pontoonBridgeRoot = pontoonGo.transform;

        // 沉在水下的旧栈桥：顺着过海方向摆，升起后正好补上缺口
        var dock = TideTownStyleUI.CloneSceneLux3D("S8_木码头_分享包版", 2.3f, false);
        if (dock != null)
        {
            dock.transform.SetParent(pontoonBridgeRoot, false);
            dock.transform.localPosition = Vector3.zero;
            dock.transform.localRotation = Quaternion.identity;
            // 码头模型最长的一边要顺着过海方向（本地 Z）
            var db = Lux3DModelFit.WorldBounds(dock);
            Vector3 local = pontoonBridgeRoot.InverseTransformVector(db.size);
            if (Mathf.Abs(local.x) > Mathf.Abs(local.z)) dock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        // 可踩的范围单独给一块顺着过海方向的板，不依赖模型外形
        int groundLayer = LayerMask.NameToLayer("Ground");
        pontoonGo.layer = groundLayer >= 0 ? groundLayer : 6;
        var deck = pontoonGo.AddComponent<BoxCollider>();
        deck.center = new Vector3(0f, 0.2f, 0f);
        deck.size = new Vector3(1.0f, 0.4f, 2.4f);
        deck.enabled = false;

        var oar = TideTownStyleUI.CloneSceneLux3D("D4O_船桨_v2", 1.0f, false);
        if (oar != null)
        {
            oar.transform.SetParent(pontoonBridgeRoot, false);
            oar.transform.localPosition = new Vector3(0.35f, 0.45f, 0.2f);
            oar.transform.localRotation = Quaternion.Euler(8f, 15f, 80f);
        }
    }
    void SpawnLighthouseSceneMechanism()
    {
        var lampGo = GameObject.Find("LH_lamp");
        if (lampGo == null) lampGo = GameObject.Find("LH_scope");
        Vector3 basePos = lampGo != null ? lampGo.transform.position : new Vector3(22.18f, 16.98f, 3.3f);

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M4_守塔人黄铜三轴星盘", 0.78f);
        mech.name = "Scene_M4_守塔人黄铜三轴星盘";
        mech.transform.SetParent(transform, false);
        mech.transform.position = basePos + new Vector3(-0.92f, 0.45f, -0.42f);
        mech.transform.rotation = Quaternion.Euler(0f, -120f, 0f);
        lighthouseMechRoot = mech.transform;
        for (int i = 0; i < 3; i++)
            lighthouseRings[i] = TideTownStyleUI.FindDeepChild(lighthouseMechRoot, "Ring_" + i);
        if (world != null)
            world.AddCollider(new EggboyCollider { x = mech.transform.position.x, z = mech.transform.position.z, r = 0.12f, top = basePos.y + 1.20f, minY = basePos.y - 0.5f });

        var lampHead = TideTownStyleUI.CloneSceneLux3D("B9_灯塔灯头_v4", 0.85f, false);
        if (lampHead != null)
        {
            lampHead.transform.SetParent(mech.transform, false);
            lampHead.transform.localPosition = new Vector3(0f, -0.68f, 0f);
        }

        wishPedestalPositions[2] = basePos + new Vector3(0.95f, -0.35f, -0.85f);
    }

    void SpawnStationWishPedestals()
    {
        string[] pedestalNames = { "WishPedestal_Bakery", "WishPedestal_School", "WishPedestal_Lighthouse" };
        for (int i = 0; i < 3; i++)
        {
            var pGo = new GameObject(pedestalNames[i]);
            pGo.transform.SetParent(transform, false);
            pGo.transform.position = wishPedestalPositions[i];

            var baseModel = TideTownStyleUI.CloneSceneLux3D("S6_路灯_分享包版", 0.65f, false);
            if (baseModel != null)
            {
                baseModel.transform.SetParent(pGo.transform, false);
                baseModel.transform.localScale = new Vector3(1.1f, 0.32f, 1.1f);
            }
            wishPedestals[i] = pGo.transform;
            if (world != null)
            {
                Vector3 wp = wishPedestalPositions[i];
                world.AddCollider(new EggboyCollider
                {
                    x = wp.x,
                    z = wp.z,
                    r = 0.12f,
                    top = wp.y + 0.85f,
                    minY = i == 2 ? wp.y - 0.5f : 0f
                });
            }
        }
    }

    void SpawnTidalDriftBottles()
    {
        Vector3[] pts =
        {
            new Vector3(-7.2f, 1.4f, -3.5f),
            new Vector3(11.4f, 2.2f, -6.2f),
            winchWorldPos + new Vector3(-1.2f, 0f, -0.8f)
        };

        // 绞盘台本身在高潮会湿。沿岸找最近能站住的高处，避开上坡通道和海堤实体。
        bool bottleBankFound = world == null;
        if (world != null)
        {
            bottleBankFound = TryBottleBankPosition(pts[2], out var bank);
            if (bottleBankFound) pts[2] = bank + Vector3.up * .05f;
            else Debug.LogWarning("DRIFTBOTTLE 绞盘附近找不到高潮可达的岸边");
        }

        for (int i = 0; i < pts.Length; i++)
        {
            if (i == 2 && !bottleBankFound) continue;
            Vector3 p = pts[i];
            if (world != null && i != 2) p.y = world.GroundAt(p.x, p.z, p.y);
            var bGo = new GameObject("TidalDriftBottle_" + i);
            bGo.transform.SetParent(transform, false);
            bGo.transform.position = p;

            var jar = TideTownStyleUI.CloneSceneLux3D("A4_果酱罐_v2", 0.42f, false);
            if (jar != null)
            {
                jar.transform.SetParent(bGo.transform, false);
                jar.transform.localRotation = Quaternion.Euler(68f, i * 55f, 15f);
                foreach (var collider in jar.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }

            var lGo = new GameObject("BottleGlow");
            lGo.transform.SetParent(bGo.transform, false);
            lGo.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            var l = lGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color32(251, 207, 142, 255);
            l.range = i == 2 ? .95f : 1.4f;
            l.intensity = i == 2 ? .28f : .44f;
            l.shadows = LightShadows.None;

            driftBottles.Add(bGo.transform);
        }
    }

    bool TryBottleBankPosition(Vector3 origin, out Vector3 position)
    {
        position = origin;
        float dry = WaterLevel.LevelYs[3] + .15f;
        var ramp = FindShedRamp();
        var nearby = new List<EggboyCollider>();
        float best = float.MaxValue;
        const float radius = .31f;
        for (float dx = -5.5f; dx <= 5.5f; dx += .25f)
            for (float dz = -5.5f; dz <= 5.5f; dz += .25f)
            {
                float score = dx * dx + dz * dz;
                if (score >= best) continue;
                var point = origin + new Vector3(dx, 0f, dz);
                if ((ramp != null && RampDistance(ramp, point) < .88f) ||
                    !world.InWalkArea(point.x, point.z)) continue;
                float ground = world.GroundAt(point.x, point.z, 3.65f);
                if (ground < dry || ground > 3.48f) continue;
                world.QueryColliders(point.x, point.z, nearby);
                if (nearby.Exists(c => BlocksBottleBank(c, point, ground, radius))) continue;
                best = score;
                position = new Vector3(point.x, ground, point.z);
            }
        return best < float.MaxValue;
    }

    static bool BlocksBottleBank(EggboyCollider obstacle, Vector3 point, float ground, float radius)
    {
        if (obstacle.water || obstacle.minY > ground + .04f ||
            (obstacle.top.HasValue && obstacle.top.Value <= ground + .22f)) return false;
        float dx = point.x - obstacle.x, dz = point.z - obstacle.z;
        if (obstacle.r.HasValue)
        {
            float reach = obstacle.r.Value + radius;
            return dx * dx + dz * dz < reach * reach;
        }
        return obstacle.hw.HasValue && obstacle.hd.HasValue &&
            Mathf.Abs(dx) < obstacle.hw.Value + radius && Mathf.Abs(dz) < obstacle.hd.Value + radius;
    }

    public Vector3 GetDogBowlPosition()
    {
        var bowlGo = GameObject.Find("S_bowl");
        return bowlGo != null ? bowlGo.transform.position : new Vector3(17.5f, 3.05f, -5.2f);
    }

    public Vector3 GetCrossingChannelCenter() => crossingChannelCenter;
    public Vector3 WinchPosition => winchWorldPos;
    Vector3 gapA, gapB;
    bool hasGapEnds;
    // 缺口两端（缺口外侧最近的两只浮箱，高度按现在的箱顶算）
    public bool GapEndpoints(out Vector3 a, out Vector3 b)
    {
        float top = WaterLevel.StaticY + 0.18f;
        a = new Vector3(gapA.x, top, gapA.z);
        b = new Vector3(gapB.x, top, gapB.z);
        return hasGapEnds;
    }

    public Transform GetNearestWishPedestal(Vector3 pos, out float dist)
    {
        Transform best = wishPedestals[0];
        dist = float.MaxValue;
        for (int i = 0; i < wishPedestals.Length; i++)
        {
            if (wishPedestals[i] == null) continue;
            float d = Vector3.Distance(pos, wishPedestals[i].position);
            if (d < dist)
            {
                dist = d;
                best = wishPedestals[i];
            }
        }
        return best;
    }

    public bool TryEchoAssistWinch(Vector3 actorPos)
    {
        if (winchRoot == null) return false;
        if (Vector3.Distance(actorPos, winchWorldPos) < 12f)
        {
            RaiseDockWinch(true, false);
            return true;
        }
        return false;
    }

    // 审计用：把渡口恢复成"还没解决"，好接着测另一种过海办法
    public void DebugResetCrossing()
    {
        winchRaised = false;
        boatBridge = false;
        if (state != null) state.flags.Remove("dock_winch_raised");
    }

    // 用一条船补上渡口缺口（揭字拼出的木船等）：不升栈桥，记成过海已解决，解锁指定成就
    public void BridgeGapWithBoat(string achievementId)
    {
        winchRaised = true;
        boatBridge = true;
        if (state != null && !state.flags.Contains("dock_winch_raised")) state.flags.Add("dock_winch_raised");
        ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.25f);
        if (!string.IsNullOrEmpty(achievementId)) TideTownAchievementSystem.Unlock(achievementId);
    }

    public const float WinchRadius = 2.4f;
    const float WinchCrankSeconds = 2.5f;
    TidalEchoCloneSystem echoSys;
    PrototypeGame game;
    float winchCrank, winchStrain, winchWiggle, winchCreakAt;
    bool strainFaceShown;
    public bool DebugPlayerHoldsWinch;           // 审计用：代替"玩家按住 E"
    public int WinchHolders { get; private set; }
    public float WinchCrank01 => Mathf.Clamp01(winchCrank / WinchCrankSeconds);
    public bool WinchStrained => winchStrain > 1.2f;

    void WinchTick(float dt, bool playerHolds, bool echoHolds)
    {
        WinchHolders = (playerHolds ? 1 : 0) + (echoHolds ? 1 : 0);
        if (winchRaised || !GapActive) { winchWiggle = 0f; winchStrain = 0f; return; }
        if (WinchHolders >= 2)
        {
            // 两个人一起：真的转起来了
            winchStrain = 0f;
            winchWiggle = 0f;
            winchCrank += dt;
            winchSpinAngle += 260f * dt;
            if (Time.time >= winchCreakAt) { winchCreakAt = Time.time + 0.35f; ShadowAndChimeSystem.PlayProceduralChime(330f + winchCrank * 90f, 0.12f); }
            if (winchCrank >= WinchCrankSeconds) RaiseDockWinch(true, false);
        }
        else if (WinchHolders == 1)
        {
            // 一个人：使劲也只是晃一晃就弹回去
            winchStrain += dt;
            winchCrank = Mathf.Max(0f, winchCrank - dt * 2f);
            winchWiggle = Mathf.Sin(Time.time * 9f) * 7f;
            if (Time.time >= winchCreakAt) { winchCreakAt = Time.time + 0.9f; ShadowAndChimeSystem.PlayProceduralChime(155f, 0.2f); }
            if (winchStrain > 1.2f && playerHolds && game != null && !strainFaceShown) { strainFaceShown = true; game.SetExpression(ExprId.nervous); }
        }
        else
        {
            winchStrain = 0f;
            winchCrank = Mathf.Max(0f, winchCrank - dt * 2f);
            winchWiggle = Mathf.Lerp(winchWiggle, 0f, dt * 8f);
            strainFaceShown = false;
        }
    }

    public void RaiseDockWinch(bool byEcho, bool byWishBoat = false)
    {
        winchRaised = true;
        if (byWishBoat) boatBridge = true;
        winchSpinAngle += 720f;
        if (state != null && !state.flags.Contains("dock_winch_raised"))
            state.flags.Add("dock_winch_raised");

        ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.25f);
        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.4f);
        if (player != null) player.PlayEmote("hop");

        if (byWishBoat)
        {
            TideTownAchievementSystem.Unlock("wish_boat");
        }
        else if (byEcho)
        {
            TideTownAchievementSystem.Unlock("echo_coop");
            TideTownAchievementSystem.Unlock("winch_bridge");
        }
        else
        {
            TideTownAchievementSystem.Unlock("winch_bridge");
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (surgePenaltyCooldown > 0f) surgePenaltyCooldown -= dt;

        bool bakerySolved = state != null && (state.flags.Contains("oven_lit") || state.flags.Contains("puzzle_bakery_lock"));
        AnimateRings(bakeryRings, bakerySolved ? 55f : 12f, dt);

        bool schoolSolved = state != null && (state.flags.Contains("ball_placed") || state.flags.Contains("puzzle_school_lock"));
        AnimateRings(schoolRings, schoolSolved ? 60f : 10f, dt);

        bool lhSolved = state != null && (state.flags.Contains("beam_lit") || state.flags.Contains("puzzle_lighthouse_lock"));
        AnimateRings(lighthouseRings, lhSolved ? 75f : 15f, dt);

        if (winchRing != null)
        {
            float targetZ = winchRaised ? -winchSpinAngle - Time.time * 18f : -15f - winchSpinAngle + winchWiggle;
            winchRing.localRotation = Quaternion.Slerp(winchRing.localRotation, Quaternion.Euler(0f, 0f, targetZ), dt * 6f);
        }
        if (winchSpindle != null)
        {
            float targetZ = winchSpinAngle - winchWiggle;
            winchSpindle.localRotation = Quaternion.Slerp(winchSpindle.localRotation, Quaternion.Euler(0f, 0f, targetZ), dt * 6f);
        }
        if (pontoonBridgeRoot != null)
        {
            // 这一局用心愿船补了缺口，栈桥就不用升了（读档后船不在了，栈桥顶上）
            bool bridgeUp = winchRaised && !boatBridge;
            // 升起后浮在当前水面上（桥面略高于水），没升起就沉在水底
            float floatY = Mathf.Max(pontoonBaseY, WaterLevel.StaticY - 0.22f);
            float bob = bridgeUp ? Mathf.Sin(Time.time * 2.1f) * 0.02f : 0f;
            float targetY = bridgeUp ? floatY + bob : pontoonBaseY - 1.35f;
            Vector3 targetPos = new Vector3(crossingChannelCenter.x, targetY, crossingChannelCenter.z);
            pontoonBridgeRoot.position = Vector3.Lerp(pontoonBridgeRoot.position, targetPos, dt * 3.5f);
            // 缺口：真人游玩时中间几只浮箱不出现
            bool gap = GapActive;
            foreach (var gc in gapCrates) if (gc != null) gc.suppressed = gap;
            // 沉在水下的栈桥不能踩：碰撞只在升起后生效
            if (pontoonColliders == null) pontoonColliders = pontoonBridgeRoot.GetComponentsInChildren<Collider>(true);
            if (pontoonCollidersOn != bridgeUp || !pontoonCollidersInit)
            {
                pontoonCollidersInit = true;
                pontoonCollidersOn = bridgeUp;
                foreach (var c in pontoonColliders) if (c != null) c.enabled = bridgeUp;
            }
        }

        // 绞盘：一个人摇不动，要两个"人"同时按住（你 + 10 秒前的你）
        if (player != null && ui != null)
        {
            bool free = ui.inputClose && !player.locked && !ui.TitleShowing && !ui.Paused && !ui.LetterOpen;
            bool near = Vector3.Distance(player.transform.position, winchWorldPos) < WinchRadius;
            bool playerHolds = near && ((free && Input.GetKey(KeyCode.E)) || DebugPlayerHoldsWinch);
            if (echoSys == null) echoSys = FindObjectOfType<TidalEchoCloneSystem>();
            bool echoHolds = echoSys != null && echoSys.EchoActive && echoSys.EchoHoldZone == "winch";
            WinchTick(dt, playerHolds, echoHolds);
        }

        if (player == null || ui == null || !ui.inputClose || player.locked || ui.TitleShowing || ui.Paused ||
            ui.LetterOpen || ui.Cinematic || MenuController.OverlayOpen || !Input.GetKeyDown(KeyCode.E))
            return;

        // 1. 拾取阅读退潮浅滩的漂流瓶（按 E）
        for (int i = 0; i < driftBottles.Count; i++)
        {
            var b = driftBottles[i];
            if (b == null || !b.gameObject.activeSelf) continue;
            if (Vector3.Distance(player.transform.position, b.position) < 1.45f)
            {
                b.gameObject.SetActive(false);
                ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
                ui.ShowToast(driftBottleNotes[i % driftBottleNotes.Length], 5.5f);
                TideTownAchievementSystem.Unlock("drift_bottles");
                return;
            }
        }

        // （原来这里有"涉水就瞬移回岸边"的惩罚：缺口处本来就走不进水里，瞬移只会让人莫名其妙，已去掉）
    }

    static void AnimateRings(Transform[] rings, float speedDeg, float dt)
    {
        if (rings == null) return;
        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] == null) continue;
            float dir = (i % 2 == 0) ? 1f : -1.35f;
            rings[i].Rotate(0f, 0f, speedDeg * dir * dt, Space.Self);
        }
    }
}

// 绞盘旁的提示（按住 E 由 WorldMechanismDirector 自己读，这里只负责显示那行字）
public sealed class WinchInteractable : IInteractable
{
    readonly WorldMechanismDirector mech;
    public WinchInteractable(WorldMechanismDirector m) { mech = m; }
    public string EntityId => "dock_winch";
    public Vector3 Position => mech.WinchPosition;
    public string Prompt(GameState s)
    {
        if (!mech.GapActive || mech.CrossingSolved) return "";
        if (mech.WinchHolders >= 2) return "一起摇——";
        return mech.WinchStrained ? "一个人摇不动" : "按住 E　摇绞盘";
    }
    public bool CanInteract(GameState s) => false;
    public void Interact(GameState s, InteractionService svc) { }
}
