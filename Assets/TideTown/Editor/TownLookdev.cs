// 2026-11-07 给 EGGBOY_Renderer 加屏幕空间环境光遮蔽（SSAO）：墙角、窗台下、拱门边、物体脚下变暗，
// Lux3D 模型的体积感主要靠它。批处理调用：tools\build.ps1 -Method TownLookdev.AddSSAO
// 已经有 SSAO 就只更新参数，可以重复跑。
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class TownLookdev
{
    const string RendererPath = "Assets/Settings/EGGBOY_Renderer.asset";

    public static void AddSSAO()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null) throw new System.Exception("找不到 " + RendererPath);

        // URP 14 里 ScreenSpaceAmbientOcclusion 是 internal，按类型名取
        var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
        if (type == null) throw new System.Exception("URP 里找不到 ScreenSpaceAmbientOcclusion");
        ScriptableRendererFeature ssao = null;
        foreach (var f in data.rendererFeatures) if (f != null && f.GetType() == type) ssao = f;
        if (ssao == null)
        {
            ssao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            ssao.name = "TownSSAO";
            AssetDatabase.AddObjectToAsset(ssao, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var fs = new SerializedObject(ssao);
        Set(fs, "m_Settings.Source", 1);                 // DepthNormals：法线更准，接缝少
        Set(fs, "m_Settings.NormalSamples", 1);          // Medium
        Set(fs, "m_Settings.Downsample", true);          // 半分辨率，网页和核显也扛得住
        Set(fs, "m_Settings.AfterOpaque", false);
        Set(fs, "m_Settings.Intensity", 1.6f);
        Set(fs, "m_Settings.DirectLightingStrength", 0.3f);
        Set(fs, "m_Settings.Radius", 0.4f);
        Set(fs, "m_Settings.Falloff", 60f);
        fs.ApplyModifiedPropertiesWithoutUndo();
        ssao.SetActive(true);

        EditorUtility.SetDirty(ssao);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("TOWNLOOKDEV SSAO ok features=" + data.rendererFeatures.Count);
    }

    static void Set(SerializedObject so, string path, object v)
    {
        var p = so.FindProperty(path);
        if (p == null) { Debug.LogWarning("TOWNLOOKDEV 没有属性 " + path); return; }
        switch (v)
        {
            case bool b: p.boolValue = b; break;
            case int i: if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i; break;
            case float f: p.floatValue = f; break;
        }
    }
}
