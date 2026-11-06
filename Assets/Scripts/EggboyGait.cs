// 走路 / 跑步的步态：按走过的距离驱动，不用烘好的动作片段。
// 从 game/src/gait.ts 逐行移植：着地脚在身体坐标里以行进速度往后挪，在世界里钉住不打滑；
// 跑步前倾、左右摇、蹬地后脚跟往后踢、握拳摆臂。
// 每帧在 Animation 更新之后、姿态层之前调用，叠在待机动作上；速度为 0 时权重为 0。
using System.Collections.Generic;
using UnityEngine;

public enum JumpPhase { none, crouch, air, land }

public class JumpState
{
    public JumpPhase phase = JumpPhase.none;
    public float t, dur;
    public bool moving;
    public float run;
    public float vy, vy0;
    public float impact;
    public LegSide lead = LegSide.L;
}

public struct GaitContext
{
    public float speed;
    public float accF, accL;
    public bool emoting;
    public JumpState jump;
}

public class FootState
{
    public bool stance = true;
    public float fwd, lift;
}

class LegParams
{
    public float fwd, up, pitch, k;
}

public class EggboyGait
{
    static readonly Vector3 AX = new Vector3(1f, 0f, 0f);
    static readonly Vector3 AY = new Vector3(0f, 1f, 0f);
    static readonly Vector3 AZ = new Vector3(0f, 0f, 1f);
    const float G = 9.8f;

    const float RANGE_WALK = 0.12f, RANGE_RUN = 0.14f;
    const float LIFT_WALK = 0.022f, LIFT_RUN = 0.03f;
    const float HOP_MAX = 0.035f;
    const float SINK = 0.009f;
    const float TOE = 0.074f, HEEL = 0.08f, SOLE = 0.085f;
    static readonly float[] FIST = { 65f, 60f };

    // 可调参数（和网页版 TUNE 同名；角度都是度）
    public static class Tune
    {
        public const float Kick = 0.015f, KickPitch = 55f, KickBack = 0.03f, Delay = 0.15f;
        public static readonly float[] Lower = { 24f, 12f }, LowerVar = { 4f, 0f }, Swing = { 24f, 50f }, Bias = { 3f, 8f };
        public static readonly float[] Bend = { 14f, 55f };
        public const float BendFwd = 16f, Inward = 0f, BagClear = 6f;
        public static readonly float[] Cheer = { 170f, 30f, 10f };
    }

    class Spring
    {
        public float x, v, k, damp;
        public Spring(float k, float damp) { this.k = k; this.damp = damp; }
        public float Step(float target, float dt, float impulse = 0f)
        {
            v += impulse;
            float c = 2f * Mathf.Sqrt(k) * damp;
            v += (k * (target - x) - c * v) * dt;
            x += v * dt;
            return x;
        }
    }

    public float w, amp, phase, duty = 1f, rootY, rootVy, accY, jw;
    JumpPhase prevJump = JumpPhase.none;
    readonly LegParams[] legG = { new LegParams(), new LegParams() };
    readonly LegParams[] legJ = { new LegParams(), new LegParams() };
    float sinceLand = 9f, impact;
    public readonly FootState[] feet = { new FootState(), new FootState() };
    readonly EggboyBones bones;
    readonly EggboySkin skin;
    float legLen = 0.155f;
    readonly float scale;
    readonly Spring bagX = new Spring(60f, 0.35f); // 包前后摆
    readonly Spring bagZ = new Spring(60f, 0.35f); // 包往外甩
    float prevVy;
    bool wasFlying;
    readonly Vector3 bagAxis;
    readonly float[] pos = new float[2];
    readonly float[] from = new float[2];
    readonly Vector3[] du = new Vector3[2];
    readonly Vector3[] curl = new Vector3[2];
    Vector3 elbowDir, elbowAxis;

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }
    static float Frac(float x) => x - Mathf.Floor(x);
    static float D(float deg) => deg * Mathf.Deg2Rad;

    public EggboyGait(EggboyBones bones, EggboySkin skin, Transform model)
    {
        this.bones = bones;
        this.skin = skin;
        scale = model.localScale.x > 0f ? model.localScale.x : 1f;
        var foot = bones.Get("footR")?.bone;
        if (foot != null) legLen = foot.localPosition.magnitude;
        bagAxis = FindBagAxis(skin, bones, model) ?? AZ;

        // 手臂方向 du = 肩 → 指节；手指轴让指尖往手心（往下）卷
        for (int i = 0; i < 2; i++)
        {
            LegSide s = (LegSide)i;
            var arm = bones.Get("arm" + s)?.bone;
            var k1 = bones.Get("finger1" + s)?.bone;
            if (arm == null || k1 == null) { du[i] = i == 0 ? AX : -AX; curl[i] = AZ; continue; }
            Vector3 a = model.InverseTransformPoint(arm.position);
            Vector3 k = model.InverseTransformPoint(k1.position);
            Vector3 dir = (k - a).normalized;
            du[i] = dir;
            Vector3 nn = (new Vector3(0f, -1f, 0f) + dir * dir.y).normalized;
            curl[i] = Vector3.Cross(dir, nn).normalized;
        }
    }

    public bool Ok => bones.Has("legL") && bones.Has("legR") && bones.Has("footL") && bones.Has("footR") && bones.Has("body") && bones.Has("root");

    void Rot(string name, Vector3 axis, float rad) => bones.Rotate(name, axis, rad);
    void Move(string name, float x, float y, float z) => bones.Move(name, x, y, z);

    // 肩带前后两个挂点的连线（后 → 前），包绕它往外甩
    static Vector3? FindBagAxis(EggboySkin skin, EggboyBones bones, Transform model)
    {
        if (skin == null || skin.leatherVerts.Length == 0) return null;
        int bi = skin.BoneIndex("bag");
        if (bi < 0) return null;
        var bagBone = skin.renderer.bones[bi];
        Vector3 pivotMesh = skin.mesh.bindposes[bi].inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 pivot = model.InverseTransformPoint(skin.renderer.transform.TransformPoint(pivotMesh));

        Vector3 front = Vector3.zero, back = Vector3.zero;
        int nf = 0, nb = 0;
        foreach (int vi in skin.leatherVerts)
        {
            var bw = skin.weights[vi];
            float wb = 0f;
            if (bw.boneIndex0 == bi) wb += bw.weight0;
            if (bw.boneIndex1 == bi) wb += bw.weight1;
            if (bw.boneIndex2 == bi) wb += bw.weight2;
            if (bw.boneIndex3 == bi) wb += bw.weight3;
            if (wb < 0.2f || wb > 0.95f) continue;
            Vector3 v = model.InverseTransformPoint(skin.renderer.transform.TransformPoint(skin.verts[vi]));
            if (v.z > pivot.z) { front += v; nf++; } else { back += v; nb++; }
        }
        if (nf < 5 || nb < 5) return null;
        return (front / nf - back / nb).normalized;
    }

    // 一条腿：脚踝在身体下面往前 fwd、抬高 up、鞋绕脚踝转 pitch（正 = 鞋尖朝下），权重 k
    void Leg(LegSide s, float fwd, float up, float pitch, float k)
    {
        if (k < 1e-4f) return;
        float a = Mathf.Asin(Mathf.Clamp(fwd / legLen, -0.9f, 0.9f));
        float arc = legLen * (1f - Mathf.Cos(a));
        float tip = Mathf.Max(0f, pitch > 0f
            ? TOE * Mathf.Sin(pitch) - SOLE * (1f - Mathf.Cos(pitch))
            : HEEL * Mathf.Sin(-pitch) - SOLE * (1f - Mathf.Cos(pitch)));
        Rot("leg" + s, AX, -a * k);
        Move("leg" + s, 0f, (up - arc + tip) * k, 0f);
        Rot("foot" + s, AX, (a + pitch) * k);
    }

    // 一只手臂：先从 A 字站姿往下放 lower，再在"手臂和正前方"的平面里摆 swing·fw + bias，手肘弯 bend
    void Arm(LegSide s, float fw, float swing, float bias, float lower, float bend, float k)
    {
        if (k < 1e-4f) return;
        float zr = (s == LegSide.R ? lower : -lower) * k;
        elbowDir = du[(int)s];
        // 手动算方向时要和 Rotate 的镜像保持一致
        elbowDir = Quaternion.AngleAxis(-zr * Mathf.Rad2Deg, AZ) * elbowDir;
        elbowAxis = Vector3.Cross(elbowDir, AZ).normalized;
        // elbowAxis 是由 Unity 骨骼方向叉乘出来的，已经带镜像方向，不能再做一次 X 镜像
        bones.RotateRaw("arm" + s, elbowAxis, (swing * fw + bias) * k);
        Rot("arm" + s, AZ, zr);

        float inward = Tune.Inward;
        elbowDir = new Vector3(s == LegSide.R ? inward : -inward, 0f, 1f).normalized;
        elbowAxis = Vector3.Cross(du[(int)s], elbowDir).normalized;
        bones.RotateRaw("hand" + s, elbowAxis, bend * k);
    }

    void Fist(LegSide s, float k)
    {
        // 手指轴是从 Unity 骨骼位置量出来的，已经带镜像方向，直接转即可
        bones.RotateRaw("finger1" + s, curl[(int)s], D(FIST[0]) * k);
        bones.RotateRaw("finger2" + s, curl[(int)s], D(FIST[1]) * k);
    }

    // 移动中跳完落地：前面那条腿着地，步态从这一步接着走，不停顿
    void Land(LegSide lead, float range)
    {
        LegSide back = lead == LegSide.L ? LegSide.R : LegSide.L;
        phase = lead == LegSide.L ? 0f : 0.5f;
        pos[(int)lead] = range * 0.3f;
        pos[(int)back] = -range * 0.45f;
        feet[(int)lead].stance = true;
        feet[(int)back].stance = true;
    }

    void ScaleBody(Transform body, Vector3 factor)
    {
        body.localScale = new Vector3(
            body.localScale.x * factor.x,
            body.localScale.y * factor.y,
            body.localScale.z * factor.z);
    }

    // —— 跳跃姿态，权重 this.jw ——
    void JumpPose(JumpState J, Transform body)
    {
        float jw = this.jw;
        float hit = Mathf.Clamp01(impact / 4.5f) * Mathf.Exp(-sinceLand * 13f);
        float landSq = 0.2f * hit * Mathf.Cos(sinceLand * 19f);
        if (Mathf.Abs(landSq) > 1e-4f)
        {
            ScaleBody(body, new Vector3(1f + landSq * 0.2f, 1f - landSq, 1f + landSq * 0.35f));
            Move("body", 0f, -0.018f * hit * Mathf.Max(0f, Mathf.Cos(sinceLand * 19f)), 0f);
        }
        if (jw < 0.001f) return;

        float dur = J.dur > 0f ? J.dur : 1f;
        float k = Mathf.Clamp01(J.t / dur);
        float crouch = J.phase == JumpPhase.crouch ? Mathf.Sin((Mathf.PI / 2f) * k) : 0f;
        float air = J.phase == JumpPhase.air ? Smooth(0f, 0.07f, J.t) : J.phase == JumpPhase.land ? 1f : 0f;
        float rise = J.phase == JumpPhase.air && J.vy0 > 0f ? Mathf.Clamp01(J.vy / J.vy0) : 0f;
        float fall = J.phase == JumpPhase.air ? Mathf.Clamp01(-J.vy / Mathf.Max(J.vy0, 2.5f)) : J.phase == JumpPhase.land ? 1f : 0f;
        float apex = air * (1f - rise) * (1f - fall);
        float land = J.phase == JumpPhase.land ? 1f - Smooth(0f, 1f, J.t / dur) : 0f;

        float sq = (J.moving ? 0.08f : 0.16f) * crouch - 0.13f * rise * air - 0.04f * fall * air * (1f - land);
        ScaleBody(body, new Vector3(1f + sq * 0.5f * jw, 1f - sq * jw, 1f + sq * 0.5f * jw));
        Move("body", 0f, -0.012f * crouch * jw, 0f);

        if (!J.moving)
        {
            // 原地跳：双拳举高
            Rot("body", AX, D(10f * crouch - 7f * apex + 4f * fall * air + 8f * land) * jw);
            float tuck = air * (1f - Smooth(0.35f, 0.9f, fall));
            for (int i = 0; i < 2; i++)
                legJ[i] = new LegParams { fwd = 0f, up = 0.03f * tuck, pitch = D(25f) * tuck, k = jw };

            float tAir = J.phase == JumpPhase.air ? J.t : 0f;
            float wind = J.phase == JumpPhase.crouch ? crouch : J.phase == JumpPhase.air ? 1f - Smooth(0f, 0.1f, tAir) : 0f;
            float raise = (J.phase == JumpPhase.air ? Smooth(0.02f, 0.18f, tAir) * (1f - 0.5f * Smooth(0.5f, 1f, fall))
                : J.phase == JumpPhase.land ? 0.5f : 0f) * (1f - 0.5f * land);
            float upX = Tune.Cheer[0], outZ = Tune.Cheer[1], bend = Tune.Cheer[2];
            for (int i = 0; i < 2; i++)
            {
                LegSide s = (LegSide)i;
                float sg = s == LegSide.R ? 1f : -1f;
                Rot("arm" + s, AX, (D(35f) * wind - D(upX) * raise) * jw);
                Rot("arm" + s, AZ, -sg * D(outZ) * raise * jw);
                Arm(s, 0f, 0f, 0f, 0f, D(bend) * raise + D(15f) * wind, jw);
                Fist(s, jw);
            }
            return;
        }

        // 移动中跳：跑步大步定格
        float A = Mathf.Lerp(0.7f, 1f, J.run);
        LegSide leadS = J.lead, backS = leadS == LegSide.L ? LegSide.R : LegSide.L;
        Rot("body", AX, D((6f * crouch + 14f * air * (1f - land) + 8f * land) * A) * jw);
        float fwR = leadS == LegSide.L ? 1f : -1f;
        Rot("body", AY, D(8f) * fwR * A * air * jw);

        float reach = 1f - Smooth(0.4f, 0.95f, fall);
        legJ[(int)leadS] = new LegParams
        {
            fwd = 0.075f * A,
            up = 0.035f * A * reach,
            pitch = -D(12f) * (1f - Smooth(0.6f, 1f, fall)),
            k = jw * air,
        };
        legJ[(int)backS] = new LegParams
        {
            fwd = Mathf.Lerp(-0.085f, -0.05f, Smooth(0.5f, 1f, fall)) * A,
            up = 0.03f * A,
            pitch = D(55f) * A * (1f - 0.5f * Smooth(0.5f, 1f, fall)),
            k = jw * air,
        };
        Arm(LegSide.R, fwR, D(55f) * A, D(8f), D(12f), D(55f) + D(16f) * fwR * A, jw * air);
        Fist(LegSide.R, jw);
        Arm(LegSide.L, -fwR, D(55f) * A, D(8f), D(12f) + (fwR > 0f ? -D(Tune.BagClear) : 0f), D(55f) - D(16f) * fwR * A, jw * air);
        Fist(LegSide.L, jw);
    }

    public void Update(float dt, GaitContext ctx)
    {
        if (!Ok || dt <= 0f) return;
        float v = ctx.speed / scale;
        bool moving = ctx.speed > 0.05f;
        float run = Smooth(0.9f, 1.8f, ctx.speed);

        float ampTarget = ctx.emoting ? 0f : Smooth(0.03f, 0.25f, ctx.speed);
        amp += (ampTarget - amp) * (1f - Mathf.Exp(-dt * 10f));
        bool settled = !moving && feet[0].stance && feet[1].stance && Mathf.Abs(pos[0]) < 0.003f && Mathf.Abs(pos[1]) < 0.003f;
        float wTarget = ctx.emoting ? 0f : (moving || !settled ? 1f : 0f);
        w += (wTarget - w) * (1f - Mathf.Exp(-dt * 12f));

        JumpState J = ctx.jump;
        float jwT = 0f;
        if (J != null && J.phase != JumpPhase.none)
        {
            float k = J.dur > 0f ? Mathf.Clamp01(J.t / J.dur) : 1f;
            jwT = J.phase == JumpPhase.crouch ? Smooth(0f, 1f, k)
                : J.phase == JumpPhase.air ? 1f
                : J.moving ? 1f - Smooth(0f, 1f, k) : 1f - Smooth(0.35f, 1f, k);
        }
        jw += (jwT - jw) * (1f - Mathf.Exp(-dt * 30f));
        float keep = 1f - jw;
        float RANGE = Mathf.Lerp(RANGE_WALK, RANGE_RUN, run);

        bool landNow = J != null && prevJump == JumpPhase.air && J.phase == JumpPhase.land;
        if (landNow)
        {
            sinceLand = 0f;
            impact = J.impact;
            if (J.moving) Land(J.lead, RANGE);
        }
        prevJump = J != null ? J.phase : JumpPhase.none;
        sinceLand += dt;
        float weight = w * keep;

        // 步频随速度：走 ~5.6 步/秒，跑 ~5.2 步/秒
        float steps = Mathf.Lerp(5.6f, 5.2f, Smooth(0.4f, 2.0f, ctx.speed));
        float T = 2f / steps;
        if (weight > 0.001f && !settled) phase = Frac(phase + dt / T);
        float ts = Mathf.Min(0.56f * T, RANGE / Mathf.Max(v, 0.01f));
        duty = ts / T;
        float stride = v * ts;
        float lift = Mathf.Lerp(LIFT_WALK, LIFT_RUN, run);

        // —— 脚：着地的脚按走过的距离往后挪，抬起的脚送到该落的地方 ——
        for (int i = 0; i < 2; i++)
        {
            LegSide s = (LegSide)i;
            float off = i == 0 ? 0f : 0.5f;
            float u = Frac(phase + off);
            float up, pitch;
            FootState f = feet[i];
            if (u < duty)
            {
                float k = u / duty;
                pos[i] = Mathf.Clamp(pos[i] - v * dt, -RANGE * 0.75f, RANGE * 0.75f);
                up = 0f;
                pitch = (D(22f) * run * Smooth(0.65f, 1f, k) + D(8f) * (1f - run) * Smooth(0.75f, 1f, k)) * amp;
                f.stance = true;
            }
            else
            {
                if (f.stance) from[i] = pos[i];
                float k = (u - duty) / (1f - duty);
                float target = stride / 2f;
                float reach = Mathf.Min(1f, Mathf.Abs(target - from[i]) / 0.03f);
                float kick = run * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k / 0.6f)) * reach;
                pos[i] = Mathf.Lerp(from[i], target, Mathf.Lerp(Smooth(0f, 1f, k), Smooth(Tune.Delay, 0.95f, k), run)) - Tune.KickBack * kick;
                up = lift * reach * Mathf.Pow(Mathf.Sin(Mathf.PI * k), 0.85f) + Tune.Kick * kick;
                float pWalk = (D(8f) * (1f - Smooth(0f, 0.35f, k)) - D(10f) * Smooth(0.45f, 0.8f, k) * (1f - Smooth(0.85f, 1f, k))) * reach;
                float pRun = Mathf.Max(D(22f) * (1f - Smooth(0f, 0.3f, k)) * reach, D(Tune.KickPitch) * kick)
                    - D(14f) * Smooth(0.6f, 0.85f, k) * (1f - Smooth(0.9f, 1f, k)) * reach;
                pitch = Mathf.Lerp(pWalk, pRun, run);
                f.stance = false;
            }
            f.fwd = pos[i] * scale;
            f.lift = up * scale;
            legG[i] = new LegParams { fwd = pos[i], up = up, pitch = pitch, k = w };
        }

        // —— 整体弹跳（小跑才有）：两步之间双脚都离地 ——
        float flight = Mathf.Max(0f, 0.5f - duty) * T;
        float y = 0f;
        if (flight > 0.005f)
        {
            float hop = Mathf.Min(HOP_MAX, (G / scale) * flight * flight / 8f);
            float q = Frac(phase * 2f);
            float c = 2f * duty;
            y = q < c
                ? -SINK * Mathf.Sin(Mathf.PI * q / c) * run
                : hop * 4f * ((q - c) / (1f - c)) * (1f - (q - c) / (1f - c));
        }
        y *= weight;
        float vy = (y - rootY) / dt;
        accY = (vy - prevVy) / dt;
        prevVy = vy;
        rootY = y;
        rootVy = vy;
        Move("root", 0f, Mathf.Max(0f, y), 0f);
        Move("body", 0f, Mathf.Min(0f, y), 0f);

        // —— 身体：往着地脚那边歪、前倾；小跑肩膀跟着摆臂扭 ——
        float ampNow = amp * keep;
        float c2 = Mathf.Cos(2f * Mathf.PI * (phase - duty / 2f));
        float cArm = Mathf.Cos(2f * Mathf.PI * (phase - 0.03f));
        float roll = D(Mathf.Lerp(6f, 5.5f, run)) * c2;
        float lean = D(Mathf.Lerp(2.5f, 10f, run));
        float twist = Mathf.Lerp(D(3f) * c2, D(5f) * cArm, run);
        float bodyBob = Mathf.Lerp(0.008f * (1f - Mathf.Abs(Mathf.Cos(2f * Mathf.PI * phase))), 0f, run);
        Rot("body", AZ, -roll * ampNow);
        Rot("body", AX, lean * ampNow);
        Rot("body", AY, twist * ampNow);
        Move("body", 0f, bodyBob * ampNow, 0f);

        Transform bodyT = bones.Get("body")?.bone;
        if (bodyT != null)
        {
            float squash = run * ampNow * (y < 0f
                ? Mathf.Clamp01(-y / SINK) * 0.035f
                : -Mathf.Clamp01(y / HOP_MAX) * 0.02f);
            ScaleBody(bodyT, new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f));
        }

        // —— 手臂：照参考图前后摆，手肘弯着；跑步握拳，走路半握 ——
        float wr(float[] x) => D(Mathf.Lerp(x[0], x[1], run));
        for (int i = 0; i < 2; i++)
        {
            LegSide s = (LegSide)i;
            float fw = i == (int)LegSide.R ? cArm : -cArm;
            float lower = wr(Tune.Lower) - wr(Tune.LowerVar) * fw
                - (s == LegSide.L ? D(Tune.BagClear) * Smooth(0.6f, -0.4f, fw) : 0f);
            Arm(s, fw, wr(Tune.Swing), wr(Tune.Bias), lower, wr(Tune.Bend) + D(Tune.BendFwd) * run * fw, ampNow);
            Fist(s, ampNow * Mathf.Lerp(0.75f, 1f, run));
        }

        legJ[0] = new LegParams();
        legJ[1] = new LegParams();
        if (J != null && bodyT != null) JumpPose(J, bodyT);
        for (int i = 0; i < 2; i++)
        {
            var g = legG[i];
            var j = legJ[i];
            float b = j.k;
            Leg((LegSide)i,
                Mathf.Lerp(g.fwd, j.fwd, b),
                Mathf.Lerp(g.up, j.up, b),
                Mathf.Lerp(g.pitch, j.pitch, b),
                Mathf.Lerp(g.k, 1f, b));
        }

        // —— 邮差包：被加减速、左右晃和每次落地推着；角度有硬上限 ——
        bool landed = wasFlying && y <= 0f;
        wasFlying = y > 0.002f;
        float deg = Mathf.Rad2Deg;
        float jumpLanded = landNow ? 30f * Mathf.Clamp(impact / 3f, 0f, 1.5f) : 0f;
        float floatOut = J != null && J.phase == JumpPhase.air ? 7f * jw : 0f;
        float bx = bagX.Step(Mathf.Clamp(-ctx.accF * 1.2f, -3f, 3f), dt);
        float bz = bagZ.Step(Mathf.Clamp(ctx.accL * 1.6f, -6f, 10f) + roll * ampNow * deg * 0.5f + floatOut,
            dt, (landed ? 40f * run : 0f) + jumpLanded);
        Rot("bag", AX, D(Mathf.Clamp(bx, -3f, 3f)));
        Rot("bag", bagAxis, D(Mathf.Clamp(bz, 0f, 10f)));
    }
}
