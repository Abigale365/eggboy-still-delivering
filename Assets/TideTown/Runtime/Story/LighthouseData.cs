// WP-14：灯塔的油壶/塔顶三件回忆位置，以及塔门、塔顶落点和灯头点。
using System;
using UnityEngine;

[Serializable] public class LighthouseItem { public string id, label, text; public float x, y, z; }
[Serializable] public class LighthousePoint { public float x, y, z; }
[Serializable] public class LighthouseList
{
    public LighthouseItem[] items;
    public LighthousePoint door, deck, lamp;
}

public static class LighthouseData
{
    public static LighthouseList Load()
    {
        var asset = Resources.Load<TextAsset>("Story/lighthouse");
        if (asset == null)
        {
            Debug.LogError("找不到 Resources/Story/lighthouse.json");
            return new LighthouseList { items = new LighthouseItem[0] };
        }
        var list = JsonUtility.FromJson<LighthouseList>(asset.text);
        if (list == null) return new LighthouseList { items = new LighthouseItem[0] };
        if (list.items == null) list.items = new LighthouseItem[0];
        return list;
    }

    public static Vector3 Point(LighthousePoint p)
    {
        return p != null ? new Vector3(p.x, p.y, p.z) : Vector3.zero;
    }
}
