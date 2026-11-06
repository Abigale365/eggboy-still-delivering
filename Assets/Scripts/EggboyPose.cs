// 程序姿态层：叠在动作片段上面，让 EGGBOY 不同表情有不同的身体状态（照设定稿），
// 平时也会呼吸、换重心、东张西望；天线和邮差包有弹簧惯性。
// 从 game/src/pose.ts 逐行移植；所有参数都用略带回弹的弹簧追目标。
using System;
using System.Collections.Generic;
using UnityEngine;

public enum LegSide { L, R }

// 一个表情的身体状态。角度单位：度
public class PoseDef
{
    public float lean, tilt, twist, drop;
    public float[] armR, armL, handR, handL;
    public float ant;
    public float bob, bobHz, sway, swayHz, shiver;
    public Action<float, Gesture> gesture;

    public PoseDef()
    {
        // 自然站姿：手臂从 A 字站姿放下来贴近身体，左手稍微往前避开邮差包，手腕微微往里收
        lean = 0f; tilt = 0f; twist = 0f; drop = 0f;
        armR = new[] { -30f, 12f };
        armL = new[] { -24f, 18f };
        handR = new[] { 6f, 14f };
        handL = new[] { 6f, 14f };
        ant = 0f;
        bob = 0.004f; bobHz = 0.25f;
        sway = 2.2f; swayHz = 0.12f;
        shiver = 0f;
    }
}

public class Gesture
{
    public float lean, tilt, twist, drop;
    public float[] armR, armL, handR, handL;

    public Gesture(PoseDef b)
    {
        lean = b.lean; tilt = b.tilt; twist = b.twist; drop = b.drop;
        armR = new[] { b.armR[0], b.armR[1] };
        armL = new[] { b.armL[0], b.armL[1] };
        handR = new[] { b.handR[0], b.handR[1] };
        handL = new[] { b.handL[0], b.handL[1] };
    }
}

public struct PoseContext
{
    public float speed;   // 水平速度 m/s
    public float accF;    // 往前的加速度（机器人自己的坐标）
    public float accL;    // 往左的加速度
    public float accY;    // 上下的加速度（小跑弹跳），推着天线点头
    public bool emoting;  // 正在播挥手之类的动作片段
    public float jump;    // 跳跃姿态的权重 0–1（跳起来时手臂、身体让给步态层的跳跃姿态）
}

public class EggboyPose
{
    static readonly Vector3 AX = new Vector3(1f, 0f, 0f);
    static readonly Vector3 AY = new Vector3(0f, 1f, 0f);
    static readonly Vector3 AZ = new Vector3(0f, 0f, 1f);

    static readonly string[] Keys =
    {
        "lean", "tilt", "twist", "drop", "armR0", "armR1", "armL0", "armL1",
        "handR0", "handR1", "handL0", "handL1", "ant", "bob", "sway", "shiver", "armW", "bodyW",
    };

    class Spring
    {
        public float x, v, k, damp;
        public Spring(float k = 70f, float damp = 0.72f) { this.k = k; this.damp = damp; }
        public float Step(float target, float dt)
        {
            float c = 2f * Mathf.Sqrt(k) * damp;
            v += (k * (target - x) - c * v) * dt;
            x += v * dt;
            return x;
        }
    }

    class Fidget
    {
        public float len;
        public Action<float, Gesture> fn;
    }

    static float Wave(float t, float hz) => Mathf.Sin(t * Mathf.PI * 2f * hz);
    static float Pulse(float t, float period, float len)
    {
        float p = (t % period) / len;
        return p < 1f ? Mathf.Sin(p * Mathf.PI) : 0f;
    }
    static float Ease(float p)
    {
        float e = Mathf.Min(1f, Mathf.Min(p * 3f, (1f - p) * 3f));
        return e * e * (3f - 2f * e);
    }

    static readonly Dictionary<ExprId, PoseDef> POSES = BuildPoses();
    static readonly Fidget[] FIDGETS = BuildFidgets();

    static Dictionary<ExprId, PoseDef> BuildPoses()
    {
        var d = new Dictionary<ExprId, PoseDef>();
        d[ExprId.happy] = new PoseDef { armR = new[] { -20f, 14f }, armL = new[] { -16f, 18f }, bob = 0.006f, bobHz = 1.1f, sway = 3f, swayHz = 0.4f };
        d[ExprId.wink] = new PoseDef { armR = new[] { 74f, 46f }, handR = new[] { 20f, 55f }, tilt = 6f, lean = 2f, bob = 0.004f, bobHz = 1.2f };
        d[ExprId.surprised] = new PoseDef { armR = new[] { 88f, 22f }, armL = new[] { 88f, 22f }, handR = new[] { 30f, 20f }, handL = new[] { 30f, 20f }, lean = -8f, ant = -18f, bob = 0f };
        d[ExprId.curious] = new PoseDef { tilt = 10f, lean = 6f, armL = new[] { 62f, 52f }, handL = new[] { 15f, 60f }, ant = 12f, sway = 1.5f };
        d[ExprId.angry] = new PoseDef
        {
            lean = 8f, armR = new[] { -34f, -16f }, armL = new[] { -30f, -12f }, handR = new[] { 25f, 25f }, handL = new[] { 25f, 25f }, shiver = 0.7f, ant = 10f,
            gesture = (t, g) => { float s = Mathf.Max(0f, Wave(t, 2.2f)); g.drop -= 0.012f * s; g.armR[0] += 10f * s; g.armL[0] += 10f * s; },
        };
        d[ExprId.cute] = new PoseDef { armR = new[] { 70f, 46f }, armL = new[] { 70f, 46f }, handR = new[] { 10f, 62f }, handL = new[] { 10f, 62f }, lean = 5f, sway = 6f, swayHz = 0.5f, ant = 6f };
        d[ExprId.smug] = new PoseDef { lean = -6f, tilt = -4f, armR = new[] { -12f, -34f }, armL = new[] { -12f, -30f }, handR = new[] { 10f, 55f }, handL = new[] { 10f, 55f }, ant = -6f, bob = 0.003f, bobHz = 0.5f };
        d[ExprId.sleepy] = new PoseDef
        {
            lean = 12f, drop = -0.02f, armR = new[] { -36f, 8f }, armL = new[] { -30f, 12f }, sway = 4f, swayHz = 0.18f, ant = 32f, bob = 0.008f, bobHz = 0.2f,
            gesture = (t, g) => { float r = Pulse(t, 6.5f, 1.6f); g.armR[0] += 105f * r; g.armR[1] += 38f * r; g.handR[1] += 55f * r; g.lean -= 6f * r; },
        };
        d[ExprId.meh] = new PoseDef
        {
            armR = new[] { 12f, 40f }, armL = new[] { 12f, 40f }, handR = new[] { 40f, -10f }, handL = new[] { 40f, -10f }, lean = 3f,
            gesture = (t, g) => { float s = Pulse(t, 3.2f, 1.2f); g.drop += 0.015f * s - 0.01f * Pulse(t + 1.2f, 3.2f, 1f); g.armR[0] += 12f * s; g.armL[0] += 12f * s; },
        };
        d[ExprId.sad] = new PoseDef
        {
            lean = 14f, drop = -0.015f, armR = new[] { -34f, 24f }, armL = new[] { -30f, 26f }, handR = new[] { 10f, 20f }, handL = new[] { 10f, 20f }, ant = 36f, sway = 2f, swayHz = 0.15f,
            gesture = (t, g) => { float r = Pulse(t, 4f, 1.4f); g.armR[0] += 100f * r; g.armR[1] += 30f * r; g.handR[1] += 50f * r; },
        };
        d[ExprId.nervous] = new PoseDef
        {
            armR = new[] { 6f, 58f }, armL = new[] { 6f, 58f }, handR = new[] { 10f, 60f }, handL = new[] { 10f, 60f }, drop = -0.01f, shiver = 1.2f,
            gesture = (t, g) => { float w = Wave(t, 0.35f); g.twist += 12f * Mathf.Sign(w) * Mathf.Min(1f, Mathf.Abs(w) * 3f); },
        };
        d[ExprId.shy] = new PoseDef { armR = new[] { 76f, 50f }, armL = new[] { 76f, 50f }, handR = new[] { 10f, 70f }, handL = new[] { 10f, 70f }, twist = 15f, tilt = 6f, lean = 8f, sway = 3f, swayHz = 0.4f };
        d[ExprId.thinking] = new PoseDef
        {
            armR = new[] { 64f, 50f }, handR = new[] { 15f, 62f }, armL = new[] { -10f, 40f }, handL = new[] { 0f, 45f }, tilt = 6f, lean = 3f, ant = 8f,
            gesture = (t, g) => { g.handR[0] += 10f * Mathf.Max(0f, Wave(t, 1.5f)) * Pulse(t, 5f, 2.2f); },
        };
        d[ExprId.puzzled] = new PoseDef
        {
            armR = new[] { 100f, 42f }, handR = new[] { 35f, 70f }, tilt = 10f, lean = 2f,
            gesture = (t, g) => { g.handR[0] += 18f * Wave(t, 4f); },
        };
        d[ExprId.excited] = new PoseDef
        {
            armR = new[] { 96f, 18f }, armL = new[] { 96f, 18f }, handR = new[] { 20f, 25f }, handL = new[] { 20f, 25f }, bob = 0.02f, bobHz = 2.4f, ant = -6f,
            gesture = (t, g) => { float s = Wave(t, 2.4f); g.armR[0] += 16f * s; g.armL[0] -= 16f * s; },
        };
        d[ExprId.scared] = new PoseDef { armR = new[] { 36f, 52f }, armL = new[] { 36f, 52f }, handR = new[] { 20f, 55f }, handL = new[] { 20f, 55f }, lean = -8f, drop = -0.025f, shiver = 1.8f, ant = -20f };
        return d;
    }

    static Fidget[] BuildFidgets()
    {
        return new[]
        {
            new Fidget { len = 2.6f, fn = (p, g) => { g.twist += 18f * Ease(p); g.tilt += 3f * Ease(p); } },
            new Fidget { len = 2.6f, fn = (p, g) => { g.twist -= 18f * Ease(p); g.tilt -= 3f * Ease(p); } },
            new Fidget { len = 2.8f, fn = (p, g) => { float e = Ease(p); g.lean += 9f * e; g.twist += 14f * e; g.armL[1] += 28f * e; g.handL[1] += 25f * e; } },
            new Fidget { len = 2.2f, fn = (p, g) => { float e = Ease(p); g.armR[0] += 40f * e; g.armL[0] += 40f * e; g.lean -= 4f * e; g.drop += 0.01f * e; } },
            new Fidget { len = 0.8f, fn = (p, g) => { g.drop += p < 0.35f ? -0.015f * Mathf.Sin((p / 0.35f) * Mathf.PI) : 0.03f * Mathf.Sin(((p - 0.35f) / 0.65f) * Mathf.PI); } },
        };
    }

    public ExprId expr = ExprId.happy;
    readonly EggboyBones bones;
    readonly Dictionary<string, Spring> s = new Dictionary<string, Spring>();
    float t, exprT;
    Fidget fidget;
    float fidgetT;
    float nextFidget = 4f, still;
    readonly Spring antX = new Spring(38f, 0.22f);
    readonly Spring antZ = new Spring(38f, 0.22f);

    public EggboyPose(EggboyBones bones)
    {
        this.bones = bones;
        foreach (var k in Keys) s[k] = new Spring();
        s["armW"].x = 1f;
        s["bodyW"].x = 1f;
    }

    public void SetExpression(ExprId id)
    {
        if (id == expr) return;
        expr = id;
        exprT = 0f;
        fidget = null;
    }

    public void Update(float dt, PoseContext ctx)
    {
        t += dt;
        exprT += dt;
        PoseDef def = POSES[expr];
        var g = new Gesture(def);
        def.gesture?.Invoke(exprT, g);

        // 自然状态站着不动时，隔几秒随机来个小动作
        bool moving = ctx.speed > 0.15f;
        still = moving || ctx.emoting ? 0f : still + dt;
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
        else fidget = null;
        if (fidget != null)
        {
            fidgetT += dt;
            float p = fidgetT / fidget.len;
            if (p >= 1f) fidget = null;
            else fidget.fn(p, g);
        }

        g.armR[0] = Mathf.Clamp(g.armR[0], -45f, 105f);
        g.armR[1] = Mathf.Clamp(g.armR[1], -40f, 75f);
        g.armL[0] = Mathf.Clamp(g.armL[0], -45f, 105f);
        g.armL[1] = Mathf.Clamp(g.armL[1], -40f, 75f);

        // 走起来：手臂交给步态，身体保留一半姿态，再加上加减速的前后倾
        float loco = Mathf.Clamp01(ctx.speed / 0.8f);
        float jumpW = ctx.jump;
        float armW = (ctx.emoting ? 0f : 1f - 0.85f * loco) * (1f - jumpW);
        float bodyW = (ctx.emoting ? 0.2f : 1f - 0.5f * loco) * (1f - 0.7f * jumpW);

        float lean = s["lean"].Step(g.lean + Mathf.Clamp(ctx.accF * 1.6f, -10f, 10f), dt);
        float tilt = s["tilt"].Step(g.tilt - Mathf.Clamp(ctx.accL * 1.4f, -8f, 8f), dt);
        float twist = s["twist"].Step(g.twist, dt);
        float drop = s["drop"].Step(g.drop, dt);
        float armR0 = s["armR0"].Step(g.armR[0], dt), armR1 = s["armR1"].Step(g.armR[1], dt);
        float armL0 = s["armL0"].Step(g.armL[0], dt), armL1 = s["armL1"].Step(g.armL[1], dt);
        float handR0 = s["handR0"].Step(g.handR[0], dt), handR1 = s["handR1"].Step(g.handR[1], dt);
        float handL0 = s["handL0"].Step(g.handL[0], dt), handL1 = s["handL1"].Step(g.handL[1], dt);
        float ant = s["ant"].Step(def.ant, dt);
        float bob = s["bob"].Step(def.bob, dt), sway = s["sway"].Step(def.sway, dt), shiver = s["shiver"].Step(def.shiver, dt);
        float aw = s["armW"].Step(armW, dt), bw = s["bodyW"].Step(bodyW, dt);

        // 身体：姿态 + 呼吸 / 摇摆 / 发抖
        float breath = Wave(t, def.bobHz) * bob;
        float swayNow = Wave(t, def.swayHz) * sway;
        float shake = shiver * (Mathf.Sin(t * 91f) * 0.6f + Mathf.Sin(t * 57f) * 0.4f);
        if (bones.Get("body") != null)
        {
            bones.Move("body", 0f, (drop + breath) * bw, 0f);
            float leanWave = Wave(t, def.bobHz) * 1.2f * (bob > 0.001f ? 1f : 0f);
            bones.Rotate("body", AX, Deg((lean + leanWave) * bw));
            bones.Rotate("body", AZ, Deg((tilt + swayNow + shake) * bw));
            bones.Rotate("body", AY, Deg((twist + shake * 0.6f) * bw));
        }

        // 手臂：先在正面抬起，再绕竖轴往前；右臂在 -X 那边，方向相反
        bones.Rotate("armR", AY, Deg(armR1 * aw));
        bones.Rotate("armR", AZ, Deg(-armR0 * aw));
        bones.Rotate("armL", AY, Deg(-armL1 * aw));
        bones.Rotate("armL", AZ, Deg(armL0 * aw));
        bones.Rotate("handR", AY, Deg(handR1 * aw));
        bones.Rotate("handR", AZ, Deg(-handR0 * aw));
        bones.Rotate("handL", AY, Deg(-handL1 * aw));
        bones.Rotate("handL", AZ, Deg(handL0 * aw));

        // 天线：被加速度、身体晃动和小跑的弹跳推着摆
        float push = ctx.accF * 2.2f, side = ctx.accL * 2.2f;
        float bounce = Mathf.Clamp(ctx.accY * 0.04f, -12f, 12f);
        float ax = antX.Step(-push + lean * 0.25f + bounce, dt);
        float az = antZ.Step(side - (tilt + swayNow) * 0.3f, dt);
        bones.Rotate("antenna1", AX, Deg(ant * 0.5f + ax * 0.5f));
        bones.Rotate("antenna1", AZ, Deg(az * 0.5f));
        bones.Rotate("antenna2", AX, Deg(ant * 0.5f + ax));
        bones.Rotate("antenna2", AZ, Deg(az));
    }

    static float Deg(float degrees) => degrees * Mathf.Deg2Rad;
}
