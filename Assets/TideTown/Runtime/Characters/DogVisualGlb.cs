using UnityEngine;

public sealed class DogVisualGlb : DogVisual
{
	public Animation animationPlayer;

	private string active;

	private void Start()
	{
		if (!(animationPlayer == null))
		{
			animationPlayer.playAutomatically = false;
			ApplyMotion(0f, 0f, trot: false, 0f, happy: false);
		}
	}

	public override void ApplyMotion(float dt, float speed, bool trot, float wag, bool happy)
	{
		if (animationPlayer == null)
		{
			return;
		}
		string text = ((speed < 0.1f) ? "idle" : (trot ? "trot" : "walk"));
		AnimationState animationState = animationPlayer[text];
		if (!(animationState == null))
		{
			animationState.wrapMode = WrapMode.Loop;
			animationState.speed = ((text == "idle") ? 1f : Mathf.Clamp(speed / (trot ? 1.55f : 0.65f), 0.65f, 1.5f));
			if (active != text)
			{
				active = text;
				animationPlayer.Play(text);
			}
		}
	}
}
