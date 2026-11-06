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
        var stoveMat = Mat("BakeryStove", new Color32(0x4a, 0x44, 0x40, 255));
        var metal = Mat("BakeryMetal", new Color32(0x2c, 0x2c, 0x2c, 255));
        var cream = Mat("BakeryCream", new Color32(0xe8, 0xd6, 0xb4, 255));
        var red = Mat("BakeryRed", new Color32(0x9e, 0x35, 0x2b, 255));
        var particle = ParticleMat();

        foreach (var it in list.items)
        {
            var root = new GameObject("Bakery_" + it.id);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(it.x, it.y, it.z);
            switch (it.id)
            {
                case "stall":
                    Cube(root.transform, "table", new Vector3(0, 0.72f, 0), new Vector3(1.1f, 0.08f, 0.62f), wood);
                    for (int i = 0; i < 4; i++)
                        Cube(root.transform, "leg", new Vector3(i % 2 == 0 ? -0.47f : 0.47f, 0.36f, i < 2 ? -0.24f : 0.24f),
                            new Vector3(0.07f, 0.72f, 0.07f), wood);
                    Cube(root.transform, "matchbox", new Vector3(0.2f, 0.79f, 0), new Vector3(0.14f, 0.05f, 0.10f), red);
                    break;
                case "wood":
                    for (int i = 0; i < 4; i++)
                    {
                        var log = Cyl(root.transform, "log", new Vector3((i % 2) * 0.13f - 0.065f, 0.07f + (i / 2) * 0.12f, 0f),
                            new Vector3(0.12f, 0.25f, 0.12f), wood);
                        log.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    }
                    Cyl(root.transform, "strap", new Vector3(0, 0.17f, 0), new Vector3(0.30f, 0.02f, 0.30f), metal);
                    break;
                case "stove":
                    Cube(root.transform, "body", new Vector3(0, 1.0f, 0), new Vector3(1.2f, 2.0f, 0.9f), stoveMat);
                    Cube(root.transform, "mouth", new Vector3(0, 0.72f, -0.46f), new Vector3(0.55f, 0.45f, 0.04f), metal);
                    Cube(root.transform, "shelf", new Vector3(0, 1.6f, 0.5f), new Vector3(1.3f, 0.08f, 0.2f), metal);
                    var pipe = Cyl(root.transform, "pipe", new Vector3(0.3f, 2.9f, 0), new Vector3(0.18f, 0.9f, 0.18f), metal);
                    pipe.transform.localScale = new Vector3(0.18f, 0.9f, 0.18f);
                    var fx = root.AddComponent<BakeryEffects>();
                    fx.chimneyTopLocalY = 3.7f;
                    fx.ParticleMaterial = particle;
                    break;
                case "bell":
                    Cyl(root.transform, "bell", new Vector3(0, 1.42f, 0), new Vector3(0.14f, 0.05f, 0.14f), metal);
                    Cube(root.transform, "plate", new Vector3(0, 1.42f, -0.03f), new Vector3(0.2f, 0.12f, 0.03f), cream);
                    break;
                case "basket":
                    Cyl(root.transform, "basket", new Vector3(0, 0.14f, 0), new Vector3(0.36f, 0.12f, 0.36f), wood);
                    Cube(root.transform, "bread", new Vector3(0, 0.30f, 0), new Vector3(0.18f, 0.12f, 0.14f), cream);
                    break;
                case "scale":
                    Cube(root.transform, "base", new Vector3(0, 0.05f, 0), new Vector3(0.34f, 0.1f, 0.3f), metal);
                    Cyl(root.transform, "plate", new Vector3(0, 0.16f, 0), new Vector3(0.30f, 0.02f, 0.30f), cream);
                    Cube(root.transform, "dial", new Vector3(0.1f, 0.26f, -0.08f), new Vector3(0.1f, 0.1f, 0.04f), cream);
                    break;
                case "recipe":
                    Cube(root.transform, "book", new Vector3(0, 0.79f, 0), new Vector3(0.2f, 0.04f, 0.15f), cream);
                    break;
                case "flour":
                    Cyl(root.transform, "sack", new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.3f, 0.4f), cream);
                    Cube(root.transform, "knot", new Vector3(0, 0.62f, 0), new Vector3(0.12f, 0.08f, 0.12f), cream);
                    break;
            }

            if (it.id == "bell" || it.id == "basket" || it.id == "scale" || it.id == "flour" || it.id == "wood")
            {
                var g = root.AddComponent<MailboxGlow>();
                g.UseLight = false;
                g.OrbSize = 0.1f;
                g.Height = it.id == "bell" ? 1.35f : it.id == "flour" ? 0.6f : it.id == "wood" ? 0.25f : 0.3f;
            }
        }
        Debug.Log($"BAKERY items={list.items.Length}");
    }

    // WP-13：学校后门狗碗/狗窝、操场秋千/滑梯/黑板/课桌，全部先灰盒，WP-17 再换真资产
}
