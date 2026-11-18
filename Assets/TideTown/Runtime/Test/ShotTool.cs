using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ShotTool : MonoBehaviour
{
	[Serializable]
	public class CamDef
	{
		public string name;

		public float[] pos;

		public float[] euler;

		public float fov;

		public int width;

		public int height;
	}

	[Serializable]
	private class CamList
	{
		public CamDef[] cameras;
	}

	[Serializable]
	internal class MapPointCapture
	{
		public string site;

		public Vector3 position;

		public Vector2 pixel;
	}

	[Serializable]
	internal class MapCalibration
	{
		public List<MapPointCapture> points = new List<MapPointCapture>();
	}

	public sealed class HeroVisibilityProbe : IDisposable
	{
		private readonly Renderer[] renderers;

		private readonly List<Renderer> heroes = new List<Renderer>();

		private readonly Material idMaterial;

		private readonly RenderTexture target;

		private readonly Texture2D pixels;

		public int ReferencePixels { get; private set; }

		public int VisiblePixels { get; private set; }

		public HeroVisibilityProbe(int width = 480, int height = 270)
		{
			renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
			Renderer[] array = renderers;
			foreach (Renderer renderer in array)
			{
				if (IsHero(renderer))
				{
					heroes.Add(renderer);
				}
			}
			Shader shader = Resources.Load<Shader>("TideIdPass");
			if (shader == null)
			{
				throw new InvalidOperationException("TideIdPass shader missing");
			}
			idMaterial = new Material(shader)
			{
				color = HeroId
			};
			target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
			{
				antiAliasing = 1
			};
			pixels = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false);
		}

		private Color32[] Read(Camera camera)
		{
			camera.Render();
			RenderTexture.active = target;
			pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
			pixels.Apply();
			return pixels.GetPixels32();
		}

		public float Sample(Camera camera)
		{
			UniversalAdditionalCameraData universalAdditionalCameraData = camera.GetUniversalAdditionalCameraData();
			RenderTexture targetTexture = camera.targetTexture;
			RenderTexture active = RenderTexture.active;
			AntialiasingMode antialiasing = universalAdditionalCameraData.antialiasing;
			bool renderPostProcessing = universalAdditionalCameraData.renderPostProcessing;
			CameraClearFlags clearFlags = camera.clearFlags;
			Color backgroundColor = camera.backgroundColor;
			List<Material[]> list = new List<Material[]>();
			List<Renderer> list2 = new List<Renderer>();
			try
			{
				camera.targetTexture = target;
				universalAdditionalCameraData.renderPostProcessing = false;
				universalAdditionalCameraData.antialiasing = AntialiasingMode.None;
				camera.clearFlags = CameraClearFlags.Color;
				camera.backgroundColor = Color.black;
				foreach (Renderer hero in heroes)
				{
					list.Add(hero.sharedMaterials);
					Material[] array = new Material[hero.sharedMaterials.Length];
					for (int i = 0; i < array.Length; i++)
					{
						array[i] = idMaterial;
					}
					hero.sharedMaterials = array;
				}
				Color32[] array2 = Read(camera);
				Renderer[] array3 = renderers;
				foreach (Renderer renderer in array3)
				{
					if (renderer != null && renderer.enabled && !heroes.Contains(renderer))
					{
						renderer.enabled = false;
						list2.Add(renderer);
					}
				}
				Color32[] array4 = Read(camera);
				int j = (VisiblePixels = 0);
				ReferencePixels = j;
				for (int k = 0; k < array4.Length; k++)
				{
					Color32 color = array4[k];
					if (color.r >= 245 && color.g >= 115 && color.g <= 200 && color.b <= 8)
					{
						ReferencePixels++;
						Color32 color2 = array2[k];
						if (Math.Abs(color2.r - color.r) <= 2 && Math.Abs(color2.g - color.g) <= 2 && Math.Abs(color2.b - color.b) <= 2)
						{
							VisiblePixels++;
						}
					}
				}
				return (ReferencePixels > 0) ? ((float)VisiblePixels / (float)ReferencePixels) : 0f;
			}
			finally
			{
				foreach (Renderer item in list2)
				{
					if (item != null)
					{
						item.enabled = true;
					}
				}
				for (int l = 0; l < list.Count; l++)
				{
					if (heroes[l] != null)
					{
						heroes[l].sharedMaterials = list[l];
					}
				}
				camera.targetTexture = targetTexture;
				RenderTexture.active = active;
				universalAdditionalCameraData.antialiasing = antialiasing;
				universalAdditionalCameraData.renderPostProcessing = renderPostProcessing;
				camera.clearFlags = clearFlags;
				camera.backgroundColor = backgroundColor;
			}
		}

		public void Dispose()
		{
			target.Release();
			UnityEngine.Object.Destroy(target);
			UnityEngine.Object.Destroy(pixels);
			UnityEngine.Object.Destroy(idMaterial);
		}
	}

	private readonly struct Color32Key : IEquatable<Color32Key>
	{
		private readonly int v;

		public Color32Key(Color32 c)
		{
			v = (c.r << 16) | (c.g << 8) | c.b;
		}

		public bool Equals(Color32Key o)
		{
			return v == o.v;
		}

		public override int GetHashCode()
		{
			return v;
		}
	}

	private static readonly Color32 Sea = new Color32(0, 0, byte.MaxValue, byte.MaxValue);

	private static readonly Color32 Land = new Color32(0, byte.MaxValue, 0, byte.MaxValue);

	private static readonly Color32 Rock = new Color32(byte.MaxValue, byte.MaxValue, 0, byte.MaxValue);

	private static readonly Color32 Foliage = new Color32(0, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	private static readonly Color32 Prop = new Color32(byte.MaxValue, 0, byte.MaxValue, byte.MaxValue);

	public static readonly Color32 HeroId = new Color32(byte.MaxValue, 128, 0, byte.MaxValue);

	private const byte BuildingR = 200;

	private string shotName;

	private bool idPass;

	private static readonly CamDef[] TownViews = new CamDef[4]
	{
		ReportCamera("four_south", new Vector3(-6f, 67f, -84f), new Vector3(0f, 3f, 10f), 36f),
		ReportCamera("four_north", new Vector3(-4f, 74f, 106f), new Vector3(1f, 3f, 13f), 38f),
		ReportCamera("four_east", new Vector3(100f, 68f, 18f), new Vector3(0f, 3f, 10f), 38f),
		ReportCamera("four_west", new Vector3(-100f, 70f, 15f), new Vector3(0f, 3f, 10f), 38f)
	};

	private static readonly CamDef[] ReportDetails = new CamDef[3]
	{
		ReportCamera("report_fountain", new Vector3(-6.2f, 8.6f, -3.8f), new Vector3(-0.8f, 4.4f, 8.2f), 49f),
		ReportCamera("report_harbor", new Vector3(27.5f, 8.6f, -25.5f), new Vector3(19.8f, 2.3f, -14.5f), 51f),
		ReportCamera("report_school", new Vector3(7.8f, 6.6f, -8.4f), new Vector3(14.7f, 4.2f, -0.5f), 48f)
	};

	private static CamDef ReportCamera(string name, Vector3 position, Vector3 target, float fov)
	{
		Vector3 eulerAngles = Quaternion.LookRotation(target - position, Vector3.up).eulerAngles;
		CamDef camDef = new CamDef();
		camDef.name = name;
		camDef.pos = new float[3] { position.x, position.y, position.z };
		camDef.euler = new float[3] { eulerAngles.x, eulerAngles.y, eulerAngles.z };
		camDef.fov = fov;
		camDef.width = 1920;
		camDef.height = 1080;
		return camDef;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		MainSceneGate.WhenMainLoaded(BootNow);
	}

	private static void BootNow()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-shot");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			ShotTool shotTool = new GameObject("ShotTool").AddComponent<ShotTool>();
			shotTool.shotName = commandLineArgs[num + 1];
			shotTool.idPass = Array.IndexOf(commandLineArgs, "-idpass") >= 0;
			UnityEngine.Object.DontDestroyOnLoad(shotTool.gameObject);
		}
	}

	public static CamDef FindCamera(string name)
	{
		TextAsset textAsset = Resources.Load<TextAsset>("World/world_layout");
		if (textAsset != null)
		{
			CamList camList = JsonUtility.FromJson<CamList>(textAsset.text);
			if (camList != null && camList.cameras != null)
			{
				CamDef[] cameras = camList.cameras;
				foreach (CamDef camDef in cameras)
				{
					if (camDef.name == name)
					{
						return camDef;
					}
				}
			}
		}
		switch (name)
		{
		case "mapbase":
			return new CamDef
			{
				name = name,
				width = 1774,
				height = 887
			};
		case "meadow":
		{
			CamDef camDef2 = new CamDef();
			camDef2.name = name;
			camDef2.pos = new float[3] { -9.5f, 4.1f, -4f };
			camDef2.euler = new float[3] { 8f, -62f, 0f };
			camDef2.fov = 50f;
			camDef2.width = 1774;
			camDef2.height = 887;
			return camDef2;
		}
		case "lawn":
		{
			CamDef camDef2 = new CamDef();
			camDef2.name = name;
			camDef2.pos = new float[3] { 20f, 3.75f, -6.5f };
			camDef2.euler = new float[3] { 10f, -35f, 0f };
			camDef2.fov = 50f;
			camDef2.width = 1774;
			camDef2.height = 887;
			return camDef2;
		}
		case "coast":
		{
			CamDef camDef2 = new CamDef();
			camDef2.name = name;
			camDef2.pos = new float[3] { -4f, 2.2f, -27.5f };
			camDef2.euler = new float[3] { 2f, 8f, 0f };
			camDef2.fov = 55f;
			camDef2.width = 1774;
			camDef2.height = 887;
			return camDef2;
		}
		case "shore":
		{
			CamDef camDef2 = new CamDef();
			camDef2.name = name;
			camDef2.pos = new float[3] { 6f, 4.4f, -12.2f };
			camDef2.euler = new float[3] { 24f, 215f, 0f };
			camDef2.fov = 55f;
			camDef2.width = 1774;
			camDef2.height = 887;
			return camDef2;
		}
		case "sunward":
		{
			CamDef camDef2 = new CamDef();
			camDef2.name = name;
			camDef2.pos = new float[3] { 6f, 4.4f, -12.2f };
			camDef2.euler = new float[3] { -4f, 138f, 0f };
			camDef2.fov = 60f;
			camDef2.width = 1774;
			camDef2.height = 887;
			return camDef2;
		}
		case "lh_town":
			return ReportCamera(name, new Vector3(14f, 9f, -2f), new Vector3(34f, 10.5f, 10f), 52f);
		case "lh_sea":
			return ReportCamera(name, new Vector3(64f, 13f, -22f), new Vector3(33f, 9f, 9f), 50f);
		case "lh_deck":
			return ReportCamera(name, new Vector3(32f, 12.2f, 11f), new Vector3(-6f, 8f, 70f), 60f);
		case "custom":
			return ReportCamera(name, ArgVec("-campos", new Vector3(60f, 14f, -24f)), ArgVec("-camlook", new Vector3(20f, 5f, 8f)), ArgFloat("-camfov", 50f));
		case "chase":
		{
			Camera main = Camera.main;
			if (main == null)
			{
				return null;
			}
			Vector3 position = main.transform.position;
			Vector3 eulerAngles = main.transform.eulerAngles;
			CamDef camDef2 = new CamDef();
			camDef2.name = name;
			camDef2.pos = new float[3] { position.x, position.y, position.z };
			camDef2.euler = new float[3] { eulerAngles.x, eulerAngles.y, eulerAngles.z };
			camDef2.fov = main.fieldOfView;
			camDef2.width = 1920;
			camDef2.height = 1080;
			return camDef2;
		}
		default:
			if (name == "preview")
			{
				TextAsset textAsset2 = Resources.Load<TextAsset>("World/preview_camera");
				if (textAsset2 != null)
				{
					return JsonUtility.FromJson<CamDef>(textAsset2.text);
				}
			}
			return null;
		}
	}

	private IEnumerator Start()
	{
		Application.runInBackground = true;
		yield return new WaitForSecondsRealtime(ArgFloat("-shotdelay", 3f));
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-night") >= 0)
		{
			SkyDimmer skyDimmer = UnityEngine.Object.FindObjectOfType<SkyDimmer>();
			if (skyDimmer != null)
			{
				skyDimmer.SetDimmedInstant();
				yield return new WaitForSecondsRealtime(0.5f);
			}
			else
			{
				Debug.LogWarning("SHOT -night 找不到 SkyDimmer");
			}
		}
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-beacon") >= 0)
		{
			BeaconLight beaconLight = UnityEngine.Object.FindObjectOfType<BeaconLight>();
			CamDef camDef = FindCamera(shotName);
			if (beaconLight != null)
			{
				if (Array.IndexOf(Environment.GetCommandLineArgs(), "-beaconignite") >= 0)
				{
					beaconLight.TurnOn();
				}
				else
				{
					beaconLight.TurnOnInstant();
				}
				beaconLight.ManualMode = true;
				if (shotName == "lh_deck")
				{
					beaconLight.SetYawOffsetFrom(new Vector3(-6f, 8f, 70f), ArgFloat("-beaconyaw", -14f));
				}
				else if (camDef != null && camDef.pos != null)
				{
					beaconLight.SetYawOffsetFrom(new Vector3(camDef.pos[0], camDef.pos[1], camDef.pos[2]), ArgFloat("-beaconyaw", 55f));
				}
			}
			FlashlightReply flashlightReply = UnityEngine.Object.FindObjectOfType<FlashlightReply>();
			if (flashlightReply != null)
			{
				flashlightReply.SetDoneInstant();
			}
			yield return new WaitForSecondsRealtime(ArgFloat("-beaconwait", 0.6f));
		}
		CamDef[] sequence = ((shotName == "fourviews") ? TownViews : ((shotName == "reportfigs") ? ReportDetails : null));
		CamDef camDef2 = ((sequence != null) ? sequence[0] : FindCamera(shotName));
		if (camDef2 == null)
		{
			Debug.LogError("SHOT 找不到机位 " + shotName);
			Application.Quit(1);
			yield break;
		}
		bool mapBase = shotName == "mapbase";
		if (mapBase && Mathf.Abs(TownLighting.Phase) > 0.001f)
		{
			Debug.LogError("MAPBASE requires active daylight. Use -resetsave -look day -shot mapbase -shot_aa.");
			Application.Quit(1);
			yield break;
		}
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-noshadows") >= 0)
		{
			Light[] array = UnityEngine.Object.FindObjectsOfType<Light>();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].shadows = LightShadows.None;
			}
		}
		Canvas[] array2 = UnityEngine.Object.FindObjectsOfType<Canvas>();
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].enabled = false;
		}
		if (shotName == "preview" || mapBase || sequence != null)
		{
			GameObject gameObject = GameObject.Find("EGGBOY");
			if (gameObject != null)
			{
				gameObject.SetActive(value: false);
			}
		}
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		Camera main = Camera.main;
		GameObject gameObject2 = new GameObject("ShotCam");
		Camera cam = gameObject2.AddComponent<Camera>();
		if (main != null)
		{
			cam.CopyFrom(main);
			main.enabled = false;
		}
		UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
		data.renderPostProcessing = true;
		data.antialiasing = ((Array.IndexOf(Environment.GetCommandLineArgs(), "-shot_aa") >= 0) ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None);
		data.antialiasingQuality = AntialiasingQuality.High;
		cam.nearClipPlane = 1f;
		cam.farClipPlane = 800f;
		if (sequence != null)
		{
			TownLighting.ApplyShadowRange(210f, 4);
			CamDef[] array3 = sequence;
			foreach (CamDef camDef3 in array3)
			{
				SetPose(cam, camDef3);
				cam.aspect = (float)camDef3.width / (float)camDef3.height;
				Shader.SetGlobalVector("_TideCamFade", Vector4.zero);
				yield return Capture(cam, camDef3.width, camDef3.height, Path.Combine(dir, camDef3.name + ".png"), renderExplicitly: true);
			}
			Debug.Log($"SHOT done {shotName} ({sequence.Length} images)");
			Application.Quit();
			yield break;
		}
		if (shotName == "preview")
		{
			TownLighting.ApplyShadowRange(260f, 4);
		}
		int w = ((camDef2.width > 0) ? camDef2.width : 1774);
		int h = ((camDef2.height > 0) ? camDef2.height : 887);
		TownMap.MapBounds bounds = null;
		if (mapBase)
		{
			IslandWorld islandWorld = UnityEngine.Object.FindObjectOfType<IslandWorld>();
			bounds = ConfigureMapBase(cam, islandWorld, w, h);
			if (bounds == null)
			{
				Application.Quit(1);
				yield break;
			}
			WaterLevel.Main?.SetInstant(WaterLevel.LevelYs[0]);
			RenderSettings.fog = false;
			Shader.SetGlobalFloat("_ToonHaze", 0f);
			Shader.SetGlobalVector("_TideCamFade", Vector4.zero);
			TownMap townMap = UnityEngine.Object.FindObjectOfType<TownMap>();
			if (townMap != null)
			{
				townMap.enabled = false;
			}
			if (islandWorld.mailboxTrail != null)
			{
				islandWorld.mailboxTrail.SetActive(value: false);
			}
			TownLighting.ApplyShadowRange(160f, 4);
		}
		else
		{
			SetPose(cam, camDef2);
		}
		yield return Capture(cam, w, h, Path.Combine(dir, shotName + ".png"), mapBase);
		int frames = Mathf.RoundToInt(ArgFloat("-shotframes", 0f));
		float interval = ArgFloat("-shotinterval", 0.1f);
		for (int j = 0; j < frames; j++)
		{
			if (mapBase)
			{
				break;
			}
			yield return new WaitForSecondsRealtime(interval);
			yield return Capture(cam, w, h, Path.Combine(dir, shotName + "_" + j.ToString("000") + ".png"));
		}
		if (bounds != null)
		{
			File.WriteAllText(Path.Combine(dir, "map_bounds.json"), JsonUtility.ToJson(bounds, prettyPrint: true), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			MapCalibration mapCalibration = new MapCalibration();
			string[] array4 = new string[4] { "P_03", "P_FOUNTAIN", "P_20", "P_25" };
			foreach (string text in array4)
			{
				GameObject gameObject3 = GameObject.Find(text);
				if (gameObject3 == null)
				{
					Debug.LogError("MAPBASE missing scene marker " + text);
					Application.Quit(1);
					yield break;
				}
				Vector3 position = gameObject3.transform.position;
				if (text == "P_FOUNTAIN")
				{
					position.z -= 2f;
				}
				Vector3 vector = cam.WorldToViewportPoint(position);
				mapCalibration.points.Add(new MapPointCapture
				{
					site = text,
					position = position,
					pixel = new Vector2(vector.x * (float)w, (1f - vector.y) * (float)h)
				});
			}
			File.WriteAllText(Path.Combine(dir, "mapbase_points.json"), JsonUtility.ToJson(mapCalibration, prettyPrint: true), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		if (idPass)
		{
			List<string> info = PrepareIdPass(cam, data);
			yield return Capture(cam, w, h, Path.Combine(dir, shotName + "_id.png"));
			StringBuilder stringBuilder = new StringBuilder("{\n  \"width\": " + w + ", \"height\": " + h + ",\n  \"buildings\": {");
			for (int k = 0; k < info.Count; k++)
			{
				stringBuilder.Append((k == 0) ? "" : ", ").Append('"').Append(info[k])
					.Append("\": ")
					.Append((k + 1) * 20);
			}
			stringBuilder.Append("},\n  \"points\": {");
			bool flag = true;
			cam.aspect = (float)w / (float)h;
			Transform[] array5 = UnityEngine.Object.FindObjectsOfType<Transform>();
			foreach (Transform transform in array5)
			{
				if (transform.name.StartsWith("P_"))
				{
					Vector3 vector2 = cam.WorldToViewportPoint(transform.position);
					stringBuilder.Append(flag ? "" : ", ").Append('"').Append(transform.name.Substring(2))
						.Append("\": [")
						.Append((vector2.x * (float)w).ToString("F1"))
						.Append(", ")
						.Append(((1f - vector2.y) * (float)h).ToString("F1"))
						.Append("]");
					flag = false;
				}
			}
			stringBuilder.Append("}\n}\n");
			File.WriteAllText(Path.Combine(dir, shotName + "_id.json"), stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		Debug.Log("SHOT done " + shotName);
		Application.Quit();
	}

	private static void SetPose(Camera cam, CamDef def)
	{
		cam.transform.SetPositionAndRotation(new Vector3(def.pos[0], def.pos[1], def.pos[2]), Quaternion.Euler(def.euler[0], def.euler[1], def.euler[2]));
		cam.fieldOfView = def.fov;
	}

	public static IEnumerator Capture(Camera cam, int w, int h, string path, bool renderExplicitly = false)
	{
		RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
		{
			antiAliasing = 1
		};
		cam.allowMSAA = false;
		if (renderExplicitly)
		{
			cam.enabled = false;
			UniversalRenderPipeline.SingleCameraRequest data = new UniversalRenderPipeline.SingleCameraRequest
			{
				destination = rt
			};
			RenderPipeline.SubmitRenderRequest(cam, data);
		}
		else
		{
			cam.targetTexture = rt;
			cam.enabled = true;
			yield return null;
			yield return null;
			yield return new WaitForEndOfFrame();
		}
		RenderTexture active = RenderTexture.active;
		RenderTexture.active = rt;
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGB24, mipChain: false, linear: false);
		texture2D.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
		texture2D.Apply();
		RenderTexture.active = active;
		File.WriteAllBytes(path, texture2D.EncodeToPNG());
		UnityEngine.Object.Destroy(texture2D);
		cam.targetTexture = null;
		rt.Release();
		Debug.Log("SHOT " + path);
	}

	private static TownMap.MapBounds ConfigureMapBase(Camera cam, IslandWorld world, int width, int height)
	{
		GameObject gameObject = GameObject.Find("Tier_coast");
		Renderer renderer = ((gameObject != null) ? gameObject.GetComponent<Renderer>() : null);
		if (world == null || renderer == null)
		{
			Debug.LogError("MAPBASE requires the main island scene and its generated coast");
			return null;
		}
		Bounds bounds = renderer.bounds;
		foreach (WalkPoly walkPoly in world.walkPolys)
		{
			if (walkPoly.points != null)
			{
				Vector2[] points = walkPoly.points;
				for (int i = 0; i < points.Length; i++)
				{
					Vector2 vector = points[i];
					bounds.Encapsulate(new Vector3(vector.x, walkPoly.y, vector.y));
				}
			}
		}
		float num = (float)width / (float)height;
		float num2 = Mathf.Max(bounds.size.z + 7f, (bounds.size.x + 7f) / num);
		TownMap.MapBounds mapBounds = new TownMap.MapBounds
		{
			cx = bounds.center.x,
			cz = bounds.center.z,
			spanX = num2 * num,
			spanZ = num2,
			pixelWidth = width,
			pixelHeight = height
		};
		cam.orthographic = true;
		cam.orthographicSize = num2 * 0.5f;
		cam.aspect = num;
		cam.transform.SetPositionAndRotation(new Vector3(mapBounds.cx, bounds.max.y + 65f, mapBounds.cz), Quaternion.LookRotation(Vector3.down, Vector3.forward));
		cam.ResetProjectionMatrix();
		return mapBounds;
	}

	private static bool InIdRegion(Bounds b)
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-idregion");
		if (num < 0 || num + 1 >= commandLineArgs.Length)
		{
			return true;
		}
		string[] array = commandLineArgs[num + 1].Split(',');
		if (array.Length < 4)
		{
			return true;
		}
		float num2 = float.Parse(array[0], CultureInfo.InvariantCulture);
		float num3 = float.Parse(array[1], CultureInfo.InvariantCulture);
		float num4 = float.Parse(array[2], CultureInfo.InvariantCulture);
		float num5 = float.Parse(array[3], CultureInfo.InvariantCulture);
		if (b.max.x >= num2 && b.min.x <= num3 && b.max.z >= num4)
		{
			return b.min.z <= num5;
		}
		return false;
	}

	private List<string> PrepareIdPass(Camera cam, UniversalAdditionalCameraData data)
	{
		data.renderPostProcessing = false;
		data.antialiasing = AntialiasingMode.None;
		RenderSettings.fog = false;
		RenderSettings.skybox = null;
		cam.clearFlags = CameraClearFlags.Color;
		cam.backgroundColor = Color.black;
		Shader shader = Resources.Load<Shader>("TideIdPass");
		Dictionary<Color32Key, Material> dictionary = new Dictionary<Color32Key, Material>();
		List<string> list = new List<string>();
		bool flag = Array.IndexOf(Environment.GetCommandLineArgs(), "-idobjects") >= 0;
		StringBuilder stringBuilder = (flag ? new StringBuilder() : null);
		int num = 0;
		Renderer[] array = UnityEngine.Object.FindObjectsOfType<Renderer>();
		foreach (Renderer renderer in array)
		{
			if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
			{
				renderer.enabled = false;
				continue;
			}
			Color32 color;
			if (flag && !InIdRegion(renderer.bounds))
			{
				color = new Color32(0, 0, 0, byte.MaxValue);
			}
			else if (flag)
			{
				num++;
				color = new Color32((byte)(20 + num % 12 * 19), (byte)(20 + num / 12 % 12 * 19), (byte)(20 + num / 144 % 12 * 19), byte.MaxValue);
				List<string> list2 = new List<string>();
				Transform parent = renderer.transform;
				while (parent != null)
				{
					list2.Insert(0, parent.name);
					parent = parent.parent;
				}
				stringBuilder.Append(color.r).Append(',').Append(color.g)
					.Append(',')
					.Append(color.b)
					.Append('\t')
					.Append(string.Join("/", list2))
					.Append('\n');
			}
			else
			{
				color = Classify(renderer, list);
			}
			Color32Key key = new Color32Key(color);
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = new Material(shader);
				value.color = color;
				dictionary[key] = value;
			}
			Material[] sharedMaterials = renderer.sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				sharedMaterials[j] = value;
			}
			renderer.sharedMaterials = sharedMaterials;
			renderer.shadowCastingMode = ShadowCastingMode.Off;
		}
		if (stringBuilder != null)
		{
			string text = Path.Combine(Application.dataPath, "..", "shots");
			Directory.CreateDirectory(text);
			File.WriteAllText(Path.Combine(text, "id_legend.txt"), stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		return list;
	}

	private static Color32 Classify(Renderer r, List<string> buildings)
	{
		if (IsHero(r))
		{
			return HeroId;
		}
		TownAssetInfo componentInParent = r.GetComponentInParent<TownAssetInfo>();
		if (componentInParent != null && componentInParent.category == "Buildings")
		{
			string item = InstanceId(r.transform, componentInParent);
			int num = buildings.IndexOf(item);
			if (num < 0)
			{
				buildings.Add(item);
				num = buildings.Count - 1;
			}
			return new Color32(200, (byte)((num + 1) * 20), 0, byte.MaxValue);
		}
		Transform parent = r.transform;
		while (parent != null)
		{
			string text = parent.name;
			if (parent.gameObject.layer == LayerMask.NameToLayer("Water") || text == "Sea" || text.StartsWith("Water"))
			{
				return Sea;
			}
			if (text == "Rocks" || text.StartsWith("Rock") || text.StartsWith("Cliff"))
			{
				return Rock;
			}
			if (text == "Foliage" || text.StartsWith("Tree") || text.StartsWith("Bush") || text.StartsWith("Flower"))
			{
				return Foliage;
			}
			parent = parent.parent;
		}
		if (!(componentInParent != null))
		{
			return Land;
		}
		return Prop;
	}

	public static bool IsHero(Renderer renderer)
	{
		if (renderer.GetComponentInParent<EggboyPlayer>() != null)
		{
			return true;
		}
		Transform parent = renderer.transform;
		while (parent != null)
		{
			if (parent.name == "EGGBOY")
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}

	private static string InstanceId(Transform t, TownAssetInfo info)
	{
		return info.transform.name;
	}

	private static float ArgFloat(string key, float fallback)
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, key);
		if (num < 0 || num + 1 >= commandLineArgs.Length || !float.TryParse(commandLineArgs[num + 1], out var result))
		{
			return fallback;
		}
		return result;
	}

	private static Vector3 ArgVec(string key, Vector3 fallback)
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, key);
		if (num < 0 || num + 1 >= commandLineArgs.Length)
		{
			return fallback;
		}
		string[] array = commandLineArgs[num + 1].Split(',');
		if (array.Length != 3)
		{
			return fallback;
		}
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		if (!float.TryParse(array[0], NumberStyles.Float, invariantCulture, out var result) || !float.TryParse(array[1], NumberStyles.Float, invariantCulture, out var result2) || !float.TryParse(array[2], NumberStyles.Float, invariantCulture, out var result3))
		{
			return fallback;
		}
		return new Vector3(result, result2, result3);
	}
}
