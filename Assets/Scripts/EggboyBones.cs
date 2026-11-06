// 骨头工具：把网页版里"绕模型坐标轴转 / 沿模型坐标平移"的写法搬过来。
// 网页版用 model 的世界旋转做参照，这里保持同样的换算，只是换成 Unity 的四元数。
using System.Collections.Generic;
using UnityEngine;

public class EggboyBones
{
    public class Entry
    {
        public Transform bone;
        public Quaternion rb;    // 绑定时骨头相对模型根的世界朝向
        public Quaternion rbi;   // rb 的逆
        public Quaternion prbi;  // 父骨头相对模型根的世界朝向的逆
        public Vector3 bindLocalPosition;
        public Quaternion bindLocalRotation;
        public Vector3 bindLocalScale;
        public Vector3 cleanLocalPosition;
        public Quaternion cleanLocalRotation;
        public Vector3 cleanLocalScale;
    }

    public readonly Transform model;
    readonly Dictionary<string, Entry> map = new Dictionary<string, Entry>();

    public EggboyBones(GameObject modelObject, SkinnedMeshRenderer smr)
    {
        model = modelObject.transform;
        Quaternion rootQInv = Quaternion.Inverse(model.rotation);
        if (smr == null) return;
        foreach (var bone in smr.bones)
        {
            if (bone == null) continue;
            Quaternion rb = rootQInv * bone.rotation;
            Quaternion prb = bone.parent != null ? rootQInv * bone.parent.rotation : Quaternion.identity;
            map[Norm(bone.name)] = new Entry
            {
                bone = bone,
                rb = rb,
                rbi = Quaternion.Inverse(rb),
                prbi = Quaternion.Inverse(prb),
                bindLocalPosition = bone.localPosition,
                bindLocalRotation = bone.localRotation,
                bindLocalScale = bone.localScale,
                cleanLocalPosition = bone.localPosition,
                cleanLocalRotation = bone.localRotation,
                cleanLocalScale = bone.localScale,
            };
        }
    }

    public static string Norm(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var sb = new System.Text.StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = char.ToLowerInvariant(name[i]);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
        }
        return sb.ToString();
    }

    public Entry Get(string name) => map.TryGetValue(Norm(name), out var e) ? e : null;

    public Transform Bone(string name) => Get(name)?.bone;

    public bool Has(string name) => map.ContainsKey(Norm(name));

    // 绕模型坐标轴额外转，叠在当前姿态上（对应网页版 rot）
    public void Rotate(string name, Vector3 axis, float radians)
    {
        if (Mathf.Abs(radians) < 1e-7f) return;
        var e = Get(name);
        if (e == null) return;
        // 网页模型与 Unity 关于 X 轴镜像：轴取 (-x, y, z)，角度取反。
        // 纯 Y/Z 轴退化成“轴不变、角度取反”，与原来的手工修正一致。
        Vector3 mirrored = new Vector3(-axis.x, axis.y, axis.z);
        Quaternion q = Quaternion.AngleAxis(-radians * Mathf.Rad2Deg, mirrored);
        Quaternion q2 = e.rbi * q * e.rb;
        e.bone.localRotation = e.bone.localRotation * q2;
    }

    // 轴已经是在 Unity 模型坐标系里量出来的（手指卷曲轴等），不要再做一次 X 镜像
    public void RotateRaw(string name, Vector3 axis, float radians)
    {
        if (Mathf.Abs(radians) < 1e-7f) return;
        var e = Get(name);
        if (e == null) return;
        Quaternion q = Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis);
        Quaternion q2 = e.rbi * q * e.rb;
        e.bone.localRotation = e.bone.localRotation * q2;
    }

    // 沿模型坐标平移，换算到父骨头的局部坐标（对应网页版 move）
    public void Move(string name, float x, float y, float z)
    {
        var e = Get(name);
        if (e == null) return;
        Vector3 v = e.prbi * new Vector3(x, y, z);
        e.bone.localPosition += v;
    }

    // 绑定时骨头在模型坐标里的位置（网页版用 skeleton.boneInverses 反推的等价物）
    public Vector3 BindPosition(string name)
    {
        var e = Get(name);
        if (e == null) return Vector3.zero;
        Vector3 world = e.bone.position;
        return model.InverseTransformPoint(world);
    }

    public void RestoreBindPose()
    {
        foreach (var e in map.Values)
        {
            e.bone.localPosition = e.bindLocalPosition;
            e.bone.localRotation = e.bindLocalRotation;
            e.bone.localScale = e.bindLocalScale;
        }
    }

    // 记录"动作片段算出来的干净值"：在 Animation 更新之后、程序层叠加之前调用
    public void CaptureClean()
    {
        foreach (var e in map.Values)
        {
            e.cleanLocalPosition = e.bone.localPosition;
            e.cleanLocalRotation = e.bone.localRotation;
            e.cleanLocalScale = e.bone.localScale;
        }
    }

    // 还原上一帧的干净值：在 Animation 更新之前调用，防止程序层叠加量跨帧累积
    public void RestoreClean()
    {
        foreach (var e in map.Values)
        {
            e.bone.localPosition = e.cleanLocalPosition;
            e.bone.localRotation = e.cleanLocalRotation;
            e.bone.localScale = e.cleanLocalScale;
        }
    }
}
