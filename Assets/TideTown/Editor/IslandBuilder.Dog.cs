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
    static void BuildDog(RouteFile route, Transform parent, Material fur, Material muzzle, Material collar, Material eye)
    {
        float yaw0 = -Mathf.PI / 2f; // PrototypeGame 在岛上的初始朝向
        var fwd = new Vector3(Mathf.Sin(yaw0), 0f, Mathf.Cos(yaw0));
        var right = new Vector3(Mathf.Cos(yaw0), 0f, -Mathf.Sin(yaw0));
        var at = new Vector3(route.route[0].x, 0f, route.route[0].z) - fwd * 1.2f + right * 0.4f;
        float y = GroundY(at.x, at.z);
        if (y <= 0.2f) throw new Exception($"小黑没落到地面：{at.x:F2},{at.z:F2}");

        var root = new GameObject("XIAOHEI");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(at.x, y, at.z);
        root.transform.rotation = Quaternion.Euler(0f, yaw0 * Mathf.Rad2Deg, 0f);

        var vis = root.AddComponent<DogVisualGreybox>();
        vis.body = Cube(root.transform, "body", new Vector3(0, 0.33f, 0), new Vector3(0.22f, 0.18f, 0.5f), fur).transform;
        var head = Cube(root.transform, "head", new Vector3(0, 0.42f, 0.28f), new Vector3(0.20f, 0.19f, 0.21f), fur);
        vis.head = head.transform;
        Cube(head.transform, "muzzle", new Vector3(0, -0.04f, 0.13f), new Vector3(0.10f, 0.08f, 0.10f), muzzle);
        Cube(head.transform, "nose", new Vector3(0, -0.045f, 0.185f), new Vector3(0.05f, 0.04f, 0.03f), muzzle);
        Cube(head.transform, "eyeL", new Vector3(-0.055f, 0.03f, 0.11f), new Vector3(0.035f, 0.035f, 0.015f), eye);
        Cube(head.transform, "eyeR", new Vector3(0.055f, 0.03f, 0.11f), new Vector3(0.035f, 0.035f, 0.015f), eye);
        var earL = Cube(head.transform, "earL", new Vector3(-0.07f, 0.10f, 0f), new Vector3(0.055f, 0.13f, 0.045f), fur);
        earL.transform.localRotation = Quaternion.Euler(-10f, 0f, 12f);
        var earR = Cube(head.transform, "earR", new Vector3(0.07f, 0.10f, 0f), new Vector3(0.055f, 0.13f, 0.045f), fur);
        earR.transform.localRotation = Quaternion.Euler(-10f, 0f, -12f);
        Cube(root.transform, "collar", new Vector3(0, 0.44f, 0.19f), new Vector3(0.21f, 0.03f, 0.22f), collar);

        vis.legFL = Cyl(root.transform, "legFL", new Vector3(-0.075f, 0.12f, 0.17f), new Vector3(0.07f, 0.12f, 0.07f), fur).transform;
        vis.legFR = Cyl(root.transform, "legFR", new Vector3(0.075f, 0.12f, 0.17f), new Vector3(0.07f, 0.12f, 0.07f), fur).transform;
        vis.legBL = Cyl(root.transform, "legBL", new Vector3(-0.075f, 0.12f, -0.16f), new Vector3(0.07f, 0.12f, 0.07f), fur).transform;
        vis.legBR = Cyl(root.transform, "legBR", new Vector3(0.075f, 0.12f, -0.16f), new Vector3(0.07f, 0.12f, 0.07f), fur).transform;

        var tail = new GameObject("tail");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0, 0.38f, -0.24f);
        vis.tail = tail.transform;
        var tailMesh = Cyl(tail.transform, "tailMesh", new Vector3(0, 0.10f, -0.02f), new Vector3(0.05f, 0.12f, 0.05f), fur);
        tailMesh.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);

        root.AddComponent<DogFollower>();
        var follow = root.GetComponent<DogFollower>();
        var school = SchoolData.Load();
        if (school.peek != null)
            follow.PeekPoint = new Vector3(school.peek.x, school.peek.y, school.peek.z);
        Debug.Log($"DOG x={at.x:F2} y={y:F2} z={at.z:F2} greybox");
    }

}
