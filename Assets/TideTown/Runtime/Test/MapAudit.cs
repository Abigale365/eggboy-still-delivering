// WP-16 地图验收：5 个点的红点投影、读信和地图互斥。
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public class MapAudit : MonoBehaviour
{
    [System.Serializable] class Wp { public string id; public bool stop; public float x, z; }
    [System.Serializable] class RouteList { public Wp[] route; }

    GameBootstrap boot;
    string status = "passed", error = "";
    readonly StringBuilder items = new StringBuilder();

    public void Begin(GameBootstrap b) { boot = b; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(0.8f);
        Time.timeScale = 1f;
        var p = boot.player;
        var map = FindObjectOfType<TownMap>();
        if (map == null) { status = "no_map"; error = "TownMap 没挂上"; Finish(); yield break; }

        var asset = Resources.Load<TextAsset>("World/route");
        var route = JsonUtility.FromJson<RouteList>(asset.text).route;
        string[] ids = { "03", "07", "11", "20", "31" };
        bool ok = true;

        foreach (var id in ids)
        {
            Wp stop = null;
            foreach (var r in route) if (r.stop && r.id == id) { stop = r; break; }
            if (stop == null) { ok = false; error += "missing_" + id + ";"; continue; }

            float y = boot.world.GroundAt(stop.x, stop.z);
            p.transform.position = new Vector3(stop.x, y + 0.02f, stop.z);
            p.vel = Vector3.zero;
            p.grounded = true;
            yield return null;
            yield return null;

            map.Toggle();
            yield return new WaitForSecondsRealtime(0.25f);
            bool open = map.IsOpen && !boot.ui.LetterOpen;
            var uv = map.ProjectUV(p.transform.position);
            if (open) yield return Capture("map_" + id + ".png");
            else { ok = false; error += "not_open_" + id + ";"; }
            if (uv.x < 0f || uv.x > 1774f || uv.y < 0f || uv.y > 887f) { ok = false; error += "uv_" + id + ";"; }

            items.Append(items.Length == 0 ? "" : ",").Append("{\"id\":\"").Append(id)
                 .Append("\",\"x\":").Append(p.transform.position.x.ToString("F2"))
                 .Append(",\"z\":").Append(p.transform.position.z.ToString("F2"))
                 .Append(",\"uv\":[").Append(uv.x.ToString("F1")).Append(',').Append(uv.y.ToString("F1")).Append("]}");

            map.Close();
            yield return null;
        }

        // 读信时打不开地图
        foreach (var r in route)
            if (r.stop && r.id == "03")
            {
                p.transform.position = new Vector3(r.x, boot.world.GroundAt(r.x, r.z) + 0.02f, r.z);
                break;
            }
        yield return null;
        bool letterBlocks = false;
        bool mapCloses = false;
        if (boot.service.TryInteract("mailbox_03"))
        {
            for (int i = 0; i < 90 && !boot.ui.LetterOpen; i++) yield return null;
            if (boot.ui.LetterOpen)
            {
                map.Toggle();
                yield return new WaitForSecondsRealtime(0.2f);
                letterBlocks = !map.IsOpen;
                yield return Capture("map_letter_blocked.png");

                // 地图开着时来了信：地图自己让位，两个界面不叠
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
        else error += "mailbox_failed;";

        if (!letterBlocks) { ok = false; error += "letter_blocks_map;"; }
        if (!mapCloses) { ok = false; error += "map_closes_on_letter;"; }
        if (!ok) status = "map_failed";

        WriteJson(letterBlocks, mapCloses);
        Finish();
        yield break;
    }

    IEnumerator Capture(string file)
    {
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    void WriteJson(bool letterBlocks, bool mapCloses)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"status\": \"" + status + "\",");
        sb.AppendLine("  \"points\": [" + items + "],");
        sb.AppendLine("  \"letter_blocks_map\": " + (letterBlocks ? "true" : "false") + ",");
        sb.AppendLine("  \"map_closes_on_letter\": " + (mapCloses ? "true" : "false") + ",");
        sb.AppendLine("  \"errors\": \"" + error + "\"");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "map-audit.json"), sb.ToString(), new UTF8Encoding(false));
    }

    void Finish()
    {
        Debug.Log("MAPAUDIT " + status + " " + error);
        Application.Quit();
    }
}
