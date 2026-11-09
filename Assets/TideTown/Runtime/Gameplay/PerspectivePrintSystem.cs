// 创新方向 E：【透视借位拓印 · 手持微缩 Lux3D 物件，借位放大化为实景建筑】（Perspective Print System）。
// 100% 复用场景内真实 Lux3D 资产（S6 长椅、C5 滑梯、A4 面包篮、D3 黄铜望远镜），
// 界面完全采用 TideTownStyleUI 羊皮纸信笺 uGUI，无任何原生灰盒 OnGUI 或粗糙白盒几何体。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PerspectivePrintSystem : MonoBehaviour
{
    public sealed class PrintTemplate
    {
        public string id;
        public string title;
        public string sceneSourceName;
        public float baseSize;
        public float defaultScale;
        public bool walkable;
        public string desc;
    }

    static readonly PrintTemplate[] Templates =
    {
        new PrintTemplate
        {
            id = "bench_bridge",
            title = "长椅木桥（Lux3D 街角长椅）",
            sceneSourceName = "Street_S6",
            baseSize = 1.4f,
            defaultScale = 3.2f,
            walkable = true,
            desc = "举起微缩的实木长椅，对准前方浅滩或台阶按下快门，长椅将按透视放大为可真实踩踏通过的木桥。"
        },
        new PrintTemplate
        {
            id = "slide_ramp",
            title = "登高木滑梯（Lux3D 操场滑梯）",
            sceneSourceName = "S_slide",
            baseSize = 1.6f,
            defaultScale = 2.6f,
            walkable = true,
            desc = "举起微缩的学校木滑梯，借位对准高处按下快门，滑梯将放大落地为可攀爬登高的实木斜坡阶梯。"
        },
        new PrintTemplate
        {
            id = "basket_pavilion",
            title = "藤编暖亭（Lux3D 藤编面包篮）",
            sceneSourceName = "Bakery_basket",
            baseSize = 1.2f,
            defaultScale = 3.6f,
            walkable = true,
            desc = "将林阿婆的藤编面包篮在镜头前借位放大，落地化为一座散发烘焙暖光的藤编避风亭。"
        },
        new PrintTemplate
        {
            id = "scope_beacon",
            title = "引航望远台（Lux3D 黄铜望远镜）",
            sceneSourceName = "LH_scope",
            baseSize = 1.1f,
            defaultScale = 3.8f,
            walkable = false,
            desc = "将老陈的黄铜单筒望远镜借位放大架设在地面，照亮通往山顶新城的方位。"
        }
    };

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    GameState state;

    public bool IsViewfinderActive { get; private set; }
    int currentIndex;
    float yawOffset;
    float scaleFactor = 3.2f;

    GameObject heldRig;
    Transform heldPivot;
    RectTransform viewfinderCard;
    Text titleLabel, descLabel, scaleLabel;
    readonly List<GameObject> printedMonuments = new List<GameObject>();

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
    {
        player = p;
        ui = u;
        world = w;
        state = s;
    }

    public void ToggleViewfinder()
    {
        if (IsViewfinderActive) CloseViewfinder();
        else OpenViewfinder();
    }

    public void OpenViewfinder()
    {
        if (IsViewfinderActive) return;
        IsViewfinderActive = true;
        yawOffset = 0f;
        scaleFactor = Templates[currentIndex].defaultScale;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureViewfinderUI();
        if (viewfinderCard != null) viewfinderCard.gameObject.SetActive(true);
        RefreshUIText();
        RebuildHeldPreview();
    }

    public void CloseViewfinder()
    {
        IsViewfinderActive = false;
        if (heldRig != null) Destroy(heldRig);
        heldRig = null;
        heldPivot = null;
        if (viewfinderCard != null) viewfinderCard.gameObject.SetActive(false);
    }

    void EnsureViewfinderUI()
    {
        if (viewfinderCard != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (root == null) return;

        viewfinderCard = TideTownStyleUI.CreateParchmentCard(root, "PerspectiveViewfinderCard", 640f, 196f, 4);
        viewfinderCard.anchorMin = viewfinderCard.anchorMax = new Vector2(0.5f, 0f);
        viewfinderCard.pivot = new Vector2(0.5f, 0f);
        viewfinderCard.anchoredPosition = new Vector2(0f, 94f);

        titleLabel = TideTownStyleUI.CreateText(
            viewfinderCard,
            "",
            18,
            TideTownStyleUI.InkDark,
            TextAnchor.MiddleLeft,
            new Vector2(-20f, 68f),
            new Vector2(540f, 28f),
            true);

        scaleLabel = TideTownStyleUI.CreateText(
            viewfinderCard,
            "",
            14,
            TideTownStyleUI.InkMuted,
            TextAnchor.MiddleRight,
            new Vector2(20f, 68f),
            new Vector2(540f, 24f),
            false);

        descLabel = TideTownStyleUI.CreateText(
            viewfinderCard,
            "",
            14,
            TideTownStyleUI.InkDark,
            TextAnchor.UpperLeft,
            new Vector2(0f, 22f),
            new Vector2(584f, 48f),
            false);

        // 四个真实 Lux3D 物件切换按钮
        string[] shortNames = { "长椅木桥", "操场滑梯", "藤编暖亭", "黄铜望远台" };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            TideTownStyleUI.CreateWarmButton(
                viewfinderCard,
                shortNames[i],
                new Vector2(-225f + i * 112f, -26f),
                new Vector2(104f, 30f),
                false,
                () => SelectTemplate(idx));
        }

        TideTownStyleUI.CreateWarmButton(
            viewfinderCard,
            "撤回已放物件",
            new Vector2(228f, -26f),
            new Vector2(124f, 30f),
            false,
            ClearMonuments);

        // 底部主操作栏
        TideTownStyleUI.CreateWarmButton(
            viewfinderCard,
            "按透视放大落入地面（空格 / 回车）",
            new Vector2(-80f, -68f),
            new Vector2(360f, 36f),
            true,
            StampPerspectiveIntoWorld);

        TideTownStyleUI.CreateWarmButton(
            viewfinderCard,
            "收起（Esc / 2）",
            new Vector2(198f, -68f),
            new Vector2(160f, 36f),
            false,
            CloseViewfinder);
    }

    void SelectTemplate(int index)
    {
        currentIndex = (index + Templates.Length) % Templates.Length;
        scaleFactor = Templates[currentIndex].defaultScale;
        RefreshUIText();
        if (IsViewfinderActive) RebuildHeldPreview();
    }

    void RefreshUIText()
    {
        if (viewfinderCard == null) return;
        var tpl = Templates[currentIndex];
        if (titleLabel != null) titleLabel.text = "透视借位拓印 · " + tpl.title;
        if (scaleLabel != null) scaleLabel.text = "滚轮缩放倍率：" + scaleFactor.ToString("F1") + "x   Q/E 旋转";
        if (descLabel != null) descLabel.text = tpl.desc;
    }

    void RebuildHeldPreview()
    {
        if (heldRig != null) Destroy(heldRig);
        var cam = Camera.main;
        if (cam == null) return;

        heldRig = new GameObject("PerspectiveHeldRig");
        heldRig.transform.SetParent(cam.transform, false);
        heldRig.transform.localPosition = new Vector3(0f, 0.04f, 2.05f);
        heldRig.transform.localRotation = Quaternion.identity;

        var pivotGo = new GameObject("HeldPivot");
        pivotGo.transform.SetParent(heldRig.transform, false);
        heldPivot = pivotGo.transform;

        var tpl = Templates[currentIndex];
        var model = TideTownStyleUI.CloneSceneLux3D(tpl.sceneSourceName, 0.55f, false);
        if (model != null)
        {
            model.transform.SetParent(heldPivot, false);
            model.transform.localPosition = new Vector3(0f, -0.22f, 0f);
        }
    }

    public void StampPerspectiveIntoWorld()
    {
        if (player == null) return;
        var tpl = Templates[currentIndex];

        float yaw = player.yaw + yawOffset * Mathf.Deg2Rad;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 landPos = player.transform.position + fwd * 3.4f;
        if (world != null)
        {
            float gy = world.GroundAt(landPos.x, landPos.z, player.transform.position.y + 2f);
            landPos.y = gy > -0.4f ? gy : world.WaterY;
        }

        var monument = TideTownStyleUI.CloneSceneLux3D(
            tpl.sceneSourceName,
            tpl.baseSize * scaleFactor,
            tpl.walkable);

        if (monument != null)
        {
            monument.transform.position = landPos;
            monument.transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
            printedMonuments.Add(monument);
        }

        ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.38f);
        player.PlayEmote("hop");

        if (ui != null)
        {
            ui.ShowToast("已将「" + tpl.title + "」按 " + scaleFactor.ToString("F1") + " 倍透视拓印在前方地面！", 3.8f);
        }

        CloseViewfinder();
    }

    void Update()
    {
        if (!IsViewfinderActive) return;

        float dt = Time.unscaledDeltaTime;
        bool changed = false;
        if (Input.GetKey(KeyCode.Q)) { yawOffset -= 75f * dt; changed = true; }
        if (Input.GetKey(KeyCode.E)) { yawOffset += 75f * dt; changed = true; }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            scaleFactor = Mathf.Clamp(scaleFactor + scroll * 1.8f, 1.8f, 5.5f);
            changed = true;
        }

        if (changed) RefreshUIText();

        if (heldPivot != null)
        {
            float ratio = scaleFactor / Templates[currentIndex].defaultScale;
            heldPivot.localRotation = Quaternion.Euler(10f, yawOffset + Mathf.Sin(Time.unscaledTime * 1.4f) * 4f, 0f);
            heldPivot.localScale = Vector3.one * Mathf.Clamp(ratio, 0.72f, 1.38f);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            StampPerspectiveIntoWorld();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseViewfinder();
        }
    }

    void ClearMonuments()
    {
        foreach (var go in printedMonuments)
            if (go != null) Destroy(go);
        printedMonuments.Clear();
        if (ui != null) ui.ShowToast("已收起透视拓印的物件。", 2.5f);
    }
}
