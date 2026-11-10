// Lux3D 网关：玩法代码只认 ILux3DGateway，真连、转发、假数据三种实现可以互换。
//   mock        开发 / 审计默认：等几秒，返回保底库里的 GLB 字节，不联网、不花积分
//   dev-direct  直连 Aholo（只编进 Editor 和 Development Build；每次下单先记账本，满 30 次拒绝）
//   relay       预留：走自己的转发服务（密钥放在服务端）
//   offline     发布包默认：永远不生成，只用保底
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class Lux3DRequest
{
    public string prompt;       // 玩家原文或字签词
    public string style = "cartoon";
    public int faceCount = 20000;
    public string fallbackId;   // 保底库条目 id（mock 用它决定返回哪个 GLB）
}

public sealed class Lux3DResult
{
    public bool ok;
    public byte[] glb;
    public string error;
    public float seconds;
}

public interface ILux3DGateway
{
    string Name { get; }
    bool CanGenerate { get; }
    // 协程：跑完后 result 一定被填好（成功或失败），不抛异常
    IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled);
}

public sealed class OfflineGateway : ILux3DGateway
{
    public string Name => "offline";
    public bool CanGenerate => false;
    public IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled)
    {
        result.ok = false; result.error = "offline";
        yield break;
    }
}

public sealed class MockGateway : ILux3DGateway
{
    public float delaySeconds = 8f;
    public float failRate = 0f;
    public string Name => "mock";
    public bool CanGenerate => true;

    public IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled)
    {
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < delaySeconds)
        {
            if (cancelled != null && cancelled()) { result.error = "cancelled"; yield break; }
            yield return null;
        }
        result.seconds = Time.realtimeSinceStartup - t0;
        if (UnityEngine.Random.value < failRate) { result.error = "mock failure"; yield break; }
        // Resources/Lux3DMock/<id>.bytes 是真实的 Lux3D GLB，用来走一遍运行时加载
        var ta = Resources.Load<TextAsset>("Lux3DMock/" + (string.IsNullOrEmpty(req.fallbackId) ? "default" : req.fallbackId));
        if (ta == null) ta = Resources.Load<TextAsset>("Lux3DMock/default");
        if (ta == null) { result.error = "mock: no glb bytes"; yield break; }
        result.glb = ta.bytes;
        result.ok = true;
    }
}

// 转发服务（预留）。服务端约定：
//   POST {relayUrl}/lux3d/text-to-3d   {prompt, style, faceCount}  → {"taskId":"..."}
//   GET  {relayUrl}/lux3d/task/{id}                               → {"status":"running|done|failed","glbUrl":"...","error":"..."}
public sealed class RelayGateway : ILux3DGateway
{
    [Serializable] class CreateBody { public string prompt, style; public int faceCount; }
    [Serializable] class CreateResp { public string taskId; }
    [Serializable] class TaskResp { public string status, glbUrl, error; }

    readonly string baseUrl;
    readonly float timeout;
    public RelayGateway(string relayUrl, float timeoutSec) { baseUrl = (relayUrl ?? "").TrimEnd('/'); timeout = timeoutSec; }
    public string Name => "relay";
    public bool CanGenerate => !string.IsNullOrEmpty(baseUrl);

    public IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled)
    {
        float t0 = Time.realtimeSinceStartup;
        string taskId = null;
        var body = JsonUtility.ToJson(new CreateBody { prompt = req.prompt, style = req.style, faceCount = req.faceCount });
        using (var www = new UnityWebRequest(baseUrl + "/lux3d/text-to-3d", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 30;
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success) { result.error = "relay create: " + www.error; yield break; }
            var r = JsonUtility.FromJson<CreateResp>(www.downloadHandler.text);
            taskId = r != null ? r.taskId : null;
        }
        if (string.IsNullOrEmpty(taskId)) { result.error = "relay: no taskId"; yield break; }

        string glbUrl = null;
        while (Time.realtimeSinceStartup - t0 < timeout)
        {
            float wait = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < wait)
            {
                if (cancelled != null && cancelled()) { result.error = "cancelled"; yield break; }
                yield return null;
            }
            using (var www = UnityWebRequest.Get(baseUrl + "/lux3d/task/" + UnityWebRequest.EscapeURL(taskId)))
            {
                www.timeout = 20;
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success) continue;
                var r = JsonUtility.FromJson<TaskResp>(www.downloadHandler.text);
                if (r == null) continue;
                if (r.status == "failed") { result.error = "relay task failed: " + r.error; yield break; }
                if (r.status == "done" && !string.IsNullOrEmpty(r.glbUrl)) { glbUrl = r.glbUrl; break; }
            }
        }
        if (glbUrl == null) { result.error = "timeout"; yield break; }
        using (var www = UnityWebRequest.Get(glbUrl))
        {
            www.timeout = 60;
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success) { result.error = "relay download: " + www.error; yield break; }
            result.glb = www.downloadHandler.data;
        }
        result.seconds = Time.realtimeSinceStartup - t0;
        result.ok = result.glb != null && result.glb.Length > 1024;
        if (!result.ok) result.error = "empty glb";
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
// 直连 Aholo。密钥只从本机 .env 读，绝不写进工程；正式（非 Development）构建里这个类根本不存在。
public sealed class DevDirectGateway : ILux3DGateway
{
    const string Gateway = "https://api.aholo3d.cn";
    public const int Budget = 30;
    readonly string key, ledgerPath;
    readonly float timeout;

    public DevDirectGateway(float timeoutSec)
    {
        timeout = timeoutSec;
        key = Environment.GetEnvironmentVariable("AHOLO_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            string env = Environment.GetEnvironmentVariable("AHOLO_ENV");
            if (string.IsNullOrEmpty(env))
                env = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "say-and-slay", "lux3d-test", ".env");
            if (System.IO.File.Exists(env))
                foreach (var line in System.IO.File.ReadAllLines(env))
                {
                    var t = line.Trim();
                    if (t.StartsWith("AHOLO_API_KEY")) { int i = t.IndexOf('='); if (i > 0) key = t.Substring(i + 1).Trim().Trim('"', '\''); }
                }
        }
        // 账本在工程 docs 下；从 Build/Main 或编辑器都能找到
        string ledger = Environment.GetEnvironmentVariable("LUX3D_LEDGER");
        if (string.IsNullOrEmpty(ledger))
        {
            var dir = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
            {
                string p = System.IO.Path.Combine(dir.FullName, "docs", "lux3d-ledger.md");
                if (System.IO.File.Exists(p)) { ledger = p; break; }
            }
        }
        ledgerPath = ledger;
    }

    public string Name => "dev-direct";
    public bool CanGenerate => !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(ledgerPath);

    int LedgerRows()
    {
        int n = 0;
        foreach (var l in System.IO.File.ReadAllLines(ledgerPath))
        {
            var t = l.TrimStart();
            if (t.Length > 2 && t[0] == '|' && char.IsDigit(t.TrimStart('|', ' ')[0])) n++;
        }
        return n;
    }

    public IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled)
    {
        float t0 = Time.realtimeSinceStartup;
        if (!CanGenerate) { result.error = "no key or ledger"; yield break; }
        int row = LedgerRows() + 1;
        if (row > Budget) { result.error = "Lux3D 积分账本已满 " + Budget + " 次，需要用户同意才能继续"; Debug.LogError(result.error); yield break; }
        string stamp = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm");
        string safePrompt = (req.prompt ?? "").Replace("|", "/").Replace("\n", " ");
        System.IO.File.AppendAllText(ledgerPath, $"| {row} | {stamp} | text-to-3d G1-Turbo | 游戏内实时生成 | {safePrompt} | PENDING-{row} | 已下单 |\n");

        string body = "{\"prompt\":" + Quote(req.prompt) + ",\"style\":" + Quote(req.style) + ",\"version\":\"G1-Turbo\",\"faceCount\":" + req.faceCount
            + ",\"outputFormat\":[\"glb\"],\"enablePbr\":true}";
        string taskId = null;
        using (var www = new UnityWebRequest(Gateway + "/lux3d/v1/generate/text-to-3d/task/create", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", key);
            www.timeout = 30;
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success) { Finish(row, "-", "下单失败 " + www.error); result.error = "create: " + www.error; yield break; }
            taskId = MatchNumber(www.downloadHandler.text, "taskId");
        }
        if (string.IsNullOrEmpty(taskId)) { Finish(row, "-", "下单失败：没有 taskId"); result.error = "no taskId"; yield break; }
        Mark(row, taskId, "生成中");

        string glbUrl = null;
        while (Time.realtimeSinceStartup - t0 < timeout && glbUrl == null)
        {
            float wait = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < wait)
            {
                if (cancelled != null && cancelled()) { result.error = "cancelled"; yield break; }
                yield return null;
            }
            using (var www = UnityWebRequest.Get(Gateway + "/lux3d/v1/generate/task/get?taskid=" + taskId))
            {
                www.SetRequestHeader("Authorization", key);
                www.timeout = 20;
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success) continue;
                string text = www.downloadHandler.text;
                string status = MatchNumber(text, "status");
                if (status == "4") { Replace(taskId, "生成中", "失败"); result.error = "task failed"; yield break; }
                if (status == "3") glbUrl = MatchGlbUrl(text);
            }
        }
        if (glbUrl == null) { Replace(taskId, "生成中", "超时（任务仍在服务器上）"); result.error = "timeout"; yield break; }
        using (var www = UnityWebRequest.Get(glbUrl))
        {
            www.timeout = 90;
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success) { Replace(taskId, "生成中", "下载失败"); result.error = "download: " + www.error; yield break; }
            result.glb = www.downloadHandler.data;
        }
        result.seconds = Time.realtimeSinceStartup - t0;
        result.ok = result.glb != null && result.glb.Length > 1024;
        Replace(taskId, "生成中", result.ok ? $"成功 {result.seconds:F0}s {result.glb.Length / 1e6f:F1}MB" : "空文件");
        if (!result.ok) result.error = "empty glb";
    }

    void Finish(int row, string taskId, string text) => Mark(row, taskId, text);
    void Mark(int row, string taskId, string text)
    {
        var all = System.IO.File.ReadAllText(ledgerPath).Replace($"| PENDING-{row} | 已下单 |", $"| {taskId} | {text} |");
        System.IO.File.WriteAllText(ledgerPath, all);
    }
    void Replace(string taskId, string from, string to)
    {
        var all = System.IO.File.ReadAllText(ledgerPath).Replace($"| {taskId} | {from} |", $"| {taskId} | {to} |");
        System.IO.File.WriteAllText(ledgerPath, all);
    }

    static string Quote(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ") + "\"";
    static string MatchNumber(string json, string field)
    {
        var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + field + "\"\\s*:\\s*\"?(\\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }
    static string MatchGlbUrl(string json)
    {
        var m = System.Text.RegularExpressions.Regex.Match(json, "\"(https?:[^\"]+?\\.glb(\\?[^\"]*)?)\"");
        return m.Success ? m.Groups[1].Value.Replace("\\/", "/").Replace("\\u0026", "&") : null;
    }
}
#endif
