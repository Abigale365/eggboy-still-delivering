// 2026-11-16 真实接触式拾取（docs/PHYSICAL_PICKUP_IK_BOOK_20261116.md，源自 docs/reference/forward_pickup/CSharp/PickupGrip.cs）。
// 在 EggboyPlayer.LateUpdate（动作片段、步态、姿态、防穿模）之后改骨骼：上身送右肩前倾，右臂两骨骼解析 IK 让掌心贴到物体表面，手指包住。
// 和参考源码的两处不同（工程书第二节）：
//   · Reach 的物体直径参数化（默认 0.14 m，学校红球 C2 的拟合尺寸），接触点 = 球心 + 方向 × 直径 × 0.49
//   · KeepAboveFloor 用角色脚下真实地面高度（参考 Demo 的地面恒为 y = 0，主场景学校草坪在 3 m 高）
using System.Collections.Generic;
using UnityEngine;

public sealed class PickupGrip
{
    public const float DefaultDiameter = 0.14f;

    readonly EggboyBones bones;
    readonly Transform shoulder;
    readonly Transform wrist;
    readonly Transform finger1;
    readonly Transform finger2;
    readonly Quaternion finger1Rest;
    readonly Quaternion finger2Rest;
    readonly Vector3 palmOffset;
    readonly Vector3 palmNormal;
    readonly Vector3 curlAxis;
    readonly List<Vector3> shell = new List<Vector3>();
    public Transform Body { get; }
    public readonly Vector3 CarryCenterLocal = new Vector3(0.359f, 0.192f, 0.287f);
    public Vector3 CarryCenter => Body.TransformPoint(CarryCenterLocal);
    public Vector3 PalmPosition => wrist.TransformPoint(palmOffset);
    public bool Ok { get; }

    public PickupGrip(EggboyBones bones, EggboySkin skin)
    {
        this.bones = bones;
        Body = bones.Bone("body");
        shoulder = bones.Bone("armR");
        wrist = bones.Bone("handR");
        finger1 = bones.Bone("finger1R");
        finger2 = bones.Bone("finger2R");
        Ok = Body != null && shoulder != null && wrist != null && finger1 != null && finger2 != null && skin != null && skin.mesh != null;
        if (!Ok) return;
        finger1Rest = finger1.localRotation;
        finger2Rest = finger2.localRotation;
        palmNormal = wrist.InverseTransformDirection(Vector3.ProjectOnPlane(Vector3.down, wrist.up)).normalized;
        palmOffset = new Vector3(0.008f, 0.083f, 0.011f) + palmNormal * 0.022f;
        curlAxis = Vector3.Cross(Vector3.up, palmNormal).normalized;

        int index = skin.BoneIndex("body");
        if (index < 0) return;
        Matrix4x4 bindpose = skin.mesh.bindposes[index];
        foreach (int vertex in skin.shellVerts)
            if (skin.boneOfVertex[vertex] == index)
                shell.Add(bindpose.MultiplyPoint3x4(skin.verts[vertex]));
    }

    public void LeanForward(float weight, float groundY)
    {
        // 只转上身把右肩送出去，双脚还朝着物体
        bones.RotateRaw("body", Vector3.right, 4f * weight * Mathf.Deg2Rad);
        Body.rotation = Quaternion.AngleAxis(-34f * weight, Vector3.up) * Body.rotation;
        KeepAboveFloor(groundY);
    }

    public void LeanSide(float degrees, float groundY)
    {
        bones.RotateRaw("body", Vector3.forward, -degrees * Mathf.Deg2Rad);
        KeepAboveFloor(groundY);
    }

    // 蛋壳最低点不能低于脚下地面 + 9 mm（前倾 / 侧弯时下沿会往下扫）
    public void KeepAboveFloor(float groundY)
    {
        if (shell.Count == 0) return;
        var matrix = Body.localToWorldMatrix;
        float bottom = float.PositiveInfinity;
        foreach (var vertex in shell) bottom = Mathf.Min(bottom, matrix.MultiplyPoint3x4(vertex).y);
        if (bottom < groundY + 0.009f) Body.position += Vector3.up * (groundY + 0.009f - bottom);
    }

    // 2026-11-16 持物用：蛋壳在 frame 坐标系里、某个高度附近（±halfHeight）、左右 ±halfWidth 以内最靠前的 z
    public float ShellFrontAt(Transform frame, float localY, float halfWidth, float halfHeight)
    {
        if (shell.Count == 0) return 0.30f;
        var m = Body.localToWorldMatrix;
        float best = float.NegativeInfinity;
        foreach (var v in shell)
        {
            var l = frame.InverseTransformPoint(m.MultiplyPoint3x4(v));
            if (Mathf.Abs(l.y - localY) > halfHeight || Mathf.Abs(l.x) > halfWidth) continue;
            if (l.z > best) best = l.z;
        }
        return float.IsNegativeInfinity(best) ? 0.30f : best;
    }

    // 蛋壳在某个世界高度附近（±5 cm）离身体竖轴最远的水平距离（往右外侧挂东西时用）
    public float ShellRadiusAt(float worldY, Vector3 towards)
    {
        if (shell.Count == 0) return 0.35f;
        var m = Body.localToWorldMatrix;
        var c = Body.position; towards.y = 0f; towards.Normalize();
        float best = 0f;
        foreach (var v in shell)
        {
            var w = m.MultiplyPoint3x4(v);
            if (Mathf.Abs(w.y - worldY) > 0.05f) continue;
            var d = w - c; d.y = 0f;
            float along = Vector3.Dot(d, towards);
            if (along > best) best = along;
        }
        return best;
    }

    public float DistanceToShell(Vector3 point)
    {
        if (shell.Count == 0) return float.PositiveInfinity;
        var matrix = Body.localToWorldMatrix;
        float squared = float.PositiveInfinity;
        foreach (var vertex in shell)
            squared = Mathf.Min(squared, (matrix.MultiplyPoint3x4(vertex) - point).sqrMagnitude);
        return Mathf.Sqrt(squared);
    }

    // weight：IK 权重；scoop：0 从侧面碰、1 托在底下；curl：手指弯多少。返回掌心到接触点的距离（米）。
    public float Reach(Vector3 center, float weight, float scoop, float curl, float itemDiameter = DefaultDiameter)
    {
        Vector3 direction = (shoulder.position - center).normalized;
        Vector3 underside = (Vector3.ProjectOnPlane(direction, Body.up).normalized - Body.up * 0.68f).normalized;
        Vector3 radial = Vector3.Slerp(direction, underside, 0.47f * scoop);
        Vector3 contact = center + radial * (itemDiameter * 0.49f);
        if (weight > 0f)
        {
            Quaternion initialShoulder = shoulder.localRotation;
            Quaternion initialWrist = wrist.localRotation;
            AimPalm(contact, -radial);
            shoulder.localRotation = Quaternion.Slerp(initialShoulder, shoulder.localRotation, weight);
            wrist.localRotation = Quaternion.Slerp(initialWrist, wrist.localRotation, weight);
            finger1.localRotation = Quaternion.Slerp(finger1.localRotation,
                finger1Rest * Quaternion.AngleAxis(19f * curl, curlAxis), weight);
            finger2.localRotation = Quaternion.Slerp(finger2.localRotation,
                finger2Rest * Quaternion.AngleAxis(12f * curl, curlAxis), weight);
        }
        return Vector3.Distance(PalmPosition, contact);
    }

    void AimPalm(Vector3 contact, Vector3 surfaceNormal)
    {
        Vector3 pivot = shoulder.position;
        Vector3 upper = wrist.position - pivot;
        float upperLength = upper.magnitude;
        float palmLength = wrist.TransformVector(palmOffset).magnitude;
        Vector3 reach = contact - pivot;
        Vector3 axis = reach.normalized;
        float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(upperLength - palmLength) + 0.0001f,
            upperLength + palmLength - 0.0001f);
        float along = (upperLength * upperLength + distance * distance - palmLength * palmLength) / (2f * distance);
        float offset = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 bend = Vector3.ProjectOnPlane(-Body.up - 0.2f * Body.forward, axis).normalized;
        if (bend.sqrMagnitude < 0.01f) bend = Vector3.ProjectOnPlane(Body.right, axis).normalized;

        Vector3 wristTarget = pivot + axis * along + bend * offset;
        shoulder.rotation = Quaternion.FromToRotation(upper, wristTarget - pivot) * shoulder.rotation;
        Vector3 palmAxis = (pivot + axis * distance - wrist.position).normalized;
        wrist.rotation = Quaternion.FromToRotation(wrist.TransformVector(palmOffset), palmAxis) * wrist.rotation;

        // 绕掌心连线扭一下手腕：接触点不动，掌面朝向物体表面
        Vector3 actual = Vector3.ProjectOnPlane(wrist.TransformDirection(palmNormal), palmAxis);
        Vector3 desired = Vector3.ProjectOnPlane(surfaceNormal, palmAxis);
        if (actual.sqrMagnitude > 0.01f && desired.sqrMagnitude > 0.01f)
            wrist.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(actual, desired, palmAxis), palmAxis) * wrist.rotation;
    }
}
