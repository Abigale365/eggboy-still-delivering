using System;
using System.Collections;
using System.IO;
using UnityEngine;

public sealed class BasicsTutorial : MonoBehaviour
{
	private sealed class Step
	{
		public string[] keys;

		public string text;

		public Func<bool> done;

		public float autoSeconds = -1f;

		public bool afterLetter;

		public Action onEnter;

		public Action onExit;
	}

	public const string SeenId = "basics_v1";

	private GameBootstrap boot;

	private EggboyUI ui;

	private EggboyPlayer player;

	private TownMap map;

	private ChaseCamera chase;

	private Step[] steps;

	private int index = -1;

	private float stepT;

	private float freeT;

	private float doneT = -1f;

	private float yawTurned;

	private float lastYaw;

	private float walked;

	private float shiftWalk;

	private float wheel;

	private Vector3 lastPos;

	private bool jumped;

	private bool mapOpened;

	private bool hPressed;

	private bool wheelOpened;

	private bool entered;

	private bool cardShown;

	public static bool Running { get; private set; }

	private bool Free
	{
		get
		{
			if (!ui.TitleShowing && !ui.Paused && !ui.LetterOpen && !ui.Cinematic && !player.locked && !MenuController.OverlayOpen)
			{
				if (!(boot.intro == null))
				{
					return boot.intro.Done;
				}
				return true;
			}
			return false;
		}
	}

	private void HideCard()
	{
		if (cardShown)
		{
			cardShown = false;
			ui.HideBasicsCard();
		}
	}

	public void Begin(GameBootstrap b)
	{
		boot = b;
		ui = b.ui;
		player = b.player;
		map = b.GetComponent<TownMap>();
		chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		bool flag = b.state.flags.Contains("letters_taken");
		string text = (PrototypeGame.DefaultRun ? "按住 Shift 慢慢走（松开就是跑）" : "按住 Shift 跑起来");
		steps = new Step[10]
		{
			new Step
			{
				keys = new string[1] { "鼠标" },
				text = "先点一下画面，再移动鼠标转动镜头",
				done = () => yawTurned > 60f
			},
			new Step
			{
				keys = new string[4] { "W", "A", "S", "D" },
				text = "走一走：W A S D（方向键也行）",
				done = () => walked > 4f
			},
			new Step
			{
				keys = new string[1] { "Shift" },
				text = text,
				done = () => shiftWalk > 0.6f
			},
			new Step
			{
				keys = new string[1] { "空格" },
				text = "按空格跳一下",
				done = () => jumped
			},
			new Step
			{
				keys = new string[1] { "滚轮" },
				text = "滚动鼠标滚轮，把镜头拉近拉远",
				done = () => wheel > 1.5f,
				autoSeconds = 10f
			},
			new Step
			{
				keys = null,
				text = "跟着地上的金色光点，去山口的红色邮筒",
				done = () => NearMailbox() || Letters(),
				onEnter = delegate
				{
					TownMap.ForceShow = true;
				},
				onExit = delegate
				{
					TownMap.ForceShow = false;
				}
			},
			new Step
			{
				keys = new string[1] { "E" },
				text = "看到「E 取信」的提示，按 E 取信",
				done = Letters
			},
			new Step
			{
				keys = new string[1] { "M" },
				text = "按 M 打开地图，看看下一站在哪",
				done = () => mapOpened,
				afterLetter = true
			},
			new Step
			{
				keys = new string[1] { "H" },
				text = "忘了要做什么？随时按 H，路单和金色光点会再亮起来",
				done = () => hPressed,
				autoSeconds = 12f,
				afterLetter = true
			},
			new Step
			{
				keys = new string[1] { "按住鼠标中键" },
				text = "挑一个表情或动作，和小镇打个招呼",
				done = () => wheelOpened,
				autoSeconds = 12f,
				afterLetter = true
			}
		};
		index = (flag ? Array.FindIndex(steps, (Step s) => s.afterLetter) : 0);
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-tutorialstep");
		if (num >= 0 && num + 1 < commandLineArgs.Length && int.TryParse(commandLineArgs[num + 1], out var result))
		{
			index = Mathf.Clamp(result - 1, 0, steps.Length - 1);
			if (steps[index].afterLetter && !flag)
			{
				boot.state.flags.Add("letters_taken");
			}
		}
		if (Array.IndexOf(commandLineArgs, "-tutorialshot") >= 0)
		{
			StartCoroutine(ShotWhenShown());
		}
		Running = true;
		ui.HideKeysStrip();
		if (chase != null)
		{
			lastYaw = chase.yaw;
		}
		lastPos = player.transform.position;
		Debug.Log($"BASICS_TUTORIAL begin at step {index + 1}/{steps.Length}");
	}

	private bool Letters()
	{
		return boot.state.flags.Contains("letters_taken");
	}

	private bool NearMailbox()
	{
		if (QuestGuide.Instance != null && QuestGuide.Instance.HasTarget && QuestGuide.Instance.Distance < 3.5f)
		{
			if (!(boot.state.step == "P0_boot"))
			{
				return boot.state.step == "P1_mailbox";
			}
			return true;
		}
		return false;
	}

	private void OnDestroy()
	{
		if (Running)
		{
			Running = false;
			TownMap.ForceShow = false;
		}
	}

	private void Update()
	{
		if (!Running || steps == null)
		{
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if (map != null && map.IsOpen)
		{
			mapOpened = true;
		}
		if (EmoteWheel.IsOpen)
		{
			wheelOpened = true;
		}
		if (Input.GetKeyDown(KeyCode.H))
		{
			hPressed = true;
		}
		if (!Free)
		{
			freeT = 0f;
			HideCard();
			return;
		}
		freeT += unscaledDeltaTime;
		if (freeT < 0.6f)
		{
			return;
		}
		if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
		{
			Finish(skipped: true);
			return;
		}
		Step step = steps[index];
		if (step.afterLetter && !Letters())
		{
			HideCard();
			return;
		}
		if (!entered)
		{
			entered = true;
			step.onEnter?.Invoke();
			ResetProgress();
		}
		if (!cardShown)
		{
			cardShown = true;
			ui.ShowBasicsCard($"新手教程 · {index + 1} / {steps.Length}", step.keys, step.text);
			if (doneT >= 0f)
			{
				ui.MarkBasicsDone();
			}
		}
		stepT += unscaledDeltaTime;
		TrackProgress(unscaledDeltaTime);
		if (doneT < 0f && (step.done() || (step.autoSeconds > 0f && stepT >= step.autoSeconds)))
		{
			doneT = 0f;
			ui.MarkBasicsDone();
			ShadowAndChimeSystem.PlayProceduralChime(880f, 0.18f);
		}
		if (!(doneT >= 0f))
		{
			return;
		}
		doneT += unscaledDeltaTime;
		if (doneT >= 0.9f)
		{
			step.onExit?.Invoke();
			doneT = -1f;
			stepT = 0f;
			entered = (cardShown = false);
			index++;
			if (index >= steps.Length)
			{
				Finish(skipped: false);
			}
		}
	}

	private void ResetProgress()
	{
		yawTurned = (walked = (shiftWalk = (wheel = 0f)));
		jumped = false;
		if (chase != null)
		{
			lastYaw = chase.yaw;
		}
		lastPos = player.transform.position;
		mapOpened = map != null && map.IsOpen;
		hPressed = false;
		wheelOpened = false;
	}

	private void TrackProgress(float dt)
	{
		if (chase != null)
		{
			yawTurned += Mathf.Abs(Mathf.DeltaAngle(lastYaw * 57.29578f, chase.yaw * 57.29578f));
			lastYaw = chase.yaw;
		}
		Vector3 position = player.transform.position;
		float magnitude = new Vector2(position.x - lastPos.x, position.z - lastPos.z).magnitude;
		if (magnitude < 2f)
		{
			walked += magnitude;
		}
		lastPos = position;
		if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && player.Speed > 0.2f)
		{
			shiftWalk += dt;
		}
		if (EasyInputBridge.Down(KeyCode.Space))
		{
			jumped = true;
		}
		wheel += Mathf.Abs(Input.mouseScrollDelta.y);
	}

	private IEnumerator ShotWhenShown()
	{
		Application.runInBackground = true;
		float limit = Time.realtimeSinceStartup + 40f;
		while (!cardShown && Time.realtimeSinceStartup < limit)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1.5f);
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"tutorial_step{index + 1}.png"));
		yield return new WaitForSecondsRealtime(0.6f);
		Application.Quit();
	}

	private void Finish(bool skipped)
	{
		if (index >= 0 && index < steps.Length)
		{
			steps[index].onExit?.Invoke();
		}
		Running = false;
		TownMap.ForceShow = false;
		ui.HideBasicsCard();
		GameState state = boot.state;
		if (!state.seenTutorials.Contains("basics_v1"))
		{
			state.seenTutorials.Add("basics_v1");
		}
		boot.store.Save(state);
		ui.ShowToast(skipped ? "已跳过新手教程 · 按 Esc 随时暂停、调设置" : "新手教程完成！按 Esc 随时暂停、调设置", 4f);
		Debug.Log(string.Format("BASICS_TUTORIAL {0} at step {1}", skipped ? "skipped" : "done", index + 1));
		base.enabled = false;
	}
}
