using System;
using UnityEngine;

public sealed class Lux3DSpawn
{
	public GameObject root;

	public Lux3DLibraryEntry entry;

	public string prompt;

	public string state = "fallback";

	public float seconds;

	public Action<Lux3DSpawn> onSwapped;
}
