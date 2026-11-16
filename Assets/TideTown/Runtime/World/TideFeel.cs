// 2026-11-14 经过花丛、草地、水面时的反应（用户要求：参考《塞尔达》《沙贝》《原神》《风之旅人》）。
//
// 一、痕迹图（Resources/TideTrail.shader）
//   一张 256×256、盖 25.6 米见方、首尾相接铺满世界的贴图。EGGBOY 和小黑每帧往上盖印：
//   身体两侧的草木往外分，脚下和正前方的顺着走的方向倒；松开后花丛像弹簧一样晃回去，草慢慢立起来。
//   TideFoliage / TideGrass 的顶点着色器读它（全局量 _TideTrailSpring、_TideTrailAux、_TideTrail）。
//   取代 2026-11-13 的"最近 6 次蹭过"数组（跑起来一秒就被覆盖，还在晃的枝叶会突然停）。
//
// 二、水面（Water.shader 读 _TideRipple* / _TideWade）
//   站在没过脚的水里：脚边一圈水线；每落一步一圈涟漪 + 几颗水珠；站着不动隔两秒荡一圈；
//   跳进水里是一大圈 + 一蓬水花。水里走得慢（EggboyPlayer.speedScale）。
//   能涉多深由 IslandWorld.WadeDepth 定。
//
// 三、花瓣和碎叶：穿过花丛时带起几片，颜色取自那一丛花。
//
// 命令行 -nofeel 整套关掉（对比用）。不改步态、姿态、防穿模的任何数值，只读它们的结果。
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TideFeel : MonoBehaviour
{
    public static TideFeel Main { get; private set; }

    public const int Res = 256;
    public const float Size = 25.6f;      // 一张痕迹图盖多少米（0.1 米一格）
    const int MaxActors = 4, MaxRipples = 16;
    const float HeroRadius = 0.62f, DogRadius = 0.42f;

    // 给演示和审计看的记录
    public bool TrailOn { get; private set; }
    public string TrailFormat { get; private set; } = "none";
    public int Ripples { get; private set; }
    public int Splashes { get; private set; }
    public int Petals { get; private set; }
    public int Slams { get; private set; }
    public float HeroDepth { get; private set; }     // 主角脚下的水有多深（米，不在水里是 0）
    public int BushCount => bushes != null ? bushes.Count : 0;
    public bool HeroInBush { get; private set; }

    class Actor
    {
        public Transform t;
        public float radius, bodyR;
        public Vector3 last, vel;
        public bool has, wet;
        public float press = 1f, slam, travel, idleRing;
    }

    class Bush { public Vector3 pos; public float radius; public Color flower, leaf; }

    static readonly int IdTrailSize = Shader.PropertyToID("_TrailSize");
    static readonly int IdTrailDt = Shader.PropertyToID("_TrailDt");
    static readonly int IdActorPos = Shader.PropertyToID("_ActorPos");
    static readonly int IdActorVel = Shader.PropertyToID("_ActorVel");
    static readonly int IdSpring = Shader.PropertyToID("_TideTrailSpring");
    static readonly int IdAux = Shader.PropertyToID("_TideTrailAux");
    static readonly int IdTrail = Shader.PropertyToID("_TideTrail");
    static readonly int IdRipple = Shader.PropertyToID("_TideRipple");
    static readonly int IdRippleB = Shader.PropertyToID("_TideRippleB");
    static readonly int IdRippleFocus = Shader.PropertyToID("_TideRippleFocus");
    static readonly int IdWade = Shader.PropertyToID("_TideWade");
    static readonly int IdTime = Shader.PropertyToID("_TideTime");

    bool off;
    Material trailMat;
    readonly RenderTexture[] spring = new RenderTexture[2];
    readonly RenderTexture[] aux = new RenderTexture[2];
    int cur;

    readonly Actor hero = new Actor { radius = HeroRadius, bodyR = 0.21f };
    readonly Actor dog = new Actor { radius = DogRadius, bodyR = 0.16f };
    EggboyPlayer player;
    DogFollower dogFollower;
    float lookAt;
    bool wasGrounded = true;
    readonly bool[] wasStance = { true, true };
    float lastStepRing = -9f;

    readonly Vector4[] actorPos = new Vector4[MaxActors];
    readonly Vector4[] actorVel = new Vector4[MaxActors];
    readonly Vector4[] ripple = new Vector4[MaxRipples];
    readonly Vector4[] rippleB = new Vector4[MaxRipples];
    readonly Vector4[] wade = new Vector4[MaxActors];
    int rippleNext;
    float rippleUntil = -9f;

    ParticleSystem drops, petals;
    List<Bush> bushes;
    float petalDebt;
    bool wasInBush;
    System.Random rnd = new System.Random(20261114);

    void Awake()
    {
        Main = this;
        off = Array.IndexOf(Environment.GetCommandLineArgs(), "-nofeel") >= 0;
        for (int i = 0; i < MaxRipples; i++) { ripple[i] = new Vector4(0f, 0f, -999f, 0f); rippleB[i] = new Vector4(1f, 0f, 0f, 0f); }
        PushWaterGlobals(Vector3.zero);
        Shader.SetGlobalVector(IdTrail, Vector4.zero);
        if (off) { Debug.Log("FEEL off (-nofeel)"); return; }
        SetupTrail();
        drops = MakeParticles("FeelDrops", 0f, 1.7f, 160);
        petals = MakeParticles("FeelPetals", 1f, 0.32f, 120);
    }

    void OnDestroy()
    {
        if (Main == this) Main = null;
        Shader.SetGlobalVector(IdTrail, Vector4.zero);
        for (int i = 0; i < 2; i++)
        {
            if (spring[i] != null) spring[i].Release();
            if (aux[i] != null) aux[i].Release();
        }
    }

    // ---------- 痕迹图 ----------

    void SetupTrail()
    {
        var shader = Resources.Load<Shader>("TideTrail");
        RenderTextureFormat fmt;
        if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) fmt = RenderTextureFormat.ARGBHalf;
        else if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)) fmt = RenderTextureFormat.ARGBFloat;
        else { Debug.LogWarning("FEEL 这台机器不支持浮点渲染贴图，花草的痕迹关掉"); return; }
        if (shader == null || !shader.isSupported) { Debug.LogWarning("FEEL 找不到 TideTrail 着色器，花草的痕迹关掉"); return; }
        trailMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        for (int i = 0; i < 2; i++)
        {
            spring[i] = NewState("TideTrailSpring" + i, fmt);
            aux[i] = NewState("TideTrailAux" + i, fmt);
        }
        TrailFormat = fmt.ToString();
        TrailOn = true;
        Debug.Log($"FEEL trail {Res}x{Res} {fmt} size={Size}m");
    }

    static RenderTexture NewState(string name, RenderTextureFormat fmt)
    {
        var rt = new RenderTexture(Res, Res, 0, fmt, RenderTextureReadWrite.Linear)
        {
            name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, useMipMap = false, autoGenerateMips = false,
        };
        rt.Create();
        Clear(rt);
        return rt;
    }

    static void Clear(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = prev;
    }

    void StepTrail(float dt, Vector3 focus)
    {
        if (!TrailOn) return;
        for (int i = 0; i < 2; i++)
        {
            // 切窗口 / 设备丢失后贴图内容会没：重新清零，不然读到垃圾值草会乱飞
            if (!spring[i].IsCreated()) { spring[i].Create(); Clear(spring[i]); }
            if (!aux[i].IsCreated()) { aux[i].Create(); Clear(aux[i]); }
        }
        trailMat.SetFloat(IdTrailSize, Size);
        trailMat.SetFloat(IdTrailDt, dt);
        trailMat.SetVectorArray(IdActorPos, actorPos);
        trailMat.SetVectorArray(IdActorVel, actorVel);
        var prev = RenderTexture.active;
        Graphics.Blit(spring[cur], spring[1 - cur], trailMat, 0);
        Graphics.Blit(aux[cur], aux[1 - cur], trailMat, 1);
        RenderTexture.active = prev;
        cur = 1 - cur;
        Shader.SetGlobalTexture(IdSpring, spring[cur]);
        Shader.SetGlobalTexture(IdAux, aux[cur]);
        Shader.SetGlobalVector(IdTrail, new Vector4(1f / Size, focus.x, focus.z, 1f));
    }

    public RenderTexture SpringTexture => TrailOn ? spring[cur] : null;
    public RenderTexture SlowTexture => TrailOn ? aux[cur] : null;

    // 读痕迹图在某个世界位置那一格的前两个分量（演示 / 审计核对盖印位置用，平时不调）
    Texture2D probeTex;
    public Vector2 Probe(RenderTexture rt, Vector3 world)
    {
        if (rt == null) return Vector2.zero;
        if (probeTex == null) probeTex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        int x = Mathf.FloorToInt(Mathf.Repeat(world.x / Size, 1f) * Res) % Res;
        int y = Mathf.FloorToInt(Mathf.Repeat(world.z / Size, 1f) * Res) % Res;
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        probeTex.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
        probeTex.Apply();
        RenderTexture.active = prev;
        var c = probeTex.GetPixel(0, 0);
        return new Vector2(c.r, c.g);
    }

    // ---------- 每帧 ----------

    void LateUpdate()
    {
        Shader.SetGlobalFloat(IdTime, Time.time);
        if (off) return;
        FindActors();
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;   // 暂停时不动

        bool heroOn = hero.t != null && hero.t.gameObject.activeInHierarchy;
        bool dogOn = dog.t != null && dog.t.gameObject.activeInHierarchy && (dogFollower == null || dogFollower.Visible);
        Track(hero, heroOn, dt);
        Track(dog, dogOn, dt);

        // 主角：跳起来就不压着草了；落地那一下往外震一圈
        hero.slam = 0f;
        bool grounded = player == null || player.grounded;
        if (heroOn)
        {
            hero.press = Mathf.MoveTowards(hero.press, grounded ? 1f : 0f, dt * 8f);
            if (grounded && !wasGrounded && player != null)
            {
                hero.slam = Mathf.Clamp(player.jump.impact / 4f, 0.45f, 1.3f);
                Slams++;
            }
        }
        dog.press = 0.7f;
        dog.slam = 0f;

        for (int i = 0; i < MaxActors; i++) { actorPos[i] = Vector4.zero; actorVel[i] = Vector4.zero; }
        if (heroOn) Fill(0, hero);
        if (dogOn) Fill(1, dog);
        Vector3 focus = heroOn ? hero.last : (dogOn ? dog.last : Vector3.zero);
        StepTrail(dt, focus);

        Water(heroOn, dogOn, grounded, dt);
        if (heroOn) Flora(grounded, dt);
        PushWaterGlobals(focus);
        wasGrounded = grounded;
    }

    void FindActors()
    {
        if ((hero.t != null && dog.t != null) || Time.unscaledTime < lookAt) return;
        lookAt = Time.unscaledTime + 1f;
        if (hero.t == null)
        {
            var h = GameObject.Find("EGGBOY");
            if (h != null) { hero.t = h.transform; player = h.GetComponent<EggboyPlayer>(); hero.has = false; }
        }
        if (dog.t == null)
        {
            dogFollower = FindObjectOfType<DogFollower>();
            if (dogFollower != null) { dog.t = dogFollower.transform; dog.has = false; }
        }
    }

    static void Track(Actor a, bool on, float dt)
    {
        if (!on) { a.has = false; a.vel = Vector3.zero; return; }
        Vector3 p = a.t.position;
        // 被传送（救援、过场摆位）不算走过去的
        if (!a.has || (p - a.last).sqrMagnitude > 4f) { a.vel = Vector3.zero; a.has = true; a.travel = 0f; }
        else
        {
            Vector3 step = p - a.last;
            a.vel = Vector3.Lerp(a.vel, step / dt, Mathf.Min(1f, dt * 18f));
            a.travel += new Vector2(step.x, step.z).magnitude;
        }
        a.last = p;
    }

    void Fill(int i, Actor a)
    {
        actorPos[i] = new Vector4(a.last.x, a.last.y, a.last.z, a.radius);
        actorVel[i] = new Vector4(a.vel.x, a.vel.z, a.press, a.slam);
    }

    // ---------- 水 ----------

    void Water(bool heroOn, bool dogOn, bool grounded, float dt)
    {
        float waterY = WaterLevel.StaticY;
        for (int i = 0; i < MaxActors; i++) wade[i] = Vector4.zero;
        HeroDepth = 0f;

        if (heroOn)
        {
            float depth = waterY - hero.last.y;
            bool wet = depth > 0.02f && depth < 0.7f;
            float w = wet ? Mathf.Clamp01(depth / 0.15f) : 0f;
            float speed = new Vector2(hero.vel.x, hero.vel.z).magnitude;
            if (wet && grounded) HeroDepth = depth;
            if (player != null) player.speedScale = Mathf.MoveTowards(player.speedScale, wet && grounded ? Mathf.Lerp(1f, 0.74f, w) : 1f, dt * 2.5f);

            if (wet)
            {
                var p = hero.last;
                // 脚边的水线：走得越快越明显
                if (grounded) wade[0] = new Vector4(p.x, p.z, hero.bodyR, w * (0.55f + 0.45f * Mathf.Clamp01(speed / 1.2f)));

                if (grounded && !wasGrounded)
                {
                    // 跳进水里：一大圈，后面跟一圈小的，一蓬水花
                    float hit = player != null ? Mathf.Clamp(player.jump.impact / 4f, 0.6f, 1.3f) : 1f;
                    AddRipple(p, 1.25f * hit, 2.1f, 0.85f, 0.12f, 0f);
                    AddRipple(p, 0.7f * hit, 1.9f, 0.6f, 0.08f, 0.22f);
                    Splash(p, waterY, 14 + Mathf.RoundToInt(14f * hit), 1.5f * hit, hero.vel);
                    hero.idleRing = 2.4f;
                }
                else if (!grounded && wasGrounded)
                {
                    Splash(p, waterY, 7, 0.9f, hero.vel);   // 起跳带起一点水
                    AddRipple(p, 0.55f, 1.3f, 0.5f, 0.15f, 0f);
                }
                else if (grounded && !hero.wet)
                {
                    AddRipple(p, 0.7f, 1.5f, 0.55f, 0.15f, 0f);   // 刚踏进水里
                    Splash(p, waterY, 6, 0.8f, hero.vel);
                }

                // 每落一步一圈；跑起来步子太密，隔 0.2 秒才给下一圈
                if (grounded && player != null && player.gait != null)
                {
                    for (int f = 0; f < 2; f++)
                    {
                        bool stance = player.gait.feet[f].stance;
                        if (stance && !wasStance[f] && speed > 0.2f && Time.time - lastStepRing > 0.2f)
                        {
                            lastStepRing = Time.time;
                            float run = Mathf.Clamp01((speed - 0.6f) / 1.2f);
                            Vector3 foot = p + hero.t.right * (f == 0 ? -0.09f : 0.09f) + hero.t.forward * player.gait.feet[f].fwd;
                            AddRipple(foot, 0.8f + 0.3f * run, 1.6f, 0.5f + 0.2f * run, 0.05f, 0f);
                            Splash(foot, waterY, 3 + Mathf.RoundToInt(5f * run), 0.55f + 0.5f * run, hero.vel);
                            hero.idleRing = 1.6f;
                        }
                        wasStance[f] = stance;
                    }
                }

                // 站着不动：隔一会儿荡开一圈（身体在呼吸、在晃）
                if (grounded && speed < 0.15f)
                {
                    hero.idleRing -= dt;
                    if (hero.idleRing <= 0f)
                    {
                        hero.idleRing = 1.7f + (float)rnd.NextDouble() * 0.8f;
                        AddRipple(p, 0.6f, 2.4f, 0.32f, hero.bodyR, 0f);
                    }
                }
            }
            hero.wet = wet;
        }
        else if (player != null) player.speedScale = 1f;

        if (dogOn)
        {
            float depth = waterY - dog.last.y;
            bool wet = depth > 0.02f && depth < 0.7f;
            if (wet)
            {
                float speed = new Vector2(dog.vel.x, dog.vel.z).magnitude;
                wade[1] = new Vector4(dog.last.x, dog.last.z, dog.bodyR, Mathf.Clamp01(depth / 0.12f) * (0.5f + 0.5f * Mathf.Clamp01(speed / 1.5f)));
                if (!dog.wet) dog.travel = 0.3f;
                if (dog.travel >= 0.3f)
                {
                    dog.travel = 0f;
                    AddRipple(dog.last, 0.45f, 1.1f, 0.5f, 0.05f, 0f);
                    Splash(dog.last, waterY, 2, 0.5f, dog.vel);
                }
            }
            dog.wet = wet;
        }
    }

    // 一圈涟漪：位置、力度、寿命（秒）、扩散速度（米/秒）、起始半径、晚多久开始
    public void AddRipple(Vector3 at, float strength, float life, float speed, float startRadius, float delay)
    {
        float t0 = Time.time + delay;
        ripple[rippleNext] = new Vector4(at.x, at.z, t0, strength);
        rippleB[rippleNext] = new Vector4(life, speed, startRadius, 0f);
        rippleNext = (rippleNext + 1) % MaxRipples;
        rippleUntil = Mathf.Max(rippleUntil, t0 + life);
        Ripples++;
    }

    void PushWaterGlobals(Vector3 focus)
    {
        bool any = Time.time < rippleUntil;
        for (int i = 0; i < MaxActors && !any; i++) any = wade[i].w > 0f;
        Shader.SetGlobalVectorArray(IdRipple, ripple);
        Shader.SetGlobalVectorArray(IdRippleB, rippleB);
        Shader.SetGlobalVectorArray(IdWade, wade);
        Shader.SetGlobalVector(IdRippleFocus, new Vector4(focus.x, focus.z, 16f, any ? 1f : 0f));
    }

    // ---------- 粒子 ----------

    ParticleSystem MakeParticles(string name, float shape, float gravity, int max)
    {
        var shader = Resources.Load<Shader>("TideFeelParticle");
        if (shader == null) { Debug.LogWarning("FEEL 找不到 TideFeelParticle 着色器，" + name + " 不出"); return null; }
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.gravityModifier = gravity;
        main.startSpeed = 0f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        if (shape > 0.5f)
        {
            // 花瓣：飘，不是砸下来；边落边转
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.drag = 1.6f; lim.limit = 6f; lim.dampen = 0f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3.2f, 3.2f);
        }
        else
        {
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.45f)));
        }
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        r.sharedMaterial.SetFloat("_Shape", shape);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.StableRandomX,
        });
        ps.Play();
        return ps;
    }

    float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    void Splash(Vector3 at, float waterY, int count, float power, Vector3 carry)
    {
        if (drops == null) return;
        Splashes++;
        for (int i = 0; i < count; i++)
        {
            float ang = R(0f, Mathf.PI * 2f), outward = R(0.25f, 0.95f) * power;
            var e = new ParticleSystem.EmitParams
            {
                position = new Vector3(at.x + Mathf.Cos(ang) * 0.06f, waterY + 0.02f, at.z + Mathf.Sin(ang) * 0.06f),
                velocity = new Vector3(Mathf.Cos(ang) * outward + carry.x * 0.35f, R(1.1f, 2.3f) * power, Mathf.Sin(ang) * outward + carry.z * 0.35f),
                startSize = R(0.022f, 0.05f) * Mathf.Lerp(0.9f, 1.25f, Mathf.Clamp01(power - 0.5f)),
                startLifetime = R(0.3f, 0.55f),
                startColor = new Color(0.86f, 0.97f, 1f, 0.92f),
            };
            drops.Emit(e, 1);
        }
    }

    // ---------- 花瓣 ----------

    void BuildBushes()
    {
        bushes = new List<Bush>();
        var root = GameObject.Find("Bushes");
        if (root == null) { Debug.Log("FEEL 场景里没有 Bushes，不出花瓣"); return; }
        foreach (Transform t in root.transform)
        {
            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var mats = mr.sharedMaterials;
            var b = new Bush
            {
                pos = t.position,
                radius = 0.55f * Mathf.Max(t.lossyScale.x, t.lossyScale.z),
                leaf = new Color(0.36f, 0.6f, 0.22f),
                flower = new Color(0.98f, 0.96f, 0.9f),
            };
            if (mats.Length > 0 && mats[0] != null && mats[0].HasProperty("_BaseColor")) b.leaf = mats[0].GetColor("_BaseColor");
            if (mats.Length > 1 && mats[1] != null && mats[1].HasProperty("_BaseColor")) b.flower = mats[1].GetColor("_BaseColor");
            bushes.Add(b);
        }
        Debug.Log($"FEEL bushes {bushes.Count}");
    }

    void Flora(bool grounded, float dt)
    {
        if (petals == null) return;
        if (bushes == null) BuildBushes();
        var p = hero.last;
        Bush inside = null;
        float best = float.MaxValue;
        for (int i = 0; i < bushes.Count; i++)
        {
            var b = bushes[i];
            float dx = p.x - b.pos.x, dz = p.z - b.pos.z;
            float d2 = dx * dx + dz * dz, reach = b.radius + 0.22f;
            if (d2 > reach * reach || Mathf.Abs(p.y - b.pos.y) > 1.0f) continue;
            if (d2 < best) { best = d2; inside = b; }
        }
        HeroInBush = inside != null;
        if (inside != null)
        {
            float speed = new Vector2(hero.vel.x, hero.vel.z).magnitude;
            if (!wasInBush && speed > 0.2f) EmitPetals(inside, p, 5);           // 一头扎进去
            if (hero.slam > 0f) EmitPetals(inside, p, 10);                     // 跳进花丛里
            if (grounded && speed > 0.25f)
            {
                petalDebt += (3.5f + 6f * Mathf.Clamp01(speed / 2f)) * dt;
                int n = Mathf.FloorToInt(petalDebt);
                if (n > 0) { petalDebt -= n; EmitPetals(inside, p, n); }
            }
        }
        wasInBush = inside != null;
    }

    void EmitPetals(Bush b, Vector3 heroPos, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = R(0f, Mathf.PI * 2f), rad = R(0.1f, 0.4f);
            // 从身体蹭到的那一侧冒出来：偏向花丛中心
            Vector3 toBush = new Vector3(b.pos.x - heroPos.x, 0f, b.pos.z - heroPos.z) * 0.35f;
            Color c = rnd.NextDouble() < 0.7 ? b.flower : b.leaf;
            float tint = R(0.9f, 1.08f);
            var e = new ParticleSystem.EmitParams
            {
                position = heroPos + toBush + new Vector3(Mathf.Cos(ang) * rad, R(0.22f, 0.62f), Mathf.Sin(ang) * rad),
                velocity = new Vector3(hero.vel.x * 0.45f + R(-0.55f, 0.55f), R(0.5f, 1.35f), hero.vel.z * 0.45f + R(-0.55f, 0.55f)),
                startSize = R(0.05f, 0.085f),
                startLifetime = R(1.2f, 2.0f),
                startColor = new Color(c.r * tint, c.g * tint, c.b * tint, 1f),
                rotation = R(0f, 360f),
            };
            petals.Emit(e, 1);
            Petals++;
        }
    }
}
