// WP-07 剧情推进，WP-13 覆盖学校：拖鞋 3D 检视、鲁班玩具木锁、红球滚入狗碗、小黑探头与逐行读信。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
    // 2026-11-16 真实接触式拾取（PhysicalPickupController）：玩家按 E 走这些 ...At 入口——先走到物体正面、伸手碰到再提交。
    // 揭字成物、AI 校准台这类"隔空"调用照旧走原来的即时方法；审计模式下 ...At 也直接退回即时方法。
    PhysicalPickupController Pickup => PhysicalPickupController.Instance;

    bool BeginPhysical(PhysicalPickupController.Job job)
    {
        var pk = Pickup;
        if (pk == null || !pk.Active(job.clip)) return false;
        if (pk.Busy) return true;   // 正在走过去 / 正在做动作：这次按 E 不重复触发
        bool ok = pk.Begin(job);
        if (ok && game != null) game.SetExpression(ExprId.thinking);
        return ok;
    }

    public void InspectSlipperAt(Vector3 pos)
    {
        if (!ready) return;
        if (BeginPhysical(new PhysicalPickupController.Job
            { clip = "inspect_slipper", target = () => pos + Vector3.up * 0.04f, diameter = 0.12f, standDistance = 0.32f,
              contactTime = 0.42f, lockSeconds = 0.70f, tolerance = 0.08f, onContact = () => InspectSlipperCommit(false) })) return;
        InspectSlipper();
    }

    public void InspectSlipper() => InspectSlipperCommit(true);

    void InspectSlipperCommit(bool animate)
    {
        if (!ready) return;
        if (!State.flags.Contains("slipper_seen")) State.flags.Add("slipper_seen");
        if (animate) PlayAction("inspect_slipper", ExprId.curious, 0.70f, 0.42f);
        else if (game != null) game.SetExpression(ExprId.curious);
        const string text = "拖鞋上有小黑的牙印，爪印一直通到操场";
        ui.ShowToast(text);
        if (State.carrying == "ball") AdvanceTo("S3_bowl");
        else AdvanceTo("S2_ball");
        if (!IsAuditMode)
        {
            ObjectInspectModal.Open("slipper", "被咬过的拖鞋", text, State, store, player, ui, gate);
        }
    }

    // 玩家按 E 捡红球：走到球的正面（约 0.40 m），送肩伸右手，掌心真贴到球面的那一帧才算拿到，再沿弧线托到身侧
    public void TakeBallAt()
    {
        if (!ready || State.flags.Contains("ball_taken")) return;
        if (ball != null)
        {
            var bt = ball.transform;
            if (BeginPhysical(new PhysicalPickupController.Job
                { clip = "pickup_ball", target = () => bt.position, item = bt, diameter = PickupGrip.DefaultDiameter,
                  standDistance = 0.35f, contactTime = 0.40f, lockSeconds = 0.85f, liftBall = true,
                  onContact = () => TakeBallCommit(false) })) return;
        }
        TakeBall();
    }

    public void TakeBall() => TakeBallCommit(true);

    void TakeBallCommit(bool animate)
    {
        if (!ready || State.flags.Contains("ball_taken")) return;
        State.flags.Add("ball_taken");
        State.carrying = "ball";
        store.Save(State);
        if (animate)
            PlayAction("pickup_ball", ExprId.happy, 0.85f, 0.40f, () =>
            {
                if (ball != null && player != null) ball.Take(player.transform);
                SyncCarryPose();
            });
        else
        {
            SyncCarryPose();
            if (game != null) game.SetExpression(ExprId.happy);
        }
        ui.SetCarried(CarryLabel());
        ui.ShowToast("红球上有小黑的牙印 · 快拿去学校后院的狗碗吧");
        AdvanceTo("S3_bowl");
    }

    public void PutBallInBowl()
    {
        if (!ready || State.carrying != "ball" || State.flags.Contains("ball_in_bowl")) return;
        WoodenMechanismPuzzleModal.OpenOrRun("school_lock", State, store, player, ui, gate, CompletePutBallInBowl);
    }

    void CompletePutBallInBowl()
    {
        if (!ready || State.flags.Contains("ball_in_bowl")) return;
        State.flags.Add("ball_in_bowl");
        State.carrying = "";
        store.Save(State);
        ui.SetCarried("");
        // 真实放下：走到狗碗侧前方，侧身弯腰把球送到碗上方，0.48 s 松手落进碗里；动作做完小黑才探头、才开始读信
        var pk = Pickup;
        bool physical = false;
        if (ball != null && pk != null && pk.HoldsBall && pk.HeldBall == ball && !pk.Busy && pk.Active("put_ball_bowl"))
        {
            Vector3 bowlBase = ball.BowlPoint - Vector3.up * 0.11f;
            physical = pk.Begin(new PhysicalPickupController.Job
            {
                clip = "put_ball_bowl", target = () => bowlBase, standOffset = new Vector3(0.446f, 0f, 0.13f),
                contactTime = 0.48f, lockSeconds = 0.90f, putBall = true,
                onContact = () => { if (ball != null) ball.RollIntoBowl(); SyncCarryPose(); },
                onDone = () =>
                {
                    if (dog != null) dog.ShowAt(dog.PeekPoint);
                    StartCoroutine(ReadSchoolLetter());
                }
            });
            if (physical && game != null) game.SetExpression(ExprId.surprised);
        }
        if (!physical)
            PlayAction("put_ball_bowl", ExprId.surprised, 0.90f, 0.48f, () =>
            {
                if (ball != null) ball.RollIntoBowl();
                SyncCarryPose();
            });
        TideTownAchievementSystem.Unlock("classic_ball");
        ui.ShowToast("碗里多了个红球。滑梯下面有动静。");
        AdvanceTo("S4_read");
        if (!physical)
        {
            if (dog != null) dog.ShowAt(dog.PeekPoint);
            StartCoroutine(ReadSchoolLetter());
        }
    }

    // 方案 B / C：玩家按喜好为小黑实时 3D 造出心仪美食或新玩具，同样可唤出小黑并解锁对应多解成就！
    public void CompleteSchoolWithCustomGift(string giftName, bool isFood)
    {
        if (!ready) return;
        TideTownAchievementSystem.Unlock(isFood ? "chef_dog" : "toy_dog");
        if (State.flags.Contains("ball_in_bowl"))
        {
            PlayAction("pet_dog", ExprId.excited, 0.90f, 0.35f);
            if (ui != null) ui.ShowToast("小黑欢快地围着你造出的「" + giftName + "」打转！", 3.5f);
            return;
        }
        State.flags.Add("ball_in_bowl");
        State.carrying = "";
        store.Save(State);
        ui.SetCarried("");
        SyncCarryPose();
        PlayAction("pet_dog", ExprId.excited, 0.90f, 0.35f);
        ui.ShowToast("碗旁放上了热气腾腾的「" + giftName + "」。滑梯下面传来了小黑惊喜的动静！", 3.8f);
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
        if (letterLock != null && player != null)
        {
            player.locked = true;
            player.SetStoryCrouch(true);
        }

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
        if (player != null)
        {
            player.SetStoryCrouch(false);
            player.locked = false;
        }
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
        if (water != null) water.SetLevel("L2", WaterLevel.StationRiseSeconds);
        if (dog != null) dog.Follow();
        if (game != null) game.SetExpression(ExprId.happy);
        else if (player != null) player.PlayEmote("hop");
        ui.SetCarried("");
        ui.ShowToast("邮戳 2/3 · 学校 · 小黑摇着尾巴跟上了你的脚步");
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
    bool cutscene;
    object endLock;
    UnityEngine.UI.Image fade;

    public bool Climbing => climbing;
    public bool Cutscene => cutscene;
    public bool Ended { get; private set; }
    public string EndingDebug = "";

    void Mark(string s) { EndingDebug += s + "[" + Time.realtimeSinceStartup.ToString("F1") + "];"; }

}
