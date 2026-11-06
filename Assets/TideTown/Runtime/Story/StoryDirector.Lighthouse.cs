// WP-07 剧情推进，WP-11 起覆盖面包店：到点自动读信、火柴/柴火/烤炉链条、回忆线索和第一枚邮戳。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
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

}
