using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 把 GLB 内嵌贴图换成独立 PNG，按用途设压缩，再把 prefab 的材质重绑到压缩后的贴图。
public class TideTownTexturePipeline
{
    const string MapPath = "Assets/TideTown/Data/texture_map.json";
    const string ArtRoot = "Assets/TideTown/Art";

    [Serializable]
    public class Root { public List<Model> models; }
    [Serializable]
    public class Model { public string id; public string folder; public List<Img> images; public List<Mat> materials; }
    [Serializable]
    public class Img { public int index; public string name; public string file; public string mime; public int bytes; }
    [Serializable]
    public class Mat
    {
        public string name;
        public string alphaMode;
        public float[] baseColorFactor;
        public float metallicFactor;
        public float roughnessFactor;
        public float normalScale;
        public int baseTex, normalTex, mrTex, occlusionTex, emissiveTex;
    }

    [MenuItem("潮汐镇/资产/压缩贴图并重绑材质")]
    public static void Run()
    {
        var root = JsonUtility.FromJson<Root>(File.ReadAllText(MapPath));
        if (root == null || root.models == null) { Debug.LogError("TEXMAP parse fail"); return; }

        var normalSet = new HashSet<string>();
        var linearSet = new HashSet<string>();
        var srgbSet = new HashSet<string>();

        foreach (var m in root.models)
        {
            foreach (var mat in m.materials)
            {
                Mark(m, normalSet, mat.normalTex);
                Mark(m, linearSet, mat.mrTex);
                Mark(m, linearSet, mat.occlusionTex);
                Mark(m, srgbSet, mat.baseTex);
                Mark(m, srgbSet, mat.emissiveTex);
            }
        }

        int imported = 0;
        foreach (var m in root.models)
        {
            foreach (var im in m.images)
            {
                string path = "Assets/TideTown/" + m.folder + "/" + im.file;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { Debug.LogWarning("TEX no importer " + path); continue; }
                ti.maxTextureSize = 2048;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.crunchedCompression = false;
                if (normalSet.Contains(path))
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.sRGBTexture = false;
                }
                else
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = srgbSet.Contains(path) && !linearSet.Contains(path);
                }
                ti.SaveAndReimport();
                imported++;
            }
        }
        Debug.Log("TEX import configured " + imported);
        AssetDatabase.Refresh();

        var prefabs = Directory.GetFiles(ArtRoot, "*.prefab", SearchOption.AllDirectories);
        int rebound = 0;
        foreach (var prefabPath in prefabs)
        {
            string id = Path.GetFileNameWithoutExtension(prefabPath);
            var model = root.models.Find(x => x.id == id);
            if (model == null) { Debug.LogWarning("TEX no map for " + id); continue; }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var cache = new Dictionary<string, Material>();
            foreach (var mr in contents.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    var entry = model.materials.Find(x => x.name == src.name);
                    if (entry == null)
                        entry = model.materials[Mathf.Min(i, model.materials.Count - 1)];

                    string key = entry.name + "_" + i;
                    if (!cache.TryGetValue(key, out var mat))
                    {
                        mat = new Material(src);
                        Bind(mat, "baseColorTexture", model, entry.baseTex);
                        Bind(mat, "normalTexture", model, entry.normalTex);
                        Bind(mat, "metallicRoughnessTexture", model, entry.mrTex);
                        Bind(mat, "occlusionTexture", model, entry.occlusionTex);
                        Bind(mat, "emissiveTexture", model, entry.emissiveTex);
                        mat.SetFloat("normalTexture_scale", entry.normalScale);

                        string dir = ArtRoot + "/Materials/" + San(id);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        string assetPath = dir + "/" + San(entry.name) + "_" + i + ".mat";
                        AssetDatabase.DeleteAsset(assetPath);
                        AssetDatabase.CreateAsset(mat, assetPath);
                        cache[key] = mat;
                    }
                    mats[i] = mat;
                }
                mr.sharedMaterials = mats;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            rebound++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TEX rebound prefabs " + rebound);
    }

    static void Mark(Model m, HashSet<string> set, int idx)
    {
        if (idx < 0) return;
        var im = m.images.Find(x => x.index == idx);
        if (im == null) return;
        set.Add("Assets/TideTown/" + m.folder + "/" + im.file);
    }

    static void Bind(Material mat, string prop, Model m, int idx)
    {
        if (idx < 0) return;
        var im = m.images.Find(x => x.index == idx);
        if (im == null) return;
        string path = "Assets/TideTown/" + m.folder + "/" + im.file;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) mat.SetTexture(prop, tex);
        else Debug.LogWarning("TEX missing " + path);
    }

    static string San(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
