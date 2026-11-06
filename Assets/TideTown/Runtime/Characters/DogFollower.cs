// WP-12：小黑跟随 EGGBOY。模型导入前先挂灰盒视觉（DogVisualGreybox），
// 跟随逻辑只依赖 DogVisual 抽象，换模型时不用改这里。
using UnityEngine;

public class DogFollower : MonoBehaviour
{
    public float followDistance = 1.2f; // 工程书 WP-12：跟在身后约 1.2 米
    public float sideOffset = 0.4f;     // 略靠一侧，避免正好挡住镜头中心
    public float maxSpeed = 3.4f;
    public float snapDistance = 8f;     // 被墙卡住、掉队太远时直接回到身后

    EggboyPlayer player;
    IslandWorld world;
    GameState state;
    DogVisual visual;
    bool bound;

    Vector3 vel;
    float yaw;
    Vector3 lastPlayerPos;
    bool hasLastPlayer;

    public bool Bound => bound;
    public bool Happy { get; private set; }
    public float WagHz { get; private set; }
    public float LastGroundError { get; private set; }

    // GameBootstrap 在场景加载后把玩家和世界接进来
    public void Bind(EggboyPlayer p, IslandWorld w, GameState s)
    {
        player = p;
        world = w;
        state = s;
        visual = GetComponent<DogVisual>();
        bound = player != null && world != null;
        if (!bound) return;
        lastPlayerPos = player.transform.position;
        hasLastPlayer = true;
        Snap(TargetPoint());
    }

    void LateUpdate()
    {
        if (!bound || player == null || world == null) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        if (dt <= 0f) return;

        // 自动测试每帧用固定 0.05 秒推玩家，帧率高时玩家实际比 real time 快得多；
        // 小黑的追赶速度按玩家这一帧真正走了多远来定，实机仍是 0.65–2 m/s
        Vector3 pNow = player.transform.position;
        float playerStep = 0f;
        if (hasLastPlayer)
            playerStep = new Vector2(pNow.x - lastPlayerPos.x, pNow.z - lastPlayerPos.z).magnitude / dt;
        lastPlayerPos = pNow;
        hasLastPlayer = true;

        // 目标点：玩家身后 1.2 米，略偏右；偏到路面外就退回正后方
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
            // 目标速度：离得远就快一点，玩家跑起来时至少跟得上
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

        // 贴地：GroundAt 与玩家同一套地面层，不靠物理重力，避免掉进缝里
        float ground = world.GroundAt(pos.x, pos.z);
        pos.y = ground;
        LastGroundError = pos.y - ground;
        transform.position = pos;

        float speed = new Vector2(vel.x, vel.z).magnitude;
        bool trot = speed > 1.0f || player.Speed > 1.2f;

        // 朝向：走路看方向，站住看玩家
        float targetYaw = speed > 0.15f
            ? Mathf.Atan2(vel.x, vel.z)
            : Mathf.Atan2(player.transform.position.x - pos.x, player.transform.position.z - pos.z);
        float diff = Mathf.Atan2(Mathf.Sin(targetYaw - yaw), Mathf.Cos(targetYaw - yaw));
        yaw += diff * Mathf.Min(1f, dt * 8f);
        transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);

        Happy = state != null && (state.flags.Contains("oven_lit") || (player.CurrentEmote != null && player.CurrentEmote == "hop"));
        WagHz = Happy ? 4.6f : (speed < 0.2f ? 2.4f : 1.3f);
        if (visual != null) visual.ApplyMotion(dt, speed, trot, WagHz, Happy);
    }

    // 优先侧后方；侧偏点出路面就退回正后方，再不行就贴近一点，最后一招站在玩家脚下
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

    // 灰盒狗不挡路，但身体不应该穿进建筑；只处理建筑盒和柱子，水面交给 Walkable
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
        float y = world.GroundAt(at.x, at.z);
        transform.position = new Vector3(at.x, y, at.z);
        vel = Vector3.zero;
    }
}
