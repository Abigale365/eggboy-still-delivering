// P0-1 & 方向 A：3D 旧物翻转寻迹与 3D 表面暗扣拆解系统（ObjectInspectModal）。
// 彻底移除“随便转一下就自动解锁”的伪交互：
//   第一步：玩家必须旋转右侧 3D 物件，真正找到藏在背面/底部的【暗扣视角】（视角对准度达到 80%+ 暗扣才会显露）；
//   第二步：直接用鼠标点击 3D 模型表面上的三枚黄铜暗扣（或按 1/2/3），按正确咬合顺序依次按下暗扣，若按错顺序卡簧会全部弹回重置！
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ObjectInspectModal : MonoBehaviour
{
    public sealed class InspectMeta
    {
        public string id;
        public string code;
        public string sceneName;
        public Vector3 sweetEuler;
        public int[] pinOrder; // 必须按此顺序点击 3D 模型上的三枚暗扣（0=左小扣, 1=中扣, 2=右大扣）
        public string pinOrderHint;
        public string guide;
        public string hiddenTitle;
        public string hiddenText;
        public string cipherClueBadge;
    }

    static ObjectInspectModal instance;

    static readonly Dictionary<string, InspectMeta> MetaTable = new Dictionary<string, InspectMeta>
    {
        {
            "basket", new InspectMeta
            {
                id = "basket", code = "A4 · 藤编面包篮", sceneName = "Bakery_basket",
                sweetEuler = new Vector3(-68f, 35f, 0f),
                pinOrder = new[] { 0, 1, 2 },
                pinOrderHint = "暗扣已显露！请直接点击 3D 篮底的三枚铜扣（顺序：左扣 -> 中扣 -> 右扣）拆开夹层",
                guide = "第一步：按住鼠标右键（或按 WASD）将 3D 面包篮翻转到【篮底朝前】，寻找藤条下的三枚铜扣",
                hiddenTitle = "篮底夹层 · 林阿婆的手写字条",
                hiddenText = "藤编底部夹着一张沾着面粉的牛皮纸：“阿萤八岁生日面包，记得多放【 2 】勺葡萄干，出炉趁热送去学校后门。——林阿婆”",
                cipherClueBadge = "已解开篮底暗扣：面包房风门 · 内环刻度为 2"
            }
        },
        {
            "bell", new InspectMeta
            {
                id = "bell", code = "A5 · 面包店铜门铃", sceneName = "Bakery_bell",
                sweetEuler = new Vector3(48f, -120f, 15f),
                pinOrder = new[] { 1, 0, 2 },
                pinOrderHint = "暗扣已显露！请点击 3D 铜铃内壁的卡扣（顺序：中扣 -> 左扣 -> 右扣）",
                guide = "第一步：按住鼠标或 WASD 翻转铜铃，使【铃铛内壁朝向镜头】",
                hiddenTitle = "铜铃内壁 · 四十二年的凹痕",
                hiddenText = "黄铜内壁有一道被撞得发亮的浅坑，木柄内侧刻着：“风门三环刻度分别藏在【菜谱(外环)、台秤(中环)、面包篮(内环)】背面暗扣中。”",
                cipherClueBadge = "已解开铜铃暗扣：风门刻度藏于 菜谱(外) · 台秤(中) · 面包篮(内)"
            }
        },
        {
            "scale", new InspectMeta
            {
                id = "scale", code = "A6 · 铁制老台秤", sceneName = "Bakery_scale",
                sweetEuler = new Vector3(-35f, 145f, 0f),
                pinOrder = new[] { 2, 1, 0 },
                pinOrderHint = "暗扣已显露！请点击 3D 台秤底座上的铜销（顺序：右扣 -> 中扣 -> 左扣）",
                guide = "第一步：按住鼠标或 WASD 将老台秤转到【背面底座朝前】，寻找底座铜销",
                hiddenTitle = "秤盘底座 · 从不计较的刻度",
                hiddenText = "拆开底座护盖，里面的止动针永远停在【 5 】两刻度——林阿婆称面粉和果干时，手总是往重里放。",
                cipherClueBadge = "已解开台秤底座：面包房风门 · 中环刻度为 5"
            }
        },
        {
            "recipe", new InspectMeta
            {
                id = "recipe", code = "A7 · 手写菜谱本", sceneName = "Bakery_recipe",
                sweetEuler = new Vector3(62f, 155f, 0f),
                pinOrder = new[] { 0, 2, 1 },
                pinOrderHint = "暗扣已显露！请点击 3D 菜谱封底的书扣（顺序：左扣 -> 右扣 -> 中扣）解开封底",
                guide = "第一步：按住鼠标或 WASD 翻转菜谱本，使【封底背面朝向镜头】",
                hiddenTitle = "菜谱封底 · 烤炉的脾气",
                hiddenText = "封底暗袋里用铅笔写着：“起海风的日子，炉膛要分【 3 】次鼓风，火苗才会稳。如果哪天我不在了，别让老伙计受潮。”",
                cipherClueBadge = "已解开菜谱封底：面包房风门 · 外环刻度为 3"
            }
        },
        {
            "flour", new InspectMeta
            {
                id = "flour", code = "A8 · 粗布面粉袋", sceneName = "Bakery_flour",
                sweetEuler = new Vector3(-40f, -95f, 10f),
                pinOrder = new[] { 1, 2, 0 },
                pinOrderHint = "暗扣已显露！请点击 3D 麻袋侧面的铅封扣（顺序：中扣 -> 右扣 -> 左扣）",
                guide = "第一步：按住鼠标或 WASD 转动面粉袋，查看【侧面缝线折角】",
                hiddenTitle = "麻袋折角 · 鲁班木栓口诀",
                hiddenText = "袋角木牌上刻着炉门木栓的解法：“甲乙丙三栓联动，只需拨动中间的【乙栓】即可一举解开三连木栓。”",
                cipherClueBadge = "已解开麻袋木牌：第一重三连木栓只需拨动中间「乙栓」"
            }
        },
        {
            "slipper", new InspectMeta
            {
                id = "slipper", code = "C3 · 被咬过的旧拖鞋", sceneName = "S_slipper",
                sweetEuler = new Vector3(-65f, 40f, 0f),
                pinOrder = new[] { 0, 1, 2 },
                pinOrderHint = "暗扣已显露！请点击 3D 鞋底卡在泥槽里的三处印记（顺序：左 -> 中 -> 右）",
                guide = "第一步：按住鼠标或 WASD 将拖鞋翻转到【鞋底朝前】，查看鞋底泥印",
                hiddenTitle = "鞋头牙印 · 小黑的藏宝方向",
                hiddenText = "鞋帮上留着【 4 】颗小狗乳牙印，鞋底沾着操场秋千架下的红土——小黑叼着红球跑向了操场秋千！",
                cipherClueBadge = "已解开拖鞋线索：学校木匣 · 外环刻度为 4（红球在秋千下）"
            }
        },
        {
            "doghouse", new InspectMeta
            {
                id = "doghouse", code = "C4 · 学校后门狗窝", sceneName = "S_doghouse",
                sweetEuler = new Vector3(25f, 135f, 0f),
                pinOrder = new[] { 2, 0, 1 },
                pinOrderHint = "暗扣已显露！请点击 3D 狗窝背板上的三枚木栓（顺序：右扣 -> 左扣 -> 中扣）",
                guide = "第一步：按住鼠标或 WASD 转动狗窝，使【木箱背面内侧朝向镜头】",
                hiddenTitle = "木箱内侧 · 阿萤的粉笔字",
                hiddenText = "木板内侧用彩色粉笔画着骨头和小太阳，旁边写着：“小黑专属值班室——放学第【 1 】个出来的人负责喂水！”",
                cipherClueBadge = "已解开狗窝木栓：学校木匣 · 中环刻度为 1"
            }
        },
        {
            "blackboard", new InspectMeta
            {
                id = "blackboard", code = "C7 · 操场移动黑板", sceneName = "S_blackboard",
                sweetEuler = new Vector3(15f, 165f, 0f),
                pinOrder = new[] { 1, 2, 0 },
                pinOrderHint = "暗扣已显露！请点击 3D 黑板背面木框的卡簧（顺序：中扣 -> 右扣 -> 左扣）",
                guide = "第一步：按住鼠标或 WASD 将黑板旋转 180° 到【背面木框朝前】",
                hiddenTitle = "黑板背面 · 最后一天的全班签名",
                hiddenText = "背面暗格木条上写着：“第【 6 】值日小组全员签名！最角落画着一只吐舌头的小黑狗。”",
                cipherClueBadge = "已解开黑板背框：学校木匣 · 内环刻度为 6"
            }
        },
        {
            "desk", new InspectMeta
            {
                id = "desk", code = "C8 · 刻画小狗的旧课桌", sceneName = "S_desk",
                sweetEuler = new Vector3(-55f, -110f, 0f),
                pinOrder = new[] { 0, 2, 1 },
                pinOrderHint = "暗扣已显露！请点击 3D 课桌抽屉底部的暗扣（顺序：左扣 -> 右扣 -> 中扣）",
                guide = "第一步：按住鼠标或 WASD 翻转课桌，查看【桌洞底面暗格】",
                hiddenTitle = "桌洞内壁 · 阿萤与小黑的约定",
                hiddenText = "桌洞深处用小刀刻着：“小黑不怕打雷，也不怕涨潮。暗号是【 4 - 1 - 6 】，等山路修好了我们就回来看你。”",
                cipherClueBadge = "已解开课桌暗格：学校木匣完整刻度为 4 - 1 - 6"
            }
        },
        {
            "log", new InspectMeta
            {
                id = "log", code = "D2 · 守塔人航海日志", sceneName = "LH_log",
                sweetEuler = new Vector3(-62f, 130f, 0f),
                pinOrder = new[] { 2, 1, 0 },
                pinOrderHint = "暗扣已显露！请点击 3D 日志皮套背面的黄铜搭扣（顺序：右扣 -> 中扣 -> 左扣）",
                guide = "第一步：按住鼠标或 WASD 翻转航海日志，使【封底皮套朝向镜头】",
                hiddenTitle = "日志封底 · 灯塔联络暗语",
                hiddenText = "封底写着：“加满煤油后，把灯头对准北面山脊，连闪【 3 】次光束——山顶的人看到信号，就会用手电筒回信。”",
                cipherClueBadge = "已解开日志皮扣：灯塔罗经 · 内环刻度为 3"
            }
        },
        {
            "hat", new InspectMeta
            {
                id = "hat", code = "D5 · 守塔人白檐帽", sceneName = "LH_hat",
                sweetEuler = new Vector3(-72f, 20f, 0f),
                pinOrder = new[] { 0, 1, 2 },
                pinOrderHint = "暗扣已显露！请点击 3D 帽檐内衬的三枚铜徽扣（顺序：左扣 -> 中扣 -> 右扣）",
                guide = "第一步：按住鼠标或 WASD 向上翻转白檐帽，使【帽檐内衬朝向镜头】",
                hiddenTitle = "帽檐内衬 · 三十一年的值守",
                hiddenText = "发白的内衬布条下缝着“潮汐镇航标站 · 北偏西第【 6 】象限”，帽檐盐渍斑驳，却被擦拭得干干净净。",
                cipherClueBadge = "已解开帽衬徽扣：灯塔罗经 · 外环刻度为 6"
            }
        },
        {
            "scope", new InspectMeta
            {
                id = "scope", code = "D3 · 黄铜单筒望远镜", sceneName = "LH_scope",
                sweetEuler = new Vector3(30f, -145f, 0f),
                pinOrder = new[] { 1, 0, 2 },
                pinOrderHint = "暗扣已显露！请点击 3D 望远镜镜筒上的调焦销（顺序：中扣 -> 左扣 -> 右扣）",
                guide = "第一步：按住鼠标或 WASD 旋转望远镜，找到【镜筒背面调焦环】",
                hiddenTitle = "镜筒刻度 · 锁定山顶新城的方位",
                hiddenText = "黄铜调焦环被一道红漆固定在北偏西仰角第【 2 】档——那是山那边新城亮灯的位置，也是老周新邮局的方向。",
                cipherClueBadge = "已解开镜筒调焦锁：灯塔罗经 · 中环刻度为 2"
            }
        }
    };

    public static bool IsOpen => instance != null && instance.active;

    public static void ExternalRotate(float deltaYaw, float deltaPitch)
    {
        if (!IsOpen) return;
        instance.currentEuler.y += deltaYaw;
        instance.currentEuler.x = Mathf.Clamp(instance.currentEuler.x + deltaPitch, -88f, 88f);
        if (instance.modelPivot != null)
            instance.modelPivot.localRotation = Quaternion.Euler(instance.currentEuler);
        instance.UpdatePinVisuals();
        instance.RefreshInspectUI();
    }

    public static void ExternalPressPin(int pinIndex)
    {
        if (!IsOpen) return;
        instance.TryPressPin(Mathf.Clamp(pinIndex, 0, 2));
    }

    public static void ExternalClearDust(bool byBreath)
    {
        if (!IsOpen) return;
        instance.ClearSaltDust(byBreath);
    }

    public static void ExternalAutoCalibrateByAI()
    {
        if (!IsOpen || instance.currentMeta == null) return;
        instance.currentEuler = instance.currentMeta.sweetEuler;
        instance.sweetSpotExposed = true;
        instance.UnlockHiddenDetail();
    }

    public static void Open(
        string itemId,
        string label,
        string surfaceText,
        GameState state,
        SaveStore store,
        EggboyPlayer player,
        EggboyUI ui,
        ControlGate gate)
    {
        if (ui == null || !ui.inputClose) return;
        if (instance == null)
        {
            var host = new GameObject("ObjectInspectModal");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ObjectInspectModal>();
        }
        instance.ShowModal(itemId, label, surfaceText, state, store, player, ui, gate);
    }

    bool active;
    string currentId = "";
    string currentLabel = "";
    string currentSurface = "";
    InspectMeta currentMeta;
    GameState gameState;
    SaveStore saveStore;
    EggboyPlayer eggboy;
    EggboyUI eggboyUi;
    ControlGate controlGate;
    object gateToken;
    ChaseCamera chaseCam;
    bool restoreChase;

    GameObject inspectRig;
    Transform modelPivot;
    Transform pinClusterRoot;
    GameObject saltFrostCover;
    readonly Transform[] pinTransforms = new Transform[3];
    readonly Renderer[] pinRenderers = new Renderer[3];
    readonly bool[] pinPressed = new bool[3];
    int pinStepIndex;
    bool sweetSpotExposed;
    bool dustCleared;
    float rubProgress;
    string pinFeedbackMsg = "";

    Light keyLight;
    Vector3 currentEuler = new Vector3(16f, -28f, 0f);
    float zoom = 1f;
    float resonance;
    bool detailUnlocked;
    float pulseTimer;
    Vector2 mouseDownPos;
    bool isDraggingRotate;

    RectTransform inspectCard;
    Text headerTitleText, codeSubText, surfaceBodyText, progressTitleText, guideSubText, hiddenTitleText, hiddenBodyText, clueBadgeText;
    readonly Text[] pinBtnTexts = new Text[3];

    void ShowModal(
        string itemId,
        string label,
        string surfaceText,
        GameState state,
        SaveStore store,
        EggboyPlayer player,
        EggboyUI ui,
        ControlGate gate)
    {
        if (active) CloseModal();

        currentId = itemId ?? "";
        currentLabel = string.IsNullOrEmpty(label) ? itemId : label;
        currentSurface = surfaceText ?? "";
        gameState = state;
        saveStore = store;
        eggboy = player;
        eggboyUi = ui;
        controlGate = gate;

        if (!MetaTable.TryGetValue(currentId, out currentMeta))
        {
            currentMeta = new InspectMeta
            {
                id = currentId,
                code = "潮汐镇旧物",
                sceneName = "Bakery_" + currentId,
                sweetEuler = new Vector3(-55f, 120f, 0f),
                pinOrder = new[] { 0, 1, 2 },
                pinOrderHint = "暗扣已显露！请按顺序点击 3D 物件上的三枚暗扣（左 -> 中 -> 右）",
                guide = "第一步：按住鼠标或 WASD 旋转右侧 3D 旧物，寻找背面暗扣",
                hiddenTitle = "旧物背面 · 留下的痕迹",
                hiddenText = "旧物表面留着经年累月摩挲的温润痕迹，那是潮汐镇的日常留给它的印记。",
                cipherClueBadge = "已记录旧物线索"
            };
        }

        detailUnlocked = gameState != null && gameState.seenMemories.Contains(currentId + "_detail");
        currentEuler = detailUnlocked ? currentMeta.sweetEuler : new Vector3(14f, -25f, 0f);
        zoom = 1f;
        resonance = detailUnlocked ? 1f : 0.08f;
        sweetSpotExposed = detailUnlocked;
        dustCleared = detailUnlocked;
        rubProgress = 0f;
        pinStepIndex = detailUnlocked ? 3 : 0;
        for (int i = 0; i < 3; i++) pinPressed[i] = detailUnlocked;
        pinFeedbackMsg = "";
        pulseTimer = 0f;

        if (controlGate != null) gateToken = controlGate.Acquire("inspect");
        if (eggboy != null) eggboy.locked = true;
        chaseCam = FindObjectOfType<ChaseCamera>();
        restoreChase = chaseCam != null && chaseCam.enabled;
        if (restoreChase) chaseCam.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureInspectUI();
        if (inspectCard != null) inspectCard.gameObject.SetActive(true);
        BuildInspectRig();
        RefreshInspectUI();
        active = true;
    }

    void EnsureInspectUI()
    {
        if (inspectCard != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(eggboyUi);
        if (root == null) return;

        inspectCard = TideTownStyleUI.CreateParchmentCard(root, "InspectParchmentCard", 520f, 490f, 8);
        inspectCard.anchorMin = inspectCard.anchorMax = new Vector2(0f, 0.5f);
        inspectCard.pivot = new Vector2(0f, 0.5f);
        inspectCard.anchoredPosition = new Vector2(28f, 10f);

        headerTitleText = TideTownStyleUI.CreateText(
            inspectCard, "", 22, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 206f), new Vector2(458f, 32f), true);

        codeSubText = TideTownStyleUI.CreateText(
            inspectCard, "", 13, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft,
            new Vector2(0f, 178f), new Vector2(458f, 22f), false);

        surfaceBodyText = TideTownStyleUI.CreateText(
            inspectCard, "", 14, TideTownStyleUI.InkDark, TextAnchor.UpperLeft,
            new Vector2(0f, 134f), new Vector2(458f, 48f), false);

        progressTitleText = TideTownStyleUI.CreateText(
            inspectCard, "", 15, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 84f), new Vector2(458f, 26f), true);

        guideSubText = TideTownStyleUI.CreateText(
            inspectCard, "", 13, TideTownStyleUI.InkMuted, TextAnchor.UpperLeft,
            new Vector2(0f, 46f), new Vector2(458f, 42f), false);

        // 三枚对应 3D 模型表面暗扣的联动按钮（既可直接点右侧 3D 模型上的铜扣，也可点此处或按 1/2/3）
        string[] pNames = { "1 · 左铜扣", "2 · 中铜扣", "3 · 右铜扣" };
        for (int i = 0; i < 3; i++)
        {
            int pinIdx = i;
            var btn = TideTownStyleUI.CreateWarmButton(
                inspectCard,
                pNames[i],
                new Vector2(-152f + i * 152f, -2f),
                new Vector2(140f, 32f),
                false,
                () => TryPressPin(pinIdx));
            pinBtnTexts[i] = btn.GetComponentInChildren<Text>();
        }

        hiddenTitleText = TideTownStyleUI.CreateText(
            inspectCard, "", 16, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, -44f), new Vector2(458f, 26f), true);

        hiddenBodyText = TideTownStyleUI.CreateText(
            inspectCard, "", 14, TideTownStyleUI.InkDark, TextAnchor.UpperLeft,
            new Vector2(0f, -96f), new Vector2(458f, 72f), false);

        clueBadgeText = TideTownStyleUI.CreateText(
            inspectCard, "", 14, TideTownStyleUI.AmberAccent, TextAnchor.MiddleLeft,
            new Vector2(0f, -152f), new Vector2(458f, 26f), true);

        TideTownStyleUI.CreateWarmButton(
            inspectCard, "放下旧物（空格 / Esc）", new Vector2(0f, -202f), new Vector2(260f, 38f), true, CloseModal);
    }

    void RefreshInspectUI()
    {
        if (inspectCard == null || currentMeta == null) return;
        headerTitleText.text = "旧物端详 · " + currentLabel;
        codeSubText.text = currentMeta.code;
        surfaceBodyText.text = currentSurface;

        if (detailUnlocked)
        {
            progressTitleText.text = "背面暗扣已开启";
            guideSubText.text = "这件旧物背面尘封的字迹与痕迹已展现在眼前。";
        }
        else if (sweetSpotExposed && !dustCleared)
        {
            progressTitleText.text = "背面覆盖着一层海雾盐霜";
            guideSubText.text = !string.IsNullOrEmpty(pinFeedbackMsg)
                ? pinFeedbackMsg
                : "盐霜封住了旧铜扣，吹一口气、或是轻轻拂拭，或许能让它重见天日。";
        }
        else if (sweetSpotExposed)
        {
            progressTitleText.text = "背面三枚铜扣已显露（已落槽 " + pinStepIndex + " / 3）";
            guideSubText.text = !string.IsNullOrEmpty(pinFeedbackMsg)
                ? pinFeedbackMsg
                : "盐霜散去，露出三枚深浅与大小各异的黄铜铆钉。";
        }
        else
        {
            progressTitleText.text = "端详这件旧物的各个角落";
            guideSubText.text = !string.IsNullOrEmpty(pinFeedbackMsg)
                ? pinFeedbackMsg
                : "正面似乎只是寻常物件，不知背面或底座是否藏着别的痕迹。";
        }

        string[] pLabels = { "左铜扣", "中铜扣", "右铜扣" };
        for (int i = 0; i < 3; i++)
        {
            if (pinBtnTexts[i] == null) continue;
            if (!sweetSpotExposed && !detailUnlocked)
                pinBtnTexts[i].text = pLabels[i];
            else if (!dustCleared && !detailUnlocked)
                pinBtnTexts[i].text = pLabels[i] + " (覆霜)";
            else if (pinPressed[i])
                pinBtnTexts[i].text = pLabels[i] + " · 已落槽";
            else
                pinBtnTexts[i].text = pLabels[i];
        }

        if (detailUnlocked)
        {
            hiddenTitleText.text = currentMeta.hiddenTitle;
            hiddenBodyText.text = currentMeta.hiddenText;
            clueBadgeText.text = currentMeta.cipherClueBadge;
        }
        else if (sweetSpotExposed && !dustCleared)
        {
            hiddenTitleText.text = "岁月留下的海盐霜层";
            hiddenBodyText.text = "海风在物件背面凝成了一层薄薄的白霜，遮住了底下的机关铜扣。";
            clueBadgeText.text = "";
        }
        else
        {
            hiddenTitleText.text = sweetSpotExposed ? "三连黄铜暗扣" : "表面平平无奇";
            hiddenBodyText.text = sweetSpotExposed
                ? "三枚黄铜铆钉由内侧卡簧相连，只有按正确的咬合先后顺序推入，暗格才会弹开。"
                : "将手里的物件翻转过来仔细端详，或许能找到前人留下的暗记。";
            clueBadgeText.text = "";
        }
    }

    void BuildInspectRig()
    {
        var cam = Camera.main;
        if (cam == null) return;

        inspectRig = new GameObject("InspectRig");
        inspectRig.transform.SetParent(cam.transform, false);
        float dist = 2.38f;
        float halfH = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * dist;
        float offsetX = 0.35f * cam.aspect * halfH;
        inspectRig.transform.localPosition = new Vector3(offsetX, 0.02f, dist);
        inspectRig.transform.localRotation = Quaternion.identity;

        var lightGo = new GameObject("InspectKeyLight");
        lightGo.transform.SetParent(inspectRig.transform, false);
        lightGo.transform.localPosition = new Vector3(-0.6f, 0.7f, -0.9f);
        keyLight = lightGo.AddComponent<Light>();
        keyLight.type = LightType.Point;
        keyLight.color = new Color32(255, 224, 178, 255);
        keyLight.range = 4.5f;
        keyLight.intensity = 2.2f;
        keyLight.shadows = LightShadows.None;

        var pivotGo = new GameObject("ModelPivot");
        pivotGo.transform.SetParent(inspectRig.transform, false);
        modelPivot = pivotGo.transform;

        var clone = TideTownStyleUI.CloneSceneLux3D(currentMeta.sceneName, 0.86f, false);
        if (clone != null)
        {
            clone.transform.SetParent(modelPivot, false);
            clone.transform.localPosition = new Vector3(0f, -0.28f, 0f);
        }

        // 在 3D 物件的“隐藏视角面”挂载三枚可点击的 3D 黄铜暗扣与海盐灰尘遮盖层
        Build3DInteractivePinsOnModel();

        modelPivot.localRotation = Quaternion.Euler(currentEuler);
        UpdatePinVisuals();
    }

    void Build3DInteractivePinsOnModel()
    {
        if (modelPivot == null || currentMeta == null) return;

        pinClusterRoot = new GameObject("HiddenBrassPinCluster").transform;
        pinClusterRoot.SetParent(modelPivot, false);

        Quaternion invSweet = Quaternion.Inverse(Quaternion.Euler(currentMeta.sweetEuler));
        pinClusterRoot.localRotation = invSweet;
        pinClusterRoot.localPosition = invSweet * new Vector3(0f, 0.02f, -0.24f);

        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        for (int i = 0; i < 3; i++)
        {
            var pinGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pinGo.name = "InspectPin_" + i;
            pinGo.transform.SetParent(pinClusterRoot, false);
            float radius = 0.042f + i * 0.008f;
            pinGo.transform.localPosition = new Vector3((i - 1) * 0.115f, 0f, 0f);
            pinGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pinGo.transform.localScale = new Vector3(radius, 0.024f, radius);

            var mat = new Material(sh);
            mat.color = new Color32(212, 162, 84, 255);
            pinRenderers[i] = pinGo.GetComponent<Renderer>();
            pinRenderers[i].sharedMaterial = mat;

            Destroy(pinGo.GetComponent<Collider>());
            var sc = pinGo.AddComponent<SphereCollider>();
            sc.radius = 1.45f;

            pinTransforms[i] = pinGo.transform;
        }

        // 海盐霜尘层（未吹气或擦拭前盖在铜扣上方）
        saltFrostCover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        saltFrostCover.name = "SaltFrostCover";
        Destroy(saltFrostCover.GetComponent<Collider>());
        saltFrostCover.transform.SetParent(pinClusterRoot, false);
        saltFrostCover.transform.localPosition = new Vector3(0f, 0f, -0.022f);
        saltFrostCover.transform.localScale = new Vector3(0.36f, 0.12f, 0.018f);
        var frostMat = new Material(sh);
        frostMat.color = new Color32(236, 232, 224, 235);
        saltFrostCover.GetComponent<Renderer>().sharedMaterial = frostMat;
    }

    void UpdatePinVisuals()
    {
        if (pinClusterRoot == null) return;
        bool visible = sweetSpotExposed || detailUnlocked || resonance >= 0.62f;
        if (pinClusterRoot.gameObject.activeSelf != visible)
            pinClusterRoot.gameObject.SetActive(visible);

        if (saltFrostCover != null)
            saltFrostCover.SetActive(visible && !dustCleared && !detailUnlocked);

        for (int i = 0; i < 3; i++)
        {
            if (pinTransforms[i] == null || pinRenderers[i] == null) continue;
            bool pressed = pinPressed[i] || detailUnlocked;
            Vector3 lp = pinTransforms[i].localPosition;
            lp.z = pressed ? 0.032f : 0f;
            pinTransforms[i].localPosition = lp;

            var mat = pinRenderers[i].sharedMaterial;
            if (mat != null)
            {
                if (pressed)
                {
                    mat.color = new Color32(255, 215, 110, 255);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.28f) * 1.6f);
                    }
                }
                else if (sweetSpotExposed && dustCleared)
                {
                    mat.color = new Color32(235, 175, 88, 255);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", new Color(0.95f, 0.55f, 0.18f) * 0.75f);
                    }
                }
                else
                {
                    mat.color = new Color32(165, 128, 78, 255);
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }
    }

    void ClearSaltDust(bool byBreath)
    {
        if (dustCleared) return;
        dustCleared = true;
        ShadowAndChimeSystem.PlayProceduralChime(698.46f, 0.22f);
        pinFeedbackMsg = byBreath
            ? "呼——你吹散了覆盖在背面的海盐霜尘，露出了底下的三枚黄铜暗扣！"
            : "你拂去了覆盖在背面的海盐霜尘，露出了底下的三枚黄铜暗扣！";
        UpdatePinVisuals();
        RefreshInspectUI();

        if (byBreath)
            TideTownAchievementSystem.Unlock("breath_dust");
    }

    void TryPressPin(int pinIndex)
    {
        if (detailUnlocked || currentMeta == null) return;

        if (!sweetSpotExposed)
        {
            pinFeedbackMsg = "正面并未看到铜扣，不妨将旧物转到背面或底座看看。";
            ShadowAndChimeSystem.PlayProceduralChime(240f, 0.18f);
            RefreshInspectUI();
            return;
        }

        if (!dustCleared)
        {
            // 点击覆霜铜扣时累积拂拭进度（点 2 下或吹气/按 F 即可拂去盐霜，保证无麦克风玩家 100% 顺畅）
            rubProgress += 0.55f;
            if (rubProgress >= 1f)
            {
                ClearSaltDust(false);
            }
            else
            {
                pinFeedbackMsg = "铜扣上覆着一层盐霜，再拂拭一下或吹一口气就能露出铜扣。";
                ShadowAndChimeSystem.PlayProceduralChime(330f, 0.14f);
                RefreshInspectUI();
            }
            return;
        }

        if (pinPressed[pinIndex]) return;

        int expectedPin = currentMeta.pinOrder[Mathf.Clamp(pinStepIndex, 0, 2)];
        if (pinIndex == expectedPin)
        {
            pinPressed[pinIndex] = true;
            pinStepIndex++;
            float pitch = 587.33f + pinStepIndex * 110f;
            ShadowAndChimeSystem.PlayProceduralChime(pitch, 0.25f);
            pinFeedbackMsg = "咔哒！第 " + pinStepIndex + " 枚铜扣已顺利咬合落槽。";
            UpdatePinVisuals();

            if (pinStepIndex >= 3)
            {
                UnlockHiddenDetail();
            }
            else
            {
                RefreshInspectUI();
            }
        }
        else
        {
            for (int i = 0; i < 3; i++) pinPressed[i] = false;
            pinStepIndex = 0;
            ShadowAndChimeSystem.PlayProceduralChime(210f, 0.32f);
            pinFeedbackMsg = "卡簧触发反震，所有铜扣已弹回原位，看来推入的先后顺序不对。";
            UpdatePinVisuals();
            RefreshInspectUI();
        }
    }

    bool TryRaycast3DPin(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null || pinClusterRoot == null || !pinClusterRoot.gameObject.activeSelf) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        var hits = Physics.RaycastAll(ray, 8f);
        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            if (col != null && col.name.StartsWith("InspectPin_"))
            {
                if (int.TryParse(col.name.Substring("InspectPin_".Length), out int idx) && idx >= 0 && idx < 3)
                {
                    TryPressPin(idx);
                    return true;
                }
            }
        }
        return false;
    }

    void Update()
    {
        if (!active) return;

        // 当背面露出但盐霜未除时：支持对着麦克风吹气除霜，也支持按 F / 手柄 RB 或鼠标来回擦拭除霜
        if (sweetSpotExposed && !dustCleared && !detailUnlocked)
        {
            bool micBlowing = HardwareAndSensorHub.Instance != null && HardwareAndSensorHub.Instance.MicLoudness > 0.16f;
            if (micBlowing)
            {
                rubProgress += Time.unscaledDeltaTime * 2.4f;
                if (rubProgress >= 1f) ClearSaltDust(true);
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                ClearSaltDust(false);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            mouseDownPos = Input.mousePosition;
            isDraggingRotate = false;
        }

        float rotX = 0f, rotY = 0f;
        if (Input.GetMouseButton(0))
        {
            if (Vector2.Distance(Input.mousePosition, mouseDownPos) > 6f)
                isDraggingRotate = true;

            if (isDraggingRotate)
            {
                if (sweetSpotExposed && !dustCleared)
                {
                    // 在背面覆霜状态下来回拖动鼠标相当于擦拭盐霜
                    rubProgress += (Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y"))) * 0.18f;
                    if (rubProgress >= 1f) ClearSaltDust(false);
                }
                else
                {
                    rotY = -Input.GetAxis("Mouse X") * 5.2f;
                    rotX = Input.GetAxis("Mouse Y") * 5.2f;
                }
            }
        }
        else if (Input.GetMouseButton(1))
        {
            rotY = -Input.GetAxis("Mouse X") * 5.2f;
            rotX = Input.GetAxis("Mouse Y") * 5.2f;
        }

        if (Input.GetMouseButtonUp(0) && !isDraggingRotate)
        {
            TryRaycast3DPin(Input.mousePosition);
        }

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) rotY += 95f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) rotY -= 95f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) rotX += 85f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) rotX -= 85f * Time.unscaledDeltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            zoom = Mathf.Clamp(zoom + scroll * 0.8f, 0.65f, 1.55f);

        bool changed = false;
        if (Mathf.Abs(rotX) > 0.001f || Mathf.Abs(rotY) > 0.001f)
        {
            currentEuler.x = Mathf.Clamp(currentEuler.x + rotX, -88f, 88f);
            currentEuler.y += rotY;
            changed = true;
        }

        if (modelPivot != null)
        {
            modelPivot.localRotation = Quaternion.Euler(currentEuler);
            if (inspectRig != null)
                inspectRig.transform.localScale = Vector3.one * zoom;
        }

        if (!detailUnlocked && currentMeta != null)
        {
            Quaternion curQ = Quaternion.Euler(currentEuler);
            Quaternion targetQ = Quaternion.Euler(currentMeta.sweetEuler);
            float angle = Quaternion.Angle(curQ, targetQ);

            resonance = Mathf.Clamp01(1f - Mathf.Max(0f, angle - 10f) / 125f);
            bool nowExposed = angle <= 38f;
            if (nowExposed && !sweetSpotExposed)
            {
                sweetSpotExposed = true;
                pinFeedbackMsg = "";
                ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.22f);
                changed = true;
            }
            else if (!nowExposed && sweetSpotExposed && pinStepIndex == 0 && !dustCleared)
            {
                sweetSpotExposed = false;
                changed = true;
            }
        }
        else if (detailUnlocked)
        {
            resonance = 1f;
            sweetSpotExposed = true;
            dustCleared = true;
        }

        if (changed)
        {
            UpdatePinVisuals();
            RefreshInspectUI();
        }

        if (keyLight != null)
        {
            if (pulseTimer > 0f)
            {
                pulseTimer -= Time.unscaledDeltaTime;
                keyLight.intensity = 2.2f + Mathf.Sin(pulseTimer * 12f) * 1.2f;
            }
            else
            {
                keyLight.intensity = Mathf.Lerp(1.8f, 2.8f, resonance);
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) TryPressPin(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) TryPressPin(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) TryPressPin(2);
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            CloseModal();
        }
    }

    void UnlockHiddenDetail()
    {
        if (detailUnlocked) return;
        detailUnlocked = true;
        sweetSpotExposed = true;
        dustCleared = true;
        resonance = 1f;
        pulseTimer = 1.6f;
        for (int i = 0; i < 3; i++) pinPressed[i] = true;
        UpdatePinVisuals();
        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.42f);
        if (gameState != null)
        {
            string key = currentId + "_detail";
            if (!gameState.seenMemories.Contains(key))
            {
                gameState.seenMemories.Add(key);
                if (saveStore != null) saveStore.Save(gameState);
            }
        }
        RefreshInspectUI();

        TideTownAchievementSystem.Unlock("tactile_pins");
        if (currentId == "desk")
            TideTownAchievementSystem.Unlock("desk_shortcut");
    }

    public void CloseModal()
    {
        if (!active) return;
        active = false;
        if (inspectRig != null) Destroy(inspectRig);
        inspectRig = null;
        modelPivot = null;
        pinClusterRoot = null;
        keyLight = null;
        if (inspectCard != null) inspectCard.gameObject.SetActive(false);

        if (restoreChase && chaseCam != null) chaseCam.enabled = true;
        restoreChase = false;

        if (controlGate != null && gateToken != null)
        {
            controlGate.Release(gateToken);
            gateToken = null;
        }
        if (eggboy != null)
        {
            eggboy.locked = false;
            if (detailUnlocked) eggboy.PlayEmote("hop");
        }
        if (eggboyUi != null && detailUnlocked && currentMeta != null)
        {
            eggboyUi.ShowToast(
                !string.IsNullOrEmpty(currentMeta.cipherClueBadge)
                    ? currentMeta.cipherClueBadge
                    : "已拆解「" + currentLabel + "」的背面暗扣线索",
                3.8f);
        }
    }
}
