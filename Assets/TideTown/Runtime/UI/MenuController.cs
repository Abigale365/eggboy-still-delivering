// WP-16：Esc 暂停。读信、过场、看地图时不抢 Esc；地图刚关掉的那一帧也不抢。
// 修正（2026-11-07）：
//   1. 关信的那一下 Esc 不再顺手打开暂停。EggboyUI 和本脚本谁先跑不固定，
//      所以“上一帧信还开着”也算读信中。
//   2. 网页版里第一下 Esc 被浏览器拿去退出鼠标锁定，游戏收不到这个键。
//      改成：游玩中鼠标锁定被意外解除，就当玩家按了 Esc，直接暂停。
using UnityEngine;

public sealed class MenuController : MonoBehaviour
{
    EggboyUI ui;
    TownMap map;
    StoryDirector director;
    bool letterOpenLastFrame;
    bool wasLocked;
    float lastEscAt = -10f;

    public void Init(EggboyUI u, TownMap m, StoryDirector d)
    {
        ui = u;
        map = m;
        director = d;
    }

    bool Blocked =>
        ui.LetterOpen || letterOpenLastFrame
        || (map != null && (map.IsOpen || Time.unscaledTime - map.ClosedAt < 0.25f))
        || (director != null && (director.Cutscene || director.Climbing || director.Ended));

    void Update()
    {
        if (ui == null) return;
        bool esc = Input.GetKeyDown(KeyCode.Escape);
        if (esc) lastEscAt = Time.unscaledTime;
        bool blocked = Blocked;
        letterOpenLastFrame = ui.LetterOpen;
        if (ui.TitleShowing || !esc || blocked) return;
        ui.SetPaused(!ui.Paused);
    }

    void LateUpdate()
    {
        if (ui == null) return;
        bool locked = Cursor.lockState == CursorLockMode.Locked;
#if UNITY_WEBGL && !UNITY_EDITOR
        // 锁定 → 解锁，而且不是游戏自己解的（读信、地图、暂停、刚按过 Esc、切走窗口都不算）
        if (wasLocked && !locked && Application.isFocused && !ui.TitleShowing && !ui.Paused
            && !Blocked && Time.unscaledTime - lastEscAt > 0.3f)
            ui.SetPaused(true);
#endif
        wasLocked = locked;
    }
}
