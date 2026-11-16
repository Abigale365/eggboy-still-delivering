// 2026-11-07 新地图街景（preview.png v2）：
//   · 石板路材质（CobbleMat）：主街、广场、灯塔台这些 zone=stone 的台地顶面直接用它（IslandBuilder.BuildMain）
//   · 木栅栏：可走区外沿、外面落差 ≥ 1.5 m 的地方（临海步道、灯塔台），用 Lux3D 的 S7 一段段排开
//   · 路灯：栅栏线上每 4 段一盏 Lux3D 的 S4
//   · 小船：Lux3D 的 S9 放到 28 号沙滩
//   · 喷泉占位：中央广场 FOUNTAIN 点，灰盒拼的（F1 模型到了再换），带碰撞
// 栅栏、路灯、小船都不进 Ground 层、没有碰撞：GroundAt / Walkable 和以前一样，路线和剧情判定不受影响。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    static readonly List<Vector3> streetscapeBlockers = new List<Vector3>();   // 小船、喷泉这类大件，花丛和礁石要避开

    // ---------- 石板贴图 ----------

    static Material CobbleMat()
    {
        if (!AssetDatabase.IsValidFolder(TexRoot)) AssetDatabase.CreateFolder("Assets/TideTown/Art", "Textures");
        // 2026-11-12 美术改版：石板换成浅奶油粉的配色（文件名带 2，旧贴图有缓存不会重画）
        string albedoPath = TexRoot + "/NatureCobble2_Albedo.png";
        string normalPath = TexRoot + "/NatureCobble2_Normal.png";
        if (!System.IO.File.Exists(albedoPath) || !System.IO.File.Exists(normalPath))
        {
            BuildCobbleTextures(albedoPath, normalPath, 83);
            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("NATURETEX generated NatureCobble");
        }
        ConfigureSurfaceTexture(albedoPath, false);
        ConfigureSurfaceTexture(normalPath, true);
        string path = MatRoot + "/IslandCobble.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.12f);
        m.SetFloat("_Metallic", 0f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        m.EnableKeyword("_NORMALMAP");
        var scale = new Vector2(1f / 1.8f, 1f / 1.8f);   // 一张 1.8 m，每块石板约 0.22 m（2.6 m 时石块显大、像地砖）
        m.SetTextureScale("_BaseMap", scale);
        m.SetTextureScale("_BumpMap", scale);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 错位网格的 Worley 噪声：F1 决定石块隆起，F2-F1 小的地方是灰缝
    static void BuildCobbleTextures(string albedoPath, string normalPath, int seed)
    {
        int n = SurfaceTexSize;
        int cells = 8;
        float cs = n / (float)cells;
        var pts = new Vector2[cells * cells];
        var tone = new float[cells * cells];
        var hue = new float[cells * cells];
        for (int cy = 0; cy < cells; cy++)
        for (int cx = 0; cx < cells; cx++)
        {
            int k = cy * cells + cx;
            float off = (cy % 2) * 0.5f;     // 隔行错开半块，像铺出来的
            pts[k] = new Vector2((cx + off + 0.2f + Hash01(cx, cy, seed) * 0.6f) * cs,
                                 (cy + 0.2f + Hash01(cx, cy, seed + 1) * 0.6f) * cs);
            tone[k] = Hash01(cx, cy, seed + 2);
            hue[k] = Hash01(cx, cy, seed + 3);
        }
        var height = new float[n * n];
        var albedo = new Color32[n * n];
        var normal = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float f1 = float.MaxValue, f2 = float.MaxValue; int best = 0;
            int gx = Mathf.FloorToInt(x / cs), gy = Mathf.FloorToInt(y / cs);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int cx = gx + dx, cy = gy + dy;
                int wx = ((cx % cells) + cells) % cells, wy = ((cy % cells) + cells) % cells;
                var p = pts[wy * cells + wx];
                float px = p.x + (cx - wx) * cs, py = p.y + (cy - wy) * cs;
                float ddx = x + 0.5f - px, ddy = y + 0.5f - py;
                float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                if (d < f1) { f2 = f1; f1 = d; best = wy * cells + wx; }
                else if (d < f2) f2 = d;
            }
            float edge = (f2 - f1) / cs;
            float mortar = Mathf.Clamp01(1f - edge / 0.12f);
            float dome = Mathf.Clamp01(1f - f1 / (cs * 0.75f));
            float u = x / (float)n, v = y / (float)n;
            float grain = LatticeNoise(u, v, 96, seed + 9);
            float mott = Fbm(u, v, 3, seed + 11);
            int i = y * n + x;
            height[i] = (1f - mortar) * (0.35f + 0.5f * Mathf.Sqrt(dome)) + 0.08f * grain;
            // 石块：暖米色 / 浅粉，块与块深浅不一；灰缝偏沙色、更暗（目标图的路是暖粉米色）
            float t = tone[best], h = hue[best];
            float sr = 0.88f + 0.06f * h, sg = 0.79f + 0.04f * h, sb = 0.72f - 0.02f * h;   // 浅奶油粉（黄昏暖光会再染一层橙，底色要浅、要淡）
            if (h < 0.25f) { sr = 0.80f; sg = 0.77f; sb = 0.78f; }
            float shade = 0.74f + 0.22f * t + 0.10f * (mott - 0.5f) + 0.06f * (grain - 0.5f) + 0.10f * dome;
            float r = Mathf.Lerp(sr * shade, 0.54f, mortar);
            float g = Mathf.Lerp(sg * shade, 0.46f, mortar);
            float b = Mathf.Lerp(sb * shade, 0.44f, mortar);
            albedo[i] = new Color32(ToByte(r), ToByte(g), ToByte(b), 255);
        }
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int xm = Wrap(x - 1, n), xp = Wrap(x + 1, n), ym = Wrap(y - 1, n), yp = Wrap(y + 1, n);
            float gx = (height[xp + y * n] - height[xm + y * n]) * 0.5f;
            float gy = (height[x + yp * n] - height[x + ym * n]) * 0.5f;
            var nrm = new Vector3(-gx * 6f, -gy * 6f, 1f).normalized;
            normal[x + y * n] = new Color32(ToByte(nrm.x * 0.5f + 0.5f), ToByte(nrm.y * 0.5f + 0.5f), ToByte(nrm.z * 0.5f + 0.5f), 255);
        }
        WriteSurfaceTexture(albedoPath, albedo, n);
        WriteSurfaceTexture(normalPath, normal, n);
    }

    // ---------- 摆放 ----------

    static void BuildStreetscape(Layout layout, Transform parent, List<WalkArea> walks)
    {
        streetscapeBlockers.Clear();
        var root = new GameObject("Streetscape").transform;
        root.SetParent(parent, false);
        int fences = 0, lamps = 0;

        // 1. 矮木栅栏 + 路灯（目标图：每条石板路两边都围着一圈矮木栅栏，隔一段一盏路灯）
        //    沿每块平的可走区（石板路、小广场、学校草坪、灯塔台）的外沿；外面是别的可走区（路口）或贴着房子/摊位的地方留口子。
        //    栅栏是程序拼的（立柱 + 两道横杆，0.75 m 高），一块可走区合成一个网格；不单独做碰撞——
        //    栅栏正好立在可走区边线上，边线本来就挡人，看到的和撞到的一致。
        var wood = FlatMat("FenceWood", new Color32(0x8a, 0x5e, 0x3a, 255), 0.08f);
        var fenceRoot = new GameObject("Fences").transform; fenceRoot.SetParent(root, false);
        float lampRun = 0f;
        Vector2 schoolGate = new Vector2(21.4f, -6.2f);
        var schoolPromenade = SmoothCoastal(PromenadeKnots);
        for (int wi = 0; wi < walks.Count; wi++)
        {
            var w = walks[wi];
            string id = layout.walk[wi].id;
            if (!id.StartsWith("pad_") && id != "lighthouse") continue;    // 坡道、码头、船棚不围
            bool schoolLawn = id == "pad_grass_3_0_0";
            float postH = schoolLawn ? 0.66f : 0.75f;
            float r1 = schoolLawn ? 0.28f : 0.34f;
            float r2 = schoolLawn ? 0.52f : 0.60f;
            var verts = new List<Vector3>(); var tris = new List<int>();
            var parkSpans = new List<(Vector3 mid, float len, float yaw)>();
            var poly = w.poly;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                float len = (b - a).magnitude;
                if (len < 0.05f) continue;
                var dir = (b - a) / len; var outN = new Vector2(dir.y, -dir.x);
                if (InPolyXZ(poly, (a + b) * 0.5f + outN * 0.05f)) outN = -outN;
                int posts = Mathf.Max(1, Mathf.RoundToInt(len / 1.25f));
                Vector2? prev = null, lampPrev = null;
                bool prevOnStone = false;
                for (int s = 0; s <= posts; s++)
                {
                    var c = Vector2.Lerp(a, b, s / (float)posts) - outN * 0.08f;
                    // 留口子：外面 0.5 m 是同层或往上接续的可走区（路口），或者贴着建筑/摊位
                    var probe = c + outN * 0.5f;
                    bool gap = NearBuilding(new Vector3(c.x, w.y, c.y), 0.9f);
                    // 2026-11-15：海滨步道（CoastalPromenade）自己有一道 school_border 石墩木栏，
                    // 学校草坪靠步道那一侧不再立第二道木栅栏（两道叠着穿模），由步道那道收边。
                    // 这段只是不画；路灯间距照原来的栅栏长度累计（lampPrev），全镇路灯位置不变。
                    bool promenadeSide = schoolLawn && CoastalDistance(new Vector3(c.x, w.y, c.y), schoolPromenade) < 2.45f;
                    // 第四轮：剧情站位旁边不立栏杆（灯塔塔脚 31 原来正好站在栏杆上；栏杆现在按外形挡人，会让人走不到）
                    if (!gap && layout.points != null)
                        foreach (var pt in layout.points)
                            if (pt.id != "FOUNTAIN" && Mathf.Abs(pt.y - w.y) < 1f && (new Vector2(pt.x, pt.z) - c).sqrMagnitude < 0.9f * 0.9f) { gap = true; break; }
                    bool onStone = false;
                    if (!gap)
                    {
                        for (int oi = 0; oi < walks.Count; oi++)
                        {
                            var o = walks[oi];
                            if (o == w) continue;
                            float dy = o.y - w.y;
                            // 下方 -1.6 m 的低台地（船棚、绞盘台）不算平面路口，免得海边崖顶栏杆被整段挖空
                            if (dy < -0.75f || dy > 1.25f) continue;
                            if (DistToPoly(o.poly, probe) < 0.35f || DistToPoly(o.poly, c) < 0.6f)
                            {
                                if (schoolLawn && layout.walk[oi].id == "pad_stone_3_0_0")
                                {
                                    // 学校操场临街一侧保留低矮园区栅栏，仅敞开 SW 入口和 SE 路线大门
                                    if (c.x < 12.2f || Vector2.Distance(c, schoolGate) < 1.75f) { gap = true; break; }
                                    onStone = true;
                                }
                                else { gap = true; break; }
                            }
                        }
                    }
                    if (gap) { prev = null; lampPrev = null; prevOnStone = false; continue; }
                    if (promenadeSide)
                    {
                        if (lampPrev.HasValue) lampRun += (c - lampPrev.Value).magnitude;
                        lampPrev = c; prev = null; prevOnStone = false;
                        continue;
                    }
                    AddFenceBox(verts, tris, new Vector3(c.x, w.y, c.y), new Vector3(0.11f, postH, 0.11f), 0f);
                    if (prev.HasValue)
                    {
                        var m = (prev.Value + c) * 0.5f; float l = (c - prev.Value).magnitude;
                        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        AddFenceBox(verts, tris, new Vector3(m.x, w.y + r1, m.y), new Vector3(0.05f, 0.06f, l), yaw);
                        AddFenceBox(verts, tris, new Vector3(m.x, w.y + r2, m.y), new Vector3(0.05f, 0.06f, l), yaw);
                        if (schoolLawn && (onStone || prevOnStone))
                            parkSpans.Add((new Vector3(m.x, w.y + postH * 0.5f, m.y), l + 0.10f, yaw));
                        fences++;
                    }
                    if (lampPrev.HasValue)
                    {
                        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        lampRun += (c - lampPrev.Value).magnitude;
                        if (lampRun > 9f && id.StartsWith("pad_stone"))
                        {
                            lampRun = 0f;
                            var lamp = new GameObject("Lamp");
                            lamp.transform.SetParent(root, false);
                            lamp.transform.position = new Vector3(c.x, w.y + 0.01f, c.y);
                            var lampModel = MountSourceModel(lamp.transform, "S4", Vector3.zero, 2.2f, true, yaw);
                            AddLampGlow(lamp.transform, lampModel);
                            lamps++;
                        }
                    }
                    prev = c;
                    lampPrev = c;
                    prevOnStone = onStone;
                }
            }
            if (verts.Count == 0) continue;
            var mesh = new Mesh { name = "Fence_" + id, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, NatureMeshRoot + "/Fence_" + id + ".asset");
            var fgo = new GameObject("Fence_" + id);
            fgo.transform.SetParent(fenceRoot, false);
            fgo.AddComponent<MeshFilter>().sharedMesh = mesh;
            fgo.AddComponent<MeshRenderer>().sharedMaterial = wood;
            GameObjectUtility.SetStaticEditorFlags(fgo, StaticEditorFlags.BatchingStatic);
            for (int si = 0; si < parkSpans.Count; si++)
            {
                var sp = parkSpans[si];
                var colGo = new GameObject("SchoolFenceCol_" + si);
                colGo.transform.SetParent(fgo.transform, false);
                colGo.transform.position = sp.mid;
                colGo.transform.rotation = Quaternion.Euler(0f, sp.yaw, 0f);
                colGo.AddComponent<BoxCollider>().size = new Vector3(0.16f, postH + 0.08f, sp.len);
                colGo.AddComponent<TownObstacle>().kind = TownObstacle.Kind.Obstacle;
            }
        }

        // 2. 小船：28 号沙滩，船头斜朝海；东侧码头旁也系泊一艘小船，并补上一盏码头港灯
        if (layout.points != null)
            foreach (var pt in layout.points)
            {
                if (pt.id != "28") continue;
                float gy = GroundBelow(pt.x, pt.z, 2f);
                var holder = new GameObject("BeachBoat");
                holder.transform.SetParent(root, false);
                holder.transform.position = new Vector3(pt.x, Mathf.Max(gy, 0.12f) + 0.02f, pt.z);
                MountSourceModel(holder.transform, "S9", Vector3.zero, 3.0f, false, 115f);
                streetscapeBlockers.Add(holder.transform.position);
            }
        if (layout.tiers != null)
        {
            var pierTier = Array.Find(layout.tiers, a => a.id == "pier");
            if (pierTier != null)
            {
                var pierLampPos = new Vector3(26.75f, pierTier.y + 0.01f, -16.12f);
                var pierLamp = new GameObject("PierLamp");
                pierLamp.transform.SetParent(root, false);
                pierLamp.transform.position = pierLampPos;
                var lampModel = MountSourceModel(pierLamp.transform, "S4", Vector3.zero, 2.2f, true, 160f);
                AddLampGlow(pierLamp.transform, lampModel);
                lamps++;

                // 2026-11-15：小船原来离码头 3 米漂在海里，挪到木码头东头外侧，顺着码头边停（船头朝海）。
                // 船帮离码头边留 0.9 m：再近，轮廓碰撞会在码头边上长出一排圆，小黑在码头东北角被卡住（创新审计 dog_steps_on_plate）
                var boatPos = new Vector3(28.45f, 0.76f, -15.5f);
                var harborBoat = new GameObject("HarborBoat");
                harborBoat.transform.SetParent(root, false);
                harborBoat.transform.position = boatPos;
                MountSourceModel(harborBoat.transform, "S9", Vector3.zero, 3.5f, false, 180f);
                streetscapeBlockers.Add(boatPos);
            }
            foreach (var areaId in new[] { "winch_pad", "pier", "dock" })
            {
                var ar = Array.Find(layout.tiers, a => a.id == areaId);
                if (ar != null && ar.x != null && ar.x.Length > 0)
                {
                    float sx = 0f, sz = 0f;
                    for (int k = 0; k < ar.x.Length; k++) { sx += ar.x[k]; sz += ar.z[k]; }
                    streetscapeBlockers.Add(new Vector3(sx / ar.x.Length, ar.y, sz / ar.x.Length));
                }
            }
        }

        // 3. 喷泉占位：中央广场。F1 月亮喷泉模型到了再换；碰撞用 TownObstacle 盒子，人不能穿过去
        if (layout.points != null)
            foreach (var pt in layout.points)
            {
                if (pt.id != "FOUNTAIN") continue;
                BuildFountainPlaceholder(root, new Vector3(pt.x, GroundBelow(pt.x, pt.z, pt.y + 0.5f), pt.z));
                streetscapeBlockers.Add(new Vector3(pt.x, pt.y, pt.z));
            }
        // 4. 集市摊位占位（M1 模型到了再换）：中央大道两边各两个，面朝大道，摆在大道边线外侧，不占路
        BuildMarketStalls(root);
        // 5. 广场中心：大道上的星星罗盘、三个住宅组团小广场（左：一棵树，中：月亮雕塑，右：星星地砖）
        BuildCenterpieces(root);
        Debug.Log($"STREETSCAPE fences={fences} lamps={lamps} blockers={streetscapeBlockers.Count}");
    }

    // ---------- 路灯发光（2026-11-12 美术改版） ----------
    // 2026-11-16：原来取 58%–88% 高度所有顶点的平均，混进了右边竖直灯杆和鹅颈弯，光球偏到杆子和灯笼之间。
    // 现在只认吊挂灯笼本身：先找琥珀玻璃那块（S4 里叫 "Warm amber lantern"、材质 "Honey amber glass"），取它的包围盒中心；
    // 找不到就退回"55%–84% 高度、离灯杆轴心（底座 10%–40% 高度那段的中位）超过最大悬臂 35% 的点簇"的包围盒中心。
    static Vector3 LampLanternCenter(GameObject model)
    {
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            bool glass = r.name.IndexOf("lantern", StringComparison.OrdinalIgnoreCase) >= 0 && r.name.IndexOf("amber", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0) glass = true;
            if (glass) return r.bounds.center;
        }
        var b = ModelBounds(model);
        var pts = new List<Vector3>();
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var t = mf.transform;
            foreach (var v in mf.sharedMesh.vertices) pts.Add(t.TransformPoint(v));
        }
        float K(Vector3 w) => (w.y - b.min.y) / Mathf.Max(0.01f, b.size.y);
        var low = pts.Where(w => K(w) > 0.1f && K(w) < 0.4f).ToList();
        if (low.Count == 0) return new Vector3(b.center.x, b.min.y + b.size.y * 0.68f, b.center.z);
        var xs = low.Select(w => w.x).OrderBy(x => x).ToList(); var zs = low.Select(w => w.z).OrderBy(z => z).ToList();
        var axis = new Vector2(xs[xs.Count / 2], zs[zs.Count / 2]);
        float reach = pts.Max(w => Vector2.Distance(new Vector2(w.x, w.z), axis));
        var head = pts.Where(w => K(w) > 0.55f && K(w) < 0.84f && Vector2.Distance(new Vector2(w.x, w.z), axis) > reach * 0.35f).ToList();
        if (head.Count == 0) return new Vector3(b.center.x, b.min.y + b.size.y * 0.68f, b.center.z);
        var hb = new Bounds(head[0], Vector3.zero); foreach (var w in head) hb.Encapsulate(w);
        return hb.center;
    }

    // 把整盏灯绕灯座转到"灯笼朝 toward 那边挑出去"（toward 只看水平方向）
    static void TurnLampArm(Transform lamp, Vector3 toward)
    {
        toward.y = 0f; if (toward.sqrMagnitude < 1e-6f) return;
        var glow = lamp.Find("LampGlow"); if (glow == null) return;
        var arm = glow.position - lamp.position; arm.y = 0f; if (arm.sqrMagnitude < 1e-6f) return;
        float ang = Vector3.SignedAngle(arm, toward, Vector3.up);
        lamp.RotateAround(lamp.position, Vector3.up, ang);
    }

    // 灯杆轴心（底座 10%–40% 高度那段顶点的水平中位）相对灯根节点的水平偏移。模型是按包围盒中心落位的，
    // 灯杆比根节点往"悬臂反方向"偏 0.2 m 左右，按根节点算净距会把底座压进栏杆。
    static Vector3 LampPoleOffset(Transform lamp)
    {
        var pts = new List<Vector3>();
        foreach (var mf in lamp.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponentInParent<LampGlow>() != null) continue;
            var t = mf.transform; foreach (var v in mf.sharedMesh.vertices) pts.Add(t.TransformPoint(v));
        }
        if (pts.Count == 0) return Vector3.zero;
        float y0 = pts.Min(p => p.y), y1 = pts.Max(p => p.y);
        var low = pts.Where(p => p.y > y0 + (y1 - y0) * 0.1f && p.y < y0 + (y1 - y0) * 0.4f).ToList();
        if (low.Count == 0) return Vector3.zero;
        var xs = low.Select(p => p.x).OrderBy(x => x).ToList(); var zs = low.Select(p => p.z).OrderBy(z => z).ToList();
        return new Vector3(xs[xs.Count / 2] - lamp.position.x, 0f, zs[zs.Count / 2] - lamp.position.z);
    }

    // 2026-11-16：路灯立在护栏内侧：灯笼朝 inward 挑出去，灯杆中心离护栏线 railPoint 往里 poleClear 米，底座落在下面的铺装上。
    static bool PlaceLampInsideRail(Transform lamp, Vector3 railPoint, Vector3 inward, float poleClear)
    {
        inward.y = 0f; inward.Normalize();
        TurnLampArm(lamp, inward);
        var pole = railPoint + inward * poleClear;
        var root = pole - LampPoleOffset(lamp);
        Physics.SyncTransforms();
        float top = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(new Vector3(pole.x, railPoint.y + 1.5f, pole.z), Vector3.down, 3.5f, 1 << 6, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<FloatingCrate>() == null) top = Mathf.Max(top, hit.point.y);
        if (float.IsNegativeInfinity(top)) return false;
        lamp.position = new Vector3(root.x, top + 0.01f, root.z);
        return true;
    }

    static void AddLampGlow(Transform lamp, GameObject model)
    {
        Vector3 pos = LampLanternCenter(model);

        string path = MatRoot + "/LampHalo.mat";
        var shader = Shader.Find("TideTown/Glow");
        if (shader == null) throw new Exception("找不到 TideTown/Glow 着色器（Assets/Resources/TideGlow.shader）");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else if (mat.shader != shader) mat.shader = shader;
        mat.SetColor("_Color", new Color(1.9f, 1.15f, 0.48f, 1f));
        mat.SetFloat("_Size", 1.9f);
        mat.SetFloat("_Core", 2.6f);
        EditorUtility.SetDirty(mat);

        var go = new GameObject("LampGlow");
        go.transform.SetParent(lamp, false);
        go.transform.position = pos;
        var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        UnityEngine.Object.DestroyImmediate(halo.GetComponent<Collider>());
        halo.name = "halo";
        halo.transform.SetParent(go.transform, false);
        halo.transform.localRotation = Quaternion.Euler(35f, 45f, 0f);   // 面片只用来给裁剪算包围盒，朝向由着色器定
        halo.transform.localScale = Vector3.one * 1.9f;
        var hr = halo.GetComponent<Renderer>();
        hr.sharedMaterial = mat;
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hr.receiveShadows = false;
        var lightGo = new GameObject("light");
        lightGo.transform.SetParent(go.transform, false);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color32(0xff, 0xc2, 0x78, 255);
        l.range = 7.5f;
        l.intensity = 3.2f;
        l.shadows = LightShadows.None;
        l.enabled = false;   // LampGlowDirector 只开离镜头最近的几盏
        var glow = go.AddComponent<LampGlow>();
        glow.lamp = l;
        glow.halo = hr;
    }

    // ---------- 集市摊位占位 ----------

    static Material StripeMat(string name, Color32 a, Color32 b)
    {
        string texPath = TexRoot + "/" + name + ".png";
        if (!System.IO.File.Exists(texPath))
        {
            var px = new Color32[64 * 8];
            for (int y = 0; y < 8; y++) for (int x = 0; x < 64; x++) px[y * 64 + x] = (x / 8) % 2 == 0 ? a : b;
            var t = new Texture2D(64, 8, TextureFormat.RGBA32, false); t.SetPixels32(px); t.Apply();
            System.IO.File.WriteAllBytes(texPath, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Repeat; imp.SaveAndReimport();
        }
        var m = FlatMat(name, new Color32(255, 255, 255, 255), 0.15f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildMarketStalls(Transform parent)
    {
        var wood = FlatMat("StallWood", new Color32(0x9a, 0x68, 0x40, 255), 0.08f);
        var red = StripeMat("StallStripeRed", new Color32(0xe0, 0x4a, 0x3c, 255), new Color32(0xfa, 0xf2, 0xe4, 255));
        var blue = StripeMat("StallStripeBlue", new Color32(0x4a, 0x7c, 0xc8, 255), new Color32(0xfa, 0xf2, 0xe4, 255));
        Material[] fruit = {
            FlatMat("FruitOrange", new Color32(0xf0, 0x90, 0x2c, 255), 0.35f), FlatMat("FruitRed", new Color32(0xd8, 0x34, 0x2c, 255), 0.35f),
            FlatMat("FruitYellow", new Color32(0xf2, 0xd0, 0x4a, 255), 0.35f), FlatMat("FruitGreen", new Color32(0x8c, 0xbe, 0x3c, 255), 0.35f) };
        // 图上位置：大道西边线外 (790, 470/530)，东边线外 (946, 470/530)；朝向大道
        // 2026-11-08：原 790/946 正好压在大道边线上，剧情审计卡在摊子东面；各往外挪 18 px（约 0.8 m）
        // 北侧两座摊位取 v=472，与 B7_market_r2 南墙留出宽敞草坪通道
        var spots = new[] { (772f, 535f, 90f, red), (772f, 472f, 90f, blue), (964f, 535f, -90f, red), (964f, 472f, -90f, blue) };
        var rnd = new Lcg(31);
        int n = 0;
        foreach (var (u, v, yaw, awning) in spots)
        {
            var c = Unproject(u, v, 3.0f);
            float gy = GroundBelow(c.x, c.z, 3.5f);
            var go = new GameObject("MarketStall_" + n++);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(c.x, gy, c.z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            void Box(string name, Vector3 pos, Vector3 scale, Material mat, PrimitiveType type = PrimitiveType.Cube)
            {
                var p = GameObject.CreatePrimitive(type);
                p.name = name;
                UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = pos; p.transform.localScale = scale;
                p.GetComponent<Renderer>().sharedMaterial = mat;
                GameObjectUtility.SetStaticEditorFlags(p, StaticEditorFlags.BatchingStatic);
            }
            // 局部坐标：+z 朝大道。摊台 2.2 × 1.1，台面 0.8 m；四根柱子 2.2 m；条纹遮阳棚往大道一侧斜下
            Box("counter", new Vector3(0, 0.4f, 0), new Vector3(2.2f, 0.8f, 1.1f), wood);
            foreach (var (x, z) in new[] { (-1.05f, -0.5f), (1.05f, -0.5f), (-1.05f, 0.5f), (1.05f, 0.5f) })
                Box("post", new Vector3(x, 1.1f, z), new Vector3(0.08f, 2.2f, 0.08f), wood);
            var aw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            aw.name = "awning";
            UnityEngine.Object.DestroyImmediate(aw.GetComponent<Collider>());
            aw.transform.SetParent(go.transform, false);
            aw.transform.localPosition = new Vector3(0, 2.15f, 0.25f);
            aw.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            aw.transform.localScale = new Vector3(2.5f, 0.06f, 1.7f);
            aw.GetComponent<Renderer>().sharedMaterial = awning;
            for (int k = 0; k < 14; k++)
            {
                float fx = -0.9f + (k % 7) * 0.3f + (rnd.Next01() - 0.5f) * 0.08f, fz = (k < 7 ? -0.2f : 0.22f);
                float r = 0.16f + rnd.Next01() * 0.06f;
                Box("fruit", new Vector3(fx, 0.82f + r * 0.5f, fz), Vector3.one * r, fruit[k % fruit.Length], PrimitiveType.Sphere);
            }
            // 碰撞：摊台占的那一块（TownObstacle 长方形，按世界轴对齐）
            var col = new GameObject("MarketStall_collision");
            col.transform.SetParent(go.transform, false);
            col.layer = 7;
            var bc = col.AddComponent<BoxCollider>();
            bool sideways = Mathf.Abs(Mathf.Abs(yaw) - 90f) < 1f;
            col.transform.rotation = Quaternion.identity;
            col.transform.position = new Vector3(c.x, gy + 0.6f, c.z);
            bc.size = sideways ? new Vector3(1.2f, 1.2f, 2.3f) : new Vector3(2.3f, 1.2f, 1.2f);
            col.AddComponent<TownObstacle>();
            footprints.Add(new Footprint { instance = go.name, x = c.x, z = c.z, w = bc.size.x, d = bc.size.z });
        }
        Debug.Log("MARKET stalls=" + n);
    }

    // ---------- 广场中心装饰 ----------

    // 五角星平面网格（贴地 2 cm），r 外半径
    static Mesh StarMesh(float r)
    {
        var v = new List<Vector3> { Vector3.zero }; var t = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float rr = i % 2 == 0 ? r : r * 0.42f;
            v.Add(new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr));
        }
        for (int i = 0; i < 10; i++) { t.Add(0); t.Add(1 + (i + 1) % 10); t.Add(1 + i); }
        var m = new Mesh { name = "Star_" + r };
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, NatureMeshRoot + "/Star_" + Mathf.RoundToInt(r * 100) + ".asset");
    }

    static void BuildCenterpieces(Transform parent)
    {
        var gold = FlatMat("FountainGold", new Color32(0xe8, 0xb4, 0x48, 255), 0.6f);
        var ring = FlatMat("MosaicRing", new Color32(0xc9, 0xa4, 0x7c, 255), 0.2f);
        var stone = FlatMat("FountainStone", new Color32(0xec, 0xdf, 0xc8, 255), 0.15f);
        void Flat(string name, Mesh mesh, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        void Disc(string name, Vector3 pos, float r, Material mat)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            d.name = name; UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
            d.transform.SetParent(parent, false);
            d.transform.position = pos; d.transform.localScale = new Vector3(r * 2f, 0.01f, r * 2f);
            d.GetComponent<Renderer>().sharedMaterial = mat;
        }
        // 大道上的星星罗盘（图上 868,465）
        var a = Unproject(868, 465, 3.0f); float ay = GroundBelow(a.x, a.z, 3.5f);
        Disc("Compass_ring", new Vector3(a.x, ay + 0.012f, a.z), 1.5f, ring);
        Flat("Compass_star", StarMesh(1.25f), new Vector3(a.x, ay + 0.03f, a.z), gold);
        // 右组团小广场：星星地砖
        var r = Unproject(1222, 228, 5.5f); float ry = GroundBelow(r.x, r.z, 6f);
        Disc("StarPlaza_ring", new Vector3(r.x, ry + 0.012f, r.z), 1.6f, ring);
        Flat("StarPlaza_star", StarMesh(1.35f), new Vector3(r.x, ry + 0.03f, r.z), gold);
        // 中组团小广场：月亮雕塑（石座 + 金色弯月，弯月用一大一小两个球相减的样子：金球 + 前面挡一个石色小球）
        var m = Unproject(846, 178, 5.5f); float my = GroundBelow(m.x, m.z, 6f);
        var stat = new GameObject("MoonStatue"); stat.transform.SetParent(parent, false); stat.transform.position = new Vector3(m.x, my, m.z);
        void Part(PrimitiveType type, Vector3 lp, Vector3 s, Material mat)
        {
            var p = GameObject.CreatePrimitive(type); UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(stat.transform, false); p.transform.localPosition = lp; p.transform.localScale = s;
            p.GetComponent<Renderer>().sharedMaterial = mat;
        }
        // 2026-11-16 扩建 EXP-04：中院中心改成"新月日晷花坛"——十二边石砌花坛 + 一圈花 + 石座上立金色月牙日晷（原来是白圆柱 + 压扁金球）
        void MeshPart(string name, Mesh mesh, Vector3 lp, Material mat, Quaternion rot)
        {
            var p = new GameObject(name); p.transform.SetParent(stat.transform, false); p.transform.localPosition = lp; p.transform.localRotation = rot;
            p.AddComponent<MeshFilter>().sharedMesh = mesh; p.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        var soil = FlatMat("SundialSoil", new Color32(0x6b, 0x4a, 0x32, 255), 0.05f);
        var bedLeaf = FlatMat("FountainLeaf", new Color32(0x4f, 0x8f, 0x3c, 255), 0.1f);
        var bedBlooms = new[] { FlatMat("FountainBloomPink", new Color32(0xf0, 0x86, 0xb4, 255), 0.1f), FlatMat("FountainBloomLilac", new Color32(0xb4, 0x98, 0xec, 255), 0.1f), FlatMat("FountainBloomWhite", new Color32(0xfb, 0xf6, 0xea, 255), 0.1f), FlatMat("FountainBloomYellow", new Color32(0xf8, 0xd2, 0x58, 255), 0.1f) };
        MeshPart("bed_wall", SaveMesh(PrismRing("SundialBedWall", 12, 0.92f, 1.12f, 0f, 0.36f), NatureMeshRoot + "/SundialBedWall.asset"), Vector3.zero, stone, Quaternion.identity);
        MeshPart("bed_soil", SaveMesh(PrismRing("SundialBedSoil", 12, 0f, 0.93f, 0f, 0.30f), NatureMeshRoot + "/SundialBedSoil.asset"), Vector3.zero, soil, Quaternion.identity);
        for (int i = 0; i < 12; i++)
        {
            float ang = i * Mathf.PI / 6f; var lp = new Vector3(Mathf.Cos(ang) * 0.68f, 0.38f, Mathf.Sin(ang) * 0.68f);
            Part(PrimitiveType.Sphere, lp, new Vector3(0.34f, 0.2f, 0.34f), bedLeaf);
            Part(PrimitiveType.Sphere, lp + Vector3.up * 0.08f, new Vector3(0.2f, 0.12f, 0.2f), bedBlooms[i % bedBlooms.Length]);
        }
        Part(PrimitiveType.Cylinder, new Vector3(0, 0.55f, 0), new Vector3(0.34f, 0.25f, 0.34f), stone);
        MeshPart("sundial_dial", SaveMesh(PrismRing("SundialDial", 12, 0f, 0.36f, 0.80f, 0.86f), NatureMeshRoot + "/SundialDial.asset"), Vector3.zero, gold, Quaternion.identity);
        MeshPart("sundial_moon", CrescentMesh("SundialMoon", 0.5f, 0.41f, 0.22f, 0.08f), new Vector3(0, 1.38f, 0), gold, Quaternion.identity);
        var colGo = new GameObject("MoonStatue_collision"); colGo.transform.SetParent(stat.transform, false);
        colGo.transform.localPosition = new Vector3(0, 0.6f, 0); colGo.layer = 7;
        colGo.AddComponent<BoxCollider>().size = new Vector3(1.5f, 1.2f, 1.5f); colGo.AddComponent<TownObstacle>();
        footprints.Add(new Footprint { instance = "MoonStatue", x = m.x, z = m.z, w = 1.5f, d = 1.5f });
        // 左组团小广场：中间一棵大树（Nature 里种）——记下位置给 BuildNature
        plazaTreeSpots.Clear();
        var l = Unproject(530, 228, 5.5f); plazaTreeSpots.Add(new Vector3(l.x, GroundBelow(l.x, l.z, 6f), l.z));
        // 2026-11-16 扩建 EXP-04：左院（西院）中心大树脚下补一圈八角星地面镶嵌（中间留出树干的圆孔），和东院的星纹地砖呼应
        float ly = GroundBelow(l.x, l.z, 6f);
        Disc("WestStar_ring", new Vector3(l.x, ly + 0.010f, l.z), 2.25f, ring);
        Flat("WestStar_star", OctaStarMesh("WestStarInlay", 2.0f, 0.55f), new Vector3(l.x, ly + 0.032f, l.z), gold);
        Debug.Log("CENTERPIECES compass+star+moon, plazaTree=" + plazaTreeSpots.Count);
    }

    static readonly List<Vector3> plazaTreeSpots = new List<Vector3>();

    // 一根立柱或一道横杆：底面中心 center（横杆是中心），尺寸 size（x 厚、y 高、z 长），绕 Y 转 yaw 度
    static void AddFenceBox(List<Vector3> v, List<int> t, Vector3 center, Vector3 size, float yaw)
    {
        var rot = Quaternion.Euler(0f, yaw, 0f);
        bool post = yaw == 0f && size.y > 0.5f;
        var c = post ? center + Vector3.up * size.y * 0.5f : center;
        var h = size * 0.5f;
        Vector3 P(float x, float y, float z) => c + rot * new Vector3(x * h.x, y * h.y, z * h.z);
        // 6 个面各 4 个顶点（平直法线），每个面从外面看是顺时针
        var corners = new[]
        {
            new[] { P(-1,-1, 1), P( 1,-1, 1), P( 1, 1, 1), P(-1, 1, 1) },
            new[] { P( 1,-1,-1), P(-1,-1,-1), P(-1, 1,-1), P( 1, 1,-1) },
            new[] { P( 1,-1, 1), P( 1,-1,-1), P( 1, 1,-1), P( 1, 1, 1) },
            new[] { P(-1,-1,-1), P(-1,-1, 1), P(-1, 1, 1), P(-1, 1,-1) },
            new[] { P(-1, 1, 1), P( 1, 1, 1), P( 1, 1,-1), P(-1, 1,-1) },
            new[] { P(-1,-1,-1), P( 1,-1,-1), P( 1,-1, 1), P(-1,-1, 1) },
        };
        foreach (var f in corners)
        {
            int o = v.Count;
            v.AddRange(f);
            t.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
        }
    }

    // 2026-11-16 扩建 EXP-04：中央双层石砌新月喷泉（原来是白圆柱 + 一个压扁的金球）。
    // 名字仍叫 Fountain_placeholder（ShadowSoundPuzzles、地图校准按这个名字找）；外径不变（1.6 m），轮廓碰撞照旧按池沿生成。
    static void BuildFountainPlaceholder(Transform parent, Vector3 basePos)
    {
        var stone = FlatMat("FountainStone", new Color32(0xec, 0xdf, 0xc8, 255), 0.15f);
        var stoneDark = FlatMat("FountainStoneDark", new Color32(0xc9, 0xb3, 0x92, 255), 0.12f);
        var water = FlatMat("FountainWater", new Color32(0x5a, 0xc0, 0xd6, 255), 0.92f);
        var gold = FlatMat("FountainGold", new Color32(0xe8, 0xb4, 0x48, 255), 0.6f);
        gold.SetFloat("_Metallic", 0.7f);
        var clay = FlatMat("FountainPotClay", new Color32(0xc2, 0x6e, 0x46, 255), 0.1f);
        var blooms = new[] { FlatMat("FountainBloomPink", new Color32(0xf0, 0x86, 0xb4, 255), 0.1f), FlatMat("FountainBloomLilac", new Color32(0xb4, 0x98, 0xec, 255), 0.1f), FlatMat("FountainBloomWhite", new Color32(0xfb, 0xf6, 0xea, 255), 0.1f) };
        var leaf = FlatMat("FountainLeaf", new Color32(0x4f, 0x8f, 0x3c, 255), 0.1f);
        var go = new GameObject("Fountain_placeholder");
        go.transform.SetParent(parent, false);
        // 第四轮：池子半径 1.9 → 1.6、往南挪 0.3 m。原来池子北沿离北坡道口不到 1 m，夹着住宅台地的墙角只剩一条缝
        basePos.z -= 0.3f;
        go.transform.position = basePos;
        void Mesh(string name, Mesh mesh, Vector3 local, Material mat)
        {
            var p = new GameObject(name);
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = local;
            p.AddComponent<MeshFilter>().sharedMesh = mesh;
            p.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        void Part(PrimitiveType type, string name, Vector3 local, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = local;
            p.transform.localRotation = rot ?? Quaternion.identity;
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = mat;
        }
        // 下层：八角石砌池（池壁 + 压顶）、水面、池底基座
        Mesh("basin_plinth", SaveMesh(PrismRing("FountainPlinth", 8, 0f, 1.62f, 0f, 0.08f), NatureMeshRoot + "/FountainPlinth.asset"), Vector3.zero, stoneDark);
        Mesh("basin_wall", SaveMesh(PrismRing("FountainBasinWall", 8, 1.30f, 1.52f, 0.08f, 0.52f), NatureMeshRoot + "/FountainBasinWall.asset"), Vector3.zero, stone);
        Mesh("basin_cap", SaveMesh(PrismRing("FountainBasinCap", 8, 1.24f, 1.60f, 0.52f, 0.60f), NatureMeshRoot + "/FountainBasinCap.asset"), Vector3.zero, stoneDark);
        Part(PrimitiveType.Cylinder, "water", new Vector3(0, 0.44f, 0), new Vector3(2.62f, 0.02f, 2.62f), water);
        // 中柱 + 上层托盘（也做成八角）
        Part(PrimitiveType.Cylinder, "pillar", new Vector3(0, 0.95f, 0), new Vector3(0.42f, 0.45f, 0.42f), stone);
        Mesh("bowl", SaveMesh(PrismRing("FountainBowl", 8, 0.0f, 0.82f, 1.36f, 1.50f), NatureMeshRoot + "/FountainBowl.asset"), Vector3.zero, stone);
        Mesh("bowl_rim", SaveMesh(PrismRing("FountainBowlRim", 8, 0.70f, 0.86f, 1.50f, 1.60f), NatureMeshRoot + "/FountainBowlRim.asset"), Vector3.zero, stoneDark);
        Part(PrimitiveType.Cylinder, "bowl_water", new Vector3(0, 1.555f, 0), new Vector3(1.40f, 0.01f, 1.40f), water);
        Part(PrimitiveType.Cylinder, "stem", new Vector3(0, 1.85f, 0), new Vector3(0.2f, 0.3f, 0.2f), stone);
        Part(PrimitiveType.Sphere, "stem_ball", new Vector3(0, 2.18f, 0), new Vector3(0.26f, 0.26f, 0.26f), gold);
        // 顶上金色新月（真正的月牙形，挤出 9 cm 厚），开口朝北坡道
        Mesh("moon", CrescentMesh("FountainMoon", 0.52f, 0.43f, 0.24f, 0.09f), new Vector3(0, 2.72f, 0), gold);
        // 池沿一圈小花盆
        for (int i = 0; i < 8; i++)
        {
            float a = (i + 0.5f) * Mathf.PI / 4f;
            var p = new Vector3(Mathf.Cos(a) * 1.42f, 0.6f, Mathf.Sin(a) * 1.42f);
            Part(PrimitiveType.Cylinder, "pot", p + Vector3.up * 0.09f, new Vector3(0.22f, 0.09f, 0.22f), clay);
            Part(PrimitiveType.Sphere, "pot_leaf", p + Vector3.up * 0.24f, new Vector3(0.26f, 0.16f, 0.26f), leaf);
            Part(PrimitiveType.Sphere, "pot_bloom", p + Vector3.up * 0.30f, new Vector3(0.16f, 0.1f, 0.16f), blooms[i % blooms.Length]);
        }
        // 水花：托盘上方往上喷、落回下层池子
        var spray = new GameObject("fountain_spray");
        spray.transform.SetParent(go.transform, false);
        spray.transform.localPosition = new Vector3(0, 1.6f, 0);
        spray.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = spray.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.9f; main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.2f); main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.gravityModifier = 0.55f; main.maxParticles = 160; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.82f, 0.95f, 1f, 0.75f);
        var em = ps.emission; em.rateOverTime = 70f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 16f; sh.radius = 0.05f;
        var rend = spray.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = SprayMaterial();
        // 碰撞由 BuildContourCollision 按池子外沿统一生成（原来是 3.6 m 方块，四个角是空气墙）
        Debug.Log($"FOUNTAIN crescent at ({basePos.x:F2},{basePos.y:F2},{basePos.z:F2})");
    }

    static Material SprayMaterial()
    {
        string path = MatRoot + "/FountainSpray.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        // 圆形软边的小水珠贴图（没有贴图时粒子是一个个方块）
        string texPath = TexRoot + "/FountainDrop.png";
        if (!System.IO.File.Exists(texPath))
        {
            const int N = 32; var px = new Color[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float d = new Vector2(x - N / 2f + .5f, y - N / 2f + .5f).magnitude / (N / 2f); px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(1f - d * d) ); }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false); tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath); imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetColor("_BaseColor", new Color(0.85f, 0.96f, 1f, 0.7f));
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(m);
        return m;
    }

    // 正 n 边形环柱：内半径 rIn（0 = 实心）到外半径 rOut，高 y0–y1。每个面单独顶点，平直法线。
    static Mesh PrismRing(string name, int n, float rIn, float rOut, float y0, float y1)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        Vector3 P(float r, int i, float y) { float a = (i + 0.5f) * Mathf.PI * 2f / n; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int o = v.Count; v.AddRange(new[] { a, b, c, d }); t.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 }); }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Q(P(rOut, i, y0), P(rOut, i, y1), P(rOut, j, y1), P(rOut, j, y0));                 // 外壁
            if (rIn > 0.001f)
            {
                Q(P(rIn, j, y0), P(rIn, j, y1), P(rIn, i, y1), P(rIn, i, y0));                 // 内壁
                Q(P(rIn, i, y1), P(rIn, j, y1), P(rOut, j, y1), P(rOut, i, y1));               // 顶面
            }
            else
            {
                int o = v.Count; v.AddRange(new[] { new Vector3(0, y1, 0), P(rOut, j, y1), P(rOut, i, y1) }); t.AddRange(new[] { o, o + 1, o + 2 });
                o = v.Count; v.AddRange(new[] { new Vector3(0, y0, 0), P(rOut, i, y0), P(rOut, j, y0) }); t.AddRange(new[] { o, o + 1, o + 2 });
            }
        }
        DoubleSided(v, t);
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 背面另复制一套顶点再反着连一遍：两面都看得见，法线各管各的（共用顶点会把法线平均成 0 → NaN 黑点）
    static void DoubleSided(List<Vector3> v, List<int> t)
    {
        int n = v.Count, m = t.Count;
        for (int i = 0; i < n; i++) v.Add(v[i]);
        for (int i = 0; i < m; i += 3) { t.Add(t[i] + n); t.Add(t[i + 2] + n); t.Add(t[i + 1] + n); }
    }

    // 竖直的月牙：外圆半径 R、圆心原点；挖掉圆心在 (dx,0)、半径 r 的内圆，开口朝 +x。沿 z 挤出 depth 厚。
    static Mesh CrescentMesh(string name, float R, float r, float dx, float depth)
    {
        float ix = (R * R - r * r + dx * dx) / (2f * dx), iy = Mathf.Sqrt(Mathf.Max(0f, R * R - ix * ix));
        float th0 = Mathf.Atan2(iy, ix), ph0 = Mathf.Atan2(iy, ix - dx);
        var outline = new List<Vector2>();
        const int N = 28;
        for (int i = 0; i <= N; i++) { float a = Mathf.Lerp(th0, 2f * Mathf.PI - th0, i / (float)N); outline.Add(new Vector2(Mathf.Cos(a) * R, Mathf.Sin(a) * R)); }
        for (int i = 1; i < N; i++) { float a = Mathf.Lerp(2f * Mathf.PI - ph0, ph0, i / (float)N); outline.Add(new Vector2(dx + Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
        var tris = Triangulate(outline);
        var v = new List<Vector3>(); var t = new List<int>();
        float h = depth * 0.5f;
        int f0 = v.Count; foreach (var p in outline) v.Add(new Vector3(p.x, p.y, -h));
        int b0 = v.Count; foreach (var p in outline) v.Add(new Vector3(p.x, p.y, h));
        bool ccw = SignedArea(outline) > 0f;
        foreach (var tr in tris)
        {
            if (ccw) { t.AddRange(new[] { f0 + tr.Item1, f0 + tr.Item3, f0 + tr.Item2 }); t.AddRange(new[] { b0 + tr.Item1, b0 + tr.Item2, b0 + tr.Item3 }); }
            else { t.AddRange(new[] { f0 + tr.Item1, f0 + tr.Item2, f0 + tr.Item3 }); t.AddRange(new[] { b0 + tr.Item1, b0 + tr.Item3, b0 + tr.Item2 }); }
        }
        for (int i = 0; i < outline.Count; i++)
        {
            int j = (i + 1) % outline.Count; int o = v.Count;
            var a = outline[i]; var b = outline[j];
            v.AddRange(new[] { new Vector3(a.x, a.y, -h), new Vector3(b.x, b.y, -h), new Vector3(b.x, b.y, h), new Vector3(a.x, a.y, h) });
            if (ccw) t.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 }); else t.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
        }
        DoubleSided(v, t);
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, NatureMeshRoot + "/" + name + ".asset");
    }

    // 八角星地面镶嵌：外半径 r、凹角半径 r*0.55，中间可留 hole 半径的圆孔（给树干）。贴地。
    static Mesh OctaStarMesh(string name, float r, float hole)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        const int pts = 8;
        for (int i = 0; i < pts * 2; i++)
        {
            float a = i * Mathf.PI / pts, a1 = (i + 1) * Mathf.PI / pts;
            float ro = i % 2 == 0 ? r : r * 0.55f, ro1 = (i + 1) % 2 == 0 ? r : r * 0.55f;
            var o0 = new Vector3(Mathf.Cos(a) * ro, 0, Mathf.Sin(a) * ro); var o1 = new Vector3(Mathf.Cos(a1) * ro1, 0, Mathf.Sin(a1) * ro1);
            var i0 = new Vector3(Mathf.Cos(a) * hole, 0, Mathf.Sin(a) * hole); var i1 = new Vector3(Mathf.Cos(a1) * hole, 0, Mathf.Sin(a1) * hole);
            int o = v.Count; v.AddRange(new[] { i0, o0, o1, i1 }); t.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
        }
        DoubleSided(v, t);
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, NatureMeshRoot + "/" + name + ".asset");
    }
}
