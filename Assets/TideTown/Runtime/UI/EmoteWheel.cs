// 2026-11-16 用户议题 3：按住鼠标中键呼出「表情与动作轮盘」（GTA 武器轮盘式）。
//   · 单页双圈：内圈 8 个全身动作，外圈 16 个面屏表情。按住中键时鼠标移动驱动一个虚拟指针（镜头冻结），
//     推得近选内圈、推得远选外圈，中心留一块死区；松开中键立刻执行，停在死区里松开 = 取消。
//   · 指向的扇区往外推一点、放大 1.08、换成暖橘色，中心圆显示它的大图标和中文名。
//   · 动作照旧自动配表情；选表情 = 换"常驻表情"（和 Z / X 一样），保持到下一次自动换表情为止。
//   · 摸小黑：只有小黑跟着、就在身边（2 m 内）时能用，否则这一格变灰，松开会提示"小黑不在身边"。
//   · 剧情锁定、读信、暂停、地图、别的弹窗、人在空中时呼不出来。时间照常走。
// 画风：羊皮纸圆盘 + 胡桃木细边 + 金细线分格，图标是程序画的：表情直接用面屏同一套画法，动作是手绘线描。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class EmoteWheel : MonoBehaviour
{
    static EmoteWheel instance;
    public static bool IsOpen => instance != null && instance.open;

    sealed class Item { public string name, label; public bool expr; public ExprId id; public int repeat = 1; public Image sector, icon; public Text text; public RectTransform rt; }

    static readonly (string clip, string label, int repeat)[] Actions =
    {
        ("wave", "挥手", 2), ("hop", "欢跳", 1), ("nod", "点头", 1), ("shake", "摇头", 1),
        ("look", "张望", 1), ("give", "递礼", 1), ("pet_dog", "摸小黑", 1), ("signal_wave", "招呼", 1),
    };

    const float R = 250f, InnerLo = 70f, InnerHi = 140f, OuterLo = 146f, Dead = 46f;

    EggboyPlayer player; EggboyUI ui; PrototypeGame game; StoryDirector story; TownMap map; DogFollower dog; ChaseCamera chase;
    bool open, restoreChase;
    float openT;
    Vector2 pointer;
    int hover = -1;
    readonly List<Item> inner = new List<Item>(), outer = new List<Item>();
    RectTransform root, wheel, pointerDot;
    Image dim, centerIcon; Text centerLabel, centerHint;
    static Sprite ringInner, ringOuter, discSprite, thinRing;

    // 审计 / 测试用
    public string LastFired { get; private set; } = "";
    public static EmoteWheel Instance => instance;

    public void Init(EggboyPlayer p, EggboyUI u)
    {
        instance = this; player = p; ui = u;
        game = FindObjectOfType<PrototypeGame>();
        story = FindObjectOfType<StoryDirector>();
        map = FindObjectOfType<TownMap>();
    }

    bool CanOpen()
    {
        if (player == null || ui == null || !ui.inputClose) return false;
        if (ui.Paused || ui.TitleShowing || ui.LetterOpen) return false;
        if (player.locked || player.ActionLocked || !player.grounded) return false;
        if (MenuController.OverlayOpen) return false;
        if (map == null) map = FindObjectOfType<TownMap>();
        if (map != null && map.IsOpen) return false;
        if (story != null && (story.Cutscene || story.Climbing || story.Ended)) return false;
        var pk = PhysicalPickupController.Instance; if (pk != null && pk.Busy) return false;
        return true;
    }

    // 截图验收用：不靠鼠标把轮盘摆出来、指着第 hoverIndex 格
    bool debugHold;
    public void DebugShow(int hoverIndex)
    {
        EnsureUI(); if (root == null) return;
        open = true; debugHold = true; openT = 1f; hover = hoverIndex;
        root.gameObject.SetActive(true); wheel.localScale = Vector3.one; dim.color = new Color(0.10f, 0.06f, 0.03f, 0.34f);
        var it = ItemAt(hoverIndex);
        if (it != null) pointer = it.rt.anchoredPosition; pointerDot.anchoredPosition = pointer;
        Restyle();
    }
    public void DebugHide() { debugHold = false; Close(false); }

    void Update()
    {
        if (debugHold) return;
        if (!open)
        {
            if (Input.GetMouseButtonDown(2) && CanOpen()) Open();
            return;
        }
        // 开着的时候被别的东西打断（暂停、剧情锁）就直接收起，不执行
        if (ui.Paused || ui.LetterOpen || player.locked || MenuController.OverlayOpen) { Close(false); return; }
        openT += Time.unscaledDeltaTime;
        float s = Mathf.Lerp(0.86f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(openT / 0.12f)));
        wheel.localScale = Vector3.one * s;
        dim.color = new Color(0.10f, 0.06f, 0.03f, 0.34f * Mathf.Clamp01(openT / 0.12f));

        // 虚拟指针：鼠标锁定时 Input.mousePosition 不动，用原始位移累加
        float mx = Input.GetAxisRaw("Mouse X"), my = Input.GetAxisRaw("Mouse Y");
        pointer += new Vector2(mx, my) * 14f;
        if (pointer.magnitude > R * 1.02f) pointer = pointer.normalized * R * 1.02f;
        pointerDot.anchoredPosition = pointer;

        int h = Pick(pointer);
        if (h != hover) { hover = h; ShadowAndChimeSystem.PlayProceduralChime(h < 0 ? 392f : 1046.5f, 0.03f); Restyle(); }
        if (!Input.GetMouseButton(2)) Close(true);
    }

    int Pick(Vector2 v)
    {
        float d = v.magnitude;
        if (d < Dead) return -1;
        float ang = Mathf.Repeat(Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg, 360f);   // 从正上方顺时针
        if (d < (InnerHi + OuterLo) * 0.5f) return Mathf.FloorToInt(ang / (360f / inner.Count)) % inner.Count;
        return inner.Count + Mathf.FloorToInt(ang / (360f / outer.Count)) % outer.Count;
    }

    Item ItemAt(int i) => i < 0 ? null : i < inner.Count ? inner[i] : outer[i - inner.Count];

    bool Disabled(Item it) => it != null && !it.expr && it.name == "pet_dog" && !DogNear();
    bool DogNear()
    {
        if (dog == null) dog = FindObjectOfType<DogFollower>();
        return dog != null && player != null && dog.CanBePetted(player.transform.position);
    }

    void Open()
    {
        EnsureUI();
        if (root == null) return;
        open = true; openT = 0f; pointer = Vector2.zero; hover = -1;
        chase = FindObjectOfType<ChaseCamera>();
        restoreChase = chase != null && chase.enabled;
        if (restoreChase) chase.enabled = false;
        root.gameObject.SetActive(true);
        Restyle();
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.06f);
    }

    void Close(bool fire)
    {
        open = false;
        if (root != null) root.gameObject.SetActive(false);
        if (restoreChase && chase != null) chase.enabled = true;
        restoreChase = false;
        var it = ItemAt(hover);
        hover = -1;
        if (fire && it != null) Fire(it);
    }

    // 不经过鼠标直接执行某一格（审计用）：name = 动作片段名，或 "expr:<表情>"
    public bool FireByName(string name)
    {
        foreach (var it in inner) if (it.name == name) { if (Disabled(it)) return false; Fire(it); return true; }
        foreach (var it in outer) if ("expr:" + it.id == name) { Fire(it); return true; }
        return false;
    }

    void Fire(Item it)
    {
        LastFired = it.expr ? "expr:" + it.id : it.name;
        if (it.expr)
        {
            if (game == null) game = FindObjectOfType<PrototypeGame>();
            if (game != null) game.SetBaseExpression(it.id);
            return;
        }
        if (it.name == "pet_dog")
        {
            if (!DogNear()) { ui.ShowToast("小黑不在身边。它在学校那边等你。", 2.5f); LastFired = "pet_dog_unavailable"; return; }
            player.FaceTowards(dog.transform.position);
            dog.PetByPlayer();
        }
        if (!player.HasClip(it.name)) { LastFired = "missing:" + it.name; return; }
        player.PlayEmote(it.name, it.repeat);
    }

    // ---------------- 外观 ----------------
    static readonly Color32 Paper = new Color32(0xfb, 0xf3, 0xe2, 242);
    static readonly Color32 PaperInner = new Color32(0xf1, 0xdf, 0xbd, 245);
    static readonly Color32 Amber = new Color32(0xf0, 0x8c, 0x3c, 250);
    static readonly Color32 Walnut = new Color32(0x4a, 0x30, 0x1c, 255);
    static readonly Color32 Gold = new Color32(0xc9, 0x9a, 0x52, 230);

    void Restyle()
    {
        for (int i = 0; i < inner.Count + outer.Count; i++)
        {
            var it = ItemAt(i); bool on = i == hover; bool off = Disabled(it);
            it.sector.color = on ? (Color)(off ? new Color32(0xb8, 0xa8, 0x90, 250) : Amber) : (Color)(i < inner.Count ? PaperInner : Paper);
            it.sector.rectTransform.localScale = Vector3.one * (on ? 1.06f : 1f);
            float a = off ? 0.35f : 1f;
            it.icon.color = new Color(1f, 1f, 1f, a);
            it.rt.localScale = Vector3.one * (on ? 1.18f : 1f);
            if (it.text != null) it.text.color = on ? new Color32(0xff, 0xf8, 0xec, 255) : new Color32(0x3b, 0x2a, 0x1a, (byte)(255 * a));
        }
        var cur = ItemAt(hover);
        bool current = cur != null && cur.expr && game != null && game.BaseExpression == cur.id;
        centerIcon.enabled = cur != null;
        if (cur != null) centerIcon.sprite = cur.icon.sprite;
        centerIcon.color = Disabled(cur) ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        centerLabel.text = cur == null ? "表情 · 动作" : cur.label + (current ? "（当前）" : "");
        centerHint.text = cur == null ? "移动鼠标选择　松开中键取消" : Disabled(cur) ? "小黑不在身边" : "松开中键" + (cur.expr ? "换上这个表情" : "做这个动作");
    }

    void EnsureUI()
    {
        if (root != null) return;
        var hud = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (hud == null) return;
        BuildSprites();
        var go = new GameObject("EmoteWheel", typeof(RectTransform));
        go.transform.SetParent(hud, false);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();

        dim = TideTownStyleUI.CreatePanel(root, "WheelDim", 10f, 10f, new Color32(26, 16, 8, 80), false);
        var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.sprite = TideTownStyleUI.SoftVignetteSprite;
        dim.raycastTarget = false;

        var w = new GameObject("Wheel", typeof(RectTransform)); w.transform.SetParent(root, false);
        wheel = w.GetComponent<RectTransform>(); wheel.sizeDelta = new Vector2(R * 2f, R * 2f);

        // 底盘投影 + 胡桃木外圈
        Disc("Shadow", R * 2f + 18f, new Color32(20, 12, 6, 90), discSprite, new Vector2(4f, -8f));
        Disc("Rim", R * 2f + 8f, Walnut, discSprite, Vector2.zero);

        for (int i = 0; i < 16; i++)
            outer.Add(new Item { name = "expr:" + EggboyExpressions.All[i], label = EggboyExpressions.Names[i], expr = true, id = EggboyExpressions.All[i] });
        foreach (var a in Actions) inner.Add(new Item { name = a.clip, label = a.label, repeat = a.repeat });

        BuildRing(outer, ringOuter, OuterLo, R, 46f, false);
        BuildRing(inner, ringInner, InnerLo, InnerHi, 40f, true);
        Disc("OuterLine", R * 2f, Gold, thinRing, Vector2.zero);
        Disc("MidLine", OuterLo * 2f, Gold, thinRing, Vector2.zero);
        Disc("InnerLine", InnerHi * 2f, Gold, thinRing, Vector2.zero);

        // 中心：当前选中的大图标 + 名字
        Disc("Center", InnerLo * 2f - 6f, Paper, discSprite, Vector2.zero);
        Disc("CenterLine", InnerLo * 2f - 6f, Gold, thinRing, Vector2.zero);
        centerIcon = TideTownStyleUI.CreatePanel(wheel, "CenterIcon", 62f, 62f, Color.white, false);
        centerIcon.rectTransform.anchoredPosition = new Vector2(0f, 14f); centerIcon.raycastTarget = false; centerIcon.preserveAspect = true;
        centerLabel = TideTownStyleUI.CreateText(wheel, "", 16, TideTownStyleUI.InkDark, TextAnchor.MiddleCenter, new Vector2(0f, -28f), new Vector2(150f, 24f), true);
        centerHint = TideTownStyleUI.CreateText(root, "", 15, new Color32(0xff, 0xf3, 0xdc, 255), TextAnchor.MiddleCenter, new Vector2(0f, -R - 34f), new Vector2(520f, 26f), true);
        var hs = centerHint.gameObject.AddComponent<Shadow>(); hs.effectColor = new Color32(30, 18, 8, 200); hs.effectDistance = new Vector2(1.5f, -1.5f);
        var title = TideTownStyleUI.CreateText(root, "按住中键 · 表情与动作", 15, new Color32(0xff, 0xf3, 0xdc, 255), TextAnchor.MiddleCenter, new Vector2(0f, R + 30f), new Vector2(520f, 26f), true);
        var ts = title.gameObject.AddComponent<Shadow>(); ts.effectColor = new Color32(30, 18, 8, 200); ts.effectDistance = new Vector2(1.5f, -1.5f);

        var pd = TideTownStyleUI.CreatePanel(wheel, "Pointer", 14f, 14f, new Color32(0xc0, 0x3a, 0x2c, 235), false);
        pd.sprite = discSprite; pd.raycastTarget = false; pointerDot = pd.rectTransform;
        root.gameObject.SetActive(false);
    }

    void Disc(string name, float size, Color32 c, Sprite sp, Vector2 offset)
    {
        var img = TideTownStyleUI.CreatePanel(wheel, name, size, size, c, false);
        img.sprite = sp; img.raycastTarget = false; img.rectTransform.anchoredPosition = offset;
    }

    void BuildRing(List<Item> items, Sprite ring, float lo, float hi, float iconSize, bool actions)
    {
        int n = items.Count; float step = 360f / n;
        var painter = actions ? null : new FacePainter();
        for (int i = 0; i < n; i++)
        {
            var it = items[i];
            var sec = TideTownStyleUI.CreatePanel(wheel, "Sector_" + it.name, R * 2f, R * 2f, Paper, false);
            sec.sprite = ring; sec.type = Image.Type.Filled; sec.fillMethod = Image.FillMethod.Radial360;
            sec.fillOrigin = (int)Image.Origin360.Top; sec.fillClockwise = true; sec.fillAmount = 1f / n - 0.004f;
            sec.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -i * step - 0.2f);
            sec.raycastTarget = false;
            it.sector = sec;
            // 分格金线
            var line = TideTownStyleUI.CreatePanel(wheel, "Sep", 1.6f, hi - lo, Gold, false);
            line.raycastTarget = false;
            float a0 = i * step * Mathf.Deg2Rad; float rm = (lo + hi) * 0.5f;
            line.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * rm;
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -i * step);
        }
        for (int i = 0; i < n; i++)
        {
            var it = items[i];
            float a = (i + 0.5f) * step * Mathf.Deg2Rad; float rm = (lo + hi) * 0.5f + (actions ? 6f : 0f);
            var holder = new GameObject("Icon_" + it.name, typeof(RectTransform)); holder.transform.SetParent(wheel, false);
            it.rt = holder.GetComponent<RectTransform>(); it.rt.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * rm;
            var icon = TideTownStyleUI.CreatePanel(holder.transform, "Img", iconSize, actions ? iconSize : iconSize * 0.82f, Color.white, false);
            icon.raycastTarget = false; icon.preserveAspect = true;
            icon.sprite = actions ? ActionIcon(it.name) : FaceIcon(painter, it.id);
            it.icon = icon;
            if (actions)
            {
                icon.rectTransform.anchoredPosition = new Vector2(0f, 7f);
                it.text = TideTownStyleUI.CreateText(holder.transform, it.label, 12, TideTownStyleUI.InkDark, TextAnchor.MiddleCenter, new Vector2(0f, -22f), new Vector2(70f, 18f), true);
            }
        }
    }

    // 表情图标：面屏同一套画法，画在一块圆角黑色面屏上
    static Sprite FaceIcon(FacePainter painter, ExprId id)
    {
        painter.Set(id);
        var src = painter.Texture.GetPixels32();
        int w = FacePainter.W, h = FacePainter.H;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x - w * .5f) - (w * .5f - 70f)), dy = Mathf.Max(0f, Mathf.Abs(y - h * .5f) - (h * .5f - 70f));
                float inside = Mathf.Clamp01(70f - Mathf.Sqrt(dx * dx + dy * dy));
                var s = src[y * w + x];
                float t = s.a / 255f;
                var bg = new Color32(0x1c, 0x17, 0x14, 255);
                px[y * w + x] = new Color32((byte)(bg.r + (s.r - bg.r) * t), (byte)(bg.g + (s.g - bg.g) * t), (byte)(bg.b + (s.b - bg.b) * t), (byte)(255 * inside));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f));
    }

    // 动作图标：手绘线描（胡桃木色），128×128，y 向下
    static Sprite ActionIcon(string clip)
    {
        var c = new PixelCanvas(128, 128); c.Clear(new Color32(0, 0, 0, 0));
        var ink = new Color32(0x4a, 0x30, 0x1c, 255); var accent = new Color32(0xe0, 0x6a, 0x2a, 255);
        void Head(float cx, float cy, float r) { c.Arc(cx, cy, r, 0f, Mathf.PI * 2f, 6f, ink); c.Line(cx - r * .35f, cy - r * .05f, cx - r * .35f, cy + r * .12f, 5f, ink); c.Line(cx + r * .35f, cy - r * .05f, cx + r * .35f, cy + r * .12f, 5f, ink); }
        switch (clip)
        {
            case "wave":
                c.Circle(64f, 70f, 20f, ink); c.Circle(64f, 70f, 14f, new Color32(0xfb, 0xf3, 0xe2, 255));
                for (int k = 0; k < 4; k++) c.Line(52f + k * 8f, 54f, 50f + k * 9f, 28f, 7f, ink);
                c.Arc(64f, 66f, 44f, -2.6f, -1.9f, 5f, accent); c.Arc(64f, 66f, 44f, -1.2f, -0.5f, 5f, accent); break;
            case "hop":
                c.Line(24f, 108f, 104f, 108f, 6f, ink); c.Arc(64f, 70f, 34f, Mathf.PI * 1.15f, Mathf.PI * 1.85f, 5f, accent);
                c.Line(64f, 88f, 64f, 26f, 8f, ink); c.Line(64f, 24f, 46f, 44f, 8f, ink); c.Line(64f, 24f, 82f, 44f, 8f, ink); break;
            case "nod":
                Head(64f, 70f, 28f); c.Line(108f, 40f, 108f, 96f, 6f, accent); c.Line(108f, 98f, 98f, 86f, 6f, accent); c.Line(108f, 98f, 118f, 86f, 6f, accent); break;
            case "shake":
                Head(64f, 62f, 28f); c.Line(30f, 108f, 98f, 108f, 6f, accent); c.Line(26f, 108f, 38f, 100f, 6f, accent); c.Line(26f, 108f, 38f, 116f, 6f, accent);
                c.Line(102f, 108f, 90f, 100f, 6f, accent); c.Line(102f, 108f, 90f, 116f, 6f, accent); break;
            case "look":
                c.Arc(64f, 92f, 50f, Mathf.PI * 1.2f, Mathf.PI * 1.8f, 7f, ink); c.Arc(64f, 36f, 50f, Mathf.PI * 0.2f, Mathf.PI * 0.8f, 7f, ink);
                c.Circle(64f, 64f, 15f, ink); c.Circle(58f, 58f, 5f, new Color32(0xfb, 0xf3, 0xe2, 255)); break;
            case "give":
                c.Line(18f, 94f, 54f, 82f, 7f, ink); c.Line(110f, 94f, 74f, 82f, 7f, ink); c.Line(54f, 82f, 74f, 82f, 7f, ink);
                c.Line(42f, 48f, 86f, 48f, 6f, accent); c.Line(86f, 48f, 86f, 76f, 6f, accent); c.Line(86f, 76f, 42f, 76f, 6f, accent); c.Line(42f, 76f, 42f, 48f, 6f, accent);
                c.Line(64f, 48f, 64f, 76f, 5f, accent); c.Arc(56f, 42f, 8f, Mathf.PI, Mathf.PI * 2f, 5f, accent); c.Arc(72f, 42f, 8f, Mathf.PI, Mathf.PI * 2f, 5f, accent); break;
            case "pet_dog":
                c.Circle(58f, 74f, 30f, ink); c.Polygon(new[] { new Vector2(32f, 58f), new Vector2(26f, 96f), new Vector2(44f, 70f) }, ink); c.Polygon(new[] { new Vector2(84f, 58f), new Vector2(90f, 96f), new Vector2(72f, 70f) }, ink);
                c.Circle(48f, 70f, 4f, new Color32(0xfb, 0xf3, 0xe2, 255)); c.Circle(68f, 70f, 4f, new Color32(0xfb, 0xf3, 0xe2, 255));
                c.Circle(96f, 30f, 9f, accent); c.Circle(110f, 30f, 9f, accent); c.Polygon(new[] { new Vector2(88f, 33f), new Vector2(118f, 33f), new Vector2(103f, 50f) }, accent); break;
            default: // signal_wave：举高的手 + 小星星
                c.Line(54f, 112f, 64f, 40f, 9f, ink); c.Circle(64f, 32f, 13f, ink);
                c.Line(96f, 20f, 96f, 44f, 5f, accent); c.Line(84f, 32f, 108f, 32f, 5f, accent); c.Line(22f, 40f, 22f, 56f, 4f, accent); c.Line(14f, 48f, 30f, 48f, 4f, accent); break;
        }
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false); c.Upload(tex);
        return Sprite.Create(tex, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
    }

    static void BuildSprites()
    {
        if (discSprite != null) return;
        ringOuter = Ring(OuterLo / R, 1f);
        ringInner = Ring(InnerLo / R, InnerHi / R);
        discSprite = Ring(0f, 1f);
        thinRing = Ring(1f - 3f / R, 1f);
    }

    // 圆环贴图（内外半径按比例），边缘抗锯齿
    static Sprite Ring(float lo, float hi)
    {
        const int N = 512; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false); var px = new Color32[N * N];
        float half = N * .5f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x + .5f - half) * (x + .5f - half) + (y + .5f - half) * (y + .5f - half)) / half;
                float a = Mathf.Clamp01((hi - d) * half) * (lo <= 0f ? 1f : Mathf.Clamp01((d - lo) * half));
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f));
    }
}
