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
    PrototypeGame game;
    DogFollower dog;
    SchoolBall ball;
    object letterLock;
    bool ready;
    bool readingMemory;
    bool readingSchool;
    string arrivedStep = "";
    GameObject woodVisual;

    public int SchoolLinesShown { get; private set; }
    public int SchoolLinesTotal { get; private set; }
    public string SchoolExprTrace { get; private set; } = "";

    Dictionary<string, Vector3> routePoints;
    LetterData[] letters = new LetterData[0];
    StoryStep[] steps = new StoryStep[0];

    public void Init(EggboyPlayer p, EggboyUI u, GameState s, SaveStore st, ControlGate g, WaterLevel w)
    {
        player = p; ui = u; State = s; store = st; gate = g; water = w;
        game = FindObjectOfType<PrototypeGame>();
        dog = FindObjectOfType<DogFollower>();
        ball = FindObjectOfType<SchoolBall>();
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
        if (State.carrying == "ball") return "手上 · 红球";
        if (State.carrying == "oil") return "手上 · 油壶";
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
        ui.ShowToast("邮戳 1/3 · 面包店");
        AdvanceTo("S0_arrive");
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

    // ---- WP-13 学校 ----

    public void InspectSlipper()
    {
        if (!ready || State.step != "S1_slipper") return;
        if (game != null) game.SetExpression(ExprId.curious);
        ui.ShowToast("拖鞋上有小黑的牙印，爪印一直通到操场");
        AdvanceTo("S2_ball");
    }

    public void TakeBall()
    {
        if (!ready || State.step != "S2_ball" || State.flags.Contains("ball_taken")) return;
        State.flags.Add("ball_taken");
        State.carrying = "ball";
        store.Save(State);
        if (ball != null && player != null) ball.Take(player.transform);
        if (game != null) game.SetExpression(ExprId.happy);
        else if (player != null) player.PlayEmote("hop");
        ui.SetCarried(CarryLabel());
        ui.ShowToast("红球上有小黑的牙印");
        AdvanceTo("S3_bowl");
    }

    public void PutBallInBowl()
    {
        if (!ready || State.step != "S3_bowl" || State.carrying != "ball") return;
        State.flags.Add("ball_in_bowl");
        State.carrying = "";
        store.Save(State);
        if (ball != null) ball.PlaceInBowl();
        ui.SetCarried("");
        if (game != null) game.SetExpression(ExprId.surprised);
        else if (player != null) player.PlayEmote("look");
        ui.ShowToast("碗里多了个红球。滑梯下面有动静。");
        if (dog != null) dog.ShowAt(dog.PeekPoint);
        AdvanceTo("S4_read");
        StartCoroutine(ReadSchoolLetter());
    }

    // S4：不锁输入也能读，玩家提前按 E 合上信就当成读完；每行换一次面屏
    System.Collections.IEnumerator ReadSchoolLetter()
    {
        if (State.flags.Contains("school_read")) { ApplyStepGoal(); yield break; }
        readingSchool = true;
        SchoolLinesShown = 0;
        SchoolLinesTotal = 0;
        SchoolExprTrace = "";
        var st = Step("S4_read");
        var letter = FindLetter(st != null ? st.letter : "");
        if (letter == null)
        {
            readingSchool = false;
            FinishSchoolRead();
            yield break;
        }

        yield return new WaitForSecondsRealtime(1.4f); // 先让小黑从滑梯下探头
        if (dog != null) dog.Follow(); // 信纸挡住画面时接回身后，读完就能看见它

        var lines = SplitLines(letter.body);
        SchoolLinesTotal = lines.Length;
        letterLock = gate != null ? gate.Acquire("schoolread") : null;
        if (letterLock != null && player != null) player.locked = true;

        string shown = "";
        for (int i = 0; i < lines.Length; i++)
        {
            shown = i == 0 ? lines[i] : shown + "\n" + lines[i];
            var expr = i < 2 ? ExprId.sad : (i < 4 ? ExprId.curious : (i == lines.Length - 1 ? ExprId.happy : ExprId.sad));
            if (game != null) game.SetExpression(expr);
            SchoolExprTrace += (i == 0 ? "" : ">") + expr;
            ui.ShowLetterDetail(letter.title, shown, i == lines.Length - 1 ? letter.signature : "");
            SchoolLinesShown = i + 1;
            yield return new WaitForSecondsRealtime(1.15f);
            if (ui != null && !ui.LetterOpen) break;
        }

        readingSchool = false;
        if (gate != null && letterLock != null) gate.Release(letterLock);
        letterLock = null;
        if (player != null) player.locked = false;
        if (ui != null && ui.LetterOpen) ui.CloseLetter();
        FinishSchoolRead();
    }

    void FinishSchoolRead()
    {
        if (State.flags.Contains("school_read")) { ApplyStepGoal(); return; }
        State.flags.Add("school_read");
        if (!State.stamps.Contains("school")) State.stamps.Add("school");
        State.carrying = "";
        store.Save(State);
        if (water != null) water.SetLevel("L2", 2.5f);
        if (dog != null) dog.Follow();
        if (game != null) game.SetExpression(ExprId.happy);
        else if (player != null) player.PlayEmote("hop");
        ui.SetCarried("");
        ui.ShowToast("邮戳 2/3 · 学校");
        // S4 读完信后才接上灯塔段；提前合上信也走这里，避免停在 S4_read
        if (State.step == "S4_read") AdvanceTo("L0_cross");
        else ApplyStepGoal();
    }

    static string[] SplitLines(string body)
    {
        var lines = new List<string>();
        foreach (var raw in body.Split('\n'))
            if (!string.IsNullOrWhiteSpace(raw)) lines.Add(raw.Trim());
        return lines.ToArray();
    }

    // ---- WP-14 灯塔 ----

    const float FadeSeconds = 0.55f;
    bool climbing;
    UnityEngine.UI.Image fade;

    public bool Climbing => climbing;

    public void TakeOil()
    {
        if (!ready || State.step != "L1_shed" || State.flags.Contains("oil_taken")) return;
        State.flags.Add("oil_taken");
        State.carrying = "oil";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        ui.ShowToast("油壶里还剩小半壶。");
        AdvanceTo("L2_door");
    }

    public void ClimbTower()
    {
        if (!ready || State.step != "L2_door" || climbing) return;
        StartCoroutine(ClimbTowerRoutine());
    }

    // 工程书 L2：淡出、爬楼梯的短过场，再淡入到塔顶平台
    System.Collections.IEnumerator ClimbTowerRoutine()
    {
        climbing = true;
        if (player != null) player.locked = true;
        var dog = FindObjectOfType<DogFollower>();
        if (dog != null) dog.Hide(); // 小黑不会爬塔，留在塔下
        yield return FadeTo(1f, FadeSeconds);

        var data = LighthouseData.Load();
        var deck = LighthouseData.Point(data.deck);
        if (deck.y <= 0.1f) deck = new Vector3(22.18f, 16.98f, 3.3f);
        var lamp = LighthouseData.Point(data.lamp);
        float yaw = Mathf.Atan2(lamp.x - deck.x, lamp.z - deck.z);
        if (player != null)
        {
            player.transform.position = deck;
            player.vel = Vector3.zero;
            player.grounded = true;
            player.yaw = yaw;
            player.transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
        }
        var chase = FindObjectOfType<ChaseCamera>();
        if (chase != null) chase.SnapBehind(deck, yaw);
        yield return null;
        yield return null;
        AdvanceTo("L3_oil");
        yield return FadeTo(0f, FadeSeconds);
        if (player != null) player.locked = false;
        climbing = false;
    }

    public void OilLamp()
    {
        if (!ready || State.step != "L3_oil" || State.flags.Contains("lamp_oiled")) return;
        State.flags.Add("lamp_oiled");
        State.carrying = "";
        store.Save(State);
        ui.SetCarried("");
        if (game != null) game.SetExpression(ExprId.thinking);
        var st = Step("L3_oil");
        var letter = FindLetter(st != null ? st.letter : "");
        letterLock = gate != null ? gate.Acquire("lighthouse") : null;
        if (letterLock != null && player != null) player.locked = true;
        if (letter != null) ui.ShowLetterDetail(letter.title, letter.body, letter.signature);
        else ui.ShowLetter();
    }

    public void LightLamp()
    {
        if (!ready || State.step != "L4_light" || State.flags.Contains("beam_lit")) return;
        State.flags.Add("beam_lit");
        store.Save(State);
        if (game != null) game.SetExpression(ExprId.excited);
        if (player != null) player.PlayEmote("hop");
        var dimmer = FindObjectOfType<SkyDimmer>();
        if (dimmer != null) dimmer.Dim(20f);
        var beacon = FindObjectOfType<BeaconLight>();
        if (beacon != null) beacon.TurnOn();
        var reply = FindObjectOfType<FlashlightReply>();
        if (reply != null) reply.Begin();
        ui.ShowToast("灯塔亮了。");
        StartCoroutine(FinishLighthouse(reply));
    }

    System.Collections.IEnumerator FinishLighthouse(FlashlightReply reply)
    {
        float t0 = Time.realtimeSinceStartup;
        while (reply != null && !reply.Done && Time.realtimeSinceStartup - t0 < 30f) yield return null;
        FinishLighthouseNow();
    }

    // 山那边的手电亮完：盖第三枚邮戳，水位到 L3
    public void FinishLighthouseNow()
    {
        if (State.flags.Contains("lighthouse_done")) return;
        State.flags.Add("lighthouse_done");
        if (!State.stamps.Contains("lighthouse")) State.stamps.Add("lighthouse");
        store.Save(State);
        if (water != null) water.SetLevel("L3", 2.5f);
        if (game != null) game.SetExpression(ExprId.happy);
        AdvanceTo("L5_reply");
        ui.ShowToast("邮戳 3/3 · 灯塔");
    }

    System.Collections.IEnumerator FadeTo(float alpha, float seconds)
    {
        EnsureFade();
        float from = fade.color.a;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            var c = fade.color;
            c.a = Mathf.Lerp(from, alpha, Mathf.Clamp01(t / seconds));
            fade.color = c;
            yield return null;
        }
        var end = fade.color;
        end.a = alpha;
        fade.color = end;
    }

    void EnsureFade()
    {
        if (fade != null) return;
        var go = new GameObject("WP14 Fade", typeof(Canvas), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fade = go.GetComponent<UnityEngine.UI.Image>();
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.raycastTarget = false;
    }

    void OnLetterClosed()
    {
        if (gate != null && letterLock != null) gate.Release(letterLock);
        letterLock = null;
        if (player != null) player.locked = false;

        if (readingSchool) { readingSchool = false; ApplyStepGoal(); return; }
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
        // 不带信纸的到点步骤（L0 踩木箱到船棚）直接推进，不锁人
        if (string.IsNullOrEmpty(st.letter))
        {
            if (!string.IsNullOrEmpty(st.next)) AdvanceTo(st.next);
            else ApplyStepGoal();
            return;
        }
        letterLock = gate.Acquire("letter");
        if (letterLock == null) return;
        if (player != null)
        {
            player.locked = true;
            player.FaceTowards(pos);
            if (st.id == "S0_arrive" && game != null) game.SetExpression(ExprId.sad);
            else player.PlayEmote("look");
        }
        var letter = FindLetter(st.letter);
        if (letter != null) ui.ShowLetterDetail(letter.title, letter.body, letter.signature);
        else ui.ShowLetter();
    }
}
