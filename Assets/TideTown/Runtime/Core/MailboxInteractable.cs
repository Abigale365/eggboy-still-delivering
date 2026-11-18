using UnityEngine;

public sealed class MailboxInteractable : IInteractable, IObjectiveInteractable
{
	private StoryDirector director;

	public Vector3 Position { get; }

	public string EntityId => "mailbox_03";

	public bool IsObjective(GameState s)
	{
		if (s != null)
		{
			return !s.flags.Contains("letters_taken");
		}
		return false;
	}

	public MailboxInteractable(Vector3 p, StoryDirector d)
	{
		Position = p;
		director = d;
	}

	public string Prompt(GameState s)
	{
		if (!s.flags.Contains("letters_taken"))
		{
			return "E\u3000取信";
		}
		return "";
	}

	public bool CanInteract(GameState s)
	{
		return !s.flags.Contains("letters_taken");
	}

	public void Interact(GameState s, InteractionService svc)
	{
		director.TakeLetters();
	}
}
