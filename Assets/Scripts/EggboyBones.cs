using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class EggboyBones
{
	public class Entry
	{
		public Transform bone;

		public Quaternion rb;

		public Quaternion rbi;

		public Quaternion prbi;

		public Vector3 bindLocalPosition;

		public Quaternion bindLocalRotation;

		public Vector3 bindLocalScale;

		public Vector3 cleanLocalPosition;

		public Quaternion cleanLocalRotation;

		public Vector3 cleanLocalScale;
	}

	public readonly Transform model;

	private readonly Dictionary<string, Entry> map = new Dictionary<string, Entry>();

	public EggboyBones(GameObject modelObject, SkinnedMeshRenderer smr)
	{
		model = modelObject.transform;
		Quaternion quaternion = Quaternion.Inverse(model.rotation);
		if (smr == null)
		{
			return;
		}
		Transform[] bones = smr.bones;
		foreach (Transform transform in bones)
		{
			if (!(transform == null))
			{
				Quaternion quaternion2 = quaternion * transform.rotation;
				Quaternion rotation = ((transform.parent != null) ? (quaternion * transform.parent.rotation) : Quaternion.identity);
				map[Norm(transform.name)] = new Entry
				{
					bone = transform,
					rb = quaternion2,
					rbi = Quaternion.Inverse(quaternion2),
					prbi = Quaternion.Inverse(rotation),
					bindLocalPosition = transform.localPosition,
					bindLocalRotation = transform.localRotation,
					bindLocalScale = transform.localScale,
					cleanLocalPosition = transform.localPosition,
					cleanLocalRotation = transform.localRotation,
					cleanLocalScale = transform.localScale
				};
			}
		}
	}

	public static string Norm(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(name.Length);
		for (int i = 0; i < name.Length; i++)
		{
			char c = char.ToLowerInvariant(name[i]);
			if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	public Entry Get(string name)
	{
		if (!map.TryGetValue(Norm(name), out var value))
		{
			return null;
		}
		return value;
	}

	public Transform Bone(string name)
	{
		return Get(name)?.bone;
	}

	public bool Has(string name)
	{
		return map.ContainsKey(Norm(name));
	}

	public void Rotate(string name, Vector3 axis, float radians)
	{
		if (!(Mathf.Abs(radians) < 1E-07f))
		{
			Entry entry = Get(name);
			if (entry != null)
			{
				Vector3 axis2 = new Vector3(0f - axis.x, axis.y, axis.z);
				Quaternion quaternion = Quaternion.AngleAxis((0f - radians) * 57.29578f, axis2);
				Quaternion quaternion2 = entry.rbi * quaternion * entry.rb;
				entry.bone.localRotation = entry.bone.localRotation * quaternion2;
			}
		}
	}

	public void RotateRaw(string name, Vector3 axis, float radians)
	{
		if (!(Mathf.Abs(radians) < 1E-07f))
		{
			Entry entry = Get(name);
			if (entry != null)
			{
				Quaternion quaternion = Quaternion.AngleAxis(radians * 57.29578f, axis);
				Quaternion quaternion2 = entry.rbi * quaternion * entry.rb;
				entry.bone.localRotation = entry.bone.localRotation * quaternion2;
			}
		}
	}

	public void Move(string name, float x, float y, float z)
	{
		Entry entry = Get(name);
		if (entry != null)
		{
			Vector3 vector = entry.prbi * new Vector3(x, y, z);
			entry.bone.localPosition += vector;
		}
	}

	public Vector3 BindPosition(string name)
	{
		Entry entry = Get(name);
		if (entry == null)
		{
			return Vector3.zero;
		}
		Vector3 position = entry.bone.position;
		return model.InverseTransformPoint(position);
	}

	public void RestoreBindPose()
	{
		foreach (Entry value in map.Values)
		{
			value.bone.localPosition = value.bindLocalPosition;
			value.bone.localRotation = value.bindLocalRotation;
			value.bone.localScale = value.bindLocalScale;
		}
	}

	public void CaptureClean()
	{
		foreach (Entry value in map.Values)
		{
			value.cleanLocalPosition = value.bone.localPosition;
			value.cleanLocalRotation = value.bone.localRotation;
			value.cleanLocalScale = value.bone.localScale;
		}
	}

	public void RestoreClean()
	{
		foreach (Entry value in map.Values)
		{
			value.bone.localPosition = value.cleanLocalPosition;
			value.bone.localRotation = value.cleanLocalRotation;
			value.bone.localScale = value.cleanLocalScale;
		}
	}
}
