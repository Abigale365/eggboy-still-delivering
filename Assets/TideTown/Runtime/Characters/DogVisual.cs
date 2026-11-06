// WP-12：小黑视觉层的预留接口。灰盒版先顶着；以后导入 小黑_绑骨.glb 时，
// 写一个 DogVisualGlb : DogVisual 挂在同一个 XIAOHEI 根节点上，替换灰盒组件即可。
using UnityEngine;

public abstract class DogVisual : MonoBehaviour
{
    // speed：实际水平速度（米/秒）；trot：是否小跑；wag：摆尾频率（Hz）；happy：开心表情
    public abstract void ApplyMotion(float dt, float speed, bool trot, float wag, bool happy);
}
