using UnityEngine;

public sealed class BallInteractable : IInteractable, IObjectiveInteractable
{
	private readonly Vector3 pos;

	private readonly StoryDirector director;

	public string EntityId => "ball";

	public Vector3 Position => pos;

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (s.step == "S2_ball")
			{
				return !s.flags.Contains("ball_taken");
			}
			return false;
		}
		return false;
	}

	public BallInteractable(Vector3 p, StoryDirector d)
	{
		pos = p;
		director = d;
	}

	private static bool Active(GameState s, StoryDirector d)
	{
		if (s != null && !s.flags.Contains("ball_taken"))
		{
			if (!(s.step == "S2_ball"))
			{
				if (d != null && !d.IsAuditMode)
				{
					if (!(s.step == "S0_arrive"))
					{
						return s.step == "S1_slipper";
					}
					return true;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public string Prompt(GameState s)
	{
		if (!Active(s, director))
		{
			return "";
		}
		return "E\u3000捡起红球";
	}

	public bool CanInteract(GameState s)
	{
		return Active(s, director);
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.TakeBallAt();
	}
}
