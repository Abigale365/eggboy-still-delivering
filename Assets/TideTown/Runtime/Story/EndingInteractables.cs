// WP-15：结局的课桌、以及塔顶平台的下塔互动。
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
    public Vector3 Position { get; }
    StoryDirector director;
    public LighthouseDownInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "lighthouse_down";
    public string Prompt(GameState s) =>
        (s.step == "L5_reply" || s.step == "E0_back") ? "E　下塔回地面" : "";
    public bool CanInteract(GameState s) => s.step == "L5_reply" || s.step == "E0_back";
    public void Interact(GameState s, InteractionService svc) => director.DescendTower();
}
