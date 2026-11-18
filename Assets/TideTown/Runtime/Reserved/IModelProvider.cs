using System;
using UnityEngine;

public interface IModelProvider
{
	void Load(string itemId, Action<GameObject> done, Action<string> fail);
}
