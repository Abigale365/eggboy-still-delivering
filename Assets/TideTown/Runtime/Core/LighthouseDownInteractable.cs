using UnityEngine;

public sealed class LighthouseDownInteractable : IInteractable, IObjectiveInteractable
{
	private readonly Vector3 basePos;

	private readonly StoryDirector director;

	public Vector3 Position
	{
		get
		{
			GameState gameState = ((director != null) ? director.State : null);
			if (gameState != null && (gameState.step == "L5_reply" || gameState.step == "E0_back"))
			{
				return basePos;
			}
			return basePos + new Vector3(-0.6f, 0f, -1.75f);
		}
	}

	public string EntityId => "lighthouse_down";

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (!(s.step == "L5_reply"))
			{
				return s.step == "E0_back";
			}
			return true;
		}
		return false;
	}

	public LighthouseDownInteractable(Vector3 p, StoryDirector d)
	{
		basePos = p;
		director = d;
	}

	public string Prompt(GameState s)
	{
		if (s.step == "L5_reply" || s.step == "E0_back")
		{
			return "E\u3000下塔回地面";
		}
		if (s.step == "L3_oil" || s.step == "L4_light" || s.step == "L1_shed" || s.step == "L2_door")
		{
			return "E\u3000顺旋梯暂回地面（或按 B 键）";
		}
		return "";
	}

	public bool CanInteract(GameState s)
	{
		if (!(s.step == "L5_reply") && !(s.step == "E0_back") && !(s.step == "L3_oil") && !(s.step == "L4_light") && !(s.step == "L1_shed"))
		{
			return s.step == "L2_door";
		}
		return true;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.DescendTower();
	}
}
