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

    // 与 ShotTool 的真实正交相机共用这份坐标定义。图片左上为原点，+Z 朝北。
    [Serializable]
    public sealed class MapBounds
    {
        public float cx, cz, spanX, spanZ;
        public int pixelWidth, pixelHeight;

        public bool Matches(int width, int height)
        {
            // Unity 可能把 1774x887 导入为 2048x1024；换算取拍摄范围和原图比例。
            return width > 0 && height > 0 && pixelWidth > 0 && pixelHeight > 0
                && spanX > 0f && spanZ > 0f && !float.IsInfinity(spanX) && !float.IsInfinity(spanZ)
                && !float.IsNaN(cx) && !float.IsInfinity(cx) && !float.IsNaN(cz) && !float.IsInfinity(cz)
                && Mathf.Abs(spanX / spanZ - (float)pixelWidth / pixelHeight) < .001f
                && Mathf.Abs((float)pixelWidth / pixelHeight - (float)width / height) < .001f;
        }

        public Vector2 ProjectPixels(Vector3 position)
        {
            return new Vector2((.5f + (position.x - cx) / spanX) * pixelWidth,
                (.5f - (position.z - cz) / spanZ) * pixelHeight);
        }
    }

    struct LandmarkInfo
    {
        public string name;
        public string sub;
        public Vector3 worldPos;
        public string stampId;
        public string sceneName;

        public LandmarkInfo(string n, string s, Vector3 p, string st, string sceneName)
        {
            name = n; sub = s; worldPos = p; stampId = st; this.sceneName = sceneName;
        }
    }

    static readonly LandmarkInfo[] Landmarks =
    {
        new LandmarkInfo("山口邮筒", "起点", new Vector3(-31.04f, 3.2f, -6.76f), "", "P_MAILBOX"),
        new LandmarkInfo("旧邮局", "老周的值班室", new Vector3(-22.50f, 5.2f, -9.51f), "", "B1_post_office"),
        new LandmarkInfo("① 林阿婆面包店", "第一站 · 烤炉与柴火", new Vector3(-14.40f, 5.2f, -8.50f), "bakery", "B2_bakery"),
        new LandmarkInfo("面包店门铃", "面包店正门旁", new Vector3(-12.58f, 3.72f, -6.66f), "", "Bakery_bell"),
        new LandmarkInfo("♪ 广场八音盒", "心愿展台 · 声影机关", new Vector3(-11.90f, 3.3f, -5.80f), "", "MusicBoxPlinth"),
        new LandmarkInfo("② 潮汐镇学校", "第二站 · 阿萤与小黑", new Vector3(13.53f, 5.6f, -2.02f), "school", "B3_school"),
        new LandmarkInfo("学校操场", "秋千 / 红球 / 滑梯", new Vector3(17.50f, 3.8f, -5.60f), "", "P_21"),
        new LandmarkInfo("学校后院 · 狗窝", "拖鞋 / 狗碗", new Vector3(21.76f, 4.1f, 1.95f), "", "S_doghouse"),
        new LandmarkInfo("⚙ 渡口齿轮绞盘", "涨潮过海机关", new Vector3(13.40f, 1.8f, -13.10f), "", "Scene_M3_潮汐船坞齿轮绞盘_总成"),
        new LandmarkInfo("海边船棚 & 码头", "拿煤油壶", new Vector3(19.50f, 3.2f, -14.80f), "", "B5_boathouse"),
        new LandmarkInfo("③ 海角灯塔", "第三站 · 点亮灯头", new Vector3(33.59f, 8.5f, 9.62f), "lighthouse", "B4_lighthouse"),
        new LandmarkInfo("山顶新邮局", "终点 · 记忆寄件台", new Vector3(-27.80f, 5.5f, 18.55f), "", "P_06"),
    };

    EggboyPlayer player;
    EggboyUI ui;
    StoryDirector director;
    InteractionService interactions;
    Transform bakeryArrival, schoolArrival, schoolSlide, summit;
    ControlGate gate;
    IslandWorld island;
    Texture2D preview;
    object mapLock;
    MapBounds mapBounds;
    readonly Vector3[] landmarkPositions = new Vector3[Landmarks.Length];

    bool hasCam;
    Vector3 camPos, camFwd, camUp;
    float focal;
    int imgW = 1774, imgH = 887;

    public enum GroundGuideMode { Automatic, Always, Off }
    const string GuidePreference = "tidetown.groundGuide";
    const float RecallSeconds = 6f, StraySeconds = 8f;
    const float GuideHeight = 0.008f;
    static readonly float[] GuideDistances = { 1.55f, 3.10f, 4.65f, 6.20f };
    static readonly bool CaptureMode = Array.Exists(Environment.GetCommandLineArgs(), a =>
        a == "-shot" || a == "-feeldemo" || a == "-shot_aa");
    public GroundGuideMode GuideMode { get; private set; }
    GameObject guideRoot;
    Transform[] guideDots;
    Renderer[] guideDotRenderers;
    Renderer targetDisc;
    Material guideBaseMat;
    Texture2D guideTexture;
    readonly MaterialPropertyBlock guideColor = new MaterialPropertyBlock();
    string guideStep;
    Vector3 guideTarget, idleAnchor;
    float guideRecall, guideIdle, guideStray, guideOpacity, nextGuideRoute;
    bool guideOffRoute;

    // 路网寻路折线（用于 3D 地面引路光点与 2D 地图引导虚线，避免穿墙入海）
    bool navLoaded;
    readonly List<Vector2> navNodes = new List<Vector2>();
    readonly List<List<int>> navAdj = new List<List<int>>();
    readonly List<Vector3> guidePath = new List<Vector3>();
    readonly List<Vector3> mapPath = new List<Vector3>();

    public bool IsOpen { get; private set; }
    public bool HasOrthographicMap => mapBounds != null;
    public float ClosedAt { get; private set; }

    public void Init(EggboyPlayer p, EggboyUI u, StoryDirector d, ControlGate g)
    {
        player = p;
        ui = u;
        director = d;
        interactions = GetComponent<InteractionService>();
        if (director != null)
        {
            bakeryArrival = GameObject.Find("P_11")?.transform;
            schoolArrival = GameObject.Find("P_19")?.transform;
            schoolSlide = GameObject.Find("S_slide")?.transform;
            summit = GameObject.Find("P_06")?.transform;
        }
        gate = g;
        island = FindObjectOfType<IslandWorld>();
        GuideMode = (GroundGuideMode)Mathf.Clamp(PlayerPrefs.GetInt(GuidePreference), 0, 2);
        preview = Resources.Load<Texture2D>("UI/town_preview");
        if (preview != null) { imgW = preview.width; imgH = preview.height; }
        LoadCamera();
        EnsureNavGraph();
    }

    void LoadCamera()
    {
        var boundsAsset = Resources.Load<TextAsset>("UI/map_bounds");
        if (boundsAsset != null && preview != null)
        {
            var bounds = JsonUtility.FromJson<MapBounds>(boundsAsset.text);
            if (bounds != null && bounds.Matches(imgW, imgH))
            {
                mapBounds = bounds;
                imgW = bounds.pixelWidth;
                imgH = bounds.pixelHeight;
                hasCam = true;
                return;
            }
            Debug.LogError("MAP_BOUNDS invalid for town_preview: " + imgW + "x" + imgH);
            return;
        }
        // Wave 1 包及旧 Prototype 保留概念底图，等实拍和边界元数据一起导入再切换。
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
        for (int i = 0; i < Landmarks.Length; i++)
        {
            var landmark = Landmarks[i];
            var anchor = mapBounds != null ? GameObject.Find(landmark.sceneName) : null;
            landmarkPositions[i] = anchor != null ? anchor.transform.position : landmark.worldPos;
        }
        ui.SetCinematic(true,"map");
        player.locked = true;
        player.vel = Vector3.zero;
        MenuController.ReleasePointer();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if(ui!=null)ui.SetCinematic(false,"map");
        ClosedAt = Time.unscaledTime;
        MenuController.NoteScriptUnlock();
        if (gate != null && mapLock != null) gate.Release(mapLock);
        mapLock = null;
        if (player != null) player.locked = false;
        MenuController.RestorePointer(ui);
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

    public void CycleGuideMode()
    {
        GuideMode = (GroundGuideMode)(((int)GuideMode + 1) % 3);
        PlayerPrefs.SetInt(GuidePreference, (int)GuideMode);
        PlayerPrefs.Save();
    }

    void OnDisable()
    {
        Close();
        if (guideRoot != null) guideRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (guideBaseMat != null) Destroy(guideBaseMat);
        if (guideTexture != null) Destroy(guideTexture);
    }

    void EnsureGuide3D()
    {
        if (guideRoot != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return;

        guideRoot = new GameObject("DynamicGroundGuide3D");
        guideRoot.transform.SetParent(transform, false);
        guideBaseMat = new Material(shader) { name = "GroundGuideGlow" };
        guideBaseMat.SetFloat("_Surface", 1f);
        guideBaseMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        guideBaseMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        guideBaseMat.SetFloat("_ZWrite", 0f);
        guideBaseMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        guideBaseMat.SetOverrideTag("RenderType", "Transparent");
        guideBaseMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        const int size = 32;
        guideTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "GroundGuideFalloff",
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var offset = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f);
                float alpha = 1f - Mathf.SmoothStep(.0f, 1f, offset.magnitude);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        guideTexture.SetPixels32(pixels);
        guideTexture.Apply(false, true);
        guideBaseMat.SetTexture("_BaseMap", guideTexture);

        guideDots = new Transform[GuideDistances.Length];
        guideDotRenderers = new Renderer[GuideDistances.Length];
        for (int i = 0; i < guideDots.Length; i++)
        {
            guideDotRenderers[i] = CreateGuideDisc("GuideDot_" + i, guideRoot.transform);
            guideDots[i] = guideDotRenderers[i].transform;
        }

        var beacon = new GameObject("TargetBeacon").transform;
        beacon.SetParent(guideRoot.transform, false);
        targetDisc = CreateGuideDisc("TargetGroundDisc", beacon);
    }

    Renderer CreateGuideDisc(string name, Transform parent)
    {
        var dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dot.name = name;
        var collider = dot.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }
        dot.transform.SetParent(parent, false);
        dot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var renderer = dot.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = guideBaseMat;
        renderer.enabled = false;
        return renderer;
    }

    void UpdateGroundGuide3D()
    {
        if (CaptureMode)
        {
            if (island != null && island.mailboxTrail != null && island.mailboxTrail.activeSelf)
                island.mailboxTrail.SetActive(false);
            HideGuide();
            return;
        }

        if (GuideMode == GroundGuideMode.Off || !ui.inputClose || ui.TitleShowing || ui.Paused ||
            ui.LetterOpen || ui.Cinematic || IsOpen || MenuController.OverlayOpen || player.locked ||
            director == null || director.IsAuditMode || director.Cutscene || director.Ended || director.Climbing ||
            director.State == null || director.State.step == "P0_boot" ||
            !TryGetStepTarget(out var target, out _))
        {
            HideGuide();
            return;
        }

        var position = player.transform.position;
        if (guideStep != director.State.step || (guideTarget - target).sqrMagnitude > 0.01f)
        {
            guideStep = director.State.step;
            guideTarget = target;
            guideRecall = RecallSeconds;
            guideIdle = guideStray = 0f;
            idleAnchor = position;
            nextGuideRoute = 0f;
        }

        float dt = Time.unscaledDeltaTime;
        if (Input.GetKeyDown(KeyCode.H)) guideRecall = RecallSeconds;
        if (HorizontalDistance(position, idleAnchor) > .55f)
        {
            guideIdle = 0f;
            idleAnchor = position;
        }
        else guideIdle += dt;

        if (Time.unscaledTime >= nextGuideRoute)
        {
            nextGuideRoute = Time.unscaledTime + .18f;
            BuildGuidePolyline(position, target, guidePath);
            guideOffRoute = HorizontalDistance(position, target) > 4f && !NearGuideRoute(position);
        }
        guideStray = guideOffRoute ? guideStray + dt : 0f;

        bool active = GuideMode == GroundGuideMode.Always ||
            guideRecall > 0f || guideIdle >= StraySeconds || guideStray >= StraySeconds;
        guideOpacity = Mathf.MoveTowards(guideOpacity, active ? .92f : .06f, dt * 2.5f);
        guideRecall = Mathf.Max(0f, guideRecall - dt);
        if (guideOpacity <= .02f) return;

        EnsureGuide3D();
        if (guideRoot == null) return;
        if (!guideRoot.activeSelf) guideRoot.SetActive(true);
        float span = PolylineTotalLengthXZ(guidePath);
        float pulse = .9f + .1f * Mathf.Sin(Time.unscaledTime * 2.3f);
        for (int i = 0; i < guideDots.Length; i++)
        {
            float distance = GuideDistances[i];
            float fade = i / (guideDots.Length - 1f);
            var sample = SamplePolylineXZ(guidePath, distance);
            float fromPlayer = HorizontalDistance(position, sample);
            guideDotRenderers[i].enabled = distance < span - .45f &&
                fromPlayer >= 1.5f && fromPlayer <= 6.5f &&
                PlaceGuideDisc(guideDotRenderers[i], sample, Mathf.Lerp(.10f, .07f, fade),
                    guideOpacity * Mathf.Lerp(.96f, .42f, fade) * pulse);
        }

        targetDisc.enabled = span <= 6.5f && HorizontalDistance(position, target) >= 1.5f &&
            PlaceGuideDisc(targetDisc, target, .23f, guideOpacity * .62f * pulse);
    }

    void HideGuide()
    {
        if (guideRoot != null && guideRoot.activeSelf) guideRoot.SetActive(false);
        guideIdle = guideStray = guideOpacity = 0f;
        if (player != null) idleAnchor = player.transform.position;
        nextGuideRoute = 0f;
    }

    bool PlaceGuideDisc(Renderer disc, Vector3 position, float size, float opacity)
    {
        float ground = island != null ? island.GroundAt(position.x, position.z, position.y + 1.2f) : position.y;
        if (island != null && (ground <= .05f || ground < island.WaterY - .01f)) return false;
        disc.transform.position = new Vector3(position.x, ground + GuideHeight, position.z);
        disc.transform.localScale = Vector3.one * size;
        guideColor.SetColor("_BaseColor", new Color(1f, .78f, .36f, opacity));
        disc.SetPropertyBlock(guideColor);
        return true;
    }

    bool NearGuideRoute(Vector3 position)
    {
        var point = new Vector2(position.x, position.z);
        for (int i = 0; i < navNodes.Count; i++)
        {
            Vector2 a = navNodes[i];
            foreach (int v in navAdj[i])
            {
                if (v <= i) continue;
                var segment = navNodes[v] - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude);
                if ((point - a - t * segment).sqrMagnitude <= 9f) return true;
            }
        }
        return navNodes.Count == 0;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));


    public Vector2 ProjectUV(Vector3 p) { return Project(p); }

    Vector2 Project(Vector3 p)
    {
        if (mapBounds != null) return mapBounds.ProjectPixels(p);
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

    internal bool TryGetStepTarget(out Vector3 targetPos, out string targetLabel)
    {
        targetPos = Vector3.zero;
        targetLabel = "";
        if (director == null || director.State == null) return false;
        var s = director.State;
        switch (s.step)
        {
            case "P0_boot":
            case "P1_mailbox":
                return InteractionTarget("mailbox_03", "山口红色邮筒（按 E 取信）", out targetPos, out targetLabel);
            case "B0_arrive":
                targetPos = bakeryArrival != null ? bakeryArrival.position : new Vector3(-14.40f, 3f, -9.58f);
                targetLabel = "① 林阿婆面包店门前";
                return true;
            case "B1_matches":
                return InteractionTarget("stall", "橱窗下的摊位（拿火柴盒）", out targetPos, out targetLabel);
            case "B2_wood":
                return InteractionTarget("wood", "烤炉右前方（抱柴火捆）", out targetPos, out targetLabel);
            case "B3_load":
                return InteractionTarget("stove", "面包店石烤炉（把柴火塞进炉膛）", out targetPos, out targetLabel);
            case "B4_light":
                if (!s.flags.Contains("oven_lit"))
                    return InteractionTarget("stove", "面包店石烤炉（按住 E 擦火柴）", out targetPos, out targetLabel);
                goto case "S0_arrive";
            case "S0_arrive":
                targetPos = schoolArrival != null ? schoolArrival.position : new Vector3(13.53f, 3f, -4.25f);
                targetLabel = "② 潮汐镇学校（靠近自动读信）";
                return true;
            case "S1_slipper":
                return InteractionTarget("slipper", "学校后院狗窝旁（查看拖鞋）", out targetPos, out targetLabel);
            case "S2_ball":
                return InteractionTarget("ball", "学校操场秋千前（捡起红球）", out targetPos, out targetLabel);
            case "S3_bowl":
                return InteractionTarget("bowl", "学校后院狗窝前（红球放进狗碗）", out targetPos, out targetLabel);
            case "S4_read":
                targetPos = schoolSlide != null ? schoolSlide.position : new Vector3(19f, 3f, -4f);
                targetLabel = "操场滑梯旁（小黑与阿萤的信）";
                return true;
            case "L0_cross":
                targetPos = new Vector3(12.34f, 1.2f, -12.16f);
                targetLabel = "海边淹水坡道（踩木箱去船棚）";
                return true;
            case "L1_shed":
                return InteractionTarget("oil", "海边船棚屋檐下（拿煤油壶）", out targetPos, out targetLabel);
            case "L2_door":
                return InteractionTarget("lighthouse_door", "③ 海角灯塔塔门（按 E 上塔）", out targetPos, out targetLabel);
            case "L3_oil":
            case "L4_light":
            case "L5_reply":
                return InteractionTarget("lamp_head", "灯塔顶层灯头（加煤油/转灯头）", out targetPos, out targetLabel);
            case "E0_back":
            case "E1_read":
                return InteractionTarget("ending_desk", "旧邮局门前课桌（读老周的信）", out targetPos, out targetLabel);
            case "E2_climb":
            case "E3_end":
                targetPos = summit != null ? summit.position : new Vector3(-27.80f, 5.5f, 18.55f);
                targetLabel = "山顶新邮局（记忆寄件工作台）";
                return true;
        }
        return false;
    }

    bool InteractionTarget(string id, string label, out Vector3 target, out string targetLabel)
    {
        target = default;
        targetLabel = label;
        return interactions != null && interactions.TryGetPosition(id, out target);
    }

    // ───────── 2026-11-16「潮汐镇手绘航海地图册（Postal Atlas）」 ─────────
    // 和暂停手账、信封同一套色板：胡桃木外框 + 暖米羊皮纸内页 + 深褐细描边；顶栏 3 枚邮戳槽，地图右上角航海罗盘，底栏任务 + 图例。
    // 仍用 OnGUI 画（MapAudit 截图走同一帧）。所有尺寸、字号按屏幕等比缩放，2K/4K 下字不糊。
    static readonly Color AtlasWalnut = new Color32(0x3b, 0x2a, 0x1a, 255);
    static readonly Color AtlasWalnutHi = new Color32(0x6a, 0x4c, 0x30, 230);
    static readonly Color AtlasParchment = new Color32(0xfb, 0xf3, 0xe2, 255);
    static readonly Color AtlasParchmentDeep = new Color32(0xf1, 0xe2, 0xc4, 255);
    static readonly Color AtlasInk = new Color32(0x3b, 0x2a, 0x1a, 255);
    static readonly Color AtlasInkSoft = new Color32(0x3b, 0x2a, 0x1a, 150);
    static readonly Color AtlasInkMuted = new Color32(0x8a, 0x6a, 0x4a, 255);
    static readonly Color AtlasRule = new Color32(0xc9, 0xa8, 0x7a, 255);
    static readonly Color AtlasRuleSoft = new Color32(0xc9, 0xa8, 0x7a, 190);
    static readonly Color AtlasCinnabar = new Color32(0xb2, 0x35, 0x28, 255);
    static readonly Color AtlasGold = new Color32(0xe9, 0xa9, 0x3f, 255);
    static readonly Color AtlasGoldSoft = new Color32(0xfd, 0xdc, 0x92, 255);
    static readonly string[] StampIds = { "bakery", "school", "lighthouse" };
    static readonly string[] StampLabels = { "① 面包店", "② 学校", "③ 灯塔" };
    float uiK = 1f;
    float U(float v) => v * uiK;

    void OnGUI()
    {
        if (!IsOpen || player == null) return;
        var oldFont = GUI.skin.font;
        var oldColor = GUI.color;
        GUI.skin.font = TideTownStyleUI.UiFont;
        GUI.color = Color.white;
        float sw = Screen.width, sh = Screen.height;
        uiK = Mathf.Clamp(Mathf.Min(sw / 1600f, sh / 900f), .72f, 2.6f);

        // 1. 柔和暗角：整体只压一层很淡的暖褐，四周再叠暗角，不再是死黑大方块
        Fill(new Rect(0f, 0f, sw, sh), new Color(.12f, .08f, .05f, .32f), 0f);
        GUI.DrawTexture(new Rect(0f, 0f, sw, sh), TideTownStyleUI.SoftVignetteSprite.texture, ScaleMode.StretchToFill, true, 0f,
            new Color(.09f, .055f, .03f, .9f), 0f, 0f);

        // 2. 版面：按底图比例反推整本图册的大小
        float aspect = (float)imgW / imgH;
        float frame = U(13f), pad = U(16f), head = U(52f), foot = U(46f), gap = U(10f);
        float chromeH = (frame + pad) * 2f;
        float chromeV = frame * 2f + U(6f) + head + gap + U(6f) + foot + U(4f);
        float mapW = sw * .94f - chromeH, mapH = mapW / aspect;
        if (mapH + chromeV > sh * .93f) { mapH = sh * .93f - chromeV; mapW = mapH * aspect; }
        var panel = new Rect((sw - mapW - chromeH) * .5f, (sh - mapH - chromeV) * .5f, mapW + chromeH, mapH + chromeV);

        for (int i = 1; i <= 4; i++)
            Fill(Grow(new Rect(panel.x, panel.y + U(8f), panel.width, panel.height), U(i * 3.5f)), new Color(0f, 0f, 0f, .08f), U(22f + i * 3.5f));
        Fill(panel, AtlasWalnut, U(20f));
        Line(Grow(panel, -U(4f)), AtlasWalnutHi, Mathf.Max(1f, U(1.2f)), U(16f));
        var page = Grow(panel, -frame);
        Fill(page, AtlasParchment, U(10f));
        Line(Grow(page, -U(5f)), AtlasRuleSoft, Mathf.Max(1f, U(1f)), U(7f));

        // 3. 顶栏：眉题 + 3 枚邮戳槽 + 合上键帽
        var headR = new Rect(page.x + pad, page.y + U(6f), page.width - pad * 2f, head);
        DrawAtlasHeader(headR);
        Fill(new Rect(headR.x, headR.yMax - U(1f), headR.width, Mathf.Max(1f, U(1.2f))), AtlasRule, 0f);

        // 4. 地图画面：深褐细描边 + 羊皮纸暖调 + 内缩细线 + 右上航海罗盘
        var img = new Rect(page.x + pad, headR.yMax + gap, mapW, mapH);
        Fill(Grow(img, U(3f)), AtlasInk, U(6f));
        if (preview != null) GUI.DrawTexture(img, preview, ScaleMode.StretchToFill, false, 0f, Color.white, 0f, U(3.5f));
        else Fill(img, AtlasParchmentDeep, U(3.5f));
        Fill(img, new Color(.98f, .88f, .70f, .10f), U(3.5f));
        Line(Grow(img, -U(6f)), new Color(.99f, .96f, .88f, .5f), Mathf.Max(1f, U(1f)), U(3f));
        DrawCompass(img);

        if (hasCam && preview != null) DrawMapMarks(img);

        // 5. 底栏：左当前任务，右图例
        var footR = new Rect(page.x + pad, img.yMax + U(6f), page.width - pad * 2f, foot);
        DrawAtlasFooter(footR);

        GUI.color = oldColor;
        GUI.skin.font = oldFont;
    }

    void DrawAtlasHeader(Rect r)
    {
        float cy = r.center.y - U(1f);
        // 左：一枚淡朱砂邮戳环 + POSTAL ATLAS 眉题 + 中文大标题
        float ring = U(32f);
        var ringR = new Rect(r.x, cy - ring * .5f, ring, ring);
        GUI.DrawTexture(ringR, TideTownStyleUI.PostmarkRingSprite.texture, ScaleMode.StretchToFill, true, 0f,
            new Color(AtlasCinnabar.r, AtlasCinnabar.g, AtlasCinnabar.b, .6f), 0f, 0f);
        var eyebrow = AtlasStyle(12f, AtlasInkMuted, true, TextAnchor.MiddleLeft);
        var title = AtlasStyle(21f, AtlasInk, true, TextAnchor.MiddleLeft);
        const string eb = "POSTAL ATLAS  ·";
        float ebW = TextW(eyebrow, eb);
        float x = ringR.xMax + U(10f);
        GUI.Label(new Rect(x, cy - U(14f), ebW + U(4f), U(28f)), eb, eyebrow);
        x += ebW + U(8f);
        GUI.Label(new Rect(x, cy - U(16f), r.width * .45f, U(32f)), "潮汐镇航海投递全图", title);

        // 右：合上键帽（Esc / M），一道竖细线，再从右往左排 3 枚邮戳槽
        float right = r.xMax;
        var hint = AtlasStyle(13f, AtlasInkMuted, false, TextAnchor.MiddleLeft);
        float hintW = TextW(hint, "合上");
        GUI.Label(new Rect(right - hintW, cy - U(12f), hintW + U(4f), U(24f)), "合上", hint);
        right -= hintW + U(7f);
        right = Keycap(right, cy, "M");
        GUI.Label(new Rect(right - U(16f), cy - U(12f), U(16f), U(24f)), "/", AtlasStyle(13f, AtlasInkMuted, false, TextAnchor.MiddleCenter));
        right -= U(16f);
        right = Keycap(right, cy, "Esc");
        right -= U(16f);
        Fill(new Rect(right, cy - U(14f), Mathf.Max(1f, U(1.2f)), U(28f)), AtlasRule, 0f);
        right -= U(16f);

        var lab = AtlasStyle(13.5f, AtlasInk, true, TextAnchor.MiddleLeft);
        var labOff = AtlasStyle(13.5f, AtlasInkMuted, false, TextAnchor.MiddleLeft);
        var state = director != null ? director.State : null;
        for (int i = StampIds.Length - 1; i >= 0; i--)
        {
            bool done = state != null && state.stamps.Contains(StampIds[i]);
            var st = done ? lab : labOff;
            float lw = TextW(st, StampLabels[i]);
            GUI.Label(new Rect(right - lw, cy - U(12f), lw + U(4f), U(24f)), StampLabels[i], st);
            right -= lw + U(7f);
            float d = U(34f);
            DrawStampSlot(new Vector2(right - d * .5f, cy), d, done);
            right -= d + U(18f);
        }
    }

    float Keycap(float right, float cy, string label)
    {
        var st = AtlasStyle(11.5f, AtlasInk, true, TextAnchor.MiddleCenter);
        float w = Mathf.Max(U(26f), TextW(st, label) + U(14f)), h = U(25f);
        var r = new Rect(right - w, cy - h * .5f, w, h);
        Fill(r, new Color(.36f, .24f, .15f, 1f), U(6f));
        var face = new Rect(r.x + U(2f), r.y + U(1.5f), r.width - U(4f), r.height - U(6f));
        Fill(face, new Color(.98f, .94f, .86f, 1f), U(5f));
        GUI.Label(face, label, st);
        return r.x;
    }

    void DrawStampSlot(Vector2 c, float d, bool done)
    {
        if (done)
        {
            // 已盖章：一圈淡朱砂邮戳环压在后面，前面一枚朱砂红火漆印，中间奶白 ✓
            float pm = d * 1.32f;
            GUI.DrawTexture(new Rect(c.x - pm * .5f + U(4f), c.y - pm * .5f - U(1f), pm, pm), TideTownStyleUI.PostmarkRingSprite.texture,
                ScaleMode.StretchToFill, true, 0f, new Color(AtlasCinnabar.r, AtlasCinnabar.g, AtlasCinnabar.b, .42f), 0f, 0f);
            GUI.DrawTexture(Circle(c, d * .5f), TideTownStyleUI.WaxSealSprite.texture, ScaleMode.StretchToFill, true, 0f, Color.white, 0f, 0f);
            GUI.Label(Circle(c, d * .5f), "✓", AtlasStyle(16f, AtlasParchment, true, TextAnchor.MiddleCenter));
        }
        else
        {
            // 未盖章：浅褐虚线空槽
            Fill(Circle(c, d * .5f - U(2f)), new Color(AtlasParchmentDeep.r, AtlasParchmentDeep.g, AtlasParchmentDeep.b, .85f), d * .5f);
            const int dots = 18;
            for (int i = 0; i < dots; i++)
            {
                float a = i * Mathf.PI * 2f / dots;
                Fill(Circle(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (d * .5f - U(1.5f)), U(1.5f)), AtlasRule, U(1.5f));
            }
        }
    }

    void DrawCompass(Rect img)
    {
        float s = Mathf.Clamp(img.height * .15f, U(58f), U(100f));
        var r = new Rect(img.xMax - s - U(14f), img.y + U(12f), s, s);
        GUI.DrawTexture(r, TideTownStyleUI.CompassRoseTexture, ScaleMode.StretchToFill, true, 0f, Color.white, 0f, 0f);
        var n = AtlasStyle(Mathf.Max(10f, s / uiK * .16f), AtlasCinnabar, true, TextAnchor.MiddleCenter);
        GUI.Label(new Rect(r.center.x - s * .2f, r.y + s * .06f, s * .4f, s * .17f), "N", n);
    }

    void DrawMapMarks(Rect img)
    {
        var playerPosition = player.transform.position;
        var playerMapPos = WorldToMapScreen(playerPosition, img);
        const string youText = "● 你在这里";
        var youStyle = AtlasStyle(12.5f, AtlasParchment, true, TextAnchor.MiddleCenter);
        var youRect = ClampRectToMap(new Rect(playerMapPos.x + U(15f), playerMapPos.y - U(11.5f), TextW(youStyle, youText) + U(22f), U(23f)), img);
        bool hasTargetRect = false;
        Rect targetRect = default;
        Vector2 targetMapPos = default;
        string targetBadgeText = "";
        var targetStyle = AtlasStyle(13f, AtlasInk, true, TextAnchor.MiddleCenter);

        // 1. 当前目标：沿可走路网画一串金色步道点（外沿一圈深褐，压在羊皮纸色底图上也看得清）
        if (TryGetStepTarget(out var stepTargetWorld, out var stepTargetName))
        {
            targetMapPos = WorldToMapScreen(stepTargetWorld, img);
            targetBadgeText = "★ 当前目标：" + stepTargetName;
            float tw = Mathf.Min(img.width - U(8f), TextW(targetStyle, targetBadgeText) + U(26f));
            targetRect = ClampRectToMap(new Rect(targetMapPos.x - tw * .5f, targetMapPos.y + U(13f), tw, U(25f)), img);
            hasTargetRect = true;

            BuildGuidePolyline(playerPosition, stepTargetWorld, mapPath);
            if (mapPath.Count >= 2)
                for (int s = 0; s + 1 < mapPath.Count; s++)
                    DrawRouteDots(WorldToMapScreen(mapPath[s], img), WorldToMapScreen(mapPath[s + 1], img));
            else DrawRouteDots(playerMapPos, targetMapPos);
        }

        // 2. 地标路牌：羊皮纸胶囊 + 深褐描边；已盖章的描边换朱砂、名字后跟朱砂 ✓
        var tagStyle = AtlasStyle(12.5f, AtlasInk, true, TextAnchor.MiddleCenter);
        tagStyle.richText = true;
        int lmCount = Landmarks.Length;
        var pinPos = new Vector2[lmCount];
        var tagRects = new Rect[lmCount];
        var tagTexts = new string[lmCount];
        var tagStamped = new bool[lmCount];
        for (int i = 0; i < lmCount; i++)
        {
            var lm = Landmarks[i];
            var lp = WorldToMapScreen(landmarkPositions[i], img);
            bool stamped = !string.IsNullOrEmpty(lm.stampId) && director != null && director.State != null && director.State.stamps.Contains(lm.stampId);
            float tw = Mathf.Min(img.width - U(8f), TextW(tagStyle, stamped ? lm.name + " ✓" : lm.name) + U(20f));
            pinPos[i] = lp;
            tagTexts[i] = stamped ? lm.name + " <color=#b23528>✓</color>" : lm.name;
            tagStamped[i] = stamped;
            tagRects[i] = new Rect(lp.x - tw * .5f, lp.y - U(29f), tw, U(22f));
        }

        // 迭代排斥重叠标签（同时避让「你在这里」和「当前目标」标签）
        float sep = U(4f);
        for (int iter = 0; iter < 6; iter++)
        {
            for (int i = 0; i < lmCount; i++)
            {
                var ri = tagRects[i];
                if (hasTargetRect) SeparateRect(ref ri, targetRect, sep);
                SeparateRect(ref ri, youRect, sep);
                for (int j = i + 1; j < lmCount; j++)
                {
                    var rj = tagRects[j];
                    SeparatePair(ref ri, ref rj, sep);
                    tagRects[j] = ClampRectToMap(rj, img);
                }
                tagRects[i] = ClampRectToMap(ri, img);
            }
        }

        for (int i = 0; i < lmCount; i++)
        {
            var lp = pinPos[i];
            var tagRect = tagRects[i];
            // 标签被挤远时，画一串深褐细点连回地标
            var badgeAnchor = new Vector2(Mathf.Clamp(lp.x, tagRect.xMin + U(6f), tagRect.xMax - U(6f)), lp.y < tagRect.center.y ? tagRect.yMin : tagRect.yMax);
            if (Vector2.Distance(lp, badgeAnchor) > U(8f))
                DrawDottedLine(lp, badgeAnchor, AtlasInkSoft, 5, U(2.6f));
            Fill(Circle(lp, U(5f)), AtlasInk, U(5f));
            Fill(Circle(lp, U(3f)), AtlasParchment, U(3f));
            DrawTag(tagRect, tagStamped[i] ? AtlasCinnabar : AtlasInk, new Color(AtlasParchment.r, AtlasParchment.g, AtlasParchment.b, .95f));
            GUI.Label(tagRect, tagTexts[i], tagStyle);
        }

        if (hasTargetRect)
        {
            Fill(Circle(targetMapPos, U(10f)), AtlasInk, U(10f));
            Fill(Circle(targetMapPos, U(8.5f)), AtlasGold, U(8.5f));
            GUI.Label(Circle(targetMapPos + new Vector2(0f, -U(.5f)), U(9f)), "★", AtlasStyle(12f, AtlasInk, true, TextAnchor.MiddleCenter));
            DrawTag(targetRect, AtlasInk, AtlasGoldSoft);
            GUI.Label(targetRect, targetBadgeText, targetStyle);
        }

        DrawPlayerPin(playerPosition, playerMapPos, img);
        DrawTag(youRect, AtlasInk, AtlasCinnabar);
        GUI.Label(youRect, youText, youStyle);
    }

    void DrawAtlasFooter(Rect r)
    {
        float cy = r.center.y;
        Fill(new Rect(r.x, r.y, r.width, Mathf.Max(1f, U(1.2f))), AtlasRule, 0f);

        // 右侧图例（从右往左排）：┅ 推荐步道  ★ 当前目标  ● 你的位置
        var leg = AtlasStyle(13f, AtlasInk, false, TextAnchor.MiddleLeft);
        float right = r.xMax;
        string[] names = { "推荐步道", "当前目标", "你的位置" };
        for (int i = 0; i < names.Length; i++)
        {
            float w = TextW(leg, names[i]);
            GUI.Label(new Rect(right - w, cy - U(12f), w + U(4f), U(24f)), names[i], leg);
            right -= w + U(7f);
            if (i == 0)
            {
                for (int d = 0; d < 3; d++) RouteDot(new Vector2(right - U(4f) - d * U(9f), cy));
                right -= U(26f);
            }
            else if (i == 1)
            {
                // ★：深褐描一圈再盖金色，压在羊皮纸上也清楚
                var star = new Rect(right - U(20f), cy - U(13f), U(20f), U(26f));
                var ink = AtlasStyle(17f, AtlasInk, true, TextAnchor.MiddleCenter);
                foreach (var o in new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f) })
                    GUI.Label(new Rect(star.x + o.x * U(1.1f), star.y + o.y * U(1.1f), star.width, star.height), "★", ink);
                GUI.Label(star, "★", AtlasStyle(17f, AtlasGold, true, TextAnchor.MiddleCenter));
                right -= U(20f);
            }
            else
            {
                var c = new Vector2(right - U(8f), cy);
                Fill(Circle(c, U(8f)), AtlasInk, U(8f));
                Fill(Circle(c, U(6.5f)), AtlasParchment, U(6.5f));
                Fill(Circle(c, U(5f)), AtlasCinnabar, U(5f));
                right -= U(16f);
            }
            right -= U(22f);
        }

        // 左侧：深褐「当前任务」小签 + 任务目标（太长就截断，不换行）
        var ebSt = AtlasStyle(11.5f, AtlasParchment, true, TextAnchor.MiddleCenter);
        var eb = new Rect(r.x, cy - U(11f), TextW(ebSt, "当前任务") + U(20f), U(22f));
        Fill(eb, AtlasWalnut, eb.height * .5f);
        GUI.Label(eb, "当前任务", ebSt);
        var goal = AtlasStyle(15f, AtlasInk, false, TextAnchor.MiddleLeft);
        goal.clipping = TextClipping.Clip;
        GUI.Label(new Rect(eb.xMax + U(12f), r.y, Mathf.Max(0f, right - eb.xMax - U(16f)), r.height), GoalText().Replace('\n', ' '), goal);
    }

    void DrawRouteDots(Vector2 a, Vector2 b)
    {
        int segs = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / U(13f)), 2, 48);
        for (int i = 0; i <= segs; i++) RouteDot(Vector2.Lerp(a, b, i / (float)segs));
    }

    void RouteDot(Vector2 p)
    {
        Fill(Circle(p, U(3.7f)), new Color(AtlasInk.r, AtlasInk.g, AtlasInk.b, .6f), U(3.7f));
        Fill(Circle(p, U(2.5f)), AtlasGold, U(2.5f));
    }

    void DrawTag(Rect r, Color border, Color fill)
    {
        float b = Mathf.Max(1f, U(1.4f));
        Fill(r, border, r.height * .5f);
        Fill(Grow(r, -b), fill, r.height * .5f - b);
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

    static void DrawDottedLine(Vector2 a, Vector2 b, Color col, int segments, float size)
    {
        for (int i = 0; i <= segments; i++)
        {
            var p = Vector2.Lerp(a, b, i / (float)segments);
            Fill(Circle(p, size * .5f), col, size * .5f);
        }
    }

    static void Fill(Rect r, Color c, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
    }

    static void Line(Rect r, Color c, float width, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, radius);
    }

    static Rect Grow(Rect r, float d) => new Rect(r.x - d, r.y - d, r.width + d * 2f, r.height + d * 2f);
    static Rect Circle(Vector2 c, float r) => new Rect(c.x - r, c.y - r, r * 2f, r * 2f);
    static float TextW(GUIStyle s, string t) => s.CalcSize(new GUIContent(t)).x;

    void DrawPlayerPin(Vector3 position, Vector2 point, Rect img)
    {
        var direction = new Vector3(Mathf.Sin(player.yaw), 0f, Mathf.Cos(player.yaw));
        Vector2 screenHeading = WorldToMapScreen(position + direction, img) - point;
        var original = GUI.matrix;
        float angle = Mathf.Atan2(screenHeading.x, -screenHeading.y) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, point);
        Fill(new Rect(point.x - U(3.2f), point.y - U(20f), U(6.4f), U(18f)), AtlasInk, U(3.2f));
        Fill(new Rect(point.x - U(1.8f), point.y - U(18.5f), U(3.6f), U(16f)), AtlasCinnabar, U(1.8f));
        GUI.matrix = original;
        Fill(Circle(point, U(9.5f)), AtlasInk, U(9.5f));
        Fill(Circle(point, U(8f)), AtlasParchment, U(8f));
        Fill(Circle(point, U(6f)), AtlasCinnabar, U(6f));
        Fill(Circle(point, U(2f)), AtlasParchment, U(2f));
    }

    string GoalText()
    {
        if (ui == null) return "";
        var t = ui.GoalText;
        return string.IsNullOrEmpty(t) ? "自由逛逛 · 靠近发光的东西按 E" : t;
    }

    GUIStyle AtlasStyle(float size, Color color, bool bold, TextAnchor anchor)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(9, Mathf.RoundToInt(size * uiK)),
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            alignment = anchor,
            wordWrap = false,
            clipping = TextClipping.Overflow,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            font = TideTownStyleUI.UiFont,
        };
        s.normal.textColor = color;
        return s;
    }
}
