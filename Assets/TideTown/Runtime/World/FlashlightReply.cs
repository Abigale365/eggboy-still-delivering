using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class FlashlightReply : MonoBehaviour
{
	public Renderer[] dots = new Renderer[0];

	public float startDelay = 5f;

	public float stagger = 0.18f;

	private bool started;

	private Transform[] halos = new Transform[0];

	private Material haloMat;

	public bool Done { get; private set; }

	public int LitCount { get; private set; }

	public int Total
	{
		get
		{
			if (dots == null)
			{
				return 0;
			}
			return dots.Length;
		}
	}

	private void Awake()
	{
		if (dots == null)
		{
			return;
		}
		Renderer[] array = dots;
		foreach (Renderer renderer in array)
		{
			if (renderer != null)
			{
				renderer.enabled = false;
			}
		}
		Shader shader = Shader.Find("TideTown/SoftGlow");
		if (shader == null)
		{
			return;
		}
		haloMat = new Material(shader)
		{
			name = "ReplyHalo"
		};
		haloMat.SetColor("_Color", new Color(1f, 0.82f, 0.52f, 1f));
		haloMat.SetFloat("_Intensity", 1.5f);
		haloMat.SetFloat("_Mode", 0f);
		haloMat.SetFloat("_Billboard", 1f);
		haloMat.SetFloat("_Push", 0f);
		Mesh mesh = new Mesh
		{
			name = "ReplyHaloQuad"
		};
		mesh.vertices = new Vector3[4]
		{
			new Vector3(-0.5f, -0.5f, 0f),
			new Vector3(0.5f, -0.5f, 0f),
			new Vector3(0.5f, 0.5f, 0f),
			new Vector3(-0.5f, 0.5f, 0f)
		};
		mesh.uv = new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(1f, 1f),
			new Vector2(0f, 1f)
		};
		mesh.triangles = new int[6] { 0, 2, 1, 0, 3, 2 };
		mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
		halos = new Transform[dots.Length];
		for (int j = 0; j < dots.Length; j++)
		{
			if (!(dots[j] == null))
			{
				GameObject gameObject = new GameObject("reply_halo_" + j);
				gameObject.transform.SetParent(base.transform, worldPositionStays: false);
				gameObject.transform.position = dots[j].transform.position;
				gameObject.transform.localScale = Vector3.one * 1.8f;
				gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
				MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = haloMat;
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer.receiveShadows = false;
				gameObject.SetActive(value: false);
				halos[j] = gameObject.transform;
			}
		}
	}

	public void ResetForFilming()
	{
		StopAllCoroutines();
		started = (Done = false);
		LitCount = 0;
		if (dots == null) return;
		foreach (MeshRenderer dot in dots)
		{
			if (dot != null) dot.enabled = false;
		}
	}

	public void Begin()
	{
		if (!started && !Done)
		{
			started = true;
			StartCoroutine(Run());
		}
	}

	public void RevealUpTo(int count)
	{
		if (dots == null)
		{
			return;
		}
		int num = Mathf.Clamp(count, 0, Total);
		for (int i = 0; i < num; i++)
		{
			if (dots[i] != null)
			{
				dots[i].enabled = true;
			}
		}
		if (num > LitCount)
		{
			LitCount = num;
		}
		if (LitCount >= Total && Total > 0)
		{
			Done = true;
		}
	}

	public void SetDoneInstant()
	{
		Done = true;
		LitCount = Total;
		if (dots == null)
		{
			return;
		}
		Renderer[] array = dots;
		foreach (Renderer renderer in array)
		{
			if (renderer != null)
			{
				renderer.enabled = true;
			}
		}
	}

	private IEnumerator Run()
	{
		yield return new WaitForSeconds(startDelay);
		for (int i = 0; i < Total; i++)
		{
			if (dots[i] != null)
			{
				dots[i].enabled = true;
			}
			LitCount = i + 1;
			yield return new WaitForSeconds(stagger);
		}
		Done = true;
	}

	private void Update()
	{
		if (LitCount <= 0 || dots == null)
		{
			return;
		}
		for (int i = 0; i < dots.Length; i++)
		{
			if (dots[i] == null || !dots[i].enabled)
			{
				continue;
			}
			float num = 0.82f + 0.18f * Mathf.Sin(Time.time * 2.4f + (float)i * 1.7f);
			dots[i].transform.localScale = Vector3.one * (0.3f * num);
			if (i < halos.Length && halos[i] != null)
			{
				if (!halos[i].gameObject.activeSelf)
				{
					halos[i].gameObject.SetActive(value: true);
				}
				float num2 = 0.75f + 0.25f * Mathf.Sin(Time.time * 3.1f + (float)i * 2.3f) * Mathf.Sin(Time.time * 1.3f + (float)i);
				halos[i].localScale = Vector3.one * (3.4f * num2);
			}
		}
	}
}
