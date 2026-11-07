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
                    Cube(root.transform, "table", new Vector3(0, 0.72f, 0), new Vector3(1.1f, 0.08f, 0.62f), wood);
                    for (int i = 0; i < 4; i++)
                        Cube(root.transform, "leg", new Vector3(i % 2 == 0 ? -0.47f : 0.47f, 0.36f, i < 2 ? -0.24f : 0.24f),
                            new Vector3(0.07f, 0.72f, 0.07f), wood);
                    MountSourceModel(root.transform, "A3", new Vector3(0.2f, 0.77f, 0), 0.18f, false);
                    break;
                case "wood":
                    MountSourceModel(root.transform, "A2", Vector3.zero, 0.52f, false);
                    break;
                case "stove":
                    var oven = MountSourceModel(root.transform, "A1", Vector3.zero, 1.8f, true, 180f);
                    var ovenBounds = ModelBounds(oven);
                    var fx = root.AddComponent<BakeryEffects>();
                    // Measured at the top of the imported chimney mesh.
                    fx.chimneyLocalPosition = new Vector3(-0.406f, 1.8f, 0.123f);
                    fx.fireLocalPosition = new Vector3(0f, 0.57f, -ovenBounds.extents.z + 0.09f);
                    fx.ParticleMaterial = particle;
                    break;
                case "bell":
                    MountSourceModel(root.transform, "A5", new Vector3(0f, 1.22f, 0f), 0.26f, true);
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
                g.Height = it.id == "bell" ? 1.35f : it.id == "flour" ? 0.6f : it.id == "wood" ? 0.25f : 0.3f;
            }
        }
        Debug.Log($"BAKERY items={list.items.Length}");
    }

    // WP-13：学校后门狗碗/狗窝、操场秋千/滑梯/黑板/课桌，全部先灰盒，WP-17 再换真资产
}
