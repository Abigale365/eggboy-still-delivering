// 创新方向 G：【光影对齐 & 海岛声波八音盒】（Shadow Alignment & Island Music Box）。
// 100% 采用 TideTownStyleUI 羊皮纸信笺 uGUI 与真实 Lux3D 铜门铃/望远镜资产（无任何原生灰盒 OnGUI 或异形图标）。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShadowAndChimeSystem : MonoBehaviour
{
    static AudioSource sharedAudio;

    public static void PlayProceduralChime(float freqHz, float durationSec = 0.42f)
    {
        try
        {
            if (sharedAudio == null)
            {
                var go = new GameObject("ProceduralMusicBoxAudio");
                DontDestroyOnLoad(go);
                sharedAudio = go.AddComponent<AudioSource>();
                sharedAudio.spatialBlend = 0f;
                sharedAudio.volume = 0.42f;
            }

            int sampleRate = 22050;
            int samples = Mathf.Max(512, Mathf.RoundToInt(sampleRate * durationSec));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6.5f) * Mathf.Min(1f, t * 80f);
                float wave = Mathf.Sin(2f * Mathf.PI * freqHz * t) * 0.65f
                           + Mathf.Sin(2f * Mathf.PI * (freqHz * 2f) * t) * 0.25f
                           + Mathf.Sin(2f * Mathf.PI * (freqHz * 3f) * t) * 0.10f;
                data[i] = wave * env;
            }

            var clip = AudioClip.Create("Chime_" + Mathf.RoundToInt(freqHz), samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            sharedAudio.PlayOneShot(clip);
        }
        catch
        {
        }
    }

    EggboyPlayer player;
    EggboyUI ui;
    GameState state;

    public bool IsStudioOpen { get; private set; }

    GameObject shadowRig;
    Transform bellModelPivot;
    Transform scopeModelPivot;
    Light rigLight;

    Vector2 currentAngles = new Vector2(-46f, 38f);
    float alignmentScore;
    bool shadowSolved;

    static readonly string[] NoteNames =
    {
        "宫音 · 潮汐海浪",
        "角音 · 秋千微风",
        "徵音 · 炉膛柴火",
        "羽音 · 铜铃叮当"
    };
    static readonly float[] NoteFreqs = { 523.25f, 659.25f, 783.99f, 880.00f };
    readonly List<int> playedNotes = new List<int>();
    bool musicBoxSolved;

    RectTransform studioCard;
    Text alignStatusLabel;
    Text noteSequenceLabel;

    public void Init(EggboyPlayer p, EggboyUI u, GameState s)
    {
        player = p;
        ui = u;
        state = s;
    }

    public void ToggleStudio()
    {
        if (IsStudioOpen) CloseStudio();
        else OpenStudio();
    }

    public void OpenStudio()
    {
        if (IsStudioOpen) return;
        IsStudioOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (!shadowSolved)
        {
            currentAngles = new Vector2(-46f, 38f);
            alignmentScore = 0.25f;
        }
        EnsureStudioUI();
        if (studioCard != null) studioCard.gameObject.SetActive(true);
        Build3DLuxRig();
        RefreshUIText();
    }

    public void CloseStudio()
    {
        IsStudioOpen = false;
        if (shadowRig != null) Destroy(shadowRig);
        shadowRig = null;
        if (studioCard != null) studioCard.gameObject.SetActive(false);
    }

    void EnsureStudioUI()
    {
        if (studioCard != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (root == null) return;

        studioCard = TideTownStyleUI.CreateParchmentCard(root, "ShadowMusicStudioCard", 540f, 440f, 8);
        studioCard.anchorMin = studioCard.anchorMax = new Vector2(0f, 0.5f);
        studioCard.pivot = new Vector2(0f, 0.5f);
        studioCard.anchoredPosition = new Vector2(28f, 16f);

        TideTownStyleUI.CreateText(
            studioCard,
            "旧物光影叠合 · 海岛声波八音盒",
            22,
            TideTownStyleUI.InkDark,
            TextAnchor.MiddleLeft,
            new Vector2(0f, 178f),
            new Vector2(476f, 32f),
            true);

        TideTownStyleUI.CreateText(
            studioCard,
            "第一部分：按住鼠标左键或按 WASD 旋转右侧的「铜门铃」与「黄铜望远镜」，当两件旧物在暖光下叠合对齐时，将共鸣浮现全岛刻度线索。",
            14,
            TideTownStyleUI.InkDark,
            TextAnchor.UpperLeft,
            new Vector2(0f, 122f),
            new Vector2(476f, 52f),
            false);

        alignStatusLabel = TideTownStyleUI.CreateText(
            studioCard,
            "",
            15,
            TideTownStyleUI.InkMuted,
            TextAnchor.MiddleLeft,
            new Vector2(0f, 72f),
            new Vector2(476f, 28f),
            true);

        TideTownStyleUI.CreateWarmButton(
            studioCard,
            "直接对齐两件旧物光影",
            new Vector2(-118f, 30f),
            new Vector2(236f, 34f),
            false,
            CompleteShadowAlignment);

        TideTownStyleUI.CreateText(
            studioCard,
            "第二部分：依次点击下方四枚海岛音符（宫 · 角 · 徵 · 羽），奏响潮汐八音盒：",
            14,
            TideTownStyleUI.InkDark,
            TextAnchor.MiddleLeft,
            new Vector2(0f, -24f),
            new Vector2(476f, 28f),
            false);

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            int row = i / 2;
            int col = i % 2;
            Vector2 pos = new Vector2(col == 0 ? -122f : 122f, -66f - row * 44f);
            TideTownStyleUI.CreateWarmButton(
                studioCard,
                NoteNames[i],
                pos,
                new Vector2(228f, 36f),
                false,
                () => PlayNote(idx));
        }

        noteSequenceLabel = TideTownStyleUI.CreateText(
            studioCard,
            "",
            14,
            TideTownStyleUI.InkMuted,
            TextAnchor.MiddleLeft,
            new Vector2(0f, -150f),
            new Vector2(476f, 24f),
            false);

        TideTownStyleUI.CreateWarmButton(
            studioCard,
            "合奏潮汐八音盒旋律",
            new Vector2(-95f, -188f),
            new Vector2(280f, 36f),
            true,
            TriggerMusicBoxGrandResonance);

        TideTownStyleUI.CreateWarmButton(
            studioCard,
            "收起（Esc / 4）",
            new Vector2(148f, -188f),
            new Vector2(176f, 36f),
            false,
            CloseStudio);
    }

    void Build3DLuxRig()
    {
        if (shadowRig != null) Destroy(shadowRig);
        var cam = Camera.main;
        if (cam == null) return;

        shadowRig = new GameObject("LuxShadowChimeRig");
        shadowRig.transform.SetParent(cam.transform, false);
        float dist = 2.35f;
        float halfH = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * dist;
        float offsetX = 0.34f * cam.aspect * halfH;
        shadowRig.transform.localPosition = new Vector3(offsetX, 0.02f, dist);
        shadowRig.transform.localRotation = Quaternion.identity;

        var lightGo = new GameObject("WarmRigLight");
        lightGo.transform.SetParent(shadowRig.transform, false);
        lightGo.transform.localPosition = new Vector3(-0.6f, 0.65f, -0.85f);
        rigLight = lightGo.AddComponent<Light>();
        rigLight.type = LightType.Point;
        rigLight.color = new Color(1f, 0.86f, 0.58f);
        rigLight.range = 4.5f;
        rigLight.intensity = 2.4f;

        // 克隆真实的 Lux3D 铜门铃 (A5) 与 黄铜望远镜 (D3)
        var bell = TideTownStyleUI.CloneSceneLux3D("Bakery_bell", 0.52f, false);
        if (bell != null)
        {
            bell.transform.SetParent(shadowRig.transform, false);
            bell.transform.localPosition = new Vector3(-0.14f, -0.18f, 0f);
            bellModelPivot = bell.transform;
        }

        var scope = TideTownStyleUI.CloneSceneLux3D("LH_scope", 0.62f, false);
        if (scope != null)
        {
            scope.transform.SetParent(shadowRig.transform, false);
            scope.transform.localPosition = new Vector3(0.14f, -0.22f, 0.08f);
            scopeModelPivot = scope.transform;
        }
    }

    void Update()
    {
        if (!IsStudioOpen) return;

        float dt = Time.unscaledDeltaTime;
        bool changed = false;

        if (!shadowSolved)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) { currentAngles.x += 65f * dt; changed = true; }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) { currentAngles.x -= 65f * dt; changed = true; }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) { currentAngles.y -= 65f * dt; changed = true; }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) { currentAngles.y += 65f * dt; changed = true; }
            if (Input.GetMouseButton(0))
            {
                currentAngles.x -= Input.GetAxis("Mouse X") * 4.2f;
                currentAngles.y -= Input.GetAxis("Mouse Y") * 4.2f;
                changed = true;
            }

            alignmentScore = Mathf.Clamp01(1f - currentAngles.magnitude / 80f);
            if (alignmentScore >= 0.92f)
            {
                CompleteShadowAlignment();
            }
        }
        else
        {
            currentAngles = Vector2.Lerp(currentAngles, Vector2.zero, dt * 10f);
            alignmentScore = 1f;
        }

        if (bellModelPivot != null)
            bellModelPivot.localRotation = Quaternion.Euler(currentAngles.y, currentAngles.x, 0f);
        if (scopeModelPivot != null)
            scopeModelPivot.localRotation = Quaternion.Euler(-currentAngles.y * 0.8f, -currentAngles.x * 1.1f + 25f, 0f);

        if (changed) RefreshUIText();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseStudio();
        }
    }

    void RefreshUIText()
    {
        if (alignStatusLabel != null)
        {
            int pct = Mathf.RoundToInt(alignmentScore * 100f);
            alignStatusLabel.text = shadowSolved
                ? "光影叠合状态：已完全吻合（100%） · 全岛旧物刻度线索已记录"
                : "光影叠合进度：" + pct + "%（拖动鼠标或按 WASD 旋转右侧旧物）";
        }
        if (noteSequenceLabel != null)
        {
            string seq = "";
            for (int i = 0; i < playedNotes.Count; i++)
                seq += (i > 0 ? " · " : "") + NoteNames[playedNotes[i]].Substring(0, 2);
            noteSequenceLabel.text = string.IsNullOrEmpty(seq)
                ? "当前旋律：点击上方四枚音符开始弹奏"
                : "当前旋律：" + seq + (musicBoxSolved ? "（八音盒共鸣完成）" : "");
        }
    }

    public void CompleteShadowAlignment()
    {
        if (shadowSolved) return;
        shadowSolved = true;
        currentAngles = Vector2.zero;
        alignmentScore = 1f;
        PlayProceduralChime(880f, 0.4f);

        if (state != null)
        {
            foreach (var k in new[] { "recipe", "scale", "basket", "slipper", "doghouse", "blackboard", "hat", "scope", "log" })
            {
                string flag = "dog_sniff_" + k;
                if (!state.flags.Contains(flag)) state.flags.Add(flag);
            }
        }
        RefreshUIText();
        if (ui != null)
        {
            ui.ShowToast("铜门铃与黄铜望远镜光影吻合，已为你点亮全岛所有旧物刻度线索！", 4.0f);
        }
    }

    public void PlayNote(int noteIndex)
    {
        if (noteIndex < 0 || noteIndex >= NoteFreqs.Length) return;
        PlayProceduralChime(NoteFreqs[noteIndex], 0.45f);

        playedNotes.Add(noteIndex);
        if (playedNotes.Count > 4) playedNotes.RemoveAt(0);
        RefreshUIText();

        if (playedNotes.Count == 4 &&
            playedNotes[0] == 0 && playedNotes[1] == 1 && playedNotes[2] == 2 && playedNotes[3] == 3)
        {
            TriggerMusicBoxGrandResonance();
        }
    }

    public void TriggerMusicBoxGrandResonance()
    {
        musicBoxSolved = true;
        RefreshUIText();
        StartCoroutine(PlayMelodyRoutine());
    }

    IEnumerator PlayMelodyRoutine()
    {
        for (int i = 0; i < NoteFreqs.Length; i++)
        {
            PlayProceduralChime(NoteFreqs[i], 0.42f);
            yield return new WaitForSecondsRealtime(0.22f);
        }
        PlayProceduralChime(1046.5f, 0.65f);

        if (player != null) player.PlayEmote("hop");
        if (ui != null)
        {
            ui.ShowToast("四道海岛声波化作温暖的八音盒旋律，在小岛上空久久回响。", 4.0f);
        }
    }
}
