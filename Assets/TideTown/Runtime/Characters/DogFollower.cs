using System;
using System.Collections.Generic;
using UnityEngine;

public class DogFollower : MonoBehaviour
{
	[Serializable]
	private class HomeTier
	{
		public string id;

		public float[] x;

		public float[] z;
	}

	[Serializable]
	private class HomeDoc
	{
		public HomeTier[] tiers;
	}

	public float followDistance = 1.2f;

	public float sideOffset = 0.4f;

	public float maxSpeed = 3.4f;

	public float snapDistance = 8f;

	public Vector3 PeekPoint;

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private GameState state;

	private DogVisual visual;

	private bool bound;

	private Vector3 vel;

	private float yaw;

	private Vector3 lastPlayerPos;

	private bool hasLastPlayer;

	private float petTimer;

	private int petCount;

	private readonly HashSet<string> barkedSteps = new HashSet<string>();

	private static readonly string[] SniffClueOrder = new string[9] { "hat", "scope", "log", "slipper", "doghouse", "blackboard", "recipe", "scale", "basket" };

	private static readonly Dictionary<string, string> SniffClueToast = new Dictionary<string, string>
	{
		{ "hat", "小黑嗅出了「守塔人帽」的线索：灯塔罗经 · 外环刻度为 6" },
		{ "scope", "小黑嗅出了「黄铜望远镜」的线索：灯塔罗经 · 中环刻度为 2" },
		{ "log", "小黑嗅出了「航海日志」的线索：灯塔罗经 · 内环刻度为 3" },
		{ "slipper", "小黑叼起「旧拖鞋」转了一圈：学校木匣 · 外环刻度为 4" },
		{ "doghouse", "小黑用爪子拍了拍「狗窝」：学校木匣 · 中环刻度为 1" },
		{ "blackboard", "小黑对着「操场黑板」叫了两声：学校木匣 · 内环刻度为 6" },
		{ "recipe", "小黑嗅出了「菜谱本」的气味：面包房风门 · 外环刻度为 3" },
		{ "scale", "小黑嗅出了「老台秤」的气味：面包房风门 · 中环刻度为 5" },
		{ "basket", "小黑嗅出了「面包篮」的葡萄干香：面包房风门 · 内环刻度为 2" }
	};

	public static bool StayNearSchool = true;

	private const float LeashMargin = 3f;

	private Vector2[] homeArea;

	private Vector3 wanderTarget;

	private float wanderTimer;

	private Vector3 guidePos;

	private float guideTimer;

	private float guideArriveRadius = 1.3f;

	private float guideStaySeconds = 2.5f;

	private bool guideArrived;

	private string sniffTarget;

	private static Dictionary<string, Vector3> cluePositions;

	public bool Bound => bound;

	public bool Following { get; private set; } = true;


	public bool HomeMode { get; private set; }

	public Vector2[] HomeArea
	{
		get
		{
			LoadHome();
			return homeArea;
		}
	}

	public bool InSchoolArea => InSchool(base.transform.position, 1f);

	public bool Visible { get; private set; } = true;


	public bool Happy { get; private set; }

	public float WagHz { get; private set; }

	public float LastGroundError { get; private set; }

	public bool Guiding => guideTimer > 0f;

	public bool GuideArrived => guideArrived;

	public string LastCommand { get; private set; } = "";


	private void LoadHome()
	{
		if (homeArea != null)
		{
			return;
		}
		TextAsset textAsset = Resources.Load<TextAsset>("World/world_layout");
		HomeDoc homeDoc = ((textAsset != null) ? JsonUtility.FromJson<HomeDoc>(textAsset.text) : null);
		HomeTier homeTier = ((homeDoc?.tiers != null) ? Array.Find(homeDoc.tiers, (HomeTier a) => a.id == "pad_grass_3_0_0") : null);
		if (homeTier == null || homeTier.x == null)
		{
			homeArea = new Vector2[0];
			return;
		}
		homeArea = new Vector2[homeTier.x.Length];
		for (int i = 0; i < homeArea.Length; i++)
		{
			homeArea[i] = new Vector2(homeTier.x[i], homeTier.z[i]);
		}
	}

	public bool InSchool(Vector3 p, float margin = 0f)
	{
		LoadHome();
		if (homeArea.Length < 3)
		{
			return true;
		}
		bool flag = false;
		int num = 0;
		int num2 = homeArea.Length - 1;
		while (num < homeArea.Length)
		{
			Vector2 vector = homeArea[num];
			Vector2 vector2 = homeArea[num2];
			if (vector.y > p.z != vector2.y > p.z && p.x < (vector2.x - vector.x) * (p.z - vector.y) / (vector2.y - vector.y) + vector.x)
			{
				flag = !flag;
			}
			num2 = num++;
		}
		if (flag || margin <= 0f)
		{
			return flag;
		}
		Vector2 vector3 = new Vector2(p.x, p.z);
		int num3 = 0;
		int num4 = homeArea.Length - 1;
		while (num3 < homeArea.Length)
		{
			Vector2 vector4 = homeArea[num4];
			Vector2 vector5 = homeArea[num3] - vector4;
			float num5 = Mathf.Clamp01(Vector2.Dot(vector3 - vector4, vector5) / Mathf.Max(vector5.sqrMagnitude, 1E-06f));
			if ((vector4 + vector5 * num5 - vector3).sqrMagnitude < margin * margin)
			{
				return true;
			}
			num4 = num3++;
		}
		return false;
	}

	private Vector3 HomeWanderPoint(Vector3 from)
	{
		wanderTimer -= Time.deltaTime;
		if (wanderTimer > 0f && wanderTarget != Vector3.zero)
		{
			return wanderTarget;
		}
		Vector3 vector = ((PeekPoint != Vector3.zero) ? PeekPoint : from);
		for (int i = 0; i < 16; i++)
		{
			float f = UnityEngine.Random.value * MathF.PI * 2f;
			float num = UnityEngine.Random.Range(0.8f, 6f);
			Vector3 p = vector + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
			if (InSchool(p) && world.Walkable(p.x, p.z))
			{
				wanderTarget = p;
				wanderTimer = UnityEngine.Random.Range(5f, 10f);
				return wanderTarget;
			}
		}
		wanderTarget = vector;
		wanderTimer = 4f;
		return wanderTarget;
	}

	public void Hide()
	{
		Following = false;
		SetVisible(on: false);
	}

	public void ShowAt(Vector3 p)
	{
		Following = false;
		Snap(p);
		SetVisible(on: true);
	}

	public void Follow()
	{
		if (Following)
		{
			SetVisible(on: true);
			return;
		}
		Following = true;
		lastPlayerPos = ((player != null) ? player.transform.position : base.transform.position);
		hasLastPlayer = player != null;
		if (bound)
		{
			Snap((StayNearSchool && !InSchool(player.transform.position, 3f)) ? HomeWanderPoint((PeekPoint != Vector3.zero) ? PeekPoint : base.transform.position) : TargetPoint());
		}
		SetVisible(on: true);
	}

	private void SetVisible(bool on)
	{
		Visible = on;
		Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = on;
		}
	}

	public void Bind(EggboyPlayer p, IslandWorld w, GameState s)
	{
		player = p;
		world = w;
		state = s;
		ui = UnityEngine.Object.FindObjectOfType<EggboyUI>();
		visual = GetComponent<DogVisual>();
		bound = player != null && world != null;
		if (bound)
		{
			lastPlayerPos = player.transform.position;
			hasLastPlayer = true;
			Snap(TargetPoint());
		}
	}

	public void GuideTo(Vector3 worldPos, float seconds, float arriveRadius = 1.3f, float staySeconds = 2.5f)
	{
		if (!StayNearSchool || InSchool(worldPos, 3f))
		{
			guidePos = worldPos;
			guideTimer = seconds;
			guideArrived = false;
			guideArriveRadius = arriveRadius;
			guideStaySeconds = staySeconds;
		}
	}

	public void CommandOrPet()
	{
		if (Following && Visible && !(player == null) && !HomeMode)
		{
			Vector3 position = player.transform.position;
			DogPawPlate instance = DogPawPlate.Instance;
			string id;
			Vector3 pos;
			if (instance != null && !instance.Done && instance.Available && Vector3.Distance(position, instance.Center) < 8f)
			{
				LastCommand = "plate";
				sniffTarget = null;
				GuideTo(instance.Center, 20f, 0.25f, 3.5f);
				ShadowAndChimeSystem.PlayProceduralChime(880f, 0.12f);
			}
			else if (FindSniffTarget(position, 10f, out id, out pos))
			{
				LastCommand = "sniff:" + id;
				sniffTarget = id;
				GuideTo(pos, 10f, 1.1f, 2.2f);
				ShadowAndChimeSystem.PlayProceduralChime(880f, 0.12f);
			}
			else
			{
				LastCommand = "pet";
				petTimer = 2.6f;
				petCount++;
				player.FaceTowards(base.transform.position);
				player.PlayEmote(player.HasClip("pet_dog") ? "pet_dog" : "hop");
				ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.25f);
			}
		}
	}

	public bool CanBePetted(Vector3 from)
	{
		if (Following && Visible && !HomeMode)
		{
			return new Vector2(base.transform.position.x - from.x, base.transform.position.z - from.z).magnitude < 2f;
		}
		return false;
	}

	public void PetByPlayer()
	{
		petTimer = 2.6f;
		petCount++;
		ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.25f);
	}

	private bool FindSniffTarget(Vector3 from, float range, out string id, out Vector3 pos)
	{
		id = null;
		pos = Vector3.zero;
		if (state == null)
		{
			return false;
		}
		float num = range;
		foreach (KeyValuePair<string, Vector3> item in ClueItemPositions())
		{
			string key = item.Key;
			if (!state.seenMemories.Contains(key + "_detail") && !state.flags.Contains("dog_sniff_" + key) && !(Mathf.Abs(item.Value.y - from.y) > 3f))
			{
				float num2 = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(item.Value.x, 0f, item.Value.z));
				if (num2 < num)
				{
					num = num2;
					id = key;
					pos = item.Value;
				}
			}
		}
		return id != null;
	}

	private static Dictionary<string, Vector3> ClueItemPositions()
	{
		if (cluePositions != null)
		{
			return cluePositions;
		}
		cluePositions = new Dictionary<string, Vector3>();
		string[] sniffClueOrder = SniffClueOrder;
		foreach (string text in sniffClueOrder)
		{
			BakeryItem[] array = BakeryData.Load();
			foreach (BakeryItem bakeryItem in array)
			{
				if (bakeryItem.id == text)
				{
					cluePositions[text] = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
				}
			}
			SchoolItem[] items = SchoolData.Load().items;
			foreach (SchoolItem schoolItem in items)
			{
				if (schoolItem.id == text)
				{
					cluePositions[text] = new Vector3(schoolItem.x, schoolItem.y, schoolItem.z);
				}
			}
			LighthouseItem[] items2 = LighthouseData.Load().items;
			foreach (LighthouseItem lighthouseItem in items2)
			{
				if (lighthouseItem.id == text)
				{
					cluePositions[text] = new Vector3(lighthouseItem.x, lighthouseItem.y, lighthouseItem.z);
				}
			}
		}
		return cluePositions;
	}

	private void MarkSniffedClue(string itemId)
	{
		if (state == null || string.IsNullOrEmpty(itemId))
		{
			return;
		}
		if (!state.flags.Contains("dog_sniff_" + itemId))
		{
			state.flags.Add("dog_sniff_" + itemId);
		}
		ShadowAndChimeSystem.PlayProceduralChime(987.77f, 0.18f);
		if (!WoodenMechanismPuzzleModal.ClueInfo(itemId, out var ring, out var number))
		{
			return;
		}
		int num = 0;
		string[] sniffClueOrder = SniffClueOrder;
		foreach (string text in sniffClueOrder)
		{
			if (state.flags.Contains("dog_sniff_" + text))
			{
				num++;
			}
		}
		if (num >= 3)
		{
			TideTownAchievementSystem.Unlock("dog_detective");
		}
		Vector3 position = base.transform.position;
		position.y = ((world != null) ? world.GroundAt(position.x, position.z, position.y + 0.5f) : position.y);
		ChalkMark.Spawn(position + base.transform.forward * 0.25f, (new string[3] { "外环", "中环", "内环" })[ring] + " " + number, itemId);
	}

	private void LateUpdate()
	{
		if (!bound || !Following || player == null || world == null)
		{
			return;
		}
		float num = Mathf.Min(0.05f, Time.deltaTime);
		if (num <= 0f)
		{
			return;
		}
		bool flag = ui != null && ui.inputClose && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen && !player.locked && !ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen;
		if (flag)
		{
			if (EasyInputBridge.Down(KeyCode.F))
			{
				CommandOrPet();
			}
			CheckCompanionBarkHint();
		}
		if (petTimer > 0f)
		{
			petTimer = Mathf.Max(0f, petTimer - num);
		}
		Vector3 position = player.transform.position;
		float num2 = 0f;
		if (hasLastPlayer)
		{
			num2 = new Vector2(position.x - lastPlayerPos.x, position.z - lastPlayerPos.z).magnitude / num;
		}
		lastPlayerPos = position;
		hasLastPlayer = true;
		Vector3 at = TargetPoint();
		bool homeMode = HomeMode;
		HomeMode = StayNearSchool && guideTimer <= 0f && !InSchool(position, 3f);
		if (HomeMode)
		{
			at = HomeWanderPoint(base.transform.position);
		}
		else if (homeMode)
		{
			wanderTimer = 0f;
		}
		if (guideTimer > 0f)
		{
			guideTimer -= num;
			Vector3 vector = guidePos;
			if (!world.Walkable(vector.x, vector.z))
			{
				Vector3 position2 = base.transform.position;
				for (float num3 = 0.9f; num3 > 0.05f; num3 -= 0.1f)
				{
					Vector3 vector2 = Vector3.Lerp(position2, guidePos, num3);
					if (world.Walkable(vector2.x, vector2.z))
					{
						vector = vector2;
						break;
					}
				}
			}
			at = vector;
			float magnitude = new Vector2(base.transform.position.x - guidePos.x, base.transform.position.z - guidePos.z).magnitude;
			float magnitude2 = new Vector2(base.transform.position.x - vector.x, base.transform.position.z - vector.z).magnitude;
			if (!guideArrived && (magnitude < guideArriveRadius || magnitude2 < 0.25f))
			{
				guideArrived = true;
				petTimer = 1.8f;
				guideTimer = Mathf.Min(guideTimer, guideStaySeconds);
				if (sniffTarget != null)
				{
					MarkSniffedClue(sniffTarget);
					sniffTarget = null;
				}
			}
		}
		Vector3 position3 = base.transform.position;
		Vector3 vector3 = new Vector3(at.x - position3.x, 0f, at.z - position3.z);
		float magnitude3 = vector3.magnitude;
		if (!world.Walkable(position3.x, position3.z))
		{
			Snap(at);
		}
		else if (magnitude3 > (HomeMode ? 30f : snapDistance) && guideTimer <= 0f)
		{
			Snap(at);
			position3 = base.transform.position;
		}
		else
		{
			float a = Mathf.Min(maxSpeed, magnitude3 * 4f);
			if (HomeMode)
			{
				a = Mathf.Min(1.6f, magnitude3 * 2f);
			}
			else
			{
				if (player.Speed > 0.2f)
				{
					a = Mathf.Max(a, player.Speed * 1.08f);
				}
				a = Mathf.Max(a, Mathf.Min(12f, num2 + 0.35f));
			}
			Vector3 vector4 = ((magnitude3 > 0.0001f) ? (vector3 / magnitude3) : Vector3.zero);
			vel = Vector3.Lerp(vel, vector4 * a, Mathf.Min(1f, num * 10f));
			Vector3 vector5 = new Vector3(position3.x + vel.x * num, position3.y, position3.z + vel.z * num);
			Vector3 vector6 = PushOutOfBoxes(vector5);
			if (!world.Walkable(vector6.x, vector6.z))
			{
				vector6 = vector5;
			}
			if (world.Walkable(vector6.x, vector6.z))
			{
				position3.x = vector6.x;
				position3.z = vector6.z;
			}
			else if (world.Walkable(vector6.x, position3.z))
			{
				position3.x = vector6.x;
				vel.z = 0f;
			}
			else if (world.Walkable(position3.x, vector6.z))
			{
				position3.z = vector6.z;
				vel.x = 0f;
			}
			else
			{
				vel = Vector3.zero;
			}
		}
		float aboveY = position3.y;
		float y = player.transform.position.y;
		if (new Vector2(player.transform.position.x - position3.x, player.transform.position.z - position3.z).magnitude < 4.5f && y - position3.y < 0.65f && y - position3.y > -1f)
		{
			aboveY = Mathf.Max(position3.y, y + 0.15f);
		}
		float num4 = world.GroundAt(position3.x, position3.z, aboveY);
		float num5 = ((flag && petTimer > 0f) ? (Mathf.Abs(Mathf.Sin(petTimer * 10f)) * 0.14f) : 0f);
		position3.y = num4 + num5;
		LastGroundError = num4 - num4;
		base.transform.position = position3;
		float magnitude4 = new Vector2(vel.x, vel.z).magnitude;
		bool trot = magnitude4 > 1f || player.Speed > 1.2f || petTimer > 0f;
		float num6 = ((magnitude4 > 0.15f) ? Mathf.Atan2(vel.x, vel.z) : Mathf.Atan2(player.transform.position.x - position3.x, player.transform.position.z - position3.z));
		float num7 = Mathf.Atan2(Mathf.Sin(num6 - yaw), Mathf.Cos(num6 - yaw));
		yaw += num7 * Mathf.Min(1f, num * 8f);
		base.transform.rotation = Quaternion.Euler(0f, yaw * 57.29578f, 0f);
		Happy = petTimer > 0f || (state != null && (state.flags.Contains("oven_lit") || state.flags.Contains("school_read") || (player.CurrentEmote != null && player.CurrentEmote == "hop")));
		WagHz = ((petTimer > 0f) ? 6.5f : (Happy ? 4.6f : ((magnitude4 < 0.2f) ? 2.4f : 1.3f)));
		if (visual != null)
		{
			visual.ApplyMotion(num, magnitude4, trot, WagHz, Happy);
		}
	}

	private void CheckCompanionBarkHint()
	{
		if (state == null || Guiding)
		{
			return;
		}
		string step = state.step;
		if (string.IsNullOrEmpty(step) || barkedSteps.Contains(step))
		{
			return;
		}
		Vector3 position = player.transform.position;
		Vector3 vector = Vector3.zero;
		float num = 0f;
		if (step == "L1_shed")
		{
			LighthouseItem[] items = LighthouseData.Load().items;
			foreach (LighthouseItem lighthouseItem in items)
			{
				if (lighthouseItem.id == "oil")
				{
					vector = new Vector3(lighthouseItem.x, lighthouseItem.y, lighthouseItem.z);
				}
			}
			num = 5f;
		}
		else if (step == "E1_read")
		{
			vector = EndingData.Point(EndingData.Load().desk);
			num = 5f;
		}
		if (!(num <= 0f) && !(vector == Vector3.zero) && !(Vector2.Distance(new Vector2(position.x, position.z), new Vector2(vector.x, vector.z)) > num))
		{
			barkedSteps.Add(step);
			GuideTo(vector, 6f);
			ShadowAndChimeSystem.PlayProceduralChime(880f, 0.12f);
		}
	}

	private Vector3 TargetPoint()
	{
		float f = player.yaw;
		Vector3 vector = new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f));
		Vector3 vector2 = new Vector3(Mathf.Cos(f), 0f, 0f - Mathf.Sin(f));
		Vector3 position = player.transform.position;
		Vector3 vector3 = position - vector * followDistance;
		Vector3 result = vector3 + vector2 * sideOffset;
		if (world.Walkable(result.x, result.z))
		{
			return result;
		}
		if (world.Walkable(vector3.x, vector3.z))
		{
			return vector3;
		}
		Vector3 result2 = position - vector * (followDistance * 0.6f);
		if (world.Walkable(result2.x, result2.z))
		{
			return result2;
		}
		return position;
	}

	private Vector3 PushOutOfBoxes(Vector3 p)
	{
		foreach (EggboyCollider collider in world.Colliders)
		{
			if (collider.water || (collider.top.HasValue && collider.top.Value <= p.y + 0.25f))
			{
				continue;
			}
			if (collider.r.HasValue)
			{
				float num = p.x - collider.x;
				float num2 = p.z - collider.z;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				float num4 = collider.r.Value + 0.16f;
				if (num3 < num4 && num3 > 1E-06f)
				{
					p.x = collider.x + num / num3 * num4;
					p.z = collider.z + num2 / num3 * num4;
				}
			}
			else
			{
				if (!collider.hw.HasValue || !collider.hd.HasValue)
				{
					continue;
				}
				float num5 = collider.hw.Value + 0.16f;
				float num6 = collider.hd.Value + 0.16f;
				float f = p.x - collider.x;
				float f2 = p.z - collider.z;
				if (Mathf.Abs(f) < num5 && Mathf.Abs(f2) < num6)
				{
					if (num5 - Mathf.Abs(f) < num6 - Mathf.Abs(f2))
					{
						p.x = collider.x + Mathf.Sign(f) * num5;
					}
					else
					{
						p.z = collider.z + Mathf.Sign(f2) * num6;
					}
				}
			}
		}
		return p;
	}

	private void Snap(Vector3 at)
	{
		float y = world.GroundAt(at.x, at.z, float.PositiveInfinity);
		base.transform.position = new Vector3(at.x, y, at.z);
		vel = Vector3.zero;
	}
}
