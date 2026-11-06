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

        if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "-storyaudit"))
            gameObject.AddComponent<StoryAudit>().Begin(this);
        initialized = true;
    }
}
