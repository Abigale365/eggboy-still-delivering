// 2026-11-07 小镇灯光：主场景原来全是 Unity 默认值——
//   没烘过光照（m_LightingDataAsset 为空），打包后环境光探针是 0，背光面纯黑；
//   没有环境反射，Lux3D 的釉面、黄铜、玻璃什么都反射不到；太阳不投影；没有任何后期。
// 这里在运行时补上：环境光（自己算球谐，跟着 RenderSettings 的颜色变）、一次实时反射探针、
// 太阳软阴影、全局后期。不改任何模型和材质。
// 灯塔点灯和天亮（LighthouseFX）改的是 RenderSettings 的环境光颜色，这里每帧检测到变化就重算。
//
// 2026-11-12 美术改版：气氛照目标图（docs/reference/preview.png）做成"绘本"感，分两套——
//   白天（TownLook.Day）：开局到学校那封信读完。晴朗蓝天、白云、暖白的太阳，路灯不亮。
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

    public static TownLook Day()
    {
        return new TownLook
        {
            sunFrom = new Vector3(0.50f, 0.95f, -0.60f),
            skySunY = 0.80f,
            sunColor = new Color32(255, 241, 216, 255),
            sunIntensity = 2.0f,
            shadowStrength = 0.72f,
            ambientSky = new Color32(196, 216, 246, 255),
            ambientEquator = new Color32(234, 224, 208, 255),
            ambientGround = new Color32(132, 114, 94, 255),
            ambientIntensity = 0.62f,
            skyTop = new Color32(64, 134, 224, 255),
            skyMid = new Color32(130, 190, 240, 255),
            skyHorizon = new Color32(226, 236, 238, 255),
            skySun = new Color32(255, 246, 220, 255),
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
            contrast = 13f,
            saturation = 15f,
            temperature = 3f,
            tint = 0f,
            splitShadows = new Color(0.48f, 0.49f, 0.53f, 1f),
            splitHighlights = new Color(0.52f, 0.51f, 0.48f, 1f),
            splitBalance = 0f,
            bloomThreshold = 1.0f,
            bloomIntensity = 0.25f,
            bloomScatter = 0.68f,
            bloomTint = Color.white,
            vignette = 0.15f,
            lampOn = 0f,
            toonShadow = new Color(0.80f, 0.86f, 1.06f, 1f),
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
        ApplyShadowRange(set.shadowDistance, set.shadowCascades);

        var sea = GameObject.Find("Sea");
        if (sea != null && sea.GetComponent<Renderer>() != null) water = sea.GetComponent<Renderer>().material;

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
        if (TownToon.Enabled) gameObject.AddComponent<TownToon>();

        for (int k = 0; k < pushPos.Length; k++) pushPos[k] = new Vector4(0f, -999f, 0f, -999f);
        Shader.SetGlobalVectorArray("_TidePushPos", pushPos);
        Shader.SetGlobalVectorArray("_TidePushDir", pushDir);
        {
            var a = Environment.GetCommandLineArgs();
            int di = Array.IndexOf(a, "-pushdemo");
            if (di >= 0 && di + 1 < a.Length)
            {
                var parts = a[di + 1].Split(',');
                if (parts.Length == 3
                    && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dx)
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dy)
                    && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dz))
                { demoPush = true; demoCenter = new Vector3(dx, dy, dz); }
            }
        }

        lamps = gameObject.AddComponent<LampGlowDirector>();
        lamps.maxLights = set.lampLights;

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
        if (lamps != null) lamps.lightIntensity = set.lampIntensity * look.lampOn;
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
    readonly Vector4[] pushPos = new Vector4[6];
    readonly Vector4[] pushDir = new Vector4[6];
    int pushNext;
    bool pushStarted;
    Vector3 lastPushPos, lastHeroPos;
    bool demoPush;
    Vector3 demoCenter;

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
        if (demoPush)
        {
            // 命令行 -pushdemo x,y,z：没有人操作时，用一个来回走的"假人"验证花丛的反应
            heroOn = true;
            heroPos = demoCenter + new Vector3(Mathf.PingPong(Time.time * 1.6f, 2.4f) - 1.2f, 0f, 0f);
        }
        Shader.SetGlobalVector("_TidePlayerPos", heroOn ? new Vector4(heroPos.x, heroPos.y, heroPos.z, 1f) : Vector4.zero);
        Shader.SetGlobalFloat("_TideTime", Time.time);
        // 每走出一小段记一次"蹭过"：位置、时刻、前进方向、力度（跑比走更猛）。着色器拿它算枝叶的回弹。
        if (heroOn)
        {
            if (!pushStarted) { pushStarted = true; lastPushPos = heroPos; lastHeroPos = heroPos; }
            Vector3 v = (heroPos - lastHeroPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastHeroPos = heroPos;
            Vector3 moved = heroPos - lastPushPos; moved.y = 0f;
            if (moved.magnitude > 0.32f)
            {
                var dir = moved.normalized;
                float speed = new Vector2(v.x, v.z).magnitude;
                pushPos[pushNext] = new Vector4(lastPushPos.x + moved.x * 0.5f, heroPos.y, lastPushPos.z + moved.z * 0.5f, Time.time);
                pushDir[pushNext] = new Vector4(dir.x, dir.z, Mathf.Clamp(speed / 1.4f, 0.5f, 1.5f), 0f);
                pushNext = (pushNext + 1) % pushPos.Length;
                lastPushPos = heroPos;
                Shader.SetGlobalVectorArray("_TidePushPos", pushPos);
                Shader.SetGlobalVectorArray("_TidePushDir", pushDir);
            }
        }

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
