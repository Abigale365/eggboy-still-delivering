// 表情复用原模型的弧形面屏表面和蒙皮权重，保留真实边框。
// 头顶符号单独朝向镜头。
using UnityEngine;

public class EggboyFace
{
    public readonly FacePainter Painter = new FacePainter();

    readonly EggboySkin skin;
    readonly Transform model;
    readonly Transform body;
    Transform screen, symbol;
    Material screenMat, symbolMat;

    public EggboyFace(EggboyPlayer player)
    {
        skin = player.Skin;
        model = player.ModelObject.transform;
        body = player.Bones != null ? player.Bones.Bone("body") : null;
        if (skin == null || skin.renderer == null || body == null)
        {
            Debug.LogError("面屏表情：缺少 Skin / body 骨头，表情不可用");
            return;
        }
        BuildScreen();
        BuildSymbol(player.transform);
        Set(ExprId.happy);
    }

    public void Set(ExprId id) => Painter.Set(id);

    public void Update(float dt, Camera cam)
    {
        Painter.Update(dt);
        if (symbol == null) return;
        bool show = Painter.Symbol != FaceSymbol.None;
        symbol.gameObject.SetActive(show);
        if (!show) return;
        float back = Painter.SymbolPopScale.x;
        symbol.localScale = Vector3.one * back;
        float y = 1.0f + Mathf.Sin(Painter.Time * 2.2f) * 0.025f;
        if (Painter.Symbol == FaceSymbol.Zz) y += (Painter.Time * 0.15f) % 0.12f;
        symbol.localPosition = new Vector3(-0.3f, y, 0.05f);
        if (cam != null)
        {
            Vector3 dir = cam.transform.position - symbol.position;
            if (dir.sqrMagnitude > 1e-6f)
                symbol.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }

    void BuildScreen()
    {
        if (!EggboyFaceSurface.Build(skin, model, Painter.Texture, out screen))
            Debug.LogError("面屏表情贴合失败，请检查模型与图集导入设置");
    }

    void BuildSymbol(Transform playerRoot)
    {
        var go = new GameObject("FaceSymbol");
        go.transform.SetParent(playerRoot, false);
        go.transform.localPosition = new Vector3(-0.3f, 1.0f, 0.05f);
        go.transform.localScale = Vector3.one * 0.34f;
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var mesh = new Mesh { name = "SymbolQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        symbolMat = TransparentMaterial(Painter.SymbolTexture, "eggboy-symbol");
        if (symbolMat == null) { Object.Destroy(go); return; }
        mr.sharedMaterial = symbolMat;
        symbol = go.transform;
        symbol.gameObject.SetActive(false);
    }

    Vector3 ToBody(Vector3 modelLocal)
    {
        Vector3 world = model.TransformPoint(modelLocal);
        return body.InverseTransformPoint(world);
    }

    static Material TransparentMaterial(Texture tex, string name)
    {
        // Shader 放在 Resources 里，打包时不会被裁掉；URP 优先，Built-in 只作为回退
        Shader sh = Resources.Load<Shader>("EggboyURPUnlitTransparent");
        if (sh == null) sh = Resources.Load<Shader>("EggboyUnlitTransparent");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null)
        {
            Debug.LogError("面屏表情：找不到可用的透明 Shader");
            return null;
        }
        var m = new Material(sh);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        m.renderQueue = 3100;
        m.name = name;
        return m;
    }
}
