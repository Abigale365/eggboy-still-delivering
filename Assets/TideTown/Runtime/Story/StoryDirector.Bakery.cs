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
        ShowCarriedModel("wood");
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
        ShowCarriedModel(null);
        AdvanceTo("B4_light");
    }

    public void LightOven(BakeryEffects fx)
    {
        if (!ready || State.step != "B4_light" || State.flags.Contains("oven_lit")) return;
        State.flags.Add("oven_lit");
        if (!State.stamps.Contains("bakery")) State.stamps.Add("bakery");
        store.Save(State);
        if (fx != null) fx.Light();
        if (water != null) water.SetLevel("L1", WaterLevel.StationRiseSeconds);
        if (player != null) player.PlayEmote("hop");
        ui.ShowToast("邮戳 1/3 · 面包店");
        AdvanceTo("S0_arrive");
    }

    public bool ShowMemory(string id, string text, bool replay)
    {
        if (!ready || ui == null || gate == null || gate.Locked) return false;
        if (!State.seenMemories.Contains(id))
        {
            State.seenMemories.Add(id);
            store.Save(State);
        }
        else if (!replay) return false;
        ui.ShowToast(text, 3f);
        return true;
    }

    void ShowCarriedModel(string item)
    {
        if (carriedVisual != null) Destroy(carriedVisual);
        carriedVisual = null;
        if (player == null || (item != "wood" && item != "oil")) return;
        modelProvider.Load(item, model =>
        {
            carriedVisual = Instantiate(model, player.transform, false);
            var localOffset = model.transform.localPosition;
            carriedVisual.transform.localPosition = localOffset + new Vector3(0.20f, 0.46f, 0.28f);
        }, message => Debug.LogWarning(message));
    }

    // ---- WP-13 学校 ----

}
