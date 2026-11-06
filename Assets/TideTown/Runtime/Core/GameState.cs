// WP-07 存档状态。字段和工程书 6.1 一致。
using System;
using System.Collections.Generic;

[Serializable]
public class GameState
{
    public int schemaVersion = 1;
    public string step;
    public string carrying;
    public List<string> stamps = new List<string>();
    public List<string> flags = new List<string>();
    public List<string> seenMemories = new List<string>();
    public float waterLevel;
    public string playerItemId;
}
