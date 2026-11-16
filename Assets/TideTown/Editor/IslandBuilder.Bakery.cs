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
    static void BuildBakeryProps(Transform parent)
    {
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TideTown/Data/Resources/Story/bakery.json");
        if (ta == null) { Debug.LogWarning("BAKERY no bakery.json"); return; }
        var list = JsonUtility.FromJson<BakeryList>(ta.text);
        if (list == null || list.items == null) return;

        var wood = Mat("BakeryWood", new Color32(0x8a, 0x62, 0x3e, 255));
        var particle = ParticleMat();

        foreach (var it in list.items)
        {
            var root = new GameObject("Bakery_" + it.id);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(it.x, it.y, it.z);
            switch (it.id)
            {
                case "stall":
                    // 2026-11-16：贴着面包店橱窗下摆的小摊：三块木板台面 + 米白桌布（珊瑚色边）+ 四条腿 + 后挡板
                    for (int i = 0; i < 3; i++)
                        Cube(root.transform, "table_plank", new Vector3(0, 0.72f, -0.205f + i * 0.205f), new Vector3(1.1f, 0.06f, 0.195f), wood);
                    Cube(root.transform, "cloth", new Vector3(0, 0.755f, 0.02f), new Vector3(0.84f, 0.012f, 0.50f), Mat("StallCloth", new Color32(0xf3, 0xe6, 0xcc, 255)));
                    Cube(root.transform, "cloth_edge", new Vector3(0, 0.70f, -0.255f), new Vector3(0.84f, 0.10f, 0.012f), Mat("StallClothEdge", new Color32(0xe0, 0x7a, 0x5c, 255)));
                    for (int i = 0; i < 4; i++)
                        Cube(root.transform, "leg", new Vector3(i % 2 == 0 ? -0.47f : 0.47f, 0.36f, i < 2 ? -0.24f : 0.24f),
                            new Vector3(0.07f, 0.72f, 0.07f), wood);
                    Cube(root.transform, "backboard", new Vector3(0, 0.86f, 0.285f), new Vector3(1.1f, 0.22f, 0.04f), wood);
                    MountSourceModel(root.transform, "A3", new Vector3(0.2f, 0.762f, 0), 0.18f, false);
                    break;
                case "wood":
                    MountSourceModel(root.transform, "A2", Vector3.zero, 0.52f, false);
                    break;
                case "stove":
                    var oven = MountSourceModel(root.transform, "A1", Vector3.zero, 1.8f, true, 180f);
                    var fx = root.AddComponent<BakeryEffects>();
                    MeasureOven(root.transform, oven, fx);
                    fx.ParticleMaterial = particle;
                    break;
                case "bell":
                    // 2026-11-16：挂在面包店正门拱券右边的墙上（门把手那一侧），离地 0.90–1.16 m（EGGBOY 伸手够得着），
                    // 一根铸铁短挂臂伸出墙面 0.19 m，铃顶吊在挂臂下面（原来离地 1.22 m 悬在橱窗前的街上）。
                    // 铃口离地 0.72 m：阿萤的身高、也是 EGGBOY 伸手碰得到的高度
                    MountSourceModel(root.transform, "A5", new Vector3(0f, 0.72f, 0f), 0.26f, true);
                    var iron = Mat("BellBracketIron", new Color32(0x3a, 0x33, 0x2e, 255));
                    Cube(root.transform, "bracket_arm", new Vector3(0f, 1.005f, 0.095f), new Vector3(0.025f, 0.025f, 0.21f), iron);
                    Cube(root.transform, "bracket_plate", new Vector3(0f, 1.005f, 0.195f), new Vector3(0.07f, 0.13f, 0.014f), iron);
                    Cube(root.transform, "bracket_hook", new Vector3(0f, 0.99f, 0f), new Vector3(0.012f, 0.035f, 0.012f), iron);
                    root.AddComponent<BellSwing>();
                    break;
                case "basket":
                    MountSourceModel(root.transform, "A4", Vector3.zero, 0.46f, false);
                    break;
                case "scale":
                    MountSourceModel(root.transform, "A6", Vector3.zero, 0.34f, true);
                    break;
                case "recipe":
                    MountSourceModel(root.transform, "A7", new Vector3(-0.24f, 0.77f, 0f), 0.26f, false);
                    break;
                case "flour":
                    MountSourceModel(root.transform, "A8", Vector3.zero, 0.58f, true);
                    break;
            }

            if (it.id == "bell" || it.id == "basket" || it.id == "scale" || it.id == "flour" || it.id == "wood")
            {
                var g = root.AddComponent<MailboxGlow>();
                g.UseLight = false;
                g.OrbSize = 0.1f;
                g.Height = it.id == "bell" ? 1.12f : it.id == "flour" ? 0.6f : it.id == "wood" ? 0.25f : 0.3f;
            }
        }
        Debug.Log($"BAKERY items={list.items.Length}");
    }

    // 2026-11-16：量 A1 烤炉网格，找炉门开口（从炉前往里平射，射线能穿进去的那一片就是开口）、炉膛底板、烟囱顶。
    // 原来火在炉门外 5 cm 的半空中（fireLocalPosition 在前表面往里 9 cm，FireMouth 又往外退 14 cm）。
    static void MeasureOven(Transform stove, GameObject oven, BakeryEffects fx)
    {
        var temp = new List<GameObject>();
        foreach (var mf in oven.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var go = new GameObject("oven_probe"); go.layer = 31;
            go.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation); go.transform.localScale = mf.transform.lossyScale;
            go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; temp.Add(go);
        }
        Physics.SyncTransforms();
        var lb = new Bounds(stove.InverseTransformPoint(ModelBounds(oven).center), Vector3.zero);
        var wb = ModelBounds(oven);
        foreach (var c in new[] { wb.min, wb.max }) lb.Encapsulate(stove.InverseTransformPoint(c));
        float FirstZ(float x, float y)
        {
            var o = stove.TransformPoint(new Vector3(x, y, lb.min.z - 1f));
            return Physics.Raycast(o, stove.forward, out var h, lb.size.z + 2f, 1 << 31) ? stove.InverseTransformPoint(h.point).z : float.PositiveInfinity;
        }
        // 炉身前表面：两侧肩部的第一个面
        float front = Mathf.Min(FirstZ(lb.min.x * 0.55f, lb.size.y * 0.3f), FirstZ(lb.max.x * 0.55f, lb.size.y * 0.3f));
        float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue; int n = 0;
        for (float y = lb.size.y * 0.25f; y <= lb.size.y * 0.75f; y += 0.02f)
            for (float x = lb.min.x * 0.6f; x <= lb.max.x * 0.6f; x += 0.02f)
                // 射线一直穿到炉身后壁（离后表面 0.3 m 以内）才算开口；穹顶前面往后收，打到的面也比前表面靠后，不能只比前表面
                if (FirstZ(x, y) > lb.max.z - 0.3f) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); n++; }
        if (n < 6) { x0 = -0.15f; x1 = 0.15f; y0 = lb.size.y * 0.33f; y1 = lb.size.y * 0.5f; Debug.LogWarning("OVEN mouth not found, fallback"); }
        float cx = (x0 + x1) * 0.5f, depthIn = 0.14f;
        // 开口那一面：开口四周一圈里最靠前的面
        float mouthZ = Mathf.Min(FirstZ(cx, y1 + 0.04f), FirstZ(x0 - 0.04f, (y0 + y1) * 0.5f), FirstZ(x1 + 0.04f, (y0 + y1) * 0.5f));
        if (float.IsInfinity(mouthZ)) mouthZ = front;
        fx.mouthLocal = new Vector3(cx, (y0 + y1) * 0.5f, mouthZ);
        fx.mouthSize = new Vector2(x1 - x0 + 0.02f, y1 - y0 + 0.02f);
        fx.chamberFloorLocal = new Vector3(cx, y0, front + depthIn);
        fx.fireLocalPosition = new Vector3(cx, y0 + Mathf.Min(0.07f, (y1 - y0) * 0.25f), front + depthIn);
        // 烟囱顶：模型最高 4 cm 里的顶点取中心
        Vector3 top = Vector3.zero; int tn = 0;
        foreach (var mf in oven.GetComponentsInChildren<MeshFilter>())
            foreach (var v in mf.sharedMesh.vertices)
            {
                var l = stove.InverseTransformPoint(mf.transform.TransformPoint(v));
                if (l.y > lb.max.y - 0.04f) { top += l; tn++; }
            }
        if (tn > 0) fx.chimneyLocalPosition = top / tn;
        foreach (var g in temp) UnityEngine.Object.DestroyImmediate(g);
        Debug.Log($"OVEN front={front:F3} mouth=({cx:F2},{(y0 + y1) * 0.5f:F2},{mouthZ:F3}) size=({x1 - x0:F2}x{y1 - y0:F2}) cells={n} fire={fx.fireLocalPosition} chimney={fx.chimneyLocalPosition}");
    }

    // WP-13：学校后门狗碗/狗窝、操场秋千/滑梯/黑板/课桌，全部先灰盒，WP-17 再换真资产
}
