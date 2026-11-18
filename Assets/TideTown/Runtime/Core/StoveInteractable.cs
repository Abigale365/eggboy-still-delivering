using UnityEngine;

public sealed class StoveInteractable : IInteractable, IHoldInteraction, IObjectiveInteractable
{
	private StoryDirector director;

	private BakeryEffects effects;

	public Vector3 Position { get; }

	public string EntityId => "stove";

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (!(s.step == "B3_load"))
			{
				return s.step == "B4_light";
			}
			return true;
		}
		return false;
	}

	public float HoldSeconds(GameState state)
	{
		if (state == null || !state.flags.Contains("wood_loaded") || !state.flags.Contains("matches_taken") || state.flags.Contains("oven_lit"))
		{
			return 0f;
		}
		return 0.7f;
	}

	public string HoldEmote(GameState state)
	{
		return "hold_light_stove";
	}

	public StoveInteractable(Vector3 p, StoryDirector d, BakeryEffects fx)
	{
		Position = p;
		director = d;
		effects = fx;
	}

	public string Prompt(GameState s)
	{
		if (s == null)
		{
			return "";
		}
		if (s.carrying == "wood" && (!s.flags.Contains("wood_loaded") || s.flags.Contains("wood_wet")))
		{
			if (!s.flags.Contains("wood_wet"))
			{
				return "E\u3000把柴火放进炉膛";
			}
			return "E\u3000换上干柴";
		}
		if (s.flags.Contains("wood_loaded") && s.flags.Contains("matches_taken") && !s.flags.Contains("oven_lit"))
		{
			if (!s.flags.Contains("wood_wet") || !s.flags.Contains("wet_fizzled"))
			{
				return "按住 E\u3000擦亮火柴";
			}
			return "柴是湿的，点不着";
		}
		if (s.flags.Contains("oven_lit") && effects != null && effects.BellowsStage < 3 && director != null && !director.IsAuditMode)
		{
			return "E\u3000拉动风箱助燃（" + (effects.BellowsStage + 1) + "/3）";
		}
		if (director != null && !director.IsAuditMode && !s.flags.Contains("oven_lit"))
		{
			if (!s.flags.Contains("wood_loaded"))
			{
				return "炉膛空空如也，还缺一捆干柴";
			}
			if (!s.flags.Contains("matches_taken"))
			{
				return "干柴已塞入炉膛，还差火柴引火";
			}
		}
		return "";
	}

	public bool CanInteract(GameState s)
	{
		if (s == null)
		{
			return false;
		}
		if (s.carrying == "wood" && (!s.flags.Contains("wood_loaded") || s.flags.Contains("wood_wet")))
		{
			return true;
		}
		if (s.flags.Contains("wood_loaded") && s.flags.Contains("matches_taken") && !s.flags.Contains("oven_lit"))
		{
			return true;
		}
		if (s.flags.Contains("oven_lit") && effects != null && effects.BellowsStage < 3 && director != null && !director.IsAuditMode)
		{
			return true;
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		if (s.carrying == "wood" && (!s.flags.Contains("wood_loaded") || s.flags.Contains("wood_wet")))
		{
			director.LoadWoodAt(Position);
		}
		else if (s.flags.Contains("wood_loaded") && s.flags.Contains("matches_taken") && !s.flags.Contains("oven_lit"))
		{
			director.LightOven(effects);
		}
		else if (s.flags.Contains("oven_lit") && effects != null && effects.BellowsStage < 3)
		{
			director.BoostOvenBellows(effects);
		}
	}
}
