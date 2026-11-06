// WP-16：地图。背景用布局预览图，玩家位置按预览相机投影，和目标、邮戳一起显示。
// M 打开 / M 或 Esc 关闭；读信或过场时不会打开。读 world_layout.json，不再手写形状。
using System;
using UnityEngine;

public class TownMap : MonoBehaviour
{
    [Serializable] class MapCam { public string name; public float[] pos, euler; public float fov; public int width, height; }
    [Serializable] class MapCameraList { public MapCam[] cameras; }

    EggboyPlayer player;
    EggboyUI ui;
    StoryDirector director;
    ControlGate gate;
    Texture2D preview;
    object mapLock;

    bool hasCam;
    Vector3 camPos, camFwd, camUp;
    float focal;
    int imgW = 1774, imgH = 887;

    public bool IsOpen { get; private set; }
    public float ClosedAt { get; private set; }

    public void Init(EggboyPlayer p, EggboyUI u, StoryDirector d, ControlGate g)
    {
        player = p;
        ui = u;
        director = d;
        gate = g;
        preview = Resources.Load<Texture2D>("UI/town_preview");
        if (preview != null) { imgW = preview.width; imgH = preview.height; }
        LoadCamera();
    }

    void LoadCamera()
    {
        var asset = Resources.Load<TextAsset>("World/world_layout");
        if (asset == null) return;
        var list = JsonUtility.FromJson<MapCameraList>(asset.text);
        if (list == null || list.cameras == null) return;
        foreach (var c in list.cameras)
        {
            if (c.name != "preview" || c.pos == null || c.euler == null || c.pos.Length < 3 || c.euler.Length < 3) continue;
            camPos = new Vector3(c.pos[0], c.pos[1], c.pos[2]);
            float pitch = c.euler[0] * Mathf.Deg2Rad;
            camFwd = new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));
            camUp = new Vector3(0f, Mathf.Cos(pitch), Mathf.Sin(pitch));
            focal = (c.height * 0.5f) / Mathf.Tan(c.fov * Mathf.Deg2Rad * 0.5f);
            if (c.width > 0) imgW = c.width;
            if (c.height > 0) imgH = c.height;
            hasCam = true;
            break;
        }
    }

    // 旧 Prototype 场景没有剧情系统，也给它一个能打开的地图
    public void Init(EggboyPlayer p, EggboyUI u) { Init(p, u, null, null); }

    public bool CanOpen()
    {
        if (player == null || ui == null) return false;
        if (ui.LetterOpen) return false;
        if (director != null && (director.Cutscene || director.Ended || director.Climbing)) return false;
        return true;
    }

    public void Toggle()
    {
        if (IsOpen) { Close(); return; }
        if (!CanOpen()) return;
        if (gate != null && gate.Locked) return;
        if (gate != null)
        {
            mapLock = gate.Acquire("map");
            if (mapLock == null) return;
        }
        IsOpen = true;
        player.locked = true;
        player.vel = Vector3.zero;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        ClosedAt = Time.unscaledTime;
        if (gate != null && mapLock != null) gate.Release(mapLock);
        mapLock = null;
        if (player != null) player.locked = false;
    }

    void Update()
    {
        if (player == null || ui == null) return;
        if (ui.LetterOpen)
        {
            if (IsOpen) Close();
            return;
        }
        if (Input.GetKeyDown(KeyCode.M)) Toggle();
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void OnDisable() { Close(); }

    public Vector2 ProjectUV(Vector3 p) { return Project(p); }

    Vector2 Project(Vector3 p)
    {
        var q = p - camPos;
        float dz = Vector3.Dot(q, camFwd);
        float dy = Vector3.Dot(q, camUp);
        if (Mathf.Abs(dz) < 0.01f) dz = 0.01f;
        return new Vector2(imgW * 0.5f + focal * q.x / dz, imgH * 0.5f - focal * dy / dz);
    }

    void OnGUI()
    {
        if (!IsOpen || player == null) return;
        var old = GUI.skin.font;
        var font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
        if (font != null) GUI.skin.font = font;

        float w = Mathf.Min(Screen.width * 0.92f, 1180f);
        float h = Mathf.Min(Screen.height * 0.92f, 700f);
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.92f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(panel.x + 18, panel.y + 10, panel.width - 36, 28),
            "潮汐镇地图 · M / Esc 关闭    邮戳 " + Stamps() + "/3", Title(font));

        // 背景预览图保持 1774:887
        float pad = 16f;
        var area = new Rect(panel.x + pad, panel.y + 44, panel.width - pad * 2, panel.height - 44 - pad);
        float aspect = (float)imgW / imgH;
        var img = new Rect(area.x, area.y, area.width, area.width / aspect);
        if (img.height > area.height) { img.height = area.height; img.width = area.height * aspect; }
        img.x = area.x + (area.width - img.width) * 0.5f;
        img.y = area.y + (area.height - img.height) * 0.5f;
        if (preview != null) GUI.DrawTexture(img, preview, ScaleMode.ScaleToFit);

        // 玩家位置：和生成岛时同一台预览相机
        if (hasCam && preview != null)
        {
            var uv = Project(player.transform.position);
            var p = new Vector2(img.x + uv.x / imgW * img.width, img.y + uv.y / imgH * img.height);
            GUI.color = new Color(1f, 0.18f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(p.x - 7f, p.y - 7f, 14f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(p.x - 7f, p.y + 5f, 14f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(p.x - 7f, p.y - 7f, 2f, 14f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(p.x + 5f, p.y - 7f, 2f, 14f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(p.x + 10f, p.y - 12f, 140f, 24f), "你在这里", Label(font));
        }

        GUI.Label(new Rect(panel.x + 18, panel.yMax - 34, panel.width - 36, 26),
            GoalText(), Label(font));
        GUI.skin.font = old;
    }

    string Stamps()
    {
        var d = FindObjectOfType<StoryDirector>();
        return d != null && d.State != null ? d.State.stamps.Count.ToString() : "0";
    }

    string GoalText()
    {
        if (ui == null) return "";
        var t = ui.GoalText;
        return string.IsNullOrEmpty(t) ? "自由逛逛 · 靠近发光的东西按 E" : t;
    }

    GUIStyle Title(Font font)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = 20;
        s.fontStyle = FontStyle.Bold;
        s.normal.textColor = new Color32(0xfb, 0xef, 0xd9, 255);
        if (font != null) s.font = font;
        return s;
    }

    GUIStyle Label(Font font)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = 15;
        s.normal.textColor = new Color32(0xe8, 0xd6, 0xb4, 255);
        if (font != null) s.font = font;
        return s;
    }
}
