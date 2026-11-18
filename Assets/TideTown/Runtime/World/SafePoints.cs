using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SafePoints : MonoBehaviour
{
	[Serializable]
	private class RoutePt
	{
		public string id;

		public bool stop;

		public float x;

		public float z;
	}

	[Serializable]
	private class RouteDoc
	{
		public RoutePt[] route;
	}

	public List<Vector3> points = new List<Vector3>();

	public float stuckSeconds = 3f;

	public float stuckSpeed = 0.15f;

	public float stuckRadius = 0.25f;

	public bool Enabled = true;

	private EggboyPlayer player;

	private IslandWorld world;

	private float stuck;

	private Vector3 last;

	private const int SafeHistoryLimit = 8;

	private readonly List<Vector3> safeHistory = new List<Vector3>(8);

	private float nextSafeSample;

	public float fellSeconds = 2f;

	public float fellRadius = 2.5f;

	public float fellDrop = 0.5f;

	private float fell;

	private Vector2[] routePts;

	public int RescueCount { get; private set; }

	public string LastReason { get; private set; }

	public Vector3 LastFrom { get; private set; }

	public Vector3 LastTarget { get; private set; }

	private void Start()
	{
		player = UnityEngine.Object.FindObjectOfType<EggboyPlayer>();
		world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		if (player != null)
		{
			last = player.transform.position;
		}
	}

	private void Update()
	{
		if (!Enabled)
		{
			return;
		}
		if (player == null)
		{
			player = UnityEngine.Object.FindObjectOfType<EggboyPlayer>();
		}
		if (player == null)
		{
			return;
		}
		if (world == null)
		{
			world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		}
		Vector3 position = player.transform.position;
		float num = ((world != null && world.IsBeachSurface(position.x, position.z, position.y)) ? 0.2f : 0.05f);
		if (position.y < WaterLevel.StaticY - num)
		{
			Rescue("water");
			return;
		}
		RememberSafeGround(position);
		float magnitude = new Vector2(player.vel.x, player.vel.z).magnitude;
		bool flag = world != null && player.grounded && world.IsBeachSurface(position.x, position.z, position.y);
		bool flag2 = !player.locked && (flag ? player.TryingToMove : (magnitude > stuckSpeed));
		if (!flag2 || stuck <= 0f)
		{
			last = position;
			stuck = (flag2 ? Time.deltaTime : 0f);
		}
		else if (new Vector2(position.x - last.x, position.z - last.z).magnitude > stuckRadius)
		{
			last = position;
			stuck = Time.deltaTime;
		}
		else
		{
			stuck += Time.deltaTime;
		}
		if (stuck >= stuckSeconds)
		{
			Rescue(flag ? "beach" : "stuck");
			return;
		}
		Vector3 back = position;
		if (!player.locked && player.grounded && FellBelowRoute(position, out back))
		{
			fell += Time.deltaTime;
		}
		else
		{
			fell = 0f;
		}
		if (fell >= fellSeconds)
		{
			RescueTo(back, "fell");
		}
	}

	private void RememberSafeGround(Vector3 position)
	{
		if (world == null || player.locked || player.ActionLocked || !player.grounded || Time.time < nextSafeSample)
		{
			return;
		}
		nextSafeSample = Time.time + 0.25f;
		if ((safeHistory.Count <= 0 || !((position - safeHistory[safeHistory.Count - 1]).sqrMagnitude < 0.36f)) && TrySafeGround(position, out var safe))
		{
			if (safeHistory.Count == 8)
			{
				safeHistory.RemoveAt(0);
			}
			safeHistory.Add(safe);
		}
	}

	private bool TrySafeGround(Vector3 position, out Vector3 safe)
	{
		safe = position;
		if (!world.InWalkArea(position.x, position.z))
		{
			return false;
		}
		if (!Physics.Raycast(position + Vector3.up * 0.25f, Vector3.down, out var hitInfo, 0.5f, world.groundMask, QueryTriggerInteraction.Ignore))
		{
			return false;
		}
		if (hitInfo.collider.gameObject.layer == 8 || hitInfo.collider.GetComponentInParent<FloatingCrate>() != null || hitInfo.normal.y < 0.6f || hitInfo.point.y <= WaterLevel.StaticY + 0.05f)
		{
			return false;
		}
		safe = hitInfo.point + Vector3.up * 0.02f;
		if (!world.InRoamBlock(safe.x, safe.z, hitInfo.point.y) && !world.RockWallAt(safe.x, safe.z, hitInfo.point.y) && !world.RockHeadroomBlocked(safe.x, safe.z, hitInfo.point.y))
		{
			return player.HasStandingClearance(safe);
		}
		return false;
	}

	public bool TryReturnToSafety()
	{
		if (!Enabled || world == null || player == null || player.ActionLocked)
		{
			return false;
		}
		for (int num = safeHistory.Count - 1; num >= 0; num--)
		{
			if (TrySafeGround(safeHistory[num], out var safe))
			{
				ManualReturnTo(safe);
				return true;
			}
		}
		Vector3 spawnPos = player.SpawnPos;
		if (TrySafeGround(spawnPos, out var safe2))
		{
			ManualReturnTo(safe2);
			return true;
		}
		float num2 = 144f;
		Vector3 target = spawnPos;
		bool flag = false;
		foreach (Vector3 point in points)
		{
			float num3 = point.x - spawnPos.x;
			float num4 = point.z - spawnPos.z;
			float num5 = num3 * num3 + num4 * num4;
			if (!(num5 >= num2) && TrySafeGround(point, out var safe3))
			{
				num2 = num5;
				target = safe3;
				flag = true;
			}
		}
		if (flag)
		{
			ManualReturnTo(target);
		}
		return flag;
	}

	private void ManualReturnTo(Vector3 target)
	{
		RescueTo(target, "manual");
		player.SyncAfterSafeReturn();
		Camera main = Camera.main;
		if (main != null && main.TryGetComponent<ChaseCamera>(out var component))
		{
			component.SnapBehind(target, player.yaw);
		}
	}

	private void LoadRoute()
	{
		routePts = new Vector2[0];
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		if (textAsset == null)
		{
			return;
		}
		RouteDoc routeDoc = JsonUtility.FromJson<RouteDoc>(textAsset.text);
		if (routeDoc != null && routeDoc.route != null)
		{
			routePts = new Vector2[routeDoc.route.Length];
			for (int i = 0; i < routeDoc.route.Length; i++)
			{
				routePts[i] = new Vector2(routeDoc.route[i].x, routeDoc.route[i].z);
			}
		}
	}

	public bool FellBelowRoute(Vector3 p, out Vector3 back)
	{
		back = p;
		if (world == null || world.IsBeachSurface(p.x, p.z, p.y))
		{
			return false;
		}
		if (world.FreeRoam)
		{
			return false;
		}
		if (routePts == null)
		{
			LoadRoute();
		}
		if (routePts.Length < 2)
		{
			return false;
		}
		foreach (WalkPoly walkPoly in world.walkPolys)
		{
			if (Mathf.Abs(walkPoly.y - p.y) < 0.6f && InPoly(walkPoly.points, p.x, p.z))
			{
				return false;
			}
		}
		if (world.lawnPolys != null)
		{
			foreach (WalkPoly lawnPoly in world.lawnPolys)
			{
				if (Mathf.Abs(lawnPoly.y - p.y) < 0.6f && InPoly(lawnPoly.points, p.x, p.z))
				{
					return false;
				}
			}
		}
		Vector2 vector = new Vector2(p.x, p.z);
		bool flag = false;
		float num = float.MaxValue;
		for (int i = 0; i + 1 < routePts.Length; i++)
		{
			Vector2 vector2 = ClosestOnSegment(routePts[i], routePts[i + 1], vector);
			float magnitude = (vector2 - vector).magnitude;
			if (!(magnitude > fellRadius))
			{
				float num2 = world.GroundAt(vector2.x, vector2.y, p.y + 9f);
				if (num2 - p.y < fellDrop)
				{
					return false;
				}
				flag = true;
				if (magnitude < num && world.Walkable(vector2.x, vector2.y))
				{
					num = magnitude;
					back = new Vector3(vector2.x, num2 + 0.02f, vector2.y);
				}
			}
		}
		if (flag)
		{
			return num < float.MaxValue;
		}
		return false;
	}

	private static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
	{
		Vector2 vector = b - a;
		float sqrMagnitude = vector.sqrMagnitude;
		if (sqrMagnitude < 1E-06f)
		{
			return a;
		}
		float num = Mathf.Clamp01(Vector2.Dot(p - a, vector) / sqrMagnitude);
		return a + vector * num;
	}

	private static bool InPoly(Vector2[] poly, float x, float z)
	{
		if (poly == null || poly.Length < 3)
		{
			return false;
		}
		bool flag = false;
		int num = 0;
		int num2 = poly.Length - 1;
		while (num < poly.Length)
		{
			Vector2 vector = poly[num];
			Vector2 vector2 = poly[num2];
			if (vector.y > z != vector2.y > z && x < (vector2.x - vector.x) * (z - vector.y) / (vector2.y - vector.y) + vector.x)
			{
				flag = !flag;
			}
			num2 = num++;
		}
		return flag;
	}

	private void RescueTo(Vector3 target, string why)
	{
		Vector3 position = player.transform.position;
		player.transform.position = target;
		player.vel = Vector3.zero;
		player.grounded = true;
		player.jump.phase = JumpPhase.none;
		stuck = 0f;
		fell = 0f;
		last = target;
		RescueCount++;
		LastReason = why;
		LastFrom = position;
		LastTarget = target;
		Debug.Log($"WATER_SAFE rescue {why} ({position.x:F2},{position.y:F2},{position.z:F2}) -> ({target.x:F2},{target.y:F2},{target.z:F2})");
	}

	private void Rescue(string why)
	{
		Vector3 position = player.transform.position;
		Vector3 vector = player.SpawnPos;
		float num = float.MaxValue;
		bool flag = false;
		foreach (Vector3 point in points)
		{
			float num2 = ((world != null) ? world.GroundAt(point.x, point.z, point.y) : point.y);
			if (!(num2 < WaterLevel.StaticY - 0.05f))
			{
				float sqrMagnitude = new Vector2(point.x - position.x, point.z - position.z).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					vector = new Vector3(point.x, num2 + 0.02f, point.z);
					flag = true;
				}
			}
		}
		if (!flag)
		{
			vector = new Vector3(player.SpawnPos.x, player.SpawnPos.y + 0.02f, player.SpawnPos.z);
		}
		player.transform.position = vector;
		player.vel = Vector3.zero;
		player.grounded = true;
		player.jump.phase = JumpPhase.none;
		stuck = 0f;
		fell = 0f;
		last = vector;
		RescueCount++;
		LastReason = why;
		LastFrom = position;
		LastTarget = vector;
		Debug.Log($"WATER_SAFE rescue {why} ({position.x:F2},{position.z:F2}) -> ({vector.x:F2},{vector.z:F2})");
	}
}
