// WP-07 剧情推进：读 Resources/Story/steps.json 和 letters.json，管邮筒取信与读信锁。
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class LetterData { public string id, title, body, signature; }
[Serializable] class LetterList { public LetterData[] letters; }
[Serializable] public class StoryStep { public string id, goal; public string letter; public string next; }
[Serializable] class StepList { public StoryStep[] steps; }

public sealed class StoryDirector : MonoBehaviour
{
    public GameState State { get; private set; }

    EggboyPlayer player;
    EggboyUI ui;
    SaveStore store;
    ControlGate gate;
    object letterLock;
    bool ready;

    LetterData[] letters = new LetterData[0];
    StoryStep[] steps = new StoryStep[0];

    public void Init(EggboyPlayer p, EggboyUI u, GameState s, SaveStore st, ControlGate g)
    {
        player = p; ui = u; State = s; store = st; gate = g;
        LoadData();
        ready = true;
        ApplyStepGoal();
        ui.OnLetterClosed += OnLetterClosed;
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
        var st = Step(State.step);
        if (st != null && !string.IsNullOrEmpty(st.goal)) ui.SetGoal(st.goal);
        // WP-10 教学提示：只在序章显示，取到信进入 P1 后收起
        ui.SetTutorial(State.step == "P0_boot"
            ? "按住 W 往左走 · 靠近发光的邮筒按 E"
            : "");
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
        LetterData letter = null;
        foreach (var l in letters) if (l.id == id) letter = l;
        if (letter == null && letters.Length > 0) letter = letters[0];
        if (ui != null)
        {
            if (letter != null) ui.ShowLetter(letter.title, letter.body, letter.signature);
            else ui.ShowLetter();
        }
    }

    void OnLetterClosed()
    {
        if (gate != null && letterLock != null) gate.Release(letterLock);
        letterLock = null;
        if (player != null) player.locked = false;
        var st = Step(State.step);
        if (st != null && !string.IsNullOrEmpty(st.next))
        {
            State.step = st.next;
            store.Save(State);
        }
        ApplyStepGoal();
    }
}
