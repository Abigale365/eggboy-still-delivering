// 创新方向 F：【潮汐回声 · 与“十秒前的自己”跨时空接力】（Tidal Echo Self-Coop System）。
// 100% 克隆真实 EggboyPlayer 角色模型（绝不使用粗糙球体白盒，绝不在地面乱摆突兀几何体）。
// 按 3 键（或 R 键）记录自己 8 秒内的行动轨迹，再次按下即可唤出「十秒前的你」重演路径，并与现在的你协同完成取物、鼓风与解谜。
using System.Collections.Generic;
using UnityEngine;

public sealed class TidalEchoCloneSystem : MonoBehaviour
{
    struct EchoFrame
    {
        public Vector3 pos;
        public float yaw;
    }

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;

    public bool IsRecording { get; private set; }
    public bool IsReplaying { get; private set; }
    float recordTimer;
    const float MaxRecordSeconds = 8.0f;
    float sampleAccum;
    readonly List<EchoFrame> frames = new List<EchoFrame>();
    int replayIndex;
    float replayAccum;
    bool pingPongForward = true;

    GameObject cloneGo;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        player = p;
        ui = u;
        world = w;
        director = d;
        state = s;
    }

    public void TriggerEchoButton()
    {
        if (player == null) return;

        if (!IsRecording && !IsReplaying)
        {
            StartRecording();
        }
        else if (IsRecording)
        {
            StartReplaying();
        }
        else
        {
            StopEcho();
        }
    }

    void StartRecording()
    {
        IsRecording = true;
        IsReplaying = false;
        recordTimer = MaxRecordSeconds;
        sampleAccum = 0f;
        frames.Clear();
        frames.Add(new EchoFrame { pos = player.transform.position, yaw = player.yaw });

        ShadowAndChimeSystem.PlayProceduralChime(523.25f, 0.25f);
        if (ui != null)
            ui.ShowToast("潮汐怀表已开启：正在记录你的脚步轨迹（再按 3 或 R 唤出十秒前的自己协力）", 3.6f);
    }

    void StartReplaying()
    {
        IsRecording = false;
        if (frames.Count < 2 && player != null)
        {
            // 若玩家原地连按两次 3，自动生成一段向前协助探路的轨迹
            Vector3 p0 = player.transform.position;
            float yaw = player.yaw;
            Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                Vector3 pt = p0 + fwd * (t * 2.6f);
                if (world != null) pt.y = world.GroundAt(pt.x, pt.z, p0.y + 1f);
                frames.Add(new EchoFrame { pos = pt, yaw = yaw });
            }
        }

        IsReplaying = true;
        replayIndex = 0;
        replayAccum = 0f;
        pingPongForward = true;

        EnsureAuthenticEggboyClone();
        if (cloneGo != null)
        {
            cloneGo.SetActive(true);
            cloneGo.transform.position = frames[0].pos;
            cloneGo.transform.rotation = Quaternion.Euler(0f, frames[0].yaw * Mathf.Rad2Deg, 0f);
        }

        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.35f);
        AssistNearbyStationTask();
    }

    void StopEcho()
    {
        IsRecording = false;
        IsReplaying = false;
        if (cloneGo != null) cloneGo.SetActive(false);
        if (ui != null) ui.ShowToast("潮汐残影已随海风消散。", 2.4f);
    }

    void EnsureAuthenticEggboyClone()
    {
        if (cloneGo != null || player == null) return;

        // 直接克隆真实的 EggboyPlayer 视觉层级，保持 100% 原版角色画风与质感
        cloneGo = Instantiate(player.gameObject, transform, false);
        cloneGo.name = "EggboyTidalEchoClone";

        foreach (var mb in cloneGo.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(mb);
        foreach (var col in cloneGo.GetComponentsInChildren<Collider>(true))
            Destroy(col);
        foreach (var rb in cloneGo.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);

        var lightGo = new GameObject("EchoWarmLight");
        lightGo.transform.SetParent(cloneGo.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.84f, 0.45f);
        l.range = 4.2f;
        l.intensity = 1.8f;
    }

    void AssistNearbyStationTask()
    {
        if (player == null || state == null || ui == null) return;
        Vector3 p = player.transform.position;

        // 1. 若在船坞渡口附近，十秒前的残影可协助你摇动并卡住【3D 潮汐船坞齿轮绞盘】
        var worldMech = FindObjectOfType<WorldMechanismDirector>();
        if (worldMech != null && worldMech.TryEchoAssistWinch(p))
            return;

        // 2. 面包店范围：若已点亮烤炉，残影可协助拉动风箱助燃
        if (Vector3.Distance(p, new Vector3(-11.5f, 1.3f, -7.8f)) < 11f)
        {
            var fx = FindObjectOfType<BakeryEffects>();
            if (state.flags.Contains("oven_lit") && director != null && fx != null)
            {
                director.BoostOvenBellows(fx);
                return;
            }
        }

        ui.ShowToast("十秒前的潮汐残影已唤出：正在重演你的脚步轨迹，可在船坞渡口协力摇动 3D 齿轮绞盘（再按 R 收起）", 3.6f);
    }

    void Update()
    {
        if (player == null || ui == null || !ui.inputClose) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (IsRecording)
        {
            recordTimer -= dt;
            sampleAccum += dt;
            if (sampleAccum >= 0.05f)
            {
                sampleAccum = 0f;
                frames.Add(new EchoFrame { pos = player.transform.position, yaw = player.yaw });
            }
            if (recordTimer <= 0f)
            {
                StartReplaying();
            }
        }
        else if (IsReplaying && cloneGo != null && frames.Count > 0)
        {
            replayAccum += dt;
            if (replayAccum >= 0.05f)
            {
                replayAccum = 0f;
                if (frames.Count > 1)
                {
                    if (pingPongForward)
                    {
                        replayIndex++;
                        if (replayIndex >= frames.Count - 1) pingPongForward = false;
                    }
                    else
                    {
                        replayIndex--;
                        if (replayIndex <= 0) pingPongForward = true;
                    }
                }
            }
            replayIndex = Mathf.Clamp(replayIndex, 0, frames.Count - 1);
            var f = frames[replayIndex];
            cloneGo.transform.position = Vector3.Lerp(cloneGo.transform.position, f.pos, dt * 14f);
            cloneGo.transform.rotation = Quaternion.Slerp(cloneGo.transform.rotation, Quaternion.Euler(0f, f.yaw * Mathf.Rad2Deg, 0f), dt * 12f);
        }
    }

    public string GetButtonText()
    {
        if (IsRecording) return "3 · 正在记下脚步(" + Mathf.CeilToInt(recordTimer) + "s)";
        if (IsReplaying) return "3 · 收起潮汐分身";
        return "3 · 潮汐回声";
    }
}
