// WP-07 剧情推进，WP-11 起覆盖面包店：到点自动读信、火柴/柴火/烤炉链条、回忆线索和第一枚邮戳。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
    public void DescendTower()
    {
        if (!ready || cutscene) return;
        if (State.step != "L5_reply" && State.step != "E0_back") return;
        StartCoroutine(DescendTowerRoutine());
    }

    System.Collections.IEnumerator DescendTowerRoutine()
    {
        cutscene = true;
        if (player != null) player.locked = true;
        yield return FadeTo(1f, FadeSeconds);

        var data = LighthouseData.Load();
        var door = LighthouseData.Point(data.door);
        if (door.y <= 0.1f) door = new Vector3(21.5f, 5.0f, 1.45f);
        if (player != null)
        {
            player.transform.position = door;
            player.vel = Vector3.zero;
            player.grounded = true;
            player.yaw = Mathf.PI; // 面向回镇的路
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        var chase = FindObjectOfType<ChaseCamera>();
        if (chase != null) chase.SnapBehind(door, Mathf.PI);
        var dog = FindObjectOfType<DogFollower>();
        if (dog != null) dog.Follow(); // 小黑在塔下等了一路，重新跟上来
        yield return null;
        yield return null;
        if (State.step == "L5_reply") AdvanceTo("E0_back");
        yield return FadeTo(0f, FadeSeconds);
        if (player != null) player.locked = false;
        cutscene = false;
    }

    public void ReadEndingLetter()
    {
        if (!ready || State.step != "E1_read" || State.flags.Contains("ending_letter_read")) return;
        State.flags.Add("ending_letter_read");
        store.Save(State);
        var letter = FindLetter("zhou_eggboy");
        letterLock = gate != null ? gate.Acquire("endingletter") : null;
        if (letterLock != null && player != null) player.locked = true;
        if (game != null) game.SetExpression(ExprId.surprised);
        if (letter != null) ui.ShowLetterDetail(letter.title, letter.body, letter.signature);
        else ui.ShowLetter();
        StartCoroutine(EndingLetterMood());
    }

    // 表情从惊讶慢慢变成笑
    System.Collections.IEnumerator EndingLetterMood()
    {
        yield return new WaitForSecondsRealtime(4.5f);
        if (ui != null && ui.LetterOpen && game != null) game.SetExpression(ExprId.happy);
    }

    // E2：水位到 L4，镜头回望小镇，灯塔还在转
    System.Collections.IEnumerator E2Sequence()
    {
        cutscene = true;
        Mark("E2a");
        if (player != null) { player.locked = true; player.FaceTowards(new Vector3(0f, 2f, -6f)); }
        if (water != null) water.SetLevel("L4", 4f);
        var chase = FindObjectOfType<ChaseCamera>();
        if (chase != null) chase.enabled = false;
        // 回望是背光面，黎明主光只有 0.55 强度，灰盒房全黑；过场期间加一盏顺镜头的补光，结束即销毁
        var fill = new GameObject("EndingFillLight");
        var fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color32(255, 226, 196, 255);
        fillLight.intensity = 0.45f;
        fillLight.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.LookRotation(new Vector3(4.0f, 0.8f, -3.0f) - new Vector3(-23.0f, 14.6f, 19.5f), Vector3.up);
        yield return MoveCamera(new Vector3(-23.0f, 14.6f, 19.5f), new Vector3(4.0f, 0.8f, -3.0f), 1.6f);
        Mark("E2cam");
        yield return new WaitForSecondsRealtime(8f);
        Mark("E2wait");
        Destroy(fill);
        if (chase != null && player != null)
        {
            chase.enabled = true;
            chase.SnapBehind(player.transform.position, player.yaw);
        }
        if (player != null) player.locked = false;
        cutscene = false;
        AdvanceTo("E3_end");
        Mark("E3go");
        StartCoroutine(E3Sequence());
    }

    // E3：山顶看新城灯光，挥手，字幕，停在画面上
    System.Collections.IEnumerator E3Sequence()
    {
        cutscene = true;
        Mark("E3a");
        if (player != null) player.locked = true;
        var chase = FindObjectOfType<ChaseCamera>();
        if (chase != null) chase.enabled = false;
        var city = FindObjectOfType<CityGlow>();
        if (city != null) city.Show();
        if (game != null) game.SetExpression(ExprId.happy);
        ui.SetGoal("");
        ui.ShowSubtitle("所有的信，都送到了。");
        yield return MoveCamera(new Vector3(-19.6f, 9.0f, 13.7f), new Vector3(-60.0f, 5.8f, 3.0f), 2.2f);
        Mark("E3cam");
        yield return new WaitForSecondsRealtime(1.6f);
        if (player != null) player.PlayEmote("wave", 3);
        if (!State.flags.Contains("ending_done"))
        {
            State.flags.Add("ending_done");
            store.Save(State);
        }
        Ended = true;
        endLock = gate != null ? gate.Acquire("ending") : null;
        cutscene = false;
        Mark("E3done");
    }

}
