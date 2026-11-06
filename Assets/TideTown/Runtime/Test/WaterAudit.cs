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

        var start = new Vector3(14.2f, 0f, -6.55f);
        start.y = world.GroundAt(start.x, start.z) + 0.01f;
        player.transform.position = start;
        player.vel = Vector3.zero;
        player.grounded = true;
        yield return null;
        yield return null;

        float t0 = Time.realtimeSinceStartup;
        int waterFrames = 0;
        int frames = 0;
        float maxY = start.y;
        while (Time.realtimeSinceStartup - t0 < 12f)
        {
            var p = player.transform.position;
            if (p.x >= 16.5f) break;
            if (p.y < water.Y - 0.05f) waterFrames++;
            maxY = Mathf.Max(maxY, p.y);
            player.Move(Mathf.Min(0.05f, Time.deltaTime), 1f, 0f, false, false);
            frames++;
            yield return null;
        }

        var end = player.transform.position;
        float seconds = Time.realtimeSinceStartup - t0;
        bool ok = end.x >= 16.5f && waterFrames == 0;
        crateJson = "{\"ok\":" + (ok ? "true" : "false") + ",\"from\":[" + start.x.ToString("F2") + "," + start.y.ToString("F2") + "," + start.z.ToString("F2") +
                    "],\"to\":[" + end.x.ToString("F2") + "," + end.y.ToString("F2") + "," + end.z.ToString("F2") +
                    "],\"seconds\":" + seconds.ToString("F2") + ",\"frames\":" + frames +
                    ",\"waterFrames\":" + waterFrames + ",\"maxY\":" + maxY.ToString("F2") +
                    ",\"jumpUsed\":false,\"walkableOnCrates\":" + (world.Walkable(15.4f, -6.55f) ? "true" : "false") +
                    ",\"walkableInWater\":" + (world.Walkable(18.0f, -8.0f) ? "true" : "false") + "}";
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
        // 码头 18,-8 在 L2 被淹（地面 0.9，水位 1.15）
        var from = new Vector3(18.0f, world.GroundAt(18.0f, -8.0f), -8.0f);
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
            ",\"rise\":" + riseJson + ",\"crate\":" + crateJson + ",\"rescue\":" + rescueJson + "}";
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "water-audit.json"), json, new UTF8Encoding(false));
        Debug.Log("WATERAUDIT " + status + " " + reason);
        Application.Quit(status == "passed" ? 0 : 1);
    }
}
