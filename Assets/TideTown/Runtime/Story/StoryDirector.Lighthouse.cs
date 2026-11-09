// WP-07 剧情推进，WP-14 覆盖灯塔：拿油壶、上塔、加油读信、鲁班三轴罗经、转动灯头对焦山顶并闪灯三次触发山那边手电回应。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public sealed partial class StoryDirector : MonoBehaviour
{
    bool signalActive;
    float signalAlignment;
    int signalFlashes;
    BeaconLight activeBeacon;
    FlashlightReply activeReply;
    RectTransform signalCard;
    Text signalAlignLabel, signalFlashLabel, signalReplyLabel;

    public void TakeOil()
    {
        if (!ready || State.step != "L1_shed" || State.flags.Contains("oil_taken")) return;
        State.flags.Add("oil_taken");
        State.carrying = "oil";
        ShowCarriedModel("oil");
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        ui.ShowToast("油壶里还剩小半壶。");
        AdvanceTo("L2_door");
    }

    public void ClimbTower()
    {
        if (!ready || climbing) return;
        if (State.step != "L2_door" && State.step != "L3_oil" && State.step != "L4_light") return;
        StartCoroutine(ClimbTowerRoutine());
    }

    System.Collections.IEnumerator ClimbTowerRoutine()
    {
        climbing = true;
        if (player != null) player.locked = true;
        var dog = FindObjectOfType<DogFollower>();
        if (dog != null) dog.Hide();
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
        if (State.step == "L2_door")
            AdvanceTo(State.flags.Contains("lamp_oiled") ? "L4_light" : "L3_oil");
        yield return FadeTo(0f, FadeSeconds);
        if (player != null) player.locked = false;
        climbing = false;
    }

    public void OilLamp()
    {
        if (!ready || State.step != "L3_oil" || State.flags.Contains("lamp_oiled")) return;
        State.flags.Add("lamp_oiled");
        State.carrying = "";
        ShowCarriedModel(null);
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
        WoodenMechanismPuzzleModal.OpenOrRun("lighthouse_lock", State, store, player, ui, gate, CompleteLightLamp);
    }

    void CompleteLightLamp()
    {
        if (!ready || State.flags.Contains("beam_lit")) return;
        State.flags.Add("beam_lit");
        store.Save(State);
        if (game != null) game.SetExpression(ExprId.excited);
        if (player != null) player.PlayEmote("hop");
        var dimmer = FindObjectOfType<SkyDimmer>();
        if (dimmer != null) dimmer.Dim(20f);
        var beacon = FindObjectOfType<BeaconLight>();
        var reply = FindObjectOfType<FlashlightReply>();

        if (IsAuditMode)
        {
            if (beacon != null) { beacon.Face(EndingCityPos()); beacon.TurnOn(); }
            if (reply != null) reply.Begin();
            ui.ShowToast("灯塔亮了。");
            StartCoroutine(FinishLighthouse(reply));
        }
        else
        {
            StartCoroutine(InteractiveLighthouseRoutine(beacon, reply));
        }
    }

    void EnsureSignalCardUI()
    {
        if (signalCard != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (root == null) return;

        signalCard = TideTownStyleUI.CreateParchmentCard(root, "LighthouseSignalCard", 440f, 220f, 4);
        signalCard.anchorMin = signalCard.anchorMax = new Vector2(1f, 1f);
        signalCard.pivot = new Vector2(1f, 1f);
        signalCard.anchoredPosition = new Vector2(-24f, -64f);

        TideTownStyleUI.CreateText(
            signalCard, "塔顶观海便签 · 呼唤山那边的人", 18, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 72f), new Vector2(390f, 28f), true);

        signalAlignLabel = TideTownStyleUI.CreateText(
            signalCard, "", 13, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 34f), new Vector2(390f, 24f), false);

        signalFlashLabel = TideTownStyleUI.CreateText(
            signalCard, "", 13, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 2f), new Vector2(390f, 24f), false);

        signalReplyLabel = TideTownStyleUI.CreateText(
            signalCard, "", 13, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft,
            new Vector2(0f, -28f), new Vector2(390f, 24f), false);

        TideTownStyleUI.CreateWarmButton(
            signalCard, "朝山那边挥手闪灯", new Vector2(0f, -74f), new Vector2(360f, 36f), true, TriggerSignalFlash);
    }

    void RefreshSignalCardUI()
    {
        if (signalCard == null) return;
        int pct = Mathf.RoundToInt(signalAlignment * 100f);
        if (signalAlignLabel != null)
            signalAlignLabel.text = "光束正掠过夜海，山顶新城就在远方（光束对焦：" + pct + "%）";
        if (signalFlashLabel != null)
            signalFlashLabel.text = "山那边的人在等你的身影，朝他们挥挥手吧（信标：" + signalFlashes + " / 3）";
        int lit = activeReply != null ? activeReply.LitCount : 0;
        if (signalReplyLabel != null)
            signalReplyLabel.text = "对岸山头亮起的手电：" + lit + " / 40 盏";
    }

    System.Collections.IEnumerator InteractiveLighthouseRoutine(BeaconLight beacon, FlashlightReply reply)
    {
        activeBeacon = beacon;
        activeReply = reply;
        signalFlashes = 0;
        signalActive = true;

        EnsureSignalCardUI();
        if (signalCard != null) signalCard.gameObject.SetActive(true);

        var targetCity = EndingCityPos();
        if (beacon != null)
        {
            beacon.ManualMode = true;
            beacon.SetYawOffsetFrom(targetCity, -34f);
            beacon.TurnOn();
        }
        ui.ShowToast("灯塔亮起了暖光。山那边的人正在等你的身影，朝他们挥挥手吧。", 4.5f);

        float idleTimer = 0f;
        float flashCooldown = 0f;

        while (signalFlashes < 3)
        {
            float dt = Time.unscaledDeltaTime;
            if (flashCooldown > 0f) flashCooldown -= dt;

            float turn = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) turn -= 52f * dt;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) turn += 52f * dt;
            if (Input.GetMouseButton(0)) turn += Input.GetAxis("Mouse X") * 2.6f;

            // 单手柄摇杆或摄像头手势横移同步驱动灯头转向
            float padX = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(padX) > 0.18f) turn += padX * 52f * dt;
            if (HardwareAndSensorHub.Instance != null && HardwareAndSensorHub.Instance.CamOpen)
            {
                float handOffset = (HardwareAndSensorHub.Instance.HandCenterX - 0.5f) * 2f;
                if (Mathf.Abs(handOffset) > 0.18f) turn += handOffset * 48f * dt;
            }

            if (Mathf.Abs(turn) > 0.001f)
            {
                idleTimer = 0f;
                if (beacon != null) beacon.RotateYaw(turn);
            }
            else
            {
                idleTimer += dt;
            }

            signalAlignment = beacon != null ? beacon.AlignmentWith(targetCity) : 1f;

            bool camWave = HardwareAndSensorHub.Instance != null &&
                           HardwareAndSensorHub.Instance.CamOpen &&
                           HardwareAndSensorHub.Instance.HandWaveSpeed > 0.38f;
            bool padWave = Input.GetKeyDown(KeyCode.JoystickButton0) ||
                           Input.GetKeyDown(KeyCode.JoystickButton3) ||
                           Input.GetKeyDown(KeyCode.JoystickButton5);

            bool wantFlash = flashCooldown <= 0f &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || camWave || padWave);

            if (idleTimer > 11f)
            {
                if (beacon != null && signalAlignment < 0.92f)
                {
                    beacon.Face(targetCity);
                    signalAlignment = 1f;
                }
                if (flashCooldown <= 0f) wantFlash = true;
            }

            if (wantFlash)
            {
                if (signalAlignment < 0.72f && beacon != null)
                {
                    beacon.Face(targetCity);
                    signalAlignment = beacon.AlignmentWith(targetCity);
                }
                TriggerSignalFlash();
                flashCooldown = 0.85f;
                idleTimer = 0f;
            }

            RefreshSignalCardUI();
            yield return null;
        }

        signalActive = false;
        if (signalCard != null) signalCard.gameObject.SetActive(false);
        if (beacon != null)
        {
            beacon.Face(targetCity);
            beacon.ManualMode = false;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        if (player != null) player.PlayEmote("wave", 2);
        FinishLighthouseNow();
    }

    void TriggerSignalFlash()
    {
        signalFlashes = Mathf.Min(3, signalFlashes + 1);
        TideTownAchievementSystem.Unlock("cam_wave");
        if (activeBeacon != null)
        {
            activeBeacon.Face(EndingCityPos());
            activeBeacon.PulseFlash();
        }
        if (player != null) player.PlayEmote(signalFlashes >= 3 ? "wave" : "hop");
        if (game != null) game.SetExpression(signalFlashes >= 3 ? ExprId.happy : ExprId.excited);

        int total = activeReply != null && activeReply.Total > 0 ? activeReply.Total : 40;
        int litTarget = signalFlashes == 1 ? Mathf.CeilToInt(total * 0.33f)
                      : signalFlashes == 2 ? Mathf.CeilToInt(total * 0.68f)
                      : total;
        if (activeReply != null) activeReply.RevealUpTo(litTarget);

        RefreshSignalCardUI();

        if (ui != null)
        {
            string msg = signalFlashes == 1
                ? "闪灯信号 1/3 · 对岸山头亮起了第一批手电光！"
                : signalFlashes == 2
                    ? "闪灯信号 2/3 · 越来越多的手电筒在山那边闪烁回应！"
                    : "闪灯信号 3/3 · 整座山头 40 盏手电全部亮起，他们看见你了！";
            ui.ShowToast(msg, 3.6f);
        }
    }

    System.Collections.IEnumerator FinishLighthouse(FlashlightReply reply)
    {
        float t0 = Time.realtimeSinceStartup;
        while (reply != null && !reply.Done && Time.realtimeSinceStartup - t0 < 30f) yield return null;
        FinishLighthouseNow();
    }

    public void FinishLighthouseNow()
    {
        if (State.flags.Contains("lighthouse_done")) return;
        State.flags.Add("lighthouse_done");
        if (!State.stamps.Contains("lighthouse")) State.stamps.Add("lighthouse");
        store.Save(State);
        if (water != null) water.SetLevel("L3", WaterLevel.StationRiseSeconds);
        if (game != null) game.SetExpression(ExprId.happy);
        AdvanceTo("L5_reply");
        ui.ShowToast("邮戳 3/3 · 灯塔");
    }

}
