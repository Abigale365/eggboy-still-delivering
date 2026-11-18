using System;
using System.Collections;
using UnityEngine;

public sealed class MockGateway : ILux3DGateway
{
	public float delaySeconds = 8f;

	public float failRate;

	public string Name => "mock";

	public bool CanGenerate => true;

	public IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled)
	{
		float t0 = Time.realtimeSinceStartup;
		while (Time.realtimeSinceStartup - t0 < delaySeconds)
		{
			if (cancelled != null && cancelled())
			{
				result.error = "cancelled";
				yield break;
			}
			yield return null;
		}
		result.seconds = Time.realtimeSinceStartup - t0;
		if (UnityEngine.Random.value < failRate)
		{
			result.error = "mock failure";
			yield break;
		}
		TextAsset textAsset = Resources.Load<TextAsset>("Lux3DMock/" + (string.IsNullOrEmpty(req.fallbackId) ? "default" : req.fallbackId));
		if (textAsset == null)
		{
			textAsset = Resources.Load<TextAsset>("Lux3DMock/default");
		}
		if (textAsset == null)
		{
			result.error = "mock: no glb bytes";
			yield break;
		}
		result.glb = textAsset.bytes;
		result.ok = true;
	}
}
