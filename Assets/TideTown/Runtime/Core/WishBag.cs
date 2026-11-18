using System.Collections.Generic;
using UnityEngine;

public static class WishBag
{
	public const int Capacity = 6;

	private static readonly List<WishToy> items = new List<WishToy>();

	public static int Count
	{
		get
		{
			items.RemoveAll((WishToy t) => t == null);
			return items.Count;
		}
	}

	public static bool Full => Count >= 6;

	public static IReadOnlyList<WishToy> Items => items;

	public static void Add(WishToy t)
	{
		if (t != null && !items.Contains(t))
		{
			items.Add(t);
		}
	}

	public static string Summary()
	{
		items.RemoveAll((WishToy t) => t == null);
		if (items.Count == 0)
		{
			return "邮差包里还空着（0/" + 6 + "）";
		}
		List<string> list = new List<string>();
		foreach (WishToy item in items)
		{
			list.Add("「" + item.description + "」");
		}
		return "邮差包（" + items.Count + "/" + 6 + "）：" + string.Join("、", list);
	}

	public static WishToy TakeOutLatest(EggboyPlayer p)
	{
		items.RemoveAll((WishToy t) => t == null);
		if (items.Count == 0 || p == null)
		{
			return null;
		}
		WishToy wishToy = items[items.Count - 1];
		items.RemoveAt(items.Count - 1);
		Vector3 vector = new Vector3(Mathf.Sin(p.yaw), 0f, Mathf.Cos(p.yaw));
		Vector3 at = p.transform.position + vector * 1.1f;
		IslandWorld islandWorld = Object.FindObjectOfType<IslandWorld>();
		if (islandWorld != null)
		{
			at.y = islandWorld.GroundAt(at.x, at.z, p.transform.position.y + 0.6f);
		}
		wishToy.TakeOutAt(at, p.yaw * 57.29578f + 180f);
		return wishToy;
	}
}
