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

    public void CommandOrPet()
    {
        if (!Following || !Visible || player == null) return;
        petTimer = 2.6f;
        player.PlayEmote("hop");
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.25f);

        if (state != null)
        {
            foreach (var itemId in SniffClueOrder)
            {
                string flag = "dog_sniff_" + itemId;
                bool alreadyKnown = state.seenMemories.Contains(itemId) ||
                                    state.seenMemories.Contains(itemId + "_detail") ||
                                    state.flags.Contains(flag);
                if (!alreadyKnown)
                {
                    state.flags.Add(flag);
                    if (ui != null && SniffClueToast.TryGetValue(itemId, out string clueMsg))
                    {
                        ui.ShowToast(clueMsg, 3.8f);
                    }
                    return;
                }
            }
        }

        if (ui != null)
        {
            string[] msgs =
            {
                "小黑摇着尾巴蹭了蹭你的外壳，所有旧物刻度线索都已帮你找齐了。",
                "小黑开心地原地蹦了一下，耳朵一抖一抖地跟紧了你。",
                "小黑用湿漉漉的鼻子碰了碰你的手，干劲十足！"
            };
            ui.ShowToast(msgs[petCount % msgs.Length], 3.0f);
            petCount++;
        }
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

        Vector3 pos = transform.position;
        Vector3 delta = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
        float gap = delta.magnitude;

        if (!world.Walkable(pos.x, pos.z)) Snap(target);
        else if (gap > snapDistance)
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

    void CheckCompanionBarkHint()
    {
        if (state == null || ui == null || ui.ToastVisible) return;
        string step = state.step;
        if (string.IsNullOrEmpty(step) || barkedSteps.Contains(step)) return;

        Vector3 p = player.transform.position;
        if (step == "L0_cross")
        {
            var cratePos = new Vector3(17.4f, 1.0f, -1.4f);
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(cratePos.x, cratePos.z)) < 5.5f)
            {
                barkedSteps.Add(step);
                petTimer = 1.6f;
                ui.ShowToast("汪！小黑跑到浅滩边回头看你，示意可以踩着漂浮的木箱去船棚（按 F 可指挥小黑嗅探线索）", 4.0f);
            }
        }
        else if (step == "L1_shed")
        {
            var shedPos = new Vector3(19.7f, 1.4f, -5.7f);
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(shedPos.x, shedPos.z)) < 4.5f)
            {
                barkedSteps.Add(step);
                petTimer = 1.6f;
                ui.ShowToast("汪汪！小黑停在船棚屋檐下，用鼻子嗅了嗅那只旧油壶。", 4.0f);
            }
        }
        else if (step == "L2_door")
        {
            var doorPos = new Vector3(21.7f, 5.0f, 2.4f);
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(doorPos.x, doorPos.z)) < 5.0f)
            {
                barkedSteps.Add(step);
                petTimer = 1.6f;
                ui.ShowToast("小黑在灯塔台阶下摇着尾巴为你守门（按 F 可让小黑帮你找齐塔顶罗经刻度）", 4.0f);
            }
        }
        else if (step == "E1_read")
        {
            var deskPos = EndingData.Point(EndingData.Load().desk);
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(deskPos.x, deskPos.z)) < 4.5f)
            {
                barkedSteps.Add(step);
                petTimer = 1.6f;
                ui.ShowToast("汪！小黑围着邮局门前的课桌转了一圈，上面放着写给你的信。", 4.0f);
            }
        }
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
