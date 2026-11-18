using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class PhysicalPickupController : MonoBehaviour
{
	public sealed class Job
	{
		public string clip;

		public Func<Vector3> target;

		public Transform item;

		public float diameter = 0.14f;

		public float standDistance = 0.4f;

		public Vector3 standOffset;

		public float contactTime = 0.4f;

		public float tolerance = 0.03f;

		public float lockSeconds = -1f;

		public bool reach = true;

		public bool liftBall;

		public bool putBall;

		public Action onContact;

		public Action onDone;
	}

	private enum Phase
	{
		Idle,
		Approach,
		Act
	}

	private EggboyPlayer player;

	private IslandWorld island;

	private StoryDirector director;

	private PickupGrip grip;

	private Animation anim;

	private Phase phase;

	private Job job;

	private Vector3 stand;

	private float standYaw;

	private float phaseStart;

	private bool contacted;

	private Vector3 itemStart;

	private Vector3 liftFrom;

	private Vector3 dropFrom;

	private float releasedAt;

	private SchoolBall heldBall;

	private readonly List<EggboyCollider> near = new List<EggboyCollider>(32);

	public static bool Trace;

	private float lastError = 9f;

	public static PhysicalPickupController Instance { get; private set; }

	public float ContactError { get; private set; }

	public float ContactForward { get; private set; }

	public float LastShellGap { get; private set; }

	public string LastEvent { get; private set; } = "";


	public bool Busy => phase != Phase.Idle;

	public PickupGrip Grip => grip;

	public bool HoldsBall => heldBall != null;

	public SchoolBall HeldBall => heldBall;

	public void Init(EggboyPlayer p, IslandWorld w, StoryDirector d)
	{
		Instance = this;
		player = p;
		island = w;
		director = d;
		if (player != null && player.Bones != null && player.Skin != null)
		{
			grip = new PickupGrip(player.Bones, player.Skin);
		}
		anim = ((player != null && player.ModelObject != null) ? player.ModelObject.GetComponentInChildren<Animation>() : null);
		if (anim != null)
		{
			anim.cullingType = AnimationCullingType.AlwaysAnimate;
		}
		if (grip == null || !grip.Ok)
		{
			Debug.LogWarning("PICKUP 右手 / 身体骨骼不全，真实拾取停用");
		}
	}

	public bool Active(string clip)
	{
		if (grip != null && grip.Ok && player != null && island != null && director != null && !director.IsAuditMode)
		{
			if (!string.IsNullOrEmpty(clip))
			{
				return player.HasClip(clip);
			}
			return true;
		}
		return false;
	}

	public bool Begin(Job j)
	{
		if (j == null || Busy || !Active(j.clip))
		{
			return false;
		}
		job = j;
		contacted = false;
		Vector3 vector = j.target();
		if (!FindStand(vector, j, out stand, out standYaw))
		{
			Debug.Log($"PICKUP_NOSTAND {j.clip} target=({vector.x:F2},{vector.z:F2}) from=({player.transform.position.x:F2},{player.transform.position.z:F2})");
			stand = player.transform.position;
			standYaw = YawTo(player.transform.position, vector);
			if (j.standOffset != Vector3.zero)
			{
				standYaw -= Mathf.Atan2(j.standOffset.x, j.standOffset.z) * 57.29578f;
			}
		}
		phase = Phase.Approach;
		phaseStart = Time.time;
		player.HoldActionLock(0.2f);
		LastEvent = $"approach {j.clip} stand=({stand.x:F2},{stand.z:F2}) yaw={standYaw:F0}";
		Debug.Log("PICKUP_BEGIN " + LastEvent);
		return true;
	}

	public void AttachCarryBall(SchoolBall ball)
	{
		if (!(ball == null))
		{
			heldBall = ball;
			ball.transform.SetParent(null, worldPositionStays: true);
			if (player != null)
			{
				player.SetCarryPose(EggboyPlayer.CarryPose.None);
			}
			if (grip != null && grip.Ok)
			{
				ball.transform.position = grip.CarryCenter;
			}
		}
	}

	public void ReleaseCarryBall(SchoolBall ball = null)
	{
		if (!(heldBall == null) && (!(ball != null) || !(ball != heldBall)))
		{
			heldBall = null;
			if (phase == Phase.Act && job != null && job.putBall && !contacted)
			{
				phase = Phase.Idle;
				job = null;
			}
		}
	}

	private void RestorePose()
	{
		if (player.Bones != null)
		{
			if (player.disableAnim)
			{
				player.Bones.RestoreBindPose();
			}
			else
			{
				player.Bones.RestoreClean();
			}
		}
	}

	private void Update()
	{
		if (player == null)
		{
			return;
		}
		if (phase == Phase.Act || heldBall != null)
		{
			RestorePose();
		}
		if (phase != Phase.Approach)
		{
			return;
		}
		float num = Mathf.Min(0.05f, Time.deltaTime);
		player.HoldActionLock(0.15f);
		Vector3 position = player.transform.position;
		float num2 = Mathf.MoveTowardsAngle(player.transform.eulerAngles.y, standYaw, num * 540f);
		bool num3 = Mathf.Abs(Mathf.DeltaAngle(num2, standYaw)) <= 0.5f;
		Vector3 vector = Flat(position);
		Vector3 vector2 = Flat(stand);
		float num4 = Vector3.Distance(vector, vector2);
		bool flag = Time.time - phaseStart > 4.5f;
		if (!num3 && num4 > 0.05f && !flag)
		{
			player.PuppetStep(position, num2 * (MathF.PI / 180f), Vector3.zero, isGrounded: true, num);
			return;
		}
		float num5 = Mathf.Clamp(num4 * 5f, 0.2f, 0.65f);
		Vector3 vector3 = Vector3.MoveTowards(vector, vector2, num * num5);
		float num6 = island.GroundAt(vector3.x, vector3.z, position.y);
		if ((!(num4 <= 0.0005f) && (!island.CanTraverse(position, vector3.x, vector3.z, jumping: false) || !(Mathf.Abs(num6 - position.y) < 0.25f) || (Blocked(vector3, num6) && (!Blocked(position, position.y) || !(Clearance(vector3, num6) >= Clearance(position, position.y) - 0.001f))))) || flag)
		{
			Debug.Log(string.Format("PICKUP_STOP {0} at=({1:F2},{2:F2},{3:F2}) next=({4:F2},{5:F2},{6:F2}) traverse={7} blocked={8}", flag ? "timeout" : "blocked", position.x, position.y, position.z, vector3.x, num6, vector3.z, island.CanTraverse(position, vector3.x, vector3.z, jumping: false), Blocked(vector3, num6)));
			player.PuppetStep(position, standYaw * (MathF.PI / 180f), Vector3.zero, isGrounded: true, num);
			StartAct();
		}
		else
		{
			vector3.y = num6;
			bool num7 = Vector3.Distance(vector3, new Vector3(vector2.x, 0f, vector2.z) + Vector3.up * vector3.y) < 0.0005f;
			Vector3 velocity = ((num7 || num <= 0f) ? Vector3.zero : ((vector3 - position) / num));
			velocity.y = 0f;
			player.PuppetStep(vector3, num2 * (MathF.PI / 180f), velocity, isGrounded: true, num);
			if (num7 && Mathf.Abs(Mathf.DeltaAngle(num2, standYaw)) < 1f)
			{
				StartAct();
			}
		}
	}

	private void StartAct()
	{
		phase = Phase.Act;
		phaseStart = Time.time;
		contacted = false;
		lastError = 9f;
		player.PuppetStep(player.transform.position, standYaw * (MathF.PI / 180f), Vector3.zero, isGrounded: true, 0f);
		player.PlayInteractAction(job.clip, job.lockSeconds);
		if (job.liftBall)
		{
			itemStart = job.target();
		}
		LastEvent = $"act {job.clip} at=({player.transform.position.x:F2},{player.transform.position.z:F2})";
	}

	private void LateUpdate()
	{
		if (player == null || grip == null || !grip.Ok)
		{
			return;
		}
		float y = player.transform.position.y;
		if (phase == Phase.Act && job != null)
		{
			if (job.putBall)
			{
				UpdatePut(y);
			}
			else if (job.liftBall)
			{
				UpdateLift(y);
			}
			else
			{
				UpdateReach(y);
			}
		}
		else if (heldBall != null)
		{
			HoldAt(heldBall.transform, grip.CarryCenter, 1f, 1f);
		}
	}

	private float ClipTime()
	{
		return Time.time - phaseStart;
	}

	private float ClipLength()
	{
		AnimationState animationState = ((anim != null) ? anim[job.clip] : null);
		if (!(animationState != null))
		{
			return 0.9f;
		}
		return animationState.length;
	}

	private void UpdateLift(float groundY)
	{
		float num = ClipTime();
		float num2 = ClipLength();
		float weight = Ease(0.09f, 0.37f, num) * (1f - Ease(0.47f, 0.84f, num));
		grip.LeanForward(weight, groundY);
		Transform item = job.item;
		if (!contacted)
		{
			float num3 = Ease(0.09f, 0.38f, num);
			ContactError = grip.Reach(itemStart, num3, 0f, 0f, job.diameter);
			if (Trace)
			{
				Debug.Log($"PICKUP_TRACE t={num:F2} reach={num3:F2} err={ContactError:F3} palm={grip.PalmPosition:F3} item={itemStart:F3} body={grip.Body.position:F3} root={player.transform.position:F3}");
			}
			bool flag = num >= job.contactTime && ContactError <= job.tolerance && ContactError >= lastError - 0.0005f;
			bool flag2 = num >= job.contactTime + 0.22f;
			lastError = ContactError;
			if (num < job.contactTime || (ContactError > 0.012f && !flag && !flag2))
			{
				if (num >= num2)
				{
					FinishContact(forced: true);
				}
				return;
			}
			liftFrom = grip.Body.InverseTransformPoint(itemStart);
			ContactForward = Vector3.Dot(grip.PalmPosition - player.transform.position, player.transform.forward);
			SchoolBall schoolBall = ((item != null) ? item.GetComponent<SchoolBall>() : null);
			if (schoolBall != null)
			{
				heldBall = schoolBall;
				item.SetParent(null, worldPositionStays: true);
				player.SetCarryPose(EggboyPlayer.CarryPose.None);
			}
			FinishContact(flag2);
		}
		if (item == null)
		{
			if (num >= num2)
			{
				EndAct();
			}
			return;
		}
		float t = Ease(job.contactTime + 0.045f, 0.84f, num);
		Vector3 carryCenterLocal = grip.CarryCenterLocal;
		Vector3 position = Vector3.Slerp(Flat(liftFrom), Flat(carryCenterLocal), t);
		position.y = Mathf.Lerp(liftFrom.y, carryCenterLocal.y, t);
		Vector3 center = Vector3.Lerp(itemStart, grip.Body.TransformPoint(position), Ease(job.contactTime, 0.53f, num));
		HoldAt(item, center, Ease(0.53f, 0.8f, num), Ease(job.contactTime, 0.5f, num));
		if (num >= num2)
		{
			EndAct();
		}
	}

	private void UpdatePut(float groundY)
	{
		float num = ClipTime();
		float num2 = ClipLength();
		float num3 = Ease(0.06f, 0.42f, num) * (1f - Ease(0.49f, 0.77f, num));
		grip.LeanSide(num3 * 9f, groundY);
		Vector3 vector = job.target();
		if (!contacted)
		{
			if (heldBall != null)
			{
				Vector3 b = vector + Vector3.up * 0.205f;
				HoldAt(heldBall.transform, Vector3.Lerp(grip.CarryCenter, b, Ease(0.03f, 0.45f, num)), 1f - Ease(0.19f, 0.45f, num), 1f);
				dropFrom = heldBall.transform.position;
			}
			if (num >= job.contactTime || num >= num2)
			{
				FinishContact(forced: false);
				releasedAt = Time.time;
			}
		}
		else
		{
			grip.Reach(dropFrom, 1f - Ease(job.contactTime, 0.76f, num), 0f, 0f, job.diameter);
		}
		if (num >= num2)
		{
			EndAct();
		}
	}

	private void UpdateReach(float groundY)
	{
		float num = ClipTime();
		float num2 = ClipLength();
		if (!job.reach)
		{
			if (!contacted && (num >= job.contactTime || num >= num2))
			{
				FinishContact(forced: false);
			}
			if (num >= num2)
			{
				EndAct();
			}
			return;
		}
		float num3 = Ease(0.09f, 0.37f, num) * (1f - Ease(job.contactTime + 0.05f, job.contactTime + 0.4f, num));
		grip.LeanForward(num3 * 0.7f, groundY);
		Vector3 center = ReachablePoint(job.target(), groundY);
		float weight = (contacted ? (1f - Ease(job.contactTime, job.contactTime + 0.35f, num)) : Ease(0.09f, job.contactTime, num));
		ContactError = grip.Reach(center, weight, 0.3f, contacted ? 1f : Ease(job.contactTime - 0.1f, job.contactTime, num), job.diameter);
		bool flag = ContactError <= job.tolerance && ContactError >= lastError - 0.0005f;
		lastError = ContactError;
		if (!contacted && num >= job.contactTime && (ContactError <= 0.02f || flag || num >= job.contactTime + 0.15f || num >= num2))
		{
			ContactForward = Vector3.Dot(grip.PalmPosition - player.transform.position, player.transform.forward);
			FinishContact(ContactError > job.tolerance);
		}
		if (num >= num2)
		{
			EndAct();
		}
	}

	private Vector3 ReachablePoint(Vector3 target, float groundY)
	{
		Vector3 position = player.transform.position;
		Vector3 vector = Flat(target - position);
		float magnitude = vector.magnitude;
		if (magnitude > 0.38f)
		{
			vector = vector / magnitude * 0.38f;
		}
		float y = Mathf.Clamp(target.y, groundY + 0.16f, groundY + 0.42f);
		return new Vector3(position.x + vector.x, y, position.z + vector.z);
	}

	private void FinishContact(bool forced)
	{
		if (!contacted)
		{
			contacted = true;
			LastShellGap = ((heldBall != null) ? (grip.DistanceToShell(heldBall.transform.position) - job.diameter * 0.5f) : 0f);
			LastEvent = string.Format("contact {0} error={1:F4} forward={2:F3}{3}", job.clip, ContactError, ContactForward, forced ? " forced" : "");
			Debug.Log("PICKUP_CONTACT " + LastEvent);
			Action onContact = job.onContact;
			job.onContact = null;
			if (job.putBall)
			{
				heldBall = null;
			}
			onContact?.Invoke();
		}
	}

	private void EndAct()
	{
		if (!contacted)
		{
			FinishContact(forced: true);
		}
		Action onDone = job.onDone;
		phase = Phase.Idle;
		job = null;
		onDone?.Invoke();
	}

	private void HoldAt(Transform item, Vector3 center, float scoop, float curl)
	{
		item.position = center;
		ContactError = grip.Reach(center, 1f, scoop, curl);
		LastShellGap = grip.DistanceToShell(center) - 0.07f;
	}

	private bool FindStand(Vector3 target, Job j, out Vector3 at, out float yawDeg)
	{
		Vector3 position = player.transform.position;
		Vector3 vector = Flat(position - target);
		if (vector.sqrMagnitude < 0.0001f)
		{
			vector = -Flat(player.transform.forward);
		}
		vector.Normalize();
		float[] array = new float[5] { 0f, 0.2f, 0.4f, 0.65f, 0.9f };
		foreach (float num in array)
		{
			float[] array2 = new float[12]
			{
				0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f, 120f,
				-120f, 180f
			};
			foreach (float y in array2)
			{
				Vector3 vector2 = Quaternion.Euler(0f, y, 0f) * vector;
				float num2 = YawTo(target + vector2, target);
				Vector3 vector3;
				if (j.standOffset != Vector3.zero)
				{
					float num3 = num2 - Mathf.Atan2(j.standOffset.x, j.standOffset.z) * 57.29578f;
					vector3 = Flat(target) - Quaternion.Euler(0f, num3, 0f) * j.standOffset + vector2 * num;
					num2 = num3;
				}
				else
				{
					vector3 = Flat(target) + vector2 * (j.standDistance + num);
				}
				if (!island.Walkable(vector3.x, vector3.z))
				{
					continue;
				}
				float num4 = island.GroundAt(vector3.x, vector3.z, position.y + 0.3f);
				if (!(Mathf.Abs(num4 - position.y) > 0.25f) && !Blocked(vector3, num4))
				{
					vector3.y = num4;
					if (PathClear(position, vector3))
					{
						at = vector3;
						yawDeg = num2;
						return true;
					}
				}
			}
		}
		at = position;
		yawDeg = player.transform.eulerAngles.y;
		return false;
	}

	private bool PathClear(Vector3 a, Vector3 b)
	{
		float num = Vector3.Distance(Flat(a), Flat(b));
		int num2 = Mathf.Max(1, Mathf.CeilToInt(num / 0.1f));
		Vector3 from = a;
		float num3 = Clearance(a, a.y);
		for (int i = 1; i <= num2; i++)
		{
			Vector3 vector = Vector3.Lerp(a, b, (float)i / (float)num2);
			float num4 = island.GroundAt(vector.x, vector.z, from.y + 0.3f);
			bool flag = Blocked(vector, num4) && (!(Vector3.Distance(Flat(vector), Flat(a)) < 0.45f) || !(Clearance(vector, num4) >= num3 - 0.001f));
			if (!island.CanTraverse(from, vector.x, vector.z, jumping: false) || Mathf.Abs(num4 - from.y) > 0.25f || flag)
			{
				return false;
			}
			vector.y = num4;
			from = vector;
		}
		return true;
	}

	private float Clearance(Vector3 p, float ground)
	{
		island.QueryColliders(p.x, p.z, near);
		float num = 9f;
		foreach (EggboyCollider item in near)
		{
			if (!item.water && (!item.top.HasValue || !(item.top.Value - ground <= 0.22f)) && (!(item.minY > 0f) || !(ground < item.minY - 0.03f)))
			{
				if (item.r.HasValue)
				{
					num = Mathf.Min(num, new Vector2(p.x - item.x, p.z - item.z).magnitude - item.r.Value);
				}
				else if (item.hw.HasValue && item.hd.HasValue)
				{
					num = Mathf.Min(num, Mathf.Max(Mathf.Abs(p.x - item.x) - item.hw.Value, Mathf.Abs(p.z - item.z) - item.hd.Value));
				}
			}
		}
		return num;
	}

	private bool Blocked(Vector3 p, float ground)
	{
		island.QueryColliders(p.x, p.z, near);
		foreach (EggboyCollider item in near)
		{
			if (item.water || (item.top.HasValue && item.top.Value - ground <= 0.22f) || (item.minY > 0f && ground < item.minY - 0.03f))
			{
				continue;
			}
			if (item.r.HasValue)
			{
				float num = p.x - item.x;
				float num2 = p.z - item.z;
				if (num * num + num2 * num2 < (item.r.Value + 0.26f) * (item.r.Value + 0.26f))
				{
					return true;
				}
			}
			else if (item.hw.HasValue && item.hd.HasValue && Mathf.Abs(p.x - item.x) < item.hw.Value + 0.26f && Mathf.Abs(p.z - item.z) < item.hd.Value + 0.26f)
			{
				return true;
			}
		}
		return false;
	}

	private static Vector3 Flat(Vector3 v)
	{
		return new Vector3(v.x, 0f, v.z);
	}

	private static float YawTo(Vector3 from, Vector3 to)
	{
		return Mathf.Atan2(to.x - from.x, to.z - from.z) * 57.29578f;
	}

	private static float Ease(float start, float end, float value)
	{
		return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));
	}
}
