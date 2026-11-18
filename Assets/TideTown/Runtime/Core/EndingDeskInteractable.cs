using UnityEngine;

public sealed class EndingDeskInteractable : IInteractable, IObjectiveInteractable
{
	private StoryDirector director;

	public Vector3 Position { get; }

	public string EntityId => "ending_desk";

	public bool IsObjective(GameState s)
	{
		return CanInteract(s);
	}

	public EndingDeskInteractable(Vector3 p, StoryDirector d)
	{
		Position = p;
		director = d;
	}

	public string Prompt(GameState s)
	{
		if (!CanInteract(s))
		{
			return "";
		}
		return "E\u3000读老周的信";
	}

	public bool CanInteract(GameState s)
	{
		if (s != null)
		{
			return s.step == "E1_read";
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.ReadEndingLetter();
	}
}
