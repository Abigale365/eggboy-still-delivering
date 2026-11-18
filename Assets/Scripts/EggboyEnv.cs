using UnityEngine;
using UnityEngine.Rendering;

public static class EggboyEnv
{
	public static Light Apply(Transform parent)
	{
		Camera main = Camera.main;
		if (main != null)
		{
			Shader shader = Resources.Load<Shader>("EggboySky");
			if (shader != null)
			{
				Material material = new Material(shader);
				material.SetColor("_TopColor", new Color32(101, 164, 224, byte.MaxValue));
				material.SetColor("_HorizonColor", new Color32(byte.MaxValue, 207, 152, byte.MaxValue));
				material.SetColor("_SunColor", new Color32(byte.MaxValue, 232, 184, byte.MaxValue));
				material.SetVector("_SunDir", new Vector4(-0.5f, 0.35f, -0.8f, 0f));
				RenderSettings.skybox = material;
				main.clearFlags = CameraClearFlags.Skybox;
			}
			else
			{
				main.clearFlags = CameraClearFlags.Color;
				main.backgroundColor = new Color32(243, 217, 181, byte.MaxValue);
			}
			main.fieldOfView = 44f;
			main.allowHDR = true;
			if (main.GetComponent<EggboyPostFX>() == null)
			{
				main.gameObject.AddComponent<EggboyPostFX>();
			}
		}
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.Linear;
		RenderSettings.fogColor = new Color32(238, 193, 143, byte.MaxValue);
		RenderSettings.fogStartDistance = 80f;
		RenderSettings.fogEndDistance = 240f;
		RenderSettings.ambientMode = AmbientMode.Trilight;
		RenderSettings.ambientSkyColor = new Color32(250, 239, 221, byte.MaxValue);
		RenderSettings.ambientEquatorColor = new Color32(228, 204, 170, byte.MaxValue);
		RenderSettings.ambientGroundColor = new Color32(122, 93, 58, byte.MaxValue);
		RenderSettings.ambientIntensity = 0.72f;
		QualitySettings.shadowDistance = 120f;
		GameObject gameObject = new GameObject("Sun");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		Light light = gameObject.AddComponent<Light>();
		light.type = LightType.Directional;
		light.color = new Color32(byte.MaxValue, 235, 205, byte.MaxValue);
		light.intensity = 1.55f;
		light.shadows = LightShadows.Soft;
		light.shadowStrength = 0.72f;
		gameObject.transform.rotation = Quaternion.LookRotation(new Vector3(12f, -10f, -7f));
		return light;
	}
}
