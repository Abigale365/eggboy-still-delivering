// 2026-11-12 美术改版：路灯亮起来。
// 每盏灯一个光晕面片（远近都看得到）+ 一盏暖色点光。点光只开离镜头最近的几盏：
// 全镇二三十盏同时开，核显和网页版吃不消，而且远处的点光本来也照不出什么。
using System.Collections.Generic;
using UnityEngine;

public sealed class LampGlow : MonoBehaviour
{
    public Light lamp;
    public Renderer halo;

    static readonly List<LampGlow> All = new List<LampGlow>();
    public static IReadOnlyList<LampGlow> Lamps => All;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
}

public sealed class LampGlowDirector : MonoBehaviour
{
    public int maxLights = 6;
    public float lightIntensity = 3.2f;
    float nextAt;
    readonly List<LampGlow> sorted = new List<LampGlow>();

    void Update()
    {
        if (Time.unscaledTime < nextAt) return;
        nextAt = Time.unscaledTime + 0.3f;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        sorted.Clear();
        sorted.AddRange(LampGlow.Lamps);
        sorted.Sort((a, b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
        for (int i = 0; i < sorted.Count; i++)
        {
            var l = sorted[i].lamp;
            if (l == null) continue;
            bool on = i < maxLights;
            if (l.enabled != on) l.enabled = on;
            if (on) l.intensity = lightIntensity;
        }
    }
}
