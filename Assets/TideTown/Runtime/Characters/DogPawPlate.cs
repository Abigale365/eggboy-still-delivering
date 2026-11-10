// 创新方向 C：小黑踩踏板（工程书 INNOVATION_BOOK_20261110 的 WP-C）。
// 船棚旁的浅滩边有一块橙色爪印踏板。人踩上去没反应（太重了，踏板只认小爪子）；
// 按 F 让小黑跑过去站上去，水里升起一只旧木匣，打开里面是老陈留下的星盘刻度（灯塔站密码的另一种解法）。
using System.Collections;
using UnityEngine;

public sealed class DogPawPlate : MonoBehaviour
{
    public static DogPawPlate Instance { get; private set; }

    public Vector3 Center { get; private set; }
    public Vector3 ChestRest { get; private set; }
    public bool Done => state != null && state.flags.Contains("paw_plate_done");
    public bool ChestOpened => state != null && state.flags.Contains("paw_chest_opened");
    // 小黑跟着你以后才用得上
    public bool Available => dog != null && dog.Following && dog.Visible;

    GameState state;
    DogFollower dog;
    EggboyUI ui;
    IslandWorld world;
    Transform plateTop, chest;
    bool rising;

    public void Init(IslandWorld w, GameState s, EggboyUI u)
    {
        Instance = this;
        world = w; state = s; ui = u;
        dog = FindObjectOfType<DogFollower>();
        Place();
        BuildPlate();
        BuildChest();
        var svc = FindObjectOfType<InteractionService>();
        if (svc != null) svc.Register(new PawChestInteractable(this));
    }

    // 船棚的油壶附近，找一块涨到 L2 也不会淹、但旁边就是水的地方
    void Place()
    {
        Vector3 oil = Vector3.zero;
        foreach (var it in LighthouseData.Load().items) if (it.id == "oil") oil = new Vector3(it.x, it.y, it.z);
        float dry = WaterLevel.LevelYs[2] + 0.12f;
        Vector3 best = oil; float bestScore = float.MaxValue; Vector3 seaDir = Vector3.forward;
        for (float r = 1.5f; r <= 6f; r += 0.5f)
            for (int a = 0; a < 24; a++)
            {
                float ang = a * Mathf.PI / 12f;
                var q = oil + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (!world.InWalkArea(q.x, q.z)) continue;
                float gy = world.GroundAt(q.x, q.z, oil.y + 2f);
                if (gy < dry) continue;
                // 往外 1.6 m 就是水（不在可走区里，或地面低于 L2）
                for (int b = 0; b < 8; b++)
                {
                    float bang = b * Mathf.PI / 4f;
                    var w = q + new Vector3(Mathf.Cos(bang), 0f, Mathf.Sin(bang)) * 1.6f;
                    bool wet = !world.InWalkArea(w.x, w.z) || world.GroundAt(w.x, w.z, gy + 1f) < WaterLevel.LevelYs[2] - 0.05f;
                    if (!wet) continue;
                    float score = r;
                    if (score < bestScore) { bestScore = score; best = new Vector3(q.x, gy, q.z); seaDir = (w - q).normalized; }
                }
            }
        Center = best;
        // 木匣浮上来以后搁在踏板旁边的岸上
        Vector3 side = new Vector3(-seaDir.z, 0f, seaDir.x);
        Vector3 rest = best + side * 1.1f;
        if (!world.Walkable(rest.x, rest.z)) rest = best - side * 1.1f;
        if (!world.Walkable(rest.x, rest.z)) rest = best - seaDir * 1.0f;
        rest.y = world.GroundAt(rest.x, rest.z, best.y + 1f);
        ChestRest = rest;
        seaDirection = seaDir;
    }
    Vector3 seaDirection;

    static Material Mat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        return m;
    }

    void BuildPlate()
    {
        var root = new GameObject("DogPawPlate");
        root.transform.SetParent(transform, false);
        root.transform.position = Center;
        var wood = Mat(new Color(0.55f, 0.36f, 0.2f));
        var orange = Mat(new Color(0.95f, 0.5f, 0.18f));
        var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(baseGo.GetComponent<Collider>());
        baseGo.transform.SetParent(root.transform, false);
        baseGo.transform.localScale = new Vector3(0.7f, 0.03f, 0.7f);
        baseGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        baseGo.GetComponent<Renderer>().sharedMaterial = wood;
        var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(top.GetComponent<Collider>());
        top.transform.SetParent(root.transform, false);
        top.transform.localScale = new Vector3(0.6f, 0.02f, 0.6f);
        top.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        top.GetComponent<Renderer>().sharedMaterial = orange;
        plateTop = top.transform;
        // 爪印：一个大肉垫 + 四个小趾头
        void Pad(Vector3 p, float s)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(top.transform, false);
            g.transform.localPosition = p;
            g.transform.localScale = new Vector3(s, 0.9f, s);
            g.GetComponent<Renderer>().sharedMaterial = wood;
        }
        Pad(new Vector3(0f, 0.6f, -0.08f), 0.32f);
        Pad(new Vector3(-0.2f, 0.6f, 0.16f), 0.13f);
        Pad(new Vector3(-0.07f, 0.6f, 0.24f), 0.13f);
        Pad(new Vector3(0.07f, 0.6f, 0.24f), 0.13f);
        Pad(new Vector3(0.2f, 0.6f, 0.16f), 0.13f);
    }

    void BuildChest()
    {
        var holder = TideTownStyleUI.CloneSceneLux3D("S2_木箱_分享包版", 0.55f, false);
        if (holder == null) holder = new GameObject("PawChest");
        holder.name = "PawChest";
        holder.transform.SetParent(transform, true);
        chest = holder.transform;
        // 黄铜包角的感觉：整体往暖铜色偏一点
        foreach (var r in holder.GetComponentsInChildren<Renderer>())
            foreach (var m in r.materials)
                foreach (var p in new[] { "_BaseColor", "_Color", "baseColorFactor" })
                    if (m.HasProperty(p)) m.SetColor(p, Color.Lerp(m.GetColor(p), new Color(0.85f, 0.62f, 0.3f), 0.35f));
        if (Done) chest.position = ChestRest;
        else chest.position = ChestRest + seaDirection * 1.4f + Vector3.down * 1.4f;
        chest.gameObject.SetActive(Done);
    }

    void Update()
    {
        if (state == null || dog == null || Done) return;
        bool dogOn = dog.Visible && Vector2.Distance(new Vector2(dog.transform.position.x, dog.transform.position.z), new Vector2(Center.x, Center.z)) < 0.6f;
        if (plateTop != null) plateTop.localPosition = Vector3.Lerp(plateTop.localPosition, new Vector3(0f, dogOn ? 0.035f : 0.06f, 0f), Time.deltaTime * 10f);
        if (dogOn && !rising)
        {
            state.flags.Add("paw_plate_done");
            StartCoroutine(RiseChest());
        }
    }

    IEnumerator RiseChest()
    {
        rising = true;
        ShadowAndChimeSystem.PlayProceduralChime(330f, 0.3f);
        yield return new WaitForSeconds(0.4f);
        chest.gameObject.SetActive(true);
        Vector3 from = chest.position;
        // 先从水下冒出来，再被浪推到岸上
        Vector3 surface = new Vector3(from.x, Mathf.Max(WaterLevel.StaticY, ChestRest.y) - 0.1f, from.z);
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            chest.position = Vector3.Lerp(from, surface, Mathf.SmoothStep(0f, 1f, t / 1.2f));
            yield return null;
        }
        ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.25f);
        for (float t = 0f; t < 1.0f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            chest.position = Vector3.Lerp(surface, ChestRest, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.2f;
            yield return null;
        }
        chest.position = ChestRest;
        rising = false;
    }

    public bool ChestReady => Done && !rising && chest != null && chest.gameObject.activeSelf;

    public void OpenChest()
    {
        if (!ChestReady || ChestOpened) return;
        state.flags.Add("paw_chest_opened");
        foreach (var id in new[] { "hat", "scope", "log" })
            if (!state.flags.Contains("dog_sniff_" + id)) state.flags.Add("dog_sniff_" + id);
        TideTownAchievementSystem.Unlock("dog_detective");
        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.4f);
        if (chest != null) chest.localRotation *= Quaternion.Euler(0f, 0f, 8f);
        if (ui != null)
            ui.ShowMemory("匣子里的一张航标纸",
                "纸被海水泡软了，字还看得清：\n\n白檐帽　6\n单筒镜　2\n航海日志　3\n\n底下一行小字：给小黑留着，它鼻子比我灵。");
    }
}

public sealed class PawChestInteractable : IInteractable
{
    readonly DogPawPlate plate;
    public PawChestInteractable(DogPawPlate p) { plate = p; }
    public string EntityId => "paw_chest";
    public Vector3 Position => plate.ChestRest;
    public string Prompt(GameState s) => plate.ChestReady && !plate.ChestOpened ? "E　打开木匣" : "";
    public bool CanInteract(GameState s) => plate.ChestReady && !plate.ChestOpened;
    public void Interact(GameState s, InteractionService svc) => plate.OpenChest();
}

// 地上的粉笔爪印 + 一行字（"外环 3"），平躺在地面上
public static class ChalkMark
{
    static Font font;
    public static GameObject Spawn(Vector3 at, string text, string id)
    {
        var root = new GameObject("DogClueMark_" + id);
        root.transform.position = at + Vector3.up * 0.015f;
        var chalk = new Color(0.98f, 0.95f, 0.86f);
        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(sh); mat.color = chalk;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", chalk);
        void Dot(Vector3 p, float s)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = p;
            g.transform.localScale = new Vector3(s, 0.002f, s);
            g.GetComponent<Renderer>().sharedMaterial = mat;
        }
        Dot(new Vector3(0f, 0f, 0f), 0.14f);
        Dot(new Vector3(-0.08f, 0f, 0.1f), 0.05f);
        Dot(new Vector3(-0.03f, 0f, 0.13f), 0.05f);
        Dot(new Vector3(0.03f, 0f, 0.13f), 0.05f);
        Dot(new Vector3(0.08f, 0f, 0.1f), 0.05f);
        if (font == null) font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
        var tgo = new GameObject("Label");
        tgo.transform.SetParent(root.transform, false);
        tgo.transform.localPosition = new Vector3(0f, 0.004f, -0.2f);
        // 平躺、字头朝着镜头一般所在的方向（玩家身后）
        var cam = Camera.main;
        Vector3 toCam = cam != null ? cam.transform.position - at : Vector3.back;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 1e-4f) toCam = Vector3.back;
        tgo.transform.rotation = Quaternion.LookRotation(Vector3.down, -toCam.normalized);
        var tm = tgo.AddComponent<TextMesh>();
        tm.font = font; tm.text = text; tm.fontSize = 64; tm.characterSize = 0.012f;
        tm.anchor = TextAnchor.MiddleCenter; tm.color = chalk;
        if (font != null) tgo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return root;
    }
}
