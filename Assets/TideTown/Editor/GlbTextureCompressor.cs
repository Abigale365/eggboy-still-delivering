// 2026-11-08 网页版瘦身：Lux3D 的 GLB 经 glTFast 导入后，内嵌贴图全是未压缩 ARGB32（1024/2048），
// 网页版 1.9 GB 素材里贴图占 1.6 GB，data 文件压完还有 492 MB（GitHub 单文件上限 100 MB，评委也下不动）。
// glTFast 没有压缩选项，所以：
//   1. Extract()：从每个 GLB 里把原图字节原样取出（不重编码），存到 Assets/TideTown/Art/GlbTex/<编号>/，
//      文件名带用途后缀（__n 法线、__l 线性数据、无后缀 = 颜色），.meta 用固定 GUID（GLB 路径 + 图片序号的 MD5），
//      所以这个目录不进 git，任何时候重新取出来引用都不会断；打开工程时缺了会自动补（InitializeOnLoad）
//   2. GlbTexturePostprocessor：这个目录里的贴图最大 1024、crunch 压缩，法线按 NormalMap，线性数据关 sRGB
//   3. ReplaceInScene()：场景里所有用 GLB 子材质的渲染器，换成 Assets/TideTown/Art/GlbMats/ 下的材质副本，
//      副本里 GLB 贴图按名字换成压缩版。原 GLB 文件一个字节不动。IslandBuilder.BuildMain 最后调用。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GlbTextureCompressor
{
    public const string SourceRoot = "Assets/TideTown/SourceGLB";
    public const string TexRoot = "Assets/TideTown/Art/GlbTex";
    public const string MatRoot = "Assets/TideTown/Art/GlbMats";

    static GlbTextureCompressor()
    {
        // 新克隆的工程没有 GlbTex（不进 git）：编辑器加载完自动补上
        EditorApplication.delayCall += () =>
        {
            if (!Directory.Exists(TexRoot) && Directory.Exists(SourceRoot)) Extract();
        };
    }

    [Serializable] class GJson { public GImage[] images; public GTex[] textures; public GMat[] materials; }
    [Serializable] class GImage { public string name; public int bufferView = -1; public string mimeType; public string uri; }
    [Serializable] class GTex { public int source = -1; }
    [Serializable] class GRef { public int index = -1; }
    [Serializable] class GPbr { public GRef baseColorTexture; public GRef metallicRoughnessTexture; }
    [Serializable] class GMat { public string name; public GPbr pbrMetallicRoughness; public GRef normalTexture; public GRef occlusionTexture; public GRef emissiveTexture; }
    [Serializable] class GView { public int buffer; public int byteOffset; public int byteLength; }
    [Serializable] class GViews { public GView[] bufferViews; }

    static string GlbId(string glbPath) => Path.GetFileNameWithoutExtension(glbPath).Split('_')[0];

    static string Guid32(string s)
    {
        using (var md5 = MD5.Create())
            return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(b => b.ToString("x2")));
    }

    static string Safe(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '_');
        var r = sb.ToString();
        return r.Length > 40 ? r.Substring(0, 40) : r;
    }

    // 每个 GLB：图片序号 → (导入名, 文件路径)
    public class GlbImages { public string glb; public List<string> names = new List<string>(); public List<string> files = new List<string>(); }

    public static List<GlbImages> Extract()
    {
        var all = new List<GlbImages>();
        if (!Directory.Exists(SourceRoot)) return all;
        int written = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var glb in Directory.GetFiles(SourceRoot, "*.glb", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                var bytes = File.ReadAllBytes(glb);
                int jsonLen = BitConverter.ToInt32(bytes, 12);
                string json = Encoding.UTF8.GetString(bytes, 20, jsonLen);
                int binOff = 20 + jsonLen + 8;
                var g = JsonUtility.FromJson<GJson>(json);
                var views = JsonUtility.FromJson<GViews>(json).bufferViews;
                var info = new GlbImages { glb = glb };
                all.Add(info);
                if (g.images == null) continue;
                // 用途：被当法线用 → __n；被当金属粗糙度 / AO 用 → __l；其余（颜色、自发光）不加后缀
                var usage = new Dictionary<int, string>();
                void Mark(GRef r, string u)
                {
                    if (r == null || r.index < 0 || g.textures == null || r.index >= g.textures.Length) return;
                    int img = g.textures[r.index].source;
                    if (img < 0) return;
                    if (!usage.TryGetValue(img, out var old) || old == "l" && u == "n") usage[img] = u;
                    else if (old == "c" && u != "c") { }   // 同一张图又当颜色又当数据：按颜色
                }
                if (g.materials != null)
                    foreach (var m in g.materials)
                    {
                        Mark(m.pbrMetallicRoughness?.baseColorTexture, "c");
                        Mark(m.emissiveTexture, "c");
                        Mark(m.normalTexture, "n");
                        Mark(m.pbrMetallicRoughness?.metallicRoughnessTexture, "l");
                        Mark(m.occlusionTexture, "l");
                    }
                string dir = TexRoot + "/" + GlbId(glb);
                Directory.CreateDirectory(dir);
                for (int i = 0; i < g.images.Length; i++)
                {
                    var im = g.images[i];
                    string name = string.IsNullOrEmpty(im.name) ? "Image_" + i : im.name;
                    info.names.Add(name);
                    if (im.bufferView < 0 || views == null) { info.files.Add(null); continue; }
                    var v = views[im.bufferView];
                    string ext = im.mimeType == "image/jpeg" ? ".jpg" : ".png";
                    usage.TryGetValue(i, out var u);
                    string suffix = u == "n" ? "__n" : u == "l" ? "__l" : "";
                    string file = $"{dir}/{i:D2}_{Safe(name)}{suffix}{ext}";
                    info.files.Add(file);
                    if (!File.Exists(file))
                    {
                        using (var fs = File.Create(file)) fs.Write(bytes, binOff + v.byteOffset, v.byteLength);
                        written++;
                    }
                    if (!File.Exists(file + ".meta"))
                        File.WriteAllText(file + ".meta", "fileFormatVersion: 2\nguid: " + Guid32(glb + "#" + i) + "\nTextureImporter:\n  serializedVersion: 12\n");
                }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        if (written > 0) AssetDatabase.Refresh();
        Debug.Log($"GLBTEX extracted {written} new images from {all.Count} GLBs");
        return all;
    }

    public static void ReplaceInScene()
    {
        var glbs = Extract().ToDictionary(x => x.glb);
        var texCache = new Dictionary<string, Texture2D>();
        Texture2D Compressed(string glb, string texName)
        {
            if (!glbs.TryGetValue(glb, out var info)) return null;
            int i = info.names.IndexOf(texName);
            // 没名字的图 glTFast 有时叫 image_N（小写），按序号对
            if (i < 0 && texName.StartsWith("image_", StringComparison.OrdinalIgnoreCase) && int.TryParse(texName.Substring(6), out int idx) && idx < info.files.Count) i = idx;
            if (i < 0 || info.files[i] == null) return null;
            if (!texCache.TryGetValue(info.files[i], out var t)) texCache[info.files[i]] = t = AssetDatabase.LoadAssetAtPath<Texture2D>(info.files[i]);
            return t;
        }
        var matCache = new Dictionary<Material, Material>();
        var usedNames = new HashSet<string>();
        int swappedTex = 0, missing = 0, renderers = 0;
        if (!AssetDatabase.IsValidFolder(MatRoot)) AssetDatabase.CreateFolder("Assets/TideTown/Art", "GlbMats");
        Material Replace(Material src)
        {
            if (src == null) return null;
            string glb = AssetDatabase.GetAssetPath(src);
            if (!glb.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) return src;
            if (matCache.TryGetValue(src, out var done)) return done;
            string dir = MatRoot + "/" + GlbId(glb);
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(MatRoot, GlbId(glb));
            string baseName = $"{dir}/{Safe(src.name)}";
            string path = baseName + ".mat";
            for (int k = 2; usedNames.Contains(path); k++) path = $"{baseName}_{k}.mat";
            usedNames.Add(path);
            var copy = new Material(src) { name = src.name };
            foreach (var p in src.GetTexturePropertyNames())
            {
                var tx = src.GetTexture(p);
                if (tx == null || !AssetDatabase.GetAssetPath(tx).EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) continue;
                var c = Compressed(glb, tx.name);
                if (c != null) { copy.SetTexture(p, c); swappedTex++; }
                else { missing++; Debug.LogWarning($"GLBTEX no match {glb} {tx.name}"); }
            }
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { EditorUtility.CopySerialized(copy, existing); EditorUtility.SetDirty(existing); copy = existing; }
            else AssetDatabase.CreateAsset(copy, path);
            matCache[src] = copy;
            return copy;
        }
        foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var n = Replace(mats[i]);
                if (n != mats[i]) { mats[i] = n; changed = true; }
            }
            if (changed) { r.sharedMaterials = mats; renderers++; }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"GLBTEX scene: renderers={renderers} materials={matCache.Count} textures_swapped={swappedTex} missing={missing}");
    }
}

// GlbTex 目录里的贴图：最大 1024、crunch；法线 / 线性数据按文件名后缀
public class GlbTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(GlbTextureCompressor.TexRoot + "/")) return;
        var ti = (TextureImporter)assetImporter;
        string stem = Path.GetFileNameWithoutExtension(assetPath);
        bool normal = stem.EndsWith("__n"), linear = stem.EndsWith("__l");
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !(normal || linear);
        ti.mipmapEnabled = true;
        ti.isReadable = false;
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.crunchedCompression = true;
        ti.compressionQuality = 60;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.filterMode = FilterMode.Bilinear;
    }
}
