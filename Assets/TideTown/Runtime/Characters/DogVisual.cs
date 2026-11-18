using UnityEngine;

public abstract class DogVisual : MonoBehaviour
{
	public abstract void ApplyMotion(float dt, float speed, bool trot, float wag, bool happy);
}
