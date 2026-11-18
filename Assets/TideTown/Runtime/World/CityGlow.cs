using UnityEngine;

public sealed class CityGlow : MonoBehaviour
{
	public Renderer[] windows = new Renderer[0];

	public Transform signal;

	public float swing = 1.1f;

	private Vector3 signalBase;

	public bool Shown { get; private set; }

	private void Awake()
	{
		if (windows != null)
		{
			Renderer[] array = windows;
			foreach (Renderer renderer in array)
			{
				if (renderer != null)
				{
					renderer.enabled = false;
				}
			}
		}
		if (signal != null)
		{
			signalBase = signal.localPosition;
			signal.gameObject.SetActive(value: false);
		}
	}

	public void Show()
	{
		if (Shown)
		{
			return;
		}
		Shown = true;
		GameObject gameObject = Resources.Load<GameObject>("Ending/NewPostOffice");
		if (gameObject != null)
		{
			GameObject gameObject2 = Object.Instantiate(gameObject, base.transform);
			gameObject2.name = "NewPostOffice";
			gameObject2.transform.localPosition = Vector3.down * 2f;
			Vector3 forward = Vector3.ProjectOnPlane(-base.transform.position, Vector3.up);
			if (forward.sqrMagnitude > 0.01f)
			{
				gameObject2.transform.rotation = Quaternion.LookRotation(forward);
			}
			gameObject2.transform.localScale *= 2f;
			Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			GameObject obj = new GameObject("NewPostOfficeWelcomeLight");
			obj.transform.SetParent(base.transform, worldPositionStays: false);
			obj.transform.localPosition = Vector3.up * 3f + forward.normalized * 5f;
			Light light = obj.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = new Color32(byte.MaxValue, 206, 147, byte.MaxValue);
			light.range = 18f;
			light.intensity = 2.1f;
		}
		if (windows != null)
		{
			Renderer[] array = windows;
			foreach (Renderer renderer in array)
			{
				if (renderer != null)
				{
					renderer.enabled = true;
				}
			}
		}
		if (signal != null)
		{
			signal.gameObject.SetActive(value: true);
		}
	}

	private void Update()
	{
		if (Shown && !(signal == null))
		{
			signal.localPosition = signalBase + new Vector3(Mathf.Sin(Time.time * 1.8f) * swing, 0f, 0f);
		}
	}
}
