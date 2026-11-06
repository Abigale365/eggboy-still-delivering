// WP-05 生成岛：读 Data/Resources/World/world_layout.json，在 TideTown_Main.unity 里生成纯色白盒。
// 场景结构：Generated/（每次重建清空）+ Manual/（绝不碰）。
// 菜单：潮汐镇/场景/生成主场景（WP-05）；命令行：-executeMethod IslandBuilder.BuildMain
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class IslandBuilder
{
    static void BuildSchoolProps(Transform parent)
    {
        var wood = Mat("SchoolWood", new Color32(0x8a, 0x62, 0x3e, 255));
        var dark = Mat("SchoolDark", new Color32(0x3f, 0x44, 0x44, 255));
        var cream = Mat("SchoolCream", new Color32(0xe8, 0xd6, 0xb4, 255));
        var red = Mat("SchoolBallRed", new Color32(0xc2, 0x3a, 0x32, 255));
        var paw = Mat("SchoolPaw", new Color32(0x3a, 0x33, 0x2d, 255));

        var list = SchoolData.Load();
        Vector3 bowlPos = Vector3.zero;
        SchoolBall ballComp = null;

        foreach (var it in list.items)
        {
            var root = new GameObject("S_" + it.id);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(it.x, it.y, it.z);

            switch (it.id)
            {
                case "bowl":
                    bowlPos = root.transform.position;
                    Cyl(root.transform, "bowl", new Vector3(0, 0.08f, 0), new Vector3(0.34f, 0.08f, 0.34f), cream);
                    Cyl(root.transform, "inner", new Vector3(0, 0.14f, 0), new Vector3(0.25f, 0.02f, 0.25f), dark);
                    break;
                case "ball":
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
                    ball.name = "ball_mesh";
                    ball.transform.SetParent(root.transform, false);
                    ball.transform.localPosition = new Vector3(0, 0.07f, 0);
                    ball.transform.localScale = Vector3.one * 0.14f;
                    ball.GetComponent<Renderer>().sharedMaterial = red;
                    ballComp = ball.AddComponent<SchoolBall>();
                    break;
                case "slipper":
                    root.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
                    Cube(root.transform, "sole", new Vector3(0, 0.03f, 0), new Vector3(0.30f, 0.05f, 0.12f), wood);
                    Cube(root.transform, "strap", new Vector3(0, 0.065f, 0.02f), new Vector3(0.13f, 0.04f, 0.13f), cream);
                    break;
                case "doghouse":
                    Cube(root.transform, "box", new Vector3(0, 0.31f, 0), new Vector3(0.75f, 0.62f, 0.6f), wood);
                    Cube(root.transform, "door", new Vector3(0, 0.26f, 0.305f), new Vector3(0.34f, 0.40f, 0.03f), dark);
                    break;
                case "swing":
                    foreach (var x in new[] { -0.6f, 0.6f })
                        foreach (var z in new[] { -0.35f, 0.35f })
                            Cyl(root.transform, "post", new Vector3(x, 0.9f, z), new Vector3(0.05f, 0.9f, 0.05f), dark);
                    Cube(root.transform, "bar", new Vector3(0, 1.82f, 0), new Vector3(1.35f, 0.07f, 0.08f), dark);
                    Cube(root.transform, "seat", new Vector3(0, 0.55f, 0), new Vector3(0.5f, 0.05f, 0.22f), wood);
                    foreach (var x in new[] { -0.22f, 0.22f })
                        Cyl(root.transform, "rope", new Vector3(x, 1.2f, 0), new Vector3(0.02f, 0.62f, 0.02f), cream);
                    break;
                case "slide":
                    var ramp = Cube(root.transform, "ramp", new Vector3(0, 0.75f, 0.1f), new Vector3(0.7f, 0.08f, 1.5f), wood);
                    ramp.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                    foreach (var x in new[] { -0.34f, 0.34f })
                        Cyl(root.transform, "post", new Vector3(x, 0.7f, -0.7f), new Vector3(0.05f, 0.7f, 0.05f), dark);
                    Cube(root.transform, "platform", new Vector3(0, 1.36f, -0.7f), new Vector3(0.7f, 0.08f, 0.5f), wood);
                    break;
                case "blackboard":
                    Cube(root.transform, "board", new Vector3(0, 1.0f, 0), new Vector3(1.2f, 0.8f, 0.06f), dark);
                    foreach (var x in new[] { -0.5f, 0.5f })
                        Cyl(root.transform, "leg", new Vector3(x, 0.45f, 0), new Vector3(0.04f, 0.45f, 0.04f), wood);
                    break;
                case "desk":
                    Cube(root.transform, "top", new Vector3(0, 0.72f, 0), new Vector3(0.7f, 0.06f, 0.45f), wood);
                    foreach (var x in new[] { -0.3f, 0.3f })
                        foreach (var z in new[] { -0.18f, 0.18f })
                            Cube(root.transform, "leg", new Vector3(x, 0.35f, z), new Vector3(0.05f, 0.7f, 0.05f), dark);
                    break;
            }
        }

        if (ballComp != null) ballComp.BowlPoint = bowlPos + Vector3.up * 0.02f;

        // 爪印：学校后门 → 拖鞋 → 绕到操场
        var path = new[]
        {
            new Vector2(13.50f, 3.94f), new Vector2(13.85f, 3.70f), new Vector2(14.20f, 3.42f),
            new Vector2(14.55f, 3.18f), new Vector2(14.85f, 3.02f), new Vector2(15.05f, 2.70f),
            new Vector2(15.15f, 2.10f), new Vector2(15.18f, 1.40f), new Vector2(15.15f, 0.70f),
            new Vector2(15.05f, 0.00f), new Vector2(14.85f, -0.90f), new Vector2(14.55f, -1.80f),
            new Vector2(14.15f, -2.70f), new Vector2(13.60f, -3.60f), new Vector2(12.90f, -4.30f),
            new Vector2(12.00f, -4.80f), new Vector2(11.05f, -4.95f), new Vector2(10.30f, -4.90f),
        };
        int paws = BuildPawPrints(parent, path, paw);
        Debug.Log($"SCHOOL items={list.items.Length} paws={paws}");
    }

    static int BuildPawPrints(Transform parent, Vector2[] path, Material mat)
    {
        const float step = 0.45f;
        int n = 0;
        bool left = false;
        for (int s = 0; s < path.Length - 1; s++)
        {
            var a = path[s]; var b = path[s + 1];
            var d = b - a; float len = d.magnitude;
            if (len < 0.01f) continue;
            d /= len;
            var perp = new Vector2(-d.y, d.x);
            for (float t = 0; t < len; t += step)
            {
                var p = a + d * t + perp * (left ? 0.06f : -0.06f);
                float y = GroundY(p.x, p.y);
                if (y <= 0.2f) continue;
                var root = new GameObject("paw" + n++);
                root.transform.SetParent(parent, false);
                root.transform.position = new Vector3(p.x, y + 0.012f, p.y);
                root.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0f);
                Cube(root.transform, "pad", new Vector3(0, 0, 0), new Vector3(0.075f, 0.008f, 0.10f), mat);
                foreach (var toe in new[] { new Vector2(-0.04f, 0.075f), new Vector2(-0.014f, 0.095f), new Vector2(0.014f, 0.095f), new Vector2(0.04f, 0.075f) })
                    Cube(root.transform, "toe", new Vector3(toe.x, 0, toe.y), new Vector3(0.028f, 0.008f, 0.035f), mat);
                left = !left;
            }
        }
        return n;
    }

    // ---------- WP-14 灯塔 ----------
    // 塔身沿用 WP-05 灰盒，这里补 M5 要用的塔门、塔顶平台、灯头和远景手电光点；B4/B9 真资产留到 WP-17。
}
