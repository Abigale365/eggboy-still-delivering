using UnityEngine;

public sealed class BottlePickup : IInteractable
{
	private readonly ShadowLanternPuzzle p;

	public string EntityId => "sound_bottle_pickup";

	public Vector3 Position => p.BottlePos;

	public BottlePickup(ShadowLanternPuzzle puzzle)
	{
		p = puzzle;
	}

	public string Prompt(GameState s)
	{
		if (!p.Solved || p.BottleTaken)
		{
			return "";
		}
		return "E\u3000拿起空瓶";
	}

	public bool CanInteract(GameState s)
	{
		if (p.Solved)
		{
			return !p.BottleTaken;
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		p.TakeBottle();
	}
}
