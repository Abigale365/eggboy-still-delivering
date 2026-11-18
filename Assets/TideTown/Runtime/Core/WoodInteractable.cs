using UnityEngine;

public sealed class WoodInteractable : IInteractable, IObjectiveInteractable
{
	private StoryDirector director;

	public Vector3 Position { get; }

	public string EntityId => "wood";

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (s.step == "B2_wood" || s.flags.Contains("wood_wet"))
			{
				return s.carrying != "wood";
			}
			return false;
		}
		return false;
	}

	public WoodInteractable(Vector3 p, StoryDirector d)
	{
		Position = p;
		director = d;
	}

	private static bool CanPickWood(GameState s)
	{
		if (s != null && !s.flags.Contains("oven_lit") && s.carrying != "wood")
		{
			if (!s.flags.Contains("wood_wet"))
			{
				if (s.step == "P1_mailbox" || s.step == "B0_arrive" || s.step == "B1_matches" || s.step == "B2_wood" || s.step == "B3_load")
				{
					return !s.flags.Contains("wood_loaded");
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public string Prompt(GameState s)
	{
		if (!CanPickWood(s))
		{
			return "";
		}
		return "E\u3000抱起柴火捆";
	}

	public bool CanInteract(GameState s)
	{
		return CanPickWood(s);
	}

	public void Interact(GameState s, InteractionService svc)
	{
		if (s != null) { s.flags.Remove("carrying_wet_wood"); s.flags.Remove("wood_wet"); }
		director.TakeWoodAt(Position);
	}
}
