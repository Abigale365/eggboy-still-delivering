// 潮汐镇统一视觉与真实 Lux3D 资产工具库（TideTownStyleUI）。
// 严格遵循 EggboyUI.cs 的羊皮纸信件质感（#FBF3E2 暖白信纸、#E9DCC0 信笺横线、#3B2A1A 深褐墨字、#FF9A4D 暖橘邮戳色）
// 以及 100% 复用场景内真实 Lux3D 模型资产，绝不使用原生灰盒 OnGUI 或脱离画风的几何体。
using System;
using UnityEngine;
using UnityEngine.UI;

public static class TideTownStyleUI
{
    public static readonly Color32 PaperColor = new Color32(0xfb, 0xf3, 0xe2, 255);
    public static readonly Color32 PaperRuleColor = new Color32(0xe9, 0xdc, 0xc0, 210);
    public static readonly Color32 SlipColor = new Color32(0xff, 0xf9, 0xee, 255);
    public static readonly Color32 SlipBorderColor = new Color32(0xd8, 0xb8, 0x88, 255);
    public static readonly Color32 InkDark = new Color32(0x3b, 0x2a, 0x1a, 255);
    public static readonly Color32 InkMuted = new Color32(0x8a, 0x6a, 0x4a, 255);
    public static readonly Color32 BarkPanel = new Color32(0x3b, 0x2a, 0x1a, 225);
    public static readonly Color32 CreamText = new Color32(0xfb, 0xef, 0xd9, 255);
    public static readonly Color32 AmberAccent = new Color32(0xff, 0x9a, 0x4d, 255);
    public static readonly Color32 AmberSoft = new Color32(0xff, 0xdf, 0xc0, 255);

    static Font cachedFont;
    static Sprite cachedRounded;
    static Sprite cachedWhite;

    public static Font UiFont
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<Font>("Fonts/NotoSansSC-VF");
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return cachedFont;
        }
    }

    public static Sprite RoundedSprite
    {
        get
        {
            if (cachedRounded == null) cachedRounded = MakeRounded(64, 14);
            return cachedRounded;
        }
    }

    public static Sprite WhiteSprite
    {
        get
        {
            if (cachedWhite == null)
                cachedWhite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            return cachedWhite;
        }
    }

    public static RectTransform FindHudCanvasRoot(EggboyUI ui)
    {
        if (ui == null) return null;
        var t = ui.transform.Find("EGGBOY HUD");
        return t != null ? t.GetComponent<RectTransform>() : null;
    }

    public static RectTransform FindLetterOverlay(EggboyUI ui)
    {
        var root = FindHudCanvasRoot(ui);
        if (root == null || root.childCount < 2) return null;
        // 2026-11-16 UI 绘本化：信纸背后多了一层信封，信纸不再是第 0 个子物体；按名字找，找不到再走旧办法
        var named = root.Find("LetterOverlay");
        if (named != null) return named.GetComponent<RectTransform>();
        // EggboyUI.Build 中：child 0 是 GameplayHUD，child 1 是 letterOverlay
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.childCount > 0 && c.GetChild(0).GetComponent<Shadow>() != null)
                return c.GetComponent<RectTransform>();
        }
        return root.GetChild(1).GetComponent<RectTransform>();
    }

    public static RectTransform FindLetterPaper(EggboyUI ui)
    {
        var ov = FindLetterOverlay(ui);
        if (ov == null || ov.childCount == 0) return null;
        var named = ov.Find("LetterPaper");
        if (named != null) return named.GetComponent<RectTransform>();
        return ov.GetChild(0).GetComponent<RectTransform>();
    }

    // ───────────── 2026-11-16「海滨绘本邮局」程序化精灵（不依赖外部图片，第一次用时生成、之后复用） ─────────────
    static Sprite cachedBorder, cachedFrame, cachedWax, cachedPostmark, cachedKeycap, cachedVignette, cachedPill;

    static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        return t;
    }

    // 圆角矩形的有符号距离（像素，内部为负）
    static float RoundRectSdf(float x, float y, float w, float h, float r)
    {
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // 带内描边双细线的圆角卡片（灰度：底是白，两道细线是 0.62 / 0.74 灰，Image.color 一染就是同色系的深一档描边）。九宫格。
    public static Sprite RoundedBorderSprite
    {
        get
        {
            if (cachedBorder != null) return cachedBorder;
            const int S = 96; const float R = 16f;
            var tex = NewTex(S, S); var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = RoundRectSdf(x + 0.5f, y + 0.5f, S, S, R);
                    float a = Mathf.Clamp01(0.5f - d);
                    float g = 1f;
                    float l1 = 1f - Mathf.Clamp01(Mathf.Abs(d + 5.5f) - 0.6f);   // 外细线
                    float l2 = 1f - Mathf.Clamp01(Mathf.Abs(d + 8.5f) - 0.4f);   // 内细线
                    g = Mathf.Lerp(g, 0.62f, l1); g = Mathf.Lerp(g, 0.74f, l2);
                    byte v = (byte)(g * 255f);
                    px[y * S + x] = new Color32(v, v, v, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            cachedBorder = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
            return cachedBorder;
        }
    }

    // 只有两道细线、中间透明的圆角框（叠在信纸上做信笺内框）。九宫格。
    public static Sprite RoundedFrameSprite
    {
        get
        {
            if (cachedFrame != null) return cachedFrame;
            const int S = 96; const float R = 12f;
            var tex = NewTex(S, S); var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = RoundRectSdf(x + 0.5f, y + 0.5f, S, S, R);
                    float a = Mathf.Max(1f - Mathf.Clamp01(Mathf.Abs(d + 1.5f) - 0.5f), 0.6f * (1f - Mathf.Clamp01(Mathf.Abs(d + 5f) - 0.3f)));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            cachedFrame = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
            return cachedFrame;
        }
    }

    // 胶囊（两头全圆），九宫格，横向可拉长
    public static Sprite PillSprite
    {
        get
        {
            if (cachedPill != null) return cachedPill;
            const int W = 64, H = 64; const float R = 31.5f;
            var tex = NewTex(W, H); var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = RoundRectSdf(x + 0.5f, y + 0.5f, W, H, R);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            cachedPill = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
            return cachedPill;
        }
    }

    // 朱砂红火漆印：外圈波浪滴蜡边、内圈压印环、中间一颗四角星和一圈星点。本身带颜色，Image.color 用白色。
    public static Sprite WaxSealSprite
    {
        get
        {
            if (cachedWax != null) return cachedWax;
            const int S = 128; float c = S * 0.5f;
            var tex = NewTex(S, S); var px = new Color32[S * S];
            var baseCol = new Color(0.70f, 0.16f, 0.12f); var dark = new Color(0.46f, 0.08f, 0.07f); var hi = new Color(0.90f, 0.38f, 0.30f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                    float edge = 56f + 3.2f * Mathf.Sin(ang * 9f) + 1.6f * Mathf.Sin(ang * 23f + 1.3f);
                    float a = Mathf.Clamp01(edge - r + 0.5f);
                    if (a <= 0f) { px[y * S + x] = new Color32(0, 0, 0, 0); continue; }
                    var col = Color.Lerp(baseCol, hi, Mathf.Clamp01((-dx - dy) / 90f) * 0.45f);               // 左上受光
                    col = Color.Lerp(col, dark, Mathf.Clamp01((r - 44f) / 12f) * 0.55f);                     // 滴蜡边缘厚一点、暗一点
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(r - 38f) - 1.2f);                              // 压印环
                    col = Color.Lerp(col, dark, ring * 0.8f);
                    float ring2 = 1f - Mathf.Clamp01(Mathf.Abs(r - 34f) - 0.5f);
                    col = Color.Lerp(col, hi, ring2 * 0.5f);
                    // 四角星（中心）
                    float sx = Mathf.Abs(dx), sy = Mathf.Abs(dy);
                    float star = Mathf.Clamp01(1.2f - (Mathf.Pow(sx / 20f, 0.5f) + Mathf.Pow(sy / 20f, 0.5f)) * 1.0f);
                    col = Color.Lerp(col, dark, Mathf.Clamp01(star * 3f) * 0.85f);
                    // 一圈 8 颗星点
                    for (int k = 0; k < 8; k++)
                    {
                        float ka = k * Mathf.PI / 4f + Mathf.PI / 8f;
                        float ddx = dx - Mathf.Cos(ka) * 27f, ddy = dy - Mathf.Sin(ka) * 27f;
                        if (ddx * ddx + ddy * ddy < 5.5f) col = Color.Lerp(col, dark, 0.8f);
                    }
                    px[y * S + x] = new Color(col.r, col.g, col.b, a);
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            cachedWax = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return cachedWax;
        }
    }

    // 复古邮戳圆环：外粗环 + 内细环 + 中间三道横波浪线，边缘斑驳（白色，Image.color 染朱砂红、带透明度）
    public static Sprite PostmarkRingSprite
    {
        get
        {
            if (cachedPostmark != null) return cachedPostmark;
            const int S = 160; float c = S * 0.5f;
            var tex = NewTex(S, S); var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.Clamp01(Mathf.Abs(r - 70f) - 2.2f);
                    a = Mathf.Max(a, 1f - Mathf.Clamp01(Mathf.Abs(r - 60f) - 0.8f));
                    if (r < 56f)
                        for (int k = -1; k <= 1; k++)
                        {
                            float wy = k * 14f + Mathf.Sin(dx * 0.11f) * 3f;
                            a = Mathf.Max(a, (1f - Mathf.Clamp01(Mathf.Abs(dy - wy) - 1.1f)) * Mathf.Clamp01((56f - r) / 6f));
                        }
                    float mottle = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.7f + Mathf.PerlinNoise(x * 0.37f + 9f, y * 0.37f) * 0.3f;
                    a *= Mathf.Clamp01(0.35f + mottle);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            cachedPostmark = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return cachedPostmark;
        }
    }

    // 键帽：顶面暖白，底下一道深褐厚度边，顶沿一条浅高光；本身带颜色，Image.color 用白色（变暗就乘灰）。九宫格。
    public static Sprite KeycapSprite
    {
        get
        {
            if (cachedKeycap != null) return cachedKeycap;
            const int S = 64; const float R = 10f;
            var tex = NewTex(S, S); var px = new Color32[S * S];
            var body = new Color(0.36f, 0.24f, 0.15f); var face = new Color(0.98f, 0.94f, 0.86f); var faceLo = new Color(0.92f, 0.85f, 0.73f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = RoundRectSdf(x + 0.5f, y + 0.5f, S, S, R);
                    float a = Mathf.Clamp01(0.5f - d);
                    // 顶面：比外框整体往上收 6 px（下面露出厚度边），左右上各收 3 px
                    float df = RoundRectSdf(x + 0.5f - 3f, y + 0.5f - 7f, S - 6f, S - 10f, R - 2f);
                    float inFace = Mathf.Clamp01(0.5f - df);
                    var col = Color.Lerp(body, Color.Lerp(faceLo, face, (y - 7f) / (S - 10f)), inFace);
                    float highlight = (1f - Mathf.Clamp01(Mathf.Abs(df + 2f) - 0.6f)) * Mathf.Clamp01((y - S * 0.6f) / (S * 0.3f));
                    col = Color.Lerp(col, Color.white, highlight * 0.7f);
                    px[y * S + x] = new Color(col.r, col.g, col.b, a);
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            cachedKeycap = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(14, 16, 14, 14));
            return cachedKeycap;
        }
    }

    // 四周柔和暗角、中央完全通透（白色 + 透明度，Image.color 染暖褐）
    public static Sprite SoftVignetteSprite
    {
        get
        {
            if (cachedVignette != null) return cachedVignette;
            const int W = 256, H = 144;
            var tex = NewTex(W, H); var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.85f + v * v * 1.15f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.75f));
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            cachedVignette = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return cachedVignette;
        }
    }

    // 航海十字罗盘（M 键地图册右上角）：半透明羊皮纸圆盘 + 双环 16 道刻度 + 四主角（北角朱砂、其余深褐/奶白对半）+ 四个细斜角。
    // 本身带颜色，GUI 直接画；"N" 字由调用方另写，留出北角上方的位置。
    static Texture2D cachedCompass;
    public static Texture2D CompassRoseTexture
    {
        get
        {
            if (cachedCompass != null) return cachedCompass;
            const int S = 128; float c = S * 0.5f;
            var tex = NewTex(S, S); var px = new Color32[S * S];
            var ink = new Color(0.23f, 0.16f, 0.10f); var cream = new Color(0.98f, 0.95f, 0.88f); var rule = new Color(0.55f, 0.42f, 0.29f);
            var cin = new Color(0.66f, 0.18f, 0.13f); var cinHi = new Color(0.88f, 0.45f, 0.36f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                    var col = new Color(0f, 0f, 0f, 0f);
                    col = Over(col, cream, 0.80f * Mathf.Clamp01(60.5f - r));
                    float ring = Mathf.Max(1f - Mathf.Clamp01(Mathf.Abs(r - 59f) - 1.1f), 0.8f * (1f - Mathf.Clamp01(Mathf.Abs(r - 53.5f) - 0.45f)));
                    float deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float off = Mathf.Abs(Mathf.DeltaAngle(deg, Mathf.Round(deg / 22.5f) * 22.5f)) * Mathf.Deg2Rad * r;
                    if (r > 53.5f && r < 59f) ring = Mathf.Max(ring, 0.85f * Mathf.Clamp01(1.2f - off));
                    col = Over(col, rule, ring);
                    // 细斜角
                    float p = (dx + dy) * 0.7071f, q = (dx - dy) * 0.7071f;
                    float sd = Mathf.Min(Mathf.Abs(p) / 22f + Mathf.Abs(q) / 4.5f, Mathf.Abs(q) / 22f + Mathf.Abs(p) / 4.5f);
                    float ad = Mathf.Clamp01((1f - sd) * 6f);
                    if (ad > 0f) col = Over(col, p * q > 0f ? Color.Lerp(rule, cream, 0.35f) : rule, ad * 0.9f);
                    // 主四角 + 深褐细描边
                    float sNS = Mathf.Abs(dy) / 33f + Mathf.Abs(dx) / 7.5f, sEW = Mathf.Abs(dx) / 33f + Mathf.Abs(dy) / 7.5f;
                    col = Over(col, ink, 0.9f * Mathf.Clamp01((1.12f - Mathf.Min(sNS, sEW)) * 10f));
                    float aEW = Mathf.Clamp01((1f - sEW) * 7f), aNS = Mathf.Clamp01((1f - sNS) * 7f);
                    if (aEW > 0f) col = Over(col, dy > 0f ? ink : cream, aEW);
                    if (aNS > 0f) col = Over(col, dy > 0f ? (dx < 0f ? cin : cinHi) : (dx < 0f ? ink : cream), aNS);
                    col = Over(col, ink, Mathf.Clamp01(3.2f - r));
                    col = Over(col, cream, Mathf.Clamp01(1.6f - r));
                    px[y * S + x] = col;
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            cachedCompass = tex;
            return cachedCompass;
        }
    }

    static Color Over(Color dst, Color src, float a)
    {
        a = Mathf.Clamp01(a);
        float outA = a + dst.a * (1f - a);
        if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);
        float w = dst.a * (1f - a);
        return new Color((src.r * a + dst.r * w) / outA, (src.g * a + dst.g * w) / outA, (src.b * a + dst.b * w) / outA, outA);
    }

    // 暂停菜单里的设置项按钮统一外观（EggboyUI 的开关和 MenuController 的"引路光点"共用）
    public static void StyleSettingButton(Button btn)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img != null) { img.sprite = RoundedBorderSprite; img.type = Image.Type.Sliced; img.color = new Color32(0xf3, 0xe1, 0xc0, 255); }
        btn.transition = Selectable.Transition.ColorTint;
        btn.colors = new ColorBlock
        {
            normalColor = Color.white, highlightedColor = new Color32(255, 236, 205, 255), pressedColor = new Color32(232, 196, 150, 255),
            selectedColor = Color.white, disabledColor = new Color32(200, 200, 200, 180), colorMultiplier = 1f, fadeDuration = 0.08f
        };
        foreach (var sh in btn.GetComponents<Shadow>()) UnityEngine.Object.Destroy(sh);
        var t = btn.GetComponentInChildren<Text>();
        if (t != null) { t.color = InkDark; t.fontSize = 14; t.fontStyle = FontStyle.Normal; t.alignment = TextAnchor.MiddleLeft; var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(16f, 0f); trt.offsetMax = new Vector2(-10f, 0f); }
    }

    public static Image CreatePanel(Transform parent, string name, float w, float h, Color32 color, bool rounded = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = rounded ? RoundedSprite : WhiteSprite;
        img.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        return img;
    }

    public static RectTransform CreateParchmentCard(Transform parent, string name, float w, float h, int ruledLines = 7)
    {
        // 2026-11-16 统一成手账色板：羊皮纸 + 内缩双细线（RoundedBorderSprite）+ 胡桃木细外沿 + 柔和投影，右上角一枚淡朱砂邮戳
        var card = CreatePanel(parent, name, w, h, PaperColor, true);
        card.sprite = RoundedBorderSprite;
        var rim = card.gameObject.AddComponent<Outline>();
        rim.effectColor = new Color32(0x4a, 0x30, 0x1c, 215);
        rim.effectDistance = new Vector2(2f, -2f);
        var sh = card.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color32(0x1a, 0x10, 0x08, 80);
        sh.effectDistance = new Vector2(4f, -7f);
        if (h >= 140f && w >= 260f)
        {
            var mark = CreatePanel(card.transform, "CardPostmark", 66f, 66f, new Color32(0xc0, 0x3a, 0x2c, 70), false);
            mark.sprite = PostmarkRingSprite;
            mark.raycastTarget = false;
            var mrt = mark.rectTransform;
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1f, 1f);
            mrt.anchoredPosition = new Vector2(-14f, -12f);
            mrt.localRotation = Quaternion.Euler(0f, 0f, -12f);
        }

        for (int i = 0; i < ruledLines; i++)
        {
            var line = CreatePanel(card.transform, "Rule_" + i, w - 72f, 1f, new Color32(0xe9, 0xdc, 0xc0, 150), false);
            line.raycastTarget = false;
            var lrt = line.rectTransform;
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = new Vector2(0f, (h * 0.32f) - i * 34f);
        }
        return card.rectTransform;
    }

    public static Text CreateText(
        Transform parent,
        string content,
        int fontSize,
        Color32 color,
        TextAnchor align,
        Vector2 anchoredPos,
        Vector2 size,
        bool bold = false)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = UiFont;
        t.fontSize = fontSize;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.color = color;
        t.alignment = align;
        t.text = content;
        t.supportRichText = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.28f;
        t.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return t;
    }

    public static Button CreateWarmButton(
        Transform parent,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        bool primaryAmber,
        Action onClick)
    {
        // 2026-11-16 和暂停手账的设置按钮同一套：暖米底 + 内缩双细线；主按钮是暖橘底、胡桃木细外沿、奶白字
        Color32 normal = primaryAmber ? new Color32(0xe8, 0x86, 0x3c, 255) : new Color32(0xf3, 0xe1, 0xc0, 255);
        Color32 textCol = primaryAmber ? new Color32(0xff, 0xf8, 0xec, 255) : InkDark;

        var img = CreatePanel(parent, "Btn_" + label, size.x, size.y, normal, true);
        img.sprite = RoundedBorderSprite;
        if (primaryAmber)
        {
            var rim = img.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color32(0x6a, 0x30, 0x14, 150);
            rim.effectDistance = new Vector2(1.2f, -1.2f);
        }
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        var sh = img.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color32(0x3b, 0x2a, 0x1a, 55);
        sh.effectDistance = new Vector2(1.5f, -2.5f);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        btn.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color32(255, 240, 220, 255),
            pressedColor = new Color32(230, 195, 160, 255),
            selectedColor = Color.white,
            disabledColor = new Color32(200, 200, 200, 180),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
        if (onClick != null) btn.onClick.AddListener(() => onClick());

        CreateText(img.transform, label, 15, textCol, TextAnchor.MiddleCenter, Vector2.zero, size - new Vector2(12f, 4f), true);
        return btn;
    }

    // 从当前场景或 Resources/Lux3D、Resources/Mechanisms 克隆真实 Lux3D 模型
    public static GameObject CloneSceneLux3D(string primarySceneName, float targetSize, bool addWalkableGroundCollider = false)
    {
        GameObject src = FindSceneObject(primarySceneName);
        if (src == null) return null;

        var holder = new GameObject("Lux3D_" + primarySceneName);
        var clone = UnityEngine.Object.Instantiate(src, holder.transform, false);
        clone.name = "Model";
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.SetActive(true);

        // 移除原交互/发光/物理脚本，只保留纯净高精度的 Lux3D 网格与材质
        foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
            UnityEngine.Object.Destroy(mb);
        foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.Destroy(col);

        var renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            foreach (var r in renderers) r.enabled = true;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float scale = (maxDim > 0.001f && targetSize > 0.001f) ? (targetSize / maxDim) : 1f;
            clone.transform.localScale *= scale;

            // 重新计算缩放后的包围盒，使底部贴合 holder 原点 (y = 0)，水平居中
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            Vector3 offset = new Vector3(
                holder.transform.position.x - b.center.x,
                holder.transform.position.y - b.min.y,
                holder.transform.position.z - b.center.z);
            clone.transform.position += offset;

            if (addWalkableGroundCollider)
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer < 0) groundLayer = 6;
                holder.layer = groundLayer;
                var box = holder.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                box.size = new Vector3(Mathf.Max(0.45f, b.size.x * 0.92f), Mathf.Max(0.12f, b.size.y * 0.92f), Mathf.Max(0.45f, b.size.z * 0.92f));
            }
        }

        return holder;
    }

    public static GameObject FindSceneObject(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var mechRes = Resources.Load<GameObject>("Mechanisms/" + name);
        if (mechRes != null) return mechRes;

        var luxRes = Resources.Load<GameObject>("Lux3D/" + name);
        if (luxRes != null) return luxRes;

        var direct = GameObject.Find(name);
        if (direct != null) return direct;

        // 模糊匹配场景中的 Street_S6, crate_0 等真实 Lux3D 节点
        foreach (var r in UnityEngine.Object.FindObjectsOfType<Transform>())
        {
            if (r.name == name || r.name.StartsWith(name))
                return r.gameObject;
        }
        return Resources.Load<GameObject>("Items/ending_oilcan");
    }

    // 加载或构建可动多部件 3D 机关模型（优先使用 Resources/Mechanisms/ 下的 GLB 预制体，并确保 Frame_Base / Latch_0..2 / Ring_0..2 / Gear_0..2 / Core_Spindle 节点齐全）
    public static GameObject BuildArticulatedMechanism3D(string mechanismName, float overallScale = 1f)
    {
        var root = new GameObject("Mechanism3D_" + mechanismName);
        var glbAsset = Resources.Load<GameObject>("Mechanisms/" + mechanismName);
        if (glbAsset != null)
        {
            var inst = UnityEngine.Object.Instantiate(glbAsset, root.transform, false);
            inst.name = "GLBRoot";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * overallScale;
            if (FindDeepChild(root.transform, "Ring_0") != null)
                return root;
        }

        // 若 GLB 尚未完成首次导入，使用同款参数化多齿啮合齿轮网格 + 真实 Lux3D 手绘木纹/黄铜/青铜 PBR 材质实时组装同构节点
        Material matWood = ExtractLuxMaterial("C4_狗窝木箱_后续修改版", "S_doghouse", new Color32(158, 108, 66, 255), 0.12f, 0.35f);
        Material matBrass = ExtractLuxMaterial("D3_望远镜_v2", "LH_scope", new Color32(214, 168, 86, 255), 0.72f, 0.65f);
        Material matBronze = ExtractLuxMaterial("A6_台秤_v2", "Bakery_scale", new Color32(126, 92, 62, 255), 0.58f, 0.48f);

        bool isWinch = mechanismName != null && mechanismName.Contains("M3");
        bool isBrass = mechanismName != null && mechanismName.Contains("M4");

        var frameGo = CreateMeshNode(root.transform, "Frame_Base",
            BuildGearWheelMesh(0.46f, 0.38f, 0.14f, 6, 0.015f),
            isBrass ? matBronze : matWood,
            Vector3.zero);

        if (isWinch)
        {
            CreateMeshNode(root.transform, "Ring_0",
                BuildGearWheelMesh(0.38f, 0.12f, 0.08f, 16, 0.042f),
                matBrass,
                new Vector3(0f, 0.12f, 0f));
            CreateMeshNode(root.transform, "Core_Spindle",
                BuildGearWheelMesh(0.11f, 0.02f, 0.24f, 8, 0.022f),
                matWood,
                new Vector3(0f, 0.18f, 0f));
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                float lx = (i - 1) * 0.22f;
                CreateMeshNode(root.transform, "Latch_" + i,
                    BuildBoltLatchMesh(0.065f, 0.22f, 0.055f),
                    i == 1 ? matBrass : matBronze,
                    new Vector3(lx, 0.28f, 0.085f));
            }

            CreateMeshNode(root.transform, "Ring_0",
                BuildGearWheelMesh(0.36f, 0.26f, 0.06f, 18, 0.035f),
                isBrass ? matBrass : matWood,
                new Vector3(0f, -0.02f, 0.09f));

            CreateMeshNode(root.transform, "Ring_1",
                BuildGearWheelMesh(0.24f, 0.15f, 0.065f, 12, 0.03f),
                matBrass,
                new Vector3(0f, -0.02f, 0.115f));

            CreateMeshNode(root.transform, "Ring_2",
                BuildGearWheelMesh(0.13f, 0.06f, 0.07f, 8, 0.024f),
                matBronze,
                new Vector3(0f, -0.02f, 0.14f));

            for (int g = 0; g < 3; g++)
            {
                float ang = (g * 120f + 30f) * Mathf.Deg2Rad;
                CreateMeshNode(root.transform, "Gear_" + g,
                    BuildGearWheelMesh(0.065f, 0.018f, 0.05f, 8, 0.016f),
                    matBrass,
                    new Vector3(Mathf.Cos(ang) * 0.25f, -0.02f + Mathf.Sin(ang) * 0.25f, 0.135f));
            }

            CreateMeshNode(root.transform, "Core_Spindle",
                BuildGearWheelMesh(0.058f, 0.01f, 0.16f, 6, 0.014f),
                matBrass,
                new Vector3(0f, -0.02f, 0.165f));
        }

        root.transform.localScale = Vector3.one * overallScale;
        return root;
    }

    public static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null) return null;
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName || t.name.StartsWith(childName))
                return t;
        }
        return null;
    }

    static GameObject CreateMeshNode(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    static Material ExtractLuxMaterial(string luxResourceName, string sceneFallbackName, Color32 fallbackColor, float metallic, float smoothness)
    {
        var src = Resources.Load<GameObject>("Lux3D/" + luxResourceName);
        if (src == null) src = FindSceneObject(sceneFallbackName);
        if (src != null)
        {
            var r = src.GetComponentInChildren<Renderer>(true);
            if (r != null && r.sharedMaterial != null)
                return r.sharedMaterial;
        }
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = fallbackColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fallbackColor);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    static Mesh BuildGearWheelMesh(float outerR, float innerR, float thickness, int teeth, float toothH)
    {
        var mesh = new Mesh { name = "ProcGearWheel" };
        int segs = Mathf.Max(12, teeth * 4);
        float hz = thickness * 0.5f;
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();

        for (int i = 0; i < segs; i++)
        {
            float a = (2f * Mathf.PI * i) / segs;
            bool isTooth = (i % 4 == 1) || (i % 4 == 2);
            float ro = outerR + (isTooth ? toothH : 0f);
            float co = Mathf.Cos(a);
            float si = Mathf.Sin(a);
            verts.Add(new Vector3(ro * co, ro * si, hz));
            verts.Add(new Vector3(innerR * co, innerR * si, hz));
            verts.Add(new Vector3(ro * co, ro * si, -hz));
            verts.Add(new Vector3(innerR * co, innerR * si, -hz));
            uvs.Add(new Vector2(ro * co + 0.5f, ro * si + 0.5f));
            uvs.Add(new Vector2(innerR * co + 0.5f, innerR * si + 0.5f));
            uvs.Add(new Vector2(ro * co + 0.5f, ro * si + 0.5f));
            uvs.Add(new Vector2(innerR * co + 0.5f, innerR * si + 0.5f));
        }

        for (int i = 0; i < segs; i++)
        {
            int ni = (i + 1) % segs;
            int b0 = i * 4, b1 = ni * 4;
            // Front
            tris.Add(b0 + 1); tris.Add(b0 + 0); tris.Add(b1 + 0);
            tris.Add(b0 + 1); tris.Add(b1 + 0); tris.Add(b1 + 1);
            // Back
            tris.Add(b0 + 2); tris.Add(b0 + 3); tris.Add(b1 + 3);
            tris.Add(b0 + 2); tris.Add(b1 + 3); tris.Add(b1 + 2);
            // Outer rim
            tris.Add(b0 + 0); tris.Add(b0 + 2); tris.Add(b1 + 2);
            tris.Add(b0 + 0); tris.Add(b1 + 2); tris.Add(b1 + 0);
            // Inner rim
            tris.Add(b0 + 3); tris.Add(b0 + 1); tris.Add(b1 + 1);
            tris.Add(b0 + 3); tris.Add(b1 + 1); tris.Add(b1 + 3);
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildBoltLatchMesh(float w, float h, float d)
    {
        return BuildGearWheelMesh(Mathf.Max(w, d) * 0.65f, 0.01f, h, 4, 0.012f);
    }

    static Sprite MakeRounded(int S, int R)
    {
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
