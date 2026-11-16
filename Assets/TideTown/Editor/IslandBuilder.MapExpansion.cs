// 2026-11-16 四向全景地图扩建（docs/MAP_EXPANSION_BOOK_20261115.md，参考图 docs/reference/four_views/）。
// 全部程序化网格 + 仓库现有材质 / 模型，不调 Lux3D、不用 ExpandedTownBuilder。在 BuildMain 里紧跟 ApplyCoastalPromenade 执行。
//   EXP-02 住宅台地前沿：2.5 m 直墙前面加"上沿矮石坎 + 草坡 + 下沿矮石坎 + 花带"（只是外观，不新增可走区）
//   视线走廊：西山顶俯瞰机位（spot_14）镜头前 16 m 内挡住画面的树冠清掉
//   EXP-05 西山塌方：层状岩壁 + 坡顶乱石 + 两堆滚石 + X 型圆木支撑架 + 警戒绳
//   EXP-06 港口：随潮水起伏的红白顶蒸汽小渡轮 + 低码头上的木箱麻袋
//   EXP-01 灯塔 → 东院的崖顶步道（高架段两侧石墙封到海床）+ 三座院落背后的北侧后环路；都是可走区（Ground 层 + walkPolys）
// 所有随机摆放用自己的 Lcg，岛上原有的石头、花、树位置不受影响。route.json 主线不改。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    static void ApplyMapExpansion(Layout layout, Transform parent)
    {
        var old = parent.Find("MapExpansion"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("MapExpansion").transform; root.SetParent(parent, false);
        var world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
        var areas = layout.walk.ToList();
        var walks = areas.Select(a => new WalkArea { y = a.y, poly = a.x.Select((x, i) => new Vector2(x, a.z[i])).ToList() }).ToList();
        var masonry = CoastalMaterial("SeawallMasonry", true);
        var paving = CoastalMaterial("PromenadePaving", false);
        var cap = FlatMat("CoastalCapstone", new Color(.77f, .66f, .50f), .13f);
        var timber = FlatMat("CoastalRailTimber", new Color(.43f, .29f, .14f), .12f);
        Physics.SyncTransforms();

        ExpRingWalks(layout, parent, root, world, areas, paving, masonry, cap, timber);
        layout.walk = areas.ToArray();
        walks = areas.Select(a => new WalkArea { y = a.y, poly = a.x.Select((x, i) => new Vector2(x, a.z[i])).ToList() }).ToList();
        Physics.SyncTransforms();
        ExpTerraceBank(layout, parent, root, walks, masonry, cap);
        ExpHouseWallFinish(layout, parent, root, walks, masonry, cap);
        ExpViewCorridor(parent);
        ExpLandslide(layout, parent, root, walks);
        ExpFerryAndCargo(root);
        ExpSchoolPathBenches(layout, parent, root, paving);
        ExpBeachProps(layout, root);
        ExpWestSeawall(layout, parent, root, walks, masonry, cap, timber);
        ExpTreesInWater(layout, parent);
        ExpLighthouseWedge(parent);
        Physics.SyncTransforms();
        ExpFinalGroundCleanup(layout, parent);
        Physics.SyncTransforms();
    }

    // ---------------- EXP-05 补充：学校前院弧形石板小径 + 两张木长椅 ----------------
    // 小径从校门坡顶弯到学校正门，是贴在草坪上的装饰铺面（草坪本来就能走，不另加碰撞）；路上的草叶裁掉。
    // 长椅避开秋千、滑梯、红球、狗碗、剧情站位 19 和路线。
    static void ExpSchoolPathBenches(Layout layout, Transform parent, Transform root, Material paving)
    {
        var lawn = Array.Find(layout.tiers, t => t.id == "pad_grass_3_0_0");
        if (lawn == null) return;
        // 2026-11-16 下午（图 4 三层同高闪烁）：小径原来只比草坪高 1.2 mm（运行时草坪又被压到 3.028），和学校门前石板带、
        // 草坪三层几乎同高。小径抬到草坪上 2.2 cm
        float y = lawn.y + 0.022f;
        // 门前那条直的六角石板带（Entrance_B3_school）和这条弯小径是同一条路的两份，整块关掉（连碰撞一起，免得人浮在看不见的石板上）；
        // 弯小径换成同样的六角石板材质，并且自己带地面碰撞
        var schoolEntrance = parent.Find("BuildingEntrances/Entrance_B3_school");
        if (schoolEntrance != null) schoolEntrance.gameObject.SetActive(false);
        var hexStone = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/IslandCobble.mat") ?? paving;
        var path = SmoothCoastal(new[] { new Vector3(13.7f, y, -5.55f), new Vector3(13.2f, y, -4.6f), new Vector3(12.95f, y, -3.3f), new Vector3(13.35f, y, -2.0f), new Vector3(13.7f, y, -0.85f) });
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); float run = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0) run += Vector3.Distance(path[i - 1], path[i]);
            var s = CoastalSide(path, i) * 0.55f;
            v.Add(path[i] - s); v.Add(path[i] + s); uv.Add(new Vector2(0f, run * 0.5f)); uv.Add(new Vector2(0.55f, run * 0.5f));
            if (i > 0) { int k = (i - 1) * 2; FinishQuad(t, k, k + 2, k + 3, k + 1); }
        }
        for (int i = 0; i < uv.Count; i++) uv[i] = new Vector2(v[i].x, v[i].z);   // 和门前石板一样按世界坐标铺
        var pathGo = CoastalPart("exp_school_path", FinishMesh("exp_school_path", v, uv, t), root, hexStone);
        pathGo.layer = 6; pathGo.AddComponent<MeshCollider>().sharedMesh = pathGo.GetComponent<MeshFilter>().sharedMesh;
        foreach (var mf in parent.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if (mf.sharedMesh == null || !HasPlacementAncestor(mf.transform, "GrassField")) continue;
            string key = mf.name; for (var tr = mf.transform; tr != parent && tr != null; tr = tr.parent) key += "_" + tr.GetSiblingIndex();
            TrimCoastalMesh(mf, p => p.y < 3.5f && CoastalDistance(p, path) < 0.62f, "exp_trim_school_" + key);
        }
        var wood = FlatMat("BenchWood", new Color32(0x9a, 0x6a, 0x3e, 255), 0.1f);
        var iron = FlatMat("BenchIron", new Color32(0x3a, 0x3a, 0x40, 255), 0.35f);
        var bench = new[] { (new Vector3(11.85f, lawn.y, -3.0f), 90f), (new Vector3(15.25f, lawn.y, -1.25f), 180f) };
        int n = 0;
        foreach (var (pos, yaw) in bench)
        {
            var go = new GameObject("SchoolBench_" + n++); go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            void Box(Vector3 lp, Vector3 s, Material m, float tilt = 0f)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.DestroyImmediate(b.GetComponent<Collider>());
                b.transform.SetParent(go.transform, false); b.transform.localPosition = lp; b.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f); b.transform.localScale = s;
                b.GetComponent<Renderer>().sharedMaterial = m;
            }
            // 座板三条、靠背两条（朝 +z 坐），两侧铸铁腿
            foreach (float z in new[] { -0.13f, 0f, 0.13f }) Box(new Vector3(0f, 0.43f, z), new Vector3(1.3f, 0.04f, 0.11f), wood);
            foreach (float yy in new[] { 0.62f, 0.78f }) Box(new Vector3(0f, yy, -0.22f), new Vector3(1.3f, 0.10f, 0.035f), wood, -12f);
            foreach (float x in new[] { -0.58f, 0.58f })
            {
                Box(new Vector3(x, 0.21f, 0.12f), new Vector3(0.05f, 0.42f, 0.05f), iron);
                Box(new Vector3(x, 0.40f, -0.18f), new Vector3(0.05f, 0.80f, 0.05f), iron, -8f);
                Box(new Vector3(x, 0.40f, 0f), new Vector3(0.05f, 0.04f, 0.42f), iron);
            }
        }
        Debug.Log("EXP_SCHOOL path=" + path.Count + " benches=" + n);
    }

    // ---------------- EXP-06 补充：沙滩晒网木架、船桨、海星 ----------------
    // 都放在沙滩西半边（沙滩入口跳台在东边 x −0.2–4.8，涉水测试也在那边），只是摆件，不挡路线。
    static void ExpBeachProps(Layout layout, Transform root)
    {
        var beach = Array.Find(layout.tiers, t => t.id == "beach");
        if (beach == null) return;
        float y = beach.y;
        var holder = new GameObject("BeachProps").transform; holder.SetParent(root, false);
        var wood = FlatMat("BeachRackWood", new Color32(0x8a, 0x66, 0x44, 255), 0.06f);
        var net = FlatMat("BeachNet", new Color32(0x6f, 0x8f, 0x8a, 255), 0.05f);
        var star = FlatMat("BeachStarfish", new Color32(0xf0, 0x7a, 0x4a, 255), 0.2f);
        var bv = new List<Vector3>(); var bu = new List<Vector2>(); var bt = new List<int>();
        // 晒网木架：两副 X 架 + 一根横杆，横杆上搭一张网
        Vector3 rack = new Vector3(-6.6f, y, -15.6f); Vector3 along = new Vector3(1f, 0f, 0.25f).normalized; Vector3 across = Vector3.Cross(Vector3.up, along);
        foreach (float s in new[] { -0.9f, 0.9f })
        {
            Vector3 c = rack + along * s;
            AddBeam(bv, bu, bt, c - across * 0.45f, c + across * 0.35f + Vector3.up * 1.15f, 0.04f);
            AddBeam(bv, bu, bt, c + across * 0.45f, c - across * 0.35f + Vector3.up * 1.15f, 0.04f);
        }
        AddBeam(bv, bu, bt, rack - along * 1.1f + Vector3.up * 1.02f, rack + along * 1.1f + Vector3.up * 1.02f, 0.035f);
        // 斜靠在架子上的两支船桨
        foreach (float s in new[] { -0.35f, 0.15f })
        {
            Vector3 b0 = rack + along * s + across * 0.75f, b1 = rack + along * (s + 0.12f) + across * 0.2f + Vector3.up * 1.05f;
            AddBeam(bv, bu, bt, b0, b1, 0.025f);
            AddOrientedBox(bv, bu, bt, b0 + (b1 - b0).normalized * 0.12f + Vector3.up * 0.02f, across, (b1 - b0).normalized, along, new Vector3(0.03f, 0.32f, 0.14f));
        }
        CoastalPart("exp_beach_rack", FinishMesh("exp_beach_rack", bv, bu, bt), holder, wood);
        // 网：从横杆垂下来的两片（中间低），双面
        var nv = new List<Vector3>(); var nu = new List<Vector2>(); var nt = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            float a0 = Mathf.Lerp(-0.95f, 0.95f, i / 8f), a1 = Mathf.Lerp(-0.95f, 0.95f, (i + 1) / 8f);
            float sag0 = 0.12f * (1f - a0 * a0), sag1 = 0.12f * (1f - a1 * a1);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 t0 = rack + along * a0 + Vector3.up * 1.0f, t1 = rack + along * a1 + Vector3.up * 1.0f;
                Vector3 l0 = rack + along * a0 + across * side * 0.32f + Vector3.up * (0.45f - sag0), l1 = rack + along * a1 + across * side * 0.32f + Vector3.up * (0.45f - sag1);
                AddTwoSidedQuad(nv, nu, nt, t0, t1, l1, l0, new Vector2(a0, 1), new Vector2(a1, 1), new Vector2(a1, 0), new Vector2(a0, 0));
            }
        }
        CoastalPart("exp_beach_net", FinishMesh("exp_beach_net", nv, nu, nt), holder, net);
        // 海星
        var starMesh = OctaStarMesh("BeachStarfishMesh", 0.16f, 0f);
        int k = 0;
        foreach (var p in new[] { new Vector3(-3.6f, y, -18.3f), new Vector3(-8.9f, y, -18.0f), new Vector3(-11.2f, y, -16.4f), new Vector3(-5.0f, y, -19.6f) })
        {
            var go = new GameObject("Starfish_" + k); go.transform.SetParent(holder, false);
            go.transform.SetPositionAndRotation(p + Vector3.up * 0.02f, Quaternion.Euler(0f, k * 37f, 0f));
            go.transform.localScale = new Vector3(1f, 1f, 1f) * (0.9f + 0.15f * (k % 3));
            go.AddComponent<MeshFilter>().sharedMesh = starMesh; go.AddComponent<MeshRenderer>().sharedMaterial = star;
            k++;
        }
        Debug.Log("EXP_BEACH rack+oars+net starfish=" + k);
    }

    // ---------------- EXP-03 邮局 / 面包店临海街沿 + 西侧山路临海侧：条石海堤 + 石柱木栏 ----------------
    // 沿主街石板路（3.02）外沿、外面是落差 1 m 以上的崖（不是另一块平地）的那几段：
    // 崖口砌条石挡墙（压到崖脚）、换成和海滨步道一样的石柱双横杆木栏；那几段原来的细木栅栏、路缘石裁掉。
    // 上山道外侧（临海、落差 1 m 以上）同样立石柱木栏。
    static void ExpWestSeawall(Layout layout, Transform parent, Transform root, List<WalkArea> walks, Material masonry, Material cap, Material timber)
    {
        var padArea = Array.Find(layout.walk, a => a.id == "pad_stone_3_0_0");
        if (padArea == null) return;
        var pad = padArea.x.Select((x, i) => new Vector2(x, padArea.z[i])).ToList();
        var chains = new List<List<Vector3>>(); var cur = new List<Vector3>();
        bool Seaward(Vector2 m, Vector2 outN)
        {
            if (m.x > 6.0f) return false;                       // 学校海滨步道那边已经有海堤
            // 往外 0.9 m 的地面（查询上限要高过石板面，不然看不见旁边 3.0 m 的草地，每条内沿都会被当成崖口）
            float g = GroundBelow(m.x + outN.x * 0.9f, m.y + outN.y * 0.9f, padArea.y + 0.3f);
            if (g > padArea.y - 1.0f) return false;
            foreach (var w in walks) if (w.y > 2.0f && w.y < 4.0f && w.poly != null && !ReferenceEquals(w.poly, pad) && DistToPoly(w.poly, m + outN * 0.5f) < 0.3f) return false;
            return !NearBuilding(new Vector3(m.x, padArea.y, m.y), 0.4f);
        }
        for (int i = 0; i < pad.Count; i++)
        {
            var a = pad[i]; var b = pad[(i + 1) % pad.Count];
            float len = (b - a).magnitude; if (len < 0.05f) { continue; }
            var dir = (b - a) / len; var outN = new Vector2(dir.y, -dir.x);
            if (InPolyXZ(pad, (a + b) * 0.5f + outN * 0.1f)) outN = -outN;
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.6f));
            for (int s = 0; s < steps; s++)
            {
                var p0 = Vector2.Lerp(a, b, s / (float)steps); var p1 = Vector2.Lerp(a, b, (s + 1) / (float)steps);
                if (Seaward((p0 + p1) * 0.5f, outN))
                {
                    if (cur.Count == 0) cur.Add(new Vector3(p0.x, padArea.y, p0.y));
                    cur.Add(new Vector3(p1.x, padArea.y, p1.y));
                }
                else if (cur.Count > 0) { if (cur.Count >= 3) chains.Add(cur); cur = new List<Vector3>(); }
            }
        }
        if (cur.Count >= 3) chains.Add(cur);
        float total = 0f;
        foreach (var c in chains) for (int i = 1; i < c.Count; i++) total += Vector3.Distance(c[i - 1], c[i]);
        // 裁掉这几段的细木栅栏和路缘石
        bool OnChain(Vector3 p) => chains.Any(c => CoastalDistance(p, c) < 0.35f) && Mathf.Abs(p.y - padArea.y) < 1.2f;
        foreach (var mf in parent.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if (mf.sharedMesh == null) continue;
            bool fence = HasPlacementAncestor(mf.transform, "Fences") && mf.name == "Fence_pad_stone_3_0_0";
            bool curb = mf.name == "PromenadeCurbs";
            if (!fence && !curb) continue;
            TrimCoastalMesh(mf, OnChain, "exp_trim_west_" + mf.name);
        }
        int idx = 0;
        foreach (var c in chains)
        {
            // 挡墙顶边退进石板 2 cm，底压到崖脚以下（-0.2）；护栏立在石板边上
            CoastalWall("exp_west_wall_" + idx, c, root, masonry, p => false);
            CoastalRail("exp_west_rail_" + idx, c, root, cap, timber, p => false);
            idx++;
        }
        int mrails = ExpMountainRailsAndCurbs(layout, parent, root, cap, timber);
        Debug.Log($"EXP_WESTWALL chains={chains.Count} length={total:F1}m mountainRails={mrails}");
    }

    // 2026-11-16 上山道护栏重做 + 两侧路缘石。
    // 原来两边都按"往外 0.8 m 的地面低 1 m 以上就立"判断，地面射线看不见岩块（岩块不在 Ground 层），
    // 结果靠岩壁那侧的护栏一根根插进岩块里（截图 8 左边那半截石柱），临空那侧反而在坡脚碎石处断档。现在按每一侧的实际几何分：
    //   · 先认哪边是临空侧：沿路每 1.5 m 往两边 2.5 m 外打地面，低于路面 1.2 m 以上次数多的那边；
    //   · 临空侧：只要往外 0.9 m 落差 ≥ 0.5 m 就立（不再要求 1 m），一直立到坡顶（到 west_up 平路口为止）；
    //   · 两侧都一样：立柱 / 横杆的位置（路面以上 0.1–1.0 m、0.2 m 半径内）只要碰到高过路面的岩块，就不立——
    //     那边本来就是岩壁，用不着护栏，也不会再有插在石头里的废柱。
    // 路缘石：最终路面（Road_mountain 的 RoundedDeck）左右边缘各一条 0.16 m 宽、高出路面 6 cm 的条石，压住路面边线。
    static int ExpMountainRailsAndCurbs(Layout layout, Transform parent, Transform root, Material cap, Material timber)
    {
        var mountain = Array.Find(layout.roads, r => r.id == "mountain");
        if (mountain == null) return 0;
        var line = SmoothCoastal(mountain.x.Select((x, i) => new Vector3(x, mountain.y[i], mountain.z[i])).ToArray(), smoothHeight: false);
        // 路面实际高度：直接打 Road_mountain 的碰撞面（layout 里的节点高度和成品路面差 2.6 cm，坡顶还有圆角）
        var roadGo = RequireObject("Road_mountain"); var roadCol = roadGo.GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        float RoadY(Vector3 p) { return roadCol != null && roadCol.Raycast(new Ray(new Vector3(p.x, p.y + 3f, p.z), Vector3.down), out var h, 6f) ? h.point.y : p.y; }
        // 高过路面的岩块（塌方岩壁、崖边圆石、山麓岩堆）——它们没有碰撞，用渲染包围盒（收 15%）近似
        var rocks = new List<Bounds>();
        foreach (var r in parent.GetComponentsInChildren<MeshRenderer>())
        {
            string n = r.name;
            if (!(n == "Boulder" || n == "FlankRock" || n == "ShoreRock" || n == "SlideRock" || n == "SeaRock" || n == "HarborShoreRock") || !r.gameObject.activeInHierarchy) continue;
            var b = r.bounds; b.extents *= 0.85f; rocks.Add(b);
        }
        bool RockAt(Vector3 p, float roadY)
        {
            foreach (var b in rocks)
            {
                if (b.max.y < roadY + 0.1f || b.min.y > roadY + 1.0f) continue;
                var c = b.ClosestPoint(new Vector3(p.x, Mathf.Clamp(roadY + 0.5f, b.min.y, b.max.y), p.z));
                if (new Vector2(c.x - p.x, c.z - p.z).magnitude < 0.2f) return true;
            }
            return false;
        }
        // 哪边临空
        int[] lowCount = new int[2];
        for (int i = 0; i < line.Count; i += 9)
            for (int s = 0; s < 2; s++)
            {
                var o = CoastalSide(line, i) * (s == 0 ? -1f : 1f);
                var q = line[i] + o * (mountain.width * 0.5f + 2.5f);
                if (GroundBelow(q.x, q.z, line[i].y + 0.5f) < line[i].y - 1.2f) lowCount[s]++;
            }
        int seaSign = lowCount[1] >= lowCount[0] ? 1 : -1;
        float crestZ = mountain.z[mountain.z.Length - 1] - 0.3f;
        int rails = 0;
        foreach (int sign in new[] { -1, 1 })
        {
            var edge = new List<Vector3>();
            for (int i = 0; i < line.Count; i++) { var p = line[i] + CoastalSide(line, i) * ((mountain.width * 0.5f + 0.02f) * sign); p.y = RoadY(p) + 0.02f; edge.Add(p); }
            bool sea = sign == seaSign;
            // 岩壁侧：往外 0.4–1.6 m 内有高出路面 0.6 m 以上的岩块，就是贴着山体走的那一边，不立护栏。
            // 前后各 6 个截面（约 ±1 m）一起看，免得岩块之间的空当里冒出一截截短护栏。
            var wall = new bool[edge.Count];
            for (int i = 0; i < edge.Count; i++)
            {
                var o = CoastalSide(line, i) * sign; float ry = edge[i].y - 0.02f;
                foreach (float dist in new[] { 0.4f, 0.8f, 1.2f, 1.6f })
                {
                    var q = edge[i] + o * dist;
                    foreach (var b in rocks)
                        if (b.max.y > ry + 0.6f && b.min.y < ry + 1.5f && q.x > b.min.x && q.x < b.max.x && q.z > b.min.z && q.z < b.max.z) { wall[i] = true; break; }
                    if (wall[i]) break;
                }
            }
            bool Skip(Vector3 p)
            {
                int j = 0; float d = float.MaxValue; for (int q = 0; q < edge.Count; q++) { float dd = (edge[q] - p).sqrMagnitude; if (dd < d) { d = dd; j = q; } }
                for (int k = Mathf.Max(0, j - 6); k <= Mathf.Min(edge.Count - 1, j + 6); k++) if (wall[k]) return true;
                var o = CoastalSide(line, j) * sign;
                float g = GroundBelow(p.x + o.x * 0.9f, p.z + o.z * 0.9f, p.y + 0.5f);
                if (RockAt(p, p.y - 0.02f)) return true;
                if (sea) return g > p.y - 0.5f || p.z > crestZ;
                return g > p.y - 1.0f || p.z > crestZ;
            }
            CoastalRail("exp_mountain_rail_" + (sign > 0 ? "r" : "l"), edge, root, cap, timber, Skip);
            rails++;
        }
        // 路缘石：取 Road_mountain 最终路面网格的两条边（DeckMesh 每个截面 2 个顶点：左、右）
        var mf = roadGo.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount % 2 == 0)
        {
            var vs = mf.sharedMesh.vertices; var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int side = 0; side < 2; side++)
                for (int i = 2 + side; i < vs.Length; i += 2)
                {
                    var a = roadGo.transform.TransformPoint(vs[i - 2]); var b = roadGo.transform.TransformPoint(vs[i]);
                    var other = roadGo.transform.TransformPoint(vs[i + (side == 0 ? 1 : -1)]);
                    var d = b - a; float l = new Vector2(d.x, d.z).magnitude; if (l < 0.01f) continue;
                    var inward = other - b; inward.y = 0f; inward.Normalize();
                    var mid = (a + b) * 0.5f + inward * 0.06f;
                    // 盒子顺着坡：forward 取带坡度的方向，高 0.10（压进路面 4 cm，露 6 cm）
                    var f = d.normalized; var up = Vector3.Cross(f, Vector3.Cross(Vector3.up, f)).normalized; if (up.y < 0f) up = -up;
                    AddOrientedBox(v, uv, t, mid + up * 0.01f, Vector3.Cross(up, f), up, f, new Vector3(0.16f, 0.10f, d.magnitude + 0.03f));
                }
            if (v.Count > 0) CoastalPart("exp_mountain_curbs", FinishMesh("exp_mountain_curbs", v, uv, t), root, cap);
        }
        Debug.Log($"EXP_MOUNTAIN seaSide={(seaSign > 0 ? "r" : "l")} rails={rails} rocks={rocks.Count}");
        return rails;
    }

    // ---------------- 外海边站在水里的树 ----------------
    // 海岸树（CoastTree）和台地上的树：树干脚下不是实地（低于 0.12 m，就是海面附近）或离海岸线不到 1.3 m 的，摘掉
    // 2026-11-16 下午（图 2）：灯塔坡道左侧和住宅台地东墙之间的三角夹角。用户定：两边的花全部清空；
    // 穿进石墙的岩块拿掉；学校草坪那圈旧木栅栏在这里的一截拆掉（坡道护栏和石墙已经围住了）。
    // 夹角里"悬空走路"是坡道碰撞两侧 0.93 m 的隐形路肩造成的，已在 RoadFinishes.ShoulderWidth 收掉。
    static void ExpLighthouseWedge(Transform parent)
    {
        var wedge = new List<Vector2> { new Vector2(23.0f, 5.4f), new Vector2(25.6f, 4.4f), new Vector2(27.6f, 8.2f), new Vector2(28.2f, 11.5f), new Vector2(26.4f, 13.0f), new Vector2(23.3f, 6.4f) };
        float Out(Vector2 p)
        {
            if (InPolyXZ(wedge, p)) return 0f;
            float best = float.MaxValue;
            for (int i = 0; i < wedge.Count; i++)
            {
                var a = wedge[i]; var b = wedge[(i + 1) % wedge.Count]; var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }
        // 住宅台地东墙那条边（墙脚线）
        Vector2 wa = new Vector2(23.5f, 6.2f), wb = new Vector2(26.2f, 12.7f);
        float ToWall(Vector2 p) { var ab = wb - wa; float t = Mathf.Clamp01(Vector2.Dot(p - wa, ab) / ab.sqrMagnitude); return (wa + ab * t - p).magnitude; }
        int bushes = 0, rocks = 0;
        foreach (var group in parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Bushes" || t.name == "Boulders").ToArray())
            foreach (Transform item in group)
            {
                if (!item.gameObject.activeSelf) continue;
                var rs = item.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var c = new Vector2(b.center.x, b.center.z);
                if (group.name == "Bushes" && Out(c) < 1.6f) { item.gameObject.SetActive(false); bushes++; }
                else if (group.name == "Boulders" && Out(c) < 0.6f && ToWall(c) < Mathf.Max(b.extents.x, b.extents.z) + 0.15f) { item.gameObject.SetActive(false); rocks++; }
            }
        int fences = 0;
        foreach (var mf in parent.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if (mf.sharedMesh == null || !HasPlacementAncestor(mf.transform, "Fences")) continue;
            var bb = mf.GetComponent<Renderer>() != null ? mf.GetComponent<Renderer>().bounds : new Bounds();
            if (bb.max.x < 22.5f || bb.min.z > 13.5f || bb.max.z < 4f) continue;
            string key = mf.name; for (var tr = mf.transform; tr != parent && tr != null; tr = tr.parent) key += "_" + tr.GetSiblingIndex();
            if (TrimCoastalMesh(mf, p => Out(new Vector2(p.x, p.z)) < 0.3f, "exp_wedge_" + key)) fences++;
        }
        Debug.Log($"EXP_WEDGE bushes={bushes} rocks={rocks} fences={fences}");
    }

    static void ExpTreesInWater(Layout layout, Transform parent)
    {
        if (layout.coast == null) return;
        var coast = layout.coast.x.Select((x, i) => new Vector2(x, layout.coast.z[i])).ToList();
        int removed = 0;
        foreach (var group in parent.GetComponentsInChildren<Transform>().Where(t => t.name == "Trees").ToArray())
            foreach (Transform item in group)
            {
                if (!item.gameObject.activeSelf) continue;
                var p = item.position; var q = new Vector2(p.x, p.z);
                float g = GroundBelow(p.x, p.z, p.y + 0.3f);
                bool wet = g < 0.12f || !InPolyXZ(coast, q) || (p.y < 1.0f && DistToEdge(coast, q) < 3.0f);
                // 树冠底下一圈（1 m）有一处是水（地面低于 0.12），远看就是站在浅水里
                if (!wet && p.y < 1.0f)
                    for (int a = 0; a < 8 && !wet; a++)
                    {
                        float ang = a * Mathf.PI / 4f;
                        if (GroundBelow(p.x + Mathf.Cos(ang), p.z + Mathf.Sin(ang), p.y + 0.3f) < 0.12f) wet = true;
                    }
                if (wet) { item.gameObject.SetActive(false); removed++; }
            }
        // 2026-11-16：灌木花丛也查。原来只看中心一个点，崖口花丛一半悬在空中、岸边花丛泡在海里都漏掉了。
        // 崖顶花丛（EdgeBush）、岸边花丛（ShoreBush）、崖面花丛（CliffTuft）：在中心 + 一圈 8 个点（半径取花丛外包盒半宽的 55%）往下找托底——
        // 0.4 m 内有实地（Ground 层、高于 0.12 m 不算海）或者有圆石 / 岩块顶着都算；中心没托住、或 9 个点里托住的不到 5 个，就摘掉。
        // 只做事后筛选（SetActive false），不改 Nature 里的摆放和随机数：岛上别的石头、花、树位置一点不动。
        var rockTops = new List<Bounds>();
        foreach (var r in parent.GetComponentsInChildren<MeshRenderer>())
        {
            string n = r.name;
            if ((n == "Boulder" || n == "FlankRock" || n == "ShoreRock" || n == "SlideRock" || n == "SeaRock" || n == "HarborShoreRock") && r.gameObject.activeInHierarchy)
            { var b = r.bounds; b.extents = new Vector3(b.extents.x * 0.6f, b.extents.y, b.extents.z * 0.6f); rockTops.Add(b); }
        }
        bool Held(float x, float z, float baseY)
        {
            float g = GroundBelow(x, z, baseY + 0.3f);
            if (g > 0.12f && g >= baseY - 0.4f) return true;
            foreach (var b in rockTops)
                if (x > b.min.x && x < b.max.x && z > b.min.z && z < b.max.z && b.max.y >= baseY - 0.4f && b.max.y <= baseY + 0.6f) return true;
            return false;
        }
        int edgeN = 0, shoreN = 0, tuftN = 0;
        int bushes = 0;
        foreach (var group in parent.GetComponentsInChildren<Transform>().Where(t => t.name == "Bushes").ToArray())
            foreach (Transform item in group)
            {
                if (!item.gameObject.activeSelf) continue;
                if (item.name != "EdgeBush" && item.name != "ShoreBush" && item.name != "CliffTuft") continue;
                var rs = item.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                var p = item.position; float rad = Mathf.Max(0.15f, Mathf.Min(bb.extents.x, bb.extents.z) * 0.55f);
                bool center = Held(p.x, p.z, p.y);
                int held = center ? 1 : 0;
                for (int a = 0; a < 8; a++) { float ang = a * Mathf.PI / 4f; if (Held(p.x + Mathf.Cos(ang) * rad, p.z + Mathf.Sin(ang) * rad, p.y)) held++; }
                if (item.name == "EdgeBush") edgeN++; else if (item.name == "ShoreBush") shoreN++; else tuftN++;
                if (!center || held < 6) { item.gameObject.SetActive(false); bushes++; }
            }
        Debug.Log($"EXP_TREES_IN_WATER removed={removed} floatingBushes={bushes} checked edge={edgeN} shore={shoreN} tuft={tuftN}");
    }

    // ---------------- 2026-11-16 收尾：草地和石板的接缝、所有路面上的草叶 ----------------
    // 1) 学校草坪（pad_grass_3_0_0）和主街石板（pad_stone_3_0_0）都是 3.020，重叠的东半边两张面共面，远看一圈锯齿闪烁：
    //    草坪顶面整体压低 4 mm，重叠处石板稳稳盖在上面（4 mm 的高差看不出来，碰撞不受影响）。
    // 2) 放在所有路面改造（海滨步道、灯塔坡道、上山道、扩建步道、校园小径、建筑门前路）之后，统一裁一遍草叶：
    //    把所有铺装面（Ground 层的石板 / 路面 / 步道 + 校园小径 + 建筑门前路）的朝上三角形栅格化到 0.1 m 的格子里，
    //    一整片草叶（5 个顶点）只要根部离铺装 0.25 m 以内（盖住往路上倒的叶长）或叶片任何一点落在铺装格上、且高度对得上，就整片拿掉。
    static void ExpFinalGroundCleanup(Layout layout, Transform parent)
    {
        // 1) 草坪压低
        var lawn = parent.Find("Tier_pad_grass_3_0_0");
        if (lawn != null)
        {
            var mf = lawn.GetComponent<MeshFilter>(); var m = UnityEngine.Object.Instantiate(mf.sharedMesh); var vs = m.vertices; int moved = 0;
            float top = Array.Find(layout.tiers, t => t.id == "pad_grass_3_0_0").y;
            for (int i = 0; i < vs.Length; i++) { var w = lawn.TransformPoint(vs[i]); if (Mathf.Abs(w.y - top) < 0.003f) { w.y = top - 0.004f; vs[i] = lawn.InverseTransformPoint(w); moved++; } }
            m.vertices = vs; m.RecalculateBounds();
            mf.sharedMesh = SaveMesh(m, CoastalAssets + "/lawn_lowered_Tier_pad_grass_3_0_0.asset");
            var col = lawn.GetComponent<MeshCollider>(); if (col != null) col.sharedMesh = mf.sharedMesh;
            Debug.Log("EXP_FINAL lawn lowered vertices=" + moved);
        }
        Physics.SyncTransforms();
        // 2) 铺装栅格
        var grassTiers = new HashSet<string>(layout.tiers.Where(t => t.zone == "grass" || t.zone == "sand" || t.zone == "rock").Select(t => "Tier_" + t.id)) { "Tier_coast" };
        var sources = new List<MeshFilter>();
        foreach (var mf in parent.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var r = mf.GetComponent<MeshRenderer>();
            bool pathMesh = mf.name == "exp_school_path" || HasPlacementAncestor(mf.transform, "BuildingEntrances");
            bool ground = mf.gameObject.layer == 6 && !grassTiers.Contains(mf.name) && !mf.name.StartsWith("Cliff_") && mf.GetComponentInParent<FloatingCrate>() == null
                          && !HasPlacementAncestor(mf.transform, "Sea") && !HasPlacementAncestor(mf.transform, "WaterSystem");
            if (pathMesh || ground) sources.Add(mf);
        }
        const float Cell = 0.1f;
        float x0 = -90f, z0 = -60f; int nx = 1800, nz = 1300;
        var gridY = new float[nx * nz]; for (int i = 0; i < gridY.Length; i++) gridY[i] = float.NaN;
        int Ix(float x) => Mathf.FloorToInt((x - x0) / Cell); int Iz(float z) => Mathf.FloorToInt((z - z0) / Cell);
        foreach (var mf in sources)
        {
            var vs = mf.sharedMesh.vertices; var tr = mf.transform;
            for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
            {
                var tri = mf.sharedMesh.GetTriangles(s);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    var a = tr.TransformPoint(vs[tri[i]]); var b = tr.TransformPoint(vs[tri[i + 1]]); var c = tr.TransformPoint(vs[tri[i + 2]]);
                    var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-10f || Mathf.Abs(n.normalized.y) < 0.6f) continue;
                    int ix0 = Mathf.Max(0, Ix(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), ix1 = Mathf.Min(nx - 1, Ix(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                    int iz0 = Mathf.Max(0, Iz(Mathf.Min(a.z, Mathf.Min(b.z, c.z)))), iz1 = Mathf.Min(nz - 1, Iz(Mathf.Max(a.z, Mathf.Max(b.z, c.z))));
                    var A = new Vector2(a.x, a.z); var B = new Vector2(b.x, b.z); var C = new Vector2(c.x, c.z);
                    float den = (B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y); if (Mathf.Abs(den) < 1e-9f) continue;
                    for (int ix = ix0; ix <= ix1; ix++)
                        for (int iz = iz0; iz <= iz1; iz++)
                        {
                            var P = new Vector2(x0 + (ix + 0.5f) * Cell, z0 + (iz + 0.5f) * Cell);
                            float l1 = ((B.y - C.y) * (P.x - C.x) + (C.x - B.x) * (P.y - C.y)) / den, l2 = ((C.y - A.y) * (P.x - C.x) + (A.x - C.x) * (P.y - C.y)) / den, l3 = 1f - l1 - l2;
                            if (l1 < -0.05f || l2 < -0.05f || l3 < -0.05f) continue;
                            float y = l1 * a.y + l2 * b.y + l3 * c.y; int k = ix * nz + iz;
                            if (float.IsNaN(gridY[k]) || y > gridY[k]) gridY[k] = y;
                        }
                }
            }
        }
        bool PavedNear(Vector3 p, int reach)
        {
            int cx = Ix(p.x), cz = Iz(p.z);
            for (int dx = -reach; dx <= reach; dx++)
                for (int dz = -reach; dz <= reach; dz++)
                {
                    if (dx * dx + dz * dz > reach * reach + 1) continue;
                    int ix = cx + dx, iz = cz + dz; if (ix < 0 || iz < 0 || ix >= nx || iz >= nz) continue;
                    float y = gridY[ix * nz + iz]; if (float.IsNaN(y)) continue;
                    if (p.y > y - 0.35f && p.y < y + 0.45f) return true;
                }
            return false;
        }
        int blades = 0, flowers = 0;
        var gf = parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GrassField");
        if (gf != null)
            foreach (var mf in gf.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var src = mf.sharedMesh; var vs = src.vertices; var tr = mf.transform; bool changed = false;
                var subs = new List<int[]>();
                bool blade = mf.name.StartsWith("Grass_") && src.subMeshCount == 1 && vs.Length % 5 == 0;
                var bladeCut = blade ? new sbyte[vs.Length / 5] : null;   // 0 没算过、1 留、2 裁
                for (int s = 0; s < src.subMeshCount; s++)
                {
                    var tri = src.GetTriangles(s); var kept = new List<int>();
                    for (int i = 0; i < tri.Length; i += 3)
                    {
                        bool cut;
                        if (blade)
                        {
                            int b0 = tri[i] / 5 * 5;
                            if (bladeCut[b0 / 5] == 0)
                            {
                                var rootP = tr.TransformPoint((vs[b0] + vs[b0 + 1]) * 0.5f);
                                bladeCut[b0 / 5] = (sbyte)(PavedNear(rootP, 3) || PavedNear(tr.TransformPoint(vs[b0 + 4]), 0) || PavedNear(tr.TransformPoint(vs[b0 + 2]), 0) ? 2 : 1);
                            }
                            cut = bladeCut[b0 / 5] == 2;
                        }
                        else cut = PavedNear(tr.TransformPoint((vs[tri[i]] + vs[tri[i + 1]] + vs[tri[i + 2]]) / 3f), 3);
                        if (cut) { changed = true; if (blade) blades++; else flowers++; }
                        else { kept.Add(tri[i]); kept.Add(tri[i + 1]); kept.Add(tri[i + 2]); }
                    }
                    subs.Add(kept.ToArray());
                }
                if (!changed) continue;
                var clone = UnityEngine.Object.Instantiate(src); for (int s = 0; s < subs.Count; s++) clone.SetTriangles(subs[s], s);
                clone.bounds = src.bounds;
                mf.sharedMesh = SaveMesh(clone, CoastalAssets + "/final_grass_" + mf.name + ".asset");
            }
        Debug.Log($"EXP_FINAL paved sources={sources.Count} grass tris removed={blades} meadow tris removed={flowers}");
    }

    // ---------------- EXP-02 住宅台地前沿：直墙前面砌"双层矮石坎 + 中间草坡 + 花带" ----------------
    // 中央广场的石板一直铺到 2.5 m 直墙脚下（墙里面紧挨着石板环路，也没法往里挖），所以在墙脚的广场边上砌两级花台：
    //   下级：离墙 R（0.7–1.2 m）处 0.47 m 条石坎 + 压顶石，坎后草坡往墙这边爬到 3.92 m；
    //   上级：离墙 0.42 m 处再一道 0.48 m 条石坎，坎顶到墙之间一条花带（4.4–4.5 m）。
    // 直墙露出来的只剩上面 1 m。花台只是外观，挡人靠轮廓碰撞（和别的石墙一样）；
    // 坡道中线 1.55 m 内（坡道本身和侧墙）、花台前面留不出 1.6 m 通道的地方不砌。
    static void ExpTerraceBank(Layout layout, Transform parent, Transform root, List<WalkArea> walks, Material masonry, Material cap)
    {
        var houses = Array.Find(layout.tiers, t => t.id == "houses");
        if (houses == null) return;
        var poly = houses.x.Select((x, i) => new Vector2(x, houses.z[i])).ToList();
        var grass = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/IslandGrass.mat");
        var wallMat = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/IslandWall.mat") ?? masonry;
        const float Base = 3.02f, Kerb1 = 3.49f, Slope2 = 3.92f, Kerb2 = 4.40f, Ledge = 4.50f, Step2 = 0.42f;
        var ramps = layout.roads.Where(r => r.id.StartsWith("branch_")).Select(r => r.x.Select((x, i) => new Vector3(x, r.y[i], r.z[i])).ToList()).ToList();
        // 广场上会挡路的东西（喷泉、摊位、路灯、房子）：花台前面要给它们之间留出 2 m
        var blockers = new List<Bounds>();
        foreach (var t in parent.GetComponentsInChildren<Transform>())
        {
            bool b = (t.parent != null && t.parent.name == "Streetscape" && !t.name.StartsWith("Fences")) || (t.name.StartsWith("B") && t.name.EndsWith("_visual"));
            if (!b) continue;
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
            var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); if (bb.max.y < 2.6f || bb.min.y > 4.2f) continue;
            blockers.Add(bb);
        }
        bool Plaza(Vector2 q) { float g = GroundBelow(q.x, q.y, 4.2f); return g > 2.9f && g < 3.12f; }
        bool Blocked(Vector2 q) { foreach (var b in blockers) { var c = b.ClosestPoint(new Vector3(q.x, b.center.y, q.y)); if ((new Vector2(c.x, c.z) - q).sqrMagnitude < 0.09f) return true; } return false; }
        var sv = new List<Vector3>(); var su = new List<Vector2>(); var st = new List<int>();
        var gv = new List<Vector3>(); var gu = new List<Vector2>(); var gt = new List<int>();
        var ev = new List<Vector3>(); var eu = new List<Vector2>(); var et = new List<int>();
        var bloomSpots = new List<(Vector3 pos, float s)>();
        int runs = 0, segs = 0;
        for (int e = 0; e < poly.Count; e++)
        {
            var a = poly[e]; var b = poly[(e + 1) % poly.Count];
            float len = (b - a).magnitude; if (len < 1.6f) continue;
            var dir = (b - a) / len; var n = new Vector2(dir.y, -dir.x);
            if (InPolyXZ(poly, (a + b) * 0.5f + n * 0.3f)) n = -n;     // n 朝广场
            var ps = new List<Vector2>(); var rs = new List<float>();
            for (float s = 0.3f; s <= len - 0.3f; s += 0.4f)
            {
                var p = a + dir * s; float R = -1f;
                bool nearRamp = ramps.Any(r => CoastalDistance(new Vector3(p.x, 0, p.y), r) < 1.55f);
                if (!nearRamp && Plaza(p + n * 0.3f))
                {
                    float free = 0f;
                    for (float k = 0.2f; k <= 4.0f; k += 0.2f) { var q = p + n * k; if (!Plaza(q) || Blocked(q) || !InPolyXZ(poly, q) && GroundBelow(q.x, q.y, 6f) > 3.2f) break; free = k; }
                    R = Mathf.Min(1.2f, free - 1.6f);
                    if (R < 0.7f) R = -1f;
                }
                ps.Add(p); rs.Add(R);
            }
            int i0 = 0;
            while (i0 < ps.Count)
            {
                if (rs[i0] < 0f) { i0++; continue; }
                int i1 = i0; while (i1 + 1 < ps.Count && rs[i1 + 1] > 0f) i1++;
                if (i1 - i0 >= 2)
                {
                    var R = new float[i1 - i0 + 1];
                    for (int k = 0; k < R.Length; k++) { float m = rs[i0 + k]; for (int d = -2; d <= 2; d++) { int q = i0 + k + d; if (q >= i0 && q <= i1) m = Mathf.Min(m, rs[q] + Mathf.Abs(d) * 0.12f); } R[k] = m; }
                    Vector3 S(int k, float o, float y) { var p = ps[i0 + k] + n * o; return new Vector3(p.x, y, p.y); }
                    var d3 = new Vector3(dir.x, 0f, dir.y); var n3 = new Vector3(n.x, 0f, n.y);
                    float run = 0f;
                    for (int k = 0; k + 1 < R.Length; k++)
                    {
                        int ka = k, kb = k + 1;
                        float seg = (ps[i0 + kb] - ps[i0 + ka]).magnitude, next = run + seg;
                        Vector2 U(float r, float y) => new Vector2(r * .5f, y * .5f);
                        // 下级条石坎（朝广场的竖面）
                        AddTwoSidedQuad(sv, su, st, S(ka, R[ka], Base - 0.02f), S(kb, R[kb], Base - 0.02f), S(kb, R[kb], Kerb1), S(ka, R[ka], Kerb1), U(run, Base), U(next, Base), U(next, Kerb1), U(run, Kerb1));
                        // 下级草坡：坎后 0.12 m 起爬到上级坎脚
                        AddTwoSidedQuad(gv, gu, gt, S(ka, R[ka] - 0.12f, Kerb1), S(kb, R[kb] - 0.12f, Kerb1), S(kb, Step2 + 0.07f, Slope2), S(ka, Step2 + 0.07f, Slope2),
                            new Vector2(S(ka, R[ka], 0).x, S(ka, R[ka], 0).z), new Vector2(S(kb, R[kb], 0).x, S(kb, R[kb], 0).z), new Vector2(S(kb, Step2, 0).x, S(kb, Step2, 0).z), new Vector2(S(ka, Step2, 0).x, S(ka, Step2, 0).z));
                        // 上级条石坎（竖面 + 坎脚一条 7 cm 的石边）
                        AddTwoSidedQuad(sv, su, st, S(ka, Step2 + 0.07f, Slope2), S(kb, Step2 + 0.07f, Slope2), S(kb, Step2, Slope2), S(ka, Step2, Slope2), U(run, 0), U(next, 0), U(next, .04f), U(run, .04f));
                        AddTwoSidedQuad(sv, su, st, S(ka, Step2, Slope2 - 0.02f), S(kb, Step2, Slope2 - 0.02f), S(kb, Step2, Kerb2), S(ka, Step2, Kerb2), U(run, Slope2), U(next, Slope2), U(next, Kerb2), U(run, Kerb2));
                        // 上级花带：坎顶到墙
                        AddTwoSidedQuad(gv, gu, gt, S(ka, Step2 - 0.08f, Kerb2), S(kb, Step2 - 0.08f, Kerb2), S(kb, 0.02f, Ledge), S(ka, 0.02f, Ledge),
                            new Vector2(S(ka, Step2, 0).x, S(ka, Step2, 0).z), new Vector2(S(kb, Step2, 0).x, S(kb, Step2, 0).z), new Vector2(S(kb, 0f, 0).x, S(kb, 0f, 0).z), new Vector2(S(ka, 0f, 0).x, S(ka, 0f, 0).z));
                        // 两道压顶石
                        AddOrientedBox(sv, su, st, (S(ka, R[ka] - 0.06f, Kerb1) + S(kb, R[kb] - 0.06f, Kerb1)) * 0.5f + Vector3.up * 0.04f, n3, Vector3.up, d3, new Vector3(0.24f, 0.09f, seg + 0.03f));
                        AddOrientedBox(sv, su, st, (S(ka, Step2 - 0.04f, Kerb2) + S(kb, Step2 - 0.04f, Kerb2)) * 0.5f + Vector3.up * 0.04f, n3, Vector3.up, d3, new Vector3(0.22f, 0.09f, seg + 0.03f));
                        if (k % 2 == 0)
                        {
                            float o = (R[ka] + Step2) * 0.5f;
                            bloomSpots.Add((S(ka, o, Mathf.Lerp(Kerb1, Slope2, Mathf.InverseLerp(R[ka] - 0.12f, Step2 + 0.07f, o)) - 0.05f), 0.72f + (k * 37 % 10) * 0.03f));
                        }
                        if (k % 3 == 1) bloomSpots.Add((S(ka, 0.26f, Kerb2 + 0.02f), 0.55f + (k * 53 % 10) * 0.02f));
                        run = next; segs++;
                    }
                    // 两头竖面封口（台地墙的石材）
                    foreach (int k in new[] { 0, R.Length - 1 })
                    {
                        var prof = new[] { S(k, R[k], Base - 0.02f), S(k, R[k], Kerb1), S(k, R[k] - 0.12f, Kerb1), S(k, Step2 + 0.07f, Slope2), S(k, Step2, Slope2), S(k, Step2, Kerb2), S(k, Step2 - 0.08f, Kerb2), S(k, 0.02f, Ledge) };
                        var bot = S(k, 0.02f, Base - 0.02f);
                        for (int q = 0; q + 1 < prof.Length; q++)
                            AddTwoSidedQuad(ev, eu, et, bot, prof[q], prof[q + 1], prof[q + 1], new Vector2(0, 0), new Vector2(prof[q].y * .5f, 0), new Vector2(prof[q + 1].y * .5f, .5f), new Vector2(prof[q + 1].y * .5f, .5f));
                    }
                    runs++;
                }
                i0 = i1 + 1;
            }
        }
        if (sv.Count == 0) { Debug.Log("EXP_TERRACE none"); return; }
        CoastalPart("exp_terrace_kerb", FinishMesh("exp_terrace_kerb", sv, su, st), root, masonry);
        CoastalPart("exp_terrace_ends", FinishMesh("exp_terrace_ends", ev, eu, et), root, wallMat);
        CoastalPart("exp_terrace_bank", FinishMesh("exp_terrace_bank", gv, gu, gt), root, grass);
        var bushParent = parent.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Bushes") ?? root;
        var leafShade = new Color32(0x1f, 0x4c, 0x3a, 255);
        var leafA = FoliageMat("FloraLeafA", new Color32(0x5a, 0x9a, 0x34, 255), leafShade, false, 0.07f, 0.012f);
        var leafC = FoliageMat("FloraLeafC", new Color32(0x3f, 0x86, 0x3e, 255), leafShade, false, 0.07f, 0.012f);
        var blossomMats = new[] {
            FoliageMat("FloraBlossomPink", new Color32(0xf0, 0x86, 0xb4, 255), new Color32(0xa8, 0x56, 0x8c, 255), true, 0.05f, 0.01f),
            FoliageMat("FloraBlossomLilac", new Color32(0xb4, 0x98, 0xec, 255), new Color32(0x74, 0x62, 0xb4, 255), true, 0.05f, 0.01f),
            FoliageMat("FloraFlowerWhite", new Color32(0xfb, 0xf6, 0xea, 255), new Color32(0xb8, 0xb4, 0xd0, 255), true, 0.05f, 0.01f),
            FoliageMat("FloraBlossomRose", new Color32(0xe0, 0x6a, 0x98, 255), new Color32(0x98, 0x48, 0x7c, 255), true, 0.05f, 0.01f) };
        var bloomMeshes = new Mesh[4]; for (int i = 0; i < 4; i++) bloomMeshes[i] = BushMesh(i, true);
        int k2 = 0;
        foreach (var (pos, s) in bloomSpots)
        {
            Place(bushParent, "TerraceBloom", bloomMeshes[k2 % 4], new[] { k2 % 2 == 0 ? leafC : leafA, blossomMats[k2 % 4] }, pos, new Vector3(s, s * 0.8f, s), (k2 * 73) % 360);
            k2++;
        }
        Debug.Log($"EXP_TERRACE planters runs={runs} segments={segs} blooms={k2}");
    }

    // ---------------- EXP-02 补充：住宅台地的墙改成条石挡土墙 + 压顶石 ----------------
    // 喷泉两边、三条坡道之间那几面墙太窄（花台会堵坡道口），而且整圈墙原来是一张泥色崖壁贴图。
    // 换成和海堤同一套的条石砌体（贴图按台地墙的 UV 缩放一半，砖块大小和海堤一致），顶沿一圈压顶石。
    static void ExpHouseWallFinish(Layout layout, Transform parent, Transform root, List<WalkArea> walks, Material masonry, Material cap)
    {
        var houses = Array.Find(layout.tiers, t => t.id == "houses");
        var wall = parent.Find("Cliff_houses");
        if (houses == null || wall == null) return;
        string path = MatRoot + "/HouseWallMasonry.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(masonry); AssetDatabase.CreateAsset(mat, path); }
        mat.CopyPropertiesFromMaterial(masonry);
        mat.SetTextureScale("_BaseMap", new Vector2(0.5f, 1f));   // 台地墙 UV 是（米, 米），海堤是（米/2, 米）：横向缩一半，砖一样大
        EditorUtility.SetDirty(mat);
        wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        var poly = houses.x.Select((x, i) => new Vector2(x, houses.z[i])).ToList();
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int caps = 0;
        for (int e = 0; e < poly.Count; e++)
        {
            var a = poly[e]; var b = poly[(e + 1) % poly.Count];
            float len = (b - a).magnitude; if (len < 0.3f) continue;
            var dir = (b - a) / len; var n = new Vector2(dir.y, -dir.x);
            if (InPolyXZ(poly, (a + b) * 0.5f + n * 0.2f)) n = -n;
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 1.2f));
            for (int s = 0; s < steps; s++)
            {
                var p0 = a + dir * (len * s / steps); var p1 = a + dir * (len * (s + 1) / steps); var m = (p0 + p1) * 0.5f;
                // 外面低于台地 1 m 以上（真是一面墙）、顶上不是石板路 / 后环路，才压顶
                float outside = GroundBelow(m.x + n.x * 0.5f, m.y + n.y * 0.5f, houses.y - 0.3f);
                if (outside > houses.y - 1.0f) continue;
                bool onWalk = false;
                foreach (var w in walks) { if (w.y < houses.y - 0.3f) continue; if (InPolyXZ(w.poly, m - n * 0.15f) || DistToPoly(w.poly, m - n * 0.15f) < 0.2f) { onWalk = true; break; } }
                if (onWalk) continue;
                var c = new Vector3(m.x - n.x * 0.06f, houses.y + 0.045f, m.y - n.y * 0.06f);
                AddOrientedBox(v, uv, t, c, new Vector3(n.x, 0, n.y), Vector3.up, new Vector3(dir.x, 0, dir.y), new Vector3(0.30f, 0.10f, (p1 - p0).magnitude + 0.04f));
                caps++;
            }
        }
        if (v.Count > 0) CoastalPart("exp_house_wall_cap", FinishMesh("exp_house_wall_cap", v, uv, t), root, cap);
        Debug.Log("EXP_HOUSEWALL masonry + caps=" + caps);
    }

    // ---------------- 西山顶俯瞰机位的视线走廊 ----------------
    static void ExpViewCorridor(Transform parent)
    {
        var camGo = new GameObject("exp_tmp_view");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f; cam.aspect = 2f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 16f;
        camGo.transform.SetPositionAndRotation(new Vector3(-38f, 11f, 6f), Quaternion.Euler(18.4f, 53f, 0f));
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        UnityEngine.Object.DestroyImmediate(camGo);
        int cleared = 0;
        foreach (var group in parent.GetComponentsInChildren<Transform>().Where(t => t.name == "Trees").ToArray())
            foreach (Transform item in group)
            {
                if (!item.gameObject.activeSelf) continue;
                var rs = item.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.SqrDistance(new Vector3(-38f, 11f, 6f)) < 16f * 16f && GeometryUtility.TestPlanesAABB(planes, b)) { item.gameObject.SetActive(false); cleared++; }
            }
        Debug.Log("EXP_VIEW corridor trees cleared=" + cleared);
    }

    // ---------------- EXP-05 西山塌方 ----------------
    static void ExpLandslide(Layout layout, Transform parent, Transform root, List<WalkArea> walks)
    {
        var slide = Array.Find(layout.tiers, t => t.id == "landslide");
        if (slide == null) return;
        var poly = slide.x.Select((x, i) => new Vector2(x, slide.z[i])).ToList();
        var holder = new GameObject("Landslide").transform; holder.SetParent(root, false);
        var rockA = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/NatureRockA.mat");
        var rockB = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/NatureRockB.mat");
        var rocks = new Mesh[6]; for (int i = 0; i < 6; i++) rocks[i] = CliffRockMesh(i, 2);
        var rnd = new Lcg(20261117);
        int nRocks = 0;
        void Rock(Vector3 c, Vector3 scale)
        {
            Place(holder, "SlideRock", rocks[nRocks % 6], new[] { rnd.Next01() < 0.5f ? rockA : rockB }, c, scale, rnd.Next01() * 360f);
            nRocks++;
        }
        // 1) 层状岩壁：沿塌方台四周（靠山的那几边不用），从脚下往上一层层叠扁平岩块，一直堆到台顶上方 1 m
        for (int e = 0; e < poly.Count; e++)
        {
            var a = poly[e]; var b = poly[(e + 1) % poly.Count];
            float len = (b - a).magnitude; if (len < 0.5f) continue;
            var dir = (b - a) / len; var n = new Vector2(dir.y, -dir.x);
            if (InPolyXZ(poly, (a + b) * 0.5f + n * 0.3f)) n = -n;
            int steps = Mathf.Max(1, Mathf.RoundToInt(len / 1.5f));
            for (int s = 0; s < steps; s++)
            {
                var p = a + dir * (len * (s + 0.5f) / steps);
                float g = GroundBelow(p.x + n.x * 1.2f, p.y + n.y * 1.2f, slide.y - 0.3f);
                if (g > slide.y - 0.9f) continue;
                for (float yy = Mathf.Max(g, -0.4f); yy < slide.y + 1.0f;)
                {
                    float r = 0.65f + rnd.Next01() * 0.55f;
                    var c2 = p + n * (r * 0.35f + 0.05f) + dir * ((rnd.Next01() - 0.5f) * 0.6f);
                    var c = new Vector3(c2.x, yy + r * 0.3f, c2.y);
                    if (!BlocksWalk(walks, c, r * 1.1f, c.y - r * 0.4f, c.y + r * 0.5f, 0.12f))
                        Rock(c, new Vector3(r * 1.35f, r * 0.55f, r * 1.1f));
                    yy += r * 0.85f;
                }
            }
        }
        // 2) 台顶乱石（远看是一座高出来的碎石山头）
        var bmin = new Vector2(poly.Min(q => q.x), poly.Min(q => q.y)); var bmax = new Vector2(poly.Max(q => q.x), poly.Max(q => q.y));
        for (int i = 0; i < 14; i++)
        {
            var q = new Vector2(Mathf.Lerp(bmin.x, bmax.x, rnd.Next01()), Mathf.Lerp(bmin.y, bmax.y, rnd.Next01()));
            if (!InPolyXZ(poly, q) || DistToPoly(poly, q) < 0.6f) continue;
            float r = 0.7f + rnd.Next01() * 0.8f;
            Rock(new Vector3(q.x, slide.y + r * 0.35f + rnd.Next01() * 0.4f, q.y), new Vector3(r, r * 0.8f, r * 0.9f));
        }
        // 3) 两堆滚石：南面一堆（上山道拐弯处外侧），东北面一堆
        var piles = new List<Vector3>();
        foreach (var center in new[] { new Vector2(-32.4f, 16.5f), new Vector2(-29.7f, 22.9f) })
        {
            float g = GroundBelow(center.x, center.y, slide.y - 0.3f);
            int placed = 0;
            for (int i = 0; i < 11; i++)
            {
                float ang = rnd.Next01() * Mathf.PI * 2f, rad = (i == 0 ? 0f : 0.35f + rnd.Next01() * 1.1f);
                float r = i == 0 ? 0.9f : 0.35f + rnd.Next01() * 0.5f;
                var c = new Vector3(center.x + Mathf.Cos(ang) * rad, g + r * 0.55f + (i == 0 ? 0.2f : 0f), center.y + Mathf.Sin(ang) * rad);
                if (BlocksWalk(walks, c, r, c.y - r * 0.6f, c.y + r * 0.7f, 0.15f)) continue;
                Rock(c, new Vector3(r, r * 0.85f, r * 0.95f)); placed++;
            }
            if (placed > 0) piles.Add(new Vector3(center.x, g, center.y));
        }
        // 4) X 型圆木支撑架：贴在离上山道最近的那面岩壁上，脚落在路基高度
        var mountain = Array.Find(layout.roads, r => r.id == "mountain");
        var roadLine = mountain == null ? new List<Vector3>() : mountain.x.Select((x, i) => new Vector3(x, mountain.y[i], mountain.z[i])).ToList();
        int bestEdge = 0; float bestD = float.MaxValue;
        for (int e = 0; e < poly.Count && roadLine.Count > 1; e++)
        {
            var m = (poly[e] + poly[(e + 1) % poly.Count]) * 0.5f;
            float d = CoastalDistance(new Vector3(m.x, 0, m.y), roadLine); if (d < bestD) { bestD = d; bestEdge = e; }
        }
        var wood = FlatMat("ShoringTimber", new Color32(0x7a, 0x54, 0x30, 255), 0.06f);
        var bv = new List<Vector3>(); var bu = new List<Vector2>(); var bt = new List<int>();
        {
            var a = poly[bestEdge]; var b = poly[(bestEdge + 1) % poly.Count];
            var dir2 = (b - a).normalized; var n2 = new Vector2(dir2.y, -dir2.x);
            if (InPolyXZ(poly, (a + b) * 0.5f + n2 * 0.3f)) n2 = -n2;
            foreach (float f in new[] { 0.3f, 0.7f })
            {
                var c2 = Vector2.Lerp(a, b, f) + n2 * 0.02f;
                float baseY = GroundBelow(c2.x + n2.x * 0.7f, c2.y + n2.y * 0.7f, slide.y - 0.3f);
                if (baseY < 0f) baseY = 0.15f;
                var d3 = new Vector3(dir2.x, 0, dir2.y); var c3 = new Vector3(c2.x, baseY, c2.y);
                Vector3 P(float along, float up) => c3 + d3 * along + Vector3.up * up;
                AddBeam(bv, bu, bt, P(-0.8f, 0f), P(-0.8f, 2.1f), 0.09f);
                AddBeam(bv, bu, bt, P(0.8f, 0f), P(0.8f, 2.1f), 0.09f);
                AddBeam(bv, bu, bt, P(-0.8f, 0.1f), P(0.8f, 2.0f), 0.075f);
                AddBeam(bv, bu, bt, P(0.8f, 0.1f), P(-0.8f, 2.0f), 0.075f);
                AddBeam(bv, bu, bt, P(-0.95f, 2.05f), P(0.95f, 2.05f), 0.08f);
            }
        }
        // 5) 警戒绳：南面那堆滚石靠路一侧围半圈矮桩 + 红绳 + 小白布条
        var rope = FlatMat("WarningRopeRed", new Color32(0xc8, 0x32, 0x2a, 255), 0.08f);
        var cloth = FlatMat("WarningCloth", new Color32(0xf4, 0xf0, 0xe6, 255), 0.05f);
        var rv = new List<Vector3>(); var ru = new List<Vector2>(); var rt = new List<int>();
        var fv = new List<Vector3>(); var fu = new List<Vector2>(); var ft = new List<int>();
        if (piles.Count > 0)
        {
            var c = piles[0]; Vector3? last = null;
            for (int i = 0; i <= 5; i++)
            {
                float ang = Mathf.Lerp(-0.2f, 1.9f, i / 5f);   // 朝东、东北（上山道那边）
                var q = new Vector3(c.x + Mathf.Cos(ang) * 1.75f, 0f, c.z + Mathf.Sin(ang) * 1.75f);
                float g = GroundBelow(q.x, q.z, c.y + 1f); q.y = g;
                if (BlocksWalk(walks, q, 0.08f, g - 0.2f, g + 0.9f, 0.1f)) { last = null; continue; }
                AddCylinderPiling(bv, bu, bt, q, 0.05f, 0.85f, 6);
                var att = q + Vector3.up * 0.72f;
                if (last.HasValue)
                {
                    AddRopeSpan(rv, ru, rt, last.Value, att, 0.018f, 0.07f);
                    var mid = (last.Value + att) * 0.5f - Vector3.up * 0.06f; var d = (att - last.Value).normalized;
                    AddOrientedBox(fv, fu, ft, mid - Vector3.up * 0.09f, Vector3.Cross(d, Vector3.up).normalized, Vector3.up, d, new Vector3(0.01f, 0.16f, 0.12f));
                }
                last = att;
            }
        }
        if (bv.Count > 0) CoastalPart("exp_landslide_timber", FinishMesh("exp_landslide_timber", bv, bu, bt), holder, wood);
        if (rv.Count > 0) CoastalPart("exp_landslide_rope", FinishMesh("exp_landslide_rope", rv, ru, rt), holder, rope);
        if (fv.Count > 0) CoastalPart("exp_landslide_cloth", FinishMesh("exp_landslide_cloth", fv, fu, ft), holder, cloth);
        Debug.Log($"EXP_LANDSLIDE rocks={nRocks} piles={piles.Count} shoringEdge={bestEdge} roadGap={bestD:F2}");
    }

    // 两点之间一根圆木（六棱柱，两端封口）
    static void AddBeam(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, float radius, int sides = 6)
    {
        var axis = (b - a).normalized;
        var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        var up = Vector3.Cross(side, axis).normalized;
        int start = v.Count;
        for (int i = 0; i < sides; i++)
        {
            float ang = i * Mathf.PI * 2f / sides;
            var off = (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius;
            v.Add(a + off); uv.Add(new Vector2(i / (float)sides, 0f));
            v.Add(b + off); uv.Add(new Vector2(i / (float)sides, 1f));
        }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            FinishQuad(t, start + i * 2, start + i * 2 + 1, start + j * 2 + 1, start + j * 2);
        }
        int ca = v.Count; v.Add(a); uv.Add(Vector2.zero); int cb = v.Count; v.Add(b); uv.Add(Vector2.one);
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            t.Add(ca); t.Add(start + j * 2); t.Add(start + i * 2);
            t.Add(cb); t.Add(start + i * 2 + 1); t.Add(start + j * 2 + 1);
        }
    }

    // ---------------- EXP-06 蒸汽小渡轮 + 码头货物 ----------------
    static void ExpFerryAndCargo(Transform root)
    {
        var navy = FlatMat("FerryHullNavy", new Color32(0x2c, 0x4a, 0x7a, 255), 0.25f);
        var red = FlatMat("FerryRed", new Color32(0xc8, 0x3a, 0x30, 255), 0.25f);
        var white = FlatMat("FerryWhite", new Color32(0xf6, 0xf1, 0xe6, 255), 0.2f);
        var deckWood = FlatMat("FerryDeckWood", new Color32(0xb0, 0x80, 0x52, 255), 0.1f);
        var window = FlatMat("FerryWindow", new Color32(0x3a, 0x5c, 0x78, 255), 0.6f);
        var black = FlatMat("FerryFunnelTop", new Color32(0x2a, 0x26, 0x24, 255), 0.2f);
        var yellow = FlatMat("FerryFlagYellow", new Color32(0xf2, 0xc8, 0x3a, 255), 0.1f);
        var orange = FlatMat("FerryBuoyOrange", new Color32(0xf0, 0x6a, 0x2c, 255), 0.2f);
        var ferry = new GameObject("SteamFerry");
        ferry.transform.SetParent(root, false);
        ferry.transform.position = new Vector3(24.2f, 0f, -21.1f);
        ferry.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        var body = new GameObject("body").transform; body.SetParent(ferry.transform, false);
        void Part(PrimitiveType type, Vector3 lp, Vector3 s, Material m, Vector3? euler = null)
        {
            var p = GameObject.CreatePrimitive(type); UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(body, false); p.transform.localPosition = lp; p.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero); p.transform.localScale = s;
            p.GetComponent<Renderer>().sharedMaterial = m;
        }
        // 船体：深蓝船底 + 红腰线 + 白舷墙，船头是转了 45° 的方块（尖头），船尾半圆
        Part(PrimitiveType.Cube, new Vector3(0, 0.02f, 0), new Vector3(1.9f, 0.56f, 4.4f), navy);
        Part(PrimitiveType.Cube, new Vector3(0, 0.02f, 2.2f), new Vector3(1.34f, 0.56f, 1.34f), navy, new Vector3(0, 45f, 0));
        Part(PrimitiveType.Cylinder, new Vector3(0, 0.02f, -2.2f), new Vector3(1.9f, 0.28f, 1.9f), navy);
        Part(PrimitiveType.Cube, new Vector3(0, 0.36f, 0), new Vector3(1.94f, 0.13f, 4.42f), red);
        Part(PrimitiveType.Cube, new Vector3(0, 0.36f, 2.2f), new Vector3(1.37f, 0.13f, 1.37f), red, new Vector3(0, 45f, 0));
        Part(PrimitiveType.Cylinder, new Vector3(0, 0.36f, -2.2f), new Vector3(1.94f, 0.065f, 1.94f), red);
        Part(PrimitiveType.Cube, new Vector3(0, 0.58f, 0), new Vector3(1.88f, 0.32f, 4.36f), white);
        Part(PrimitiveType.Cube, new Vector3(0, 0.58f, 2.2f), new Vector3(1.32f, 0.32f, 1.32f), white, new Vector3(0, 45f, 0));
        Part(PrimitiveType.Cylinder, new Vector3(0, 0.58f, -2.2f), new Vector3(1.88f, 0.16f, 1.88f), white);
        Part(PrimitiveType.Cube, new Vector3(0, 0.75f, -0.1f), new Vector3(1.7f, 0.04f, 4.5f), deckWood);
        // 舱室：白墙、蓝窗、红顶
        Part(PrimitiveType.Cube, new Vector3(0, 1.25f, -0.45f), new Vector3(1.36f, 0.96f, 2.0f), white);
        foreach (float z in new[] { -1.05f, -0.45f, 0.15f })
            foreach (float x in new[] { -0.69f, 0.69f })
                Part(PrimitiveType.Cube, new Vector3(x, 1.38f, z), new Vector3(0.02f, 0.32f, 0.38f), window);
        Part(PrimitiveType.Cube, new Vector3(0, 1.38f, 0.56f), new Vector3(0.9f, 0.32f, 0.02f), window);
        Part(PrimitiveType.Cube, new Vector3(0, 1.79f, -0.45f), new Vector3(1.6f, 0.12f, 2.3f), red);
        Part(PrimitiveType.Cube, new Vector3(0, 1.73f, -0.45f), new Vector3(1.66f, 0.04f, 2.36f), white);
        // 烟囱：红身白箍黑顶
        Part(PrimitiveType.Cylinder, new Vector3(0, 2.25f, -0.75f), new Vector3(0.42f, 0.42f, 0.42f), red);
        Part(PrimitiveType.Cylinder, new Vector3(0, 2.34f, -0.75f), new Vector3(0.44f, 0.06f, 0.44f), white);
        Part(PrimitiveType.Cylinder, new Vector3(0, 2.7f, -0.75f), new Vector3(0.45f, 0.05f, 0.45f), black);
        // 桅杆 + 小黄旗
        Part(PrimitiveType.Cylinder, new Vector3(0, 2.45f, 0.35f), new Vector3(0.05f, 0.7f, 0.05f), white);
        Part(PrimitiveType.Cube, new Vector3(0, 2.95f, 0.58f), new Vector3(0.02f, 0.26f, 0.44f), yellow);
        // 救生圈：橙色外圈 + 白心（舱室两侧）
        foreach (float x in new[] { -0.7f, 0.7f })
        {
            Part(PrimitiveType.Cylinder, new Vector3(x * 1.01f, 1.2f, 0.25f), new Vector3(0.36f, 0.025f, 0.36f), orange, new Vector3(0, 0, 90f));
            Part(PrimitiveType.Cylinder, new Vector3(x * 1.03f, 1.2f, 0.25f), new Vector3(0.18f, 0.027f, 0.18f), white, new Vector3(0, 0, 90f));
        }
        // 船头船尾白栏杆
        foreach (float z in new[] { 1.4f, -1.8f })
            foreach (float x in new[] { -0.88f, 0.88f })
                Part(PrimitiveType.Cube, new Vector3(x, 0.98f, z), new Vector3(0.04f, 0.04f, 1.0f), white);
        var fl = ferry.AddComponent<TideFloat>(); fl.offset = 0f; fl.bob = 0.035f; fl.roll = 1.0f;

        // 低码头东头：木箱、麻袋、一卷缆绳
        var crateMat = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/IslandCrate.mat") ?? FlatMat("CargoCrate", new Color32(0x8a, 0x66, 0x42, 255), 0.08f);
        var sack = FlatMat("CargoSack", new Color32(0xd8, 0xc4, 0x98, 255), 0.05f);
        var ropeMat = FlatMat("HarborRope", new Color(.73f, .61f, .40f), .1f);
        var cargo = new GameObject("PierCargo").transform; cargo.SetParent(root, false);
        void Box(PrimitiveType type, Vector3 pos, Vector3 s, Material m, float yaw)
        {
            var p = GameObject.CreatePrimitive(type); UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(cargo, false); p.transform.position = pos; p.transform.rotation = Quaternion.Euler(0, yaw, 0); p.transform.localScale = s;
            p.GetComponent<Renderer>().sharedMaterial = m;
        }
        float dy = GroundBelow(25.9f, -18.4f, 1.3f);
        Box(PrimitiveType.Cube, new Vector3(25.95f, dy + 0.23f, -18.55f), Vector3.one * 0.46f, crateMat, 8f);
        Box(PrimitiveType.Cube, new Vector3(25.95f, dy + 0.23f, -18.02f), Vector3.one * 0.44f, crateMat, -6f);
        Box(PrimitiveType.Cube, new Vector3(25.97f, dy + 0.68f, -18.3f), Vector3.one * 0.40f, crateMat, 20f);
        Box(PrimitiveType.Sphere, new Vector3(25.45f, dy + 0.15f, -18.62f), new Vector3(0.46f, 0.30f, 0.36f), sack, 30f);
        Box(PrimitiveType.Sphere, new Vector3(25.42f, dy + 0.15f, -18.18f), new Vector3(0.44f, 0.30f, 0.38f), sack, -20f);
        Box(PrimitiveType.Cylinder, new Vector3(25.5f, dy + 0.05f, -17.68f), new Vector3(0.42f, 0.05f, 0.42f), ropeMat, 0f);
        Debug.Log("EXP_FERRY ferry + cargo dockY=" + dy.ToString("F2"));
    }

    // ---------------- 2026-11-16 灯塔台的两个路口（灯塔坡道、东侧崖顶步道）收口 ----------------
    // 1) 东步道起点在灯塔台里面、和台地（六角石 5.000）共面闪烁：台地范围里的步道顶点压低 4 mm，让台地石面盖住。
    // 2) 台上原来那圈细木栅栏（Fence_lighthouse）和坡道、步道两边的石柱木栏是两套东西，接口处一截敞口、材质断档：
    //    整圈换成同款石柱木栏，沿台地外沿、外面是 1 m 以上落差的地方立，让开坡道和步道的路口（端柱和路两边的护栏端柱对上）。
    // 3) 两个路口在台地边线上各压一条过门石（压顶石材质，高出台面 2 cm），盖住长砖和六角石的斜接缝。
    static void ExpLighthouseJoin(Layout layout, Transform parent, Transform root, List<Vector3> east, float eastW, List<Vector2> lightPoly, float lightY, Material cap, Material timber)
    {
        var deck = root.Find("exp_east_walk");
        if (deck != null)
        {
            var mf = deck.GetComponent<MeshFilter>(); var m = UnityEngine.Object.Instantiate(mf.sharedMesh); var vs = m.vertices; int low = 0;
            for (int i = 0; i < vs.Length; i++)
            {
                var w = deck.TransformPoint(vs[i]);
                if (w.y > lightY - 0.02f && InPolyXZ(lightPoly, new Vector2(w.x, w.z))) { w.y = lightY - 0.004f; vs[i] = deck.InverseTransformPoint(w); low++; }
            }
            m.vertices = vs; m.RecalculateNormals(); m.RecalculateBounds();
            mf.sharedMesh = SaveMesh(m, CoastalAssets + "/exp_east_walk.asset");
            var col = deck.GetComponent<MeshCollider>(); if (col != null) col.sharedMesh = mf.sharedMesh;
            Debug.Log("EXP_LH east walk vertices tucked under lighthouse tier=" + low);
        }
        var oldFence = parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Fence_lighthouse");
        if (oldFence != null) oldFence.gameObject.SetActive(false);
        var ramp = ExistingSlopeDeck(FindRoad(layout, "to_lighthouse")).Select(s => s.Center).ToList();
        var rampRoad = FindRoad(layout, "to_lighthouse");
        Physics.SyncTransforms();
        // 台地外沿每 0.25 m 取一点，判断要不要立护栏
        var standPts = (layout.points ?? new Pt[0]).Where(pt => DistToPoly(lightPoly, new Vector2(pt.x, pt.z)) < 0.3f && DistToEdge(lightPoly, new Vector2(pt.x, pt.z)) < 1.0f)
                                                       .Select(pt => new Vector2(pt.x, pt.z)).ToList();
        var pts = new List<Vector3>(); var flags = new List<bool>();
        for (int e = 0; e < lightPoly.Count; e++)
        {
            var a = lightPoly[e]; var b = lightPoly[(e + 1) % lightPoly.Count];
            float len = (b - a).magnitude; if (len < 0.05f) continue;
            var dir = (b - a) / len; var outN = new Vector2(dir.y, -dir.x);
            if (InPolyXZ(lightPoly, (a + b) * 0.5f + outN * 0.1f)) outN = -outN;
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.1f));
            for (int s = 0; s < steps; s++)
            {
                var q = Vector2.Lerp(a, b, s / (float)steps) - outN * 0.14f;   // 石栏底座整条压在台面上
                // 灯塔塔身旁边那条窄道（剧情点 30 → 31 走这里）：护栏往外挪到边线外 0.12 m（立柱内侧面正好在边线上），
                // 不比原来的细木栏更占路，不然路线绕不过去
                if (NearBuilding(new Vector3(q.x, lightY, q.y), 1.5f)) q += outN * 0.26f;
                var p = new Vector3(q.x, lightY, q.y);
                bool drop = GroundBelow(q.x + outN.x * 1.0f, q.y + outN.y * 1.0f, lightY - 0.3f) < lightY - 1.0f;
                // 路口让到和路两边护栏同一条线（坡道护栏在路边外 2 cm，步道护栏就在路边），两根端柱几乎重合成一个转角石墩，不再并排两三根
                bool opening = CoastalDistance(p, ramp) < rampRoad.width * 0.5f + 0.02f || CoastalDistance(p, east) < eastW * 0.5f;
                // 台上的剧情站位（31 号就站在台地边线上看海）前后 1.4 m 不立：原来的细木栏在这里也是断开的
                bool stand = standPts.Any(sp => (sp - new Vector2(p.x, p.z)).magnitude < 1.4f);
                pts.Add(p); flags.Add(drop && !opening && !stand && !NearBuilding(p, -0.05f));
            }
        }
        int start = flags.IndexOf(false);
        int rails = 0;
        if (start < 0) { start = 0; }
        var chain = new List<Vector3>();
        for (int k = 0; k <= pts.Count; k++)
        {
            int i = (start + k) % pts.Count;
            bool on = k < pts.Count && flags[i];
            if (on) chain.Add(pts[i]);
            else if (chain.Count > 0)
            {
                if (chain.Count >= 2) { CoastalRail("lighthouse_platform_rail_" + rails, chain, root, cap, timber, p => false); rails++; }
                chain = new List<Vector3>();
            }
        }
        // 过门石：沿台地边线、落在路面范围里的那几段
        int stones = 0;
        foreach (var (line, half, name) in new[] { (ramp, rampRoad.width * 0.5f, "ramp"), (east, eastW * 0.5f, "east") })
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int e = 0; e < lightPoly.Count; e++)
            {
                var a = lightPoly[e]; var b = lightPoly[(e + 1) % lightPoly.Count];
                float len = (b - a).magnitude; if (len < 0.05f) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.12f));
                for (int s = 0; s < steps; s++)
                {
                    var q0 = Vector2.Lerp(a, b, s / (float)steps); var q1 = Vector2.Lerp(a, b, (s + 1) / (float)steps);
                    var mid = new Vector3((q0.x + q1.x) * 0.5f, lightY, (q0.y + q1.y) * 0.5f);
                    if (CoastalDistance(mid, line) > half + 0.06f) continue;
                    var d = new Vector3(q1.x - q0.x, 0f, q1.y - q0.y); float l = d.magnitude; d /= l;
                    // 高出台面 4 cm（压进 2 cm）：东步道出台地后是缓缓上坡的，只高 2 cm 的话会和步道面互相穿插出锯齿
                    AddOrientedBox(v, uv, t, mid + Vector3.up * 0.01f, Vector3.Cross(d, Vector3.up), Vector3.up, d, new Vector3(0.32f, 0.06f, l + 0.015f));
                    stones++;
                }
            }
            if (v.Count > 0) CoastalPart("lighthouse_threshold_" + name, FinishMesh("lighthouse_threshold_" + name, v, uv, t), root, cap);
        }
        Debug.Log($"EXP_LH platform rails={rails} thresholdPieces={stones} oldFenceHidden={(oldFence != null)}");
    }

    // ---------------- EXP-01 灯塔 → 东院崖顶步道 + 北侧后环路 ----------------
    static void ExpRingWalks(Layout layout, Transform parent, Transform root, IslandWorld world, List<Area> areas, Material paving, Material masonry, Material cap, Material timber)
    {
        var east = SmoothCoastal(new[] {
            new Vector3(32.2f, 5.0f, 12.7f), new Vector3(30.4f, 5.05f, 13.4f), new Vector3(28.5f, 5.25f, 14.8f),
            new Vector3(26.9f, 5.48f, 16.9f), new Vector3(25.1f, 5.53f, 18.6f), new Vector3(23.6f, 5.53f, 18.65f), new Vector3(22.3f, 5.53f, 18.0f) });
        var back = SmoothCoastal(new[] {
            new Vector3(-14.7f, 5.53f, 20.2f), new Vector3(-14.55f, 5.53f, 22.4f), new Vector3(-14.4f, 5.53f, 25.2f), new Vector3(-12.8f, 5.53f, 28.6f),
            new Vector3(-10.6f, 5.53f, 31.0f), new Vector3(-7.5f, 5.53f, 31.6f), new Vector3(-3.5f, 5.53f, 31.9f), new Vector3(0.5f, 5.53f, 31.6f),
            new Vector3(4.5f, 5.53f, 30.95f), new Vector3(8.5f, 5.53f, 30.0f), new Vector3(12.5f, 5.53f, 28.8f), new Vector3(16.0f, 5.53f, 27.6f),
            new Vector3(19.0f, 5.53f, 26.4f), new Vector3(19.05f, 5.53f, 24.6f), new Vector3(19.0f, 5.53f, 22.4f), new Vector3(18.8f, 5.53f, 20.3f) });
        const float EastW = 2.2f, BackW = 1.5f;
        // 先把挡路的旧栅栏、树、花丛清掉（和海滨步道同一套做法）
        int fenceTrim = 0;
        foreach (var mf in parent.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if (mf.sharedMesh == null) continue;
            bool fence = HasPlacementAncestor(mf.transform, "Fences"), grassField = HasPlacementAncestor(mf.transform, "GrassField");
            if (!fence && !grassField) continue;
            string key = mf.name; for (var t = mf.transform; t != parent && t != null; t = t.parent) key += "_" + t.GetSiblingIndex();
            float pad = fence ? 0.3f : 0.12f;
            // 栅栏在步道两边让开 0.3 m；草叶只清步道面上的（不然草从石板里长出来）
            if (TrimCoastalMesh(mf, p => p.y > 4.5f && (CoastalDistance(p, east) < EastW * 0.5f + pad || CoastalDistance(p, back) < BackW * 0.5f + pad), "exp_trim_" + key)) fenceTrim++;
        }
        int plants = 0;
        foreach (var group in parent.GetComponentsInChildren<Transform>().Where(t => t.name == "Trees" || t.name == "Bushes").ToArray())
            foreach (Transform item in group)
            {
                if (!item.gameObject.activeSelf) continue;
                var p = item.position;
                bool hit = CoastalDistance(p, east) < EastW * 0.5f + (item.parent.name == "Trees" ? 1.0f : 0.5f) && p.y > 3.5f
                        || CoastalDistance(p, back) < BackW * 0.5f + (item.parent.name == "Trees" ? 1.0f : 0.45f) && p.y > 4.5f;
                if (hit) { item.gameObject.SetActive(false); plants++; }
            }
        foreach (var lamp in parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Lamp").ToArray())
            if (CoastalDistance(lamp.position, east) < EastW * 0.5f + 0.2f || CoastalDistance(lamp.position, back) < BackW * 0.5f + 0.2f) lamp.gameObject.SetActive(false);

        CoastalDeck("exp_east_walk", east, EastW, root, paving, areas, world);
        CoastalDeck("exp_back_ring", back, BackW, root, paving, areas, world);
        // 灯塔步道架在灯塔台和住宅台地之间的那一段：两边石墙封到海床，不留透光缝
        var houses = Array.Find(layout.tiers, t => t.id == "houses");
        var housePoly = houses.x.Select((x, i) => new Vector2(x, houses.z[i])).ToList();
        var light = Array.Find(layout.tiers, t => t.id == "lighthouse");
        var lightPoly = light.x.Select((x, i) => new Vector2(x, light.z[i])).ToList();
        bool OverTier(Vector3 p) => GroundBelow(p.x, p.z, p.y - 0.25f) > p.y - 0.75f;
        foreach (int sign in new[] { -1, 1 })
        {
            var edge = new List<Vector3>();
            for (int i = 0; i < east.Count; i++) edge.Add(east[i] + CoastalSide(east, i) * (EastW * 0.5f * sign));
            CoastalWall("exp_wall_east_" + sign, edge, root, masonry, OverTier);
            // 护栏：灯塔台上（入口）、东院石板环上、和后环路交汇处不立
            // 2026-11-16：原来离灯塔台 0.4 m 就停，留一道敞口；现在立到台地边线，末端端柱和台上新换的石柱木栏接上
            CoastalRail("exp_rail_east_" + sign, edge, root, cap, timber,
                p => InPolyXZ(lightPoly, new Vector2(p.x, p.z)) || p.x < 23.4f);
        }
        ExpLighthouseJoin(layout, parent, root, east, EastW, lightPoly, light.y, cap, timber);
        // 后环路：只在靠近台地北沿（外侧是崖）的地方立护栏
        {
            var outer = new List<Vector3>();
            for (int i = 0; i < back.Count; i++) outer.Add(back[i] - CoastalSide(back, i) * (BackW * 0.5f));
            var inner = new List<Vector3>();
            for (int i = 0; i < back.Count; i++) inner.Add(back[i] + CoastalSide(back, i) * (BackW * 0.5f));
            // 2026-11-16：原来按"哪边离住宅台地边更近"挑外侧，可两边都在台地里面（DistToPoly 都是 0），
            // 结果护栏立在了房子那一侧的草坪边上，真正临崖的那一侧只有一道矮压顶石。改成直接量落差：往外 1 m 的地面低于路面 1 m 以上的那一侧才是崖口。
            // 查询上限要高过路面（GroundBelow 只看上限以下），不然旁边同高的草坪也被当成崖口
            bool Drop(Vector3 p, Vector3 o) => GroundBelow(p.x + o.x * 1.0f, p.z + o.z * 1.0f, p.y + 0.3f) < p.y - 1.0f;
            int dropOuter = 0, dropInner = 0;
            for (int i = 0; i < back.Count; i += 8)
            {
                var s = CoastalSide(back, i);
                if (Drop(outer[i], -s)) dropOuter++;
                if (Drop(inner[i], s)) dropInner++;
            }
            bool outerIsRim = dropOuter >= dropInner;
            var rim = outerIsRim ? outer : inner;
            CoastalRail("exp_rail_back", rim, root, cap, timber, p =>
            {
                int j = 0; float d = float.MaxValue; for (int q = 0; q < rim.Count; q++) { float dd = (rim[q] - p).sqrMagnitude; if (dd < d) { d = dd; j = q; } }
                var o = CoastalSide(back, j) * (outerIsRim ? -1f : 1f);
                return p.z < 26f || !Drop(p, o);
            });
            Debug.Log($"EXP_BACK rim={(outerIsRim ? "outer" : "inner")} drops outer={dropOuter} inner={dropInner}");
            // 路灯：外侧护栏线上每 9 m 一盏
            float run = 0f; int lamps = 0;
            for (int i = 1; i < rim.Count; i++)
            {
                run += Vector3.Distance(rim[i - 1], rim[i]);
                if (run < 9f || rim[i].z < 26f) continue;
                run = 0f;
                var lamp = new GameObject("Lamp"); lamp.transform.SetParent(root, false);
                var side = (rim[i] - back[i]); side.y = 0; side = side.normalized;
                // 2026-11-16：原来在 rim + side*0.25，悬在护栏外侧的崖口上。改到护栏内侧路缘：灯杆中心离护栏线往里 0.42 m
                // （任务给的 0.28 m 会让 0.25 m 半径的灯座压进 0.28 m 宽的石栏底座），灯笼朝路面挑。
                lamp.transform.position = rim[i] - side * 0.28f;
                var model = MountSourceModel(lamp.transform, "S4", Vector3.zero, 2.2f, true, Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg);
                AddLampGlow(lamp.transform, model);
                if (!PlaceLampInsideRail(lamp.transform, rim[i], -side, 0.42f)) { UnityEngine.Object.DestroyImmediate(lamp); continue; }
                lamps++;
            }
            // 灯塔步道外侧（东北面）也每 8 m 一盏
            float runE = 0f;
            for (int i = 1; i < east.Count; i++)
            {
                runE += Vector3.Distance(east[i - 1], east[i]);
                if (runE < 8f) continue; runE = 0f;
                var side = CoastalSide(east, i) * -1f;
                var lamp = new GameObject("Lamp"); lamp.transform.SetParent(root, false);
                lamp.transform.position = east[i] + side * (EastW * 0.5f - 0.25f);
                var model = MountSourceModel(lamp.transform, "S4", Vector3.zero, 2.2f, true, Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg);
                AddLampGlow(lamp.transform, model);
                // 2026-11-16：灯座原来压在护栏石栏底座上；收进护栏内侧 0.42 m，灯笼朝路面挑
                if (!PlaceLampInsideRail(lamp.transform, east[i] + side * (EastW * 0.5f), -side, 0.42f)) { UnityEngine.Object.DestroyImmediate(lamp); continue; }
                lamps++;
            }
            Debug.Log($"EXP_WALKS east={east.Count} back={back.Count} fencesTrimmed={fenceTrim} plantsCleared={plants} lamps={lamps}");
        }
        // 和房子的距离自查：路边 0.15 m 以内碰到房子的外包盒就记一笔（只是日志，方便调路线）
        foreach (var t in parent.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("B") && t.name.EndsWith("_visual")))
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            foreach (var (line, w, name) in new[] { (east, EastW, "east"), (back, BackW, "back") })
                foreach (var p in line)
                {
                    var q = b.ClosestPoint(new Vector3(p.x, b.center.y, p.z)); float d = Vector2.Distance(new Vector2(q.x, q.z), new Vector2(p.x, p.z));
                    if (d < w * 0.5f + 0.15f) { Debug.LogWarning($"EXP_WALK_CLASH {name} {t.name} at ({p.x:F1},{p.z:F1}) gap={d - w * 0.5f:F2}"); break; }
                }
        }
    }
}
