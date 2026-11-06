// WP-07 剧情推进，WP-11 起覆盖面包店：到点自动读信、火柴/柴火/烤炉链条、回忆线索和第一枚邮戳。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
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
    bool cutscene;
    object endLock;
    UnityEngine.UI.Image fade;

    public bool Climbing => climbing;
    public bool Cutscene => cutscene;
    public bool Ended { get; private set; }
    public string EndingDebug = "";

    void Mark(string s) { EndingDebug += s + "[" + Time.realtimeSinceStartup.ToString("F1") + "];"; }

}
