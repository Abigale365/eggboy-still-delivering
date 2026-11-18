using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class LiveLux3DCheck : MonoBehaviour
{
	private GameBootstrap boot;

	private string prompt = "一只戴着小红围巾的胖海鸥木雕";

	public void Begin(GameBootstrap b)
	{
		boot = b;
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i + 1 < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == "-lux3dlive" && !commandLineArgs[i + 1].StartsWith("-"))
			{
				prompt = commandLineArgs[i + 1];
			}
			if (commandLineArgs[i] == "-lux3dtask")
			{
				Lux3DService.DebugResumeTaskId = commandLineArgs[i + 1];
			}
		}
		if (boot.ui != null)
		{
			boot.ui.SetAuditMode(on: true);
		}
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return new WaitForSeconds(1.5f);
		Lux3DService lux3DService = Lux3DService.Ensure();
		lux3DService.SetMode("dev-direct");
		string mode = lux3DService.Mode;
		float seconds = 0f;
		string status;
		string note;
		if (mode != "dev-direct")
		{
			status = "no_direct_gateway";
			note = "不是开发版，或者没找到密钥 / 账本";
		}
		else
		{
			AIAgentCalibrationDirector aIAgentCalibrationDirector = UnityEngine.Object.FindObjectOfType<AIAgentCalibrationDirector>();
			EggboyPlayer player = boot.player;
			player.transform.position = FindOpen(player.transform.position);
			aIAgentCalibrationDirector.SpawnLiveWishObjectInWorld(prompt);
			Lux3DSpawn s = aIAgentCalibrationDirector.LastWishSpawn;
			yield return Snap("lux3d_live_fallback.png", s.root.transform.position);
			float t0 = Time.realtimeSinceStartup;
			while (s.state == "generating" && Time.realtimeSinceStartup - t0 < 480f)
			{
				yield return null;
			}
			yield return new WaitForSeconds(1.2f);
			seconds = Time.realtimeSinceStartup - t0;
			status = ((s.state == "swapped") ? "passed" : "failed");
			note = "state=" + s.state + " gateway=" + s.seconds.ToString("F0") + "s";
			if (s.root != null)
			{
				yield return Snap("lux3d_live_swapped.png", s.root.transform.position);
			}
		}
		string contents = "{\n  \"status\": \"" + status + "\",\n  \"mode\": \"" + mode + "\",\n  \"prompt\": \"" + prompt.Replace("\"", "'") + "\",\n  \"seconds\": " + seconds.ToString("F1", CultureInfo.InvariantCulture) + ",\n  \"note\": \"" + note.Replace("\"", "'") + "\"\n}\n";
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "lux3d-live.json"), contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Debug.Log("LUX3DLIVE " + status + " " + note);
		yield return new WaitForSecondsRealtime(0.5f);
		Application.Quit();
	}

	private Vector3 FindOpen(Vector3 c)
	{
		IslandWorld world = boot.world;
		for (float num = 0f; num < 6f; num += 0.5f)
		{
			for (int i = 0; i < 12; i++)
			{
				float f = (float)i * MathF.PI / 6f;
				Vector3 result = c + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
				if (world.Walkable(result.x, result.z))
				{
					result.y = world.GroundAt(result.x, result.z, c.y + 1f);
					return result;
				}
			}
		}
		return c;
	}

	private IEnumerator Snap(string file, Vector3 target)
	{
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		Camera main = Camera.main;
		ChaseCamera chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		if (chase != null)
		{
			chase.enabled = false;
		}
		Vector3 vector = target + new Vector3(1.4f, 1.1f, -1.6f);
		main.transform.SetPositionAndRotation(vector, Quaternion.LookRotation(target + Vector3.up * 0.25f - vector, Vector3.up));
		yield return null;
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
		yield return new WaitForSecondsRealtime(0.6f);
		if (chase != null)
		{
			chase.enabled = true;
		}
	}
}
