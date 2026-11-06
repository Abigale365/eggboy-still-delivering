// WP-07 自动剧情审计，WP-10 起覆盖序章：开场镜头、面屏睁眼、30 秒内取信。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class StoryAudit : MonoBehaviour
{
    [Serializable] class Wp { public string id; public bool stop; public float x, z; }
    [Serializable] class RouteList { public Wp[] route; }

    GameBootstrap boot;
    float sumFps; int frames; string error = "";

    public void Begin(GameBootstrap b) { boot = b; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(0.5f);
        Time.timeScale = 1f;
        var p = boot.player;
        float bootAt = boot.intro != null ? boot.intro.StartTime : Time.realtimeSinceStartup;

        string status = "passed";
        bool introRan = boot.intro != null;
        bool introDone = !introRan;
        bool faceOk = true;
        float introSeconds = 0f;
        string exprStart = p.pose != null ? p.pose.expr.ToString() : "";
        string exprEnd = exprStart;

        // 开场：先等镜头飞下来，再开始走路
        if (introRan)
        {
            yield return new WaitForSeconds(1.5f);
            yield return Snap("story_boot.png");
            while (!boot.intro.Done && Time.realtimeSinceStartup - bootAt < 12f) yield return null;
            introDone = boot.intro.Done;
            introSeconds = Time.realtimeSinceStartup - bootAt;
            exprEnd = p.pose != null ? p.pose.expr.ToString() : "";
            faceOk = exprStart == ExprId.sleepy.ToString() && exprEnd == ExprId.happy.ToString();
            if (!introDone) { status = "intro_timeout"; error = "intro did not finish in 12 s"; }
            else if (!faceOk) { status = "intro_face_wrong"; error = exprStart + " -> " + exprEnd; }
            yield return Snap("story_wake.png");
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        // 只走 07 → 03 这一段，沿 route.json 的手写路径点
        var asset = Resources.Load<TextAsset>("World/route");
        var route = JsonUtility.FromJson<RouteList>(asset.text).route;
        var way = new List<Wp>();
        foreach (var w in route)
        {
            way.Add(w);
            if (w.stop && w.id == "03") break;
        }

        float started = Time.realtimeSinceStartup;
        foreach (var w in way)
        {
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                var pos = p.transform.position;
                float dx = w.x - pos.x, dz = w.z - pos.z;
                if (Mathf.Sqrt(dx * dx + dz * dz) < 0.8f) break;
                if (Time.realtimeSinceStartup - t0 > 30f) { status = "blocked_walk_" + w.id; break; }
                float len = Mathf.Max(0.01f, Mathf.Sqrt(dx * dx + dz * dz));
                p.Move(0.05f, dx / len, dz / len, false, false);
                yield return null;
            }
            if (status != "passed") break;
        }

        bool opened = false;
        float pickupSeconds = 0f;
        if (status == "passed")
        {
            yield return new WaitForSeconds(0.4f);
            if (!boot.service.TryInteractNearest()) { status = "interact_failed"; error = "no interactable in range"; }
            else
            {
                for (int i = 0; i < 90; i++) { if (boot.ui.LetterOpen) { opened = true; break; } yield return null; }
                if (!opened) { status = "letter_not_open"; error = "ui.LetterOpen false"; }
            }
        }

        pickupSeconds = Time.realtimeSinceStartup - bootAt;
        bool within30 = pickupSeconds <= 30f;
        if (status == "passed") yield return Snap("story_pickup.png");
        if (status == "passed" && !within30) { status = "pickup_too_slow"; error = pickupSeconds.ToString("F1") + " s"; }

        bool stepOk = false;
        bool lockedMove = false;
        bool noRepeat = false;
        if (status == "passed")
        {
            stepOk = boot.state.step == "P1_mailbox" && boot.state.flags.Contains("letters_taken");
            var before = p.transform.position;
            p.Move(0.05f, 1f, 0f, false, false);
            lockedMove = (p.transform.position - before).magnitude < 0.01f;
            boot.ui.CloseLetter();
            yield return new WaitForSeconds(0.3f);
            noRepeat = !boot.service.TryInteractNearest();
            if (!stepOk) { status = "state_wrong"; error = "step=" + boot.state.step; }
            else if (p.locked) { status = "still_locked"; error = "player.locked true"; }
            else if (!lockedMove) { status = "move_while_reading"; error = "player moved with letter open"; }
            else if (!noRepeat) { status = "repeat_interact"; error = "mailbox still interactable"; }
        }

        bool saveOk = false;
        if (status == "passed")
        {
            var loaded = new SaveStore().Load();
            saveOk = loaded != null && loaded.step == boot.state.step && loaded.flags.Contains("letters_taken");
            if (!saveOk) { status = "save_failed"; error = "reload mismatch"; }
            // 恢复现场，避免审计把玩家存档带偏
            new SaveStore().Clear();
        }

        float secs = Time.realtimeSinceStartup - started;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"status\": \"" + status + "\",");
        sb.AppendLine("  \"steps\": { \"P0_boot\": " + (introDone ? "true" : "false")
                  + ", \"P1_mailbox\": " + (stepOk ? "true" : "false") + " },");
        sb.AppendLine("  \"intro\": { \"ran\": " + (introRan ? "true" : "false")
                  + ", \"seconds\": " + introSeconds.ToString("F1")
                  + ", \"start_expr\": \"" + exprStart + "\""
                  + ", \"end_expr\": \"" + exprEnd + "\""
                  + ", \"face_ok\": " + (faceOk ? "true" : "false")
                  + ", \"shots\": [\"story_boot.png\", \"story_wake.png\"] },");
        sb.AppendLine("  \"pickup_seconds\": " + pickupSeconds.ToString("F1") + ",");
        sb.AppendLine("  \"pickup_within_30s\": " + (within30 ? "true" : "false") + ",");
        sb.AppendLine("  \"save_reload\": " + (saveOk ? "true" : "false") + ",");
        sb.AppendLine("  \"letter_open\": " + (opened ? "true" : "false") + ",");
        sb.AppendLine("  \"move_locked\": " + (lockedMove ? "true" : "false") + ",");
        sb.AppendLine("  \"no_repeat\": " + (noRepeat ? "true" : "false") + ",");
        sb.AppendLine("  \"seconds\": " + secs.ToString("F1") + ",");
        sb.AppendLine("  \"avg_fps\": " + (frames > 0 ? (sumFps / frames).ToString("F1") : "0") + ",");
        sb.AppendLine("  \"errors\": \"" + error + "\"");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "story-audit.json"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log("STORYAUDIT " + status + " intro=" + introSeconds.ToString("F1") + " pickup=" + pickupSeconds.ToString("F1")
            + " letter=" + opened + " save=" + saveOk + " seconds=" + secs.ToString("F1"));
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    // 全屏截图带 HUD，给评审看目标、教学提示和信纸
    IEnumerator Snap(string file)
    {
        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    void Update()
    {
        if (Time.deltaTime > 0f) { sumFps += 1f / Time.deltaTime; frames++; }
    }
}
