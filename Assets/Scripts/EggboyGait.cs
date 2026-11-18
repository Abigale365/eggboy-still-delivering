using System;
using UnityEngine;

public class EggboyGait
{
	public static class Tune
	{
		public const float Kick = 0.015f;

		public const float KickPitch = 55f;

		public const float KickBack = 0.03f;

		public const float Delay = 0.15f;

		public static readonly float[] Lower = new float[2] { 24f, 12f };

		public static readonly float[] LowerVar = new float[2] { 4f, 0f };

		public static readonly float[] Swing = new float[2] { 24f, 50f };

		public static readonly float[] Bias = new float[2] { 3f, 8f };

		public static readonly float[] Bend = new float[2] { 14f, 55f };

		public const float BendFwd = 16f;

		public const float Inward = 0f;

		public const float BagClear = 6f;

		public static readonly float[] Cheer = new float[3] { 170f, 30f, 10f };
	}

	private class Spring
	{
		public float x;

		public float v;

		public float k;

		public float damp;

		public Spring(float k, float damp)
		{
			this.k = k;
			this.damp = damp;
		}

		public float Step(float target, float dt, float impulse = 0f)
		{
			v += impulse;
			float num = 2f * Mathf.Sqrt(k) * damp;
			v += (k * (target - x) - num * v) * dt;
			x += v * dt;
			return x;
		}
	}

	private static readonly Vector3 AX = new Vector3(1f, 0f, 0f);

	private static readonly Vector3 AY = new Vector3(0f, 1f, 0f);

	private static readonly Vector3 AZ = new Vector3(0f, 0f, 1f);

	private const float G = 9.8f;

	private const float RANGE_WALK = 0.12f;

	private const float RANGE_RUN = 0.14f;

	private const float LIFT_WALK = 0.022f;

	private const float LIFT_RUN = 0.03f;

	private const float HOP_MAX = 0.035f;

	private const float SINK = 0.009f;

	private const float TOE = 0.074f;

	private const float HEEL = 0.08f;

	private const float SOLE = 0.085f;

	private static readonly float[] FIST = new float[2] { 65f, 60f };

	public float w;

	public float amp;

	public float phase;

	public float duty = 1f;

	public float rootY;

	public float rootVy;

	public float accY;

	public float jw;

	private JumpPhase prevJump;

	private readonly LegParams[] legG = new LegParams[2]
	{
		new LegParams(),
		new LegParams()
	};

	private readonly LegParams[] legJ = new LegParams[2]
	{
		new LegParams(),
		new LegParams()
	};

	private float sinceLand = 9f;

	private float impact;

	public readonly FootState[] feet = new FootState[2]
	{
		new FootState(),
		new FootState()
	};

	private readonly EggboyBones bones;

	private readonly EggboySkin skin;

	private float legLen = 0.155f;

	private readonly float scale;

	private readonly Spring bagX = new Spring(60f, 0.35f);

	private readonly Spring bagZ = new Spring(60f, 0.35f);

	private float prevVy;

	private bool wasFlying;

	private readonly Vector3 bagAxis;

	private readonly float[] pos = new float[2];

	private readonly float[] from = new float[2];

	private readonly Vector3[] du = new Vector3[2];

	private readonly Vector3[] curl = new Vector3[2];

	private Vector3 elbowDir;

	private Vector3 elbowAxis;

	private readonly float[] fistApplied = new float[2];

	public bool Ok
	{
		get
		{
			if (bones.Has("legL") && bones.Has("legR") && bones.Has("footL") && bones.Has("footR") && bones.Has("body"))
			{
				return bones.Has("root");
			}
			return false;
		}
	}

	private static float Smooth(float a, float b, float x)
	{
		float num = Mathf.Clamp01((x - a) / (b - a));
		return num * num * (3f - 2f * num);
	}

	private static float Frac(float x)
	{
		return x - Mathf.Floor(x);
	}

	private static float D(float deg)
	{
		return deg * (MathF.PI / 180f);
	}

	public EggboyGait(EggboyBones bones, EggboySkin skin, Transform model)
	{
		this.bones = bones;
		this.skin = skin;
		scale = ((model.localScale.x > 0f) ? model.localScale.x : 1f);
		Transform transform = bones.Get("footR")?.bone;
		if (transform != null)
		{
			legLen = transform.localPosition.magnitude;
		}
		bagAxis = FindBagAxis(skin, bones, model) ?? AZ;
		for (int i = 0; i < 2; i++)
		{
			LegSide legSide = (LegSide)i;
			Transform transform2 = bones.Get("arm" + legSide)?.bone;
			Transform transform3 = bones.Get("finger1" + legSide)?.bone;
			if (transform2 == null || transform3 == null)
			{
				du[i] = ((i == 0) ? AX : (-AX));
				curl[i] = AZ;
				continue;
			}
			Vector3 vector = model.InverseTransformPoint(transform2.position);
			Vector3 normalized = (model.InverseTransformPoint(transform3.position) - vector).normalized;
			du[i] = normalized;
			Vector3 normalized2 = (new Vector3(0f, -1f, 0f) + normalized * normalized.y).normalized;
			curl[i] = Vector3.Cross(normalized, normalized2).normalized;
		}
	}

	private void Rot(string name, Vector3 axis, float rad)
	{
		bones.Rotate(name, axis, rad);
	}

	private void Move(string name, float x, float y, float z)
	{
		bones.Move(name, x, y, z);
	}

	private static Vector3? FindBagAxis(EggboySkin skin, EggboyBones bones, Transform model)
	{
		if (skin == null || skin.leatherVerts.Length == 0)
		{
			return null;
		}
		int num = skin.BoneIndex("bag");
		if (num < 0)
		{
			return null;
		}
		_ = skin.renderer.bones[num];
		Vector3 position = skin.mesh.bindposes[num].inverse.MultiplyPoint3x4(Vector3.zero);
		Vector3 vector = model.InverseTransformPoint(skin.renderer.transform.TransformPoint(position));
		Vector3 zero = Vector3.zero;
		Vector3 zero2 = Vector3.zero;
		int num2 = 0;
		int num3 = 0;
		int[] leatherVerts = skin.leatherVerts;
		foreach (int num4 in leatherVerts)
		{
			BoneWeight boneWeight = skin.weights[num4];
			float num5 = 0f;
			if (boneWeight.boneIndex0 == num)
			{
				num5 += boneWeight.weight0;
			}
			if (boneWeight.boneIndex1 == num)
			{
				num5 += boneWeight.weight1;
			}
			if (boneWeight.boneIndex2 == num)
			{
				num5 += boneWeight.weight2;
			}
			if (boneWeight.boneIndex3 == num)
			{
				num5 += boneWeight.weight3;
			}
			if (!(num5 < 0.2f) && !(num5 > 0.95f))
			{
				Vector3 vector2 = model.InverseTransformPoint(skin.renderer.transform.TransformPoint(skin.verts[num4]));
				if (vector2.z > vector.z)
				{
					zero += vector2;
					num2++;
				}
				else
				{
					zero2 += vector2;
					num3++;
				}
			}
		}
		if (num2 < 5 || num3 < 5)
		{
			return null;
		}
		return (zero / num2 - zero2 / num3).normalized;
	}

	private void Leg(LegSide s, float fwd, float up, float pitch, float k)
	{
		if (!(k < 0.0001f))
		{
			float num = Mathf.Asin(Mathf.Clamp(fwd / legLen, -0.9f, 0.9f));
			float num2 = legLen * (1f - Mathf.Cos(num));
			float num3 = Mathf.Max(0f, (pitch > 0f) ? (0.074f * Mathf.Sin(pitch) - 0.085f * (1f - Mathf.Cos(pitch))) : (0.08f * Mathf.Sin(0f - pitch) - 0.085f * (1f - Mathf.Cos(pitch))));
			Rot("leg" + s, AX, (0f - num) * k);
			Move("leg" + s, 0f, (up - num2 + num3) * k, 0f);
			Rot("foot" + s, AX, (num + pitch) * k);
		}
	}

	private void Arm(LegSide s, float fw, float swing, float bias, float lower, float bend, float k)
	{
		if (!(k < 0.0001f))
		{
			float num = ((s == LegSide.R) ? lower : (0f - lower)) * k;
			elbowDir = du[(int)s];
			elbowDir = Quaternion.AngleAxis((0f - num) * 57.29578f, AZ) * elbowDir;
			elbowAxis = Vector3.Cross(elbowDir, AZ).normalized;
			bones.RotateRaw("arm" + s, elbowAxis, (swing * fw + bias) * k);
			Rot("arm" + s, AZ, num);
			float num2 = 0f;
			elbowDir = new Vector3((s == LegSide.R) ? num2 : (0f - num2), 0f, 1f).normalized;
			elbowAxis = Vector3.Cross(du[(int)s], elbowDir).normalized;
			bones.RotateRaw("hand" + s, elbowAxis, bend * k);
		}
	}

	public void Grip(LegSide s, float k)
	{
		float num = k - fistApplied[(int)s];
		if (num > 0f)
		{
			Fist(s, num);
		}
	}

	private void Fist(LegSide s, float k)
	{
		fistApplied[(int)s] += k;
		bones.RotateRaw("finger1" + s, curl[(int)s], D(FIST[0]) * k);
		bones.RotateRaw("finger2" + s, curl[(int)s], D(FIST[1]) * k);
	}

	private void Land(LegSide lead, float range)
	{
		LegSide legSide = ((lead == LegSide.L) ? LegSide.R : LegSide.L);
		phase = ((lead == LegSide.L) ? 0f : 0.5f);
		pos[(int)lead] = range * 0.3f;
		pos[(int)legSide] = (0f - range) * 0.45f;
		feet[(int)lead].stance = true;
		feet[(int)legSide].stance = true;
	}

	private void ScaleBody(Transform body, Vector3 factor)
	{
		body.localScale = new Vector3(body.localScale.x * factor.x, body.localScale.y * factor.y, body.localScale.z * factor.z);
	}

	private void JumpPose(JumpState J, Transform body)
	{
		float num = jw;
		float num2 = Mathf.Clamp01(impact / 4.5f) * Mathf.Exp((0f - sinceLand) * 13f);
		float num3 = 0.2f * num2 * Mathf.Cos(sinceLand * 19f);
		if (Mathf.Abs(num3) > 0.0001f)
		{
			ScaleBody(body, new Vector3(1f + num3 * 0.2f, 1f - num3, 1f + num3 * 0.35f));
			Move("body", 0f, -0.018f * num2 * Mathf.Max(0f, Mathf.Cos(sinceLand * 19f)), 0f);
		}
		if (num < 0.001f)
		{
			return;
		}
		float num4 = ((J.dur > 0f) ? J.dur : 1f);
		float num5 = Mathf.Clamp01(J.t / num4);
		float num6 = ((J.phase == JumpPhase.crouch) ? Mathf.Sin(MathF.PI / 2f * num5) : 0f);
		float num7 = ((J.phase == JumpPhase.air) ? Smooth(0f, 0.07f, J.t) : ((J.phase == JumpPhase.land) ? 1f : 0f));
		float num8 = ((J.phase == JumpPhase.air && J.vy0 > 0f) ? Mathf.Clamp01(J.vy / J.vy0) : 0f);
		float num9 = ((J.phase == JumpPhase.air) ? Mathf.Clamp01((0f - J.vy) / Mathf.Max(J.vy0, 2.5f)) : ((J.phase == JumpPhase.land) ? 1f : 0f));
		float num10 = num7 * (1f - num8) * (1f - num9);
		float num11 = ((J.phase == JumpPhase.land) ? (1f - Smooth(0f, 1f, J.t / num4)) : 0f);
		float num12 = (J.moving ? 0.08f : 0.16f) * num6 - 0.13f * num8 * num7 - 0.04f * num9 * num7 * (1f - num11);
		ScaleBody(body, new Vector3(1f + num12 * 0.5f * num, 1f - num12 * num, 1f + num12 * 0.5f * num));
		Move("body", 0f, -0.012f * num6 * num, 0f);
		if (!J.moving)
		{
			Rot("body", AX, D(10f * num6 - 7f * num10 + 4f * num9 * num7 + 8f * num11) * num);
			float num13 = num7 * (1f - Smooth(0.35f, 0.9f, num9));
			for (int i = 0; i < 2; i++)
			{
				legJ[i] = new LegParams
				{
					fwd = 0f,
					up = 0.03f * num13,
					pitch = D(25f) * num13,
					k = num
				};
			}
			float x = ((J.phase == JumpPhase.air) ? J.t : 0f);
			float num14 = ((J.phase == JumpPhase.crouch) ? num6 : ((J.phase == JumpPhase.air) ? (1f - Smooth(0f, 0.1f, x)) : 0f));
			float num15 = ((J.phase == JumpPhase.air) ? (Smooth(0.02f, 0.18f, x) * (1f - 0.5f * Smooth(0.5f, 1f, num9))) : ((J.phase == JumpPhase.land) ? 0.5f : 0f)) * (1f - 0.5f * num11);
			float deg = Tune.Cheer[0];
			float deg2 = Tune.Cheer[1];
			float deg3 = Tune.Cheer[2];
			for (int j = 0; j < 2; j++)
			{
				LegSide legSide = (LegSide)j;
				float num16 = ((legSide == LegSide.R) ? 1f : (-1f));
				Rot("arm" + legSide, AX, (D(35f) * num14 - D(deg) * num15) * num);
				Rot("arm" + legSide, AZ, (0f - num16) * D(deg2) * num15 * num);
				Arm(legSide, 0f, 0f, 0f, 0f, D(deg3) * num15 + D(15f) * num14, num);
				Fist(legSide, num);
			}
		}
		else
		{
			float num17 = Mathf.Lerp(0.7f, 1f, J.run);
			LegSide lead = J.lead;
			LegSide legSide2 = ((lead == LegSide.L) ? LegSide.R : LegSide.L);
			Rot("body", AX, D((6f * num6 + 14f * num7 * (1f - num11) + 8f * num11) * num17) * num);
			float num18 = ((lead == LegSide.L) ? 1f : (-1f));
			Rot("body", AY, D(8f) * num18 * num17 * num7 * num);
			float num19 = 1f - Smooth(0.4f, 0.95f, num9);
			legJ[(int)lead] = new LegParams
			{
				fwd = 0.075f * num17,
				up = 0.035f * num17 * num19,
				pitch = (0f - D(12f)) * (1f - Smooth(0.6f, 1f, num9)),
				k = num * num7
			};
			legJ[(int)legSide2] = new LegParams
			{
				fwd = Mathf.Lerp(-0.085f, -0.05f, Smooth(0.5f, 1f, num9)) * num17,
				up = 0.03f * num17,
				pitch = D(55f) * num17 * (1f - 0.5f * Smooth(0.5f, 1f, num9)),
				k = num * num7
			};
			Arm(LegSide.R, num18, D(55f) * num17, D(8f), D(12f), D(55f) + D(16f) * num18 * num17, num * num7);
			Fist(LegSide.R, num);
			Arm(LegSide.L, 0f - num18, D(55f) * num17, D(8f), D(12f) + ((num18 > 0f) ? (0f - D(6f)) : 0f), D(55f) - D(16f) * num18 * num17, num * num7);
			Fist(LegSide.L, num);
		}
	}

	public void Update(float dt, GaitContext ctx)
	{
		fistApplied[0] = (fistApplied[1] = 0f);
		if (!Ok || dt <= 0f)
		{
			return;
		}
		float num = ctx.speed / scale;
		bool flag = ctx.speed > 0.05f;
		float run = Smooth(0.9f, 1.8f, ctx.speed);
		float num2 = (ctx.emoting ? 0f : Smooth(0.03f, 0.25f, ctx.speed));
		amp += (num2 - amp) * (1f - Mathf.Exp((0f - dt) * 10f));
		bool flag2 = !flag && feet[0].stance && feet[1].stance && Mathf.Abs(pos[0]) < 0.003f && Mathf.Abs(pos[1]) < 0.003f;
		float num3 = (ctx.emoting ? 0f : ((flag || !flag2) ? 1f : 0f));
		w += (num3 - w) * (1f - Mathf.Exp((0f - dt) * 12f));
		JumpState jump = ctx.jump;
		float num4 = 0f;
		if (jump != null && jump.phase != 0)
		{
			float x2 = ((jump.dur > 0f) ? Mathf.Clamp01(jump.t / jump.dur) : 1f);
			num4 = ((jump.phase == JumpPhase.crouch) ? Smooth(0f, 1f, x2) : ((jump.phase == JumpPhase.air) ? 1f : (jump.moving ? (1f - Smooth(0f, 1f, x2)) : (1f - Smooth(0.35f, 1f, x2)))));
		}
		jw += (num4 - jw) * (1f - Mathf.Exp((0f - dt) * 30f));
		float num5 = 1f - jw;
		float num6 = Mathf.Lerp(0.12f, 0.14f, run);
		bool flag3 = jump != null && prevJump == JumpPhase.air && jump.phase == JumpPhase.land;
		if (flag3)
		{
			sinceLand = 0f;
			impact = jump.impact;
			if (jump.moving)
			{
				Land(jump.lead, num6);
			}
		}
		prevJump = jump?.phase ?? JumpPhase.none;
		sinceLand += dt;
		float num7 = w * num5;
		float num8 = Mathf.Lerp(5.6f, 5.2f, Smooth(0.4f, 2f, ctx.speed));
		float num9 = 2f / num8;
		if (num7 > 0.001f && !flag2)
		{
			phase = Frac(phase + dt / num9);
		}
		float num10 = Mathf.Min(0.56f * num9, num6 / Mathf.Max(num, 0.01f));
		duty = num10 / num9;
		float num11 = num * num10;
		float num12 = Mathf.Lerp(0.022f, 0.03f, run);
		for (int i = 0; i < 2; i++)
		{
			float num13 = ((i == 0) ? 0f : 0.5f);
			float num14 = Frac(phase + num13);
			FootState footState = feet[i];
			float num15;
			float pitch;
			if (num14 < duty)
			{
				float x3 = num14 / duty;
				pos[i] = Mathf.Clamp(pos[i] - num * dt, (0f - num6) * 0.75f, num6 * 0.75f);
				num15 = 0f;
				pitch = (D(22f) * run * Smooth(0.65f, 1f, x3) + D(8f) * (1f - run) * Smooth(0.75f, 1f, x3)) * amp;
				footState.stance = true;
			}
			else
			{
				if (footState.stance)
				{
					from[i] = pos[i];
				}
				float num16 = (num14 - duty) / (1f - duty);
				float num17 = num11 / 2f;
				float num18 = Mathf.Min(1f, Mathf.Abs(num17 - from[i]) / 0.03f);
				float num19 = run * Mathf.Sin(MathF.PI * Mathf.Clamp01(num16 / 0.6f)) * num18;
				pos[i] = Mathf.Lerp(from[i], num17, Mathf.Lerp(Smooth(0f, 1f, num16), Smooth(0.15f, 0.95f, num16), run)) - 0.03f * num19;
				num15 = num12 * num18 * Mathf.Pow(Mathf.Sin(MathF.PI * num16), 0.85f) + 0.015f * num19;
				float a = (D(8f) * (1f - Smooth(0f, 0.35f, num16)) - D(10f) * Smooth(0.45f, 0.8f, num16) * (1f - Smooth(0.85f, 1f, num16))) * num18;
				float b = Mathf.Max(D(22f) * (1f - Smooth(0f, 0.3f, num16)) * num18, D(55f) * num19) - D(14f) * Smooth(0.6f, 0.85f, num16) * (1f - Smooth(0.9f, 1f, num16)) * num18;
				pitch = Mathf.Lerp(a, b, run);
				footState.stance = false;
			}
			footState.fwd = pos[i] * scale;
			footState.lift = num15 * scale;
			legG[i] = new LegParams
			{
				fwd = pos[i],
				up = num15,
				pitch = pitch,
				k = w
			};
		}
		float num20 = Mathf.Max(0f, 0.5f - duty) * num9;
		float num21 = 0f;
		if (num20 > 0.005f)
		{
			float num22 = Mathf.Min(0.035f, 9.8f / scale * num20 * num20 / 8f);
			float num23 = Frac(phase * 2f);
			float num24 = 2f * duty;
			num21 = ((num23 < num24) ? (-0.009f * Mathf.Sin(MathF.PI * num23 / num24) * run) : (num22 * 4f * ((num23 - num24) / (1f - num24)) * (1f - (num23 - num24) / (1f - num24))));
		}
		num21 *= num7;
		float num25 = (num21 - rootY) / dt;
		accY = (num25 - prevVy) / dt;
		prevVy = num25;
		rootY = num21;
		rootVy = num25;
		Move("root", 0f, Mathf.Max(0f, num21), 0f);
		Move("body", 0f, Mathf.Min(0f, num21), 0f);
		float num26 = amp * num5;
		float num27 = Mathf.Cos(MathF.PI * 2f * (phase - duty / 2f));
		float num28 = Mathf.Cos(MathF.PI * 2f * (phase - 0.03f));
		float num29 = D(Mathf.Lerp(6f, 5.5f, run)) * num27;
		float num30 = D(Mathf.Lerp(2.5f, 10f, run));
		float num31 = Mathf.Lerp(D(3f) * num27, D(5f) * num28, run);
		float num32 = Mathf.Lerp(0.008f * (1f - Mathf.Abs(Mathf.Cos(MathF.PI * 2f * phase))), 0f, run);
		Rot("body", AZ, (0f - num29) * num26);
		Rot("body", AX, num30 * num26);
		Rot("body", AY, num31 * num26);
		Move("body", 0f, num32 * num26, 0f);
		Transform transform = bones.Get("body")?.bone;
		if (transform != null)
		{
			float num33 = run * num26 * ((num21 < 0f) ? (Mathf.Clamp01((0f - num21) / 0.009f) * 0.035f) : ((0f - Mathf.Clamp01(num21 / 0.035f)) * 0.02f));
			ScaleBody(transform, new Vector3(1f + num33 * 0.5f, 1f - num33, 1f + num33 * 0.5f));
		}
		for (int j = 0; j < 2; j++)
		{
			LegSide legSide = (LegSide)j;
			float num34 = ((j == 1) ? num28 : (0f - num28));
			float lower = wr(Tune.Lower) - wr(Tune.LowerVar) * num34 - ((legSide == LegSide.L) ? (D(6f) * Smooth(0.6f, -0.4f, num34)) : 0f);
			Arm(legSide, num34, wr(Tune.Swing), wr(Tune.Bias), lower, wr(Tune.Bend) + D(16f) * run * num34, num26);
			Fist(legSide, num26 * Mathf.Lerp(0.75f, 1f, run));
		}
		legJ[0] = new LegParams();
		legJ[1] = new LegParams();
		if (jump != null && transform != null)
		{
			JumpPose(jump, transform);
		}
		for (int k = 0; k < 2; k++)
		{
			LegParams legParams = legG[k];
			LegParams legParams2 = legJ[k];
			float k2 = legParams2.k;
			Leg((LegSide)k, Mathf.Lerp(legParams.fwd, legParams2.fwd, k2), Mathf.Lerp(legParams.up, legParams2.up, k2), Mathf.Lerp(legParams.pitch, legParams2.pitch, k2), Mathf.Lerp(legParams.k, 1f, k2));
		}
		bool flag4 = wasFlying && num21 <= 0f;
		wasFlying = num21 > 0.002f;
		float num35 = 57.29578f;
		float num36 = (flag3 ? (30f * Mathf.Clamp(impact / 3f, 0f, 1.5f)) : 0f);
		float num37 = ((jump != null && jump.phase == JumpPhase.air) ? (7f * jw) : 0f);
		float value = bagX.Step(Mathf.Clamp((0f - ctx.accF) * 1.2f, -3f, 3f), dt);
		float value2 = bagZ.Step(Mathf.Clamp(ctx.accL * 1.6f, -6f, 10f) + num29 * num26 * num35 * 0.5f + num37, dt, (flag4 ? (40f * run) : 0f) + num36);
		Rot("bag", AX, D(Mathf.Clamp(value, -3f, 3f)));
		Rot("bag", bagAxis, D(Mathf.Clamp(value2, 0f, 10f)));
		float wr(float[] x)
		{
			return D(Mathf.Lerp(x[0], x[1], run));
		}
	}
}
