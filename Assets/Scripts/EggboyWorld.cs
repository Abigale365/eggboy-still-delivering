// 原型场景：从 game/src/world.ts 移植。物件先用简单几何体占位（之后换成 Lux3D 素材）。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 圆或轴对齐矩形。top：能站上去的台子；water：水坑，走不进去，只能跳过去
public class EggboyCollider
{
    public float x, z;
    public float? r, hw, hd, top;
    public bool water;
}

public class EggboyWorld : MonoBehaviour, IEggboyWorld
{
    public IReadOnlyList<EggboyCollider> Colliders => colliders;
    public Vector3 MailboxPos => mailboxPos;
    public Vector3 SpawnPos => new Vector3(-21f, 0f, 0f);

    const float Foot = 0.12f; // 身体中心离台子边缘多远还算站在台子上
    const float RoadLen = 70f;
    const float ShoreZ = 7f; // 海岸线
    const float PierX0 = 11f, PierX1 = 13.4f, PierZ0 = 5f, PierZ1 = 17f;

    public readonly List<EggboyCollider> colliders = new List<EggboyCollider>();
    public readonly Vector3 mailboxPos = new Vector3(-24f, 0f, -2.3f);
    Material baseMat;
    Light sun;
    Transform seaFoam;
    readonly List<Transform> foams = new List<Transform>();

    void Awake()
    {
        Sky();
        Ground();
        Fence();
        Bushes();
        Mailbox();
        Houses();
        Signs();
        Lamps();
        Crates();
        Pier();
        JumpSpots();
    }

    // 共用默认材质的副本，这样打包时着色器不会被裁掉
    static Shader WorldShader()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null) return urp;
        }
        return Shader.Find("Standard");
    }

    Material Mat(uint rgb, float smoothness = 0.15f)
    {
        var m = new Material(WorldShader());
        var c = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }

    Material Emissive(uint rgb, float intensity = 0.7f)
    {
        var m = Mat(rgb, 0.6f);
        m.EnableKeyword("_EMISSION");
        var c = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        m.SetColor("_EmissionColor", new Color(c.r / 255f, c.g / 255f, c.b / 255f) * intensity);
        return m;
    }

    void Fence()
    {
        var wood = Mat(0x8a6a45, 0.1f);
        for (float x = -34f; x <= 34f; x += 2.2f)
        {
            Prim(PrimitiveType.Cube, wood, new Vector3(x, 0.45f, 6.55f), new Vector3(0.12f, 0.9f, 0.12f));
            Prim(PrimitiveType.Cube, wood, new Vector3(x + 1.1f, 0.52f, 6.55f), new Vector3(2.2f, 0.08f, 0.08f));
            Prim(PrimitiveType.Cube, wood, new Vector3(x + 1.1f, 0.8f, 6.55f), new Vector3(2.2f, 0.08f, 0.08f));
        }
    }

    void Bushes()
    {
        uint[] greens = { 0x6f8f5a, 0x7fa05e, 0x8fae6a, 0x5f7f50 };
        float[,] list =
        {
            { -19f, -4.6f, 0.7f }, { -15f, -8.6f, 0.9f }, { -8f, -4.7f, 0.6f },
            { 2f, -4.6f, 0.8f }, { 9f, -8.5f, 0.9f }, { 21f, -4.5f, 0.7f },
            { -28f, 4.6f, 0.6f }, { 24f, 5f, 0.8f },
        };
        for (int i = 0; i < list.GetLength(0); i++)
        {
            float x = list[i, 0], z = list[i, 1], s = list[i, 2];
            Prim(PrimitiveType.Sphere, Mat(greens[i % greens.Length], 0.05f), new Vector3(x, s * 0.5f, z), new Vector3(s * 1.6f, s, s * 1.4f));
        }
    }

    void Signs()
    {
        var post = Mat(0x5a4a3a, 0.1f);
        var board = Mat(0xf1e3c8, 0.25f);
        var trim = Mat(0xb4563a, 0.3f);
        // 邮箱旁边的小告示牌
        Prim(PrimitiveType.Cube, post, new Vector3(-22f, 0.8f, -3.0f), new Vector3(0.1f, 1.6f, 0.1f));
        Prim(PrimitiveType.Cube, board, new Vector3(-22f, 1.55f, -3.0f), new Vector3(0.9f, 0.5f, 0.06f));
        Prim(PrimitiveType.Cube, trim, new Vector3(-22f, 1.32f, -3.05f), new Vector3(0.9f, 0.06f, 0.04f));
        // 路牌
        Prim(PrimitiveType.Cube, post, new Vector3(10f, 0.9f, -2.8f), new Vector3(0.1f, 1.8f, 0.1f));
        Prim(PrimitiveType.Cube, board, new Vector3(10f, 1.65f, -2.8f), new Vector3(1.1f, 0.34f, 0.06f));
        Prim(PrimitiveType.Cube, trim, new Vector3(10.28f, 1.65f, -2.86f), new Vector3(0.36f, 0.05f, 0.04f));
        // 房前遮阳棚
        Prim(PrimitiveType.Cube, trim, new Vector3(18f, 1.75f, -4.75f), new Vector3(1.4f, 0.08f, 0.7f)).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
    }

    GameObject Prim(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, Transform parent = null, bool keepCollider = false)
    {
        var o = GameObject.CreatePrimitive(type);
        if (baseMat == null) baseMat = o.GetComponent<Renderer>().sharedMaterial;
        if (!keepCollider) Destroy(o.GetComponent<Collider>());
        if (mat != null) o.GetComponent<Renderer>().sharedMaterial = mat;
        o.transform.SetParent(parent == null ? transform : parent, false);
        o.transform.localPosition = pos;
        o.transform.localScale = scale;
        return o;
    }

    void Sky()
    {
        Prim(PrimitiveType.Cube, null, Vector3.down * 100f, Vector3.one); // 只为先拿到默认材质
        sun = EggboyEnv.Apply(transform);
    }

    void Ground()
    {
        Prim(PrimitiveType.Cube, Mat(0x7fa05e), new Vector3(0f, -0.05f, -57f), new Vector3(220f, 0.1f, 120f));
        Prim(PrimitiveType.Cube, Mat(0xd9c49d), new Vector3(0f, -0.06f, ShoreZ - 3f), new Vector3(220f, 0.1f, 14f));
        Prim(PrimitiveType.Cube, Mat(0xc3ad8b, 0.05f), new Vector3(0f, -0.04f, 0f), new Vector3(RoadLen, 0.1f, 3f));
        foreach (var z in new[] { -1.55f, 1.55f }) Prim(PrimitiveType.Cube, Mat(0x9c8668), new Vector3(0f, 0.04f, z), new Vector3(RoadLen, 0.08f, 0.12f));
        Prim(PrimitiveType.Cube, Mat(0x4f94b5, 0.75f), new Vector3(0f, -0.1f, ShoreZ + 60f), new Vector3(240f, 0.1f, 120f));
        seaFoam = Prim(PrimitiveType.Cube, Mat(0xf4f8f8, 0.3f), new Vector3(0f, -0.03f, ShoreZ + 0.3f), new Vector3(240f, 0.02f, 0.6f)).transform;
        for (int i = 0; i < 3; i++)
            foams.Add(Prim(PrimitiveType.Cube, Mat(0xe8f4f4, 0.2f), new Vector3(0f, -0.035f, ShoreZ + 0.9f + i * 0.9f), new Vector3(240f, 0.015f, 0.22f + i * 0.12f)).transform);
        foreach (var h in new[] { new Vector3(-50f, 30f, 10f), new Vector3(-15f, 26f, 13f), new Vector3(22f, 34f, 9f), new Vector3(55f, 28f, 12f) })
            Prim(PrimitiveType.Sphere, Mat(0x8fae86), new Vector3(h.x, -1f, -48f), new Vector3(h.y * 2f, h.z * 2f, 28f));
    }

    void Mailbox()
    {
        var root = new GameObject("Mailbox").transform;
        root.SetParent(transform, false);
        root.localPosition = mailboxPos;
        var red = Mat(0xc8423a, 0.4f);
        Prim(PrimitiveType.Cylinder, red, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), root);
        Prim(PrimitiveType.Sphere, red, new Vector3(0f, 1f, 0f), Vector3.one * 0.48f, root);
        Prim(PrimitiveType.Cube, Mat(0x2b2320), new Vector3(0f, 0.82f, 0.24f), new Vector3(0.22f, 0.04f, 0.05f), root);
        colliders.Add(new EggboyCollider { x = mailboxPos.x, z = mailboxPos.z, r = 0.4f });
    }

    void Houses()
    {
        uint[] walls = { 0xf1e3c8, 0xe9cfae, 0xdfe6d8, 0xf0d6c4 }, roofs = { 0xb4563a, 0x8a5a3c, 0x6f7f8f, 0xa8653f };
        float[] xs = { -17f, -6f, 5f, 18f };
        const float w = 4.2f, d = 3.2f, h = 2.8f;
        for (int i = 0; i < xs.Length; i++)
        {
            var house = new GameObject("House" + i).transform;
            house.SetParent(transform, false);
            house.localPosition = new Vector3(xs[i], 0f, -6.5f);
            Prim(PrimitiveType.Cube, Mat(walls[i]), new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), house, true); // 留碰撞体给镜头防穿墙
            var roof = Prim(PrimitiveType.Cube, Mat(roofs[i]), new Vector3(0f, h + 0.3f, 0f), new Vector3(w + 0.4f, d * 0.72f, d * 0.72f), house);
            roof.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            Prim(PrimitiveType.Cube, Mat(0x5d7d5a), new Vector3(0f, 0.8f, d / 2f + 0.02f), new Vector3(0.9f, 1.6f, 0.06f), house);
            foreach (var wx in new[] { -1.3f, 1.3f }) Prim(PrimitiveType.Cube, Emissive(0xffd28a, 0.55f), new Vector3(wx, 1.7f, d / 2f + 0.02f), new Vector3(0.8f, 0.7f, 0.05f), house);
            colliders.Add(new EggboyCollider { x = xs[i], z = -6.5f, hw = w / 2f + 0.25f, hd = d / 2f + 0.25f });
        }
    }

    void Lamps()
    {
        var pole = Mat(0x3a3532);
        var bulb = Emissive(0xfff1c9, 2f);
        for (int x = -30; x <= 30; x += 10)
        {
            Prim(PrimitiveType.Cylinder, pole, new Vector3(x, 1.3f, -2.1f), new Vector3(0.12f, 1.3f, 0.12f));
            Prim(PrimitiveType.Sphere, bulb, new Vector3(x, 2.7f, -2.1f), Vector3.one * 0.32f);
            colliders.Add(new EggboyCollider { x = x, z = -2.1f, r = 0.2f });
        }
    }

    void Crates()
    {
        var mat = Mat(0xa9794b);
        float[,] list = { { -3f, 2.3f, 0.55f, 0.3f }, { -2.4f, 2.6f, 0.45f, -0.2f }, { 9f, 3.2f, 0.6f, 0.1f }, { 14.5f, 4.2f, 0.5f, 0.6f } };
        for (int i = 0; i < list.GetLength(0); i++)
        {
            float x = list[i, 0], z = list[i, 1], s = list[i, 2];
            var c = Prim(PrimitiveType.Cube, mat, new Vector3(x, s / 2f, z), Vector3.one * s);
            c.transform.localRotation = Quaternion.Euler(0f, list[i, 3] * Mathf.Rad2Deg, 0f);
            colliders.Add(new EggboyCollider { x = x, z = z, r = s * 0.7f });
        }
    }

    void Pier()
    {
        var plank = Mat(0xb08a5e);
        for (float z = PierZ0; z < PierZ1; z += 0.5f)
            Prim(PrimitiveType.Cube, plank, new Vector3((PierX0 + PierX1) / 2f, 0.08f, z + 0.25f), new Vector3(PierX1 - PierX0, 0.1f, 0.42f));
    }

    void JumpSpots()
    {
        var wood = Mat(0xb08a5e);
        float[,] steps = { { -13.2f, 3.3f, 0.18f }, { -12.1f, 3.3f, 0.34f } };
        for (int i = 0; i < 2; i++)
        {
            float x = steps[i, 0], z = steps[i, 1], top = steps[i, 2];
            Prim(PrimitiveType.Cube, wood, new Vector3(x, top / 2f, z), new Vector3(1.1f, top, 1.1f));
            colliders.Add(new EggboyCollider { x = x, z = z, hw = 0.55f, hd = 0.55f, top = top });
        }
        Prim(PrimitiveType.Cylinder, Mat(0x6fa9c6, 0.85f), new Vector3(-9.6f, 0.012f, 3.4f), new Vector3(0.64f, 0.01f, 0.64f));
        colliders.Add(new EggboyCollider { x = -9.6f, z = 3.4f, r = 0.32f, water = true });
    }

    // 走得到的地方：岸上 + 码头
    public bool Walkable(float x, float z)
    {
        return (Mathf.Abs(x) < 34f && z > -11f && z < ShoreZ - 0.2f)
            || (x > PierX0 + 0.2f && x < PierX1 - 0.2f && z > PierZ0 - 0.5f && z < PierZ1 - 0.3f);
    }

    // 脚下地面高度：码头木板面 13 cm，台子按台面
    public float GroundAt(float x, float z)
    {
        float y = x > PierX0 && x < PierX1 && z > PierZ0 && z < PierZ1 ? 0.13f : 0f;
        foreach (var c in colliders)
        {
            if (c.top == null) continue;
            bool on = c.r != null
                ? Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.z) * (z - c.z)) < c.r.Value + Foot
                : Mathf.Abs(x - c.x) < (c.hw ?? 0f) + Foot && Mathf.Abs(z - c.z) < (c.hd ?? 0f) + Foot;
            if (on) y = Mathf.Max(y, c.top.Value);
        }
        return y;
    }

    void Update()
    {
        var p = seaFoam.localPosition;
        p.z = ShoreZ + 0.3f + Mathf.Sin(Time.time * 1.3f) * 0.25f;
        seaFoam.localPosition = p;
        for (int i = 0; i < foams.Count; i++)
        {
            var q = foams[i].localPosition;
            q.z = ShoreZ + 0.9f + i * 0.9f + Mathf.Sin(Time.time * (0.8f + i * 0.25f) + i * 1.7f) * 0.35f;
            foams[i].localPosition = q;
        }
    }
}
