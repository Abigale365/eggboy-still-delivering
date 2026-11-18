using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FeelDemo : MonoBehaviour
{
	[Serializable]
	private class Spot
	{
		public string name;

		public float x;

		public float z;

		public float yaw;

		public float pitch = 14f;

		public float fov = 55f;

		public int water = -1;

		public float[] cam;

		public float[] euler;
	}

	[Serializable]
	private class SpotList
	{
		public Spot[] spots;
	}

	[Serializable]
	private class MapAlignmentCheck
	{
		public string site;

		public Vector2 cameraPixel;

		public Vector2 playerPixel;

		public float pixelError;

		public bool opened;

		public bool walkable;
	}

	[Serializable]
	private class MapAlignmentReport
	{
		public List<MapAlignmentCheck> points = new List<MapAlignmentCheck>();
	}

	private sealed class JumpFixture : IEggboyWorld
	{
		public float floor = 20f;

		public IReadOnlyList<EggboyCollider> Colliders => Array.Empty<EggboyCollider>();

		public Vector3 MailboxPos => Vector3.zero;

		public Vector3 SpawnPos => new Vector3(0f, floor, 0f);

		public void QueryColliders(float x, float z, List<EggboyCollider> results)
		{
			results.Clear();
		}

		public bool Walkable(float x, float z)
		{
			return true;
		}

		public float GroundAt(float x, float z)
		{
			return floor;
		}

		public float GroundAt(float x, float z, float aboveY)
		{
			return floor;
		}
	}

	[Serializable]
	private sealed class JumpReport
	{
		public string fixture = "real EggboyPlayer.Move, isolated flat/drop floor, dt=0.01s";

		public int bufferedSuccess;

		public int coyoteSuccess;

		public int expiredUnexpected;

		public List<string> trials = new List<string>();
	}

	[Serializable]
	private sealed class CameraWaypoint
	{
		public string id;

		public float x;

		public float z;

		public bool stop;
	}

	[Serializable]
	private sealed class CameraRouteData
	{
		public CameraWaypoint[] route;
	}

	[Serializable]
	private sealed class CameraSample
	{
		public int routeIndex;

		public int yawStep;

		public int referencePixels;

		public int visiblePixels;

		public Vector3 player;

		public Vector3 camera;

		public float visibility;

		public bool inside;
	}

	[Serializable]
	private sealed class CameraReport
	{
		public int sampledFrames;

		public int lowVisibilityFrames;

		public int insideSolidFrames;

		public int reachedWaypoints;

		public int plannedWaypoints;

		public float lowVisibilityFraction;

		public string sampling = "moving: every 10 frames; six fixed stops: all 12 headings at 30 degrees; original scene shaders retained";

		public List<CameraSample> samples = new List<CameraSample>();
	}

	[Serializable]
	private class RockNode
	{
		public float x;

		public float z;

		public float y;

		public bool lid;
	}

	[Serializable]
	private class RockNodeList
	{
		public RockNode[] nodes;
	}

	[Serializable]
	private class WalkItem
	{
		public string name;

		public float x;

		public float y;

		public float z;

		public float tx;

		public float ty;

		public float tz;
	}

	[Serializable]
	private class WalkList
	{
		public WalkItem[] items;
	}

	private string mode;

	private EggboyPlayer player;

	private IslandWorld world;

	private Camera cam;

	private RenderTexture rt;

	private Texture2D grab;

	private string dir;

	private int frame;

	private int shot;

	private float maxDepth;

	private float maxTrail;

	private float maxSpring;

	private int bushFrames;

	private int stuckPhases;

	private readonly List<string> phases = new List<string>();

	private readonly List<EggboyCollider> near = new List<EggboyCollider>();

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		MainSceneGate.WhenMainLoaded(BootNow);
	}

	private static void BootNow()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-feeldemo");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			FeelDemo feelDemo = new GameObject("FeelDemo").AddComponent<FeelDemo>();
			feelDemo.mode = commandLineArgs[num + 1];
			UnityEngine.Object.DontDestroyOnLoad(feelDemo.gameObject);
		}
	}

	private IEnumerator Start()
	{
		Application.runInBackground = true;
		yield return new WaitForSecondsRealtime(3f);
		GameObject gameObject = GameObject.Find("EGGBOY");
		player = ((gameObject != null) ? gameObject.GetComponent<EggboyPlayer>() : null);
		world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		if (player == null || world == null)
		{
			Finish("setup_failed", "缺 EGGBOY / IslandWorld");
			yield break;
		}
		if (mode.StartsWith("playinput:"))
		{
			yield return PlayInput(mode.Substring(10));
			yield break;
		}
		PrototypeGame prototypeGame = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		if (prototypeGame != null)
		{
			prototypeGame.enabled = false;
		}
		Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].enabled = false;
		}
		SafePoints safePoints = UnityEngine.Object.FindObjectOfType<SafePoints>();
		if (safePoints != null)
		{
			safePoints.Enabled = false;
		}
		if ((mode == "beach" || mode == "beachentry") && WaterLevel.Main != null && !WaterLevel.Main.CliOverride)
		{
			WaterLevel.Main.SetInstant(WaterLevel.LevelYs[1]);
		}
		yield return null;
		Vector3 a2;
		Vector3 b2;
		string what;
		if (mode == "beachentry")
		{
			yield return BeachEntry();
		}
		else if (mode == "ferry")
		{
			yield return Ferry();
		}
		else if (mode == "jump")
		{
			yield return JumpWindows();
		}
		else if (mode == "camera")
		{
			yield return CameraRoute();
		}
		else if (mode == "map-alignment")
		{
			yield return MapAlignment();
		}
		else if (mode.StartsWith("spots:"))
		{
			yield return Spots(mode.Substring(6));
		}
		else if (mode == "dogplate")
		{
			yield return DogPlateTrace();
		}
		else if (mode == "lamptouch")
		{
			yield return LampTouch();
		}
		else if (mode == "safereturn")
		{
			yield return SafeReturnTest();
		}
		else if (mode.StartsWith("walkinto:"))
		{
			string src = mode.Substring(9);
			mode = "walkinto";
			yield return WalkInto(src);
		}
		else if (mode.StartsWith("rockdrop:"))
		{
			string src2 = mode.Substring(9);
			mode = "rockdrop";
			yield return RockDrop(src2);
		}
		else if (mode == "pickup")
		{
			yield return PickupTest();
		}
		else if (mode == "wishwheel")
		{
			yield return WishWheelTest();
		}
		else if (mode == "props")
		{
			yield return PropsTest.Run(this, player, world);
		}
		else if (mode == "bottle-bank")
		{
			yield return BottleBank();
		}
		else if (mode == "landscan")
		{
			StringBuilder stringBuilder = new StringBuilder();
			List<Vector3> list = new List<Vector3>();
			int[] array2 = new int[50];
			int num = 0;
			for (float num2 = -70f; num2 <= 70f; num2 += 0.5f)
			{
				for (float num3 = -70f; num3 <= 70f; num3 += 0.5f)
				{
					if (world.InWalkArea(num2, num3) && world.Walkable(num2, num3))
					{
						float num4 = world.GroundAt(num2, num3);
						if (!float.IsNaN(num4) && !world.IsBeachSurface(num2, num3, num4))
						{
							num++;
							array2[Mathf.Clamp(Mathf.FloorToInt(num4 / 0.1f), 0, 49)]++;
							list.Add(new Vector3(num2, num4, num3));
						}
					}
				}
			}
			list.Sort((Vector3 a, Vector3 b) => a.y.CompareTo(b.y));
			stringBuilder.Append($"walkable_non_beach={num} | bins(0.1m):");
			for (int j = 0; j < array2.Length; j++)
			{
				if (array2[j] > 0)
				{
					stringBuilder.Append($" {(float)j * 0.1f:F1}={array2[j]}");
				}
			}
			stringBuilder.Append(" | samples:");
			float num5 = -1f;
			foreach (Vector3 item in list)
			{
				float num6 = Mathf.Floor(item.y / 0.1f);
				if (num6 != num5)
				{
					num5 = num6;
					stringBuilder.Append($" {item.y:F2}@({item.x:F1},{item.z:F1})");
				}
			}
			mode = "landscan";
			Finish("passed", stringBuilder.ToString());
		}
		else if (mode.StartsWith("probe:"))
		{
			StringBuilder stringBuilder2 = new StringBuilder();
			string[] array3 = mode.Substring(6).Split(';');
			for (int i = 0; i < array3.Length; i++)
			{
				string[] array4 = array3[i].Split(',');
				if (array4.Length == 2)
				{
					float num7 = float.Parse(array4[0], CultureInfo.InvariantCulture);
					float num8 = float.Parse(array4[1], CultureInfo.InvariantCulture);
					float num9 = world.GroundAt(num7, num8);
					RaycastHit[] array5 = Physics.RaycastAll(new Vector3(num7, 200f, num8), Vector3.down, 400f, world.groundMask, QueryTriggerInteraction.Ignore);
					Array.Sort(array5, (RaycastHit h1, RaycastHit h2) => h2.point.y.CompareTo(h1.point.y));
					float num10 = world.SurfaceBelow(num7, num8, 4.3f);
					stringBuilder2.Append($"({num7:F2},{num8:F2}) ground={num9:F2} surf3={num10:F2} rockWall3={world.RockWallAt(num7, num8, 3f)} headroom={world.RockHeadroomBlocked(num7, num8, num10)} block={world.InRoamBlock(num7, num8, num10)} walkable={world.Walkable(num7, num8)} road={world.InWalkArea(num7, num8)} beach={world.IsBeachSurface(num7, num8, num9)} hits=");
					for (int k = 0; k < array5.Length && k < 4; k++)
					{
						stringBuilder2.Append($"{array5[k].point.y:F2}:{array5[k].collider.name} ");
					}
					stringBuilder2.Append("| ");
				}
			}
			mode = "probe";
			Finish("passed", stringBuilder2.ToString());
		}
		else if (!PickPath(out a2, out b2, out what))
		{
			Finish("no_path", "没找到能走的演示路线：" + mode);
		}
		else
		{
			Debug.Log($"FEELDEMO {mode} {what} from ({a2.x:F2},{a2.y:F2},{a2.z:F2}) to ({b2.x:F2},{b2.y:F2},{b2.z:F2}) water={WaterLevel.StaticY:F2}");
			SetupCamera(a2, b2);
			dir = Path.Combine(Application.dataPath, "..", "shots");
			Directory.CreateDirectory(dir);
			string[] array3 = Directory.GetFiles(dir, "feel_" + mode + "_*.png");
			for (int i = 0; i < array3.Length; i++)
			{
				File.Delete(array3[i]);
			}
			Time.captureFramerate = 30;
			Place(a2, b2);
			yield return Idle(0.6f, "start");
			yield return Go(b2, run: false, jumpMidway: false, "walk");
			yield return Idle(1.3f, "rest1");
			yield return Go(a2, run: true, jumpMidway: true, "run_jump");
			yield return Idle(1.6f, "rest2");
			if (mode == "beach")
			{
				yield return Step(0f, 0f, run: false, jump: true);
				yield return Idle(2.2f, "jump_in_place");
			}
			Finish("passed", what);
		}
	}

	private IEnumerator PlayInput(string scenario)
	{
		mode = "playinput-" + scenario;
		GameBootstrap boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
		if (boot == null || boot.ui == null || boot.state == null)
		{
			Finish("setup_failed", "找不到游戏入口");
			yield break;
		}
		Vector3 vector = new Vector3(-15f, 3f, -9.4f);
		Vector3 vector2 = Vector3.forward;
		Func<string> probe = () => "";
		boot.state.step = "B2_wood";
		boot.state.flags.Add("letters_taken");
		Func<bool> reacted;
		switch (scenario)
		{
		case "wood":
		{
			if (!WoodenMechanismPuzzleModal.DebugOpen("bakery_lock", boot.state, boot.store, player, boot.ui, boot.gate, delegate
			{
			}))
			{
				Finish("failed", "找不到面包店木匣");
				yield break;
			}
			WoodenMechanismPuzzleModal.DebugOpenAllLatches();
			int initialRing = WoodenMechanismPuzzleModal.DebugRing(0);
			reacted = () => WoodenMechanismPuzzleModal.IsOpen && WoodenMechanismPuzzleModal.DebugRing(0) != initialRing;
			break;
		}
		case "wind":
		{
			TidalWindChallenge wind = UnityEngine.Object.FindObjectOfType<TidalWindChallenge>();
			GameObject gameObject = GameObject.Find("P_22");
			if (wind == null || gameObject == null)
			{
				Finish("failed", "找不到海风步道");
				yield break;
			}
			vector = gameObject.transform.position;
			boot.state.step = "L0_cross";
			boot.state.carrying = "ball";
			reacted = () => wind.Survived > 0;
			break;
		}
		case "dog":
		{
			DogFollower dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
			GameObject gameObject3 = GameObject.Find("P_20");
			if (gameObject3 == null || dog == null)
			{
				Finish("failed", "找不到校园后院或小黑");
				yield break;
			}
			vector = gameObject3.transform.position;
			boot.state.step = "S2_ball";
			dog.ShowAt(vector + Vector3.left);
			dog.Follow();
			reacted = () => dog.LastCommand.StartsWith("sniff:");
			break;
		}
		case "word":
		{
			WordMaterializerSystem word = UnityEngine.Object.FindObjectOfType<WordMaterializerSystem>();
			reacted = () => word != null && word.LastSpawn != null && word.LastSpawn.root != null;
			probe = () => $"letter={boot.ui.LetterOpen} action={word?.LastAction}";
			break;
		}
		case "print":
		{
			PerspectivePrintSystem print = UnityEngine.Object.FindObjectOfType<PerspectivePrintSystem>();
			GameObject gameObject2 = GameObject.Find("P_FOUNTAIN");
			if (gameObject2 != null)
			{
				vector = gameObject2.transform.position + Vector3.right * 1.6f;
			}
			vector2 = Vector3.right;
			reacted = () => print != null && print.IsViewfinderActive && print.LastPrint != null;
			probe = () => $"viewfinder={print?.IsViewfinderActive} aim={print?.AimValid} scale={print?.AimScale:F2}";
			break;
		}
		case "echo":
		{
			TidalEchoCloneSystem echo = UnityEngine.Object.FindObjectOfType<TidalEchoCloneSystem>();
			reacted = () => echo != null && echo.EchoActive;
			break;
		}
		case "shadow":
		{
			ShadowLanternPuzzle shade = ShadowLanternPuzzle.Instance;
			if (shade == null || !shade.enabled)
			{
				Finish("failed", "找不到船棚石台上的布幕");
				yield break;
			}
			vector = FindFixtureStand(shade.TurntablePos, 1.28f);
			vector2 = shade.TurntablePos - vector;
			reacted = () => shade.Solved;
			probe = () => $"angle={shade.Angle:F1} near={shade.PlayerNear} pos={player.transform.position:F2}";
			break;
		}
		case "music":
		{
			SoundBottleSystem music = SoundBottleSystem.Instance;
			if (music == null)
			{
				Finish("failed", "找不到广场八音盒");
				yield break;
			}
			boot.state.flags.Add("has_sound_bottle");
			string[] order = SoundBottleSystem.Order;
			foreach (string text in order)
			{
				boot.state.flags.Add("sound_" + text);
			}
			vector = FindFixtureStand(music.MusicBoxPos, 0.95f);
			vector2 = music.MusicBoxPos - vector;
			reacted = () => music.Playing && music.MaxHouseLift > 0.1f;
			break;
		}
		default:
			Finish("failed", "未知交互 " + scenario);
			yield break;
		}
		if (scenario != "wood")
		{
			vector.y = world.GroundAt(vector.x, vector.z, vector.y + 0.5f);
			Place(vector, vector + vector2);
			ChaseCamera chaseCamera = ((Camera.main != null) ? Camera.main.GetComponent<ChaseCamera>() : null);
			if (chaseCamera != null)
			{
				chaseCamera.SnapBehind(vector, player.yaw);
			}
		}
		string folder = Path.Combine(Application.dataPath, "..");
		File.WriteAllText(Path.Combine(folder, mode + "-ready.txt"), "ready");
		float deadline = Time.realtimeSinceStartup + 18f;
		bool wrotePointer = false;
		while (Time.realtimeSinceStartup < deadline && !reacted())
		{
			if (scenario == "word" && !wrotePointer)
			{
				GameObject gameObject4 = GameObject.Find("WordSlip_wood");
				if (gameObject4 != null)
				{
					Vector2 vector3 = RectTransformUtility.WorldToScreenPoint(null, gameObject4.transform.position);
					File.WriteAllText(Path.Combine(folder, mode + "-pointer.txt"), $"{(int)vector3.x} {Screen.height - (int)vector3.y} {Screen.width} {Screen.height}");
					wrotePointer = true;
				}
			}
			yield return null;
		}
		bool passed = reacted();
		if (passed)
		{
			string dir = Path.Combine(folder, "shots");
			Directory.CreateDirectory(dir);
			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, mode + ".png"));
			yield return new WaitForSecondsRealtime(0.6f);
			shot++;
		}
		Finish(passed ? "passed" : "failed", scenario + (passed ? " 收到了键鼠输入并推进了交互 " : " 未观察到预期交互 ") + probe());
	}

	private Vector3 FindFixtureStand(Vector3 target, float radius)
	{
		for (int i = 0; i < 16; i++)
		{
			Vector3 result = target + Quaternion.Euler(0f, (float)i * 22.5f, 0f) * Vector3.forward * radius;
			if (world.Walkable(result.x, result.z) && Mathf.Abs(world.GroundAt(result.x, result.z, target.y + 0.5f) - target.y) < 0.6f)
			{
				return result;
			}
		}
		return target;
	}

	private IEnumerator BottleBank()
	{
		GameObject gameObject = GameObject.Find("TidalDriftBottle_2");
		WorldMechanismDirector ferry = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
		if (gameObject == null || ferry == null || WaterLevel.Main == null)
		{
			Finish("failed", "找不到第三只漂流瓶、绞盘或水位组件。");
			yield break;
		}
		WaterLevel.Main.SetInstant(WaterLevel.LevelYs[3]);
		Vector3 p = gameObject.transform.position;
		Light componentInChildren = gameObject.GetComponentInChildren<Light>();
		float num = world.GroundAt(p.x, p.z, p.y + 0.1f);
		bool dry = num >= WaterLevel.LevelYs[3] + 0.149f && p.y > num && world.Walkable(p.x, p.z);
		bool quiet = componentInChildren != null && componentInChildren.range <= 1f && componentInChildren.intensity <= 0.3f;
		bool clean = GameObject.Find("DynamicGroundGuide3D") == null;
		yield return null;
		cam = Camera.main;
		if (cam != null)
		{
			ChaseCamera component = cam.GetComponent<ChaseCamera>();
			if (component != null)
			{
				component.enabled = false;
			}
			cam.transform.position = p + new Vector3(0.75f, 2.15f, 2.25f);
			cam.transform.LookAt(Vector3.Lerp(p, ferry.WinchPosition, 0.21f) + Vector3.up * 0.18f);
			cam.fieldOfView = 55f;
			string text = Path.Combine(Application.dataPath, "..", "shots");
			Directory.CreateDirectory(text);
			yield return ShotTool.Capture(cam, 1920, 1080, Path.Combine(text, "bottle_bank_L3.png"), renderExplicitly: true);
			shot++;
		}
		Finish((dry && quiet && clean) ? "passed" : "failed", $"L3={WaterLevel.StaticY:F2} bottle={p:F2} winch={ferry.WinchPosition:F2} dry={dry} softLight={quiet} guideHidden={clean}");
	}

	private IEnumerator Spots(string path)
	{
		mode = "spots";
		if (!File.Exists(path))
		{
			Finish("setup_failed", "找不到机位文件 " + path);
			yield break;
		}
		SpotList spotList = JsonUtility.FromJson<SpotList>(File.ReadAllText(path));
		cam = Camera.main;
		ChaseCamera chase = ((cam != null) ? cam.GetComponent<ChaseCamera>() : null);
		if (spotList?.spots == null || chase == null)
		{
			Finish("setup_failed", "机位文件为空或缺 ChaseCamera");
			yield break;
		}
		StoryDirector storyDirector = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		if (storyDirector != null)
		{
			storyDirector.StopAllCoroutines();
			storyDirector.enabled = false;
		}
		DogFollower dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		Time.captureFramerate = 30;
		StringBuilder sb = new StringBuilder();
		Spot[] spots = spotList.spots;
		foreach (Spot s in spots)
		{
			if (s.water >= 0 && WaterLevel.Main != null)
			{
				WaterLevel.Main.SetInstant(WaterLevel.LevelYs[Mathf.Clamp(s.water, 0, WaterLevel.LevelYs.Length - 1)]);
			}
			if (s.cam != null && s.cam.Length == 3)
			{
				chase.enabled = false;
				cam.transform.SetPositionAndRotation(new Vector3(s.cam[0], s.cam[1], s.cam[2]), Quaternion.Euler((s.euler != null && s.euler.Length == 3) ? new Vector3(s.euler[0], s.euler[1], s.euler[2]) : Vector3.zero));
				cam.fieldOfView = s.fov;
				for (int i = 0; i < 3; i++)
				{
					Shader.SetGlobalVector("_TideCamFade", Vector4.zero);
					yield return null;
				}
				yield return ShotTool.Capture(cam, 1774, 887, Path.Combine(dir, "spot_" + s.name + ".png"));
				shot++;
				chase.enabled = true;
				sb.Append(s.name + ": free cam | ");
				continue;
			}
			float num = s.yaw * (MathF.PI / 180f);
			Vector3 vector = new Vector3(s.x, world.GroundAt(s.x, s.z), s.z);
			Place(vector, vector + new Vector3(Mathf.Sin(num), 0f, Mathf.Cos(num)));
			chase.SnapBehind(vector, num);
			chase.DebugOrbit(num, s.pitch);
			if (dog != null && dog.Bound)
			{
				if (!dog.Following)
				{
					dog.Follow();
				}
				Vector3 vector2 = vector - new Vector3(Mathf.Sin(num), 0f, Mathf.Cos(num)) * 1.6f;
				dog.transform.position = new Vector3(vector2.x, world.GroundAt(vector2.x, vector2.z, vector.y + 0.05f), vector2.z);
			}
			for (int i = 0; i < 50; i++)
			{
				player.locked = false;
				player.Move(1f / 30f, 0f, 0f, run: false, jumpPressed: false);
				chase.Tick(1f / 30f, player.camFocus, player.yaw, moving: false, running: false);
				chase.SetFadeActive(on: true);
				yield return null;
			}
			yield return ShotTool.Capture(cam, 1774, 887, Path.Combine(dir, "spot_" + s.name + ".png"));
			shot++;
			string text = "";
			if (dog != null)
			{
				Vector3 position = dog.transform.position;
				float num2 = float.NegativeInfinity;
				RaycastHit[] array = Physics.RaycastAll(new Vector3(position.x, position.y + 0.6f, position.z), Vector3.down, 1.2f, world.groundMask, QueryTriggerInteraction.Ignore);
				foreach (RaycastHit raycastHit in array)
				{
					num2 = Mathf.Max(num2, raycastHit.point.y);
				}
				text = (float.IsNegativeInfinity(num2) ? $" dog=({position.x:F2},{position.y:F2},{position.z:F2}) noGround" : $" dog=({position.x:F2},{position.y:F2},{position.z:F2}) sunk={num2 - position.y:F2}");
			}
			Vector3 position2 = player.transform.position;
			sb.Append($"{s.name}: hero=({position2.x:F2},{position2.y:F2},{position2.z:F2}){text} | ");
		}
		Finish("passed", sb.ToString());
	}

	private IEnumerator PickupTest()
	{
		PhysicalPickupController pk = PhysicalPickupController.Instance;
		StoryDirector director = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		GameBootstrap gameBootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
		SchoolBall ball = UnityEngine.Object.FindObjectOfType<SchoolBall>();
		EggboyUI ui = UnityEngine.Object.FindObjectOfType<EggboyUI>();
		if (pk == null || director == null || gameBootstrap == null || ball == null)
		{
			Finish("setup_failed", "缺拾取控制器 / 剧情 / 红球");
			yield break;
		}
		if (!pk.Active("pickup_ball"))
		{
			Finish("setup_failed", "拾取控制器没启用（审计模式？）");
			yield break;
		}
		PhysicalPickupController.Trace = true;
		director.StopAllCoroutines();
		director.enabled = false;
		GameState st = gameBootstrap.state;
		cam = Camera.main;
		ChaseCamera chase = ((cam != null) ? cam.GetComponent<ChaseCamera>() : null);
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		Time.captureFramerate = 30;
		StringBuilder sb = new StringBuilder();
		int pass = 0;
		int total = 0;
		Vector3 ballHome = ball.transform.position;
		string[] names = new string[6] { "front_near", "front_mid", "left_near", "left_far", "right_near", "back_turned" };
		float[] angles = new float[6] { 0f, 0f, 90f, 90f, -90f, 0f };
		float[] dists = new float[6] { 0.65f, 0.95f, 0.65f, 1.4f, 0.9f, 0.8f };
		for (int i = 0; i < names.Length; i++)
		{
			ResetBall();
			yield return null;
			Vector3 c = ball.transform.position;
			Vector3 vector = Quaternion.Euler(0f, angles[i], 0f) * Vector3.back * dists[i];
			Vector3 vector2 = new Vector3(c.x + vector.x, 0f, c.z + vector.z);
			for (int l = 0; l < 8; l++)
			{
				if (world.Walkable(vector2.x, vector2.z) && !(Clear(vector2, c.y) < 0.45f))
				{
					break;
				}
				vector2 += vector.normalized * 0.15f;
			}
			if (!world.Walkable(vector2.x, vector2.z))
			{
				string text = "ball_" + names[i];
				Vector3 vector3 = vector2;
				Check(text, ok: false, "起点不能走 " + vector3.ToString());
				continue;
			}
			vector2.y = world.GroundAt(vector2.x, vector2.z, c.y + 0.5f);
			if (names[i] == "back_turned")
			{
				Place(vector2, vector2 + (vector2 - c));
			}
			else
			{
				Place(vector2, vector2 + Quaternion.Euler(0f, 37f * (float)i, 0f) * Vector3.forward);
			}
			if (chase != null)
			{
				chase.SnapBehind(vector2, player.yaw);
			}
			yield return null;
			director.TakeBallAt();
			bool began = pk.Busy;
			float t0 = Time.time;
			bool shotTaken = false;
			while ((pk.Busy || player.ActionLocked) && Time.time - t0 < 8f)
			{
				if (!shotTaken && pk.LastEvent.StartsWith("contact"))
				{
					shotTaken = true;
					yield return Shot(names[i], c + Vector3.up * 0.25f, player.transform.eulerAngles.y - 75f);
				}
				yield return null;
			}
			Vector3 position = player.transform.position;
			float magnitude = new Vector2(position.x - c.x, position.z - c.z).magnitude;
			float num = Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, Mathf.Atan2(c.x - position.x, c.z - position.z) * 57.29578f));
			bool ok2 = began && st.flags.Contains("ball_taken") && pk.HoldsBall && pk.LastShellGap >= 0f && !pk.LastEvent.Contains("forced");
			Check("ball_" + names[i], ok2, $"began={began} stand={magnitude:F3} yawErr={num:F1} {pk.LastEvent} shellGap={pk.LastShellGap:F3} held={pk.HoldsBall}");
		}
		float worstErr = 0f;
		float worstGap = 1f;
		if (pk.HoldsBall)
		{
			Vector3 c = -player.transform.forward;
			for (int i = 0; i < 50; i++)
			{
				player.locked = false;
				player.Move(1f / 30f, c.x, c.z, i > 25, i == 20);
				yield return null;
				worstErr = Mathf.Max(worstErr, pk.ContactError);
				worstGap = Mathf.Min(worstGap, pk.LastShellGap);
				if (i == 24)
				{
					yield return Shot("carry_jump", player.transform.position + Vector3.up * 0.45f, player.transform.eulerAngles.y - 90f);
				}
			}
			for (int i = 0; i < 30; i++)
			{
				player.Move(1f / 30f, 0f, 0f, run: false, jumpPressed: false);
				yield return null;
			}
		}
		Check("carry_walk_jump", pk.HoldsBall && worstErr < 0.05f && worstGap >= 0f, $"maxPalmError={worstErr:F3} minShellGap={worstGap:F3}");
		pk.ReleaseCarryBall();
		ball.Take(player.transform);
		yield return null;
		yield return null;
		Check("reload_carry", pk.HoldsBall && Vector3.Distance(ball.transform.position, player.transform.position) < 1f, string.Format("held={0} parent={1}", pk.HoldsBall, (ball.transform.parent != null) ? ball.transform.parent.name : "none"));
		Vector3 bowl = ball.BowlPoint;
		Vector3 a = FindFree(bowl, 1.1f);
		Place(a, bowl);
		yield return null;
		typeof(StoryDirector).GetMethod("CompletePutBallInBowl", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);
		bool putBegan = pk.Busy;
		float t1 = Time.time;
		bool putShot = false;
		while ((pk.Busy || player.ActionLocked) && Time.time - t1 < 8f)
		{
			if (!putShot && pk.LastEvent.StartsWith("contact"))
			{
				putShot = true;
				yield return Shot("bowl_release", bowl + Vector3.up * 0.25f, player.transform.eulerAngles.y - 75f);
			}
			yield return null;
		}
		yield return new WaitForSeconds(0.8f);
		float num2 = Vector3.Distance(ball.transform.position, bowl);
		Check("put_in_bowl", putBegan && ball.InBowl && num2 < 0.05f && !pk.HoldsBall, $"began={putBegan} ballToBowl={num2:F3} {pk.LastEvent}");
		director.StopAllCoroutines();
		if (ui != null && ui.LetterOpen)
		{
			ui.CloseLetter();
		}
		player.locked = false;
		player.SetStoryCrouch(crouch: false);
		ResetBall();
		ball.Take(player.transform);
		st.flags.Add("ball_taken");
		st.carrying = "ball";
		Vector3 here = FindFree(ballHome, 2.5f);
		Place(here, here + Vector3.forward);
		yield return null;
		director.DropCarriedNear(here + Vector3.right * 0.8f);
		yield return new WaitForSeconds(1.8f);
		bool dropped = !pk.HoldsBall && st.carrying == "";
		director.TakeBallAt();
		yield return WaitIdle(8f);
		Check("wind_drop_repick", dropped && st.flags.Contains("ball_taken") && pk.HoldsBall && !pk.LastEvent.Contains("forced"), $"dropped={dropped} {pk.LastEvent}");
		ResetBall();
		Vector3 stall = Vector3.zero;
		Vector3 wood = Vector3.zero;
		Vector3 stove = Vector3.zero;
		Vector3 oil = Vector3.zero;
		Vector3 slipper = Vector3.zero;
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			Vector3 vector4 = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
			if (bakeryItem.id == "stall")
			{
				stall = vector4;
			}
			else if (bakeryItem.id == "wood")
			{
				wood = vector4;
			}
			else if (bakeryItem.id == "stove")
			{
				stove = vector4;
			}
		}
		LighthouseItem[] items = LighthouseData.Load().items;
		foreach (LighthouseItem lighthouseItem in items)
		{
			if (lighthouseItem.id == "oil")
			{
				oil = new Vector3(lighthouseItem.x, lighthouseItem.y, lighthouseItem.z);
			}
		}
		SchoolItem[] items2 = SchoolData.Load().items;
		foreach (SchoolItem schoolItem in items2)
		{
			if (schoolItem.id == "slipper")
			{
				slipper = new Vector3(schoolItem.x, schoolItem.y, schoolItem.z);
			}
		}
		yield return Item("matches", stall, delegate
		{
			st.step = "B1_matches";
			st.flags.Remove("matches_taken");
			st.carrying = "";
		}, delegate
		{
			director.TakeMatchesAt(stall);
		}, () => st.flags.Contains("matches_taken"));
		yield return Item("wood", wood, delegate
		{
			st.step = "B2_wood";
			st.flags.Remove("wood_loaded");
		}, delegate
		{
			director.TakeWoodAt(wood);
		}, () => st.carrying == "wood");
		yield return Item("load_wood", stove, delegate
		{
		}, delegate
		{
			director.LoadWoodAt(stove);
		}, () => st.flags.Contains("wood_loaded"));
		yield return Item("oil", oil, delegate
		{
			st.step = "L1_shed";
			st.flags.Remove("oil_taken");
			st.carrying = "";
		}, delegate
		{
			director.TakeOilAt(oil);
		}, () => st.carrying == "oil");
		yield return Item("slipper", slipper, delegate
		{
			st.step = "S1_slipper";
			st.flags.Remove("slipper_seen");
		}, delegate
		{
			director.InspectSlipperAt(slipper);
		}, () => st.flags.Contains("slipper_seen"));
		Finish((pass == total) ? "passed" : "failed", $"{pass}/{total} | " + sb);
		void Check(string name, bool ok, string note)
		{
			total++;
			if (ok)
			{
				pass++;
			}
			sb.Append(name + "=" + (ok ? "ok" : "FAIL") + " " + note + " | ");
			Debug.Log($"PICKUPTEST {name}={ok} {note}");
		}
		IEnumerator Item(string name, Vector3 at, Action prepare, Action call, Func<bool> done)
		{
			prepare();
			Vector3 vector5 = FindFree(at, 1.3f);
			Place(vector5, vector5 + Vector3.left);
			yield return null;
			call();
			bool b = pk.Busy;
			float t2 = Time.time;
			bool sh = false;
			while ((pk.Busy || player.ActionLocked) && Time.time - t2 < 8f)
			{
				if (!sh && pk.LastEvent.StartsWith("contact"))
				{
					sh = true;
					yield return Shot(name, at + Vector3.up * 0.4f, player.transform.eulerAngles.y - 75f);
				}
				yield return null;
			}
			Vector3 position2 = player.transform.position;
			Check(name, b && done() && !pk.LastEvent.Contains("forced"), $"began={b} toTarget={new Vector2(position2.x - at.x, position2.z - at.z).magnitude:F2} {pk.LastEvent}");
			director.StopAllCoroutines();
			if (ui != null && ui.LetterOpen)
			{
				ui.CloseLetter();
			}
			player.locked = false;
		}
		void ResetBall()
		{
			pk.ReleaseCarryBall();
			ball.ReturnToSwing();
			st.flags.Remove("ball_taken");
			st.flags.Remove("ball_in_bowl");
			st.carrying = "";
			st.step = "S2_ball";
			if (!st.flags.Contains("slipper_seen"))
			{
				st.flags.Add("slipper_seen");
			}
			player.SetCarryPose(EggboyPlayer.CarryPose.None);
		}
		IEnumerator Shot(string name, Vector3 focus, float yawDeg)
		{
			if (!(cam == null))
			{
				bool chaseOn = chase != null && chase.enabled;
				if (chase != null)
				{
					chase.enabled = false;
				}
				Quaternion quaternion = Quaternion.Euler(34f, yawDeg, 0f);
				cam.transform.SetPositionAndRotation(focus - quaternion * Vector3.forward * 1.6f, quaternion);
				cam.fieldOfView = 45f;
				yield return ShotTool.Capture(cam, 1280, 720, Path.Combine(dir, "pickup_" + name + ".png"));
				shot++;
				if (chase != null)
				{
					chase.enabled = chaseOn;
				}
			}
		}
		IEnumerator WaitIdle(float max)
		{
			float t3 = Time.time;
			yield return null;
			while ((pk.Busy || player.ActionLocked) && Time.time - t3 < max)
			{
				if (chase != null)
				{
					chase.Tick(1f / 30f, player.camFocus, player.yaw, moving: false, running: false);
				}
				yield return null;
			}
		}
	}

	private Vector3 FindFree(Vector3 c, float r)
	{
		for (float num = r; num <= r + 1.5f; num += 0.25f)
		{
			for (int i = 0; i < 24; i++)
			{
				float f = (float)i * MathF.PI / 12f;
				Vector3 vector = c + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
				if (world.Walkable(vector.x, vector.z))
				{
					vector.y = world.GroundAt(vector.x, vector.z, c.y + 0.6f);
					if (Mathf.Abs(vector.y - c.y) < 0.6f && Clear(vector, vector.y) >= 0.45f)
					{
						return vector;
					}
				}
			}
		}
		return c;
	}

	private float Clear(Vector3 p, float ground)
	{
		List<EggboyCollider> list = new List<EggboyCollider>();
		world.QueryColliders(p.x, p.z, list);
		float num = 9f;
		foreach (EggboyCollider item in list)
		{
			if (!item.water && (!item.top.HasValue || !(item.top.Value - ground <= 0.22f)))
			{
				if (item.r.HasValue)
				{
					num = Mathf.Min(num, new Vector2(p.x - item.x, p.z - item.z).magnitude - item.r.Value);
				}
				else if (item.hw.HasValue && item.hd.HasValue)
				{
					num = Mathf.Min(num, Mathf.Max(Mathf.Abs(p.x - item.x) - item.hw.Value, Mathf.Abs(p.z - item.z) - item.hd.Value));
				}
			}
		}
		return num;
	}

	private IEnumerator DogPlateTrace()
	{
		DogPawPlate plate = DogPawPlate.Instance;
		DogFollower dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		GameState gameState = UnityEngine.Object.FindObjectOfType<GameBootstrap>()?.state;
		if (plate == null || dog == null)
		{
			Finish("setup_failed", "no plate/dog");
			yield break;
		}
		if (gameState != null)
		{
			gameState.step = "L1_shed";
		}
		WaterLevel.Main?.SetInstant(WaterLevel.LevelYs[2]);
		Vector3 at = plate.Center;
		for (float num = 2.5f; num <= 4f; num += 0.5f)
		{
			if (!(at == plate.Center))
			{
				break;
			}
			for (int j = 0; j < 16; j++)
			{
				float f = (float)j * MathF.PI / 8f;
				Vector3 vector = plate.Center + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
				if (world.Walkable(vector.x, vector.z))
				{
					at = vector;
					break;
				}
			}
		}
		at.y = world.GroundAt(at.x, at.z);
		Place(at, plate.Center);
		dog.Follow();
		yield return new WaitForSeconds(0.8f);
		dog.CommandOrPet();
		StringBuilder sb = new StringBuilder($"plate=({plate.Center.x:F2},{plate.Center.y:F2},{plate.Center.z:F2}) hero=({at.x:F2},{at.y:F2},{at.z:F2}) cmd={dog.LastCommand} | ");
		for (int i = 0; i < 32; i++)
		{
			if (plate.Done)
			{
				break;
			}
			Vector3 position = dog.transform.position;
			sb.Append($"{(float)i * 0.25f:F2}s ({position.x:F2},{position.y:F2},{position.z:F2}) w={world.Walkable(position.x, position.z)} g={world.GroundAt(position.x, position.z):F2} guide={dog.Guiding} | ");
			yield return new WaitForSeconds(0.25f);
		}
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		cam = Camera.main;
		Finish(plate.Done ? "passed" : "failed", sb.ToString());
	}

	private IEnumerator MapAlignment()
	{
		TownMap map = UnityEngine.Object.FindObjectOfType<TownMap>();
		dir = Path.Combine(Application.dataPath, "..", "shots");
		string path = Path.Combine(dir, "mapbase_points.json");
		if (map == null || !map.HasOrthographicMap || !File.Exists(path))
		{
			Finish("setup_failed", "需要已导入实拍底图和 map_bounds 的预览包，以及 shots/mapbase_points.json");
			yield break;
		}
		map.UseOrthographicMapForAudit();
		ShotTool.MapCalibration mapCalibration = JsonUtility.FromJson<ShotTool.MapCalibration>(File.ReadAllText(path));
		if (mapCalibration == null || mapCalibration.points == null || mapCalibration.points.Count != 4)
		{
			Finish("setup_failed", "mapbase_points 必须包含四处校准点");
			yield break;
		}
		StoryDirector storyDirector = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		if (storyDirector != null)
		{
			storyDirector.enabled = false;
		}
		MapAlignmentReport report = new MapAlignmentReport();
		bool passed = true;
		foreach (ShotTool.MapPointCapture site in mapCalibration.points)
		{
			Vector3 position = site.position;
			position.y = world.GroundAt(position.x, position.z);
			bool walkable = world.Walkable(position.x, position.z);
			Place(position, position + Vector3.forward);
			yield return null;
			map.Toggle();
			yield return new WaitForSecondsRealtime(0.25f);
			Vector2 vector = map.ProjectUV(player.transform.position);
			float num = Vector2.Distance(vector, site.pixel);
			bool isOpen = map.IsOpen;
			passed = passed && walkable && isOpen && num <= 1f;
			report.points.Add(new MapAlignmentCheck
			{
				site = site.site,
				cameraPixel = site.pixel,
				playerPixel = vector,
				pixelError = num,
				opened = isOpen,
				walkable = walkable
			});
			if (isOpen)
			{
				yield return new WaitForEndOfFrame();
				ScreenCapture.CaptureScreenshot(Path.Combine(dir, "map_alignment_" + site.site + ".png"));
				yield return new WaitForSecondsRealtime(0.6f);
				shot++;
				map.Close();
			}
		}
		Finish(passed ? "passed" : "failed", "four in-game map pins versus the orthographic camera projection", JsonUtility.ToJson(report));
	}

	private void ResetJumpTrial(float y, bool grounded)
	{
		player.ResetJumpInput();
		player.locked = false;
		player.speedScale = 1f;
		player.transform.position = new Vector3(0f, y, 0f);
		player.vel = Vector3.zero;
		player.grounded = grounded;
		player.jump = new JumpState();
		player.jump.phase = ((!grounded) ? JumpPhase.air : JumpPhase.none);
	}

	private IEnumerator JumpWindows()
	{
		IEggboyWorld originalWorld = player.world;
		Vector3 originalPosition = player.transform.position;
		JumpFixture fixture = new JumpFixture();
		JumpReport report = new JumpReport();
		while (player.ActionLocked)
		{
			yield return null;
		}
		player.world = fixture;
		try
		{
			for (int trial3 = 0; trial3 < 10; trial3++)
			{
				float y = 20.5f + (float)trial3 * 0.03f;
				fixture.floor = 20f;
				ResetJumpTrial(y, grounded: false);
				int num = 0;
				while (!player.grounded && num < 150)
				{
					player.Move(0.01f, 0f, 0f, run: false, jumpPressed: false);
					num++;
				}
				ResetJumpTrial(y, grounded: false);
				bool flag = false;
				for (int i = 1; i < num + 60; i++)
				{
					player.Move(0.01f, 0f, 0f, run: false, i == num - 8);
					frame++;
					if (player.jump.phase == JumpPhase.air && player.jump.vy > 0f)
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					report.bufferedSuccess++;
				}
				report.trials.Add($"buffer_{trial3}: press 0.08s before measured landing, success={flag}");
				yield return null;
			}
			int[] array = new int[2] { 6, 20 };
			foreach (int delay in array)
			{
				for (int trial = 0; trial < 10; trial++)
				{
					fixture.floor = 20f;
					ResetJumpTrial(20f, grounded: true);
					player.Move(0.01f, 0f, 0f, run: false, jumpPressed: false);
					fixture.floor = 18f;
					player.Move(0.01f, 0f, 0f, run: false, jumpPressed: false);
					bool flag2 = false;
					for (int j = 1; j <= delay + 19; j++)
					{
						player.Move(0.01f, 0f, 0f, run: false, j == delay);
						frame++;
						if (player.jump.phase == JumpPhase.air && player.jump.vy > 0f)
						{
							flag2 = true;
							break;
						}
					}
					if (flag2)
					{
						if (delay == 6)
						{
							report.coyoteSuccess++;
						}
						else
						{
							report.expiredUnexpected++;
						}
					}
					report.trials.Add($"coyote_{delay * 10}ms_{trial}: success={flag2}");
					yield return null;
				}
			}
		}
		finally
		{
			FeelDemo feelDemo = this;
			feelDemo.player.world = originalWorld;
			feelDemo.Place(originalPosition, originalPosition + Vector3.forward);
		}
		bool flag3 = report.bufferedSuccess == 10 && report.coyoteSuccess == 10 && report.expiredUnexpected == 0;
		Finish(flag3 ? "passed" : "failed", "jump input windows; scene route regression remains separate", JsonUtility.ToJson(report));
	}

	private IEnumerator CameraRoute()
	{
		cam = Camera.main;
		ChaseCamera chase = ((cam != null) ? cam.GetComponent<ChaseCamera>() : null);
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		CameraWaypoint[] route = ((!(textAsset != null)) ? null : JsonUtility.FromJson<CameraRouteData>(textAsset.text)?.route);
		if (chase == null || route == null)
		{
			Finish("setup_failed", "缺 ChaseCamera / World/route");
			yield break;
		}
		int end = Array.FindIndex(route, (CameraWaypoint p) => p.id == "25");
		if (end < 0)
		{
			Finish("setup_failed", "标准路线缺船棚站点 25");
			yield break;
		}
		WaterLevel.Main?.SetInstant(WaterLevel.LevelYs[0]);
		StoryDirector storyDirector = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		if (storyDirector != null)
		{
			storyDirector.StopAllCoroutines();
			storyDirector.enabled = false;
		}
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		Time.captureFramerate = 30;
		chase.enabled = true;
		chase.extraBlock = world.CameraBlockDistance;
		int market = Array.FindIndex(route, (CameraWaypoint p) => p.id == "13");
		int townReturn = Array.FindIndex(route, market + 1, (CameraWaypoint p) => p.z > -2f && p.z < 0f);
		if (market < 0 || townReturn <= market || townReturn > end)
		{
			Finish("setup_failed", "标准路线缺集市出口");
			yield break;
		}
		CameraReport report = new CameraReport
		{
			plannedWaypoints = end + 1 - (townReturn - market - 1)
		};
		Vector3 vector = new Vector3(route[0].x, world.GroundAt(route[0].x, route[0].z), route[0].z);
		Place(vector, vector + Vector3.forward);
		chase.SnapBehind(vector, 0f);
		int yawStep = 0;
		HashSet<string> photographed = new HashSet<string>();
		string[] stops = new string[6] { "07", "03", "10", "13", "21", "25" };
		string[] names = new string[6] { "story_wake", "mailbox", "bakery", "market", "school_backyard", "boathouse" };
		using (ShotTool.HeroVisibilityProbe probe = new ShotTool.HeroVisibilityProbe())
		{
			for (int index = 0; index <= end; index++)
			{
				if (index > market && index < townReturn)
				{
					continue;
				}
				CameraWaypoint waypoint = route[index];
				Vector2 goal = new Vector2(waypoint.x, waypoint.z);
				int stagnant = 0;
				float best = float.MaxValue;
				bool reached = false;
				for (int tick2 = 0; tick2 < 2400; tick2++)
				{
					Vector3 position = player.transform.position;
					Vector2 vector2 = goal - new Vector2(position.x, position.z);
					if (vector2.magnitude < (waypoint.stop ? 0.5f : 0.35f))
					{
						reached = true;
						break;
					}
					if (vector2.magnitude < best - 0.004f)
					{
						best = vector2.magnitude;
						stagnant = 0;
					}
					else
					{
						int num = stagnant + 1;
						stagnant = num;
						if (num > 100)
						{
							break;
						}
					}
					vector2.Normalize();
					player.locked = false;
					player.Move(1f / 30f, vector2.x, vector2.y, run: true, jumpPressed: false);
					frame++;
					chase.Tick(1f / 30f, player.camFocus, player.yaw, moving: true, running: true);
					chase.SetFadeActive(on: true);
					yield return null;
					if (tick2 % 10 == 0)
					{
						CameraMeasure(probe, report, index, yawStep);
						yawStep = (yawStep + 1) % 12;
						chase.DebugOrbit((float)yawStep * 30f * (MathF.PI / 180f));
					}
				}
				if (!reached)
				{
					stuckPhases++;
					Debug.LogWarning($"CAMERA_ROUTE blocked at waypoint {index} {waypoint.id}: {player.transform.position} -> ({waypoint.x}, {waypoint.z})");
					break;
				}
				report.reachedWaypoints++;
				int num2 = Array.IndexOf(stops, waypoint.id);
				if (num2 >= 0 && photographed.Add(waypoint.id))
				{
					chase.SnapBehind(player.transform.position, 0f);
					chase.DebugOrbit(0f);
					for (int i = 0; i < 30; i++)
					{
						chase.Tick(1f / 30f, player.transform.position, player.yaw, moving: false, running: false);
					}
					chase.SetFadeActive(on: true);
					yield return ShotTool.Capture(cam, 1774, 887, Path.Combine(dir, "feel_camera_" + names[num2] + ".png"));
					shot++;
					for (int tick2 = 0; tick2 < 12; tick2++)
					{
						chase.DebugOrbit((float)tick2 * 30f * (MathF.PI / 180f));
						chase.Tick(1f / 30f, player.transform.position, player.yaw, moving: false, running: false);
						chase.SetFadeActive(on: true);
						CameraMeasure(probe, report, index, tick2);
						yield return null;
					}
				}
			}
		}
		report.lowVisibilityFraction = ((report.sampledFrames > 0) ? ((float)report.lowVisibilityFrames / (float)report.sampledFrames) : 1f);
		bool flag = report.reachedWaypoints == report.plannedWaypoints && photographed.Count == 6 && report.lowVisibilityFraction <= 0.02f && report.insideSolidFrames == 0;
		Finish(flag ? "passed" : "failed", "camera visibility and collision; performance must be measured separately without readback", JsonUtility.ToJson(report));
	}

	private void CameraMeasure(ShotTool.HeroVisibilityProbe probe, CameraReport report, int routeIndex, int yawStep)
	{
		Vector3 position = player.transform.position;
		Shader.SetGlobalVector("_TidePlayerPos", new Vector4(position.x, position.y, position.z, 1f));
		float num = probe.Sample(cam);
		bool flag = world.CameraPointBlocked(cam.transform.position);
		Collider[] array = Physics.OverlapSphere(cam.transform.position, 0.005f, -5, QueryTriggerInteraction.Ignore);
		foreach (Collider collider in array)
		{
			if (collider is BoxCollider && (collider.ClosestPoint(cam.transform.position) - cam.transform.position).sqrMagnitude < 1E-06f)
			{
				flag = true;
			}
		}
		report.sampledFrames++;
		if (num < 0.6f)
		{
			report.lowVisibilityFrames++;
		}
		if (flag)
		{
			report.insideSolidFrames++;
		}
		report.samples.Add(new CameraSample
		{
			routeIndex = routeIndex,
			yawStep = yawStep,
			referencePixels = probe.ReferencePixels,
			visiblePixels = probe.VisiblePixels,
			player = position,
			camera = cam.transform.position,
			visibility = num,
			inside = flag
		});
	}

	private IEnumerator Ferry()
	{
		WaterLevel water = WaterLevel.Main;
		WorldMechanismDirector mech = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
		List<FloatingCrate> crates = new List<FloatingCrate>(UnityEngine.Object.FindObjectsOfType<FloatingCrate>());
		crates.Sort((FloatingCrate p, FloatingCrate q) => CrateNo(p.name).CompareTo(CrateNo(q.name)));
		if (water == null || mech == null || crates.Count < 5)
		{
			Finish("failed", "缺 WaterLevel / WorldMechanismDirector，或浮箱少于 5 个：" + crates.Count);
			yield break;
		}
		StringBuilder sb = new StringBuilder();
		int bad = 0;
		water.SetInstant(WaterLevel.LevelYs[1]);
		for (int i = 0; i < 4; i++)
		{
			yield return null;
		}
		int num = 0;
		foreach (FloatingCrate item2 in crates)
		{
			if (world.Walkable(item2.transform.position.x, item2.transform.position.z))
			{
				num++;
			}
		}
		Check("L1_road_dry", num == crates.Count, $"{num}/{crates.Count} 个箱位能走");
		water.SetInstant(WaterLevel.LevelYs[2]);
		for (int i = 0; i < 6; i++)
		{
			yield return null;
		}
		Physics.SyncTransforms();
		float w = water.Y;
		float limit = w - 0.05f;
		int g0 = crates.Count / 2 - 1;
		int g1 = crates.Count / 2 + 1;
		StringBuilder stringBuilder = new StringBuilder();
		float num2 = -99f;
		string text = "";
		for (int l = 0; l < crates.Count; l++)
		{
			string what2;
			float num3 = Floor(crates[l].transform.position, out what2);
			stringBuilder.Append(F(num3)).Append((l >= g0 && l <= g1) ? "* " : " ");
			if (num3 > num2)
			{
				num2 = num3;
				text = what2;
			}
		}
		Check("L2_bed_flooded", num2 < limit - 0.01f, $"箱位地面 {stringBuilder}（* 是缺口）最高 {F(num2)} 来自 {text}，要低于 {F(limit)}；箱子记的河床 {F(crates[0].bedY)}");
		Vector3 approach = crates[g0 - 1].transform.position;
		approach.y = world.GroundAt(approach.x, approach.z);
		Vector3 position = crates[g0].transform.position;
		Vector3 heading = new Vector3(position.x - approach.x, 0f, position.z - approach.z);
		float gapStart = heading.magnitude;
		heading.Normalize();
		int blocked = 0;
		int runtimeBlocked = 0;
		for (int m = g0; m <= g1; m++)
		{
			Vector3 position2 = crates[m].transform.position;
			if (!world.Walkable(position2.x, position2.z))
			{
				blocked++;
			}
			if (!world.CanTraverse(approach, position2.x, position2.z, jumping: false))
			{
				runtimeBlocked++;
			}
		}
		Place(approach, approach + heading);
		player.locked = false;
		float farthest = 0f;
		float lowestSupport = approach.y;
		for (int i = 0; i < 90; i++)
		{
			player.Move(1f / 30f, heading.x, heading.z, run: true, jumpPressed: false);
			Vector3 position3 = player.transform.position;
			farthest = Mathf.Max(farthest, Vector3.Dot(position3 - approach, heading));
			float num4 = world.SurfaceBelow(position3.x, position3.z, position3.y + 0.15f);
			lowestSupport = (float.IsNaN(num4) ? float.NegativeInfinity : Mathf.Min(lowestSupport, num4));
			if (i % 6 == 0)
			{
				yield return null;
			}
		}
		int num5 = g1 - g0 + 1;
		bool flag = gapStart > 0.15f && farthest < gapStart && lowestSupport >= limit && player.transform.position.y >= limit;
		Check("L2_gap_blocks", mech.GapActive && world.FreeRoam && blocked == num5 && runtimeBlocked == num5 && flag, $"缺口 {blocked}/{num5}，漫游判定 {runtimeBlocked}/{num5}，实走 {F(farthest)}m/首个缺失箱位 {F(gapStart)}m，最低支撑面 {F(lowestSupport)}m/涉水线 {F(limit)}m，gapActive={mech.GapActive}");
		int num6 = 0;
		for (int n = 0; n < crates.Count; n++)
		{
			if (n < g0 || n > g1)
			{
				Vector3 position4 = crates[n].transform.position;
				if (world.Walkable(position4.x, position4.z) && Mathf.Abs(world.GroundAt(position4.x, position4.z) - (w + 0.18f)) < 0.08f)
				{
					num6++;
				}
			}
		}
		Check("L2_crates_stand", num6 == crates.Count - (g1 - g0 + 1), $"{num6}/{crates.Count - (g1 - g0 + 1)} 只箱子浮在水面上能踩");
		Vector3 position5 = crates[0].transform.position;
		Vector3 position6 = crates[crates.Count - 1].transform.position;
		Vector3 vector = position6 - position5;
		vector.y = 0f;
		vector.Normalize();
		Vector3 vector2 = position5 - vector * 1.3f;
		Vector3 vector3 = position6 + vector * 1.3f;
		float num7 = world.GroundAt(vector2.x, vector2.z);
		float num8 = world.GroundAt(vector3.x, vector3.z);
		Check("L2_banks_dry", world.Walkable(vector2.x, vector2.z) && world.Walkable(vector3.x, vector3.z) && num7 >= limit && num8 >= limit, "来路岸 (" + F(vector2.x) + "," + F(vector2.z) + ") 高 " + F(num7) + "，对岸 (" + F(vector3.x) + "," + F(vector3.z) + ") 高 " + F(num8));
		Vector3 winchPosition = mech.WinchPosition;
		float num9 = world.GroundAt(winchPosition.x, winchPosition.z, winchPosition.y + 1f);
		Check("L2_winch_dry", num9 >= w + 0.15f, "绞盘 (" + F(winchPosition.x) + "," + F(winchPosition.z) + ") 地面 " + F(num9));
		if (GameObject.Find("SchoolLawnAndWinchQuay") != null)
		{
			Vector3[,] array = new Vector3[51, 45];
			bool[,] array2 = new bool[51, 45];
			Queue<Vector2Int> queue = new Queue<Vector2Int>();
			int num10 = 0;
			for (int num11 = 0; num11 < 45; num11++)
			{
				for (int num12 = 0; num12 < 51; num12++)
				{
					Vector3 vector4 = new Vector3(12.1f + (float)num12 * 0.1f, 0f, -14.88f + (float)num11 * 0.1f);
					vector4.y = Floor(vector4, out var _);
					if (!(vector4.y < w + 0.045f) && !(vector4.y > 1.64f) && player.HasStandingClearance(vector4))
					{
						array[num12, num11] = vector4;
						if (vector4.x >= 16.4f)
						{
							num10++;
						}
						if (!(vector4.x < 12.6f) && !(vector4.x > 14.15f) && !(vector4.z < -13.56f) && !(vector4.z > -12.62f))
						{
							queue.Enqueue(new Vector2Int(num12, num11));
							array2[num12, num11] = true;
						}
					}
				}
			}
			int count = queue.Count;
			bool flag2 = false;
			Vector2Int[] array3 = new Vector2Int[4]
			{
				Vector2Int.left,
				Vector2Int.right,
				Vector2Int.up,
				Vector2Int.down
			};
			while (!flag2 && queue.Count > 0)
			{
				Vector2Int vector2Int = queue.Dequeue();
				Vector3 from = array[vector2Int.x, vector2Int.y];
				if (from.x >= 16.4f)
				{
					flag2 = true;
					break;
				}
				Vector2Int[] array4 = array3;
				foreach (Vector2Int vector2Int2 in array4)
				{
					Vector2Int item = vector2Int + vector2Int2;
					if (item.x >= 0 && item.x < 51 && item.y >= 0 && item.y < 45 && !array2[item.x, item.y])
					{
						Vector3 vector5 = array[item.x, item.y];
						if (vector5.y != 0f && world.RoamStep(from, vector5.x, vector5.z, jumping: false))
						{
							array2[item.x, item.y] = true;
							queue.Enqueue(item);
						}
					}
				}
			}
			Check("L2_quay_wall_blocks_dry_bypass", count > 0 && num10 > 0 && !flag2, $"绞盘岸起点 {count}，船棚岸可站点 {num10}，石墙旁存在干路={flag2}");
		}
		water.SetInstant(WaterLevel.LevelYs[0]);
		Finish((bad == 0) ? "passed" : "failed", sb.ToString());
		void Check(string name, bool ok, string note)
		{
			if (!ok)
			{
				bad++;
			}
			sb.Append(name).Append(ok ? "=ok " : "=FAIL ").Append(note)
				.Append(" | ");
			Debug.Log("FERRY " + name + "=" + (ok ? "ok" : "FAIL") + " " + note);
		}
		static string F(float v)
		{
			return v.ToString("F2", CultureInfo.InvariantCulture);
		}
		float Floor(Vector3 at, out string what)
		{
			float num14 = -99f;
			what = "none";
			RaycastHit[] array5 = Physics.RaycastAll(new Vector3(at.x, 200f, at.z), Vector3.down, 400f, world.groundMask, QueryTriggerInteraction.Ignore);
			for (int num15 = 0; num15 < array5.Length; num15++)
			{
				RaycastHit raycastHit = array5[num15];
				if (!(raycastHit.collider.GetComponent<FloatingCrate>() != null) && raycastHit.point.y > num14)
				{
					num14 = raycastHit.point.y;
					what = raycastHit.collider.name;
				}
			}
			return num14;
		}
	}

	private static int CrateNo(string name)
	{
		int num = name.LastIndexOf('_');
		if (num < 0 || !int.TryParse(name.Substring(num + 1), out var result))
		{
			return 0;
		}
		return result;
	}

	private IEnumerator BeachEntry()
	{
		Time.captureFramerate = 30;
		WalkPoly beach = world.beachPoly;
		if (beach == null || beach.points == null)
		{
			Finish("no_path", "没有沙滩");
			yield break;
		}
		List<Vector2> list = new List<Vector2>();
		foreach (Vector2 item2 in Grid(beach.points, 0.5f))
		{
			if (world.Walkable(item2.x, item2.y) && world.IsBeachSurface(item2.x, item2.y, world.GroundAt(item2.x, item2.y)))
			{
				list.Add(item2);
			}
		}
		float num = float.MaxValue;
		float num2 = float.MinValue;
		float num3 = float.MaxValue;
		float num4 = float.MinValue;
		Vector2[] points = beach.points;
		for (int j = 0; j < points.Length; j++)
		{
			Vector2 vector = points[j];
			num = Mathf.Min(num, vector.x);
			num2 = Mathf.Max(num2, vector.x);
			num3 = Mathf.Min(num3, vector.y);
			num4 = Mathf.Max(num4, vector.y);
		}
		List<Vector3> starts = new List<Vector3>();
		List<Vector2> goals = new List<Vector2>();
		for (float num5 = num - 3.5f; num5 <= num2 + 3.5f; num5 += 0.5f)
		{
			for (float num6 = num3 - 3.5f; num6 <= num4 + 3.5f; num6 += 0.5f)
			{
				if (!world.Walkable(num5, num6))
				{
					continue;
				}
				float num7 = world.GroundAt(num5, num6);
				if (num7 < beach.y + 0.55f || num7 > beach.y + 4f)
				{
					continue;
				}
				float num8 = 12.25f;
				Vector2 item = Vector2.zero;
				bool flag = false;
				foreach (Vector2 item3 in list)
				{
					float num9 = (item3.x - num5) * (item3.x - num5) + (item3.y - num6) * (item3.y - num6);
					if (num9 < num8)
					{
						num8 = num9;
						item = item3;
						flag = true;
					}
				}
				if (flag)
				{
					starts.Add(new Vector3(num5, num7, num6));
					goals.Add(item);
				}
			}
		}
		SafePoints safe = UnityEngine.Object.FindObjectOfType<SafePoints>();
		Vector2 open = Vector2.zero;
		foreach (Vector2 item4 in list)
		{
			open += item4;
		}
		if (list.Count > 0)
		{
			open /= (float)list.Count;
		}
		int tried = 0;
		int landed = 0;
		int backOut = 0;
		int walkedOff = 0;
		int roamed = 0;
		StringBuilder spots = new StringBuilder();
		for (int i = 0; i < starts.Count; i += Mathf.Max(1, starts.Count / 80))
		{
			if (tried >= 80)
			{
				break;
			}
			tried++;
			Vector3 s = starts[i];
			Vector3 goal = new Vector3(goals[i].x, beach.y, goals[i].y);
			Vector3 far = goal + (goal - new Vector3(s.x, goal.y, s.z)).normalized * 1.5f;
			Place(s, goal);
			bool fell = false;
			for (int f4 = 0; f4 < 75; f4++)
			{
				if (fell)
				{
					break;
				}
				Vector3 position = player.transform.position;
				Vector2 normalized = new Vector2(far.x - position.x, far.z - position.z).normalized;
				player.locked = false;
				player.Move(1f / 30f, normalized.x, normalized.y, run: true, jumpPressed: false);
				fell = player.transform.position.y < s.y - 0.6f;
				if (f4 % 5 == 0)
				{
					yield return null;
				}
			}
			if (fell)
			{
				walkedOff++;
			}
			Place(s, goal);
			bool on = false;
			for (int f4 = 0; f4 < 150; f4++)
			{
				if (on)
				{
					break;
				}
				Vector3 position2 = player.transform.position;
				Vector2 normalized2 = new Vector2(far.x - position2.x, far.z - position2.z).normalized;
				player.locked = false;
				player.Move(1f / 30f, normalized2.x, normalized2.y, run: true, f4 == 12);
				position2 = player.transform.position;
				on = player.grounded && world.IsBeachSurface(position2.x, position2.z, position2.y);
				if (f4 % 5 == 0)
				{
					yield return null;
				}
			}
			if (!on)
			{
				continue;
			}
			landed++;
			Vector3 land = player.transform.position;
			for (int f4 = 0; f4 < 180; f4++)
			{
				Vector3 position3 = player.transform.position;
				Vector2 vector2 = new Vector2(open.x - position3.x, open.y - position3.z);
				if (vector2.magnitude < 0.6f)
				{
					break;
				}
				vector2.Normalize();
				player.locked = false;
				player.Move(1f / 30f, vector2.x, vector2.y, run: true, jumpPressed: false);
				if (f4 % 5 == 0)
				{
					yield return null;
				}
			}
			Vector3 position4 = player.transform.position;
			float magnitude = new Vector2(position4.x - land.x, position4.z - land.z).magnitude;
			if (new Vector2(open.x - position4.x, open.y - position4.z).magnitude < 1f || magnitude > 2.5f)
			{
				roamed++;
			}
			if (landed <= 14)
			{
				spots.Append((spots.Length == 0) ? "" : " ").Append($"[{s.x:F1},{s.z:F1}>{land.x:F1},{land.z:F1} walked {magnitude:F1}]");
			}
			if (safe == null)
			{
				continue;
			}
			int before = safe.RescueCount;
			safe.Enabled = true;
			for (int f4 = 0; f4 < 420; f4++)
			{
				if (safe.RescueCount != before)
				{
					break;
				}
				Vector3 position5 = player.transform.position;
				Vector2 normalized3 = new Vector2(s.x - position5.x, s.z - position5.z).normalized;
				player.locked = false;
				player.Move(1f / 30f, normalized3.x, normalized3.y, run: true, jumpPressed: false);
				yield return null;
			}
			safe.Enabled = false;
			Vector3 position6 = player.transform.position;
			if (safe.RescueCount > before && position6.y >= WaterLevel.StaticY - 0.05f && !world.IsBeachSurface(position6.x, position6.z, position6.y))
			{
				backOut++;
			}
		}
		maxDepth = Mathf.Max(0f, WaterLevel.StaticY - beach.y);
		Finish((landed > 0 && roamed > 0 && backOut == landed && walkedOff == 0) ? "passed" : "failed", $"beach y={beach.y:F2} open=({open.x:F1},{open.y:F1}) startSpots={starts.Count} tried={tried} walkedOff={walkedOff} landed={landed} roamed={roamed} backOut={backOut} launch={spots}");
	}

	private bool PickPath(out Vector3 a, out Vector3 b, out string what)
	{
		a = (b = Vector3.zero);
		what = "";
		List<Vector2> list = new List<Vector2>();
		for (int i = 0; i < 8; i++)
		{
			list.Add(new Vector2(Mathf.Cos((float)i * MathF.PI / 8f), Mathf.Sin((float)i * MathF.PI / 8f)));
		}
		if (mode == "bush")
		{
			GameObject gameObject = GameObject.Find("Bushes");
			if (gameObject == null)
			{
				return false;
			}
			float num = float.MaxValue;
			foreach (Transform item in gameObject.transform)
			{
				Vector3 position = item.position;
				float num2 = ((item.name == "LawnBed") ? 0f : 20f) + (world.InWalkArea(position.x, position.z) ? 100f : 0f);
				if (num2 >= num)
				{
					continue;
				}
				foreach (Vector2 item2 in list)
				{
					if (Clear(position, item2, 2.2f, 0f))
					{
						num = num2;
						a = At(position.x - item2.x * 2.2f, position.z - item2.y * 2.2f);
						b = At(position.x + item2.x * 2.2f, position.z + item2.y * 2.2f);
						what = item.name + "@" + position.x.ToString("F1", CultureInfo.InvariantCulture) + "," + position.z.ToString("F1", CultureInfo.InvariantCulture);
						break;
					}
				}
			}
			return num < float.MaxValue;
		}
		if (mode == "grass")
		{
			if (world.lawnPolys == null)
			{
				return false;
			}
			foreach (WalkPoly lawnPoly in world.lawnPolys)
			{
				foreach (Vector2 item3 in Grid(lawnPoly.points, 1f))
				{
					foreach (Vector2 item4 in list)
					{
						if (world.InLawnArea(item3.x, item3.y) && Clear(new Vector3(item3.x, 0f, item3.y), item4, 2.4f, 1.2f, grassOnly: true))
						{
							a = At(item3.x - item4.x * 2.4f, item3.y - item4.y * 2.4f);
							b = At(item3.x + item4.x * 2.4f, item3.y + item4.y * 2.4f);
							what = "lawn " + lawnPoly.id;
							return true;
						}
					}
				}
			}
			return false;
		}
		if (mode == "beach")
		{
			if (world.beachPoly == null || world.beachPoly.points == null)
			{
				return false;
			}
			Vector2 zero = Vector2.zero;
			Vector2[] points = world.beachPoly.points;
			foreach (Vector2 vector in points)
			{
				zero += vector;
			}
			zero /= (float)world.beachPoly.points.Length;
			float num3 = float.MaxValue;
			foreach (Vector2 item5 in Grid(world.beachPoly.points, 0.5f))
			{
				float sqrMagnitude = (item5 - zero).sqrMagnitude;
				if (sqrMagnitude >= num3)
				{
					continue;
				}
				foreach (Vector2 item6 in list)
				{
					if (Clear(new Vector3(item5.x, 0f, item5.y), item6, 2.3f, 0f))
					{
						num3 = sqrMagnitude;
						a = At(item5.x - item6.x * 2.3f, item5.y - item6.y * 2.3f);
						b = At(item5.x + item6.x * 2.3f, item5.y + item6.y * 2.3f);
						what = "beach depth=" + (WaterLevel.StaticY - a.y).ToString("F2", CultureInfo.InvariantCulture);
						break;
					}
				}
			}
			return num3 < float.MaxValue;
		}
		return false;
	}

	private Vector3 At(float x, float z)
	{
		return new Vector3(x, world.GroundAt(x, z), z);
	}

	private static IEnumerable<Vector2> Grid(Vector2[] poly, float step)
	{
		if (poly == null || poly.Length < 3)
		{
			yield break;
		}
		float num = float.MaxValue;
		float x2 = float.MinValue;
		float z2 = float.MaxValue;
		float z3 = float.MinValue;
		for (int i = 0; i < poly.Length; i++)
		{
			Vector2 vector = poly[i];
			num = Mathf.Min(num, vector.x);
			x2 = Mathf.Max(x2, vector.x);
			z2 = Mathf.Min(z2, vector.y);
			z3 = Mathf.Max(z3, vector.y);
		}
		for (float x = num + step; x < x2; x += step)
		{
			for (float z = z2 + step; z < z3; z += step)
			{
				yield return new Vector2(x, z);
			}
		}
	}

	private bool Clear(Vector3 c, Vector2 d, float half, float bushFree, bool grassOnly = false)
	{
		float num = world.GroundAt(c.x, c.z);
		for (float num2 = 0f - half; num2 <= half + 0.01f; num2 += 0.2f)
		{
			float num3 = c.x + d.x * num2;
			float num4 = c.z + d.y * num2;
			if (!world.Walkable(num3, num4))
			{
				return false;
			}
			if (grassOnly && world.InWalkArea(num3, num4))
			{
				return false;
			}
			if (Mathf.Abs(world.GroundAt(num3, num4) - num) > 0.08f)
			{
				return false;
			}
			world.QueryColliders(num3, num4, near);
			foreach (EggboyCollider item in near)
			{
				if (!item.water)
				{
					float num5 = (item.r ?? Mathf.Max(item.hw.GetValueOrDefault(), item.hd.GetValueOrDefault())) + 0.6f;
					if (new Vector2(num3 - item.x, num4 - item.z).sqrMagnitude < num5 * num5)
					{
						return false;
					}
				}
			}
		}
		if (bushFree > 0f)
		{
			GameObject gameObject = GameObject.Find("Bushes");
			if (gameObject != null)
			{
				foreach (Transform item2 in gameObject.transform)
				{
					Vector3 position = item2.position;
					float num6 = Mathf.Clamp((position.x - c.x) * d.x + (position.z - c.z) * d.y, 0f - half, half);
					if (new Vector2(position.x - c.x - d.x * num6, position.z - c.z - d.y * num6).sqrMagnitude < bushFree * bushFree)
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	private void SetupCamera(Vector3 a, Vector3 b)
	{
		Camera main = Camera.main;
		GameObject gameObject = new GameObject("FeelCam");
		cam = gameObject.AddComponent<Camera>();
		if (main != null)
		{
			cam.CopyFrom(main);
			main.enabled = false;
		}
		cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
		cam.fieldOfView = 38f;
		cam.nearClipPlane = 0.2f;
		cam.farClipPlane = 600f;
		Vector3 vector = (a + b) * 0.5f;
		Vector3 normalized = (b - a).normalized;
		Vector3 vector2 = new Vector3(0f - normalized.z, 0f, normalized.x);
		cam.fieldOfView = 46f;
		float num = ((mode == "bush") ? 3.2f : 4.2f);
		float num2 = ((mode == "bush") ? 1.5f : 2.2f);
		Vector3 worldPosition = vector + Vector3.up * 0.32f;
		List<Bounds> list = new List<Bounds>();
		MeshRenderer[] array = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
		foreach (MeshRenderer meshRenderer in array)
		{
			Bounds bounds = meshRenderer.bounds;
			if (!((bounds.center - vector).sqrMagnitude > 196f) && !(Mathf.Max(bounds.size.x, bounds.size.z) > 25f) && ((1 << meshRenderer.gameObject.layer) & world.groundMask.value) == 0 && !(meshRenderer.name == "Sea"))
			{
				bool flag = false;
				Transform parent = meshRenderer.transform;
				while (parent != null && !flag)
				{
					flag = parent.name == "GrassField" || parent.name == "EGGBOY" || parent.name == "XIAOHEI";
					parent = parent.parent;
				}
				if (!flag)
				{
					list.Add(bounds);
				}
			}
		}
		Vector3 position = vector + vector2 * num + Vector3.up * num2;
		float num3 = float.MinValue;
		for (int j = 0; j < 2; j++)
		{
			for (int k = 0; k < 8; k++)
			{
				float f = (float)k * MathF.PI / 4f;
				Vector3 vector3 = vector2 * Mathf.Cos(f) + normalized * Mathf.Sin(f);
				Vector3 vector4 = vector + vector3 * num + Vector3.up * (num2 + (float)j * 1.4f);
				float num4 = (0f - Mathf.Abs(Vector3.Dot(vector3, normalized))) * 3f - (float)j;
				if (world.GroundAt(vector4.x, vector4.z) > vector4.y - 0.4f)
				{
					num4 -= 100f;
				}
				foreach (Bounds item in list)
				{
					if (item.Contains(vector4))
					{
						num4 -= 100f;
						break;
					}
				}
				Vector3[] array2 = new Vector3[3] { a, vector, b };
				for (int i = 0; i < array2.Length; i++)
				{
					Vector3 vector5 = array2[i] + Vector3.up * 0.4f;
					if (Physics.Linecast(vector4, vector5, -1, QueryTriggerInteraction.Ignore) || Physics.Linecast(vector5, vector4, -1, QueryTriggerInteraction.Ignore))
					{
						continue;
					}
					num4 += 10f;
					float magnitude = (vector5 - vector4).magnitude;
					Ray ray = new Ray(vector4, (vector5 - vector4) / magnitude);
					foreach (Bounds item2 in list)
					{
						if (item2.IntersectRay(ray, out var distance) && distance < magnitude - 1.4f)
						{
							num4 -= 6f;
							break;
						}
					}
				}
				if (num4 > num3)
				{
					num3 = num4;
					position = vector4;
				}
			}
		}
		Debug.Log($"FEELDEMO cam ({position.x:F1},{position.y:F1},{position.z:F1}) score={num3:F1}");
		cam.transform.position = position;
		cam.transform.LookAt(worldPosition);
		rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
		{
			antiAliasing = 1
		};
		cam.allowMSAA = false;
		cam.targetTexture = rt;
		cam.enabled = true;
		grab = new Texture2D(960, 540, TextureFormat.RGB24, mipChain: false, linear: false);
	}

	private IEnumerator RockDrop(string src)
	{
		Camera cam0 = Camera.main;
		ChaseCamera chase = ((cam0 != null) ? cam0.GetComponent<ChaseCamera>() : null);
		StoryDirector storyDirector = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		if (storyDirector != null)
		{
			storyDirector.StopAllCoroutines();
			storyDirector.enabled = false;
		}
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		Time.captureFramerate = 30;
		if (!File.Exists(src))
		{
			Finish("setup_failed", "找不到站点文件 " + src);
			yield break;
		}
		RockNode[] source = JsonUtility.FromJson<RockNodeList>(File.ReadAllText(src))?.nodes ?? new RockNode[0];
		List<(Vector3 p, string kind)> pts = source.Select((RockNode n) => (p: new Vector3(n.x, n.y, n.z), kind: n.lid ? "lid" : "rock")).ToList();
		int lidN = pts.Count(((Vector3 p, string kind) q) => q.kind == "lid");
		if (pts.Count == 0)
		{
			Finish("setup_failed", "站点文件是空的");
			yield break;
		}
		int fell = 0;
		int stuck = 0;
		int i = 0;
		StringBuilder sb = new StringBuilder();
		foreach (var item in pts)
		{
			Vector3 p = item.p;
			string kind = item.kind;
			float expect = world.GroundAt(p.x, p.z, p.y + 0.05f);
			Place(p + Vector3.up * 1f, p + Vector3.forward);
			player.grounded = false;
			player.jump.phase = JumpPhase.air;
			player.jump.vy = 0f;
			for (int t3 = 0; t3 < 75; t3++)
			{
				player.locked = false;
				player.Move(1f / 30f, 0f, 0f, run: false, jumpPressed: false);
				if (chase != null)
				{
					chase.Tick(1f / 30f, player.camFocus, player.yaw, moving: false, running: false);
					chase.SetFadeActive(on: true);
				}
				yield return null;
			}
			Vector3 land = player.transform.position;
			bool fellIn = false;
			RaycastHit[] array = Physics.RaycastAll(new Vector3(land.x, land.y + 1.2f, land.z), Vector3.down, 1.1f, 256, QueryTriggerInteraction.Ignore);
			foreach (RaycastHit raycastHit in array)
			{
				if (raycastHit.point.y > land.y + 0.25f)
				{
					fellIn = true;
				}
			}
			if (i < 4 && chase != null)
			{
				yield return ShotTool.Capture(cam0, 1280, 720, Path.Combine(dir, $"rockdrop_{i}_{kind}.png"));
				shot++;
			}
			float best = 0f;
			for (int t3 = 0; t3 < 2; t3++)
			{
				if (!(best < 1f))
				{
					break;
				}
				for (int d = 0; d < 8; d++)
				{
					if (!(best < 1f))
					{
						break;
					}
					Place(land, land + Vector3.forward);
					float ang = (float)d * MathF.PI / 4f;
					for (int t = 0; t < 75; t++)
					{
						player.locked = false;
						player.Move(1f / 30f, Mathf.Cos(ang), Mathf.Sin(ang), run: false, t3 == 1 && t % 20 == 0);
						yield return null;
					}
					Vector3 position = player.transform.position;
					best = Mathf.Max(best, new Vector2(position.x - land.x, position.z - land.z).magnitude);
				}
			}
			bool flag = best < 1f;
			if (fellIn)
			{
				fell++;
			}
			if (flag)
			{
				stuck++;
			}
			string text = "";
			if (flag)
			{
				StringBuilder stringBuilder = new StringBuilder(" dirs=");
				for (int k = 0; k < 8; k++)
				{
					float f = (float)k * MathF.PI / 4f;
					float x = land.x + Mathf.Cos(f) * 0.15f;
					float z = land.z + Mathf.Sin(f) * 0.15f;
					float num = world.SurfaceBelow(x, z, land.y + 1.3f);
					stringBuilder.Append(float.IsNaN(num) ? 'N' : ((num - land.y > 0.23f) ? 'R' : ((land.y - num > 0.6f) ? 'D' : (world.InRoamBlock(x, z, num) ? 'B' : '.'))));
				}
				List<EggboyCollider> list = new List<EggboyCollider>();
				world.QueryColliders(land.x, land.z, list);
				int num2 = list.Count((EggboyCollider c) => !c.water && c.r.HasValue && c.top.HasValue && !(land.y >= c.top.Value - 0.03f) && !(c.top.Value - land.y <= 0.22f) && new Vector2(land.x - c.x, land.z - c.z).magnitude < c.r.Value + 0.3f);
				text = stringBuilder?.ToString() + $" circles={num2} free={world.FreeRoam} blk={world.InRoamBlock(land.x, land.z, land.y)}";
			}
			if (fellIn || flag || i < 6)
			{
				sb.Append(string.Format("[{0} ({1:F2},{2:F2},{3:F2}) land={4:F2} expect={5:F2} move={6:F1}{7}{8}] ", kind, p.x, p.y, p.z, land.y, expect, best, fellIn ? " FELL" : "", flag ? (" STUCK" + text) : ""));
			}
			i++;
		}
		string checks = $"{{\"lids\":{lidN},\"rocks\":{pts.Count - lidN},\"fellIn\":{fell},\"stuck\":{stuck}}}";
		Finish((fell == 0 && stuck == 0) ? "passed" : "failed", sb.ToString(), checks);
	}

	private IEnumerator LampTouch()
	{
		StoryDirector storyDirector = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		if (storyDirector != null)
		{
			storyDirector.StopAllCoroutines();
			storyDirector.enabled = false;
		}
		Time.captureFramerate = 30;
		List<Vector2> poles = new List<Vector2>();
		Transform[] array = UnityEngine.Object.FindObjectsOfType<Transform>();
		foreach (Transform transform in array)
		{
			if ((!(transform.name == "Lamp") && !(transform.name == "PierLamp")) || !transform.gameObject.activeInHierarchy)
			{
				continue;
			}
			List<EggboyCollider> list = new List<EggboyCollider>();
			world.QueryColliders(transform.position.x, transform.position.z, list);
			EggboyCollider eggboyCollider = null;
			float num = 1f;
			foreach (EggboyCollider item in list)
			{
				if (item.r.HasValue && item.r.Value < 0.15f && item.top.HasValue && item.top.Value - transform.position.y > 1.5f)
				{
					float magnitude = new Vector2(item.x - transform.position.x, item.z - transform.position.z).magnitude;
					if (magnitude < num)
					{
						num = magnitude;
						eggboyCollider = item;
					}
				}
			}
			if (eggboyCollider != null)
			{
				poles.Add(new Vector2(eggboyCollider.x, eggboyCollider.z));
			}
		}
		int tried = 0;
		int reached = 0;
		int inside = 0;
		float minAll = 9f;
		float maxAll = 0f;
		StringBuilder sb = new StringBuilder();
		foreach (Vector2 pole in poles)
		{
			float best = 9f;
			for (int d = 0; d < 4; d++)
			{
				float f = (float)d * MathF.PI / 2f;
				Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
				Vector2 vector2 = pole + vector * 1.3f;
				float gy = world.GroundAt(vector2.x, vector2.y, 9f);
				Vector3 a = new Vector3(vector2.x, gy, vector2.y);
				if (!world.RoamStandable(vector2.x, vector2.y, gy + 0.1f))
				{
					continue;
				}
				Place(a, new Vector3(pole.x, gy, pole.y));
				tried++;
				for (int i = 0; i < 75; i++)
				{
					Vector3 position = player.transform.position;
					Vector2 normalized = new Vector2(pole.x - position.x, pole.y - position.z).normalized;
					player.locked = false;
					player.Move(1f / 30f, normalized.x, normalized.y, run: false, jumpPressed: false);
					if (i % 5 == 0)
					{
						yield return null;
					}
				}
				Vector3 position2 = player.transform.position;
				float magnitude2 = new Vector2(position2.x - pole.x, position2.z - pole.y).magnitude;
				if (Mathf.Abs(position2.y - gy) < 0.3f)
				{
					best = Mathf.Min(best, magnitude2);
					reached++;
				}
			}
			if (best < 9f)
			{
				minAll = Mathf.Min(minAll, best);
				maxAll = Mathf.Max(maxAll, best);
				if (best < 0.25f)
				{
					inside++;
				}
				sb.Append($"[({pole.x:F2},{pole.y:F2}) closest={best:F2}] ");
			}
		}
		Finish((inside == 0 && reached > 0) ? "passed" : "failed", sb.ToString(), string.Format("{{\"poles\":{0},\"tries\":{1},\"reached\":{2},\"closestMin\":{3},\"closestMax\":{4},\"inside\":{5}}}", poles.Count, tried, reached, minAll.ToString("F3", CultureInfo.InvariantCulture), maxAll.ToString("F3", CultureInfo.InvariantCulture), inside));
	}

	private IEnumerator SafeReturnTest()
	{
		StoryDirector storyDirector = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		if (storyDirector != null)
		{
			storyDirector.StopAllCoroutines();
			storyDirector.enabled = false;
		}
		SafePoints safe = UnityEngine.Object.FindObjectOfType<SafePoints>();
		if (safe == null)
		{
			Finish("setup_failed", "no SafePoints");
			yield break;
		}
		safe.Enabled = true;
		Time.captureFramerate = 30;
		int ok = 0;
		StringBuilder sb = new StringBuilder();
		Vector3[] pits = new Vector3[2]
		{
			new Vector3(-26.41f, 4.5f, 26.37f),
			new Vector3(-16.61f, 4.12f, 32.97f)
		};
		Vector3[] array = pits;
		for (int l = 0; l < array.Length; l++)
		{
			Vector3 pit = array[l];
			Vector3 vector = new Vector3(-13.6f, 5.53f, 27.4f);
			vector.y = world.GroundAt(vector.x, vector.z, 7f);
			Place(vector, vector + Vector3.forward);
			for (int k = 0; k < 60; k++)
			{
				player.locked = false;
				player.Move(1f / 30f, 0f, 1f, run: false, jumpPressed: false);
				yield return null;
			}
			Place(pit + Vector3.up, pit);
			player.grounded = false;
			player.jump.phase = JumpPhase.air;
			player.jump.vy = 0f;
			for (int k = 0; k < 75; k++)
			{
				player.locked = false;
				player.Move(1f / 30f, 0f, 0f, run: false, jumpPressed: false);
				yield return null;
			}
			Vector3 land = player.transform.position;
			bool returned = safe.TryReturnToSafety();
			for (int k = 0; k < 10; k++)
			{
				player.locked = false;
				player.Move(1f / 30f, 0f, 0f, run: false, jumpPressed: false);
				yield return null;
			}
			Vector3 position = player.transform.position;
			bool flag = returned && world.InWalkArea(position.x, position.z) && player.grounded && position.y > land.y + 0.5f;
			if (flag)
			{
				ok++;
			}
			sb.Append(string.Format("[pit ({0:F2},{1:F2}) land=({2:F2},{3:F2},{4:F2}) return={5} at=({6:F2},{7:F2},{8:F2}) {9}] ", pit.x, pit.z, land.x, land.y, land.z, returned, position.x, position.y, position.z, flag ? "ok" : "FAIL"));
		}
		Finish((ok == pits.Length) ? "passed" : "failed", sb.ToString(), $"{{\"pits\":{pits.Length},\"ok\":{ok}}}");
	}

	private IEnumerator WalkInto(string src)
	{
		if (!File.Exists(src))
		{
			Finish("setup_failed", "找不到 " + src);
			yield break;
		}
		WalkItem[] items = JsonUtility.FromJson<WalkList>(File.ReadAllText(src))?.items ?? new WalkItem[0];
		Camera cam0 = Camera.main;
		ChaseCamera chase = ((cam0 != null) ? cam0.GetComponent<ChaseCamera>() : null);
		StoryDirector storyDirector = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		if (storyDirector != null)
		{
			storyDirector.StopAllCoroutines();
			storyDirector.enabled = false;
		}
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		Time.captureFramerate = 30;
		int under = 0;
		int stuck = 0;
		StringBuilder sb = new StringBuilder();
		WalkItem[] array = items;
		foreach (WalkItem it in array)
		{
			Vector3 a = new Vector3(it.x, world.GroundAt(it.x, it.z, it.y + 0.1f), it.z);
			Vector3 vector = new Vector3(it.tx, a.y, it.tz);
			Place(a, vector);
			Vector3 d = vector - a;
			d.y = 0f;
			float magnitude = d.magnitude;
			d = ((magnitude > 0.0001f) ? (d / magnitude) : Vector3.forward);
			if (chase != null)
			{
				chase.SnapBehind(a, Mathf.Atan2(d.x, d.z));
				chase.DebugOrbit(Mathf.Atan2(d.x, d.z), 22f);
			}
			for (int k = 0; k < 90; k++)
			{
				player.locked = false;
				player.Move(1f / 30f, d.x, d.z, run: false, jumpPressed: false);
				if (chase != null)
				{
					chase.Tick(1f / 30f, player.camFocus, player.yaw, moving: false, running: false);
					chase.SetFadeActive(on: true);
				}
				yield return null;
			}
			Vector3 p = player.transform.position;
			bool roof = Physics.Raycast(p + Vector3.up * 0.15f, Vector3.up, 1.15f, 256, QueryTriggerInteraction.Ignore);
			if (roof)
			{
				under++;
			}
			yield return ShotTool.Capture(cam0, 1280, 720, Path.Combine(dir, "walkinto_" + it.name + ".png"));
			shot++;
			float home = 0f;
			for (int k = 0; k < 2; k++)
			{
				if (!(home < 1f))
				{
					break;
				}
				for (int dd = 0; dd < 8; dd++)
				{
					if (!(home < 1f))
					{
						break;
					}
					Place(p, p + Vector3.forward);
					float ang = (float)dd * MathF.PI / 4f;
					for (int i = 0; i < 75; i++)
					{
						player.locked = false;
						player.Move(1f / 30f, Mathf.Cos(ang), Mathf.Sin(ang), run: false, k == 1 && i % 20 == 0);
						if (i % 5 == 0)
						{
							yield return null;
						}
					}
					Vector3 position = player.transform.position;
					home = Mathf.Max(home, new Vector2(position.x - p.x, position.z - p.z).magnitude);
				}
			}
			bool flag = home < 1f;
			if (flag)
			{
				stuck++;
			}
			sb.Append(string.Format("[{0} from=({1:F2},{2:F2},{3:F2}) to=({4:F2},{5:F2},{6:F2}) end=({7:F2},{8:F2},{9:F2}){10} back={11:F2}{12}] ", it.name, a.x, a.y, a.z, it.tx, it.ty, it.tz, p.x, p.y, p.z, roof ? " UNDER_ROCK" : "", home, flag ? " STUCK" : ""));
		}
		Finish((under == 0 && stuck == 0) ? "passed" : "failed", sb.ToString(), $"{{\"items\":{items.Length},\"underRock\":{under},\"stuck\":{stuck}}}");
	}

	private void Place(Vector3 a, Vector3 b)
	{
		player.ResetJumpInput();
		player.transform.position = a + Vector3.up * 0.01f;
		player.vel = Vector3.zero;
		player.grounded = true;
		player.jump.phase = JumpPhase.none;
		player.FaceTowards(b);
	}

	private IEnumerator WishWheelTest()
	{
		AIAgentCalibrationDirector agent = UnityEngine.Object.FindObjectOfType<AIAgentCalibrationDirector>();
		EmoteWheel wheelSys = EmoteWheel.Instance;
		PrototypeGame game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		if (agent == null || wheelSys == null)
		{
			Finish("setup_failed", "缺心愿笺 / 轮盘");
			yield break;
		}
		Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
		for (int l = 0; l < array.Length; l++)
		{
			array[l].enabled = true;
		}
		cam = Camera.main;
		ChaseCamera chase = ((cam != null) ? cam.GetComponent<ChaseCamera>() : null);
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		StringBuilder sb = new StringBuilder();
		int pass = 0;
		int total = 0;
		Vector3 start = world.SpawnPos;
		Place(start, start + Vector3.forward);
		if (chase != null)
		{
			chase.SnapBehind(start, player.yaw);
		}
		yield return Frames(10);
		agent.SpawnLiveWishObjectInWorld("毛绒熊玩偶");
		yield return Frames(20);
		WishToy toy = agent.LastWishToy;
		Check("toy_spawned", toy != null, (toy != null) ? toy.description : "null");
		if (toy == null)
		{
			Finish("failed", sb.ToString());
			yield break;
		}
		Vector3 position = toy.transform.position;
		float num = WishToy.RouteDistance(new Vector2(position.x, position.z));
		Check("toy_off_route", num >= 0.95f, $"routeDist={num:F2} radius={toy.Radius:F2}");
		Check("toy_colliders", toy.Kickable ? (toy.ColliderCount == 0) : (toy.ColliderCount > 0), $"height={toy.Height:F2} circles={toy.ColliderCount}");
		Bounds b = toy.WorldBounds();
		Vector3 a2 = b.center + (start - b.center).normalized * (toy.Radius + 1.2f);
		a2.y = world.GroundAt(a2.x, a2.z, b.min.y + 0.5f);
		Place(a2, b.center);
		float minGap = float.MaxValue;
		for (int k = 0; k < 90; k++)
		{
			Vector2 normalized = new Vector2(b.center.x - player.transform.position.x, b.center.z - player.transform.position.z).normalized;
			player.locked = false;
			player.Move(1f / 30f, normalized.x, normalized.y, run: false, jumpPressed: false);
			yield return null;
			foreach (EggboyCollider collider in world.Colliders)
			{
				if (collider.r.HasValue && collider.r.Value < 0.2f && Vector2.Distance(new Vector2(collider.x, collider.z), new Vector2(b.center.x, b.center.z)) <= toy.Radius + 0.1f)
				{
					minGap = Mathf.Min(minGap, new Vector2(collider.x - player.transform.position.x, collider.z - player.transform.position.z).magnitude - collider.r.Value);
				}
			}
		}
		Check("toy_blocks_body", toy.Kickable || minGap >= 0.28f, $"closest={minGap:F3}（身体撞台子半径 0.30）");
		yield return Screen("toy_world");
		Vector3 home = toy.transform.position;
		int circles = toy.ColliderCount;
		toy.BeginPick();
		float t2 = Time.time;
		while (!WishInspectModal.IsOpen && Time.time - t2 < 10f)
		{
			yield return null;
		}
		Check("pick_opens_inspect", WishInspectModal.IsOpen && toy.Held && toy.ColliderCount == 0, $"open={WishInspectModal.IsOpen} held={toy.Held} event={PhysicalPickupController.Instance?.LastEvent}");
		yield return Frames(20);
		yield return Screen("inspect");
		WishInspectModal.DebugPutBack();
		t2 = Time.time;
		while (toy.Held && Time.time - t2 < 3f)
		{
			yield return Frames(1);
		}
		yield return Frames(5);
		Check("put_back", !toy.Held && (toy.transform.position - home).magnitude < 0.05f && toy.ColliderCount == circles && !player.locked, $"moved={(toy.transform.position - home).magnitude:F3} circles={toy.ColliderCount}/{circles}");
		toy.BeginPick();
		t2 = Time.time;
		while (!WishInspectModal.IsOpen && Time.time - t2 < 10f)
		{
			yield return null;
		}
		bool stored = WishInspectModal.DebugStore();
		yield return Frames(5);
		Check("store_in_bag", stored && toy.InBag && !toy.gameObject.activeSelf && toy.ColliderCount == 0 && WishBag.Count == 1, $"bag={WishBag.Count}");
		WishToy back = WishBag.TakeOutLatest(player);
		yield return Frames(5);
		Check("take_out", back == toy && toy.gameObject.activeSelf && !toy.InBag && WishBag.Count == 0 && (toy.Kickable || toy.ColliderCount > 0), $"circles={toy.ColliderCount}");
		for (int k = 0; k < 6; k++)
		{
			agent.SpawnLiveWishObjectInWorld("小木马玩具" + k);
			yield return Frames(3);
			if (agent.LastWishToy != null && agent.LastWishToy != toy)
			{
				agent.LastWishToy.StoreInBag();
			}
		}
		Check("bag_full", WishBag.Full && WishBag.Count == 6, $"bag={WishBag.Count}");
		toy.BeginPick();
		t2 = Time.time;
		while (!WishInspectModal.IsOpen && Time.time - t2 < 10f)
		{
			yield return null;
		}
		yield return Frames(10);
		yield return Screen("inspect_bag_full");
		Check("bag_refuses_when_full", !WishInspectModal.DebugStore() && WishInspectModal.IsOpen, "");
		WishInspectModal.DebugPutBack();
		t2 = Time.time;
		while (toy.Held && Time.time - t2 < 3f)
		{
			yield return Frames(1);
		}
		wheelSys.DebugShow(2);
		yield return Frames(3);
		yield return Screen("wheel_action");
		wheelSys.DebugShow(20);
		yield return Frames(3);
		yield return Screen("wheel_expr");
		wheelSys.DebugHide();
		int actOk = 0;
		List<string> bad = new List<string>();
		string[] array2 = new string[7] { "wave", "hop", "nod", "shake", "look", "give", "signal_wave" };
		foreach (string a in array2)
		{
			yield return Frames(45);
			bool fired = wheelSys.FireByName(a);
			yield return Frames(2);
			if (fired && player.CurrentEmote == a)
			{
				actOk++;
				continue;
			}
			bad.Add(a + ":" + wheelSys.LastFired + "/" + player.CurrentEmote);
		}
		Check("wheel_actions", actOk == 7, string.Format("{0}/7 {1}", actOk, string.Join(",", bad)));
		bool ok2 = !wheelSys.FireByName("pet_dog");
		Check("wheel_pet_dog_needs_dog", ok2, "");
		int num2 = 0;
		ExprId[] all = EggboyExpressions.All;
		for (int l = 0; l < all.Length; l++)
		{
			ExprId exprId = all[l];
			wheelSys.FireByName("expr:" + exprId);
			if (game != null && game.BaseExpression == exprId)
			{
				num2++;
			}
		}
		Check("wheel_expressions", num2 == 16, $"{num2}/16");
		Finish((pass == total) ? "passed" : "failed", $"{pass}/{total} " + sb.ToString());
		void Check(string name, bool ok, string note)
		{
			total++;
			if (ok)
			{
				pass++;
			}
			sb.Append(name + "=" + (ok ? "ok" : "FAIL") + " " + note + " | ");
			Debug.Log($"WISHTEST {name}={ok} {note}");
		}
		IEnumerator Frames(int n)
		{
			for (int m = 0; m < n; m++)
			{
				player.Move(1f / 30f, 0f, 0f, run: false, jumpPressed: false);
				if (chase != null)
				{
					chase.Tick(1f / 30f, player.camFocus, player.yaw, moving: false, running: false);
				}
				yield return null;
			}
		}
		IEnumerator Screen(string name)
		{
			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "wish_" + name + ".png"));
			yield return null;
			yield return null;
			shot++;
		}
	}

	private IEnumerator Step(float dx, float dz, bool run, bool jump)
	{
		player.locked = false;
		player.Move(1f / 30f, dx, dz, run, jump);
		frame++;
		TideFeel main = TideFeel.Main;
		if (main != null)
		{
			maxDepth = Mathf.Max(maxDepth, main.HeroDepth);
			if (main.HeroInBush)
			{
				bushFrames++;
			}
		}
		if (frame % 3 == 0)
		{
			yield return new WaitForEndOfFrame();
			ProbeTrail();
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			grab.ReadPixels(new Rect(0f, 0f, 960f, 540f), 0, 0);
			grab.Apply();
			RenderTexture.active = active;
			File.WriteAllBytes(Path.Combine(dir, $"feel_{mode}_{shot:000}.png"), grab.EncodeToPNG());
			shot++;
		}
		yield return null;
	}

	private IEnumerator Idle(float seconds, string name)
	{
		phases.Add($"\"{name}\":{shot}");
		Debug.Log($"FEELDEMO phase {name} shot={shot}");
		int j = Mathf.RoundToInt(seconds * 30f);
		for (int i = 0; i < j; i++)
		{
			yield return Step(0f, 0f, run: false, jump: false);
		}
	}

	private IEnumerator Go(Vector3 to, bool run, bool jumpMidway, string name)
	{
		phases.Add($"\"{name}\":{shot}");
		Debug.Log($"FEELDEMO phase {name} shot={shot}");
		Vector3 position = player.transform.position;
		float total = new Vector2(to.x - position.x, to.z - position.z).magnitude;
		bool jumped = !jumpMidway;
		float best = float.MaxValue;
		int since = 0;
		for (int i = 0; i < 420; i++)
		{
			Vector3 position2 = player.transform.position;
			Vector2 vector = new Vector2(to.x - position2.x, to.z - position2.z);
			float magnitude = vector.magnitude;
			if (magnitude < 0.15f)
			{
				break;
			}
			if (magnitude < best - 0.005f)
			{
				best = magnitude;
				since = 0;
			}
			else
			{
				int num = since + 1;
				since = num;
				if (num > 45)
				{
					stuckPhases++;
					Debug.Log($"FEELDEMO stuck in {name} at ({position2.x:F2},{position2.z:F2}) dist={magnitude:F2}");
					break;
				}
			}
			vector /= magnitude;
			bool flag = !jumped && magnitude < total * 0.55f;
			if (flag)
			{
				jumped = true;
			}
			yield return Step(vector.x, vector.y, run, flag);
		}
	}

	private void ProbeTrail()
	{
		TideFeel main = TideFeel.Main;
		if (!(main == null) && main.TrailOn)
		{
			Vector3 position = player.transform.position;
			maxTrail = Mathf.Max(maxTrail, main.Probe(main.SlowTexture, position).magnitude);
			maxSpring = Mathf.Max(maxSpring, main.Probe(main.SpringTexture, position + player.transform.right * 0.3f).magnitude);
		}
	}

	private void Finish(string status, string note, string checks = null)
	{
		TideFeel main = TideFeel.Main;
		string text = "{\"status\":\"" + status + "\",\"mode\":\"" + mode + "\",\"note\":\"" + note.Replace("\"", "'") + "\",\"frames\":" + frame + ",\"shots\":" + shot + ",\"phases\":{" + string.Join(",", phases) + "},\"stuckPhases\":" + stuckPhases + ",\"trail\":" + ((main != null && main.TrailOn) ? ("\"" + main.TrailFormat + "\"") : "null") + ",\"maxSlowUnderHero\":" + F(maxTrail) + ",\"maxSpringBesideHero\":" + F(maxSpring) + ",\"ripples\":" + ((main != null) ? main.Ripples : 0) + ",\"splashes\":" + ((main != null) ? main.Splashes : 0) + ",\"petals\":" + ((main != null) ? main.Petals : 0) + ",\"slams\":" + ((main != null) ? main.Slams : 0) + ",\"bushes\":" + ((main != null) ? main.BushCount : 0) + ",\"framesInBush\":" + bushFrames + ",\"maxDepth\":" + F(maxDepth) + ",\"water\":" + F(WaterLevel.StaticY) + ",\"speedScale\":" + F((player != null) ? player.speedScale : 1f) + ((checks != null) ? (",\"checks\":" + checks) : "") + "}";
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "feel-demo-" + mode + ".json"), text);
		Debug.Log("FEELDEMO " + text);
		Time.captureFramerate = 0;
		Application.Quit((!(status == "passed")) ? 1 : 0);
		static string F(float v)
		{
			return v.ToString("F3", CultureInfo.InvariantCulture);
		}
	}
}
