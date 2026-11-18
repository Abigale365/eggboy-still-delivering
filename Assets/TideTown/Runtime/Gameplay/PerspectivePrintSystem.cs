using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class PerspectivePrintSystem : MonoBehaviour
{
	private sealed class Item
	{
		public string id;

		public string title;

		public string lib;

		public string scene;

		public float hold;

		public Func<GameState, bool> unlocked;
	}

	private static readonly Item[] Items = new Item[3]
	{
		new Item
		{
			id = "ruler",
			title = "小木尺",
			lib = "print_ruler",
			hold = 0.28f,
			unlocked = (GameState s) => true
		},
		new Item
		{
			id = "boat",
			title = "微缩船模",
			scene = "S9_小船_分享包版",
			hold = 0.24f,
			unlocked = (GameState s) => true
		},
		new Item
		{
			id = "slipper",
			title = "旧拖鞋",
			scene = "C3_被咬过的拖鞋_分享包版",
			hold = 0.2f,
			unlocked = (GameState s) => s.flags.Contains("slipper_seen")
		}
	};

	private const float MaxPrintSize = 12f;

	private const float MinScale = 1.3f;

	private const int MaxPrints = 3;

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private GameState state;

	private ChaseCamera chase;

	private Camera cam;

	private WorldMechanismDirector ferry;

	private int openedFrame;

	private int itemIndex;

	private float viewYaw;

	private float viewPitch;

	private float holdDist = 0.6f;

	private float heldYaw = 90f;

	private Transform held;

	private GameObject heldModel;

	private bool restoreChase;

	private int groundMask;

	private Vector3 aimCamPos;

	private bool aimBridgesFerry;

	private readonly List<GameObject> prints = new List<GameObject>();

	private RectTransform frame;

	private readonly List<Image> corners = new List<Image>();

	private readonly Image[] bankMarkers = new Image[2];

	private readonly List<Renderer> hiddenPlayerParts = new List<Renderer>();

	private Text label;

	private Image flash;

	private float flashT;

	public bool IsViewfinderActive { get; private set; }

	public bool AimValid { get; private set; }

	public float AimScale { get; private set; }

	public GameObject LastPrint { get; private set; }

	public bool LastPrintSnapped { get; private set; }

	public GameObject DebugHeldModel => heldModel;

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
	{
		player = p;
		ui = u;
		world = w;
		state = s;
		groundMask = LayerMask.GetMask("Ground");
	}

	public void PrepareFilmingBridge()
	{
		StartCoroutine(PrepareFilmingBridgeRoutine());
	}

	private IEnumerator PrepareFilmingBridgeRoutine()
	{
		yield return new WaitForSecondsRealtime(0.4f);
		ferry = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
		if (ferry == null)
		{
			yield break;
		}
		DebugClearPrints();
		ferry.DebugResetCrossing();
		if (!ferry.GapEndpoints(out var a, out var b))
		{
			yield break;
		}
		Vector3 vector = b - a;
		vector.y = 0f;
		Vector3 vector2 = a - vector.normalized * 1.1f;
		Vector3 vector3 = vector2;
		bool flag = false;
		for (float num = 0f; num <= 3f; num += 0.25f)
		{
			if (flag)
			{
				break;
			}
			for (int i = 0; i < 16; i++)
			{
				if (flag)
				{
					break;
				}
				Vector3 vector4 = vector3 + new Vector3(Mathf.Cos((float)i * MathF.PI / 8f), 0f, Mathf.Sin((float)i * MathF.PI / 8f)) * num;
				if (world.Walkable(vector4.x, vector4.z))
				{
					vector2 = vector4;
					flag = true;
				}
			}
		}
		vector2.y = world.GroundAt(vector2.x, vector2.z);
		Vector3 vector5 = vector2;
		Debug.Log("FILMING_PRINT stand=" + vector5.ToString() + " safe=" + flag);
		player.transform.position = vector2;
		player.locked = false;
		player.FaceTowards(b);
		OpenViewfinder();
		DebugSelect("ruler");
		float num2 = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
		for (float num3 = 0f; num3 <= 90f; num3 += 90f)
		{
			for (float num4 = 0.35f; num4 <= 1.1f; num4 += 0.05f)
			{
				for (float num5 = 5f; num5 <= 50f; num5 += 1.5f)
				{
					DebugSetView(num2, num5, num4, num3);
					if (aimBridgesFerry)
					{
						DebugSetView(num2 + 0.6f, num5, num4, num3);
						Debug.Log("FILMING_PRINT calibrated");
						yield break;
					}
				}
			}
		}
		Debug.LogWarning("FILMING_PRINT no matching aim");
	}

	public void ToggleViewfinder()
	{
		if (IsViewfinderActive)
		{
			CloseViewfinder();
		}
		else
		{
			OpenViewfinder();
		}
	}

	public void OpenViewfinder()
	{
		if (IsViewfinderActive || player == null || ui == null || player.locked || ui.LetterOpen || ui.Paused || ui.TitleShowing || ui.Cinematic || MenuController.OverlayOpen)
		{
			return;
		}
		cam = Camera.main;
		if (!(cam == null))
		{
			IsViewfinderActive = true;
			openedFrame = Time.frameCount;
			if (ferry == null)
			{
				ferry = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
			}
			player.locked = true;
			player.vel = Vector3.zero;
			chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
			restoreChase = chase != null && chase.enabled;
			if (restoreChase)
			{
				chase.enabled = false;
			}
			viewYaw = player.yaw * 57.29578f;
			viewPitch = 10f;
			SetPlayerVisible(on: false);
			ui.SetCinematic(on: true, "viewfinder");
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
			EnsureUI();
			frame.gameObject.SetActive(value: true);
			itemIndex = FirstUnlocked(itemIndex);
			OverlayRender.Acquire(cam);
			BuildHeld();
			ApplyView();
			ShadowAndChimeSystem.PlayProceduralChime(523.25f, 0.12f);
		}
	}

	public void CloseViewfinder()
	{
		StopAllCoroutines();
		if (IsViewfinderActive)
		{
			IsViewfinderActive = false;
			OverlayRender.Release();
			if (held != null)
			{
				UnityEngine.Object.Destroy(held.gameObject);
			}
			held = null;
			heldModel = null;
			if (frame != null)
			{
				frame.gameObject.SetActive(value: false);
			}
			SetPlayerVisible(on: true);
			if (ui != null)
			{
				ui.SetCinematic(on: false, "viewfinder");
			}
			if (player != null)
			{
				player.locked = false;
			}
			if (restoreChase && chase != null)
			{
				chase.enabled = true;
			}
			restoreChase = false;
		}
	}

	private void SetPlayerVisible(bool on)
	{
		if (on)
		{
			foreach (Renderer hiddenPlayerPart in hiddenPlayerParts)
			{
				if (hiddenPlayerPart != null)
				{
					hiddenPlayerPart.enabled = true;
				}
			}
			hiddenPlayerParts.Clear();
		}
		else
		{
			if (!(player != null))
			{
				return;
			}
			Renderer[] componentsInChildren = player.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (renderer.enabled)
				{
					hiddenPlayerParts.Add(renderer);
					renderer.enabled = false;
				}
			}
		}
	}

	private int FirstUnlocked(int from)
	{
		for (int i = 0; i < Items.Length; i++)
		{
			int num = (from + i) % Items.Length;
			if (Items[num].unlocked(state))
			{
				return num;
			}
		}
		return 0;
	}

	private void BuildHeld()
	{
		if (held != null)
		{
			UnityEngine.Object.Destroy(held.gameObject);
		}
		Item item = Items[itemIndex];
		GameObject gameObject = new GameObject("PrintHeld_" + item.id);
		gameObject.transform.SetParent(cam.transform, worldPositionStays: false);
		held = gameObject.transform;
		GameObject gameObject2 = null;
		if (!string.IsNullOrEmpty(item.lib))
		{
			Lux3DService lux3DService = Lux3DService.Ensure();
			gameObject2 = lux3DService.InstantiateFallback(lux3DService.Find(item.lib), item.hold);
		}
		if (gameObject2 == null && !string.IsNullOrEmpty(item.scene))
		{
			gameObject2 = TideTownStyleUI.CloneSceneLux3D(item.scene, item.hold);
		}
		if (gameObject2 == null)
		{
			gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
		}
		Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		gameObject2.transform.SetParent(held, worldPositionStays: false);
		Bounds bounds = Lux3DModelFit.WorldBounds(gameObject2);
		gameObject2.transform.localPosition = new Vector3(0f, (0f - bounds.size.y) * 0.5f, 0f);
		heldModel = gameObject2;
		Renderer[] componentsInChildren2 = gameObject2.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].shadowCastingMode = ShadowCastingMode.Off;
		}
		OverlayRender.SetLayerRecursive(gameObject, 30);
		UpdateHeldPose();
	}

	private void UpdateHeldPose()
	{
		if (!(held == null))
		{
			held.localPosition = new Vector3(0f, -0.06f, holdDist);
			held.localRotation = Quaternion.Euler(0f, heldYaw, 0f);
			held.localScale = Vector3.one;
		}
	}

	private void ApplyView()
	{
		if (!(cam == null) && !(player == null))
		{
			Vector3 position = player.transform.position + Vector3.up * 0.72f;
			cam.transform.position = position;
			cam.transform.rotation = Quaternion.Euler(viewPitch, viewYaw, 0f);
			player.yaw = viewYaw * (MathF.PI / 180f);
			player.transform.rotation = Quaternion.Euler(0f, viewYaw, 0f);
		}
	}

	private void Update()
	{
		if (!IsViewfinderActive || openedFrame == Time.frameCount)
		{
			return;
		}
		if (cam == null)
		{
			CloseViewfinder();
			return;
		}
		bool mouseButton = Input.GetMouseButton(1);
		float axis = Input.GetAxis("Mouse X");
		float axis2 = Input.GetAxis("Mouse Y");
		if (mouseButton)
		{
			heldYaw += axis * 4f;
		}
		else
		{
			viewYaw += axis * 2.2f;
			viewPitch = Mathf.Clamp(viewPitch - axis2 * 2.2f, -35f, 55f);
		}
		float axis3 = Input.GetAxis("Mouse ScrollWheel");
		if (EasyInputBridge.Held(KeyCode.A))
		{
			viewYaw -= 18f * Time.unscaledDeltaTime;
		}
		if (EasyInputBridge.Held(KeyCode.D))
		{
			viewYaw += 18f * Time.unscaledDeltaTime;
		}
		if (Mathf.Abs(axis3) > 0.001f)
		{
			holdDist = Mathf.Clamp(holdDist - axis3 * 0.5f, 0.35f, 1.1f);
		}
		if (Input.GetKeyDown(KeyCode.Q))
		{
			CycleItem(-1);
		}
		if (Input.GetKeyDown(KeyCode.Z))
		{
			CycleItem(1);
		}
		ApplyView();
		UpdateHeldPose();
		ComputeAim();
		UpdateUI();
		if (Input.GetMouseButtonDown(0) || EasyInputBridge.Down(KeyCode.Space) || EasyInputBridge.Down(KeyCode.E))
		{
			Shutter();
		}
		else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.C))
		{
			CloseViewfinder();
		}
	}

	private void CycleItem(int d)
	{
		for (int i = 1; i <= Items.Length; i++)
		{
			int num = ((itemIndex + d * i) % Items.Length + Items.Length) % Items.Length;
			if (Items[num].unlocked(state))
			{
				itemIndex = num;
				break;
			}
		}
		BuildHeld();
		ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.08f);
	}

	private void ComputeAim()
	{
		AimValid = false;
		AimScale = 0f;
		aimBridgesFerry = false;
		if (heldModel == null)
		{
			return;
		}
		aimCamPos = cam.transform.position;
		Bounds bounds = Lux3DModelFit.WorldBounds(heldModel);
		Vector3 direction = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - aimCamPos;
		float magnitude = direction.magnitude;
		if (magnitude < 0.0001f)
		{
			return;
		}
		direction /= magnitude;
		float num = float.MaxValue;
		if (Physics.Raycast(aimCamPos, direction, out var hitInfo, 60f, groundMask, QueryTriggerInteraction.Ignore))
		{
			num = hitInfo.distance;
		}
		float staticY = WaterLevel.StaticY;
		if (direction.y < -0.0001f && aimCamPos.y > staticY)
		{
			float num2 = (staticY - aimCamPos.y) / direction.y;
			if (num2 > 0f && num2 < num)
			{
				num = num2;
			}
		}
		if (num != float.MaxValue)
		{
			float num3 = num / magnitude;
			float num4 = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * num3;
			AimScale = num3;
			AimValid = num3 >= 1.3f && num4 <= 12f;
			if (AimValid && FerrySocket(out var a, out var b))
			{
				Bounds bounds2 = new Bounds(aimCamPos + (bounds.center - aimCamPos) * num3, bounds.size * num3);
				aimBridgesFerry = CoversFerry(bounds2, a, b);
			}
		}
	}

	public GameObject Shutter()
	{
		if (!IsViewfinderActive)
		{
			return null;
		}
		ComputeAim();
		if (!AimValid || held == null)
		{
			ShadowAndChimeSystem.PlayProceduralChime(196f, 0.15f);
			return null;
		}
		float aimScale = AimScale;
		GameObject gameObject = UnityEngine.Object.Instantiate(held.gameObject);
		gameObject.name = "PerspectivePrint_" + Items[itemIndex].id;
		OverlayRender.SetLayerRecursive(gameObject, 0);
		Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].shadowCastingMode = ShadowCastingMode.On;
		}
		gameObject.transform.SetPositionAndRotation(aimCamPos + (held.position - aimCamPos) * aimScale, held.rotation);
		gameObject.transform.localScale = held.lossyScale * aimScale;
		prints.Add(gameObject);
		LastPrint = gameObject;
		LastPrintSnapped = TrySnapToSocket(gameObject);
		while (prints.Count > 3)
		{
			if (prints[0] != null)
			{
				UnityEngine.Object.Destroy(prints[0]);
			}
			prints.RemoveAt(0);
		}
		flashT = 0.18f;
		ShadowAndChimeSystem.PlayProceduralChime(1318.5f, 0.06f);
		ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
		if (LastPrintSnapped && FilmingStageJumper.ActiveSession)
		{
			CloseViewfinder();
		}
		return gameObject;
	}

	private bool TrySnapToSocket(GameObject go)
	{
		if (!FerrySocket(out var a, out var b) || !CoversFerry(Lux3DModelFit.WorldBounds(go), a, b))
		{
			return false;
		}
		Vector3 vector = b - a;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		GameObject gameObject = new GameObject("PrintBridge");
		int num = LayerMask.NameToLayer("Ground");
		gameObject.layer = ((num >= 0) ? num : 6);
		Vector3 vector2 = (a + b) * 0.5f;
		float num2 = Mathf.Max(WaterLevel.StaticY + 0.08f, Mathf.Min(a.y, b.y));
		gameObject.transform.SetPositionAndRotation(new Vector3(vector2.x, num2, vector2.z), Quaternion.LookRotation(vector / magnitude, Vector3.up));
		go.transform.SetParent(gameObject.transform, worldPositionStays: true);
		go.transform.localRotation = Quaternion.identity;
		Bounds bounds = Lux3DModelFit.WorldBounds(go);
		if (bounds.size.x > bounds.size.z)
		{
			go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
		}
		bounds = Lux3DModelFit.WorldBounds(go);
		float num3 = Mathf.Max(bounds.size.x, bounds.size.z);
		if (num3 < magnitude + 0.6f)
		{
			go.transform.localScale *= (magnitude + 0.6f) / Mathf.Max(0.01f, num3);
		}
		bounds = Lux3DModelFit.WorldBounds(go);
		go.transform.position += new Vector3(vector2.x - bounds.center.x, num2 - bounds.min.y, vector2.z - bounds.center.z);
		BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
		boxCollider.center = new Vector3(0f, 0.06f, 0f);
		boxCollider.size = new Vector3(1f, 0.12f, magnitude + 0.6f);
		Physics.SyncTransforms();
		prints[prints.Count - 1] = gameObject;
		LastPrint = gameObject;
		ferry.BridgeGapWithBoat("print_bridge");
		ShadowAndChimeSystem.PlayProceduralChime(1046.5f, 0.4f);
		return true;
	}

	private bool FerrySocket(out Vector3 a, out Vector3 b)
	{
		a = (b = Vector3.zero);
		if (ferry != null && ferry.GapActive && !ferry.CrossingSolved)
		{
			return ferry.GapEndpoints(out a, out b);
		}
		return false;
	}

	private static bool CoversFerry(Bounds bounds, Vector3 a, Vector3 b)
	{
		bounds.Expand(new Vector3(1.2f, 4f, 1.2f));
		if (bounds.Contains(new Vector3(a.x, bounds.center.y, a.z)))
		{
			return bounds.Contains(new Vector3(b.x, bounds.center.y, b.z));
		}
		return false;
	}

	private void EnsureUI()
	{
		if (frame != null)
		{
			return;
		}
		RectTransform parent = TideTownStyleUI.FindHudCanvasRoot(ui);
		GameObject gameObject = new GameObject("ViewfinderFrame", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		frame = gameObject.GetComponent<RectTransform>();
		frame.anchorMin = Vector2.zero;
		frame.anchorMax = Vector2.one;
		frame.offsetMin = new Vector2(100f, 72f);
		frame.offsetMax = new Vector2(-100f, -72f);
		Vector2 anchorMin;
		for (int i = 0; i < 4; i++)
		{
			Vector2 vector = new Vector2(i % 2, i / 2);
			for (int j = 0; j < 2; j++)
			{
				Image image = TideTownStyleUI.CreatePanel(frame, "Corner", (j == 0) ? 44f : 2f, (j == 0) ? 2f : 44f, TideTownStyleUI.CreamText, rounded: false);
				image.raycastTarget = false;
				RectTransform rectTransform = image.rectTransform;
				Vector2 vector3 = (rectTransform.pivot = vector);
				anchorMin = (rectTransform.anchorMax = vector3);
				rectTransform.anchorMin = anchorMin;
				rectTransform.anchoredPosition = Vector2.zero;
				corners.Add(image);
			}
		}
		Image image2 = TideTownStyleUI.CreatePanel(frame, "CenterDot", 4f, 4f, TideTownStyleUI.CreamText);
		image2.raycastTarget = false;
		corners.Add(image2);
		for (int k = 0; k < bankMarkers.Length; k++)
		{
			Image image3 = TideTownStyleUI.CreatePanel(frame, "FerryBank_" + k, 10f, 10f, TideTownStyleUI.CreamText, rounded: false);
			image3.raycastTarget = false;
			image3.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
			bankMarkers[k] = image3;
		}
		Image image4 = TideTownStyleUI.CreatePanel(frame, "ViewfinderCaption", 410f, 36f, new Color32(74, 48, 28, 232));
		image4.sprite = TideTownStyleUI.RoundedBorderSprite;
		image4.raycastTarget = false;
		Shadow shadow = image4.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color32(26, 16, 8, 90);
		shadow.effectDistance = new Vector2(3f, -5f);
		RectTransform rectTransform2 = image4.rectTransform;
		anchorMin = (rectTransform2.anchorMax = new Vector2(0.5f, 0f));
		rectTransform2.anchorMin = anchorMin;
		rectTransform2.anchoredPosition = new Vector2(0f, 18f);
		Image image5 = TideTownStyleUI.CreatePanel(rectTransform2, "CaptionFrame", 0f, 0f, new Color32(224, 180, 114, 150));
		image5.sprite = TideTownStyleUI.RoundedFrameSprite;
		image5.raycastTarget = false;
		RectTransform rectTransform3 = image5.rectTransform;
		rectTransform3.anchorMin = Vector2.zero;
		rectTransform3.anchorMax = Vector2.one;
		rectTransform3.offsetMin = new Vector2(3f, 3f);
		rectTransform3.offsetMax = new Vector2(-3f, -3f);
		label = TideTownStyleUI.CreateText(rectTransform2, "", 15, TideTownStyleUI.CreamText, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(374f, 30f));
		GameObject gameObject2 = new GameObject("ShutterFlash", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		anchorMin = (component.offsetMax = Vector2.zero);
		component.offsetMin = anchorMin;
		flash = gameObject2.GetComponent<Image>();
		flash.color = new Color(1f, 1f, 1f, 0f);
		flash.raycastTarget = false;
	}

	private void UpdateUI()
	{
		if (frame == null)
		{
			return;
		}
		Color color = (aimBridgesFerry ? new Color(0.45f, 1f, 0.65f) : (AimValid ? ((Color)TideTownStyleUI.CreamText) : new Color(0.86f, 0.77f, 0.67f, 0.78f)));
		foreach (Image corner in corners)
		{
			corner.color = color;
		}
		Vector3 a;
		Vector3 b;
		bool flag = FerrySocket(out a, out b) && Vector3.Distance(player.transform.position, (a + b) * 0.5f) <= 9f;
		SetBankMarker(bankMarkers[0], a, flag, color);
		SetBankMarker(bankMarkers[1], b, flag, color);
		label.text = Items[itemIndex].title + (aimBridgesFerry ? "\u3000可以接上两岸" : (flag ? "\u3000让物件搭住两枚岸标" : (AimValid ? "\u3000空格拓印" : "\u3000把物件底边对向地面")));
	}

	private void SetBankMarker(Image marker, Vector3 bank, bool visible, Color color)
	{
		bank.y = Mathf.Max(bank.y, WaterLevel.StaticY) + 0.1f;
		Vector3 vector = cam.WorldToScreenPoint(bank);
		RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, vector, null, out var localPoint);
		marker.enabled = visible && vector.z > 0f && frame.rect.Contains(localPoint);
		marker.rectTransform.anchoredPosition = localPoint;
		marker.color = color;
	}

	private void LateUpdate()
	{
		if (!(flash == null))
		{
			if (flashT > 0f)
			{
				flashT -= Time.unscaledDeltaTime;
			}
			flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flashT / 0.18f) * 0.75f);
		}
	}

	public void DebugSetView(float yawDeg, float pitchDeg, float hold, float heldYawDeg)
	{
		viewYaw = yawDeg;
		viewPitch = pitchDeg;
		holdDist = hold;
		heldYaw = heldYawDeg;
		ApplyView();
		UpdateHeldPose();
		ComputeAim();
	}

	public void DebugSelect(string id)
	{
		for (int i = 0; i < Items.Length; i++)
		{
			if (Items[i].id == id)
			{
				itemIndex = i;
				if (IsViewfinderActive)
				{
					BuildHeld();
				}
			}
		}
	}

	public Rect DebugScreenRect(GameObject go)
	{
		Camera camera = ((cam != null) ? cam : Camera.main);
		Bounds bounds = Lux3DModelFit.WorldBounds(go);
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MinValue;
		for (int i = 0; i < 8; i++)
		{
			Vector3 position = new Vector3((i % 2 == 0) ? bounds.min.x : bounds.max.x, (i / 2 % 2 == 0) ? bounds.min.y : bounds.max.y, (i / 4 == 0) ? bounds.min.z : bounds.max.z);
			Vector3 vector = camera.WorldToScreenPoint(position);
			num = Mathf.Min(num, vector.x);
			num2 = Mathf.Min(num2, vector.y);
			num3 = Mathf.Max(num3, vector.x);
			num4 = Mathf.Max(num4, vector.y);
		}
		return Rect.MinMaxRect(num, num2, num3, num4);
	}

	public void DebugClearPrints()
	{
		foreach (GameObject print in prints)
		{
			if (print != null)
			{
				UnityEngine.Object.Destroy(print);
			}
		}
		prints.Clear();
		LastPrint = null;
	}
}
