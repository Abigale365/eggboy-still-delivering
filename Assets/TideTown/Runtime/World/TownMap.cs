using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TownMap : MonoBehaviour
{
	[Serializable]
	private class MapCam
	{
		public string name;

		public float[] pos;

		public float[] euler;

		public float fov;

		public int width;

		public int height;
	}

	[Serializable]
	private class MapCameraList
	{
		public MapCam[] cameras;
	}

	[Serializable]
	private class RouteNodeJson
	{
		public string id;

		public bool stop;

		public float x;

		public float z;
	}

	[Serializable]
	private class RouteFileJson
	{
		public RouteNodeJson[] route;
	}

	[Serializable]
	public sealed class MapBounds
	{
		public float cx;

		public float cz;

		public float spanX;

		public float spanZ;

		public int pixelWidth;

		public int pixelHeight;

		public bool Matches(int width, int height)
		{
			if (width > 0 && height > 0 && pixelWidth > 0 && pixelHeight > 0 && spanX > 0f && spanZ > 0f && !float.IsInfinity(spanX) && !float.IsInfinity(spanZ) && !float.IsNaN(cx) && !float.IsInfinity(cx) && !float.IsNaN(cz) && !float.IsInfinity(cz) && Mathf.Abs(spanX / spanZ - (float)pixelWidth / (float)pixelHeight) < 0.001f)
			{
				return Mathf.Abs((float)pixelWidth / (float)pixelHeight - (float)width / (float)height) < 0.001f;
			}
			return false;
		}

		public Vector2 ProjectPixels(Vector3 position)
		{
			return new Vector2((0.5f + (position.x - cx) / spanX) * (float)pixelWidth, (0.5f - (position.z - cz) / spanZ) * (float)pixelHeight);
		}
	}

	[Serializable]
	private sealed class MapIllustration
	{
		public Vector2[] source;

		public Vector2[] illustration;

		public int[] triangles;

		public bool IsValid()
		{
			if (source == null || illustration == null || source.Length != illustration.Length || triangles == null || triangles.Length < 3 || triangles.Length % 3 != 0)
			{
				return false;
			}
			for (int i = 0; i < triangles.Length; i += 3)
			{
				int num = triangles[i];
				int num2 = triangles[i + 1];
				int num3 = triangles[i + 2];
				if ((uint)num >= source.Length || (uint)num2 >= source.Length || (uint)num3 >= source.Length || !(Cross(source[num2] - source[num], source[num3] - source[num]) > 0.001f) || !(Cross(illustration[num2] - illustration[num], illustration[num3] - illustration[num]) > 0.001f))
				{
					return false;
				}
			}
			return true;
		}

		public Vector2 Project(Vector2 point)
		{
			for (int i = 0; i < triangles.Length; i += 3)
			{
				int num = triangles[i];
				int num2 = triangles[i + 1];
				int num3 = triangles[i + 2];
				Vector2 a = source[num2] - source[num];
				Vector2 b = source[num3] - source[num];
				Vector2 vector = point - source[num];
				float num4 = Cross(a, b);
				float num5 = Cross(vector, b) / num4;
				float num6 = Cross(a, vector) / num4;
				float num7 = 1f - num5 - num6;
				if (num7 >= -1E-05f && num5 >= -1E-05f && num6 >= -1E-05f)
				{
					return num7 * illustration[num] + num5 * illustration[num2] + num6 * illustration[num3];
				}
			}
			return point;
		}

		private static float Cross(Vector2 a, Vector2 b)
		{
			return a.x * b.y - a.y * b.x;
		}
	}

	private struct LandmarkInfo
	{
		public string name;

		public string sub;

		public Vector3 worldPos;

		public string stampId;

		public string sceneName;

		public LandmarkInfo(string n, string s, Vector3 p, string st, string sceneName)
		{
			name = n;
			sub = s;
			worldPos = p;
			stampId = st;
			this.sceneName = sceneName;
		}
	}

	public enum GroundGuideMode
	{
		Automatic,
		Always,
		Off
	}

	private static readonly LandmarkInfo[] Landmarks = new LandmarkInfo[12]
	{
		new LandmarkInfo("山口邮筒", "起点", new Vector3(-31.04f, 3.2f, -6.76f), "", "P_MAILBOX"),
		new LandmarkInfo("旧邮局", "老周的值班室", new Vector3(-22.5f, 5.2f, -9.51f), "", "B1_post_office"),
		new LandmarkInfo("① 林阿婆面包店", "第一站 · 烤炉与柴火", new Vector3(-14.4f, 5.2f, -8.5f), "bakery", "B2_bakery"),
		new LandmarkInfo("面包店门铃", "面包店正门旁", new Vector3(-12.58f, 3.72f, -6.66f), "", "Bakery_bell"),
		new LandmarkInfo("♪ 广场八音盒", "心愿展台 · 声影机关", new Vector3(-11.9f, 3.3f, -5.8f), "", "MusicBoxPlinth"),
		new LandmarkInfo("② 潮汐镇学校", "第二站 · 阿萤与小黑", new Vector3(13.53f, 5.6f, -2.02f), "school", "B3_school"),
		new LandmarkInfo("学校操场", "秋千 / 红球 / 滑梯", new Vector3(17.5f, 3.8f, -5.6f), "", "P_21"),
		new LandmarkInfo("学校后院 · 狗窝", "拖鞋 / 狗碗", new Vector3(21.76f, 4.1f, 1.95f), "", "S_doghouse"),
		new LandmarkInfo("⚙ 渡口齿轮绞盘", "涨潮过海机关", new Vector3(13.4f, 1.8f, -13.1f), "", "Scene_M3_潮汐船坞齿轮绞盘_总成"),
		new LandmarkInfo("海边船棚 & 码头", "拿煤油壶", new Vector3(19.5f, 3.2f, -14.8f), "", "B5_boathouse"),
		new LandmarkInfo("③ 海角灯塔", "第三站 · 点亮灯头", new Vector3(33.59f, 8.5f, 9.62f), "lighthouse", "B4_lighthouse"),
		new LandmarkInfo("山顶新邮局", "终点 · 记忆寄件台", new Vector3(-27.8f, 5.5f, 18.55f), "", "P_06")
	};

	private EggboyPlayer player;

	private EggboyUI ui;

	private StoryDirector director;

	private InteractionService interactions;

	private Transform bakeryArrival;

	private Transform schoolArrival;

	private Transform schoolSlide;

	private Transform summit;

	private ControlGate gate;

	private IslandWorld island;

	private Texture2D preview;

	private Texture2D playerPortrait;

	private object mapLock;

	private MapBounds mapBounds;

	private MapIllustration illustratedProjection;

	private readonly Vector3[] landmarkPositions = new Vector3[Landmarks.Length];

	private bool hasCam;

	private Vector3 camPos;

	private Vector3 camFwd;

	private Vector3 camUp;

	private float focal;

	private int imgW = 1774;

	private int imgH = 887;

	private const string GuidePreference = "tidetown.groundGuide";

	private const float RecallSeconds = 6f;

	private const float StraySeconds = 8f;

	private const float GuideHeight = 0.07f;

	private static readonly float[] GuideDistances = new float[4] { 1.55f, 3.1f, 4.65f, 6.2f };

	private static readonly bool CaptureMode = Array.Exists(Environment.GetCommandLineArgs(), (string a) => a == "-shot" || a == "-feeldemo" || a == "-shot_aa");

	public static bool ForceShow;

	private bool guideGlow;

	private GameObject guideRoot;

	private Transform[] guideDots;

	private Renderer[] guideDotRenderers;

	private Renderer targetDisc;

	private Material guideBaseMat;

	private Texture2D guideTexture;

	private readonly MaterialPropertyBlock guideColor = new MaterialPropertyBlock();

	private string guideStep;

	private Vector3 guideTarget;

	private Vector3 idleAnchor;

	private float guideRecall;

	private float guideIdle;

	private float guideStray;

	private float guideOpacity;

	private float nextGuideRoute;

	private bool guideOffRoute;

	private bool navLoaded;

	private readonly List<Vector2> navNodes = new List<Vector2>();

	private readonly List<List<int>> navAdj = new List<List<int>>();

	private readonly List<Vector3> guidePath = new List<Vector3>();

	private readonly List<Vector3> mapPath = new List<Vector3>();

	private readonly List<Vector3> aheadPath = new List<Vector3>();

	private static readonly Color AtlasBlue = TideTownStyleUI.SeaBlue;

	private static readonly Color AtlasMist = TideTownStyleUI.SeaMist;

	private static readonly Color AtlasParchment = TideTownStyleUI.PaperColor;

	private static readonly Color AtlasParchmentDeep = new Color32(243, 229, 201, byte.MaxValue);

	private static readonly Color AtlasInk = TideTownStyleUI.InkDark;

	private static readonly Color AtlasInkSoft = new Color32(41, 58, 57, 150);

	private static readonly Color AtlasInkMuted = TideTownStyleUI.InkMuted;

	private static readonly Color AtlasRule = new Color32(203, 184, 148, byte.MaxValue);

	private static readonly Color AtlasCinnabar = TideTownStyleUI.Clay;

	private static readonly Color AtlasGold = new Color32(233, 169, 63, byte.MaxValue);

	private static readonly Color AtlasGoldSoft = new Color32(253, 220, 146, byte.MaxValue);

	private static readonly string[] StampIds = new string[4] { "bakery", "school", "lighthouse", "ending" };

	private static readonly string[] StampLabels = new string[4] { "烤炉", "小黑", "灯塔", "回家" };

	private float uiK = 1f;

	public GroundGuideMode GuideMode { get; private set; }

	public bool IsOpen { get; private set; }

	public bool HasOrthographicMap => mapBounds != null;

	public float ClosedAt { get; private set; }

	public void Init(EggboyPlayer p, EggboyUI u, StoryDirector d, ControlGate g)
	{
		player = p;
		ui = u;
		director = d;
		interactions = GetComponent<InteractionService>();
		if (director != null)
		{
			bakeryArrival = GameObject.Find("P_11")?.transform;
			schoolArrival = GameObject.Find("P_19")?.transform;
			schoolSlide = GameObject.Find("S_slide")?.transform;
			summit = GameObject.Find("P_06")?.transform;
		}
		gate = g;
		island = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		GuideMode = (GroundGuideMode)Mathf.Clamp(PlayerPrefs.GetInt("tidetown.groundGuide.v2", 2), 0, 2);
		preview = Resources.Load<Texture2D>("UI/town_preview");
		if (preview != null)
		{
			imgW = preview.width;
			imgH = preview.height;
		}
		LoadCamera();
		LoadIllustratedMap();
		playerPortrait = Resources.Load<Texture2D>("UI/eggboy_map_avatar");
		EnsureNavGraph();
	}

	private void LoadIllustratedMap()
	{
		if (mapBounds == null)
		{
			return;
		}
		Texture2D texture2D = Resources.Load<Texture2D>("UI/town_illustrated");
		TextAsset textAsset = Resources.Load<TextAsset>("UI/town_illustrated_projection");
		if (!(texture2D == null) && !(textAsset == null) && mapBounds.Matches(texture2D.width, texture2D.height))
		{
			MapIllustration mapIllustration = JsonUtility.FromJson<MapIllustration>(textAsset.text);
			if (mapIllustration == null || !mapIllustration.IsValid())
			{
				Debug.LogError("MAP_ILLUSTRATION invalid calibration");
				return;
			}
			preview = texture2D;
			illustratedProjection = mapIllustration;
		}
	}

	internal void UseOrthographicMapForAudit()
	{
		if (illustratedProjection != null)
		{
			preview = Resources.Load<Texture2D>("UI/town_preview");
			illustratedProjection = null;
		}
	}

	private void LoadCamera()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("UI/map_bounds");
		if (textAsset != null && preview != null)
		{
			MapBounds mapBounds = JsonUtility.FromJson<MapBounds>(textAsset.text);
			if (mapBounds != null && mapBounds.Matches(imgW, imgH))
			{
				this.mapBounds = mapBounds;
				imgW = mapBounds.pixelWidth;
				imgH = mapBounds.pixelHeight;
				hasCam = true;
			}
			else
			{
				Debug.LogError("MAP_BOUNDS invalid for town_preview: " + imgW + "x" + imgH);
			}
			return;
		}
		TextAsset textAsset2 = Resources.Load<TextAsset>("World/world_layout");
		if (textAsset2 == null)
		{
			return;
		}
		MapCameraList mapCameraList = JsonUtility.FromJson<MapCameraList>(textAsset2.text);
		if (mapCameraList == null || mapCameraList.cameras == null)
		{
			return;
		}
		MapCam[] cameras = mapCameraList.cameras;
		foreach (MapCam mapCam in cameras)
		{
			if (!(mapCam.name != "preview") && mapCam.pos != null && mapCam.euler != null && mapCam.pos.Length >= 3 && mapCam.euler.Length >= 3)
			{
				camPos = new Vector3(mapCam.pos[0], mapCam.pos[1], mapCam.pos[2]);
				float f = mapCam.euler[0] * (MathF.PI / 180f);
				camFwd = new Vector3(0f, 0f - Mathf.Sin(f), Mathf.Cos(f));
				camUp = new Vector3(0f, Mathf.Cos(f), Mathf.Sin(f));
				focal = (float)mapCam.height * 0.5f / Mathf.Tan(mapCam.fov * (MathF.PI / 180f) * 0.5f);
				if (mapCam.width > 0)
				{
					imgW = mapCam.width;
				}
				if (mapCam.height > 0)
				{
					imgH = mapCam.height;
				}
				hasCam = true;
				break;
			}
		}
	}

	private void EnsureNavGraph()
	{
		if (navLoaded)
		{
			return;
		}
		navLoaded = true;
		navNodes.Clear();
		navAdj.Clear();
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		if (textAsset == null)
		{
			return;
		}
		RouteFileJson routeFileJson = JsonUtility.FromJson<RouteFileJson>(textAsset.text);
		if (routeFileJson == null || routeFileJson.route == null || routeFileJson.route.Length == 0)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < routeFileJson.route.Length; i++)
		{
			RouteNodeJson routeNodeJson = routeFileJson.route[i];
			int num2 = AddOrFindNode(new Vector2(routeNodeJson.x, routeNodeJson.z));
			if (num >= 0 && (navNodes[num2] - navNodes[num]).sqrMagnitude < 256f)
			{
				Link(num, num2);
			}
			num = num2;
		}
		Vector2[] array = new Vector2[3]
		{
			new Vector2(-22.85f, 16.9f),
			new Vector2(-23.3f, 18.4f),
			new Vector2(-25.6f, 18.7f)
		};
		foreach (Vector2 pt2 in array)
		{
			AddOrFindNode(pt2);
		}
		for (int k = 0; k < navNodes.Count; k++)
		{
			for (int l = k + 1; l < navNodes.Count; l++)
			{
				if (!((navNodes[k] - navNodes[l]).sqrMagnitude > 17.639997f))
				{
					Vector2 vector = (navNodes[k] + navNodes[l]) * 0.5f;
					if (island == null || island.InWalkArea(vector.x, vector.y))
					{
						Link(k, l);
					}
				}
			}
		}
		int AddOrFindNode(Vector2 pt)
		{
			for (int m = 0; m < navNodes.Count; m++)
			{
				if ((navNodes[m] - pt).sqrMagnitude < 0.040000003f)
				{
					return m;
				}
			}
			int count = navNodes.Count;
			navNodes.Add(pt);
			navAdj.Add(new List<int>());
			return count;
		}
		void Link(int a, int b)
		{
			if (a != b && a >= 0 && b >= 0)
			{
				if (!navAdj[a].Contains(b))
				{
					navAdj[a].Add(b);
				}
				if (!navAdj[b].Contains(a))
				{
					navAdj[b].Add(a);
				}
			}
		}
	}

	private void BuildGuidePolyline(Vector3 from, Vector3 to, List<Vector3> outPath)
	{
		outPath.Clear();
		outPath.Add(from);
		EnsureNavGraph();
		Vector2 vector = new Vector2(from.x, from.z);
		Vector2 vector2 = new Vector2(to.x, to.z);
		float num = Vector2.Distance(vector, vector2);
		if (num <= 7f || navNodes.Count < 2)
		{
			bool flag = true;
			if (island != null && num > 1.5f)
			{
				for (int i = 1; i <= 4; i++)
				{
					Vector2 vector3 = Vector2.Lerp(vector, vector2, (float)i / 5f);
					if (!island.InWalkArea(vector3.x, vector3.y))
					{
						flag = false;
						break;
					}
				}
			}
			if (flag || navNodes.Count < 2)
			{
				outPath.Add(to);
				return;
			}
		}
		int num2 = 0;
		int num3 = 0;
		float num4 = float.MaxValue;
		float num5 = float.MaxValue;
		for (int j = 0; j < navNodes.Count; j++)
		{
			float sqrMagnitude = (navNodes[j] - vector).sqrMagnitude;
			if (sqrMagnitude < num4)
			{
				num4 = sqrMagnitude;
				num2 = j;
			}
			float sqrMagnitude2 = (navNodes[j] - vector2).sqrMagnitude;
			if (sqrMagnitude2 < num5)
			{
				num5 = sqrMagnitude2;
				num3 = j;
			}
		}
		if (num2 == num3)
		{
			outPath.Add(new Vector3(navNodes[num2].x, from.y, navNodes[num2].y));
			outPath.Add(to);
			return;
		}
		int count = navNodes.Count;
		float[] array = new float[count];
		int[] array2 = new int[count];
		bool[] array3 = new bool[count];
		for (int k = 0; k < count; k++)
		{
			array[k] = float.MaxValue;
			array2[k] = -1;
		}
		array[num2] = 0f;
		for (int l = 0; l < count; l++)
		{
			int num6 = -1;
			float num7 = float.MaxValue;
			for (int m = 0; m < count; m++)
			{
				if (!array3[m] && array[m] < num7)
				{
					num7 = array[m];
					num6 = m;
				}
			}
			if (num6 < 0 || num6 == num3)
			{
				break;
			}
			array3[num6] = true;
			List<int> list = navAdj[num6];
			for (int n = 0; n < list.Count; n++)
			{
				int num8 = list[n];
				if (!array3[num8])
				{
					float num9 = Vector2.Distance(navNodes[num6], navNodes[num8]);
					if (array[num6] + num9 < array[num8])
					{
						array[num8] = array[num6] + num9;
						array2[num8] = num6;
					}
				}
			}
		}
		if (array2[num3] >= 0)
		{
			List<int> list2 = new List<int>();
			for (int num10 = num3; num10 >= 0; num10 = array2[num10])
			{
				list2.Add(num10);
			}
			list2.Reverse();
			int num11 = 0;
			if (list2.Count >= 2)
			{
				Vector2 rhs = navNodes[list2[1]] - navNodes[list2[0]];
				Vector2 lhs = vector - navNodes[list2[0]];
				if (rhs.sqrMagnitude > 0.01f && Vector2.Dot(lhs, rhs) > 0f)
				{
					num11 = 1;
				}
			}
			int num12 = list2.Count - 1;
			if (num12 - num11 >= 1)
			{
				Vector2 rhs2 = navNodes[list2[num12]] - navNodes[list2[num12 - 1]];
				Vector2 lhs2 = vector2 - navNodes[list2[num12 - 1]];
				if (rhs2.sqrMagnitude > 0.01f && Vector2.Dot(lhs2, rhs2) < rhs2.sqrMagnitude)
				{
					num12--;
				}
			}
			for (int num13 = num11; num13 <= num12; num13++)
			{
				Vector2 vector4 = navNodes[list2[num13]];
				float t = ((float)num13 + 1f) / ((float)list2.Count + 1f);
				outPath.Add(new Vector3(vector4.x, Mathf.Lerp(from.y, to.y, t), vector4.y));
			}
		}
		outPath.Add(to);
	}

	public Vector3 PathPointAhead(Vector3 from, Vector3 to, float ahead)
	{
		BuildGuidePolyline(from, to, aheadPath);
		return SamplePolylineXZ(aheadPath, ahead);
	}

	private static float PolylineTotalLengthXZ(List<Vector3> path)
	{
		float num = 0f;
		for (int i = 0; i + 1 < path.Count; i++)
		{
			num += Vector2.Distance(new Vector2(path[i].x, path[i].z), new Vector2(path[i + 1].x, path[i + 1].z));
		}
		return num;
	}

	private static Vector3 SamplePolylineXZ(List<Vector3> path, float targetDist)
	{
		if (path.Count == 0)
		{
			return Vector3.zero;
		}
		if (path.Count == 1 || targetDist <= 0f)
		{
			return path[0];
		}
		float num = 0f;
		for (int i = 0; i + 1 < path.Count; i++)
		{
			Vector3 a = path[i];
			Vector3 b = path[i + 1];
			float num2 = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
			if (num + num2 >= targetDist && num2 > 0.0001f)
			{
				return Vector3.Lerp(a, b, (targetDist - num) / num2);
			}
			num += num2;
		}
		return path[path.Count - 1];
	}

	public void Init(EggboyPlayer p, EggboyUI u)
	{
		Init(p, u, null, null);
	}

	public bool CanOpen()
	{
		if (player == null || ui == null)
		{
			return false;
		}
		if (ui.LetterOpen || ui.Paused || ui.TitleShowing || MenuController.OverlayOpen)
		{
			return false;
		}
		if (director != null && (director.Cutscene || director.Ended || director.Climbing))
		{
			return false;
		}
		return true;
	}

	public void Toggle()
	{
		if (IsOpen)
		{
			Close();
		}
		else
		{
			if (!CanOpen() || (gate != null && gate.Locked))
			{
				return;
			}
			if (gate != null)
			{
				mapLock = gate.Acquire("map");
				if (mapLock == null)
				{
					return;
				}
			}
			IsOpen = true;
			for (int i = 0; i < Landmarks.Length; i++)
			{
				LandmarkInfo landmarkInfo = Landmarks[i];
				GameObject gameObject = ((mapBounds != null) ? GameObject.Find(landmarkInfo.sceneName) : null);
				landmarkPositions[i] = ((gameObject != null) ? gameObject.transform.position : landmarkInfo.worldPos);
			}
			ui.SetCinematic(on: true, "map");
			player.locked = true;
			player.vel = Vector3.zero;
			MenuController.ReleasePointer();
		}
	}

	public void Close()
	{
		if (IsOpen)
		{
			IsOpen = false;
			if (ui != null)
			{
				ui.SetCinematic(on: false, "map");
			}
			ClosedAt = Time.unscaledTime;
			MenuController.NoteScriptUnlock();
			if (gate != null && mapLock != null)
			{
				gate.Release(mapLock);
			}
			mapLock = null;
			if (player != null)
			{
				player.locked = false;
			}
			MenuController.RestorePointer(ui);
		}
	}

	private void Update()
	{
		if (player == null || ui == null)
		{
			return;
		}
		UpdateGroundGuide3D();
		if (ui.LetterOpen || ui.Paused || ui.TitleShowing || MenuController.OverlayOpen)
		{
			if (IsOpen)
			{
				Close();
			}
		}
		else if (EasyInputBridge.Down(KeyCode.M))
		{
			Toggle();
		}
		else if (IsOpen && EasyInputBridge.Down(KeyCode.Escape))
		{
			Close();
		}
	}

	public void CycleGuideMode()
	{
		GuideMode = (GroundGuideMode)((int)(GuideMode + 1) % 3);
		PlayerPrefs.SetInt("tidetown.groundGuide.v2", (int)GuideMode);
		PlayerPrefs.Save();
	}

	private void OnDisable()
	{
		Close();
		if (guideRoot != null)
		{
			guideRoot.SetActive(value: false);
		}
	}

	private void OnDestroy()
	{
		if (guideBaseMat != null)
		{
			UnityEngine.Object.Destroy(guideBaseMat);
		}
		if (guideTexture != null)
		{
			UnityEngine.Object.Destroy(guideTexture);
		}
	}

	private void EnsureGuide3D()
	{
		if (guideRoot != null)
		{
			return;
		}
		Shader shader = Shader.Find("TideTown/SoftGlow");
		if (shader != null)
		{
			guideGlow = true;
			guideRoot = new GameObject("DynamicGroundGuide3D");
			guideRoot.transform.SetParent(base.transform, worldPositionStays: false);
			guideBaseMat = new Material(shader)
			{
				name = "GroundGuideSoftGlow"
			};
			guideBaseMat.SetFloat("_Mode", 0f);
			guideBaseMat.SetFloat("_Billboard", 1f);
			guideBaseMat.SetFloat("_Push", 0f);
			guideBaseMat.SetFloat("_Intensity", 1f);
			guideDots = new Transform[GuideDistances.Length];
			guideDotRenderers = new Renderer[GuideDistances.Length];
			for (int i = 0; i < guideDots.Length; i++)
			{
				guideDotRenderers[i] = CreateGuideDisc("GuideDot_" + i, guideRoot.transform);
				guideDots[i] = guideDotRenderers[i].transform;
			}
			Transform transform = new GameObject("TargetBeacon").transform;
			transform.SetParent(guideRoot.transform, worldPositionStays: false);
			targetDisc = CreateGuideDisc("TargetGroundDisc", transform);
			return;
		}
		Shader shader2 = Shader.Find("Universal Render Pipeline/Unlit");
		if (shader2 == null)
		{
			return;
		}
		guideRoot = new GameObject("DynamicGroundGuide3D");
		guideRoot.transform.SetParent(base.transform, worldPositionStays: false);
		guideBaseMat = new Material(shader2)
		{
			name = "GroundGuideGlow"
		};
		guideBaseMat.SetFloat("_Surface", 1f);
		guideBaseMat.SetFloat("_SrcBlend", 5f);
		guideBaseMat.SetFloat("_DstBlend", 10f);
		guideBaseMat.SetFloat("_ZWrite", 0f);
		guideBaseMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		guideBaseMat.SetOverrideTag("RenderType", "Transparent");
		guideBaseMat.renderQueue = 3000;
		guideTexture = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false)
		{
			name = "GroundGuideFalloff",
			wrapMode = TextureWrapMode.Clamp
		};
		Color32[] array = new Color32[1024];
		for (int j = 0; j < 32; j++)
		{
			for (int k = 0; k < 32; k++)
			{
				float a = 1f - Mathf.SmoothStep(0f, 1f, new Vector2(((float)k + 0.5f) / 32f * 2f - 1f, ((float)j + 0.5f) / 32f * 2f - 1f).magnitude);
				array[j * 32 + k] = new Color(1f, 1f, 1f, a);
			}
		}
		guideTexture.SetPixels32(array);
		guideTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		guideBaseMat.SetTexture("_BaseMap", guideTexture);
		guideDots = new Transform[GuideDistances.Length];
		guideDotRenderers = new Renderer[GuideDistances.Length];
		for (int l = 0; l < guideDots.Length; l++)
		{
			guideDotRenderers[l] = CreateGuideDisc("GuideDot_" + l, guideRoot.transform);
			guideDots[l] = guideDotRenderers[l].transform;
		}
		Transform transform2 = new GameObject("TargetBeacon").transform;
		transform2.SetParent(guideRoot.transform, worldPositionStays: false);
		targetDisc = CreateGuideDisc("TargetGroundDisc", transform2);
	}

	private Renderer CreateGuideDisc(string name, Transform parent)
	{
		GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
		obj.name = name;
		Collider component = obj.GetComponent<Collider>();
		if (component != null)
		{
			component.enabled = false;
			UnityEngine.Object.Destroy(component);
		}
		obj.transform.SetParent(parent, worldPositionStays: false);
		obj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
		Renderer component2 = obj.GetComponent<Renderer>();
		component2.shadowCastingMode = ShadowCastingMode.Off;
		component2.receiveShadows = false;
		component2.sharedMaterial = guideBaseMat;
		component2.enabled = false;
		return component2;
	}

	private void UpdateGroundGuide3D()
	{
		if (CaptureMode)
		{
			if (island != null && island.mailboxTrail != null && island.mailboxTrail.activeSelf)
			{
				island.mailboxTrail.SetActive(value: false);
			}
			HideGuide();
			return;
		}
		if (ui.inputClose && !ui.TitleShowing && !ui.Paused && !ui.LetterOpen && !ui.Cinematic && !IsOpen && !MenuController.OverlayOpen && !WoodenMechanismPuzzleModal.IsOpen && !ObjectInspectModal.IsOpen && Input.GetKeyDown(KeyCode.H))
		{
			GuideMode = (GuideMode == GroundGuideMode.Off) ? GroundGuideMode.Always : GroundGuideMode.Off;
			PlayerPrefs.SetInt("tidetown.groundGuide.v2", (int)GuideMode);
			PlayerPrefs.Save();
			ui.ShowToast((GuideMode == GroundGuideMode.Off) ? "已关闭指路引导（自由探索模式，迷路时按 H 可开启）" : "已开启指路引导（再按 H 或在 Esc 菜单中可关闭）", 2.6f);
		}
		if (GuideMode == GroundGuideMode.Off || !ui.inputClose || ui.TitleShowing || ui.Paused || ui.LetterOpen || ui.Cinematic || IsOpen || MenuController.OverlayOpen || player.locked || director == null || director.IsAuditMode || director.Cutscene || director.Ended || director.Climbing || director.State == null || !TryGetStepTarget(out var targetPos, out var _))
		{
			HideGuide();
			return;
		}
		Vector3 position = player.transform.position;
		if (guideStep != director.State.step || (guideTarget - targetPos).sqrMagnitude > 0.01f)
		{
			guideStep = director.State.step;
			guideTarget = targetPos;
			guideRecall = 6f;
			guideIdle = (guideStray = 0f);
			idleAnchor = position;
			nextGuideRoute = 0f;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if (Input.GetKeyDown(KeyCode.H))
		{
			guideRecall = 6f;
		}
		if (HorizontalDistance(position, idleAnchor) > 0.55f)
		{
			guideIdle = 0f;
			idleAnchor = position;
		}
		else
		{
			guideIdle += unscaledDeltaTime;
		}
		if (Time.unscaledTime >= nextGuideRoute)
		{
			nextGuideRoute = Time.unscaledTime + 0.18f;
			BuildGuidePolyline(position, targetPos, guidePath);
			guideOffRoute = HorizontalDistance(position, targetPos) > 4f && !NearGuideRoute(position);
		}
		guideStray = (guideOffRoute ? (guideStray + unscaledDeltaTime) : 0f);
		bool flag = GuideMode == GroundGuideMode.Always || ForceShow || guideRecall > 0f || guideIdle >= 8f || guideStray >= 8f;
		guideOpacity = Mathf.MoveTowards(guideOpacity, flag ? 0.92f : 0.06f, unscaledDeltaTime * 2.5f);
		guideRecall = Mathf.Max(0f, guideRecall - unscaledDeltaTime);
		if (guideOpacity <= 0.02f)
		{
			return;
		}
		EnsureGuide3D();
		if (!(guideRoot == null))
		{
			if (!guideRoot.activeSelf)
			{
				guideRoot.SetActive(value: true);
			}
			float num = PolylineTotalLengthXZ(guidePath);
			float num2 = 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 2.3f);
			for (int i = 0; i < guideDots.Length; i++)
			{
				float num3 = GuideDistances[i];
				float t = (float)i / ((float)guideDots.Length - 1f);
				Vector3 vector = SamplePolylineXZ(guidePath, num3);
				float num4 = HorizontalDistance(position, vector);
				guideDotRenderers[i].enabled = num3 < num - 0.45f && num4 >= 1.5f && num4 <= 6.5f && PlaceGuideDisc(guideDotRenderers[i], vector, Mathf.Lerp(0.17f, 0.12f, t), guideOpacity * Mathf.Lerp(0.96f, 0.42f, t) * num2);
			}
			targetDisc.enabled = num <= 6.5f && HorizontalDistance(position, targetPos) >= 1.5f && PlaceGuideDisc(targetDisc, targetPos, 0.23f, guideOpacity * 0.62f * num2);
		}
	}

	private void HideGuide()
	{
		if (guideRoot != null && guideRoot.activeSelf)
		{
			guideRoot.SetActive(value: false);
		}
		guideIdle = (guideStray = (guideOpacity = 0f));
		if (player != null)
		{
			idleAnchor = player.transform.position;
		}
		nextGuideRoute = 0f;
	}

	private bool PlaceGuideDisc(Renderer disc, Vector3 position, float size, float opacity)
	{
		float num = ((island != null) ? island.GroundAt(position.x, position.z, position.y + 1.2f) : position.y);
		if (island != null && (num <= 0.05f || num < island.WaterY - 0.01f))
		{
			return false;
		}
		if (guideGlow)
		{
			float num2 = Mathf.Sin(Time.unscaledTime * 2.4f + position.x * 1.7f) * 0.03f;
			disc.transform.position = new Vector3(position.x, num + 0.18f + num2, position.z);
			disc.transform.localScale = Vector3.one * size * 3.2f;
			guideColor.SetColor("_Color", new Color(1f, 0.62f, 0.22f, 1f) * opacity * 3.4f);
			disc.SetPropertyBlock(guideColor);
			return true;
		}
		disc.transform.position = new Vector3(position.x, num + 0.07f, position.z);
		disc.transform.localScale = Vector3.one * size;
		guideColor.SetColor("_BaseColor", new Color(1f, 0.78f, 0.36f, opacity));
		disc.SetPropertyBlock(guideColor);
		return true;
	}

	private bool NearGuideRoute(Vector3 position)
	{
		Vector2 vector = new Vector2(position.x, position.z);
		for (int i = 0; i < navNodes.Count; i++)
		{
			Vector2 vector2 = navNodes[i];
			foreach (int item in navAdj[i])
			{
				if (item > i)
				{
					Vector2 vector3 = navNodes[item] - vector2;
					float num = Mathf.Clamp01(Vector2.Dot(vector - vector2, vector3) / vector3.sqrMagnitude);
					if ((vector - vector2 - num * vector3).sqrMagnitude <= 9f)
					{
						return true;
					}
				}
			}
		}
		return navNodes.Count == 0;
	}

	private static float HorizontalDistance(Vector3 a, Vector3 b)
	{
		return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
	}

	public Vector2 ProjectUV(Vector3 p)
	{
		return Project(p);
	}

	private Vector2 Project(Vector3 p)
	{
		if (mapBounds != null)
		{
			Vector2 vector = mapBounds.ProjectPixels(p);
			if (illustratedProjection == null)
			{
				return vector;
			}
			return illustratedProjection.Project(vector);
		}
		Vector3 lhs = p - camPos;
		float num = Vector3.Dot(lhs, camFwd);
		float num2 = Vector3.Dot(lhs, camUp);
		if (Mathf.Abs(num) < 0.01f)
		{
			num = 0.01f;
		}
		return new Vector2((float)imgW * 0.5f + focal * lhs.x / num, (float)imgH * 0.5f - focal * num2 / num);
	}

	private Vector2 WorldToMapScreen(Vector3 worldPos, Rect img)
	{
		Vector2 vector = Project(worldPos);
		return new Vector2(img.x + vector.x / (float)imgW * img.width, img.y + vector.y / (float)imgH * img.height);
	}

	internal bool TryGetStepTarget(out Vector3 targetPos, out string targetLabel)
	{
		targetPos = Vector3.zero;
		targetLabel = "";
		if (director == null || director.State == null)
		{
			return false;
		}
		GameState state = director.State;
		switch (state.step)
		{
		case "P0_boot":
		case "P1_mailbox":
			return InteractionTarget("mailbox_03", "山口红色邮筒（按 E 取信）", out targetPos, out targetLabel);
		case "B0_arrive":
			targetPos = ((bakeryArrival != null) ? bakeryArrival.position : new Vector3(-14.4f, 3f, -9.58f));
			targetLabel = "① 林阿婆面包店门前";
			return true;
		case "B1_matches":
			return InteractionTarget("stall", "橱窗下的摊位（拿火柴盒）", out targetPos, out targetLabel);
		case "B2_wood":
			return InteractionTarget("wood", "烤炉右前方（抱柴火捆）", out targetPos, out targetLabel);
		case "B3_load":
			return InteractionTarget("stove", "面包店石烤炉（把柴火塞进炉膛）", out targetPos, out targetLabel);
		case "B4_light":
			if (!state.flags.Contains("oven_lit"))
			{
				return InteractionTarget("stove", "面包店石烤炉（按住 E 擦火柴）", out targetPos, out targetLabel);
			}
			goto case "S0_arrive";
		case "S0_arrive":
			targetPos = ((schoolArrival != null) ? schoolArrival.position : new Vector3(13.53f, 3f, -4.25f));
			targetLabel = "② 潮汐镇学校（靠近自动读信）";
			return true;
		case "S1_slipper":
			return InteractionTarget("slipper", "学校后院狗窝旁（查看拖鞋）", out targetPos, out targetLabel);
		case "S2_ball":
			return InteractionTarget("ball", "学校操场秋千前（捡起红球）", out targetPos, out targetLabel);
		case "S3_bowl":
			return InteractionTarget("bowl", "学校后院狗窝前（红球放进狗碗）", out targetPos, out targetLabel);
		case "S4_read":
			targetPos = ((schoolSlide != null) ? schoolSlide.position : new Vector3(19f, 3f, -4f));
			targetLabel = "操场滑梯旁（小黑与阿萤的信）";
			return true;
		case "L0_cross":
			targetPos = new Vector3(12.34f, 1.2f, -12.16f);
			targetLabel = "海边淹水坡道（踩木箱去船棚）";
			return true;
		case "L1_shed":
			return InteractionTarget("oil", "海边船棚屋檐下（拿煤油壶）", out targetPos, out targetLabel);
		case "L2_door":
			return InteractionTarget("lighthouse_door", "③ 海角灯塔塔门（按 E 上塔）", out targetPos, out targetLabel);
		case "L3_oil":
		case "L4_light":
		case "L5_reply":
			return InteractionTarget("lamp_head", "灯塔顶层灯头（加煤油/转灯头）", out targetPos, out targetLabel);
		case "E0_back":
		case "E1_read":
			return InteractionTarget("ending_desk", "旧邮局门前课桌（读老周的信）", out targetPos, out targetLabel);
		case "E2_climb":
		case "E3_end":
			targetPos = ((summit != null) ? summit.position : new Vector3(-27.8f, 5.5f, 18.55f));
			targetLabel = "山顶新邮局（记忆寄件工作台）";
			return true;
		default:
			return false;
		}
	}

	private bool InteractionTarget(string id, string label, out Vector3 target, out string targetLabel)
	{
		target = default(Vector3);
		targetLabel = label;
		if (interactions != null)
		{
			return interactions.TryGetPosition(id, out target);
		}
		return false;
	}

	private float U(float v)
	{
		return v * uiK;
	}

	private void OnGUI()
	{
		if (IsOpen && !(player == null))
		{
			Font font = GUI.skin.font;
			Color color = GUI.color;
			GUI.skin.font = TideTownStyleUI.UiFont;
			GUI.color = Color.white;
			float num = Screen.width;
			float num2 = Screen.height;
			uiK = Mathf.Min(num / 1600f, num2 / 900f);
			Fill(new Rect(0f, 0f, num, num2), new Color(0.07f, 0.15f, 0.18f, 0.39f), 0f);
			GUI.DrawTexture(new Rect(0f, 0f, num, num2), TideTownStyleUI.SoftVignetteSprite.texture, ScaleMode.StretchToFill, alphaBlend: true, 0f, new Color(0.06f, 0.13f, 0.16f, 0.7f), 0f, 0f);
			float num3 = (float)imgW / (float)imgH;
			float num4 = U(11f);
			float num5 = U(18f);
			float num6 = U(70f);
			float num7 = U(47f);
			float num8 = U(10f);
			float num9 = (num4 + num5) * 2f;
			float num10 = num4 * 2f + U(6f) + num6 + num8 + U(6f) + num7 + U(4f);
			float num11 = num * 0.94f - num9;
			float num12 = num11 / num3;
			if (num12 + num10 > num2 * 0.93f)
			{
				num12 = num2 * 0.93f - num10;
				num11 = num12 * num3;
			}
			Rect r = new Rect((num - num11 - num9) * 0.5f, (num2 - num12 - num10) * 0.5f, num11 + num9, num12 + num10);
			for (int i = 1; i <= 4; i++)
			{
				Fill(Grow(new Rect(r.x, r.y + U(8f), r.width, r.height), U((float)i * 3.5f)), new Color(0f, 0f, 0f, 0.08f), U(22f + (float)i * 3.5f));
			}
			Fill(r, AtlasBlue, U(20f));
			Line(Grow(r, 0f - U(4f)), AtlasMist, Mathf.Max(1f, U(1.2f)), U(16f));
			Rect r2 = Grow(r, 0f - num4);
			Fill(r2, AtlasParchment, U(10f));
			Rect r3 = new Rect(r2.x + num5, r2.y + U(6f), r2.width - num5 * 2f, num6);
			DrawAtlasHeader(r3);
			Fill(new Rect(r3.x, r3.yMax - U(1f), r3.width, Mathf.Max(1f, U(1.2f))), AtlasRule, 0f);
			Rect rect = new Rect(r2.x + num5, r3.yMax + num8, num11, num12);
			Fill(Grow(rect, U(3f)), AtlasBlue, U(6f));
			if (preview != null)
			{
				GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill, alphaBlend: false, 0f, Color.white, 0f, U(3.5f));
			}
			else
			{
				Fill(rect, AtlasParchmentDeep, U(3.5f));
			}
			Line(Grow(rect, 0f - U(6f)), new Color(0.99f, 0.96f, 0.88f, 0.5f), Mathf.Max(1f, U(1f)), U(3f));
			DrawCompass(rect);
			if (hasCam && preview != null)
			{
				DrawMapMarks(rect);
			}
			Rect r4 = new Rect(r2.x + num5, rect.yMax + U(6f), r2.width - num5 * 2f, num7);
			DrawAtlasFooter(r4);
			GUI.color = color;
			GUI.skin.font = font;
		}
	}

	private void DrawAtlasHeader(Rect r)
	{
		float y = r.center.y;
		Rect position = new Rect(r.x, y - U(23f), U(46f), U(46f));
		GUI.DrawTexture(position, TideTownStyleUI.PostmarkRingSprite.texture, ScaleMode.ScaleToFit, alphaBlend: true, 0f, AtlasCinnabar, 0f, 0f);
		GameState gameState = ((director != null) ? director.State : null);
		int num = 0;
		string[] stampIds = StampIds;
		foreach (string item in stampIds)
		{
			if (gameState != null && gameState.stamps.Contains(item))
			{
				num++;
			}
		}
		float x = position.xMax + U(12f);
		GUI.Label(new Rect(x, y - U(25f), U(205f), U(34f)), "潮汐镇投递地图", AtlasStyle(26f, AtlasBlue, bold: true, TextAnchor.MiddleLeft));
		GUI.Label(new Rect(x, y + U(10f), U(205f), U(18f)), "邮戳 " + num + " / " + StampIds.Length, AtlasStyle(14f, AtlasInkMuted, bold: true, TextAnchor.MiddleLeft));
		Rect rect = new Rect(r.xMax - U(102f), y - U(19f), U(102f), U(38f));
		Fill(rect, rect.Contains(Event.current.mousePosition) ? AtlasMist : AtlasParchmentDeep, U(10f));
		Line(rect, AtlasRule, Mathf.Max(1f, U(1f)), U(10f));
		GUI.Label(rect, "M / Esc  合上", AtlasStyle(13f, AtlasInk, bold: true, TextAnchor.MiddleCenter));
		if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
		{
			Close();
		}
		float num2 = rect.xMin - U(20f);
		GUIStyle style = AtlasStyle(15f, AtlasInk, bold: true, TextAnchor.MiddleLeft);
		GUIStyle gUIStyle = AtlasStyle(12f, AtlasCinnabar, bold: true, TextAnchor.MiddleLeft);
		GUIStyle gUIStyle2 = AtlasStyle(12f, AtlasInkMuted, bold: false, TextAnchor.MiddleLeft);
		for (int num3 = StampIds.Length - 1; num3 >= 0; num3--)
		{
			bool flag = gameState?.stamps.Contains(StampIds[num3]) ?? false;
			Rect r2 = new Rect(num2 - U(118f), y - U(27f), U(118f), U(54f));
			Fill(r2, flag ? new Color(1f, 0.9f, 0.78f) : AtlasParchmentDeep, U(10f));
			Sprite sprite = PostalArtwork.Stamp(StampIds[num3]);
			Rect position2 = new Rect(r2.x + U(6f), y - U(23f), U(53f), U(46f));
			if (sprite != null)
			{
				GUI.DrawTexture(position2, sprite.texture, ScaleMode.ScaleToFit, alphaBlend: true, 0f, new Color(1f, 1f, 1f, flag ? 1f : 0.32f), 0f, 0f);
			}
			GUI.Label(new Rect(r2.x + U(64f), y - U(18f), U(50f), U(21f)), StampLabels[num3], style);
			GUI.Label(new Rect(r2.x + U(64f), y + U(4f), U(50f), U(17f)), flag ? "已盖章" : "待投递", flag ? gUIStyle : gUIStyle2);
			num2 = r2.xMin - U(9f);
		}
	}

	private void DrawCompass(Rect img)
	{
		float num = Mathf.Clamp(img.height * 0.15f, U(58f), U(100f));
		Rect position = new Rect(img.xMax - num - U(14f), img.y + U(12f), num, num);
		GUI.DrawTexture(position, TideTownStyleUI.CompassRoseTexture, ScaleMode.StretchToFill, alphaBlend: true, 0f, Color.white, 0f, 0f);
		GUIStyle style = AtlasStyle(Mathf.Max(10f, num / uiK * 0.16f), AtlasCinnabar, bold: true, TextAnchor.MiddleCenter);
		GUI.Label(new Rect(position.center.x - num * 0.2f, position.y + num * 0.06f, num * 0.4f, num * 0.17f), "N", style);
	}

	private void DrawMapMarks(Rect img)
	{
		Vector3 position = player.transform.position;
		Vector2 vector = WorldToMapScreen(position, img);
		float num = U((playerPortrait != null) ? 21f : 9.5f);
		Rect fixedRect = Circle(vector, num);
		string text = ((playerPortrait != null) ? "你在这里" : "● 你在这里");
		GUIStyle gUIStyle = AtlasStyle(12.5f, AtlasParchment, bold: true, TextAnchor.MiddleCenter);
		float num2 = TextW(gUIStyle, text) + U(22f);
		float num3 = num + U(9f);
		float num4 = vector.x + num3;
		if (num4 + num2 > img.xMax)
		{
			num4 = vector.x - num3 - num2;
		}
		Rect rect = ClampRectToMap(new Rect(num4, vector.y - U(11.5f), num2, U(23f)), img);
		bool flag = false;
		Rect rect2 = default(Rect);
		Vector2 vector2 = default(Vector2);
		string text2 = "";
		GUIStyle gUIStyle2 = AtlasStyle(13f, AtlasInk, bold: true, TextAnchor.MiddleCenter);
		if (TryGetStepTarget(out var targetPos, out var targetLabel))
		{
			vector2 = WorldToMapScreen(targetPos, img);
			text2 = "★ 当前目标：" + targetLabel;
			float num5 = Mathf.Min(img.width - U(8f), TextW(gUIStyle2, text2) + U(26f));
			rect2 = ClampRectToMap(new Rect(vector2.x - num5 * 0.5f, vector2.y + U(13f), num5, U(25f)), img);
			flag = true;
			BuildGuidePolyline(position, targetPos, mapPath);
			if (mapPath.Count >= 2)
			{
				for (int i = 0; i + 1 < mapPath.Count; i++)
				{
					DrawRouteDots(WorldToMapScreen(mapPath[i], img), WorldToMapScreen(mapPath[i + 1], img));
				}
			}
			else
			{
				DrawRouteDots(vector, vector2);
			}
		}
		GUIStyle gUIStyle3 = AtlasStyle(12.5f, AtlasInk, bold: true, TextAnchor.MiddleCenter);
		gUIStyle3.richText = true;
		int num6 = Landmarks.Length;
		Vector2[] array = new Vector2[num6];
		Rect[] array2 = new Rect[num6];
		string[] array3 = new string[num6];
		bool[] array4 = new bool[num6];
		for (int j = 0; j < num6; j++)
		{
			LandmarkInfo landmarkInfo = Landmarks[j];
			Vector2 vector3 = WorldToMapScreen(landmarkPositions[j], img);
			bool flag2 = !string.IsNullOrEmpty(landmarkInfo.stampId) && director != null && director.State != null && director.State.stamps.Contains(landmarkInfo.stampId);
			float num7 = Mathf.Min(img.width - U(8f), TextW(gUIStyle3, flag2 ? (landmarkInfo.name + " ✓") : landmarkInfo.name) + U(20f));
			array[j] = vector3;
			array3[j] = (flag2 ? (landmarkInfo.name + " <color=#b23528>✓</color>") : landmarkInfo.name);
			array4[j] = flag2;
			array2[j] = new Rect(vector3.x - num7 * 0.5f, vector3.y - U(29f), num7, U(22f));
		}
		float pad = U(4f);
		for (int k = 0; k < 6; k++)
		{
			for (int l = 0; l < num6; l++)
			{
				Rect movable = array2[l];
				if (flag)
				{
					SeparateRect(ref movable, rect2, pad);
				}
				if (playerPortrait != null)
				{
					SeparateRect(ref movable, fixedRect, pad);
				}
				SeparateRect(ref movable, rect, pad);
				for (int m = l + 1; m < num6; m++)
				{
					Rect b = array2[m];
					SeparatePair(ref movable, ref b, pad);
					array2[m] = ClampRectToMap(b, img);
				}
				array2[l] = ClampRectToMap(movable, img);
			}
		}
		for (int n = 0; n < num6; n++)
		{
			Vector2 vector4 = array[n];
			Rect rect3 = array2[n];
			Vector2 b2 = new Vector2(Mathf.Clamp(vector4.x, rect3.xMin + U(6f), rect3.xMax - U(6f)), (vector4.y < rect3.center.y) ? rect3.yMin : rect3.yMax);
			if (Vector2.Distance(vector4, b2) > U(8f))
			{
				DrawDottedLine(vector4, b2, AtlasInkSoft, 5, U(2.6f));
			}
			Fill(Circle(vector4, U(5f)), AtlasInk, U(5f));
			Fill(Circle(vector4, U(3f)), AtlasParchment, U(3f));
			DrawTag(rect3, array4[n] ? AtlasCinnabar : AtlasInk, new Color(AtlasParchment.r, AtlasParchment.g, AtlasParchment.b, 0.95f));
			GUI.Label(rect3, array3[n], gUIStyle3);
		}
		if (flag)
		{
			Fill(Circle(vector2, U(10f)), AtlasInk, U(10f));
			Fill(Circle(vector2, U(8.5f)), AtlasGold, U(8.5f));
			GUI.Label(Circle(vector2 + new Vector2(0f, 0f - U(0.5f)), U(9f)), "★", AtlasStyle(12f, AtlasInk, bold: true, TextAnchor.MiddleCenter));
			DrawTag(rect2, AtlasInk, AtlasGoldSoft);
			GUI.Label(rect2, text2, gUIStyle2);
		}
		DrawPlayerPin(position, vector, img, num);
		DrawTag(rect, AtlasInk, AtlasBlue);
		GUI.Label(rect, text, gUIStyle);
	}

	private void DrawAtlasFooter(Rect r)
	{
		float y = r.center.y;
		Fill(new Rect(r.x, r.y, r.width, Mathf.Max(1f, U(1.2f))), AtlasRule, 0f);
		GUIStyle gUIStyle = AtlasStyle(13f, AtlasInk, bold: false, TextAnchor.MiddleLeft);
		float num = r.xMax;
		string[] array = new string[3] { "推荐步道", "当前目标", "你的位置" };
		for (int i = 0; i < array.Length; i++)
		{
			float num2 = TextW(gUIStyle, array[i]);
			GUI.Label(new Rect(num - num2, y - U(12f), num2 + U(4f), U(24f)), array[i], gUIStyle);
			num -= num2 + U(7f);
			switch (i)
			{
			case 0:
			{
				for (int k = 0; k < 3; k++)
				{
					RouteDot(new Vector2(num - U(4f) - (float)k * U(9f), y));
				}
				num -= U(26f);
				break;
			}
			case 1:
			{
				Rect position = new Rect(num - U(20f), y - U(13f), U(20f), U(26f));
				GUIStyle style = AtlasStyle(17f, AtlasInk, bold: true, TextAnchor.MiddleCenter);
				Vector2[] array2 = new Vector2[4]
				{
					new Vector2(-1f, 0f),
					new Vector2(1f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, 1f)
				};
				for (int j = 0; j < array2.Length; j++)
				{
					Vector2 vector = array2[j];
					GUI.Label(new Rect(position.x + vector.x * U(1.1f), position.y + vector.y * U(1.1f), position.width, position.height), "★", style);
				}
				GUI.Label(position, "★", AtlasStyle(17f, AtlasGold, bold: true, TextAnchor.MiddleCenter));
				num -= U(20f);
				break;
			}
			default:
			{
				float num3 = U((playerPortrait != null) ? 11.5f : 8f);
				DrawPlayerMedallion(new Vector2(num - num3, y), num3);
				num -= 2f * num3;
				break;
			}
			}
			num -= U(22f);
		}
		GUIStyle gUIStyle2 = AtlasStyle(11.5f, AtlasParchment, bold: true, TextAnchor.MiddleCenter);
		Rect rect = new Rect(r.x, y - U(11f), TextW(gUIStyle2, "当前任务") + U(20f), U(22f));
		Fill(rect, AtlasBlue, rect.height * 0.5f);
		GUI.Label(rect, "当前任务", gUIStyle2);
		GUIStyle gUIStyle3 = AtlasStyle(15f, AtlasInk, bold: false, TextAnchor.MiddleLeft);
		gUIStyle3.clipping = TextClipping.Clip;
		GUI.Label(new Rect(rect.xMax + U(12f), r.y, Mathf.Max(0f, num - rect.xMax - U(16f)), r.height), GoalText().Replace('\n', ' '), gUIStyle3);
	}

	private void DrawRouteDots(Vector2 a, Vector2 b)
	{
		int num = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / U(13f)), 2, 48);
		for (int i = 0; i <= num; i++)
		{
			RouteDot(Vector2.Lerp(a, b, (float)i / (float)num));
		}
	}

	private void RouteDot(Vector2 p)
	{
		Fill(Circle(p, U(3.7f)), new Color(AtlasInk.r, AtlasInk.g, AtlasInk.b, 0.6f), U(3.7f));
		Fill(Circle(p, U(2.5f)), AtlasGold, U(2.5f));
	}

	private void DrawTag(Rect r, Color border, Color fill)
	{
		float num = Mathf.Max(1f, U(1.4f));
		Fill(r, border, r.height * 0.5f);
		Fill(Grow(r, 0f - num), fill, r.height * 0.5f - num);
	}

	private static void SeparatePair(ref Rect a, ref Rect b, float pad)
	{
		float num = Mathf.Min(a.xMax + pad, b.xMax + pad) - Mathf.Max(a.xMin, b.xMin);
		float num2 = Mathf.Min(a.yMax + pad, b.yMax + pad) - Mathf.Max(a.yMin, b.yMin);
		if (!(num <= 0f) && !(num2 <= 0f))
		{
			if (num2 <= num * 1.4f)
			{
				float num3 = ((a.center.y <= b.center.y) ? (-1f) : 1f);
				float num4 = num2 * 0.52f * num3;
				a.y += num4;
				b.y -= num4;
			}
			else
			{
				float num5 = ((a.center.x <= b.center.x) ? (-1f) : 1f);
				float num6 = num * 0.52f * num5;
				a.x += num6;
				b.x -= num6;
			}
		}
	}

	private static void SeparateRect(ref Rect movable, Rect fixedRect, float pad)
	{
		float num = Mathf.Min(movable.xMax + pad, fixedRect.xMax + pad) - Mathf.Max(movable.xMin, fixedRect.xMin);
		float num2 = Mathf.Min(movable.yMax + pad, fixedRect.yMax + pad) - Mathf.Max(movable.yMin, fixedRect.yMin);
		if (!(num <= 0f) && !(num2 <= 0f))
		{
			float num3 = ((movable.center.y <= fixedRect.center.y) ? (-1f) : 1f);
			movable.y += num2 * num3;
		}
	}

	private static Rect ClampRectToMap(Rect r, Rect bounds)
	{
		r.x = Mathf.Clamp(r.x, bounds.xMin + 4f, bounds.xMax - r.width - 4f);
		r.y = Mathf.Clamp(r.y, bounds.yMin + 4f, bounds.yMax - r.height - 4f);
		return r;
	}

	private static void DrawDottedLine(Vector2 a, Vector2 b, Color col, int segments, float size)
	{
		for (int i = 0; i <= segments; i++)
		{
			Fill(Circle(Vector2.Lerp(a, b, (float)i / (float)segments), size * 0.5f), col, size * 0.5f);
		}
	}

	private static void Fill(Rect r, Color c, float radius)
	{
		radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
		GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: true, 0f, c, 0f, radius);
	}

	private static void Line(Rect r, Color c, float width, float radius)
	{
		radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
		GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: true, 0f, c, width, radius);
	}

	private static Rect Grow(Rect r, float d)
	{
		return new Rect(r.x - d, r.y - d, r.width + d * 2f, r.height + d * 2f);
	}

	private static Rect Circle(Vector2 c, float r)
	{
		return new Rect(c.x - r, c.y - r, r * 2f, r * 2f);
	}

	private static float TextW(GUIStyle s, string t)
	{
		return s.CalcSize(new GUIContent(t)).x;
	}

	private void DrawPlayerPin(Vector3 position, Vector2 point, Rect img, float radius)
	{
		Vector3 vector = new Vector3(Mathf.Sin(player.yaw), 0f, Mathf.Cos(player.yaw));
		Vector2 vector2 = WorldToMapScreen(position + vector, img) - point;
		Matrix4x4 matrix = GUI.matrix;
		GUIUtility.RotateAroundPivot(Mathf.Atan2(vector2.x, 0f - vector2.y) * 57.29578f, point);
		Fill(new Rect(point.x - U(3.2f), point.y - radius - U(10.5f), U(6.4f), U(13f)), AtlasInk, U(3.2f));
		Fill(new Rect(point.x - U(1.8f), point.y - radius - U(9f), U(3.6f), U(11.5f)), AtlasBlue, U(1.8f));
		GUI.matrix = matrix;
		DrawPlayerMedallion(point, radius);
	}

	private void DrawPlayerMedallion(Vector2 point, float radius)
	{
		float num = radius - U(1.5f);
		Fill(Circle(point, radius), AtlasInk, radius);
		Fill(Circle(point, num), AtlasParchment, num);
		if (playerPortrait != null)
		{
			float r = radius - U(2.5f);
			GUI.DrawTexture(Circle(point, r), playerPortrait, ScaleMode.ScaleToFit, alphaBlend: true);
		}
		else
		{
			float num2 = radius - U(3.5f);
			Fill(Circle(point, num2), AtlasBlue, num2);
		}
	}

	private string GoalText()
	{
		if (ui == null)
		{
			return "";
		}
		string goalText = ui.GoalText;
		if (!string.IsNullOrEmpty(goalText))
		{
			return goalText;
		}
		return "自由逛逛 · 靠近发光的东西按 E";
	}

	private GUIStyle AtlasStyle(float size, Color color, bool bold, TextAnchor anchor)
	{
		return new GUIStyle(GUI.skin.label)
		{
			fontSize = Mathf.Max(9, Mathf.RoundToInt(size * uiK)),
			fontStyle = (bold ? FontStyle.Bold : FontStyle.Normal),
			alignment = anchor,
			wordWrap = false,
			clipping = TextClipping.Overflow,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			font = TideTownStyleUI.UiFont,
			normal = 
			{
				textColor = color
			}
		};
	}
}
