using UnityEngine;

public sealed class MemoryInteractable : IInteractable
{
	private StoryDirector director;

	private string id;

	private string label;

	private string text;

	public Vector3 Position { get; }

	public string EntityId => id;

	public MemoryInteractable(Vector3 p, StoryDirector d, string itemId, string itemLabel, string itemText)
	{
		Position = p;
		director = d;
		id = itemId;
		label = itemLabel;
		text = itemText;
	}

	public string Prompt(GameState s)
	{
		if (s != null && s.seenMemories.Contains(id + "_detail"))
		{
			return "E\u3000端详" + label + "（已发现刻痕）";
		}
		return ((s != null && s.seenMemories.Contains(id)) ? "E\u3000拿起端详" : "E\u3000看看") + label;
	}

	public bool CanInteract(GameState s)
	{
		return true;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.InspectMemory(id, label, text);
	}

	public void ShowNearby()
	{
		director.ShowMemory(id, text, replay: false);
	}
}
