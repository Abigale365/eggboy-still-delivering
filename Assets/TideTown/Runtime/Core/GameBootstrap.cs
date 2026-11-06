// WP-07 启动：把状态、存档、互动、剧情挂到现有 PrototypeGame 上，保留旧场景入口。
using System;
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
        "-noIntro", "-routeaudit", "-routeaudit2", "-wateraudit", "-scanaudit",
        "-autotest", "-townrun", "-docktest", "-shot", "-overview", "-dockview", "-capture",
    };


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
        director.Init(player, ui, state, store, gate);

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

        // WP-09：扫描站默认隐藏；-scan 打开时才注册到互动服务
        var scan = FindObjectOfType<ScanStation>();
        if (scan != null)
        {
            scan.SetEnabled(Features.scan);
            if (Features.scan) service.Register(scan);
        }

        // WP-10：序章只在真实开局时演，自动测试或固定机位模式跳过
        var args = Environment.GetCommandLineArgs();
        if (state.step == "P0_boot" && !Array.Exists(args, a => Array.IndexOf(SkipIntroArgs, a) >= 0))
        {
            intro = gameObject.AddComponent<IntroSequence>();
            intro.Begin(this);
        }

        if (Array.Exists(args, a => a == "-storyaudit"))
            gameObject.AddComponent<StoryAudit>().Begin(this);
        if (Array.Exists(args, a => a == "-wateraudit"))
            gameObject.AddComponent<WaterAudit>().Begin(this);
        if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "-scanaudit"))
            gameObject.AddComponent<ScanAudit>().Begin(this);
        initialized = true;
    }
}
