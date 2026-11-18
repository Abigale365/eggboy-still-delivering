using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class StoryAudit : MonoBehaviour
{
	[Serializable]
	private class Wp
	{
		public string id;

		public bool stop;

		public float x;

		public float z;
	}

	[Serializable]
	private class RouteList
	{
		public Wp[] route;
	}

	private GameBootstrap boot;

	private float sumFps;

	private int frames;

	private string error = "";

	private string status = "passed";

	private string walkFail = "";

	private string walkLast = "";

	private int recoveries;

	private DogFollower dog;

	private float dogMax;

	private float dogMinClear = 999f;

	public void Begin(GameBootstrap b)
	{
		boot = b;
		if (boot.ui != null)
		{
			boot.ui.SetAuditMode(on: true);
		}
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return new WaitForSeconds(0.5f);
		Time.timeScale = 1f;
		EggboyPlayer p = boot.player;
		dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		float bootAt = ((boot.intro != null) ? boot.intro.StartTime : Time.realtimeSinceStartup);
		bool introRan = boot.intro != null;
		bool introDone = !introRan;
		bool faceOk = true;
		float introSeconds = 0f;
		string exprStart = ((p.pose != null) ? p.pose.expr.ToString() : "");
		string exprEnd = exprStart;
		if (introRan)
		{
			yield return new WaitForSeconds(1.5f);
			yield return Snap("story_boot.png");
			while (!boot.intro.Done && Time.realtimeSinceStartup - bootAt < 12f)
			{
				yield return null;
			}
			introDone = boot.intro.Done;
			introSeconds = Time.realtimeSinceStartup - bootAt;
			exprEnd = ((p.pose != null) ? p.pose.expr.ToString() : "");
			faceOk = exprStart == ExprId.sleepy.ToString() && exprEnd == ExprId.happy.ToString();
			if (!introDone)
			{
				status = "intro_timeout";
				error = "intro did not finish in 12 s";
			}
			else if (!faceOk)
			{
				status = "intro_face_wrong";
				error = exprStart + " -> " + exprEnd;
			}
			yield return Snap("story_wake.png");
		}
		else
		{
			yield return new WaitForSeconds(0.5f);
		}
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		Wp[] route = JsonUtility.FromJson<RouteList>(textAsset.text).route;
		int i2 = -1;
		int i5 = -1;
		int i6 = -1;
		int i7 = -1;
		int i8 = -1;
		int i9 = -1;
		int i10 = -1;
		int i22b = -1;
		int i11 = -1;
		int i12 = -1;
		int i30b = -1;
		int i4 = -1;
		int i3 = -1;
		for (int n = 0; n < route.Length; n++)
		{
			if (i2 < 0 && route[n].id == "03")
			{
				i2 = n;
			}
			else if (i5 < 0 && route[n].id == "11")
			{
				i5 = n;
			}
			else if (i6 < 0 && route[n].id == "12")
			{
				i6 = n;
			}
			else if (i7 < 0 && route[n].id == "13")
			{
				i7 = n;
			}
			else if (i8 < 0 && route[n].id == "20")
			{
				i8 = n;
			}
			else if (i9 < 0 && route[n].id == "21")
			{
				i9 = n;
			}
			if (route[n].id == "07")
			{
				i4 = n;
			}
			if (route[n].id == "06")
			{
				i3 = n;
			}
			if (route[n].id == "22")
			{
				if (i10 < 0)
				{
					i10 = n;
				}
				else if (i22b < 0)
				{
					i22b = n;
				}
			}
			if (i11 < 0 && route[n].id == "25")
			{
				i11 = n;
			}
			if (route[n].id == "30")
			{
				if (i12 < 0)
				{
					i12 = n;
				}
				else if (i30b < 0)
				{
					i30b = n;
				}
			}
		}
		if (status == "passed")
		{
			yield return WalkRoute(route, 0, i2);
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
		}
		bool opened = false;
		if (status == "passed")
		{
			yield return new WaitForSeconds(0.4f);
			if (!boot.service.TryInteract("mailbox_03"))
			{
				status = "interact_failed";
				error = "no mailbox in range";
			}
			else
			{
				for (int j = 0; j < 90; j++)
				{
					if (boot.ui.LetterOpen)
					{
						opened = true;
						break;
					}
					yield return null;
				}
				if (!opened)
				{
					status = "letter_not_open";
					error = "ui.LetterOpen false";
				}
			}
		}
		float pickupSeconds = Time.realtimeSinceStartup - bootAt;
		bool within30 = pickupSeconds <= 30f;
		if (status == "passed")
		{
			yield return Snap("story_pickup.png");
		}
		if (status == "passed" && !within30)
		{
			status = "pickup_too_slow";
			error = pickupSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s";
		}
		bool stepOk = false;
		bool lockedMove = false;
		bool noRepeat = false;
		if (status == "passed")
		{
			stepOk = boot.state.step == "P1_mailbox" && boot.state.flags.Contains("letters_taken");
			Vector3 position = p.transform.position;
			p.Move(0.05f, 1f, 0f, run: false, jumpPressed: false);
			lockedMove = (p.transform.position - position).magnitude < 0.01f;
			boot.ui.CloseLetter();
			yield return new WaitForSeconds(0.3f);
			noRepeat = !boot.service.TryInteract("mailbox_03");
			if (!stepOk)
			{
				status = "state_wrong";
				error = "step=" + boot.state.step;
			}
			else if (p.locked)
			{
				status = "still_locked";
				error = "player.locked true";
			}
			else if (!lockedMove)
			{
				status = "move_while_reading";
				error = "player moved with letter open";
			}
			else if (!noRepeat)
			{
				status = "repeat_interact";
				error = "mailbox still interactable";
			}
			else if (boot.state.step != "B0_arrive")
			{
				status = "b0_missing";
				error = "step=" + boot.state.step;
			}
		}
		float bakeryAt = Time.realtimeSinceStartup;
		bool b0Auto = false;
		bool memoryOk = false;
		bool matchesOk = false;
		bool woodOk = false;
		bool loadOk = false;
		bool litOk = false;
		bool stampOk = false;
		bool waterOk = false;
		bool smokeOk = false;
		bool fireVisibleOven = false;
		bool smokeVisibleOven = false;
		BakeryEffects fx = null;
		if (status == "passed")
		{
			yield return WalkRoute(route, i2 + 1, i5);
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			for (int j = 0; j < 120; j++)
			{
				if (!(status == "passed"))
				{
					break;
				}
				if (boot.ui.LetterOpen)
				{
					b0Auto = true;
					break;
				}
				yield return null;
			}
			if (status == "passed" && !b0Auto)
			{
				status = "b0_not_auto";
				error = "letter did not open at 11 [" + boot.director.DebugAuto + "]";
			}
			else if (b0Auto)
			{
				yield return Snap("story_bakery_letter.png");
				boot.ui.CloseLetter();
				yield return new WaitForSeconds(0.3f);
				if (boot.state.step != "B1_matches")
				{
					status = "b1_missing";
					error = "step=" + boot.state.step;
				}
			}
		}
		if (status == "passed")
		{
			BakeryItem bakeryItem = Find("basket");
			if (bakeryItem == null)
			{
				status = "bakery_data";
				error = "basket missing";
			}
			else
			{
				yield return WalkTo(bakeryItem.x, bakeryItem.z, "basket");
				if (walkFail != "")
				{
					status = "blocked_walk_" + walkFail;
					error = walkFail;
				}
				else
				{
					yield return null;
					float memoryDeadline5 = Time.realtimeSinceStartup + 3.5f;
					while (!boot.state.seenMemories.Contains("basket") && Time.realtimeSinceStartup < memoryDeadline5)
					{
						yield return null;
					}
					bool flag = boot.state.seenMemories.Contains("basket") && boot.ui.ToastVisible;
					bool flag2 = boot.service.TryInteract("basket") && boot.ui.ToastVisible;
					memoryOk = flag2 && !boot.ui.LetterOpen && !p.locked;
					if (!memoryOk)
					{
						status = "memory_failed";
						error = $"nearby={flag} replay={flag2} locked={p.locked}";
					}
				}
			}
		}
		if (status == "passed")
		{
			BakeryItem bakeryItem2 = Find("stall");
			yield return WalkTo(bakeryItem2.x, bakeryItem2.z, "stall");
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			else if (!boot.service.TryInteract("stall"))
			{
				status = "stall_failed";
				error = "stall not in range";
			}
			else
			{
				yield return new WaitForSeconds(0.25f);
				matchesOk = boot.state.step == "B2_wood" && boot.state.carrying == "matches";
				if (!matchesOk)
				{
					status = "matches_failed";
					error = "step=" + boot.state.step + " carry=" + boot.state.carrying;
				}
			}
		}
		if (status == "passed")
		{
			yield return WalkRoute(route, i5 + 1, i6);
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			else if (!boot.service.TryInteract("wood"))
			{
				status = "wood_failed";
				error = "wood not in range";
			}
			else
			{
				yield return new WaitForSeconds(0.25f);
				woodOk = boot.state.step == "B3_load" && boot.state.carrying == "wood";
				if (!woodOk)
				{
					status = "wood_failed";
					error = "step=" + boot.state.step + " carry=" + boot.state.carrying;
				}
			}
		}
		if (status == "passed")
		{
			yield return WalkRoute(route, i6 + 1, i7);
			if (walkFail == "" && !boot.ui.LetterOpen)
			{
				yield return WalkRouteBack(route, i7, i6 + 1);
			}
			BakeryItem bakeryItem3 = Find("stove");
			yield return WalkTo(bakeryItem3.x, bakeryItem3.z, "stove");
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			else if (!boot.service.TryInteract("stove"))
			{
				status = "stove_failed";
				error = "stove not in range";
			}
			else
			{
				yield return new WaitForSeconds(0.25f);
				loadOk = boot.state.step == "B4_light" && boot.state.carrying != "wood";
				if (loadOk && boot.service.TryInteract("stove"))
				{
					yield return new WaitForSeconds(0.4f);
					litOk = boot.state.flags.Contains("oven_lit");
					stampOk = boot.state.stamps.Contains("bakery");
					fx = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
					smokeOk = fx != null && fx.Lit && fx.HasSmoke;
					yield return new WaitForSeconds(4.1f);
					waterOk = boot.water != null && Mathf.Abs(boot.water.Y - 0.45f) <= 0.06f;
					yield return new WaitForSeconds(2.4f);
				}
				else if (loadOk)
				{
					status = "stove_failed";
					error = "second E did nothing";
				}
				else
				{
					status = "load_failed";
					error = "step=" + boot.state.step + " carry=" + boot.state.carrying;
				}
			}
			if (status == "passed" && fx != null)
			{
				Vector3 from = fx.transform.TransformPoint(new Vector3(0.7f, 1.6f, -3.7f));
				yield return SnapFrom("story_oven.png", from, fx.FireMouth + Vector3.up * 0.67f, delegate(Camera cam)
				{
					fireVisibleOven = fx.FireVisibleFrom(cam);
					smokeVisibleOven = fx.SmokeVisible;
				});
			}
		}
		if (status == "passed" && !(litOk && stampOk && waterOk && smokeOk && fireVisibleOven))
		{
			status = "fire_failed";
			error = $"lit={litOk} stamp={stampOk} water={waterOk} smoke={smokeOk} fire_visible={fireVisibleOven}";
		}
		float bakerySecs = Time.realtimeSinceStartup - bakeryAt;
		float schoolAt = Time.realtimeSinceStartup;
		bool s0Auto = false;
		bool slipperOk = false;
		bool ballOk = false;
		bool bowlOk = false;
		bool schoolRead = false;
		bool stamp2 = false;
		bool waterL2 = false;
		bool ballInBowl = false;
		bool dogPeek = false;
		bool crouched = false;
		int linesShown = 0;
		int linesTotal = 0;
		string exprTrace = "";
		if (status == "passed")
		{
			yield return WalkRoute(route, i6 + 1, i7);
			if (walkFail == "")
			{
				yield return WalkRoute(route, i7 + 1, i8);
			}
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			for (int j = 0; j < 120; j++)
			{
				if (!(status == "passed"))
				{
					break;
				}
				if (boot.ui.LetterOpen)
				{
					s0Auto = true;
					break;
				}
				yield return null;
			}
			if (status == "passed" && !s0Auto)
			{
				status = "s0_not_auto";
				error = "letter did not open at 20 [" + boot.director.DebugAuto + "]";
			}
			else if (s0Auto)
			{
				yield return Snap("story_school_letter.png");
				if (boot.state.step != "S0_arrive" || boot.state.flags.Contains("ball_in_bowl"))
				{
					status = "s0_wrong";
					error = "step=" + boot.state.step;
				}
				boot.ui.CloseLetter();
				yield return new WaitForSeconds(0.3f);
				if (status == "passed" && boot.state.step != "S1_slipper")
				{
					status = "s1_missing";
					error = "step=" + boot.state.step;
				}
			}
		}
		if (status == "passed")
		{
			yield return SnapFrom("story_pawprints.png", new Vector3(11.8f, 6.1f, 6.9f), new Vector3(14.3f, 3.3f, 0.6f));
		}
		if (status == "passed")
		{
			SchoolItem schoolItem = SchoolFind("slipper");
			if (schoolItem == null)
			{
				status = "school_data";
				error = "slipper missing";
			}
			else
			{
				yield return WalkTo(schoolItem.x, schoolItem.z, "slipper");
				if (walkFail != "")
				{
					status = "blocked_walk_" + walkFail;
					error = walkFail;
				}
				else if (!boot.service.TryInteract("slipper"))
				{
					status = "slipper_failed";
					error = "slipper not in range";
				}
				else
				{
					yield return new WaitForSeconds(0.25f);
					slipperOk = boot.state.step == "S2_ball";
					if (!slipperOk)
					{
						status = "slipper_failed";
						error = "step=" + boot.state.step;
					}
				}
			}
		}
		if (status == "passed")
		{
			yield return WalkRoute(route, i8, i9);
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			else
			{
				SchoolItem schoolItem2 = SchoolFind("ball");
				if (schoolItem2 == null)
				{
					status = "school_data";
					error = "ball missing";
				}
				else
				{
					yield return WalkTo(schoolItem2.x, schoolItem2.z, "ball");
					if (walkFail != "")
					{
						status = "blocked_walk_" + walkFail;
						error = walkFail;
					}
					else
					{
						if (status == "passed")
						{
							yield return SnapFrom("story_playground.png", new Vector3(13.8f, 5.8f, -8.6f), new Vector3(9.8f, 3.4f, -4.8f));
						}
						if (!boot.service.TryInteract("ball"))
						{
							status = "ball_failed";
							error = "ball not in range";
						}
						else
						{
							yield return new WaitForSeconds(0.25f);
							ballOk = boot.state.step == "S3_bowl" && boot.state.carrying == "ball";
							if (!ballOk)
							{
								status = "ball_failed";
								error = "step=" + boot.state.step + " carry=" + boot.state.carrying;
							}
						}
					}
				}
			}
		}
		if (status == "passed")
		{
			yield return WalkRouteBack(route, i9, i8);
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			else
			{
				SchoolItem schoolItem3 = SchoolFind("bowl");
				if (schoolItem3 == null)
				{
					status = "school_data";
					error = "bowl missing";
				}
				else
				{
					yield return WalkTo(schoolItem3.x, schoolItem3.z, "bowl");
					if (walkFail != "")
					{
						status = "blocked_walk_" + walkFail;
						error = walkFail;
					}
					else if (!boot.service.TryInteract("bowl"))
					{
						status = "bowl_failed";
						error = "bowl not in range";
					}
					else
					{
						yield return new WaitForSeconds(0.25f);
						bowlOk = boot.state.step == "S4_read" && boot.state.carrying == "" && boot.state.flags.Contains("ball_in_bowl");
						SchoolBall schoolBall = UnityEngine.Object.FindObjectOfType<SchoolBall>();
						ballInBowl = schoolBall != null && schoolBall.InBowl;
						dogPeek = dog != null && dog.Visible && !dog.Following;
						yield return new WaitForSecondsRealtime(2.8f);
						crouched = p.IsStoryCrouched;
						if (status == "passed")
						{
							yield return Snap("story_school_read.png");
						}
					}
				}
			}
		}
		if (status == "passed" && !(bowlOk && ballInBowl && dogPeek && crouched))
		{
			status = "school_failed";
			error = $"bowl={bowlOk} ball={ballInBowl} peek={dogPeek} crouched={crouched}";
		}
		if (status == "passed")
		{
			float memoryDeadline5 = Time.realtimeSinceStartup;
			while (!boot.state.flags.Contains("school_read") && Time.realtimeSinceStartup - memoryDeadline5 < 22f)
			{
				yield return null;
			}
			schoolRead = boot.state.flags.Contains("school_read");
			stamp2 = boot.state.stamps.Contains("school");
			linesShown = boot.director.SchoolLinesShown;
			linesTotal = boot.director.SchoolLinesTotal;
			exprTrace = boot.director.SchoolExprTrace;
			yield return new WaitForSecondsRealtime(4.1f);
			waterL2 = boot.water != null && Mathf.Abs(boot.water.Y - 1.15f) <= 0.06f;
			if (status == "passed" && !(schoolRead && stamp2 && waterL2))
			{
				status = "s4_failed";
				error = $"read={schoolRead} stamp={stamp2} water={waterL2}";
			}
		}
		if (status == "passed")
		{
			yield return SnapDog("story_dog.png");
		}
		float dogEnd = 999f;
		bool dogBehind = false;
		bool flag3 = false;
		bool dogFollowed = false;
		float dogMaxCheck = 0f;
		if (dog != null && dog.Bound && dog.Following && p != null)
		{
			Vector3 position2 = dog.transform.position;
			Vector3 position3 = p.transform.position;
			dogEnd = new Vector2(position2.x - position3.x, position2.z - position3.z).magnitude;
			dogBehind = Vector3.Dot(rhs: new Vector3(Mathf.Sin(p.yaw), 0f, Mathf.Cos(p.yaw)), lhs: new Vector3(position2.x - position3.x, 0f, position2.z - position3.z)) < 0.05f;
			dogMaxCheck = dogMax;
			flag3 = dog.Visible && dogMax <= 4f && dogMinClear >= -0.05f && dogEnd <= 3f && dogBehind;
			dogFollowed = dog.Visible;
		}
		if (status == "passed" && !flag3)
		{
			status = "dog_failed";
			error = $"follow={dog != null && dog.Following} max={dogMax:F2} end={dogEnd:F2} clear={dogMinClear:F2} behind={dogBehind}";
		}
		float schoolSecs = Time.realtimeSinceStartup - schoolAt;
		float lightAt = Time.realtimeSinceStartup;
		bool l0Auto = false;
		bool oilOk = false;
		bool climbOk = false;
		bool oilLetter = false;
		bool beamOk = false;
		bool dimOk = false;
		bool beaconOk = false;
		bool replyOk = false;
		bool stamp3 = false;
		bool waterL3 = false;
		int replyCount = 0;
		if (status == "passed")
		{
			yield return WalkTo(route[i8].x, route[i8].z, "20_back");
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			else
			{
				yield return WalkRoute(route, i8, i11);
			}
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			l0Auto = boot.state.step == "L1_shed";
			if (status == "passed" && !l0Auto)
			{
				status = "l0_failed";
				error = "step=" + boot.state.step + " [" + boot.director.DebugAuto + "]";
			}
			if (status == "passed")
			{
				yield return SnapFrom("story_lighthouse_crates.png", new Vector3(15.5f, 5.8f, -8.9f), new Vector3(15.5f, 1.3f, -6.5f));
			}
		}
		if (status == "passed")
		{
			if (!boot.service.TryInteract("oil"))
			{
				status = "oil_failed";
				error = "oil not in range";
			}
			else
			{
				yield return new WaitForSeconds(0.25f);
				oilOk = boot.state.step == "L2_door" && boot.state.carrying == "oil";
				if (!oilOk)
				{
					status = "oil_failed";
					error = "step=" + boot.state.step + " carry=" + boot.state.carrying;
				}
			}
		}
		if (status == "passed")
		{
			yield return WalkRouteBack(route, i11 + 1, i10);
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			else
			{
				yield return WalkRoute(route, i22b + 1, i12);
			}
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			if (status == "passed")
			{
				yield return SnapFrom("story_lighthouse_door.png", new Vector3(19.6f, 8f, -1.4f), new Vector3(21.5f, 5.9f, 1.9f));
			}
		}
		if (status == "passed")
		{
			if (!boot.service.TryInteract("lighthouse_door"))
			{
				status = "door_failed";
				error = "door not in range";
			}
			else
			{
				float memoryDeadline5 = Time.realtimeSinceStartup;
				while (boot.state.step != "L3_oil" && Time.realtimeSinceStartup - memoryDeadline5 < 10f)
				{
					yield return null;
				}
				while (boot.director.Climbing && Time.realtimeSinceStartup - memoryDeadline5 < 12f)
				{
					yield return null;
				}
				yield return new WaitForSecondsRealtime(0.2f);
				float y = LighthouseData.Point(LighthouseData.Load().deck).y;
				climbOk = boot.state.step == "L3_oil" && boot.player.transform.position.y > y - 0.5f;
				if (!climbOk)
				{
					status = "climb_failed";
					error = $"step={boot.state.step} y={boot.player.transform.position.y:F2}";
				}
			}
		}
		if (status == "passed")
		{
			if (!boot.service.TryInteract("lamp_head"))
			{
				status = "oilletter_failed";
				error = "lamp not in range";
			}
			else
			{
				for (int j = 0; j < 90; j++)
				{
					if (boot.ui.LetterOpen)
					{
						break;
					}
					yield return null;
				}
				oilLetter = boot.state.flags.Contains("lamp_oiled") && boot.ui.LetterOpen;
				if (!oilLetter)
				{
					status = "oilletter_failed";
					error = "letter not open";
				}
				else
				{
					yield return Snap("story_lighthouse_letter.png");
					boot.ui.CloseLetter();
					yield return new WaitForSeconds(0.3f);
					if (status == "passed" && boot.state.step != "L4_light")
					{
						status = "l4_missing";
						error = "step=" + boot.state.step;
					}
				}
			}
		}
		if (status == "passed")
		{
			if (!boot.service.TryInteract("lamp_head"))
			{
				status = "light_failed";
				error = "lamp not in range";
			}
			else
			{
				yield return new WaitForSecondsRealtime(10f);
				beamOk = boot.state.flags.Contains("beam_lit");
				SkyDimmer skyDimmer = UnityEngine.Object.FindObjectOfType<SkyDimmer>();
				BeaconLight beaconLight = UnityEngine.Object.FindObjectOfType<BeaconLight>();
				dimOk = skyDimmer != null && skyDimmer.Lit && skyDimmer.Progress > 0.3f;
				beaconOk = beaconLight != null && beaconLight.On;
				if (!(beamOk && dimOk && beaconOk))
				{
					status = "light_failed";
					error = $"beam={beamOk} dim={dimOk} beacon={beaconOk}";
				}
				else
				{
					yield return SnapFrom("story_lighthouse_beam.png", new Vector3(4f, 14.5f, 10f), new Vector3(21.58f, 12f, 2.7f));
				}
			}
		}
		if (status == "passed")
		{
			FlashlightReply reply = UnityEngine.Object.FindObjectOfType<FlashlightReply>();
			float memoryDeadline5 = Time.realtimeSinceStartup;
			while (boot.state.step != "L5_reply" && Time.realtimeSinceStartup - memoryDeadline5 < 34f)
			{
				yield return null;
			}
			replyOk = boot.state.step == "L5_reply" && reply != null && reply.Done && reply.LitCount == 40;
			replyCount = ((reply != null) ? reply.LitCount : 0);
			stamp3 = boot.state.stamps.Contains("lighthouse");
			if (!(replyOk && stamp3))
			{
				status = "reply_failed";
				error = $"step={boot.state.step} lit={replyCount} stamp={stamp3}";
			}
			else
			{
				yield return new WaitForSecondsRealtime(4.1f);
				waterL3 = boot.water != null && Mathf.Abs(boot.water.Y - 1.6f) <= 0.06f;
				if (!waterL3)
				{
					status = "l3_failed";
					error = "water=" + ((boot.water != null) ? boot.water.Y.ToString("F2", CultureInfo.InvariantCulture) : "-");
				}
				else
				{
					yield return SnapFrom("story_lighthouse_reply.png", new Vector3(21.6f, 19.4f, 5.6f), new Vector3(-12f, 11.6f, 25f));
				}
			}
		}
		float lightSecs = Time.realtimeSinceStartup - lightAt;
		bool saveOk = false;
		if (status == "passed")
		{
			GameState gameState = new SaveStore().Load();
			saveOk = gameState != null && gameState.step == "L5_reply" && gameState.flags.Contains("school_read") && gameState.flags.Contains("lighthouse_done") && gameState.flags.Contains("beam_lit") && gameState.flags.Contains("lamp_oiled") && gameState.flags.Contains("ball_in_bowl") && gameState.flags.Contains("letters_taken") && gameState.stamps.Contains("bakery") && gameState.stamps.Contains("school") && gameState.stamps.Contains("lighthouse") && gameState.seenMemories.Contains("basket") && Mathf.Abs(gameState.waterLevel - 1.6f) <= 0.06f;
			if (!saveOk)
			{
				status = "save_failed";
				error = "reload mismatch";
			}
			new SaveStore().Clear();
		}
		float endingAt = Time.realtimeSinceStartup;
		bool downOk = false;
		bool e0Ok = false;
		bool e1Open = false;
		bool exprSurprised = false;
		bool exprHappy = false;
		bool lookBack = false;
		bool waterL4 = false;
		bool ended = false;
		bool cityShown = false;
		string subtitleText = "";
		float dogFinal = 999f;
		if (status == "passed")
		{
			if (!boot.service.TryInteract("lighthouse_down"))
			{
				status = "down_failed";
				error = "descent not in range";
			}
			else
			{
				float memoryDeadline5 = Time.realtimeSinceStartup;
				while (boot.director.Cutscene && Time.realtimeSinceStartup - memoryDeadline5 < 8f)
				{
					yield return null;
				}
				yield return new WaitForSecondsRealtime(0.3f);
				Vector3 position4 = boot.player.transform.position;
				downOk = position4.y < 6f;
				if (!downOk)
				{
					status = "down_failed";
					error = $"y={position4.y:F2} step={boot.state.step}";
				}
				else
				{
					yield return WalkRoute(route, i30b + 1, i4);
					if (walkFail != "")
					{
						status = "blocked_walk_" + walkFail;
						error = walkFail;
					}
				}
			}
		}
		if (status == "passed")
		{
			float memoryDeadline5 = Time.realtimeSinceStartup;
			while (boot.state.step != "E1_read" && Time.realtimeSinceStartup - memoryDeadline5 < 5f)
			{
				yield return null;
			}
			e0Ok = boot.state.step == "E1_read" && boot.state.flags.Contains("dawn_shown");
			if (!e0Ok)
			{
				status = "e0_failed";
				error = "step=" + boot.state.step + " [" + boot.director.DebugAuto + "]";
			}
			else
			{
				yield return new WaitForSecondsRealtime(3.6f);
				yield return SnapFrom("story_ending_desk.png", new Vector3(-17f, 5.4f, -8.6f), new Vector3(-17f, 3.4f, -5.6f));
			}
		}
		if (status == "passed")
		{
			Vector3 vector = EndingData.Point(EndingData.Load().desk);
			yield return WalkTo(vector.x, vector.z, "ending_desk");
			if (walkFail != "" || !boot.service.TryInteract("ending_desk"))
			{
				status = "e1_failed";
				error = "desk not in range " + walkFail;
			}
			else
			{
				for (int j = 0; j < 90; j++)
				{
					if (boot.ui.LetterOpen)
					{
						break;
					}
					yield return null;
				}
				e1Open = boot.ui.LetterOpen;
				exprSurprised = p.pose != null && p.pose.expr == ExprId.surprised;
				if (!(e1Open && exprSurprised))
				{
					status = "e1_failed";
					error = string.Format("open={0} expr={1}", e1Open, (p.pose != null) ? p.pose.expr.ToString() : "-");
				}
				else if (boot.state.stamps.Contains("ending"))
				{
					status = "ending_stamp_early";
					error = "EGGBOY has not finished reading";
				}
				else
				{
					yield return Snap("story_ending_letter.png");
					yield return new WaitForSecondsRealtime(4.6f);
					exprHappy = p.pose != null && p.pose.expr == ExprId.happy;
					boot.ui.CloseLetter();
					yield return new WaitForSeconds(0.3f);
					if (!exprHappy)
					{
						status = "e1_mood_failed";
						error = ((p.pose != null) ? p.pose.expr.ToString() : "-");
					}
					else if (boot.state.step != "E2_climb")
					{
						status = "e2_missing";
						error = "step=" + boot.state.step;
					}
					else
					{
						GameState gameState2 = new SaveStore().Load();
						if (gameState2 == null || !gameState2.stamps.Contains("ending"))
						{
							status = "ending_stamp_missing";
							error = "Closing the final story letter did not save its postmark";
						}
					}
				}
			}
		}
		if (status == "passed")
		{
			yield return WalkRoute(route, i4, i3);
			if (walkFail != "")
			{
				status = "blocked_walk_" + walkFail;
				error = walkFail;
			}
			float memoryDeadline5 = Time.realtimeSinceStartup;
			while (!boot.director.Cutscene && Time.realtimeSinceStartup - memoryDeadline5 < 4f)
			{
				yield return null;
			}
			lookBack = boot.director.Cutscene;
			yield return new WaitForSecondsRealtime(5f);
			waterL4 = boot.water != null && Mathf.Abs(boot.water.Y - WaterLevel.LevelYs[4]) <= 0.08f;
			if (lookBack)
			{
				yield return Snap("story_ending_lookback.png");
			}
			if (status == "passed" && !(lookBack && waterL4))
			{
				status = "e2_failed";
				error = $"lookback={lookBack} water={waterL4} dbg={boot.director.EndingDebug} step={boot.state.step} t={Time.realtimeSinceStartup - endingAt:F1}";
			}
		}
		if (status == "passed")
		{
			float memoryDeadline5 = Time.realtimeSinceStartup;
			while (!boot.director.Ended && Time.realtimeSinceStartup - memoryDeadline5 < 30f)
			{
				yield return null;
			}
			ended = boot.director.Ended;
			subtitleText = boot.ui.SubtitleText;
			bool subtitleOk = subtitleText == "所有的信，都送到了。";
			CityGlow cityGlow = UnityEngine.Object.FindObjectOfType<CityGlow>();
			cityShown = cityGlow != null && cityGlow.Shown;
			yield return new WaitForSecondsRealtime(1.2f);
			Vector3 vector2 = ((dog != null) ? dog.transform.position : Vector3.zero);
			Vector3 vector3 = ((p != null) ? p.transform.position : Vector3.zero);
			dogFinal = new Vector2(vector2.x - vector3.x, vector2.z - vector3.z).magnitude;
			if (status == "passed")
			{
				if (!(ended && subtitleOk && cityShown))
				{
					status = "e3_failed";
					error = $"ended={ended} subtitle={subtitleText} city={cityShown} dbg={boot.director.EndingDebug}";
				}
				else if (dog == null || !dog.Following || !dog.Visible || (dogFinal > 5f && (!DogFollower.StayNearSchool || !dog.InSchoolArea)))
				{
					status = "e3_dog_failed";
					error = $"dog={dogFinal:F2} school={dog != null && dog.InSchoolArea}";
				}
				else
				{
					yield return Snap("story_ending_city.png");
				}
			}
		}
		float num = Time.realtimeSinceStartup - endingAt;
		new SaveStore().Clear();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine("  \"status\": \"" + status + "\",");
		stringBuilder.AppendLine("  \"steps\": { \"P0_boot\": " + (introDone ? "true" : "false") + ", \"P1_mailbox\": " + (stepOk ? "true" : "false") + ", \"B0_arrive\": " + (b0Auto ? "true" : "false") + ", \"B1_matches\": " + (matchesOk ? "true" : "false") + ", \"B2_wood\": " + (woodOk ? "true" : "false") + ", \"B3_load\": " + (loadOk ? "true" : "false") + ", \"B4_light\": " + (litOk ? "true" : "false") + ", \"S0_arrive\": " + (s0Auto ? "true" : "false") + ", \"S1_slipper\": " + (slipperOk ? "true" : "false") + ", \"S2_ball\": " + (ballOk ? "true" : "false") + ", \"S3_bowl\": " + (bowlOk ? "true" : "false") + ", \"S4_read\": " + (schoolRead ? "true" : "false") + ", \"L0_cross\": " + (l0Auto ? "true" : "false") + ", \"L1_shed\": " + (oilOk ? "true" : "false") + ", \"L2_door\": " + (climbOk ? "true" : "false") + ", \"L3_oil\": " + (oilLetter ? "true" : "false") + ", \"L4_light\": " + (beamOk ? "true" : "false") + ", \"L5_reply\": " + (replyOk ? "true" : "false") + ", \"E0_back\": " + (e0Ok ? "true" : "false") + ", \"E1_read\": " + ((e1Open && exprHappy) ? "true" : "false") + ", \"E2_climb\": " + ((lookBack && waterL4) ? "true" : "false") + ", \"E3_end\": " + (ended ? "true" : "false") + " },");
		stringBuilder.AppendLine("  \"intro\": { \"ran\": " + (introRan ? "true" : "false") + ", \"seconds\": " + introSeconds.ToString("F1", CultureInfo.InvariantCulture) + ", \"start_expr\": \"" + exprStart + "\", \"end_expr\": \"" + exprEnd + "\", \"face_ok\": " + (faceOk ? "true" : "false") + ", \"shots\": [\"story_boot.png\", \"story_wake.png\"] },");
		stringBuilder.AppendLine("  \"pickup_seconds\": " + pickupSeconds.ToString("F1", CultureInfo.InvariantCulture) + ",");
		stringBuilder.AppendLine("  \"pickup_within_30s\": " + (within30 ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"bakery\": { \"seconds\": " + bakerySecs.ToString("F1", CultureInfo.InvariantCulture) + ", \"auto_letter\": " + (b0Auto ? "true" : "false") + ", \"memory\": " + (memoryOk ? "true" : "false") + ", \"matches\": " + (matchesOk ? "true" : "false") + ", \"wood\": " + (woodOk ? "true" : "false") + ", \"loaded\": " + (loadOk ? "true" : "false") + ", \"lit\": " + (litOk ? "true" : "false") + ", \"stamp\": " + (stampOk ? "true" : "false") + ", \"water_l1\": " + (waterOk ? "true" : "false") + ", \"smoke\": " + (smokeOk ? "true" : "false") + ", \"fire_particles\": " + ((fx != null) ? fx.FireCount : 0) + ", \"smoke_particles\": " + ((fx != null) ? fx.SmokeCount : 0) + ", \"fire_visible\": " + (fireVisibleOven ? "true" : "false") + ", \"smoke_visible\": " + (smokeVisibleOven ? "true" : "false") + ", \"shots\": [\"story_bakery_letter.png\", \"story_oven.png\"] },");
		stringBuilder.AppendLine("  \"school\": { \"seconds\": " + schoolSecs.ToString("F1", CultureInfo.InvariantCulture) + ", \"auto_letter\": " + (s0Auto ? "true" : "false") + ", \"slipper\": " + (slipperOk ? "true" : "false") + ", \"ball\": " + (ballOk ? "true" : "false") + ", \"bowl\": " + (bowlOk ? "true" : "false") + ", \"ball_in_bowl\": " + (ballInBowl ? "true" : "false") + ", \"peek\": " + (dogPeek ? "true" : "false") + ", \"read\": " + (schoolRead ? "true" : "false") + ", \"crouched\": " + (crouched ? "true" : "false") + ", \"line_by_line\": " + ((linesShown == linesTotal && linesTotal > 0) ? "true" : "false") + ", \"lines_shown\": " + linesShown + ", \"lines_total\": " + linesTotal + ", \"expr_trace\": \"" + exprTrace + "\", \"stamp\": " + (stamp2 ? "true" : "false") + ", \"water_l2\": " + (waterL2 ? "true" : "false") + ", \"shots\": [\"story_school_letter.png\", \"story_pawprints.png\", \"story_playground.png\", \"story_school_read.png\"] },");
		stringBuilder.AppendLine("  \"lighthouse\": { \"seconds\": " + lightSecs.ToString("F1", CultureInfo.InvariantCulture) + ", \"crates\": " + (l0Auto ? "true" : "false") + ", \"oil\": " + (oilOk ? "true" : "false") + ", \"climb\": " + (climbOk ? "true" : "false") + ", \"read\": " + (oilLetter ? "true" : "false") + ", \"beam\": " + (beamOk ? "true" : "false") + ", \"dim\": " + (dimOk ? "true" : "false") + ", \"beacon\": " + (beaconOk ? "true" : "false") + ", \"reply\": " + (replyOk ? "true" : "false") + ", \"reply_lights\": " + replyCount + ", \"stamp\": " + (stamp3 ? "true" : "false") + ", \"water_l3\": " + (waterL3 ? "true" : "false") + ", \"shots\": [\"story_lighthouse_crates.png\", \"story_lighthouse_door.png\", \"story_lighthouse_letter.png\", \"story_lighthouse_beam.png\", \"story_lighthouse_reply.png\"] },");
		stringBuilder.AppendLine("  \"ending\": { \"seconds\": " + num.ToString("F1", CultureInfo.InvariantCulture) + ", \"descent\": " + (downOk ? "true" : "false") + ", \"back_at_post\": " + (e0Ok ? "true" : "false") + ", \"letter\": " + (e1Open ? "true" : "false") + ", \"mood_surprised\": " + (exprSurprised ? "true" : "false") + ", \"mood_happy\": " + (exprHappy ? "true" : "false") + ", \"look_back\": " + (lookBack ? "true" : "false") + ", \"water_l4\": " + (waterL4 ? "true" : "false") + ", \"ended\": " + (ended ? "true" : "false") + ", \"city\": " + (cityShown ? "true" : "false") + ", \"subtitle\": \"" + subtitleText + "\", \"dog_end\": " + dogFinal.ToString("F2", CultureInfo.InvariantCulture) + ", \"shots\": [\"story_ending_desk.png\", \"story_ending_letter.png\", \"story_ending_lookback.png\", \"story_ending_city.png\"] },");
		stringBuilder.AppendLine("  \"save_reload\": " + (saveOk ? "true" : "false") + ",");
		DogVisual dogVisual = ((dog != null) ? dog.GetComponent<DogVisual>() : null);
		stringBuilder.AppendLine("  \"dog\": { \"followed\": " + (dogFollowed ? "true" : "false") + ", \"visible\": " + (dogFollowed ? "true" : "false") + ", \"left_at_tower\": " + ((dog != null && !dog.Visible) ? "true" : "false") + ", \"follow_max\": " + dogMaxCheck.ToString("F2", CultureInfo.InvariantCulture) + ", \"follow_max_all\": " + dogMax.ToString("F2", CultureInfo.InvariantCulture) + ", \"distance_end\": " + dogEnd.ToString("F2", CultureInfo.InvariantCulture) + ", \"behind\": " + (dogBehind ? "true" : "false") + ", \"ground_error_min\": " + dogMinClear.ToString("F2", CultureInfo.InvariantCulture) + ", \"wag_hz\": " + ((dog != null) ? dog.WagHz.ToString("F1", CultureInfo.InvariantCulture) : "0") + ", \"visual\": \"" + ((dogVisual != null) ? dogVisual.GetType().Name : "none") + "\", \"shots\": [\"story_dog.png\"] },");
		stringBuilder.AppendLine("  \"letter_open\": " + (opened ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"move_locked\": " + (lockedMove ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"no_repeat\": " + (noRepeat ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"avg_fps\": " + ((frames > 0) ? (sumFps / (float)frames).ToString("F1", CultureInfo.InvariantCulture) : "0") + ",");
		stringBuilder.AppendLine("  \"walk_recoveries\": " + recoveries + ",");
		stringBuilder.AppendLine("  \"walk_last\": \"" + walkLast + "\",");
		stringBuilder.AppendLine("  \"total_seconds\": " + (Time.realtimeSinceStartup - bootAt).ToString("F1", CultureInfo.InvariantCulture) + ",");
		stringBuilder.AppendLine("  \"errors\": \"" + error.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r")
			.Replace("\n", "\\n")
			.Replace("\t", "\\t") + "\"");
		stringBuilder.AppendLine("}");
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "story-audit.json"), stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Debug.Log("STORYAUDIT " + status + " intro=" + introSeconds.ToString("F1", CultureInfo.InvariantCulture) + " pickup=" + pickupSeconds.ToString("F1", CultureInfo.InvariantCulture) + " bakery=" + bakerySecs.ToString("F1", CultureInfo.InvariantCulture) + " lit=" + litOk + " save=" + saveOk);
		yield return new WaitForSecondsRealtime(0.5f);
		Application.Quit();
	}

	private BakeryItem Find(string id)
	{
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			if (bakeryItem.id == id)
			{
				return bakeryItem;
			}
		}
		return null;
	}

	private SchoolItem SchoolFind(string id)
	{
		SchoolItem[] items = SchoolData.Load().items;
		foreach (SchoolItem schoolItem in items)
		{
			if (schoolItem.id == id)
			{
				return schoolItem;
			}
		}
		return null;
	}

	private IEnumerator WalkRoute(Wp[] route, int from, int to)
	{
		if (to < 0)
		{
			walkFail = "route_index";
			yield break;
		}
		for (int i = Mathf.Max(0, from); i <= to; i++)
		{
			yield return WalkTo(route[i].x, route[i].z, route[i].id);
			if (walkFail != "" || boot.ui.LetterOpen)
			{
				break;
			}
		}
	}

	private IEnumerator WalkRouteBack(Wp[] route, int from, int to)
	{
		for (int i = from - 1; i >= to; i--)
		{
			yield return WalkTo(route[i].x, route[i].z, route[i].id + "r");
			if (walkFail != "" || boot.ui.LetterOpen)
			{
				break;
			}
		}
	}

	private IEnumerator WalkTo(float x, float z, string label)
	{
		float t0 = Time.realtimeSinceStartup;
		float best = float.MaxValue;
		float bestAt = t0;
		while (true)
		{
			if (boot.ui.LetterOpen || boot.director.Cutscene)
			{
				yield break;
			}
			Vector3 position = boot.player.transform.position;
			float num = x - position.x;
			float num2 = z - position.z;
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			if (num3 < 0.8f)
			{
				yield break;
			}
			if (num3 < best - 0.01f)
			{
				best = num3;
				bestAt = Time.realtimeSinceStartup;
			}
			else if (num3 < 1.55f && Time.realtimeSinceStartup - bestAt > 0.6f)
			{
				yield break;
			}
			if (Time.realtimeSinceStartup - t0 > 30f)
			{
				break;
			}
			boot.player.Move(0.05f, num / Mathf.Max(0.01f, num3), num2 / Mathf.Max(0.01f, num3), run: false, jumpPressed: false);
			yield return null;
		}
		Vector3 position2 = boot.player.transform.position;
		string text = label + "@" + position2.x.ToString("F2") + "," + position2.y.ToString("F2") + "," + position2.z.ToString("F2") + "→" + x.ToString("F2") + "," + z.ToString("F2") + " w=" + boot.world.Walkable(x, z);
		if (recoveries < 4)
		{
			recoveries++;
			float num4 = boot.world.GroundAt(x, z);
			boot.player.transform.position = new Vector3(x, num4 + 0.05f, z);
			boot.player.vel = Vector3.zero;
			boot.player.grounded = true;
			walkLast = text;
			yield return null;
		}
		else
		{
			walkFail = text;
		}
	}

	private IEnumerator SnapDog(string file)
	{
		EggboyPlayer player = boot.player;
		Vector3 vector = new Vector3(Mathf.Sin(player.yaw), 0f, Mathf.Cos(player.yaw));
		Vector3 from = player.transform.position - vector * 4.4f + Vector3.up * 1.8f;
		Vector3 look = player.transform.position + Vector3.up * 0.55f;
		yield return SnapFrom(file, from, look);
	}

	private IEnumerator Snap(string file)
	{
		string text = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(text);
		ScreenCapture.CaptureScreenshot(Path.Combine(text, file), 1);
		yield return new WaitForSecondsRealtime(0.6f);
	}

	private IEnumerator SnapFrom(string file, Vector3 from, Vector3 look, Action<Camera> onFrame = null)
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
		cam.transform.position = from;
		cam.transform.rotation = Quaternion.LookRotation(look - from, Vector3.up);
		cam.fieldOfView = 42f;
		yield return null;
		yield return null;
		onFrame?.Invoke(cam);
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, file), 1);
		yield return new WaitForSecondsRealtime(0.6f);
		cam.transform.SetPositionAndRotation(p0, r0);
		cam.fieldOfView = f0;
		if (chase != null)
		{
			chase.enabled = chaseOn;
		}
	}

	private void Update()
	{
		if (Time.deltaTime > 0f)
		{
			sumFps += 1f / Time.deltaTime;
			frames++;
		}
		if (dog != null && dog.Bound && dog.Following && boot != null && boot.player != null && boot.world != null)
		{
			Vector3 position = dog.transform.position;
			Vector3 position2 = boot.player.transform.position;
			float magnitude = new Vector2(position.x - position2.x, position.z - position2.z).magnitude;
			if (Time.timeScale > 0f)
			{
				dogMax = Mathf.Max(dogMax, magnitude);
			}
			dogMinClear = Mathf.Min(dogMinClear, position.y - boot.world.GroundAt(position.x, position.z, position.y));
		}
	}
}
