// WP-14：灯头的半透明锥体和聚光灯，绕 Y 轴扫过海面。
using UnityEngine;

public sealed class BeaconLight : MonoBehaviour
{
    public Transform pivot;
    public Light spot;
    public Light glow;
    public Renderer beam;
    public float degreesPerSecond = 24f;

    public bool On { get; private set; }

    void Awake() { SetEnabled(false); }

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
        pivot.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
