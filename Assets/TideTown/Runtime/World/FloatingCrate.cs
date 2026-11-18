using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class FloatingCrate : MonoBehaviour
{
	public float bedY;

	public float phase;

	public Vector3 size = new Vector3(0.8f, 0.55f, 0.6f);

	public float topAboveWater = 0.18f;

	public float bobAmplitude = 0.03f;

	public bool suppressed;

	private IslandWorld world;

	private BoxCollider box;

	private Renderer rend;

	private EggboyCollider entry;

	private bool floating;

	private void Start()
	{
		world = Object.FindObjectOfType<IslandWorld>();
		box = GetComponent<BoxCollider>();
		rend = GetComponent<Renderer>();
		base.transform.localScale = size;
		box.size = Vector3.one;
		if (world != null)
		{
			entry = new EggboyCollider
			{
				x = base.transform.position.x,
				z = base.transform.position.z,
				hw = size.x * 0.5f,
				hd = size.z * 0.5f
			};
			world.AddCollider(entry);
		}
		Apply(force: true);
	}

	private void OnDestroy()
	{
		if (world != null && entry != null)
		{
			world.RemoveCollider(entry);
		}
	}

	private void Update()
	{
		Apply(force: false);
	}

	private void Apply(bool force)
	{
		float staticY = WaterLevel.StaticY;
		bool flag = !suppressed && staticY > bedY + 0.01f;
		if (force || flag != floating)
		{
			floating = flag;
			if (rend != null)
			{
				rend.enabled = flag;
			}
			if (box != null)
			{
				box.enabled = flag;
			}
		}
		if (entry != null)
		{
			entry.top = (flag ? new float?(staticY + topAboveWater) : null);
			entry.hw = (flag ? new float?(size.x * 0.5f) : null);
			entry.hd = (flag ? new float?(size.z * 0.5f) : null);
		}
		if (flag)
		{
			float num = staticY + topAboveWater + Mathf.Sin(Time.time * 1.15f + phase) * bobAmplitude;
			Vector3 position = base.transform.position;
			position.y = num - size.y * 0.5f;
			base.transform.position = position;
			if (entry != null)
			{
				entry.top = num;
			}
		}
	}
}
