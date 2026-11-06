// 面屏表情的像素画板：网页版用 canvas 2D，这里用 Texture2D 画同样的形状。
// 坐标沿用网页版的 512×400、y 向下；上传时翻转成 Unity 纹理的行序。
using System.Collections.Generic;
using UnityEngine;

public enum FaceSymbol
{
    None, Excl, Question, QuestionGrey, Anger, Heart, Sparkle, Zz, Puff, Sweat, Dots, Shake,
}

public class PixelCanvas
{
    public readonly int W, H;
    public readonly Color32[] Px;
    readonly Color32[] upload;

    public PixelCanvas(int w, int h)
    {
        W = w; H = h;
        Px = new Color32[w * h];
        upload = new Color32[w * h];
    }

    public void Clear(Color32 c)
    {
        for (int i = 0; i < Px.Length; i++) Px[i] = c;
    }

    void Blend(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || c.a == 0) return;
        int i = y * W + x;
        if (c.a == 255) { Px[i] = c; return; }
        Color32 d = Px[i];
        float t = c.a / 255f;
        d.r = (byte)(c.r * t + d.r * (1f - t));
        d.g = (byte)(c.g * t + d.g * (1f - t));
        d.b = (byte)(c.b * t + d.b * (1f - t));
        float a = c.a / 255f + d.a / 255f * (1f - t);
        d.a = (byte)(Mathf.Clamp01(a) * 255f);
        Px[i] = d;
    }

    static float DistSegment(float px, float py, float x0, float y0, float x1, float y1)
    {
        float dx = x1 - x0, dy = y1 - y0;
        float len2 = dx * dx + dy * dy;
        float t = len2 > 1e-9f ? Mathf.Clamp01(((px - x0) * dx + (py - y0) * dy) / len2) : 0f;
        float qx = x0 + dx * t, qy = y0 + dy * t;
        return Mathf.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
    }

    public void Line(float x0, float y0, float x1, float y1, float width, Color32 c, float glow = 0f)
    {
        if (glow > 0f)
        {
            Line(x0, y0, x1, y1, width + glow * 2f, new Color32(c.r, c.g, c.b, (byte)(c.a * 0.18f)), 0f);
            Line(x0, y0, x1, y1, width + glow, new Color32(c.r, c.g, c.b, (byte)(c.a * 0.3f)), 0f);
        }
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - width * 0.5f - 2f));
        int maxX = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + width * 0.5f + 2f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - width * 0.5f - 2f));
        int maxY = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + width * 0.5f + 2f));
        float rad = width * 0.5f;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = DistSegment(x + 0.5f, y + 0.5f, x0, y0, x1, y1);
                float aa = Mathf.Clamp01(rad + 0.9f - d);
                if (aa <= 0f) continue;
                Blend(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * aa)));
            }
        }
    }

    public void Arc(float cx, float cy, float r, float a0, float a1, float width, Color32 c, float glow = 0f)
    {
        // Composite each pixel once; overlapping segment glows used to fill hollow eyes.
        float span = a1 - a0, tau = Mathf.PI * 2f, rad = width * .5f;
        float bound = r + rad + glow + 2f;
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx-bound)), maxX = Mathf.Min(W-1,Mathf.CeilToInt(cx+bound));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy-bound)), maxY = Mathf.Min(H-1,Mathf.CeilToInt(cy+bound));
        var start = new Vector2(cx + Mathf.Cos(a0)*r, cy + Mathf.Sin(a0)*r);
        var end = new Vector2(cx + Mathf.Cos(a1)*r, cy + Mathf.Sin(a1)*r);
        for (int y=minY;y<=maxY;y++) for(int x=minX;x<=maxX;x++)
        {
            var p = new Vector2(x+.5f,y+.5f); var v=p-new Vector2(cx,cy);
            float angle=Mathf.Repeat(Mathf.Atan2(v.y,v.x)-a0,tau);
            float d=span>=tau-.001f || angle<=span ? Mathf.Abs(v.magnitude-r) : Mathf.Min((p-start).magnitude,(p-end).magnitude);
            float aa=Mathf.Clamp01(rad+.9f-d);
            if(glow>0f && d>rad) { float g=(d-rad)/Mathf.Max(1,glow*.5f);float alpha=.28f*Mathf.Exp(-g*g);if(alpha>.003f)Blend(x,y,new Color32(c.r,c.g,c.b,(byte)(c.a*alpha))); }
            if(aa>0f)Blend(x,y,new Color32(c.r,c.g,c.b,(byte)(c.a*aa)));
        }
    }

    public void Quadratic(float x0, float y0, float cx, float cy, float x1, float y1, float width, Color32 c, float glow = 0f)
    {
        int steps = 20;
        float px = x0, py = y0;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1f - t;
            float x = u * u * x0 + 2f * u * t * cx + t * t * x1;
            float y = u * u * y0 + 2f * u * t * cy + t * t * y1;
            Line(px, py, x, y, width, c, glow);
            px = x; py = y;
        }
    }

    public void Cubic(float x0, float y0, float cx1, float cy1, float cx2, float cy2, float x1, float y1, float width, Color32 c, float glow = 0f)
    {
        int steps = 24;
        float px = x0, py = y0;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1f - t;
            float x = u * u * u * x0 + 3f * u * u * t * cx1 + 3f * u * t * t * cx2 + t * t * t * x1;
            float y = u * u * u * y0 + 3f * u * u * t * cy1 + 3f * u * t * t * cy2 + t * t * t * y1;
            Line(px, py, x, y, width, c, glow);
            px = x; py = y;
        }
    }

    public void Ellipse(float cx, float cy, float rx, float ry, Color32 c, float glow = 0f)
    {
        if (glow > 0f)
            Ellipse(cx, cy, rx + glow, ry + glow, new Color32(c.r, c.g, c.b, (byte)(c.a * 0.28f)));
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 2f)), maxX = Mathf.Min(W - 1, Mathf.CeilToInt(cx + rx + 2f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 2f)), maxY = Mathf.Min(H - 1, Mathf.CeilToInt(cy + ry + 2f));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                float aa = Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) + 0.8f);
                Blend(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * Mathf.Min(1f, aa))));
            }
        }
    }

    public void Circle(float cx, float cy, float r, Color32 c, float glow = 0f) => Ellipse(cx, cy, r, r, c, glow);

    public void Polygon(Vector2[] pts, Color32 c, float glow = 0f, float outline = 0f, Color32? lineColor = null)
    {
        if (pts == null || pts.Length < 3) return;
        if (glow > 0f || outline > 0f)
        {
            Color32 lc = lineColor ?? c;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                if (glow > 0f) Line(a.x, a.y, b.x, b.y, outline + glow, new Color32(lc.r, lc.g, lc.b, (byte)(lc.a * 0.25f)));
                if (outline > 0f) Line(a.x, a.y, b.x, b.y, outline, lc);
            }
        }
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
        int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
        int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(maxY));
        var xs = new List<float>();
        for (int y = y0; y <= y1; y++)
        {
            xs.Clear();
            float sy = y + 0.5f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                    xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
            }
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
            {
                int xa = Mathf.Max(0, Mathf.FloorToInt(xs[k] + 0.5f));
                int xb = Mathf.Min(W - 1, Mathf.CeilToInt(xs[k + 1] - 0.5f));
                for (int x = xa; x <= xb; x++) Blend(x, y, c);
            }
        }
    }

    public void RoundedRect(float x0, float y0, float x1, float y1, float radius, Color32 top, Color32 bottom)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(x0)), maxX = Mathf.Min(W - 1, Mathf.CeilToInt(x1));
        int minY = Mathf.Max(0, Mathf.FloorToInt(y0)), maxY = Mathf.Min(H - 1, Mathf.CeilToInt(y1));
        float h = Mathf.Max(1f, y1 - y0);
        for (int y = minY; y <= maxY; y++)
        {
            float t = (y - y0) / h;
            Color32 c = new Color32(
                (byte)Mathf.Lerp(top.r, bottom.r, t),
                (byte)Mathf.Lerp(top.g, bottom.g, t),
                (byte)Mathf.Lerp(top.b, bottom.b, t), 255);
            for (int x = minX; x <= maxX; x++)
            {
                float dx = Mathf.Max(x0 + radius - x - 0.5f, 0f, x + 0.5f - (x1 - radius));
                float dy = Mathf.Max(y0 + radius - y - 0.5f, 0f, y + 0.5f - (y1 - radius));
                if (dx * dx + dy * dy > radius * radius) continue;
                Blend(x, y, c);
            }
        }
    }

    public void Polyline(Vector2[] pts, float width, Color32 c, float glow = 0f)
    {
        for (int i = 0; i + 1 < pts.Length; i++) Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, width, c, glow);
    }

    public void Upload(Texture2D tex)
    {
        for (int y = 0; y < H; y++)
        {
            int src = y * W;
            int dst = (H - 1 - y) * W;
            for (int x = 0; x < W; x++) upload[dst + x] = Px[src + x];
        }
        tex.SetPixels32(upload);
        tex.Apply(false, false);
    }
}

public class FacePainter
{
    public const int W = 512, H = 400;
    static readonly Color32 GOLD = new Color32(0xff, 0xc7, 0x5a, 255);
    static readonly Color32 BLUSH = new Color32(0xff, 0x5a, 0x3c, 255);
    static readonly Color32 TEAR = new Color32(0x6c, 0xc8, 0xff, 255);
    static readonly Color32 FEAR = new Color32(0xd6, 0xec, 0xff, 255);
    static readonly Color32 DARK = new Color32(0x3b, 0x2a, 0x1a, 255);

    public readonly PixelCanvas Canvas = new PixelCanvas(W, H);
    public readonly PixelCanvas SymbolCanvas = new PixelCanvas(256, 256);
    public readonly Texture2D Texture;
    public readonly Texture2D SymbolTexture;
    public ExprId Current { get; private set; } = ExprId.happy;
    public FaceSymbol Symbol { get; private set; } = FaceSymbol.None;
    public float Time => t;

    float t, since, blinkTimer = 2.5f, blink;
    float redraw;
    bool wasBlinking;
    readonly Dictionary<ExprId, FaceSymbol> symbols = new Dictionary<ExprId, FaceSymbol>
    {
        { ExprId.surprised, FaceSymbol.Excl }, { ExprId.curious, FaceSymbol.Question },
        { ExprId.angry, FaceSymbol.Anger }, { ExprId.cute, FaceSymbol.Heart },
        { ExprId.smug, FaceSymbol.Sparkle }, { ExprId.sleepy, FaceSymbol.Zz },
        { ExprId.meh, FaceSymbol.Puff }, { ExprId.nervous, FaceSymbol.Sweat },
        { ExprId.thinking, FaceSymbol.Dots }, { ExprId.puzzled, FaceSymbol.QuestionGrey },
        { ExprId.excited, FaceSymbol.Sparkle }, { ExprId.scared, FaceSymbol.Shake },
    };

    public FacePainter()
    {
        Texture = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
        Texture.wrapMode = TextureWrapMode.Clamp;
        Texture.filterMode = FilterMode.Bilinear;
        SymbolTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false, false);
        SymbolTexture.wrapMode = TextureWrapMode.Clamp;
        SymbolTexture.filterMode = FilterMode.Bilinear;
        DrawAll();
        DrawSymbolAll();
    }

    public void Set(ExprId id)
    {
        if (id == Current) return;
        Current = id;
        since = 0f;
        blink = 0f;
        Symbol = symbols.TryGetValue(id, out var s) ? s : FaceSymbol.None;
        DrawAll();
        DrawSymbolAll();
    }

    // 只有这几张表情会随时间连续动；其余是静态图，不需要反复上传纹理。
    static bool FaceAnimated(ExprId id)
    {
        return id == ExprId.surprised || id == ExprId.sleepy || id == ExprId.sad
            || id == ExprId.nervous || id == ExprId.excited || id == ExprId.scared;
    }

    public void Update(float dt)
    {
        t += dt;
        since += dt;
        blinkTimer -= dt;
        if (blinkTimer <= 0f) { blink = 0.13f; blinkTimer = 2.5f + Random.value * 3f; }
        blink = Mathf.Max(0f, blink - dt);
        redraw += dt;

        bool blinking = blink > 0f;
        bool faceDirty = blinking || wasBlinking || FaceAnimated(Current);
        bool symbolDirty = Symbol == FaceSymbol.Sparkle;
        if (redraw >= 1f / 30f)
        {
            redraw = 0f;
            if (faceDirty) DrawAll();
            if (symbolDirty) DrawSymbolAll();
            wasBlinking = blinking;
        }
    }

    public Vector2 SymbolPopScale
    {
        get
        {
            float p = Mathf.Min(1f, since / 0.3f) - 1f;
            float back = 1f + 2.70158f * p * p * p + 1.70158f * p * p;
            return Vector2.one * Mathf.Max(0.01f, 0.34f * back);
        }
    }

    void DrawAll()
    {
        var c = Canvas;
        c.Clear(new Color32(0, 0, 0, 0));
        c.RoundedRect(2f, 2f, W - 2f, H - 2f, 130f, new Color32(0x16, 0x13, 0x1a, 255), new Color32(0x0a, 0x09, 0x0c, 255));
        float L = 0.3f * W, R = 0.7f * W, Y = 0.5f * H, s = 0.14f * W;
        bool closed = blink > 0f;
        float k = since;
        switch (Current)
        {
            case ExprId.happy:
                if (closed) Lines(L, R, Y + s * 0.15f, s, GOLD);
                else { ArcEye(L, Y, s, GOLD); ArcEye(R, Y, s, GOLD); }
                Blush(1f);
                break;
            case ExprId.wink:
                Ring(L, Y, s * 0.48f, GOLD, 26f);
                Chevron(R, Y, s * 0.55f, -1, GOLD);
                Blush(1f);
                break;
            case ExprId.surprised:
                float rr = s * (0.5f + 0.04f * Mathf.Sin(t * 9f));
                Ring(L, Y, rr, GOLD, 26f); Ring(R, Y, rr, GOLD, 26f);
                break;
            case ExprId.curious:
                if (closed) Lines(L, R, Y, s, GOLD);
                else { ArcEye(L, Y - s * 0.12f, s * 1.05f, GOLD); ArcEye(R, Y + s * 0.05f, s * 0.9f, GOLD); }
                Blush(0.6f);
                break;
            case ExprId.angry:
                foreach (var (x, d) in new[] { (L, 1f), (R, -1f) })
                {
                    var pts = new[]
                    {
                        new Vector2(x - d * s * 0.6f, Y - s * 0.38f),
                        new Vector2(x + d * s * 0.62f, Y + s * 0.02f),
                        new Vector2(x + d * s * 0.5f, Y + s * 0.32f),
                        new Vector2(x - d * s * 0.52f, Y + s * 0.2f),
                    };
                    c.Polygon(pts, new Color32(0xff, 0xad, 0x3a, 255), 14f);
                }
                break;
            case ExprId.cute:
                foreach (var x in new[] { L, R })
                {
                    Ring(x, Y, s * 0.55f, GOLD, 20f);
                    c.Circle(x, Y + s * 0.04f, s * 0.33f, GOLD, 10f);
                    c.Circle(x - s * 0.12f, Y - s * 0.1f, s * 0.11f, new Color32(0xff, 0xf7, 0xe0, 255));
                }
                Blush(1.3f);
                break;
            case ExprId.smug:
                foreach (var (x, d) in new[] { (L, -1f), (R, 1f) })
                    c.Quadratic(x - s * 0.55f, Y + d * s * 0.04f, x, Y - s * 0.22f, x + s * 0.55f, Y - d * s * 0.04f, 24f, GOLD, 12f);
                Blush(0.8f);
                break;
            case ExprId.sleepy:
                float droop = 0.12f + 0.06f * Mathf.Sin(t * 1.4f);
                foreach (var x in new[] { L, R })
                    c.Quadratic(x - s * 0.55f, Y + s * droop, x, Y + s * (droop + 0.18f), x + s * 0.55f, Y + s * droop, 22f, GOLD, 12f);
                break;
            case ExprId.meh:
                Lines(L, R, Y + s * 0.05f, s * 1.05f, GOLD);
                break;
            case ExprId.sad:
                ArcEye(L, Y, s, GOLD); ArcEye(R, Y, s, GOLD);
                foreach (var (x, ph) in new[] { (L, 0f), (R, 0.45f) })
                {
                    float f = ((k * 0.9f + ph) % 1f);
                    Drop(x + s * 0.15f, Y + s * 0.55f + f * s * 1.6f, s * 0.18f, 1f - f * 0.6f, TEAR);
                }
                break;
            case ExprId.nervous:
                foreach (var x in new[] { L, R })
                    for (int i = 0; i < 3; i++)
                    {
                        float r = s * (0.18f + i * 0.17f) * (1f + 0.06f * Mathf.Sin(t * 8f + i));
                        Ring(x, Y, r, GOLD, 13f);
                    }
                break;
            case ExprId.shy:
                Chevron(L, Y, s * 0.5f, 1, GOLD); Chevron(R, Y, s * 0.5f, -1, GOLD);
                Blush(1.8f);
                break;
            case ExprId.thinking:
                float dx = s * 0.12f, dy = -s * 0.15f;
                if (closed) Lines(L + dx, R + dx, Y + dy, s, GOLD);
                else { ArcEye(L + dx, Y + dy, s * 0.9f, GOLD); ArcEye(R + dx, Y + dy, s * 0.9f, GOLD); }
                break;
            case ExprId.puzzled:
                ArcEye(L, Y - s * 0.1f, s, GOLD);
                Lines(R, R, Y + s * 0.12f, s * 0.7f, GOLD);
                break;
            case ExprId.excited:
                foreach (var (x, ph) in new[] { (L, 0f), (R, 1.3f) })
                    Star(x, Y, s * (0.62f + 0.08f * Mathf.Sin(t * 7f + ph)), t * 0.6f + ph);
                Blush(1f);
                break;
            case ExprId.scared:
                float jx = Mathf.Sin(t * 47f) * 3f, jy = Mathf.Cos(t * 53f) * 2f;
                foreach (var x in new[] { L, R })
                    c.Ellipse(x + jx, Y + jy - s * 0.05f, s * 0.32f, s * 0.4f, FEAR, 20f);
                c.Polyline(new[]
                {
                    new Vector2(W / 2f - s * 0.6f + jx, Y + s * 0.85f + jy),
                    new Vector2(W / 2f - s * 0.4f + jx, Y + s * 0.73f + jy),
                    new Vector2(W / 2f - s * 0.2f + jx, Y + s * 0.85f + jy),
                    new Vector2(W / 2f + jx, Y + s * 0.73f + jy),
                    new Vector2(W / 2f + s * 0.2f + jx, Y + s * 0.85f + jy),
                    new Vector2(W / 2f + s * 0.4f + jx, Y + s * 0.73f + jy),
                    new Vector2(W / 2f + s * 0.6f + jx, Y + s * 0.85f + jy),
                }, 10f, FEAR);
                break;
        }
        Canvas.Upload(Texture);
    }

    void DrawSymbolAll()
    {
        var c = SymbolCanvas;
        c.Clear(new Color32(0, 0, 0, 0));
        if (Symbol == FaceSymbol.None) { c.Upload(SymbolTexture); return; }
        switch (Symbol)
        {
            case FaceSymbol.Excl:
                foreach (var x in new[] { 95f, 160f })
                {
                    Outline(c, new[]
                    {
                        new Vector2(x - 14f, 40f), new Vector2(x + 14f, 40f),
                        new Vector2(x + 7f, 150f), new Vector2(x - 7f, 150f),
                    }, new Color32(0xff, 0x5a, 0x3c, 255), 8f);
                    OutlineCircle(c, x, 190f, 16f, new Color32(0xff, 0x5a, 0x3c, 255), 8f);
                }
                break;
            case FaceSymbol.Question:
            case FaceSymbol.QuestionGrey:
                Question(c, 128f, 135f, Symbol == FaceSymbol.Question ? new Color32(0x5a, 0xa9, 0xff, 255) : new Color32(0xb8, 0xb1, 0xa8, 255));
                break;
            case FaceSymbol.Anger:
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                    float x0 = 128f + Mathf.Cos(a) * 16f, y0 = 128f + Mathf.Sin(a) * 16f;
                    float x1 = 128f + Mathf.Cos(a) * 58f - Mathf.Sin(a) * 22f;
                    float y1 = 128f + Mathf.Sin(a) * 58f + Mathf.Cos(a) * 22f;
                    c.Quadratic(x0, y0, 128f + Mathf.Cos(a) * 34f, 128f + Mathf.Sin(a) * 34f, x1, y1, 14f, new Color32(0xff, 0x8a, 0x2a, 255), 4f);
                }
                break;
            case FaceSymbol.Heart:
                Heart(c);
                break;
            case FaceSymbol.Sparkle:
                foreach (var (x, y, r, ph) in new[] { (90f, 120f, 46f, 0f), (175f, 70f, 30f, 1.7f), (180f, 175f, 24f, 3.1f) })
                    Sparkle(c, x, y, r * (0.75f + 0.25f * Mathf.Sin(t * 5f + ph)));
                break;
            case FaceSymbol.Zz:
                Z(c, 80f, 180f, 1f);
                Z(c, 160f, 110f, 0.7f);
                break;
            case FaceSymbol.Puff:
                foreach (var (x, y, r) in new[] { (95f, 140f, 40f), (140f, 120f, 48f), (180f, 145f, 36f), (130f, 165f, 40f) })
                    OutlineCircle(c, x, y, r, new Color32(255, 255, 255, 255), 8f);
                break;
            case FaceSymbol.Sweat:
                Teardrop(c, 90f, 110f, 30f, new Color32(0x7f, 0xd0, 0xff, 255));
                Teardrop(c, 170f, 160f, 24f, new Color32(0x7f, 0xd0, 0xff, 255));
                break;
            case FaceSymbol.Dots:
                for (int i = 0; i < 3; i++)
                {
                    bool on = Mathf.FloorToInt(t * 3f) % 4 > i;
                    OutlineCircle(c, 70f + i * 58f, 150f, 18f, on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 80), 6f);
                }
                break;
            case FaceSymbol.Shake:
                for (int i = 0; i < 2; i++)
                {
                    float r = i == 0 ? 60f : 95f;
                    c.Arc(128f, 150f, r, -Mathf.PI * 0.85f, -Mathf.PI * 0.6f, 12f, new Color32(0x8a, 0x7a, 0x6a, 255));
                    c.Arc(128f, 150f, r, -Mathf.PI * 0.4f, -Mathf.PI * 0.15f, 12f, new Color32(0x8a, 0x7a, 0x6a, 255));
                }
                break;
        }
        SymbolCanvas.Upload(SymbolTexture);
    }

    // —— 画笔小工具 ——
    static void Sparkle(PixelCanvas c, float x, float y, float r)
    {
        var pts = new[]
        {
            new Vector2(x, y - r), new Vector2(x + r * 0.15f, y - r * 0.15f), new Vector2(x + r, y),
            new Vector2(x + r * 0.15f, y + r * 0.15f), new Vector2(x, y + r),
            new Vector2(x - r * 0.15f, y + r * 0.15f), new Vector2(x - r, y),
            new Vector2(x - r * 0.15f, y - r * 0.15f),
        };
        c.Polygon(pts, new Color32(0xff, 0xd8, 0x4a, 255), 14f, 7f, DARK);
    }

    static void Heart(PixelCanvas c)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < 40; i++)
        {
            float a = i * Mathf.PI * 2f / 40f;
            float hx = 16f * Mathf.Pow(Mathf.Sin(a), 3f);
            float hy = 13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f * a) - 2f * Mathf.Cos(3f * a) - Mathf.Cos(4f * a);
            pts.Add(new Vector2(128f + hx * 5.2f, 130f - hy * 5.2f));
        }
        c.Polygon(pts.ToArray(), new Color32(0xff, 0x6b, 0x7a, 255), 12f, 7f, DARK);
    }

    static void Teardrop(PixelCanvas c, float x, float y, float r, Color32 color)
    {
        c.Polygon(new[]
        {
            new Vector2(x, y - r * 1.7f),
            new Vector2(x - r * 0.8f, y + r * 0.1f),
            new Vector2(x + r * 0.8f, y + r * 0.1f),
        }, color, 8f, 5f, DARK);
        c.Circle(x, y + r * 0.25f, r * 0.8f, color, 8f);
    }

    static void Outline(PixelCanvas c, Vector2[] pts, Color32 fill, float w)
    {
        c.Polygon(pts, fill, 8f, w, DARK);
    }

    static void OutlineCircle(PixelCanvas c, float x, float y, float r, Color32 fill, float w)
    {
        c.Circle(x, y, r + w * 0.5f, DARK);
        c.Circle(x, y, r, fill);
    }

    static void Question(PixelCanvas c, float x, float y, Color32 color)
    {
        c.Polyline(new[]
        {
            new Vector2(x - 34f, y - 38f), new Vector2(x - 20f, y - 58f), new Vector2(x + 8f, y - 64f),
            new Vector2(x + 34f, y - 48f), new Vector2(x + 38f, y - 18f), new Vector2(x + 20f, y + 2f),
            new Vector2(x + 4f, y + 18f), new Vector2(x, y + 38f),
        }, 18f, DARK);
        c.Polyline(new[]
        {
            new Vector2(x - 34f, y - 38f), new Vector2(x - 20f, y - 58f), new Vector2(x + 8f, y - 64f),
            new Vector2(x + 34f, y - 48f), new Vector2(x + 38f, y - 18f), new Vector2(x + 20f, y + 2f),
            new Vector2(x + 4f, y + 18f), new Vector2(x, y + 38f),
        }, 12f, color);
        OutlineCircle(c, x, y + 70f, 9f, color, 6f);
    }

    static void Z(PixelCanvas c, float x, float y, float scale)
    {
        var pts = new[]
        {
            new Vector2(x - 40f * scale, y - 34f * scale), new Vector2(x + 40f * scale, y - 34f * scale),
            new Vector2(x - 40f * scale, y + 34f * scale), new Vector2(x + 40f * scale, y + 34f * scale),
        };
        c.Polyline(pts, 20f * scale, DARK);
        c.Polyline(pts, 12f * scale, new Color32(0x7f, 0xc4, 0xff, 255));
    }

    // 这些实例方法需要访问 Canvas，改成实例方法
    void ArcEye(float x, float y, float s, Color32 color)
    {
        Canvas.Arc(x, y + s * 0.3f, s * 0.55f, Mathf.PI * 1.08f, Mathf.PI * 1.92f, 30f, color, 14f);
    }

    void Lines(float L, float R, float y, float s, Color32 color)
    {
        if (Mathf.Abs(L - R) < 0.01f)
        {
            Canvas.Line(L - s * 0.5f, y, L + s * 0.5f, y, 28f, color, 14f);
            return;
        }
        Canvas.Line(L - s * 0.5f, y, L + s * 0.5f, y, 28f, color, 14f);
        Canvas.Line(R - s * 0.5f, y, R + s * 0.5f, y, 28f, color, 14f);
    }

    void Ring(float x, float y, float r, Color32 color, float w)
    {
        Canvas.Arc(x, y, r, 0f, Mathf.PI * 2f, w, color, 12f);
    }

    void Chevron(float x, float y, float s, int dir, Color32 color)
    {
        Canvas.Polyline(new[]
        {
            new Vector2(x - dir * s * 0.45f, y - s * 0.55f),
            new Vector2(x + dir * s * 0.45f, y),
            new Vector2(x - dir * s * 0.45f, y + s * 0.55f),
        }, 28f, color, 14f);
    }

    void Blush(float k)
    {
        float n = k > 1.5f ? 4f : 3f;
        for (int side = 0; side < 2; side++)
        {
            float cx = side == 0 ? 0.2f * W : 0.8f * W;
            for (int i = 0; i < n; i++)
            {
                float x = cx - ((n - 1) / 2f) * 17f + i * 17f, y = 0.76f * H;
                float dy = 11f * Mathf.Min(k, 1.3f);
                Canvas.Line(x + 4f, y - dy, x - 4f, y + dy, 7f,
                    new Color32(BLUSH.r, BLUSH.g, BLUSH.b, (byte)(255 * Mathf.Min(1f, 0.55f + 0.25f * k))), 6f);
            }
        }
    }

    void Drop(float x, float y, float r, float alpha, Color32 color)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= 16; i++)
        {
            float a = Mathf.PI * i / 16f;
            pts.Add(new Vector2(x + Mathf.Cos(a) * r, y + r * 0.2f + Mathf.Sin(a) * r));
        }
        pts.Add(new Vector2(x, y - r * 1.8f));
        var cc = new Color32(color.r, color.g, color.b, (byte)(255 * alpha));
        Canvas.Polygon(pts.ToArray(), cc, 8f);
    }

    void Star(float x, float y, float r, float rot)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = rot * 0.2f - Mathf.PI / 2f + i * Mathf.PI / 5f;
            float rr = i % 2 == 1 ? r * 0.45f : r;
            pts[i] = new Vector2(x + Mathf.Cos(a) * rr, y + Mathf.Sin(a) * rr);
        }
        Canvas.Polygon(pts, new Color32(0xff, 0xe0, 0x66, 255), 14f);
    }
}
