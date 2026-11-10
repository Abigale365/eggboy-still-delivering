// 真调一次 Lux3D（只在开发版里有用：直连网关只编进 Development Build）。
//   Build/Dev/TideTown.exe -lux3dlive "提示词"
// 在玩家面前许一个愿：先放保底，等真模型生成、下载、运行时加载、原位替换，拍照，写 lux3d-live.json，退出。
// 每跑一次扣 1 次积分（记进 docs/lux3d-ledger.md，满 30 次网关会拒绝）。
using System.Collections;
using System.IO;
using UnityEngine;

public sealed class LiveLux3DCheck : MonoBehaviour
{
    GameBootstrap boot;
    string prompt = "一只戴着小红围巾的胖海鸥木雕";

    public void Begin(GameBootstrap b)
    {
        boot = b;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-lux3dlive" && !args[i + 1].StartsWith("-")) prompt = args[i + 1];
            if (args[i] == "-lux3dtask") Lux3DService.DebugResumeTaskId = args[i + 1]; // 接着等已有任务，不花积分
        }
        if (boot.ui != null) boot.ui.SetAuditMode(true);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(1.5f);
        var lux = Lux3DService.Ensure();
        lux.SetMode("dev-direct");
        string mode = lux.Mode;
        string status = "running", note = "";
        float seconds = 0f;
        if (mode != "dev-direct") { status = "no_direct_gateway"; note = "不是开发版，或者没找到密钥 / 账本"; }
        else
        {
            var wish = FindObjectOfType<AIAgentCalibrationDirector>();
            var p = boot.player;
            p.transform.position = FindOpen(p.transform.position);
            wish.SpawnLiveWishObjectInWorld(prompt);
            var s = wish.LastWishSpawn;
            yield return Snap("lux3d_live_fallback.png", s.root.transform.position);
            float t0 = Time.realtimeSinceStartup;
            while (s.state == "generating" && Time.realtimeSinceStartup - t0 < 480f) yield return null;
            yield return new WaitForSeconds(1.2f);
            seconds = Time.realtimeSinceStartup - t0;
            status = s.state == "swapped" ? "passed" : "failed";
            note = "state=" + s.state + " gateway=" + s.seconds.ToString("F0") + "s";
            if (s.root != null) yield return Snap("lux3d_live_swapped.png", s.root.transform.position);
        }
        string json = "{\n  \"status\": \"" + status + "\",\n  \"mode\": \"" + mode + "\",\n  \"prompt\": \"" + prompt.Replace("\"", "'") +
            "\",\n  \"seconds\": " + seconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ",\n  \"note\": \"" + note.Replace("\"", "'") + "\"\n}\n";
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "lux3d-live.json"), json, new System.Text.UTF8Encoding(false));
        Debug.Log("LUX3DLIVE " + status + " " + note);
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    Vector3 FindOpen(Vector3 c)
    {
        var w = boot.world;
        for (float r = 0f; r < 6f; r += 0.5f)
            for (int a = 0; a < 12; a++)
            {
                float ang = a * Mathf.PI / 6f;
                var q = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (w.Walkable(q.x, q.z)) { q.y = w.GroundAt(q.x, q.z, c.y + 1f); return q; }
            }
        return c;
    }

    IEnumerator Snap(string file, Vector3 target)
    {
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        var chase = FindObjectOfType<ChaseCamera>();
        if (chase != null) chase.enabled = false;
        Vector3 from = target + new Vector3(1.4f, 1.1f, -1.6f);
        cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target + Vector3.up * 0.25f - from, Vector3.up));
        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
        yield return new WaitForSecondsRealtime(0.6f);
        if (chase != null) chase.enabled = true;
    }
}
