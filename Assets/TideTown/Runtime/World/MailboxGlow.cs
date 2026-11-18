using UnityEngine;
using UnityEngine.Rendering;

public sealed class MailboxGlow : MonoBehaviour
{
	public float Height = 1.6f;

	public float OrbSize = 0.22f;

	public float LightIntensity = 1.3f;

	public bool UseLight = true;

	private static readonly Color Warm = new Color(1f, 0.78f, 0.36f, 1f);

	private static readonly Color Paper = new Color(0.99f, 0.95f, 0.86f, 1f);

	private static readonly Color PaperFold = new Color(0.9f, 0.83f, 0.7f, 1f);

	private static readonly Color Seal = new Color(0.72f, 0.2f, 0.15f, 1f);

	private Light lamp;

	private Transform orb;

	private Transform envelope;

	private Material haloMat;

	private bool mailboxMode;

	private float t;

	private float fade = 1f;

	private float nextStateCheck;

	private GameBootstrap boot;

	private Vector3 envelopeLocal;

	private float target = 1f;

	private void Start()
	{
		mailboxMode = base.name.StartsWith("S1_Mailbox");
		Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
		if (shader == null)
		{
			shader = Shader.Find("Unlit/Color");
		}
		if (shader == null)
		{
			shader = Shader.Find("Sprites/Default");
		}
		if (mailboxMode)
		{
			BuildEnvelope(shader);
		}
		else if (shader != null)
		{
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			gameObject.name = "MailboxGlowOrb";
			Object.Destroy(gameObject.GetComponent<Collider>());
			gameObject.transform.SetParent(base.transform, worldPositionStays: false);
			gameObject.transform.localPosition = new Vector3(0f, Height + 0.25f, 0f);
			gameObject.transform.localScale = Vector3.one * OrbSize;
			gameObject.GetComponent<Renderer>().sharedMaterial = UnlitMat(shader, "MailboxGlowOrb", Warm);
			orb = gameObject.transform;
		}
		GameObject gameObject2 = new GameObject("MailboxGlowLight");
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject2.transform.localPosition = new Vector3(0f, Height + 0.1f, 0f);
		if (UseLight)
		{
			lamp = gameObject2.AddComponent<Light>();
			lamp.type = LightType.Point;
			lamp.color = new Color(1f, 0.76f, 0.4f);
			lamp.range = 3.2f;
			lamp.intensity = LightIntensity;
			lamp.shadows = LightShadows.None;
		}
	}

	private static Material UnlitMat(Shader s, string name, Color c)
	{
		Material material = new Material(s)
		{
			name = name
		};
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", c);
		}
		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", c);
		}
		return material;
	}

	private void BuildEnvelope(Shader unlit)
	{
		float num = Mathf.Max(0.0001f, base.transform.lossyScale.y);
		Vector3 vector = base.transform.position + Vector3.up * (Height + 0.32f);
		Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>();
		if (componentsInChildren.Length != 0)
		{
			Bounds bounds = componentsInChildren[0].bounds;
			Renderer[] array = componentsInChildren;
			foreach (Renderer renderer in array)
			{
				bounds.Encapsulate(renderer.bounds);
			}
			vector = new Vector3(bounds.center.x, bounds.max.y + 0.32f, bounds.center.z);
		}
		envelopeLocal = base.transform.InverseTransformPoint(vector);
		Debug.Log($"MAILBOX_ENVELOPE pivot={base.transform.position} above={vector} scale={num:F3} rends={componentsInChildren.Length}");
		Transform transform = new GameObject("MailboxEnvelope").transform;
		transform.SetParent(base.transform, worldPositionStays: false);
		transform.localPosition = envelopeLocal;
		transform.localScale = Vector3.one * (1.25f / num);
		envelope = transform;
		Shader shader = Shader.Find("TideTown/SoftGlow");
		if (shader != null)
		{
			GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
			obj.name = "EnvelopeHalo";
			Object.Destroy(obj.GetComponent<Collider>());
			obj.transform.SetParent(base.transform, worldPositionStays: false);
			obj.transform.localPosition = transform.localPosition;
			obj.transform.localScale = Vector3.one * (1.15f / num);
			haloMat = new Material(shader)
			{
				name = "EnvelopeHalo"
			};
			haloMat.SetColor("_Color", new Color(1f, 0.8f, 0.5f, 1f));
			haloMat.SetFloat("_Intensity", 0.55f);
			haloMat.SetFloat("_Mode", 0f);
			haloMat.SetFloat("_Billboard", 1f);
			haloMat.SetFloat("_Push", 0.12f);
			Renderer component = obj.GetComponent<Renderer>();
			component.sharedMaterial = haloMat;
			component.shadowCastingMode = ShadowCastingMode.Off;
		}
		if (!(unlit == null))
		{
			GameObject obj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			obj2.name = "EnvelopeBody";
			Object.Destroy(obj2.GetComponent<Collider>());
			obj2.transform.SetParent(transform, worldPositionStays: false);
			obj2.transform.localScale = new Vector3(0.3f, 0.2f, 0.012f);
			obj2.GetComponent<Renderer>().sharedMaterial = UnlitMat(unlit, "EnvelopePaper", Paper);
			Material sharedMaterial = UnlitMat(unlit, "EnvelopeFold", PaperFold);
			Material sharedMaterial2 = UnlitMat(unlit, "EnvelopeSeal", Seal);
			float[] array2 = new float[2] { -1f, 1f };
			foreach (float num2 in array2)
			{
				GameObject gameObject = new GameObject("EnvelopeFlap");
				gameObject.transform.SetParent(transform, worldPositionStays: false);
				gameObject.transform.localPosition = new Vector3(0f, 0f, num2 * 0.0065f);
				Mesh mesh = new Mesh
				{
					name = "EnvelopeFlap"
				};
				mesh.vertices = new Vector3[3]
				{
					new Vector3(-0.15f, 0.1f, 0f),
					new Vector3(0.15f, 0.1f, 0f),
					new Vector3(0f, -0.015f, 0f)
				};
				mesh.triangles = new int[6] { 0, 1, 2, 0, 2, 1 };
				mesh.RecalculateBounds();
				gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
				MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = sharedMaterial;
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				GameObject obj3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				obj3.name = "EnvelopeSeal";
				Object.Destroy(obj3.GetComponent<Collider>());
				obj3.transform.SetParent(transform, worldPositionStays: false);
				obj3.transform.localPosition = new Vector3(0f, -0.005f, num2 * 0.009f);
				obj3.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
				obj3.transform.localScale = new Vector3(0.045f, 0.003f, 0.045f);
				obj3.GetComponent<Renderer>().sharedMaterial = sharedMaterial2;
			}
			Renderer[] array = transform.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].shadowCastingMode = ShadowCastingMode.Off;
			}
		}
	}

	private bool LettersTaken()
	{
		if (boot == null)
		{
			boot = Object.FindObjectOfType<GameBootstrap>();
		}
		if (boot != null && boot.state != null)
		{
			return boot.state.flags.Contains("letters_taken");
		}
		return false;
	}

	private void Update()
	{
		float deltaTime = Time.deltaTime;
		t += deltaTime;
		float num = 0.5f + 0.5f * Mathf.Sin(t * 2.1f);
		if (!mailboxMode)
		{
			if (lamp != null)
			{
				lamp.intensity = (1f + num * 0.7f) * LightIntensity / 1.3f;
			}
			if (orb != null)
			{
				orb.localScale = Vector3.one * OrbSize * (0.82f + num * 0.28f);
			}
			return;
		}
		if (Time.unscaledTime >= nextStateCheck)
		{
			nextStateCheck = Time.unscaledTime + 0.5f;
			target = (LettersTaken() ? 0f : 1f);
		}
		fade = Mathf.MoveTowards(fade, target, deltaTime / 0.8f);
		float num2 = Mathf.SmoothStep(0f, 1f, fade);
		if (envelope != null)
		{
			float num3 = Mathf.Max(0.0001f, base.transform.lossyScale.y);
			float num4 = Mathf.Sin(t * 1.6f) * 0.06f;
			envelope.localPosition = envelopeLocal + Vector3.up * (num4 / num3);
			envelope.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 6f, t * 40f, Mathf.Sin(t * 0.8f) * 4f);
			envelope.localScale = Vector3.one * (1.25f * num2 / num3);
			bool flag = num2 > 0.01f;
			if (envelope.gameObject.activeSelf != flag)
			{
				envelope.gameObject.SetActive(flag);
			}
		}
		if (haloMat != null)
		{
			haloMat.SetFloat("_Intensity", Mathf.Lerp(0.12f, 0.45f + num * 0.2f, num2));
		}
		if (lamp != null)
		{
			lamp.intensity = Mathf.Lerp(0.35f, 1f + num * 0.7f, num2) * LightIntensity / 1.3f;
		}
	}
}
