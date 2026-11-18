using UnityEngine;

public sealed class TideFloat : MonoBehaviour
{
	public float offset;

	public float bob = 0.04f;

	public float roll = 1.2f;

	private float phase;

	private Quaternion baseRot;

	private void Awake()
	{
		phase = (base.transform.position.x * 0.37f + base.transform.position.z * 0.21f) % 6.28f;
		baseRot = base.transform.rotation;
	}

	private void LateUpdate()
	{
		float time = Time.time;
		Vector3 position = base.transform.position;
		position.y = WaterLevel.StaticY + offset + Mathf.Sin(time * 0.9f + phase) * bob;
		base.transform.position = position;
		base.transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(time * 0.7f + phase) * roll * 0.4f, 0f, Mathf.Sin(time * 0.8f + phase * 1.3f) * roll);
	}
}
