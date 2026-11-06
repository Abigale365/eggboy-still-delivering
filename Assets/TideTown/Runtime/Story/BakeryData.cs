// WP-11 面包店的道具位置与回忆文字，运行时和编辑器共用。
using System;
using UnityEngine;

[Serializable] public class BakeryItem { public string id, label, text; public float x, y, z; }
[Serializable] public class BakeryList { public BakeryItem[] items; }

public static class BakeryData
{
    public static BakeryItem[] Load()
    {
        var ta = Resources.Load<TextAsset>("Story/bakery");
        if (ta == null) return new BakeryItem[0];
        var list = JsonUtility.FromJson<BakeryList>(ta.text);
        return list != null && list.items != null ? list.items : new BakeryItem[0];
    }
}
