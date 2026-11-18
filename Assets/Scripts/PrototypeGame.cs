using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PrototypeGame : MonoBehaviour
{
	public float modelYawDegrees;

	private IEggboyWorld world;

	private EggboyPlayer player;

	private ChaseCamera chase;

	private EggboyFace face;

	private EggboyUI ui;

	private TownMap townMap;

	private bool gotLetter;

	private bool frontView;

	private bool autoWalk;

	private bool autoRun;

	private bool autoJump;

	private bool autoJumped;

	private bool autoLetter;

	private float captureAt = 5f;

	private ExprId baseExpr;

	private static readonly Dictionary<string, ExprId> EmoteExpr = new Dictionary<string, ExprId>
	{
		{
			"wave",
			ExprId.happy
		},
		{
			"nod",
			ExprId.smug
		},
		{
			"shake",
			ExprId.meh
		},
		{
			"hop",
			ExprId.excited
		},
		{
			"give",
			ExprId.happy
		},
		{
			"look",
			ExprId.curious
		},
		{
			"sad",
			ExprId.sad
		},
		{
			"pickup_mailbox",
			ExprId.curious
		},
		{
			"read_letter",
			ExprId.thinking
		},
		{
			"pickup_drop",
			ExprId.happy
		},
		{
			"pickup_matches",
			ExprId.happy
		},
		{
			"pickup_wood",
			ExprId.happy
		},
		{
			"load_wood",
			ExprId.happy
		},
		{
			"hold_light_stove",
			ExprId.thinking
		},
		{
			"light_stove_done",
			ExprId.excited
		},
		{
			"inspect_memory",
			ExprId.curious
		},
		{
			"inspect_slipper",
			ExprId.curious
		},
		{
			"pickup_ball",
			ExprId.happy
		},
		{
			"put_ball_bowl",
			ExprId.surprised
		},
		{
			"pet_dog",
			ExprId.excited
		},
		{
			"pickup_oil",
			ExprId.happy
		},
		{
			"open_lighthouse_door",
			ExprId.curious
		},
		{
			"hold_pour_oil",
			ExprId.thinking
		},
		{
			"signal_wave",
			ExprId.happy
		},
		{
			"ending_desk",
			ExprId.surprised
		}
	};

	private static readonly (KeyCode key, string emote, int repeat)[] Emotes = new(KeyCode, string, int)[5]
	{
		(KeyCode.Alpha1, "wave", 2),
		(KeyCode.Alpha2, "nod", 1),
		(KeyCode.Alpha3, "shake", 1),
		(KeyCode.Alpha4, "look", 1),
		(KeyCode.Alpha5, "sad", int.MaxValue)
	};

	private bool frameworkMode;

	private bool movementRunning;

	private TidalWindChallenge wind;

	private StoryDirector story;

	public bool introHold;

	public static bool DefaultRun
	{
		get
		{
			return PlayerPrefs.GetInt("tidetown.defaultrun", 1) == 1;
		}
		set
		{
			PlayerPrefs.SetInt("tidetown.defaultrun", value ? 1 : 0);
			PlayerPrefs.Save();
		}
	}

	public ExprId BaseExpression => baseExpr;

	public static bool ResolveRun(bool defaultRun, bool shiftHeld, bool windActive)
	{
		if (!(windActive && shiftHeld))
		{
			if (!defaultRun)
			{
				return shiftHeld;
			}
			return !shiftHeld;
		}
		return false;
	}

	private void Start()
	{
		Application.runInBackground = true;
		frameworkMode = UnityEngine.Object.FindObjectOfType<GameBootstrap>() != null;
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i + 1 < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == "-expr" && Enum.TryParse<ExprId>(commandLineArgs[i + 1], ignoreCase: true, out var result))
			{
				baseExpr = result;
			}
		}
		TownWorld townWorld = UnityEngine.Object.FindObjectOfType<TownWorld>();
		IslandWorld islandWorld = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		IEggboyWorld eggboyWorld2;
		if (!(islandWorld != null))
		{
			if (!(townWorld != null))
			{
				IEggboyWorld eggboyWorld = base.gameObject.AddComponent<EggboyWorld>();
				eggboyWorld2 = eggboyWorld;
			}
			else
			{
				IEggboyWorld eggboyWorld = townWorld;
				eggboyWorld2 = eggboyWorld;
			}
		}
		else
		{
			IEggboyWorld eggboyWorld = islandWorld;
			eggboyWorld2 = eggboyWorld;
		}
		world = eggboyWorld2;
		GameObject gameObject = Resources.Load<GameObject>("Models/eggboy");
		if (gameObject == null)
		{
			Debug.LogError("找不到 Resources/Models/eggboy");
			base.enabled = false;
			return;
		}
		GameObject gameObject2 = new GameObject("EGGBOY");
		player = gameObject2.AddComponent<EggboyPlayer>();
		player.disableAnim = Array.Exists(Environment.GetCommandLineArgs(), (string a) => a == "-noanim");
		player.debugPose = Array.Exists(Environment.GetCommandLineArgs(), (string a) => a == "-debugpose");
		player.antiClipOn = !Array.Exists(Environment.GetCommandLineArgs(), (string a) => a == "-noanticlip");
		player.Setup(UnityEngine.Object.Instantiate(gameObject), world, modelYawDegrees);
		if (player.pose == null)
		{
			base.enabled = false;
			return;
		}
		gameObject2.transform.position = world.SpawnPos;
		if (Array.Exists(commandLineArgs, (string a) => a == "-nearmail"))
		{
			gameObject2.transform.position = world.MailboxPos;
		}
		bool flag = townWorld != null && SceneManager.GetActiveScene().name == "TideTown_Expanded_Stage1";
		player.yaw = ((islandWorld != null && islandWorld.spawnMarker != null) ? (islandWorld.spawnMarker.eulerAngles.y * (MathF.PI / 180f)) : (flag ? (MathF.PI / 2f) : (-MathF.PI / 2f)));
		gameObject2.transform.rotation = Quaternion.Euler(0f, player.yaw * 57.29578f, 0f);
		face = new EggboyFace(player);
		SetExpression(baseExpr);
		player.onEmote = delegate(string e)
		{
			SetExpression(EmoteExpr.TryGetValue(e, out var value) ? value : ExprId.happy);
		};
		player.onEmoteEnd = delegate
		{
			SetExpression(baseExpr);
		};
		ui = base.gameObject.AddComponent<EggboyUI>();
		ui.Init(player, world);
		if (flag)
		{
			ui.SetGoal("目标：靠近山口的红色邮筒，按 E 取信");
		}
		if (townWorld != null)
		{
			townMap = base.gameObject.AddComponent<TownMap>();
			townMap.Init(player, ui);
		}
		ui.OnExpressionPicked = delegate(ExprId id)
		{
			baseExpr = id;
			player.idle = 0f;
			SetExpression(id);
		};
		ui.OnLetterClosed = delegate
		{
			player.locked = false;
			player.PlayEmote("nod");
			ui.SetGoal("目标：去面包店（下一步还没做，先随便逛逛）");
		};
		ui.SetActiveExpression(baseExpr);
		chase = Camera.main.gameObject.AddComponent<ChaseCamera>();
		if (islandWorld != null)
		{
			chase.extraBlock = islandWorld.CameraBlockDistance;
		}
		bool flag2 = (frontView = Array.Exists(commandLineArgs, (string a) => a == "-frontview"));
		autoWalk = Array.Exists(commandLineArgs, (string a) => a == "-autowalk");
		autoJump = Array.Exists(commandLineArgs, (string a) => a == "-autojump");
		autoLetter = Array.Exists(commandLineArgs, (string a) => a == "-letter");
		for (int j = 0; j + 1 < commandLineArgs.Length; j++)
		{
			if (commandLineArgs[j] == "-captureat")
			{
				float.TryParse(commandLineArgs[j + 1], out captureAt);
			}
		}
		chase.SnapBehind(gameObject2.transform.position, player.yaw + (flag2 ? MathF.PI : 0f));
		if (Array.Exists(commandLineArgs, (string a) => a == "-overview"))
		{
			chase.enabled = false;
			Transform obj = Camera.main.transform;
			obj.position = new Vector3(0f, 78f, 118f);
			obj.LookAt(new Vector3(0f, 0f, -10f));
			Camera.main.fieldOfView = 50f;
			Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
			for (int k = 0; k < array.Length; k++)
			{
				array[k].enabled = false;
			}
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-autotest"))
		{
			autoWalk = true;
			player.yaw = MathF.PI / 2f;
			gameObject2.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
			chase.SnapBehind(gameObject2.transform.position, MathF.PI / 2f);
			base.gameObject.AddComponent<TideTownAutoTest>().Begin(player);
		}
		if (Array.Exists(commandLineArgs, GameBootstrap.IsRouteAuditFlag))
		{
			if (world is IslandWorld)
			{
				base.gameObject.AddComponent<RouteAudit2>().Begin(player, this);
			}
			else
			{
				base.gameObject.AddComponent<ExpandedRouteAudit>().Begin(player, this);
			}
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-townrun"))
		{
			autoWalk = true;
			autoRun = true;
			player.yaw = MathF.PI / 2f;
			gameObject2.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
			chase.SnapBehind(gameObject2.transform.position, MathF.PI / 2f);
			TideTownAutoTest tideTownAutoTest = base.gameObject.AddComponent<TideTownAutoTest>();
			tideTownAutoTest.duration = 80f;
			tideTownAutoTest.Begin(player);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-docktest"))
		{
			autoWalk = true;
			gameObject2.transform.position = new Vector3(44f, 0f, 13f);
			player.yaw = 0f;
			gameObject2.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
			chase.SnapBehind(gameObject2.transform.position, 0f);
			TideTownAutoTest tideTownAutoTest2 = base.gameObject.AddComponent<TideTownAutoTest>();
			tideTownAutoTest2.duration = 30f;
			tideTownAutoTest2.Begin(player);
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-dockview"))
		{
			chase.enabled = false;
			Transform obj2 = Camera.main.transform;
			obj2.position = new Vector3(28f, 14f, 44f);
			obj2.LookAt(new Vector3(44f, 4f, 28f));
			Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
			for (int k = 0; k < array.Length; k++)
			{
				array[k].enabled = false;
			}
		}
		if (autoLetter)
		{
			StartCoroutine(DebugLetter());
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-capture"))
		{
			StartCoroutine(CaptureAfter(captureAt));
		}
		if (Array.Exists(commandLineArgs, (string a) => a == "-showcase"))
		{
			StartCoroutine(ShowcaseRoutine());
		}
	}

	private IEnumerator ShowcaseRoutine()
	{
		yield return new WaitForSeconds(1.2f);
		frontView = true;
		chase.SnapBehind(player.transform.position, player.yaw + MathF.PI * 39f / 50f);
		string shotDir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(shotDir);
		(string clip, string title, EggboyPlayer.CarryPose carry, float shotTime, string shotFile)[] items = new(string, string, EggboyPlayer.CarryPose, float, string)[8]
		{
			("pickup_wood", "【演示 1/8】抱起柴火捆 (pickup_wood.anim + 0.44s 抓握 + 抱柴姿态)", EggboyPlayer.CarryPose.Wood, 0.46f, "showcase_1_pickup_wood.png"),
			("load_wood", "【演示 2/8】双手塞柴入炉 (load_wood.anim)", EggboyPlayer.CarryPose.None, 0.5f, "showcase_2_load_wood.png"),
			("hold_light_stove", "【演示 3/8】长按擦火柴蓄力 (hold_light_stove.anim)", EggboyPlayer.CarryPose.None, 0.35f, "showcase_3_hold_stove.png"),
			("light_stove_done", "【演示 4/8】点火成功欢呼 (light_stove_done.anim)", EggboyPlayer.CarryPose.None, 0.5f, "showcase_4_light_done.png"),
			("inspect_slipper", "【演示 5/8】弯腰拿起端详拖鞋 (inspect_slipper.anim)", EggboyPlayer.CarryPose.None, 0.5f, "showcase_5_slipper.png"),
			("pickup_ball", "【演示 6/8】蹲下捧起红球 (pickup_ball.anim + 捧球姿态)", EggboyPlayer.CarryPose.Ball, 0.42f, "showcase_6_ball.png"),
			("pet_dog", "【演示 7/8】蹲身抚摸小黑 (pet_dog.anim)", EggboyPlayer.CarryPose.None, 0.48f, "showcase_7_pet_dog.png"),
			("signal_wave", "【演示 8/8】灯塔高举右手挥手 (signal_wave.anim)", EggboyPlayer.CarryPose.None, 0.48f, "showcase_8_wave.png")
		};
		bool savedShots = false;
		while (true)
		{
			(string clip, string title, EggboyPlayer.CarryPose carry, float shotTime, string shotFile)[] array = items;
			for (int i = 0; i < array.Length; i++)
			{
				(string clip, string title, EggboyPlayer.CarryPose carry, float shotTime, string shotFile) it = array[i];
				chase.SnapBehind(player.transform.position, player.yaw + MathF.PI * 39f / 50f);
				if (ui != null)
				{
					ui.ShowToast(it.title, 2.4f);
				}
				player.SetCarryPose(EggboyPlayer.CarryPose.None);
				player.PlayInteractAction(it.clip, 1.1f, 0.42f, delegate
				{
					player.SetCarryPose(it.carry);
				});
				yield return new WaitForSeconds(it.shotTime);
				if (!savedShots && !string.IsNullOrEmpty(it.shotFile))
				{
					ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, it.shotFile));
				}
				yield return new WaitForSeconds(Mathf.Max(0.8f, 2.2f - it.shotTime));
			}
			savedShots = true;
		}
	}

	private IEnumerator DebugLetter()
	{
		yield return new WaitForSeconds(1.2f);
		gotLetter = true;
		if (ui != null)
		{
			ui.SetLetterTaken(taken: true);
		}
		player.locked = true;
		SetExpression(ExprId.thinking);
		if (ui != null)
		{
			ui.ShowLetter();
		}
	}

	private IEnumerator CaptureAfter(float seconds)
	{
		yield return new WaitForSeconds(seconds);
		string text = Path.Combine(Application.dataPath, "..", "capture.png");
		ScreenCapture.CaptureScreenshot(text, 1);
		Debug.Log("CAPTURE " + text);
		yield return new WaitForSeconds(1f);
		Application.Quit();
	}

	private void Update()
	{
		if (player == null || player.pose == null || chase == null || (ui != null && (ui.LetterOpen || ui.Paused || ui.TitleShowing)) || (townMap != null && townMap.IsOpen) || MenuController.OverlayOpen)
		{
			return;
		}
		float dt = Mathf.Min(0.05f, Time.deltaTime);
		float num = ((EasyInputBridge.Held(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f) - ((EasyInputBridge.Held(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f);
		float num2 = ((EasyInputBridge.Held(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ? 1f : 0f) - ((EasyInputBridge.Held(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ? 1f : 0f);
		Vector3 forward = chase.Forward;
		Vector3 right = chase.Right;
		if (wind == null)
		{
			wind = UnityEngine.Object.FindObjectOfType<TidalWindChallenge>();
		}
		bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool run = ResolveRun(DefaultRun, shiftHeld, wind != null && wind.IsGustActive);
		if (autoWalk)
		{
			num = 1f;
			num2 = 0f;
			run = autoRun;
		}
		movementRunning = run;
		bool jumpPressed = EasyInputBridge.Down(KeyCode.Space);
		if (autoJump && !autoJumped && Time.time > 1f)
		{
			jumpPressed = true;
			autoJumped = true;
		}
		if (EmoteWheel.IsOpen)
		{
			player.Move(dt, 0f, 0f, run: false, jumpPressed: false);
			return;
		}
		if (Input.GetKeyDown(KeyCode.F2))
		{
			frontView = !frontView;
			chase.SnapBehind(player.transform.position, player.yaw + (frontView ? MathF.PI : 0f));
		}
		if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X))
		{
			int num3 = Array.IndexOf(EggboyExpressions.All, baseExpr);
			int num4 = EggboyExpressions.All.Length;
			baseExpr = EggboyExpressions.All[(num3 + (Input.GetKeyDown(KeyCode.X) ? 1 : (num4 - 1)) + num4) % num4];
			SetExpression(baseExpr);
			if (ui != null)
			{
				ui.SetActiveExpression(baseExpr);
			}
		}
		(KeyCode, string, int)[] emotes = Emotes;
		for (int i = 0; i < emotes.Length; i++)
		{
			(KeyCode, string, int) tuple = emotes[i];
			if (Input.GetKeyDown(tuple.Item1))
			{
				player.PlayEmote(tuple.Item2, tuple.Item3);
			}
		}
		if (!frameworkMode && EasyInputBridge.Down(KeyCode.E) && Vector3.Distance(world.MailboxPos, player.transform.position) < 1.4f && !gotLetter)
		{
			gotLetter = true;
			if (ui != null)
			{
				ui.SetLetterTaken(taken: true);
			}
			player.locked = true;
			player.FaceTowards(world.MailboxPos);
			player.PlayEmote("give");
			StartCoroutine(ShowLetterAfter(1.1f));
		}
		player.Move(dt, forward.x * num + right.x * num2, forward.z * num + right.z * num2, run, jumpPressed);
		if (!introHold && player.CurrentEmote == null)
		{
			if (player.idle > 14f && player.pose.expr != ExprId.sleepy)
			{
				SetExpression(ExprId.sleepy);
			}
			else if (player.idle == 0f && player.pose.expr == ExprId.sleepy && baseExpr != ExprId.sleepy)
			{
				SetExpression(baseExpr);
			}
		}
	}

	private IEnumerator ShowLetterAfter(float seconds)
	{
		yield return new WaitForSeconds(seconds);
		SetExpression(ExprId.thinking);
		if (ui != null)
		{
			ui.ShowLetter();
		}
	}

	public void SetBaseExpression(ExprId id)
	{
		baseExpr = id;
		if (player != null)
		{
			player.idle = 0f;
		}
		SetExpression(id);
		if (ui != null)
		{
			ui.SetActiveExpression(id);
		}
	}

	public void SetExpression(ExprId id)
	{
		if (player != null && player.pose != null)
		{
			player.pose.SetExpression(id);
		}
		if (face != null)
		{
			face.Set(id);
		}
	}

	private void LateUpdate()
	{
		if (!(player == null) && !(chase == null))
		{
			float dt = Mathf.Min(0.05f, Time.deltaTime);
			if (face != null)
			{
				face.Update(dt, Camera.main);
			}
			bool flag = EasyInputBridge.Held(KeyCode.W) || EasyInputBridge.Held(KeyCode.A) || EasyInputBridge.Held(KeyCode.S) || EasyInputBridge.Held(KeyCode.D) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
			if (story == null)
			{
				story = UnityEngine.Object.FindObjectOfType<StoryDirector>();
			}
			bool flag2 = chase.enabled && (townMap == null || !townMap.IsOpen) && (ui == null || (!ui.LetterOpen && !ui.Paused && !ui.TitleShowing)) && !MenuController.OverlayOpen;
			chase.SetFadeActive(flag2 && (ui == null || !ui.Cinematic) && !frontView && !introHold && (story == null || (!story.Cutscene && !story.Climbing && !story.Ended)));
			if (flag2)
			{
				chase.Tick(dt, player.camFocus, player.transform.eulerAngles.y * (MathF.PI / 180f), flag && !frontView && !player.locked, movementRunning);
			}
		}
	}
}
