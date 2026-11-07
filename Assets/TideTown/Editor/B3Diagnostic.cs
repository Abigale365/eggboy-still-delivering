// B3 法线诊断：只读，不改资产。检查合并后每个网格的"存储法线 vs 三角面朝向"和"面朝里/朝外"。
using System.Text;
using UnityEditor;
using UnityEngine;

public static class B3Diagnostic
{
    const string PrefabPath = "Assets/TideTown/Art/Buildings/B3_小学校舍_精修版.prefab";

    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("B3DIAG prefab missing: " + PrefabPath); return; }
        var inst = Object.Instantiate(prefab);
        var sb = new StringBuilder();
        int meshCount = 0;
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            meshCount++;
            var v = mesh.vertices;
            var n = mesh.normals;
            var tris = mesh.triangles;
            var mr = mf.GetComponent<MeshRenderer>();
            var bounds = mr != null ? mr.bounds : new Bounds(mf.transform.position, Vector3.one);
            var center = bounds.center;
            int triCount = tris.Length / 3, mismatch = 0, inwardWinding = 0, degenerate = 0, noNormal = 0;
            var badMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var badMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var subBad = new int[mesh.subMeshCount];
            for (int t = 0; t < triCount; t++)
            {
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
                var a = mf.transform.TransformPoint(v[i0]);
                var b = mf.transform.TransformPoint(v[i1]);
                var c = mf.transform.TransformPoint(v[i2]);
                var fn = Vector3.Cross(b - a, c - a);
                if (fn.sqrMagnitude < 1e-10f) { degenerate++; continue; }
                fn.Normalize();
                var sn = n[i0] + n[i1] + n[i2];
                if (sn.sqrMagnitude > 1e-10f)
                {
                    sn.Normalize();
                    if (Vector3.Dot(sn, fn) < 0f)
                    {
                        mismatch++;
                        for (int s = 0; s < mesh.subMeshCount; s++)
                        {
                            int start = (int)mesh.GetIndexStart(s), count = (int)mesh.GetIndexCount(s);
                            if (t * 3 >= start && t * 3 < start + count) { subBad[s]++; break; }
                        }
                        badMin = Vector3.Min(badMin, Vector3.Min(a, Vector3.Min(b, c)));
                        badMax = Vector3.Max(badMax, Vector3.Max(a, Vector3.Max(b, c)));
                    }
                }
                else noNormal++;
                var tc = (a + b + c) / 3f;
                if (Vector3.Dot(fn, tc - center) < 0f) inwardWinding++;
            }
            var mat = mr != null ? mr.sharedMaterial : null;
            string cull = mat != null && mat.HasProperty("_Cull") ? mat.GetFloat("_Cull").ToString("F0") : "-";
            string subText = "";
            for (int s = 0; s < subBad.Length; s++) if (subBad[s] > 0) subText += $" sub{s}={subBad[s]}";
            string badBox = mismatch > 0 ? $" badBox={badMin.ToString("F2")}..{badMax.ToString("F2")}" : "";
            sb.AppendLine($"B3MESH {mf.name} tris={triCount} normalsOppositeWinding={mismatch}{badBox}{subText} faceInward={inwardWinding} degenerate={degenerate} noNormal={noNormal} mat={(mat != null ? mat.name : "-")} shader={(mat != null ? mat.shader.name : "-")} cull={cull}");
        }
        Object.DestroyImmediate(inst);
        Debug.Log("B3DIAG meshes=" + meshCount + "\n" + sb);
    }
}
