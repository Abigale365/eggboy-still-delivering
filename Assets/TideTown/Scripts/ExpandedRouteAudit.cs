using System.Collections;
using System.IO;
using UnityEngine;

// Optional automated traversal against the real movement and collision code.
public class ExpandedRouteAudit : MonoBehaviour
{
    EggboyPlayer player;
    PrototypeGame game;
    readonly Vector3[] points = { new Vector3(-75,0,0), new Vector3(-38,0,0), new Vector3(-10,0,0), new Vector3(18,0,0), new Vector3(44,0,0), new Vector3(44,0,17), new Vector3(44,0,31), new Vector3(44,0,17), new Vector3(35,0,0), new Vector3(35,0,-32), new Vector3(-61,0,-32), new Vector3(-61,0,0) };
    public void Begin(EggboyPlayer p, PrototypeGame g) { player = p; game = g; StartCoroutine(Run()); }
    IEnumerator Run()
    {
        yield return null;
        game.enabled = false;
        int reached = 0; string status = "passed"; Time.timeScale = 4f;
        for (int i = 0; i < points.Length; i++)
        {
            float started = Time.realtimeSinceStartup;
            while (true)
            {
                var pos = player.transform.position; var d = points[i] - pos; d.y = 0;
                if (d.magnitude < 0.65f) { reached++; break; }
                if (Time.realtimeSinceStartup - started > 25f) { status = "blocked_at_" + i; break; }
                d.Normalize(); player.Move(Mathf.Min(.05f, Time.deltaTime), d.x, d.z, true, false);
                yield return null;
            }
            if (status != "passed") break;
        }
        Time.timeScale = 1f;
        string path = Path.Combine(Application.dataPath, "..", "route-audit.json");
        File.WriteAllText(path, "{\"status\":\"" + status + "\",\"reached\":" + reached + ",\"total\":" + points.Length + ",\"position\":\"" + player.transform.position + "\"}");
        Debug.Log("ROUTE_AUDIT " + status + " reached=" + reached + "/" + points.Length);
        Application.Quit();
    }
}
