using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleScreen : MonoBehaviour
{
	private enum Phase
	{
		Fly,
		Idle,
		Leaving
	}

	private sealed class ArcPath
	{
		private const int Samples = 600;

		private readonly Vector3[] pts;

		private readonly float[] cum = new float[601];

		public ArcPath(Vector3[] points)
		{
			pts = points;
			Vector3 a = Spline(pts, 0f);
			for (int i = 1; i <= 600; i++)
			{
				Vector3 vector = Spline(pts, (float)i / 600f);
				cum[i] = cum[i - 1] + Vector3.Distance(a, vector);
				a = vector;
			}
		}

		public Vector3 AtFraction(float f)
		{
			float num = Mathf.Clamp01(f) * cum[600];
			int num2 = 0;
			int num3 = 600;
			while (num3 - num2 > 1)
			{
				int num4 = num2 + num3 >> 1;
				if (cum[num4] < num)
				{
					num2 = num4;
				}
				else
				{
					num3 = num4;
				}
			}
			float num5 = (num - cum[num2]) / Mathf.Max(cum[num3] - cum[num2], 1E-06f);
			return Spline(pts, ((float)num2 + num5) / 600f);
		}

		public static Vector3 Spline(Vector3[] p, float x)
		{
			int num = p.Length - 1;
			x = Mathf.Clamp01(x) * (float)num;
			int num2 = Mathf.Min(num - 1, Mathf.FloorToInt(x));
			float t = x - (float)num2;
			return Centripetal((num2 > 0) ? p[num2 - 1] : (p[0] * 2f - p[1]), p3: (num2 + 2 <= num) ? p[num2 + 2] : (p[num] * 2f - p[num - 1]), p1: p[num2], p2: p[num2 + 1], t: t);
		}

		private static Vector3 Centripetal(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
		{
			float num = 0f;
			float num2 = num + Mathf.Sqrt(Mathf.Max(Vector3.Distance(p0, p1), 0.0001f));
			float num3 = num2 + Mathf.Sqrt(Mathf.Max(Vector3.Distance(p1, p2), 0.0001f));
			float num4 = num3 + Mathf.Sqrt(Mathf.Max(Vector3.Distance(p2, p3), 0.0001f));
			float num5 = Mathf.Lerp(num2, num3, t);
			Vector3 vector = (num2 - num5) / (num2 - num) * p0 + (num5 - num) / (num2 - num) * p1;
			Vector3 vector2 = (num3 - num5) / (num3 - num2) * p1 + (num5 - num2) / (num3 - num2) * p2;
			Vector3 vector3 = (num4 - num5) / (num4 - num3) * p2 + (num5 - num3) / (num4 - num3) * p3;
			Vector3 vector4 = (num3 - num5) / (num3 - num) * vector + (num5 - num) / (num3 - num) * vector2;
			Vector3 vector5 = (num4 - num5) / (num4 - num2) * vector2 + (num5 - num2) / (num4 - num2) * vector3;
			return (num3 - num5) / (num3 - num2) * vector4 + (num5 - num2) / (num3 - num2) * vector5;
		}
	}

	private static bool flownThisSession;

	private static readonly string[] StampIds = new string[4] { "bakery", "school", "lighthouse", "ending" };

	private static readonly string[] StampLabels = new string[4] { "烤炉", "小黑", "灯塔", "回家" };

	private static readonly Color32 Ink = new Color32(41, 58, 57, byte.MaxValue);

	private static readonly Color32 InkSoft = new Color32(97, 102, 86, byte.MaxValue);

	private static readonly Color32 Sea = new Color32(41, 97, 112, byte.MaxValue);

	private static readonly Color32 Clay = new Color32(165, 75, 53, byte.MaxValue);

	private static readonly Color32 Cream = new Color32(251, 243, 226, byte.MaxValue);

	private static readonly Color32 CreamBack = new Color32(247, 238, 219, byte.MaxValue);

	private static readonly Color32 Rule = new Color32(201, 180, 146, byte.MaxValue);

	private static readonly Color32 Amber = new Color32(byte.MaxValue, 223, 192, byte.MaxValue);

	private static readonly Vector3 VistaPos = new Vector3(58f, 16f, -30f);

	private static readonly Vector3 VistaLook = new Vector3(30f, 4f, 19f);

	private const float VistaFov = 48f;

	private static readonly Vector3 Lamp = new Vector3(33.59f, 11.33f, 9.62f);

	private static readonly Vector3[] FlyA = new Vector3[6]
	{
		new Vector3(128f, 5f, 34f),
		new Vector3(80f, 7.5f, 20f),
		new Vector3(50f, 12f, 1f),
		new Vector3(24f, 15.5f, -6f),
		new Vector3(4f, 17f, -2f),
		new Vector3(-8f, 17.5f, 4f)
	};

	private static readonly Vector3[] FlyB = new Vector3[4]
	{
		new Vector3(-8f, 17.5f, 4f),
		new Vector3(14f, 19f, -12f),
		new Vector3(40f, 18f, -26f),
		VistaPos
	};

	private static readonly Vector3[] LookA = new Vector3[5]
	{
		Lamp,
		new Vector3(33f, 10.5f, 9.5f),
		new Vector3(16f, 6f, 6f),
		new Vector3(-10f, 5f, 9f),
		new Vector3(-28f, 5f, 14f)
	};

	private static readonly Vector3[] LookB = new Vector3[3]
	{
		new Vector3(-28f, 5f, 14f),
		new Vector3(0f, 5f, 16f),
		VistaLook
	};

	private const float FlyASeconds = 8f;

	private const float FlyBSeconds = 5f;

	private const float FlySeconds = 13f;

	private static ArcPath pathA;

	private static ArcPath pathB;

	private GameBootstrap boot;

	private EggboyUI ui;

	private EggboyPlayer player;

	private ChaseCamera chase;

	private Camera cam;

	private bool hasProgress;

	private Action onNewJourney;

	private Phase phase;

	private float phaseT;

	private float idleT;

	private bool flyStarted;

	private bool warming;

	private bool flyLogOnly;

	private int stableFrames;

	private float holdT;

	private StreamWriter flyLog;

	private Vector3 camFromPos;

	private Quaternion camFromRot;

	private float camFromFov;

	private bool blendingToVista;

	private Transform cardRoot;

	private Transform cardPivot;

	private CanvasGroup frontGroup;

	private CanvasGroup backGroup;

	private bool popped;

	private float popT;

	private Quaternion userRot = Quaternion.identity;

	private Vector2 angVel;

	private bool mouseDown;

	private bool dragging;

	private Vector2 downPos;

	private Vector2 lastPos;

	private float releasedAt = -10f;

	private int dragEndFrame = -10;

	private float appear;

	private RectTransform menuPanel;

	private RectTransform settingsPanel;

	private RectTransform confirmPanel;

	private CanvasGroup fade;

	private Text hint;

	private static readonly Vector3 IdleLocal = new Vector3(0.44f, -0.1f, 1.38f);

	private static readonly Quaternion IdleRot = Quaternion.Euler(8f, -26f, -5f);

	private static readonly Vector3 PopLocal = new Vector3(0f, 0.015f, 1.06f);

	private static readonly Quaternion Back = Quaternion.Euler(0f, 180f, 0f);

	public static TitleScreen Current { get; private set; }

	private bool BlockedByDrag
	{
		get
		{
			if (Time.frameCount - dragEndFrame > 1)
			{
				return dragging;
			}
			return true;
		}
	}

	public static TitleScreen Show(GameBootstrap boot, bool hasProgress, Action onNewJourney)
	{
		TitleScreen titleScreen = boot.gameObject.AddComponent<TitleScreen>();
		titleScreen.Init(boot, hasProgress, onNewJourney);
		return titleScreen;
	}

	private void Init(GameBootstrap b, bool progress, Action newJourney)
	{
		Current = this;
		boot = b;
		ui = b.ui;
		player = b.player;
		chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		cam = Camera.main;
		hasProgress = progress;
		onNewJourney = newJourney;
		if (chase != null)
		{
			chase.enabled = false;
		}
		if (player != null)
		{
			player.locked = true;
		}
		ui?.BeginTitle();
		EnsureEventSystem();
		BuildOverlay();
		BuildCard();
		bool flag = Array.IndexOf(Environment.GetCommandLineArgs(), "-titleshot") >= 0;
		flyLogOnly = !flag && Array.IndexOf(Environment.GetCommandLineArgs(), "-flylog") >= 0;
		if (flyLogOnly)
		{
			Application.runInBackground = true;
		}
		bool num = !flownThisSession && (!BootLoader.IsAutomatedLaunch() || flag || flyLogOnly);
		if (flag)
		{
			StartCoroutine(ShotSequence());
		}
		flownThisSession = true;
		if (num)
		{
			phase = Phase.Fly;
			phaseT = 0f;
			appear = 0f;
			SetHint("点一下跳过开场");
			if (pathA == null)
			{
				pathA = new ArcPath(FlyA);
				pathB = new ArcPath(FlyB);
			}
			if (BootLoader.Opaque)
			{
				StartCoroutine(WarmFlightPath());
			}
			if (Array.IndexOf(Environment.GetCommandLineArgs(), "-flylog") >= 0)
			{
				OpenFlyLog();
			}
		}
		else
		{
			EnterIdle(skipped: false);
			appear = 1f;
		}
		PlaceCamera();
	}

	private IEnumerator WarmFlightPath()
	{
		warming = true;
		BootLoader.WarmupBusy = true;
		int done = 0;
		float began = Time.realtimeSinceStartup;
		try
		{
			for (int i = 0; i <= 40; i++)
			{
				if (!BootLoader.Opaque)
				{
					break;
				}
				PoseAt(13f * (float)i / 40f, out var pos, out var rot, out var fov);
				cam.transform.SetPositionAndRotation(pos, rot);
				cam.fieldOfView = fov;
				done++;
				yield return null;
			}
		}
		finally
		{
			TitleScreen titleScreen = this;
			UnityEngine.Debug.Log($"TITLE_WARM samples={done} seconds={Time.realtimeSinceStartup - began:F2}");
			titleScreen.warming = false;
			BootLoader.WarmupBusy = false;
			titleScreen.PlaceCamera();
		}
	}

	private IEnumerator QuitSoon()
	{
		yield return new WaitForSecondsRealtime(1.5f);
		Application.Quit();
	}

	private void OpenFlyLog()
	{
		string text = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(text);
		flyLog = new StreamWriter(Path.Combine(text, "flylog.csv"));
		flyLog.WriteLine("frame,raw_dt,phase_t,x,y,z,yaw,pitch,roll,fov,gc0,heap_mb");
	}

	private void OnDestroy()
	{
		if (Current == this)
		{
			Current = null;
		}
		if (flyLog != null)
		{
			flyLog.Dispose();
			flyLog = null;
		}
		if (warming)
		{
			BootLoader.WarmupBusy = false;
		}
		if (cardRoot != null)
		{
			UnityEngine.Object.Destroy(cardRoot.gameObject);
		}
		if (fade != null)
		{
			UnityEngine.Object.Destroy(fade.transform.root.gameObject);
		}
	}

	private void Update()
	{
		if (cam == null)
		{
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		float num = Mathf.Min(unscaledDeltaTime, 1f / 30f);
		if (phase == Phase.Fly && !flyStarted)
		{
			holdT += unscaledDeltaTime;
			stableFrames = ((unscaledDeltaTime < 0.025f) ? (stableFrames + 1) : 0);
			if (!warming && !BootLoader.Opaque && (stableFrames >= 6 || holdT > 4f))
			{
				flyStarted = true;
				UnityEngine.Debug.Log($"TITLE_FLY start after hold {holdT:F2}s");
			}
		}
		else
		{
			phaseT += num;
		}
		switch (phase)
		{
		case Phase.Fly:
			if (phaseT >= 13f || (!warming && AnyPress()))
			{
				EnterIdle(phaseT < 13f);
			}
			break;
		case Phase.Idle:
			idleT += num;
			appear = Mathf.MoveTowards(appear, 1f, num / 0.9f);
			HandleCardInput(num);
			break;
		}
		PlaceCamera();
		PlaceCard(num);
	}

	private bool AnyPress()
	{
		if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Return) && !EasyInputBridge.Down(KeyCode.Space) && !EasyInputBridge.Down(KeyCode.E) && !EasyInputBridge.Down(KeyCode.Escape))
		{
			return Input.GetKeyDown(KeyCode.KeypadEnter);
		}
		return true;
	}

	private void EnterIdle(bool skipped)
	{
		phase = Phase.Idle;
		idleT = 0f;
		if (skipped)
		{
			blendingToVista = true;
			camFromPos = cam.transform.position;
			camFromRot = cam.transform.rotation;
			camFromFov = cam.fieldOfView;
			phaseT = 0f;
		}
		else
		{
			blendingToVista = false;
		}
		SetHint("点一下明信片");
	}

	private void PlaceCamera()
	{
		if (phase == Phase.Leaving || warming)
		{
			return;
		}
		if (phase == Phase.Fly)
		{
			PoseAt(phaseT, out var pos, out var rot, out var fov);
			cam.transform.SetPositionAndRotation(pos, rot);
			cam.fieldOfView = fov;
			if (flyLog != null)
			{
				Vector3 eulerAngles = rot.eulerAngles;
				flyLog.WriteLine($"{Time.frameCount},{Time.unscaledDeltaTime:F4},{phaseT:F4},{pos.x:F3},{pos.y:F3},{pos.z:F3},{eulerAngles.y:F3},{eulerAngles.x:F3},{eulerAngles.z:F3},{fov:F2},{GC.CollectionCount(0)},{GC.GetTotalMemory(forceFullCollection: false) / 1048576}");
			}
			return;
		}
		if (flyLog != null)
		{
			flyLog.Dispose();
			flyLog = null;
			if (flyLogOnly)
			{
				StartCoroutine(QuitSoon());
			}
		}
		Vector3 normalized = (VistaLook - VistaPos).normalized;
		Vector3 normalized2 = Vector3.Cross(Vector3.up, normalized).normalized;
		float num = Mathf.SmoothStep(0f, 1f, idleT / 4f);
		Vector3 vector = (normalized2 * (Mathf.Sin(idleT * 0.06f) * 2.4f) + Vector3.up * (Mathf.Sin(idleT * 0.09f) * 0.6f)) * num;
		Vector3 vector2 = normalized2 * (Mathf.Sin(idleT * 0.05f) * 1.2f) * num;
		Vector3 vector3 = VistaPos + vector;
		Quaternion quaternion = Quaternion.LookRotation(VistaLook + vector2 - vector3, Vector3.up);
		if (blendingToVista)
		{
			float num2 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseT / 0.9f));
			vector3 = Vector3.Lerp(camFromPos, vector3, num2);
			quaternion = Quaternion.Slerp(camFromRot, quaternion, num2);
			cam.fieldOfView = Mathf.Lerp(camFromFov, 48f, num2);
			if (num2 >= 1f)
			{
				blendingToVista = false;
			}
		}
		else
		{
			cam.fieldOfView = 48f;
		}
		cam.transform.SetPositionAndRotation(vector3, quaternion);
	}

	private static float EaseOutHermite(float u)
	{
		u = Mathf.Clamp01(u);
		return u + u * u - u * u * u;
	}

	private static float Smooth(float u)
	{
		u = Mathf.Clamp01(u);
		return u * u * (3f - 2f * u);
	}

	private static void RawPose(float t, out Vector3 pos, out Vector3 look, out float fov)
	{
		if (t < 8f)
		{
			float u = t / 8f;
			float num = EaseOutHermite(u);
			pos = pathA.AtFraction(num);
			look = ArcPath.Spline(LookA, num);
			fov = Mathf.Lerp(56f, 52f, Smooth(u));
		}
		else
		{
			float num2 = Smooth((t - 8f) / 5f);
			pos = pathB.AtFraction(num2);
			look = ArcPath.Spline(LookB, num2);
			fov = Mathf.Lerp(52f, 48f, num2);
		}
	}

	private static float YawAt(float t)
	{
		RawPose(Mathf.Clamp(t, 0f, 13f), out var pos, out var look, out var _);
		Vector3 vector = look - pos;
		return Mathf.Atan2(vector.x, vector.z) * 57.29578f;
	}

	private static void PoseAt(float t, out Vector3 pos, out Quaternion rot, out float fov)
	{
		RawPose(t, out pos, out var look, out fov);
		float z = Mathf.Clamp((0f - Mathf.DeltaAngle(YawAt(t - 0.1f), YawAt(t + 0.1f)) / 0.2f) * 0.22f, -4f, 4f);
		rot = Quaternion.LookRotation(look - pos, Vector3.up) * Quaternion.Euler(0f, 0f, z);
	}

	private void HandleCardInput(float dt)
	{
		if (EasyInputBridge.Down(KeyCode.Escape) && popped)
		{
			SetPopped(on: false);
			return;
		}
		if (!popped && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || EasyInputBridge.Down(KeyCode.Space) || EasyInputBridge.Down(KeyCode.E)))
		{
			SetPopped(on: true);
			return;
		}
		Vector2 vector = Input.mousePosition;
		if (Input.GetMouseButtonDown(0))
		{
			mouseDown = true;
			dragging = false;
			downPos = (lastPos = vector);
		}
		if (mouseDown && Input.GetMouseButton(0))
		{
			if (!dragging && popped && (vector - downPos).magnitude > 7f)
			{
				dragging = true;
			}
			if (dragging)
			{
				Vector2 vector2 = vector - lastPos;
				userRot = Quaternion.AngleAxis((0f - vector2.x) * 0.42f, Vector3.up) * Quaternion.AngleAxis(vector2.y * 0.42f, Vector3.right) * userRot;
				angVel = Vector2.Lerp(angVel, vector2 / Mathf.Max(dt, 0.001f), 0.5f);
			}
			lastPos = vector;
		}
		if (mouseDown && Input.GetMouseButtonUp(0))
		{
			mouseDown = false;
			if (dragging)
			{
				dragging = false;
				releasedAt = Time.unscaledTime;
				dragEndFrame = Time.frameCount;
			}
			else if (!popped)
			{
				SetPopped(on: true);
			}
			else if (!PointerOnCard())
			{
				SetPopped(on: false);
			}
		}
		if (popped && !dragging)
		{
			if (angVel.sqrMagnitude > 1f)
			{
				userRot = Quaternion.AngleAxis((0f - angVel.x) * 0.42f * dt, Vector3.up) * Quaternion.AngleAxis(angVel.y * 0.42f * dt, Vector3.right) * userRot;
				angVel *= Mathf.Exp((0f - dt) * 4f);
			}
			if (Time.unscaledTime - releasedAt > 1.1f)
			{
				Quaternion b = (((userRot * Vector3.forward).z < 0f) ? Back : Quaternion.identity);
				userRot = Quaternion.Slerp(userRot, b, 1f - Mathf.Exp((0f - dt) * 5f));
			}
		}
	}

	private bool PointerOnCard()
	{
		if (EventSystem.current == null)
		{
			return false;
		}
		PointerEventData eventData = new PointerEventData(EventSystem.current)
		{
			position = Input.mousePosition
		};
		List<RaycastResult> list = new List<RaycastResult>();
		EventSystem.current.RaycastAll(eventData, list);
		foreach (RaycastResult item in list)
		{
			if (item.gameObject.transform.IsChildOf(cardRoot))
			{
				return true;
			}
		}
		return false;
	}

	private void SetPopped(bool on)
	{
		if (popped != on)
		{
			popped = on;
			if (on)
			{
				userRot = IdleRot;
				releasedAt = Time.unscaledTime - 0.4f;
				angVel = Vector2.zero;
				StartCoroutine(FlipTo(Back, 0.65f));
				SetHint("按住拖动可以翻转明信片\u3000·\u3000Esc 收起");
			}
			else
			{
				ShowMenu();
				SetHint("点一下明信片");
			}
		}
	}

	private IEnumerator FlipTo(Quaternion target, float seconds)
	{
		Quaternion from = userRot;
		for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
		{
			if (dragging)
			{
				yield break;
			}
			float t2 = EaseOutBack(t / seconds);
			userRot = Quaternion.SlerpUnclamped(from, target, t2);
			yield return null;
		}
		userRot = target;
	}

	private static float EaseOutBack(float x)
	{
		x = Mathf.Clamp01(x);
		return 1f + 2.4f * Mathf.Pow(x - 1f, 3f) + 1.4f * Mathf.Pow(x - 1f, 2f);
	}

	private void PlaceCard(float dt)
	{
		if (!(cardRoot == null))
		{
			popT = Mathf.MoveTowards(popT, popped ? 1f : 0f, dt / 0.5f);
			float num = (popped ? EaseOutBack(popT) : Mathf.SmoothStep(0f, 1f, popT));
			float unscaledTime = Time.unscaledTime;
			Vector3 vector = new Vector3(0f, Mathf.Sin(unscaledTime * 1.3f) * 0.012f, 0f);
			Quaternion quaternion = Quaternion.Euler(Mathf.Sin(unscaledTime * 0.9f) * 2f, Mathf.Sin(unscaledTime * 0.7f) * 3f, Mathf.Sin(unscaledTime * 1.1f) * 1.2f);
			bool flag = !popped && phase == Phase.Idle && PointerOnCard();
			Vector3 a = IdleLocal + vector + (flag ? new Vector3(0f, 0.02f, -0.05f) : Vector3.zero);
			float num2 = 1f - Mathf.SmoothStep(0f, 1f, appear);
			a += new Vector3(0f, -0.6f * num2, 0f);
			cardRoot.localPosition = Vector3.LerpUnclamped(a, PopLocal, num);
			Quaternion quaternion2 = IdleRot * quaternion;
			cardPivot.localRotation = ((popped || popT > 0f) ? Quaternion.Slerp(quaternion2, userRot, Mathf.Clamp01(num)) : quaternion2);
			float num3 = Mathf.SmoothStep(0f, 1f, appear);
			CanvasGroup canvasGroup = frontGroup;
			float alpha = (backGroup.alpha = num3);
			canvasGroup.alpha = alpha;
			CanvasGroup canvasGroup2 = frontGroup;
			bool blocksRaycasts = (backGroup.blocksRaycasts = phase == Phase.Idle);
			canvasGroup2.blocksRaycasts = blocksRaycasts;
		}
	}

	private void Act(Action a)
	{
		if (!BlockedByDrag && phase != Phase.Leaving)
		{
			a();
		}
	}

	private void OnPrimary()
	{
		if (hasProgress)
		{
			StartCoroutine(LeaveToGame(newJourney: false));
		}
		else
		{
			StartCoroutine(LeaveToGame(newJourney: true));
		}
	}

	private void ShowMenu()
	{
		SetPanel(menuPanel);
	}

	private void ShowSettings()
	{
		SetPanel(settingsPanel);
	}

	private void ShowConfirm()
	{
		SetPanel(confirmPanel);
	}

	private void SetPanel(RectTransform p)
	{
		if (menuPanel != null)
		{
			menuPanel.gameObject.SetActive(p == menuPanel);
		}
		if (settingsPanel != null)
		{
			settingsPanel.gameObject.SetActive(p == settingsPanel);
		}
		if (confirmPanel != null)
		{
			confirmPanel.gameObject.SetActive(p == confirmPanel);
		}
	}

	private void RestartFresh()
	{
		if (boot != null && boot.store != null)
		{
			boot.store.Clear();
		}
		PlayerPrefs.Save();
		if (!Application.isEditor && Application.platform == RuntimePlatform.WindowsPlayer)
		{
			try
			{
				Process.Start(Environment.GetCommandLineArgs()[0]);
				Application.Quit();
				return;
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogWarning("TITLE restart failed: " + ex.Message);
			}
		}
		SceneManager.LoadScene("TideTown_Main");
	}

	private IEnumerator LeaveToGame(bool newJourney)
	{
		phase = Phase.Leaving;
		SetHint("");
		Vector3 fromLocal = cardRoot.localPosition;
		Quaternion fromRot = cardPivot.localRotation;
		for (float t2 = 0f; t2 < 0.7f; t2 += Time.unscaledDeltaTime)
		{
			float num = Mathf.SmoothStep(0f, 1f, t2 / 0.7f);
			cardRoot.localPosition = Vector3.Lerp(fromLocal, new Vector3(0.9f, -0.9f, 1.4f), num * num);
			cardPivot.localRotation = Quaternion.Slerp(fromRot, Quaternion.Euler(20f, 10f, -25f), num);
			CanvasGroup canvasGroup = frontGroup;
			float alpha = (backGroup.alpha = 1f - num);
			canvasGroup.alpha = alpha;
			yield return null;
		}
		cardRoot.gameObject.SetActive(value: false);
		if (newJourney)
		{
			yield return Fade(0f, 1f, 0.45f);
			ui?.EndTitle();
			onNewJourney?.Invoke();
			yield return null;
			yield return Fade(1f, 0f, 0.7f);
		}
		else
		{
			ChasePose(out var toPos, out var toRot);
			Vector3 p0 = cam.transform.position;
			Quaternion r0 = cam.transform.rotation;
			float t2 = cam.fieldOfView;
			Vector3 mid = Vector3.Lerp(p0, toPos, 0.5f) + Vector3.up * 8f;
			for (float t = 0f; t < 2.2f; t += Time.unscaledDeltaTime)
			{
				float t4 = Mathf.SmoothStep(0f, 1f, t / 2.2f);
				Vector3 a = Vector3.Lerp(p0, mid, t4);
				Vector3 b = Vector3.Lerp(mid, toPos, t4);
				cam.transform.SetPositionAndRotation(Vector3.Lerp(a, b, t4), Quaternion.Slerp(r0, toRot, Mathf.SmoothStep(0f, 1f, t4)));
				cam.fieldOfView = Mathf.Lerp(t2, 44f, t4);
				yield return null;
			}
			if (chase != null && player != null)
			{
				chase.SnapBehind(player.transform.position, player.yaw);
				chase.enabled = true;
			}
			if (player != null)
			{
				player.locked = false;
			}
			ui?.EndTitle();
		}
		UnityEngine.Object.Destroy(this);
	}

	private void ChasePose(out Vector3 toPos, out Quaternion toRot)
	{
		float f = ((player != null) ? player.yaw : 0f);
		Vector3 vector = new Vector3(Mathf.Cos(f), 0f, 0f - Mathf.Sin(f));
		Vector3 vector2 = new Vector3((0f - Mathf.Sin(f)) * Mathf.Cos(0.2443461f), Mathf.Sin(0.2443461f), (0f - Mathf.Cos(f)) * Mathf.Cos(0.2443461f));
		Vector3 vector3 = ((player != null) ? player.transform.position : Vector3.zero) + Vector3.up * 0.95f + vector * 0.35f;
		toPos = vector3 + vector2 * 3.6f;
		toRot = Quaternion.LookRotation(new Vector3(vector3.x, vector3.y - 0.1f, vector3.z) - toPos, Vector3.up);
	}

	private IEnumerator Fade(float from, float to, float seconds)
	{
		for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
		{
			fade.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
			yield return null;
		}
		fade.alpha = to;
	}

	private void SetHint(string s)
	{
		if (!(hint == null))
		{
			hint.text = s;
			hint.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(s));
		}
	}

	private IEnumerator ShotSequence()
	{
		Application.runInBackground = true;
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		while (phase == Phase.Fly && !flyStarted)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(3f);
		yield return Shot("fly1");
		yield return new WaitForSecondsRealtime(3f);
		yield return Shot("fly2");
		yield return new WaitForSecondsRealtime(3.5f);
		yield return Shot("fly3");
		while (phase == Phase.Fly)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(2.5f);
		yield return Shot("idle");
		SetPopped(on: true);
		yield return new WaitForSecondsRealtime(1.6f);
		yield return Shot("back");
		userRot = Quaternion.Euler(-12f, 130f, 6f);
		releasedAt = Time.unscaledTime + 10f;
		yield return new WaitForSecondsRealtime(0.3f);
		yield return Shot("turn");
		userRot = Quaternion.identity;
		yield return new WaitForSecondsRealtime(0.3f);
		yield return Shot("front");
		userRot = Back;
		releasedAt = Time.unscaledTime;
		ShowSettings();
		yield return new WaitForSecondsRealtime(0.5f);
		yield return Shot("settings");
		ShowConfirm();
		yield return new WaitForSecondsRealtime(0.3f);
		yield return Shot("confirm");
		ShowMenu();
		SkyDimmer skyDimmer = UnityEngine.Object.FindObjectOfType<SkyDimmer>();
		BeaconLight beaconLight = UnityEngine.Object.FindObjectOfType<BeaconLight>();
		if (skyDimmer != null)
		{
			skyDimmer.SetDimmedInstant();
		}
		if (beaconLight != null)
		{
			beaconLight.TurnOnInstant();
		}
		SetPopped(on: false);
		yield return new WaitForSecondsRealtime(1.5f);
		yield return Shot("night");
		Application.Quit();
		IEnumerator Shot(string n)
		{
			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "title_" + n + ".png"));
			yield return new WaitForSecondsRealtime(0.25f);
		}
	}

	private static void EnsureEventSystem()
	{
		if (!(EventSystem.current != null))
		{
			UnityEngine.Object.DontDestroyOnLoad(new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)));
		}
	}

	private void BuildOverlay()
	{
		GameObject obj = new GameObject("TitleOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
		Canvas component = obj.GetComponent<Canvas>();
		component.renderMode = RenderMode.ScreenSpaceOverlay;
		component.sortingOrder = 30000;
		CanvasScaler component2 = obj.GetComponent<CanvasScaler>();
		component2.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		component2.referenceResolution = new Vector2(1920f, 1080f);
		component2.matchWidthOrHeight = 0.5f;
		RectTransform parent = (RectTransform)obj.transform;
		Image image = Img(parent, "HintPill", TideTownStyleUI.PillSprite, new Color32(251, 243, 226, 225));
		RectTransform rectTransform = image.rectTransform;
		RectTransform rectTransform2 = image.rectTransform;
		Vector2 vector2 = (image.rectTransform.pivot = new Vector2(0.5f, 0f));
		Vector2 anchorMin = (rectTransform2.anchorMax = vector2);
		rectTransform.anchorMin = anchorMin;
		image.rectTransform.anchoredPosition = new Vector2(0f, 46f);
		image.rectTransform.sizeDelta = new Vector2(560f, 50f);
		hint = Txt(image.rectTransform, "", 22, Ink, Vector2.zero, new Vector2(540f, 44f), bold: false, TextAnchor.MiddleCenter);
		Image image2 = Img(parent, "Fade", null, new Color32(246, 234, 210, byte.MaxValue));
		image2.rectTransform.anchorMin = Vector2.zero;
		image2.rectTransform.anchorMax = Vector2.one;
		RectTransform rectTransform3 = image2.rectTransform;
		anchorMin = (image2.rectTransform.offsetMax = Vector2.zero);
		rectTransform3.offsetMin = anchorMin;
		fade = image2.gameObject.AddComponent<CanvasGroup>();
		fade.alpha = 0f;
		fade.blocksRaycasts = false;
	}

	private void BuildCard()
	{
		cardRoot = new GameObject("TitlePostcard").transform;
		cardRoot.SetParent(cam.transform, worldPositionStays: false);
		cardRoot.localPosition = IdleLocal;
		cardPivot = new GameObject("Pivot").transform;
		cardPivot.SetParent(cardRoot, worldPositionStays: false);
		RectTransform rectTransform = Face("Front", back: false);
		frontGroup = rectTransform.GetComponent<CanvasGroup>();
		BuildFront(rectTransform);
		RectTransform rectTransform2 = Face("Back", back: true);
		backGroup = rectTransform2.GetComponent<CanvasGroup>();
		BuildBack(rectTransform2);
		ShowMenu();
	}

	private RectTransform Face(string name, bool back)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
		gameObject.layer = cam.gameObject.layer;
		RectTransform obj = (RectTransform)gameObject.transform;
		obj.SetParent(cardPivot, worldPositionStays: false);
		obj.sizeDelta = new Vector2(900f, 600f);
		obj.localScale = Vector3.one * 0.001f;
		obj.localPosition = new Vector3(0f, 0f, back ? 0.0012f : (-0.0012f));
		obj.localRotation = (back ? Back : Quaternion.identity);
		Canvas component = gameObject.GetComponent<Canvas>();
		component.renderMode = RenderMode.WorldSpace;
		component.worldCamera = cam;
		gameObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
		gameObject.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = true;
		return obj;
	}

	private void BuildFront(RectTransform f)
	{
		Image image = Img(f, "Paper", TideTownStyleUI.PaperSprite, Cream);
		Stretch(image.rectTransform, 0f);
		image.raycastTarget = true;
		Stretch(Img(f, "Frame", TideTownStyleUI.RoundedFrameSprite, new Color32(165, 75, 53, 120)).rectTransform, 16f);
		Image image2 = Img(f, "ArtMount", TideTownStyleUI.PaperSprite, new Color32(byte.MaxValue, 251, 242, byte.MaxValue));
		Place(image2.rectTransform, new Vector2(-150f, 70f), new Vector2(540f, 370f));
		image2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
		Image image3 = Img(image2.rectTransform, "Art", PostalArtwork.Postcard, Color.white);
		image3.preserveAspect = true;
		Place(image3.rectTransform, Vector2.zero, new Vector2(510f, 340f));
		Image image4 = Img(image2.rectTransform, "Tape", TideTownStyleUI.PaperSprite, new Color32(163, 206, 208, 225));
		Place(image4.rectTransform, new Vector2(-170f, 178f), new Vector2(130f, 28f));
		image4.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
		Txt(f, "记忆邮局", 22, Clay, new Vector2(-260f, -160f), new Vector2(360f, 30f), bold: true, TextAnchor.MiddleLeft);
		Txt(f, "潮汐镇", 70, Sea, new Vector2(-260f, -212f), new Vector2(360f, 84f), bold: true, TextAnchor.MiddleLeft);
		Txt(f, "EGGBOY · STILL DELIVERING", 16, InkSoft, new Vector2(-240f, -262f), new Vector2(400f, 24f), bold: true, TextAnchor.MiddleLeft);
		GameState gameState = ((boot != null) ? boot.state : null);
		int num = 0;
		Txt(f, "邮戳", 22, Ink, new Vector2(282f, 222f), new Vector2(260f, 30f), bold: true, TextAnchor.MiddleCenter);
		for (int i = 0; i < 4; i++)
		{
			float x = 222f + (float)(i % 2) * 124f;
			float num2 = 120f - (float)(i / 2) * 156f;
			bool flag = gameState != null && gameState.stamps != null && gameState.stamps.Contains(StampIds[i]);
			if (flag)
			{
				num++;
			}
			Place(Img(f, "Slot" + i, TideTownStyleUI.RoundedBorderSprite, flag ? new Color32(0, 0, 0, 0) : new Color32(97, 102, 86, 110)).rectTransform, new Vector2(x, num2), new Vector2(108f, 128f));
			if (flag)
			{
				Image image5 = Img(f, "Stamp" + i, PostalArtwork.Stamp(StampIds[i]), Color.white);
				image5.preserveAspect = true;
				Place(image5.rectTransform, new Vector2(x, num2), new Vector2(112f, 132f));
				image5.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0) ? (-4f) : 3f);
			}
			Txt(f, StampLabels[i], 16, flag ? Ink : InkSoft, new Vector2(x, num2 - 80f), new Vector2(110f, 22f), bold: false, TextAnchor.MiddleCenter);
		}
		Txt(f, "已集 " + num + " / 4", 18, Sea, new Vector2(284f, -206f), new Vector2(240f, 26f), bold: true, TextAnchor.MiddleCenter);
		Image image6 = Img(f, "Postmark", TideTownStyleUI.PostmarkRingSprite, new Color32(165, 75, 53, 150));
		Place(image6.rectTransform, new Vector2(170f, 196f), new Vector2(96f, 96f));
		image6.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -16f);
	}

	private void BuildBack(RectTransform b)
	{
		Image image = Img(b, "Paper", TideTownStyleUI.PaperSprite, CreamBack);
		Stretch(image.rectTransform, 0f);
		image.raycastTarget = true;
		Place(Img(b, "Divider", null, Rule).rectTransform, new Vector2(70f, -10f), new Vector2(2f, 470f));
		Txt(b, "POST CARD · 明信片", 18, InkSoft, new Vector2(0f, 262f), new Vector2(600f, 26f), bold: true, TextAnchor.MiddleCenter);
		Place(Img(b, "StampBox", TideTownStyleUI.RoundedBorderSprite, new Color32(97, 102, 86, 120)).rectTransform, new Vector2(345f, 170f), new Vector2(104f, 124f));
		GameState gameState = ((boot != null) ? boot.state : null);
		string text = null;
		if (gameState != null && gameState.stamps != null)
		{
			for (int num = StampIds.Length - 1; num >= 0; num--)
			{
				if (gameState.stamps.Contains(StampIds[num]))
				{
					text = StampIds[num];
					break;
				}
			}
		}
		if (text != null)
		{
			Image image2 = Img(b, "Stamp", PostalArtwork.Stamp(text), Color.white);
			image2.preserveAspect = true;
			Place(image2.rectTransform, new Vector2(345f, 170f), new Vector2(100f, 120f));
		}
		else
		{
			Txt(b, "贴邮票处", 16, InkSoft, new Vector2(345f, 170f), new Vector2(100f, 24f), bold: false, TextAnchor.MiddleCenter);
		}
		Image image3 = Img(b, "Postmark", TideTownStyleUI.PostmarkRingSprite, new Color32(165, 75, 53, 140));
		Place(image3.rectTransform, new Vector2(250f, 116f), new Vector2(104f, 104f));
		image3.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
		string[] array = new string[3] { "寄往：潮汐镇 · 山口邮局", "收件人：正在读这张明信片的你", "邮差：EGGBOY" };
		for (int i = 0; i < array.Length; i++)
		{
			float num2 = 30f - (float)i * 64f;
			Txt(b, array[i], 19, Ink, new Vector2(255f, num2 + 14f), new Vector2(330f, 28f), bold: false, TextAnchor.MiddleLeft);
			Place(Img(b, "Rule" + i, null, Rule).rectTransform, new Vector2(255f, num2 - 6f), new Vector2(330f, 2f));
		}
		menuPanel = Panel(b, "Menu");
		Txt(menuPanel, "亲爱的邮差：", 26, Ink, new Vector2(-190f, 200f), new Vector2(420f, 36f), bold: true, TextAnchor.MiddleLeft);
		string text2 = ((ui != null) ? ui.GoalText : "");
		if (!string.IsNullOrEmpty(text2))
		{
			text2 = text2.Replace("目标：", "").Replace('\n', ' ');
		}
		MenuButton(menuPanel, hasProgress ? "继续旅程" : "启程派件", (!hasProgress) ? "第一封信在山口邮筒里等你" : (string.IsNullOrEmpty(text2) ? "接着上次的路走" : ("下一站：" + text2)), new Vector2(-190f, 116f), primary: true, delegate
		{
			Act(OnPrimary);
		}).name = "StartButton";
		float num3 = 24f;
		if (hasProgress)
		{
			MenuButton(menuPanel, "新的旅程", "从第一封信重新开始", new Vector2(-190f, num3), primary: false, delegate
			{
				Act(ShowConfirm);
			}).name = "NewJourneyButton";
			num3 -= 84f;
		}
		MenuButton(menuPanel, "邮差设置", "音量 · 视角灵敏度", new Vector2(-190f, num3), primary: false, delegate
		{
			Act(ShowSettings);
		}).name = "SettingsButton";
		num3 -= 84f;
		MenuButton(menuPanel, "离开小镇", "下次再来送信", new Vector2(-190f, num3), primary: false, delegate
		{
			Act(Application.Quit);
		}).name = "QuitButton";
		Txt(menuPanel, "—— 记忆邮局 敬上", 18, InkSoft, new Vector2(-150f, -246f), new Vector2(360f, 26f), bold: false, TextAnchor.MiddleRight);
		settingsPanel = Panel(b, "Settings");
		Txt(settingsPanel, "邮差设置", 26, Ink, new Vector2(-190f, 200f), new Vector2(420f, 36f), bold: true, TextAnchor.MiddleLeft);
		float num4 = ((ui != null) ? ui.Volume : AudioListener.volume);
		Text volLabel = Txt(settingsPanel, "", 20, Ink, new Vector2(-190f, 128f), new Vector2(420f, 28f), bold: false, TextAnchor.MiddleLeft);
		Slider(settingsPanel, new Vector2(-190f, 90f), num4, 0f, 1f, delegate(float v)
		{
			ui?.SetVolume(v);
			volLabel.text = "音量\u3000" + Mathf.RoundToInt(v * 100f) + "%";
		});
		volLabel.text = "音量\u3000" + Mathf.RoundToInt(num4 * 100f) + "%";
		Text sensLabel = Txt(settingsPanel, "", 20, Ink, new Vector2(-190f, 26f), new Vector2(420f, 28f), bold: false, TextAnchor.MiddleLeft);
		Slider(settingsPanel, new Vector2(-190f, -12f), ChaseCamera.Sensitivity, 0.4f, 2f, delegate(float v)
		{
			ChaseCamera.Sensitivity = v;
			sensLabel.text = "视角灵敏度\u3000" + Mathf.RoundToInt(v * 100f) + "%";
		});
		sensLabel.text = "视角灵敏度\u3000" + Mathf.RoundToInt(ChaseCamera.Sensitivity * 100f) + "%";
		MenuButton(settingsPanel, "写好了", "回到寄件单", new Vector2(-190f, -150f), primary: false, delegate
		{
			Act(ShowMenu);
		});
		confirmPanel = Panel(b, "Confirm");
		Txt(confirmPanel, "要从头开始新的旅程吗？", 26, Ink, new Vector2(-190f, 190f), new Vector2(420f, 36f), bold: true, TextAnchor.MiddleLeft);
		Txt(confirmPanel, "现在的进度和集到的邮戳都会清空，\n不能找回。游戏会重新启动一次。", 19, InkSoft, new Vector2(-190f, 116f), new Vector2(420f, 64f), bold: false, TextAnchor.MiddleLeft);
		MenuButton(confirmPanel, "确定，重新出发", "清空进度并重启", new Vector2(-190f, 10f), primary: true, delegate
		{
			Act(RestartFresh);
		});
		MenuButton(confirmPanel, "再想想", "回到寄件单", new Vector2(-190f, -76f), primary: false, delegate
		{
			Act(ShowMenu);
		});
	}

	private RectTransform Panel(RectTransform parent, string name)
	{
		RectTransform component = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
		component.SetParent(parent, worldPositionStays: false);
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		Vector2 offsetMin = (component.offsetMax = Vector2.zero);
		component.offsetMin = offsetMin;
		return component;
	}

	private Button MenuButton(RectTransform parent, string title, string sub, Vector2 pos, bool primary, Action onClick)
	{
		Image image = Img(parent, title, TideTownStyleUI.PaperSprite, primary ? new Color32(165, 75, 53, byte.MaxValue) : Amber);
		image.raycastTarget = true;
		Place(image.rectTransform, pos, new Vector2(400f, 70f));
		Button button = image.gameObject.AddComponent<Button>();
		ColorBlock colors = button.colors;
		colors.normalColor = (primary ? Color.white : new Color(1f, 1f, 1f, 0f));
		colors.highlightedColor = (primary ? new Color(1.08f, 1.08f, 1.08f, 1f) : new Color(1f, 1f, 1f, 0.95f));
		colors.selectedColor = colors.normalColor;
		colors.pressedColor = (primary ? new Color(0.85f, 0.85f, 0.85f, 1f) : new Color(1f, 0.92f, 0.85f, 1f));
		colors.fadeDuration = 0.12f;
		button.colors = colors;
		button.targetGraphic = image;
		button.onClick.AddListener(delegate
		{
			onClick();
		});
		Txt(color: primary ? Cream : Ink, parent: image.rectTransform, text: (primary ? "" : "·  ") + title, size: 26, pos: new Vector2(primary ? 12f : (-10f), 11f), box: new Vector2(360f, 34f), bold: true, anchor: TextAnchor.MiddleLeft);
		Txt(image.rectTransform, sub, 16, primary ? new Color32(251, 230, 208, byte.MaxValue) : InkSoft, new Vector2(22f, -18f), new Vector2(340f, 22f), bold: false, TextAnchor.MiddleLeft);
		if (!primary)
		{
			Place(Img(image.rectTransform, "Underline", null, Rule).rectTransform, new Vector2(0f, -36f), new Vector2(380f, 2f));
		}
		return button;
	}

	private void Slider(RectTransform parent, Vector2 pos, float value, float min, float max, Action<float> onChange)
	{
		RectTransform component = new GameObject("Slider", typeof(RectTransform)).GetComponent<RectTransform>();
		component.SetParent(parent, worldPositionStays: false);
		Place(component, pos, new Vector2(400f, 26f));
		Image image = Img(component, "Track", TideTownStyleUI.PillSprite, new Color32(216, 200, 170, byte.MaxValue));
		Stretch(image.rectTransform, 0f);
		image.rectTransform.offsetMin = new Vector2(0f, 8f);
		image.rectTransform.offsetMax = new Vector2(0f, -8f);
		RectTransform component2 = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
		component2.SetParent(component, worldPositionStays: false);
		Stretch(component2, 0f);
		component2.offsetMin = new Vector2(0f, 8f);
		component2.offsetMax = new Vector2(0f, -8f);
		Image image2 = Img(component2, "Fill", TideTownStyleUI.PillSprite, Sea);
		image2.rectTransform.anchorMin = Vector2.zero;
		image2.rectTransform.anchorMax = new Vector2(0f, 1f);
		RectTransform rectTransform = image2.rectTransform;
		Vector2 offsetMin = (image2.rectTransform.offsetMax = Vector2.zero);
		rectTransform.offsetMin = offsetMin;
		RectTransform component3 = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>();
		component3.SetParent(component, worldPositionStays: false);
		Stretch(component3, 0f);
		component3.offsetMin = new Vector2(12f, 0f);
		component3.offsetMax = new Vector2(-12f, 0f);
		Image image3 = Img(component3, "Handle", BootLoader.CircleSprite, Clay);
		image3.raycastTarget = true;
		image3.rectTransform.sizeDelta = new Vector2(26f, 26f);
		Slider slider = component.gameObject.AddComponent<Slider>();
		slider.fillRect = image2.rectTransform;
		slider.handleRect = image3.rectTransform;
		slider.targetGraphic = image3;
		slider.minValue = min;
		slider.maxValue = max;
		slider.SetValueWithoutNotify(value);
		slider.onValueChanged.AddListener(delegate(float v)
		{
			onChange(v);
		});
		image.raycastTarget = true;
	}

	private static Image Img(Transform parent, string name, Sprite sprite, Color color)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(parent, worldPositionStays: false);
		obj.layer = parent.gameObject.layer;
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
		obj.layer = parent.gameObject.layer;
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

	private static void Stretch(RectTransform rt, float inset)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = new Vector2(inset, inset);
		rt.offsetMax = new Vector2(0f - inset, 0f - inset);
	}
}
