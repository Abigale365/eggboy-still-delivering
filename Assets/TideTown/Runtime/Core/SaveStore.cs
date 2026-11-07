// WP-07 存档：PlayerPrefs 存 JSON，Windows 和网页都能用。
using System;
using UnityEngine;

public sealed class SaveStore
{
    static readonly string Key = Array.Exists(Environment.GetCommandLineArgs(), IsAudit)
        ? "tidetown.qa.save.v1" : "tidetown.save.v1";

    static bool IsAudit(string arg)
    {
        switch (arg)
        {
            case "-storyaudit":
            case "-routeaudit":
            case "-routeaudit2":
            case "-mapaudit":
            case "-wateraudit":
            case "-scanaudit":
            case "-shot":
                return true;
            default:
                return false;
        }
    }

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
