// 启动时选择窗口大小，按显卡和持续帧率选择主相机抗锯齿与阴影预算。
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class DisplayResolutionGuard
{
    internal enum Tier { Low, Medium, High }

    const int FullW = 1920, FullH = 1080;
    const int ChromeMargin = 90;
    const float ObservationSeconds = 4f, MinimumFps = 48f;
    static readonly string[] IntegratedNames = { "610M", "UHD", "Iris", "Vega" };

    static bool smaaOff, automatic;
    static Tier tier;
    static Camera configuredCamera;
    static Vector2Int expectedWindow;

    internal struct FrameBudget
    {
        float seconds;
        int frames;

        public bool Observe(float elapsed)
        {
            seconds += elapsed;
            frames++;
            if (seconds < ObservationSeconds) return false;
            bool slow = frames / seconds < MinimumFps;
            this = default;
            return slow;
        }
    }

    static bool HasArg(string[] args, string name) =>
        Array.Exists(args, arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    static bool ResolutionGivenOnCommandLine(string[] args) => Array.Exists(args, arg =>
        arg.StartsWith("-screen-", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "-popupwindow", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "-window-mode", StringComparison.OrdinalIgnoreCase));

    // 纯计算入口，分辨率自检与正常启动共用。
    public static Vector2Int PickWindowSize(int displayWidth, int displayHeight)
    {
        if (displayWidth < FullW || displayHeight < FullH) return new Vector2Int(1280, 720);
        if (displayHeight < FullH + ChromeMargin) return new Vector2Int(1600, 900);
        return new Vector2Int(FullW, FullH);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void FitWindow()
    {
        expectedWindow = Vector2Int.zero;
        if (Application.isEditor || Application.isBatchMode || Application.isMobilePlatform ||
            Application.platform == RuntimePlatform.WebGLPlayer) return;

        var args = Environment.GetCommandLineArgs();
        if (ResolutionGivenOnCommandLine(args))
        {
            Debug.Log("RESGUARD window source=command-line");
            return;
        }

        var display = Display.main;
        expectedWindow = PickWindowSize(display.systemWidth, display.systemHeight);
        Debug.Log($"RESGUARD window source=display native={display.systemWidth}x{display.systemHeight} " +
                  $"selected={expectedWindow.x}x{expectedWindow.y} mode={Screen.fullScreenMode}");
        if (Screen.fullScreenMode != FullScreenMode.Windowed) { expectedWindow = Vector2Int.zero; return; }
        if (Screen.width != expectedWindow.x || Screen.height != expectedWindow.y)
            Screen.SetResolution(expectedWindow.x, expectedWindow.y, FullScreenMode.Windowed);
    }

    internal static Tier AutomaticTier(string deviceName, bool web)
    {
        if (web) return Tier.Low;
        if (string.IsNullOrEmpty(deviceName)) return Tier.High;
        foreach (var name in IntegratedNames)
            if (deviceName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return Tier.Medium;
        return Tier.High;
    }

    static bool TryReadTier(string[] args, out Tier result)
    {
        result = Tier.High;
        if (HasArg(args, "-trailerRecord") && !FilmingStageJumper.AuditArguments()) return true;
        int index = Array.FindIndex(args, a => string.Equals(a, "-quality", StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        switch (index + 1 < args.Length ? args[index + 1].ToLowerInvariant() : "")
        {
            case "high": return true;
            case "medium": result = Tier.Medium; return true;
            case "low": result = Tier.Low; return true;
            default: Debug.LogWarning("RESGUARD 无法识别画质档位。可用 high、medium、low，将采用自动设置。"); return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookQuality()
    {
        var args = Environment.GetCommandLineArgs();
        bool manual = TryReadTier(args, out tier);
        bool photograph = HasArg(args, "-shot_aa");
        automatic = !manual && !photograph;
        if (automatic) tier = AutomaticTier(SystemInfo.graphicsDeviceName, Application.platform == RuntimePlatform.WebGLPlayer);
        smaaOff = HasArg(args, "-nosmaa");
        configuredCamera = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        ApplyQuality(manual ? "command-line" : photograph ? "capture" : "hardware");

        bool check = HasArg(args, "-qualitycheck");
        if (automatic || check)
        {
            var monitor = new GameObject("DisplayQuality").AddComponent<DisplayQualityMonitor>();
            UnityEngine.Object.DontDestroyOnLoad(monitor.gameObject);
            monitor.Check = check;
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        configuredCamera = null;
        ApplyQuality("scene");
    }

    static void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == configuredCamera || cam.cameraType != CameraType.Game || !cam.CompareTag("MainCamera")) return;
        ApplyCamera(cam);
    }

    static void ApplyQuality(string reason)
    {
        if (tier != Tier.High && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            float limit = tier == Tier.Medium ? 52f : 32f;
            int cascades = tier == Tier.Medium ? 2 : 1;
            TownLighting.ApplyShadowRange(Mathf.Min(limit, pipeline.shadowDistance),
                Mathf.Min(cascades, pipeline.shadowCascadeCount));
            pipeline.maxAdditionalLightsCount = Mathf.Min(pipeline.maxAdditionalLightsCount, tier == Tier.Medium ? 4 : 2);
        }
        ApplyCamera(Camera.main);
        Debug.Log($"RESGUARD quality={tier} reason={reason} gpu={SystemInfo.graphicsDeviceName}");
    }

    static void ApplyCamera(Camera cam)
    {
        if (cam == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        bool smaa = !smaaOff && tier != Tier.Low;
        data.antialiasing = smaa ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
        if (smaa)
        {
            data.antialiasingQuality = tier == Tier.High ? AntialiasingQuality.High : AntialiasingQuality.Low;
            data.renderPostProcessing = true;
        }
        else if (tier == Tier.Low) cam.allowMSAA = true;
        configuredCamera = cam;
    }

    internal static bool CanAdapt => automatic && tier != Tier.Low;

    internal static void StepDown()
    {
        if (!CanAdapt) return;
        tier = tier == Tier.High ? Tier.Medium : Tier.Low;
        ApplyQuality("fps<48/4s");
    }

    internal static IEnumerator CheckOnStartup()
    {
        yield return null;
        yield return null;
        bool windowCases = PickWindowSize(1366, 768) == new Vector2Int(1280, 720)
            && PickWindowSize(1920, 1080) == new Vector2Int(1600, 900)
            && PickWindowSize(2560, 1440) == new Vector2Int(1920, 1080);
        bool hardwareCases = AutomaticTier("AMD Radeon 610M", false) == Tier.Medium
            && AutomaticTier("Intel Iris Xe", false) == Tier.Medium
            && AutomaticTier("NVIDIA RTX", true) == Tier.Low
            && AutomaticTier("NVIDIA RTX", false) == Tier.High;
        var budget = new FrameBudget();
        bool early = false;
        for (int i = 0; i < 119; i++) early |= budget.Observe(1f / 30f);
        bool slow = budget.Observe(.04f);
        bool windowApplied = expectedWindow == Vector2Int.zero || expectedWindow == new Vector2Int(Screen.width, Screen.height);
        bool passed = windowCases && hardwareCases && !early && slow && windowApplied;
        Debug.Log($"RESGUARD selfcheck {(passed ? "PASS" : "FAIL")} windows={windowCases} " +
                  $"hardware={hardwareCases} slowFrames={!early && slow} applied={windowApplied} actual={Screen.width}x{Screen.height}");
        Application.Quit(passed ? 0 : 2);
    }
}

internal sealed class DisplayQualityMonitor : MonoBehaviour
{
    public bool Check;
    DisplayResolutionGuard.FrameBudget frameBudget;
    float warmedFor;

    IEnumerator Start()
    {
        if (Check) yield return DisplayResolutionGuard.CheckOnStartup();
    }

    void Update()
    {
        if (!DisplayResolutionGuard.CanAdapt || Time.timeScale <= 0f || !Application.isFocused)
        {
            frameBudget = default;
            return;
        }
        warmedFor += Time.unscaledDeltaTime;
        if (warmedFor < 2f) return;
        if (frameBudget.Observe(Time.unscaledDeltaTime)) DisplayResolutionGuard.StepDown();
    }
}

