// WP-07 自动剧情审计：走到邮筒、取信、读信、收信，写 story-audit.json。
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
        yield return new WaitForSeconds(1f);
        Time.timeScale = 1f;
        var p = boot.player;

        // 只走 07 → 03 这一段，沿 route.json 的手写路径点
        var asset = Resources.Load<TextAsset>("World/route");
        var route = JsonUtility.FromJson<RouteList>(asset.text).route;
        var way = new List<Wp>();
        foreach (var w in route)
        {
            way.Add(w);
            if (w.stop && w.id == "03") break;
        }

        string status = "passed";
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
        sb.AppendLine("  \"steps\": { \"P1_mailbox\": " + (stepOk ? "true" : "false") + " },");
        sb.AppendLine("  \"save_reload\": " + (saveOk ? "true" : "false") + ",");
        sb.AppendLine("  \"letter_open\": " + (opened ? "true" : "false") + ",");
        sb.AppendLine("  \"move_locked\": " + (lockedMove ? "true" : "false") + ",");
        sb.AppendLine("  \"no_repeat\": " + (noRepeat ? "true" : "false") + ",");
        sb.AppendLine("  \"seconds\": " + secs.ToString("F1") + ",");
        sb.AppendLine("  \"avg_fps\": " + (frames > 0 ? (sumFps / frames).ToString("F1") : "0") + ",");
        sb.AppendLine("  \"errors\": \"" + error + "\"");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "story-audit.json"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log("STORYAUDIT " + status + " letter=" + opened + " save=" + saveOk + " seconds=" + secs.ToString("F1"));
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    void Update()
    {
        if (Time.deltaTime > 0f) { sumFps += 1f / Time.deltaTime; frames++; }
    }
}
