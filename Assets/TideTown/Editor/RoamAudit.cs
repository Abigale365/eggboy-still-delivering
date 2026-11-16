// 2026-11-16 自由漫游审计（用户议题 2）：编辑器里打开主场景，按 IslandWorld.RoamStep 同一套规则把整座岛铺成 0.2 m 格子做连通检查。
//   · 每格记下所有朝上的 Ground 面（多层：坡道底下的海岸、灯塔台……）；
//   · 节点 = (格子, 某一层面)。走一步：目标格取"身体顶部（脚 + 1.3 m）往下最高的面"，
//     比脚高 ≤ 0.23 m 直接走、≤ 0.65 m 跳上去；往下 ≤ 3 m 跳下去；轮廓碰撞圆挡住的、水太深的、禁区里的不能站；
//   · 从出生点正向泛洪 = 能走到的地方；反向泛洪 = 能走回出生点的地方。能到、回不来的就是"坑"（陷阱）。
// 场景：L0 / L1 / L3 木箱都在（不考虑缺口）查陷阱；L2 缺口（木箱全拿掉）查有没有别的路绕到船棚（油壶）那边。
// 输出 Build/roam/roam-audit.json + 每个场景一张俯视图 Build/roam/roam_<场景>.png。
// 用法：Unity -batchmode -nographics -executeMethod RoamAudit.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoamAudit
{
    const float Cell = 0.2f, BodyTop = 1.3f, StepUp = 0.23f, JumpUp = 0.65f, MaxDrop = 3.0f, PlatR = 0.30f, BodyR = 0.42f;
    [Serializable] class Block { public string id, when; public float minY = -10f, maxY = 100f; public float[] x, z; }
    [Serializable] class BlockDoc { public Block[] blocks; }
    [Serializable] class Coast { public float[] x, z; }
    [Serializable] class Tier { public string id; public float y; public float[] x, z; }
    [Serializable] class LayoutDoc { public Coast coast; public Tier[] tiers; }
    [Serializable] class RoutePt { public string id; public float x, z; }
    [Serializable] class RouteDoc { public RoutePt[] route; }

    struct Surf { public float y; public bool crate; }
    class Obs { public float x, z, r, hw, hd, top, minY; public bool box, hasTop; }

    static int nx, nz; static float x0, z0;
    static Surf[][] cols;
    static Vector2[] beach; static float beachY;
    static List<Obs>[] obsGrid; static int ogx, ogz;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/TideTown/Scenes/TideTown_Main.unity", OpenSceneMode.Single);
        var world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
        var layout = JsonUtility.FromJson<LayoutDoc>(File.ReadAllText("Assets/TideTown/Data/Resources/World/world_layout.json"));
        var blocks = File.Exists("Assets/TideTown/Data/Resources/World/roam_blocks.json")
            ? (JsonUtility.FromJson<BlockDoc>(File.ReadAllText("Assets/TideTown/Data/Resources/World/roam_blocks.json")).blocks ?? new Block[0]) : new Block[0];
        var bt = Array.Find(layout.tiers, t => t.id == "beach");
        beach = bt == null ? null : bt.x.Select((x, i) => new Vector2(x, bt.z[i])).ToArray(); beachY = bt?.y ?? 0f;
        var coast = layout.coast.x.Select((x, i) => new Vector2(x, layout.coast.z[i])).ToArray();
        x0 = coast.Min(p => p.x) - 1f; z0 = coast.Min(p => p.y) - 1f;
        nx = Mathf.CeilToInt((coast.Max(p => p.x) + 1f - x0) / Cell); nz = Mathf.CeilToInt((coast.Max(p => p.y) + 1f - z0) / Cell);

        // 1. 每格所有朝上的地面
        int groundMask = 1 << 6;
        cols = new Surf[nx * nz][];
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = x0 + (ix + .5f) * Cell, z = z0 + (iz + .5f) * Cell;
                var hits = Physics.RaycastAll(new Vector3(x, 80f, z), Vector3.down, 120f, groundMask, QueryTriggerInteraction.Ignore);
                var list = new List<Surf>();
                foreach (var h in hits.OrderByDescending(h => h.point.y))
                {
                    if (h.normal.y < 0.4f) continue;
                    if (list.Count > 0 && list[list.Count - 1].y - h.point.y < 0.05f) continue;
                    list.Add(new Surf { y = h.point.y, crate = h.collider.GetComponentInParent<FloatingCrate>() != null });
                }
                cols[iz * nx + ix] = list.ToArray();
            }
        // 2. 碰撞（和 IslandWorld.CollectObstacles 同源）
        var obs = new List<Obs>();
        foreach (var set in UnityEngine.Object.FindObjectsOfType<TownObstacleSet>())
            for (int i = 0; i < set.x.Length; i++) obs.Add(new Obs { x = set.x[i], z = set.z[i], r = set.r[i], top = set.top[i], hasTop = true });
        foreach (var o in UnityEngine.Object.FindObjectsOfType<TownObstacle>())
        {
            if (o.kind == TownObstacle.Kind.Water) continue;
            var box = o.GetComponent<BoxCollider>(); if (box == null) continue;
            var c = o.transform.TransformPoint(box.center); var s = Vector3.Scale(box.size, o.transform.lossyScale);
            obs.Add(new Obs { box = true, x = c.x, z = c.z, hw = s.x * .5f, hd = s.z * .5f, minY = o.minY, hasTop = o.kind == TownObstacle.Kind.WalkableTop, top = c.y + s.y * .5f });
        }
        ogx = Mathf.CeilToInt(nx * Cell); ogz = Mathf.CeilToInt(nz * Cell);
        obsGrid = new List<Obs>[ogx * ogz];
        foreach (var o in obs)
        {
            float ex = (o.box ? o.hw : o.r) + BodyR + .1f, ez = (o.box ? o.hd : o.r) + BodyR + .1f;
            for (int gz = Mathf.FloorToInt(o.z - ez - z0); gz <= Mathf.FloorToInt(o.z + ez - z0); gz++)
                for (int gx = Mathf.FloorToInt(o.x - ex - x0); gx <= Mathf.FloorToInt(o.x + ex - x0); gx++)
                {
                    if (gx < 0 || gz < 0 || gx >= ogx || gz >= ogz) continue;
                    (obsGrid[gz * ogx + gx] ??= new List<Obs>()).Add(o);
                }
        }

        var route = JsonUtility.FromJson<RouteDoc>(Resources.Load<TextAsset>("World/route").text).route;
        Vector3 oil = Vector3.zero; foreach (var it in LighthouseData.Load().items) if (it.id == "oil") oil = new Vector3(it.x, it.y, it.z);
        var spawn = world.SpawnPos;

        lastTrapClusters.Clear();
        var report = new StringBuilder("{\"scenarios\":[");
        var scen = new (string name, float water, bool gap)[] { ("L0", 0f, false), ("L1", .45f, false), ("L2_bridge", 1.15f, false), ("L2_gap", 1.15f, true), ("L3", 1.60f, false) };
        bool allOk = true;
        for (int si = 0; si < scen.Length; si++)
        {
            var (name, water, gap) = scen[si];
            var res = Flood(spawn, water, gap, blocks, out var nodesY, out var reach, out var back, out int nodeCount);
            // 陷阱：能到、回不来
            int trap = 0; var trapCells = new List<int>();
            for (int n = 0; n < nodeCount; n++) if (reach[n] && !back[n]) { trap++; trapCells.Add(n); }
            var clusters = Clusters(trapCells, res, nodesY);
            lastTrapClusters.AddRange(clusters);
            // 路线站点能不能到（缺口场景只看缺口这一侧）
            int routeMiss = 0; var miss = new List<string>();
            if (!gap)
                foreach (var rp in route)
                {
                    if (!NearReach(rp.x, rp.z, 1.0f, res, reach, back)) { routeMiss++; miss.Add(rp.id); }
                }
            bool oilReach = NearReach(oil.x, oil.z, 1.5f, res, reach, null);
            bool ok = gap ? !oilReach && trap == 0 : trap == 0;
            // L1 起沙滩 / 海岸低地会被淹：路线点在水下不算漏
            allOk &= ok;
            if (si > 0) report.Append(',');
            report.Append($"{{\"name\":\"{name}\",\"water\":{water:F2},\"gap\":{(gap ? "true" : "false")},\"nodes\":{nodeCount},\"reach\":{reach.Count(b => b)},\"traps\":{trap},\"routeMissing\":[{string.Join(",", miss.Select(m => "\"" + m + "\""))}],\"oilReachable\":{(oilReach ? "true" : "false")},\"ok\":{(ok ? "true" : "false")},\"trapClusters\":[");
            for (int k = 0; k < clusters.Count && k < 40; k++)
            {
                var cl = clusters[k];
                if (k > 0) report.Append(',');
                report.Append("{\"n\":" + cl.n + ",\"x\":" + F(cl.cx) + ",\"z\":" + F(cl.cz) + ",\"y\":" + F(cl.cy) + ",\"minX\":" + F(cl.minX) + ",\"maxX\":" + F(cl.maxX) + ",\"minZ\":" + F(cl.minZ) + ",\"maxZ\":" + F(cl.maxZ) + "}");
            }
            report.Append("]}");
            DrawMap(name, res, reach, back, route, oil, water);
            Debug.Log($"ROAM {name} reach={reach.Count(b => b)} traps={trap} clusters={clusters.Count} routeMissing={routeMiss} oilReachable={oilReach} ok={ok}");
        }
        report.Append($"],\"ok\":{(allOk ? "true" : "false")}}}");
        Directory.CreateDirectory("Build/roam");
        File.WriteAllText("Build/roam/roam-audit.json", report.ToString());
        Debug.Log("ROAM_DONE ok=" + allOk);
    }

    static string F(float v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    // 能跳进去、走不回来的坑：每个坑按包围盒外扩 0.25 m 写成一块禁区（高度限在坑底 ±0.5 m），
    // 人走到坑边就停住，不会掉进去出不来。手写的禁区（id 不以 auto_trap 开头）保留。用法：-executeMethod RoamAudit.BakeTrapBlocks
    public static void BakeTrapBlocks()
    {
        const string path = "Assets/TideTown/Data/Resources/World/roam_blocks.json";
        for (int round = 0; round < 3; round++)
        {
            File.WriteAllText(path, JsonUtility.ToJson(new BlockDoc { blocks = (File.Exists(path) ? JsonUtility.FromJson<BlockDoc>(File.ReadAllText(path)).blocks ?? new Block[0] : new Block[0]) }, true));
            Run();
            var doc = File.Exists(path) ? JsonUtility.FromJson<BlockDoc>(File.ReadAllText(path)) : new BlockDoc();
            var list = new List<Block>(doc.blocks ?? new Block[0]);
            int added = 0;
            foreach (var cl in lastTrapClusters)
            {
                bool dup = false;
                foreach (var b in list)
                    if (cl.cy >= b.minY && cl.cy <= b.maxY && InPoly(b.x.Select((bx, i) => new Vector2(bx, b.z[i])).ToArray(), cl.cx, cl.cz)) { dup = true; break; }
                if (dup) continue;
                float x0b = cl.minX - .35f, x1b = cl.maxX + .35f, z0b = cl.minZ - .35f, z1b = cl.maxZ + .35f;
                list.Add(new Block { id = "auto_trap_" + list.Count, when = "always", minY = cl.minYv - .5f, maxY = cl.maxYv + .5f, x = new[] { x0b, x1b, x1b, x0b }, z = new[] { z0b, z0b, z1b, z1b } });
                added++;
            }
            File.WriteAllText(path, JsonUtility.ToJson(new BlockDoc { blocks = list.ToArray() }, true));
            Debug.Log("ROAM_BAKE round=" + round + " added=" + added);
            if (added == 0) break;
        }
        Run();
    }
    static readonly List<Cluster> lastTrapClusters = new List<Cluster>();

    // 节点编号：每格的面按顺序排，nodeBase[c] + k
    class Graph { public int[] nodeBase; public int total; public float water; public bool gap; public Block[] blocks; }

    static Graph Flood(Vector3 spawn, float water, bool gap, Block[] blocks, out float[] nodesY, out bool[] reach, out bool[] back, out int nodeCount)
    {
        var g = new Graph { nodeBase = new int[nx * nz + 1], water = water, gap = gap, blocks = blocks };
        int total = 0;
        for (int c = 0; c < nx * nz; c++) { g.nodeBase[c] = total; total += cols[c].Length; }
        g.nodeBase[nx * nz] = total; g.total = total; nodeCount = total;
        nodesY = new float[total];
        var valid = new bool[total];
        for (int c = 0; c < nx * nz; c++)
        {
            float x = x0 + (c % nx + .5f) * Cell, z = z0 + (c / nx + .5f) * Cell;
            for (int k = 0; k < cols[c].Length; k++)
            {
                var s = cols[c][k]; int n = g.nodeBase[c] + k; nodesY[n] = s.y;
                valid[n] = !(gap && s.crate) && Standable(x, z, s.y, g);
            }
        }
        // 正向边
        var fwd = new List<int>[total];
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        for (int c = 0; c < nx * nz; c++)
        {
            int cx = c % nx, cz = c / nx;
            for (int k = 0; k < cols[c].Length; k++)
            {
                int n = g.nodeBase[c] + k; if (!valid[n]) continue;
                float y = nodesY[n];
                for (int d = 0; d < 8; d++)
                {
                    int ex = cx + dx[d], ez = cz + dz[d]; if (ex < 0 || ez < 0 || ex >= nx || ez >= nz) continue;
                    int c2 = ez * nx + ex;
                    // 目标格：身体顶部往下最高的面（木箱在缺口场景里不算）
                    int best = -1;
                    for (int k2 = 0; k2 < cols[c2].Length; k2++)
                    {
                        var s2 = cols[c2][k2]; if (gap && s2.crate) continue;
                        if (s2.y <= y + BodyTop) { best = k2; break; }
                    }
                    if (best < 0) continue;
                    int n2 = g.nodeBase[c2] + best; float y2 = nodesY[n2];
                    if (y2 - y > JumpUp || y - y2 > MaxDrop) continue;
                    if (!valid[n2])
                    {
                        // 跳下崖：落点紧贴崖脚那 0.45 m 被崖壁的轮廓碰撞圆推开，人会落在再往外一点的地方
                        if (y - y2 < 0.5f) continue;
                        for (int step = 2; step <= 3; step++)
                        {
                            int fx = cx + dx[d] * step, fz = cz + dz[d] * step; if (fx < 0 || fz < 0 || fx >= nx || fz >= nz) break;
                            int c3 = fz * nx + fx; int b3 = -1;
                            for (int k3 = 0; k3 < cols[c3].Length; k3++) { var s3 = cols[c3][k3]; if (gap && s3.crate) continue; if (s3.y <= y + BodyTop) { b3 = k3; break; } }
                            if (b3 < 0) break;
                            int n3 = g.nodeBase[c3] + b3;
                            if (Mathf.Abs(nodesY[n3] - y2) > 0.3f) break;
                            if (valid[n3]) { (fwd[n] ??= new List<int>(4)).Add(n3); break; }
                        }
                        continue;
                    }
                    (fwd[n] ??= new List<int>(4)).Add(n2);
                }
            }
        }
        // 出生点
        int sc = Col(spawn.x, spawn.z), start = -1; float bd = float.MaxValue;
        for (int k = 0; k < cols[sc].Length; k++) { float d = Mathf.Abs(cols[sc][k].y - spawn.y); if (d < bd && valid[g.nodeBase[sc] + k]) { bd = d; start = g.nodeBase[sc] + k; } }
        reach = new bool[total]; back = new bool[total];
        if (start < 0) { Debug.LogError("ROAM spawn not standable"); return g; }
        var q = new Queue<int>(); reach[start] = true; q.Enqueue(start);
        while (q.Count > 0) { int n = q.Dequeue(); if (fwd[n] == null) continue; foreach (int m in fwd[n]) if (!reach[m]) { reach[m] = true; q.Enqueue(m); } }
        var rev = new List<int>[total];
        for (int n = 0; n < total; n++) if (fwd[n] != null) foreach (int m in fwd[n]) (rev[m] ??= new List<int>(4)).Add(n);
        back[start] = true; q.Enqueue(start);
        while (q.Count > 0) { int n = q.Dequeue(); if (rev[n] == null) continue; foreach (int m in rev[n]) if (!back[m]) { back[m] = true; q.Enqueue(m); } }
        return g;
    }

    static int Col(float x, float z) => Mathf.Clamp(Mathf.FloorToInt((z - z0) / Cell), 0, nz - 1) * nx + Mathf.Clamp(Mathf.FloorToInt((x - x0) / Cell), 0, nx - 1);

    static bool Standable(float x, float z, float y, Graph g)
    {
        bool onBeach = beach != null && Mathf.Abs(y - beachY) <= .12f && InPoly(beach, x, z);
        if (y < g.water - (onBeach ? IslandWorld.WadeDepth : IslandWorld.RoadWadeDepth)) return false;
        foreach (var b in g.blocks)
        {
            if (y < b.minY || y > b.maxY) continue;
            if (!string.IsNullOrEmpty(b.when) && b.when != "always" && !(b.when == "gap" && g.gap)) continue;
            var pts = b.x.Select((bx, i) => new Vector2(bx, b.z[i])).ToArray();
            if (InPoly(pts, x, z)) return false;
        }
        int gx = Mathf.FloorToInt(x - x0), gz = Mathf.FloorToInt(z - z0);
        if (gx < 0 || gz < 0 || gx >= ogx || gz >= ogz) return true;
        var list = obsGrid[gz * ogx + gx]; if (list == null) return true;
        foreach (var o in list)
        {
            if (o.minY > 0f && y < o.minY - .03f) continue;
            if (o.hasTop && (y >= o.top - .03f || o.top - y <= StepUp)) continue;
            float rad = o.hasTop ? PlatR : BodyR;
            if (o.box) { if (Mathf.Abs(x - o.x) < o.hw + rad && Mathf.Abs(z - o.z) < o.hd + rad) return false; }
            else if ((x - o.x) * (x - o.x) + (z - o.z) * (z - o.z) < (o.r + rad) * (o.r + rad)) return false;
        }
        return true;
    }

    static bool NearReach(float x, float z, float radius, Graph g, bool[] reach, bool[] back)
    {
        int r = Mathf.CeilToInt(radius / Cell); int c0 = Col(x, z); int cx = c0 % nx, cz = c0 / nx;
        for (int ez = cz - r; ez <= cz + r; ez++)
            for (int ex = cx - r; ex <= cx + r; ex++)
            {
                if (ex < 0 || ez < 0 || ex >= nx || ez >= nz) continue;
                int c = ez * nx + ex;
                for (int n = g.nodeBase[c]; n < g.nodeBase[c + 1]; n++) if (reach[n] && (back == null || back[n])) return true;
            }
        return false;
    }

    class Cluster { public int n; public float cx, cz, cy, minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f, minYv = 1e9f, maxYv = -1e9f; }
    static List<Cluster> Clusters(List<int> nodes, Graph g, float[] nodesY)
    {
        // 按格子 4 邻接合并（不分层，够用来定位）
        var colOf = new Dictionary<int, int>();
        foreach (int n in nodes) { int c = Array.BinarySearch(g.nodeBase, 0, nx * nz, n); if (c < 0) c = ~c - 1; while (c + 1 < g.nodeBase.Length && g.nodeBase[c + 1] <= n) c++; colOf[n] = c; }
        var cellSet = new Dictionary<int, float>(); foreach (var kv in colOf) cellSet[kv.Value] = nodesY[kv.Key];
        var seen = new HashSet<int>(); var result = new List<Cluster>();
        foreach (int c in cellSet.Keys)
        {
            if (seen.Contains(c)) continue;
            var cl = new Cluster(); var st = new Stack<int>(); st.Push(c); seen.Add(c);
            while (st.Count > 0)
            {
                int cc = st.Pop(); float x = x0 + (cc % nx + .5f) * Cell, z = z0 + (cc / nx + .5f) * Cell;
                cl.n++; cl.cx += x; cl.cz += z; cl.minX = Mathf.Min(cl.minX, x); cl.maxX = Mathf.Max(cl.maxX, x); cl.minZ = Mathf.Min(cl.minZ, z); cl.maxZ = Mathf.Max(cl.maxZ, z);
                float ny = cellSet[cc]; cl.cy += ny; cl.minYv = Mathf.Min(cl.minYv, ny); cl.maxYv = Mathf.Max(cl.maxYv, ny);
                foreach (int nb in new[] { cc + 1, cc - 1, cc + nx, cc - nx }) if (cellSet.ContainsKey(nb) && seen.Add(nb)) st.Push(nb);
            }
            cl.cx /= cl.n; cl.cz /= cl.n; cl.cy /= cl.n; result.Add(cl);
        }
        return result.OrderByDescending(c => c.n).ToList();
    }

    static void DrawMap(string name, Graph g, bool[] reach, bool[] back, RoutePt[] route, Vector3 oil, float water)
    {
        var tex = new Texture2D(nx, nz, TextureFormat.RGB24, false);
        var px = new Color32[nx * nz];
        for (int c = 0; c < nx * nz; c++)
        {
            Color32 col = cols[c].Length == 0 ? new Color32(30, 50, 90, 255) : new Color32(70, 70, 70, 255);
            if (cols[c].Length > 0 && cols[c][0].y < water) col = new Color32(40, 80, 140, 255);
            for (int n = g.nodeBase[c]; n < g.nodeBase[c + 1]; n++)
            {
                if (reach[n] && back[n]) { float y = cols[c][n - g.nodeBase[c]].y; byte v = (byte)Mathf.Clamp(90 + y * 18f, 0, 255); col = new Color32(40, v, 60, 255); break; }
                if (reach[n] && !back[n]) { col = new Color32(240, 40, 40, 255); break; }
            }
            px[c] = col;
        }
        foreach (var rp in route) { int c = Col(rp.x, rp.z); px[c] = new Color32(255, 255, 255, 255); }
        { int c = Col(oil.x, oil.z); for (int k = -2; k <= 2; k++) { int cc = c + k; if (cc >= 0 && cc < px.Length) px[cc] = new Color32(255, 200, 0, 255); } }
        tex.SetPixels32(px); tex.Apply();
        Directory.CreateDirectory("Build/roam");
        File.WriteAllBytes("Build/roam/roam_" + name + ".png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static bool InPoly(Vector2[] poly, float x, float z)
    {
        if (poly == null || poly.Length < 3) return false;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
}
