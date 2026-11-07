// 2026-11-08 按模型外形生成碰撞（用户要求“碰撞箱和建模的模型一样”）：
//   1. 取模型所有三角形里落在“离地 0.25–1.3 m”（EGGBOY 身体高度）这一段的部分——台阶、门槛在下面，屋檐在上面，都不挡
//   2. 按 10 cm 网格把这些三角形在地面上的投影涂实
//   3. 从网格外圈往里灌水，灌不到的格子（墙围起来的室内）也算实心
//   4. 实心格子合并成尽量少的长方形，每个长方形一个 BoxCollider + TownObstacle（角色层只认长方形碰撞，不改角色代码）
// 换模型（B8、集市摊位到了）会自动重新算。route_check.py 读 building_footprints.json，同一批长方形。
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    const float ColCell = 0.1f;
    const float ColBandLo = 0.25f;
    const float ColBandHi = 1.3f;

    struct ColRect { public float x0, z0, x1, z1; }

    // 模型在离地 0.25–1.3 m 这一段的占地，合并成长方形（世界坐标，轴对齐）
    static List<ColRect> ModelFootprintRects(GameObject root, float groundY)
    {
        var rects = new List<ColRect>();
        var meshes = new List<(Mesh mesh, Matrix4x4 m)>();
        Bounds wb = default; bool has = false;
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr.enabled) continue;
            string n = mr.gameObject.name;
            if (n.StartsWith("COL", StringComparison.OrdinalIgnoreCase) || n.IndexOf("invisible", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            meshes.Add((mf.sharedMesh, mr.transform.localToWorldMatrix));
            if (has) wb.Encapsulate(mr.bounds); else { wb = mr.bounds; has = true; }
        }
        if (!has) return rects;
        float lo = groundY + ColBandLo, hi = groundY + ColBandHi;
        if (wb.max.y < lo || wb.min.y > hi) return rects;

        float gx0 = wb.min.x - ColCell * 2, gz0 = wb.min.z - ColCell * 2;
        int nx = Mathf.CeilToInt((wb.size.x + ColCell * 4) / ColCell), nz = Mathf.CeilToInt((wb.size.z + ColCell * 4) / ColCell);
        if (nx <= 0 || nz <= 0 || nx * nz > 4_000_000) return rects;
        var solid = new bool[nx * nz];
        void Mark(float x, float z)
        {
            int ix = Mathf.FloorToInt((x - gx0) / ColCell), iz = Mathf.FloorToInt((z - gz0) / ColCell);
            if (ix >= 0 && iz >= 0 && ix < nx && iz < nz) solid[iz * nx + ix] = true;
        }

        // 三角形按 5 cm 间距采样（半个格子），只取高度落在带内的采样点
        foreach (var (mesh, m) in meshes)
        {
            var v = mesh.vertices;
            var w = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) w[i] = m.MultiplyPoint3x4(v[i]);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                var t = mesh.GetTriangles(s);
                for (int k = 0; k < t.Length; k += 3)
                {
                    Vector3 a = w[t[k]], b = w[t[k + 1]], c = w[t[k + 2]];
                    float ymin = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), ymax = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    if (ymax < lo || ymin > hi) continue;
                    float len = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
                    int n = Mathf.Clamp(Mathf.CeilToInt(len / (ColCell * 0.5f)), 1, 200);
                    for (int i = 0; i <= n; i++)
                        for (int j = 0; j <= n - i; j++)
                        {
                            float u = i / (float)n, vv = j / (float)n;
                            var p = a + (b - a) * u + (c - a) * vv;
                            if (p.y < lo || p.y > hi) continue;
                            Mark(p.x, p.z);
                        }
                }
            }
        }

        // 从外圈灌水：灌不到的空格在墙里面，也算实心
        var outside = new bool[nx * nz];
        var q = new Queue<int>();
        for (int ix = 0; ix < nx; ix++) { Seed(ix, 0); Seed(ix, nz - 1); }
        for (int iz = 0; iz < nz; iz++) { Seed(0, iz); Seed(nx - 1, iz); }
        void Seed(int ix, int iz) { int id = iz * nx + ix; if (!solid[id] && !outside[id]) { outside[id] = true; q.Enqueue(id); } }
        while (q.Count > 0)
        {
            int id = q.Dequeue(); int ix = id % nx, iz = id / nx;
            if (ix > 0) Seed(ix - 1, iz); if (ix < nx - 1) Seed(ix + 1, iz);
            if (iz > 0) Seed(ix, iz - 1); if (iz < nz - 1) Seed(ix, iz + 1);
        }
        for (int i = 0; i < solid.Length; i++) if (!outside[i]) solid[i] = true;

        // 贪心合并成长方形：每行找连续段，往下延伸到段不一样为止
        var used = new bool[nx * nz];
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                if (!solid[iz * nx + ix] || used[iz * nx + ix]) continue;
                int x1 = ix;
                while (x1 + 1 < nx && solid[iz * nx + x1 + 1] && !used[iz * nx + x1 + 1]) x1++;
                int z1 = iz;
                while (z1 + 1 < nz)
                {
                    bool full = true;
                    for (int xx = ix; xx <= x1; xx++) if (!solid[(z1 + 1) * nx + xx] || used[(z1 + 1) * nx + xx]) { full = false; break; }
                    if (!full) break;
                    z1++;
                }
                for (int zz = iz; zz <= z1; zz++) for (int xx = ix; xx <= x1; xx++) used[zz * nx + xx] = true;
                rects.Add(new ColRect { x0 = gx0 + ix * ColCell, z0 = gz0 + iz * ColCell, x1 = gx0 + (x1 + 1) * ColCell, z1 = gz0 + (z1 + 1) * ColCell });
            }
        return rects;
    }

    // 给模型挂上按外形算出的碰撞：每个长方形一个子物体（TownObstacle 层，BoxCollider）。返回长方形个数。
    static int AddModelCollision(GameObject model, float groundY, Transform parent, string id, bool recordFootprint)
    {
        var rects = ModelFootprintRects(model, groundY);
        if (rects.Count == 0) return 0;
        var holder = new GameObject(id + "_collision");
        holder.transform.SetParent(parent, true);
        int k = 0;
        foreach (var r in rects)
        {
            var go = new GameObject("col_" + k++);
            go.transform.SetParent(holder.transform, false);
            go.transform.position = new Vector3((r.x0 + r.x1) * 0.5f, groundY + 0.6f, (r.z0 + r.z1) * 0.5f);
            go.layer = 7;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(r.x1 - r.x0, 1.2f, r.z1 - r.z0);
            go.AddComponent<TownObstacle>();
            if (recordFootprint)
                footprints.Add(new Footprint { instance = id, x = (r.x0 + r.x1) * 0.5f, z = (r.z0 + r.z1) * 0.5f, w = r.x1 - r.x0, d = r.z1 - r.z0 });
        }
        Debug.Log($"MODELCOL {id} rects={rects.Count}");
        return rects.Count;
    }
}
