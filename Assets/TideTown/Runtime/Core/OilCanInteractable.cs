using UnityEngine;

public sealed class OilCanInteractable : IInteractable, IObjectiveInteractable
{
	private StoryDirector director;

	public Vector3 Position { get; }

	public string EntityId => "oil";

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (s.step == "L1_shed")
			{
				return !s.flags.Contains("oil_taken");
			}
			return false;
		}
		return false;
	}

	public OilCanInteractable(Vector3 p, StoryDirector d)
	{
		Position = p;
		director = d;
	}

	public string Prompt(GameState s)
	{
		if (!(s.step == "L1_shed") || s.flags.Contains("oil_taken"))
		{
			return "";
		}
		return "E\u3000拿起油壶";
	}

	public bool CanInteract(GameState s)
	{
		if (s.step == "L1_shed")
		{
			return !s.flags.Contains("oil_taken");
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.TakeOilAt(Position);
	}
}
