using System;

[Serializable]
internal class Lux3DConfigFile
{
	public string mode = "offline";

	public string relayUrl = "";

	public int maxPerSession = 6;

	public float timeoutSec = 330f;

	public float mockDelaySec = 8f;
}
