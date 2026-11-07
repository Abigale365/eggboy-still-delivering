// 2026-11-07 新地图街景（preview.png v2）：
//   · 石板路材质（CobbleMat）：主街、广场、灯塔台这些 zone=stone 的台地顶面直接用它（IslandBuilder.BuildMain）
//   · 木栅栏：可走区外沿、外面落差 ≥ 1.5 m 的地方（临海步道、灯塔台），用 Lux3D 的 S7 一段段排开
//   · 路灯：栅栏线上每 4 段一盏 Lux3D 的 S4
//   · 小船：Lux3D 的 S9 放到 28 号沙滩
//   · 喷泉占位：中央广场 FOUNTAIN 点，灰盒拼的（F1 模型到了再换），带碰撞
// 栅栏、路灯、小船都不进 Ground 层、没有碰撞：GroundAt / Walkable 和以前一样，路线和剧情判定不受影响。
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    static readonly List<Vector3> streetscapeBlockers = new List<Vector3>();   // 小船、喷泉这类大件，花丛和礁石要避开

    // ---------- 石板贴图 ----------

    static Material CobbleMat()
    {
        if (!AssetDatabase.IsValidFolder(TexRoot)) AssetDatabase.CreateFolder("Assets/TideTown/Art", "Textures");
        string albedoPath = TexRoot + "/NatureCobble_Albedo.png";
        string normalPath = TexRoot + "/NatureCobble_Normal.png";
        if (!System.IO.File.Exists(albedoPath) || !System.IO.File.Exists(normalPath))
        {
            BuildCobbleTextures(albedoPath, normalPath, 83);
            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("NATURETEX generated NatureCobble");
        }
        ConfigureSurfaceTexture(albedoPath, false);
        ConfigureSurfaceTexture(normalPath, true);
        string path = MatRoot + "/IslandCobble.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.12f);
        m.SetFloat("_Metallic", 0f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        m.EnableKeyword("_NORMALMAP");
        var scale = new Vector2(1f / 1.8f, 1f / 1.8f);   // 一张 1.8 m，每块石板约 0.22 m（2.6 m 时石块显大、像地砖）
        m.SetTextureScale("_BaseMap", scale);
        m.SetTextureScale("_BumpMap", scale);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 错位网格的 Worley 噪声：F1 决定石块隆起，F2-F1 小的地方是灰缝
    static void BuildCobbleTextures(string albedoPath, string normalPath, int seed)
    {
        int n = SurfaceTexSize;
        int cells = 8;
        float cs = n / (float)cells;
        var pts = new Vector2[cells * cells];
        var tone = new float[cells * cells];
        var hue = new float[cells * cells];
        for (int cy = 0; cy < cells; cy++)
        for (int cx = 0; cx < cells; cx++)
        {
            int k = cy * cells + cx;
            float off = (cy % 2) * 0.5f;     // 隔行错开半块，像铺出来的
            pts[k] = new Vector2((cx + off + 0.2f + Hash01(cx, cy, seed) * 0.6f) * cs,
                                 (cy + 0.2f + Hash01(cx, cy, seed + 1) * 0.6f) * cs);
            tone[k] = Hash01(cx, cy, seed + 2);
            hue[k] = Hash01(cx, cy, seed + 3);
        }
        var height = new float[n * n];
        var albedo = new Color32[n * n];
        var normal = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float f1 = float.MaxValue, f2 = float.MaxValue; int best = 0;
            int gx = Mathf.FloorToInt(x / cs), gy = Mathf.FloorToInt(y / cs);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int cx = gx + dx, cy = gy + dy;
                int wx = ((cx % cells) + cells) % cells, wy = ((cy % cells) + cells) % cells;
                var p = pts[wy * cells + wx];
                float px = p.x + (cx - wx) * cs, py = p.y + (cy - wy) * cs;
                float ddx = x + 0.5f - px, ddy = y + 0.5f - py;
                float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                if (d < f1) { f2 = f1; f1 = d; best = wy * cells + wx; }
                else if (d < f2) f2 = d;
            }
            float edge = (f2 - f1) / cs;
            float mortar = Mathf.Clamp01(1f - edge / 0.12f);
            float dome = Mathf.Clamp01(1f - f1 / (cs * 0.75f));
            float u = x / (float)n, v = y / (float)n;
            float grain = LatticeNoise(u, v, 96, seed + 9);
            float mott = Fbm(u, v, 3, seed + 11);
            int i = y * n + x;
            height[i] = (1f - mortar) * (0.35f + 0.5f * Mathf.Sqrt(dome)) + 0.08f * grain;
            // 石块：暖米色 / 浅粉，块与块深浅不一；灰缝偏沙色、更暗（目标图的路是暖粉米色）
            float t = tone[best], h = hue[best];
            float sr = 0.80f + 0.10f * h, sg = 0.64f + 0.05f * h, sb = 0.54f - 0.03f * h;   // 暖粉米色
            if (h < 0.2f) { sr = 0.74f; sg = 0.66f; sb = 0.60f; }
            float shade = 0.74f + 0.22f * t + 0.10f * (mott - 0.5f) + 0.06f * (grain - 0.5f) + 0.10f * dome;
            float r = Mathf.Lerp(sr * shade, 0.44f, mortar);
            float g = Mathf.Lerp(sg * shade, 0.35f, mortar);
            float b = Mathf.Lerp(sb * shade, 0.28f, mortar);
            albedo[i] = new Color32(ToByte(r), ToByte(g), ToByte(b), 255);
        }
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int xm = Wrap(x - 1, n), xp = Wrap(x + 1, n), ym = Wrap(y - 1, n), yp = Wrap(y + 1, n);
            float gx = (height[xp + y * n] - height[xm + y * n]) * 0.5f;
            float gy = (height[x + yp * n] - height[x + ym * n]) * 0.5f;
            var nrm = new Vector3(-gx * 6f, -gy * 6f, 1f).normalized;
            normal[x + y * n] = new Color32(ToByte(nrm.x * 0.5f + 0.5f), ToByte(nrm.y * 0.5f + 0.5f), ToByte(nrm.z * 0.5f + 0.5f), 255);
        }
        WriteSurfaceTexture(albedoPath, albedo, n);
        WriteSurfaceTexture(normalPath, normal, n);
    }

    // ---------- 摆放 ----------

    static void BuildStreetscape(Layout layout, Transform parent, List<WalkArea> walks)
    {
        streetscapeBlockers.Clear();
        var root = new GameObject("Streetscape").transform;
        root.SetParent(parent, false);
        int fences = 0, lamps = 0;

        // 1. 矮木栅栏 + 路灯（目标图：每条石板路两边都围着一圈矮木栅栏，隔一段一盏路灯）
        //    沿每块平的可走区（石板路、小广场、学校草坪、灯塔台）的外沿；外面是别的可走区（路口）或贴着房子/摊位的地方留口子。
        //    栅栏是程序拼的（立柱 + 两道横杆，0.75 m 高），一块可走区合成一个网格；不单独做碰撞——
        //    栅栏正好立在可走区边线上，边线本来就挡人，看到的和撞到的一致。
        var wood = FlatMat("FenceWood", new Color32(0x8a, 0x5e, 0x3a, 255), 0.08f);
        var fenceRoot = new GameObject("Fences").transform; fenceRoot.SetParent(root, false);
        float lampRun = 0f;
        for (int wi = 0; wi < walks.Count; wi++)
        {
            var w = walks[wi];
            string id = layout.walk[wi].id;
            if (!id.StartsWith("pad_") && id != "lighthouse") continue;    // 坡道、码头、船棚不围
            var verts = new List<Vector3>(); var tris = new List<int>();
            var poly = w.poly;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                float len = (b - a).magnitude;
                if (len < 0.05f) continue;
                var dir = (b - a) / len; var outN = new Vector2(dir.y, -dir.x);
                if (InPolyXZ(poly, (a + b) * 0.5f + outN * 0.05f)) outN = -outN;
                int posts = Mathf.Max(1, Mathf.RoundToInt(len / 1.25f));
                Vector2? prev = null;
                for (int s = 0; s <= posts; s++)
                {
                    var c = Vector2.Lerp(a, b, s / (float)posts) - outN * 0.08f;
                    // 留口子：外面 0.5 m 是别的可走区（路口），或者贴着建筑/摊位
                    var probe = c + outN * 0.5f;
                    bool gap = NearBuilding(new Vector3(c.x, w.y, c.y), 0.9f);
                    if (!gap)
                        foreach (var o in walks)
                            if (o != w && Mathf.Abs(o.y - w.y) < 3f && DistToPoly(o.poly, probe) < 0.35f) { gap = true; break; }
                    if (gap) { prev = null; continue; }
                    AddFenceBox(verts, tris, new Vector3(c.x, w.y, c.y), new Vector3(0.11f, 0.75f, 0.11f), 0f);
                    if (prev.HasValue)
                    {
                        var m = (prev.Value + c) * 0.5f; float l = (c - prev.Value).magnitude;
                        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        AddFenceBox(verts, tris, new Vector3(m.x, w.y + 0.34f, m.y), new Vector3(0.05f, 0.06f, l), yaw);
                        AddFenceBox(verts, tris, new Vector3(m.x, w.y + 0.60f, m.y), new Vector3(0.05f, 0.06f, l), yaw);
                        fences++;
                        lampRun += l;
                        if (lampRun > 9f && id.StartsWith("pad_stone"))
                        {
                            lampRun = 0f;
                            var lamp = new GameObject("Lamp");
                            lamp.transform.SetParent(root, false);
                            lamp.transform.position = new Vector3(c.x, w.y + 0.01f, c.y);
                            MountSourceModel(lamp.transform, "S4", Vector3.zero, 2.2f, true, yaw);
                            lamps++;
                        }
                    }
                    prev = c;
                }
            }
            if (verts.Count == 0) continue;
            var mesh = new Mesh { name = "Fence_" + id, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, NatureMeshRoot + "/Fence_" + id + ".asset");
            var fgo = new GameObject("Fence_" + id);
            fgo.transform.SetParent(fenceRoot, false);
            fgo.AddComponent<MeshFilter>().sharedMesh = mesh;
            fgo.AddComponent<MeshRenderer>().sharedMaterial = wood;
            GameObjectUtility.SetStaticEditorFlags(fgo, StaticEditorFlags.BatchingStatic);
        }

        // 2. 小船：28 号沙滩，船头斜朝海
        if (layout.points != null)
            foreach (var pt in layout.points)
            {
                if (pt.id != "28") continue;
                float gy = GroundBelow(pt.x, pt.z, 2f);
                var holder = new GameObject("BeachBoat");
                holder.transform.SetParent(root, false);
                holder.transform.position = new Vector3(pt.x, Mathf.Max(gy, 0.12f) + 0.02f, pt.z);
                MountSourceModel(holder.transform, "S9", Vector3.zero, 3.0f, false, 115f);
                streetscapeBlockers.Add(holder.transform.position);
            }

        // 3. 喷泉占位：中央广场。F1 月亮喷泉模型到了再换；碰撞用 TownObstacle 盒子，人不能穿过去
        if (layout.points != null)
            foreach (var pt in layout.points)
            {
                if (pt.id != "FOUNTAIN") continue;
                BuildFountainPlaceholder(root, new Vector3(pt.x, GroundBelow(pt.x, pt.z, pt.y + 0.5f), pt.z));
                streetscapeBlockers.Add(new Vector3(pt.x, pt.y, pt.z));
            }
        Debug.Log($"STREETSCAPE fences={fences} lamps={lamps} blockers={streetscapeBlockers.Count}");
    }

    // 一根立柱或一道横杆：底面中心 center（横杆是中心），尺寸 size（x 厚、y 高、z 长），绕 Y 转 yaw 度
    static void AddFenceBox(List<Vector3> v, List<int> t, Vector3 center, Vector3 size, float yaw)
    {
        var rot = Quaternion.Euler(0f, yaw, 0f);
        bool post = yaw == 0f && size.y > 0.5f;
        var c = post ? center + Vector3.up * size.y * 0.5f : center;
        var h = size * 0.5f;
        Vector3 P(float x, float y, float z) => c + rot * new Vector3(x * h.x, y * h.y, z * h.z);
        // 6 个面各 4 个顶点（平直法线），每个面从外面看是顺时针
        var corners = new[]
        {
            new[] { P(-1,-1, 1), P( 1,-1, 1), P( 1, 1, 1), P(-1, 1, 1) },
            new[] { P( 1,-1,-1), P(-1,-1,-1), P(-1, 1,-1), P( 1, 1,-1) },
            new[] { P( 1,-1, 1), P( 1,-1,-1), P( 1, 1,-1), P( 1, 1, 1) },
            new[] { P(-1,-1,-1), P(-1,-1, 1), P(-1, 1, 1), P(-1, 1,-1) },
            new[] { P(-1, 1, 1), P( 1, 1, 1), P( 1, 1,-1), P(-1, 1,-1) },
            new[] { P(-1,-1,-1), P( 1,-1,-1), P( 1,-1, 1), P(-1,-1, 1) },
        };
        foreach (var f in corners)
        {
            int o = v.Count;
            v.AddRange(f);
            t.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
        }
    }

    static void BuildFountainPlaceholder(Transform parent, Vector3 basePos)
    {
        var stone = FlatMat("FountainStone", new Color32(0xec, 0xdf, 0xc8, 255), 0.15f);
        var water = FlatMat("FountainWater", new Color32(0x5a, 0xc0, 0xd6, 255), 0.92f);
        var gold = FlatMat("FountainGold", new Color32(0xe8, 0xb4, 0x48, 255), 0.6f);
        gold.SetFloat("_Metallic", 0.7f);
        var go = new GameObject("Fountain_placeholder");
        go.transform.SetParent(parent, false);
        go.transform.position = basePos;
        void Part(PrimitiveType type, string name, Vector3 local, Vector3 scale, Material mat)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = local;
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = mat;
        }
        Part(PrimitiveType.Cylinder, "basin", new Vector3(0, 0.25f, 0), new Vector3(3.8f, 0.25f, 3.8f), stone);
        Part(PrimitiveType.Cylinder, "water", new Vector3(0, 0.46f, 0), new Vector3(3.4f, 0.02f, 3.4f), water);
        Part(PrimitiveType.Cylinder, "pillar", new Vector3(0, 0.9f, 0), new Vector3(0.45f, 0.45f, 0.45f), stone);
        Part(PrimitiveType.Cylinder, "bowl", new Vector3(0, 1.4f, 0), new Vector3(1.6f, 0.08f, 1.6f), stone);
        Part(PrimitiveType.Cylinder, "bowl_water", new Vector3(0, 1.49f, 0), new Vector3(1.4f, 0.01f, 1.4f), water);
        Part(PrimitiveType.Cylinder, "stem", new Vector3(0, 1.75f, 0), new Vector3(0.22f, 0.3f, 0.22f), stone);
        Part(PrimitiveType.Sphere, "moon", new Vector3(0, 2.4f, 0), new Vector3(0.75f, 0.75f, 0.2f), gold);

        var col = new GameObject("Fountain_collider");
        col.transform.SetParent(go.transform, false);
        col.transform.localPosition = new Vector3(0, 0.5f, 0);
        col.layer = 7;
        var box = col.AddComponent<BoxCollider>();
        box.size = new Vector3(3.6f, 1f, 3.6f);
        col.AddComponent<TownObstacle>();
        Debug.Log($"FOUNTAIN placeholder at ({basePos.x:F2},{basePos.y:F2},{basePos.z:F2})");
    }
}
