using UnityEngine;

public sealed class TurntablePrompt : IInteractable
{
	private readonly ShadowLanternPuzzle p;

	public string EntityId => "shadow_turntable";

	public Vector3 Position => p.TurntablePos;

	public TurntablePrompt(ShadowLanternPuzzle puzzle)
	{
		p = puzzle;
	}

	public string Prompt(GameState s)
	{
		if (!p.Solved)
		{
			return "按住 E\u3000转杂物架";
		}
		return "";
	}

	public bool CanInteract(GameState s)
	{
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
	}
}
