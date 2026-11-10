// 创新方向 F：潮汐残影（工程书 INNOVATION_BOOK_20261110 的 WP-F）。
// 怀表一直在后台记着最近 10 秒：位置、朝向、速度、是否着地、有没有在某个机关旁按住 E。
// 按 R：一个半透明暖金色的"10 秒前的你"从当时的位置出现，原样重演这 10 秒，然后化掉。
// 残影用的是和真人同一套模型和步态层（EggboyPlayer.PuppetStep），所以会迈腿、摆臂，不是平移的雕像。
// 残影能做的事不写特判：它重演"在某个机关旁按住 E"时，那个机关自己去数有几个人在按（见 HoldZones）。
using System.Collections.Generic;
using UnityEngine;

// 需要"有人按住 E"的机关在这里登记一个范围；录制和残影都只认这个表
public static class HoldZones
{
    struct Zone { public string id; public Vector3 pos; public float radius; }
    static readonly List<Zone> zones = new List<Zone>();

    public static void Register(string id, Vector3 pos, float radius)
    {
        for (int i = 0; i < zones.Count; i++) if (zones[i].id == id) { zones[i] = new Zone { id = id, pos = pos, radius = radius }; return; }
        zones.Add(new Zone { id = id, pos = pos, radius = radius });
    }

    public static string At(Vector3 p)
    {
        foreach (var z in zones)
            if (new Vector2(p.x - z.pos.x, p.z - z.pos.z).magnitude <= z.radius && Mathf.Abs(p.y - z.pos.y) < 2f) return z.id;
        return "";
    }

    public static void Clear() => zones.Clear();
}

public sealed class TidalEchoCloneSystem : MonoBehaviour
{
    public const float WindowSeconds = 10f;
    const float SampleDt = 0.05f;
    const float MinSeconds = 2.5f;      // 攒够这么久才叫得出残影
    const float LingerSeconds = 1.3f;   // 重演完定住一会儿再化掉
    const float CooldownSeconds = 3f;

    struct EchoFrame
    {
        public Vector3 pos, vel;
        public float yaw;
        public bool grounded;
        public string hold;
    }

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;
    PrototypeGame game;

    readonly List<EchoFrame> buffer = new List<EchoFrame>();   // 滚动录制
    readonly List<EchoFrame> replay = new List<EchoFrame>();   // 正在重演的那一段
    float sampleAccum, replayTime, lingerLeft, cooldown;

    EggboyPlayer puppet;
    Light puppetLight;

    public bool EchoActive { get; private set; }
    public Vector3 EchoPosition => puppet != null ? puppet.transform.position : Vector3.zero;
    public string EchoHoldZone { get; private set; } = "";
    public float EchoSpeed => puppet != null ? puppet.Speed : 0f;
    public float BufferedSeconds => buffer.Count * SampleDt;
    public int SummonCount { get; private set; }
    // 兼容旧接口
    public bool IsRecording => false;
    public bool IsReplaying => EchoActive;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        player = p; ui = u; world = w; director = d; state = s;
        game = FindObjectOfType<PrototypeGame>();
    }

    // R 键
    public void TriggerEchoButton()
    {
        if (EchoActive) { Dismiss(); return; }
        if (!Summon())
        {
            // 还叫不出来（刚用过，或者攒的时间不够）：一声低音，不弹字
            ShadowAndChimeSystem.PlayProceduralChime(196f, 0.2f);
        }
    }

    public bool Summon()
    {
        if (player == null || EchoActive || cooldown > 0f || BufferedSeconds < MinSeconds) return false;
        replay.Clear();
        replay.AddRange(buffer);
        replayTime = 0f;
        lingerLeft = LingerSeconds;
        EnsurePuppet();
        if (puppet == null) return false;
        puppet.gameObject.SetActive(true);
        var f0 = replay[0];
        puppet.PuppetStep(f0.pos, f0.yaw, Vector3.zero, true, 0f);
        EchoActive = true;
        EchoHoldZone = f0.hold ?? "";
        SummonCount++;
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.35f);
        ShadowAndChimeSystem.PlayProceduralChime(987.77f, 0.5f);
        if (player.Speed < 0.2f) player.PlayEmote("look");
        return true;
    }

    public void Dismiss()
    {
        if (!EchoActive) return;
        EchoActive = false;
        EchoHoldZone = "";
        cooldown = CooldownSeconds;
        if (puppet != null) puppet.gameObject.SetActive(false);
        ShadowAndChimeSystem.PlayProceduralChime(392f, 0.3f);
    }

    void EnsurePuppet()
    {
        if (puppet != null || player == null) return;
        var prefab = Resources.Load<GameObject>("Models/eggboy");
        if (prefab == null) { Debug.LogError("ECHO 找不到 Resources/Models/eggboy"); return; }
        var go = new GameObject("EGGBOY_TidalEcho");
        go.transform.SetParent(transform, false);
        puppet = go.AddComponent<EggboyPlayer>();
        puppet.Setup(Instantiate(prefab), player.world, game != null ? game.modelYawDegrees : 0f);
        if (puppet.gait == null) { Destroy(go); puppet = null; return; }

        // 暖金色的"潮水做的自己"：沿用 EGGBOY 原来的材质（打包里一定有、受光有明暗），整体染成金色、带一点自发光
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.materials; // 实例副本，不影响真人
            for (int i = 0; i < mats.Length; i++) Ghostify(mats[i]);
            r.materials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        var lightGo = new GameObject("EchoWarmLight");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        puppetLight = lightGo.AddComponent<Light>();
        puppetLight.type = LightType.Point;
        puppetLight.color = new Color(1f, 0.84f, 0.45f);
        puppetLight.range = 3.6f;
        puppetLight.intensity = 1.6f;
        puppetLight.shadows = LightShadows.None;
        go.SetActive(false);
    }

    static readonly Color EchoGold = new Color(1f, 0.78f, 0.36f, 1f);

    static void Ghostify(Material m)
    {
        if (m == null) return;
        m.name = "TidalEchoGhost_" + m.name;
        foreach (var p in new[] { "_BaseColor", "_Color", "baseColorFactor" })
        {
            if (!m.HasProperty(p)) continue;
            var c = m.GetColor(p);
            float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            // 先去色再染金，保留原来的深浅关系（外壳亮、皮包暗）
            var tinted = EchoGold * Mathf.Lerp(0.55f, 1.15f, lum);
            tinted.a = c.a;
            m.SetColor(p, tinted);
        }
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", EchoGold * 0.45f);
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
    }
    void Update()
    {
        if (player == null || ui == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (cooldown > 0f) cooldown -= dt;

        // 真人游玩时滚动录制；自动审计用 DebugPushFrame 喂
        if (ui.inputClose && !ui.Paused && !ui.TitleShowing)
        {
            sampleAccum += dt;
            while (sampleAccum >= SampleDt)
            {
                sampleAccum -= SampleDt;
                bool holding = Input.GetKey(KeyCode.E) && !player.locked && !ui.LetterOpen;
                Push(new EchoFrame
                {
                    pos = player.transform.position, yaw = player.yaw, vel = player.vel, grounded = player.grounded,
                    hold = holding ? HoldZones.At(player.transform.position) : "",
                });
            }
        }
        StepReplay(dt);
    }

    void Push(EchoFrame f)
    {
        buffer.Add(f);
        int max = Mathf.RoundToInt(WindowSeconds / SampleDt);
        if (buffer.Count > max) buffer.RemoveRange(0, buffer.Count - max);
    }

    void StepReplay(float dt)
    {
        if (!EchoActive || puppet == null || replay.Count == 0) return;
        replayTime += dt;
        float total = (replay.Count - 1) * SampleDt;
        if (replayTime >= total)
        {
            // 演完了：停在最后的位置，还保持着最后的动作（比如仍按着机关），过一会儿化掉
            var last = replay[replay.Count - 1];
            puppet.PuppetStep(last.pos, last.yaw, Vector3.zero, last.grounded, dt);
            EchoHoldZone = last.hold ?? "";
            lingerLeft -= dt;
            if (puppetLight != null) puppetLight.intensity = 1.6f * Mathf.Clamp01(lingerLeft / LingerSeconds);
            if (lingerLeft <= 0f) Dismiss();
            return;
        }
        float fi = replayTime / SampleDt;
        int i0 = Mathf.Clamp(Mathf.FloorToInt(fi), 0, replay.Count - 2);
        float k = fi - i0;
        var a = replay[i0]; var b = replay[i0 + 1];
        float yaw = a.yaw + Mathf.Atan2(Mathf.Sin(b.yaw - a.yaw), Mathf.Cos(b.yaw - a.yaw)) * k;
        puppet.PuppetStep(Vector3.Lerp(a.pos, b.pos, k), yaw, Vector3.Lerp(a.vel, b.vel, k), a.grounded, dt);
        EchoHoldZone = a.hold ?? "";
        if (puppetLight != null) puppetLight.intensity = 1.6f + Mathf.Sin(Time.time * 5f) * 0.25f;
    }

    // ---- 审计用 ----
    public void DebugClear() { buffer.Clear(); cooldown = 0f; if (EchoActive) Dismiss(); cooldown = 0f; }
    public void DebugPushFrame(Vector3 pos, float yaw, Vector3 vel, bool grounded, string holdZone) =>
        Push(new EchoFrame { pos = pos, yaw = yaw, vel = vel, grounded = grounded, hold = holdZone ?? "" });

    public string GetButtonText() => EchoActive ? "R · 收起残影" : "R · 潮汐怀表";
}
