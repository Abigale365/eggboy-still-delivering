// WP-08 安全点：掉进水里或卡住 3 秒，回到最近的安全点（干燥的路线站位）。
// 2026-11-07：加掉层救援，从路边掉到路面下方 2 秒后送回头顶那段路。
using System.Collections.Generic;
using UnityEngine;

public sealed class SafePoints : MonoBehaviour
{
    public List<Vector3> points = new List<Vector3>();
    public float stuckSeconds = 3f;
    public float stuckSpeed = 0.15f;   // 想走（速度大于它）却没挪动，才算卡住
    // 3 秒内离起点不到 25 cm 才算卡住。换了字段名：场景里存着旧的 stuckDistance=0.02（按每帧算的），不能沿用
    public float stuckRadius = 0.25f;

    // 给 -wateraudit 看的记录
    public bool Enabled = true;
    public int RescueCount { get; private set; }
    public string LastReason { get; private set; }
    public Vector3 LastFrom { get; private set; }
    public Vector3 LastTarget { get; private set; }

    EggboyPlayer player;
    IslandWorld world;
    float stuck;
    Vector3 last;

    void Start()
    {
        player = FindObjectOfType<EggboyPlayer>();
        world = FindObjectOfType<IslandWorld>();
        if (player != null) last = player.transform.position;
    }

    void Update()
    {
        if (!Enabled) return;
        if (player == null) player = FindObjectOfType<EggboyPlayer>();
        if (player == null) return;
        if (world == null) world = FindObjectOfType<IslandWorld>();

        var p = player.transform.position;
        if (p.y < WaterLevel.StaticY - 0.05f) { Rescue("water"); return; }

        // 卡住 = 一直想走，但离开始卡住的那个点不到 stuckRadius。
        // 以前按“每帧挪动 < 2 cm”判断，帧率一高正常走路每帧也不到 2 cm（60 帧走路约 1.1 cm），
        // 结果走着走着每 3 秒被传送一次。改成按累计位移判断，和帧率无关。（2026-11-07）
        float speed = new Vector2(player.vel.x, player.vel.z).magnitude;
        bool trying = !player.locked && speed > stuckSpeed;
        if (!trying || stuck <= 0f) { last = p; stuck = trying ? Time.deltaTime : 0f; }
        else if (new Vector2(p.x - last.x, p.z - last.z).magnitude > stuckRadius) { last = p; stuck = Time.deltaTime; }
        else stuck += Time.deltaTime;

        if (stuck >= stuckSeconds) { Rescue("stuck"); return; }

        // 掉层：从路边掉到路面下方，人还能走动但回不去（known-issues #14）
        Vector3 back = p;
        if (!player.locked && player.grounded && FellBelowRoute(p, out back)) fell += Time.deltaTime;
        else fell = 0f;
        if (fell >= fellSeconds) RescueTo(back, "fell");
    }

    // ---------- 掉层判定 ----------
    // 同时满足才算掉层：
    //   1. 2.5 米内有一段路线，比脚高 0.5–9 米（刚从那儿掉下来）
    //   2. 2.5 米内没有和脚差不多高的路线（不是正常走在别的路上）
    //   3. 脚下没有设计高度和脚差不多的可走区（不是落到另一层正常地面上）
    public float fellSeconds = 2f;
    public float fellRadius = 2.5f;
    public float fellDrop = 0.5f;
    float fell;

    [System.Serializable] class RoutePt { public string id; public bool stop; public float x, z; }
    [System.Serializable] class RouteDoc { public RoutePt[] route; }
    Vector2[] routePts;

    void LoadRoute()
    {
        routePts = new Vector2[0];
        var asset = Resources.Load<TextAsset>("World/route");
        if (asset == null) return;
        var doc = JsonUtility.FromJson<RouteDoc>(asset.text);
        if (doc == null || doc.route == null) return;
        routePts = new Vector2[doc.route.Length];
        for (int i = 0; i < doc.route.Length; i++) routePts[i] = new Vector2(doc.route[i].x, doc.route[i].z);
    }

    public bool FellBelowRoute(Vector3 p, out Vector3 back)
    {
        back = p;
        if (world == null) return false;
        if (routePts == null) LoadRoute();
        if (routePts.Length < 2) return false;

        // 3：落在别的可走层上就不管
        foreach (var poly in world.walkPolys)
            if (Mathf.Abs(poly.y - p.y) < 0.6f && InPoly(poly.points, p.x, p.z)) return false;

        var me = new Vector2(p.x, p.z);
        bool above = false;
        float bestD = float.MaxValue;
        for (int i = 0; i + 1 < routePts.Length; i++)
        {
            var q = ClosestOnSegment(routePts[i], routePts[i + 1], me);
            float d = (q - me).magnitude;
            if (d > fellRadius) continue;
            // 只认脚上方 9 米以内的地面（山路最高 8.5 米，从海平面掉下去也能找回），
            // 塔顶平台比塔脚高 13 米，不算
            float g = world.GroundAt(q.x, q.y, p.y + 9f);
            float h = g - p.y;
            if (h < fellDrop) return false;          // 2：附近有同高的路，正常
            above = true;
            if (d < bestD && world.Walkable(q.x, q.y)) { bestD = d; back = new Vector3(q.x, g + 0.02f, q.y); }
        }
        return above && bestD < float.MaxValue;
    }

    static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        var ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-6f) return a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return a + ab * t;
    }

    static bool InPoly(Vector2[] poly, float x, float z)
    {
        if (poly == null || poly.Length < 3) return false;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    void RescueTo(Vector3 target, string why)
    {
        var from = player.transform.position;
        player.transform.position = target;
        player.vel = Vector3.zero;
        player.grounded = true;
        player.jump.phase = JumpPhase.none;
        stuck = 0f;
        fell = 0f;
        last = target;
        RescueCount++;
        LastReason = why;
        LastFrom = from;
        LastTarget = target;
        Debug.Log($"WATER_SAFE rescue {why} ({from.x:F2},{from.y:F2},{from.z:F2}) -> ({target.x:F2},{target.y:F2},{target.z:F2})");
    }

    void Rescue(string why)
    {
        var from = player.transform.position;
        Vector3 best = player.SpawnPos;
        float bestD = float.MaxValue;
        bool found = false;
        foreach (var q in points)
        {
            float g = world != null ? world.GroundAt(q.x, q.z, q.y) : q.y;
            if (g < WaterLevel.StaticY - 0.05f) continue;   // 涨水后不往水里送
            float d = (new Vector2(q.x - from.x, q.z - from.z)).sqrMagnitude;
            if (d < bestD) { bestD = d; best = new Vector3(q.x, g + 0.02f, q.z); found = true; }
        }
        if (!found) best = new Vector3(player.SpawnPos.x, player.SpawnPos.y + 0.02f, player.SpawnPos.z);

        player.transform.position = best;
        player.vel = Vector3.zero;
        player.grounded = true;
        player.jump.phase = JumpPhase.none;
        stuck = 0f;
        fell = 0f;
        last = best;
        RescueCount++;
        LastReason = why;
        LastFrom = from;
        LastTarget = best;
        Debug.Log($"WATER_SAFE rescue {why} ({from.x:F2},{from.z:F2}) -> ({best.x:F2},{best.z:F2})");
    }
}
