using System;
using System.Collections.Generic;
using UnityEngine;

public class EggboySkin
{
	public readonly SkinnedMeshRenderer renderer;

	public readonly Mesh mesh;

	public readonly Transform model;

	public readonly Material shellMat;

	public readonly Material leatherMat;

	public readonly Material visorMat;

	public readonly int shellSubmesh = -1;

	public readonly int leatherSubmesh = -1;

	public readonly int visorSubmesh = -1;

	public readonly int[] shellVerts;

	public readonly int[] leatherVerts;

	public readonly int[] visorVerts;

	public readonly Vector3[] verts;

	public readonly BoneWeight[] weights;

	public readonly int[] boneOfVertex;

	public EggboySkin(GameObject modelObject, SkinnedMeshRenderer smr)
	{
		model = modelObject.transform;
		renderer = smr;
		mesh = ((smr != null) ? smr.sharedMesh : null);
		if (mesh == null)
		{
			shellVerts = (leatherVerts = (visorVerts = new int[0]));
			verts = new Vector3[0];
			weights = new BoneWeight[0];
			boneOfVertex = new int[0];
			return;
		}
		if (!mesh.isReadable)
		{
			Debug.LogError("模型网格不可读，无法做表情/防穿模；请在导入设置里打开 Read/Write。");
		}
		verts = mesh.vertices;
		weights = mesh.boneWeights;
		boneOfVertex = new int[verts.Length];
		for (int i = 0; i < boneOfVertex.Length; i++)
		{
			boneOfVertex[i] = Dominant(i, 0.9f);
		}
		Material[] sharedMaterials = smr.sharedMaterials;
		Dictionary<string, HashSet<int>> dictionary = new Dictionary<string, HashSet<int>>();
		string[] array = new string[mesh.subMeshCount];
		for (int j = 0; j < mesh.subMeshCount; j++)
		{
			Material material = ((j < sharedMaterials.Length) ? sharedMaterials[j] : null);
			string text = MatKind((material != null) ? material.name : null);
			if (text.Length == 0 && mesh.subMeshCount == 3)
			{
				text = j switch
				{
					1 => "leather", 
					0 => "shell", 
					_ => "visor", 
				};
			}
			array[j] = text;
			if (text.Length != 0 && !dictionary.ContainsKey(text))
			{
				HashSet<int> hashSet = new HashSet<int>();
				int[] triangles = mesh.GetTriangles(j);
				foreach (int item in triangles)
				{
					hashSet.Add(item);
				}
				dictionary[text] = hashSet;
			}
		}
		shellVerts = ToArray(dictionary, "shell");
		leatherVerts = ToArray(dictionary, "leather");
		visorVerts = ToArray(dictionary, "visor");
		shellSubmesh = Find(array, "shell");
		leatherSubmesh = Find(array, "leather");
		visorSubmesh = Find(array, "visor");
		shellMat = MaterialOf(sharedMaterials, shellSubmesh);
		leatherMat = MaterialOf(sharedMaterials, leatherSubmesh);
		visorMat = MaterialOf(sharedMaterials, visorSubmesh);
		ApplyAtlas();
	}

	private void ApplyAtlas()
	{
		Texture2D texture2D = Resources.Load<Texture2D>("Textures/img0");
		if (texture2D == null)
		{
			Debug.LogError("找不到 Resources/Textures/img0，角色会保持白模");
			return;
		}
		Material[] materials = renderer.materials;
		Material[] array = materials;
		foreach (Material material in array)
		{
			if (!(material == null))
			{
				if (material.HasProperty("_MainTex"))
				{
					material.SetTexture("_MainTex", texture2D);
				}
				if (material.HasProperty("_BaseMap"))
				{
					material.SetTexture("_BaseMap", texture2D);
				}
				if (material.HasProperty("_Color"))
				{
					material.SetColor("_Color", Color.white);
				}
				if (material.HasProperty("_BaseColor"))
				{
					material.SetColor("_BaseColor", Color.white);
				}
				material.mainTexture = texture2D;
			}
		}
		Texture2D texture2D2 = Resources.Load<Texture2D>("Textures/img1_mg");
		Material material2 = Resources.Load<Material>("EggboyURPLitMetallic");
		if (texture2D2 != null && material2 != null)
		{
			array = materials;
			foreach (Material material3 in array)
			{
				if (!(material3 == null))
				{
					material3.shader = material2.shader;
					if (material3.HasProperty("_BaseMap"))
					{
						material3.SetTexture("_BaseMap", texture2D);
					}
					if (material3.HasProperty("_BaseColor"))
					{
						material3.SetColor("_BaseColor", Color.white);
					}
					if (material3.HasProperty("_MetallicGlossMap"))
					{
						material3.SetTexture("_MetallicGlossMap", texture2D2);
					}
					material3.EnableKeyword("_METALLICSPECGLOSSMAP");
					if (material3.HasProperty("_Metallic"))
					{
						material3.SetFloat("_Metallic", 1f);
					}
					if (material3.HasProperty("_Smoothness"))
					{
						material3.SetFloat("_Smoothness", 1f);
					}
					if (material3.HasProperty("_Glossiness"))
					{
						material3.SetFloat("_Glossiness", 1f);
					}
				}
			}
			Debug.Log("URP 金属度/粗糙度贴图已接入：" + texture2D2.width + "x" + texture2D2.height);
		}
		else
		{
			Debug.LogError("找不到 img1_mg 或 EggboyURPLitMetallic 模板，角色材质会不完整");
		}
		Debug.Log("角色图集已接入：" + texture2D.width + "x" + texture2D.height);
	}

	private static string MatKind(string name)
	{
		string text = EggboyBones.Norm(name);
		if (text.Contains("visor"))
		{
			return "visor";
		}
		if (text.Contains("leather"))
		{
			return "leather";
		}
		if (text.Contains("shell"))
		{
			return "shell";
		}
		return "";
	}

	private static int Find(string[] kinds, string kind)
	{
		for (int i = 0; i < kinds.Length; i++)
		{
			if (kinds[i] == kind)
			{
				return i;
			}
		}
		return -1;
	}

	private static Material MaterialOf(Material[] mats, int submesh)
	{
		if (submesh < 0 || submesh >= mats.Length)
		{
			return null;
		}
		return mats[submesh];
	}

	private static int[] ToArray(Dictionary<string, HashSet<int>> sets, string kind)
	{
		if (!sets.TryGetValue(kind, out var value))
		{
			return new int[0];
		}
		int[] array = new int[value.Count];
		value.CopyTo(array);
		Array.Sort(array);
		return array;
	}

	public int BoneIndex(string name)
	{
		if (renderer == null)
		{
			return -1;
		}
		string text = EggboyBones.Norm(name);
		for (int i = 0; i < renderer.bones.Length; i++)
		{
			if (renderer.bones[i] != null && EggboyBones.Norm(renderer.bones[i].name) == text)
			{
				return i;
			}
		}
		return -1;
	}

	public Transform Bone(string name)
	{
		int num = BoneIndex(name);
		if (num < 0)
		{
			return null;
		}
		return renderer.bones[num];
	}

	public int Dominant(int vertex, float threshold)
	{
		BoneWeight boneWeight = weights[vertex];
		int result = -1;
		float num = threshold;
		if (boneWeight.weight0 > num)
		{
			num = boneWeight.weight0;
			result = boneWeight.boneIndex0;
		}
		if (boneWeight.weight1 > num)
		{
			num = boneWeight.weight1;
			result = boneWeight.boneIndex1;
		}
		if (boneWeight.weight2 > num)
		{
			num = boneWeight.weight2;
			result = boneWeight.boneIndex2;
		}
		if (boneWeight.weight3 > num)
		{
			num = boneWeight.weight3;
			result = boneWeight.boneIndex3;
		}
		return result;
	}

	public Matrix4x4 SkinMatrix(int boneIndex)
	{
		return renderer.bones[boneIndex].localToWorldMatrix * mesh.bindposes[boneIndex];
	}

	public Matrix4x4[] SkinMatrices()
	{
		Matrix4x4[] array = new Matrix4x4[renderer.bones.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = SkinMatrix(i);
		}
		return array;
	}

	public Vector3 VertexWorld(int vertex, Matrix4x4[] skin)
	{
		BoneWeight boneWeight = weights[vertex];
		Vector3 vector = verts[vertex];
		float num = boneWeight.weight0 + boneWeight.weight1 + boneWeight.weight2 + boneWeight.weight3;
		if (num < 0.0001f)
		{
			return renderer.transform.TransformPoint(vector);
		}
		return (Vector3.zero + skin[boneWeight.boneIndex0].MultiplyPoint3x4(vector) * boneWeight.weight0 + skin[boneWeight.boneIndex1].MultiplyPoint3x4(vector) * boneWeight.weight1 + skin[boneWeight.boneIndex2].MultiplyPoint3x4(vector) * boneWeight.weight2 + skin[boneWeight.boneIndex3].MultiplyPoint3x4(vector) * boneWeight.weight3) / num;
	}

	public Vector3 BindWorld(int vertex)
	{
		return renderer.transform.TransformPoint(verts[vertex]);
	}
}
