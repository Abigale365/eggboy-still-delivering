using System.Collections.Generic;
using UnityEngine;

public class TownWorld : MonoBehaviour, IEggboyWorld
{
	public LayerMask groundMask;

	public LayerMask obstacleMask;

	public Transform mailboxMarker;

	public Transform spawnMarker;

	[Header("可行走区域（世界坐标，可多个矩形）")]
	public List<Rect> walkRects = new List<Rect>();

	private const float Foot = 0.12f;

	private readonly List<EggboyCollider> colliders = new List<EggboyCollider>();

	private float[] groundCache;

	private int cacheW;

	private int cacheH;

	private Vector3 cacheOrigin;

	private float cacheStep = 1f;

	public IReadOnlyList<EggboyCollider> Colliders => colliders;

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

	public void QueryColliders(float x, float z, List<EggboyCollider> results)
	{
		results.Clear();
		results.AddRange(colliders);
	}

	private void Awake()
	{
		if ((int)groundMask == 0)
		{
			groundMask = LayerMask.GetMask("Ground");
		}
		if ((int)obstacleMask == 0)
		{
			obstacleMask = LayerMask.GetMask("TownObstacle");
		}
		CollectObstacles();
		EggboyEnv.Apply(base.transform);
	}

	private void CollectObstacles()
	{
		colliders.Clear();
		TownObstacle[] array = Object.FindObjectsOfType<TownObstacle>();
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
					water = (townObstacle.kind == TownObstacle.Kind.Water)
				};
				if (townObstacle.kind == TownObstacle.Kind.WalkableTop)
				{
					eggboyCollider.top = vector.y + vector2.y * 0.5f;
				}
				colliders.Add(eggboyCollider);
			}
		}
	}

	public bool Walkable(float x, float z)
	{
		if (walkRects.Count == 0)
		{
			return true;
		}
		foreach (Rect walkRect in walkRects)
		{
			if (x >= walkRect.xMin && x <= walkRect.xMax && z >= walkRect.yMin && z <= walkRect.yMax)
			{
				return true;
			}
		}
		return false;
	}

	public float GroundAt(float x, float z, float aboveY)
	{
		return GroundAt(x, z);
	}

	public float GroundAt(float x, float z)
	{
		if (groundCache != null)
		{
			int num = Mathf.RoundToInt((x - cacheOrigin.x) / cacheStep);
			int num2 = Mathf.RoundToInt((z - cacheOrigin.z) / cacheStep);
			if (num >= 0 && num < cacheW && num2 >= 0 && num2 < cacheH)
			{
				return groundCache[num2 * cacheW + num];
			}
		}
		if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out var hitInfo, 400f, groundMask, QueryTriggerInteraction.Ignore))
		{
			return hitInfo.point.y;
		}
		return 0f;
	}

	[ContextMenu("烘焙地面高度")]
	public void BakeGround()
	{
		if (walkRects.Count == 0)
		{
			Debug.LogError("没有可行走矩形，无法烘焙地面");
			return;
		}
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MinValue;
		foreach (Rect walkRect in walkRects)
		{
			num = Mathf.Min(num, walkRect.xMin);
			num2 = Mathf.Min(num2, walkRect.yMin);
			num3 = Mathf.Max(num3, walkRect.xMax);
			num4 = Mathf.Max(num4, walkRect.yMax);
		}
		cacheOrigin = new Vector3(num, 0f, num2);
		cacheW = Mathf.CeilToInt((num3 - num) / cacheStep) + 1;
		cacheH = Mathf.CeilToInt((num4 - num2) / cacheStep) + 1;
		groundCache = new float[cacheW * cacheH];
		int num5 = 0;
		for (int i = 0; i < cacheH; i++)
		{
			for (int j = 0; j < cacheW; j++)
			{
				float x = cacheOrigin.x + (float)j * cacheStep;
				float z = cacheOrigin.z + (float)i * cacheStep;
				float num6 = 0f;
				if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out var hitInfo, 400f, groundMask, QueryTriggerInteraction.Ignore))
				{
					num6 = hitInfo.point.y;
					num5++;
				}
				groundCache[i * cacheW + j] = num6;
			}
		}
		Debug.Log($"地面烘焙完成 {cacheW}x{cacheH}，命中 {num5} 个采样点");
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(0.3f, 0.9f, 0.6f, 0.35f);
		foreach (Rect walkRect in walkRects)
		{
			Gizmos.DrawWireCube(new Vector3(walkRect.center.x, 0.2f, walkRect.center.y), new Vector3(walkRect.width, 0.4f, walkRect.height));
		}
	}
}
