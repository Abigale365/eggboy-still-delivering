using System.Collections.Generic;
using UnityEngine;

public sealed class LampGlow : MonoBehaviour
{
	public Light lamp;

	public Renderer halo;

	private static readonly List<LampGlow> All = new List<LampGlow>();

	public static IReadOnlyList<LampGlow> Lamps => All;

	private void OnEnable()
	{
		All.Add(this);
	}

	private void OnDisable()
	{
		All.Remove(this);
	}
}
