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
    static void BuildLighthouseProps(Transform parent)
    {
        if (lighthouseSize.y <= 0f) throw new Exception("没有拿到灯塔灰盒尺寸");
        var data = LighthouseData.Load();
        Vector3 basePos = new Vector3(lighthouseCenter.x, GroundY(lighthouseCenter.x, lighthouseCenter.z), lighthouseCenter.z);
        if (basePos.y <= 0.2f) throw new Exception($"灯塔没落到地面：{basePos.x:F2},{basePos.z:F2}");

        var dotMat = EmissiveMat("LighthouseReplyDot", new Color32(0xff, 0xe1, 0x9c, 255));
        var beamMat = BeamMat();

        var root = new GameObject("LighthouseKit");
        root.transform.SetParent(parent, false);
        root.transform.position = basePos;

        if (lighthouseLantern == null)
            throw new InvalidOperationException("B4 GLB is missing its Lantern chamber node.");

        // 塔顶平台：圆盘视觉 + 一块 Ground 碰撞；顶面 11.98（世界 16.98）
        float deckTop = lighthouseSize.y + 0.16f;
        var floor = new GameObject("deck_floor");
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, deckTop - 0.08f, 0f);
        floor.layer = 6; // Ground
        var floorBox = floor.AddComponent<BoxCollider>();
        floorBox.size = new Vector3(3.2f, 0.16f, 3.2f);

        // The original GLB provides the gallery and rails; these boxes are gameplay barriers.
        foreach (var side in new[] { new Vector3(0, 0, 1), new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(-1, 0, 0) })
        {
            var go = new GameObject("rail_block");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(side.x * 1.62f, deckTop + 0.25f, side.z * 1.62f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(side.x != 0f ? 0.12f : 3.36f, 0.7f, side.z != 0f ? 0.12f : 3.36f);
            // 只在塔顶高度挡人；TownObstacle 没有高度，会在塔下把路线挡死
            var raised = go.AddComponent<RaisedObstacle>();
            raised.minY = basePos.y + deckTop - 0.25f;
        }

        var chamber = lighthouseLantern.bounds;
        var lampCenter = root.transform.InverseTransformPoint(chamber.center);
        SyncLighthouseData(data, basePos, deckTop, chamber.center);
        MountSourceModel(root.transform, "B9", lampCenter - Vector3.up * 0.32f, 0.64f, true);

        // 光束：半透明锥体 + 聚光灯，绕 Y 轴扫海
        var beaconGo = new GameObject("Beacon");
        beaconGo.transform.SetParent(root.transform, false);
        beaconGo.transform.localPosition = lampCenter;
        var pivot = new GameObject("BeaconPivot");
        pivot.transform.SetParent(beaconGo.transform, false);
        var cone = Cone(pivot.transform, beamMat, 16f, 1.6f, 24);
        cone.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        var spotGo = new GameObject("BeaconSpot");
        spotGo.transform.SetParent(pivot.transform, false);
        spotGo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        var spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 42f;
        spot.spotAngle = 34f;
        spot.intensity = 2.4f;
        spot.color = new Color32(0xff, 0xd6, 0x96, 255);
        spot.shadows = LightShadows.None;
        var glowGo = new GameObject("LampGlow");
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localPosition = lampCenter;
        var glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 11f;
        glow.intensity = 1.4f;
        glow.color = new Color32(0xff, 0xd9, 0x9a, 255);
        var beacon = beaconGo.AddComponent<BeaconLight>();
        beacon.pivot = pivot.transform;
        beacon.spot = spot;
        beacon.glow = glow;
        beacon.beam = cone.GetComponent<Renderer>();

        // 塔顶三件回忆和船棚油壶
        foreach (var it in data.items)
        {
            var go = new GameObject("LH_" + it.id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(it.x, it.y, it.z);
            switch (it.id)
            {
                case "oil":
                    MountSourceModel(go.transform, "D1", Vector3.zero, 0.38f, true);
                    break;
                case "log":
                    MountSourceModel(go.transform, "D2", Vector3.zero, 0.28f, false);
                    break;
                case "hat":
                    MountSourceModel(go.transform, "D5", Vector3.zero, 0.30f, false);
                    break;
                case "scope":
                    MountSourceModel(go.transform, "D3", Vector3.zero, 0.52f, true);
                    break;
            }
        }

        // 山那边：约 40 个手电光点，点灯后逐个亮起
        var replyRoot = new GameObject("FlashlightReply");
        replyRoot.transform.SetParent(parent, false);
        var reply = replyRoot.AddComponent<FlashlightReply>();
        var dots = new List<Renderer>();
        // 2026-11-07 新地图：岛后面是住宅区，光点挪到岛北面远处的对岸（海面上方）
        for (int i = 0; i < 40; i++)
        {
            float x = -45f + i * 1.6f;
            float z = 72f + Mathf.Sin(i * 0.7f) * 3f;
            float y = 7.5f + Mathf.Sin(i * 1.3f) * 1.0f + (i % 3) * 0.45f;
            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
            d.name = "reply_" + i;
            d.transform.SetParent(replyRoot.transform, false);
            d.transform.position = new Vector3(x, y, z);
            d.transform.localScale = Vector3.one * 0.3f;
            d.GetComponent<Renderer>().sharedMaterial = dotMat;
            dots.Add(d.GetComponent<Renderer>());
        }
        reply.dots = dots.ToArray();

        root.AddComponent<SkyDimmer>();

        Debug.Log($"LIGHTHOUSE base=({basePos.x:F2},{basePos.y:F2},{basePos.z:F2}) top={basePos.y + lighthouseSize.y:F2} deck={basePos.y + deckTop:F2} dots={dots.Count}");
    }

    // 2026-11-07：塔门、塔顶落点、灯头和塔顶三件回忆都按刚生成的塔重算并写回 lighthouse.json，
    // 换地图后不会再和塔对不上。相对位置沿用旧版（灯头在塔中心，落点偏 0.6 m，三件回忆在 ±1 m）。
    // 船棚油壶（oil）不在塔上，不动。
    static void SyncLighthouseData(LighthouseList data, Vector3 basePos, float deckTop, Vector3 lampWorld)
    {
        float deckY = basePos.y + deckTop;
        float cx = basePos.x, cz = basePos.z;
        data.door = new LighthousePoint { x = cx - 0.08f, y = basePos.y, z = cz - lighthouseSize.z * 0.5f - 0.35f };
        data.deck = new LighthousePoint { x = cx + 0.6f, y = deckY, z = cz + 0.6f };
        data.lamp = new LighthousePoint { x = lampWorld.x, y = deckY + 0.37f, z = lampWorld.z };
        foreach (var it in data.items)
        {
            switch (it.id)
            {
                case "log": it.x = cx; it.y = deckY + 0.04f; it.z = cz + 1.0f; break;
                case "hat": it.x = cx + 1.0f; it.y = deckY + 0.04f; it.z = cz; break;
                case "scope": it.x = cx - 1.0f; it.y = deckY + 0.04f; it.z = cz; break;
            }
        }
        const string path = "Assets/TideTown/Data/Resources/Story/lighthouse.json";
        File.WriteAllText(path, JsonUtility.ToJson(data, true), new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Debug.Log($"LIGHTHOUSE_DATA door=({data.door.x:F2},{data.door.y:F2},{data.door.z:F2}) deck=({data.deck.x:F2},{data.deck.y:F2},{data.deck.z:F2})");
    }

    static GameObject Cone(Transform parent, Material mat, float length, float radius, int segments)
    {
        var go = new GameObject("BeamCone");
        go.transform.SetParent(parent, false);
        var mesh = new Mesh();
        var verts = new List<Vector3> { Vector3.zero };
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            verts.Add(new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, length));
        }
        var tris = new List<int>();
        for (int i = 0; i < segments; i++) { tris.Add(0); tris.Add(2 + i); tris.Add(1 + i); }
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = SaveMesh(mesh, ArtRoot + "/Meshes/LighthouseBeam.asset");
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    static Material EmissiveMat(string name, Color32 c)
    {
        var m = Mat(name, new Color32(0x1c, 0x1a, 0x16, 255));
        if (m.HasProperty("_EmissionColor"))
        {
            m.SetColor("_EmissionColor", new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f) * 2.6f);
            m.EnableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material BeamMat()
    {
        string path = MatRoot + "/LighthouseBeam.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) { Debug.LogWarning("BEAM shader missing"); return null; }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5f);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 10f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(1f, 0.86f, 0.58f, 0.2f));
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- WP-15 结局 ----------
    // 邮局门口的课桌（老周的信）和山那边的新城灯光；真正的远景物贴图留到 WP-17。
}
