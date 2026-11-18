using UnityEngine;

public sealed class LighthouseDoorInteractable : IInteractable, IObjectiveInteractable
{
	private StoryDirector director;

	public Vector3 Position { get; }

	public string EntityId => "lighthouse_door";

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			return s.step == "L2_door";
		}
		return false;
	}

	public LighthouseDoorInteractable(Vector3 p, StoryDirector d)
	{
		Position = p;
		director = d;
	}

	public string Prompt(GameState s)
	{
		if (!(s.step == "L2_door") && !(s.step == "L3_oil") && !(s.step == "L4_light"))
		{
			return "";
		}
		return "E\u3000进灯塔，爬上去";
	}

	public bool CanInteract(GameState s)
	{
		if (!(s.step == "L2_door") && !(s.step == "L3_oil"))
		{
			return s.step == "L4_light";
		}
		return true;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.ClimbTower();
	}
}
