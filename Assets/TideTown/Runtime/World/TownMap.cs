// WP-16：地图与 3D 地面引路微光。
// 1. 按 M 打开地图：显示全镇地标名称（邮局、面包店、学校、操场、狗窝、船棚、灯塔、山顶新邮局、渡口绞盘、广场八音盒）、地标防重叠排布、当前目标高亮与沿路引导虚线；
// 2. 平时漫游时：移除遮挡视线的 2D 飘动标签，改用贴地的「沿可走路网折线的 3D 金色引路光点流 + 目标物暖金光环」，自然引导玩家脚下路线与互动目标。
using System;
using System.Collections.Generic;
using UnityEngine;

public class TownMap : MonoBehaviour
{
    [Serializable] class MapCam { public string name; public float[] pos, euler; public float fov; public int width, height; }
    [Serializable] class MapCameraList { public MapCam[] cameras; }
    [Serializable] class RouteNodeJson { public string id; public bool stop; public float x, z; }
    [Serializable] class RouteFileJson { public RouteNodeJson[] route; }

    struct LandmarkInfo
    {
        public string name;
        public string sub;
        public Vector3 worldPos;
        public string stampId;
        public Color badgeColor;

        public LandmarkInfo(string n, string s, Vector3 p, string st, Color c)
        {
            name = n; sub = s; worldPos = p; stampId = st; badgeColor = c;
        }
    }

    static readonly LandmarkInfo[] Landmarks =
    {
        new LandmarkInfo("山口邮筒", "起点", new Vector3(-31.04f, 3.2f, -6.76f), "", new Color(0.75f, 0.32f, 0.25f, 0.92f)),
        new LandmarkInfo("旧邮局", "老周的值班室", new Vector3(-22.50f, 5.2f, -9.51f), "", new Color(0.32f, 0.45f, 0.58f, 0.92f)),
        new LandmarkInfo("① 林阿婆面包店", "第一站 · 烤炉与柴火", new Vector3(-14.40f, 5.2f, -8.50f), "bakery", new Color(0.76f, 0.45f, 0.20f, 0.94f)),
        new LandmarkInfo("♪ 广场八音盒", "心愿展台 · 声影机关", new Vector3(-11.90f, 3.3f, -5.80f), "", new Color(0.58f, 0.42f, 0.24f, 0.90f)),
        new LandmarkInfo("② 潮汐镇学校", "第二站 · 阿萤与小黑", new Vector3(13.53f, 5.6f, -2.02f), "school", new Color(0.26f, 0.56f, 0.42f, 0.94f)),
        new LandmarkInfo("学校操场", "秋千 / 红球 / 滑梯", new Vector3(17.50f, 3.8f, -5.60f), "", new Color(0.30f, 0.52f, 0.36f, 0.88f)),
        new LandmarkInfo("学校后院 · 狗窝", "拖鞋 / 狗碗", new Vector3(21.76f, 4.1f, 1.95f), "", new Color(0.48f, 0.38f, 0.28f, 0.88f)),
        new LandmarkInfo("⚙ 渡口齿轮绞盘", "涨潮过海机关", new Vector3(11.60f, 1.8f, -11.40f), "", new Color(0.62f, 0.48f, 0.20f, 0.90f)),
        new LandmarkInfo("海边船棚 & 码头", "拿煤油壶", new Vector3(18.12f, 3.2f, -15.56f), "", new Color(0.25f, 0.48f, 0.64f, 0.92f)),
        new LandmarkInfo("③ 海角灯塔", "第三站 · 点亮灯头", new Vector3(34.22f, 8.5f, 8.75f), "lighthouse", new Color(0.78f, 0.56f, 0.18f, 0.94f)),
        new LandmarkInfo("山顶新邮局", "终点 · 记忆寄件台", new Vector3(-27.54f, 7.6f, 14.89f), "", new Color(0.55f, 0.36f, 0.62f, 0.92f)),
    };

    EggboyPlayer player;
    EggboyUI ui;
    StoryDirector director;
    ControlGate gate;
    IslandWorld island;
    Texture2D preview;
    object mapLock;

    bool hasCam;
    Vector3 camPos, camFwd, camUp;
    float focal;
    int imgW = 1774, imgH = 887;

    // 3D 地面引路光点与目标物暖金光球（不挡视线的纯 3D 地面引导）
    const int GuideDotCount = 9;
    GameObject guideRoot;
    Transform[] guideDots;
    Renderer[] guideDotRenderers;
    Transform targetBeaconRoot;
    Transform targetOrb;
    Transform targetRing;
    Light targetLight;
    Material guideBaseMat;

    // 路网寻路折线（用于 3D 地面引路光点与 2D 地图引导虚线，避免穿墙入海）
    bool navLoaded;
    readonly List<Vector2> navNodes = new List<Vector2>();
    readonly List<List<int>> navAdj = new List<List<int>>();
    readonly List<Vector3> guidePath = new List<Vector3>();

    public bool IsOpen { get; private set; }
    public float ClosedAt { get; private set; }

    public void Init(EggboyPlayer p, EggboyUI u, StoryDirector d, ControlGate g)
    {
        player = p;
        ui = u;
        director = d;
        gate = g;
        island = FindObjectOfType<IslandWorld>();
        preview = Resources.Load<Texture2D>("UI/town_preview");
        if (preview != null) { imgW = preview.width; imgH = preview.height; }
        LoadCamera();
        EnsureNavGraph();
    }

    void LoadCamera()
    {
        var asset = Resources.Load<TextAsset>("World/world_layout");
        if (asset == null) return;
        var list = JsonUtility.FromJson<MapCameraList>(asset.text);
        if (list == null || list.cameras == null) return;
        foreach (var c in list.cameras)
        {
            if (c.name != "preview" || c.pos == null || c.euler == null || c.pos.Length < 3 || c.euler.Length < 3) continue;
            camPos = new Vector3(c.pos[0], c.pos[1], c.pos[2]);
            float pitch = c.euler[0] * Mathf.Deg2Rad;
            camFwd = new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));
            camUp = new Vector3(0f, Mathf.Cos(pitch), Mathf.Sin(pitch));
            focal = (c.height * 0.5f) / Mathf.Tan(c.fov * Mathf.Deg2Rad * 0.5f);
            if (c.width > 0) imgW = c.width;
            if (c.height > 0) imgH = c.height;
            hasCam = true;
            break;
        }
    }

    void EnsureNavGraph()
    {
        if (navLoaded) return;
        navLoaded = true;
        navNodes.Clear();
        navAdj.Clear();
        var asset = Resources.Load<TextAsset>("World/route");
        if (asset == null) return;
        var file = JsonUtility.FromJson<RouteFileJson>(asset.text);
        if (file == null || file.route == null || file.route.Length == 0) return;

        int AddOrFindNode(Vector2 pt)
        {
            for (int i = 0; i < navNodes.Count; i++)
                if ((navNodes[i] - pt).sqrMagnitude < 0.20f * 0.20f) return i;
            int id = navNodes.Count;
            navNodes.Add(pt);
            navAdj.Add(new List<int>());
            return id;
        }

        void Link(int a, int b)
        {
            if (a == b || a < 0 || b < 0) return;
            if (!navAdj[a].Contains(b)) navAdj[a].Add(b);
            if (!navAdj[b].Contains(a)) navAdj[b].Add(a);
        }

        int prev = -1;
        for (int i = 0; i < file.route.Length; i++)
        {
            var r = file.route[i];
            int cur = AddOrFindNode(new Vector2(r.x, r.z));
            if (prev >= 0 && (navNodes[cur] - navNodes[prev]).sqrMagnitude < 16f * 16f)
                Link(prev, cur);
            prev = cur;
        }

        // 相邻路网节点若在 4.2m 内且中点在可走区内，也建立短边以支持岔路就近寻路
        for (int i = 0; i < navNodes.Count; i++)
        {
            for (int j = i + 1; j < navNodes.Count; j++)
            {
                float d2 = (navNodes[i] - navNodes[j]).sqrMagnitude;
                if (d2 > 4.2f * 4.2f) continue;
                Vector2 mid = (navNodes[i] + navNodes[j]) * 0.5f;
                if (island == null || island.InWalkArea(mid.x, mid.y))
                    Link(i, j);
            }
        }
    }

    void BuildGuidePolyline(Vector3 from, Vector3 to, List<Vector3> outPath)
    {
        outPath.Clear();
        outPath.Add(from);
        EnsureNavGraph();

        Vector2 a2 = new Vector2(from.x, from.z);
        Vector2 b2 = new Vector2(to.x, to.z);
        float directDist = Vector2.Distance(a2, b2);

        // 近距离且中间均在可走区，直接连线即可
        if (directDist <= 7.0f || navNodes.Count < 2)
        {
            bool clear = true;
            if (island != null && directDist > 1.5f)
            {
                for (int k = 1; k <= 4; k++)
                {
                    Vector2 m = Vector2.Lerp(a2, b2, k / 5f);
                    if (!island.InWalkArea(m.x, m.y)) { clear = false; break; }
                }
            }
            if (clear || navNodes.Count < 2)
            {
                outPath.Add(to);
                return;
            }
        }

        int startIdx = 0, endIdx = 0;
        float bestStart = float.MaxValue, bestEnd = float.MaxValue;
        for (int i = 0; i < navNodes.Count; i++)
        {
            float ds = (navNodes[i] - a2).sqrMagnitude;
            if (ds < bestStart) { bestStart = ds; startIdx = i; }
            float de = (navNodes[i] - b2).sqrMagnitude;
            if (de < bestEnd) { bestEnd = de; endIdx = i; }
        }

        if (startIdx == endIdx)
        {
            outPath.Add(new Vector3(navNodes[startIdx].x, from.y, navNodes[startIdx].y));
            outPath.Add(to);
            return;
        }

        int n = navNodes.Count;
        var dist = new float[n];
        var parent = new int[n];
        var used = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; parent[i] = -1; }
        dist[startIdx] = 0f;

        for (int step = 0; step < n; step++)
        {
            int u = -1;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (!used[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0 || u == endIdx) break;
            used[u] = true;
            var adj = navAdj[u];
            for (int k = 0; k < adj.Count; k++)
            {
                int v = adj[k];
                if (used[v]) continue;
                float w = Vector2.Distance(navNodes[u], navNodes[v]);
                if (dist[u] + w < dist[v])
                {
                    dist[v] = dist[u] + w;
                    parent[v] = u;
                }
            }
        }

        if (parent[endIdx] >= 0)
        {
            var chain = new List<int>();
            for (int cur = endIdx; cur >= 0; cur = parent[cur]) chain.Add(cur);
            chain.Reverse();

            // 若第一个路网点在玩家身后（玩家已走在该路段上），跳过第一个点避免引路光点往回指
            int firstIdx = 0;
            if (chain.Count >= 2)
            {
                Vector2 seg = navNodes[chain[1]] - navNodes[chain[0]];
                Vector2 toPlayer = a2 - navNodes[chain[0]];
                if (seg.sqrMagnitude > 0.01f && Vector2.Dot(toPlayer, seg) > 0f)
                    firstIdx = 1;
            }
            int lastIdx = chain.Count - 1;
            if (lastIdx - firstIdx >= 1)
            {
                Vector2 segEnd = navNodes[chain[lastIdx]] - navNodes[chain[lastIdx - 1]];
                Vector2 toTgt = b2 - navNodes[chain[lastIdx - 1]];
                if (segEnd.sqrMagnitude > 0.01f && Vector2.Dot(toTgt, segEnd) < segEnd.sqrMagnitude)
                    lastIdx--;
            }
            for (int i = firstIdx; i <= lastIdx; i++)
            {
                Vector2 pt = navNodes[chain[i]];
                float tLerp = (i + 1f) / (chain.Count + 1f);
                outPath.Add(new Vector3(pt.x, Mathf.Lerp(from.y, to.y, tLerp), pt.y));
            }
        }

        outPath.Add(to);
    }

    static float PolylineTotalLengthXZ(List<Vector3> path)
    {
        float sum = 0f;
        for (int i = 0; i + 1 < path.Count; i++)
            sum += Vector2.Distance(new Vector2(path[i].x, path[i].z), new Vector2(path[i + 1].x, path[i + 1].z));
        return sum;
    }

    static Vector3 SamplePolylineXZ(List<Vector3> path, float targetDist)
    {
        if (path.Count == 0) return Vector3.zero;
        if (path.Count == 1 || targetDist <= 0f) return path[0];
        float acc = 0f;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector3 a = path[i], b = path[i + 1];
            float seg = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            if (acc + seg >= targetDist && seg > 1e-4f)
                return Vector3.Lerp(a, b, (targetDist - acc) / seg);
            acc += seg;
        }
        return path[path.Count - 1];
    }

    // 旧 Prototype 场景没有剧情系统，也给它一个能打开的地图
    public void Init(EggboyPlayer p, EggboyUI u) { Init(p, u, null, null); }

    public bool CanOpen()
    {
        if (player == null || ui == null) return false;
        if (ui.LetterOpen || ui.Paused || ui.TitleShowing || MenuController.OverlayOpen) return false;
        if (director != null && (director.Cutscene || director.Ended || director.Climbing)) return false;
        return true;
    }

    public void Toggle()
    {
        if (IsOpen) { Close(); return; }
        if (!CanOpen()) return;
        if (gate != null && gate.Locked) return;
        if (gate != null)
        {
            mapLock = gate.Acquire("map");
            if (mapLock == null) return;
        }
        IsOpen = true;
        player.locked = true;
        player.vel = Vector3.zero;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        ClosedAt = Time.unscaledTime;
        MenuController.NoteScriptUnlock();
        if (gate != null && mapLock != null) gate.Release(mapLock);
        mapLock = null;
        if (player != null) player.locked = false;
    }

    void Update()
    {
        if (player == null || ui == null) return;
        UpdateGroundGuide3D();
        if (ui.LetterOpen || ui.Paused || ui.TitleShowing || MenuController.OverlayOpen)
        {
            if (IsOpen) Close();
            return;
        }
        if (Input.GetKeyDown(KeyCode.M)) Toggle();
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void OnDisable()
    {
        Close();
        if (guideRoot != null) guideRoot.SetActive(false);
    }

    void EnsureGuide3D()
    {
        if (guideRoot != null) return;
        guideRoot = new GameObject("DynamicGroundGuide3D");
        guideRoot.transform.SetParent(transform, false);

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        guideBaseMat = new Material(shader) { name = "GuideGoldMat" };
        var warm = new Color(1f, 0.82f, 0.32f, 1f);
        if (guideBaseMat.HasProperty("_BaseColor")) guideBaseMat.SetColor("_BaseColor", warm);
        if (guideBaseMat.HasProperty("_Color")) guideBaseMat.SetColor("_Color", warm);

        guideDots = new Transform[GuideDotCount];
        guideDotRenderers = new Renderer[GuideDotCount];
        for (int i = 0; i < GuideDotCount; i++)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dot.name = "GuideDot_" + i;
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(guideRoot.transform, false);
            dot.transform.localScale = new Vector3(0.22f, 0.015f, 0.22f);
            var r = dot.GetComponent<Renderer>();
            r.sharedMaterial = new Material(guideBaseMat);
            guideDots[i] = dot.transform;
            guideDotRenderers[i] = r;
        }

        targetBeaconRoot = new GameObject("TargetBeacon").transform;
        targetBeaconRoot.SetParent(guideRoot.transform, false);

        var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringGo.name = "TargetGroundDisc";
        Destroy(ringGo.GetComponent<Collider>());
        ringGo.transform.SetParent(targetBeaconRoot, false);
        ringGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        ringGo.transform.localScale = new Vector3(0.95f, 0.014f, 0.95f);
        ringGo.GetComponent<Renderer>().sharedMaterial = guideBaseMat;
        targetRing = ringGo.transform;

        var orbGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbGo.name = "TargetWarmOrb";
        Destroy(orbGo.GetComponent<Collider>());
        orbGo.transform.SetParent(targetBeaconRoot, false);
        orbGo.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        orbGo.transform.localScale = Vector3.one * 0.24f;
        orbGo.GetComponent<Renderer>().sharedMaterial = guideBaseMat;
        targetOrb = orbGo.transform;

        var lightGo = new GameObject("TargetWarmLight");
        lightGo.transform.SetParent(targetBeaconRoot, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        targetLight = lightGo.AddComponent<Light>();
        targetLight.type = LightType.Point;
        targetLight.color = new Color(1f, 0.80f, 0.36f);
        targetLight.range = 4.2f;
        targetLight.intensity = 1.6f;
        targetLight.shadows = LightShadows.None;
    }

    void UpdateGroundGuide3D()
    {
        // 自动审计截图模式或过场/暂停/弹窗/序章已有 mailboxTrail 时隐藏动态地面引导
        bool canShow = ui != null && ui.inputClose
            && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen && !MenuController.OverlayOpen
            && director != null && !director.IsAuditMode
            && !director.Cutscene && !director.Ended && !director.Climbing
            && director.State != null && director.State.step != "P0_boot"
            && TryGetStepTarget(out var targetPos, out _);

        if (!canShow)
        {
            if (guideRoot != null && guideRoot.activeSelf) guideRoot.SetActive(false);
            return;
        }

        EnsureGuide3D();
        if (!guideRoot.activeSelf) guideRoot.SetActive(true);

        TryGetStepTarget(out var tgt, out _);
        if (island == null) island = FindObjectOfType<IslandWorld>();

        float tgtGroundY = island != null ? island.GroundAt(tgt.x, tgt.z, tgt.y + 2f) : tgt.y;
        if (tgtGroundY <= 0.1f) tgtGroundY = tgt.y;
        targetBeaconRoot.position = new Vector3(tgt.x, tgtGroundY, tgt.z);

        float t = Time.time;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.8f);
        if (targetOrb != null)
        {
            targetOrb.localPosition = new Vector3(0f, 0.95f + 0.18f * Mathf.Sin(t * 2.4f), 0f);
            targetOrb.localScale = Vector3.one * (0.20f + 0.08f * pulse);
        }
        if (targetRing != null)
        {
            float rs = 0.78f + 0.25f * pulse;
            targetRing.localScale = new Vector3(rs, 0.014f, rs);
        }
        if (targetLight != null)
        {
            targetLight.intensity = 1.3f + 0.8f * pulse;
        }

        // 地面金色引路光点流：沿可走路网折线从玩家脚下前方 1.35m 引导至当前任务目标，避免穿墙入海
        var pPos = player.transform.position;
        BuildGuidePolyline(pPos, new Vector3(tgt.x, tgtGroundY, tgt.z), guidePath);
        float totalDist = PolylineTotalLengthXZ(guidePath);

        for (int i = 0; i < GuideDotCount; i++)
        {
            float d = 1.35f + i * 1.45f;
            if (d >= totalDist - 0.8f || totalDist < 2.2f)
            {
                if (guideDots[i].gameObject.activeSelf) guideDots[i].gameObject.SetActive(false);
                continue;
            }
            if (!guideDots[i].gameObject.activeSelf) guideDots[i].gameObject.SetActive(true);

            Vector3 sample = SamplePolylineXZ(guidePath, d);
            float gy = island != null ? island.GroundAt(sample.x, sample.z, sample.y + 2.5f) : sample.y;
            if (gy <= 0.1f) gy = Mathf.Lerp(pPos.y, tgtGroundY, d / Mathf.Max(1f, totalDist));
            if (island != null && gy < island.WaterY) gy = island.WaterY + 0.04f;

            guideDots[i].position = new Vector3(sample.x, gy + 0.03f, sample.z);
            float wave = 0.5f + 0.5f * Mathf.Sin(t * 4.2f - i * 0.65f);
            float scale = 0.15f + 0.10f * wave;
            guideDots[i].localScale = new Vector3(scale, 0.016f, scale);

            var mat = guideDotRenderers[i].sharedMaterial;
            if (mat != null)
            {
                var c = Color.Lerp(new Color(0.88f, 0.64f, 0.20f, 1f), new Color(1f, 0.94f, 0.55f, 1f), wave);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            }
        }
    }

    public Vector2 ProjectUV(Vector3 p) { return Project(p); }

    Vector2 Project(Vector3 p)
    {
        var q = p - camPos;
        float dz = Vector3.Dot(q, camFwd);
        float dy = Vector3.Dot(q, camUp);
        if (Mathf.Abs(dz) < 0.01f) dz = 0.01f;
        return new Vector2(imgW * 0.5f + focal * q.x / dz, imgH * 0.5f - focal * dy / dz);
    }

    Vector2 WorldToMapScreen(Vector3 worldPos, Rect img)
    {
        var uv = Project(worldPos);
        return new Vector2(img.x + uv.x / imgW * img.width, img.y + uv.y / imgH * img.height);
    }

    bool TryGetStepTarget(out Vector3 targetPos, out string targetLabel)
    {
        targetPos = Vector3.zero;
        targetLabel = "";
        if (director == null || director.State == null) return false;
        var s = director.State;
        switch (s.step)
        {
            case "P0_boot":
            case "P1_mailbox":
                targetPos = new Vector3(-31.04f, 3.0f, -6.76f);
                targetLabel = "山口红色邮筒（按 E 取信）";
                return true;
            case "B0_arrive":
                targetPos = new Vector3(-14.40f, 3.0f, -9.38f);
                targetLabel = "① 林阿婆面包店门前";
                return true;
            case "B1_matches":
                targetPos = new Vector3(-14.40f, 3.0f, -10.30f);
                targetLabel = "面包店门前摊位（拿火柴盒）";
                return true;
            case "B2_wood":
                targetPos = new Vector3(-9.41f, 3.0f, -8.08f);
                targetLabel = "面包店右侧柴堆（抱柴火捆）";
                return true;
            case "B3_load":
                targetPos = new Vector3(-10.55f, 3.0f, -5.84f);
                targetLabel = "面包店石烤炉（把柴火塞进炉膛）";
                return true;
            case "B4_light":
                if (!s.flags.Contains("oven_lit"))
                {
                    targetPos = new Vector3(-10.55f, 3.0f, -5.84f);
                    targetLabel = "面包店石烤炉（按住 E 擦火柴）";
                    return true;
                }
                targetPos = new Vector3(13.53f, 3.0f, -4.25f);
                targetLabel = "② 潮汐镇学校";
                return true;
            case "S0_arrive":
                targetPos = new Vector3(13.53f, 3.0f, -4.25f);
                targetLabel = "② 潮汐镇学校（靠近自动读信）";
                return true;
            case "S1_slipper":
                targetPos = new Vector3(22.86f, 3.0f, 2.67f);
                targetLabel = "学校后院狗窝旁（查看拖鞋）";
                return true;
            case "S2_ball":
                targetPos = new Vector3(16.80f, 3.0f, -5.60f);
                targetLabel = "学校操场秋千下（捡起红球）";
                return true;
            case "S3_bowl":
                targetPos = new Vector3(20.71f, 3.0f, 1.95f);
                targetLabel = "学校后院狗窝前（红球放进狗碗）";
                return true;
            case "S4_read":
                targetPos = new Vector3(19.00f, 3.0f, -5.30f);
                targetLabel = "操场滑梯旁（小黑与阿萤的信）";
                return true;
            case "L0_cross":
                targetPos = new Vector3(12.34f, 1.2f, -12.16f);
                targetLabel = "海边淹水坡道（踩木箱去船棚）";
                return true;
            case "L1_shed":
                targetPos = new Vector3(21.55f, 1.4f, -15.60f);
                targetLabel = "海边船棚屋檐下（拿煤油壶）";
                return true;
            case "L2_door":
                targetPos = new Vector3(35.20f, 5.0f, 6.46f);
                targetLabel = "③ 海角灯塔塔门（按 E 上塔）";
                return true;
            case "L3_oil":
            case "L4_light":
            case "L5_reply":
                targetPos = new Vector3(34.22f, 12.2f, 8.75f);
                targetLabel = "灯塔顶层灯头（加煤油/转灯头）";
                return true;
            case "E0_back":
            case "E1_read":
                targetPos = new Vector3(-21.20f, 3.0f, -9.20f);
                targetLabel = "旧邮局门前课桌（读老周的信）";
                return true;
            case "E2_climb":
            case "E3_end":
                targetPos = new Vector3(-27.54f, 6.4f, 14.89f);
                targetLabel = "山顶新邮局（记忆寄件工作台）";
                return true;
        }
        return false;
    }

    void OnGUI()
    {
        if (!IsOpen || player == null) return;
        var old = GUI.skin.font;
        var font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
        if (font != null) GUI.skin.font = font;

        float w = Mathf.Min(Screen.width * 0.92f, 1180f);
        float h = Mathf.Min(Screen.height * 0.92f, 700f);
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.94f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(panel.x + 18, panel.y + 10, panel.width - 36, 28),
            "潮汐镇全图（带地标路牌与目标指引） · 按 M 或 Esc 关闭    已盖邮戳 " + Stamps() + "/3", Title(font));

        // 背景预览图保持 1774:887
        float pad = 16f;
        var area = new Rect(panel.x + pad, panel.y + 44, panel.width - pad * 2, panel.height - 44 - pad - 24f);
        float aspect = (float)imgW / imgH;
        var img = new Rect(area.x, area.y, area.width, area.width / aspect);
        if (img.height > area.height) { img.height = area.height; img.width = area.height * aspect; }
        img.x = area.x + (area.width - img.width) * 0.5f;
        img.y = area.y + (area.height - img.height) * 0.5f;
        if (preview != null) GUI.DrawTexture(img, preview, ScaleMode.ScaleToFit);

        if (hasCam && preview != null)
        {
            var playerMapPos = WorldToMapScreen(player.transform.position, img);
            var youRect = new Rect(playerMapPos.x + 10f, playerMapPos.y - 12f, 88f, 24f);
            bool hasTargetRect = false;
            Rect targetRect = default;
            Vector2 targetMapPos = default;
            string targetBadgeText = "";

            // 1. 若有当前任务目标，画一条从「你在这里」沿可走路网指向「当前目标」的金色引导虚线
            if (TryGetStepTarget(out var stepTargetWorld, out var stepTargetName))
            {
                targetMapPos = WorldToMapScreen(stepTargetWorld, img);
                targetBadgeText = "★ 当前目标：" + stepTargetName;
                float tw = Mathf.Max(150f, targetBadgeText.Length * 14f + 20f);
                targetRect = new Rect(targetMapPos.x - tw * 0.5f, targetMapPos.y + 10f, tw, 24f);
                hasTargetRect = true;

                BuildGuidePolyline(player.transform.position, stepTargetWorld, guidePath);
                if (guidePath.Count >= 2)
                {
                    for (int k = 0; k + 1 < guidePath.Count; k++)
                    {
                        var pa = WorldToMapScreen(guidePath[k], img);
                        var pb = WorldToMapScreen(guidePath[k + 1], img);
                        int segs = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(pa, pb) / 14f), 2, 16);
                        DrawDottedLine(pa, pb, new Color(1f, 0.86f, 0.28f, 0.92f), segs);
                    }
                }
                else
                {
                    DrawDottedLine(playerMapPos, targetMapPos, new Color(1f, 0.86f, 0.28f, 0.92f), 18);
                }
            }

            // 2. 绘制全镇所有建筑/地标名称标签（带 2D 防重叠排斥计算，仅在打开地图 M 时显示）
            var tagStyle = MapTagStyle(font);
            int lmCount = Landmarks.Length;
            var pinPos = new Vector2[lmCount];
            var tagRects = new Rect[lmCount];
            var tagTexts = new string[lmCount];
            var tagStamped = new bool[lmCount];

            for (int i = 0; i < lmCount; i++)
            {
                var lm = Landmarks[i];
                var lp = WorldToMapScreen(lm.worldPos, img);
                bool stamped = !string.IsNullOrEmpty(lm.stampId) && director != null && director.State != null && director.State.stamps.Contains(lm.stampId);
                string text = stamped ? (lm.name + " ✓") : lm.name;
                float tw = Mathf.Max(84f, text.Length * 13.5f + 16f);
                pinPos[i] = lp;
                tagTexts[i] = text;
                tagStamped[i] = stamped;
                tagRects[i] = new Rect(lp.x - tw * 0.5f, lp.y - 26f, tw, 22f);
            }

            // 迭代排斥重叠标签（同时避让「你在这里」和「当前目标」标签）
            for (int iter = 0; iter < 6; iter++)
            {
                for (int i = 0; i < lmCount; i++)
                {
                    var ri = tagRects[i];
                    if (hasTargetRect) SeparateRect(ref ri, targetRect, 4f);
                    SeparateRect(ref ri, youRect, 4f);
                    for (int j = i + 1; j < lmCount; j++)
                    {
                        var rj = tagRects[j];
                        SeparatePair(ref ri, ref rj, 4f);
                        tagRects[j] = ClampRectToMap(rj, img);
                    }
                    tagRects[i] = ClampRectToMap(ri, img);
                }
            }

            for (int i = 0; i < lmCount; i++)
            {
                var lm = Landmarks[i];
                var lp = pinPos[i];
                var tagRect = tagRects[i];
                bool stamped = tagStamped[i];

                // 若标签因防重叠偏移较远，画一条细引线连回地标锚点
                Vector2 badgeAnchor = new Vector2(Mathf.Clamp(lp.x, tagRect.xMin + 6f, tagRect.xMax - 6f), lp.y < tagRect.center.y ? tagRect.yMin : tagRect.yMax);
                if (Vector2.Distance(lp, badgeAnchor) > 8f)
                    DrawDottedLine(lp, badgeAnchor, new Color(0.95f, 0.88f, 0.68f, 0.65f), 4);

                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(lp.x - 4f, lp.y - 4f, 8f, 8f), Texture2D.whiteTexture);
                GUI.color = stamped ? new Color(0.35f, 0.88f, 0.48f, 1f) : new Color(0.98f, 0.90f, 0.72f, 1f);
                GUI.DrawTexture(new Rect(lp.x - 3f, lp.y - 3f, 6f, 6f), Texture2D.whiteTexture);

                GUI.color = new Color(0.06f, 0.05f, 0.04f, 0.88f);
                GUI.DrawTexture(new Rect(tagRect.x - 1f, tagRect.y - 1f, tagRect.width + 2f, tagRect.height + 2f), Texture2D.whiteTexture);
                GUI.color = stamped ? new Color(0.18f, 0.46f, 0.26f, 0.92f) : lm.badgeColor;
                GUI.DrawTexture(tagRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(tagRect, tagTexts[i], tagStyle);
            }

            if (hasTargetRect)
                DrawMapTargetPin(targetMapPos, targetBadgeText, font);

            // 3. 玩家位置红点
            GUI.color = new Color(1f, 0.18f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(playerMapPos.x - 6f, playerMapPos.y - 6f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(playerMapPos.x - 8f, playerMapPos.y - 8f, 16f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(playerMapPos.x - 8f, playerMapPos.y + 6f, 16f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(playerMapPos.x - 8f, playerMapPos.y - 8f, 2f, 16f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(playerMapPos.x + 6f, playerMapPos.y - 8f, 2f, 16f), Texture2D.whiteTexture);

            GUI.color = new Color(0.85f, 0.16f, 0.12f, 0.94f);
            GUI.DrawTexture(youRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(youRect, "● 你在这里", tagStyle);
        }

        GUI.Label(new Rect(panel.x + 18, panel.yMax - 34, panel.width - 36, 26),
            GoalText() + "   |   提示：平时走路时可跟随脚下的【金色地面引路微光】前往发光目标点", Label(font));
        GUI.skin.font = old;
    }

    static void SeparatePair(ref Rect a, ref Rect b, float pad)
    {
        float ox = Mathf.Min(a.xMax + pad, b.xMax + pad) - Mathf.Max(a.xMin, b.xMin);
        float oy = Mathf.Min(a.yMax + pad, b.yMax + pad) - Mathf.Max(a.yMin, b.yMin);
        if (ox <= 0f || oy <= 0f) return;
        if (oy <= ox * 1.4f)
        {
            float dir = a.center.y <= b.center.y ? -1f : 1f;
            float shift = oy * 0.52f * dir;
            a.y += shift;
            b.y -= shift;
        }
        else
        {
            float dir = a.center.x <= b.center.x ? -1f : 1f;
            float shift = ox * 0.52f * dir;
            a.x += shift;
            b.x -= shift;
        }
    }

    static void SeparateRect(ref Rect movable, Rect fixedRect, float pad)
    {
        float ox = Mathf.Min(movable.xMax + pad, fixedRect.xMax + pad) - Mathf.Max(movable.xMin, fixedRect.xMin);
        float oy = Mathf.Min(movable.yMax + pad, fixedRect.yMax + pad) - Mathf.Max(movable.yMin, fixedRect.yMin);
        if (ox <= 0f || oy <= 0f) return;
        float dir = movable.center.y <= fixedRect.center.y ? -1f : 1f;
        movable.y += oy * dir;
    }

    static Rect ClampRectToMap(Rect r, Rect bounds)
    {
        r.x = Mathf.Clamp(r.x, bounds.xMin + 4f, bounds.xMax - r.width - 4f);
        r.y = Mathf.Clamp(r.y, bounds.yMin + 4f, bounds.yMax - r.height - 4f);
        return r;
    }

    static void DrawDottedLine(Vector2 a, Vector2 b, Color col, int segments)
    {
        var prev = GUI.color;
        GUI.color = col;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            var p = Vector2.Lerp(a, b, t);
            GUI.DrawTexture(new Rect(p.x - 2.5f, p.y - 2.5f, 5f, 5f), Texture2D.whiteTexture);
        }
        GUI.color = prev;
    }

    void DrawMapTargetPin(Vector2 p, string text, Font font)
    {
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
        GUI.color = new Color(1f, 0.82f, 0.16f, pulse);
        GUI.DrawTexture(new Rect(p.x - 8f, p.y - 8f, 16f, 16f), Texture2D.whiteTexture);
        GUI.color = new Color(0.18f, 0.12f, 0.04f, 0.96f);
        float w = Mathf.Max(150f, text.Length * 14f + 20f);
        var r = new Rect(p.x - w * 0.5f, p.y + 10f, w, 24f);
        GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Texture2D.whiteTexture);
        GUI.color = new Color(0.96f, 0.74f, 0.18f, 0.96f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        var st = WorldSignStyle(font, 13, new Color32(0x1e, 0x14, 0x06, 255));
        GUI.Label(r, text, st);
    }

    string Stamps()
    {
        var d = FindObjectOfType<StoryDirector>();
        return d != null && d.State != null ? d.State.stamps.Count.ToString() : "0";
    }

    string GoalText()
    {
        if (ui == null) return "";
        var t = ui.GoalText;
        return string.IsNullOrEmpty(t) ? "自由逛逛 · 靠近发光的东西按 E" : t;
    }

    GUIStyle Title(Font font)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = 20;
        s.fontStyle = FontStyle.Bold;
        s.normal.textColor = new Color32(0xfb, 0xef, 0xd9, 255);
        if (font != null) s.font = font;
        return s;
    }

    GUIStyle Label(Font font)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = 15;
        s.normal.textColor = new Color32(0xe8, 0xd6, 0xb4, 255);
        if (font != null) s.font = font;
        return s;
    }

    static GUIStyle MapTagStyle(Font font)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = 13;
        s.fontStyle = FontStyle.Bold;
        s.alignment = TextAnchor.MiddleCenter;
        s.normal.textColor = Color.white;
        if (font != null) s.font = font;
        return s;
    }

    static GUIStyle WorldSignStyle(Font font, int size, Color32 textColor)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = size;
        s.fontStyle = FontStyle.Bold;
        s.alignment = TextAnchor.MiddleCenter;
        s.normal.textColor = textColor;
        if (font != null) s.font = font;
        return s;
    }
}
