// WP-15：结局的课桌、以及塔顶平台的下塔互动。
// 支持在 L3_oil / L4_light 解谜未完成时也可从塔顶南侧旋梯口（或按 B 键）随时下塔回地面，绝不困住玩家。
using UnityEngine;

public sealed class EndingDeskInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    public EndingDeskInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "ending_desk";
    public string Prompt(GameState s) =>
        s.step == "E1_read" && !s.flags.Contains("ending_letter_read") ? "E　读老周的信" : "";
    public bool CanInteract(GameState s) => s.step == "E1_read" && !s.flags.Contains("ending_letter_read");
    public void Interact(GameState s, InteractionService svc) => director.ReadEndingLetter();
}

public sealed class LighthouseDownInteractable : IInteractable
{
    readonly Vector3 basePos;
    readonly StoryDirector director;

    public LighthouseDownInteractable(Vector3 p, StoryDirector d)
    {
        basePos = p;
        director = d;
    }

    public Vector3 Position
    {
        get
        {
            var s = director != null ? director.State : null;
            if (s != null && (s.step == "L5_reply" || s.step == "E0_back"))
                return basePos;
            // 在 L3_oil / L4_light 期间把下塔旋梯口移到塔顶南侧护栏边，避免与塔中央的灯头互动重叠
            return basePos + new Vector3(-0.6f, 0f, -1.75f);
        }
    }

    public string EntityId => "lighthouse_down";

    public string Prompt(GameState s)
    {
        if (s.step == "L5_reply" || s.step == "E0_back") return "E　下塔回地面";
        if (s.step == "L3_oil" || s.step == "L4_light" || s.step == "L1_shed" || s.step == "L2_door")
            return "E　顺旋梯暂回地面（或按 B 键）";
        return "";
    }

    public bool CanInteract(GameState s) =>
        s.step == "L5_reply" || s.step == "E0_back" ||
        s.step == "L3_oil" || s.step == "L4_light" ||
        s.step == "L1_shed" || s.step == "L2_door";

    public void Interact(GameState s, InteractionService svc) => director.DescendTower();
}
