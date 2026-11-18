using UnityEngine;
using UnityEngine.Rendering;

public class EggboyFace
{
	public readonly FacePainter Painter = new FacePainter();

	private readonly EggboySkin skin;

	private readonly Transform model;

	private readonly Transform body;

	private Transform screen;

	private Transform symbol;

	private Material screenMat;

	private Material symbolMat;

	public EggboyFace(EggboyPlayer player)
	{
		skin = player.Skin;
		model = player.ModelObject.transform;
		body = ((player.Bones != null) ? player.Bones.Bone("body") : null);
		if (skin == null || skin.renderer == null || body == null)
		{
			Debug.LogError("面屏表情：缺少 Skin / body 骨头，表情不可用");
			return;
		}
		BuildScreen();
		BuildSymbol(player.transform);
		Set(ExprId.happy);
	}

	public void Set(ExprId id)
	{
		Painter.Set(id);
	}

	public void Update(float dt, Camera cam)
	{
		Painter.Update(dt);
		if (symbol == null)
		{
			return;
		}
		bool flag = Painter.Symbol != FaceSymbol.None;
		symbol.gameObject.SetActive(flag);
		if (!flag)
		{
			return;
		}
		float x = Painter.SymbolPopScale.x;
		symbol.localScale = Vector3.one * x;
		float num = 1f + Mathf.Sin(Painter.Time * 2.2f) * 0.025f;
		if (Painter.Symbol == FaceSymbol.Zz)
		{
			num += Painter.Time * 0.15f % 0.12f;
		}
		symbol.localPosition = new Vector3(-0.3f, num, 0.05f);
		if (cam != null)
		{
			Vector3 vector = cam.transform.position - symbol.position;
			if (vector.sqrMagnitude > 1E-06f)
			{
				symbol.rotation = Quaternion.LookRotation(vector.normalized, Vector3.up);
			}
		}
	}

	private void BuildScreen()
	{
		if (!EggboyFaceSurface.Build(skin, model, Painter.Texture, out screen))
		{
			Debug.LogError("面屏表情贴合失败，请检查模型与图集导入设置");
		}
	}

	private void BuildSymbol(Transform playerRoot)
	{
		GameObject gameObject = new GameObject("FaceSymbol");
		gameObject.transform.SetParent(playerRoot, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(-0.3f, 1f, 0.05f);
		gameObject.transform.localScale = Vector3.one * 0.34f;
		MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		Mesh mesh = new Mesh
		{
			name = "SymbolQuad"
		};
		mesh.vertices = new Vector3[4]
		{
			new Vector3(-0.5f, -0.5f, 0f),
			new Vector3(0.5f, -0.5f, 0f),
			new Vector3(-0.5f, 0.5f, 0f),
			new Vector3(0.5f, 0.5f, 0f)
		};
		mesh.uv = new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(0f, 1f),
			new Vector2(1f, 1f)
		};
		mesh.triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
		mesh.RecalculateBounds();
		meshFilter.sharedMesh = mesh;
		symbolMat = TransparentMaterial(Painter.SymbolTexture, "eggboy-symbol");
		if (symbolMat == null)
		{
			Object.Destroy(gameObject);
			return;
		}
		meshRenderer.sharedMaterial = symbolMat;
		symbol = gameObject.transform;
		symbol.gameObject.SetActive(value: false);
	}

	private Vector3 ToBody(Vector3 modelLocal)
	{
		Vector3 position = model.TransformPoint(modelLocal);
		return body.InverseTransformPoint(position);
	}

	private static Material TransparentMaterial(Texture tex, string name)
	{
		Shader shader = Resources.Load<Shader>("EggboyURPUnlitTransparent");
		if (shader == null)
		{
			shader = Resources.Load<Shader>("EggboyUnlitTransparent");
		}
		if (shader == null)
		{
			shader = Shader.Find("Universal Render Pipeline/Unlit");
		}
		if (shader == null)
		{
			shader = Shader.Find("Unlit/Transparent");
		}
		if (shader == null)
		{
			shader = Shader.Find("Sprites/Default");
		}
		if (shader == null)
		{
			Debug.LogError("面屏表情：找不到可用的透明 Shader");
			return null;
		}
		Material material = new Material(shader);
		if (material.HasProperty("_MainTex"))
		{
			material.SetTexture("_MainTex", tex);
		}
		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", Color.white);
		}
		if (material.HasProperty("_BaseMap"))
		{
			material.SetTexture("_BaseMap", tex);
		}
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", Color.white);
		}
		material.renderQueue = 3100;
		material.name = name;
		return material;
	}
}
