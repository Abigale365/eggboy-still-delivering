using UnityEngine;

public sealed class PawChestInteractable : IInteractable
{
	private readonly DogPawPlate plate;

	public string EntityId => "paw_chest";

	public Vector3 Position => plate.ChestRest;

	public PawChestInteractable(DogPawPlate p)
	{
		plate = p;
	}

	public string Prompt(GameState s)
	{
		if (!plate.ChestReady || plate.ChestOpened)
		{
			return "";
		}
		return "E\u3000打开木匣";
	}

	public bool CanInteract(GameState s)
	{
		if (plate.ChestReady)
		{
			return !plate.ChestOpened;
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		plate.OpenChest();
	}
}
