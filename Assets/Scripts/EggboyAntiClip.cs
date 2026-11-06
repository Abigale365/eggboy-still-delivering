// 防穿模：每帧在姿态层之后，检查手臂 / 手 / 邮差包有没有戳进身体、耳机、邮差包，
// 戳进去就绕关节往外转一点，直到刚好贴着表面。碰撞形状从模型本身量出来（绑定姿态下）：
// 蛋形身体按高度切片记每层的半宽 / 半厚；两侧耳机当球；邮差包当椭球。
// 从 game/src/anticlip.ts 移植，保留三个关键做法：
// 1) 每帧从上一帧的纠正量接着解；2) 离表面 1.2 cm 内既不推也不松；3) 纠正量限速。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class EggboyAntiClip
{
    const int BANDS = 28;
    const float MARGIN = 0.006f;
    const float EAR_X = 0.31f;
    const float RELAX = 5f;
    const float HOLD = 0.012f;
    const float MAX_RATE = 6f;

    struct Ear { public Vector3 c; public float r; }
    struct BagShape { public Vector3 c; public Vector3 r; }
    struct WorstResult { public float pen; public Vector3 pt; public Vector3 nrm; }
    class Limb { public Transform bone; public int[] idx; }
    class Job { public Transform bone; public int[] idx; public bool skipBag; public float allow; }
    class Corr { public Quaternion c = Quaternion.identity; public Quaternion baseQ = Quaternion.identity; }

    readonly EggboySkin skin;
    readonly EggboyBones bones;
    Transform bodyBone, bagBone;
    int bodyIdx = -1, bagIdx = -1;
    float y0, y1;
    readonly float[] rx = new float[BANDS];
    readonly float[] rz = new float[BANDS];
    readonly List<Ear> ears = new List<Ear>();
    BagShape? bag;
    readonly Limb[] limbs = new Limb[4]; // armR, handR, armL, handL
    int[] bagPts = new int[0];
    float bagBase;
    public readonly bool ok;
    readonly Dictionary<Transform, Corr> corr = new Dictionary<Transform, Corr>();
    Matrix4x4[] skinMats;

    public EggboyAntiClip(EggboySkin skin, EggboyBones bones, Transform model)
    {
        this.skin = skin;
        this.bones = bones;
        if (skin == null || skin.renderer == null || skin.mesh == null) { ok = false; return; }
        bodyIdx = skin.BoneIndex("body");
        bagIdx = skin.BoneIndex("bag");
        bodyBone = bodyIdx >= 0 ? skin.renderer.bones[bodyIdx] : null;
        bagBone = bagIdx >= 0 ? skin.renderer.bones[bagIdx] : null;

        var bodyVerts = new List<int>();
        Matrix4x4 bodyBind = bodyIdx >= 0 ? skin.mesh.bindposes[bodyIdx] : Matrix4x4.identity; // 网格空间 → body 骨骼本地
        foreach (int vi in skin.shellVerts)
        {
            if (bodyIdx < 0 || skin.boneOfVertex[vi] != bodyIdx) continue;
            Vector3 lb = bodyBind.MultiplyPoint3x4(skin.verts[vi]);
            if (Mathf.Abs(lb.x) < EAR_X) bodyVerts.Add(vi);
        }
        if (bodyVerts.Count > 0)
        {
            y0 = float.PositiveInfinity; y1 = float.NegativeInfinity;
            foreach (int vi in bodyVerts)
            {
                Vector3 lb = bodyBind.MultiplyPoint3x4(skin.verts[vi]);
                y0 = Mathf.Min(y0, lb.y);
                y1 = Mathf.Max(y1, lb.y);
            }
            foreach (int vi in bodyVerts)
            {
                Vector3 lb = bodyBind.MultiplyPoint3x4(skin.verts[vi]);
                int b = Band(lb.y);
                rx[b] = Mathf.Max(rx[b], Mathf.Abs(lb.x));
                rz[b] = Mathf.Max(rz[b], Mathf.Abs(lb.z));
            }
        }

        // 耳机：两侧伸出去的部分各当一个球（body 本地）
        foreach (int side in new[] { -1, 1 })
        {
            var ear = new List<int>();
            foreach (int vi in skin.shellVerts)
            {
                if (bodyIdx < 0 || skin.boneOfVertex[vi] != bodyIdx) continue;
                Vector3 lb = bodyBind.MultiplyPoint3x4(skin.verts[vi]);
                if (lb.x * side >= EAR_X) ear.Add(vi);
            }
            if (ear.Count == 0) continue;
            Vector3 c = Vector3.zero;
            foreach (int vi in ear) c += bodyBind.MultiplyPoint3x4(skin.verts[vi]);
            c /= ear.Count;
            float r = 0f;
            foreach (int vi in ear) r = Mathf.Max(r, Vector3.Distance(bodyBind.MultiplyPoint3x4(skin.verts[vi]), c));
            ears.Add(new Ear { c = c, r = r * 0.85f });
        }

        // 邮差包：包身的包围盒当椭球（bag 本地）
        var bagList = new List<int>();
        Matrix4x4 bagBind = bagIdx >= 0 ? skin.mesh.bindposes[bagIdx] : Matrix4x4.identity;
        if (bagIdx >= 0)
            foreach (int vi in skin.leatherVerts) if (skin.boneOfVertex[vi] == bagIdx) bagList.Add(vi);
        if (bagList.Count > 0)
        {
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (int vi in bagList)
            {
                Vector3 v = bagBind.MultiplyPoint3x4(skin.verts[vi]);
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
            bag = new BagShape { c = (min + max) * 0.5f, r = (max - min) * 0.45f };
            var sampled = new List<int>();
            for (int k = 0; k < bagList.Count; k++) if (k % 6 == 0) sampled.Add(bagList[k]);
            bagPts = sampled.ToArray();
        }

        // 手臂 / 手上的采样点（肩膀根部本来就插在身体关节槽里，不算）
        limbs[0] = LimbOf("armR", true, skin);
        limbs[1] = LimbOf("handR", false, skin);
        limbs[2] = LimbOf("armL", true, skin);
        limbs[3] = LimbOf("handL", false, skin);
        ok = bodyIdx >= 0 && bagIdx >= 0 && bodyVerts.Count > 100 && limbs[0] != null && limbs[0].idx.Length > 5;

        // 绑定姿态下包压进身体多少
        if (ok && bag != null)
        {
            Refresh();
            bagBase = Worst(bagPts, true, 0f).pen;
        }
        if (ok) AddShoulderJoints(model);
    }

    Limb LimbOf(string name, bool arm, EggboySkin skin)
    {
        int id = skin.BoneIndex(name);
        if (id < 0) return null;
        var list = new List<int>();
        foreach (int vi in skin.shellVerts)
            if (skin.boneOfVertex[vi] == id) list.Add(vi);
        if (arm)
        {
            // 肩关节在网格空间里的绑定位置
            Vector3 sh = skin.mesh.bindposes[id].inverse.MultiplyPoint3x4(Vector3.zero);
            list.RemoveAll(vi => Vector3.Distance(skin.verts[vi], sh) <= 0.1f);
        }
        int step = Mathf.Max(1, list.Count / 70);
        var sampled = new List<int>();
        for (int k = 0; k < list.Count; k++) if (k % step == 0) sampled.Add(list[k]);
        return new Limb { bone = skin.renderer.bones[id], idx = sampled.ToArray() };
    }

    int Band(float y)
    {
        float t = Mathf.Clamp01((y - y0) / Mathf.Max(1e-6f, y1 - y0));
        return Mathf.Min(BANDS - 1, Mathf.FloorToInt(t * BANDS));
    }

    void Refresh()
    {
        if (skin == null) return;
        skinMats = skin.SkinMatrices();
    }

    // 世界坐标的点戳进碰撞体多深；out 是往外推的世界方向；extra 把碰撞体整体放大
    float Penetrate(Vector3 pw, bool skipBag, out Vector3 outDir, float extra = 0f)
    {
        Vector3 bestDir = Vector3.zero;
        float best = 0f;
        Vector3 p = bodyBone.InverseTransformPoint(pw);
        if (p.y > y0 && p.y < y1)
        {
            int b = Band(p.y);
            float ex = rx[b] + MARGIN + extra, ez = rz[b] + MARGIN + extra;
            float q = (p.x / ex) * (p.x / ex) + (p.z / ez) * (p.z / ez);
            if (q < 1f)
            {
                float d = (1f - Mathf.Sqrt(q)) * Mathf.Min(ex, ez);
                if (d > best)
                {
                    best = d;
                    Vector3 g = new Vector3(p.x / (ex * ex), 0f, p.z / (ez * ez));
                    bestDir = bodyBone.TransformDirection(g).normalized;
                }
            }
        }
        foreach (var e in ears)
        {
            float d = e.r + MARGIN + extra - Vector3.Distance(p, e.c);
            if (d > best)
            {
                best = d;
                bestDir = bodyBone.TransformDirection(p - e.c).normalized;
            }
        }
        if (!skipBag && bag.HasValue && bagBone != null)
        {
            p = bagBone.InverseTransformPoint(pw) - bag.Value.c;
            float ex = bag.Value.r.x + extra, ey = bag.Value.r.y + extra, ez = bag.Value.r.z + extra;
            float q = (p.x / ex) * (p.x / ex) + (p.y / ey) * (p.y / ey) + (p.z / ez) * (p.z / ez);
            if (q < 1f)
            {
                float d = (1f - Mathf.Sqrt(q)) * Mathf.Min(ex, Mathf.Min(ey, ez));
                if (d > best)
                {
                    best = d;
                    Vector3 g = new Vector3(p.x / (ex * ex), p.y / (ey * ey), p.z / (ez * ez));
                    bestDir = bagBone.TransformDirection(g).normalized;
                }
            }
        }
        outDir = bestDir;
        return best;
    }

    WorstResult Worst(int[] idx, bool skipBag, float extra)
    {
        WorstResult w = new WorstResult { pen = 0f, pt = Vector3.zero, nrm = Vector3.zero };
        if (idx == null || idx.Length == 0) return w;
        Refresh();
        foreach (int vi in idx)
        {
            Vector3 tp = skin.VertexWorld(vi, skinMats);
            float d = Penetrate(tp, skipBag, out Vector3 nrm, extra);
            if (d > w.pen) { w.pen = d; w.pt = tp; w.nrm = nrm; }
        }
        return w;
    }

    // 把骨头绕着它的关节、在世界坐标里转一下
    void Turn(Transform bone, Vector3 axisWorld, float angle)
    {
        if (bone.parent == null) return;
        Quaternion pq = bone.parent.rotation;
        Quaternion q = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axisWorld);
        bone.localRotation = (Quaternion.Inverse(pq) * q * pq) * bone.localRotation;
    }

    void Push(Transform bone, int[] idx, bool skipBag, float allow)
    {
        for (int it = 0; it < 8; it++)
        {
            WorstResult w = Worst(idx, skipBag, 0f);
            w.pen -= allow;
            if (w.pen < 0.001f) return;
            Vector3 pivot = bone.position;
            Vector3 d = w.pt - pivot;
            float len = d.magnitude;
            Vector3 axis = Vector3.Cross(d, w.nrm);
            if (len < 1e-4f || axis.sqrMagnitude < 1e-10f) return;
            axis.Normalize();
            Turn(bone, axis, Mathf.Min(0.3f, (w.pen / len) * 1.05f));
        }
    }

    void SetBone(Transform b, Quaternion c, Quaternion baseQ)
    {
        b.localRotation = c * baseQ;
    }

    // 每帧调用（姿态层之后）。boost：纠正量跟得快几倍（跳跃时手臂摆得快）
    public void Solve(float dt, float boost = 1f)
    {
        if (!ok || dt <= 0f) return;
        var jobs = new List<Job>();
        if (bagBone != null && bagPts.Length > 0)
            jobs.Add(new Job { bone = bagBone, idx = bagPts, skipBag = true, allow = bagBase + 0.004f });
        if (limbs[0] != null)
        {
            jobs.Add(new Job { bone = limbs[0].bone, idx = Merge(limbs[0].idx, limbs[1]?.idx), skipBag = false, allow = 0f });
            if (limbs[1] != null) jobs.Add(new Job { bone = limbs[1].bone, idx = limbs[1].idx, skipBag = false, allow = 0f });
            jobs.Add(new Job { bone = limbs[2].bone, idx = Merge(limbs[2].idx, limbs[3]?.idx), skipBag = false, allow = 0f });
            if (limbs[3] != null) jobs.Add(new Job { bone = limbs[3].bone, idx = limbs[3].idx, skipBag = false, allow = 0f });
        }

        // 1. 先套上上一帧的纠正量，从那里接着解
        foreach (var job in jobs)
        {
            if (!corr.TryGetValue(job.bone, out var s))
            {
                s = new Corr();
                corr[job.bone] = s;
            }
            s.baseQ = job.bone.localRotation;
            job.bone.localRotation = s.c * job.bone.localRotation;
        }
        Refresh();

        // 2. 离表面够远就把纠正量松一点，再把还戳进去的推出来
        float relax = 1f - Mathf.Exp(-dt * RELAX * boost);
        Quaternion identity = Quaternion.identity;
        foreach (var job in jobs)
        {
            var s = corr[job.bone];
            if (relax > 0f && Angle(s.c, identity) > 0.006f)
            {
                Quaternion keep = s.c;
                SetBone(job.bone, Quaternion.Slerp(s.c, identity, relax), s.baseQ);
                if (Worst(job.idx, job.skipBag, HOLD).pen > job.allow)
                {
                    s.c = keep;
                    SetBone(job.bone, keep, s.baseQ);
                }
            }
            Push(job.bone, job.idx, job.skipBag, job.allow);
        }

        // 3. 记下新的纠正量，但一帧最多变 MAX_RATE·dt，偶尔解跳了也只会平滑地过去
        float stepDeg = MAX_RATE * boost * dt * Mathf.Rad2Deg;
        foreach (var job in jobs)
        {
            var s = corr[job.bone];
            Quaternion target = job.bone.localRotation * Quaternion.Inverse(s.baseQ);
            s.c = Quaternion.RotateTowards(s.c, target, stepDeg);
            job.bone.localRotation = s.c * s.baseQ;
        }
    }

    static int[] Merge(int[] a, int[] b)
    {
        if (b == null || b.Length == 0) return a;
        var r = new int[a.Length + b.Length];
        System.Array.Copy(a, 0, r, 0, a.Length);
        System.Array.Copy(b, 0, r, a.Length, b.Length);
        return r;
    }

    static float Angle(Quaternion a, Quaternion b) => Quaternion.Angle(a, b);

    // 肩关节球：部件拆分时手臂根部是个没封口的洞，转大角度会露出来
    void AddShoulderJoints(Transform model)
    {
        if (bodyBone == null) return;
        Material mat = JointMaterial();
        foreach (string name in new[] { "armR", "armL" })
        {
            int id = skin.BoneIndex(name);
            if (id < 0) continue;
            Matrix4x4 armBind = skin.mesh.bindposes[id].inverse;
            Vector3 pivot = armBind.MultiplyPoint3x4(Vector3.zero);
            Vector3 dir = armBind.MultiplyVector(Vector3.up).normalized;
            Vector3 centerMesh = pivot + dir * -0.012f;
            Vector3 centerBody = skin.mesh.bindposes[bodyIdx].MultiplyPoint3x4(centerMesh);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "joint-" + name;
            Object.Destroy(ball.GetComponent<Collider>());
            var mr = ball.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            ball.transform.SetParent(bodyBone, false);
            ball.transform.localPosition = centerBody;
            ball.transform.localScale = Vector3.one * 0.096f * 2f;
        }
    }

    static Material JointMaterial()
    {
        Shader sh = GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return null;
        var m = new Material(sh);
        m.name = "joint";
        m.color = new Color32(0x4b, 0x4a, 0x50, 255);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.42f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.42f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.55f);
        return m;
    }

    // 测量用：当前各部位最深戳进去多少（米），不纠正
    public string Measure()
    {
        if (!ok) return "antiClip not ready";
        Refresh();
        return $"armR {Worst(limbs[0].idx, false, 0f).pen:F4} handR {Worst(limbs[1].idx, false, 0f).pen:F4} "
             + $"armL {Worst(limbs[2].idx, false, 0f).pen:F4} handL {Worst(limbs[3].idx, false, 0f).pen:F4}";
    }
}
