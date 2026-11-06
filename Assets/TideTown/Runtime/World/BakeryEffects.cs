// WP-11 炉火与烟囱：点火后亮起暖光，烟从烟囱一直飘向山那边。
using UnityEngine;

public sealed class BakeryEffects : MonoBehaviour
{
    public float chimneyTopLocalY = 3.7f;
    // 由 IslandBuilder 指到烘焙好的粒子材质；运行时 Shader.Find 在打包后可能找不到
    public Material ParticleMaterial;

    public bool Lit { get; private set; }
    public bool HasSmoke => smoke != null && smoke.isPlaying;
    public int FireCount => fire != null ? fire.particleCount : 0;
    public int SmokeCount => smoke != null ? smoke.particleCount : 0;
    public bool FireVisible => fire != null && fire.GetComponent<ParticleSystemRenderer>().isVisible;
    public bool SmokeVisible => smoke != null && smoke.GetComponent<ParticleSystemRenderer>().isVisible;

    Light fireLight;
    ParticleSystem fire, smoke;
    float t;

    public void Light()
    {
        if (Lit) return;
        Lit = true;
        BuildFire();
        BuildSmoke();
    }

    void BuildFire()
    {
        var go = new GameObject("OvenFire");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.80f, -0.56f);
        fire = go.AddComponent<ParticleSystem>();
        var main = fire.main;
        main.loop = true;
        main.startLifetime = 0.6f;
        main.startSpeed = 0.4f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.45f), new Color(1f, 0.42f, 0.10f));
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = fire.emission;
        em.rateOverTime = 55f;
        var shape = fire.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.12f;
        shape.angle = 12f;
        var col = fire.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.90f, 0.60f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMat(ParticleMaterial);

        var lightGo = new GameObject("OvenLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.0f, -0.35f);
        fireLight = lightGo.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.62f, 0.28f);
        fireLight.range = 4.5f;
        fireLight.intensity = 2.2f;
        fireLight.shadows = LightShadows.None;

        fire.Play();
        Debug.Log("BAKERYFIRE lit");
    }

    void BuildSmoke()
    {
        var go = new GameObject("ChimneySmoke");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0.22f, chimneyTopLocalY, 0f);
        smoke = go.AddComponent<ParticleSystem>();
        var main = smoke.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 2.4f);
        main.startColor = new Color(0.76f, 0.76f, 0.74f, 0.72f);
        main.maxParticles = 170;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = smoke.emission;
        em.rateOverTime = 7f;
        var shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.18f;
        shape.angle = 8f;

        var vel = smoke.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.65f, -0.35f); // 山那边在西边
        vel.y = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        var size = smoke.sizeOverLifetime;
        size.enabled = true;
        var grow = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.5f, 1.7f), new Keyframe(1f, 3.5f));
        size.size = new ParticleSystem.MinMaxCurve(1f, grow);

        var col = smoke.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.82f, 0.82f, 0.80f), 0f), new GradientColorKey(new Color(0.66f, 0.66f, 0.64f), 1f) },
            new[] { new GradientAlphaKey(0.72f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMat(ParticleMaterial);
        smoke.Play();
    }

    static Material SoftMat(Material baseMat)
    {
        Material mat = baseMat;
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            mat = new Material(sh);
        }
        var tex = SoftCircle();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        return mat;
    }

    static Texture2D soft;
    static Texture2D SoftCircle()
    {
        if (soft != null) return soft;
        const int n = 64;
        soft = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                soft.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
            }
        soft.Apply();
        return soft;
    }

    void Update()
    {
        if (fireLight == null) return;
        t += Time.deltaTime;
        fireLight.intensity = 1.9f + Mathf.PerlinNoise(t * 7f, 0.3f) * 0.9f;
    }
}
