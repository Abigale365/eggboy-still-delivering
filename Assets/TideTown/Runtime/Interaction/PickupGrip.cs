using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PickupGrip
{
	public const float DefaultDiameter = 0.14f;

	private readonly EggboyBones bones;

	private readonly Transform shoulder;

	private readonly Transform wrist;

	private readonly Transform finger1;

	private readonly Transform finger2;

	private readonly Quaternion finger1Rest;

	private readonly Quaternion finger2Rest;

	private readonly Vector3 palmOffset;

	private readonly Vector3 palmNormal;

	private readonly Vector3 curlAxis;

	private readonly List<Vector3> shell = new List<Vector3>();

	public readonly Vector3 CarryCenterLocal = new Vector3(0.359f, 0.192f, 0.287f);

	public Transform Body { get; }

	public Vector3 CarryCenter => Body.TransformPoint(CarryCenterLocal);

	public Vector3 PalmPosition => wrist.TransformPoint(palmOffset);

	public bool Ok { get; }

	public PickupGrip(EggboyBones bones, EggboySkin skin)
	{
		this.bones = bones;
		Body = bones.Bone("body");
		shoulder = bones.Bone("armR");
		wrist = bones.Bone("handR");
		finger1 = bones.Bone("finger1R");
		finger2 = bones.Bone("finger2R");
		Ok = Body != null && shoulder != null && wrist != null && finger1 != null && finger2 != null && skin != null && skin.mesh != null;
		if (!Ok)
		{
			return;
		}
		finger1Rest = finger1.localRotation;
		finger2Rest = finger2.localRotation;
		palmNormal = wrist.InverseTransformDirection(Vector3.ProjectOnPlane(Vector3.down, wrist.up)).normalized;
		palmOffset = new Vector3(0.008f, 0.083f, 0.011f) + palmNormal * 0.022f;
		curlAxis = Vector3.Cross(Vector3.up, palmNormal).normalized;
		int num = skin.BoneIndex("body");
		if (num < 0)
		{
			return;
		}
		Matrix4x4 matrix4x = skin.mesh.bindposes[num];
		int[] shellVerts = skin.shellVerts;
		foreach (int num2 in shellVerts)
		{
			if (skin.boneOfVertex[num2] == num)
			{
				shell.Add(matrix4x.MultiplyPoint3x4(skin.verts[num2]));
			}
		}
	}

	public void LeanForward(float weight, float groundY)
	{
		bones.RotateRaw("body", Vector3.right, 4f * weight * (MathF.PI / 180f));
		Body.rotation = Quaternion.AngleAxis(-34f * weight, Vector3.up) * Body.rotation;
		KeepAboveFloor(groundY);
	}

	public void LeanSide(float degrees, float groundY)
	{
		bones.RotateRaw("body", Vector3.forward, (0f - degrees) * (MathF.PI / 180f));
		KeepAboveFloor(groundY);
	}

	public void KeepAboveFloor(float groundY)
	{
		if (shell.Count == 0)
		{
			return;
		}
		Matrix4x4 localToWorldMatrix = Body.localToWorldMatrix;
		float num = float.PositiveInfinity;
		foreach (Vector3 item in shell)
		{
			num = Mathf.Min(num, localToWorldMatrix.MultiplyPoint3x4(item).y);
		}
		if (num < groundY + 0.009f)
		{
			Body.position += Vector3.up * (groundY + 0.009f - num);
		}
	}

	public float ShellFrontAt(Transform frame, float localY, float halfWidth, float halfHeight)
	{
		if (shell.Count == 0)
		{
			return 0.3f;
		}
		Matrix4x4 localToWorldMatrix = Body.localToWorldMatrix;
		float num = float.NegativeInfinity;
		foreach (Vector3 item in shell)
		{
			Vector3 vector = frame.InverseTransformPoint(localToWorldMatrix.MultiplyPoint3x4(item));
			if (!(Mathf.Abs(vector.y - localY) > halfHeight) && !(Mathf.Abs(vector.x) > halfWidth) && vector.z > num)
			{
				num = vector.z;
			}
		}
		if (!float.IsNegativeInfinity(num))
		{
			return num;
		}
		return 0.3f;
	}

	public float ShellRadiusAt(float worldY, Vector3 towards)
	{
		if (shell.Count == 0)
		{
			return 0.35f;
		}
		Matrix4x4 localToWorldMatrix = Body.localToWorldMatrix;
		Vector3 position = Body.position;
		towards.y = 0f;
		towards.Normalize();
		float num = 0f;
		foreach (Vector3 item in shell)
		{
			Vector3 vector = localToWorldMatrix.MultiplyPoint3x4(item);
			if (!(Mathf.Abs(vector.y - worldY) > 0.05f))
			{
				Vector3 lhs = vector - position;
				lhs.y = 0f;
				float num2 = Vector3.Dot(lhs, towards);
				if (num2 > num)
				{
					num = num2;
				}
			}
		}
		return num;
	}

	public float DistanceToShell(Vector3 point)
	{
		if (shell.Count == 0)
		{
			return float.PositiveInfinity;
		}
		Matrix4x4 localToWorldMatrix = Body.localToWorldMatrix;
		float num = float.PositiveInfinity;
		foreach (Vector3 item in shell)
		{
			num = Mathf.Min(num, (localToWorldMatrix.MultiplyPoint3x4(item) - point).sqrMagnitude);
		}
		return Mathf.Sqrt(num);
	}

	public float Reach(Vector3 center, float weight, float scoop, float curl, float itemDiameter = 0.14f)
	{
		Vector3 normalized = (shoulder.position - center).normalized;
		Vector3 normalized2 = (Vector3.ProjectOnPlane(normalized, Body.up).normalized - Body.up * 0.68f).normalized;
		Vector3 vector = Vector3.Slerp(normalized, normalized2, 0.47f * scoop);
		Vector3 vector2 = center + vector * (itemDiameter * 0.49f);
		if (weight > 0f)
		{
			Quaternion localRotation = shoulder.localRotation;
			Quaternion localRotation2 = wrist.localRotation;
			AimPalm(vector2, -vector);
			shoulder.localRotation = Quaternion.Slerp(localRotation, shoulder.localRotation, weight);
			wrist.localRotation = Quaternion.Slerp(localRotation2, wrist.localRotation, weight);
			finger1.localRotation = Quaternion.Slerp(finger1.localRotation, finger1Rest * Quaternion.AngleAxis(19f * curl, curlAxis), weight);
			finger2.localRotation = Quaternion.Slerp(finger2.localRotation, finger2Rest * Quaternion.AngleAxis(12f * curl, curlAxis), weight);
		}
		return Vector3.Distance(PalmPosition, vector2);
	}

	private void AimPalm(Vector3 contact, Vector3 surfaceNormal)
	{
		Vector3 position = shoulder.position;
		Vector3 fromDirection = wrist.position - position;
		float magnitude = fromDirection.magnitude;
		float magnitude2 = wrist.TransformVector(palmOffset).magnitude;
		Vector3 vector = contact - position;
		Vector3 normalized = vector.normalized;
		float num = Mathf.Clamp(vector.magnitude, Mathf.Abs(magnitude - magnitude2) + 0.0001f, magnitude + magnitude2 - 0.0001f);
		float num2 = (magnitude * magnitude + num * num - magnitude2 * magnitude2) / (2f * num);
		float num3 = Mathf.Sqrt(Mathf.Max(0f, magnitude * magnitude - num2 * num2));
		Vector3 normalized2 = Vector3.ProjectOnPlane(-Body.up - 0.2f * Body.forward, normalized).normalized;
		if (normalized2.sqrMagnitude < 0.01f)
		{
			normalized2 = Vector3.ProjectOnPlane(Body.right, normalized).normalized;
		}
		Vector3 vector2 = position + normalized * num2 + normalized2 * num3;
		shoulder.rotation = Quaternion.FromToRotation(fromDirection, vector2 - position) * shoulder.rotation;
		Vector3 normalized3 = (position + normalized * num - wrist.position).normalized;
		wrist.rotation = Quaternion.FromToRotation(wrist.TransformVector(palmOffset), normalized3) * wrist.rotation;
		Vector3 from = Vector3.ProjectOnPlane(wrist.TransformDirection(palmNormal), normalized3);
		Vector3 to = Vector3.ProjectOnPlane(surfaceNormal, normalized3);
		if (from.sqrMagnitude > 0.01f && to.sqrMagnitude > 0.01f)
		{
			wrist.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(from, to, normalized3), normalized3) * wrist.rotation;
		}
	}
}
