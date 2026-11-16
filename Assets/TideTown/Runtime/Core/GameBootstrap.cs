// WP-07 启动：把状态、存档、互动、剧情挂到现有 PrototypeGame 上，保留旧场景入口。
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] class RouteStop { public string id; public bool stop; public float x, z; }
[Serializable] class RouteList { public RouteStop[] route; }

public sealed class GameBootstrap : MonoBehaviour
{
    public EggboyPlayer player;
    public EggboyUI ui;
    public IslandWorld world;
    public GameState state;
    public SaveStore store;
    public ControlGate gate;
    public InteractionService service;
    public StoryDirector director;
    public WaterLevel water;
    public IntroSequence intro;

    // 这些模式测的是别的系统，或需要固定机位，开场镜头会添乱
    static readonly string[] SkipIntroArgs =
    {
        "-noIntro", "-traversalaudit", "-wateraudit", "-scanaudit", "-innovaudit", "-lux3dlive",
        "-autotest", "-townrun", "-docktest", "-shot", "-overview", "-dockview", "-capture", "-mapaudit", "-showcase",
    };

    internal static bool IsRouteAuditFlag(string arg) =>
        string.Equals(arg, "-routeaudit", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-routeaudit2", StringComparison.OrdinalIgnoreCase);

    bool initialized;

    void Start() { StartCoroutine(InitWhenReady()); }

    System.Collections.IEnumerator InitWhenReady()
    {
        for (int i = 0; i < 120 && !initialized; i++)
        {
            if (FindObjectOfType<EggboyPlayer>() != null && FindObjectOfType<EggboyUI>() != null) Init();
            if (!initialized) yield return null;
        }
    }

    void Init()
    {
        if (initialized) return;
        player = FindObjectOfType<EggboyPlayer>();
        ui = FindObjectOfType<EggboyUI>();
        world = FindObjectOfType<IslandWorld>();
        if (player == null || ui == null) return;
        if (world != null) RegisterLawnTreeTrunks();

        if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "-resetsave")) new SaveStore().Clear();
        store = new SaveStore();
        state = store.Load();
        if (state == null) { state = new GameState(); state.step = "P0_boot"; }

        // WP-08：水位跟存档走；-water 命令行优先
        water = FindObjectOfType<WaterLevel>();
        if (water != null)
        {
            water.OnSettled = y => { state.waterLevel = y; store.Save(state); };
            if (!water.CliOverride && state.waterLevel > 0.01f) water.SetInstant(state.waterLevel);
        }

        gate = new ControlGate();
        service = gameObject.AddComponent<InteractionService>();
        service.Init(player, ui, gate, () => state);
        director = gameObject.AddComponent<StoryDirector>();
        director.Init(player, ui, state, store, gate, water);
        // 2026-11-16 真实接触式拾取（docs/PHYSICAL_PICKUP_IK_BOOK_20261116.md）：读档恢复持球前就要有
        player.gameObject.AddComponent<PhysicalPickupController>().Init(player, world, director);
        ui.legacyPrompt = false;

        var asset = Resources.Load<TextAsset>("World/route");
        if (asset != null)
        {
            var route = JsonUtility.FromJson<RouteList>(asset.text);
            if (route != null && route.route != null)
                foreach (var p in route.route)
                    if (p.stop && p.id == "03")
                    {
                        float y = world != null ? world.GroundAt(p.x, p.z) : 0f;
                        service.Register(new MailboxInteractable(new Vector3(p.x, y, p.z), director));
                        break;
                    }
        }

        // WP-11：面包店的摊位、柴火、烤炉和 A4–A8 回忆线索
        var effects = FindObjectOfType<BakeryEffects>();
        foreach (var item in BakeryData.Load())
        {
            var pos = new Vector3(item.x, item.y, item.z);
            switch (item.id)
            {
                case "stall": service.Register(new StallInteractable(pos, director)); break;
                case "wood": service.Register(new WoodInteractable(pos, director)); break;
                case "stove": service.Register(new StoveInteractable(pos, director, effects)); break;
                case "bell": service.Register(new BellInteractable(pos, director, item.label, item.text)); break;
                default:
                    if (!string.IsNullOrEmpty(item.text))
                        service.Register(new MemoryInteractable(pos, director, item.id, item.label, item.text));
                    break;
            }
        }
        // 存档里已经点过火，重开时炉火和烟要还在
        if (effects != null && state.flags.Contains("oven_lit")) effects.Light();

        // WP-13：学校后门狗碗/狗窝，操场红球/拖鞋和两条回忆线索
        var school = SchoolData.Load();
        var schoolBall = FindObjectOfType<SchoolBall>();
        foreach (var item in school.items)
        {
            var pos = new Vector3(item.x, item.y, item.z);
            switch (item.id)
            {
                case "bowl": service.Register(new BowlInteractable(pos, director)); break;
                case "ball": service.Register(new BallInteractable(pos, director)); break;
                case "slipper": service.Register(new SlipperInteractable(pos, director)); break;
                default:
                    if (!string.IsNullOrEmpty(item.text))
                        service.Register(new MemoryInteractable(pos, director, item.id, item.label, item.text));
                    break;
            }
        }
        if (schoolBall != null)
        {
            if (state.flags.Contains("ball_in_bowl")) schoolBall.PlaceInBowl();
            else if (state.carrying == "ball") schoolBall.Take(player.transform);
        }

        // WP-14：船棚油壶、灯塔塔门、灯头和塔顶三件回忆
        var light = LighthouseData.Load();
        foreach (var item in light.items)
        {
            var pos = new Vector3(item.x, item.y, item.z);
            if (item.id == "oil") service.Register(new OilCanInteractable(pos, director));
            else if (!string.IsNullOrEmpty(item.text))
                service.Register(new MemoryInteractable(pos, director, item.id, item.label, item.text));
        }
        service.Register(new LighthouseDoorInteractable(LighthouseData.Point(light.door), director));
        service.Register(new LampHeadInteractable(LighthouseData.Point(light.lamp), director));
        service.Register(new LighthouseDownInteractable(LighthouseData.Point(light.deck), director));

        // WP-15：邮局门口的课桌和老周的信，山那边的新城灯光
        var ending = EndingData.Load();
        service.Register(new EndingDeskInteractable(EndingData.Point(ending.desk), director));

        // 存档里已经点过灯，重开时天光和光束、山那边的手电都要还在
        if (state.flags.Contains("beam_lit"))
        {
            var dimmer = FindObjectOfType<SkyDimmer>();
            if (dimmer != null) dimmer.SetDimmedInstant();
            var beacon = FindObjectOfType<BeaconLight>();
            if (beacon != null) beacon.TurnOn();
            var reply = FindObjectOfType<FlashlightReply>();
            if (reply != null) reply.SetDoneInstant();
        }
        // 存档已经天亮或已到结局，就保持黎明和山顶那份画面
        if (state.flags.Contains("dawn_shown") || state.flags.Contains("ending_done"))
        {
            var dimmer = FindObjectOfType<SkyDimmer>();
            if (dimmer != null) dimmer.SetDawnInstant();
        }
        if (state.flags.Contains("ending_done"))
        {
            var city = FindObjectOfType<CityGlow>();
            if (city != null) city.Show();
            ui.ShowSubtitle("所有的信，都送到了。");
            gameObject.AddComponent<EndingDispatch>();
            ui.SetGoal("");
        }

        // WP-12/13：小黑在 S3 前不在场，球放进碗后从滑梯下探头，S4 读完信才开始跟随
        var dog = FindObjectOfType<DogFollower>();
        if (dog != null)
        {
            dog.Bind(player, world, state);
            if (state.flags.Contains("school_read")) dog.Follow();
            else if (state.flags.Contains("ball_in_bowl")) dog.ShowAt(dog.PeekPoint);
            else dog.Hide();
        }

        // WP-09：扫描站默认隐藏；-scan 打开时才注册到互动服务
        var scan = FindObjectOfType<ScanStation>();
        if (scan != null)
        {
            scan.SetEnabled(Features.scan);
            if (Features.scan) service.Register(scan);
        }

        // WP-16：地图（预览图 + 投影红点）和 Esc 暂停菜单
        var map = gameObject.AddComponent<TownMap>();
        map.Init(player, ui, director, gate);
        gameObject.AddComponent<MenuController>().Init(ui, map, director);
        // WP-22/P1：F3 显示碰撞盒线框
        gameObject.AddComponent<CollisionDebugView>();
        // 方向 B：潮汐海风浪涌 & 护送重心平衡挑战
        gameObject.AddComponent<TidalWindChallenge>().Init(player, ui, world, state);
        // 创新方向 D / E / F / G：拆字成物、透视拓印、潮汐回声分身、光影成形与八音盒
        gameObject.AddComponent<AvantGardeGameplayHub>().Init(player, ui, world, director, state);
        // 2026-11-16 用户议题 3：按住中键的表情与动作轮盘
        gameObject.AddComponent<EmoteWheel>().Init(player, ui);

        // WP-10：序章只在真实开局时演，自动测试或固定机位模式跳过；WP-16 真实开局先给标题画面
        var args = Environment.GetCommandLineArgs();
        bool introMode = state.step == "P0_boot" &&
            !Array.Exists(args, a => IsRouteAuditFlag(a) || Array.IndexOf(SkipIntroArgs, a) >= 0);
        if (introMode)
        {
            if (Array.Exists(args, a => a == "-storyaudit") || Array.Exists(args, a => a == "-notitle"))
            {
                intro = gameObject.AddComponent<IntroSequence>();
                intro.Begin(this);
            }
            else
            {
                player.locked = true;
                ui.ShowTitle(() =>
                {
                    intro = gameObject.AddComponent<IntroSequence>();
                    intro.Begin(this);
                });
            }
        }

        if (Array.Exists(args, a => a == "-traversalaudit"))
            gameObject.AddComponent<TraversalAudit>().Begin(this);
        if (Array.Exists(args, a => a == "-storyaudit"))
            gameObject.AddComponent<StoryAudit>().Begin(this);
        if (Array.Exists(args, a => a == "-mapaudit"))
            gameObject.AddComponent<MapAudit>().Begin(this);
        if (Array.Exists(args, a => a == "-wateraudit"))
            gameObject.AddComponent<WaterAudit>().Begin(this);
        if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "-scanaudit"))
            gameObject.AddComponent<ScanAudit>().Begin(this);
        if (Array.Exists(args, a => a == "-innovaudit"))
            gameObject.AddComponent<InnovationAudit>().Begin(this);
        if (Array.Exists(args, a => a == "-lux3dlive"))
            gameObject.AddComponent<LiveLux3DCheck>().Begin(this);
        // 2026-11-16 UI 展示图（封面 / HUD / 信 / 地图册 / 暂停手账）
        if (Array.Exists(args, a => a == "-uishowcase") && introMode)
            gameObject.AddComponent<UiShowcase>().Begin(this);
        initialized = true;
    }

    void RegisterLawnTreeTrunks()
    {
        // TreeMesh uses a 17 cm trunk base before scaling. In the player the static batch may share its mesh with other trees.
        const float localTrunkRadius = .17f;
        var pending = new List<EggboyCollider>();
        var nearby = new List<EggboyCollider>();
        foreach (var tree in FindObjectsOfType<MeshRenderer>())
        {
            if (!tree.enabled || !tree.name.EndsWith("LawnTree", StringComparison.Ordinal)) continue;
            Vector3 center = tree.transform.position;
            if (!world.InLawnArea(center.x, center.z) ||
                Mathf.Abs(world.GroundAt(center.x, center.z) - center.y) > .20f) continue;
            if (!Array.Exists(tree.sharedMaterials, material => material != null &&
                material.name.StartsWith("NatureTrunk", StringComparison.Ordinal))) continue;
            var scale = tree.transform.lossyScale;
            float radius = localTrunkRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            world.QueryColliders(center.x, center.z, nearby);
            // On stacked terraces, the contours at these XZ coordinates may belong to the lower lawn.
            float trunkReach = radius + .06f;
            if (nearby.Exists(c => c.r.HasValue && c.top.HasValue && c.top.Value > center.y + .25f &&
                (new Vector2(c.x - center.x, c.z - center.z)).sqrMagnitude <= trunkReach * trunkReach)) continue;

            pending.Add(new EggboyCollider { x = center.x, z = center.z, r = radius, minY = center.y - .15f });
        }
        foreach (var trunk in pending) world.AddCollider(trunk);
        if (pending.Count > 0) Debug.Log($"LAWN_TRUNKS runtime={pending.Count}");
    }
}
