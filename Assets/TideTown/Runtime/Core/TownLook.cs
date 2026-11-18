using System;
using System.Reflection;
using UnityEngine;

[Serializable]
public sealed class TownLook
{
	public Vector3 sunFrom;

	public float skySunY;

	public Color sunColor;

	public float sunIntensity;

	public float shadowStrength;

	public Color ambientSky;

	public Color ambientEquator;

	public Color ambientGround;

	public float ambientIntensity;

	public Color skyTop;

	public Color skyMid;

	public Color skyHorizon;

	public Color skySun;

	public Color cloudLight;

	public Color cloudShade;

	public Color skyLow;

	public Color skyHorizonAnti;

	public float cloudCover;

	public Vector3 moonDir;

	public float stars;

	public Color fogColor;

	public float fogStart;

	public float fogEnd;

	public Color waterSky;

	public float waterSkyReflect;

	public float exposure;

	public float contrast;

	public float saturation;

	public float temperature;

	public float tint;

	public Color splitShadows;

	public Color splitHighlights;

	public float splitBalance;

	public float bloomThreshold;

	public float bloomIntensity;

	public float bloomScatter;

	public Color bloomTint;

	public float vignette;

	public float lampOn;

	public Color toonShadow;

	public Color toonHaze;

	private static readonly FieldInfo[] Fields = typeof(TownLook).GetFields(BindingFlags.Instance | BindingFlags.Public);

	public static TownLook Day()
	{
		return new TownLook
		{
			sunFrom = new Vector3(0.5f, 0.56f, -0.6f),
			skySunY = 0.6f,
			sunColor = new Color32(byte.MaxValue, 234, 198, byte.MaxValue),
			sunIntensity = 2f,
			shadowStrength = 0.82f,
			ambientSky = new Color32(188, 212, 250, byte.MaxValue),
			ambientEquator = new Color32(234, 224, 208, byte.MaxValue),
			ambientGround = new Color32(132, 114, 94, byte.MaxValue),
			ambientIntensity = 0.62f,
			skyTop = new Color32(46, 112, 206, byte.MaxValue),
			skyMid = new Color32(104, 168, 232, byte.MaxValue),
			skyLow = new Color32(158, 204, 240, byte.MaxValue),
			skyHorizon = new Color32(226, 238, 242, byte.MaxValue),
			skyHorizonAnti = new Color32(206, 228, 246, byte.MaxValue),
			skySun = new Color32(byte.MaxValue, 240, 206, byte.MaxValue),
			cloudLight = new Color32(byte.MaxValue, 254, 248, byte.MaxValue),
			cloudShade = new Color32(176, 194, 226, byte.MaxValue),
			cloudCover = 0.5f,
			moonDir = new Vector3(0.42f, -0.35f, 0.78f),
			stars = 0f,
			fogColor = new Color32(216, 234, 246, byte.MaxValue),
			fogStart = 150f,
			fogEnd = 700f,
			waterSky = new Color32(206, 230, 244, byte.MaxValue),
			waterSkyReflect = 0.4f,
			exposure = 0.05f,
			contrast = 14f,
			saturation = 15f,
			temperature = 4f,
			tint = 0f,
			splitShadows = new Color(0.47f, 0.49f, 0.55f, 1f),
			splitHighlights = new Color(0.53f, 0.51f, 0.47f, 1f),
			splitBalance = 0f,
			bloomThreshold = 1f,
			bloomIntensity = 0.25f,
			bloomScatter = 0.68f,
			bloomTint = Color.white,
			vignette = 0.15f,
			lampOn = 0.25f,
			toonShadow = new Color(0.76f, 0.83f, 1.08f, 1f),
			toonHaze = new Color32(176, 208, 236, byte.MaxValue)
		};
	}

	public static TownLook Dusk()
	{
		return new TownLook
		{
			sunFrom = new Vector3(0.62f, 0.5f, -0.64f),
			skySunY = 0.14f,
			sunColor = new Color32(byte.MaxValue, 210, 164, byte.MaxValue),
			sunIntensity = 2.3f,
			shadowStrength = 0.82f,
			ambientSky = new Color32(170, 156, 222, byte.MaxValue),
			ambientEquator = new Color32(234, 190, 170, byte.MaxValue),
			ambientGround = new Color32(124, 88, 92, byte.MaxValue),
			ambientIntensity = 0.62f,
			skyTop = new Color32(52, 62, 146, byte.MaxValue),
			skyMid = new Color32(150, 116, 188, byte.MaxValue),
			skyLow = new Color32(246, 152, 146, byte.MaxValue),
			skyHorizon = new Color32(byte.MaxValue, 190, 122, byte.MaxValue),
			skyHorizonAnti = new Color32(222, 152, 172, byte.MaxValue),
			skySun = new Color32(byte.MaxValue, 214, 140, byte.MaxValue),
			cloudLight = new Color32(byte.MaxValue, 190, 168, byte.MaxValue),
			cloudShade = new Color32(140, 104, 172, byte.MaxValue),
			cloudCover = 0.5f,
			moonDir = new Vector3(0.42f, 0.46f, 0.78f),
			stars = 0.8f,
			fogColor = new Color32(250, 184, 150, byte.MaxValue),
			fogStart = 150f,
			fogEnd = 700f,
			waterSky = new Color32(byte.MaxValue, 178, 158, byte.MaxValue),
			waterSkyReflect = 0.66f,
			exposure = 0.05f,
			contrast = 10f,
			saturation = 10f,
			temperature = 2f,
			tint = 0f,
			splitShadows = new Color(0.46f, 0.45f, 0.57f, 1f),
			splitHighlights = new Color(0.6f, 0.52f, 0.42f, 1f),
			splitBalance = -8f,
			bloomThreshold = 0.95f,
			bloomIntensity = 0.5f,
			bloomScatter = 0.72f,
			bloomTint = new Color32(byte.MaxValue, 226, 196, byte.MaxValue),
			vignette = 0.2f,
			lampOn = 1f,
			toonShadow = new Color(0.86f, 0.8f, 1.08f, 1f),
			toonHaze = new Color32(226, 170, 176, byte.MaxValue)
		};
	}

	public static void Lerp(TownLook a, TownLook b, float t, TownLook into)
	{
		FieldInfo[] fields = Fields;
		foreach (FieldInfo fieldInfo in fields)
		{
			if (fieldInfo.FieldType == typeof(float))
			{
				fieldInfo.SetValue(into, Mathf.Lerp((float)fieldInfo.GetValue(a), (float)fieldInfo.GetValue(b), t));
			}
			else if (fieldInfo.FieldType == typeof(Color))
			{
				fieldInfo.SetValue(into, Color.Lerp((Color)fieldInfo.GetValue(a), (Color)fieldInfo.GetValue(b), t));
			}
			else if (fieldInfo.FieldType == typeof(Vector3))
			{
				fieldInfo.SetValue(into, Vector3.Lerp((Vector3)fieldInfo.GetValue(a), (Vector3)fieldInfo.GetValue(b), t));
			}
		}
	}
}
