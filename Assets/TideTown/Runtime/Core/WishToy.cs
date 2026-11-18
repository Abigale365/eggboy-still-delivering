using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WishToy : MonoBehaviour
{
	[Serializable]
	private class RoutePtW
	{
		public float x;

		public float z;
	}

	[Serializable]
	private class RouteDocW
	{
		public RoutePtW[] route;
	}

	public static readonly List<WishToy> All = new List<WishToy>();

	public const float KickHeight = 0.22f;

	public string description;

	public string category;

	public string recipient;

	public string blurb;

	public int number;

	public Lux3DSpawn spawn;

	public bool bagAllowed = true;

	private IslandWorld world;

	private EggboyPlayer player;

	private readonly List<EggboyCollider> cols = new List<EggboyCollider>();

	private Vector3 homePos;

	private Vector3 homeScale;

	private Quaternion homeRot;

	private Transform homeParent;

	private Vector3 kickVel;

	private bool autoPlace;

	private float placeRadius;

	private static List<Vector2> routePts;

	public float Height { get; private set; }

	public float Radius { get; private set; }

	public bool Held { get; private set; }

	public bool InBag { get; private set; }

	public bool Busy { get; set; }

	public int ColliderCount => cols.Count;

	public bool Kickable => Height < 0.22f;

	public bool CanPick
	{
		get
		{
			if (!Held && !InBag && !Busy)
			{
				return base.gameObject.activeInHierarchy;
			}
			return false;
		}
	}

	private Transform Model => base.transform.Find("Model");

	public void Init(Lux3DSpawn s, string desc, string cat, string to, int num, IslandWorld w, EggboyPlayer p, bool place, float maxShift)
	{
		spawn = s;
		description = desc;
		category = cat;
		recipient = to;
		number = num;
		world = w;
		player = p;
		autoPlace = place;
		placeRadius = maxShift;
		All.Add(this);
		homeParent = base.transform.parent;
		homeScale = base.transform.localScale;
		if (autoPlace)
		{
			Relocate();
		}
		RememberHome();
		Rebuild();
		if (s != null)
		{
			s.onSwapped = (Action<Lux3DSpawn>)Delegate.Combine(s.onSwapped, (Action<Lux3DSpawn>)delegate
			{
				if (!Held && !InBag)
				{
					if (autoPlace)
					{
						Relocate();
					}
					RememberHome();
					Rebuild();
				}
			});
		}
		blurb = WishBlurb.Template(this);
		WishBlurb.RequestFromRelay(this);
		InteractionService interactionService = UnityEngine.Object.FindObjectOfType<InteractionService>();
		if (interactionService != null)
		{
			interactionService.Register(new WishToyInteractable(this));
		}
	}

	private void OnDestroy()
	{
		All.Remove(this);
		ClearColliders();
	}

	public Bounds WorldBounds()
	{
		Transform model = Model;
		return Lux3DModelFit.WorldBounds((model != null) ? model.gameObject : base.gameObject);
	}

	private void RememberHome()
	{
		homePos = base.transform.position;
		homeRot = base.transform.rotation;
	}

	private float GroundUnder(Vector3 p, float near)
	{
		if (!(world != null))
		{
			return p.y;
		}
		return world.GroundAt(p.x, p.z, near);
	}

	private void ClearColliders()
	{
		if (world != null)
		{
			foreach (EggboyCollider col in cols)
			{
				world.RemoveCollider(col);
			}
		}
		cols.Clear();
	}

	public void Rebuild()
	{
		ClearColliders();
		if (world == null || Held || InBag || !base.gameObject.activeInHierarchy)
		{
			return;
		}
		Bounds bounds = WorldBounds();
		float num = GroundUnder(bounds.center, bounds.min.y + 0.3f);
		Height = bounds.max.y - num;
		Radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
		if (Height < 0.22f)
		{
			return;
		}
		float num2 = bounds.min.x - 0.1f;
		float num3 = bounds.min.z - 0.1f;
		int num4 = Mathf.CeilToInt((bounds.size.x + 0.2f) / 0.05f);
		int num5 = Mathf.CeilToInt((bounds.size.z + 0.2f) / 0.05f);
		if (num4 * num5 > 400000)
		{
			num4 = Mathf.Min(num4, 600);
			num5 = Mathf.Min(num5, 600);
		}
		bool[] array = new bool[num4 * num5];
		float[] array2 = new float[num4 * num5];
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i] = float.NegativeInfinity;
		}
		MeshFilter[] componentsInChildren = ((Model != null) ? Model : base.transform).GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			Mesh sharedMesh = meshFilter.sharedMesh;
			if (sharedMesh == null || !sharedMesh.isReadable)
			{
				continue;
			}
			Matrix4x4 localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
			Vector3[] vertices = sharedMesh.vertices;
			Vector3[] array3 = new Vector3[vertices.Length];
			for (int k = 0; k < vertices.Length; k++)
			{
				array3[k] = localToWorldMatrix.MultiplyPoint3x4(vertices[k]);
			}
			int[] triangles = sharedMesh.triangles;
			for (int l = 0; l + 2 < triangles.Length; l += 3)
			{
				Vector3 vector = array3[triangles[l]];
				Vector3 vector2 = array3[triangles[l + 1]];
				Vector3 vector3 = array3[triangles[l + 2]];
				int num6 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max((vector2 - vector).magnitude, Mathf.Max((vector3 - vector2).magnitude, (vector - vector3).magnitude)) / 0.025f), 1, 60);
				for (int m = 0; m <= num6; m++)
				{
					for (int n = 0; n <= num6 - m; n++)
					{
						Vector3 vector4 = vector + (vector2 - vector) * ((float)m / (float)num6) + (vector3 - vector) * ((float)n / (float)num6);
						float num7 = vector4.y - num;
						if (num7 < 0.02f || num7 > 1.3f)
						{
							continue;
						}
						int num8 = Mathf.FloorToInt((vector4.x - num2) / 0.05f);
						int num9 = Mathf.FloorToInt((vector4.z - num3) / 0.05f);
						if (num8 >= 0 && num9 >= 0 && num8 < num4 && num9 < num5)
						{
							int num10 = num9 * num4 + num8;
							array[num10] = true;
							if (vector4.y > array2[num10])
							{
								array2[num10] = vector4.y;
							}
						}
					}
				}
			}
		}
		Dictionary<int, (Vector2, int, float)> dictionary = new Dictionary<int, (Vector2, int, float)>();
		for (int num11 = 0; num11 < num5; num11++)
		{
			for (int num12 = 0; num12 < num4; num12++)
			{
				int num13 = num11 * num4 + num12;
				if (array[num13] && (num12 == 0 || num11 == 0 || num12 == num4 - 1 || num11 == num5 - 1 || !array[num13 - 1] || !array[num13 + 1] || !array[num13 - num4] || !array[num13 + num4]))
				{
					int key = num11 / 2 * 100000 + num12 / 2;
					Vector2 vector5 = new Vector2(num2 + ((float)num12 + 0.5f) * 0.05f, num3 + ((float)num11 + 0.5f) * 0.05f);
					float num14 = Mathf.Max(array2[num13], num + 0.32f);
					dictionary[key] = (dictionary.TryGetValue(key, out var value) ? (value.Item1 + vector5, value.Item2 + 1, Mathf.Max(value.Item3, num14)) : (vector5, 1, num14));
				}
			}
		}
		foreach (KeyValuePair<int, (Vector2, int, float)> item in dictionary)
		{
			Vector2 vector6 = item.Value.Item1 / item.Value.Item2;
			EggboyCollider eggboyCollider = new EggboyCollider
			{
				x = vector6.x,
				z = vector6.y,
				r = 0.148f,
				top = item.Value.Item3
			};
			cols.Add(eggboyCollider);
			world.AddCollider(eggboyCollider);
		}
		if (cols.Count == 0)
		{
			EggboyCollider eggboyCollider2 = new EggboyCollider
			{
				x = bounds.center.x,
				z = bounds.center.z,
				r = Mathf.Max(0.05f, Radius - 0.25f),
				top = bounds.max.y
			};
			cols.Add(eggboyCollider2);
			world.AddCollider(eggboyCollider2);
		}
	}

	private static void LoadRoute()
	{
		if (routePts != null)
		{
			return;
		}
		routePts = new List<Vector2>();
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		if (textAsset == null)
		{
			return;
		}
		RouteDocW routeDocW = JsonUtility.FromJson<RouteDocW>(textAsset.text);
		if (routeDocW?.route != null)
		{
			RoutePtW[] route = routeDocW.route;
			foreach (RoutePtW routePtW in route)
			{
				routePts.Add(new Vector2(routePtW.x, routePtW.z));
			}
		}
	}

	public static float RouteDistance(Vector2 q)
	{
		LoadRoute();
		float num = float.MaxValue;
		for (int i = 0; i + 1 < routePts.Count; i++)
		{
			Vector2 vector = routePts[i];
			Vector2 vector2 = routePts[i + 1] - vector;
			float num2 = Mathf.Clamp01(Vector2.Dot(q - vector, vector2) / Mathf.Max(vector2.sqrMagnitude, 1E-06f));
			num = Mathf.Min(num, (vector + vector2 * num2 - q).magnitude);
		}
		return num;
	}

	private bool SpotOk(Vector3 p, float radius)
	{
		if (world == null)
		{
			return true;
		}
		if (RouteDistance(new Vector2(p.x, p.z)) < 1f + radius)
		{
			return false;
		}
		float num = GroundUnder(p, p.y + 0.6f);
		if (!(world.FreeRoam ? world.RoamStandable(p.x, p.z, num) : world.Walkable(p.x, p.z)) || Mathf.Abs(num - p.y) > 0.6f)
		{
			return false;
		}
		foreach (EggboyCollider collider in world.Colliders)
		{
			if (!collider.water && !cols.Contains(collider))
			{
				float num2 = collider.r ?? Mathf.Max(collider.hw.GetValueOrDefault(), collider.hd.GetValueOrDefault());
				if (new Vector2(p.x - collider.x, p.z - collider.z).magnitude < num2 + radius + 0.15f)
				{
					return false;
				}
			}
		}
		return true;
	}

	private void Relocate()
	{
		Bounds bounds = WorldBounds();
		float radius = Mathf.Max(0.1f, Mathf.Max(bounds.extents.x, bounds.extents.z));
		Vector3 position = base.transform.position;
		if (SpotOk(position, radius))
		{
			return;
		}
		Vector3 vector = ((player != null) ? (position - player.transform.position) : Vector3.forward);
		vector.y = 0f;
		float num = Mathf.Atan2(vector.z, vector.x);
		for (float num2 = 0.3f; num2 <= placeRadius + 0.001f; num2 += 0.25f)
		{
			for (int i = 0; i < 16; i++)
			{
				float f = num + (float)(((i % 2 == 0) ? 1 : (-1)) * (i / 2)) * MathF.PI / 8f;
				Vector3 vector2 = position + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * num2;
				vector2.y = GroundUnder(vector2, position.y + 0.6f);
				if (SpotOk(vector2, radius))
				{
					base.transform.position = vector2;
					return;
				}
			}
		}
	}

	private void Update()
	{
		if (world == null || player == null || Held || InBag)
		{
			return;
		}
		if (Kickable)
		{
			Vector3 position = player.transform.position;
			Vector3 position2 = base.transform.position;
			Vector2 vector = new Vector2(position2.x - position.x, position2.z - position.z);
			float magnitude = new Vector2(player.vel.x, player.vel.z).magnitude;
			if (vector.magnitude < 0.42f + Radius * 0.6f && magnitude > 0.2f && kickVel.sqrMagnitude < 0.01f && Mathf.Abs(position2.y - position.y) < 0.4f)
			{
				Vector2 vector2 = ((vector.sqrMagnitude > 0.0001f) ? vector.normalized : new Vector2(player.vel.x, player.vel.z).normalized);
				kickVel = new Vector3(vector2.x, 0f, vector2.y) * (0.9f + magnitude * 0.45f);
				ShadowAndChimeSystem.PlayProceduralChime(523.25f, 0.06f);
			}
		}
		if (kickVel.sqrMagnitude > 0.0004f)
		{
			float num = Mathf.Min(0.05f, Time.deltaTime);
			Vector3 vector3 = base.transform.position + kickVel * num;
			float num2 = GroundUnder(vector3, base.transform.position.y + 0.25f);
			if (!(Mathf.Abs(num2 - base.transform.position.y) < 0.2f) || !(world.FreeRoam ? world.RoamStandable(vector3.x, vector3.z, num2) : world.Walkable(vector3.x, vector3.z)))
			{
				kickVel = Vector3.zero;
			}
			else
			{
				vector3.y = num2;
				base.transform.position = vector3;
				Vector3 axis = Vector3.Cross(Vector3.up, kickVel.normalized);
				base.transform.Rotate(axis, kickVel.magnitude * num / Mathf.Max(0.05f, Height * 0.5f) * 57.29578f, Space.World);
				kickVel *= Mathf.Exp(-3.2f * num);
			}
			if (kickVel.sqrMagnitude <= 0.0004f)
			{
				kickVel = Vector3.zero;
				RememberHome();
			}
		}
	}

	public Vector3 GrabPoint()
	{
		Bounds bounds = WorldBounds();
		Vector3 vector = ((player != null) ? player.transform.position : bounds.center);
		float y = Mathf.Min(bounds.center.y, GroundUnder(bounds.center, bounds.min.y + 0.3f) + 0.38f);
		return Vector3.Lerp(new Vector3(Mathf.Clamp(vector.x, bounds.min.x, bounds.max.x), y, Mathf.Clamp(vector.z, bounds.min.z, bounds.max.z)), new Vector3(bounds.center.x, y, bounds.center.z), 0.5f);
	}

	public void BeginPick()
	{
		if (CanPick && !(player == null))
		{
			Busy = true;
			string clip = ((WorldBounds().center.y - player.transform.position.y > 0.45f) ? "inspect_memory" : "pickup_drop");
			PhysicalPickupController instance = PhysicalPickupController.Instance;
			if (!(instance != null) || !instance.Active(clip) || !instance.Begin(new PhysicalPickupController.Job
			{
				clip = clip,
				target = GrabPoint,
				standDistance = Mathf.Clamp(Radius + 0.32f, 0.42f, 0.95f),
				contactTime = 0.4f,
				tolerance = 0.1f,
				reach = true,
				onContact = Attach,
				onDone = delegate
				{
					WishInspectModal.Open(this, player);
				}
			}))
			{
				Attach();
				WishInspectModal.Open(this, player);
			}
		}
	}

	private void Attach()
	{
		if (!Held && !(player == null))
		{
			ClearColliders();
			kickVel = Vector3.zero;
			Held = true;
			Transform transform = ((player.CarryAnchor != null) ? player.CarryAnchor : player.transform);
			Bounds bounds = WorldBounds();
			float num = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
			base.transform.SetParent(transform, worldPositionStays: true);
			if (num > 0.32f)
			{
				base.transform.localScale *= 0.32f / num;
			}
			Bounds bounds2 = WorldBounds();
			base.transform.position += transform.position + transform.forward * 0.04f - bounds2.center;
			player.SetCarryPose(EggboyPlayer.CarryPose.Ball);
			ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.12f);
		}
	}

	public void PutBack()
	{
		Busy = false;
		if (player != null)
		{
			player.SetCarryPose(EggboyPlayer.CarryPose.None);
		}
		StartCoroutine(FlyHome());
	}

	private IEnumerator FlyHome()
	{
		Vector3 from = base.transform.position;
		Quaternion fromRot = base.transform.rotation;
		Vector3 fromScale = base.transform.lossyScale;
		base.transform.SetParent(homeParent, worldPositionStays: true);
		if (player != null && player.HasClip("pickup_drop"))
		{
			player.PlayEmote("pickup_drop");
		}
		for (float t = 0f; t < 0.45f; t += Time.deltaTime)
		{
			float num = Mathf.SmoothStep(0f, 1f, t / 0.45f);
			base.transform.position = Vector3.Lerp(from, homePos, num) + Vector3.up * Mathf.Sin(num * MathF.PI) * 0.18f;
			base.transform.rotation = Quaternion.Slerp(fromRot, homeRot, num);
			base.transform.localScale = Vector3.Lerp(homeScale * (fromScale.x / Mathf.Max(0.0001f, LossyX(homeScale))), homeScale, num);
			yield return null;
		}
		base.transform.SetPositionAndRotation(homePos, homeRot);
		base.transform.localScale = homeScale;
		Held = false;
		Rebuild();
	}

	private float LossyX(Vector3 local)
	{
		if (!(homeParent != null))
		{
			return local.x;
		}
		return local.x * homeParent.lossyScale.x;
	}

	public void StoreInBag()
	{
		Busy = false;
		if (player != null)
		{
			player.SetCarryPose(EggboyPlayer.CarryPose.None);
		}
		base.transform.SetParent(homeParent, worldPositionStays: true);
		base.transform.localScale = homeScale;
		Held = false;
		InBag = true;
		ClearColliders();
		base.gameObject.SetActive(value: false);
		WishBag.Add(this);
	}

	public void TakeOutAt(Vector3 at, float yawDeg)
	{
		InBag = false;
		base.gameObject.SetActive(value: true);
		base.transform.SetParent(homeParent, worldPositionStays: true);
		base.transform.localScale = homeScale;
		base.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yawDeg, 0f));
		autoPlace = true;
		placeRadius = 2.5f;
		Relocate();
		RememberHome();
		Rebuild();
	}
}
