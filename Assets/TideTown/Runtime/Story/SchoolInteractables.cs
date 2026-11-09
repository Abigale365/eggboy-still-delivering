// WP-13：学校的拖鞋（支持 3D 检视）、红球和狗碗。支持灵活先后顺序，避免玩家先看到秋千红球却点不了。
using UnityEngine;

public sealed class SlipperInteractable : IInteractable
{
    readonly Vector3 pos;
    readonly StoryDirector director;
    public SlipperInteractable(Vector3 p, StoryDirector d) { pos = p; director = d; }
    public string EntityId => "slipper";
    public Vector3 Position => pos;
    static bool Active(GameState s) =>
        s != null && (s.step == "S0_arrive" || s.step == "S1_slipper" || (s.step == "S2_ball" && !s.flags.Contains("slipper_seen")));
    public string Prompt(GameState s) => Active(s) ? "E　拿起端详拖鞋" : "";
    public bool CanInteract(GameState s) => Active(s);
    public void Interact(GameState s, InteractionService svc) => director.InspectSlipper();
}

public sealed class BallInteractable : IInteractable
{
    readonly Vector3 pos;
    readonly StoryDirector director;
    public BallInteractable(Vector3 p, StoryDirector d) { pos = p; director = d; }
    public string EntityId => "ball";
    public Vector3 Position => pos;
    static bool Active(GameState s, StoryDirector d) =>
        s != null && !s.flags.Contains("ball_taken")
        && (s.step == "S2_ball" || (d != null && !d.IsAuditMode && (s.step == "S0_arrive" || s.step == "S1_slipper")));
    public string Prompt(GameState s) => Active(s, director) ? "E　捡起红球" : "";
    public bool CanInteract(GameState s) => Active(s, director);
    public void Interact(GameState s, InteractionService svc) => director.TakeBall();
}

public sealed class BowlInteractable : IInteractable
{
    readonly Vector3 pos;
    readonly StoryDirector director;
    public BowlInteractable(Vector3 p, StoryDirector d) { pos = p; director = d; }
    public string EntityId => "bowl";
    public Vector3 Position => pos;
    public string Prompt(GameState s)
    {
        if (s == null) return "";
        if (s.carrying == "ball" && !s.flags.Contains("ball_in_bowl")) return "E　把红球滚向狗碗";
        if (director != null && !director.IsAuditMode && (s.step == "S0_arrive" || s.step == "S1_slipper" || s.step == "S2_ball" || s.step == "S3_bowl") && !s.flags.Contains("ball_in_bowl"))
            return "E　查看小黑的空食碗";
        return "";
    }
    public bool CanInteract(GameState s)
    {
        if (s == null || s.flags.Contains("ball_in_bowl")) return false;
        if (s.carrying == "ball") return true;
        return director != null && !director.IsAuditMode && (s.step == "S0_arrive" || s.step == "S1_slipper" || s.step == "S2_ball" || s.step == "S3_bowl");
    }
    public void Interact(GameState s, InteractionService svc)
    {
        if (s != null && s.carrying == "ball")
        {
            director.PutBallInBowl();
            return;
        }
        if (director != null && !director.IsAuditMode)
        {
            var ui = Object.FindObjectOfType<EggboyUI>();
            if (ui != null)
                ui.ShowToast("空食碗旁压着一张纸条：『小黑想吃顿好的，或者有个新玩具陪它玩。』", 4.2f);
            AIAgentCalibrationDirector.ToggleConsole();
        }
    }
}
