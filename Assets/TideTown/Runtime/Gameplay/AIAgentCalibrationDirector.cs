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
        if (!instance.consoleOpen && instance.OtherOverlayOpen()) return;
        instance.SetConsoleOpen(!instance.consoleOpen);
    }

    // 暂停、标题、读信、旧物端详、木匣、取景框开着时，心愿造物笺不叠上去（会抢鼠标、盖住别的弹窗）
    bool OtherOverlayOpen()
    {
        return ui != null && (ui.Paused || ui.TitleShowing || ui.LetterOpen
            || ObjectInspectModal.IsOpen || WoodenMechanismPuzzleModal.IsOpen || AvantGardeGameplayHub.AnyModalOpen);
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
            MenuController.NoteScriptUnlock();
            RefreshAchievementGallery();
        }
    }

    // 审计用：不按 V 也能开关心愿造物笺（验证打开时不会自动弹出暂停菜单）
    public static void DebugSetConsole(bool open) { if (instance != null) instance.SetConsoleOpen(open); }

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

    // --- 心愿造物：功能由"在哪 + 哪一类"决定（写死、可测），外观交给 Lux3D（先保底，生成好原位替换） ---
    public Lux3DSpawn LastWishSpawn { get; private set; }
    public string LastWishKind { get; private set; } = "";

    static bool HasAny(string text, params string[] keys)
    {
        foreach (var k in keys) if (text.Contains(k)) return true;
        return false;
    }

    public void SpawnLiveWishObjectInWorld(string rawDescription)
    {
        if (player == null) return;
        string description = string.IsNullOrWhiteSpace(rawDescription) ? "星空海螺八音盒" : rawDescription.Trim();
        if (description.Length > 40) description = description.Substring(0, 40);
        forgedCounter++;

        var worldMech = FindObjectOfType<WorldMechanismDirector>();
        Vector3 pPos = player.transform.position;

        bool isDogFood = HasAny(description, "骨", "肉", "蛋糕", "肠", "鱼", "饼", "食", "饭", "奶", "糖", "面包", "鸡腿", "罐头");
        bool isBoat = HasAny(description, "船", "筏", "浮", "桥", "海龟", "艇", "舟", "鲸");
        bool isDogToy = !isBoat && HasAny(description, "飞盘", "玩具", "球", "鸭", "娃娃", "玩偶", "铃铛");
        bool isFire = HasAny(description, "柴", "木头", "火", "炉", "炭");

        Vector3 channelPos = worldMech != null ? worldMech.GetCrossingChannelCenter() : new Vector3(15.3f, 1.35f, 11.7f);
        Vector3 bowlPos = worldMech != null ? worldMech.GetDogBowlPosition() : new Vector3(17.5f, 3.05f, -5.2f);
        Vector3 ovenPos = new Vector3(-11.5f, 1.3f, -7.8f);
        bool nearChannel = Vector3.Distance(pPos, channelPos) < 7.5f;
        bool nearBowl = Vector3.Distance(pPos, bowlPos) < 9.5f && state != null && state.step.StartsWith("S");
        bool nearOven = Vector3.Distance(pPos, ovenPos) < 11f;

        if (isBoat || (nearChannel && !isDogFood && !isDogToy && !isFire)) SpawnWishBoat(description, channelPos, worldMech);
        else if (isDogFood || isDogToy || nearBowl) SpawnWishDogGift(description, isDogFood || !isDogToy, bowlPos);
        else if (isFire && nearOven) SpawnWishFirewood(description, ovenPos);
        else SpawnWishKeepsake(description, worldMech);
        RefreshAchievementGallery();
    }

    Lux3DSpawn SpawnWish(string description, string category, Vector3 pos, Quaternion rot, Transform parent, Color32 glow, float glowRange)
    {
        var lux = Lux3DService.Ensure();
        var entry = lux.Match(description, category);
        var spawn = lux.Spawn(description, entry, pos, rot, parent);
        spawn.root.name = "Wish_" + category + "_" + forgedCounter;
        AttachWarmGlow(spawn.root.transform, glow, glowRange);
        LastWishSpawn = spawn;
        LastWishKind = category;
        if (player != null) player.PlayEmote("give");
        return spawn;
    }

    string WaitNote(Lux3DSpawn s)
    {
        if (s.state == "generating") return "信已经寄出去了，真正的样子还在路上（大约几分钟），先用这一件。";
        if (s.state == "cached") return "这件以前造过，马上就换上。";
        if (s.state == "capped") return "这一趟能寄的心愿用完了，先用镇上现成的这一件。";
        return "";
    }

    void SpawnWishDogGift(string description, bool isFood, Vector3 bowlPos)
    {
        Vector3 spawnPos = Vector3.Distance(player.transform.position, bowlPos) < 12f
            ? bowlPos + new Vector3(0.28f, 0.02f, 0.22f)
            : player.transform.position + new Vector3(Mathf.Sin(player.yaw), 0f, Mathf.Cos(player.yaw)) * 1.4f;
        if (world != null) spawnPos.y = world.GroundAt(spawnPos.x, spawnPos.z, spawnPos.y + 1f);

        var s = SpawnWish(description, isFood ? "food" : "toy", spawnPos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), transform,
            isFood ? new Color32(255, 195, 110, 255) : new Color32(130, 230, 255, 255), 3.2f);

        // 只有放在狗碗附近才算哄到小黑；在别处造出来就只是一件摆在地上的东西
        if (director != null && Vector3.Distance(spawnPos, bowlPos) < 3f)
            director.CompleteSchoolWithCustomGift(description, isFood);

        if (wishFeedbackText != null)
            wishFeedbackText.text = "「" + description + "」放在了小黑的碗旁。" + WaitNote(s);
    }

    void SpawnWishBoat(string description, Vector3 channelPos, WorldMechanismDirector worldMech)
    {
        var pos = new Vector3(channelPos.x, Mathf.Max(channelPos.y, WaterLevel.StaticY) - 0.05f, channelPos.z);
        // 船身顺着过海的方向，正好补在浮箱的缺口上
        Vector3 dir = worldMech != null ? worldMech.CrossingDirection : (channelPos - player.transform.position);
        dir.y = 0f;
        var rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
        var s = SpawnWish(description, "boat", pos, rot, transform, new Color32(255, 220, 125, 255), 5.0f);
        s.root.AddComponent<WishBob>();
        // 能踩的范围固定成一块顺着过海方向的甲板，不跟模型外形走：换成真模型后照样过得去
        var deck = s.root.GetComponent<BoxCollider>();
        if (deck == null) deck = s.root.AddComponent<BoxCollider>();
        int groundLayer = LayerMask.NameToLayer("Ground");
        s.root.layer = groundLayer >= 0 ? groundLayer : 6;
        deck.center = new Vector3(0f, 0.1f, 0f);
        deck.size = new Vector3(1.1f, 0.4f, 2.4f);
        AlignLongAxisToForward(s.root.transform);
        s.onSwapped = sp => { if (sp.root != null) AlignLongAxisToForward(sp.root.transform); };

        if (worldMech != null) worldMech.RaiseDockWinch(false, true);
        else TideTownAchievementSystem.Unlock("wish_boat");

        if (wishFeedbackText != null)
            wishFeedbackText.text = "「" + description + "」浮在了渡口的水面上，可以踩着过海。" + WaitNote(s);
    }

    // 让模型最长的一边顺着父节点的前方（本地 Z）
    static void AlignLongAxisToForward(Transform root)
    {
        var model = root.Find("Model");
        if (model == null) return;
        var keep = root.rotation;
        root.rotation = Quaternion.identity;
        var b = Lux3DModelFit.WorldBounds(model.gameObject);
        if (b.size.x > b.size.z * 1.1f) model.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.localRotation;
        root.rotation = keep;
    }

    // 在烤炉旁许愿要柴 / 火：给你一捆柴并塞进炉膛（省掉去后院抱柴），点火仍然要火柴和风门木匣
    void SpawnWishFirewood(string description, Vector3 ovenPos)
    {
        Vector3 spawnPos = ovenPos + new Vector3(0.85f, 0f, 0.65f);
        if (world != null) spawnPos.y = world.GroundAt(spawnPos.x, spawnPos.z, ovenPos.y + 1f);
        var s = SpawnWish(description, "fire", spawnPos, Quaternion.identity, transform, new Color32(255, 175, 90, 255), 4.5f);

        var fx = FindObjectOfType<BakeryEffects>();
        if (state != null && director != null)
        {
            if (state.flags.Contains("oven_lit")) { if (fx != null) director.BoostOvenBellows(fx); }
            else if (!state.flags.Contains("wood_loaded")) { director.TakeWood(); director.LoadWood(); }
        }
        TideTownAchievementSystem.Unlock("word_fire");
        if (wishFeedbackText != null)
            wishFeedbackText.text = "「" + description + "」落在了烤炉旁，炉膛里有柴了。" + WaitNote(s);
    }

    void SpawnWishKeepsake(string description, WorldMechanismDirector worldMech)
    {
        float yaw = player.yaw;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 spawnPos = player.transform.position + fwd * 1.9f;
        if (world != null) spawnPos.y = world.GroundAt(spawnPos.x, spawnPos.z, player.transform.position.y + 1f);

        Transform parent = transform;
        if (worldMech != null)
        {
            var ped = worldMech.GetNearestWishPedestal(player.transform.position, out float pedDist);
            if (ped != null && pedDist < 14f)
            {
                parent = ped;
                spawnPos = ped.position + new Vector3(0f, 0.72f, 0f);
            }
        }
        var s = SpawnWish(description, "keepsake", spawnPos, Quaternion.LookRotation(-fwd, Vector3.up), parent, new Color32(255, 215, 130, 255), 4.2f);
        TideTownAchievementSystem.Unlock("wish_town");
        if (wishFeedbackText != null)
            wishFeedbackText.text = "「" + description + "」留在了潮汐镇，会一直摆到结局。" + WaitNote(s);
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

    void Update()
    {
        if (ui == null || !ui.inputClose) return;

        bool typing = promptInput != null && promptInput.isFocused;
        // V 开关心愿造物笺（笺已经开着时 V 随时能关）。Esc 也能关：Esc 在笺开着时不会触发暂停，不关的话没有别的办法用键盘退出
        if (consoleOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            SetConsoleOpen(false);
        }
        else if (!typing && Input.GetKeyDown(KeyCode.V))
        {
            if (consoleOpen || !OtherOverlayOpen()) SetConsoleOpen(!consoleOpen);
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
                : "待启用 (旧物特写中可开启，也可用鼠标擦拭 / F 键 / 手柄 RB)";
            string camStr = hub.CamOpen
                ? $"已开启 (挥手速度 {hub.HandWaveSpeed:F2} · 可在灯塔顶对着镜头挥手闪灯)"
                : "待机中 (可点下方按钮开启摄像头挥手，或直接用空格/E/手柄按键)";

            hardwareTelemetryText.text =
                "• 操控模式：" + padStr + "\n" +
                "• 吹气与手势：麦克风 [" + micStr + "]  |  摄像头 [" + camStr + "]";
        }
    }
}
