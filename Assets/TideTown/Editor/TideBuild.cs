// WP-01 构建入口：只打包，不生成场景。
// 用法：Unity.exe -batchmode -nographics -quit -projectPath <工程> -executeMethod TideBuild.All
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TideBuild
{
    const string MainScene = "Assets/TideTown/Scenes/TideTown_Main.unity";
    const string FallbackScene = "Assets/TideTown/Scenes/TideTown_Expanded_Stage1.unity";
    const string WindowsOut = "Build/Main/TideTown.exe";
    const string WebGLOut = "Build/MainWebGL";

    // WP-05 生成 TideTown_Main 后自动切换；在此之前用现有扩展场景。
    static string Scene => File.Exists(MainScene) ? MainScene : FallbackScene;

    public static void Windows() => Build(WindowsOut, BuildTarget.StandaloneWindows64, "WINDOWS");

    public static void WebGL()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:EGGBOY";
        Build(WebGLOut, BuildTarget.WebGL, "WEBGL");
    }

    public static void All()
    {
        Windows();
        WebGL();
    }

    static void Build(string output, BuildTarget target, string tag)
    {
        var report = BuildPipeline.BuildPlayer(new[] { Scene }, output, target, BuildOptions.None);
        Debug.Log($"TIDEBUILD {tag} {report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize} scene={Scene}");
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception(tag + " build failed");
    }
}
