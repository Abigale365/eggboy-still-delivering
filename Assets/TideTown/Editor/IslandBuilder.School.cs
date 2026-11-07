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
                    MountSourceModel(root.transform, "C1", Vector3.zero, 0.38f, false);
                    break;
                case "ball":
                    var ball = new GameObject("ball_mesh");
                    ball.transform.SetParent(root.transform, false);
                    ball.transform.localPosition = Vector3.up * 0.07f;
                    MountSourceModel(ball.transform, "C2", Vector3.down * 0.07f, 0.14f, false);
                    ballComp = ball.AddComponent<SchoolBall>();
                    break;
                case "slipper":
                    MountSourceModel(root.transform, "C3", Vector3.zero, 0.30f, false, 25f);
                    break;
                case "doghouse":
                    MountSourceModel(root.transform, "C4", Vector3.zero, 0.62f, true, 180f);
                    break;
                case "swing":
                    MountSourceModel(root.transform, "C6", Vector3.zero, 1.85f, true);
                    break;
                case "slide":
                    MountSourceModel(root.transform, "C5", Vector3.zero, 1.45f, true);
                    break;
                case "blackboard":
                    MountSourceModel(root.transform, "C7", Vector3.zero, 1.4f, true, 180f);
                    break;
                case "desk":
                    MountSourceModel(root.transform, "C8", Vector3.zero, 0.75f, true);
                    break;
            }
        }

        if (ballComp != null) ballComp.BowlPoint = bowlPos + Vector3.up * 0.11f;

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
