// 2026-11-16 只读普查：剧情道具落点（离地 / 悬空 / 压进墙）、面包店和邮局的门窗部件、A1 烤炉的炉门和炉膛。
// 输出 Build/probe/props.txt。用法：-executeMethod PropSurvey.Run
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PropSurvey
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/TideTown/Scenes/TideTown_Main.unity", OpenSceneMode.Single);
        var sb = new StringBuilder();
        var gen = GameObject.Find("Generated").transform;
        int ground = 1 << 6;
        float GroundAt(Vector3 p, float above)
        {
            float best = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, above, p.z), Vector3.down, 80f, ground, QueryTriggerInteraction.Ignore)) best = Mathf.Max(best, h.point.y);
            return best;
        }
        Bounds B(Transform t) { var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray(); if (rs.Length == 0) return new Bounds(t.position, Vector3.zero); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        string V(Vector3 v) => $"({v.x:F2},{v.y:F3},{v.z:F2})";

        sb.AppendLine("== props");
        foreach (Transform c in gen)
        {
            string n = c.name;
            if (!(n.StartsWith("Bakery_") || n.StartsWith("S_") || n.StartsWith("LH_") || n.StartsWith("L_") || n.Contains("Mailbox") || n.Contains("ScanStation") || n.Contains("EndingDesk") || n.StartsWith("Lighthouse"))) continue;
            var b = B(c);
            // 包围盒底面四角 + 中心下面的地面
            var pts = new[] { b.center, new Vector3(b.min.x, 0, b.min.z), new Vector3(b.max.x, 0, b.min.z), new Vector3(b.min.x, 0, b.max.z), new Vector3(b.max.x, 0, b.max.z) };
            var gs = pts.Select(p => GroundAt(p, b.max.y + 0.5f)).ToArray();
            sb.AppendLine($"{n} root={V(c.position)} bmin={V(b.min)} bmax={V(b.max)} groundC={gs[0]:F3} corners={string.Join("/", gs.Skip(1).Select(g => g.ToString("F2")))} gapC={(b.min.y - gs[0]):F3} active={c.gameObject.activeInHierarchy}");
            foreach (Transform k in c) sb.AppendLine($"    child {k.name} pos={V(k.position)} b={V(B(k).min)}..{V(B(k).max)}");
        }

        foreach (var bn in new[] { "B1_post_office_visual", "B2_bakery_visual" })
        {
            var t = gen.Find(bn);
            if (t == null) { sb.AppendLine("missing " + bn); continue; }
            var bb = B(t);
            sb.AppendLine($"== {bn} root={V(t.position)} rotY={t.eulerAngles.y:F0} fwd={V(t.forward)} bounds={V(bb.min)}..{V(bb.max)}");
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                string ln = r.name.ToLowerInvariant();
                if (!(ln.Contains("door") || ln.Contains("arch") || ln.Contains("frame") || ln.Contains("window") || ln.Contains("opening") || ln.Contains("bell") || ln.Contains("sign") || ln.Contains("lintel") || ln.Contains("step"))) continue;
                sb.AppendLine($"    {r.name} b={V(r.bounds.min)}..{V(r.bounds.max)}");
            }
        }

        var stove = gen.Find("Bakery_stove");
        if (stove != null)
        {
            sb.AppendLine($"== A1 oven root={V(stove.position)} rotY={stove.eulerAngles.y:F0} fwd={V(stove.forward)}");
            foreach (var r in stove.GetComponentsInChildren<Renderer>())
            {
                var lb = new Bounds(stove.InverseTransformPoint(r.bounds.center), Vector3.zero);
                foreach (var cc in new[] { r.bounds.min, r.bounds.max, new Vector3(r.bounds.min.x, r.bounds.min.y, r.bounds.max.z), new Vector3(r.bounds.max.x, r.bounds.max.y, r.bounds.min.z) }) lb.Encapsulate(stove.InverseTransformPoint(cc));
                sb.AppendLine($"    {r.name} mat={(r.sharedMaterial != null ? r.sharedMaterial.name : "-")} localB={V(lb.min)}..{V(lb.max)}");
            }
            // 炉膛探测：从炉前沿 -z 方向往里（本地 +z）水平打射线，找到第一个面（炉门/后壁）
            var mcs = new List<MeshCollider>();
            foreach (var mf in stove.GetComponentsInChildren<MeshFilter>()) { var go = new GameObject("tmp"); go.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation); go.transform.localScale = mf.transform.lossyScale; var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; go.layer = 31; mcs.Add(mc); }
            Physics.SyncTransforms();
            sb.AppendLine("    probe (local y, local x) -> depth hits from front (local z) ...");
            for (float y = 0.2f; y <= 1.4f; y += 0.1f)
            {
                var line = new StringBuilder($"    y={y:F1}:");
                for (float x = -0.6f; x <= 0.61f; x += 0.15f)
                {
                    var o = stove.TransformPoint(new Vector3(x, y, -2f));
                    var d = stove.TransformDirection(Vector3.forward);
                    var hits = Physics.RaycastAll(o, d, 4f, 1 << 31).OrderBy(h => h.distance).Take(2).ToArray();
                    line.Append(hits.Length == 0 ? "  ----" : "  " + string.Join("|", hits.Select(h => (stove.InverseTransformPoint(h.point).z).ToString("F2"))));
                }
                sb.AppendLine(line.ToString());
            }
            foreach (var mc in mcs) Object.DestroyImmediate(mc.gameObject);
        }
        Directory.CreateDirectory("Build/probe");
        File.WriteAllText("Build/probe/props.txt", sb.ToString());
        Debug.Log("PROBE_DONE props");
    }
}
