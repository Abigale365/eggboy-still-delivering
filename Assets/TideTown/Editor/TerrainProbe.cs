// 2026-11-16 只读诊断：打开主场景（不保存），对几块区域按 5 cm 网格从上往下打射线，记下每一格所有可见表面
// （临时给区域里的可见网格挂 MeshCollider），先按运行时 IslandWorld.Awake 的视觉修补处理一遍网格。
// 输出 Build/probe/<区域>.json，给 python 画俯视图找穿模、同高闪烁、悬空。
// 用法：Unity -batchmode -executeMethod TerrainProbe.Run  （区域写在 Build/probe/regions.json）
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TerrainProbe
{
    [Serializable] class Region { public string name; public float x0, x1, z0, z1, cell = 0.05f; }
    [Serializable] class RegionList { public Region[] regions; }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/TideTown/Scenes/TideTown_Main.unity", OpenSceneMode.Single);
        var regions = JsonUtility.FromJson<RegionList>(File.ReadAllText("Build/probe/regions.json")).regions;
        var world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
        if (world != null)
        {
            var m = typeof(IslandWorld).GetMethod("CleanVisualRoadAndTierSeams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            m?.Invoke(world, null);
        }
        const int ProbeLayer = 31;
        var owners = new List<string>(); var ownerIdx = new Dictionary<string, int>();
        var temp = new List<GameObject>();
        foreach (var mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
        {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            bool grassBlades = false;
            for (var t = mr.transform; t != null; t = t.parent) if (t.name == "GrassField" || t.name == "Sea" || t.name == "WaterSystem") grassBlades = true;
            if (grassBlades) continue;
            var b = mr.bounds; bool hit = false;
            foreach (var r in regions) if (b.max.x >= r.x0 && b.min.x <= r.x1 && b.max.z >= r.z0 && b.min.z <= r.z1) hit = true;
            if (!hit) continue;
            var go = new GameObject("probe"); go.layer = ProbeLayer;
            go.transform.SetPositionAndRotation(mr.transform.position, mr.transform.rotation);
            go.transform.localScale = mr.transform.lossyScale;
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
            string path = Path(mr.transform);
            if (!ownerIdx.TryGetValue(path, out int oi)) { oi = owners.Count; owners.Add(path); ownerIdx[path] = oi; }
            go.name = "probe_" + oi;
            temp.Add(go);
        }
        Physics.SyncTransforms();
        Directory.CreateDirectory("Build/probe");
        int mask = 1 << ProbeLayer;
        foreach (var r in regions)
        {
            int nx = Mathf.CeilToInt((r.x1 - r.x0) / r.cell), nz = Mathf.CeilToInt((r.z1 - r.z0) / r.cell);
            var sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(r.name).Append("\",\"x0\":").Append(r.x0).Append(",\"z0\":").Append(r.z0).Append(",\"cell\":").Append(r.cell)
              .Append(",\"nx\":").Append(nx).Append(",\"nz\":").Append(nz).Append(",\"cells\":[");
            var used = new HashSet<int>();
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = r.x0 + (ix + 0.5f) * r.cell, z = r.z0 + (iz + 0.5f) * r.cell;
                    var hits = Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 80f, mask, QueryTriggerInteraction.Collide)
                        .OrderByDescending(h => h.point.y).Take(8).ToArray();
                    if (ix + iz > 0) sb.Append(',');
                    // 真实物理地面（Ground 层碰撞体）最高点，放在第一位；没有就 -99
                    float phys = -99f;
                    foreach (var gh in Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 80f, 1 << 6, QueryTriggerInteraction.Ignore))
                        phys = Mathf.Max(phys, gh.point.y);
                    sb.Append('[').Append(phys.ToString("F4")).Append(",-1,1");
                    if (hits.Length > 0) sb.Append(',');
                    for (int k = 0; k < hits.Length; k++)
                    {
                        int o = int.Parse(hits[k].collider.gameObject.name.Substring(6)); used.Add(o);
                        if (k > 0) sb.Append(',');
                        sb.Append(hits[k].point.y.ToString("F4")).Append(',').Append(o).Append(',').Append(hits[k].normal.y.ToString("F2"));
                    }
                    sb.Append(']');
                }
            sb.Append("],\"owners\":{");
            bool first = true;
            foreach (int o in used) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(o).Append("\":\"").Append(owners[o].Replace("\\", "/").Replace("\"", "'")).Append('"'); }
            sb.Append("},\"walk\":[");
            first = true;
            foreach (var p in world.walkPolys) { if (!first) sb.Append(','); first = false; AppendPoly(sb, p); }
            sb.Append("],\"lawn\":[");
            first = true;
            foreach (var p in world.lawnPolys) { if (!first) sb.Append(','); first = false; AppendPoly(sb, p); }
            sb.Append("],\"circles\":[");
            first = true;
            foreach (var set in UnityEngine.Object.FindObjectsOfType<TownObstacleSet>())
                for (int i = 0; i < set.x.Length; i++)
                {
                    if (set.x[i] < r.x0 - 1 || set.x[i] > r.x1 + 1 || set.z[i] < r.z0 - 1 || set.z[i] > r.z1 + 1) continue;
                    if (!first) sb.Append(','); first = false;
                    sb.Append('[').Append(set.x[i].ToString("F3")).Append(',').Append(set.z[i].ToString("F3")).Append(',').Append(set.r[i].ToString("F3")).Append(',').Append(set.top[i].ToString("F3")).Append(']');
                }
            sb.Append("],\"objects\":[");
            first = true;
            foreach (var mr in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (!mr.enabled || !mr.gameObject.activeInHierarchy || mr is ParticleSystemRenderer) continue;
                var b = mr.bounds;
                if (b.max.x < r.x0 || b.min.x > r.x1 || b.max.z < r.z0 || b.min.z > r.z1) continue;
                string path = Path(mr.transform);
                if (path.Contains("GrassField")) continue;
                if (!first) sb.Append(','); first = false;
                sb.Append("{\"p\":\"").Append(path.Replace("\"", "'")).Append("\",\"min\":[").Append(V(b.min)).Append("],\"max\":[").Append(V(b.max)).Append("]}");
            }
            sb.Append("]}");
            File.WriteAllText("Build/probe/" + r.name + ".json", sb.ToString());
            Debug.Log($"PROBE {r.name} {nx}x{nz}");
        }
        foreach (var g in temp) UnityEngine.Object.DestroyImmediate(g);
        Debug.Log("PROBE_DONE");
    }

    // 把某个网格的世界坐标顶点打出来（-executeMethod TerrainProbe.DumpGates）
    public static void DumpGates()
    {
        EditorSceneManager.OpenScene("Assets/TideTown/Scenes/TideTown_Main.unity", OpenSceneMode.Single);
        var sb = new StringBuilder();
        foreach (var n in new[] { "school_east_gate", "school_gate", "school_west_gate" })
        {
            var go = GameObject.Find("Generated/CoastalPromenade/" + n);
            if (go == null) { sb.AppendLine(n + " missing"); continue; }
            var mf = go.GetComponent<MeshFilter>();
            var v = mf.sharedMesh.vertices;
            sb.Append(n).Append(':');
            for (int i = 0; i < v.Length; i++) { var w = go.transform.TransformPoint(v[i]); sb.Append(' ').Append(i).Append('(').Append(w.x.ToString("F2")).Append(',').Append(w.y.ToString("F3")).Append(',').Append(w.z.ToString("F2")).Append(')'); }
            sb.AppendLine();
        }
        File.WriteAllText("Build/probe/gates.txt", sb.ToString());
        Debug.Log("PROBE_DONE gates");
    }

    static string V(Vector3 v) => v.x.ToString("F3") + "," + v.y.ToString("F3") + "," + v.z.ToString("F3");
    static void AppendPoly(StringBuilder sb, WalkPoly p)
    {
        sb.Append("{\"id\":\"").Append(p.id).Append("\",\"y\":").Append(p.y.ToString("F3")).Append(",\"pts\":[");
        for (int i = 0; i < p.points.Length; i++) { if (i > 0) sb.Append(','); sb.Append(p.points[i].x.ToString("F3")).Append(',').Append(p.points[i].y.ToString("F3")); }
        sb.Append("]}");
    }
    static string Path(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        if (parts.Count > 1 && parts[0] == "Generated") parts.RemoveAt(0);
        return string.Join("/", parts.Take(5));
    }
}
