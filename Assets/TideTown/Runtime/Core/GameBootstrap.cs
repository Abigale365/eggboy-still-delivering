using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameBootstrap : MonoBehaviour
{
	public EggboyPlayer player;

	public EggboyUI ui;

	public IslandWorld world;

	public GameState state;

	public SaveStore store;

	public ControlGate gate;

	public InteractionService service;

	public StoryDirector director;

	public WaterLevel water;

	public IntroSequence intro;

	private static readonly string[] SkipIntroArgs = new string[20]
	{
		"-trailerRecord", "-audiocheck", "-noIntro", "-traversalaudit", "-wateraudit", "-scanaudit", "-innovaudit", "-lux3dlive", "-autotest", "-townrun", "-docktest", "-shot",
		"-overview", "-dockview", "-capture", "-mapaudit", "-showcase", "-closeupshowcase", "-chalkboardaudit", "-chalkboardrestore"
	};

	private bool initialized;

	internal static bool IsRouteAuditFlag(string arg)
	{
		if (!string.Equals(arg, "-routeaudit", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(arg, "-routeaudit2", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private void Start()
	{
		StartCoroutine(InitWhenReady());
	}

	private IEnumerator InitWhenReady()
	{
		for (int i = 0; i < 120; i++)
		{
			if (initialized)
			{
				break;
			}
			if (UnityEngine.Object.FindObjectOfType<EggboyPlayer>() != null && UnityEngine.Object.FindObjectOfType<EggboyUI>() != null)
			{
				Init();
			}
			if (!initialized)
			{
				yield return null;
			}
		}
	}

	private void Init()
	{
		if (initialized)
		{
			return;
		}
		player = UnityEngine.Object.FindObjectOfType<EggboyPlayer>();
		ui = UnityEngine.Object.FindObjectOfType<EggboyUI>();
		world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		if (player == null || ui == null)
		{
			return;
		}
		if (world != null)
		{
			RegisterLawnTreeTrunks();
		}
		if (Array.Exists(Environment.GetCommandLineArgs(), (string a) => a == "-resetsave"))
		{
			new SaveStore().Clear();
		}
		store = new SaveStore();
		state = store.Load();
		if (state == null)
		{
			state = new GameState();
			state.step = "P0_boot";
		}
		water = UnityEngine.Object.FindObjectOfType<WaterLevel>();
		if (water != null)
		{
			water.OnSettled = delegate(float y)
			{
				state.waterLevel = y;
				store.Save(state);
			};
			if (!water.CliOverride && state.waterLevel > 0.01f)
			{
				water.SetInstant(Mathf.Min(state.waterLevel, WaterLevel.MaxY));
			}
		}
		gate = new ControlGate();
		service = base.gameObject.AddComponent<InteractionService>();
		service.Init(player, ui, gate, () => state);
		director = base.gameObject.AddComponent<StoryDirector>();
		director.Init(player, ui, state, store, gate, water);
		player.gameObject.AddComponent<PhysicalPickupController>().Init(player, world, director);
		ui.legacyPrompt = false;
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		if (textAsset != null)
		{
			RouteList routeList = JsonUtility.FromJson<RouteList>(textAsset.text);
			if (routeList != null && routeList.route != null)
			{
				RouteStop[] route = routeList.route;
				foreach (RouteStop routeStop in route)
				{
					if (routeStop.stop && routeStop.id == "03")
					{
						float y2 = ((world != null) ? world.GroundAt(routeStop.x, routeStop.z) : 0f);
						service.Register(new MailboxInteractable(new Vector3(routeStop.x, y2, routeStop.z), director));
						break;
					}
				}
			}
		}
		BakeryEffects bakeryEffects = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			Vector3 p = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
			switch (bakeryItem.id)
			{
			case "stall":
				service.Register(new StallInteractable(p, director));
				continue;
			case "wood":
				service.Register(new WoodInteractable(p, director));
				continue;
			case "stove":
				service.Register(new StoveInteractable(p, director, bakeryEffects));
				continue;
			case "bell":
				service.Register(new BellInteractable(p, director, bakeryItem.label, bakeryItem.text));
				continue;
			}
			if (!string.IsNullOrEmpty(bakeryItem.text))
			{
				service.Register(new MemoryInteractable(p, director, bakeryItem.id, bakeryItem.label, bakeryItem.text));
			}
		}
		if (bakeryEffects != null && state.flags.Contains("oven_lit"))
		{
			bakeryEffects.Light();
		}
		SchoolList schoolList = SchoolData.Load();
		SchoolBall schoolBall = UnityEngine.Object.FindObjectOfType<SchoolBall>();
		SchoolBlackboard schoolBlackboard = null;
		SchoolItem[] items = schoolList.items;
		foreach (SchoolItem schoolItem in items)
		{
			Vector3 p2 = new Vector3(schoolItem.x, schoolItem.y, schoolItem.z);
			switch (schoolItem.id)
			{
			case "bowl":
				service.Register(new BowlInteractable(p2, director));
				break;
			case "ball":
				service.Register(new BallInteractable(p2, director));
				break;
			case "slipper":
				service.Register(new SlipperInteractable(p2, director));
				break;
			case "blackboard":
				schoolBlackboard = base.gameObject.AddComponent<SchoolBlackboard>();
				if (schoolBlackboard.Init(schoolItem, this))
				{
					service.Register(schoolBlackboard);
					break;
				}
				UnityEngine.Object.Destroy(schoolBlackboard);
				schoolBlackboard = null;
				service.Register(new MemoryInteractable(p2, director, schoolItem.id, schoolItem.label, schoolItem.text));
				break;
			default:
				if (!string.IsNullOrEmpty(schoolItem.text))
				{
					service.Register(new MemoryInteractable(p2, director, schoolItem.id, schoolItem.label, schoolItem.text));
				}
				break;
			}
		}
		if (schoolBall != null)
		{
			if (state.flags.Contains("ball_in_bowl"))
			{
				schoolBall.PlaceInBowl();
			}
			else if (state.carrying == "ball")
			{
				schoolBall.Take(player.transform);
			}
		}
		LighthouseList lighthouseList = LighthouseData.Load();
		LighthouseItem[] items2 = lighthouseList.items;
		foreach (LighthouseItem lighthouseItem in items2)
		{
			Vector3 p3 = new Vector3(lighthouseItem.x, lighthouseItem.y, lighthouseItem.z);
			if (lighthouseItem.id == "oil")
			{
				service.Register(new OilCanInteractable(p3, director));
			}
			else if (!string.IsNullOrEmpty(lighthouseItem.text))
			{
				service.Register(new MemoryInteractable(p3, director, lighthouseItem.id, lighthouseItem.label, lighthouseItem.text));
			}
		}
		service.Register(new LighthouseDoorInteractable(LighthouseData.Point(lighthouseList.door), director));
		service.Register(new LampHeadInteractable(LighthouseData.Point(lighthouseList.lamp), director));
		service.Register(new LighthouseDownInteractable(LighthouseData.Point(lighthouseList.deck), director));
		EndingList endingList = EndingData.Load();
		service.Register(new EndingDeskInteractable(EndingData.Point(endingList.desk), director));
		if (state.flags.Contains("beam_lit"))
		{
			SkyDimmer skyDimmer = UnityEngine.Object.FindObjectOfType<SkyDimmer>();
			if (skyDimmer != null)
			{
				skyDimmer.SetDimmedInstant();
			}
			BeaconLight beaconLight = UnityEngine.Object.FindObjectOfType<BeaconLight>();
			if (beaconLight != null)
			{
				beaconLight.TurnOnInstant();
			}
			FlashlightReply flashlightReply = UnityEngine.Object.FindObjectOfType<FlashlightReply>();
			if (flashlightReply != null)
			{
				flashlightReply.SetDoneInstant();
			}
		}
		if (state.flags.Contains("dawn_shown") || state.flags.Contains("ending_done"))
		{
			SkyDimmer skyDimmer2 = UnityEngine.Object.FindObjectOfType<SkyDimmer>();
			if (skyDimmer2 != null)
			{
				skyDimmer2.SetDawnInstant();
			}
		}
		if (state.flags.Contains("ending_done"))
		{
			CityGlow cityGlow = UnityEngine.Object.FindObjectOfType<CityGlow>();
			if (cityGlow != null)
			{
				cityGlow.Show();
			}
			ui.ShowSubtitle("所有的信，都送到了。");
			base.gameObject.AddComponent<EndingDispatch>();
			ui.SetGoal("");
		}
		DogFollower dogFollower = UnityEngine.Object.FindObjectOfType<DogFollower>();
		if (dogFollower != null)
		{
			dogFollower.Bind(player, world, state);
			if (state.flags.Contains("school_read"))
			{
				dogFollower.Follow();
			}
			else if (state.flags.Contains("ball_in_bowl"))
			{
				dogFollower.ShowAt(dogFollower.PeekPoint);
			}
			else
			{
				dogFollower.Hide();
			}
		}
		ScanStation scanStation = UnityEngine.Object.FindObjectOfType<ScanStation>();
		if (scanStation != null)
		{
			scanStation.SetEnabled(Features.scan);
			if (Features.scan)
			{
				service.Register(scanStation);
			}
		}
		TownMap townMap = base.gameObject.AddComponent<TownMap>();
		townMap.Init(player, ui, director, gate);
		base.gameObject.AddComponent<QuestGuide>().Init(ui, townMap, director, player);
		base.gameObject.AddComponent<MenuController>().Init(ui, townMap, director);
		base.gameObject.AddComponent<CollisionDebugView>();
		base.gameObject.AddComponent<TidalWindChallenge>().Init(player, ui, world, state);
		base.gameObject.AddComponent<AvantGardeGameplayHub>().Init(player, ui, world, director, state);
		base.gameObject.AddComponent<EmoteWheel>().Init(player, ui);
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		bool flag = Array.Exists(commandLineArgs, (string a) => IsRouteAuditFlag(a) || Array.IndexOf(SkipIntroArgs, a) >= 0);
		bool flag2 = Array.Exists(commandLineArgs, (string a) => a == "-storyaudit") || Array.Exists(commandLineArgs, (string a) => a == "-notitle");
		bool flag3 = state.step == "P0_boot";
		if ((!flag && !BootLoader.IsAutomatedLaunch()) || SpawnPlan.ForcedSpot() != null)
		{
			SpawnPlan.Apply(this);
		}
		if (flag3 && !flag && flag2)
		{
			intro = base.gameObject.AddComponent<IntroSequence>();
			intro.Begin(this);
		}
		else if (!flag && !flag2)
		{
			TitleScreen.Show(this, !flag3, flag3 ? ((Action)delegate
			{
				intro = base.gameObject.AddComponent<IntroSequence>();
				intro.Begin(this);
			}) : null);
		}
		bool flag4 = Array.Exists(commandLineArgs, (string a) => a == "-tutorial");
		if (!state.seenTutorials.Contains("basics_v1") && ((!flag && !BootLoader.IsAutomatedLaunch()) || flag4))
		{
			base.gameObject.AddComponent<BasicsTutorial>().Begin(this);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-traversalaudit"))
		{
			base.gameObject.AddComponent<TraversalAudit>().Begin(this);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-storyaudit"))
		{
			base.gameObject.AddComponent<StoryAudit>().Begin(this);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-mapaudit"))
		{
			base.gameObject.AddComponent<MapAudit>().Begin(this);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-wateraudit"))
		{
			base.gameObject.AddComponent<WaterAudit>().Begin(this);
		}
		if (Array.Exists(Environment.GetCommandLineArgs(), (string a) => a == "-scanaudit"))
		{
			base.gameObject.AddComponent<ScanAudit>().Begin(this);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-innovaudit"))
		{
			base.gameObject.AddComponent<InnovationAudit>().Begin(this);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-lux3dlive"))
		{
			base.gameObject.AddComponent<LiveLux3DCheck>().Begin(this);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-uishowcase") && flag3 && !flag)
		{
			base.gameObject.AddComponent<UiShowcase>().Begin(this);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-closeupshowcase"))
		{
			base.gameObject.AddComponent<CloseupShowcase>().Begin(this);
		}
		bool flag5 = Array.Exists(commandLineArgs, (string a) => a == "-chalkboardrestore");
		if (flag5 || Array.Exists(commandLineArgs, (string a) => a == "-chalkboardaudit"))
		{
			base.gameObject.AddComponent<BlackboardAudit>().Begin(this, schoolBlackboard, flag5);
		}
		initialized = true;
	}

	private void RegisterLawnTreeTrunks()
	{
		List<EggboyCollider> list = new List<EggboyCollider>();
		List<EggboyCollider> list2 = new List<EggboyCollider>();
		MeshRenderer[] array = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
		foreach (MeshRenderer meshRenderer in array)
		{
			if (!meshRenderer.enabled || !meshRenderer.name.EndsWith("LawnTree", StringComparison.Ordinal))
			{
				continue;
			}
			Vector3 center = meshRenderer.transform.position;
			if (world.InLawnArea(center.x, center.z) && !(Mathf.Abs(world.GroundAt(center.x, center.z) - center.y) > 0.2f) && Array.Exists(meshRenderer.sharedMaterials, (Material material) => material != null && material.name.StartsWith("NatureTrunk", StringComparison.Ordinal)))
			{
				Vector3 lossyScale = meshRenderer.transform.lossyScale;
				float num = 0.17f * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
				world.QueryColliders(center.x, center.z, list2);
				float trunkReach = num + 0.06f;
				if (!list2.Exists((EggboyCollider c) => c.r.HasValue && c.top.HasValue && c.top.Value > center.y + 0.25f && new Vector2(c.x - center.x, c.z - center.z).sqrMagnitude <= trunkReach * trunkReach))
				{
					list.Add(new EggboyCollider
					{
						x = center.x,
						z = center.z,
						r = num,
						minY = center.y - 0.15f
					});
				}
			}
		}
		foreach (EggboyCollider item in list)
		{
			world.AddCollider(item);
		}
		if (list.Count > 0)
		{
			Debug.Log($"LAWN_TRUNKS runtime={list.Count}");
		}
	}
}
