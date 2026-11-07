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
    static void BuildEndingProps(Transform parent)
    {
        var data = EndingData.Load();
        var dark = Mat("EndingDark", new Color32(0x2e, 0x2e, 0x30, 255));
        var paper = Mat("EndingPaper", new Color32(0xf3, 0xe6, 0xc8, 255));
        var windowMat = EmissiveMat("CityWindow", new Color32(0xff, 0xd2, 0x8a, 255));
        var signalMat = EmissiveMat("CitySignal", new Color32(0xff, 0xf2, 0xc8, 255));

        var deskPos = EndingData.Point(data.desk);
        var desk = new GameObject("EndingDesk");
        desk.transform.SetParent(parent, false);
        desk.transform.position = deskPos;
        MountSourceModel(desk.transform, "C8", Vector3.zero, 0.75f, true);
        Cube(desk.transform, "letter", new Vector3(0.08f, 0.755f, 0.02f), new Vector3(0.24f, 0.012f, 0.16f), paper);
        Cube(desk.transform, "seal", new Vector3(0.15f, 0.762f, 0.05f), new Vector3(0.05f, 0.014f, 0.04f), dark);

        var cityPos = EndingData.Point(data.city);
        var cityGo = new GameObject("CityGlow");
        cityGo.transform.SetParent(parent, false);
        cityGo.transform.position = cityPos;
        var city = cityGo.AddComponent<CityGlow>();
        var dots = new List<Renderer>();
        for (int i = 0; i < 24; i++)
        {
            float x = -14f + i * 1.2f;
            float z = -7f + Mathf.Sin(i * 0.9f) * 7f;
            float y = -3.8f + (i % 5) * 1.6f + Mathf.Sin(i * 0.7f) * 0.5f;
            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
            d.name = "city_" + i;
            d.transform.SetParent(cityGo.transform, false);
            d.transform.localPosition = new Vector3(x, y, z);
            d.transform.localScale = Vector3.one * 0.3f;
            d.GetComponent<Renderer>().sharedMaterial = windowMat;
            dots.Add(d.GetComponent<Renderer>());
        }
        city.windows = dots.ToArray();

        var sig = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.DestroyImmediate(sig.GetComponent<Collider>());
        sig.name = "city_signal";
        sig.transform.SetParent(cityGo.transform, false);
        sig.transform.localPosition = new Vector3(4f, 3.2f, 3f);
        sig.transform.localScale = Vector3.one * 0.5f;
        sig.GetComponent<Renderer>().sharedMaterial = signalMat;
        city.signal = sig.transform;

        Debug.Log($"ENDING desk=({deskPos.x:F2},{deskPos.y:F2},{deskPos.z:F2}) city=({cityPos.x:F2},{cityPos.z:F2}) windows={dots.Count}");
    }

    // WP-12：小黑先用灰盒顶着。导入 小黑_绑骨.glb 后，把 DogVisualGreybox 换成 DogVisualGlb 即可，
    // DogFollower 和剧情、审计都只认 DogVisual 接口。
}
