// 创新方向 D：【信纸揭字 · 抛空成物】（Peel Word-Slip from Parchment Letter & Throw into 3D Air）。
// 直接附着在 EggboyUI 原版高质感羊皮纸信件上：
//   1. 每封信的正文下方都有几张带有邮票齿孔与阴影的「可揭下信纸字签」（如〖柴火捆〗〖最喜欢的球〗〖操场滑梯〗〖浮水木箱〗〖煤油壶〗〖长椅木桥〗）；
//   2. 玩家用鼠标按住字签即可将其从信纸上“揭/撕”下来；
//   3. 将字签往信纸外的 3D 空中一拖，信纸自动半透明退后，空中出现从蛋仔手中抛出的 3D 抛物线与真实 Lux3D 落点预览；
//   4. 松开鼠标，信纸字签在 3D 空气中翻飞划出抛物线，落地瞬间直接生长为场景内 100% 真实的 Lux3D 模型（无任何粗糙白盒或异形图标）！
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WordMaterializerSystem : MonoBehaviour
{
    public sealed class LetterWordSlipDef
    {
        public string word;
        public string subLabel;
        public string lux3DSceneName;
        public float targetSize;
        public bool walkable;
        public string storyAction; // wood, fire, ball, oil, lamp, none
    }

    static readonly LetterWordSlipDef[] LinOvenSlips =
    {
        new LetterWordSlipDef { word = "柴火捆", subLabel = "揭下抛向地面化为柴火", lux3DSceneName = "Bakery_wood", targetSize = 0.65f, walkable = false, storyAction = "wood" },
        new LetterWordSlipDef { word = "最后一炉暖火", subLabel = "抛向烤炉直接生火", lux3DSceneName = "Bakery_basket", targetSize = 0.58f, walkable = false, storyAction = "fire" },
        new LetterWordSlipDef { word = "老台秤", subLabel = "化为铁制台秤并显露刻度", lux3DSceneName = "Bakery_scale", targetSize = 0.46f, walkable = false, storyAction = "clue_bakery" },
        new LetterWordSlipDef { word = "长椅木桥", subLabel = "化为可踩踏的实木长椅桥", lux3DSceneName = "Street_S6", targetSize = 1.65f, walkable = true, storyAction = "none" }
    };

    static readonly LetterWordSlipDef[] YingDogSlips =
    {
        new LetterWordSlipDef { word = "最喜欢的球", subLabel = "抛向狗碗直接滚入唤出小黑", lux3DSceneName = "S_ball", targetSize = 0.24f, walkable = false, storyAction = "ball" },
        new LetterWordSlipDef { word = "操场滑梯", subLabel = "化为可攀爬的木滑梯", lux3DSceneName = "S_slide", targetSize = 2.20f, walkable = true, storyAction = "none" },
        new LetterWordSlipDef { word = "旧课桌", subLabel = "化为可垫脚攀登的旧课桌", lux3DSceneName = "S_desk", targetSize = 0.92f, walkable = true, storyAction = "clue_school" },
        new LetterWordSlipDef { word = "狗窝木箱", subLabel = "化为学校后院木狗窝", lux3DSceneName = "S_doghouse", targetSize = 0.88f, walkable = true, storyAction = "clue_school" }
    };

    static readonly LetterWordSlipDef[] ChenLighthouseSlips =
    {
        new LetterWordSlipDef { word = "浮水木箱", subLabel = "抛向浅滩化为可踩踏浮箱", lux3DSceneName = "crate_0", targetSize = 1.05f, walkable = true, storyAction = "none" },
        new LetterWordSlipDef { word = "煤油壶", subLabel = "化为守塔人的黄铜油壶", lux3DSceneName = "LH_oil", targetSize = 0.46f, walkable = false, storyAction = "oil" },
        new LetterWordSlipDef { word = "单筒望远镜", subLabel = "化为锁定山顶方位的望远镜", lux3DSceneName = "LH_scope", targetSize = 0.65f, walkable = false, storyAction = "clue_lighthouse" },
        new LetterWordSlipDef { word = "街角暖灯", subLabel = "化为散发暖光的街道路灯", lux3DSceneName = "Street_S4", targetSize = 2.65f, walkable = false, storyAction = "lamp" }
    };

    static readonly LetterWordSlipDef[] ZhouEggboySlips =
    {
        new LetterWordSlipDef { word = "街角暖灯", subLabel = "化为照亮归途的暖光路灯", lux3DSceneName = "Street_S4", targetSize = 2.65f, walkable = false, storyAction = "lamp" },
        new LetterWordSlipDef { word = "窗台花盆", subLabel = "化为盛开的潮汐镇花盆", lux3DSceneName = "Street_E1", targetSize = 0.48f, walkable = false, storyAction = "none" },
        new LetterWordSlipDef { word = "长椅木桥", subLabel = "化为可踩踏的实木长椅桥", lux3DSceneName = "Street_S6", targetSize = 1.65f, walkable = true, storyAction = "none" },
        new LetterWordSlipDef { word = "浮水木箱", subLabel = "化为可垫脚攀爬的木箱", lux3DSceneName = "crate_0", targetSize = 1.05f, walkable = true, storyAction = "none" }
    };

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;

    RectTransform hudCanvasRoot;
    RectTransform letterOverlay;
    RectTransform letterPaper;
    CanvasGroup paperCanvasGroup;
    Image overlayBackdropImage;

    GameObject slipTrayRoot;
    readonly List<GameObject> slipCards = new List<GameObject>();

    // 当前正在被鼠标拖拽的信纸字签
    LetterWordSlipDef draggingSlip;
    RectTransform draggedSlipUI;
    Vector2 dragStartScreenPos;
    bool aimedInAir;
    Vector3 currentAimWorldPos;

    // 3D 空中抛物线轨迹点与真实 Lux3D 落点预览
    GameObject aimPreviewRoot;
    readonly Transform[] arcDots = new Transform[14];
    GameObject ghostLux3DModel;

    readonly List<GameObject> materializedObjects = new List<GameObject>();

    public bool IsPanelOpen => ui != null && ui.LetterOpen;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        player = p;
        ui = u;
        world = w;
        director = d;
        state = s;
    }

    public void OpenLetterWithSlips()
    {
        if (ui == null) return;
        if (ui.LetterOpen)
        {
            ui.CloseLetter();
            return;
        }

        // 根据当前剧情阶段打开对应的原版高质感羊皮纸信件
        string step = state != null ? state.step : "";
        if (step.StartsWith("S"))
            OpenSpecificLetter(1);
        else if (step.StartsWith("L"))
            OpenSpecificLetter(2);
        else if (step.StartsWith("E"))
            OpenSpecificLetter(3);
        else
            OpenSpecificLetter(0);
    }

    public void OpenSpecificLetter(int index)
    {
        if (ui == null) return;
        if (index == 0)
        {
            ui.ShowLetterDetail(
                "致 面包店的烤炉",
                "老伙计：\n\n四十二年，你每天早上五点就热起来，比我还早。\n搬家的卡车装不下你，我只带走了你烤的最后一炉面包，路上就吃完了。\n\n邮差，如果你读到这里，请帮我再生一次火。",
                "——林阿婆");
        }
        else if (index == 1)
        {
            ui.ShowLetterDetail(
                "致 小黑",
                "小黑：\n\n搬家那天你追着卡车跑，后来就不见了。\n我把你的碗放回了学校后门，你以前每天在那里等我放学。我还放了你最喜欢的球。",
                "——阿萤，8 岁");
        }
        else if (index == 2)
        {
            ui.ShowLetterDetail(
                "致 灯塔",
                "灯塔：\n\n我在你身上点了三十一年的灯，一晚都没灭过。\n听说海水下个月就会淹到你脚下。在那之前，能不能再亮一次？",
                "——老陈，守塔人");
        }
        else
        {
            ui.ShowLetterDetail(
                "致 EGGBOY",
                "EGGBOY：\n\n后来我发现，寄回镇上的信都被送到了：面包店的烟囱冒了烟，小黑不见了，灯塔亮了。\n我就知道是你。\n\n回家吧。山这边的新邮局，一直给你留着位置。",
                "——老周");
        }
        RebuildSlipTrayForCategory(index);
    }

    void EnsureBoundToLetterUI()
    {
        if (letterPaper != null) return;
        hudCanvasRoot = TideTownStyleUI.FindHudCanvasRoot(ui);
        letterOverlay = TideTownStyleUI.FindLetterOverlay(ui);
        letterPaper = TideTownStyleUI.FindLetterPaper(ui);
        if (letterPaper == null) return;

        // 让信纸本体拦截点击，避免点击信纸内部字签时误触背景关闭信件
        var paperImg = letterPaper.GetComponent<Image>();
        if (paperImg != null) paperImg.raycastTarget = true;

        paperCanvasGroup = letterPaper.GetComponent<CanvasGroup>();
        if (paperCanvasGroup == null) paperCanvasGroup = letterPaper.gameObject.AddComponent<CanvasGroup>();

        if (letterOverlay != null)
            overlayBackdropImage = letterOverlay.GetComponent<Image>();

        BuildSlipTrayOnPaper();
    }

    void BuildSlipTrayOnPaper()
    {
        if (letterPaper == null || slipTrayRoot != null) return;

        slipTrayRoot = new GameObject("WordSlipTray", typeof(RectTransform));
        slipTrayRoot.transform.SetParent(letterPaper, false);
        var rt = slipTrayRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(560f, 620f);

        // 顶部四枚信笺切换书签（保持与信纸完全一致的暖褐与米白质感）
        string[] tabNames = { "林阿婆的信", "阿萤的信", "老陈的信", "老周的信" };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float x = -186f + i * 124f;
            TideTownStyleUI.CreateWarmButton(
                slipTrayRoot.transform,
                tabNames[i],
                new Vector2(x, 294f),
                new Vector2(116f, 28f),
                false,
                () => OpenSpecificLetter(idx));
        }

        // 信纸下半部的引导小字
        TideTownStyleUI.CreateText(
            slipTrayRoot.transform,
            "· 信纸拆字成物：按住下方字签拖出信纸，往空气中一扔即可化为实物 ·",
            13,
            TideTownStyleUI.InkMuted,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -158f),
            new Vector2(500f, 24f),
            true);

        RebuildSlipTrayForCurrentStep();
    }

    void RebuildSlipTrayForCurrentStep()
    {
        string step = state != null ? state.step : "";
        int cat = step.StartsWith("S") ? 1 : step.StartsWith("L") ? 2 : step.StartsWith("E") ? 3 : 0;
        RebuildSlipTrayForCategory(cat);
    }

    void RebuildSlipTrayForCategory(int category)
    {
        EnsureBoundToLetterUI();
        if (slipTrayRoot == null) return;

        foreach (var go in slipCards)
            if (go != null) Destroy(go);
        slipCards.Clear();

        LetterWordSlipDef[] defs = category == 1 ? YingDogSlips
                                 : category == 2 ? ChenLighthouseSlips
                                 : category == 3 ? ZhouEggboySlips
                                                 : LinOvenSlips;

        // 在信纸下半部排布 2x2 共 4 张高质感可撕信纸字签
        for (int i = 0; i < defs.Length; i++)
        {
            var def = defs[i];
            int row = i / 2;
            int col = i % 2;
            Vector2 pos = new Vector2(col == 0 ? -126f : 126f, -198f - row * 48f);

            // 底层虚线凹槽（字签被揭起后露出的信纸底槽）
            var slotBg = TideTownStyleUI.CreatePanel(
                slipTrayRoot.transform,
                "SlipSlot_" + def.word,
                232f,
                40f,
                new Color32(0xec, 0xdf, 0xc6, 255),
                true);
            slotBg.rectTransform.anchorMin = slotBg.rectTransform.anchorMax = slotBg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            slotBg.rectTransform.anchoredPosition = pos;
            TideTownStyleUI.CreateText(
                slotBg.transform,
                "已揭下「" + def.word + "」",
                12,
                TideTownStyleUI.InkMuted,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                new Vector2(210f, 30f),
                false);
            slotCardsAdd(slotBg.gameObject);

            // 表层可揭下的信纸字签本体
            var slipCard = TideTownStyleUI.CreatePanel(
                slipTrayRoot.transform,
                "WordSlip_" + def.word,
                232f,
                40f,
                TideTownStyleUI.SlipColor,
                true);
            var srt = slipCard.rectTransform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = pos;

            var sh = slipCard.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color32(0x3b, 0x2a, 0x1a, 70);
            sh.effectDistance = new Vector2(2f, -3f);

            // 左侧暖橘色邮戳细条装饰
            var stampBar = TideTownStyleUI.CreatePanel(
                slipCard.transform,
                "StampAccent",
                6f,
                28f,
                TideTownStyleUI.AmberAccent,
                true);
            stampBar.raycastTarget = false;
            stampBar.rectTransform.anchorMin = stampBar.rectTransform.anchorMax = stampBar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            stampBar.rectTransform.anchoredPosition = new Vector2(-104f, 0f);

            TideTownStyleUI.CreateText(
                slipCard.transform,
                "「" + def.word + "」",
                15,
                TideTownStyleUI.InkDark,
                TextAnchor.MiddleLeft,
                new Vector2(-10f, 6f),
                new Vector2(180f, 22f),
                true);

            TideTownStyleUI.CreateText(
                slipCard.transform,
                def.subLabel,
                11,
                TideTownStyleUI.InkMuted,
                TextAnchor.MiddleLeft,
                new Vector2(-4f, -10f),
                new Vector2(192f, 18f),
                false);

            var handler = slipCard.gameObject.AddComponent<WordSlipDragRelay>();
            handler.system = this;
            handler.slipDef = def;
            handler.cardRect = srt;

            slipCards.Add(slipCard.gameObject);
        }
    }

    void slotCardsAdd(GameObject go) => slipCards.Add(go);

    bool lastLetterOpen;

    void Update()
    {
        if (ui == null || !ui.inputClose) return;

        EnsureBoundToLetterUI();

        // 当信件刚刚打开时，自动刷新对应信件的可揭字签，并确保信纸透明度恢复为 1
        if (ui.LetterOpen && !lastLetterOpen)
        {
            RestorePaperOpacity();
            RebuildSlipTrayForCurrentStep();
        }
        else if (!ui.LetterOpen && lastLetterOpen)
        {
            CancelActiveDrag();
            RestorePaperOpacity();
        }
        lastLetterOpen = ui.LetterOpen;

        if (draggingSlip != null)
        {
            UpdateDraggingInAir();
        }
    }

    internal void OnSlipBeginDrag(LetterWordSlipDef def, RectTransform originalCard, PointerEventData ev)
    {
        if (def == null || hudCanvasRoot == null) return;
        draggingSlip = def;
        dragStartScreenPos = ev.position;
        aimedInAir = false;

        // 原字签隐藏，露出底下的“已揭下”虚线槽
        if (originalCard != null) originalCard.gameObject.SetActive(false);

        // 在顶层 Canvas 创建跟随鼠标的“揭下的信纸字签”
        var dragImg = TideTownStyleUI.CreatePanel(
            hudCanvasRoot,
            "DraggedWordSlip",
            220f,
            44f,
            TideTownStyleUI.PaperColor,
            true);
        dragImg.raycastTarget = false;
        var sh = dragImg.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color32(0, 0, 0, 120);
        sh.effectDistance = new Vector2(5f, -8f);

        TideTownStyleUI.CreateText(
            dragImg.transform,
            "「" + def.word + "」 · 松手抛向空中",
            15,
            TideTownStyleUI.InkDark,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            new Vector2(204f, 36f),
            true);

        draggedSlipUI = dragImg.rectTransform;
        UpdateDraggedSlipScreenPos(ev.position, Vector2.zero);
        ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.18f);
    }

    internal void OnSlipDrag(PointerEventData ev)
    {
        if (draggingSlip == null || draggedSlipUI == null) return;
        UpdateDraggedSlipScreenPos(ev.position, ev.delta);
    }

    internal void OnSlipEndDrag(RectTransform originalCard, PointerEventData ev)
    {
        if (draggingSlip == null) return;
        var def = draggingSlip;
        Vector3 landPos = currentAimWorldPos;
        bool shouldThrow = aimedInAir || Vector2.Distance(ev.position, dragStartScreenPos) > 48f;

        CleanupDragVisuals();

        if (shouldThrow && player != null)
        {
            // 关闭信纸界面，让玩家完整观看信纸签在 3D 空中划出抛物线化为真物的全过程！
            RestorePaperOpacity();
            if (ui != null && ui.LetterOpen) ui.CloseLetter();
            StartCoroutine(ThrowPaperSlipIn3DAirRoutine(def, landPos));
        }
        else
        {
            // 如果只是原地轻点没拖出，把字签放回原槽位
            RestorePaperOpacity();
            if (originalCard != null) originalCard.gameObject.SetActive(true);
        }
    }

    void UpdateDraggedSlipScreenPos(Vector2 screenPos, Vector2 delta)
    {
        if (draggedSlipUI == null || hudCanvasRoot == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            hudCanvasRoot, screenPos, null, out Vector2 localPt);
        draggedSlipUI.anchoredPosition = localPt;
        float tilt = Mathf.Clamp(-delta.x * 0.65f, -18f, 18f);
        draggedSlipUI.localRotation = Quaternion.Euler(0f, 0f, -3f + tilt);
        draggedSlipUI.localScale = Vector3.one * 1.08f;
    }

    void UpdateDraggingInAir()
    {
        Vector2 mousePos = Input.mousePosition;
        float dragDist = Vector2.Distance(mousePos, dragStartScreenPos);

        // 一旦字签被拖离原位超过 45 像素，信件自动退为极淡半透明（12%），露出完整的 3D 小岛视野！
        if (dragDist > 45f)
        {
            aimedInAir = true;
            if (paperCanvasGroup != null)
                paperCanvasGroup.alpha = Mathf.Lerp(paperCanvasGroup.alpha, 0.12f, Time.unscaledDeltaTime * 12f);
            if (overlayBackdropImage != null)
                overlayBackdropImage.color = new Color32(0, 0, 0, 10);
        }

        // 计算鼠标在 3D 世界中的抛投落点
        currentAimWorldPos = ComputeAimWorldPoint(mousePos);
        Update3DArcAndGhostPreview(currentAimWorldPos);
    }

    Vector3 ComputeAimWorldPoint(Vector2 screenPos)
    {
        if (player == null) return Vector3.zero;
        Vector3 pPos = player.transform.position;
        var cam = Camera.main;

        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            // 与玩家所在高度附近的水平面或地面求交
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, pPos.y, 0f));
            if (groundPlane.Raycast(ray, out float enter) && enter > 0.5f && enter < 35f)
            {
                Vector3 hit = ray.GetPoint(enter);
                Vector3 flatDelta = new Vector3(hit.x - pPos.x, 0f, hit.z - pPos.z);
                float dist = flatDelta.magnitude;
                if (dist < 1.4f)
                    flatDelta = flatDelta.normalized * 1.4f;
                else if (dist > 8.5f)
                    flatDelta = flatDelta.normalized * 8.5f;

                Vector3 target = pPos + flatDelta;
                if (world != null)
                {
                    float gy = world.GroundAt(target.x, target.z, pPos.y + 2.5f);
                    target.y = gy > -0.4f ? gy : world.WaterY;
                }
                return target;
            }
        }

        float yaw = player.yaw;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 fallback = pPos + fwd * 2.6f;
        if (world != null) fallback.y = world.GroundAt(fallback.x, fallback.z, pPos.y + 1.5f);
        return fallback;
    }

    void Update3DArcAndGhostPreview(Vector3 targetPos)
    {
        if (player == null || draggingSlip == null) return;

        if (aimPreviewRoot == null)
        {
            aimPreviewRoot = new GameObject("WordThrowAimPreview");
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var dotMat = new Material(sh) { color = TideTownStyleUI.PaperColor };
            if (dotMat.HasProperty("_EmissionColor"))
            {
                dotMat.EnableKeyword("_EMISSION");
                dotMat.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.42f) * 1.5f);
            }

            for (int i = 0; i < arcDots.Length; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(d.GetComponent<Collider>());
                d.transform.SetParent(aimPreviewRoot.transform, false);
                float s = Mathf.Lerp(0.05f, 0.09f, (float)i / arcDots.Length);
                d.transform.localScale = Vector3.one * s;
                d.GetComponent<Renderer>().sharedMaterial = dotMat;
                arcDots[i] = d.transform;
            }

            ghostLux3DModel = TideTownStyleUI.CloneSceneLux3D(draggingSlip.lux3DSceneName, draggingSlip.targetSize, false);
            if (ghostLux3DModel != null)
                ghostLux3DModel.transform.SetParent(aimPreviewRoot.transform, false);
        }

        Vector3 startPos = player.transform.position + Vector3.up * 0.68f;
        for (int i = 0; i < arcDots.Length; i++)
        {
            float t = (float)(i + 1) / arcDots.Length;
            Vector3 p = Vector3.Lerp(startPos, targetPos, t);
            p.y += Mathf.Sin(t * Mathf.PI) * 1.85f;
            arcDots[i].position = p;
        }

        if (ghostLux3DModel != null)
        {
            ghostLux3DModel.transform.position = targetPos;
            float lookYaw = Mathf.Atan2(targetPos.x - startPos.x, targetPos.z - startPos.z) * Mathf.Rad2Deg;
            ghostLux3DModel.transform.rotation = Quaternion.Euler(0f, lookYaw, 0f);
        }
    }

    IEnumerator ThrowPaperSlipIn3DAirRoutine(LetterWordSlipDef def, Vector3 landPos)
    {
        if (player == null) yield break;

        Vector3 startPos = player.transform.position + Vector3.up * 0.72f;
        float lookYaw = Mathf.Atan2(landPos.x - startPos.x, landPos.z - startPos.z);
        player.yaw = lookYaw;
        player.transform.rotation = Quaternion.Euler(0f, lookYaw * Mathf.Rad2Deg, 0f);
        player.PlayEmote("give");

        // 创建一张在 3D 空气中翻飞的实体信纸字签薄片
        var flyingSlip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(flyingSlip.GetComponent<Collider>());
        flyingSlip.name = "FlyingParchmentSlip";
        flyingSlip.transform.localScale = new Vector3(0.28f, 0.008f, 0.14f);
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var paperMat = new Material(sh) { color = TideTownStyleUI.PaperColor };
        if (paperMat.HasProperty("_EmissionColor"))
        {
            paperMat.EnableKeyword("_EMISSION");
            paperMat.SetColor("_EmissionColor", new Color(1f, 0.86f, 0.55f) * 1.2f);
        }
        flyingSlip.GetComponent<Renderer>().sharedMaterial = paperMat;

        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.28f);

        float duration = 0.52f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(startPos, landPos + Vector3.up * 0.05f, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 2.1f;
            flyingSlip.transform.position = pos;
            flyingSlip.transform.rotation = Quaternion.Euler(t * 520f, lookYaw * Mathf.Rad2Deg + t * 180f, Mathf.Sin(t * 14f) * 28f);
            yield return null;
        }

        Destroy(flyingSlip);

        // 落地瞬间：克隆真实的 Lux3D 模型并从信纸中舒展生长出来！
        var realObj = TideTownStyleUI.CloneSceneLux3D(def.lux3DSceneName, def.targetSize, def.walkable);
        if (realObj != null)
        {
            realObj.transform.position = landPos;
            realObj.transform.rotation = Quaternion.Euler(0f, lookYaw * Mathf.Rad2Deg, 0f);
            materializedObjects.Add(realObj);

            // 如果是路灯或暖火，附上柔和暖光源
            if (def.storyAction == "lamp" || def.storyAction == "fire")
            {
                var lgo = new GameObject("WarmLight");
                lgo.transform.SetParent(realObj.transform, false);
                lgo.transform.localPosition = new Vector3(0f, def.storyAction == "lamp" ? 2.1f : 0.65f, 0f);
                var light = lgo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.38f);
                light.range = 7.5f;
                light.intensity = 2.2f;
            }

            // 弹簧生长动画
            float growT = 0f;
            while (growT < 0.38f && realObj != null)
            {
                growT += Time.deltaTime;
                float u = Mathf.Clamp01(growT / 0.38f);
                float ease = 1f - Mathf.Pow(1f - u, 3f) + Mathf.Sin(u * Mathf.PI) * 0.14f;
                realObj.transform.localScale = Vector3.one * ease;
                yield return null;
            }
            if (realObj != null) realObj.transform.localScale = Vector3.one;
        }

        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.42f);
        player.PlayEmote("hop");
        ApplyThrownWordStoryEffect(def, landPos);
    }

    void ApplyThrownWordStoryEffect(LetterWordSlipDef def, Vector3 landPos)
    {
        if (state == null || ui == null || director == null) return;
        TideTownAchievementSystem.Unlock("word_fire");

        if (def.storyAction == "wood")
        {
            float dStove = Vector3.Distance(landPos, new Vector3(-9.73f, 1.3f, -5.85f));
            if (dStove < 6.5f && !state.flags.Contains("wood_loaded"))
            {
                director.TakeWood();
                director.LoadWood();
                ui.ShowToast("信纸上的「柴火捆」落入烤炉旁，已直接塞入炉膛！", 3.8f);
                return;
            }
        }
        else if (def.storyAction == "fire")
        {
            float dStove = Vector3.Distance(landPos, new Vector3(-9.73f, 1.3f, -5.85f));
            if (dStove < 7.5f && !state.flags.Contains("oven_lit"))
            {
                if (!state.flags.Contains("matches_taken")) state.flags.Add("matches_taken");
                if (!state.flags.Contains("wood_loaded")) state.flags.Add("wood_loaded");
                if (!state.flags.Contains("puzzle_bakery_lock")) state.flags.Add("puzzle_bakery_lock");
                director.LightOven(FindObjectOfType<BakeryEffects>());
                ui.ShowToast("信纸上的「最后一炉暖火」落入炉膛，直接点燃了面包店烤炉！", 4.0f);
                return;
            }
        }
        else if (def.storyAction == "ball")
        {
            float dBowl = Vector3.Distance(landPos, new Vector3(22.5f, 3.07f, 3.8f));
            if (dBowl < 8.5f && !state.flags.Contains("ball_in_bowl"))
            {
                if (!state.flags.Contains("puzzle_school_lock")) state.flags.Add("puzzle_school_lock");
                director.TakeBall();
                director.PutBallInBowl();
                ui.ShowToast("信纸上的「最喜欢的球」蹦跳着滚进了狗碗，把小黑唤了出来！", 4.0f);
                return;
            }
        }
        else if (def.storyAction == "oil")
        {
            if (state.step == "L1_shed" && !state.flags.Contains("oil_taken"))
            {
                director.TakeOil();
                ui.ShowToast("信纸上的「煤油壶」化作沉甸甸的真油壶落入怀中！", 3.8f);
                return;
            }
        }
        else if (def.storyAction == "clue_bakery")
        {
            foreach (var k in new[] { "recipe", "scale", "basket" })
                if (!state.flags.Contains("dog_sniff_" + k)) state.flags.Add("dog_sniff_" + k);
        }
        else if (def.storyAction == "clue_school")
        {
            foreach (var k in new[] { "slipper", "doghouse", "blackboard" })
                if (!state.flags.Contains("dog_sniff_" + k)) state.flags.Add("dog_sniff_" + k);
        }
        else if (def.storyAction == "clue_lighthouse")
        {
            foreach (var k in new[] { "hat", "scope", "log" })
                if (!state.flags.Contains("dog_sniff_" + k)) state.flags.Add("dog_sniff_" + k);
        }

        ui.ShowToast("已从信纸揭下「" + def.word + "」抛入空中，化为真实的 " + def.word + "！", 3.6f);
    }

    void CleanupDragVisuals()
    {
        draggingSlip = null;
        aimedInAir = false;
        if (draggedSlipUI != null) Destroy(draggedSlipUI.gameObject);
        draggedSlipUI = null;
        if (aimPreviewRoot != null) Destroy(aimPreviewRoot);
        aimPreviewRoot = null;
        ghostLux3DModel = null;
    }

    void CancelActiveDrag()
    {
        CleanupDragVisuals();
    }

    void RestorePaperOpacity()
    {
        if (paperCanvasGroup != null) paperCanvasGroup.alpha = 1f;
        if (overlayBackdropImage != null) overlayBackdropImage.color = new Color32(0, 0, 0, 60);
    }
}

public sealed class WordSlipDragRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public WordMaterializerSystem system;
    public WordMaterializerSystem.LetterWordSlipDef slipDef;
    public RectTransform cardRect;

    public void OnPointerDown(PointerEventData eventData)
    {
        // 支持直接按住拖出
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (system != null) system.OnSlipBeginDrag(slipDef, cardRect, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (system != null) system.OnSlipDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (system != null) system.OnSlipEndDrag(cardRect, eventData);
    }
}
