using UnityEngine;

public sealed class WinchInteractable : IInteractable
{
	private readonly WorldMechanismDirector mech;

	public string EntityId => "dock_winch";

	public Vector3 Position => mech.WinchPosition;

	public WinchInteractable(WorldMechanismDirector m)
	{
		mech = m;
	}

	public string Prompt(GameState s)
	{
		if (!mech.GapActive || mech.CrossingSolved)
		{
			return "";
		}
		if (mech.WinchHolders >= 2)
		{
			return "一起摇——";
		}
		if (!mech.WinchStrained)
		{
			TidalEchoCloneSystem ec = Object.FindObjectOfType<TidalEchoCloneSystem>();
			if (ec != null && ec.EchoActive) return "按住 E\u3000与回声分身一起摇起浮桥";
			return "按 R 唤出回声分身，再按住 E 一起摇绞盘";
		}
		return "一个人摇不动（先按 R 叫来回声分身，再按住 E）";
	}

	public bool CanInteract(GameState s)
	{
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
	}
}
