using UnityEngine;

// 场景里的阻挡标记，TownWorld 启动时把它转成 EggboyCollider。
// minY > 0：只挡站在这个高度以上的人（高台上的物件不挡下面那层路，2026-11-08）
public class TownObstacle : MonoBehaviour
{
    public enum Kind { Obstacle, WalkableTop, Water }
    public Kind kind = Kind.Obstacle;
    public float topHeight = 0f;
    public float minY = 0f;
}
