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
    string HoldEmote(GameState state);
}

// 可选接口：辅助机关沿用原 IInteractable，不承担主线目标判断。
public interface IObjectiveInteractable
{
    bool IsObjective(GameState state);
}

public sealed class InteractionService : MonoBehaviour
{
    public float range = 1.85f;

    IInteractable heldTarget;
    float heldTime;
    string activeHoldEmote;
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

    public bool TryGetPosition(string entityId, out Vector3 position)
    {
        foreach (var item in items)
        {
            if (item.EntityId != entityId) continue;
            position = item.Position;
            return true;
        }
        position = default;
        return false;
    }

    static int Priority(IInteractable it, GameState s)
    {
        if (it == null) return -1;
        if(!it.CanInteract(s))return 1;
        if(it is IObjectiveInteractable objective)return objective.IsObjective(s)?4:2;
        return it is MemoryInteractable?2:3;
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
        // 2026-11-16：表情轮盘 / 心愿端详开着时不显示也不响应 [E]（提示条会压在轮盘下沿的说明上）
        if (ui.Paused || ui.TitleShowing || player.locked || (gate != null && gate.Locked) || EmoteWheel.IsOpen || WishInspectModal.IsOpen)
        { heldTarget = null; ui.SetPrompt(""); return; }

        var s = state();
        IInteractable best = FindBest(s, false);

        string prompt = best != null ? best.Prompt(s) : "";
        float duration = best is IHoldInteraction action ? action.HoldSeconds(s) : 0f;
        if (heldTarget != best || !Input.GetKey(KeyCode.E))
        {
            if (heldTarget != null && !string.IsNullOrEmpty(activeHoldEmote) && player != null)
                player.StopEmote(activeHoldEmote);
            heldTarget = null;
            activeHoldEmote = null;
        }
        if (best != null && best.CanInteract(s))
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                player.FaceTowards(best.Position);
                if (duration <= 0f) best.Interact(s, this);
                else
                {
                    heldTarget = best;
                    heldTime = 0f;
                    activeHoldEmote = best is IHoldInteraction ha ? ha.HoldEmote(s) : null;
                    if (!string.IsNullOrEmpty(activeHoldEmote))
                        player.PlayEmote(activeHoldEmote, int.MaxValue);
                }
            }

            if (heldTarget != null && duration > 0f)
            {
                player.FaceTowards(heldTarget.Position);
                player.HoldActionLock(0.12f);
                heldTime += Time.unscaledDeltaTime;
                if (heldTime >= duration)
                {
                    if (!string.IsNullOrEmpty(activeHoldEmote))
                        player.StopEmote(activeHoldEmote);
                    heldTarget = null;
                    activeHoldEmote = null;
                    best.Interact(s, this);
                }
                else prompt += $"  {Mathf.FloorToInt(100f * heldTime / duration)}%";
            }
            else if (best is MemoryInteractable memory && !ui.ToastVisible) memory.ShowNearby();
        }
        ui.SetPrompt(prompt, best != null && Priority(best,s)>=3);
    }

    public bool TryInteractNearest()
    {
        var s = state();
        IInteractable best = FindBest(s, true);
        if (best == null) return false;
        if (player != null) player.FaceTowards(best.Position);
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
            if (player != null) player.FaceTowards(it.Position);
            it.Interact(s, this);
            return true;
        }
        return false;
    }
}

public sealed class MailboxInteractable : IInteractable, IObjectiveInteractable
{
    public bool IsObjective(GameState s) => s!=null&&!s.flags.Contains("letters_taken");
    public Vector3 Position { get; }
    StoryDirector director;
    public MailboxInteractable(Vector3 p, StoryDirector d) { Position = p; director = d; }
    public string EntityId => "mailbox_03";
    public string Prompt(GameState s) =>
        s.flags.Contains("letters_taken") ? "" : "E　取信";
    public bool CanInteract(GameState s) => !s.flags.Contains("letters_taken");
    public void Interact(GameState s, InteractionService svc) => director.TakeLetters();
}
