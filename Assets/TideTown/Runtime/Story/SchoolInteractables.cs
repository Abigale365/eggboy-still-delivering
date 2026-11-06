// WP-13：学校的拖鞋、红球和狗碗。回忆类道具继续用 MemoryInteractable。
using UnityEngine;

public sealed class SlipperInteractable : IInteractable
{
    readonly Vector3 pos;
    readonly StoryDirector director;
    public SlipperInteractable(Vector3 p, StoryDirector d) { pos = p; director = d; }
    public string EntityId => "slipper";
    public Vector3 Position => pos;
    public string Prompt(GameState s) => s.step == "S1_slipper" ? "E　看看拖鞋" : "";
    public bool CanInteract(GameState s) => s.step == "S1_slipper";
    public void Interact(GameState s, InteractionService svc) => director.InspectSlipper();
}

public sealed class BallInteractable : IInteractable
{
    readonly Vector3 pos;
    readonly StoryDirector director;
    public BallInteractable(Vector3 p, StoryDirector d) { pos = p; director = d; }
    public string EntityId => "ball";
    public Vector3 Position => pos;
    public string Prompt(GameState s) =>
        s.step == "S2_ball" && !s.flags.Contains("ball_taken") ? "E　捡起红球" : "";
    public bool CanInteract(GameState s) => s.step == "S2_ball" && !s.flags.Contains("ball_taken");
    public void Interact(GameState s, InteractionService svc) => director.TakeBall();
}

public sealed class BowlInteractable : IInteractable
{
    readonly Vector3 pos;
    readonly StoryDirector director;
    public BowlInteractable(Vector3 p, StoryDirector d) { pos = p; director = d; }
    public string EntityId => "bowl";
    public Vector3 Position => pos;
    public string Prompt(GameState s) =>
        s.step == "S3_bowl" && s.carrying == "ball" ? "E　把球放进狗碗" : "";
    public bool CanInteract(GameState s) => s.step == "S3_bowl" && s.carrying == "ball";
    public void Interact(GameState s, InteractionService svc) => director.PutBallInBowl();
}
