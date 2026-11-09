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

public interface IHoldInteraction
{
    float HoldSeconds(GameState state);
}

public sealed class InteractionService : MonoBehaviour
{
    public float range = 1.85f;

    IInteractable heldTarget;
    float heldTime;
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

    static int Priority(IInteractable it, GameState s)
    {
        if (it == null) return -1;
        // 主线任务可交互物永远优先于周边回忆旧物，避免回忆旧物抢走摊位/柴火/烤炉的 E 键
        if (!(it is MemoryInteractable))
            return it.CanInteract(s) ? 3 : 1;
        return 2;
    }

    IInteractable FindBest(GameState s, bool requireCanInteract)
    {
        IInteractable best = null;
        float bestD = range;
        int bestPrio = -1;
        foreach (var it in items)
        {
            if (requireCanInteract && !it.CanInteract(s)) continue;
            string p = it.Prompt(s);
            if (string.IsNullOrEmpty(p)) continue;
            float d = Vector3.Distance(player.transform.position, it.Position);
            if (d >= range) continue;
            int prio = Priority(it, s);
            if (prio > bestPrio || (prio == bestPrio && d < bestD))
            {
                bestPrio = prio;
                bestD = d;
                best = it;
            }
        }
        return best;
    }

    void Update()
    {
        if (player == null || ui == null) return;
        if (ui.Paused || ui.TitleShowing || player.locked || (gate != null && gate.Locked))
        { heldTarget = null; ui.SetPrompt(""); return; }

        var s = state();
        IInteractable best = FindBest(s, false);

        string prompt = best != null ? best.Prompt(s) : "";
        float duration = best is IHoldInteraction action ? action.HoldSeconds(s) : 0f;
        if (heldTarget != best || !Input.GetKey(KeyCode.E)) heldTarget = null;
        if (best != null && best.CanInteract(s))
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (duration <= 0f) best.Interact(s, this);
                else { heldTarget = best; heldTime = 0f; }
            }

            if (heldTarget != null && duration > 0f)
            {
                heldTime += Time.unscaledDeltaTime;
                if (heldTime >= duration)
                {
                    heldTarget = null;
                    best.Interact(s, this);
                }
                else prompt += $"  {Mathf.FloorToInt(100f * heldTime / duration)}%";
            }
            else if (best is MemoryInteractable memory && !ui.ToastVisible) memory.ShowNearby();
        }
        ui.SetPrompt(prompt);
    }

    public bool TryInteractNearest()
    {
        var s = state();
        IInteractable best = FindBest(s, true);
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
