using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    static bool HasJoinedSlope(string id) =>
        id == "mountain" || id == "branch_n" || id == "to_lighthouse" || id == "to_shed";

    // Regenerate the visual and ground-collision assets without rebuilding the town scene.
    public static void RebuildSlopeMeshes()
    {
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        int updated = 0;
        foreach (var road in layout.roads)
        {
            if (!HasJoinedSlope(road.id)) continue;
            string path = "Assets/TideTown/Art/Meshes/Main_road_" + road.id;
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path + ".asset") == null ||
                AssetDatabase.LoadAssetAtPath<Mesh>(path + "_col.asset") == null)
                throw new InvalidOperationException("Missing slope mesh assets for " + road.id);
            SaveMesh(JoinedSlopeMesh(road, false), path + ".asset");
            SaveMesh(JoinedSlopeMesh(road, true), path + "_col.asset");
            updated++;
        }
        if (updated != 4) throw new InvalidOperationException("Expected four slope roads");
        AssetDatabase.SaveAssets();
        Debug.Log("SLOPE_MESHES updated=" + updated);
    }

    struct SlopeSection
    {
        public Vector3 center;
        public Vector3 side;
        public float distance;
        public float plazaShoulder;
        public float plazaHeight;
    }

    static Mesh JoinedSlopeMesh(RoadDef road, bool groundOnly)
    {
        var sections = SlopeSections(road);
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var top = new List<int>();
        var curb = new List<int>();
        var walk = new List<int>();
        var skirt = new List<int>();
        float edge = road.width * 0.5f;

        AddSlopeRibbon(sections, vertices, uv, top, -edge, edge, 0f, 0f, edge);
        AddSlopeRibbon(sections, vertices, uv, curb, -edge - 0.18f, -edge, 0.08f, 0f, edge);
        AddSlopeRibbon(sections, vertices, uv, curb, edge, edge + 0.18f, 0f, 0.08f, edge);
        AddSlopeRibbon(sections, vertices, uv, walk, -edge - 0.93f, -edge - 0.18f, 0.08f, 0.08f, edge);
        AddSlopeRibbon(sections, vertices, uv, walk, edge + 0.18f, edge + 0.93f, 0.08f, 0.08f, edge);
        if (!groundOnly)
        {
            AddSlopeRibbon(sections, vertices, uv, skirt, -edge - 0.93f, -edge - 0.93f, 0.08f, -0.25f, edge);
            AddSlopeRibbon(sections, vertices, uv, skirt, edge + 0.93f, edge + 0.93f, -0.25f, 0.08f, edge);
        }

        var mesh = new Mesh { name = "road_" + road.id + (groundOnly ? "_col" : ""), subMeshCount = groundOnly ? 3 : 4 };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(top, 0);
        mesh.SetTriangles(curb, 1);
        mesh.SetTriangles(walk, 2);
        if (!groundOnly) mesh.SetTriangles(skirt, 3);
        mesh.RecalculateNormals();
        if (!groundOnly) mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static List<SlopeSection> SlopeSections(RoadDef road)
    {
        var nodes = new List<Vector3>();
        for (int i = 0; i < road.x.Length; i++)
            nodes.Add(new Vector3(road.x[i], road.y[i], road.z[i]));

        // Bring the north ramp up to the housing platform before its vertical face.
        // The longer approach from the plaza keeps the grade below 30 degrees.
        if (road.id == "branch_n")
        {
            var start = nodes[0];
            var end = nodes[nodes.Count - 1];
            var direction = SlopeDirection(start, end);
            nodes.Clear();
            nodes.Add(start - direction * 1.28f);
            nodes.Add(end - direction * 0.31f);
            nodes.Add(end);
        }
        else if (road.id == "mountain")
        {
            // Match the level cap of the short road beyond the summit.
            AddUpperLanding(nodes, 0.36f);
        }
        else if (road.id == "to_lighthouse")
        {
            AddUpperLanding(nodes, 1.88f);
        }
        else if (road.id == "to_shed")
        {
            // The edge of the town platform runs diagonally across the top of this ramp.
            // Carry the level landing across it, then regrade down to the existing low point.
            var landing = nodes[1] + SlopeDirection(nodes[1], nodes[2]) * 0.34f;
            float span = SlopeDistance(landing, nodes[2]) + SlopeDistance(nodes[2], nodes[3]) + SlopeDistance(nodes[3], nodes[4]);
            float distance = 0f;
            var from = landing;
            for (int i = 2; i < 4; i++)
            {
                var point = nodes[i];
                distance += SlopeDistance(from, point);
                point.y = Mathf.Lerp(landing.y, nodes[4].y, distance / span);
                nodes[i] = point;
                from = point;
            }
            nodes.Insert(2, landing);
            AddUpperLanding(nodes, 1.33f);
        }

        var last = nodes[nodes.Count - 1];
        nodes.Insert(0, nodes[0] - SlopeDirection(nodes[0], nodes[1]) * 0.15f);
        nodes.Add(last + SlopeDirection(nodes[nodes.Count - 2], last) * 0.15f);

        var distanceAt = new float[nodes.Count];
        var directions = new Vector3[nodes.Count - 1];
        var grades = new float[directions.Length];
        for (int i = 0; i < directions.Length; i++)
        {
            var delta = nodes[i + 1] - nodes[i];
            float length = SlopeDistance(nodes[i], nodes[i + 1]);
            directions[i] = new Vector3(delta.x, 0f, delta.z) / length;
            grades[i] = delta.y / length;
            distanceAt[i + 1] = distanceAt[i] + length;
        }

        var sides = new Vector3[nodes.Count];
        var blend = new float[nodes.Count];
        sides[0] = Vector3.Cross(directions[0], Vector3.up);
        sides[sides.Length - 1] = Vector3.Cross(directions[directions.Length - 1], Vector3.up);
        for (int i = 1; i < sides.Length - 1; i++)
        {
            var outgoing = Vector3.Cross(directions[i], Vector3.up);
            var miter = Vector3.Cross((directions[i - 1] + directions[i]).normalized, Vector3.up);
            sides[i] = miter / Mathf.Max(0.65f, Vector3.Dot(miter, outgoing));
            float shortSide = Mathf.Min(distanceAt[i] - distanceAt[i - 1], distanceAt[i + 1] - distanceAt[i]);
            blend[i] = Mathf.Min(0.28f, shortSide * 0.45f);
        }

        var sections = new List<SlopeSection>();
        for (int i = 0; i < directions.Length; i++)
        {
            float length = distanceAt[i + 1] - distanceAt[i];
            int steps = Mathf.CeilToInt(length / 0.12f);
            for (int step = 0; step < steps; step++)
            {
                float t = step / (float)steps;
                float offset = length * t;
                var center = Vector3.Lerp(nodes[i], nodes[i + 1], t);
                if (offset < blend[i])
                    center.y = RoundedGrade(nodes[i].y, grades[i - 1], grades[i], blend[i], offset);
                else if (offset > length - blend[i + 1])
                    center.y = RoundedGrade(nodes[i + 1].y, grades[i], grades[i + 1], blend[i + 1], offset - length);
                center.y += 0.026f;
                float plazaShoulder = road.id == "branch_n"
                    ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(road.z[0] - 0.12f, road.z[0] + 0.25f, center.z))
                    : 0f;
                sections.Add(new SlopeSection
                {
                    center = center,
                    side = Vector3.Lerp(sides[i], sides[i + 1], t),
                    distance = distanceAt[i] + offset,
                    plazaShoulder = plazaShoulder,
                    plazaHeight = road.y[0] + 0.022f,
                });
            }
        }
        sections.Add(new SlopeSection { center = nodes[nodes.Count - 1] + Vector3.up * 0.026f, side = sides[sides.Length - 1], distance = distanceAt[distanceAt.Length - 1] });
        return sections;
    }

    static void AddUpperLanding(List<Vector3> nodes, float length)
    {
        var end = nodes[nodes.Count - 1];
        var previous = nodes[nodes.Count - 2];
        nodes.Insert(nodes.Count - 1, end - SlopeDirection(previous, end) * length);
    }

    static float SlopeDistance(Vector3 from, Vector3 to) =>
        new Vector2(to.x - from.x, to.z - from.z).magnitude;

    static Vector3 SlopeDirection(Vector3 from, Vector3 to)
    {
        var delta = to - from;
        delta.y = 0f;
        return delta.normalized;
    }

    // Blend two grades while meeting both original lines at the ends of the blend.
    static float RoundedGrade(float height, float incoming, float outgoing, float radius, float offset)
    {
        float t = offset + radius;
        return height - incoming * radius + incoming * t + (outgoing - incoming) * t * t / (4f * radius);
    }

    static void AddSlopeRibbon(List<SlopeSection> sections, List<Vector3> vertices, List<Vector2> uv,
        List<int> triangles, float left, float right, float leftHeight, float rightHeight, float roadEdge)
    {
        int start = vertices.Count;
        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            vertices.Add(SlopeVertex(section, left, leftHeight, roadEdge));
            vertices.Add(SlopeVertex(section, right, rightHeight, roadEdge));
            uv.Add(new Vector2(left, section.distance - 0.15f));
            uv.Add(new Vector2(right, section.distance - 0.15f));
            if (i == 0) continue;
            int a = start + (i - 1) * 2;
            triangles.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
        }
    }

    static Vector3 SlopeVertex(SlopeSection section, float offset, float height, float roadEdge)
    {
        var vertex = section.center + section.side * offset + Vector3.up * height;
        // Keep the plaza approach level at the outside of the shoulder so traffic
        // across the plaza can cross the new ramp apron without stepping off a ledge.
        float shoulder = Mathf.Clamp01((Mathf.Abs(offset) - roadEdge) / 0.93f) * section.plazaShoulder;
        vertex.y = Mathf.Lerp(vertex.y, section.plazaHeight + Mathf.Min(0f, height), shoulder);
        return vertex;
    }
}
