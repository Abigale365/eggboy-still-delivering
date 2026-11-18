using UnityEngine;

public sealed class StallInteractable : IInteractable, IObjectiveInteractable
{
	private StoryDirector director;

	public Vector3 Position { get; }

	public string EntityId => "stall";

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (s.step == "B1_matches")
			{
				return !s.flags.Contains("matches_taken");
			}
			return false;
		}
		return false;
	}

	public StallInteractable(Vector3 p, StoryDirector d)
	{
		Position = p;
		director = d;
	}

	private static bool InBakeryPhase(GameState s)
	{
		if (s != null)
		{
			if (!(s.step == "P1_mailbox") && !(s.step == "B0_arrive") && !(s.step == "B1_matches") && !(s.step == "B2_wood") && !(s.step == "B3_load"))
			{
				return s.step == "B4_light";
			}
			return true;
		}
		return false;
	}

	public string Prompt(GameState s)
	{
		if (!InBakeryPhase(s) || s.flags.Contains("matches_taken"))
		{
			return "";
		}
		return "E\u3000拿火柴盒";
	}

	public bool CanInteract(GameState s)
	{
		if (InBakeryPhase(s))
		{
			return !s.flags.Contains("matches_taken");
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.TakeMatchesAt(Position);
	}
}
