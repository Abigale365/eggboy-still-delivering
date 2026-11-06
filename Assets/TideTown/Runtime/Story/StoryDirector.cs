// WP-07 剧情推进，WP-11 起覆盖面包店：到点自动读信、火柴/柴火/烤炉链条、回忆线索和第一枚邮戳。
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class LetterData { public string id, title, body, signature; }
[Serializable] class LetterList { public LetterData[] letters; }
[Serializable] public class StoryStep { public string id, goal; public string letter; public string next, at; public bool auto; }
[Serializable] class StepList { public StoryStep[] steps; }
[Serializable] class StoryRoutePoint { public string id; public float x, z; }
[Serializable] class StoryRouteList { public StoryRoutePoint[] route; }

public sealed class StoryDirector : MonoBehaviour
{
    public GameState State { get; private set; }

    EggboyPlayer player;
    EggboyUI ui;
    SaveStore store;
    ControlGate gate;
    WaterLevel water;
    object letterLock;
    bool ready;
    bool readingMemory;
    string arrivedStep = "";
    GameObject woodVisual;

    Dictionary<string, Vector3> routePoints;
    LetterData[] letters = new LetterData[0];
    StoryStep[] steps = new StoryStep[0];

    public void Init(EggboyPlayer p, EggboyUI u, GameState s, SaveStore st, ControlGate g, WaterLevel w)
    {
        player = p; ui = u; State = s; store = st; gate = g; water = w;
        LoadData();
        ready = true;
        ApplyStepGoal();
        ui.OnLetterClosed += OnLetterClosed;
        ui.SetCarried(CarryLabel());
        ShowWood(State.carrying == "wood");
    }

    void LoadData()
    {
        var l = Resources.Load<TextAsset>("Story/letters");
        if (l != null)
        {
            var list = JsonUtility.FromJson<LetterList>(l.text);
            if (list != null && list.letters != null) letters = list.letters;
        }
        var s = Resources.Load<TextAsset>("Story/steps");
        if (s != null)
        {
            var list = JsonUtility.FromJson<StepList>(s.text);
            if (list != null && list.steps != null) steps = list.steps;
        }
    }

    StoryStep Step(string id)
    {
        foreach (var s in steps) if (s.id == id) return s;
        return null;
    }

    void ApplyStepGoal()
    {
        if (State.step == "B4_light" && State.flags.Contains("oven_lit"))
        {
            ui.SetGoal("目标：第一站完成，烟囱冒烟了");
        }
        else
        {
            var st = Step(State.step);
            if (st != null && !string.IsNullOrEmpty(st.goal)) ui.SetGoal(st.goal);
        }
        // WP-10 教学提示：只在序章显示，取到信进入 P1 后收起
        ui.SetTutorial(State.step == "P0_boot"
            ? "按住 W 往左走 · 靠近发光的邮筒按 E"
            : "");
    }

    public string CarryLabel()
    {
        if (State == null) return "";
        if (State.carrying == "matches") return "邮包 · 火柴盒";
        if (State.carrying == "wood") return "手上 · 柴火捆";
        return "";
    }

    public void TakeLetters()
    {
        if (!ready || State.flags.Contains("letters_taken")) return;
        State.flags.Add("letters_taken");
        State.step = "P1_mailbox";
        store.Save(State);
        if (ui != null) ui.SetTutorial("");
        letterLock = gate.Acquire("letter");
        if (letterLock != null && player != null) player.locked = true;
        var id = Step(State.step)?.letter;
        LetterData letter = FindLetter(id);
        if (ui != null)
        {
            if (letter != null) ui.ShowLetter(letter.title, letter.body, letter.signature);
            else ui.ShowLetter();
        }
    }

    LetterData FindLetter(string id)
    {
        LetterData letter = null;
        foreach (var l in letters) if (l.id == id) letter = l;
        if (letter == null && letters.Length > 0) letter = letters[0];
        return letter;
    }

    public void AdvanceTo(string id)
    {
        State.step = id;
        store.Save(State);
        ApplyStepGoal();
    }

    // ---- WP-11 面包店 ----

    public void TakeMatches()
    {
        if (!ready || State.step != "B1_matches" || State.flags.Contains("matches_taken")) return;
        State.flags.Add("matches_taken");
        State.carrying = "matches";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        AdvanceTo("B2_wood");
    }

    public void TakeWood()
    {
        if (!ready || State.step != "B2_wood") return;
        if (State.flags.Contains("matches_taken") && !State.flags.Contains("matches_packed")) State.flags.Add("matches_packed");
        State.carrying = "wood";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        ShowWood(true);
        AdvanceTo("B3_load");
    }

    public void LoadWood()
    {
        if (!ready || State.step != "B3_load" || State.carrying != "wood") return;
        State.flags.Add("wood_loaded");
        State.carrying = "";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried("");
        ShowWood(false);
        AdvanceTo("B4_light");
    }

    public void LightOven(BakeryEffects fx)
    {
        if (!ready || State.step != "B4_light" || State.flags.Contains("oven_lit")) return;
        State.flags.Add("oven_lit");
        if (!State.stamps.Contains("bakery")) State.stamps.Add("bakery");
        store.Save(State);
        if (fx != null) fx.Light();
        if (water != null) water.SetLevel("L1", 2f);
        if (player != null) player.PlayEmote("hop");
        ui.SetGoal("目标：第一站完成，烟囱冒烟了");
        ui.ShowToast("邮戳 1/3 · 面包店");
    }

    public bool ShowMemory(string id, string title, string text)
    {
        if (!ready || ui == null || gate == null || gate.Locked) return false;
        if (!State.seenMemories.Contains(id)) { State.seenMemories.Add(id); store.Save(State); }
        letterLock = gate.Acquire("memory");
        if (letterLock == null) return false;
        readingMemory = true;
        if (player != null) player.locked = true;
        ui.ShowMemory(title, text);
        return true;
    }

    void ShowWood(bool on)
    {
        if (on && woodVisual == null && player != null)
        {
            woodVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(woodVisual.GetComponent<Collider>());
            woodVisual.name = "CarriedWood";
            woodVisual.transform.SetParent(player.transform, false);
            woodVisual.transform.localPosition = new Vector3(0f, 0.52f, -0.30f);
            woodVisual.transform.localScale = new Vector3(0.5f, 0.2f, 0.28f);
            woodVisual.GetComponent<Renderer>().material.color = new Color(0.55f, 0.36f, 0.2f);
        }
        if (!on && woodVisual != null) { Destroy(woodVisual); woodVisual = null; }
    }

    void OnLetterClosed()
    {
        if (gate != null && letterLock != null) gate.Release(letterLock);
        letterLock = null;
        if (player != null) player.locked = false;

        if (readingMemory) { readingMemory = false; ApplyStepGoal(); return; }

        var st = Step(State.step);
        if (st != null && !string.IsNullOrEmpty(st.next)) AdvanceTo(st.next);
        else ApplyStepGoal();
    }

    Dictionary<string, Vector3> RoutePoints()
    {
        if (routePoints != null) return routePoints;
        routePoints = new Dictionary<string, Vector3>();
        var asset = Resources.Load<TextAsset>("World/route");
        if (asset == null) return routePoints;
        var list = JsonUtility.FromJson<StoryRouteList>(asset.text);
        if (list == null || list.route == null) return routePoints;
        foreach (var p in list.route) routePoints[p.id] = new Vector3(p.x, 0f, p.z);
        return routePoints;
    }

    // 自动触发调试信息，storyaudit 失败时写进 JSON
    public string DebugAuto = "";

    // B0 走到 11 自动打开林阿婆的信；其它步骤也可以写 at + auto 复用
    void Update()
    {
        if (!ready || player == null || ui == null || gate == null) return;
        var st = Step(State.step);
        DebugAuto = "step=" + State.step + " auto=" + (st != null && st.auto) + " at=" + (st != null ? st.at : "")
            + " gate=" + gate.Locked + " letter=" + ui.LetterOpen + " arrived=" + arrivedStep;
        if (gate.Locked || ui.LetterOpen) return;
        if (st == null || !st.auto || string.IsNullOrEmpty(st.at) || arrivedStep == st.id) return;
        var pts = RoutePoints();
        if (!pts.ContainsKey(st.at)) return;
        var pos = pts[st.at];
        var pp = player.transform.position;
        float dx = pp.x - pos.x, dz = pp.z - pos.z;
        DebugAuto += $" d={Mathf.Sqrt(dx * dx + dz * dz):F2}";
        if (dx * dx + dz * dz > 1.4f * 1.4f) return;

        arrivedStep = st.id;
        letterLock = gate.Acquire("letter");
        if (letterLock == null) return;
        if (player != null)
        {
            player.locked = true;
            player.FaceTowards(pos);
            player.PlayEmote("look");
        }
        var letter = FindLetter(st.letter);
        if (letter != null) ui.ShowLetterDetail(letter.title, letter.body, letter.signature);
        else ui.ShowLetter();
    }
}
