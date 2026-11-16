// WP-16：Esc 暂停。读信、过场、看地图时不抢 Esc；地图刚关掉的那一帧也不抢。
// 修正（2026-11-07）：
//   1. 关信的那一下 Esc 不再顺手打开暂停。EggboyUI 和本脚本谁先跑不固定，
//      所以“上一帧信还开着”也算读信中。
//   2. 网页版里第一下 Esc 被浏览器拿去退出鼠标锁定，游戏收不到这个键。
//      改成：游玩中鼠标锁定被意外解除，就当玩家按了 Esc，直接暂停。
// 修正（2026-11-11）：解密中自动弹出暂停菜单（暂停会隐藏 HUD、冻结时间，右侧 3D 旧物和互动全没了）。
//   原因：旧物端详、心愿造物笺打开时，游戏自己把鼠标解锁（要用鼠标转模型、点铜扣、打字），
//   网页版上被第 2 条当成“意外解锁”而自动暂停；而这两个弹窗都不在 Blocked 名单里。
//   另外，在弹窗里按 Esc 关弹窗时，同一下 Esc 还会顺手打开暂停（和关信同一个问题，只是没修到弹窗）。
//   现在：
//   1. 所有解密弹窗（旧物端详、木匣、心愿造物笺、取景框、光影工作台）都算“占用中”，占用时不暂停。
//   2. 弹窗刚关闭的 0.5 秒内也算占用，不受两个脚本谁先跑的影响。
//   3. 游戏自己解锁鼠标时（NoteScriptUnlock）1 秒内不把鼠标解锁当成“意外解锁”。
using UnityEngine;
using UnityEngine.UI;

public sealed class MenuController : MonoBehaviour
{
    // 弹窗刚关掉的这段时间里，Esc 和鼠标锁定变化都不算“玩家要暂停”
    const float OverlayGrace = 0.5f;
    // 游戏自己解锁鼠标后，这段时间内的“锁定 → 解锁”不是意外
    const float ScriptUnlockGrace = 1f;

    static float lastScriptUnlockAt = -10f;

    // 脚本主动 Cursor.lockState = None 之后调用（弹窗要用鼠标时）。
    public static void NoteScriptUnlock() { lastScriptUnlockAt = Time.unscaledTime; }
    public static void ReleasePointer()
    {
        NoteScriptUnlock(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    public static void RestorePointer(EggboyUI ui)
    {
        var map = Object.FindObjectOfType<TownMap>();
        var director = Object.FindObjectOfType<StoryDirector>();
        if (ui == null || ui.Paused || ui.LetterOpen || ui.TitleShowing || ui.Cinematic || OverlayOpen
            || (map != null && map.IsOpen) || (director != null && (director.Cutscene || director.Climbing || director.Ended))) return;
        if (Application.platform == RuntimePlatform.WebGLPlayer) { ReleasePointer(); return; }
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    EggboyUI ui;
    TownMap map;
    StoryDirector director;
    Button guideButton;
    Text guideCaption;
    bool letterOpenLastFrame;
    bool wasLocked;
    float lastEscAt = -10f;
    float overlaySeenAt = -10f;

    // 只有网页版才把“鼠标锁定被解除”当成按了 Esc。审计里可以打开它在 Windows 上验证同一套判断
    public bool AutoPauseOnLockLoss;

    void Awake()
    {
        AutoPauseOnLockLoss = Application.platform == RuntimePlatform.WebGLPlayer;
    }

    public void Init(EggboyUI u, TownMap m, StoryDirector d)
    {
        ui = u;
        map = m;
        director = d;
        var canvas = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (map == null || canvas == null) return;
        guideButton = TideTownStyleUI.CreateWarmButton(canvas, "", new Vector2(0f, -133f),
            new Vector2(360f, 26f), false, () =>
            {
                map.CycleGuideMode();
                RefreshGuideCaption();
            });
        guideButton.name = "PauseGroundGuide";
        guideCaption = guideButton.GetComponentInChildren<Text>();
        // 2026-11-16 手账暂停菜单：挂进左页设置列表的第五项（EggboyUI 留的 PauseGuideSlot），样式和"默认移动""镜头自动回正"一样
        var slot = TideTownStyleUI.FindDeepChild(canvas, "PauseGuideSlot");
        if (slot != null)
        {
            guideButton.transform.SetParent(slot, false);
            var rt = (RectTransform)guideButton.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero; rt.anchoredPosition = Vector2.zero;
            TideTownStyleUI.StyleSettingButton(guideButton);
        }
        RefreshGuideCaption();
        guideButton.gameObject.SetActive(false);
    }

    void RefreshGuideCaption()
    {
        string mode;
        switch (map.GuideMode)
        {
            case TownMap.GroundGuideMode.Always: mode = "常显"; break;
            case TownMap.GroundGuideMode.Off: mode = "关闭"; break;
            default: mode = "柔和自动"; break;
        }
        guideCaption.text = "引路光点：" + mode;   // 和"默认移动：奔跑"同一种写法
    }

    // 现在有没有解密弹窗开着
    public static bool OverlayOpen =>
        ObjectInspectModal.IsOpen || WoodenMechanismPuzzleModal.IsOpen || AIAgentCalibrationDirector.IsOpen
        || AvantGardeGameplayHub.AnyModalOpen || WishInspectModal.IsOpen;

    void TrackOverlay()
    {
        if (OverlayOpen) overlaySeenAt = Time.unscaledTime;
    }

    // 弹窗开着，或者刚关上（0.5 秒内）
    public bool OverlayBusy
    {
        get
        {
            TrackOverlay();
            return Time.unscaledTime - overlaySeenAt < OverlayGrace;
        }
    }

    public bool Blocked =>
        ui.LetterOpen || letterOpenLastFrame || OverlayBusy
        || (map != null && (map.IsOpen || Time.unscaledTime - map.ClosedAt < 0.25f))
        || (director != null && (director.Cutscene || director.Climbing || director.Ended));

    // 按了 Esc 要不要开关暂停（审计直接调这个，不用真按键）
    public bool WouldTogglePause(bool esc) => esc && ui != null && !ui.TitleShowing && !Blocked;

    // 鼠标从锁定变成解锁，要不要当成“按了 Esc”自动暂停
    public bool WouldAutoPause(bool wasLockedBefore, bool lockedNow, bool focused)
    {
        return AutoPauseOnLockLoss && ui != null
            && wasLockedBefore && !lockedNow && focused && !ui.TitleShowing && !ui.Paused
            && !Blocked
            && Time.unscaledTime - lastEscAt > 0.3f
            && Time.unscaledTime - lastScriptUnlockAt > ScriptUnlockGrace;
    }

    void Update()
    {
        if (ui == null) return;
        bool esc = EasyInputBridge.Down(KeyCode.Escape);
        if (esc) lastEscAt = Time.unscaledTime;
        bool toggle = WouldTogglePause(esc);
        letterOpenLastFrame = ui.LetterOpen;
        if (toggle) ui.SetPaused(!ui.Paused);
    }

    void LateUpdate()
    {
        if (ui == null) return;
        TrackOverlay();
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (WouldAutoPause(wasLocked, locked, Application.isFocused)) ui.SetPaused(true);
        wasLocked = locked;
        if (guideButton != null && guideButton.gameObject.activeSelf != ui.Paused)
            guideButton.gameObject.SetActive(ui.Paused);
    }
}
