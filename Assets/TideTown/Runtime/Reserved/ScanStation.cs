using UnityEngine;

public sealed class ScanStation : MonoBehaviour, IInteractable
{
	private readonly IScanSource source = new NullScanSource();

	private string message = "";

	private float messageUntil;

	public string EntityId => "scan_station";

	public Vector3 Position => base.transform.position;

	private void Awake()
	{
		SetEnabled(Features.scan);
	}

	public void SetEnabled(bool on)
	{
		Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = on;
		}
		Collider[] componentsInChildren2 = GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].enabled = on;
		}
	}

	public string Prompt(GameState s)
	{
		if (!(Time.time < messageUntil))
		{
			return "E\u3000扫描";
		}
		return message;
	}

	public bool CanInteract(GameState s)
	{
		return Features.scan;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		source.Capture(delegate
		{
			Show("样例已就绪");
		}, delegate(string err)
		{
			Show(err);
		});
	}

	private void Show(string text)
	{
		message = text;
		messageUntil = Time.time + 6f;
		Debug.Log("SCAN " + text);
	}
}
