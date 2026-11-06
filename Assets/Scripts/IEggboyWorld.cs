using System.Collections.Generic;
using UnityEngine;

// 角色层只依赖这四个查询，具体是世界用解析式还是读场景，由实现决定。
public interface IEggboyWorld
{
    IReadOnlyList<EggboyCollider> Colliders { get; }
    bool Walkable(float x, float z);
    float GroundAt(float x, float z);
    Vector3 MailboxPos { get; }
    Vector3 SpawnPos { get; }
}
