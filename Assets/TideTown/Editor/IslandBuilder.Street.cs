// WP-17/P1：街道街具。路灯、路牌、长椅、栅栏、花盆全部用现成 GLB，
// 沿路侧摆放，不占行走面（不放碰撞），位置由 route/road 数据算出来。
using UnityEditor;
using UnityEngine;

public static partial class IslandBuilder
{
    static void BuildStreetProps(Transform parent, Layout layout)
    {
        var root = new GameObject("StreetProps");
        root.transform.SetParent(parent, false);
        int placed = 0;
        // 主街 upper：路牌 → 路灯 → 长椅/花盆交替，左右分散
        placed += PlaceOnRoad(root.transform, layout, "upper", 0.10f, 2.00f, "S5", 2.0f, true, true) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "upper", 0.24f, -2.05f, "S4", 2.6f, true, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "upper", 0.38f, 2.30f, "S6", 1.4f, false, true) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "upper", 0.52f, 2.00f, "S4", 2.6f, true, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "upper", 0.64f, -2.20f, "E1", 0.3f, true, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "upper", 0.78f, -2.05f, "S4", 2.6f, true, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "upper", 0.92f, 2.30f, "S6", 1.4f, false, true) ? 1 : 0;
        // 船棚小路：两段栅栏 + 一盏灯
        placed += PlaceOnRoad(root.transform, layout, "to_shed", 0.28f, 2.25f, "S7", 2.0f, false, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "to_shed", 0.62f, 2.25f, "S7", 2.0f, false, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "to_shed", 0.50f, -2.05f, "S4", 2.6f, true, false) ? 1 : 0;
        // 码头、灯塔、学校后门、山路
        placed += PlaceOnRoad(root.transform, layout, "to_dock", 0.55f, 2.20f, "S7", 2.0f, false, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "to_lighthouse", 0.62f, 2.05f, "S4", 2.6f, true, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "to_school_back", 0.55f, 2.00f, "E1", 0.3f, true, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "mountain", 0.42f, -2.10f, "S4", 2.6f, true, false) ? 1 : 0;
        placed += PlaceOnRoad(root.transform, layout, "mountain", 0.80f, 2.35f, "S7", 2.0f, false, false) ? 1 : 0;
        // 手工补几件：邮局门前长椅、面包店和学校的花盆、灯塔脚下的栅栏
        placed += StreetProp(root.transform, "S6", -18.9f, -6.6f, 90f, 1.4f, false) ? 1 : 0;
        placed += StreetProp(root.transform, "E1", -8.6f, -5.2f, 0f, 0.3f, true) ? 1 : 0;
        placed += StreetProp(root.transform, "E1", 13.2f, 4.3f, 0f, 0.3f, true) ? 1 : 0;
        placed += StreetProp(root.transform, "S7", 22.6f, 3.8f, 10f, 2.0f, false) ? 1 : 0;
        Debug.Log($"STREET props={placed}");
    }

    // 在一条路上取 t（弧长比例）处、侧向 lateral 米的位置；yaw 沿路或面向路
    static bool PlaceOnRoad(Transform parent, Layout layout, string roadId, float t, float lateral,
        string id, float size, bool fitHeight, bool faceRoad)
    {
        RoadDef road = null;
        if (layout.roads != null)
            foreach (var r in layout.roads)
                if (r.id == roadId) { road = r; break; }
        if (road == null || road.x == null || road.x.Length < 2) return false;

        float total = 0f;
        for (int i = 0; i + 1 < road.x.Length; i++)
            total += Mathf.Sqrt((road.x[i + 1] - road.x[i]) * (road.x[i + 1] - road.x[i]) +
                                (road.z[i + 1] - road.z[i]) * (road.z[i + 1] - road.z[i]));
        if (total < 0.01f) return false;
        float want = Mathf.Clamp01(t) * total;

        for (int i = 0; i + 1 < road.x.Length; i++)
        {
            float ax = road.x[i], az = road.z[i], bx = road.x[i + 1], bz = road.z[i + 1];
            float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            if (len < 1e-4f) continue;
            if (want > len) { want -= len; continue; }
            float k = want / len;
            var dir = new Vector3(bx - ax, 0f, bz - az) / len;
            var side = new Vector3(-dir.z, 0f, dir.x);
            float x = ax + (bx - ax) * k + side.x * lateral;
            float z = az + (bz - az) * k + side.z * lateral;
            float along = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float yaw = faceRoad ? (lateral > 0f ? along - 90f : along + 90f) : along;
            return StreetProp(parent, id, x, z, yaw, size, fitHeight);
        }
        return false;
    }

    static bool StreetProp(Transform parent, string id, float x, float z, float yaw, float size, bool fitHeight)
    {
        float y = GroundY(x, z);
        if (y <= 0.2f)
        {
            Debug.Log($"STREET skip {id} ({x:F2},{z:F2}) no ground");
            return false;
        }
        if (Physics.CheckSphere(new Vector3(x, y + 0.6f, z), 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"STREET skip {id} ({x:F2},{z:F2}) blocked");
            return false;
        }
        var holder = new GameObject("Street_" + id);
        holder.transform.SetParent(parent, false);
        holder.transform.position = new Vector3(x, y + 0.02f, z);
        MountSourceModel(holder.transform, id, Vector3.zero, size, fitHeight, yaw);
        return true;
    }
}
