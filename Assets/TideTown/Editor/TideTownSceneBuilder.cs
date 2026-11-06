using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 生成"邮局街区"竖切场景：山口邮筒 → 邮局 → 面包店 → 学校，用来验证导入、碰撞、镜头和性能。
public class TideTownSceneBuilder
{
    const string ScenePath = "Assets/TideTown/Scenes/TideTown_Stage1.unity";
    const string ArtRoot = "Assets/TideTown/Art";

    [MenuItem("潮汐镇/场景/生成邮局街区竖切")]
    public static void BuildSlice()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 相机
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 360f;
        camGO.transform.position = new Vector3(-72f, 1.4f, 4f);

        // 游戏入口（PrototypeGame 检测到 TownWorld 时不再生成原型世界）
        var game = new GameObject("Game");
        game.AddComponent<PrototypeGame>();

        // 世界
        var twGO = new GameObject("TownWorld");
        var tw = twGO.AddComponent<TownWorld>();
        // 全镇 160×110：陆地 x -80..80、z -55..19，海 z 19..55
        tw.walkRects = new List<Rect>
        {
            new Rect(-78f, -52f, 156f, 70f),     // 陆地 x -78..78, z -52..18
            new Rect(41f, 16f, 6f, 12.5f),       // 码头 x 41..47, z 16..28.5
            new Rect(39.5f, 28.5f, 9f, 9f),      // 灯塔基座 x 39.5..48.5, z 28.5..37.5
        };

        var spawn = new GameObject("SpawnPoint").transform;
        spawn.SetParent(twGO.transform, false);
        spawn.position = new Vector3(-75f, 0.06f, 4f);
        tw.spawnMarker = spawn;

        var mail = new GameObject("MailboxPoint").transform;
        mail.SetParent(twGO.transform, false);
        mail.position = new Vector3(-75f, 0.06f, -2.5f);
        tw.mailboxMarker = mail;

        // 地面与道路（Ground 层）
        Box("Ground", new Vector3(0f, -0.1f, -18f), new Vector3(160f, 0.2f, 74f),
            Mat("Grass", new Color32(0x7f, 0xa0, 0x5e, 255), 0.05f), twGO.transform);
        Box("Sea", new Vector3(0f, -0.4f, 37f), new Vector3(168f, 0.3f, 36f),
            Mat("Sea", new Color32(0x3f, 0x7f, 0x96, 255), 0.75f), twGO.transform, 0);

        var roadMat = Mat("Road", new Color32(0xc3, 0xad, 0x8b, 255), 0.06f);
        var plazaMat = Mat("Plaza", new Color32(0xd9, 0xc4, 0x9d, 255), 0.05f);
        Box("Road", new Vector3(0f, 0.03f, 0f), new Vector3(150f, 0.06f, 6f), roadMat, twGO.transform);
        Box("PlazaPostOffice", new Vector3(-38f, 0.03f, -6.2f), new Vector3(14f, 0.06f, 8f), plazaMat, twGO.transform);
        Box("PlazaBakery", new Vector3(-10f, 0.03f, -6f), new Vector3(12f, 0.06f, 7f), plazaMat, twGO.transform);
        Box("PlazaSchool", new Vector3(18f, 0.03f, -8f), new Vector3(20f, 0.06f, 10f), plazaMat, twGO.transform);
        foreach (float x in new[] { -50f, -20f, 10f, 40f })
            Box("Alley" + x, new Vector3(x, 0.03f, -23.5f), new Vector3(5f, 0.06f, 43f), roadMat, twGO.transform);

        // 沙滩台阶：14 cm，正好等于 EggboyPlayer.Step，用来验迈步
        Box("BeachStep", new Vector3(44f, 0.07f, 17.3f), new Vector3(6f, 0.14f, 1.4f),
            Mat("Sand", new Color32(0xd8, 0xc4, 0x96, 255), 0.05f), twGO.transform);

        // 木码头：与台阶同高 0.14，到基座南沿 z=28.5 为止，不与基座板重叠
        var pier = Box("Pier", new Vector3(44f, 0.07f, 23.25f), new Vector3(6f, 0.14f, 10.5f),
            Mat("PierWood", new Color32(0x9a, 0x7b, 0x52, 255), 0.08f), twGO.transform);
        for (int i = 0; i < 5; i++)
        {
            float z = 19f + i * 2f;
            Box("PilingL" + i, new Vector3(41.3f, -0.46f, z), new Vector3(0.22f, 1.2f, 0.22f),
                Mat("PierWood", new Color32(0x9a, 0x7b, 0x52, 255), 0.08f), pier.transform, 0);
            Box("PilingR" + i, new Vector3(46.7f, -0.46f, z), new Vector3(0.22f, 1.2f, 0.22f),
                Mat("PierWood", new Color32(0x9a, 0x7b, 0x52, 255), 0.08f), pier.transform, 0);
        }

        // 三栋公共建筑
        Place("Buildings/B1_邮局_分享包版.prefab", new Vector3(-38f, 0.06f, -9.5f), 0f, twGO.transform);
        Place("Buildings/B2_面包店_分享包版.prefab", new Vector3(-10f, 0.06f, -9.5f), 0f, twGO.transform);
        Place("Buildings/B3_小学校舍_精修版.prefab", new Vector3(18f, 0.06f, -14f), 0f, twGO.transform);

        // 住宅区两排，共 28 栋，交替使用瓦房 / 木板房 / 船棚
        string[] houses =
        {
            "Buildings/B6_瓦房_分享包版.prefab",
            "Buildings/B7_木板房_分享包版.prefab",
            "Buildings/B5_船棚_分享包版.prefab",
        };
        for (int row = 0; row < 2; row++)
        {
            float z = row == 0 ? -25f : -38f;
            for (int i = 0; i < 14; i++)
            {
                float x = -65f + i * 8f;
                Place(houses[(i + row) % houses.Length], new Vector3(x, 0.06f, z), 0f, twGO.transform);
            }
        }
        // 海边两座船棚
        Place(houses[2], new Vector3(-62f, 0.06f, 12f), 180f, twGO.transform);
        Place(houses[2], new Vector3(58f, 0.06f, 12f), 180f, twGO.transform);

        // 山口与主街
        Place("Street/S1_山口邮筒_分享包版.prefab", new Vector3(-75f, 0.06f, -2.5f), 180f, twGO.transform);
        Place("Street/S5_路牌_分享包版.prefab", new Vector3(-70f, 0.06f, 6.6f), 0f, twGO.transform);
        Place("Street/S3_邮局门口信箱_分享包版.prefab", new Vector3(-38f, 0.06f, -3.3f), 0f, twGO.transform);
        for (int i = 0; i < 10; i++)
        {
            float x = -68f + i * 12f;
            Place("Street/S4_路灯_分享包版.prefab", new Vector3(x, 0.06f, -4.8f), 0f, twGO.transform);
        }
        for (int i = 0; i < 6; i++)
            Place("Street/S6_长椅_分享包版.prefab", new Vector3(-60f + i * 20f, 0.06f, 6.6f), 180f, twGO.transform);
        Place("Street/S2_木箱_分享包版.prefab", new Vector3(-48f, 0.06f, 6f), 25f, twGO.transform);
        Place("Street/S2_木箱_分享包版.prefab", new Vector3(8f, 0.06f, 6.2f), 10f, twGO.transform);
        Place("Street/S2_木箱_分享包版.prefab", new Vector3(32f, 0.06f, 6f), -15f, twGO.transform);
        Place("Street/S7_木栅栏_分享包版.prefab", new Vector3(-70f, 0.06f, 10f), 0f, twGO.transform);
        Place("Street/S7_木栅栏_分享包版.prefab", new Vector3(-67.6f, 0.06f, 10f), 0f, twGO.transform);
        for (int i = 0; i < 4; i++)
            Place("Street/S11_晾衣架_分享包版.prefab", new Vector3(-55f + i * 30f, 0.06f, -31.5f), 90f, twGO.transform);

        // 面包店门口道具
        Place("Props_Bakery/A1_烤炉_暖色材质版.prefab", new Vector3(-7f, 0.06f, -4.4f), 0f, twGO.transform);
        Place("Props_Bakery/A8_面粉袋_后续平滑版.prefab", new Vector3(-13f, 0.06f, -4.8f), 0f, twGO.transform);
        Place("Props_Bakery/A4_面包篮_v2.prefab", new Vector3(-9f, 0.06f, -4.4f), 0f, twGO.transform);
        Place("Props_Bakery/A6_台秤_v2.prefab", new Vector3(-11f, 0.06f, -4.6f), 0f, twGO.transform);

        // 学校操场
        Place("Props_School/C5_滑梯_分享包版.prefab", new Vector3(12f, 0.06f, -5.0f), 0f, twGO.transform);
        Place("Props_School/C6_秋千_分享包版.prefab", new Vector3(20f, 0.06f, -5.0f), 0f, twGO.transform);
        Place("Props_School/C1_狗碗_分享包版.prefab", new Vector3(8f, 0.06f, -4.2f), 0f, twGO.transform);
        Place("Props_School/C2_红色小球_分享包版.prefab", new Vector3(16f, 0.06f, -4.4f), 0f, twGO.transform);
        Place("Props_School/C4_狗窝木箱_后续修改版.prefab", new Vector3(24f, 0.06f, -4.6f), 0f, twGO.transform);
        Place("Props_School/C7_黑板_后续修改2.prefab", new Vector3(26f, 0.06f, -6f), 0f, twGO.transform);
        Place("Props_School/C8_课桌_后续修改2.prefab", new Vector3(22f, 0.06f, -6.2f), 0f, twGO.transform);

        // 码头与灯塔：码头尽头一块石头基座，灯塔立在上面
        Box("LighthouseBase", new Vector3(44f, 0.07f, 33f), new Vector3(9f, 0.14f, 9f),
            Mat("LightStone", new Color32(0xb8, 0xb2, 0xa4, 255), 0.05f), twGO.transform);
        const float LightScale = 1.9f;   // 原模型只有 4.85 米，按 1.9 倍放大成 ~9.2 米
        var lighthouse = Place("Buildings/B4_灯塔_分享包版.prefab", new Vector3(44f, 0.14f, 33.5f), 0f, twGO.transform);
        if (lighthouse != null) lighthouse.transform.localScale = Vector3.one * LightScale;
        Place("Props_Lighthouse/D1_油壶_v2.prefab", new Vector3(46.5f, 0.14f, 29f), 0f, twGO.transform);
        Place("Props_Lighthouse/D2_航海日志_v2.prefab", new Vector3(41.2f, 0.14f, 29f), 0f, twGO.transform);
        // 望远镜、帽子靠边放，给码头→基座的中线留出净宽，避免挡住上岛的入口
        Place("Props_Lighthouse/D3_望远镜_v2.prefab", new Vector3(45.8f, 0.14f, 29.3f), -90f, twGO.transform);
        Place("Props_Lighthouse/D5_守塔人帽子_v9.prefab", new Vector3(42.3f, 0.14f, 29.1f), 0f, twGO.transform);
        Place("Props_Lighthouse/D4O_船桨_v2.prefab", new Vector3(42.4f, 0.14f, 20f), 0f, twGO.transform);
        Place("Props_Lighthouse/D6_救生圈_v2.prefab", new Vector3(46f, 0.14f, 20.5f), 0f, twGO.transform);
        Place("Street/S8_木码头_分享包版.prefab", new Vector3(48.5f, -0.15f, 22f), 30f, twGO.transform);
        Place("Street/S9_小船_分享包版.prefab", new Vector3(52f, -0.25f, 24f), 15f, twGO.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!AssetDatabase.IsValidFolder("Assets/TideTown/Scenes"))
            AssetDatabase.CreateFolder("Assets/TideTown", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == ScenePath))
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();

        int renderers = Object.FindObjectsOfType<MeshRenderer>().Length;
        int tris = 0;
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>())
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            for (int s = 0; s < m.subMeshCount; s++) tris += (int)(m.GetIndexCount(s) / 3);
        }
        Debug.Log($"SLICE BUILT scene={ScenePath} renderers={renderers} tris={tris}");
    }

    // 地面/台阶用一个薄盒子，默认放 Ground 层，GroundAt 的向下射线就能打到它的顶面。
    static GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent, int layer = 6)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.layer = layer;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static Material Mat(string name, Color color, float smoothness)
    {
        const string dir = "Assets/TideTown/Art/Materials";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/TideTown/Art", "Materials");
        string path = dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Place(string rel, Vector3 pos, float rotY, Transform parent)
    {
        string path = ArtRoot + "/" + rel;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("PREFAB MISSING " + path);
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        go.layer = 7;
        return go;
    }
}
