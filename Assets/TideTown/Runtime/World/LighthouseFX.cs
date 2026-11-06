// WP-14：灯塔点灯后约 20 秒把天光和主光压暗；存了档再进来直接落到最暗。
using UnityEngine;

public sealed class SkyDimmer : MonoBehaviour
{
    public float seconds = 20f;

    public bool Lit { get; private set; }
    public bool Dimmed => Lit && t >= seconds;
    public float Progress => seconds > 0f ? Mathf.Clamp01(t / seconds) : 1f;

    Light sun;
    Material sky;
    float sun0;
    Color sunColor0, skyTint0, groundColor0;
    float exposure0, ambient0, atmosphere0 = 1f;
    float t;
    bool captured;

    public void Dim(float s)
    {
        if (Lit) return;
        Capture();
        Lit = true;
        seconds = Mathf.Max(0.2f, s);
        t = 0f;
    }

    public void SetDimmedInstant()
    {
        Capture();
        Lit = true;
        t = seconds;
        Apply(1f);
    }

    void Capture()
    {
        if (captured) return;
        captured = true;
        foreach (var l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null) { sun0 = sun.intensity; sunColor0 = sun.color; }
        sky = RenderSettings.skybox;
        if (sky != null)
        {
            // 用副本，别改到工程里的天空盒资产
            sky = new Material(sky);
            RenderSettings.skybox = sky;
            if (sky.HasProperty("_Exposure")) exposure0 = sky.GetFloat("_Exposure");
            if (sky.HasProperty("_SkyTint")) skyTint0 = sky.GetColor("_SkyTint");
            if (sky.HasProperty("_GroundColor")) groundColor0 = sky.GetColor("_GroundColor");
            atmosphere0 = sky.HasProperty("_AtmosphereThickness") ? sky.GetFloat("_AtmosphereThickness") : 1f;
        }
        ambient0 = RenderSettings.ambientIntensity;
    }

    void Update()
    {
        if (!Lit || t >= seconds) return;
        t = Mathf.Min(seconds, t + Time.deltaTime);
        Apply(Mathf.SmoothStep(0f, 1f, Progress));
    }

    void Apply(float p)
    {
        if (sun != null)
        {
            sun.intensity = Mathf.Lerp(sun0, sun0 * 0.18f, p);
            sun.color = Color.Lerp(sunColor0, new Color32(255, 148, 96, 255), p);
        }
        if (sky != null)
        {
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(exposure0, exposure0 * 0.28f, p));
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", Color.Lerp(skyTint0, new Color32(92, 86, 140, 255), p));
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", Color.Lerp(groundColor0, new Color32(22, 18, 26, 255), p));
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(atmosphere0, atmosphere0 * 2.4f, p));
        }
        RenderSettings.ambientIntensity = Mathf.Lerp(ambient0, ambient0 * 0.6f, p);
        RenderSettings.ambientEquatorColor = Color.Lerp(new Color32(228, 204, 170, 255), new Color32(86, 78, 96, 255), p);
        RenderSettings.ambientSkyColor = Color.Lerp(new Color32(250, 239, 221, 255), new Color32(58, 66, 106, 255), p);
    }
}
