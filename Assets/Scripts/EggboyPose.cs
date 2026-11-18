using System;
using System.Collections.Generic;
using UnityEngine;

public class EggboyPose
{
	private class Spring
	{
		public float x;

		public float v;

		public float k;

		public float damp;

		public Spring(float k = 70f, float damp = 0.72f)
		{
			this.k = k;
			this.damp = damp;
		}

		public float Step(float target, float dt)
		{
			float num = 2f * Mathf.Sqrt(k) * damp;
			v += (k * (target - x) - num * v) * dt;
			x += v * dt;
			return x;
		}
	}

	private class Fidget
	{
		public float len;

		public Action<float, Gesture> fn;
	}

	private static readonly Vector3 AX = new Vector3(1f, 0f, 0f);

	private static readonly Vector3 AY = new Vector3(0f, 1f, 0f);

	private static readonly Vector3 AZ = new Vector3(0f, 0f, 1f);

	private static readonly string[] Keys = new string[18]
	{
		"lean", "tilt", "twist", "drop", "armR0", "armR1", "armL0", "armL1", "handR0", "handR1",
		"handL0", "handL1", "ant", "bob", "sway", "shiver", "armW", "bodyW"
	};

	private static readonly Dictionary<ExprId, PoseDef> POSES = BuildPoses();

	private static readonly Fidget[] FIDGETS = BuildFidgets();

	public ExprId expr;

	private readonly EggboyBones bones;

	private readonly Dictionary<string, Spring> s = new Dictionary<string, Spring>();

	private float t;

	private float exprT;

	private Fidget fidget;

	private float fidgetT;

	private float nextFidget = 4f;

	private float still;

	private readonly Spring antX = new Spring(38f, 0.22f);

	private readonly Spring antZ = new Spring(38f, 0.22f);

	private static float Wave(float t, float hz)
	{
		return Mathf.Sin(t * MathF.PI * 2f * hz);
	}

	private static float Pulse(float t, float period, float len)
	{
		float num = t % period / len;
		if (!(num < 1f))
		{
			return 0f;
		}
		return Mathf.Sin(num * MathF.PI);
	}

	private static float Ease(float p)
	{
		float num = Mathf.Min(1f, Mathf.Min(p * 3f, (1f - p) * 3f));
		return num * num * (3f - 2f * num);
	}

	private static Dictionary<ExprId, PoseDef> BuildPoses()
	{
		Dictionary<ExprId, PoseDef> dictionary = new Dictionary<ExprId, PoseDef>();
		dictionary[ExprId.happy] = new PoseDef
		{
			armR = new float[2] { -20f, 14f },
			armL = new float[2] { -16f, 18f },
			bob = 0.006f,
			bobHz = 1.1f,
			sway = 3f,
			swayHz = 0.4f
		};
		dictionary[ExprId.wink] = new PoseDef
		{
			armR = new float[2] { 74f, 46f },
			handR = new float[2] { 20f, 55f },
			tilt = 6f,
			lean = 2f,
			bob = 0.004f,
			bobHz = 1.2f
		};
		dictionary[ExprId.surprised] = new PoseDef
		{
			armR = new float[2] { 88f, 22f },
			armL = new float[2] { 88f, 22f },
			handR = new float[2] { 30f, 20f },
			handL = new float[2] { 30f, 20f },
			lean = -8f,
			ant = -18f,
			bob = 0f
		};
		dictionary[ExprId.curious] = new PoseDef
		{
			tilt = 10f,
			lean = 6f,
			armL = new float[2] { 62f, 52f },
			handL = new float[2] { 15f, 60f },
			ant = 12f,
			sway = 1.5f
		};
		dictionary[ExprId.angry] = new PoseDef
		{
			lean = 8f,
			armR = new float[2] { -34f, -16f },
			armL = new float[2] { -30f, -12f },
			handR = new float[2] { 25f, 25f },
			handL = new float[2] { 25f, 25f },
			shiver = 0.7f,
			ant = 10f,
			gesture = delegate(float t, Gesture g)
			{
				float num5 = Mathf.Max(0f, Wave(t, 2.2f));
				g.drop -= 0.012f * num5;
				g.armR[0] += 10f * num5;
				g.armL[0] += 10f * num5;
			}
		};
		dictionary[ExprId.cute] = new PoseDef
		{
			armR = new float[2] { 70f, 46f },
			armL = new float[2] { 70f, 46f },
			handR = new float[2] { 10f, 62f },
			handL = new float[2] { 10f, 62f },
			lean = 5f,
			sway = 6f,
			swayHz = 0.5f,
			ant = 6f
		};
		dictionary[ExprId.smug] = new PoseDef
		{
			lean = -6f,
			tilt = -4f,
			armR = new float[2] { -12f, -34f },
			armL = new float[2] { -12f, -30f },
			handR = new float[2] { 10f, 55f },
			handL = new float[2] { 10f, 55f },
			ant = -6f,
			bob = 0.003f,
			bobHz = 0.5f
		};
		dictionary[ExprId.sleepy] = new PoseDef
		{
			lean = 12f,
			drop = -0.02f,
			armR = new float[2] { -36f, 8f },
			armL = new float[2] { -30f, 12f },
			sway = 4f,
			swayHz = 0.18f,
			ant = 32f,
			bob = 0.008f,
			bobHz = 0.2f,
			gesture = delegate(float t, Gesture g)
			{
				float num4 = Pulse(t, 6.5f, 1.6f);
				g.armR[0] += 105f * num4;
				g.armR[1] += 38f * num4;
				g.handR[1] += 55f * num4;
				g.lean -= 6f * num4;
			}
		};
		dictionary[ExprId.meh] = new PoseDef
		{
			armR = new float[2] { 12f, 40f },
			armL = new float[2] { 12f, 40f },
			handR = new float[2] { 40f, -10f },
			handL = new float[2] { 40f, -10f },
			lean = 3f,
			gesture = delegate(float t, Gesture g)
			{
				float num3 = Pulse(t, 3.2f, 1.2f);
				g.drop += 0.015f * num3 - 0.01f * Pulse(t + 1.2f, 3.2f, 1f);
				g.armR[0] += 12f * num3;
				g.armL[0] += 12f * num3;
			}
		};
		dictionary[ExprId.sad] = new PoseDef
		{
			lean = 14f,
			drop = -0.015f,
			armR = new float[2] { -34f, 24f },
			armL = new float[2] { -30f, 26f },
			handR = new float[2] { 10f, 20f },
			handL = new float[2] { 10f, 20f },
			ant = 36f,
			sway = 2f,
			swayHz = 0.15f,
			gesture = delegate(float t, Gesture g)
			{
				float num2 = Pulse(t, 4f, 1.4f);
				g.armR[0] += 100f * num2;
				g.armR[1] += 30f * num2;
				g.handR[1] += 50f * num2;
			}
		};
		dictionary[ExprId.nervous] = new PoseDef
		{
			armR = new float[2] { 6f, 58f },
			armL = new float[2] { 6f, 58f },
			handR = new float[2] { 10f, 60f },
			handL = new float[2] { 10f, 60f },
			drop = -0.01f,
			shiver = 1.2f,
			gesture = delegate(float t, Gesture g)
			{
				float f = Wave(t, 0.35f);
				g.twist += 12f * Mathf.Sign(f) * Mathf.Min(1f, Mathf.Abs(f) * 3f);
			}
		};
		dictionary[ExprId.shy] = new PoseDef
		{
			armR = new float[2] { 76f, 50f },
			armL = new float[2] { 76f, 50f },
			handR = new float[2] { 10f, 70f },
			handL = new float[2] { 10f, 70f },
			twist = 15f,
			tilt = 6f,
			lean = 8f,
			sway = 3f,
			swayHz = 0.4f
		};
		dictionary[ExprId.thinking] = new PoseDef
		{
			armR = new float[2] { 64f, 50f },
			handR = new float[2] { 15f, 62f },
			armL = new float[2] { -10f, 40f },
			handL = new float[2] { 0f, 45f },
			tilt = 6f,
			lean = 3f,
			ant = 8f,
			gesture = delegate(float t, Gesture g)
			{
				g.handR[0] += 10f * Mathf.Max(0f, Wave(t, 1.5f)) * Pulse(t, 5f, 2.2f);
			}
		};
		dictionary[ExprId.puzzled] = new PoseDef
		{
			armR = new float[2] { 100f, 42f },
			handR = new float[2] { 35f, 70f },
			tilt = 10f,
			lean = 2f,
			gesture = delegate(float t, Gesture g)
			{
				g.handR[0] += 18f * Wave(t, 4f);
			}
		};
		dictionary[ExprId.excited] = new PoseDef
		{
			armR = new float[2] { 96f, 18f },
			armL = new float[2] { 96f, 18f },
			handR = new float[2] { 20f, 25f },
			handL = new float[2] { 20f, 25f },
			bob = 0.02f,
			bobHz = 2.4f,
			ant = -6f,
			gesture = delegate(float t, Gesture g)
			{
				float num = Wave(t, 2.4f);
				g.armR[0] += 16f * num;
				g.armL[0] -= 16f * num;
			}
		};
		dictionary[ExprId.scared] = new PoseDef
		{
			armR = new float[2] { 36f, 52f },
			armL = new float[2] { 36f, 52f },
			handR = new float[2] { 20f, 55f },
			handL = new float[2] { 20f, 55f },
			lean = -8f,
			drop = -0.025f,
			shiver = 1.8f,
			ant = -20f
		};
		return dictionary;
	}

	private static Fidget[] BuildFidgets()
	{
		return new Fidget[5]
		{
			new Fidget
			{
				len = 2.6f,
				fn = delegate(float p, Gesture g)
				{
					g.twist += 18f * Ease(p);
					g.tilt += 3f * Ease(p);
				}
			},
			new Fidget
			{
				len = 2.6f,
				fn = delegate(float p, Gesture g)
				{
					g.twist -= 18f * Ease(p);
					g.tilt -= 3f * Ease(p);
				}
			},
			new Fidget
			{
				len = 2.8f,
				fn = delegate(float p, Gesture g)
				{
					float num2 = Ease(p);
					g.lean += 9f * num2;
					g.twist += 14f * num2;
					g.armL[1] += 28f * num2;
					g.handL[1] += 25f * num2;
				}
			},
			new Fidget
			{
				len = 2.2f,
				fn = delegate(float p, Gesture g)
				{
					float num = Ease(p);
					g.armR[0] += 40f * num;
					g.armL[0] += 40f * num;
					g.lean -= 4f * num;
					g.drop += 0.01f * num;
				}
			},
			new Fidget
			{
				len = 0.8f,
				fn = delegate(float p, Gesture g)
				{
					g.drop += ((p < 0.35f) ? (-0.015f * Mathf.Sin(p / 0.35f * MathF.PI)) : (0.03f * Mathf.Sin((p - 0.35f) / 0.65f * MathF.PI)));
				}
			}
		};
	}

	public EggboyPose(EggboyBones bones)
	{
		this.bones = bones;
		string[] keys = Keys;
		foreach (string key in keys)
		{
			s[key] = new Spring();
		}
		s["armW"].x = 1f;
		s["bodyW"].x = 1f;
	}

	public void SetExpression(ExprId id)
	{
		if (id != expr)
		{
			expr = id;
			exprT = 0f;
			fidget = null;
		}
	}

	public void Update(float dt, PoseContext ctx)
	{
		t += dt;
		exprT += dt;
		PoseDef poseDef = POSES[expr];
		Gesture gesture = new Gesture(poseDef);
		poseDef.gesture?.Invoke(exprT, gesture);
		bool flag = ctx.speed > 0.15f;
		still = ((flag || ctx.emoting) ? 0f : (still + dt));
		if (expr == ExprId.happy && still > 2f)
		{
			if (fidget == null)
			{
				nextFidget -= dt;
				if (nextFidget <= 0f)
				{
					fidget = FIDGETS[UnityEngine.Random.Range(0, FIDGETS.Length)];
					fidgetT = 0f;
					nextFidget = 5f + UnityEngine.Random.value * 6f;
				}
			}
		}
		else
		{
			fidget = null;
		}
		if (fidget != null)
		{
			fidgetT += dt;
			float num = fidgetT / fidget.len;
			if (num >= 1f)
			{
				fidget = null;
			}
			else
			{
				fidget.fn(num, gesture);
			}
		}
		gesture.armR[0] = Mathf.Clamp(gesture.armR[0], -45f, 105f);
		gesture.armR[1] = Mathf.Clamp(gesture.armR[1], -40f, 75f);
		gesture.armL[0] = Mathf.Clamp(gesture.armL[0], -45f, 105f);
		gesture.armL[1] = Mathf.Clamp(gesture.armL[1], -40f, 75f);
		float num2 = Mathf.Clamp01(ctx.speed / 0.8f);
		float jump = ctx.jump;
		float target = (ctx.emoting ? 0f : (1f - 0.85f * num2)) * (1f - jump);
		float target2 = (ctx.emoting ? 0.2f : (1f - 0.5f * num2)) * (1f - 0.7f * jump);
		float num3 = s["lean"].Step(gesture.lean + Mathf.Clamp(ctx.accF * 1.6f, -10f, 10f), dt);
		float num4 = s["tilt"].Step(gesture.tilt - Mathf.Clamp(ctx.accL * 1.4f, -8f, 8f), dt);
		float num5 = s["twist"].Step(gesture.twist, dt);
		float num6 = s["drop"].Step(gesture.drop, dt);
		float num7 = s["armR0"].Step(gesture.armR[0], dt);
		float num8 = s["armR1"].Step(gesture.armR[1], dt);
		float num9 = s["armL0"].Step(gesture.armL[0], dt);
		float num10 = s["armL1"].Step(gesture.armL[1], dt);
		float num11 = s["handR0"].Step(gesture.handR[0], dt);
		float num12 = s["handR1"].Step(gesture.handR[1], dt);
		float num13 = s["handL0"].Step(gesture.handL[0], dt);
		float num14 = s["handL1"].Step(gesture.handL[1], dt);
		float num15 = s["ant"].Step(poseDef.ant, dt);
		float num16 = s["bob"].Step(poseDef.bob, dt);
		float num17 = s["sway"].Step(poseDef.sway, dt);
		float num18 = s["shiver"].Step(poseDef.shiver, dt);
		float num19 = s["armW"].Step(target, dt);
		float num20 = s["bodyW"].Step(target2, dt);
		float num21 = Wave(t, poseDef.bobHz) * num16;
		float num22 = Wave(t, poseDef.swayHz) * num17;
		float num23 = num18 * (Mathf.Sin(t * 91f) * 0.6f + Mathf.Sin(t * 57f) * 0.4f);
		if (bones.Get("body") != null)
		{
			bones.Move("body", 0f, (num6 + num21) * num20, 0f);
			float num24 = Wave(t, poseDef.bobHz) * 1.2f * ((num16 > 0.001f) ? 1f : 0f);
			bones.Rotate("body", AX, Deg((num3 + num24) * num20));
			bones.Rotate("body", AZ, Deg((num4 + num22 + num23) * num20));
			bones.Rotate("body", AY, Deg((num5 + num23 * 0.6f) * num20));
		}
		bones.Rotate("armR", AY, Deg(num8 * num19));
		bones.Rotate("armR", AZ, Deg((0f - num7) * num19));
		bones.Rotate("armL", AY, Deg((0f - num10) * num19));
		bones.Rotate("armL", AZ, Deg(num9 * num19));
		bones.Rotate("handR", AY, Deg(num12 * num19));
		bones.Rotate("handR", AZ, Deg((0f - num11) * num19));
		bones.Rotate("handL", AY, Deg((0f - num14) * num19));
		bones.Rotate("handL", AZ, Deg(num13 * num19));
		float num25 = ctx.accF * 2.2f;
		float num26 = ctx.accL * 2.2f;
		float num27 = Mathf.Clamp(ctx.accY * 0.04f, -12f, 12f);
		float num28 = antX.Step(0f - num25 + num3 * 0.25f + num27, dt);
		float num29 = antZ.Step(num26 - (num4 + num22) * 0.3f, dt);
		bones.Rotate("antenna1", AX, Deg(num15 * 0.5f + num28 * 0.5f));
		bones.Rotate("antenna1", AZ, Deg(num29 * 0.5f));
		bones.Rotate("antenna2", AX, Deg(num15 * 0.5f + num28));
		bones.Rotate("antenna2", AZ, Deg(num29));
	}

	private static float Deg(float degrees)
	{
		return degrees * (MathF.PI / 180f);
	}
}
