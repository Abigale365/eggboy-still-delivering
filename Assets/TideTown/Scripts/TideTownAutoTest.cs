using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

// 自动巡检：跑一段路线、记录帧率、卡住次数、地面高度变化，最后写 JSON。
// 用法：-autotest 加在命令行。
public class TideTownAutoTest : MonoBehaviour
{
    public float duration = 24f;
    EggboyPlayer player;
    float t, sumFps, minFps, maxFps;
    int frames, stuckFrames;
    Vector3 lastPos;
    float maxGroundDelta;
    readonly StringBuilder route = new StringBuilder();

    public void Begin(EggboyPlayer p)
    {
        player = p;
        lastPos = p.transform.position;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        float start = Time.time;
        float nextSample = 0.5f;
        while (Time.time - start < duration)
        {
            yield return null;
            float dt = Time.deltaTime;
            if (dt <= 0f) continue;
            frames++;
            float fps = 1f / dt;
            sumFps += fps;
            if (frames > 10)
            {
                minFps = minFps == 0f ? fps : Mathf.Min(minFps, fps);
                maxFps = Mathf.Max(maxFps, fps);
            }
            var p = player.transform.position;
            if ((p - lastPos).magnitude < 0.02f * dt * 60f && player.Speed < 0.05f && player.locked == false)
                stuckFrames++;
            lastPos = p;

            if (Time.time - start >= nextSample)
            {
                nextSample += 0.5f;
                route.Append($"({p.x:F1},{p.y:F2},{p.z:F1}) ");
            }
        }

        float avg = frames > 0 ? sumFps / frames : 0f;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"avgFps\": {avg:F1},");
        sb.AppendLine($"  \"minFps\": {minFps:F1},");
        sb.AppendLine($"  \"maxFps\": {maxFps:F1},");
        sb.AppendLine($"  \"frames\": {frames},");
        sb.AppendLine($"  \"duration\": {duration},");
        sb.AppendLine($"  \"finalPos\": \"{player.transform.position}\",");
        sb.AppendLine($"  \"route\": \"{route.ToString().Trim()}\"");
        sb.AppendLine("}");
        string dir = Path.Combine(Application.persistentDataPath, "..");
        string path = Path.Combine(Application.dataPath, "..", "autotest.json");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("AUTOTEST " + path + " avg=" + avg.ToString("F1") + " min=" + minFps.ToString("F1"));
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }
}
