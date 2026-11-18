using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class TownLighting : MonoBehaviour
{
	private static TownLighting instance;

	private static bool duskRequested;

	public static bool? LowTierOverride;

	private static bool? lowTier;

	private TownLookSet set;

	private readonly TownLook current = new TownLook();

	private float phase;

	private float phaseTarget;

	private bool frozen;

	private float nextPoll;

	private float bornAt;

	private Light sun;

	private Material sky;

	private Material water;

	private ColorAdjustments fxColor;

	private WhiteBalance fxWhite;

	private SplitToning fxSplit;

	private Bloom fxBloom;

	private Vignette fxVignette;

	private LampGlowDirector lamps;

	private GameBootstrap boot;

	private Color lastSky;

	private Color lastEq;

	private Color lastGround;

	private float lastIntensity = -1f;

	private ReflectionProbe probe;

	private float probeDirtyAt = -1f;

	private Transform hero;

	private float heroLookAt;

	private bool heroDone;

	private float probeBase = float.NaN;

	public static void SetFilmingPhase(float value)
	{
		if (instance != null)
		{
			instance.frozen = true;
			instance.phase = (instance.phaseTarget = Mathf.Clamp01(value));
			instance.ApplyLook();
		}
	}

	public static bool LowTier
	{
		get
		{
			if (LowTierOverride.HasValue)
			{
				return LowTierOverride.Value;
			}
			if (lowTier.HasValue)
			{
				return lowTier.Value;
			}
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			bool flag;
			if (Array.IndexOf(commandLineArgs, "-highgfx") >= 0)
			{
				flag = false;
			}
			else if (Array.IndexOf(commandLineArgs, "-lowgfx") >= 0 || Application.platform == RuntimePlatform.WebGLPlayer)
			{
				flag = true;
			}
			else
			{
				string text = SystemInfo.graphicsDeviceName ?? "";
				bool num = text.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0;
				bool flag2 = text.IndexOf("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) >= 0 || Regex.IsMatch(text, "Radeon\\(TM\\)\\s*\\d{3}M", RegexOptions.IgnoreCase) || (text.IndexOf("Vega", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("Graphics", StringComparison.OrdinalIgnoreCase) >= 0);
				flag = num || flag2;
			}
			lowTier = flag;
			return flag;
		}
	}

	public static float Phase
	{
		get
		{
			if (!(instance != null))
			{
				return -1f;
			}
			return instance.phase;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics()
	{
		instance = null;
		duskRequested = false;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Install()
	{
		MainSceneGate.WhenMainLoaded(InstallNow);
	}

	private static void InstallNow()
	{
		if (!(UnityEngine.Object.FindObjectOfType<IslandWorld>() == null) && !(UnityEngine.Object.FindObjectOfType<TownLighting>() != null))
		{
			new GameObject("TownLighting").AddComponent<TownLighting>();
		}
	}

	public static void HandOverAtDusk()
	{
		duskRequested = true;
		if (!(instance == null) && !instance.frozen)
		{
			instance.phase = (instance.phaseTarget = 1f);
			instance.ApplyLook();
			instance.frozen = true;
		}
	}

	private void Awake()
	{
		instance = this;
		bornAt = Time.unscaledTime;
		set = TownLookSet.Load();
		RenderSettings.ambientMode = AmbientMode.Trilight;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.Linear;
		Shader shader = Resources.Load<Shader>("TideSky");
		if (shader != null)
		{
			sky = new Material(shader);
			sky.SetFloat("_Exposure", 1f);
			RenderSettings.skybox = sky;
		}
		else
		{
			Debug.LogWarning("TOWNLOOK 找不到 Resources/TideSky，天空保持原样");
		}
		Light[] array = UnityEngine.Object.FindObjectsOfType<Light>();
		foreach (Light light in array)
		{
			if (light.type == LightType.Directional && !(light.name != "Sun"))
			{
				if (sun == null)
				{
					sun = light;
				}
				else
				{
					light.gameObject.SetActive(value: false);
				}
			}
		}
		if (sun != null)
		{
			sun.shadows = LightShadows.Soft;
			sun.shadowBias = 0.6f;
			sun.shadowNormalBias = 0.6f;
		}
		ApplyShadowRange(LowTier ? (set.shadowDistance * 0.7f) : set.shadowDistance, set.shadowCascades);
		GameObject gameObject = GameObject.Find("Sea");
		if (gameObject != null && gameObject.GetComponent<Renderer>() != null)
		{
			water = gameObject.GetComponent<Renderer>().material;
		}
		if (water != null)
		{
			water.SetColor("_ShallowColor", set.waterShallow);
			water.SetColor("_DeepColor", set.waterDeep);
			water.SetColor("_FarColor", set.waterFar);
			water.SetFloat("_DepthRange", set.waterDepthRange);
		}
		GameObject gameObject2 = new GameObject("TownReflection");
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject2.transform.position = new Vector3(0f, 6f, 0f);
		probe = gameObject2.AddComponent<ReflectionProbe>();
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
		Camera[] array2 = UnityEngine.Object.FindObjectsOfType<Camera>();
		foreach (Camera obj in array2)
		{
			obj.clearFlags = CameraClearFlags.Skybox;
			obj.GetUniversalAdditionalCameraData().renderPostProcessing = true;
		}
		QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
		Texture.SetGlobalAnisotropicFilteringLimits(8, 16);
		Shader.SetGlobalFloat("_ToonLit", set.toonLit);
		Shader.SetGlobalFloat("_ToonShade", set.toonShade);
		Shader.SetGlobalFloat("_ToonEdge", set.toonEdge);
		Shader.SetGlobalFloat("_ToonSoft", set.toonSoft);
		Shader.SetGlobalFloat("_ToonRim", set.toonRim);
		Shader.SetGlobalFloat("_ToonHaze", set.toonHazeStrength);
		Shader.SetGlobalFloat("_ToonNormal", set.toonNormal);
		TownToon.WindowGlow = set.windowGlow * Mathf.Max(0f, set.windowGlowStrength);
		TownToon.RoofVariety = Mathf.Clamp01(set.roofVariety);
		if (TownToon.Enabled)
		{
			base.gameObject.AddComponent<TownToon>();
		}
		base.gameObject.AddComponent<TideFeel>();
		base.gameObject.AddComponent<ShoreWaves>();
		lamps = base.gameObject.AddComponent<LampGlowDirector>();
		lamps.maxLights = (LowTier ? Mathf.Min(4, set.lampLights) : set.lampLights);
		Debug.Log(string.Format("TOWNLOOK gfxTier={0} gpu=\"{1}\" lampLights={2}", LowTier ? "low" : "normal", SystemInfo.graphicsDeviceName, lamps.maxLights));
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-look");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			phase = (phaseTarget = ((commandLineArgs[num + 1] == "dusk") ? 1f : 0f));
			ApplyLook();
			frozen = true;
		}
		else
		{
			phase = (phaseTarget = (duskRequested ? 1f : ProgressTarget()));
			ApplyLook();
			if (duskRequested)
			{
				frozen = true;
				SkyDimmer[] array3 = UnityEngine.Object.FindObjectsOfType<SkyDimmer>();
				for (int i = 0; i < array3.Length; i++)
				{
					array3[i].Rebase();
				}
			}
		}
		RefreshAmbient(force: true);
	}

	private float ProgressTarget()
	{
		if (boot == null)
		{
			boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
		}
		GameState gameState = ((boot != null) ? boot.state : null);
		if (gameState == null || gameState.stamps == null)
		{
			return 0f;
		}
		if (gameState.stamps.Contains("school") || gameState.stamps.Contains("lighthouse"))
		{
			return 1f;
		}
		if (gameState.stamps.Contains("bakery"))
		{
			return set.afternoon;
		}
		return 0f;
	}

	private void Update()
	{
		if (frozen)
		{
			return;
		}
		if (Time.unscaledTime >= nextPoll)
		{
			nextPoll = Time.unscaledTime + 0.5f;
			float b = ProgressTarget();
			if (Time.unscaledTime - bornAt < 2f && !Mathf.Approximately(phase, b))
			{
				phase = b;
				ApplyLook();
			}
			phaseTarget = b;
		}
		if (!Mathf.Approximately(phase, phaseTarget))
		{
			phase = Mathf.MoveTowards(phase, phaseTarget, Time.deltaTime / Mathf.Max(1f, set.duskSeconds));
			ApplyLook();
		}
	}

	private void ApplyLook()
	{
		float t = Mathf.SmoothStep(0f, 1f, phase);
		TownLook.Lerp(set.day, set.dusk, t, current);
		TownLook townLook = current;
		RenderSettings.ambientSkyColor = townLook.ambientSky;
		RenderSettings.ambientEquatorColor = townLook.ambientEquator;
		RenderSettings.ambientGroundColor = townLook.ambientGround;
		RenderSettings.ambientIntensity = townLook.ambientIntensity;
		RenderSettings.fogColor = townLook.fogColor;
		RenderSettings.fogStartDistance = townLook.fogStart;
		RenderSettings.fogEndDistance = townLook.fogEnd;
		Vector3 normalized = townLook.sunFrom.normalized;
		if (sun != null)
		{
			sun.transform.rotation = Quaternion.LookRotation(-normalized);
			sun.color = townLook.sunColor;
			sun.intensity = townLook.sunIntensity;
			sun.shadowStrength = townLook.shadowStrength;
		}
		if (sky != null)
		{
			sky.SetColor("_TopColor", townLook.skyTop);
			sky.SetColor("_MidColor", townLook.skyMid);
			sky.SetColor("_LowColor", townLook.skyLow);
			sky.SetColor("_HorizonColor", townLook.skyHorizon);
			sky.SetColor("_HorizonAnti", townLook.skyHorizonAnti);
			sky.SetColor("_SunColor", townLook.skySun);
			sky.SetVector("_SunDir", new Vector3(normalized.x, townLook.skySunY, normalized.z).normalized);
			sky.SetColor("_CloudLight", townLook.cloudLight);
			sky.SetColor("_CloudShade", townLook.cloudShade);
			sky.SetFloat("_CloudCover", townLook.cloudCover);
			sky.SetVector("_MoonDir", townLook.moonDir.normalized);
			sky.SetFloat("_Stars", townLook.stars);
		}
		if (water != null)
		{
			water.SetColor("_SkyColor", townLook.waterSky);
			water.SetFloat("_SkyReflect", townLook.waterSkyReflect);
		}
		Shader.SetGlobalColor("_TideSkyTop", townLook.skyTop);
		Shader.SetGlobalColor("_TideSkyMid", townLook.skyMid);
		Shader.SetGlobalColor("_TideSkyLow", townLook.skyLow);
		Shader.SetGlobalColor("_TideSkyHorizon", townLook.skyHorizon);
		Shader.SetGlobalColor("_TideSkyHorizonAnti", townLook.skyHorizonAnti);
		Shader.SetGlobalColor("_TideSkySun", townLook.skySun);
		Shader.SetGlobalColor("_TideCloudLight", townLook.cloudLight);
		Shader.SetGlobalVector("_TideSkySunDir", new Vector3(normalized.x, townLook.skySunY, normalized.z).normalized);
		if (fxColor != null)
		{
			fxColor.postExposure.Override(townLook.exposure);
			fxColor.contrast.Override(townLook.contrast);
			fxColor.saturation.Override(townLook.saturation);
			fxWhite.temperature.Override(townLook.temperature);
			fxWhite.tint.Override(townLook.tint);
			fxSplit.shadows.Override(townLook.splitShadows);
			fxSplit.highlights.Override(townLook.splitHighlights);
			fxSplit.balance.Override(townLook.splitBalance);
			bool flag = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
			fxBloom.threshold.Override(flag ? townLook.bloomThreshold : (townLook.bloomThreshold - 0.15f));
			fxBloom.intensity.Override(townLook.bloomIntensity);
			fxBloom.scatter.Override(townLook.bloomScatter);
			fxBloom.tint.Override(townLook.bloomTint);
			fxVignette.intensity.Override(townLook.vignette);
		}
		Shader.SetGlobalColor("_ToonShadowTint", townLook.toonShadow);
		Shader.SetGlobalColor("_ToonHazeColor", townLook.toonHaze);
		Shader.SetGlobalFloat("_TideLampOn", townLook.lampOn);
		if (lamps != null)
		{
			lamps.lightIntensity = set.lampIntensity * townLook.lampOn * Mathf.Lerp(0.4f, 1f, townLook.lampOn);
		}
	}

	public static void ApplyShadowRange(float distance, int cascades)
	{
		UniversalRenderPipelineAsset universalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
		if (!(universalRenderPipelineAsset == null))
		{
			universalRenderPipelineAsset.shadowDistance = distance;
			universalRenderPipelineAsset.shadowCascadeCount = Mathf.Clamp(cascades, 1, 4);
			QualitySettings.shadowDistance = distance;
		}
	}

	private void LateUpdate()
	{
		Shader.SetGlobalFloat("_TideWind", set.wind);
		Shader.SetGlobalFloat("_TideWaterY", (WaterLevel.Main != null) ? WaterLevel.Main.Y : 0f);
		Material skybox = RenderSettings.skybox;
		Shader.SetGlobalFloat("_TideSkyExposure", (skybox != null && skybox.HasProperty("_Exposure")) ? skybox.GetFloat("_Exposure") : 1f);
		if (hero == null && Time.unscaledTime >= heroLookAt)
		{
			heroLookAt = Time.unscaledTime + 1f;
			GameObject gameObject = GameObject.Find("EGGBOY");
			if (gameObject != null)
			{
				hero = gameObject.transform;
			}
		}
		Vector3 vector = ((hero != null) ? hero.position : Vector3.zero);
		bool flag = hero != null;
		Shader.SetGlobalVector("_TidePlayerPos", flag ? new Vector4(vector.x, vector.y, vector.z, 1f) : Vector4.zero);
		RefreshAmbient(force: false);
		if (!heroDone)
		{
			ExcludeHeroFromReflections();
		}
		if (probeDirtyAt >= 0f && Time.unscaledTime >= probeDirtyAt)
		{
			probeDirtyAt = -1f;
			if (probe != null)
			{
				probe.RenderProbe();
			}
		}
	}

	private void ExcludeHeroFromReflections()
	{
		GameObject gameObject = GameObject.Find("EGGBOY");
		if (!(gameObject == null))
		{
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
			}
			heroDone = true;
		}
	}

	private void RefreshAmbient(bool force)
	{
		Color ambientSkyColor = RenderSettings.ambientSkyColor;
		Color ambientEquatorColor = RenderSettings.ambientEquatorColor;
		Color ambientGroundColor = RenderSettings.ambientGroundColor;
		float ambientIntensity = RenderSettings.ambientIntensity;
		if (force || !(ambientSkyColor == lastSky) || !(ambientEquatorColor == lastEq) || !(ambientGroundColor == lastGround) || !Mathf.Approximately(ambientIntensity, lastIntensity))
		{
			lastSky = ambientSkyColor;
			lastEq = ambientEquatorColor;
			lastGround = ambientGroundColor;
			lastIntensity = ambientIntensity;
			SphericalHarmonicsL2 ambientProbe = default(SphericalHarmonicsL2);
			ambientProbe.AddAmbientLight(ambientEquatorColor.linear * ambientIntensity);
			ambientProbe.AddDirectionalLight(Vector3.up, (ambientSkyColor.linear - ambientEquatorColor.linear) * ambientIntensity * 1.6f, 1f);
			ambientProbe.AddDirectionalLight(Vector3.down, (ambientGroundColor.linear - ambientEquatorColor.linear) * ambientIntensity * 1.6f, 1f);
			RenderSettings.ambientProbe = ambientProbe;
			float num = ambientSkyColor.b - ambientSkyColor.r + ambientSkyColor.grayscale * ambientIntensity;
			if (float.IsNaN(probeBase))
			{
				probeBase = num;
			}
			if (!force && Mathf.Abs(num - probeBase) > 0.06f && probe != null && probeDirtyAt < 0f)
			{
				probeBase = num;
				probeDirtyAt = Time.unscaledTime + 1f;
			}
		}
	}

	private void BuildVolume()
	{
		GameObject obj = new GameObject("TownPostFX");
		obj.transform.SetParent(base.transform, worldPositionStays: false);
		Volume volume = obj.AddComponent<Volume>();
		volume.isGlobal = true;
		volume.priority = 5f;
		VolumeProfile volumeProfile = (volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>());
		volumeProfile.Add<Tonemapping>(overrides: true).mode.Override(TonemappingMode.Neutral);
		fxColor = volumeProfile.Add<ColorAdjustments>(overrides: true);
		fxColor.colorFilter.Override(Color.white);
		fxWhite = volumeProfile.Add<WhiteBalance>(overrides: true);
		fxSplit = volumeProfile.Add<SplitToning>(overrides: true);
		fxBloom = volumeProfile.Add<Bloom>(overrides: true);
		fxVignette = volumeProfile.Add<Vignette>(overrides: true);
		fxVignette.smoothness.Override(0.45f);
	}
}
