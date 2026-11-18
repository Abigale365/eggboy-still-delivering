using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class DisplayResolutionGuard
{
	internal enum Tier
	{
		Low,
		Medium,
		High
	}

	internal struct FrameBudget
	{
		private float seconds;

		private int frames;

		public bool Observe(float elapsed)
		{
			seconds += elapsed;
			frames++;
			if (seconds < 4f)
			{
				return false;
			}
			bool result = (float)frames / seconds < 48f;
			this = default(FrameBudget);
			return result;
		}
	}

	private const int FullW = 1920;

	private const int FullH = 1080;

	private const int ChromeMargin = 90;

	private const float ObservationSeconds = 4f;

	private const float MinimumFps = 48f;

	private static readonly string[] IntegratedNames = new string[4] { "610M", "UHD", "Iris", "Vega" };

	private static bool smaaOff;

	private static bool automatic;

	private static Tier tier;

	private static Camera configuredCamera;

	private static Vector2Int expectedWindow;

	internal static bool CanAdapt
	{
		get
		{
			if (automatic)
			{
				return tier != Tier.Low;
			}
			return false;
		}
	}

	private static bool HasArg(string[] args, string name)
	{
		return Array.Exists(args, (string arg) => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
	}

	private static bool ResolutionGivenOnCommandLine(string[] args)
	{
		return Array.Exists(args, (string arg) => arg.StartsWith("-screen-", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-popupwindow", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-window-mode", StringComparison.OrdinalIgnoreCase));
	}

	public static Vector2Int PickWindowSize(int displayWidth, int displayHeight)
	{
		if (displayWidth < 1920 || displayHeight < 1080)
		{
			return new Vector2Int(1280, 720);
		}
		if (displayHeight < 1170)
		{
			return new Vector2Int(1600, 900);
		}
		return new Vector2Int(1920, 1080);
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void FitWindow()
	{
		expectedWindow = Vector2Int.zero;
		if (Application.isEditor || Application.isBatchMode || Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
		{
			return;
		}
		if (ResolutionGivenOnCommandLine(Environment.GetCommandLineArgs()))
		{
			Debug.Log("RESGUARD window source=command-line");
			return;
		}
		Display main = Display.main;
		expectedWindow = PickWindowSize(main.systemWidth, main.systemHeight);
		Debug.Log($"RESGUARD window source=display native={main.systemWidth}x{main.systemHeight} " + $"selected={expectedWindow.x}x{expectedWindow.y} mode={Screen.fullScreenMode}");
		if (Screen.fullScreenMode != FullScreenMode.Windowed)
		{
			expectedWindow = Vector2Int.zero;
		}
		else if (Screen.width != expectedWindow.x || Screen.height != expectedWindow.y)
		{
			Screen.SetResolution(expectedWindow.x, expectedWindow.y, FullScreenMode.Windowed);
		}
	}

	internal static Tier AutomaticTier(string deviceName, bool web)
	{
		if (web)
		{
			return Tier.Low;
		}
		if (string.IsNullOrEmpty(deviceName))
		{
			return Tier.High;
		}
		string[] integratedNames = IntegratedNames;
		foreach (string value in integratedNames)
		{
			if (deviceName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return Tier.Medium;
			}
		}
		return Tier.High;
	}

	private static bool TryReadTier(string[] args, out Tier result)
	{
		result = Tier.High;
		if (HasArg(args, "-trailerRecord") && !FilmingStageJumper.AuditArguments())
		{
			return true;
		}
		int num = Array.FindIndex(args, (string a) => string.Equals(a, "-quality", StringComparison.OrdinalIgnoreCase));
		if (num < 0)
		{
			return false;
		}
		switch ((num + 1 < args.Length) ? args[num + 1].ToLowerInvariant() : "")
		{
		case "high":
			return true;
		case "medium":
			result = Tier.Medium;
			return true;
		case "low":
			result = Tier.Low;
			return true;
		default:
			Debug.LogWarning("RESGUARD 无法识别画质档位。可用 high、medium、low，将采用自动设置。");
			return false;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void HookQuality()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		bool flag = TryReadTier(commandLineArgs, out tier);
		bool flag2 = HasArg(commandLineArgs, "-shot_aa");
		automatic = !flag && !flag2;
		if (automatic)
		{
			tier = AutomaticTier(SystemInfo.graphicsDeviceName, Application.platform == RuntimePlatform.WebGLPlayer);
		}
		smaaOff = HasArg(commandLineArgs, "-nosmaa");
		configuredCamera = null;
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneLoaded += OnSceneLoaded;
		RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
		RenderPipelineManager.beginCameraRendering += OnBeginCamera;
		ApplyQuality(flag ? "command-line" : (flag2 ? "capture" : "hardware"));
		bool flag3 = HasArg(commandLineArgs, "-qualitycheck");
		if (automatic || flag3)
		{
			DisplayQualityMonitor displayQualityMonitor = new GameObject("DisplayQuality").AddComponent<DisplayQualityMonitor>();
			UnityEngine.Object.DontDestroyOnLoad(displayQualityMonitor.gameObject);
			displayQualityMonitor.Check = flag3;
		}
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		configuredCamera = null;
		ApplyQuality("scene");
	}

	private static void OnBeginCamera(ScriptableRenderContext context, Camera cam)
	{
		if (!(cam == configuredCamera) && cam.cameraType == CameraType.Game && cam.CompareTag("MainCamera"))
		{
			ApplyCamera(cam);
		}
	}

	private static void ApplyQuality(string reason)
	{
		if (tier != Tier.High && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset universalRenderPipelineAsset)
		{
			float a = ((tier == Tier.Medium) ? 52f : 32f);
			TownLighting.ApplyShadowRange(cascades: Mathf.Min((tier != Tier.Medium) ? 1 : 2, universalRenderPipelineAsset.shadowCascadeCount), distance: Mathf.Min(a, universalRenderPipelineAsset.shadowDistance));
			universalRenderPipelineAsset.maxAdditionalLightsCount = Mathf.Min(universalRenderPipelineAsset.maxAdditionalLightsCount, (tier == Tier.Medium) ? 4 : 2);
		}
		ApplyCamera(Camera.main);
		Debug.Log($"RESGUARD quality={tier} reason={reason} gpu={SystemInfo.graphicsDeviceName}");
	}

	private static void ApplyCamera(Camera cam)
	{
		if (!(cam == null))
		{
			UniversalAdditionalCameraData universalAdditionalCameraData = cam.GetUniversalAdditionalCameraData();
			bool flag = !smaaOff && tier != Tier.Low;
			universalAdditionalCameraData.antialiasing = (flag ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None);
			if (flag)
			{
				universalAdditionalCameraData.antialiasingQuality = ((tier == Tier.High) ? AntialiasingQuality.High : AntialiasingQuality.Low);
				universalAdditionalCameraData.renderPostProcessing = true;
			}
			else if (tier == Tier.Low)
			{
				cam.allowMSAA = true;
			}
			configuredCamera = cam;
		}
	}

	internal static void StepDown()
	{
		if (CanAdapt)
		{
			tier = ((tier == Tier.High) ? Tier.Medium : Tier.Low);
			ApplyQuality("fps<48/4s");
		}
	}

	internal static IEnumerator CheckOnStartup()
	{
		yield return null;
		yield return null;
		bool flag = PickWindowSize(1366, 768) == new Vector2Int(1280, 720) && PickWindowSize(1920, 1080) == new Vector2Int(1600, 900) && PickWindowSize(2560, 1440) == new Vector2Int(1920, 1080);
		bool flag2 = AutomaticTier("AMD Radeon 610M", web: false) == Tier.Medium && AutomaticTier("Intel Iris Xe", web: false) == Tier.Medium && AutomaticTier("NVIDIA RTX", web: true) == Tier.Low && AutomaticTier("NVIDIA RTX", web: false) == Tier.High;
		FrameBudget frameBudget = default(FrameBudget);
		bool flag3 = false;
		for (int i = 0; i < 119; i++)
		{
			flag3 |= frameBudget.Observe(1f / 30f);
		}
		bool flag4 = frameBudget.Observe(0.04f);
		bool flag5 = expectedWindow == Vector2Int.zero || expectedWindow == new Vector2Int(Screen.width, Screen.height);
		bool flag6 = flag && flag2 && !flag3 && flag4 && flag5;
		Debug.Log(string.Format("RESGUARD selfcheck {0} windows={1} ", flag6 ? "PASS" : "FAIL", flag) + $"hardware={flag2} slowFrames={!flag3 && flag4} applied={flag5} actual={Screen.width}x{Screen.height}");
		Application.Quit((!flag6) ? 2 : 0);
	}
}
