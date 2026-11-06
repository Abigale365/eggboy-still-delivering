// 原型入口：搭场景、放主角、接输入。对应 game/src/main.ts（信件界面还没移植，表情先驱动身体姿态）。
using System.Collections.Generic;
using UnityEngine;

public class PrototypeGame : MonoBehaviour
{
    public float modelYawDegrees = 0f; // 模型朝向不对时改这里

    IEggboyWorld world;
    EggboyPlayer player;
    ChaseCamera chase;
    EggboyFace face;
    EggboyUI ui;
    TownMap townMap;
    bool gotLetter;
    bool frontView; // 调试：F2 把镜头放到正面看脸
    bool autoWalk;  // 调试：-autowalk 自动往前走，用来检查步态
    bool autoRun;   // 调试：-townrun 自动快跑，用来横穿全镇
    bool autoJump;  // 调试：-autojump 原地跳一次
    bool autoJumped;
    bool autoLetter; // 调试：-letter 直接打开信件界面
    float captureAt = 5f;
    ExprId baseExpr = ExprId.happy;

    static readonly Dictionary<string, ExprId> EmoteExpr = new Dictionary<string, ExprId>
    {
        { "wave", ExprId.happy }, { "nod", ExprId.smug }, { "shake", ExprId.meh },
        { "hop", ExprId.excited }, { "give", ExprId.happy }, { "look", ExprId.curious }, { "sad", ExprId.sad },
    };

    static readonly (KeyCode key, string emote, int repeat)[] Emotes =
    {
        (KeyCode.Alpha1, "wave", 2), (KeyCode.Alpha2, "nod", 1), (KeyCode.Alpha3, "shake", 1),
        (KeyCode.Alpha4, "look", 1), (KeyCode.Alpha5, "sad", int.MaxValue),
    };

    void Start()
    {
        Application.runInBackground = true;
        var cliArgs = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < cliArgs.Length; i++)
            if (cliArgs[i] == "-expr" && System.Enum.TryParse<ExprId>(cliArgs[i + 1], true, out var expr))
                baseExpr = expr;
        var town = FindObjectOfType<TownWorld>();
        var island = FindObjectOfType<IslandWorld>();
        world = island != null ? (IEggboyWorld)island
              : town != null ? (IEggboyWorld)town
              : gameObject.AddComponent<EggboyWorld>();
        var prefab = Resources.Load<GameObject>("Models/eggboy");
        if (prefab == null) { Debug.LogError("找不到 Resources/Models/eggboy"); enabled = false; return; }
        var go = new GameObject("EGGBOY");
        player = go.AddComponent<EggboyPlayer>();
        player.disableAnim = System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-noanim");
        player.debugPose = System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-debugpose");
        player.antiClipOn = !System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-noanticlip");
        player.Setup(Instantiate(prefab), world, modelYawDegrees);
        if (player.pose == null) { enabled = false; return; }
        go.transform.position = world.SpawnPos;
        if (System.Array.Exists(cliArgs, a => a == "-nearmail")) go.transform.position = world.MailboxPos;
        bool expandedTown = town != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "TideTown_Expanded_Stage1";
        player.yaw = expandedTown ? Mathf.PI / 2f : -Mathf.PI / 2f;
        go.transform.rotation = Quaternion.Euler(0f, player.yaw * Mathf.Rad2Deg, 0f);
        face = new EggboyFace(player);
        SetExpression(baseExpr);
        player.onEmote = e => SetExpression(EmoteExpr.TryGetValue(e, out var expr) ? expr : ExprId.happy);
        player.onEmoteEnd = () => SetExpression(baseExpr);
        ui = gameObject.AddComponent<EggboyUI>();
        ui.Init(player, world);
        if (expandedTown) ui.SetGoal("目标：靠近山口的红色邮筒，按 E 取信");
        if (town != null) { townMap = gameObject.AddComponent<TownMap>(); townMap.Init(player, ui); }
        ui.OnExpressionPicked = id => { baseExpr = id; player.idle = 0f; SetExpression(id); };
        ui.OnLetterClosed = () =>
        {
            player.locked = false;
            player.PlayEmote("nod");
            ui.SetGoal("目标：去面包店（下一步还没做，先随便逛逛）");
        };
        ui.SetActiveExpression(baseExpr);
        chase = Camera.main.gameObject.AddComponent<ChaseCamera>();
        bool front = System.Array.Exists(cliArgs, a => a == "-frontview");
        frontView = front;
        autoWalk = System.Array.Exists(cliArgs, a => a == "-autowalk");
        autoJump = System.Array.Exists(cliArgs, a => a == "-autojump");
        autoLetter = System.Array.Exists(cliArgs, a => a == "-letter");
        for (int i = 0; i + 1 < cliArgs.Length; i++)
            if (cliArgs[i] == "-captureat") float.TryParse(cliArgs[i + 1], out captureAt);
        chase.SnapBehind(go.transform.position, player.yaw + (front ? Mathf.PI : 0f));
        if (System.Array.Exists(cliArgs, a => a == "-overview"))
        {
            chase.enabled = false;
            var ct = Camera.main.transform;
            ct.position = new Vector3(0f, 78f, 118f);
            ct.LookAt(new Vector3(0f, 0f, -10f));
            Camera.main.fieldOfView = 50f;
            foreach (var c in FindObjectsOfType<Canvas>()) c.enabled = false;
        }
        if (System.Array.Exists(cliArgs, a => a == "-autotest"))
        {
            autoWalk = true;
            player.yaw = Mathf.PI / 2f;
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            chase.SnapBehind(go.transform.position, Mathf.PI / 2f);
            var at = gameObject.AddComponent<TideTownAutoTest>();
            at.Begin(player);
        }
        if (System.Array.Exists(cliArgs, a => a == "-routeaudit")) gameObject.AddComponent<ExpandedRouteAudit>().Begin(player, this);
        if (System.Array.Exists(cliArgs, a => a == "-routeaudit2")) gameObject.AddComponent<RouteAudit2>().Begin(player, this);
        // 全镇横穿：从山口快跑到东侧码头，验主街全程无阻挡
        if (System.Array.Exists(cliArgs, a => a == "-townrun"))
        {
            autoWalk = true;
            autoRun = true;
            player.yaw = Mathf.PI / 2f;
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            chase.SnapBehind(go.transform.position, Mathf.PI / 2f);
            var at = gameObject.AddComponent<TideTownAutoTest>();
            at.duration = 80f;
            at.Begin(player);
        }
        // 码头段巡检：从沙滩走到码头尽头，验 14 cm 台阶和海上挡边
        if (System.Array.Exists(cliArgs, a => a == "-docktest"))
        {
            autoWalk = true;
            go.transform.position = new Vector3(44f, 0f, 13f);
            player.yaw = 0f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            chase.SnapBehind(go.transform.position, 0f);
            var at = gameObject.AddComponent<TideTownAutoTest>();
            at.duration = 30f;
            at.Begin(player);
        }
        if (System.Array.Exists(cliArgs, a => a == "-dockview"))
        {
            chase.enabled = false;
            var ct = Camera.main.transform;
            ct.position = new Vector3(28f, 14f, 44f);
            ct.LookAt(new Vector3(44f, 4f, 28f));
            foreach (var c in FindObjectsOfType<Canvas>()) c.enabled = false;
        }
        if (autoLetter) StartCoroutine(DebugLetter());
        if (System.Array.Exists(cliArgs, a => a == "-capture"))
            StartCoroutine(CaptureAfter(captureAt));
    }

    System.Collections.IEnumerator DebugLetter()
    {
        yield return new WaitForSeconds(1.2f);
        gotLetter = true;
        if (ui != null) ui.SetLetterTaken(true);
        player.locked = true;
        SetExpression(ExprId.thinking);
        if (ui != null) ui.ShowLetter();
    }

    System.Collections.IEnumerator CaptureAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        string path = System.IO.Path.Combine(Application.dataPath, "..", "capture.png");
        ScreenCapture.CaptureScreenshot(path, 1);
        Debug.Log("CAPTURE " + path);
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }

    void Update()
    {
        if (player == null || player.pose == null || chase == null) return;
        if ((ui != null && ui.LetterOpen) || (townMap != null && townMap.IsOpen)) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        float f = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        float s = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        var fwd = chase.Forward;
        var right = chase.Right;
        bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (autoWalk) { f = 1f; s = 0f; run = autoRun; }
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        if (autoJump && !autoJumped && Time.time > 1.0f) { jumpPressed = true; autoJumped = true; }

        // Z / X 换表情；表情同时驱动身体姿态
        if (Input.GetKeyDown(KeyCode.F2))
        {
            frontView = !frontView;
            chase.SnapBehind(player.transform.position, player.yaw + (frontView ? Mathf.PI : 0f));
        }
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X))
        {
            int i = System.Array.IndexOf(EggboyExpressions.All, baseExpr);
            int n = EggboyExpressions.All.Length;
            baseExpr = EggboyExpressions.All[(i + (Input.GetKeyDown(KeyCode.X) ? 1 : n - 1) + n) % n];
            SetExpression(baseExpr);
            if (ui != null) ui.SetActiveExpression(baseExpr);
        }

        foreach (var e in Emotes) if (Input.GetKeyDown(e.key)) player.PlayEmote(e.emote, e.repeat);

        if (Input.GetKeyDown(KeyCode.E) && Vector3.Distance(world.MailboxPos, player.transform.position) < 1.4f && !gotLetter)
        {
            gotLetter = true;
            if (ui != null) ui.SetLetterTaken(true);
            player.locked = true;
            player.FaceTowards(world.MailboxPos);
            player.PlayEmote("give");
            StartCoroutine(ShowLetterAfter(1.1f));
        }

        player.Move(dt, fwd.x * f + right.x * s, fwd.z * f + right.z * s, run, jumpPressed);

        // 站久了犯困，一动就醒
        if (player.CurrentEmote == null)
        {
            if (player.idle > 14f && player.pose.expr != ExprId.sleepy) SetExpression(ExprId.sleepy);
            else if (player.idle == 0f && player.pose.expr == ExprId.sleepy && baseExpr != ExprId.sleepy)
                SetExpression(baseExpr);
        }
    }

    System.Collections.IEnumerator ShowLetterAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetExpression(ExprId.thinking);
        if (ui != null) ui.ShowLetter();
    }

    void SetExpression(ExprId id)
    {
        if (player != null && player.pose != null) player.pose.SetExpression(id);
        if (face != null) face.Set(id);
    }

    void LateUpdate()
    {
        if (player == null || chase == null) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        if (face != null) face.Update(dt, Camera.main);
        bool moving = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)
            || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
        if (chase.enabled && (townMap == null || !townMap.IsOpen) && (ui == null || !ui.LetterOpen))
            chase.Tick(dt, player.camFocus, player.transform.eulerAngles.y * Mathf.Deg2Rad, moving && !frontView && !player.locked, Input.GetKey(KeyCode.LeftShift));
    }
}
