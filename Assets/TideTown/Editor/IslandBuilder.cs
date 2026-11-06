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

    [Serializable] public class Footprint { public string instance; public float x, z, w, d; }
    [Serializable] class FootprintList { public List<Footprint> buildings; }
    [Serializable] class RoutePoint { public string id; public bool stop; public float x, z; }
    [Serializable] class RouteFile { public RoutePoint[] route; }

    const string FootprintPath = "Assets/TideTown/Data/Resources/World/building_footprints.json";
    const string RoutePath = "Assets/TideTown/Data/Resources/World/route.json";

    static Cam previewCam;
    static readonly List<Footprint> footprints = new List<Footprint>();

    [MenuItem("潮汐镇/场景/生成主场景（WP-05）")]
    public static void BuildMain()
    {
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        if (layout == null || layout.tiers == null) throw new Exception("world_layout.json 读取失败");
        var route = JsonUtility.FromJson<RouteFile>(File.ReadAllText(RoutePath));
        if (route == null || route.route == null) throw new Exception("route.json 读取失败");
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
        var crate = Mat("IslandCrate", new Color32(0x8a, 0x66, 0x42, 255));
        var greybox = Mat("IslandBuildingGrey", new Color32(0xb4, 0xb4, 0xb4, 255));
        var scanMat = Mat("IslandScan", new Color32(0xd8, 0xd2, 0xc4, 255));

        if (layout.coast != null)
            BuildTier(new Area { id = "coast", y = 0.15f, walk = false, x = layout.coast.x, z = layout.coast.z }, generated.transform, grass, cliff);
        for (int i = 0; i < layout.tiers.Length; i++) BuildTier(layout.tiers[i], generated.transform, grass, cliff);
        if (layout.roads != null)
            foreach (var r in layout.roads) BuildRoad(r, generated.transform, road);

        // WP-08：海面、安全点、浮箱。顺序有讲究：安全点要在浮箱之前取地面，否则射线会打到箱顶
        AlwaysIncludeWaterShader();
        BuildSea(generated.transform, WaterMat(), layout);
        BuildSafePoints(route, generated.transform);
        BuildCrates(generated.transform, crate);

        footprints.Clear();
        if (layout.buildings != null)
            foreach (var b in layout.buildings) BuildBuilding(b, generated.transform, greybox);
        BuildScanStation(layout, generated.transform, scanMat);
        BuildMailbox(layout, generated.transform, Mat("MailboxRed", new Color32(0x9e, 0x2b, 0x25, 255)));
        // 给 tools/preview/route_check.py 用，和场景里的碰撞是同一份数字
        File.WriteAllText(FootprintPath, JsonUtility.ToJson(new FootprintList { buildings = footprints }, true));

        if (layout.points != null)
            foreach (var p in layout.points)
            {
                var marker = new GameObject("P_" + p.id);
                marker.transform.SetParent(generated.transform, false);
                marker.transform.position = new Vector3(p.x, p.y, p.z);
            }

        BuildWorld(layout, route, generated.transform);

        ScatterRocksAndTrees(layout, generated.transform, rockMat, leaves, trunk);
        BuildCamera(layout, generated.transform);
        BuildLight(generated.transform);
        var game = new GameObject("Game");
        game.transform.SetParent(generated.transform, false);
        game.AddComponent<PrototypeGame>();
        game.AddComponent<GameBootstrap>();

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
        // 三角化丢面积就是地上有洞，角色会掉下去。宁可生成失败，也不留静默的洞。
        float triArea = 0f;
        foreach (var t in tris) triArea += Mathf.Abs(Cross(poly[t.Item2] - poly[t.Item1], poly[t.Item3] - poly[t.Item1])) * 0.5f;
        float polyArea = Mathf.Abs(SignedArea(poly));
        if (Mathf.Abs(triArea - polyArea) > polyArea * 0.001f + 0.001f)
            throw new Exception($"三角化丢面积：{name} 多边形 {polyArea:F2}，三角形合计 {triArea:F2}。检查多边形是否自交或有多余共线点。");
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
            // 坡面本身不动；两端各加 15 cm 盖片补住拐弯和接缝，盖片顺着相邻那段的坡度走，
            // 这样盖片不会高出邻段形成台阶。算法和 route_check.py 一致。
            var capA = a - d * 0.15f; capA.y = a.y - NeighbourSlope(r, i - 1) * 0.15f;
            var capB = b + d * 0.15f; capB.y = b.y + NeighbourSlope(r, i + 1) * 0.15f;
            AddRoadQuad(verts, idx, capA, a, side);
            AddRoadQuad(verts, idx, a, b, side);
            AddRoadQuad(verts, idx, b, capB, side);
        }
        var mesh = new Mesh { name = "road_" + r.id };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 第 seg 段每水平走一米的高度变化；路的两头之外没有邻段，算平的
    static float NeighbourSlope(RoadDef r, int seg)
    {
        if (seg < 0 || seg + 1 >= r.x.Length) return 0f;
        float dx = r.x[seg + 1] - r.x[seg], dz = r.z[seg + 1] - r.z[seg];
        float len = Mathf.Sqrt(dx * dx + dz * dz);
        return len < 1e-3f ? 0f : (r.y[seg + 1] - r.y[seg]) / len;
    }

    static void AddRoadQuad(List<Vector3> verts, List<int> idx, Vector3 a, Vector3 b, Vector3 side)
    {
        int o = verts.Count;
        verts.Add(a - side); verts.Add(a + side); verts.Add(b + side); verts.Add(b - side);
        idx.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
    }

    // ---------- 海面 ----------

    // WP-08 海面：顶点色 R = 离岸距离（0..8 米归一化），给泡沫带用；高度由 WaterLevel 移动这个平面。
    static WaterLevel BuildSea(Transform parent, Material mat, Layout layout)
    {
        var go = new GameObject("Sea");
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var mesh = SeaMesh(layout.coast);
        mf.sharedMesh = SaveMesh(mesh, "Assets/TideTown/Art/Meshes/Main_sea.asset");

        var waterGO = new GameObject("WaterSystem");
        waterGO.transform.SetParent(parent, false);
        var water = waterGO.AddComponent<WaterLevel>();
        water.sea = go.transform;
        Debug.Log($"SEA verts={mesh.vertexCount} tris={mesh.triangles.Length / 3}");
        return water;
    }

    static Mesh SeaMesh(Coast coast)
    {
        var verts = new List<Vector3>();
        var cols = new List<Color32>();
        var idx = new List<int>();
        AddSeaGrid(verts, cols, idx, -48f, 48f, 1.5f, false, coast);    // 岛附近细
        AddSeaGrid(verts, cols, idx, -400f, 400f, 16f, true, coast);   // 外海粗，盖到远处
        var mesh = new Mesh { name = "sea" };
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddSeaGrid(List<Vector3> verts, List<Color32> cols, List<int> idx,
        float min, float max, float step, bool skipInner, Coast coast)
    {
        int n = Mathf.RoundToInt((max - min) / step);
        int start = verts.Count;
        for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = min + i * step, z = min + j * step;
                verts.Add(new Vector3(x, 0f, z));
                float d = CoastDistance(coast, x, z);
                byte r = (byte)Mathf.Clamp(d / 8f * 255f, 0f, 255f);
                byte g = (byte)Mathf.Clamp(Mathf.PerlinNoise(x * 0.11f + 3.7f, z * 0.11f + 9.1f) * 255f, 0f, 255f);
                cols.Add(new Color32(r, g, 0, 255));
            }
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (skipInner)
                {
                    float cx = min + (i + 0.5f) * step, cz = min + (j + 0.5f) * step;
                    if (Mathf.Abs(cx) < 48f && Mathf.Abs(cz) < 48f) continue;
                }
                int a = start + j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                idx.AddRange(new[] { a, c, b, b, c, d });
            }
    }

    static float CoastDistance(Coast c, float x, float z)
    {
        if (c == null || c.x == null || c.x.Length < 2) return 50f;
        float best = float.MaxValue;
        int n = c.x.Length;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            float ax = c.x[i], az = c.z[i], bx = c.x[j], bz = c.z[j];
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(((x - ax) * dx + (z - az) * dz) / len2);
            float px = ax + dx * t, pz = az + dz * t;
            best = Mathf.Min(best, (x - px) * (x - px) + (z - pz) * (z - pz));
        }
        return Mathf.Sqrt(best);
    }

    static Material WaterMat()
    {
        string path = MatRoot + "/IslandWater.mat";
        var shader = Shader.Find("TideTown/Water");
        if (shader == null) throw new Exception("找不到 TideTown/Water 着色器（Assets/TideTown/Art/Shaders/Water.shader）");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader)
        {
            m.shader = shader;
        }
        m.SetColor("_ShallowColor", new Color(0.33f, 0.72f, 0.72f, 1f));
        m.SetColor("_DeepColor", new Color(0.10f, 0.33f, 0.47f, 1f));
        m.SetColor("_FoamColor", new Color(0.94f, 0.98f, 0.96f, 1f));
        m.SetFloat("_ShoreRange", 8f);
        m.SetFloat("_FoamWidth", 1.4f);
        m.SetFloat("_Alpha", 0.86f);
        m.SetFloat("_WaveSpeed", 1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 自定义着色器写进 Always Included，WebGL 构建不会因为没有变体被裁掉
    static void AlwaysIncludeWaterShader()
    {
        var shader = Shader.Find("TideTown/Water");
        if (shader == null) throw new Exception("找不到 TideTown/Water 着色器");
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning("WATER_SHADER 读不到 GraphicsSettings，只靠场景材质引用进包");
            return;
        }
        var so = new SerializedObject(assets[0]);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        if (arr == null) { Debug.LogWarning("WATER_SHADER 找不到 m_AlwaysIncludedShaders"); return; }
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
        arr.InsertArrayElementAtIndex(arr.arraySize);
        arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("WATER_SHADER always-included " + shader.name);
    }

    // ---------- WP-08 安全点与浮箱 ----------

    static float GroundY(float x, float z)
    {
        Physics.SyncTransforms();
        if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out var hit, 400f,
                LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    static void BuildSafePoints(RouteFile route, Transform parent)
    {
        var go = new GameObject("SafePoints");
        go.transform.SetParent(parent, false);
        var sp = go.AddComponent<SafePoints>();
        sp.points.Clear();
        foreach (var p in route.route)
        {
            if (!p.stop) continue;
            float y = GroundY(p.x, p.z);
            if (y <= 0.2f) throw new Exception($"安全点没落到地面：{p.id} ({p.x:F2},{p.z:F2})");
            sp.points.Add(new Vector3(p.x, y, p.z));
        }
        Debug.Log("SAFEPOINTS n=" + sp.points.Count);
    }

    static void BuildCrates(Transform parent, Material mat)
    {
        // 低洼段（工程书 5.3：22 和船棚之间约 3 米）。木箱铺在 L2 会淹掉的那段路上，
        // 两头压在干路面，箱顶 = 水位 + 0.18，L2 时 1.33，两边都只差一步以内。
        float[] xs = { 14.9f, 15.45f, 16.0f };
        const float z = -6.55f;
        var size = new Vector3(0.6f, 0.55f, 0.6f);
        var root = new GameObject("Crates").transform;
        root.SetParent(parent, false);
        float bed = float.MaxValue;
        foreach (var x in xs)
        {
            float g = GroundY(x, z);
            if (g <= 0.2f) throw new Exception($"浮箱没落到地面：{x:F2},{z:F2}");
            bed = Mathf.Min(bed, g);
        }
        for (int i = 0; i < xs.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Crate_" + i;
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(xs[i], bed + size.y * 0.5f, z);
            go.transform.localScale = size;
            go.layer = 6; // Ground：GroundAt 的射线要能打到箱顶
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var fc = go.AddComponent<FloatingCrate>();
            fc.bedY = bed;
            fc.phase = i * 1.3f;
            fc.size = size;
            Debug.Log($"CRATE {i} x={xs[i]:F3} bed={bed:F2}");
        }
    }

    static void BuildScanStation(Layout layout, Transform parent, Material mat)
    {
        // 工程书 WP-09：扫描站放邮局门口，显示与否由 Features.scan 决定。
        Bld post = null;
        if (layout.buildings != null)
            foreach (var b in layout.buildings)
                if (b.instance != null && b.instance.Contains("post_office")) { post = b; break; }
        if (post == null) throw new Exception("没有 post_office，扫描站没地方放");
        float x = post.x, z = post.z - 1.0f; // 邮局正面朝 -z，站位 07 在门前
        float y = GroundY(x, z);
        if (y <= 0.2f) throw new Exception($"扫描站没落到地面：{x:F2},{z:F2}");
        var go = new GameObject("ScanStation");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, z);
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        body.transform.localScale = new Vector3(0.7f, 1.1f, 0.5f);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<BoxCollider>()); // 预留物，不挡路
        body.GetComponent<Renderer>().sharedMaterial = mat;
        go.AddComponent<ScanStation>();
        Debug.Log($"SCANSTATION x={x:F2} y={y:F2} z={z:F2}");
    }

    // WP-10：山口 P_03 放真的红色邮筒（素材缺失时用简单圆柱兜底），并挂一团暖光做教学提示。
    static void BuildMailbox(Layout layout, Transform parent, Material fallback)
    {
        var pos = new Vector3(-25.7f, 3f, 1.1f);
        if (layout.points != null)
            foreach (var p in layout.points) if (p.id == "03") { pos = new Vector3(p.x, p.y, p.z); break; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Street/S1_山口邮筒_分享包版.prefab");
        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "S1_Mailbox";
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "S1_Mailbox_fallback";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos + new Vector3(0, 0.7f, 0);
            go.transform.localScale = new Vector3(0.62f, 0.7f, 0.62f);
            go.GetComponent<Renderer>().sharedMaterial = fallback;
        }
        go.layer = 7;

        // 等比缩到 1.6 米高（5.3 比例表 S1 的目标高度）
        float h = 1.6f;
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            if (b.size.y > 0.01f) h = b.size.y;
        }
        if (h > 0.01f) go.transform.localScale *= 1.6f / h;

        var glow = go.AddComponent<MailboxGlow>();
        glow.Height = 1.6f;
        Debug.Log($"MAILBOX x={pos.x:F2} y={pos.y:F2} z={pos.z:F2} prefab={(prefab != null)} native_h={h:F2}");
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
        // 灰盒视觉是整框（含屋檐）；碰撞只算墙体：宽 80%，前墙在图上的墙根线（foot）上，后墙和灰盒一样。
        // 角色的站位放在 route.json，不靠缩碰撞让路。
        const float WallWidth = 0.8f;
        float zFront = b.z, zBack = center.z + d * 0.5f;
        var bc = go.GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.size = new Vector3(WallWidth, 1f, (zBack - zFront) / d);
            bc.center = new Vector3(0f, 0f, ((zFront + zBack) * 0.5f - center.z) / d);
        }
        footprints.Add(new Footprint { instance = b.instance, x = Mathf.Round(center.x * 1000f) / 1000f, z = Mathf.Round((zFront + zBack) * 500f) / 1000f,
                                       w = Mathf.Round(w * WallWidth * 1000f) / 1000f, d = Mathf.Round((zBack - zFront) * 1000f) / 1000f });
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

    // ---------- 世界 ----------

    static void BuildWorld(Layout layout, RouteFile route, Transform parent)
    {
        var go = new GameObject("IslandWorld");
        go.transform.SetParent(parent, false);
        var world = go.AddComponent<IslandWorld>();
        world.groundMask = LayerMask.GetMask("Ground");
        world.walkPolys = new List<WalkPoly>();
        if (layout.walk != null)
            foreach (var a in layout.walk)
            {
                var poly = new List<Vector2>();
                for (int i = 0; i < a.x.Length; i++) poly.Add(new Vector2(a.x[i], a.z[i]));
                world.walkPolys.Add(new WalkPoly { id = a.id, y = a.y, points = poly.ToArray() });
            }
        // P_xx 是预览图标签，可能贴在建筑上；出生点用 route.json 的第一站（站位）
        var spawnGO = new GameObject("SpawnPoint");
        spawnGO.transform.SetParent(go.transform, false);
        spawnGO.transform.position = new Vector3(route.route[0].x, 3f, route.route[0].z);
        world.spawnMarker = spawnGO.transform;
        var mailbox = GameObject.Find("P_03");
        if (mailbox != null) world.mailboxMarker = mailbox.transform;
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
