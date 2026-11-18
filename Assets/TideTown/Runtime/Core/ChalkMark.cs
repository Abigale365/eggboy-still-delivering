using UnityEngine;

public static class ChalkMark
{
	private static Font font;

	public static GameObject Spawn(Vector3 at, string text, string id)
	{
		GameObject root = new GameObject("DogClueMark_" + id);
		root.transform.position = at + Vector3.up * 0.015f;
		Color color = new Color(0.98f, 0.95f, 0.86f);
		Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
		Material mat = new Material(shader);
		mat.color = color;
		if (mat.HasProperty("_BaseColor"))
		{
			mat.SetColor("_BaseColor", color);
		}
		Dot(new Vector3(0f, 0f, 0f), 0.14f);
		Dot(new Vector3(-0.08f, 0f, 0.1f), 0.05f);
		Dot(new Vector3(-0.03f, 0f, 0.13f), 0.05f);
		Dot(new Vector3(0.03f, 0f, 0.13f), 0.05f);
		Dot(new Vector3(0.08f, 0f, 0.1f), 0.05f);
		if (font == null)
		{
			font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
		}
		GameObject gameObject = new GameObject("Label");
		gameObject.transform.SetParent(root.transform, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(0f, 0.004f, -0.2f);
		Camera main = Camera.main;
		Vector3 vector = ((main != null) ? (main.transform.position - at) : Vector3.back);
		vector.y = 0f;
		if (vector.sqrMagnitude < 0.0001f)
		{
			vector = Vector3.back;
		}
		gameObject.transform.rotation = Quaternion.LookRotation(Vector3.down, -vector.normalized);
		TextMesh textMesh = gameObject.AddComponent<TextMesh>();
		textMesh.font = font;
		textMesh.text = text;
		textMesh.fontSize = 64;
		textMesh.characterSize = 0.012f;
		textMesh.anchor = TextAnchor.MiddleCenter;
		textMesh.color = color;
		if (font != null)
		{
			gameObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
		}
		return root;
		void Dot(Vector3 p, float s)
		{
			GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			Object.Destroy(gameObject2.GetComponent<Collider>());
			gameObject2.transform.SetParent(root.transform, worldPositionStays: false);
			gameObject2.transform.localPosition = p;
			gameObject2.transform.localScale = new Vector3(s, 0.002f, s);
			gameObject2.GetComponent<Renderer>().sharedMaterial = mat;
		}
	}
}
