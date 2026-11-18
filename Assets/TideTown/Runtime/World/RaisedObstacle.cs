using UnityEngine;

public sealed class RaisedObstacle : MonoBehaviour
{
	public float minY = 16.6f;

	private IslandWorld world;

	private EggboyCollider entry;

	private void Start()
	{
		world = Object.FindObjectOfType<IslandWorld>();
		BoxCollider component = GetComponent<BoxCollider>();
		if (!(world == null) && !(component == null))
		{
			Transform transform = base.transform;
			Vector3 vector = transform.TransformPoint(component.center);
			Vector3 vector2 = Vector3.Scale(component.size, transform.lossyScale);
			entry = new EggboyCollider
			{
				x = vector.x,
				z = vector.z,
				hw = vector2.x * 0.5f,
				hd = vector2.z * 0.5f,
				minY = minY
			};
			world.AddCollider(entry);
		}
	}

	private void OnDestroy()
	{
		if (world != null && entry != null)
		{
			world.RemoveCollider(entry);
		}
	}
}
