using System;
using System.Collections;
using UnityEngine;

public sealed class DogPawPlate : MonoBehaviour
{
	private GameState state;

	private DogFollower dog;

	private EggboyUI ui;

	private IslandWorld world;

	private Transform plateTop;

	private Transform chest;

	private bool rising;

	private Vector3 seaDirection;

	public static DogPawPlate Instance { get; private set; }

	public Vector3 Center { get; private set; }

	public Vector3 ChestRest { get; private set; }

	public bool Done
	{
		get
		{
			if (state != null)
			{
				return state.flags.Contains("paw_plate_done");
			}
			return false;
		}
	}

	public bool ChestOpened
	{
		get
		{
			if (state != null)
			{
				return state.flags.Contains("paw_chest_opened");
			}
			return false;
		}
	}

	public bool Available
	{
		get
		{
			if (dog != null && dog.Following)
			{
				return dog.Visible;
			}
			return false;
		}
	}

	public bool ChestReady
	{
		get
		{
			if (Done && !rising && chest != null)
			{
				return chest.gameObject.activeSelf;
			}
			return false;
		}
	}

	public void Init(IslandWorld w, GameState s, EggboyUI u)
	{
		Instance = this;
		world = w;
		state = s;
		ui = u;
		dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		Place();
		BuildPlate();
		BuildChest();
		InteractionService interactionService = UnityEngine.Object.FindObjectOfType<InteractionService>();
		if (interactionService != null)
		{
			interactionService.Register(new PawChestInteractable(this));
		}
	}

	private void Place()
	{
		if (!DogFollower.StayNearSchool || !(dog != null) || !PlaceAtSchool())
		{
			PlaceAtShed();
		}
	}

	private bool PlaceAtSchool()
	{
		Vector3 vector = Vector3.zero;
		SchoolItem[] items = SchoolData.Load().items;
		foreach (SchoolItem schoolItem in items)
		{
			if (schoolItem.id == "doghouse")
			{
				vector = new Vector3(schoolItem.x, schoolItem.y, schoolItem.z);
			}
		}
		if (vector == Vector3.zero)
		{
			vector = dog.PeekPoint;
		}
		if (vector == Vector3.zero)
		{
			return false;
		}
		for (float num = 2f; num <= 5f; num += 0.5f)
		{
			for (int j = 0; j < 24; j++)
			{
				float f = (float)j * MathF.PI / 12f;
				Vector3 vector2 = vector + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
				if (!dog.InSchool(vector2) || !world.Walkable(vector2.x, vector2.z) || WishToy.RouteDistance(new Vector2(vector2.x, vector2.z)) < 1.2f || !Clear(vector2, 0.9f))
				{
					continue;
				}
				Vector3 vector3 = new Vector3(0f - Mathf.Sin(f), 0f, Mathf.Cos(f));
				Vector3 vector4 = vector2 + vector3 * 1.1f;
				if (!dog.InSchool(vector4) || !world.Walkable(vector4.x, vector4.z) || !Clear(vector4, 0.8f))
				{
					vector4 = vector2 - vector3 * 1.1f;
					if (!dog.InSchool(vector4) || !world.Walkable(vector4.x, vector4.z) || !Clear(vector4, 0.8f))
					{
						continue;
					}
				}
				float num2 = world.GroundAt(vector2.x, vector2.z, vector.y + 1f);
				Center = new Vector3(vector2.x, num2, vector2.z);
				vector4.y = world.GroundAt(vector4.x, vector4.z, num2 + 1f);
				ChestRest = vector4;
				seaDirection = Vector3.zero;
				return true;
			}
		}
		return false;
	}

	private bool Clear(Vector3 q, float d)
	{
		foreach (EggboyCollider collider in world.Colliders)
		{
			if (!collider.water)
			{
				float num = collider.r ?? Mathf.Max(collider.hw.GetValueOrDefault(), collider.hd.GetValueOrDefault());
				if (new Vector2(q.x - collider.x, q.z - collider.z).magnitude < num + d)
				{
					return false;
				}
			}
		}
		return true;
	}

	private void PlaceAtShed()
	{
		Vector3 vector = Vector3.zero;
		LighthouseItem[] items = LighthouseData.Load().items;
		foreach (LighthouseItem lighthouseItem in items)
		{
			if (lighthouseItem.id == "oil")
			{
				vector = new Vector3(lighthouseItem.x, lighthouseItem.y, lighthouseItem.z);
			}
		}
		float num = WaterLevel.LevelYs[2] + 0.12f;
		Vector3 vector2 = vector;
		float num2 = float.MaxValue;
		Vector3 vector3 = Vector3.forward;
		for (float num3 = 1.5f; num3 <= 6f; num3 += 0.5f)
		{
			for (int j = 0; j < 24; j++)
			{
				float f = (float)j * MathF.PI / 12f;
				Vector3 vector4 = vector + new Vector3(Mathf.Cos(f) * num3, 0f, Mathf.Sin(f) * num3);
				if (!world.InWalkArea(vector4.x, vector4.z))
				{
					continue;
				}
				float num4 = world.GroundAt(vector4.x, vector4.z, vector.y + 2f);
				if (num4 < num)
				{
					continue;
				}
				for (int k = 0; k < 8; k++)
				{
					float f2 = (float)k * MathF.PI / 4f;
					Vector3 vector5 = vector4 + new Vector3(Mathf.Cos(f2), 0f, Mathf.Sin(f2)) * 1.6f;
					if (!world.InWalkArea(vector5.x, vector5.z) || world.GroundAt(vector5.x, vector5.z, num4 + 1f) < WaterLevel.LevelYs[2] - 0.05f)
					{
						float num5 = num3;
						if (num5 < num2)
						{
							num2 = num5;
							vector2 = new Vector3(vector4.x, num4, vector4.z);
							vector3 = (vector5 - vector4).normalized;
						}
					}
				}
			}
		}
		Center = vector2;
		Vector3 vector6 = new Vector3(0f - vector3.z, 0f, vector3.x);
		Vector3 chestRest = vector2 + vector6 * 1.1f;
		if (!world.Walkable(chestRest.x, chestRest.z))
		{
			chestRest = vector2 - vector6 * 1.1f;
		}
		if (!world.Walkable(chestRest.x, chestRest.z))
		{
			chestRest = vector2 - vector3 * 1f;
		}
		chestRest.y = world.GroundAt(chestRest.x, chestRest.z, vector2.y + 1f);
		ChestRest = chestRest;
		seaDirection = vector3;
	}

	private static Material Mat(Color c)
	{
		Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
		material.color = c;
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", c);
		}
		if (material.HasProperty("_Smoothness"))
		{
			material.SetFloat("_Smoothness", 0.25f);
		}
		return material;
	}

	private void BuildPlate()
	{
		GameObject gameObject = new GameObject("DogPawPlate");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.transform.position = Center;
		Material wood = Mat(new Color(0.55f, 0.36f, 0.2f));
		Material sharedMaterial = Mat(new Color(0.95f, 0.5f, 0.18f));
		GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		UnityEngine.Object.Destroy(obj.GetComponent<Collider>());
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		obj.transform.localScale = new Vector3(0.7f, 0.03f, 0.7f);
		obj.transform.localPosition = new Vector3(0f, 0.02f, 0f);
		obj.GetComponent<Renderer>().sharedMaterial = wood;
		GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		UnityEngine.Object.Destroy(top.GetComponent<Collider>());
		top.transform.SetParent(gameObject.transform, worldPositionStays: false);
		top.transform.localScale = new Vector3(0.6f, 0.02f, 0.6f);
		top.transform.localPosition = new Vector3(0f, 0.06f, 0f);
		top.GetComponent<Renderer>().sharedMaterial = sharedMaterial;
		plateTop = top.transform;
		Pad(new Vector3(0f, 0.6f, -0.08f), 0.32f);
		Pad(new Vector3(-0.2f, 0.6f, 0.16f), 0.13f);
		Pad(new Vector3(-0.07f, 0.6f, 0.24f), 0.13f);
		Pad(new Vector3(0.07f, 0.6f, 0.24f), 0.13f);
		Pad(new Vector3(0.2f, 0.6f, 0.16f), 0.13f);
		void Pad(Vector3 p, float s)
		{
			GameObject obj2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			UnityEngine.Object.Destroy(obj2.GetComponent<Collider>());
			obj2.transform.SetParent(top.transform, worldPositionStays: false);
			obj2.transform.localPosition = p;
			obj2.transform.localScale = new Vector3(s, 0.9f, s);
			obj2.GetComponent<Renderer>().sharedMaterial = wood;
		}
	}

	private void BuildChest()
	{
		GameObject gameObject = TideTownStyleUI.CloneSceneLux3D("S2_木箱_分享包版", 0.55f);
		if (gameObject == null)
		{
			gameObject = new GameObject("PawChest");
		}
		gameObject.name = "PawChest";
		gameObject.transform.SetParent(base.transform, worldPositionStays: true);
		chest = gameObject.transform;
		Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Material[] materials = componentsInChildren[i].materials;
			foreach (Material material in materials)
			{
				string[] array = new string[3] { "_BaseColor", "_Color", "baseColorFactor" };
				foreach (string text in array)
				{
					if (material.HasProperty(text))
					{
						material.SetColor(text, Color.Lerp(material.GetColor(text), new Color(0.85f, 0.62f, 0.3f), 0.35f));
					}
				}
			}
		}
		if (Done)
		{
			chest.position = ChestRest;
		}
		else
		{
			chest.position = ChestRest + seaDirection * 1.4f + Vector3.down * ((seaDirection == Vector3.zero) ? 0.7f : 1.4f);
		}
		chest.gameObject.SetActive(Done);
	}

	private void Update()
	{
		if (state != null && !(dog == null) && !Done)
		{
			bool flag = dog.Visible && Vector2.Distance(new Vector2(dog.transform.position.x, dog.transform.position.z), new Vector2(Center.x, Center.z)) < 0.6f;
			if (plateTop != null)
			{
				plateTop.localPosition = Vector3.Lerp(plateTop.localPosition, new Vector3(0f, flag ? 0.035f : 0.06f, 0f), Time.deltaTime * 10f);
			}
			if (flag && !rising)
			{
				state.flags.Add("paw_plate_done");
				StartCoroutine(RiseChest());
			}
		}
	}

	private IEnumerator RiseChest()
	{
		rising = true;
		ShadowAndChimeSystem.PlayProceduralChime(330f, 0.3f);
		yield return new WaitForSeconds(0.4f);
		chest.gameObject.SetActive(value: true);
		Vector3 from = chest.position;
		if (seaDirection == Vector3.zero)
		{
			for (float t2 = 0f; t2 < 1.4f; t2 += Time.deltaTime)
			{
				float t5 = Mathf.SmoothStep(0f, 1f, t2 / 1.4f) + Mathf.Sin(t2 * 22f) * 0.015f * (1f - t2 / 1.4f);
				chest.position = Vector3.LerpUnclamped(from, ChestRest, t5);
				yield return null;
			}
			ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.25f);
			for (float t2 = 0f; t2 < 0.35f; t2 += Time.deltaTime)
			{
				chest.position = ChestRest + Vector3.up * Mathf.Sin(t2 / 0.35f * MathF.PI) * 0.12f;
				yield return null;
			}
			chest.position = ChestRest;
			rising = false;
			yield break;
		}
		Vector3 surface = new Vector3(from.x, Mathf.Max(WaterLevel.StaticY, ChestRest.y) - 0.1f, from.z);
		for (float t2 = 0f; t2 < 1.2f; t2 += Time.deltaTime)
		{
			chest.position = Vector3.Lerp(from, surface, Mathf.SmoothStep(0f, 1f, t2 / 1.2f));
			yield return null;
		}
		ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.25f);
		for (float t2 = 0f; t2 < 1f; t2 += Time.deltaTime)
		{
			float num = Mathf.SmoothStep(0f, 1f, t2);
			chest.position = Vector3.Lerp(surface, ChestRest, num) + Vector3.up * Mathf.Sin(num * MathF.PI) * 0.2f;
			yield return null;
		}
		chest.position = ChestRest;
		rising = false;
	}

	public void OpenChest()
	{
		if (!ChestReady || ChestOpened)
		{
			return;
		}
		state.flags.Add("paw_chest_opened");
		string[] array = new string[3] { "hat", "scope", "log" };
		foreach (string text in array)
		{
			if (!state.flags.Contains("dog_sniff_" + text))
			{
				state.flags.Add("dog_sniff_" + text);
			}
		}
		TideTownAchievementSystem.Unlock("dog_detective");
		ShadowAndChimeSystem.PlayProceduralChime(880f, 0.4f);
		if (chest != null)
		{
			chest.localRotation *= Quaternion.Euler(0f, 0f, 8f);
		}
		if (ui != null)
		{
			ui.ShowMemory("匣子里的一张航标纸", ((seaDirection == Vector3.zero) ? "纸边沾着土，有点潮，字还看得清：" : "纸被海水泡软了，字还看得清：") + "\n\n白檐帽\u30006\n单筒镜\u30002\n航海日志\u30003\n\n底下一行小字：给小黑留着，它鼻子比我灵。");
		}
	}
}
