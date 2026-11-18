using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public class MapAudit : MonoBehaviour
{
	[Serializable]
	private class Wp
	{
		public string id;

		public bool stop;

		public float x;

		public float z;
	}

	[Serializable]
	private class RouteList
	{
		public Wp[] route;
	}

	private GameBootstrap boot;

	private string status = "passed";

	private string error = "";

	private readonly StringBuilder items = new StringBuilder();

	private int targetsMatched;

	private const int TargetCount = 8;

	public void Begin(GameBootstrap b)
	{
		boot = b;
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return new WaitForSeconds(0.8f);
		Time.timeScale = 1f;
		EggboyPlayer p = boot.player;
		TownMap map = UnityEngine.Object.FindObjectOfType<TownMap>();
		if (map == null)
		{
			status = "no_map";
			error = "TownMap 没挂上";
			Finish();
			yield break;
		}
		CheckInteractionTargets(map);
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		Wp[] route = JsonUtility.FromJson<RouteList>(textAsset.text).route;
		string[] array = new string[5] { "03", "07", "11", "20", "31" };
		bool ok = targetsMatched == 8;
		string[] array2 = array;
		Wp[] array3;
		foreach (string id in array2)
		{
			Wp wp = null;
			array3 = route;
			foreach (Wp wp2 in array3)
			{
				if (wp2.stop && wp2.id == id)
				{
					wp = wp2;
					break;
				}
			}
			if (wp == null)
			{
				ok = false;
				error = error + "missing_" + id + ";";
				continue;
			}
			float num = boot.world.GroundAt(wp.x, wp.z);
			p.transform.position = new Vector3(wp.x, num + 0.02f, wp.z);
			p.vel = Vector3.zero;
			p.grounded = true;
			yield return null;
			yield return null;
			map.Toggle();
			yield return new WaitForSecondsRealtime(0.25f);
			bool num2 = map.IsOpen && !boot.ui.LetterOpen;
			Vector2 uv = map.ProjectUV(p.transform.position);
			if (num2)
			{
				yield return Capture("map_" + id + ".png");
			}
			else
			{
				ok = false;
				error = error + "not_open_" + id + ";";
			}
			if (uv.x < 0f || uv.x > 1774f || uv.y < 0f || uv.y > 887f)
			{
				ok = false;
				error = error + "uv_" + id + ";";
			}
			items.Append((items.Length == 0) ? "" : ",").Append("{\"id\":\"").Append(id)
				.Append("\",\"x\":")
				.Append(p.transform.position.x.ToString("F2"))
				.Append(",\"z\":")
				.Append(p.transform.position.z.ToString("F2"))
				.Append(",\"uv\":[")
				.Append(uv.x.ToString("F1"))
				.Append(',')
				.Append(uv.y.ToString("F1"))
				.Append("]}");
			map.Close();
			yield return null;
		}
		array3 = route;
		foreach (Wp wp3 in array3)
		{
			if (wp3.stop && wp3.id == "03")
			{
				p.transform.position = new Vector3(wp3.x, boot.world.GroundAt(wp3.x, wp3.z) + 0.02f, wp3.z);
				break;
			}
		}
		yield return null;
		bool letterBlocks = false;
		bool mapCloses = false;
		if (boot.service.TryInteract("mailbox_03"))
		{
			for (int i = 0; i < 90; i++)
			{
				if (boot.ui.LetterOpen)
				{
					break;
				}
				yield return null;
			}
			if (boot.ui.LetterOpen)
			{
				map.Toggle();
				yield return new WaitForSecondsRealtime(0.2f);
				letterBlocks = !map.IsOpen;
				yield return Capture("map_letter_blocked.png");
				boot.ui.CloseLetter();
				yield return new WaitForSecondsRealtime(0.3f);
				map.Toggle();
				yield return new WaitForSecondsRealtime(0.2f);
				if (map.IsOpen)
				{
					boot.ui.ShowLetterDetail("测试来信", "地图和信不应该同时出现。", "");
					yield return null;
					yield return null;
					mapCloses = !map.IsOpen;
					boot.ui.CloseLetter();
				}
			}
		}
		else
		{
			error += "mailbox_failed;";
		}
		if (!letterBlocks)
		{
			ok = false;
			error += "letter_blocks_map;";
		}
		if (!mapCloses)
		{
			ok = false;
			error += "map_closes_on_letter;";
		}
		if (!ok)
		{
			status = "map_failed";
		}
		WriteJson(letterBlocks, mapCloses);
		Finish();
	}

	private void CheckInteractionTargets(TownMap map)
	{
		(string, string)[] obj = new(string, string)[8]
		{
			("B1_matches", "stall"),
			("B2_wood", "wood"),
			("B3_load", "stove"),
			("S1_slipper", "slipper"),
			("S2_ball", "ball"),
			("L1_shed", "oil"),
			("L2_door", "lighthouse_door"),
			("L3_oil", "lamp_head")
		};
		string step = boot.state.step;
		(string, string)[] array = obj;
		for (int i = 0; i < array.Length; i++)
		{
			(string, string) tuple = array[i];
			boot.state.step = tuple.Item1;
			if (boot.service.TryGetPosition(tuple.Item2, out var position) && map.TryGetStepTarget(out var targetPos, out var _) && (targetPos - position).sqrMagnitude < 1E-06f)
			{
				targetsMatched++;
			}
			else
			{
				error = error + "target_" + tuple.Item2 + ";";
			}
		}
		boot.state.step = step;
	}

	private IEnumerator Capture(string file)
	{
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
		yield return new WaitForSecondsRealtime(0.6f);
	}

	private void WriteJson(bool letterBlocks, bool mapCloses)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine("  \"status\": \"" + status + "\",");
		stringBuilder.AppendLine("  \"points\": [" + items?.ToString() + "],");
		stringBuilder.AppendLine("  \"interaction_targets_matched\": " + targetsMatched + ",");
		stringBuilder.AppendLine("  \"interaction_targets_total\": " + 8 + ",");
		stringBuilder.AppendLine("  \"letter_blocks_map\": " + (letterBlocks ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"map_closes_on_letter\": " + (mapCloses ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"errors\": \"" + error + "\"");
		stringBuilder.AppendLine("}");
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "map-audit.json"), stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private void Finish()
	{
		Debug.Log("MAPAUDIT " + status + " " + error);
		Application.Quit();
	}
}
