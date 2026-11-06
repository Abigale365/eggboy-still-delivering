using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editable first-stage terrain and route blockout, built from the existing asset scene.
public static class ExpandedTownBuilder
{
    const string Scene = "Assets/TideTown/Scenes/TideTown_Expanded_Stage1.unity";
    const string Art = "Assets/TideTown/Art/";
    static float Height(float z) { return Mathf.Clamp((-z - 11f) / 44f, 0f, 1f) * 10f; }

    [MenuItem("潮汐镇/场景/生成扩展镇区")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/TideTown/Scenes/TideTown_Stage1.unity", OpenSceneMode.Single);
        var world = Object.FindObjectOfType<TownWorld>();
        if (world == null) throw new System.Exception("TownWorld missing");
        var root = world.transform;
        if (world.spawnMarker != null) world.spawnMarker.position = new Vector3(-78f, 0.06f, 1f);
        var remove = new List<GameObject>();
        foreach (Transform t in root)
        {
            if (t.name == "Ground" || t.name.StartsWith("Alley")) { remove.Add(t.gameObject); continue; }
            if (t.name == "SpawnPoint" || t.name == "MailboxPoint" || t.name == "Sea" || t.name == "Road" || t.name.StartsWith("Plaza") || t.name == "BeachStep" || t.name == "Pier" || t.name == "LighthouseBase") continue;
            var p = t.position;
            if (t.name.StartsWith("S11_")) p.z = -35.5f;
            p.y += Height(p.z); t.position = p;
        }
        foreach (var go in remove) Object.DestroyImmediate(go);
        world.walkRects = new List<Rect> { new Rect(-112f, -76f, 224f, 94f), new Rect(41f, 16f, 6f, 12.5f), new Rect(39.5f, 28.5f, 9f, 9f) };
        var grass = Material("TerrainGrass", new Color32(132, 157, 91, 255));
        var road = Material("WarmStone", new Color32(199, 174, 135, 255));
        var rock = Material("CoastalRock", new Color32(153, 137, 115, 255));
        var leaves = Material("Foliage", new Color32(95, 128, 65, 255));
        var trunk = Material("TreeBark", new Color32(107, 77, 49, 255));
        Terrain(root, grass);
        for (int i = 0; i < 4; i++) Road(root, new Vector3(-61 + i * 32, 0, -58), new Vector3(-61 + i * 32, 0, 0), 3f, road);
        Road(root, new Vector3(-70, 0, -52), new Vector3(45, 0, -52), 4f, road);
        Road(root, new Vector3(-70, 0, -32), new Vector3(45, 0, -32), 4f, road);
        Road(root, new Vector3(44, 0, 0), new Vector3(44, 0, 18), 5f, road);
        // Additional upper neighbourhood; the original rows and public buildings are retained.
        string[] houses = { "Buildings/B6_瓦房_分享包版.prefab", "Buildings/B7_木板房_分享包版.prefab", "Buildings/B5_船棚_分享包版.prefab" };
        for (int i = 0; i < 10; i++) Place(root, houses[i % houses.Length], new Vector3(-65 + i * 12, Height(-64), -64), i % 2 == 0 ? 0 : 180);
        // Blocked mountain pass; simplified geometry is editable in the scene.
        for (int i = 0; i < 18; i++)
        {
            float x = -105f + (i % 6) * 2.7f, z = -4f + (i / 6) * 2.4f;
            Primitive(root, "山口塌方_石块", PrimitiveType.Sphere, new Vector3(x, 0.8f + (i % 3) * 0.25f, z), new Vector3(3.1f, 2.0f, 2.5f), rock);
        }
        // Distant mountain backdrop and coastal vegetation.
        for (int i = 0; i < 24; i++)
        {
            float x = -112f + i * 10f;
            Primitive(root, "北侧山体_白盒", PrimitiveType.Sphere, new Vector3(x, 8f, -85f), new Vector3(16f, 22f + (i % 4) * 3, 15f), rock, false);
        }
        for (int i = 0; i < 64; i++)
        {
            float x = -102f + ((i * 37) % 200), z = -72f + ((i * 19) % 80);
            if (Mathf.Abs(z) < 9f || (x > -76 && x < 50 && z > -43 && z < -18)) continue;
            float y = Height(z);
            Primitive(root, "树干", PrimitiveType.Cylinder, new Vector3(x, y + 0.9f, z), new Vector3(.3f, .9f, .3f), trunk, false);
            Primitive(root, "树冠", PrimitiveType.Sphere, new Vector3(x, y + 2.4f, z), new Vector3(2.6f, 2.3f, 2.5f), leaves, false);
        }
        // Build settings: existing Prototype remains available for regression.
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, Scene);
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == Scene);
        list.Insert(0, new EditorBuildSettingsScene(Scene, true));
        EditorBuildSettings.scenes = list.ToArray();
        WarmTownPalette.Apply();
        AssetDatabase.SaveAssets();
        Debug.Log("EXPANDED_SCENE_SAVED " + Scene);
    }

    static Material Material(string name, Color color)
    {
        string path = Art + "Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; mat.SetFloat("_Smoothness", .08f); EditorUtility.SetDirty(mat); return mat;
    }
    static void Terrain(Transform root, Material mat)
    {
        const int nx = 112, nz = 47; var vertices = new Vector3[(nx + 1) * (nz + 1)]; var uv = new Vector2[vertices.Length];
        var tris = new int[nx * nz * 6];
        for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++) { int i = z * (nx + 1) + x; float px = -112 + x * 2, pz = -76 + z * 2; vertices[i] = new Vector3(px, Height(pz), pz); uv[i] = new Vector2(x / 8f, z / 8f); }
        int k = 0; for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++) { int i = z * (nx + 1) + x; tris[k++] = i; tris[k++] = i + nx + 1; tris[k++] = i + 1; tris[k++] = i + 1; tris[k++] = i + nx + 1; tris[k++] = i + nx + 2; }
        var mesh = new Mesh { name = "ExpandedTerrain", vertices = vertices, triangles = tris, uv = uv }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = Art + "Meshes/ExpandedTerrain.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; } else AssetDatabase.CreateAsset(mesh, path);
        var go = new GameObject("扩展地形_可行走"); go.transform.SetParent(root, false); go.layer = LayerMask.NameToLayer("Ground"); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat; go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
    static void Road(Transform root, Vector3 a, Vector3 b, float width, Material mat)
    {
        var d = (b - a).normalized; var side = new Vector3(-d.z, 0, d.x) * width * .5f; const int n = 40;
        var v = new Vector3[(n + 1) * 2]; var t = new int[n * 6];
        for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n); var l = p - side; var r = p + side; l.y = Height(l.z) + .035f; r.y = Height(r.z) + .035f; v[i * 2] = l; v[i * 2 + 1] = r; }
        for (int i = 0; i < n; i++) { int j = i * 6, q = i * 2; t[j] = q; t[j+1] = q+1; t[j+2] = q+2; t[j+3] = q+1; t[j+4] = q+3; t[j+5] = q+2; }
        var mesh = new Mesh { vertices = v, triangles = t }; mesh.RecalculateNormals(); var go = new GameObject("坡道_白盒"); go.transform.SetParent(root, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        string path = AssetDatabase.GenerateUniqueAssetPath(Art + "Meshes/Road.asset"); AssetDatabase.CreateAsset(mesh, path);
    }
    static void Place(Transform root, string rel, Vector3 pos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + rel); if (!prefab) { Debug.LogWarning("Missing " + rel); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
    }
    static void Primitive(Transform root, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, bool collide = true)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(root, false); go.transform.position = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
        if (collide) { Object.DestroyImmediate(go.GetComponent<Collider>()); go.AddComponent<BoxCollider>(); go.AddComponent<TownObstacle>(); }
        else Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    public static void BuildWindows()
    {
        Build();
        var report = BuildPipeline.BuildPlayer(new[] { Scene }, "Build/Expanded/TideTown.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("EXPANDED_BUILD " + report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Build failed");
    }
    public static void BuildWebGL()
    {
        WarmTownPalette.Apply();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:EGGBOY";
        var report = BuildPipeline.BuildPlayer(new[] { Scene }, "Build/ExpandedWebGL", BuildTarget.WebGL, BuildOptions.None);
        Debug.Log("EXPANDED_WEBGL " + report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("WebGL build failed");
    }
    public static void BuildPreviews()
    {
        BuildWindows();
        BuildWebGL();
    }
}
