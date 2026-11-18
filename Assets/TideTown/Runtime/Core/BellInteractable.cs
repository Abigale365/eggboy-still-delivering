using UnityEngine;

public sealed class BellInteractable : IInteractable
{
	private readonly StoryDirector director;

	private readonly string label;

	private readonly string text;

	public Vector3 Position { get; }

	public string EntityId => "bell";

	public BellInteractable(Vector3 p, StoryDirector d, string itemLabel, string itemText)
	{
		Position = p;
		director = d;
		label = itemLabel;
		text = itemText;
	}

	public string Prompt(GameState s)
	{
		if (s == null || !s.seenMemories.Contains("bell_detail"))
		{
			return "E\u3000摇响门铃";
		}
		return "E\u3000摇响门铃（已发现刻痕）";
	}

	public bool CanInteract(GameState s)
	{
		return true;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.RingBellAt(Position, label, text);
	}
}
