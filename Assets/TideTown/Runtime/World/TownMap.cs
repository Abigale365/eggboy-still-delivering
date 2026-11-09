// WP-16：地图与 3D 地面引路微光。
// 1. 按 M 打开地图：显示全镇地标名称（邮局、面包店、学校、操场、狗窝、船棚、灯塔、山顶新邮局）、当前目标高亮与引导虚线；
// 2. 平时漫游时：移除遮挡视线的 2D 飘动标签，改用贴地的「3D 金色引路光点流 + 目标物暖金光环」，像序章一样自然引导玩家脚下路线与互动目标。
using System;
using UnityEngine;

public class TownMap : MonoBehaviour
{
    [Serializable] class MapCam { public string name; public float[] pos, euler; public float fov; public int width, height; }
    [Serializable] class MapCameraList { public MapCam[] cameras; }

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
        new LandmarkInfo("② 潮汐镇学校", "第二站 · 阿萤与小黑", new Vector3(13.53f, 5.6f, -2.02f), "school", new Color(0.26f, 0.56f, 0.42f, 0.94f)),
        new LandmarkInfo("学校操场", "秋千 / 红球 / 滑梯", new Vector3(17.50f, 3.8f, -5.60f), "", new Color(0.30f, 0.52f, 0.36f, 0.88f)),
        new LandmarkInfo("学校后院 · 狗窝", "拖鞋 / 狗碗", new Vector3(21.76f, 4.1f, 1.95f), "", new Color(0.48f, 0.38f, 0.28f, 0.88f)),
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

    // 旧 Prototype 场景没有剧情系统，也给它一个能打开的地图
    public void Init(EggboyPlayer p, EggboyUI u) { Init(p, u, null, null); }

    public bool CanOpen()
    {
        if (player == null || ui == null) return false;
        if (ui.LetterOpen) return false;
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
        if (gate != null && mapLock != null) gate.Release(mapLock);
        mapLock = null;
        if (player != null) player.locked = false;
    }

    void Update()
    {
        if (player == null || ui == null) return;
        UpdateGroundGuide3D();
        if (ui.LetterOpen)
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
        // 自动审计截图模式或过场/暂停/序章已有 mailboxTrail 时隐藏动态地面引导
        bool canShow = ui != null && ui.inputClose
            && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen
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

        // 地面金色引路光点流：从角色脚下前方 1.4m 沿着地面指向当前任务目标
        var pPos = player.transform.position;
        Vector3 flatDelta = new Vector3(tgt.x - pPos.x, 0f, tgt.z - pPos.z);
        float totalDist = flatDelta.magnitude;
        Vector3 dir = totalDist > 0.01f ? flatDelta / totalDist : player.transform.forward;

        for (int i = 0; i < GuideDotCount; i++)
        {
            float d = 1.35f + i * 1.45f;
            if (d >= totalDist - 0.8f || totalDist < 2.2f)
            {
                if (guideDots[i].gameObject.activeSelf) guideDots[i].gameObject.SetActive(false);
                continue;
            }
            if (!guideDots[i].gameObject.activeSelf) guideDots[i].gameObject.SetActive(true);

            Vector3 sample = pPos + dir * d;
            float gy = island != null ? island.GroundAt(sample.x, sample.z, Mathf.Max(pPos.y, tgt.y) + 3f) : pPos.y;
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

            // 1. 若有当前任务目标，画一条从「你在这里」指向「当前目标」的金色引导虚线
            if (TryGetStepTarget(out var stepTargetWorld, out var stepTargetName))
            {
                var targetMapPos = WorldToMapScreen(stepTargetWorld, img);
                DrawDottedLine(playerMapPos, targetMapPos, new Color(1f, 0.86f, 0.28f, 0.92f), 18);
                DrawMapTargetPin(targetMapPos, "★ 当前目标：" + stepTargetName, font);
            }

            // 2. 绘制全镇所有建筑/地标名称标签（仅在打开地图 M 时显示，平时走路绝不遮挡视野）
            var tagStyle = MapTagStyle(font);
            foreach (var lm in Landmarks)
            {
                var lp = WorldToMapScreen(lm.worldPos, img);
                bool stamped = !string.IsNullOrEmpty(lm.stampId) && director != null && director.State != null && director.State.stamps.Contains(lm.stampId);
                string text = stamped ? (lm.name + " ✓") : lm.name;
                float tw = Mathf.Max(84f, text.Length * 13.5f + 16f);
                var tagRect = new Rect(lp.x - tw * 0.5f, lp.y - 26f, tw, 22f);

                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(lp.x - 4f, lp.y - 4f, 8f, 8f), Texture2D.whiteTexture);
                GUI.color = stamped ? new Color(0.35f, 0.88f, 0.48f, 1f) : new Color(0.98f, 0.90f, 0.72f, 1f);
                GUI.DrawTexture(new Rect(lp.x - 3f, lp.y - 3f, 6f, 6f), Texture2D.whiteTexture);

                GUI.color = new Color(0.06f, 0.05f, 0.04f, 0.88f);
                GUI.DrawTexture(new Rect(tagRect.x - 1f, tagRect.y - 1f, tagRect.width + 2f, tagRect.height + 2f), Texture2D.whiteTexture);
                GUI.color = stamped ? new Color(0.18f, 0.46f, 0.26f, 0.92f) : lm.badgeColor;
                GUI.DrawTexture(tagRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(tagRect, text, tagStyle);
            }

            // 3. 玩家位置红点
            GUI.color = new Color(1f, 0.18f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(playerMapPos.x - 6f, playerMapPos.y - 6f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(playerMapPos.x - 8f, playerMapPos.y - 8f, 16f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(playerMapPos.x - 8f, playerMapPos.y + 6f, 16f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(playerMapPos.x - 8f, playerMapPos.y - 8f, 2f, 16f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(playerMapPos.x + 6f, playerMapPos.y - 8f, 2f, 16f), Texture2D.whiteTexture);

            var youRect = new Rect(playerMapPos.x + 10f, playerMapPos.y - 12f, 88f, 24f);
            GUI.color = new Color(0.85f, 0.16f, 0.12f, 0.94f);
            GUI.DrawTexture(youRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(youRect, "● 你在这里", tagStyle);
        }

        GUI.Label(new Rect(panel.x + 18, panel.yMax - 34, panel.width - 36, 26),
            GoalText() + "   |   提示：平时走路时可跟随脚下的【金色地面引路微光】前往发光目标点", Label(font));
        GUI.skin.font = old;
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
