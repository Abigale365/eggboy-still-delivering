// WP-14：只在某一高度以上生效的挡边（灯塔塔顶栏杆），不影响地面的路线和审计。
using UnityEngine;

public sealed class RaisedObstacle : MonoBehaviour
{
    public float minY = 16.6f;

    IslandWorld world;
    EggboyCollider entry;

    void Start()
    {
        world = FindObjectOfType<IslandWorld>();
        var box = GetComponent<BoxCollider>();
        if (world == null || box == null) return;
        var t = transform;
        Vector3 c = t.TransformPoint(box.center);
        Vector3 s = Vector3.Scale(box.size, t.lossyScale);
        entry = new EggboyCollider
        {
            x = c.x,
            z = c.z,
            hw = s.x * 0.5f,
            hd = s.z * 0.5f,
            minY = minY,
        };
        world.AddCollider(entry);
    }

    void OnDestroy()
    {
        if (world != null && entry != null) world.RemoveCollider(entry);
    }
}
