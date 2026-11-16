// 方向 A：三大站点核心联动机关（榫卯三连滑栓 + 三环齿轮咬合刻度 + 旧物暗扣线索链 + 试错反震惩罚）。
// 2026-11-11 WP-A：操作改成直接摆弄 3D 部件，界面只剩左下角一张邮差笔记（见下方说明）。
// 彻底移除“直接解开机关”作弊按钮与未检视直接剧透答案：
//   1. 玩家必须先在院子/塔顶翻转 3D 旧物并拆解背面暗扣（或让小黑嗅探）以获知三环密码；
//   2. 必须先解开第一重「三连榫卯木栓」，才能拨动第二重「三环咬合木轮」；
//   3. 拨好三环后点击【推动主轴开锁】：若连续 3 次推错刻度，触发防盗卡簧反震惩罚（吹灭火柴或弹飞红球，必须重拿）！
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

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

    // ===================================================================================
    // 2026-11-11 WP-A 改版：去掉左边那张写满说明、十几个按钮的大卡片。
    //   · 机关摆在画面正中，直接操作 3D 部件：点木栓、拖圆环（绕圆心转，每 60° 咔一格）、点主轴推进去。
    //   · 刻度数字刻在环上，顶上一枚黄铜指针，指着的就是当前刻度；不再有"当前刻度【3】"这种文字。
    //   · 左下角只剩一张小小的"邮差笔记"：找到的刻度写成"菜谱 · 外环 3"，没找到的只写纹样。
    //   · 推错了：整座机关一震、木栓弹回；第三次反震照旧（火柴被吹灭等）。
    //   · 键盘：↑↓ 选环、←→ 转、1/2/3 拨木栓、空格推主轴、Esc 退出。
    // ===================================================================================

    // 某件线索旧物对应哪一环、刻度几（小黑嗅探、浅滩木匣用）
    public static bool ClueInfo(string itemId, out int ring, out int number)
    {
        foreach (var spec in Specs.Values)
            for (int i = 0; i < spec.clueItemIds.Length; i++)
                if (spec.clueItemIds[i] == itemId) { ring = i; number = spec.targetRings[i]; return true; }
        ring = -1; number = 0;
        return false;
    }

    public static bool IsOpen => instance != null && instance.active;
    public static int ActiveRingSelection { get; set; } = 0;
    public static string OpenPuzzleId => IsOpen && instance.currentSpec != null ? instance.currentSpec.id : "";

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
        instance.RefreshPuzzleUI();
    }

    // "鲁班巧思"：只有卡住提示到了最后一层才给（见 HintUnlockReady），一步步替你拨好，不给成就
    public static void ExternalStartAICalibration()
    {
        if (!IsOpen || instance.puzzleSolved) return;
        instance.StopAllCoroutines();
        instance.assisted = true;
        instance.StartCoroutine(instance.AICalibrateStepByStepRoutine());
    }

    System.Collections.IEnumerator AICalibrateStepByStepRoutine()
    {
        if (currentSpec == null) yield break;
        yield return new WaitForSecondsRealtime(0.3f);
        foreach (int li in LatchSolution()) { ToggleLatch(li); yield return new WaitForSecondsRealtime(0.3f); }
        yield return new WaitForSecondsRealtime(0.2f);
        for (int r = 0; r < 3; r++)
        {
            int guard = 0;
            while (rings[r] != currentSpec.targetRings[r] && guard++ < 8)
            {
                RotateRing(r, 1);
                yield return new WaitForSecondsRealtime(0.28f);
            }
        }
        yield return new WaitForSecondsRealtime(0.35f);
        AttemptUnlockSpindle();
    }

    // ---- 审计用 ----
    public static bool DebugOpen(string puzzleId, GameState state, SaveStore store, EggboyPlayer player, EggboyUI ui, ControlGate gate, Action onSolved)
    {
        if (instance == null)
        {
            var host = new GameObject("WoodenMechanismPuzzleModal");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<WoodenMechanismPuzzleModal>();
        }
        instance.BeginPuzzle(puzzleId, state, store, player, ui, gate, onSolved);
        return instance.active;
    }
    // 从现在的三根栓状态出发，要拨哪几根才能全开（三种拨法在 GF(2) 上张成全空间，任何状态都有解）
    List<int> LatchSolution()
    {
        var sol = new List<int>();
        if (latches[0] && latches[1] && latches[2]) return sol;
        for (int mask = 1; mask < 8; mask++)
        {
            bool a = latches[0], b = latches[1], c = latches[2];
            if ((mask & 1) != 0) { a = !a; b = !b; }
            if ((mask & 2) != 0) { a = !a; b = !b; c = !c; }
            if ((mask & 4) != 0) { b = !b; c = !c; }
            if (a && b && c)
            {
                for (int i = 0; i < 3; i++) if ((mask & (1 << i)) != 0) sol.Add(i);
                return sol;
            }
        }
        return sol;
    }
    public static void DebugOpenAllLatches() { if (IsOpen) foreach (int i in instance.LatchSolution()) instance.ToggleLatch(i); }
    public static int DebugRing(int i) => IsOpen ? instance.rings[i] : -1;
    public static bool DebugStage1 => IsOpen && instance.stage1Unlocked;
    public static int DebugAttemptsLeft => IsOpen ? instance.attemptsLeft : -1;
    public static bool DebugSolved => instance != null && instance.puzzleSolved;
    public static void DebugClose() { if (IsOpen) instance.CleanupModal(false); }
    // 每个环上，现在离指针最近的那个数字（看画面读出来的刻度）
    public static int DebugNumberUnderPointer(int ringIndex) => IsOpen ? instance.NumberUnderPointer(ringIndex) : -1;
    // 模拟拖动：把屏幕上的一段弧拖过去
    public static void DebugDragRing(int ringIndex, float degrees) { if (IsOpen) instance.ApplyRingDrag(ringIndex, degrees); }

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
    bool puzzleSolved, assisted;
    float solveAnimTimer, openedAt, shakeTimer, spindlePush;
    int selectedRing;
    bool assistShown;
    public const float AssistSeconds = 120f;
    bool AssistReady => active && !puzzleSolved && Time.unscaledTime - openedAt > AssistSeconds;

    GameObject puzzleRig;
    Transform modelPivot;
    readonly Transform[] latchNodes = new Transform[3];
    readonly Vector3[] latchBasePos = new Vector3[3];
    readonly Vector3[] latchBaseScale = new Vector3[3];
    readonly Transform[] ringNodes = new Transform[3];
    readonly Transform[] gearNodes = new Transform[3];
    readonly List<TextMesh>[] ringNumbers = { new List<TextMesh>(), new List<TextMesh>(), new List<TextMesh>() };
    readonly float[] ringVisualAngle = new float[3];
    Transform coreSpindleNode, pointerNode;
    Vector3 spindleBasePos;

    // 拖环
    int dragRing = -1;
    float dragLastAngle, dragAccum;
    bool dragMoved;
    Vector2 dragStartMouse;

    RectTransform noteCard;
    Text noteTitle, noteBody;

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
        if (!Specs.TryGetValue(puzzleId, out currentSpec)) { onSolved?.Invoke(); return; }

        gameState = state; saveStore = store; eggboy = player; eggboyUi = ui; controlGate = gate; solvedCallback = onSolved;
        latches[0] = latches[1] = latches[2] = false;
        stage1Unlocked = false;
        rings[0] = WrapRing(currentSpec.targetRings[0] + 2);
        rings[1] = WrapRing(currentSpec.targetRings[1] + 3);
        rings[2] = WrapRing(currentSpec.targetRings[2] + 4);
        attemptsLeft = 3;
        puzzleSolved = false; assisted = false;
        solveAnimTimer = 0f; shakeTimer = 0f; spindlePush = 0f;
        selectedRing = 0; dragRing = -1;
        openedAt = Time.unscaledTime;

        if (controlGate != null) gateToken = controlGate.Acquire("wooden_puzzle");
        if (eggboy != null) eggboy.locked = true;
        chaseCam = FindObjectOfType<ChaseCamera>();
        restoreChase = chaseCam != null && chaseCam.enabled;
        if (restoreChase) chaseCam.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        MenuController.NoteScriptUnlock();

        EnsurePuzzleUI();
        if (noteCard != null) noteCard.gameObject.SetActive(true);
        Build3DLuxRig();
        for (int i = 0; i < 3; i++) ringVisualAngle[i] = -rings[i] * 60f;
        RefreshPuzzleUI();
        active = true;
    }

    static int WrapRing(int v)
    {
        while (v < 1) v += 6;
        while (v > 6) v -= 6;
        return v;
    }

    // 左下角的邮差笔记：只写找到了什么
    void EnsurePuzzleUI()
    {
        if (noteCard != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(eggboyUi);
        if (root == null) return;
        noteCard = TideTownStyleUI.CreateParchmentCard(root, "WoodenMechanismNote", 330f, 150f, 4);
        noteCard.anchorMin = noteCard.anchorMax = new Vector2(0f, 0f);
        noteCard.pivot = new Vector2(0f, 0f);
        noteCard.anchoredPosition = new Vector2(28f, 96f);
        noteTitle = TideTownStyleUI.CreateText(noteCard, "", 15, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 52f), new Vector2(296f, 24f), true);
        noteBody = TideTownStyleUI.CreateText(noteCard, "", 14, TideTownStyleUI.InkDark, TextAnchor.UpperLeft,
            new Vector2(0f, -12f), new Vector2(296f, 90f), false);
    }

    void Build3DLuxRig()
    {
        if (puzzleRig != null) Destroy(puzzleRig);
        var cam = Camera.main;
        if (cam == null || currentSpec == null) return;

        puzzleRig = new GameObject("LuxPuzzlePreviewRig");
        puzzleRig.transform.SetParent(cam.transform, false);
        puzzleRig.transform.localPosition = new Vector3(0.05f, 0.02f, 1.55f);
        puzzleRig.transform.localRotation = Quaternion.identity;

        var lightGo = new GameObject("PuzzleWarmLight");
        lightGo.transform.SetParent(puzzleRig.transform, false);
        lightGo.transform.localPosition = new Vector3(-0.5f, 0.6f, -0.8f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color32(255, 224, 178, 255);
        l.range = 4f;
        l.intensity = 1.7f;

        var pivotGo = new GameObject("Pivot");
        pivotGo.transform.SetParent(puzzleRig.transform, false);
        // 机关正面朝向镜头（圆环在本地 +Z 那面），稍微俯一点
        pivotGo.transform.localRotation = Quaternion.Euler(6f, 180f, 0f);
        modelPivot = pivotGo.transform;

        var mechGo = TideTownStyleUI.BuildArticulatedMechanism3D(currentSpec.mechanism3DName, 0.92f);
        mechGo.transform.SetParent(modelPivot, false);
        mechGo.transform.localPosition = Vector3.zero;

        for (int i = 0; i < 3; i++)
        {
            latchNodes[i] = TideTownStyleUI.FindDeepChild(mechGo.transform, "Latch_" + i);
            if (latchNodes[i] != null)
            {
                latchBasePos[i] = latchNodes[i].localPosition;
                latchBaseScale[i] = latchNodes[i].localScale;
                latchNodes[i].gameObject.name = "MechLatch_" + i;
                AddFittedBox(latchNodes[i], 1.6f);
            }
            ringNodes[i] = TideTownStyleUI.FindDeepChild(mechGo.transform, "Ring_" + i);
            if (ringNodes[i] != null)
            {
                ringNodes[i].gameObject.name = "MechRing_" + i;
                AddFittedBox(ringNodes[i], 1f);
            }
            gearNodes[i] = TideTownStyleUI.FindDeepChild(mechGo.transform, "Gear_" + i);
        }
        coreSpindleNode = TideTownStyleUI.FindDeepChild(mechGo.transform, "Core_Spindle");
        if (coreSpindleNode != null)
        {
            spindleBasePos = coreSpindleNode.localPosition;
            coreSpindleNode.gameObject.name = "MechSpindle";
            AddFittedBox(coreSpindleNode, 1.8f);
        }
        BuildRingNumbers();
        BuildPointer();
        SetupOverlayCamera(cam);
    }

    // 木匣画在整个场景最上面：镜头和木匣之间有房子、柱子也挡不住（OverlayRender 叠加相机）
    bool overlayHeld;
    void SetupOverlayCamera(Camera cam)
    {
        OverlayRender.SetLayerRecursive(puzzleRig, OverlayRender.Layer);
        OverlayRender.LimitLights(puzzleRig);
        OverlayRender.Acquire(cam);
        overlayHeld = true;
    }

    void TeardownOverlayCamera()
    {
        if (!overlayHeld) return;
        overlayHeld = false;
        OverlayRender.Release();
    }
    // 碰撞盒按部件自己的网格大小来（GLB 版和程序版尺寸不同）
    static void AddFittedBox(Transform t, float grow)
    {
        var mf = t.GetComponent<MeshFilter>() ?? t.GetComponentInChildren<MeshFilter>();
        var box = t.gameObject.AddComponent<BoxCollider>();
        if (mf != null && mf.sharedMesh != null)
        {
            var b = mf.sharedMesh.bounds;
            if (mf.transform != t)
            {
                // 子节点的网格：换算到 t 的本地空间
                Vector3 c = t.InverseTransformPoint(mf.transform.TransformPoint(b.center));
                Vector3 s = Vector3.Scale(b.size, mf.transform.lossyScale);
                s = new Vector3(s.x / Mathf.Max(1e-5f, t.lossyScale.x), s.y / Mathf.Max(1e-5f, t.lossyScale.y), s.z / Mathf.Max(1e-5f, t.lossyScale.z));
                box.center = c; box.size = s * grow;
            }
            else { box.center = b.center; box.size = b.size * grow; }
        }
    }

    // 环面（本地 XY 平面）上的半径和朝镜头那一面的 z
    static void RingGeometry(Transform ring, out float radius, out float frontZ)
    {
        radius = 0.2f; frontZ = 0f;
        var mf = ring.GetComponent<MeshFilter>() ?? ring.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var b = mf.sharedMesh.bounds;
        if (mf.transform != ring)
        {
            float k = mf.transform.lossyScale.x / Mathf.Max(1e-5f, ring.lossyScale.x);
            radius = Mathf.Max(b.extents.x, b.extents.y) * k;
            frontZ = ring.InverseTransformPoint(mf.transform.TransformPoint(b.center)).z + b.extents.z * k;
        }
        else
        {
            radius = Mathf.Max(b.extents.x, b.extents.y);
            frontZ = b.max.z;
        }
    }

    static Font numberFont;
    void BuildRingNumbers()
    {
        if (numberFont == null) numberFont = Resources.Load<Font>("Fonts/NotoSansSC-VF");
        if (numberFont == null) numberFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Color ink = currentSpec.id == "lighthouse_lock" ? new Color(0.22f, 0.12f, 0.04f) : new Color(0.98f, 0.93f, 0.82f);
        for (int r = 0; r < 3; r++)
        {
            ringNumbers[r].Clear();
            var ring = ringNodes[r];
            if (ring == null) continue;
            RingGeometry(ring, out float radius, out float frontZ);
            // 三个环叠在一起：数字写在各自那一圈露出来的地方
            float rr = radius * (r == 0 ? 0.86f : r == 1 ? 0.8f : 0.72f);
            for (int k = 1; k <= 6; k++)
            {
                var go = new GameObject("RingNumber_" + r + "_" + k);
                go.transform.SetParent(ring, false);
                // 环转到 rings[r] == k 时（本地 z 旋转 -k*60°），这个数字正好转到最上面
                float phi = (90f + 60f * k) * Mathf.Deg2Rad;
                go.transform.localPosition = new Vector3(Mathf.Cos(phi) * rr, Mathf.Sin(phi) * rr, frontZ + 0.004f);
                // 文字朝镜头（本地 -Z 是背面，所以绕 Y 转 180°），字头朝外
                // 字面朝镜头（镜头在环的 +Z 那一侧，TextMesh 从自己的 -Z 方向看才是正的），字头朝外
                go.transform.localRotation = Quaternion.LookRotation(Vector3.back, new Vector3(Mathf.Cos(phi), Mathf.Sin(phi), 0f));
                var tm = go.AddComponent<TextMesh>();
                tm.font = numberFont;
                tm.text = k.ToString();
                tm.fontSize = 64;
                tm.characterSize = radius * (r == 2 ? 0.05f : 0.035f);
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = ink;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = numberFont.material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ringNumbers[r].Add(tm);
            }
        }
    }

    // 顶上一枚黄铜指针：指着的数字就是现在的刻度
    void BuildPointer()
    {
        var ring0 = ringNodes[0];
        if (ring0 == null) return;
        RingGeometry(ring0, out float radius, out float frontZ);
        var go = new GameObject("RingPointer");
        go.transform.SetParent(ring0.parent, false);
        go.transform.localPosition = ring0.localPosition + new Vector3(0f, radius * 1.12f * ring0.localScale.y, frontZ * ring0.localScale.z + 0.01f);
        go.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        var tm = go.AddComponent<TextMesh>();
        tm.font = numberFont;
        tm.text = "▼";
        tm.fontSize = 64;
        tm.characterSize = radius * 0.05f * ring0.localScale.x;
        tm.anchor = TextAnchor.LowerCenter;
        tm.color = new Color(0.86f, 0.62f, 0.22f);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = numberFont.material;
        pointerNode = go.transform;
    }

    int NumberUnderPointer(int r)
    {
        var cam = Camera.main;
        if (cam == null || ringNumbers[r].Count == 0) return -1;
        int best = -1; float bestY = float.MinValue;
        foreach (var tm in ringNumbers[r])
        {
            float y = cam.transform.InverseTransformPoint(tm.transform.position).y;
            if (y > bestY) { bestY = y; best = int.Parse(tm.text); }
        }
        return best;
    }

    void RefreshPuzzleUI()
    {
        if (noteCard == null || currentSpec == null) return;
        string dots = "";
        for (int i = 0; i < 3; i++) dots += i < attemptsLeft ? "●" : "○";
        noteTitle.text = "邮差笔记　" + dots + (AssistReady ? "　·　按 H 搭把手" : "");
        bool deskShortcut = currentSpec.id == "school_lock" && gameState != null && gameState.seenMemories.Contains("desk_detail");
        string[] ringNames = { "外环", "中环", "内环" };
        string body = "";
        for (int i = 0; i < 3; i++)
        {
            string itemId = currentSpec.clueItemIds[i];
            bool found = deskShortcut || (gameState != null &&
                (gameState.seenMemories.Contains(itemId + "_detail") || gameState.flags.Contains("dog_sniff_" + itemId)));
            body += (i > 0 ? "\n" : "") + (found
                ? currentSpec.clueItemNames[i] + " · " + ringNames[i] + " " + currentSpec.targetRings[i]
                : ringNames[i] + " · " + Glyph(currentSpec.lockedCluePrompts[i]) + " · ？");
        }
        noteBody.text = body;
    }

    // "外环轮缘：镌刻着一枚「翻开的书页」纹样" → "翻开的书页"
    static string Glyph(string s)
    {
        int a = s.IndexOf('「'), b = s.IndexOf('」');
        return a >= 0 && b > a ? s.Substring(a + 1, b - a - 1) : s;
    }

    void ToggleLatch(int index)
    {
        if (puzzleSolved) return;
        if (index == 0) { latches[0] = !latches[0]; latches[1] = !latches[1]; }
        else if (index == 1) { latches[0] = !latches[0]; latches[1] = !latches[1]; latches[2] = !latches[2]; }
        else if (index == 2) { latches[1] = !latches[1]; latches[2] = !latches[2]; }
        bool was = stage1Unlocked;
        stage1Unlocked = latches[0] && latches[1] && latches[2];
        ShadowAndChimeSystem.PlayProceduralChime(stage1Unlocked && !was ? 880f : 587.33f, 0.2f);
        RefreshPuzzleUI();
    }

    void RotateRing(int ringIndex, int delta)
    {
        if (puzzleSolved) return;
        if (!stage1Unlocked)
        {
            // 被木栓卡着：环晃一下就回来
            shakeTimer = Mathf.Max(shakeTimer, 0.18f);
            ShadowAndChimeSystem.PlayProceduralChime(220f, 0.15f);
            return;
        }
        rings[ringIndex] = WrapRing(rings[ringIndex] + delta);
        if (ringIndex == 0) rings[1] = WrapRing(rings[1] + delta);
        else if (ringIndex == 1) rings[2] = WrapRing(rings[2] - delta);
        ShadowAndChimeSystem.PlayProceduralChime(659.25f + ringIndex * 60f, 0.12f);
    }

    // 拖动累计的角度，每满 60° 转一格（顺时针为正）
    void ApplyRingDrag(int ringIndex, float degrees)
    {
        dragAccum += degrees;
        while (dragAccum >= 30f) { RotateRing(ringIndex, +1); dragAccum -= 60f; }
        while (dragAccum <= -30f) { RotateRing(ringIndex, -1); dragAccum += 60f; }
    }

    void AttemptUnlockSpindle()
    {
        if (puzzleSolved || currentSpec == null) return;
        spindlePush = 1f;
        if (!stage1Unlocked)
        {
            shakeTimer = 0.25f;
            ShadowAndChimeSystem.PlayProceduralChime(220f, 0.25f);
            return;
        }
        int correct = 0;
        for (int i = 0; i < 3; i++) if (rings[i] == currentSpec.targetRings[i]) correct++;
        if (correct == 3) { TriggerSolveSuccess(); return; }

        attemptsLeft--;
        shakeTimer = 0.55f;
        ShadowAndChimeSystem.PlayProceduralChime(196f, 0.35f);
        ShadowAndChimeSystem.PlayProceduralChime(147f, 0.4f);
        if (attemptsLeft <= 0)
        {
            string pid = currentSpec.id;
            CleanupModal(false);
            var dir = FindObjectOfType<StoryDirector>();
            if (dir != null) dir.OnMechanismJamPenalty(pid);
            return;
        }
        // 反震：中间那根木栓弹回去
        latches[1] = false;
        stage1Unlocked = false;
        RefreshPuzzleUI();
    }

    void TriggerSolveSuccess()
    {
        if (puzzleSolved) return;
        puzzleSolved = true;
        stage1Unlocked = true;
        latches[0] = latches[1] = latches[2] = true;
        solveAnimTimer = 1.1f;
        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.45f);
        ShadowAndChimeSystem.PlayProceduralChime(1174.66f, 0.6f);
        if (gameState != null && currentSpec != null)
        {
            string flag = "puzzle_" + currentSpec.id;
            if (!gameState.flags.Contains(flag)) gameState.flags.Add(flag);
            if (saveStore != null) saveStore.Save(gameState);
            if (!assisted)
            {
                if (currentSpec.id == "bakery_lock") TideTownAchievementSystem.Unlock("luban_oven");
                else if (currentSpec.id == "school_lock") TideTownAchievementSystem.Unlock("classic_ball");
            }
        }
        if (eggboy != null) eggboy.PlayEmote("hop");
        RefreshPuzzleUI();
    }

    int RingUnderMouse(out string latchOrSpindle)
    {
        latchOrSpindle = null;
        var cam = Camera.main;
        if (cam == null) return -1;
        var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 6f, ~0, QueryTriggerInteraction.Collide);
        float best = float.MaxValue; Collider col = null;
        foreach (var h in hits)
            if (h.collider != null && h.collider.name.StartsWith("Mech") && h.distance < best) { best = h.distance; col = h.collider; }
        if (col == null) return -1;
        string n = col.name;
        if (n.StartsWith("MechRing_") && int.TryParse(n.Substring(9), out int r))
        {
            // 环叠在一起：按鼠标离圆心多远决定是哪一圈（射线对薄薄的环不可靠）
            return RingByRadius(r);
        }
        latchOrSpindle = n;
        return -1;
    }

    int RingByRadius(int fallback)
    {
        var cam = Camera.main;
        if (cam == null || ringNodes[0] == null) return fallback;
        Vector2 c = cam.WorldToScreenPoint(ringNodes[0].position);
        float d = Vector2.Distance(Input.mousePosition, c);
        float[] edge = new float[3];
        for (int i = 0; i < 3; i++)
        {
            if (ringNodes[i] == null) return fallback;
            RingGeometry(ringNodes[i], out float rad, out _);
            Vector3 rim = ringNodes[i].TransformPoint(new Vector3(rad, 0f, 0f));
            edge[i] = Vector2.Distance(cam.WorldToScreenPoint(rim), c);
        }
        if (d <= edge[2] * 1.05f) return 2;
        if (d <= edge[1] * 1.03f) return 1;
        return 0;
    }

    float MouseAngleAroundRings()
    {
        var cam = Camera.main;
        Vector2 c = cam != null && ringNodes[0] != null ? (Vector2)cam.WorldToScreenPoint(ringNodes[0].position) : new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 v = (Vector2)Input.mousePosition - c;
        return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    }

    void Update()
    {
        if (!active) return;
        float dt = Time.unscaledDeltaTime;

        if (!puzzleSolved)
        {
            // 鼠标：按下时记住是哪个部件；拖环转、点木栓 / 主轴
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                int r = RingUnderMouse(out string other);
                dragStartMouse = Input.mousePosition;
                dragMoved = false;
                if (r >= 0)
                {
                    dragRing = r; selectedRing = r; ActiveRingSelection = r;
                    dragLastAngle = MouseAngleAroundRings();
                    dragAccum = 0f;
                }
                else if (other != null && other.StartsWith("MechLatch_") && int.TryParse(other.Substring(10), out int li)) ToggleLatch(li);
                else if (other == "MechSpindle") AttemptUnlockSpindle();
            }
            if (dragRing >= 0 && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
            {
                float a = MouseAngleAroundRings();
                float d = Mathf.DeltaAngle(dragLastAngle, a);
                dragLastAngle = a;
                if (Vector2.Distance(Input.mousePosition, dragStartMouse) > 6f) dragMoved = true;
                // 镜头从环的正面看：刻度变大时环在屏幕上逆时针转，和鼠标角度增大的方向一致
                if (dragMoved) ApplyRingDrag(dragRing, d);
            }
            if (dragRing >= 0 && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)))
            {
                // 没拖动只点了一下：左键 +1、右键 -1
                if (!dragMoved) RotateRing(dragRing, Input.GetMouseButtonUp(1) ? -1 : +1);
                dragRing = -1;
            }
            // 滚轮转鼠标下面那一圈
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.01f)
            {
                int r = RingUnderMouse(out _);
                if (r >= 0) { selectedRing = r; RotateRing(r, wheel > 0 ? -1 : +1); }
            }

            // 键盘
            if (Input.GetKeyDown(KeyCode.UpArrow)) selectedRing = Mathf.Max(0, selectedRing - 1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) selectedRing = Mathf.Min(2, selectedRing + 1);
            ActiveRingSelection = selectedRing;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) RotateRing(selectedRing, -1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) RotateRing(selectedRing, +1);
            if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleLatch(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ToggleLatch(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleLatch(2);
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) AttemptUnlockSpindle();
            // 卡了两分钟：笔记上多一行"H 鲁班巧思"，按 H 一步步替你拨好（不给成就）
            if (AssistReady != assistShown) { assistShown = AssistReady; RefreshPuzzleUI(); }
            if (AssistReady && Input.GetKeyDown(KeyCode.H)) ExternalStartAICalibration();
        }

        AnimateParts(dt);

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
        else if (Input.GetKeyDown(KeyCode.Escape)) CleanupModal(false);
    }

    void AnimateParts(float dt)
    {
        // 卡住一分钟还没拨开木栓：中间那根轻轻一跳一跳（不写字）
        bool nudge = !stage1Unlocked && !puzzleSolved && Time.unscaledTime - openedAt > 60f;
        for (int i = 0; i < 3; i++)
        {
            if (latchNodes[i] != null)
            {
                float lift = latches[i] ? 0.12f : 0f;
                if (nudge && i == 1) lift += Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 6f)) * 0.025f;
                latchNodes[i].localPosition = Vector3.Lerp(latchNodes[i].localPosition, latchBasePos[i] + new Vector3(0f, lift, 0f), dt * 12f);
                latchNodes[i].localRotation = Quaternion.Slerp(latchNodes[i].localRotation, Quaternion.Euler(0f, latches[i] ? 45f : 0f, 0f), dt * 12f);
            }
            if (ringNodes[i] != null)
            {
                // 走最短的那条路转过去，别绕一大圈
                float target = -rings[i] * 60f;
                float diff = Mathf.DeltaAngle(ringVisualAngle[i], target);
                ringVisualAngle[i] += diff * Mathf.Min(1f, dt * 12f);
                ringNodes[i].localRotation = Quaternion.Euler(0f, 0f, ringVisualAngle[i]);
            }
            if (gearNodes[i] != null)
            {
                float gAng = (ringVisualAngle[0] - ringVisualAngle[1]) * 0.75f * (i % 2 == 0 ? 1f : -1f);
                gearNodes[i].localRotation = Quaternion.Euler(0f, 0f, gAng);
            }
        }
        if (coreSpindleNode != null)
        {
            spindlePush = Mathf.MoveTowards(spindlePush, 0f, dt * 3f);
            float push = puzzleSolved ? 0.09f : spindlePush * 0.035f;
            coreSpindleNode.localPosition = Vector3.Lerp(coreSpindleNode.localPosition, spindleBasePos + new Vector3(0f, 0f, -push), dt * 14f);
            coreSpindleNode.localRotation = Quaternion.Slerp(coreSpindleNode.localRotation,
                Quaternion.Euler(0f, 0f, puzzleSolved ? 180f : (stage1Unlocked ? 30f : 0f)), dt * 10f);
        }
        if (modelPivot != null)
        {
            shakeTimer = Mathf.Max(0f, shakeTimer - dt);
            float shake = shakeTimer > 0f ? Mathf.Sin(Time.unscaledTime * 70f) * 3.5f * Mathf.Clamp01(shakeTimer / 0.3f) : 0f;
            float open = puzzleSolved ? Mathf.Clamp01(1f - solveAnimTimer / 1.1f) : 0f;
            modelPivot.localRotation = Quaternion.Euler(6f - open * 10f, 180f + shake, shake * 0.4f);
            modelPivot.localPosition = new Vector3(0f, open * 0.05f, 0f);
        }
    }

    void CleanupModal(bool solved)
    {
        active = false;
        dragRing = -1;
        StopAllCoroutines();
        TeardownOverlayCamera();
        if (puzzleRig != null) Destroy(puzzleRig);
        puzzleRig = null;
        modelPivot = null;
        pointerNode = null;
        for (int i = 0; i < 3; i++) { latchNodes[i] = null; ringNodes[i] = null; gearNodes[i] = null; ringNumbers[i].Clear(); }
        coreSpindleNode = null;
        if (noteCard != null) noteCard.gameObject.SetActive(false);
        if (restoreChase && chaseCam != null) chaseCam.enabled = true;
        restoreChase = false;
        if (controlGate != null && gateToken != null) { controlGate.Release(gateToken); gateToken = null; }
        if (eggboy != null) eggboy.locked = false;
        if (!solved) solvedCallback = null;
    }
}
