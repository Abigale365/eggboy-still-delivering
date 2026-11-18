using UnityEngine;

public sealed class SkyDimmer : MonoBehaviour
{
	public float seconds = 20f;

	private Light sun;

	private Material sky;

	private float sun0;

	private Color sunColor0;

	private Color skyTint0;

	private Color groundColor0;

	private float exposure0;

	private float ambient0;

	private float atmosphere0 = 1f;

	private float t;

	private bool captured;

	private bool dawning;

	private float dawnT;

	private float dawnSeconds;

	private float fSunI;

	private float fExposure;

	private float fAmbient;

	private float fAtmosphere;

	private Color fSun;

	private Color fSkyTint;

	private Color fGround;

	private Color fAmbSky;

	private Color fAmbEq;

	private Color ambSky0;

	private Color ambEq0;

	public bool Lit { get; private set; }

	public bool Dawned { get; private set; }

	public bool Dimmed
	{
		get
		{
			if (Lit)
			{
				return t >= seconds;
			}
			return false;
		}
	}

	public float Progress
	{
		get
		{
			if (!(seconds > 0f))
			{
				return 1f;
			}
			return Mathf.Clamp01(t / seconds);
		}
	}

	public void Dim(float s)
	{
		if (!Lit && !dawning)
		{
			Capture();
			Lit = true;
			seconds = Mathf.Max(0.2f, s);
			t = 0f;
		}
	}

	public void SetDimmedInstant()
	{
		Capture();
		Lit = true;
		t = seconds;
		Apply(1f);
	}

	public void Dawn(float s)
	{
		if (!Dawned)
		{
			Capture();
			Dawned = true;
			Lit = true;
			t = seconds;
			dawning = true;
			dawnT = 0f;
			dawnSeconds = Mathf.Max(0.2f, s);
			fSunI = ((sun != null) ? sun.intensity : 1f);
			fSun = ((sun != null) ? sun.color : Color.white);
			fExposure = ((sky != null && sky.HasProperty("_Exposure")) ? sky.GetFloat("_Exposure") : 1f);
			fSkyTint = ((sky != null && sky.HasProperty("_SkyTint")) ? sky.GetColor("_SkyTint") : Color.white);
			fGround = ((sky != null && sky.HasProperty("_GroundColor")) ? sky.GetColor("_GroundColor") : Color.black);
			fAtmosphere = ((sky != null && sky.HasProperty("_AtmosphereThickness")) ? sky.GetFloat("_AtmosphereThickness") : 1f);
			fAmbient = RenderSettings.ambientIntensity;
			fAmbSky = RenderSettings.ambientSkyColor;
			fAmbEq = RenderSettings.ambientEquatorColor;
		}
	}

	public void SetDawnInstant()
	{
		if (!Dawned)
		{
			Dawn(0.01f);
			dawnT = dawnSeconds;
			ApplyDawn(1f);
			dawning = false;
		}
	}

	private void Capture()
	{
		if (captured)
		{
			return;
		}
		captured = true;
		TownLighting.HandOverAtDusk();
		Light[] array = Object.FindObjectsOfType<Light>();
		foreach (Light light in array)
		{
			if (light.type == LightType.Directional)
			{
				sun = light;
				break;
			}
		}
		if (sun != null)
		{
			sun0 = sun.intensity;
			sunColor0 = sun.color;
		}
		sky = RenderSettings.skybox;
		if (sky != null)
		{
			sky = new Material(sky);
			RenderSettings.skybox = sky;
			if (sky.HasProperty("_Exposure"))
			{
				exposure0 = sky.GetFloat("_Exposure");
			}
			if (sky.HasProperty("_SkyTint"))
			{
				skyTint0 = sky.GetColor("_SkyTint");
			}
			if (sky.HasProperty("_GroundColor"))
			{
				groundColor0 = sky.GetColor("_GroundColor");
			}
			atmosphere0 = (sky.HasProperty("_AtmosphereThickness") ? sky.GetFloat("_AtmosphereThickness") : 1f);
		}
		ambient0 = RenderSettings.ambientIntensity;
		ambSky0 = RenderSettings.ambientSkyColor;
		ambEq0 = RenderSettings.ambientEquatorColor;
	}

	public void Rebase()
	{
		if (captured)
		{
			bool dawned = Dawned;
			captured = false;
			Capture();
			if (dawned)
			{
				Dawned = false;
				dawning = false;
				SetDawnInstant();
			}
			else if (Lit)
			{
				Apply(Mathf.SmoothStep(0f, 1f, Progress));
			}
		}
	}

	private void Update()
	{
		if (dawning)
		{
			dawnT = Mathf.Min(dawnSeconds, dawnT + Time.deltaTime);
			ApplyDawn(Mathf.SmoothStep(0f, 1f, dawnT / dawnSeconds));
			if (dawnT >= dawnSeconds)
			{
				dawning = false;
			}
		}
		else if (Lit && !(t >= seconds))
		{
			t = Mathf.Min(seconds, t + Time.deltaTime);
			Apply(Mathf.SmoothStep(0f, 1f, Progress));
		}
	}

	private void ApplyDawn(float q)
	{
		if (sun != null)
		{
			sun.intensity = Mathf.Lerp(fSunI, sun0 * 0.55f, q);
			sun.color = Color.Lerp(fSun, new Color32(byte.MaxValue, 200, 150, byte.MaxValue), q);
		}
		if (sky != null)
		{
			if (sky.HasProperty("_Exposure"))
			{
				sky.SetFloat("_Exposure", Mathf.Lerp(fExposure, exposure0 * 0.8f, q));
			}
			if (sky.HasProperty("_SkyTint"))
			{
				sky.SetColor("_SkyTint", Color.Lerp(fSkyTint, new Color32(byte.MaxValue, 176, 142, byte.MaxValue), q));
			}
			if (sky.HasProperty("_GroundColor"))
			{
				sky.SetColor("_GroundColor", Color.Lerp(fGround, new Color32(46, 36, 38, byte.MaxValue), q));
			}
			if (sky.HasProperty("_AtmosphereThickness"))
			{
				sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(fAtmosphere, atmosphere0 * 1.5f, q));
			}
		}
		RenderSettings.ambientIntensity = Mathf.Lerp(fAmbient, ambient0 * 0.9f, q);
		RenderSettings.ambientEquatorColor = Color.Lerp(fAmbEq, new Color32(230, 186, 152, byte.MaxValue), q);
		RenderSettings.ambientSkyColor = Color.Lerp(fAmbSky, new Color32(byte.MaxValue, 222, 192, byte.MaxValue), q);
	}

	private void Apply(float p)
	{
		if (sun != null)
		{
			sun.intensity = Mathf.Lerp(sun0, sun0 * 0.18f, p);
			sun.color = Color.Lerp(sunColor0, new Color32(byte.MaxValue, 148, 96, byte.MaxValue), p);
		}
		if (sky != null)
		{
			if (sky.HasProperty("_Exposure"))
			{
				sky.SetFloat("_Exposure", Mathf.Lerp(exposure0, exposure0 * 0.28f, p));
			}
			if (sky.HasProperty("_SkyTint"))
			{
				sky.SetColor("_SkyTint", Color.Lerp(skyTint0, new Color32(92, 86, 140, byte.MaxValue), p));
			}
			if (sky.HasProperty("_GroundColor"))
			{
				sky.SetColor("_GroundColor", Color.Lerp(groundColor0, new Color32(22, 18, 26, byte.MaxValue), p));
			}
			if (sky.HasProperty("_AtmosphereThickness"))
			{
				sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(atmosphere0, atmosphere0 * 2.4f, p));
			}
		}
		RenderSettings.ambientIntensity = Mathf.Lerp(ambient0, ambient0 * 0.6f, p);
		RenderSettings.ambientEquatorColor = Color.Lerp(ambEq0, new Color32(86, 78, 96, byte.MaxValue), p);
		RenderSettings.ambientSkyColor = Color.Lerp(ambSky0, new Color32(58, 66, 106, byte.MaxValue), p);
	}
}
