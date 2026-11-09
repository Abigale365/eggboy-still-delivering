// 邮差心愿 3D 造物笺 & 一题多解成就图鉴（AIAgentCalibrationDirector）。
// 按键盘 V 键（或单手柄 Y 键）随时呼出或收起：
//   1. 【按玩家喜好实时 3D 造物（Lux3D 心愿造物笺）】：
//      - 支持键盘自由打字输入，也支持单手柄/鼠标直接点选 6 张「心愿灵感词牌」（无需打字或麦克风也能丝滑造物）：
//        * 玩法一（按喜好功能破局）：
//          - 在学校后院为小黑造出喜欢的美食（如「香烤肉骨头」「奶油生日蛋糕」）或新玩具（如「发光飞盘」「小黄鸭玩具」），无需去找旧红球也能唤出小黑并解锁【小黑的专属大厨】或【新玩具造物主】成就！
//          - 在涨潮渡口按喜好造出专属渡海载具（如「童话大黄鸭浮船」「海龟背壳浮台」），直接铺设可踩踏的 3D 浮桥过海并解锁【凭空造舟 · 乘心愿渡海】成就！
//          - 在面包房烤炉旁造出「暖阳防风炉罩」，助燃炉火并解锁【笔落成真】成就！
//        * 玩法二（装点三站心愿展台）：
//          - 在面包房橱窗、学校操场、灯塔观景台的心愿展台上，按个人喜好实时生成「星空海螺八音盒」「招财橘猫雕塑」等 3D 纪念物永久留存至结局镜头，解锁【筑梦邮差 · 焕然一新的潮汐镇】成就！
//   2. 【一题多解 · “嘀嘟！”成就图鉴】：
//      - 实时展示全岛 15 项多解成就的解锁进度与方案分支，鼓励玩家尝试不同方案刷满成就！
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public sealed class AIAgentCalibrationDirector : MonoBehaviour
{
    static AIAgentCalibrationDirector instance;

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;

    bool consoleOpen;
    RectTransform consoleCard;
    Text hardwareTelemetryText;
    Text wishFeedbackText;
    InputField promptInput;
    Text achievementGalleryText;

    readonly List<Transform> forgedRuntimeObjects = new List<Transform>();
    int forgedCounter;

    public static bool IsOpen => instance != null && instance.consoleOpen;

    public static void ToggleConsole()
    {
        if (instance == null) return;
        instance.SetConsoleOpen(!instance.consoleOpen);
    }

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        instance = this;
        player = p;
        ui = u;
        world = w;
        director = d;
        state = s;
    }

    void SetConsoleOpen(bool open)
    {
        EnsureConsoleUI();
        if (consoleCard == null) return;

        consoleOpen = open;
        consoleCard.gameObject.SetActive(open);
        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshAchievementGallery();
        }
    }

    void EnsureConsoleUI()
    {
        if (consoleCard != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (root == null) return;

        consoleCard = TideTownStyleUI.CreateParchmentCard(root, "PostmanWishAndTrophySheet", 660f, 560f, 18);
        consoleCard.anchorMin = consoleCard.anchorMax = new Vector2(1f, 0.5f);
        consoleCard.pivot = new Vector2(1f, 0.5f);
        consoleCard.anchoredPosition = new Vector2(-24f, 4f);

        TideTownStyleUI.CreateText(
            consoleCard,
            "邮差心愿 3D 造物笺 & 一题多解成就图鉴 (V / 手柄Y)",
            18,
            TideTownStyleUI.InkDark,
            TextAnchor.MiddleLeft,
            new Vector2(0f, 246f),
            new Vector2(608f, 28f),
            true);

        // 1. 全设备兼容状态栏（单手柄 + 键鼠 + 麦克风 + 摄像头手势）
        hardwareTelemetryText = TideTownStyleUI.CreateText(
            consoleCard,
            "",
            12,
            TideTownStyleUI.InkMuted,
            TextAnchor.UpperLeft,
            new Vector2(0f, 206f),
            new Vector2(608f, 44f),
            false);

        // 2. 心愿造物说明与反馈
        wishFeedbackText = TideTownStyleUI.CreateText(
            consoleCard,
            "写下你的心愿（或直接点选下方灵感词牌），即可按你的喜好实时生成 3D 物件：\n• 给小黑造美食/新玩具可替代找红球 · 在渡口造浮船可乘船过海 · 在展台造摆件可永久装点小镇",
            13,
            TideTownStyleUI.InkDark,
            TextAnchor.UpperLeft,
            new Vector2(0f, 154f),
            new Vector2(608f, 46f),
            false);

        // 3. 自由心愿输入框 + 立即造物按钮
        var inputBg = TideTownStyleUI.CreatePanel(consoleCard, "WishPromptInputBg", 412f, 36f, TideTownStyleUI.SlipColor, true);
        inputBg.rectTransform.anchoredPosition = new Vector2(-98f, 104f);
        var inputText = TideTownStyleUI.CreateText(
            inputBg.transform,
            "",
            14,
            TideTownStyleUI.InkDark,
            TextAnchor.MiddleLeft,
            Vector2.zero,
            new Vector2(392f, 30f),
            false);
        var placeholder = TideTownStyleUI.CreateText(
            inputBg.transform,
            "输入你想造的 3D 物件（如：香烤肉骨头、发光飞盘、大黄鸭浮船、海螺八音盒）...",
            12,
            TideTownStyleUI.InkMuted,
            TextAnchor.MiddleLeft,
            Vector2.zero,
            new Vector2(392f, 30f),
            false);

        promptInput = inputBg.gameObject.AddComponent<InputField>();
        promptInput.textComponent = inputText;
        promptInput.placeholder = placeholder;
        promptInput.text = "香喷喷的烤肉骨头";

        TideTownStyleUI.CreateWarmButton(
            consoleCard,
            "按心愿实时 3D 造物",
            new Vector2(210f, 104f),
            new Vector2(188f, 36f),
            true,
            () => SpawnLiveWishObjectInWorld(promptInput != null ? promptInput.text : "香喷喷的烤肉骨头"));

        // 4. 六张单手柄/鼠标一键「心愿灵感词牌」（无需打字也能按喜好造物破局！）
        string[] cardLabels =
        {
            "词牌：香烤肉骨头 (哄小黑)",
            "词牌：发光飞盘玩具 (哄小黑)",
            "词牌：大黄鸭浮船 (渡海)",
            "词牌：海龟背壳浮台 (渡海)",
            "词牌：暖阳防风炉罩 (助燃)",
            "词牌：星空海螺八音盒 (展台)"
        };
        string[] cardPrompts =
        {
            "香喷喷的烤肉骨头",
            "发光飞盘玩具",
            "童话大黄鸭浮船",
            "海龟背壳浮台",
            "暖阳防风炉罩",
            "星空海螺八音盒"
        };

        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            int row = i / 3;
            int col = i % 3;
            float x = -204f + col * 204f;
            float y = 54f - row * 40f;
            TideTownStyleUI.CreateWarmButton(
                consoleCard,
                cardLabels[i],
                new Vector2(x, y),
                new Vector2(194f, 32f),
                false,
                () =>
                {
                    if (promptInput != null) promptInput.text = cardPrompts[idx];
                    SpawnLiveWishObjectInWorld(cardPrompts[idx]);
                });
        }

        // 5. 一题多解 · 15 项成就图鉴展示板
        var galleryBg = TideTownStyleUI.CreatePanel(consoleCard, "AchievementGalleryBg", 608f, 198f, new Color32(0xec, 0xdf, 0xc6, 255), true);
        galleryBg.rectTransform.anchoredPosition = new Vector2(0f, -92f);

        achievementGalleryText = TideTownStyleUI.CreateText(
            galleryBg.transform,
            "",
            12,
            TideTownStyleUI.InkDark,
            TextAnchor.UpperLeft,
            Vector2.zero,
            new Vector2(584f, 182f),
            false);

        // 6. 底部辅助按钮（开关内置摄像头手势感应 + 关闭面板）
        TideTownStyleUI.CreateWarmButton(
            consoleCard,
            "开关摄像头 AR 挥手感应",
            new Vector2(-150f, -228f),
            new Vector2(240f, 34f),
            false,
            OnClickToggleWebcam);

        TideTownStyleUI.CreateWarmButton(
            consoleCard,
            "收起心愿笺 (V / 手柄Y)",
            new Vector2(150f, -228f),
            new Vector2(240f, 34f),
            false,
            () => SetConsoleOpen(false));

        consoleCard.gameObject.SetActive(false);
    }

    void OnClickToggleWebcam()
    {
        if (HardwareAndSensorHub.Instance == null) return;
        bool active = HardwareAndSensorHub.Instance.ToggleBuiltInWebcam();
        ShadowAndChimeSystem.PlayProceduralChime(active ? 659.25f : 440f, 0.22f);
        if (wishFeedbackText != null)
        {
            wishFeedbackText.text = active
                ? "已开启内置摄像头 AR 手势感应！登上灯塔顶层时，对着镜头挥手即可闪灯呼唤山那边的人。"
                : "已关闭摄像头手势感应（你依然可以随时用空格键、E 键或手柄按键挥手闪灯）。";
        }
    }

    void RefreshAchievementGallery()
    {
        if (achievementGalleryText == null) return;
        var sys = TideTownAchievementSystem.Instance;
        if (sys == null)
        {
            achievementGalleryText.text = "一题多解成就系统准备中...";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"【一题多解成就图鉴 · 已解锁 {sys.UnlockedCount} / {sys.TotalCount} 项】（尝试不同方案解谜即可触发“嘀嘟！”刷满成就）：");

        var defs = sys.AllDefinitions;
        for (int i = 0; i < defs.Count; i++)
        {
            var d = defs[i];
            bool ok = sys.IsUnlocked(d.id);
            string mark = ok ? "★ [已达成]" : "☆ [待探索]";
            sb.Append($"  {mark} {d.title}（{d.category}）");
            if (i % 2 == 1 || i == defs.Count - 1) sb.AppendLine();
            else sb.Append("    |  ");
        }

        achievementGalleryText.text = sb.ToString();
    }

    // --- 按玩家个人喜好实时生成 3D 物件，并根据所在场景触发一题多解互动与成就 ---
    public void SpawnLiveWishObjectInWorld(string rawDescription)
    {
        if (player == null) return;
        string description = string.IsNullOrWhiteSpace(rawDescription) ? "星空海螺八音盒" : rawDescription.Trim();
        forgedCounter++;

        // 1. 若配置了 AHOLO_API_KEY，异步向 https://api.aholo3d.cn 提交云端图生 3D 任务
        string apiKey = Environment.GetEnvironmentVariable("AHOLO_API_KEY") ?? "";
        if (!string.IsNullOrEmpty(apiKey))
        {
            StartCoroutine(RequestCloudLux3DAsync(description, apiKey));
        }

        var worldMech = FindObjectOfType<WorldMechanismDirector>();
        Vector3 pPos = player.transform.position;

        // 判断心愿语义类别
        bool isDogFood = description.Contains("骨") || description.Contains("肉") || description.Contains("蛋糕") ||
                         description.Contains("肠") || description.Contains("鱼") || description.Contains("饼") ||
                         description.Contains("食") || description.Contains("饭") || description.Contains("奶");
        bool isDogToy = description.Contains("飞盘") || description.Contains("玩具") || description.Contains("球") ||
                        (description.Contains("小黄鸭") && !description.Contains("船"));
        bool isWaterBoat = description.Contains("船") || description.Contains("筏") || description.Contains("浮") ||
                           description.Contains("桥") || description.Contains("海龟") || description.Contains("艇");
        bool isOvenHelper = description.Contains("炉") || description.Contains("火") || description.Contains("风罩") ||
                            description.Contains("风箱") || description.Contains("柴");

        // A. 如果玩家造的是渡海浮船/海龟浮台（或者正站在涨潮渡口海峡旁）
        Vector3 channelPos = worldMech != null ? worldMech.GetCrossingChannelCenter() : new Vector3(15.3f, 1.35f, 11.7f);
        if (isWaterBoat || Vector3.Distance(pPos, channelPos) < 7.5f && !isDogFood && !isDogToy)
        {
            SpawnCustomWishBoatBridge(description, channelPos, worldMech);
            RefreshAchievementGallery();
            return;
        }

        // B. 如果玩家造的是给小黑的美食或新玩具（或者站在学校狗碗附近）
        Vector3 bowlPos = worldMech != null ? worldMech.GetDogBowlPosition() : new Vector3(17.5f, 3.05f, -5.2f);
        if (isDogFood || isDogToy || (Vector3.Distance(pPos, bowlPos) < 9.5f && state != null && state.step.StartsWith("S")))
        {
            bool treatAsFood = isDogFood || !isDogToy;
            SpawnCustomGiftForDog(description, treatAsFood, bowlPos);
            RefreshAchievementGallery();
            return;
        }

        // C. 如果玩家造的是面包房助燃防风炉罩（或者站在面包房烤炉旁）
        Vector3 ovenPos = new Vector3(-11.5f, 1.3f, -7.8f);
        if (isOvenHelper && Vector3.Distance(pPos, ovenPos) < 11f)
        {
            SpawnCustomOvenHelper(description, ovenPos);
            RefreshAchievementGallery();
            return;
        }

        // D. 默认/心愿展台模式：在最近的三站「心愿展台」（或玩家正前方）生成高精 3D 纪念摆件并永久装点小镇！
        SpawnCustomTownKeepsake(description, worldMech);
        RefreshAchievementGallery();
    }

    void SpawnCustomGiftForDog(string description, bool isFood, Vector3 bowlPos)
    {
        string luxModel = isFood ? "Bakery_basket" : "D6_救生圈_v2";
        float size = isFood ? 0.48f : 0.42f;

        Vector3 spawnPos = Vector3.Distance(player.transform.position, bowlPos) < 12f
            ? bowlPos + new Vector3(0.28f, 0.12f, 0.22f)
            : player.transform.position + new Vector3(Mathf.Sin(player.yaw), 0f, Mathf.Cos(player.yaw)) * 1.4f;
        if (world != null) spawnPos.y = world.GroundAt(spawnPos.x, spawnPos.z, spawnPos.y);

        var holder = new GameObject("WishDogGift_" + forgedCounter + "_" + description);
        holder.transform.SetParent(transform, false);
        holder.transform.position = spawnPos;

        var visual = TideTownStyleUI.CloneSceneLux3D(luxModel, size, false);
        if (visual != null)
        {
            visual.transform.SetParent(holder.transform, false);
            visual.transform.localPosition = Vector3.zero;
        }
        AttachWarmGlow(holder.transform, isFood ? new Color32(255, 195, 110, 255) : new Color32(130, 230, 255, 255), 3.2f);
        forgedRuntimeObjects.Add(holder.transform);

        if (director != null)
            director.CompleteSchoolWithCustomGift(description, isFood);

        if (wishFeedbackText != null)
            wishFeedbackText.text = "已按你的心愿造出 3D 礼物「" + description + "」放在小黑碗旁！小黑闻着味儿跑出来了！";
    }

    void SpawnCustomWishBoatBridge(string description, Vector3 channelPos, WorldMechanismDirector worldMech)
    {
        var holder = new GameObject("WishBoatBridge_" + forgedCounter + "_" + description);
        holder.transform.SetParent(transform, false);
        holder.transform.position = new Vector3(channelPos.x, channelPos.y + 0.08f, channelPos.z);

        var boat = TideTownStyleUI.CloneSceneLux3D("S9_小船_分享包版", 2.35f, true);
        if (boat != null)
        {
            boat.transform.SetParent(holder.transform, false);
            boat.transform.localPosition = new Vector3(0.35f, 0f, 0.2f);
        }

        var buoy = TideTownStyleUI.CloneSceneLux3D("D6_救生圈_v2", 0.72f, true);
        if (buoy != null)
        {
            buoy.transform.SetParent(holder.transform, false);
            buoy.transform.localPosition = new Vector3(-0.55f, 0.28f, -0.25f);
        }

        AttachWarmGlow(holder.transform, new Color32(255, 220, 125, 255), 5.0f);
        forgedRuntimeObjects.Add(holder.transform);

        if (worldMech != null)
            worldMech.RaiseDockWinch(false, true);
        else
            TideTownAchievementSystem.Unlock("wish_boat");

        if (ui != null)
            ui.ShowToast("心愿成真！水面浮起了你亲手造出的「" + description + "」，现在可以直接踩着它渡海了！", 4.0f);
        if (wishFeedbackText != null)
            wishFeedbackText.text = "已在涨潮渡口为你实时造出可踩踏的 3D 渡海载具：「" + description + "」！";
    }

    void SpawnCustomOvenHelper(string description, Vector3 ovenPos)
    {
        var holder = new GameObject("WishOvenHelper_" + forgedCounter + "_" + description);
        holder.transform.SetParent(transform, false);
        Vector3 spawnPos = ovenPos + new Vector3(0.85f, 0f, 0.65f);
        if (world != null) spawnPos.y = world.GroundAt(spawnPos.x, spawnPos.z, ovenPos.y);
        holder.transform.position = spawnPos;

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M1_鲁班三环风门机关", 0.62f);
        mech.transform.SetParent(holder.transform, false);
        mech.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        AttachWarmGlow(holder.transform, new Color32(255, 175, 90, 255), 4.5f);
        forgedRuntimeObjects.Add(mech.transform);

        var fx = FindObjectOfType<BakeryEffects>();
        if (state != null && director != null)
        {
            if (!state.flags.Contains("oven_lit"))
            {
                if (!state.flags.Contains("matches_taken")) state.flags.Add("matches_taken");
                if (!state.flags.Contains("wood_loaded")) state.flags.Add("wood_loaded");
                if (!state.flags.Contains("puzzle_bakery_lock")) state.flags.Add("puzzle_bakery_lock");
                director.LightOven(fx);
            }
            if (fx != null) director.BoostOvenBellows(fx);
        }
        TideTownAchievementSystem.Unlock("word_fire");
        if (wishFeedbackText != null)
            wishFeedbackText.text = "已在烤炉旁造出「" + description + "」，炉膛里的暖火瞬间升腾了起来！";
    }

    void SpawnCustomTownKeepsake(string description, WorldMechanismDirector worldMech)
    {
        float yaw = player.yaw;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 spawnPos = player.transform.position + fwd * 1.9f;
        if (world != null) spawnPos.y = world.GroundAt(spawnPos.x, spawnPos.z, player.transform.position.y);

        Transform targetPedestal = null;
        if (worldMech != null)
        {
            var ped = worldMech.GetNearestWishPedestal(player.transform.position, out float pedDist);
            if (ped != null && pedDist < 14f)
            {
                targetPedestal = ped;
                spawnPos = ped.position + new Vector3(0f, 0.72f, 0f);
            }
        }

        var holder = new GameObject("WishTownKeepsake_" + forgedCounter + "_" + description);
        holder.transform.SetParent(targetPedestal != null ? targetPedestal : transform, false);
        holder.transform.position = spawnPos;
        holder.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);

        string baseMech = "M4_守塔人黄铜三轴星盘";
        string luxAccent = "Street_E1";
        if (description.Contains("猫") || description.Contains("狗") || description.Contains("偶"))
        {
            baseMech = "M2_小黑爪印转盘木匣";
            luxAccent = "S_doghouse";
        }
        else if (description.Contains("车") || description.Contains("船") || description.Contains("锚"))
        {
            baseMech = "M3_潮汐船坞齿轮绞盘";
            luxAccent = "D6_救生圈_v2";
        }

        var mechGo = TideTownStyleUI.BuildArticulatedMechanism3D(baseMech, 0.68f);
        mechGo.transform.SetParent(holder.transform, false);
        mechGo.transform.localPosition = new Vector3(0f, 0.48f, 0f);

        var propGo = TideTownStyleUI.CloneSceneLux3D(luxAccent, 0.56f, false);
        if (propGo != null)
        {
            propGo.transform.SetParent(holder.transform, false);
            propGo.transform.localPosition = Vector3.zero;
        }

        AttachWarmGlow(holder.transform, new Color32(255, 215, 130, 255), 4.2f);
        forgedRuntimeObjects.Add(mechGo.transform);

        player.PlayEmote("hop");
        TideTownAchievementSystem.Unlock("wish_town");

        if (ui != null)
            ui.ShowToast("心愿造物完成！「" + description + "」已永久陈列在潮汐镇展台上！", 3.8f);
        if (wishFeedbackText != null)
            wishFeedbackText.text = "已按你的喜好造出 3D 纪念物：「" + description + "」，它将永久装点在小镇展台上并保留至结局！";
    }

    static void AttachWarmGlow(Transform parent, Color32 color, float range)
    {
        var lightGo = new GameObject("WishWarmGlow");
        lightGo.transform.SetParent(parent, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        var pt = lightGo.AddComponent<Light>();
        pt.type = LightType.Point;
        pt.color = color;
        pt.range = range;
        pt.intensity = 2.0f;
    }

    System.Collections.IEnumerator RequestCloudLux3DAsync(string prompt, string apiKey)
    {
        string url = "https://api.aholo3d.cn/api/open/v1/flux/2b/multimodal-to-image";
        string body = "{\"prompt\":\"Hand-painted cozy 3D island keepsake, " + prompt.Replace("\"", "") + ", white background, PBR\",\"width\":1024,\"height\":1024}";
        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Aholo-Key", apiKey);
            yield return req.SendWebRequest();
        }
    }

    void Update()
    {
        if (ui == null || !ui.inputClose) return;

        bool typing = promptInput != null && promptInput.isFocused;
        if (!typing && Input.GetKeyDown(KeyCode.V))
        {
            SetConsoleOpen(!consoleOpen);
        }

        float dt = Time.deltaTime;
        for (int i = forgedRuntimeObjects.Count - 1; i >= 0; i--)
        {
            var m = forgedRuntimeObjects[i];
            if (m == null) { forgedRuntimeObjects.RemoveAt(i); continue; }
            var r0 = TideTownStyleUI.FindDeepChild(m, "Ring_0");
            var r1 = TideTownStyleUI.FindDeepChild(m, "Ring_1");
            if (r0 != null) r0.Rotate(0f, 0f, 32f * dt, Space.Self);
            if (r1 != null) r1.Rotate(0f, 0f, -42f * dt, Space.Self);
        }

        if (!consoleOpen || hardwareTelemetryText == null) return;

        var hub = HardwareAndSensorHub.Instance;
        if (hub != null)
        {
            string padStr = hub.Gamepad1Connected
                ? $"已连接单手柄 (左摇杆 {hub.Pad1LeftStick.x:F1},{hub.Pad1LeftStick.y:F1} | 与键鼠无缝共存)"
                : "当前使用键鼠模式 (随时插入任意 1 个手柄即可无缝切换)";
            string micStr = hub.MicActive
                ? $"已就绪 (实时声压 {Mathf.RoundToInt(hub.MicLoudness * 100f)}% · 无麦时可用 F 键/鼠标擦拭/手柄 RB 平替)"
                : "未接麦克风 (已自动启用鼠标擦拭 / F 键 / 手柄 RB 无缝平替)";
            string camStr = hub.CamOpen
                ? $"已开启 (挥手速度 {hub.HandWaveSpeed:F2} · 可在灯塔顶对着镜头挥手闪灯)"
                : "待机中 (可点下方按钮开启摄像头挥手，或直接用空格/E/手柄按键)";

            hardwareTelemetryText.text =
                "• 操控模式：" + padStr + "\n" +
                "• 吹气与手势：麦克风 [" + micStr + "]  |  摄像头 [" + camStr + "]";
        }
    }
}
