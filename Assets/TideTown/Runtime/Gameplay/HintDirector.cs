using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class HintDirector : MonoBehaviour
{
	private sealed class Node
	{
		public string id;

		public Func<bool> active;

		public Func<Vector3> center;

		public float radius;

		public Func<string> progress;

		public Func<Vector3> clue;

		public Func<string> letterId;

		public Func<string> phrase;

		public Func<string> tell;
	}

	private static readonly float[] TierSeconds = new float[4] { 45f, 90f, 150f, 240f };

	private static readonly string[] RateNames = new string[3] { "常规", "少", "关" };

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private StoryDirector director;

	private GameState state;

	private PrototypeGame game;

	private DogFollower dog;

	private WorldMechanismDirector mech;

	private readonly List<Node> nodes = new List<Node>();

	private Node current;

	private string lastProgress = "";

	private GameObject beacon;

	private Light beaconLight;

	private bool tutorialShown;

	private readonly Dictionary<string, Vector3> bakery = new Dictionary<string, Vector3>();

	private readonly Dictionary<string, Vector3> school = new Dictionary<string, Vector3>();

	private Vector3 oilPos;

	private Vector3 doorPos;

	private Vector3 lampPos;

	public static HintDirector Instance { get; private set; }

	public string LetterHintId { get; private set; } = "";


	public string LetterHintPhrase { get; private set; } = "";


	public string CurrentNodeId { get; private set; } = "";


	public int CurrentTier { get; private set; }

	public float StuckSeconds { get; private set; }

	public int Rate { get; private set; }

	private bool PlayerFree
	{
		get
		{
			if (ui != null && ui.inputClose && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen && !player.locked && !ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen && !AvantGardeGameplayHub.AnyModalOpen && !AIAgentCalibrationDirector.IsOpen)
			{
				if (!(director == null))
				{
					if (!director.Cutscene)
					{
						return !director.Climbing;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
	{
		Instance = this;
		player = p;
		ui = u;
		world = w;
		director = d;
		state = s;
		game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		mech = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
		Rate = Mathf.Clamp(PlayerPrefs.GetInt("tidetown.hintrate", 0), 0, 2);
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i + 1 < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == "-hints")
			{
				Rate = ((commandLineArgs[i + 1] == "off") ? 2 : ((commandLineArgs[i + 1] == "less") ? 1 : 0));
			}
		}
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			bakery[bakeryItem.id] = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
		}
		SchoolItem[] items = SchoolData.Load().items;
		foreach (SchoolItem schoolItem in items)
		{
			school[schoolItem.id] = new Vector3(schoolItem.x, schoolItem.y, schoolItem.z);
		}
		LighthouseList lighthouseList = LighthouseData.Load();
		LighthouseItem[] items2 = lighthouseList.items;
		foreach (LighthouseItem lighthouseItem in items2)
		{
			if (lighthouseItem.id == "oil")
			{
				oilPos = new Vector3(lighthouseItem.x, lighthouseItem.y, lighthouseItem.z);
			}
		}
		doorPos = LighthouseData.Point(lighthouseList.door);
		lampPos = LighthouseData.Point(lighthouseList.lamp);
		BuildNodes();
	}

	private bool F(string flag)
	{
		return state.flags.Contains(flag);
	}

	private Vector3 B(string id)
	{
		if (!bakery.TryGetValue(id, out var value))
		{
			return Vector3.zero;
		}
		return value;
	}

	private Vector3 S(string id)
	{
		if (!school.TryGetValue(id, out var value))
		{
			return Vector3.zero;
		}
		return value;
	}

	private void BuildNodes()
	{
		nodes.Add(new Node
		{
			id = "bakery_fire",
			active = () => state.step.StartsWith("B") && state.step != "B0_arrive" && !F("oven_lit"),
			center = () => B("stove"),
			radius = 16f,
			progress = () => state.step + F("matches_taken") + F("wood_loaded") + F("wood_wet") + state.carrying + state.seenMemories.Count,
			clue = () => F("matches_taken") ? ((F("wood_loaded") && !F("wood_wet")) ? B("stove") : B("wood")) : B("stall"),
			letterId = () => (!F("wood_wet")) ? "lin_oven" : "ying_dog",
			phrase = () => (!F("wood_wet")) ? (F("wood_loaded") ? "再生一次火" : "后院的木柴") : "晒干了",
			tell = () => F("matches_taken") ? (F("wood_loaded") ? ((!F("wood_wet")) ? "炉子的风门锁着。看看店里的菜谱、台秤、面包篮，背面各有一个刻度。" : "炉膛里的柴是湿的。去后院抱一捆干的来换；或者翻翻阿萤的信，那里有个「干」字。") : "后院的柴堆可以抱一捆柴来，塞进炉膛。") : "门前的摊位上有一盒火柴。"
		});
		nodes.Add(new Node
		{
			id = "school_dog",
			active = () => (state.step == "S1_slipper" || state.step == "S2_ball" || state.step == "S3_bowl") && !F("ball_in_bowl"),
			center = () => S("bowl"),
			radius = 22f,
			progress = () => state.step + state.carrying + F("slipper_seen") + state.seenMemories.Count,
			clue = () => (!(state.step == "S1_slipper")) ? ((!(state.carrying == "ball")) ? S("ball") : S("bowl")) : S("slipper"),
			letterId = () => "ying_dog",
			phrase = () => (!(state.carrying == "ball")) ? "最喜欢的球" : "放回了学校后门",
			tell = () => (!(state.step == "S1_slipper")) ? ((!(state.carrying == "ball")) ? "操场的秋千下面有一颗红球。也可以按 V 给小黑许一样它爱吃的。" : "把球带回学校后门的狗碗那里。") : "后门外有一只被咬过的拖鞋，爪印从那里一直通到操场。"
		});
		nodes.Add(new Node
		{
			id = "crossing",
			active = () => mech != null && mech.GapActive && !mech.CrossingSolved && state.step == "L0_cross" && WaterLevel.StaticY >= WaterLevel.LevelYs[2] - 0.05f,
			center = () => mech.GetCrossingChannelCenter(),
			radius = 12f,
			progress = () => state.step + mech.CrossingSolved,
			clue = () => mech.WinchPosition,
			letterId = () => "chen_lighthouse",
			phrase = () => "船棚里",
			tell = () => "岸边的绞盘连着水下的旧栈桥，按 E 摇一摇。也可以把信里的「木柴」和「船」拼在一起扔过去，或者按 V 许一条船。"
		});
		nodes.Add(new Node
		{
			id = "lighthouse",
			active = () => (state.step == "L1_shed" || state.step == "L2_door" || state.step == "L3_oil" || state.step == "L4_light") && !F("beam_lit"),
			center = () => (!(state.step == "L1_shed")) ? doorPos : oilPos,
			radius = 20f,
			progress = () => state.step + state.carrying + F("lamp_oiled") + state.seenMemories.Count,
			clue = () => (!(state.step == "L1_shed")) ? ((!(state.step == "L2_door")) ? lampPos : doorPos) : oilPos,
			letterId = () => "chen_lighthouse",
			phrase = () => (!(state.step == "L1_shed")) ? "能不能再亮一次" : "半壶煤油",
			tell = () => (!(state.step == "L1_shed")) ? ((!(state.step == "L2_door")) ? ((!(state.step == "L3_oil")) ? "灯头的星盘锁着。塔顶的帽子、望远镜、航海日志上各有一个刻度。" : "给塔顶的灯头加油。") : "带着油壶走台阶上灯塔，在塔门按 E。") : "船棚的屋檐下有一只油壶。"
		});
	}

	private void Update()
	{
		if (state == null || player == null || ui == null)
		{
			return;
		}
		if (ui.Paused && Input.GetKeyDown(KeyCode.H))
		{
			Rate = (Rate + 1) % 3;
			PlayerPrefs.SetInt("tidetown.hintrate", Rate);
			ui.ShowToast("提示：" + RateNames[Rate], 2f);
			if (Rate == 2)
			{
				Reset();
			}
		}
		if (ui.inputClose)
		{
			Tick(Time.deltaTime, PlayerFree);
		}
	}

	public void DebugAdvance(float seconds)
	{
		Tick(seconds, playerFree: true);
	}

	private void Tick(float dt, bool playerFree)
	{
		if (ui != null && ui.FirstTutorialActive)
		{
			return;
		}
		Node node = null;
		if (Rate != 2)
		{
			foreach (Node node2 in nodes)
			{
				if (node2.active())
				{
					Vector3 vector = node2.center();
					Vector3 position = player.transform.position;
					if (new Vector2(position.x - vector.x, position.z - vector.z).magnitude <= node2.radius)
					{
						node = node2;
						break;
					}
				}
			}
		}
		if (node != current)
		{
			Reset();
			current = node;
			lastProgress = ((node != null) ? node.progress() : "");
		}
		if (current == null)
		{
			return;
		}
		string text = current.progress();
		if (text != lastProgress)
		{
			lastProgress = text;
			ResetTimerOnly();
			return;
		}
		if (!playerFree)
		{
			UpdateBeacon();
			return;
		}
		StuckSeconds += dt;
		CurrentNodeId = current.id;
		float num = ((Rate == 1) ? 2f : 1f);
		int num2 = 0;
		for (int i = 0; i < TierSeconds.Length; i++)
		{
			if (StuckSeconds >= TierSeconds[i] * num)
			{
				num2 = i + 1;
			}
		}
		while (CurrentTier < num2)
		{
			CurrentTier++;
			Fire(CurrentTier);
		}
		UpdateBeacon();
	}

	private void Fire(int tier)
	{
		Vector3 vector = current.clue();
		Debug.Log($"HINT node={current.id} tier={tier} stuck={StuckSeconds:F0}s");
		switch (tier)
		{
		case 1:
			if (game != null)
			{
				game.SetExpression(ExprId.thinking);
			}
			if (player.Speed < 0.2f)
			{
				player.FaceTowards(vector);
				player.PlayEmote("look");
			}
			break;
		case 2:
			if (game != null)
			{
				game.SetExpression(ExprId.curious);
			}
			ShowBeacon(vector);
			if (dog != null && dog.Following && dog.Visible)
			{
				dog.GuideTo(vector, 9f);
			}
			ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.25f);
			break;
		case 3:
			LetterHintId = current.letterId();
			LetterHintPhrase = current.phrase();
			ui.SetTutorial("Tab\u3000再读一遍信");
			tutorialShown = true;
			ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.25f);
			break;
		case 4:
			ui.SetTutorial(current.tell());
			tutorialShown = true;
			ShadowAndChimeSystem.PlayProceduralChime(523.25f, 0.3f);
			break;
		}
	}

	private void ShowBeacon(Vector3 at)
	{
		if (beacon == null)
		{
			beacon = new GameObject("HintBeacon");
			beaconLight = beacon.AddComponent<Light>();
			beaconLight.type = LightType.Point;
			beaconLight.color = new Color(1f, 0.82f, 0.45f);
			beaconLight.range = 3.2f;
			beaconLight.shadows = LightShadows.None;
		}
		beacon.SetActive(value: true);
		beacon.transform.position = at + Vector3.up * 0.6f;
	}

	private void UpdateBeacon()
	{
		if (!(beacon == null) && beacon.activeSelf && current != null)
		{
			beacon.transform.position = current.clue() + Vector3.up * 0.6f;
			beaconLight.intensity = 1.6f + Mathf.Sin(Time.unscaledTime * 3.2f) * 1.1f;
		}
	}

	private void ResetTimerOnly()
	{
		StuckSeconds = 0f;
		CurrentTier = 0;
		LetterHintId = "";
		LetterHintPhrase = "";
		if (beacon != null)
		{
			beacon.SetActive(value: false);
		}
		if (tutorialShown && ui != null)
		{
			ui.SetTutorial("");
			tutorialShown = false;
		}
	}

	private void Reset()
	{
		ResetTimerOnly();
		current = null;
		CurrentNodeId = "";
	}
}
