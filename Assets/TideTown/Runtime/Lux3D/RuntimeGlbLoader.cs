// 运行时把 Lux3D 返回的 GLB 字节变成场景里的模型（glTFast），并归一化尺寸 / 朝向 / 贴地。
// Lux3D 的返回尺寸不可信、朝向不统一，所以一律按用途给的目标尺寸缩放。
using System;
using System.Collections;
using UnityEngine;

public static class RuntimeGlbLoader
{
    public static IEnumerator Load(byte[] glb, Action<GameObject> done, Action<string> fail)
    {
        if (glb == null || glb.Length < 20) { fail?.Invoke("empty glb"); yield break; }
        var import = new GLTFast.GltfImport();
        var loadTask = import.LoadGltfBinary(glb);
        while (!loadTask.IsCompleted) yield return null;
        if (loadTask.IsFaulted || !loadTask.Result)
        {
            fail?.Invoke("glTFast load failed" + (loadTask.Exception != null ? ": " + loadTask.Exception.GetBaseException().Message : ""));
            import.Dispose();
            yield break;
        }
        var holder = new GameObject("Model");
        var instTask = import.InstantiateMainSceneAsync(holder.transform);
        while (!instTask.IsCompleted) yield return null;
        if (instTask.IsFaulted || !instTask.Result)
        {
            UnityEngine.Object.Destroy(holder);
            fail?.Invoke("glTFast instantiate failed");
            import.Dispose();
            yield break;
        }
        // 模型活着的时候不能 Dispose（网格和贴图归 import 管）；挂一个组件，模型销毁时一起释放
        holder.AddComponent<GltfImportLifetime>().import = import;
        foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            // Lux3D 的三角形朝向不可靠：材质一律双面
            foreach (var m in r.materials)
            {
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                m.doubleSidedGI = true;
            }
        }
        // WebGL 显存小：贴图降到 1024
#if UNITY_WEBGL && !UNITY_EDITOR
        QualitySettings.globalTextureMipmapLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, 0);
#endif
        done?.Invoke(holder);
    }
}

public sealed class GltfImportLifetime : MonoBehaviour
{
    public GLTFast.GltfImport import;
    void OnDestroy() { if (import != null) { import.Dispose(); import = null; } }
}

public static class Lux3DModelFit
{
    public static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // 把 model（根在原点、未挂父节点或父节点为单位变换）调成：指定的轴朝上、最长边 = size、底面贴 y=0、水平居中。
    // 做法：外面再包一层，所以调用后 model 的 localPosition/Rotation/Scale 就是最终值，可以直接 SetParent(root,false)。
    public static void Fit(GameObject model, float size, string up)
    {
        var t = model.transform;
        t.position = Vector3.zero; t.rotation = Quaternion.identity; t.localScale = Vector3.one;
        var b = WorldBounds(model);
        Vector3 s = b.size;
        if (s.x <= 1e-5f && s.y <= 1e-5f && s.z <= 1e-5f) return;

        // 选朝上的轴
        int upAxis = 1;
        if (up == "thin") upAxis = MinAxis(s);
        else if (up == "long") upAxis = MaxAxis(s);
        Quaternion rot = Quaternion.identity;
        if (upAxis == 0) rot = Quaternion.Euler(0f, 0f, 90f);       // X → Y
        else if (upAxis == 2) rot = Quaternion.Euler(-90f, 0f, 0f); // Z → Y

        // 内容整体挪进一个子节点，旋转 / 缩放 / 平移都做在子节点上
        var inner = new GameObject("Fit").transform;
        inner.SetParent(t, false);
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i);
            if (c != inner) c.SetParent(inner, true);
        }
        inner.localRotation = rot;
        b = WorldBounds(model);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float k = maxDim > 1e-5f && size > 1e-5f ? size / maxDim : 1f;
        inner.localScale = Vector3.one * k;
        b = WorldBounds(model);
        inner.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }

    static int MinAxis(Vector3 s) => s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
    static int MaxAxis(Vector3 s) => s.x >= s.y && s.x >= s.z ? 0 : (s.y >= s.z ? 1 : 2);

    // 可踩的东西：碰撞盒挂在功能父节点上，换模型不动它
    public static void AddWalkableBox(GameObject root, GameObject model)
    {
        if (root == null || model == null || root.GetComponent<BoxCollider>() != null) return;
        // 包围盒要在父节点自己的坐标系里量：先把父节点转正，量完再转回去
        var keepRot = root.transform.rotation;
        root.transform.rotation = Quaternion.identity;
        var b = WorldBounds(model);
        int ground = LayerMask.NameToLayer("Ground");
        root.layer = ground >= 0 ? ground : 6;
        var box = root.AddComponent<BoxCollider>();
        Vector3 local = root.transform.InverseTransformPoint(b.center);
        box.center = new Vector3(local.x, b.size.y * 0.5f, local.z);
        box.size = new Vector3(Mathf.Max(0.45f, b.size.x * 0.92f), Mathf.Max(0.12f, b.size.y * 0.92f), Mathf.Max(0.45f, b.size.z * 0.92f));
        root.transform.rotation = keepRot;
        Physics.SyncTransforms();
    }
}

// 浮在水面上的心愿船：跟着水位走，轻轻起伏
public sealed class WishBob : MonoBehaviour
{
    float baseY, phase;
    void Start() { baseY = transform.position.y; phase = UnityEngine.Random.value * 6.28f; }
    void Update()
    {
        var p = transform.position;
        p.y = Mathf.Max(baseY, WaterLevel.StaticY - 0.05f) + Mathf.Sin(Time.time * 1.7f + phase) * 0.025f;
        transform.position = p;
    }
}
