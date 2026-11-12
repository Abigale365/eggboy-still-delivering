// 2026-11-13 植被重做（用户要求：花簇、树、草地更真实，同时保留童趣；参考《塞尔达：旷野之息》）。
//   · 树冠 / 灌木：原来的鼓包留作里面的实心体积，外面加一层叶片面片（一半贴着表面像瓦片，一半立着往外伸），
//     轮廓是叶子边。材质 TideTown/Foliage，会随风晃。
//   · 花：原来的小方块换成五瓣花面片（花瓣 + 花心）。
//   · 草地：草皮上铺真的草叶（一丛三片），材质 TideTown/Grass，有风浪，主角走过会倒；草里零星开小野花。
// 规矩不变：全是装饰，没有碰撞，不在 Ground 层，路线 / 剧情 / 水位判定和以前一样。固定种子，重建结果一致。
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    // ---------- 贴图 ----------

    // 一小簇叶子：九片尖叶从中心往外长。RGB 是每片叶子的深浅（带叶脉），A 是形状。
    static Texture2D LeafClusterTexture()
    {
        string path = TexRoot + "/FloraLeafCluster.png";
        if (!System.IO.File.Exists(path))
        {
            const int n = 256;
            var px = new Color32[n * n];
            var rnd = new Lcg(4401);
            int leaves = 9;
            var ang = new float[leaves]; var len = new float[leaves]; var wid = new float[leaves]; var tone = new float[leaves]; var rad = new float[leaves];
            for (int i = 0; i < leaves; i++)
            {
                ang[i] = (i + rnd.Next01() * 0.7f) / leaves * Mathf.PI * 2f;
                len[i] = 0.24f + rnd.Next01() * 0.08f;
                wid[i] = 0.105f + rnd.Next01() * 0.035f;
                tone[i] = 0.55f + rnd.Next01() * 0.45f;
                rad[i] = 0.17f + rnd.Next01() * 0.05f;
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float bestA = 0f, bestT = 0f;
                for (int i = 0; i < leaves; i++)
                {
                    float ca = Mathf.Cos(ang[i]), sa = Mathf.Sin(ang[i]);
                    // 叶子自己的坐标：a 沿叶长（-1 叶柄 … 1 叶尖），b 横向
                    float lx = u - ca * rad[i], ly = v - sa * rad[i];
                    float a = (lx * ca + ly * sa) / len[i];
                    float b = (-lx * sa + ly * ca) / wid[i];
                    if (a < -1f || a > 1f) continue;
                    float half = Mathf.Pow(1f - a * a, 0.75f) * (0.78f + 0.22f * (1f - a) * 0.5f);   // 靠叶柄宽一点，叶尖收尖
                    float edge = half - Mathf.Abs(b);
                    if (edge <= 0f) continue;
                    float alpha = Mathf.Clamp01(edge / 0.10f);
                    float rib = Mathf.Clamp01(1f - Mathf.Abs(b) / 0.07f) * 0.22f;                       // 叶脉暗一道
                    float t = tone[i] * (0.82f + 0.18f * (a * 0.5f + 0.5f)) - rib;
                    if (alpha >= bestA) { bestA = alpha; bestT = t; }
                }
                // 中心补一团，叶柄之间不留洞
                float core = Mathf.Clamp01((0.11f - Mathf.Sqrt(u * u + v * v)) / 0.03f);
                if (core > bestA) { bestA = core; bestT = 0.5f; }
                byte c = (byte)Mathf.Clamp(bestT * 255f, 0f, 255f);
                px[y * n + x] = new Color32(c, c, c, (byte)Mathf.Clamp(bestA * 255f, 0f, 255f));
            }
            WriteFloraTexture(path, px, n);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 五瓣花：R = 花瓣明暗，G = 花心，A = 形状
    static Texture2D FlowerTexture()
    {
        string path = TexRoot + "/FloraFlower.png";
        if (!System.IO.File.Exists(path))
        {
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float petal = 0.46f * (0.50f + 0.50f * Mathf.Pow(Mathf.Abs(Mathf.Cos(th * 2.5f)), 0.55f));
                float alpha = Mathf.Clamp01((petal - r) / 0.03f);
                float crease = Mathf.Pow(Mathf.Abs(Mathf.Cos(th * 2.5f)), 3f);          // 每瓣中间一道浅浅的折
                float shade = Mathf.Clamp01(0.55f + 1.0f * r + 0.18f * crease);
                float center = Mathf.Clamp01((0.105f - r) / 0.02f);
                px[y * n + x] = new Color32((byte)(shade * 255f), (byte)(center * 255f), 0, (byte)(alpha * 255f));
            }
            WriteFloraTexture(path, px, n);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void WriteFloraTexture(string path, Color32[] px, int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.SetPixels32(px); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = false;                 // 存的是明暗和遮罩，不是颜色
        imp.alphaIsTransparency = false;
        imp.mipmapEnabled = true;
        imp.mipMapsPreserveCoverage = true;      // 远处的叶子不因为缩小而变秃
        imp.alphaTestReferenceValue = 0.42f;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Trilinear;
        imp.anisoLevel = 4;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
    }

    // ---------- 材质 ----------

    static Material FoliageMat(string name, Color32 color, Color32 shade, bool flower, float sway, float flutter)
    {
        var shader = Shader.Find("TideTown/Foliage");
        if (shader == null) throw new Exception("找不到 TideTown/Foliage 着色器（Assets/Resources/TideFoliage.shader）");
        string path = MatRoot + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != shader) m.shader = shader;
        m.SetTexture("_BaseMap", flower ? FlowerTexture() : LeafClusterTexture());
        m.SetColor("_BaseColor", color);
        m.SetColor("_ShadeColor", shade);
        m.SetColor("_CenterColor", new Color32(0xff, 0xcf, 0x4a, 255));
        m.SetFloat("_Cutoff", 0.42f);
        m.SetFloat("_Flower", flower ? 1f : 0f);
        m.SetFloat("_Sway", sway);
        m.SetFloat("_Flutter", flutter);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        m.enableInstancing = false;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material GrassBladeMat()
    {
        var shader = Shader.Find("TideTown/Grass");
        if (shader == null) throw new Exception("找不到 TideTown/Grass 着色器（Assets/Resources/TideGrass.shader）");
        string path = MatRoot + "/GrassBlades.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != shader) m.shader = shader;
        m.SetColor("_BaseColor", new Color(0.20f, 0.40f, 0.13f, 1f));
        m.SetColor("_TipColor", new Color(0.42f, 0.66f, 0.20f, 1f));
        m.SetColor("_DryColor", new Color(0.62f, 0.72f, 0.26f, 1f));
        m.SetFloat("_Bend", 0.11f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- 叶片面片 ----------

    // 在一团鼓包的表面上长叶片。samples[first..] 是鼓包的顶点（当采样点用），center 是整团的中心。
    // 顶点色：R 摆动幅度，G 随机相位，B 树冠里的高度，A = 1（面片）。
    static void AddLeafCards(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<Color32> colors, List<int> tris,
                             int first, int last, Vector3 center, float radius, float cardSize, int seed, float keep, float sway)
    {
        var rnd = new Lcg(seed);
        float yMin = float.MaxValue, yMax = float.MinValue;
        for (int i = first; i < last; i++) { yMin = Mathf.Min(yMin, verts[i].y); yMax = Mathf.Max(yMax, verts[i].y); }
        for (int i = first; i < last; i++)
        {
            if (rnd.Next01() > keep) continue;
            var p = verts[i];
            var d = p - center;
            if (d.sqrMagnitude < 1e-6f) continue;
            d.Normalize();
            if (d.y < -0.55f && rnd.Next01() < 0.7f) continue;      // 树冠正底下少长，反正看不到
            float hgt = Mathf.InverseLerp(yMin, yMax, p.y);
            // 面片的两条边：先取一条和朝外方向垂直的切线
            var side = Vector3.Cross(d, Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            side = Quaternion.AngleAxis(rnd.Next01() * 360f, d) * side;
            var up2 = Vector3.Cross(d, side);
            float s = cardSize * (0.8f + rnd.Next01() * 0.5f);
            bool upright = rnd.Next01() < 0.5f;
            Vector3 c, ax, ay;
            if (upright)
            {
                // 立着往外伸：轮廓上的叶子边就靠它
                ax = side; ay = (d * 0.9f + Vector3.up * 0.25f).normalized;
                c = p + d * s * 0.45f;
            }
            else
            {
                // 贴着表面，稍微翘起来一点，像一层层瓦片
                var tilt = Quaternion.AngleAxis(20f + rnd.Next01() * 30f, side);
                ax = side; ay = tilt * up2;
                c = p + d * s * 0.18f;
            }
            // 明暗用的法线：朝树冠外、略朝上——整棵树是一大块亮面和一大块暗面
            var n = (d + Vector3.up * 0.35f).normalized;
            byte phase = (byte)(rnd.Next01() * 255f);
            var col = new Color32((byte)(Mathf.Clamp01(sway * (0.6f + 0.4f * hgt)) * 255f), phase, (byte)(hgt * 255f), 255);
            int o = verts.Count;
            verts.Add(c - ax * s - ay * s); verts.Add(c + ax * s - ay * s); verts.Add(c + ax * s + ay * s); verts.Add(c - ax * s + ay * s);
            for (int k = 0; k < 4; k++) { normals.Add(n); colors.Add(col); }
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            tris.Add(o); tris.Add(o + 1); tris.Add(o + 2); tris.Add(o); tris.Add(o + 2); tris.Add(o + 3);
        }
    }

    // 给鼓包（实心体积）的顶点补顶点色：A = 0
    static void FillBlobColors(List<Vector3> verts, List<Color32> colors, int first, int last, float sway)
    {
        float yMin = float.MaxValue, yMax = float.MinValue;
        for (int i = first; i < last; i++) { yMin = Mathf.Min(yMin, verts[i].y); yMax = Mathf.Max(yMax, verts[i].y); }
        for (int i = first; i < last; i++)
        {
            float hgt = Mathf.InverseLerp(yMin, yMax, verts[i].y);
            colors.Add(new Color32((byte)(Mathf.Clamp01(sway * (0.6f + 0.4f * hgt)) * 255f), 128, (byte)(hgt * 255f), 0));
        }
    }

    // 一朵花：朝外的一张小面片
    static void AddFlowerCard(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<Color32> colors, List<int> tris,
                              Vector3 c, Vector3 facing, float size, Lcg rnd, float sway, float height = 1f)
    {
        facing.Normalize();
        var side = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        side = Quaternion.AngleAxis(rnd.Next01() * 360f, facing) * side;
        var up2 = Vector3.Cross(facing, side);
        var n = (facing + Vector3.up * 0.5f).normalized;
        // B = 在花丛里的高度：被主角蹭到时，花和它旁边的叶子要动得一样多
        var col = new Color32((byte)(Mathf.Clamp01(sway) * 255f), (byte)(rnd.Next01() * 255f), (byte)(Mathf.Clamp01(height) * 255f), 255);
        int o = verts.Count;
        verts.Add(c - side * size - up2 * size); verts.Add(c + side * size - up2 * size); verts.Add(c + side * size + up2 * size); verts.Add(c - side * size + up2 * size);
        for (int k = 0; k < 4; k++) { normals.Add(n); colors.Add(col); }
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
        tris.Add(o); tris.Add(o + 1); tris.Add(o + 2); tris.Add(o); tris.Add(o + 2); tris.Add(o + 3);
    }

    // ---------- 草地 ----------

    const float GrassPerSquareMetre = 15f;     // 每平方米几丛（一丛三片叶）
    const float GrassChunk = 12f;             // 分块大小（米）：离镜头远的整块不画

    class GrassChunkData
    {
        public List<Vector3> v = new List<Vector3>(); public List<Color32> c = new List<Color32>(); public List<int> t = new List<int>();
        public List<Vector3> fv = new List<Vector3>(); public List<Vector3> fn = new List<Vector3>(); public List<Vector2> fuv = new List<Vector2>();
        public List<Color32> fc = new List<Color32>(); public List<int>[] ft = { new List<int>(), new List<int>(), new List<int>() };
    }

    static void BuildGrass(Layout layout, Transform parent, Material[] meadowFlowers)
    {
        var root = new GameObject("GrassField").transform;   // 名字在 IslandBuilder.Contour.Skip 里登记过：碰撞扫描不看它
        root.SetParent(parent, false);
        var mat = GrassBladeMat();
        var rnd = new Lcg(20261113);
        var chunks = new Dictionary<long, GrassChunkData>();
        int groundMask = LayerMask.GetMask("Ground");
        float step = 1f / Mathf.Sqrt(GrassPerSquareMetre);
        int clumps = 0, flowers = 0;

        var areas = new List<Area>();
        foreach (var t in layout.tiers) if (t.zone == "grass") areas.Add(t);
        if (layout.coast != null) areas.Add(new Area { id = "coast", y = 0.15f, zone = "grass", x = layout.coast.x, z = layout.coast.z });

        foreach (var t in areas)
        {
            if (t.x == null || t.x.Length < 3) continue;
            var poly = new List<Vector2>();
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int i = 0; i < t.x.Length; i++)
            {
                poly.Add(new Vector2(t.x[i], t.z[i]));
                x0 = Mathf.Min(x0, t.x[i]); x1 = Mathf.Max(x1, t.x[i]); z0 = Mathf.Min(z0, t.z[i]); z1 = Mathf.Max(z1, t.z[i]);
            }
            for (float x = x0; x < x1; x += step)
            for (float z = z0; z < z1; z += step)
            {
                float px = x + rnd.Next01() * step, pz = z + rnd.Next01() * step;
                float r0 = rnd.Next01(), r1 = rnd.Next01(), r2 = rnd.Next01(), r3 = rnd.Next01();
                var p2 = new Vector2(px, pz);
                if (!InPolyXZ(poly, p2)) continue;
                // 这块草皮上面要是压着别的东西（石板路、别的台地、沙滩），最高的地面就不是它自己：不长草
                if (!Physics.Raycast(new Vector3(px, t.y + 30f, pz), Vector3.down, out var hit, 60f, groundMask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.point.y - t.y) > 0.006f) continue;
                if (NearBuilding(new Vector3(px, t.y, pz), -0.05f)) continue;
                float edge = DistToEdge(poly, p2);
                if (edge < 0.06f) continue;

                long key = ((long)Mathf.FloorToInt(px / GrassChunk) << 32) ^ (uint)Mathf.FloorToInt(pz / GrassChunk);
                if (!chunks.TryGetValue(key, out var ch)) chunks[key] = ch = new GrassChunkData();

                // 一丛三片：高矮不一，往不同方向倒
                float baseH = 0.13f + r0 * 0.15f;
                byte tint = (byte)(r1 * 255f), phase = (byte)(r2 * 255f);
                for (int b = 0; b < 3; b++)
                {
                    float a = r3 * 6.283f + b * 2.1f + rnd.Next01() * 0.8f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var side = new Vector3(-dir.z, 0f, dir.x);
                    float h = baseH * (0.75f + rnd.Next01() * 0.5f), w = 0.016f + rnd.Next01() * 0.010f;
                    var root0 = new Vector3(px, t.y, pz) + dir * 0.045f;
                    var lean = dir * h * (0.25f + rnd.Next01() * 0.3f);
                    int o = ch.v.Count;
                    ch.v.Add(root0 - side * w); ch.v.Add(root0 + side * w);
                    ch.v.Add(root0 + lean * 0.35f + Vector3.up * h * 0.55f - side * w * 0.7f); ch.v.Add(root0 + lean * 0.35f + Vector3.up * h * 0.55f + side * w * 0.7f);
                    ch.v.Add(root0 + lean + Vector3.up * h);
                    ch.c.Add(new Color32(0, phase, tint, 255)); ch.c.Add(new Color32(0, phase, tint, 255));
                    ch.c.Add(new Color32(140, phase, tint, 255)); ch.c.Add(new Color32(140, phase, tint, 255));
                    ch.c.Add(new Color32(255, phase, tint, 255));
                    ch.t.Add(o); ch.t.Add(o + 1); ch.t.Add(o + 2); ch.t.Add(o + 1); ch.t.Add(o + 3); ch.t.Add(o + 2); ch.t.Add(o + 2); ch.t.Add(o + 3); ch.t.Add(o + 4);
                }
                clumps++;

                // 草里零星的小野花（白、黄、粉），比草高一点，朝天开
                if (rnd.Next01() < 0.035f && edge > 0.3f)
                {
                    var c = new Vector3(px, t.y + baseH + 0.06f + rnd.Next01() * 0.05f, pz);
                    var face = new Vector3(rnd.Next01() - 0.5f, 1.2f, rnd.Next01() - 0.5f);
                    int kind = (int)(rnd.Next01() * 2.999f);
                    AddFlowerCard(ch.fv, ch.fn, ch.fuv, ch.fc, ch.ft[kind], c, face, 0.045f + rnd.Next01() * 0.025f, rnd, 0.35f);
                    flowers++;
                }
            }
        }

        int nChunk = 0, tris = 0;
        foreach (var kv in chunks)
        {
            var ch = kv.Value;
            if (ch.v.Count > 0)
            {
                var mesh = new Mesh { name = "Grass_" + nChunk, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(ch.v); mesh.SetColors(ch.c); mesh.SetTriangles(ch.t, 0);
                mesh.RecalculateBounds();
                var bb = mesh.bounds; bb.Expand(new Vector3(0.5f, 0.3f, 0.5f)); mesh.bounds = bb;   // 风会把草叶吹出原来的包围盒一点
                mesh = SaveMesh(mesh, NatureMeshRoot + "/Grass_" + nChunk + ".asset");
                var go = new GameObject("Grass_" + nChunk);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                tris += ch.t.Count / 3;
            }
            if (ch.fv.Count > 0)
            {
                var mesh = new Mesh { name = "Meadow_" + nChunk, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(ch.fv); mesh.SetNormals(ch.fn); mesh.SetUVs(0, ch.fuv); mesh.SetColors(ch.fc);
                mesh.subMeshCount = 3;
                for (int k = 0; k < 3; k++) mesh.SetTriangles(ch.ft[k], k);
                mesh.RecalculateBounds();
                mesh = SaveMesh(mesh, NatureMeshRoot + "/Meadow_" + nChunk + ".asset");
                var go = new GameObject("Meadow_" + nChunk);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = meadowFlowers;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            nChunk++;
        }
        Debug.Log($"GRASS_BUILT clumps={clumps} flowers={flowers} chunks={nChunk} tris={tris}");
    }
}
