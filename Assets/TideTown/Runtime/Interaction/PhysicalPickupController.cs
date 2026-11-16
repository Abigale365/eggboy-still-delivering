// 2026-11-16 真实接触式拾取 / 放下（docs/PHYSICAL_PICKUP_IK_BOOK_20261116.md）。
// 按 E 以后：先转身朝物体，沿直线走到正面站位 → 播互动动作 → 上身送肩、右手 IK 伸向物体真实位置 →
// 掌心真贴到物体表面（或动作到了接触时刻还差一点就强制贴上）的那一帧才提交剧情（拿到手 / 放进去）。
// 红球另外全程托在右手掌心里（持球走、跑、跳都跟着身体），放进狗碗时侧身弯腰、松手落进碗里。
//
// 单一位移控制：走位阶段每帧 HoldActionLock 让 PrototypeGame 的键盘输入不起作用，位移只由这里 PuppetStep 驱动，
// 每一步都查 IslandWorld.CanTraverse / GroundAt 和碰撞圆；走不过去就原地做动作，绝不卡死。
// 审计模式（-storyaudit / -innovaudit 等，StoryDirector.IsAuditMode）或缺动作片段时 Active = false，
// 调用方照旧走原来的即时回调，自动化审计零回归。
// 执行顺序 100：LateUpdate 在 EggboyPlayer.LateUpdate（动画、步态、姿态、防穿模）之后；Update 在 PrototypeGame.Update 之后。
using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class PhysicalPickupController : MonoBehaviour
{
    public static PhysicalPickupController Instance { get; private set; }

    // 一次"走过去 + 做动作"的请求
    public sealed class Job
    {
        public string clip;
        public Func<Vector3> target;        // 手要碰的点（世界坐标；红球就是球心）
        public Transform item;              // 要托起来的物体（红球）；其他物品不用
        public float diameter = PickupGrip.DefaultDiameter;
        public float standDistance = 0.40f; // 站位离目标（水平）多远
        public Vector3 standOffset;         // 非零时：站位 = 目标 - 朝向旋转后的这个偏移（狗碗是侧前方）
        public float contactTime = 0.40f;   // 动作播到这一刻以后才认接触
        public float tolerance = 0.03f;     // 伸到底时掌心离接触点多近算碰到（摊台上的火柴盒比 EGGBOY 还高，放宽）
        public float lockSeconds = -1f;
        public bool reach = true;           // 右手 IK 伸过去
        public bool liftBall;               // 红球：接触后弧线托到身侧并一直拿着
        public bool putBall;                // 红球：伸到目标上方松手
        public Action onContact;            // 接触 / 松手那一帧
        public Action onDone;               // 动作播完
    }

    enum Phase { Idle, Approach, Act }

    EggboyPlayer player;
    IslandWorld island;
    StoryDirector director;
    PickupGrip grip;
    Animation anim;
    Phase phase;
    Job job;
    Vector3 stand;
    float standYaw;          // 度
    float phaseStart;
    bool contacted;
    Vector3 itemStart;       // 接触时物体的世界位置
    Vector3 liftFrom;        // 接触时物体在 body 骨局部的位置
    Vector3 dropFrom;
    float releasedAt;
    SchoolBall heldBall;     // 正拿在右手里的红球
    readonly List<EggboyCollider> near = new List<EggboyCollider>(32);

    public static bool Trace;   // 验收脚本打开：接触前每帧打印掌心 / 目标
    public float ContactError { get; private set; }
    public float ContactForward { get; private set; }
    public float LastShellGap { get; private set; }
    public string LastEvent { get; private set; } = "";
    public bool Busy => phase != Phase.Idle;
    public PickupGrip Grip => grip;   // 2026-11-16：持物（油壶挂手、柴捆贴胸）要用掌心位置和蛋壳轮廓
    public bool HoldsBall => heldBall != null;
    public SchoolBall HeldBall => heldBall;

    public void Init(EggboyPlayer p, IslandWorld w, StoryDirector d)
    {
        Instance = this;
        player = p; island = w; director = d;
        if (player != null && player.Bones != null && player.Skin != null) grip = new PickupGrip(player.Bones, player.Skin);
        anim = player != null && player.ModelObject != null ? player.ModelObject.GetComponentInChildren<Animation>() : null;
        // 和参考 Demo 一样：角色不在镜头里也要播动作（拾取下蹲在片段里，不播的话手够不着地上的球）
        if (anim != null) anim.cullingType = AnimationCullingType.AlwaysAnimate;
        if (grip == null || !grip.Ok) Debug.LogWarning("PICKUP 右手 / 身体骨骼不全，真实拾取停用");
    }

    // 真人游玩才启用；审计和缺片段时调用方走原来的即时路径
    public bool Active(string clip) =>
        grip != null && grip.Ok && player != null && island != null && director != null && !director.IsAuditMode
        && (string.IsNullOrEmpty(clip) || player.HasClip(clip));

    public bool Begin(Job j)
    {
        if (j == null || Busy || !Active(j.clip)) return false;
        job = j;
        contacted = false;
        Vector3 target = j.target();
        if (!FindStand(target, j, out stand, out standYaw))
        {
            // 前后左右都找不到能走过去的站位：原地转身做动作
            Debug.Log($"PICKUP_NOSTAND {j.clip} target=({target.x:F2},{target.z:F2}) from=({player.transform.position.x:F2},{player.transform.position.z:F2})");
            stand = player.transform.position;
            standYaw = YawTo(player.transform.position, target);
            if (j.standOffset != Vector3.zero) standYaw -= Mathf.Atan2(j.standOffset.x, j.standOffset.z) * Mathf.Rad2Deg;
        }
        phase = Phase.Approach;
        phaseStart = Time.time;
        player.HoldActionLock(0.2f);
        LastEvent = $"approach {j.clip} stand=({stand.x:F2},{stand.z:F2}) yaw={standYaw:F0}";
        Debug.Log("PICKUP_BEGIN " + LastEvent);
        return true;
    }

    // ---------- 红球持球生命周期 ----------

    public void AttachCarryBall(SchoolBall ball)
    {
        if (ball == null) return;
        heldBall = ball;
        ball.transform.SetParent(null, true);
        if (player != null) player.SetCarryPose(EggboyPlayer.CarryPose.None);   // 右手 IK 托着，不叠抱东西的双臂姿态
        if (grip != null && grip.Ok) ball.transform.position = grip.CarryCenter;
    }

    // 掉落 / 退回秋千 / 滚进碗：不再跟着手
    public void ReleaseCarryBall(SchoolBall ball = null)
    {
        if (heldBall == null || (ball != null && ball != heldBall)) return;
        heldBall = null;
        if (phase == Phase.Act && job != null && job.putBall && !contacted) { phase = Phase.Idle; job = null; }
    }

    // ---------- 每帧 ----------

    // 这一帧还没人把骨骼还原到干净姿态（读信、端详弹窗打开时 PrototypeGame 不调 Move）就自己还原一次：
    // LeanForward / Reach 是在当前骨骼上叠的，不还原会一帧帧累积，手越伸越歪。重复还原没有副作用。
    void RestorePose()
    {
        if (player.Bones == null) return;
        if (player.disableAnim) player.Bones.RestoreBindPose(); else player.Bones.RestoreClean();
    }

    void Update()
    {
        if (player == null) return;
        if (phase == Phase.Act || heldBall != null) RestorePose();
        if (phase != Phase.Approach) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        player.HoldActionLock(0.15f);
        Vector3 pos = player.transform.position;
        float yaw = Mathf.MoveTowardsAngle(player.transform.eulerAngles.y, standYaw, dt * 540f);
        bool facing = Mathf.Abs(Mathf.DeltaAngle(yaw, standYaw)) <= 0.5f;
        Vector3 flatPos = Flat(pos), flatStand = Flat(stand);
        float remaining = Vector3.Distance(flatPos, flatStand);
        bool timedOut = Time.time - phaseStart > 4.5f;
        // 先转到目标朝向再走（离站位很近时边走边转）
        if (!facing && remaining > 0.05f && !timedOut)
        {
            player.PuppetStep(pos, yaw * Mathf.Deg2Rad, Vector3.zero, true, dt);
            return;
        }
        float speed = Mathf.Clamp(remaining * 5f, 0.20f, 0.65f);
        Vector3 next = Vector3.MoveTowards(flatPos, flatStand, dt * speed);
        float ground = island.GroundAt(next.x, next.z, pos.y);
        // 起点本来就贴着栏杆 / 墙（在 0.26 m 净空以内）时允许往外走，只拦"从空地走进障碍物"
        bool canStep = remaining <= 0.0005f || island.CanTraverse(pos, next.x, next.z, false) && Mathf.Abs(ground - pos.y) < 0.25f
            && (!Blocked(next, ground) || Blocked(pos, pos.y) && Clearance(next, ground) >= Clearance(pos, pos.y) - 0.001f);
        if (!canStep || timedOut)
        {
            Debug.Log($"PICKUP_STOP {(timedOut ? "timeout" : "blocked")} at=({pos.x:F2},{pos.y:F2},{pos.z:F2}) next=({next.x:F2},{ground:F2},{next.z:F2}) traverse={island.CanTraverse(pos, next.x, next.z, false)} blocked={Blocked(next, ground)}");
            // 走不过去（有人挡住、地形变了）：就在这里做动作
            player.PuppetStep(pos, standYaw * Mathf.Deg2Rad, Vector3.zero, true, dt);
            StartAct();
            return;
        }
        next.y = ground;
        bool arrived = Vector3.Distance(next, new Vector3(flatStand.x, 0f, flatStand.z) + Vector3.up * next.y) < 0.0005f;
        Vector3 v = arrived || dt <= 0f ? Vector3.zero : (next - pos) / dt;
        v.y = 0f;
        player.PuppetStep(next, yaw * Mathf.Deg2Rad, v, true, dt);
        if (arrived && Mathf.Abs(Mathf.DeltaAngle(yaw, standYaw)) < 1f) StartAct();
    }

    float lastError = 9f;

    void StartAct()
    {
        phase = Phase.Act;
        phaseStart = Time.time;
        contacted = false;
        lastError = 9f;
        player.PuppetStep(player.transform.position, standYaw * Mathf.Deg2Rad, Vector3.zero, true, 0f);
        player.PlayInteractAction(job.clip, job.lockSeconds, -1f, null, null);
        if (job.liftBall) itemStart = job.target();
        LastEvent = $"act {job.clip} at=({player.transform.position.x:F2},{player.transform.position.z:F2})";
    }

    void LateUpdate()
    {
        if (player == null || grip == null || !grip.Ok) return;
        float groundY = player.transform.position.y;
        if (phase == Phase.Act && job != null)
        {
            if (job.putBall) UpdatePut(groundY);
            else if (job.liftBall) UpdateLift(groundY);
            else UpdateReach(groundY);
            return;
        }
        if (heldBall != null) HoldAt(heldBall.transform, grip.CarryCenter, 1f, 1f);
    }

    // 动作开始以来的时间。不读 AnimationState.time：角色不在镜头里时 Animation 可能不更新，那样接触帧永远等不到
    float ClipTime() => Time.time - phaseStart;

    float ClipLength()
    {
        var s = anim != null ? anim[job.clip] : null;
        return s != null ? s.length : 0.9f;
    }

    // 红球拿起：掌心碰到球面那一帧球不动，之后沿弧线托到身体右侧
    void UpdateLift(float groundY)
    {
        float t = ClipTime(), len = ClipLength();
        float stretch = Ease(0.09f, 0.37f, t) * (1f - Ease(0.47f, 0.84f, t));
        grip.LeanForward(stretch, groundY);
        var ball = job.item;
        if (!contacted)
        {
            float reach = Ease(0.09f, 0.38f, t);
            ContactError = grip.Reach(itemStart, reach, 0f, 0f, job.diameter);
            if (Trace) Debug.Log($"PICKUP_TRACE t={t:F2} reach={reach:F2} err={ContactError:F3} palm={grip.PalmPosition:F3} item={itemStart:F3} body={grip.Body.position:F3} root={player.transform.position:F3}");
            // 接触：掌心到球面 ≤ 1.2 cm；或者到了接触时刻以后已经贴到 3 cm 内、而且不再靠近（这一伸已经到底）
            bool settled = t >= job.contactTime && ContactError <= job.tolerance && ContactError >= lastError - 0.0005f;
            bool forced = t >= job.contactTime + 0.22f;
            lastError = ContactError;
            if (t < job.contactTime || (ContactError > 0.012f && !settled && !forced))
            {
                if (t >= len) FinishContact(true);
                return;
            }
            liftFrom = grip.Body.InverseTransformPoint(itemStart);
            ContactForward = Vector3.Dot(grip.PalmPosition - player.transform.position, player.transform.forward);
            var sb = ball != null ? ball.GetComponent<SchoolBall>() : null;
            if (sb != null) { heldBall = sb; ball.SetParent(null, true); player.SetCarryPose(EggboyPlayer.CarryPose.None); }
            FinishContact(forced);
        }
        if (ball == null) { if (t >= len) EndAct(); return; }
        float lift = Ease(job.contactTime + 0.045f, 0.84f, t);
        Vector3 carry = grip.CarryCenterLocal;
        Vector3 local = Vector3.Slerp(Flat(liftFrom), Flat(carry), lift);
        local.y = Mathf.Lerp(liftFrom.y, carry.y, lift);
        Vector3 center = Vector3.Lerp(itemStart, grip.Body.TransformPoint(local), Ease(job.contactTime, 0.53f, t));
        HoldAt(ball, center, Ease(0.53f, 0.80f, t), Ease(job.contactTime, 0.50f, t));
        if (t >= len) EndAct();
    }

    // 红球放进狗碗：侧身弯腰，手托着球伸到碗上方，松手
    void UpdatePut(float groundY)
    {
        float t = ClipTime(), len = ClipLength();
        float crouch = Ease(0.06f, 0.42f, t) * (1f - Ease(0.49f, 0.77f, t));
        grip.LeanSide(crouch * 9f, groundY);
        var target = job.target();
        if (!contacted)
        {
            if (heldBall != null)
            {
                Vector3 release = target + Vector3.up * 0.205f;
                HoldAt(heldBall.transform, Vector3.Lerp(grip.CarryCenter, release, Ease(0.03f, 0.45f, t)), 1f - Ease(0.19f, 0.45f, t), 1f);
                dropFrom = heldBall.transform.position;
            }
            if (t >= job.contactTime || t >= len)
            {
                FinishContact(false);
                releasedAt = Time.time;
            }
        }
        else grip.Reach(dropFrom, 1f - Ease(job.contactTime, 0.76f, t), 0f, 0f, job.diameter);
        if (t >= len) EndAct();
    }

    // 一般物品（柴火、油壶、火柴、拖鞋、炉口、灯头）：右手伸向物体，碰到那一帧提交，然后手收回
    void UpdateReach(float groundY)
    {
        float t = ClipTime(), len = ClipLength();
        if (!job.reach)
        {
            if (!contacted && (t >= job.contactTime || t >= len)) FinishContact(false);
            if (t >= len) EndAct();
            return;
        }
        float stretch = Ease(0.09f, 0.37f, t) * (1f - Ease(job.contactTime + 0.05f, job.contactTime + 0.4f, t));
        grip.LeanForward(stretch * 0.7f, groundY);
        Vector3 aim = ReachablePoint(job.target(), groundY);
        float weight = contacted ? 1f - Ease(job.contactTime, job.contactTime + 0.35f, t) : Ease(0.09f, job.contactTime, t);
        ContactError = grip.Reach(aim, weight, 0.3f, contacted ? 1f : Ease(job.contactTime - 0.1f, job.contactTime, t), job.diameter);
        bool settled = ContactError <= job.tolerance && ContactError >= lastError - 0.0005f;
        lastError = ContactError;
        if (!contacted && t >= job.contactTime && (ContactError <= 0.02f || settled || t >= job.contactTime + 0.15f || t >= len))
        {
            ContactForward = Vector3.Dot(grip.PalmPosition - player.transform.position, player.transform.forward);
            FinishContact(ContactError > job.tolerance);
        }
        if (t >= len) EndAct();
    }

    // 太高 / 太远（摊台、炉口、灯头）的目标：手伸到朝它那一侧、够得着的点
    Vector3 ReachablePoint(Vector3 target, float groundY)
    {
        Vector3 from = player.transform.position;
        Vector3 flat = Flat(target - from);
        float d = flat.magnitude;
        if (d > 0.38f) flat = flat / d * 0.38f;
        // 实测（-feeldemo pickup）：右手在身前 0.3–0.4 m 处够得到的高度大约是离地 0.16–0.42 m
        float y = Mathf.Clamp(target.y, groundY + 0.16f, groundY + 0.42f);
        return new Vector3(from.x + flat.x, y, from.z + flat.z);
    }

    void FinishContact(bool forced)
    {
        if (contacted) return;
        contacted = true;
        LastShellGap = heldBall != null ? grip.DistanceToShell(heldBall.transform.position) - job.diameter * 0.5f : 0f;
        LastEvent = $"contact {job.clip} error={ContactError:F4} forward={ContactForward:F3}{(forced ? " forced" : "")}";
        Debug.Log("PICKUP_CONTACT " + LastEvent);
        var cb = job.onContact; job.onContact = null;
        if (job.putBall) heldBall = null;
        cb?.Invoke();
    }

    void EndAct()
    {
        if (!contacted) FinishContact(true);
        var done = job.onDone;
        phase = Phase.Idle;
        job = null;
        done?.Invoke();
    }

    void HoldAt(Transform item, Vector3 center, float scoop, float curl)
    {
        item.position = center;
        ContactError = grip.Reach(center, 1f, scoop, curl, PickupGrip.DefaultDiameter);
        LastShellGap = grip.DistanceToShell(center) - PickupGrip.DefaultDiameter * 0.5f;
    }

    // ---------- 站位 ----------

    // 从玩家这一侧朝目标找正面站位：先试正对，再左右各转 20°、40°…；站位能走、和玩家一样高、一路没有挡人的东西
    bool FindStand(Vector3 target, Job j, out Vector3 at, out float yawDeg)
    {
        Vector3 from = player.transform.position;
        Vector3 back = Flat(from - target);
        if (back.sqrMagnitude < 1e-4f) back = -Flat(player.transform.forward);
        back.Normalize();
        // 大件（炉子、摊台、灯头）的中心在模型里面：正面站位被挡就往外退 0.2 / 0.4 / 0.65 m 再找
        foreach (float extra in new[] { 0f, 0.2f, 0.4f, 0.65f, 0.9f })
        foreach (float ang in new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f, 120f, -120f, 180f })
        {
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * back;
            float yaw = YawTo(target + dir, target);   // 站在 dir 那边、脸朝目标
            Vector3 p;
            if (j.standOffset != Vector3.zero)
            {
                float yawStand = yaw - Mathf.Atan2(j.standOffset.x, j.standOffset.z) * Mathf.Rad2Deg;
                p = Flat(target) - Quaternion.Euler(0f, yawStand, 0f) * j.standOffset + dir * extra;
                yaw = yawStand;
            }
            else p = Flat(target) + dir * (j.standDistance + extra);
            if (!island.Walkable(p.x, p.z)) continue;
            float g = island.GroundAt(p.x, p.z, from.y + 0.3f);
            if (Mathf.Abs(g - from.y) > 0.25f || Blocked(p, g)) continue;
            p.y = g;
            if (!PathClear(from, p)) continue;
            at = p; yawDeg = yaw;
            return true;
        }
        at = from; yawDeg = player.transform.eulerAngles.y;
        return false;
    }

    bool PathClear(Vector3 a, Vector3 b)
    {
        float len = Vector3.Distance(Flat(a), Flat(b));
        int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.1f));
        Vector3 prev = a;
        float startClear = Clearance(a, a.y);
        for (int i = 1; i <= n; i++)
        {
            Vector3 q = Vector3.Lerp(a, b, i / (float)n);
            float g = island.GroundAt(q.x, q.z, prev.y + 0.3f);
            // 起点附近 0.45 m 内只要不比起点更挤就算过（起点本来就贴着栏杆时）
            bool tight = Blocked(q, g) && !(Vector3.Distance(Flat(q), Flat(a)) < 0.45f && Clearance(q, g) >= startClear - 0.001f);
            if (!island.CanTraverse(prev, q.x, q.z, false) || Mathf.Abs(g - prev.y) > 0.25f || tight) return false;
            q.y = g; prev = q;
        }
        return true;
    }

    // 离最近的迈不上去的碰撞体还有多远（负数 = 已经压进去）
    float Clearance(Vector3 p, float ground)
    {
        island.QueryColliders(p.x, p.z, near);
        float best = 9f;
        foreach (var c in near)
        {
            if (c.water) continue;
            if (c.top.HasValue && c.top.Value - ground <= 0.22f) continue;
            if (c.minY > 0f && ground < c.minY - 0.03f) continue;
            if (c.r.HasValue) best = Mathf.Min(best, new Vector2(p.x - c.x, p.z - c.z).magnitude - c.r.Value);
            else if (c.hw.HasValue && c.hd.HasValue) best = Mathf.Min(best, Mathf.Max(Mathf.Abs(p.x - c.x) - c.hw.Value, Mathf.Abs(p.z - c.z) - c.hd.Value));
        }
        return best;
    }

    // 身体半径 0.26 m 内有迈不上去的碰撞体（墙、栏杆、模型外轮廓）
    bool Blocked(Vector3 p, float ground)
    {
        island.QueryColliders(p.x, p.z, near);
        foreach (var c in near)
        {
            if (c.water) continue;
            if (c.top.HasValue && c.top.Value - ground <= 0.22f) continue;
            if (c.minY > 0f && ground < c.minY - 0.03f) continue;
            if (c.r.HasValue)
            {
                float dx = p.x - c.x, dz = p.z - c.z;
                if (dx * dx + dz * dz < (c.r.Value + 0.26f) * (c.r.Value + 0.26f)) return true;
            }
            else if (c.hw.HasValue && c.hd.HasValue)
            {
                if (Mathf.Abs(p.x - c.x) < c.hw.Value + 0.26f && Mathf.Abs(p.z - c.z) < c.hd.Value + 0.26f) return true;
            }
        }
        return false;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
    static float Ease(float start, float end, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));
}
