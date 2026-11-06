// 天空、雾、环境光、太阳和相机后处理；原型世界和场景世界共用。
using UnityEngine;
using UnityEngine.Rendering;

public static class EggboyEnv
{
    public static Light Apply(Transform parent)
    {
        var cam = Camera.main;
        if (cam != null)
        {
            var skyShader = Resources.Load<Shader>("EggboySky");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetColor("_TopColor", new Color32(101, 164, 224, 255));
                sky.SetColor("_HorizonColor", new Color32(255, 207, 152, 255));
                sky.SetColor("_SunColor", new Color32(255, 232, 184, 255));
                sky.SetVector("_SunDir", new Vector4(-0.5f, 0.35f, -0.8f, 0f));
                RenderSettings.skybox = sky;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0xf3, 0xd9, 0xb5, 255);
            }
            cam.fieldOfView = 44f;
            cam.allowHDR = true;
            if (cam.GetComponent<EggboyPostFX>() == null) cam.gameObject.AddComponent<EggboyPostFX>();
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color32(238, 193, 143, 255);
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance = 240f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(250, 239, 221, 255);
        RenderSettings.ambientEquatorColor = new Color32(228, 204, 170, 255);
        RenderSettings.ambientGroundColor = new Color32(122, 93, 58, 255);
        RenderSettings.ambientIntensity = 0.72f;
        QualitySettings.shadowDistance = 42f;

        var go = new GameObject("Sun");
        go.transform.SetParent(parent, false);
        var sun = go.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color32(255, 235, 205, 255);
        sun.intensity = 1.55f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.72f;
        go.transform.rotation = Quaternion.LookRotation(new Vector3(12f, -10f, -7f));
        return sun;
    }
}
