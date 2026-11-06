using UnityEngine;

// 场景里的阻挡标记，TownWorld 启动时把它转成 EggboyCollider。
public class TownObstacle : MonoBehaviour
{
    public enum Kind { Obstacle, WalkableTop, Water }
    public Kind kind = Kind.Obstacle;
    public float topHeight = 0f;
}
