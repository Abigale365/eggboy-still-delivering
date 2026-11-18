using System.Collections.Generic;
using UnityEngine;

public static class HoldZones
{
	private struct Zone
	{
		public string id;

		public Vector3 pos;

		public float radius;
	}

	private static readonly List<Zone> zones = new List<Zone>();

	public static void Register(string id, Vector3 pos, float radius)
	{
		for (int i = 0; i < zones.Count; i++)
		{
			if (zones[i].id == id)
			{
				zones[i] = new Zone
				{
					id = id,
					pos = pos,
					radius = radius
				};
				return;
			}
		}
		zones.Add(new Zone
		{
			id = id,
			pos = pos,
			radius = radius
		});
	}

	public static string At(Vector3 p)
	{
		foreach (Zone zone in zones)
		{
			if (new Vector2(p.x - zone.pos.x, p.z - zone.pos.z).magnitude <= zone.radius && Mathf.Abs(p.y - zone.pos.y) < 2f)
			{
				return zone.id;
			}
		}
		return "";
	}

	public static void Clear()
	{
		zones.Clear();
	}
}
