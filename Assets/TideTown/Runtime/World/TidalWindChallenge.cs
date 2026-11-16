// 方向 B：海风重心（工程书 INNOVATION_BOOK_20261110 的 WP-B）。
// 2026-11-11 改版：
//   · 只在两段迎风的海边路上刮（低洼路到船棚、去灯塔的海岸路），风从海那边往陆地吹；别处不刮。
//   · 风来之前 1.5 秒：海面方向扫过来一道道白色风线、风声变大、EGGBOY 换紧张脸。没有横幅、没有进度条。
//   · 按住 Shift = 蹲低顶风（顺带也慢下来），扛过这阵风有 3 秒顺风冲刺（身后拖着风线）。
//   · 没扛住：踉跄退两步，手上的东西掉在脚边（掉进水里就漂回最近的岸边），走过去按 E 捡起来就行。
//     剧情状态照旧退回"去拿它"那一步，读档后东西回到原处，不会卡关。
using System.Collections.Generic;
using UnityEngine;

public sealed class TidalWindChallenge : MonoBehaviour
{
    sealed class Zone { public string a, b; public Vector3 p0, p1; public Vector3 wind; public float radius = 3.2f; }

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    GameState state;
    StoryDirector director;
    PrototypeGame game;

    readonly List<Zone> zones = new List<Zone>();
    Zone currentZone;
    float zoneTime, cooldown, warnTimer, gustTimer, boostTimer;
    const float WarnSeconds = 1.5f, GustSeconds = 3.2f, BoostSeconds = 3f;
    public float Imbalance { get; private set; }
    public bool DebugBrace;
    public static bool Suppress; // 创新审计测别的方向时先关掉风
    public string Phase => warnTimer > 0f ? "warn" : gustTimer > 0f ? "gust" : boostTimer > 0f ? "boost" : "calm";
    public bool InWindZone => currentZone != null;
    public bool IsGustActive => !Suppress && !Features.classicAudit && currentZone != null && (warnTimer > 0f || gustTimer > 0f);
    public Vector3 WindDir { get; private set; } = Vector3.right;
    public int Failures { get; private set; }
    public int Survived { get; private set; }

    ParticleSystem streaks, trail;
    AudioSource windAudio;
    bool crouchedByWind;
    ExprId expressionBeforeWind;
    ExprId? windExpression;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
    {
        player = p; ui = u; world = w; state = s;
        director = FindObjectOfType<StoryDirector>();
        game = FindObjectOfType<PrototypeGame>();
        BuildZones();
    }

    // 风道用路线上的停靠点连成线段；海在哪一边，用两侧地面高度判断（低的那边是海）
    void BuildZones()
    {
        var pts = new Dictionary<string, Vector3>();
        var asset = Resources.Load<TextAsset>("World/route");
        if (asset != null)
        {
            var list = JsonUtility.FromJson<RouteList>(asset.text);
            if (list != null && list.route != null)
                foreach (var r in list.route) if (r.stop && !pts.ContainsKey(r.id)) pts[r.id] = new Vector3(r.x, 0f, r.z);
        }
        void Add(string a, string b)
        {
            if (!pts.ContainsKey(a) || !pts.ContainsKey(b) || world == null) return;
            var z = new Zone { a = a, b = b, p0 = pts[a], p1 = pts[b] };
            Vector3 mid = (z.p0 + z.p1) * 0.5f;
            Vector3 along = (z.p1 - z.p0); along.y = 0f; along.Normalize();
            Vector3 side = new Vector3(-along.z, 0f, along.x);
            float gl = SideHeight(mid + side * 6f), gr = SideHeight(mid - side * 6f);
            // 风从低的那一侧（海）吹向高的那一侧（陆地）
            z.wind = gl < gr ? -side : side;
            zones.Add(z);
        }
        Add("LOW", "24"); Add("24", "25"); Add("22", "30");
    }

    float SideHeight(Vector3 p) => world.InWalkArea(p.x, p.z) ? world.GroundAt(p.x, p.z) : -1f;

    Zone ZoneAt(Vector3 p)
    {
        foreach (var z in zones)
        {
            Vector3 ab = z.p1 - z.p0; ab.y = 0f;
            Vector3 ap = p - z.p0; ap.y = 0f;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            Vector3 c = z.p0 + ab * t;
            if (new Vector2(p.x - c.x, p.z - c.z).magnitude <= z.radius) return z;
        }
        return null;
    }

    bool PlayerFree =>
        ui != null && !ui.TitleShowing && !ui.Paused && !ui.LetterOpen && player != null && !player.locked
        && !ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen && !AvantGardeGameplayHub.AnyModalOpen
        && !AIAgentCalibrationDirector.IsOpen;

    void Update()
    {
        if (player == null || ui == null || world == null) return;
        // 主线自动审计走经典路线，不刮风；真人游玩和创新审计才刮
        if (Features.classicAudit || Suppress) return;
        Tick(Time.deltaTime);
    }

    public void Tick(float dt)
    {
        if (dt <= 0f || dt > 0.2f) return;
        var z = PlayerFree ? ZoneAt(player.transform.position) : null;
        if (z != currentZone)
        {
            currentZone = z;
            zoneTime = 0f;
            cooldown = 2.0f; // 进风道 2 秒后来第一阵
        }
        if (boostTimer > 0f) Boost(dt);
        if (currentZone == null) { EndGust(false); UpdateAudio(dt, 0f); return; }
        zoneTime += dt;
        WindDir = currentZone.wind;

        if (warnTimer > 0f)
        {
            warnTimer -= dt;
            UpdateAudio(dt, 0.35f + 0.4f * (1f - warnTimer / WarnSeconds));
            if (warnTimer <= 0f) { gustTimer = GustSeconds; Imbalance = 0f; }
            return;
        }
        if (gustTimer > 0f) { Gust(dt); return; }

        UpdateAudio(dt, 0.12f);
        // 走动时才来风；站着不动看风景不打扰
        if (player.Speed > 0.2f || !string.IsNullOrEmpty(state.carrying)) cooldown -= dt;
        if (cooldown <= 0f) StartWarning();
    }

    public void DebugStartGust() { if (currentZone != null) StartWarning(); }

    void StartWarning()
    {
        warnTimer = WarnSeconds;
        cooldown = Random.Range(7f, 10f);
        EnsureFx();
        streaks.Play();
        SetWindExpression(ExprId.nervous);
        ShadowAndChimeSystem.PlayProceduralChime(440f, 0.18f);
    }

    void Gust(float dt)
    {
        gustTimer -= dt;
        bool brace = DebugBrace || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            || (HardwareAndSensorHub.Instance != null && HardwareAndSensorHub.Instance.ExternalWindStabilizeBoost > 0.15f);
        // 顶风往前走算一半的力
        Vector3 v = player.vel; v.y = 0f;
        bool counter = v.sqrMagnitude > 0.04f && Vector3.Dot(v.normalized, WindDir) < -0.2f;

        if (brace) Imbalance = Mathf.Max(0f, Imbalance - 0.6f * dt);
        else Imbalance = Mathf.Clamp01(Imbalance + (counter ? 0.28f : 0.55f) * dt);
        SetWindCrouch(brace);
        if (brace) player.vel *= 0.35f; // 蹲着只能挪

        float push = brace ? 0.25f : 1.6f;
        Vector3 next = player.transform.position + WindDir * (push * dt);
        if (world.Walkable(next.x, next.z))
        {
            next.y = world.GroundAt(next.x, next.z, next.y + 0.3f);
            player.transform.position = next;
        }
        UpdateAudio(dt, 1f);
        if (!brace && Imbalance > 0.55f) SetWindExpression(ExprId.scared);

        if (Imbalance >= 1f) { Fail(); return; }
        if (gustTimer <= 0f)
        {
            Survived++;
            EndGust(true);
            boostTimer = BoostSeconds;
            if (game != null) game.SetExpression(ExprId.happy);
            player.PlayEmote("hop");
            ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
            TideTownAchievementSystem.Unlock("tailwind");
            EnsureFx();
            trail.Play();
        }
    }

    void Boost(float dt)
    {
        boostTimer -= dt;
        if (player.Speed > 0.3f && PlayerFree)
        {
            Vector3 v = player.vel; v.y = 0f;
            Vector3 next = player.transform.position + v.normalized * (0.6f * player.Speed * dt);
            if (world.Walkable(next.x, next.z))
            {
                next.y = world.GroundAt(next.x, next.z, next.y + 0.3f);
                player.transform.position = next;
            }
        }
        if (trail != null) trail.transform.position = player.transform.position + Vector3.up * 0.4f;
        if (boostTimer <= 0f && trail != null) trail.Stop();
    }

    void Fail()
    {
        Failures++;
        EndGust(false);
        cooldown = 9f;
        ShadowAndChimeSystem.PlayProceduralChime(185f, 0.45f);
        if (game != null) game.SetExpression(ExprId.scared);
        // 踉跄：顺着风退两步（退不动就少退一点）
        Vector3 p = player.transform.position;
        for (float d = 2.2f; d > 0.3f; d -= 0.4f)
        {
            Vector3 q = p + WindDir * d;
            if (!world.Walkable(q.x, q.z)) continue;
            q.y = world.GroundAt(q.x, q.z, p.y + 0.5f);
            player.transform.position = q;
            break;
        }
        player.vel = Vector3.zero;
        player.PlayEmote("shake");
        // 东西掉在原来站的地方附近，被风往下风处推半米
        if (director != null && !string.IsNullOrEmpty(state.carrying) && state.carrying != "matches")
            director.DropCarriedNear(p + WindDir * 0.6f);
    }

    void EndGust(bool keepBoostFx)
    {
        warnTimer = 0f; gustTimer = 0f; Imbalance = 0f;
        SetWindCrouch(false);
        if (windExpression.HasValue && game != null && player.pose?.expr == windExpression.Value)
            game.SetExpression(expressionBeforeWind);
        windExpression = null;
        if (streaks != null) streaks.Stop();
        if (!keepBoostFx && trail != null && boostTimer <= 0f) trail.Stop();
    }

    void SetWindExpression(ExprId expression)
    {
        if (game == null || player.pose == null) return;
        if (player.pose.expr != windExpression) expressionBeforeWind = player.pose.expr;
        windExpression = expression;
        game.SetExpression(expression);
    }

    void SetWindCrouch(bool on)
    {
        if (on == crouchedByWind) return;
        crouchedByWind = on;
        player.SetStoryCrouch(on);
    }

    // ---- 风线和风声 ----

    void EnsureFx()
    {
        if (streaks != null) return;
        var fx = FindObjectOfType<BakeryEffects>();
        Material mat = fx != null ? fx.ParticleMaterial : null;

        var go = new GameObject("WindStreaks");
        streaks = go.AddComponent<ParticleSystem>();
        var main = streaks.main;
        main.loop = true; main.playOnAwake = false;
        main.startLifetime = 0.9f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.035f);
        main.startColor = new Color(1f, 1f, 1f, 0.38f);
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = streaks.emission; em.rateOverTime = 90f;
        var shape = streaks.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(9f, 2.2f, 1f);
        var vel = streaks.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.12f; r.lengthScale = 2f;
        if (mat != null) r.sharedMaterial = mat;

        var go2 = new GameObject("TailwindTrail");
        trail = go2.AddComponent<ParticleSystem>();
        var m2 = trail.main;
        m2.loop = true; m2.playOnAwake = false;
        m2.startLifetime = 0.6f; m2.startSpeed = 0.2f; m2.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        m2.startColor = new Color(1f, 0.95f, 0.8f, 0.6f);
        m2.simulationSpace = ParticleSystemSimulationSpace.World;
        var e2 = trail.emission; e2.rateOverTime = 40f;
        var s2 = trail.shape; s2.shapeType = ParticleSystemShapeType.Sphere; s2.radius = 0.25f;
        var r2 = go2.GetComponent<ParticleSystemRenderer>();
        if (mat != null) r2.sharedMaterial = mat;
    }

    void LateUpdate()
    {
        if (streaks == null || player == null) return;
        if (streaks.isPlaying)
        {
            // 风线从上风处（海那边）扫过来
            Vector3 up = -WindDir;
            streaks.transform.position = player.transform.position + up * 6f + Vector3.up * 1.0f;
            streaks.transform.rotation = Quaternion.LookRotation(WindDir, Vector3.up);
            var vel = streaks.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(WindDir.x * 13f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(WindDir.z * 13f);
        }
    }

    static AudioClip windClip;
    void UpdateAudio(float dt, float target)
    {
        if (windAudio == null)
        {
            if (target <= 0.01f) return;
            if (windClip == null) windClip = MakeWindClip();
            windAudio = gameObject.AddComponent<AudioSource>();
            windAudio.clip = windClip; windAudio.loop = true; windAudio.volume = 0f; windAudio.spatialBlend = 0f;
            windAudio.Play();
        }
        windAudio.volume = Mathf.MoveTowards(windAudio.volume, target * 0.5f, dt * 0.8f);
        windAudio.pitch = 0.85f + windAudio.volume * 0.5f;
    }

    // 风声：低通过滤的白噪声，音量慢慢起伏
    static AudioClip MakeWindClip()
    {
        const int rate = 22050, len = rate * 4;
        var data = new float[len];
        var rng = new System.Random(7);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < len; i++)
        {
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (n - lp) * 0.05f;
            lp2 += (lp - lp2) * 0.08f;
            float t = (float)i / len;
            float swell = 0.65f + 0.35f * Mathf.Sin(t * Mathf.PI * 2f * 2f) * Mathf.Sin(t * Mathf.PI);
            // 首尾淡入淡出，循环时不爆音
            float edge = Mathf.Clamp01(Mathf.Min(t, 1f - t) * 40f);
            data[i] = lp2 * 6f * swell * edge;
        }
        var clip = AudioClip.Create("WindNoise", len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
