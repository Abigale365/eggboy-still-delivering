// 创新方向 E：透视拓印（工程书 INNOVATION_BOOK_20261110 的 WP-E）。2026-11-11 重写。
// 按 C 举起取景框：镜头切到 EGGBOY 眼睛的位置，手里拿着一件小物件（小木尺 / 微缩船模 / 旧拖鞋）。
// 规则（Superliminal 式）：手里的东西在框里看起来多大，按下快门它就在世界里变成多大——
//   以镜头为中心按比例放大：k = 镜头到落点的距离 / 镜头到物件底部的距离，位置和大小都乘 k，朝向不变，
//   所以按快门前后画面上一模一样，"变真"那一下没有跳变。
// 拍下的东西同时盖住渡口缺口两端，就吸附摆正成一座能走的桥（第 5 种过海办法）；拍在别处只是摆设，不挡路。
//   鼠标：看方向；滚轮：物件拿近 / 拿远；按住右键拖：转物件；左键 / 空格：快门；Q / Z：换物件；C / Esc：放下。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PerspectivePrintSystem : MonoBehaviour
{
    sealed class Item { public string id, title, lib, scene; public float hold; public System.Func<GameState, bool> unlocked; }

    static readonly Item[] Items =
    {
        new Item { id = "ruler",   title = "小木尺",   lib = "print_ruler", hold = 0.28f, unlocked = s => true },
        new Item { id = "boat",    title = "微缩船模", scene = "S9_小船_分享包版", hold = 0.24f, unlocked = s => true },
        new Item { id = "slipper", title = "旧拖鞋",   scene = "C3_被咬过的拖鞋_分享包版", hold = 0.2f, unlocked = s => s.flags.Contains("slipper_seen") },
    };

    const float MaxPrintSize = 12f, MinScale = 1.3f;
    const int MaxPrints = 3;

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    GameState state;
    ChaseCamera chase;
    Camera cam;
    WorldMechanismDirector ferry;
    int openedFrame;

    public bool IsViewfinderActive { get; private set; }
    int itemIndex;
    float viewYaw, viewPitch, holdDist = 0.6f, heldYaw = 90f;
    Transform held;           // 镜头前的手持物（镜头的子物体）
    GameObject heldModel;
    bool restoreChase;
    int groundMask;

    // 这一帧算出来的落点
    public bool AimValid { get; private set; }
    public float AimScale { get; private set; }
    Vector3 aimCamPos;
    bool aimBridgesFerry;

    readonly List<GameObject> prints = new List<GameObject>();
    public GameObject LastPrint { get; private set; }
    public bool LastPrintSnapped { get; private set; }

    RectTransform frame;
    readonly List<Image> corners = new List<Image>();
    readonly Image[] bankMarkers = new Image[2];
    readonly List<Renderer> hiddenPlayerParts = new List<Renderer>();
    Text label;
    Image flash;
    float flashT;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
    {
        player = p; ui = u; world = w; state = s;
        groundMask = LayerMask.GetMask("Ground");
    }

    public void ToggleViewfinder()
    {
        if (IsViewfinderActive) CloseViewfinder();
        else OpenViewfinder();
    }

    public void OpenViewfinder()
    {
        if (IsViewfinderActive || player == null || ui == null) return;
        if (player.locked || ui.LetterOpen || ui.Paused || ui.TitleShowing || ui.Cinematic || MenuController.OverlayOpen) return;
        cam = Camera.main;
        if (cam == null) return;
        IsViewfinderActive = true;
        openedFrame = Time.frameCount;
        if (ferry == null) ferry = FindObjectOfType<WorldMechanismDirector>();
        player.locked = true;
        player.vel = Vector3.zero;
        chase = FindObjectOfType<ChaseCamera>();
        restoreChase = chase != null && chase.enabled;
        if (restoreChase) chase.enabled = false;
        viewYaw = player.yaw * Mathf.Rad2Deg;
        viewPitch = 10f;
        SetPlayerVisible(false);
        ui.SetCinematic(true, "viewfinder");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        EnsureUI();
        frame.gameObject.SetActive(true);
        itemIndex = FirstUnlocked(itemIndex);
        OverlayRender.Acquire(cam);
        BuildHeld();
        ApplyView();
        ShadowAndChimeSystem.PlayProceduralChime(523.25f, 0.12f);
    }

    public void CloseViewfinder()
    {
        if (!IsViewfinderActive) return;
        IsViewfinderActive = false;
        OverlayRender.Release();
        if (held != null) Destroy(held.gameObject);
        held = null; heldModel = null;
        if (frame != null) frame.gameObject.SetActive(false);
        SetPlayerVisible(true);
        if (ui != null) ui.SetCinematic(false, "viewfinder");
        if (player != null) player.locked = false;
        if (restoreChase && chase != null) chase.enabled = true;
        restoreChase = false;
    }

    void SetPlayerVisible(bool on)
    {
        if (on)
        {
            foreach (var r in hiddenPlayerParts) if (r != null) r.enabled = true;
            hiddenPlayerParts.Clear();
        }
        else if (player != null)
        {
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                hiddenPlayerParts.Add(r);
                r.enabled = false;
            }
        }
    }

    int FirstUnlocked(int from)
    {
        for (int i = 0; i < Items.Length; i++)
        {
            int k = (from + i) % Items.Length;
            if (Items[k].unlocked(state)) return k;
        }
        return 0;
    }

    void BuildHeld()
    {
        if (held != null) Destroy(held.gameObject);
        var it = Items[itemIndex];
        var root = new GameObject("PrintHeld_" + it.id);
        root.transform.SetParent(cam.transform, false);
        held = root.transform;
        GameObject model = null;
        if (!string.IsNullOrEmpty(it.lib))
        {
            var lux = Lux3DService.Ensure();
            model = lux.InstantiateFallback(lux.Find(it.lib), it.hold);
        }
        if (model == null && !string.IsNullOrEmpty(it.scene)) model = TideTownStyleUI.CloneSceneLux3D(it.scene, it.hold, false);
        if (model == null) model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        model.transform.SetParent(held, false);
        // 模型底面贴 y=0：把它往下挪半个身高，让"手持点"在物件中心
        var b = Lux3DModelFit.WorldBounds(model);
        model.transform.localPosition = new Vector3(0f, -b.size.y * 0.5f, 0f);
        heldModel = model;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 手上的东西离镜头只有半米，近处的柱子、栏杆会挡住它：画在叠加层上
        OverlayRender.SetLayerRecursive(root, OverlayRender.Layer);
        UpdateHeldPose();
    }

    void UpdateHeldPose()
    {
        if (held == null) return;
        held.localPosition = new Vector3(0f, -0.06f, holdDist);
        held.localRotation = Quaternion.Euler(0f, heldYaw, 0f);
        held.localScale = Vector3.one;
    }

    void ApplyView()
    {
        if (cam == null || player == null) return;
        Vector3 eye = player.transform.position + Vector3.up * 0.72f;
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.Euler(viewPitch, viewYaw, 0f);
        player.yaw = viewYaw * Mathf.Deg2Rad;
        player.transform.rotation = Quaternion.Euler(0f, viewYaw, 0f);
    }

    void Update()
    {
        if (!IsViewfinderActive || openedFrame == Time.frameCount) return;
        if (cam == null) { CloseViewfinder(); return; }
        bool rotatingHeld = Input.GetMouseButton(1);
        float mx = Input.GetAxis("Mouse X"), my = Input.GetAxis("Mouse Y");
        if (rotatingHeld) heldYaw += mx * 4f;
        else
        {
            viewYaw += mx * 2.2f;
            viewPitch = Mathf.Clamp(viewPitch - my * 2.2f, -35f, 55f);
        }
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        // HID 旋钮与键盘 A/D 共用输入；取景框已锁角色，不会同时移动。
        if (EasyInputBridge.Held(KeyCode.A)) viewYaw -= 18f * Time.unscaledDeltaTime;
        if (EasyInputBridge.Held(KeyCode.D)) viewYaw += 18f * Time.unscaledDeltaTime;
        if (Mathf.Abs(wheel) > 0.001f) holdDist = Mathf.Clamp(holdDist - wheel * 0.5f, 0.35f, 1.1f);
        if (Input.GetKeyDown(KeyCode.Q)) CycleItem(-1);
        if (Input.GetKeyDown(KeyCode.Z)) CycleItem(+1);

        ApplyView();
        UpdateHeldPose();
        ComputeAim();
        UpdateUI();

        if (Input.GetMouseButtonDown(0) || EasyInputBridge.Down(KeyCode.Space) || EasyInputBridge.Down(KeyCode.E)) Shutter();
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.C)) CloseViewfinder();
    }

    void CycleItem(int d)
    {
        for (int i = 1; i <= Items.Length; i++)
        {
            int k = ((itemIndex + d * i) % Items.Length + Items.Length) % Items.Length;
            if (Items[k].unlocked(state)) { itemIndex = k; break; }
        }
        BuildHeld();
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.08f);
    }

    // 从镜头穿过手持物底部中心打一条线，落到地面或水面上的距离 / 物件底部离镜头的距离 = 放大倍数
    void ComputeAim()
    {
        AimValid = false; AimScale = 0f; aimBridgesFerry = false;
        if (heldModel == null) return;
        aimCamPos = cam.transform.position;
        var heldBounds = Lux3DModelFit.WorldBounds(heldModel);
        Vector3 bottom = new Vector3(heldBounds.center.x, heldBounds.min.y, heldBounds.center.z);
        Vector3 dir = bottom - aimCamPos;
        float near = dir.magnitude;
        if (near < 1e-4f) return;
        dir /= near;
        float hitDist = float.MaxValue;
        if (Physics.Raycast(aimCamPos, dir, out var hit, 60f, groundMask, QueryTriggerInteraction.Ignore)) hitDist = hit.distance;
        // 水面也算"落点"：东西浮在水上
        float wy = WaterLevel.StaticY;
        if (dir.y < -1e-4f && aimCamPos.y > wy)
        {
            float tw = (wy - aimCamPos.y) / dir.y;
            if (tw > 0f && tw < hitDist) hitDist = tw;
        }
        if (hitDist == float.MaxValue) return;
        float k = hitDist / near;
        float size = Mathf.Max(heldBounds.size.x, Mathf.Max(heldBounds.size.y, heldBounds.size.z)) * k;
        AimScale = k;
        AimValid = k >= MinScale && size <= MaxPrintSize;
        if (AimValid && FerrySocket(out var a, out var b))
        {
            var projected = new Bounds(aimCamPos + (heldBounds.center - aimCamPos) * k, heldBounds.size * k);
            aimBridgesFerry = CoversFerry(projected, a, b);
        }
    }

    public GameObject Shutter()
    {
        if (!IsViewfinderActive) return null;
        ComputeAim();
        if (!AimValid || held == null)
        {
            ShadowAndChimeSystem.PlayProceduralChime(196f, 0.15f);
            return null;
        }
        float k = AimScale;
        // 以镜头为中心按 k 放大：位置、大小都乘 k，朝向不变
        var go = Instantiate(held.gameObject);
        go.name = "PerspectivePrint_" + Items[itemIndex].id;
        OverlayRender.SetLayerRecursive(go, 0);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        go.transform.SetPositionAndRotation(aimCamPos + (held.position - aimCamPos) * k, held.rotation);
        go.transform.localScale = held.lossyScale * k;
        prints.Add(go);
        LastPrint = go;
        LastPrintSnapped = TrySnapToSocket(go);
        while (prints.Count > MaxPrints)
        {
            if (prints[0] != null) Destroy(prints[0]);
            prints.RemoveAt(0);
        }
        flashT = 0.18f;
        ShadowAndChimeSystem.PlayProceduralChime(1318.5f, 0.06f);
        ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
        return go;
    }

    // 拓印插座：渡口缺口。拍下的东西同时盖住缺口两端（放宽 0.6 m）就吸附摆正，变成能走的桥
    bool TrySnapToSocket(GameObject go)
    {
        if (!FerrySocket(out var a, out var b) || !CoversFerry(Lux3DModelFit.WorldBounds(go), a, b)) return false;

        // 摆正：最长的水平边顺着 A→B，中心放在缺口中点，底面略高于水面
        Vector3 ab = b - a; ab.y = 0f;
        float len = ab.magnitude;
        var root = new GameObject("PrintBridge");
        int ground = LayerMask.NameToLayer("Ground");
        root.layer = ground >= 0 ? ground : 6;
        Vector3 mid = (a + b) * 0.5f;
        float deckY = Mathf.Max(WaterLevel.StaticY + 0.08f, Mathf.Min(a.y, b.y));
        root.transform.SetPositionAndRotation(new Vector3(mid.x, deckY, mid.z), Quaternion.LookRotation(ab / len, Vector3.up));
        go.transform.SetParent(root.transform, true);
        go.transform.localRotation = Quaternion.identity;
        var lb = Lux3DModelFit.WorldBounds(go);
        // 让最长的那条水平边顺着 Z
        if (lb.size.x > lb.size.z) go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        lb = Lux3DModelFit.WorldBounds(go);
        // 长度不够就再撑一点，盖满缺口两端各 0.3 m
        float along = Mathf.Max(lb.size.x, lb.size.z);
        if (along < len + 0.6f) go.transform.localScale *= (len + 0.6f) / Mathf.Max(0.01f, along);
        lb = Lux3DModelFit.WorldBounds(go);
        go.transform.position += new Vector3(mid.x - lb.center.x, deckY - lb.min.y, mid.z - lb.center.z);
        // 能踩的甲板：固定一块顺着缺口的板，不依赖模型外形
        var deck = root.AddComponent<BoxCollider>();
        deck.center = new Vector3(0f, 0.06f, 0f);
        deck.size = new Vector3(1.0f, 0.12f, len + 0.6f);
        Physics.SyncTransforms();
        prints[prints.Count - 1] = root;
        LastPrint = root;
        ferry.BridgeGapWithBoat("print_bridge");
        ShadowAndChimeSystem.PlayProceduralChime(1046.5f, 0.4f);
        return true;
    }

    bool FerrySocket(out Vector3 a, out Vector3 b)
    {
        a = b = Vector3.zero;
        return ferry != null && ferry.GapActive && !ferry.CrossingSolved && ferry.GapEndpoints(out a, out b);
    }

    static bool CoversFerry(Bounds bounds, Vector3 a, Vector3 b)
    {
        bounds.Expand(new Vector3(1.2f, 4f, 1.2f));
        return bounds.Contains(new Vector3(a.x, bounds.center.y, a.z)) &&
               bounds.Contains(new Vector3(b.x, bounds.center.y, b.z));
    }

    void EnsureUI()
    {
        if (frame != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(ui);
        var go = new GameObject("ViewfinderFrame", typeof(RectTransform));
        go.transform.SetParent(root, false);
        frame = go.GetComponent<RectTransform>();
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(100f, 72f); frame.offsetMax = new Vector2(-100f, -72f);
        for (int i = 0; i < 4; i++)
        {
            Vector2 anchor = new Vector2(i % 2, i / 2);
            for (int j = 0; j < 2; j++)
            {
                var bar = TideTownStyleUI.CreatePanel(frame, "Corner", j == 0 ? 44f : 2f, j == 0 ? 2f : 44f, TideTownStyleUI.CreamText, false);
                bar.raycastTarget = false;
                var rt = bar.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
                rt.anchoredPosition = Vector2.zero;
                corners.Add(bar);
            }
        }
        var dot = TideTownStyleUI.CreatePanel(frame, "CenterDot", 4f, 4f, TideTownStyleUI.CreamText, true);
        dot.raycastTarget = false;
        corners.Add(dot);
        for (int i = 0; i < bankMarkers.Length; i++)
        {
            var marker = TideTownStyleUI.CreatePanel(frame, "FerryBank_" + i, 10f, 10f, TideTownStyleUI.CreamText, false);
            marker.raycastTarget = false;
            marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            bankMarkers[i] = marker;
        }
        // 2026-11-16 取景提示条换成和 HUD 教学条同一套：胡桃木底 + 内缩金细线 + 投影
        var caption = TideTownStyleUI.CreatePanel(frame, "ViewfinderCaption", 410f, 36f, new Color32(0x4a, 0x30, 0x1c, 232));
        caption.sprite = TideTownStyleUI.RoundedBorderSprite;
        caption.raycastTarget = false;
        var capShadow = caption.gameObject.AddComponent<Shadow>();
        capShadow.effectColor = new Color32(0x1a, 0x10, 0x08, 90);
        capShadow.effectDistance = new Vector2(3f, -5f);
        var cr = caption.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(.5f, 0f);
        cr.anchoredPosition = new Vector2(0f, 18f);
        var capLine = TideTownStyleUI.CreatePanel(cr, "CaptionFrame", 0f, 0f, new Color32(0xe0, 0xb4, 0x72, 150), true);
        capLine.sprite = TideTownStyleUI.RoundedFrameSprite;
        capLine.raycastTarget = false;
        var clr = capLine.rectTransform;
        clr.anchorMin = Vector2.zero; clr.anchorMax = Vector2.one; clr.offsetMin = new Vector2(3f, 3f); clr.offsetMax = new Vector2(-3f, -3f);
        label = TideTownStyleUI.CreateText(cr, "", 15, TideTownStyleUI.CreamText, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(374f, 30f), false);
        var fgo = new GameObject("ShutterFlash", typeof(RectTransform), typeof(Image));
        fgo.transform.SetParent(root, false);
        var frt = fgo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        flash = fgo.GetComponent<Image>();
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;
    }

    void UpdateUI()
    {
        if (frame == null) return;
        Color c = aimBridgesFerry ? new Color(1f, .82f, .39f) :
            AimValid ? (Color)TideTownStyleUI.CreamText : new Color(.86f, .77f, .67f, .78f);
        foreach (var im in corners) im.color = c;
        bool nearbyFerry = FerrySocket(out var a, out var b) &&
            Vector3.Distance(player.transform.position, (a + b) * .5f) <= 9f;
        SetBankMarker(bankMarkers[0], a, nearbyFerry, c);
        SetBankMarker(bankMarkers[1], b, nearbyFerry, c);
        label.text = Items[itemIndex].title + (aimBridgesFerry ? "　可以接上两岸" :
            nearbyFerry ? "　让物件搭住两枚岸标" :
            AimValid ? "　空格拓印" : "　把物件底边对向地面");
    }

    void SetBankMarker(Image marker, Vector3 bank, bool visible, Color color)
    {
        bank.y = Mathf.Max(bank.y, WaterLevel.StaticY) + .10f;
        var point = cam.WorldToScreenPoint(bank);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, point, null, out var local);
        marker.enabled = visible && point.z > 0f && frame.rect.Contains(local);
        marker.rectTransform.anchoredPosition = local;
        marker.color = color;
    }

    void LateUpdate()
    {
        if (flash == null) return;
        if (flashT > 0f) flashT -= Time.unscaledDeltaTime;
        flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flashT / 0.18f) * 0.75f);
    }

    // ---- 审计用 ----
    public void DebugSetView(float yawDeg, float pitchDeg, float hold, float heldYawDeg)
    {
        viewYaw = yawDeg; viewPitch = pitchDeg; holdDist = hold; heldYaw = heldYawDeg;
        ApplyView(); UpdateHeldPose(); ComputeAim();
    }
    public void DebugSelect(string id)
    {
        for (int i = 0; i < Items.Length; i++) if (Items[i].id == id) { itemIndex = i; if (IsViewfinderActive) BuildHeld(); }
    }
    public Rect DebugScreenRect(GameObject go)
    {
        var c = cam != null ? cam : Camera.main;
        var b = Lux3DModelFit.WorldBounds(go);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
            Vector3 s = c.WorldToScreenPoint(p);
            x0 = Mathf.Min(x0, s.x); y0 = Mathf.Min(y0, s.y); x1 = Mathf.Max(x1, s.x); y1 = Mathf.Max(y1, s.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }
    public GameObject DebugHeldModel => heldModel;
    public void DebugClearPrints() { foreach (var p in prints) if (p != null) Destroy(p); prints.Clear(); LastPrint = null; }
}
