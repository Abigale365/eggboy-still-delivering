using UnityEngine;

public sealed class LampHeadInteractable : IInteractable, IHoldInteraction, IObjectiveInteractable
{
	private StoryDirector director;

	public Vector3 Position { get; }

	public string EntityId => "lamp_head";

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (!(s.step == "L3_oil"))
			{
				return s.step == "L4_light";
			}
			return true;
		}
		return false;
	}

	public float HoldSeconds(GameState state)
	{
		if (!(state.step == "L4_light"))
		{
			return 0f;
		}
		return 1f;
	}

	public string HoldEmote(GameState state)
	{
		return "hold_pour_oil";
	}

	public LampHeadInteractable(Vector3 p, StoryDirector d)
	{
		Position = p;
		director = d;
	}

	public string Prompt(GameState s)
	{
		if (s.step == "L3_oil" && !s.flags.Contains("lamp_oiled"))
		{
			return "E\u3000给灯头加油";
		}
		if (s.step == "L4_light" && !s.flags.Contains("beam_lit"))
		{
			return "按住 E\u3000转动灯头";
		}
		return "";
	}

	public bool CanInteract(GameState s)
	{
		if (!(s.step == "L3_oil") || s.flags.Contains("lamp_oiled"))
		{
			if (s.step == "L4_light")
			{
				return !s.flags.Contains("beam_lit");
			}
			return false;
		}
		return true;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		if (s.step == "L3_oil")
		{
			director.OilLampAt(Position);
		}
		else
		{
			director.LightLamp();
		}
	}
}
