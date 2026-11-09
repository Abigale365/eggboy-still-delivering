// WP-07 剧情推进，WP-11 起覆盖面包店：到点自动读信、火柴/柴火/烤炉链条、回忆线索 3D 检视、鲁班三环风门木匣、三段鼓风助燃和第一枚邮戳。
using System;
using System.Collections.Generic;
using UnityEngine;


public sealed partial class StoryDirector : MonoBehaviour
{
    public bool IsAuditMode => ui == null || !ui.inputClose;

    public void TakeMatches()
    {
        if (!ready || State.flags.Contains("matches_taken")) return;
        State.flags.Add("matches_taken");
        if (State.carrying != "wood") State.carrying = "matches";
        else if (!State.flags.Contains("matches_packed")) State.flags.Add("matches_packed");
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        if (State.flags.Contains("wood_loaded")) AdvanceTo("B4_light");
        else if (State.carrying == "wood") AdvanceTo("B3_load");
        else AdvanceTo("B2_wood");
    }

    public void TakeWood()
    {
        if (!ready || State.flags.Contains("wood_loaded")) return;
        if (State.flags.Contains("matches_taken") && !State.flags.Contains("matches_packed")) State.flags.Add("matches_packed");
        State.carrying = "wood";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        ShowCarriedModel("wood");
        AdvanceTo("B3_load");
    }

    public void LoadWood()
    {
        if (!ready || State.carrying != "wood") return;
        State.flags.Add("wood_loaded");
        State.carrying = State.flags.Contains("matches_taken") ? "matches" : "";
        store.Save(State);
        if (player != null) player.PlayEmote("give");
        ui.SetCarried(CarryLabel());
        ShowCarriedModel(null);
        if (State.flags.Contains("matches_taken")) AdvanceTo("B4_light");
        else AdvanceTo("B1_matches");
    }

    public void LightOven(BakeryEffects fx)
    {
        if (!ready || !State.flags.Contains("wood_loaded") || !State.flags.Contains("matches_taken") || State.flags.Contains("oven_lit")) return;
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
        if (player != null) player.PlayEmote("hop");
        ui.SetCarried(CarryLabel());
        ui.ShowToast("邮戳 1/3 · 面包店 · 炉膛里的火苗刚刚燃起");
        AdvanceTo("S0_arrive");
    }

    public void BoostOvenBellows(BakeryEffects fx)
    {
        if (!ready || fx == null) return;
        int stage = fx.BoostBellows();
        TideTownAchievementSystem.Unlock("breath_fire");
        if (player != null) player.PlayEmote(stage >= 3 ? "hop" : "give");
        if (game != null) game.SetExpression(stage >= 3 ? ExprId.excited : ExprId.happy);
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
        if (ok && !IsAuditMode)
        {
            ObjectInspectModal.Open(id, label, text, State, store, player, ui, gate);
        }
        return ok;
    }

    void ShowCarriedModel(string item)
    {
        if (carriedVisual != null) Destroy(carriedVisual);
        carriedVisual = null;
        if (player == null || (item != "wood" && item != "oil")) return;
        modelProvider.Load(item, model =>
        {
            carriedVisual = Instantiate(model, player.transform, false);
            var localOffset = model.transform.localPosition;
            carriedVisual.transform.localPosition = localOffset + new Vector3(0.20f, 0.46f, 0.28f);
        }, message => Debug.LogWarning(message));
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
                ui.ShowToast("【机关反震惩罚】连续 3 次刻度错误！倒灌冷风吹灭了火柴，请先端详菜谱、台秤、面包篮找准刻度，再去门前重拿火柴！", 4.8f);
        }
        else if (puzzleId == "school_lock")
        {
            State.flags.Remove("ball_taken");
            State.carrying = "";
            if (ball != null) ball.ReturnToSwing();
            if (ui != null) ui.SetCarried("");
            AdvanceTo("S2_ball");
            if (ui != null)
                ui.ShowToast("【机关反震惩罚】连续 3 次刻度错误！木锁卡簧将红球弹回了秋千下，请先端详拖鞋、狗窝、黑板找准刻度再去捡球！", 4.8f);
        }
        else
        {
            if (ui != null)
                ui.ShowToast("【机关反震惩罚】连续 3 次刻度错误！黄铜罗经卡死复位，请先端详塔顶白檐帽、望远镜、航海日志找准刻度！", 4.8f);
        }
    }

    // ---- WP-13 学校 ----

}
