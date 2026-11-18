public class Gesture
{
	public float lean;

	public float tilt;

	public float twist;

	public float drop;

	public float[] armR;

	public float[] armL;

	public float[] handR;

	public float[] handL;

	public Gesture(PoseDef b)
	{
		lean = b.lean;
		tilt = b.tilt;
		twist = b.twist;
		drop = b.drop;
		armR = new float[2]
		{
			b.armR[0],
			b.armR[1]
		};
		armL = new float[2]
		{
			b.armL[0],
			b.armL[1]
		};
		handR = new float[2]
		{
			b.handR[0],
			b.handR[1]
		};
		handL = new float[2]
		{
			b.handL[0],
			b.handL[1]
		};
	}
}
