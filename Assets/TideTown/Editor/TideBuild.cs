// WP-01 构建入口：只打包，不生成场景。
// 用法：Unity.exe -batchmode -nographics -quit -projectPath <工程> -executeMethod TideBuild.All
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TideBuild
{
    const string MainScene = "Assets/TideTown/Scenes/TideTown_Main.unity";
    const string WindowsOut = "Build/Main/TideTown.exe";
    const string WebGLOut = "Build/MainWebGL";

    // 2026-11-07 旧的扩展场景已删除，只打主场景
    static string Scene => MainScene;

    public static void Windows() => Build(WindowsOut, BuildTarget.StandaloneWindows64, "WINDOWS");

    // 开发版：只有开发版里才编进 Lux3D 直连（DevDirectGateway），用来在本机真调一次 Lux3D。不发给别人。
    public static void WindowsDev() => Build("Build/Dev/TideTown.exe", BuildTarget.StandaloneWindows64, "WINDOWSDEV", BuildOptions.Development);

    // 预览版：打到 Build/Preview，正式包（Build/Main）开着游戏时也能出图调效果
    public static void WindowsPreview() => Build("Build/Preview/TideTown.exe", BuildTarget.StandaloneWindows64, "WINDOWSPREVIEW");

    // 2026-11-16：验收用的第三份包（Build/Verify）。预览包正被人开着玩的时候，审计跑这一份，不覆盖正在运行的程序
    public static void WindowsVerify() => Build("Build/Verify/TideTown.exe", BuildTarget.StandaloneWindows64, "WINDOWSVERIFY");

    public static void WebGL()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:EGGBOY";
        Build(WebGLOut, BuildTarget.WebGL, "WEBGL");
    }

    // Repack resource-only changes after a successful full WebGL build.
    public static void WebGLIncremental()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:EGGBOY";
        Build(WebGLOut, BuildTarget.WebGL, "WEBGL", BuildOptions.None, false);
    }

    public static void All()
    {
        Windows();
        WebGL();
    }

    static void Build(string output, BuildTarget target, string tag, BuildOptions options = BuildOptions.None, bool clean = true)
    {
        // Clean worktrees must restore the ignored, deterministic GLB texture cache before packing materials.
        GlbTextureCompressor.Extract();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (clean) AssetDatabase.ImportAsset(GlbTextureCompressor.MatRoot,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(Scene, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var report = BuildPipeline.BuildPlayer(new[] { Scene }, output, target, options | (clean ? BuildOptions.CleanBuildCache : BuildOptions.None));
        Debug.Log($"TIDEBUILD {tag} {report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize} scene={Scene}");
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception(tag + " build failed");
    }
}
