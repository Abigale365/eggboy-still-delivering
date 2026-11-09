// WP-07 剧情推进，WP-13 覆盖学校：拖鞋 3D 检视、鲁班玩具木锁、红球滚入狗碗、小黑探头与逐行读信。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
    public void InspectSlipper()
    {
        if (!ready) return;
        if (!State.flags.Contains("slipper_seen")) State.flags.Add("slipper_seen");
        if (game != null) game.SetExpression(ExprId.curious);
        const string text = "拖鞋上有小黑的牙印，爪印一直通到操场";
        ui.ShowToast(text);
        if (State.carrying == "ball") AdvanceTo("S3_bowl");
        else AdvanceTo("S2_ball");
        if (!IsAuditMode)
        {
            ObjectInspectModal.Open("slipper", "被咬过的拖鞋", text, State, store, player, ui, gate);
        }
    }

    public void TakeBall()
    {
        if (!ready || State.flags.Contains("ball_taken")) return;
        State.flags.Add("ball_taken");
        State.carrying = "ball";
        store.Save(State);
        if (ball != null && player != null) ball.Take(player.transform);
        if (game != null) game.SetExpression(ExprId.happy);
        else if (player != null) player.PlayEmote("hop");
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
        if (ball != null) ball.RollIntoBowl();
        ui.SetCarried("");
        if (game != null) game.SetExpression(ExprId.surprised);
        else if (player != null) player.PlayEmote("look");
        TideTownAchievementSystem.Unlock("classic_ball");
        ui.ShowToast("碗里多了个红球。滑梯下面有动静。");
        if (dog != null) dog.ShowAt(dog.PeekPoint);
        AdvanceTo("S4_read");
        StartCoroutine(ReadSchoolLetter());
    }

    // 方案 B / C：玩家按喜好为小黑实时 3D 造出心仪美食或新玩具，同样可唤出小黑并解锁对应多解成就！
    public void CompleteSchoolWithCustomGift(string giftName, bool isFood)
    {
        if (!ready) return;
        TideTownAchievementSystem.Unlock(isFood ? "chef_dog" : "toy_dog");
        if (State.flags.Contains("ball_in_bowl"))
        {
            if (ui != null) ui.ShowToast("小黑欢快地围着你造出的「" + giftName + "」打转！", 3.5f);
            return;
        }
        State.flags.Add("ball_in_bowl");
        State.carrying = "";
        store.Save(State);
        ui.SetCarried("");
        if (game != null) game.SetExpression(ExprId.excited);
        else if (player != null) player.PlayEmote("hop");
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
