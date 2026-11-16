// 2026-11-07 小镇灯光：主场景原来全是 Unity 默认值——
//   没烘过光照（m_LightingDataAsset 为空），打包后环境光探针是 0，背光面纯黑；
//   没有环境反射，Lux3D 的釉面、黄铜、玻璃什么都反射不到；太阳不投影；没有任何后期。
// 这里在运行时补上：环境光（自己算球谐，跟着 RenderSettings 的颜色变）、一次实时反射探针、
// 太阳软阴影、全局后期。不改任何模型和材质。
// 灯塔点灯和天亮（LighthouseFX）改的是 RenderSettings 的环境光颜色，这里每帧检测到变化就重算。
//
// 2026-11-12 美术改版：气氛照目标图（docs/reference/preview.png）做成"绘本"感，分两套——
//   白天（TownLook.Day）：开局到学校那封信读完。晴朗蓝天、白云、偏低的暖黄太阳，路灯和窗灯微亮（2026-11-15 PL-13 改过，见 Day() 上面的说明）。
//   黄昏（TownLook.Dusk）：学校的邮戳盖完以后慢慢过渡过去，去灯塔和结局都在黄昏里。
//                        低角度的金橙色太阳、粉紫色天空、偏紫的阴影、路灯亮起、月亮升上来。
//   面包店的邮戳盖完先往黄昏走三成（午后，光线开始变暖）。
//   之后灯塔点灯变暗、结局天亮仍由 LighthouseFX 的 SkyDimmer 接手，这里不再动。
// 包目录下放 look.json 可以不重新打包直接调：{"day":{...},"dusk":{...}}，字段名同 TownLook。
// 命令行 -look day / -look dusk 固定在一档（截图、调色用）。
// 全部用 URP 自带的东西：URP Lit 材质、URP 后期 Volume、URP 阴影级联、自写的 URP 天空和光晕着色器。
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public sealed class TownLook
{
    // 太阳（sunFrom 是太阳所在的方向；预览机位朝北，房子正面和东墙受光）
    public Vector3 sunFrom;
    public float skySunY;          // 天上画的太阳的高度（黄昏时画得比实际光照低，贴着海面）
    public Color sunColor;
    public float sunIntensity;
    public float shadowStrength;

    // 环境光：上 / 中 / 下三色
    public Color ambientSky, ambientEquator, ambientGround;
    public float ambientIntensity;

    // 天空（TideTown/Sky）
    public Color skyTop, skyMid, skyHorizon, skySun, cloudLight, cloudShade;
    public float cloudCover;
    public Vector3 moonDir;
    public float stars;

    // 雾
    public Color fogColor;
    public float fogStart, fogEnd;

    // 海面映出的天色
    public Color waterSky;
    public float waterSkyReflect;

    // 后期
    public float exposure, contrast, saturation, temperature, tint;
    public Color splitShadows, splitHighlights;
    public float splitBalance;
    public float bloomThreshold, bloomIntensity, bloomScatter;
    public Color bloomTint;
    public float vignette;

    // 路灯：0 不亮，1 全亮
    public float lampOn;

    // 卡通材质（TideTown/Toon）：阴影的颜色、远处空气的颜色
    public Color toonShadow, toonHaze;

    // 2026-11-15 PL-13（工程书 D3）：白天也压低太阳、加暖。
    //   · 太阳高度角从 51° 压到 36°（sunFrom.y 0.95 → 0.56），房子、树、栅栏拖出斜长的影子；天上画的太阳跟着降
    //   · 主光往暖黄走一档，阴影色再冷一点、深一点：向阳面暖金，背光面是通透的冷蓝，不是灰
    //   · 路灯和窗灯微亮（lampOn 0 → 0.25）：太阳直晒的地方基本看不出，屋檐下、树荫里透出一点暖光
    //     （直晒处怎么压掉的见 TideToon.shader 的 dayFade、TideGlow.shader 的 GlowDim）
    //   · 色温只加了 1：石板和礁石的底色偏橙，暖光再叠后期色温会发红发脏（11-12 踩过）
    // 改之前的数：sunFrom.y 0.95、skySunY 0.80、sunColor (255,241,216)、shadowStrength 0.72、ambientSky (196,216,246)、
    //   skyHorizon (226,236,238)、skySun (255,246,220)、contrast 13、temperature 3、splitShadows (0.48,0.49,0.53)、
    //   splitHighlights (0.52,0.51,0.48)、lampOn 0、toonShadow (0.80,0.86,1.06)。黄昏那套（Dusk）没动。
    public static TownLook Day()
    {
        return new TownLook
        {
            sunFrom = new Vector3(0.50f, 0.56f, -0.60f),
            skySunY = 0.60f,
            sunColor = new Color32(255, 234, 198, 255),
            sunIntensity = 2.0f,
            shadowStrength = 0.82f,
            ambientSky = new Color32(188, 212, 250, 255),
            ambientEquator = new Color32(234, 224, 208, 255),
            ambientGround = new Color32(132, 114, 94, 255),
            ambientIntensity = 0.62f,
            skyTop = new Color32(64, 134, 224, 255),
            skyMid = new Color32(130, 190, 240, 255),
            skyHorizon = new Color32(234, 236, 230, 255),
            skySun = new Color32(255, 244, 214, 255),
            cloudLight = new Color32(255, 255, 250, 255),
            cloudShade = new Color32(188, 200, 226, 255),
            cloudCover = 0.42f,
            moonDir = new Vector3(0.42f, -0.35f, 0.78f),
            stars = 0f,
            fogColor = new Color32(206, 226, 238, 255),
            fogStart = 150f,
            fogEnd = 700f,
            waterSky = new Color32(206, 230, 244, 255),
            waterSkyReflect = 0.28f,
            exposure = 0.05f,
            contrast = 14f,
            saturation = 15f,
            temperature = 4f,
            tint = 0f,
            splitShadows = new Color(0.47f, 0.49f, 0.55f, 1f),
            splitHighlights = new Color(0.53f, 0.51f, 0.47f, 1f),
            splitBalance = 0f,
            bloomThreshold = 1.0f,
            bloomIntensity = 0.25f,
            bloomScatter = 0.68f,
            bloomTint = Color.white,
            vignette = 0.15f,
            lampOn = 0.25f,
            toonShadow = new Color(0.76f, 0.83f, 1.08f, 1f),
            toonHaze = new Color32(176, 208, 236, 255),
        };
    }

    public static TownLook Dusk()
    {
        return new TownLook
        {
            sunFrom = new Vector3(0.62f, 0.50f, -0.64f),
            skySunY = 0.14f,
            sunColor = new Color32(255, 210, 164, 255),
            sunIntensity = 2.3f,
            shadowStrength = 0.82f,
            ambientSky = new Color32(170, 156, 222, 255),
            ambientEquator = new Color32(234, 190, 170, 255),
            ambientGround = new Color32(124, 88, 92, 255),
            ambientIntensity = 0.62f,
            skyTop = new Color32(58, 70, 152, 255),
            skyMid = new Color32(158, 120, 190, 255),
            skyHorizon = new Color32(255, 178, 132, 255),
            skySun = new Color32(255, 220, 150, 255),
            cloudLight = new Color32(255, 186, 190, 255),
            cloudShade = new Color32(150, 108, 178, 255),
            cloudCover = 0.48f,
            moonDir = new Vector3(0.42f, 0.46f, 0.78f),
            stars = 0.8f,
            fogColor = new Color32(238, 176, 160, 255),
            fogStart = 150f,
            fogEnd = 700f,
            waterSky = new Color32(255, 178, 158, 255),
            waterSkyReflect = 0.32f,
            exposure = 0.05f,
            contrast = 10f,
            saturation = 10f,
            temperature = 2f,
            tint = 0f,
            splitShadows = new Color(0.46f, 0.45f, 0.57f, 1f),
            splitHighlights = new Color(0.60f, 0.52f, 0.42f, 1f),
            splitBalance = -8f,
            bloomThreshold = 0.95f,
            bloomIntensity = 0.5f,
            bloomScatter = 0.72f,
            bloomTint = new Color32(255, 226, 196, 255),
            vignette = 0.2f,
            lampOn = 1f,
            toonShadow = new Color(0.86f, 0.80f, 1.08f, 1f),
            toonHaze = new Color32(226, 170, 176, 255),
        };
    }

    static readonly FieldInfo[] Fields = typeof(TownLook).GetFields(BindingFlags.Public | BindingFlags.Instance);

    // 两套之间逐项插值（只有 float / Color / Vector3 三种字段）
    public static void Lerp(TownLook a, TownLook b, float t, TownLook into)
    {
        foreach (var f in Fields)
        {
            if (f.FieldType == typeof(float)) f.SetValue(into, Mathf.Lerp((float)f.GetValue(a), (float)f.GetValue(b), t));
            else if (f.FieldType == typeof(Color)) f.SetValue(into, Color.Lerp((Color)f.GetValue(a), (Color)f.GetValue(b), t));
            else if (f.FieldType == typeof(Vector3)) f.SetValue(into, Vector3.Lerp((Vector3)f.GetValue(a), (Vector3)f.GetValue(b), t));
        }
    }
}

[Serializable]
public sealed class TownLookSet
{
    public TownLook day = TownLook.Day();
    public TownLook dusk = TownLook.Dusk();
    public float shadowDistance = 75f;
    public int shadowCascades = 4;
    // 卡通材质的手感：受光面 / 背光面亮度、明暗交界的位置和软硬、边缘光、空气感、法线贴图参与多少
    public float toonLit = 0.54f;
    public float toonShade = 1.18f;
    public float toonEdge = 0.50f;
    public float toonSoft = 0.035f;
    public float toonRim = 0.55f;
    public float toonHazeStrength = 0.10f;
    public float toonNormal = 0.7f;
    public float wind = 1f;              // 风力：0 草木不动，1 微风
    public int lampLights = 6;
    public float lampIntensity = 3.2f;
    public float duskSeconds = 45f;      // 从白天完整走到黄昏要多久
    public float afternoon = 0.3f;       // 面包店送完以后往黄昏走多少

    // 2026-11-15 PL-13 窗灯：窗玻璃跟着路灯一起亮的颜色和强度（白天乘 lampOn 0.25，黄昏全亮；0 = 窗不亮）
    public Color windowGlow = new Color(1.0f, 0.74f, 0.40f, 1f);
    public float windowGlowStrength = 1.5f;
    // 2026-11-15 PL-16 民居屋顶换色的力度（0 = 全镇保持原色，1 = 完全换成调色板的颜色）；-noroof 启动等于 0
    public float roofVariety = 1f;
    // 2026-11-15 PL-14 海水三档颜色：浅滩（看得到底，清透青绿）、近海、外海（偏灰的深蓝）。
    // 放在这里运行时设：IslandBuilder.WaterMat() 每次重建场景都会把 IslandWater.mat 的浅 / 深两个颜色写回旧值。
    public Color waterShallow = new Color(0.27f, 0.66f, 0.66f, 1f);
    public Color waterDeep = new Color(0.09f, 0.30f, 0.53f, 1f);
    public Color waterFar = new Color(0.15f, 0.29f, 0.43f, 1f);
    public float waterDepthRange = 3.2f;

    public static TownLookSet Load()
    {
        var set = new TownLookSet();
        try
        {
            string path = Path.Combine(Application.dataPath, "..", "look.json");
            if (File.Exists(path))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), set);
                Debug.Log("TOWNLOOK 读取 look.json");
            }
        }
        catch (Exception e) { Debug.LogWarning("TOWNLOOK look.json 读取失败：" + e.Message); }
        return set;
    }
}

public sealed class TownLighting : MonoBehaviour
{
    static TownLighting instance;
    static bool duskRequested;     // SkyDimmer 在这里装好之前就要求过黄昏（读档）

    // 2026-11-16 C4 渲染轻量档：WebGL、Intel 核显、AMD APU 核显（"Radeon(TM) 610M/660M/680M/780M"、"AMD Radeon(TM) Graphics" 这类）
    // 或命令行 -lowgfx 时为 true；-highgfx 强制普通档。轻量档：同时亮的路灯点光 6 → 4，阴影距离收到七成。
    // 别的脚本（画质守卫、菜单）只读这个值，想手动切换就在 TownLighting 启动前设 LowTierOverride。
    public static bool? LowTierOverride;
    static bool? lowTier;
    public static bool LowTier
    {
        get
        {
            if (LowTierOverride.HasValue) return LowTierOverride.Value;
            if (lowTier.HasValue) return lowTier.Value;
            var args = Environment.GetCommandLineArgs();
            bool low;
            if (Array.IndexOf(args, "-highgfx") >= 0) low = false;
            else if (Array.IndexOf(args, "-lowgfx") >= 0 || Application.platform == RuntimePlatform.WebGLPlayer) low = true;
            else
            {
                string gpu = SystemInfo.graphicsDeviceName ?? "";
                bool intel = gpu.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0;
                bool apu = gpu.IndexOf("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) >= 0
                    || System.Text.RegularExpressions.Regex.IsMatch(gpu, @"Radeon\(TM\)\s*\d{3}M", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    || gpu.IndexOf("Vega", StringComparison.OrdinalIgnoreCase) >= 0 && gpu.IndexOf("Graphics", StringComparison.OrdinalIgnoreCase) >= 0;
                low = intel || apu;
            }
            lowTier = low;
            return low;
        }
    }

    TownLookSet set;
    readonly TownLook current = new TownLook();
    float phase;                   // 0 白天 … 1 黄昏
    float phaseTarget;
    bool frozen;                   // 交给 SkyDimmer 以后不再动
    float nextPoll;
    float bornAt;

    Light sun;
    Material sky;
    Material water;
    ColorAdjustments fxColor;
    WhiteBalance fxWhite;
    SplitToning fxSplit;
    Bloom fxBloom;
    Vignette fxVignette;
    LampGlowDirector lamps;
    GameBootstrap boot;

    Color lastSky, lastEq, lastGround;
    float lastIntensity = -1f;
    ReflectionProbe probe;
    float probeDirtyAt = -1f;

    public static float Phase => instance != null ? instance.phase : -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; duskRequested = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindObjectOfType<IslandWorld>() == null) return;      // 只管小镇主场景
        if (FindObjectOfType<TownLighting>() != null) return;
        new GameObject("TownLighting").AddComponent<TownLighting>();
    }

    // SkyDimmer 要接手天色（灯塔点灯、读档直接落到夜里 / 黎明）之前调：立刻落到黄昏，之后这里不再改任何东西
    public static void HandOverAtDusk()
    {
        duskRequested = true;
        if (instance == null || instance.frozen) return;
        instance.phase = instance.phaseTarget = 1f;
        instance.ApplyLook();
        instance.frozen = true;
    }

    void Awake()
    {
        instance = this;
        bornAt = Time.unscaledTime;
        set = TownLookSet.Load();

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;

        var skyShader = Resources.Load<Shader>("TideSky");
        if (skyShader != null)
        {
            sky = new Material(skyShader);
            sky.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = sky;
        }
        else Debug.LogWarning("TOWNLOOK 找不到 Resources/TideSky，天空保持原样");

        // 太阳：场景里有两盏叫 Sun 的平行光（生成器一盏、EggboyEnv 一盏），只留一盏，另一盏关掉
        foreach (var l in FindObjectsOfType<Light>())
        {
            if (l.type != LightType.Directional || l.name != "Sun") continue;
            if (sun == null) sun = l;
            else l.gameObject.SetActive(false);
        }
        if (sun != null)
        {
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.6f;
            sun.shadowNormalBias = 0.6f;
        }
        // 2026-11-16 C4：核显 / WebGL 轻量档（LowTier 说明见下面）阴影距离收到七成
        ApplyShadowRange(LowTier ? set.shadowDistance * 0.7f : set.shadowDistance, set.shadowCascades);

        var sea = GameObject.Find("Sea");
        if (sea != null && sea.GetComponent<Renderer>() != null) water = sea.GetComponent<Renderer>().material;
        if (water != null)
        {
            water.SetColor("_ShallowColor", set.waterShallow);
            water.SetColor("_DeepColor", set.waterDeep);
            water.SetColor("_FarColor", set.waterFar);
            water.SetFloat("_DepthRange", set.waterDepthRange);
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

        BuildVolume();
        foreach (var cam in FindObjectsOfType<Camera>())
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        // 贴图斜着看不发虚：全局强制各向异性过滤
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        Texture.SetGlobalAnisotropicFilteringLimits(8, 16);

        // 塞尔达式卡通材质（-notoon 关掉，回到写实材质，对比用）
        Shader.SetGlobalFloat("_ToonLit", set.toonLit);
        Shader.SetGlobalFloat("_ToonShade", set.toonShade);
        Shader.SetGlobalFloat("_ToonEdge", set.toonEdge);
        Shader.SetGlobalFloat("_ToonSoft", set.toonSoft);
        Shader.SetGlobalFloat("_ToonRim", set.toonRim);
        Shader.SetGlobalFloat("_ToonHaze", set.toonHazeStrength);
        Shader.SetGlobalFloat("_ToonNormal", set.toonNormal);
        TownToon.WindowGlow = set.windowGlow * Mathf.Max(0f, set.windowGlowStrength);
        TownToon.RoofVariety = Mathf.Clamp01(set.roofVariety);
        if (TownToon.Enabled) gameObject.AddComponent<TownToon>();

        // 2026-11-14：经过花丛、草地、水面的反应（痕迹图、涟漪、水花、花瓣）搬到 TideFeel.cs，
        // 取代 11-13 这里的"最近 6 次蹭过"数组和 -pushdemo 假人（验证改用 -feeldemo，见 FeelDemo.cs）
        gameObject.AddComponent<TideFeel>();

        lamps = gameObject.AddComponent<LampGlowDirector>();
        lamps.maxLights = LowTier ? Mathf.Min(4, set.lampLights) : set.lampLights;
        Debug.Log($"TOWNLOOK gfxTier={(LowTier ? "low" : "normal")} gpu=\"{SystemInfo.graphicsDeviceName}\" lampLights={lamps.maxLights}");

        // 命令行 -look day / dusk：截图和调色用，固定在一档不跟进度走
        var args = Environment.GetCommandLineArgs();
        int li = Array.IndexOf(args, "-look");
        if (li >= 0 && li + 1 < args.Length)
        {
            phase = phaseTarget = args[li + 1] == "dusk" ? 1f : 0f;
            ApplyLook();
            frozen = true;
        }
        else
        {
            phase = phaseTarget = duskRequested ? 1f : ProgressTarget();
            ApplyLook();
            if (duskRequested)
            {
                // SkyDimmer 抢在前面按旧天色取过基准：让它按黄昏重取一遍
                frozen = true;
                foreach (var d in FindObjectsOfType<SkyDimmer>()) d.Rebase();
            }
        }
        RefreshAmbient(true);
    }

    // 剧情进度 → 天色：面包店送完往黄昏走一点，学校送完就是黄昏
    float ProgressTarget()
    {
        if (boot == null) boot = FindObjectOfType<GameBootstrap>();
        var s = boot != null ? boot.state : null;
        if (s == null || s.stamps == null) return 0f;
        if (s.stamps.Contains("school") || s.stamps.Contains("lighthouse")) return 1f;
        if (s.stamps.Contains("bakery")) return set.afternoon;
        return 0f;
    }

    void Update()
    {
        if (frozen) return;
        if (Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + 0.5f;
            float target = ProgressTarget();
            // 刚进游戏的头两秒读到的是存档进度：直接落过去，不演过渡
            if (Time.unscaledTime - bornAt < 2f && !Mathf.Approximately(phase, target)) { phase = target; ApplyLook(); }
            phaseTarget = target;
        }
        if (Mathf.Approximately(phase, phaseTarget)) return;
        phase = Mathf.MoveTowards(phase, phaseTarget, Time.deltaTime / Mathf.Max(1f, set.duskSeconds));
        ApplyLook();
    }

    void ApplyLook()
    {
        float t = Mathf.SmoothStep(0f, 1f, phase);
        TownLook.Lerp(set.day, set.dusk, t, current);
        var look = current;

        RenderSettings.ambientSkyColor = look.ambientSky;
        RenderSettings.ambientEquatorColor = look.ambientEquator;
        RenderSettings.ambientGroundColor = look.ambientGround;
        RenderSettings.ambientIntensity = look.ambientIntensity;
        RenderSettings.fogColor = look.fogColor;
        RenderSettings.fogStartDistance = look.fogStart;
        RenderSettings.fogEndDistance = look.fogEnd;

        Vector3 sunFrom = look.sunFrom.normalized;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(-sunFrom);
            sun.color = look.sunColor;
            sun.intensity = look.sunIntensity;
            sun.shadowStrength = look.shadowStrength;
        }
        if (sky != null)
        {
            sky.SetColor("_TopColor", look.skyTop);
            sky.SetColor("_MidColor", look.skyMid);
            sky.SetColor("_HorizonColor", look.skyHorizon);
            sky.SetColor("_SunColor", look.skySun);
            sky.SetVector("_SunDir", new Vector3(sunFrom.x, look.skySunY, sunFrom.z).normalized);
            sky.SetColor("_CloudLight", look.cloudLight);
            sky.SetColor("_CloudShade", look.cloudShade);
            sky.SetFloat("_CloudCover", look.cloudCover);
            sky.SetVector("_MoonDir", look.moonDir.normalized);
            sky.SetFloat("_Stars", look.stars);
        }
        if (water != null)
        {
            water.SetColor("_SkyColor", look.waterSky);
            water.SetFloat("_SkyReflect", look.waterSkyReflect);
        }

        if (fxColor != null)
        {
            fxColor.postExposure.Override(look.exposure);
            fxColor.contrast.Override(look.contrast);
            fxColor.saturation.Override(look.saturation);
            fxWhite.temperature.Override(look.temperature);
            fxWhite.tint.Override(look.tint);
            fxSplit.shadows.Override(look.splitShadows);
            fxSplit.highlights.Override(look.splitHighlights);
            fxSplit.balance.Override(look.splitBalance);
            bool hdr = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            fxBloom.threshold.Override(hdr ? look.bloomThreshold : look.bloomThreshold - 0.15f);
            fxBloom.intensity.Override(look.bloomIntensity);
            fxBloom.scatter.Override(look.bloomScatter);
            fxBloom.tint.Override(look.bloomTint);
            fxVignette.intensity.Override(look.vignette);
        }

        Shader.SetGlobalColor("_ToonShadowTint", look.toonShadow);
        Shader.SetGlobalColor("_ToonHazeColor", look.toonHaze);
        Shader.SetGlobalFloat("_TideLampOn", look.lampOn);
        // 白天的点光比按比例算的再压一档（0.25 → 0.14 倍）：有的灯紧贴着浅色石墙，按比例算会在墙上打出一块白斑。黄昏（1）不变。
        if (lamps != null) lamps.lightIntensity = set.lampIntensity * look.lampOn * Mathf.Lerp(0.4f, 1f, look.lampOn);
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

    Transform hero;
    float heroLookAt;

    void LateUpdate()
    {
        // 植被和海岸着色器要的三个全局量：风力、海面高度（湿痕跟着潮水走）、主角脚下的位置（草被踩倒）
        Shader.SetGlobalFloat("_TideWind", set.wind);
        Shader.SetGlobalFloat("_TideWaterY", WaterLevel.Main != null ? WaterLevel.Main.Y : 0f);
        if (hero == null && Time.unscaledTime >= heroLookAt)
        {
            heroLookAt = Time.unscaledTime + 1f;
            var h = GameObject.Find("EGGBOY");
            if (h != null) hero = h.transform;
        }
        Vector3 heroPos = hero != null ? hero.position : Vector3.zero;
        bool heroOn = hero != null;
        Shader.SetGlobalVector("_TidePlayerPos", heroOn ? new Vector4(heroPos.x, heroPos.y, heroPos.z, 1f) : Vector4.zero);

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

    // 环境光颜色一变就重算球谐；和上次渲反射时比天色变了一截，1 秒后重渲一次反射（过渡到黄昏、灯塔变暗、天亮）
    float probeBase = float.NaN;
    void RefreshAmbient(bool force)
    {
        var skyC = RenderSettings.ambientSkyColor;
        var eq = RenderSettings.ambientEquatorColor;
        var gr = RenderSettings.ambientGroundColor;
        float k = RenderSettings.ambientIntensity;
        if (!force && skyC == lastSky && eq == lastEq && gr == lastGround && Mathf.Approximately(k, lastIntensity)) return;
        lastSky = skyC; lastEq = eq; lastGround = gr; lastIntensity = k;

        // 三色环境光 → L2 球谐：赤道色做底，天空色从上方补，地面色从下方补
        var sh = new SphericalHarmonicsL2();
        sh.AddAmbientLight(eq.linear * k);
        sh.AddDirectionalLight(Vector3.up, (skyC.linear - eq.linear) * k * 1.6f, 1f);
        sh.AddDirectionalLight(Vector3.down, (gr.linear - eq.linear) * k * 1.6f, 1f);
        RenderSettings.ambientProbe = sh;

        // 慢慢过渡时每帧只变一点点，所以跟"上次渲反射时"比，不跟上一帧比
        float mark = (skyC.b - skyC.r) + skyC.grayscale * k;
        if (float.IsNaN(probeBase)) probeBase = mark;
        if (!force && Mathf.Abs(mark - probeBase) > 0.06f && probe != null && probeDirtyAt < 0f)
        {
            probeBase = mark;
            probeDirtyAt = Time.unscaledTime + 1f;
        }
    }

    void BuildVolume()
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

        fxColor = profile.Add<ColorAdjustments>(true);
        fxColor.colorFilter.Override(Color.white);
        fxWhite = profile.Add<WhiteBalance>(true);
        fxSplit = profile.Add<SplitToning>(true);
        fxBloom = profile.Add<Bloom>(true);
        fxVignette = profile.Add<Vignette>(true);
        fxVignette.smoothness.Override(0.45f);
    }
}
