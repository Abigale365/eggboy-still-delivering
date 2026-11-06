// WP-09 预留：物品模型来源。以后接扫描/下载只换实现，玩法代码不动。
using System;
using System.Collections.Generic;
using UnityEngine;

public interface IModelProvider
{
    void Load(string itemId, Action<GameObject> done, Action<string> fail);
}

// 最小实现：先查引用表，再查 Resources/Items/<itemId>。没有就 fail，不联网、不下载。
public sealed class BuiltinModelProvider : IModelProvider
{
    readonly Dictionary<string, GameObject> table = new Dictionary<string, GameObject>();

    public void Register(string itemId, GameObject prefab)
    {
        if (!string.IsNullOrEmpty(itemId) && prefab != null) table[itemId] = prefab;
    }

    public void Load(string itemId, Action<GameObject> done, Action<string> fail)
    {
        if (string.IsNullOrEmpty(itemId)) { if (fail != null) fail("缺少 itemId"); return; }
        GameObject prefab = table.ContainsKey(itemId) ? table[itemId] : Resources.Load<GameObject>("Items/" + itemId);
        if (prefab == null) { if (fail != null) fail("没有物品模型: " + itemId); return; }
        if (done != null) done(prefab);
    }
}
