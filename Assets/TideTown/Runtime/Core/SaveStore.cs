// WP-07 存档：PlayerPrefs 存 JSON，Windows 和网页都能用。
using UnityEngine;

public sealed class SaveStore
{
    const string Key = "tidetown.save.v1";

    public GameState Load()
    {
        var json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var s = JsonUtility.FromJson<GameState>(json);
            if (s == null || s.schemaVersion != 1) return null;
            return s;
        }
        catch
        {
            return null;
        }
    }

    public void Save(GameState s)
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
