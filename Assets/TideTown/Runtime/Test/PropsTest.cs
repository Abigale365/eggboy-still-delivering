using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class PropsTest
{
	public static IEnumerator Run(MonoBehaviour host, EggboyPlayer player, IslandWorld world)
	{
		GameBootstrap boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
		StoryDirector director = ((boot != null) ? boot.director : null);
		PhysicalPickupController pk = PhysicalPickupController.Instance;
		BakeryEffects fx = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
		Camera cam = Camera.main;
		ChaseCamera chase = ((cam != null) ? cam.GetComponent<ChaseCamera>() : null);
		string dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		StringBuilder sb = new StringBuilder();
		int pass = 0;
		int total = 0;
		if (boot == null || director == null || pk == null || fx == null || cam == null || !pk.Active("pickup_wood"))
		{
			Check("setup", ok: false, string.Format("boot={0} director={1} pk={2} fx={3} active={4}", boot != null, director != null, pk != null, fx != null, pk != null && pk.Active("pickup_wood")));
			Done();
			yield break;
		}
		director.StopAllCoroutines();
		director.enabled = false;
		GameState st = boot.state;
		Time.captureFramerate = 30;
		st.flags.Add("letters_taken");
		st.flags.Add("matches_taken");
		st.flags.Add("matches_packed");
		st.flags.Remove("wood_loaded");
		st.flags.Remove("wood_wet");
		st.flags.Remove("oven_lit");
		st.carrying = "";
		st.step = "B2_wood";
		director.DebugSyncProps();
		yield return Frames(3);
		Vector3 wood = Item("wood");
		Check("wood_pile_visible_before", director.WoodPileVisible, "");
		Place(wood + new Vector3(0.2f, 0f, -1.3f), wood);
		yield return Frames(5);
		director.TakeWoodAt(wood);
		yield return WaitIdle(8f);
		yield return Frames(10);
		GameObject carriedVisual = director.CarriedVisual;
		Check("wood_taken", st.carrying == "wood" && carriedVisual != null, "carry=" + st.carrying);
		Check("wood_pile_hidden", !director.WoodPileVisible, "");
		if (carriedVisual != null)
		{
			Bounds bounds = HandCarry.LocalBounds(carriedVisual, player.transform);
			float num = pk.Grip.ShellFrontAt(player.transform, bounds.center.y, bounds.size.x * 0.4f, bounds.size.y * 0.5f);
			Check("wood_centered", Mathf.Abs(bounds.center.x) < 0.03f, "x=" + F(bounds.center.x));
			Check("wood_clear_of_shell", bounds.min.z >= num - 0.005f, "back=" + F(bounds.min.z) + " shellFront=" + F(num));
			Check("wood_follows_body", carriedVisual.transform.parent == player.CarryAnchor, "parent=" + ((carriedVisual.transform.parent != null) ? carriedVisual.transform.parent.name : "null"));
		}
		yield return Shot("05_wood_carry_front", player.transform.position + Vector3.up * 0.45f, PlayerYawDeg() + 180f, 14f, 1.5f);
		Vector3 stove = Item("stove");
		for (int i = 0; i < 40; i++)
		{
			Vector3 vector = new Vector3(stove.x - player.transform.position.x, 0f, stove.z - 1.9f - player.transform.position.z);
			if (vector.magnitude > 0.2f)
			{
				vector.Normalize();
			}
			else
			{
				vector = Vector3.zero;
			}
			player.Move(1f / 30f, vector.x, vector.z, run: false, jumpPressed: false);
			if (i == 24)
			{
				yield return Shot("06_wood_carry_walk", player.transform.position + Vector3.up * 0.45f, PlayerYawDeg() - 70f, 12f);
			}
			yield return null;
		}
		Place(new Vector3(fx.LoadPoint.x, stove.y, fx.LoadPoint.z - 1.2f), fx.LoadPoint);
		yield return Frames(5);
		director.LoadWoodAt(stove);
		bool shotLoad = false;
		float t1 = Time.time;
		float loadStartedAt = float.NaN;
		yield return null;
		while ((pk.Busy || player.ActionLocked) && Time.time - t1 < 8f)
		{
			if (pk.LastEvent.StartsWith("act load_wood") && float.IsNaN(loadStartedAt))
			{
				loadStartedAt = Time.time;
			}
			if (!shotLoad && Time.time - loadStartedAt >= 0.33f && director.CarriedVisual != null)
			{
				shotLoad = true;
				Vector3 focus2 = Vector3.Lerp(player.transform.position + Vector3.up * 0.48f, fx.LoadPoint + Vector3.up * 0.15f, 0.5f);
				yield return Shot("07_wood_into_oven", focus2, PlayerYawDeg() - 42f, 10f, 2.15f, 50f);
			}
			yield return null;
		}
		yield return Frames(6);
		Check("wood_loaded", st.flags.Contains("wood_loaded") && st.carrying != "wood" && director.CarriedVisual == null, "step=" + st.step + " event=" + pk.LastEvent);
		Transform transform = fx.transform.Find("OvenLoadedWood");
		bool flag = transform != null && Array.Exists(transform.GetComponentsInChildren<Renderer>(), (Renderer r) => r.enabled);
		float num2 = ((transform != null) ? (HandCarry.LocalBounds(transform.gameObject, fx.transform).min.z - fx.mouthLocal.z) : 0f);
		Check("oven_has_wood_inside", fx.HasLoadedWood && !fx.LoadedWoodWet && flag && num2 > 0.01f, "renderer=" + flag + " inset=" + F(num2));
		Check("wood_pile_still_hidden", !director.WoodPileVisible, "");
		Vector3 mouth = fx.LoadPoint;
		yield return Shot("08_oven_loaded_wood", mouth, 0f, 6f, 0.9f, 50f);
		fx.SetLoadedWood(loaded: true, wet: true);
		yield return Frames(2);
		Check("oven_wet_wood_swaps", fx.HasLoadedWood && fx.LoadedWoodWet, "");
		fx.WetPuff();
		yield return Frames(14);
		yield return Shot("09_oven_wet_puff", mouth, 0f, 8f, 1.2f, 50f);
		fx.SetLoadedWood(loaded: true, wet: false);
		fx.Light();
		yield return Frames(45);
		Vector3 vector2 = fx.transform.InverseTransformPoint(fx.FireMouth);
		Check("fire_inside_chamber", vector2.z > fx.mouthLocal.z + 0.05f && vector2.y > fx.chamberFloorLocal.y - 0.01f, "fireLocal=" + vector2.ToString("F3") + " mouthZ=" + F(fx.mouthLocal.z));
		Quaternion quaternion = Quaternion.Euler(8f, 0f, 0f);
		cam.transform.SetPositionAndRotation(mouth - quaternion * Vector3.forward * 1.4f, quaternion);
		cam.fieldOfView = 50f;
		yield return null;
		Check("fire_visible_from_front", fx.FireVisibleFrom(cam) && fx.FireCount > 0, "count=" + fx.FireCount);
		yield return Shot("10_oven_fire_inside", mouth, 0f, 8f, 1.1f, 50f);
		yield return Shot("11_oven_fire_glow_street", mouth + Vector3.down * 0.3f, 0f, 20f, 3.2f, 50f);
		Vector3 focus3 = fx.transform.TransformPoint(fx.chimneyLocalPosition);
		yield return Shot("12_chimney_smoke", focus3, 20f, 10f, 4.5f, 50f);
		LighthouseItem lighthouseItem = null;
		LighthouseItem[] items = LighthouseData.Load().items;
		foreach (LighthouseItem lighthouseItem2 in items)
		{
			if (lighthouseItem2.id == "oil")
			{
				lighthouseItem = lighthouseItem2;
			}
		}
		Vector3 oil = new Vector3(lighthouseItem.x, lighthouseItem.y, lighthouseItem.z);
		st.flags.Remove("oil_taken");
		st.carrying = "";
		st.step = "L1_shed";
		director.DebugSyncProps();
		yield return Frames(3);
		Check("oil_can_visible_before", director.OilCanVisible, "");
		Place(oil + new Vector3(-1.1f, 0f, 0.2f), oil);
		yield return Frames(5);
		director.TakeOilAt(oil);
		yield return WaitIdle(8f);
		yield return Frames(15);
		HandCarry hc = ((director.CarriedVisual != null) ? director.CarriedVisual.GetComponent<HandCarry>() : null);
		Check("oil_taken", st.carrying == "oil" && hc != null, "carry=" + st.carrying);
		Check("oil_can_hidden", !director.OilCanVisible, "");
		if (hc != null)
		{
			Check("oil_handle_in_palm", hc.PalmError < 0.03f, "err=" + F(hc.PalmError));
			yield return Shot("13_oil_carry_side", player.transform.position + Vector3.up * 0.32f, PlayerYawDeg() - 90f, 10f, 1.4f);
			float maxErr = 0f;
			float minShell = 1f;
			float minGround = 1f;
			float maxTilt = 0f;
			for (int i = 0; i < 90; i++)
			{
				float f = (float)i / 90f * MathF.PI * 2f;
				player.Move(1f / 30f, Mathf.Cos(f), Mathf.Sin(f), i > 45, jumpPressed: false);
				yield return null;
				maxErr = Mathf.Max(maxErr, hc.PalmError);
				minShell = Mathf.Min(minShell, hc.ShellGap);
				minGround = Mathf.Min(minGround, hc.GroundGap);
				maxTilt = Mathf.Max(maxTilt, Vector3.Angle(hc.transform.up, Vector3.up));
				if (i == 30)
				{
					yield return Shot("14_oil_walk", player.transform.position + Vector3.up * 0.32f, PlayerYawDeg() - 90f, 30f, 1.5f);
				}
				if (i == 52)
				{
					yield return Shot("15_oil_run", player.transform.position + Vector3.up * 0.25f, PlayerYawDeg() - 90f, 62f, 1.8f, 50f);
				}
			}
			Check("oil_walk_handle_stays", maxErr < 0.06f, "maxErr=" + F(maxErr));
			Check("oil_walk_no_shell_clip", minShell >= -0.01f, "minShellGap=" + F(minShell));
			Check("oil_walk_off_ground", minGround >= 0.015f, "minGroundGap=" + F(minGround));
			Check("oil_swings", maxTilt > 2f, "maxTilt=" + F(maxTilt));
		}
		LighthouseList lighthouseList = LighthouseData.Load();
		Vector3 lamp = LighthouseData.Point(lighthouseList.lamp);
		Vector3 at2 = LighthouseData.Point(lighthouseList.deck);
		st.step = "L3_oil";
		st.flags.Remove("lamp_oiled");
		Place(at2, lamp);
		yield return Frames(10);
		director.OilLampAt(lamp);
		bool pourShot = false;
		float maxPour = 0f;
		float t2 = Time.time;
		while (Time.time - t2 < 4f)
		{
			if (hc != null)
			{
				maxPour = Mathf.Max(maxPour, Vector3.Angle(hc.transform.up, Vector3.up));
			}
			if (!pourShot && pk.LastEvent.StartsWith("contact") && hc != null && Vector3.Angle(hc.transform.up, Vector3.up) > 40f)
			{
				pourShot = true;
				Vector3 vector3 = player.transform.position - lamp;
				vector3.y = 0f;
				vector3.Normalize();
				Vector3 vector4 = Vector3.Cross(Vector3.up, vector3);
				Vector3 vector5 = player.transform.position + vector3 * 0.17f + vector4 * 0.83f + Vector3.up * 0.7f;
				Vector3 vector6 = Vector3.Lerp(player.transform.position + Vector3.up * 0.42f, hc.transform.position, 0.48f);
				bool on = chase != null && chase.enabled;
				if (chase != null)
				{
					chase.enabled = false;
				}
				cam.transform.SetPositionAndRotation(vector5, Quaternion.LookRotation(vector6 - vector5));
				cam.fieldOfView = 62f;
				yield return ShotTool.Capture(cam, 1920, 1080, Path.Combine(dir, "prop_fix_16_oil_pour.png"));
				if (chase != null)
				{
					chase.enabled = on;
				}
			}
			if (director.CarriedVisual == null && Time.time - t2 > 1f)
			{
				break;
			}
			yield return null;
		}
		Check("oil_pours_then_removed", maxPour > 45f && director.CarriedVisual == null && st.flags.Contains("lamp_oiled"), "maxPour=" + F(maxPour));
		if (boot.ui.LetterOpen)
		{
			boot.ui.CloseLetter();
		}
		yield return Frames(5);
		Vector3 bell = Item("bell");
		BellSwing swing = GameObject.Find("Bakery_bell")?.GetComponent<BellSwing>();
		Place(bell + new Vector3(0f, 0f, -1.3f), bell);
		yield return Frames(6);
		yield return Shot("01_bell_on_door", (swing != null) ? (swing.TouchPoint + Vector3.up * 0.1f) : bell, 0f, 4f);
		int rings0 = ((swing != null) ? swing.RingCount : 0);
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			if (bakeryItem.id == "bell")
			{
				director.RingBellAt(bell, bakeryItem.label, bakeryItem.text);
			}
		}
		bool bellShot = false;
		float t3 = Time.time;
		yield return null;
		while ((pk.Busy || player.ActionLocked) && Time.time - t3 < 8f)
		{
			if (!bellShot && pk.LastEvent.StartsWith("contact"))
			{
				bellShot = true;
				yield return Shot("02_bell_ring", (swing != null) ? swing.TouchPoint : bell, PlayerYawDeg() - 65f, 12f, 1.5f);
			}
			yield return null;
		}
		yield return Frames(4);
		Check("bell_rings_on_touch", swing != null && swing.RingCount == rings0 + 1 && pk.LastEvent.StartsWith("contact"), "event=" + pk.LastEvent + " rings=" + ((swing != null) ? swing.RingCount : (-1)));
		if (ObjectInspectModal.IsOpen)
		{
			ObjectInspectModal.DebugClose();
		}
		Done();
		void Check(string name, bool ok, string note)
		{
			total++;
			if (ok)
			{
				pass++;
			}
			sb.Append(name).Append(ok ? "=ok " : "=FAIL ").Append(note)
				.Append(" | ");
			Debug.Log("PROPSTEST " + name + "=" + ok + " " + note);
		}
		void Done()
		{
			string text = "{\"status\":\"" + ((pass == total && total > 0) ? "passed" : "failed") + "\",\"mode\":\"props\",\"note\":\"" + pass + "/" + total + " " + sb.ToString().Replace("\"", "'") + "\"}";
			File.WriteAllText(Path.Combine(Application.dataPath, "..", "feel-demo-props.json"), text);
			Debug.Log("FEELDEMO " + text);
			Time.captureFramerate = 0;
			Application.Quit((pass != total) ? 1 : 0);
		}
		static string F(float v)
		{
			return v.ToString("F3", CultureInfo.InvariantCulture);
		}
		IEnumerator Frames(int n)
		{
			for (int m = 0; m < n; m++)
			{
				if (!pk.Busy)
				{
					player.Move(1f / 30f, 0f, 0f, run: false, jumpPressed: false);
				}
				yield return null;
			}
		}
		static Vector3 Item(string id)
		{
			BakeryItem[] array2 = BakeryData.Load();
			foreach (BakeryItem bakeryItem2 in array2)
			{
				if (bakeryItem2.id == id)
				{
					return new Vector3(bakeryItem2.x, bakeryItem2.y, bakeryItem2.z);
				}
			}
			return Vector3.zero;
		}
		void Place(Vector3 at, Vector3 face)
		{
			at.y = world.GroundAt(at.x, at.z, at.y + 1f);
			player.ResetJumpInput();
			player.transform.position = at + Vector3.up * 0.01f;
			player.vel = Vector3.zero;
			player.grounded = true;
			player.jump.phase = JumpPhase.none;
			player.locked = false;
			player.FaceTowards(face);
			player.transform.rotation = Quaternion.Euler(0f, player.yaw * 57.29578f, 0f);
			if (chase != null)
			{
				chase.SnapBehind(player.transform.position, player.yaw);
			}
		}
		float PlayerYawDeg()
		{
			return player.transform.eulerAngles.y;
		}
		IEnumerator Shot(string name, Vector3 focus, float yawDeg, float pitch = 22f, float dist = 1.6f, float fov = 45f)
		{
			bool on2 = chase != null && chase.enabled;
			if (chase != null)
			{
				chase.enabled = false;
			}
			Quaternion quaternion2 = Quaternion.Euler(pitch, yawDeg, 0f);
			cam.transform.SetPositionAndRotation(focus - quaternion2 * Vector3.forward * dist, quaternion2);
			cam.fieldOfView = fov;
			yield return ShotTool.Capture(cam, 1920, 1080, Path.Combine(dir, "prop_fix_" + name + ".png"));
			if (chase != null)
			{
				chase.enabled = on2;
			}
		}
		IEnumerator WaitIdle(float max, Func<bool> onFrame = null)
		{
			float t4 = Time.time;
			yield return null;
			while ((pk.Busy || player.ActionLocked) && Time.time - t4 < max)
			{
				onFrame?.Invoke();
				yield return null;
			}
		}
	}
}
