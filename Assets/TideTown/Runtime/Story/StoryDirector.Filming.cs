using UnityEngine;

// 拍摄专用入口：不替换剧情算法；复用现有状态、动画、灯塔信号协程。
public sealed partial class StoryDirector
{
    public void PrepareFilmingStage(int stage)
    {
        if (IsAuditMode || FilmingStageJumper.AuditArguments()) return;
        StopAllCoroutines();
        ui.CloseLetter();
        if (letterLock != null) gate.Release(letterLock);
        letterLock = null; readingSchool = false; signalActive = false;
        cutscene = false; climbing = false;
        if (signalCard != null) signalCard.gameObject.SetActive(false);
        if (fade != null) fade.color = new Color(0, 0, 0, 0);
        arrivedStep = "";
        State.carrying = "";
        State.flags.Clear(); State.stamps.Clear();
        if (stage >= 6) State.flags.Add("letters_taken");
        if (stage >= 7) { State.flags.Add("oven_lit"); State.stamps.Add("bakery"); }
        if (stage >= 8) { State.flags.Add("school_read"); State.flags.Add("ball_in_bowl"); State.stamps.Add("school"); }
        var beacon = FindObjectOfType<BeaconLight>();
        if (beacon != null) { beacon.ManualMode = false; beacon.SetEnabled(false); }
        var sky = FindObjectOfType<SkyDimmer>();
        if (sky != null) sky.enabled = false;
        TownLighting.SetFilmingPhase(stage == 9 ? 1f : 0f);
        if (water != null) water.SetInstant(WaterLevel.LevelYs[stage >= 8 ? 2 : stage >= 7 ? 1 : 0]);
        Vector3 at, target;
        if (stage == 5)
        {
            var house = GameObject.Find("B1_post_office_visual");
            var bounds = house != null ? Lux3DModelFit.WorldBounds(house) : new Bounds(island.MailboxPos, Vector3.one);
            at = new Vector3(bounds.center.x, 0, bounds.min.z - .65f);
            target = at + Vector3.back * 3f; State.step = "P0_boot";
        }
        else if (stage == 6)
        {
            var stove = System.Array.Find(BakeryData.Load(), x => x.id == "stove");
            target = new Vector3(stove.x, stove.y, stove.z);
            at = target + new Vector3(0, 0, -1.5f); State.step = "B4_light";
            State.flags.Add("matches_taken"); State.flags.Add("matches_packed"); State.flags.Add("wood_loaded");
            State.flags.Add("oven_lit");
        }
        else if (stage == 7)
        {
            target = FindObjectOfType<WorldMechanismDirector>().GetDogBowlPosition();
            at = target + new Vector3(-1.2f, 0, -1.2f); State.step = "S1_slipper";
        }
        else if (stage == 8)
        {
            target = FindObjectOfType<WorldMechanismDirector>().WinchPosition;
            at = target + new Vector3(-1f, 0, -1f); State.step = "L0_cross";
            FindObjectOfType<WorldMechanismDirector>().DebugResetCrossing();
        }
        else
        {
            State.flags.Add("oil_taken"); State.flags.Add("lamp_oiled"); State.flags.Add("beam_lit");
            var ld = LighthouseData.Load();
            target = LighthouseData.Point(ld.lamp);
            var liveLamp = GameObject.Find("LH_lamp");
            if (liveLamp != null) target = liveLamp.transform.position;
            at = LighthouseData.Point(ld.deck);
            // 灯头/甲板必须取当前场景真实高度，禁止使用旧版固定 17m 坐标悬空传送。
            at.y = island.GroundAt(at.x, at.z, target.y + 1f);
            State.step = "L4_light";
        }
        if (stage != 9) at.y = island.GroundAt(at.x, at.z);
        player.transform.position = at; player.vel = Vector3.zero; player.grounded = true;
        player.locked = false; player.FaceTowards(target);
        var chase = FindObjectOfType<ChaseCamera>();
        if (chase != null) { chase.enabled = true; chase.SnapBehind(at, player.yaw); }
        ShowCarriedModel(null); ApplyStepGoal();
        var oven = FindObjectOfType<BakeryEffects>();
        if (stage >= 6 && oven != null) oven.Light();
        if (stage == 6) ui.SetGoal("实拍：炉火已亮 · 对电脑麦克风吹气鼓风 · Tab 重读信");
        var dog = FindObjectOfType<DogFollower>();
        if (dog != null) { if (stage >= 8) dog.Follow(); else dog.Hide(); }
        if (stage == 9)
        {
            player.locked = true; // 旋钮 A/D 只转灯，不让邮差沿塔顶平台横移。
            StartCoroutine(InteractiveLighthouseRoutine(beacon, FindObjectOfType<FlashlightReply>()));
        }
        Debug.Log("FILMING_STAGE F" + stage + " at=" + at + " lamp=" + target);
    }
}
