// WP-22/P1：F3 碰撞盒线框。红色=挡人矩形，绿色=可站顶面，蓝色=水坑。
using System.Collections.Generic;
using UnityEngine;

public sealed class CollisionDebugView : MonoBehaviour
{
    public bool Visible;
    IslandWorld world;
    Mesh lineMesh;
    Material lineMat;
    bool dirty = true;

    void Awake()
    {
        world = FindObjectOfType<IslandWorld>();
        // 截图审计用：-collisiondebug 开局就显示线框
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "-collisiondebug") { Visible = true; dirty = true; }
    }

    void Update()
    {
        // 只有调试模式（-debugui、开发版、编辑器）才响应 F3，发布包里玩家按了没反应
        if (Input.GetKeyDown(KeyCode.F3) && (Features.debugUi || Debug.isDebugBuild))
        { Visible = !Visible; dirty = true; Debug.Log("COLLISIONDEBUG " + Visible); }
        if (!Visible || world == null) return;
        if (dirty) { Build(); dirty = false; }
        if (lineMesh != null && lineMat != null)
            Graphics.DrawMesh(lineMesh, Matrix4x4.identity, lineMat, 0);
    }

    void Build()
    {
        if (lineMat == null)
        {
            var sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return;
            lineMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMat.SetInt("_ZWrite", 0);
        }
        var verts = new List<Vector3>();
        var cols = new List<Color32>();
        foreach (var c in world.Colliders)
        {
            float hw = c.hw ?? c.r ?? 0.5f;
            float hd = c.hd ?? c.r ?? 0.5f;
            float gy = world.GroundAt(c.x, c.z);
            if (gy <= 0.2f) gy = 0.15f;
            float top = c.top.HasValue ? c.top.Value : gy + 5f;
            var col = c.water ? new Color32(80, 150, 255, 220)
                : c.top.HasValue ? new Color32(80, 230, 130, 220)
                : new Color32(255, 70, 50, 220);
            AddBox(verts, cols, c.x, c.z, hw, hd, gy, top, col);
        }
        if (lineMesh == null) lineMesh = new Mesh { name = "collision_debug" };
        lineMesh.Clear();
        lineMesh.SetVertices(verts);
        lineMesh.SetColors(cols);
        lineMesh.SetIndices(EnumerableRange(verts.Count), MeshTopology.Lines, 0, true);
        lineMesh.RecalculateBounds();
    }

    static List<int> EnumerableRange(int n)
    {
        var list = new List<int>(n);
        for (int i = 0; i < n; i++) list.Add(i);
        return list;
    }

    static void AddBox(List<Vector3> v, List<Color32> cols, float x, float z, float hw, float hd, float y0, float y1, Color32 col)
    {
        // 底框、顶框、四条竖线
        AddRect(v, cols, x, z, hw, hd, y0, col);
        if (y1 > y0 + 0.01f)
        {
            AddRect(v, cols, x, z, hw, hd, y1, col);
            Line(v, cols, new Vector3(x - hw, y0, z - hd), new Vector3(x - hw, y1, z - hd), col);
            Line(v, cols, new Vector3(x + hw, y0, z - hd), new Vector3(x + hw, y1, z - hd), col);
            Line(v, cols, new Vector3(x - hw, y0, z + hd), new Vector3(x - hw, y1, z + hd), col);
            Line(v, cols, new Vector3(x + hw, y0, z + hd), new Vector3(x + hw, y1, z + hd), col);
        }
    }

    static void AddRect(List<Vector3> v, List<Color32> cols, float x, float z, float hw, float hd, float y, Color32 col)
    {
        Line(v, cols, new Vector3(x - hw, y, z - hd), new Vector3(x + hw, y, z - hd), col);
        Line(v, cols, new Vector3(x + hw, y, z - hd), new Vector3(x + hw, y, z + hd), col);
        Line(v, cols, new Vector3(x + hw, y, z + hd), new Vector3(x - hw, y, z + hd), col);
        Line(v, cols, new Vector3(x - hw, y, z + hd), new Vector3(x - hw, y, z - hd), col);
    }

    static void Line(List<Vector3> v, List<Color32> cols, Vector3 a, Vector3 b, Color32 col)
    {
        v.Add(a); v.Add(b); cols.Add(col); cols.Add(col);
    }
}
