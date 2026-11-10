// 邮差随身互动中枢（AvantGardeGameplayHub）。
// 严格遵循去标签化沉浸设计：不在屏幕上方挂载任何常驻的“四种游戏模式标签栏”，
// 所有能力均有机融入剧情与场景交互中（读信时直接揭下字签抛空成物、按 Tab 翻看当前信件、在涨潮渡口或站点按 R 唤出潮汐回声协力）。
using UnityEngine;

public sealed class AvantGardeGameplayHub : MonoBehaviour
{
    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;

    WordMaterializerSystem wordSys;
    PerspectivePrintSystem perspSys;
    TidalEchoCloneSystem echoSys;
    ShadowAndChimeSystem shadowSys;

    public static bool AnyModalOpen { get; private set; }

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        player = p;
        ui = u;
        world = w;
        director = d;
        state = s;

        wordSys = gameObject.AddComponent<WordMaterializerSystem>();
        wordSys.Init(p, u, w, d, s);

        perspSys = gameObject.AddComponent<PerspectivePrintSystem>();
        perspSys.Init(p, u, w, s);

        echoSys = gameObject.AddComponent<TidalEchoCloneSystem>();
        echoSys.Init(p, u, w, d, s);

        shadowSys = gameObject.AddComponent<ShadowAndChimeSystem>();
        shadowSys.Init(p, u, s);

        var achieveSys = gameObject.AddComponent<TideTownAchievementSystem>();
        achieveSys.Init(u, s);

        var worldMech = gameObject.AddComponent<WorldMechanismDirector>();
        worldMech.Init(p, u, w, d, s);

        var hwHub = gameObject.AddComponent<HardwareAndSensorHub>();
        hwHub.Init(p, u, w, d, s);

        var aiDir = gameObject.AddComponent<AIAgentCalibrationDirector>();
        aiDir.Init(p, u, w, d, s);

        // 小黑的浅滩爪印踏板（WP-C）
        gameObject.AddComponent<DogPawPlate>().Init(w, s, u);
        // 影子钥匙 + 装声音的瓶子 + 广场八音盒（WP-G）
        gameObject.AddComponent<ShadowLanternPuzzle>().Init(p, u, w, s);
        gameObject.AddComponent<SoundBottleSystem>().Init(p, u, w, s);

        // 卡住才给提示（要放在最后：它要找到上面这些系统）
        gameObject.AddComponent<HintDirector>().Init(p, u, w, d, s);
    }

    void TriggerMode(int mode)
    {
        if (mode == 1)
        {
            if (perspSys != null && perspSys.IsViewfinderActive) perspSys.CloseViewfinder();
            if (shadowSys != null && shadowSys.IsStudioOpen) shadowSys.CloseStudio();
            if (wordSys != null) wordSys.OpenLetterWithSlips();
        }
        else if (mode == 2)
        {
            if (ui != null && ui.LetterOpen) ui.CloseLetter();
            if (shadowSys != null && shadowSys.IsStudioOpen) shadowSys.CloseStudio();
            if (perspSys != null) perspSys.ToggleViewfinder();
        }
        else if (mode == 3)
        {
            if (echoSys != null) echoSys.TriggerEchoButton();
        }
        else if (mode == 4)
        {
            if (ui != null && ui.LetterOpen) ui.CloseLetter();
            if (perspSys != null && perspSys.IsViewfinderActive) perspSys.CloseViewfinder();
            if (shadowSys != null) shadowSys.ToggleStudio();
        }
    }

    void Update()
    {
        if (ui == null || !ui.inputClose)
        {
            AnyModalOpen = false;
            return;
        }

        AnyModalOpen = (perspSys != null && perspSys.IsViewfinderActive) ||
                       (shadowSys != null && shadowSys.IsStudioOpen);

        if (ui.TitleShowing || ui.Paused || ObjectInspectModal.IsOpen || WoodenMechanismPuzzleModal.IsOpen)
            return;

        // 按 Tab 或 L 键可随时翻阅邮包里的当前信件（在信纸上直接揭下字签抛向空中化为实物）
        // 按键表见 docs/controls.md：Tab 读信揭字，R 潮汐怀表。数字键切"模式"的做法已去掉。
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            TriggerMode(1);
        }
        // C：举起取景框（透视拓印）。放下由取景框自己处理（C / Esc），这里只负责打开
        else if (Input.GetKeyDown(KeyCode.C) && !ui.LetterOpen && perspSys != null && !perspSys.IsViewfinderActive)
        {
            perspSys.OpenViewfinder();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            if (!ui.LetterOpen && !AnyModalOpen)
                TriggerMode(3);
        }
        // 透视拓印（WP-E）和光影声音（WP-G）的 v3 旧版是弹窗假玩法，重做完成前不给玩家入口；
        // 开发时加 -oldmodes 命令行还能用 2 / 4 键打开看
        else if (Features.oldModes && Input.GetKeyDown(KeyCode.Alpha2))
        {
            TriggerMode(2);
        }
        else if (Features.oldModes && Input.GetKeyDown(KeyCode.Alpha4))
        {
            TriggerMode(4);
        }
    }
}
