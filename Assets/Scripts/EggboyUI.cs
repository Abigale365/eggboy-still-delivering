// HUD + 信件界面，照网页版 index.html 的布局和文案。
// 全部用 uGUI 运行时生成，不依赖预制体；中文字体放在 Resources/Fonts。
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EggboyUI : MonoBehaviour
{
    public Action<ExprId> OnExpressionPicked;
    public Action OnLetterClosed;
    public bool LetterOpen { get; private set; }
    public Text LetterBodyText => letterBody; // 揭字系统要给正文里能撕的字上色
    public bool legacyPrompt = true;   // 旧 Prototype 场景自己管邮筒提示；新框架交给 InteractionService
    // 自动测试时关掉键盘/点击收信，避免系统误触把逐行读信打断
    public bool inputClose = true;
    Button letterCloseBtn;
    Text letterTitle, letterBody, letterSign, letterIntro1, letterIntro2, letterHint;

    const int RefW = 1280, RefH = 720;
    const float GoalPaddingX = 12f, GoalPaddingY = 5f;
    const float GoalMaxTextWidth = 368f;
    EggboyPlayer player;
    IEggboyWorld world;
    Font font;
    Canvas canvas;
    Text goalText, promptText, lookText, tutorialText, carryText, toastText, subtitleText;
    GameObject promptPanel, letterOverlay, tutorialPanel, carryPanel, toastPanel, subtitlePanel;
    GameObject titlePanel, pausePanel, hud;
    GameObject goalPanel;
    CanvasGroup keysFade, goalFade;
    Text pauseGoal, runSetting, recenterSetting;
    float keysRemaining = 12f, goalRemaining, firstTutorialRemaining;
    bool freeStarted;
    KeyCode firstTutorialKey;
    Action firstTutorialDone;
    readonly System.Collections.Generic.HashSet<string> cinematicOwners = new System.Collections.Generic.HashSet<string>();
    public bool Cinematic => cinematicOwners.Count > 0;
    public bool FirstTutorialActive => firstTutorialDone != null;
    RectTransform letterContent, letterViewport;
    ScrollRect letterScroll;
    const float LetterBodyPadding = 12f;
    readonly System.Collections.Generic.List<RectTransform> letterRules = new System.Collections.Generic.List<RectTransform>();
    string ruledText;
    public bool ToastVisible => toastPanel != null && toastPanel.activeInHierarchy;
    Slider volumeBar1, volumeBar2, sensBar;
    Text volumeText1, volumeText2, sensText;
    System.Action onTitleStart;
    bool titleShowing;
    public bool Paused { get; private set; }
    public bool TitleShowing => titleShowing;
    float volume = 1f;
    float toastUntil;
    readonly System.Collections.Generic.List<Image> faceBg = new System.Collections.Generic.List<Image>();
    readonly System.Collections.Generic.List<Text> faceLabel = new System.Collections.Generic.List<Text>();
    ExprId activeExpr = ExprId.happy;
    bool letterTaken;
    Sprite rounded, white;

    public void Init(EggboyPlayer p, IEggboyWorld w)
    {
        player = p;
        world = w;
        font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = MakeRounded();
        white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        Build();
        SetGoal("目标：去山口的红色邮筒取信（往左走）");
        SetActiveExpression(ExprId.happy);
        SetLetterTaken(false);
    }

    void Build()
    {
        var es = EventSystem.current;
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es = go.GetComponent<EventSystem>();
        }
        var cgo = new GameObject("EGGBOY HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.matchWidthOrHeight = 0.5f;
        var root = cgo.GetComponent<RectTransform>();
        Debug.Log($"UI canvas {root.rect.width}x{root.rect.height} screen {Screen.width}x{Screen.height} scale {scaler.scaleFactor}");

        var hudRoot = new GameObject("GameplayHUD", typeof(RectTransform)).GetComponent<RectTransform>();
        hudRoot.SetParent(root, false);
        hudRoot.anchorMin = Vector2.zero;
        hudRoot.anchorMax = Vector2.one;
        hudRoot.offsetMin = hudRoot.offsetMax = Vector2.zero;
        hud = hudRoot.gameObject;
        var canvasRoot = root;
        root = hudRoot;

        // 目标：邮差投递路单。深胡桃木外框 + 羊皮纸内衬 + 左侧暖橘邮戳竖条 + 眉题（2026-11-16 绘本化）
        var goalCard = Panel(root, 18, -18, 0, 0, Walnut);
        goalCard.sprite = TideTownStyleUI.RoundedBorderSprite;
        goalCard.raycastTarget = false;
        goalPanel = goalCard.gameObject;
        goalFade = goalPanel.AddComponent<CanvasGroup>();
        AddDropShadow(goalCard);
        Inset(goalCard.transform, Parchment, 3f, TideTownStyleUI.RoundedBorderSprite);
        Strip(goalCard.transform, Amber, 9f, 4f, 9f);
        goalEyebrow = Label(goalPanel.transform, "POSTAL ROUTE · 当前目标", 11, Eyebrow, TextAnchor.UpperLeft, GoalTextLeft, -11f, 260f, 14f, true);
        goalText = Label(goalPanel.transform, "", 16, new Color32(0x3b, 0x2a, 0x1a, 255),
            TextAnchor.MiddleLeft, GoalTextLeft, -GoalTextTop, GoalMaxTextWidth, 22f, false);

        // 序章教学提示：只在 P0_boot 显示。深褐底 + 双细金线框 + 柔和投影
        var tutPanel = Panel(root, 18, -120, 620, 32, new Color32(0x3b, 0x2a, 0x1a, 200));
        AddDropShadow(tutPanel);
        Frame(tutPanel.transform, new Color32(0xe0, 0xb4, 0x72, 150), 2f);
        tutorialText = Label(tutPanel.transform, "", 14, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleLeft, 16, -4, 590, 24, false);
        tutorialPanel = tutPanel.gameObject;
        tutorialPanel.SetActive(false);

        // 邮包 / 手上的东西（WP-11 火柴盒、柴火捆）：挂在路单下面的小标签卡，左边一颗暖橘小圆点
        var carry = Panel(root, 18, -156, 300, 32, Walnut);
        carry.sprite = TideTownStyleUI.RoundedBorderSprite;
        AddDropShadow(carry);
        Inset(carry.transform, Parchment, 2.5f, TideTownStyleUI.RoundedBorderSprite);
        var dot = Img(carry.transform, "CarryDot", TideTownStyleUI.PillSprite, Amber);
        Pin(dot.rectTransform, 0f, 0.5f, 16f, 0f, 10f, 10f);
        carryText = Label(carry.transform, "", 14, InkDark, TextAnchor.MiddleLeft, 30, -4, 260, 24, false);
        carryPanel = carry.gameObject;
        carryPanel.SetActive(false);

        // 渲染标签
        var look = Panel(root, -18, -14, 150, 32, new Color32(0x3b, 0x2a, 0x1a, 210), true, true);
        lookText = Label(look.transform, "渲染：v2", 14, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleCenter, 0, 0, 130, 28, false);
        SetRect(lookText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 130, 28);
        if (!Features.debugUi) look.gameObject.SetActive(false);

        // 表情面板：点标题折叠/展开，和网页版 #faces-toggle 一致
        var faceTitle = Panel(root, -18, -54, 248, 30, new Color32(0x3b, 0x2a, 0x1a, 210), true, true);
        var faceTitleText = Label(faceTitle.transform, "表情 ▾", 14, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleLeft, 10, -3, 200, 24, false);
        var gridPanel = Panel(root, -18, -88, 248, 184, new Color32(0x3b, 0x2a, 0x1a, 210), true, true);
        var faceToggle = faceTitle.gameObject.AddComponent<Button>();
        faceToggle.targetGraphic = faceTitle;
        faceToggle.transition = Selectable.Transition.None;
        bool facesOpen = Features.debugUi;
        faceTitle.gameObject.SetActive(Features.debugUi);
        gridPanel.gameObject.SetActive(facesOpen);
        faceToggle.onClick.AddListener(() =>
        {
            facesOpen = !facesOpen;
            gridPanel.gameObject.SetActive(facesOpen);
            faceTitleText.text = facesOpen ? "表情 ▾" : "表情 ▸";
            Debug.Log("facesOpen " + facesOpen);
        });
        var grid = gridPanel.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(56, 40);
        grid.spacing = new Vector2(4, 4);
        grid.padding = new RectOffset(6, 6, 6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < EggboyExpressions.All.Length; i++)
        {
            var id = EggboyExpressions.All[i];
            var bgo = new GameObject("Face_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
            bgo.transform.SetParent(gridPanel.transform, false);
            var bg = bgo.GetComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color32(0xff, 0xf8, 0xec, 255);
            var btn = bgo.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor = new Color32(0xff, 0xf8, 0xec, 255),
                highlightedColor = new Color32(0xff, 0xdf, 0xc0, 255),
                pressedColor = new Color32(0xff, 0x9a, 0x4d, 255),
                selectedColor = new Color32(0xff, 0xf8, 0xec, 255),
                disabledColor = new Color32(0xcc, 0xcc, 0xcc, 255),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
            var lab = Label(bgo.transform, EggboyExpressions.Names[i], 13, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.MiddleCenter, 0, 0, 54, 36, false);
            SetRect(lab.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 54, 36);
            btn.onClick.AddListener(() => { OnExpressionPicked?.Invoke(id); SetActiveExpression(id); });
            faceBg.Add(bg);
            faceLabel.Add(lab);
        }

        // 底部按键提示：2026-11-16 下午从整屏宽的深色长条收成和教学条同款的胡桃木小牌（内缩金细线 + 投影），只按文字宽
        var keys = Panel(root, 0, 40, 720, 54, new Color32(0x3b, 0x2a, 0x1a, 222), true, true);
        keys.sprite = TideTownStyleUI.RoundedBorderSprite;
        keysFade = keys.gameObject.AddComponent<CanvasGroup>();
        keysFade.blocksRaycasts = false;
        SetRect(keys.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, 36, 720, 54);
        AddDropShadow(keys);
        Frame(keys.transform, new Color32(0xe0, 0xb4, 0x72, 130), 3f);
        Label(keys.transform, "WASD 移动    Shift 慢走（迎风时蹲低稳住）    空格 跳    E 互动\n鼠标转视角 · 滚轮缩放 · M 地图 · H 目标与按键 · Esc 暂停", 14,
            new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleCenter, 0, 0, 700, 48, false);

        // 互动提示：左边一枚立体键帽（E / 按住 E），右边深胡桃木外框 + 暖白内衬的胶囊写动作名，带投影、键帽轻轻呼吸。
        // promptText 保留整句原文（PromptText 照旧返回它，审计读的就是这句），只是不画出来；画面上是拆开的键帽和动作名。
        var promptImg = Panel(root, 0, 106, 340, 46, Walnut, true, true);
        promptImg.sprite = TideTownStyleUI.PillSprite;
        promptPanel = promptImg.gameObject;
        promptPanel.name = "PromptPanel";
        SetRect(promptImg.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, 106, 340, 46);
        AddDropShadow(promptImg);
        promptInner = Inset(promptPanel.transform, new Color32(0xff, 0xf8, 0xec, 255), 3f, TideTownStyleUI.PillSprite);
        promptText = Label(promptPanel.transform, "", 16, InkDark, TextAnchor.MiddleCenter, 0, 0, 320, 34, true);
        SetRect(promptText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 320, 34);
        promptText.enabled = false;
        promptKey = Img(promptPanel.transform, "PromptKeycap", TideTownStyleUI.KeycapSprite, Color.white);
        promptKey.type = Image.Type.Sliced;
        promptKeyText = Label(promptKey.transform, "E", 15, InkDark, TextAnchor.MiddleCenter, 0, 0, 60, 30, true);
        promptAction = Label(promptPanel.transform, "", 17, InkDark, TextAnchor.MiddleLeft, 0, 0, 300, 30, true);
        promptPanel.SetActive(false);

        // 邮戳 / 道具提示，几秒后自己收：深褐底，上下各一道细金线，左边一颗朱砂小圆点
        var toast = Panel(root, 0, 0, 440, 40, new Color32(0x3b, 0x2a, 0x1a, 235), true, true);
        SetRect(toast.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, 158, 440, 40);
        AddDropShadow(toast);
        HairLine(toast.transform, Gold, 1f, 3f, 18f);
        HairLine(toast.transform, Gold, 0f, 3f, 18f);
        var seal = Img(toast.transform, "ToastSeal", TideTownStyleUI.PillSprite, Cinnabar);
        Pin(seal.rectTransform, 0f, 0.5f, 18f, 0f, 9f, 9f);
        toastText = Label(toast.transform, "", 16, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleCenter, 0, 0, 400, 32, false);
        SetRect(toastText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 6, 0, 396, 32);
        toastPanel = toast.gameObject;
        toastPanel.SetActive(false);

        // WP-15 结局字幕：停在画面上，不自动收。电影感的宽幅半透明深暖褐底衬，上下细边
        var sub = Panel(canvasRoot, 0, 0, 980, 70, new Color32(0x1e, 0x14, 0x0c, 195), false, true);
        SetRect(sub.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, 214, 980, 70);
        HairLine(sub.transform, new Color32(0xe0, 0xb4, 0x72, 150), 1f, 0f, 40f);
        HairLine(sub.transform, new Color32(0xe0, 0xb4, 0x72, 150), 0f, 0f, 40f);
        subtitleText = Label(sub.transform, "", 22, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleCenter, 0, 0, 900, 56, false);
        SetRect(subtitleText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 900, 56);
        subtitleText.gameObject.AddComponent<Shadow>().effectColor = new Color32(0, 0, 0, 120);
        subtitlePanel = sub.gameObject;
        subtitlePanel.SetActive(false);

        BuildLetter(canvasRoot);
        BuildMenus(canvasRoot);
    }

    void BuildLetter(RectTransform root)
    {
        var overlay = Panel(root, 0, 0, RefW, RefH, new Color32(0, 0, 0, 60), false, true);
        SetRect(overlay.rectTransform, 0f, 0f, 1f, 1f, 0, 0, 0, 0);
        letterOverlay = overlay.gameObject;
        letterOverlay.name = "LetterOverlay";   // TideTownStyleUI.FindLetterOverlay 按这个名字找
        var overlayBtn = overlay.gameObject.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(CloseLetter);
        letterCloseBtn = overlayBtn;
        letterOverlay.SetActive(false);

        // 2026-11-16 绘本化：信纸背后先垫一层微微转开 +2.2° 的牛皮纸信封，像刚从信封里抽出来
        var envelope = Img(overlay.transform, "LetterEnvelope", TideTownStyleUI.RoundedBorderSprite, new Color32(0xd9, 0xbd, 0x8e, 255));
        envelope.type = Image.Type.Sliced;
        Pin(envelope.rectTransform, 0.5f, 0.5f, 24f, -16f, 580f, 632f);
        envelope.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.2f);
        envelope.raycastTarget = false;
        var envShadow = envelope.gameObject.AddComponent<Shadow>();
        envShadow.effectColor = new Color32(0, 0, 0, 70);
        envShadow.effectDistance = new Vector2(5f, -7f);
        // 信封口的三角折线（两条斜细线）
        foreach (float s in new[] { -1f, 1f })
        {
            var flap = Img(envelope.transform, "EnvelopeFlap", null, new Color32(0xb8, 0x96, 0x64, 160));
            Pin(flap.rectTransform, 0.5f, 1f, s * 143f, -78f, 330f, 2f);
            flap.rectTransform.localRotation = Quaternion.Euler(0f, 0f, s * 28f);
        }

        var paper = Panel(overlay.transform, 0, 0, 560, 620, new Color32(0xfb, 0xf3, 0xe2, 255), true, false);
        paper.gameObject.name = "LetterPaper";   // TideTownStyleUI.FindLetterPaper 按这个名字找（拆字系统挂字签用）
        SetRect(paper.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 560, 620);
        paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1f);
        paper.raycastTarget = false;
        var shadow = paper.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(0, 0, 0, 90);
        shadow.effectDistance = new Vector2(4f, -6f);
        // 信笺内框（两道淡细线）和右上角半透明朱砂邮戳：放在信纸最底层，不挡正文和字签
        var frame = Img(paper.transform, "LetterFrame", TideTownStyleUI.RoundedFrameSprite, new Color32(0xc9, 0xa8, 0x78, 170));
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = false;
        var frt = frame.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = new Vector2(14f, 14f); frt.offsetMax = new Vector2(-14f, -14f);
        var postmark = Img(paper.transform, "LetterPostmark", TideTownStyleUI.PostmarkRingSprite, new Color32(0xc0, 0x3a, 0x2c, 105));
        Pin(postmark.rectTransform, 1f, 1f, -70f, -64f, 96f, 96f);
        postmark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);
        postmark.raycastTarget = false;
        var hintBar = Img(paper.transform, "LetterHintBar", TideTownStyleUI.RoundedBorderSprite, new Color32(0xf1, 0xe2, 0xc4, 255));
        hintBar.type = Image.Type.Sliced;
        Pin(hintBar.rectTransform, 0.5f, 1f, 0f, -584f, 300f, 28f);
        hintBar.raycastTarget = false;

        letterIntro1 = Label(paper.transform, "你从邮筒里取出了三封信：", 14, new Color32(0x8a, 0x6a, 0x4a, 255), TextAnchor.UpperLeft, 30, -24, 500, 24, false);
        letterIntro2 = Label(paper.transform, "· 致 面包店的烤炉\n· 致 小黑\n· 致 灯塔", 14, new Color32(0x8a, 0x6a, 0x4a, 255), TextAnchor.UpperLeft, 44, -50, 480, 62, false);
        letterTitle = Label(paper.transform, "面包店的烤炉", 24, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.UpperLeft, 30, -120, 500, 34, true);
        letterBody = Label(paper.transform,
            "老伙计：\n\n四十二年，你每天早上五点就热起来，比我还早。\n搬家的卡车装不下你，我只带走了你烤的最后一炉面包，路上就吃完了。\n新城的面包都用电炉烤，没有味道。\n\n邮差，如果你读到这里，请帮我再生一次火。\n不用烤什么，让它暖一暖就好。",
            18, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.UpperLeft, 30, -162, 500, 320, false);
        letterSign = Label(paper.transform, "——林阿婆", 19, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.UpperRight, 30, -486, 500, 30, false);
        letterHint = Label(paper.transform, "点一下或按 E 收起信", 12, new Color32(0xa0, 0x8a, 0x6a, 255), TextAnchor.MiddleCenter, 30, -572, 500, 24, false);
        var viewport = new GameObject("LetterBodyViewport",typeof(RectTransform),typeof(RectMask2D),typeof(Image),typeof(ScrollRect));
        letterViewport=viewport.GetComponent<RectTransform>();letterViewport.SetParent(paper.transform,false);
        SetRect(letterViewport,0,1,0,1,30,-162,500,312);letterViewport.pivot=new Vector2(0,1);
        viewport.GetComponent<Image>().color=Color.clear;
        letterContent=new GameObject("LetterContent",typeof(RectTransform)).GetComponent<RectTransform>();letterContent.SetParent(letterViewport,false);
        SetRect(letterContent,0,1,0,1,0,0,500,312);letterContent.pivot=new Vector2(0,1);
        letterBody.transform.SetParent(letterContent, false);
        letterBody.rectTransform.anchoredPosition = new Vector2(0, -LetterBodyPadding);
        letterScroll = viewport.GetComponent<ScrollRect>();
        letterScroll.viewport = letterViewport;
        letterScroll.content = letterContent;
        letterScroll.horizontal = false;
        letterScroll.movementType = ScrollRect.MovementType.Clamped;
        letterScroll.scrollSensitivity = 24f;
    }

    // WP-16：标题画面、暂停、音量
    void BuildMenus(RectTransform root)
    {
        volume = PlayerPrefs.GetFloat("tidetown.volume", 1f);
        AudioListener.volume = volume;

        BuildTitleCover(root);
        BuildHandbook(root);
        RefreshSettings();
        ApplyVolume(volume);
        ApplySensitivity(ChaseCamera.Sensitivity);
    }

    // ───────────── 2026-11-16「海滨绘本邮局」：开场封面 ─────────────
    // 去掉挡住画面的大黑块：背后只有一层四周暖褐暗角，中间完全通透，潮汐镇的实时风景整个透出来；
    // 左侧黄金分割位一张半透明的邮政铭牌封面卡。主按钮"启程派件"轻轻呼吸；"邮差设置"点开才展开音量抽屉。
    void BuildTitleCover(RectTransform root)
    {
        var shade = Img(root, "TitleBackdrop", TideTownStyleUI.SoftVignetteSprite, new Color32(0x2a, 0x18, 0x0c, 235));
        Stretch(shade.rectTransform, 0f);
        shade.raycastTarget = true;
        titlePanel = shade.gameObject;

        var card = Img(shade.transform, "TitleCoverCard", TideTownStyleUI.RoundedBorderSprite, new Color32(0x2a, 0x1c, 0x12, 172));
        Pin(card.rectTransform, 0.5f, 0.5f, -205f, 0f, 540f, 440f);
        AddDropShadow(card);
        Frame(card.transform, new Color32(0xe0, 0xb4, 0x72, 110), 7f);
        var ct = card.transform;

        var wax = Img(ct, "TitleWaxSeal", TideTownStyleUI.WaxSealSprite, Color.white);
        Pin(wax.rectTransform, 0.5f, 0.5f, -219f, 176f, 44f, 44f);
        wax.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        Txt(ct, "TIDE TOWN POST OFFICE · 潮汐镇邮政总局", 13, Gold, TextAnchor.MiddleLeft, 22f, 176f, 432f, 20f, true);

        var title = Txt(ct, "《记忆邮局 · 潮汐镇》", 40, new Color32(0xff, 0xf4, 0xe2, 255), TextAnchor.MiddleLeft, 0f, 120f, 480f, 58f, true);
        var tsh = title.gameObject.AddComponent<Shadow>(); tsh.effectColor = new Color32(0x1a, 0x0e, 0x06, 170); tsh.effectDistance = new Vector2(2f, -3f);
        Txt(ct, "EGGBOY · STILL DELIVERING — 最后一位邮差", 16, new Color32(0xe8, 0xd6, 0xb4, 255), TextAnchor.MiddleLeft, 0f, 74f, 480f, 24f, false);

        // ─── ◆ ─── 细金线 + 中央菱形星花
        var l1 = Img(ct, "DividerL", null, Gold); Pin(l1.rectTransform, 0.5f, 0.5f, -183f, 42f, 114f, 1.5f);
        var l2 = Img(ct, "DividerR", null, Gold); Pin(l2.rectTransform, 0.5f, 0.5f, -57f, 42f, 114f, 1.5f);
        var gem = Img(ct, "DividerGem", null, Gold); Pin(gem.rectTransform, 0.5f, 0.5f, -120f, 42f, 8f, 8f);
        gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var gem2 = Img(ct, "DividerGemCore", null, new Color32(0x2a, 0x1c, 0x12, 255)); Pin(gem2.rectTransform, 0.5f, 0.5f, -120f, 42f, 3f, 3f);
        gem2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // 主按钮：朱砂金外圈 + 暖橘内层 + 上半一道浅高光，左边 [Enter] 键帽
        var start = Img(ct, "StartButton", TideTownStyleUI.RoundedBorderSprite, new Color32(0xb8, 0x4a, 0x22, 255));
        start.raycastTarget = true;
        Pin(start.rectTransform, 0.5f, 0.5f, -80f, -20f, 320f, 60f);
        AddDropShadow(start);
        Inset(start.transform, new Color32(0xff, 0x9a, 0x4d, 255), 3.5f, TideTownStyleUI.RoundedBorderSprite);
        var shine = Img(start.transform, "Shine", TideTownStyleUI.PillSprite, new Color32(255, 255, 255, 46));
        var srt = shine.rectTransform; srt.anchorMin = new Vector2(0f, 0.55f); srt.anchorMax = new Vector2(1f, 1f); srt.offsetMin = new Vector2(10f, 0f); srt.offsetMax = new Vector2(-10f, -6f);
        Keycap(start.transform, "Enter", -146f, 1f, 32f, 13, true);
        var startText = Txt(start.transform, "启程派件", 22, new Color32(0xff, 0xf8, 0xec, 255), TextAnchor.MiddleLeft, 5f, 0f, 170f, 40f, true);
        startText.gameObject.AddComponent<Shadow>().effectColor = new Color32(0x6a, 0x26, 0x0e, 140);
        var startBtn = start.gameObject.AddComponent<Button>();
        startBtn.targetGraphic = start;
        startBtn.transition = Selectable.Transition.ColorTint;
        startBtn.onClick.AddListener(StartFromTitle);
        titleStart = start.rectTransform;

        // 次按钮：邮差设置 ▾（默认收起）
        var settings = Img(ct, "SettingsButton", TideTownStyleUI.RoundedBorderSprite, new Color32(0x3b, 0x2a, 0x1a, 170));
        settings.raycastTarget = true;
        Pin(settings.rectTransform, 0.5f, 0.5f, -135f, -86f, 210f, 38f);
        Frame(settings.transform, new Color32(0xe0, 0xb4, 0x72, 130), 3f);
        titleSettingsText = Txt(settings.transform, "邮差设置 ▾", 15, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleCenter, 0f, 0f, 190f, 30f, false);
        var setBtn = settings.gameObject.AddComponent<Button>();
        setBtn.targetGraphic = settings;
        setBtn.onClick.AddListener(() => { titleDrawerOpen = !titleDrawerOpen; titleSettingsText.text = titleDrawerOpen ? "邮差设置 ▴" : "邮差设置 ▾"; });

        // 音量抽屉
        var drawer = Img(ct, "SettingsDrawer", TideTownStyleUI.RoundedBorderSprite, new Color32(0x3b, 0x2a, 0x1a, 215));
        Pin(drawer.rectTransform, 0.5f, 0.5f, -90f, -150f, 300f, 66f);
        drawer.rectTransform.pivot = new Vector2(0.5f, 1f);
        drawer.rectTransform.anchoredPosition = new Vector2(-90f, -117f);
        titleDrawer = drawer.gameObject.AddComponent<CanvasGroup>();
        titleDrawer.alpha = 0f; titleDrawer.interactable = false; titleDrawer.blocksRaycasts = false;
        volumeText1 = Txt(drawer.transform, "音量", 13, new Color32(0xe8, 0xd6, 0xb4, 255), TextAnchor.MiddleLeft, 0f, 16f, 250f, 20f, false);
        volumeBar1 = BuildVolume(drawer.transform, -12f, 250f);

        Txt(ct, "第 047 号海岛投递路单 · 按 Enter 或 空格 启程", 12, new Color32(0xc8, 0xb0, 0x8c, 255), TextAnchor.MiddleLeft, 0f, -196f, 480f, 20f, false);
        titlePanel.SetActive(false);
    }

    // ───────────── 2026-11-16 Esc 暂停：摊开的邮差手账本 ─────────────
    // 深棕皮质封套 + 左右两页暖米色羊皮纸、中间书脊。左页：投递手账（当前路单、设置、继续派件）；右页：操作与法宝图鉴（键帽 + 说明）。
    // 引路光点那一项由 MenuController 挂进左页的 PauseGuideSlot。
    void BuildHandbook(RectTransform root)
    {
        var shade = Img(root, "PauseBackdrop", null, new Color32(12, 9, 6, 120));
        Stretch(shade.rectTransform, 0f);
        shade.raycastTarget = true;
        var vig = Img(shade.transform, "PauseVignette", TideTownStyleUI.SoftVignetteSprite, new Color32(0x1e, 0x10, 0x06, 220));
        Stretch(vig.rectTransform, 0f);
        pausePanel = shade.gameObject;

        var book = Img(shade.transform, "Handbook", TideTownStyleUI.RoundedBorderSprite, new Color32(0x5a, 0x34, 0x1c, 255));
        Pin(book.rectTransform, 0.5f, 0.5f, 0f, 0f, 920f, 580f);
        var bsh = book.gameObject.AddComponent<Shadow>(); bsh.effectColor = new Color32(0, 0, 0, 130); bsh.effectDistance = new Vector2(6f, -9f);
        Frame(book.transform, new Color32(0xd8, 0xa8, 0x68, 90), 7f);   // 封套边上一圈压线
        var spine = Img(book.transform, "Spine", null, new Color32(0x2a, 0x16, 0x0a, 230));
        Pin(spine.rectTransform, 0.5f, 0.5f, 0f, 0f, 16f, 548f);

        var left = Img(book.transform, "LeftPage", TideTownStyleUI.RoundedBorderSprite, new Color32(0xf6, 0xea, 0xd2, 255));
        Pin(left.rectTransform, 0.5f, 0.5f, -218f, 0f, 420f, 530f);
        var right = Img(book.transform, "RightPage", TideTownStyleUI.RoundedBorderSprite, new Color32(0xf6, 0xea, 0xd2, 255));
        Pin(right.rectTransform, 0.5f, 0.5f, 218f, 0f, 420f, 530f);
        // 靠书脊那一侧压一道浅阴影
        var gl = Img(left.transform, "Gutter", null, new Color32(0x6a, 0x48, 0x28, 40)); Pin(gl.rectTransform, 1f, 0.5f, -5f, 0f, 10f, 510f);
        var gr = Img(right.transform, "Gutter", null, new Color32(0x6a, 0x48, 0x28, 40)); Pin(gr.rectTransform, 0f, 0.5f, 5f, 0f, 10f, 510f);

        // —— 左页：投递手账 ——
        var lp = left.transform;
        Txt(lp, "POSTAL LOG · 投递手账", 13, Eyebrow, TextAnchor.MiddleLeft, 0f, 232f, 360f, 20f, true);
        var rule = Img(lp, "Rule", null, new Color32(0xc9, 0xa8, 0x78, 255)); Pin(rule.rectTransform, 0.5f, 0.5f, 0f, 218f, 360f, 1f);
        var goalCard = Img(lp, "RouteCard", TideTownStyleUI.RoundedBorderSprite, new Color32(0xff, 0xf8, 0xec, 255));
        Pin(goalCard.rectTransform, 0.5f, 0.5f, 0f, 166f, 360f, 82f);
        Strip(goalCard.transform, Amber, 8f, 4f, 10f);
        Txt(goalCard.transform, "当前任务路单", 11, Eyebrow, TextAnchor.MiddleLeft, 12f, 26f, 300f, 16f, true);
        pauseGoal = Txt(goalCard.transform, GoalText, 15, InkDark, TextAnchor.UpperLeft, 12f, -10f, 300f, 46f, false);

        Txt(lp, "邮差设置", 12, Eyebrow, TextAnchor.MiddleLeft, 0f, 108f, 360f, 18f, true);
        volumeText2 = Txt(lp, "音量", 13, InkDark, TextAnchor.MiddleLeft, 0f, 86f, 340f, 20f, false);
        volumeBar2 = BuildVolume(lp, 66f, 340f);
        sensText = Txt(lp, "视角灵敏度", 13, InkDark, TextAnchor.MiddleLeft, 0f, 42f, 340f, 20f, false);
        sensBar = BuildSensitivity(lp, 22f, 340f);
        runSetting = SettingButton(lp, -18f, () => { PrototypeGame.DefaultRun = !PrototypeGame.DefaultRun; RefreshSettings(); });
        recenterSetting = SettingButton(lp, -58f, () => { ChaseCamera.AutoRecenter = !ChaseCamera.AutoRecenter; RefreshSettings(); });
        var slot = new GameObject("PauseGuideSlot", typeof(RectTransform)).GetComponent<RectTransform>();
        slot.SetParent(lp, false);
        Pin(slot, 0.5f, 0.5f, 0f, -98f, 340f, 32f);
        Txt(lp, "暂停时按 H 切换提示频率", 12, new Color32(0xa0, 0x80, 0x5c, 255), TextAnchor.MiddleLeft, 0f, -134f, 340f, 18f, false);

        var resume = Img(lp, "ResumeButton", TideTownStyleUI.RoundedBorderSprite, new Color32(0xb8, 0x4a, 0x22, 255));
        resume.raycastTarget = true;
        Pin(resume.rectTransform, 0.5f, 0.5f, 0f, -204f, 270f, 48f);
        AddDropShadow(resume);
        Inset(resume.transform, new Color32(0xff, 0x9a, 0x4d, 255), 3f, TideTownStyleUI.RoundedBorderSprite);
        Txt(resume.transform, "继续派件", 19, new Color32(0xff, 0xf8, 0xec, 255), TextAnchor.MiddleCenter, -22f, 0f, 160f, 34f, true);
        Keycap(resume.transform, "Esc", 74f, 0f, 28f, 12, false);
        var resumeBtn = resume.gameObject.AddComponent<Button>();
        resumeBtn.targetGraphic = resume;
        resumeBtn.onClick.AddListener(() => SetPaused(false));

        // —— 右页：操作与法宝图鉴 ——
        var rp = right.transform;
        Txt(rp, "FIELD GUIDE · 操作与法宝图鉴", 13, Eyebrow, TextAnchor.MiddleLeft, 0f, 232f, 360f, 20f, true);
        var rule2 = Img(rp, "Rule", null, new Color32(0xc9, 0xa8, 0x78, 255)); Pin(rule2.rectTransform, 0.5f, 0.5f, 0f, 218f, 360f, 1f);
        var sections = new[]
        {
            ("基础行动", new[] { ("WASD", "移动"), ("Shift", "慢走 / 迎风稳住"), ("Space", "跳跃"), ("E", "互动") }),
            ("邮差法宝", new[] { ("Tab", "读信与拆字"), ("V", "心愿造物笺"), ("R", "潮汐怀表残影"), ("C", "透视拓印取景框") }),
            ("观察与伙伴", new[] { ("F", "指挥小黑"), ("M", "航海全图"), ("H", "目标 / 提示频率"), ("Z/X", "切换表情") }),
        };
        float sy = 194f;
        foreach (var (head, keys) in sections)
        {
            var bg = Img(rp, "Section", TideTownStyleUI.RoundedBorderSprite, new Color32(0xef, 0xdc, 0xbc, 150));
            Pin(bg.rectTransform, 0.5f, 0.5f, 0f, sy - 38f, 376f, 100f);
            var dotS = Img(rp, "SectionDot", TideTownStyleUI.PillSprite, Cinnabar); Pin(dotS.rectTransform, 0.5f, 0.5f, -172f, sy, 7f, 7f);
            Txt(rp, head, 13, new Color32(0x7a, 0x4a, 0x26, 255), TextAnchor.MiddleLeft, 0f, sy, 330f, 20f, true);
            for (int k = 0; k < keys.Length; k++)
            {
                float cx = k % 2 == 0 ? -170f : 14f, ky = sy - 32f - (k / 2) * 38f;
                var cap = Keycap(rp, keys[k].Item1, cx, ky, 28f, 12, true);
                Txt(rp, keys[k].Item2, 13, InkDark, TextAnchor.MiddleLeft, cx + cap.sizeDelta.x + 8f + 75f, ky, 150f, 22f, false);
            }
            sy -= 112f;
        }
        Txt(rp, "鼠标转视角 · 滚轮缩放 · 取景框内 Q / Z 换物", 12, new Color32(0xa0, 0x80, 0x5c, 255), TextAnchor.MiddleLeft, 0f, -150f, 360f, 18f, false);
        var stamp = Img(rp, "HandbookPostmark", TideTownStyleUI.PostmarkRingSprite, new Color32(0xc0, 0x3a, 0x2c, 80));
        Pin(stamp.rectTransform, 0.5f, 0.5f, 128f, -200f, 96f, 96f);
        stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        var seal2 = Img(rp, "HandbookSeal", TideTownStyleUI.WaxSealSprite, Color.white);
        Pin(seal2.rectTransform, 0.5f, 0.5f, -160f, -204f, 34f, 34f);
        Txt(rp, "潮汐镇邮政总局 · 邮差手册 第 47 号", 11, new Color32(0xa0, 0x80, 0x5c, 255), TextAnchor.MiddleLeft, 30f, -204f, 250f, 18f, false);

        pausePanel.SetActive(false);
    }

    // —— 绘本化用的小工具 ——
    static readonly Color32 Walnut = new Color32(0x4a, 0x30, 0x1c, 238);
    static readonly Color32 Parchment = new Color32(0xfb, 0xf3, 0xe2, 255);
    static readonly Color32 Amber = new Color32(0xff, 0x9a, 0x4d, 255);
    static readonly Color32 Eyebrow = new Color32(0xa0, 0x74, 0x48, 255);
    static readonly Color32 Gold = new Color32(0xe0, 0xb4, 0x72, 235);
    static readonly Color32 Cinnabar = new Color32(0xc0, 0x3a, 0x2c, 255);
    static readonly Color32 InkDark = new Color32(0x3b, 0x2a, 0x1a, 255);
    const float GoalTextLeft = 22f, GoalTextTop = 29f;   // 眉题往下让开内缩细线（11-16 下午实机图里被细线压住）
    Text goalEyebrow, promptKeyText, promptAction, titleSettingsText;
    Image promptInner, promptKey;
    bool promptPrimary = true, titleDrawerOpen;
    float titleDrawerT;
    RectTransform titleStart;
    CanvasGroup titleDrawer;

    Image Img(Transform parent, string name, Sprite sprite, Color32 color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite != null ? sprite : white;
        img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Pin(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
    }

    Image Inset(Transform parent, Color32 color, float pad, Sprite sprite)
    {
        var img = Img(parent, "Inner", sprite, color);
        Stretch(img.rectTransform, pad);
        return img;
    }

    void Strip(Transform parent, Color32 color, float left, float width, float vpad)
    {
        var img = Img(parent, "StampStrip", TideTownStyleUI.PillSprite, color);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(left, vpad); rt.offsetMax = new Vector2(left + width, -vpad);
    }

    void Frame(Transform parent, Color32 color, float pad)
    {
        var img = Img(parent, "Frame", TideTownStyleUI.RoundedFrameSprite, color);
        Stretch(img.rectTransform, pad);
    }

    // 上沿（anchorY = 1）或下沿（0）一道细线
    void HairLine(Transform parent, Color32 color, float anchorY, float inset, float side)
    {
        var img = Img(parent, "HairLine", null, color);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY); rt.pivot = new Vector2(0.5f, anchorY);
        rt.offsetMin = new Vector2(side, anchorY > 0.5f ? -inset - 1f : inset); rt.offsetMax = new Vector2(-side, anchorY > 0.5f ? -inset : inset + 1f);
    }

    static void AddDropShadow(Graphic g)
    {
        var sh = g.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color32(0x1a, 0x10, 0x08, 80);
        sh.effectDistance = new Vector2(2f, -3f);
    }

    // 以父物体中心为原点的文字框
    Text Txt(Transform parent, string text, int size, Color32 color, TextAnchor align, float x, float y, float w, float h, bool bold)
    {
        var t = Label(parent, text, size, color, align, 0, 0, w, h, bold);
        SetRect(t.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, x, y, w, h);
        return t;
    }

    // 立体键帽。leftEdge = true 时 x 是键帽左边缘，否则是中心；宽度按字长自适应
    RectTransform Keycap(Transform parent, string key, float x, float y, float h, int fontSize, bool leftEdge)
    {
        var cap = Img(parent, "Keycap_" + key, TideTownStyleUI.KeycapSprite, Color.white);
        var t = Label(cap.transform, key, fontSize, InkDark, TextAnchor.MiddleCenter, 0, 0, 120, h, true);
        float w = Mathf.Max(h, Mathf.Ceil(t.preferredWidth) + 16f);
        Pin(cap.rectTransform, 0.5f, 0.5f, leftEdge ? x + w * 0.5f : x, y, w, h);
        var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
        trt.offsetMin = new Vector2(0f, h * 0.16f); trt.offsetMax = Vector2.zero;   // 顶面比外框往上收，字也往上挪
        return cap.rectTransform;
    }

    // 呼吸、抽屉
    void AnimateUi()
    {
        float t = Time.unscaledTime;
        if (promptKey != null && promptPanel != null && promptPanel.activeSelf)
            promptKey.rectTransform.localScale = Vector3.one * (promptPrimary ? 1f + 0.04f * Mathf.Sin(t * 3.2f) : 1f);
        if (titleShowing && titleStart != null)
            titleStart.localScale = Vector3.one * (1f + 0.022f * Mathf.Sin(t * 2.4f));
        if (titleDrawer != null)
        {
            titleDrawerT = Mathf.MoveTowards(titleDrawerT, titleDrawerOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.22f);
            float e = Mathf.SmoothStep(0f, 1f, titleDrawerT);
            titleDrawer.alpha = e;
            titleDrawer.interactable = titleDrawer.blocksRaycasts = e > 0.5f;
            titleDrawer.transform.localScale = new Vector3(1f, 0.6f + 0.4f * e, 1f);
        }
    }

    Text CenterLabel(Transform parent, string text, int size, Color32 color, float y, float h, bool bold)
    {
        var t = Label(parent, text, size, color, TextAnchor.MiddleCenter, 0, 0, 720, h, bold);
        SetRect(t.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, y, 720, h);
        return t;
    }

    Text SettingButton(Transform parent,float y,Action toggle)
    {
        // 2026-11-16：手账左页的设置项：暖米底 + 双细线框，深褐字靠左（和 MenuController 的"引路光点"同一个样式）
        var card=CenterPanel(parent,340,32,y,new Color32(0xf3,0xe1,0xc0,255));
        var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card;button.onClick.AddListener(()=>toggle());
        var label=CenterLabel(card.transform,"",14,InkDark,0,30,false);
        TideTownStyleUI.StyleSettingButton(button);
        return label;
    }
    void RefreshSettings()
    {
        if(runSetting!=null)runSetting.text="默认移动："+(PrototypeGame.DefaultRun?"奔跑":"慢走");
        if(recenterSetting!=null)recenterSetting.text="镜头自动回正："+(ChaseCamera.AutoRecenter?"开":"关");
    }
    public void SetCinematic(bool on,string owner="story")
    {
        if(on)cinematicOwners.Add(owner);else cinematicOwners.Remove(owner);
        RefreshHud();
    }
    void RefreshHud() { if(hud!=null)hud.SetActive(!Cinematic&&!LetterOpen&&!Paused&&!titleShowing); }
    public void RecallGuide()
    {
        if(Paused||Cinematic||LetterOpen||titleShowing||MenuController.OverlayOpen)return;
        keysRemaining=goalRemaining=6f;
    }
    public bool ShowFirstTutorial(string text,KeyCode key,Action completed)
    {
        if(FirstTutorialActive)return false;
        if(!inputClose){completed?.Invoke();return true;}
        SetTutorial(text);firstTutorialKey=key;firstTutorialRemaining=8f;firstTutorialDone=completed;
        return true;
    }
    void UpdateGuide()
    {
        // Tab / M / C 本帧可能已经打开模态界面，仍然消费这次教学按键。
        if (FirstTutorialActive && !Paused && Input.GetKeyDown(firstTutorialKey)) CompleteFirstTutorial();
        bool free=!Paused&&!Cinematic&&!LetterOpen&&!titleShowing&&!MenuController.OverlayOpen;
        if(free){
            if(!freeStarted){freeStarted=true;keysRemaining=12f;}
            float dt=Time.unscaledDeltaTime;keysRemaining=Mathf.Max(0,keysRemaining-dt);goalRemaining=Mathf.Max(0,goalRemaining-dt);
            if(inputClose&&Input.GetKeyDown(KeyCode.H))RecallGuide();
            if(FirstTutorialActive){firstTutorialRemaining-=dt;if(firstTutorialRemaining<=0)CompleteFirstTutorial();}
        }
        FadeGuide(keysFade,inputClose&&keysRemaining>0f);
        FadeGuide(goalFade,!string.IsNullOrEmpty(GoalText)&&(!inputClose||goalRemaining>0f));
        RefreshHud();
    }
    void CompleteFirstTutorial()
    {
        var callback = firstTutorialDone; firstTutorialDone = null;
        SetTutorial(""); callback?.Invoke();
    }
    void FadeGuide(CanvasGroup group,bool visible)
    {
        if(group==null)return;
        group.alpha=inputClose?Mathf.MoveTowards(group.alpha,visible?1f:0f,Time.unscaledDeltaTime/.35f):(visible?1f:0f);
        group.gameObject.SetActive(group.alpha>.001f);
    }
    void RebuildLetterRules()
    {
        if(letterContent==null||letterBody==null)return;
        ruledText=letterBody.text;
        float height=Mathf.Max(letterViewport.rect.height,letterBody.preferredHeight+LetterBodyPadding+8f);
        letterContent.sizeDelta=new Vector2(500,height);letterBody.rectTransform.sizeDelta=new Vector2(500,height-LetterBodyPadding);
        Canvas.ForceUpdateCanvases();
        var generator=letterBody.cachedTextGenerator;generator.Populate(letterBody.text,letterBody.GetGenerationSettings(letterBody.rectTransform.rect.size));
        var lines=generator.lines;var verts=generator.verts;float units=letterBody.pixelsPerUnit;
        for(int i=0;i<lines.Count;i++){
            if(letterRules.Count<=i){var line=Panel(letterContent,0,0,500,1,new Color32(233,220,192,210),false,false);line.raycastTarget=false;line.transform.SetAsFirstSibling();letterRules.Add(line.rectTransform);}
            // 使用这一渲染行实际字形的下缘，不按固定 32px 间隔猜测字体基线。
            int first=lines[i].startCharIdx*4,last=(i+1<lines.Count?lines[i+1].startCharIdx*4:verts.Count-4);
            float y=float.PositiveInfinity;
            for(int v=first;v<Mathf.Min(last,verts.Count-4);v++)if(verts[v].position!=Vector3.zero)y=Mathf.Min(y,verts[v].position.y/units);
            if(float.IsInfinity(y))y=(lines[i].topY-lines[i].height)/units+3f;
            var rt=letterRules[i];SetRect(rt,0,1,0,1,0,y-LetterBodyPadding-2f,500,1);rt.pivot=new Vector2(0,1);rt.gameObject.SetActive(true);
        }
        for(int i=lines.Count;i<letterRules.Count;i++)letterRules[i].gameObject.SetActive(false);
    }

    Image CenterPanel(Transform parent, float w, float h, float y, Color32 color)
    {
        var img = Panel(parent, 0, 0, w, h, color, true, false);
        SetRect(img.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, y, w, h);
        return img;
    }

    GameObject AddMenuBackdrop(RectTransform canvasRoot, Image card)
    {
        var shade = Panel(canvasRoot, 0, 0, RefW, RefH, new Color32(10, 12, 16, 160), false, true);
        SetRect(shade.rectTransform, 0f, 0f, 1f, 1f, 0, 0, 0, 0);
        card.transform.SetParent(shade.transform, false);
        return shade.gameObject;
    }

    // 2026-11-16：滑条换成胶囊轨道 + 暖橘填充 + 带细描边的米白圆钮
    void StyleSlider(Image bar, Image fill, Image handle)
    {
        bar.sprite = TideTownStyleUI.PillSprite; bar.type = Image.Type.Sliced; bar.color = new Color32(0x6a, 0x4c, 0x32, 255);
        fill.sprite = TideTownStyleUI.PillSprite; fill.type = Image.Type.Sliced;
        handle.sprite = TideTownStyleUI.RoundedBorderSprite; handle.type = Image.Type.Sliced; handle.color = new Color32(0xff, 0xf3, 0xe0, 255);
        AddDropShadow(handle);
    }

    Slider BuildVolume(Transform parent, float y, float w = 300f)
    {
        var bar = CenterPanel(parent, w, 12, y, new Color32(0x3b, 0x2a, 0x1a, 255));
        var fill = CenterPanel(bar.transform, 0, 8, 0, new Color32(0xff, 0x9a, 0x4d, 255));
        var handle = CenterPanel(bar.transform, 22, 22, 0, new Color32(0xff, 0xf8, 0xec, 255));
        StyleSlider(bar, fill, handle);
        bar.raycastTarget = true;
        fill.raycastTarget = false;
        handle.rectTransform.sizeDelta = new Vector2(22f, 22f);
        var slider = bar.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = volume;
        slider.onValueChanged.AddListener(ApplyVolume);
        return slider;
    }

    void ApplyVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("tidetown.volume", volume);
        PlayerPrefs.Save();
        if (volumeBar1 != null && Mathf.Abs(volumeBar1.value - volume) > 0.001f) volumeBar1.SetValueWithoutNotify(volume);
        if (volumeBar2 != null && Mathf.Abs(volumeBar2.value - volume) > 0.001f) volumeBar2.SetValueWithoutNotify(volume);
        var label = "音量 " + Mathf.RoundToInt(volume * 100f) + "%";
        if (volumeText1 != null) volumeText1.text = label;
        if (volumeText2 != null) volumeText2.text = label;
    }

    // 视角灵敏度：0.4–2.0 倍，存 PlayerPrefs，和 ChaseCamera 的 MouseSens 相乘
    Slider BuildSensitivity(Transform parent, float y, float w = 300f)
    {
        var bar = CenterPanel(parent, w, 12, y, new Color32(0x3b, 0x2a, 0x1a, 255));
        var fill = CenterPanel(bar.transform, 0, 8, 0, new Color32(0xff, 0x9a, 0x4d, 255));
        var handle = CenterPanel(bar.transform, 22, 22, 0, new Color32(0xff, 0xf8, 0xec, 255));
        StyleSlider(bar, fill, handle);
        bar.raycastTarget = true;
        fill.raycastTarget = false;
        handle.rectTransform.sizeDelta = new Vector2(22f, 22f);
        var slider = bar.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0.4f;
        slider.maxValue = 2f;
        slider.value = ChaseCamera.Sensitivity;
        slider.onValueChanged.AddListener(ApplySensitivity);
        return slider;
    }

    void ApplySensitivity(float v)
    {
        ChaseCamera.Sensitivity = v;
        float s = ChaseCamera.Sensitivity;
        if (sensBar != null && Mathf.Abs(sensBar.value - s) > 0.001f) sensBar.SetValueWithoutNotify(s);
        if (sensText != null) sensText.text = "视角灵敏度 " + Mathf.RoundToInt(s * 100f) + "%";
    }

    public void ShowTitle(System.Action onStart)
    {
        onTitleStart = onStart;
        titleShowing = true;
        hud.SetActive(false);
        MenuController.ReleasePointer();
        if (titlePanel != null) titlePanel.SetActive(true);
        ApplyVolume(volume);
    }

    void StartFromTitle()
    {
        if (!titleShowing) return;
        titleShowing = false;
        RefreshHud();
        if (titlePanel != null) titlePanel.SetActive(false);
        var cb = onTitleStart;
        onTitleStart = null;
        if (cb != null) cb();
        MenuController.RestorePointer(this);
    }

    public void SetPaused(bool on)
    {
        if (Paused == on) return;
        Paused = on;
        RefreshHud();
        if (pausePanel != null) pausePanel.SetActive(on);
        if (on)
        {
            ApplyVolume(volume);
            ApplySensitivity(ChaseCamera.Sensitivity);
            RefreshSettings();
        }
        Time.timeScale = on ? 0f : 1f;
        if (player != null) player.locked = on || LetterOpen || MenuController.OverlayOpen;
        if (on)
        {
            MenuController.ReleasePointer();
        }
        else MenuController.RestorePointer(this);
    }

    public void SetGoal(string text)
    {
        if (goalText == null || goalText.text == text) return;
        goalRemaining = 5.5f;
        goalText.text = text;
        if (pauseGoal != null) pauseGoal.text = text;
        bool visible = !string.IsNullOrEmpty(text);
        goalPanel.SetActive(visible);
        if (!visible) return;

        var textRect = goalText.rectTransform;
        float width = Mathf.Min(GoalMaxTextWidth, Mathf.Ceil(goalText.preferredWidth) + 2f);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        float height = Mathf.Max(22f, Mathf.Ceil(goalText.preferredHeight));
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        // 2026-11-16：路单卡 = 左边邮戳竖条 + 眉题一行 + 目标文字，宽度跟着文字（不比眉题窄）
        float inner = Mathf.Max(width, goalEyebrow != null ? Mathf.Ceil(goalEyebrow.preferredWidth) : 0f);
        ((RectTransform)goalPanel.transform).sizeDelta = new Vector2(GoalTextLeft + inner + GoalPaddingX + 2f, GoalTextTop + height + GoalPaddingY + 4f);
    }

    public string GoalText => goalText != null ? goalText.text : "";

    // 序章教学一行；空串即隐藏
    public void SetTutorial(string text)
    {
        if(FirstTutorialActive)return;
        if (tutorialPanel == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (on && tutorialText != null) tutorialText.text = text;
        tutorialPanel.SetActive(on);
    }

    public string PromptText => promptText != null && promptPanel != null && promptPanel.activeSelf ? promptText.text : "";

    public void SetPrompt(string text, bool isPrimary = true)
    {
        if (promptPanel == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (on) promptText.text = text;
        promptPrimary = isPrimary;
        // 2026-11-16：拆成键帽 + 动作名。"E　取信" → [E] 取信；"按住 E　点燃" → [E] 按住 点燃；拆不开的整句照原样放胶囊里
        string full = promptText.text ?? "", key = null, action = full;
        var m = System.Text.RegularExpressions.Regex.Match(full, @"^\s*(按住|按)?\s*(Esc|Tab|Shift|Space|Enter|[A-Za-z0-9]{1,3}|[A-Za-z]/[A-Za-z])[\s　]+(.+)$");
        if (m.Success)
        {
            key = m.Groups[2].Value.Length == 1 ? m.Groups[2].Value.ToUpperInvariant() : m.Groups[2].Value;
            action = (m.Groups[1].Value == "按住" ? "按住 " : "") + m.Groups[3].Value.Trim();
        }
        float h = isPrimary ? 46f : 34f, keyH = isPrimary ? 34f : 26f;
        var image = promptPanel.GetComponent<Image>();
        image.color = isPrimary ? (Color)Walnut : (Color)new Color32(45, 36, 28, 170);
        if (promptInner != null) promptInner.gameObject.SetActive(isPrimary);
        float keyW = 0f;
        promptKey.gameObject.SetActive(key != null);
        if (key != null)
        {
            promptKeyText.text = key; promptKeyText.fontSize = isPrimary ? 15 : 12;
            keyW = Mathf.Max(keyH, Mathf.Ceil(promptKeyText.preferredWidth) + 16f);
            promptKey.color = isPrimary ? Color.white : new Color(1f, 1f, 1f, 0.85f);
        }
        promptAction.text = action;
        promptAction.fontSize = isPrimary ? 17 : 13;
        promptAction.fontStyle = isPrimary ? FontStyle.Bold : FontStyle.Normal;
        promptAction.color = isPrimary ? (Color)InkDark : (Color)new Color32(251, 239, 217, 215);
        float actW = Mathf.Min(460f, Mathf.Ceil(promptAction.preferredWidth) + 2f);
        float textX = key != null ? 7f + keyW + 10f : 22f;
        SetRect(promptPanel.GetComponent<RectTransform>(), .5f, 0, .5f, 0, 0, isPrimary ? 106 : 76, textX + actW + 22f, h);
        var krt = promptKey.rectTransform;
        krt.anchorMin = krt.anchorMax = new Vector2(0f, 0.5f); krt.pivot = new Vector2(0.5f, 0.5f);
        krt.anchoredPosition = new Vector2(7f + keyW * 0.5f, 0f); krt.sizeDelta = new Vector2(keyW, keyH);
        var trt = promptKeyText.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
        trt.offsetMin = new Vector2(0f, keyH * 0.16f); trt.offsetMax = Vector2.zero;
        var art = promptAction.rectTransform;
        art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f); art.pivot = new Vector2(0f, 0.5f);
        art.anchoredPosition = new Vector2(textX, 0f); art.sizeDelta = new Vector2(actW, h - 8f);
        promptPanel.SetActive(on);
    }

    public void SetActiveExpression(ExprId id)
    {
        activeExpr = id;
        int idx = Array.IndexOf(EggboyExpressions.All, id);
        for (int i = 0; i < faceBg.Count; i++)
        {
            bool on = i == idx;
            faceBg[i].color = on ? new Color32(0xff, 0x9a, 0x4d, 255) : new Color32(0xff, 0xf8, 0xec, 255);
            faceLabel[i].color = on ? new Color32(255, 255, 255, 255) : new Color32(0x3b, 0x2a, 0x1a, 255);
        }
    }

    public void SetLetterTaken(bool taken) => letterTaken = taken;

    public void SetCarried(string text)
    {
        if (carryPanel == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (on && carryText != null) carryText.text = text;
        carryPanel.SetActive(on);
    }

    // WP-15：结局字幕
    public string SubtitleText => subtitleText != null && subtitlePanel != null && subtitlePanel.activeSelf ? subtitleText.text : "";

    public void ShowSubtitle(string text)
    {
        if (subtitlePanel == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (on && subtitleText != null) subtitleText.text = text;
        subtitlePanel.SetActive(on);
    }

    public void ShowToast(string text, float seconds = 4.5f)
    {
        if (toastPanel == null) return;
        if (toastText != null) toastText.text = text;
        toastPanel.SetActive(true);
        toastUntil = Time.unscaledTime + seconds;
    }

    public void ShowLetter()
    {
        if (letterOverlay == null) return;
        LetterOpen = true;
        letterOverlay.SetActive(true);
        SetCinematic(true,"letter");MenuController.ReleasePointer();
        letterScroll.StopMovement();
        letterContent.anchoredPosition = Vector2.zero;
        RebuildLetterRules();
    }

    // WP-07：信正文从 Resources/Story/letters.json 读，旧场景仍可调无参版本
    public void ShowLetter(string title, string body, string sign)
    {
        Chrome(true, true, "点一下或按 E 收起信");
        Fill(title, body, sign);
        ShowLetter();
    }

    // B0 重读单封信：不显示“三封信”的清单
    public void ShowLetterDetail(string title, string body, string sign)
    {
        Chrome(false, true, "点一下或按 E 收起信");
        Fill(title, body, sign);
        ShowLetter();
    }

    // WP-11 回忆线索：没有落款，用“回忆 · 门铃”做标题
    public void ShowMemory(string title, string body)
    {
        Chrome(false, false, "点一下或按 E 收起");
        Fill(title, body, null);
        ShowLetter();
    }

    void Fill(string title, string body, string sign)
    {
        if (letterTitle != null && !string.IsNullOrEmpty(title)) letterTitle.text = title;
        if (letterBody != null && !string.IsNullOrEmpty(body)) letterBody.text = body;
        if (letterSign != null && !string.IsNullOrEmpty(sign)) letterSign.text = sign;
    }

    void Chrome(bool list, bool sign, string hint)
    {
        if (letterIntro1 != null) letterIntro1.gameObject.SetActive(list);
        if (letterIntro2 != null) letterIntro2.gameObject.SetActive(list);
        if (letterSign != null) letterSign.gameObject.SetActive(sign);
        if (letterHint != null && hint != null) letterHint.text = hint;
    }

    public void CloseLetter()
    {
        if (!LetterOpen) return;
        LetterOpen = false;
        letterOverlay.SetActive(false);
        SetCinematic(false,"letter");
        OnLetterClosed?.Invoke();
        MenuController.RestorePointer(this);
    }

    // 自动测试用：信纸只由剧情代码收，不会被键盘或点击误关
    public void SetAuditMode(bool on)
    {
        inputClose = !on;
        if (letterCloseBtn != null) letterCloseBtn.interactable = !on;
    }

    void Update()
    {
        UpdateGuide();
        AnimateUi();
        if(LetterOpen&&ruledText!=letterBody.text)RebuildLetterRules();
        if (toastPanel != null && toastPanel.activeSelf && Time.unscaledTime >= toastUntil)
            toastPanel.SetActive(false);
        if (titleShowing)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                StartFromTitle();
            return;
        }
        if (Paused) return;
        if (player == null || world == null) return;
        if (LetterOpen)
        {
            if (inputClose && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
                CloseLetter();
            return;
        }
        if (!legacyPrompt) return;
        bool near = Vector3.Distance(player.transform.position, world.MailboxPos) < 1.4f;
        SetPrompt(near && !letterTaken ? "E　取信" : null);
    }

    // —— uGUI 小工具 ——
    Image Panel(Transform parent, float x, float y, float w, float h, Color32 color, bool round = true, bool right = false)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = round ? rounded : white;
        img.type = round ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        float ax = right ? 1f : 0f;
        SetRect(rt, ax, 1f, ax, 1f, right ? x : x, y, w, h);
        rt.pivot = new Vector2(right ? 1f : 0f, 1f);
        return img;
    }

    Text Label(Transform parent, string text, int size, Color32 color, TextAnchor align, float x, float y, float w, float h, bool bold)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.color = color;
        t.alignment = align;
        t.text = text;
        t.supportRichText = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.3f;
        t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        SetRect(rt, 0f, 1f, 0f, 1f, x, y, w, h);
        rt.pivot = new Vector2(0f, 1f);
        return t;
    }

    static void SetRect(RectTransform rt, float ax, float ay, float bx, float by, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(bx, by);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static Sprite MakeRounded()
    {
        const int S = 64, R = 14;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false, false);
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = Mathf.Max(R - x - 0.5f, 0f, x + 0.5f - (S - R));
            float dy = Mathf.Max(R - y - 0.5f, 0f, y + 0.5f - (S - R));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            byte a = (byte)(Mathf.Clamp01(R + 1f - d) * 255f);
            px[y * S + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
    }
}
