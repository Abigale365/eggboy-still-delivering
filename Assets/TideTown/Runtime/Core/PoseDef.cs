using System;

public class PoseDef
{
	public float lean;

	public float tilt;

	public float twist;

	public float drop;

	public float[] armR;

	public float[] armL;

	public float[] handR;

	public float[] handL;

	public float ant;

	public float bob;

	public float bobHz;

	public float sway;

	public float swayHz;

	public float shiver;

	public Action<float, Gesture> gesture;

	public PoseDef()
	{
		lean = 0f;
		tilt = 0f;
		twist = 0f;
		drop = 0f;
		armR = new float[2] { -30f, 12f };
		armL = new float[2] { -24f, 18f };
		handR = new float[2] { 6f, 14f };
		handL = new float[2] { 6f, 14f };
		ant = 0f;
		bob = 0.004f;
		bobHz = 0.25f;
		sway = 2.2f;
		swayHz = 0.12f;
		shiver = 0f;
	}
}
