// WP-07 剧情推进，WP-11 起覆盖面包店：到点自动读信、火柴/柴火/烤炉链条、回忆线索 3D 检视、鲁班三环风门木匣、三段鼓风助燃和第一枚邮戳。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
    public bool IsAuditMode => ui == null || !ui.inputClose;

    public void TakeMatchesAt(Vector3 pos)
    {
        if (!ready || State.flags.Contains("matches_taken")) return;
        if (BeginPhysical(new PhysicalPickupController.Job
            { clip = "pickup_matches", target = () => pos + Vector3.up * 0.8f, diameter = 0.08f, standDistance = 0.45f,
              contactTime = 0.35f, lockSeconds = 0.75f, tolerance = 0.10f, onContact = () => TakeMatchesCommit(false) })) return;
        TakeMatches();
    }

    public void TakeMatches() => TakeMatchesCommit(true);

    void TakeMatchesCommit(bool animate)
    {
        if (!ready || State.flags.Contains("matches_taken")) return;
        State.flags.Add("matches_taken");
        if (State.carrying != "wood") State.carrying = "matches";
        else if (!State.flags.Contains("matches_packed")) State.flags.Add("matches_packed");
        store.Save(State);
        if (animate) PlayAction("pickup_matches", ExprId.happy, 0.75f, 0.35f);
        else if (game != null) game.SetExpression(ExprId.happy);
        ui.SetCarried(CarryLabel());
        if (State.flags.Contains("wood_loaded")) AdvanceTo("B4_light");
        else if (State.carrying == "wood") AdvanceTo("B3_load");
        else AdvanceTo("B2_wood");
    }

    // 后院抱柴：走到柴火堆正面 0.45 m，伸手碰到柴捆那一帧（0.44 s）才抱起来
    public void TakeWoodAt(Vector3 pos)
    {
        if (!ready || (State.flags.Contains("wood_loaded") && !State.flags.Contains("wood_wet"))) return;
        if (BeginPhysical(new PhysicalPickupController.Job
            { clip = "pickup_wood", target = () => pos + Vector3.up * 0.18f, diameter = 0.30f, standDistance = 0.45f,
              contactTime = 0.44f, lockSeconds = 1.00f, onContact = () => TakeWoodCommit(false) })) return;
        if (player != null) player.FaceTowards(pos);
        TakeWood();
    }

    public void TakeWood() => TakeWoodCommit(true);

    void TakeWoodCommit(bool animate)
    {
        // 炉膛里是湿柴时，还可以再去抱一捆干的来换
        if (!ready || (State.flags.Contains("wood_loaded") && !State.flags.Contains("wood_wet"))) return;
        if (State.flags.Contains("matches_taken") && !State.flags.Contains("matches_packed")) State.flags.Add("matches_packed");
        State.carrying = "wood";
        store.Save(State);
        ui.SetCarried(CarryLabel());
        if (animate) PlayAction("pickup_wood", ExprId.happy, 1.00f, 0.44f, () => ShowCarriedModel("wood"));
        else ShowCarriedModel("wood");
        AdvanceTo("B3_load");
    }

    // 2026-11-16 门铃：走到门边，伸手碰到铃口那一帧铃身摆起来、叮当两声，然后端详
    public void RingBellAt(Vector3 pos, string label, string text)
    {
        if (!ready) return;
        var swing = GameObject.Find("Bakery_bell")?.GetComponent<BellSwing>();
        Vector3 touch = swing != null ? swing.TouchPoint : pos + Vector3.up * 0.76f;
        if (BeginPhysical(new PhysicalPickupController.Job
            { clip = "inspect_memory", target = () => touch, diameter = 0.20f, standDistance = 0.40f,
              contactTime = 0.38f, lockSeconds = 0.85f, tolerance = 0.12f,
              onContact = () => { if (swing != null) swing.Ring(); },
              onDone = () => InspectMemory("bell", label, text) })) return;
        if (swing != null) swing.Ring();
        InspectMemory("bell", label, text);
    }

    BakeryEffects ovenFx;
    BakeryEffects OvenFx => ovenFx != null ? ovenFx : (ovenFx = FindObjectOfType<BakeryEffects>());

    // 往炉膛里塞柴：走到炉口前，伸手到炉门开口那一帧（0.50 s）柴才离手、出现在炉膛里
    public void LoadWoodAt(Vector3 pos)
    {
        if (!ready || State.carrying != "wood") return;
        Vector3 mouth = OvenFx != null ? OvenFx.LoadPoint : pos + Vector3.up * 0.45f;
        if (BeginPhysical(new PhysicalPickupController.Job
            { clip = "load_wood", target = () => mouth, diameter = 0.30f, standDistance = 0.55f,
              contactTime = 0.50f, lockSeconds = 0.95f, onContact = () => LoadWoodCommit(false) })) return;
        LoadWood();
    }

    public void LoadWood() => LoadWoodCommit(true);

    void LoadWoodCommit(bool animate)
    {
        if (!ready || State.carrying != "wood") return;
        State.flags.Remove("wood_wet");
        if (!State.flags.Contains("wood_loaded")) State.flags.Add("wood_loaded");
        State.carrying = State.flags.Contains("matches_taken") ? "matches" : "";
        store.Save(State);
        ui.SetCarried(CarryLabel());
        if (animate) PlayAction("load_wood", ExprId.happy, 0.95f, 0.50f, () => ShowCarriedModel(null));
        else ShowCarriedModel(null);
        if (State.flags.Contains("matches_taken")) AdvanceTo("B4_light");
        else AdvanceTo("B1_matches");
    }

    public void LightOven(BakeryEffects fx)
    {
        if (!ready || !State.flags.Contains("wood_loaded") || !State.flags.Contains("matches_taken") || State.flags.Contains("oven_lit")) return;
        if (State.flags.Contains("wood_wet")) { WetWoodFizzle(fx); return; }
        WoodenMechanismPuzzleModal.OpenOrRun("bakery_lock", State, store, player, ui, gate, () => CompleteLightOven(fx));
    }

    void CompleteLightOven(BakeryEffects fx)
    {
        if (!ready || State.flags.Contains("oven_lit")) return;
        State.flags.Add("oven_lit");
        if (State.carrying == "matches") State.carrying = "";
        if (!State.stamps.Contains("bakery")) State.stamps.Add("bakery");
        store.Save(State);
        if (fx != null) fx.Light();
        if (water != null) water.SetLevel("L1", WaterLevel.StationRiseSeconds);
        PlayAction("light_stove_done", ExprId.excited, 0.80f, 0.25f);
        ui.SetCarried(CarryLabel());
        ui.ShowToast("邮戳 1/3 · 面包店 · 炉膛里的火苗刚刚燃起");
        AdvanceTo("S0_arrive");
    }

    // ---- 揭字成物（WP-D）：信上撕下来的「木柴」是湿的 ----

    public int WetFizzleCount { get; private set; }

    // 把一捆湿柴塞进炉膛：占住炉膛，但点不着。返回 false 表示炉膛里已经有柴了
    public bool LoadWetWood()
    {
        if (!ready || State.flags.Contains("oven_lit") || State.flags.Contains("wood_loaded")) return false;
        State.flags.Add("wood_loaded");
        State.flags.Add("wood_wet");
        store.Save(State);
        PlayAction("load_wood", ExprId.happy, 0.95f, 0.50f);
        if (State.flags.Contains("matches_taken")) AdvanceTo("B4_light");
        else AdvanceTo("B1_matches");
        return true;
    }

    // 干柴：炉膛里有湿柴就换掉，空着就直接塞进去。返回 true 表示这次起了作用
    public bool LoadDryWoodFromWord()
    {
        if (!ready || State.flags.Contains("oven_lit")) return false;
        if (State.flags.Contains("wood_wet")) { State.flags.Remove("wood_wet"); store.Save(State); ApplyStepGoal(); return true; }
        if (State.flags.Contains("wood_loaded")) return false;
        State.flags.Add("wood_loaded");
        store.Save(State);
        PlayAction("load_wood", ExprId.happy, 0.95f, 0.50f);
        if (State.flags.Contains("matches_taken")) AdvanceTo("B4_light");
        else AdvanceTo("B1_matches");
        return true;
    }

    // 湿柴点不着：一股白烟，EGGBOY 垮脸。不弹说明文字，炉子上的提示会变成"柴是湿的"
    void WetWoodFizzle(BakeryEffects fx)
    {
        WetFizzleCount++;
        if (!State.flags.Contains("wet_fizzled")) { State.flags.Add("wet_fizzled"); store.Save(State); }
        if (fx != null) fx.WetPuff();
        if (game != null) game.SetExpression(ExprId.sad);
        ShadowAndChimeSystem.PlayProceduralChime(185f, 0.35f);
    }

    public void BoostOvenBellows(BakeryEffects fx)
    {
        if (!ready || fx == null) return;
        int stage = fx.BoostBellows();
        TideTownAchievementSystem.Unlock("breath_fire");
        PlayAction(stage >= 3 ? "light_stove_done" : "load_wood", stage >= 3 ? ExprId.excited : ExprId.happy, 0.80f);
        if (ui != null)
        {
            string msg = stage == 1
                ? "呼——炉膛里的柴火噼啪作响，火苗蹿高了一截（1/3）"
                : stage == 2
                    ? "呼——烤炉彻底热了起来，暖意漫出屋檐（2/3）"
                    : "呼——浓密的炊烟笔直升起，顺着海风一直飘向山那边！（3/3）";
            ui.ShowToast(msg, 3.5f);
        }
    }

    public bool ShowMemory(string id, string text, bool replay)
    {
        if (!ready || ui == null || gate == null || gate.Locked) return false;
        if (!State.seenMemories.Contains(id))
        {
            State.seenMemories.Add(id);
            store.Save(State);
        }
        else if (!replay) return false;
        ui.ShowToast(text, 3f);
        return true;
    }

    public bool InspectMemory(string id, string label, string text)
    {
        bool ok = ShowMemory(id, text, true);
        if (ok)
        {
            PlayAction("inspect_memory", ExprId.curious, 0.65f, 0.40f);
            if (!IsAuditMode)
            {
                ObjectInspectModal.Open(id, label, text, State, store, player, ui, gate);
            }
        }
        return ok;
    }

    public void PickupDroppedItem(GameObject visual, bool keepVisual, System.Action take)
    {
        // 掉在地上的红球（keepVisual：球本身就在地上）：直接走红球的正面接触拾取，不先播一遍 pickup_drop
        if (keepVisual && Pickup != null && Pickup.Active("pickup_ball")) { take?.Invoke(); return; }
        PlayAction("pickup_drop", ExprId.happy, 0.85f, 0.42f, () =>
        {
            if (visual != null && !keepVisual) Destroy(visual);
            take?.Invoke();
        });
    }

    // 2026-11-16 持物重做（用户议题"抓握联动"）：
    //   · 柴火捆：挂在 body 骨头下的胸前锚点（CarryAnchor），左右居中、横在两臂之间，按蛋壳实际前沿往前让开不穿胸，走路跟着身体起伏；
    //   · 油壶：HandCarry 让壶顶提手每帧对准右手掌心，壶身竖直挂在身体右外侧，随手的移动像钟摆一样晃，不进蛋壳；
    //   · 原来是把场景里那件复制一份硬挂在主角根节点上（x +0.20 右偏），地上的原件还在。现在原件同步藏起来（SyncWorldProps）。
    void ShowCarriedModel(string item)
    {
        if (carriedVisual != null) Destroy(carriedVisual);
        carriedVisual = null;
        SyncCarryPose();
        SyncWorldProps();
        if (player == null || (item != "wood" && item != "oil")) return;
        modelProvider.Load(item, model =>
        {
            if (State == null || State.carrying != item) return;
            carriedVisual = Instantiate(model);
            carriedVisual.name = "Carried_" + item;
            carriedVisual.SetActive(true);
            foreach (var c in carriedVisual.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in carriedVisual.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            carriedVisual.transform.localScale = model.transform.lossyScale;
            if (item == "wood") PlaceWoodInArms(carriedVisual);
            else carriedVisual.AddComponent<HandCarry>().Init(player);
        }, message => Debug.LogWarning(message));
    }

    // 在胸前锚点自己的坐标系里摆（锚点挂在 body 骨头上，和蛋壳一起动）：拿起那一刻身体正前倾 / 侧转，按世界坐标摆会歪到一边
    void PlaceWoodInArms(GameObject v)
    {
        var anchor = player.CarryAnchor != null ? player.CarryAnchor : player.transform;
        v.transform.SetParent(anchor, false);
        // 锚点在主角本地 (0, 0.46, 0.28)，静止时轴向和主角一致；抱着的高度 0.42 → 锚点里 y = -0.04
        const float AnchorY = 0.46f, AnchorZ = 0.28f, HugY = 0.42f;
        var srcScale = island != null && island.woodModel != null ? island.woodModel.transform.lossyScale : Vector3.one;
        var anchorScale = anchor.lossyScale;
        v.transform.localScale = new Vector3(srcScale.x / anchorScale.x, srcScale.y / anchorScale.y, srcScale.z / anchorScale.z);
        v.transform.localRotation = Quaternion.identity;
        var lb = HandCarry.LocalBounds(v, anchor);
        if (lb.size.z > lb.size.x) { v.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); lb = HandCarry.LocalBounds(v, anchor); }
        var grip = PhysicalPickupController.Instance != null ? PhysicalPickupController.Instance.Grip : null;
        float front = grip != null
            ? grip.ShellFrontAt(anchor, HugY - AnchorY, Mathf.Max(0.12f, lb.size.x * 0.4f), lb.size.y * 0.5f)
            : 0.30f - AnchorZ;
        var want = new Vector3(0f, HugY - AnchorY, front + 0.02f + lb.size.z * 0.5f);
        v.transform.localPosition += want - lb.center;
    }

    // 地上的柴堆 / 船棚油壶 / 炉膛里的柴，跟着剧情状态显隐（只关渲染，不关物体：结局和谜题还会按名字找它们）
    string droppedPending = "";
    bool? woodPileShown, oilShown;
    void SyncWorldProps()
    {
        if (State == null) return;
        bool woodPile = State.carrying != "wood" && droppedPending != "wood"
            && !(State.flags.Contains("wood_loaded") && !State.flags.Contains("wood_wet"));
        bool oil = !State.flags.Contains("oil_taken") && droppedPending != "oil";
        if (woodPileShown != woodPile) { woodPileShown = woodPile; SetRenderers("Bakery_wood", woodPile); }
        if (oilShown != oil) { oilShown = oil; SetRenderers("LH_oil", oil); }
        if (OvenFx != null) OvenFx.SetLoadedWood(State.flags.Contains("wood_loaded"), State.flags.Contains("wood_wet"));
    }

    static void SetRenderers(string rootName, bool on)
    {
        var go = GameObject.Find(rootName);
        if (go == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
    }

    public void DebugSyncProps() => SyncWorldProps();
    public bool WoodPileVisible => woodPileShown ?? true;
    public bool OilCanVisible => oilShown ?? true;
    public GameObject CarriedVisual => carriedVisual;

    // ---- WP-B：海风里没站稳，手上的东西掉在脚边 ----
    // 剧情状态退回"去拿它"那一步（读档后东西回到原处，不会卡关）；地上留一件能按 E 捡回来的。
    public string LastDroppedItem { get; private set; } = "";
    public Vector3 LastDropPosition { get; private set; }

    public void DropCarriedNear(Vector3 at)
    {
        if (!ready || State == null) return;
        string item = State.carrying;
        if (item != "wood" && item != "oil" && item != "ball") return;
        Vector3 start = at;
        if (island != null) start.y = Mathf.Max(island.GroundAt(at.x, at.z, at.y + 1f), island.WaterY);
        Vector3 rest = SafeDropPoint(at);
        GameObject visual = null;
        System.Action pickup = null;

        if (item == "wood")
        {
            State.carrying = State.flags.Contains("matches_taken") ? "matches" : "";
            ShowCarriedModel(null);
            AdvanceTo(State.flags.Contains("matches_taken") ? "B2_wood" : "B1_matches");
            if (island != null && island.woodModel != null) visual = Instantiate(island.woodModel);
            droppedPending = "wood";
            pickup = () => { droppedPending = ""; TakeWood(); };
        }
        else if (item == "oil")
        {
            State.flags.Remove("oil_taken");
            State.carrying = "";
            ShowCarriedModel(null);
            AdvanceTo("L1_shed");
            if (island != null && island.oilModel != null) visual = Instantiate(island.oilModel);
            droppedPending = "oil";
            pickup = () => { droppedPending = ""; TakeOil(); };
        }
        else
        {
            State.flags.Remove("ball_taken");
            State.carrying = "";
            SyncCarryPose();
            AdvanceTo("S2_ball");
            if (Pickup != null) Pickup.ReleaseCarryBall(ball);   // 先松开右手的持球跟随，再让球掉下去
            if (ball != null) { ball.transform.SetParent(null, true); visual = ball.gameObject; }
            pickup = TakeBallAt;
        }
        store.Save(State);
        ui.SetCarried(CarryLabel());
        LastDroppedItem = item;
        LastDropPosition = rest;
        if (visual != null)
        {
            // 场景里的原件可能已经藏起来（关了渲染），掉在地上的这份要看得见
            visual.SetActive(true);
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            visual.transform.position = start;
            // 红球的位置是球心：落地时抬半个球（7 cm），不然半个球埋在地里，捡的时候手也伸到地底下
            StartCoroutine(DriftToRest(visual.transform, start, item == "ball" ? rest + Vector3.up * PickupGrip.DefaultDiameter * 0.5f : rest));
        }
        var svc = FindObjectOfType<InteractionService>();
        if (svc != null) svc.Register(new DroppedItemPickup(item, rest, this, visual, item == "ball", pickup));
    }

    // 掉进水里就漂到最近的岸边；本来就在地上就原地
    Vector3 SafeDropPoint(Vector3 at)
    {
        if (island == null) return at;
        if (island.Walkable(at.x, at.z)) { at.y = island.GroundAt(at.x, at.z, at.y + 1f); return at; }
        for (float r = 0.5f; r <= 5f; r += 0.5f)
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.PI / 8f;
                var q = at + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (island.Walkable(q.x, q.z)) { q.y = island.GroundAt(q.x, q.z, at.y + 1f); return q; }
            }
        Vector3 toward = player != null ? player.transform.position : at;
        toward.y = island.GroundAt(toward.x, toward.z, toward.y + 1f);
        return toward;
    }

    System.Collections.IEnumerator DriftToRest(Transform t, Vector3 from, Vector3 to)
    {
        float dur = Vector3.Distance(from, to) > 0.3f ? 1.4f : 0.35f;
        for (float e = 0f; e < dur && t != null; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / dur);
            t.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
            t.Rotate(Vector3.up, 200f * Time.deltaTime, Space.World);
            yield return null;
        }
        if (t != null) t.position = to;
    }

    // 真实失衡惩罚：狂风/潮涌中未能稳住重心时，手上的物品会被吹落或打湿，必须回去重新拾取
    public bool OnGalePenaltyDropItem(out string penaltyMessage)
    {
        penaltyMessage = "【失衡惩罚】你没能稳住重心，被强海风吹退回了安全处！";
        if (!ready || State == null) return false;

        if (game != null) game.SetExpression(ExprId.sad);

        if (State.carrying == "wood")
        {
            State.carrying = State.flags.Contains("matches_taken") ? "matches" : "";
            ShowCarriedModel(null);
            if (ui != null) ui.SetCarried(CarryLabel());
            AdvanceTo("B2_wood");
            penaltyMessage = "【失衡惩罚】狂风把你吹退！怀里的柴火捆被吹落打湿，必须回后院柴堆重新抱取！";
            return true;
        }
        if (State.carrying == "matches")
        {
            State.flags.Remove("matches_taken");
            State.flags.Remove("matches_packed");
            State.carrying = "";
            if (ui != null) ui.SetCarried("");
            AdvanceTo("B1_matches");
            penaltyMessage = "【失衡惩罚】狂风把你吹退！火柴盒被海雾打湿失效，必须回面包店门前摊位重拿火柴！";
            return true;
        }
        if (State.carrying == "ball")
        {
            State.flags.Remove("ball_taken");
            State.carrying = "";
            if (ball != null) ball.ReturnToSwing();
            if (ui != null) ui.SetCarried("");
            AdvanceTo("S2_ball");
            penaltyMessage = "【失衡惩罚】狂风把你吹退！手里的红球滚回了操场秋千下，必须回去重新捡球！";
            return true;
        }
        if (State.carrying == "oil")
        {
            State.flags.Remove("oil_taken");
            State.carrying = "";
            ShowCarriedModel(null);
            if (ui != null) ui.SetCarried("");
            AdvanceTo("L1_shed");
            penaltyMessage = "【失衡惩罚】海浪把你冲退！煤油壶被打翻滑回船棚，必须回船棚重新拿取油壶！";
            return true;
        }
        return false;
    }

    // 真实解谜失败惩罚：木机关连续 3 次推错主轴刻度时触发卡簧反震惩罚
    public void OnMechanismJamPenalty(string puzzleId)
    {
        if (!ready || State == null) return;
        if (game != null) game.SetExpression(ExprId.sad);

        if (player != null && island != null)
        {
            float yaw = player.yaw;
            Vector3 back = player.transform.position - new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 1.15f;
            if (island.Walkable(back.x, back.z))
            {
                back.y = island.GroundAt(back.x, back.z, back.y);
                player.transform.position = back;
            }
        }

        if (puzzleId == "bakery_lock")
        {
            State.flags.Remove("matches_taken");
            State.flags.Remove("matches_packed");
            if (State.carrying == "matches") State.carrying = "";
            if (ui != null) ui.SetCarried(CarryLabel());
            AdvanceTo("B1_matches");
            if (ui != null)
                ui.ShowToast("风门猛地一震，一股冷风灌出来——火柴灭了。", 3.2f);
        }
        else if (puzzleId == "school_lock")
        {
            State.flags.Remove("ball_taken");
            State.carrying = "";
            if (ball != null) ball.ReturnToSwing();
            if (ui != null) ui.SetCarried("");
            AdvanceTo("S2_ball");
            if (ui != null)
                ui.ShowToast("木匣的卡簧一弹，红球蹦回了秋千底下。", 3.2f);
        }
        else
        {
            if (ui != null)
                ui.ShowToast("星盘咔地一声卡死，又转回了原位。", 3.2f);
        }
    }

    // ---- WP-13 学校 ----

}
