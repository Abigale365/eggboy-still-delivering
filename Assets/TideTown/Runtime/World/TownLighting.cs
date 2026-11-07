// 2026-11-07 小镇灯光：主场景原来全是 Unity 默认值——
//   没烘过光照（m_LightingDataAsset 为空），打包后环境光探针是 0，背光面纯黑；
//   没有环境反射，Lux3D 的釉面、黄铜、玻璃什么都反射不到；太阳不投影；没有任何后期。
// 这里在运行时补上：环境光（自己算球谐，跟着 RenderSettings 的颜色变）、一次实时反射探针、
// 太阳软阴影、全局后期（中性色调映射 + 轻微色彩 + 暗角 + 泛光）。不改任何模型和材质。
// 灯塔点灯和天亮（LighthouseFX）改的是 RenderSettings 的环境光颜色，这里每帧检测到变化就重算。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class TownLighting : MonoBehaviour
{
    // 和 EggboyEnv 同一组暖色（LighthouseFX 的日间基准色也是这组）
    static readonly Color AmbSky = new Color32(250, 239, 221, 255);
    static readonly Color AmbEquator = new Color32(228, 204, 170, 255);
    static readonly Color AmbGround = new Color32(122, 93, 58, 255);
    const float AmbIntensity = 0.5f;   // 补光弱一点、太阳强一点，亮面和背光面才分得开，模型才有体积

    Color lastSky, lastEq, lastGround;
    float lastIntensity = -1f;
    ReflectionProbe probe;
    float probeDirtyAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindObjectOfType<IslandWorld>() == null) return;      // 只管小镇主场景
        if (FindObjectOfType<TownLighting>() != null) return;
        new GameObject("TownLighting").AddComponent<TownLighting>();
    }

    void Awake()
    {
        // 环境光
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbSky;
        RenderSettings.ambientEquatorColor = AmbEquator;
        RenderSettings.ambientGroundColor = AmbGround;
        RenderSettings.ambientIntensity = AmbIntensity;
        RefreshAmbient(true);

        // 太阳：打开软阴影
        foreach (var l in FindObjectsOfType<Light>())
        {
            if (l.type != LightType.Directional || l.name != "Sun") continue;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.8f;
            l.intensity = Mathf.Max(l.intensity, 2.0f);
            break;
        }

        // 反射：整镇一个实时探针，开局渲一次
        var pgo = new GameObject("TownReflection");
        pgo.transform.SetParent(transform, false);
        pgo.transform.position = new Vector3(0f, 6f, 0f);
        probe = pgo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.size = new Vector3(400f, 120f, 400f);
        probe.resolution = 128;
        probe.hdr = true;
        probe.intensity = 1f;
        probe.importance = 0;
        probeDirtyAt = 0f;

        // 后期：全局 Volume
        BuildVolume();
        foreach (var cam in FindObjectsOfType<Camera>())
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    void LateUpdate()
    {
        RefreshAmbient(false);
        if (!heroDone) ExcludeHeroFromReflections();
        if (probeDirtyAt >= 0f && Time.unscaledTime >= probeDirtyAt)
        {
            probeDirtyAt = -1f;
            if (probe != null) probe.RenderProbe();
        }
    }

    // 2026-11-08：EGGBOY 不吃小镇反射探针。面屏是低面数的黑色亮面，反射环境后左上角露出一块块灰色棱角，
    // 左眼旁还映出路灯的亮斑（用户截图）。关掉后面屏回到纯黑底，只显示表情；外壳本来就哑光，看不出差别。
    // 不改 EGGBOY 的任何脚本和材质，只改它渲染器的反射探针用法。
    bool heroDone;
    void ExcludeHeroFromReflections()
    {
        var hero = GameObject.Find("EGGBOY");
        if (hero == null) return;
        foreach (var r in hero.GetComponentsInChildren<Renderer>(true))
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        heroDone = true;
    }

    // 环境光颜色一变就重算球谐；变化较大时 1 秒后重渲一次反射（灯塔变暗、天亮）
    void RefreshAmbient(bool force)
    {
        var sky = RenderSettings.ambientSkyColor;
        var eq = RenderSettings.ambientEquatorColor;
        var gr = RenderSettings.ambientGroundColor;
        float k = RenderSettings.ambientIntensity;
        if (!force && sky == lastSky && eq == lastEq && gr == lastGround && Mathf.Approximately(k, lastIntensity)) return;
        bool big = Mathf.Abs(k - lastIntensity) > 0.08f || Mathf.Abs(sky.grayscale - lastSky.grayscale) > 0.08f;
        lastSky = sky; lastEq = eq; lastGround = gr; lastIntensity = k;

        // 三色环境光 → L2 球谐：赤道色做底，天空色从上方补，地面色从下方补
        var sh = new SphericalHarmonicsL2();
        sh.AddAmbientLight(eq.linear * k);
        sh.AddDirectionalLight(Vector3.up, (sky.linear - eq.linear) * k * 1.6f, 1f);
        sh.AddDirectionalLight(Vector3.down, (gr.linear - eq.linear) * k * 1.6f, 1f);
        RenderSettings.ambientProbe = sh;

        if (!force && big && probe != null && probeDirtyAt < 0f) probeDirtyAt = Time.unscaledTime + 1f;
    }

    void BuildVolume()
    {
        var vgo = new GameObject("TownPostFX");
        vgo.transform.SetParent(transform, false);
        var vol = vgo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 0.5f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.sharedProfile = profile;

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);   // ACES 会把暖色压灰、压暗，三.js 版当时也选了中性

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.1f);
        color.contrast.Override(8f);
        color.saturation.Override(6f);

        var bloom = profile.Add<Bloom>(true);
        bool hdr = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
        bloom.threshold.Override(hdr ? 1.05f : 0.85f);
        bloom.intensity.Override(0.18f);
        bloom.scatter.Override(0.65f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.14f);
        vignette.smoothness.Override(0.45f);
    }
}
