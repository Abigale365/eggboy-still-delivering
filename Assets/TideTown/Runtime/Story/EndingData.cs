// WP-15：结局用的两个点 - 邮局门口的课桌、山那边的新城。
using System;
using UnityEngine;

[Serializable] public class EndingPoint { public float x, y, z; }
[Serializable] public class EndingList { public EndingPoint desk, city; }

public static class EndingData
{
    public static EndingList Load()
    {
        var asset = Resources.Load<TextAsset>("Story/ending");
        if (asset == null)
        {
            Debug.LogError("找不到 Resources/Story/ending.json");
            return new EndingList();
        }
        var list = JsonUtility.FromJson<EndingList>(asset.text);
        return list ?? new EndingList();
    }

    public static Vector3 Point(EndingPoint p)
    {
        return p != null ? new Vector3(p.x, p.y, p.z) : Vector3.zero;
    }
}
