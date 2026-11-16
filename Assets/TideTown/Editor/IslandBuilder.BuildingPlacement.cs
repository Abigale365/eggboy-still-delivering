// Targeted placement correction. Preserves the existing terrain, roads and gameplay objects.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class IslandBuilder
{
    const string PlacementPath = "Assets/TideTown/Data/Resources/World/building_placement.json";
    const string PlacementMeshDir = "Assets/TideTown/Art/Meshes/BuildingPlacement";
    [Serializable] public class Placement { public string id; public float x, z, yaw, doorOffset; public bool hasApproach; public float approachX, approachZ; }
    [Serializable] public class PlacementList { public Placement[] buildings; }
    [Serializable] public class EntranceRecord { public string id; public Vector3 before, after, front, approach; public float yaw; }
    [Serializable] public class PlacementReport { public List<EntranceRecord> buildings = new List<EntranceRecord>(); public int clearedPlants, trimmedMeshes; }
    struct AccessStrip { public Vector3 a, b; public float halfWidth; }

    public static void CorrectBuildingPlacement()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        var root = GameObject.Find("Generated").transform;
        ApplyBuildingPlacement(layout, root);
        var old = root.Find("ContourCollision");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        Physics.SyncTransforms();
        BuildContourCollision(layout, root);
        File.WriteAllText(FootprintPath, JsonUtility.ToJson(new FootprintList { buildings = footprints, circles = contourCircles }, true));
        AssetDatabase.ImportAsset(FootprintPath);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save placement scene");
        AssetDatabase.SaveAssets();
        Debug.Log("BUILDING_PLACEMENT_SAVED");
    }

    // Called before contour baking by both the targeted updater and full scene generation.
    static void ApplyBuildingPlacement(Layout layout, Transform root)
    {
        if (!File.Exists(PlacementPath)) return;
        var config = JsonUtility.FromJson<PlacementList>(File.ReadAllText(PlacementPath));
        var report = new PlacementReport();
        var access = new List<AccessStrip>();
        var footprints2D = new List<(Transform root, Bounds bounds)>();
        var oldPaths = root.Find("BuildingEntrances");
        if (oldPaths != null) UnityEngine.Object.DestroyImmediate(oldPaths.gameObject);
        var pathRoot = new GameObject("BuildingEntrances").transform;
        pathRoot.SetParent(root, false);
        var world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
        world.walkPolys.RemoveAll(p => p.id.StartsWith("entrance_"));
        layout.walk = layout.walk.Where(p => !p.id.StartsWith("entrance_")).ToArray();
        var walk = layout.walk.ToList();
        var roadMat = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/IslandCobble.mat");
        if (roadMat == null) roadMat = root.GetComponentsInChildren<MeshRenderer>().First(r => r.name.StartsWith("Tier_pad_stone")).sharedMaterial;
        Directory.CreateDirectory(PlacementMeshDir);
        foreach (var p in config.buildings)
        {
            var t = root.Find(p.id + "_visual");
            if (t == null) throw new Exception("Missing building " + p.id);
            var before = PlacementBounds(t);
            // The root can be offset from the geometry center. Rotate first, then recenter the geometry.
            t.rotation = Quaternion.Euler(0, p.yaw, 0);
            var rotated = PlacementBounds(t);
            t.position += new Vector3(p.x - rotated.center.x, before.min.y - rotated.min.y, p.z - rotated.center.z);
            var bounds = PlacementBounds(t);
            var proxy = root.Find(p.id);
            if (proxy != null && p.id != "B4_lighthouse")
            {
                proxy.position = bounds.center;
                proxy.rotation = Quaternion.identity;
                proxy.localScale = bounds.size;
                var mr = proxy.GetComponent<Renderer>(); if (mr != null) mr.enabled = false;
            }
            if (p.id == "B4_lighthouse") continue; // Its doorway and tower kit were already aligned.
            var forward = t.forward;
            var right = t.right;
            float maxFront = float.NegativeInfinity;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                // Use the wall band, not projecting roof eaves, to locate the entrance plane.
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var q = mf.transform.TransformPoint(v);
                    if (q.y < bounds.min.y + .15f || q.y > bounds.min.y + 1.8f) continue;
                    maxFront = Mathf.Max(maxFront, Vector3.Dot(q - bounds.center, forward));
                }
            }
            if (float.IsNegativeInfinity(maxFront)) maxFront = 1.5f;
            var door = new Vector3(p.x, bounds.min.y, p.z) + forward * (maxFront + .6f) + right * p.doorOffset;
            var join = p.hasApproach ? new Vector3(p.approachX, bounds.min.y, p.approachZ) : NearestPlacementWalk(door, forward, layout.walk, bounds.min.y);
            var start = door - forward * .6f;
            access.Add(new AccessStrip { a = start, b = join, halfWidth = 1.05f });
            footprints2D.Add((t, bounds));
            var poly = MakeEntrance(p.id, start, join, bounds.min.y, pathRoot, roadMat);
            walk.Add(poly);
            world.walkPolys.Add(new WalkPoly { id = poly.id, y = poly.y, points = poly.x.Select((x, i) => new Vector2(x, poly.z[i])).ToArray() });
            report.buildings.Add(new EntranceRecord { id = p.id, before = before.center, after = bounds.center, yaw = p.yaw, front = door, approach = join });
        }
        layout.walk = walk.ToArray();
        // The basket's interaction coordinate is updated in the same data file as its scene object.
        var basket = root.Find("Bakery_basket");
        if (basket != null)
        {
            var item = BakeryData.Load().FirstOrDefault(b => b.id == "basket");
            if (item != null) basket.position = new Vector3(item.x, item.y, item.z);
        }
        // Open just the frontage corridors. Keep the decorative enclosure elsewhere.
        foreach (var group in root.GetComponentsInChildren<Transform>().Where(t => t.name == "Trees" || t.name == "Bushes").ToArray())
        {
            foreach (Transform plant in group)
            {
                if (!plant.gameObject.activeSelf) continue;
                var rs = plant.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var point = new Vector3(b.center.x, b.min.y, b.center.z);
                float radius = group.name == "Trees" ? .8f : Mathf.Min(1.5f, Mathf.Max(b.extents.x, b.extents.z));
                if (InPlacementClearance(point, radius, access, footprints2D)) { plant.gameObject.SetActive(false); report.clearedPlants++; }
            }
        }
        // Grass and fence meshes are batches: trim triangles only inside the entrance or building footprint.
        int meshIndex = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>().ToArray())
        {
            bool grass = HasPlacementAncestor(mf.transform, "GrassField");
            bool fence = HasPlacementAncestor(mf.transform, "Fences");
            if ((!grass && !fence) || mf.sharedMesh == null) continue;
            var source = mf.sharedMesh;
            var verts = source.vertices; bool changed = false;
            var submeshes = new List<int[]>();
            for (int sm = 0; sm < source.subMeshCount; sm++)
            {
                var tris = source.GetTriangles(sm); var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var c = mf.transform.TransformPoint((verts[tris[i]] + verts[tris[i+1]] + verts[tris[i+2]]) / 3f);
                    bool cut = InPlacementClearance(c, 0, access, footprints2D, !fence);
                    if (cut) changed = true; else { keep.Add(tris[i]); keep.Add(tris[i+1]); keep.Add(tris[i+2]); }
                }
                submeshes.Add(keep.ToArray());
            }
            if (!changed) continue;
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = "PlacementClearance_" + meshIndex;
            for (int sm = 0; sm < submeshes.Count; sm++) mesh.SetTriangles(submeshes[sm], sm);
            mesh.RecalculateBounds();
            var assetPath = PlacementMeshDir + "/Clearance_" + meshIndex++ + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, assetPath);
            mf.sharedMesh = mesh;
            var mc = mf.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = mesh;
            report.trimmedMeshes++;
        }
        Physics.SyncTransforms();
        Directory.CreateDirectory("docs/evidence/building-placement-20261114");
        File.WriteAllText("docs/evidence/building-placement-20261114/placements.json", JsonUtility.ToJson(report, true));
        Debug.Log($"BUILDING_PLACEMENT buildings={report.buildings.Count} plants={report.clearedPlants} meshes={report.trimmedMeshes}");
    }

    static Bounds PlacementBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        if (rs.Length == 0) throw new Exception("No visible building mesh " + t.name);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }
    static bool HasPlacementAncestor(Transform t, string name) { for (; t != null; t = t.parent) if (t.name == name) return true; return false; }
    static float PlacementSegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = a.y = b.y = 0; var d = b-a;
        return Vector3.Distance(p, a + d * Mathf.Clamp01(Vector3.Dot(p-a,d) / Mathf.Max(.0001f,d.sqrMagnitude)));
    }
    static bool InPlacementClearance(Vector3 p, float radius, List<AccessStrip> strips, List<(Transform root, Bounds bounds)> buildings, bool includeBuildings = true)
    {
        foreach (var s in strips) if (Mathf.Abs(p.y-s.a.y) < 5f && PlacementSegmentDistance(p,s.a,s.b) < s.halfWidth + radius) return true;
        if (!includeBuildings) return false;
        foreach (var b in buildings)
        {
            if (Mathf.Abs(p.y-b.bounds.min.y) > 5f) continue;
            if (p.x > b.bounds.min.x-radius && p.x < b.bounds.max.x+radius && p.z > b.bounds.min.z-radius && p.z < b.bounds.max.z+radius) return true;
        }
        return false;
    }
    static Vector3 NearestPlacementWalk(Vector3 door, Vector3 forward, Area[] areas, float y)
    {
        var best = door + forward * 2f; float score = float.MaxValue;
        foreach (var a in areas)
        {
            if (Mathf.Abs(a.y-y) > .15f) continue;
            for (int i=0;i<a.x.Length;i++)
            {
                int j=(i+1)%a.x.Length;
                var v=new Vector3(a.x[i],y,a.z[i]); var d=new Vector3(a.x[j]-v.x,0,a.z[j]-v.z);
                var q=v+d*Mathf.Clamp01(Vector3.Dot(door-v,d)/Mathf.Max(.00001f,d.sqrMagnitude));
                var delta=q-door;
                if (Vector3.Dot(delta,forward)<-.1f) continue;
                var cost=delta.magnitude + Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,forward)))*.3f;
                if(cost<score) {score=cost;best=q + (q-door).normalized*.5f;}
            }
        }
        if(score>8f) throw new Exception("Entrance too far from street: " + door);
        return best;
    }
    static Area MakeEntrance(string id, Vector3 a, Vector3 b, float y, Transform root, Material mat)
    {
        var d=b-a;d.y=0;if(d.magnitude<.5f)b=a+d.normalized*.5f;
        var side=Vector3.Cross(Vector3.up,(b-a).normalized)*.9f;
        var verts=new[]{a-side,a+side,b+side,b-side};
        for(int i=0;i<verts.Length;i++)verts[i].y=y+.035f;
        var mesh=new Mesh { name="Entrance_"+id, vertices=verts, triangles=new[]{0,2,1,0,3,2}, uv=verts.Select(v=>new Vector2(v.x,v.z)).ToArray() };
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=PlacementMeshDir+"/Entrance_"+id+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject("Entrance_"+id);go.transform.SetParent(root,false);go.layer=6;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;go.AddComponent<MeshCollider>().sharedMesh=mesh;
        return new Area { id="entrance_"+id,y=y+.035f,walk=true,zone="stone",x=verts.Select(v=>v.x).ToArray(),z=verts.Select(v=>v.z).ToArray() };
    }
}
