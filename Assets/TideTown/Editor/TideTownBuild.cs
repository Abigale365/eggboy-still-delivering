using UnityEditor;
using UnityEngine;

public class TideTownBuild
{
    public static void SliceAndWindows()
    {
        TideTownSceneBuilder.BuildSlice();
        BuildWindows();
    }

    public static void BuildWindows()
    {
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/TideTown/Scenes/TideTown_Stage1.unity" },
            locationPathName = "Build/TideTown_Stage1/TideTown_Stage1.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"BUILDWINDOWS result={report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize}");
    }

    public static void BuildWebGL()
    {
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/TideTown/Scenes/TideTown_Stage1.unity" },
            locationPathName = "Build/TideTown_WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"BUILDWEBGL result={report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize}");
    }
}
