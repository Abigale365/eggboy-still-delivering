using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class EggboyWorld : MonoBehaviour, IEggboyWorld
{
	private const float Foot = 0.12f;

	private const float RoadLen = 70f;

	private const float ShoreZ = 7f;

	private const float PierX0 = 11f;

	private const float PierX1 = 13.4f;

	private const float PierZ0 = 5f;

	private const float PierZ1 = 17f;

	public readonly List<EggboyCollider> colliders = new List<EggboyCollider>();

	public readonly Vector3 mailboxPos = new Vector3(-24f, 0f, -2.3f);

	private Material baseMat;

	private Light sun;

	private Transform seaFoam;

	private readonly List<Transform> foams = new List<Transform>();

	public IReadOnlyList<EggboyCollider> Colliders => colliders;

	public Vector3 MailboxPos => mailboxPos;

	public Vector3 SpawnPos => new Vector3(-21f, 0f, 0f);

	public void QueryColliders(float x, float z, List<EggboyCollider> results)
	{
		results.Clear();
		results.AddRange(colliders);
	}

	private void Awake()
	{
		Sky();
		Ground();
		Fence();
		Bushes();
		Mailbox();
		Houses();
		Signs();
		Lamps();
		Crates();
		Pier();
		JumpSpots();
	}

	private static Shader WorldShader()
	{
		if (GraphicsSettings.currentRenderPipeline != null)
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader != null)
			{
				return shader;
			}
		}
		return Shader.Find("Standard");
	}

	private Material Mat(uint rgb, float smoothness = 0.15f)
	{
		Material material = new Material(WorldShader());
		Color32 color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, byte.MaxValue);
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", color);
		}
		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", color);
		}
		if (material.HasProperty("_Smoothness"))
		{
			material.SetFloat("_Smoothness", smoothness);
		}
		if (material.HasProperty("_Glossiness"))
		{
			material.SetFloat("_Glossiness", smoothness);
		}
		if (material.HasProperty("_Metallic"))
		{
			material.SetFloat("_Metallic", 0f);
		}
		return material;
	}

	private Material Emissive(uint rgb, float intensity = 0.7f)
	{
		Material material = Mat(rgb, 0.6f);
		material.EnableKeyword("_EMISSION");
		Color32 color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, byte.MaxValue);
		material.SetColor("_EmissionColor", new Color((float)(int)color.r / 255f, (float)(int)color.g / 255f, (float)(int)color.b / 255f) * intensity);
		return material;
	}

	private void Fence()
	{
		Material mat = Mat(9071173u, 0.1f);
		for (float num = -34f; num <= 34f; num += 2.2f)
		{
			Prim(PrimitiveType.Cube, mat, new Vector3(num, 0.45f, 6.55f), new Vector3(0.12f, 0.9f, 0.12f));
			Prim(PrimitiveType.Cube, mat, new Vector3(num + 1.1f, 0.52f, 6.55f), new Vector3(2.2f, 0.08f, 0.08f));
			Prim(PrimitiveType.Cube, mat, new Vector3(num + 1.1f, 0.8f, 6.55f), new Vector3(2.2f, 0.08f, 0.08f));
		}
	}

	private void Bushes()
	{
		uint[] array = new uint[4] { 7311194u, 8364126u, 9416298u, 6258512u };
		float[,] array2 = new float[8, 3]
		{
			{ -19f, -4.6f, 0.7f },
			{ -15f, -8.6f, 0.9f },
			{ -8f, -4.7f, 0.6f },
			{ 2f, -4.6f, 0.8f },
			{ 9f, -8.5f, 0.9f },
			{ 21f, -4.5f, 0.7f },
			{ -28f, 4.6f, 0.6f },
			{ 24f, 5f, 0.8f }
		};
		for (int i = 0; i < array2.GetLength(0); i++)
		{
			float x = array2[i, 0];
			float z = array2[i, 1];
			float num = array2[i, 2];
			Prim(PrimitiveType.Sphere, Mat(array[i % array.Length], 0.05f), new Vector3(x, num * 0.5f, z), new Vector3(num * 1.6f, num, num * 1.4f));
		}
	}

	private void Signs()
	{
		Material mat = Mat(5917242u, 0.1f);
		Material mat2 = Mat(15852488u, 0.25f);
		Material mat3 = Mat(11818554u, 0.3f);
		Prim(PrimitiveType.Cube, mat, new Vector3(-22f, 0.8f, -3f), new Vector3(0.1f, 1.6f, 0.1f));
		Prim(PrimitiveType.Cube, mat2, new Vector3(-22f, 1.55f, -3f), new Vector3(0.9f, 0.5f, 0.06f));
		Prim(PrimitiveType.Cube, mat3, new Vector3(-22f, 1.32f, -3.05f), new Vector3(0.9f, 0.06f, 0.04f));
		Prim(PrimitiveType.Cube, mat, new Vector3(10f, 0.9f, -2.8f), new Vector3(0.1f, 1.8f, 0.1f));
		Prim(PrimitiveType.Cube, mat2, new Vector3(10f, 1.65f, -2.8f), new Vector3(1.1f, 0.34f, 0.06f));
		Prim(PrimitiveType.Cube, mat3, new Vector3(10.28f, 1.65f, -2.86f), new Vector3(0.36f, 0.05f, 0.04f));
		Prim(PrimitiveType.Cube, mat3, new Vector3(18f, 1.75f, -4.75f), new Vector3(1.4f, 0.08f, 0.7f)).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
	}

	private GameObject Prim(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, Transform parent = null, bool keepCollider = false)
	{
		GameObject gameObject = GameObject.CreatePrimitive(type);
		if (baseMat == null)
		{
			baseMat = gameObject.GetComponent<Renderer>().sharedMaterial;
		}
		if (!keepCollider)
		{
			Object.Destroy(gameObject.GetComponent<Collider>());
		}
		if (mat != null)
		{
			gameObject.GetComponent<Renderer>().sharedMaterial = mat;
		}
		gameObject.transform.SetParent((parent == null) ? base.transform : parent, worldPositionStays: false);
		gameObject.transform.localPosition = pos;
		gameObject.transform.localScale = scale;
		return gameObject;
	}

	private void Sky()
	{
		Prim(PrimitiveType.Cube, null, Vector3.down * 100f, Vector3.one);
		sun = EggboyEnv.Apply(base.transform);
	}

	private void Ground()
	{
		Prim(PrimitiveType.Cube, Mat(8364126u), new Vector3(0f, -0.05f, -57f), new Vector3(220f, 0.1f, 120f));
		Prim(PrimitiveType.Cube, Mat(14271645u), new Vector3(0f, -0.06f, 4f), new Vector3(220f, 0.1f, 14f));
		Prim(PrimitiveType.Cube, Mat(12823947u, 0.05f), new Vector3(0f, -0.04f, 0f), new Vector3(70f, 0.1f, 3f));
		float[] array = new float[2] { -1.55f, 1.55f };
		foreach (float z in array)
		{
			Prim(PrimitiveType.Cube, Mat(10258024u), new Vector3(0f, 0.04f, z), new Vector3(70f, 0.08f, 0.12f));
		}
		Prim(PrimitiveType.Cube, Mat(5215413u, 0.75f), new Vector3(0f, -0.1f, 67f), new Vector3(240f, 0.1f, 120f));
		seaFoam = Prim(PrimitiveType.Cube, Mat(16054520u, 0.3f), new Vector3(0f, -0.03f, 7.3f), new Vector3(240f, 0.02f, 0.6f)).transform;
		for (int j = 0; j < 3; j++)
		{
			foams.Add(Prim(PrimitiveType.Cube, Mat(15267060u, 0.2f), new Vector3(0f, -0.035f, 7.9f + (float)j * 0.9f), new Vector3(240f, 0.015f, 0.22f + (float)j * 0.12f)).transform);
		}
		Vector3[] array2 = new Vector3[4]
		{
			new Vector3(-50f, 30f, 10f),
			new Vector3(-15f, 26f, 13f),
			new Vector3(22f, 34f, 9f),
			new Vector3(55f, 28f, 12f)
		};
		for (int i = 0; i < array2.Length; i++)
		{
			Vector3 vector = array2[i];
			Prim(PrimitiveType.Sphere, Mat(9416326u), new Vector3(vector.x, -1f, -48f), new Vector3(vector.y * 2f, vector.z * 2f, 28f));
		}
	}

	private void Mailbox()
	{
		Transform transform = new GameObject("Mailbox").transform;
		transform.SetParent(base.transform, worldPositionStays: false);
		transform.localPosition = mailboxPos;
		Material mat = Mat(13124154u, 0.4f);
		Prim(PrimitiveType.Cylinder, mat, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), transform);
		Prim(PrimitiveType.Sphere, mat, new Vector3(0f, 1f, 0f), Vector3.one * 0.48f, transform);
		Prim(PrimitiveType.Cube, Mat(2827040u), new Vector3(0f, 0.82f, 0.24f), new Vector3(0.22f, 0.04f, 0.05f), transform);
		colliders.Add(new EggboyCollider
		{
			x = mailboxPos.x,
			z = mailboxPos.z,
			r = 0.4f
		});
	}

	private void Houses()
	{
		uint[] array = new uint[4] { 15852488u, 15323054u, 14673624u, 15783620u };
		uint[] array2 = new uint[4] { 11818554u, 9067068u, 7307151u, 11035967u };
		float[] array3 = new float[4] { -17f, -6f, 5f, 18f };
		for (int i = 0; i < array3.Length; i++)
		{
			Transform transform = new GameObject("House" + i).transform;
			transform.SetParent(base.transform, worldPositionStays: false);
			transform.localPosition = new Vector3(array3[i], 0f, -6.5f);
			Prim(PrimitiveType.Cube, Mat(array[i]), new Vector3(0f, 1.4f, 0f), new Vector3(4.2f, 2.8f, 3.2f), transform, keepCollider: true);
			Prim(PrimitiveType.Cube, Mat(array2[i]), new Vector3(0f, 3.1f, 0f), new Vector3(4.6f, 2.3040001f, 2.3040001f), transform).transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
			Prim(PrimitiveType.Cube, Mat(6126938u), new Vector3(0f, 0.8f, 1.62f), new Vector3(0.9f, 1.6f, 0.06f), transform);
			float[] array4 = new float[2] { -1.3f, 1.3f };
			foreach (float x in array4)
			{
				Prim(PrimitiveType.Cube, Emissive(16765578u, 0.55f), new Vector3(x, 1.7f, 1.62f), new Vector3(0.8f, 0.7f, 0.05f), transform);
			}
			colliders.Add(new EggboyCollider
			{
				x = array3[i],
				z = -6.5f,
				hw = 2.35f,
				hd = 1.85f
			});
		}
	}

	private void Lamps()
	{
		Material mat = Mat(3814706u);
		Material mat2 = Emissive(16773577u, 2f);
		for (int i = -30; i <= 30; i += 10)
		{
			Prim(PrimitiveType.Cylinder, mat, new Vector3(i, 1.3f, -2.1f), new Vector3(0.12f, 1.3f, 0.12f));
			Prim(PrimitiveType.Sphere, mat2, new Vector3(i, 2.7f, -2.1f), Vector3.one * 0.32f);
			colliders.Add(new EggboyCollider
			{
				x = i,
				z = -2.1f,
				r = 0.2f
			});
		}
	}

	private void Crates()
	{
		Material mat = Mat(11106635u);
		float[,] array = new float[4, 4]
		{
			{ -3f, 2.3f, 0.55f, 0.3f },
			{ -2.4f, 2.6f, 0.45f, -0.2f },
			{ 9f, 3.2f, 0.6f, 0.1f },
			{ 14.5f, 4.2f, 0.5f, 0.6f }
		};
		for (int i = 0; i < array.GetLength(0); i++)
		{
			float x = array[i, 0];
			float z = array[i, 1];
			float num = array[i, 2];
			Prim(PrimitiveType.Cube, mat, new Vector3(x, num / 2f, z), Vector3.one * num).transform.localRotation = Quaternion.Euler(0f, array[i, 3] * 57.29578f, 0f);
			colliders.Add(new EggboyCollider
			{
				x = x,
				z = z,
				r = num * 0.7f
			});
		}
	}

	private void Pier()
	{
		Material mat = Mat(11569758u);
		for (float num = 5f; num < 17f; num += 0.5f)
		{
			Prim(PrimitiveType.Cube, mat, new Vector3(12.2f, 0.08f, num + 0.25f), new Vector3(2.3999996f, 0.1f, 0.42f));
		}
	}

	private void JumpSpots()
	{
		Material mat = Mat(11569758u);
		float[,] array = new float[2, 3]
		{
			{ -13.2f, 3.3f, 0.18f },
			{ -12.1f, 3.3f, 0.34f }
		};
		for (int i = 0; i < 2; i++)
		{
			float x = array[i, 0];
			float z = array[i, 1];
			float num = array[i, 2];
			Prim(PrimitiveType.Cube, mat, new Vector3(x, num / 2f, z), new Vector3(1.1f, num, 1.1f));
			colliders.Add(new EggboyCollider
			{
				x = x,
				z = z,
				hw = 0.55f,
				hd = 0.55f,
				top = num
			});
		}
		Prim(PrimitiveType.Cylinder, Mat(7317958u, 0.85f), new Vector3(-9.6f, 0.012f, 3.4f), new Vector3(0.64f, 0.01f, 0.64f));
		colliders.Add(new EggboyCollider
		{
			x = -9.6f,
			z = 3.4f,
			r = 0.32f,
			water = true
		});
	}

	public bool Walkable(float x, float z)
	{
		if (!(Mathf.Abs(x) < 34f) || !(z > -11f) || !(z < 6.8f))
		{
			if (x > 11.2f && x < 13.2f && z > 4.5f)
			{
				return z < 16.7f;
			}
			return false;
		}
		return true;
	}

	public float GroundAt(float x, float z, float aboveY)
	{
		return GroundAt(x, z);
	}

	public float GroundAt(float x, float z)
	{
		float num = ((x > 11f && x < 13.4f && z > 5f && z < 17f) ? 0.13f : 0f);
		foreach (EggboyCollider collider in colliders)
		{
			if (!collider.top.HasValue)
			{
				continue;
			}
			bool num2;
			if (!collider.r.HasValue)
			{
				if (!(Mathf.Abs(x - collider.x) < collider.hw.GetValueOrDefault() + 0.12f))
				{
					continue;
				}
				num2 = Mathf.Abs(z - collider.z) < collider.hd.GetValueOrDefault() + 0.12f;
			}
			else
			{
				num2 = Mathf.Sqrt((x - collider.x) * (x - collider.x) + (z - collider.z) * (z - collider.z)) < collider.r.Value + 0.12f;
			}
			if (num2)
			{
				num = Mathf.Max(num, collider.top.Value);
			}
		}
		return num;
	}

	private void Update()
	{
		Vector3 localPosition = seaFoam.localPosition;
		localPosition.z = 7.3f + Mathf.Sin(Time.time * 1.3f) * 0.25f;
		seaFoam.localPosition = localPosition;
		for (int i = 0; i < foams.Count; i++)
		{
			Vector3 localPosition2 = foams[i].localPosition;
			localPosition2.z = 7.9f + (float)i * 0.9f + Mathf.Sin(Time.time * (0.8f + (float)i * 0.25f) + (float)i * 1.7f) * 0.35f;
			foams[i].localPosition = localPosition2;
		}
	}
}
