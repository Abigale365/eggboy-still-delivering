// WP-13：学校道具的位置和回忆文字，和 bakery.json 一样走 Resources。
using System;
using System.IO;
using UnityEngine;

[Serializable] public class SchoolItem { public string id, label, text; public float x, y, z; }
[Serializable] public class SchoolPoint { public float x, y, z; }
[Serializable] public class SchoolList { public SchoolItem[] items; public SchoolPoint peek; }

public static class SchoolData
{
    public static SchoolList Load()
    {
        var asset = Resources.Load<TextAsset>("Story/school");
        if (asset == null)
        {
            Debug.LogError("找不到 Resources/Story/school.json");
            return new SchoolList { items = new SchoolItem[0] };
        }
        var list = JsonUtility.FromJson<SchoolList>(asset.text);
        if (list == null) return new SchoolList { items = new SchoolItem[0] };
        if (list.items == null) list.items = new SchoolItem[0];
        return list;
    }
}
