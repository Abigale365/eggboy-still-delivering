using UnityEngine;

public sealed class BellSwing : MonoBehaviour
{
	private Transform bell;

	private Vector3 hookLocal = new Vector3(0f, 0.99f, 0f);

	private Vector3 restPos;

	private Quaternion restRot;

	private float angle;

	private float speed;

	public Vector3 TouchPoint => base.transform.TransformPoint(new Vector3(0f, 0.76f, 0f));

	public int RingCount { get; private set; }

	private void Awake()
	{
		bell = base.transform.Find("A5_GLB");
		if (bell != null)
		{
			restRot = bell.localRotation;
			restPos = bell.localPosition;
		}
	}

	public void Ring()
	{
		RingCount++;
		speed += 260f;
		ShadowAndChimeSystem.PlayProceduralChime(1567.98f, 0.18f);
		ShadowAndChimeSystem.PlayProceduralChime(2093f, 0.12f);
	}

	private void Update()
	{
		if (!(bell == null) && (!(Mathf.Abs(angle) < 0.01f) || !(Mathf.Abs(speed) < 0.01f)))
		{
			float num = Mathf.Min(0.05f, Time.deltaTime);
			speed += ((0f - angle) * 90f - speed * 3.2f) * num;
			angle += speed * num;
			bell.localRotation = restRot;
			bell.localPosition = restPos;
			bell.RotateAround(base.transform.TransformPoint(hookLocal), base.transform.right, angle);
		}
	}
}
