// 蒙皮网格数据：FBX 里外壳 / 皮革 / 面屏是同一个网格的三个子材质，网页版里是三个 SkinnedMesh。
// 这里按子材质把顶点分开，供表情、防穿模和邮差包轴使用。
using System.Collections.Generic;
using UnityEngine;

public class EggboySkin
{
    public readonly SkinnedMeshRenderer renderer;
    public readonly Mesh mesh;
    public readonly Transform model;
    public readonly Material shellMat, leatherMat, visorMat;
    public readonly int shellSubmesh = -1, leatherSubmesh = -1, visorSubmesh = -1;
    public readonly int[] shellVerts, leatherVerts, visorVerts;
    public readonly Vector3[] verts;
    public readonly BoneWeight[] weights;
    public readonly int[] boneOfVertex; // -1 = 权重不集中在一根骨头

    public EggboySkin(GameObject modelObject, SkinnedMeshRenderer smr)
    {
        model = modelObject.transform;
        renderer = smr;
        mesh = smr != null ? smr.sharedMesh : null;
        if (mesh == null)
        {
            shellVerts = leatherVerts = visorVerts = new int[0];
            verts = new Vector3[0];
            weights = new BoneWeight[0];
            boneOfVertex = new int[0];
            return;
        }
        if (!mesh.isReadable)
            Debug.LogError("模型网格不可读，无法做表情/防穿模；请在导入设置里打开 Read/Write。");

        verts = mesh.vertices;
        weights = mesh.boneWeights;
        boneOfVertex = new int[verts.Length];
        for (int i = 0; i < boneOfVertex.Length; i++) boneOfVertex[i] = Dominant(i, 0.9f);

        var mats = smr.sharedMaterials;
        var sets = new Dictionary<string, HashSet<int>>();
        var kinds = new string[mesh.subMeshCount];
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            Material m = s < mats.Length ? mats[s] : null;
            string kind = MatKind(m != null ? m.name : null);
            if (kind.Length == 0 && mesh.subMeshCount == 3)
                kind = s == 0 ? "shell" : s == 1 ? "leather" : "visor";
            kinds[s] = kind;
            if (kind.Length == 0 || sets.ContainsKey(kind)) continue;
            var set = new HashSet<int>();
            foreach (int vi in mesh.GetTriangles(s)) set.Add(vi);
            sets[kind] = set;
        }
        shellVerts = ToArray(sets, "shell");
        leatherVerts = ToArray(sets, "leather");
        visorVerts = ToArray(sets, "visor");
        shellSubmesh = Find(kinds, "shell");
        leatherSubmesh = Find(kinds, "leather");
        visorSubmesh = Find(kinds, "visor");
        shellMat = MaterialOf(mats, shellSubmesh);
        leatherMat = MaterialOf(mats, leatherSubmesh);
        visorMat = MaterialOf(mats, visorSubmesh);

        ApplyAtlas();
    }

    // GLB 里 shell/leather/visor 共用同一张 baseColor 图集；FBX 导入后材质没接上
    void ApplyAtlas()
    {
        var tex = Resources.Load<Texture2D>("Textures/img0");
        if (tex == null)
        {
            Debug.LogError("找不到 Resources/Textures/img0，角色会保持白模");
            return;
        }
        var mats = renderer.materials;
        foreach (var m in mats)
        {
            if (m == null) continue;
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.mainTexture = tex;
        }

        // img1 是 glTF 的 metallicRoughness（G=粗糙度，B=金属度）。
        // URP Lit 的金属贴图同样是 R=金属度、A=平滑度，所以用离线转换好的 img1_mg。
        // 变体由 Resources 里的 URP 模板材质保证，避免打包时 _METALLICGLOSSMAP 被裁掉。
        var mg = Resources.Load<Texture2D>("Textures/img1_mg");
        var tpl = Resources.Load<Material>("EggboyURPLitMetallic");
        if (mg != null && tpl != null)
        {
            foreach (var m in mats)
            {
                if (m == null) continue;
                m.shader = tpl.shader;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                if (m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", mg);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 1f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 1f);
            }
            Debug.Log("URP 金属度/粗糙度贴图已接入：" + mg.width + "x" + mg.height);
        }
        else Debug.LogError("找不到 img1_mg 或 EggboyURPLitMetallic 模板，角色材质会不完整");

        Debug.Log("角色图集已接入：" + tex.width + "x" + tex.height);
    }

    static string MatKind(string name)
    {
        string n = EggboyBones.Norm(name);
        if (n.Contains("visor")) return "visor";
        if (n.Contains("leather")) return "leather";
        if (n.Contains("shell")) return "shell";
        return "";
    }

    static int Find(string[] kinds, string kind)
    {
        for (int i = 0; i < kinds.Length; i++) if (kinds[i] == kind) return i;
        return -1;
    }

    static Material MaterialOf(Material[] mats, int submesh)
    {
        return submesh >= 0 && submesh < mats.Length ? mats[submesh] : null;
    }

    static int[] ToArray(Dictionary<string, HashSet<int>> sets, string kind)
    {
        if (!sets.TryGetValue(kind, out var set)) return new int[0];
        var arr = new int[set.Count];
        set.CopyTo(arr);
        System.Array.Sort(arr);
        return arr;
    }

    public int BoneIndex(string name)
    {
        if (renderer == null) return -1;
        string want = EggboyBones.Norm(name);
        for (int i = 0; i < renderer.bones.Length; i++)
            if (renderer.bones[i] != null && EggboyBones.Norm(renderer.bones[i].name) == want) return i;
        return -1;
    }

    public Transform Bone(string name)
    {
        int i = BoneIndex(name);
        return i >= 0 ? renderer.bones[i] : null;
    }

    public int Dominant(int vertex, float threshold)
    {
        var w = weights[vertex];
        int best = -1;
        float bw = threshold;
        if (w.weight0 > bw) { bw = w.weight0; best = w.boneIndex0; }
        if (w.weight1 > bw) { bw = w.weight1; best = w.boneIndex1; }
        if (w.weight2 > bw) { bw = w.weight2; best = w.boneIndex2; }
        if (w.weight3 > bw) { bw = w.weight3; best = w.boneIndex3; }
        return best;
    }

    public Matrix4x4 SkinMatrix(int boneIndex)
    {
        return renderer.bones[boneIndex].localToWorldMatrix * mesh.bindposes[boneIndex];
    }

    public Matrix4x4[] SkinMatrices()
    {
        var mats = new Matrix4x4[renderer.bones.Length];
        for (int i = 0; i < mats.Length; i++) mats[i] = SkinMatrix(i);
        return mats;
    }

    // 蒙皮后的世界坐标；四个权重都走 Unity 的 BoneWeight
    public Vector3 VertexWorld(int vertex, Matrix4x4[] skin)
    {
        var w = weights[vertex];
        Vector3 v = verts[vertex];
        float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
        if (sum < 1e-4f) return renderer.transform.TransformPoint(v);
        Vector3 p = Vector3.zero;
        p += skin[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
        p += skin[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
        p += skin[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
        p += skin[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
        return p / sum;
    }

    // 绑定姿态下某个顶点在模型本地坐标里的位置
    public Vector3 BindWorld(int vertex)
    {
        return renderer.transform.TransformPoint(verts[vertex]);
    }
}
