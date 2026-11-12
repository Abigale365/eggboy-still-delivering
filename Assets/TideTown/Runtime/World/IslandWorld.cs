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
    public GameObject mailboxTrail;
    public GameObject endingDesk;
    public GameObject woodModel, oilModel;
    public float waterLevel;

    public List<WalkPoly> walkPolys = new List<WalkPoly>();
    public List<WalkPoly> lawnPolys = new List<WalkPoly>();
    public WalkPoly beachPoly;

    readonly List<EggboyCollider> colliders = new List<EggboyCollider>();
    const float SpatialCell = 2.0f;
    readonly Dictionary<long, List<EggboyCollider>> spatialBuckets = new Dictionary<long, List<EggboyCollider>>();
    bool spatialDirty = true;
    EggboyCollider endingDeskCollider;

    public IReadOnlyList<EggboyCollider> Colliders => colliders;

    // WP-08：水位由 WaterLevel 管，IslandWorld 只读它
    public float WaterY => WaterLevel.Main != null ? WaterLevel.Main.Y : waterLevel;
    public void AddCollider(EggboyCollider c)
    {
        if (c != null && !colliders.Contains(c))
        {
            colliders.Add(c);
            spatialDirty = true;
        }
    }
    public void RemoveCollider(EggboyCollider c)
    {
        if (c != null && colliders.Remove(c))
            spatialDirty = true;
    }

    public void SyncEndingDeskCollider()
    {
        if (endingDesk == null) return;
        if (endingDesk.activeInHierarchy)
        {
            if (endingDeskCollider == null)
            {
                var p = endingDesk.transform.position;
                endingDeskCollider = new EggboyCollider
                {
                    x = p.x,
                    z = p.z,
                    r = 0.20f,
                    top = p.y + 0.76f
                };
            }
            AddCollider(endingDeskCollider);
        }
        else if (endingDeskCollider != null)
        {
            RemoveCollider(endingDeskCollider);
        }
    }

    void RebuildSpatialGrid()
    {
        spatialDirty = false;
        spatialBuckets.Clear();
        const float Margin = 0.60f; // 大于 EggboyPlayer.Radius (0.42m)，保证单格查询即可覆盖全部可能接触的碰撞体
        for (int i = 0; i < colliders.Count; i++)
        {
            var c = colliders[i];
            float ex = (c.hw ?? c.r ?? 0.5f) + Margin;
            float ez = (c.hd ?? c.r ?? 0.5f) + Margin;
            int x0 = Mathf.FloorToInt((c.x - ex) / SpatialCell);
            int x1 = Mathf.FloorToInt((c.x + ex) / SpatialCell);
            int z0 = Mathf.FloorToInt((c.z - ez) / SpatialCell);
            int z1 = Mathf.FloorToInt((c.z + ez) / SpatialCell);
            for (int gz = z0; gz <= z1; gz++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    long key = ((long)gx << 32) | (uint)gz;
                    if (!spatialBuckets.TryGetValue(key, out var list))
                    {
                        list = new List<EggboyCollider>(8);
                        spatialBuckets[key] = list;
                    }
                    list.Add(c);
                }
            }
        }
    }

    public void QueryColliders(float x, float z, List<EggboyCollider> results)
    {
        results.Clear();
        if (spatialDirty) RebuildSpatialGrid();
        int gx = Mathf.FloorToInt(x / SpatialCell);
        int gz = Mathf.FloorToInt(z / SpatialCell);
        long key = ((long)gx << 32) | (uint)gz;
        if (spatialBuckets.TryGetValue(key, out var list))
            results.AddRange(list);
    }
    public Vector3 MailboxPos => mailboxMarker != null ? mailboxMarker.position : Vector3.zero;
    public Vector3 SpawnPos => spawnMarker != null ? spawnMarker.position : Vector3.zero;

    void Awake()
    {
        if (groundMask == 0) groundMask = LayerMask.GetMask("Ground");
        if (obstacleMask == 0) obstacleMask = LayerMask.GetMask("TownObstacle");
        CollectObstacles();
        CleanVisualRoadAndTierSeams();
    }

    // 仅调整视觉 MeshFilter（绝不改动 MeshCollider 物理碰撞，保证寻路与审计 100% 一致）：
    // 1. 移除坡道 Road_* 外扩 93cm 的凸起碎石边/路缘/悬空裙边（避免切穿石板路和栅栏造成穿模）；
    // 2. 将 Road_* 与 3.0m 主街交界处的顶面微压 2.5cm，避免与石板路 Z-fighting 闪烁；
    // 3. 消除 Tier_pad_grass_3_0_0（操场草坪 y=3.02）与 Tier_pad_stone_3_0_0（石板路 y=3.02）同高重叠导致的草坪三角穿模。
    void CleanVisualRoadAndTierSeams()
    {
        Material cobbleMat = null;
        var stoneTier = GameObject.Find("Tier_pad_stone_3_0_0");
        if (stoneTier != null)
        {
            var stoneMr = stoneTier.GetComponent<MeshRenderer>();
            if (stoneMr != null) cobbleMat = stoneMr.sharedMaterial;
        }

        var grassTier = GameObject.Find("Tier_pad_grass_3_0_0");
        if (grassTier != null)
        {
            var gmf = grassTier.GetComponent<MeshFilter>();
            if (gmf != null && gmf.sharedMesh != null)
            {
                var m = Instantiate(gmf.sharedMesh);
                var v = m.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    // 操场草坪整体微降 0.8cm（仍高于 Tier_main 3.00m），但在秋千/滑梯操场内部区域略抬高以盖住石板多边形凹角
                    if (v[i].x > 8.2f && v[i].x < 23.2f && v[i].z > -7.1f && v[i].z < 8.5f)
                        v[i].y = 3.028f;
                    else
                        v[i].y = 3.012f;
                }
                m.vertices = v;
                m.RecalculateBounds();
                gmf.sharedMesh = m;
            }
        }

        if (stoneTier != null)
        {
            var smf = stoneTier.GetComponent<MeshFilter>();
            if (smf != null && smf.sharedMesh != null)
            {
                var m = Instantiate(smf.sharedMesh);
                var v = m.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    // 石板路在操场护栏内侧的折角顶点压到草坪下方，消除草坪里的斜三角石板穿模
                    if (v[i].x > 8.5f && v[i].x < 23.8f && v[i].z > -7.9f && v[i].z < -4.5f)
                        v[i].y = 3.005f;
                    else
                        v[i].y = 3.024f;
                }
                m.vertices = v;
                m.RecalculateBounds();
                smf.sharedMesh = m;
            }
        }

        foreach (var mf in FindObjectsOfType<MeshFilter>())
        {
            if (mf == null || mf.sharedMesh == null) continue;
            string goName = mf.gameObject.name;
            if (!goName.StartsWith("Road_")) continue;

            var mesh = Instantiate(mf.sharedMesh);
            if (mesh.subMeshCount >= 4)
            {
                // 只保留主路面（subMesh 0），去掉两侧外扩 93cm 穿入石板路/栅栏/悬空的路缘、碎石边和裙边
                mesh.SetTriangles(new int[0], 1);
                mesh.SetTriangles(new int[0], 2);
                mesh.SetTriangles(new int[0], 3);
            }

            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                // 坡道与各层台地（3.0m 主街、5.0m 灯塔台地、5.5m 住宅石板台地、6.5m 山顶邮局台地）交界处微压，彻底消除路面接缝 Z-fighting
                if (verts[i].y >= 2.95f && verts[i].y <= 3.05f)
                    verts[i].y = 3.010f;
                else if (verts[i].y >= 4.96f && verts[i].y <= 5.05f)
                    verts[i].y = 4.992f;
                else if (verts[i].y >= 5.46f && verts[i].y <= 5.55f)
                    verts[i].y = 5.510f;
                else if (verts[i].y >= 6.46f && verts[i].y <= 6.55f)
                    verts[i].y = 6.492f;
            }
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            if (cobbleMat != null && goName != "Road_to_dock")
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterials != null && mr.sharedMaterials.Length > 0)
                {
                    var mats = mr.sharedMaterials;
                    mats[0] = cobbleMat;
                    mr.sharedMaterials = mats;
                }
            }
        }
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
                minY = o.minY,
            };
            if (o.kind == TownObstacle.Kind.WalkableTop)
                ec.top = c.y + s.y * 0.5f;
            colliders.Add(ec);
        }
        // 按模型外轮廓的小圆（TownObstacleSet）
        foreach (var set in FindObjectsOfType<TownObstacleSet>())
            for (int i = 0; i < set.x.Length; i++)
                colliders.Add(new EggboyCollider { x = set.x[i], z = set.z[i], r = set.r[i], top = set.top[i] });
        spatialDirty = true;
    }

    public bool Walkable(float x, float z) => walkPolys.Count == 0 || TryWalkableGround(x, z, out _);

    bool TryWalkableGround(float x, float z, out float ground)
    {
        ground = 0f;
        bool road = InWalkArea(x, z);
        bool lawn = !road && InLawnArea(x, z);
        bool sand = beachPoly != null && Contains(beachPoly.points, x, z);
        if (!road && !lawn && !sand) return false;
        ground = GroundAt(x, z);
        if (ground < WaterY - 0.05f) return false;
        return sand && Mathf.Abs(ground - beachPoly.y) <= 0.12f || (road ? ground > 0.22f : lawn && ground >= 2.5f);
    }

    public bool IsBeachSurface(float x, float z, float height) =>
        beachPoly != null && Mathf.Abs(height - beachPoly.y) <= 0.12f && Contains(beachPoly.points, x, z);

    public bool CanTraverse(Vector3 from, float x, float z, bool jumping)
    {
        if (walkPolys.Count == 0) return true;
        if (!TryWalkableGround(x, z, out float ground)) return jumping && beachPoly != null && from.y >= beachPoly.y + 0.55f && IsDryBeachApproach(x, z);
        if (beachPoly == null) return true;
        // 跳下沙滩才进入低层。沙滩上的人不能从崖壁底部穿进上层地形。
        bool onBeach = from.y < beachPoly.y + 0.65f && Contains(beachPoly.points, from.x, from.z);
        if (IsBeachSurface(x, z, ground)) return onBeach || jumping;
        return !onBeach || ground <= from.y + 0.30f;
    }

    bool IsDryBeachApproach(float x, float z)
    {
        if (beachPoly?.points == null || beachPoly.points.Length < 3 || WaterY > 0.05f) return false;
        // 北侧坡道与沙滩之间有一条低矮的干岸带，允许角色在空中越过这段间隙。
        const float MaxGap = 0.90f;
        var point = new Vector2(x, z);
        var vertices = beachPoly.points;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            var edge = vertices[i] - vertices[j];
            float t = Mathf.Clamp01(Vector2.Dot(point - vertices[j], edge) / Mathf.Max(edge.sqrMagnitude, 0.0001f));
            if ((point - vertices[j] - t * edge).sqrMagnitude >= MaxGap * MaxGap) continue;
            float ground = GroundAt(x, z);
            return ground > 0.05f && ground <= beachPoly.y + 0.10f;
        }
        return false;
    }

    public bool InWalkArea(float x, float z)
    {
        foreach (var p in walkPolys)
            if (Contains(p.points, x, z)) return true;
        return false;
    }

    public bool InLawnArea(float x, float z)
    {
        if (lawnPolys == null) return false;
        foreach (var p in lawnPolys)
            if (Contains(p.points, x, z)) return true;
        return false;
    }

    public float GroundAt(float x, float z)
    {
        return GroundAt(x, z, float.PositiveInfinity);
    }

    // aboveY：只认这个高度附近及以下的地面。塔顶平台在塔门正上方，
    // 不带上限的话从塔下打射线会直接打到 12 米高的平台，把人抬上去。
    public float GroundAt(float x, float z, float aboveY)
    {
        float best = -1f;
        var hits = Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f, groundMask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            float y = hit.point.y;
            if (y <= aboveY + 0.3f && y > best) best = y;
        }
        return best < -0.5f ? 0f : best;
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
