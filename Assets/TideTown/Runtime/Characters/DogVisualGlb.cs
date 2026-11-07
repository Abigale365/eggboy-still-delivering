using UnityEngine;

public sealed class DogVisualGlb : DogVisual
{
    public Animation animationPlayer;
    string active;

    void Start()
    {
        if (animationPlayer == null) return;
        animationPlayer.playAutomatically = false;
        ApplyMotion(0f, 0f, false, 0f, false);
    }

    public override void ApplyMotion(float dt, float speed, bool trot, float wag, bool happy)
    {
        if (animationPlayer == null) return;
        string clip = speed < 0.10f ? "idle" : trot ? "trot" : "walk";
        var state = animationPlayer[clip];
        if (state == null) return;
        state.wrapMode = WrapMode.Loop;
        state.speed = clip == "idle" ? 1f : Mathf.Clamp(speed / (trot ? 1.55f : 0.65f), 0.65f, 1.5f);
        if (active != clip)
        {
            active = clip;
            animationPlayer.Play(clip);
        }
    }
}
