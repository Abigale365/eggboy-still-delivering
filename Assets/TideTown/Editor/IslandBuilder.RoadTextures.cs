// 路面程序贴图：土路 / 碎石各一套 albedo + 法线，512² 可平铺。
// UV 以米为单位（见 IslandBuilder.cs 的 AddRibbon），材质 _BaseMap_ST 决定一张贴图铺几米。
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    const string TexRoot = "Assets/TideTown/Art/Textures";
    const int SurfaceTexSize = 512;

    // 有贴图的 URP Lit 材质；贴图只在文件缺失时生成，之后跟着工程走
    static Material RoadSurfaceMat(string name, Color32 tint, string key, int seed, int pebbles, float tile, float normalStrength)
    {
        if (!AssetDatabase.IsValidFolder(TexRoot)) AssetDatabase.CreateFolder("Assets/TideTown/Art", "Textures");
        string albedoPath = TexRoot + "/" + key + "_Albedo.png";
        string normalPath = TexRoot + "/" + key + "_Normal.png";
        if (!File.Exists(albedoPath) || !File.Exists(normalPath))
        {
            BuildSurfaceTextures(albedoPath, normalPath, tint, seed, pebbles, normalStrength);
            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"ROADTEX generated {key}");
        }
        ConfigureSurfaceTexture(albedoPath, false);
        ConfigureSurfaceTexture(normalPath, true);

        string path = MatRoot + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.08f);
        m.SetFloat("_Metallic", 0f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        m.EnableKeyword("_NORMALMAP");
        var scale = new Vector2(1f / tile, 1f / tile);
        m.SetTextureScale("_BaseMap", scale);
        m.SetTextureScale("_BumpMap", scale);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void ConfigureSurfaceTexture(string path, bool normal)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            imp = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (imp == null) throw new Exception("贴图导入失败: " + path);
        bool dirty = false;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (imp.textureType != type) { imp.textureType = type; dirty = true; }
        if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; dirty = true; }
        if (imp.maxTextureSize != SurfaceTexSize) { imp.maxTextureSize = SurfaceTexSize; dirty = true; }
        if (imp.textureCompression != TextureImporterCompression.Compressed) { imp.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
        if (!normal && !imp.sRGBTexture) { imp.sRGBTexture = true; dirty = true; }
        if (dirty) imp.SaveAndReimport();
    }

    static void BuildSurfaceTextures(string albedoPath, string normalPath, Color32 tint, int seed, int pebbles, float normalStrength)
    {
        int n = SurfaceTexSize;
        var height = new float[n * n];
        var mottle = new float[n * n];
        var grain = new float[n * n];
        var stone = new float[n * n];

        // 大尺度土色斑块 + 细颗粒，两者都可平铺
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float u = x / (float)n, v = y / (float)n;
            int i = y * n + x;
            mottle[i] = Fbm(u, v, 3, seed + 11);
            grain[i] = LatticeNoise(u, v, 128, seed + 23);
            height[i] = 0.35f * mottle[i] + 0.12f * grain[i];
        }

        // 石子：隆起 + 灰白色；位置、半径、高度都用固定种子，贴图可复现
        var rnd = new Lcg(seed);
        for (int k = 0; k < pebbles; k++)
        {
            float cx = rnd.Next01() * n, cy = rnd.Next01() * n;
            float rad = 2.2f + rnd.Next01() * 5.5f;
            float bump = 0.22f + rnd.Next01() * 0.55f;
            int x0 = Mathf.FloorToInt(cx - rad) - 1, x1 = Mathf.CeilToInt(cx + rad) + 1;
            int y0 = Mathf.FloorToInt(cy - rad) - 1, y1 = Mathf.CeilToInt(cy + rad) + 1;
            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                float dx = WrapDelta(px + 0.5f - cx, n);
                float dy = WrapDelta(py + 0.5f - cy, n);
                float d2 = (dx * dx + dy * dy) / (rad * rad);
                if (d2 >= 1f) continue;
                int i = Wrap(px, n) + Wrap(py, n) * n;
                float dome = 1f - d2;
                height[i] += bump * dome * dome;
                stone[i] = Mathf.Max(stone[i], Mathf.Clamp01(dome * 1.3f));
            }
        }

        var albedo = new Color32[n * n];
        var normal = new Color32[n * n];
        float br = tint.r / 255f, bg = tint.g / 255f, bb = tint.b / 255f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            float m = mottle[i], g = grain[i], st = stone[i];
            float shade = 0.86f + 0.20f * (m - 0.5f) + 0.10f * (g - 0.5f);
            if (Hash01(x, y, seed + 91) < 0.04f) shade -= 0.12f;
            // 石子偏灰白，土面保留 tint；两者都不纯色，远看才不像塑料
            float sr = Mathf.Lerp(br * shade, 0.80f * (0.90f + 0.20f * m), st * 0.7f);
            float sg = Mathf.Lerp(bg * shade, 0.79f * (0.90f + 0.20f * m), st * 0.7f);
            float sb = Mathf.Lerp(bb * shade, 0.75f * (0.90f + 0.20f * m), st * 0.7f);
            albedo[i] = new Color32(ToByte(sr), ToByte(sg), ToByte(sb), 255);

            int xm = Wrap(x - 1, n), xp = Wrap(x + 1, n), ym = Wrap(y - 1, n), yp = Wrap(y + 1, n);
            float gx = (height[xp + y * n] - height[xm + y * n]) * 0.5f;
            float gy = (height[x + yp * n] - height[x + ym * n]) * 0.5f;
            var nrm = new Vector3(-gx * normalStrength, -gy * normalStrength, 1f).normalized;
            normal[i] = new Color32(ToByte(nrm.x * 0.5f + 0.5f), ToByte(nrm.y * 0.5f + 0.5f),
                ToByte(nrm.z * 0.5f + 0.5f), 255);
        }

        WriteSurfaceTexture(albedoPath, albedo, n);
        WriteSurfaceTexture(normalPath, normal, n);
    }

    static void WriteSurfaceTexture(string path, Color32[] pixels, int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.SetPixels32(pixels);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
    }

    // 周期性插值噪声：lattice 下标按 period 取模，贴图左右/上下接缝一致
    static float LatticeNoise(float u, float v, int period, int seed)
    {
        float x = u * period, y = v * period;
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash01(xi, yi, seed), b = Hash01(xi + 1, yi, seed);
        float c = Hash01(xi, yi + 1, seed), d = Hash01(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * LatticeNoise(u, v, 4 << o, seed + o * 37);
            norm += amp;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777215f;
        }
    }

    static int Wrap(int v, int n)
    {
        v %= n;
        return v < 0 ? v + n : v;
    }

    static float WrapDelta(float d, int n)
    {
        if (d > n * 0.5f) d -= n;
        else if (d < -n * 0.5f) d += n;
        return d;
    }

    static byte ToByte(float v)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }

    struct Lcg
    {
        uint state;
        public Lcg(int seed) { state = (uint)seed * 2654435761u + 1u; }
        public float Next01()
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) / 16777216f;
        }
    }
}
