using System.Collections;
using System.IO;
using UnityEngine;

public class ExpandedRouteAudit : MonoBehaviour
{
	private EggboyPlayer player;

	private PrototypeGame game;

	private readonly Vector3[] points = new Vector3[12]
	{
		new Vector3(-75f, 0f, 0f),
		new Vector3(-38f, 0f, 0f),
		new Vector3(-10f, 0f, 0f),
		new Vector3(18f, 0f, 0f),
		new Vector3(44f, 0f, 0f),
		new Vector3(44f, 0f, 17f),
		new Vector3(44f, 0f, 31f),
		new Vector3(44f, 0f, 17f),
		new Vector3(35f, 0f, 0f),
		new Vector3(35f, 0f, -32f),
		new Vector3(-61f, 0f, -32f),
		new Vector3(-61f, 0f, 0f)
	};

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
		int reached = 0;
		string status = "passed";
		Time.timeScale = 4f;
		for (int i = 0; i < points.Length; i++)
		{
			float started = Time.realtimeSinceStartup;
			while (true)
			{
				Vector3 position = player.transform.position;
				Vector3 vector = points[i] - position;
				vector.y = 0f;
				if (vector.magnitude < 0.65f)
				{
					reached++;
					break;
				}
				if (Time.realtimeSinceStartup - started > 25f)
				{
					status = "blocked_at_" + i;
					break;
				}
				vector.Normalize();
				player.Move(Mathf.Min(0.05f, Time.deltaTime), vector.x, vector.z, run: true, jumpPressed: false);
				yield return null;
			}
			if (status != "passed")
			{
				break;
			}
		}
		Time.timeScale = 1f;
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "route-audit.json"), "{\"status\":\"" + status + "\",\"reached\":" + reached + ",\"total\":" + points.Length + ",\"position\":\"" + player.transform.position.ToString() + "\"}");
		Debug.Log("ROUTE_AUDIT " + status + " reached=" + reached + "/" + points.Length);
		Application.Quit();
	}
}
