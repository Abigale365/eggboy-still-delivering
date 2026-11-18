using System;

public static class Features
{
	public static bool scan = HasArg("-scan");

	public static bool debugUi = HasArg("-debugui");

	public static bool classicAudit = HasArg("-storyaudit") || Array.Exists(Environment.GetCommandLineArgs(), GameBootstrap.IsRouteAuditFlag) || HasArg("-wateraudit") || HasArg("-mapaudit") || HasArg("-scanaudit") || HasArg("-shot");

	public static bool crossingGap = !HasArg("-nogap");

	public static bool oldModes = HasArg("-oldmodes");

	public static bool freeRoam = !HasArg("-whitelist");

	private static bool HasArg(string key)
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == key)
			{
				return true;
			}
		}
		return false;
	}
}
