using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed class InnovationAudit : MonoBehaviour
{
	private GameBootstrap boot;

	private readonly List<KeyValuePair<string, bool>> checks = new List<KeyValuePair<string, bool>>();

	private readonly List<string> notes = new List<string>();

	private float sumFps;

	private int frames;

	public void Begin(GameBootstrap b)
	{
		boot = b;
		if (boot.ui != null)
		{
			boot.ui.SetAuditMode(on: true);
		}
		StartCoroutine(Run());
	}

	private void Check(string name, bool ok, string note = "")
	{
		checks.Add(new KeyValuePair<string, bool>(name, ok));
		if (!string.IsNullOrEmpty(note))
		{
			notes.Add(name + ": " + note);
		}
		Debug.Log("INNOVAUDIT " + name + "=" + ok + ((note != "") ? (" " + note) : ""));
	}

	private void Update()
	{
		if (Time.deltaTime > 0f)
		{
			sumFps += 1f / Time.deltaTime;
			frames++;
		}
	}

	private IEnumerator Run()
	{
		yield return new WaitForSeconds(1f);
		Time.timeScale = 1f;
		string fatal = "";
		TidalWindChallenge.Suppress = true;
		IEnumerator[] array = new IEnumerator[10]
		{
			AuditWooden(),
			AuditWind(),
			AuditDog(),
			AuditPrint(),
			AuditShadowSound(),
			AuditHints(),
			AuditWordSlips(),
			AuditEcho(),
			AuditLux3DAndWish(),
			AuditPauseGuard()
		};
		IEnumerator[] array2 = array;
		foreach (IEnumerator s in array2)
		{
			while (true)
			{
				bool flag;
				try
				{
					flag = s.MoveNext();
				}
				catch (Exception ex)
				{
					fatal = fatal + ex.GetType().Name + ": " + ex.Message + "; ";
					Debug.LogException(ex);
					break;
				}
				if (!flag)
				{
					break;
				}
				yield return s.Current;
			}
		}
		bool flag2 = fatal == "";
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("{\n  \"checks\": {");
		for (int j = 0; j < checks.Count; j++)
		{
			flag2 &= checks[j].Value;
			stringBuilder.Append((j == 0) ? "\n" : ",\n").Append("    \"").Append(checks[j].Key)
				.Append("\": ")
				.Append(checks[j].Value ? "true" : "false");
		}
		stringBuilder.Append("\n  },\n  \"notes\": [");
		for (int k = 0; k < notes.Count; k++)
		{
			stringBuilder.Append((k == 0) ? "\n" : ",\n").Append("    \"").Append(notes[k].Replace("\\", "/").Replace("\"", "'"))
				.Append("\"");
		}
		stringBuilder.Append("\n  ],\n  \"avg_fps\": ").Append(((frames > 0) ? (sumFps / (float)frames) : 0f).ToString("F1", CultureInfo.InvariantCulture));
		stringBuilder.Append(",\n  \"errors\": \"").Append(fatal.Replace("\"", "'")).Append("\"");
		stringBuilder.Append(",\n  \"status\": \"").Append(flag2 ? "passed" : "failed").Append("\"\n}\n");
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "innov-audit.json"), stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Debug.Log("INNOVAUDIT status=" + (flag2 ? "passed" : "failed"));
		yield return new WaitForSecondsRealtime(0.5f);
		Application.Quit();
	}

	private IEnumerator AuditPauseGuard()
	{
		MenuController menu = UnityEngine.Object.FindObjectOfType<MenuController>();
		Check("pause_guard_present", menu != null);
		if (!(menu == null))
		{
			bool oldAuto = menu.AutoPauseOnLockLoss;
			menu.AutoPauseOnLockLoss = true;
			EggboyUI ui = boot.ui;
			GameState st = boot.state;
			if (AIAgentCalibrationDirector.IsOpen)
			{
				AIAgentCalibrationDirector.DebugSetConsole(open: false);
			}
			yield return new WaitForSecondsRealtime(1.3f);
			Check("pause_guard_idle_esc_pauses", menu.WouldTogglePause(esc: true));
			Check("pause_guard_idle_unexpected_unlock_pauses", menu.WouldAutoPause(wasLockedBefore: true, lockedNow: false, focused: true));
			MenuController.NoteScriptUnlock();
			Check("pause_guard_script_unlock_is_not_unexpected", !menu.WouldAutoPause(wasLockedBefore: true, lockedNow: false, focused: true));
			yield return new WaitForSecondsRealtime(1.2f);
			Check("pause_guard_unexpected_unlock_after_grace", menu.WouldAutoPause(wasLockedBefore: true, lockedNow: false, focused: true));
			yield return GuardOverlay(menu, "inspect", () => ObjectInspectModal.DebugOpen("slipper", "被咬过的拖鞋", "拖鞋上留着一道小小的牙印。", st, boot.player, ui, boot.gate) && ObjectInspectModal.DebugHasVisibleModel, () => ObjectInspectModal.IsOpen, ObjectInspectModal.DebugClose);
			yield return GuardOverlay(menu, "wooden", () => WoodenMechanismPuzzleModal.DebugOpen("school_lock", st, null, boot.player, ui, boot.gate, null), () => WoodenMechanismPuzzleModal.IsOpen, WoodenMechanismPuzzleModal.DebugClose);
			yield return GuardOverlay(menu, "wish", delegate
			{
				AIAgentCalibrationDirector.DebugSetConsole(open: true);
				return AIAgentCalibrationDirector.IsOpen;
			}, () => AIAgentCalibrationDirector.IsOpen, delegate
			{
				AIAgentCalibrationDirector.DebugSetConsole(open: false);
			});
			menu.AutoPauseOnLockLoss = oldAuto;
			if (ui.Paused)
			{
				ui.SetPaused(on: false);
			}
			Time.timeScale = 1f;
		}
	}

	private IEnumerator GuardOverlay(MenuController menu, string tag, Func<bool> open, Func<bool> isOpen, Action close)
	{
		EggboyUI ui = boot.ui;
		bool opened = open();
		yield return new WaitForSecondsRealtime(0.4f);
		Check("pause_guard_" + tag + "_opened", opened && isOpen());
		yield return new WaitForSecondsRealtime(1.1f);
		Check("pause_guard_" + tag + "_unlock_does_not_pause", !menu.WouldAutoPause(wasLockedBefore: true, lockedNow: false, focused: true) && !ui.Paused, "paused=" + ui.Paused);
		Check("pause_guard_" + tag + "_esc_does_not_pause", !menu.WouldTogglePause(esc: true));
		close();
		Check("pause_guard_" + tag + "_closed", !isOpen());
		Check("pause_guard_" + tag + "_closing_esc_does_not_pause", !menu.WouldTogglePause(esc: true) && !menu.WouldAutoPause(wasLockedBefore: true, lockedNow: false, focused: true));
		yield return new WaitForSecondsRealtime(0.8f);
		Check("pause_guard_" + tag + "_esc_works_again", menu.WouldTogglePause(esc: true), $"letterOpen={ui.LetterOpen} overlayOpen={MenuController.OverlayOpen} overlayBusy={menu.OverlayBusy} blocked={menu.Blocked}");
		Check("pause_guard_" + tag + "_never_paused", !ui.Paused);
	}

	private IEnumerator AuditHints()
	{
		HintDirector hint = HintDirector.Instance;
		Check("hint_system_present", hint != null);
		if (hint == null)
		{
			yield break;
		}
		GameState st = boot.state;
		if (!st.flags.Contains("letters_taken"))
		{
			st.flags.Add("letters_taken");
		}
		st.step = "B1_matches";
		Vector3 stove = Vector3.zero;
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			if (bakeryItem.id == "stove")
			{
				stove = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
			}
		}
		Teleport(FindWalkableNear(stove, 1.5f, 4f));
		yield return null;
		hint.DebugAdvance(0.1f);
		hint.DebugAdvance(44f);
		Check("hint_quiet_before_45s", hint.CurrentNodeId == "bakery_fire" && hint.CurrentTier == 0, $"node={hint.CurrentNodeId} tier={hint.CurrentTier}");
		hint.DebugAdvance(2f);
		Check("hint_tier1_at_45s", hint.CurrentTier == 1);
		hint.DebugAdvance(45f);
		Check("hint_tier2_at_90s", hint.CurrentTier == 2 && GameObject.Find("HintBeacon") != null);
		yield return Snap("innov_hint_tier2.png", stove, 5f, 30f);
		hint.DebugAdvance(60f);
		Check("hint_tier3_letter", hint.CurrentTier == 3 && hint.LetterHintId == "lin_oven" && hint.LetterHintPhrase == "后院的木柴", hint.LetterHintId + "/" + hint.LetterHintPhrase);
		WordMaterializerSystem wordMaterializerSystem = UnityEngine.Object.FindObjectOfType<WordMaterializerSystem>();
		if (wordMaterializerSystem != null)
		{
			wordMaterializerSystem.OpenSpecificLetter(0);
			yield return new WaitForSecondsRealtime(0.3f);
			Check("hint_tier3_phrase_marked", boot.ui.LetterBodyText != null && boot.ui.LetterBodyText.text.Contains("#2F7D6B"));
			boot.ui.CloseLetter();
			yield return new WaitForSecondsRealtime(0.2f);
		}
		hint.DebugAdvance(90f);
		Check("hint_tier4_at_240s", hint.CurrentTier == 4, "stuck=" + hint.StuckSeconds.ToString("F0"));
		yield return SnapScreen("innov_hint_tier4.png");
		st.flags.Add("matches_taken");
		hint.DebugAdvance(0.1f);
		Check("hint_reset_on_progress", hint.CurrentTier == 0 && hint.StuckSeconds < 1f && hint.LetterHintPhrase == "");
		st.flags.Remove("matches_taken");
		hint.DebugAdvance(0.1f);
		Teleport(FindWalkableNear(stove + new Vector3(40f, 0f, 0f), 0f, 12f));
		yield return null;
		hint.DebugAdvance(300f);
		Check("hint_silent_when_away", hint.CurrentTier == 0 && hint.CurrentNodeId == "", $"node={hint.CurrentNodeId} tier={hint.CurrentTier}");
	}

	private IEnumerator AuditWind()
	{
		TidalWindChallenge wind = UnityEngine.Object.FindObjectOfType<TidalWindChallenge>();
		Check("wind_present", wind != null);
		if (wind == null)
		{
			yield break;
		}
		GameState st = boot.state;
		TidalWindChallenge.Suppress = false;
		Vector3 stove = Vector3.zero;
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			if (bakeryItem.id == "stove")
			{
				stove = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
			}
		}
		Teleport(FindWalkableNear(stove, 1.5f, 4f));
		yield return new WaitForSeconds(0.3f);
		Check("wind_calm_inland", !wind.InWindZone && wind.Phase == "calm");
		Vector3 vector = Vector3.zero;
		Vector3 vector2 = Vector3.zero;
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		RouteList routeList = ((textAsset != null) ? JsonUtility.FromJson<RouteList>(textAsset.text) : null);
		if (routeList != null)
		{
			RouteStop[] route = routeList.route;
			foreach (RouteStop routeStop in route)
			{
				if (routeStop.stop && routeStop.id == "LOW")
				{
					vector = new Vector3(routeStop.x, 0f, routeStop.z);
				}
				if (routeStop.stop && routeStop.id == "24")
				{
					vector2 = new Vector3(routeStop.x, 0f, routeStop.z);
				}
			}
		}
		Vector3 mid = FindWalkableNear((vector + vector2) * 0.5f, 0f, 2f);
		st.step = "B3_load";
		st.carrying = "wood";
		Teleport(mid);
		yield return new WaitForSeconds(0.3f);
		Check("wind_zone_on_coast", wind.InWindZone, "mid=" + mid.ToString("F1"));
		Vector3 vector3 = mid + wind.WindDir * 5f;
		Check("wind_blows_from_sea", !boot.world.InWalkArea(mid.x - wind.WindDir.x * 5f, mid.z - wind.WindDir.z * 5f) || boot.world.GroundAt(mid.x - wind.WindDir.x * 5f, mid.z - wind.WindDir.z * 5f) <= boot.world.GroundAt(vector3.x, vector3.z) + 0.05f, "wind=" + wind.WindDir.ToString("F2"));
		wind.DebugBrace = true;
		wind.DebugStartGust();
		yield return new WaitForSeconds(2.2f);
		Check("wind_gust_brace_crouches", wind.Phase == "gust" && boot.player.IsStoryCrouched, "phase=" + wind.Phase);
		yield return Snap("innov_wind_gust.png", boot.player.transform.position, 4f, 200f);
		float t = Time.realtimeSinceStartup;
		while (wind.Phase == "gust" && Time.realtimeSinceStartup - t < 5f)
		{
			yield return null;
		}
		Check("wind_brace_survives", wind.Survived == 1 && wind.Failures == 0 && st.carrying == "wood" && wind.Phase == "boost", "phase=" + wind.Phase);
		wind.DebugBrace = false;
		yield return new WaitForSeconds(3.3f);
		Teleport(mid);
		Vector3 before = boot.player.transform.position;
		wind.DebugStartGust();
		t = Time.realtimeSinceStartup;
		while (wind.Failures == 0 && Time.realtimeSinceStartup - t < 6f)
		{
			yield return null;
		}
		float num = Vector3.Distance(boot.player.transform.position, before);
		Check("wind_fail_happens", wind.Failures == 1);
		Check("wind_fail_short_stumble", num < 7f, "moved=" + num.ToString("F1"));
		Check("wind_fail_drops_item_nearby", st.carrying != "wood" && boot.director.LastDroppedItem == "wood" && Vector3.Distance(boot.director.LastDropPosition, boot.player.transform.position) < 5f && boot.world.Walkable(boot.director.LastDropPosition.x, boot.director.LastDropPosition.z), $"carry={st.carrying} step={st.step} drop={boot.director.LastDropPosition:F1}");
		Check("wind_fail_no_text_banner", GameObject.Find("TidalWindBanner") == null);
		yield return new WaitForSeconds(1.6f);
		yield return Snap("innov_wind_dropped.png", boot.director.LastDropPosition, 3.5f, 290f);
		TidalWindChallenge.Suppress = true;
		Teleport(boot.director.LastDropPosition);
		yield return null;
		bool flag = boot.service.TryInteract("dropped_wood");
		Check("wind_pickup_restores", flag && st.carrying == "wood" && st.step == "B3_load", "step=" + st.step);
		st.carrying = "";
		st.step = "P1_mailbox";
		boot.player.SetStoryCrouch(crouch: false);
		float pickupDeadline = Time.realtimeSinceStartup + 4f;
		while (boot.player.Emoting && Time.realtimeSinceStartup < pickupDeadline)
		{
			yield return null;
		}
		PrototypeGame prototypeGame = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		if (prototypeGame != null)
		{
			TidalWindChallenge.Suppress = false;
			boot.player.idle = 0f;
			prototypeGame.SetExpression(ExprId.curious);
			Teleport(mid);
			float enterDeadline = Time.realtimeSinceStartup + 2f;
			while (!wind.InWindZone && Time.realtimeSinceStartup < enterDeadline)
			{
				yield return null;
			}
			wind.DebugBrace = true;
			wind.DebugStartGust();
			float braceDeadline = Time.realtimeSinceStartup + 5f;
			while ((wind.Phase != "gust" || !boot.player.IsStoryCrouched) && Time.realtimeSinceStartup < braceDeadline)
			{
				yield return null;
			}
			Check("wind_exit_setup", wind.Phase == "gust" && boot.player.IsStoryCrouched && boot.player.pose.expr == ExprId.nervous, $"zone={wind.InWindZone} phase={wind.Phase} crouched={boot.player.IsStoryCrouched} expr={boot.player.pose.expr}");
			Teleport(FindWalkableNear(stove, 1.5f, 4f));
			yield return new WaitForSeconds(0.45f);
			Check("wind_exit_restores_pose", wind.Phase == "calm" && !boot.player.IsStoryCrouched && boot.player.pose.expr == ExprId.curious, "expr=" + boot.player.pose.expr);
			wind.DebugBrace = false;
			TidalWindChallenge.Suppress = true;
		}
		yield return new WaitForSeconds(0.3f);
	}

	private IEnumerator AuditShadowSound()
	{
		ShadowLanternPuzzle sp = ShadowLanternPuzzle.Instance;
		SoundBottleSystem snd = SoundBottleSystem.Instance;
		Check("shadow_present", sp != null && sp.enabled && snd != null);
		if (sp == null || !sp.enabled || snd == null)
		{
			yield break;
		}
		GameState st = boot.state;
		Vector3 turntablePos = sp.TurntablePos;
		bool ok = Vector2.Distance(new Vector2(turntablePos.x, turntablePos.z), new Vector2(20.5f, -11.7f)) <= 1f && turntablePos.y >= WaterLevel.LevelYs[2] + 0.1f && boot.world.InWalkArea(turntablePos.x, turntablePos.z);
		Check("shadow_off_route", ok, "turntable=" + turntablePos.ToString("F2"));
		Teleport(FindWalkableNear(sp.TurntablePos, 1.2f, 1.8f));
		yield return new WaitForSeconds(0.3f);
		Check("shadow_start_unsolved", !sp.Solved && Mathf.Abs(Mathf.DeltaAngle(0f, sp.Angle)) > 60f);
		yield return Snap("innov_shadow_start.png", sp.ScreenCenter, 3.2f, Mathf.Atan2(0f - sp.ScreenCenter.x + sp.TurntablePos.x, 0f - sp.ScreenCenter.z + sp.TurntablePos.z) * 57.29578f + 40f);
		sp.DebugHold = true;
		float t = Time.realtimeSinceStartup;
		while (!sp.Solved && Time.realtimeSinceStartup - t < 8f)
		{
			yield return null;
		}
		sp.DebugHold = false;
		Check("shadow_turn_solves", sp.Solved && Mathf.Abs(sp.Angle) < 0.01f, "angle=" + sp.Angle.ToString("F1"));
		yield return new WaitForSeconds(0.8f);
		Transform transform = sp.DebugJunk("ring");
		Transform transform2 = sp.DebugJunk("shaft");
		bool ok2 = false;
		string note = "missing parts";
		if (transform != null && transform2 != null)
		{
			Bounds bounds = Lux3DModelFit.WorldBounds(transform.gameObject);
			Bounds bounds2 = Lux3DModelFit.WorldBounds(transform2.gameObject);
			Vector2 vector = sp.DebugScreenUV(bounds.center);
			Vector3.Cross(Vector3.up, (sp.ScreenCenter - sp.TurntablePos).normalized);
			float num = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.8f;
			Vector2 vector2 = sp.DebugScreenUV(bounds.center + Vector3.up * num);
			Vector2 vector3 = sp.DebugScreenUV(bounds2.center);
			Vector2 vector4 = sp.DebugScreenUV(bounds2.center + Vector3.down * 0.18f + Vector3.Cross(Vector3.up, (sp.ScreenCenter - sp.TurntablePos).normalized) * -0.05f);
			bool flag = !sp.DebugIsShadow(vector.x, vector.y);
			bool flag2 = sp.DebugIsShadow(vector2.x, vector2.y);
			bool flag3 = sp.DebugIsShadow(vector3.x, vector3.y);
			bool flag4 = !sp.DebugIsShadow(vector4.x, vector4.y);
			ok2 = flag && flag2 && flag3 && flag4;
			note = $"holeLit={flag} rimDark={flag2} shaftDark={flag3} belowLit={flag4} hole={vector} shaft={vector3}";
		}
		Check("shadow_screen_shows_key", ok2, note);
		yield return Snap("innov_shadow_key.png", sp.ScreenCenter, 3.2f, Mathf.Atan2(0f - sp.ScreenCenter.x + sp.TurntablePos.x, 0f - sp.ScreenCenter.z + sp.TurntablePos.z) * 57.29578f + 40f);
		Teleport(sp.BottlePos + (boot.player.transform.position - sp.BottlePos).normalized * 0.6f);
		yield return null;
		bool took = boot.service.TryInteract("sound_bottle_pickup");
		yield return new WaitForSecondsRealtime(0.2f);
		if (boot.ui.LetterOpen)
		{
			boot.ui.CloseLetter();
		}
		Check("shadow_gives_bottle", took && snd.HasBottle);
		DogFollower dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		string[] order = SoundBottleSystem.Order;
		foreach (string id in order)
		{
			Vector3 c;
			object obj;
			GameObject gameObject;
			switch (id)
			{
			case "bark":
				dog.Follow();
				yield return new WaitForSeconds(0.3f);
				c = dog.transform.position;
				break;
			default:
				obj = "Bakery_wood";
				goto IL_07c6;
			case "swing":
				obj = "S_swing";
				goto IL_07c6;
			case "bell":
				{
					obj = "Bakery_bell";
					goto IL_07c6;
				}
				IL_07c6:
				gameObject = GameObject.Find((string)obj);
				c = ((gameObject != null) ? gameObject.transform.position : Vector3.zero);
				break;
			}
			Teleport(FindWalkableNear(c, 0.6f, 1.6f));
			yield return null;
			if (id == "bark")
			{
				yield return new WaitForSeconds(0.4f);
			}
			boot.service.TryInteract("sound_" + id);
			yield return new WaitForSeconds(0.2f);
		}
		Check("sound_all_four", snd.Count == 4, "count=" + snd.Count);
		Teleport(FindWalkableNear(snd.MusicBoxPos, 0.8f, 1.6f));
		yield return null;
		bool flag5 = boot.service.TryInteract("music_box");
		Check("music_box_starts", flag5 && snd.Playing && snd.HouseCount >= 6, "houses=" + snd.HouseCount);
		yield return new WaitForSeconds(3.5f);
		Check("music_box_houses_move", snd.MaxHouseLift > 0.1f, "lift=" + snd.MaxHouseLift.ToString("F2"));
		yield return Snap("innov_music_box.png", snd.MusicBoxPos + Vector3.up * 1.5f, 9f, 200f);
		t = Time.realtimeSinceStartup;
		while (snd.Playing && Time.realtimeSinceStartup - t < 15f)
		{
			yield return null;
		}
		float num2 = snd.DebugHouseError();
		Check("music_box_restores_exactly", !snd.Playing && snd.Done && num2 < 0.001f && !boot.player.locked, "err=" + num2.ToString("F5"));
		st.step = "P1_mailbox";
		yield return new WaitForSeconds(0.3f);
	}

	private IEnumerator AuditPrint()
	{
		PerspectivePrintSystem ps = UnityEngine.Object.FindObjectOfType<PerspectivePrintSystem>();
		WorldMechanismDirector mech = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
		Check("print_present", ps != null && mech != null);
		if (ps == null || mech == null)
		{
			yield break;
		}
		GameState st = boot.state;
		EggboyPlayer p = boot.player;
		Vector3 c = Vector3.zero;
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			if (bakeryItem.id == "stove")
			{
				c = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
			}
		}
		Teleport(FindWalkableNear(c, 3f, 6f));
		yield return null;
		ps.OpenViewfinder();
		yield return null;
		Check("print_open", ps.IsViewfinderActive && p.locked);
		bool ok = true;
		Renderer[] componentsInChildren = p.GetComponentsInChildren<Renderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i].enabled)
			{
				ok = false;
			}
		}
		Check("print_hides_player_in_fp", ok);
		ps.DebugSelect("ruler");
		ps.DebugSetView(p.yaw * 57.29578f, 25f, 0.6f, 0f);
		yield return null;
		ps.DebugSetView(p.yaw * 57.29578f, 25f, 0.6f, 0f);
		yield return SnapScreen("innov_print_viewfinder.png");
		Check("print_aim_valid", ps.AimValid, "k=" + ps.AimScale.ToString("F2"));
		Rect rect = ps.DebugScreenRect(ps.DebugHeldModel);
		GameObject go = ps.Shutter();
		Rect rect2 = ((go != null) ? ps.DebugScreenRect(go) : default(Rect));
		float num = Mathf.Max(rect.width, rect.height) * 0.03f;
		bool ok2 = go != null && Mathf.Abs(rect.xMin - rect2.xMin) <= num && Mathf.Abs(rect.xMax - rect2.xMax) <= num && Mathf.Abs(rect.yMin - rect2.yMin) <= num && Mathf.Abs(rect.yMax - rect2.yMax) <= num;
		Check("print_screen_rect_unchanged", ok2, $"before={rect} after={rect2}");
		Check("print_scaled_up", go != null && go.transform.lossyScale.x > 1.25f, (go != null) ? ("scale=" + go.transform.lossyScale.x.ToString("F2")) : "null");
		Check("print_not_bridge_inland", !ps.LastPrintSnapped);
		ps.DebugSetView(p.yaw * 57.29578f, -30f, 0.6f, 0f);
		Check("print_sky_invalid", !ps.AimValid && ps.Shutter() == null);
		ps.CloseViewfinder();
		yield return null;
		bool flag = false;
		componentsInChildren = p.GetComponentsInChildren<Renderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i].enabled)
			{
				flag = true;
			}
		}
		Check("print_close_restores", !ps.IsViewfinderActive && !p.locked && flag);
		if (go != null)
		{
			yield return Snap("innov_print_inland.png", go.transform.position, 3.5f, 200f);
		}
		ps.DebugClearPrints();
		st.step = "L0_cross";
		if (WaterLevel.Main != null)
		{
			WaterLevel.Main.SetInstant(WaterLevel.LevelYs[2]);
		}
		mech.DebugResetCrossing();
		yield return new WaitForSeconds(0.4f);
		mech.GapEndpoints(out var a, out var b);
		Vector3 ab = b - a;
		ab.y = 0f;
		Vector3 pos = FindWalkableNear(a - ab.normalized * 1f, 0f, 1.2f);
		Teleport(pos);
		yield return null;
		Check("print_gap_blocks", !boot.world.Walkable(((a + b) * 0.5f).x, ((a + b) * 0.5f).z));
		ps.OpenViewfinder();
		yield return null;
		ps.DebugSelect("ruler");
		float yaw = Mathf.Atan2(ab.x, ab.z) * 57.29578f;
		bool found = false;
		string note = "";
		float heldYaw = 0f;
		float holdDistance = 0f;
		float aimPitch = 0f;
		for (float num2 = 0f; num2 <= 90f; num2 += 90f)
		{
			if (found)
			{
				break;
			}
			for (float num3 = 0.35f; num3 <= 1.1f; num3 += 0.05f)
			{
				if (found)
				{
					break;
				}
				for (float num4 = 5f; num4 <= 50f; num4 += 1.5f)
				{
					if (found)
					{
						break;
					}
					ps.DebugSetView(yaw, num4, num3, num2);
					if (ps.AimValid)
					{
						Bounds bounds = Lux3DModelFit.WorldBounds(ps.DebugHeldModel);
						Vector3 position = Camera.main.transform.position;
						Bounds bounds2 = new Bounds(position + (bounds.center - position) * ps.AimScale, bounds.size * ps.AimScale);
						bounds2.Expand(new Vector3(1.1f, 8f, 1.1f));
						if (bounds2.Contains(new Vector3(a.x, bounds2.center.y, a.z)) && bounds2.Contains(new Vector3(b.x, bounds2.center.y, b.z)))
						{
							found = true;
							heldYaw = num2;
							holdDistance = num3;
							aimPitch = num4;
							note = $"heldYaw={num2} hold={num3:F2} pitch={num4:F1} k={ps.AimScale:F1}";
						}
					}
				}
			}
		}
		Check("print_gap_aim_found", found, note);
		yield return SnapScreen("innov_print_gap_aim.png");
		if (found)
		{
			ps.DebugSetView(yaw, aimPitch, holdDistance, heldYaw);
		}
		GameObject bridge = ps.Shutter();
		ps.CloseViewfinder();
		yield return new WaitForSeconds(0.3f);
		Vector3 target = (a + b) * 0.5f;
		Check("print_snaps_to_gap", bridge != null && ps.LastPrintSnapped && mech.CrossingSolved, "snapped=" + ps.LastPrintSnapped);
		Check("print_bridge_walkable", boot.world.Walkable(target.x, target.z) && boot.world.Walkable(Vector3.Lerp(a, b, 0.25f).x, Vector3.Lerp(a, b, 0.25f).z) && boot.world.Walkable(Vector3.Lerp(a, b, 0.75f).x, Vector3.Lerp(a, b, 0.75f).z), $"ground={boot.world.GroundAt(target.x, target.z):F2} water={WaterLevel.StaticY:F2}");
		yield return Snap("innov_print_bridge.png", target, 4f, yaw + 100f);
		ps.DebugClearPrints();
		mech.DebugResetCrossing();
		if (WaterLevel.Main != null)
		{
			WaterLevel.Main.SetInstant(0f);
		}
		st.step = "P1_mailbox";
		yield return new WaitForSeconds(0.5f);
	}

	private IEnumerator AuditDog()
	{
		DogFollower dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		DogPawPlate plate = DogPawPlate.Instance;
		Check("dog_present", dog != null && plate != null);
		if (dog == null || plate == null)
		{
			yield break;
		}
		GameState st = boot.state;
		st.step = "S2_ball";
		Vector3 recipe = Vector3.zero;
		SchoolItem[] items = SchoolData.Load().items;
		foreach (SchoolItem schoolItem in items)
		{
			if (schoolItem.id == "blackboard")
			{
				recipe = new Vector3(schoolItem.x, schoolItem.y, schoolItem.z);
			}
		}
		Teleport(FindWalkableNear(recipe, 1.5f, 3.5f));
		dog.Follow();
		yield return new WaitForSeconds(0.6f);
		dog.CommandOrPet();
		string lastCommand = dog.LastCommand;
		Check("dog_sniff_command", lastCommand.StartsWith("sniff:"), lastCommand);
		string id = (lastCommand.StartsWith("sniff:") ? lastCommand.Substring(6) : "");
		Vector3 dogFrom = dog.transform.position;
		float t0 = Time.realtimeSinceStartup;
		while (!st.flags.Contains("dog_sniff_" + id) && Time.realtimeSinceStartup - t0 < 8f)
		{
			yield return null;
		}
		Check("dog_sniff_runs_and_marks", st.flags.Contains("dog_sniff_" + id) && GameObject.Find("DogClueMark_" + id) != null, $"id={id} ran={Vector3.Distance(dogFrom, dog.transform.position):F1}");
		GameObject gameObject = GameObject.Find("DogClueMark_" + id);
		TextMesh textMesh = ((gameObject != null) ? gameObject.GetComponentInChildren<TextMesh>() : null);
		Check("dog_mark_shows_ring_number", textMesh != null && textMesh.text.Contains("环"), (textMesh != null) ? textMesh.text : "null");
		if (gameObject != null)
		{
			yield return Snap("innov_dog_sniff.png", gameObject.transform.position, 2.4f, 200f);
		}
		yield return new WaitForSeconds(2.5f);
		Vector3 c = Vector3.zero;
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		RouteList routeList = ((textAsset != null) ? JsonUtility.FromJson<RouteList>(textAsset.text) : null);
		if (routeList != null)
		{
			RouteStop[] route = routeList.route;
			foreach (RouteStop routeStop in route)
			{
				if (routeStop.stop && routeStop.id == "03")
				{
					c = new Vector3(routeStop.x, 0f, routeStop.z);
				}
			}
		}
		Teleport(FindWalkableNear(c, 0f, 3f));
		yield return new WaitForSeconds(4f);
		Check("dog_stays_at_school", dog.HomeMode && dog.InSchoolArea && dog.Visible, string.Format("home={0} inSchool={1} at={2}", dog.HomeMode, dog.InSchoolArea, dog.transform.position.ToString("F1")));
		string[] array = new string[3] { "slipper", "doghouse", "blackboard" };
		foreach (string text in array)
		{
			if (!st.flags.Contains("dog_sniff_" + text))
			{
				st.flags.Add("dog_sniff_" + text);
			}
		}
		Teleport(FindWalkableNear(recipe, 1.5f, 3.5f));
		float tBack = Time.realtimeSinceStartup;
		while ((dog.HomeMode || Vector3.Distance(dog.transform.position, boot.player.transform.position) > 2.5f) && Time.realtimeSinceStartup - tBack < 8f)
		{
			yield return null;
		}
		dog.CommandOrPet();
		Check("dog_pet_when_nothing_near", dog.LastCommand == "pet", dog.LastCommand);
		yield return new WaitForSeconds(1f);
		st.step = "L1_shed";
		array = new string[3] { "hat", "scope", "log" };
		foreach (string text2 in array)
		{
			st.flags.Remove("dog_sniff_" + text2);
		}
		if (WaterLevel.Main != null)
		{
			WaterLevel.Main.SetInstant(WaterLevel.LevelYs[2]);
		}
		Check("dog_plate_dry_at_L2", boot.world.Walkable(plate.Center.x, plate.Center.z), "plate=" + plate.Center.ToString("F1"));
		Teleport(FindWalkableNear(plate.Center, 2.5f, 4f));
		dog.Follow();
		yield return new WaitForSeconds(0.8f);
		Check("dog_plate_not_yet", !plate.Done);
		dog.CommandOrPet();
		Check("dog_plate_command", dog.LastCommand == "plate", dog.LastCommand);
		t0 = Time.realtimeSinceStartup;
		while (!plate.Done && Time.realtimeSinceStartup - t0 < 8f)
		{
			yield return null;
		}
		Check("dog_steps_on_plate", plate.Done);
		t0 = Time.realtimeSinceStartup;
		while (!plate.ChestReady && Time.realtimeSinceStartup - t0 < 5f)
		{
			yield return null;
		}
		Check("dog_chest_rises", plate.ChestReady && boot.world.Walkable(plate.ChestRest.x, plate.ChestRest.z));
		yield return Snap("innov_dog_chest.png", (plate.Center + plate.ChestRest) * 0.5f, 3.2f, 200f);
		Vector3 vector = FindWalkableNear(plate.ChestRest, 0.6f, 1.5f);
		bool flag = vector != plate.ChestRest;
		Check("dog_chest_reachable_dry", flag, string.Format("chest={0} stand={1} water={2:F2}", plate.ChestRest.ToString("F2"), vector.ToString("F2"), WaterLevel.StaticY));
		Teleport(flag ? vector : (plate.ChestRest + (boot.player.transform.position - plate.ChestRest).normalized * 0.8f));
		yield return null;
		bool opened = boot.service.TryInteract("paw_chest");
		yield return new WaitForSecondsRealtime(0.3f);
		Check("dog_chest_gives_lighthouse_code", opened && st.flags.Contains("dog_sniff_hat") && st.flags.Contains("dog_sniff_scope") && st.flags.Contains("dog_sniff_log"));
		yield return SnapScreen("innov_dog_chest_note.png");
		if (boot.ui.LetterOpen)
		{
			boot.ui.CloseLetter();
		}
		WoodenMechanismPuzzleModal.DebugOpen("lighthouse_lock", st, null, boot.player, boot.ui, boot.gate, null);
		yield return new WaitForSecondsRealtime(0.3f);
		GameObject gameObject2 = GameObject.Find("WoodenMechanismNote");
		string all = "";
		if (gameObject2 != null)
		{
			Text[] componentsInChildren = gameObject2.GetComponentsInChildren<Text>();
			foreach (Text text3 in componentsInChildren)
			{
				all += text3.text;
			}
		}
		Check("dog_code_in_lighthouse_note", Row("外环", 6) && Row("中环", 2) && Row("内环", 3), all.Replace("\n", " / "));
		WoodenMechanismPuzzleModal.DebugClose();
		st.step = "P1_mailbox";
		if (WaterLevel.Main != null)
		{
			WaterLevel.Main.SetInstant(0f);
		}
		yield return new WaitForSeconds(0.5f);
		bool Row(string ring, int n)
		{
			return Regex.IsMatch(all, ring + "[^环]*?刻度 " + n);
		}
	}

	private IEnumerator AuditWooden()
	{
		GameState st = boot.state;
		bool solvedCb = false;
		bool opened = WoodenMechanismPuzzleModal.DebugOpen("bakery_lock", st, null, boot.player, boot.ui, boot.gate, delegate
		{
			solvedCb = true;
		});
		yield return new WaitForSecondsRealtime(0.8f);
		Check("wood_open", opened && WoodenMechanismPuzzleModal.IsOpen);
		GameObject note = GameObject.Find("WoodenMechanismNote");
		Check("wood_only_small_note", note != null && note.activeInHierarchy && GameObject.Find("WoodenMechanismParchmentCard") == null);
		bool ok = true;
		string text = "";
		for (int i = 0; i < 3; i++)
		{
			int num = WoodenMechanismPuzzleModal.DebugRing(i);
			int num2 = WoodenMechanismPuzzleModal.DebugNumberUnderPointer(i);
			text += $"r{i}={num}/{num2} ";
			if (num != num2)
			{
				ok = false;
			}
		}
		Check("wood_numbers_match_rings_at_open", ok, text);
		yield return SnapScreen("innov_wood_open.png");
		int num3 = WoodenMechanismPuzzleModal.DebugRing(0);
		WoodenMechanismPuzzleModal.ExternalRotateRing(0, 1);
		Check("wood_locked_by_latches", WoodenMechanismPuzzleModal.DebugRing(0) == num3 && !WoodenMechanismPuzzleModal.DebugStage1);
		WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
		Check("wood_push_while_locked_free", WoodenMechanismPuzzleModal.DebugAttemptsLeft == 3);
		WoodenMechanismPuzzleModal.ExternalToggleLatch(1);
		Check("wood_latch_b_opens_all", WoodenMechanismPuzzleModal.DebugStage1);
		int num4 = WoodenMechanismPuzzleModal.DebugRing(0);
		int num5 = WoodenMechanismPuzzleModal.DebugRing(1);
		int num6 = WoodenMechanismPuzzleModal.DebugRing(2);
		WoodenMechanismPuzzleModal.DebugDragRing(0, 62f);
		int num7 = WoodenMechanismPuzzleModal.DebugRing(0);
		int num8 = WoodenMechanismPuzzleModal.DebugRing(1);
		int num9 = WoodenMechanismPuzzleModal.DebugRing(2);
		Check("wood_drag_outer_couples_middle", num7 == num4 % 6 + 1 && num8 == num5 % 6 + 1 && num9 == num6, $"{num4}{num5}{num6}->{num7}{num8}{num9}");
		WoodenMechanismPuzzleModal.DebugDragRing(0, -20f);
		Check("wood_small_drag_ignored", WoodenMechanismPuzzleModal.DebugRing(0) == num7);
		WoodenMechanismPuzzleModal.DebugDragRing(0, 20f);
		yield return new WaitForSecondsRealtime(0.8f);
		ok = true;
		text = "";
		for (int j = 0; j < 3; j++)
		{
			int num10 = WoodenMechanismPuzzleModal.DebugRing(j);
			int num11 = WoodenMechanismPuzzleModal.DebugNumberUnderPointer(j);
			text += $"r{j}={num10}/{num11} ";
			if (num10 != num11)
			{
				ok = false;
			}
		}
		Check("wood_numbers_follow_rotation", ok, text);
		WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
		Check("wood_wrong_push_costs_one", WoodenMechanismPuzzleModal.DebugAttemptsLeft == 2 && !WoodenMechanismPuzzleModal.DebugStage1);
		Text[] obj = ((note != null) ? note.GetComponentsInChildren<Text>() : new Text[0]);
		string text2 = "";
		Text[] array = obj;
		foreach (Text text3 in array)
		{
			text2 += text3.text;
		}
		Check("wood_note_shows_attempts", text2.Contains("●●○") && text2.Contains("？"), text2.Replace("\n", " / "));
		WoodenMechanismPuzzleModal.ExternalToggleLatch(1);
		Check("wood_rebound_needs_new_order", !WoodenMechanismPuzzleModal.DebugStage1);
		WoodenMechanismPuzzleModal.DebugOpenAllLatches();
		Check("wood_rebound_solvable", WoodenMechanismPuzzleModal.DebugStage1);
		int[] array2 = new int[3] { 3, 5, 2 };
		for (int l = 0; l < 3; l++)
		{
			int num12 = 0;
			while (WoodenMechanismPuzzleModal.DebugRing(l) != array2[l] && num12++ < 8)
			{
				WoodenMechanismPuzzleModal.ExternalRotateRing(l, 1);
			}
		}
		yield return new WaitForSecondsRealtime(0.7f);
		yield return SnapScreen("innov_wood_aligned.png");
		Check("wood_aligned", WoodenMechanismPuzzleModal.DebugRing(0) == 3 && WoodenMechanismPuzzleModal.DebugRing(1) == 5 && WoodenMechanismPuzzleModal.DebugRing(2) == 2);
		WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
		Check("wood_solves", WoodenMechanismPuzzleModal.DebugSolved);
		yield return SnapScreen("innov_wood_unlocked.png");
		float t0 = Time.realtimeSinceStartup;
		while (!solvedCb && Time.realtimeSinceStartup - t0 < 3f)
		{
			yield return null;
		}
		Check("wood_callback_and_closes", solvedCb && !WoodenMechanismPuzzleModal.IsOpen && st.flags.Contains("puzzle_bakery_lock") && !boot.player.locked);
		st.flags.Remove("puzzle_bakery_lock");
		st.step = "B4_light";
		if (!st.flags.Contains("matches_taken"))
		{
			st.flags.Add("matches_taken");
		}
		WoodenMechanismPuzzleModal.DebugOpen("bakery_lock", st, null, boot.player, boot.ui, boot.gate, null);
		yield return null;
		for (int m = 0; m < 3; m++)
		{
			WoodenMechanismPuzzleModal.DebugOpenAllLatches();
			WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
		}
		yield return null;
		Check("wood_third_failure_jams", !WoodenMechanismPuzzleModal.IsOpen && !st.flags.Contains("matches_taken") && st.step == "B1_matches", "step=" + st.step);
		WoodenMechanismPuzzleModal.DebugClose();
		st.step = "P1_mailbox";
		yield return new WaitForSecondsRealtime(0.3f);
	}

	private IEnumerator AuditEcho()
	{
		TidalEchoCloneSystem echo = UnityEngine.Object.FindObjectOfType<TidalEchoCloneSystem>();
		WorldMechanismDirector mech = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
		Check("echo_system_present", echo != null && mech != null);
		if (echo == null || mech == null)
		{
			yield break;
		}
		boot.state.step = "L0_cross";
		if (WaterLevel.Main != null)
		{
			WaterLevel.Main.SetInstant(WaterLevel.LevelYs[2]);
		}
		mech.DebugResetCrossing();
		yield return new WaitForSeconds(0.3f);
		Vector3 ch = mech.GetCrossingChannelCenter();
		Vector3 winch = mech.WinchPosition;
		Check("echo_gap_blocks", mech.GapActive && !boot.world.Walkable(ch.x, ch.z), "gap=" + mech.GapActive);
		Check("echo_winch_on_dry_land", boot.world.Walkable(winch.x, winch.z), "winch=" + winch.ToString("F2"));
		Teleport(winch + new Vector3(0.3f, 0f, 0.3f));
		yield return null;
		mech.DebugPlayerHoldsWinch = true;
		yield return new WaitForSeconds(3.5f);
		Check("echo_alone_cannot_crank", !mech.CrossingSolved && mech.WinchHolders == 1 && mech.WinchStrained, $"holders={mech.WinchHolders} solved={mech.CrossingSolved}");
		mech.DebugPlayerHoldsWinch = false;
		yield return new WaitForSeconds(0.3f);
		echo.DebugClear();
		for (int i = 0; i < 20; i++)
		{
			echo.DebugPushFrame(winch, 0f, Vector3.zero, grounded: true, "winch");
		}
		Check("echo_needs_buffer", !echo.Summon() && !echo.EchoActive, "buffered=" + echo.BufferedSeconds.ToString("F1"));
		echo.DebugClear();
		Vector3 vector = FindWalkableNear(winch, 2.5f, 3.5f);
		vector.y = boot.world.GroundAt(vector.x, vector.z, winch.y + 1f);
		float yaw = Mathf.Atan2(winch.x - vector.x, winch.z - vector.z);
		for (int j = 0; j <= 40; j++)
		{
			float t4 = (float)j / 40f;
			Vector3 pos = Vector3.Lerp(vector, winch, t4);
			pos.y = boot.world.GroundAt(pos.x, pos.z, winch.y + 1f);
			Vector3 vector2 = (winch - vector) / 2f;
			vector2.y = 0f;
			echo.DebugPushFrame(pos, yaw, (j < 40) ? vector2 : Vector3.zero, grounded: true, "");
		}
		for (int k = 0; k < 80; k++)
		{
			echo.DebugPushFrame(winch, yaw, Vector3.zero, grounded: true, "winch");
		}
		Check("echo_summon", echo.Summon() && echo.EchoActive);
		Vector3 e0 = echo.EchoPosition;
		float maxSpeed = 0f;
		float maxLegSwing = 0f;
		GameObject puppet = GameObject.Find("EGGBOY_TidalEcho");
		EggboyPlayer eggboyPlayer = ((puppet != null) ? puppet.GetComponent<EggboyPlayer>() : null);
		Transform footL = ((eggboyPlayer != null && eggboyPlayer.Bones != null) ? eggboyPlayer.Bones.Bone("footL") : null);
		Vector3 footStart = Vector3.zero;
		float t3 = Time.realtimeSinceStartup;
		while (Time.realtimeSinceStartup - t3 < 1.6f)
		{
			yield return new WaitForEndOfFrame();
			maxSpeed = Mathf.Max(maxSpeed, echo.EchoSpeed);
			if (footL != null)
			{
				Vector3 vector3 = Quaternion.Inverse(puppet.transform.rotation) * (footL.position - puppet.transform.position);
				if (footStart == Vector3.zero)
				{
					footStart = vector3;
				}
				maxLegSwing = Mathf.Max(maxLegSwing, Vector3.Distance(vector3, footStart));
			}
		}
		Check("echo_walks", Vector3.Distance(echo.EchoPosition, e0) > 1f && maxSpeed > 1f, $"moved={Vector3.Distance(echo.EchoPosition, e0):F2} speed={maxSpeed:F2}");
		Check("echo_legs_move", maxLegSwing > 0.03f, $"footSwing={maxLegSwing:F3}");
		bool ok = false;
		if (puppet != null)
		{
			SkinnedMeshRenderer componentInChildren = puppet.GetComponentInChildren<SkinnedMeshRenderer>();
			ok = componentInChildren != null && componentInChildren.sharedMaterial != null && componentInChildren.sharedMaterial.name.StartsWith("TidalEchoGhost");
		}
		Check("echo_ghost_material", ok);
		yield return Snap("innov_echo_walk.png", echo.EchoPosition, 3f, 200f);
		t3 = Time.realtimeSinceStartup;
		while (echo.EchoHoldZone != "winch" && Time.realtimeSinceStartup - t3 < 3f)
		{
			yield return null;
		}
		Check("echo_holds_winch", echo.EchoHoldZone == "winch");
		Teleport(winch + new Vector3(-0.35f, 0f, 0.25f));
		mech.DebugPlayerHoldsWinch = true;
		yield return new WaitForSeconds(0.4f);
		Check("echo_two_holders", mech.WinchHolders == 2, "holders=" + mech.WinchHolders);
		yield return Snap("innov_echo_crank.png", winch, 3.2f, 200f);
		t3 = Time.realtimeSinceStartup;
		while (!mech.CrossingSolved && Time.realtimeSinceStartup - t3 < 5f)
		{
			yield return null;
		}
		mech.DebugPlayerHoldsWinch = false;
		Check("echo_crank_raises_bridge", mech.CrossingSolved, "crank=" + mech.WinchCrank01.ToString("F2"));
		yield return new WaitForSeconds(2f);
		Check("echo_bridge_walkable", boot.world.Walkable(ch.x, ch.z), $"ground={boot.world.GroundAt(ch.x, ch.z):F2} water={WaterLevel.StaticY:F2}");
		yield return Snap("innov_echo_bridge.png", ch, 5f, 200f);
		t3 = Time.realtimeSinceStartup;
		while (echo.EchoActive && Time.realtimeSinceStartup - t3 < 6f)
		{
			yield return null;
		}
		Check("echo_fades_out", !echo.EchoActive && (puppet == null || !puppet.activeSelf));
		mech.DebugResetCrossing();
		echo.DebugClear();
		yield return new WaitForSeconds(2f);
	}

	private IEnumerator AuditWordSlips()
	{
		Lux3DService.ClearInnovationAuditCache();
		Lux3DService lux3DService = Lux3DService.Ensure();
		lux3DService.SetMode("mock");
		lux3DService.OverrideSessionCap(50);
		WordMaterializerSystem words = UnityEngine.Object.FindObjectOfType<WordMaterializerSystem>();
		BakeryEffects fx = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
		Check("word_system_present", words != null && fx != null);
		if (words == null)
		{
			yield break;
		}
		GameState st = boot.state;
		st.flags.Remove("letters_taken");
		Check("word_no_slips_before_letters", words.PocketSlips().Count == 0, "count=" + words.PocketSlips().Count);
		if (!st.flags.Contains("letters_taken"))
		{
			st.flags.Add("letters_taken");
		}
		st.step = "B1_matches";
		List<WordSlipDef> list = words.PocketSlips();
		Check("word_pocket_has_three_letters", list.Count == 8, "count=" + list.Count);
		int num = 0;
		string text = "";
		foreach (WordSlipDef item in list)
		{
			LetterData letter = boot.director.GetLetter(item.letter);
			if (letter != null && letter.body.Contains(item.word))
			{
				num++;
			}
			else
			{
				text = text + item.word + " ";
			}
		}
		Check("word_all_in_letter_text", num == list.Count, text);
		Vector3 stove = Vector3.zero;
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			if (bakeryItem.id == "stove")
			{
				stove = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
			}
		}
		Vector3 stand = FindWalkableNear(stove, 1.5f, 4f);
		Teleport(stand);
		yield return null;
		Vector3 land = Vector3.Lerp(stand, stove, 0.6f);
		land.y = boot.world.GroundAt(land.x, land.z, stove.y + 1f);
		yield return words.ThrowSlip("fire", land);
		yield return new WaitForSeconds(0.3f);
		Check("word_fire_gives_matches", st.flags.Contains("matches_taken") && !st.flags.Contains("oven_lit"), "step=" + st.step);
		yield return words.ThrowSlip("wood", land);
		yield return new WaitForSeconds(0.3f);
		Check("word_wet_wood_loaded", st.flags.Contains("wood_loaded") && st.flags.Contains("wood_wet") && words.LastActionApplied, "step=" + st.step);
		Check("word_wet_wood_spawn", words.LastSpawn != null && words.LastSpawn.root != null && words.LastSpawn.state == "generating", (words.LastSpawn != null) ? words.LastSpawn.state : "null");
		GameObject wetRoot = ((words.LastSpawn != null) ? words.LastSpawn.root : null);
		yield return Snap("innov_word_wet_wood.png", land, 2.4f, 30f);
		boot.director.LightOven(fx);
		yield return new WaitForSeconds(0.5f);
		Check("word_wet_wood_wont_light", !st.flags.Contains("oven_lit") && fx.WetPuffCount == 1 && st.flags.Contains("wet_fizzled"), "puffs=" + fx.WetPuffCount);
		string stepBefore = st.step;
		words.OpenSpecificLetter(0);
		yield return new WaitForSecondsRealtime(0.3f);
		bool opened = boot.ui.LetterOpen;
		bool highlighted = boot.ui.LetterBodyText != null && boot.ui.LetterBodyText.text.Contains("<color=");
		yield return SnapScreen("innov_word_letter.png");
		boot.ui.CloseLetter();
		yield return new WaitForSecondsRealtime(0.3f);
		Check("word_reread_no_skip", opened && st.step == stepBefore && !boot.player.locked, "before=" + stepBefore + " after=" + st.step);
		Check("word_letter_highlight", highlighted);
		yield return words.ThrowSlip("dry", land);
		Check("word_single_char_noop", st.flags.Contains("wood_wet") && words.LastSpawn == null);
		WordSlipDef wordSlipDef = words.Combine("dry", "wood");
		Check("word_combo_dry_wood", wordSlipDef != null && wordSlipDef.id == "dry_wood" && st.flags.Contains("combo_dry_wood"));
		Check("word_combo_in_pocket", words.PocketSlips().Count == 9, "count=" + words.PocketSlips().Count);
		yield return words.ThrowSlip("dry_wood", land);
		yield return new WaitForSeconds(0.3f);
		Check("word_dry_wood_replaces_wet", st.flags.Contains("wood_loaded") && !st.flags.Contains("wood_wet") && words.LastActionApplied && wetRoot == null);
		boot.director.LightOven(fx);
		float t = Time.realtimeSinceStartup;
		while (!st.flags.Contains("oven_lit") && Time.realtimeSinceStartup - t < 5f)
		{
			yield return null;
		}
		Check("word_oven_lights_with_dry_wood", st.flags.Contains("oven_lit") && fx.Lit, "step=" + st.step);
		yield return Snap("innov_word_oven_lit.png", stove, 4f, 30f);
		string stepNow = st.step;
		WordSlipDef wordSlipDef2 = words.Combine("warm", "ball");
		Check("word_free_combo", wordSlipDef2 != null && wordSlipDef2.action == "keepsake" && wordSlipDef2.word == "暖球", (wordSlipDef2 != null) ? wordSlipDef2.word : "null");
		if (wordSlipDef2 != null)
		{
			yield return words.ThrowSlip(wordSlipDef2.id, land + new Vector3(0.8f, 0f, 0.4f));
			Check("word_free_combo_keepsake", words.LastSpawn != null && words.LastSpawn.root != null && st.step == stepNow);
		}
		t = Time.realtimeSinceStartup;
		while (Time.realtimeSinceStartup - t < 6f)
		{
			yield return null;
		}
		Check("word_swaps_done", words.LastSpawn == null || words.LastSpawn.state == "swapped", (words.LastSpawn != null) ? words.LastSpawn.state : "null");
		yield return new WaitForSecondsRealtime(4.5f);
	}

	private IEnumerator SnapScreen(string file)
	{
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
		yield return new WaitForSecondsRealtime(0.6f);
	}

	private IEnumerator AuditLux3DAndWish()
	{
		Lux3DService.ClearInnovationAuditCache();
		Lux3DService lux = Lux3DService.Ensure();
		lux.SetMode("mock");
		Check("lux_mock_mode", lux.Mode == "mock");
		Check("lux_library_loaded", lux.Library.Count >= 8, "entries=" + lux.Library.Count);
		int num = 0;
		string text = "";
		foreach (Lux3DLibraryEntry item in lux.Library)
		{
			GameObject gameObject = lux.InstantiateFallback(item);
			if (gameObject == null)
			{
				num++;
				text = text + item.id + " ";
			}
			else
			{
				UnityEngine.Object.Destroy(gameObject);
			}
		}
		Check("lux_library_all_models", num == 0, text);
		AIAgentCalibrationDirector wish = UnityEngine.Object.FindObjectOfType<AIAgentCalibrationDirector>();
		WorldMechanismDirector mech = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
		Check("wish_system_present", wish != null && mech != null);
		if (wish == null || mech == null)
		{
			yield break;
		}
		_ = boot.player;
		Vector3 bowl = mech.GetDogBowlPosition();
		boot.state.step = "S1_slipper";
		Teleport(bowl + new Vector3(-1.2f, 0f, -0.6f));
		yield return null;
		wish.SpawnLiveWishObjectInWorld("香喷喷的烤肉骨头");
		Lux3DSpawn s1 = wish.LastWishSpawn;
		Check("wish_food_kind", wish.LastWishKind == "food", wish.LastWishKind);
		Check("wish_food_fallback_instant", s1 != null && s1.root != null && s1.root.transform.Find("Model") != null);
		Check("wish_food_generating", s1 != null && s1.state == "generating", (s1 != null) ? s1.state : "null");
		Check("wish_food_story", boot.state.flags.Contains("ball_in_bowl"));
		yield return Snap("innov_wish_food_fallback.png", s1?.root.transform.position ?? bowl, 2.2f);
		GameObject rootBefore = s1?.root;
		float t2 = Time.realtimeSinceStartup;
		while (s1 != null && s1.state == "generating" && Time.realtimeSinceStartup - t2 < 40f)
		{
			yield return null;
		}
		yield return new WaitForSeconds(1f);
		Check("wish_food_swapped", s1 != null && s1.state == "swapped", (s1 != null) ? (s1.state + " " + s1.seconds.ToString("F1") + "s") : "null");
		bool flag = s1 != null && s1.root == rootBefore && s1.root != null;
		Transform transform = (flag ? s1.root.transform.Find("Model") : null);
		Check("wish_food_same_root", flag && transform != null && s1.root.transform.Find("ModelOld") == null);
		if (transform != null)
		{
			Quaternion rotation = s1.root.transform.rotation;
			s1.root.transform.rotation = Quaternion.identity;
			Bounds bounds = Lux3DModelFit.WorldBounds(transform.gameObject);
			s1.root.transform.rotation = rotation;
			float num2 = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
			float num3 = ((s1.entry != null) ? s1.entry.size : 0.4f);
			Check("wish_food_size_fit", Mathf.Abs(num2 - num3) < num3 * 0.15f, $"max={num2:F2} want={num3:F2}");
			Check("wish_food_on_ground", Mathf.Abs(bounds.min.y - s1.root.transform.position.y) < 0.05f, $"minY={bounds.min.y:F2} rootY={s1.root.transform.position.y:F2}");
			Check("wish_food_has_mesh", transform.GetComponentsInChildren<Renderer>().Length != 0);
		}
		Check("wish_food_story_kept", boot.state.flags.Contains("ball_in_bowl"));
		yield return Snap("innov_wish_food_swapped.png", (s1 != null && s1.root != null) ? s1.root.transform.position : bowl, 2.2f);
		t2 = Time.realtimeSinceStartup;
		while (!boot.state.flags.Contains("school_read") && Time.realtimeSinceStartup - t2 < 25f)
		{
			if (boot.ui.LetterOpen && Time.realtimeSinceStartup - t2 > 3f)
			{
				boot.ui.CloseLetter();
			}
			yield return null;
		}
		yield return new WaitForSecondsRealtime(4.5f);
		if (s1 != null && s1.root != null)
		{
			yield return Snap("innov_wish_food_swapped.png", s1.root.transform.position, 1.6f);
		}
		Vector3 ch = mech.GetCrossingChannelCenter();
		float staticY = WaterLevel.StaticY;
		Check("wish_boat_water_L2", Mathf.Abs(staticY - 1.15f) < 0.08f, "waterY=" + staticY.ToString("F2"));
		bool before = boot.world.Walkable(ch.x, ch.z);
		Teleport(FindWalkableNear(ch, 3.5f, 6.5f));
		yield return null;
		WordMaterializerSystem words = UnityEngine.Object.FindObjectOfType<WordMaterializerSystem>();
		if (words != null)
		{
			WordSlipDef wordSlipDef = words.Combine("wood", "boat");
			Check("word_boat_combo", wordSlipDef != null && wordSlipDef.id == "wood_boat" && boot.state.flags.Contains("combo_wood_boat"));
			Check("word_boat_gap_before", !boot.world.Walkable(ch.x, ch.z));
			yield return words.ThrowSlip("wood_boat", ch);
			yield return new WaitForSeconds(0.6f);
			Check("word_boat_walkable", boot.world.Walkable(ch.x, ch.z) && words.LastActionApplied, $"applied={words.LastActionApplied} ground={boot.world.GroundAt(ch.x, ch.z):F2} water={WaterLevel.StaticY:F2}");
			Check("word_boat_solved", mech.CrossingSolved && boot.state.flags.Contains("dock_winch_raised"));
			yield return Snap("innov_word_boat.png", ch, 5f, 200f);
			if (words.LastSpawn != null && words.LastSpawn.root != null)
			{
				UnityEngine.Object.Destroy(words.LastSpawn.root);
			}
			mech.DebugResetCrossing();
			yield return new WaitForSeconds(0.3f);
			before = boot.world.Walkable(ch.x, ch.z);
		}
		wish.SpawnLiveWishObjectInWorld("戴草帽的海龟船");
		Lux3DSpawn s2 = wish.LastWishSpawn;
		Check("wish_boat_kind", wish.LastWishKind == "boat", wish.LastWishKind);
		yield return new WaitForSeconds(0.5f);
		bool flag2 = boot.world.Walkable(ch.x, ch.z);
		float num4 = boot.world.GroundAt(ch.x, ch.z);
		Check("crossing_gap_blocks_at_L2", !before, "channel=" + ch.ToString("F2"));
		Check("wish_boat_walkable", flag2, $"before={before} after={flag2} ground={num4:F2} water={WaterLevel.StaticY:F2}");
		Check("wish_boat_flag", boot.state.flags.Contains("dock_winch_raised"));
		yield return Snap("innov_wish_boat.png", ch, 5f, 200f);
		t2 = Time.realtimeSinceStartup;
		while (s2 != null && s2.state == "generating" && Time.realtimeSinceStartup - t2 < 40f)
		{
			yield return null;
		}
		yield return new WaitForSeconds(1f);
		Check("wish_boat_swapped", s2 != null && s2.state == "swapped", (s2 != null) ? s2.state : "null");
		Check("wish_boat_still_walkable", boot.world.Walkable(ch.x, ch.z));
		yield return Snap("innov_wish_boat_swapped.png", ch, 5f, 200f);
		int used = lux.GeneratedThisSession;
		Teleport(FindWalkableNear(bowl, 2f, 5f));
		yield return null;
		wish.SpawnLiveWishObjectInWorld("星空海螺八音盒");
		Lux3DSpawn s3 = wish.LastWishSpawn;
		Check("wish_keepsake_kind", wish.LastWishKind == "keepsake", wish.LastWishKind);
		t2 = Time.realtimeSinceStartup;
		while (s3 != null && s3.state == "generating" && Time.realtimeSinceStartup - t2 < 40f)
		{
			yield return null;
		}
		Check("wish_keepsake_swapped", s3 != null && s3.state == "swapped", (s3 != null) ? s3.state : "null");
		if (s3 != null && s3.root != null)
		{
			yield return Snap("innov_wish_keepsake.png", s3.root.transform.position, 2.6f);
		}
		int generatedThisSession = lux.GeneratedThisSession;
		wish.SpawnLiveWishObjectInWorld("星空海螺八音盒");
		Lux3DSpawn lastWishSpawn = wish.LastWishSpawn;
		Check("wish_cache_hit", lastWishSpawn != null && lastWishSpawn.state == "cached" && lux.GeneratedThisSession == generatedThisSession, (lastWishSpawn != null) ? lastWishSpawn.state : "null");
		Check("wish_session_count", generatedThisSession == used + 1, $"{used}->{generatedThisSession}");
		lux.SetMode("offline");
		wish.SpawnLiveWishObjectInWorld("招财橘猫雕塑" + UnityEngine.Random.Range(1000, 9999));
		Lux3DSpawn lastWishSpawn2 = wish.LastWishSpawn;
		Check("wish_offline_fallback_only", lastWishSpawn2 != null && lastWishSpawn2.state == "offline" && lastWishSpawn2.root.transform.Find("Model") != null, (lastWishSpawn2 != null) ? lastWishSpawn2.state : "null");
		lux.SetMode("mock");
	}

	private void Teleport(Vector3 pos)
	{
		EggboyPlayer player = boot.player;
		pos.y = boot.world.GroundAt(pos.x, pos.z, pos.y + 2f);
		player.transform.position = pos;
		player.vel = Vector3.zero;
	}

	private Vector3 FindWalkableNear(Vector3 c, float rMin, float rMax)
	{
		for (float num = rMin; num <= rMax; num += 0.5f)
		{
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector3 result = c + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
				if (boot.world.Walkable(result.x, result.z))
				{
					return result;
				}
			}
		}
		return c;
	}

	private IEnumerator Snap(string file, Vector3 target, float dist, float yawDeg = -40f)
	{
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		Camera cam = Camera.main;
		ChaseCamera chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		bool chaseOn = chase != null && chase.enabled;
		if (chase != null)
		{
			chase.enabled = false;
		}
		Vector3 p0 = cam.transform.position;
		Quaternion r0 = cam.transform.rotation;
		float f0 = cam.fieldOfView;
		float f = yawDeg * (MathF.PI / 180f);
		Vector3 vector = target + new Vector3(Mathf.Sin(f) * dist * 0.85f, dist * 0.6f, Mathf.Cos(f) * dist * 0.85f);
		cam.transform.position = vector;
		cam.transform.rotation = Quaternion.LookRotation(target + Vector3.up * 0.2f - vector, Vector3.up);
		cam.fieldOfView = 42f;
		yield return null;
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
		yield return new WaitForSecondsRealtime(0.6f);
		cam.transform.SetPositionAndRotation(p0, r0);
		cam.fieldOfView = f0;
		if (chase != null)
		{
			chase.enabled = chaseOn;
		}
	}
}
