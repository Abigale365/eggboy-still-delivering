// 2026-11-16 用户议题 4：心愿玩具的 3D 端详。和旧物端详（ObjectInspectModal）同一套样式和叠加相机，
// 但没有暗扣 / 盐霜：拖动鼠标或 WASD 360° 转、滚轮远近、双击回正、放着不动会慢慢自己转；
// 左边一张手账卡：心愿铭牌（编号、心愿原文、寄给谁）+ 一段温情描述 + 邮差包容量；
// 下面两个按钮：放回原处（Esc / 空格）、收进邮差包（满了就灰掉）。
using UnityEngine;
using UnityEngine.UI;

public sealed class WishInspectModal : MonoBehaviour
{
    static WishInspectModal instance;
    public static bool IsOpen => instance != null && instance.active;
    public static WishToy Current => instance != null && instance.active ? instance.toy : null;

    bool active;
    WishToy toy;
    EggboyPlayer player;
    EggboyUI ui;
    ControlGate gate;
    object gateToken;
    ChaseCamera chase;
    bool restoreChase;

    RectTransform card;
    Text titleText, plateText, plateSubText, blurbText, hintText, bagText;
    Button bagButton; Text bagButtonText;
    GameObject rig; Transform pivot; Light keyLight;
    bool overlayHeld;
    Vector3 euler = new Vector3(12f, -30f, 0f);
    float zoom = 1f, idleSpin, lastClick;
    Vector2 lastMouse;

    public static void Open(WishToy t, EggboyPlayer p)
    {
        if (t == null) return;
        if (instance == null)
        {
            var go = new GameObject("WishInspectModal");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WishInspectModal>();
        }
        instance.Show(t, p);
    }

    // 审计 / 测试：直接按按钮
    public static void DebugPutBack() { if (IsOpen) instance.Close(false); }
    public static bool DebugStore() { if (!IsOpen || WishBag.Full) return false; instance.Close(true); return true; }

    void Show(WishToy t, EggboyPlayer p)
    {
        if (active) Close(false);
        toy = t; player = p;
        ui = FindObjectOfType<EggboyUI>();
        var boot = FindObjectOfType<GameBootstrap>();
        gate = boot != null ? boot.gate : null;
        if (gate != null) gateToken = gate.Acquire("wish_inspect");
        if (player != null) player.locked = true;
        chase = FindObjectOfType<ChaseCamera>();
        restoreChase = chase != null && chase.enabled;
        if (restoreChase) chase.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        MenuController.NoteScriptUnlock();
        euler = new Vector3(12f, -30f, 0f); zoom = 1f; idleSpin = 0f;
        lastMouse = Input.mousePosition;
        EnsureUI();
        if (card != null) card.gameObject.SetActive(true);
        Refresh();
        BuildRig();
        active = true;
        ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.18f);
    }

    void EnsureUI()
    {
        if (card != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (root == null) return;
        card = TideTownStyleUI.CreateParchmentCard(root, "WishInspectCard", 500f, 470f, 7);
        card.anchorMin = card.anchorMax = new Vector2(0f, 0.5f);
        card.pivot = new Vector2(0f, 0.5f);
        card.anchoredPosition = new Vector2(28f, 10f);

        titleText = TideTownStyleUI.CreateText(card, "", 22, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, new Vector2(0f, 196f), new Vector2(440f, 32f), true);

        // 黄铜心愿铭牌
        var plate = TideTownStyleUI.CreatePanel(card, "WishBrassPlate", 440f, 78f, new Color32(0xc9, 0x9a, 0x52, 255), true);
        plate.rectTransform.anchoredPosition = new Vector2(0f, 128f);
        var rim = plate.gameObject.AddComponent<Outline>(); rim.effectColor = new Color32(0x6a, 0x45, 0x1c, 220); rim.effectDistance = new Vector2(1.5f, -1.5f);
        var inner = TideTownStyleUI.CreatePanel(plate.transform, "PlateInner", 424f, 62f, new Color32(0xe2, 0xbb, 0x74, 255), true);
        inner.sprite = TideTownStyleUI.RoundedBorderSprite;
        inner.rectTransform.anchoredPosition = Vector2.zero;
        foreach (var x in new[] { -200f, 200f })
        {
            var rivet = TideTownStyleUI.CreatePanel(plate.transform, "Rivet", 9f, 9f, new Color32(0x8a, 0x5e, 0x22, 255), true);
            rivet.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }
        plateText = TideTownStyleUI.CreateText(plate.transform, "", 18, new Color32(0x4a, 0x2c, 0x10, 255), TextAnchor.MiddleCenter, new Vector2(0f, 12f), new Vector2(380f, 28f), true);
        plateSubText = TideTownStyleUI.CreateText(plate.transform, "", 13, new Color32(0x6a, 0x45, 0x1c, 255), TextAnchor.MiddleCenter, new Vector2(0f, -15f), new Vector2(380f, 22f), false);

        blurbText = TideTownStyleUI.CreateText(card, "", 15, TideTownStyleUI.InkDark, TextAnchor.UpperLeft, new Vector2(0f, 26f), new Vector2(440f, 110f), false);
        hintText = TideTownStyleUI.CreateText(card, "拖动鼠标 / WASD 转着看　滚轮远近　双击回正", 13, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft, new Vector2(0f, -66f), new Vector2(440f, 24f), false);
        bagText = TideTownStyleUI.CreateText(card, "", 13, TideTownStyleUI.InkMuted, TextAnchor.UpperLeft, new Vector2(0f, -110f), new Vector2(440f, 44f), false);

        TideTownStyleUI.CreateWarmButton(card, "放回原处（Esc / 空格）", new Vector2(-112f, -190f), new Vector2(212f, 40f), true, () => Close(false));
        bagButton = TideTownStyleUI.CreateWarmButton(card, "收进邮差包", new Vector2(116f, -190f), new Vector2(200f, 40f), false, () => { if (!WishBag.Full) Close(true); });
        bagButtonText = bagButton.GetComponentInChildren<Text>();
    }

    void Refresh()
    {
        if (card == null || toy == null) return;
        titleText.text = "心愿端详 · 「" + toy.description + "」";
        plateText.text = "心愿 No." + toy.number.ToString("00") + "　「" + Short(toy.description, 12) + "」";
        string src = toy.spawn == null ? "" : toy.spawn.state == "swapped" || toy.spawn.state == "cached" ? "　·　Lux3D 现做" : toy.spawn.state == "generating" ? "　·　正在照心愿重做" : "　·　镇上找来的同款";
        plateSubText.text = WishBlurb.CategoryName(toy.category) + "　·　寄给：" + toy.recipient + src;
        blurbText.text = toy.blurb;
        bagText.text = WishBag.Summary();
        bool can = toy.bagAllowed && !WishBag.Full;
        bagButton.interactable = can;
        bagButtonText.text = !toy.bagAllowed ? "这件留在原处" : WishBag.Full ? "邮差包满了（" + WishBag.Capacity + "/" + WishBag.Capacity + "）" : "收进邮差包（" + WishBag.Count + "/" + WishBag.Capacity + "）";
    }

    static string Short(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n) + "…";

    void BuildRig()
    {
        var cam = Camera.main;
        if (cam == null || toy == null) return;
        rig = new GameObject("WishInspectRig");
        rig.transform.SetParent(cam.transform, false);
        float dist = 2.3f;
        float halfH = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * dist;
        rig.transform.localPosition = new Vector3(0.33f * cam.aspect * halfH, 0.02f, dist);
        var lg = new GameObject("WishKeyLight"); lg.transform.SetParent(rig.transform, false);
        lg.transform.localPosition = new Vector3(-0.6f, 0.7f, -0.9f);
        keyLight = lg.AddComponent<Light>(); keyLight.type = LightType.Point; keyLight.color = new Color32(255, 226, 182, 255);
        keyLight.range = 4.5f; keyLight.intensity = 2.4f; keyLight.shadows = LightShadows.None;
        var rim2 = new GameObject("WishRimLight"); rim2.transform.SetParent(rig.transform, false);
        rim2.transform.localPosition = new Vector3(0.7f, 0.2f, 0.8f);
        var rl = rim2.AddComponent<Light>(); rl.type = LightType.Point; rl.color = new Color32(170, 200, 255, 255); rl.range = 3.5f; rl.intensity = 1.2f;

        var pv = new GameObject("WishPivot"); pv.transform.SetParent(rig.transform, false); pivot = pv.transform;
        // 克隆一份模型来看（手里抱着的那件不动）
        var src = toy.transform.Find("Model");
        var clone = Instantiate(src != null ? src.gameObject : toy.gameObject);
        foreach (var l in clone.GetComponentsInChildren<Light>(true)) Destroy(l.gameObject);
        foreach (var c in clone.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
        clone.transform.SetParent(pivot, false);
        clone.transform.localPosition = Vector3.zero; clone.transform.localRotation = Quaternion.identity; clone.transform.localScale = Vector3.one;
        var b = Lux3DModelFit.WorldBounds(clone);
        float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (size > 1e-4f) clone.transform.localScale = Vector3.one * (0.78f / size);
        b = Lux3DModelFit.WorldBounds(clone);
        clone.transform.position += pivot.position - b.center;   // 绕自己的中心转
        pivot.localRotation = Quaternion.Euler(euler);
        OverlayRender.SetLayerRecursive(rig, OverlayRender.Layer);
        OverlayRender.LimitLights(rig);
        OverlayRender.Acquire(cam);
        overlayHeld = true;
    }

    void Update()
    {
        if (!active) return;
        if (toy == null) { Close(false); return; }
        Vector2 m = Input.mousePosition; Vector2 d = m - lastMouse; lastMouse = m;
        float rx = 0f, ry = 0f;
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            // 点在按钮上的那一下不算转
            bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!overUi) { ry = -d.x * 0.45f; rx = d.y * 0.45f; }
        }
        if (Input.GetMouseButtonDown(0))
        {
            if (Time.unscaledTime - lastClick < 0.3f) { euler = new Vector3(12f, -30f, 0f); zoom = 1f; }
            lastClick = Time.unscaledTime;
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ry += 95f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ry -= 95f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) rx += 85f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) rx -= 85f * Time.unscaledDeltaTime;
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f) zoom = Mathf.Clamp(zoom * Mathf.Exp(wheel * 0.12f), 0.6f, 1.7f);
        if (Mathf.Abs(rx) + Mathf.Abs(ry) > 0.001f) idleSpin = 0f; else idleSpin += Time.unscaledDeltaTime;
        if (idleSpin > 2.5f) ry -= 14f * Time.unscaledDeltaTime;   // 放着不动：慢慢自己转
        euler.x = Mathf.Clamp(euler.x + rx, -85f, 85f);
        euler.y += ry;
        if (pivot != null)
        {
            pivot.localRotation = Quaternion.Euler(euler);
            pivot.localPosition = new Vector3(0f, Mathf.Sin(Time.unscaledTime * 1.6f) * 0.015f, 0f);
            rig.transform.localScale = Vector3.one * zoom;
        }
        OverlayRender.SyncFov();
        if (blurbText != null && blurbText.text != toy.blurb) blurbText.text = toy.blurb;   // 转发服务写好了就换上
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Close(false);
    }

    void Close(bool store)
    {
        if (!active) return;
        active = false;
        if (overlayHeld) { OverlayRender.Release(); overlayHeld = false; }
        if (rig != null) Destroy(rig);
        rig = null; pivot = null; keyLight = null;
        if (card != null) card.gameObject.SetActive(false);
        if (restoreChase && chase != null) chase.enabled = true;
        restoreChase = false;
        if (gate != null && gateToken != null) { gate.Release(gateToken); gateToken = null; }
        if (player != null) player.locked = false;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        var t = toy; toy = null;
        if (t == null) return;
        if (store && t.bagAllowed && !WishBag.Full)
        {
            t.StoreInBag();
            if (ui != null) ui.ShowToast("「" + t.description + "」收进了邮差包（" + WishBag.Count + "/" + WishBag.Capacity + "）");
            ShadowAndChimeSystem.PlayProceduralChime(987.77f, 0.2f);
        }
        else t.PutBack();
    }
}
