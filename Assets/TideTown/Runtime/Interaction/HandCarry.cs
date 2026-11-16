// 2026-11-16 手提油壶：壶顶的提手每帧对准右手掌心（PickupGrip.PalmPosition），壶身竖直挂在身体右外侧，
// 跟着手的移动像钟摆一样前后左右晃；离蛋壳太近就整体往外让；太高会擦地就按比例缩小一点。
// pourTarget > 0：往前倾倒（塔顶倒油）。执行顺序在姿态层、拾取 IK 之后。
using UnityEngine;

[DefaultExecutionOrder(150)]
public sealed class HandCarry : MonoBehaviour
{
    public float pourTarget;
    EggboyPlayer player;
    Transform hand;
    PickupGrip grip;
    Vector3 handleLocal, centerLocal;
    float heightLocal, radiusLocal;
    Vector3 baseScale;
    float scale = 1f;
    int frames;
    Vector3 lastPalm;
    Vector2 tilt, tiltVel;
    float pour, outward;

    public float Scale => scale;
    public Vector3 HandlePoint => transform.TransformPoint(handleLocal);
    public float PalmError { get; private set; }
    public float ShellGap { get; private set; } = 1f;
    public float GroundGap { get; private set; } = 1f;

    public void Init(EggboyPlayer p)
    {
        player = p;
        hand = p.Bones != null ? p.Bones.Bone("handR") : null;
        grip = PhysicalPickupController.Instance != null ? PhysicalPickupController.Instance.Grip : null;
        baseScale = transform.localScale;
        transform.rotation = Quaternion.identity;
        // 提手 = 最高 14% 那圈顶点的中心（D1 油壶的提手在壶顶）
        var lb = LocalBounds(gameObject, transform);
        heightLocal = lb.size.y;
        radiusLocal = Mathf.Max(lb.extents.x, lb.extents.z);
        centerLocal = lb.center;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
            foreach (var v in MeshPoints(mf.sharedMesh))
            {
                var l = transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                if (l.y > lb.max.y - heightLocal * 0.14f) { sum += l; n++; }
            }
        handleLocal = n > 0 ? sum / n : new Vector3(lb.center.x, lb.max.y, lb.center.z);
        handleLocal.y = lb.max.y - heightLocal * 0.04f;
        lastPalm = Palm();
    }

    Vector3 Palm() => grip != null ? grip.PalmPosition : hand != null ? hand.position : player.transform.position + Vector3.up * 0.3f;

    void LateUpdate()
    {
        if (player == null) return;
        float dt = Mathf.Max(1e-4f, Mathf.Min(0.05f, Time.deltaTime));
        var palm = Palm();
        float ground = player.transform.position.y;
        // 前几帧手臂姿态才到位：取这几帧里最低的掌心定缩放，保证壶底离地 ≥ 5 cm
        if (frames < 12)
        {
            frames++;
            float room = palm.y - ground - 0.12f;   // 走跑时手会往下沉、壶身倾斜时下角更低，留 12 cm
            float worldH = heightLocal * baseScale.y;
            float s = Mathf.Clamp(room / Mathf.Max(0.01f, worldH * 0.96f), 0.45f, 1f);
            scale = frames == 1 ? s : Mathf.Min(scale, s);
            transform.localScale = baseScale * scale;
        }
        // 钟摆：手往哪边快速移动，壶身就往反方向甩一点，再弹回竖直
        var hv = (palm - lastPalm) / dt; lastPalm = palm;
        var fwd = player.transform.forward; var right = player.transform.right;
        var target = new Vector2(Mathf.Clamp(-Vector3.Dot(hv, fwd) * 9f, -16f, 16f), Mathf.Clamp(Vector3.Dot(hv, right) * 9f, -12f, 12f));
        tiltVel += (target - tilt) * 70f * dt - tiltVel * 9f * dt;
        tilt += tiltVel * dt;
        pour = Mathf.MoveTowards(pour, pourTarget, 170f * dt);

        var rot = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f) * Quaternion.Euler(tilt.x + pour, 0f, tilt.y);
        transform.rotation = rot;
        transform.position = palm - transform.TransformVector(handleLocal);

        // 离蛋壳太近：整体往身体右外侧让（平滑）
        float need = 0f;
        if (grip != null)
        {
            var c = transform.TransformPoint(centerLocal);
            float shellR = grip.ShellRadiusAt(c.y, right);
            float canR = radiusLocal * baseScale.x * scale;
            float lateral = Vector3.Dot(c - grip.Body.position, right);
            need = Mathf.Max(0f, shellR + canR + 0.02f - lateral);
            ShellGap = lateral - canR - shellR;
        }
        // 要往外让时立刻让到位（不穿壳优先），回来时慢慢回
        outward = need > outward ? need : Mathf.MoveTowards(outward, need, 0.25f * dt);
        transform.position += right * outward;
        if (grip != null) ShellGap = Vector3.Dot(transform.TransformPoint(centerLocal) - grip.Body.position, right) - radiusLocal * baseScale.x * scale
            - grip.ShellRadiusAt(transform.TransformPoint(centerLocal).y, right);
        // 壶底不进地：低于地面 2 cm 就整体往上托（提手会离掌心一点，比穿地好）
        float bottom = Lux3DModelFit.WorldBounds(gameObject).min.y;
        if (bottom < ground + 0.02f) transform.position += Vector3.up * (ground + 0.02f - bottom);
        PalmError = Vector3.Distance(HandlePoint, palm);
        GroundGap = Lux3DModelFit.WorldBounds(gameObject).min.y - ground;
    }

    // 打包后网格可能没开读写：读不了顶点就用网格包围盒的 8 个角
    static Vector3[] MeshPoints(Mesh m)
    {
        if (m == null) return new Vector3[0];
        if (m.isReadable) return m.vertices;
        var b = m.bounds; var p = new Vector3[8];
        for (int i = 0; i < 8; i++) p[i] = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        return p;
    }

    // 网格顶点在 frame 坐标系里的包围盒
    public static Bounds LocalBounds(GameObject go, Transform frame)
    {
        bool any = false; var b = new Bounds();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            foreach (var v in MeshPoints(mf.sharedMesh))
            {
                var l = frame.InverseTransformPoint(mf.transform.TransformPoint(v));
                if (!any) { b = new Bounds(l, Vector3.zero); any = true; } else b.Encapsulate(l);
            }
        }
        return b;
    }
}
