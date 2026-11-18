using System.Collections.Generic;
using UnityEngine;

public interface IEggboyWorld
{
	IReadOnlyList<EggboyCollider> Colliders { get; }

	Vector3 MailboxPos { get; }

	Vector3 SpawnPos { get; }

	void QueryColliders(float x, float z, List<EggboyCollider> results);

	bool Walkable(float x, float z);

	float GroundAt(float x, float z);

	float GroundAt(float x, float z, float aboveY);
}
