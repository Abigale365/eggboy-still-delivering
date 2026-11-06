// WP-07 剧情推进，WP-11 起覆盖面包店：到点自动读信、火柴/柴火/烤炉链条、回忆线索和第一枚邮戳。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
    public void TakeMatches()
    {
        if (!ready || State.step != "B1_matches" || State.flags.Contains("matches_taken")) return;
        State.flags.Add("matches_taken");
        State.carrying = "matches";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        AdvanceTo("B2_wood");
    }

    public void TakeWood()
    {
        if (!ready || State.step != "B2_wood") return;
        if (State.flags.Contains("matches_taken") && !State.flags.Contains("matches_packed")) State.flags.Add("matches_packed");
        State.carrying = "wood";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        ShowWood(true);
        AdvanceTo("B3_load");
    }

    public void LoadWood()
    {
        if (!ready || State.step != "B3_load" || State.carrying != "wood") return;
        State.flags.Add("wood_loaded");
        State.carrying = "";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried("");
        ShowWood(false);
        AdvanceTo("B4_light");
    }

    public void LightOven(BakeryEffects fx)
    {
        if (!ready || State.step != "B4_light" || State.flags.Contains("oven_lit")) return;
        State.flags.Add("oven_lit");
        if (!State.stamps.Contains("bakery")) State.stamps.Add("bakery");
        store.Save(State);
        if (fx != null) fx.Light();
        if (water != null) water.SetLevel("L1", 2f);
        if (player != null) player.PlayEmote("hop");
        ui.ShowToast("邮戳 1/3 · 面包店");
        AdvanceTo("S0_arrive");
    }

    public bool ShowMemory(string id, string title, string text)
    {
        if (!ready || ui == null || gate == null || gate.Locked) return false;
        if (!State.seenMemories.Contains(id)) { State.seenMemories.Add(id); store.Save(State); }
        letterLock = gate.Acquire("memory");
        if (letterLock == null) return false;
        readingMemory = true;
        if (player != null) player.locked = true;
        ui.ShowMemory(title, text);
        return true;
    }

    void ShowWood(bool on)
    {
        if (on && woodVisual == null && player != null)
        {
            woodVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(woodVisual.GetComponent<Collider>());
            woodVisual.name = "CarriedWood";
            woodVisual.transform.SetParent(player.transform, false);
            woodVisual.transform.localPosition = new Vector3(0f, 0.52f, -0.30f);
            woodVisual.transform.localScale = new Vector3(0.5f, 0.2f, 0.28f);
            woodVisual.GetComponent<Renderer>().material.color = new Color(0.55f, 0.36f, 0.2f);
        }
        if (!on && woodVisual != null) { Destroy(woodVisual); woodVisual = null; }
    }

    // ---- WP-13 学校 ----

}
