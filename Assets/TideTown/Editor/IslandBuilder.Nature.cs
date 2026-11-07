// 2026-11-07 场景环境重做（替换原来的圆球石头 + 圆柱棒棒糖树）：
//   · 圆石：程序生成 6 种不规则圆石，沿每层台地边缘和海岸成排堆叠，盖住垂直崖壁（预览图的“石头堆起来的岛”）
//   · 树：多团簇树冠 + 收分树干，树冠法线朝团簇中心偏，远看是一团一团的软体积
//   · 花丛：路边一圈小灌木，带白 / 粉 / 黄小花
//   · 海里零星礁石
//   · 草地、沙滩、崖壁、石头、树叶都换成程序贴图（albedo + 法线），不再是纯色
// 规矩：所有装饰都没有碰撞、不在 Ground 层，不放在可走区里，所以路线、剧情、水位的判定和以前完全一样。
// 只用固定种子，重建两次结果一致。不花 Lux3D 积分。
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    const string NatureMeshRoot = "Assets/TideTown/Art/Meshes/Nature";

    // ---------- 材质 ----------

    enum NatureKind { Grass, Rock, Leaves, Sand }

    static Material NatureMat(string name, string key, NatureKind kind, Color32 tint, int seed, float tile, float normalStrength, float smooth = 0.06f)
    {
        if (!AssetDatabase.IsValidFolder(TexRoot)) AssetDatabase.CreateFolder("Assets/TideTown/Art", "Textures");
        string albedoPath = TexRoot + "/" + key + "_Albedo.png";
        string normalPath = TexRoot + "/" + key + "_Normal.png";
        if (!System.IO.File.Exists(albedoPath) || !System.IO.File.Exists(normalPath))
        {
            BuildNatureTextures(albedoPath, normalPath, kind, tint, seed, normalStrength);
            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"NATURETEX generated {key}");
        }
        ConfigureSurfaceTexture(albedoPath, false);
        ConfigureSurfaceTexture(normalPath, true);

        string path = MatRoot + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        m.EnableKeyword("_NORMALMAP");
        var scale = new Vector2(1f / tile, 1f / tile);
        m.SetTextureScale("_BaseMap", scale);
        m.SetTextureScale("_BumpMap", scale);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material FlatMat(string name, Color32 c, float smooth = 0.1f)
    {
        var m = Mat(name, c);
        m.SetFloat("_Smoothness", smooth);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildNatureTextures(string albedoPath, string normalPath, NatureKind kind, Color32 tint, int seed, float normalStrength)
    {
        int n = SurfaceTexSize;
        var height = new float[n * n];
        var albedo = new Color32[n * n];
        var normal = new Color32[n * n];
        float br = tint.r / 255f, bg = tint.g / 255f, bb = tint.b / 255f;

        // 叶子 / 草用一堆圆斑（叶簇），石头用裂纹 + 斑驳，沙子用细颗粒
        var blobs = new float[n * n];
        var blobTone = new float[n * n];
        if (kind == NatureKind.Leaves || kind == NatureKind.Grass)
        {
            var rnd = new Lcg(seed + 5);
            int count = kind == NatureKind.Leaves ? 900 : 1400;
            for (int k = 0; k < count; k++)
            {
                float cx = rnd.Next01() * n, cy = rnd.Next01() * n;
                float rad = kind == NatureKind.Leaves ? 6f + rnd.Next01() * 9f : 1.5f + rnd.Next01() * 3.5f;
                float ry = rad;
                float tone = rnd.Next01();
                int x0 = Mathf.FloorToInt(cx - rad) - 1, x1 = Mathf.CeilToInt(cx + rad) + 1;
                int y0 = Mathf.FloorToInt(cy - ry) - 1, y1 = Mathf.CeilToInt(cy + ry) + 1;
                for (int py = y0; py <= y1; py++)
                for (int px = x0; px <= x1; px++)
                {
                    float dx = WrapDelta(px + 0.5f - cx, n) / rad;
                    float dy = WrapDelta(py + 0.5f - cy, n) / ry;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= 1f) continue;
                    int i = Wrap(px, n) + Wrap(py, n) * n;
                    float dome = Mathf.Sqrt(1f - d2);
                    if (dome > blobs[i]) { blobs[i] = dome; blobTone[i] = tone; }
                }
            }
        }

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float u = x / (float)n, v = y / (float)n;
            int i = y * n + x;
            float m = Fbm(u, v, 4, seed + 11);
            float g = LatticeNoise(u, v, 128, seed + 23);
            float shade, h;
            float r, gg, b;
            switch (kind)
            {
                case NatureKind.Rock:
                {
                    // 裂纹：低频噪声的等值线附近变暗
                    // 细裂纹 + 大块斑驳；裂纹又窄又淡，远看不成条纹
                    float c = Fbm(u, v, 3, seed + 41);
                    float crack = Mathf.Clamp01(1f - Mathf.Abs(c - 0.5f) * 40f);
                    float lichen = Mathf.Clamp01((Fbm(u, v, 3, seed + 77) - 0.64f) * 5f);
                    shade = 0.86f + 0.24f * (m - 0.5f) + 0.08f * (g - 0.5f) - 0.12f * crack;
                    h = 0.5f * m + 0.15f * g - 0.15f * crack;
                    r = Mathf.Lerp(br * shade, 0.55f * shade, lichen * 0.5f);
                    gg = Mathf.Lerp(bg * shade, 0.60f * shade, lichen * 0.5f);
                    b = Mathf.Lerp(bb * shade, 0.38f * shade, lichen * 0.5f);
                    break;
                }
                case NatureKind.Sand:
                {
                    shade = 0.92f + 0.12f * (m - 0.5f) + 0.10f * (g - 0.5f);
                    if (Hash01(x, y, seed + 3) < 0.05f) shade -= 0.10f;
                    h = 0.3f * m + 0.3f * g;
                    r = br * shade; gg = bg * shade; b = bb * shade;
                    break;
                }
                default:
                {
                    // 叶簇 / 草叶：圆斑中心亮、边缘暗，斑与斑之间是深色缝
                    float d = blobs[i], t = blobTone[i];
                    float hue = (t - 0.5f) * (kind == NatureKind.Leaves ? 0.30f : 0.16f);
                    shade = kind == NatureKind.Leaves ? 0.55f + 0.55f * d + 0.12f * (m - 0.5f)
                                                      : 0.90f + 0.10f * d + 0.22f * (m - 0.5f);   // 草地对比度低，远看不花，只留大块深浅
                    h = d * 0.8f + 0.1f * m;
                    r = br * shade * (1f + hue * 0.9f);
                    gg = bg * shade * (1f + hue * 0.3f);
                    b = bb * shade * (1f - hue * 0.4f);
                    break;
                }
            }
            height[i] = h;
            albedo[i] = new Color32(ToByte(r), ToByte(gg), ToByte(b), 255);
        }

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int xm = Wrap(x - 1, n), xp = Wrap(x + 1, n), ym = Wrap(y - 1, n), yp = Wrap(y + 1, n);
            float gx = (height[xp + y * n] - height[xm + y * n]) * 0.5f;
            float gy = (height[x + yp * n] - height[x + ym * n]) * 0.5f;
            var nrm = new Vector3(-gx * normalStrength, -gy * normalStrength, 1f).normalized;
            normal[x + y * n] = new Color32(ToByte(nrm.x * 0.5f + 0.5f), ToByte(nrm.y * 0.5f + 0.5f), ToByte(nrm.z * 0.5f + 0.5f), 255);
        }
        WriteSurfaceTexture(albedoPath, albedo, n);
        WriteSurfaceTexture(normalPath, normal, n);
    }

    // ---------- 网格 ----------

    static void Icosphere(int subdiv, List<Vector3> verts, List<int> tris)
    {
        verts.Clear(); tris.Clear();
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v0 = {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
        foreach (var v in v0) verts.Add(v.normalized);
        int[] f0 = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                     3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        tris.AddRange(f0);
        for (int s = 0; s < subdiv; s++)
        {
            var cache = new Dictionary<long, int>();
            var next = new List<int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int m)) return m;
                verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                cache[key] = verts.Count - 1;
                return verts.Count - 1;
            }
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris.Clear(); tris.AddRange(next);
        }
    }

    static float Noise3(Vector3 p, int seed)
    {
        int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
        float fx = p.x - xi, fy = p.y - yi, fz = p.z - zi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
        float H(int x, int y, int z) => Hash01(x * 73856093 ^ z * 19349663, y, seed);
        float a = Mathf.Lerp(H(xi, yi, zi), H(xi + 1, yi, zi), fx);
        float b = Mathf.Lerp(H(xi, yi + 1, zi), H(xi + 1, yi + 1, zi), fx);
        float c = Mathf.Lerp(H(xi, yi, zi + 1), H(xi + 1, yi, zi + 1), fx);
        float d = Mathf.Lerp(H(xi, yi + 1, zi + 1), H(xi + 1, yi + 1, zi + 1), fx);
        return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
    }

    // 球面 UV：按经纬度展开，每个面拉伸都差不多（斜投影会在某些朝向拉成条纹）。scale ≈ 球的周长 / 一张贴图铺的米数
    static Vector2 SphereUV(Vector3 d, float scale)
    {
        float lon = Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI) + 0.5f;
        float lat = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI + 0.5f;
        return new Vector2(lon * scale * 2f, lat * scale);
    }

    static float Fbm3(Vector3 p, int seed) => (Noise3(p, seed) * 0.6f + Noise3(p * 2.1f, seed + 7) * 0.3f + Noise3(p * 4.3f, seed + 13) * 0.1f);

    // 圆石：半径约 1，底部略平
    static Mesh RockMesh(int variant, int subdiv = 2)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        Icosphere(subdiv, v, t);
        var off = new Vector3(variant * 3.7f, variant * 1.3f, variant * 2.9f);
        var uv = new List<Vector2>();
        float squash = 0.7f + (variant % 3) * 0.08f;
        for (int i = 0; i < v.Count; i++)
        {
            var d = v[i];
            float r = 0.80f + 0.42f * (Fbm3(d * 1.4f + off, 900 + variant) - 0.5f) * 2f * 0.5f + 0.2f;
            var p = d * r;
            p.y *= squash;
            if (p.y < -0.35f) p.y = Mathf.Lerp(p.y, -0.35f, 0.6f);
            v[i] = p;
            uv.Add(SphereUV(d, 1.6f));
        }
        string nm = (subdiv >= 2 ? "Rock_" : "RockLo_") + variant;
        var mesh = new Mesh { name = nm };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return SaveMesh(mesh, NatureMeshRoot + "/" + nm + ".asset");
    }

    // 团簇：若干个鼓包拼成一团；法线往团簇中心方向偏，阴影是一整团的软过渡，不是一个个球
    static void AddBlobCluster(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
                               Vector3 center, float radius, int blobs, float flatten, int seed, float bend = 0.6f)
    {
        var rnd = new Lcg(seed);
        var sv = new List<Vector3>(); var st = new List<int>();
        Icosphere(1, sv, st);
        for (int k = 0; k < blobs; k++)
        {
            Vector3 c;
            float br;
            if (k == 0) { c = Vector3.zero; br = radius * 0.78f; }
            else
            {
                float ang = rnd.Next01() * Mathf.PI * 2f;
                float dist = radius * (0.38f + rnd.Next01() * 0.30f);
                c = new Vector3(Mathf.Cos(ang) * dist, (rnd.Next01() - 0.35f) * radius * 0.6f, Mathf.Sin(ang) * dist);
                br = radius * (0.48f + rnd.Next01() * 0.22f);
            }
            int o = verts.Count;
            var noiseOff = new Vector3(seed * 0.37f + k, k * 2.1f, seed * 0.11f);
            for (int i = 0; i < sv.Count; i++)
            {
                var d = sv[i];
                float r = br * (0.88f + 0.24f * Fbm3(d * 2f + noiseOff, seed + k));
                var p = c + d * r;
                p.y *= flatten;
                p += center;
                verts.Add(p);
                var radial = (p - center); radial.y /= Mathf.Max(0.3f, flatten);
                normals.Add(Vector3.Lerp(d, radial.normalized, bend).normalized);
                uvs.Add(SphereUV(d, br * 2.2f) + new Vector2(k * 0.37f, k * 0.61f));
            }
            foreach (var idx in st) tris.Add(o + idx);
        }
    }

    static Mesh TreeMesh(int variant)
    {
        var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>();
        var crown = new List<int>(); var trunk = new List<int>();
        var rnd = new Lcg(400 + variant);
        float h = 1.5f + rnd.Next01() * 0.6f;
        float cr = 1.25f + rnd.Next01() * 0.35f;
        AddBlobCluster(verts, normals, uvs, crown, new Vector3(0, h + cr * 0.55f, 0), cr, 6 + variant % 3, 0.9f, 500 + variant * 17);

        // 树干：8 边收分圆柱，插进树冠
        int sides = 8; int o = verts.Count;
        float r0 = 0.17f, r1 = 0.10f, top = h + cr * 0.4f;
        for (int s = 0; s <= sides; s++)
        {
            float a = s / (float)sides * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts.Add(dir * r0 + new Vector3(0, -0.2f, 0)); normals.Add(dir); uvs.Add(new Vector2(s / (float)sides, 0f));
            verts.Add(dir * r1 + new Vector3(0.05f * Mathf.Sin(variant), top, 0)); normals.Add(dir); uvs.Add(new Vector2(s / (float)sides, top));
        }
        for (int s = 0; s < sides; s++)
        {
            int a = o + s * 2;
            trunk.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
        }
        var mesh = new Mesh { name = "Tree_" + variant, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2; mesh.SetTriangles(crown, 0); mesh.SetTriangles(trunk, 1);
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return SaveMesh(mesh, NatureMeshRoot + "/Tree_" + variant + ".asset");
    }

    static Mesh BushMesh(int variant)
    {
        var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>();
        var leaf = new List<int>(); var flower = new List<int>();
        AddBlobCluster(verts, normals, uvs, leaf, new Vector3(0, 0.28f, 0), 0.5f, 4, 0.75f, 700 + variant * 13, 0.7f);

        // 小花：只长在上半球
        var rnd = new Lcg(800 + variant);
        var sv = new List<Vector3>(); var st = new List<int>();
        Icosphere(0, sv, st);
        int count = 10 + variant * 2;
        for (int k = 0; k < count; k++)
        {
            float ang = rnd.Next01() * Mathf.PI * 2f, el = 0.25f + rnd.Next01() * 0.9f;
            var dir = new Vector3(Mathf.Cos(ang) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(ang) * Mathf.Cos(el));
            var c = new Vector3(0, 0.28f, 0) + new Vector3(dir.x * 0.55f, dir.y * 0.38f, dir.z * 0.55f);
            int o = verts.Count;
            float fr = 0.045f + rnd.Next01() * 0.03f;
            foreach (var d in sv) { verts.Add(c + d * fr); normals.Add((d + Vector3.up).normalized); uvs.Add(Vector2.zero); }
            foreach (var idx in st) flower.Add(o + idx);
        }
        var mesh = new Mesh { name = "Bush_" + variant, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2; mesh.SetTriangles(leaf, 0); mesh.SetTriangles(flower, 1);
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return SaveMesh(mesh, NatureMeshRoot + "/Bush_" + variant + ".asset");
    }

    // ---------- 摆放 ----------

    class WalkArea { public float y; public List<Vector2> poly; }

    static bool InPolyXZ(List<Vector2> poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    static float DistToPoly(List<Vector2> poly, Vector2 p)
    {
        if (InPolyXZ(poly, p)) return 0f;
        float best = float.MaxValue;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[j]; var b = poly[i]; var ab = b - a;
            float tt = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            best = Mathf.Min(best, (a + ab * tt - p).magnitude);
        }
        return best;
    }

    // 这一圈（中心 c、半径 r、高度 y0..y1）会不会盖到某块可走区
    static bool BlocksWalk(List<WalkArea> walks, Vector3 c, float r, float y0, float y1, float margin)
    {
        var cc = new Vector2(c.x, c.z);
        foreach (var w in walks)
        {
            if (w.y < y0 - 0.5f || w.y > y1 + 0.3f) continue;
            if (DistToPoly(w.poly, cc) < r + margin) return true;
        }
        return false;
    }

    static bool NearBuilding(Vector3 p, float margin)
    {
        foreach (var f in footprints)
            if (Mathf.Abs(p.x - f.x) < f.w * 0.5f + margin && Mathf.Abs(p.z - f.z) < f.d * 0.5f + margin) return true;
        return false;
    }

    // 往下打 Ground 层，取 maxY 以下最高的地面；没有就当海底
    static float GroundBelow(float x, float z, float maxY)
    {
        float best = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(new Vector3(x, maxY + 0.05f, z), Vector3.down, 50f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
            best = Mathf.Max(best, hit.point.y);
        return float.IsNegativeInfinity(best) ? -1.2f : best;
    }

    static GameObject Place(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 pos, Vector3 scale, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static int nRockStatic;

    static void BuildNature(Layout layout, Transform parent)
    {
        nRockStatic = 0;
        if (!AssetDatabase.IsValidFolder(NatureMeshRoot)) AssetDatabase.CreateFolder("Assets/TideTown/Art/Meshes", "Nature");
        Physics.SyncTransforms();

        var rockA = NatureMat("NatureRockA", "NatureRockA", NatureKind.Rock, new Color32(0xb8, 0x95, 0x80, 255), 61, 2.2f, 6f);
        var rockB = NatureMat("NatureRockB", "NatureRockB", NatureKind.Rock, new Color32(0xa3, 0x86, 0x74, 255), 67, 2.2f, 6f);
        var leafA = NatureMat("NatureLeafA", "NatureLeafA", NatureKind.Leaves, new Color32(0x6e, 0x8e, 0x3a, 255), 71, 1.6f, 5f);
        var leafB = NatureMat("NatureLeafB", "NatureLeafB", NatureKind.Leaves, new Color32(0x84, 0x9c, 0x3c, 255), 73, 1.6f, 5f);
        var leafC = NatureMat("NatureLeafC", "NatureLeafC", NatureKind.Leaves, new Color32(0x5a, 0x7c, 0x38, 255), 79, 1.6f, 5f);
        var trunk = FlatMat("NatureTrunk", new Color32(0x6e, 0x4c, 0x30, 255), 0.05f);
        var flowerW = FlatMat("NatureFlowerWhite", new Color32(0xf6, 0xf0, 0xe0, 255), 0.2f);
        var flowerP = FlatMat("NatureFlowerPink", new Color32(0xf0, 0xa8, 0xb4, 255), 0.2f);
        var flowerY = FlatMat("NatureFlowerYellow", new Color32(0xf2, 0xcf, 0x6a, 255), 0.2f);

        var rocks = new Mesh[6]; for (int i = 0; i < rocks.Length; i++) rocks[i] = RockMesh(i);
        var rocksLo = new Mesh[6]; for (int i = 0; i < rocksLo.Length; i++) rocksLo[i] = RockMesh(i, 1);   // 80 面，用在小石头和水下
        Mesh RockFor(float size, float y) => (size > 1.5f && y > 0.5f) ? rocks[nRockStatic++ % 6] : rocksLo[nRockStatic++ % 6];
        var trees = new Mesh[5]; for (int i = 0; i < trees.Length; i++) trees[i] = TreeMesh(i);
        var bushes = new Mesh[4]; for (int i = 0; i < bushes.Length; i++) bushes[i] = BushMesh(i);
        Material[] leafMats = { leafA, leafB, leafC };
        var leafPink = NatureMat("NatureLeafPink", "NatureLeafPink", NatureKind.Leaves, new Color32(0xec, 0xa6, 0xc4, 255), 89, 1.6f, 5f);   // 开花树
        Material[] flowerMats = { flowerW, flowerP, flowerY };

        var walks = new List<WalkArea>();
        if (layout.walk != null)
            foreach (var a in layout.walk)
            {
                var poly = new List<Vector2>();
                for (int i = 0; i < a.x.Length; i++) poly.Add(new Vector2(a.x[i], a.z[i]));
                walks.Add(new WalkArea { y = a.y, poly = poly });
            }

        // 栅栏、路灯、小船、喷泉占位（IslandBuilder.Nature2.cs），先放，后面的花丛和礁石避开它们
        BuildStreetscape(layout, parent, walks);

        var root = new GameObject("Nature").transform;
        root.SetParent(parent, false);
        var rockRoot = new GameObject("Boulders").transform; rockRoot.SetParent(root, false);
        var treeRoot = new GameObject("Trees").transform; treeRoot.SetParent(root, false);
        var bushRoot = new GameObject("Bushes").transform; bushRoot.SetParent(root, false);
        var rnd = new Lcg(20261107);
        int nRock = 0, nTree = 0, nBush = 0;

        // 1. 崖边圆石：每层台地（含海岸底座）沿边走一圈，往外、往下成排堆
        var areas = new List<Area>(layout.tiers);
        if (layout.coast != null) areas.Add(new Area { id = "coast", y = 0.15f, x = layout.coast.x, z = layout.coast.z });
        foreach (var t in areas)
        {
            if (t.x == null || t.x.Length < 3) continue;
            var poly = new List<Vector2>();
            for (int i = 0; i < t.x.Length; i++) poly.Add(new Vector2(t.x[i], t.z[i]));
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                float len = (b - a).magnitude;
                if (len < 0.05f) continue;
                var dir = (b - a) / len;
                var outN = new Vector2(dir.y, -dir.x);
                var mid = (a + b) * 0.5f;
                if (InPolyXZ(poly, mid + outN * 0.3f)) outN = -outN;
                int steps = Mathf.Max(1, Mathf.RoundToInt(len / 1.35f));
                for (int s = 0; s < steps; s++)
                {
                    var e = Vector2.Lerp(a, b, (s + 0.5f) / steps);
                    float lower = GroundBelow(e.x + outN.x * 0.9f, e.y + outN.y * 0.9f, t.y - 0.25f);
                    float drop = t.y - lower;
                    if (drop < 0.45f) continue;                       // 台阶太矮，不堆石头

                    // 崖顶一圈花丛（预览图里每道崖边上都是开花的灌木）
                    if (t.id != "coast" && rnd.Next01() < 0.5f)
                    {
                        var bp2 = e + outN * 0.25f;
                        float bs = 1.2f + rnd.Next01() * 0.9f;
                        var bp = new Vector3(bp2.x, t.y - 0.12f, bp2.y);
                        if (!BlocksWalk(walks, bp, 0.45f * bs, t.y - 0.3f, t.y + 0.7f, 0.1f) && !NearBuilding(bp, 0.4f))
                        {
                            Place(bushRoot, "EdgeBush", bushes[nBush % bushes.Length], new[] { leafMats[nBush % 3], flowerMats[(nBush / 3) % 3] },
                                  bp, new Vector3(bs, bs * 0.85f, bs), rnd.Next01() * 360f);
                            nBush++;
                        }
                    }

                    float yb = Mathf.Max(lower, -0.4f);
                    // 从下往上一排排堆，最上面一排顶到台地边缘附近
                    for (float yy = yb; yy < t.y + 0.1f;)
                    {
                        float size = 0.9f + rnd.Next01() * 1.1f;
                        float r = size * 0.5f;
                        float outOff = r * (0.35f + rnd.Next01() * 0.35f) + 0.1f;
                        var c2 = e + outN * outOff + dir * ((rnd.Next01() - 0.5f) * 0.5f);
                        float cy = yy + r * 0.45f;
                        float top = cy + r * 0.75f;
                        if (top > t.y + 0.35f) { cy -= top - (t.y + 0.35f); top = t.y + 0.35f; }
                        var c = new Vector3(c2.x, cy, c2.y);
                        if (!BlocksWalk(walks, c, r, cy - r * 0.7f, top, 0.25f) && !NearBuilding(c, r * 0.6f))
                        {
                            float sq = 0.85f + rnd.Next01() * 0.3f;
                            Place(rockRoot, "Boulder", RockFor(size, cy), new[] { rnd.Next01() < 0.5f ? rockA : rockB },
                                  c, new Vector3(r * sq, r, r * (2f - sq)), rnd.Next01() * 360f);
                            nRock++;
                        }
                        yy += size * 0.75f;
                    }
                }
            }
        }

        // 2. 海里礁石：海岸外 1.5–5 m，稀疏
        if (layout.coast != null)
        {
            var cpoly = new List<Vector2>();
            for (int i = 0; i < layout.coast.x.Length; i++) cpoly.Add(new Vector2(layout.coast.x[i], layout.coast.z[i]));
            for (int i = 0; i < cpoly.Count; i++)
            {
                var a = cpoly[i]; var b = cpoly[(i + 1) % cpoly.Count];
                float len = (b - a).magnitude; if (len < 0.05f) continue;
                var dir = (b - a) / len; var outN = new Vector2(dir.y, -dir.x);
                if (InPolyXZ(cpoly, (a + b) * 0.5f + outN * 0.3f)) outN = -outN;
                int steps = Mathf.Max(1, Mathf.RoundToInt(len / 3.5f));
                for (int s = 0; s < steps; s++)
                {
                    if (rnd.Next01() < 0.45f) continue;
                    var e = Vector2.Lerp(a, b, rnd.Next01()) + outN * (1.5f + rnd.Next01() * 3.5f);
                    float r = 0.5f + rnd.Next01() * 0.9f;
                    var c = new Vector3(e.x, -0.25f + rnd.Next01() * 0.25f, e.y);
                    if (BlocksWalk(walks, c, r, -2f, 2f, 0.4f)) continue;
                    Place(rockRoot, "SeaRock", rocksLo[nRock % rocksLo.Length], new[] { rockB }, c, new Vector3(r, r * 0.9f, r), rnd.Next01() * 360f);
                    nRock++;
                }
            }

            // 2b. 海岸那一圈（在海岸多边形里、不在任何台地上）：铺圆石和灌木，只留 28/29 号的小沙滩
            var tierPolys = new List<List<Vector2>>();
            foreach (var t in layout.tiers)
            {
                if (t.x == null || t.x.Length < 3) continue;
                var tp = new List<Vector2>();
                for (int i = 0; i < t.x.Length; i++) tp.Add(new Vector2(t.x[i], t.z[i]));
                tierPolys.Add(tp);
            }
            var beach = new List<Vector2>();
            if (layout.points != null)
                foreach (var pt in layout.points) if (pt.id == "28" || pt.id == "29") beach.Add(new Vector2(pt.x, pt.z));
            float cx0 = float.MaxValue, cx1 = float.MinValue, cz0 = float.MaxValue, cz1 = float.MinValue;
            foreach (var q in cpoly) { cx0 = Mathf.Min(cx0, q.x); cx1 = Mathf.Max(cx1, q.x); cz0 = Mathf.Min(cz0, q.y); cz1 = Mathf.Max(cz1, q.y); }
            for (float x = cx0; x < cx1; x += 1.7f)
            for (float z = cz0; z < cz1; z += 1.7f)
            {
                var p2 = new Vector2(x + (rnd.Next01() - 0.5f) * 1.2f, z + (rnd.Next01() - 0.5f) * 1.2f);
                if (!InPolyXZ(cpoly, p2)) continue;
                bool onTier = false;
                foreach (var tp in tierPolys) if (DistToPoly(tp, p2) < 0.4f) { onTier = true; break; }
                if (onTier) continue;
                bool nearBoat = false;
                foreach (var bp in streetscapeBlockers) if ((new Vector2(bp.x, bp.z) - p2).magnitude < 2.8f) { nearBoat = true; break; }
                if (nearBoat) continue;
                bool onBeach = false;
                foreach (var bc in beach) if ((bc - p2).magnitude < 5.5f) { onBeach = true; break; }
                float roll = rnd.Next01();
                if (onBeach && roll < 0.85f) continue;                  // 沙滩上只零星几块
                float r = 0.55f + rnd.Next01() * 0.6f;
                var c = new Vector3(p2.x, 0.15f + r * 0.1f, p2.y);
                if (BlocksWalk(walks, c, r, -1f, 1.6f, 0.3f) || NearBuilding(c, r)) continue;
                if (!onBeach && roll < 0.22f)
                {
                    float bs = 1.1f + rnd.Next01() * 0.8f;
                    Place(bushRoot, "ShoreBush", bushes[nBush % bushes.Length], new[] { leafMats[nBush % 3], flowerMats[(nBush / 3) % 3] },
                          new Vector3(p2.x, 0.12f, p2.y), new Vector3(bs, bs * 0.8f, bs), rnd.Next01() * 360f);
                    nBush++;
                    continue;
                }
                float sq = 0.85f + rnd.Next01() * 0.3f;
                Place(rockRoot, "ShoreRock", RockFor(r * 2f, c.y), new[] { rnd.Next01() < 0.5f ? rockA : rockB }, c,
                      new Vector3(r * sq, r * 0.85f, r * (2f - sq)), rnd.Next01() * 360f);
                nRock++;
            }
        }

        // 3. 树：台地上可走区以外，离路 1.6 m、离房子 1.8 m；预览机位朝北看，房子正南 4 m 内不种，免得挡房子
        var placed = new List<Vector2>();
        foreach (var t in layout.tiers)
        {
            if (t.x == null || t.x.Length < 3) continue;
            if (t.zone == "sand") continue;                         // 沙滩不种树
            bool homes = t.zone == "grass" && !t.walk;               // 住宅区：树少一些、离房子远一些，不能把房子盖住
            var poly = new List<Vector2>();
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int i = 0; i < t.x.Length; i++)
            {
                poly.Add(new Vector2(t.x[i], t.z[i]));
                x0 = Mathf.Min(x0, t.x[i]); x1 = Mathf.Max(x1, t.x[i]); z0 = Mathf.Min(z0, t.z[i]); z1 = Mathf.Max(z1, t.z[i]);
            }
            for (float x = x0 + 0.8f; x < x1; x += 1.9f)
            for (float z = z0 + 0.8f; z < z1; z += 1.9f)
            {
                var p2 = new Vector2(x + (rnd.Next01() - 0.5f) * 1.6f, z + (rnd.Next01() - 0.5f) * 1.6f);
                if (!InPolyXZ(poly, p2) || DistToPoly(poly, p2) > 0f) continue;
                // 离台地边缘至少 0.8 m，树不悬空
                bool nearEdge = false;
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    var a = poly[j]; var ab = poly[i] - a;
                    float tt = Mathf.Clamp01(Vector2.Dot(p2 - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    if ((a + ab * tt - p2).magnitude < 0.5f) { nearEdge = true; break; }
                }
                if (nearEdge) continue;
                float gy = GroundBelow(p2.x, p2.y, t.y + 0.3f);
                if (Mathf.Abs(gy - t.y) > 0.3f) continue;
                var p = new Vector3(p2.x, gy, p2.y);
                // 树冠半径约 1.3 m：离路 1.2 m（树冠可以伸到路边，树干不能），离房子 1.5 m
                if (BlocksWalk(walks, p, 0.9f, gy - 0.5f, gy + 2.5f, 0.3f)) continue;
                if (NearBuilding(p, homes ? 2.6f : 1.5f)) continue;
                if (homes && rnd.Next01() < 0.55f) continue;
                bool shadowsHouse = false;
                foreach (var f in footprints)
                    if (Mathf.Abs(p.x - f.x) < f.w * 0.5f + 0.5f && p.z < f.z && p.z > f.z - f.d * 0.5f - (homes ? 4.5f : 2.5f)) { shadowsHouse = true; break; }
                if (shadowsHouse) continue;
                bool crowded = false;
                foreach (var q in placed) if ((q - p2).sqrMagnitude < 1.8f * 1.8f) { crowded = true; break; }
                if (crowded) continue;
                placed.Add(p2);
                float s = 0.85f + rnd.Next01() * 0.55f;
                var crown = (t.id == "main" && rnd.Next01() < 0.3f) ? leafPink : leafMats[nTree % 3];   // 主街草地上三成是粉色开花树
                Place(treeRoot, "Tree", trees[nTree % trees.Length], new[] { crown, trunk }, p, new Vector3(s, s * (0.9f + rnd.Next01() * 0.25f), s), rnd.Next01() * 360f);
                nTree++;
            }
        }

        // 4. 花丛：沿每块可走区的外沿，往外 0.55 m；落在同一层地面上、不在任何可走区里才种
        foreach (var w in walks)
        {
            var poly = w.poly;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                float len = (b - a).magnitude; if (len < 0.05f) continue;
                var dir = (b - a) / len; var outN = new Vector2(dir.y, -dir.x);
                if (InPolyXZ(poly, (a + b) * 0.5f + outN * 0.2f)) outN = -outN;
                int steps = Mathf.Max(1, Mathf.RoundToInt(len / 1.15f));
                for (int s = 0; s < steps; s++)
                {
                    if (rnd.Next01() < 0.35f) continue;
                    var e = Vector2.Lerp(a, b, (s + rnd.Next01()) / steps) + outN * (0.55f + rnd.Next01() * 0.35f);
                    float gy = GroundBelow(e.x, e.y, w.y + 0.4f);
                    if (Mathf.Abs(gy - w.y) > 0.45f) continue;
                    var p = new Vector3(e.x, gy, e.y);
                    float sc = 0.75f + rnd.Next01() * 0.5f;
                    if (BlocksWalk(walks, p, 0.45f * sc, gy - 0.3f, gy + 0.6f, 0.08f)) continue;
                    if (NearBuilding(p, 0.3f)) continue;
                    int v = nBush % bushes.Length;
                    Place(bushRoot, "Bush", bushes[v], new[] { leafMats[(nBush + 1) % 3], flowerMats[(nBush / 4) % 3] }, p,
                          new Vector3(sc, sc * (0.85f + rnd.Next01() * 0.3f), sc), rnd.Next01() * 360f);
                    nBush++;
                }
            }
        }

        int tris = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>()) tris += (int)(mf.sharedMesh.triangles.Length / 3);
        Debug.Log($"NATURE_BUILT boulders={nRock} trees={nTree} bushes={nBush} tris={tris}");
    }
}
