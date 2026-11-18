using System.Collections.Generic;
using UnityEngine;

public sealed class LampGlowDirector : MonoBehaviour
{
	public int maxLights = 6;

	public float lightIntensity = 3.2f;

	private float nextAt;

	private readonly List<LampGlow> sorted = new List<LampGlow>();

	private void Update()
	{
		if (Time.unscaledTime < nextAt)
		{
			return;
		}
		nextAt = Time.unscaledTime + 0.3f;
		Camera main = Camera.main;
		if (main == null)
		{
			return;
		}
		Vector3 p = main.transform.position;
		sorted.Clear();
		sorted.AddRange(LampGlow.Lamps);
		sorted.Sort((LampGlow a, LampGlow b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
		for (int i = 0; i < sorted.Count; i++)
		{
			Light lamp = sorted[i].lamp;
			if (!(lamp == null))
			{
				bool flag = i < maxLights;
				if (lamp.enabled != flag)
				{
					lamp.enabled = flag;
				}
				if (flag)
				{
					lamp.intensity = lightIntensity;
				}
			}
		}
	}
}
