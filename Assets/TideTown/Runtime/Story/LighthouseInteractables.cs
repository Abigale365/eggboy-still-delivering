// WP-14：船棚油壶、灯塔塔门、灯头三处互动。
// 允许玩家在 L2_door / L3_oil / L4_light 任意阶段随时上下灯塔，绝不把玩家困在塔顶。
using UnityEngine;

public sealed class OilCanInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    public OilCanInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "oil";
    public string Prompt(GameState s) =>
        s.step == "L1_shed" && !s.flags.Contains("oil_taken") ? "E　拿起油壶" : "";
    public bool CanInteract(GameState s) => s.step == "L1_shed" && !s.flags.Contains("oil_taken");
    public void Interact(GameState s, InteractionService svc) => director.TakeOil();
}

public sealed class LighthouseDoorInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    public LighthouseDoorInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "lighthouse_door";
    public string Prompt(GameState s) =>
        (s.step == "L2_door" || s.step == "L3_oil" || s.step == "L4_light") ? "E　进灯塔，爬上去" : "";
    public bool CanInteract(GameState s) =>
        s.step == "L2_door" || s.step == "L3_oil" || s.step == "L4_light";
    public void Interact(GameState s, InteractionService svc) => director.ClimbTower();
}

public sealed class LampHeadInteractable : IInteractable, IHoldInteraction
{
    public float HoldSeconds(GameState state) => state.step == "L4_light" ? 1.0f : 0f;

    public Vector3 Position { get; }
    StoryDirector director;
    public LampHeadInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "lamp_head";
    public string Prompt(GameState s)
    {
        if (s.step == "L3_oil" && !s.flags.Contains("lamp_oiled")) return "E　给灯头加油";
        if (s.step == "L4_light" && !s.flags.Contains("beam_lit")) return "按住 E　转动灯头";
        return "";
    }
    public bool CanInteract(GameState s) =>
        (s.step == "L3_oil" && !s.flags.Contains("lamp_oiled")) ||
        (s.step == "L4_light" && !s.flags.Contains("beam_lit"));
    public void Interact(GameState s, InteractionService svc)
    {
        if (s.step == "L3_oil") director.OilLamp();
        else director.LightLamp();
    }
}
