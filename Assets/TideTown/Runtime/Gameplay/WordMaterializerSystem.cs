// 创新方向 D：信纸揭字 · 抛空成物 + 拼字（工程书 INNOVATION_BOOK_20261110 的 WP-D）。
//   · 信上真的有这个字，才能撕下来（数据在 Resources/Story/word_slips.json，正文里那个字会变成邮戳橘色）。
//   · 撕下来的字放在信纸下方的"邮包"里：拖出信纸 = 扔进世界变成实物；拖到另一张字上 = 拼成新的字。
//   · 字变出来的东西只"给你一样物品"，不直接把这一站设成完成：
//       「木柴」是湿的（塞进炉膛点不着）→ 和阿萤信里的「干」拼成「干木柴」才烧得起来；
//       「火」= 火种（等于拿到火柴）；「球」= 那颗红球；「煤油」= 一壶油；
//       「木柴」+「船」=「木船」，涨潮后扔到渡口缺口上能踩着过海。
//   · 外观走 Lux3DService：先放保底模型（玩法立即生效），后台生成好了原位替换。
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable] public class WordSlipDef
{
    public string id, letter, word, near, kind, entry, action, prompt;
    [NonSerialized] public bool combo;
}
[Serializable] public class WordComboDef { public string a, b, id, word, entry, action, prompt; }
[Serializable] class WordSlipFile { public WordSlipDef[] slips; public WordComboDef[] combos; }

public sealed class WordMaterializerSystem : MonoBehaviour
{
    static readonly string[] LetterIds = { "lin_oven", "ying_dog", "chen_lighthouse", "zhou_eggboy" };
    static readonly string[] LetterTabs = { "林阿婆的信", "阿萤的信", "老陈的信", "老周的信" };
    const string HighlightHex = "#C2621D";

    EggboyPlayer player;
    EggboyUI ui;
    IslandWorld world;
    StoryDirector director;
    GameState state;

    WordSlipDef[] baseSlips = new WordSlipDef[0];
    WordComboDef[] combos = new WordComboDef[0];
    readonly List<WordSlipDef> sessionCombos = new List<WordSlipDef>(); // 表里没有的自由组合，只在这一局里有

    RectTransform hudCanvasRoot, letterOverlay, letterPaper;
    CanvasGroup paperCanvasGroup;
    Image overlayBackdropImage;
    Button letterCloseButton;
    GameObject slipTrayRoot;
    readonly List<GameObject> trayObjects = new List<GameObject>();
    readonly Dictionary<string, RectTransform> slipCards = new Dictionary<string, RectTransform>();
    readonly List<GameObject> tabButtons = new List<GameObject>();
    int currentLetter;

    WordSlipDef draggingSlip;
    RectTransform draggedSlipUI;
    CanvasGroup draggedSource;
    Vector2 dragStartScreenPos, lastDragPointer;
    int restoreLetterCloseAt;
    bool aimedInAir;
    GameObject aimPreviewRoot;
    readonly Transform[] arcDots = new Transform[8];
    LineRenderer landing;
    Material arcMaterial, landingMaterial;
    float landingTime;
    const float LandingSeconds = .36f;

    // 同一个字在世界里只留一份：再扔一次，旧的收回
    readonly Dictionary<string, Lux3DSpawn> materialized = new Dictionary<string, Lux3DSpawn>();

    public Lux3DSpawn LastSpawn { get; private set; }
    public string LastAction { get; private set; } = "";
    public bool LastActionApplied { get; private set; }
    public bool IsPanelOpen => ui != null && ui.LetterOpen;

    public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
    {
        player = p; ui = u; world = w; director = d; state = s;
        var ta = Resources.Load<TextAsset>("Story/word_slips");
        if (ta != null)
        {
            var f = JsonUtility.FromJson<WordSlipFile>(ta.text);
            if (f != null)
            {
                if (f.slips != null) baseSlips = f.slips;
                if (f.combos != null) combos = f.combos;
            }
        }
    }

    // ---- 现在手里有哪些字 ----

    bool HasLetter(string letterId)
    {
        if (state == null || !state.flags.Contains("letters_taken")) return false;
        // 前三封一开始就装在邮包里；老周那封要等回到邮局才有
        if (letterId == "zhou_eggboy") return state.flags.Contains("dawn_shown");
        return true;
    }

    public List<WordSlipDef> PocketSlips()
    {
        var list = new List<WordSlipDef>();
        foreach (var s in baseSlips) if (HasLetter(s.letter)) list.Add(s);
        foreach (var c in combos)
            if (state != null && state.flags.Contains("combo_" + c.id)) list.Add(ComboSlip(c));
        list.AddRange(sessionCombos);
        return list;
    }

    static WordSlipDef ComboSlip(WordComboDef c) => new WordSlipDef
    { id = c.id, word = c.word, kind = "item", entry = c.entry, action = c.action, prompt = c.prompt, combo = true };

    WordSlipDef FindSlip(string id)
    {
        foreach (var s in PocketSlips()) if (s.id == id) return s;
        return null;
    }

    // 两张字叠在一起。表里有就得到固定的新字（存档记住）；没有就得到一张"纪念物"字签，只在这一局里有。
    public WordSlipDef Combine(string aId, string bId)
    {
        var a = FindSlip(aId); var b = FindSlip(bId);
        if (a == null || b == null || aId == bId || a.combo || b.combo) return null;
        foreach (var c in combos)
        {
            if (!((c.a == aId && c.b == bId) || (c.a == bId && c.b == aId))) continue;
            if (state != null && !state.flags.Contains("combo_" + c.id)) state.flags.Add("combo_" + c.id);
            TideTownAchievementSystem.Unlock("word_combo");
            return ComboSlip(c);
        }
        string id = "free_" + aId + "_" + bId, idRev = "free_" + bId + "_" + aId;
        foreach (var s in sessionCombos) if (s.id == id || s.id == idRev) return s;
        var free = new WordSlipDef { id = id, word = a.word + b.word, kind = "item", entry = "", action = "keepsake", prompt = a.word + b.word, combo = true };
        sessionCombos.Add(free);
        return free;
    }

    // ---- 打开信 ----

    public void OpenLetterWithSlips()
    {
        if (ui == null) return;
        if (ui.LetterOpen) { ui.CloseLetter(); return; }
        if (state == null || !state.flags.Contains("letters_taken")) return; // 还没取到信
        string step = state.step ?? "";
        int idx = step.StartsWith("S") ? 1 : step.StartsWith("L") ? 2 : step.StartsWith("E") && HasLetter("zhou_eggboy") ? 3 : 0;
        OpenSpecificLetter(idx);
    }

    public void OpenSpecificLetter(int index)
    {
        if (ui == null || director == null) return;
        index = Mathf.Clamp(index, 0, LetterIds.Length - 1);
        if (!HasLetter(LetterIds[index])) return;
        var letter = director.GetLetter(LetterIds[index]);
        if (letter == null) return;
        currentLetter = index;
        ui.ShowLetterDetail(letter.title, HighlightBody(letter.body, LetterIds[index]), letter.signature);
        RebuildTray();
    }

    // 正文里能撕的那个字涂成邮戳橘色
    string HighlightBody(string body, string letterId)
    {
        if (ui.LetterBodyText != null) ui.LetterBodyText.supportRichText = true;
        // 卡住提示第 3 层：信里和眼下这一步有关的那句话先标成青绿色
        var hint = HintDirector.Instance;
        if (hint != null && hint.LetterHintId == letterId && !string.IsNullOrEmpty(hint.LetterHintPhrase))
        {
            int h = body.IndexOf(hint.LetterHintPhrase, StringComparison.Ordinal);
            if (h >= 0)
                body = body.Substring(0, h) + "<color=#2F7D6B>" + hint.LetterHintPhrase + "</color>" + body.Substring(h + hint.LetterHintPhrase.Length);
        }
        foreach (var s in baseSlips)
        {
            if (s.letter != letterId || string.IsNullOrEmpty(s.word)) continue;
            int at = -1;
            if (!string.IsNullOrEmpty(s.near))
            {
                int n = body.IndexOf(s.near, StringComparison.Ordinal);
                if (n >= 0) { int w = s.near.IndexOf(s.word, StringComparison.Ordinal); if (w >= 0) at = n + w; }
            }
            if (at < 0) at = body.IndexOf(s.word, StringComparison.Ordinal);
            if (at < 0) continue;
            body = body.Substring(0, at) + "<color=" + HighlightHex + "><b>" + s.word + "</b></color>" + body.Substring(at + s.word.Length);
        }
        return body;
    }

    void EnsureBoundToLetterUI()
    {
        if (letterPaper != null) return;
        hudCanvasRoot = TideTownStyleUI.FindHudCanvasRoot(ui);
        letterOverlay = TideTownStyleUI.FindLetterOverlay(ui);
        letterPaper = TideTownStyleUI.FindLetterPaper(ui);
        if (letterPaper == null) return;
        var paperImg = letterPaper.GetComponent<Image>();
        if (paperImg != null) paperImg.raycastTarget = true; // 点信纸内部不算"点背景关信"
        paperCanvasGroup = letterPaper.GetComponent<CanvasGroup>();
        if (paperCanvasGroup == null) paperCanvasGroup = letterPaper.gameObject.AddComponent<CanvasGroup>();
        if (letterOverlay != null)
        {
            overlayBackdropImage = letterOverlay.GetComponent<Image>();
            letterCloseButton = letterOverlay.GetComponent<Button>();
        }

        slipTrayRoot = new GameObject("WordSlipTray", typeof(RectTransform));
        slipTrayRoot.transform.SetParent(letterPaper, false);
        var rt = slipTrayRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(560f, 620f);
    }

    void RebuildTray()
    {
        EnsureBoundToLetterUI();
        if (slipTrayRoot == null) return;
        foreach (var go in trayObjects) if (go != null) Destroy(go);
        trayObjects.Clear();
        slipCards.Clear();
        foreach (var go in tabButtons) if (go != null) Destroy(go);
        tabButtons.Clear();

        // 顶部：邮包里有的信
        int shown = 0;
        for (int i = 0; i < LetterIds.Length; i++)
        {
            if (!HasLetter(LetterIds[i])) continue;
            int idx = i;
            var btn = TideTownStyleUI.CreateWarmButton(slipTrayRoot.transform, LetterTabs[i],
                // 2026-11-16 下午：信件页签挪到信纸上沿外面（像索引签），不再压住信的第一行
                new Vector2(-186f + shown * 124f, 324f), new Vector2(116f, 28f), i == currentLetter, () => OpenSpecificLetter(idx));
            tabButtons.Add(btn.gameObject);
            shown++;
        }

        var pocket = PocketSlips();
        var hintBar = letterPaper != null ? letterPaper.Find("LetterHintBar") : null;
        if (hintBar != null) hintBar.gameObject.SetActive(true);
        if (pocket.Count == 0) return;

        // 先数一遍要几行；两行以上时整片往上挪一行，并把信纸页脚的"收起信"小条让出来（信纸上沿 +310，页脚条在 -274 附近）
        int rows = 1;
        {
            float px = -250f;
            foreach (var def in pocket)
            {
                float w = def.kind == "char" ? 48f : 30f + def.word.Length * 20f;
                if (px + w > 250f) { px = -250f; rows++; }
                px += w + 8f;
            }
        }
        if (rows >= 2 && hintBar != null) hintBar.gameObject.SetActive(false);
        float lift = rows >= 2 ? 22f : 0f;

        var hint = TideTownStyleUI.CreateText(slipTrayRoot.transform, "· 信上橘色的字可以撕下来 ·",
            12, TideTownStyleUI.InkMuted, TextAnchor.MiddleCenter, new Vector2(0f, -214f + lift), new Vector2(500f, 18f), false);
        trayObjects.Add(hint.gameObject);

        // 流式排版：单字是小方块，词和拼出来的字长一些
        float x = -250f, y = -240f + lift;
        const float gap = 8f, rowH = 40f;
        foreach (var def in pocket)
        {
            float w = def.kind == "char" ? 48f : 30f + def.word.Length * 20f;
            if (x + w > 250f) { x = -250f; y -= rowH; }
            Vector2 pos = new Vector2(x + w * 0.5f, y);
            x += w + gap;

            var card = TideTownStyleUI.CreatePanel(slipTrayRoot.transform, "WordSlip_" + def.id, w, 34f,
                def.combo ? TideTownStyleUI.AmberSoft : TideTownStyleUI.SlipColor, true);
            card.sprite = TideTownStyleUI.RoundedBorderSprite;   // 2026-11-16 字条和手账同款内缩细线
            var srt = card.rectTransform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = pos;
            var sh = card.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color32(0x3b, 0x2a, 0x1a, 70);
            sh.effectDistance = new Vector2(2f, -3f);
            TideTownStyleUI.CreateText(card.transform, def.word, 16,
                def.kind == "char" ? (Color32)new Color32(0xC2, 0x62, 0x1D, 255) : TideTownStyleUI.InkDark,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(w - 6f, 28f), true);
            card.gameObject.AddComponent<CanvasGroup>();
            trayObjects.Add(card.gameObject);
            slipCards[def.id] = srt;
        }
    }

    bool lastLetterOpen;

    void Update()
    {
        if (restoreLetterCloseAt > 0 && Time.frameCount >= restoreLetterCloseAt)
        {
            restoreLetterCloseAt = 0;
            if (letterCloseButton != null) letterCloseButton.interactable = ui != null && ui.inputClose;
        }
        if (draggingSlip == null && landingTime > 0f)
        {
            landingTime = Mathf.Max(0f, landingTime - Time.unscaledDeltaTime);
            float fraction = 1f - landingTime / LandingSeconds;
            UpdateLandingMarker(landing.transform.position - Vector3.up * .012f, Mathf.Lerp(.14f, .29f, fraction), .8f * (1f - fraction));
            if (landingTime <= 0f) landing.enabled = false;
        }
        if (ui == null || !ui.inputClose) return;
        if (ui.LetterOpen && !lastLetterOpen) { RestorePaperOpacity(); RebuildTray(); }
        else if (!ui.LetterOpen && lastLetterOpen) { CancelActiveDrag(); RestorePaperOpacity(); }
        lastLetterOpen = ui.LetterOpen;
        if (!ui.LetterOpen) return;

        Vector2 pointer = Input.mousePosition;
        if (draggingSlip == null && Input.GetMouseButtonDown(0))
        {
            string slip = SlipUnder(pointer, null);
            if (slip != null) BeginSlipDrag(FindSlip(slip), slipCards[slip], pointer);
        }
        if (draggingSlip == null) return;
        if (Input.GetMouseButtonUp(0)) EndSlipDrag(pointer);
        else if (!Input.GetMouseButton(0)) { CancelActiveDrag(); RestorePaperOpacity(); }
        else
        {
            UpdateDraggedSlipScreenPos(pointer, pointer - lastDragPointer);
            lastDragPointer = pointer;
            UpdateDraggingInAir();
        }
    }

    // ---- 拖拽 ----

    void BeginSlipDrag(WordSlipDef def, RectTransform originalCard, Vector2 pointer)
    {
        if (def == null || hudCanvasRoot == null) return;
        draggingSlip = def;
        draggedSource = originalCard != null ? originalCard.GetComponent<CanvasGroup>() : null;
        dragStartScreenPos = lastDragPointer = pointer;
        aimedInAir = false;
        if (draggedSource != null) draggedSource.alpha = 0f;
        // The backdrop normally closes the letter on click. The slip owns this press until release.
        if (letterCloseButton != null) letterCloseButton.interactable = false;

        float w = def.kind == "char" ? 56f : 36f + def.word.Length * 22f;
        var dragImg = TideTownStyleUI.CreatePanel(hudCanvasRoot, "DraggedWordSlip", w, 40f, TideTownStyleUI.PaperColor, true);
        dragImg.sprite = TideTownStyleUI.RoundedBorderSprite;
        dragImg.raycastTarget = false;
        var sh = dragImg.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color32(0, 0, 0, 120);
        sh.effectDistance = new Vector2(5f, -8f);
        TideTownStyleUI.CreateText(dragImg.transform, def.word, 18, TideTownStyleUI.InkDark, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(w - 8f, 34f), true);
        draggedSlipUI = dragImg.rectTransform;
        UpdateDraggedSlipScreenPos(pointer, Vector2.zero);
        ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.18f);
    }

    void EndSlipDrag(Vector2 pointer)
    {
        if (draggingSlip == null) return;
        var def = draggingSlip;

        // 1) 松手时压在另一张字上：拼字
        string over = SlipUnder(pointer, def.id);
        if (over != null)
        {
            CleanupDragVisuals();
            RestorePaperOpacity();
            var made = Combine(def.id, over);
            RebuildTray();
            if (made != null && slipCards.TryGetValue(made.id, out var card)) StartCoroutine(PopCard(card));
            ShadowAndChimeSystem.PlayProceduralChime(made != null ? 880f : 220f, 0.3f);
            return;
        }

        // 2) 拖出了信纸：扔出去
        bool outsidePaper = letterPaper == null || !RectTransformUtility.RectangleContainsScreenPoint(letterPaper, pointer, null);
        bool shouldThrow = outsidePaper && (aimedInAir || Vector2.Distance(pointer, dragStartScreenPos) > 48f);
        Vector3 landPos = shouldThrow ? ComputeAimWorldPoint(pointer) : Vector3.zero;
        CleanupDragVisuals();
        RestorePaperOpacity();
        if (shouldThrow && player != null)
        {
            if (ui != null && ui.LetterOpen) ui.CloseLetter();
            StartCoroutine(ThrowRoutine(def, landPos));
        }
    }

    string SlipUnder(Vector2 screenPos, string exceptId)
    {
        foreach (var kv in slipCards)
        {
            if (kv.Key == exceptId || kv.Value == null || !kv.Value.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(kv.Value, screenPos, null)) return kv.Key;
        }
        return null;
    }

    IEnumerator PopCard(RectTransform card)
    {
        float t = 0f;
        while (t < 0.35f && card != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.35f);
            card.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        if (card != null) card.localScale = Vector3.one;
    }

    void UpdateDraggedSlipScreenPos(Vector2 screenPos, Vector2 delta)
    {
        if (draggedSlipUI == null || hudCanvasRoot == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(hudCanvasRoot, screenPos, null, out Vector2 localPt);
        draggedSlipUI.anchoredPosition = localPt;
        float tilt = Mathf.Clamp(-delta.x * 0.65f, -18f, 18f);
        draggedSlipUI.localRotation = Quaternion.Euler(0f, 0f, -3f + tilt);
        draggedSlipUI.localScale = Vector3.one * 1.08f;
    }

    void UpdateDraggingInAir()
    {
        Vector2 mousePos = Input.mousePosition;
        bool outside = letterPaper == null || !RectTransformUtility.RectangleContainsScreenPoint(letterPaper, mousePos, null);
        // 字签拖出信纸，信纸退成半透明，露出小岛
        aimedInAir = outside;
        if (paperCanvasGroup != null)
            paperCanvasGroup.alpha = Mathf.Lerp(paperCanvasGroup.alpha, outside ? 0.12f : 1f, Time.unscaledDeltaTime * 12f);
        if (overlayBackdropImage != null && outside) overlayBackdropImage.color = new Color32(0, 0, 0, 10);
        UpdateArcPreview(ComputeAimWorldPoint(mousePos), outside && draggingSlip.kind != "char");
    }

    Vector3 ComputeAimWorldPoint(Vector2 screenPos)
    {
        if (player == null) return Vector3.zero;
        Vector3 pPos = player.transform.position;
        var cam = Camera.main;
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, pPos.y, 0f));
            if (groundPlane.Raycast(ray, out float enter) && enter > 0.5f && enter < 35f)
            {
                Vector3 hit = ray.GetPoint(enter);
                Vector3 flat = new Vector3(hit.x - pPos.x, 0f, hit.z - pPos.z);
                float dist = flat.magnitude;
                if (dist < 1.4f) flat = flat.normalized * 1.4f;
                else if (dist > 8.5f) flat = flat.normalized * 8.5f;
                Vector3 target = pPos + flat;
                if (world != null)
                {
                    float gy = world.GroundAt(target.x, target.z, pPos.y + 2.5f);
                    target.y = Mathf.Max(gy, world.WaterY);
                }
                return target;
            }
        }
        float yaw = player.yaw;
        Vector3 fallback = pPos + new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 2.6f;
        if (world != null) fallback.y = world.GroundAt(fallback.x, fallback.z, pPos.y + 1.5f);
        return fallback;
    }

    void UpdateArcPreview(Vector3 targetPos, bool visible)
    {
        if (player == null || !visible)
        {
            if (aimPreviewRoot != null && landingTime <= 0f) aimPreviewRoot.SetActive(false);
            return;
        }
        EnsureArcPreview();
        landingTime = 0f;
        aimPreviewRoot.SetActive(true);
        Vector3 startPos = player.transform.position + Vector3.up * 0.72f;
        for (int i = 0; i < arcDots.Length; i++)
        {
            float t = (i + 1f) / (arcDots.Length + 1f);
            arcDots[i].gameObject.SetActive(true);
            arcDots[i].position = ThrowArc(startPos, targetPos + Vector3.up * .05f, t, 2.1f);
        }
        UpdateLandingMarker(targetPos, .14f, .76f);
    }

    void EnsureArcPreview()
    {
        if (aimPreviewRoot == null)
        {
            aimPreviewRoot = new GameObject("WordThrowAimPreview");
            aimPreviewRoot.transform.SetParent(transform, false);
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            arcMaterial = new Material(sh) { color = TideTownStyleUI.SlipColor };
            if (arcMaterial.HasProperty("_EmissionColor"))
            {
                arcMaterial.EnableKeyword("_EMISSION");
                arcMaterial.SetColor("_EmissionColor", new Color(.65f, .42f, .14f));
            }
            for (int i = 0; i < arcDots.Length; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(d.GetComponent<Collider>());
                d.transform.SetParent(aimPreviewRoot.transform, false);
                d.transform.localScale = Vector3.one * Mathf.Lerp(.043f, .024f, i / (arcDots.Length - 1f));
                var renderer = d.GetComponent<Renderer>();
                renderer.sharedMaterial = arcMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arcDots[i] = d.transform;
            }
            var marker = new GameObject("WordLandingRing");
            marker.transform.SetParent(aimPreviewRoot.transform, false);
            landing = marker.AddComponent<LineRenderer>();
            landingMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            landing.sharedMaterial = landingMaterial;
            landing.useWorldSpace = false;
            landing.loop = true;
            landing.positionCount = 24;
            landing.widthMultiplier = .009f;
            landing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < landing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / landing.positionCount;
                landing.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
        }
    }

    void UpdateLandingMarker(Vector3 position, float radius, float opacity)
    {
        landing.transform.position = position + Vector3.up * .012f;
        landing.transform.localScale = Vector3.one * radius;
        landing.startColor = landing.endColor = new Color(1f, .80f, .46f, opacity);
        landing.enabled = true;
    }

    static Vector3 ThrowArc(Vector3 start, Vector3 end, float t, float height) =>
        Vector3.Lerp(start, end, t) + Vector3.up * (4f * height * t * (1f - t));

    void ShowLanding(Vector3 position)
    {
        if (draggingSlip != null) return;
        EnsureArcPreview();
        foreach (var dot in arcDots) dot.gameObject.SetActive(false);
        aimPreviewRoot.SetActive(true);
        UpdateLandingMarker(position, .14f, .8f);
        landingTime = LandingSeconds;
    }

    void OnDestroy()
    {
        if (arcMaterial != null) Destroy(arcMaterial);
        if (landingMaterial != null) Destroy(landingMaterial);
    }

    // ---- 扔出去 ----

    // 审计和手柄用：不经过拖拽，直接把某个字扔到某处
    public Coroutine ThrowSlip(string slipId, Vector3 landPos)
    {
        var def = FindSlip(slipId);
        return def != null ? StartCoroutine(ThrowRoutine(def, landPos)) : null;
    }

    IEnumerator ThrowRoutine(WordSlipDef def, Vector3 landPos)
    {
        if (player == null) yield break;
        LastAction = def.action ?? "";
        LastActionApplied = false;
        LastSpawn = null;

        Vector3 startPos = player.transform.position + Vector3.up * 0.72f;
        float lookYaw = Mathf.Atan2(landPos.x - startPos.x, landPos.z - startPos.z);
        player.yaw = lookYaw;
        player.transform.rotation = Quaternion.Euler(0f, lookYaw * Mathf.Rad2Deg, 0f);
        player.PlayEmote("give");

        // 单个字太轻，变不出东西：飘起来又落回邮差手里
        bool loose = def.kind == "char";
        Vector3 endPos = loose ? startPos : landPos + Vector3.up * 0.05f;

        var flyingSlip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(flyingSlip.GetComponent<Collider>());
        flyingSlip.name = "FlyingParchmentSlip";
        flyingSlip.transform.localScale = new Vector3(0.28f, 0.008f, 0.14f);
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var paperMat = new Material(sh) { color = TideTownStyleUI.PaperColor };
        if (paperMat.HasProperty("_EmissionColor"))
        {
            paperMat.EnableKeyword("_EMISSION");
            paperMat.SetColor("_EmissionColor", new Color(1f, 0.86f, 0.55f) * 1.2f);
        }
        flyingSlip.GetComponent<Renderer>().sharedMaterial = paperMat;
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.28f);

        float duration = loose ? 0.9f : 0.52f, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 mid = ThrowArc(startPos, endPos, t, loose ? 1.1f : 2.1f);
            if (loose) mid += new Vector3(Mathf.Sin(lookYaw), 0f, Mathf.Cos(lookYaw)) * (1.2f * Mathf.Sin(t * Mathf.PI));
            flyingSlip.transform.position = mid;
            flyingSlip.transform.rotation = Quaternion.Euler(t * 520f, lookYaw * Mathf.Rad2Deg + t * 180f, Mathf.Sin(t * 14f) * 28f);
            yield return null;
        }
        Destroy(flyingSlip);
        Destroy(paperMat);
        if (loose) { ShadowAndChimeSystem.PlayProceduralChime(294f, 0.25f); yield break; }
        ShowLanding(landPos);

        Materialize(def, landPos, lookYaw * Mathf.Rad2Deg);
        ShadowAndChimeSystem.PlayProceduralChime(880f, 0.42f);
        if (LastSpawn != null && LastSpawn.root != null) yield return Grow(LastSpawn.root.transform);
    }

    IEnumerator Grow(Transform root)
    {
        float t = 0f;
        while (t < 0.38f && root != null)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / 0.38f);
            root.localScale = Vector3.one * (1f - Mathf.Pow(1f - u, 3f) + Mathf.Sin(u * Mathf.PI) * 0.14f);
            yield return null;
        }
        if (root != null) root.localScale = Vector3.one;
    }

    Vector3 StovePos()
    {
        foreach (var it in BakeryData.Load()) if (it.id == "stove") return new Vector3(it.x, it.y, it.z);
        return new Vector3(-9.73f, 1.3f, -5.85f);
    }

    void Materialize(WordSlipDef def, Vector3 landPos, float yawDeg)
    {
        if (state == null || director == null) return;
        var lux = Lux3DService.Ensure();
        var mech = FindObjectOfType<WorldMechanismDirector>();
        string action = def.action ?? "";

        // 这几样直接到邮差手里，不在地上留东西
        if (action == "fire")
        {
            bool bakery = state.step.StartsWith("B");
            if (bakery && !state.flags.Contains("matches_taken")) { director.TakeMatches(); LastActionApplied = true; }
            SpawnEmber(landPos);
            return;
        }
        if (action == "ball")
        {
            bool school = state.step == "S1_slipper" || state.step == "S2_ball";
            if (school && !state.flags.Contains("ball_taken") && !state.flags.Contains("ball_in_bowl")) { director.TakeBall(); LastActionApplied = true; }
            return;
        }
        if (action == "oil")
        {
            if (state.step == "L1_shed" && !state.flags.Contains("oil_taken")) { director.TakeOil(); LastActionApplied = true; }
            return;
        }

        // 其余的落在地上，走 Lux3D：先保底，生成好了原位替换
        if (materialized.TryGetValue(def.id, out var old) && old != null && old.root != null) Destroy(old.root);
        var entry = !string.IsNullOrEmpty(def.entry) ? lux.Find(def.entry) : lux.Match(def.word, "keepsake");
        string prompt = string.IsNullOrEmpty(def.prompt) ? def.word : def.prompt;
        Vector3 pos = landPos;
        Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f);

        bool gapBoat = false;
        if (action == "boat" && mech != null)
        {
            Vector3 ch = mech.GetCrossingChannelCenter();
            bool flooded = WaterLevel.StaticY >= WaterLevel.LevelYs[2] - 0.05f;
            if (flooded && mech.GapActive && !mech.CrossingSolved && Vector3.Distance(landPos, ch) < 6f)
            {
                gapBoat = true;
                pos = new Vector3(ch.x, WaterLevel.StaticY - 0.05f, ch.z);
                rot = Quaternion.LookRotation(mech.CrossingDirection, Vector3.up);
            }
        }

        var spawn = lux.Spawn(prompt, entry, pos, rot, transform, true, gapBoat ? 2.2f : (action == "boat" ? 1.2f : 0f));
        spawn.root.name = "Word_" + def.id;
        materialized[def.id] = spawn;
        LastSpawn = spawn;

        if (gapBoat)
        {
            // 能踩的范围固定成一块顺着过海方向的甲板，换成真模型后照样过得去
            var deck = spawn.root.GetComponent<BoxCollider>();
            if (deck == null) deck = spawn.root.AddComponent<BoxCollider>();
            int ground = LayerMask.NameToLayer("Ground");
            spawn.root.layer = ground >= 0 ? ground : 6;
            deck.center = new Vector3(0f, 0.1f, 0f);
            deck.size = new Vector3(1.1f, 0.4f, 2.4f);
            spawn.root.AddComponent<WishBob>();
            mech.BridgeGapWithBoat("wood_boat");
            LastActionApplied = true;
        }
        else if (action == "boat")
        {
            // 没放在缺口上：只是一条小船摆在那儿，不能当桥
            var box = spawn.root.GetComponent<BoxCollider>();
            if (box != null) Destroy(box);
        }
        else if (action == "wet_wood")
        {
            ApplyWetLook(spawn.root);
            spawn.onSwapped = s => { if (s.root != null) ApplyWetLook(s.root); };
            if (Vector3.Distance(landPos, StovePos()) < 6.5f) LastActionApplied = director.LoadWetWood();
        }
        else if (action == "dry_wood")
        {
            if (Vector3.Distance(landPos, StovePos()) < 6.5f && director.LoadDryWoodFromWord())
            {
                LastActionApplied = true;
                TideTownAchievementSystem.Unlock("word_fire");
                // 换下来的湿柴收走
                if (materialized.TryGetValue("wood", out var wet) && wet != null && wet.root != null) Destroy(wet.root);
            }
        }
        else if (action == "lamp")
        {
            var lgo = new GameObject("WarmLight");
            lgo.transform.SetParent(spawn.root.transform, false);
            lgo.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            var light = lgo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.38f);
            light.range = 7.5f;
            light.intensity = 2.2f;
            LastActionApplied = true;
        }
        else LastActionApplied = true; // 纪念物：摆在那儿就是全部作用
    }

    // 「火」：一团小火苗飘向邮差的包
    void SpawnEmber(Vector3 at)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "WordEmber";
        go.transform.position = at + Vector3.up * 0.4f;
        go.transform.localScale = Vector3.one * 0.14f;
        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(sh);
        var c = new Color(1f, 0.62f, 0.2f);
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = 3.5f; l.intensity = 2.4f;
        StartCoroutine(EmberFly(go.transform));
    }

    IEnumerator EmberFly(Transform ember)
    {
        float t = 0f;
        Vector3 from = ember.position;
        while (t < 1.1f && ember != null && player != null)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
            Vector3 to = player.transform.position + Vector3.up * 0.55f;
            ember.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.5f;
            ember.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.05f, k);
            yield return null;
        }
        if (ember != null) Destroy(ember.gameObject);
    }

    // 湿柴：颜色发暗发青，往下滴水
    static void ApplyWetLook(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            foreach (var m in r.materials)
            {
                foreach (var prop in new[] { "_BaseColor", "baseColorFactor", "_Color" })
                    if (m.HasProperty(prop)) m.SetColor(prop, new Color(0.5f, 0.58f, 0.66f, 1f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
                if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", 0.2f);
            }
        }
        if (root.transform.Find("WetDrips") != null) return;
        var go = new GameObject("WetDrips");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.7f; main.startSpeed = 0f; main.startSize = 0.035f;
        main.startColor = new Color(0.7f, 0.86f, 0.95f, 0.9f);
        main.gravityModifier = 1.2f; main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 9f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.5f, 0.05f, 0.3f);
        var fx = FindObjectOfType<BakeryEffects>();
        var pr = go.GetComponent<ParticleSystemRenderer>();
        if (fx != null && fx.ParticleMaterial != null) pr.sharedMaterial = fx.ParticleMaterial;
        else
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh != null) pr.sharedMaterial = new Material(sh);
        }
    }

    // ---- 收尾 ----

    void CleanupDragVisuals()
    {
        if (draggedSlipUI != null) Destroy(draggedSlipUI.gameObject);
        draggedSlipUI = null;
        if (draggedSource != null) draggedSource.alpha = 1f;
        draggedSource = null;
        draggingSlip = null;
        restoreLetterCloseAt = Time.frameCount + 1;
        if (aimPreviewRoot != null) aimPreviewRoot.SetActive(false);
    }

    void CancelActiveDrag()
    {
        if (draggingSlip == null) return;
        CleanupDragVisuals();
    }

    void RestorePaperOpacity()
    {
        if (paperCanvasGroup != null) paperCanvasGroup.alpha = 1f;
        if (overlayBackdropImage != null) overlayBackdropImage.color = new Color32(0, 0, 0, 60);
    }
}
