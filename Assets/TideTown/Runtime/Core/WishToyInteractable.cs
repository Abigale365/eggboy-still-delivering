using UnityEngine;

public sealed class WishToyInteractable : IInteractable
{
	private readonly WishToy toy;

	public string EntityId => "wish_toy_" + toy.number;

	public Vector3 Position
	{
		get
		{
			if (!(toy != null) || !toy.gameObject.activeInHierarchy)
			{
				return new Vector3(0f, -999f, 0f);
			}
			return toy.WorldBounds().center;
		}
	}

	public WishToyInteractable(WishToy t)
	{
		toy = t;
	}

	public string Prompt(GameState s)
	{
		if (!(toy != null) || !toy.CanPick)
		{
			return "";
		}
		return "E\u3000拿起「" + toy.description + "」";
	}

	public bool CanInteract(GameState s)
	{
		if (toy != null && toy.CanPick && !WishInspectModal.IsOpen)
		{
			return !ObjectInspectModal.IsOpen;
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		if (toy != null)
		{
			toy.BeginPick();
		}
	}
}
