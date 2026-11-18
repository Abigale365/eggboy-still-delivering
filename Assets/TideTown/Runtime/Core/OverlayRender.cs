using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class OverlayRender
{
	public const int Layer = 30;

	private static Camera overlay;

	private static Camera baseCam;

	private static int users;

	private static int savedMask;

	private static int ownLightingUsers;

	private static bool movedPost;

	private static bool savedBasePost;

	private static readonly List<Light> mutedLights = new List<Light>();

	public static Color SceneKeyColor
	{
		get
		{
			Color color = new Color(1f, 0.92f, 0.84f);
			Light sun = RenderSettings.sun;
			if (sun == null || !sun.isActiveAndEnabled)
			{
				return color;
			}
			return Color.Lerp(color, sun.color, 0.5f);
		}
	}

	public static void Acquire(Camera cam, bool ownLighting)
	{
		if (ownLighting && ownLightingUsers++ == 0)
		{
			MuteSceneLights();
		}
		Acquire(cam);
	}

	public static void Release(bool ownLighting)
	{
		if (ownLighting && ownLightingUsers > 0 && --ownLightingUsers == 0)
		{
			RestoreSceneLights();
		}
		Release();
	}

	private static void MuteSceneLights()
	{
		mutedLights.Clear();
		Light[] array = Object.FindObjectsOfType<Light>();
		foreach (Light light in array)
		{
			if (!(light == null) && light.gameObject.layer != 30 && ((uint)light.cullingMask & 0x40000000u) != 0)
			{
				light.cullingMask &= -1073741825;
				mutedLights.Add(light);
			}
		}
	}

	private static void RestoreSceneLights()
	{
		foreach (Light mutedLight in mutedLights)
		{
			if (mutedLight != null)
			{
				mutedLight.cullingMask |= 1073741824;
			}
		}
		mutedLights.Clear();
	}

	public static void Acquire(Camera cam)
	{
		if (cam == null)
		{
			return;
		}
		users++;
		if (users > 1 && baseCam == cam)
		{
			return;
		}
		baseCam = cam;
		savedMask = cam.cullingMask;
		cam.cullingMask &= -1073741825;
		UniversalAdditionalCameraData universalAdditionalCameraData = cam.GetUniversalAdditionalCameraData();
		if (!(universalAdditionalCameraData == null) && universalAdditionalCameraData.renderType == CameraRenderType.Base)
		{
			if (overlay == null)
			{
				GameObject gameObject = new GameObject("OverlayRenderCam");
				Object.DontDestroyOnLoad(gameObject);
				overlay = gameObject.AddComponent<Camera>();
				overlay.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
			}
			overlay.transform.SetParent(cam.transform, worldPositionStays: false);
			overlay.transform.localPosition = Vector3.zero;
			overlay.transform.localRotation = Quaternion.identity;
			overlay.fieldOfView = cam.fieldOfView;
			overlay.nearClipPlane = 0.03f;
			overlay.farClipPlane = 10f;
			overlay.cullingMask = 1073741824;
			overlay.gameObject.SetActive(value: true);
			UniversalAdditionalCameraData universalAdditionalCameraData2 = overlay.GetUniversalAdditionalCameraData();
			universalAdditionalCameraData2.renderShadows = false;
			savedBasePost = universalAdditionalCameraData.renderPostProcessing;
			movedPost = savedBasePost;
			universalAdditionalCameraData2.renderPostProcessing = savedBasePost;
			universalAdditionalCameraData2.volumeLayerMask = universalAdditionalCameraData.volumeLayerMask;
			universalAdditionalCameraData2.antialiasing = universalAdditionalCameraData.antialiasing;
			universalAdditionalCameraData2.antialiasingQuality = universalAdditionalCameraData.antialiasingQuality;
			if (movedPost)
			{
				universalAdditionalCameraData.renderPostProcessing = false;
			}
			if (!universalAdditionalCameraData.cameraStack.Contains(overlay))
			{
				universalAdditionalCameraData.cameraStack.Add(overlay);
			}
		}
	}

	public static void Release()
	{
		if (users <= 0)
		{
			return;
		}
		users--;
		if (users > 0)
		{
			return;
		}
		if (baseCam != null)
		{
			baseCam.cullingMask = savedMask | baseCam.cullingMask;
			UniversalAdditionalCameraData universalAdditionalCameraData = baseCam.GetUniversalAdditionalCameraData();
			if (universalAdditionalCameraData != null)
			{
				if (overlay != null)
				{
					universalAdditionalCameraData.cameraStack.Remove(overlay);
				}
				if (movedPost)
				{
					universalAdditionalCameraData.renderPostProcessing = savedBasePost;
				}
			}
		}
		movedPost = false;
		if (overlay != null)
		{
			overlay.gameObject.SetActive(value: false);
		}
		baseCam = null;
	}

	public static void SyncFov()
	{
		if (overlay != null && baseCam != null)
		{
			overlay.fieldOfView = baseCam.fieldOfView;
		}
	}

	public static void SetLayerRecursive(GameObject go, int layer)
	{
		Transform[] componentsInChildren = go.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].gameObject.layer = layer;
		}
	}

	public static void LimitLights(GameObject go)
	{
		Light[] componentsInChildren = go.GetComponentsInChildren<Light>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].cullingMask = 1073741824;
		}
	}
}
