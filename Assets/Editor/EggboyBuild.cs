// WebGL 打包入口。菜单和命令行都能用：
// Unity.exe -batchmode -quit -projectPath 项目 -executeMethod EggboyBuild.BuildWebGL
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class EggboyBuild
{
    const string ScenePath = "Assets/Scenes/Prototype.unity";
    const string OutputDir = "Build/WebGL";

    [MenuItem("EGGBOY/构建 Windows 预览版")]
    public static void BuildWindows()
    {
        EnsureURP();
        EnsureURPMaterials();
        EnsureURPPostProfile();
        if (!System.IO.File.Exists(ScenePath)) EggboySetup.BuildScene();
        if (!System.IO.File.Exists(ScenePath)) { Debug.LogError("场景不存在，先执行 EGGBOY/生成原型场景"); return; }
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.companyName = "EGGBOY";
        PlayerSettings.productName = "EGGBOY · Still Delivering";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Build/Windows/EGGBOY.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"Windows 构建完成：Build/Windows/EGGBOY.exe（{report.summary.totalSize / 1024f / 1024f:F1} MB）");
        else
            Debug.LogError("Windows 构建失败：" + report.summary.result);
    }

    [MenuItem("EGGBOY/构建 WebGL")]
    public static void BuildWebGL()
    {
        EnsureURP();
        EnsureURPMaterials();
        EnsureURPPostProfile();
        if (!System.IO.File.Exists(ScenePath)) EggboySetup.BuildScene();
        if (!System.IO.File.Exists(ScenePath)) { Debug.LogError("场景不存在，先执行 EGGBOY/生成原型场景"); return; }

        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.companyName = "EGGBOY";
        PlayerSettings.productName = "EGGBOY · Still Delivering";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.template = "PROJECT:EGGBOY";

        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, OutputDir, BuildTarget.WebGL, BuildOptions.None);
        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"WebGL 构建完成：{OutputDir}（{report.summary.totalSize / 1024f / 1024f:F1} MB）");
        else
            Debug.LogError("WebGL 构建失败：" + report.summary.result);
    }

    // 运行时才打开 _METALLICGLOSSMAP / _EMISSION，打包器的变体裁剪会删掉这些变体。
    // 这里落模板材质到 Resources，保证构建里保留 URP Lit 的金属与自发光变体。
    static void EnsureURPMaterials()
    {
        EnsureURPLit("Assets/Resources/EggboyURPLitMetallic.mat", true, false);
        EnsureURPLit("Assets/Resources/EggboyURPLitEmission.mat", false, true);
    }

    static void EnsureURPLit(string path, bool metallic, bool emission)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) { Debug.LogError("找不到 URP Lit，材质模板创建失败"); return; }
        var m = new Material(sh);
        m.name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (metallic)
        {
            Texture2D mg = Resources.Load<Texture2D>("Textures/img1_mg");
            if (mg != null)
            {
                m.SetTexture("_MetallicGlossMap", mg);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Metallic", 1f);
                m.SetFloat("_Smoothness", 1f);
            }
        }
        if (emission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white * 0.5f);
        }
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        Debug.Log("URP 模板材质已创建：" + path);
    }

    // URP 资产 + 渲染器 + 全局 Volume Profile，全部在构建前落盘，避免运行时资源缺失
    static void EnsureURP()
    {
        const string dir = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Settings");
        string rendererPath = dir + "/EGGBOY_Renderer.asset";
        string assetPath = dir + "/EGGBOY_URP.asset";

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "EGGBOY_Renderer";
            ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }
        // UniversalRendererData 的 postProcessData 不会由 ResourceReloader 自动填充，
        // 为空时 URP 会跳过整条 Volume 后期链。
        if (renderer.postProcessData == null)
        {
            var pp = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (pp != null)
            {
                renderer.postProcessData = pp;
                EditorUtility.SetDirty(renderer);
            }
            else Debug.LogError("找不到 URP 默认 PostProcessData");
        }

        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (asset == null)
        {
            asset = UniversalRenderPipelineAsset.Create(renderer);
            asset.name = "EGGBOY_URP";
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        asset.supportsHDR = true;
        asset.msaaSampleCount = 4;
        asset.renderScale = 1f;
        asset.shadowDistance = 42f;
        var so = new SerializedObject(asset);
        var soft = so.FindProperty("m_SoftShadowsSupported");
        if (soft != null) soft.boolValue = true;
        so.ApplyModifiedProperties();

        GraphicsSettings.defaultRenderPipeline = asset;
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = asset;
        }
        QualitySettings.SetQualityLevel(0, false);
        AssetDatabase.SaveAssets();
        Debug.Log("URP 已启用：" + assetPath);
    }

    static void EnsureURPPostProfile()
    {
        const string path = "Assets/Resources/EggboyURPProfile.asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) return;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "EggboyURPProfile";
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.7f);
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.None);
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0f);
        color.saturation.Override(0f);
        color.contrast.Override(0f);
        color.colorFilter.Override(new Color(1f, 1f, 1.04f));
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.4f);

        AssetDatabase.CreateAsset(profile, path);
        foreach (var c in new VolumeComponent[] { bloom, tone, color, vignette })
        {
            c.name = c.GetType().Name;
            c.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("URP 后期 Profile 已创建：" + path);
    }
}
