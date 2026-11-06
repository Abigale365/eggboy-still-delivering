// WP-06 场景世界：地面走 Ground 层射线，阻挡读 TownObstacle，可走区域用多边形 + 水位。
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WalkPoly
{
    public string id;
    public float y;
    public Vector2[] points;
}

public class IslandWorld : MonoBehaviour, IEggboyWorld
{
    public LayerMask groundMask;
    public LayerMask obstacleMask;
    public Transform mailboxMarker;
    public Transform spawnMarker;
    public float waterLevel;

    public List<WalkPoly> walkPolys = new List<WalkPoly>();

    readonly List<EggboyCollider> colliders = new List<EggboyCollider>();

    public IReadOnlyList<EggboyCollider> Colliders => colliders;
    public Vector3 MailboxPos => mailboxMarker != null ? mailboxMarker.position : Vector3.zero;
    public Vector3 SpawnPos => spawnMarker != null ? spawnMarker.position : Vector3.zero;

    void Awake()
    {
        if (groundMask == 0) groundMask = LayerMask.GetMask("Ground");
        if (obstacleMask == 0) obstacleMask = LayerMask.GetMask("TownObstacle");
        CollectObstacles();
    }

    void CollectObstacles()
    {
        colliders.Clear();
        foreach (var o in FindObjectsOfType<TownObstacle>())
        {
            var box = o.GetComponent<BoxCollider>();
            if (box == null) continue;
            var t = o.transform;
            Vector3 c = t.TransformPoint(box.center);
            Vector3 s = Vector3.Scale(box.size, t.lossyScale);
            var ec = new EggboyCollider
            {
                x = c.x,
                z = c.z,
                hw = s.x * 0.5f,
                hd = s.z * 0.5f,
                water = o.kind == TownObstacle.Kind.Water,
            };
            if (o.kind == TownObstacle.Kind.WalkableTop)
                ec.top = c.y + s.y * 0.5f;
            colliders.Add(ec);
        }
    }

    public bool Walkable(float x, float z)
    {
        if (walkPolys.Count == 0) return true;
        if (!InWalkArea(x, z)) return false;
        // 工程书 5.4：地面低于水位 0.05 就走进水里了
        return GroundAt(x, z) >= waterLevel - 0.05f;
    }

    public bool InWalkArea(float x, float z)
    {
        foreach (var p in walkPolys)
            if (Contains(p.points, x, z)) return true;
        return false;
    }

    public float GroundAt(float x, float z)
    {
        if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out var hit, 400f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    static bool Contains(Vector2[] poly, float x, float z)
    {
        if (poly == null || poly.Length < 3) return false;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.y > z) != (b.y > z) &&
                x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }
}
