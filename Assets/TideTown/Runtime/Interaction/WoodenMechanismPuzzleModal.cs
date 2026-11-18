using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class WoodenMechanismPuzzleModal : MonoBehaviour
{
	public sealed class PuzzleSpec
	{
		public string id;

		public string title;

		public string subtitle;

		public string lux3DModelName;

		public string mechanism3DName;

		public int[] targetRings;

		public string[] clueItemIds;

		public string[] clueItemNames;

		public string[] clueTexts;

		public string[] lockedCluePrompts;

		public string successToast;
	}

	private struct RingPaint
	{
		public Renderer r;

		public int index;

		public int[] props;

		public Color[] baseColors;
	}

	private static WoodenMechanismPuzzleModal instance;

	private static readonly Dictionary<string, PuzzleSpec> Specs = new Dictionary<string, PuzzleSpec>
	{
		{
			"bakery_lock",
			new PuzzleSpec
			{
				id = "bakery_lock",
				title = "面包房烤炉 · 鲁班三环风门机关",
				subtitle = "炉门旁压着一张沾满面粉的便签：『老炉子怕海风倒灌，凑齐屋里三样老物件底下的数字方肯开膛。塞入干柴后，给火苗吹一口暖气，烟囱便会升起炊烟。』",
				lux3DModelName = "Bakery_scale",
				mechanism3DName = "M1_鲁班三环风门机关",
				targetRings = new int[3] { 3, 5, 2 },
				clueItemIds = new string[3] { "recipe", "scale", "basket" },
				clueItemNames = new string[3] { "菜谱本", "老台秤", "藤编面包篮" },
				clueTexts = new string[3] { "外环「书页」纹样（菜谱封底）：起海风时需分 3 次鼓风  ->  刻度【 3 】", "中环「秤砣」纹样（台秤底座）：黄铜秤砣常年停在 5 两  ->  刻度【 5 】", "内环「藤篮」纹样（面包篮底）：生日面包多放 2 勺葡萄干  ->  刻度【 2 】" },
				lockedCluePrompts = new string[3] { "外环轮缘：镌刻着一枚「翻开的书页」纹样", "中环轮缘：镌刻着一枚「黄铜秤砣」纹样", "内环轮缘：镌刻着一枚「藤编提篮」纹样" },
				successToast = "3D 鲁班三环风门严丝合缝归位，面包房烤炉已点燃！"
			}
		},
		{
			"school_lock",
			new PuzzleSpec
			{
				id = "school_lock",
				title = "学校后院 · 小黑爪印转盘木匣",
				subtitle = "食盆旁压着阿萤留下的值日字条：『小黑守了一整天，肚子饿了，或者想玩新玩具了。送它一样东西吧——哪怕用笔在信笺背面画出来也算数。』",
				lux3DModelName = "S_doghouse",
				mechanism3DName = "M2_小黑爪印转盘木匣",
				targetRings = new int[3] { 4, 1, 6 },
				clueItemIds = new string[3] { "slipper", "doghouse", "blackboard" },
				clueItemNames = new string[3] { "旧拖鞋", "狗窝木箱", "操场黑板" },
				clueTexts = new string[3] { "外环「牙印」纹样（拖鞋鞋底）：鞋帮留着 4 颗小狗乳牙印  ->  刻度【 4 】", "中环「木屋」纹样（狗窝内侧）：放学第 1 个出来的人喂水  ->  刻度【 1 】", "内环「黑板」纹样（黑板背框）：第 6 值日小组留下的签名  ->  刻度【 6 】" },
				lockedCluePrompts = new string[3] { "外环轮缘：镌刻着一枚「小狗牙印」纹样", "中环轮缘：镌刻着一枚「尖顶木屋」纹样", "内环轮缘：镌刻着一枚「值日黑板」纹样" },
				successToast = "爪印转盘木匣清脆弹开，红球顺着木槽滚进了狗碗！"
			}
		},
		{
			"lighthouse_lock",
			new PuzzleSpec
			{
				id = "lighthouse_lock",
				title = "灯塔顶层 · 守塔人黄铜三轴星盘",
				subtitle = "星盘底座压着老陈泛黄的航标签：『三环对准方位、仰角与闪频后，光束便能刺破夜雾。当光束照向对岸山脊时，朝山那边的人挥挥手吧。』",
				lux3DModelName = "LH_scope",
				mechanism3DName = "M4_守塔人黄铜三轴星盘",
				targetRings = new int[3] { 6, 2, 3 },
				clueItemIds = new string[3] { "hat", "scope", "log" },
				clueItemNames = new string[3] { "守塔人帽", "黄铜望远镜", "航海日志" },
				clueTexts = new string[3] { "外环「船帽」纹样（白檐帽衬）：潮汐镇航标北偏西第 6 象限  ->  刻度【 6 】", "中环「铜镜」纹样（望远镜筒）：红漆固定在仰角第 2 档位  ->  刻度【 2 】", "内环「日志」纹样（航海日志）：对准山脊连闪 3 次光束联络  ->  刻度【 3 】" },
				lockedCluePrompts = new string[3] { "外环轮缘：镌刻着一枚「白檐船帽」纹样", "中环轮缘：镌刻着一枚「单筒铜镜」纹样", "内环轮缘：镌刻着一枚「航海日志」纹样" },
				successToast = "3D 黄铜三轴星盘校准完毕，灯塔主透镜已点亮！"
			}
		}
	};

	private bool active;

	private PuzzleSpec currentSpec;

	private GameState gameState;

	private SaveStore saveStore;

	private EggboyPlayer eggboy;

	private EggboyUI eggboyUi;

	private ControlGate controlGate;

	private object gateToken;

	private ChaseCamera chaseCam;

	private bool restoreChase;

	private Action solvedCallback;

	private readonly bool[] latches = new bool[3];

	private bool stage1Unlocked;

	private readonly int[] rings = new int[3];

	private int attemptsLeft = 3;

	private bool puzzleSolved;

	private bool assisted;

	private float solveAnimTimer;

	private float openedAt;

	private float shakeTimer;

	private float spindlePush;

	private int selectedRing;

	private bool assistShown;

	public const float AssistSeconds = 120f;

	private GameObject puzzleRig;

	private Transform modelPivot;

	private readonly Transform[] latchNodes = new Transform[3];

	private readonly Vector3[] latchBasePos = new Vector3[3];

	private readonly Vector3[] latchBaseScale = new Vector3[3];

	private readonly Transform[] ringNodes = new Transform[3];

	private readonly Transform[] gearNodes = new Transform[3];

	private readonly List<TextMesh>[] ringNumbers = new List<TextMesh>[3]
	{
		new List<TextMesh>(),
		new List<TextMesh>(),
		new List<TextMesh>()
	};

	private readonly float[] ringVisualAngle = new float[3];

	private Transform coreSpindleNode;

	private Transform pointerNode;

	private Vector3 spindleBasePos;

	private int dragRing = -1;

	private float dragLastAngle;

	private float dragAccum;

	private bool dragMoved;

	private Vector2 dragStartMouse;

	private RectTransform noteCard;

	private Text placeText;

	private Text titleText;

	private Text attemptsText;

	private Text latchText;

	private Text hintText;

	private readonly Image[] rowBand = new Image[3];

	private readonly Text[] rowChip = new Text[3];

	private readonly Text[] rowClue = new Text[3];

	private readonly Text[] rowNow = new Text[3];

	private readonly Text[] rowDone = new Text[3];

	private int uiSig = int.MinValue;

	private static readonly string[] ColorProps = new string[3] { "_BaseColor", "baseColorFactor", "_Color" };

	private readonly List<RingPaint>[] ringPaints = new List<RingPaint>[3]
	{
		new List<RingPaint>(),
		new List<RingPaint>(),
		new List<RingPaint>()
	};

	private readonly float[] ringFocus = new float[3] { 1f, 1f, 1f };

	private readonly Renderer[] ringEdges = new Renderer[3];

	private Material edgeMat;

	private Material dimMat;

	private MaterialPropertyBlock mpb;

	private Color numberInk;

	private const float DimOther = 0.62f;

	private const float CardW = 384f;

	private const float CardH = 352f;

	private static readonly Color32 BandSelected = new Color32(246, 217, 168, 170);

	private static readonly Color32 BandIdle = new Color32(246, 217, 168, 0);

	private static readonly Color32 InkDone = new Color32(77, 116, 58, byte.MaxValue);

	private static Texture2D backdropTex;

	private bool overlayHeld;

	private static Font numberFont;

	private static readonly Color NumberShadow = new Color(0.16f, 0.09f, 0.04f, 0.8f);

	private readonly List<TextMesh>[] ringNumberShadows = new List<TextMesh>[3]
	{
		new List<TextMesh>(),
		new List<TextMesh>(),
		new List<TextMesh>()
	};

	public static bool IsOpen
	{
		get
		{
			if (instance != null)
			{
				return instance.active;
			}
			return false;
		}
	}

	public static int ActiveRingSelection { get => (instance != null) ? instance.selectedRing : 0; set { if (instance != null) instance.selectedRing = Mathf.Clamp(value, 0, 2); } }


	public static string OpenPuzzleId
	{
		get
		{
			if (!IsOpen || instance.currentSpec == null)
			{
				return "";
			}
			return instance.currentSpec.id;
		}
	}

	public static bool DebugStage1
	{
		get
		{
			if (IsOpen)
			{
				return instance.stage1Unlocked;
			}
			return false;
		}
	}

	public static int DebugAttemptsLeft
	{
		get
		{
			if (!IsOpen)
			{
				return -1;
			}
			return instance.attemptsLeft;
		}
	}

	public static bool DebugSolved
	{
		get
		{
			if (instance != null)
			{
				return instance.puzzleSolved;
			}
			return false;
		}
	}

	private bool AssistReady
	{
		get
		{
			if (active && !puzzleSolved)
			{
				return Time.unscaledTime - openedAt > 0.5f;
			}
			return false;
		}
	}

	public static bool ClueInfo(string itemId, out int ring, out int number)
	{
		foreach (PuzzleSpec value in Specs.Values)
		{
			for (int i = 0; i < value.clueItemIds.Length; i++)
			{
				if (value.clueItemIds[i] == itemId)
				{
					ring = i;
					number = value.targetRings[i];
					return true;
				}
			}
		}
		ring = -1;
		number = 0;
		return false;
	}

	public static void ExternalToggleLatch(int index)
	{
		if (IsOpen)
		{
			instance.ToggleLatch(Mathf.Clamp(index, 0, 2));
		}
	}

	public static void ExternalUnlockAllLatchesByBlowOrGesture(string sourceLabel)
	{
		if (IsOpen && !instance.puzzleSolved)
		{
			bool[] array = instance.latches;
			bool flag;
			instance.latches[1] = (flag = (instance.latches[2] = true));
			array[0] = flag;
			instance.stage1Unlocked = true;
			ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.25f);
			instance.RefreshPuzzleUI();
		}
	}

	public static void ExternalRotateRing(int ringIndex, int delta)
	{
		if (IsOpen && delta != 0)
		{
			instance.RotateRing(Mathf.Clamp(ringIndex, 0, 2), (delta > 0) ? 1 : (-1));
		}
	}

	public static void ExternalAttemptUnlock()
	{
		if (IsOpen)
		{
			instance.AttemptUnlockSpindle();
		}
	}

	public static void ExternalAlignRingByVoiceNote(int ringIndex, string noteName)
	{
		if (IsOpen && instance.currentSpec != null && !instance.puzzleSolved)
		{
			int num = Mathf.Clamp(ringIndex, 0, 2);
			if (!instance.stage1Unlocked)
			{
				bool[] array = instance.latches;
				bool flag;
				instance.latches[1] = (flag = (instance.latches[2] = true));
				array[0] = flag;
				instance.stage1Unlocked = true;
			}
			instance.rings[num] = instance.currentSpec.targetRings[num];
			ShadowAndChimeSystem.PlayProceduralChime(523.25f + (float)num * 130f, 0.28f);
			instance.RefreshPuzzleUI();
		}
	}

	public static void ExternalStartAICalibration()
	{
		if (IsOpen && !instance.puzzleSolved)
		{
			instance.StopAllCoroutines();
			instance.assisted = true;
			instance.StartCoroutine(instance.AICalibrateStepByStepRoutine());
		}
	}

	private IEnumerator AICalibrateStepByStepRoutine()
	{
		if (currentSpec == null)
		{
			yield break;
		}
		yield return new WaitForSecondsRealtime(0.3f);
		foreach (int item in LatchSolution())
		{
			ToggleLatch(item);
			yield return new WaitForSecondsRealtime(0.3f);
		}
		yield return new WaitForSecondsRealtime(0.2f);
		for (int r = 0; r < 3; r++)
		{
			int guard = 0;
			while (rings[r] != currentSpec.targetRings[r] && guard++ < 8)
			{
				RotateRing(r, 1);
				yield return new WaitForSecondsRealtime(0.28f);
			}
		}
		yield return new WaitForSecondsRealtime(0.35f);
		AttemptUnlockSpindle();
	}

	public static bool DebugOpen(string puzzleId, GameState state, SaveStore store, EggboyPlayer player, EggboyUI ui, ControlGate gate, Action onSolved)
	{
		if (instance == null)
		{
			GameObject obj = new GameObject("WoodenMechanismPuzzleModal");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			instance = obj.AddComponent<WoodenMechanismPuzzleModal>();
		}
		instance.BeginPuzzle(puzzleId, state, store, player, ui, gate, onSolved);
		return instance.active;
	}

	private List<int> LatchSolution()
	{
		List<int> list = new List<int>();
		if (latches[0] && latches[1] && latches[2])
		{
			return list;
		}
		for (int i = 1; i < 8; i++)
		{
			bool flag = latches[0];
			bool flag2 = latches[1];
			bool flag3 = latches[2];
			if (((uint)i & (true ? 1u : 0u)) != 0)
			{
				flag = !flag;
				flag2 = !flag2;
			}
			if (((uint)i & 2u) != 0)
			{
				flag = !flag;
				flag2 = !flag2;
				flag3 = !flag3;
			}
			if (((uint)i & 4u) != 0)
			{
				flag2 = !flag2;
				flag3 = !flag3;
			}
			if (!(flag && flag2 && flag3))
			{
				continue;
			}
			for (int j = 0; j < 3; j++)
			{
				if ((i & (1 << j)) != 0)
				{
					list.Add(j);
				}
			}
			return list;
		}
		return list;
	}

	public static void DebugOpenAllLatches()
	{
		if (!IsOpen)
		{
			return;
		}
		foreach (int item in instance.LatchSolution())
		{
			instance.ToggleLatch(item);
		}
	}

	public static int DebugRing(int i)
	{
		if (!IsOpen)
		{
			return -1;
		}
		return instance.rings[i];
	}

	public static void DebugClose()
	{
		if (IsOpen)
		{
			instance.CleanupModal(solved: false);
		}
	}

	public static int DebugNumberUnderPointer(int ringIndex)
	{
		if (!IsOpen)
		{
			return -1;
		}
		return instance.NumberUnderPointer(ringIndex);
	}

	public static void DebugDragRing(int ringIndex, float degrees)
	{
		if (IsOpen)
		{
			instance.ApplyRingDrag(ringIndex, degrees);
		}
	}

	public static void DebugSelectRing(int ringIndex)
	{
		if (IsOpen)
		{
			instance.selectedRing = Mathf.Clamp(ringIndex, 0, 2);
		}
	}

	public static void OpenOrRun(string puzzleId, GameState state, SaveStore store, EggboyPlayer player, EggboyUI ui, ControlGate gate, Action onSolved)
	{
		if (ui == null || !ui.inputClose || state == null || state.flags.Contains("puzzle_" + puzzleId))
		{
			onSolved?.Invoke();
			return;
		}
		if (instance == null)
		{
			GameObject obj = new GameObject("WoodenMechanismPuzzleModal");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			instance = obj.AddComponent<WoodenMechanismPuzzleModal>();
		}
		instance.BeginPuzzle(puzzleId, state, store, player, ui, gate, onSolved);
	}

	private void BeginPuzzle(string puzzleId, GameState state, SaveStore store, EggboyPlayer player, EggboyUI ui, ControlGate gate, Action onSolved)
	{
		if (active)
		{
			CleanupModal(solved: false);
		}
		if (!Specs.TryGetValue(puzzleId, out currentSpec))
		{
			onSolved?.Invoke();
			return;
		}
		gameState = state;
		saveStore = store;
		eggboy = player;
		eggboyUi = ui;
		controlGate = gate;
		solvedCallback = onSolved;
		bool[] array = latches;
		bool flag;
		latches[1] = (flag = (latches[2] = false));
		array[0] = flag;
		stage1Unlocked = false;
		rings[0] = WrapRing(currentSpec.targetRings[0] + 2);
		rings[1] = WrapRing(currentSpec.targetRings[1] + 3);
		rings[2] = WrapRing(currentSpec.targetRings[2] + 4);
		attemptsLeft = 3;
		puzzleSolved = false;
		assisted = false;
		solveAnimTimer = 0f;
		shakeTimer = 0f;
		spindlePush = 0f;
		selectedRing = 0;
		dragRing = -1;
		openedAt = Time.unscaledTime;
		if (controlGate != null)
		{
			gateToken = controlGate.Acquire("wooden_puzzle");
		}
		if (eggboy != null)
		{
			eggboy.locked = true;
		}
		chaseCam = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		restoreChase = chaseCam != null && chaseCam.enabled;
		if (restoreChase)
		{
			chaseCam.enabled = false;
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		MenuController.NoteScriptUnlock();
		EnsurePuzzleUI();
		if (noteCard != null)
		{
			noteCard.gameObject.SetActive(value: true);
		}
		Build3DLuxRig();
		for (int i = 0; i < 3; i++)
		{
			ringVisualAngle[i] = (float)(-rings[i]) * 60f;
		}
		RefreshPuzzleUI();
		active = true;
	}

	private static int WrapRing(int v)
	{
		while (v < 1)
		{
			v += 6;
		}
		while (v > 6)
		{
			v -= 6;
		}
		return v;
	}

	private void EnsurePuzzleUI()
	{
		if (noteCard != null)
		{
			return;
		}
		RectTransform rectTransform = TideTownStyleUI.FindHudCanvasRoot(eggboyUi);
		if (!(rectTransform == null))
		{
			noteCard = TideTownStyleUI.CreateParchmentCard(rectTransform, "WoodenMechanismNote", 384f, 352f, 0);
			RectTransform rectTransform2 = noteCard;
			Vector2 anchorMin = (noteCard.anchorMax = new Vector2(1f, 0.5f));
			rectTransform2.anchorMin = anchorMin;
			noteCard.pivot = new Vector2(1f, 0.5f);
			noteCard.anchoredPosition = new Vector2(-44f, 18f);
			float num = -166f;
			float num2 = 332f;
			placeText = Label("Place", 13, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft, num, 146f, 260f, 20f, bold: false);
			titleText = Label("Title", 21, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, num, 120f, 270f, 30f, bold: true);
			Image image = TideTownStyleUI.CreatePanel(noteCard, "TitleRule", num2, 1.5f, new Color32(201, 174, 134, 200), rounded: false);
			image.raycastTarget = false;
			image.rectTransform.anchoredPosition = new Vector2(0f, 96f);
			Label("NotesHead", 14, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft, num, 74f, 120f, 22f, bold: true).text = "邮差笔记";
			attemptsText = Label("Attempts", 14, TideTownStyleUI.InkMuted, TextAnchor.MiddleRight, num + num2 - 230f, 74f, 230f, 22f, bold: false);
			for (int i = 0; i < 3; i++)
			{
				float y2 = 36f - (float)i * 46f;
				Image image2 = TideTownStyleUI.CreatePanel(noteCard, "RingRow_" + i, num2 + 12f, 40f, BandIdle);
				image2.raycastTarget = false;
				image2.rectTransform.anchoredPosition = new Vector2(0f, y2);
				rowBand[i] = image2;
				rowChip[i] = Label("Chip_" + i, 16, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, num, y2, 64f, 30f, bold: true);
				rowClue[i] = Label("Clue_" + i, 15, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, num + 64f, y2, 170f, 30f, bold: false);
				rowNow[i] = Label("Now_" + i, 14, TideTownStyleUI.InkMuted, TextAnchor.MiddleRight, num + num2 - 116f, y2, 52f, 30f, bold: false);
				rowDone[i] = Label("Done_" + i, 13, InkDone, TextAnchor.MiddleRight, num + num2 - 58f, y2, 58f, 30f, bold: true);
			}
			Image image3 = TideTownStyleUI.CreatePanel(noteCard, "FootRule", num2, 1f, new Color32(224, 207, 176, 200), rounded: false);
			image3.raycastTarget = false;
			image3.rectTransform.anchoredPosition = new Vector2(0f, -94f);
			latchText = Label("Latches", 14, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, num, -114f, num2, 24f, bold: false);
			hintText = TideTownStyleUI.CreateText(noteCard, "", 12, TideTownStyleUI.InkMuted, TextAnchor.UpperLeft, new Vector2(num + num2 * 0.5f, -152f), new Vector2(num2, 44f));
			hintText.lineSpacing = 1.2f;
		}
		Text Label(string name, int size, Color32 col, TextAnchor a, float x, float y, float w, float h, bool bold)
		{
			Text text = TideTownStyleUI.CreateText(noteCard, "", size, col, a, new Vector2(x + w * 0.5f, y), new Vector2(w, h), bold);
			text.gameObject.name = name;
			text.horizontalOverflow = HorizontalWrapMode.Overflow;
			return text;
		}
	}

	private void Build3DLuxRig()
	{
		if (puzzleRig != null)
		{
			UnityEngine.Object.Destroy(puzzleRig);
		}
		Camera main = Camera.main;
		if (main == null || currentSpec == null)
		{
			return;
		}
		puzzleRig = new GameObject("LuxPuzzlePreviewRig");
		puzzleRig.transform.SetParent(main.transform, worldPositionStays: false);
		float num = Mathf.Tan(main.fieldOfView * (MathF.PI / 180f) * 0.5f) * 1.55f;
		float num2 = -0.17f * main.aspect * num;
		puzzleRig.transform.localPosition = new Vector3(num2, 0.02f, 1.55f);
		puzzleRig.transform.localRotation = Quaternion.identity;
		GameObject obj = new GameObject("PuzzleWarmLight");
		obj.transform.SetParent(puzzleRig.transform, worldPositionStays: false);
		obj.transform.localPosition = new Vector3(-0.5f, 0.6f, -0.8f);
		Light light = obj.AddComponent<Light>();
		light.type = LightType.Point;
		light.color = OverlayRender.SceneKeyColor;
		light.range = 4f;
		light.intensity = 0.8f;
		light.shadows = LightShadows.None;
		ObjectInspectModal.AddRimLight(puzzleRig.transform);
		BuildBackdrop(puzzleRig.transform, main, 0f - num2, 1.55f);
		GameObject gameObject = new GameObject("Pivot");
		gameObject.transform.SetParent(puzzleRig.transform, worldPositionStays: false);
		gameObject.transform.localRotation = Quaternion.Euler(6f, 180f, 0f);
		modelPivot = gameObject.transform;
		GameObject gameObject2 = TideTownStyleUI.BuildArticulatedMechanism3D(currentSpec.mechanism3DName, 0.92f);
		gameObject2.transform.SetParent(modelPivot, worldPositionStays: false);
		gameObject2.transform.localPosition = Vector3.zero;
		for (int i = 0; i < 3; i++)
		{
			latchNodes[i] = TideTownStyleUI.FindDeepChild(gameObject2.transform, "Latch_" + i);
			if (latchNodes[i] != null)
			{
				latchBasePos[i] = latchNodes[i].localPosition;
				latchBaseScale[i] = latchNodes[i].localScale;
				latchNodes[i].gameObject.name = "MechLatch_" + i;
				AddFittedBox(latchNodes[i], 1.6f);
			}
			ringNodes[i] = TideTownStyleUI.FindDeepChild(gameObject2.transform, "Ring_" + i);
			if (ringNodes[i] != null)
			{
				ringNodes[i].gameObject.name = "MechRing_" + i;
				AddFittedBox(ringNodes[i], 1f);
			}
			gearNodes[i] = TideTownStyleUI.FindDeepChild(gameObject2.transform, "Gear_" + i);
		}
		coreSpindleNode = TideTownStyleUI.FindDeepChild(gameObject2.transform, "Core_Spindle");
		if (coreSpindleNode != null)
		{
			spindleBasePos = coreSpindleNode.localPosition;
			coreSpindleNode.gameObject.name = "MechSpindle";
			AddFittedBox(coreSpindleNode, 1.8f);
		}
		BuildRingNumbers();
		BuildPointer();
		CollectRingPaints();
		BuildRingEdges();
		for (int j = 0; j < 3; j++)
		{
			ringFocus[j] = ((j == selectedRing) ? 1f : 0.62f);
		}
		ApplyRingFocus();
		SetupOverlayCamera(main);
	}

	private static Material UnlitTransparent(Color c, Texture tex = null)
	{
		Shader shader = Resources.Load<Shader>("EggboyURPUnlitTransparent");
		if (shader == null)
		{
			shader = Shader.Find("EGGBOY/URPUnlitTransparent");
		}
		if (shader == null)
		{
			return null;
		}
		Material material = new Material(shader);
		if (tex != null)
		{
			material.SetTexture("_BaseMap", tex);
		}
		material.SetColor("_BaseColor", c);
		return material;
	}

	private void BuildBackdrop(Transform rig, Camera cam, float centerX, float dist)
	{
		if (backdropTex == null)
		{
			backdropTex = new Texture2D(128, 72, TextureFormat.RGBA32, mipChain: false)
			{
				wrapMode = TextureWrapMode.Clamp,
				name = "PuzzleBackdrop"
			};
			Color32[] array = new Color32[9216];
			for (int i = 0; i < 72; i++)
			{
				for (int j = 0; j < 128; j++)
				{
					float num = ((float)j + 0.5f) / 128f * 2f - 1f;
					float num2 = ((float)i + 0.5f) / 72f * 2f - 1f;
					float num3 = Mathf.Sqrt(num * num * 0.8f + num2 * num2 * 1.2f);
					float num4 = Mathf.Lerp(0.5f, 0.8f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num3 - 0.25f) / 0.95f)));
					array[i * 128 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num4 * 255f));
				}
			}
			backdropTex.SetPixels32(array);
			backdropTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		}
		if (dimMat == null)
		{
			dimMat = UnlitTransparent(new Color(0.11f, 0.07f, 0.04f, 1f), backdropTex);
		}
		if (!(dimMat == null))
		{
			GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
			obj.name = "PuzzleBackdrop";
			UnityEngine.Object.Destroy(obj.GetComponent<Collider>());
			obj.transform.SetParent(rig, worldPositionStays: false);
			float num5 = 1.6f;
			float num6 = dist + num5;
			float num7 = 2f * Mathf.Tan(cam.fieldOfView * (MathF.PI / 180f) * 0.5f) * num6 * 1.15f;
			obj.transform.localPosition = new Vector3(centerX, -0.02f, num5);
			obj.transform.localScale = new Vector3(num7 * cam.aspect * 1.05f, num7, 1f);
			MeshRenderer component = obj.GetComponent<MeshRenderer>();
			component.sharedMaterial = dimMat;
			component.shadowCastingMode = ShadowCastingMode.Off;
			component.receiveShadows = false;
		}
	}

	private void CollectRingPaints()
	{
		if (mpb == null)
		{
			mpb = new MaterialPropertyBlock();
		}
		for (int i = 0; i < 3; i++)
		{
			ringPaints[i].Clear();
			if (ringNodes[i] == null)
			{
				continue;
			}
			Renderer[] componentsInChildren = ringNodes[i].GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (renderer.GetComponent<TextMesh>() != null || OwningRing(renderer.transform) != i)
				{
					continue;
				}
				Material[] sharedMaterials = renderer.sharedMaterials;
				for (int k = 0; k < sharedMaterials.Length; k++)
				{
					Material material = sharedMaterials[k];
					if (material == null)
					{
						continue;
					}
					List<int> list = new List<int>();
					List<Color> list2 = new List<Color>();
					string[] colorProps = ColorProps;
					foreach (string text in colorProps)
					{
						if (material.HasProperty(text))
						{
							list.Add(Shader.PropertyToID(text));
							list2.Add(material.GetColor(text));
						}
					}
					if (list.Count != 0)
					{
						ringPaints[i].Add(new RingPaint
						{
							r = renderer,
							index = k,
							props = list.ToArray(),
							baseColors = list2.ToArray()
						});
					}
				}
			}
		}
	}

	private int OwningRing(Transform t)
	{
		Transform transform = t;
		while (transform != null)
		{
			for (int i = 0; i < 3; i++)
			{
				if (ringNodes[i] == transform)
				{
					return i;
				}
			}
			transform = transform.parent;
		}
		return -1;
	}

	private void BuildRingEdges()
	{
		if (edgeMat == null)
		{
			edgeMat = UnlitTransparent(new Color(0.93f, 0.62f, 0.28f, 0.78f));
		}
		for (int i = 0; i < 3; i++)
		{
			ringEdges[i] = null;
			Transform transform = ringNodes[i];
			if (!(transform == null) && !(edgeMat == null))
			{
				RingGeometry(transform, out var radius, out var frontZ);
				float num = Mathf.Max(0.0001f, transform.lossyScale.x);
				float num2 = 0.007f / num;
				GameObject obj = new GameObject("RingEdge_" + i);
				obj.transform.SetParent(transform, worldPositionStays: false);
				obj.transform.localPosition = new Vector3(0f, 0f, frontZ + 0.003f / num);
				obj.AddComponent<MeshFilter>().sharedMesh = AnnulusMesh(radius * 1.015f - num2, radius * 1.015f, 96);
				MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = edgeMat;
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer.receiveShadows = false;
				ringEdges[i] = meshRenderer;
			}
		}
	}

	private static Mesh AnnulusMesh(float r0, float r1, int segs)
	{
		Vector3[] array = new Vector3[(segs + 1) * 2];
		Vector2[] array2 = new Vector2[array.Length];
		int[] array3 = new int[segs * 6];
		for (int i = 0; i <= segs; i++)
		{
			float f = (float)i * MathF.PI * 2f / (float)segs;
			float num = Mathf.Cos(f);
			float num2 = Mathf.Sin(f);
			array[i * 2] = new Vector3(num * r0, num2 * r0, 0f);
			array[i * 2 + 1] = new Vector3(num * r1, num2 * r1, 0f);
			array2[i * 2] = new Vector2(0.5f, 0.5f);
			array2[i * 2 + 1] = new Vector2(0.5f, 0.5f);
			if (i == segs)
			{
				break;
			}
			int num3 = i * 2;
			int num4 = i * 6;
			array3[num4] = num3;
			array3[num4 + 1] = num3 + 1;
			array3[num4 + 2] = num3 + 3;
			array3[num4 + 3] = num3;
			array3[num4 + 4] = num3 + 3;
			array3[num4 + 5] = num3 + 2;
		}
		Mesh mesh = new Mesh();
		mesh.name = "RingEdge";
		mesh.vertices = array;
		mesh.uv = array2;
		mesh.triangles = array3;
		mesh.RecalculateBounds();
		return mesh;
	}

	private void ApplyRingFocus()
	{
		if (mpb == null)
		{
			mpb = new MaterialPropertyBlock();
		}
		for (int i = 0; i < 3; i++)
		{
			float num = ringFocus[i];
			foreach (RingPaint item in ringPaints[i])
			{
				if (!(item.r == null))
				{
					item.r.GetPropertyBlock(mpb, item.index);
					for (int j = 0; j < item.props.Length; j++)
					{
						Color color = item.baseColors[j];
						mpb.SetColor(item.props[j], new Color(color.r * num, color.g * num, color.b * num, color.a));
					}
					item.r.SetPropertyBlock(mpb, item.index);
				}
			}
			float num2 = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0.62f, 1f, num));
			foreach (TextMesh item2 in ringNumbers[i])
			{
				if (item2 != null)
				{
					item2.color = new Color(numberInk.r, numberInk.g, numberInk.b, num2);
				}
			}
			foreach (TextMesh item3 in ringNumberShadows[i])
			{
				if (item3 != null)
				{
					item3.color = new Color(NumberShadow.r, NumberShadow.g, NumberShadow.b, NumberShadow.a * num2);
				}
			}
			if (ringEdges[i] != null)
			{
				ringEdges[i].enabled = num > 0.97f && !puzzleSolved;
			}
		}
	}

	private void SetupOverlayCamera(Camera cam)
	{
		OverlayRender.SetLayerRecursive(puzzleRig, 30);
		OverlayRender.LimitLights(puzzleRig);
		OverlayRender.Acquire(cam, ownLighting: true);
		overlayHeld = true;
	}

	private void TeardownOverlayCamera()
	{
		if (overlayHeld)
		{
			overlayHeld = false;
			OverlayRender.Release(ownLighting: true);
		}
	}

	private static void AddFittedBox(Transform t, float grow)
	{
		MeshFilter meshFilter = t.GetComponent<MeshFilter>() ?? t.GetComponentInChildren<MeshFilter>();
		BoxCollider boxCollider = t.gameObject.AddComponent<BoxCollider>();
		if (meshFilter != null && meshFilter.sharedMesh != null)
		{
			Bounds bounds = meshFilter.sharedMesh.bounds;
			if (meshFilter.transform != t)
			{
				Vector3 center = t.InverseTransformPoint(meshFilter.transform.TransformPoint(bounds.center));
				Vector3 vector = Vector3.Scale(bounds.size, meshFilter.transform.lossyScale);
				vector = new Vector3(vector.x / Mathf.Max(1E-05f, t.lossyScale.x), vector.y / Mathf.Max(1E-05f, t.lossyScale.y), vector.z / Mathf.Max(1E-05f, t.lossyScale.z));
				boxCollider.center = center;
				boxCollider.size = vector * grow;
			}
			else
			{
				boxCollider.center = bounds.center;
				boxCollider.size = bounds.size * grow;
			}
		}
	}

	private static void RingGeometry(Transform ring, out float radius, out float frontZ)
	{
		radius = 0.2f;
		frontZ = 0f;
		MeshFilter meshFilter = ring.GetComponent<MeshFilter>() ?? ring.GetComponentInChildren<MeshFilter>();
		if (!(meshFilter == null) && !(meshFilter.sharedMesh == null))
		{
			Bounds bounds = meshFilter.sharedMesh.bounds;
			if (meshFilter.transform != ring)
			{
				float num = meshFilter.transform.lossyScale.x / Mathf.Max(1E-05f, ring.lossyScale.x);
				radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * num;
				frontZ = ring.InverseTransformPoint(meshFilter.transform.TransformPoint(bounds.center)).z + bounds.extents.z * num;
			}
			else
			{
				radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
				frontZ = bounds.max.z;
			}
		}
	}

	private void BuildRingNumbers()
	{
		if (numberFont == null)
		{
			numberFont = Resources.Load<Font>("Fonts/NotoSansSC-VF");
		}
		if (numberFont == null)
		{
			numberFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		}
		Color color = (numberInk = new Color(0.98f, 0.93f, 0.82f));
		for (int i = 0; i < 3; i++)
		{
			ringNumbers[i].Clear();
			ringNumberShadows[i].Clear();
			Transform transform = ringNodes[i];
			if (!(transform == null))
			{
				RingGeometry(transform, out var radius, out var frontZ);
				float num = radius * i switch
				{
					1 => 0.8f, 
					0 => 0.86f, 
					_ => 0.72f, 
				};
				for (int j = 1; j <= 6; j++)
				{
					GameObject gameObject = new GameObject("RingNumber_" + i + "_" + j);
					gameObject.transform.SetParent(transform, worldPositionStays: false);
					float f = (90f + 60f * (float)j) * (MathF.PI / 180f);
					gameObject.transform.localPosition = new Vector3(Mathf.Cos(f) * num, Mathf.Sin(f) * num, frontZ + 0.004f);
					gameObject.transform.localRotation = Quaternion.LookRotation(Vector3.back, new Vector3(Mathf.Cos(f), Mathf.Sin(f), 0f));
					TextMesh textMesh = gameObject.AddComponent<TextMesh>();
					textMesh.font = numberFont;
					textMesh.text = j.ToString();
					textMesh.fontSize = 64;
					textMesh.characterSize = radius * ((i == 2) ? 0.05f : 0.035f);
					textMesh.anchor = TextAnchor.MiddleCenter;
					textMesh.alignment = TextAlignment.Center;
					textMesh.color = color;
					MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
					component.sharedMaterial = numberFont.material;
					component.shadowCastingMode = ShadowCastingMode.Off;
					ringNumbers[i].Add(textMesh);
					GameObject obj = new GameObject("Shadow");
					obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
					float num2 = textMesh.characterSize * 0.55f;
					obj.transform.localPosition = new Vector3(num2, 0f - num2, 0.0015f);
					TextMesh textMesh2 = obj.AddComponent<TextMesh>();
					textMesh2.font = numberFont;
					textMesh2.text = textMesh.text;
					textMesh2.fontSize = textMesh.fontSize;
					textMesh2.characterSize = textMesh.characterSize;
					textMesh2.anchor = textMesh.anchor;
					textMesh2.alignment = textMesh.alignment;
					textMesh2.color = NumberShadow;
					MeshRenderer component2 = obj.GetComponent<MeshRenderer>();
					component2.sharedMaterial = numberFont.material;
					component2.shadowCastingMode = ShadowCastingMode.Off;
					ringNumberShadows[i].Add(textMesh2);
				}
			}
		}
	}

	private void BuildPointer()
	{
		Transform transform = ringNodes[0];
		if (!(transform == null))
		{
			RingGeometry(transform, out var radius, out var frontZ);
			GameObject gameObject = new GameObject("RingPointer");
			gameObject.transform.SetParent(transform.parent, worldPositionStays: false);
			gameObject.transform.localPosition = transform.localPosition + new Vector3(0f, radius * 1.12f * transform.localScale.y, frontZ * transform.localScale.z + 0.01f);
			gameObject.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
			TextMesh textMesh = gameObject.AddComponent<TextMesh>();
			textMesh.font = numberFont;
			textMesh.text = "▼";
			textMesh.fontSize = 64;
			textMesh.characterSize = radius * 0.05f * transform.localScale.x;
			textMesh.anchor = TextAnchor.LowerCenter;
			textMesh.color = new Color(0.86f, 0.62f, 0.22f);
			gameObject.GetComponent<MeshRenderer>().sharedMaterial = numberFont.material;
			pointerNode = gameObject.transform;
		}
	}

	private int NumberUnderPointer(int r)
	{
		Camera main = Camera.main;
		if (main == null || ringNumbers[r].Count == 0)
		{
			return -1;
		}
		int result = -1;
		float num = float.MinValue;
		foreach (TextMesh item in ringNumbers[r])
		{
			float y = main.transform.InverseTransformPoint(item.transform.position).y;
			if (y > num)
			{
				num = y;
				result = int.Parse(item.text);
			}
		}
		return result;
	}

	private bool ClueFound(int i)
	{
		if (gameState == null || currentSpec == null)
		{
			return false;
		}
		if (currentSpec.id == "school_lock" && gameState.seenMemories.Contains("desk_detail"))
		{
			return true;
		}
		string text = currentSpec.clueItemIds[i];
		if (!gameState.seenMemories.Contains(text + "_detail"))
		{
			return gameState.flags.Contains("dog_sniff_" + text);
		}
		return true;
	}

	private int UiSignature()
	{
		int num = selectedRing;
		for (int i = 0; i < 3; i++)
		{
			num = num * 7 + rings[i];
		}
		for (int j = 0; j < 3; j++)
		{
			num = num * 2 + (latches[j] ? 1 : 0);
		}
		num = num * 4 + attemptsLeft;
		num = num * 2 + (AssistReady ? 1 : 0);
		return num * 2 + (puzzleSolved ? 1 : 0);
	}

	private void RefreshPuzzleUI()
	{
		if (noteCard == null || currentSpec == null)
		{
			return;
		}
		uiSig = UiSignature();
		string text = currentSpec.title ?? "";
		int num = text.IndexOf(" · ");
		placeText.text = ((num > 0) ? text.Substring(0, num) : "");
		titleText.text = ((num > 0) ? text.Substring(num + 3) : text);
		string text2 = "";
		for (int i = 0; i < 3; i++)
		{
			text2 += ((i < attemptsLeft) ? "●" : "○");
		}
		attemptsText.text = (AssistReady ? "H 搭把手\u3000·\u3000" : "") + "推主轴机会\u3000" + text2;
		attemptsText.color = ((attemptsLeft <= 1) ? ((Color)new Color32(180, 74, 44, byte.MaxValue)) : ((Color)TideTownStyleUI.InkMuted));
		string[] array = new string[3] { "外环", "中环", "内环" };
		for (int j = 0; j < 3; j++)
		{
			bool flag = j == selectedRing && !puzzleSolved;
			bool flag2 = ClueFound(j);
			bool flag3 = flag2 && rings[j] == currentSpec.targetRings[j];
			rowBand[j].color = (flag ? BandSelected : BandIdle);
			rowChip[j].text = (flag ? "▶ " : "\u3000") + array[j];
			rowChip[j].color = (flag ? TideTownStyleUI.InkDark : TideTownStyleUI.InkMuted);
			rowClue[j].text = (flag2 ? (currentSpec.clueItemNames[j] + " · 刻度 " + currentSpec.targetRings[j]) : ("「" + Glyph(currentSpec.lockedCluePrompts[j]) + "」· ？"));
			rowClue[j].color = (flag2 ? TideTownStyleUI.InkDark : TideTownStyleUI.InkMuted);
			rowNow[j].text = "现 " + rings[j];
			rowDone[j].text = ((flag3 || puzzleSolved) ? "已对准" : "");
		}
		if (puzzleSolved)
		{
			latchText.text = "机关已经打开了";
		}
		else
		{
			string text3 = "";
			for (int k = 0; k < 3; k++)
			{
				text3 += (latches[k] ? "●" : "○");
			}
			latchText.text = (stage1Unlocked ? ("木栓\u3000" + text3 + "\u3000全部拨开，可以转环了") : ("木栓\u3000" + text3 + "\u3000先把三根木栓都拨开"));
		}
		hintText.text = "拖动圆环转动\u3000↑↓ 选环\u3000←→ 转一格\n1 / 2 / 3 拨木栓\u3000空格 推主轴\u3000Esc 离开";
	}

	private static string Glyph(string s)
	{
		int num = s.IndexOf('「');
		int num2 = s.IndexOf('」');
		if (num < 0 || num2 <= num)
		{
			return s;
		}
		return s.Substring(num + 1, num2 - num - 1);
	}

	private void ToggleLatch(int index)
	{
		if (!puzzleSolved)
		{
			switch (index)
			{
			case 0:
				latches[0] = !latches[0];
				latches[1] = !latches[1];
				break;
			case 1:
				latches[0] = !latches[0];
				latches[1] = !latches[1];
				latches[2] = !latches[2];
				break;
			case 2:
				latches[1] = !latches[1];
				latches[2] = !latches[2];
				break;
			}
			bool flag = stage1Unlocked;
			stage1Unlocked = latches[0] && latches[1] && latches[2];
			ShadowAndChimeSystem.PlayProceduralChime((stage1Unlocked && !flag) ? 880f : 587.33f, 0.2f);
			RefreshPuzzleUI();
		}
	}

	private void RotateRing(int ringIndex, int delta)
	{
		if (puzzleSolved)
		{
			return;
		}
		if (!stage1Unlocked)
		{
			shakeTimer = Mathf.Max(shakeTimer, 0.18f);
			ShadowAndChimeSystem.PlayProceduralChime(220f, 0.15f);
			return;
		}
		rings[ringIndex] = WrapRing(rings[ringIndex] + delta);
		switch (ringIndex)
		{
		case 0:
			rings[1] = WrapRing(rings[1] + delta);
			break;
		case 1:
			rings[2] = WrapRing(rings[2] - delta);
			break;
		}
		ShadowAndChimeSystem.PlayProceduralChime(659.25f + (float)ringIndex * 60f, 0.12f);
	}

	private void ApplyRingDrag(int ringIndex, float degrees)
	{
		dragAccum += degrees;
		while (dragAccum >= 30f)
		{
			RotateRing(ringIndex, 1);
			dragAccum -= 60f;
		}
		while (dragAccum <= -30f)
		{
			RotateRing(ringIndex, -1);
			dragAccum += 60f;
		}
	}

	private void AttemptUnlockSpindle()
	{
		if (puzzleSolved || currentSpec == null)
		{
			return;
		}
		spindlePush = 1f;
		if (!stage1Unlocked)
		{
			shakeTimer = 0.25f;
			ShadowAndChimeSystem.PlayProceduralChime(220f, 0.25f);
			return;
		}
		int num = 0;
		for (int i = 0; i < 3; i++)
		{
			if (rings[i] == currentSpec.targetRings[i])
			{
				num++;
			}
		}
		if (num == 3)
		{
			TriggerSolveSuccess();
			return;
		}
		attemptsLeft--;
		shakeTimer = 0.55f;
		ShadowAndChimeSystem.PlayProceduralChime(196f, 0.35f);
		ShadowAndChimeSystem.PlayProceduralChime(147f, 0.4f);
		if (false && attemptsLeft <= 0)
		{
			string id = currentSpec.id;
			CleanupModal(solved: false);
			StoryDirector storyDirector = UnityEngine.Object.FindObjectOfType<StoryDirector>();
			if (storyDirector != null)
			{
				storyDirector.OnMechanismJamPenalty(id);
			}
		}
		else
		{
			latches[1] = false;
			stage1Unlocked = false;
			RefreshPuzzleUI();
		}
	}

	private void TriggerSolveSuccess()
	{
		if (puzzleSolved)
		{
			return;
		}
		puzzleSolved = true;
		stage1Unlocked = true;
		bool[] array = latches;
		bool flag;
		latches[1] = (flag = (latches[2] = true));
		array[0] = flag;
		solveAnimTimer = 1.1f;
		ShadowAndChimeSystem.PlayProceduralChime(880f, 0.45f);
		ShadowAndChimeSystem.PlayProceduralChime(1174.66f, 0.6f);
		if (gameState != null && currentSpec != null)
		{
			string item = "puzzle_" + currentSpec.id;
			if (!gameState.flags.Contains(item))
			{
				gameState.flags.Add(item);
			}
			if (saveStore != null)
			{
				saveStore.Save(gameState);
			}
			if (!assisted)
			{
				if (currentSpec.id == "bakery_lock")
				{
					TideTownAchievementSystem.Unlock("luban_oven");
				}
				else if (currentSpec.id == "school_lock")
				{
					TideTownAchievementSystem.Unlock("classic_ball");
				}
			}
		}
		if (eggboy != null)
		{
			eggboy.PlayEmote("hop");
		}
		RefreshPuzzleUI();
	}

	private int RingUnderMouse(out string latchOrSpindle)
	{
		latchOrSpindle = null;
		Camera main = Camera.main;
		if (main == null)
		{
			return -1;
		}
		RaycastHit[] array = Physics.RaycastAll(main.ScreenPointToRay(Input.mousePosition), 6f, -1, QueryTriggerInteraction.Collide);
		float num = float.MaxValue;
		Collider collider = null;
		RaycastHit[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			RaycastHit raycastHit = array2[i];
			if (raycastHit.collider != null && raycastHit.collider.name.StartsWith("Mech") && raycastHit.distance < num)
			{
				num = raycastHit.distance;
				collider = raycastHit.collider;
			}
		}
		if (collider == null)
		{
			return -1;
		}
		string text = collider.name;
		if (text.StartsWith("MechRing_") && int.TryParse(text.Substring(9), out var result))
		{
			return RingByRadius(result);
		}
		latchOrSpindle = text;
		return -1;
	}

	private int RingByRadius(int fallback)
	{
		Camera main = Camera.main;
		if (main == null || ringNodes[0] == null)
		{
			return fallback;
		}
		Vector2 b = main.WorldToScreenPoint(ringNodes[0].position);
		float num = Vector2.Distance(Input.mousePosition, b);
		float[] array = new float[3];
		for (int i = 0; i < 3; i++)
		{
			if (ringNodes[i] == null)
			{
				return fallback;
			}
			RingGeometry(ringNodes[i], out var radius, out var _);
			Vector3 position = ringNodes[i].TransformPoint(new Vector3(radius, 0f, 0f));
			array[i] = Vector2.Distance(main.WorldToScreenPoint(position), b);
		}
		if (num <= array[2] * 1.05f)
		{
			return 2;
		}
		if (num <= array[1] * 1.03f)
		{
			return 1;
		}
		return 0;
	}

	private float MouseAngleAroundRings()
	{
		Camera main = Camera.main;
		Vector2 vector = ((main != null && ringNodes[0] != null) ? ((Vector2)main.WorldToScreenPoint(ringNodes[0].position)) : new Vector2((float)Screen.width / 2f, (float)Screen.height / 2f));
		Vector2 vector2 = (Vector2)Input.mousePosition - vector;
		return Mathf.Atan2(vector2.y, vector2.x) * 57.29578f;
	}

	private void Update()
	{
		if (!active)
		{
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if (!puzzleSolved)
		{
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				string latchOrSpindle;
				int num = RingUnderMouse(out latchOrSpindle);
				dragStartMouse = Input.mousePosition;
				dragMoved = false;
				int result;
				if (num >= 0)
				{
					dragRing = num;
					selectedRing = num;
					ActiveRingSelection = num;
					dragLastAngle = MouseAngleAroundRings();
					dragAccum = 0f;
				}
				else if (latchOrSpindle != null && latchOrSpindle.StartsWith("MechLatch_") && int.TryParse(latchOrSpindle.Substring(10), out result))
				{
					ToggleLatch(result);
				}
				else if (latchOrSpindle == "MechSpindle")
				{
					AttemptUnlockSpindle();
				}
			}
			if (dragRing >= 0 && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
			{
				float target = MouseAngleAroundRings();
				float degrees = Mathf.DeltaAngle(dragLastAngle, target);
				dragLastAngle = target;
				if (Vector2.Distance(Input.mousePosition, dragStartMouse) > 6f)
				{
					dragMoved = true;
				}
				if (dragMoved)
				{
					ApplyRingDrag(dragRing, degrees);
				}
			}
			if (dragRing >= 0 && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)))
			{
				if (!dragMoved)
				{
					RotateRing(dragRing, (!Input.GetMouseButtonUp(1)) ? 1 : (-1));
				}
				dragRing = -1;
			}
			float axis = Input.GetAxis("Mouse ScrollWheel");
			if (Mathf.Abs(axis) > 0.01f)
			{
				string latchOrSpindle2;
				int num2 = RingUnderMouse(out latchOrSpindle2);
				if (num2 >= 0)
				{
					selectedRing = num2;
					RotateRing(num2, (!(axis > 0f)) ? 1 : (-1));
				}
			}
			if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				selectedRing = Mathf.Max(0, selectedRing - 1);
			}
			if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				selectedRing = Mathf.Min(2, selectedRing + 1);
			}
			ActiveRingSelection = selectedRing;
			if (Input.GetKeyDown(KeyCode.LeftArrow))
			{
				RotateRing(selectedRing, -1);
			}
			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				RotateRing(selectedRing, 1);
			}
			if (Input.GetKeyDown(KeyCode.Alpha1))
			{
				ToggleLatch(0);
			}
			if (Input.GetKeyDown(KeyCode.Alpha2))
			{
				ToggleLatch(1);
			}
			if (Input.GetKeyDown(KeyCode.Alpha3))
			{
				ToggleLatch(2);
			}
			if (EasyInputBridge.Down(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
			{
				AttemptUnlockSpindle();
			}
			if (AssistReady != assistShown)
			{
				assistShown = AssistReady;
				RefreshPuzzleUI();
			}
			if (AssistReady && Input.GetKeyDown(KeyCode.H))
			{
				ExternalStartAICalibration();
			}
		}
		AnimateParts(unscaledDeltaTime);
		if (puzzleSolved)
		{
			solveAnimTimer -= unscaledDeltaTime;
			if (solveAnimTimer <= 0f)
			{
				Action action = solvedCallback;
				CleanupModal(solved: true);
				action?.Invoke();
			}
		}
		else if (EasyInputBridge.Down(KeyCode.Escape))
		{
			CleanupModal(solved: false);
		}
	}

	private void AnimateParts(float dt)
	{
		bool flag = false;
		for (int i = 0; i < 3; i++)
		{
			float target = ((puzzleSolved || i == selectedRing) ? 1f : 0.62f);
			float num = Mathf.MoveTowards(ringFocus[i], target, dt * 3f);
			if (!Mathf.Approximately(num, ringFocus[i]))
			{
				ringFocus[i] = num;
				flag = true;
			}
		}
		if (flag || puzzleSolved)
		{
			ApplyRingFocus();
		}
		if (UiSignature() != uiSig)
		{
			RefreshPuzzleUI();
		}
		bool flag2 = !stage1Unlocked && !puzzleSolved && Time.unscaledTime - openedAt > 60f;
		for (int j = 0; j < 3; j++)
		{
			if (latchNodes[j] != null)
			{
				float num2 = (latches[j] ? 0.12f : 0f);
				if (flag2 && j == 1)
				{
					num2 += Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 6f)) * 0.025f;
				}
				latchNodes[j].localPosition = Vector3.Lerp(latchNodes[j].localPosition, latchBasePos[j] + new Vector3(0f, num2, 0f), dt * 12f);
				latchNodes[j].localRotation = Quaternion.Slerp(latchNodes[j].localRotation, Quaternion.Euler(0f, latches[j] ? 45f : 0f, 0f), dt * 12f);
			}
			if (ringNodes[j] != null)
			{
				float target2 = (float)(-rings[j]) * 60f;
				float num3 = Mathf.DeltaAngle(ringVisualAngle[j], target2);
				ringVisualAngle[j] += num3 * Mathf.Min(1f, dt * 12f);
				ringNodes[j].localRotation = Quaternion.Euler(0f, 0f, ringVisualAngle[j]);
			}
			if (gearNodes[j] != null)
			{
				float z = (ringVisualAngle[0] - ringVisualAngle[1]) * 0.75f * ((j % 2 == 0) ? 1f : (-1f));
				gearNodes[j].localRotation = Quaternion.Euler(0f, 0f, z);
			}
		}
		if (coreSpindleNode != null)
		{
			spindlePush = Mathf.MoveTowards(spindlePush, 0f, dt * 3f);
			float num4 = (puzzleSolved ? 0.09f : (spindlePush * 0.035f));
			coreSpindleNode.localPosition = Vector3.Lerp(coreSpindleNode.localPosition, spindleBasePos + new Vector3(0f, 0f, 0f - num4), dt * 14f);
			coreSpindleNode.localRotation = Quaternion.Slerp(coreSpindleNode.localRotation, Quaternion.Euler(0f, 0f, puzzleSolved ? 180f : (stage1Unlocked ? 30f : 0f)), dt * 10f);
		}
		if (modelPivot != null)
		{
			shakeTimer = Mathf.Max(0f, shakeTimer - dt);
			float num5 = ((shakeTimer > 0f) ? (Mathf.Sin(Time.unscaledTime * 70f) * 3.5f * Mathf.Clamp01(shakeTimer / 0.3f)) : 0f);
			float num6 = (puzzleSolved ? Mathf.Clamp01(1f - solveAnimTimer / 1.1f) : 0f);
			modelPivot.localRotation = Quaternion.Euler(6f - num6 * 10f, 180f + num5, num5 * 0.4f);
			modelPivot.localPosition = new Vector3(0f, num6 * 0.05f, 0f);
		}
	}

	private void CleanupModal(bool solved)
	{
		active = false;
		dragRing = -1;
		StopAllCoroutines();
		TeardownOverlayCamera();
		if (puzzleRig != null)
		{
			UnityEngine.Object.Destroy(puzzleRig);
		}
		puzzleRig = null;
		modelPivot = null;
		pointerNode = null;
		for (int i = 0; i < 3; i++)
		{
			latchNodes[i] = null;
			ringNodes[i] = null;
			gearNodes[i] = null;
			ringNumbers[i].Clear();
			ringNumberShadows[i].Clear();
			ringPaints[i].Clear();
			if (ringEdges[i] != null)
			{
				MeshFilter component = ringEdges[i].GetComponent<MeshFilter>();
				if (component != null && component.sharedMesh != null)
				{
					UnityEngine.Object.Destroy(component.sharedMesh);
				}
			}
			ringEdges[i] = null;
		}
		coreSpindleNode = null;
		if (noteCard != null)
		{
			noteCard.gameObject.SetActive(value: false);
		}
		if (restoreChase && chaseCam != null)
		{
			chaseCam.enabled = true;
		}
		restoreChase = false;
		if (controlGate != null && gateToken != null)
		{
			controlGate.Release(gateToken);
			gateToken = null;
		}
		if (eggboy != null)
		{
			eggboy.locked = false;
		}
		if (!solved)
		{
			solvedCallback = null;
		}
	}
}
