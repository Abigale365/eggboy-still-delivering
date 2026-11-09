// WP-11 面包店互动：门前摊位拿火柴、后院抱柴火、烤炉装柴和点火（含按住 E 擦火柴与三段鼓风助燃），以及 A4–A8 回忆线索 3D 检视。
using UnityEngine;

public sealed class StallInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    public StallInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "stall";
    static bool InBakeryPhase(GameState s) =>
        s != null && (s.step == "B0_arrive" || s.step == "B1_matches" || s.step == "B2_wood" || s.step == "B3_load" || s.step == "B4_light");

    public string Prompt(GameState s) =>
        InBakeryPhase(s) && !s.flags.Contains("matches_taken") ? "E　拿火柴盒" : "";
    public bool CanInteract(GameState s) => InBakeryPhase(s) && !s.flags.Contains("matches_taken");
    public void Interact(GameState s, InteractionService svc) => director.TakeMatches();
}

public sealed class WoodInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    public WoodInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "wood";
    static bool CanPickWood(GameState s) =>
        s != null
        && (s.step == "B0_arrive" || s.step == "B1_matches" || s.step == "B2_wood")
        && !s.flags.Contains("wood_loaded")
        && s.carrying != "wood";

    public string Prompt(GameState s) => CanPickWood(s) ? "E　抱起柴火捆" : "";
    public bool CanInteract(GameState s) => CanPickWood(s);
    public void Interact(GameState s, InteractionService svc) => director.TakeWood();
}

public sealed class StoveInteractable : IInteractable, IHoldInteraction
{
    public float HoldSeconds(GameState state) =>
        state != null && state.flags.Contains("wood_loaded") && state.flags.Contains("matches_taken") && !state.flags.Contains("oven_lit") ? 0.7f : 0f;
    public Vector3 Position { get; }
    StoryDirector director;
    BakeryEffects effects;
    public StoveInteractable(Vector3 p, StoryDirector d, BakeryEffects fx) { Position = p; director = d; effects = fx; }
    public string EntityId => "stove";
    public string Prompt(GameState s)
    {
        if (s == null) return "";
        if (s.carrying == "wood" && !s.flags.Contains("wood_loaded")) return "E　把柴火放进炉膛";
        if (s.flags.Contains("wood_loaded") && s.flags.Contains("matches_taken") && !s.flags.Contains("oven_lit"))
            return "按住 E　擦亮火柴";
        if (s.flags.Contains("oven_lit") && effects != null && effects.BellowsStage < 3 && director != null && !director.IsAuditMode)
            return "E　拉动风箱助燃（" + (effects.BellowsStage + 1) + "/3）";
        if (director != null && !director.IsAuditMode && !s.flags.Contains("oven_lit"))
        {
            if (!s.flags.Contains("wood_loaded")) return "炉膛空空如也，还缺一捆干柴";
            if (!s.flags.Contains("matches_taken")) return "干柴已塞入炉膛，还差火柴引火";
        }
        return "";
    }
    public bool CanInteract(GameState s)
    {
        if (s == null) return false;
        if (s.carrying == "wood" && !s.flags.Contains("wood_loaded")) return true;
        if (s.flags.Contains("wood_loaded") && s.flags.Contains("matches_taken") && !s.flags.Contains("oven_lit")) return true;
        if (s.flags.Contains("oven_lit") && effects != null && effects.BellowsStage < 3 && director != null && !director.IsAuditMode)
            return true;
        return false;
    }
    public void Interact(GameState s, InteractionService svc)
    {
        if (s.carrying == "wood" && !s.flags.Contains("wood_loaded")) director.LoadWood();
        else if (s.flags.Contains("wood_loaded") && s.flags.Contains("matches_taken") && !s.flags.Contains("oven_lit")) director.LightOven(effects);
        else if (s.flags.Contains("oven_lit") && effects != null && effects.BellowsStage < 3) director.BoostOvenBellows(effects);
    }
}

public sealed class MemoryInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    string id, label, text;
    public MemoryInteractable(Vector3 p, StoryDirector d, string itemId, string itemLabel, string itemText)
    { Position = p; director = d; id = itemId; label = itemLabel; text = itemText; }
    public string EntityId => id;
    public string Prompt(GameState s)
    {
        bool hasDetail = s != null && s.seenMemories.Contains(id + "_detail");
        if (hasDetail) return "E　端详" + label + "（已发现刻痕）";
        return (s != null && s.seenMemories.Contains(id) ? "E　拿起端详" : "E　看看") + label;
    }
    public bool CanInteract(GameState s) => true;
    public void Interact(GameState s, InteractionService svc) =>
        director.InspectMemory(id, label, text);

    public void ShowNearby() => director.ShowMemory(id, text, false);
}
