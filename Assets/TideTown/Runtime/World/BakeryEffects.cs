// WP-11 炉火与烟囱：点火后亮起暖光，烟从烟囱一直飘向山那边。
// P2-1：支持划亮火柴后的三段“拉动风箱助燃”微交互，炉火与炊烟逐级升温。
using UnityEngine;

public sealed class BakeryEffects : MonoBehaviour
{
    public Vector3 chimneyLocalPosition = new Vector3(0.22f, 3.7f, 0f);
    // 2026-11-16：火、湿柴白烟、装进去的柴都在炉膛里面。下面三个由 IslandBuilder 量 A1 网格写入：
    //   mouthLocal = 炉门开口中心（开口那一面上）；chamberFloorLocal = 炉膛底板（开口下沿往里 0.14 m）；mouthSize = 开口宽高
    public Vector3 fireLocalPosition = new Vector3(0f, 0.80f, -0.56f);
    public Vector3 mouthLocal = new Vector3(0f, 0.75f, -0.62f);
    public Vector3 chamberFloorLocal = new Vector3(0f, 0.58f, -0.40f);
    public Vector2 mouthSize = new Vector2(0.40f, 0.35f);
    // 由 IslandBuilder 指到烘焙好的粒子材质；运行时 Shader.Find 在打包后可能找不到
    public Material ParticleMaterial;

    public bool Lit { get; private set; }
    public int BellowsStage { get; private set; }
    public bool HasSmoke => smoke != null && smoke.isPlaying;
    public int FireCount => fire != null ? fire.particleCount : 0;
    public int SmokeCount => smoke != null ? smoke.particleCount : 0;
    public bool SmokeVisible => smoke != null && smoke.GetComponent<ParticleSystemRenderer>().isVisible;
    // 火焰发射点：炉膛里、柴捆上（原来在炉门外 5 cm 的半空中）
    public Vector3 FireMouth => transform.TransformPoint(fireLocalPosition);
    // 往炉膛里塞柴时手伸到的点：炉门开口中心
    public Vector3 LoadPoint => transform.TransformPoint(mouthLocal);

    // ---------- 炉膛里的柴捆（OvenLoadedWood）----------
    GameObject loadedWood;
    bool loadedWet;
    public bool HasLoadedWood => loadedWood != null && loadedWood.activeSelf;
    public bool LoadedWoodWet => HasLoadedWood && loadedWet;

    public void SetLoadedWood(bool loaded, bool wet)
    {
        if (!loaded) { if (loadedWood != null) loadedWood.SetActive(false); return; }
        if (loadedWood == null || loadedWet != wet)
        {
            if (loadedWood != null) Destroy(loadedWood);
            loadedWood = BuildLoadedWood(wet);
            loadedWet = wet;
        }
        if (loadedWood != null) loadedWood.SetActive(true);
    }

    GameObject BuildLoadedWood(bool wet)
    {
        var world = FindObjectOfType<IslandWorld>();
        var src = world != null ? world.woodModel : null;
        if (src == null) return null;
        var go = Instantiate(src, transform, false);
        go.name = "OvenLoadedWood";
        go.SetActive(true);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        // 抱起柴捆后，院里的原件会隐藏。炉内副本需要恢复自己的渲染。
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
        go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
        go.transform.localScale = Vector3.one;
        var bounds = HandCarry.LocalBounds(go, transform);
        float widthFit = mouthSize.x * 0.60f / Mathf.Max(0.01f, bounds.size.x);
        float heightFit = mouthSize.y * 0.55f / Mathf.Max(0.01f, bounds.size.y);
        float depthFit = mouthSize.x * 0.95f / Mathf.Max(0.01f, bounds.size.z);
        float fit = Mathf.Min(1f, Mathf.Min(widthFit, Mathf.Min(heightFit, depthFit)));
        // 让柴捆靠前的一端也越过炉门，另一端顺着炉膛进深方向放进去。
        var front = chamberFloorLocal + Vector3.up * 0.008f;
        front.z = mouthLocal.z + Mathf.Clamp(mouthSize.x * 0.07f, 0.015f, 0.035f);
        go.transform.localScale = Vector3.one * fit;
        go.transform.localPosition = front - new Vector3(bounds.center.x, bounds.min.y, bounds.min.z) * fit;
        if (wet)
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var m in mats)
                    foreach (var p in new[] { "_BaseColor", "_Color" })
                        if (m.HasProperty(p)) m.SetColor(p, m.GetColor(p) * new Color(0.48f, 0.50f, 0.56f, 1f));   // 湿柴：发暗、偏冷
                r.materials = mats;
            }
        return go;
    }

    public bool FireVisibleFrom(Camera camera)
    {
        if (!Lit || FireCount == 0 || !fireRenderer.isVisible) return false;
        var point = camera.WorldToViewportPoint(FireMouth);
        return point.z > camera.nearClipPlane && point.x > 0f && point.x < 1f && point.y > 0f && point.y < 1f;
    }

    Light fireLight;
    ParticleSystem fire, smoke;
    ParticleSystemRenderer fireRenderer;
    float t;
    float boostPulse;

    public void Light()
    {
        if (Lit) return;
        Lit = true;
        BellowsStage = 0;
        BuildFire();
        BuildSmoke();
    }

    public int BoostBellows()
    {
        if (!Lit) Light();
        if (BellowsStage < 3) BellowsStage++;
        boostPulse = 1.25f;
        if (fire != null)
        {
            var em = fire.emission;
            em.rateOverTime = 55f + BellowsStage * 32f;
            fire.Emit(18 + BellowsStage * 8);
        }
        if (smoke != null)
        {
            var em = smoke.emission;
            em.rateOverTime = 7f + BellowsStage * 3.5f;
            smoke.Emit(6 + BellowsStage * 4);
        }
        if (fireLight != null)
        {
            fireLight.range = 4.5f + BellowsStage * 1.4f;
        }
        return BellowsStage;
    }

    // 湿柴点不着：炉口冒一股白烟就散了
    ParticleSystem wetPuff;
    public int WetPuffCount { get; private set; }
    public void WetPuff()
    {
        WetPuffCount++;
        if (wetPuff == null)
        {
            var go = new GameObject("OvenWetPuff");
            go.transform.SetParent(transform, false);
            // 白烟从炉膛里的湿柴上冒出来，再从炉门飘出去
            go.transform.localPosition = fireLocalPosition + new Vector3(0f, 0.04f, 0f);
            go.transform.localRotation = Quaternion.Euler(60f, 0f, 0f) * Quaternion.LookRotation(Vector3.back);   // 朝炉门外、往上 60°
            wetPuff = go.AddComponent<ParticleSystem>();
            var main = wetPuff.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = new Color(0.93f, 0.94f, 0.95f, 0.8f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = wetPuff.emission;
            em.rateOverTime = 0f;
            var shape = wetPuff.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.14f;
            shape.angle = 28f;
            var size = wetPuff.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));
            var col = wetPuff.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.82f, 0.84f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMat(ParticleMaterial);
        }
        wetPuff.Emit(26);
    }

    void BuildFire()
    {
        var go = new GameObject("OvenFire");
        go.transform.SetParent(transform, false);
        // 2026-11-16：发射口在炉膛里的柴捆上（不再在炉门外），火舌往上舔到炉膛顶；从炉门开口看得见
        go.transform.position = FireMouth;
        fire = go.AddComponent<ParticleSystem>();
        var main = fire.main;
        main.loop = true;
        float roof = Mathf.Max(0.12f, mouthLocal.y + mouthSize.y * 0.5f - fireLocalPosition.y);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.50f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(roof * 0.9f, roof * 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(mouthSize.x * 0.35f, mouthSize.x * 0.6f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.45f), new Color(1f, 0.42f, 0.10f));
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = fire.emission;
        em.rateOverTime = 55f;
        var shape = fire.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = Mathf.Min(0.10f, mouthSize.x * 0.28f);
        shape.angle = 10f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var col = fire.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.90f, 0.60f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        fireRenderer = go.GetComponent<ParticleSystemRenderer>();
        fireRenderer.sharedMaterial = SoftMat(ParticleMaterial);

        var lightGo = new GameObject("OvenLight");
        lightGo.transform.SetParent(transform, false);
        // 点光放在炉门开口外一点：暖光从开口溢到门前的路面上（不投影，放在炉膛里也会穿墙照到外面，放门口更像从门里漏出来）
        lightGo.transform.localPosition = mouthLocal + Vector3.back * 0.18f;
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
        go.transform.localPosition = chimneyLocalPosition;
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
        if (boostPulse > 0f) boostPulse = Mathf.Max(0f, boostPulse - Time.deltaTime);
        float baseIntensity = 1.9f + BellowsStage * 0.45f + boostPulse * 1.1f;
        fireLight.intensity = baseIntensity + Mathf.PerlinNoise(t * 7f, 0.3f) * 0.9f;
    }
}
