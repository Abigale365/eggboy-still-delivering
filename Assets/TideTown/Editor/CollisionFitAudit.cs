// 2026-11-08 碰撞贴合检查（用户："碰撞箱的体积和判定要和建模的外体轮廓贴合"）
//   在可走区里每 5 cm 一个点（只算从出生点走得到的格子），比两件事：
//   ① 模型外形：所有可见、会挡人的模型在"离地 0.25–1.3 m"（EGGBOY 身体高度）这一段的投影（ContourGrid，和建碰撞同一套）
//   ② 实际挡人：IslandWorld 认的碰撞——长方形按推人半径 0.42 m 外扩；带顶面的圆按 PlatR 0.3 m 外扩，站得比顶面高不挡
//   理想：离模型外形 < 身体半径的点被挡，其余不挡。
//     穿模 = 没被挡、但身体会戳进模型；空气墙 = 被挡了、但离模型还有一段空
//   结果：Build/collision-fit.json（按物件汇总）+ Build/collision-fit.png（俯视图，上南下北）
//   树叶花丛不算（软的，蹭过去没关系）。
// 跑法：tools\build.ps1 -Method CollisionFitAudit.Run
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CollisionFitAudit
{
    const float Cell = 0.05f;
    const float PushRadius = 0.42f;   // EggboyPlayer.Radius
    const float PlatR = 0.30f;        // EggboyPlayer.PlatR
    const float StepH = 0.22f;        // EggboyPlayer.Step
    const float Tol = 0.08f;          // 8 cm 以内算贴合

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/TideTown/Scenes/TideTown_Main.unity");
        Physics.SyncTransforms();
        var report = Measure(out var png);
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/collision-fit.json", report);
        File.WriteAllBytes("Build/collision-fit.png", png);
        Debug.Log("COLLISIONFIT_DONE " + report.Substring(0, Mathf.Min(400, report.Length)));
    }

    // EGGBOY 身体横向半径：模型 ×0.85，离地 0.1 m 以上所有顶点到中轴的最大水平距离
    public static float BodyRadius()
    {
        var prefab = Resources.Load<GameObject>("Models/eggboy");
        if (prefab == null) return 0.427f;
        var go = Object.Instantiate(prefab);
        go.transform.position = Vector3.zero;
        go.transform.localScale = Vector3.one * 0.85f;
        float minY = float.MaxValue; var pts = new List<Vector3>();
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var m = new Mesh(); smr.BakeMesh(m);
            var mtx = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            foreach (var v in m.vertices) { var w = mtx.MultiplyPoint3x4(v); pts.Add(w); minY = Mathf.Min(minY, w.y); }
        }
        Object.DestroyImmediate(go);
        float all = 0f;
        foreach (var p in pts) if (p.y - minY > 0.1f) all = Mathf.Max(all, new Vector2(p.x, p.z).magnitude);
        Debug.Log($"EGGBOY_BODY r_all={all:F3}");
        return pts.Count > 0 ? all : 0.427f;
    }

    class Col { public float x, z, hw, hd, r, top, minY; public bool circle, hasTop; public string owner; }

    public static string Measure(out byte[] png)
    {
        float bodyR = BodyRadius();
        var world = Object.FindObjectOfType<IslandWorld>();
        var polys = world.walkPolys.Select(p => (p.points, p.y)).ToList();
        var g = ContourGrid.Build(polys, Cell, 1.0f, true);
        int nx = g.nx, nz = g.nz, N = nx * nz;

        // 只算走得到的格子：从出生点在可走、非实心格里灌水
        var reach = new bool[N];
        var sp = world.SpawnPos;
        int sx = Mathf.FloorToInt((sp.x - g.x0) / Cell), sz = Mathf.FloorToInt((sp.z - g.z0) / Cell);
        var q = new Queue<int>();
        void Seed(int ix, int iz) { if (ix < 0 || iz < 0 || ix >= nx || iz >= nz) return; int id = iz * nx + ix; if (reach[id] || !g.walk[id] || g.solidOwner[id] >= 0) return; reach[id] = true; q.Enqueue(id); }
        for (int dz = -6; dz <= 6; dz++) for (int dx = -6; dx <= 6; dx++) Seed(sx + dx, sz + dz);
        while (q.Count > 0) { int id = q.Dequeue(); int ix = id % nx, iz = id / nx; Seed(ix - 1, iz); Seed(ix + 1, iz); Seed(ix, iz - 1); Seed(ix, iz + 1); }

        // 到最近实心格的距离 + 那格的物件
        var nearX = new int[N]; var nearZ = new int[N]; var dist = new float[N];
        for (int i = 0; i < N; i++)
        {
            if (g.solidOwner[i] >= 0) { nearX[i] = i % nx; nearZ[i] = i / nx; dist[i] = 0f; }
            else { nearX[i] = -100000; nearZ[i] = -100000; dist[i] = float.MaxValue; }
        }
        void Try(int id, int from)
        {
            if (nearX[from] < -9999) return;
            float ddx = (id % nx - nearX[from]) * Cell, ddz = (id / nx - nearZ[from]) * Cell;
            float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);
            if (d < dist[id]) { dist[id] = d; nearX[id] = nearX[from]; nearZ[id] = nearZ[from]; }
        }
        for (int pass = 0; pass < 2; pass++)
        {
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    int id = iz * nx + ix;
                    if (ix > 0) Try(id, id - 1);
                    if (iz > 0) { Try(id, id - nx); if (ix > 0) Try(id, id - nx - 1); if (ix < nx - 1) Try(id, id - nx + 1); }
                }
            for (int iz = nz - 1; iz >= 0; iz--)
                for (int ix = nx - 1; ix >= 0; ix--)
                {
                    int id = iz * nx + ix;
                    if (ix < nx - 1) Try(id, id + 1);
                    if (iz < nz - 1) { Try(id, id + nx); if (ix < nx - 1) Try(id, id + nx + 1); if (ix > 0) Try(id, id + nx - 1); }
                }
        }

        // 场景里实际的碰撞（和 IslandWorld.CollectObstacles 同一套）
        var cols = new List<Col>();
        foreach (var o in Object.FindObjectsOfType<TownObstacle>())
        {
            if (o.kind == TownObstacle.Kind.Water) continue;
            var box = o.GetComponent<BoxCollider>(); if (box == null) continue;
            var t = o.transform;
            Vector3 c = t.TransformPoint(box.center), s = Vector3.Scale(box.size, t.lossyScale);
            var col = new Col { x = c.x, z = c.z, hw = Mathf.Abs(s.x) * 0.5f, hd = Mathf.Abs(s.z) * 0.5f, minY = o.minY, owner = ContourGrid.OwnerName(t) };
            if (o.kind == TownObstacle.Kind.WalkableTop) { col.hasTop = true; col.top = c.y + s.y * 0.5f; }
            cols.Add(col);
        }
        foreach (var set in Object.FindObjectsOfType<TownObstacleSet>())
            for (int i = 0; i < set.x.Length; i++)
                cols.Add(new Col { x = set.x[i], z = set.z[i], r = set.r[i], top = set.top[i], circle = true, hasTop = true, owner = "contour" });

        var blockOwner = new int[N]; for (int i = 0; i < N; i++) blockOwner[i] = -1;
        var ownerNames = new List<string>(g.owners); var ownerIdx = new Dictionary<string, int>();
        for (int i = 0; i < ownerNames.Count; i++) ownerIdx[ownerNames[i]] = i;
        int Own(string n) { if (!ownerIdx.TryGetValue(n, out int i)) { i = ownerNames.Count; ownerNames.Add(n); ownerIdx[n] = i; } return i; }
        int circleCount = 0;
        foreach (var c in cols)
        {
            float rad = c.hasTop ? PlatR : PushRadius;
            float ex = c.circle ? c.r + rad : c.hw + rad, ez = c.circle ? c.r + rad : c.hd + rad;
            if (c.circle) circleCount++;
            int ia = Mathf.FloorToInt((c.x - ex - g.x0) / Cell), ib = Mathf.CeilToInt((c.x + ex - g.x0) / Cell);
            int ja = Mathf.FloorToInt((c.z - ez - g.z0) / Cell), jb = Mathf.CeilToInt((c.z + ez - g.z0) / Cell);
            int ow = -2;
            for (int iz = Mathf.Max(0, ja); iz < Mathf.Min(nz, jb); iz++)
                for (int ix = Mathf.Max(0, ia); ix < Mathf.Min(nx, ib); ix++)
                {
                    int id = iz * nx + ix;
                    if (!reach[id]) continue;
                    float x = g.x0 + (ix + 0.5f) * Cell, z = g.z0 + (iz + 0.5f) * Cell;
                    if (c.circle) { float ddx = x - c.x, ddz = z - c.z; if (ddx * ddx + ddz * ddz >= ex * ex) continue; }
                    else if (Mathf.Abs(x - c.x) >= ex || Mathf.Abs(z - c.z) >= ez) continue;
                    float gy = g.ground[id];
                    if (c.hasTop && (gy >= c.top - 0.03f || c.top - gy <= StepH)) continue;
                    if (c.minY > 0f && gy < c.minY - 0.03f) continue;
                    if (ow == -2)
                    {
                        // 圆按它所在格子的实心物件记名
                        if (c.circle)
                        {
                            int cx = Mathf.FloorToInt((c.x - g.x0) / Cell), cz = Mathf.FloorToInt((c.z - g.z0) / Cell);
                            int cid = Mathf.Clamp(cz, 0, nz - 1) * nx + Mathf.Clamp(cx, 0, nx - 1);
                            int so = g.solidOwner[cid];
                            ow = so >= 0 ? so : Own("contour(?)");
                        }
                        else ow = Own(c.owner);
                    }
                    blockOwner[id] = ow;
                }
        }

        var pen = new Dictionary<int, int>(); var air = new Dictionary<int, int>();
        int walkCells = 0, penCells = 0, airCells = 0, okBlocked = 0;
        var img = new Color32[N];
        for (int id = 0; id < N; id++)
        {
            bool blocked = blockOwner[id] >= 0;
            float d = dist[id];
            Color32 col = g.solidOwner[id] >= 0 ? new Color32(30, 30, 30, 255)
                : reach[id] ? new Color32(200, 200, 200, 255)
                : g.walk[id] ? new Color32(150, 150, 170, 255) : new Color32(120, 150, 120, 255);
            if (reach[id])
            {
                walkCells++;
                // 最近的实心格如果最高点不比脚下高一步（平台自己往下的崖边），那是悬崖不是障碍，不算穿模
                int nearId = nearX[id] > -9999 ? nearZ[id] * nx + nearX[id] : -1;
                bool obstacle = nearId >= 0 && g.maxTop[nearId] - g.ground[id] > StepH;
                if (!blocked && obstacle && d < bodyR - Tol)
                {
                    penCells++; col = new Color32(230, 40, 200, 255);
                    int ow = g.solidOwner[nearZ[id] * nx + nearX[id]];
                    pen[ow] = pen.TryGetValue(ow, out int c0) ? c0 + 1 : 1;
                }
                else if (blocked && d > bodyR + Tol)
                {
                    airCells++; col = new Color32(250, 200, 30, 255);
                    int ow = blockOwner[id];
                    air[ow] = air.TryGetValue(ow, out int c1) ? c1 + 1 : 1;
                }
                else if (blocked) { okBlocked++; col = new Color32(220, 120, 110, 255); }
            }
            img[id] = col;
        }
        var tex = new Texture2D(nx, nz, TextureFormat.RGBA32, false);
        tex.SetPixels32(img); tex.Apply();
        png = tex.EncodeToPNG();

        var inv = CultureInfo.InvariantCulture;
        float a2 = Cell * Cell;
        string F(float v) => v.ToString("F2", inv);
        string List(Dictionary<int, int> m) => string.Join(",", m.OrderByDescending(kv => kv.Value).Take(40)
            .Select(kv => "{\"owner\":\"" + ownerNames[kv.Key].Replace("\"", "'") + "\",\"m2\":" + F(kv.Value * a2) + "}"));
        return "{" +
            "\"bodyRadius\":" + bodyR.ToString("F3", inv) + ",\"pushRadius\":" + F(PushRadius) + ",\"platR\":" + F(PlatR) + ",\"tolerance\":" + F(Tol) +
            ",\"colliders\":" + cols.Count + ",\"circles\":" + circleCount +
            ",\"reachable_m2\":" + F(walkCells * a2) + ",\"blocked_ok_m2\":" + F(okBlocked * a2) +
            ",\"penetrate_m2\":" + F(penCells * a2) + ",\"airwall_m2\":" + F(airCells * a2) +
            ",\"grid\":{\"x0\":" + F(g.x0) + ",\"z0\":" + F(g.z0) + ",\"cell\":" + F(Cell) + ",\"nx\":" + nx + ",\"nz\":" + nz + "}" +
            ",\"penetrate\":[" + List(pen) + "],\"airwall\":[" + List(air) + "]}";
    }
}
