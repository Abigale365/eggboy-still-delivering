// WP-05 生成岛：读 Data/Resources/World/world_layout.json，在 TideTown_Main.unity 里生成纯色白盒。
// 场景结构：Generated/（每次重建清空）+ Manual/（绝不碰）。
// 菜单：潮汐镇/场景/生成主场景（WP-05）；命令行：-executeMethod IslandBuilder.BuildMain
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IslandBuilder
{
    const string ScenePath = "Assets/TideTown/Scenes/TideTown_Main.unity";
    const string ArtRoot = "Assets/TideTown/Art";
    const string MatRoot = "Assets/TideTown/Art/Materials";
    const string LayoutPath = "Assets/TideTown/Data/Resources/World/world_layout.json";
    const float Seabed = -2.0f;

    [Serializable] public class Pt { public string id; public float x, y, z; public bool main; }
    [Serializable] public class Area { public string id; public float y; public bool walk; public float[] x, z; }
    [Serializable] public class RoadDef { public string id, kind; public float width; public bool walk; public float[] x, y, z; }
    [Serializable] public class Bld { public string name, instance, asset; public float x, y, z, width, height; public bool mirror; public float[] box, foot; }
    [Serializable] public class Coast { public float[] x, z; }
    [Serializable] public class Cam { public string name; public float[] pos, euler; public float fov; public int width, height; }
    [Serializable] public class Layout { public Pt[] points; public Coast coast; public Area[] tiers; public Area[] walk; public RoadDef[] roads; public Bld[] buildings; public Cam[] cameras; }

    static Cam previewCam;

    [MenuItem("潮汐镇/场景/生成主场景（WP-05）")]
    public static void BuildMain()
    {
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        if (layout == null || layout.tiers == null) throw new Exception("world_layout.json 读取失败");
        previewCam = null;
        if (layout.cameras != null)
            foreach (var c in layout.cameras) if (c.name == "preview") { previewCam = c; break; }

        // 打开或新建场景：Manual/ 永远保留
        Scene scene;
        if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var old = GameObject.Find("Generated");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var generated = new GameObject("Generated");
        var manual = GameObject.Find("Manual");
        if (manual == null) manual = new GameObject("Manual");

        var grass = Mat("IslandGrass", new Color32(0x86, 0x9d, 0x5b, 255));
        var road = Mat("IslandRoad", new Color32(0xc7, 0xae, 0x87, 255));
        var cliff = Mat("IslandCliff", new Color32(0x99, 0x89, 0x73, 255));
        var rockMat = Mat("IslandRock", new Color32(0x9b, 0x8c, 0x78, 255));
        var leaves = Mat("IslandLeaves", new Color32(0x5f, 0x7f, 0x45, 255));
        var trunk = Mat("IslandTrunk", new Color32(0x6b, 0x4d, 0x31, 255));
        var sea = Mat("IslandSea", new Color32(0x2f, 0x7d, 0x9a, 255));
        var greybox = Mat("IslandBuildingGrey", new Color32(0xb4, 0xb4, 0xb4, 255));

        if (layout.coast != null)
            BuildTier(new Area { id = "coast", y = 0.15f, walk = false, x = layout.coast.x, z = layout.coast.z }, generated.transform, grass, cliff);
        for (int i = 0; i < layout.tiers.Length; i++) BuildTier(layout.tiers[i], generated.transform, grass, cliff);
        if (layout.roads != null)
            foreach (var r in layout.roads) BuildRoad(r, generated.transform, road);
        BuildSea(generated.transform, sea);

        if (layout.buildings != null)
            foreach (var b in layout.buildings) BuildBuilding(b, generated.transform, greybox);

        if (layout.points != null)
            foreach (var p in layout.points)
            {
                var marker = new GameObject("P_" + p.id);
                marker.transform.SetParent(generated.transform, false);
                marker.transform.position = new Vector3(p.x, p.y, p.z);
            }

        ScatterRocksAndTrees(layout, generated.transform, rockMat, leaves, trunk);
        BuildCamera(layout, generated.transform);
        BuildLight(generated.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();

        int tris = 0, renderers = 0;
        foreach (var mf in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            renderers++;
            for (int s = 0; s < m.subMeshCount; s++) tris += (int)(m.GetIndexCount(s) / 3);
        }
        Debug.Log($"ISLAND_BUILT scene={ScenePath} renderers={renderers} tris={tris} buildings={(layout.buildings != null ? layout.buildings.Length : 0)}");
    }

    // ---------- 地形 ----------

    static void BuildTier(Area t, Transform parent, Material top, Material side)
    {
        var poly = new List<Vector2>();
        for (int i = 0; i < t.x.Length; i++) poly.Add(new Vector2(t.x[i], t.z[i]));
        if (poly.Count < 3) return;

        var topGO = new GameObject("Tier_" + t.id);
        topGO.transform.SetParent(parent, false);
        topGO.layer = 6; // Ground
        var mf = topGO.AddComponent<MeshFilter>();
        var mr = topGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = top;
        var mesh = SaveMesh(TopMesh(poly, t.y, t.id), "Assets/TideTown/Art/Meshes/Main_" + t.id + ".asset");
        mf.sharedMesh = mesh;
        topGO.AddComponent<MeshCollider>().sharedMesh = mesh;

        var wallGO = new GameObject("Cliff_" + t.id);
        wallGO.transform.SetParent(parent, false);
        var wmf = wallGO.AddComponent<MeshFilter>();
        wallGO.AddComponent<MeshRenderer>().sharedMaterial = side;
        var wall = SaveMesh(WallMesh(poly, t.y, Seabed, t.id + "_wall"), "Assets/TideTown/Art/Meshes/Main_" + t.id + "_wall.asset");
        wmf.sharedMesh = wall;
    }

    static Mesh TopMesh(List<Vector2> poly, float y, string name)
    {
        var tris = Triangulate(poly);
        var verts = new List<Vector3>();
        var idx = new List<int>();
        foreach (var v in poly) verts.Add(new Vector3(v.x, y, v.y));
        foreach (var t in tris) idx.AddRange(new[] { t.Item1, t.Item3, t.Item2 });
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh WallMesh(List<Vector2> poly, float top, float bottom, string name)
    {
        var verts = new List<Vector3>();
        var idx = new List<int>();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            var at = new Vector3(a.x, top, a.y);
            var bt = new Vector3(b.x, top, b.y);
            var bb = new Vector3(b.x, bottom, b.y);
            var ab = new Vector3(a.x, bottom, a.y);
            AddQuad(verts, idx, at, bt, bb, ab, false);
            AddQuad(verts, idx, at, bt, bb, ab, true);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 每面用独立顶点，正反两面各一份，法线不会被平均成 0
    static void AddQuad(List<Vector3> v, List<int> idx, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool flip)
    {
        int o = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        if (!flip) idx.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
        else idx.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
    }

    // 耳切法：支持简单多边形（可凹）
    static List<(int, int, int)> Triangulate(List<Vector2> pts)
    {
        var result = new List<(int, int, int)>();
        var idx = new List<int>();
        for (int i = 0; i < pts.Count; i++) idx.Add(i);
        if (SignedArea(pts) < 0f) idx.Reverse();

        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool found = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                var a = pts[ia]; var b = pts[ib]; var c = pts[ic];
                if (Cross(b - a, c - a) <= 0f) continue;
                bool inside = false;
                for (int j = 0; j < idx.Count; j++)
                {
                    int ij = idx[j];
                    if (ij == ia || ij == ib || ij == ic) continue;
                    if (PointInTriangle(pts[ij], a, b, c)) { inside = true; break; }
                }
                if (inside) continue;
                result.Add((ia, ib, ic));
                idx.RemoveAt(i);
                found = true;
                break;
            }
            if (!found) break;
        }
        if (idx.Count == 3) result.Add((idx[0], idx[1], idx[2]));
        return result;
    }

    static float SignedArea(List<Vector2> p)
    {
        float s = 0f;
        for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.x * b.y - b.x * a.y; }
        return s * 0.5f;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    // ---------- 路面 ----------

    static void BuildRoad(RoadDef r, Transform parent, Material mat)
    {
        if (r.x == null || r.x.Length < 2) return;
        var go = new GameObject("Road_" + r.id);
        go.transform.SetParent(parent, false);
        go.layer = 6;
        var mf = go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var mesh = SaveMesh(RoadMesh(r), "Assets/TideTown/Art/Meshes/Main_road_" + r.id + ".asset");
        mf.sharedMesh = mesh;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static Mesh RoadMesh(RoadDef r)
    {
        var verts = new List<Vector3>();
        var idx = new List<int>();
        for (int i = 0; i + 1 < r.x.Length; i++)
        {
            var a = new Vector3(r.x[i], r.y[i] + 0.02f, r.z[i]);
            var b = new Vector3(r.x[i + 1], r.y[i + 1] + 0.02f, r.z[i + 1]);
            var d = b - a; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) continue;
            d.Normalize();
            var side = new Vector3(-d.z, 0f, d.x) * (r.width * 0.5f);
            int o = verts.Count;
            verts.Add(a - side); verts.Add(a + side); verts.Add(b + side); verts.Add(b - side);
            idx.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
        }
        var mesh = new Mesh { name = "road_" + r.id };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- 海面 ----------

    static void BuildSea(Transform parent, Material mat)
    {
        var go = new GameObject("Sea");
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var mesh = new Mesh { name = "sea" };
        mesh.vertices = new[] { new Vector3(-200, 0, -200), new Vector3(200, 0, -200), new Vector3(200, 0, 200), new Vector3(-200, 0, 200) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = SaveMesh(mesh, "Assets/TideTown/Art/Meshes/Main_sea.asset");
    }

    // 工程书 WP-05 第 4 步：未减面时建筑先用灰盒。按预览图的建筑框反推世界尺寸与位置。
    static void BuildBuilding(Bld b, Transform parent, Material mat)
    {
        if (b.box == null || b.box.Length < 4 || previewCam == null) { Debug.LogWarning("建筑缺框 " + b.name); return; }
        float u = (b.box[0] + b.box[2]) * 0.5f;
        float v = (b.box[1] + b.box[3]) * 0.5f;
        float wPx = b.box[2] - b.box[0];
        float hPx = b.box[3] - b.box[1];

        var center = Unproject(u, v, b.y + 2f);
        float w = wPx * DepthOf(center) / Focal();
        float h = hPx * DepthOf(center) / (Focal() * Mathf.Cos(Pitch()));
        float d = w * 0.6f;

        // 反复把盒子的 8 个角投回画面，对齐目标框：宽、高、横向、竖向
        for (int iter = 0; iter < 12; iter++)
        {
            center.y = b.y + h * 0.5f;
            var bb = ProjectBox(center, w, h, d);
            bool clipped = bb.yMin <= 1f || bb.yMax >= previewCam.height - 1f;
            if (!clipped)
            {
                w *= Mathf.Clamp(wPx / Mathf.Max(1f, bb.xMax - bb.xMin), 0.5f, 2f);
                h *= Mathf.Clamp(hPx / Mathf.Max(1f, bb.yMax - bb.yMin), 0.5f, 2f);
            }
            else h *= 0.96f;
            d = w * 0.6f;

            center.y = b.y + h * 0.5f;
            var pc = Project(center);
            center.x += (u - pc.x) * DepthOf(center) / Focal();

            bb = ProjectBox(center, w, h, d);
            float dv = v - (bb.yMin + bb.yMax) * 0.5f;
            float depth = DepthOf(center);
            float k = (-Focal() * Mathf.Sin(Pitch()) + (previewCam.height * 0.5f - v) * Mathf.Cos(Pitch())) / depth;
            if (Mathf.Abs(k) > 1f) center.z += dv / k;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = b.instance;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(w, h, d);
        go.transform.rotation = Quaternion.identity;
        go.layer = 7;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (go.GetComponent<TownObstacle>() == null) go.AddComponent<TownObstacle>();
        var info = go.AddComponent<TownAssetInfo>();
        info.assetId = b.instance;
        info.displayName = b.name;
        info.category = "Buildings";
        info.sourceFile = "greybox";
        info.size = new Vector3(w, h, d);
        Debug.Log($"GREYBOX {b.instance} w={w:F2} h={h:F2} d={d:F2} pos=({center.x:F2},{center.z:F2})");
    }

    static float Pitch() { return previewCam.euler[0] * Mathf.Deg2Rad; }
    static float Focal() { return (previewCam.height * 0.5f) / Mathf.Tan(previewCam.fov * Mathf.Deg2Rad * 0.5f); }
    static Vector3 CamPos() { return new Vector3(previewCam.pos[0], previewCam.pos[1], previewCam.pos[2]); }
    static Vector3 CamFwd() { return new Vector3(0f, -Mathf.Sin(Pitch()), Mathf.Cos(Pitch())); }
    static Vector3 CamUp() { return new Vector3(0f, Mathf.Cos(Pitch()), Mathf.Sin(Pitch())); }
    static float DepthOf(Vector3 p) { return Vector3.Dot(p - CamPos(), CamFwd()); }

    static Vector2 Project(Vector3 p)
    {
        var q = p - CamPos();
        float dz = Vector3.Dot(q, CamFwd());
        float dy = Vector3.Dot(q, CamUp());
        return new Vector2(previewCam.width * 0.5f + Focal() * q.x / dz, previewCam.height * 0.5f - Focal() * dy / dz);
    }

    static Rect ProjectBox(Vector3 c, float w, float h, float d)
    {
        var r = new Rect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var p = c + new Vector3((i & 1) == 0 ? -w * 0.5f : w * 0.5f,
                                    (i & 2) == 0 ? -h * 0.5f : h * 0.5f,
                                    (i & 4) == 0 ? -d * 0.5f : d * 0.5f);
            var s = Project(p);
            r.xMin = Mathf.Min(r.xMin, s.x); r.xMax = Mathf.Max(r.xMax, s.x);
            r.yMin = Mathf.Min(r.yMin, s.y); r.yMax = Mathf.Max(r.yMax, s.y);
        }
        return r;
    }

    // 预览机位的反投影：像素 (u,v) 落在高度 y 的平面上
    static Vector3 Unproject(float u, float v, float y)
    {
        float pitch = previewCam.euler[0] * Mathf.Deg2Rad;
        float f = (previewCam.height * 0.5f) / Mathf.Tan(previewCam.fov * Mathf.Deg2Rad * 0.5f);
        var C = new Vector3(previewCam.pos[0], previewCam.pos[1], previewCam.pos[2]);
        var F = new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));
        var U = new Vector3(0f, Mathf.Cos(pitch), Mathf.Sin(pitch));
        float a = u - previewCam.width * 0.5f;
        float b = previewCam.height * 0.5f - v;
        var D = F * f + Vector3.right * a + U * b;
        float k = (y - C.y) / D.y;
        return new Vector3(C.x + D.x * k, y, C.z + D.z * k);
    }

    // ---------- 圆石与树（无碰撞） ----------

    static void ScatterRocksAndTrees(Layout layout, Transform parent, Material rock, Material leaves, Material trunk)
    {
        var root = new GameObject("Foliage").transform;
        root.SetParent(parent, false);
        var rng = new System.Random(20261106);
        var edge = new List<Vector3>();
        foreach (var t in layout.tiers)
        {
            if (t.x == null) continue;
            for (int i = 0; i < t.x.Length; i++)
            {
                var a = new Vector3(t.x[i], t.y, t.z[i]);
                var b = new Vector3(t.x[(i + 1) % t.x.Length], t.y, t.z[(i + 1) % t.x.Length]);
                float len = Vector3.Distance(a, b);
                int n = Mathf.Max(1, Mathf.FloorToInt(len / 6f));
                for (int k = 0; k < n; k++)
                    edge.Add(Vector3.Lerp(a, b, (k + 0.5f) / n));
            }
        }
        foreach (var p in edge)
        {
            if (rng.NextDouble() < 0.5)
            {
                var rockGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rockGO.name = "Rock";
                UnityEngine.Object.DestroyImmediate(rockGO.GetComponent<Collider>());
                rockGO.transform.SetParent(root, false);
                float s = 1.6f + (float)rng.NextDouble() * 1.6f;
                rockGO.transform.position = p + new Vector3(0, -0.3f, 0);
                rockGO.transform.localScale = new Vector3(s, s * 0.8f, s);
                rockGO.GetComponent<Renderer>().sharedMaterial = rock;
            }
            else if (rng.NextDouble() < 0.55)
            {
                var tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tree.name = "Tree";
                UnityEngine.Object.DestroyImmediate(tree.GetComponent<Collider>());
                tree.transform.SetParent(root, false);
                tree.transform.position = p + new Vector3(0, 1.0f, 0);
                tree.transform.localScale = new Vector3(0.35f, 1.0f, 0.35f);
                tree.GetComponent<Renderer>().sharedMaterial = trunk;
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Crown";
                UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
                crown.transform.SetParent(tree.transform, false);
                crown.transform.localPosition = new Vector3(0, 1.2f, 0);
                crown.transform.localScale = new Vector3(2.6f, 2.2f, 2.6f);
                crown.GetComponent<Renderer>().sharedMaterial = leaves;
            }
        }
    }

    // ---------- 机位与灯光 ----------

    static void BuildCamera(Layout layout, Transform parent)
    {
        Cam cam = null;
        if (layout.cameras != null)
            foreach (var c in layout.cameras) if (c.name == "preview") { cam = c; break; }
        var go = new GameObject("PreviewCam");
        go.transform.SetParent(parent, false);
        var cch = go.AddComponent<Camera>();
        cch.tag = "MainCamera";
        if (cam != null)
        {
            go.transform.position = new Vector3(cam.pos[0], cam.pos[1], cam.pos[2]);
            go.transform.rotation = Quaternion.Euler(cam.euler[0], cam.euler[1], cam.euler[2]);
            cch.fieldOfView = cam.fov;
            cch.nearClipPlane = 0.3f;
            cch.farClipPlane = 800f;
        }
    }

    static void BuildLight(Transform parent)
    {
        var go = new GameObject("Sun");
        go.transform.SetParent(parent, false);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color32(0xff, 0xe8, 0xb8, 255);
        l.intensity = 1.1f;
        go.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }

    // ---------- 工具 ----------

    static Material Mat(string name, Color32 c)
    {
        if (!AssetDatabase.IsValidFolder(MatRoot)) AssetDatabase.CreateFolder("Assets/TideTown/Art", "Materials");
        string path = MatRoot + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var dir = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        }
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            // 复用同一个资产，重建不会换 GUID，场景哈希才稳定
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
