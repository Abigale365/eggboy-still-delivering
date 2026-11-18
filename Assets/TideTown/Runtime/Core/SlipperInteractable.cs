using UnityEngine;

public sealed class SlipperInteractable : IInteractable, IObjectiveInteractable
{
	private readonly Vector3 pos;

	private readonly StoryDirector director;

	public string EntityId => "slipper";

	public Vector3 Position => pos;

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (s.step == "S1_slipper")
			{
				return !s.flags.Contains("slipper_seen");
			}
			return false;
		}
		return false;
	}

	public SlipperInteractable(Vector3 p, StoryDirector d)
	{
		pos = p;
		director = d;
	}

	private static bool Active(GameState s)
	{
		if (s != null)
		{
			if (!(s.step == "S0_arrive") && !(s.step == "S1_slipper"))
			{
				if (s.step == "S2_ball")
				{
					return !s.flags.Contains("slipper_seen");
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public string Prompt(GameState s)
	{
		if (!Active(s))
		{
			return "";
		}
		return "E\u3000拿起端详拖鞋";
	}

	public bool CanInteract(GameState s)
	{
		return Active(s);
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.InspectSlipperAt(pos);
	}
}
