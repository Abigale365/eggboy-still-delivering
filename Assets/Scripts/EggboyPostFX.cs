using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class EggboyPostFX : MonoBehaviour
{
	private Material mat;

	private void Start()
	{
		Configure();
	}

	public void Configure()
	{
		if (GraphicsSettings.currentRenderPipeline == null)
		{
			return;
		}
		Camera camera = GetComponent<Camera>();
		if (camera == null)
		{
			camera = Camera.main;
		}
		if (camera != null)
		{
			camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
		}
		VolumeProfile volumeProfile = Resources.Load<VolumeProfile>("EggboyURPProfile");
		if (volumeProfile == null)
		{
			Debug.LogError("找不到 Resources/EggboyURPProfile");
			return;
		}
		Volume volume = base.gameObject.GetComponent<Volume>();
		if (volume == null)
		{
			volume = base.gameObject.AddComponent<Volume>();
		}
		volume.isGlobal = true;
		volume.priority = 1f;
		volume.sharedProfile = volumeProfile;
		volumeProfile = volume.profile;
		volumeProfile.TryGet<Bloom>(out var component);
		volumeProfile.TryGet<Tonemapping>(out var component2);
		volumeProfile.TryGet<ColorAdjustments>(out var component3);
		volumeProfile.TryGet<Vignette>(out var component4);
		ApplyCalibration(component, component2, component3, component4);
		if (!volumeProfile.TryGet<WhiteBalance>(out var component5))
		{
			component5 = volumeProfile.Add<WhiteBalance>(overrides: true);
		}
		component5.temperature.Override(6f);
		component5.tint.Override(2f);
	}

	private static void ApplyCalibration(Bloom bloom, Tonemapping tone, ColorAdjustments color, Vignette vignette)
	{
		bool flag = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
		_ = QualitySettings.activeColorSpace;
		if (bloom != null)
		{
			bloom.threshold.Override(flag ? 1.05f : 0.8f);
			bloom.intensity.Override(flag ? 0.22f : 0.14f);
			bloom.scatter.Override(0.7f);
		}
		if (tone != null)
		{
			tone.mode.Override(TonemappingMode.ACES);
		}
		if (color != null)
		{
			color.postExposure.Override(0.15f);
			color.saturation.Override(8f);
			color.contrast.Override(5f);
			color.colorFilter.Override(new Color(1f, 0.995f, 0.98f));
		}
		if (vignette != null)
		{
			vignette.intensity.Override(0.16f);
			vignette.smoothness.Override(0.4f);
		}
	}

	private void OnRenderImage(RenderTexture src, RenderTexture dst)
	{
		if (GraphicsSettings.currentRenderPipeline != null)
		{
			Graphics.Blit(src, dst);
			return;
		}
		if (mat == null)
		{
			Shader shader = Resources.Load<Shader>("EggboyPost");
			if (shader == null)
			{
				Graphics.Blit(src, dst);
				return;
			}
			mat = new Material(shader);
		}
		mat.SetFloat("_Bloom", 0.35f);
		mat.SetFloat("_Threshold", 1.05f);
		mat.SetFloat("_Vignette", 0.4f);
		mat.SetFloat("_Warm", 0f);
		mat.SetFloat("_Saturation", 1.12f);
		mat.SetFloat("_Contrast", 1.04f);
		Graphics.Blit(src, dst, mat);
	}
}
