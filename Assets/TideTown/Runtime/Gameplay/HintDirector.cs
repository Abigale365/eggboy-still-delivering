// 卡住才给提示（工程书 INNOVATION_BOOK_20261110 的 WP-03）。
// 平时不给任何引导；玩家在某个谜题附近、长时间没有进展时，一层一层加码：
//   第 1 层（45 s）  EGGBOY 换"思考"表情，转头看向下一条线索
//   第 2 层（90 s）  小黑跑到线索旁蹦两下；线索上亮起一团暖光
//   第 3 层（150 s） 提醒再读一遍信（信里相关的那句话会变色）
//   第 4 层（240 s） 一句话说明下一步（全游戏唯一允许直接说明的地方）
// 任何进展都会清零。暂停时按 H 切换：常规 / 少（时间翻倍）/ 关。
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class HintDirector : MonoBehaviour
{
    public static HintDirector Instance { get; private set; }
    static readonly float[] TierSeconds = { 45f, 90f, 150f, 240f };
    static readonly string[] RateNames = { "常规", "少", "关" };

    // 给揭字系统用：现在该在哪封信里把哪句话标出来
    public string LetterHintId { get; private set; } = "";
    public string LetterHintPhrase { get; private set; } = "";

    public string CurrentNodeId { get; private set; } = "";
    public int CurrentTier { get; private set; }
    public float StuckSeconds { get; private set; }
    public int Rate { get; private set; } // 0 常规 1 少 2 关

    sealed class Node
    {
        public string id;
        public Func<bool> active;
        public Func<Vector3> center;
        public float radius;
        public Func<string> progress;  // 进展签名：变了就清零
        public Func<Vector3> clue;     // 下一条线索在哪
        public Func<string> letterId, phrase, tell;
    }

    EggboyPlayer player; EggboyUI ui; IslandWorld world; StoryDirector director; GameState state;
    PrototypeGame game; DogFollower dog; WorldMechanismDirector mech;
    readonly List<Node> nodes = new List<Node>();
    Node current;
    string lastProgress = "";
    GameObject beacon;
    Light beaconLight;
    bool tutorialShown;

    readonly Dictionary<string, Vector3> bakery = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Vector3> school = new Dictionary<string, Vector3>();
    Vector3 oilPos, doorPos, lampPos;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        Instance = this;
        player = p; ui = u; world = w; director = d; state = s;
        game = FindObjectOfType<PrototypeGame>();
        dog = FindObjectOfType<DogFollower>();
        mech = FindObjectOfType<WorldMechanismDirector>();
        Rate = Mathf.Clamp(PlayerPrefs.GetInt("tidetown.hintrate", 0), 0, 2);
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-hints") Rate = args[i + 1] == "off" ? 2 : args[i + 1] == "less" ? 1 : 0;

        foreach (var it in BakeryData.Load()) bakery[it.id] = new Vector3(it.x, it.y, it.z);
        foreach (var it in SchoolData.Load().items) school[it.id] = new Vector3(it.x, it.y, it.z);
        var lh = LighthouseData.Load();
        foreach (var it in lh.items) if (it.id == "oil") oilPos = new Vector3(it.x, it.y, it.z);
        doorPos = LighthouseData.Point(lh.door);
        lampPos = LighthouseData.Point(lh.lamp);
        BuildNodes();
    }

    bool F(string flag) => state.flags.Contains(flag);
    Vector3 B(string id) => bakery.TryGetValue(id, out var v) ? v : Vector3.zero;
    Vector3 S(string id) => school.TryGetValue(id, out var v) ? v : Vector3.zero;

    void BuildNodes()
    {
        // 面包店：火柴 → 柴 →（湿柴要换成干的）→ 点火
        nodes.Add(new Node
        {
            id = "bakery_fire",
            active = () => state.step.StartsWith("B") && state.step != "B0_arrive" && !F("oven_lit"),
            center = () => B("stove"), radius = 16f,
            progress = () => state.step + F("matches_taken") + F("wood_loaded") + F("wood_wet") + state.carrying + state.seenMemories.Count,
            clue = () => !F("matches_taken") ? B("stall") : (!F("wood_loaded") || F("wood_wet")) ? B("wood") : B("stove"),
            letterId = () => F("wood_wet") ? "ying_dog" : "lin_oven",
            phrase = () => F("wood_wet") ? "晒干了" : !F("wood_loaded") ? "后院的木柴" : "再生一次火",
            tell = () => !F("matches_taken") ? "门前的摊位上有一盒火柴。"
                : !F("wood_loaded") ? "后院的柴堆可以抱一捆柴来，塞进炉膛。"
                : F("wood_wet") ? "炉膛里的柴是湿的。去后院抱一捆干的来换；或者翻翻阿萤的信，那里有个「干」字。"
                : "炉子的风门锁着。看看店里的菜谱、台秤、面包篮，背面各有一个刻度。",
        });
        // 学校：拖鞋 → 红球 → 狗碗
        nodes.Add(new Node
        {
            id = "school_dog",
            active = () => (state.step == "S1_slipper" || state.step == "S2_ball" || state.step == "S3_bowl") && !F("ball_in_bowl"),
            center = () => S("bowl"), radius = 22f,
            progress = () => state.step + state.carrying + F("slipper_seen") + state.seenMemories.Count,
            clue = () => state.step == "S1_slipper" ? S("slipper") : state.carrying == "ball" ? S("bowl") : S("ball"),
            letterId = () => "ying_dog",
            phrase = () => state.carrying == "ball" ? "放回了学校后门" : "最喜欢的球",
            tell = () => state.step == "S1_slipper" ? "后门外有一只被咬过的拖鞋，爪印从那里一直通到操场。"
                : state.carrying == "ball" ? "把球带回学校后门的狗碗那里。"
                : "操场的秋千下面有一颗红球。也可以按 V 给小黑许一样它爱吃的。",
        });
        // 涨潮的渡口：浮箱中间有缺口
        nodes.Add(new Node
        {
            id = "crossing",
            active = () => mech != null && mech.GapActive && !mech.CrossingSolved && state.step == "L0_cross"
                && WaterLevel.StaticY >= WaterLevel.LevelYs[2] - 0.05f,
            center = () => mech.GetCrossingChannelCenter(), radius = 12f,
            progress = () => state.step + mech.CrossingSolved,
            clue = () => mech.WinchPosition,
            letterId = () => "chen_lighthouse",
            phrase = () => "船棚里",
            tell = () => "岸边的绞盘连着水下的旧栈桥，按 E 摇一摇。也可以把信里的「木柴」和「船」拼在一起扔过去，或者按 V 许一条船。",
        });
        // 灯塔：油壶 → 塔门 → 灯头
        nodes.Add(new Node
        {
            id = "lighthouse",
            active = () => (state.step == "L1_shed" || state.step == "L2_door" || state.step == "L3_oil" || state.step == "L4_light") && !F("beam_lit"),
            center = () => state.step == "L1_shed" ? oilPos : doorPos, radius = 20f,
            progress = () => state.step + state.carrying + F("lamp_oiled") + state.seenMemories.Count,
            clue = () => state.step == "L1_shed" ? oilPos : state.step == "L2_door" ? doorPos : lampPos,
            letterId = () => "chen_lighthouse",
            phrase = () => state.step == "L1_shed" ? "半壶煤油" : "能不能再亮一次",
            tell = () => state.step == "L1_shed" ? "船棚的屋檐下有一只油壶。"
                : state.step == "L2_door" ? "带着油壶走台阶上灯塔，在塔门按 E。"
                : state.step == "L3_oil" ? "给塔顶的灯头加油。"
                : "灯头的星盘锁着。塔顶的帽子、望远镜、航海日志上各有一个刻度。",
        });
    }

    bool PlayerFree =>
        ui != null && ui.inputClose && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen && !player.locked
        && !ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen && !AvantGardeGameplayHub.AnyModalOpen
        && !AIAgentCalibrationDirector.IsOpen && (director == null || (!director.Cutscene && !director.Climbing));

    void Update()
    {
        if (state == null || player == null || ui == null) return;
        if (ui.Paused && Input.GetKeyDown(KeyCode.H))
        {
            Rate = (Rate + 1) % 3;
            PlayerPrefs.SetInt("tidetown.hintrate", Rate);
            ui.ShowToast("提示：" + RateNames[Rate], 2f);
            if (Rate == 2) Reset();
        }
        if (!ui.inputClose) return; // 自动审计不计时，审计用 DebugAdvance
        Tick(Time.deltaTime, PlayerFree);
    }

    // 审计用：直接往前拨时间
    public void DebugAdvance(float seconds) => Tick(seconds, true);

    void Tick(float dt, bool playerFree)
    {
        Node node = null;
        if (Rate != 2)
            foreach (var n in nodes)
            {
                if (!n.active()) continue;
                Vector3 c = n.center();
                Vector3 p = player.transform.position;
                if (new Vector2(p.x - c.x, p.z - c.z).magnitude <= n.radius) { node = n; break; }
            }
        if (node != current) { Reset(); current = node; lastProgress = node != null ? node.progress() : ""; }
        if (current == null) return;

        string prog = current.progress();
        if (prog != lastProgress) { lastProgress = prog; ResetTimerOnly(); return; }
        if (!playerFree) { UpdateBeacon(); return; } // 读信、解机关的时间不算卡住

        StuckSeconds += dt;
        CurrentNodeId = current.id;
        float mult = Rate == 1 ? 2f : 1f;
        int tier = 0;
        for (int i = 0; i < TierSeconds.Length; i++) if (StuckSeconds >= TierSeconds[i] * mult) tier = i + 1;
        while (CurrentTier < tier) { CurrentTier++; Fire(CurrentTier); }
        UpdateBeacon();
    }

    void Fire(int tier)
    {
        Vector3 clue = current.clue();
        Debug.Log($"HINT node={current.id} tier={tier} stuck={StuckSeconds:F0}s");
        if (tier == 1)
        {
            if (game != null) game.SetExpression(ExprId.thinking);
            if (player.Speed < 0.2f) { player.FaceTowards(clue); player.PlayEmote("look"); }
        }
        else if (tier == 2)
        {
            if (game != null) game.SetExpression(ExprId.curious);
            ShowBeacon(clue);
            if (dog != null && dog.Following && dog.Visible) dog.GuideTo(clue, 9f);
            ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.25f);
        }
        else if (tier == 3)
        {
            LetterHintId = current.letterId();
            LetterHintPhrase = current.phrase();
            ui.SetTutorial("Tab　再读一遍信");
            tutorialShown = true;
            ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.25f);
        }
        else if (tier == 4)
        {
            ui.SetTutorial(current.tell());
            tutorialShown = true;
            ShadowAndChimeSystem.PlayProceduralChime(523.25f, 0.3f);
        }
    }

    void ShowBeacon(Vector3 at)
    {
        if (beacon == null)
        {
            beacon = new GameObject("HintBeacon");
            beaconLight = beacon.AddComponent<Light>();
            beaconLight.type = LightType.Point;
            beaconLight.color = new Color(1f, 0.82f, 0.45f);
            beaconLight.range = 3.2f;
            beaconLight.shadows = LightShadows.None;
        }
        beacon.SetActive(true);
        beacon.transform.position = at + Vector3.up * 0.6f;
    }

    void UpdateBeacon()
    {
        if (beacon == null || !beacon.activeSelf || current == null) return;
        beacon.transform.position = current.clue() + Vector3.up * 0.6f;
        beaconLight.intensity = 1.6f + Mathf.Sin(Time.unscaledTime * 3.2f) * 1.1f;
    }

    void ResetTimerOnly()
    {
        StuckSeconds = 0f;
        CurrentTier = 0;
        LetterHintId = ""; LetterHintPhrase = "";
        if (beacon != null) beacon.SetActive(false);
        if (tutorialShown && ui != null) { ui.SetTutorial(""); tutorialShown = false; }
    }

    void Reset()
    {
        ResetTimerOnly();
        current = null;
        CurrentNodeId = "";
    }
}
