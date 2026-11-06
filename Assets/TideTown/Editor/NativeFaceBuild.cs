using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class NativeFaceBuild
{
    const string Scene="Assets/TideTown/Scenes/TideTown_Expanded_Stage1.unity";
    public static void BuildPreviews()
    {
        var windows=BuildPipeline.BuildPlayer(new[]{Scene},"Build/Expanded/TideTown.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
        Debug.Log("NATIVE_WINDOWS "+windows.summary.result+" errors="+windows.summary.totalErrors);
        if(windows.summary.result!=BuildResult.Succeeded)throw new System.Exception("Windows build failed");
        PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.template="PROJECT:EGGBOY";
        var web=BuildPipeline.BuildPlayer(new[]{Scene},"Build/ExpandedWebGL",BuildTarget.WebGL,BuildOptions.None);
        Debug.Log("NATIVE_WEBGL "+web.summary.result+" errors="+web.summary.totalErrors);
        if(web.summary.result!=BuildResult.Succeeded)throw new System.Exception("WebGL build failed");
    }
}
