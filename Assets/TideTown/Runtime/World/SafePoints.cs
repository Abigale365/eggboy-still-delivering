// WP-08 安全点：掉进水里或卡住 3 秒，回到最近的安全点（干燥的路线站位）。
using System.Collections.Generic;
using UnityEngine;

public sealed class SafePoints : MonoBehaviour
{
    public List<Vector3> points = new List<Vector3>();
    public float stuckSeconds = 3f;
    public float stuckSpeed = 0.15f;   // 想走（速度大于它）却没挪动，才算卡住
    public float stuckDistance = 0.02f;

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

        float moved = new Vector2(p.x - last.x, p.z - last.z).magnitude;
        float speed = new Vector2(player.vel.x, player.vel.z).magnitude;
        if (!player.locked && speed > stuckSpeed && moved < stuckDistance) stuck += Time.deltaTime;
        else stuck = 0f;
        last = p;

        if (stuck >= stuckSeconds) Rescue("stuck");
    }

    void Rescue(string why)
    {
        var from = player.transform.position;
        Vector3 best = player.SpawnPos;
        float bestD = float.MaxValue;
        bool found = false;
        foreach (var q in points)
        {
            float g = world != null ? world.GroundAt(q.x, q.z) : q.y;
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
        last = best;
        RescueCount++;
        LastReason = why;
        LastFrom = from;
        LastTarget = best;
        Debug.Log($"WATER_SAFE rescue {why} ({from.x:F2},{from.z:F2}) -> ({best.x:F2},{best.z:F2})");
    }
}
