using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TownToon : MonoBehaviour
{
	private struct PaneRule
	{
		public string building;

		public string material;

		public float strength;

		public Func<Vector3, bool> inside;
	}

	public static Color WindowGlow = Color.black;

	public static float RoofVariety = 1f;

	private static readonly bool onlyGround = Array.IndexOf(Environment.GetCommandLineArgs(), "-toonground") >= 0;

	private static readonly bool noRoof = Array.IndexOf(Environment.GetCommandLineArgs(), "-noroof") >= 0;

	private const int GroundLayer = 6;

	private Shader toon;

	private readonly Dictionary<Material, Material> cache = new Dictionary<Material, Material>();

	private readonly Dictionary<Material, Material> cacheNoTangent = new Dictionary<Material, Material>();

	private readonly Dictionary<Material, Material> noFade = new Dictionary<Material, Material>();

	private readonly Dictionary<long, Material> roofMats = new Dictionary<long, Material>();

	private readonly Dictionary<Material, Material> glowMats = new Dictionary<Material, Material>();

	private readonly Dictionary<Mesh, Mesh> shutterMeshes = new Dictionary<Mesh, Mesh>();

	private readonly Dictionary<Transform, int> roofPlan = new Dictionary<Transform, int>();

	private readonly HashSet<int> seen = new HashSet<int>();

	private float nextSweep;

	private int converted;

	private int skipped;

	private int roofRenderers;

	private int glassMats;

	private int glassRenderers;

	private static readonly Color[] RoofPalette = new Color[5]
	{
		Color.white,
		new Color32(168, 78, 62, byte.MaxValue),
		new Color32(112, 138, 160, byte.MaxValue),
		new Color32(124, 148, 110, byte.MaxValue),
		new Color32(150, 134, 120, byte.MaxValue)
	};

	private static readonly string[] RoofNames = new string[5] { "原色", "砖红", "雾蓝", "鼠尾草绿", "暖灰瓦" };

	private static readonly PaneRule[] PaneRules = new PaneRule[1]
	{
		new PaneRule
		{
			building = "B6",
			material = "Sage painted wood",
			strength = 0.7f,
			inside = IsTileHouseWindow
		}
	};

	private static readonly string[] CoastalSolidPrefixes = new string[13]
	{
		"upper_seawall", "descent_wall_", "harbor_quay", "school_plinth", "gate_plinth_", "upper_seaward_", "descent_rail_", "descent_sill_", "harbor_pilings", "harbor_rope",
		"lighthouse_sidewall", "lighthouse_rail_", "ramp_sidewall_"
	};

	public static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-notoon") < 0;

	private void Awake()
	{
		toon = Resources.Load<Shader>("TideToon");
		if (toon == null)
		{
			Debug.LogWarning("TOWNTOON 找不到 Resources/TideToon");
			base.enabled = false;
			return;
		}
		Shader.SetGlobalFloat("_ToonDebug", (Array.IndexOf(Environment.GetCommandLineArgs(), "-toondebug") >= 0) ? 1f : 0f);
		if (!noRoof && RoofVariety > 0.001f)
		{
			PlanRoofs();
		}
		Sweep();
		Debug.Log($"TOWNTOON materials={cache.Count + cacheNoTangent.Count} renderers={converted} skipped={skipped} " + $"noFade={noFade.Count} roofMats={roofMats.Count} roofRenderers={roofRenderers} glassMats={glassMats} glassParts={glassRenderers}");
	}

	private void Update()
	{
		if (!(Time.unscaledTime < nextSweep))
		{
			nextSweep = Time.unscaledTime + 1.5f;
			Sweep();
		}
	}

	private void Sweep()
	{
		Renderer[] array = UnityEngine.Object.FindObjectsOfType<Renderer>(includeInactive: true);
		foreach (Renderer renderer in array)
		{
			if (!seen.Add(renderer.GetInstanceID()))
			{
				continue;
			}
			if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
			{
				skipped++;
				continue;
			}
			if (IsHero(renderer.transform))
			{
				skipped++;
				continue;
			}
			if (onlyGround && renderer.gameObject.layer != 6)
			{
				skipped++;
				continue;
			}
			bool tangents = HasTangents(renderer);
			bool flag = renderer.gameObject.layer == 6 || renderer is SkinnedMeshRenderer || IsSolidStructure(renderer.transform);
			int num = ((!flag) ? RoofVariantOf(renderer.transform) : 0);
			string text = ((num > 0) ? RoofMaterialKey(renderer.transform) : null);
			Material[] sharedMaterials = renderer.sharedMaterials;
			if (!flag && sharedMaterials.Length == 1 && LightTileHouseWindows(renderer, sharedMaterials[0], tangents))
			{
				converted++;
				glassRenderers++;
				continue;
			}
			bool flag2 = false;
			bool flag3 = false;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				Material material = sharedMaterials[j];
				Material material2 = Convert(material, tangents) ?? material;
				if (material2 != null && material2.shader == toon)
				{
					if (flag)
					{
						material2 = NoFadeCopy(material2);
					}
					else if (text != null && IsRoofPart(material, renderer, text))
					{
						material2 = RoofCopy(material2, num);
						flag3 = true;
					}
					else if (IsWindowPart(renderer))
					{
						material2 = GlowCopy(material2);
						glassRenderers++;
					}
					else if (IsBakeryArchPart(renderer))
					{
						material2 = GlowCopy(material2, renderer.gameObject.name.StartsWith("Door plank joint", StringComparison.OrdinalIgnoreCase) ? 0.9f : 0.55f);
						glassRenderers++;
					}
				}
				if (material2 != material)
				{
					sharedMaterials[j] = material2;
					flag2 = true;
				}
			}
			if (flag2)
			{
				renderer.sharedMaterials = sharedMaterials;
				converted++;
			}
			if (flag3)
			{
				roofRenderers++;
			}
		}
	}

	private static bool IsHero(Transform t)
	{
		while (t != null)
		{
			if (t.name == "EGGBOY")
			{
				return true;
			}
			t = t.parent;
		}
		return false;
	}

	private static bool HasTangents(Renderer r)
	{
		Mesh mesh = null;
		if (r is SkinnedMeshRenderer skinnedMeshRenderer)
		{
			mesh = skinnedMeshRenderer.sharedMesh;
		}
		else
		{
			MeshFilter component = r.GetComponent<MeshFilter>();
			if (component != null)
			{
				mesh = component.sharedMesh;
			}
		}
		if (mesh != null)
		{
			return mesh.HasVertexAttribute(VertexAttribute.Tangent);
		}
		return false;
	}

	private static bool IsHouseRoot(string n)
	{
		if (n != null && n.EndsWith("_visual", StringComparison.Ordinal))
		{
			if (!n.StartsWith("B6_", StringComparison.Ordinal))
			{
				return n.StartsWith("B7_", StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	private static Transform HouseRootOf(Transform t)
	{
		while (t != null)
		{
			if (IsHouseRoot(t.name))
			{
				return t;
			}
			t = t.parent;
		}
		return null;
	}

	private int RoofVariantOf(Transform t)
	{
		if (roofPlan.Count == 0)
		{
			return 0;
		}
		Transform transform = HouseRootOf(t);
		if (!(transform != null) || !roofPlan.TryGetValue(transform, out var value))
		{
			return 0;
		}
		return value;
	}

	private static string RoofMaterialKey(Transform t)
	{
		Transform transform = HouseRootOf(t);
		if (transform == null)
		{
			return null;
		}
		if (!transform.name.StartsWith("B6_", StringComparison.Ordinal))
		{
			return "Worn slate blue paint";
		}
		return "Old coral enamel";
	}

	private static bool IsRoofPart(Material src, Renderer r, string key)
	{
		if (!(src != null) || !src.name.StartsWith(key, StringComparison.OrdinalIgnoreCase))
		{
			return r.gameObject.name.StartsWith(key, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static int Family(int variant, bool tileHouse)
	{
		if (variant == 0)
		{
			if (!tileHouse)
			{
				return 2;
			}
			return 1;
		}
		return variant;
	}

	private static uint StableHash(string s)
	{
		uint num = 2166136261u;
		foreach (char c in s)
		{
			num ^= c;
			num *= 16777619;
		}
		return num;
	}

	private void PlanRoofs()
	{
		List<Transform> list = new List<Transform>();
		Transform[] array = UnityEngine.Object.FindObjectsOfType<Transform>(includeInactive: true);
		foreach (Transform transform in array)
		{
			if (IsHouseRoot(transform.name))
			{
				list.Add(transform);
			}
		}
		list.Sort((Transform a, Transform b) => string.CompareOrdinal(a.name, b.name));
		List<KeyValuePair<Vector3, int>> list2 = new List<KeyValuePair<Vector3, int>>();
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Transform item in list)
		{
			bool flag = item.name.StartsWith("B6_", StringComparison.Ordinal);
			uint num = StableHash(item.name);
			int[] array2 = ((!flag) ? new int[3] { 1, 3, 4 } : new int[4] { 1, 2, 3, 4 });
			List<int> list3 = new List<int>();
			int num2 = (int)(num % (uint)array2.Length);
			for (int j = 0; j < array2.Length; j++)
			{
				list3.Add(array2[(num2 + j) % array2.Length]);
			}
			if (num / 7 % 5 < 2)
			{
				list3.Insert(0, 0);
			}
			else
			{
				list3.Add(0);
			}
			int num3 = list3[0];
			foreach (int item2 in list3)
			{
				int num4 = Family(item2, flag);
				bool flag2 = false;
				foreach (KeyValuePair<Vector3, int> item3 in list2)
				{
					if (item3.Value == num4 && (item3.Key - item.position).sqrMagnitude < 81f)
					{
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					num3 = item2;
					break;
				}
			}
			roofPlan[item] = num3;
			list2.Add(new KeyValuePair<Vector3, int>(item.position, Family(num3, flag)));
			stringBuilder.Append(item.name.Replace("_visual", "")).Append('=').Append(RoofNames[num3])
				.Append(' ');
		}
		Debug.Log($"TOWNTOON roofs houses={list.Count} {stringBuilder}");
	}

	private Material RoofCopy(Material toonMat, int variant)
	{
		long key = ((long)toonMat.GetInstanceID() << 4) | variant;
		if (roofMats.TryGetValue(key, out var value))
		{
			return value;
		}
		Material material = new Material(toonMat)
		{
			name = toonMat.name + " roof:" + RoofNames[variant]
		};
		if (AverageColor(toonMat.GetTexture("_BaseMap"), out var avg))
		{
			Color color = toonMat.GetColor("_BaseColor");
			material.SetColor("_RoofSrc", new Color(avg.r * color.r, avg.g * color.g, avg.b * color.b, 1f));
		}
		else
		{
			material.SetColor("_RoofSrc", new Color(0.5f, 0.5f, 0.5f, 0f));
		}
		material.SetColor("_RoofTint", RoofPalette[variant]);
		material.SetFloat("_RoofAmount", RoofVariety);
		roofMats[key] = material;
		return material;
	}

	private static bool AverageColor(Texture tex, out Color avg)
	{
		avg = Color.gray;
		if (tex == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
		{
			return false;
		}
		RenderTexture temporary = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
		RenderTexture active = RenderTexture.active;
		Texture2D texture2D = null;
		try
		{
			Graphics.Blit(tex, temporary);
			RenderTexture.active = temporary;
			texture2D = new Texture2D(8, 8, TextureFormat.RGBA32, mipChain: false);
			texture2D.ReadPixels(new Rect(0f, 0f, 8f, 8f), 0, 0);
			Color32[] pixels = texture2D.GetPixels32();
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			Color32[] array = pixels;
			for (int i = 0; i < array.Length; i++)
			{
				Color32 color = array[i];
				num += (float)(int)color.r;
				num2 += (float)(int)color.g;
				num3 += (float)(int)color.b;
			}
			float num4 = 1f / ((float)pixels.Length * 255f);
			avg = new Color(num * num4, num2 * num4, num3 * num4, 1f);
			return avg.r + avg.g + avg.b > 0.06f;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("TOWNTOON 屋顶平均色读取失败：" + ex.Message);
			return false;
		}
		finally
		{
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(temporary);
			if (texture2D != null)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
		}
	}

	private static string BuildingId(Transform t)
	{
		while (t != null)
		{
			string text = t.name;
			if (text.Length > 9 && text[0] == 'B' && char.IsDigit(text[1]) && text[2] == '_' && text.EndsWith("_visual", StringComparison.Ordinal))
			{
				return text.Substring(0, 2);
			}
			t = t.parent;
		}
		return null;
	}

	private static bool IsWindowPart(Renderer r)
	{
		if (WindowGlow.maxColorComponent <= 0.001f)
		{
			return false;
		}
		string text = r.gameObject.name;
		bool flag = text.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;
		bool flag2 = text.Equals("Recess shadow", StringComparison.OrdinalIgnoreCase);
		if (!flag && !flag2)
		{
			return false;
		}
		string text2 = BuildingId(r.transform);
		if (text2 != null)
		{
			if (!flag)
			{
				return text2 == "B2";
			}
			return true;
		}
		return false;
	}

	private static bool IsBakeryArchPart(Renderer r)
	{
		if (WindowGlow.maxColorComponent <= 0.001f)
		{
			return false;
		}
		string text = r.gameObject.name;
		if (text.StartsWith("Deep arched opening", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Door plank joint", StringComparison.OrdinalIgnoreCase))
		{
			return BuildingId(r.transform) == "B2";
		}
		return false;
	}

	private bool LightTileHouseWindows(Renderer renderer, Material source, bool tangents)
	{
		if (WindowGlow.maxColorComponent <= 0.001f || source == null)
		{
			return false;
		}
		string text = BuildingId(renderer.transform);
		PaneRule paneRule = default(PaneRule);
		bool flag = false;
		PaneRule[] paneRules = PaneRules;
		for (int i = 0; i < paneRules.Length; i++)
		{
			PaneRule paneRule2 = paneRules[i];
			if (paneRule2.building == text && source.name.StartsWith(paneRule2.material, StringComparison.OrdinalIgnoreCase))
			{
				paneRule = paneRule2;
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return false;
		}
		MeshFilter component = renderer.GetComponent<MeshFilter>();
		Mesh mesh = ((component != null) ? component.sharedMesh : null);
		if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1)
		{
			return false;
		}
		Material material = Convert(source, tangents) ?? source;
		if (material.shader != toon)
		{
			return false;
		}
		if (!shutterMeshes.TryGetValue(mesh, out var value))
		{
			Vector3[] vertices = mesh.vertices;
			int[] triangles = mesh.GetTriangles(0);
			List<int> list = new List<int>();
			List<int> list2 = new List<int>();
			for (int j = 0; j < triangles.Length; j += 3)
			{
				int num = triangles[j];
				int num2 = triangles[j + 1];
				int num3 = triangles[j + 2];
				List<int> obj = ((paneRule.inside(vertices[num]) && paneRule.inside(vertices[num2]) && paneRule.inside(vertices[num3])) ? list : list2);
				obj.Add(num);
				obj.Add(num2);
				obj.Add(num3);
			}
			if (list.Count == 0 || list2.Count == 0)
			{
				return false;
			}
			value = UnityEngine.Object.Instantiate(mesh);
			value.name = mesh.name + " window panes";
			value.subMeshCount = 2;
			value.SetTriangles(list2, 0, calculateBounds: false);
			value.SetTriangles(list, 1, calculateBounds: false);
			shutterMeshes.Add(mesh, value);
		}
		component.sharedMesh = value;
		renderer.sharedMaterials = new Material[2]
		{
			material,
			GlowCopy(material, paneRule.strength)
		};
		return true;
	}

	private static bool IsTileHouseWindow(Vector3 p)
	{
		bool num = p.z > 1.35f && p.x > 0.48f && p.x < 1.42f && p.y > 0.93f && p.y < 2f;
		bool flag = p.z < -1.38f && Mathf.Abs(p.x) < 0.31f && p.y > 1.51f && p.y < 2.15f;
		bool flag2 = p.x < -2.1f && Mathf.Abs(p.z) < 0.33f && p.y > 1.2f && p.y < 1.86f;
		return num || flag || flag2;
	}

	private Material GlowCopy(Material toonMat, float strength = 1f)
	{
		if (toonMat.GetColor("_WindowGlow").maxColorComponent > 0.001f)
		{
			return toonMat;
		}
		Material material = ((strength >= 0.999f) ? toonMat : null);
		if (material != null && glowMats.TryGetValue(material, out var value))
		{
			return value;
		}
		string text = toonMat.name + " glow" + ((strength < 0.999f) ? strength.ToString("0.00") : "");
		foreach (KeyValuePair<Material, Material> glowMat in glowMats)
		{
			if (glowMat.Value.name == text)
			{
				return glowMat.Value;
			}
		}
		Material material2 = new Material(toonMat)
		{
			name = text
		};
		material2.SetColor("_WindowGlow", WindowGlow * strength);
		glowMats[material ?? material2] = material2;
		return material2;
	}

	private static bool IsSolidStructure(Transform t)
	{
		string text = t.name;
		if (text.StartsWith("Cliff_", StringComparison.Ordinal) || text == "Rock roadbed" || text == "PromenadeCurbs" || (text.StartsWith("Finished Road", StringComparison.Ordinal) && text.IndexOf("stair", StringComparison.Ordinal) >= 0))
		{
			return true;
		}
		Transform transform = t;
		while (transform != null)
		{
			if (transform.name == "HarborPier" || transform.name == "RoadSideWalls" || transform.name == "MapExpansion")
			{
				return true;
			}
			if (transform.name == "CoastalPromenade")
			{
				string[] coastalSolidPrefixes = CoastalSolidPrefixes;
				foreach (string value in coastalSolidPrefixes)
				{
					if (text.StartsWith(value, StringComparison.Ordinal))
					{
						return true;
					}
				}
				return false;
			}
			transform = transform.parent;
		}
		return false;
	}

	private Material NoFadeCopy(Material toonMat)
	{
		if (noFade.TryGetValue(toonMat, out var value))
		{
			return value;
		}
		Material material = new Material(toonMat)
		{
			name = toonMat.name + " solid"
		};
		material.SetFloat("_CamFadeOff", 1f);
		noFade[toonMat] = material;
		return material;
	}

	private Material Convert(Material src, bool tangents)
	{
		if (src == null || src.shader == null || src.shader == toon)
		{
			return null;
		}
		Dictionary<Material, Material> dictionary = (tangents ? cache : cacheNoTangent);
		if (dictionary.TryGetValue(src, out var value))
		{
			return value;
		}
		Material material = null;
		string text = src.shader.name;
		bool flag = src.HasProperty("baseColorTexture") || src.HasProperty("baseColorFactor");
		bool flag2 = text == "Universal Render Pipeline/Lit" || text == "Universal Render Pipeline/Simple Lit";
		bool flag3 = src.renderQueue >= 2900 || (src.HasProperty("_Surface") && src.GetFloat("_Surface") > 0.5f);
		if ((flag || flag2) && !flag3)
		{
			material = new Material(toon)
			{
				name = src.name + " (toon)"
			};
			if (flag)
			{
				if (src.HasProperty("baseColorTexture"))
				{
					material.SetTexture("_BaseMap", src.GetTexture("baseColorTexture"));
					material.SetTextureScale("_BaseMap", src.GetTextureScale("baseColorTexture"));
					material.SetTextureOffset("_BaseMap", src.GetTextureOffset("baseColorTexture"));
				}
				if (src.HasProperty("baseColorFactor"))
				{
					material.SetColor("_BaseColor", src.GetColor("baseColorFactor"));
				}
				Texture texture = (src.HasProperty("normalTexture") ? src.GetTexture("normalTexture") : null);
				if (texture != null && tangents)
				{
					material.SetTexture("_BumpMap", texture);
					material.SetFloat("_BumpScale", src.HasProperty("normalTexture_scale") ? src.GetFloat("normalTexture_scale") : 1f);
					material.SetFloat("_UseNormal", 1f);
				}
				Texture texture2 = (src.HasProperty("emissiveTexture") ? src.GetTexture("emissiveTexture") : null);
				if (src.HasProperty("emissiveFactor"))
				{
					Color color = src.GetColor("emissiveFactor");
					if (color.maxColorComponent > 0.001f)
					{
						material.SetColor("_EmissionColor", color);
						if (texture2 != null)
						{
							material.SetTexture("_EmissionMap", texture2);
						}
					}
				}
				if (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f)
				{
					material.SetFloat("_AlphaClip", 1f);
					material.SetFloat("_Cutoff", src.HasProperty("alphaCutoff") ? src.GetFloat("alphaCutoff") : 0.5f);
				}
			}
			else
			{
				material.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
				material.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
				material.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
				material.SetColor("_BaseColor", src.GetColor("_BaseColor"));
				Texture texture3 = (src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null);
				if (texture3 != null && tangents && src.IsKeywordEnabled("_NORMALMAP"))
				{
					material.SetTexture("_BumpMap", texture3);
					material.SetFloat("_BumpScale", src.HasProperty("_BumpScale") ? src.GetFloat("_BumpScale") : 1f);
					material.SetFloat("_UseNormal", 1f);
				}
				if (src.IsKeywordEnabled("_EMISSION") && src.HasProperty("_EmissionColor"))
				{
					material.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
					if (src.HasProperty("_EmissionMap") && src.GetTexture("_EmissionMap") != null)
					{
						material.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
					}
				}
				if (src.IsKeywordEnabled("_ALPHATEST_ON"))
				{
					material.SetFloat("_AlphaClip", 1f);
					material.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
				}
			}
			if (WindowGlow.maxColorComponent > 0.001f && src.name.IndexOf("window glass", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				material.SetColor("_WindowGlow", WindowGlow);
				glassMats++;
			}
			material.SetFloat("_Cull", src.HasProperty("_Cull") ? src.GetFloat("_Cull") : 2f);
			material.renderQueue = ((material.GetFloat("_AlphaClip") > 0.5f) ? 2450 : 2000);
			material.enableInstancing = src.enableInstancing;
		}
		dictionary[src] = material;
		return material;
	}
}
