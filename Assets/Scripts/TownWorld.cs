using System.Collections.Generic;
using UnityEngine;

// 读场景的世界实现：地面用射线、阻挡用场景标记、可行走区用边界矩形。
// 角色层与原型世界共用 IEggboyWorld，替换世界不影响移动手感。
public class TownWorld : MonoBehaviour, IEggboyWorld
{
    public LayerMask groundMask;
    public LayerMask obstacleMask;
    public Transform mailboxMarker;
    public Transform spawnMarker;

    [Header("可行走区域（世界坐标，可多个矩形）")]
    public List<Rect> walkRects = new List<Rect>();

    const float Foot = 0.12f;

    readonly List<EggboyCollider> colliders = new List<EggboyCollider>();
    float[] groundCache;
    int cacheW, cacheH;
    Vector3 cacheOrigin;
    float cacheStep = 1f;

    public IReadOnlyList<EggboyCollider> Colliders => colliders;
    public Vector3 MailboxPos => mailboxMarker != null ? mailboxMarker.position : Vector3.zero;
    public Vector3 SpawnPos => spawnMarker != null ? spawnMarker.position : Vector3.zero;

    void Awake()
    {
        if (groundMask == 0) groundMask = LayerMask.GetMask("Ground");
        if (obstacleMask == 0) obstacleMask = LayerMask.GetMask("TownObstacle");
        CollectObstacles();
        EggboyEnv.Apply(transform);
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
        if (walkRects.Count == 0) return true;
        foreach (var r in walkRects)
            if (x >= r.xMin && x <= r.xMax && z >= r.yMin && z <= r.yMax) return true;
        return false;
    }

    // 先用预采样的高度网格，命中后再用射线校正；没有地面数据时退回 0。
    public float GroundAt(float x, float z)
    {
        if (groundCache != null)
        {
            int ix = Mathf.RoundToInt((x - cacheOrigin.x) / cacheStep);
            int iz = Mathf.RoundToInt((z - cacheOrigin.z) / cacheStep);
            if (ix >= 0 && ix < cacheW && iz >= 0 && iz < cacheH)
                return groundCache[iz * cacheW + ix];
        }
        if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out var hit, 400f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    // 编辑器里点一下就能把地面高度烘成网格，运行时不用每帧射线。
    [ContextMenu("烘焙地面高度")]
    public void BakeGround()
    {
        if (walkRects.Count == 0)
        {
            Debug.LogError("没有可行走矩形，无法烘焙地面");
            return;
        }
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var r in walkRects)
        {
            minX = Mathf.Min(minX, r.xMin); minZ = Mathf.Min(minZ, r.yMin);
            maxX = Mathf.Max(maxX, r.xMax); maxZ = Mathf.Max(maxZ, r.yMax);
        }
        cacheOrigin = new Vector3(minX, 0f, minZ);
        cacheW = Mathf.CeilToInt((maxX - minX) / cacheStep) + 1;
        cacheH = Mathf.CeilToInt((maxZ - minZ) / cacheStep) + 1;
        groundCache = new float[cacheW * cacheH];

        int hits = 0;
        for (int iz = 0; iz < cacheH; iz++)
        {
            for (int ix = 0; ix < cacheW; ix++)
            {
                float wx = cacheOrigin.x + ix * cacheStep;
                float wz = cacheOrigin.z + iz * cacheStep;
                float y = 0f;
                if (Physics.Raycast(new Vector3(wx, 200f, wz), Vector3.down, out var hit, 400f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    y = hit.point.y;
                    hits++;
                }
                groundCache[iz * cacheW + ix] = y;
            }
        }
        Debug.Log($"地面烘焙完成 {cacheW}x{cacheH}，命中 {hits} 个采样点");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 0.6f, 0.35f);
        foreach (var r in walkRects)
        {
            var c = new Vector3(r.center.x, 0.2f, r.center.y);
            Gizmos.DrawWireCube(c, new Vector3(r.width, 0.4f, r.height));
        }
    }
}
