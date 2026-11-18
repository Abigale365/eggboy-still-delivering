using System;
using System.Collections;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Rendering;

public static class RuntimeGlbLoader
{
	public static IEnumerator Load(byte[] glb, Action<GameObject> done, Action<string> fail)
	{
		if (glb == null || glb.Length < 20)
		{
			fail?.Invoke("empty glb");
			yield break;
		}
		GltfImport import = new GltfImport();
		Task<bool> loadTask = import.LoadGltfBinary(glb);
		while (!loadTask.IsCompleted)
		{
			yield return null;
		}
		if (loadTask.IsFaulted || !loadTask.Result)
		{
			fail?.Invoke("glTFast load failed" + ((loadTask.Exception != null) ? (": " + loadTask.Exception.GetBaseException().Message) : ""));
			import.Dispose();
			yield break;
		}
		GameObject holder = new GameObject("Model");
		Task<bool> instTask = import.InstantiateMainSceneAsync(holder.transform);
		while (!instTask.IsCompleted)
		{
			yield return null;
		}
		if (instTask.IsFaulted || !instTask.Result)
		{
			UnityEngine.Object.Destroy(holder);
			fail?.Invoke("glTFast instantiate failed");
			import.Dispose();
			yield break;
		}
		holder.AddComponent<GltfImportLifetime>().import = import;
		Renderer[] componentsInChildren = holder.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer obj in componentsInChildren)
		{
			obj.shadowCastingMode = ShadowCastingMode.On;
			obj.receiveShadows = true;
			Material[] materials = obj.materials;
			foreach (Material material in materials)
			{
				if (material.HasProperty("_Cull"))
				{
					material.SetFloat("_Cull", 0f);
				}
				material.doubleSidedGI = true;
			}
		}
		done?.Invoke(holder);
	}
}
