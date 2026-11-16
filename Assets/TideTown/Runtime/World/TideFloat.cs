// 2026-11-16 地图扩建 EXP-06：跟着潮水上下浮的摆件（港口停的蒸汽小渡船）。
// 只改自己的高度：y = 当前水面 + offset，再加一点慢悠悠的起伏和摇晃。不碰碰撞、不参与玩法。
using UnityEngine;

public sealed class TideFloat : MonoBehaviour
{
    public float offset;          // 水面以上多少（船的吃水线位置）
    public float bob = 0.04f;     // 上下起伏幅度
    public float roll = 1.2f;     // 左右摇晃角度
    float phase;
    Quaternion baseRot;

    void Awake()
    {
        phase = (transform.position.x * 0.37f + transform.position.z * 0.21f) % 6.28f;
        baseRot = transform.rotation;
    }

    void LateUpdate()
    {
        float t = Time.time;
        var p = transform.position;
        p.y = WaterLevel.StaticY + offset + Mathf.Sin(t * 0.9f + phase) * bob;
        transform.position = p;
        transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.7f + phase) * roll * 0.4f, 0f, Mathf.Sin(t * 0.8f + phase * 1.3f) * roll);
    }
}
