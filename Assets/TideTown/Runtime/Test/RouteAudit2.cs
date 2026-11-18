using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class RouteAudit2 : MonoBehaviour
{
	[Serializable]
	private class Point
	{
		public string id;

		public bool stop;

		public float x;

		public float z;
	}

	[Serializable]
	private class RouteFile
	{
		public Point[] route;
	}

	private const float ReachVia = 0.35f;

	private const float ReachStop = 0.5f;

	private const float StuckSeconds = 2f;

	private const float StuckDistance = 0.05f;

	private const float MaxRealSeconds = 240f;

	private const float GroundTolerance = 0.15f;

	private static readonly string[] ShotStops = new string[5] { "11", "19", "LOW", "31", "05" };

	private readonly HashSet<string> shotDone = new HashSet<string>();

	private EggboyPlayer player;

	private PrototypeGame game;

	private IslandWorld world;

	private Point[] route;

	private string status = "running";

	private string reason = "";

	private int reached;

	private int totalStops;

	private int airFrames;

	private float maxGroundGap;

	private float startedAt;

	private float maxStepPerMetre;

	private Vector3 failPos;

	private readonly StringBuilder steps = new StringBuilder();

	private readonly StringBuilder events = new StringBuilder();

	private int eventCount;

	private bool wasGrounded = true;

	private string gapAt = "";

	public void Begin(EggboyPlayer p, PrototypeGame g)
	{
		player = p;
		game = g;
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return null;
		game.enabled = false;
		startedAt = Time.realtimeSinceStartup;
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		if (textAsset == null || world == null)
		{
			Finish("setup_failed", "缺 route.json 或 IslandWorld");
			yield break;
		}
		route = JsonUtility.FromJson<RouteFile>(textAsset.text).route;
		totalStops = Array.FindAll(route, (Point r) => r.stop).Length;
		Debug.Log("ROUTE2 start waypoints=" + route.Length + " stops=" + totalStops);
		Vector3 position = new Vector3(route[0].x, world.GroundAt(route[0].x, route[0].z), route[0].z);
		player.transform.position = position;
		reached = 1;
		AppendStep(route[0].id, 0f);
		WriteJson();
		for (int i = 1; i < route.Length; i++)
		{
			float t0 = Time.realtimeSinceStartup;
			yield return Walk(route[i], route[i - 1]);
			if (status != "running")
			{
				break;
			}
			if (route[i].stop)
			{
				reached++;
				AppendStep(route[i].id, Time.realtimeSinceStartup - t0);
				WriteJson();
				if (Array.IndexOf(ShotStops, route[i].id) >= 0 && shotDone.Add(route[i].id))
				{
					yield return FeetShot(route[i].id);
				}
			}
		}
		Finish((!(status == "running")) ? status : ((airFrames == 0) ? "passed" : "fell"), reason);
	}

	private IEnumerator Walk(Point to, Point from)
	{
		float radius = (to.stop ? 0.5f : 0.35f);
		float lastCheck = Time.realtimeSinceStartup;
		Vector3 lastPos = player.transform.position;
		Vector3 position;
		float num;
		float num2;
		float num3;
		while (true)
		{
			position = player.transform.position;
			num = to.x - position.x;
			num2 = to.z - position.z;
			num3 = Mathf.Sqrt(num * num + num2 * num2);
			if (num3 < radius)
			{
				yield break;
			}
			if (Time.realtimeSinceStartup - startedAt > 240f)
			{
				Fail("timeout", "总时限 " + 240f + " 秒", from, to);
				yield break;
			}
			if (Time.realtimeSinceStartup - lastCheck > 2f)
			{
				if ((position - lastPos).magnitude < 0.05f)
				{
					break;
				}
				lastCheck = Time.realtimeSinceStartup;
				lastPos = position;
			}
			float y = position.y;
			float dt = Mathf.Min(0.05f, Time.deltaTime);
			player.Move(dt, num / num3, num2 / num3, run: true, jumpPressed: false);
			Vector3 position2 = player.transform.position;
			float num4 = world.GroundAt(position2.x, position2.z, position2.y);
			if (!player.grounded)
			{
				airFrames++;
				if (wasGrounded && eventCount < 12)
				{
					eventCount++;
					events.Append((events.Length == 0) ? "" : ",").Append("[\"").Append(from.id)
						.Append('>')
						.Append(to.id)
						.Append("\",")
						.Append(position2.x.ToString("F2"))
						.Append(',')
						.Append(position2.z.ToString("F2"))
						.Append(',')
						.Append(y.ToString("F2"))
						.Append(',')
						.Append(num4.ToString("F2"))
						.Append(']');
				}
			}
			else
			{
				float num5 = position2.y - num4;
				if (num5 > maxGroundGap)
				{
					maxGroundGap = num5;
					gapAt = from.id + ">" + to.id + " " + position2.x.ToString("F2") + "," + position2.z.ToString("F2") + " y=" + position2.y.ToString("F2") + " ground=" + num4.ToString("F2");
				}
			}
			wasGrounded = player.grounded;
			float magnitude = new Vector2(position2.x - position.x, position2.z - position.z).magnitude;
			if (magnitude > 0.02f)
			{
				maxStepPerMetre = Mathf.Max(maxStepPerMetre, Mathf.Abs(position2.y - y) / magnitude);
			}
			yield return null;
		}
		Fail("blocked_at_" + to.id, WhyBlocked(position, num / num3, num2 / num3), from, to);
	}

	private IEnumerator FeetShot(string id)
	{
		Camera main = Camera.main;
		Vector3 position = player.transform.position;
		Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].enabled = false;
		}
		main.fieldOfView = 40f;
		main.nearClipPlane = 0.05f;
		main.transform.position = position + new Vector3(1.3f, 0.45f, -1.3f);
		main.transform.LookAt(position + Vector3.up * 0.25f);
		yield return new WaitForEndOfFrame();
		yield return null;
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, "..", "feet_" + id + ".png"));
		yield return null;
		yield return null;
	}

	private string WhyBlocked(Vector3 p, float dirX, float dirZ)
	{
		float num = p.x + dirX * 0.2f;
		float num2 = p.z + dirZ * 0.2f;
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(world.Walkable(num, num2) ? "前方可走区域内" : "前方不在可走区域");
		foreach (EggboyCollider collider in world.Colliders)
		{
			if (!collider.water && !collider.top.HasValue && collider.hw.HasValue && Mathf.Abs(num - collider.x) < collider.hw.Value + 0.42f && Mathf.Abs(num2 - collider.z) < collider.hd.Value + 0.42f)
			{
				stringBuilder.Append($"；撞到碰撞盒 中心({collider.x:F1},{collider.z:F1})");
			}
		}
		return stringBuilder.ToString();
	}

	private void Fail(string s, string why, Point from, Point to)
	{
		status = s;
		reason = $"{from.id}→{to.id}（目标 {to.x:F2},{to.z:F2}）：{why}";
		failPos = player.transform.position;
		string[] obj = new string[6] { "ROUTE2 FAIL ", status, " ", reason, " at ", null };
		Vector3 vector = failPos;
		obj[5] = vector.ToString();
		Debug.Log(string.Concat(obj));
	}

	private void AppendStep(string id, float seconds)
	{
		steps.Append((steps.Length == 0) ? "" : ",").Append("[\"").Append(id)
			.Append("\",")
			.Append(seconds.ToString("F1"))
			.Append(']');
	}

	private void WriteJson()
	{
		Vector3 position = player.transform.position;
		string contents = "{\"status\":\"" + status + "\",\"reached\":" + reached + ",\"total\":" + totalStops + ",\"reason\":\"" + reason.Replace("\"", "'") + "\",\"position\":[" + position.x.ToString("F2") + "," + position.y.ToString("F2") + "," + position.z.ToString("F2") + "],\"airFrames\":" + airFrames + ",\"maxGroundGap\":" + maxGroundGap.ToString("F3") + ",\"groundTolerance\":" + 0.15f.ToString("F2") + ",\"maxGapAt\":\"" + gapAt + "\",\"airEvents\":[" + events?.ToString() + "],\"maxHeightChangePerMetre\":" + maxStepPerMetre.ToString("F2") + ",\"realSeconds\":" + (Time.realtimeSinceStartup - startedAt).ToString("F1") + ",\"steps\":[" + steps?.ToString() + "]}";
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "route-audit.json"), contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private void Finish(string s, string why)
	{
		status = s;
		reason = why;
		if (s == "passed" && maxGroundGap > 0.15f)
		{
			status = "floating";
		}
		WriteJson();
		Debug.Log("ROUTE2 " + status + " reached=" + reached + "/" + totalStops + " " + reason);
		Application.Quit();
	}
}
