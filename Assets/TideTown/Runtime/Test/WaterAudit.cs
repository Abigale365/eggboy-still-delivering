using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class WaterAudit : MonoBehaviour
{
	[Serializable]
	private class RoutePt
	{
		public string id;

		public bool stop;

		public float x;

		public float z;
	}

	[Serializable]
	private class RouteDoc
	{
		public RoutePt[] route;
	}

	private GameBootstrap boot;

	private WaterLevel water;

	private EggboyPlayer player;

	private IslandWorld world;

	private SafePoints safe;

	private PrototypeGame game;

	private Camera shotCam;

	private Transform hero;

	private string status = "running";

	private string reason = "";

	private readonly StringBuilder levels = new StringBuilder();

	private string crateJson = "{}";

	private string rescueJson = "{}";

	private string fallJson = "{}";

	private string riseJson = "{}";

	private string shaderJson = "{}";

	private float startedAt;

	public void Begin(GameBootstrap b)
	{
		boot = b;
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return null;
		startedAt = Time.realtimeSinceStartup;
		water = UnityEngine.Object.FindObjectOfType<WaterLevel>();
		player = UnityEngine.Object.FindObjectOfType<EggboyPlayer>();
		world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		safe = UnityEngine.Object.FindObjectOfType<SafePoints>();
		game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		if (water == null || player == null || world == null)
		{
			Fail("setup_failed", "缺 WaterLevel / EggboyPlayer / IslandWorld");
			yield break;
		}
		if (game != null)
		{
			game.enabled = false;
		}
		Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].enabled = false;
		}
		GameObject gameObject = GameObject.Find("EGGBOY");
		if (gameObject != null)
		{
			hero = gameObject.transform;
		}
		shaderJson = ShaderCheck();
		if (status == "running")
		{
			yield return Levels();
		}
		if (status == "running")
		{
			yield return Rise();
		}
		if (status == "running")
		{
			yield return CrateCross();
		}
		if (status == "running")
		{
			yield return Rescue();
		}
		if (status == "running")
		{
			yield return Fall();
		}
		water.SetInstant(WaterLevel.LevelYs[0]);
		if (status == "running")
		{
			status = "passed";
		}
		Finish();
	}

	private IEnumerator Levels()
	{
		ShotTool.CamDef camDef = ShotTool.FindCamera("preview");
		Camera main = Camera.main;
		GameObject gameObject = new GameObject("WaterShotCam");
		shotCam = gameObject.AddComponent<Camera>();
		if (main != null)
		{
			shotCam.CopyFrom(main);
			main.enabled = false;
		}
		if (camDef != null)
		{
			shotCam.transform.SetPositionAndRotation(new Vector3(camDef.pos[0], camDef.pos[1], camDef.pos[2]), Quaternion.Euler(camDef.euler[0], camDef.euler[1], camDef.euler[2]));
			shotCam.fieldOfView = camDef.fov;
			shotCam.nearClipPlane = 1f;
			shotCam.farClipPlane = 800f;
		}
		if (hero != null)
		{
			hero.gameObject.SetActive(value: false);
		}
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		int w = ((camDef != null && camDef.width > 0) ? camDef.width : 1774);
		int h = ((camDef != null && camDef.height > 0) ? camDef.height : 887);
		for (int i = 0; i < WaterLevel.LevelNames.Length; i++)
		{
			water.SetInstant(WaterLevel.LevelYs[i]);
			yield return null;
			yield return null;
			string file = "water_" + WaterLevel.LevelNames[i] + ".png";
			yield return ShotTool.Capture(shotCam, w, h, Path.Combine(dir, file));
			levels.Append((levels.Length == 0) ? "" : ",").Append("[\"").Append(WaterLevel.LevelNames[i])
				.Append("\",")
				.Append(WaterLevel.LevelYs[i].ToString("F2"))
				.Append(",\"")
				.Append(file)
				.Append("\"]");
		}
		if (hero != null)
		{
			hero.gameObject.SetActive(value: true);
		}
		if (shotCam != null)
		{
			UnityEngine.Object.Destroy(shotCam.gameObject);
		}
		if (main != null)
		{
			main.enabled = true;
		}
		water.SetInstant(WaterLevel.LevelYs[2]);
		yield return null;
	}

	private IEnumerator Rise()
	{
		water.SetInstant(WaterLevel.LevelYs[2]);
		yield return null;
		ChaseCamera chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		float from = water.Y;
		water.Set(WaterLevel.LevelYs[3], 4f);
		float maxLift = 0f;
		float lastY = from;
		bool monotonic = true;
		int samples = 0;
		float t0 = Time.realtimeSinceStartup;
		while (Time.realtimeSinceStartup - t0 < 4.6f)
		{
			yield return new WaitForSecondsRealtime(0.2f);
			samples++;
			if (water.Y < lastY - 0.001f)
			{
				monotonic = false;
			}
			lastY = water.Y;
			if (chase != null)
			{
				maxLift = Mathf.Max(maxLift, chase.extraLift);
			}
			if (Time.realtimeSinceStartup - t0 > 4.1f && Mathf.Abs(water.Y - WaterLevel.LevelYs[3]) < 0.02f)
			{
				break;
			}
		}
		float num = Time.realtimeSinceStartup - t0;
		bool flag = monotonic && Mathf.Abs(water.Y - WaterLevel.LevelYs[3]) < 0.02f && num <= 4.9f && maxLift > 0.3f;
		riseJson = "{\"ok\":" + (flag ? "true" : "false") + ",\"from\":" + from.ToString("F2") + ",\"to\":" + water.Y.ToString("F2") + ",\"seconds\":" + num.ToString("F2") + ",\"samples\":" + samples + ",\"monotonic\":" + (monotonic ? "true" : "false") + ",\"maxCameraLift\":" + maxLift.ToString("F2") + "}";
		if (!flag)
		{
			Fail("rise_failed", "涨水过场不对：" + riseJson);
		}
	}

	private static int CrateIndex(string name)
	{
		int num = name.LastIndexOf('_');
		if (num < 0 || !int.TryParse(name.Substring(num + 1), out var result))
		{
			return 0;
		}
		return result;
	}

	private IEnumerator CrateCross()
	{
		water.SetInstant(WaterLevel.LevelYs[2]);
		yield return null;
		yield return null;
		List<FloatingCrate> crates = new List<FloatingCrate>(UnityEngine.Object.FindObjectsOfType<FloatingCrate>());
		if (crates.Count < 2)
		{
			Fail("crate_failed", "场景里浮箱少于 2 个");
			yield break;
		}
		crates.Sort((FloatingCrate a, FloatingCrate b) => CrateIndex(a.name).CompareTo(CrateIndex(b.name)));
		Vector3 position = crates[0].transform.position;
		Vector3 position2 = crates[crates.Count - 1].transform.position;
		Vector3 normalized = new Vector3(crates[1].transform.position.x - position.x, 0f, crates[1].transform.position.z - position.z).normalized;
		Vector3 position3 = crates[crates.Count - 2].transform.position;
		Vector3 normalized2 = new Vector3(position2.x - position3.x, 0f, position2.z - position3.z).normalized;
		Vector3 start = position - normalized * 1.4f;
		start.y = world.GroundAt(start.x, start.z) + 0.01f;
		List<Vector3> targets = new List<Vector3>();
		for (int j = 1; j < crates.Count; j++)
		{
			targets.Add(crates[j].transform.position);
		}
		targets.Add(position2 + normalized2 * 0.3f);
		RouteDoc routeDoc = JsonUtility.FromJson<RouteDoc>(Resources.Load<TextAsset>("World/route").text);
		int num = Array.FindIndex(routeDoc.route, (RoutePt r) => r.stop && r.id == "LOW");
		int num2 = num + 1;
		while (num >= 0 && num2 < routeDoc.route.Length)
		{
			targets.Add(new Vector3(routeDoc.route[num2].x, 0f, routeDoc.route[num2].z));
			if (routeDoc.route[num2].stop)
			{
				break;
			}
			num2++;
		}
		player.transform.position = start;
		player.vel = Vector3.zero;
		player.grounded = true;
		yield return null;
		yield return null;
		float t2 = Time.realtimeSinceStartup;
		int waterFrames = 0;
		int frames = 0;
		int i = 0;
		float maxY = start.y;
		float best = float.MaxValue;
		float bestAt = t2;
		while (Time.realtimeSinceStartup - t2 < 20f && i < targets.Count)
		{
			Vector3 position4 = player.transform.position;
			Vector3 vector = new Vector3(targets[i].x - position4.x, 0f, targets[i].z - position4.z);
			float magnitude = vector.magnitude;
			if (magnitude < 0.5f)
			{
				i++;
				best = float.MaxValue;
				bestAt = Time.realtimeSinceStartup;
				continue;
			}
			if (magnitude < best - 0.01f)
			{
				best = magnitude;
				bestAt = Time.realtimeSinceStartup;
			}
			else if (magnitude < 1.2f && Time.realtimeSinceStartup - bestAt > 0.6f)
			{
				i++;
				best = float.MaxValue;
				bestAt = Time.realtimeSinceStartup;
				continue;
			}
			if (position4.y < water.Y - 0.05f)
			{
				waterFrames++;
			}
			maxY = Mathf.Max(maxY, position4.y);
			vector.Normalize();
			player.Move(Mathf.Min(0.05f, Time.deltaTime), vector.x, vector.z, run: false, jumpPressed: false);
			frames++;
			yield return null;
		}
		Vector3 position5 = player.transform.position;
		float num3 = Time.realtimeSinceStartup - t2;
		GameObject gameObject = GameObject.Find("P_26");
		Vector3 vector2 = ((gameObject != null) ? gameObject.transform.position : Vector3.zero);
		Vector3 position6 = crates[crates.Count / 2].transform.position;
		bool flag = i >= targets.Count && targets.Count > 1 && waterFrames == 0;
		crateJson = "{\"ok\":" + (flag ? "true" : "false") + ",\"from\":[" + start.x.ToString("F2") + "," + start.y.ToString("F2") + "," + start.z.ToString("F2") + "],\"to\":[" + position5.x.ToString("F2") + "," + position5.y.ToString("F2") + "," + position5.z.ToString("F2") + "],\"k\":" + i + ",\"targets\":[" + string.Join(",", targets.ConvertAll((Vector3 t) => "[" + t.x.ToString("F2") + "," + t.z.ToString("F2") + "]")) + "],\"crates\":[" + string.Join(",", crates.ConvertAll((FloatingCrate c) => "[" + c.transform.position.x.ToString("F2") + "," + c.transform.position.y.ToString("F2") + "," + c.transform.position.z.ToString("F2") + "]")) + "],\"seconds\":" + num3.ToString("F2") + ",\"frames\":" + frames + ",\"waterFrames\":" + waterFrames + ",\"maxY\":" + maxY.ToString("F2") + ",\"jumpUsed\":false,\"walkableOnCrates\":" + (world.Walkable(position6.x, position6.z) ? "true" : "false") + ",\"walkableInWater\":" + (world.Walkable(vector2.x, vector2.z) ? "true" : "false") + "}";
		if (!flag)
		{
			Fail("crate_failed", "木箱区没走过去：" + crateJson);
		}
	}

	private IEnumerator Rescue()
	{
		water.SetInstant(WaterLevel.LevelYs[2]);
		yield return null;
		if (safe == null)
		{
			Fail("rescue_failed", "场景里没有 SafePoints");
			yield break;
		}
		int before = safe.RescueCount;
		GameObject gameObject = GameObject.Find("P_26");
		if (gameObject == null)
		{
			Fail("rescue_failed", "场景里没有 P_26 码头点");
			yield break;
		}
		Vector3 position = gameObject.transform.position;
		Vector3 from = new Vector3(position.x, world.GroundAt(position.x, position.z), position.z);
		player.transform.position = from;
		player.vel = Vector3.zero;
		player.grounded = true;
		float t0 = Time.realtimeSinceStartup;
		while (Time.realtimeSinceStartup - t0 < 2f && safe.RescueCount == before)
		{
			yield return null;
		}
		Vector3 position2 = player.transform.position;
		bool flag = safe.RescueCount > before;
		bool flag2 = position2.y >= water.Y - 0.05f;
		bool flag3 = flag && flag2;
		rescueJson = "{\"ok\":" + (flag3 ? "true" : "false") + ",\"from\":[" + from.x.ToString("F2") + "," + from.y.ToString("F2") + "," + from.z.ToString("F2") + "],\"to\":[" + position2.x.ToString("F2") + "," + position2.y.ToString("F2") + "," + position2.z.ToString("F2") + "],\"reason\":\"" + safe.LastReason + "\",\"seconds\":" + (Time.realtimeSinceStartup - t0).ToString("F2") + ",\"dry\":" + (flag2 ? "true" : "false") + ",\"rescued\":" + (flag ? "true" : "false") + "}";
		if (!flag3)
		{
			Fail("rescue_failed", "掉水后没回到安全点：" + rescueJson);
		}
	}

	private IEnumerator Fall()
	{
		water.SetInstant(WaterLevel.LevelYs[0]);
		yield return null;
		if (safe == null)
		{
			Fail("fall_failed", "场景里没有 SafePoints");
			yield break;
		}
		Vector3 back2;
		if (world.FreeRoam)
		{
			int num = 0;
			int num2 = 0;
			foreach (Vector3 point in safe.points)
			{
				num2++;
				if (safe.FellBelowRoute(point, out back2))
				{
					num++;
				}
			}
			fallJson = "{\"ok\":" + ((num == 0) ? "true" : "false") + ",\"freeRoam\":true,\"tested\":" + num2 + ",\"falsePositives\":" + num + ",\"note\":\"fell-rescue disabled under free roam; traps covered by RoamAudit\"}";
			if (num > 0)
			{
				Fail("fall_failed", "自由漫游下掉层救援应当关闭：" + fallJson);
			}
			yield break;
		}
		int tested = 0;
		int falsePos = 0;
		StringBuilder fp = new StringBuilder();
		foreach (WalkPoly walkPoly in world.walkPolys)
		{
			if (walkPoly.points == null || walkPoly.points.Length < 3)
			{
				continue;
			}
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MaxValue;
			float num6 = float.MinValue;
			Vector2[] points = walkPoly.points;
			for (int i = 0; i < points.Length; i++)
			{
				Vector2 vector = points[i];
				num3 = Mathf.Min(num3, vector.x);
				num4 = Mathf.Max(num4, vector.x);
				num5 = Mathf.Min(num5, vector.y);
				num6 = Mathf.Max(num6, vector.y);
			}
			for (float num7 = Mathf.Ceil(num3); num7 <= num4; num7 += 1f)
			{
				for (float num8 = Mathf.Ceil(num5); num8 <= num6; num8 += 1f)
				{
					if (!world.InWalkArea(num7, num8))
					{
						continue;
					}
					float num9 = world.GroundAt(num7, num8, walkPoly.y + 0.5f);
					if (num9 < 0.2f || num9 < water.Y - 0.05f)
					{
						continue;
					}
					tested++;
					if (safe.FellBelowRoute(new Vector3(num7, num9, num8), out back2))
					{
						falsePos++;
						if (falsePos <= 12)
						{
							fp.Append((fp.Length == 0) ? "" : ",").Append("[\"").Append(walkPoly.id)
								.Append("\",")
								.Append(num7.ToString("F1"))
								.Append(",")
								.Append(num9.ToString("F2"))
								.Append(",")
								.Append(num8.ToString("F1"))
								.Append("]");
						}
					}
				}
			}
		}
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		RouteDoc routeDoc = ((textAsset != null) ? JsonUtility.FromJson<RouteDoc>(textAsset.text) : null);
		Vector3 spot = Vector3.zero;
		Vector3 back = Vector3.zero;
		bool found = false;
		bool foundWalkable = false;
		if (routeDoc != null && routeDoc.route != null)
		{
			for (int j = 0; j + 1 < routeDoc.route.Length; j++)
			{
				if (foundWalkable)
				{
					break;
				}
				Vector2 vector2 = new Vector2(routeDoc.route[j].x, routeDoc.route[j].z);
				Vector2 vector3 = new Vector2(routeDoc.route[j + 1].x, routeDoc.route[j + 1].z);
				float magnitude = (vector3 - vector2).magnitude;
				if (magnitude < 0.1f)
				{
					continue;
				}
				Vector2 vector4 = (vector3 - vector2) / magnitude;
				Vector2 vector5 = new Vector2(0f - vector4.y, vector4.x);
				for (float num10 = 0f; num10 <= magnitude; num10 += 0.5f)
				{
					if (foundWalkable)
					{
						break;
					}
					Vector2 vector6 = vector2 + vector4 * num10;
					float num11 = world.GroundAt(vector6.x, vector6.y);
					float[] array = new float[10] { 0.8f, 1.2f, 1.6f, 2f, 2.4f, -0.8f, -1.2f, -1.6f, -2f, -2.4f };
					foreach (float num12 in array)
					{
						Vector2 vector7 = vector6 + vector5 * num12;
						float num13 = world.GroundAt(vector7.x, vector7.y, num11 - 0.6f);
						if (num13 <= 0.05f || num13 < water.Y - 0.05f || num11 - num13 < 0.6f)
						{
							continue;
						}
						Vector3 vector8 = new Vector3(vector7.x, num13, vector7.y);
						if (safe.FellBelowRoute(vector8, out var back3))
						{
							bool flag = world.Walkable(vector7.x, vector7.y);
							if (!found || (flag && !foundWalkable))
							{
								spot = vector8;
								back = back3;
								found = true;
								foundWalkable = flag;
							}
							if (flag)
							{
								break;
							}
						}
					}
				}
			}
		}
		string text = "false";
		string text2 = "";
		string text3 = "null";
		float num14 = 0f;
		bool ok = falsePos == 0 && found;
		if (found)
		{
			int before = safe.RescueCount;
			player.transform.position = spot + Vector3.up * 0.01f;
			player.vel = Vector3.zero;
			player.grounded = true;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 3.5f && safe.RescueCount == before)
			{
				player.Move(Mathf.Min(0.05f, Time.deltaTime), 0f, 0f, run: false, jumpPressed: false);
				yield return null;
			}
			num14 = Time.realtimeSinceStartup - t0;
			Vector3 position = player.transform.position;
			bool flag2 = safe.RescueCount > before && safe.LastReason == "fell";
			bool flag3 = position.y > spot.y + 0.5f;
			text = (flag2 ? "true" : "false");
			text2 = safe.LastReason ?? "";
			text3 = "[" + position.x.ToString("F2") + "," + position.y.ToString("F2") + "," + position.z.ToString("F2") + "]";
			ok = ok && flag2 && flag3;
		}
		fallJson = "{\"ok\":" + (ok ? "true" : "false") + ",\"tested\":" + tested + ",\"falsePositives\":" + falsePos + ",\"falseSamples\":[" + fp?.ToString() + "],\"found\":" + (found ? "true" : "false") + ",\"spotWalkable\":" + (foundWalkable ? "true" : "false") + ",\"spot\":[" + spot.x.ToString("F2") + "," + spot.y.ToString("F2") + "," + spot.z.ToString("F2") + "],\"expectedBack\":[" + back.x.ToString("F2") + "," + back.y.ToString("F2") + "," + back.z.ToString("F2") + "],\"end\":" + text3 + ",\"rescued\":" + text + ",\"reason\":\"" + text2 + "\",\"seconds\":" + num14.ToString("F2") + "}";
		if (!ok)
		{
			Fail("fall_failed", "掉层救援不对：" + fallJson);
		}
	}

	private string ShaderCheck()
	{
		GameObject gameObject = GameObject.Find("Sea");
		Material material = ((!(gameObject != null)) ? null : gameObject.GetComponent<Renderer>()?.sharedMaterial);
		Shader shader = Shader.Find("TideTown/Water");
		bool flag = shader != null && material != null && material.shader != null && material.shader.name == "TideTown/Water";
		if (!flag)
		{
			Fail("shader_failed", "水面着色器不在包里：find=" + ((shader != null) ? shader.name : "null") + " mat=" + ((material != null && material.shader != null) ? material.shader.name : "null"));
		}
		return "{\"ok\":" + (flag ? "true" : "false") + ",\"find\":\"" + ((shader != null) ? shader.name : "null") + "\",\"material\":\"" + ((material != null && material.shader != null) ? material.shader.name : "null") + "\"}";
	}

	private void Fail(string s, string why)
	{
		if (status == "running")
		{
			status = s;
		}
		reason = why;
		Debug.Log("WATERAUDIT FAIL " + s + " " + why);
	}

	private void Finish()
	{
		float num = Time.realtimeSinceStartup - startedAt;
		string contents = "{\"status\":\"" + status + "\",\"reason\":\"" + reason.Replace("\"", "'") + "\",\"seconds\":" + num.ToString("F1") + ",\"shader\":" + shaderJson + ",\"levels\":[" + levels?.ToString() + "],\"rise\":" + riseJson + ",\"crate\":" + crateJson + ",\"rescue\":" + rescueJson + ",\"fall\":" + fallJson + "}";
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "water-audit.json"), contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Debug.Log("WATERAUDIT " + status + " " + reason);
		Application.Quit((!(status == "passed")) ? 1 : 0);
	}
}
