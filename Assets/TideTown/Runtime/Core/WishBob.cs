using UnityEngine;

public sealed class WishBob : MonoBehaviour
{
	private float baseY;

	private float phase;

	private void Start()
	{
		baseY = base.transform.position.y;
		phase = Random.value * 6.28f;
	}

	private void Update()
	{
		Vector3 position = base.transform.position;
		position.y = Mathf.Max(baseY, WaterLevel.StaticY - 0.05f) + Mathf.Sin(Time.time * 1.7f + phase) * 0.025f;
		base.transform.position = position;
	}
}
