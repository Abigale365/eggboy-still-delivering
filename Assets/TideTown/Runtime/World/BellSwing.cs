// 2026-11-16 面包店门铃：挂在门边墙上的铸铁挂臂下。Ring() 时铃身绕挂钩前后摆几下（阻尼弹簧），配两声清脆的铃响。
using UnityEngine;

public sealed class BellSwing : MonoBehaviour
{
    Transform bell;
    Vector3 hookLocal = new Vector3(0f, 0.99f, 0f), restPos;
    public Vector3 TouchPoint => transform.TransformPoint(new Vector3(0f, 0.76f, 0f));   // 铃口下沿，手伸过来碰的地方
    Quaternion restRot;
    float angle, speed;
    public int RingCount { get; private set; }

    void Awake()
    {
        bell = transform.Find("A5_GLB");
        if (bell != null) { restRot = bell.localRotation; restPos = bell.localPosition; }
    }

    public void Ring()
    {
        RingCount++;
        speed += 260f;   // 度 / 秒
        ShadowAndChimeSystem.PlayProceduralChime(1567.98f, 0.18f);
        ShadowAndChimeSystem.PlayProceduralChime(2093.0f, 0.12f);
    }

    void Update()
    {
        if (bell == null || (Mathf.Abs(angle) < 0.01f && Mathf.Abs(speed) < 0.01f)) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        speed += (-angle * 90f - speed * 3.2f) * dt;   // 弹簧 + 阻尼
        angle += speed * dt;
        // 先回到静止位置，再绕挂钩、朝墙面法线方向前后摆
        bell.localRotation = restRot;
        bell.localPosition = restPos;
        bell.RotateAround(transform.TransformPoint(hookLocal), transform.right, angle);
    }
}
