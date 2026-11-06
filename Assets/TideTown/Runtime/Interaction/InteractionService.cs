// WP-07 互动接口与最近目标服务。
using System.Collections.Generic;
using UnityEngine;

public interface IInteractable
{
    string EntityId { get; }
    Vector3 Position { get; }
    string Prompt(GameState s);      // 空串表示不显示
    bool CanInteract(GameState s);
    void Interact(GameState s, InteractionService svc);
}

public sealed class InteractionService : MonoBehaviour
{
    public float range = 1.6f;

    EggboyPlayer player;
    EggboyUI ui;
    ControlGate gate;
    System.Func<GameState> state;

    readonly List<IInteractable> items = new List<IInteractable>();

    public void Init(EggboyPlayer p, EggboyUI u, ControlGate g, System.Func<GameState> s)
    {
        player = p; ui = u; gate = g; state = s;
    }

    public void Register(IInteractable item) { if (!items.Contains(item)) items.Add(item); }
    public bool IsRegistered(IInteractable item) => items.Contains(item);

    void Update()
    {
        if (player == null || ui == null) return;
        if (gate != null && gate.Locked) { ui.SetPrompt(""); return; }

        var s = state();
        IInteractable best = null;
        float bestD = range;
        foreach (var it in items)
        {
            float d = Vector3.Distance(player.transform.position, it.Position);
            if (d < bestD && !string.IsNullOrEmpty(it.Prompt(s)))
            {
                bestD = d; best = it;
            }
        }

        ui.SetPrompt(best != null ? best.Prompt(s) : "");
        if (best != null && Input.GetKeyDown(KeyCode.E)) best.Interact(s, this);
    }

    public bool TryInteractNearest()
    {
        var s = state();
        IInteractable best = null; float bestD = range;
        foreach (var it in items)
        {
            float d = Vector3.Distance(player.transform.position, it.Position);
            if (d < bestD && it.CanInteract(s) && !string.IsNullOrEmpty(it.Prompt(s))) { bestD = d; best = it; }
        }
        if (best == null) return false;
        best.Interact(s, this);
        return true;
    }

    // 自动测试用固定 id，避免附近有回忆线索时点错最近的那个
    public bool TryInteract(string entityId)
    {
        var s = state();
        foreach (var it in items)
        {
            if (it.EntityId != entityId || !it.CanInteract(s)) continue;
            if (string.IsNullOrEmpty(it.Prompt(s))) continue;
            if (player != null && Vector3.Distance(player.transform.position, it.Position) > range) continue;
            it.Interact(s, this);
            return true;
        }
        return false;
    }
}

public sealed class MailboxInteractable : IInteractable
{
    public Vector3 Position { get; }
    StoryDirector director;
    public MailboxInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "mailbox_03";
    public string Prompt(GameState s) =>
        s.flags.Contains("letters_taken") ? "" : "E　取信";
    public bool CanInteract(GameState s) => !s.flags.Contains("letters_taken");
    public void Interact(GameState s, InteractionService svc) => director.TakeLetters();
}
