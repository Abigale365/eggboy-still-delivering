using UnityEngine;

public class TownObstacle : MonoBehaviour
{
	public enum Kind
	{
		Obstacle,
		WalkableTop,
		Water
	}

	public Kind kind;

	public float topHeight;

	public float minY;
}
