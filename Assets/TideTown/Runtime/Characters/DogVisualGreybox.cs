// WP-12：模型还没导入时的小黑占位。四腿摆动、身体起伏、尾巴摇摆，开心时头抬一点、尾巴摇得更快。
using UnityEngine;

public class DogVisualGreybox : DogVisual
{
    public Transform body, head, tail, legFL, legFR, legBL, legBR;

    Vector3 bodyBase, headBase;
    float phase;
    float headTilt;

    void Awake()
    {
        if (body != null) bodyBase = body.localPosition;
        if (head != null) headBase = head.localPosition;
    }

    public override void ApplyMotion(float dt, float speed, bool trot, float wag, bool happy)
    {
        float amp = Mathf.Clamp01(speed / 2.2f);
        phase += dt * (2.2f + speed * 4.2f + (trot ? 3.2f : 0f));
        float swing = Mathf.Sin(phase * 2f) * 26f * amp;

        // 对角腿同相，另外两条反相
        SetLeg(legFL, swing, 0f);
        SetLeg(legBR, swing, 0f);
        SetLeg(legFR, -swing, 0f);
        SetLeg(legBL, -swing, 0f);

        if (body != null)
            body.localPosition = bodyBase + Vector3.up * (Mathf.Abs(Mathf.Sin(phase * 2f)) * 0.016f * amp);

        if (head != null)
        {
            head.localPosition = headBase + Vector3.up * (Mathf.Sin(phase * 2f) * 0.008f * amp);
            headTilt = Mathf.Lerp(headTilt, happy ? -8f : 0f, Mathf.Min(1f, dt * 6f));
            head.localRotation = Quaternion.Euler(headTilt, 0f, 0f);
        }

        if (tail != null)
        {
            float ampYaw = (happy ? 36f : 22f) * Mathf.Clamp01(0.5f + wag / 4f);
            float lift = happy ? -18f : -5f;
            tail.localRotation = Quaternion.Euler(lift, Mathf.Sin(Time.time * wag * Mathf.PI * 2f) * ampYaw, 0f);
        }
    }

    static void SetLeg(Transform leg, float angle, float roll)
    {
        if (leg != null) leg.localRotation = Quaternion.Euler(angle, 0f, roll);
    }
}
