using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    // 要挡人的模型：烤炉、滑梯、秋千、黑板、邮筒、路灯、路牌、长椅、栅栏段。
    // 小件（火柴、球、拖鞋……）不挡；狗窝和课桌也不挡——课桌在邮局门口的步道上（结局用），狗窝在去学校后门的路上；
    // 秋千只是个架子，红球就在秋千底下（剧情要走到跟前捡），腿又往外撇，也不挡
    static readonly string[] CollisionModelIds = { "A1", "C5", "C7", "S1", "S4", "S5", "S6", "S7" };

    static string SourceModelPath(string id)
    {
        var paths = Directory.GetFiles("Assets/TideTown/SourceGLB", id + "_*.glb", SearchOption.AllDirectories);
        if (paths.Length != 1) throw new InvalidOperationException($"Expected one GLB for {id}, found {paths.Length}");
        return paths[0].Replace('\\', '/');
    }

    static Bounds ModelBounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = default;
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (found) bounds.Encapsulate(renderer.bounds);
            else { bounds = renderer.bounds; found = true; }
        }
        if (!found) throw new InvalidOperationException($"No visible model bounds for {model.name}");
        return bounds;
    }

    static GameObject MountSourceModel(Transform parent, string id, Vector3 localBase, float size, bool fitHeight, float yaw = 0f)
    {
        string path = SourceModelPath(id);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("GLB import failed: " + path);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        model.name = id + "_GLB";
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            if (renderer.name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase) ||
                renderer.name.IndexOf("invisible", StringComparison.OrdinalIgnoreCase) >= 0)
                renderer.enabled = false;
        var bounds = ModelBounds(model);
        float extent = fitHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (extent < 0.001f) throw new InvalidOperationException("Empty GLB bounds: " + path);
        model.transform.localScale *= size / extent;
        bounds = ModelBounds(model);
        model.transform.position += parent.TransformPoint(localBase) -
            new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Debug.Log($"GLB_SCENE {parent.name}: {path}");
        // 2026-11-08：这些街道道具和大件剧情道具按模型外形挡人（IslandBuilder.Collision.cs）
        if (Array.IndexOf(CollisionModelIds, id) >= 0)
            AddModelCollision(model, parent.TransformPoint(localBase).y, parent, id, true);
        return model;
    }

    static GameObject BuildMailboxTrail(RouteFile route, Transform parent)
    {
        var root = new GameObject("MailboxTrail");
        root.transform.SetParent(parent, false);
        var gold = EmissiveMat("MailboxTrailGold", new Color32(0xd4, 0x9a, 0x48, 255));
        for (int i = 1; i < route.route.Length; i++)
        {
            var a = route.route[i - 1];
            var b = route.route[i];
            var start = new Vector2(a.x, a.z);
            var end = new Vector2(b.x, b.z);
            int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / 0.72f));
            for (int j = 1; j <= pieces; j++)
            {
                var p = Vector2.Lerp(start, end, (float)j / pieces);
                float y = GroundY(p.x, p.y);
                if (y <= 0.2f) continue;
                var mark = Cube(root.transform, "gold_marker", new Vector3(p.x, y + 0.025f, p.y),
                    new Vector3(0.22f, 0.035f, 0.22f), gold);
                mark.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            if (b.id == "03") break;
        }
        return root;
    }
}
