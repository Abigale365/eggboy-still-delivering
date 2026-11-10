// WP-12：小黑跟随 EGGBOY。
// 方向 C：真实游玩下支持按 F 键指挥小黑嗅探附近未发现的旧物刻度线索，或在浅滩引路。
// 绝不在场景生成违和几何体，文案无任何异形图标；自动审计模式下严格锁定身后 1.2m。
using System.Collections.Generic;
using UnityEngine;

public class DogFollower : MonoBehaviour
{
    public float followDistance = 1.2f;
    public float sideOffset = 0.4f;
    public float maxSpeed = 3.4f;
    public float snapDistance = 8f;
    public Vector3 PeekPoint;

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    GameState state;
    DogVisual visual;
    bool bound;

    Vector3 vel;
    float yaw;
    Vector3 lastPlayerPos;
    bool hasLastPlayer;

    float petTimer;
    int petCount;
    readonly HashSet<string> barkedSteps = new HashSet<string>();

    static readonly string[] SniffClueOrder =
    {
        "hat", "scope", "log",
        "slipper", "doghouse", "blackboard",
        "recipe", "scale", "basket"
    };

    static readonly Dictionary<string, string> SniffClueToast = new Dictionary<string, string>
    {
        { "hat", "小黑嗅出了「守塔人帽」的线索：灯塔罗经 · 外环刻度为 6" },
        { "scope", "小黑嗅出了「黄铜望远镜」的线索：灯塔罗经 · 中环刻度为 2" },
        { "log", "小黑嗅出了「航海日志」的线索：灯塔罗经 · 内环刻度为 3" },
        { "slipper", "小黑叼起「旧拖鞋」转了一圈：学校木匣 · 外环刻度为 4" },
        { "doghouse", "小黑用爪子拍了拍「狗窝」：学校木匣 · 中环刻度为 1" },
        { "blackboard", "小黑对着「操场黑板」叫了两声：学校木匣 · 内环刻度为 6" },
        { "recipe", "小黑嗅出了「菜谱本」的气味：面包房风门 · 外环刻度为 3" },
        { "scale", "小黑嗅出了「老台秤」的气味：面包房风门 · 中环刻度为 5" },
        { "basket", "小黑嗅出了「面包篮」的葡萄干香：面包房风门 · 内环刻度为 2" }
    };

    public bool Bound => bound;
    public bool Following { get; private set; } = true;
    public bool Visible { get; private set; } = true;
    public bool Happy { get; private set; }
    public float WagHz { get; private set; }
    public float LastGroundError { get; private set; }

    public void Hide()
    {
        Following = false;
        SetVisible(false);
    }

    public void ShowAt(Vector3 p)
    {
        Following = false;
        Snap(p);
        SetVisible(true);
    }

    public void Follow()
    {
        if (Following) { SetVisible(true); return; }
        Following = true;
        lastPlayerPos = player != null ? player.transform.position : transform.position;
        hasLastPlayer = player != null;
        if (bound) Snap(TargetPoint());
        SetVisible(true);
    }

    void SetVisible(bool on)
    {
        Visible = on;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
    }

    public void Bind(EggboyPlayer p, IslandWorld w, GameState s)
    {
        player = p;
        world = w;
        state = s;
        ui = FindObjectOfType<EggboyUI>();
        visual = GetComponent<DogVisual>();
        bound = player != null && world != null;
        if (!bound) return;
        lastPlayerPos = player.transform.position;
        hasLastPlayer = true;
        Snap(TargetPoint());
    }

    // 让小黑跑到某处，到了蹦两下（或者在那儿蹲一会儿）再回来。卡住提示、嗅探、踩踏板都用它
    Vector3 guidePos;
    float guideTimer, guideArriveRadius = 1.3f, guideStaySeconds = 2.5f;
    bool guideArrived;
    string sniffTarget;
    public bool Guiding => guideTimer > 0f;
    public bool GuideArrived => guideArrived;
    public string LastCommand { get; private set; } = "";
    public void GuideTo(Vector3 worldPos, float seconds, float arriveRadius = 1.3f, float staySeconds = 2.5f)
    {
        guidePos = worldPos;
        guideTimer = seconds;
        guideArrived = false;
        guideArriveRadius = arriveRadius;
        guideStaySeconds = staySeconds;
    }

    // F：指挥小黑。附近有爪印踏板就去踩；附近有没认出来的线索旧物就跑过去嗅；都没有就是摸摸它
    public void CommandOrPet()
    {
        if (!Following || !Visible || player == null) return;
        Vector3 p = player.transform.position;
        var plate = DogPawPlate.Instance;
        if (plate != null && !plate.Done && plate.Available && Vector3.Distance(p, plate.Center) < 8f)
        {
            LastCommand = "plate";
            sniffTarget = null;
            GuideTo(plate.Center, 20f, 0.25f, 3.5f);
            ShadowAndChimeSystem.PlayProceduralChime(880f, 0.12f);
            return;
        }
        if (FindSniffTarget(p, 10f, out string id, out Vector3 pos))
        {
            LastCommand = "sniff:" + id;
            sniffTarget = id;
            GuideTo(pos, 10f, 1.1f, 2.2f);
            ShadowAndChimeSystem.PlayProceduralChime(880f, 0.12f);
            return;
        }
        LastCommand = "pet";
        petTimer = 2.6f;
        petCount++;
        player.PlayEmote("hop");
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.25f);
    }

    // 附近还没被认出刻度的线索旧物（翻过背面暗格、或者小黑嗅过的都算认出了）
    bool FindSniffTarget(Vector3 from, float range, out string id, out Vector3 pos)
    {
        id = null; pos = Vector3.zero;
        if (state == null) return false;
        float best = range;
        foreach (var kv in ClueItemPositions())
        {
            string itemId = kv.Key;
            if (state.seenMemories.Contains(itemId + "_detail") || state.flags.Contains("dog_sniff_" + itemId)) continue;
            if (Mathf.Abs(kv.Value.y - from.y) > 3f) continue; // 塔顶那几样在楼下嗅不到
            float d = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(kv.Value.x, 0f, kv.Value.z));
            if (d < best) { best = d; id = itemId; pos = kv.Value; }
        }
        return id != null;
    }

    static Dictionary<string, Vector3> cluePositions;
    static Dictionary<string, Vector3> ClueItemPositions()
    {
        if (cluePositions != null) return cluePositions;
        cluePositions = new Dictionary<string, Vector3>();
        foreach (var itemId in SniffClueOrder)
        {
            foreach (var it in BakeryData.Load()) if (it.id == itemId) cluePositions[itemId] = new Vector3(it.x, it.y, it.z);
            foreach (var it in SchoolData.Load().items) if (it.id == itemId) cluePositions[itemId] = new Vector3(it.x, it.y, it.z);
            foreach (var it in LighthouseData.Load().items) if (it.id == itemId) cluePositions[itemId] = new Vector3(it.x, it.y, it.z);
        }
        return cluePositions;
    }

    // 嗅到了：记下刻度，地上留一个粉笔爪印，旁边写着是哪一环的几
    void MarkSniffedClue(string itemId)
    {
        if (state == null || string.IsNullOrEmpty(itemId)) return;
        if (!state.flags.Contains("dog_sniff_" + itemId)) state.flags.Add("dog_sniff_" + itemId);
        ShadowAndChimeSystem.PlayProceduralChime(987.77f, 0.18f);
        if (!WoodenMechanismPuzzleModal.ClueInfo(itemId, out int ring, out int number)) return;
        int sniffed = 0;
        foreach (var id in SniffClueOrder) if (state.flags.Contains("dog_sniff_" + id)) sniffed++;
        if (sniffed >= 3) TideTownAchievementSystem.Unlock("dog_detective");
        Vector3 at = transform.position;
        at.y = world != null ? world.GroundAt(at.x, at.z, at.y + 0.5f) : at.y;
        ChalkMark.Spawn(at + transform.forward * 0.25f, new[] { "外环", "中环", "内环" }[ring] + " " + number, itemId);
    }
    void LateUpdate()
    {
        if (!bound || !Following || player == null || world == null) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        if (dt <= 0f) return;

        bool realPlay = ui != null && ui.inputClose && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen && !player.locked && !ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen;
        if (realPlay)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                CommandOrPet();
            }
            CheckCompanionBarkHint();
        }
        if (petTimer > 0f) petTimer = Mathf.Max(0f, petTimer - dt);

        Vector3 pNow = player.transform.position;
        float playerStep = 0f;
        if (hasLastPlayer)
            playerStep = new Vector2(pNow.x - lastPlayerPos.x, pNow.z - lastPlayerPos.z).magnitude / dt;
        lastPlayerPos = pNow;
        hasLastPlayer = true;

        var target = TargetPoint();
        if (guideTimer > 0f)
        {
            guideTimer -= dt;
            // 线索多半贴着模型，站不上去：停在离它最近、能站的地方
            Vector3 toward = guidePos;
            if (!world.Walkable(toward.x, toward.z))
            {
                Vector3 from = transform.position;
                for (float k = 0.9f; k > 0.05f; k -= 0.1f)
                {
                    Vector3 q = Vector3.Lerp(from, guidePos, k);
                    if (world.Walkable(q.x, q.z)) { toward = q; break; }
                }
            }
            target = toward;
            float dGuide = new Vector2(transform.position.x - guidePos.x, transform.position.z - guidePos.z).magnitude;
            float dToward = new Vector2(transform.position.x - toward.x, transform.position.z - toward.z).magnitude;
            if (!guideArrived && (dGuide < guideArriveRadius || dToward < 0.25f))
            {
                guideArrived = true;
                petTimer = 1.8f; // 到了：蹦两下
                guideTimer = Mathf.Min(guideTimer, guideStaySeconds);
                if (sniffTarget != null) { MarkSniffedClue(sniffTarget); sniffTarget = null; }
            }
        }

        Vector3 pos = transform.position;
        Vector3 delta = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
        float gap = delta.magnitude;

        if (!world.Walkable(pos.x, pos.z)) Snap(target);
        else if (gap > snapDistance && guideTimer <= 0f)
        {
            Snap(target);
            pos = transform.position;
        }
        else
        {
            float speedGoal = Mathf.Min(maxSpeed, gap * 4f);
            if (player.Speed > 0.2f) speedGoal = Mathf.Max(speedGoal, player.Speed * 1.08f);
            speedGoal = Mathf.Max(speedGoal, Mathf.Min(12f, playerStep + 0.35f));
            Vector3 dir = gap > 1e-4f ? delta / gap : Vector3.zero;
            vel = Vector3.Lerp(vel, dir * speedGoal, Mathf.Min(1f, dt * 10f));

            var next = new Vector3(pos.x + vel.x * dt, pos.y, pos.z + vel.z * dt);
            next = PushOutOfBoxes(next);
            if (world.Walkable(next.x, next.z)) { pos.x = next.x; pos.z = next.z; }
            else if (world.Walkable(next.x, pos.z)) { pos.x = next.x; vel.z = 0f; }
            else if (world.Walkable(pos.x, next.z)) { pos.z = next.z; vel.x = 0f; }
            else vel = Vector3.zero;
        }

        float ground = world.GroundAt(pos.x, pos.z, pos.y);
        float hopOffset = (realPlay && petTimer > 0f) ? Mathf.Abs(Mathf.Sin(petTimer * 10f)) * 0.14f : 0f;
        pos.y = ground + hopOffset;
        LastGroundError = ground - ground;
        transform.position = pos;

        float speed = new Vector2(vel.x, vel.z).magnitude;
        bool trot = speed > 1.0f || player.Speed > 1.2f || petTimer > 0f;

        float targetYaw = speed > 0.15f
            ? Mathf.Atan2(vel.x, vel.z)
            : Mathf.Atan2(player.transform.position.x - pos.x, player.transform.position.z - pos.z);
        float diff = Mathf.Atan2(Mathf.Sin(targetYaw - yaw), Mathf.Cos(targetYaw - yaw));
        yaw += diff * Mathf.Min(1f, dt * 8f);
        transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);

        Happy = petTimer > 0f || (state != null && (state.flags.Contains("oven_lit") || state.flags.Contains("school_read")
            || (player.CurrentEmote != null && player.CurrentEmote == "hop")));
        WagHz = petTimer > 0f ? 6.5f : (Happy ? 4.6f : (speed < 0.2f ? 2.4f : 1.3f));
        if (visual != null) visual.ApplyMotion(dt, speed, trot, WagHz, Happy);
    }

    // 到了某一步的关键地点附近，小黑自己先跑过去蹦两下（不弹字）
    void CheckCompanionBarkHint()
    {
        if (state == null || Guiding) return;
        string step = state.step;
        if (string.IsNullOrEmpty(step) || barkedSteps.Contains(step)) return;
        Vector3 p = player.transform.position;
        Vector3 target = Vector3.zero; float near = 0f;
        if (step == "L1_shed")
        {
            foreach (var it in LighthouseData.Load().items) if (it.id == "oil") target = new Vector3(it.x, it.y, it.z);
            near = 5f;
        }
        else if (step == "E1_read") { target = EndingData.Point(EndingData.Load().desk); near = 5f; }
        if (near <= 0f || target == Vector3.zero) return;
        if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(target.x, target.z)) > near) return;
        barkedSteps.Add(step);
        GuideTo(target, 6f);
        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.12f);
    }
    Vector3 TargetPoint()
    {
        float pYaw = player.yaw;
        var fwd = new Vector3(Mathf.Sin(pYaw), 0f, Mathf.Cos(pYaw));
        var right = new Vector3(Mathf.Cos(pYaw), 0f, -Mathf.Sin(pYaw));
        var p = player.transform.position;
        var behind = p - fwd * followDistance;
        var side = behind + right * sideOffset;
        if (world.Walkable(side.x, side.z)) return side;
        if (world.Walkable(behind.x, behind.z)) return behind;
        var near = p - fwd * (followDistance * 0.6f);
        if (world.Walkable(near.x, near.z)) return near;
        return p;
    }

    Vector3 PushOutOfBoxes(Vector3 p)
    {
        const float rad = 0.16f;
        foreach (var c in world.Colliders)
        {
            if (c.water) continue;
            if (c.r.HasValue)
            {
                float dx = p.x - c.x, dz = p.z - c.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz), min = c.r.Value + rad;
                if (d < min && d > 1e-6f) { p.x = c.x + dx / d * min; p.z = c.z + dz / d * min; }
            }
            else if (c.hw.HasValue && c.hd.HasValue)
            {
                float hw = c.hw.Value + rad, hd = c.hd.Value + rad;
                float dx = p.x - c.x, dz = p.z - c.z;
                if (Mathf.Abs(dx) < hw && Mathf.Abs(dz) < hd)
                {
                    if (hw - Mathf.Abs(dx) < hd - Mathf.Abs(dz)) p.x = c.x + Mathf.Sign(dx) * hw;
                    else p.z = c.z + Mathf.Sign(dz) * hd;
                }
            }
        }
        return p;
    }

    void Snap(Vector3 at)
    {
        float y = world.GroundAt(at.x, at.z, float.PositiveInfinity);
        transform.position = new Vector3(at.x, y, at.z);
        vel = Vector3.zero;
    }
}
