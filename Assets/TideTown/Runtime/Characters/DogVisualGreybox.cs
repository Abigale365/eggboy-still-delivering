using System;
using UnityEngine;

public class DogVisualGreybox : DogVisual
{
	public Transform body;

	public Transform head;

	public Transform tail;

	public Transform legFL;

	public Transform legFR;

	public Transform legBL;

	public Transform legBR;

	private Vector3 bodyBase;

	private Vector3 headBase;

	private float phase;

	private float headTilt;

	private void Awake()
	{
		if (body != null)
		{
			bodyBase = body.localPosition;
		}
		if (head != null)
		{
			headBase = head.localPosition;
		}
	}

	public override void ApplyMotion(float dt, float speed, bool trot, float wag, bool happy)
	{
		float num = Mathf.Clamp01(speed / 2.2f);
		phase += dt * (2.2f + speed * 4.2f + (trot ? 3.2f : 0f));
		float num2 = Mathf.Sin(phase * 2f) * 26f * num;
		SetLeg(legFL, num2, 0f);
		SetLeg(legBR, num2, 0f);
		SetLeg(legFR, 0f - num2, 0f);
		SetLeg(legBL, 0f - num2, 0f);
		if (body != null)
		{
			body.localPosition = bodyBase + Vector3.up * (Mathf.Abs(Mathf.Sin(phase * 2f)) * 0.016f * num);
		}
		if (head != null)
		{
			head.localPosition = headBase + Vector3.up * (Mathf.Sin(phase * 2f) * 0.008f * num);
			headTilt = Mathf.Lerp(headTilt, happy ? (-8f) : 0f, Mathf.Min(1f, dt * 6f));
			head.localRotation = Quaternion.Euler(headTilt, 0f, 0f);
		}
		if (tail != null)
		{
			float num3 = (happy ? 36f : 22f) * Mathf.Clamp01(0.5f + wag / 4f);
			float x = (happy ? (-18f) : (-5f));
			tail.localRotation = Quaternion.Euler(x, Mathf.Sin(Time.time * wag * MathF.PI * 2f) * num3, 0f);
		}
	}

	private static void SetLeg(Transform leg, float angle, float roll)
	{
		if (leg != null)
		{
			leg.localRotation = Quaternion.Euler(angle, 0f, roll);
		}
	}
}
