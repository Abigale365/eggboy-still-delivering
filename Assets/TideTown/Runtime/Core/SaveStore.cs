using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SaveStore
{
	private static readonly bool Trailer = Array.Exists(Environment.GetCommandLineArgs(), (string a) => a == "-trailerRecord");

	private static readonly string NormalKey = (Array.Exists(Environment.GetCommandLineArgs(), IsAudit) ? "tidetown.qa.save.v1" : "tidetown.save.v1");

	private static string Key
	{
		get
		{
			if (!Trailer && !FilmingStageJumper.ActiveSession)
			{
				return NormalKey;
			}
			return "tidetown.filming.save.v1";
		}
	}

	private static bool IsAudit(string arg)
	{
		if (GameBootstrap.IsRouteAuditFlag(arg))
		{
			return true;
		}
		switch (arg)
		{
		case "-storyaudit":
		case "-traversalaudit":
		case "-mapaudit":
		case "-wateraudit":
		case "-innovaudit":
		case "-scanaudit":
		case "-shot":
		case "-feeldemo":
			return true;
		default:
			return false;
		}
	}

	public GameState Load()
	{
		if (Trailer)
		{
			return null;
		}
		string @string = PlayerPrefs.GetString(Key, "");
		if (string.IsNullOrEmpty(@string))
		{
			return null;
		}
		try
		{
			GameState gameState = JsonUtility.FromJson<GameState>(@string);
			if (gameState == null || gameState.schemaVersion != 1)
			{
				return null;
			}
			if (gameState.seenTutorials == null)
			{
				gameState.seenTutorials = new List<string>();
			}
			return gameState;
		}
		catch
		{
			return null;
		}
	}

	public void Save(GameState s)
	{
		if (s.seenTutorials == null)
		{
			s.seenTutorials = new List<string>();
		}
		PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
		PlayerPrefs.Save();
	}

	public void Clear()
	{
		PlayerPrefs.DeleteKey(Key);
		PlayerPrefs.Save();
	}
}
