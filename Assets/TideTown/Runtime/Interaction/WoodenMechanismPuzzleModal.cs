// 方向 A：三大站点核心联动机关（榫卯三连滑栓 + 三环齿轮咬合刻度 + 旧物暗扣线索链 + 试错反震惩罚）。
// 彻底移除“直接解开机关”作弊按钮与未检视直接剧透答案：
//   1. 玩家必须先在院子/塔顶翻转 3D 旧物并拆解背面暗扣（或让小黑嗅探）以获知三环密码；
//   2. 必须先解开第一重「三连榫卯木栓」，才能拨动第二重「三环咬合木轮」；
//   3. 拨好三环后点击【推动主轴开锁】：若连续 3 次推错刻度，触发防盗卡簧反震惩罚（吹灭火柴或弹飞红球，必须重拿）！
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class WoodenMechanismPuzzleModal : MonoBehaviour
{
    public sealed class PuzzleSpec
    {
        public string id;
        public string title;
        public string subtitle;
        public string lux3DModelName;
        public string mechanism3DName;
        public int[] targetRings;
        public string[] clueItemIds;
        public string[] clueItemNames;
        public string[] clueTexts;
        public string[] lockedCluePrompts;
        public string successToast;
    }

    static WoodenMechanismPuzzleModal instance;

    static readonly Dictionary<string, PuzzleSpec> Specs = new Dictionary<string, PuzzleSpec>
    {
        {
            "bakery_lock", new PuzzleSpec
            {
                id = "bakery_lock",
                title = "面包房烤炉 · 鲁班三环风门机关",
                subtitle = "炉门旁压着一张沾满面粉的便签：『老炉子怕海风倒灌，凑齐屋里三样老物件底下的数字方肯开膛。塞入干柴后，给火苗吹一口暖气，烟囱便会升起炊烟。』",
                lux3DModelName = "Bakery_scale",
                mechanism3DName = "M1_鲁班三环风门机关",
                targetRings = new[] { 3, 5, 2 },
                clueItemIds = new[] { "recipe", "scale", "basket" },
                clueItemNames = new[] { "菜谱本", "老台秤", "藤编面包篮" },
                clueTexts = new[]
                {
                    "外环「书页」纹样（菜谱封底）：起海风时需分 3 次鼓风  ->  刻度【 3 】",
                    "中环「秤砣」纹样（台秤底座）：黄铜秤砣常年停在 5 两  ->  刻度【 5 】",
                    "内环「藤篮」纹样（面包篮底）：生日面包多放 2 勺葡萄干  ->  刻度【 2 】"
                },
                lockedCluePrompts = new[]
                {
                    "外环轮缘：镌刻着一枚「翻开的书页」纹样",
                    "中环轮缘：镌刻着一枚「黄铜秤砣」纹样",
                    "内环轮缘：镌刻着一枚「藤编提篮」纹样"
                },
                successToast = "3D 鲁班三环风门严丝合缝归位，面包房烤炉已点燃！"
            }
        },
        {
            "school_lock", new PuzzleSpec
            {
                id = "school_lock",
                title = "学校后院 · 小黑爪印转盘木匣",
                subtitle = "食盆旁压着阿萤留下的值日字条：『小黑守了一整天，肚子饿了，或者想玩新玩具了。送它一样东西吧——哪怕用笔在信笺背面画出来也算数。』",
                lux3DModelName = "S_doghouse",
                mechanism3DName = "M2_小黑爪印转盘木匣",
                targetRings = new[] { 4, 1, 6 },
                clueItemIds = new[] { "slipper", "doghouse", "blackboard" },
                clueItemNames = new[] { "旧拖鞋", "狗窝木箱", "操场黑板" },
                clueTexts = new[]
                {
                    "外环「牙印」纹样（拖鞋鞋底）：鞋帮留着 4 颗小狗乳牙印  ->  刻度【 4 】",
                    "中环「木屋」纹样（狗窝内侧）：放学第 1 个出来的人喂水  ->  刻度【 1 】",
                    "内环「黑板」纹样（黑板背框）：第 6 值日小组留下的签名  ->  刻度【 6 】"
                },
                lockedCluePrompts = new[]
                {
                    "外环轮缘：镌刻着一枚「小狗牙印」纹样",
                    "中环轮缘：镌刻着一枚「尖顶木屋」纹样",
                    "内环轮缘：镌刻着一枚「值日黑板」纹样"
                },
                successToast = "爪印转盘木匣清脆弹开，红球顺着木槽滚进了狗碗！"
            }
        },
        {
            "lighthouse_lock", new PuzzleSpec
            {
                id = "lighthouse_lock",
                title = "灯塔顶层 · 守塔人黄铜三轴星盘",
                subtitle = "星盘底座压着老陈泛黄的航标签：『三环对准方位、仰角与闪频后，光束便能刺破夜雾。当光束照向对岸山脊时，朝山那边的人挥挥手吧。』",
                lux3DModelName = "LH_scope",
                mechanism3DName = "M4_守塔人黄铜三轴星盘",
                targetRings = new[] { 6, 2, 3 },
                clueItemIds = new[] { "hat", "scope", "log" },
                clueItemNames = new[] { "守塔人帽", "黄铜望远镜", "航海日志" },
                clueTexts = new[]
                {
                    "外环「船帽」纹样（白檐帽衬）：潮汐镇航标北偏西第 6 象限  ->  刻度【 6 】",
                    "中环「铜镜」纹样（望远镜筒）：红漆固定在仰角第 2 档位  ->  刻度【 2 】",
                    "内环「日志」纹样（航海日志）：对准山脊连闪 3 次光束联络  ->  刻度【 3 】"
                },
                lockedCluePrompts = new[]
                {
                    "外环轮缘：镌刻着一枚「白檐船帽」纹样",
                    "中环轮缘：镌刻着一枚「单筒铜镜」纹样",
                    "内环轮缘：镌刻着一枚「航海日志」纹样"
                },
                successToast = "3D 黄铜三轴星盘校准完毕，灯塔主透镜已点亮！"
            }
        }
    };

    public static bool IsOpen => instance != null && instance.active;
    public static int ActiveRingSelection { get; set; } = 0;

    public static void ExternalToggleLatch(int index)
    {
        if (!IsOpen) return;
        instance.ToggleLatch(Mathf.Clamp(index, 0, 2));
    }

    public static void ExternalUnlockAllLatchesByBlowOrGesture(string sourceLabel)
    {
        if (!IsOpen || instance.puzzleSolved) return;
        instance.latches[0] = instance.latches[1] = instance.latches[2] = true;
        instance.stage1Unlocked = true;
        instance.feedbackBanner = "【" + sourceLabel + "】三连榫卯木栓已全部震开落槽！现在请拨动三环齿轮。";
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.25f);
        instance.RefreshPuzzleUI();
    }

    public static void ExternalRotateRing(int ringIndex, int delta)
    {
        if (!IsOpen || delta == 0) return;
        instance.RotateRing(Mathf.Clamp(ringIndex, 0, 2), delta > 0 ? 1 : -1);
    }

    public static void ExternalAttemptUnlock()
    {
        if (!IsOpen) return;
        instance.AttemptUnlockSpindle();
    }

    public static void ExternalAlignRingByVoiceNote(int ringIndex, string noteName)
    {
        if (!IsOpen || instance.currentSpec == null || instance.puzzleSolved) return;
        int r = Mathf.Clamp(ringIndex, 0, 2);
        if (!instance.stage1Unlocked)
        {
            instance.latches[0] = instance.latches[1] = instance.latches[2] = true;
            instance.stage1Unlocked = true;
        }
        instance.rings[r] = instance.currentSpec.targetRings[r];
        ShadowAndChimeSystem.PlayProceduralChime(523.25f + r * 130f, 0.28f);
        instance.feedbackBanner = "【声控音律共鸣 · " + noteName + "】第 " + (r + 1) + " 环齿轮已随你的歌声自动共鸣对准刻度【 " + instance.rings[r] + " 】！";
        instance.RefreshPuzzleUI();
    }

    public static void ExternalStartAICalibration()
    {
        if (!IsOpen || instance.puzzleSolved) return;
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.AICalibrateStepByStepRoutine());
    }

    System.Collections.IEnumerator AICalibrateStepByStepRoutine()
    {
        if (currentSpec == null) yield break;
        feedbackBanner = "【AI Agent 实时校准中】正在分析三连木栓耦合矩阵，解开第一重木栓...";
        RefreshPuzzleUI();
        yield return new WaitForSecondsRealtime(0.45f);

        latches[0] = latches[1] = latches[2] = true;
        stage1Unlocked = true;
        ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.22f);
        RefreshPuzzleUI();

        string[] rNames = { "外环", "中环", "内环" };
        for (int r = 0; r < 3; r++)
        {
            int target = currentSpec.targetRings[r];
            int guard = 0;
            while (rings[r] != target && guard++ < 8)
            {
                feedbackBanner = "【AI Agent 实时校准中】正在反向补偿啮合齿轮，校准" + rNames[r] + "刻度 -> 【 " + target + " 】";
                RotateRing(r, 1);
                yield return new WaitForSecondsRealtime(0.32f);
            }
        }

        feedbackBanner = "【AI Agent 校准完毕】三环齿轮已全部啮合到位，正在推动中心主轴...";
        RefreshPuzzleUI();
        yield return new WaitForSecondsRealtime(0.4f);
        TriggerSolveSuccess();
    }

    public static void OpenOrRun(
        string puzzleId,
        GameState state,
        SaveStore store,
        EggboyPlayer player,
        EggboyUI ui,
        ControlGate gate,
        Action onSolved)
    {
        if (ui == null || !ui.inputClose || state == null || state.flags.Contains("puzzle_" + puzzleId))
        {
            onSolved?.Invoke();
            return;
        }

        if (instance == null)
        {
            var host = new GameObject("WoodenMechanismPuzzleModal");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<WoodenMechanismPuzzleModal>();
        }
        instance.BeginPuzzle(puzzleId, state, store, player, ui, gate, onSolved);
    }

    bool active;
    PuzzleSpec currentSpec;
    GameState gameState;
    SaveStore saveStore;
    EggboyPlayer eggboy;
    EggboyUI eggboyUi;
    ControlGate controlGate;
    object gateToken;
    ChaseCamera chaseCam;
    bool restoreChase;
    Action solvedCallback;

    readonly bool[] latches = new bool[3];
    bool stage1Unlocked;
    readonly int[] rings = new int[3];
    int attemptsLeft = 3;
    string feedbackBanner = "";
    bool puzzleSolved;
    float solveAnimTimer;

    GameObject puzzleRig;
    Transform modelPivot;
    readonly Transform[] latchNodes = new Transform[3];
    readonly Vector3[] latchBasePos = new Vector3[3];
    readonly Transform[] ringNodes = new Transform[3];
    readonly Transform[] gearNodes = new Transform[3];
    Transform coreSpindleNode;
    Vector3 spindleBasePos;

    RectTransform puzzleCard;
    Text titleText, subtitleText, cluesText, stage1HeaderText, stage2HeaderText, feedbackText;
    readonly Text[] latchBtnTexts = new Text[3];
    readonly Text[] ringStatusTexts = new Text[3];

    void BeginPuzzle(
        string puzzleId,
        GameState state,
        SaveStore store,
        EggboyPlayer player,
        EggboyUI ui,
        ControlGate gate,
        Action onSolved)
    {
        if (active) CleanupModal(false);

        if (!Specs.TryGetValue(puzzleId, out currentSpec))
        {
            onSolved?.Invoke();
            return;
        }

        gameState = state;
        saveStore = store;
        eggboy = player;
        eggboyUi = ui;
        controlGate = gate;
        solvedCallback = onSolved;

        latches[0] = false;
        latches[1] = false;
        latches[2] = false;
        stage1Unlocked = false;

        rings[0] = WrapRing(currentSpec.targetRings[0] + 2);
        rings[1] = WrapRing(currentSpec.targetRings[1] + 3);
        rings[2] = WrapRing(currentSpec.targetRings[2] + 4);
        attemptsLeft = 3;
        feedbackBanner = "提示：推错主轴 3 次将触发防盗卡簧惩罚（剩余机会：3 / 3）";
        puzzleSolved = false;
        solveAnimTimer = 0f;

        if (controlGate != null) gateToken = controlGate.Acquire("wooden_puzzle");
        if (eggboy != null) eggboy.locked = true;
        chaseCam = FindObjectOfType<ChaseCamera>();
        restoreChase = chaseCam != null && chaseCam.enabled;
        if (restoreChase) chaseCam.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsurePuzzleUI();
        if (puzzleCard != null) puzzleCard.gameObject.SetActive(true);
        Build3DLuxRig();
        RefreshPuzzleUI();
        active = true;
    }

    static int WrapRing(int v)
    {
        while (v < 1) v += 6;
        while (v > 6) v -= 6;
        return v;
    }

    void EnsurePuzzleUI()
    {
        if (puzzleCard != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(eggboyUi);
        if (root == null) return;

        puzzleCard = TideTownStyleUI.CreateParchmentCard(root, "WoodenMechanismParchmentCard", 590f, 560f, 10);
        puzzleCard.anchorMin = puzzleCard.anchorMax = new Vector2(0f, 0.5f);
        puzzleCard.pivot = new Vector2(0f, 0.5f);
        puzzleCard.anchoredPosition = new Vector2(28f, 8f);

        titleText = TideTownStyleUI.CreateText(
            puzzleCard, "", 22, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 244f), new Vector2(526f, 32f), true);

        subtitleText = TideTownStyleUI.CreateText(
            puzzleCard, "", 13, TideTownStyleUI.InkMuted, TextAnchor.UpperLeft,
            new Vector2(0f, 204f), new Vector2(526f, 40f), false);

        cluesText = TideTownStyleUI.CreateText(
            puzzleCard, "", 13, TideTownStyleUI.InkDark, TextAnchor.UpperLeft,
            new Vector2(0f, 134f), new Vector2(526f, 78f), false);

        stage1HeaderText = TideTownStyleUI.CreateText(
            puzzleCard, "", 15, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 72f), new Vector2(526f, 26f), true);

        string[] latchNames = { "甲栓", "乙栓", "丙栓" };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var btn = TideTownStyleUI.CreateWarmButton(
                puzzleCard,
                latchNames[i],
                new Vector2(-174f + i * 174f, 32f),
                new Vector2(160f, 34f),
                false,
                () => ToggleLatch(idx));
            latchBtnTexts[i] = btn.GetComponentInChildren<Text>();
        }

        stage2HeaderText = TideTownStyleUI.CreateText(
            puzzleCard, "", 15, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, -12f), new Vector2(526f, 26f), true);

        for (int r = 0; r < 3; r++)
        {
            int ringIdx = r;
            float y = -50f - r * 40f;
            ringStatusTexts[r] = TideTownStyleUI.CreateText(
                puzzleCard, "", 14, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
                new Vector2(-96f, y), new Vector2(320f, 28f), true);

            TideTownStyleUI.CreateWarmButton(
                puzzleCard, "逆转 -1", new Vector2(124f, y), new Vector2(88f, 30f), false, () => RotateRing(ringIdx, -1));
            TideTownStyleUI.CreateWarmButton(
                puzzleCard, "顺转 +1", new Vector2(220f, y), new Vector2(88f, 30f), false, () => RotateRing(ringIdx, +1));
        }

        feedbackText = TideTownStyleUI.CreateText(
            puzzleCard, "", 13, TideTownStyleUI.AmberAccent, TextAnchor.MiddleLeft,
            new Vector2(0f, -176f), new Vector2(526f, 28f), true);

        TideTownStyleUI.CreateWarmButton(
            puzzleCard, "推动主轴开锁（空格）", new Vector2(-176f, -226f), new Vector2(182f, 38f), true, AttemptUnlockSpindle);

        TideTownStyleUI.CreateWarmButton(
            puzzleCard, "心愿造物笺 (V / Y)", new Vector2(8f, -226f), new Vector2(172f, 38f), false, AIAgentCalibrationDirector.ToggleConsole);

        TideTownStyleUI.CreateWarmButton(
            puzzleCard, "暂退 / 下塔（Esc）", new Vector2(184f, -226f), new Vector2(164f, 38f), false, () => CleanupModal(false));
    }

    void Build3DLuxRig()
    {
        if (puzzleRig != null) Destroy(puzzleRig);
        var cam = Camera.main;
        if (cam == null || currentSpec == null) return;

        puzzleRig = new GameObject("LuxPuzzlePreviewRig");
        puzzleRig.transform.SetParent(cam.transform, false);
        float dist = 2.25f;
        float halfH = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * dist;
        float offsetX = 0.35f * cam.aspect * halfH;
        puzzleRig.transform.localPosition = new Vector3(offsetX, 0.04f, dist);
        puzzleRig.transform.localRotation = Quaternion.identity;

        var lightGo = new GameObject("PuzzleWarmLight");
        lightGo.transform.SetParent(puzzleRig.transform, false);
        lightGo.transform.localPosition = new Vector3(-0.6f, 0.7f, -0.9f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color32(255, 224, 178, 255);
        l.range = 4.5f;
        l.intensity = 2.4f;

        var pivotGo = new GameObject("Pivot");
        pivotGo.transform.SetParent(puzzleRig.transform, false);
        pivotGo.transform.localRotation = Quaternion.Euler(8f, -12f, 0f);
        modelPivot = pivotGo.transform;

        // 1. 实例化可动多部件 3D 机关主体（Latch_0..2, Ring_0..2, Gear_0..2, Core_Spindle）
        var mechGo = TideTownStyleUI.BuildArticulatedMechanism3D(currentSpec.mechanism3DName, 0.92f);
        mechGo.transform.SetParent(modelPivot, false);
        mechGo.transform.localPosition = new Vector3(0f, 0.04f, 0f);

        for (int i = 0; i < 3; i++)
        {
            latchNodes[i] = TideTownStyleUI.FindDeepChild(mechGo.transform, "Latch_" + i);
            if (latchNodes[i] != null)
            {
                latchBasePos[i] = latchNodes[i].localPosition;
                latchNodes[i].gameObject.name = "MechLatch_" + i;
                var col = latchNodes[i].gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(0.16f, 0.28f, 0.16f);
            }

            ringNodes[i] = TideTownStyleUI.FindDeepChild(mechGo.transform, "Ring_" + i);
            if (ringNodes[i] != null)
            {
                ringNodes[i].gameObject.name = "MechRing_" + i;
                float rSize = (i == 0) ? 0.72f : (i == 1 ? 0.48f : 0.28f);
                var col = ringNodes[i].gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(rSize, rSize, 0.09f);
            }

            gearNodes[i] = TideTownStyleUI.FindDeepChild(mechGo.transform, "Gear_" + i);
        }

        coreSpindleNode = TideTownStyleUI.FindDeepChild(mechGo.transform, "Core_Spindle");
        if (coreSpindleNode != null)
        {
            spindleBasePos = coreSpindleNode.localPosition;
            coreSpindleNode.gameObject.name = "MechSpindle";
            var col = coreSpindleNode.gameObject.AddComponent<SphereCollider>();
            col.radius = 0.095f;
        }

        // 2. 底座托架：搭载站点真实 Lux3D 物件作为机关基座
        var clone = TideTownStyleUI.CloneSceneLux3D(currentSpec.lux3DModelName, 0.46f, false);
        if (clone != null)
        {
            clone.transform.SetParent(modelPivot, false);
            clone.transform.localPosition = new Vector3(0f, -0.52f, -0.05f);
        }
    }

    void RefreshPuzzleUI()
    {
        if (puzzleCard == null || currentSpec == null) return;
        titleText.text = currentSpec.title;
        subtitleText.text = currentSpec.subtitle;

        // 只有真正完成了 3D 旧物背面暗扣拆解（_detail）或小黑嗅探（dog_sniff_）才显示真实刻度数字！
        bool deskShortcut = currentSpec.id == "school_lock" && gameState != null && gameState.seenMemories.Contains("desk_detail");
        string clueLines = "";
        for (int i = 0; i < 3; i++)
        {
            string itemId = currentSpec.clueItemIds[i];
            bool found = deskShortcut || (gameState != null &&
                (gameState.seenMemories.Contains(itemId + "_detail") ||
                 gameState.flags.Contains("dog_sniff_" + itemId)));

            clueLines += (i > 0 ? "\n" : "") + "· " + (found ? currentSpec.clueTexts[i] : currentSpec.lockedCluePrompts[i]);
        }
        cluesText.text = clueLines;

        stage1Unlocked = latches[0] && latches[1] && latches[2];
        stage1HeaderText.text = stage1Unlocked
            ? "第一重：三连榫卯木栓（已全部开槽）"
            : "第一重：三连榫卯木栓";

        string[] lNames = { "甲栓", "乙栓", "丙栓" };
        for (int i = 0; i < 3; i++)
        {
            if (latchBtnTexts[i] != null)
                latchBtnTexts[i].text = lNames[i] + (latches[i] ? " · 已开槽" : " · 锁死");
        }

        stage2HeaderText.text = stage1Unlocked
            ? "第二重：三环啮合齿轮"
            : "第二重：三环啮合齿轮（受上方木栓锁止）";

        string[] rNames = { "外环木轮", "中环木轮", "内环木轮" };
        for (int r = 0; r < 3; r++)
        {
            if (ringStatusTexts[r] != null)
                ringStatusTexts[r].text = rNames[r] + "：当前刻度【 " + rings[r] + " 】";
        }

        if (feedbackText != null)
            feedbackText.text = feedbackBanner;
    }

    void ToggleLatch(int index)
    {
        if (puzzleSolved) return;
        if (index == 0) { latches[0] = !latches[0]; latches[1] = !latches[1]; }
        else if (index == 1) { latches[0] = !latches[0]; latches[1] = !latches[1]; latches[2] = !latches[2]; }
        else if (index == 2) { latches[1] = !latches[1]; latches[2] = !latches[2]; }

        stage1Unlocked = latches[0] && latches[1] && latches[2];
        if (stage1Unlocked)
            feedbackBanner = "咔哒！三根木栓已全部退入槽内。";
        ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.2f);
        RefreshPuzzleUI();
    }

    void RotateRing(int ringIndex, int delta)
    {
        if (puzzleSolved) return;
        if (!stage1Unlocked)
        {
            feedbackBanner = "木轮被上方的三连木栓锁止，无法拨动。";
            ShadowAndChimeSystem.PlayProceduralChime(220f, 0.2f);
            RefreshPuzzleUI();
            return;
        }

        rings[ringIndex] = WrapRing(rings[ringIndex] + delta);
        if (ringIndex == 0) rings[1] = WrapRing(rings[1] + delta);
        else if (ringIndex == 1) rings[2] = WrapRing(rings[2] - delta);

        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.18f);
        RefreshPuzzleUI();
    }

    void AttemptUnlockSpindle()
    {
        if (puzzleSolved || currentSpec == null) return;

        if (!stage1Unlocked)
        {
            feedbackBanner = "第一重「三连木栓」尚未全部解开，主轴无法推动！";
            ShadowAndChimeSystem.PlayProceduralChime(220f, 0.25f);
            RefreshPuzzleUI();
            return;
        }

        int correctCount = 0;
        for (int i = 0; i < 3; i++)
            if (rings[i] == currentSpec.targetRings[i])
                correctCount++;

        if (correctCount == 3)
        {
            TriggerSolveSuccess();
            return;
        }

        attemptsLeft--;
        ShadowAndChimeSystem.PlayProceduralChime(196f, 0.35f);

        if (attemptsLeft <= 0)
        {
            string pid = currentSpec.id;
            CleanupModal(false);
            var dir = FindObjectOfType<StoryDirector>();
            if (dir != null) dir.OnMechanismJamPenalty(pid);
            return;
        }

        latches[1] = false;
        stage1Unlocked = false;
        feedbackBanner = "【卡簧反震】当前仅有 " + correctCount + " / 3 个齿轮刻度吻合！木栓被震回锁止（剩余机会：" + attemptsLeft + " 次）";
        RefreshPuzzleUI();
    }

    void TriggerSolveSuccess()
    {
        if (puzzleSolved) return;
        puzzleSolved = true;
        stage1Unlocked = true;
        latches[0] = latches[1] = latches[2] = true;
        if (currentSpec != null)
        {
            rings[0] = currentSpec.targetRings[0];
            rings[1] = currentSpec.targetRings[1];
            rings[2] = currentSpec.targetRings[2];
        }
        feedbackBanner = "主轴咬合成功！机关已解开！";
        RefreshPuzzleUI();
        solveAnimTimer = 0.65f;
        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.45f);

        if (gameState != null && currentSpec != null)
        {
            string flag = "puzzle_" + currentSpec.id;
            if (!gameState.flags.Contains(flag)) gameState.flags.Add(flag);
            if (saveStore != null) saveStore.Save(gameState);

            if (currentSpec.id == "bakery_lock")
                TideTownAchievementSystem.Unlock("luban_oven");
            else if (currentSpec.id == "school_lock")
                TideTownAchievementSystem.Unlock("classic_ball");
        }
        if (eggboy != null) eggboy.PlayEmote("hop");
        if (eggboyUi != null && currentSpec != null)
            eggboyUi.ShowToast(currentSpec.successToast, 3.8f);
    }

    void Update()
    {
        if (!active) return;

        float dt = Time.unscaledDeltaTime;

        // 1. 支持鼠标直接点击右侧 3D 机关上的木栓（MechLatch_0..2）、齿轮环（MechRing_0..2）与主轴（MechSpindle）
        if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && Camera.main != null && Input.mousePosition.x > Screen.width * 0.42f)
        {
            int delta = Input.GetMouseButtonDown(1) ? -1 : +1;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 12f);
            float bestDist = float.MaxValue;
            Collider bestCol = null;
            for (int h = 0; h < hits.Length; h++)
            {
                var c = hits[h].collider;
                if (c != null && c.name.StartsWith("Mech") && hits[h].distance < bestDist)
                {
                    bestDist = hits[h].distance;
                    bestCol = c;
                }
            }

            if (bestCol != null)
            {
                string n = bestCol.name;
                if (n.StartsWith("MechLatch_") && int.TryParse(n.Substring("MechLatch_".Length), out int lIdx))
                {
                    ToggleLatch(lIdx);
                }
                else if (n.StartsWith("MechRing_") && int.TryParse(n.Substring("MechRing_".Length), out int rIdx))
                {
                    RotateRing(rIdx, delta);
                }
                else if (n == "MechSpindle")
                {
                    AttemptUnlockSpindle();
                }
            }
        }

        // 2. 实时驱动 3D 机关各部件的物理位移与啮合旋转
        for (int i = 0; i < 3; i++)
        {
            if (latchNodes[i] != null)
            {
                Vector3 targetPos = latchBasePos[i] + new Vector3(0f, latches[i] ? 0.13f : 0f, 0f);
                Quaternion targetRot = Quaternion.Euler(0f, latches[i] ? 45f : 0f, 0f);
                latchNodes[i].localPosition = Vector3.Lerp(latchNodes[i].localPosition, targetPos, dt * 11f);
                latchNodes[i].localRotation = Quaternion.Slerp(latchNodes[i].localRotation, targetRot, dt * 11f);
            }

            if (ringNodes[i] != null)
            {
                float zAng = -rings[i] * 60f;
                ringNodes[i].localRotation = Quaternion.Slerp(
                    ringNodes[i].localRotation,
                    Quaternion.Euler(0f, 0f, zAng),
                    dt * 9f);
            }

            if (gearNodes[i] != null)
            {
                float gAng = (rings[0] * 45f - rings[1] * 45f) * (i % 2 == 0 ? 1f : -1f);
                gearNodes[i].localRotation = Quaternion.Slerp(
                    gearNodes[i].localRotation,
                    Quaternion.Euler(0f, 0f, gAng),
                    dt * 9f);
            }
        }

        if (coreSpindleNode != null)
        {
            Vector3 targetSpindlePos = spindleBasePos + new Vector3(0f, 0f, puzzleSolved ? -0.09f : 0f);
            Quaternion targetSpindleRot = Quaternion.Euler(0f, 0f, puzzleSolved ? 180f : (stage1Unlocked ? 30f : 0f));
            coreSpindleNode.localPosition = Vector3.Lerp(coreSpindleNode.localPosition, targetSpindlePos, dt * 10f);
            coreSpindleNode.localRotation = Quaternion.Slerp(coreSpindleNode.localRotation, targetSpindleRot, dt * 10f);
        }

        if (modelPivot != null)
        {
            float gentleSway = Mathf.Sin(Time.unscaledTime * 1.4f) * 8f;
            modelPivot.localRotation = Quaternion.Slerp(
                modelPivot.localRotation,
                Quaternion.Euler(8f, -12f + gentleSway, 0f),
                dt * 5f);
        }

        if (puzzleSolved)
        {
            solveAnimTimer -= dt;
            if (solveAnimTimer <= 0f)
            {
                var cb = solvedCallback;
                CleanupModal(true);
                cb?.Invoke();
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            AttemptUnlockSpindle();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            CleanupModal(false);
        }
    }

    void CleanupModal(bool solved)
    {
        active = false;
        if (puzzleRig != null) Destroy(puzzleRig);
        puzzleRig = null;
        modelPivot = null;
        for (int i = 0; i < 3; i++)
        {
            latchNodes[i] = null;
            ringNodes[i] = null;
            gearNodes[i] = null;
        }
        coreSpindleNode = null;
        if (puzzleCard != null) puzzleCard.gameObject.SetActive(false);

        if (restoreChase && chaseCam != null) chaseCam.enabled = true;
        restoreChase = false;

        if (controlGate != null && gateToken != null)
        {
            controlGate.Release(gateToken);
            gateToken = null;
        }
        if (eggboy != null) eggboy.locked = false;
        if (!solved) solvedCallback = null;
    }
}
