using UnityEngine;

public sealed class BowlInteractable : IInteractable, IObjectiveInteractable
{
	private readonly Vector3 pos;

	private readonly StoryDirector director;

	public string EntityId => "bowl";

	public Vector3 Position => pos;

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			if (s.step == "S3_bowl")
			{
				return s.carrying == "ball";
			}
			return false;
		}
		return false;
	}

	public BowlInteractable(Vector3 p, StoryDirector d)
	{
		pos = p;
		director = d;
	}

	public string Prompt(GameState s)
	{
		if (s == null)
		{
			return "";
		}
		if (s.carrying == "ball" && !s.flags.Contains("ball_in_bowl"))
		{
			return "E\u3000把红球滚向狗碗";
		}
		if (director != null && !director.IsAuditMode && (s.step == "S0_arrive" || s.step == "S1_slipper" || s.step == "S2_ball" || s.step == "S3_bowl") && !s.flags.Contains("ball_in_bowl"))
		{
			return "E\u3000查看小黑的空食碗";
		}
		return "";
	}

	public bool CanInteract(GameState s)
	{
		if (s == null || s.flags.Contains("ball_in_bowl"))
		{
			return false;
		}
		if (s.carrying == "ball")
		{
			return true;
		}
		if (director != null && !director.IsAuditMode)
		{
			if (!(s.step == "S0_arrive") && !(s.step == "S1_slipper") && !(s.step == "S2_ball"))
			{
				return s.step == "S3_bowl";
			}
			return true;
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		if (s != null && s.carrying == "ball")
		{
			director.PutBallInBowl();
		}
		else if (director != null && !director.IsAuditMode)
		{
			EggboyUI eggboyUI = Object.FindObjectOfType<EggboyUI>();
			if (eggboyUI != null)
			{
				eggboyUI.ShowToast("空食碗旁压着一张纸条：『小黑想吃顿好的，或者有个新玩具陪它玩。』", 4.2f);
			}
			AIAgentCalibrationDirector.ToggleConsole();
		}
	}
}
