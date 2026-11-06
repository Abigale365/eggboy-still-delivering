// 编辑器工具：FBX 用旧版 Animation 导入（脚本按名字播放最简单）；一键生成原型场景。
// 命令行：Unity.exe -batchmode -quit -projectPath 项目 -executeMethod EggboySetup.BuildScene
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class EggboyImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.EndsWith("eggboy.fbx")) return;
        var m = (ModelImporter)assetImporter;
        m.animationType = ModelImporterAnimationType.Legacy;
        m.generateAnimations = ModelImporterGenerateAnimations.GenerateAnimations;
        m.importAnimation = true;
        m.animationCompression = ModelImporterAnimationCompression.Off; // 刚性绑骨，保留每帧数据，免得手脚飘
        m.materialLocation = ModelImporterMaterialLocation.InPrefab;
        m.isReadable = true; // 表情和防穿模要读顶点 / 骨骼权重
        m.importBlendShapes = false;
        m.importVisibility = false;
        m.addCollider = false;
        m.importCameras = false;
        m.importLights = false;
    }
}

public static class EggboySetup
{
    const string ScenePath = "Assets/Scenes/Prototype.unity";

    [MenuItem("EGGBOY/检查模型导入")]
    public static void InspectModel()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/eggboy.fbx");
        if (prefab == null) { Debug.LogError("找不到 Assets/Resources/Models/eggboy.fbx"); return; }
        foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Debug.Log($"SkinnedMeshRenderer {smr.name}: verts {smr.sharedMesh.vertexCount}, submeshes {smr.sharedMesh.subMeshCount}, bounds {smr.sharedMesh.bounds}");
            Debug.Log("  bones: " + string.Join(", ", System.Array.ConvertAll(smr.bones, b => b != null ? b.name : "null")));
            var mats = smr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                Debug.Log($"  material[{i}]: {(mats[i] != null ? mats[i].name + " / " + mats[i].shader.name : "null")}");
        }
        foreach (var a in prefab.GetComponentsInChildren<Animation>(true))
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (AnimationState s in a) names.Add(s.name);
            Debug.Log("Animation clips: " + string.Join(", ", names));
        }

        // 实例化一份，看 FBX 的轴校正和实际世界包围盒
        var inst = Object.Instantiate(prefab);
        var tr = inst.transform;
        for (int depth = 0; depth < 4 && tr != null; depth++)
        {
            Debug.Log($"Transform L{depth}: {tr.name} pos {tr.localPosition} rot {tr.localRotation.eulerAngles} scale {tr.localScale}");
            if (tr.childCount == 0) break;
            tr = tr.GetChild(0);
        }
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Debug.Log($"World bounds {smr.name}: center {smr.bounds.center}, extents {smr.bounds.extents}");
        foreach (var boneName in new[] { "root", "body", "foot.R", "antenna.2" })
        {
            var t = inst.GetComponentsInChildren<Transform>(true);
            foreach (var b in t) if (b.name == boneName) Debug.Log($"Bone world {boneName}: {b.position}");
        }
        foreach (var b in inst.GetComponentsInChildren<Transform>(true))
        {
            if (b.name == "bag" || b.name.StartsWith("arm.") || b.name.StartsWith("hand.") || b.name.StartsWith("leg.") || b.name.StartsWith("foot."))
                Debug.Log($"Bone side {b.name}: {b.position}");
        }
        Object.DestroyImmediate(inst);
    }

    [MenuItem("EGGBOY/导出表情纹理")]
    public static void DumpFaces()
    {
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "Faces");
        System.IO.Directory.CreateDirectory(dir);
        foreach (ExprId id in EggboyExpressions.All)
        {
            var p = new FacePainter();
            p.Set(id);
            Debug.Log($"face {id}: symbol {p.Symbol}");
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, id + ".png"), p.Texture.EncodeToPNG());
            if (p.Symbol != FaceSymbol.None)
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, id + "-sym.png"), p.SymbolTexture.EncodeToPNG());
        }
        Debug.Log("表情纹理已导出：" + dir);
    }

    [MenuItem("EGGBOY/生成原型场景")]
    public static void BuildScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera") { tag = "MainCamera" };
        cam.AddComponent<Camera>().nearClipPlane = 0.1f;
        cam.AddComponent<AudioListener>();
        new GameObject("Game").AddComponent<PrototypeGame>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("EGGBOY 原型场景已生成：" + ScenePath);
    }
}
