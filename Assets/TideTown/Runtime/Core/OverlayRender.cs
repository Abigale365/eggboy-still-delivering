// 叠加相机：把某些东西（木匣特写、取景框里手上的物件）画在整个场景最上面，镜头前有墙、柱子也挡不住。
// 用 URP 的相机叠加（Overlay camera stack），只画第 30 层。谁用谁 Acquire，用完 Release（有引用计数）。
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class OverlayRender
{
    public const int Layer = 30;
    static Camera overlay, baseCam;
    static int users, savedMask;

    public static void Acquire(Camera cam)
    {
        if (cam == null) return;
        users++;
        if (users > 1 && baseCam == cam) return;
        baseCam = cam;
        savedMask = cam.cullingMask;
        cam.cullingMask &= ~(1 << Layer);
        var baseData = cam.GetUniversalAdditionalCameraData();
        if (baseData == null || baseData.renderType != CameraRenderType.Base) return;
        if (overlay == null)
        {
            var go = new GameObject("OverlayRenderCam");
            Object.DontDestroyOnLoad(go);
            overlay = go.AddComponent<Camera>();
            overlay.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        }
        overlay.transform.SetParent(cam.transform, false);
        overlay.transform.localPosition = Vector3.zero;
        overlay.transform.localRotation = Quaternion.identity;
        overlay.fieldOfView = cam.fieldOfView;
        overlay.nearClipPlane = 0.03f;
        overlay.farClipPlane = 10f;
        overlay.cullingMask = 1 << Layer;
        overlay.gameObject.SetActive(true);
        var od = overlay.GetUniversalAdditionalCameraData();
        od.renderPostProcessing = false;
        od.renderShadows = false;
        if (!baseData.cameraStack.Contains(overlay)) baseData.cameraStack.Add(overlay);
    }

    public static void Release()
    {
        if (users <= 0) return;
        users--;
        if (users > 0) return;
        if (baseCam != null)
        {
            baseCam.cullingMask = savedMask | baseCam.cullingMask;
            var baseData = baseCam.GetUniversalAdditionalCameraData();
            if (baseData != null && overlay != null) baseData.cameraStack.Remove(overlay);
        }
        if (overlay != null) overlay.gameObject.SetActive(false);
        baseCam = null;
    }

    // 叠加相机的视场角要跟着主相机（主相机可能在改 FOV）
    public static void SyncFov()
    {
        if (overlay != null && baseCam != null) overlay.fieldOfView = baseCam.fieldOfView;
    }

    public static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // 灯只照叠加层（不然特写里那盏补光会把整个场景也照亮）
    public static void LimitLights(GameObject go)
    {
        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.cullingMask = 1 << Layer;
    }
}
