// WP-14：灯头的半透明锥体和聚光灯，绕 Y 轴扫过海面。
// P0-2：支持玩家手动摇转灯头对准山那边的新城，并按节奏闪烁灯束发信号。
using UnityEngine;

public sealed class BeaconLight : MonoBehaviour
{
    public Transform pivot;
    public Light spot;
    public Light glow;
    public Renderer beam;
    public float degreesPerSecond = 24f;

    public bool On { get; private set; }
    public bool ManualMode { get; set; }

    float flashTimer;

    void Awake() { SetEnabled(false); }

    public void Face(Vector3 target)
    {
        if (pivot == null) return;
        var direction = target - pivot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
            pivot.rotation = Quaternion.LookRotation(direction);
    }

    public void SetYawOffsetFrom(Vector3 target, float yawOffsetDeg)
    {
        if (pivot == null) return;
        var direction = target - pivot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
            pivot.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, yawOffsetDeg, 0f);
    }

    public void RotateYaw(float deltaDeg)
    {
        if (pivot == null) return;
        pivot.Rotate(0f, deltaDeg, 0f, Space.Self);
    }

    public float AlignmentWith(Vector3 target)
    {
        if (pivot == null) return 1f;
        var dir = target - pivot.position;
        dir.y = 0f;
        var fwd = pivot.forward;
        fwd.y = 0f;
        if (dir.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.01f) return 1f;
        float angle = Vector3.Angle(fwd.normalized, dir.normalized);
        return Mathf.Clamp01(1f - angle / 55f);
    }

    public void PulseFlash()
    {
        flashTimer = 0.65f;
    }

    public void TurnOn() { SetEnabled(true); }

    public void SetEnabled(bool on)
    {
        On = on;
        if (spot != null) spot.enabled = on;
        if (glow != null) glow.enabled = on;
        if (beam != null) beam.enabled = on;
    }

    void Update()
    {
        if (!On || pivot == null) return;
        if (flashTimer > 0f)
        {
            flashTimer = Mathf.Max(0f, flashTimer - Time.unscaledDeltaTime);
            float boost = Mathf.Sin(flashTimer / 0.65f * Mathf.PI);
            if (spot != null) spot.intensity = 2.4f + boost * 4.2f;
            if (glow != null) glow.intensity = 1.4f + boost * 2.5f;
        }
        else
        {
            if (spot != null) spot.intensity = 2.4f;
            if (glow != null) glow.intensity = 1.4f;
        }

        if (!ManualMode)
        {
            pivot.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }
    }
}
