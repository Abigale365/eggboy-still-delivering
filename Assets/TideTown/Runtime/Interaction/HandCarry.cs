using UnityEngine;

[DefaultExecutionOrder(150)]
public sealed class HandCarry : MonoBehaviour
{
	public float pourTarget;

	private EggboyPlayer player;

	private Transform hand;

	private PickupGrip grip;

	private Vector3 handleLocal;

	private Vector3 centerLocal;

	private float heightLocal;

	private float radiusLocal;

	private Vector3 baseScale;

	private float scale = 1f;

	private int frames;

	private Vector3 lastPalm;

	private Vector2 tilt;

	private Vector2 tiltVel;

	private float pour;

	private float outward;

	public float Scale => scale;

	public Vector3 HandlePoint => base.transform.TransformPoint(handleLocal);

	public float PalmError { get; private set; }

	public float ShellGap { get; private set; } = 1f;


	public float GroundGap { get; private set; } = 1f;


	public void Init(EggboyPlayer p)
	{
		player = p;
		hand = ((p.Bones != null) ? p.Bones.Bone("handR") : null);
		grip = ((PhysicalPickupController.Instance != null) ? PhysicalPickupController.Instance.Grip : null);
		baseScale = base.transform.localScale;
		base.transform.rotation = Quaternion.identity;
		Bounds bounds = LocalBounds(base.gameObject, base.transform);
		heightLocal = bounds.size.y;
		radiusLocal = Mathf.Max(bounds.extents.x, bounds.extents.z);
		centerLocal = bounds.center;
		Vector3 zero = Vector3.zero;
		int num = 0;
		MeshFilter[] componentsInChildren = GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			Vector3[] array = MeshPoints(meshFilter.sharedMesh);
			foreach (Vector3 position in array)
			{
				Vector3 vector = base.transform.InverseTransformPoint(meshFilter.transform.TransformPoint(position));
				if (vector.y > bounds.max.y - heightLocal * 0.14f)
				{
					zero += vector;
					num++;
				}
			}
		}
		handleLocal = ((num > 0) ? (zero / num) : new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
		handleLocal.y = bounds.max.y - heightLocal * 0.04f;
		lastPalm = Palm();
	}

	private Vector3 Palm()
	{
		if (grip == null)
		{
			if (!(hand != null))
			{
				return player.transform.position + Vector3.up * 0.3f;
			}
			return hand.position;
		}
		return grip.PalmPosition;
	}

	private void LateUpdate()
	{
		if (!(player == null))
		{
			float num = Mathf.Max(0.0001f, Mathf.Min(0.05f, Time.deltaTime));
			Vector3 vector = Palm();
			float y = player.transform.position.y;
			if (frames < 12)
			{
				frames++;
				float num2 = vector.y - y - 0.12f;
				float num3 = heightLocal * baseScale.y;
				float num4 = Mathf.Clamp(num2 / Mathf.Max(0.01f, num3 * 0.96f), 0.45f, 1f);
				scale = ((frames == 1) ? num4 : Mathf.Min(scale, num4));
				base.transform.localScale = baseScale * scale;
			}
			Vector3 lhs = (vector - lastPalm) / num;
			lastPalm = vector;
			Vector3 forward = player.transform.forward;
			Vector3 right = player.transform.right;
			Vector2 vector2 = new Vector2(Mathf.Clamp((0f - Vector3.Dot(lhs, forward)) * 9f, -16f, 16f), Mathf.Clamp(Vector3.Dot(lhs, right) * 9f, -12f, 12f));
			tiltVel += (vector2 - tilt) * 70f * num - tiltVel * 9f * num;
			tilt += tiltVel * num;
			pour = Mathf.MoveTowards(pour, pourTarget, 170f * num);
			Quaternion rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f) * Quaternion.Euler(tilt.x + pour, 0f, tilt.y);
			base.transform.rotation = rotation;
			base.transform.position = vector - base.transform.TransformVector(handleLocal);
			float num5 = 0f;
			if (grip != null)
			{
				Vector3 vector3 = base.transform.TransformPoint(centerLocal);
				float num6 = grip.ShellRadiusAt(vector3.y, right);
				float num7 = radiusLocal * baseScale.x * scale;
				float num8 = Vector3.Dot(vector3 - grip.Body.position, right);
				num5 = Mathf.Max(0f, num6 + num7 + 0.02f - num8);
				ShellGap = num8 - num7 - num6;
			}
			outward = ((num5 > outward) ? num5 : Mathf.MoveTowards(outward, num5, 0.25f * num));
			base.transform.position += right * outward;
			if (grip != null)
			{
				ShellGap = Vector3.Dot(base.transform.TransformPoint(centerLocal) - grip.Body.position, right) - radiusLocal * baseScale.x * scale - grip.ShellRadiusAt(base.transform.TransformPoint(centerLocal).y, right);
			}
			float y2 = Lux3DModelFit.WorldBounds(base.gameObject).min.y;
			if (y2 < y + 0.02f)
			{
				base.transform.position += Vector3.up * (y + 0.02f - y2);
			}
			PalmError = Vector3.Distance(HandlePoint, vector);
			GroundGap = Lux3DModelFit.WorldBounds(base.gameObject).min.y - y;
		}
	}

	private static Vector3[] MeshPoints(Mesh m)
	{
		if (m == null)
		{
			return new Vector3[0];
		}
		if (m.isReadable)
		{
			return m.vertices;
		}
		Bounds bounds = m.bounds;
		Vector3[] array = new Vector3[8];
		for (int i = 0; i < 8; i++)
		{
			array[i] = new Vector3(((i & 1) == 0) ? bounds.min.x : bounds.max.x, ((i & 2) == 0) ? bounds.min.y : bounds.max.y, ((i & 4) == 0) ? bounds.min.z : bounds.max.z);
		}
		return array;
	}

	public static Bounds LocalBounds(GameObject go, Transform frame)
	{
		bool flag = false;
		Bounds result = default(Bounds);
		MeshFilter[] componentsInChildren = go.GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			if (meshFilter.sharedMesh == null)
			{
				continue;
			}
			Vector3[] array = MeshPoints(meshFilter.sharedMesh);
			foreach (Vector3 position in array)
			{
				Vector3 vector = frame.InverseTransformPoint(meshFilter.transform.TransformPoint(position));
				if (!flag)
				{
					result = new Bounds(vector, Vector3.zero);
					flag = true;
				}
				else
				{
					result.Encapsulate(vector);
				}
			}
		}
		return result;
	}
}
