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
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.Alpha1))
        {
            TriggerMode(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TriggerMode(2);
        }
        else if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Alpha3))
        {
            if (!ui.LetterOpen && !AnyModalOpen)
                TriggerMode(3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            TriggerMode(4);
        }
    }
}
