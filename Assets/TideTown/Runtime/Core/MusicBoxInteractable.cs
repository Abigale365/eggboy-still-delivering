using UnityEngine;

public sealed class MusicBoxInteractable : IInteractable
{
	private readonly SoundBottleSystem sys;

	public string EntityId => "music_box";

	public Vector3 Position => sys.MusicBoxPos;

	public MusicBoxInteractable(SoundBottleSystem s)
	{
		sys = s;
	}

	public string Prompt(GameState s)
	{
		if (sys.Playing)
		{
			return "";
		}
		if (sys.Count >= 4)
		{
			if (!sys.Done)
			{
				return "E\u3000把声音放进八音盒";
			}
			return "E\u3000再听一遍";
		}
		if (!sys.HasBottle)
		{
			return "";
		}
		if (sys.Count != 3)
		{
			return "把镇上的四段声音带来这里";
		}
		return "八音盒还等着最后一段声音";
	}

	public bool CanInteract(GameState s)
	{
		if (!sys.Playing)
		{
			return sys.Count >= 4;
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		sys.PlayMusicBox();
	}
}
