// GTA 式第三人称镜头：挂在 EGGBOY 身后偏右肩，鼠标转视角，滚轮拉远拉近；
// 走起来一会儿没动鼠标，镜头慢慢绕回背后；撞到房子往前缩，向场景 Shader 广播遮挡消融开关。
using UnityEngine;
using UnityEngine.EventSystems;

public class ChaseCamera : MonoBehaviour
{
    const float DistMin = 2.2f, DistMax = 7.5f, DistDefault = 4.2f;
    const float Height = 0.95f, Shoulder = 0.35f;
    const float MouseSens = 0.04f; // Input.GetAxis 的鼠标值是"像素 × 0.05"，折算成和网页版一样的手感
    static float sens = -1f;
    // 视角灵敏度倍率，暂停菜单可调，存 PlayerPrefs；0.4–2.0，默认 1.0
    public static float Sensitivity
    {
        get
        {
            if (sens < 0f) sens = Mathf.Clamp(PlayerPrefs.GetFloat("tidetown.sensitivity", 1f), 0.4f, 2f);
            return sens;
        }
        set
        {
            sens = Mathf.Clamp(value, 0.4f, 2f);
            PlayerPrefs.SetFloat("tidetown.sensitivity", sens);
            PlayerPrefs.Save();
        }
    }
    public static bool AutoRecenter
    {
        get => PlayerPrefs.GetInt("tidetown.recenter", 1) == 1;
        set { PlayerPrefs.SetInt("tidetown.recenter", value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public System.Func<Vector3, Vector3, float, float> extraBlock;
    public bool fadeActive;
    const float RecenterDelay = 5.5f, RecenterRamp = 0.8f, RecenterSpeed = 1.6f, RecenterMaxAngle = 70f * Mathf.Deg2Rad;
    const float MinDist = 0.4f;
    static readonly float[,] Rays = { { 0f, 1f }, { 12f, 0.65f }, { -12f, 0.65f }, { 24f, 0.3f }, { -24f, 0.3f } }; // 偏转角度, 权重
    static readonly float PitchMin = -20f * Mathf.Deg2Rad, PitchMax = 55f * Mathf.Deg2Rad;

    public float yaw; // 弧度，0 = 看向 +Z
    public float extraLift; // WP-08 涨水过场时额外抬高，默认 0
    float pitch = 14f * Mathf.Deg2Rad, yawGoal, pitchGoal = 14f * Mathf.Deg2Rad;
    float dist = DistDefault, distGoal = DistDefault, wallDist = DistDefault;
    float sinceBlocked = 99f, idleMouse;
    Vector3 target;
    Camera cam;

    static float Damp(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);
    static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));

    void Awake() { cam = GetComponent<Camera>(); }

    public Vector3 Forward => new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    public Vector3 Right => new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));

    public void SnapBehind(Vector3 pos, float heading)
    {
        yaw = yawGoal = heading;
        target = new Vector3(pos.x, pos.y + Height, pos.z);
    }

    public void Tick(float dt, Vector3 pos, float heading, bool moving, bool running)
    {
        // 自由漫游中点击画面锁定光标；Esc 由 MenuController 统一处理。
        bool anyClick = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
        if (anyClick && Cursor.lockState != CursorLockMode.Locked
            && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        // 保留按住右键锁定的习惯，但松开右键不再强制解锁
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mx = Input.GetAxisRaw("Mouse X"), my = Input.GetAxisRaw("Mouse Y");
            if (Mathf.Abs(mx) > 0f || Mathf.Abs(my) > 0f) idleMouse = 0f;
            float sens = MouseSens * Sensitivity;
            yawGoal += mx * sens;
            pitchGoal = Mathf.Clamp(pitchGoal - my * sens, PitchMin, PitchMax);
        }
        // 滚轮：按比例拉远拉近
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f) distGoal = Mathf.Clamp(distGoal * Mathf.Exp(-wheel * 0.12f), DistMin, DistMax);

        // 跟随点平滑追上角色
        target = Vector3.Lerp(target, new Vector3(pos.x, pos.y + Height + extraLift, pos.z), Damp(12f, dt));

        // 走着又没动鼠标：慢慢绕回背后，角度差越大越弱
        idleMouse += dt;
        if (AutoRecenter && moving && idleMouse > RecenterDelay)
        {
            float ramp = Mathf.Min(1f, (idleMouse - RecenterDelay) / RecenterRamp);
            float d = Wrap(heading - yawGoal);
            float fade = Mathf.Max(0f, Mathf.Cos(Mathf.Min(Mathf.PI / 2f, Mathf.Abs(d) / RecenterMaxAngle * (Mathf.PI / 2f))));
            yawGoal += d * ramp * fade * Mathf.Min(1f, dt * RecenterSpeed);
        }

        yaw += Wrap(yawGoal - yaw) * Damp(24f, dt);
        pitch += (pitchGoal - pitch) * Damp(24f, dt);
        dist += (distGoal - dist) * Damp(7f, dt);
        cam.fieldOfView += ((running && moving ? 60f : 55f) - cam.fieldOfView) * Damp(4f, dt);

        var back = new Vector3(-Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), -Mathf.Cos(yaw) * Mathf.Cos(pitch));
        var pivot = target + Right * Shoulder;
        if (Physics.CheckSphere(pivot, 0.12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            pivot = target - Right * Shoulder;

        // 防穿墙：正后方一条射线，左右各偏 12°、24° 打"提前量"射线，偏得越多权重越低
        float wanted = dist;
        if (Physics.SphereCast(pivot, 0.16f, back, out var wall, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            wanted = Mathf.Max(MinDist, wall.distance - 0.12f);
        for (int i = 0; i < Rays.GetLength(0); i++)
        {
            var dir = Quaternion.AngleAxis(Rays[i, 0], Vector3.up) * back;
            if (!Physics.Raycast(pivot, dir, out var hit, dist + 0.3f)) continue;
            float h = Mathf.Max(MinDist, hit.distance - 0.25f);
            wanted = Mathf.Min(wanted, dist - (dist - h) * Rays[i, 1]);
        }
        if (extraBlock != null) wanted = Mathf.Min(wanted, Mathf.Max(MinDist, extraBlock(pivot, back, dist)));
        sinceBlocked = wanted < dist - 0.01f ? 0f : sinceBlocked + dt;
        float rate = wanted < wallDist ? 14f : sinceBlocked < 0.8f ? 3f : 7f;
        float step = Mathf.Clamp((wanted - wallDist) * Damp(rate, dt), -8f * dt, 8f * dt);
        wallDist = Mathf.Min(wallDist + step, wanted);

        // 贴着墙时镜头一边靠近一边升高
        float lift = Mathf.Max(0f, 1.3f - wallDist) * 0.6f;
        var camPos = pivot + back * wallDist;
        camPos.y = Mathf.Max(camPos.y + lift, 0.25f);
        // 再检查抬高后的实际射线，不能让避墙升高把镜头送入檐口。
        var finalRay = camPos - pivot;
        float safe = finalRay.magnitude;
        if (Physics.SphereCast(pivot, .16f, finalRay.normalized, out var finalHit, safe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) safe = Mathf.Min(safe, Mathf.Max(.1f, finalHit.distance - .12f));
        if (extraBlock != null) safe = Mathf.Min(safe, Mathf.Max(.1f, extraBlock(pivot, finalRay.normalized, finalRay.magnitude)));
        camPos = pivot + finalRay.normalized * safe;
        transform.position = camPos;
        transform.LookAt(new Vector3(pivot.x, pivot.y - 0.1f - lift * 0.4f, pivot.z));
    }

    public void SetFadeActive(bool on) { fadeActive = on && enabled; BroadcastFade(); }
    void LateUpdate() => BroadcastFade();
    void BroadcastFade()
    {
        var camPos = transform.position;
        Shader.SetGlobalVector("_TideCamFade", new Vector4(camPos.x, camPos.y, camPos.z, fadeActive ? 1f : 0f));
    }
    void OnDisable() { fadeActive = false; BroadcastFade(); }
    public void DebugOrbit(float radians, float pitchDegrees = 14f)
    {
        yaw = yawGoal = radians; pitch = pitchGoal = Mathf.Clamp(pitchDegrees * Mathf.Deg2Rad, PitchMin, PitchMax); idleMouse = 0f;
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
