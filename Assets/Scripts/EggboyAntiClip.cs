using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class EggboyAntiClip
{
	private struct Ear
	{
		public Vector3 c;

		public float r;
	}

	private struct BagShape
	{
		public Vector3 c;

		public Vector3 r;
	}

	private struct WorstResult
	{
		public float pen;

		public Vector3 pt;

		public Vector3 nrm;
	}

	private class Limb
	{
		public Transform bone;

		public int[] idx;
	}

	private class Job
	{
		public Transform bone;

		public int[] idx;

		public bool skipBag;

		public float allow;
	}

	private class Corr
	{
		public Quaternion c = Quaternion.identity;

		public Quaternion baseQ = Quaternion.identity;
	}

	private const int BANDS = 28;

	private const float MARGIN = 0.006f;

	private const float EAR_X = 0.31f;

	private const float RELAX = 5f;

	private const float HOLD = 0.012f;

	private const float MAX_RATE = 6f;

	private readonly EggboySkin skin;

	private readonly EggboyBones bones;

	private Transform bodyBone;

	private Transform bagBone;

	private int bodyIdx = -1;

	private int bagIdx = -1;

	private float y0;

	private float y1;

	private readonly float[] rx = new float[28];

	private readonly float[] rz = new float[28];

	private readonly List<Ear> ears = new List<Ear>();

	private BagShape? bag;

	private readonly Limb[] limbs = new Limb[4];

	private int[] bagPts = new int[0];

	private float bagBase;

	public readonly bool ok;

	private readonly Dictionary<Transform, Corr> corr = new Dictionary<Transform, Corr>();

	private Matrix4x4[] skinMats;

	public EggboyAntiClip(EggboySkin skin, EggboyBones bones, Transform model)
	{
		this.skin = skin;
		this.bones = bones;
		if (skin == null || skin.renderer == null || skin.mesh == null)
		{
			ok = false;
			return;
		}
		bodyIdx = skin.BoneIndex("body");
		bagIdx = skin.BoneIndex("bag");
		bodyBone = ((bodyIdx >= 0) ? skin.renderer.bones[bodyIdx] : null);
		bagBone = ((bagIdx >= 0) ? skin.renderer.bones[bagIdx] : null);
		List<int> list = new List<int>();
		Matrix4x4 matrix4x = ((bodyIdx >= 0) ? skin.mesh.bindposes[bodyIdx] : Matrix4x4.identity);
		int[] shellVerts = skin.shellVerts;
		foreach (int num in shellVerts)
		{
			if (bodyIdx >= 0 && skin.boneOfVertex[num] == bodyIdx && Mathf.Abs(matrix4x.MultiplyPoint3x4(skin.verts[num]).x) < 0.31f)
			{
				list.Add(num);
			}
		}
		if (list.Count > 0)
		{
			y0 = float.PositiveInfinity;
			y1 = float.NegativeInfinity;
			foreach (int item in list)
			{
				Vector3 vector = matrix4x.MultiplyPoint3x4(skin.verts[item]);
				y0 = Mathf.Min(y0, vector.y);
				y1 = Mathf.Max(y1, vector.y);
			}
			foreach (int item2 in list)
			{
				Vector3 vector2 = matrix4x.MultiplyPoint3x4(skin.verts[item2]);
				int num2 = Band(vector2.y);
				rx[num2] = Mathf.Max(rx[num2], Mathf.Abs(vector2.x));
				rz[num2] = Mathf.Max(rz[num2], Mathf.Abs(vector2.z));
			}
		}
		shellVerts = new int[2] { -1, 1 };
		foreach (int num3 in shellVerts)
		{
			List<int> list2 = new List<int>();
			int[] shellVerts2 = skin.shellVerts;
			foreach (int num4 in shellVerts2)
			{
				if (bodyIdx >= 0 && skin.boneOfVertex[num4] == bodyIdx && matrix4x.MultiplyPoint3x4(skin.verts[num4]).x * (float)num3 >= 0.31f)
				{
					list2.Add(num4);
				}
			}
			if (list2.Count == 0)
			{
				continue;
			}
			Vector3 zero = Vector3.zero;
			foreach (int item3 in list2)
			{
				zero += matrix4x.MultiplyPoint3x4(skin.verts[item3]);
			}
			zero /= (float)list2.Count;
			float num5 = 0f;
			foreach (int item4 in list2)
			{
				num5 = Mathf.Max(num5, Vector3.Distance(matrix4x.MultiplyPoint3x4(skin.verts[item4]), zero));
			}
			ears.Add(new Ear
			{
				c = zero,
				r = num5 * 0.85f
			});
		}
		List<int> list3 = new List<int>();
		Matrix4x4 matrix4x2 = ((bagIdx >= 0) ? skin.mesh.bindposes[bagIdx] : Matrix4x4.identity);
		if (bagIdx >= 0)
		{
			shellVerts = skin.leatherVerts;
			foreach (int num6 in shellVerts)
			{
				if (skin.boneOfVertex[num6] == bagIdx)
				{
					list3.Add(num6);
				}
			}
		}
		if (list3.Count > 0)
		{
			Vector3 vector3 = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			Vector3 vector4 = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
			foreach (int item5 in list3)
			{
				Vector3 rhs = matrix4x2.MultiplyPoint3x4(skin.verts[item5]);
				vector3 = Vector3.Min(vector3, rhs);
				vector4 = Vector3.Max(vector4, rhs);
			}
			bag = new BagShape
			{
				c = (vector3 + vector4) * 0.5f,
				r = (vector4 - vector3) * 0.45f
			};
			List<int> list4 = new List<int>();
			for (int k = 0; k < list3.Count; k++)
			{
				if (k % 6 == 0)
				{
					list4.Add(list3[k]);
				}
			}
			bagPts = list4.ToArray();
		}
		limbs[0] = LimbOf("armR", arm: true, skin);
		limbs[1] = LimbOf("handR", arm: false, skin);
		limbs[2] = LimbOf("armL", arm: true, skin);
		limbs[3] = LimbOf("handL", arm: false, skin);
		ok = bodyIdx >= 0 && bagIdx >= 0 && list.Count > 100 && limbs[0] != null && limbs[0].idx.Length > 5;
		if (ok && bag.HasValue)
		{
			Refresh();
			bagBase = Worst(bagPts, skipBag: true, 0f).pen;
		}
		if (ok)
		{
			AddShoulderJoints(model);
		}
	}

	private Limb LimbOf(string name, bool arm, EggboySkin skin)
	{
		int num = skin.BoneIndex(name);
		if (num < 0)
		{
			return null;
		}
		List<int> list = new List<int>();
		int[] shellVerts = skin.shellVerts;
		foreach (int num2 in shellVerts)
		{
			if (skin.boneOfVertex[num2] == num)
			{
				list.Add(num2);
			}
		}
		if (arm)
		{
			Vector3 sh = skin.mesh.bindposes[num].inverse.MultiplyPoint3x4(Vector3.zero);
			list.RemoveAll((int vi) => Vector3.Distance(skin.verts[vi], sh) <= 0.1f);
		}
		int num3 = Mathf.Max(1, list.Count / 70);
		List<int> list2 = new List<int>();
		for (int j = 0; j < list.Count; j++)
		{
			if (j % num3 == 0)
			{
				list2.Add(list[j]);
			}
		}
		return new Limb
		{
			bone = skin.renderer.bones[num],
			idx = list2.ToArray()
		};
	}

	private int Band(float y)
	{
		float num = Mathf.Clamp01((y - y0) / Mathf.Max(1E-06f, y1 - y0));
		return Mathf.Min(27, Mathf.FloorToInt(num * 28f));
	}

	private void Refresh()
	{
		if (skin != null)
		{
			skinMats = skin.SkinMatrices();
		}
	}

	private float Penetrate(Vector3 pw, bool skipBag, out Vector3 outDir, float extra = 0f)
	{
		Vector3 vector = Vector3.zero;
		float num = 0f;
		Vector3 vector2 = bodyBone.InverseTransformPoint(pw);
		if (vector2.y > y0 && vector2.y < y1)
		{
			int num2 = Band(vector2.y);
			float num3 = rx[num2] + 0.006f + extra;
			float num4 = rz[num2] + 0.006f + extra;
			float num5 = vector2.x / num3 * (vector2.x / num3) + vector2.z / num4 * (vector2.z / num4);
			if (num5 < 1f)
			{
				float num6 = (1f - Mathf.Sqrt(num5)) * Mathf.Min(num3, num4);
				if (num6 > num)
				{
					num = num6;
					Vector3 direction = new Vector3(vector2.x / (num3 * num3), 0f, vector2.z / (num4 * num4));
					vector = bodyBone.TransformDirection(direction).normalized;
				}
			}
		}
		foreach (Ear ear in ears)
		{
			float num7 = ear.r + 0.006f + extra - Vector3.Distance(vector2, ear.c);
			if (num7 > num)
			{
				num = num7;
				vector = bodyBone.TransformDirection(vector2 - ear.c).normalized;
			}
		}
		if (!skipBag && bag.HasValue && bagBone != null)
		{
			vector2 = bagBone.InverseTransformPoint(pw) - bag.Value.c;
			float num8 = bag.Value.r.x + extra;
			float num9 = bag.Value.r.y + extra;
			float num10 = bag.Value.r.z + extra;
			float num11 = vector2.x / num8 * (vector2.x / num8) + vector2.y / num9 * (vector2.y / num9) + vector2.z / num10 * (vector2.z / num10);
			if (num11 < 1f)
			{
				float num12 = (1f - Mathf.Sqrt(num11)) * Mathf.Min(num8, Mathf.Min(num9, num10));
				if (num12 > num)
				{
					num = num12;
					Vector3 direction2 = new Vector3(vector2.x / (num8 * num8), vector2.y / (num9 * num9), vector2.z / (num10 * num10));
					vector = bagBone.TransformDirection(direction2).normalized;
				}
			}
		}
		outDir = vector;
		return num;
	}

	private WorstResult Worst(int[] idx, bool skipBag, float extra)
	{
		WorstResult worstResult = default(WorstResult);
		worstResult.pen = 0f;
		worstResult.pt = Vector3.zero;
		worstResult.nrm = Vector3.zero;
		WorstResult result = worstResult;
		if (idx == null || idx.Length == 0)
		{
			return result;
		}
		Refresh();
		foreach (int vertex in idx)
		{
			Vector3 vector = skin.VertexWorld(vertex, skinMats);
			Vector3 outDir;
			float num = Penetrate(vector, skipBag, out outDir, extra);
			if (num > result.pen)
			{
				result.pen = num;
				result.pt = vector;
				result.nrm = outDir;
			}
		}
		return result;
	}

	private void Turn(Transform bone, Vector3 axisWorld, float angle)
	{
		if (!(bone.parent == null))
		{
			Quaternion rotation = bone.parent.rotation;
			Quaternion quaternion = Quaternion.AngleAxis(angle * 57.29578f, axisWorld);
			bone.localRotation = Quaternion.Inverse(rotation) * quaternion * rotation * bone.localRotation;
		}
	}

	private void Push(Transform bone, int[] idx, bool skipBag, float allow)
	{
		for (int i = 0; i < 8; i++)
		{
			WorstResult worstResult = Worst(idx, skipBag, 0f);
			worstResult.pen -= allow;
			if (worstResult.pen < 0.001f)
			{
				break;
			}
			Vector3 position = bone.position;
			Vector3 lhs = worstResult.pt - position;
			float magnitude = lhs.magnitude;
			Vector3 axisWorld = Vector3.Cross(lhs, worstResult.nrm);
			if (magnitude < 0.0001f || axisWorld.sqrMagnitude < 1E-10f)
			{
				break;
			}
			axisWorld.Normalize();
			Turn(bone, axisWorld, Mathf.Min(0.3f, worstResult.pen / magnitude * 1.05f));
		}
	}

	private void SetBone(Transform b, Quaternion c, Quaternion baseQ)
	{
		b.localRotation = c * baseQ;
	}

	public void Solve(float dt, float boost = 1f)
	{
		if (!ok || dt <= 0f)
		{
			return;
		}
		List<Job> list = new List<Job>();
		if (bagBone != null && bagPts.Length != 0)
		{
			list.Add(new Job
			{
				bone = bagBone,
				idx = bagPts,
				skipBag = true,
				allow = bagBase + 0.004f
			});
		}
		if (limbs[0] != null)
		{
			list.Add(new Job
			{
				bone = limbs[0].bone,
				idx = Merge(limbs[0].idx, limbs[1]?.idx),
				skipBag = false,
				allow = 0f
			});
			if (limbs[1] != null)
			{
				list.Add(new Job
				{
					bone = limbs[1].bone,
					idx = limbs[1].idx,
					skipBag = false,
					allow = 0f
				});
			}
			list.Add(new Job
			{
				bone = limbs[2].bone,
				idx = Merge(limbs[2].idx, limbs[3]?.idx),
				skipBag = false,
				allow = 0f
			});
			if (limbs[3] != null)
			{
				list.Add(new Job
				{
					bone = limbs[3].bone,
					idx = limbs[3].idx,
					skipBag = false,
					allow = 0f
				});
			}
		}
		foreach (Job item in list)
		{
			if (!this.corr.TryGetValue(item.bone, out var value))
			{
				value = new Corr();
				this.corr[item.bone] = value;
			}
			value.baseQ = item.bone.localRotation;
			item.bone.localRotation = value.c * item.bone.localRotation;
		}
		Refresh();
		float num = 1f - Mathf.Exp((0f - dt) * 5f * boost);
		Quaternion identity = Quaternion.identity;
		foreach (Job item2 in list)
		{
			Corr corr = this.corr[item2.bone];
			if (num > 0f && Angle(corr.c, identity) > 0.006f)
			{
				Quaternion c = corr.c;
				SetBone(item2.bone, Quaternion.Slerp(corr.c, identity, num), corr.baseQ);
				if (Worst(item2.idx, item2.skipBag, 0.012f).pen > item2.allow)
				{
					corr.c = c;
					SetBone(item2.bone, c, corr.baseQ);
				}
			}
			Push(item2.bone, item2.idx, item2.skipBag, item2.allow);
		}
		float maxDegreesDelta = 6f * boost * dt * 57.29578f;
		foreach (Job item3 in list)
		{
			Corr corr2 = this.corr[item3.bone];
			Quaternion to = item3.bone.localRotation * Quaternion.Inverse(corr2.baseQ);
			corr2.c = Quaternion.RotateTowards(corr2.c, to, maxDegreesDelta);
			item3.bone.localRotation = corr2.c * corr2.baseQ;
		}
	}

	private static int[] Merge(int[] a, int[] b)
	{
		if (b == null || b.Length == 0)
		{
			return a;
		}
		int[] array = new int[a.Length + b.Length];
		Array.Copy(a, 0, array, 0, a.Length);
		Array.Copy(b, 0, array, a.Length, b.Length);
		return array;
	}

	private static float Angle(Quaternion a, Quaternion b)
	{
		return Quaternion.Angle(a, b);
	}

	private void AddShoulderJoints(Transform model)
	{
		if (bodyBone == null)
		{
			return;
		}
		Material material = JointMaterial();
		string[] array = new string[2] { "armR", "armL" };
		foreach (string text in array)
		{
			int num = skin.BoneIndex(text);
			if (num >= 0)
			{
				Matrix4x4 inverse = skin.mesh.bindposes[num].inverse;
				Vector3 vector = inverse.MultiplyPoint3x4(Vector3.zero);
				Vector3 normalized = inverse.MultiplyVector(Vector3.up).normalized;
				Vector3 point = vector + normalized * -0.012f;
				Vector3 localPosition = skin.mesh.bindposes[bodyIdx].MultiplyPoint3x4(point);
				GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				gameObject.name = "joint-" + text;
				UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
				MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
				if (material != null)
				{
					component.sharedMaterial = material;
				}
				gameObject.transform.SetParent(bodyBone, worldPositionStays: false);
				gameObject.transform.localPosition = localPosition;
				gameObject.transform.localScale = Vector3.one * 0.096f * 2f;
			}
		}
	}

	private static Material JointMaterial()
	{
		Shader shader = ((GraphicsSettings.currentRenderPipeline != null) ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard"));
		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}
		if (shader == null)
		{
			shader = Shader.Find("Universal Render Pipeline/Lit");
		}
		if (shader == null)
		{
			return null;
		}
		Material material = new Material(shader);
		material.name = "joint";
		material.color = new Color32(75, 74, 80, byte.MaxValue);
		if (material.HasProperty("_Smoothness"))
		{
			material.SetFloat("_Smoothness", 0.42f);
		}
		if (material.HasProperty("_Glossiness"))
		{
			material.SetFloat("_Glossiness", 0.42f);
		}
		if (material.HasProperty("_Metallic"))
		{
			material.SetFloat("_Metallic", 0.55f);
		}
		return material;
	}

	public string Measure()
	{
		if (!ok)
		{
			return "antiClip not ready";
		}
		Refresh();
		return $"armR {Worst(limbs[0].idx, skipBag: false, 0f).pen:F4} handR {Worst(limbs[1].idx, skipBag: false, 0f).pen:F4} " + $"armL {Worst(limbs[2].idx, skipBag: false, 0f).pen:F4} handL {Worst(limbs[3].idx, skipBag: false, 0f).pen:F4}";
	}
}
