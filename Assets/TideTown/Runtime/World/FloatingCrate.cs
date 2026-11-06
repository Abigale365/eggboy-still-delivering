// WP-08 浮箱（工程书 5.4）：top 碰撞体，top = 水位 + 0.18，每帧更新，带 ±3 cm 起伏。
// 水漫过箱底之前不出现也不挡路，所以 L0/L1 的路线审计和原来一样。
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class FloatingCrate : MonoBehaviour
{
    public float bedY;          // 箱子搁浅处的地面高度
    public float phase;         // 错开起伏相位
    public Vector3 size = new Vector3(0.8f, 0.55f, 0.6f);
    public float topAboveWater = 0.18f;
    public float bobAmplitude = 0.03f;

    IslandWorld world;
    BoxCollider box;
    Renderer rend;
    EggboyCollider entry;
    bool floating;

    void Start()
    {
        world = FindObjectOfType<IslandWorld>();
        box = GetComponent<BoxCollider>();
        rend = GetComponent<Renderer>();
        transform.localScale = size;
        box.size = Vector3.one;

        if (world != null)
        {
            entry = new EggboyCollider
            {
                x = transform.position.x,
                z = transform.position.z,
                hw = size.x * 0.5f,
                hd = size.z * 0.5f,
            };
            world.AddCollider(entry);
        }
        Apply(true);
    }

    void OnDestroy()
    {
        if (world != null && entry != null) world.RemoveCollider(entry);
    }

    void Update() { Apply(false); }

    void Apply(bool force)
    {
        float water = WaterLevel.StaticY;
        bool wet = water > bedY + 0.01f;
        if (force || wet != floating)
        {
            floating = wet;
            if (rend != null) rend.enabled = wet;
            if (box != null) box.enabled = wet;
        }

        if (entry != null)
        {
            // 不漂的时候把碰撞体从世界列表里摘掉，免得挡住 L0/L1 的干路
            entry.top = wet ? water + topAboveWater : (float?)null;
            entry.hw = wet ? size.x * 0.5f : (float?)null;
            entry.hd = wet ? size.z * 0.5f : (float?)null;
        }
        if (!wet) return;

        float top = water + topAboveWater + Mathf.Sin(Time.time * 1.15f + phase) * bobAmplitude;
        var p = transform.position;
        p.y = top - size.y * 0.5f;
        transform.position = p;
        if (entry != null) entry.top = top;
    }
}
