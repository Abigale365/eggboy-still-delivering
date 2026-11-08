// 2026-11-08 按模型外轮廓生成的碰撞：一串小圆（IslandBuilder.Contour.cs 生成）。
// 每个圆都带顶面高度 top：站得比它高的人不挡（高台上的人不会被下面那层的墙、栏杆挡住）。
// IslandWorld 启动时把它们转成 EggboyCollider（r + top），角色代码不用改。
using UnityEngine;

public sealed class TownObstacleSet : MonoBehaviour
{
    public float[] x = new float[0], z = new float[0], r = new float[0], top = new float[0];
}
