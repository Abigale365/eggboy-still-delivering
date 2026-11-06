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

        var metal = Mat("LighthouseMetal", new Color32(0x3a, 0x3f, 0x44, 255));
        var wood = Mat("LighthouseDeck", new Color32(0x8a, 0x62, 0x3e, 255));
        var glass = Mat("LighthouseGlass", new Color32(0xb9, 0xcb, 0xd2, 255));
        var cream = Mat("LighthouseCream", new Color32(0xe8, 0xd6, 0xb4, 255));
        var dark = Mat("LighthouseDark", new Color32(0x2e, 0x2e, 0x30, 255));
        var lampMat = EmissiveMat("LighthouseLamp", new Color32(0xff, 0xd9, 0x9a, 255));
        var dotMat = EmissiveMat("LighthouseReplyDot", new Color32(0xff, 0xe1, 0x9c, 255));
        var beamMat = BeamMat();

        var root = new GameObject("LighthouseKit");
        root.transform.SetParent(parent, false);
        root.transform.position = basePos;

        // 塔门和塔基台阶：门朝 30 号点（-z 方向）
        float front = -lighthouseSize.z * 0.5f;
        Cube(root.transform, "door", new Vector3(0f, 0.68f, front - 0.03f), new Vector3(0.6f, 1.35f, 0.06f), metal);
        Cube(root.transform, "doorstep", new Vector3(0f, 0.06f, front - 0.22f), new Vector3(0.95f, 0.12f, 0.4f), wood);
        Cube(root.transform, "window", new Vector3(0f, 4.3f, front - 0.02f), new Vector3(0.3f, 0.42f, 0.05f), glass);

        // 塔顶平台：圆盘视觉 + 一块 Ground 碰撞；顶面 11.98（世界 16.98）
        float deckTop = lighthouseSize.y + 0.16f;
        Cyl(root.transform, "deck", new Vector3(0f, deckTop - 0.08f, 0f), new Vector3(3.4f, 0.08f, 3.4f), wood);
        var floor = new GameObject("deck_floor");
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, deckTop - 0.08f, 0f);
        floor.layer = 6; // Ground
        var floorBox = floor.AddComponent<BoxCollider>();
        floorBox.size = new Vector3(3.2f, 0.16f, 3.2f);

        // 栏杆：视觉一圈，碰撞用四块方形挡边（EggboyCollider 只认 AABB）
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            Cyl(root.transform, "railpost", new Vector3(Mathf.Sin(a) * 1.62f, deckTop + 0.22f, Mathf.Cos(a) * 1.62f),
                new Vector3(0.05f, 0.22f, 0.05f), dark);
            var rail = Cube(root.transform, "rail", new Vector3(Mathf.Sin(a) * 1.62f, deckTop + 0.42f, Mathf.Cos(a) * 1.62f),
                new Vector3(0.95f, 0.05f, 0.07f), wood);
            rail.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
        }
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

        // 灯室、帽盖、灯芯
        float lampBottom = deckTop + 0.12f;
        Cyl(root.transform, "lamp_glass", new Vector3(0f, lampBottom + 0.35f, 0f), new Vector3(1.32f, 0.375f, 1.32f), glass);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            Cube(root.transform, "mullion", new Vector3(Mathf.Sin(a) * 0.6f, lampBottom + 0.35f, Mathf.Cos(a) * 0.6f),
                new Vector3(0.06f, 0.75f, 0.06f), dark);
        }
        Cyl(root.transform, "lamp_cap", new Vector3(0f, lampBottom + 0.76f, 0f), new Vector3(1.7f, 0.06f, 1.7f), metal);
        Cyl(root.transform, "finial", new Vector3(0f, lampBottom + 0.88f, 0f), new Vector3(0.12f, 0.08f, 0.12f), metal);
        var lampOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.DestroyImmediate(lampOrb.GetComponent<Collider>());
        lampOrb.name = "lamp_orb";
        lampOrb.transform.SetParent(root.transform, false);
        lampOrb.transform.localPosition = new Vector3(0f, lampBottom + 0.35f, 0f);
        lampOrb.transform.localScale = Vector3.one * 0.34f;
        lampOrb.GetComponent<Renderer>().sharedMaterial = lampMat;

        // 光束：半透明锥体 + 聚光灯，绕 Y 轴扫海
        var beaconGo = new GameObject("Beacon");
        beaconGo.transform.SetParent(root.transform, false);
        beaconGo.transform.localPosition = new Vector3(0f, lampBottom + 0.35f, 0f);
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
        glowGo.transform.localPosition = new Vector3(0f, lampBottom + 0.35f, 0f);
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
                    Cyl(go.transform, "can", new Vector3(0f, 0.18f, 0f), new Vector3(0.22f, 0.18f, 0.22f), metal);
                    Cube(go.transform, "handle", new Vector3(0f, 0.34f, 0f), new Vector3(0.04f, 0.1f, 0.16f), metal);
                    break;
                case "log":
                    Cube(go.transform, "book", new Vector3(0f, 0.025f, 0f), new Vector3(0.26f, 0.05f, 0.19f), cream);
                    break;
                case "hat":
                    Cyl(go.transform, "brim", new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.01f, 0.3f), cream);
                    Cyl(go.transform, "crown", new Vector3(0f, 0.07f, 0f), new Vector3(0.16f, 0.06f, 0.16f), cream);
                    break;
                case "scope":
                    var body = Cyl(go.transform, "scope", new Vector3(0f, 0.32f, 0f), new Vector3(0.09f, 0.3f, 0.09f), metal);
                    body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Cyl(go.transform, "stand", new Vector3(0f, 0.14f, 0f), new Vector3(0.04f, 0.14f, 0.04f), dark);
                    break;
            }
        }

        // 山那边：约 40 个手电光点，点灯后逐个亮起
        var replyRoot = new GameObject("FlashlightReply");
        replyRoot.transform.SetParent(parent, false);
        var reply = replyRoot.AddComponent<FlashlightReply>();
        var dots = new List<Renderer>();
        for (int i = 0; i < 40; i++)
        {
            float x = -34f + i * 1.02f;
            float z = 23.5f + Mathf.Sin(i * 0.7f) * 1.6f;
            float y = 10.9f + Mathf.Sin(i * 1.3f) * 0.8f + (i % 3) * 0.35f;
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
