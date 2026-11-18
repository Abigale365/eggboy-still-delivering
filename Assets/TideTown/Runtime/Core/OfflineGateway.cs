using System;
using System.Collections;

public sealed class OfflineGateway : ILux3DGateway
{
	public string Name => "offline";

	public bool CanGenerate => false;

	public IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled)
	{
		result.ok = false;
		result.error = "offline";
		yield break;
	}
}
