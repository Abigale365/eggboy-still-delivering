// WP-16：Esc 暂停。读信、过场、看地图时不抢 Esc；地图刚关掉的那一帧也不抢。
using UnityEngine;

public sealed class MenuController : MonoBehaviour
{
    EggboyUI ui;
    TownMap map;
    StoryDirector director;

    public void Init(EggboyUI u, TownMap m, StoryDirector d)
    {
        ui = u;
        map = m;
        director = d;
    }

    void Update()
    {
        if (ui == null || ui.TitleShowing) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (ui.LetterOpen) return;
        if (map != null && (map.IsOpen || Time.unscaledTime - map.ClosedAt < 0.25f)) return;
        if (director != null && (director.Cutscene || director.Climbing || director.Ended)) return;
        ui.SetPaused(!ui.Paused);
    }
}
