// WP-07 剧情推进，WP-15 结局与上下灯塔控制。
// 支持玩家在塔顶任意时刻（包括 L3_oil / L4_light 解谜未完成时）随时下塔回地面，下塔后再按塔门 E 即可重回塔顶。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
    public void DescendTower()
    {
        if (!ready || cutscene || climbing) return;
        bool onTower = player != null && player.transform.position.y > 10f;
        if (!onTower && State.step != "L5_reply" && State.step != "E0_back") return;
        StartCoroutine(DescendTowerRoutine());
    }

    // 2026-11-07：场景里的点位标记（P_xx，由 IslandBuilder 按 world_layout 生成），找不到就用兜底值
    static Vector3 MarkerPos(string name, Vector3 fallback)
    {
        var go = GameObject.Find(name);
        return go != null ? go.transform.position : fallback;
    }

    static Vector3 EndingCityPos()
    {
        var c = EndingData.Load().city;
        return c != null ? new Vector3(c.x, c.y, c.z) : new Vector3(-85f, 7f, 25f);
    }

    System.Collections.IEnumerator DescendTowerRoutine()
    {
        cutscene = true;
        if (player != null) player.locked = true;
        yield return FadeTo(1f, FadeSeconds);

        var data = LighthouseData.Load();
        var door = LighthouseData.Point(data.door);
        if (door.y <= 0.1f) door = new Vector3(31.5f, 5.0f, 6.6f);   // 只在 lighthouse.json 缺数据时用（2026-11-07 新地图）
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
        // 2026-11-07：从山路顶（P_06）身后高处往下看整个小镇（P_13 附近）和海；按场景里的点算，换地图不用改
        var top = MarkerPos("P_06", new Vector3(-24.7f, 6.5f, 10.8f));
        var town = MarkerPos("P_13", new Vector3(0f, 3f, -6f));
        var lookFrom = top + new Vector3(-4.3f, 7.0f, 8.2f);
        var lookAt = new Vector3(town.x + 2.0f, 1.0f, town.z + 1.0f);
        fill.transform.rotation = Quaternion.LookRotation(lookAt - lookFrom, Vector3.up);
        yield return MoveCamera(lookFrom, lookAt, 1.6f);
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
        // 看山那边的新城（ending.json city），机位在山路顶（P_06）旁边略高处
        var top3 = MarkerPos("P_06", new Vector3(-24.7f, 6.5f, 10.8f));
        var cityPos = EndingCityPos();
        yield return MoveCamera(top3 + new Vector3(3.2f, 3.0f, 4.7f), cityPos, 2.2f);
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
        yield return new WaitForSecondsRealtime(3f);
        ui.ShowSubtitle("回家吧。山这边的新邮局，一直给你留着位置。");
        yield return new WaitForSecondsRealtime(3f);
        gameObject.AddComponent<EndingDispatch>();
    }

}
