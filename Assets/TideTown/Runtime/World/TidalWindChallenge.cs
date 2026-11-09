// 方向 B：潮汐海风浪涌与护送重心平衡挑战（TidalWindChallenge）。
// 具备真实的【重心偏移进度槽】与【失衡惩罚机制】：
//   1. 当蛋仔抱着柴火、火柴、红球、油壶或经过涨潮渡口时，每隔一段时间会刮起持续 4.8 秒的强海风；
//   2. 强海风会持续把蛋仔往路边/水边推，并使顶部「重心偏移度」迅速逼近 100%；
//   3. 玩家必须【按住 Shift 压低重心】并【连按空格键（或逆风按 A/D）】把偏移度压回安全区；
//   4. 【真实失败惩罚】：一旦重心偏移达到 100% 失衡，蛋仔会被狂风吹翻退回上一段安全起点，且手里的柴火/火柴/红球/油壶会被吹落打湿，必须重新回去拾取！
using UnityEngine;
using UnityEngine.UI;

public sealed class TidalWindChallenge : MonoBehaviour
{
    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    GameState state;
    StoryDirector director;

    float cooldown = 9.0f;
    float warnTimer;
    float gustTimer;
    const float GustDuration = 4.8f;
    float boostTimer;

    Vector3 windDir = new Vector3(1f, 0f, 0f);
    Vector3 safeAnchorPos;
    float imbalance; // 0.0 (稳固) -> 1.0 (彻底失衡受罚！)

    RectTransform windBanner;
    RectTransform barFillRect;
    Image barFillImg;
    Text windTitleText;
    Text windSubText;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
    {
        player = p;
        ui = u;
        world = w;
        state = s;
        director = FindObjectOfType<StoryDirector>();
        if (player != null) safeAnchorPos = player.transform.position;
    }

    void EnsureBannerUI()
    {
        if (windBanner != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (root == null) return;

        var img = TideTownStyleUI.CreatePanel(root, "TidalWindBanner", 520f, 68f, TideTownStyleUI.BarkPanel, true);
        img.raycastTarget = false;
        windBanner = img.rectTransform;
        windBanner.anchorMin = windBanner.anchorMax = new Vector2(0.5f, 1f);
        windBanner.pivot = new Vector2(0.5f, 1f);
        windBanner.anchoredPosition = new Vector2(0f, -16f);

        windTitleText = TideTownStyleUI.CreateText(
            windBanner,
            "",
            15,
            TideTownStyleUI.CreamText,
            TextAnchor.MiddleCenter,
            new Vector2(0f, 18f),
            new Vector2(490f, 24f),
            true);

        windSubText = TideTownStyleUI.CreateText(
            windBanner,
            "",
            12,
            TideTownStyleUI.AmberSoft,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -2f),
            new Vector2(490f, 20f),
            false);

        // 底部重心偏移进度槽背景
        var barBg = TideTownStyleUI.CreatePanel(windBanner, "BalanceBarBg", 460f, 10f, new Color32(35, 24, 15, 220), true);
        barBg.raycastTarget = false;
        barBg.rectTransform.anchorMin = barBg.rectTransform.anchorMax = barBg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        barBg.rectTransform.anchoredPosition = new Vector2(0f, -21f);

        barFillImg = TideTownStyleUI.CreatePanel(barBg.transform, "BalanceBarFill", 460f, 10f, TideTownStyleUI.AmberAccent, true);
        barFillImg.raycastTarget = false;
        barFillRect = barFillImg.rectTransform;
        barFillRect.anchorMin = new Vector2(0f, 0.5f);
        barFillRect.anchorMax = new Vector2(0f, 0.5f);
        barFillRect.pivot = new Vector2(0f, 0.5f);
        barFillRect.anchoredPosition = Vector2.zero;

        windBanner.gameObject.SetActive(false);
    }

    bool IsChallengeContextActive()
    {
        if (player == null || ui == null || world == null || state == null) return false;
        if (!ui.inputClose) return false;
        if (ui.TitleShowing || ui.Paused || ui.LetterOpen || player.locked) return false;
        if (ObjectInspectModal.IsOpen || WoodenMechanismPuzzleModal.IsOpen || AvantGardeGameplayHub.AnyModalOpen) return false;

        bool carryingItem = !string.IsNullOrEmpty(state.carrying);
        bool tideStage =
            state.step == "B2_wood" || state.step == "B3_load" ||
            state.step == "S2_ball" || state.step == "S3_bowl" ||
            state.step == "L0_cross" || state.step == "L1_shed" || state.step == "L2_door" || state.step == "E0_back";
        return carryingItem || tideStage;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || dt > 0.2f) return;

        EnsureBannerUI();
        if (director == null) director = FindObjectOfType<StoryDirector>();

        if (!IsChallengeContextActive())
        {
            warnTimer = 0f;
            gustTimer = 0f;
            imbalance = 0f;
            if (windBanner != null && windBanner.gameObject.activeSelf)
                windBanner.gameObject.SetActive(false);
            return;
        }

        // 顺风奖励阶段
        if (boostTimer > 0f)
        {
            boostTimer -= dt;
            if (player.Speed > 0.3f)
            {
                float yaw = player.yaw;
                Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                Vector3 next = player.transform.position + fwd * (0.85f * dt);
                if (world.Walkable(next.x, next.z))
                {
                    next.y = world.GroundAt(next.x, next.z, next.y);
                    player.transform.position = next;
                }
            }
            UpdateBanner(
                "顺风轻快步伐奖励生效中（剩余 " + boostTimer.ToString("F1") + " 秒）",
                "你成功顶住了狂风，护住了手里的信物！",
                0f,
                false);
            return;
        }

        // 狂风预警阶段（1.8 秒）
        if (warnTimer > 0f)
        {
            warnTimer -= dt;
            bool preBracing = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            imbalance = preBracing ? 0.05f : 0.22f;

            UpdateBanner(
                "【狂风潮涌预警】强海风即将袭来！（" + warnTimer.ToString("F1") + "s）",
                "请立即按住 Shift 压低重心，并准备连按【空格键】顶风稳住脚步，否则会被吹飞掉落物品！",
                imbalance,
                false);

            if (warnTimer <= 0f)
            {
                gustTimer = GustDuration;
                ShadowAndChimeSystem.PlayProceduralChime(311.13f, 0.25f);
            }
            return;
        }

        // 狂风吹袭与重心对抗阶段（持续 4.8 秒）
        if (gustTimer > 0f)
        {
            gustTimer -= dt;

            bool holdingShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool tappedSpace = Input.GetKeyDown(KeyCode.Space);
            bool sensorStabilizing = HardwareAndSensorHub.Instance != null && HardwareAndSensorHub.Instance.ExternalWindStabilizeBoost > 0.15f;
            if (sensorStabilizing) holdingShift = true;

            // 检查玩家是否在逆风方向按键对抗
            Vector3 inputMove = Vector3.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputMove.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputMove.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inputMove.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inputMove.z -= 1f;
            bool counterSteering = sensorStabilizing || (inputMove.sqrMagnitude > 0.1f && Vector3.Dot(inputMove.normalized, windDir) < -0.15f);

            // 计算重心偏移度变化：不按键时 2.2 秒即失衡满格触发惩罚！
            if (tappedSpace)
            {
                imbalance = Mathf.Max(0f, imbalance - 0.22f);
            }

            if (holdingShift && counterSteering)
            {
                imbalance = Mathf.Max(0f, imbalance - 0.36f * dt);
            }
            else if (holdingShift || counterSteering)
            {
                imbalance = Mathf.Clamp01(imbalance + 0.06f * dt);
            }
            else
            {
                // 完全不防备：重心迅速失控，且被强风以 2.15 m/s 猛烈推离路线！
                imbalance = Mathf.Clamp01(imbalance + 0.44f * dt);
            }

            float pushSpeed = holdingShift ? 0.45f : 2.15f;
            Vector3 push = windDir * (pushSpeed * dt);
            Vector3 next = player.transform.position + push;
            if (world.Walkable(next.x, next.z))
            {
                next.y = world.GroundAt(next.x, next.z, next.y);
                player.transform.position = next;
            }
            else if (!holdingShift)
            {
                // 被吹到路沿边缘撞墙/悬崖边时，失衡加速
                imbalance = Mathf.Clamp01(imbalance + 0.25f * dt);
            }

            int imbPct = Mathf.RoundToInt(imbalance * 100f);
            string statusLine = holdingShift
                ? "重心对抗中（剩余 " + gustTimer.ToString("F1") + "s） · 失衡度：" + imbPct + "%（连按空格可快速稳住！）"
                : "【危险！正在被狂风吹跑】失衡度：" + imbPct + "%（快按住 Shift + 连按空格稳住！）";

            string subLine = !string.IsNullOrEmpty(state.carrying)
                ? "警告：失衡度达 100% 将被吹退回起点，且手上的物品会被吹落打湿！"
                : "警告：失衡度达 100% 将被狂风巨浪吹翻退回上一段安全处！";

            UpdateBanner(statusLine, subLine, imbalance, imbalance >= 0.65f);

            // 触发真实失败惩罚！
            if (imbalance >= 1.0f)
            {
                TriggerGaleFailurePenalty();
                return;
            }

            // 顺利撑过 4.8 秒强风！
            if (gustTimer <= 0f)
            {
                if (windBanner != null) windBanner.gameObject.SetActive(false);
                cooldown = 12.5f;
                imbalance = 0f;
                boostTimer = 3.5f;
                player.PlayEmote("hop");
                ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
                ui.ShowToast("成功顶住狂风稳住重心！手上的物品完好无损，获得 3.5 秒顺风疾行！", 3.5f);
            }
            return;
        }

        if (windBanner != null && windBanner.gameObject.activeSelf)
            windBanner.gameObject.SetActive(false);

        // 平常行走时记录安全锚点，并倒计时下一次海风袭击
        if (player.Speed > 0.22f)
        {
            if (player.grounded && world.Walkable(player.transform.position.x, player.transform.position.z))
            {
                // 每隔一段距离刷新狂风前的退回锚点
                if (Vector3.Distance(player.transform.position, safeAnchorPos) > 4.8f)
                    safeAnchorPos = player.transform.position;
            }

            cooldown -= dt;
            if (cooldown <= 0f)
            {
                warnTimer = 1.8f;
                imbalance = 0.15f;
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                windDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
                ShadowAndChimeSystem.PlayProceduralChime(440f, 0.2f);
            }
        }
    }

    void TriggerGaleFailurePenalty()
    {
        gustTimer = 0f;
        warnTimer = 0f;
        imbalance = 0f;
        cooldown = 11.0f;
        if (windBanner != null) windBanner.gameObject.SetActive(false);

        ShadowAndChimeSystem.PlayProceduralChime(185f, 0.45f);

        // 1. 将玩家吹退回安全锚点（或顺着风向往回退 4 米）
        if (player != null && world != null)
        {
            Vector3 targetPos = safeAnchorPos;
            if (Vector3.Distance(player.transform.position, targetPos) < 1.5f)
            {
                float yaw = player.yaw;
                Vector3 back = player.transform.position - new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 3.8f;
                if (world.Walkable(back.x, back.z))
                {
                    back.y = world.GroundAt(back.x, back.z, back.y);
                    targetPos = back;
                }
            }
            if (world.Walkable(targetPos.x, targetPos.z))
            {
                targetPos.y = world.GroundAt(targetPos.x, targetPos.z, targetPos.y) + 0.02f;
                player.transform.position = targetPos;
                player.vel = Vector3.zero;
            }
        }

        // 2. 如果手里拿着柴火/火柴/红球/油壶，直接触发掉落/打湿惩罚，必须重新回去拿！
        string msg = "【失衡惩罚】你没能稳住重心，被强海风吹翻退回了安全起点！";
        if (director != null)
        {
            director.OnGalePenaltyDropItem(out msg);
        }
        if (ui != null)
        {
            ui.ShowToast(msg, 4.8f);
        }
    }

    void UpdateBanner(string title, string sub, float fill01, bool dangerRed)
    {
        if (windBanner == null) return;
        if (!windBanner.gameObject.activeSelf) windBanner.gameObject.SetActive(true);
        if (windTitleText != null) windTitleText.text = title;
        if (windSubText != null) windSubText.text = sub;
        if (barFillRect != null)
            barFillRect.sizeDelta = new Vector2(Mathf.Clamp(460f * Mathf.Clamp01(fill01), 6f, 460f), 10f);
        if (barFillImg != null)
            barFillImg.color = dangerRed ? new Color32(235, 85, 65, 255) : TideTownStyleUI.AmberAccent;
    }
}
