using System;
using UnityEngine;

public sealed class DroppedItemPickup : IInteractable, IObjectiveInteractable
{
	private readonly string item;

	private readonly StoryDirector director;

	private readonly GameObject visual;

	private readonly bool keepVisual;

	private readonly Action take;

	private bool gone;

	public Vector3 Position { get; }

	public string EntityId => "dropped_" + item;

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			return Valid(s);
		}
		return false;
	}

	public DroppedItemPickup(string itemId, Vector3 pos, StoryDirector d, GameObject v, bool keepVisualObject, Action takeAction)
	{
		item = itemId;
		Position = pos;
		director = d;
		visual = v;
		keepVisual = keepVisualObject;
		take = takeAction;
	}

	private bool Valid(GameState s)
	{
		if (!gone && s != null)
		{
			return s.carrying != item;
		}
		return false;
	}

	public string Prompt(GameState s)
	{
		if (!Valid(s))
		{
			return "";
		}
		return "E\u3000捡起来";
	}

	public bool CanInteract(GameState s)
	{
		return Valid(s);
	}

	public void Interact(GameState s, InteractionService svc)
	{
		if (!Valid(s))
		{
			return;
		}
		gone = true;
		if (director != null)
		{
			director.PickupDroppedItem(visual, keepVisual, take);
			return;
		}
		if (visual != null && !keepVisual)
		{
			UnityEngine.Object.Destroy(visual);
		}
		take?.Invoke();
	}
}
