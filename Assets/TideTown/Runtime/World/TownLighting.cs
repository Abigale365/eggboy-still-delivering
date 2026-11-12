// 2026-11-07 小镇灯光：主场景原来全是 Unity 默认值——
//   没烘过光照（m_LightingDataAsset 为空），打包后环境光探针是 0，背光面纯黑；
//   没有环境反射，Lux3D 的釉面、黄铜、玻璃什么都反射不到；太阳不投影；没有任何后期。
// 这里在运行时补上：环境光（自己算球谐，跟着 RenderSettings 的颜色变）、一次实时反射探针、
// 太阳软阴影、全局后期。不改任何模型和材质。
// 灯塔点灯和天亮（LighthouseFX）改的是 RenderSettings 的环境光颜色，这里每帧检测到变化就重算。
//
// 2026-11-12 美术改版：整体气氛照目标图（docs/reference/preview.png）改成"黄昏绘本"——
//   低角度的金橙色太阳、粉紫色天空和云、偏紫的阴影、暖色高光、亮着的路灯、更明显的泛光。
//   所有数值集中在 TownLook 里；包目录下放一个 look.json 可以不重新打包直接调（字段名同 TownLook）。
//   全部用 URP 自带的东西：URP Lit 材质、URP 后期 Volume、URP 阴影级联、自写的 URP 天空和光晕着色器。
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public sealed class TownLook
{
    // 太阳：从东南方低低地照过来（预览机位朝北，房子正面和东墙受光，影子拖向西北）
    public Vector3 sunFrom = new Vector3(0.62f, 0.50f, -0.64f);
    public Color sunColor = new Color32(255, 210, 164, 255);
    public float sunIntensity = 2.3f;
    public float shadowStrength = 0.82f;
    public float shadowDistance = 75f;
    public int shadowCascades = 2;

    // 环境光：上面冷紫、中间粉、下面暗玫瑰，阴影就不再是灰的
    public Color ambientSky = new Color32(170, 156, 222, 255);
    public Color ambientEquator = new Color32(234, 190, 170, 255);
    public Color ambientGround = new Color32(124, 88, 92, 255);
    public float ambientIntensity = 0.62f;

    // 天空（TideTown/Sky）
    public Color skyTop = new Color32(58, 70, 152, 255);
    public Color skyMid = new Color32(158, 120, 190, 255);
    public Color skyHorizon = new Color32(255, 178, 132, 255);
    public Color skySun = new Color32(255, 220, 150, 255);
    public Color cloudLight = new Color32(255, 186, 190, 255);
    public Color cloudShade = new Color32(150, 108, 178, 255);
    public float cloudCover = 0.48f;
    public Vector3 moonDir = new Vector3(0.42f, 0.46f, 0.78f);
    public float stars = 0.8f;

    // 雾：远处往粉橙色的地平线收
    public Color fogColor = new Color32(238, 176, 160, 255);
    public float fogStart = 150f;
    public float fogEnd = 700f;

    // 后期
    public float exposure = 0.05f;
    public float contrast = 10f;
    public float saturation = 10f;
    public float temperature = 2f;
    public float tint = 0f;
    public Color splitShadows = new Color(0.46f, 0.45f, 0.57f, 1f);
    public Color splitHighlights = new Color(0.60f, 0.52f, 0.42f, 1f);
    public float splitBalance = -8f;
    public float bloomThreshold = 0.95f;
    public float bloomIntensity = 0.5f;
    public float bloomScatter = 0.72f;
    public Color bloomTint = new Color32(255, 226, 196, 255);
    public float vignette = 0.2f;

    // 路灯
    public int lampLights = 6;
    public float lampIntensity = 3.2f;

    public static TownLook Load()
    {
        var look = new TownLook();
        try
        {
            string path = Path.Combine(Application.dataPath, "..", "look.json");
            if (File.Exists(path))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), look);
                Debug.Log("TOWNLOOK 读取 look.json");
            }
        }
        catch (Exception e) { Debug.LogWarning("TOWNLOOK look.json 读取失败：" + e.Message); }
        return look;
    }
}

public sealed class TownLighting : MonoBehaviour
{
    public static TownLook Look { get; private set; }

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
        var look = Look = TownLook.Load();

        // 环境光
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = look.ambientSky;
        RenderSettings.ambientEquatorColor = look.ambientEquator;
        RenderSettings.ambientGroundColor = look.ambientGround;
        RenderSettings.ambientIntensity = look.ambientIntensity;
        RefreshAmbient(true);

        // 雾
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = look.fogColor;
        RenderSettings.fogStartDistance = look.fogStart;
        RenderSettings.fogEndDistance = look.fogEnd;

        // 天空
        Vector3 sunFrom = look.sunFrom.normalized;
        var skyShader = Resources.Load<Shader>("TideSky");
        if (skyShader != null)
        {
            var sky = new Material(skyShader);
            sky.SetColor("_TopColor", look.skyTop);
            sky.SetColor("_MidColor", look.skyMid);
            sky.SetColor("_HorizonColor", look.skyHorizon);
            sky.SetColor("_SunColor", look.skySun);
            // 天上的太阳画得比实际光照方向低一些：贴着海面的落日，光照太低的话影子会长到盖住整条街
            sky.SetVector("_SunDir", new Vector3(sunFrom.x, 0.14f, sunFrom.z).normalized);
            sky.SetColor("_CloudLight", look.cloudLight);
            sky.SetColor("_CloudShade", look.cloudShade);
            sky.SetFloat("_CloudCover", look.cloudCover);
            sky.SetVector("_MoonDir", look.moonDir.normalized);
            sky.SetFloat("_Stars", look.stars);
            sky.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = sky;
        }
        else Debug.LogWarning("TOWNLOOK 找不到 Resources/TideSky，天空保持原样");

        // 太阳：场景里有两盏叫 Sun 的平行光（生成器一盏、EggboyEnv 一盏），只留一盏，另一盏关掉
        Light sun = null;
        foreach (var l in FindObjectsOfType<Light>())
        {
            if (l.type != LightType.Directional || l.name != "Sun") continue;
            if (sun == null) sun = l;
            else l.gameObject.SetActive(false);
        }
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(-sunFrom);
            sun.color = look.sunColor;
            sun.intensity = look.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = look.shadowStrength;
            sun.shadowBias = 0.6f;
            sun.shadowNormalBias = 0.6f;
        }
        ApplyShadowRange(look.shadowDistance, look.shadowCascades);

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
        BuildVolume(look);
        foreach (var cam in FindObjectsOfType<Camera>())
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        // 路灯：只开最近的几盏点光
        var lamps = gameObject.AddComponent<LampGlowDirector>();
        lamps.maxLights = look.lampLights;
        lamps.lightIntensity = look.lampIntensity;
    }

    // 阴影范围：URP 资产上的设置，运行时改的是内存里的副本，不动工程文件
    public static void ApplyShadowRange(float distance, int cascades)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        urp.shadowDistance = distance;
        urp.shadowCascadeCount = Mathf.Clamp(cascades, 1, 4);
        QualitySettings.shadowDistance = distance;
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

    void BuildVolume(TownLook look)
    {
        var vgo = new GameObject("TownPostFX");
        vgo.transform.SetParent(transform, false);
        var vol = vgo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 5f;   // 压过相机上 EggboyPostFX 的那个（优先级 1），小镇里以这里为准
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.sharedProfile = profile;

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);   // ACES 会把暖色压灰、压暗

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(look.exposure);
        color.contrast.Override(look.contrast);
        color.saturation.Override(look.saturation);
        color.colorFilter.Override(Color.white);

        var white = profile.Add<WhiteBalance>(true);
        white.temperature.Override(look.temperature);
        white.tint.Override(look.tint);

        var split = profile.Add<SplitToning>(true);
        split.shadows.Override(look.splitShadows);
        split.highlights.Override(look.splitHighlights);
        split.balance.Override(look.splitBalance);

        var bloom = profile.Add<Bloom>(true);
        bool hdr = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
        bloom.threshold.Override(hdr ? look.bloomThreshold : look.bloomThreshold - 0.15f);
        bloom.intensity.Override(look.bloomIntensity);
        bloom.scatter.Override(look.bloomScatter);
        bloom.tint.Override(look.bloomTint);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(look.vignette);
        vignette.smoothness.Override(0.45f);
    }
}
