using System;
using UnityEngine;

public sealed class SoundSource : IInteractable
{
	private readonly SoundBottleSystem sys;

	private readonly string id;

	private readonly Func<Vector3> pos;

	private readonly Func<bool> available;

	public string EntityId => "sound_" + id;

	public Vector3 Position => pos();

	public SoundSource(SoundBottleSystem s, string soundId, Func<Vector3> p, Func<bool> avail)
	{
		sys = s;
		id = soundId;
		pos = p;
		available = avail;
	}

	public string Prompt(GameState s)
	{
		if (!sys.HasBottle || sys.Has(id) || !available())
		{
			return "";
		}
		return "E\u3000装下" + SoundBottleSystem.NameOf(id);
	}

	public bool CanInteract(GameState s)
	{
		if (sys.HasBottle && !sys.Has(id))
		{
			return available();
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		sys.Capture(id);
	}
}
