using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class EmoteWheel : MonoBehaviour
{
	private sealed class Item
	{
		public string name;

		public string label;

		public bool expr;

		public ExprId id;

		public int repeat = 1;

		public Image sector;

		public Image icon;

		public Text text;

		public RectTransform rt;
	}

	private static EmoteWheel instance;

	private static readonly (string clip, string label, int repeat)[] Actions = new(string, string, int)[8]
	{
		("wave", "挥手", 2),
		("hop", "欢跳", 1),
		("nod", "点头", 1),
		("shake", "摇头", 1),
		("look", "张望", 1),
		("give", "递礼", 1),
		("pet_dog", "摸小黑", 1),
		("signal_wave", "招呼", 1)
	};

	private const float R = 250f;

	private const float InnerLo = 70f;

	private const float InnerHi = 140f;

	private const float OuterLo = 146f;

	private const float Dead = 46f;

	private EggboyPlayer player;

	private EggboyUI ui;

	private PrototypeGame game;

	private StoryDirector story;

	private TownMap map;

	private DogFollower dog;

	private ChaseCamera chase;

	private bool open;

	private bool restoreChase;

	private float openT;

	private Vector2 pointer;

	private int hover = -1;

	private readonly List<Item> inner = new List<Item>();

	private readonly List<Item> outer = new List<Item>();

	private RectTransform root;

	private RectTransform wheel;

	private RectTransform pointerDot;

	private Image dim;

	private Image centerIcon;

	private Text centerLabel;

	private Text centerHint;

	private static Sprite ringInner;

	private static Sprite ringOuter;

	private static Sprite discSprite;

	private static Sprite thinRing;

	private bool debugHold;

	private static readonly Color32 Paper = new Color32(251, 243, 226, 242);

	private static readonly Color32 PaperInner = new Color32(241, 223, 189, 245);

	private static readonly Color32 Amber = new Color32(240, 140, 60, 250);

	private static readonly Color32 Walnut = new Color32(74, 48, 28, byte.MaxValue);

	private static readonly Color32 Gold = new Color32(201, 154, 82, 230);

	public static bool IsOpen
	{
		get
		{
			if (instance != null)
			{
				return instance.open;
			}
			return false;
		}
	}

	public string LastFired { get; private set; } = "";


	public static EmoteWheel Instance => instance;

	public void Init(EggboyPlayer p, EggboyUI u)
	{
		instance = this;
		player = p;
		ui = u;
		game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		story = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		map = UnityEngine.Object.FindObjectOfType<TownMap>();
	}

	private bool CanOpen()
	{
		if (player == null || ui == null || !ui.inputClose)
		{
			return false;
		}
		if (ui.Paused || ui.TitleShowing || ui.LetterOpen)
		{
			return false;
		}
		if (player.locked || player.ActionLocked || !player.grounded)
		{
			return false;
		}
		if (MenuController.OverlayOpen)
		{
			return false;
		}
		if (map == null)
		{
			map = UnityEngine.Object.FindObjectOfType<TownMap>();
		}
		if (map != null && map.IsOpen)
		{
			return false;
		}
		if (story != null && (story.Cutscene || story.Climbing || story.Ended))
		{
			return false;
		}
		PhysicalPickupController physicalPickupController = PhysicalPickupController.Instance;
		if (physicalPickupController != null && physicalPickupController.Busy)
		{
			return false;
		}
		return true;
	}

	public void DebugShow(int hoverIndex)
	{
		EnsureUI();
		if (!(root == null))
		{
			open = true;
			debugHold = true;
			openT = 1f;
			hover = hoverIndex;
			root.gameObject.SetActive(value: true);
			wheel.localScale = Vector3.one;
			dim.color = new Color(0.1f, 0.06f, 0.03f, 0.34f);
			Item item = ItemAt(hoverIndex);
			if (item != null)
			{
				pointer = item.rt.anchoredPosition;
			}
			pointerDot.anchoredPosition = pointer;
			Restyle();
		}
	}

	public void DebugHide()
	{
		debugHold = false;
		Close(fire: false);
	}

	private void Update()
	{
		if (debugHold)
		{
			return;
		}
		if (!open)
		{
			if (Input.GetMouseButtonDown(2) && CanOpen())
			{
				Open();
			}
			return;
		}
		if (ui.Paused || ui.LetterOpen || player.locked || MenuController.OverlayOpen)
		{
			Close(fire: false);
			return;
		}
		openT += Time.unscaledDeltaTime;
		float num = Mathf.Lerp(0.86f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(openT / 0.12f)));
		wheel.localScale = Vector3.one * num;
		dim.color = new Color(0.1f, 0.06f, 0.03f, 0.34f * Mathf.Clamp01(openT / 0.12f));
		float axisRaw = Input.GetAxisRaw("Mouse X");
		float axisRaw2 = Input.GetAxisRaw("Mouse Y");
		pointer += new Vector2(axisRaw, axisRaw2) * 14f;
		if (pointer.magnitude > 255f)
		{
			pointer = pointer.normalized * 250f * 1.02f;
		}
		pointerDot.anchoredPosition = pointer;
		int num2 = Pick(pointer);
		if (num2 != hover)
		{
			hover = num2;
			ShadowAndChimeSystem.PlayProceduralChime((num2 < 0) ? 392f : 1046.5f, 0.03f);
			Restyle();
		}
		if (!Input.GetMouseButton(2))
		{
			Close(fire: true);
		}
	}

	private int Pick(Vector2 v)
	{
		float magnitude = v.magnitude;
		if (magnitude < 46f)
		{
			return -1;
		}
		float num = Mathf.Repeat(Mathf.Atan2(v.x, v.y) * 57.29578f, 360f);
		if (magnitude < 143f)
		{
			return Mathf.FloorToInt(num / (360f / (float)inner.Count)) % inner.Count;
		}
		return inner.Count + Mathf.FloorToInt(num / (360f / (float)outer.Count)) % outer.Count;
	}

	private Item ItemAt(int i)
	{
		if (i >= 0)
		{
			if (i >= inner.Count)
			{
				return outer[i - inner.Count];
			}
			return inner[i];
		}
		return null;
	}

	private bool Disabled(Item it)
	{
		if (it != null && !it.expr && it.name == "pet_dog")
		{
			return !DogNear();
		}
		return false;
	}

	private bool DogNear()
	{
		if (dog == null)
		{
			dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		}
		if (dog != null && player != null)
		{
			return dog.CanBePetted(player.transform.position);
		}
		return false;
	}

	private void Open()
	{
		EnsureUI();
		if (!(root == null))
		{
			open = true;
			openT = 0f;
			pointer = Vector2.zero;
			hover = -1;
			chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
			restoreChase = chase != null && chase.enabled;
			if (restoreChase)
			{
				chase.enabled = false;
			}
			root.gameObject.SetActive(value: true);
			Restyle();
			ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.06f);
		}
	}

	private void Close(bool fire)
	{
		open = false;
		if (root != null)
		{
			root.gameObject.SetActive(value: false);
		}
		if (restoreChase && chase != null)
		{
			chase.enabled = true;
		}
		restoreChase = false;
		Item item = ItemAt(hover);
		hover = -1;
		if (fire && item != null)
		{
			Fire(item);
		}
	}

	public bool FireByName(string name)
	{
		foreach (Item item in inner)
		{
			if (item.name == name)
			{
				if (Disabled(item))
				{
					return false;
				}
				Fire(item);
				return true;
			}
		}
		foreach (Item item2 in outer)
		{
			if ("expr:" + item2.id == name)
			{
				Fire(item2);
				return true;
			}
		}
		return false;
	}

	private void Fire(Item it)
	{
		LastFired = (it.expr ? ("expr:" + it.id) : it.name);
		if (it.expr)
		{
			if (game == null)
			{
				game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
			}
			if (game != null)
			{
				game.SetBaseExpression(it.id);
			}
			return;
		}
		if (it.name == "pet_dog")
		{
			if (!DogNear())
			{
				ui.ShowToast("小黑不在身边。它在学校那边等你。", 2.5f);
				LastFired = "pet_dog_unavailable";
				return;
			}
			player.FaceTowards(dog.transform.position);
			dog.PetByPlayer();
		}
		if (!player.HasClip(it.name))
		{
			LastFired = "missing:" + it.name;
		}
		else
		{
			player.PlayEmote(it.name, it.repeat);
		}
	}

	private void Restyle()
	{
		for (int i = 0; i < inner.Count + outer.Count; i++)
		{
			Item item = ItemAt(i);
			bool flag = i == hover;
			bool flag2 = Disabled(item);
			item.sector.color = (flag ? ((Color)(flag2 ? new Color32(184, 168, 144, 250) : Amber)) : ((Color)((i < inner.Count) ? PaperInner : Paper)));
			item.sector.rectTransform.localScale = Vector3.one * (flag ? 1.06f : 1f);
			float num = (flag2 ? 0.35f : 1f);
			item.icon.color = new Color(1f, 1f, 1f, num);
			item.rt.localScale = Vector3.one * (flag ? 1.18f : 1f);
			if (item.text != null)
			{
				item.text.color = (flag ? new Color32(byte.MaxValue, 248, 236, byte.MaxValue) : new Color32(59, 42, 26, (byte)(255f * num)));
			}
		}
		Item item2 = ItemAt(hover);
		bool flag3 = item2 != null && item2.expr && game != null && game.BaseExpression == item2.id;
		centerIcon.enabled = item2 != null;
		if (item2 != null)
		{
			centerIcon.sprite = item2.icon.sprite;
		}
		centerIcon.color = (Disabled(item2) ? new Color(1f, 1f, 1f, 0.35f) : Color.white);
		centerLabel.text = ((item2 == null) ? "表情 · 动作" : (item2.label + (flag3 ? "（当前）" : "")));
		centerHint.text = ((item2 == null) ? "移动鼠标选择\u3000松开中键取消" : (Disabled(item2) ? "小黑不在身边" : ("松开中键" + (item2.expr ? "换上这个表情" : "做这个动作"))));
	}

	private void EnsureUI()
	{
		if (root != null)
		{
			return;
		}
		RectTransform rectTransform = TideTownStyleUI.FindHudCanvasRoot(ui);
		if (!(rectTransform == null))
		{
			BuildSprites();
			GameObject gameObject = new GameObject("EmoteWheel", typeof(RectTransform));
			gameObject.transform.SetParent(rectTransform, worldPositionStays: false);
			root = gameObject.GetComponent<RectTransform>();
			root.anchorMin = Vector2.zero;
			root.anchorMax = Vector2.one;
			RectTransform rectTransform2 = root;
			Vector2 offsetMin = (root.offsetMax = Vector2.zero);
			rectTransform2.offsetMin = offsetMin;
			root.SetAsLastSibling();
			dim = TideTownStyleUI.CreatePanel(root, "WheelDim", 10f, 10f, new Color32(26, 16, 8, 80), rounded: false);
			RectTransform rectTransform3 = dim.rectTransform;
			rectTransform3.anchorMin = Vector2.zero;
			rectTransform3.anchorMax = Vector2.one;
			offsetMin = (rectTransform3.offsetMax = Vector2.zero);
			rectTransform3.offsetMin = offsetMin;
			dim.sprite = TideTownStyleUI.SoftVignetteSprite;
			dim.raycastTarget = false;
			GameObject gameObject2 = new GameObject("Wheel", typeof(RectTransform));
			gameObject2.transform.SetParent(root, worldPositionStays: false);
			wheel = gameObject2.GetComponent<RectTransform>();
			wheel.sizeDelta = new Vector2(500f, 500f);
			Disc("Shadow", 518f, new Color32(20, 12, 6, 90), discSprite, new Vector2(4f, -8f));
			Disc("Rim", 508f, Walnut, discSprite, Vector2.zero);
			for (int i = 0; i < 16; i++)
			{
				outer.Add(new Item
				{
					name = "expr:" + EggboyExpressions.All[i],
					label = EggboyExpressions.Names[i],
					expr = true,
					id = EggboyExpressions.All[i]
				});
			}
			(string, string, int)[] actions = Actions;
			foreach ((string, string, int) tuple in actions)
			{
				List<Item> list = inner;
				Item item = new Item();
				(item.name, item.label, item.repeat) = tuple;
				list.Add(item);
			}
			BuildRing(outer, ringOuter, 146f, 250f, 46f, actions: false);
			BuildRing(inner, ringInner, 70f, 140f, 40f, actions: true);
			Disc("OuterLine", 500f, Gold, thinRing, Vector2.zero);
			Disc("MidLine", 292f, Gold, thinRing, Vector2.zero);
			Disc("InnerLine", 280f, Gold, thinRing, Vector2.zero);
			Disc("Center", 134f, Paper, discSprite, Vector2.zero);
			Disc("CenterLine", 134f, Gold, thinRing, Vector2.zero);
			centerIcon = TideTownStyleUI.CreatePanel(wheel, "CenterIcon", 62f, 62f, Color.white, rounded: false);
			centerIcon.rectTransform.anchoredPosition = new Vector2(0f, 14f);
			centerIcon.raycastTarget = false;
			centerIcon.preserveAspect = true;
			centerLabel = TideTownStyleUI.CreateText(wheel, "", 16, TideTownStyleUI.InkDark, TextAnchor.MiddleCenter, new Vector2(0f, -28f), new Vector2(150f, 24f), bold: true);
			centerHint = TideTownStyleUI.CreateText(root, "", 15, new Color32(byte.MaxValue, 243, 220, byte.MaxValue), TextAnchor.MiddleCenter, new Vector2(0f, -284f), new Vector2(520f, 26f), bold: true);
			Shadow shadow = centerHint.gameObject.AddComponent<Shadow>();
			shadow.effectColor = new Color32(30, 18, 8, 200);
			shadow.effectDistance = new Vector2(1.5f, -1.5f);
			Shadow shadow2 = TideTownStyleUI.CreateText(root, "按住中键 · 表情与动作", 15, new Color32(byte.MaxValue, 243, 220, byte.MaxValue), TextAnchor.MiddleCenter, new Vector2(0f, 280f), new Vector2(520f, 26f), bold: true).gameObject.AddComponent<Shadow>();
			shadow2.effectColor = new Color32(30, 18, 8, 200);
			shadow2.effectDistance = new Vector2(1.5f, -1.5f);
			Image image = TideTownStyleUI.CreatePanel(wheel, "Pointer", 14f, 14f, new Color32(192, 58, 44, 235), rounded: false);
			image.sprite = discSprite;
			image.raycastTarget = false;
			pointerDot = image.rectTransform;
			root.gameObject.SetActive(value: false);
		}
	}

	private void Disc(string name, float size, Color32 c, Sprite sp, Vector2 offset)
	{
		Image image = TideTownStyleUI.CreatePanel(wheel, name, size, size, c, rounded: false);
		image.sprite = sp;
		image.raycastTarget = false;
		image.rectTransform.anchoredPosition = offset;
	}

	private void BuildRing(List<Item> items, Sprite ring, float lo, float hi, float iconSize, bool actions)
	{
		int count = items.Count;
		float num = 360f / (float)count;
		FacePainter painter = (actions ? null : new FacePainter());
		for (int i = 0; i < count; i++)
		{
			Item item = items[i];
			Image image = TideTownStyleUI.CreatePanel(wheel, "Sector_" + item.name, 500f, 500f, Paper, rounded: false);
			image.sprite = ring;
			image.type = Image.Type.Filled;
			image.fillMethod = Image.FillMethod.Radial360;
			image.fillOrigin = 2;
			image.fillClockwise = true;
			image.fillAmount = 1f / (float)count - 0.004f;
			image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (float)(-i) * num - 0.2f);
			image.raycastTarget = false;
			item.sector = image;
			Image image2 = TideTownStyleUI.CreatePanel(wheel, "Sep", 1.6f, hi - lo, Gold, rounded: false);
			image2.raycastTarget = false;
			float f = (float)i * num * (MathF.PI / 180f);
			float num2 = (lo + hi) * 0.5f;
			image2.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(f), Mathf.Cos(f)) * num2;
			image2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (float)(-i) * num);
		}
		for (int j = 0; j < count; j++)
		{
			Item item2 = items[j];
			float f2 = ((float)j + 0.5f) * num * (MathF.PI / 180f);
			float num3 = (lo + hi) * 0.5f + (actions ? 6f : 0f);
			GameObject gameObject = new GameObject("Icon_" + item2.name, typeof(RectTransform));
			gameObject.transform.SetParent(wheel, worldPositionStays: false);
			item2.rt = gameObject.GetComponent<RectTransform>();
			item2.rt.anchoredPosition = new Vector2(Mathf.Sin(f2), Mathf.Cos(f2)) * num3;
			Image image3 = TideTownStyleUI.CreatePanel(gameObject.transform, "Img", iconSize, actions ? iconSize : (iconSize * 0.82f), Color.white, rounded: false);
			image3.raycastTarget = false;
			image3.preserveAspect = true;
			image3.sprite = (actions ? ActionIcon(item2.name) : FaceIcon(painter, item2.id));
			item2.icon = image3;
			if (actions)
			{
				image3.rectTransform.anchoredPosition = new Vector2(0f, 7f);
				item2.text = TideTownStyleUI.CreateText(gameObject.transform, item2.label, 12, TideTownStyleUI.InkDark, TextAnchor.MiddleCenter, new Vector2(0f, -22f), new Vector2(70f, 18f), bold: true);
			}
		}
	}

	private static Sprite FaceIcon(FacePainter painter, ExprId id)
	{
		painter.Set(id);
		Color32[] pixels = painter.Texture.GetPixels32();
		int num = 512;
		int num2 = 400;
		Texture2D texture2D = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false);
		Color32[] array = new Color32[num * num2];
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				float num3 = Mathf.Max(0f, Mathf.Abs((float)j - (float)num * 0.5f) - ((float)num * 0.5f - 70f));
				float num4 = Mathf.Max(0f, Mathf.Abs((float)i - (float)num2 * 0.5f) - ((float)num2 * 0.5f - 70f));
				float num5 = Mathf.Clamp01(70f - Mathf.Sqrt(num3 * num3 + num4 * num4));
				Color32 color = pixels[i * num + j];
				float num6 = (float)(int)color.a / 255f;
				Color32 color2 = new Color32(28, 23, 20, byte.MaxValue);
				array[i * num + j] = new Color32((byte)((float)(int)color2.r + (float)(color.r - color2.r) * num6), (byte)((float)(int)color2.g + (float)(color.g - color2.g) * num6), (byte)((float)(int)color2.b + (float)(color.b - color2.b) * num6), (byte)(255f * num5));
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, num, num2), new Vector2(0.5f, 0.5f));
	}

	private static Sprite ActionIcon(string clip)
	{
		PixelCanvas c = new PixelCanvas(128, 128);
		c.Clear(new Color32(0, 0, 0, 0));
		Color32 ink = new Color32(74, 48, 28, byte.MaxValue);
		Color32 c2 = new Color32(224, 106, 42, byte.MaxValue);
		switch (clip)
		{
		case "wave":
		{
			c.Circle(64f, 70f, 20f, ink);
			c.Circle(64f, 70f, 14f, new Color32(251, 243, 226, byte.MaxValue));
			for (int i = 0; i < 4; i++)
			{
				c.Line(52f + (float)i * 8f, 54f, 50f + (float)i * 9f, 28f, 7f, ink);
			}
			c.Arc(64f, 66f, 44f, -2.6f, -1.9f, 5f, c2);
			c.Arc(64f, 66f, 44f, -1.2f, -0.5f, 5f, c2);
			break;
		}
		case "hop":
			c.Line(24f, 108f, 104f, 108f, 6f, ink);
			c.Arc(64f, 70f, 34f, 3.6128316f, 5.811947f, 5f, c2);
			c.Line(64f, 88f, 64f, 26f, 8f, ink);
			c.Line(64f, 24f, 46f, 44f, 8f, ink);
			c.Line(64f, 24f, 82f, 44f, 8f, ink);
			break;
		case "nod":
			Head(64f, 70f, 28f);
			c.Line(108f, 40f, 108f, 96f, 6f, c2);
			c.Line(108f, 98f, 98f, 86f, 6f, c2);
			c.Line(108f, 98f, 118f, 86f, 6f, c2);
			break;
		case "shake":
			Head(64f, 62f, 28f);
			c.Line(30f, 108f, 98f, 108f, 6f, c2);
			c.Line(26f, 108f, 38f, 100f, 6f, c2);
			c.Line(26f, 108f, 38f, 116f, 6f, c2);
			c.Line(102f, 108f, 90f, 100f, 6f, c2);
			c.Line(102f, 108f, 90f, 116f, 6f, c2);
			break;
		case "look":
			c.Arc(64f, 92f, 50f, 3.7699115f, 5.6548667f, 7f, ink);
			c.Arc(64f, 36f, 50f, MathF.PI / 5f, MathF.PI * 4f / 5f, 7f, ink);
			c.Circle(64f, 64f, 15f, ink);
			c.Circle(58f, 58f, 5f, new Color32(251, 243, 226, byte.MaxValue));
			break;
		case "give":
			c.Line(18f, 94f, 54f, 82f, 7f, ink);
			c.Line(110f, 94f, 74f, 82f, 7f, ink);
			c.Line(54f, 82f, 74f, 82f, 7f, ink);
			c.Line(42f, 48f, 86f, 48f, 6f, c2);
			c.Line(86f, 48f, 86f, 76f, 6f, c2);
			c.Line(86f, 76f, 42f, 76f, 6f, c2);
			c.Line(42f, 76f, 42f, 48f, 6f, c2);
			c.Line(64f, 48f, 64f, 76f, 5f, c2);
			c.Arc(56f, 42f, 8f, MathF.PI, MathF.PI * 2f, 5f, c2);
			c.Arc(72f, 42f, 8f, MathF.PI, MathF.PI * 2f, 5f, c2);
			break;
		case "pet_dog":
			c.Circle(58f, 74f, 30f, ink);
			c.Polygon(new Vector2[3]
			{
				new Vector2(32f, 58f),
				new Vector2(26f, 96f),
				new Vector2(44f, 70f)
			}, ink);
			c.Polygon(new Vector2[3]
			{
				new Vector2(84f, 58f),
				new Vector2(90f, 96f),
				new Vector2(72f, 70f)
			}, ink);
			c.Circle(48f, 70f, 4f, new Color32(251, 243, 226, byte.MaxValue));
			c.Circle(68f, 70f, 4f, new Color32(251, 243, 226, byte.MaxValue));
			c.Circle(96f, 30f, 9f, c2);
			c.Circle(110f, 30f, 9f, c2);
			c.Polygon(new Vector2[3]
			{
				new Vector2(88f, 33f),
				new Vector2(118f, 33f),
				new Vector2(103f, 50f)
			}, c2);
			break;
		default:
			c.Line(54f, 112f, 64f, 40f, 9f, ink);
			c.Circle(64f, 32f, 13f, ink);
			c.Line(96f, 20f, 96f, 44f, 5f, c2);
			c.Line(84f, 32f, 108f, 32f, 5f, c2);
			c.Line(22f, 40f, 22f, 56f, 4f, c2);
			c.Line(14f, 48f, 30f, 48f, 4f, c2);
			break;
		}
		Texture2D texture2D = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: false);
		c.Upload(texture2D);
		return Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
		void Head(float cx, float cy, float r)
		{
			c.Arc(cx, cy, r, 0f, MathF.PI * 2f, 6f, ink);
			c.Line(cx - r * 0.35f, cy - r * 0.05f, cx - r * 0.35f, cy + r * 0.12f, 5f, ink);
			c.Line(cx + r * 0.35f, cy - r * 0.05f, cx + r * 0.35f, cy + r * 0.12f, 5f, ink);
		}
	}

	private static void BuildSprites()
	{
		if (!(discSprite != null))
		{
			ringOuter = Ring(0.584f, 1f);
			ringInner = Ring(0.28f, 0.56f);
			discSprite = Ring(0f, 1f);
			thinRing = Ring(0.988f, 1f);
		}
	}

	private static Sprite Ring(float lo, float hi)
	{
		Texture2D texture2D = new Texture2D(512, 512, TextureFormat.RGBA32, mipChain: false);
		Color32[] array = new Color32[262144];
		float num = 256f;
		for (int i = 0; i < 512; i++)
		{
			for (int j = 0; j < 512; j++)
			{
				float num2 = Mathf.Sqrt(((float)j + 0.5f - num) * ((float)j + 0.5f - num) + ((float)i + 0.5f - num) * ((float)i + 0.5f - num)) / num;
				float num3 = Mathf.Clamp01((hi - num2) * num) * ((lo <= 0f) ? 1f : Mathf.Clamp01((num2 - lo) * num));
				array[i * 512 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num3 * 255f));
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, 512f, 512f), new Vector2(0.5f, 0.5f));
	}
}
