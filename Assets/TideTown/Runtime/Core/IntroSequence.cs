// WP-10 序章：开场停在预览机位，约 5 秒落到邮局门廊的角色身后；面屏 sleepy → happy。
using UnityEngine;

public sealed class IntroSequence : MonoBehaviour
{
    public const float Duration = 5f;
    public const float WakeAt = 4.2f;

    public bool Done { get; private set; }
    public bool Running => !Done;
    public float StartTime { get; private set; }
    public ExprId StartExpr { get; private set; } = ExprId.sleepy;
    public ExprId EndExpr { get; private set; } = ExprId.happy;

    EggboyPlayer player;
    EggboyUI ui;
    PrototypeGame game;
    ChaseCamera chase;
    Camera cam;

    Vector3 fromPos, toPos;
    Quaternion fromRot, toRot;
    float fromFov, toFov;
    float t;
    bool woke, finished;

    public void Begin(GameBootstrap boot)
    {
        StartTime = Time.realtimeSinceStartup;
        player = boot.player;
        ui = boot.ui;
        game = FindObjectOfType<PrototypeGame>();
        chase = FindObjectOfType<ChaseCamera>();
        cam = Camera.main;

        if (player == null || cam == null || chase == null)
        {
            finish(true);
            return;
        }

        // 开场：镜头先停在预览机位，角色定在原地，面屏是没睡醒的两条线
        chase.enabled = false;
        player.locked = true;
        if (game != null) game.introHold = true;
        game?.SetExpression(StartExpr);
        ui?.SetActiveExpression(StartExpr);

        // 若上一帧 ChaseCamera 已经把镜头拉到身后，这里强制拉回预览机位
        var def = ShotTool.FindCamera("preview");
        if (def != null && def.pos != null && def.pos.Length >= 3
            && def.euler != null && def.euler.Length >= 3)
        {
            fromPos = new Vector3(def.pos[0], def.pos[1], def.pos[2]);
            fromRot = Quaternion.Euler(def.euler[0], def.euler[1], def.euler[2]);
            fromFov = def.fov > 1f ? def.fov : 20f;
            cam.transform.SetPositionAndRotation(fromPos, fromRot);
            cam.fieldOfView = fromFov;
        }
        else
        {
            fromPos = cam.transform.position;
            fromRot = cam.transform.rotation;
            fromFov = cam.fieldOfView;
        }

        // 终点和 ChaseCamera.Tick 的初始姿态一致：角色身后 3.6 m、右肩 0.35 m、俯 14°
        float heading = player.yaw;
        const float pitch = 14f * Mathf.Deg2Rad;
        var right = new Vector3(Mathf.Cos(heading), 0f, -Mathf.Sin(heading));
        var back = new Vector3(-Mathf.Sin(heading) * Mathf.Cos(pitch), Mathf.Sin(pitch),
                               -Mathf.Cos(heading) * Mathf.Cos(pitch));
        var pivot = player.transform.position + Vector3.up * 0.95f + right * 0.35f;
        toPos = pivot + back * 3.6f;
        toPos.y = Mathf.Max(toPos.y, 0.25f);
        toRot = Quaternion.LookRotation(new Vector3(pivot.x, pivot.y - 0.1f, pivot.z) - toPos, Vector3.up);
        toFov = 44f;
    }

    void Update()
    {
        if (Done || cam == null) return;
        t += Time.deltaTime;

        if (t >= WakeAt && !woke)
        {
            woke = true;
            game?.SetExpression(EndExpr);
            ui?.SetActiveExpression(EndExpr);
        }

        if (t >= Duration) { finish(false); return; }

        float k = Mathf.SmoothStep(0f, 1f, t / Duration);
        cam.transform.position = Vector3.Lerp(fromPos, toPos, k);
        cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
        cam.fieldOfView = Mathf.Lerp(fromFov, toFov, k);
    }

    void finish(bool skip)
    {
        if (finished) return;
        finished = true;
        Done = true;
        if (cam != null && !skip)
        {
            cam.transform.position = toPos;
            cam.transform.rotation = toRot;
            cam.fieldOfView = toFov;
        }
        if (player != null) player.locked = false;
        if (game != null) game.introHold = false;
        if (chase != null && player != null)
        {
            chase.SnapBehind(player.transform.position, player.yaw);
            chase.enabled = true;
        }
        game?.SetExpression(EndExpr);
        ui?.SetActiveExpression(EndExpr);
    }
}
