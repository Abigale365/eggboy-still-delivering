// 场景级 3D 机关、三站「心愿展台」、退潮漂流瓶与渡口多解过海总控（WorldMechanismDirector）。
// 包含：
//   1. 四大可动 3D 机关（M1 鲁班三环风门、M2 小黑爪印木匣、M3 潮汐船坞齿轮绞盘、M4 守塔人黄铜三轴星盘）；
//   2. 涨潮渡口三种过海解法与对应“嘀嘟！”成就：
//      - 方案 A：摇动岸边【3D 潮汐船坞齿轮绞盘】升起木码头与浮桥小船 -> 成就【老船坞工 · 齿轮绞盘升桥】；
//      - 方案 B：在「心愿 3D 造物笺」按个人喜好实时生成 3D 渡海载具（大黄鸭浮船/海龟浮台/木筏） -> 成就【凭空造舟 · 乘心愿渡海】；
//      - 方案 C：唤出十秒前的潮汐残影协力摇住绞盘 -> 成就【时空旅人 · 与过去的自己接力】；
//   3. 三大站点「潮汐心愿展台」（面包房橱窗、学校操场、灯塔观景台）：玩家按喜好实时生成的 3D 作品将永久陈列在展台上并保留至结局镜头 -> 成就【筑梦邮差 · 焕然一新的潮汐镇】；
//   4. 退潮浅滩 3 只发光潮汐漂流瓶：拾取阅读镇民留下的海边手札 -> 成就【拾潮之人 · 听见大海的回信】。
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldMechanismDirector : MonoBehaviour
{
    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;

    Transform bakeryMechRoot;
    readonly Transform[] bakeryRings = new Transform[3];

    Transform schoolMechRoot;
    readonly Transform[] schoolRings = new Transform[3];

    Transform lighthouseMechRoot;
    readonly Transform[] lighthouseRings = new Transform[3];

    Transform winchRoot;
    Transform winchRing;
    Transform winchSpindle;
    Transform pontoonBridgeRoot;
    Vector3 winchWorldPos;
    Vector3 crossingChannelCenter;
    float pontoonBaseY;
    float winchSpinAngle;
    bool winchRaised;
    bool nearWinchPromptShown;
    float surgePenaltyCooldown;

    // 三站心愿展台
    readonly Transform[] wishPedestals = new Transform[3];
    readonly Vector3[] wishPedestalPositions = new Vector3[3];

    // 退潮浅滩漂流瓶
    readonly List<Transform> driftBottles = new List<Transform>();
    readonly string[] driftBottleNotes =
    {
        "漂流瓶里的第一张海盐纸条：\n『阿婆烤炉的风门最怕急脾气。先把上面那根牵一发而动全身的中间木栓拨开，再顺着菜谱、台秤和藤篮底下的数字慢慢对。炉膛点着后，给它吹一口暖气，火就旺了。』",
        "漂流瓶里的第二张折角信笺：\n『学校的小黑其实最好哄了。要是你懒得去秋千底下找那颗旧红球，不妨翻开随身的信笺，画一根香喷喷的烤骨头、或者一只新飞盘送进它的碗里。』",
        "漂流瓶里的第三张航标残页：\n『涨潮的时候渡口水急，摇动岸边的黄铜齿轮绞盘就能把旧木船拉起来；当然，你要是愿意在心愿笺上造一艘属于自己的船渡海，大海也会托起你的心意。』"
    };

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        player = p;
        ui = u;
        world = w;
        director = d;
        state = s;
        winchRaised = state != null && state.flags.Contains("dock_winch_raised");

        SpawnBakerySceneMechanism();
        SpawnSchoolSceneMechanism();
        SpawnFloodedDockWinchAndPontoon();
        SpawnLighthouseSceneMechanism();
        SpawnStationWishPedestals();
        SpawnTidalDriftBottles();
    }

    void SpawnBakerySceneMechanism()
    {
        var ovenGo = GameObject.Find("Bakery_oven");
        Vector3 basePos = ovenGo != null ? ovenGo.transform.position : new Vector3(-11.8f, 1.3f, -7.8f);
        if (world != null) basePos.y = world.GroundAt(basePos.x, basePos.z, basePos.y);

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M1_鲁班三环风门机关", 0.68f);
        mech.name = "Scene_M1_鲁班三环风门机关";
        mech.transform.SetParent(transform, false);
        mech.transform.position = basePos + new Vector3(1.08f, 0.92f, 0.42f);
        mech.transform.rotation = Quaternion.Euler(0f, 145f, 0f);
        bakeryMechRoot = mech.transform;
        for (int i = 0; i < 3; i++)
            bakeryRings[i] = TideTownStyleUI.FindDeepChild(bakeryMechRoot, "Ring_" + i);

        var rack = TideTownStyleUI.CloneSceneLux3D("S11_晾衣架_分享包版", 1.65f, false);
        if (rack != null)
        {
            Vector3 rp = basePos + new Vector3(-2.15f, 0f, 1.15f);
            if (world != null) rp.y = world.GroundAt(rp.x, rp.z, basePos.y);
            rack.transform.SetParent(transform, false);
            rack.transform.position = rp;
            rack.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
        }

        var buoy = TideTownStyleUI.CloneSceneLux3D("D6_救生圈_v2", 0.58f, false);
        if (buoy != null)
        {
            buoy.transform.SetParent(mech.transform, false);
            buoy.transform.localPosition = new Vector3(0f, -0.58f, -0.08f);
        }

        wishPedestalPositions[0] = basePos + new Vector3(2.4f, 0f, 1.8f);
        if (world != null) wishPedestalPositions[0].y = world.GroundAt(wishPedestalPositions[0].x, wishPedestalPositions[0].z, basePos.y);
    }

    void SpawnSchoolSceneMechanism()
    {
        var bowlGo = GameObject.Find("S_bowl");
        if (bowlGo == null) bowlGo = GameObject.Find("S_doghouse");
        Vector3 basePos = bowlGo != null ? bowlGo.transform.position : new Vector3(17.5f, 3.05f, -5.2f);
        if (world != null) basePos.y = world.GroundAt(basePos.x, basePos.z, basePos.y);

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M2_小黑爪印转盘木匣", 0.62f);
        mech.name = "Scene_M2_小黑爪印转盘木匣";
        mech.transform.SetParent(transform, false);
        mech.transform.position = basePos + new Vector3(0.76f, 0.55f, -0.24f);
        mech.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
        schoolMechRoot = mech.transform;
        for (int i = 0; i < 3; i++)
            schoolRings[i] = TideTownStyleUI.FindDeepChild(schoolMechRoot, "Ring_" + i);

        wishPedestalPositions[1] = basePos + new Vector3(-2.6f, 0f, 2.2f);
        if (world != null) wishPedestalPositions[1].y = world.GroundAt(wishPedestalPositions[1].x, wishPedestalPositions[1].z, basePos.y);
    }

    void SpawnFloodedDockWinchAndPontoon()
    {
        var crate0 = GameObject.Find("crate_0");
        var oilGo = GameObject.Find("LH_oil");
        Vector3 cPos = crate0 != null
            ? crate0.transform.position
            : (oilGo != null ? oilGo.transform.position + new Vector3(-3.2f, 0f, -2.5f) : new Vector3(14.5f, 1.35f, 10.5f));

        crossingChannelCenter = cPos + new Vector3(0.8f, 0f, 1.2f);
        winchWorldPos = cPos + new Vector3(-1.55f, 0f, -1.35f);
        if (world != null)
            winchWorldPos.y = world.GroundAt(winchWorldPos.x, winchWorldPos.z, cPos.y + 0.3f);

        var winchGo = new GameObject("Scene_M3_潮汐船坞齿轮绞盘_总成");
        winchGo.transform.SetParent(transform, false);
        winchGo.transform.position = winchWorldPos;
        winchGo.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
        winchRoot = winchGo.transform;

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M3_潮汐船坞齿轮绞盘", 0.88f);
        mech.transform.SetParent(winchRoot, false);
        mech.transform.localPosition = new Vector3(0f, 0.78f, 0f);
        winchRing = TideTownStyleUI.FindDeepChild(mech.transform, "Ring_0");
        winchSpindle = TideTownStyleUI.FindDeepChild(mech.transform, "Core_Spindle");

        var buoy = TideTownStyleUI.CloneSceneLux3D("D6_救生圈_v2", 0.55f, false);
        if (buoy != null)
        {
            buoy.transform.SetParent(winchRoot, false);
            buoy.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        }

        var pontoonGo = new GameObject("Scene_DockPontoonBridge");
        pontoonGo.transform.SetParent(transform, false);
        pontoonBaseY = cPos.y - 0.06f;
        pontoonGo.transform.position = new Vector3(
            crossingChannelCenter.x,
            winchRaised ? pontoonBaseY : (pontoonBaseY - 0.68f),
            crossingChannelCenter.z);
        pontoonBridgeRoot = pontoonGo.transform;

        var dock = TideTownStyleUI.CloneSceneLux3D("S8_木码头_分享包版", 2.4f, true);
        if (dock != null)
        {
            dock.transform.SetParent(pontoonBridgeRoot, false);
            dock.transform.localPosition = new Vector3(-0.4f, 0f, -0.3f);
            dock.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);
        }

        var boat = TideTownStyleUI.CloneSceneLux3D("S9_小船_分享包版", 2.1f, true);
        if (boat != null)
        {
            boat.transform.SetParent(pontoonBridgeRoot, false);
            boat.transform.localPosition = new Vector3(0.95f, 0.04f, 0.65f);
            boat.transform.localRotation = Quaternion.Euler(0f, -62f, 0f);
        }

        var oar = TideTownStyleUI.CloneSceneLux3D("D4O_船桨_v2", 1.15f, false);
        if (oar != null)
        {
            oar.transform.SetParent(pontoonBridgeRoot, false);
            oar.transform.localPosition = new Vector3(0.85f, 0.32f, 0.65f);
            oar.transform.localRotation = Quaternion.Euler(18f, 15f, 72f);
        }
    }

    void SpawnLighthouseSceneMechanism()
    {
        var lampGo = GameObject.Find("LH_lamp");
        if (lampGo == null) lampGo = GameObject.Find("LH_scope");
        Vector3 basePos = lampGo != null ? lampGo.transform.position : new Vector3(22.18f, 16.98f, 3.3f);

        var mech = TideTownStyleUI.BuildArticulatedMechanism3D("M4_守塔人黄铜三轴星盘", 0.78f);
        mech.name = "Scene_M4_守塔人黄铜三轴星盘";
        mech.transform.SetParent(transform, false);
        mech.transform.position = basePos + new Vector3(-0.92f, 0.45f, -0.42f);
        mech.transform.rotation = Quaternion.Euler(0f, -120f, 0f);
        lighthouseMechRoot = mech.transform;
        for (int i = 0; i < 3; i++)
            lighthouseRings[i] = TideTownStyleUI.FindDeepChild(lighthouseMechRoot, "Ring_" + i);

        var lampHead = TideTownStyleUI.CloneSceneLux3D("B9_灯塔灯头_v4", 0.85f, false);
        if (lampHead != null)
        {
            lampHead.transform.SetParent(mech.transform, false);
            lampHead.transform.localPosition = new Vector3(0f, -0.68f, 0f);
        }

        wishPedestalPositions[2] = basePos + new Vector3(0.95f, -0.35f, -0.85f);
    }

    void SpawnStationWishPedestals()
    {
        string[] pedestalNames = { "WishPedestal_Bakery", "WishPedestal_School", "WishPedestal_Lighthouse" };
        for (int i = 0; i < 3; i++)
        {
            var pGo = new GameObject(pedestalNames[i]);
            pGo.transform.SetParent(transform, false);
            pGo.transform.position = wishPedestalPositions[i];

            var baseModel = TideTownStyleUI.CloneSceneLux3D("S6_路灯_分享包版", 0.65f, false);
            if (baseModel != null)
            {
                baseModel.transform.SetParent(pGo.transform, false);
                baseModel.transform.localScale = new Vector3(1.1f, 0.32f, 1.1f);
            }
            wishPedestals[i] = pGo.transform;
        }
    }

    void SpawnTidalDriftBottles()
    {
        Vector3[] pts =
        {
            new Vector3(-7.2f, 1.4f, -3.5f),
            new Vector3(11.4f, 2.2f, -6.2f),
            winchWorldPos + new Vector3(-1.2f, 0f, -0.8f)
        };

        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 p = pts[i];
            if (world != null) p.y = world.GroundAt(p.x, p.z, p.y);
            var bGo = new GameObject("TidalDriftBottle_" + i);
            bGo.transform.SetParent(transform, false);
            bGo.transform.position = p;

            var jar = TideTownStyleUI.CloneSceneLux3D("A4_果酱罐_v2", 0.42f, false);
            if (jar != null)
            {
                jar.transform.SetParent(bGo.transform, false);
                jar.transform.localRotation = Quaternion.Euler(68f, i * 55f, 15f);
            }

            var lGo = new GameObject("BottleGlow");
            lGo.transform.SetParent(bGo.transform, false);
            lGo.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            var l = lGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color32(135, 225, 255, 255);
            l.range = 2.6f;
            l.intensity = 1.6f;

            driftBottles.Add(bGo.transform);
        }
    }

    public Vector3 GetDogBowlPosition()
    {
        var bowlGo = GameObject.Find("S_bowl");
        return bowlGo != null ? bowlGo.transform.position : new Vector3(17.5f, 3.05f, -5.2f);
    }

    public Vector3 GetCrossingChannelCenter() => crossingChannelCenter;

    public Transform GetNearestWishPedestal(Vector3 pos, out float dist)
    {
        Transform best = wishPedestals[0];
        dist = float.MaxValue;
        for (int i = 0; i < wishPedestals.Length; i++)
        {
            if (wishPedestals[i] == null) continue;
            float d = Vector3.Distance(pos, wishPedestals[i].position);
            if (d < dist)
            {
                dist = d;
                best = wishPedestals[i];
            }
        }
        return best;
    }

    public bool TryEchoAssistWinch(Vector3 actorPos)
    {
        if (winchRoot == null) return false;
        if (Vector3.Distance(actorPos, winchWorldPos) < 12f)
        {
            RaiseDockWinch(true, false);
            return true;
        }
        return false;
    }

    public void RaiseDockWinch(bool byEcho, bool byWishBoat = false)
    {
        winchRaised = true;
        winchSpinAngle += 720f;
        if (state != null && !state.flags.Contains("dock_winch_raised"))
            state.flags.Add("dock_winch_raised");

        ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.25f);
        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.4f);
        if (player != null) player.PlayEmote("hop");

        if (byWishBoat)
        {
            TideTownAchievementSystem.Unlock("wish_boat");
        }
        else if (byEcho)
        {
            TideTownAchievementSystem.Unlock("echo_coop");
            if (ui != null)
                ui.ShowToast("十秒前的潮汐残影与你协力摇动了【3D 潮汐船坞齿轮绞盘】，浮桥已升起！", 3.8f);
        }
        else
        {
            TideTownAchievementSystem.Unlock("winch_bridge");
            if (ui != null)
                ui.ShowToast("咔哒——你摇动了【3D 潮汐船坞齿轮绞盘】，木码头与小船浮桥已从海水中升起！", 3.8f);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (surgePenaltyCooldown > 0f) surgePenaltyCooldown -= dt;

        bool bakerySolved = state != null && (state.flags.Contains("oven_lit") || state.flags.Contains("puzzle_bakery_lock"));
        AnimateRings(bakeryRings, bakerySolved ? 55f : 12f, dt);

        bool schoolSolved = state != null && (state.flags.Contains("ball_placed") || state.flags.Contains("puzzle_school_lock"));
        AnimateRings(schoolRings, schoolSolved ? 60f : 10f, dt);

        bool lhSolved = state != null && (state.flags.Contains("beam_lit") || state.flags.Contains("puzzle_lighthouse_lock"));
        AnimateRings(lighthouseRings, lhSolved ? 75f : 15f, dt);

        if (winchRing != null)
        {
            float targetZ = winchRaised ? -winchSpinAngle - Time.time * 18f : -15f;
            winchRing.localRotation = Quaternion.Slerp(winchRing.localRotation, Quaternion.Euler(0f, 0f, targetZ), dt * 6f);
        }
        if (winchSpindle != null)
        {
            float targetZ = winchRaised ? winchSpinAngle : 0f;
            winchSpindle.localRotation = Quaternion.Slerp(winchSpindle.localRotation, Quaternion.Euler(0f, 0f, targetZ), dt * 6f);
        }
        if (pontoonBridgeRoot != null)
        {
            float bob = winchRaised ? Mathf.Sin(Time.time * 2.1f) * 0.04f : -0.68f;
            Vector3 targetPos = new Vector3(crossingChannelCenter.x, pontoonBaseY + bob, crossingChannelCenter.z);
            pontoonBridgeRoot.position = Vector3.Lerp(pontoonBridgeRoot.position, targetPos, dt * 4.5f);
        }

        if (player == null || ui == null || !ui.inputClose || player.locked || ui.TitleShowing || ui.Paused || ui.LetterOpen)
            return;

        // 1. 拾取阅读退潮浅滩的漂流瓶（按 E）
        for (int i = 0; i < driftBottles.Count; i++)
        {
            var b = driftBottles[i];
            if (b == null || !b.gameObject.activeSelf) continue;
            if (Vector3.Distance(player.transform.position, b.position) < 2.2f && Input.GetKeyDown(KeyCode.E))
            {
                b.gameObject.SetActive(false);
                ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
                ui.ShowToast(driftBottleNotes[i % driftBottleNotes.Length], 5.5f);
                TideTownAchievementSystem.Unlock("drift_bottles");
                return;
            }
        }

        // 2. 岸边摇动 3D 潮汐船坞齿轮绞盘（纯环境化反馈，不弹直白教学命令）
        float distToWinch = Vector3.Distance(player.transform.position, winchWorldPos);
        if (distToWinch < 2.9f)
        {
            if (!nearWinchPromptShown && !winchRaised)
            {
                nearWinchPromptShown = true;
                ui.ShowToast("岸边的黄铜齿轮绞盘连着水下的粗缆绳，似乎能把沉没的旧栈桥拉起来。", 3.2f);
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                RaiseDockWinch(false, false);
            }
        }
        else
        {
            nearWinchPromptShown = false;
        }

        // 3. 未升起浮桥且未造船时强行涉水：急流冲退回岸边
        if (!winchRaised && surgePenaltyCooldown <= 0f)
        {
            Vector2 pXZ = new Vector2(player.transform.position.x, player.transform.position.z);
            Vector2 cXZ = new Vector2(crossingChannelCenter.x, crossingChannelCenter.z);
            if (Vector2.Distance(pXZ, cXZ) < 1.95f && Mathf.Abs(player.transform.position.y - pontoonBaseY) < 1.3f)
            {
                surgePenaltyCooldown = 2.5f;
                ShadowAndChimeSystem.PlayProceduralChime(196f, 0.4f);
                Vector3 safePos = winchWorldPos + new Vector3(-0.4f, 0.05f, -0.4f);
                if (world != null && world.Walkable(safePos.x, safePos.z))
                    safePos.y = world.GroundAt(safePos.x, safePos.z, safePos.y) + 0.02f;
                player.transform.position = safePos;
                player.vel = Vector3.zero;

                string dropExtra = "";
                if (state != null && state.carrying == "oil" && director != null)
                {
                    director.OnGalePenaltyDropItem(out dropExtra);
                }

                ui.ShowToast("海水漫过了石滩，湍急的暗流将你推回了岸边栓船桩旁。", 3.8f);
            }
        }
    }

    static void AnimateRings(Transform[] rings, float speedDeg, float dt)
    {
        if (rings == null) return;
        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] == null) continue;
            float dir = (i % 2 == 0) ? 1f : -1.35f;
            rings[i].Rotate(0f, 0f, speedDeg * dir * dt, Space.Self);
        }
    }
}
