using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

public class IslandWorld : MonoBehaviour, IEggboyWorld
{
	[Serializable]
	public class RoamBlock
	{
		public string id;

		public string when;

		public float minY = -10f;

		public float maxY = 100f;

		public float[] x;

		public float[] z;

		[NonSerialized]
		public Vector2[] pts;

		[NonSerialized]
		public float waterMin = float.NaN;
	}

	[Serializable]
	private class RoamBlockDoc
	{
		public RoamBlock[] blocks;
	}

	public LayerMask groundMask;

	public LayerMask obstacleMask;

	public Transform mailboxMarker;

	public Transform spawnMarker;

	public GameObject mailboxTrail;

	public GameObject endingDesk;

	public GameObject woodModel;

	public GameObject oilModel;

	public float waterLevel;

	public List<WalkPoly> walkPolys = new List<WalkPoly>();

	public List<WalkPoly> lawnPolys = new List<WalkPoly>();

	public WalkPoly beachPoly;

	private readonly List<EggboyCollider> colliders = new List<EggboyCollider>();

	private const float SpatialCell = 2f;

	private readonly Dictionary<long, List<EggboyCollider>> spatialBuckets = new Dictionary<long, List<EggboyCollider>>();

	private bool spatialDirty = true;

	private EggboyCollider endingDeskCollider;

	private readonly List<EggboyCollider> cameraCandidates = new List<EggboyCollider>();

	private readonly HashSet<EggboyCollider> cameraSeen = new HashSet<EggboyCollider>();

	public const float WadeDepth = 0.18f;

	public const float RoadWadeDepth = 0.05f;

	public const int RockGroundLayer = 8;

	public const int RockGroundMask = 256;

	public const float MaxDrop = 3f;

	public const float WalkDrop = 0.6f;

	public const float BodyTop = 1.3f;

	public const float StepUp = 0.23f;

	public const float JumpUp = 0.28f;

	private RoamBlock[] roamBlocks;

	public Func<string, bool> RoamCondition;

	private static readonly RaycastHit[] rockWallHits = new RaycastHit[16];

	public IReadOnlyList<EggboyCollider> Colliders => colliders;

	public float WaterY
	{
		get
		{
			if (!(WaterLevel.Main != null))
			{
				return waterLevel;
			}
			return WaterLevel.Main.Y;
		}
	}

	public Vector3 MailboxPos
	{
		get
		{
			if (!(mailboxMarker != null))
			{
				return Vector3.zero;
			}
			return mailboxMarker.position;
		}
	}

	public Vector3 SpawnPos
	{
		get
		{
			if (!(spawnMarker != null))
			{
				return Vector3.zero;
			}
			return spawnMarker.position;
		}
	}

	public bool FreeRoam
	{
		get
		{
			if (Features.freeRoam)
			{
				return walkPolys.Count > 0;
			}
			return false;
		}
	}

	public IReadOnlyList<RoamBlock> RoamBlocks
	{
		get
		{
			LoadRoamBlocks();
			return roamBlocks;
		}
	}

	public void AddCollider(EggboyCollider c)
	{
		if (c != null && !colliders.Contains(c))
		{
			colliders.Add(c);
			spatialDirty = true;
		}
	}

	public void RemoveCollider(EggboyCollider c)
	{
		if (c != null && colliders.Remove(c))
		{
			spatialDirty = true;
		}
	}

	public void SyncEndingDeskCollider()
	{
		if (endingDesk == null)
		{
			return;
		}
		if (endingDesk.activeInHierarchy)
		{
			if (endingDeskCollider == null)
			{
				Vector3 position = endingDesk.transform.position;
				endingDeskCollider = new EggboyCollider
				{
					x = position.x,
					z = position.z,
					r = 0.2f,
					top = position.y + 0.76f
				};
			}
			AddCollider(endingDeskCollider);
		}
		else if (endingDeskCollider != null)
		{
			RemoveCollider(endingDeskCollider);
		}
	}

	private void RebuildSpatialGrid()
	{
		spatialDirty = false;
		spatialBuckets.Clear();
		for (int i = 0; i < colliders.Count; i++)
		{
			EggboyCollider eggboyCollider = colliders[i];
			float num = (eggboyCollider.hw ?? eggboyCollider.r ?? 0.5f) + 0.6f;
			float num2 = (eggboyCollider.hd ?? eggboyCollider.r ?? 0.5f) + 0.6f;
			int num3 = Mathf.FloorToInt((eggboyCollider.x - num) / 2f);
			int num4 = Mathf.FloorToInt((eggboyCollider.x + num) / 2f);
			int num5 = Mathf.FloorToInt((eggboyCollider.z - num2) / 2f);
			int num6 = Mathf.FloorToInt((eggboyCollider.z + num2) / 2f);
			for (int j = num5; j <= num6; j++)
			{
				for (int k = num3; k <= num4; k++)
				{
					long key = ((long)k << 32) | (uint)j;
					if (!spatialBuckets.TryGetValue(key, out var value))
					{
						value = new List<EggboyCollider>(8);
						spatialBuckets[key] = value;
					}
					value.Add(eggboyCollider);
				}
			}
		}
	}

	public void QueryColliders(float x, float z, List<EggboyCollider> results)
	{
		results.Clear();
		if (spatialDirty)
		{
			RebuildSpatialGrid();
		}
		int num = Mathf.FloorToInt(x / 2f);
		int num2 = Mathf.FloorToInt(z / 2f);
		long key = ((long)num << 32) | (uint)num2;
		if (spatialBuckets.TryGetValue(key, out var value))
		{
			results.AddRange(value);
		}
	}

	public void QueryCollidersRange(Vector3 a, Vector3 b, float margin, List<EggboyCollider> results)
	{
		results.Clear();
		cameraSeen.Clear();
		if (spatialDirty)
		{
			RebuildSpatialGrid();
		}
		int num = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - margin) / 2f);
		int num2 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + margin) / 2f);
		int num3 = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - margin) / 2f);
		int num4 = Mathf.FloorToInt((Mathf.Max(a.z, b.z) + margin) / 2f);
		for (int i = num3; i <= num4; i++)
		{
			for (int j = num; j <= num2; j++)
			{
				if (!spatialBuckets.TryGetValue(((long)j << 32) | (uint)i, out var value))
				{
					continue;
				}
				foreach (EggboyCollider item in value)
				{
					if (cameraSeen.Add(item))
					{
						results.Add(item);
					}
				}
			}
		}
	}

	public float CameraBlockDistance(Vector3 pivot, Vector3 direction, float maxDistance)
	{
		direction.Normalize();
		QueryCollidersRange(pivot, pivot + direction * maxDistance, 0.35f, cameraCandidates);
		float num = maxDistance;
		foreach (EggboyCollider cameraCandidate in cameraCandidates)
		{
			if (CameraIntersection(cameraCandidate, pivot, direction, num, 0.3f, out var enter))
			{
				num = Mathf.Min(num, Mathf.Max(0f, enter - 0.08f));
			}
		}
		return num;
	}

	public bool CameraPointBlocked(Vector3 point)
	{
		QueryCollidersRange(point, point, 0.01f, cameraCandidates);
		foreach (EggboyCollider cameraCandidate in cameraCandidates)
		{
			if (CameraIntersection(cameraCandidate, point, Vector3.forward, 0f, 0f, out var _))
			{
				return true;
			}
		}
		return false;
	}

	private static bool CameraIntersection(EggboyCollider c, Vector3 p, Vector3 d, float max, float radius, out float enter)
	{
		enter = 0f;
		float exit = max;
		if (c.water || (c.r.HasValue && c.r.Value < 0.24f))
		{
			return false;
		}
		if (c.r.HasValue)
		{
			float num = p.x - c.x;
			float num2 = p.z - c.z;
			float num3 = c.r.Value + radius;
			float num4 = d.x * d.x + d.z * d.z;
			float num5 = num * d.x + num2 * d.z;
			float num6 = num * num + num2 * num2 - num3 * num3;
			if (num4 < 1E-08f)
			{
				if (num6 > 0f)
				{
					return false;
				}
			}
			else
			{
				float num7 = num5 * num5 - num4 * num6;
				if (num7 < 0f)
				{
					return false;
				}
				float num8 = Mathf.Sqrt(num7);
				enter = Mathf.Max(enter, (0f - num5 - num8) / num4);
				exit = Mathf.Min(exit, (0f - num5 + num8) / num4);
			}
		}
		else
		{
			if (!c.hw.HasValue || !c.hd.HasValue)
			{
				return false;
			}
			if (!CameraSlab(p.x, d.x, c.x - c.hw.Value - radius, c.x + c.hw.Value + radius, ref enter, ref exit) || !CameraSlab(p.z, d.z, c.z - c.hd.Value - radius, c.z + c.hd.Value + radius, ref enter, ref exit))
			{
				return false;
			}
		}
		float low = ((c.minY > 0f) ? (c.minY - radius) : float.NegativeInfinity);
		float high = (c.top.HasValue ? (c.top.Value + 0.08f + radius) : float.PositiveInfinity);
		if (CameraSlab(p.y, d.y, low, high, ref enter, ref exit) && exit >= enter && exit >= 0f)
		{
			return enter <= max;
		}
		return false;
	}

	private static bool CameraSlab(float p, float d, float low, float high, ref float enter, ref float exit)
	{
		if (Mathf.Abs(d) < 1E-08f)
		{
			if (p >= low)
			{
				return p <= high;
			}
			return false;
		}
		float num = (low - p) / d;
		float num2 = (high - p) / d;
		if (num > num2)
		{
			float num3 = num;
			num = num2;
			num2 = num3;
		}
		enter = Mathf.Max(enter, num);
		exit = Mathf.Min(exit, num2);
		return exit >= enter;
	}

	private void Awake()
	{
		if ((int)groundMask == 0)
		{
			groundMask = LayerMask.GetMask("Ground");
		}
		groundMask = (int)groundMask | 0x100;
		if ((int)obstacleMask == 0)
		{
			obstacleMask = LayerMask.GetMask("TownObstacle");
		}
		CollectObstacles();
		CleanVisualRoadAndTierSeams();
	}

	private void CleanVisualRoadAndTierSeams()
	{
		Material material = null;
		GameObject gameObject = GameObject.Find("Tier_pad_stone_3_0_0");
		if (gameObject != null)
		{
			MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
			if (component != null)
			{
				material = component.sharedMaterial;
			}
		}
		GameObject gameObject2 = GameObject.Find("Tier_pad_grass_3_0_0");
		if (gameObject2 != null)
		{
			MeshFilter component2 = gameObject2.GetComponent<MeshFilter>();
			if (component2 != null && component2.sharedMesh != null)
			{
				Mesh mesh = UnityEngine.Object.Instantiate(component2.sharedMesh);
				Vector3[] vertices = mesh.vertices;
				for (int i = 0; i < vertices.Length; i++)
				{
					if (vertices[i].x > 8.2f && vertices[i].x < 23.2f && vertices[i].z > -7.1f && vertices[i].z < 8.5f)
					{
						vertices[i].y = 3.028f;
					}
					else
					{
						vertices[i].y = 3.012f;
					}
				}
				mesh.vertices = vertices;
				mesh.RecalculateBounds();
				component2.sharedMesh = mesh;
			}
		}
		if (gameObject != null)
		{
			MeshFilter component3 = gameObject.GetComponent<MeshFilter>();
			if (component3 != null && component3.sharedMesh != null)
			{
				Mesh mesh2 = UnityEngine.Object.Instantiate(component3.sharedMesh);
				Vector3[] vertices2 = mesh2.vertices;
				for (int j = 0; j < vertices2.Length; j++)
				{
					if (vertices2[j].x > 8.5f && vertices2[j].x < 23.8f && vertices2[j].z > -7.9f && vertices2[j].z < -4.5f)
					{
						vertices2[j].y = 3.005f;
					}
					else
					{
						vertices2[j].y = 3.024f;
					}
				}
				mesh2.vertices = vertices2;
				mesh2.RecalculateBounds();
				component3.sharedMesh = mesh2;
			}
		}
		MeshFilter[] array = UnityEngine.Object.FindObjectsOfType<MeshFilter>();
		foreach (MeshFilter meshFilter in array)
		{
			if (meshFilter == null || meshFilter.sharedMesh == null)
			{
				continue;
			}
			string text = meshFilter.gameObject.name;
			if (!text.StartsWith("Road_") || meshFilter.sharedMesh.name.StartsWith("Finished Road ", StringComparison.Ordinal))
			{
				continue;
			}
			Mesh mesh3 = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
			if (mesh3.subMeshCount >= 4)
			{
				mesh3.SetTriangles(new int[0], 1);
				mesh3.SetTriangles(new int[0], 2);
				mesh3.SetTriangles(new int[0], 3);
			}
			Vector3[] vertices3 = mesh3.vertices;
			for (int l = 0; l < vertices3.Length; l++)
			{
				if (vertices3[l].y >= 2.95f && vertices3[l].y <= 3.05f)
				{
					vertices3[l].y = 3.01f;
				}
				else if (vertices3[l].y >= 4.96f && vertices3[l].y <= 5.05f)
				{
					vertices3[l].y = 4.992f;
				}
				else if (vertices3[l].y >= 5.46f && vertices3[l].y <= 5.55f)
				{
					vertices3[l].y = 5.51f;
				}
				else if (vertices3[l].y >= 6.46f && vertices3[l].y <= 6.55f)
				{
					vertices3[l].y = 6.492f;
				}
			}
			mesh3.vertices = vertices3;
			mesh3.RecalculateBounds();
			meshFilter.sharedMesh = mesh3;
			if (material != null && text != "Road_to_dock")
			{
				MeshRenderer component4 = meshFilter.GetComponent<MeshRenderer>();
				if (component4 != null && component4.sharedMaterials != null && component4.sharedMaterials.Length != 0)
				{
					Material[] sharedMaterials = component4.sharedMaterials;
					sharedMaterials[0] = material;
					component4.sharedMaterials = sharedMaterials;
				}
			}
		}
		GameObject gameObject3 = GameObject.Find("exp_mountain_curbs");
		if (gameObject3 != null)
		{
			MeshFilter component5 = gameObject3.GetComponent<MeshFilter>();
			if (component5 != null && component5.sharedMesh != null)
			{
				component5.sharedMesh = ExpandOuterEdge(component5.sharedMesh, 0.03f);
			}
		}
		SinkUnderRoad("Cliff_houses", "Road_mountain", 0.03f);
		array = UnityEngine.Object.FindObjectsOfType<MeshFilter>();
		foreach (MeshFilter meshFilter2 in array)
		{
			if (meshFilter2 == null || meshFilter2.sharedMesh == null || meshFilter2.gameObject.name != "Rock roadbed")
			{
				continue;
			}
			Transform parent = meshFilter2.transform.parent;
			if (!(parent == null) && parent.name.StartsWith("Road_"))
			{
				Mesh mesh4 = UnityEngine.Object.Instantiate(meshFilter2.sharedMesh);
				mesh4.name = meshFilter2.sharedMesh.name;
				Vector3[] vertices4 = mesh4.vertices;
				float num = Mathf.Max(0.0001f, meshFilter2.transform.lossyScale.y);
				for (int m = 0; m < vertices4.Length; m++)
				{
					vertices4[m].y -= 0.03f / num;
				}
				mesh4.vertices = vertices4;
				mesh4.RecalculateBounds();
				meshFilter2.sharedMesh = mesh4;
			}
		}
	}

	private static void SinkUnderRoad(string meshName, string roadName, float depth)
	{
		GameObject gameObject = GameObject.Find(meshName);
		GameObject gameObject2 = GameObject.Find(roadName);
		if (gameObject == null || gameObject2 == null)
		{
			return;
		}
		MeshFilter component = gameObject.GetComponent<MeshFilter>();
		MeshFilter component2 = gameObject2.GetComponent<MeshFilter>();
		if (component == null || component.sharedMesh == null || component2 == null || component2.sharedMesh == null)
		{
			return;
		}
		Mesh sharedMesh = component.sharedMesh;
		Vector3[] vertices = sharedMesh.vertices;
		Vector2[] uv = sharedMesh.uv;
		int[] triangles = sharedMesh.triangles;
		if (vertices.Length % 4 != 0 || triangles.Length != vertices.Length / 4 * 6 || uv.Length != vertices.Length)
		{
			return;
		}
		GameObject gameObject3 = new GameObject("RoadSinkProbe");
		gameObject3.transform.SetPositionAndRotation(gameObject2.transform.position, gameObject2.transform.rotation);
		gameObject3.transform.localScale = gameObject2.transform.lossyScale;
		MeshCollider mc = gameObject3.AddComponent<MeshCollider>();
		mc.sharedMesh = component2.sharedMesh;
		Physics.SyncTransforms();
		Bounds rb = mc.bounds;
		Transform transform = gameObject.transform;
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<int> list3 = new List<int>();
		int num = 0;
		for (int i = 0; i < vertices.Length; i += 4)
		{
			bool flag = triangles[i / 4 * 6 + 1] != i + 1;
			Vector3 vector = vertices[i];
			Vector3 vector2 = vertices[i + 1];
			Vector3 vector3 = vertices[i + 2];
			Vector3 vector4 = vertices[i + 3];
			Vector3 a = transform.TransformPoint(vector);
			Vector3 b = transform.TransformPoint(vector2);
			float magnitude = new Vector2(b.x - a.x, b.z - a.z).magnitude;
			int num2 = Mathf.Max(1, Mathf.CeilToInt(magnitude / 0.1f));
			float[] array = new float[num2 + 1];
			bool flag2 = false;
			for (int j = 0; j <= num2; j++)
			{
				Vector3 w2 = Vector3.Lerp(a, b, (float)j / (float)num2);
				float num3 = Sunk(w2);
				array[j] = (float.IsNaN(num3) ? w2.y : num3);
				flag2 |= !float.IsNaN(num3);
			}
			if (!flag2 || num2 == 1)
			{
				int count = list.Count;
				list.Add(vector);
				list.Add(vector2);
				list.Add(vector3);
				list.Add(vector4);
				list2.Add(uv[i]);
				list2.Add(uv[i + 1]);
				list2.Add(uv[i + 2]);
				list2.Add(uv[i + 3]);
				for (int k = 0; k < 6; k++)
				{
					list3.Add(triangles[i / 4 * 6 + k] - i + count);
				}
				continue;
			}
			num++;
			for (int l = 0; l < num2; l++)
			{
				float t = (float)l / (float)num2;
				float t2 = (float)(l + 1) / (float)num2;
				Vector3 position = Vector3.Lerp(a, b, t);
				position.y = array[l];
				Vector3 position2 = Vector3.Lerp(a, b, t2);
				position2.y = array[l + 1];
				int count2 = list.Count;
				list.Add(transform.InverseTransformPoint(position));
				list.Add(transform.InverseTransformPoint(position2));
				list.Add(Vector3.Lerp(vector4, vector3, t2));
				list.Add(Vector3.Lerp(vector4, vector3, t));
				list2.Add(new Vector2(Mathf.Lerp(uv[i].x, uv[i + 1].x, t), array[l]));
				list2.Add(new Vector2(Mathf.Lerp(uv[i].x, uv[i + 1].x, t2), array[l + 1]));
				list2.Add(Vector2.Lerp(uv[i + 3], uv[i + 2], t2));
				list2.Add(Vector2.Lerp(uv[i + 3], uv[i + 2], t));
				if (!flag)
				{
					list3.AddRange(new int[6]
					{
						count2,
						count2 + 1,
						count2 + 2,
						count2,
						count2 + 2,
						count2 + 3
					});
				}
				else
				{
					list3.AddRange(new int[6]
					{
						count2,
						count2 + 2,
						count2 + 1,
						count2,
						count2 + 3,
						count2 + 2
					});
				}
			}
		}
		UnityEngine.Object.Destroy(gameObject3);
		if (num != 0)
		{
			Mesh mesh = new Mesh
			{
				name = sharedMesh.name,
				indexFormat = ((list.Count > 65000) ? IndexFormat.UInt32 : IndexFormat.UInt16)
			};
			mesh.SetVertices(list);
			mesh.SetUVs(0, list2);
			mesh.SetTriangles(list3, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateTangents();
			mesh.RecalculateBounds();
			component.sharedMesh = mesh;
			Debug.Log($"ROAD_SINK {meshName} under {roadName} quads_split={num} verts {vertices.Length}->{list.Count}");
		}
		float Sunk(Vector3 w)
		{
			if (w.x < rb.min.x || w.x > rb.max.x || w.z < rb.min.z || w.z > rb.max.z)
			{
				return float.NaN;
			}
			if (!mc.Raycast(new Ray(new Vector3(w.x, w.y + 0.5f, w.z), Vector3.down), out var hitInfo, 0.8f))
			{
				return float.NaN;
			}
			if (w.y < hitInfo.point.y - depth || w.y > hitInfo.point.y + 0.12f)
			{
				return float.NaN;
			}
			return hitInfo.point.y - depth;
		}
	}

	private static Mesh ExpandOuterEdge(Mesh src, float amount)
	{
		Mesh mesh = UnityEngine.Object.Instantiate(src);
		mesh.name = src.name;
		Vector3[] vertices = mesh.vertices;
		int[] triangles = mesh.GetTriangles(0);
		Dictionary<(Vector3Int, Vector3Int), int> dictionary = new Dictionary<(Vector3Int, Vector3Int), int>();
		for (int i = 0; i < triangles.Length; i += 3)
		{
			for (int j = 0; j < 3; j++)
			{
				(Vector3Int, Vector3Int) key = E(Key(vertices[triangles[i + j]]), Key(vertices[triangles[i + (j + 1) % 3]]));
				dictionary[key] = ((!dictionary.TryGetValue(key, out var value)) ? 1 : (value + 1));
			}
		}
		Dictionary<Vector3Int, Vector2> dictionary2 = new Dictionary<Vector3Int, Vector2>();
		for (int k = 0; k < triangles.Length; k += 3)
		{
			for (int l = 0; l < 3; l++)
			{
				Vector3 p2 = vertices[triangles[k + l]];
				Vector3 p3 = vertices[triangles[k + (l + 1) % 3]];
				Vector3 vector = vertices[triangles[k + (l + 2) % 3]];
				Vector3Int vector3Int = Key(p2);
				Vector3Int vector3Int2 = Key(p3);
				if (dictionary[E(vector3Int, vector3Int2)] != 1)
				{
					continue;
				}
				Vector2 vector2 = new Vector2(p3.x - p2.x, p3.z - p2.z);
				if (!(vector2.sqrMagnitude < 1E-08f))
				{
					Vector2 vector3 = new Vector2(vector2.y, 0f - vector2.x).normalized;
					if (Vector2.Dot(vector3, new Vector2(vector.x - p2.x, vector.z - p2.z)) > 0f)
					{
						vector3 = -vector3;
					}
					dictionary2[vector3Int] = (dictionary2.TryGetValue(vector3Int, out var value2) ? value2 : Vector2.zero) + vector3;
					dictionary2[vector3Int2] = (dictionary2.TryGetValue(vector3Int2, out var value3) ? value3 : Vector2.zero) + vector3;
				}
			}
		}
		if (dictionary2.Count == 0)
		{
			Vector3[] normals = mesh.normals;
			if (normals != null && normals.Length == vertices.Length)
			{
				for (int m = 0; m < vertices.Length; m++)
				{
					Vector2 vector4 = new Vector2(normals[m].x, normals[m].z);
					if (!(vector4.sqrMagnitude < 0.25f))
					{
						Vector3Int key2 = Key(vertices[m]);
						dictionary2[key2] = (dictionary2.TryGetValue(key2, out var value4) ? value4 : Vector2.zero) + vector4.normalized;
					}
				}
			}
		}
		int num = 0;
		for (int n = 0; n < vertices.Length; n++)
		{
			if (dictionary2.TryGetValue(Key(vertices[n]), out var value5) && !(value5.sqrMagnitude < 1E-06f))
			{
				value5.Normalize();
				vertices[n] += new Vector3(value5.x, 0f, value5.y) * amount;
				num++;
			}
		}
		mesh.vertices = vertices;
		mesh.RecalculateBounds();
		Debug.Log($"ROAD_EDGE_EXPAND {src.name} moved={num}");
		return mesh;
		static (Vector3Int, Vector3Int) E(Vector3Int a, Vector3Int b)
		{
			if (a.x >= b.x && (a.x != b.x || (a.y >= b.y && (a.y != b.y || a.z > b.z))))
			{
				return (b, a);
			}
			return (a, b);
		}
		static Vector3Int Key(Vector3 p)
		{
			return new Vector3Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));
		}
	}

	private void CollectObstacles()
	{
		colliders.Clear();
		TownObstacle[] array = UnityEngine.Object.FindObjectsOfType<TownObstacle>();
		foreach (TownObstacle townObstacle in array)
		{
			BoxCollider component = townObstacle.GetComponent<BoxCollider>();
			if (!(component == null))
			{
				Transform transform = townObstacle.transform;
				Vector3 vector = transform.TransformPoint(component.center);
				Vector3 vector2 = Vector3.Scale(component.size, transform.lossyScale);
				EggboyCollider eggboyCollider = new EggboyCollider
				{
					x = vector.x,
					z = vector.z,
					hw = vector2.x * 0.5f,
					hd = vector2.z * 0.5f,
					water = (townObstacle.kind == TownObstacle.Kind.Water),
					minY = townObstacle.minY
				};
				if (townObstacle.kind == TownObstacle.Kind.WalkableTop)
				{
					eggboyCollider.top = vector.y + vector2.y * 0.5f;
				}
				colliders.Add(eggboyCollider);
			}
		}
		TownObstacleSet[] array2 = UnityEngine.Object.FindObjectsOfType<TownObstacleSet>();
		foreach (TownObstacleSet townObstacleSet in array2)
		{
			for (int j = 0; j < townObstacleSet.x.Length; j++)
			{
				colliders.Add(new EggboyCollider
				{
					x = townObstacleSet.x[j],
					z = townObstacleSet.z[j],
					r = townObstacleSet.r[j],
					top = townObstacleSet.top[j]
				});
			}
		}
		spatialDirty = true;
	}

	public float WadeLimit(float x, float z, float ground)
	{
		if (!IsBeachSurface(x, z, ground))
		{
			return 0.05f;
		}
		return 0.18f;
	}

	public bool Walkable(float x, float z)
	{
		float ground;
		if (walkPolys.Count != 0)
		{
			return TryWalkableGround(x, z, out ground);
		}
		return true;
	}

	private bool TryWalkableGround(float x, float z, out float ground)
	{
		ground = 0f;
		bool flag = InWalkArea(x, z);
		bool flag2 = !flag && InLawnArea(x, z);
		bool flag3 = beachPoly != null && Contains(beachPoly.points, x, z);
		if (!flag && !flag2 && !flag3)
		{
			return false;
		}
		ground = GroundAt(x, z);
		if (ground < WaterY - WadeLimit(x, z, ground))
		{
			return false;
		}
		if (!flag3 || !(Mathf.Abs(ground - beachPoly.y) <= 0.12f))
		{
			if (!flag)
			{
				if (flag2)
				{
					return ground >= 2.5f;
				}
				return false;
			}
			return ground > 0.22f;
		}
		return true;
	}

	public bool IsBeachSurface(float x, float z, float height)
	{
		if (beachPoly != null && Mathf.Abs(height - beachPoly.y) <= 0.12f)
		{
			return Contains(beachPoly.points, x, z);
		}
		return false;
	}

	private void LoadRoamBlocks()
	{
		if (roamBlocks != null)
		{
			return;
		}
		TextAsset textAsset = Resources.Load<TextAsset>("World/roam_blocks");
		RoamBlockDoc roamBlockDoc = ((textAsset != null) ? JsonUtility.FromJson<RoamBlockDoc>(textAsset.text) : null);
		roamBlocks = ((roamBlockDoc != null && roamBlockDoc.blocks != null) ? roamBlockDoc.blocks : new RoamBlock[0]);
		RoamBlock[] array = roamBlocks;
		foreach (RoamBlock roamBlock in array)
		{
			float[] x = roamBlock.x;
			int a = ((x != null) ? x.Length : 0);
			float[] z = roamBlock.z;
			roamBlock.pts = new Vector2[Mathf.Min(a, (z != null) ? z.Length : 0)];
			for (int j = 0; j < roamBlock.pts.Length; j++)
			{
				roamBlock.pts[j] = new Vector2(roamBlock.x[j], roamBlock.z[j]);
			}
			if (roamBlock.when != null && roamBlock.when.StartsWith("water>=", StringComparison.Ordinal) && float.TryParse(roamBlock.when.Substring(7), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				roamBlock.waterMin = result;
			}
		}
	}

	public bool InRoamBlock(float x, float z, float ground)
	{
		LoadRoamBlocks();
		RoamBlock[] array = roamBlocks;
		foreach (RoamBlock roamBlock in array)
		{
			if (ground < roamBlock.minY || ground > roamBlock.maxY || !Contains(roamBlock.pts, x, z))
			{
				continue;
			}
			if (!float.IsNaN(roamBlock.waterMin))
			{
				if (WaterLevel.StaticY < roamBlock.waterMin)
				{
					continue;
				}
			}
			else if (!string.IsNullOrEmpty(roamBlock.when) && roamBlock.when != "always" && RoamCondition != null && !RoamCondition(roamBlock.when))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	public float SurfaceBelow(float x, float z, float fromY)
	{
		float num = float.NaN;
		RaycastHit[] array = Physics.RaycastAll(new Vector3(x, fromY, z), Vector3.down, fromY + 60f, groundMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if (float.IsNaN(num) || raycastHit.point.y > num)
			{
				num = raycastHit.point.y;
			}
		}
		return num;
	}

	public bool RoamStep(Vector3 from, float x, float z, bool jumping)
	{
		float num = SurfaceBelow(x, z, from.y + 1.3f);
		if (float.IsNaN(num))
		{
			return false;
		}
		if (num - from.y > (jumping ? 0.28f : 0.23f))
		{
			return false;
		}
		if (RockWallAt(x, z, from.y))
		{
			return false;
		}
		if (RockHeadroomBlocked(x, z, num))
		{
			return false;
		}
		if (from.y - num > (jumping ? 3.45f : 0.6f))
		{
			return false;
		}
		if (num < WaterY - WadeLimit(x, z, num))
		{
			return false;
		}
		if (InRoamBlock(x, z, num))
		{
			return false;
		}
		return true;
	}

	public bool RockWallAt(float x, float z, float feetY)
	{
		float num = feetY + 40f;
		int num2 = Physics.RaycastNonAlloc(new Vector3(x, num, z), Vector3.down, rockWallHits, num - (feetY + 1.3f), 256, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < num2; i++)
		{
			Collider collider = rockWallHits[i].collider;
			if (collider.name != "RockGapLid" && collider.bounds.min.y < feetY + 0.25f)
			{
				return true;
			}
		}
		return false;
	}

	public bool RockHeadroomBlocked(float x, float z, float feetY)
	{
		if (float.IsNaN(feetY))
		{
			return false;
		}
		if (Physics.Raycast(new Vector3(x, feetY + 0.15f, z), Vector3.up, out var hitInfo, 1.15f, 256, QueryTriggerInteraction.Ignore))
		{
			return hitInfo.collider.name != "RockGapLid";
		}
		return false;
	}

	public bool RoamStandable(float x, float z, float nearY)
	{
		float num = SurfaceBelow(x, z, nearY + 1.3f);
		if (!float.IsNaN(num) && num >= WaterY - WadeLimit(x, z, num))
		{
			return !InRoamBlock(x, z, num);
		}
		return false;
	}

	public bool CanTraverse(Vector3 from, float x, float z, bool jumping)
	{
		if (walkPolys.Count == 0)
		{
			return true;
		}
		if (FreeRoam)
		{
			return RoamStep(from, x, z, jumping);
		}
		if (!TryWalkableGround(x, z, out var ground))
		{
			if (jumping && beachPoly != null && from.y >= beachPoly.y + 0.55f)
			{
				return IsDryBeachApproach(x, z);
			}
			return false;
		}
		if (beachPoly == null)
		{
			return true;
		}
		bool flag = from.y < beachPoly.y + 0.65f && Contains(beachPoly.points, from.x, from.z);
		if (IsBeachSurface(x, z, ground))
		{
			return flag || jumping;
		}
		if (flag)
		{
			return ground <= from.y + 0.3f;
		}
		return true;
	}

	private bool IsDryBeachApproach(float x, float z)
	{
		if (beachPoly?.points == null || beachPoly.points.Length < 3 || WaterY > beachPoly.y + 0.18f)
		{
			return false;
		}
		Vector2 vector = new Vector2(x, z);
		Vector2[] points = beachPoly.points;
		int num = 0;
		int num2 = points.Length - 1;
		while (num < points.Length)
		{
			Vector2 vector2 = points[num] - points[num2];
			float num3 = Mathf.Clamp01(Vector2.Dot(vector - points[num2], vector2) / Mathf.Max(vector2.sqrMagnitude, 0.0001f));
			if (!((vector - points[num2] - num3 * vector2).sqrMagnitude >= 0.80999994f))
			{
				float num4 = GroundAt(x, z);
				if (num4 > 0.05f)
				{
					return num4 <= beachPoly.y + 0.1f;
				}
				return false;
			}
			num2 = num++;
		}
		return false;
	}

	public bool InWalkArea(float x, float z)
	{
		foreach (WalkPoly walkPoly in walkPolys)
		{
			if (Contains(walkPoly.points, x, z))
			{
				return true;
			}
		}
		return false;
	}

	public bool InLawnArea(float x, float z)
	{
		if (lawnPolys == null)
		{
			return false;
		}
		foreach (WalkPoly lawnPoly in lawnPolys)
		{
			if (Contains(lawnPoly.points, x, z))
			{
				return true;
			}
		}
		return false;
	}

	public float GroundAt(float x, float z)
	{
		return GroundAt(x, z, float.PositiveInfinity);
	}

	public float GroundAt(float x, float z, float aboveY)
	{
		float num = -1f;
		RaycastHit[] array = Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f, groundMask, QueryTriggerInteraction.Ignore);
		foreach (RaycastHit raycastHit in array)
		{
			float y = raycastHit.point.y;
			if (y <= aboveY + 0.3f && y > num)
			{
				num = y;
			}
		}
		if (!(num < -0.5f))
		{
			return num;
		}
		return 0f;
	}

	private static bool Contains(Vector2[] poly, float x, float z)
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
}
