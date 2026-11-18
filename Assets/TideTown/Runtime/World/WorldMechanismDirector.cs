using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldMechanismDirector : MonoBehaviour
{
	[Serializable]
	private sealed class RampLayout
	{
		public RampRoad[] roads;
	}

	[Serializable]
	private sealed class RampRoad
	{
		public string id;

		public float[] x;

		public float[] z;
	}

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private StoryDirector director;

	private GameState state;

	private Transform bakeryMechRoot;

	private readonly Transform[] bakeryRings = new Transform[3];

	private Transform schoolMechRoot;

	private readonly Transform[] schoolRings = new Transform[3];

	private Transform lighthouseMechRoot;

	private readonly Transform[] lighthouseRings = new Transform[3];

	private Transform winchRoot;

	private Transform winchRing;

	private Transform winchSpindle;

	private Transform pontoonBridgeRoot;

	private Vector3 winchWorldPos;

	private Vector3 crossingChannelCenter;

	private float pontoonBaseY;

	private float winchSpinAngle;

	private bool winchRaised;

	private bool nearWinchPromptShown;

	private float surgePenaltyCooldown;

	private readonly List<FloatingCrate> gapCrates = new List<FloatingCrate>();

	private Vector3 crossingDir = Vector3.forward;

	private Collider[] pontoonColliders;

	private bool pontoonCollidersOn;

	private bool pontoonCollidersInit;

	private bool boatBridge;

	private readonly Transform[] wishPedestals = new Transform[3];

	private readonly Vector3[] wishPedestalPositions = new Vector3[3];

	private readonly List<Transform> driftBottles = new List<Transform>();

	private readonly string[] driftBottleNotes = new string[3] { "漂流瓶里的第一张海盐纸条：\n『阿婆烤炉的风门最怕急脾气。先把上面那根牵一发而动全身的中间木栓拨开，再顺着菜谱、台秤和藤篮底下的数字慢慢对。炉膛点着后，给它吹一口暖气，火就旺了。』", "漂流瓶里的第二张折角信笺：\n『学校的小黑其实最好哄了。要是你懒得去秋千底下找那颗旧红球，不妨翻开随身的信笺，画一根香喷喷的烤骨头、或者一只新飞盘送进它的碗里。』", "漂流瓶里的第三张航标残页：\n『涨潮的时候渡口水急，摇动岸边的黄铜齿轮绞盘就能把旧木船拉起来；当然，你要是愿意在心愿笺上造一艘属于自己的船渡海，大海也会托起你的心意。』" };

	private Vector3 gapA;

	private Vector3 gapB;

	private bool hasGapEnds;

	public const float WinchRadius = 2.4f;

	private const float WinchCrankSeconds = 2.5f;

	private TidalEchoCloneSystem echoSys;

	private PrototypeGame game;

	private float winchCrank;

	private float winchStrain;

	private float winchWiggle;

	private float winchCreakAt;

	private bool strainFaceShown;

	public bool DebugPlayerHoldsWinch;

	public Vector3 CrossingDirection => crossingDir;

	public bool GapActive
	{
		get
		{
			if (Features.crossingGap && !Features.classicAudit)
			{
				return gapCrates.Count > 0;
			}
			return false;
		}
	}

	public bool CrossingSolved => winchRaised;

	public Vector3 WinchPosition => winchWorldPos;

	public int WinchHolders { get; private set; }

	public float WinchCrank01 => Mathf.Clamp01(winchCrank / 2.5f);

	public bool WinchStrained => winchStrain > 1.2f;

	private static int CrateIndex(string name)
	{
		int num = name.LastIndexOf('_');
		if (num < 0 || !int.TryParse(name.Substring(num + 1), out var result))
		{
			return 999;
		}
		return result;
	}

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
	{
		player = p;
		ui = u;
		world = w;
		director = d;
		state = s;
		winchRaised = state != null && state.flags.Contains("dock_winch_raised");
		SpawnBakerySceneMechanism();
		SpawnSchoolSceneMechanism();
		SpawnFloodedDockWinchAndPontoon();
		SpawnLighthouseSceneMechanism();
		SpawnStationWishPedestals();
		SpawnTidalDriftBottles();
	}

	private void SpawnBakerySceneMechanism()
	{
		GameObject gameObject = GameObject.Find("Bakery_oven");
		Vector3 vector = ((gameObject != null) ? gameObject.transform.position : new Vector3(-11.8f, 1.3f, -7.8f));
		if (world != null)
		{
			vector.y = world.GroundAt(vector.x, vector.z, vector.y);
		}
		GameObject gameObject2 = TideTownStyleUI.BuildArticulatedMechanism3D("M1_鲁班三环风门机关", 0.68f);
		gameObject2.name = "Scene_M1_鲁班三环风门机关";
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject2.transform.position = vector + new Vector3(1.08f, 0.92f, 0.42f);
		gameObject2.transform.rotation = Quaternion.Euler(0f, 145f, 0f);
		bakeryMechRoot = gameObject2.transform;
		for (int i = 0; i < 3; i++)
		{
			bakeryRings[i] = TideTownStyleUI.FindDeepChild(bakeryMechRoot, "Ring_" + i);
		}
		if (world != null)
		{
			world.AddCollider(new EggboyCollider
			{
				x = gameObject2.transform.position.x,
				z = gameObject2.transform.position.z,
				r = 0.12f,
				top = vector.y + 1.35f
			});
		}
		GameObject gameObject3 = TideTownStyleUI.CloneSceneLux3D("S11_晾衣架_分享包版", 1.65f);
		if (gameObject3 != null)
		{
			Vector3 position = vector + new Vector3(-2.15f, 0f, 1.15f);
			if (world != null)
			{
				position.y = world.GroundAt(position.x, position.z, vector.y);
			}
			gameObject3.transform.SetParent(base.transform, worldPositionStays: false);
			gameObject3.transform.position = position;
			gameObject3.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
			if (world != null)
			{
				world.AddCollider(new EggboyCollider
				{
					x = position.x,
					z = position.z,
					r = 0.14f,
					top = position.y + 1.45f
				});
			}
		}
		GameObject gameObject4 = TideTownStyleUI.CloneSceneLux3D("D6_救生圈_v2", 0.58f);
		if (gameObject4 != null)
		{
			gameObject4.transform.SetParent(gameObject2.transform, worldPositionStays: false);
			gameObject4.transform.localPosition = new Vector3(0f, -0.58f, -0.08f);
		}
		wishPedestalPositions[0] = vector + new Vector3(2.4f, 0f, 1.8f);
		if (world != null)
		{
			wishPedestalPositions[0].y = world.GroundAt(wishPedestalPositions[0].x, wishPedestalPositions[0].z, vector.y);
		}
	}

	private void SpawnSchoolSceneMechanism()
	{
		GameObject gameObject = GameObject.Find("S_bowl");
		if (gameObject == null)
		{
			gameObject = GameObject.Find("S_doghouse");
		}
		Vector3 vector = ((gameObject != null) ? gameObject.transform.position : new Vector3(17.5f, 3.05f, -5.2f));
		if (world != null)
		{
			vector.y = world.GroundAt(vector.x, vector.z, vector.y);
		}
		GameObject gameObject2 = TideTownStyleUI.BuildArticulatedMechanism3D("M2_小黑爪印转盘木匣", 0.62f);
		gameObject2.name = "Scene_M2_小黑爪印转盘木匣";
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject2.transform.position = vector + new Vector3(0.76f, 0.55f, -0.24f);
		gameObject2.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
		schoolMechRoot = gameObject2.transform;
		for (int i = 0; i < 3; i++)
		{
			schoolRings[i] = TideTownStyleUI.FindDeepChild(schoolMechRoot, "Ring_" + i);
		}
		if (world != null)
		{
			world.AddCollider(new EggboyCollider
			{
				x = gameObject2.transform.position.x,
				z = gameObject2.transform.position.z,
				r = 0.12f,
				top = vector.y + 1.1f
			});
		}
		wishPedestalPositions[1] = vector + new Vector3(-2.6f, 0f, 2.2f);
		if (world != null)
		{
			wishPedestalPositions[1].y = world.GroundAt(wishPedestalPositions[1].x, wishPedestalPositions[1].z, vector.y);
		}
	}

	private static RampRoad FindShedRamp()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("World/world_layout");
		RampRoad[] array = ((!(textAsset != null)) ? null : JsonUtility.FromJson<RampLayout>(textAsset.text)?.roads);
		if (array != null)
		{
			return Array.Find(array, (RampRoad road) => road.id == "to_shed");
		}
		return null;
	}

	private static float RampDistance(RampRoad road, Vector3 point)
	{
		float num = float.PositiveInfinity;
		Vector2 vector = new Vector2(point.x, point.z);
		for (int i = 1; i < road.x.Length; i++)
		{
			Vector2 vector2 = new Vector2(road.x[i - 1], road.z[i - 1]);
			Vector2 vector3 = new Vector2(road.x[i], road.z[i]) - vector2;
			float num2 = Mathf.Clamp01(Vector2.Dot(vector - vector2, vector3) / Mathf.Max(0.0001f, vector3.sqrMagnitude));
			num = Mathf.Min(num, (vector - (vector2 + vector3 * num2)).sqrMagnitude);
		}
		return Mathf.Sqrt(num);
	}

	private void SpawnFloodedDockWinchAndPontoon()
	{
		GameObject gameObject = GameObject.Find("crate_0");
		GameObject gameObject2 = GameObject.Find("LH_oil");
		Vector3 vector = ((gameObject != null) ? gameObject.transform.position : ((gameObject2 != null) ? (gameObject2.transform.position + new Vector3(-3.2f, 0f, -2.5f)) : new Vector3(14.5f, 1.35f, 10.5f)));
		crossingChannelCenter = vector + new Vector3(0.8f, 0f, 1.2f);
		winchWorldPos = vector + new Vector3(-1.55f, 0f, -1.35f);
		List<FloatingCrate> list = new List<FloatingCrate>(UnityEngine.Object.FindObjectsOfType<FloatingCrate>());
		list.Sort((FloatingCrate a, FloatingCrate b) => CrateIndex(a.name).CompareTo(CrateIndex(b.name)));
		if (list.Count >= 5)
		{
			int num = list.Count / 2 - 1;
			int num2 = list.Count / 2 + 1;
			Vector3 zero = Vector3.zero;
			for (int i = num; i <= num2; i++)
			{
				gapCrates.Add(list[i]);
				zero += list[i].transform.position;
			}
			gapA = list[Mathf.Max(0, num - 1)].transform.position;
			gapB = list[Mathf.Min(list.Count - 1, num2 + 1)].transform.position;
			hasGapEnds = true;
			crossingChannelCenter = zero / gapCrates.Count;
			crossingChannelCenter.y = list[num].bedY;
			crossingDir = list[list.Count - 1].transform.position - list[0].transform.position;
			crossingDir.y = 0f;
			crossingDir.Normalize();
			Vector3 position = list[0].transform.position;
			winchWorldPos = position - crossingDir * 1.6f;
			if (world != null)
			{
				RampRoad rampRoad = FindShedRamp();
				Vector3 point = new Vector3(13.4f, 1.4f, -13.1f);
				float num3 = (world.InWalkArea(point.x, point.z) ? world.GroundAt(point.x, point.z, 3f) : (-99f));
				float num4 = ((rampRoad != null) ? RampDistance(rampRoad, point) : float.MaxValue);
				if (num3 >= WaterLevel.LevelYs[2] + 0.15f && num4 >= 1.15f)
				{
					winchWorldPos = new Vector3(point.x, num3, point.z);
				}
				else
				{
					Vector3 vector2 = new Vector3(0f - crossingDir.z, 0f, crossingDir.x);
					float num5 = float.MaxValue;
					for (float num6 = 0f; num6 <= 3.6f; num6 += 0.4f)
					{
						for (float num7 = -2.4f; num7 <= 2.4f; num7 += 0.4f)
						{
							Vector3 point2 = position - crossingDir * num6 + vector2 * num7;
							if (!world.InWalkArea(point2.x, point2.z))
							{
								continue;
							}
							float num8 = world.GroundAt(point2.x, point2.z, position.y + 3f);
							if (!(num8 < WaterLevel.LevelYs[2] + 0.15f))
							{
								float num9 = ((rampRoad != null) ? RampDistance(rampRoad, point2) : Mathf.Abs(num7));
								float num10 = num6 + Mathf.Abs(num9 - 1.25f);
								if (num9 < 0.85f)
								{
									num10 += 10f;
								}
								if (num10 < num5)
								{
									num5 = num10;
									winchWorldPos = new Vector3(point2.x, num8, point2.z);
								}
							}
						}
					}
				}
			}
		}
		if (world != null)
		{
			winchWorldPos.y = world.GroundAt(winchWorldPos.x, winchWorldPos.z, winchWorldPos.y + 1.5f);
		}
		HoldZones.Register("winch", winchWorldPos, 2.4f);
		game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		InteractionService interactionService = UnityEngine.Object.FindObjectOfType<InteractionService>();
		if (interactionService != null)
		{
			interactionService.Register(new WinchInteractable(this));
		}
		GameObject gameObject3 = new GameObject("Scene_M3_潮汐船坞齿轮绞盘_总成");
		gameObject3.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject3.transform.position = winchWorldPos;
		Vector3 vector3 = new Vector3(crossingDir.x, 0f, crossingDir.z);
		gameObject3.transform.rotation = ((vector3.sqrMagnitude > 0.001f) ? Quaternion.LookRotation(vector3.normalized, Vector3.up) : Quaternion.Euler(0f, 40f, 0f));
		winchRoot = gameObject3.transform;
		if (world != null)
		{
			world.AddCollider(new EggboyCollider
			{
				x = winchWorldPos.x,
				z = winchWorldPos.z,
				r = 0.1f,
				top = winchWorldPos.y + 1.15f
			});
		}
		GameObject gameObject4 = TideTownStyleUI.BuildArticulatedMechanism3D("M3_潮汐船坞齿轮绞盘", 0.88f);
		gameObject4.transform.SetParent(winchRoot, worldPositionStays: false);
		gameObject4.transform.localPosition = new Vector3(0f, 0.78f, 0f);
		winchRing = TideTownStyleUI.FindDeepChild(gameObject4.transform, "Ring_0");
		winchSpindle = TideTownStyleUI.FindDeepChild(gameObject4.transform, "Core_Spindle");
		GameObject gameObject5 = TideTownStyleUI.CloneSceneLux3D("D6_救生圈_v2", 0.55f);
		if (gameObject5 != null)
		{
			gameObject5.transform.SetParent(winchRoot, worldPositionStays: false);
			GameObject gameObject6 = GameObject.Find("winch_stone_footing");
			bool flag = gameObject6 != null && gameObject6.GetComponent<MeshRenderer>().bounds.Contains(winchWorldPos + Vector3.up * 0.4f);
			gameObject5.transform.localPosition = new Vector3(0f, flag ? 0.34f : 0.22f, 0f);
			if (flag)
			{
				gameObject5.transform.position += new Vector3(-0.08f, 0f, -0.44f);
			}
		}
		GameObject gameObject7 = new GameObject("Scene_DockPontoonBridge");
		gameObject7.transform.SetParent(base.transform, worldPositionStays: false);
		pontoonBaseY = crossingChannelCenter.y;
		gameObject7.transform.position = new Vector3(crossingChannelCenter.x, pontoonBaseY - 1.35f, crossingChannelCenter.z);
		gameObject7.transform.rotation = Quaternion.LookRotation(crossingDir, Vector3.up);
		pontoonBridgeRoot = gameObject7.transform;
		GameObject gameObject8 = TideTownStyleUI.CloneSceneLux3D("S8_木码头_分享包版", 2.3f);
		if (gameObject8 != null)
		{
			gameObject8.transform.SetParent(pontoonBridgeRoot, worldPositionStays: false);
			gameObject8.transform.localPosition = Vector3.zero;
			gameObject8.transform.localRotation = Quaternion.identity;
			Bounds bounds = Lux3DModelFit.WorldBounds(gameObject8);
			Vector3 vector4 = pontoonBridgeRoot.InverseTransformVector(bounds.size);
			if (Mathf.Abs(vector4.x) > Mathf.Abs(vector4.z))
			{
				gameObject8.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
			}
		}
		int num11 = LayerMask.NameToLayer("Ground");
		gameObject7.layer = ((num11 >= 0) ? num11 : 6);
		BoxCollider boxCollider = gameObject7.AddComponent<BoxCollider>();
		boxCollider.center = new Vector3(0f, 0.2f, 0f);
		boxCollider.size = new Vector3(1f, 0.4f, 2.4f);
		boxCollider.enabled = false;
		GameObject gameObject9 = TideTownStyleUI.CloneSceneLux3D("D4O_船桨_v2", 1f);
		if (gameObject9 != null)
		{
			gameObject9.transform.SetParent(pontoonBridgeRoot, worldPositionStays: false);
			gameObject9.transform.localPosition = new Vector3(0.35f, 0.45f, 0.2f);
			gameObject9.transform.localRotation = Quaternion.Euler(8f, 15f, 80f);
		}
	}

	private void SpawnLighthouseSceneMechanism()
	{
		GameObject gameObject = GameObject.Find("LH_lamp");
		if (gameObject == null)
		{
			gameObject = GameObject.Find("LH_scope");
		}
		Vector3 vector = ((gameObject != null) ? gameObject.transform.position : new Vector3(22.18f, 16.98f, 3.3f));
		GameObject gameObject2 = TideTownStyleUI.BuildArticulatedMechanism3D("M4_守塔人黄铜三轴星盘", 0.78f);
		gameObject2.name = "Scene_M4_守塔人黄铜三轴星盘";
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject2.transform.position = vector + new Vector3(-0.92f, 0.45f, -0.42f);
		gameObject2.transform.rotation = Quaternion.Euler(0f, -120f, 0f);
		lighthouseMechRoot = gameObject2.transform;
		for (int i = 0; i < 3; i++)
		{
			lighthouseRings[i] = TideTownStyleUI.FindDeepChild(lighthouseMechRoot, "Ring_" + i);
		}
		if (world != null)
		{
			world.AddCollider(new EggboyCollider
			{
				x = gameObject2.transform.position.x,
				z = gameObject2.transform.position.z,
				r = 0.12f,
				top = vector.y + 1.2f,
				minY = vector.y - 0.5f
			});
		}
		GameObject gameObject3 = TideTownStyleUI.CloneSceneLux3D("B9_灯塔灯头_v4", 0.85f);
		if (gameObject3 != null)
		{
			gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
			gameObject3.transform.localPosition = new Vector3(0f, -0.68f, 0f);
		}
		wishPedestalPositions[2] = vector + new Vector3(0.95f, -0.35f, -0.85f);
	}

	private void SpawnStationWishPedestals()
	{
		string[] array = new string[3] { "WishPedestal_Bakery", "WishPedestal_School", "WishPedestal_Lighthouse" };
		for (int i = 0; i < 3; i++)
		{
			GameObject gameObject = new GameObject(array[i]);
			gameObject.transform.SetParent(base.transform, worldPositionStays: false);
			gameObject.transform.position = wishPedestalPositions[i];
			GameObject gameObject2 = TideTownStyleUI.CloneSceneLux3D("S6_路灯_分享包版", 0.65f);
			if (gameObject2 != null)
			{
				gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				gameObject2.transform.localScale = new Vector3(1.1f, 0.32f, 1.1f);
			}
			wishPedestals[i] = gameObject.transform;
			if (world != null)
			{
				Vector3 vector = wishPedestalPositions[i];
				world.AddCollider(new EggboyCollider
				{
					x = vector.x,
					z = vector.z,
					r = 0.12f,
					top = vector.y + 0.85f,
					minY = ((i == 2) ? (vector.y - 0.5f) : 0f)
				});
			}
		}
	}

	private void SpawnTidalDriftBottles()
	{
		Vector3[] array = new Vector3[3]
		{
			new Vector3(-7.2f, 1.4f, -3.5f),
			new Vector3(11.4f, 2.2f, -6.2f),
			winchWorldPos + new Vector3(-1.2f, 0f, -0.8f)
		};
		bool flag = world == null;
		if (world != null)
		{
			flag = TryBottleBankPosition(array[2], out var position);
			if (flag)
			{
				array[2] = position + Vector3.up * 0.05f;
			}
			else
			{
				Debug.LogWarning("DRIFTBOTTLE 绞盘附近找不到高潮可达的岸边");
			}
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (i == 2 && !flag)
			{
				continue;
			}
			Vector3 position2 = array[i];
			if (world != null && i != 2)
			{
				position2.y = world.GroundAt(position2.x, position2.z, position2.y);
			}
			GameObject gameObject = new GameObject("TidalDriftBottle_" + i);
			gameObject.transform.SetParent(base.transform, worldPositionStays: false);
			gameObject.transform.position = position2;
			GameObject gameObject2 = TideTownStyleUI.CloneSceneLux3D("A4_果酱罐_v2", 0.42f);
			if (gameObject2 != null)
			{
				gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				gameObject2.transform.localRotation = Quaternion.Euler(68f, (float)i * 55f, 15f);
				Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>(includeInactive: true);
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					componentsInChildren[j].enabled = false;
				}
			}
			GameObject obj = new GameObject("BottleGlow");
			obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
			obj.transform.localPosition = new Vector3(0f, 0.28f, 0f);
			Light light = obj.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = new Color32(251, 207, 142, byte.MaxValue);
			light.range = ((i == 2) ? 0.95f : 1.4f);
			light.intensity = ((i == 2) ? 0.28f : 0.44f);
			light.shadows = LightShadows.None;
			driftBottles.Add(gameObject.transform);
		}
	}

	private bool TryBottleBankPosition(Vector3 origin, out Vector3 position)
	{
		position = origin;
		float num = WaterLevel.LevelYs[3] + 0.15f;
		RampRoad rampRoad = FindShedRamp();
		List<EggboyCollider> list = new List<EggboyCollider>();
		float num2 = float.MaxValue;
		for (float num3 = -5.5f; num3 <= 5.5f; num3 += 0.25f)
		{
			for (float num4 = -5.5f; num4 <= 5.5f; num4 += 0.25f)
			{
				float num5 = num3 * num3 + num4 * num4;
				if (num5 >= num2)
				{
					continue;
				}
				Vector3 point = origin + new Vector3(num3, 0f, num4);
				if ((rampRoad != null && RampDistance(rampRoad, point) < 0.88f) || !world.InWalkArea(point.x, point.z))
				{
					continue;
				}
				float ground = world.GroundAt(point.x, point.z, 3.65f);
				if (!(ground < num) && !(ground > 3.48f))
				{
					world.QueryColliders(point.x, point.z, list);
					if (!list.Exists((EggboyCollider c) => BlocksBottleBank(c, point, ground, 0.31f)))
					{
						num2 = num5;
						position = new Vector3(point.x, ground, point.z);
					}
				}
			}
		}
		return num2 < float.MaxValue;
	}

	private static bool BlocksBottleBank(EggboyCollider obstacle, Vector3 point, float ground, float radius)
	{
		if (obstacle.water || obstacle.minY > ground + 0.04f || (obstacle.top.HasValue && obstacle.top.Value <= ground + 0.22f))
		{
			return false;
		}
		float num = point.x - obstacle.x;
		float num2 = point.z - obstacle.z;
		if (obstacle.r.HasValue)
		{
			float num3 = obstacle.r.Value + radius;
			return num * num + num2 * num2 < num3 * num3;
		}
		if (obstacle.hw.HasValue && obstacle.hd.HasValue && Mathf.Abs(num) < obstacle.hw.Value + radius)
		{
			return Mathf.Abs(num2) < obstacle.hd.Value + radius;
		}
		return false;
	}

	public Vector3 GetDogBowlPosition()
	{
		GameObject gameObject = GameObject.Find("S_bowl");
		if (!(gameObject != null))
		{
			return new Vector3(17.5f, 3.05f, -5.2f);
		}
		return gameObject.transform.position;
	}

	public Vector3 GetCrossingChannelCenter()
	{
		return crossingChannelCenter;
	}

	public bool GapEndpoints(out Vector3 a, out Vector3 b)
	{
		float y = WaterLevel.StaticY + 0.18f;
		a = new Vector3(gapA.x, y, gapA.z);
		b = new Vector3(gapB.x, y, gapB.z);
		return hasGapEnds;
	}

	public Transform GetNearestWishPedestal(Vector3 pos, out float dist)
	{
		Transform result = wishPedestals[0];
		dist = float.MaxValue;
		for (int i = 0; i < wishPedestals.Length; i++)
		{
			if (!(wishPedestals[i] == null))
			{
				float num = Vector3.Distance(pos, wishPedestals[i].position);
				if (num < dist)
				{
					dist = num;
					result = wishPedestals[i];
				}
			}
		}
		return result;
	}

	public bool TryEchoAssistWinch(Vector3 actorPos)
	{
		if (winchRoot == null)
		{
			return false;
		}
		if (Vector3.Distance(actorPos, winchWorldPos) < 12f)
		{
			RaiseDockWinch(byEcho: true);
			return true;
		}
		return false;
	}

	public void DebugResetCrossing()
	{
		winchRaised = false;
		boatBridge = false;
		if (state != null)
		{
			state.flags.Remove("dock_winch_raised");
		}
	}

	public void BridgeGapWithBoat(string achievementId)
	{
		winchRaised = true;
		boatBridge = true;
		if (state != null && !state.flags.Contains("dock_winch_raised"))
		{
			state.flags.Add("dock_winch_raised");
		}
		ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.25f);
		if (!string.IsNullOrEmpty(achievementId))
		{
			TideTownAchievementSystem.Unlock(achievementId);
		}
	}

	private void WinchTick(float dt, bool playerHolds, bool echoHolds)
	{
		WinchHolders = (playerHolds ? 1 : 0) + (echoHolds ? 1 : 0);
		if (winchRaised || !GapActive)
		{
			winchWiggle = 0f;
			winchStrain = 0f;
		}
		else if (WinchHolders >= 2)
		{
			winchStrain = 0f;
			winchWiggle = 0f;
			winchCrank += dt;
			winchSpinAngle += 260f * dt;
			if (Time.time >= winchCreakAt)
			{
				winchCreakAt = Time.time + 0.35f;
				ShadowAndChimeSystem.PlayProceduralChime(330f + winchCrank * 90f, 0.12f);
			}
			if (winchCrank >= ((director != null && director.IsAuditMode) ? 2.5f : 0.75f))
			{
				RaiseDockWinch(byEcho: true);
			}
		}
		else if (WinchHolders == 1)
		{
			winchStrain += dt;
			winchCrank = Mathf.Max(0f, winchCrank - dt * 2f);
			winchWiggle = Mathf.Sin(Time.time * 9f) * 7f;
			if (Time.time >= winchCreakAt)
			{
				winchCreakAt = Time.time + 0.9f;
				ShadowAndChimeSystem.PlayProceduralChime(155f, 0.2f);
			}
			if (winchStrain > 1.2f && playerHolds && game != null && !strainFaceShown)
			{
				strainFaceShown = true;
				game.SetExpression(ExprId.nervous);
			}
		}
		else
		{
			winchStrain = 0f;
			winchCrank = Mathf.Max(0f, winchCrank - dt * 2f);
			winchWiggle = Mathf.Lerp(winchWiggle, 0f, dt * 8f);
			strainFaceShown = false;
		}
	}

	public void RaiseDockWinch(bool byEcho, bool byWishBoat = false)
	{
		winchRaised = true;
		if (byWishBoat)
		{
			boatBridge = true;
		}
		winchSpinAngle += 720f;
		if (state != null && !state.flags.Contains("dock_winch_raised"))
		{
			state.flags.Add("dock_winch_raised");
		}
		ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.25f);
		ShadowAndChimeSystem.PlayProceduralChime(880f, 0.4f);
		if (player != null)
		{
			player.PlayEmote("hop");
		}
		if (byWishBoat)
		{
			TideTownAchievementSystem.Unlock("wish_boat");
		}
		else if (byEcho)
		{
			TideTownAchievementSystem.Unlock("echo_coop");
			TideTownAchievementSystem.Unlock("winch_bridge");
		}
		else
		{
			TideTownAchievementSystem.Unlock("winch_bridge");
		}
	}

	private void Update()
	{
		float deltaTime = Time.deltaTime;
		if (deltaTime <= 0f)
		{
			return;
		}
		if (surgePenaltyCooldown > 0f)
		{
			surgePenaltyCooldown -= deltaTime;
		}
		bool flag = state != null && (state.flags.Contains("oven_lit") || state.flags.Contains("puzzle_bakery_lock"));
		AnimateRings(bakeryRings, flag ? 55f : 12f, deltaTime);
		bool flag2 = state != null && (state.flags.Contains("ball_placed") || state.flags.Contains("puzzle_school_lock"));
		AnimateRings(schoolRings, flag2 ? 60f : 10f, deltaTime);
		bool flag3 = state != null && (state.flags.Contains("beam_lit") || state.flags.Contains("puzzle_lighthouse_lock"));
		AnimateRings(lighthouseRings, flag3 ? 75f : 15f, deltaTime);
		if (winchRing != null)
		{
			float z = (winchRaised ? (0f - winchSpinAngle - Time.time * 18f) : (-15f - winchSpinAngle + winchWiggle));
			winchRing.localRotation = Quaternion.Slerp(winchRing.localRotation, Quaternion.Euler(0f, 0f, z), deltaTime * 6f);
		}
		if (winchSpindle != null)
		{
			float z2 = winchSpinAngle - winchWiggle;
			winchSpindle.localRotation = Quaternion.Slerp(winchSpindle.localRotation, Quaternion.Euler(0f, 0f, z2), deltaTime * 6f);
		}
		if (pontoonBridgeRoot != null)
		{
			bool flag4 = winchRaised && !boatBridge;
			float num = Mathf.Max(pontoonBaseY, WaterLevel.StaticY - 0.22f);
			float num2 = (flag4 ? (Mathf.Sin(Time.time * 2.1f) * 0.02f) : 0f);
			float y = (flag4 ? (num + num2) : (pontoonBaseY - 1.35f));
			Vector3 b = new Vector3(crossingChannelCenter.x, y, crossingChannelCenter.z);
			pontoonBridgeRoot.position = Vector3.Lerp(pontoonBridgeRoot.position, b, deltaTime * 3.5f);
			bool gapActive = GapActive;
			foreach (FloatingCrate gapCrate in gapCrates)
			{
				if (gapCrate != null)
				{
					gapCrate.suppressed = gapActive;
				}
			}
			if (pontoonColliders == null)
			{
				pontoonColliders = pontoonBridgeRoot.GetComponentsInChildren<Collider>(includeInactive: true);
			}
			if (pontoonCollidersOn != flag4 || !pontoonCollidersInit)
			{
				pontoonCollidersInit = true;
				pontoonCollidersOn = flag4;
				Collider[] array = pontoonColliders;
				foreach (Collider collider in array)
				{
					if (collider != null)
					{
						collider.enabled = flag4;
					}
				}
			}
		}
		if (player != null && ui != null)
		{
			bool flag5 = ui.inputClose && !player.locked && !ui.TitleShowing && !ui.Paused && !ui.LetterOpen;
			bool playerHolds = Vector3.Distance(player.transform.position, winchWorldPos) < 2.4f && ((flag5 && EasyInputBridge.Held(KeyCode.E)) || DebugPlayerHoldsWinch);
			if (echoSys == null)
			{
				echoSys = UnityEngine.Object.FindObjectOfType<TidalEchoCloneSystem>();
			}
			bool echoHolds = echoSys != null && echoSys.EchoActive && (echoSys.EchoHoldZone == "winch" || Vector3.Distance(player.transform.position, winchWorldPos) < 3.6f || Vector3.Distance(echoSys.EchoPosition, winchWorldPos) < 3.6f);
			if (echoHolds && Vector3.Distance(player.transform.position, winchWorldPos) < 3.2f && flag5 && EasyInputBridge.Down(KeyCode.E)) winchCrank += 0.5f;
			WinchTick(deltaTime, playerHolds, echoHolds);
		}
		if (player == null || ui == null || !ui.inputClose || player.locked || ui.TitleShowing || ui.Paused || ui.LetterOpen || ui.Cinematic || MenuController.OverlayOpen || !EasyInputBridge.Down(KeyCode.E))
		{
			return;
		}
		for (int j = 0; j < driftBottles.Count; j++)
		{
			Transform transform = driftBottles[j];
			if (!(transform == null) && transform.gameObject.activeSelf && Vector3.Distance(player.transform.position, transform.position) < 1.45f)
			{
				transform.gameObject.SetActive(value: false);
				ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
				ui.ShowToast(driftBottleNotes[j % driftBottleNotes.Length], 5.5f);
				if (!ui.LetterOpen) TownVoiceover.Play("vo_bottle_" + j % driftBottleNotes.Length);
				TideTownAchievementSystem.Unlock("drift_bottles");
				break;
			}
		}
	}

	private static void AnimateRings(Transform[] rings, float speedDeg, float dt)
	{
		if (rings == null)
		{
			return;
		}
		for (int i = 0; i < rings.Length; i++)
		{
			if (!(rings[i] == null))
			{
				float num = ((i % 2 == 0) ? 1f : (-1.35f));
				rings[i].Rotate(0f, 0f, speedDeg * num * dt, Space.Self);
			}
		}
	}
}
