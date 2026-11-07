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
    static void BuildDog(RouteFile route, Transform parent)
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

        var model = MountSourceModel(root.transform, "R1", Vector3.zero, 0.48f, true);
        var vis = root.AddComponent<DogVisualGlb>();
        vis.animationPlayer = model.GetComponentInChildren<Animation>();
        if (vis.animationPlayer == null || vis.animationPlayer.GetClip("walk") == null ||
            vis.animationPlayer.GetClip("trot") == null || vis.animationPlayer.GetClip("idle") == null)
            throw new InvalidOperationException("小黑 GLB 缺少 Unity 的 idle/walk/trot 动画。");

        root.AddComponent<DogFollower>();
        var follow = root.GetComponent<DogFollower>();
        var school = SchoolData.Load();
        if (school.peek != null)
            follow.PeekPoint = new Vector3(school.peek.x, school.peek.y, school.peek.z);
        Debug.Log($"DOG x={at.x:F2} y={y:F2} z={at.z:F2} GLB v21");
    }

}
