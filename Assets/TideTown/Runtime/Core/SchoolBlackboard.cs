using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class SchoolBlackboard : MonoBehaviour, IInteractable
{
	private const string GateOwner = "chalkboard";

	private const float HalfWidth = 0.366f;

	private const float Bottom = 0.247f;

	private const float Top = 0.708f;

	private const float Slope = -0.184f;

	private const float Front = 0.168f;

	private const float PushInSeconds = 0.25f;

	private const float BaseFov = 38f;

	private const float MaxZoom = 2.5f;

	private const int Eraser = 5;

	private Transform model;

	private EggboyPlayer player;

	private EggboyUI ui;

	private StoryDirector director;

	private SaveStore store;

	private GameState state;

	private ControlGate gate;

	private SchoolItem item;

	private ChalkDrawing drawing;

	private Material chalkMaterial;

	private Mesh inkMesh;

	private GameObject surfaceObject;

	private object token;

	private Camera view;

	private ChaseCamera chase;

	private bool previousChase;

	private bool previousPlayerLock;

	private Renderer[] playerRenderers;

	private bool[] previousRendering;

	private Vector3 previousCameraPosition;

	private Quaternion previousCameraRotation;

	private float previousFov;

	private float openedAt;

	private int tool;

	private bool painting;

	private bool panning;

	private bool resettingZoom;

	private float zoom = 1f;

	private float zoomTarget = 1f;

	private Vector2 zoomAnchor;

	private Vector2 pan;

	private Vector2 lastPanPointer;

	private static readonly string[] ToolNames = new string[6] { "奶白", "暖黄", "珊瑚橙", "海蓝", "浅绿", "板擦" };

	private static readonly string[] ToolKeys = new string[6] { "1", "2", "3", "4", "5", "X" };

	private static readonly Color32 Cream = new Color32(byte.MaxValue, 246, 223, byte.MaxValue);

	private readonly Transform[] shelfTools = new Transform[ToolNames.Length];

	private readonly Vector3[] toolAtRest = new Vector3[ToolNames.Length];

	private readonly RectTransform[] projectedButtons = new RectTransform[ToolNames.Length];

	private readonly Image[] tags = new Image[ToolNames.Length];

	private readonly Text[] tagLabels = new Text[ToolNames.Length];

	private readonly Image[] toolSwatches = new Image[ToolNames.Length];

	private readonly List<UnityEngine.Object> runtimeArt = new List<UnityEngine.Object>();

	private Transform shelf;

	private RectTransform controls;

	private Text toolHint;

	private Text zoomHint;

	private Button undoButton;

	private Button resetButton;

	private Image zoomDock;

	private Image actionsCard;

	private Text shortcutHint;

	private int zoomPercent = -1;

	private Image pointerMark;

	private Sprite chalkRing;

	public static bool IsOpen { get; private set; }

	public string EntityId => "blackboard";

	public Vector3 Position { get; private set; }

	internal bool ViewReady
	{
		get
		{
			if (token != null)
			{
				return Time.unscaledTime - openedAt >= 0.25f;
			}
			return false;
		}
	}

	internal ChalkDrawing Drawing => drawing;

	internal int SelectedTool => tool;

	internal float ViewZoom => zoom;

	internal bool Init(SchoolItem data, GameBootstrap boot)
	{
		GameObject gameObject = GameObject.Find("S_blackboard");
		MeshFilter meshFilter = ((gameObject != null) ? gameObject.GetComponentInChildren<MeshFilter>() : null);
		Shader shader = Resources.Load<Shader>("BlackboardChalk");
		if (meshFilter == null || meshFilter.sharedMesh == null || shader == null)
		{
			return false;
		}
		model = meshFilter.transform;
		item = data;
		Position = new Vector3(data.x, data.y, data.z);
		player = boot.player;
		ui = boot.ui;
		director = boot.director;
		gate = boot.gate;
		store = boot.store;
		state = boot.state;
		drawing = new ChalkDrawing(state.blackboardChalk);
		BuildSurface(shader);
		BuildTools();
		return true;
	}

	private static Vector3 SurfaceLocal(Vector2 uv)
	{
		float num = Mathf.LerpUnclamped(0.247f, 0.708f, uv.y);
		return new Vector3(Mathf.LerpUnclamped(0.366f, -0.366f, uv.x), num, 0.168f + -0.184f * num);
	}

	private Vector3 SurfaceWorld(Vector2 uv)
	{
		return model.TransformPoint(SurfaceLocal(uv));
	}

	private void BuildSurface(Shader shader)
	{
		surfaceObject = new GameObject("Blackboard chalk layer", typeof(MeshFilter), typeof(MeshRenderer));
		surfaceObject.transform.SetParent(model, worldPositionStays: false);
		inkMesh = new Mesh
		{
			name = "C7 slate inset"
		};
		inkMesh.vertices = new Vector3[4]
		{
			SurfaceLocal(Vector2.zero),
			SurfaceLocal(Vector2.right),
			SurfaceLocal(Vector2.up),
			SurfaceLocal(Vector2.one)
		};
		inkMesh.uv = new Vector2[4]
		{
			Vector2.zero,
			Vector2.right,
			Vector2.up,
			Vector2.one
		};
		inkMesh.triangles = new int[6] { 0, 2, 1, 1, 2, 3 };
		inkMesh.RecalculateBounds();
		surfaceObject.GetComponent<MeshFilter>().sharedMesh = inkMesh;
		chalkMaterial = new Material(shader);
		chalkMaterial.SetTexture("_Chalk", drawing.Texture);
		MeshRenderer component = surfaceObject.GetComponent<MeshRenderer>();
		component.shadowCastingMode = ShadowCastingMode.Off;
		component.receiveShadows = false;
		component.sharedMaterial = chalkMaterial;
	}

	public string Prompt(GameState s)
	{
		return "E\u3000黑板留言";
	}

	public bool CanInteract(GameState s)
	{
		if (token == null && ui != null && !ui.Cinematic && !ui.LetterOpen)
		{
			return !ui.Paused;
		}
		return false;
	}

	public void Interact(GameState s, InteractionService svc)
	{
		OpenBoard();
	}

	private void OpenBoard()
	{
		if (!CanInteract(state) || MenuController.OverlayOpen || ui.TitleShowing)
		{
			return;
		}
		view = Camera.main;
		if (view == null || !EnsureControls())
		{
			return;
		}
		token = gate.Acquire("chalkboard");
		if (token != null)
		{
			previousCameraPosition = view.transform.position;
			previousCameraRotation = view.transform.rotation;
			previousFov = view.fieldOfView;
			chase = view.GetComponent<ChaseCamera>();
			previousChase = chase != null && chase.enabled;
			if (previousChase)
			{
				chase.enabled = false;
			}
			previousPlayerLock = player.locked;
			player.locked = true;
			player.vel = Vector3.zero;
			playerRenderers = player.GetComponentsInChildren<Renderer>(includeInactive: true);
			previousRendering = new bool[playerRenderers.Length];
			for (int i = 0; i < playerRenderers.Length; i++)
			{
				previousRendering[i] = playerRenderers[i].forceRenderingOff;
				playerRenderers[i].forceRenderingOff = true;
			}
			ui.SetCinematic(on: true, "chalkboard");
			IsOpen = true;
			MenuController.ReleasePointer();
			openedAt = Time.unscaledTime;
			zoom = (zoomTarget = 1f);
			pan = (zoomAnchor = Vector2.zero);
			panning = (resettingZoom = false);
			controls.gameObject.SetActive(value: true);
			ApplyTool(0);
			PositionCamera();
			LayoutProjectedTools();
		}
	}

	private void PositionCamera()
	{
		Vector3 normalized = model.TransformDirection(new Vector3(0f, 1f, -0.184f)).normalized;
		Vector3 normalized2 = model.TransformDirection(new Vector3(0f, 0.184f, 1f)).normalized;
		Vector3 normalized3 = Vector3.Cross(normalized, -normalized2).normalized;
		Vector3 vector = SurfaceWorld(new Vector2(0.5f, 0.5f));
		float num = BaseHalfHeight();
		float num2 = Mathf.Tan(MathF.PI * 19f / 180f);
		float num3 = num / num2;
		float num4 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.25f));
		float t = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
		float num5 = zoom;
		zoom = Mathf.Lerp(zoom, zoomTarget, t);
		if (Mathf.Abs(zoom - zoomTarget) < 0.002f)
		{
			zoom = zoomTarget;
		}
		if (resettingZoom)
		{
			pan = Vector2.Lerp(pan, Vector2.zero, t);
		}
		else
		{
			pan += Vector2.Scale(zoomAnchor, new Vector2(view.aspect, 1f)) * (num * (1f / num5 - 1f / zoom));
		}
		float num6 = Mathf.Clamp01((zoom - 1f) / 1.5f);
		float num7 = Vector3.Distance(SurfaceWorld(Vector2.zero), SurfaceWorld(Vector2.right)) * 0.47f * num6;
		float num8 = Vector3.Distance(SurfaceWorld(Vector2.zero), SurfaceWorld(Vector2.up)) * 0.47f * num6;
		pan = new Vector2(Mathf.Clamp(pan.x, 0f - num7, num7), Mathf.Clamp(pan.y, 0f - num8, num8));
		view.fieldOfView = Mathf.Atan(num2 / zoom) * 2f * 57.29578f;
		view.transform.SetPositionAndRotation(vector + normalized3 * pan.x + normalized * pan.y + normalized2 * (num3 + 0.2f * (1f - num4)), Quaternion.LookRotation(-normalized2, normalized));
	}

	private float BaseHalfHeight()
	{
		return Mathf.Abs(model.lossyScale.y) * Mathf.Max(1.28f, 1.3f / Mathf.Max(0.7f, view.aspect)) * 0.5f;
	}

	internal void ZoomAt(Vector2 pointer, float scroll)
	{
		if (!ViewReady || Mathf.Abs(scroll) < 0.001f)
		{
			return;
		}
		float num = Mathf.Clamp(zoomTarget * Mathf.Pow(1.22f, scroll), 1f, 2.5f);
		if (!(Mathf.Abs(num - zoomTarget) < 0.001f))
		{
			EndStroke();
			resettingZoom = num <= 1f;
			zoomTarget = num;
			if (ScreenToSlate(pointer, out var _))
			{
				Vector3 vector = view.ScreenToViewportPoint(pointer);
				zoomAnchor = new Vector2(vector.x * 2f - 1f, vector.y * 2f - 1f);
			}
			else
			{
				zoomAnchor = Vector2.zero;
			}
		}
	}

	private void ResetZoom()
	{
		EndStroke();
		zoomTarget = 1f;
		resettingZoom = true;
	}

	internal void PanView(Vector2 screenDelta)
	{
		if (!(zoom <= 1.01f))
		{
			float num = 2f * BaseHalfHeight() / (zoom * Mathf.Max(1f, view.pixelHeight));
			pan -= screenDelta * num;
		}
	}

	private bool ScreenToSlate(Vector2 screen, out Vector2 uv)
	{
		uv = default(Vector2);
		Ray ray = view.ScreenPointToRay(screen);
		if (!new Plane(model.TransformDirection(new Vector3(0f, 0.184f, 1f)), SurfaceWorld(Vector2.zero)).Raycast(ray, out var enter))
		{
			return false;
		}
		Vector3 vector = model.InverseTransformPoint(ray.GetPoint(enter));
		uv = new Vector2((0.366f - vector.x) / 0.732f, (vector.y - 0.247f) / 0.46100003f);
		if (uv.x >= 0f && uv.x <= 1f && uv.y >= 0f)
		{
			return uv.y <= 1f;
		}
		return false;
	}

	internal Vector2 SlateToScreen(Vector2 uv)
	{
		return view.WorldToScreenPoint(SurfaceWorld(uv));
	}

	internal bool StartStroke(Vector2 screen)
	{
		if (!ViewReady || !ScreenToSlate(screen, out var uv))
		{
			return false;
		}
		drawing.Begin();
		drawing.Drag(uv, tool);
		painting = true;
		return true;
	}

	internal void DragStroke(Vector2 screen)
	{
		if (painting)
		{
			if (ScreenToSlate(screen, out var uv))
			{
				drawing.Drag(uv, tool);
			}
			else
			{
				drawing.BreakLine();
			}
		}
	}

	internal void EndStroke()
	{
		if (painting)
		{
			painting = false;
			drawing.End();
			undoButton.interactable = drawing.CanUndo;
		}
	}

	private void Update()
	{
		if (token == null)
		{
			return;
		}
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			LeaveBoard();
			return;
		}
		Vector2 vector = Input.mousePosition;
		if (ViewReady)
		{
			ZoomAt(vector, Input.mouseScrollDelta.y);
			if (Input.GetMouseButtonDown(2) && zoom > 1.01f)
			{
				EndStroke();
				panning = true;
				lastPanPointer = vector;
			}
			if (!Input.GetMouseButton(2))
			{
				panning = false;
			}
			if (panning)
			{
				PanView(vector - lastPanPointer);
				lastPanPointer = vector;
			}
		}
		PositionCamera();
		LayoutProjectedTools();
		if (!ViewReady)
		{
			return;
		}
		for (int i = 0; i < ChalkDrawing.Colors.Length; i++)
		{
			if (Input.GetKeyDown((KeyCode)(49 + i)))
			{
				ApplyTool(i);
			}
		}
		if (Input.GetKeyDown(KeyCode.X))
		{
			ApplyTool(5);
		}
		if (Input.GetKeyDown(KeyCode.Z) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
		{
			UndoStroke();
		}
		bool flag = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
		if (Input.GetMouseButtonDown(0) && !flag && !panning)
		{
			StartStroke(vector);
		}
		if (Input.GetMouseButton(0))
		{
			DragStroke(vector);
		}
		else
		{
			EndStroke();
		}
		Vector2 uv;
		bool flag2 = ScreenToSlate(vector, out uv);
		UpdatePointer(vector, uv, !flag && !panning && flag2);
		drawing.Upload();
	}

	private void UndoStroke()
	{
		EndStroke();
		drawing.Undo();
		drawing.Upload();
		undoButton.interactable = drawing.CanUndo;
	}

	private void SaveDrawing(bool showFeedback)
	{
		EndStroke();
		if (!drawing.HasChanges)
		{
			return;
		}
		state.blackboardChalk = drawing.Encode();
		try
		{
			store.Save(state);
			drawing.MarkSaved();
			if (showFeedback)
			{
				ui.ShowToast("黑板留言已保存", 2f);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Blackboard save failed: " + ex.Message);
			if (showFeedback)
			{
				ui.ShowToast("留言暂时无法写入存档", 3f);
			}
		}
	}

	internal void LeaveBoard()
	{
		CloseBoard(returnPointer: true);
	}

	private void InspectBack()
	{
		CloseBoard(returnPointer: false);
		director.InspectMemory(item.id, item.label, item.text, faceBack: true);
	}

	private void CloseBoard(bool returnPointer)
	{
		if (token == null)
		{
			return;
		}
		SaveDrawing(returnPointer);
		drawing.Upload();
		controls.gameObject.SetActive(value: false);
		for (int i = 0; i < playerRenderers.Length; i++)
		{
			if (playerRenderers[i] != null)
			{
				playerRenderers[i].forceRenderingOff = previousRendering[i];
			}
		}
		if (view != null)
		{
			view.fieldOfView = previousFov;
			view.transform.SetPositionAndRotation(previousCameraPosition, previousCameraRotation);
		}
		if (chase != null)
		{
			chase.enabled = previousChase;
		}
		gate.Release(token);
		token = null;
		if (player != null)
		{
			player.locked = previousPlayerLock;
		}
		IsOpen = false;
		if (ui != null)
		{
			ui.SetCinematic(on: false, "chalkboard");
		}
		if (returnPointer)
		{
			MenuController.RestorePointer(ui);
		}
	}

	private void OnApplicationFocus(bool focused)
	{
		if (!focused && token != null)
		{
			SaveDrawing(showFeedback: false);
		}
	}

	private void OnApplicationPause(bool paused)
	{
		if (paused && token != null)
		{
			SaveDrawing(showFeedback: false);
		}
	}

	private void OnApplicationQuit()
	{
		if (token != null)
		{
			SaveDrawing(showFeedback: false);
		}
	}

	private void OnDisable()
	{
		CloseBoard(returnPointer: false);
	}

	private void OnDestroy()
	{
		drawing?.Dispose();
		UnityEngine.Object.Destroy(inkMesh);
		UnityEngine.Object.Destroy(chalkMaterial);
		UnityEngine.Object.Destroy(surfaceObject);
		DisposeTools();
	}

	private void BuildTools()
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		if (shader == null)
		{
			return;
		}
		shelf = new GameObject("Chalk and felt eraser").transform;
		shelf.SetParent(model, worldPositionStays: false);
		Texture2D texture = MakeWoodGrain();
		Material material = ToolMaterial(shader, "Honey wood", new Color32(225, 167, 116, byte.MaxValue), texture);
		Material material2 = ToolMaterial(shader, "Sea-blue felt", new Color32(46, 77, 79, byte.MaxValue));
		for (int i = 0; i < ToolNames.Length; i++)
		{
			Transform transform = new GameObject(ToolNames[i]).transform;
			transform.SetParent(shelf, worldPositionStays: false);
			shelfTools[i] = transform;
			toolAtRest[i] = ((i == 5) ? new Vector3(-0.277f, 0.237f, 0.164f) : new Vector3(0.32f - 0.107f * (float)i, 0.234f, 0.164f + (float)(i % 2) * 0.01f));
			transform.localPosition = toolAtRest[i];
			transform.localRotation = Quaternion.Euler(0f, (float)(i % 3 - 1) * 9f, 0f);
			if (i == 5)
			{
				RoundedBlock("Wood grip", transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.123f, 0.032f, 0.061f), 0.009f, material);
				RoundedBlock("Worn felt pad", transform, new Vector3(0f, -0.009f, 0f), new Vector3(0.12f, 0.021f, 0.06f), 0.008f, material2);
			}
			else
			{
				Material material3 = ToolMaterial(shader, ToolNames[i] + " chalk", ChalkDrawing.Colors[i]);
				RoundedBlock("Chalk stick", transform, Vector3.zero, new Vector3(0.076f + (float)(i % 3) * 0.006f, 0.026f, 0.025f), 0.008f, material3);
			}
		}
	}

	private Material ToolMaterial(Shader shader, string label, Color32 color, Texture2D texture = null)
	{
		Material material = new Material(shader)
		{
			name = label
		};
		material.SetColor("_BaseColor", color);
		material.SetFloat("_Smoothness", 0.13f);
		if (texture != null)
		{
			material.SetTexture("_BaseMap", texture);
		}
		runtimeArt.Add(material);
		return material;
	}

	private Texture2D MakeWoodGrain()
	{
		Texture2D texture2D = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: true);
		Color32[] array = new Color32[4096];
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = Mathf.PerlinNoise((float)j * 0.036f, (float)i * 0.29f + Mathf.Sin((float)j * 0.061f) * 0.7f);
				byte b = (byte)Mathf.RoundToInt(192f + num * 62f);
				array[i * 64 + j] = new Color32(b, b, b, byte.MaxValue);
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		runtimeArt.Add(texture2D);
		return texture2D;
	}

	private void RoundedBlock(string label, Transform parent, Vector3 position, Vector3 size, float radius, Material material)
	{
		GameObject gameObject = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localPosition = position;
		List<Vector3> list = new List<Vector3>(96);
		List<Vector3> list2 = new List<Vector3>(96);
		List<Vector2> list3 = new List<Vector2>(96);
		List<int> list4 = new List<int>(324);
		Vector3 vector = size * 0.5f;
		Vector3 vector2 = vector - Vector3.one * radius;
		Vector3[] array = new Vector3[6]
		{
			Vector3.right,
			Vector3.left,
			Vector3.up,
			Vector3.down,
			Vector3.forward,
			Vector3.back
		};
		for (int i = 0; i < array.Length; i++)
		{
			Vector3 vector3 = array[i];
			int count = list.Count;
			Vector3 vector4 = ((Mathf.Abs(vector3.y) > 0.5f) ? Vector3.right : Vector3.Cross(Vector3.up, vector3));
			Vector3 vector5 = Vector3.Cross(vector3, vector4);
			float num = Mathf.Abs(Vector3.Dot(vector, vector4));
			float num2 = Mathf.Abs(Vector3.Dot(vector, vector5));
			float[] array2 = new float[4]
			{
				0f - num,
				0f - num + radius,
				num - radius,
				num
			};
			float[] array3 = new float[4]
			{
				0f - num2,
				0f - num2 + radius,
				num2 - radius,
				num2
			};
			for (int j = 0; j < 4; j++)
			{
				for (int k = 0; k < 4; k++)
				{
					Vector3 vector6 = Vector3.Scale(vector, vector3) + array2[k] * vector4 + array3[j] * vector5;
					Vector3 vector7 = new Vector3(Mathf.Clamp(vector6.x, 0f - vector2.x, vector2.x), Mathf.Clamp(vector6.y, 0f - vector2.y, vector2.y), Mathf.Clamp(vector6.z, 0f - vector2.z, vector2.z));
					Vector3 normalized = (vector6 - vector7).normalized;
					list.Add(vector7 + normalized * radius);
					list2.Add(normalized);
					list3.Add(new Vector2(0.5f + array2[k] / size.magnitude, 0.5f + array3[j] / size.magnitude));
					if (k != 3 && j != 3)
					{
						int num3 = count + j * 4 + k;
						list4.Add(num3);
						list4.Add(num3 + 1);
						list4.Add(num3 + 4);
						list4.Add(num3 + 4);
						list4.Add(num3 + 1);
						list4.Add(num3 + 5);
					}
				}
			}
		}
		Mesh mesh = new Mesh
		{
			name = label + " bevel"
		};
		mesh.SetVertices(list);
		mesh.SetNormals(list2);
		mesh.SetUVs(0, list3);
		mesh.SetTriangles(list4, 0);
		gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
		gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
		runtimeArt.Add(mesh);
	}

	private bool EnsureControls()
	{
		if (controls != null)
		{
			return true;
		}
		RectTransform rectTransform = TideTownStyleUI.FindHudCanvasRoot(ui);
		if (rectTransform == null)
		{
			return false;
		}
		controls = new GameObject("SchoolBlackboardControls", typeof(RectTransform)).GetComponent<RectTransform>();
		controls.SetParent(rectTransform, worldPositionStays: false);
		controls.anchorMin = Vector2.zero;
		controls.anchorMax = Vector2.one;
		RectTransform rectTransform2 = controls;
		Vector2 offsetMin = (controls.offsetMax = Vector2.zero);
		rectTransform2.offsetMin = offsetMin;
		Image image = Paper("BlackboardHeading", new Vector2(274f, 109f), Vector2.up, new Vector2(161f, -76f), Cream);
		TextOn(image.transform, "黑板留言", 26, TideTownStyleUI.SeaBlue, new Vector2(0f, 27f), new Vector2(226f, 34f), bold: true);
		toolHint = TextOn(image.transform, "", 14, TideTownStyleUI.InkDark, new Vector2(0f, -6f), new Vector2(226f, 23f), bold: false);
		zoomHint = TextOn(image.transform, "", 13, TideTownStyleUI.SeaBlue, new Vector2(0f, -32f), new Vector2(226f, 21f), bold: true);
		Button button = TideTownStyleUI.CreateWarmButton(controls, "保存并离开  Esc", Vector2.zero, new Vector2(173f, 43f), primaryAmber: true, LeaveBoard);
		button.name = "LeaveBlackboard";
		Place((RectTransform)button.transform, Vector2.one, new Vector2(-112f, -49f));
		Button button2 = TideTownStyleUI.CreateWarmButton(controls, "翻看背面", Vector2.zero, new Vector2(173f, 39f), primaryAmber: false, InspectBack);
		button2.name = "InspectBlackboardBack";
		Place((RectTransform)button2.transform, Vector2.one, new Vector2(-112f, -99f));
		actionsCard = Paper("BlackboardHelp", new Vector2(546f, 47f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), Cream);
		shortcutHint = TextOn(actionsCard.transform, "1–5 换粉笔   X 板擦", 15, TideTownStyleUI.InkDark, new Vector2(-139f, 0f), new Vector2(225f, 29f), bold: true);
		undoButton = TideTownStyleUI.CreateWarmButton(actionsCard.transform, "撤销  Ctrl+Z", new Vector2(52f, 0f), new Vector2(145f, 33f), primaryAmber: false, UndoStroke);
		undoButton.name = "UndoChalkStroke";
		resetButton = TideTownStyleUI.CreateWarmButton(actionsCard.transform, "复位视野", new Vector2(194f, 0f), new Vector2(120f, 33f), primaryAmber: false, ResetZoom);
		resetButton.name = "ResetChalkView";
		zoomDock = Paper("ZoomedChalkTray", new Vector2(546f, 54f), new Vector2(0.5f, 0f), new Vector2(0f, 91f), Cream);
		for (int i = 0; i < ToolNames.Length; i++)
		{
			int index = i;
			Image image2 = TideTownStyleUI.CreatePanel(controls, "ChalkTool" + i, (i == 5) ? 76f : 62f, 53f, Color.clear, rounded: false);
			projectedButtons[i] = image2.rectTransform;
			Image image3 = TideTownStyleUI.CreatePanel(image2.transform, "ChalkKeyTag", (i == 5) ? 64f : 59f, 23f, Cream);
			image3.sprite = TideTownStyleUI.PaperSprite;
			image3.raycastTarget = false;
			Place(image3.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f));
			tags[i] = image3;
			tagLabels[i] = TideTownStyleUI.CreateText(image3.transform, ToolKeys[i] + " " + ToolNames[i], 12, TideTownStyleUI.InkDark, TextAnchor.MiddleCenter, Vector2.zero, image3.rectTransform.sizeDelta, bold: true);
			Image image4 = TideTownStyleUI.CreatePanel(image3.transform, "ChalkColor", 13f, 13f, (i == 5) ? new Color32(81, 127, 129, byte.MaxValue) : ChalkDrawing.Colors[i]);
			image4.raycastTarget = false;
			Place(image4.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f));
			toolSwatches[i] = image4;
			Button button3 = image2.gameObject.AddComponent<Button>();
			button3.targetGraphic = image3;
			TideTownStyleUI.StyleButtonStates(button3);
			button3.onClick.AddListener(delegate
			{
				ApplyTool(index);
			});
		}
		pointerMark = TideTownStyleUI.CreatePanel(controls, "SlatePointer", 10f, 10f, Cream, rounded: false);
		pointerMark.raycastTarget = false;
		pointerMark.enabled = false;
		chalkRing = MakeChalkRing();
		controls.gameObject.SetActive(value: false);
		return true;
	}

	private Image Paper(string label, Vector2 size, Vector2 anchor, Vector2 position, Color32 color)
	{
		Image image = TideTownStyleUI.CreatePanel(controls, label, size.x, size.y, color);
		image.sprite = TideTownStyleUI.PaperSprite;
		image.raycastTarget = false;
		Place(image.rectTransform, anchor, position);
		Shadow shadow = image.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color32(28, 38, 36, 100);
		shadow.effectDistance = new Vector2(2f, -3f);
		return image;
	}

	private static Text TextOn(Transform parent, string value, int size, Color32 color, Vector2 position, Vector2 box, bool bold)
	{
		return TideTownStyleUI.CreateText(parent, value, size, color, TextAnchor.MiddleLeft, position, box, bold);
	}

	private static void Place(RectTransform rt, Vector2 anchor, Vector2 position)
	{
		Vector2 anchorMin = (rt.anchorMax = anchor);
		rt.anchorMin = anchorMin;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = position;
	}

	private void LayoutProjectedTools()
	{
		bool flag = zoom > 1.025f;
		if (zoomDock.gameObject.activeSelf != flag)
		{
			zoomDock.gameObject.SetActive(flag);
			shortcutHint.gameObject.SetActive(!flag);
			actionsCard.rectTransform.sizeDelta = new Vector2(flag ? 298f : 546f, 47f);
			((RectTransform)undoButton.transform).anchoredPosition = new Vector2(flag ? (-66f) : 52f, 0f);
			((RectTransform)resetButton.transform).anchoredPosition = new Vector2(flag ? 74f : 194f, 0f);
		}
		resetButton.interactable = flag || zoomTarget > 1f;
		int num = Mathf.RoundToInt(zoom * 100f);
		if (num != zoomPercent)
		{
			zoomPercent = num;
			zoomHint.text = num + "%   滚轮缩放 · 中键拖动";
		}
		for (int i = 0; i < shelfTools.Length; i++)
		{
			Vector3 vector = toolAtRest[i];
			if (shelfTools[i] != null)
			{
				float num2 = ((i == tool) ? 0.015f : 0f);
				shelfTools[i].localPosition = Vector3.Lerp(shelfTools[i].localPosition, vector + Vector3.up * num2, Mathf.Clamp01(16f * Time.unscaledDeltaTime));
			}
			toolSwatches[i].enabled = flag;
			RectTransform rectTransform = projectedButtons[i];
			RectTransform rectTransform2 = tags[i].rectTransform;
			if (flag)
			{
				rectTransform.sizeDelta = new Vector2(79f, 40f);
				Place(rectTransform, new Vector2(0.5f, 0f), new Vector2(((float)i - 2.5f) * 87f, 91f));
				rectTransform2.sizeDelta = new Vector2(79f, 36f);
				rectTransform2.anchoredPosition = Vector2.zero;
				tagLabels[i].rectTransform.anchoredPosition = new Vector2(7f, 0f);
				continue;
			}
			Vector2 vector2 = view.WorldToScreenPoint(model.TransformPoint(vector));
			RectTransformUtility.ScreenPointToLocalPointInRectangle(controls, vector2, null, out var localPoint);
			Vector2 screenPoint = ((shelfTools[i] != null) ? ((Vector2)view.WorldToScreenPoint(shelfTools[i].position)) : vector2);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(controls, screenPoint, null, out var localPoint2);
			float num3 = localPoint2.y + ((i == 5) ? 16f : 10f);
			float num4 = localPoint.y - 50f;
			float num5 = (num3 + num4) * 0.5f;
			rectTransform.sizeDelta = new Vector2((i == 5) ? 76f : 62f, num3 - num4);
			Place(rectTransform, new Vector2(0.5f, 0.5f), new Vector2(localPoint.x, num5));
			rectTransform2.sizeDelta = new Vector2((i == 5) ? 64f : 59f, 23f);
			rectTransform2.anchoredPosition = new Vector2(0f, localPoint.y - 36f - num5);
			tagLabels[i].rectTransform.anchoredPosition = Vector2.zero;
		}
	}

	private void ApplyTool(int index)
	{
		EndStroke();
		tool = index;
		for (int i = 0; i < tags.Length; i++)
		{
			tags[i].color = ((i == tool) ? TideTownStyleUI.SeaBlue : Cream);
			tagLabels[i].color = ((i == tool) ? Cream : TideTownStyleUI.InkDark);
		}
		toolHint.text = ((tool == 5) ? "板擦 · 按住左键擦拭" : (ToolNames[tool] + "粉笔 · 按住左键绘制"));
		undoButton.interactable = drawing.CanUndo;
	}

	private void UpdatePointer(Vector2 screen, Vector2 uv, bool visible)
	{
		pointerMark.enabled = visible;
		Cursor.visible = !visible;
		if (visible)
		{
			RectTransformUtility.ScreenPointToLocalPointInRectangle(controls, screen, null, out var localPoint);
			bool flag = tool == 5;
			Vector2 vector = (flag ? new Vector2(18f, 12f) : new Vector2(3.8f, 3.8f));
			Vector2 screenPoint = SlateToScreen(uv + new Vector2(vector.x / 640f, vector.y / 416f));
			RectTransformUtility.ScreenPointToLocalPointInRectangle(controls, screenPoint, null, out var localPoint2);
			float x = Mathf.Max(10f, 2f * Mathf.Abs(localPoint2.x - localPoint.x));
			float y = Mathf.Max(10f, 2f * Mathf.Abs(localPoint2.y - localPoint.y));
			pointerMark.sprite = (flag ? TideTownStyleUI.RoundedFrameSprite : chalkRing);
			pointerMark.type = Image.Type.Simple;
			pointerMark.color = (flag ? Cream : ChalkDrawing.Colors[tool]);
			pointerMark.rectTransform.sizeDelta = new Vector2(x, y);
			Place(pointerMark.rectTransform, new Vector2(0.5f, 0.5f), localPoint);
		}
	}

	private Sprite MakeChalkRing()
	{
		Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false);
		Color32[] array = new Color32[1024];
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				float magnitude = new Vector2((float)j - 15.5f, (float)i - 15.5f).magnitude;
				byte a = (byte)(255f * Mathf.Clamp01(2.3f - Mathf.Abs(magnitude - 12.5f)));
				array[i * 32 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, a);
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		Sprite result = Sprite.Create(texture2D, new Rect(0f, 0f, 32f, 32f), Vector2.one * 0.5f);
		runtimeArt.Add(texture2D);
		runtimeArt.Add(result);
		return result;
	}

	private void DisposeTools()
	{
		if (controls != null)
		{
			UnityEngine.Object.Destroy(controls.gameObject);
		}
		if (shelf != null)
		{
			UnityEngine.Object.Destroy(shelf.gameObject);
		}
		foreach (UnityEngine.Object item in runtimeArt)
		{
			UnityEngine.Object.Destroy(item);
		}
	}
}
