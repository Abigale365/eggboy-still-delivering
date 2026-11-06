using UnityEngine;

// A simple schematic map based on the playable blockout, with a live player marker.
public class TownMap : MonoBehaviour
{
    EggboyPlayer player;
    EggboyUI ui;
    public bool IsOpen { get; private set; }
    public void Init(EggboyPlayer p, EggboyUI u) { player = p; ui = u; }
    void Update()
    {
        if (player == null || (ui != null && ui.LetterOpen)) return;
        if (Input.GetKeyDown(KeyCode.M) || (IsOpen && Input.GetKeyDown(KeyCode.Escape)))
        {
            IsOpen = !IsOpen;
            player.locked = IsOpen;
            if (IsOpen) { player.vel = Vector3.zero; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
    void OnGUI()
    {
        if (!IsOpen || player == null) return;
        float w = Mathf.Min(760, Screen.width - 30), h = Mathf.Min(490, Screen.height - 30);
        var panel = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
        var old = GUI.skin.font; var font = Resources.Load<Font>("Fonts/NotoSansSC-VF"); if (font != null) GUI.skin.font = font;
        GUI.Box(panel, "潮汐镇地图 · M / Esc 关闭");
        var area = new Rect(panel.x + 36, panel.y + 55, panel.width - 72, panel.height - 115);
        GUI.color = new Color(.33f, .49f, .28f, .8f); GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = Color.white;
        DrawArea(area, -78, -3, 152, 6, new Color(.76f, .64f, .46f));
        for (int i = 0; i < 4; i++) DrawArea(area, -61 + i * 32 - 1.5f, -58, 3, 58, new Color(.76f, .64f, .46f));
        DrawArea(area, -70, -54, 115, 4, new Color(.76f, .64f, .46f));
        DrawArea(area, 41, 16, 6, 20, new Color(.63f, .46f, .3f));
        Mark(area, -75, -2, "山口邮筒"); Mark(area, -38, -9, "邮局"); Mark(area, -10, -9, "面包店"); Mark(area, 18, -14, "学校");
        Mark(area, 30, -50, "住宅区"); Mark(area, 58, 12, "船棚"); Mark(area, 44, 33, "码头 / 灯塔");
        var p = Map(area, player.transform.position.x, player.transform.position.z); GUI.color = Color.red; GUI.DrawTexture(new Rect(p.x-4,p.y-4,8,8), Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(panel.x + 36, panel.yMax - 48, panel.width - 72, 30), "红点：当前位置    山口陆路中断 · 对外联系经码头海路");
        GUI.skin.font = old;
    }
    static Vector2 Map(Rect r, float x, float z) { return new Vector2(r.x + (x + 112f) / 224f * r.width, r.y + (z + 76f) / 114f * r.height); }
    static void DrawArea(Rect r, float x, float z, float w, float h, Color c) { var p = Map(r,x,z); GUI.color = c; GUI.DrawTexture(new Rect(p.x,p.y,w/224f*r.width,h/114f*r.height),Texture2D.whiteTexture); GUI.color = Color.white; }
    static void Mark(Rect r, float x, float z, string label) { var p = Map(r,x,z); GUI.Label(new Rect(p.x,p.y-17,90,24), label); }
}
