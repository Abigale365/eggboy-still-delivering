using UnityEngine;

public static class Lux3DModelFit
{
	public static Bounds WorldBounds(GameObject go)
	{
		Renderer[] componentsInChildren = go.GetComponentsInChildren<Renderer>(includeInactive: true);
		if (componentsInChildren.Length == 0)
		{
			return new Bounds(go.transform.position, Vector3.zero);
		}
		Bounds bounds = componentsInChildren[0].bounds;
		for (int i = 1; i < componentsInChildren.Length; i++)
		{
			bounds.Encapsulate(componentsInChildren[i].bounds);
		}
		return bounds;
	}

	public static void Fit(GameObject model, float size, string up)
	{
		Transform transform = model.transform;
		transform.position = Vector3.zero;
		transform.rotation = Quaternion.identity;
		transform.localScale = Vector3.one;
		Vector3 size2 = WorldBounds(model).size;
		if (size2.x <= 1E-05f && size2.y <= 1E-05f && size2.z <= 1E-05f)
		{
			return;
		}
		int num = 1;
		if (up == "thin")
		{
			num = MinAxis(size2);
		}
		else if (up == "long")
		{
			num = MaxAxis(size2);
		}
		Quaternion localRotation = Quaternion.identity;
		switch (num)
		{
		case 0:
			localRotation = Quaternion.Euler(0f, 0f, 90f);
			break;
		case 2:
			localRotation = Quaternion.Euler(-90f, 0f, 0f);
			break;
		}
		Transform transform2 = new GameObject("Fit").transform;
		transform2.SetParent(transform, worldPositionStays: false);
		for (int num2 = transform.childCount - 1; num2 >= 0; num2--)
		{
			Transform child = transform.GetChild(num2);
			if (child != transform2)
			{
				child.SetParent(transform2, worldPositionStays: true);
			}
		}
		transform2.localRotation = localRotation;
		Bounds bounds = WorldBounds(model);
		float num3 = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
		float num4 = ((num3 > 1E-05f && size > 1E-05f) ? (size / num3) : 1f);
		transform2.localScale = Vector3.one * num4;
		bounds = WorldBounds(model);
		transform2.localPosition = new Vector3(0f - bounds.center.x, 0f - bounds.min.y, 0f - bounds.center.z);
	}

	private static int MinAxis(Vector3 s)
	{
		if (!(s.x <= s.y) || !(s.x <= s.z))
		{
			if (!(s.y <= s.z))
			{
				return 2;
			}
			return 1;
		}
		return 0;
	}

	private static int MaxAxis(Vector3 s)
	{
		if (!(s.x >= s.y) || !(s.x >= s.z))
		{
			if (!(s.y >= s.z))
			{
				return 2;
			}
			return 1;
		}
		return 0;
	}

	public static void AddWalkableBox(GameObject root, GameObject model)
	{
		if (!(root == null) && !(model == null) && !(root.GetComponent<BoxCollider>() != null))
		{
			Quaternion rotation = root.transform.rotation;
			root.transform.rotation = Quaternion.identity;
			Bounds bounds = WorldBounds(model);
			int num = LayerMask.NameToLayer("Ground");
			root.layer = ((num >= 0) ? num : 6);
			BoxCollider boxCollider = root.AddComponent<BoxCollider>();
			Vector3 vector = root.transform.InverseTransformPoint(bounds.center);
			boxCollider.center = new Vector3(vector.x, bounds.size.y * 0.5f, vector.z);
			boxCollider.size = new Vector3(Mathf.Max(0.45f, bounds.size.x * 0.92f), Mathf.Max(0.12f, bounds.size.y * 0.92f), Mathf.Max(0.45f, bounds.size.z * 0.92f));
			root.transform.rotation = rotation;
			Physics.SyncTransforms();
		}
	}
}
