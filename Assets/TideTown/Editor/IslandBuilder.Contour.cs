// 2026-11-08 第四轮：碰撞贴着模型外轮廓（用户："碰撞箱的体积和判定要和建模的外体轮廓贴合"）
//
// 原来：房子、几件街具按模型算 10 cm 格长方形；栅栏、墙靠可走区边线挡（身体能半个戳进去）；
//       喷泉、月亮雕像、集市摊是粗方块（四角空气墙）；狗窝、课桌、秋千、长椅不挡（能穿过去）。
// 现在：整个场景建完后统一扫一遍——
//   1. 可走区附近铺 5 cm 网格，每格往下打 Ground 层拿地面高度
//   2. 所有可见模型（房子、街具、剧情道具、栅栏、崖壁、礁石……；不含树叶花丛、水、特效、捡得起来的小件）
//      的三角形采样，落在"这一格地面以上 0.25–1.3 m"（EGGBOY 身体那一段）的格子算实心，同时记下这格模型最高点
//   3. 实心区挨着空地的那圈外沿，每 10 cm 放一个小圆（TownObstacleSet），圆心就在模型外表面上
//   4. 圆半径 = 身体半径 0.427 − 撞台子半径 PlatR 0.3 + 半格 ≈ 0.15：每个圆都带顶面 top（模型最高点），
//      EggboyPlayer 遇到带顶面的碰撞用 PlatR 推人，所以身体刚好贴到模型表面停住；
//      站得比 top 高的人不挡——高台上的人不会被下面那层的崖壁、栏杆挡住
//   原来那些长方形碰撞（灯塔栏杆、浮箱、水坑除外）全部删掉，换成这一套。
//   tools/preview/route_check.py 读 building_footprints.json 里的 circles，和场景同一份数字。
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class ContourGrid
{
    public const float BandLo = 0.25f, BandHi = 1.3f;
    public float x0, z0, cell; public int nx, nz;
    public bool[] walk, relevant;
    public float[] ground, maxTop;
    public int[] solidOwner;
    public List<string> owners = new List<string>();

    // 拿不走、会挡人的模型才算。excludeFoliage：树叶花丛（软的，蹭过去没关系）
    public static bool Skip(Renderer r, HashSet<Renderer> lodHidden, bool excludeFoliage)
    {
        if (!r.enabled || !r.gameObject.activeInHierarchy || r.gameObject.layer == 6) return true;   // Ground：台地、路面
        if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r is SkinnedMeshRenderer) return true;
        if (lodHidden.Contains(r)) return true;                                                          // 远景灰盒
        for (var t = r.transform; t != null; t = t.parent)
        {
            string n = t.name;
            if (n == "Sea" || n == "WaterSystem" || n == "XIAOHEI" || n == "Crates" || n == "PreviewCam" || n == "SafePoints"
                || n == "MailboxTrail" || n == "FlashlightReply" || n == "CityGlow" || n == "EndingDesk"          // 结局才出现
                || n == "A2_GLB" || n == "D1_GLB" || n == "C1_GLB" || n == "C2_GLB" || n == "C3_GLB" || n == "A3_GLB"   // 捡得起来的小件
                || n.Contains("Beam") || n.Contains("Glow") || n.Contains("Foam") || n.StartsWith("COL", StringComparison.OrdinalIgnoreCase)
                || n.IndexOf("invisible", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (excludeFoliage && (n == "Trees" || n == "Bushes")) return true;
        }
        return false;
    }

    public static HashSet<Renderer> LodHidden()
    {
        var set = new HashSet<Renderer>();
        foreach (var g in UnityEngine.Object.FindObjectsOfType<LODGroup>())
        {
            var lods = g.GetLODs();
            for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r != null) set.Add(r);
        }
        return set;
    }

    public static string OwnerName(Transform t)
    {
        var chain = new List<string>();
        for (var x = t; x != null; x = x.parent) chain.Add(x.name);
        chain.Reverse();
        string a = chain.Count > 1 ? chain[1] : chain[0];
        if ((a == "Streetscape" || a == "Nature") && chain.Count > 2)
        {
            string b = chain[2];
            if ((b == "Fences" || b == "Boulders" || b == "Trees" || b == "Bushes") && chain.Count > 3) return b + "/" + chain[3];
            return a + "/" + b;
        }
        return a.Replace("_visual", "").Replace("_collision", "");
    }

    static bool Contains(Vector2[] poly, float x, float z)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    public static ContourGrid Build(List<(Vector2[] pts, float y)> polys, float cell, float margin, bool excludeFoliage)
    {
        var g = new ContourGrid { cell = cell };
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        foreach (var p in polys) foreach (var q in p.pts) { x0 = Mathf.Min(x0, q.x); x1 = Mathf.Max(x1, q.x); z0 = Mathf.Min(z0, q.y); z1 = Mathf.Max(z1, q.y); }
        x0 -= margin + 0.5f; z0 -= margin + 0.5f; x1 += margin + 0.5f; z1 += margin + 0.5f;
        g.x0 = x0; g.z0 = z0;
        int nx = g.nx = Mathf.CeilToInt((x1 - x0) / cell), nz = g.nz = Mathf.CeilToInt((z1 - z0) / cell);
        int N = nx * nz;
        g.walk = new bool[N]; g.relevant = new bool[N]; g.ground = new float[N]; g.maxTop = new float[N]; g.solidOwner = new int[N];
        var walkY = new float[N];
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = x0 + (ix + 0.5f) * cell, z = z0 + (iz + 0.5f) * cell;
                float py = float.NaN;
                foreach (var p in polys) if (Contains(p.pts, x, z)) py = float.IsNaN(py) ? p.y : Mathf.Max(py, p.y);
                int id = iz * nx + ix;
                g.walk[id] = !float.IsNaN(py); walkY[id] = py;
                g.solidOwner[id] = -1; g.maxTop[id] = float.NegativeInfinity;
            }
        // 可走区往外 margin 以内才管
        int m = Mathf.CeilToInt(margin / cell);
        var rowNear = new bool[N];
        for (int iz = 0; iz < nz; iz++)
        {
            int last = -100000;
            for (int ix = 0; ix < nx; ix++) { if (g.walk[iz * nx + ix]) last = ix; if (ix - last <= m) rowNear[iz * nx + ix] = true; }
            last = 100000;
            for (int ix = nx - 1; ix >= 0; ix--) { if (g.walk[iz * nx + ix]) last = ix; if (last - ix <= m) rowNear[iz * nx + ix] = true; }
        }
        for (int ix = 0; ix < nx; ix++)
        {
            int last = -100000;
            for (int iz = 0; iz < nz; iz++) { if (rowNear[iz * nx + ix]) last = iz; if (iz - last <= m) g.relevant[iz * nx + ix] = true; }
            last = 100000;
            for (int iz = nz - 1; iz >= 0; iz--) { if (rowNear[iz * nx + ix]) last = iz; if (last - iz <= m) g.relevant[iz * nx + ix] = true; }
        }
        int groundMask = LayerMask.GetMask("Ground");
        for (int id = 0; id < N; id++)
        {
            if (!g.relevant[id]) continue;
            float x = x0 + (id % nx + 0.5f) * cell, z = z0 + (id / nx + 0.5f) * cell;
            float top = float.IsNaN(walkY[id]) ? 40f : walkY[id] + 1.5f;
            g.ground[id] = Physics.Raycast(new Vector3(x, top, z), Vector3.down, out var hit, 80f, groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y : (float.IsNaN(walkY[id]) ? -1f : walkY[id]);
        }

        var lodHidden = LodHidden();
        var ownerIdx = new Dictionary<string, int>();
        foreach (var mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
        {
            if (Skip(mr, lodHidden, excludeFoliage)) continue;
            var b = mr.bounds;
            if (b.max.x < x0 || b.min.x > x1 || b.max.z < z0 || b.min.z > z1) continue;
            var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            string on = OwnerName(mr.transform);
            if (!ownerIdx.TryGetValue(on, out int o)) { o = g.owners.Count; g.owners.Add(on); ownerIdx[on] = o; }
            var mtx = mr.transform.localToWorldMatrix;
            var mesh = mf.sharedMesh;
            var v = mesh.vertices; var w = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) w[i] = mtx.MultiplyPoint3x4(v[i]);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                var t = mesh.GetTriangles(s);
                for (int k = 0; k < t.Length; k += 3)
                {
                    Vector3 a = w[t[k]], bb = w[t[k + 1]], c = w[t[k + 2]];
                    // 整个三角形都不在任何关心的格子里就跳过
                    float tminx = Mathf.Min(a.x, Mathf.Min(bb.x, c.x)), tmaxx = Mathf.Max(a.x, Mathf.Max(bb.x, c.x));
                    float tminz = Mathf.Min(a.z, Mathf.Min(bb.z, c.z)), tmaxz = Mathf.Max(a.z, Mathf.Max(bb.z, c.z));
                    if (tmaxx < x0 || tminx > x1 || tmaxz < z0 || tminz > z1) continue;
                    float len = Mathf.Max((bb - a).magnitude, Mathf.Max((c - bb).magnitude, (a - c).magnitude));
                    int n = Mathf.Clamp(Mathf.CeilToInt(len / (cell * 0.5f)), 1, 600);
                    for (int i = 0; i <= n; i++)
                        for (int j = 0; j <= n - i; j++)
                        {
                            var p = a + (bb - a) * (i / (float)n) + (c - a) * (j / (float)n);
                            int ix = Mathf.FloorToInt((p.x - x0) / cell), iz = Mathf.FloorToInt((p.z - z0) / cell);
                            if (ix < 0 || iz < 0 || ix >= nx || iz >= nz) continue;
                            int id = iz * nx + ix;
                            if (!g.relevant[id]) continue;
                            float h = p.y - g.ground[id];
                            if (h < BandLo) continue;
                            if (h <= BandHi) g.solidOwner[id] = o;
                            if (h < 12f && p.y > g.maxTop[id]) g.maxTop[id] = p.y;
                        }
                }
            }
        }
        return g;
    }
}

public static partial class IslandBuilder
{
    const float ContourCell = 0.05f;
    const float BodyRadius = 0.427f;      // CollisionFitAudit.BodyRadius 实测：eggboy.fbx ×0.85，手臂算在内
    const float PlatRadius = 0.30f;       // EggboyPlayer.PlatR：带顶面的碰撞按这个半径推人
    [Serializable] public class CircleFootprint { public float x, z, r, top; public string owner; }
    static readonly List<CircleFootprint> contourCircles = new List<CircleFootprint>();

    static void BuildContourCollision(Layout layout, Transform parent)
    {
        contourCircles.Clear();
        // 1. 拆掉旧的长方形碰撞（灯塔塔顶栏杆 RaisedObstacle、水坑不动）
        int removed = 0;
        foreach (var o in UnityEngine.Object.FindObjectsOfType<TownObstacle>())
        {
            if (o.kind == TownObstacle.Kind.Water || o.GetComponent<RaisedObstacle>() != null) continue;
            var go = o.gameObject;
            if (go.GetComponent<Renderer>() != null || go.transform.childCount > 0)
            {
                UnityEngine.Object.DestroyImmediate(o);           // 灰盒本身还要当远景 LOD，只摘掉碰撞标记
            }
            else
            {
                var holder = go.transform.parent;
                UnityEngine.Object.DestroyImmediate(go);
                if (holder != null && holder.childCount == 0 && holder.name.EndsWith("_collision")) UnityEngine.Object.DestroyImmediate(holder.gameObject);
            }
            removed++;
        }
        footprints.Clear();

        // 2. 网格 + 实心
        var polys = new List<(Vector2[] pts, float y)>();
        foreach (var a in layout.walk)
        {
            var pts = new Vector2[a.x.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(a.x[i], a.z[i]);
            polys.Add((pts, a.y));
        }
        var g = ContourGrid.Build(polys, ContourCell, 0.7f, true);
        int nx = g.nx, nz = g.nz;

        // 3. 外沿：实心格，四邻里有空格（空格离可走区 0.7 m 以内）
        var buckets = new Dictionary<long, (Vector2 sum, int n, float top, int owner)>();
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                int id = iz * nx + ix;
                if (g.solidOwner[id] < 0) continue;
                bool edge = false;
                if (ix > 0 && g.relevant[id - 1] && g.solidOwner[id - 1] < 0) edge = true;
                else if (ix < nx - 1 && g.relevant[id + 1] && g.solidOwner[id + 1] < 0) edge = true;
                else if (iz > 0 && g.relevant[id - nx] && g.solidOwner[id - nx] < 0) edge = true;
                else if (iz < nz - 1 && g.relevant[id + nx] && g.solidOwner[id + nx] < 0) edge = true;
                if (!edge) continue;
                long key = ((long)(ix / 2) << 32) | (uint)(iz / 2);
                var pos = new Vector2(g.x0 + (ix + 0.5f) * g.cell, g.z0 + (iz + 0.5f) * g.cell);
                float top = g.maxTop[id];
                if (buckets.TryGetValue(key, out var bk)) buckets[key] = (bk.sum + pos, bk.n + 1, Mathf.Max(bk.top, top), bk.owner);
                else buckets[key] = (pos, 1, top, g.solidOwner[id]);
            }

        // 4. 小圆
        float r = BodyRadius - PlatRadius + ContourCell * 0.5f;
        var xs = new List<float>(); var zs = new List<float>(); var rs = new List<float>(); var tops = new List<float>();
        foreach (var kv in buckets)
        {
            var c = kv.Value.sum / kv.Value.n;
            xs.Add(c.x); zs.Add(c.y); rs.Add(r); tops.Add(kv.Value.top);
            contourCircles.Add(new CircleFootprint { x = Round3(c.x), z = Round3(c.y), r = Round3(r), top = Round3(kv.Value.top), owner = g.owners[kv.Value.owner] });
        }
        var holderGo = new GameObject("ContourCollision");
        holderGo.transform.SetParent(parent, false);
        var set = holderGo.AddComponent<TownObstacleSet>();
        set.x = xs.ToArray(); set.z = zs.ToArray(); set.r = rs.ToArray(); set.top = tops.ToArray();

        var owners = new Dictionary<string, int>();
        foreach (var cf in contourCircles) owners[cf.owner] = owners.TryGetValue(cf.owner, out int k) ? k + 1 : 1;
        var top10 = new List<KeyValuePair<string, int>>(owners); top10.Sort((a, b) => b.Value.CompareTo(a.Value));
        string summary = string.Join(" ", top10.GetRange(0, Mathf.Min(12, top10.Count)).ConvertAll(kv => kv.Key + "=" + kv.Value));
        Debug.Log($"CONTOUR removed_rects={removed} circles={contourCircles.Count} r={r:F3} owners={owners.Count} top: {summary}");
    }

    static float Round3(float v) => Mathf.Round(v * 1000f) / 1000f;
}
