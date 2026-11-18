using GLTFast;
using UnityEngine;

public sealed class GltfImportLifetime : MonoBehaviour
{
	public GltfImport import;

	private void OnDestroy()
	{
		if (import != null)
		{
			import.Dispose();
			import = null;
		}
	}
}
