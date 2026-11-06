using System.Collections.Generic;
using UnityEngine;

// 角色层只依赖这四个查询，具体是世界用解析式还是读场景，由实现决定。
public interface IEggboyWorld
{
    IReadOnlyList<EggboyCollider> Colliders { get; }
    bool Walkable(float x, float z);
    float GroundAt(float x, float z);
    // 只认脚下（aboveY 附近及以下）的地面，避免头顶的塔顶平台把地面抬上去
    float GroundAt(float x, float z, float aboveY);
    Vector3 MailboxPos { get; }
    Vector3 SpawnPos { get; }
}
