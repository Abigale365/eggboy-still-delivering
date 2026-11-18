using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuiltinModelProvider : IModelProvider
{
	private readonly Dictionary<string, GameObject> table = new Dictionary<string, GameObject>();

	public void Register(string itemId, GameObject prefab)
	{
		if (!string.IsNullOrEmpty(itemId) && prefab != null)
		{
			table[itemId] = prefab;
		}
	}

	public void Load(string itemId, Action<GameObject> done, Action<string> fail)
	{
		if (string.IsNullOrEmpty(itemId))
		{
			fail?.Invoke("缺少 itemId");
			return;
		}
		GameObject gameObject = (table.ContainsKey(itemId) ? table[itemId] : Resources.Load<GameObject>("Items/" + itemId));
		if (gameObject == null)
		{
			fail?.Invoke("没有物品模型: " + itemId);
		}
		else
		{
			done?.Invoke(gameObject);
		}
	}
}
