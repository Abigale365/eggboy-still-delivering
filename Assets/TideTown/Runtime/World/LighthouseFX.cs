// WP-14：灯塔点灯后约 20 秒把天光和主光压暗；存了档再进来直接落到最暗。
using UnityEngine;

public sealed class SkyDimmer : MonoBehaviour
{
    public float seconds = 20f;

    public bool Lit { get; private set; }
    public bool Dawned { get; private set; }
    public bool Dimmed => Lit && t >= seconds;
    public float Progress => seconds > 0f ? Mathf.Clamp01(t / seconds) : 1f;

    Light sun;
    Material sky;
    float sun0;
    Color sunColor0, skyTint0, groundColor0;
    float exposure0, ambient0, atmosphere0 = 1f;
    float t;
    bool captured;
    bool dawning;
    float dawnT, dawnSeconds;
    float fSunI, fExposure, fAmbient, fAtmosphere;
    Color fSun, fSkyTint, fGround, fAmbSky, fAmbEq;

    public void Dim(float s)
    {
        if (Lit || dawning) return;
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

    // WP-15：回邮局时天快亮，从当前夜色往黎明的暖色过渡
    public void Dawn(float s)
    {
        if (Dawned) return;
        Capture();
        Dawned = true;
        Lit = true;
        t = seconds; // 停住变暗
        dawning = true;
        dawnT = 0f;
        dawnSeconds = Mathf.Max(0.2f, s);
        fSunI = sun != null ? sun.intensity : 1f;
        fSun = sun != null ? sun.color : Color.white;
        fExposure = sky != null && sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1f;
        fSkyTint = sky != null && sky.HasProperty("_SkyTint") ? sky.GetColor("_SkyTint") : Color.white;
        fGround = sky != null && sky.HasProperty("_GroundColor") ? sky.GetColor("_GroundColor") : Color.black;
        fAtmosphere = sky != null && sky.HasProperty("_AtmosphereThickness") ? sky.GetFloat("_AtmosphereThickness") : 1f;
        fAmbient = RenderSettings.ambientIntensity;
        fAmbSky = RenderSettings.ambientSkyColor;
        fAmbEq = RenderSettings.ambientEquatorColor;
    }

    public void SetDawnInstant()
    {
        if (Dawned) return;
        Dawn(0.01f);
        dawnT = dawnSeconds;
        ApplyDawn(1f);
        dawning = false;
    }

    // 预警：Capture 只在第一次取原始值
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
        if (dawning)
        {
            dawnT = Mathf.Min(dawnSeconds, dawnT + Time.deltaTime);
            ApplyDawn(Mathf.SmoothStep(0f, 1f, dawnT / dawnSeconds));
            if (dawnT >= dawnSeconds) dawning = false;
            return;
        }
        if (!Lit || t >= seconds) return;
        t = Mathf.Min(seconds, t + Time.deltaTime);
        Apply(Mathf.SmoothStep(0f, 1f, Progress));
    }

    void ApplyDawn(float q)
    {
        if (sun != null)
        {
            sun.intensity = Mathf.Lerp(fSunI, sun0 * 0.55f, q);
            sun.color = Color.Lerp(fSun, new Color32(255, 200, 150, 255), q);
        }
        if (sky != null)
        {
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(fExposure, exposure0 * 0.8f, q));
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", Color.Lerp(fSkyTint, new Color32(255, 176, 142, 255), q));
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", Color.Lerp(fGround, new Color32(46, 36, 38, 255), q));
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(fAtmosphere, atmosphere0 * 1.5f, q));
        }
        RenderSettings.ambientIntensity = Mathf.Lerp(fAmbient, ambient0 * 0.9f, q);
        RenderSettings.ambientEquatorColor = Color.Lerp(fAmbEq, new Color32(230, 186, 152, 255), q);
        RenderSettings.ambientSkyColor = Color.Lerp(fAmbSky, new Color32(255, 222, 192, 255), q);
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
