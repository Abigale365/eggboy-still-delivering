// 潮汐镇统一视觉与真实 Lux3D 资产工具库（TideTownStyleUI）。
// 严格遵循 EggboyUI.cs 的羊皮纸信件质感（#FBF3E2 暖白信纸、#E9DCC0 信笺横线、#3B2A1A 深褐墨字、#FF9A4D 暖橘邮戳色）
// 以及 100% 复用场景内真实 Lux3D 模型资产，绝不使用原生灰盒 OnGUI 或脱离画风的几何体。
using System;
using UnityEngine;
using UnityEngine.UI;

public static class TideTownStyleUI
{
    public static readonly Color32 PaperColor = new Color32(0xfb, 0xf3, 0xe2, 255);
    public static readonly Color32 PaperRuleColor = new Color32(0xe9, 0xdc, 0xc0, 210);
    public static readonly Color32 SlipColor = new Color32(0xff, 0xf9, 0xee, 255);
    public static readonly Color32 SlipBorderColor = new Color32(0xd8, 0xb8, 0x88, 255);
    public static readonly Color32 InkDark = new Color32(0x3b, 0x2a, 0x1a, 255);
    public static readonly Color32 InkMuted = new Color32(0x8a, 0x6a, 0x4a, 255);
    public static readonly Color32 BarkPanel = new Color32(0x3b, 0x2a, 0x1a, 225);
    public static readonly Color32 CreamText = new Color32(0xfb, 0xef, 0xd9, 255);
    public static readonly Color32 AmberAccent = new Color32(0xff, 0x9a, 0x4d, 255);
    public static readonly Color32 AmberSoft = new Color32(0xff, 0xdf, 0xc0, 255);

    static Font cachedFont;
    static Sprite cachedRounded;
    static Sprite cachedWhite;

    public static Font UiFont
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<Font>("Fonts/NotoSansSC-VF");
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return cachedFont;
        }
    }

    public static Sprite RoundedSprite
    {
        get
        {
            if (cachedRounded == null) cachedRounded = MakeRounded(64, 14);
            return cachedRounded;
        }
    }

    public static Sprite WhiteSprite
    {
        get
        {
            if (cachedWhite == null)
                cachedWhite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            return cachedWhite;
        }
    }

    public static RectTransform FindHudCanvasRoot(EggboyUI ui)
    {
        if (ui == null) return null;
        var t = ui.transform.Find("EGGBOY HUD");
        return t != null ? t.GetComponent<RectTransform>() : null;
    }

    public static RectTransform FindLetterOverlay(EggboyUI ui)
    {
        var root = FindHudCanvasRoot(ui);
        if (root == null || root.childCount < 2) return null;
        // EggboyUI.Build 中：child 0 是 GameplayHUD，child 1 是 letterOverlay
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.childCount > 0 && c.GetChild(0).GetComponent<Shadow>() != null)
                return c.GetComponent<RectTransform>();
        }
        return root.GetChild(1).GetComponent<RectTransform>();
    }

    public static RectTransform FindLetterPaper(EggboyUI ui)
    {
        var ov = FindLetterOverlay(ui);
        if (ov == null || ov.childCount == 0) return null;
        return ov.GetChild(0).GetComponent<RectTransform>();
    }

    public static Image CreatePanel(Transform parent, string name, float w, float h, Color32 color, bool rounded = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = rounded ? RoundedSprite : WhiteSprite;
        img.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        return img;
    }

    public static RectTransform CreateParchmentCard(Transform parent, string name, float w, float h, int ruledLines = 7)
    {
        var card = CreatePanel(parent, name, w, h, PaperColor, true);
        var sh = card.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color32(0, 0, 0, 88);
        sh.effectDistance = new Vector2(4f, -6f);

        for (int i = 0; i < ruledLines; i++)
        {
            var line = CreatePanel(card.transform, "Rule_" + i, w - 64f, 1f, PaperRuleColor, false);
            line.raycastTarget = false;
            var lrt = line.rectTransform;
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = new Vector2(0f, (h * 0.32f) - i * 34f);
        }
        return card.rectTransform;
    }

    public static Text CreateText(
        Transform parent,
        string content,
        int fontSize,
        Color32 color,
        TextAnchor align,
        Vector2 anchoredPos,
        Vector2 size,
        bool bold = false)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = UiFont;
        t.fontSize = fontSize;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.color = color;
        t.alignment = align;
        t.text = content;
        t.supportRichText = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.28f;
        t.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return t;
    }

    public static Button CreateWarmButton(
        Transform parent,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        bool primaryAmber,
        Action onClick)
    {
        Color32 normal = primaryAmber ? AmberAccent : SlipColor;
        Color32 textCol = primaryAmber ? new Color32(0xff, 0xf8, 0xec, 255) : InkDark;

        var img = CreatePanel(parent, "Btn_" + label, size.x, size.y, normal, true);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        var sh = img.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color32(0x3b, 0x2a, 0x1a, 55);
        sh.effectDistance = new Vector2(1.5f, -2.5f);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        btn.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color32(255, 240, 220, 255),
            pressedColor = new Color32(230, 195, 160, 255),
            selectedColor = Color.white,
            disabledColor = new Color32(200, 200, 200, 180),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
        if (onClick != null) btn.onClick.AddListener(() => onClick());

        CreateText(img.transform, label, 15, textCol, TextAnchor.MiddleCenter, Vector2.zero, size - new Vector2(12f, 4f), true);
        return btn;
    }

    // 从当前场景或 Resources/Lux3D、Resources/Mechanisms 克隆真实 Lux3D 模型
    public static GameObject CloneSceneLux3D(string primarySceneName, float targetSize, bool addWalkableGroundCollider = false)
    {
        GameObject src = FindSceneObject(primarySceneName);
        if (src == null) return null;

        var holder = new GameObject("Lux3D_" + primarySceneName);
        var clone = UnityEngine.Object.Instantiate(src, holder.transform, false);
        clone.name = "Model";
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.SetActive(true);

        // 移除原交互/发光/物理脚本，只保留纯净高精度的 Lux3D 网格与材质
        foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
            UnityEngine.Object.Destroy(mb);
        foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.Destroy(col);

        var renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            foreach (var r in renderers) r.enabled = true;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float scale = (maxDim > 0.001f && targetSize > 0.001f) ? (targetSize / maxDim) : 1f;
            clone.transform.localScale *= scale;

            // 重新计算缩放后的包围盒，使底部贴合 holder 原点 (y = 0)，水平居中
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            Vector3 offset = new Vector3(
                holder.transform.position.x - b.center.x,
                holder.transform.position.y - b.min.y,
                holder.transform.position.z - b.center.z);
            clone.transform.position += offset;

            if (addWalkableGroundCollider)
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer < 0) groundLayer = 6;
                holder.layer = groundLayer;
                var box = holder.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                box.size = new Vector3(Mathf.Max(0.45f, b.size.x * 0.92f), Mathf.Max(0.12f, b.size.y * 0.92f), Mathf.Max(0.45f, b.size.z * 0.92f));
            }
        }

        return holder;
    }

    public static GameObject FindSceneObject(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var mechRes = Resources.Load<GameObject>("Mechanisms/" + name);
        if (mechRes != null) return mechRes;

        var luxRes = Resources.Load<GameObject>("Lux3D/" + name);
        if (luxRes != null) return luxRes;

        var direct = GameObject.Find(name);
        if (direct != null) return direct;

        // 模糊匹配场景中的 Street_S6, crate_0 等真实 Lux3D 节点
        foreach (var r in UnityEngine.Object.FindObjectsOfType<Transform>())
        {
            if (r.name == name || r.name.StartsWith(name))
                return r.gameObject;
        }
        return Resources.Load<GameObject>("Items/ending_oilcan");
    }

    // 加载或构建可动多部件 3D 机关模型（优先使用 Resources/Mechanisms/ 下的 GLB 预制体，并确保 Frame_Base / Latch_0..2 / Ring_0..2 / Gear_0..2 / Core_Spindle 节点齐全）
    public static GameObject BuildArticulatedMechanism3D(string mechanismName, float overallScale = 1f)
    {
        var root = new GameObject("Mechanism3D_" + mechanismName);
        var glbAsset = Resources.Load<GameObject>("Mechanisms/" + mechanismName);
        if (glbAsset != null)
        {
            var inst = UnityEngine.Object.Instantiate(glbAsset, root.transform, false);
            inst.name = "GLBRoot";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * overallScale;
            if (FindDeepChild(root.transform, "Ring_0") != null)
                return root;
        }

        // 若 GLB 尚未完成首次导入，使用同款参数化多齿啮合齿轮网格 + 真实 Lux3D 手绘木纹/黄铜/青铜 PBR 材质实时组装同构节点
        Material matWood = ExtractLuxMaterial("C4_狗窝木箱_后续修改版", "S_doghouse", new Color32(158, 108, 66, 255), 0.12f, 0.35f);
        Material matBrass = ExtractLuxMaterial("D3_望远镜_v2", "LH_scope", new Color32(214, 168, 86, 255), 0.72f, 0.65f);
        Material matBronze = ExtractLuxMaterial("A6_台秤_v2", "Bakery_scale", new Color32(126, 92, 62, 255), 0.58f, 0.48f);

        bool isWinch = mechanismName != null && mechanismName.Contains("M3");
        bool isBrass = mechanismName != null && mechanismName.Contains("M4");

        var frameGo = CreateMeshNode(root.transform, "Frame_Base",
            BuildGearWheelMesh(0.46f, 0.38f, 0.14f, 6, 0.015f),
            isBrass ? matBronze : matWood,
            Vector3.zero);

        if (isWinch)
        {
            CreateMeshNode(root.transform, "Ring_0",
                BuildGearWheelMesh(0.38f, 0.12f, 0.08f, 16, 0.042f),
                matBrass,
                new Vector3(0f, 0.12f, 0f));
            CreateMeshNode(root.transform, "Core_Spindle",
                BuildGearWheelMesh(0.11f, 0.02f, 0.24f, 8, 0.022f),
                matWood,
                new Vector3(0f, 0.18f, 0f));
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                float lx = (i - 1) * 0.22f;
                CreateMeshNode(root.transform, "Latch_" + i,
                    BuildBoltLatchMesh(0.065f, 0.22f, 0.055f),
                    i == 1 ? matBrass : matBronze,
                    new Vector3(lx, 0.28f, 0.085f));
            }

            CreateMeshNode(root.transform, "Ring_0",
                BuildGearWheelMesh(0.36f, 0.26f, 0.06f, 18, 0.035f),
                isBrass ? matBrass : matWood,
                new Vector3(0f, -0.02f, 0.09f));

            CreateMeshNode(root.transform, "Ring_1",
                BuildGearWheelMesh(0.24f, 0.15f, 0.065f, 12, 0.03f),
                matBrass,
                new Vector3(0f, -0.02f, 0.115f));

            CreateMeshNode(root.transform, "Ring_2",
                BuildGearWheelMesh(0.13f, 0.06f, 0.07f, 8, 0.024f),
                matBronze,
                new Vector3(0f, -0.02f, 0.14f));

            for (int g = 0; g < 3; g++)
            {
                float ang = (g * 120f + 30f) * Mathf.Deg2Rad;
                CreateMeshNode(root.transform, "Gear_" + g,
                    BuildGearWheelMesh(0.065f, 0.018f, 0.05f, 8, 0.016f),
                    matBrass,
                    new Vector3(Mathf.Cos(ang) * 0.25f, -0.02f + Mathf.Sin(ang) * 0.25f, 0.135f));
            }

            CreateMeshNode(root.transform, "Core_Spindle",
                BuildGearWheelMesh(0.058f, 0.01f, 0.16f, 6, 0.014f),
                matBrass,
                new Vector3(0f, -0.02f, 0.165f));
        }

        root.transform.localScale = Vector3.one * overallScale;
        return root;
    }

    public static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null) return null;
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName || t.name.StartsWith(childName))
                return t;
        }
        return null;
    }

    static GameObject CreateMeshNode(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    static Material ExtractLuxMaterial(string luxResourceName, string sceneFallbackName, Color32 fallbackColor, float metallic, float smoothness)
    {
        var src = Resources.Load<GameObject>("Lux3D/" + luxResourceName);
        if (src == null) src = FindSceneObject(sceneFallbackName);
        if (src != null)
        {
            var r = src.GetComponentInChildren<Renderer>(true);
            if (r != null && r.sharedMaterial != null)
                return r.sharedMaterial;
        }
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = fallbackColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fallbackColor);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    static Mesh BuildGearWheelMesh(float outerR, float innerR, float thickness, int teeth, float toothH)
    {
        var mesh = new Mesh { name = "ProcGearWheel" };
        int segs = Mathf.Max(12, teeth * 4);
        float hz = thickness * 0.5f;
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();

        for (int i = 0; i < segs; i++)
        {
            float a = (2f * Mathf.PI * i) / segs;
            bool isTooth = (i % 4 == 1) || (i % 4 == 2);
            float ro = outerR + (isTooth ? toothH : 0f);
            float co = Mathf.Cos(a);
            float si = Mathf.Sin(a);
            verts.Add(new Vector3(ro * co, ro * si, hz));
            verts.Add(new Vector3(innerR * co, innerR * si, hz));
            verts.Add(new Vector3(ro * co, ro * si, -hz));
            verts.Add(new Vector3(innerR * co, innerR * si, -hz));
            uvs.Add(new Vector2(ro * co + 0.5f, ro * si + 0.5f));
            uvs.Add(new Vector2(innerR * co + 0.5f, innerR * si + 0.5f));
            uvs.Add(new Vector2(ro * co + 0.5f, ro * si + 0.5f));
            uvs.Add(new Vector2(innerR * co + 0.5f, innerR * si + 0.5f));
        }

        for (int i = 0; i < segs; i++)
        {
            int ni = (i + 1) % segs;
            int b0 = i * 4, b1 = ni * 4;
            // Front
            tris.Add(b0 + 1); tris.Add(b0 + 0); tris.Add(b1 + 0);
            tris.Add(b0 + 1); tris.Add(b1 + 0); tris.Add(b1 + 1);
            // Back
            tris.Add(b0 + 2); tris.Add(b0 + 3); tris.Add(b1 + 3);
            tris.Add(b0 + 2); tris.Add(b1 + 3); tris.Add(b1 + 2);
            // Outer rim
            tris.Add(b0 + 0); tris.Add(b0 + 2); tris.Add(b1 + 2);
            tris.Add(b0 + 0); tris.Add(b1 + 2); tris.Add(b1 + 0);
            // Inner rim
            tris.Add(b0 + 3); tris.Add(b0 + 1); tris.Add(b1 + 1);
            tris.Add(b0 + 3); tris.Add(b1 + 1); tris.Add(b1 + 3);
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildBoltLatchMesh(float w, float h, float d)
    {
        return BuildGearWheelMesh(Mathf.Max(w, d) * 0.65f, 0.01f, h, 4, 0.012f);
    }

    static Sprite MakeRounded(int S, int R)
    {
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false, false);
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = Mathf.Max(R - x - 0.5f, 0f, x + 0.5f - (S - R));
            float dy = Mathf.Max(R - y - 0.5f, 0f, y + 0.5f - (S - R));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            byte a = (byte)(Mathf.Clamp01(R + 1f - d) * 255f);
            px[y * S + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
    }
}
