using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EggboyUI : MonoBehaviour
{
	public Action<ExprId> OnExpressionPicked;

	public Action OnLetterClosed;

	public Action OnReturnToSafety;

	public bool legacyPrompt = true;

	public bool inputClose = true;

	private Button letterCloseBtn;

	private Button letterPaperCloseBtn;

	private Image letterEnvelope;

	private Text letterTitle;

	private Text letterBody;

	private Text letterSign;

	private Text letterIntro1;

	private Text letterIntro2;

	private Text letterHint;

	private const int RefW = 1280;

	private const int RefH = 720;

	private const float GoalPaddingX = 12f;

	private const float GoalPaddingY = 5f;

	private const float GoalMaxTextWidth = 368f;

	private EggboyPlayer player;

	private IEggboyWorld world;

	private Font font;

	private Canvas canvas;

	private Text goalText;

	private Text promptText;

	private Text lookText;

	private Text tutorialText;

	private Text carryText;

	private Text toastText;

	private Text subtitleText;

	private GameObject promptPanel;

	private GameObject letterOverlay;

	private GameObject tutorialPanel;

	private GameObject carryPanel;

	private GameObject toastPanel;

	private GameObject subtitlePanel;

	private GameObject titlePanel;

	private GameObject pausePanel;

	private GameObject hud;

	private GameObject goalPanel;

	private CanvasGroup keysFade;

	private CanvasGroup goalFade;

	private Text pauseGoal;

	private Text runSetting;

	private Text recenterSetting;

	private float keysRemaining = 12f;

	private float goalRemaining;

	private float firstTutorialRemaining;

	private bool freeStarted;

	private KeyCode firstTutorialKey;

	private Action firstTutorialDone;

	private readonly HashSet<string> cinematicOwners = new HashSet<string>();

	private RectTransform letterContent;

	private RectTransform letterViewport;

	private ScrollRect letterScroll;

	private const float LetterBodyPadding = 12f;

	internal static readonly Color32 LetterBackdropColor = new Color32(20, 41, 48, 88);

	private readonly List<RectTransform> letterRules = new List<RectTransform>();

	private string ruledText;
	private Button replayVoice;
	private string letterVoiceKey;
	public void ResetCinematicForFilming() { cinematicOwners.Clear(); RefreshHud(); }

	private Slider volumeBar1;

	private Slider volumeBar2;

	private Slider sensBar;

	private Text volumeText1;

	private Text volumeText2;

	private Text sensText;

	private Action onTitleStart;

	private bool titleShowing;

	private float volume = 1f;

	private float toastUntil;

	private readonly List<Image> faceBg = new List<Image>();

	private readonly List<Text> faceLabel = new List<Text>();

	private ExprId activeExpr;

	private bool letterTaken;

	private int letterOpenedFrame;

	private Sprite rounded;

	private Sprite white;

	private static readonly Color32 SeaBlue = TideTownStyleUI.SeaBlue;

	private static readonly Color32 Parchment = TideTownStyleUI.PaperColor;

	private static readonly Color32 Eyebrow = TideTownStyleUI.InkMuted;

	private static readonly Color32 Gold = new Color32(224, 180, 114, 235);

	private static readonly Color32 Cinnabar = TideTownStyleUI.Clay;

	private static readonly Color32 InkDark = TideTownStyleUI.InkDark;

	private const float GoalTextLeft = 52f;

	private const float GoalTextTop = 29f;

	private Text goalEyebrow;

	private Text promptKeyText;

	private Text promptAction;

	private Text titleSettingsText;

	private Image promptInner;

	private Image promptKey;

	private bool promptPrimary = true;

	private bool titleDrawerOpen;

	private float titleDrawerT;

	private RectTransform letterPaperRect;

	private CanvasGroup titleDrawer;

	private bool externalTitle;

	public static bool PersistentGoal = true;

	private RectTransform guideRoot;

	private RectTransform trackChip;

	private RectTransform trackArrow;

	private RectTransform waypoint;

	private RectTransform waypointArrow;

	private RectTransform basicsCard;

	private RectTransform basicsKeys;

	private CanvasGroup trackChipFade;

	private CanvasGroup waypointFade;

	private CanvasGroup basicsFade;

	private Text trackChipText;

	private Text waypointText;

	private Text basicsEyebrow;

	private Text basicsText;

	private Text basicsSkip;

	private Text basicsCheck;

	private Image waypointRing;

	private bool trackWanted;

	private bool waypointWanted;

	private bool basicsWanted;

	private float basicsPop;

	private const float OpenDuration = 1.32f;

	private const float StampContact = 0.66f;

	private const float StampDuration = 2.8f;

	private const float PostmarkX = 76f;

	private const float PostmarkY = -34f;

	private readonly Queue<string> stampQueue = new Queue<string>();

	private ICollection<string> collectedPostmarks;

	private string letterDelivery;

	private string openedDelivery;

	private string stampingDelivery;

	private CanvasGroup opening;

	private CanvasGroup stampCard;

	private Image openingEnvelope;

	private Image openingFlap;

	private Image openingMouth;

	private Image openingPocket;

	private Image openingPocketTrim;

	private Image openingInk;

	private Image stampedEnvelope;

	private Image stampInk;

	private Image stampHandle;

	private Image letterEnvelopeInk;

	private RectTransform unfoldingSlip;

	private Text openingTitle;

	private Text stampTitle;

	private float openingTime;

	private float stampingTime;

	public bool LetterOpen { get; private set; }

	public Text LetterBodyText => letterBody;

	public bool Cinematic => cinematicOwners.Count > 0;

	public bool FirstTutorialActive => firstTutorialDone != null;

	public bool ToastVisible
	{
		get
		{
			if (toastPanel != null)
			{
				return toastPanel.activeInHierarchy;
			}
			return false;
		}
	}

	public bool Paused { get; private set; }

	public bool TitleShowing => titleShowing;

	public float Volume => volume;

	public string GoalText
	{
		get
		{
			if (!(goalText != null))
			{
				return "";
			}
			return goalText.text;
		}
	}

	public string PromptText
	{
		get
		{
			if (!(promptText != null) || !(promptPanel != null) || !promptPanel.activeSelf)
			{
				return "";
			}
			return promptText.text;
		}
	}

	public string SubtitleText
	{
		get
		{
			if (!(subtitleText != null) || !(subtitlePanel != null) || !subtitlePanel.activeSelf)
			{
				return "";
			}
			return subtitleText.text;
		}
	}

	public RectTransform HudRect
	{
		get
		{
			if (!(hud != null))
			{
				return null;
			}
			return (RectTransform)hud.transform;
		}
	}

	public bool HudVisible
	{
		get
		{
			if (hud != null)
			{
				return hud.activeInHierarchy;
			}
			return false;
		}
	}

	internal bool LetterOpening
	{
		get
		{
			if (opening != null)
			{
				return opening.gameObject.activeSelf;
			}
			return false;
		}
	}

	internal bool PostalStampVisible
	{
		get
		{
			if (stampCard != null && stampCard.gameObject.activeSelf)
			{
				return stampingTime >= 0.66f;
			}
			return false;
		}
	}

	public void Init(EggboyPlayer p, IEggboyWorld w)
	{
		player = p;
		world = w;
		font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
		if (font == null)
		{
			font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		if (font == null)
		{
			font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		}
		rounded = MakeRounded();
		white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
		Build();
		BuildGuideHud();
		SetGoal("目标：去山口的红色邮筒取信（往左走）");
		SetActiveExpression(ExprId.happy);
		SetLetterTaken(taken: false);
	}

	private void Build()
	{
		if (EventSystem.current == null)
		{
			new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
		}
		GameObject gameObject = new GameObject("EGGBOY HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		canvas = gameObject.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 20;
		CanvasScaler component = gameObject.GetComponent<CanvasScaler>();
		component.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		component.referenceResolution = new Vector2(1280f, 720f);
		component.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
		RectTransform component2 = gameObject.GetComponent<RectTransform>();
		Debug.Log($"UI canvas {component2.rect.width}x{component2.rect.height} screen {Screen.width}x{Screen.height} scale {component.scaleFactor}");
		RectTransform component3 = new GameObject("GameplayHUD", typeof(RectTransform)).GetComponent<RectTransform>();
		component3.SetParent(component2, worldPositionStays: false);
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		Vector2 offsetMin = (component3.offsetMax = Vector2.zero);
		component3.offsetMin = offsetMin;
		hud = component3.gameObject;
		RectTransform rectTransform = component2;
		component2 = component3;
		Image image = Panel(component2, 22f, -22f, 0f, 0f, Parchment);
		image.sprite = TideTownStyleUI.PaperSprite;
		image.raycastTarget = false;
		goalPanel = image.gameObject;
		goalFade = goalPanel.AddComponent<CanvasGroup>();
		AddDropShadow(image);
		Image image2 = Img(image.transform, "RouteStamp", TideTownStyleUI.PostmarkRingSprite, SeaBlue);
		Pin(image2.rectTransform, 0f, 0.5f, 26f, 0f, 36f, 36f);
		image2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
		goalEyebrow = Label(goalPanel.transform, "投递路单", 12, SeaBlue, TextAnchor.UpperLeft, 52f, -10f, 180f, 16f, bold: true);
		goalText = Label(goalPanel.transform, "", 17, InkDark, TextAnchor.MiddleLeft, 52f, -29f, 368f, 22f, bold: false);
		Image image3 = Panel(component2, 22f, -120f, 584f, 42f, new Color32(229, 236, 217, byte.MaxValue));
		image3.sprite = TideTownStyleUI.PaperSprite;
		AddDropShadow(image3);
		tutorialText = Label(image3.transform, "", 15, InkDark, TextAnchor.MiddleLeft, 16f, -3f, 552f, 36f, bold: false);
		tutorialPanel = image3.gameObject;
		tutorialPanel.SetActive(value: false);
		Image image4 = Panel(component2, 22f, -174f, 300f, 34f, Parchment);
		image4.sprite = TideTownStyleUI.PaperSprite;
		AddDropShadow(image4);
		Pin(Img(image4.transform, "CarryDot", TideTownStyleUI.PillSprite, Cinnabar).rectTransform, 0f, 0.5f, 16f, 0f, 10f, 10f);
		carryText = Label(image4.transform, "", 14, InkDark, TextAnchor.MiddleLeft, 30f, -4f, 260f, 24f, bold: false);
		carryPanel = image4.gameObject;
		carryPanel.SetActive(value: false);
		Image image5 = Panel(component2, -18f, -14f, 150f, 32f, new Color32(59, 42, 26, 210), round: true, right: true);
		lookText = Label(image5.transform, "渲染：v2", 14, new Color32(251, 239, 217, byte.MaxValue), TextAnchor.MiddleCenter, 0f, 0f, 130f, 28f, bold: false);
		SetRect(lookText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 130f, 28f);
		if (!Features.debugUi)
		{
			image5.gameObject.SetActive(value: false);
		}
		Image image6 = Panel(component2, -18f, -54f, 248f, 30f, new Color32(59, 42, 26, 210), round: true, right: true);
		Text faceTitleText = Label(image6.transform, "表情 ▾", 14, new Color32(251, 239, 217, byte.MaxValue), TextAnchor.MiddleLeft, 10f, -3f, 200f, 24f, bold: false);
		Image gridPanel = Panel(component2, -18f, -88f, 248f, 184f, new Color32(59, 42, 26, 210), round: true, right: true);
		Button button = image6.gameObject.AddComponent<Button>();
		button.targetGraphic = image6;
		button.transition = Selectable.Transition.None;
		bool facesOpen = Features.debugUi;
		image6.gameObject.SetActive(Features.debugUi);
		gridPanel.gameObject.SetActive(facesOpen);
		button.onClick.AddListener(delegate
		{
			facesOpen = !facesOpen;
			gridPanel.gameObject.SetActive(facesOpen);
			faceTitleText.text = (facesOpen ? "表情 ▾" : "表情 ▸");
			Debug.Log("facesOpen " + facesOpen);
		});
		GridLayoutGroup gridLayoutGroup = gridPanel.gameObject.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.cellSize = new Vector2(56f, 40f);
		gridLayoutGroup.spacing = new Vector2(4f, 4f);
		gridLayoutGroup.padding = new RectOffset(6, 6, 6, 6);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 4;
		gridLayoutGroup.childAlignment = TextAnchor.UpperCenter;
		for (int i = 0; i < EggboyExpressions.All.Length; i++)
		{
			ExprId id = EggboyExpressions.All[i];
			GameObject gameObject2 = new GameObject("Face_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
			gameObject2.transform.SetParent(gridPanel.transform, worldPositionStays: false);
			Image component4 = gameObject2.GetComponent<Image>();
			component4.sprite = rounded;
			component4.type = Image.Type.Sliced;
			component4.color = new Color32(byte.MaxValue, 248, 236, byte.MaxValue);
			Button component5 = gameObject2.GetComponent<Button>();
			component5.targetGraphic = component4;
			component5.transition = Selectable.Transition.ColorTint;
			component5.colors = new ColorBlock
			{
				normalColor = new Color32(byte.MaxValue, 248, 236, byte.MaxValue),
				highlightedColor = new Color32(byte.MaxValue, 223, 192, byte.MaxValue),
				pressedColor = new Color32(byte.MaxValue, 154, 77, byte.MaxValue),
				selectedColor = new Color32(byte.MaxValue, 248, 236, byte.MaxValue),
				disabledColor = new Color32(204, 204, 204, byte.MaxValue),
				colorMultiplier = 1f,
				fadeDuration = 0.08f
			};
			Text text = Label(gameObject2.transform, EggboyExpressions.Names[i], 13, new Color32(59, 42, 26, byte.MaxValue), TextAnchor.MiddleCenter, 0f, 0f, 54f, 36f, bold: false);
			SetRect(text.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 54f, 36f);
			component5.onClick.AddListener(delegate
			{
				OnExpressionPicked?.Invoke(id);
				SetActiveExpression(id);
			});
			faceBg.Add(component4);
			faceLabel.Add(text);
		}
		Image image7 = Panel(component2, 0f, 40f, 654f, 56f, Parchment, round: true, right: true);
		image7.sprite = TideTownStyleUI.PaperSprite;
		keysFade = image7.gameObject.AddComponent<CanvasGroup>();
		keysFade.blocksRaycasts = false;
		SetRect(image7.rectTransform, 0.5f, 0f, 0.5f, 0f, 0f, 42f, 654f, 56f);
		AddDropShadow(image7);
		Txt(image7.transform, "WASD 移动\u3000 Shift 慢走 / 迎风稳住\u3000 空格 跳\u3000 E 互动\n鼠标转视角\u3000 滚轮缩放", 15, InkDark, TextAnchor.MiddleCenter, 0f, 0f, 624f, 46f, bold: false);
		Image image8 = Img(component2, "PostalShortcuts", TideTownStyleUI.PaperSprite, Parchment);
		Pin(image8.rectTransform, 1f, 0f, -160f, 32f, 276f, 42f);
		AddDropShadow(image8);
		float num = -124f;
		(string, string)[] array = new(string, string)[3]
		{
			("M", "地图"),
			("H", "提示"),
			("Esc", "暂停")
		};
		for (int j = 0; j < array.Length; j++)
		{
			(string, string) tuple = array[j];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			RectTransform rectTransform2 = Keycap(image8.transform, item, num, 2f, 26f, 13, leftEdge: true);
			num += rectTransform2.sizeDelta.x + 5f;
			Txt(image8.transform, item2, 15, InkDark, TextAnchor.MiddleLeft, num + 17f, 0f, 34f, 26f, bold: true);
			num += 44f;
		}
		Image image9 = Panel(component2, 0f, 106f, 340f, 46f, new Color32(201, 168, 124, byte.MaxValue), round: true, right: true);
		image9.sprite = TideTownStyleUI.PillSprite;
		promptPanel = image9.gameObject;
		promptPanel.name = "PromptPanel";
		SetRect(image9.rectTransform, 0.5f, 0f, 0.5f, 0f, 0f, 106f, 340f, 46f);
		AddDropShadow(image9);
		promptInner = Inset(promptPanel.transform, Parchment, 3f, TideTownStyleUI.PillSprite);
		promptText = Label(promptPanel.transform, "", 16, InkDark, TextAnchor.MiddleCenter, 0f, 0f, 320f, 34f, bold: true);
		SetRect(promptText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 320f, 34f);
		promptText.enabled = false;
		promptKey = Img(promptPanel.transform, "PromptKeycap", TideTownStyleUI.KeycapSprite, Color.white);
		promptKey.type = Image.Type.Sliced;
		promptKeyText = Label(promptKey.transform, "E", 15, InkDark, TextAnchor.MiddleCenter, 0f, 0f, 60f, 30f, bold: true);
		promptAction = Label(promptPanel.transform, "", 17, InkDark, TextAnchor.MiddleLeft, 0f, 0f, 300f, 30f, bold: true);
		promptPanel.SetActive(value: false);
		Image image10 = Panel(component2, 0f, 0f, 460f, 44f, Parchment, round: true, right: true);
		image10.sprite = TideTownStyleUI.PaperSprite;
		SetRect(image10.rectTransform, 0.5f, 0f, 0.5f, 0f, 0f, 158f, 460f, 44f);
		AddDropShadow(image10);
		Pin(Img(image10.transform, "ToastSeal", TideTownStyleUI.PostmarkRingSprite, Cinnabar).rectTransform, 0f, 0.5f, 26f, 0f, 26f, 26f);
		toastText = Label(image10.transform, "", 16, InkDark, TextAnchor.MiddleCenter, 0f, 0f, 400f, 32f, bold: false);
		SetRect(toastText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 6f, 0f, 396f, 32f);
		toastPanel = image10.gameObject;
		toastPanel.SetActive(value: false);
		Image image11 = Panel(rectTransform, 0f, 0f, 980f, 70f, new Color32(30, 20, 12, 195), round: false, right: true);
		SetRect(image11.rectTransform, 0.5f, 0f, 0.5f, 0f, 0f, 214f, 980f, 70f);
		HairLine(image11.transform, new Color32(224, 180, 114, 150), 1f, 0f, 40f);
		HairLine(image11.transform, new Color32(224, 180, 114, 150), 0f, 0f, 40f);
		subtitleText = Label(image11.transform, "", 22, new Color32(251, 239, 217, byte.MaxValue), TextAnchor.MiddleCenter, 0f, 0f, 900f, 56f, bold: false);
		SetRect(subtitleText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 900f, 56f);
		subtitleText.gameObject.AddComponent<Shadow>().effectColor = new Color32(0, 0, 0, 120);
		subtitlePanel = image11.gameObject;
		subtitlePanel.SetActive(value: false);
		BuildLetter(rectTransform);
		BuildMenus(rectTransform);
		BuildPostalAnimation(rectTransform);
	}

	private void BuildLetter(RectTransform root)
	{
		Image image = Panel(root, 0f, 0f, 1280f, 720f, LetterBackdropColor, round: false, right: true);
		SetRect(image.rectTransform, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
		letterOverlay = image.gameObject;
		letterOverlay.name = "LetterOverlay";
		letterCloseBtn = image.gameObject.AddComponent<Button>();
		letterCloseBtn.targetGraphic = image;
		letterCloseBtn.transition = Selectable.Transition.None;
		letterCloseBtn.onClick.AddListener(delegate
		{
			if (LetterOpening)
			{
				FinishLetterOpening();
			}
			else if (!RectTransformUtility.RectangleContainsScreenPoint(letterPaperRect, Input.mousePosition))
			{
				CloseLetter();
			}
		});
		letterOverlay.SetActive(value: false);
		Image image2 = Img(image.transform, "LetterPaper", TideTownStyleUI.PaperSprite, Parchment);
		letterPaperRect = image2.rectTransform;
		Pin(image2.rectTransform, 0.5f, 0.5f, -40f, -6f, 560f, 620f);
		image2.raycastTarget = true;
		AddDropShadow(image2);
		letterEnvelope = Img(image2.transform, "LetterEnvelope", PostalArtwork.Envelope("bakery"), Color.white);
		Pin(letterEnvelope.rectTransform, 0.5f, 0.5f, 315f, -52f, 265f, 190f);
		letterEnvelope.preserveAspect = true;
		letterEnvelope.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
		AddDropShadow(letterEnvelope);
		letterEnvelopeInk = Img(letterEnvelope.transform, "DeliveredPostmark", null, Color.white);
		Pin(letterEnvelopeInk.rectTransform, 0.5f, 0.5f, 75f, -17f, 89f, 69f);
		letterEnvelopeInk.preserveAspect = true;
		letterEnvelopeInk.enabled = false;
		Inset(image2.transform, Parchment, 0f, TideTownStyleUI.PaperSprite);
		Frame(image2.transform, new Color32(192, 171, 133, 132), 12f);
		letterIntro1 = Label(image2.transform, "邮筒里的三封信", 14, SeaBlue, TextAnchor.UpperLeft, 30f, -24f, 360f, 24f, bold: true);
		letterIntro2 = Label(image2.transform, "· 致 面包店的烤炉\n· 致 小黑\n· 致 灯塔", 14, Eyebrow, TextAnchor.UpperLeft, 30f, -50f, 330f, 62f, bold: false);
		letterTitle = Label(image2.transform, "面包店的烤炉", 27, SeaBlue, TextAnchor.UpperLeft, 30f, -119f, 500f, 39f, bold: true);
		letterBody = Label(image2.transform, "老伙计：\n\n四十二年，你每天早上五点就热起来，比我还早。\n搬家的卡车装不下你，我只带走了你烤的最后一炉面包，路上就吃完了。\n新城的面包都用电炉烤，没有味道。\n\n邮差，如果你读到这里，请帮我再生一次火。\n不用烤什么，让它暖一暖就好。", 18, InkDark, TextAnchor.UpperLeft, 30f, -162f, 500f, 312f, bold: false);
		letterSign = Label(image2.transform, "——林阿婆", 18, InkDark, TextAnchor.UpperRight, 30f, -485f, 500f, 30f, bold: false);
		letterPaperCloseBtn = TideTownStyleUI.CreateWarmButton(image2.transform, "收起信 · E", new Vector2(0f, -276f), new Vector2(188f, 32f), primaryAmber: false, CloseLetter);
		letterPaperCloseBtn.name = "LetterHintBar";
		letterHint = letterPaperCloseBtn.GetComponentInChildren<Text>();
		GameObject gameObject = new GameObject("LetterBodyViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
		letterViewport = gameObject.GetComponent<RectTransform>();
		letterViewport.SetParent(image2.transform, worldPositionStays: false);
		SetRect(letterViewport, 0f, 1f, 0f, 1f, 30f, -162f, 500f, 312f);
		letterViewport.pivot = new Vector2(0f, 1f);
		gameObject.GetComponent<Image>().color = Color.clear;
		letterContent = new GameObject("LetterContent", typeof(RectTransform)).GetComponent<RectTransform>();
		letterContent.SetParent(letterViewport, worldPositionStays: false);
		SetRect(letterContent, 0f, 1f, 0f, 1f, 0f, 0f, 500f, 312f);
		letterContent.pivot = new Vector2(0f, 1f);
		letterBody.transform.SetParent(letterContent, worldPositionStays: false);
		letterBody.rectTransform.anchoredPosition = new Vector2(0f, -12f);
		letterScroll = gameObject.GetComponent<ScrollRect>();
		letterScroll.viewport = letterViewport;
		letterScroll.content = letterContent;
		letterScroll.horizontal = false;
		letterScroll.movementType = ScrollRect.MovementType.Clamped;
		letterScroll.scrollSensitivity = 24f;
	}

	private void BuildMenus(RectTransform root)
	{
		volume = PlayerPrefs.GetFloat("tidetown.volume", 1f);
		AudioListener.volume = volume;
		BuildTitleCover(root);
		BuildHandbook(root);
		RefreshSettings();
		ApplyVolume(volume);
		ApplySensitivity(ChaseCamera.Sensitivity);
	}

	private void BuildTitleCover(RectTransform root)
	{
		Image image = Img(root, "TitleBackdrop", TideTownStyleUI.SoftVignetteSprite, new Color32(16, 37, 44, 205));
		Stretch(image.rectTransform, 0f);
		image.raycastTarget = true;
		titlePanel = image.gameObject;
		Image image2 = Img(image.transform, "PostcardBacking", TideTownStyleUI.PaperSprite, SeaBlue);
		Pin(image2.rectTransform, 0f, 0.5f, 306f, -6f, 518f, 532f);
		image2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
		AddDropShadow(image2);
		Image image3 = Img(image.transform, "TitleCoverCard", TideTownStyleUI.PaperSprite, Parchment);
		Pin(image3.rectTransform, 0f, 0.5f, 300f, 0f, 518f, 532f);
		AddDropShadow(image3);
		Frame(image3.transform, new Color32(203, 160, 109, 145), 11f);
		Transform parent = image3.transform;
		Image image4 = Img(parent, "PostcardIllustration", PostalArtwork.Postcard, Color.white);
		image4.preserveAspect = true;
		Pin(image4.rectTransform, 0.5f, 0.5f, 0f, 125f, 354f, 239f);
		image4.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2.5f);
		AddDropShadow(image4);
		Image image5 = Img(parent, "PostcardTape", TideTownStyleUI.PaperSprite, new Color32(163, 206, 208, 229));
		Pin(image5.rectTransform, 0.5f, 0.5f, -70f, 237f, 110f, 23f);
		image5.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
		Image image6 = Img(parent, "TitlePostmark", TideTownStyleUI.PostmarkRingSprite, new Color32(165, 75, 53, 180));
		Pin(image6.rectTransform, 0.5f, 0.5f, 183f, 32f, 78f, 78f);
		image6.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -13f);
		Txt(parent, "记忆邮局", 19, Cinnabar, TextAnchor.MiddleLeft, 0f, -12f, 424f, 30f, bold: true);
		Txt(parent, "潮汐镇", 57, SeaBlue, TextAnchor.MiddleLeft, 0f, -57f, 424f, 67f, bold: true);
		Txt(parent, "EGGBOY · STILL DELIVERING", 15, Eyebrow, TextAnchor.MiddleLeft, 0f, -108f, 424f, 24f, bold: true);
		Button button = TideTownStyleUI.CreateWarmButton(parent, "启程派件", new Vector2(-91f, -153f), new Vector2(242f, 48f), primaryAmber: true, StartFromTitle);
		button.name = "StartButton";
		Text componentInChildren = button.GetComponentInChildren<Text>();
		componentInChildren.fontSize = 20;
		componentInChildren.rectTransform.anchoredPosition = new Vector2(34f, 0f);
		componentInChildren.rectTransform.sizeDelta = new Vector2(152f, 40f);
		Keycap(button.transform, "Enter", -104f, 0f, 30f, 13, leftEdge: true);
		Button button2 = TideTownStyleUI.CreateWarmButton(parent, "邮差设置", new Vector2(136f, -153f), new Vector2(152f, 48f), primaryAmber: false, delegate
		{
			titleDrawerOpen = !titleDrawerOpen;
			titleSettingsText.text = (titleDrawerOpen ? "收起设置" : "邮差设置");
		});
		button2.name = "SettingsButton";
		titleSettingsText = button2.GetComponentInChildren<Text>();
		Image image7 = Img(parent, "SettingsDrawer", TideTownStyleUI.PaperSprite, TideTownStyleUI.SeaMist);
		Pin(image7.rectTransform, 0.5f, 0.5f, 0f, -217f, 424f, 60f);
		titleDrawer = image7.gameObject.AddComponent<CanvasGroup>();
		titleDrawer.alpha = 0f;
		CanvasGroup canvasGroup = titleDrawer;
		bool interactable = (titleDrawer.blocksRaycasts = false);
		canvasGroup.interactable = interactable;
		volumeText1 = Txt(image7.transform, "音量", 14, InkDark, TextAnchor.MiddleLeft, 0f, 16f, 380f, 20f, bold: true);
		volumeBar1 = BuildVolume(image7.transform, -10f, 380f);
		titlePanel.SetActive(value: false);
	}

	private void BuildHandbook(RectTransform root)
	{
		Image image = Img(root, "PauseBackdrop", null, new Color32(16, 34, 40, 125));
		Stretch(image.rectTransform, 0f);
		image.raycastTarget = true;
		Stretch(Img(image.transform, "PauseVignette", TideTownStyleUI.SoftVignetteSprite, new Color32(18, 37, 43, 180)).rectTransform, 0f);
		pausePanel = image.gameObject;
		Image image2 = Img(image.transform, "Handbook", TideTownStyleUI.PaperSprite, SeaBlue);
		Pin(image2.rectTransform, 0.5f, 0.5f, 0f, 0f, 920f, 580f);
		Shadow shadow = image2.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color32(0, 0, 0, 130);
		shadow.effectDistance = new Vector2(6f, -9f);
		Frame(image2.transform, new Color32(178, 218, 209, 160), 8f);
		Pin(Img(image2.transform, "Spine", null, new Color32(33, 77, 87, byte.MaxValue)).rectTransform, 0.5f, 0.5f, 0f, 0f, 16f, 548f);
		Image image3 = Img(image2.transform, "LeftPage", TideTownStyleUI.PaperSprite, Parchment);
		Pin(image3.rectTransform, 0.5f, 0.5f, -218f, 0f, 420f, 530f);
		Image image4 = Img(image2.transform, "RightPage", TideTownStyleUI.PaperSprite, Parchment);
		Pin(image4.rectTransform, 0.5f, 0.5f, 218f, 0f, 420f, 530f);
		Pin(Img(image3.transform, "Gutter", null, new Color32(106, 72, 40, 40)).rectTransform, 1f, 0.5f, -5f, 0f, 10f, 510f);
		Pin(Img(image4.transform, "Gutter", null, new Color32(106, 72, 40, 40)).rectTransform, 0f, 0.5f, 5f, 0f, 10f, 510f);
		Transform parent = image3.transform;
		Txt(parent, "投递手账", 26, SeaBlue, TextAnchor.MiddleLeft, 0f, 238f, 360f, 35f, bold: true);
		Pin(Img(parent, "Rule", null, new Color32(201, 168, 120, byte.MaxValue)).rectTransform, 0.5f, 0.5f, 0f, 218f, 360f, 1f);
		Image image5 = Img(parent, "RouteCard", TideTownStyleUI.PaperSprite, new Color32(229, 236, 217, byte.MaxValue));
		Pin(image5.rectTransform, 0.5f, 0.5f, 0f, 166f, 360f, 82f);
		Strip(image5.transform, SeaBlue, 8f, 4f, 10f);
		Txt(image5.transform, "当前任务", 13, SeaBlue, TextAnchor.MiddleLeft, 12f, 26f, 300f, 18f, bold: true);
		pauseGoal = Txt(image5.transform, GoalText, 15, InkDark, TextAnchor.UpperLeft, 12f, -10f, 300f, 46f, bold: false);
		Txt(parent, "邮差设置", 12, Eyebrow, TextAnchor.MiddleLeft, 0f, 108f, 360f, 18f, bold: true);
		volumeText2 = Txt(parent, "音量", 13, InkDark, TextAnchor.MiddleLeft, 0f, 86f, 340f, 20f, bold: false);
		volumeBar2 = BuildVolume(parent, 66f, 340f);
		sensText = Txt(parent, "视角灵敏度", 13, InkDark, TextAnchor.MiddleLeft, 0f, 42f, 340f, 20f, bold: false);
		sensBar = BuildSensitivity(parent, 22f, 340f);
		runSetting = SettingButton(parent, -18f, delegate
		{
			PrototypeGame.DefaultRun = !PrototypeGame.DefaultRun;
			RefreshSettings();
		});
		recenterSetting = SettingButton(parent, -58f, delegate
		{
			ChaseCamera.AutoRecenter = !ChaseCamera.AutoRecenter;
			RefreshSettings();
		});
		RectTransform component = new GameObject("PauseGuideSlot", typeof(RectTransform)).GetComponent<RectTransform>();
		component.SetParent(parent, worldPositionStays: false);
		Pin(component, 0.5f, 0.5f, 0f, -98f, 340f, 32f);
		Txt(parent, "暂停时按 H 切换提示频率", 13, Eyebrow, TextAnchor.MiddleLeft, 0f, -133f, 340f, 18f, bold: false);
		Text text = SettingButton(parent, -161f, delegate
		{
			OnReturnToSafety?.Invoke();
		});
		text.transform.parent.name = "PauseReturnToSafety";
		text.text = "返回安全位置";
		Button button = TideTownStyleUI.CreateWarmButton(parent, "继续派件", new Vector2(0f, -213f), new Vector2(340f, 47f), primaryAmber: true, delegate
		{
			SetPaused(on: false);
		});
		button.name = "ResumeButton";
		Text componentInChildren = button.GetComponentInChildren<Text>();
		componentInChildren.fontSize = 19;
		componentInChildren.rectTransform.anchoredPosition = new Vector2(-21f, 0f);
		Keycap(button.transform, "Esc", 105f, 0f, 29f, 13, leftEdge: false);
		Transform parent2 = image4.transform;
		Txt(parent2, "操作与法宝", 26, SeaBlue, TextAnchor.MiddleLeft, 0f, 238f, 360f, 35f, bold: true);
		Pin(Img(parent2, "Rule", null, new Color32(201, 168, 120, byte.MaxValue)).rectTransform, 0.5f, 0.5f, 0f, 218f, 360f, 1f);
		(string, Color32, (string, string)[])[] obj = new(string, Color32, (string, string)[])[3]
		{
			("基础行动", new Color32(232, 235, 213, byte.MaxValue), new(string, string)[4]
			{
				("WASD", "移动"),
				("Shift", "慢走 / 迎风稳住"),
				("Space", "跳跃"),
				("E", "互动")
			}),
			("邮差法宝", new Color32(245, 225, 196, byte.MaxValue), new(string, string)[4]
			{
				("Tab", "读信与拆字"),
				("V", "心愿造物笺"),
				("R", "潮汐怀表残影"),
				("C", "透视拓印取景框")
			}),
			("观察与伙伴", TideTownStyleUI.SeaMist, new(string, string)[4]
			{
				("F", "指挥小黑"),
				("M", "投递地图"),
				("H", "目标 / 提示频率"),
				("Z/X", "切换表情")
			})
		};
		float num = 194f;
		(string, Color32, (string, string)[])[] array = obj;
		for (int i = 0; i < array.Length; i++)
		{
			var (text2, color, array2) = array[i];
			Pin(Img(parent2, "Section", TideTownStyleUI.PaperSprite, color).rectTransform, 0.5f, 0.5f, 0f, num - 38f, 376f, 100f);
			Pin(Img(parent2, "SectionDot", TideTownStyleUI.PillSprite, Cinnabar).rectTransform, 0.5f, 0.5f, -172f, num, 7f, 7f);
			Txt(parent2, text2, 15, SeaBlue, TextAnchor.MiddleLeft, 0f, num, 330f, 20f, bold: true);
			for (int j = 0; j < array2.Length; j++)
			{
				float num2 = ((j % 2 == 0) ? (-170f) : 14f);
				float y = num - 32f - (float)(j / 2) * 38f;
				RectTransform rectTransform = Keycap(parent2, array2[j].Item1, num2, y, 28f, 12, leftEdge: true);
				Txt(parent2, array2[j].Item2, 13, InkDark, TextAnchor.MiddleLeft, num2 + rectTransform.sizeDelta.x + 8f + 75f, y, 150f, 22f, bold: false);
			}
			num -= 112f;
		}
		Txt(parent2, "鼠标转视角 · 滚轮缩放\n取景框内 Q / Z 换物", 13, Eyebrow, TextAnchor.MiddleLeft, -53f, -189f, 250f, 51f, bold: false);
		Image image6 = Img(parent2, "HandbookPostcard", PostalArtwork.Postcard, Color.white);
		Pin(image6.rectTransform, 0.5f, 0.5f, 126f, -192f, 127f, 92f);
		image6.preserveAspect = true;
		image6.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);
		pausePanel.SetActive(value: false);
	}

	private Image Img(Transform parent, string name, Sprite sprite, Color32 color)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(parent, worldPositionStays: false);
		Image component = obj.GetComponent<Image>();
		component.sprite = ((sprite != null) ? sprite : white);
		component.type = ((sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple);
		component.color = color;
		component.raycastTarget = false;
		return component;
	}

	private static void Pin(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
	{
		Vector2 anchorMin = (rt.anchorMax = new Vector2(ax, ay));
		rt.anchorMin = anchorMin;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = new Vector2(x, y);
		rt.sizeDelta = new Vector2(w, h);
	}

	private static void Stretch(RectTransform rt, float pad)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.offsetMin = new Vector2(pad, pad);
		rt.offsetMax = new Vector2(0f - pad, 0f - pad);
	}

	private Image Inset(Transform parent, Color32 color, float pad, Sprite sprite)
	{
		Image image = Img(parent, "Inner", sprite, color);
		Stretch(image.rectTransform, pad);
		return image;
	}

	private void Strip(Transform parent, Color32 color, float left, float width, float vpad)
	{
		RectTransform rectTransform = Img(parent, "StampStrip", null, color).rectTransform;
		rectTransform.anchorMin = new Vector2(0f, 0f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 0.5f);
		rectTransform.offsetMin = new Vector2(left, vpad);
		rectTransform.offsetMax = new Vector2(left + width, 0f - vpad);
	}

	private void Frame(Transform parent, Color32 color, float pad)
	{
		Stretch(Img(parent, "Frame", TideTownStyleUI.RoundedFrameSprite, color).rectTransform, pad);
	}

	private void HairLine(Transform parent, Color32 color, float anchorY, float inset, float side)
	{
		RectTransform rectTransform = Img(parent, "HairLine", null, color).rectTransform;
		rectTransform.anchorMin = new Vector2(0f, anchorY);
		rectTransform.anchorMax = new Vector2(1f, anchorY);
		rectTransform.pivot = new Vector2(0.5f, anchorY);
		rectTransform.offsetMin = new Vector2(side, (anchorY > 0.5f) ? (0f - inset - 1f) : inset);
		rectTransform.offsetMax = new Vector2(0f - side, (anchorY > 0.5f) ? (0f - inset) : (inset + 1f));
	}

	private static void AddDropShadow(Graphic g)
	{
		Shadow shadow = g.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color32(26, 16, 8, 80);
		shadow.effectDistance = new Vector2(2f, -3f);
	}

	private Text Txt(Transform parent, string text, int size, Color32 color, TextAnchor align, float x, float y, float w, float h, bool bold)
	{
		Text text2 = Label(parent, text, size, color, align, 0f, 0f, w, h, bold);
		SetRect(text2.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, x, y, w, h);
		return text2;
	}

	private RectTransform Keycap(Transform parent, string key, float x, float y, float h, int fontSize, bool leftEdge)
	{
		Image image = Img(parent, "Keycap_" + key, TideTownStyleUI.KeycapSprite, Color.white);
		Text text = Label(image.transform, key, fontSize, InkDark, TextAnchor.MiddleCenter, 0f, 0f, 120f, h, bold: true);
		float num = Mathf.Max(h, Mathf.Ceil(text.preferredWidth) + 16f);
		Pin(image.rectTransform, 0.5f, 0.5f, leftEdge ? (x + num * 0.5f) : x, y, num, h);
		RectTransform rectTransform = text.rectTransform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.offsetMin = new Vector2(0f, h * 0.16f);
		rectTransform.offsetMax = Vector2.zero;
		return image.rectTransform;
	}

	private void AnimateUi()
	{
		float unscaledTime = Time.unscaledTime;
		if (promptKey != null && promptPanel != null && promptPanel.activeSelf)
		{
			promptKey.rectTransform.localScale = Vector3.one * (promptPrimary ? (1f + 0.04f * Mathf.Sin(unscaledTime * 3.2f)) : 1f);
		}
		if (titleDrawer != null)
		{
			titleDrawerT = Mathf.MoveTowards(titleDrawerT, titleDrawerOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.22f);
			float num = Mathf.SmoothStep(0f, 1f, titleDrawerT);
			titleDrawer.alpha = num;
			CanvasGroup canvasGroup = titleDrawer;
			bool interactable = (titleDrawer.blocksRaycasts = num > 0.5f);
			canvasGroup.interactable = interactable;
			titleDrawer.transform.localScale = new Vector3(1f, 0.6f + 0.4f * num, 1f);
		}
	}

	private Text CenterLabel(Transform parent, string text, int size, Color32 color, float y, float h, bool bold)
	{
		Text text2 = Label(parent, text, size, color, TextAnchor.MiddleCenter, 0f, 0f, 720f, h, bold);
		SetRect(text2.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, y, 720f, h);
		return text2;
	}

	private Text SettingButton(Transform parent, float y, Action toggle)
	{
		Image image = CenterPanel(parent, 340f, 32f, y, new Color32(243, 225, 192, byte.MaxValue));
		Button button = image.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		button.onClick.AddListener(delegate
		{
			toggle();
		});
		Text result = CenterLabel(image.transform, "", 14, InkDark, 0f, 30f, bold: false);
		TideTownStyleUI.StyleSettingButton(button);
		return result;
	}

	private void RefreshSettings()
	{
		if (runSetting != null)
		{
			runSetting.text = "默认移动：" + (PrototypeGame.DefaultRun ? "奔跑" : "慢走");
		}
		if (recenterSetting != null)
		{
			recenterSetting.text = "镜头自动回正：" + (ChaseCamera.AutoRecenter ? "开" : "关");
		}
	}

	public void SetCinematic(bool on, string owner = "story")
	{
		if (on)
		{
			cinematicOwners.Add(owner);
		}
		else
		{
			cinematicOwners.Remove(owner);
		}
		RefreshHud();
	}

	private void RefreshHud()
	{
		if (hud != null)
		{
			hud.SetActive(!Cinematic && !LetterOpen && !Paused && !titleShowing);
		}
	}

	public void RecallGuide()
	{
		if (!Paused && !Cinematic && !LetterOpen && !titleShowing && !MenuController.OverlayOpen)
		{
			keysRemaining = (goalRemaining = 6f);
		}
	}

	public bool ShowFirstTutorial(string text, KeyCode key, Action completed)
	{
		if (FirstTutorialActive)
		{
			return false;
		}
		if (!inputClose)
		{
			completed?.Invoke();
			return true;
		}
		SetTutorial(text);
		firstTutorialKey = key;
		firstTutorialRemaining = 8f;
		firstTutorialDone = completed;
		return true;
	}

	private void UpdateGuide()
	{
		if (FirstTutorialActive && !Paused && EasyInputBridge.Down(firstTutorialKey))
		{
			CompleteFirstTutorial();
		}
		if (!Paused && !Cinematic && !LetterOpen && !titleShowing && !MenuController.OverlayOpen)
		{
			if (!freeStarted)
			{
				freeStarted = true;
				keysRemaining = 12f;
			}
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			keysRemaining = Mathf.Max(0f, keysRemaining - unscaledDeltaTime);
			goalRemaining = Mathf.Max(0f, goalRemaining - unscaledDeltaTime);
			if (inputClose && Input.GetKeyDown(KeyCode.H))
			{
				RecallGuide();
			}
			if (FirstTutorialActive)
			{
				firstTutorialRemaining -= unscaledDeltaTime;
				if (firstTutorialRemaining <= 0f)
				{
					CompleteFirstTutorial();
				}
			}
		}
		FadeGuide(keysFade, inputClose && keysRemaining > 0f);
		FadeGuide(goalFade, !string.IsNullOrEmpty(GoalText) && (!inputClose || goalRemaining > 0f || PersistentGoal));
		UpdateGuideHud();
		RefreshHud();
	}

	private void CompleteFirstTutorial()
	{
		Action action = firstTutorialDone;
		firstTutorialDone = null;
		SetTutorial("");
		action?.Invoke();
	}

	private void FadeGuide(CanvasGroup group, bool visible)
	{
		if (!(group == null))
		{
			group.alpha = (inputClose ? Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / 0.35f) : (visible ? 1f : 0f));
			group.gameObject.SetActive(group.alpha > 0.001f);
		}
	}

	private void RebuildLetterRules()
	{
		if (letterContent == null || letterBody == null)
		{
			return;
		}
		ruledText = letterBody.text;
		float num = Mathf.Max(letterViewport.rect.height, letterBody.preferredHeight + 12f + 8f);
		letterContent.sizeDelta = new Vector2(500f, num);
		letterBody.rectTransform.sizeDelta = new Vector2(500f, num - 12f);
		Canvas.ForceUpdateCanvases();
		TextGenerator cachedTextGenerator = letterBody.cachedTextGenerator;
		cachedTextGenerator.Populate(letterBody.text, letterBody.GetGenerationSettings(letterBody.rectTransform.rect.size));
		IList<UILineInfo> lines = cachedTextGenerator.lines;
		IList<UIVertex> verts = cachedTextGenerator.verts;
		float pixelsPerUnit = letterBody.pixelsPerUnit;
		for (int i = 0; i < lines.Count; i++)
		{
			if (letterRules.Count <= i)
			{
				Image image = Panel(letterContent, 0f, 0f, 500f, 1f, TideTownStyleUI.PaperRuleColor, round: false);
				image.raycastTarget = false;
				image.transform.SetAsFirstSibling();
				letterRules.Add(image.rectTransform);
			}
			int num2 = lines[i].startCharIdx * 4;
			int a = ((i + 1 < lines.Count) ? (lines[i + 1].startCharIdx * 4) : (verts.Count - 4));
			float num3 = float.PositiveInfinity;
			for (int j = num2; j < Mathf.Min(a, verts.Count - 4); j++)
			{
				if (verts[j].position != Vector3.zero)
				{
					num3 = Mathf.Min(num3, verts[j].position.y / pixelsPerUnit);
				}
			}
			if (float.IsInfinity(num3))
			{
				num3 = (lines[i].topY - (float)lines[i].height) / pixelsPerUnit + 3f;
			}
			RectTransform rectTransform = letterRules[i];
			SetRect(rectTransform, 0f, 1f, 0f, 1f, 0f, num3 - 12f - 2f, 500f, 1f);
			rectTransform.pivot = new Vector2(0f, 1f);
			rectTransform.gameObject.SetActive(value: true);
		}
		for (int k = lines.Count; k < letterRules.Count; k++)
		{
			letterRules[k].gameObject.SetActive(value: false);
		}
	}

	private Image CenterPanel(Transform parent, float w, float h, float y, Color32 color)
	{
		Image image = Panel(parent, 0f, 0f, w, h, color);
		SetRect(image.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, y, w, h);
		return image;
	}

	private GameObject AddMenuBackdrop(RectTransform canvasRoot, Image card)
	{
		Image image = Panel(canvasRoot, 0f, 0f, 1280f, 720f, new Color32(10, 12, 16, 160), round: false, right: true);
		SetRect(image.rectTransform, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
		card.transform.SetParent(image.transform, worldPositionStays: false);
		return image.gameObject;
	}

	private void StyleSlider(Image bar, Image fill, Image handle)
	{
		bar.sprite = TideTownStyleUI.PillSprite;
		bar.type = Image.Type.Sliced;
		bar.color = new Color32(216, 200, 170, byte.MaxValue);
		fill.sprite = TideTownStyleUI.PillSprite;
		fill.type = Image.Type.Sliced;
		fill.color = SeaBlue;
		RectTransform rectTransform = fill.rectTransform;
		rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0f);
		rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1f);
		rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, 0f);
		handle.sprite = BootLoader.CircleSprite;
		handle.type = Image.Type.Simple;
		handle.color = Cinnabar;
		AddDropShadow(handle);
	}

	private Slider BuildVolume(Transform parent, float y, float w = 300f)
	{
		Image image = CenterPanel(parent, w, 12f, y, new Color32(59, 42, 26, byte.MaxValue));
		Image image2 = CenterPanel(image.transform, 0f, 8f, 0f, new Color32(byte.MaxValue, 154, 77, byte.MaxValue));
		Image image3 = CenterPanel(image.transform, 22f, 22f, 0f, new Color32(byte.MaxValue, 248, 236, byte.MaxValue));
		StyleSlider(image, image2, image3);
		image.raycastTarget = true;
		image2.raycastTarget = false;
		image3.rectTransform.sizeDelta = new Vector2(22f, 22f);
		Slider slider = image.gameObject.AddComponent<Slider>();
		slider.fillRect = image2.rectTransform;
		slider.handleRect = image3.rectTransform;
		slider.targetGraphic = image3;
		TideTownStyleUI.StyleButtonStates(slider);
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = 0f;
		slider.maxValue = 1f;
		slider.value = volume;
		slider.onValueChanged.AddListener(ApplyVolume);
		return slider;
	}

	private void ApplyVolume(float v)
	{
		volume = Mathf.Clamp01(v);
		AudioListener.volume = volume;
		PlayerPrefs.SetFloat("tidetown.volume", volume);
		PlayerPrefs.Save();
		if (volumeBar1 != null && Mathf.Abs(volumeBar1.value - volume) > 0.001f)
		{
			volumeBar1.SetValueWithoutNotify(volume);
		}
		if (volumeBar2 != null && Mathf.Abs(volumeBar2.value - volume) > 0.001f)
		{
			volumeBar2.SetValueWithoutNotify(volume);
		}
		string text = "音量 " + Mathf.RoundToInt(volume * 100f) + "%";
		if (volumeText1 != null)
		{
			volumeText1.text = text;
		}
		if (volumeText2 != null)
		{
			volumeText2.text = text;
		}
	}

	private Slider BuildSensitivity(Transform parent, float y, float w = 300f)
	{
		Image image = CenterPanel(parent, w, 12f, y, new Color32(59, 42, 26, byte.MaxValue));
		Image image2 = CenterPanel(image.transform, 0f, 8f, 0f, new Color32(byte.MaxValue, 154, 77, byte.MaxValue));
		Image image3 = CenterPanel(image.transform, 22f, 22f, 0f, new Color32(byte.MaxValue, 248, 236, byte.MaxValue));
		StyleSlider(image, image2, image3);
		image.raycastTarget = true;
		image2.raycastTarget = false;
		image3.rectTransform.sizeDelta = new Vector2(22f, 22f);
		Slider slider = image.gameObject.AddComponent<Slider>();
		slider.fillRect = image2.rectTransform;
		slider.handleRect = image3.rectTransform;
		slider.targetGraphic = image3;
		TideTownStyleUI.StyleButtonStates(slider);
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = 0.4f;
		slider.maxValue = 2f;
		slider.value = ChaseCamera.Sensitivity;
		slider.onValueChanged.AddListener(ApplySensitivity);
		return slider;
	}

	private void ApplySensitivity(float v)
	{
		ChaseCamera.Sensitivity = v;
		float sensitivity = ChaseCamera.Sensitivity;
		if (sensBar != null && Mathf.Abs(sensBar.value - sensitivity) > 0.001f)
		{
			sensBar.SetValueWithoutNotify(sensitivity);
		}
		if (sensText != null)
		{
			sensText.text = "视角灵敏度 " + Mathf.RoundToInt(sensitivity * 100f) + "%";
		}
	}

	public void SetVolume(float v)
	{
		ApplyVolume(v);
	}

	public void BeginTitle()
	{
		externalTitle = true;
		titleShowing = true;
		if (hud != null)
		{
			hud.SetActive(value: false);
		}
		if (titlePanel != null)
		{
			titlePanel.SetActive(value: false);
		}
		MenuController.ReleasePointer();
		ApplyVolume(volume);
	}

	public void EndTitle()
	{
		if (titleShowing)
		{
			titleShowing = false;
			externalTitle = false;
			RefreshHud();
			MenuController.RestorePointer(this);
		}
	}

	public void ShowTitle(Action onStart)
	{
		onTitleStart = onStart;
		titleShowing = true;
		hud.SetActive(value: false);
		MenuController.ReleasePointer();
		if (titlePanel != null)
		{
			titlePanel.SetActive(value: true);
		}
		ApplyVolume(volume);
	}

	private void StartFromTitle()
	{
		if (titleShowing)
		{
			titleShowing = false;
			RefreshHud();
			if (titlePanel != null)
			{
				titlePanel.SetActive(value: false);
			}
			Action action = onTitleStart;
			onTitleStart = null;
			action?.Invoke();
			MenuController.RestorePointer(this);
		}
	}

	public void SetPaused(bool on)
	{
		if (Paused != on)
		{
			Paused = on;
			RefreshHud();
			if (pausePanel != null)
			{
				pausePanel.SetActive(on);
			}
			if (on)
			{
				ApplyVolume(volume);
				ApplySensitivity(ChaseCamera.Sensitivity);
				RefreshSettings();
			}
			Time.timeScale = (on ? 0f : 1f);
			if (player != null)
			{
				player.locked = on || LetterOpen || MenuController.OverlayOpen;
			}
			if (on)
			{
				MenuController.ReleasePointer();
			}
			else
			{
				MenuController.RestorePointer(this);
			}
		}
	}

	public void SetGoal(string text)
	{
		if (!(goalText == null) && !(goalText.text == text))
		{
			goalRemaining = 5.5f;
			goalText.text = text;
			if (pauseGoal != null)
			{
				pauseGoal.text = text;
			}
			bool flag = !string.IsNullOrEmpty(text);
			goalPanel.SetActive(flag);
			if (flag)
			{
				RectTransform rectTransform = goalText.rectTransform;
				float num = Mathf.Min(368f, Mathf.Ceil(goalText.preferredWidth) + 2f);
				rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, num);
				float num2 = Mathf.Max(22f, Mathf.Ceil(goalText.preferredHeight));
				rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, num2);
				float num3 = Mathf.Max(num, (goalEyebrow != null) ? Mathf.Ceil(goalEyebrow.preferredWidth) : 0f);
				((RectTransform)goalPanel.transform).sizeDelta = new Vector2(52f + num3 + 12f + 2f, 29f + num2 + 5f + 4f);
			}
		}
	}

	public void SetTutorial(string text)
	{
		if (!FirstTutorialActive && !(tutorialPanel == null))
		{
			bool flag = !string.IsNullOrEmpty(text);
			if (flag && tutorialText != null)
			{
				tutorialText.text = text;
			}
			tutorialPanel.SetActive(flag);
		}
	}

	public void SetPrompt(string text, bool isPrimary = true)
	{
		if (promptPanel == null)
		{
			return;
		}
		if (string.IsNullOrEmpty(text))
		{
			if (promptPanel.activeSelf)
			{
				promptPanel.SetActive(value: false);
			}
		}
		else if (!promptPanel.activeSelf || !(promptText.text == text) || promptPrimary != isPrimary)
		{
			promptText.text = text;
			promptPrimary = isPrimary;
			string obj = promptText.text ?? "";
			string text2 = null;
			string text3 = obj;
			Match match = Regex.Match(obj, "^\\s*(按住|按)?\\s*(Esc|Tab|Shift|Space|Enter|[A-Za-z0-9]{1,3}|[A-Za-z]/[A-Za-z])[\\s\u3000]+(.+)$");
			if (match.Success)
			{
				text2 = ((match.Groups[2].Value.Length == 1) ? match.Groups[2].Value.ToUpperInvariant() : match.Groups[2].Value);
				text3 = ((match.Groups[1].Value == "按住") ? "按住 " : "") + match.Groups[3].Value.Trim();
			}
			float num = (isPrimary ? 46f : 34f);
			float num2 = (isPrimary ? 34f : 26f);
			promptPanel.GetComponent<Image>().color = (isPrimary ? new Color32(201, 168, 124, byte.MaxValue) : new Color32(251, 243, 226, 215));
			if (promptInner != null)
			{
				promptInner.gameObject.SetActive(isPrimary);
			}
			float num3 = 0f;
			promptKey.gameObject.SetActive(text2 != null);
			if (text2 != null)
			{
				promptKeyText.text = text2;
				promptKeyText.fontSize = (isPrimary ? 15 : 12);
				num3 = Mathf.Max(num2, Mathf.Ceil(promptKeyText.preferredWidth) + 16f);
				promptKey.color = (isPrimary ? Color.white : new Color(1f, 1f, 1f, 0.85f));
			}
			promptAction.text = text3;
			promptAction.fontSize = (isPrimary ? 17 : 13);
			promptAction.fontStyle = (isPrimary ? FontStyle.Bold : FontStyle.Normal);
			promptAction.color = (isPrimary ? ((Color)InkDark) : ((Color)new Color32(41, 58, 57, 215)));
			float num4 = Mathf.Min(460f, Mathf.Ceil(promptAction.preferredWidth) + 2f);
			float num5 = ((text2 != null) ? (7f + num3 + 10f) : 22f);
			SetRect(promptPanel.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 0f, isPrimary ? 106 : 76, num5 + num4 + 22f, num);
			RectTransform rectTransform = promptKey.rectTransform;
			Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(0f, 0.5f));
			rectTransform.anchorMin = anchorMin;
			rectTransform.pivot = new Vector2(0.5f, 0.5f);
			rectTransform.anchoredPosition = new Vector2(7f + num3 * 0.5f, 0f);
			rectTransform.sizeDelta = new Vector2(num3, num2);
			RectTransform rectTransform2 = promptKeyText.rectTransform;
			rectTransform2.anchorMin = Vector2.zero;
			rectTransform2.anchorMax = Vector2.one;
			rectTransform2.pivot = new Vector2(0.5f, 0.5f);
			rectTransform2.offsetMin = new Vector2(0f, num2 * 0.16f);
			rectTransform2.offsetMax = Vector2.zero;
			RectTransform rectTransform3 = promptAction.rectTransform;
			anchorMin = (rectTransform3.anchorMax = new Vector2(0f, 0.5f));
			rectTransform3.anchorMin = anchorMin;
			rectTransform3.pivot = new Vector2(0f, 0.5f);
			rectTransform3.anchoredPosition = new Vector2(num5, 0f);
			rectTransform3.sizeDelta = new Vector2(num4, num - 8f);
			promptPanel.SetActive(value: true);
		}
	}

	public void SetActiveExpression(ExprId id)
	{
		activeExpr = id;
		int num = Array.IndexOf(EggboyExpressions.All, id);
		for (int i = 0; i < faceBg.Count; i++)
		{
			bool flag = i == num;
			faceBg[i].color = (flag ? new Color32(byte.MaxValue, 154, 77, byte.MaxValue) : new Color32(byte.MaxValue, 248, 236, byte.MaxValue));
			faceLabel[i].color = (flag ? new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) : new Color32(59, 42, 26, byte.MaxValue));
		}
	}

	public void SetLetterTaken(bool taken)
	{
		letterTaken = taken;
	}

	public void SetCarried(string text)
	{
		if (!(carryPanel == null))
		{
			bool flag = !string.IsNullOrEmpty(text);
			if (flag && carryText != null)
			{
				carryText.text = text;
			}
			carryPanel.SetActive(flag);
		}
	}

	public void ShowSubtitle(string text)
	{
		if (!(subtitlePanel == null))
		{
			bool flag = !string.IsNullOrEmpty(text);
			if (flag && subtitleText != null)
			{
				subtitleText.text = text;
			}
			subtitlePanel.SetActive(flag);
		if (text == "所有的信，都送到了。") TownVoiceover.Play("vo_ending_sub1");
		if (text == "回家吧。山这边的新邮局，一直给你留着位置。") TownVoiceover.Play("vo_ending_sub2");
		}
	}

	public void ShowToast(string text, float seconds = 4.5f)
	{
		if (!(toastPanel == null))
		{
			if (toastText != null)
			{
				toastText.text = text;
			}
			toastPanel.SetActive(value: true);
			toastUntil = Time.unscaledTime + seconds;
		}
	}

	public void ShowLetter()
	{
		if (!(letterOverlay == null))
		{
			bool letterOpen = LetterOpen;
			if (!letterOpen || openedDelivery != letterDelivery)
			{
				letterOpenedFrame = Time.frameCount;
			}
			LetterOpen = true;
			letterOverlay.SetActive(value: true);
			SetCinematic(on: true, "letter");
			MenuController.ReleasePointer();
			letterScroll.StopMovement();
			letterContent.anchoredPosition = Vector2.zero;
			RebuildLetterRules();
		letterVoiceKey = TownVoiceover.LetterKey((letterTitle != null) ? letterTitle.text : "");
		if (replayVoice != null) replayVoice.gameObject.SetActive(letterVoiceKey != null);
		TownVoiceover.Play(letterVoiceKey);
			if (inputClose && letterDelivery != null && (!letterOpen || openedDelivery != letterDelivery))
			{
				BeginLetterOpening(letterDelivery);
			}
			else if (letterDelivery == null && LetterOpening)
			{
				FinishLetterOpening();
			}
			openedDelivery = letterDelivery;
		}
	}

	public void ShowLetter(string title, string body, string sign, string letterId = null)
	{
		Chrome(list: true, sign: true, "收起信 · E");
		SetLetterArtwork(letterId);
		Fill(title, body, sign);
		ShowLetter();
	}

	public void ShowLetterDetail(string title, string body, string sign, string letterId = null)
	{
		Chrome(list: false, sign: true, "收起信 · E");
		SetLetterArtwork(letterId);
		Fill(title, body, sign);
		ShowLetter();
	}

	public void ShowMemory(string title, string body)
	{
		Chrome(list: false, sign: false, "收起 · E");
		SetLetterArtwork(null);
		Fill(title, body, null);
		ShowLetter();
	}

	private void Fill(string title, string body, string sign)
	{
		if (letterTitle != null && !string.IsNullOrEmpty(title))
		{
			letterTitle.text = title;
		}
		if (letterBody != null && !string.IsNullOrEmpty(body))
		{
			letterBody.text = body;
		}
		if (letterSign != null)
		{
			letterSign.text = sign ?? "";
		}
	}

	private void SetLetterArtwork(string letterId)
	{
		string delivery = (letterDelivery = PostalArtwork.DeliveryForLetter(letterId));
		letterEnvelope.sprite = PostalArtwork.Envelope(delivery);
		letterEnvelope.enabled = letterEnvelope.sprite != null;
		letterEnvelopeInk.sprite = PostalArtwork.Stamp(delivery);
		letterEnvelopeInk.enabled = letterEnvelopeInk.sprite != null && HasPostmark(delivery);
	}

	private void Chrome(bool list, bool sign, string hint)
	{
		if (letterIntro1 != null)
		{
			letterIntro1.gameObject.SetActive(list);
		}
		if (letterIntro2 != null)
		{
			letterIntro2.gameObject.SetActive(list);
		}
		if (letterSign != null)
		{
			letterSign.gameObject.SetActive(sign);
		}
		letterHint.text = hint;
		letterPaperCloseBtn.gameObject.SetActive(value: true);
		float num = (list ? 119f : 54f);
		float num2 = num + 43f;
		letterTitle.rectTransform.anchoredPosition = new Vector2(30f, 0f - num);
		letterViewport.anchoredPosition = new Vector2(30f, 0f - num2);
		letterViewport.sizeDelta = new Vector2(500f, 474f - num2);
	}

	public void CloseLetter()
	{
		if (LetterOpen)
		{
			FinishLetterOpening();
			LetterOpen = false;
			letterOverlay.SetActive(value: false);
			SetCinematic(on: false, "letter");
			OnLetterClosed?.Invoke();
			MenuController.RestorePointer(this);
		}
	}

	public void SetAuditMode(bool on)
	{
		inputClose = !on;
		if (letterCloseBtn != null)
		{
			letterCloseBtn.interactable = !on;
		}
		if (letterPaperCloseBtn != null)
		{
			letterPaperCloseBtn.interactable = !on;
		}
	}

	private void Update()
	{
		UpdateGuide();
		AnimateUi();
		AnimatePostalPresentation();
		if (LetterOpen && ruledText != letterBody.text)
		{
			RebuildLetterRules();
		letterVoiceKey = TownVoiceover.LetterKey((letterTitle != null) ? letterTitle.text : "");
		if (replayVoice != null) replayVoice.gameObject.SetActive(letterVoiceKey != null);
		TownVoiceover.Play(letterVoiceKey);
		}
		if (toastPanel != null && toastPanel.activeSelf && Time.unscaledTime >= toastUntil)
		{
			toastPanel.SetActive(value: false);
		}
		if (titleShowing)
		{
			if (!externalTitle && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
			{
				StartFromTitle();
			}
		}
		else
		{
			if (Paused || player == null || world == null)
			{
				return;
			}
			if (LetterOpen)
			{
				if (inputClose && Time.frameCount != letterOpenedFrame && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
				{
					if (LetterOpening)
					{
						FinishLetterOpening();
					}
					else
					{
						CloseLetter();
					}
				}
			}
			else if (legacyPrompt)
			{
				bool flag = Vector3.Distance(player.transform.position, world.MailboxPos) < 1.4f;
				SetPrompt((flag && !letterTaken) ? "E\u3000取信" : null);
			}
		}
	}

	private Image Panel(Transform parent, float x, float y, float w, float h, Color32 color, bool round = true, bool right = false)
	{
		GameObject obj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(parent, worldPositionStays: false);
		Image component = obj.GetComponent<Image>();
		component.sprite = (round ? rounded : white);
		component.type = (round ? Image.Type.Sliced : Image.Type.Simple);
		component.color = color;
		RectTransform component2 = obj.GetComponent<RectTransform>();
		float num = (right ? 1f : 0f);
		SetRect(component2, num, 1f, num, 1f, right ? x : x, y, w, h);
		component2.pivot = new Vector2(right ? 1f : 0f, 1f);
		return component;
	}

	private Text Label(Transform parent, string text, int size, Color32 color, TextAnchor align, float x, float y, float w, float h, bool bold)
	{
		GameObject gameObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		Text component = gameObject.GetComponent<Text>();
		component.font = font;
		component.fontSize = size;
		component.fontStyle = (bold ? FontStyle.Bold : FontStyle.Normal);
		component.color = color;
		component.alignment = align;
		component.text = text;
		component.supportRichText = false;
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		component.verticalOverflow = VerticalWrapMode.Overflow;
		component.lineSpacing = 1.3f;
		component.raycastTarget = false;
		RectTransform component2 = gameObject.GetComponent<RectTransform>();
		SetRect(component2, 0f, 1f, 0f, 1f, x, y, w, h);
		component2.pivot = new Vector2(0f, 1f);
		return component;
	}

	private static void SetRect(RectTransform rt, float ax, float ay, float bx, float by, float x, float y, float w, float h)
	{
		rt.anchorMin = new Vector2(ax, ay);
		rt.anchorMax = new Vector2(bx, by);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = new Vector2(x, y);
		rt.sizeDelta = new Vector2(w, h);
	}

	private static Sprite MakeRounded()
	{
		Texture2D texture2D = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false, linear: false);
		Color32[] array = new Color32[4096];
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = Mathf.Max((float)(14 - j) - 0.5f, 0f, (float)j + 0.5f - 50f);
				float num2 = Mathf.Max((float)(14 - i) - 0.5f, 0f, (float)i + 0.5f - 50f);
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				byte a = (byte)(Mathf.Clamp01(15f - num3) * 255f);
				array[i * 64 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, a);
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		return Sprite.Create(texture2D, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
	}

	public void HideKeysStrip()
	{
		keysRemaining = 0f;
		freeStarted = true;
	}

	private void BuildGuideHud()
	{
		if (!(hud == null))
		{
			RectTransform parent = (RectTransform)hud.transform;
			guideRoot = new GameObject("QuestGuideHud", typeof(RectTransform)).GetComponent<RectTransform>();
			guideRoot.SetParent(parent, worldPositionStays: false);
			guideRoot.anchorMin = Vector2.zero;
			guideRoot.anchorMax = Vector2.one;
			RectTransform rectTransform = guideRoot;
			Vector2 offsetMin = (guideRoot.offsetMax = Vector2.zero);
			rectTransform.offsetMin = offsetMin;
			guideRoot.SetSiblingIndex(0);
			Image image = Img(guideRoot, "TrackChip", TideTownStyleUI.PillSprite, new Color32(251, 243, 226, 235));
			trackChip = image.rectTransform;
			RectTransform rectTransform2 = trackChip;
			offsetMin = (trackChip.anchorMax = new Vector2(0f, 1f));
			rectTransform2.anchorMin = offsetMin;
			trackChip.pivot = new Vector2(0f, 1f);
			trackChip.sizeDelta = new Vector2(220f, 28f);
			AddDropShadow(image);
			trackChipFade = image.gameObject.AddComponent<CanvasGroup>();
			trackChipFade.alpha = 0f;
			Image image2 = Img(trackChip, "TrackArrowBg", BootLoader.CircleSprite, SeaBlue);
			Pin(image2.rectTransform, 0f, 0.5f, 16f, 0f, 22f, 22f);
			SetRect(Label(image2.transform, "▲", 12, new Color32(251, 243, 226, byte.MaxValue), TextAnchor.MiddleCenter, 0f, 0f, 22f, 22f, bold: true).rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 22f, 22f);
			trackArrow = image2.rectTransform;
			trackChipText = Label(trackChip, "", 13, InkDark, TextAnchor.MiddleLeft, 32f, 0f, 400f, 24f, bold: true);
			SetRect(trackChipText.rectTransform, 0f, 0.5f, 0f, 0.5f, 32f, 0f, 400f, 24f);
			trackChipText.rectTransform.pivot = new Vector2(0f, 0.5f);
			trackChip.gameObject.SetActive(value: false);
			waypoint = new GameObject("Waypoint", typeof(RectTransform)).GetComponent<RectTransform>();
			waypoint.SetParent(guideRoot, worldPositionStays: false);
			RectTransform rectTransform3 = waypoint;
			offsetMin = (waypoint.anchorMax = new Vector2(0.5f, 0.5f));
			rectTransform3.anchorMin = offsetMin;
			waypoint.sizeDelta = new Vector2(60f, 60f);
			waypointFade = waypoint.gameObject.AddComponent<CanvasGroup>();
			waypointFade.alpha = 0f;
			Image image3 = Img(waypoint, "WaypointDisc", BootLoader.CircleSprite, new Color32(251, 243, 226, 230));
			Pin(image3.rectTransform, 0.5f, 0.5f, 0f, 6f, 30f, 30f);
			AddDropShadow(image3);
			waypointRing = Img(image3.transform, "WaypointRing", TideTownStyleUI.PostmarkRingSprite, Cinnabar);
			Pin(waypointRing.rectTransform, 0.5f, 0.5f, 0f, 0f, 26f, 26f);
			SetRect(Label(image3.transform, "✉", 13, Cinnabar, TextAnchor.MiddleCenter, 0f, 0f, 26f, 26f, bold: true).rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 1f, 26f, 26f);
			waypointText = Label(waypoint, "", 12, new Color32(251, 243, 226, byte.MaxValue), TextAnchor.MiddleCenter, 0f, 0f, 90f, 18f, bold: true);
			SetRect(waypointText.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, -19f, 90f, 18f);
			Outline outline = waypointText.gameObject.AddComponent<Outline>();
			outline.effectColor = new Color32(26, 36, 34, 200);
			outline.effectDistance = new Vector2(1.2f, -1.2f);
			Text text = Label(waypoint, "▲", 14, Cinnabar, TextAnchor.MiddleCenter, 0f, 0f, 20f, 20f, bold: true);
			waypointArrow = text.rectTransform;
			SetRect(waypointArrow, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 6f, 20f, 20f);
			waypoint.gameObject.SetActive(value: false);
			Image image4 = Img(guideRoot, "BasicsCard", TideTownStyleUI.PaperSprite, Parchment);
			basicsCard = image4.rectTransform;
			RectTransform rectTransform4 = basicsCard;
			offsetMin = (basicsCard.anchorMax = new Vector2(0.5f, 1f));
			rectTransform4.anchorMin = offsetMin;
			basicsCard.pivot = new Vector2(0.5f, 1f);
			basicsCard.anchoredPosition = new Vector2(0f, -18f);
			basicsCard.sizeDelta = new Vector2(500f, 104f);
			AddDropShadow(image4);
			basicsFade = image4.gameObject.AddComponent<CanvasGroup>();
			basicsFade.alpha = 0f;
			Image image5 = Img(basicsCard, "BasicsStamp", TideTownStyleUI.PostmarkRingSprite, SeaBlue);
			Pin(image5.rectTransform, 0f, 1f, 30f, -24f, 30f, 30f);
			image5.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f);
			basicsEyebrow = Label(basicsCard, "", 12, SeaBlue, TextAnchor.MiddleLeft, 0f, 0f, 300f, 18f, bold: true);
			SetRect(basicsEyebrow.rectTransform, 0f, 1f, 0f, 1f, 52f, -18f, 300f, 18f);
			basicsEyebrow.rectTransform.pivot = new Vector2(0f, 0.5f);
			basicsSkip = Label(basicsCard, "Enter 跳过教程", 11, Eyebrow, TextAnchor.MiddleRight, 0f, 0f, 160f, 18f, bold: false);
			SetRect(basicsSkip.rectTransform, 1f, 1f, 1f, 1f, -14f, -18f, 160f, 18f);
			basicsSkip.rectTransform.pivot = new Vector2(1f, 0.5f);
			basicsKeys = new GameObject("BasicsKeys", typeof(RectTransform)).GetComponent<RectTransform>();
			basicsKeys.SetParent(basicsCard, worldPositionStays: false);
			RectTransform rectTransform5 = basicsKeys;
			offsetMin = (basicsKeys.anchorMax = new Vector2(0.5f, 1f));
			rectTransform5.anchorMin = offsetMin;
			basicsKeys.sizeDelta = new Vector2(10f, 32f);
			basicsKeys.anchoredPosition = new Vector2(0f, -48f);
			basicsText = Label(basicsCard, "", 16, InkDark, TextAnchor.MiddleCenter, 0f, 0f, 470f, 26f, bold: true);
			SetRect(basicsText.rectTransform, 0.5f, 0f, 0.5f, 0f, 0f, 20f, 470f, 26f);
			basicsCheck = Label(basicsCard, "✓", 30, new Color32(63, 138, 92, byte.MaxValue), TextAnchor.MiddleCenter, 0f, 0f, 40f, 40f, bold: true);
			SetRect(basicsCheck.rectTransform, 1f, 0.5f, 1f, 0.5f, -30f, -6f, 40f, 40f);
			basicsCheck.enabled = false;
			basicsCard.gameObject.SetActive(value: false);
		}
	}

	public void SetQuestTarget(bool has, Vector3 world, string label, float distance, Camera cam, bool showWaypoint, Vector3 steer)
	{
		if (guideRoot == null)
		{
			return;
		}
		trackWanted = has;
		waypointWanted = has && showWaypoint && cam != null && distance > 2.6f;
		if (has)
		{
			RectTransform rectTransform = ((goalPanel != null && goalPanel.activeSelf) ? ((RectTransform)goalPanel.transform) : null);
			float y = ((rectTransform != null) ? (rectTransform.anchoredPosition.y - rectTransform.sizeDelta.y - 6f) : (-22f));
			trackChip.anchoredPosition = new Vector2(24f, y);
			trackChipText.text = label + "\u3000" + FormatDistance(distance);
			trackChip.sizeDelta = new Vector2(Mathf.Min(420f, 32f + Mathf.Ceil(trackChipText.preferredWidth) + 14f), 28f);
			if (cam != null)
			{
				Vector3 vector = steer - cam.transform.position;
				Vector3 forward = cam.transform.forward;
				float num = Mathf.Atan2(vector.x, vector.z) - Mathf.Atan2(forward.x, forward.z);
				trackArrow.localRotation = Quaternion.Euler(0f, 0f, (0f - num) * 57.29578f);
			}
		}
		if (waypointWanted)
		{
			PlaceWaypoint(world + Vector3.up * 1.7f, distance, cam);
		}
	}

	private static string FormatDistance(float d)
	{
		if (!(d < 1.5f))
		{
			return Mathf.RoundToInt(d) + " 米";
		}
		return "就在这儿";
	}

	private void PlaceWaypoint(Vector3 world, float distance, Camera cam)
	{
		RectTransform rect = (RectTransform)hud.transform;
		Vector3 vector = cam.WorldToScreenPoint(world);
		bool num = vector.z < 0f;
		if (num)
		{
			vector.x = (float)Screen.width - vector.x;
			vector.y = (float)Screen.height - vector.y;
		}
		float num2 = (float)Mathf.Min(Screen.width, Screen.height) * 0.07f;
		bool num3 = !num && vector.x > num2 && vector.x < (float)Screen.width - num2 && vector.y > num2 && vector.y < (float)Screen.height - num2;
		Vector2 vector2 = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
		Vector2 vector3 = new Vector2(vector.x, vector.y);
		if (!num3)
		{
			Vector2 vector4 = vector3 - vector2;
			if (vector4.sqrMagnitude < 1f)
			{
				vector4 = Vector2.down;
			}
			float a = ((float)Screen.width * 0.5f - num2) / Mathf.Max(0.001f, Mathf.Abs(vector4.x));
			float b = ((float)Screen.height * 0.5f - num2) / Mathf.Max(0.001f, Mathf.Abs(vector4.y));
			vector3 = vector2 + vector4 * Mathf.Min(a, b);
			waypointArrow.gameObject.SetActive(value: true);
			float z = Mathf.Atan2(vector4.y, vector4.x) * 57.29578f - 90f;
			waypointArrow.localRotation = Quaternion.Euler(0f, 0f, z);
			waypointArrow.anchoredPosition = (Vector2)(Quaternion.Euler(0f, 0f, z) * new Vector2(0f, 26f)) + new Vector2(0f, 6f);
		}
		else
		{
			waypointArrow.gameObject.SetActive(value: true);
			waypointArrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
			waypointArrow.anchoredPosition = new Vector2(0f, -10f + Mathf.Sin(Time.unscaledTime * 4f) * 2f);
		}
		RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, vector3, null, out var localPoint);
		waypoint.anchoredPosition = localPoint;
		waypointText.text = FormatDistance(distance);
		waypointText.enabled = true;
		float num4 = (num3 ? 1f : 0.9f);
		waypoint.localScale = Vector3.one * num4;
		if (waypointRing != null)
		{
			waypointRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 20f);
		}
	}

	public void ShowBasicsCard(string eyebrow, string[] keys, string text)
	{
		if (basicsCard == null)
		{
			return;
		}
		basicsWanted = true;
		basicsEyebrow.text = eyebrow;
		basicsText.text = text;
		basicsCheck.enabled = false;
		basicsPop = 1f;
		for (int num = basicsKeys.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(basicsKeys.GetChild(num).gameObject);
		}
		float num2 = 0f;
		List<RectTransform> list = new List<RectTransform>();
		if (keys != null)
		{
			foreach (string text2 in keys)
			{
				if (text2 == "/")
				{
					Pin(Label(basicsKeys, "/", 15, Eyebrow, TextAnchor.MiddleCenter, 0f, 0f, 16f, 30f, bold: true).rectTransform, 0.5f, 0.5f, num2 + 8f, 0f, 16f, 30f);
					num2 += 20f;
				}
				else
				{
					RectTransform rectTransform = Keycap(basicsKeys, text2, num2, 0f, 32f, 15, leftEdge: true);
					list.Add(rectTransform);
					num2 += rectTransform.sizeDelta.x + 6f;
				}
			}
		}
		float num3 = Mathf.Max(0f, num2 - 6f);
		basicsKeys.anchoredPosition = new Vector2((0f - num3) * 0.5f, -48f);
		basicsCard.gameObject.SetActive(value: true);
	}

	public void MarkBasicsDone()
	{
		if (basicsCheck != null)
		{
			basicsCheck.enabled = true;
			basicsPop = 1f;
		}
	}

	public void HideBasicsCard()
	{
		basicsWanted = false;
	}

	private void UpdateGuideHud()
	{
		if (!(guideRoot == null))
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			bool flag = !Paused && !Cinematic && !LetterOpen && !titleShowing && !MenuController.OverlayOpen;
			Fade(trackChipFade, trackWanted && flag && goalPanel != null && goalPanel.activeSelf, unscaledDeltaTime);
			Fade(waypointFade, waypointWanted && flag, unscaledDeltaTime);
			Fade(basicsFade, basicsWanted && flag, unscaledDeltaTime);
			if (basicsCard != null && basicsCard.gameObject.activeSelf)
			{
				basicsPop = Mathf.MoveTowards(basicsPop, 0f, unscaledDeltaTime / 0.35f);
				basicsCard.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(basicsPop * MathF.PI));
			}
		}
	}

	private static void Fade(CanvasGroup g, bool on, float dt)
	{
		if (!(g == null))
		{
			g.alpha = Mathf.MoveTowards(g.alpha, on ? 1f : 0f, dt / 0.3f);
			bool flag = g.alpha > 0.001f;
			if (g.gameObject.activeSelf != flag)
			{
				g.gameObject.SetActive(flag);
			}
		}
	}

	internal void BindPostmarks(ICollection<string> postmarks)
	{
		collectedPostmarks = postmarks;
	}

	private bool HasPostmark(string delivery)
	{
		if (delivery != null && collectedPostmarks != null)
		{
			return collectedPostmarks.Contains(delivery);
		}
		return false;
	}

	private void BuildPostalAnimation(RectTransform root)
	{
		GameObject gameObject = new GameObject("LetterOpening", typeof(RectTransform), typeof(CanvasGroup));
		gameObject.transform.SetParent(letterOverlay.transform, worldPositionStays: false);
		Pin((RectTransform)gameObject.transform, 0.5f, 0.5f, 0f, 0f, 560f, 500f);
		opening = gameObject.GetComponent<CanvasGroup>();
		CanvasGroup canvasGroup = opening;
		bool interactable = (opening.blocksRaycasts = false);
		canvasGroup.interactable = interactable;
		openingEnvelope = Img(gameObject.transform, "ClosedEnvelope", null, Color.white);
		Pin(openingEnvelope.rectTransform, 0.5f, 0.5f, 0f, -30f, 394f, 270f);
		AddDropShadow(openingEnvelope);
		openingFlap = Img(gameObject.transform, "EnvelopeFlap", null, Color.white);
		PinOpeningFlap(168f);
		openingMouth = Img(gameObject.transform, "PocketOpening", TideTownStyleUI.PaperSprite, new Color32(108, 72, 46, 235));
		Pin(openingMouth.rectTransform, 0.5f, 0.5f, 0f, 85f, 374f, 17f);
		Image image = Img(gameObject.transform, "UnfoldingSheet", TideTownStyleUI.PaperSprite, Parchment);
		unfoldingSlip = image.rectTransform;
		Frame(image.transform, new Color32(190, 164, 121, 160), 7f);
		for (int i = 0; i < 3; i++)
		{
			Pin(Img(image.transform, "FoldedPaperRule", null, TideTownStyleUI.PaperRuleColor).rectTransform, 0.5f, 1f, 0f, -27f - 13f * (float)i, 222f, 1f);
		}
		openingPocket = Img(gameObject.transform, "OpenEnvelopePocket", TideTownStyleUI.PaperSprite, new Color32(237, 207, 164, byte.MaxValue));
		Pin(openingPocket.rectTransform, 0.5f, 0.5f, 0f, -42f, 383f, 246f);
		AddDropShadow(openingPocket);
		openingPocketTrim = Img(openingPocket.transform, "OriginalPaperEdge", null, Color.white);
		Pin(openingPocketTrim.rectTransform, 0.5f, 0f, 0f, 40.5f, 394f, 81f);
		Pin(Img(openingPocket.transform, "PocketFold", null, new Color32(140, 89, 58, 68)).rectTransform, 0.5f, 1f, 0f, -5f, 351f, 1.5f);
		openingInk = Img(gameObject.transform, "OpeningEnvelopeInk", null, Color.white);
		Pin(openingInk.rectTransform, 0.5f, 0.5f, 91f, -69f, 125f, 96f);
		openingInk.preserveAspect = true;
		openingInk.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
		Image image2 = Img(gameObject.transform, "EnvelopeAddress", TideTownStyleUI.PaperSprite, Parchment);
		Pin(image2.rectTransform, 0.5f, 0.5f, 0f, -202f, 270f, 55f);
		AddDropShadow(image2);
		openingTitle = Txt(image2.transform, "", 19, SeaBlue, TextAnchor.MiddleCenter, 0f, 10f, 260f, 25f, bold: true);
		Txt(image2.transform, "E 跳过", 13, TideTownStyleUI.InkMuted, TextAnchor.MiddleCenter, 0f, -14f, 120f, 19f, bold: false);
		gameObject.SetActive(value: false);
		Image image3 = Img(root, "DeliveryPostmarkCard", TideTownStyleUI.PaperSprite, Parchment);
		Pin(image3.rectTransform, 1f, 1f, -213f, -163f, 375f, 252f);
		AddDropShadow(image3);
		Frame(image3.transform, new Color32(196, 166, 121, 145), 8f);
		stampCard = image3.gameObject.AddComponent<CanvasGroup>();
		CanvasGroup canvasGroup2 = stampCard;
		interactable = (stampCard.blocksRaycasts = false);
		canvasGroup2.interactable = interactable;
		stampTitle = Txt(image3.transform, "", 19, SeaBlue, TextAnchor.MiddleCenter, 0f, 94f, 343f, 32f, bold: true);
		stampedEnvelope = Img(image3.transform, "StampedEnvelope", null, Color.white);
		Pin(stampedEnvelope.rectTransform, 0.5f, 0.5f, 0f, -18f, 296f, 198f);
		stampedEnvelope.preserveAspect = true;
		stampInk = Img(stampedEnvelope.transform, "EnvelopePostmarkInk", null, Color.white);
		Pin(stampInk.rectTransform, 0.5f, 0.5f, 76f, -34f, 105f, 81f);
		stampInk.preserveAspect = true;
		stampInk.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
		stampHandle = Img(image3.transform, "StampPress", PostalArtwork.StampHandle, Color.white);
		Pin(stampHandle.rectTransform, 0.5f, 0.5f, 76f, 130f, 82f, 120f);
		stampHandle.preserveAspect = true;
		stampHandle.rectTransform.pivot = new Vector2(0.5f, 0f);
		stampCard.gameObject.SetActive(value: false);
	}

	private void BeginLetterOpening(string delivery)
	{
		openingEnvelope.sprite = PostalArtwork.Envelope(delivery);
		if (!(openingEnvelope.sprite == null))
		{
			openingFlap.sprite = PostalArtwork.Flap(delivery);
			if (openingFlap.sprite != null)
			{
				PinOpeningFlap(394f * openingFlap.sprite.rect.height / openingFlap.sprite.rect.width);
			}
			openingPocketTrim.sprite = PostalArtwork.Pocket(delivery);
			openingInk.sprite = PostalArtwork.Stamp(delivery);
			openingInk.enabled = openingInk.sprite != null && HasPostmark(delivery);
			openingTitle.text = "致 " + PostalArtwork.Recipient(delivery);
			letterPaperRect.gameObject.SetActive(value: false);
			opening.gameObject.SetActive(value: true);
			openingTime = 0f;
			DrawLetterOpening();
		}
	}

	private void FinishLetterOpening()
	{
		if (LetterOpening)
		{
			opening.gameObject.SetActive(value: false);
			letterPaperRect.gameObject.SetActive(value: true);
			RebuildLetterRules();
		letterVoiceKey = TownVoiceover.LetterKey((letterTitle != null) ? letterTitle.text : "");
		if (replayVoice != null) replayVoice.gameObject.SetActive(letterVoiceKey != null);
		TownVoiceover.Play(letterVoiceKey);
		}
	}

	private void DrawLetterOpening()
	{
		float num = SmoothBetween(openingTime, 0.29f, 0.72f);
		float num2 = SmoothBetween(openingTime, 0.51f, 1.05f);
		float num3 = SmoothBetween(openingTime, 1.12f, 1.32f);
		opening.alpha = SmoothBetween(openingTime, 0f, 0.12f) * (1f - num3);
		bool flag = num >= 0.5f;
		openingEnvelope.enabled = !flag;
		openingMouth.enabled = flag;
		openingPocket.gameObject.SetActive(flag);
		openingFlap.enabled = openingFlap.sprite != null && num > 0f;
		openingFlap.rectTransform.localScale = new Vector3(1f, 1f - 2f * num, 1f);
		openingFlap.color = Color.Lerp(Color.white, new Color32(226, 196, 153, byte.MaxValue), num);
		unfoldingSlip.gameObject.SetActive(num2 > 0f);
		Pin(unfoldingSlip, 0.5f, 0.5f, 0f, 71f + num2 * 66f, Mathf.Lerp(265f, 330f, num2), 168f * num2);
	}

	private void PinOpeningFlap(float height)
	{
		Pin(openingFlap.rectTransform, 0.5f, 0.5f, 0f, 88f, 394f, height);
		openingFlap.rectTransform.pivot = new Vector2(0.5f, 1f);
	}

	internal void PlayDeliveryStamp(string delivery)
	{
		if (inputClose && !(stampCard == null) && !(PostalArtwork.Stamp(delivery) == null) && stampingDelivery != delivery && !stampQueue.Contains(delivery))
		{
			stampQueue.Enqueue(delivery);
		}
	}

	private void AnimatePostalPresentation()
	{
		if (LetterOpening)
		{
			openingTime += Time.unscaledDeltaTime;
			if (openingTime >= 1.32f)
			{
				FinishLetterOpening();
			}
			else
			{
				DrawLetterOpening();
			}
		}
		if (stampCard == null)
		{
			return;
		}
		if (titleShowing || Paused || LetterOpen || MenuController.OverlayOpen || cinematicOwners.Contains("map"))
		{
			if (stampCard.gameObject.activeSelf)
			{
				stampCard.gameObject.SetActive(value: false);
			}
			return;
		}
		if (stampingDelivery == null)
		{
			if (stampQueue.Count == 0)
			{
				return;
			}
			stampingDelivery = stampQueue.Dequeue();
			stampedEnvelope.sprite = PostalArtwork.Envelope(stampingDelivery);
			stampInk.sprite = PostalArtwork.Stamp(stampingDelivery);
			stampTitle.text = "已投递 · " + PostalArtwork.Recipient(stampingDelivery);
			stampingTime = 0f;
		}
		stampCard.gameObject.SetActive(value: true);
		stampingTime += Time.unscaledDeltaTime;
		if (stampingTime >= 2.8f)
		{
			stampCard.gameObject.SetActive(value: false);
			stampingDelivery = null;
			return;
		}
		float num = SmoothBetween(stampingTime, 0f, 0.25f);
		float num2 = Mathf.Clamp01((stampingTime - 0.24f) / 0.42000002f);
		float num3 = SmoothBetween(stampingTime, 0.66f, 1.02f);
		float num4 = Mathf.Sin(num3 * MathF.PI) * 5f;
		((RectTransform)stampCard.transform).anchoredPosition = new Vector2(-213f, -163f + 32f * (1f - num));
		stampCard.alpha = num * (1f - SmoothBetween(stampingTime, 2.35f, 2.8f));
		stampInk.enabled = stampingTime >= 0.66f;
		stampInk.rectTransform.localScale = Vector3.one * (1f + 0.11f * (1f - num3));
		stampHandle.rectTransform.anchoredPosition = new Vector2(76f + num3 * 22f, Mathf.Lerp(126f, -52f, num2 * num2) + num3 * 115f);
		stampHandle.rectTransform.localScale = new Vector3(1f + num4 / 100f, 1f - num4 / 65f, 1f);
		stampHandle.color = new Color(1f, 1f, 1f, num * (1f - num3));
	}

	private static float SmoothBetween(float time, float from, float to)
	{
		return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, time));
	}
}
