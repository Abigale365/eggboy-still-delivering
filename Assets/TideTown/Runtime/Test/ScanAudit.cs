// WP-09 自动审计：扫描站默认不可见/不可互动；打开后能显示“暂未开放”。写 scan-audit.json。
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public class ScanAudit : MonoBehaviour
{
    GameBootstrap boot;

    public void Begin(GameBootstrap b) { boot = b; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(1f);
        Time.timeScale = 1f;
        string status = "passed", error = "";

        var station = FindObjectOfType<ScanStation>();
        bool hidden = false, registeredOff = false;
        if (station == null) { status = "no_station"; error = "ScanStation not found"; }
        else
        {
            bool anyOn = false;
            foreach (var r in station.GetComponentsInChildren<Renderer>(true)) if (r.enabled) anyOn = true;
            hidden = !anyOn;
            registeredOff = boot.service.IsRegistered(station);
            if (!hidden) { status = "visible_when_off"; error = "renderer enabled with Features.scan=false"; }
            else if (registeredOff) { status = "registered_when_off"; error = "station registered with Features.scan=false"; }
        }

        bool visibleOn = false, promptOk = false;
        string prompt = "";
        if (status == "passed")
        {
            Features.scan = true;
            station.SetEnabled(true);
            boot.service.Register(station);
            visibleOn = true;
            foreach (var r in station.GetComponentsInChildren<Renderer>(true)) if (!r.enabled) visibleOn = false;
            if (!visibleOn) { status = "hidden_when_on"; error = "renderer disabled with Features.scan=true"; }
        }

        if (status == "passed")
        {
            var p = boot.player.transform.position;
            boot.player.transform.position = new Vector3(station.Position.x + 0.6f, p.y, station.Position.z);
            boot.player.vel = Vector3.zero;
            yield return null;
            if (!boot.service.TryInteractNearest()) { status = "interact_failed"; error = "station not in range"; }
            else
            {
                for (int i = 0; i < 20; i++) { yield return null; }
                prompt = boot.ui.PromptText;
                promptOk = prompt.Contains("暂未开放");
                if (!promptOk) { status = "prompt_wrong"; error = "prompt=" + prompt; }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"status\": \"" + status + "\",");
        sb.AppendLine("  \"hidden_when_off\": " + (hidden ? "true" : "false") + ",");
        sb.AppendLine("  \"unregistered_when_off\": " + (!registeredOff ? "true" : "false") + ",");
        sb.AppendLine("  \"visible_when_on\": " + (visibleOn ? "true" : "false") + ",");
        sb.AppendLine("  \"prompt_unsupported\": " + (promptOk ? "true" : "false") + ",");
        sb.AppendLine("  \"prompt\": \"" + prompt + "\",");
        sb.AppendLine("  \"errors\": \"" + error + "\"");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "scan-audit.json"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log("SCANAUDIT " + status + " hidden=" + hidden + " visibleOn=" + visibleOn + " prompt=" + prompt);
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }
}
