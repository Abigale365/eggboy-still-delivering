// EGGBOY：移动、转向、跳跃、待机 / 走 / 跑（由步态层程序化生成）和一次性动作。
// 从 game/src/player.ts 移植；动作片段只保留 idle 和一次性表情动作，
// 走 / 跑交给 EggboyGait，身体姿态交给 EggboyPose，最后跑 EggboyAntiClip。
using System.Collections.Generic;
using UnityEngine;

public class EggboyPlayer : MonoBehaviour
{
    // 腿只露出 3 cm：走路 0.65 m/s 是短腿碎步不打滑的上限
    const float Walk = 0.65f, Run = 2.0f, Radius = 0.42f;
    const float JumpStand = 0.42f, JumpMove = 0.38f; // 跳多高（米）
    const float GUp = 14f, GDown = 21f, ApexSlow = 0.55f; // 竖直速度小于 0.7 m/s 时重力乘 ApexSlow
    const float CrouchStand = 0.13f, CrouchMove = 0.06f; // 起跳前下蹲多久
    const float LandStand = 0.32f, LandMove = 0.16f; // 落地缓冲多久
    const float Step = 0.22f; // 不用跳就能迈上去的高度（原 0.14 会被路缘/小台阶卡住）
    const float PlatR = 0.3f; // 撞台子侧面时身体的半径
    const float JumpBuffer = .12f, CoyoteTime = .10f;
    float jumpBufferLeft, coyoteLeft;
    bool airborneFromJump;
    public void ResetJumpInput() { jumpBufferLeft = coyoteLeft = 0f; airborneFromJump = false; }
    const float AirControl = 0.35f;

    static readonly Dictionary<string, int> Repeat = new Dictionary<string, int>
    {
        { "wave", 2 }, { "nod", 1 }, { "shake", 1 }, { "hop", 1 }, { "give", 1 }, { "look", 1 }, { "sad", int.MaxValue },
    };

    public IEggboyWorld world;
    public Vector3 SpawnPos => world != null ? world.SpawnPos : Vector3.zero;
    public Vector3 vel;
    public float yaw;           // 弧度，0 = 朝 +Z
    public bool locked;         // 读信时不能动
    public float idle;          // 站着不动多久了（秒）
    public bool grounded = true;
    public float speedScale = 1f; // 2026-11-14 涉水时走得慢：TideFeel 按水深设（1 = 正常），不动走 / 跑本身的数值
    public bool TryingToMove { get; private set; }
    public Vector3 camFocus;    // 给镜头跟的点
    public JumpState jump = new JumpState();
    public EggboyPose pose;
    public EggboyGait gait;
    public EggboyAntiClip antiClip;
    public bool antiClipOn = true;
    public bool disableAnim; // 调试：完全停掉动作片段，只看程序层
    public bool debugPose;   // 调试：每秒打印一次骨骼姿态
    public string CurrentEmote => emote;
    public bool Emoting => emote != null;
    public System.Action<string> onEmote;
    public System.Action onEmoteEnd;
    public GameObject ModelObject { get; private set; }
    public EggboySkin Skin { get; private set; }
    public EggboyBones Bones { get; private set; }

    Animation anim;
    readonly Dictionary<string, AnimationState> clips = new Dictionary<string, AnimationState>();
    string loco = "idle", emote, currentClip;
    float emoteEnd;
    float camY;
    Vector3 prevVel, acc;
    float accYJump;
    float debugT;
    Transform modelT;
    bool storyCrouch;
    float storyCrouchWeight;
    readonly List<EggboyCollider> nearbyColliders = new List<EggboyCollider>(32);

    public void SetStoryCrouch(bool crouch) => storyCrouch = crouch;
    public bool IsStoryCrouched => storyCrouchWeight > 0.9f;

    // ---- 互动动作（docs/INTERACTION_FEEL_BOOK.md）：锁窗 + 抓握帧回调 + 持物姿态 ----
    public enum CarryPose { None, Wood, Oil, Ball }

    class CarryTarget { public string bone; public Quaternion rot; }

    public Transform CarryAnchor { get; private set; } // 挂在 body 骨头下，抱着的东西跟着身体起伏
    public CarryPose Carry => carry;
    public bool ActionLocked => Time.time < actionLockUntil;
    public string CurrentAction => actionClip;

    CarryPose carry = CarryPose.None;
    float carryWeight;
    CarryTarget[] carryTargets;
    float actionLockUntil;
    string actionClip;
    System.Action pendingGrab, pendingComplete;
    float grabAt, completeAt;

    public bool HasClip(string name) => clips.ContainsKey(name);

    // 播一个互动动作：lockSeconds 内不能走 / 跳（&lt;0 取片段时长）；grabAtSeconds 时调 onGrab（道具上手），播完调 onComplete。
    // 没有这个片段时立即调两个回调，剧情不会卡住。
    public bool PlayInteractAction(string clip, float lockSeconds = -1f, float grabAtSeconds = -1f,
        System.Action onGrab = null, System.Action onComplete = null)
    {
        FlushPendingAction();
        if (!clips.TryGetValue(clip, out var s))
        {
            Debug.LogWarning("模型里没有互动动作 " + clip + "，直接执行回调");
            onGrab?.Invoke();
            onComplete?.Invoke();
            return false;
        }
        PlayEmote(clip);
        actionClip = clip;
        float len = s.length;
        actionLockUntil = Time.time + (lockSeconds < 0f ? len : lockSeconds);
        if (grounded) { vel.x = 0f; vel.z = 0f; }
        pendingGrab = onGrab;
        grabAt = Time.time + Mathf.Max(0f, grabAtSeconds);
        pendingComplete = onComplete;
        completeAt = Time.time + len;
        return true;
    }

    // 长按互动期间每帧调用：短暂锁住移动，松手 0.1 秒后自动解锁
    public void HoldActionLock(float seconds = 0.1f)
    {
        actionLockUntil = Mathf.Max(actionLockUntil, Time.time + seconds);
        if (grounded) { vel.x = 0f; vel.z = 0f; }
    }

    public void StopEmote(string name)
    {
        if (emote != null && emote == name) EndEmote();
    }

    // 有挂起的回调（新动作打断旧动作时）立刻执行，道具状态不会丢
    void FlushPendingAction()
    {
        var g = pendingGrab; pendingGrab = null;
        var c = pendingComplete; pendingComplete = null;
        g?.Invoke();
        c?.Invoke();
    }

    void TickActions()
    {
        if (pendingGrab != null && Time.time >= grabAt) { var g = pendingGrab; pendingGrab = null; g(); }
        if (pendingComplete != null && Time.time >= completeAt) { var c = pendingComplete; pendingComplete = null; c(); }
    }

    public void SetCarryPose(CarryPose p)
    {
        if (carry == p) return;
        carry = p;
        carryTargets = BuildCarryTargets(p);
    }

    // 和 EggboyAnimGenerator.ApplyPose 同一套写法：在绑定姿态上转手臂，记下目标局部朝向。
    // 抱柴火的目标正好等于 pickup_wood.anim 的最后一帧，动作播完接持物姿态不会跳。
    CarryTarget[] BuildCarryTargets(CarryPose p)
    {
        if (Bones == null || p == CarryPose.None) return null;
        // armR0 抬放, armR1 前后, handR0, handR1, 左边同理；useL=false 时左手不管
        float armR0, armR1, handR0, handR1, armL0, armL1, handL0, handL1;
        bool useL = true;
        switch (p)
        {
            case CarryPose.Wood: armR0 = 14f; armR1 = 52f; handR0 = 0f; handR1 = 42f; armL0 = 14f; armL1 = 52f; handL0 = 0f; handL1 = 42f; break;
            case CarryPose.Ball: armR0 = 8f; armR1 = 46f; handR0 = 0f; handR1 = 48f; armL0 = 8f; armL1 = 46f; handL0 = 0f; handL1 = 48f; break;
            // 2026-11-16 手提油壶：右臂自然下垂、往外 / 往前稍微张开一点，手腕回正（原来往前端着 34°、手腕弯 24°，像端盘子）
            case CarryPose.Oil: armR0 = -12f; armR1 = 14f; handR0 = 0f; handR1 = 4f; armL0 = armL1 = handL0 = handL1 = 0f; useL = false; break;
            default: return null;
        }
        var list = new List<CarryTarget>();
        float d = Mathf.Deg2Rad;
        void Target(string bone, float r1, float r0, bool right)
        {
            var e = Bones.Get(bone);
            if (e == null) return;
            var saved = e.bone.localRotation;
            e.bone.localRotation = e.bindLocalRotation;
            Bones.Rotate(bone, Vector3.up, (right ? r1 : -r1) * d);
            Bones.Rotate(bone, Vector3.forward, (right ? -r0 : r0) * d);
            list.Add(new CarryTarget { bone = bone, rot = e.bone.localRotation });
            e.bone.localRotation = saved;
        }
        Target("armR", armR1, armR0, true);
        Target("handR", handR1, handR0, true);
        if (useL)
        {
            Target("armL", armL1, armL0, false);
            Target("handL", handL1, handL0, false);
        }
        return list.ToArray();
    }

    void ApplyCarryOverlay(float dt)
    {
        // 互动动作自己在摆手臂时不叠；别的小动作（点头、跳一下）照样抱着
        bool want = carry != CarryPose.None && carryTargets != null && (emote == null || emote != actionClip);
        carryWeight = Mathf.MoveTowards(carryWeight, want ? 1f : 0f, dt * 8f);
        if (carryWeight <= 0f || carryTargets == null || Bones == null) return;
        // 提油壶只盖 80%：走跑时步态的摆臂还留两成，油壶跟着右臂前后晃
        float w = carry == CarryPose.Oil ? carryWeight * 0.8f : carryWeight;
        foreach (var t in carryTargets)
        {
            var e = Bones.Get(t.bone);
            if (e == null) continue;
            e.bone.localRotation = Quaternion.Slerp(e.bone.localRotation, t.rot, w);
        }
        // 手指扣住提手（和跑步握拳同一套弯法，弯到八成）
        if (carry == CarryPose.Oil && gait != null) gait.Grip(LegSide.R, 0.85f * carryWeight);
    }

    // 模型还在绑定姿态时调用：在胸前约 46 cm 高、往前 28 cm 的位置放一个锚点
    void CreateCarryAnchor()
    {
        var body = Bones != null ? Bones.Bone("body") : null;
        var go = new GameObject("CarryAnchor");
        var t = go.transform;
        t.SetParent(transform, false);
        t.localPosition = new Vector3(0f, 0.46f, 0.28f);
        t.localRotation = Quaternion.identity;
        if (body != null) t.SetParent(body, true);
        CarryAnchor = t;
    }

    // 模型挂在这个物体下面。朝向不对时改 modelYawDegrees
    public void Setup(GameObject model, IEggboyWorld w, float modelYawDegrees)
    {
        world = w;
        ModelObject = model;
        modelT = model.transform;
        modelT.SetParent(transform, false);
        modelT.localRotation = Quaternion.Euler(0f, modelYawDegrees, 0f);
        modelT.localScale = Vector3.one * 0.85f; // 设定稿约 80 cm
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.updateWhenOffscreen = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        anim = model.GetComponentInChildren<Animation>();
        if (anim == null) { Debug.LogError("模型里没有 Animation 组件"); enabled = false; return; }
        anim.Stop(); // 先停在绑定姿态，步态 / 防穿模要在这个姿态下量形状
        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null) { Debug.LogError("模型里没有 SkinnedMeshRenderer"); enabled = false; return; }
        var skin = new EggboySkin(model, smr);
        var bones = new EggboyBones(model, smr);
        Skin = skin;
        Bones = bones;
        pose = new EggboyPose(bones);
        gait = new EggboyGait(bones, skin, modelT);
        antiClip = new EggboyAntiClip(skin, bones, modelT);
        CreateCarryAnchor();
        Debug.Log($"EGGBOY skin: shell {skin.shellVerts.Length}, leather {skin.leatherVerts.Length}, visor {skin.visorVerts.Length}; "
            + $"gait {gait.Ok}, antiClip {antiClip.ok}, bones {smr.bones.Length}");

        foreach (AnimationState s in anim)
        {
            // FBX 里的名字是 EGGBOY|EGGBOY|idle，只留最后一段
            var key = s.name.Substring(s.name.LastIndexOf('|') + 1);
            clips[key] = s;
        }
        foreach (var extra in Resources.LoadAll<AnimationClip>("Animations"))
        {
            if (extra == null || clips.ContainsKey(extra.name)) continue;
            extra.legacy = true;
            anim.AddClip(extra, extra.name);
            clips[extra.name] = anim[extra.name];
        }
        foreach (var k in new[] { "idle", "walk", "run" })
            if (clips.TryGetValue(k, out var s)) s.wrapMode = WrapMode.Loop;
        if (disableAnim) anim.Stop(); else Play("idle", 0f);
    }

    void Play(string name, float fade)
    {
        if (!clips.TryGetValue(name, out var s)) { Debug.LogError("模型里没有动作 " + name); return; }
        if (fade > 0f) anim.CrossFade(s.name, fade); else anim.Play(s.name);
        currentClip = name;
    }

    public void PlayEmote(string name, int repeat = 1)
    {
        if (!clips.TryGetValue(name, out var s)) { Debug.LogError("模型里没有动作 " + name); return; }
        s.wrapMode = repeat > 1 ? WrapMode.Loop : WrapMode.ClampForever;
        s.time = 0f;
        emote = name;
        emoteEnd = repeat == int.MaxValue ? float.PositiveInfinity : Time.time + s.length * repeat;
        Play(name, 0.18f);
        idle = 0f;
        onEmote?.Invoke(name);
    }

    void EndEmote()
    {
        emote = null;
        actionClip = null;
        Play(loco, 0.3f);
        onEmoteEnd?.Invoke();
    }

    public void FaceTowards(Vector3 p)
    {
        yaw = Mathf.Atan2(p.x - transform.position.x, p.z - transform.position.z);
    }

    void SetJump(JumpPhase phase, float dur = 0f)
    {
        jump.phase = phase;
        jump.t = 0f;
        jump.dur = dur;
    }

    bool CanUseCollisionPosition(Vector3 from, float x, float z)
    {
        if (!(world is IslandWorld island)) return world.Walkable(x, z);
        if (!island.CanTraverse(from, x, z, jump.phase == JumpPhase.air)) return false;

        // 轴向滑动和连续挤出以后重新检查落点，避免投影到旁边另一件物体里。
        world.QueryColliders(x, z, nearbyColliders);
        foreach (var c in nearbyColliders)
        {
            if (c.water || c.minY > 0f && from.y < c.minY - 0.03f) continue;
            if (c.top.HasValue && (from.y >= c.top.Value - 0.03f || grounded && c.top.Value - from.y <= Step)) continue;
            float radius = c.top.HasValue ? PlatR : Radius;
            float depth = CollisionDepth(c, x, z, radius);
            if (depth > 0.008f && depth > CollisionDepth(c, from.x, from.z, radius) + 0.0005f) return false;
        }
        return true;
    }

    static float CollisionDepth(EggboyCollider collider, float x, float z, float radius)
    {
        float dx = x - collider.x, dz = z - collider.z;
        if (collider.r.HasValue) return collider.r.Value + radius - Mathf.Sqrt(dx * dx + dz * dz);
        if (collider.hw.HasValue && collider.hd.HasValue)
            return Mathf.Min(collider.hw.Value + radius - Mathf.Abs(dx), collider.hd.Value + radius - Mathf.Abs(dz));
        return 0f;
    }

    public void Move(float dt, float inX, float inZ, bool run, bool jumpPressed)
    {
        if (world == null || gait == null) return;
        // Animation 更新之前先还原上一帧的干净值，程序层叠加量才不会跨帧累积
        if (Bones != null) { if (disableAnim) Bones.RestoreBindPose(); else Bones.RestoreClean(); }
        TickActions();
        float len = Mathf.Sqrt(inX * inX + inZ * inZ);
        bool blocked = locked || ActionLocked;
        if (blocked) jumpBufferLeft = 0f;
        else jumpBufferLeft = jumpPressed ? JumpBuffer : Mathf.Max(0f, jumpBufferLeft - dt);
        if (grounded && jump.phase != JumpPhase.crouch) { coyoteLeft = CoyoteTime; airborneFromJump = false; }
        else coyoteLeft = Mathf.Max(0f, coyoteLeft - dt);
        bool moving = len > 0.1f && !blocked;
        TryingToMove = moving;
        float target = moving ? (run ? Run : Walk) * speedScale : 0f;
        float dirX = moving ? inX / len : 0f, dirZ = moving ? inZ / len : 0f;
        JumpState J = jump;
        J.t += dt;

        // 原地跳下蹲时不动；空中只能稍微改方向
        bool holdStill = J.phase == JumpPhase.crouch && !J.moving;
        float k = holdStill ? Mathf.Min(1f, dt * 20f)
            : Mathf.Min(1f, dt * (moving ? 8f : 10f)) * (grounded ? 1f : AirControl);
        vel.x += ((holdStill ? 0f : dirX * target) - vel.x) * k;
        vel.z += ((holdStill ? 0f : dirZ * target) - vel.z) * k;

        // 起跳：先下蹲一下；落地缓冲中也能接着跳
        bool coyoteJump = !grounded && !airborneFromJump && coyoteLeft > 0f;
        if (jumpBufferLeft > 0f && !blocked && (grounded || coyoteJump)
            && (J.phase == JumpPhase.none || J.phase == JumpPhase.land || (coyoteJump && J.phase == JumpPhase.air)))
        {
            jumpBufferLeft = coyoteLeft = 0f;
            airborneFromJump = true;
            FlushPendingAction();
            if (emote != null) EndEmote();
            float speed0 = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
            J.moving = speed0 > 0.3f;
            J.run = Mathf.Clamp01((speed0 - Walk) / (Run - Walk));
            var fL = gait.feet[(int)LegSide.L];
            var fR = gait.feet[(int)LegSide.R];
            J.lead = fL.stance && !fR.stance ? LegSide.R : fR.stance && !fL.stance ? LegSide.L : (fL.fwd < fR.fwd ? LegSide.R : LegSide.L);
            SetJump(JumpPhase.crouch, J.moving ? CrouchMove : CrouchStand);
        }

        // 走动会打断一次性动作（跳一下除外）
        if (moving && emote != null && emote != "hop") { FlushPendingAction(); EndEmote(); }

        // 移动 + 碰撞（使用空间分桶仅查询玩家周围格子内的碰撞体）
        var p = transform.position;
        float nx = p.x + vel.x * dt, nz = p.z + vel.z * dt;
        world.QueryColliders(nx, nz, nearbyColliders);
        for (int ci = 0; ci < nearbyColliders.Count; ci++)
        {
            var c = nearbyColliders[ci];
            if (c.water)
            {
                if (p.y > 0.05f) continue;
                float wr = (c.r ?? 0f) + 0.08f;
                float wd = Mathf.Sqrt((nx - c.x) * (nx - c.x) + (nz - c.z) * (nz - c.z));
                float oldD = Mathf.Sqrt((p.x - c.x) * (p.x - c.x) + (p.z - c.z) * (p.z - c.z));
                if (wd < wr && oldD >= wr - 1e-3f && wd > 1e-6f)
                {
                    nx = c.x + (nx - c.x) / wd * wr;
                    nz = c.z + (nz - c.z) / wd * wr;
                }
                continue;
            }
            float rad = Radius;
            if (c.minY > 0f && p.y < c.minY - 0.03f) continue;
            if (c.top.HasValue)
            {
                if (p.y >= c.top.Value - 0.03f || (grounded && c.top.Value - p.y <= Step)) continue;
                rad = PlatR;
            }
            if (c.r.HasValue)
            {
                float dx = nx - c.x, dz = nz - c.z, d = Mathf.Sqrt(dx * dx + dz * dz), min = c.r.Value + rad;
                if (d < min && d > 1e-6f) { nx = c.x + dx / d * min; nz = c.z + dz / d * min; }
            }
            else if (c.hw.HasValue && c.hd.HasValue)
            {
                float hw = c.hw.Value + rad, hd = c.hd.Value + rad;
                float dx = nx - c.x, dz = nz - c.z;
                if (Mathf.Abs(dx) < hw && Mathf.Abs(dz) < hd)
                {
                    if (hw - Mathf.Abs(dx) < hd - Mathf.Abs(dz)) nx = c.x + Mathf.Sign(dx) * hw;
                    else nz = c.z + Mathf.Sign(dz) * hd;
                }
            }
        }
        if (CanUseCollisionPosition(p, nx, nz)) { p.x = nx; p.z = nz; }
        else if (CanUseCollisionPosition(p, nx, p.z)) { p.x = nx; vel.z = 0f; }
        else if (CanUseCollisionPosition(p, p.x, nz)) { p.z = nz; vel.x = 0f; }
        else vel = Vector3.zero;

        // 竖直方向
        float ground = world.GroundAt(p.x, p.z, p.y);
        float vyPrev = J.vy;
        if (J.phase == JumpPhase.crouch && J.t >= J.dur)
        {
            coyoteLeft = 0f;
            airborneFromJump = true;
            J.vy = J.vy0 = Mathf.Sqrt(2f * GUp * (J.moving ? JumpMove : JumpStand));
            grounded = false;
            SetJump(JumpPhase.air);
        }
        else if (grounded && ground < p.y - Step)
        {
            airborneFromJump = false;
            J.vy = J.vy0 = 0f;
            J.moving = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z) > 0.3f;
            J.run = Mathf.Clamp01((Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z) - Walk) / (Run - Walk));
            grounded = false;
            SetJump(JumpPhase.air);
        }
        if (!grounded)
        {
            float g = (J.vy > 0f ? GUp : GDown) * (Mathf.Abs(J.vy) < 0.7f ? ApexSlow : 1f);
            J.vy -= g * dt;
            p.y += J.vy * dt;
            if (p.y <= ground && J.vy <= 0f)
            {
                J.impact = -J.vy;
                J.vy = 0f;
                p.y = ground;
                grounded = true;
                SetJump(JumpPhase.land, J.moving ? LandMove : LandStand);
            }
        }
        else
        {
            p.y += (ground - p.y) * Mathf.Min(1f, dt * 25f); // 迈上 / 迈下小台阶
            J.vy = 0f;
            if (J.phase == JumpPhase.land && J.t >= J.dur) SetJump(JumpPhase.none);
        }
        accYJump = dt > 0f ? (J.vy - vyPrev) / dt : 0f;
        transform.position = p;

        // 镜头：落地 / 站着时跟住高度，空中只跟 35%
        float camGoal = grounded ? p.y : camY + (p.y - camY) * 0.35f;
        camY += (camGoal - camY) * Mathf.Min(1f, dt * (grounded ? 8f : 3f));
        camFocus = new Vector3(p.x, camY, p.z);

        // 转向：朝着走的方向，平滑转过去
        float speed = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
        if (speed > 0.15f) yaw = Mathf.Atan2(vel.x, vel.z);
        float cur = transform.eulerAngles.y * Mathf.Deg2Rad;
        float diff = Mathf.Atan2(Mathf.Sin(yaw - cur), Mathf.Cos(yaw - cur));
        transform.rotation = Quaternion.Euler(0f, (cur + diff * Mathf.Min(1f, dt * 12f)) * Mathf.Rad2Deg, 0f);

        idle = speed < 0.12f && emote == null && J.phase == JumpPhase.none ? idle + dt : 0f;

        // 走 / 跑由步态层生成，动作片段底下一直放待机
        loco = "idle";
        if (!disableAnim && emote == null && currentClip != loco) Play(loco, 0.22f);
        if (emote != null && Time.time >= emoteEnd) EndEmote();

        // 平滑过的加速度，推天线和邮差包
        if (dt > 0f)
        {
            Vector3 raw = (vel - prevVel) / dt;
            acc = Vector3.Lerp(acc, raw, Mathf.Min(1f, dt * 12f));
            prevVel = vel;
        }
    }

    // 潮汐残影用：不读输入、不做碰撞，直接摆到录下来的位置；速度照录下来的给，
    // 这样 LateUpdate 里的步态层会和真人一样迈腿、摆臂。要在 Update 阶段调用（Animation 更新之前）。
    public void PuppetStep(Vector3 pos, float yawRad, Vector3 velocity, bool isGrounded, float dt)
    {
        if (Bones != null) { if (disableAnim) Bones.RestoreBindPose(); else Bones.RestoreClean(); }
        transform.position = pos;
        yaw = yawRad;
        transform.rotation = Quaternion.Euler(0f, yawRad * Mathf.Rad2Deg, 0f);
        vel = velocity;
        grounded = isGrounded;
        if (dt > 0f)
        {
            Vector3 raw = (vel - prevVel) / dt;
            acc = Vector3.Lerp(acc, raw, Mathf.Min(1f, dt * 12f));
            prevVel = vel;
        }
        if (emote != null && Time.time >= emoteEnd) EndEmote();
    }

    // 在 Animation 更新之后跑程序层
    void LateUpdate()
    {
        if (pose == null || gait == null) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        if (dt <= 0f) return;
        // Animation 已经更新：先记下干净姿态，再叠程序层
        if (!disableAnim && Bones != null) Bones.CaptureClean();
        float speed = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
        float yawRad = transform.eulerAngles.y * Mathf.Deg2Rad;
        float accF = acc.x * Mathf.Sin(yawRad) + acc.z * Mathf.Cos(yawRad);
        float accL = acc.x * Mathf.Cos(yawRad) - acc.z * Mathf.Sin(yawRad);

        gait.Update(dt, new GaitContext { speed = speed, accF = accF, accL = accL, emoting = emote != null, jump = jump });
        pose.Update(dt, new PoseContext
        {
            speed = speed, accF = accF, accL = accL, accY = gait.accY + accYJump,
            emoting = emote != null, jump = gait.jw,
        });
        storyCrouchWeight = Mathf.MoveTowards(storyCrouchWeight, storyCrouch ? 1f : 0f, dt * 5f);
        if (storyCrouchWeight > 0f && Bones != null)
        {
            float bend = 0.35f * storyCrouchWeight;
            Bones.Move("body", 0f, -0.055f * storyCrouchWeight, 0f);
            Bones.Rotate("body", Vector3.right, 0.23f * storyCrouchWeight);
            Bones.Rotate("legL", Vector3.right, -bend);
            Bones.Rotate("legR", Vector3.right, -bend);
            Bones.Rotate("footL", Vector3.right, bend);
            Bones.Rotate("footR", Vector3.right, bend);
        }
        TickActions();
        ApplyCarryOverlay(dt);
        if (antiClipOn && antiClip != null) antiClip.Solve(dt, 1f + 2f * gait.jw);
        if (debugPose && Time.time - debugT > 1f)
        {
            debugT = Time.time;
            var root = Bones != null ? Bones.Bone("root") : null;
            var body = Bones != null ? Bones.Bone("body") : null;
            var armR = Bones != null ? Bones.Bone("armR") : null;
            var handR = Bones != null ? Bones.Bone("handR") : null;
            Debug.Log($"POSE rootL {(root != null ? root.localRotation.eulerAngles.ToString() : "-")} rootW {(root != null ? root.rotation.eulerAngles.ToString() : "-")} "
                + $"bodyL {(body != null ? body.localRotation.eulerAngles.ToString() : "-")} bodyW {(body != null ? body.rotation.eulerAngles.ToString() : "-")} "
                + $"bodyPos {(body != null ? body.position.ToString() : "-")} armR {(armR != null ? armR.position.ToString() : "-")} handR {(handR != null ? handR.position.ToString() : "-")} expr {pose.expr} "
                + $"modelL {modelT.localRotation.eulerAngles} player {transform.eulerAngles} "
                + $"gait w={gait.w:F2} amp={gait.amp:F2} phase={gait.phase:F2} jw={gait.jw:F2} speed={Speed:F2} bounds {Skin.renderer.bounds} "
                + $"anti {antiClip.Measure()}");
        }
    }

    public float Speed => Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
}
