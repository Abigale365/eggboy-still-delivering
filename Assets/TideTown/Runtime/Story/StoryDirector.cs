// WP-07 剧情推进，WP-11 起覆盖面包店：到点自动读信、火柴/柴火/烤炉链条、回忆线索和第一枚邮戳。
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class LetterData { public string id, title, body, signature; }
[Serializable] class LetterList { public LetterData[] letters; }
[Serializable] public class StoryStep { public string id, goal; public string letter; public string next, at; public bool auto; }
[Serializable] class StepList { public StoryStep[] steps; }
[Serializable] public class StoryRoutePoint { public string id; public float x, z; }
[Serializable] class StoryRouteList { public StoryRoutePoint[] route; }

public sealed partial class StoryDirector : MonoBehaviour
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
    bool readingSchool;
    string arrivedStep = "";
    GameObject carriedVisual;
    readonly BuiltinModelProvider modelProvider = new BuiltinModelProvider();

    public int SchoolLinesShown { get; private set; }
    public int SchoolLinesTotal { get; private set; }
    public string SchoolExprTrace { get; private set; } = "";

    Dictionary<string, Vector3> routePoints;
    IslandWorld island;
    LetterData[] letters = new LetterData[0];
    StoryStep[] steps = new StoryStep[0];

    public void Init(EggboyPlayer p, EggboyUI u, GameState s, SaveStore st, ControlGate g, WaterLevel w)
    {
        player = p; ui = u; State = s; store = st; gate = g; water = w;
        game = FindObjectOfType<PrototypeGame>();
        island = FindObjectOfType<IslandWorld>();
        dog = FindObjectOfType<DogFollower>();
        ball = FindObjectOfType<SchoolBall>();
        LoadData();
        ready = true;
        ApplyStepGoal();
        ui.OnLetterClosed += OnLetterClosed;
        ui.SetCarried(CarryLabel());
        if (island != null)
        {
            modelProvider.Register("wood", island.woodModel);
            modelProvider.Register("oil", island.oilModel);
        }
        ShowCarriedModel(State.carrying);
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
        if (IsAuditMode)
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
            if (island != null)
            {
                if (island.mailboxTrail != null) island.mailboxTrail.SetActive(State.step == "P0_boot");
                if (island.endingDesk != null) island.endingDesk.SetActive(State.flags.Contains("dawn_shown"));
            }
            ui.SetTutorial(State.step == "P0_boot"
                ? "沿金色路标走到邮筒 · 靠近后按 E 取信"
                : "");
            return;
        }

        // 真人游玩模式：采用纯密室逃脱式无保姆引导探索，关闭地上发光引路圆点与左上角直白任务剧透
        ui.SetGoal("");
        ui.SetTutorial("");
        if (island != null)
        {
            if (island.mailboxTrail != null) island.mailboxTrail.SetActive(false);
            if (island.endingDesk != null) island.endingDesk.SetActive(State.flags.Contains("dawn_shown"));
        }
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
        if (island != null && island.mailboxTrail != null) island.mailboxTrail.SetActive(false);
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

    // ---- WP-15 结局 ----

    // 塔顶读完灯以后要下塔；下塔和上塔一样是一次淡入淡出的短过场
    System.Collections.IEnumerator MoveCamera(Vector3 from, Vector3 look, float seconds)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        var p0 = cam.transform.position;
        var r0 = cam.transform.rotation;
        float f0 = cam.fieldOfView;
        var r1 = Quaternion.LookRotation(look - from, Vector3.up);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
            cam.transform.position = Vector3.Lerp(p0, from, k);
            cam.transform.rotation = Quaternion.Slerp(r0, r1, k);
            cam.fieldOfView = Mathf.Lerp(f0, 40f, k);
            yield return null;
        }
        cam.transform.SetPositionAndRotation(from, r1);
        cam.fieldOfView = 40f;
    }

    void OnLetterClosed()
    {
        if (gate != null && letterLock != null) gate.Release(letterLock);
        letterLock = null;
        if (player != null) player.locked = false;

        if (readingSchool) { readingSchool = false; ApplyStepGoal(); return; }
        if (State.step == "E1_read" && game != null) game.SetExpression(ExprId.happy);

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

    bool IsNearArrivalPoint(StoryStep st, Vector3 pp, Dictionary<string, Vector3> pts, out Vector3 lookTarget)
    {
        lookTarget = pts[st.at];
        float dx = pp.x - lookTarget.x, dz = pp.z - lookTarget.z;
        float distSq = dx * dx + dz * dz;
        if (IsAuditMode) return distSq <= 1.4f * 1.4f;

        // 真人游玩模式：放宽到点触发半径，且学校（S0_arrive）走到正门(19)、操场(21)或后门(20)任意一处即自动触发读信，不用盲找后门隐形点
        float triggerRadius = 3.8f;
        if (distSq <= triggerRadius * triggerRadius) return true;

        if (st.id == "S0_arrive")
        {
            foreach (var alt in new[] { "19", "21" })
            {
                if (!pts.ContainsKey(alt)) continue;
                var pAlt = pts[alt];
                float ax = pp.x - pAlt.x, az = pp.z - pAlt.z;
                if (ax * ax + az * az <= 4.8f * 4.8f)
                {
                    lookTarget = pAlt;
                    return true;
                }
            }
        }
        else if (st.id == "B0_arrive" && pts.ContainsKey("12"))
        {
            var pAlt = pts["12"];
            float ax = pp.x - pAlt.x, az = pp.z - pAlt.z;
            if (ax * ax + az * az <= 4.2f * 4.2f) return true;
        }
        return false;
    }

    // B0 走到 11 自动打开林阿婆的信；其它步骤也可以写 at + auto 复用
    void Update()
    {
        if (!ready || player == null || ui == null || gate == null) return;
        var st = Step(State.step);
        DebugAuto = "step=" + State.step + " auto=" + (st != null && st.auto) + " at=" + (st != null ? st.at : "")
            + " gate=" + gate.Locked + " letter=" + ui.LetterOpen + " arrived=" + arrivedStep;
        if (gate.Locked || ui.LetterOpen) return;
        // 老陈的信如果在外面被提前合上（没收起事件），补一次推进，别把玩家卡在 L3
        if (State.step == "L3_oil" && State.flags.Contains("lamp_oiled") && !ui.LetterOpen)
        {
            AdvanceTo("L4_light");
            return;
        }
        if (st == null || !st.auto || string.IsNullOrEmpty(st.at) || arrivedStep == st.id) return;
        var pts = RoutePoints();
        if (!pts.ContainsKey(st.at)) return;
        var pos = pts[st.at];
        var pp = player.transform.position;
        float dx = pp.x - pos.x, dz = pp.z - pos.z;
        DebugAuto += $" d={Mathf.Sqrt(dx * dx + dz * dz):F2}";
        if (!IsNearArrivalPoint(st, pp, pts, out var facePos)) return;
        arrivedStep = st.id;
        // E2 是过场：到山顶出口直接进回望镜头
        if (st.id == "E2_climb")
        {
            if (!cutscene) StartCoroutine(E2Sequence());
            return;
        }
        // E0 回到邮局：天快亮，桌上多一封信，小黑重新跟上
        if (st.id == "E0_back")
        {
            if (dog != null) dog.Follow();
            var dimmer = FindObjectOfType<SkyDimmer>();
            if (dimmer != null) dimmer.Dawn(6f);
            if (!State.flags.Contains("dawn_shown")) { State.flags.Add("dawn_shown"); store.Save(State); }
            ui.ShowToast("桌上多了一封信。");
        }
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
            player.FaceTowards(facePos);
            if (st.id == "S0_arrive" && game != null) game.SetExpression(ExprId.sad);
            else player.PlayEmote("look");
        }
        var letter = FindLetter(st.letter);
        if (letter != null) ui.ShowLetterDetail(letter.title, letter.body, letter.signature);
        else ui.ShowLetter();
    }
}
