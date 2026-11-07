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
    public bool legacyPrompt = true;   // 旧 Prototype 场景自己管邮筒提示；新框架交给 InteractionService
    // 自动测试时关掉键盘/点击收信，避免系统误触把逐行读信打断
    public bool inputClose = true;
    Button letterCloseBtn;
    Text letterTitle, letterBody, letterSign, letterIntro1, letterIntro2, letterHint;

    const int RefW = 1280, RefH = 720;
    EggboyPlayer player;
    IEggboyWorld world;
    Font font;
    Canvas canvas;
    Text goalText, promptText, lookText, tutorialText, carryText, toastText, subtitleText;
    GameObject promptPanel, letterOverlay, tutorialPanel, carryPanel, toastPanel, subtitlePanel;
    GameObject titlePanel, pausePanel, hud;
    public bool ToastVisible => toastPanel != null && toastPanel.activeInHierarchy;
    Slider volumeBar1, volumeBar2;
    Text volumeText1, volumeText2;
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

        // 标题
        Label(root, "EGGBOY · Still Delivering", 26, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.UpperLeft, 20, -14, 700, 34, true);
        Label(root, "潮汐镇 · 第一阶段漫游", 14, new Color32(0x3b, 0x2a, 0x1a, 200), TextAnchor.UpperLeft, 21, -48, 700, 24, false);

        // 目标
        var goalPanel = Panel(root, 18, -78, 620, 38, new Color32(0xff, 0xf8, 0xec, 220));
        goalText = Label(goalPanel.transform, "", 16, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.MiddleLeft, 14, -5, 590, 28, false);
        SetGoal("目标：去山口的红色邮筒取信（往左走）");

        // 序章教学提示：只在 P0_boot 显示
        var tutPanel = Panel(root, 18, -120, 620, 30, new Color32(0x3b, 0x2a, 0x1a, 185));
        tutorialText = Label(tutPanel.transform, "", 14, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleLeft, 13, -3, 594, 24, false);
        tutorialPanel = tutPanel.gameObject;
        tutorialPanel.SetActive(false);

        // 邮包 / 手上的东西（WP-11 火柴盒、柴火捆）
        var carry = Panel(root, 18, -156, 300, 30, new Color32(0x3b, 0x2a, 0x1a, 185));
        carryText = Label(carry.transform, "", 14, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleLeft, 13, -3, 274, 24, false);
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

        // 底部按键提示
        var keys = Panel(root, 0, 40, 1180, 50, new Color32(0x3b, 0x2a, 0x1a, 210), true, true);
        SetRect(keys.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, 40, 1180, 50);
        Label(keys.transform, "WASD 移动    Shift 跑    空格 跳    E 互动    M 地图    Esc 暂停\n移动鼠标转视角 · Esc 释放鼠标 · 滚轮缩放", 14,
            new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleCenter, 0, 0, 1150, 46, false);

        // 互动提示
        var promptPanelGo = Panel(root, 0, 106, 340, 40, new Color32(0xff, 0xf8, 0xec, 255), true, true).gameObject;
        promptPanel = promptPanelGo;
        SetRect(promptPanel.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 0, 106, 340, 40);
        promptText = Label(promptPanel.transform, "", 16, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.MiddleCenter, 0, 0, 320, 34, true);
        SetRect(promptText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 320, 34);
        promptPanel.SetActive(false);

        // 邮戳 / 道具提示，几秒后自己收
        var toast = Panel(root, 0, 0, 420, 38, new Color32(0x3b, 0x2a, 0x1a, 235), true, true);
        SetRect(toast.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, 158, 420, 38);
        toastText = Label(toast.transform, "", 16, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleCenter, 0, 0, 400, 32, false);
        SetRect(toastText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 400, 32);
        toastPanel = toast.gameObject;
        toastPanel.SetActive(false);

        // WP-15 结局字幕：停在画面上，不自动收
        var sub = Panel(root, 0, 0, 900, 64, new Color32(0x1a, 0x14, 0x10, 205), true, true);
        SetRect(sub.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, 214, 900, 64);
        subtitleText = Label(sub.transform, "", 22, new Color32(0xfb, 0xef, 0xd9, 255), TextAnchor.MiddleCenter, 0, 0, 860, 56, false);
        SetRect(subtitleText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 860, 56);
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
        var overlayBtn = overlay.gameObject.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(CloseLetter);
        letterCloseBtn = overlayBtn;
        letterOverlay.SetActive(false);

        var paper = Panel(overlay.transform, 0, 0, 560, 620, new Color32(0xfb, 0xf3, 0xe2, 255), true, false);
        SetRect(paper.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 560, 620);
        paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1f);
        paper.raycastTarget = false;
        var shadow = paper.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(0, 0, 0, 90);
        shadow.effectDistance = new Vector2(4f, -6f);

        // 信纸横线
        for (int i = 0; i < 9; i++)
        {
            var line = Panel(paper.transform, 0, 0, 480, 1, new Color32(0xe9, 0xdc, 0xc0, 210), false, false);
            SetRect(line.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 240 - i * 32, 480, 1);
            line.raycastTarget = false;
        }

        letterIntro1 = Label(paper.transform, "你从邮筒里取出了三封信：", 14, new Color32(0x8a, 0x6a, 0x4a, 255), TextAnchor.UpperLeft, 30, -24, 500, 24, false);
        letterIntro2 = Label(paper.transform, "· 致 面包店的烤炉\n· 致 小黑\n· 致 灯塔", 14, new Color32(0x8a, 0x6a, 0x4a, 255), TextAnchor.UpperLeft, 44, -50, 480, 62, false);
        letterTitle = Label(paper.transform, "面包店的烤炉", 24, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.UpperLeft, 30, -120, 500, 34, true);
        letterBody = Label(paper.transform,
            "老伙计：\n\n四十二年，你每天早上五点就热起来，比我还早。\n搬家的卡车装不下你，我只带走了你烤的最后一炉面包，路上就吃完了。\n新城的面包都用电炉烤，没有味道。\n\n邮差，如果你读到这里，请帮我再生一次火。\n不用烤什么，让它暖一暖就好。",
            18, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.UpperLeft, 30, -162, 500, 320, false);
        letterSign = Label(paper.transform, "——林阿婆", 19, new Color32(0x3b, 0x2a, 0x1a, 255), TextAnchor.UpperRight, 30, -486, 500, 30, false);
        letterHint = Label(paper.transform, "点一下或按 E 收起信", 12, new Color32(0xa0, 0x8a, 0x6a, 255), TextAnchor.MiddleCenter, 30, -572, 500, 24, false);
    }

    // WP-16：标题画面、暂停、音量
    void BuildMenus(RectTransform root)
    {
        volume = PlayerPrefs.GetFloat("tidetown.volume", 1f);
        AudioListener.volume = volume;

        var title = Panel(root, 0, 0, 860, 460, new Color32(0x1a, 0x14, 0x10, 242), true, true);
        SetRect(title.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 860, 460);
        CenterLabel(title.transform, "EGGBOY · Still Delivering", 32, new Color32(0xfb, 0xef, 0xd9, 255), 128, 46, true);
        CenterLabel(title.transform, "潮汐镇 · 最后一位邮差", 18, new Color32(0xe8, 0xd6, 0xb4, 255), 78, 30, false);
        var start = CenterPanel(title.transform, 260, 50, 8, new Color32(0xff, 0x9a, 0x4d, 255));
        var startBtn = start.gameObject.AddComponent<Button>();
        startBtn.targetGraphic = start;
        startBtn.transition = Selectable.Transition.None;
        startBtn.onClick.AddListener(StartFromTitle);
        CenterLabel(start.transform, "按 Enter 开始", 18, new Color32(0xff, 0xf8, 0xec, 255), 0, 40, true);
        volumeText1 = CenterLabel(title.transform, "音量", 14, new Color32(0xe8, 0xd6, 0xb4, 255), -86, 24, false);
        volumeBar1 = BuildVolume(title.transform, -118);
        titlePanel = AddMenuBackdrop(root, title);
        titlePanel.SetActive(false);

        var pause = Panel(root, 0, 0, 520, 320, new Color32(0x1a, 0x14, 0x10, 242), true, true);
        SetRect(pause.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 520, 320);
        CenterLabel(pause.transform, "暂停", 28, new Color32(0xfb, 0xef, 0xd9, 255), 110, 40, true);
        var resume = CenterPanel(pause.transform, 220, 46, 44, new Color32(0xff, 0x9a, 0x4d, 255));
        var resumeBtn = resume.gameObject.AddComponent<Button>();
        resumeBtn.targetGraphic = resume;
        resumeBtn.transition = Selectable.Transition.None;
        resumeBtn.onClick.AddListener(() => SetPaused(false));
        CenterLabel(resume.transform, "继续", 17, new Color32(0xff, 0xf8, 0xec, 255), 0, 36, true);
        volumeText2 = CenterLabel(pause.transform, "音量", 14, new Color32(0xe8, 0xd6, 0xb4, 255), -34, 24, false);
        volumeBar2 = BuildVolume(pause.transform, -66);
        CenterLabel(pause.transform, "Esc 继续", 12, new Color32(0xa0, 0x8a, 0x6a, 255), -120, 20, false);
        pausePanel = AddMenuBackdrop(root, pause);
        pausePanel.SetActive(false);
        ApplyVolume(volume);
    }

    Text CenterLabel(Transform parent, string text, int size, Color32 color, float y, float h, bool bold)
    {
        var t = Label(parent, text, size, color, TextAnchor.MiddleCenter, 0, 0, 720, h, bold);
        SetRect(t.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, y, 720, h);
        return t;
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

    Slider BuildVolume(Transform parent, float y)
    {
        var bar = CenterPanel(parent, 300, 14, y, new Color32(0x3b, 0x2a, 0x1a, 255));
        var fill = CenterPanel(bar.transform, 0, 8, 0, new Color32(0xff, 0x9a, 0x4d, 255));
        var handle = CenterPanel(bar.transform, 22, 22, 0, new Color32(0xff, 0xf8, 0xec, 255));
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

    public void ShowTitle(System.Action onStart)
    {
        onTitleStart = onStart;
        titleShowing = true;
        hud.SetActive(false);
        if (titlePanel != null) titlePanel.SetActive(true);
        ApplyVolume(volume);
    }

    void StartFromTitle()
    {
        if (!titleShowing) return;
        titleShowing = false;
        hud.SetActive(true);
        if (titlePanel != null) titlePanel.SetActive(false);
        var cb = onTitleStart;
        onTitleStart = null;
        if (cb != null) cb();
    }

    public void SetPaused(bool on)
    {
        if (Paused == on) return;
        Paused = on;
        hud.SetActive(!on);
        if (pausePanel != null) pausePanel.SetActive(on);
        if (on) ApplyVolume(volume);
        Time.timeScale = on ? 0f : 1f;
        if (player != null) player.locked = on;
        if (on)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void SetGoal(string text)
    {
        if (goalText == null) return;
        goalText.text = text;
        if (goalText.transform.parent != null)
            goalText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    public string GoalText => goalText != null ? goalText.text : "";

    // 序章教学一行；空串即隐藏
    public void SetTutorial(string text)
    {
        if (tutorialPanel == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (on && tutorialText != null) tutorialText.text = text;
        tutorialPanel.SetActive(on);
    }

    public string PromptText => promptText != null && promptPanel != null && promptPanel.activeSelf ? promptText.text : "";

    public void SetPrompt(string text)
    {
        if (promptPanel == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (on) promptText.text = text;
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        OnLetterClosed?.Invoke();
    }

    // 自动测试用：信纸只由剧情代码收，不会被键盘或点击误关
    public void SetAuditMode(bool on)
    {
        inputClose = !on;
        if (letterCloseBtn != null) letterCloseBtn.interactable = !on;
    }

    void Update()
    {
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
