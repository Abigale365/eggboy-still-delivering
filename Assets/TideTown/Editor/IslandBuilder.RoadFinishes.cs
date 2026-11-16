using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class IslandBuilder
{
    const string FinishedMeshRoot = ArtRoot + "/Meshes/RoadFinishes";
    // 2026-11-16 下午（图 2 悬空走路）：坡道碰撞原来两边各多出 0.93 m 的"路肩"，和路面同高但看不见——
    // 灯塔坡道左侧护栏外面那条，人从草坪踩上去就顺着它悬空走到乱石堆上面。路肩收到 4 cm，碰撞和看见的路面一样宽。
    const float ShoulderWidth = 0.04f;

    struct RoadSection
    {
        public Vector3 left, right;
        public RoadSection(Vector3 left, Vector3 right) { this.left = left; this.right = right; }
        public Vector3 Center => (left + right) * 0.5f;
    }

    [MenuItem("潮汐镇/场景/更新坡道路面和路基")]
    public static void UpdateRoadFinishes()
    {
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var cobble = RequireObject("Tier_pad_stone_3_0_0").GetComponent<MeshRenderer>().sharedMaterial;
        var cliff = RequireObject("Cliff_lighthouse").GetComponent<MeshRenderer>().sharedMaterial;
        ApplyRoadFinishes(layout, cobble, cliff);
        Physics.SyncTransforms();
        var safe = UnityEngine.Object.FindObjectOfType<SafePoints>();
        if (safe != null)
        {
            for (int i = 0; i < safe.points.Count; i++)
            {
                var point = safe.points[i];
                float y = GroundY(point.x, point.z);
                if (y <= 0.2f) throw new InvalidOperationException($"Safe point lost its ground at {point}");
                safe.points[i] = AtHeight(point, y);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the finished roads");
        AssetDatabase.SaveAssets();
        Debug.Log("ROAD_FINISHES updated A, B, C, D (three approaches), and E");
    }

    static void ApplyRoadFinishes(Layout layout, Material cobble, Material cliff)
    {
        foreach (string id in new[] { "to_lighthouse", "to_shed" })
        {
            var road = RequireObject("Road_" + id);
            var definition = FindRoad(layout, id);
            var sections = ExistingSlopeDeck(definition);
            SetFinishedDeck(road, sections, cobble);
            SetRoadbed(road, sections, cliff);
            if (id == "to_shed")
                BuildStairStepsAndStringers(road, "Finished Road to_shed_stairs", sections, cobble, 1.48f, 1.42f);
        }

        var mountain = FindRoad(layout, "mountain");
        var west = FindRoad(layout, "west_up");
        var ascent = RequireObject("Road_mountain");
        var descent = RequireObject("Road_west_up");
        var points = RoadNodes(mountain);
        int crest = points.Count - 1;
        points.AddRange(RoadNodes(west).GetRange(1, west.x.Length - 1));
        points[0] = AtHeight(points[0], 3.013f);
        points[points.Count - 1] = AtHeight(points[points.Count - 1], 5.512f);
        var route = RoundedDeck(points, mountain.width, west.width, crest, 0.26f, 0.28f);
        SetFinishedDeck(ascent, route, cobble);
        SetRoadbed(ascent, route, cliff);
        SetGround(ascent, route);
        // The continuous surface includes the old western descent and its collider.
        descent.SetActive(false);

        var houseWall = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/IslandWall.mat") ?? cliff;
        foreach (string id in new[] { "branch_w", "branch_n", "branch_e" })
        {
            var road = FindRoad(layout, id);
            var go = RequireObject("Road_" + id);
            var nodes = HousingApproach(road);
            var deck = RoundedDeck(nodes, road.width, road.width, -1, 0.15f, 0.16f);
            SetFinishedDeck(go, deck, cobble, true);
            SetGround(go, deck, id == "branch_n" ? 0.40f : 0f);
            // 2026-11-15：坡道两边砌实心石墙，一直封到广场地面以下（2.8 m），坡道底下不再是空的、侧面不再是一张纸
            BuildRampSideWalls(go, deck, 2.8f, houseWall, 0f, 0f);
        }

        var dock = RequireObject("Road_to_dock");
        var link = DockApproach(layout, out var dockGrid);
        var dockSurface = DockGridMesh(dockGrid);
        SaveDeck(dock, dockSurface, cobble);
        SetRoadbed(dock, link, cliff);
        dock.GetComponent<MeshCollider>().sharedMesh = dock.GetComponent<MeshFilter>().sharedMesh;
        BuildDockStairTreads(dock, link, cobble);

        BuildPromenadeCurbs(layout, cobble);
        BuildWoodenPier(layout);
    }

    static GameObject RequireObject(string name)
    {
        // Find the inactive western descent when regenerating an already finished scene.
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name != "Generated") continue;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        }
        throw new InvalidOperationException("Missing generated object " + name);
    }

    static RoadDef FindRoad(Layout layout, string id) =>
        Array.Find(layout.roads, road => road.id == id) ?? throw new InvalidOperationException("Missing road " + id);

    static Vector3 AtHeight(Vector3 point, float y) => new Vector3(point.x, y, point.z);

    static List<Vector3> RoadNodes(RoadDef road)
    {
        var nodes = new List<Vector3>();
        for (int i = 0; i < road.x.Length; i++)
            nodes.Add(new Vector3(road.x[i], road.y[i] + 0.026f, road.z[i]));
        return nodes;
    }

    static List<RoadSection> ExistingSlopeDeck(RoadDef road)
    {
        if (road.id == "to_lighthouse") return LighthouseRampDeck(road);
        var sections = new List<RoadSection>();
        foreach (var source in SlopeSections(road))
        {
            var p = source.center;
            // Preserve the profile used by the approved lighthouse sample and the
            // existing flooded section of the shed road.
            if (p.y >= 2.95f && p.y <= 3.05f) p.y = 3.010f;
            var lateral = source.side * (road.width * 0.5f);
            sections.Add(new RoadSection(p - lateral, p + lateral));
        }
        return sections;
    }

    // 2026-11-16 灯塔坡道两头的错层：
    //   · SlopeSections 给整条路统一抬 0.026 m，起点又平着往外伸 0.15 m，坡脚成了 3.366，比海滨步道（3.340）高出一道 2.6 cm 的坎；
    //     两条路的端线还差 13°，右边留出一条斜三角黑缝。
    //   · 坡顶原来写死 4.992（离灯塔台 5.000 差 8 mm），而且 4.95 → 4.96 处一下跳 3 cm，坡顶又多一级错台。
    // 现在：去掉 0.026 的统一抬高，坡脚正好 = 起点高度（3.340），起点往回再伸 0.45 m、压低 4 mm 藏到步道底下（盖住斜三角缝）；
    // 坡顶平台 = 灯塔台高度，落在灯塔台多边形里面的点压低 4 mm 让台地石面盖住（不共面闪烁），交界线由 LighthouseThreshold 的过门石压住。
    static List<RoadSection> LighthouseRampDeck(RoadDef road)
    {
        var src = SlopeSections(road);
        float foot = road.y[0], top = road.y[road.y.Length - 1];
        List<Vector2> tier = null;
        if (currentLayout != null)
        {
            var lt = Array.Find(currentLayout.tiers, a => a.id == "lighthouse");
            if (lt != null) { tier = new List<Vector2>(); for (int i = 0; i < lt.x.Length; i++) tier.Add(new Vector2(lt.x[i], lt.z[i])); }
        }
        Vector3 Fix(Vector3 p)
        {
            p.y = Mathf.Clamp(p.y, foot, top);
            if (tier != null && p.y > top - 0.02f && InPolyXZ(tier, new Vector2(p.x, p.z))) p.y = top - 0.004f;
            return p;
        }
        var sections = new List<RoadSection>();
        var dir = SlopeDirection(src[0].center, src[Mathf.Min(3, src.Count - 1)].center);
        // 起点往回伸：0.45 m，比步道面低 4 mm（步道宽 3.35，坡道 2.6，整段藏在步道底下）
        for (int k = 3; k >= 1; k--)
        {
            var c = src[0].center - dir * (0.15f * k); c.y = foot - 0.004f;
            var lateral = src[0].side * (road.width * 0.5f);
            sections.Add(new RoadSection(c - lateral, c + lateral));
        }
        for (int i = 0; i < src.Count; i++)
        {
            var c = src[i].center; c.y -= 0.026f;
            var lateral = src[i].side * (road.width * 0.5f);
            if (i == 0) { c.y = foot - 0.004f; sections.Add(new RoadSection(c - lateral, c + lateral)); continue; }   // SlopeSections 原有的 0.15 m 外伸段也藏在步道底下
            sections.Add(new RoadSection(Fix(c - lateral), Fix(c + lateral)));
        }
        return sections;
    }

    static List<Vector3> HousingApproach(RoadDef road)
    {
        var nodes = RoadNodes(road);
        var entrance = nodes[0];
        var exit = nodes[nodes.Count - 1];
        var approach = SlopeDirection(entrance, nodes[1]);
        var departure = SlopeDirection(nodes[nodes.Count - 2], exit);
        // Meet the finished platform across the full road width. The final section
        // is level before it tucks under the existing stone surface.
        if (road.id == "branch_n")
        {
            // 2026-11-15：坡脚原来伸到入口外 1.46 m（z≈10.33），正顶着喷泉北池沿（z≈10.30）。
            // 回缩 1.2 m（喷泉同时南移 0.4 m），坡脚只比台地边多出 0.3 m，不再在台地外露出一截三角侧墙；
            // 坡上段的拐点往上挪一点，坡度约 36°（按阶梯铺装）。
            return new List<Vector3>
            {
                AtHeight(entrance - approach * 0.26f, 3.019f),
                AtHeight(entrance + approach * 0.05f, 3.12f),
                AtHeight(exit - departure * 0.50f, 5.40f),
                AtHeight(exit - departure * 0.12f, 5.517f),
                AtHeight(exit + departure * 0.55f, 5.512f)
            };
        }
        float apron = road.id == "branch_e" ? 0.97f : 0.63f;
        nodes[0] = AtHeight(entrance - approach * 0.38f, 3.013f);
        nodes[nodes.Count - 1] = AtHeight(exit - departure * 0.55f, 5.48f);
        nodes.Add(AtHeight(exit - departure * 0.12f, 5.517f));
        nodes.Add(AtHeight(exit + departure * apron, 5.514f));
        return nodes;
    }

    static List<RoadSection> RoundedDeck(List<Vector3> nodes, float firstWidth, float lastWidth,
        int crestIndex, float startExtension, float endExtension)
    {
        var center = new List<Vector3>();
        var widths = new List<float>();
        center.Add(nodes[0] - SlopeDirection(nodes[0], nodes[1]) * startExtension);
        widths.Add(firstWidth);
        for (int i = 0; i < nodes.Count; i++)
        {
            var current = nodes[i];
            var previous = i == 0 ? center[0] : nodes[i - 1];
            var next = i == nodes.Count - 1 ? current + SlopeDirection(previous, current) * endExtension : nodes[i + 1];
            float before = SlopeDistance(previous, current);
            float after = SlopeDistance(current, next);
            bool crest = i == crestIndex;
            float turn = Vector3.Angle(SlopeDirection(previous, current), SlopeDirection(current, next));
            // A wider bend keeps the outer edge of the roadbed from folding at the inner corner.
            float blendDistance = crest ? 1.18f : Mathf.Lerp(0.43f, 1.02f, Mathf.InverseLerp(15f, 35f, turn));
            float radius = Mathf.Min(blendDistance, Mathf.Min(before, after) * (crest ? 0.69f : 0.46f));
            var enter = Vector3.Lerp(current, previous, radius / before);
            var leave = Vector3.Lerp(current, next, radius / after);
            float enterWidth = crestIndex < 0 || i <= crestIndex ? firstWidth : lastWidth;
            AddStraight(center, widths, enter, enterWidth);
            int steps = Mathf.Max(4, Mathf.CeilToInt(2f * radius / 0.085f));
            for (int j = 1; j <= steps; j++)
            {
                float t = (float)j / steps;
                center.Add(Vector3.Lerp(Vector3.Lerp(enter, current, t), Vector3.Lerp(current, leave, t), t));
                widths.Add(crest ? Mathf.Lerp(firstWidth, lastWidth, Mathf.SmoothStep(0f, 1f, t)) : enterWidth);
            }
        }
        var end = nodes[nodes.Count - 1];
        AddStraight(center, widths, end + SlopeDirection(nodes[nodes.Count - 2], end) * endExtension, lastWidth);
        var sections = new List<RoadSection>();
        for (int i = 0; i < center.Count; i++)
        {
            var forward = SlopeDirection(center[Mathf.Max(i - 1, 0)], center[Mathf.Min(i + 1, center.Count - 1)]);
            var across = Vector3.Cross(forward, Vector3.up) * (widths[i] * 0.5f);
            sections.Add(new RoadSection(center[i] - across, center[i] + across));
        }
        return sections;
    }

    static void AddStraight(List<Vector3> center, List<float> widths, Vector3 end, float width)
    {
        var start = center[center.Count - 1];
        float startWidth = widths[widths.Count - 1];
        int steps = Mathf.Max(1, Mathf.CeilToInt(SlopeDistance(start, end) / 0.11f));
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            center.Add(Vector3.Lerp(start, end, t));
            widths.Add(Mathf.Lerp(startWidth, width, t));
        }
    }

    static List<RoadSection> DockApproach(Layout layout, out Vector3[,] dockGrid)
    {
        var road = FindRoad(layout, "to_dock");
        var shed = Array.Find(layout.tiers, area => area.id == "pier")
                   ?? Array.Find(layout.tiers, area => area.id == "shed");
        var dock = Array.Find(layout.tiers, area => area.id == "dock");
        var nodes = RoadNodes(road);
        var start = nodes[0];
        var end = nodes[1];
        var forward = SlopeDirection(start, end);
        var side = Vector3.Cross(forward, Vector3.up);
        const int Columns = 8;
        var offsets = new SortedSet<float>();
        const float halfWidth = 0.80f;
        for (int i = 0; i <= Columns; i++)
            offsets.Add(Mathf.Lerp(-halfWidth, halfWidth, (float)i / Columns));
        // Include the platform corner in the ramp grid, so the edge does not
        // cut across the corner between two regularly spaced columns.
        for (int i = 0; i < shed.x.Length; i++)
        {
            var vertex = new Vector3(shed.x[i], 0f, shed.z[i]);
            float lateral = Vector3.Dot(vertex - start, side);
            if (Mathf.Abs(lateral) < halfWidth &&
                Mathf.Abs(Vector3.Dot(vertex - start, forward) - FarBoundary(shed, start + side * lateral, forward)) < 0.005f)
                offsets.Add(lateral);
        }
        var entry = new Vector3[offsets.Count];
        var exit = new Vector3[offsets.Count];
        // Follow the corner of the shed platform across the ramp, including its
        // diagonal edge. Starting the descent at this boundary buries the old wedge.
        int column = 0;
        foreach (float lateral in offsets)
        {
            var offset = start + side * lateral;
            entry[column] = AtHeight(offset + forward * FarBoundary(shed, offset, forward), shed.y);
            exit[column] = AtHeight(end + side * lateral + forward * 0.28f, dock.y);
            column++;
        }
        const int Rows = 31;
        var surface = new List<RoadSection>();
        dockGrid = new Vector3[Rows + 2, offsets.Count];
        for (int row = 0; row < Rows + 2; row++)
        {
            float t = Mathf.Clamp01((row - 1) / (float)(Rows - 1));
            float height = Mathf.Lerp(shed.y, dock.y, Mathf.SmoothStep(0f, 1f, t));
            for (int col = 0; col < offsets.Count; col++)
            {
                var p = Vector3.Lerp(entry[col], exit[col], t);
                if (row == 0) p -= forward * 0.09f;
                if (row == Rows + 1) p += forward * 0.10f;
                dockGrid[row, col] = AtHeight(p, height + (row == 0 || row == Rows + 1 ? -0.005f : 0.002f));
            }
            surface.Add(new RoadSection(dockGrid[row, 0], dockGrid[row, offsets.Count - 1]));
        }
        return surface;
    }

    static float FarBoundary(Area polygon, Vector3 origin, Vector3 direction)
    {
        var point = new Vector2(origin.x, origin.z);
        var forward = new Vector2(direction.x, direction.z);
        float boundary = float.NegativeInfinity;
        for (int i = 0; i < polygon.x.Length; i++)
        {
            int j = (i + 1) % polygon.x.Length;
            var a = new Vector2(polygon.x[i], polygon.z[i]);
            var edge = new Vector2(polygon.x[j], polygon.z[j]) - a;
            float denominator = Cross(forward, edge);
            if (Mathf.Abs(denominator) < 1e-5f) continue;
            float across = Cross(a - point, forward) / denominator;
            if (across >= -0.00001f && across <= 1.00001f)
                boundary = Mathf.Max(boundary, Cross(a - point, edge) / denominator);
        }
        if (float.IsNegativeInfinity(boundary)) throw new InvalidOperationException("Dock ramp misses the shed platform");
        return boundary;
    }

    static void SetFinishedDeck(GameObject road, List<RoadSection> sections, Material material, bool worldUv = false)
    {
        SaveDeck(road, DeckMesh(sections, false, worldUv, 0f), material);
    }

    static void SaveDeck(GameObject road, Mesh mesh, Material material)
    {
        mesh.name = "Finished Road " + road.name.Substring("Road_".Length);
        road.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, $"{FinishedMeshRoot}/{road.name}_surface.asset");
        // 2026-11-15：原来只换第一个材质（sharedMaterial），BuildRoad 留下的路缘 / 碎石 / 裙边三个材质还挂着。
        // 新网格只有一个子网格，Unity 会拿多出来的材质把整个坡面再画三遍，几种贴图互相打架，坡上拼出一块块斜三角。
        road.GetComponent<MeshRenderer>().sharedMaterials = new[] { material };
    }

    static void SetGround(GameObject road, List<RoadSection> sections, float flatApron = 0f)
    {
        var mesh = DeckMesh(sections, true, false, flatApron);
        road.GetComponent<MeshCollider>().sharedMesh = SaveMesh(mesh, $"{FinishedMeshRoot}/{road.name}_ground.asset");
    }

    static Mesh DockGridMesh(Vector3[,] dockGrid)
    {
        var verts = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        int columns = dockGrid.GetLength(1);
        for (int row = 0; row < dockGrid.GetLength(0); row++)
            for (int col = 0; col < columns; col++)
            {
                var p = dockGrid[row, col];
                verts.Add(p);
                uv.Add(new Vector2(p.x, p.z));
                if (row != 0 && col != 0)
                {
                    int k = row * columns + col;
                    FinishQuad(triangles, k - columns - 1, k - columns, k, k - 1);
                }
            }
        return FinishMesh("Dock landing and ramp", verts, uv, triangles);
    }

    static Mesh DeckMesh(List<RoadSection> sections, bool collision, bool worldUv, float flatApron)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        float distance = 0f;
        int columns = collision ? 4 : 2;
        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (i > 0) distance += SlopeDistance(sections[i - 1].Center, section.Center);
            var across = (section.right - section.left).normalized;
            float half = Vector3.Distance(section.left, section.right) * 0.5f;
            var outerLeft = section.left - across * ShoulderWidth;
            var outerRight = section.right + across * ShoulderWidth;
            if (flatApron > 0f)
            {
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(flatApron, flatApron + 1.50f, distance));
                outerLeft.y = Mathf.Lerp(3.022f, outerLeft.y, blend);
                outerRight.y = Mathf.Lerp(3.022f, outerRight.y, blend);
            }
            if (collision) AddDeckVertex(outerLeft, -half - ShoulderWidth, distance, worldUv, vertices, uv);
            AddDeckVertex(section.left, -half, distance, worldUv, vertices, uv);
            AddDeckVertex(section.right, half, distance, worldUv, vertices, uv);
            if (collision) AddDeckVertex(outerRight, half + ShoulderWidth, distance, worldUv, vertices, uv);
            if (i == 0) continue;
            int k = (i - 1) * columns;
            for (int col = 0; col < columns - 1; col++)
                FinishQuad(triangles, k + col, k + col + 1, k + col + 1 + columns, k + col + columns);
        }
        return FinishMesh(collision ? "Road ground" : "Continuous stone deck", vertices, uv, triangles);
    }

    static void AddDeckVertex(Vector3 point, float side, float distance, bool worldUv, List<Vector3> vertices, List<Vector2> uv)
    {
        vertices.Add(point);
        uv.Add(worldUv ? new Vector2(point.x, point.z) : new Vector2(side, distance));
    }

    // 2026-11-15：坡道两侧的实心石墙。顶边贴着路面边线（往外 2 cm），一直砌到 bottom；两头可以再往前 / 往后伸一段，
    // 插进相邻台地的崖壁里，不留透光缝。放在坡道底下名叫 RoadSideWalls 的子物体里：层 0（不是地面，挡人靠轮廓碰撞），
    // TownToon 按这个名字认出来，不参与相机镂空。
    static void BuildRampSideWalls(GameObject road, List<RoadSection> sections, float bottom, Material material, float extendStart, float extendEnd)
    {
        DestroyChildIfExists(road.transform, "RoadSideWalls");
        if (sections == null || sections.Count < 2) return;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        foreach (int sign in new[] { -1, 1 })
        {
            var edge = new List<Vector3>();
            foreach (var s in sections)
            {
                var across = (s.right - s.left).normalized;
                edge.Add((sign < 0 ? s.left : s.right) + across * (0.02f * sign));
            }
            if (extendStart > 0f) { var d = SlopeDirection(edge[1], edge[0]); edge.Insert(0, AtHeight(edge[0] + d * extendStart, edge[0].y)); }
            if (extendEnd > 0f) { int n = edge.Count; var d = SlopeDirection(edge[n - 2], edge[n - 1]); edge.Add(AtHeight(edge[n - 1] + d * extendEnd, edge[n - 1].y)); }
            float run = 0f;
            for (int i = 1; i < edge.Count; i++)
            {
                var a = edge[i - 1]; var b = edge[i];
                float next = run + SlopeDistance(a, b);
                if (Mathf.Max(a.y, b.y) > bottom + 0.01f)
                    AddTwoSidedQuad(v, uv, t, a, b, AtHeight(b, bottom), AtHeight(a, bottom),
                        new Vector2(run * 0.5f, a.y * 0.5f), new Vector2(next * 0.5f, b.y * 0.5f), new Vector2(next * 0.5f, bottom * 0.5f), new Vector2(run * 0.5f, bottom * 0.5f));
                run = next;
            }
        }
        var go = new GameObject("RoadSideWalls");
        go.transform.SetParent(road.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(FinishMesh("RoadSideWalls", v, uv, t), $"{FinishedMeshRoot}/{road.name}_sidewalls.asset");
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    // 正反两面各用一套顶点。共用顶点的话 RecalculateNormals 把正反两面的法线平均成 0，
    // 着色器里 normalize(0) 得到 NaN，后处理把 NaN 像素放大成一颗颗黑点。
    static void AddTwoSidedQuad(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
    {
        int k = v.Count;
        v.AddRange(new[] { a, b, c, d, a, b, c, d });
        uv.AddRange(new[] { ua, ub, uc, ud, ua, ub, uc, ud });
        FinishQuad(t, k, k + 1, k + 2, k + 3);
        FinishQuad(t, k + 7, k + 6, k + 5, k + 4);
    }

    static void SetRoadbed(GameObject road, List<RoadSection> sections, Material material)
    {
        var obj = road.transform.Find("Rock roadbed");
        if (obj == null)
        {
            obj = new GameObject("Rock roadbed").transform;
            obj.SetParent(road.transform, false);
            obj.gameObject.AddComponent<MeshFilter>();
            obj.gameObject.AddComponent<MeshRenderer>();
        }
        obj.GetComponent<MeshFilter>().sharedMesh = SaveMesh(RoadbedMesh(sections), $"{FinishedMeshRoot}/{road.name}_roadbed.asset");
        obj.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static Mesh RoadbedMesh(List<RoadSection> sections)
    {
        const int rings = 4;
        const int stride = rings * 2 + 1;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        float distance = 0f;
        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (i > 0) distance += Vector3.Distance(sections[i - 1].Center, section.Center);
            var across = (section.right - section.left).normalized;
            // Align the winding with the deck (left, right, next right).
            AddRockEdge(section.left, -across, distance, vertices, uv);
            AddRockEdge(section.right, across, distance, vertices, uv);
            var cap = section.Center - Vector3.up * 0.055f;
            vertices.Add(cap);
            uv.Add(new Vector2(distance, cap.y));
            if (i == 0) continue;
            int k = (i - 1) * stride;
            int n = i * stride;
            for (int j = 0; j < rings - 1; j++)
            {
                FinishQuad(triangles, k + j, n + j, n + j + 1, k + j + 1);
                FinishQuad(triangles, k + rings + j, k + rings + j + 1, n + rings + j + 1, n + rings + j);
            }
            FinishQuad(triangles, k + rings - 1, n + rings - 1, n + rings * 2 - 1, k + rings * 2 - 1);
            FinishQuad(triangles, k, k + stride - 1, n + stride - 1, n);
            FinishQuad(triangles, k + stride - 1, k + rings, n + rings, n + stride - 1);
        }
        int end = (sections.Count - 1) * stride;
        for (int j = 0; j < rings - 1; j++)
        {
            FinishQuad(triangles, j, j + 1, j + 1 + rings, j + rings);
            FinishQuad(triangles, end + j, end + j + rings, end + j + rings + 1, end + j + 1);
        }
        triangles.AddRange(new[] { 0, rings, stride - 1, end, end + stride - 1, end + rings });
        return FinishMesh("Rock roadbed", vertices, uv, triangles);
    }

    static void AddRockEdge(Vector3 top, Vector3 outward, float distance, List<Vector3> vertices, List<Vector2> uv)
    {
        float variation = 0.025f * Mathf.Sin(distance * 2.1f) + 0.015f * Mathf.Sin(distance * 4.2f);
        float height = Mathf.Clamp(top.y + 0.22f, 0.28f, 1f);
        var lip = top + outward * (0.065f + variation) * height - Vector3.up * (0.15f * height);
        var face = top + outward * (0.21f + variation * 1.65f) * height - Vector3.up * (0.72f * height);
        var basePoint = AtHeight(top + outward * (0.47f + variation * 2.1f) * height, -0.22f);
        foreach (var p in new[] { top, lip, face, basePoint })
        {
            vertices.Add(p);
            uv.Add(new Vector2(distance, p.y));
        }
    }

    static void FinishQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    static Mesh FinishMesh(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static GameObject FindGenerated(string name)
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name != "Generated") continue;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        }
        return null;
    }

    static Transform GeneratedRoot() => RequireObject("Road_to_dock").transform.parent;

    static void DestroyChildIfExists(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var c = parent.GetChild(i);
            if (c.name == name) UnityEngine.Object.DestroyImmediate(c.gameObject);
        }
    }

    static void AddOrientedBox(List<Vector3> v, List<Vector2> uv, List<int> t,
        Vector3 center, Vector3 right, Vector3 up, Vector3 fwd, Vector3 size)
    {
        if (Vector3.Dot(Vector3.Cross(up, fwd), right) < 0f) right = -right;
        Vector3 rx = right * (size.x * 0.5f), uy = up * (size.y * 0.5f), fz = fwd * (size.z * 0.5f);
        Vector3[] c =
        {
            center - rx - uy - fz, center + rx - uy - fz, center + rx + uy - fz, center - rx + uy - fz,
            center - rx - uy + fz, center + rx - uy + fz, center + rx + uy + fz, center - rx + uy + fz,
        };
        int[,] f = { {3,7,6,2}, {0,1,5,4}, {0,3,2,1}, {5,6,7,4}, {4,7,3,0}, {1,2,6,5} };
        for (int s = 0; s < 6; s++)
        {
            int b = v.Count;
            for (int k = 0; k < 4; k++)
            {
                var p = c[f[s, k]];
                v.Add(p);
                uv.Add(new Vector2(p.x, p.z));
            }
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
            t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }
    }

    static void AddCylinderPiling(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 baseCenter, float radius, float height, int sides = 10)
    {
        int botRing = v.Count;
        for (int i = 0; i < sides; i++)
        {
            float a = i * (2f * Mathf.PI / sides);
            var p = baseCenter + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            v.Add(p); uv.Add(new Vector2(a / (2f * Mathf.PI), 0f));
        }
        int topRing = v.Count;
        for (int i = 0; i < sides; i++)
        {
            float a = i * (2f * Mathf.PI / sides);
            var p = baseCenter + new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
            v.Add(p); uv.Add(new Vector2(a / (2f * Mathf.PI), height));
        }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            FinishQuad(t, botRing + i, topRing + i, topRing + j, botRing + j);
        }
        int capCenter = v.Count;
        v.Add(baseCenter + Vector3.up * height);
        uv.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i < sides; i++)
        {
            t.Add(capCenter); t.Add(topRing + (i + 1) % sides); t.Add(topRing + i);
        }
    }

    static void AddRopeSpan(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, float radius = 0.026f, float sag = 0.14f, int segs = 6, int sides = 5)
    {
        int[] rings = new int[segs + 1];
        for (int i = 0; i <= segs; i++)
        {
            float u = i / (float)segs;
            float uNext = Mathf.Min(1f, (i + 0.5f) / segs), uPrev = Mathf.Max(0f, (i - 0.5f) / segs);
            Vector3 P(float s) => Vector3.Lerp(a, b, s) - Vector3.up * (sag * 4f * s * (1f - s));
            Vector3 p = P(u), tan = (P(uNext) - P(uPrev)).normalized;
            Vector3 r = Vector3.Cross(tan, Vector3.up);
            if (r.sqrMagnitude < 1e-4f) r = Vector3.right; else r.Normalize();
            Vector3 up = Vector3.Cross(r, tan).normalized;
            rings[i] = v.Count;
            for (int k = 0; k < sides; k++)
            {
                float ang = k * (2f * Mathf.PI / sides);
                v.Add(p + (r * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius);
                uv.Add(new Vector2(u * 2f, k / (float)sides));
            }
        }
        for (int i = 0; i < segs; i++)
            for (int k = 0; k < sides; k++)
                FinishQuad(t, rings[i] + k, rings[i + 1] + k, rings[i + 1] + (k + 1) % sides, rings[i] + (k + 1) % sides);
    }

    /// <summary>Adds non-colliding stone stair treads + low side stringers on descending stair slopes.</summary>
    static void BuildStairStepsAndStringers(GameObject road, string childName, List<RoadSection> sections, Material cobble, float minY, float stringerMaxY)
    {
        DestroyChildIfExists(road.transform, childName);
        if (sections == null || sections.Count < 4) return;
        var curbMat = FlatMat("CurbStone", new Color32(0xdf, 0xc6, 0xa8, 255), 0.12f);

        var treadPts = new List<RoadSection>();
        for (int i = 0; i < sections.Count - 1; i++)
        {
            float dy = Mathf.Abs(sections[i + 1].Center.y - sections[i].Center.y);
            float ds = Vector2.Distance(new Vector2(sections[i].Center.x, sections[i].Center.z), new Vector2(sections[i + 1].Center.x, sections[i + 1].Center.z));
            if (sections[i].Center.y >= minY && dy / Mathf.Max(0.05f, ds) > 0.10f)
                treadPts.Add(sections[i]);
        }
        if (treadPts.Count < 2) return;

        var vS = new List<Vector3>(); var uvS = new List<Vector2>(); var tS = new List<int>();
        var vC = new List<Vector3>(); var uvC = new List<Vector2>(); var tC = new List<int>();

        // Accumulate arc along stair span and place ~0.42m steps
        float arc = 0f, nextStep = 0.18f;
        for (int i = 0; i < treadPts.Count - 1; i++)
        {
            var a = treadPts[i]; var b = treadPts[i + 1];
            Vector3 ac = a.Center, bc = b.Center;
            float seg = SlopeDistance(ac, bc);
            if (seg < 0.02f) continue;
            Vector3 fwd = new Vector3(bc.x - ac.x, 0f, bc.z - ac.z).normalized;
            Vector3 side = Vector3.Cross(fwd, Vector3.up).normalized;

            while (nextStep <= arc + seg)
            {
                float f = Mathf.Clamp01((nextStep - arc) / seg);
                Vector3 c = Vector3.Lerp(ac, bc, f);
                float w = Mathf.Lerp(Vector3.Distance(a.left, a.right), Vector3.Distance(b.left, b.right), f);
                AddOrientedBox(vS, uvS, tS, new Vector3(c.x, c.y + 0.015f, c.z), side, Vector3.up, fwd, new Vector3(w * 0.94f, 0.07f, 0.34f));
                nextStep += 0.36f;
            }
            arc += seg;

            if (ac.y >= stringerMaxY)
            {
                float hwL = Vector3.Distance(a.left, ac), hwR = Vector3.Distance(ac, a.right);
                Vector3 dir3 = (bc - ac).normalized;
                Vector3 up3 = Vector3.Cross(dir3, side).normalized;
                if (up3.y < 0f) up3 = -up3;
                Vector3 mid = (ac + bc) * 0.5f;
                float hw = (hwL + hwR) * 0.5f;
                AddOrientedBox(vC, uvC, tC, mid - side * (hw - 0.04f) + up3 * 0.035f, side, up3, dir3, new Vector3(0.16f, 0.10f, (bc - ac).magnitude + 0.04f));
                AddOrientedBox(vC, uvC, tC, mid + side * (hw - 0.04f) + up3 * 0.035f, side, up3, dir3, new Vector3(0.16f, 0.10f, (bc - ac).magnitude + 0.04f));
            }
        }

        if (vS.Count > 0)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(road.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = SaveMesh(FinishMesh(childName, vS, uvS, tS), $"{FinishedMeshRoot}/{road.name}_steps.asset");
            go.AddComponent<MeshRenderer>().sharedMaterial = cobble;
        }
        if (vC.Count > 0)
        {
            var go = new GameObject(childName + "_curbs");
            go.transform.SetParent(road.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = SaveMesh(FinishMesh(childName + "_curbs", vC, uvC, tC), $"{FinishedMeshRoot}/{road.name}_stringers.asset");
            go.AddComponent<MeshRenderer>().sharedMaterial = curbMat;
        }
    }

    static void BuildDockStairTreads(GameObject dock, List<RoadSection> link, Material cobble)
    {
        const string name = "Finished Road to_dock_stairs";
        DestroyChildIfExists(dock.transform, name);
        if (link == null || link.Count < 4) return;
        var curbMat = FlatMat("CurbStone", new Color32(0xdf, 0xc6, 0xa8, 255), 0.12f);

        var start = link[0].Center;
        var end = link[link.Count - 1].Center;
        Vector3 fwd = new Vector3(end.x - start.x, 0f, end.z - start.z);
        float horiz = Mathf.Max(0.2f, fwd.magnitude);
        fwd /= horiz;
        Vector3 side = Vector3.Cross(fwd, Vector3.up).normalized;

        var vS = new List<Vector3>(); var uvS = new List<Vector2>(); var tS = new List<int>();
        const int steps = 4;
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.55f) / steps;
            Vector3 c = Vector3.Lerp(start, end, t);
            float stepTopY = Mathf.Lerp(start.y - 0.04f, end.y + 0.04f, i / (float)steps);
            AddOrientedBox(vS, uvS, tS, new Vector3(c.x, stepTopY - 0.055f, c.z), side, Vector3.up, fwd, new Vector3(1.56f, 0.11f, (horiz / steps) + 0.08f));
        }

        var vC = new List<Vector3>(); var uvC = new List<Vector2>(); var tC = new List<int>();
        Vector3 dir3 = (end - start).normalized;
        Vector3 up3 = Vector3.Cross(dir3, side).normalized;
        if (up3.y < 0f) up3 = -up3;
        Vector3 mid = (start + end) * 0.5f + up3 * 0.06f;
        AddOrientedBox(vC, uvC, tC, mid - side * 0.84f, side, up3, dir3, new Vector3(0.18f, 0.14f, (end - start).magnitude + 0.12f));
        AddOrientedBox(vC, uvC, tC, mid + side * 0.84f, side, up3, dir3, new Vector3(0.18f, 0.14f, (end - start).magnitude + 0.12f));

        var go = new GameObject(name);
        go.transform.SetParent(dock.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(FinishMesh(name, vS, uvS, tS), $"{FinishedMeshRoot}/Road_to_dock_steps.asset");
        go.AddComponent<MeshRenderer>().sharedMaterial = cobble;

        var cgo = new GameObject(name + "_curbs");
        cgo.transform.SetParent(dock.transform, false);
        cgo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(FinishMesh(name + "_curbs", vC, uvC, tC), $"{FinishedMeshRoot}/Road_to_dock_stringers.asset");
        cgo.AddComponent<MeshRenderer>().sharedMaterial = curbMat;
    }

    static bool NearLayoutBuilding(Layout layout, Vector2 p, float margin)
    {
        if (layout.buildings == null) return false;
        foreach (var b in layout.buildings)
        {
            float r = Mathf.Max(1.6f, b.width * 0.42f) + margin;
            if (Vector2.Distance(new Vector2(b.x, b.z), p) < r) return true;
        }
        return false;
    }

    static void BuildPromenadeCurbs(Layout layout, Material cobble)
    {
        var parent = GeneratedRoot();
        DestroyChildIfExists(parent, "PromenadeCurbs");
        if (layout.walk == null) return;

        var walks = new List<(string id, float y, List<Vector2> poly)>();
        List<Vector2> padStone = null, padGrass = null;
        float stoneY = 3.02f;
        foreach (var a in layout.walk)
        {
            var poly = new List<Vector2>();
            for (int i = 0; i < a.x.Length; i++) poly.Add(new Vector2(a.x[i], a.z[i]));
            walks.Add((a.id, a.y, poly));
            if (a.id == "pad_stone_3_0_0") { padStone = poly; stoneY = a.y; }
            else if (a.id == "pad_grass_3_0_0") padGrass = poly;
        }
        if (padStone == null) return;

        var curbMat = FlatMat("CurbStone", new Color32(0xdf, 0xc6, 0xa8, 255), 0.12f);
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        Vector2 schoolGate = new Vector2(21.4f, -6.2f);

        // Build crisp outer curbs along pad_stone_3_0_0 & border curbs around pad_grass_3_0_0
        for (int i = 0; i < padStone.Count; i++)
        {
            var a = padStone[i]; var b = padStone[(i + 1) % padStone.Count];
            float len = (b - a).magnitude;
            if (len < 0.08f) continue;
            var dir = (b - a) / len;
            var outN = new Vector2(dir.y, -dir.x);
            if (InPolyXZ(padStone, (a + b) * 0.5f + outN * 0.12f)) outN = -outN;
            var mid = (a + b) * 0.5f;
            var probe = mid + outN * 0.35f;

            // Curb allowed along cliff rim (no same-height neighbor outside) OR shared school_lawn border (outside gate openings)
            bool touchesGrass = padGrass != null && DistToPoly(padGrass, probe) < 0.40f;
            bool skip = NearLayoutBuilding(layout, mid, 0.2f);
            if (!skip)
            {
                if (touchesGrass)
                {
                    if (mid.x < 12.2f || Vector2.Distance(mid, schoolGate) < 1.75f) skip = true;
                }
                else
                {
                    foreach (var o in walks)
                    {
                        if (o.id == "pad_stone_3_0_0") continue;
                        if (Mathf.Abs(o.y - stoneY) < 0.70f && DistToPoly(o.poly, probe) < 0.55f) { skip = true; break; }
                    }
                }
            }
            if (skip) continue;

            var c = mid - outN * 0.10f;
            var fwd3 = new Vector3(dir.x, 0f, dir.y);
            var side3 = new Vector3(outN.x, 0f, outN.y);
            AddOrientedBox(v, uv, t, new Vector3(c.x, stoneY + 0.055f, c.y), side3, Vector3.up, fwd3, new Vector3(0.22f, 0.13f, len + 0.04f));
        }

        if (v.Count == 0) return;
        var go = new GameObject("PromenadeCurbs");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(FinishMesh("PromenadeCurbs", v, uv, t), $"{FinishedMeshRoot}/PromenadeCurbs.asset");
        go.AddComponent<MeshRenderer>().sharedMaterial = curbMat;
    }

    static List<(float a, float b)> IntersectHorizontalLineWithPoly(List<Vector2> poly, float z)
    {
        var xs = new List<float>();
        for (int i = 0; i < poly.Count; i++)
        {
            var p1 = poly[i]; var p2 = poly[(i + 1) % poly.Count];
            if ((p1.y <= z && p2.y > z) || (p2.y <= z && p1.y > z))
                xs.Add(p1.x + (z - p1.y) / (p2.y - p1.y) * (p2.x - p1.x));
        }
        xs.Sort();
        var spans = new List<(float a, float b)>();
        for (int i = 0; i + 1 < xs.Count; i += 2)
            if (xs[i + 1] - xs[i] > 0.18f) spans.Add((xs[i], xs[i + 1]));
        return spans;
    }

    static void BuildWoodenPier(Layout layout)
    {
        var parent = GeneratedRoot();
        DestroyChildIfExists(parent, "HarborPier");

        var tierPier = FindGenerated("Tier_pier");
        var tierPierWall = FindGenerated("Tier_pier_wall");
        if (tierPier == null || layout.tiers == null) return;
        var pierArea = Array.Find(layout.tiers, a => a.id == "pier");
        var shedArea = Array.Find(layout.tiers, a => a.id == "shed");
        if (pierArea == null || pierArea.x == null || pierArea.x.Length < 3) return;

        // Preserve Tier_pier MeshCollider (Ground) while hiding flat stone block visuals
        if (tierPier.TryGetComponent<MeshRenderer>(out var pr)) pr.enabled = false;
        if (tierPierWall != null && tierPierWall.TryGetComponent<MeshRenderer>(out var pwr)) pwr.enabled = false;

        var poly = new List<Vector2>();
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < pierArea.x.Length; i++)
        {
            var pt = new Vector2(pierArea.x[i], pierArea.z[i]);
            poly.Add(pt);
            minX = Mathf.Min(minX, pt.x); maxX = Mathf.Max(maxX, pt.x);
            minZ = Mathf.Min(minZ, pt.y); maxZ = Mathf.Max(maxZ, pt.y);
        }
        var shedPoly = new List<Vector2>();
        if (shedArea != null) for (int i = 0; i < shedArea.x.Length; i++) shedPoly.Add(new Vector2(shedArea.x[i], shedArea.z[i]));

        var woodMat = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/PierWood.mat")
                      ?? FlatMat("PierWood", new Color32(0xb1, 0x88, 0x57, 255), 0.14f);
        var timberMat = FlatMat("PierTimber", new Color32(0x76, 0x56, 0x36, 255), 0.10f);
        var ropeMat = FlatMat("PierRope", new Color32(0xc8, 0xb0, 0x84, 255), 0.05f);

        float deckY = pierArea.y;
        var root = new GameObject("HarborPier");
        root.transform.SetParent(parent, false);

        // 1. Wood planks running East-West at top = deckY + 0.002 (thickness 0.10m)
        var vD = new List<Vector3>(); var uvD = new List<Vector2>(); var tD = new List<int>();
        const float plankPitch = 0.25f;
        for (float z = minZ + 0.12f; z < maxZ - 0.10f; z += plankPitch)
        {
            foreach (var (a, b) in IntersectHorizontalLineWithPoly(poly, z))
            {
                float w = (b - a) - 0.04f;
                if (w < 0.2f) continue;
                AddOrientedBox(vD, uvD, tD, new Vector3((a + b) * 0.5f, deckY - 0.048f, z), Vector3.right, Vector3.up, Vector3.forward, new Vector3(w, 0.10f, 0.215f));
            }
        }
        // Dark under-deck recessed liner & beams + timber pilings
        var vB = new List<Vector3>(); var uvB = new List<Vector2>(); var tB = new List<int>();
        for (float z = minZ + 0.16f; z < maxZ - 0.14f; z += 0.28f)
        {
            foreach (var (a, b) in IntersectHorizontalLineWithPoly(poly, z))
            {
                float w = (b - a) - 0.14f;
                if (w < 0.2f) continue;
                AddOrientedBox(vB, uvB, tB, new Vector3((a + b) * 0.5f, deckY - 0.075f, z), Vector3.right, Vector3.up, Vector3.forward, new Vector3(w, 0.06f, 0.30f));
            }
        }
        for (float x = minX + 0.6f; x < maxX - 0.4f; x += 2.15f)
            AddOrientedBox(vB, uvB, tB, new Vector3(x, deckY - 0.18f, (minZ + maxZ) * 0.5f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.22f, 0.18f, (maxZ - minZ) * 0.86f));
        for (float z = minZ + 0.45f; z < maxZ - 0.35f; z += 1.4f)
            foreach (var (a, b) in IntersectHorizontalLineWithPoly(poly, z))
                if (b - a > 0.6f)
                    AddOrientedBox(vB, uvB, tB, new Vector3((a + b) * 0.5f, deckY - 0.19f, z), Vector3.right, Vector3.up, Vector3.forward, new Vector3((b - a) - 0.3f, 0.18f, 0.22f));

        // 2. Perimeter Pilings + Bollard Posts + Hanging Rope Rails on open seaward edges
        var vR = new List<Vector3>(); var uvR = new List<Vector2>(); var tR = new List<int>();
        var dockRoad = FindRoad(layout, "to_dock");
        Vector2 dockMouth = dockRoad != null && dockRoad.x.Length > 0 ? new Vector2(dockRoad.x[0], dockRoad.z[0]) : new Vector2(23.2f, -16.3f);

        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            float len = (b - a).magnitude;
            if (len < 0.2f) continue;
            var dir = (b - a) / len;
            var outN = new Vector2(dir.y, -dir.x);
            if (InPolyXZ(poly, (a + b) * 0.5f + outN * 0.1f)) outN = -outN;
            int spans = Mathf.Max(1, Mathf.RoundToInt(len / 1.95f));
            Vector3? prevPost = null;
            for (int s = 0; s <= spans; s++)
            {
                var e = Vector2.Lerp(a, b, s / (float)spans);
                // Keep north/west landward seam buried inside stone shed terrace free of railings/pilings
                bool inShed = shedPoly.Count >= 3 && DistToPoly(shedPoly, e + outN * 0.30f) < 0.35f;
                bool inDockMouth = Vector2.Distance(e, dockMouth) < 1.55f;
                if (inShed) { prevPost = null; continue; }

                // Underwater cylindrical timber piling at outer edge of deck
                var pPos = e - outN * 0.04f;
                bool hasBollard = !inDockMouth && !NearLayoutBuilding(layout, e, 0.8f);
                float topY = hasBollard ? deckY + 0.62f : deckY + 0.03f;
                AddCylinderPiling(vB, uvB, tB, new Vector3(pPos.x, Seabed, pPos.y), hasBollard ? 0.145f : 0.155f, topY - Seabed, 10);

                if (hasBollard)
                {
                    var ropeAttach = new Vector3(pPos.x, deckY + 0.50f, pPos.y);
                    if (prevPost.HasValue && Vector3.Distance(prevPost.Value, ropeAttach) < 2.6f)
                        AddRopeSpan(vR, uvR, tR, prevPost.Value, ropeAttach, 0.025f, 0.13f);
                    prevPost = ropeAttach;
                }
                else prevPost = null;
            }
        }

        void SavePart(string partName, List<Vector3> verts, List<Vector2> uvs, List<int> tris, Material mat)
        {
            if (verts.Count == 0) return;
            var go = new GameObject(partName);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(FinishMesh(partName, verts, uvs, tris), $"{FinishedMeshRoot}/{partName}.asset");
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        SavePart("PierDeck", vD, uvD, tD, woodMat);
        SavePart("PierPilings", vB, uvB, tB, timberMat);
        SavePart("PierRopes", vR, uvR, tR, ropeMat);
    }
}
