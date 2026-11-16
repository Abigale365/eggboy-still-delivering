// 2026-11-12 塞尔达式卡通材质（样张）：进游戏时把场景里不透明的写实材质换成 TideTown/Toon。
// 不改工程里的材质文件，换的是运行时的副本——加 -notoon 启动就是原来的样子，方便前后对比。
// 两种来源的材质都认：URP Lit（地面、树、石头、栅栏这些程序生成的）和 glTFast 的（Lux3D 模型）。
// 不换的：透明的（玻璃、水、光束）、粒子、EGGBOY 自己（面屏有专门的着色器）、不认识的着色器。
//
// 2026-11-15 打磨工程书 V2，在换材质的时候顺手做三件事（都只动运行时的副本，共用的源材质和卡通材质不改）：
//   · PL-16 民居屋顶换色：B6 瓦房、B7 木板房的屋顶按"相邻不同色"从调色板里分一档，每档派生一份材质。
//     五座地标（邮局、面包店、学校、灯塔、船棚）不碰。-noroof 启动 = 全镇原色（对比用）。
//   · PL-13 窗灯：窗玻璃的材质带上 _WindowGlow，亮度跟着路灯走（着色器里乘全局 _TideLampOn）。
//   · PL-03 镂空的豁免：地面（Ground 层）和带骨骼的东西（小黑）不参与"挡住主角就镂空"，派生一份 _CamFadeOff = 1 的材质。
//     2026-11-15 加上海堤、路缘、护栏、桩基、崖壁、坡道路基和侧墙（仍在层 0，按名字认，见 IsSolidStructure）。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TownToon : MonoBehaviour
{
    public static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-notoon") < 0;

    // TownLighting 在挂这个组件之前设（来自 TownLookSet，look.json 可调）
    public static Color WindowGlow = Color.black;
    public static float RoofVariety = 1f;

    static readonly bool onlyGround = Array.IndexOf(Environment.GetCommandLineArgs(), "-toonground") >= 0;   // 调试：只换地面
    static readonly bool noRoof = Array.IndexOf(Environment.GetCommandLineArgs(), "-noroof") >= 0;           // 对比：屋顶保持原色
    const int GroundLayer = 6;

    Shader toon;
    readonly Dictionary<Material, Material> cache = new Dictionary<Material, Material>();
    readonly Dictionary<Material, Material> cacheNoTangent = new Dictionary<Material, Material>();
    readonly Dictionary<Material, Material> noFade = new Dictionary<Material, Material>();      // 卡通材质 → 不参与镂空的副本
    readonly Dictionary<long, Material> roofMats = new Dictionary<long, Material>();           // （卡通材质，调色档位）→ 屋顶副本
    readonly Dictionary<Material, Material> glowMats = new Dictionary<Material, Material>();    // 卡通材质 → 带窗灯的副本
    readonly Dictionary<Mesh, Mesh> shutterMeshes = new Dictionary<Mesh, Mesh>();
    readonly Dictionary<Transform, int> roofPlan = new Dictionary<Transform, int>();           // 民居的 _visual 根 → 调色档位（0 = 原色）
    readonly HashSet<int> seen = new HashSet<int>();
    float nextSweep;
    int converted, skipped, roofRenderers, glassMats, glassRenderers;

    // 屋顶调色板（低饱和，和奶油墙、木墙都搭）。0 号是"保持原色"。
    static readonly Color[] RoofPalette =
    {
        Color.white,
        new Color32(168, 78, 62, 255),     // 1 砖红
        new Color32(112, 138, 160, 255),   // 2 雾蓝
        new Color32(124, 148, 110, 255),   // 3 鼠尾草绿
        new Color32(150, 134, 120, 255),   // 4 暖灰瓦
    };
    static readonly string[] RoofNames = { "原色", "砖红", "雾蓝", "鼠尾草绿", "暖灰瓦" };

    void Awake()
    {
        toon = Resources.Load<Shader>("TideToon");
        if (toon == null) { Debug.LogWarning("TOWNTOON 找不到 Resources/TideToon"); enabled = false; return; }
        Shader.SetGlobalFloat("_ToonDebug", Array.IndexOf(Environment.GetCommandLineArgs(), "-toondebug") >= 0 ? 1f : 0f);
        if (!noRoof && RoofVariety > 0.001f) PlanRoofs();
        Sweep();
        Debug.Log($"TOWNTOON materials={cache.Count + cacheNoTangent.Count} renderers={converted} skipped={skipped} " +
                  $"noFade={noFade.Count} roofMats={roofMats.Count} roofRenderers={roofRenderers} glassMats={glassMats} glassParts={glassRenderers}");    }

    // 后来才出现的东西（小黑、运行时摆出来的道具）隔一会儿补换一次
    void Update()
    {
        if (Time.unscaledTime < nextSweep) return;
        nextSweep = Time.unscaledTime + 1.5f;
        Sweep();
    }

    void Sweep()
    {
        foreach (var r in FindObjectsOfType<Renderer>(true))
        {
            if (!seen.Add(r.GetInstanceID())) continue;
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) { skipped++; continue; }
            if (IsHero(r.transform)) { skipped++; continue; }
            if (onlyGround && r.gameObject.layer != GroundLayer) { skipped++; continue; }
            bool tangents = HasTangents(r);
            bool keepSolid = r.gameObject.layer == GroundLayer || r is SkinnedMeshRenderer || IsSolidStructure(r.transform);
            int roof = keepSolid ? 0 : RoofVariantOf(r.transform);
            string roofKey = roof > 0 ? RoofMaterialKey(r.transform) : null;
            var mats = r.sharedMaterials;
            if (!keepSolid && mats.Length == 1 && LightTileHouseWindows(r, mats[0], tangents))
            {
                converted++;
                glassRenderers++;
                continue;
            }
            bool changed = false, tinted = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                var cur = Convert(src, tangents) ?? src;      // 本来就是卡通材质的（礁石、崖壁、沙子）Convert 返回 null，原样往下走
                if (cur != null && cur.shader == toon)
                {
                    if (keepSolid) cur = NoFadeCopy(cur);
                    else if (roofKey != null && IsRoofPart(src, r, roofKey)) { cur = RoofCopy(cur, roof); tinted = true; }
                    else if (IsWindowPart(r)) { cur = GlowCopy(cur); glassRenderers++; }
                    else if (IsBakeryArchPart(r)) { cur = GlowCopy(cur, r.gameObject.name.StartsWith("Door plank joint", StringComparison.OrdinalIgnoreCase) ? 0.9f : 0.55f); glassRenderers++; }
                }
                if (cur != src) { mats[i] = cur; changed = true; }
            }
            if (changed) { r.sharedMaterials = mats; converted++; }
            if (tinted) roofRenderers++;
        }
    }

    static bool IsHero(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name == "EGGBOY") return true;
        return false;
    }

    static bool HasTangents(Renderer r)
    {
        Mesh m = null;
        if (r is SkinnedMeshRenderer s) m = s.sharedMesh;
        else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
        return m != null && m.HasVertexAttribute(VertexAttribute.Tangent);
    }

    // ───────────── PL-16 民居屋顶换色 ─────────────

    // 只有 B6（瓦房）、B7（木板房）算民居；B1–B5 五座地标的名字不是这两个前缀，进不来
    static bool IsHouseRoot(string n)
    {
        return n != null && n.EndsWith("_visual", StringComparison.Ordinal)
            && (n.StartsWith("B6_", StringComparison.Ordinal) || n.StartsWith("B7_", StringComparison.Ordinal));
    }

    static Transform HouseRootOf(Transform t)
    {
        for (; t != null; t = t.parent) if (IsHouseRoot(t.name)) return t;
        return null;
    }

    int RoofVariantOf(Transform t)
    {
        if (roofPlan.Count == 0) return 0;
        var root = HouseRootOf(t);
        return root != null && roofPlan.TryGetValue(root, out int v) ? v : 0;
    }

    // 屋顶是哪一块：瓦房的屋顶是"Old coral enamel"那一块（橙红瓦），木板房的是"Worn slate blue paint"（青灰板）。
    // 同一块里还有门、窗套之类同色的小件：着色器里只动朝上的面，那些竖着的不受影响。
    static string RoofMaterialKey(Transform t)
    {
        var root = HouseRootOf(t);
        if (root == null) return null;
        return root.name.StartsWith("B6_", StringComparison.Ordinal) ? "Old coral enamel" : "Worn slate blue paint";
    }

    static bool IsRoofPart(Material src, Renderer r, string key)
    {
        return (src != null && src.name.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            || r.gameObject.name.StartsWith(key, StringComparison.OrdinalIgnoreCase);
    }

    // 颜色分几路：原来的橙瓦和砖红算一路，原来的青灰和雾蓝算一路，绿、暖灰各一路。挨着的两栋不能同一路。
    static int Family(int variant, bool tileHouse)
    {
        if (variant == 0) return tileHouse ? 1 : 2;
        return variant;   // 1 砖红 → 橙红一路；2 雾蓝 → 青灰一路；3、4 各自一路
    }

    static uint StableHash(string s)
    {
        uint h = 2166136261u;
        foreach (char c in s) { h ^= c; h *= 16777619u; }
        return h;
    }

    // 给每栋民居定一档颜色：按名字排好序挨个定（结果只跟名字和位置有关，每次进游戏都一样），
    // 每栋的候选顺序由名字决定，四成的房子先试原色；选第一个和 9 米内已定的邻居不同路的。
    void PlanRoofs()
    {
        var houses = new List<Transform>();
        foreach (var t in FindObjectsOfType<Transform>(true))
            if (IsHouseRoot(t.name)) houses.Add(t);
        houses.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        var placed = new List<KeyValuePair<Vector3, int>>();
        var log = new System.Text.StringBuilder();
        foreach (var h in houses)
        {
            bool tileHouse = h.name.StartsWith("B6_", StringComparison.Ordinal);
            uint hash = StableHash(h.name);
            int[] pal = tileHouse ? new[] { 1, 2, 3, 4 } : new[] { 1, 3, 4 };   // 木板房原来就是青灰顶，雾蓝太像，不给
            var cand = new List<int>();
            int start = (int)(hash % (uint)pal.Length);
            for (int i = 0; i < pal.Length; i++) cand.Add(pal[(start + i) % pal.Length]);
            if ((hash / 7u) % 5u < 2u) cand.Insert(0, 0); else cand.Add(0);

            int pick = cand[0];
            foreach (int c in cand)
            {
                int fam = Family(c, tileHouse);
                bool clash = false;
                foreach (var p in placed)
                    if (p.Value == fam && (p.Key - h.position).sqrMagnitude < 9f * 9f) { clash = true; break; }
                if (!clash) { pick = c; break; }
            }
            roofPlan[h] = pick;
            placed.Add(new KeyValuePair<Vector3, int>(h.position, Family(pick, tileHouse)));
            log.Append(h.name.Replace("_visual", "")).Append('=').Append(RoofNames[pick]).Append(' ');
        }
        Debug.Log($"TOWNTOON roofs houses={houses.Count} {log}");
    }

    Material RoofCopy(Material toonMat, int variant)
    {
        long key = ((long)toonMat.GetInstanceID() << 4) | (long)variant;
        if (roofMats.TryGetValue(key, out var done)) return done;
        var m = new Material(toonMat) { name = toonMat.name + " roof:" + RoofNames[variant] };
        // 原屋顶的平均颜色：着色器拿它判断"这个像素是不是屋顶那一路颜色"，并把瓦片的深浅按比例搬到新颜色上
        if (AverageColor(toonMat.GetTexture("_BaseMap"), out var avg))
        {
            var f = toonMat.GetColor("_BaseColor");
            m.SetColor("_RoofSrc", new Color(avg.r * f.r, avg.g * f.g, avg.b * f.b, 1f));
        }
        else m.SetColor("_RoofSrc", new Color(0.5f, 0.5f, 0.5f, 0f));   // 读不到贴图（无显卡跑审计）：不做色域判断
        m.SetColor("_RoofTint", RoofPalette[variant]);
        m.SetFloat("_RoofAmount", RoofVariety);
        roofMats[key] = m;
        return m;
    }

    // 贴图缩到 8×8 读回来取平均（贴图本身不可读也能用）。返回的是 sRGB 颜色，交给 SetColor 时 Unity 自己转线性。
    static bool AverageColor(Texture tex, out Color avg)
    {
        avg = Color.gray;
        if (tex == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;
        var rt = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Texture2D read = null;
        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            read = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
            var px = read.GetPixels32();
            float r = 0f, g = 0f, b = 0f;
            foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
            float k = 1f / (px.Length * 255f);
            avg = new Color(r * k, g * k, b * k, 1f);
            return avg.r + avg.g + avg.b > 0.06f;   // 全黑 = 没读到
        }
        catch (Exception e)
        {
            Debug.LogWarning("TOWNTOON 屋顶平均色读取失败：" + e.Message);
            return false;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (read != null) Destroy(read);
        }
    }

    // ───────────── PL-13 窗灯 ─────────────

    static string BuildingId(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            string n = t.name;
            if (n.Length > 9 && n[0] == 'B' && char.IsDigit(n[1]) && n[2] == '_' &&
                n.EndsWith("_visual", StringComparison.Ordinal)) return n.Substring(0, 2);
        }
        return null;
    }

    static bool IsWindowPart(Renderer r)
    {
        if (WindowGlow.maxColorComponent <= 0.001f) return false;
        string name = r.gameObject.name;
        bool glass = name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;
        bool recess = name.Equals("Recess shadow", StringComparison.OrdinalIgnoreCase);
        if (!glass && !recess) return false;
        string building = BuildingId(r.transform);
        // B2 合批后的 Recess shadow 只包含店面窗玻璃。
        return building != null && (glass || building == "B2");
    }

    // 合批后窗面和别的木件在同一份网格里的几处：按烘焙网格的局部坐标把窗面三角形分到第二个子网格，单独挂发光副本。
    //   · B6 瓦房：拱窗、背面方窗、侧面圆窗是"Sage painted wood"木窗扇，前面压着百叶板 → 发光压到 0.7，从板缝里透出来像室内灯。
    //   · B2 面包店的三个深拱洞没有合批（每个部件单独一个渲染器），不走这里，见 IsBakeryArchPart。
    struct PaneRule { public string building, material; public float strength; public Func<Vector3, bool> inside; }
    static readonly PaneRule[] PaneRules =
    {
        new PaneRule { building = "B6", material = "Sage painted wood", strength = 0.7f, inside = IsTileHouseWindow },
    };

    // 2026-11-16 C1：面包店正门、背门、侧面三个深拱洞（"Weathered warm timber"，画面上那块一直不亮的深色就是它）。
    // 每个拱洞单独一个渲染器（名字就是 GLB 里的 "Deep arched opening*"），整个拱洞发 0.55 的暖光，像门里透出的室内灯，不跟店面大窗抢
    // 拱洞里其实装着木门（侧面那块"深色"就是门），光靠拱洞边一圈发光看不太出来：
    // 门板之间的板缝（"Door plank joint*"，2 cm 宽的细条）也发 0.9 的暖光，像屋里的灯从门缝里透出来。
    static bool IsBakeryArchPart(Renderer r)
    {
        if (WindowGlow.maxColorComponent <= 0.001f) return false;
        string n = r.gameObject.name;
        return (n.StartsWith("Deep arched opening", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Door plank joint", StringComparison.OrdinalIgnoreCase))
            && BuildingId(r.transform) == "B2";
    }

    bool LightTileHouseWindows(Renderer renderer, Material source, bool tangents)
    {
        if (WindowGlow.maxColorComponent <= 0.001f || source == null) return false;
        string id = BuildingId(renderer.transform);
        PaneRule rule = default; bool found = false;
        foreach (var pr in PaneRules)
            if (pr.building == id && source.name.StartsWith(pr.material, StringComparison.OrdinalIgnoreCase)) { rule = pr; found = true; break; }
        if (!found) return false;
        var filter = renderer.GetComponent<MeshFilter>();
        var mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1) return false;
        var material = Convert(source, tangents) ?? source;
        if (material.shader != toon) return false;
        if (!shutterMeshes.TryGetValue(mesh, out var split))
        {
            var vertices = mesh.vertices;
            var triangles = mesh.GetTriangles(0);
            var lit = new List<int>();
            var rest = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var part = rule.inside(vertices[a]) && rule.inside(vertices[b]) && rule.inside(vertices[c]) ? lit : rest;
                part.Add(a);
                part.Add(b);
                part.Add(c);
            }
            if (lit.Count == 0 || rest.Count == 0) return false;
            split = Instantiate(mesh);
            split.name = mesh.name + " window panes";
            split.subMeshCount = 2;
            split.SetTriangles(rest, 0, false);
            split.SetTriangles(lit, 1, false);
            shutterMeshes.Add(mesh, split);
        }
        filter.sharedMesh = split;
        renderer.sharedMaterials = new[] { material, GlowCopy(material, rule.strength) };
        return true;
    }

    static bool IsTileHouseWindow(Vector3 p)
    {
        bool front = p.z > 1.35f && p.x > 0.48f && p.x < 1.42f && p.y > 0.93f && p.y < 2f;
        bool back = p.z < -1.38f && Mathf.Abs(p.x) < 0.31f && p.y > 1.51f && p.y < 2.15f;
        bool side = p.x < -2.10f && Mathf.Abs(p.z) < 0.33f && p.y > 1.20f && p.y < 1.86f;
        return front || back || side;
    }

    Material GlowCopy(Material toonMat, float strength = 1f)
    {
        if (toonMat.GetColor("_WindowGlow").maxColorComponent > 0.001f) return toonMat;   // 材质本身已经是会亮的玻璃
        var key = strength >= 0.999f ? toonMat : null;
        if (key != null && glowMats.TryGetValue(key, out var done)) return done;
        string name = toonMat.name + " glow" + (strength < 0.999f ? strength.ToString("0.00") : "");
        foreach (var kv in glowMats) if (kv.Value.name == name) return kv.Value;
        var m = new Material(toonMat) { name = name };
        m.SetColor("_WindowGlow", WindowGlow * strength);
        glowMats[key ?? m] = m;
        return m;
    }

    // ───────────── PL-03 镂空的豁免 ─────────────

    // 2026-11-15 地形修复：海堤、路缘、护栏、桩基、崖壁这些"长在地上的硬结构"也不镂空。
    // 它们不能改到 Ground 层（层 6）——轮廓碰撞会跳过层 6、IslandWorld.GroundAt 会把层 6 当地板，护栏就成了能踩的地面——
    // 所以保持层 0，在这里按名字认出来，派生 _CamFadeOff = 1 的材质。
    // 不然相机贴着砖梯、石墙往下看时，抖动网点把前景挖出一片黑点，看着像地上漏洞。
    static readonly string[] CoastalSolidPrefixes =
    {
        "upper_seawall", "descent_wall_", "harbor_quay", "school_plinth", "gate_plinth_",
        "upper_seaward_", "descent_rail_", "descent_sill_", "harbor_pilings", "harbor_rope",
        "lighthouse_sidewall", "lighthouse_rail_", "ramp_sidewall_",
    };

    static bool IsSolidStructure(Transform t)
    {
        string n = t.name;
        if (n.StartsWith("Cliff_", StringComparison.Ordinal) || n == "Rock roadbed" || n == "PromenadeCurbs"
            || (n.StartsWith("Finished Road", StringComparison.Ordinal) && n.IndexOf("stair", StringComparison.Ordinal) >= 0))
            return true;
        for (var p = t; p != null; p = p.parent)
        {
            if (p.name == "HarborPier" || p.name == "RoadSideWalls" || p.name == "MapExpansion") return true;
            if (p.name == "CoastalPromenade")
            {
                foreach (var prefix in CoastalSolidPrefixes)
                    if (n.StartsWith(prefix, StringComparison.Ordinal)) return true;
                return false;
            }
        }
        return false;
    }

    Material NoFadeCopy(Material toonMat)
    {
        if (noFade.TryGetValue(toonMat, out var done)) return done;
        var m = new Material(toonMat) { name = toonMat.name + " solid" };
        m.SetFloat("_CamFadeOff", 1f);
        noFade[toonMat] = m;
        return m;
    }

    // ───────────── 写实材质 → 卡通材质 ─────────────

    Material Convert(Material src, bool tangents)
    {
        if (src == null || src.shader == null || src.shader == toon) return null;
        var map = tangents ? cache : cacheNoTangent;
        if (map.TryGetValue(src, out var done)) return done;

        Material m = null;
        string sn = src.shader.name;
        bool gltf = src.HasProperty("baseColorTexture") || src.HasProperty("baseColorFactor");
        bool lit = sn == "Universal Render Pipeline/Lit" || sn == "Universal Render Pipeline/Simple Lit";
        bool transparent = src.renderQueue >= 2900
            || (src.HasProperty("_Surface") && src.GetFloat("_Surface") > 0.5f);
        if ((gltf || lit) && !transparent)
        {
            m = new Material(toon) { name = src.name + " (toon)" };
            if (gltf)
            {
                if (src.HasProperty("baseColorTexture"))
                {
                    m.SetTexture("_BaseMap", src.GetTexture("baseColorTexture"));
                    m.SetTextureScale("_BaseMap", src.GetTextureScale("baseColorTexture"));
                    m.SetTextureOffset("_BaseMap", src.GetTextureOffset("baseColorTexture"));
                }
                if (src.HasProperty("baseColorFactor")) m.SetColor("_BaseColor", src.GetColor("baseColorFactor"));
                var n = src.HasProperty("normalTexture") ? src.GetTexture("normalTexture") : null;
                if (n != null && tangents)
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", src.HasProperty("normalTexture_scale") ? src.GetFloat("normalTexture_scale") : 1f);
                    m.SetFloat("_UseNormal", 1f);
                }
                var e = src.HasProperty("emissiveTexture") ? src.GetTexture("emissiveTexture") : null;
                if (src.HasProperty("emissiveFactor"))
                {
                    var ec = src.GetColor("emissiveFactor");
                    if (ec.maxColorComponent > 0.001f)
                    {
                        m.SetColor("_EmissionColor", ec);
                        if (e != null) m.SetTexture("_EmissionMap", e);
                    }
                }
                if (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", src.HasProperty("alphaCutoff") ? src.GetFloat("alphaCutoff") : 0.5f);
                }
            }
            else
            {
                m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                m.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
                m.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
                m.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                var n = src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
                if (n != null && tangents && src.IsKeywordEnabled("_NORMALMAP"))
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", src.HasProperty("_BumpScale") ? src.GetFloat("_BumpScale") : 1f);
                    m.SetFloat("_UseNormal", 1f);
                }
                if (src.IsKeywordEnabled("_EMISSION") && src.HasProperty("_EmissionColor"))
                {
                    m.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
                    if (src.HasProperty("_EmissionMap") && src.GetTexture("_EmissionMap") != null)
                        m.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
                }
                if (src.IsKeywordEnabled("_ALPHATEST_ON"))
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
                }
            }
            // PL-13 窗灯：窗玻璃（各栋房子的 "Smoked blue window glass…"）跟着路灯一起亮
            if (WindowGlow.maxColorComponent > 0.001f && src.name.IndexOf("window glass", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                m.SetColor("_WindowGlow", WindowGlow);
                glassMats++;
            }
            m.SetFloat("_Cull", src.HasProperty("_Cull") ? src.GetFloat("_Cull") : (float)CullMode.Back);
            m.renderQueue = m.GetFloat("_AlphaClip") > 0.5f ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.enableInstancing = src.enableInstancing;
        }
        map[src] = m;
        return m;
    }
}
