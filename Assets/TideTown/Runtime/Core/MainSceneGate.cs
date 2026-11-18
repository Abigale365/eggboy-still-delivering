using System;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class MainSceneGate
{
	public const string MainSceneName = "TideTown_Main";

	public static bool MainIsActive => SceneManager.GetActiveScene().name == "TideTown_Main";

	public static void WhenMainLoaded(Action action)
	{
		if (action == null)
		{
			return;
		}
		if (MainIsActive)
		{
			action();
			return;
		}
		UnityAction<Scene, LoadSceneMode> handler = null;
		handler = delegate(Scene scene, LoadSceneMode mode)
		{
			if (!(scene.name != "TideTown_Main"))
			{
				SceneManager.sceneLoaded -= handler;
				action();
			}
		};
		SceneManager.sceneLoaded += handler;
	}
}
