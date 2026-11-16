// 2026-11-16 UI 展示图：-uishowcase 在游戏里按真实流程依次截 5 张（截图时机由游戏自己掐，不靠外部定时）：
// ① 开场封面 ui_title  ② 漫游 HUD（路单卡 + [E] 键帽胶囊都在显示）ui_hud  ③ 双层信封读信 ui_letter
// ④ M 键航海地图册 ui_map_start（0/3）与 ui_map（临时记一枚面包店邮戳，只为看已盖章样式，截完还原）  ⑤ Esc 双页手账 ui_pause
// 输出到 <exe 目录>/shots/，结果写 ui-showcase.json。
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UiShowcase : MonoBehaviour
{
    GameBootstrap boot;
    readonly StringBuilder log = new StringBuilder();
    string dir;

    public void Begin(GameBootstrap b)
    {
        boot = b;
        Application.runInBackground = true;
        dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        var ui = boot.ui;
        var menu = FindObjectOfType<MenuController>();
        if (menu != null) menu.AutoPauseOnLockLoss = false;

        // ① 封面：等场景和封面淡入稳定
        yield return new WaitForSecondsRealtime(6f);
        if (ui.TitleShowing) yield return Shot("ui_title.png");
        else Note("title_not_showing");

        // 点「启程派件」，等序章开场镜头演完
        var start = FindNamed<Button>("StartButton");
        if (start != null) start.onClick.Invoke(); else Note("no_start_button");
        for (float t = 0f; t < 20f && (boot.intro == null || !boot.intro.Done); t += Time.unscaledDeltaTime) yield return null;
        yield return new WaitForSecondsRealtime(1.5f);

        // ② HUD：站到山口邮筒旁，让 [E] 取信胶囊出现，再把路单卡叫出来
        var p = boot.player;
        Vector3 box = MailboxPoint();
        Vector3[] offsets = { new Vector3(0f, 0f, -1.1f), new Vector3(1.1f, 0f, 0f), new Vector3(-1.1f, 0f, 0f), new Vector3(0f, 0f, 1.1f), new Vector3(.7f, 0f, -.7f), Vector3.zero };
        bool prompt = false;
        foreach (var o in offsets)
        {
            var at = box + o;
            p.transform.position = new Vector3(at.x, boot.world.GroundAt(at.x, at.z) + .02f, at.z);
            p.vel = Vector3.zero;
            if (o.sqrMagnitude > .01f) p.yaw = Mathf.Atan2(-o.x, -o.z);
            for (int i = 0; i < 40 && string.IsNullOrEmpty(ui.PromptText); i++) yield return null;
            if (!string.IsNullOrEmpty(ui.PromptText)) { prompt = true; break; }
        }
        if (!prompt) Note("no_prompt");
        ui.RecallGuide();
        yield return new WaitForSecondsRealtime(1.2f);
        Note("hud_prompt=" + ui.PromptText.Replace("\"", "'") + " goal=" + ui.GoalText.Replace("\"", "'"));
        yield return Shot("ui_hud.png");

        // ③ 信：真实取信，等信封展开动画
        if (boot.service.TryInteract("mailbox_03"))
        {
            for (int i = 0; i < 120 && !ui.LetterOpen; i++) yield return null;
            yield return new WaitForSecondsRealtime(1.6f);
            if (ui.LetterOpen) yield return Shot("ui_letter.png"); else Note("letter_not_open");
            ui.CloseLetter();
        }
        else Note("mailbox_failed");
        yield return new WaitForSecondsRealtime(1.2f);

        // ④ 地图册
        var map = FindObjectOfType<TownMap>();
        if (map != null)
        {
            map.Toggle();
            yield return new WaitForSecondsRealtime(.6f);
            if (map.IsOpen) yield return Shot("ui_map_start.png"); else Note("map_not_open");
            map.Close();
            yield return new WaitForSecondsRealtime(.4f);

            var stamps = boot.director.State.stamps;
            bool injected = !stamps.Contains("bakery");
            if (injected) stamps.Add("bakery");
            map.Toggle();
            yield return new WaitForSecondsRealtime(.6f);
            if (map.IsOpen) yield return Shot("ui_map.png");
            map.Close();
            if (injected) stamps.Remove("bakery");
            yield return new WaitForSecondsRealtime(.4f);
        }
        else Note("no_map");

        // ⑤ 暂停手账
        ui.SetPaused(true);
        yield return new WaitForSecondsRealtime(1.2f);
        if (ui.Paused) yield return Shot("ui_pause.png"); else Note("pause_failed");
        ui.SetPaused(false);

        File.WriteAllText(Path.Combine(Application.dataPath, "..", "ui-showcase.json"),
            "{\n  \"status\": \"done\",\n  \"notes\": \"" + log + "\"\n}\n", new UTF8Encoding(false));
        Debug.Log("UISHOWCASE done " + log);
        yield return new WaitForSecondsRealtime(.3f);
        Application.Quit();
    }

    Vector3 MailboxPoint()
    {
        var asset = Resources.Load<TextAsset>("World/route");
        if (asset != null)
        {
            var route = JsonUtility.FromJson<RouteList>(asset.text);
            if (route != null && route.route != null)
                foreach (var r in route.route)
                    if (r.stop && r.id == "03") return new Vector3(r.x, 0f, r.z);
        }
        return new Vector3(-31.04f, 0f, -6.76f);
    }

    [System.Serializable] class Wp { public string id; public bool stop; public float x, z; }
    [System.Serializable] class RouteList { public Wp[] route; }

    static T FindNamed<T>(string name) where T : Component
    {
        foreach (var c in Resources.FindObjectsOfTypeAll<T>())
            if (c != null && c.name == name && c.gameObject.scene.IsValid()) return c;
        return null;
    }

    void Note(string s) { log.Append(log.Length == 0 ? "" : "; ").Append(s); }

    IEnumerator Shot(string file)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        Note(file + " " + tex.width + "x" + tex.height);
        Destroy(tex);
        yield return null;
    }
}
