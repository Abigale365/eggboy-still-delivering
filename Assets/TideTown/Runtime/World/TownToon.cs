// 2026-11-12 塞尔达式卡通材质（样张）：进游戏时把场景里不透明的写实材质换成 TideTown/Toon。
// 不改工程里的材质文件，换的是运行时的副本——加 -notoon 启动就是原来的样子，方便前后对比。
// 两种来源的材质都认：URP Lit（地面、树、石头、栅栏这些程序生成的）和 glTFast 的（Lux3D 模型）。
// 不换的：透明的（玻璃、水、光束）、粒子、EGGBOY 自己（面屏有专门的着色器）、不认识的着色器。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TownToon : MonoBehaviour
{
    public static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-notoon") < 0;

    static readonly bool onlyGround = Array.IndexOf(Environment.GetCommandLineArgs(), "-toonground") >= 0;   // 调试：只换地面
    Shader toon;
    readonly Dictionary<Material, Material> cache = new Dictionary<Material, Material>();
    readonly Dictionary<Material, Material> cacheNoTangent = new Dictionary<Material, Material>();
    readonly HashSet<int> seen = new HashSet<int>();
    float nextSweep;
    int converted, skipped;

    void Awake()
    {
        toon = Resources.Load<Shader>("TideToon");
        if (toon == null) { Debug.LogWarning("TOWNTOON 找不到 Resources/TideToon"); enabled = false; return; }
        Shader.SetGlobalFloat("_ToonDebug", Array.IndexOf(Environment.GetCommandLineArgs(), "-toondebug") >= 0 ? 1f : 0f);
        Sweep();
        Debug.Log($"TOWNTOON materials={cache.Count + cacheNoTangent.Count} renderers={converted} skipped={skipped}");
    }

    // 后来才出现的东西（小黑、运行时摆出来的道具）隔一会儿补换一次
    void Update()
    {
        if (Time.unscaledTime < nextSweep) return;
        nextSweep = Time.unscaledTime + 1.5f;
        Sweep();
    }

    void Sweep()
    {
        foreach (var r in FindObjectsOfType<Renderer>(true))
        {
            if (!seen.Add(r.GetInstanceID())) continue;
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) { skipped++; continue; }
            if (IsHero(r.transform)) { skipped++; continue; }
            if (onlyGround && r.gameObject.layer != 6) { skipped++; continue; }
            bool tangents = HasTangents(r);
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var t = Convert(mats[i], tangents);
                if (t != null) { mats[i] = t; changed = true; }
            }
            if (changed) { r.sharedMaterials = mats; converted++; }
        }
    }

    static bool IsHero(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name == "EGGBOY") return true;
        return false;
    }

    static bool HasTangents(Renderer r)
    {
        Mesh m = null;
        if (r is SkinnedMeshRenderer s) m = s.sharedMesh;
        else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
        return m != null && m.HasVertexAttribute(VertexAttribute.Tangent);
    }

    Material Convert(Material src, bool tangents)
    {
        if (src == null || src.shader == null || src.shader == toon) return null;
        var map = tangents ? cache : cacheNoTangent;
        if (map.TryGetValue(src, out var done)) return done;

        Material m = null;
        string sn = src.shader.name;
        bool gltf = src.HasProperty("baseColorTexture") || src.HasProperty("baseColorFactor");
        bool lit = sn == "Universal Render Pipeline/Lit" || sn == "Universal Render Pipeline/Simple Lit";
        bool transparent = src.renderQueue >= 2900
            || (src.HasProperty("_Surface") && src.GetFloat("_Surface") > 0.5f);
        if ((gltf || lit) && !transparent)
        {
            m = new Material(toon) { name = src.name + " (toon)" };
            if (gltf)
            {
                if (src.HasProperty("baseColorTexture"))
                {
                    m.SetTexture("_BaseMap", src.GetTexture("baseColorTexture"));
                    m.SetTextureScale("_BaseMap", src.GetTextureScale("baseColorTexture"));
                    m.SetTextureOffset("_BaseMap", src.GetTextureOffset("baseColorTexture"));
                }
                if (src.HasProperty("baseColorFactor")) m.SetColor("_BaseColor", src.GetColor("baseColorFactor"));
                var n = src.HasProperty("normalTexture") ? src.GetTexture("normalTexture") : null;
                if (n != null && tangents)
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", src.HasProperty("normalTexture_scale") ? src.GetFloat("normalTexture_scale") : 1f);
                    m.SetFloat("_UseNormal", 1f);
                }
                var e = src.HasProperty("emissiveTexture") ? src.GetTexture("emissiveTexture") : null;
                if (src.HasProperty("emissiveFactor"))
                {
                    var ec = src.GetColor("emissiveFactor");
                    if (ec.maxColorComponent > 0.001f)
                    {
                        m.SetColor("_EmissionColor", ec);
                        if (e != null) m.SetTexture("_EmissionMap", e);
                    }
                }
                if (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", src.HasProperty("alphaCutoff") ? src.GetFloat("alphaCutoff") : 0.5f);
                }
            }
            else
            {
                m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                m.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
                m.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
                m.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                var n = src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
                if (n != null && tangents && src.IsKeywordEnabled("_NORMALMAP"))
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", src.HasProperty("_BumpScale") ? src.GetFloat("_BumpScale") : 1f);
                    m.SetFloat("_UseNormal", 1f);
                }
                if (src.IsKeywordEnabled("_EMISSION") && src.HasProperty("_EmissionColor"))
                {
                    m.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
                    if (src.HasProperty("_EmissionMap") && src.GetTexture("_EmissionMap") != null)
                        m.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
                }
                if (src.IsKeywordEnabled("_ALPHATEST_ON"))
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
                }
            }
            m.SetFloat("_Cull", src.HasProperty("_Cull") ? src.GetFloat("_Cull") : (float)CullMode.Back);
            m.renderQueue = m.GetFloat("_AlphaClip") > 0.5f ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.enableInstancing = src.enableInstancing;
        }
        map[src] = m;
        return m;
    }
}
