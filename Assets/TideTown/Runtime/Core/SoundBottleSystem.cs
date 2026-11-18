using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SoundBottleSystem : MonoBehaviour
{
	public static readonly string[] Order = new string[4] { "bell", "swing", "bark", "chop" };

	private static readonly string[] Names = new string[4] { "铃声", "秋千声", "小黑的叫声", "劈柴声" };

	private GameState state;

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private DogFollower dog;

	private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

	private readonly List<Transform> houses = new List<Transform>();

	private readonly List<Vector3> housePos = new List<Vector3>();

	private readonly List<Quaternion> houseRot = new List<Quaternion>();

	private Light boxLight;

	private readonly TextMesh[] soundSlots = new TextMesh[Order.Length];

	public static SoundBottleSystem Instance { get; private set; }

	public Vector3 MusicBoxPos { get; private set; }

	public bool Playing { get; private set; }

	public bool Done
	{
		get
		{
			if (state != null)
			{
				return state.flags.Contains("music_box_done");
			}
			return false;
		}
	}

	public float MaxHouseLift { get; private set; }

	public bool HasBottle
	{
		get
		{
			if (state != null)
			{
				return state.flags.Contains("has_sound_bottle");
			}
			return false;
		}
	}

	public int Count
	{
		get
		{
			int num = 0;
			string[] order = Order;
			foreach (string id in order)
			{
				if (Has(id))
				{
					num++;
				}
			}
			return num;
		}
	}

	public int HouseCount => houses.Count;

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
	{
		Instance = this;
		player = p;
		ui = u;
		world = w;
		state = s;
		dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		clips["bell"] = Synth("bell");
		clips["swing"] = Synth("swing");
		clips["bark"] = Synth("bark");
		clips["chop"] = Synth("chop");
		InteractionService interactionService = UnityEngine.Object.FindObjectOfType<InteractionService>();
		if (interactionService != null)
		{
			Register(interactionService, "bell", GameObject.Find("Bakery_bell"));
			Register(interactionService, "swing", GameObject.Find("S_swing"));
			Register(interactionService, "chop", GameObject.Find("Bakery_wood"));
			if (dog != null)
			{
				interactionService.Register(new SoundSource(this, "bark", () => dog.transform.position, () => dog.Following && dog.Visible));
			}
		}
		BuildMusicBox();
		RefreshSoundSlots();
		if (interactionService != null)
		{
			interactionService.Register(new MusicBoxInteractable(this));
		}
		Transform[] array = UnityEngine.Object.FindObjectsOfType<Transform>();
		foreach (Transform transform in array)
		{
			if ((transform.name.StartsWith("B6_") || transform.name.StartsWith("B7_")) && transform.name.EndsWith("_visual"))
			{
				houses.Add(transform);
			}
		}
	}

	private void Register(InteractionService svc, string id, GameObject go)
	{
		if (!(go == null))
		{
			Vector3 pos = go.transform.position;
			svc.Register(new SoundSource(this, id, () => pos, () => true));
		}
	}

	public bool Has(string id)
	{
		if (state != null)
		{
			return state.flags.Contains("sound_" + id);
		}
		return false;
	}

	public static string NameOf(string id)
	{
		int num = Array.IndexOf(Order, id);
		if (num < 0)
		{
			return id;
		}
		return Names[num];
	}

	public void Capture(string id)
	{
		if (HasBottle && !Has(id) && clips.TryGetValue(id, out var value))
		{
			state.flags.Add("sound_" + id);
			ShadowAndChimeSystem.PlayClip(value, 0.9f);
			RefreshSoundSlots();
			if (id == "bark" && dog != null)
			{
				dog.GuideTo(player.transform.position, 0.5f);
			}
		}
	}

	private void BuildMusicBox()
	{
		GameObject gameObject = GameObject.Find("P_FOUNTAIN") ?? GameObject.Find("Fountain_placeholder");
		Vector3 vector = ((gameObject != null) ? gameObject.transform.position : Vector3.zero);
		Vector3 vector2 = vector;
		float num = float.MaxValue;
		for (float num2 = 1.8f; num2 <= 4f; num2 += 0.3f)
		{
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector3 vector3 = vector + new Vector3(Mathf.Cos(f) * num2, 0f, Mathf.Sin(f) * num2);
				if (world.Walkable(vector3.x, vector3.z) && num2 < num)
				{
					num = num2;
					vector2 = vector3;
				}
			}
		}
		vector2.y = world.GroundAt(vector2.x, vector2.z, vector.y + 2f);
		MusicBoxPos = vector2;
		Lux3DService lux3DService = Lux3DService.Ensure();
		GameObject gameObject2 = lux3DService.InstantiateFallback(lux3DService.Find("wish_conch_musicbox"), 0.55f);
		GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		UnityEngine.Object.Destroy(obj.GetComponent<Collider>());
		obj.name = "MusicBoxPlinth";
		obj.transform.SetParent(base.transform, worldPositionStays: false);
		obj.transform.position = vector2 + Vector3.up * 0.25f;
		obj.transform.localScale = new Vector3(0.6f, 0.25f, 0.6f);
		Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
		Color value = (material.color = new Color(0.82f, 0.76f, 0.66f));
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", value);
		}
		obj.GetComponent<Renderer>().sharedMaterial = material;
		if (gameObject2 != null)
		{
			gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
			gameObject2.transform.position = vector2 + Vector3.up * 0.5f;
		}
		Font font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
		string[] array = new string[4] { "铃", "秋千", "汪", "柴" };
		Vector3 vector4 = vector - vector2;
		vector4.y = 0f;
		Vector3 vector5 = ((vector4.sqrMagnitude > 0.01f) ? (-vector4.normalized) : Vector3.forward);
		for (int j = 0; j < 4; j++)
		{
			GameObject gameObject3 = new GameObject("MusicBoxSlot_" + Order[j]);
			gameObject3.transform.SetParent(base.transform, worldPositionStays: false);
			Vector3 vector6 = new Vector3(vector5.z, 0f, 0f - vector5.x);
			gameObject3.transform.position = vector2 + Vector3.up * 0.32f + vector5 * 0.31f + vector6 * (((float)j - 1.5f) * 0.13f);
			gameObject3.transform.rotation = Quaternion.LookRotation(-vector5, Vector3.up);
			TextMesh textMesh = gameObject3.AddComponent<TextMesh>();
			textMesh.font = font;
			textMesh.text = array[j];
			textMesh.fontSize = 64;
			textMesh.characterSize = 0.006f;
			textMesh.anchor = TextAnchor.MiddleCenter;
			soundSlots[j] = textMesh;
			if (font != null)
			{
				gameObject3.GetComponent<MeshRenderer>().sharedMaterial = font.material;
			}
		}
		GameObject gameObject4 = new GameObject("MusicBoxLight");
		gameObject4.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject4.transform.position = vector2 + Vector3.up * 1.2f;
		boxLight = gameObject4.AddComponent<Light>();
		boxLight.type = LightType.Point;
		boxLight.range = 6f;
		boxLight.intensity = 0f;
		boxLight.color = new Color(1f, 0.85f, 0.55f);
		world.AddCollider(new EggboyCollider
		{
			x = vector2.x,
			z = vector2.z,
			r = 0.35f
		});
	}

	private void RefreshSoundSlots()
	{
		for (int i = 0; i < soundSlots.Length; i++)
		{
			if (soundSlots[i] != null)
			{
				soundSlots[i].color = (Has(Order[i]) ? new Color(1f, 0.76f, 0.36f) : new Color(0.35f, 0.25f, 0.16f));
			}
		}
	}

	public void PlayMusicBox()
	{
		if (!Playing && Count >= 4)
		{
			StartCoroutine(Show());
		}
	}

	private IEnumerator Show()
	{
		Playing = true;
		if (player != null)
		{
			player.locked = true;
		}
		housePos.Clear();
		houseRot.Clear();
		foreach (Transform house in houses)
		{
			housePos.Add(house.localPosition);
			houseRot.Add(house.localRotation);
		}
		float[] melody = new float[11]
		{
			523.25f, 659.25f, 783.99f, 659.25f, 880f, 783.99f, 659.25f, 523.25f, 587.33f, 659.25f,
			523.25f
		};
		float beat = 0.5f;
		float t = 0f;
		int mi = -4;
		float next = 0f;
		float total = 2.4f + (float)melody.Length * beat + 0.8f;
		MaxHouseLift = 0f;
		while (t < total)
		{
			t += Time.deltaTime;
			if (t >= next)
			{
				if (mi < 0)
				{
					ShadowAndChimeSystem.PlayClip(clips[Order[mi + 4]], 0.8f);
					next += 0.6f;
				}
				else if (mi < melody.Length)
				{
					ShadowAndChimeSystem.PlayProceduralChime(melody[mi], 0.55f);
					next += beat;
				}
				mi++;
			}
			float num = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((total - t) / 1.2f);
			for (int i = 0; i < houses.Count; i++)
			{
				if (!(houses[i] == null))
				{
					float num2 = (float)i * 0.7f;
					float num3 = Mathf.Abs(Mathf.Sin(t / beat * MathF.PI + num2)) * 0.22f * num;
					MaxHouseLift = Mathf.Max(MaxHouseLift, num3);
					houses[i].localPosition = housePos[i] + Vector3.up * num3;
					houses[i].localRotation = houseRot[i] * Quaternion.Euler(0f, Mathf.Sin(t * 2.2f + num2) * 3f * num, 0f);
				}
			}
			if (boxLight != null)
			{
				boxLight.intensity = 2.5f * num * (0.7f + 0.3f * Mathf.Sin(t * 8f));
			}
			yield return null;
		}
		for (int j = 0; j < houses.Count; j++)
		{
			if (houses[j] != null)
			{
				houses[j].localPosition = housePos[j];
				houses[j].localRotation = houseRot[j];
			}
		}
		if (boxLight != null)
		{
			boxLight.intensity = 0.6f;
		}
		if (!state.flags.Contains("music_box_done"))
		{
			state.flags.Add("music_box_done");
		}
		TideTownAchievementSystem.Unlock("music_box");
		if (player != null)
		{
			player.locked = false;
		}
		Playing = false;
	}

	public float DebugHouseError()
	{
		float num = 0f;
		for (int i = 0; i < houses.Count && i < housePos.Count; i++)
		{
			if (houses[i] != null)
			{
				num = Mathf.Max(num, Vector3.Distance(houses[i].localPosition, housePos[i]) + Quaternion.Angle(houses[i].localRotation, houseRot[i]) * 0.001f);
			}
		}
		return num;
	}

	private static AudioClip Synth(string id)
	{
		float num = id switch
		{
			"bark" => 0.5f, 
			"swing" => 0.9f, 
			"bell" => 1.4f, 
			_ => 0.35f, 
		};
		int num2 = Mathf.RoundToInt(22050f * num);
		float[] array = new float[num2];
		System.Random random = new System.Random(id.GetHashCode());
		float num3 = 0f;
		for (int i = 0; i < num2; i++)
		{
			float num4 = (float)i / 22050f;
			float num5 = (float)(random.NextDouble() * 2.0 - 1.0);
			float value = 0f;
			switch (id)
			{
			case "bell":
			{
				float num9 = 1046.5f;
				value = (Mathf.Sin(MathF.PI * 2f * num9 * num4) * 0.5f + Mathf.Sin(MathF.PI * 2f * num9 * 2.76f * num4) * 0.25f + Mathf.Sin(MathF.PI * 2f * num9 * 5.4f * num4) * 0.12f) * Mathf.Exp((0f - num4) * 3.2f) * Mathf.Min(1f, num4 * 200f);
				break;
			}
			case "swing":
			{
				float num8 = 380f + 520f * Mathf.Sin(MathF.PI * Mathf.Clamp01(num4 / num));
				num3 += (num5 - num3) * 0.3f;
				value = (Mathf.Sin(MathF.PI * 2f * num8 * num4 + Mathf.Sin(MathF.PI * 62f * num4) * 2f) * 0.35f + num3 * 0.25f) * Mathf.Sin(MathF.PI * Mathf.Clamp01(num4 / num));
				break;
			}
			case "bark":
			{
				float num6 = ((num4 < 0.22f) ? num4 : (num4 - 0.26f));
				if (num6 >= 0f && num6 < 0.2f)
				{
					float num7 = 520f - 260f * (num6 / 0.2f);
					value = (Mathf.Sign(Mathf.Sin(MathF.PI * 2f * num7 * num6)) * 0.25f + num5 * 0.12f) * Mathf.Sin(MathF.PI * num6 / 0.2f);
				}
				break;
			}
			default:
				num3 += (num5 - num3) * 0.15f;
				value = num3 * 1.4f * Mathf.Exp((0f - num4) * 30f) + Mathf.Sin(MathF.PI * 190f * num4) * 0.5f * Mathf.Exp((0f - num4) * 14f);
				break;
			}
			array[i] = Mathf.Clamp(value, -1f, 1f);
		}
		AudioClip audioClip = AudioClip.Create("Sound_" + id, num2, 1, 22050, stream: false);
		audioClip.SetData(array, 0);
		return audioClip;
	}
}
