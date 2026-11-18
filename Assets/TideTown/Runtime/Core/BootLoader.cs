using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BootLoader : MonoBehaviour
{
	private static readonly string[] Tips = new string[6] { "按 M 打开航海地图，金色路标会带你去下一站。", "走到发光的东西旁边按 E，看看它想告诉你什么。", "小黑会跟着你走。累了的时候，它也会坐下来等你。", "潮水会涨也会退——有些路，只在退潮时才走得通。", "按住鼠标中键，可以挥手、跳一跳，或者和小黑打招呼。", "迷路了就按 H，邮局会给你一点小提示。" };

	private static readonly Color32 Paper = new Color32(246, 234, 210, byte.MaxValue);

	private static readonly Color32 PaperDeep = new Color32(230, 210, 174, byte.MaxValue);

	private static readonly Color32 Gold = new Color32(217, 162, 74, byte.MaxValue);

	private static readonly Color32 RouteGray = new Color32(185, 165, 130, byte.MaxValue);

	private CanvasGroup group;

	private RectTransform avatar;

	private Image[] dots;

	private Text percent;

	private Text tip;

	private float shown;

	private bool automated;

	public static bool WarmupBusy;

	private static Sprite circle;

	public static bool Opaque { get; private set; }

	public static Sprite CircleSprite
	{
		get
		{
			if (circle != null)
			{
				return circle;
			}
			Texture2D texture2D = new Texture2D(96, 96, TextureFormat.RGBA32, mipChain: false)
			{
				wrapMode = TextureWrapMode.Clamp
			};
			Color32[] array = new Color32[9216];
			for (int i = 0; i < 96; i++)
			{
				for (int j = 0; j < 96; j++)
				{
					float num = Vector2.Distance(new Vector2((float)j + 0.5f, (float)i + 0.5f), new Vector2(48f, 48f));
					byte a = (byte)(Mathf.Clamp01(48f - num) * 255f);
					array[i * 96 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, a);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			circle = Sprite.Create(texture2D, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 100f);
			return circle;
		}
	}

	public static bool IsAutomatedLaunch()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 1; i < commandLineArgs.Length; i++)
		{
			string text = commandLineArgs[i];
			if (text.StartsWith("-") && !text.StartsWith("-screen"))
			{
				switch (text)
				{
				case "-logFile":
				case "-quality":
				case "-popupwindow":
				case "-flylog":
				case "-resetsave":
					continue;
				}
				return true;
			}
		}
		return Application.isBatchMode;
	}

	private void OnDestroy()
	{
		Opaque = false;
	}

	private void Start()
	{
		automated = IsAutomatedLaunch();
		Opaque = true;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		Build();
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		float started = Time.realtimeSinceStartup;
		int tipIndex = UnityEngine.Random.Range(0, Tips.Length);
		float nextTip = 0f;
		yield return null;
		AsyncOperation op = SceneManager.LoadSceneAsync("TideTown_Main");
		op.allowSceneActivation = false;
		bool bootShot = Array.IndexOf(Environment.GetCommandLineArgs(), "-bootshot") >= 0;
		float minShow = (bootShot ? 3f : (automated ? 0f : 2.2f));
		bool shotTaken = false;
		while (true)
		{
			float a = Mathf.Clamp01(op.progress / 0.9f);
			float num = Time.realtimeSinceStartup - started;
			float b = ((minShow > 0f) ? Mathf.Clamp01(num / minShow) : 1f);
			float num2 = Mathf.Min(a, b) * 0.92f;
			shown = Mathf.MoveTowards(shown, num2, Time.unscaledDeltaTime * 0.9f);
			if (Time.realtimeSinceStartup >= nextTip)
			{
				tip.text = "邮差小贴士\u3000" + Tips[tipIndex % Tips.Length];
				tipIndex++;
				nextTip = Time.realtimeSinceStartup + 3.4f;
			}
			Paint(shown);
			if (bootShot && !shotTaken && num > 1.6f)
			{
				shotTaken = true;
				string text = Path.Combine(Application.dataPath, "..", "shots");
				Directory.CreateDirectory(text);
				ScreenCapture.CaptureScreenshot(Path.Combine(text, "boot_loading.png"));
			}
			if (op.progress >= 0.9f && num >= minShow && shown >= num2 - 0.01f)
			{
				break;
			}
			yield return null;
		}
		Paint(0.95f);
		op.allowSceneActivation = true;
		while (!op.isDone)
		{
			yield return null;
		}
		for (int j = 0; j < 6; j++)
		{
			Paint(Mathf.Lerp(0.95f, 1f, (float)j / 5f));
			yield return null;
		}
		for (int j = 0; j < 30; j++)
		{
			if (automated)
			{
				break;
			}
			if (!(TitleScreen.Current == null))
			{
				break;
			}
			Paint(1f);
			yield return null;
		}
		float warmUntil = Time.realtimeSinceStartup + 6f;
		while (WarmupBusy && Time.realtimeSinceStartup < warmUntil)
		{
			Paint(1f);
			yield return null;
		}
		Opaque = false;
		Debug.Log($"BOOT reveal frame={Time.frameCount} title={TitleScreen.Current != null}");
		float fade = (automated ? 0.05f : 0.75f);
		for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
		{
			group.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / fade);
			yield return null;
		}
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private void Paint(float p)
	{
		int num = Mathf.FloorToInt(p * (float)dots.Length);
		for (int i = 0; i < dots.Length; i++)
		{
			dots[i].color = ((i < num) ? ((Color)Gold) : ((Color)RouteGray));
		}
		if (avatar != null)
		{
			float width = ((RectTransform)avatar.parent).rect.width;
			float num2 = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f)) * 7f;
			avatar.anchoredPosition = new Vector2((0f - width) * 0.5f + width * p, 30f + num2);
			avatar.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 7f) * 5f);
		}
		if (percent != null)
		{
			percent.text = "整理邮包\u3000" + Mathf.RoundToInt(p * 100f) + "%";
		}
	}

	private void Build()
	{
		GameObject gameObject = new GameObject("BootCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		Canvas component = gameObject.GetComponent<Canvas>();
		component.renderMode = RenderMode.ScreenSpaceOverlay;
		component.sortingOrder = 32000;
		CanvasScaler component2 = gameObject.GetComponent<CanvasScaler>();
		component2.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		component2.referenceResolution = new Vector2(1920f, 1080f);
		component2.matchWidthOrHeight = 0.5f;
		group = gameObject.GetComponent<CanvasGroup>();
		RectTransform parent = (RectTransform)gameObject.transform;
		Stretch(Img(parent, "Paper", GradientSprite(Paper, PaperDeep), Color.white).rectTransform);
		Stretch(Img(parent, "Vignette", TideTownStyleUI.SoftVignetteSprite, new Color32(107, 74, 42, 70)).rectTransform);
		Image image = Img(parent, "PostcardFrame", TideTownStyleUI.PaperSprite, new Color32(byte.MaxValue, 250, 239, byte.MaxValue));
		Place(image.rectTransform, new Vector2(-330f, 70f), new Vector2(760f, 520f));
		image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);
		Shadow(image);
		Image image2 = Img(image.rectTransform, "Postcard", PostalArtwork.Postcard, Color.white);
		image2.preserveAspect = true;
		Place(image2.rectTransform, Vector2.zero, new Vector2(712f, 472f));
		Image image3 = Img(image.rectTransform, "Tape", TideTownStyleUI.PaperSprite, new Color32(163, 206, 208, 220));
		Place(image3.rectTransform, new Vector2(-190f, 252f), new Vector2(170f, 34f));
		image3.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);
		Txt(parent, "记忆邮局", 30, TideTownStyleUI.Clay, new Vector2(470f, 190f), new Vector2(520f, 44f), bold: true, TextAnchor.MiddleLeft);
		Txt(parent, "潮汐镇", 104, TideTownStyleUI.SeaBlue, new Vector2(470f, 100f), new Vector2(520f, 130f), bold: true, TextAnchor.MiddleLeft);
		Txt(parent, "EGGBOY · STILL DELIVERING", 24, TideTownStyleUI.InkMuted, new Vector2(470f, 18f), new Vector2(520f, 34f), bold: true, TextAnchor.MiddleLeft);
		Txt(parent, "正在整理今天的邮包，马上出发……", 28, TideTownStyleUI.InkDark, new Vector2(470f, -40f), new Vector2(560f, 40f), bold: false, TextAnchor.MiddleLeft);
		Image image4 = Img(parent, "Postmark", TideTownStyleUI.PostmarkRingSprite, new Color32(165, 75, 53, 150));
		Place(image4.rectTransform, new Vector2(760f, 150f), new Vector2(150f, 150f));
		image4.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
		RectTransform component3 = new GameObject("Road", typeof(RectTransform)).GetComponent<RectTransform>();
		component3.SetParent(parent, worldPositionStays: false);
		Place(component3, new Vector2(-40f, -300f), new Vector2(1240f, 20f));
		dots = new Image[48];
		for (int i = 0; i < 48; i++)
		{
			Image image5 = Img(component3, "dot" + i, CircleSprite, RouteGray);
			float x = -620f + 1240f * ((float)i + 0.5f) / 48f;
			Place(image5.rectTransform, new Vector2(x, Mathf.Sin((float)i * 0.45f) * 6f), new Vector2(13f, 13f));
			dots[i] = image5;
		}
		Place(Img(component3, "GoalMark", TideTownStyleUI.PostmarkRingSprite, new Color32(165, 75, 53, 230)).rectTransform, new Vector2(660f, 6f), new Vector2(70f, 70f));
		Texture2D texture2D = Resources.Load<Texture2D>("UI/eggboy_map_avatar");
		Image image6 = Img(component3, "Avatar", CircleSprite, new Color32(41, 58, 57, byte.MaxValue));
		Place(image6.rectTransform, new Vector2(-620f, 30f), new Vector2(86f, 86f));
		Image image7 = Img(image6.rectTransform, "Ring", CircleSprite, new Color32(byte.MaxValue, 245, 223, byte.MaxValue));
		Place(image7.rectTransform, Vector2.zero, new Vector2(78f, 78f));
		image7.gameObject.AddComponent<Mask>().showMaskGraphic = true;
		if (texture2D != null)
		{
			RawImage component4 = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
			component4.transform.SetParent(image7.rectTransform, worldPositionStays: false);
			component4.texture = texture2D;
			component4.raycastTarget = false;
			Place(component4.rectTransform, Vector2.zero, new Vector2(70f, 70f));
		}
		avatar = image6.rectTransform;
		percent = Txt(parent, "整理邮包\u30000%", 26, TideTownStyleUI.SeaBlue, new Vector2(-40f, -362f), new Vector2(600f, 36f), bold: true, TextAnchor.MiddleCenter);
		tip = Txt(parent, "", 24, TideTownStyleUI.InkMuted, new Vector2(0f, -440f), new Vector2(1500f, 34f), bold: false, TextAnchor.MiddleCenter);
	}

	private static Sprite GradientSprite(Color32 top, Color32 bottom)
	{
		Texture2D texture2D = new Texture2D(4, 64, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		Color32[] array = new Color32[256];
		for (int i = 0; i < 64; i++)
		{
			Color32 color = Color32.Lerp(bottom, top, (float)i / 63f);
			for (int j = 0; j < 4; j++)
			{
				array[i * 4 + j] = color;
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return Sprite.Create(texture2D, new Rect(0f, 0f, 4f, 64f), new Vector2(0.5f, 0.5f));
	}

	private static Image Img(Transform parent, string name, Sprite sprite, Color color)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(parent, worldPositionStays: false);
		Image component = obj.GetComponent<Image>();
		component.sprite = sprite;
		component.color = color;
		component.raycastTarget = false;
		if (sprite != null && sprite.border.sqrMagnitude > 0f)
		{
			component.type = Image.Type.Sliced;
		}
		return component;
	}

	private static Text Txt(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box, bool bold, TextAnchor anchor)
	{
		GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(Text));
		obj.transform.SetParent(parent, worldPositionStays: false);
		Text component = obj.GetComponent<Text>();
		component.font = TideTownStyleUI.UiFont;
		component.text = text;
		component.fontSize = size;
		component.fontStyle = (bold ? FontStyle.Bold : FontStyle.Normal);
		component.color = color;
		component.alignment = anchor;
		component.horizontalOverflow = HorizontalWrapMode.Overflow;
		component.verticalOverflow = VerticalWrapMode.Overflow;
		component.raycastTarget = false;
		Place(component.rectTransform, pos, box);
		return component;
	}

	private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
	{
		Vector2 vector2 = (rt.pivot = new Vector2(0.5f, 0.5f));
		Vector2 anchorMin = (rt.anchorMax = vector2);
		rt.anchorMin = anchorMin;
		rt.anchoredPosition = pos;
		rt.sizeDelta = size;
	}

	private static void Stretch(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		Vector2 offsetMin = (rt.offsetMax = Vector2.zero);
		rt.offsetMin = offsetMin;
	}

	private static void Shadow(Image img)
	{
		Shadow shadow = img.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0.25f, 0.16f, 0.08f, 0.28f);
		shadow.effectDistance = new Vector2(10f, -12f);
	}
}
