using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WishInspectModal : MonoBehaviour
{
	private static WishInspectModal instance;

	private bool active;

	private WishToy toy;

	private EggboyPlayer player;

	private EggboyUI ui;

	private ControlGate gate;

	private object gateToken;

	private ChaseCamera chase;

	private bool restoreChase;

	private RectTransform card;

	private Text titleText;

	private Text plateText;

	private Text plateSubText;

	private Text blurbText;

	private Text hintText;

	private Text bagText;

	private Button bagButton;

	private Text bagButtonText;

	private GameObject rig;

	private Transform pivot;

	private Light keyLight;

	private bool overlayHeld;

	private Vector3 euler = new Vector3(12f, -30f, 0f);

	private float zoom = 1f;

	private float idleSpin;

	private float lastClick;

	private Vector2 lastMouse;

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

	public static WishToy Current
	{
		get
		{
			if (!(instance != null) || !instance.active)
			{
				return null;
			}
			return instance.toy;
		}
	}

	public static void Open(WishToy t, EggboyPlayer p)
	{
		if (!(t == null))
		{
			if (instance == null)
			{
				GameObject obj = new GameObject("WishInspectModal");
				UnityEngine.Object.DontDestroyOnLoad(obj);
				instance = obj.AddComponent<WishInspectModal>();
			}
			instance.Show(t, p);
		}
	}

	public static void DebugPutBack()
	{
		if (IsOpen)
		{
			instance.Close(store: false);
		}
	}

	public static bool DebugStore()
	{
		if (!IsOpen || WishBag.Full)
		{
			return false;
		}
		instance.Close(store: true);
		return true;
	}

	private void Show(WishToy t, EggboyPlayer p)
	{
		if (active)
		{
			Close(store: false);
		}
		toy = t;
		player = p;
		ui = UnityEngine.Object.FindObjectOfType<EggboyUI>();
		GameBootstrap gameBootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
		gate = ((gameBootstrap != null) ? gameBootstrap.gate : null);
		if (gate != null)
		{
			gateToken = gate.Acquire("wish_inspect");
		}
		if (player != null)
		{
			player.locked = true;
		}
		chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		restoreChase = chase != null && chase.enabled;
		if (restoreChase)
		{
			chase.enabled = false;
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		MenuController.NoteScriptUnlock();
		euler = new Vector3(12f, -30f, 0f);
		zoom = 1f;
		idleSpin = 0f;
		lastMouse = Input.mousePosition;
		EnsureUI();
		if (card != null)
		{
			card.gameObject.SetActive(value: true);
		}
		Refresh();
		BuildRig();
		active = true;
		ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.18f);
	}

	private void EnsureUI()
	{
		if (card != null)
		{
			return;
		}
		RectTransform rectTransform = TideTownStyleUI.FindHudCanvasRoot(ui);
		if (rectTransform == null)
		{
			return;
		}
		card = TideTownStyleUI.CreateParchmentCard(rectTransform, "WishInspectCard", 500f, 470f);
		RectTransform rectTransform2 = card;
		Vector2 anchorMin = (card.anchorMax = new Vector2(0f, 0.5f));
		rectTransform2.anchorMin = anchorMin;
		card.pivot = new Vector2(0f, 0.5f);
		card.anchoredPosition = new Vector2(28f, 10f);
		titleText = TideTownStyleUI.CreateText(card, "", 22, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, new Vector2(0f, 196f), new Vector2(440f, 32f), bold: true);
		Image image = TideTownStyleUI.CreatePanel(card, "WishBrassPlate", 440f, 78f, new Color32(201, 154, 82, byte.MaxValue));
		image.rectTransform.anchoredPosition = new Vector2(0f, 128f);
		Outline outline = image.gameObject.AddComponent<Outline>();
		outline.effectColor = new Color32(106, 69, 28, 220);
		outline.effectDistance = new Vector2(1.5f, -1.5f);
		Image image2 = TideTownStyleUI.CreatePanel(image.transform, "PlateInner", 424f, 62f, new Color32(226, 187, 116, byte.MaxValue));
		image2.sprite = TideTownStyleUI.RoundedBorderSprite;
		image2.rectTransform.anchoredPosition = Vector2.zero;
		float[] array = new float[2] { -200f, 200f };
		foreach (float x in array)
		{
			TideTownStyleUI.CreatePanel(image.transform, "Rivet", 9f, 9f, new Color32(138, 94, 34, byte.MaxValue)).rectTransform.anchoredPosition = new Vector2(x, 0f);
		}
		plateText = TideTownStyleUI.CreateText(image.transform, "", 18, new Color32(74, 44, 16, byte.MaxValue), TextAnchor.MiddleCenter, new Vector2(0f, 12f), new Vector2(380f, 28f), bold: true);
		plateSubText = TideTownStyleUI.CreateText(image.transform, "", 13, new Color32(106, 69, 28, byte.MaxValue), TextAnchor.MiddleCenter, new Vector2(0f, -15f), new Vector2(380f, 22f));
		blurbText = TideTownStyleUI.CreateText(card, "", 15, TideTownStyleUI.InkDark, TextAnchor.UpperLeft, new Vector2(0f, 26f), new Vector2(440f, 110f));
		hintText = TideTownStyleUI.CreateText(card, "拖动鼠标 / WASD 转着看\u3000滚轮远近\u3000双击回正", 13, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft, new Vector2(0f, -66f), new Vector2(440f, 24f));
		bagText = TideTownStyleUI.CreateText(card, "", 13, TideTownStyleUI.InkMuted, TextAnchor.UpperLeft, new Vector2(0f, -110f), new Vector2(440f, 44f));
		TideTownStyleUI.CreateWarmButton(card, "放回原处（Esc / 空格）", new Vector2(-112f, -190f), new Vector2(212f, 40f), primaryAmber: true, delegate
		{
			Close(store: false);
		});
		bagButton = TideTownStyleUI.CreateWarmButton(card, "收进邮差包", new Vector2(116f, -190f), new Vector2(200f, 40f), primaryAmber: false, delegate
		{
			if (!WishBag.Full)
			{
				Close(store: true);
			}
		});
		bagButtonText = bagButton.GetComponentInChildren<Text>();
	}

	private void Refresh()
	{
		if (!(card == null) && !(toy == null))
		{
			titleText.text = "心愿端详 · 「" + toy.description + "」";
			plateText.text = "心愿 No." + toy.number.ToString("00") + "\u3000「" + Short(toy.description, 12) + "」";
			string text = ((toy.spawn == null) ? "" : ((toy.spawn.state == "swapped" || toy.spawn.state == "cached") ? "\u3000·\u3000Lux3D 现做" : ((toy.spawn.state == "generating") ? "\u3000·\u3000正在照心愿重做" : "\u3000·\u3000镇上找来的同款")));
			plateSubText.text = WishBlurb.CategoryName(toy.category) + "\u3000·\u3000寄给：" + toy.recipient + text;
			blurbText.text = toy.blurb;
			bagText.text = WishBag.Summary();
			bool interactable = toy.bagAllowed && !WishBag.Full;
			bagButton.interactable = interactable;
			bagButtonText.text = ((!toy.bagAllowed) ? "这件留在原处" : (WishBag.Full ? ("邮差包满了（" + 6 + "/" + 6 + "）") : ("收进邮差包（" + WishBag.Count + "/" + 6 + "）")));
		}
	}

	private static string Short(string s, int n)
	{
		if (!string.IsNullOrEmpty(s) && s.Length > n)
		{
			return s.Substring(0, n) + "…";
		}
		return s;
	}

	private void BuildRig()
	{
		Camera main = Camera.main;
		if (!(main == null) && !(toy == null))
		{
			rig = new GameObject("WishInspectRig");
			rig.transform.SetParent(main.transform, worldPositionStays: false);
			float num = 2.3f;
			float num2 = Mathf.Tan(main.fieldOfView * (MathF.PI / 180f) * 0.5f) * num;
			rig.transform.localPosition = new Vector3(0.33f * main.aspect * num2, 0.02f, num);
			GameObject gameObject = new GameObject("WishKeyLight");
			gameObject.transform.SetParent(rig.transform, worldPositionStays: false);
			gameObject.transform.localPosition = new Vector3(-0.6f, 0.7f, -0.9f);
			keyLight = gameObject.AddComponent<Light>();
			keyLight.type = LightType.Point;
			keyLight.color = OverlayRender.SceneKeyColor;
			keyLight.range = 4.5f;
			keyLight.intensity = 0.9f;
			keyLight.shadows = LightShadows.None;
			ObjectInspectModal.AddRimLight(rig.transform);
			GameObject gameObject2 = new GameObject("WishPivot");
			gameObject2.transform.SetParent(rig.transform, worldPositionStays: false);
			pivot = gameObject2.transform;
			Transform transform = toy.transform.Find("Model");
			GameObject gameObject3 = UnityEngine.Object.Instantiate((transform != null) ? transform.gameObject : toy.gameObject);
			Light[] componentsInChildren = gameObject3.GetComponentsInChildren<Light>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				UnityEngine.Object.Destroy(componentsInChildren[i].gameObject);
			}
			Collider[] componentsInChildren2 = gameObject3.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				UnityEngine.Object.Destroy(componentsInChildren2[i]);
			}
			MonoBehaviour[] componentsInChildren3 = gameObject3.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
			for (int i = 0; i < componentsInChildren3.Length; i++)
			{
				UnityEngine.Object.Destroy(componentsInChildren3[i]);
			}
			gameObject3.transform.SetParent(pivot, worldPositionStays: false);
			gameObject3.transform.localPosition = Vector3.zero;
			gameObject3.transform.localRotation = Quaternion.identity;
			gameObject3.transform.localScale = Vector3.one;
			Bounds bounds = Lux3DModelFit.WorldBounds(gameObject3);
			float num3 = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
			if (num3 > 0.0001f)
			{
				gameObject3.transform.localScale = Vector3.one * (0.78f / num3);
			}
			bounds = Lux3DModelFit.WorldBounds(gameObject3);
			gameObject3.transform.position += pivot.position - bounds.center;
			pivot.localRotation = Quaternion.Euler(euler);
			OverlayRender.SetLayerRecursive(rig, 30);
			OverlayRender.LimitLights(rig);
			OverlayRender.Acquire(main, ownLighting: true);
			overlayHeld = true;
		}
	}

	private void Update()
	{
		if (!active)
		{
			return;
		}
		if (toy == null)
		{
			Close(store: false);
			return;
		}
		Vector2 vector = Input.mousePosition;
		Vector2 vector2 = vector - lastMouse;
		lastMouse = vector;
		float num = 0f;
		float num2 = 0f;
		if ((Input.GetMouseButton(0) || Input.GetMouseButton(1)) && (!(EventSystem.current != null) || !EventSystem.current.IsPointerOverGameObject()))
		{
			num2 = (0f - vector2.x) * 0.45f;
			num = vector2.y * 0.45f;
		}
		if (Input.GetMouseButtonDown(0))
		{
			if (Time.unscaledTime - lastClick < 0.3f)
			{
				euler = new Vector3(12f, -30f, 0f);
				zoom = 1f;
			}
			lastClick = Time.unscaledTime;
		}
		if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
		{
			num2 += 95f * Time.unscaledDeltaTime;
		}
		if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
		{
			num2 -= 95f * Time.unscaledDeltaTime;
		}
		if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
		{
			num += 85f * Time.unscaledDeltaTime;
		}
		if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
		{
			num -= 85f * Time.unscaledDeltaTime;
		}
		float y = Input.mouseScrollDelta.y;
		if (Mathf.Abs(y) > 0.01f)
		{
			zoom = Mathf.Clamp(zoom * Mathf.Exp(y * 0.12f), 0.6f, 1.7f);
		}
		if (Mathf.Abs(num) + Mathf.Abs(num2) > 0.001f)
		{
			idleSpin = 0f;
		}
		else
		{
			idleSpin += Time.unscaledDeltaTime;
		}
		if (idleSpin > 2.5f)
		{
			num2 -= 14f * Time.unscaledDeltaTime;
		}
		euler.x = Mathf.Clamp(euler.x + num, -85f, 85f);
		euler.y += num2;
		if (pivot != null)
		{
			pivot.localRotation = Quaternion.Euler(euler);
			pivot.localPosition = new Vector3(0f, Mathf.Sin(Time.unscaledTime * 1.6f) * 0.015f, 0f);
			rig.transform.localScale = Vector3.one * zoom;
		}
		OverlayRender.SyncFov();
		if (blurbText != null && blurbText.text != toy.blurb)
		{
			blurbText.text = toy.blurb;
		}
		if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
		{
			Close(store: false);
		}
	}

	private void Close(bool store)
	{
		if (!active)
		{
			return;
		}
		active = false;
		if (overlayHeld)
		{
			OverlayRender.Release(ownLighting: true);
			overlayHeld = false;
		}
		if (rig != null)
		{
			UnityEngine.Object.Destroy(rig);
		}
		rig = null;
		pivot = null;
		keyLight = null;
		if (card != null)
		{
			card.gameObject.SetActive(value: false);
		}
		if (restoreChase && chase != null)
		{
			chase.enabled = true;
		}
		restoreChase = false;
		if (gate != null && gateToken != null)
		{
			gate.Release(gateToken);
			gateToken = null;
		}
		if (player != null)
		{
			player.locked = false;
		}
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
		WishToy wishToy = toy;
		toy = null;
		if (wishToy == null)
		{
			return;
		}
		if (store && wishToy.bagAllowed && !WishBag.Full)
		{
			wishToy.StoreInBag();
			if (ui != null)
			{
				ui.ShowToast("「" + wishToy.description + "」收进了邮差包（" + WishBag.Count + "/" + 6 + "）");
			}
			ShadowAndChimeSystem.PlayProceduralChime(987.77f, 0.2f);
		}
		else
		{
			wishToy.PutBack();
		}
	}
}
