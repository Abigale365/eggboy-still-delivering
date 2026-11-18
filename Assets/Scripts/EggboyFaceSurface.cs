using System.Collections.Generic;
using UnityEngine;

public static class EggboyFaceSurface
{
	private static float Cross(Vector2 o, Vector2 a, Vector2 b)
	{
		return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
	}

	private static List<Vector2> Hull(List<Vector2> p)
	{
		p.Sort((Vector2 a, Vector2 b) => (a.x != b.x) ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
		List<Vector2> list = new List<Vector2>();
		foreach (Vector2 item in p)
		{
			while (list.Count > 1 && Cross(list[list.Count - 2], list[list.Count - 1], item) <= 0f)
			{
				list.RemoveAt(list.Count - 1);
			}
			list.Add(item);
		}
		int count = list.Count;
		for (int num = p.Count - 2; num >= 0; num--)
		{
			Vector2 vector = p[num];
			while (list.Count > count && Cross(list[list.Count - 2], list[list.Count - 1], vector) <= 0f)
			{
				list.RemoveAt(list.Count - 1);
			}
			list.Add(vector);
		}
		if (list.Count > 1)
		{
			list.RemoveAt(list.Count - 1);
		}
		return list;
	}

	public static bool Build(EggboySkin skin, Transform model, Texture face, out Transform result)
	{
		result = null;
		Texture2D texture2D = Resources.Load<Texture2D>("Textures/img0");
		if (texture2D == null || !texture2D.isReadable)
		{
			Debug.LogError("Face atlas must be readable");
			return false;
		}
		Vector3[] verts = skin.verts;
		Vector2[] uv = skin.mesh.uv;
		Vector3[] array = new Vector3[verts.Length];
		bool[] array2 = new bool[verts.Length];
		bool[] array3 = new bool[verts.Length];
		for (int i = 0; i < verts.Length; i++)
		{
			array[i] = model.InverseTransformPoint(skin.renderer.transform.TransformPoint(verts[i]));
			Color pixelBilinear = texture2D.GetPixelBilinear(uv[i].x, uv[i].y);
			float num = 0.299f * pixelBilinear.r + 0.587f * pixelBilinear.g + 0.114f * pixelBilinear.b;
			float num2 = Mathf.Max(pixelBilinear.r, pixelBilinear.g, pixelBilinear.b);
			float num3 = Mathf.Min(pixelBilinear.r, pixelBilinear.g, pixelBilinear.b);
			array2[i] = num < 0.23529412f;
			array3[i] = array2[i] || (num2 > 0f && (num2 - num3) / num2 > 0.45f && pixelBilinear.r > 0.5882353f && pixelBilinear.b < 26f / 51f);
		}
		List<float> list = new List<float>();
		List<float> list2 = new List<float>();
		float num4 = float.PositiveInfinity;
		int[] visorVerts = skin.visorVerts;
		foreach (int num5 in visorVerts)
		{
			num4 = Mathf.Min(num4, array[num5].z);
			if (array3[num5])
			{
				list.Add(array[num5].x);
				list2.Add(array[num5].y);
			}
		}
		if (list.Count < 30)
		{
			Debug.LogError("Insufficient face samples " + list.Count);
			return false;
		}
		list.Sort();
		list2.Sort();
		int index = Mathf.FloorToInt((float)(list.Count - 1) * 0.02f);
		int index2 = Mathf.FloorToInt((float)(list.Count - 1) * 0.98f);
		float num6 = list[index];
		float num7 = list[index2];
		float num8 = list2[index];
		float num9 = list2[index2];
		float num10 = (num7 - num6) * 0.04f;
		float num11 = (num9 - num8) * 0.04f;
		num6 -= num10;
		num7 += num10;
		num8 -= num11;
		num9 += num11;
		num4 -= 0.02f;
		Vector2[] array4 = new Vector2[verts.Length];
		List<Vector2> list3 = new List<Vector2>();
		for (int k = 0; k < verts.Length; k++)
		{
			array4[k] = ((array[k].z >= num4) ? new Vector2((array[k].x - num6) / (num7 - num6), (array[k].y - num8) / (num9 - num8)) : new Vector2(-10f, -10f));
		}
		bool[] array5 = new bool[51200];
		visorVerts = new int[2] { skin.shellSubmesh, skin.visorSubmesh };
		foreach (int num12 in visorVerts)
		{
			if (num12 < 0)
			{
				continue;
			}
			int[] triangles = skin.mesh.GetTriangles(num12);
			for (int l = 0; l < triangles.Length; l += 3)
			{
				int num13 = triangles[l];
				int num14 = triangles[l + 1];
				int num15 = triangles[l + 2];
				if (Within(array4[num13]) && Within(array4[num14]) && Within(array4[num15]))
				{
					if (array2[num13] && array2[num14] && array2[num15])
					{
						Raster(array4[num13], array4[num14], array4[num15], array5, 256, 200);
					}
					else if (Mathf.Max(array4[num13].x, array4[num14].x, array4[num15].x) >= 0.66f)
					{
						Raster(array4[num13], array4[num14], array4[num15], array5, 256, 200, texture2D, uv[num13], uv[num14], uv[num15]);
					}
				}
			}
		}
		bool[] array6 = new bool[51200];
		Stack<int> stack = new Stack<int>();
		List<int> list4 = new List<int>();
		for (int m = 0; m < array5.Length; m++)
		{
			if (!array5[m] || array6[m])
			{
				continue;
			}
			List<int> list5 = new List<int>();
			stack.Push(m);
			array6[m] = true;
			while (stack.Count > 0)
			{
				int num16 = stack.Pop();
				list5.Add(num16);
				int num17 = num16 % 256;
				int num18 = num16 / 256;
				visorVerts = new int[4]
				{
					(num17 > 0) ? (num16 - 1) : (-1),
					(num17 < 255) ? (num16 + 1) : (-1),
					(num18 > 0) ? (num16 - 256) : (-1),
					(num18 < 199) ? (num16 + 256) : (-1)
				};
				foreach (int num19 in visorVerts)
				{
					if (num19 >= 0 && array5[num19] && !array6[num19])
					{
						array6[num19] = true;
						stack.Push(num19);
					}
				}
			}
			if (list5.Count > list4.Count)
			{
				list4 = list5;
			}
		}
		foreach (int item in list4)
		{
			int num20 = item % 256;
			int num21 = item / 256;
			list3.Add(new Vector2((float)num20 / 256f, (float)num21 / 200f));
			list3.Add(new Vector2(((float)num20 + 1f) / 256f, ((float)num21 + 1f) / 200f));
		}
		if (list3.Count < 3)
		{
			Debug.LogError("Face contour unavailable");
			return false;
		}
		List<Vector2> list6 = Hull(list3);
		Texture2D texture2D2 = new Texture2D(256, 200, TextureFormat.RGBA32, mipChain: false, linear: true);
		texture2D2.wrapMode = TextureWrapMode.Clamp;
		Color32[] array7 = new Color32[51200];
		for (int n = 0; n < 200; n++)
		{
			for (int num22 = 0; num22 < 256; num22++)
			{
				Vector2 b = new Vector2(((float)num22 + 0.5f) / 256f, ((float)n + 0.5f) / 200f);
				float num23 = float.PositiveInfinity;
				for (int num24 = 0; num24 < list6.Count; num24++)
				{
					Vector2 vector = list6[num24];
					Vector2 vector2 = list6[(num24 + 1) % list6.Count];
					float b2 = Cross(vector, vector2, b) / (vector2 - vector).magnitude;
					num23 = Mathf.Min(num23, b2);
				}
				float num25 = Mathf.SmoothStep(0f, 1f, num23 * 256f * 0.55f + 0.5f);
				byte b3 = (byte)Mathf.RoundToInt(255f * num25);
				array7[n * 256 + num22] = new Color32(b3, b3, b3, byte.MaxValue);
			}
		}
		texture2D2.SetPixels32(array7);
		texture2D2.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		Mesh mesh = Object.Instantiate(skin.mesh);
		mesh.name = "EGGBOY native face UV";
		mesh.uv2 = array4;
		Shader shader = Resources.Load<Shader>("EggboyFaceSurface");
		if (shader == null)
		{
			Debug.LogError("Missing face surface shader");
			return false;
		}
		Material[] materials = skin.renderer.materials;
		visorVerts = new int[2] { skin.shellSubmesh, skin.visorSubmesh };
		foreach (int num26 in visorVerts)
		{
			if (num26 >= 0 && num26 < materials.Length)
			{
				Material material = new Material(materials[num26]);
				material.name = materials[num26].name + " native face";
				material.shader = shader;
				material.SetTexture("_FaceMap", face);
				material.SetTexture("_FaceMask", texture2D2);
				materials[num26] = material;
			}
		}
		skin.renderer.sharedMesh = mesh;
		skin.renderer.sharedMaterials = materials;
		result = skin.renderer.transform;
		Debug.Log($"NATIVE_FACE originalRenderer=true originalSubmeshes={mesh.subMeshCount} screenPixels={list4.Count} hull={list6.Count} x={num6:F3}..{num7:F3} y={num8:F3}..{num9:F3}");
		return true;
	}

	private static void Raster(Vector2 a, Vector2 b, Vector2 c, bool[] pixels, int w, int h, Texture2D atlas = null, Vector2 uvA = default(Vector2), Vector2 uvB = default(Vector2), Vector2 uvC = default(Vector2))
	{
		a = Vector2.Scale(a, new Vector2(w, h));
		b = Vector2.Scale(b, new Vector2(w, h));
		c = Vector2.Scale(c, new Vector2(w, h));
		int num = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)), 0, w - 1);
		int num2 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)), 0, w - 1);
		int num3 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)), 0, h - 1);
		int num4 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)), 0, h - 1);
		for (int i = num3; i <= num4; i++)
		{
			for (int j = num; j <= num2; j++)
			{
				Vector2 b2 = new Vector2((float)j + 0.5f, (float)i + 0.5f);
				float num5 = Cross(a, b, b2);
				float num6 = Cross(b, c, b2);
				float num7 = Cross(c, a, b2);
				if ((!(num5 >= 0f) || !(num6 >= 0f) || !(num7 >= 0f)) && (!(num5 <= 0f) || !(num6 <= 0f) || !(num7 <= 0f)))
				{
					continue;
				}
				if (atlas != null)
				{
					if (b2.x / (float)w < 0.66f)
					{
						continue;
					}
					float num8 = num5 + num6 + num7;
					if (Mathf.Abs(num8) < 1E-05f)
					{
						continue;
					}
					Vector2 vector = (uvA * num6 + uvB * num7 + uvC * num5) / num8;
					Color pixelBilinear = atlas.GetPixelBilinear(vector.x, vector.y);
					if (0.299f * pixelBilinear.r + 0.587f * pixelBilinear.g + 0.114f * pixelBilinear.b > 0.52f)
					{
						continue;
					}
				}
				pixels[i * w + j] = true;
			}
		}
	}

	private static bool Within(Vector2 uv)
	{
		if (uv.x > -0.08f && uv.x < 1.08f && uv.y > -0.08f)
		{
			return uv.y < 1.08f;
		}
		return false;
	}

	private static bool WithinInner(Vector2 uv)
	{
		if (uv.x > 0.02f && uv.x < 0.98f && uv.y > 0.02f)
		{
			return uv.y < 0.98f;
		}
		return false;
	}
}
