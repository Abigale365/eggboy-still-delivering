using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class StoryDirector : MonoBehaviour
{
	private BakeryEffects ovenFx;
	private float nextBellowsAllowed;

	private string droppedPending = "";

	private bool? woodPileShown;

	private bool? oilShown;

	private EggboyPlayer player;

	private EggboyUI ui;

	private SaveStore store;

	private ControlGate gate;

	private WaterLevel water;

	private PrototypeGame game;

	private DogFollower dog;

	private SchoolBall ball;

	private object letterLock;

	private bool ready;

	private bool readingSchool;

	private string arrivedStep = "";

	private GameObject carriedVisual;

	private readonly BuiltinModelProvider modelProvider = new BuiltinModelProvider();

	private Dictionary<string, Vector3> routePoints;

	private IslandWorld island;

	private LetterData[] letters = new LetterData[0];

	private StoryStep[] steps = new StoryStep[0];

	public string DebugAuto = "";

	private bool signalActive;

	private float signalAlignment;

	private int signalFlashes;

	private BeaconLight activeBeacon;

	private FlashlightReply activeReply;

	private RectTransform signalCard;

	private Text signalAlignLabel;

	private Text signalFlashLabel;

	private Text signalReplyLabel;

	private const float FadeSeconds = 0.55f;

	private bool climbing;

	private bool cutscene;

	private object endLock;

	private Image fade;

	public string EndingDebug = "";

	public bool IsAuditMode
	{
		get
		{
			if (!(ui == null))
			{
				return !ui.inputClose;
			}
			return true;
		}
	}

	private BakeryEffects OvenFx
	{
		get
		{
			if (!(ovenFx != null))
			{
				return ovenFx = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
			}
			return ovenFx;
		}
	}

	public int WetFizzleCount { get; private set; }

	public bool WoodPileVisible => woodPileShown ?? true;

	public bool OilCanVisible => oilShown ?? true;

	public GameObject CarriedVisual => carriedVisual;

	public string LastDroppedItem { get; private set; } = "";


	public Vector3 LastDropPosition { get; private set; }

	public GameState State { get; private set; }

	public int SchoolLinesShown { get; private set; }

	public int SchoolLinesTotal { get; private set; }

	public string SchoolExprTrace { get; private set; } = "";


	private PhysicalPickupController Pickup => PhysicalPickupController.Instance;

	public bool Climbing => climbing;

	public bool Cutscene => cutscene;

	public bool Ended { get; private set; }

	public void TakeMatchesAt(Vector3 pos)
	{
		if (ready && !State.flags.Contains("matches_taken") && !BeginPhysical(new PhysicalPickupController.Job
		{
			clip = "pickup_matches",
			target = () => pos + Vector3.up * 0.8f,
			diameter = 0.08f,
			standDistance = 0.45f,
			contactTime = 0.35f,
			lockSeconds = 0.75f,
			tolerance = 0.1f,
			onContact = delegate
			{
				TakeMatchesCommit(animate: false);
			}
		}))
		{
			TakeMatches();
		}
	}

	public void TakeMatches()
	{
		TakeMatchesCommit(animate: true);
	}

	private void TakeMatchesCommit(bool animate)
	{
		if (ready && !State.flags.Contains("matches_taken"))
		{
			State.flags.Add("matches_taken");
			if (State.carrying != "wood")
			{
				State.carrying = "matches";
			}
			else if (!State.flags.Contains("matches_packed"))
			{
				State.flags.Add("matches_packed");
			}
			store.Save(State);
			if (animate)
			{
				PlayAction("pickup_matches", ExprId.happy, 0.75f, 0.35f);
			}
			else if (game != null)
			{
				game.SetExpression(ExprId.happy);
			}
			ui.SetCarried(CarryLabel());
			if (State.flags.Contains("wood_loaded"))
			{
				AdvanceTo("B4_light");
			}
			else if (State.carrying == "wood")
			{
				AdvanceTo("B3_load");
			}
			else
			{
				AdvanceTo("B2_wood");
			}
		}
	}

	public void TakeWoodAt(Vector3 pos)
	{
		if (ready && (!State.flags.Contains("wood_loaded") || State.flags.Contains("wood_wet")) && !BeginPhysical(new PhysicalPickupController.Job
		{
			clip = "pickup_wood",
			target = () => pos + Vector3.up * 0.18f,
			diameter = 0.3f,
			standDistance = 0.45f,
			contactTime = 0.44f,
			lockSeconds = 1f,
			onContact = delegate
			{
				TakeWoodCommit(animate: false);
			}
		}))
		{
			if (player != null)
			{
				player.FaceTowards(pos);
			}
			TakeWood();
		}
	}

	public void TakeWood()
	{
		TakeWoodCommit(animate: true);
	}

	private void TakeWoodCommit(bool animate)
	{
		if (!ready || (State.flags.Contains("wood_loaded") && !State.flags.Contains("wood_wet")))
		{
			return;
		}
		if (State.flags.Contains("matches_taken") && !State.flags.Contains("matches_packed"))
		{
			State.flags.Add("matches_packed");
		}
		State.carrying = "wood";
		store.Save(State);
		ui.SetCarried(CarryLabel());
		if (animate)
		{
			PlayAction("pickup_wood", ExprId.happy, 1f, 0.44f, delegate
			{
				ShowCarriedModel("wood");
			});
		}
		else
		{
			ShowCarriedModel("wood");
		}
		AdvanceTo("B3_load");
	}

	public void RingBellAt(Vector3 pos, string label, string text)
	{
		if (!ready)
		{
			return;
		}
		BellSwing swing = GameObject.Find("Bakery_bell")?.GetComponent<BellSwing>();
		Vector3 touch = ((swing != null) ? swing.TouchPoint : (pos + Vector3.up * 0.76f));
		if (!BeginPhysical(new PhysicalPickupController.Job
		{
			clip = "inspect_memory",
			target = () => touch,
			diameter = 0.2f,
			standDistance = 0.4f,
			contactTime = 0.38f,
			lockSeconds = 0.85f,
			tolerance = 0.12f,
			onContact = delegate
			{
				if (swing != null)
				{
					swing.Ring();
				}
			},
			onDone = delegate
			{
				InspectMemory("bell", label, text);
			}
		}))
		{
			if (swing != null)
			{
				swing.Ring();
			}
			InspectMemory("bell", label, text);
		}
	}

	public void LoadWoodAt(Vector3 pos)
	{
		if (ready && !(State.carrying != "wood"))
		{
			Vector3 mouth = ((OvenFx != null) ? OvenFx.LoadPoint : (pos + Vector3.up * 0.45f));
			if (!BeginPhysical(new PhysicalPickupController.Job
			{
				clip = "load_wood",
				target = () => mouth,
				diameter = 0.3f,
				standDistance = 0.55f,
				contactTime = 0.5f,
				lockSeconds = 0.95f,
				onContact = delegate
				{
					LoadWoodCommit(animate: false);
				}
			}))
			{
				LoadWood();
			}
		}
	}

	public void LoadWood()
	{
		LoadWoodCommit(animate: true);
	}

	private void LoadWoodCommit(bool animate)
	{
		if (!ready || State.carrying != "wood")
		{
			return;
		}
		if (State.flags.Contains("carrying_wet_wood")) { State.flags.Remove("carrying_wet_wood"); if (!State.flags.Contains("wood_wet")) State.flags.Add("wood_wet"); } else { State.flags.Remove("wood_wet"); }
		if (!State.flags.Contains("wood_loaded"))
		{
			State.flags.Add("wood_loaded");
		}
		State.carrying = (State.flags.Contains("matches_taken") ? "matches" : "");
		store.Save(State);
		ui.SetCarried(CarryLabel());
		if (animate)
		{
			PlayAction("load_wood", ExprId.happy, 0.95f, 0.5f, delegate
			{
				ShowCarriedModel(null);
			});
		}
		else
		{
			ShowCarriedModel(null);
		}
		if (State.flags.Contains("matches_taken"))
		{
			AdvanceTo("B4_light");
		}
		else
		{
			AdvanceTo("B1_matches");
		}
	}

	public void LightOven(BakeryEffects fx)
	{
		if (!ready || !State.flags.Contains("wood_loaded") || !State.flags.Contains("matches_taken") || State.flags.Contains("oven_lit"))
		{
			return;
		}
		if (State.flags.Contains("wood_wet"))
		{
			WetWoodFizzle(fx);
			return;
		}
		WoodenMechanismPuzzleModal.OpenOrRun("bakery_lock", State, store, player, ui, gate, delegate
		{
			CompleteLightOven(fx);
		});
	}

	private void CompleteLightOven(BakeryEffects fx)
	{
		if (ready && !State.flags.Contains("oven_lit"))
		{
			State.flags.Add("oven_lit");
			TownVoiceover.Play("vo_stamp_bakery");
			if (State.carrying == "matches")
			{
				State.carrying = "";
			}
			CollectPostmark("bakery");
			store.Save(State);
			if (fx != null)
			{
				fx.Light();
			}
			if (water != null)
			{
				water.SetLevel("L1", 4f);
			}
			PlayAction("light_stove_done", ExprId.excited, 0.8f, 0.25f);
			ui.SetCarried(CarryLabel());
			ui.ShowToast("炉膛里的火苗刚刚燃起");
			AdvanceTo("S0_arrive");
		}
	}

	public bool LoadWetWood()
	{
		if (!ready || State.flags.Contains("oven_lit") || State.flags.Contains("wood_loaded"))
		{
			return false;
		}
		State.flags.Add("wood_loaded");
		State.flags.Add("wood_wet");
		store.Save(State);
		PlayAction("load_wood", ExprId.happy, 0.95f, 0.5f);
		if (State.flags.Contains("matches_taken"))
		{
			AdvanceTo("B4_light");
		}
		else
		{
			AdvanceTo("B1_matches");
		}
		return true;
	}

	public bool LoadDryWoodFromWord()
	{
		if (!ready || State.flags.Contains("oven_lit"))
		{
			return false;
		}
		if (State.flags.Contains("wood_wet"))
		{
			State.flags.Remove("wood_wet");
			store.Save(State);
			ApplyStepGoal();
			return true;
		}
		if (State.flags.Contains("wood_loaded"))
		{
			return false;
		}
		State.flags.Add("wood_loaded");
		store.Save(State);
		PlayAction("load_wood", ExprId.happy, 0.95f, 0.5f);
		if (State.flags.Contains("matches_taken"))
		{
			AdvanceTo("B4_light");
		}
		else
		{
			AdvanceTo("B1_matches");
		}
		return true;
	}

	private void WetWoodFizzle(BakeryEffects fx)
	{
		WetFizzleCount++;
		if (!State.flags.Contains("wet_fizzled"))
		{
			State.flags.Add("wet_fizzled");
			store.Save(State);
		}
		if (fx != null)
		{
			fx.WetPuff();
		}
		if (game != null)
		{
			game.SetExpression(ExprId.sad);
		}
		ShadowAndChimeSystem.PlayProceduralChime(185f, 0.35f);
	}

	public void BoostOvenBellows(BakeryEffects fx)
	{
		if (ready && !(fx == null))
		{
			if (!IsAuditMode && Time.unscaledTime < nextBellowsAllowed) return;
			nextBellowsAllowed = Time.unscaledTime + 1.1f;
			int num = fx.BoostBellows();
			TownVoiceover.Play("vo_stamp_bakery");
			TideTownAchievementSystem.Unlock("breath_fire");
			PlayAction((num >= 3) ? "light_stove_done" : "load_wood", (num >= 3) ? ExprId.excited : ExprId.happy, 0.8f);
			if (ui != null)
			{
				ui.ShowToast(num switch
				{
					2 => "呼——烤炉彻底热了起来，暖意漫出屋檐（2/3）", 
					1 => "呼——炉膛里的柴火噼啪作响，火苗蹿高了一截（1/3）", 
					_ => "呼——浓密的炊烟笔直升起，顺着海风一直飘向山那边！（3/3）", 
				}, 3.5f);
			}
		}
	}

	public bool ShowMemory(string id, string text, bool replay)
	{
		if (!ready || ui == null || gate == null || gate.Locked)
		{
			return false;
		}
		if (!State.seenMemories.Contains(id))
		{
			State.seenMemories.Add(id);
			store.Save(State);
		}
		else if (!replay)
		{
			return false;
		}
		ui.ShowToast(text, 3f);
		if (replay && !ui.LetterOpen) TownVoiceover.Play("vo_mem_" + id);
		return true;
	}

	public bool InspectMemory(string id, string label, string text, bool faceBack = false)
	{
		bool num = ShowMemory(id, text, replay: true);
		if (num)
		{
			PlayAction("inspect_memory", ExprId.curious, 0.65f, 0.4f);
			if (!IsAuditMode)
			{
				ObjectInspectModal.Open(id, label, text, State, store, player, ui, gate, faceBack);
			}
		}
		return num;
	}

	public void PickupDroppedItem(GameObject visual, bool keepVisual, Action take)
	{
		if (keepVisual && Pickup != null && Pickup.Active("pickup_ball"))
		{
			take?.Invoke();
			return;
		}
		PlayAction("pickup_drop", ExprId.happy, 0.85f, 0.42f, delegate
		{
			if (visual != null && !keepVisual)
			{
				UnityEngine.Object.Destroy(visual);
			}
			take?.Invoke();
		});
	}

	private void ShowCarriedModel(string item)
	{
		if (carriedVisual != null)
		{
			UnityEngine.Object.Destroy(carriedVisual);
		}
		carriedVisual = null;
		SyncCarryPose();
		SyncWorldProps();
		if (player == null || (item != "wood" && item != "oil"))
		{
			return;
		}
		modelProvider.Load(item, delegate(GameObject model)
		{
			if (State != null && !(State.carrying != item))
			{
				carriedVisual = UnityEngine.Object.Instantiate(model);
				carriedVisual.name = "Carried_" + item;
				carriedVisual.SetActive(value: true);
				Collider[] componentsInChildren = carriedVisual.GetComponentsInChildren<Collider>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					UnityEngine.Object.Destroy(componentsInChildren[i]);
				}
				Renderer[] componentsInChildren2 = carriedVisual.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < componentsInChildren2.Length; i++)
				{
					componentsInChildren2[i].enabled = true;
				}
				carriedVisual.transform.localScale = model.transform.lossyScale;
				if (item == "wood")
				{
					PlaceWoodInArms(carriedVisual);
				}
				else
				{
					carriedVisual.AddComponent<HandCarry>().Init(player);
				}
			}
		}, delegate(string message)
		{
			Debug.LogWarning(message);
		});
	}

	private void PlaceWoodInArms(GameObject v)
	{
		Transform transform = ((player.CarryAnchor != null) ? player.CarryAnchor : player.transform);
		v.transform.SetParent(transform, worldPositionStays: false);
		Vector3 vector = ((island != null && island.woodModel != null) ? island.woodModel.transform.lossyScale : Vector3.one);
		Vector3 lossyScale = transform.lossyScale;
		v.transform.localScale = new Vector3(vector.x / lossyScale.x, vector.y / lossyScale.y, vector.z / lossyScale.z);
		v.transform.localRotation = Quaternion.identity;
		Bounds bounds = HandCarry.LocalBounds(v, transform);
		if (bounds.size.z > bounds.size.x)
		{
			v.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
			bounds = HandCarry.LocalBounds(v, transform);
		}
		float num = ((PhysicalPickupController.Instance != null) ? PhysicalPickupController.Instance.Grip : null)?.ShellFrontAt(transform, -0.04000002f, Mathf.Max(0.12f, bounds.size.x * 0.4f), bounds.size.y * 0.5f) ?? 0.02000001f;
		Vector3 vector2 = new Vector3(0f, -0.04000002f, num + 0.02f + bounds.size.z * 0.5f);
		v.transform.localPosition += vector2 - bounds.center;
	}

	private void SyncWorldProps()
	{
		if (State != null)
		{
			bool flag = State.carrying != "wood" && droppedPending != "wood" && (!State.flags.Contains("wood_loaded") || State.flags.Contains("wood_wet"));
			bool flag2 = !State.flags.Contains("oil_taken") && droppedPending != "oil";
			if (woodPileShown != flag)
			{
				woodPileShown = flag;
				SetRenderers("Bakery_wood", flag);
			}
			if (oilShown != flag2)
			{
				oilShown = flag2;
				SetRenderers("LH_oil", flag2);
			}
			if (OvenFx != null)
			{
				OvenFx.SetLoadedWood(State.flags.Contains("wood_loaded"), State.flags.Contains("wood_wet"));
			}
		}
	}

	private static void SetRenderers(string rootName, bool on)
	{
		GameObject gameObject = GameObject.Find(rootName);
		if (!(gameObject == null))
		{
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = on;
			}
		}
	}

	public void DebugSyncProps()
	{
		SyncWorldProps();
	}

	public void DropCarriedNear(Vector3 at)
	{
		if (!ready || State == null)
		{
			return;
		}
		string carrying = State.carrying;
		if (carrying != "wood" && carrying != "oil" && carrying != "ball")
		{
			return;
		}
		Vector3 vector = at;
		if (island != null)
		{
			vector.y = Mathf.Max(island.GroundAt(at.x, at.z, at.y + 1f), island.WaterY);
		}
		Vector3 vector2 = SafeDropPoint(at);
		GameObject gameObject = null;
		Action action = null;
		if (carrying == "wood")
		{
			State.carrying = (State.flags.Contains("matches_taken") ? "matches" : "");
			ShowCarriedModel(null);
			AdvanceTo(State.flags.Contains("matches_taken") ? "B2_wood" : "B1_matches");
			if (island != null && island.woodModel != null)
			{
				gameObject = UnityEngine.Object.Instantiate(island.woodModel);
			}
			droppedPending = "wood";
			action = delegate
			{
				droppedPending = "";
				TakeWood();
			};
		}
		else if (carrying == "oil")
		{
			State.flags.Remove("oil_taken");
			State.carrying = "";
			ShowCarriedModel(null);
			AdvanceTo("L1_shed");
			if (island != null && island.oilModel != null)
			{
				gameObject = UnityEngine.Object.Instantiate(island.oilModel);
			}
			droppedPending = "oil";
			action = delegate
			{
				droppedPending = "";
				TakeOil();
			};
		}
		else
		{
			State.flags.Remove("ball_taken");
			State.carrying = "";
			SyncCarryPose();
			AdvanceTo("S2_ball");
			if (Pickup != null)
			{
				Pickup.ReleaseCarryBall(ball);
			}
			if (ball != null)
			{
				ball.transform.SetParent(null, worldPositionStays: true);
				gameObject = ball.gameObject;
			}
			action = TakeBallAt;
		}
		store.Save(State);
		ui.SetCarried(CarryLabel());
		LastDroppedItem = carrying;
		LastDropPosition = vector2;
		if (gameObject != null)
		{
			gameObject.SetActive(value: true);
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = true;
			}
			gameObject.transform.position = vector;
			StartCoroutine(DriftToRest(gameObject.transform, vector, (carrying == "ball") ? (vector2 + Vector3.up * 0.14f * 0.5f) : vector2));
		}
		InteractionService interactionService = UnityEngine.Object.FindObjectOfType<InteractionService>();
		if (interactionService != null)
		{
			interactionService.Register(new DroppedItemPickup(carrying, vector2, this, gameObject, carrying == "ball", action));
		}
	}

	private Vector3 SafeDropPoint(Vector3 at)
	{
		if (island == null)
		{
			return at;
		}
		if (island.Walkable(at.x, at.z))
		{
			at.y = island.GroundAt(at.x, at.z, at.y + 1f);
			return at;
		}
		for (float num = 0.5f; num <= 5f; num += 0.5f)
		{
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector3 result = at + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
				if (island.Walkable(result.x, result.z))
				{
					result.y = island.GroundAt(result.x, result.z, at.y + 1f);
					return result;
				}
			}
		}
		Vector3 result2 = ((player != null) ? player.transform.position : at);
		result2.y = island.GroundAt(result2.x, result2.z, result2.y + 1f);
		return result2;
	}

	private IEnumerator DriftToRest(Transform t, Vector3 from, Vector3 to)
	{
		float dur = ((Vector3.Distance(from, to) > 0.3f) ? 1.4f : 0.35f);
		for (float e = 0f; e < dur; e += Time.deltaTime)
		{
			if (!(t != null))
			{
				break;
			}
			float num = Mathf.SmoothStep(0f, 1f, e / dur);
			t.position = Vector3.Lerp(from, to, num) + Vector3.up * Mathf.Sin(num * MathF.PI) * 0.25f;
			t.Rotate(Vector3.up, 200f * Time.deltaTime, Space.World);
			yield return null;
		}
		if (t != null)
		{
			t.position = to;
		}
	}

	public bool OnGalePenaltyDropItem(out string penaltyMessage)
	{
		penaltyMessage = "【失衡惩罚】你没能稳住重心，被强海风吹退回了安全处！";
		if (!ready || State == null)
		{
			return false;
		}
		if (game != null)
		{
			game.SetExpression(ExprId.sad);
		}
		if (State.carrying == "wood")
		{
			State.carrying = (State.flags.Contains("matches_taken") ? "matches" : "");
			ShowCarriedModel(null);
			if (ui != null)
			{
				ui.SetCarried(CarryLabel());
			}
			AdvanceTo("B2_wood");
			penaltyMessage = "【失衡惩罚】狂风把你吹退！怀里的柴火捆被吹落打湿，必须回后院柴堆重新抱取！";
			return true;
		}
		if (State.carrying == "matches")
		{
			State.flags.Remove("matches_taken");
			State.flags.Remove("matches_packed");
			State.carrying = "";
			if (ui != null)
			{
				ui.SetCarried("");
			}
			AdvanceTo("B1_matches");
			penaltyMessage = "【失衡惩罚】狂风把你吹退！火柴盒被海雾打湿失效，必须回面包店门前摊位重拿火柴！";
			return true;
		}
		if (State.carrying == "ball")
		{
			State.flags.Remove("ball_taken");
			State.carrying = "";
			if (ball != null)
			{
				ball.ReturnToSwing();
			}
			if (ui != null)
			{
				ui.SetCarried("");
			}
			AdvanceTo("S2_ball");
			penaltyMessage = "【失衡惩罚】狂风把你吹退！手里的红球滚回了操场秋千下，必须回去重新捡球！";
			return true;
		}
		if (State.carrying == "oil")
		{
			State.flags.Remove("oil_taken");
			State.carrying = "";
			ShowCarriedModel(null);
			if (ui != null)
			{
				ui.SetCarried("");
			}
			AdvanceTo("L1_shed");
			penaltyMessage = "【失衡惩罚】海浪把你冲退！煤油壶被打翻滑回船棚，必须回船棚重新拿取油壶！";
			return true;
		}
		return false;
	}

	public void OnMechanismJamPenalty(string puzzleId)
	{
		if (!ready || State == null)
		{
			return;
		}
		if (game != null)
		{
			game.SetExpression(ExprId.sad);
		}
		if (player != null && island != null)
		{
			float yaw = player.yaw;
			Vector3 position = player.transform.position - new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 1.15f;
			if (island.Walkable(position.x, position.z))
			{
				position.y = island.GroundAt(position.x, position.z, position.y);
				player.transform.position = position;
			}
		}
		if (puzzleId == "bakery_lock")
		{
			State.flags.Remove("matches_taken");
			State.flags.Remove("matches_packed");
			if (State.carrying == "matches")
			{
				State.carrying = "";
			}
			if (ui != null)
			{
				ui.SetCarried(CarryLabel());
			}
			AdvanceTo("B1_matches");
			if (ui != null)
			{
				ui.ShowToast("风门猛地一震，一股冷风灌出来——火柴灭了。", 3.2f);
			}
		}
		else if (puzzleId == "school_lock")
		{
			State.flags.Remove("ball_taken");
			State.carrying = "";
			if (ball != null)
			{
				ball.ReturnToSwing();
			}
			if (ui != null)
			{
				ui.SetCarried("");
			}
			AdvanceTo("S2_ball");
			if (ui != null)
			{
				ui.ShowToast("木匣的卡簧一弹，红球蹦回了秋千底下。", 3.2f);
			}
		}
		else if (ui != null)
		{
			ui.ShowToast("星盘咔地一声卡死，又转回了原位。", 3.2f);
		}
	}

	public void Init(EggboyPlayer p, EggboyUI u, GameState s, SaveStore st, ControlGate g, WaterLevel w)
	{
		player = p;
		ui = u;
		State = s;
		store = st;
		gate = g;
		water = w;
		game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		island = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		dog = UnityEngine.Object.FindObjectOfType<DogFollower>();
		ball = UnityEngine.Object.FindObjectOfType<SchoolBall>();
		LoadData();
		if (!IsAuditMode && State.step == "P1_mailbox" && State.flags.Contains("letters_taken")) { State.step = "B0_arrive"; store.Save(State); }
		if ((State.step == "E2_climb" || State.step == "E3_end" || State.flags.Contains("ending_done")) && !State.stamps.Contains("ending"))
		{
			State.stamps.Add("ending");
			store.Save(State);
		}
		ui.BindPostmarks(State.stamps);
		ready = true;
		ApplyStepGoal();
		EggboyUI eggboyUI = ui;
		eggboyUI.OnLetterClosed = (Action)Delegate.Combine(eggboyUI.OnLetterClosed, new Action(OnLetterClosed));
		ui.SetCarried(CarryLabel());
		if (island != null)
		{
			modelProvider.Register("wood", island.woodModel);
			modelProvider.Register("oil", island.oilModel);
		}
		ShowCarriedModel(State.carrying);
	}

	private void LoadData()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("Story/letters");
		if (textAsset != null)
		{
			LetterList letterList = JsonUtility.FromJson<LetterList>(textAsset.text);
			if (letterList != null && letterList.letters != null)
			{
				letters = letterList.letters;
			}
		}
		TextAsset textAsset2 = Resources.Load<TextAsset>("Story/steps");
		if (textAsset2 != null)
		{
			StepList stepList = JsonUtility.FromJson<StepList>(textAsset2.text);
			if (stepList != null && stepList.steps != null)
			{
				steps = stepList.steps;
			}
		}
	}

	private StoryStep Step(string id)
	{
		StoryStep[] array = steps;
		foreach (StoryStep storyStep in array)
		{
			if (storyStep.id == id)
			{
				return storyStep;
			}
		}
		return null;
	}

	private void ApplyStepGoal()
	{
		if (IsAuditMode)
		{
			if (State.step == "B4_light" && State.flags.Contains("oven_lit"))
			{
				ui.SetGoal("目标：第一站完成，烟囱冒烟了");
			}
			else
			{
				StoryStep storyStep = Step(State.step);
				if (storyStep != null && !string.IsNullOrEmpty(storyStep.goal))
				{
					ui.SetGoal(storyStep.goal);
				}
			}
			if (island != null)
			{
				if (island.mailboxTrail != null)
				{
					island.mailboxTrail.SetActive(State.step == "P0_boot");
				}
				if (island.endingDesk != null)
				{
					island.endingDesk.SetActive(State.flags.Contains("dawn_shown"));
				}
				island.SyncEndingDeskCollider();
			}
			ui.SetTutorial((State.step == "P0_boot") ? "沿金色路标走到邮筒 · 靠近后按 E 取信" : "");
			return;
		}
		StoryStep storyStep2 = Step(State.step);
		string goalTxt = (storyStep2 != null) ? storyStep2.goal : "";
		if (!IsAuditMode && State.step == "B0_arrive") goalTxt = "目标：去林阿婆的面包店，替冷掉的烤炉生火";
		ui.SetGoal(goalTxt);
		if (island != null)
		{
			if (island.mailboxTrail != null)
			{
				island.mailboxTrail.SetActive(value: false);
			}
			if (island.endingDesk != null)
			{
				island.endingDesk.SetActive(State.flags.Contains("dawn_shown"));
			}
			island.SyncEndingDeskCollider();
		}
	}

	public string CarryLabel()
	{
		if (State == null)
		{
			return "";
		}
		if (State.carrying == "matches")
		{
			return "邮包 · 火柴盒";
		}
		if (State.carrying == "wood")
		{
			return "手上 · 柴火捆";
		}
		if (State.carrying == "ball")
		{
			return "手上 · 红球";
		}
		if (State.carrying == "oil")
		{
			return "手上 · 油壶";
		}
		return "";
	}

	public void TakeLetters()
	{
		if (!ready || State.flags.Contains("letters_taken"))
		{
			return;
		}
		State.flags.Add("letters_taken");
			TownVoiceover.Play("vo_mailbox_take");
		State.step = "P1_mailbox";
		if (island != null && island.mailboxTrail != null)
		{
			island.mailboxTrail.SetActive(value: false);
		}
		store.Save(State);
		if (ui != null)
		{
			ui.SetTutorial("");
		}
		letterLock = gate.Acquire("letter");
		if (letterLock != null && player != null)
		{
			player.locked = true;
		}
		PlayAction("pickup_mailbox", ExprId.curious, 0.9f, 0.4f);
		string id = Step(State.step)?.letter;
		LetterData letterData = FindLetter(id);
		if (ui != null)
		{
			if (letterData != null)
			{
				ui.ShowLetter(letterData.title, letterData.body, letterData.signature, letterData.id);
			}
			else
			{
				ui.ShowLetter();
			}
		}
	}

	private void PlayAction(string clip, ExprId expr, float lockSec = -1f, float grabSec = -1f, Action onGrab = null, Action onComplete = null)
	{
		if (player != null)
		{
			if (IsAuditMode || !player.HasClip(clip))
			{
				if (player.HasClip(clip))
				{
					player.PlayEmote(clip);
				}
				onGrab?.Invoke();
				onComplete?.Invoke();
			}
			else
			{
				player.PlayInteractAction(clip, lockSec, grabSec, onGrab, onComplete);
			}
		}
		else
		{
			onGrab?.Invoke();
			onComplete?.Invoke();
		}
		if (game != null)
		{
			game.SetExpression(expr);
		}
	}

	private void SyncCarryPose()
	{
		if (!(player == null) && State != null)
		{
			switch (State.carrying)
			{
			case "wood":
				player.SetCarryPose(EggboyPlayer.CarryPose.Wood);
				break;
			case "oil":
				player.SetCarryPose(EggboyPlayer.CarryPose.Oil);
				break;
			case "ball":
				player.SetCarryPose((!(PhysicalPickupController.Instance != null) || !PhysicalPickupController.Instance.HoldsBall) ? EggboyPlayer.CarryPose.Ball : EggboyPlayer.CarryPose.None);
				break;
			default:
				player.SetCarryPose(EggboyPlayer.CarryPose.None);
				break;
			}
		}
	}

	public LetterData GetLetter(string id)
	{
		LetterData[] array = letters;
		foreach (LetterData letterData in array)
		{
			if (letterData.id == id)
			{
				return letterData;
			}
		}
		return null;
	}

	private LetterData FindLetter(string id)
	{
		LetterData letterData = null;
		LetterData[] array = letters;
		foreach (LetterData letterData2 in array)
		{
			if (letterData2.id == id)
			{
				letterData = letterData2;
			}
		}
		if (letterData == null && letters.Length != 0)
		{
			letterData = letters[0];
		}
		return letterData;
	}

	public void AdvanceTo(string id)
	{
		State.step = id;
		store.Save(State);
		ApplyStepGoal();
	}

	private IEnumerator FadeTo(float alpha, float seconds)
	{
		EnsureFade();
		float from = fade.color.a;
		float t = 0f;
		while (t < seconds)
		{
			t += Time.unscaledDeltaTime;
			Color color = fade.color;
			color.a = Mathf.Lerp(from, alpha, Mathf.Clamp01(t / seconds));
			fade.color = color;
			yield return null;
		}
		Color color2 = fade.color;
		color2.a = alpha;
		fade.color = color2;
	}

	private void EnsureFade()
	{
		if (!(fade != null))
		{
			GameObject gameObject = new GameObject("WP14 Fade", typeof(Canvas), typeof(Image));
			gameObject.transform.SetParent(base.transform, worldPositionStays: false);
			Canvas component = gameObject.GetComponent<Canvas>();
			component.renderMode = RenderMode.ScreenSpaceOverlay;
			component.sortingOrder = 40;
			RectTransform component2 = gameObject.GetComponent<RectTransform>();
			component2.anchorMin = Vector2.zero;
			component2.anchorMax = Vector2.one;
			component2.offsetMin = Vector2.zero;
			component2.offsetMax = Vector2.zero;
			fade = gameObject.GetComponent<Image>();
			fade.color = new Color(0f, 0f, 0f, 0f);
			fade.raycastTarget = false;
		}
	}

	private IEnumerator MoveCamera(Vector3 from, Vector3 look, float seconds)
	{
		Camera cam = Camera.main;
		if (!(cam == null))
		{
			Vector3 p0 = cam.transform.position;
			Quaternion r0 = cam.transform.rotation;
			float f0 = cam.fieldOfView;
			Quaternion r1 = Quaternion.LookRotation(look - from, Vector3.up);
			float t = 0f;
			while (t < seconds)
			{
				t += Time.deltaTime;
				float t2 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
				cam.transform.position = Vector3.Lerp(p0, from, t2);
				cam.transform.rotation = Quaternion.Slerp(r0, r1, t2);
				cam.fieldOfView = Mathf.Lerp(f0, 40f, t2);
				yield return null;
			}
			cam.transform.SetPositionAndRotation(from, r1);
			cam.fieldOfView = 40f;
		}
	}

	private void OnLetterClosed()
	{
		bool num = letterLock != null || readingSchool;
		if (gate != null && letterLock != null)
		{
			gate.Release(letterLock);
		}
		letterLock = null;
		if (!num)
		{
			if (!IsAuditMode && (State.step == "P1_mailbox" || State.step == "B0_arrive" || State.step == "S4_read" || State.step == "L3_oil" || State.step == "E1_read"))
			{
				num = true;
			}
			else
			{
				ApplyStepGoal();
				return;
			}
		}
		if (player != null)
		{
			player.locked = false;
			player.StopEmote("read_letter");
		}
		if (readingSchool)
		{
			readingSchool = false;
			ApplyStepGoal();
			return;
		}
		if (State.step == "E1_read")
		{
			if (!State.flags.Contains("ending_letter_read"))
			{
				State.flags.Add("ending_letter_read");
			}
			CollectPostmark("ending");
			if (game != null)
			{
				game.SetExpression(ExprId.happy);
			}
		}
		StoryStep storyStep = Step(State.step);
		if (storyStep != null && !string.IsNullOrEmpty(storyStep.next))
		{
			if (!IsAuditMode && storyStep.id == "B0_arrive")
			{
				if (State.flags.Contains("wood_loaded") && State.flags.Contains("matches_taken")) AdvanceTo("B4_light");
				else if (State.carrying == "wood") AdvanceTo("B3_load");
				else if (State.flags.Contains("matches_taken")) AdvanceTo("B2_wood");
				else AdvanceTo("B1_matches");
			}
			else
			{
				AdvanceTo(storyStep.next);
			}
		}
		else
		{
			ApplyStepGoal();
		}
	}

	private void CollectPostmark(string delivery)
	{
		if (!State.stamps.Contains(delivery))
		{
			State.stamps.Add(delivery);
			ui.PlayDeliveryStamp(delivery);
		}
	}

	private Dictionary<string, Vector3> RoutePoints()
	{
		if (routePoints != null)
		{
			return routePoints;
		}
		routePoints = new Dictionary<string, Vector3>();
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		if (textAsset == null)
		{
			return routePoints;
		}
		StoryRouteList storyRouteList = JsonUtility.FromJson<StoryRouteList>(textAsset.text);
		if (storyRouteList == null || storyRouteList.route == null)
		{
			return routePoints;
		}
		StoryRoutePoint[] route = storyRouteList.route;
		foreach (StoryRoutePoint storyRoutePoint in route)
		{
			routePoints[storyRoutePoint.id] = new Vector3(storyRoutePoint.x, 0f, storyRoutePoint.z);
		}
		return routePoints;
	}

	private bool IsNearArrivalPoint(StoryStep st, Vector3 pp, Dictionary<string, Vector3> pts, out Vector3 lookTarget)
	{
		lookTarget = pts[st.at];
		float num = pp.x - lookTarget.x;
		float num2 = pp.z - lookTarget.z;
		float num3 = num * num + num2 * num2;
		if (IsAuditMode)
		{
			return num3 <= 1.9599999f;
		}
		float num4 = 3.8f;
		if (num3 <= num4 * num4)
		{
			return true;
		}
		if (st.id == "S0_arrive")
		{
			string[] array = new string[2] { "19", "21" };
			foreach (string key in array)
			{
				if (pts.ContainsKey(key))
				{
					Vector3 vector = pts[key];
					float num5 = pp.x - vector.x;
					float num6 = pp.z - vector.z;
					if (num5 * num5 + num6 * num6 <= 23.04f)
					{
						lookTarget = vector;
						return true;
					}
				}
			}
		}
		else if (st.id == "B0_arrive" && pts.ContainsKey("12"))
		{
			Vector3 vector2 = pts["12"];
			float num7 = pp.x - vector2.x;
			float num8 = pp.z - vector2.z;
			if (num7 * num7 + num8 * num8 <= 17.639997f)
			{
				return true;
			}
		}
		return false;
	}

	private void Update()
	{
		if (!ready || player == null || ui == null || gate == null)
		{
			return;
		}
		SyncWorldProps();
		ui.SetCinematic(Cutscene || Climbing || Ended || (game != null && game.introHold));
		TickFirstTutorials();
		StoryStep storyStep = Step(State.step);
		DebugAuto = "step=" + State.step + " auto=" + (storyStep?.auto ?? false) + " at=" + ((storyStep != null) ? storyStep.at : "") + " gate=" + gate.Locked + " letter=" + ui.LetterOpen + " arrived=" + arrivedStep;
		if (gate.Locked || ui.LetterOpen)
		{
			return;
		}
		if (!IsAuditMode && State.step == "P1_mailbox" && State.flags.Contains("letters_taken"))
		{
			AdvanceTo("B0_arrive");
			return;
		}
		if (State.step == "L3_oil" && State.flags.Contains("lamp_oiled") && !ui.LetterOpen)
		{
			AdvanceTo("L4_light");
		}
		else
		{
			if (storyStep == null || !storyStep.auto || string.IsNullOrEmpty(storyStep.at) || arrivedStep == storyStep.id)
			{
				return;
			}
			Dictionary<string, Vector3> dictionary = RoutePoints();
			if (!dictionary.ContainsKey(storyStep.at))
			{
				return;
			}
			Vector3 vector = dictionary[storyStep.at];
			Vector3 position = player.transform.position;
			float num = position.x - vector.x;
			float num2 = position.z - vector.z;
			DebugAuto += $" d={Mathf.Sqrt(num * num + num2 * num2):F2}";
			if (!IsNearArrivalPoint(storyStep, position, dictionary, out var lookTarget))
			{
				return;
			}
			arrivedStep = storyStep.id;
			if (storyStep.id == "E2_climb")
			{
				if (!cutscene)
				{
					StartCoroutine(E2Sequence());
				}
				return;
			}
			if (storyStep.id == "E0_back")
			{
				if (dog != null)
				{
					dog.Follow();
				}
				SkyDimmer skyDimmer = UnityEngine.Object.FindObjectOfType<SkyDimmer>();
				if (skyDimmer != null)
				{
					skyDimmer.Dawn(6f);
				}
				if (!State.flags.Contains("dawn_shown"))
				{
					State.flags.Add("dawn_shown");
					store.Save(State);
				}
				ui.ShowToast("桌上多了一封信。");
			}
			if (string.IsNullOrEmpty(storyStep.letter) || (!IsAuditMode && storyStep.id == "B0_arrive"))
			{
				if (!string.IsNullOrEmpty(storyStep.next))
				{
					if (!IsAuditMode && storyStep.id == "B0_arrive")
			{
				if (State.flags.Contains("wood_loaded") && State.flags.Contains("matches_taken")) AdvanceTo("B4_light");
				else if (State.carrying == "wood") AdvanceTo("B3_load");
				else if (State.flags.Contains("matches_taken")) AdvanceTo("B2_wood");
				else AdvanceTo("B1_matches");
			}
			else
			{
				AdvanceTo(storyStep.next);
			}
				}
				else
				{
					ApplyStepGoal();
				}
				return;
			}
			letterLock = gate.Acquire("letter");
			if (letterLock == null)
			{
				return;
			}
			if (player != null)
			{
				player.locked = true;
				player.FaceTowards(lookTarget);
				if (player.HasClip("read_letter"))
				{
					player.PlayEmote("read_letter", int.MaxValue);
				}
				else
				{
					player.PlayEmote("look");
				}
				if (game != null)
				{
					game.SetExpression((storyStep.id == "S0_arrive") ? ExprId.sad : ExprId.curious);
				}
			}
			LetterData letterData = FindLetter(storyStep.letter);
			if (letterData != null)
			{
				ui.ShowLetterDetail(letterData.title, letterData.body, letterData.signature, letterData.id);
			}
			else
			{
				ui.ShowLetter();
			}
		}
	}

	private void TickFirstTutorials()
	{
		if (State.seenTutorials == null)
		{
			State.seenTutorials = new List<string>();
		}
		if (!BasicsTutorial.Running && !ui.FirstTutorialActive && (IsAuditMode || (!ui.Cinematic && !ui.Paused && !ui.LetterOpen && !ui.TitleShowing && !MenuController.OverlayOpen)))
		{
			bool flag = State.flags.Contains("letters_taken") && State.step != "P1_mailbox";
			if (!OfferTutorial("tab_letter", flag, "Tab\u3000随时重读已收信件（可拆字）", KeyCode.Tab) && !OfferTutorial("map_town", flag && island != null && player.transform.position.x > island.MailboxPos.x + 3f, "M\u3000打开潮汐镇地图", KeyCode.M) && !OfferTutorial("dog_command", dog != null && dog.Following, "F\u3000指挥小黑踩踏板 / 嗅旧物 / 互动", KeyCode.F) && !OfferTutorial("pocketwatch_r", flag && !State.seenTutorials.Contains("pocketwatch_r") && UnityEngine.Object.FindObjectOfType<TidalEchoCloneSystem>() != null, "R\u3000唤出 10 秒前的回声分身", KeyCode.R) && !OfferTutorial("viewfinder_c", flag && !State.seenTutorials.Contains("viewfinder_c") && UnityEngine.Object.FindObjectOfType<PerspectivePrintSystem>() != null, "C\u3000举起透视取景框", KeyCode.C))
			{
				OfferTutorial("wishlist_v", flag && !State.seenTutorials.Contains("wishlist_v") && UnityEngine.Object.FindObjectOfType<AIAgentCalibrationDirector>() != null, "V\u3000写下心愿召唤微缩物", KeyCode.V);
			}
		}
	}

	private bool OfferTutorial(string id, bool unlocked, string text, KeyCode key)
	{
		if (!unlocked || State.seenTutorials.Contains(id))
		{
			return false;
		}
		Debug.Log("TUTORIAL_ONCE " + id);
		return ui.ShowFirstTutorial(text, key, delegate
		{
			if (!State.seenTutorials.Contains(id))
			{
				State.seenTutorials.Add(id);
				store.Save(State);
			}
		});
	}

	public void DescendTower()
	{
		if (ready && !cutscene && !climbing && ((player != null && player.transform.position.y > 10f) || !(State.step != "L5_reply") || !(State.step != "E0_back")))
		{
			StartCoroutine(DescendTowerRoutine());
		}
	}

	private static Vector3 MarkerPos(string name, Vector3 fallback)
	{
		GameObject gameObject = GameObject.Find(name);
		if (!(gameObject != null))
		{
			return fallback;
		}
		return gameObject.transform.position;
	}

	private static Vector3 EndingCityPos()
	{
		EndingPoint city = EndingData.Load().city;
		if (city == null)
		{
			return new Vector3(-85f, 7f, 25f);
		}
		return new Vector3(city.x, city.y, city.z);
	}

	private IEnumerator DescendTowerRoutine()
	{
		cutscene = true;
		if (player != null)
		{
			player.locked = true;
		}
		yield return FadeTo(1f, 0.55f);
		Vector3 vector = LighthouseData.Point(LighthouseData.Load().door);
		if (vector.y <= 0.1f)
		{
			vector = new Vector3(31.5f, 5f, 6.6f);
		}
		if (player != null)
		{
			player.transform.position = vector;
			player.vel = Vector3.zero;
			player.grounded = true;
			player.yaw = MathF.PI;
			player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
		}
		ChaseCamera chaseCamera = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		if (chaseCamera != null)
		{
			chaseCamera.SnapBehind(vector, MathF.PI);
		}
		DogFollower dogFollower = UnityEngine.Object.FindObjectOfType<DogFollower>();
		if (dogFollower != null)
		{
			dogFollower.Follow();
		}
		yield return null;
		yield return null;
		if (State.step == "L5_reply")
		{
			AdvanceTo("E0_back");
		}
		yield return FadeTo(0f, 0.55f);
		if (player != null)
		{
			player.locked = false;
		}
		cutscene = false;
	}

	public void ReadEndingLetter()
	{
		if (ready && !(State.step != "E1_read") && !ui.LetterOpen && letterLock == null)
		{
			LetterData letterData = FindLetter("zhou_eggboy");
			letterLock = ((gate != null) ? gate.Acquire("endingletter") : null);
			if (letterLock != null && player != null)
			{
				player.locked = true;
			}
			PlayAction("ending_desk", ExprId.surprised, 1f, 0.4f);
			if (letterData != null)
			{
				ui.ShowLetterDetail(letterData.title, letterData.body, letterData.signature, letterData.id);
			}
			else
			{
				ui.ShowLetter();
			}
			StartCoroutine(EndingLetterMood());
		}
	}

	private IEnumerator EndingLetterMood()
	{
		yield return new WaitForSecondsRealtime(4.5f);
		if (ui != null && ui.LetterOpen && game != null)
		{
			game.SetExpression(ExprId.happy);
		}
	}

	private IEnumerator E2Sequence()
	{
		cutscene = true;
		Mark("E2a");
		if (player != null)
		{
			player.locked = true;
			player.FaceTowards(new Vector3(0f, 2f, -6f));
		}
		if (water != null)
		{
			water.SetLevel("L4", 4f);
		}
		ChaseCamera chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		if (chase != null)
		{
			chase.enabled = false;
		}
		GameObject fill = new GameObject("EndingFillLight");
		Light light = fill.AddComponent<Light>();
		light.type = LightType.Directional;
		light.color = new Color32(byte.MaxValue, 226, 196, byte.MaxValue);
		light.intensity = 0.45f;
		light.shadows = LightShadows.None;
		Vector3 vector = MarkerPos("P_06", new Vector3(-24.7f, 6.5f, 10.8f));
		Vector3 vector2 = MarkerPos("P_13", new Vector3(0f, 3f, -6f));
		Vector3 vector3 = vector + new Vector3(-4.3f, 7f, 8.2f);
		Vector3 vector4 = new Vector3(vector2.x + 2f, 1f, vector2.z + 1f);
		fill.transform.rotation = Quaternion.LookRotation(vector4 - vector3, Vector3.up);
		yield return MoveCamera(vector3, vector4, 1.6f);
		Mark("E2cam");
		yield return new WaitForSecondsRealtime(8f);
		Mark("E2wait");
		UnityEngine.Object.Destroy(fill);
		if (chase != null && player != null)
		{
			chase.enabled = true;
			chase.SnapBehind(player.transform.position, player.yaw);
		}
		if (player != null)
		{
			player.locked = false;
		}
		cutscene = false;
		AdvanceTo("E3_end");
		Mark("E3go");
		StartCoroutine(E3Sequence());
	}

	private IEnumerator E3Sequence()
	{
		cutscene = true;
		Mark("E3a");
		if (player != null)
		{
			player.locked = true;
		}
		ChaseCamera chaseCamera = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		if (chaseCamera != null)
		{
			chaseCamera.enabled = false;
		}
		CityGlow cityGlow = UnityEngine.Object.FindObjectOfType<CityGlow>();
		if (cityGlow != null)
		{
			cityGlow.Show();
		}
		if (game != null)
		{
			game.SetExpression(ExprId.happy);
		}
		ui.SetGoal("");
		ui.ShowSubtitle("所有的信，都送到了。");
		Vector3 vector = MarkerPos("P_06", new Vector3(-24.7f, 6.5f, 10.8f));
		Vector3 look = EndingCityPos();
		yield return MoveCamera(vector + new Vector3(3.2f, 3f, 4.7f), look, 2.2f);
		Mark("E3cam");
		yield return new WaitForSecondsRealtime(1.6f);
		if (player != null)
		{
			player.PlayEmote(player.HasClip("signal_wave") ? "signal_wave" : "wave", 3);
		}
		if (!State.flags.Contains("ending_done"))
		{
			State.flags.Add("ending_done");
			store.Save(State);
		}
		Ended = true;
		endLock = ((gate != null) ? gate.Acquire("ending") : null);
		cutscene = false;
		Mark("E3done");
		yield return new WaitForSecondsRealtime(3f);
		ui.ShowSubtitle("回家吧。山这边的新邮局，一直给你留着位置。");
		yield return new WaitForSecondsRealtime(3f);
		base.gameObject.AddComponent<EndingDispatch>();
	}

	public void TakeOilAt(Vector3 pos)
	{
		if (ready && !(State.step != "L1_shed") && !State.flags.Contains("oil_taken") && !BeginPhysical(new PhysicalPickupController.Job
		{
			clip = "pickup_oil",
			target = () => pos + Vector3.up * 0.25f,
			diameter = 0.18f,
			standDistance = 0.42f,
			contactTime = 0.45f,
			lockSeconds = 0.95f,
			onContact = delegate
			{
				TakeOilCommit(animate: false);
			}
		}))
		{
			TakeOil();
		}
	}

	public void TakeOil()
	{
		TakeOilCommit(animate: true);
	}

	private void TakeOilCommit(bool animate)
	{
		if (!ready || State.step != "L1_shed" || State.flags.Contains("oil_taken"))
		{
			return;
		}
		State.flags.Add("oil_taken");
		State.carrying = "oil";
		store.Save(State);
		if (animate)
		{
			PlayAction("pickup_oil", ExprId.happy, 0.95f, 0.45f, delegate
			{
				ShowCarriedModel("oil");
			});
		}
		else
		{
			ShowCarriedModel("oil");
		}
		ui.SetCarried(CarryLabel());
		ui.ShowToast("油壶里还剩小半壶。");
		AdvanceTo("L2_door");
	}

	public void ClimbTower()
	{
		if (ready && !climbing && (!(State.step != "L2_door") || !(State.step != "L3_oil") || !(State.step != "L4_light")))
		{
			if (player != null && player.HasClip("open_lighthouse_door"))
			{
				player.PlayEmote("open_lighthouse_door");
			}
			StartCoroutine(ClimbTowerRoutine());
		}
	}

	private IEnumerator ClimbTowerRoutine()
	{
		climbing = true;
		if (player != null)
		{
			player.locked = true;
		}
		DogFollower dogFollower = UnityEngine.Object.FindObjectOfType<DogFollower>();
		if (dogFollower != null && !DogFollower.StayNearSchool)
		{
			dogFollower.Hide();
		}
		yield return FadeTo(1f, 0.55f);
		LighthouseList lighthouseList = LighthouseData.Load();
		Vector3 vector = LighthouseData.Point(lighthouseList.deck);
		if (vector.y <= 0.1f)
		{
			vector = new Vector3(22.18f, 16.98f, 3.3f);
		}
		Vector3 vector2 = LighthouseData.Point(lighthouseList.lamp);
		float num = Mathf.Atan2(vector2.x - vector.x, vector2.z - vector.z);
		if (player != null)
		{
			player.transform.position = vector;
			player.vel = Vector3.zero;
			player.grounded = true;
			player.yaw = num;
			player.transform.rotation = Quaternion.Euler(0f, num * 57.29578f, 0f);
		}
		ChaseCamera chaseCamera = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		if (chaseCamera != null)
		{
			chaseCamera.SnapBehind(vector, num);
		}
		yield return null;
		yield return null;
		if (State.step == "L2_door")
		{
			AdvanceTo(State.flags.Contains("lamp_oiled") ? "L4_light" : "L3_oil");
		}
		yield return FadeTo(0f, 0.55f);
		if (player != null)
		{
			player.locked = false;
		}
		climbing = false;
	}

	public void OilLampAt(Vector3 pos)
	{
		if (ready && !(State.step != "L3_oil") && !State.flags.Contains("lamp_oiled") && !BeginPhysical(new PhysicalPickupController.Job
		{
			clip = "hold_pour_oil",
			target = () => pos,
			diameter = 0.2f,
			standDistance = 0.55f,
			contactTime = 0.4f,
			lockSeconds = 0.8f,
			onContact = delegate
			{
				OilLampCommit(animate: false);
			}
		}))
		{
			OilLamp();
		}
	}

	public void OilLamp()
	{
		OilLampCommit(animate: true);
	}

	private IEnumerator PourThenRemove()
	{
		HandCarry hc = ((carriedVisual != null) ? carriedVisual.GetComponent<HandCarry>() : null);
		if (hc != null)
		{
			hc.pourTarget = 75f;
			yield return new WaitForSeconds(0.75f);
			hc.pourTarget = 0f;
			yield return new WaitForSeconds(0.15f);
		}
		if (State != null && State.carrying != "oil")
		{
			ShowCarriedModel(null);
		}
	}

	private void OilLampCommit(bool animate)
	{
		if (!ready || State.step != "L3_oil" || State.flags.Contains("lamp_oiled"))
		{
			return;
		}
		State.flags.Add("lamp_oiled");
		State.carrying = "";
		store.Save(State);
		ui.SetCarried("");
		if (animate)
		{
			PlayAction("hold_pour_oil", ExprId.thinking, 0.8f, 0.4f, delegate
			{
				ShowCarriedModel(null);
			});
		}
		else
		{
			StartCoroutine(PourThenRemove());
		}
		StoryStep storyStep = Step("L3_oil");
		LetterData letterData = FindLetter((storyStep != null) ? storyStep.letter : "");
		letterLock = ((gate != null) ? gate.Acquire("lighthouse") : null);
		if (letterLock != null && player != null)
		{
			player.locked = true;
		}
		if (letterData != null)
		{
			ui.ShowLetterDetail(letterData.title, letterData.body, letterData.signature, letterData.id);
		}
		else
		{
			ui.ShowLetter();
		}
	}

	public void LightLamp()
	{
		if (ready && !(State.step != "L4_light") && !State.flags.Contains("beam_lit"))
		{
			WoodenMechanismPuzzleModal.OpenOrRun("lighthouse_lock", State, store, player, ui, gate, CompleteLightLamp);
		}
	}

	private void CompleteLightLamp()
	{
		if (!ready || State.flags.Contains("beam_lit"))
		{
			return;
		}
		State.flags.Add("beam_lit");
		store.Save(State);
		if (game != null)
		{
			game.SetExpression(ExprId.excited);
		}
		if (player != null)
		{
			player.PlayEmote("hop");
		}
		SkyDimmer skyDimmer = UnityEngine.Object.FindObjectOfType<SkyDimmer>();
		if (skyDimmer != null)
		{
			skyDimmer.Dim(20f);
		}
		BeaconLight beaconLight = UnityEngine.Object.FindObjectOfType<BeaconLight>();
		FlashlightReply flashlightReply = UnityEngine.Object.FindObjectOfType<FlashlightReply>();
		if (IsAuditMode)
		{
			if (beaconLight != null)
			{
				beaconLight.Face(EndingCityPos());
				beaconLight.TurnOn();
			}
			if (flashlightReply != null)
			{
				flashlightReply.Begin();
			}
			ui.ShowToast("灯塔亮了。");
			StartCoroutine(FinishLighthouse(flashlightReply));
		}
		else
		{
			StartCoroutine(InteractiveLighthouseRoutine(beaconLight, flashlightReply));
		}
	}

	private void EnsureSignalCardUI()
	{
		if (!(signalCard != null))
		{
			RectTransform rectTransform = TideTownStyleUI.FindHudCanvasRoot(ui);
			if (!(rectTransform == null))
			{
				signalCard = TideTownStyleUI.CreateParchmentCard(rectTransform, "LighthouseSignalCard", 440f, 220f, 4);
				RectTransform rectTransform2 = signalCard;
				Vector2 anchorMin = (signalCard.anchorMax = new Vector2(1f, 1f));
				rectTransform2.anchorMin = anchorMin;
				signalCard.pivot = new Vector2(1f, 1f);
				signalCard.anchoredPosition = new Vector2(-24f, -64f);
				TideTownStyleUI.CreateText(signalCard, "塔顶观海便签 · 呼唤山那边的人", 18, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, new Vector2(0f, 72f), new Vector2(390f, 28f), bold: true);
				signalAlignLabel = TideTownStyleUI.CreateText(signalCard, "", 13, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, new Vector2(0f, 34f), new Vector2(390f, 24f));
				signalFlashLabel = TideTownStyleUI.CreateText(signalCard, "", 13, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, new Vector2(0f, 2f), new Vector2(390f, 24f));
				signalReplyLabel = TideTownStyleUI.CreateText(signalCard, "", 13, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft, new Vector2(0f, -28f), new Vector2(390f, 24f));
				TideTownStyleUI.CreateWarmButton(signalCard, "朝山那边挥手闪灯", new Vector2(0f, -74f), new Vector2(360f, 36f), primaryAmber: true, TriggerSignalFlash);
			}
		}
	}

	private void RefreshSignalCardUI()
	{
		if (!(signalCard == null))
		{
			int num = Mathf.RoundToInt(signalAlignment * 100f);
			if (signalAlignLabel != null)
			{
				signalAlignLabel.text = "光束正掠过夜海，山顶新城就在远方（光束对焦：" + num + "%）";
			}
			if (signalFlashLabel != null)
			{
				signalFlashLabel.text = "山那边的人在等你的身影，朝他们挥挥手吧（信标：" + signalFlashes + " / 3）";
			}
			int num2 = ((activeReply != null) ? activeReply.LitCount : 0);
			if (signalReplyLabel != null)
			{
				signalReplyLabel.text = "对岸山头亮起的手电：" + num2 + " / 40 盏";
			}
		}
	}

	private IEnumerator InteractiveLighthouseRoutine(BeaconLight beacon, FlashlightReply reply)
	{
		TownVoiceover.Play("vo_lighthouse_lit");
		activeBeacon = beacon;
		activeReply = reply;
		signalFlashes = 0;
		signalActive = true;
		EnsureSignalCardUI();
		if (signalCard != null)
		{
			signalCard.gameObject.SetActive(value: true);
		}
		Vector3 targetCity = EndingCityPos();
		if (beacon != null)
		{
			beacon.ManualMode = true;
			beacon.SetYawOffsetFrom(targetCity, -34f);
			beacon.TurnOn();
		}
		ui.ShowToast("灯塔亮起了暖光。山那边的人正在等你的身影，朝他们挥挥手吧。");
		float idleTimer = 0f;
		float flashCooldown = 0f;
		while (signalFlashes < 3)
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			if (flashCooldown > 0f)
			{
				flashCooldown -= unscaledDeltaTime;
			}
			float num = 0f;
			if (EasyInputBridge.Held(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
			{
				num -= 52f * unscaledDeltaTime;
			}
			if (EasyInputBridge.Held(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
			{
				num += 52f * unscaledDeltaTime;
			}
			if (Input.GetMouseButton(0))
			{
				num += Input.GetAxis("Mouse X") * 2.6f;
			}
			float axisRaw = Input.GetAxisRaw("Horizontal");
			if (Mathf.Abs(axisRaw) > 0.18f)
			{
				num += axisRaw * 52f * unscaledDeltaTime;
			}
			if (HardwareAndSensorHub.Instance != null && HardwareAndSensorHub.Instance.CamOpen)
			{
				float num2 = (HardwareAndSensorHub.Instance.HandCenterX - 0.5f) * 2f;
				if (Mathf.Abs(num2) > 0.18f)
				{
					num += num2 * 48f * unscaledDeltaTime;
				}
			}
			if (Mathf.Abs(num) > 0.001f)
			{
				idleTimer = 0f;
				if (beacon != null)
				{
					beacon.RotateYaw(num);
				}
			}
			else
			{
				idleTimer += unscaledDeltaTime;
			}
			signalAlignment = ((beacon != null) ? beacon.AlignmentWith(targetCity) : 1f);
			bool flag = HardwareAndSensorHub.Instance != null && HardwareAndSensorHub.Instance.CamOpen && HardwareAndSensorHub.Instance.HandWaveSpeed > 0.38f;
			bool flag2 = Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton3) || Input.GetKeyDown(KeyCode.JoystickButton5);
			bool flag3 = flashCooldown <= 0f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || flag || flag2);
			if (idleTimer > 11f)
			{
				if (beacon != null && signalAlignment < 0.92f)
				{
					beacon.Face(targetCity);
					signalAlignment = 1f;
				}
				if (flashCooldown <= 0f)
				{
					flag3 = true;
				}
			}
			if (flag3)
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
		if (signalCard != null)
		{
			signalCard.gameObject.SetActive(value: false);
		}
		if (beacon != null)
		{
			beacon.Face(targetCity);
			beacon.ManualMode = false;
		}
		yield return new WaitForSecondsRealtime(1.5f);
		if (player != null)
		{
			player.PlayEmote(player.HasClip("signal_wave") ? "signal_wave" : "wave", 2);
		}
		FinishLighthouseNow();
	}

	private void TriggerSignalFlash()
	{
		signalFlashes = Mathf.Min(3, signalFlashes + 1);
		TideTownAchievementSystem.Unlock("cam_wave");
		if (activeBeacon != null)
		{
			activeBeacon.Face(EndingCityPos());
			activeBeacon.PulseFlash();
		}
		if (player != null)
		{
			string text = (player.HasClip("signal_wave") ? "signal_wave" : "wave");
			player.PlayEmote((signalFlashes >= 3) ? text : "hop");
		}
		if (game != null)
		{
			game.SetExpression((signalFlashes < 3) ? ExprId.excited : ExprId.happy);
		}
		int num = ((activeReply != null && activeReply.Total > 0) ? activeReply.Total : 40);
		int count = ((signalFlashes == 1) ? Mathf.CeilToInt((float)num * 0.33f) : ((signalFlashes == 2) ? Mathf.CeilToInt((float)num * 0.68f) : num));
		if (activeReply != null)
		{
			activeReply.RevealUpTo(count);
		}
		RefreshSignalCardUI();
		if (ui != null)
		{
			string text2 = ((signalFlashes == 1) ? "闪灯信号 1/3 · 对岸山头亮起了第一批手电光！" : ((signalFlashes == 2) ? "闪灯信号 2/3 · 越来越多的手电筒在山那边闪烁回应！" : "闪灯信号 3/3 · 整座山头 40 盏手电全部亮起，他们看见你了！"));
			ui.ShowToast(text2, 3.6f);
		}
	}

	private IEnumerator FinishLighthouse(FlashlightReply reply)
	{
		float t0 = Time.realtimeSinceStartup;
		while (reply != null && !reply.Done && Time.realtimeSinceStartup - t0 < 30f)
		{
			yield return null;
		}
		FinishLighthouseNow();
	}

	public void FinishLighthouseNow()
	{
		if (!State.flags.Contains("lighthouse_done"))
		{
			State.flags.Add("lighthouse_done");
			CollectPostmark("lighthouse");
			store.Save(State);
			if (water != null)
			{
				water.SetLevel("L3", 4f);
			}
			if (game != null)
			{
				game.SetExpression(ExprId.happy);
			}
			AdvanceTo("L5_reply");
		}
	}

	private bool BeginPhysical(PhysicalPickupController.Job job)
	{
		PhysicalPickupController pickup = Pickup;
		if (pickup == null || !pickup.Active(job.clip))
		{
			return false;
		}
		if (pickup.Busy)
		{
			return true;
		}
		bool num = pickup.Begin(job);
		if (num && game != null)
		{
			game.SetExpression(ExprId.thinking);
		}
		return num;
	}

	public void InspectSlipperAt(Vector3 pos)
	{
		if (ready && !BeginPhysical(new PhysicalPickupController.Job
		{
			clip = "inspect_slipper",
			target = () => pos + Vector3.up * 0.04f,
			diameter = 0.12f,
			standDistance = 0.32f,
			contactTime = 0.42f,
			lockSeconds = 0.7f,
			tolerance = 0.08f,
			onContact = delegate
			{
				InspectSlipperCommit(animate: false);
			}
		}))
		{
			InspectSlipper();
		}
	}

	public void InspectSlipper()
	{
		InspectSlipperCommit(animate: true);
	}

	private void InspectSlipperCommit(bool animate)
	{
		if (ready)
		{
			if (!State.flags.Contains("slipper_seen"))
			{
				State.flags.Add("slipper_seen");
			}
			if (animate)
			{
				PlayAction("inspect_slipper", ExprId.curious, 0.7f, 0.42f);
			}
			else if (game != null)
			{
				game.SetExpression(ExprId.curious);
			}
			ui.ShowToast("拖鞋上有小黑的牙印，爪印一直通到操场");
		if (!ui.LetterOpen) TownVoiceover.Play("vo_mem_slipper");
			if (State.carrying == "ball")
			{
				AdvanceTo("S3_bowl");
			}
			else
			{
				AdvanceTo("S2_ball");
			}
			if (!IsAuditMode)
			{
				ObjectInspectModal.Open("slipper", "被咬过的拖鞋", "拖鞋上有小黑的牙印，爪印一直通到操场", State, store, player, ui, gate);
			}
		}
	}

	public void TakeBallAt()
	{
		if (!ready || State.flags.Contains("ball_taken"))
		{
			return;
		}
		if (ball != null)
		{
			Transform bt = ball.transform;
			if (BeginPhysical(new PhysicalPickupController.Job
			{
				clip = "pickup_ball",
				target = () => bt.position,
				item = bt,
				diameter = 0.14f,
				standDistance = 0.35f,
				contactTime = 0.4f,
				lockSeconds = 0.85f,
				liftBall = true,
				onContact = delegate
				{
					TakeBallCommit(animate: false);
				}
			}))
			{
				return;
			}
		}
		TakeBall();
	}

	public void TakeBall()
	{
		TakeBallCommit(animate: true);
	}

	private void TakeBallCommit(bool animate)
	{
		if (!ready || State.flags.Contains("ball_taken"))
		{
			return;
		}
		State.flags.Add("ball_taken");
		State.carrying = "ball";
		store.Save(State);
		if (animate)
		{
			PlayAction("pickup_ball", ExprId.happy, 0.85f, 0.4f, delegate
			{
				if (ball != null && player != null)
				{
					ball.Take(player.transform);
				}
				SyncCarryPose();
			});
		}
		else
		{
			SyncCarryPose();
			if (game != null)
			{
				game.SetExpression(ExprId.happy);
			}
		}
		ui.SetCarried(CarryLabel());
		ui.ShowToast("红球上有小黑的牙印 · 快拿去学校后院的狗碗吧");
		AdvanceTo("S3_bowl");
	}

	public void PutBallInBowl()
	{
		if (ready && !(State.carrying != "ball") && !State.flags.Contains("ball_in_bowl"))
		{
			WoodenMechanismPuzzleModal.OpenOrRun("school_lock", State, store, player, ui, gate, CompletePutBallInBowl);
		}
	}

	private void CompletePutBallInBowl()
	{
		if (!ready || State.flags.Contains("ball_in_bowl"))
		{
			return;
		}
		State.flags.Add("ball_in_bowl");
		State.carrying = "";
		store.Save(State);
		ui.SetCarried("");
		PhysicalPickupController pickup = Pickup;
		bool flag = false;
		if (ball != null && pickup != null && pickup.HoldsBall && pickup.HeldBall == ball && !pickup.Busy && pickup.Active("put_ball_bowl"))
		{
			Vector3 bowlBase = ball.BowlPoint - Vector3.up * 0.11f;
			flag = pickup.Begin(new PhysicalPickupController.Job
			{
				clip = "put_ball_bowl",
				target = () => bowlBase,
				standOffset = new Vector3(0.446f, 0f, 0.13f),
				contactTime = 0.48f,
				lockSeconds = 0.9f,
				putBall = true,
				onContact = delegate
				{
					if (ball != null)
					{
						ball.RollIntoBowl();
					}
					SyncCarryPose();
				},
				onDone = delegate
				{
					if (dog != null)
					{
						dog.ShowAt(dog.PeekPoint);
					}
					StartCoroutine(ReadSchoolLetter());
				}
			});
			if (flag && game != null)
			{
				game.SetExpression(ExprId.surprised);
			}
		}
		if (!flag)
		{
			PlayAction("put_ball_bowl", ExprId.surprised, 0.9f, 0.48f, delegate
			{
				if (ball != null)
				{
					ball.RollIntoBowl();
				}
				SyncCarryPose();
			});
		}
		TideTownAchievementSystem.Unlock("classic_ball");
		ui.ShowToast("碗里多了个红球。滑梯下面有动静。");
		AdvanceTo("S4_read");
		if (!flag)
		{
			if (dog != null)
			{
				dog.ShowAt(dog.PeekPoint);
			}
			StartCoroutine(ReadSchoolLetter());
		}
	}

	public void CompleteSchoolWithCustomGift(string giftName, bool isFood)
	{
		if (!ready)
		{
			return;
		}
		TideTownAchievementSystem.Unlock(isFood ? "chef_dog" : "toy_dog");
		if (State.flags.Contains("ball_in_bowl"))
		{
			PlayAction("pet_dog", ExprId.excited, 0.9f, 0.35f);
			if (ui != null)
			{
				ui.ShowToast("小黑欢快地围着你造出的「" + giftName + "」打转！", 3.5f);
			}
			return;
		}
		State.flags.Add("ball_in_bowl");
		State.carrying = "";
		store.Save(State);
		ui.SetCarried("");
		SyncCarryPose();
		PlayAction("pet_dog", ExprId.excited, 0.9f, 0.35f);
		ui.ShowToast("碗旁放上了热气腾腾的「" + giftName + "」。滑梯下面传来了小黑惊喜的动静！", 3.8f);
		if (dog != null)
		{
			dog.ShowAt(dog.PeekPoint);
		}
		AdvanceTo("S4_read");
		StartCoroutine(ReadSchoolLetter());
	}

	private IEnumerator ReadSchoolLetter()
	{
		if (State.flags.Contains("school_read"))
		{
			ApplyStepGoal();
			yield break;
		}
		readingSchool = true;
		SchoolLinesShown = 0;
		SchoolLinesTotal = 0;
		SchoolExprTrace = "";
		StoryStep storyStep = Step("S4_read");
		LetterData letter = FindLetter((storyStep != null) ? storyStep.letter : "");
		if (letter == null)
		{
			readingSchool = false;
			FinishSchoolRead();
			yield break;
		}
		yield return new WaitForSecondsRealtime(1.4f);
		if (dog != null)
		{
			dog.Follow();
		}
		string[] lines = SplitLines(letter.body);
		SchoolLinesTotal = lines.Length;
		letterLock = ((gate != null) ? gate.Acquire("schoolread") : null);
		if (letterLock != null && player != null)
		{
			player.locked = true;
			player.SetStoryCrouch(crouch: true);
		}
		string shown = "";
		for (int i = 0; i < lines.Length; i++)
		{
			shown = ((i == 0) ? lines[i] : (shown + "\n" + lines[i]));
			ExprId expression = ((i < 2) ? ExprId.sad : ((i < 4) ? ExprId.curious : ((i != lines.Length - 1) ? ExprId.sad : ExprId.happy)));
			if (game != null)
			{
				game.SetExpression(expression);
			}
			SchoolExprTrace = SchoolExprTrace + ((i == 0) ? "" : ">") + expression;
			ui.ShowLetterDetail(letter.title, shown, (i == lines.Length - 1) ? letter.signature : "", letter.id);
			SchoolLinesShown = i + 1;
			while (ui.LetterOpening)
			{
				yield return null;
			}
			yield return new WaitForSecondsRealtime(1.15f);
			if (ui != null && !ui.LetterOpen)
			{
				break;
			}
		}
		readingSchool = false;
		if (gate != null && letterLock != null)
		{
			gate.Release(letterLock);
		}
		letterLock = null;
		if (player != null)
		{
			player.SetStoryCrouch(crouch: false);
			player.locked = false;
		}
		if (ui != null && ui.LetterOpen)
		{
			ui.CloseLetter();
		}
		FinishSchoolRead();
	}

	private void FinishSchoolRead()
	{
		if (State.flags.Contains("school_read"))
		{
			ApplyStepGoal();
			return;
		}
		State.flags.Add("school_read");
		TownVoiceover.Play("vo_stamp_school");
		CollectPostmark("school");
		State.carrying = "";
		store.Save(State);
		if (water != null)
		{
			water.SetLevel("L2", 4f);
		}
		if (dog != null)
		{
			dog.Follow();
		}
		if (game != null)
		{
			game.SetExpression(ExprId.happy);
		}
		else if (player != null)
		{
			player.PlayEmote("hop");
		}
		ui.SetCarried("");
		ui.ShowToast("小黑摇着尾巴跟上了你的脚步");
		if (State.step == "S4_read")
		{
			AdvanceTo("L0_cross");
		}
		else
		{
			ApplyStepGoal();
		}
	}

	private static string[] SplitLines(string body)
	{
		List<string> list = new List<string>();
		string[] array = body.Split('\n');
		foreach (string text in array)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				list.Add(text.Trim());
			}
		}
		return list.ToArray();
	}

	private void Mark(string s)
	{
		EndingDebug = EndingDebug + s + "[" + Time.realtimeSinceStartup.ToString("F1") + "];";
	}
}
