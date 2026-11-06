using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 相机后期：URP 下用全局 Volume（Bloom / ACES / 色彩 / 暗角），
// Built-in 下保留原来的 OnRenderImage 路径作为回退。
public class EggboyPostFX : MonoBehaviour
{
    Material mat;

    void Start()
    {
        Configure();
    }

    public void Configure()
    {
        if (GraphicsSettings.currentRenderPipeline == null) return;

        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
        }

        var profile = Resources.Load<VolumeProfile>("EggboyURPProfile");
        if (profile == null)
        {
            Debug.LogError("找不到 Resources/EggboyURPProfile");
            return;
        }

        var volume = gameObject.GetComponent<Volume>();
        if (volume == null) volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;
        profile = volume.profile;

        Bloom bloom;
        Tonemapping tone;
        ColorAdjustments color;
        Vignette vignette;
        profile.TryGet(out bloom);
        profile.TryGet(out tone);
        profile.TryGet(out color);
        profile.TryGet(out vignette);
        ApplyCalibration(bloom, tone, color, vignette);
        if (!profile.TryGet<WhiteBalance>(out var white)) white = profile.Add<WhiteBalance>(true);
        white.temperature.Override(6f);
        white.tint.Override(2f);
    }

    // WebGL 没有 HDR 缓冲时，8 位画面到不了 1.05 的 Bloom 阈值；
    // Gamma 色彩空间下再补一点饱和与对比，Windows 继续走 HDR 参数。
    static void ApplyCalibration(Bloom bloom, Tonemapping tone, ColorAdjustments color, Vignette vignette)
    {
        bool hdr = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
        bool gamma = QualitySettings.activeColorSpace == ColorSpace.Gamma;

        if (bloom != null)
        {
            bloom.threshold.Override(hdr ? 1.05f : 0.8f);
            bloom.intensity.Override(hdr ? 0.22f : 0.14f);
            bloom.scatter.Override(0.7f);
        }
        if (tone != null) tone.mode.Override(TonemappingMode.ACES);
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

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        // URP 下 OnRenderImage 不会被调用；保留 Built-in 回退，方便没有 URP 资产时预览
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            Graphics.Blit(src, dst);
            return;
        }
        if (mat == null)
        {
            var sh = Resources.Load<Shader>("EggboyPost");
            if (sh == null)
            {
                Graphics.Blit(src, dst);
                return;
            }
            mat = new Material(sh);
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
