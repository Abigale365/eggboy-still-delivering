// 创新方向 G：光影 + 声音（工程书 INNOVATION_BOOK_20261110 的 WP-G）。2026-11-11 重写成世界里的两个谜题。
//
// 一、影子钥匙（ShadowLanternPuzzle）
//   船棚附近立着一块布幕，后面一盏老灯头，中间一个能转的杂物架（救生圈、船桨、两只小木箱）。
//   按住 E 转架子：布幕上是杂物真实的影子——从灯的位置把杂物渲染成剪影投到布幕上（自己做的一次阴影映射，
//   不依赖 URP 附加光阴影的设置）。转到对的角度，救生圈是钥匙头、船桨是钥匙杆、两只木箱是齿，拼成一把钥匙；
//   布幕亮起来，架子上多出一只空玻璃瓶。判定用"转角离目标 ±7°"（确定、好测），审计还会去读布幕上的像素核对钥匙形状。
//
// 二、装声音的瓶子（SoundBottleSystem）
//   拿着空瓶去装四种声音：面包店的铜铃、学校的秋千、小黑的叫声、柴堆的劈柴声（都是程序合成的）。
//   广场喷泉旁的八音盒底座上刻着四个空槽；装齐了放进去，旋律响起，周围的房子随着节拍一起一伏，结束后精确复位。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShadowLanternPuzzle : MonoBehaviour
{
    public static ShadowLanternPuzzle Instance { get; private set; }
    const int ShadowLayer = 29;
    const float TargetTolerance = 7f, StartAngle = 125f, TurnSpeed = 40f;

    GameState state; EggboyPlayer player; EggboyUI ui; IslandWorld world;
    public Vector3 TurntablePos { get; private set; }
    public Vector3 ScreenCenter { get; private set; }
    public Vector3 BottlePos => TurntablePos + Vector3.up * 0.05f + side * 0.45f;
    Vector3 axis, side;
    Transform turntable;
    Camera shadowCam;
    RenderTexture rt;
    Material screenMat;
    public float Angle { get; private set; } = StartAngle;
    public bool Solved => state != null && state.flags.Contains("shadow_key_done");
    public bool BottleTaken => state != null && state.flags.Contains("has_sound_bottle");
    public bool DebugHold;
    GameObject bottle;
    float glow;

    static readonly Color Lit = new Color(1f, 0.93f, 0.78f, 1f);
    static readonly Color Shade = new Color(0.22f, 0.15f, 0.1f, 1f);

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
    {
        Instance = this;
        player = p; ui = u; world = w; state = s;
        if (!Place()) { enabled = false; return; }
        Build();
        if (Solved) { Angle = 0f; ApplyAngle(); SpawnBottle(); }
        var svc = FindObjectOfType<InteractionService>();
        if (svc != null) { svc.Register(new TurntablePrompt(this)); svc.Register(new BottlePickup(this)); }
    }

    // 船棚附近找一块够大、又不挡路的平地：灯、架子、布幕排成一条线，都要站得住
    bool Place()
    {
        var shed = GameObject.Find("B5_boathouse");
        Vector3 c = shed != null ? shed.transform.position : new Vector3(21.7f, 1.4f, -15.9f);
        var route = new List<Vector3>();
        var asset = Resources.Load<TextAsset>("World/route");
        if (asset != null)
        {
            var list = JsonUtility.FromJson<RouteList>(asset.text);
            if (list != null) foreach (var r in list.route) route.Add(new Vector3(r.x, 0f, r.z));
        }
        float best = float.MaxValue; bool ok = false;
        for (float r = 2.5f; r <= 12f; r += 0.5f)
            for (int a = 0; a < 24; a++)
            {
                float ang = a * Mathf.PI / 12f;
                Vector3 pt = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                Vector3 d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)); // 布幕在外侧，灯靠船棚
                Vector3 lamp = pt - d * 1.4f, scr = pt + d * 1.6f;
                bool fits = true; float gy = 0f;
                foreach (var q in new[] { pt, lamp, scr, pt + new Vector3(-d.z, 0, d.x) * 1.2f })
                {
                    if (!world.Walkable(q.x, q.z)) { fits = false; break; }
                    float g = world.GroundAt(q.x, q.z, c.y + 3f);
                    if (q == pt) gy = g;
                    if (Mathf.Abs(g - gy) > 0.25f || g < WaterLevel.LevelYs[2] + 0.12f) { fits = false; break; }
                }
                if (!fits) continue;
                // 离路线（停靠点之间的连线）至少 2 m
                float minRoute = float.MaxValue;
                for (int i = 0; i + 1 < route.Count; i++) minRoute = Mathf.Min(minRoute, DistToSeg(pt, route[i], route[i + 1]), DistToSeg(scr, route[i], route[i + 1]));
                if (minRoute < 1.6f) continue;
                if (DogPawPlate.Instance != null && Vector3.Distance(pt, DogPawPlate.Instance.Center) < 3f) continue;
                float score = r + Mathf.Max(0f, 3f - minRoute) * 2f;
                if (score < best) { best = score; ok = true; TurntablePos = new Vector3(pt.x, gy, pt.z); axis = d; }
            }
        if (!ok) { Debug.LogWarning("SHADOW 船棚附近找不到合适的位置"); return false; }
        side = new Vector3(-axis.z, 0f, axis.x);
        ScreenCenter = TurntablePos + axis * 1.6f + Vector3.up * 0.85f;
        return true;
    }

    static float DistToSeg(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a; ab.y = 0f; Vector3 ap = p - a; ap.y = 0f;
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
        Vector3 c = a + ab * t;
        return new Vector2(p.x - c.x, p.z - c.z).magnitude;
    }

    static Material ParticleBase()
    {
        var fx = FindObjectOfType<BakeryEffects>();
        if (fx != null && fx.ParticleMaterial != null) return new Material(fx.ParticleMaterial);
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        return new Material(sh);
    }

    static Texture2D white;
    static void SetColorTex(Material m, Color c, Texture tex)
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        foreach (var p in new[] { "_BaseMap", "_MainTex" }) if (m.HasProperty(p)) m.SetTexture(p, tex != null ? tex : white);
        foreach (var p in new[] { "_BaseColor", "_Color", "_TintColor" }) if (m.HasProperty(p)) m.SetColor(p, c);
    }

    void Build()
    {
        Vector3 groundY = new Vector3(0f, TurntablePos.y, 0f);
        Quaternion face = Quaternion.LookRotation(axis, Vector3.up);

        // 灯头
        Vector3 lampPos = TurntablePos - axis * 1.4f + Vector3.up * 0.85f;
        var lamp = TideTownStyleUI.CloneSceneLux3D("B9_灯塔灯头_v4", 0.45f, false);
        if (lamp != null)
        {
            lamp.transform.SetParent(transform, false);
            var lg = TurntablePos - axis * 1.4f; lg.y = TurntablePos.y;
            lamp.transform.position = lg + Vector3.up * 0.55f;
            lamp.transform.rotation = face;
        }
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(post.GetComponent<Collider>());
        post.transform.SetParent(transform, false);
        post.transform.position = new Vector3(lampPos.x, TurntablePos.y + 0.28f, lampPos.z);
        post.transform.localScale = new Vector3(0.08f, 0.28f, 0.08f);
        post.GetComponent<Renderer>().sharedMaterial = WoodMat();
        var lgo = new GameObject("ShadowLampLight");
        lgo.transform.SetParent(transform, false);
        lgo.transform.SetPositionAndRotation(lampPos, face);
        var spot = lgo.AddComponent<Light>();
        spot.type = LightType.Spot; spot.spotAngle = 55f; spot.range = 4.5f; spot.intensity = 3f;
        spot.color = new Color(1f, 0.85f, 0.6f); spot.shadows = LightShadows.None;

        // 杂物架：转盘 + 四件杂物（每件都有一份看得见的和一份只给影子相机看的黑剪影）
        var tt = new GameObject("ShadowTurntable");
        tt.transform.SetParent(transform, false);
        tt.transform.position = TurntablePos;
        turntable = tt.transform;
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(turntable, false);
        disc.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        disc.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
        disc.GetComponent<Renderer>().sharedMaterial = WoodMat();
        var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(stick.GetComponent<Collider>());
        stick.transform.SetParent(turntable, false);
        stick.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        stick.transform.localScale = new Vector3(0.04f, 0.36f, 0.04f);
        stick.GetComponent<Renderer>().sharedMaterial = WoodMat();

        // 目标角度（Angle = 0）时，从灯这边看过去的排布：u 向右（side），v 向上；z 是离灯的远近
        //   钥匙头（救生圈）在左，钥匙杆（船桨）向右，两只齿（小木箱）挂在杆的右端下面
        // 影子放大约 2 倍（灯到架子 1.4 m、架子到布幕 1.6 m），整把钥匙在布幕上约 1.1 m 宽
        AddJunk("D6_救生圈_v2", 0.21f, new Vector3(-0.15f, 0.85f, 0.06f), "ring");
        AddJunk("D4O_船桨_v2", 0.36f, new Vector3(0.11f, 0.85f, 0f), "shaft");
        AddJunk("S2_木箱_分享包版", 0.08f, new Vector3(0.17f, 0.77f, -0.06f), "box");
        AddJunk("S2_木箱_分享包版", 0.08f, new Vector3(0.25f, 0.77f, 0.04f), "box");

        // 布幕 + 木框
        var scr = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(scr.GetComponent<Collider>());
        scr.name = "ShadowScreen";
        scr.transform.SetParent(transform, false);
        scr.transform.SetPositionAndRotation(ScreenCenter, face);
        scr.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) { name = "ShadowScreenRT" };
        screenMat = ParticleBase();
        SetColorTex(screenMat, Color.white, rt);
        scr.GetComponent<Renderer>().sharedMaterial = screenMat;
        // 背面也要有点东西，别从后面看是透明的
        var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(back.GetComponent<Collider>());
        back.transform.SetParent(scr.transform, false);
        back.transform.localPosition = new Vector3(0f, 0f, 0.002f);
        back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var bm = ParticleBase(); SetColorTex(bm, new Color(0.86f, 0.8f, 0.68f), null);
        back.GetComponent<Renderer>().sharedMaterial = bm;
        foreach (var e in new[] { new Vector3(-0.78f, 0f, 0f), new Vector3(0.78f, 0f, 0f) })
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(leg.GetComponent<Collider>());
            leg.transform.SetParent(transform, false);
            leg.transform.SetPositionAndRotation(ScreenCenter + face * e + Vector3.down * 0.15f, face);
            leg.transform.localScale = new Vector3(0.06f, 1.9f, 0.06f);
            leg.GetComponent<Renderer>().sharedMaterial = WoodMat();
        }
        var topBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(topBar.GetComponent<Collider>());
        topBar.transform.SetParent(transform, false);
        topBar.transform.SetPositionAndRotation(ScreenCenter + Vector3.up * 0.78f, face);
        topBar.transform.localScale = new Vector3(1.62f, 0.06f, 0.06f);
        topBar.GetComponent<Renderer>().sharedMaterial = WoodMat();

        // 影子相机：在灯的位置，只看剪影层，画面正好铺满布幕
        var cgo = new GameObject("ShadowCasterCam");
        cgo.transform.SetParent(transform, false);
        cgo.transform.SetPositionAndRotation(lampPos, Quaternion.LookRotation(ScreenCenter - lampPos, Vector3.up));
        shadowCam = cgo.AddComponent<Camera>();
        shadowCam.cullingMask = 1 << ShadowLayer;
        shadowCam.clearFlags = CameraClearFlags.SolidColor;
        shadowCam.backgroundColor = Lit;
        float dist = Vector3.Distance(lampPos, ScreenCenter);
        shadowCam.fieldOfView = 2f * Mathf.Atan(0.75f / dist) * Mathf.Rad2Deg;
        shadowCam.aspect = 1f;
        shadowCam.nearClipPlane = 0.1f; shadowCam.farClipPlane = dist + 0.5f;
        shadowCam.targetTexture = rt;
        var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(shadowCam);
        if (data != null) { data.renderPostProcessing = false; data.renderShadows = false; }
        foreach (var cam in Camera.allCameras) if (cam != shadowCam) cam.cullingMask &= ~(1 << ShadowLayer);

        // 玩家撞不进布幕和灯
        world.AddCollider(new EggboyCollider { x = ScreenCenter.x, z = ScreenCenter.z, r = 0.45f });
        world.AddCollider(new EggboyCollider { x = lampPos.x, z = lampPos.z, r = 0.3f });
        world.AddCollider(new EggboyCollider { x = TurntablePos.x, z = TurntablePos.z, r = 0.45f });
        ApplyAngle();
    }

    static Material woodMat;
    static Material WoodMat()
    {
        if (woodMat != null) return woodMat;
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        woodMat = new Material(sh);
        var c = new Color(0.5f, 0.33f, 0.19f);
        woodMat.color = c;
        if (woodMat.HasProperty("_BaseColor")) woodMat.SetColor("_BaseColor", c);
        return woodMat;
    }

    Material shadeMat;
    void AddJunk(string model, float size, Vector3 uvz, string kind)
    {
        // 在转盘本地坐标里：x = 向右（side），y = 向上，z = 朝布幕（axis）。Angle=0 时转盘本地轴和世界的 side/axis 对齐
        var vis = TideTownStyleUI.CloneSceneLux3D(model, size, false);
        if (vis == null) return;
        vis.name = "Junk_" + kind;
        vis.transform.SetParent(turntable, false);
        OrientJunk(vis, kind);
        vis.transform.localPosition = new Vector3(uvz.x, uvz.y, uvz.z) - LocalCenter(vis);
        // 剪影：同一个模型复制一份，换成纯色，只放在影子层
        var sil = Instantiate(vis, turntable, false);
        sil.name = "Silhouette_" + kind;
        if (shadeMat == null) { shadeMat = ParticleBase(); SetColorTex(shadeMat, Shade, null); }
        foreach (var r in sil.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = shadeMat;
            r.sharedMaterials = mats;
            r.gameObject.layer = ShadowLayer;
        }
        OverlayRender.SetLayerRecursive(sil, ShadowLayer);
    }

    // 救生圈要立起来、圆面朝灯；船桨躺平、长边朝右、薄的那面朝灯；木箱随意
    void OrientJunk(GameObject go, string kind)
    {
        var t = go.transform;
        t.localRotation = Quaternion.identity;
        var b = LocalBounds(go);
        Vector3 s = b.size;
        if (kind == "ring")
        {
            // 最薄的轴转到 z
            if (s.y <= s.x && s.y <= s.z) t.localRotation = Quaternion.Euler(90f, 0f, 0f);
            else if (s.x <= s.y && s.x <= s.z) t.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        else if (kind == "shaft")
        {
            // 最长的轴转到 x
            if (s.y >= s.x && s.y >= s.z) t.localRotation = Quaternion.Euler(0f, 0f, 90f);
            else if (s.z >= s.x && s.z >= s.y) t.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var b2 = LocalBounds(go);
            // 桨叶宽的那面朝灯（让 z 最薄）
            if (b2.size.z > b2.size.y) t.localRotation = Quaternion.Euler(90f, 0f, 0f) * t.localRotation;
        }
    }

    Bounds LocalBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        var inv = turntable.worldToLocalMatrix;
        bool init = false; var b = new Bounds();
        foreach (var r in rs)
        {
            var wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = new Vector3(i % 2 == 0 ? wb.min.x : wb.max.x, (i / 2) % 2 == 0 ? wb.min.y : wb.max.y, i / 4 == 0 ? wb.min.z : wb.max.z);
                Vector3 lp = inv.MultiplyPoint3x4(p);
                if (!init) { b = new Bounds(lp, Vector3.zero); init = true; } else b.Encapsulate(lp);
            }
        }
        return b;
    }

    Vector3 LocalCenter(GameObject go) { var b = LocalBounds(go); return b.center - go.transform.localPosition; }

    void ApplyAngle()
    {
        if (turntable == null) return;
        turntable.rotation = Quaternion.LookRotation(axis, Vector3.up) * Quaternion.Euler(0f, Angle, 0f);
    }

    bool PlayerFree => ui != null && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen && player != null && !player.locked;
    public bool PlayerNear => player != null && Vector3.Distance(player.transform.position, TurntablePos) < 1.9f;
    public bool Turning { get; private set; }

    void Update()
    {
        if (turntable == null) return;
        float dt = Time.deltaTime;
        // 影子相机只在玩家走近时才渲染，省性能
        if (shadowCam != null && player != null) shadowCam.enabled = Vector3.Distance(player.transform.position, TurntablePos) < 30f;
        Turning = false;
        if (!Solved)
        {
            bool hold = DebugHold || (PlayerFree && PlayerNear && Input.GetKey(KeyCode.E));
            if (hold)
            {
                Turning = true;
                float a = Mathf.DeltaAngle(0f, Angle);
                // 离目标近了转得慢一点，好停准
                float speed = Mathf.Abs(a) < 20f ? TurnSpeed * 0.45f : TurnSpeed;
                Angle = Mathf.Repeat(Angle - speed * dt, 360f);
                if (Mathf.Abs(Mathf.DeltaAngle(0f, Angle)) < TargetTolerance) { Angle = 0f; Solve(); }
                ApplyAngle();
            }
        }
        glow = Mathf.MoveTowards(glow, Solved ? 1f : 0f, dt * 1.5f);
        if (screenMat != null) SetColorTex(screenMat, Color.Lerp(Color.white, new Color(1.25f, 1.15f, 0.9f), glow), rt);
    }

    public void DebugSetAngle(float a) { Angle = a; ApplyAngle(); }

    void Solve()
    {
        if (Solved) return;
        state.flags.Add("shadow_key_done");
        TideTownAchievementSystem.Unlock("shadow_key");
        ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
        ShadowAndChimeSystem.PlayProceduralChime(1174.66f, 0.6f);
        SpawnBottle();
    }

    void SpawnBottle()
    {
        if (BottleTaken || bottle != null) return;
        var lux = Lux3DService.Ensure();
        bottle = lux.InstantiateFallback(lux.Find("sound_bottle"), 0.26f);
        if (bottle == null) return;
        bottle.name = "SoundBottle";
        bottle.transform.SetParent(transform, false);
        var p = BottlePos; p.y = world.GroundAt(p.x, p.z, p.y + 1f);
        bottle.transform.position = p;
    }

    public void TakeBottle()
    {
        if (!Solved || BottleTaken) return;
        state.flags.Add("has_sound_bottle");
        if (bottle != null) Destroy(bottle);
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.2f);
        if (ui != null) ui.ShowMemory("一只空玻璃瓶", "瓶底刻着一行小字：\n\n镇上的声音，装起来，\n带去广场的八音盒。");
    }

    // 审计用：读布幕上某一点是亮（被灯照到）还是暗（影子）。u、v 从布幕左下 (0,0) 到右上 (1,1)，从灯那边看
    public bool DebugIsShadow(float u, float v)
    {
        if (shadowCam == null || rt == null) return false;
        shadowCam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(Mathf.Clamp(u * rt.width, 0, rt.width - 1), Mathf.Clamp(v * rt.height, 0, rt.height - 1), 1, 1), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        var c = tex.GetPixel(0, 0);
        Destroy(tex);
        return c.r < 0.6f;
    }

    // 剪影在布幕上的位置（审计按真实几何算出钥匙各部分该落在哪儿）
    public Vector2 DebugScreenUV(Vector3 world)
    {
        if (shadowCam == null) return Vector2.zero;
        Vector3 vp = shadowCam.WorldToViewportPoint(world);
        return new Vector2(vp.x, vp.y);
    }
    public Transform DebugJunk(string kind) => turntable != null ? turntable.Find("Junk_" + kind) : null;
}

public sealed class TurntablePrompt : IInteractable
{
    readonly ShadowLanternPuzzle p;
    public TurntablePrompt(ShadowLanternPuzzle puzzle) { p = puzzle; }
    public string EntityId => "shadow_turntable";
    public Vector3 Position => p.TurntablePos;
    public string Prompt(GameState s) => p.Solved ? "" : "按住 E　转杂物架";
    public bool CanInteract(GameState s) => false; // 按住 E 由谜题自己读
    public void Interact(GameState s, InteractionService svc) { }
}

public sealed class BottlePickup : IInteractable
{
    readonly ShadowLanternPuzzle p;
    public BottlePickup(ShadowLanternPuzzle puzzle) { p = puzzle; }
    public string EntityId => "sound_bottle_pickup";
    public Vector3 Position => p.BottlePos;
    public string Prompt(GameState s) => p.Solved && !p.BottleTaken ? "E　拿起空瓶" : "";
    public bool CanInteract(GameState s) => p.Solved && !p.BottleTaken;
    public void Interact(GameState s, InteractionService svc) => p.TakeBottle();
}

// ---------------------------------------------------------------------------------------------

public sealed class SoundBottleSystem : MonoBehaviour
{
    public static SoundBottleSystem Instance { get; private set; }
    public static readonly string[] Order = { "bell", "swing", "bark", "chop" };
    static readonly string[] Names = { "铃声", "秋千声", "小黑的叫声", "劈柴声" };

    GameState state; EggboyPlayer player; EggboyUI ui; IslandWorld world; DogFollower dog;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    public Vector3 MusicBoxPos { get; private set; }
    public bool Playing { get; private set; }
    public bool Done => state != null && state.flags.Contains("music_box_done");
    readonly List<Transform> houses = new List<Transform>();
    readonly List<Vector3> housePos = new List<Vector3>();
    readonly List<Quaternion> houseRot = new List<Quaternion>();
    public float MaxHouseLift { get; private set; }
    Light boxLight;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
    {
        Instance = this;
        player = p; ui = u; world = w; state = s;
        dog = FindObjectOfType<DogFollower>();
        clips["bell"] = Synth("bell"); clips["swing"] = Synth("swing"); clips["bark"] = Synth("bark"); clips["chop"] = Synth("chop");
        var svc = FindObjectOfType<InteractionService>();
        if (svc != null)
        {
            Register(svc, "bell", GameObject.Find("Bakery_bell"));
            Register(svc, "swing", GameObject.Find("S_swing"));
            Register(svc, "chop", GameObject.Find("Bakery_wood"));
            if (dog != null) svc.Register(new SoundSource(this, "bark", () => dog.transform.position, () => dog.Following && dog.Visible));
        }
        BuildMusicBox();
        if (svc != null) svc.Register(new MusicBoxInteractable(this));
        foreach (var t in FindObjectsOfType<Transform>())
            if ((t.name.StartsWith("B6_") || t.name.StartsWith("B7_")) && t.name.EndsWith("_visual")) houses.Add(t);
    }

    void Register(InteractionService svc, string id, GameObject go)
    {
        if (go == null) return;
        Vector3 pos = go.transform.position;
        svc.Register(new SoundSource(this, id, () => pos, () => true));
    }

    public bool HasBottle => state != null && state.flags.Contains("has_sound_bottle");
    public bool Has(string id) => state != null && state.flags.Contains("sound_" + id);
    public int Count { get { int n = 0; foreach (var id in Order) if (Has(id)) n++; return n; } }
    public static string NameOf(string id) { int i = System.Array.IndexOf(Order, id); return i >= 0 ? Names[i] : id; }

    public void Capture(string id)
    {
        if (!HasBottle || Has(id)) return;
        state.flags.Add("sound_" + id);
        ShadowAndChimeSystem.PlayClip(clips[id], 0.9f);
        if (id == "bark" && dog != null) dog.GuideTo(player.transform.position, 0.5f);
    }

    void BuildMusicBox()
    {
        var f = GameObject.Find("P_FOUNTAIN") ?? GameObject.Find("Fountain_placeholder");
        Vector3 c = f != null ? f.transform.position : Vector3.zero;
        // 喷泉旁边找一块能站的地方放底座
        Vector3 best = c; float bd = float.MaxValue;
        for (float r = 1.8f; r <= 4f; r += 0.3f)
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.PI / 8f;
                var q = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (!world.Walkable(q.x, q.z)) continue;
                if (r < bd) { bd = r; best = q; }
            }
        best.y = world.GroundAt(best.x, best.z, c.y + 2f);
        MusicBoxPos = best;
        var lux = Lux3DService.Ensure();
        var box = lux.InstantiateFallback(lux.Find("wish_conch_musicbox"), 0.55f);
        var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(plinth.GetComponent<Collider>());
        plinth.name = "MusicBoxPlinth";
        plinth.transform.SetParent(transform, false);
        plinth.transform.position = best + Vector3.up * 0.25f;
        plinth.transform.localScale = new Vector3(0.6f, 0.25f, 0.6f);
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh); var col = new Color(0.82f, 0.76f, 0.66f); m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        plinth.GetComponent<Renderer>().sharedMaterial = m;
        if (box != null) { box.transform.SetParent(transform, false); box.transform.position = best + Vector3.up * 0.5f; }
        // 底座四周四个空槽：铃、秋千、爪、斧（刻的字）
        var font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
        string[] glyph = { "铃", "秋千", "汪", "柴" };
        Vector3 toFountain = (c - best); toFountain.y = 0f;
        Vector3 front = toFountain.sqrMagnitude > 0.01f ? -toFountain.normalized : Vector3.forward;
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("MusicBoxSlot_" + Order[i]);
            go.transform.SetParent(transform, false);
            Vector3 right = new Vector3(front.z, 0f, -front.x);
            go.transform.position = best + Vector3.up * 0.32f + front * 0.31f + right * ((i - 1.5f) * 0.13f);
            go.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font; tm.text = glyph[i]; tm.fontSize = 64; tm.characterSize = 0.006f;
            tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(0.35f, 0.25f, 0.16f);
            if (font != null) go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        var lgo = new GameObject("MusicBoxLight");
        lgo.transform.SetParent(transform, false);
        lgo.transform.position = best + Vector3.up * 1.2f;
        boxLight = lgo.AddComponent<Light>();
        boxLight.type = LightType.Point; boxLight.range = 6f; boxLight.intensity = 0f; boxLight.color = new Color(1f, 0.85f, 0.55f);
        world.AddCollider(new EggboyCollider { x = best.x, z = best.z, r = 0.35f });
    }

    public void PlayMusicBox()
    {
        if (Playing || Count < 4) return;
        StartCoroutine(Show());
    }

    IEnumerator Show()
    {
        Playing = true;
        if (player != null) player.locked = true;
        housePos.Clear(); houseRot.Clear();
        foreach (var h in houses) { housePos.Add(h.localPosition); houseRot.Add(h.localRotation); }
        // 先把四个声音放一遍，再奏一段旋律；房子跟着拍子一起一伏
        float[] melody = { 523.25f, 659.25f, 783.99f, 659.25f, 880f, 783.99f, 659.25f, 523.25f, 587.33f, 659.25f, 523.25f };
        float beat = 0.5f;
        float t = 0f; int mi = -4; float next = 0f;
        float total = 4 * 0.6f + melody.Length * beat + 0.8f;
        MaxHouseLift = 0f;
        while (t < total)
        {
            t += Time.deltaTime;
            if (t >= next)
            {
                if (mi < 0) { ShadowAndChimeSystem.PlayClip(clips[Order[mi + 4]], 0.8f); next += 0.6f; }
                else if (mi < melody.Length) { ShadowAndChimeSystem.PlayProceduralChime(melody[mi], 0.55f); next += beat; }
                mi++;
            }
            float env = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((total - t) / 1.2f);
            for (int i = 0; i < houses.Count; i++)
            {
                if (houses[i] == null) continue;
                float ph = i * 0.7f;
                float lift = Mathf.Abs(Mathf.Sin((t / beat) * Mathf.PI + ph)) * 0.22f * env;
                MaxHouseLift = Mathf.Max(MaxHouseLift, lift);
                houses[i].localPosition = housePos[i] + Vector3.up * lift;
                houses[i].localRotation = houseRot[i] * Quaternion.Euler(0f, Mathf.Sin(t * 2.2f + ph) * 3f * env, 0f);
            }
            if (boxLight != null) boxLight.intensity = 2.5f * env * (0.7f + 0.3f * Mathf.Sin(t * 8f));
            yield return null;
        }
        // 精确复位
        for (int i = 0; i < houses.Count; i++)
            if (houses[i] != null) { houses[i].localPosition = housePos[i]; houses[i].localRotation = houseRot[i]; }
        if (boxLight != null) boxLight.intensity = 0.6f;
        if (!state.flags.Contains("music_box_done")) state.flags.Add("music_box_done");
        TideTownAchievementSystem.Unlock("music_box");
        if (player != null) player.locked = false;
        Playing = false;
    }

    public float DebugHouseError()
    {
        float e = 0f;
        for (int i = 0; i < houses.Count && i < housePos.Count; i++)
            if (houses[i] != null) e = Mathf.Max(e, Vector3.Distance(houses[i].localPosition, housePos[i]) + Quaternion.Angle(houses[i].localRotation, houseRot[i]) * 0.001f);
        return e;
    }
    public int HouseCount => houses.Count;

    // ---- 程序合成的四种声音 ----
    static AudioClip Synth(string id)
    {
        const int rate = 22050;
        float dur = id == "bell" ? 1.4f : id == "swing" ? 0.9f : id == "bark" ? 0.5f : 0.35f;
        int n = Mathf.RoundToInt(rate * dur);
        var d = new float[n];
        var rng = new System.Random(id.GetHashCode());
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float v = 0f;
            if (id == "bell")
            {
                // 铜铃：不成谐波的几个分音，长衰减
                float f = 1046.5f;
                v = (Mathf.Sin(2 * Mathf.PI * f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * f * 2.76f * t) * 0.25f
                    + Mathf.Sin(2 * Mathf.PI * f * 5.4f * t) * 0.12f) * Mathf.Exp(-t * 3.2f) * Mathf.Min(1f, t * 200f);
            }
            else if (id == "swing")
            {
                // 秋千：吱——呀，音高先升后降，带点毛刺
                float f = 380f + 520f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                lp += (noise - lp) * 0.3f;
                v = (Mathf.Sin(2 * Mathf.PI * f * t + Mathf.Sin(2 * Mathf.PI * 31f * t) * 2f) * 0.35f + lp * 0.25f)
                    * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
            }
            else if (id == "bark")
            {
                // 小狗：汪、汪两声，每声一个带包络的粗糙方波
                float local = t < 0.22f ? t : t - 0.26f;
                if (local >= 0f && local < 0.2f)
                {
                    float f = 520f - 260f * (local / 0.2f);
                    float sq = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * local));
                    v = (sq * 0.25f + noise * 0.12f) * Mathf.Sin(Mathf.PI * local / 0.2f);
                }
            }
            else
            {
                // 劈柴：一下噪声冲击 + 低沉的一声闷响
                lp += (noise - lp) * 0.15f;
                v = lp * 1.4f * Mathf.Exp(-t * 30f) + Mathf.Sin(2 * Mathf.PI * 95f * t) * 0.5f * Mathf.Exp(-t * 14f);
            }
            d[i] = Mathf.Clamp(v, -1f, 1f);
        }
        var clip = AudioClip.Create("Sound_" + id, n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}

public sealed class SoundSource : IInteractable
{
    readonly SoundBottleSystem sys;
    readonly string id;
    readonly System.Func<Vector3> pos;
    readonly System.Func<bool> available;
    public SoundSource(SoundBottleSystem s, string soundId, System.Func<Vector3> p, System.Func<bool> avail) { sys = s; id = soundId; pos = p; available = avail; }
    public string EntityId => "sound_" + id;
    public Vector3 Position => pos();
    public string Prompt(GameState s) => sys.HasBottle && !sys.Has(id) && available() ? "E　装下" + SoundBottleSystem.NameOf(id) : "";
    public bool CanInteract(GameState s) => sys.HasBottle && !sys.Has(id) && available();
    public void Interact(GameState s, InteractionService svc) => sys.Capture(id);
}

public sealed class MusicBoxInteractable : IInteractable
{
    readonly SoundBottleSystem sys;
    public MusicBoxInteractable(SoundBottleSystem s) { sys = s; }
    public string EntityId => "music_box";
    public Vector3 Position => sys.MusicBoxPos;
    public string Prompt(GameState s)
    {
        if (sys.Playing) return "";
        if (sys.Count >= 4) return sys.Done ? "E　再听一遍" : "E　把声音放进八音盒";
        return sys.HasBottle ? "八音盒上有四个空槽（" + sys.Count + "/4）" : "";
    }
    public bool CanInteract(GameState s) => !sys.Playing && sys.Count >= 4;
    public void Interact(GameState s, InteractionService svc) => sys.PlayMusicBox();
}
