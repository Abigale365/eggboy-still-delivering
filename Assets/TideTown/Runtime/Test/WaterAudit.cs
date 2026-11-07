// WP-08 验收（-wateraudit）：
//   1. 五档水位各从 PreviewCam 截一张图（shots/water_L0..L4.png）
//   2. L2 木箱区用真实 Move 走过去（不跳），全程不进水
//   3. 掉进水里后回到最近的安全点
//   4. 4 秒涨水过场：终点正确、单调上涨、镜头有抬高
//   5. 水面着色器在包里能 Shader.Find 到（WebGL 不丢的同一套判定）
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class WaterAudit : MonoBehaviour
{
    GameBootstrap boot;
    WaterLevel water;
    EggboyPlayer player;
    IslandWorld world;
    SafePoints safe;
    PrototypeGame game;
    Camera shotCam;
    Transform hero;

    string status = "running", reason = "";
    readonly StringBuilder levels = new StringBuilder();
    string crateJson = "{}";
    string rescueJson = "{}";
    string fallJson = "{}";
    string riseJson = "{}";
    string shaderJson = "{}";
    float startedAt;

    public void Begin(GameBootstrap b) { boot = b; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        yield return null;
        startedAt = Time.realtimeSinceStartup;
        water = FindObjectOfType<WaterLevel>();
        player = FindObjectOfType<EggboyPlayer>();
        world = FindObjectOfType<IslandWorld>();
        safe = FindObjectOfType<SafePoints>();
        game = FindObjectOfType<PrototypeGame>();

        if (water == null || player == null || world == null)
        {
            Fail("setup_failed", "缺 WaterLevel / EggboyPlayer / IslandWorld");
            yield break;
        }

        // 和 RouteAudit2 一样，接管角色，避免键盘输入和审计的 Move 叠加
        if (game != null) game.enabled = false;
        foreach (var c in FindObjectsOfType<Canvas>()) c.enabled = false;
        var heroGO = GameObject.Find("EGGBOY");
        if (heroGO != null) hero = heroGO.transform;

        shaderJson = ShaderCheck();
        if (status == "running") yield return Levels();
        if (status == "running") yield return Rise();
        if (status == "running") yield return CrateCross();
        if (status == "running") yield return Rescue();
        if (status == "running") yield return Fall();

        // 收尾：回到 L0
        water.SetInstant(WaterLevel.LevelYs[0]);
        if (status == "running") status = "passed";
        Finish();
    }

    // ---------- 五档截图 ----------

    IEnumerator Levels()
    {
        var def = ShotTool.FindCamera("preview");
        var main = Camera.main;
        var go = new GameObject("WaterShotCam");
        shotCam = go.AddComponent<Camera>();
        if (main != null) { shotCam.CopyFrom(main); main.enabled = false; }
        if (def != null)
        {
            shotCam.transform.SetPositionAndRotation(new Vector3(def.pos[0], def.pos[1], def.pos[2]),
                Quaternion.Euler(def.euler[0], def.euler[1], def.euler[2]));
            shotCam.fieldOfView = def.fov;
            shotCam.nearClipPlane = 1f;
            shotCam.farClipPlane = 800f;
        }
        if (hero != null) hero.gameObject.SetActive(false);

        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        int w = def != null && def.width > 0 ? def.width : 1774;
        int h = def != null && def.height > 0 ? def.height : 887;

        for (int i = 0; i < WaterLevel.LevelNames.Length; i++)
        {
            water.SetInstant(WaterLevel.LevelYs[i]);
            yield return null;
            yield return null;
            string file = "water_" + WaterLevel.LevelNames[i] + ".png";
            yield return ShotTool.Capture(shotCam, w, h, Path.Combine(dir, file));
            levels.Append(levels.Length == 0 ? "" : ",")
                  .Append("[\"").Append(WaterLevel.LevelNames[i]).Append("\",")
                  .Append(WaterLevel.LevelYs[i].ToString("F2")).Append(",\"").Append(file).Append("\"]");
        }

        if (hero != null) hero.gameObject.SetActive(true);
        if (shotCam != null) Destroy(shotCam.gameObject);
        if (main != null) main.enabled = true;
        water.SetInstant(WaterLevel.LevelYs[2]);
        yield return null;
    }

    // ---------- 4 秒涨水 ----------

    IEnumerator Rise()
    {
        water.SetInstant(WaterLevel.LevelYs[2]);
        yield return null;
        var chase = FindObjectOfType<ChaseCamera>();
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
            if (water.Y < lastY - 1e-3f) monotonic = false;
            lastY = water.Y;
            if (chase != null) maxLift = Mathf.Max(maxLift, chase.extraLift);
            if (Time.realtimeSinceStartup - t0 > 4.1f && Mathf.Abs(water.Y - WaterLevel.LevelYs[3]) < 0.02f) break;
        }
        float seconds = Time.realtimeSinceStartup - t0;
        bool ok = monotonic && Mathf.Abs(water.Y - WaterLevel.LevelYs[3]) < 0.02f && seconds <= 4.9f && maxLift > 0.3f;
        riseJson = "{\"ok\":" + (ok ? "true" : "false") + ",\"from\":" + from.ToString("F2") + ",\"to\":" + water.Y.ToString("F2") +
                   ",\"seconds\":" + seconds.ToString("F2") + ",\"samples\":" + samples +
                   ",\"monotonic\":" + (monotonic ? "true" : "false") + ",\"maxCameraLift\":" + maxLift.ToString("F2") + "}";
        if (!ok) Fail("rise_failed", "涨水过场不对：" + riseJson);
    }

    // ---------- 木箱区 ----------

    IEnumerator CrateCross()
    {
        water.SetInstant(WaterLevel.LevelYs[2]);
        yield return null;
        yield return null;

        // 2026-11-07：按场景里生成的浮箱算起点和方向，换地图不用改坐标
        var crates = new List<FloatingCrate>(FindObjectsOfType<FloatingCrate>());
        if (crates.Count < 2) { Fail("crate_failed", "场景里浮箱少于 2 个"); yield break; }
        crates.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var c0 = crates[0].transform.position; var c1 = crates[crates.Count - 1].transform.position;
        var dir = new Vector3(c1.x - c0.x, 0f, c1.z - c0.z).normalized;
        var start = c0 - dir * 1.4f;
        start.y = world.GroundAt(start.x, start.z) + 0.01f;

        // 过了箱子以后沿路线走到船棚门口（route.json 里 LOW 之后、直到 24 的路径点）：新地图路在箱子后面拐弯
        var targets = new List<Vector3> { c1 + dir * 0.3f };
        var rdoc = JsonUtility.FromJson<RouteDoc>(Resources.Load<TextAsset>("World/route").text);
        int li = Array.FindIndex(rdoc.route, r => r.stop && r.id == "LOW");
        for (int i = li + 1; li >= 0 && i < rdoc.route.Length; i++)
        {
            targets.Add(new Vector3(rdoc.route[i].x, 0f, rdoc.route[i].z));
            if (rdoc.route[i].stop) break;
        }
        player.transform.position = start;
        player.vel = Vector3.zero;
        player.grounded = true;
        yield return null;
        yield return null;

        float t0 = Time.realtimeSinceStartup;
        int waterFrames = 0;
        int frames = 0;
        int k = 0;
        float maxY = start.y;
        while (Time.realtimeSinceStartup - t0 < 20f && k < targets.Count)
        {
            var p = player.transform.position;
            var to = new Vector3(targets[k].x - p.x, 0f, targets[k].z - p.z);
            if (to.magnitude < 0.5f) { k++; continue; }
            if (p.y < water.Y - 0.05f) waterFrames++;
            maxY = Mathf.Max(maxY, p.y);
            to.Normalize();
            player.Move(Mathf.Min(0.05f, Time.deltaTime), to.x, to.z, false, false);
            frames++;
            yield return null;
        }

        var end = player.transform.position;
        float seconds = Time.realtimeSinceStartup - t0;
        var dock = GameObject.Find("P_26");
        var dockPos = dock != null ? dock.transform.position : Vector3.zero;
        var midCrate = crates[crates.Count / 2].transform.position;
        bool ok = k >= targets.Count && targets.Count > 1 && waterFrames == 0;
        crateJson = "{\"ok\":" + (ok ? "true" : "false") + ",\"from\":[" + start.x.ToString("F2") + "," + start.y.ToString("F2") + "," + start.z.ToString("F2") +
                    "],\"to\":[" + end.x.ToString("F2") + "," + end.y.ToString("F2") + "," + end.z.ToString("F2") +
                    "],\"seconds\":" + seconds.ToString("F2") + ",\"frames\":" + frames +
                    ",\"waterFrames\":" + waterFrames + ",\"maxY\":" + maxY.ToString("F2") +
                    ",\"jumpUsed\":false,\"walkableOnCrates\":" + (world.Walkable(midCrate.x, midCrate.z) ? "true" : "false") +
                    ",\"walkableInWater\":" + (world.Walkable(dockPos.x, dockPos.z) ? "true" : "false") + "}";
        if (!ok)
        {
            Fail("crate_failed", "木箱区没走过去：" + crateJson);
        }
    }

    // ---------- 掉水回安全点 ----------

    IEnumerator Rescue()
    {
        water.SetInstant(WaterLevel.LevelYs[2]);
        yield return null;
        if (safe == null) { Fail("rescue_failed", "场景里没有 SafePoints"); yield break; }

        int before = safe.RescueCount;
        // 码头（P_26）在 L2 被淹（地面 0.9，水位 1.15）
        var dockGo = GameObject.Find("P_26");
        if (dockGo == null) { Fail("rescue_failed", "场景里没有 P_26 码头点"); yield break; }
        var dp = dockGo.transform.position;
        var from = new Vector3(dp.x, world.GroundAt(dp.x, dp.z), dp.z);
        player.transform.position = from;
        player.vel = Vector3.zero;
        player.grounded = true;

        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 2f && safe.RescueCount == before)
            yield return null;

        var end = player.transform.position;
        bool rescued = safe.RescueCount > before;
        bool dry = end.y >= water.Y - 0.05f;
        bool ok = rescued && dry;
        rescueJson = "{\"ok\":" + (ok ? "true" : "false") + ",\"from\":[" + from.x.ToString("F2") + "," + from.y.ToString("F2") + "," + from.z.ToString("F2") +
                     "],\"to\":[" + end.x.ToString("F2") + "," + end.y.ToString("F2") + "," + end.z.ToString("F2") +
                     "],\"reason\":\"" + (safe.LastReason ?? "") + "\",\"seconds\":" + (Time.realtimeSinceStartup - t0).ToString("F2") +
                     ",\"dry\":" + (dry ? "true" : "false") + ",\"rescued\":" + (rescued ? "true" : "false") + "}";
        if (!ok) Fail("rescue_failed", "掉水后没回到安全点：" + rescueJson);
    }

    // ---------- 掉层救援（known-issues #14） ----------
    // 1. 误报：所有可走区按 1 米网格站一遍，正常地面上一个都不能判成掉层
    // 2. 真掉：在路线旁边找一处比路面低 0.6 米以上的地面，把角色放过去，2.5 秒内要被送回路面

    [Serializable] class RoutePt { public string id; public bool stop; public float x, z; }
    [Serializable] class RouteDoc { public RoutePt[] route; }

    IEnumerator Fall()
    {
        water.SetInstant(WaterLevel.LevelYs[0]);
        yield return null;
        if (safe == null) { Fail("fall_failed", "场景里没有 SafePoints"); yield break; }

        // 1. 误报
        int tested = 0, falsePos = 0;
        var fp = new StringBuilder();
        foreach (var poly in world.walkPolys)
        {
            if (poly.points == null || poly.points.Length < 3) continue;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var v in poly.points) { x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); z0 = Mathf.Min(z0, v.y); z1 = Mathf.Max(z1, v.y); }
            for (float x = Mathf.Ceil(x0); x <= x1; x += 1f)
            for (float z = Mathf.Ceil(z0); z <= z1; z += 1f)
            {
                if (!world.InWalkArea(x, z)) continue;
                float g = world.GroundAt(x, z, poly.y + 0.5f);
                if (g < 0.2f || g < water.Y - 0.05f) continue;
                tested++;
                if (safe.FellBelowRoute(new Vector3(x, g, z), out _))
                {
                    falsePos++;
                    if (falsePos <= 12) fp.Append(fp.Length == 0 ? "" : ",").Append("[\"").Append(poly.id).Append("\",")
                        .Append(x.ToString("F1")).Append(",").Append(g.ToString("F2")).Append(",").Append(z.ToString("F1")).Append("]");
                }
            }
        }

        // 2. 真掉：沿路线每 0.5 米，往两侧 0.8–2.4 米找低处
        var asset = Resources.Load<TextAsset>("World/route");
        var doc = asset != null ? JsonUtility.FromJson<RouteDoc>(asset.text) : null;
        Vector3 spot = Vector3.zero, back = Vector3.zero;
        bool found = false, foundWalkable = false;
        if (doc != null && doc.route != null)
        {
            for (int i = 0; i + 1 < doc.route.Length && !foundWalkable; i++)
            {
                var a = new Vector2(doc.route[i].x, doc.route[i].z);
                var b = new Vector2(doc.route[i + 1].x, doc.route[i + 1].z);
                float len = (b - a).magnitude;
                if (len < 0.1f) continue;
                var dir = (b - a) / len;
                var side = new Vector2(-dir.y, dir.x);
                for (float t = 0f; t <= len && !foundWalkable; t += 0.5f)
                {
                    var c = a + dir * t;
                    float roadY = world.GroundAt(c.x, c.y);
                    foreach (float s in new[] { 0.8f, 1.2f, 1.6f, 2.0f, 2.4f, -0.8f, -1.2f, -1.6f, -2.0f, -2.4f })
                    {
                        var q = c + side * s;
                        float g = world.GroundAt(q.x, q.y, roadY - 0.6f);
                        if (g < 0.2f || g < water.Y - 0.05f || roadY - g < 0.6f || roadY - g > 2.2f) continue;
                        var cand = new Vector3(q.x, g, q.y);
                        if (!safe.FellBelowRoute(cand, out var bk)) continue;
                        bool walk = world.Walkable(q.x, q.y);
                        if (!found || (walk && !foundWalkable)) { spot = cand; back = bk; found = true; foundWalkable = walk; }
                        if (walk) break;
                    }
                }
            }
        }

        string rescued = "false", reasonStr = "", endStr = "null";
        float secs = 0f;
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
                player.Move(Mathf.Min(0.05f, Time.deltaTime), 0f, 0f, false, false);
                yield return null;
            }
            secs = Time.realtimeSinceStartup - t0;
            var end = player.transform.position;
            bool r = safe.RescueCount > before && safe.LastReason == "fell";
            bool onRoad = end.y > spot.y + 0.5f;
            rescued = r ? "true" : "false";
            reasonStr = safe.LastReason ?? "";
            endStr = "[" + end.x.ToString("F2") + "," + end.y.ToString("F2") + "," + end.z.ToString("F2") + "]";
            ok = ok && r && onRoad;
        }

        fallJson = "{\"ok\":" + (ok ? "true" : "false") + ",\"tested\":" + tested + ",\"falsePositives\":" + falsePos +
                   ",\"falseSamples\":[" + fp + "],\"found\":" + (found ? "true" : "false") + ",\"spotWalkable\":" + (foundWalkable ? "true" : "false") +
                   ",\"spot\":[" + spot.x.ToString("F2") + "," + spot.y.ToString("F2") + "," + spot.z.ToString("F2") + "]" +
                   ",\"expectedBack\":[" + back.x.ToString("F2") + "," + back.y.ToString("F2") + "," + back.z.ToString("F2") + "]" +
                   ",\"end\":" + endStr + ",\"rescued\":" + rescued + ",\"reason\":\"" + reasonStr + "\",\"seconds\":" + secs.ToString("F2") + "}";
        if (!ok) Fail("fall_failed", "掉层救援不对：" + fallJson);
    }

    // ---------- 着色器 ----------

    string ShaderCheck()
    {
        var sea = GameObject.Find("Sea");
        var mat = sea != null ? sea.GetComponent<Renderer>()?.sharedMaterial : null;
        var found = Shader.Find("TideTown/Water");
        bool ok = found != null && mat != null && mat.shader != null && mat.shader.name == "TideTown/Water";
        if (!ok) Fail("shader_failed", "水面着色器不在包里：find=" + (found != null ? found.name : "null") +
                                        " mat=" + (mat != null && mat.shader != null ? mat.shader.name : "null"));
        return "{\"ok\":" + (ok ? "true" : "false") + ",\"find\":\"" + (found != null ? found.name : "null") +
               "\",\"material\":\"" + (mat != null && mat.shader != null ? mat.shader.name : "null") + "\"}";
    }

    void Fail(string s, string why)
    {
        if (status == "running") status = s;
        reason = why;
        Debug.Log("WATERAUDIT FAIL " + s + " " + why);
    }

    void Finish()
    {
        float seconds = Time.realtimeSinceStartup - startedAt;
        string json = "{\"status\":\"" + status + "\",\"reason\":\"" + reason.Replace("\"", "'") +
            "\",\"seconds\":" + seconds.ToString("F1") +
            ",\"shader\":" + shaderJson +
            ",\"levels\":[" + levels + "]" +
            ",\"rise\":" + riseJson + ",\"crate\":" + crateJson + ",\"rescue\":" + rescueJson + ",\"fall\":" + fallJson + "}";
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "water-audit.json"), json, new UTF8Encoding(false));
        Debug.Log("WATERAUDIT " + status + " " + reason);
        Application.Quit(status == "passed" ? 0 : 1);
    }
}
