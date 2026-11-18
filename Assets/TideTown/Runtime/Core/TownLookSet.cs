using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class TownLookSet
{
	public TownLook day = TownLook.Day();

	public TownLook dusk = TownLook.Dusk();

	public float shadowDistance = 75f;

	public int shadowCascades = 4;

	public float toonLit = 0.54f;

	public float toonShade = 1.18f;

	public float toonEdge = 0.5f;

	public float toonSoft = 0.035f;

	public float toonRim = 0.55f;

	public float toonHazeStrength = 0.1f;

	public float toonNormal = 0.7f;

	public float wind = 1f;

	public int lampLights = 6;

	public float lampIntensity = 3.2f;

	public float duskSeconds = 45f;

	public float afternoon = 0.3f;

	public Color windowGlow = new Color(1f, 0.74f, 0.4f, 1f);

	public float windowGlowStrength = 1.5f;

	public float roofVariety = 1f;

	public Color waterShallow = new Color(0.27f, 0.66f, 0.66f, 1f);

	public Color waterDeep = new Color(0.09f, 0.3f, 0.53f, 1f);

	public Color waterFar = new Color(0.15f, 0.29f, 0.43f, 1f);

	public float waterDepthRange = 3.2f;

	public static TownLookSet Load()
	{
		TownLookSet townLookSet = new TownLookSet();
		try
		{
			string path = Path.Combine(Application.dataPath, "..", "look.json");
			if (File.Exists(path))
			{
				JsonUtility.FromJsonOverwrite(File.ReadAllText(path), townLookSet);
				Debug.Log("TOWNLOOK 读取 look.json");
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("TOWNLOOK look.json 读取失败：" + ex.Message);
		}
		return townLookSet;
	}
}
