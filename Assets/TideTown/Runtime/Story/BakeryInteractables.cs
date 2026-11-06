// WP-11 面包店互动：门前摊位拿火柴、后院抱柴火、烤炉装柴和点火，以及 A4–A8 回忆线索。
using UnityEngine;

public sealed class StallInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    public StallInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "stall";
    public string Prompt(GameState s) =>
        s.step == "B1_matches" && !s.flags.Contains("matches_taken") ? "E　拿火柴盒" : "";
    public bool CanInteract(GameState s) => s.step == "B1_matches" && !s.flags.Contains("matches_taken");
    public void Interact(GameState s, InteractionService svc) => director.TakeMatches();
}

public sealed class WoodInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    public WoodInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "wood";
    public string Prompt(GameState s) => s.step == "B2_wood" && s.carrying != "wood" ? "E　抱起柴火捆" : "";
    public bool CanInteract(GameState s) => s.step == "B2_wood" && s.carrying != "wood";
    public void Interact(GameState s, InteractionService svc) => director.TakeWood();
}

public sealed class StoveInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    BakeryEffects effects;
    public StoveInteractable(Vector3 p, StoryDirector d, BakeryEffects fx) { Position = p; director = d; effects = fx; }
    public string EntityId => "stove";
    public string Prompt(GameState s)
    {
        if (s.step == "B3_load" && s.carrying == "wood") return "E　把柴火放进炉膛";
        if (s.step == "B4_light" && !s.flags.Contains("oven_lit")) return "E　点火";
        return "";
    }
    public bool CanInteract(GameState s)
    {
        if (s.step == "B3_load" && s.carrying == "wood") return true;
        return s.step == "B4_light" && !s.flags.Contains("oven_lit");
    }
    public void Interact(GameState s, InteractionService svc)
    {
        if (s.step == "B3_load") director.LoadWood();
        else if (s.step == "B4_light") director.LightOven(effects);
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
    public string Prompt(GameState s) =>
        (s.seenMemories.Contains(id) ? "E　再读" : "E　看看") + label;
    public bool CanInteract(GameState s) => true;
    public void Interact(GameState s, InteractionService svc) =>
        director.ShowMemory(id, "回忆 · " + label, text);
}
