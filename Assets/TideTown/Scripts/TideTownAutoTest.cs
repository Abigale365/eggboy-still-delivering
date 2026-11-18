using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public class TideTownAutoTest : MonoBehaviour
{
	public float duration = 24f;

	private EggboyPlayer player;

	private float t;

	private float sumFps;

	private float minFps;

	private float maxFps;

	private int frames;

	private int stuckFrames;

	private Vector3 lastPos;

	private float maxGroundDelta;

	private readonly StringBuilder route = new StringBuilder();

	public void Begin(EggboyPlayer p)
	{
		player = p;
		lastPos = p.transform.position;
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		float start = Time.time;
		float nextSample = 0.5f;
		while (Time.time - start < duration)
		{
			yield return null;
			float deltaTime = Time.deltaTime;
			if (!(deltaTime <= 0f))
			{
				frames++;
				float num = 1f / deltaTime;
				sumFps += num;
				if (frames > 10)
				{
					minFps = ((minFps == 0f) ? num : Mathf.Min(minFps, num));
					maxFps = Mathf.Max(maxFps, num);
				}
				Vector3 position = player.transform.position;
				if ((position - lastPos).magnitude < 0.02f * deltaTime * 60f && player.Speed < 0.05f && !player.locked)
				{
					stuckFrames++;
				}
				lastPos = position;
				if (Time.time - start >= nextSample)
				{
					nextSample += 0.5f;
					route.Append($"({position.x:F1},{position.y:F2},{position.z:F1}) ");
				}
			}
		}
		float num2 = ((frames > 0) ? (sumFps / (float)frames) : 0f);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine($"  \"avgFps\": {num2:F1},");
		stringBuilder.AppendLine($"  \"minFps\": {minFps:F1},");
		stringBuilder.AppendLine($"  \"maxFps\": {maxFps:F1},");
		stringBuilder.AppendLine($"  \"frames\": {frames},");
		stringBuilder.AppendLine($"  \"duration\": {duration},");
		stringBuilder.AppendLine($"  \"finalPos\": \"{player.transform.position}\",");
		stringBuilder.AppendLine("  \"route\": \"" + route.ToString().Trim() + "\"");
		stringBuilder.AppendLine("}");
		Path.Combine(Application.persistentDataPath, "..");
		string text = Path.Combine(Application.dataPath, "..", "autotest.json");
		File.WriteAllText(text, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Debug.Log("AUTOTEST " + text + " avg=" + num2.ToString("F1") + " min=" + minFps.ToString("F1"));
		yield return new WaitForSeconds(1f);
		Application.Quit();
	}
}
