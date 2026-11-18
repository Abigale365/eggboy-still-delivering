using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class EggboyPlayer : MonoBehaviour
{
	public enum CarryPose
	{
		None,
		Wood,
		Oil,
		Ball
	}

	private class CarryTarget
	{
		public string bone;

		public Quaternion rot;
	}

	private const float Walk = 0.65f;

	private const float Run = 2f;

	private const float Radius = 0.42f;

	private const float JumpStand = 0.42f;

	private const float JumpMove = 0.38f;

	private const float GUp = 14f;

	private const float GDown = 21f;

	private const float ApexSlow = 0.55f;

	private const float CrouchStand = 0.13f;

	private const float CrouchMove = 0.06f;

	private const float LandStand = 0.32f;

	private const float LandMove = 0.16f;

	private const float Step = 0.22f;

	private const float PlatR = 0.3f;

	private const float JumpBuffer = 0.12f;

	private const float CoyoteTime = 0.1f;

	private float jumpBufferLeft;

	private float coyoteLeft;

	private bool airborneFromJump;

	private const float AirControl = 0.35f;

	private static readonly Dictionary<string, int> Repeat = new Dictionary<string, int>
	{
		{ "wave", 2 },
		{ "nod", 1 },
		{ "shake", 1 },
		{ "hop", 1 },
		{ "give", 1 },
		{ "look", 1 },
		{
			"sad",
			int.MaxValue
		}
	};

	public IEggboyWorld world;

	public Vector3 vel;

	public float yaw;

	public bool locked;

	public float idle;

	public bool grounded = true;

	public float speedScale = 1f;

	public Vector3 camFocus;

	public JumpState jump = new JumpState();

	public EggboyPose pose;

	public EggboyGait gait;

	public EggboyAntiClip antiClip;

	public bool antiClipOn = true;

	public bool disableAnim;

	public bool debugPose;

	public Action<string> onEmote;

	public Action onEmoteEnd;

	private Animation anim;

	private readonly Dictionary<string, AnimationState> clips = new Dictionary<string, AnimationState>();

	private string loco = "idle";

	private string emote;

	private string currentClip;

	private float emoteEnd;

	private float camY;

	private Vector3 prevVel;

	private Vector3 acc;

	private float accYJump;

	private float debugT;

	private Transform modelT;

	private bool storyCrouch;

	private float storyCrouchWeight;

	private readonly List<EggboyCollider> nearbyColliders = new List<EggboyCollider>(32);

	private CarryPose carry;

	private float carryWeight;

	private CarryTarget[] carryTargets;

	private float actionLockUntil;

	private string actionClip;

	private Action pendingGrab;

	private Action pendingComplete;

	private float grabAt;

	private float completeAt;

	public Vector3 SpawnPos
	{
		get
		{
			if (world == null)
			{
				return Vector3.zero;
			}
			return world.SpawnPos;
		}
	}

	public bool TryingToMove { get; private set; }

	public string CurrentEmote => emote;

	public bool Emoting => emote != null;

	public GameObject ModelObject { get; private set; }

	public EggboySkin Skin { get; private set; }

	public EggboyBones Bones { get; private set; }

	public bool IsStoryCrouched => storyCrouchWeight > 0.9f;

	public Transform CarryAnchor { get; private set; }

	public CarryPose Carry => carry;

	public bool ActionLocked => Time.time < actionLockUntil;

	public string CurrentAction => actionClip;

	public float Speed => Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);

	public void ResetJumpInput()
	{
		jumpBufferLeft = (coyoteLeft = 0f);
		airborneFromJump = false;
	}

	public bool HasStandingClearance(Vector3 p)
	{
		if (world == null)
		{
			return true;
		}
		world.QueryColliders(p.x, p.z, nearbyColliders);
		foreach (EggboyCollider nearbyCollider in nearbyColliders)
		{
			if (!nearbyCollider.water && (!(nearbyCollider.minY > 0f) || !(p.y < nearbyCollider.minY - 0.03f)) && (!nearbyCollider.top.HasValue || (!(p.y >= nearbyCollider.top.Value - 0.03f) && !(nearbyCollider.top.Value - p.y <= 0.22f))) && CollisionDepth(nearbyCollider, p.x, p.z, nearbyCollider.top.HasValue ? 0.3f : 0.42f) > 0.008f)
			{
				return false;
			}
		}
		return true;
	}

	public void SyncAfterSafeReturn()
	{
		vel = Vector3.zero;
		jump.vy = 0f;
		SetJump(JumpPhase.none);
		grounded = true;
		ResetJumpInput();
	}

	public void SetStoryCrouch(bool crouch)
	{
		storyCrouch = crouch;
	}

	public bool HasClip(string name)
	{
		return clips.ContainsKey(name);
	}

	public bool PlayInteractAction(string clip, float lockSeconds = -1f, float grabAtSeconds = -1f, Action onGrab = null, Action onComplete = null)
	{
		FlushPendingAction();
		if (!clips.TryGetValue(clip, out var value))
		{
			Debug.LogWarning("模型里没有互动动作 " + clip + "，直接执行回调");
			onGrab?.Invoke();
			onComplete?.Invoke();
			return false;
		}
		PlayEmote(clip);
		actionClip = clip;
		float length = value.length;
		actionLockUntil = Time.time + ((lockSeconds < 0f) ? length : lockSeconds);
		if (grounded)
		{
			vel.x = 0f;
			vel.z = 0f;
		}
		pendingGrab = onGrab;
		grabAt = Time.time + Mathf.Max(0f, grabAtSeconds);
		pendingComplete = onComplete;
		completeAt = Time.time + length;
		return true;
	}

	public void HoldActionLock(float seconds = 0.1f)
	{
		actionLockUntil = Mathf.Max(actionLockUntil, Time.time + seconds);
		if (grounded)
		{
			vel.x = 0f;
			vel.z = 0f;
		}
	}

	public void StopEmote(string name)
	{
		if (emote != null && emote == name)
		{
			EndEmote();
		}
	}

	private void FlushPendingAction()
	{
		Action action = pendingGrab;
		pendingGrab = null;
		Action action2 = pendingComplete;
		pendingComplete = null;
		action?.Invoke();
		action2?.Invoke();
	}

	private void TickActions()
	{
		if (pendingGrab != null && Time.time >= grabAt)
		{
			Action action = pendingGrab;
			pendingGrab = null;
			action();
		}
		if (pendingComplete != null && Time.time >= completeAt)
		{
			Action action2 = pendingComplete;
			pendingComplete = null;
			action2();
		}
	}

	public void SetCarryPose(CarryPose p)
	{
		if (carry != p)
		{
			carry = p;
			carryTargets = BuildCarryTargets(p);
		}
	}

	private CarryTarget[] BuildCarryTargets(CarryPose p)
	{
		if (Bones == null || p == CarryPose.None)
		{
			return null;
		}
		bool flag = true;
		float r2;
		float r3;
		float r4;
		float r5;
		float r9;
		float r8;
		float r7;
		float r6;
		switch (p)
		{
		case CarryPose.Wood:
			r2 = 14f;
			r3 = 52f;
			r4 = 0f;
			r5 = 42f;
			r9 = 14f;
			r8 = 52f;
			r7 = 0f;
			r6 = 42f;
			break;
		case CarryPose.Ball:
			r2 = 8f;
			r3 = 46f;
			r4 = 0f;
			r5 = 48f;
			r9 = 8f;
			r8 = 46f;
			r7 = 0f;
			r6 = 48f;
			break;
		case CarryPose.Oil:
			r2 = -12f;
			r3 = 14f;
			r4 = 0f;
			r5 = 4f;
			r9 = (r8 = (r7 = (r6 = 0f)));
			flag = false;
			break;
		default:
			return null;
		}
		List<CarryTarget> list = new List<CarryTarget>();
		float d = MathF.PI / 180f;
		Target("armR", r3, r2, right: true);
		Target("handR", r5, r4, right: true);
		if (flag)
		{
			Target("armL", r8, r9, right: false);
			Target("handL", r6, r7, right: false);
		}
		return list.ToArray();
		void Target(string bone, float r1, float r0, bool right)
		{
			EggboyBones.Entry entry = Bones.Get(bone);
			if (entry != null)
			{
				Quaternion localRotation = entry.bone.localRotation;
				entry.bone.localRotation = entry.bindLocalRotation;
				Bones.Rotate(bone, Vector3.up, (right ? r1 : (0f - r1)) * d);
				Bones.Rotate(bone, Vector3.forward, (right ? (0f - r0) : r0) * d);
				list.Add(new CarryTarget
				{
					bone = bone,
					rot = entry.bone.localRotation
				});
				entry.bone.localRotation = localRotation;
			}
		}
	}

	private void ApplyCarryOverlay(float dt)
	{
		bool flag = carry != 0 && carryTargets != null && (emote == null || emote != actionClip);
		carryWeight = Mathf.MoveTowards(carryWeight, flag ? 1f : 0f, dt * 8f);
		if (carryWeight <= 0f || carryTargets == null || Bones == null)
		{
			return;
		}
		float t = ((carry == CarryPose.Oil) ? (carryWeight * 0.8f) : carryWeight);
		CarryTarget[] array = carryTargets;
		foreach (CarryTarget carryTarget in array)
		{
			EggboyBones.Entry entry = Bones.Get(carryTarget.bone);
			if (entry != null)
			{
				entry.bone.localRotation = Quaternion.Slerp(entry.bone.localRotation, carryTarget.rot, t);
			}
		}
		if (carry == CarryPose.Oil && gait != null)
		{
			gait.Grip(LegSide.R, 0.85f * carryWeight);
		}
	}

	private void CreateCarryAnchor()
	{
		Transform transform = ((Bones != null) ? Bones.Bone("body") : null);
		Transform transform2 = new GameObject("CarryAnchor").transform;
		transform2.SetParent(base.transform, worldPositionStays: false);
		transform2.localPosition = new Vector3(0f, 0.46f, 0.28f);
		transform2.localRotation = Quaternion.identity;
		if (transform != null)
		{
			transform2.SetParent(transform, worldPositionStays: true);
		}
		CarryAnchor = transform2;
	}

	public void Setup(GameObject model, IEggboyWorld w, float modelYawDegrees)
	{
		world = w;
		ModelObject = model;
		modelT = model.transform;
		modelT.SetParent(base.transform, worldPositionStays: false);
		modelT.localRotation = Quaternion.Euler(0f, modelYawDegrees, 0f);
		modelT.localScale = Vector3.one * 0.85f;
		SkinnedMeshRenderer[] componentsInChildren = model.GetComponentsInChildren<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer obj in componentsInChildren)
		{
			obj.updateWhenOffscreen = true;
			obj.shadowCastingMode = ShadowCastingMode.On;
			obj.receiveShadows = true;
		}
		anim = model.GetComponentInChildren<Animation>();
		if (anim == null)
		{
			Debug.LogError("模型里没有 Animation 组件");
			base.enabled = false;
			return;
		}
		anim.Stop();
		SkinnedMeshRenderer componentInChildren = model.GetComponentInChildren<SkinnedMeshRenderer>();
		if (componentInChildren == null)
		{
			Debug.LogError("模型里没有 SkinnedMeshRenderer");
			base.enabled = false;
			return;
		}
		EggboySkin eggboySkin = new EggboySkin(model, componentInChildren);
		EggboyBones bones = new EggboyBones(model, componentInChildren);
		Skin = eggboySkin;
		Bones = bones;
		pose = new EggboyPose(bones);
		gait = new EggboyGait(bones, eggboySkin, modelT);
		antiClip = new EggboyAntiClip(eggboySkin, bones, modelT);
		CreateCarryAnchor();
		Debug.Log($"EGGBOY skin: shell {eggboySkin.shellVerts.Length}, leather {eggboySkin.leatherVerts.Length}, visor {eggboySkin.visorVerts.Length}; " + $"gait {gait.Ok}, antiClip {antiClip.ok}, bones {componentInChildren.bones.Length}");
		foreach (AnimationState item in anim)
		{
			string key = item.name.Substring(item.name.LastIndexOf('|') + 1);
			clips[key] = item;
		}
		AnimationClip[] array = Resources.LoadAll<AnimationClip>("Animations");
		foreach (AnimationClip animationClip in array)
		{
			if (!(animationClip == null) && !clips.ContainsKey(animationClip.name))
			{
				animationClip.legacy = true;
				anim.AddClip(animationClip, animationClip.name);
				clips[animationClip.name] = anim[animationClip.name];
			}
		}
		string[] array2 = new string[3] { "idle", "walk", "run" };
		foreach (string key2 in array2)
		{
			if (clips.TryGetValue(key2, out var value))
			{
				value.wrapMode = WrapMode.Loop;
			}
		}
		if (disableAnim)
		{
			anim.Stop();
		}
		else
		{
			Play("idle", 0f);
		}
	}

	private void Play(string name, float fade)
	{
		if (!clips.TryGetValue(name, out var value))
		{
			Debug.LogError("模型里没有动作 " + name);
			return;
		}
		if (fade > 0f)
		{
			anim.CrossFade(value.name, fade);
		}
		else
		{
			anim.Play(value.name);
		}
		currentClip = name;
	}

	public void PlayEmote(string name, int repeat = 1)
	{
		if (!clips.TryGetValue(name, out var value))
		{
			Debug.LogError("模型里没有动作 " + name);
			return;
		}
		value.wrapMode = ((repeat > 1) ? WrapMode.Loop : WrapMode.ClampForever);
		value.time = 0f;
		emote = name;
		emoteEnd = ((repeat == int.MaxValue) ? float.PositiveInfinity : (Time.time + value.length * (float)repeat));
		Play(name, 0.18f);
		idle = 0f;
		onEmote?.Invoke(name);
	}

	private void EndEmote()
	{
		emote = null;
		actionClip = null;
		Play(loco, 0.3f);
		onEmoteEnd?.Invoke();
	}

	public void FaceTowards(Vector3 p)
	{
		yaw = Mathf.Atan2(p.x - base.transform.position.x, p.z - base.transform.position.z);
	}

	private void SetJump(JumpPhase phase, float dur = 0f)
	{
		jump.phase = phase;
		jump.t = 0f;
		jump.dur = dur;
	}

	private bool CanUseCollisionPosition(Vector3 from, float x, float z)
	{
		if (!(world is IslandWorld islandWorld))
		{
			return world.Walkable(x, z);
		}
		if (!islandWorld.CanTraverse(from, x, z, jump.phase == JumpPhase.air))
		{
			return false;
		}
		world.QueryColliders(x, z, nearbyColliders);
		foreach (EggboyCollider nearbyCollider in nearbyColliders)
		{
			if (!nearbyCollider.water && (!(nearbyCollider.minY > 0f) || !(from.y < nearbyCollider.minY - 0.03f)) && (!nearbyCollider.top.HasValue || (!(from.y >= nearbyCollider.top.Value - 0.03f) && (!grounded || !(nearbyCollider.top.Value - from.y <= 0.22f)))))
			{
				float radius = (nearbyCollider.top.HasValue ? 0.3f : 0.42f);
				float num = CollisionDepth(nearbyCollider, x, z, radius);
				if (num > 0.008f && num > CollisionDepth(nearbyCollider, from.x, from.z, radius) + 0.0005f)
				{
					return false;
				}
			}
		}
		return true;
	}

	private static float CollisionDepth(EggboyCollider collider, float x, float z, float radius)
	{
		float num = x - collider.x;
		float num2 = z - collider.z;
		if (collider.r.HasValue)
		{
			return collider.r.Value + radius - Mathf.Sqrt(num * num + num2 * num2);
		}
		if (collider.hw.HasValue && collider.hd.HasValue)
		{
			return Mathf.Min(collider.hw.Value + radius - Mathf.Abs(num), collider.hd.Value + radius - Mathf.Abs(num2));
		}
		return 0f;
	}

	public void Move(float dt, float inX, float inZ, bool run, bool jumpPressed)
	{
		if (world == null || gait == null)
		{
			return;
		}
		if (Bones != null)
		{
			if (disableAnim)
			{
				Bones.RestoreBindPose();
			}
			else
			{
				Bones.RestoreClean();
			}
		}
		TickActions();
		float num = Mathf.Sqrt(inX * inX + inZ * inZ);
		bool flag = locked || ActionLocked;
		if (flag)
		{
			jumpBufferLeft = 0f;
		}
		else
		{
			jumpBufferLeft = (jumpPressed ? 0.12f : Mathf.Max(0f, jumpBufferLeft - dt));
		}
		if (grounded && jump.phase != JumpPhase.crouch)
		{
			coyoteLeft = 0.1f;
			airborneFromJump = false;
		}
		else
		{
			coyoteLeft = Mathf.Max(0f, coyoteLeft - dt);
		}
		bool flag3 = (TryingToMove = num > 0.1f && !flag);
		float num2 = (flag3 ? ((run ? 2f : 0.65f) * speedScale) : 0f);
		float num3 = (flag3 ? (inX / num) : 0f);
		float num4 = (flag3 ? (inZ / num) : 0f);
		JumpState jumpState = jump;
		jumpState.t += dt;
		bool flag4 = jumpState.phase == JumpPhase.crouch && !jumpState.moving;
		float num5 = (flag4 ? Mathf.Min(1f, dt * 20f) : (Mathf.Min(1f, dt * (flag3 ? 8f : 10f)) * (grounded ? 1f : 0.35f)));
		vel.x += ((flag4 ? 0f : (num3 * num2)) - vel.x) * num5;
		vel.z += ((flag4 ? 0f : (num4 * num2)) - vel.z) * num5;
		bool flag5 = !grounded && !airborneFromJump && coyoteLeft > 0f;
		if (jumpBufferLeft > 0f && !flag && (grounded || flag5) && (jumpState.phase == JumpPhase.none || jumpState.phase == JumpPhase.land || (flag5 && jumpState.phase == JumpPhase.air)))
		{
			jumpBufferLeft = (coyoteLeft = 0f);
			airborneFromJump = true;
			FlushPendingAction();
			if (emote != null)
			{
				EndEmote();
			}
			float num6 = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
			jumpState.moving = num6 > 0.3f;
			jumpState.run = Mathf.Clamp01((num6 - 0.65f) / 1.35f);
			FootState footState = gait.feet[0];
			FootState footState2 = gait.feet[1];
			jumpState.lead = ((footState.stance && !footState2.stance) ? LegSide.R : ((!footState2.stance || footState.stance) ? ((footState.fwd < footState2.fwd) ? LegSide.R : LegSide.L) : LegSide.L));
			SetJump(JumpPhase.crouch, jumpState.moving ? 0.06f : 0.13f);
		}
		if (flag3 && emote != null && emote != "hop")
		{
			FlushPendingAction();
			EndEmote();
		}
		Vector3 position = base.transform.position;
		float num7 = position.x + vel.x * dt;
		float num8 = position.z + vel.z * dt;
		world.QueryColliders(num7, num8, nearbyColliders);
		for (int i = 0; i < nearbyColliders.Count; i++)
		{
			EggboyCollider eggboyCollider = nearbyColliders[i];
			if (eggboyCollider.water)
			{
				if (!(position.y > 0.05f))
				{
					float num9 = eggboyCollider.r.GetValueOrDefault() + 0.08f;
					float num10 = Mathf.Sqrt((num7 - eggboyCollider.x) * (num7 - eggboyCollider.x) + (num8 - eggboyCollider.z) * (num8 - eggboyCollider.z));
					float num11 = Mathf.Sqrt((position.x - eggboyCollider.x) * (position.x - eggboyCollider.x) + (position.z - eggboyCollider.z) * (position.z - eggboyCollider.z));
					if (num10 < num9 && num11 >= num9 - 0.001f && num10 > 1E-06f)
					{
						num7 = eggboyCollider.x + (num7 - eggboyCollider.x) / num10 * num9;
						num8 = eggboyCollider.z + (num8 - eggboyCollider.z) / num10 * num9;
					}
				}
				continue;
			}
			float num12 = 0.42f;
			if (eggboyCollider.minY > 0f && position.y < eggboyCollider.minY - 0.03f)
			{
				continue;
			}
			if (eggboyCollider.top.HasValue)
			{
				if (position.y >= eggboyCollider.top.Value - 0.03f || (grounded && eggboyCollider.top.Value - position.y <= 0.22f))
				{
					continue;
				}
				num12 = 0.3f;
			}
			if (eggboyCollider.r.HasValue)
			{
				float num13 = num7 - eggboyCollider.x;
				float num14 = num8 - eggboyCollider.z;
				float num15 = Mathf.Sqrt(num13 * num13 + num14 * num14);
				float num16 = eggboyCollider.r.Value + num12;
				if (num15 < num16 && num15 > 1E-06f)
				{
					num7 = eggboyCollider.x + num13 / num15 * num16;
					num8 = eggboyCollider.z + num14 / num15 * num16;
				}
			}
			else
			{
				if (!eggboyCollider.hw.HasValue || !eggboyCollider.hd.HasValue)
				{
					continue;
				}
				float num17 = eggboyCollider.hw.Value + num12;
				float num18 = eggboyCollider.hd.Value + num12;
				float f = num7 - eggboyCollider.x;
				float f2 = num8 - eggboyCollider.z;
				if (Mathf.Abs(f) < num17 && Mathf.Abs(f2) < num18)
				{
					if (num17 - Mathf.Abs(f) < num18 - Mathf.Abs(f2))
					{
						num7 = eggboyCollider.x + Mathf.Sign(f) * num17;
					}
					else
					{
						num8 = eggboyCollider.z + Mathf.Sign(f2) * num18;
					}
				}
			}
		}
		if (CanUseCollisionPosition(position, num7, num8))
		{
			position.x = num7;
			position.z = num8;
		}
		else if (CanUseCollisionPosition(position, num7, position.z))
		{
			position.x = num7;
			vel.z = 0f;
		}
		else if (CanUseCollisionPosition(position, position.x, num8))
		{
			position.z = num8;
			vel.x = 0f;
		}
		else
		{
			vel = Vector3.zero;
		}
		float num19 = world.GroundAt(position.x, position.z, position.y);
		float vy = jumpState.vy;
		if (jumpState.phase == JumpPhase.crouch && jumpState.t >= jumpState.dur)
		{
			coyoteLeft = 0f;
			airborneFromJump = true;
			jumpState.vy = (jumpState.vy0 = Mathf.Sqrt(28f * (jumpState.moving ? 0.38f : 0.42f)));
			grounded = false;
			SetJump(JumpPhase.air);
		}
		else if (grounded && num19 < position.y - 0.22f)
		{
			airborneFromJump = false;
			jumpState.vy = (jumpState.vy0 = 0f);
			jumpState.moving = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z) > 0.3f;
			jumpState.run = Mathf.Clamp01((Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z) - 0.65f) / 1.35f);
			grounded = false;
			SetJump(JumpPhase.air);
		}
		if (!grounded)
		{
			float num20 = ((jumpState.vy > 0f) ? 14f : 21f) * ((Mathf.Abs(jumpState.vy) < 0.7f) ? 0.55f : 1f);
			jumpState.vy -= num20 * dt;
			position.y += jumpState.vy * dt;
			if (position.y <= num19 && jumpState.vy <= 0f)
			{
				jumpState.impact = 0f - jumpState.vy;
				jumpState.vy = 0f;
				position.y = num19;
				grounded = true;
				SetJump(JumpPhase.land, jumpState.moving ? 0.16f : 0.32f);
			}
		}
		else
		{
			position.y += (num19 - position.y) * Mathf.Min(1f, dt * 25f);
			jumpState.vy = 0f;
			if (jumpState.phase == JumpPhase.land && jumpState.t >= jumpState.dur)
			{
				SetJump(JumpPhase.none);
			}
		}
		accYJump = ((dt > 0f) ? ((jumpState.vy - vy) / dt) : 0f);
		base.transform.position = position;
		float num21 = (grounded ? position.y : (camY + (position.y - camY) * 0.35f));
		camY += (num21 - camY) * Mathf.Min(1f, dt * (grounded ? 8f : 3f));
		camFocus = new Vector3(position.x, camY, position.z);
		float num22 = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
		if (num22 > 0.15f)
		{
			yaw = Mathf.Atan2(vel.x, vel.z);
		}
		float num23 = base.transform.eulerAngles.y * (MathF.PI / 180f);
		float num24 = Mathf.Atan2(Mathf.Sin(yaw - num23), Mathf.Cos(yaw - num23));
		base.transform.rotation = Quaternion.Euler(0f, (num23 + num24 * Mathf.Min(1f, dt * 12f)) * 57.29578f, 0f);
		idle = ((num22 < 0.12f && emote == null && jumpState.phase == JumpPhase.none) ? (idle + dt) : 0f);
		loco = "idle";
		if (!disableAnim && emote == null && currentClip != loco)
		{
			Play(loco, 0.22f);
		}
		if (emote != null && Time.time >= emoteEnd)
		{
			EndEmote();
		}
		if (dt > 0f)
		{
			Vector3 b = (vel - prevVel) / dt;
			acc = Vector3.Lerp(acc, b, Mathf.Min(1f, dt * 12f));
			prevVel = vel;
		}
	}

	public void PuppetStep(Vector3 pos, float yawRad, Vector3 velocity, bool isGrounded, float dt)
	{
		if (Bones != null)
		{
			if (disableAnim)
			{
				Bones.RestoreBindPose();
			}
			else
			{
				Bones.RestoreClean();
			}
		}
		base.transform.position = pos;
		yaw = yawRad;
		base.transform.rotation = Quaternion.Euler(0f, yawRad * 57.29578f, 0f);
		vel = velocity;
		grounded = isGrounded;
		if (dt > 0f)
		{
			Vector3 b = (vel - prevVel) / dt;
			acc = Vector3.Lerp(acc, b, Mathf.Min(1f, dt * 12f));
			prevVel = vel;
		}
		if (emote != null && Time.time >= emoteEnd)
		{
			EndEmote();
		}
	}

	private void LateUpdate()
	{
		if (pose == null || gait == null)
		{
			return;
		}
		float num = Mathf.Min(0.05f, Time.deltaTime);
		if (!(num <= 0f))
		{
			if (!disableAnim && Bones != null)
			{
				Bones.CaptureClean();
			}
			float speed = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
			float f = base.transform.eulerAngles.y * (MathF.PI / 180f);
			float accF = acc.x * Mathf.Sin(f) + acc.z * Mathf.Cos(f);
			float accL = acc.x * Mathf.Cos(f) - acc.z * Mathf.Sin(f);
			gait.Update(num, new GaitContext
			{
				speed = speed,
				accF = accF,
				accL = accL,
				emoting = (emote != null),
				jump = jump
			});
			pose.Update(num, new PoseContext
			{
				speed = speed,
				accF = accF,
				accL = accL,
				accY = gait.accY + accYJump,
				emoting = (emote != null),
				jump = gait.jw
			});
			storyCrouchWeight = Mathf.MoveTowards(storyCrouchWeight, storyCrouch ? 1f : 0f, num * 5f);
			if (storyCrouchWeight > 0f && Bones != null)
			{
				float num2 = 0.35f * storyCrouchWeight;
				Bones.Move("body", 0f, -0.055f * storyCrouchWeight, 0f);
				Bones.Rotate("body", Vector3.right, 0.23f * storyCrouchWeight);
				Bones.Rotate("legL", Vector3.right, 0f - num2);
				Bones.Rotate("legR", Vector3.right, 0f - num2);
				Bones.Rotate("footL", Vector3.right, num2);
				Bones.Rotate("footR", Vector3.right, num2);
			}
			TickActions();
			ApplyCarryOverlay(num);
			if (antiClipOn && antiClip != null)
			{
				antiClip.Solve(num, 1f + 2f * gait.jw);
			}
			if (debugPose && Time.time - debugT > 1f)
			{
				debugT = Time.time;
				Transform transform = ((Bones != null) ? Bones.Bone("root") : null);
				Transform transform2 = ((Bones != null) ? Bones.Bone("body") : null);
				Transform transform3 = ((Bones != null) ? Bones.Bone("armR") : null);
				Transform transform4 = ((Bones != null) ? Bones.Bone("handR") : null);
				Debug.Log("POSE rootL " + ((transform != null) ? transform.localRotation.eulerAngles.ToString() : "-") + " rootW " + ((transform != null) ? transform.rotation.eulerAngles.ToString() : "-") + " bodyL " + ((transform2 != null) ? transform2.localRotation.eulerAngles.ToString() : "-") + " bodyW " + ((transform2 != null) ? transform2.rotation.eulerAngles.ToString() : "-") + " " + string.Format("bodyPos {0} armR {1} handR {2} expr {3} ", (transform2 != null) ? transform2.position.ToString() : "-", (transform3 != null) ? transform3.position.ToString() : "-", (transform4 != null) ? transform4.position.ToString() : "-", pose.expr) + $"modelL {modelT.localRotation.eulerAngles} player {base.transform.eulerAngles} " + $"gait w={gait.w:F2} amp={gait.amp:F2} phase={gait.phase:F2} jw={gait.jw:F2} speed={Speed:F2} bounds {Skin.renderer.bounds} " + "anti " + antiClip.Measure());
			}
		}
	}
}
