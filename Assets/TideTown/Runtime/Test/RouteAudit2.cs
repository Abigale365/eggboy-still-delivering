// WP-06 路线审计：沿 route.json 的固定路径点，用真实 EggboyPlayer.Move 走完工程书的 28 站。
// 不寻路、不绕行、不重试、不挪位置。走不通就停下，写清楚卡在哪、为什么。
// 每到一站就把进度写进 route-audit.json，总时限到了也会写完再退出，不会一直挂着。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class RouteAudit2 : MonoBehaviour
{
    [Serializable] class Point { public string id; public bool stop; public float x, z; }
    [Serializable] class RouteFile { public Point[] route; }

    const float ReachVia = 0.35f, ReachStop = 0.5f;
    const float StuckSeconds = 2f, StuckDistance = 0.05f;
    const float MaxRealSeconds = 240f;
    // 脚和地面的最大差。角色高度靠 EggboyPlayer 的平滑跟地（不能改），30° 坡顶会有约 8 cm 的瞬时差。
    // 迈步平滑会让 y 在台阶边短暂落后于新地面；Step 是 0.22，0.15 以内属于正常过渡，
    // 真正的悬空（>15cm 的持续误差）仍会判 floating
    const float GroundTolerance = 0.15f;
    // 工程书 WP-06：抽查 5 张截图看鞋底
    static readonly string[] ShotStops = { "11", "19", "LOW", "31", "05" };
    readonly HashSet<string> shotDone = new HashSet<string>();

    EggboyPlayer player;
    PrototypeGame game;
    IslandWorld world;
    Point[] route;

    string status = "running";
    string reason = "";
    int reached, totalStops, airFrames;
    float maxGroundGap, startedAt, maxStepPerMetre;
    Vector3 failPos;
    readonly StringBuilder steps = new StringBuilder();
    readonly StringBuilder events = new StringBuilder();
    int eventCount;
    bool wasGrounded = true;
    string gapAt = "";

    public void Begin(EggboyPlayer p, PrototypeGame g) { player = p; game = g; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        yield return null;
        game.enabled = false;
        startedAt = Time.realtimeSinceStartup;
        var asset = Resources.Load<TextAsset>("World/route");
        world = FindObjectOfType<IslandWorld>();
        if (asset == null || world == null) { Finish("setup_failed", "缺 route.json 或 IslandWorld"); yield break; }
        route = JsonUtility.FromJson<RouteFile>(asset.text).route;
        totalStops = Array.FindAll(route, r => r.stop).Length;
        Debug.Log("ROUTE2 start waypoints=" + route.Length + " stops=" + totalStops);

        // 只有起点是直接放上去的，和游戏出生一样
        var p0 = new Vector3(route[0].x, world.GroundAt(route[0].x, route[0].z), route[0].z);
        player.transform.position = p0;
        reached = 1;
        AppendStep(route[0].id, 0f);
        WriteJson();

        // 1 倍速：和玩家实际运行一样，加速会放大每帧的落差和跟地误差
        for (int i = 1; i < route.Length; i++)
        {
            float t0 = Time.realtimeSinceStartup;
            yield return Walk(route[i], route[i - 1]);
            if (status != "running") break;
            if (route[i].stop)
            {
                reached++;
                AppendStep(route[i].id, Time.realtimeSinceStartup - t0);
                WriteJson();
                if (Array.IndexOf(ShotStops, route[i].id) >= 0 && shotDone.Add(route[i].id)) yield return FeetShot(route[i].id);
            }
        }
        Finish(status == "running" ? (airFrames == 0 ? "passed" : "fell") : status, reason);
    }

    IEnumerator Walk(Point to, Point from)
    {
        float radius = to.stop ? ReachStop : ReachVia;
        float lastCheck = Time.realtimeSinceStartup;
        var lastPos = player.transform.position;
        while (true)
        {
            var pos = player.transform.position;
            float dx = to.x - pos.x, dz = to.z - pos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist < radius) yield break;

            if (Time.realtimeSinceStartup - startedAt > MaxRealSeconds)
            {
                Fail("timeout", "总时限 " + MaxRealSeconds + " 秒", from, to);
                yield break;
            }
            if (Time.realtimeSinceStartup - lastCheck > StuckSeconds)
            {
                if ((pos - lastPos).magnitude < StuckDistance)
                {
                    Fail("blocked_at_" + to.id, WhyBlocked(pos, dx / dist, dz / dist), from, to);
                    yield break;
                }
                lastCheck = Time.realtimeSinceStartup;
                lastPos = pos;
            }

            float before = pos.y;
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            player.Move(dt, dx / dist, dz / dist, true, false);

            var now = player.transform.position;
            float groundNow = world.GroundAt(now.x, now.z, now.y);
            if (!player.grounded)
            {
                airFrames++;
                if (wasGrounded && eventCount < 12)
                {
                    eventCount++;
                    events.Append(events.Length == 0 ? "" : ",").Append("[\"").Append(from.id).Append('>').Append(to.id).Append("\",")
                          .Append(now.x.ToString("F2")).Append(',').Append(now.z.ToString("F2")).Append(',')
                          .Append(before.ToString("F2")).Append(',').Append(groundNow.ToString("F2")).Append(']');
                }
            }
            else
            {
                // 只算悬空（人比地面高）；上坡时人短暂低于地面是正常的迈步平滑，不算问题
                float gap = now.y - groundNow;
                if (gap > maxGroundGap)
                {
                    maxGroundGap = gap;
                    gapAt = from.id + ">" + to.id + " " + now.x.ToString("F2") + "," + now.z.ToString("F2") + " y=" + now.y.ToString("F2") + " ground=" + groundNow.ToString("F2");
                }
            }
            wasGrounded = player.grounded;
            float moved = new Vector2(now.x - pos.x, now.z - pos.z).magnitude;
            if (moved > 0.02f) maxStepPerMetre = Mathf.Max(maxStepPerMetre, Mathf.Abs(now.y - before) / moved);
            yield return null;
        }
    }

    // 低机位看脚：站定后从角色前方侧面拍，存到 <包目录>/feet_<站>.png
    IEnumerator FeetShot(string id)
    {
        var cam = Camera.main;
        var p = player.transform.position;
        foreach (var c in FindObjectsOfType<Canvas>()) c.enabled = false;
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.05f;
        cam.transform.position = p + new Vector3(1.3f, 0.45f, -1.3f);
        cam.transform.LookAt(p + Vector3.up * 0.25f);
        yield return new WaitForEndOfFrame();
        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, "..", "feet_" + id + ".png"));
        yield return null;
        yield return null;
    }

    string WhyBlocked(Vector3 p, float dirX, float dirZ)
    {
        float ax = p.x + dirX * 0.2f, az = p.z + dirZ * 0.2f;
        var sb = new StringBuilder();
        sb.Append(world.Walkable(ax, az) ? "前方可走区域内" : "前方不在可走区域");
        foreach (var c in world.Colliders)
        {
            if (c.water || c.top.HasValue || !c.hw.HasValue) continue;
            if (Mathf.Abs(ax - c.x) < c.hw.Value + 0.42f && Mathf.Abs(az - c.z) < c.hd.Value + 0.42f)
                sb.Append($"；撞到碰撞盒 中心({c.x:F1},{c.z:F1})");
        }
        return sb.ToString();
    }

    void Fail(string s, string why, Point from, Point to)
    {
        status = s;
        reason = $"{from.id}→{to.id}（目标 {to.x:F2},{to.z:F2}）：{why}";
        failPos = player.transform.position;
        Debug.Log("ROUTE2 FAIL " + status + " " + reason + " at " + failPos);
    }

    void AppendStep(string id, float seconds)
    {
        steps.Append(steps.Length == 0 ? "" : ",").Append("[\"").Append(id).Append("\",").Append(seconds.ToString("F1")).Append(']');
    }

    void WriteJson()
    {
        var p = player.transform.position;
        string json = "{\"status\":\"" + status + "\",\"reached\":" + reached + ",\"total\":" + totalStops +
            ",\"reason\":\"" + reason.Replace("\"", "'") + "\",\"position\":[" + p.x.ToString("F2") + "," + p.y.ToString("F2") + "," + p.z.ToString("F2") + "]" +
            ",\"airFrames\":" + airFrames + ",\"maxGroundGap\":" + maxGroundGap.ToString("F3") + ",\"groundTolerance\":" + GroundTolerance.ToString("F2") +
            ",\"maxGapAt\":\"" + gapAt + "\",\"airEvents\":[" + events + "]" +
            ",\"maxHeightChangePerMetre\":" + maxStepPerMetre.ToString("F2") +
            ",\"realSeconds\":" + (Time.realtimeSinceStartup - startedAt).ToString("F1") + ",\"steps\":[" + steps + "]}";
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "route-audit.json"), json, new UTF8Encoding(false));
    }

    void Finish(string s, string why)
    {
        status = s;
        reason = why;
        if (s == "passed" && maxGroundGap > GroundTolerance) status = "floating";
        WriteJson();
        Debug.Log("ROUTE2 " + status + " reached=" + reached + "/" + totalStops + " " + reason);
        Application.Quit();
    }
}
