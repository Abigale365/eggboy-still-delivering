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

	public List<string> seenTutorials = new List<string>();

	public float waterLevel;

	public string playerItemId;

	public string blackboardChalk;
}
