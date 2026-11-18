using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class InteractionService : MonoBehaviour
{
	public float range = 1.85f;

	private IInteractable heldTarget;

	private float heldTime;

	private string activeHoldEmote;

	private EggboyPlayer player;

	private EggboyUI ui;

	private ControlGate gate;

	private Func<GameState> state;

	private readonly List<IInteractable> items = new List<IInteractable>();

	public void Init(EggboyPlayer p, EggboyUI u, ControlGate g, Func<GameState> s)
	{
		player = p;
		ui = u;
		gate = g;
		state = s;
	}

	public void Register(IInteractable item)
	{
		if (!items.Contains(item))
		{
			items.Add(item);
		}
	}

	public bool IsRegistered(IInteractable item)
	{
		return items.Contains(item);
	}

	public bool TryGetPosition(string entityId, out Vector3 position)
	{
		foreach (IInteractable item in items)
		{
			if (!(item.EntityId != entityId))
			{
				position = item.Position;
				return true;
			}
		}
		position = default(Vector3);
		return false;
	}

	private static int Priority(IInteractable it, GameState s)
	{
		if (it == null)
		{
			return -1;
		}
		if (!it.CanInteract(s))
		{
			return 1;
		}
		if (it is IObjectiveInteractable objectiveInteractable)
		{
			if (!objectiveInteractable.IsObjective(s))
			{
				return 2;
			}
			return 4;
		}
		if (!(it is MemoryInteractable))
		{
			return 3;
		}
		return 2;
	}

	private IInteractable FindBest(GameState s, bool requireCanInteract)
	{
		IInteractable result = null;
		float num = range;
		int num2 = -1;
		foreach (IInteractable item in items)
		{
			if ((requireCanInteract && !item.CanInteract(s)) || string.IsNullOrEmpty(item.Prompt(s)))
			{
				continue;
			}
			float num3 = Vector3.Distance(player.transform.position, item.Position);
			if (!(num3 >= range))
			{
				int num4 = Priority(item, s);
				if (num4 > num2 || (num4 == num2 && num3 < num))
				{
					num2 = num4;
					num = num3;
					result = item;
				}
			}
		}
		return result;
	}

	private void Update()
	{
		if (player == null || ui == null)
		{
			return;
		}
		if (ui.Paused || ui.TitleShowing || player.locked || (gate != null && gate.Locked) || EmoteWheel.IsOpen || WishInspectModal.IsOpen)
		{
			heldTarget = null;
			ui.SetPrompt("");
			return;
		}
		GameState s = state();
		IInteractable interactable = FindBest(s, requireCanInteract: false);
		string text = ((interactable != null) ? interactable.Prompt(s) : "");
		float num = ((interactable is IHoldInteraction holdInteraction) ? holdInteraction.HoldSeconds(s) : 0f);
		if (heldTarget != interactable || !EasyInputBridge.Held(KeyCode.E))
		{
			if (heldTarget != null && !string.IsNullOrEmpty(activeHoldEmote) && player != null)
			{
				player.StopEmote(activeHoldEmote);
			}
			heldTarget = null;
			activeHoldEmote = null;
		}
		if (interactable != null && interactable.CanInteract(s))
		{
			if (EasyInputBridge.Down(KeyCode.E))
			{
				player.FaceTowards(interactable.Position);
				if (num <= 0f)
				{
					interactable.Interact(s, this);
				}
				else
				{
					heldTarget = interactable;
					heldTime = 0f;
					activeHoldEmote = ((interactable is IHoldInteraction holdInteraction2) ? holdInteraction2.HoldEmote(s) : null);
					if (!string.IsNullOrEmpty(activeHoldEmote))
					{
						player.PlayEmote(activeHoldEmote, int.MaxValue);
					}
				}
			}
			if (heldTarget != null && num > 0f)
			{
				player.FaceTowards(heldTarget.Position);
				player.HoldActionLock(0.12f);
				heldTime += Time.unscaledDeltaTime;
				if (heldTime >= num)
				{
					if (!string.IsNullOrEmpty(activeHoldEmote))
					{
						player.StopEmote(activeHoldEmote);
					}
					heldTarget = null;
					activeHoldEmote = null;
					interactable.Interact(s, this);
				}
				else
				{
					text += $"  {Mathf.FloorToInt(100f * heldTime / num)}%";
				}
			}
			else if (interactable is MemoryInteractable memoryInteractable && !ui.ToastVisible)
			{
				memoryInteractable.ShowNearby();
			}
		}
		ui.SetPrompt(text, interactable != null && Priority(interactable, s) >= 3);
	}

	public bool TryInteractNearest()
	{
		GameState s = state();
		IInteractable interactable = FindBest(s, requireCanInteract: true);
		if (interactable == null)
		{
			return false;
		}
		if (player != null)
		{
			player.FaceTowards(interactable.Position);
		}
		interactable.Interact(s, this);
		return true;
	}

	public bool TryInteract(string entityId)
	{
		GameState s = state();
		foreach (IInteractable item in items)
		{
			if (!(item.EntityId != entityId) && item.CanInteract(s) && !string.IsNullOrEmpty(item.Prompt(s)) && (!(player != null) || !(Vector3.Distance(player.transform.position, item.Position) > range)))
			{
				if (player != null)
				{
					player.FaceTowards(item.Position);
				}
				item.Interact(s, this);
				return true;
			}
		}
		return false;
	}
}
