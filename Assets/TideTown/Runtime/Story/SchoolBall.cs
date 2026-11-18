using System;
using System.Collections;
using UnityEngine;

public sealed class SchoolBall : MonoBehaviour
{
	public Vector3 BowlPoint;

	private bool inBowl;

	private Vector3 initialPos;

	private Quaternion initialRot;

	private bool capturedInitial;

	public bool InBowl => inBowl;

	private void Awake()
	{
		CaptureInitial();
	}

	private void CaptureInitial()
	{
		if (!capturedInitial)
		{
			capturedInitial = true;
			initialPos = base.transform.position;
			initialRot = base.transform.rotation;
		}
	}

	public void Take(Transform player)
	{
		CaptureInitial();
		inBowl = false;
		PhysicalPickupController instance = PhysicalPickupController.Instance;
		if (instance != null && instance.Active(null))
		{
			instance.AttachCarryBall(this);
			return;
		}
		base.transform.SetParent(player, worldPositionStays: false);
		base.transform.localPosition = new Vector3(0.26f, 0.86f, 0.2f);
		base.transform.localRotation = Quaternion.identity;
	}

	private static void ReleaseFromHand(SchoolBall ball)
	{
		PhysicalPickupController instance = PhysicalPickupController.Instance;
		if (instance != null)
		{
			instance.ReleaseCarryBall(ball);
		}
	}

	public void ReturnToSwing()
	{
		CaptureInitial();
		ReleaseFromHand(this);
		inBowl = false;
		base.transform.SetParent(null, worldPositionStays: true);
		base.transform.position = initialPos;
		base.transform.rotation = initialRot;
	}

	public void RollIntoBowl()
	{
		ReleaseFromHand(this);
		base.transform.SetParent(null, worldPositionStays: true);
		inBowl = true;
		StartCoroutine(Roll());
	}

	private IEnumerator Roll()
	{
		Vector3 start = base.transform.position;
		Vector3 ground = new Vector3(start.x, BowlPoint.y, start.z);
		for (float elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
		{
			float num = elapsed / 0.6f;
			Vector3 vector = ((num < 0.25f) ? Vector3.Lerp(start, ground, num / 0.25f) : Vector3.Lerp(ground, BowlPoint, (num - 0.25f) / 0.75f));
			float num2 = ((num >= 0.25f) ? (Mathf.Abs(Mathf.Sin((num - 0.25f) / 0.75f * MathF.PI * 2f)) * (1f - num) * 0.16f) : 0f);
			base.transform.position = vector + Vector3.up * num2;
			base.transform.Rotate(Vector3.right, 540f * Time.deltaTime, Space.World);
			yield return null;
		}
		PlaceInBowl();
	}

	public void PlaceInBowl()
	{
		ReleaseFromHand(this);
		base.transform.SetParent(null, worldPositionStays: true);
		base.transform.position = BowlPoint;
		base.transform.localRotation = Quaternion.identity;
		inBowl = true;
	}
}
