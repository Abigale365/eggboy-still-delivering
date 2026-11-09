// WP-15 / P1：山顶新邮局 · 私人记忆寄件工作台与 Lux3D 资产检视、明信片盖戳系统。
// 合并预生成油壶样例（IModelProvider）、多件 Lux3D 主线纪念物 360° 检视、本机持久化保存与邮戳封装仪式。
using System.IO;
using UnityEngine;

public sealed class EndingDispatch : MonoBehaviour
{
    const string DraftKey = "TideTown.EndingDispatch.Draft";
    const string RecipientKey = "TideTown.EndingDispatch.Recipient";
    const string ItemIndexKey = "TideTown.EndingDispatch.ItemIndex";
    const string StampedKey = "TideTown.EndingDispatch.Stamped";

    sealed class ParcelSampleMeta
    {
        public string id;
        public string sceneName;
        public string title;
        public string storyTag;
        public string pipelineInfo;
        public Vector3 examineEuler;
    }

    static readonly ParcelSampleMeta[] Samples =
    {
        new ParcelSampleMeta
        {
            id = "ending_oilcan",
            sceneName = "LH_oil",
            title = "守塔人煤油壶 (D1)",
            storyTag = "船棚与灯塔的守望信物 · 预生成私人记忆物件样例",
            pipelineInfo = "管线档案：实拍输入 → Aholo Lux3D 生成 → 比例校准 (高 0.35m) → 3D 打印切片就绪",
            examineEuler = new Vector3(-28f, 135f, 0f)
        },
        new ParcelSampleMeta
        {
            id = "hat",
            sceneName = "LH_hat",
            title = "守塔人白檐帽 (D5)",
            storyTag = "陪老陈吹了三十一年海风的旧帽 · 帽檐内衬缝着航标站编号",
            pipelineInfo = "管线档案：Aholo Lux3D 生成 → Refine Skill 重点验收件 (19,687 面高保真拓扑)",
            examineEuler = new Vector3(-68f, 25f, 0f)
        },
        new ParcelSampleMeta
        {
            id = "basket",
            sceneName = "Bakery_basket",
            title = "林阿婆的面包篮 (A4)",
            storyTag = "潮汐镇第一站信物 · 藤编篮底压着给阿萤八岁生日的字条",
            pipelineInfo = "管线档案：Aholo Lux3D 生成 → 暖色陶土色板统一 → 底部刻痕交互绑定",
            examineEuler = new Vector3(-70f, 35f, 0f)
        },
        new ParcelSampleMeta
        {
            id = "desk",
            sceneName = "S_desk",
            title = "刻画小狗的旧课桌 (C8)",
            storyTag = "学校与邮局门前的木桌 · 承载阿萤与老周最后一封来信",
            pipelineInfo = "管线档案：Aholo Lux3D 生成 → 碰撞贴合审计通过 → 双场景复用资产",
            examineEuler = new Vector3(-45f, -110f, 0f)
        }
    };

    readonly IModelProvider models = new BuiltinModelProvider();
    GameObject sample;
    Transform modelPivot;
    Light showcaseLight;
    EggboyUI ui;
    ChaseCamera chase;
    bool restoreChase;
    bool open;
    bool postcardMode;
    bool stamped;
    bool autoRotate = true;
    int selectedIndex;
    string recipient = "";
    string draft = "";
    string status = "";
    Vector3 currentEuler = new Vector3(14f, -25f, 0f);
    float zoom = 1f;
    float stampAnimTimer;
    GUISkin dispatchSkin;

    void Awake()
    {
        draft = PlayerPrefs.GetString(DraftKey, "");
        recipient = PlayerPrefs.GetString(RecipientKey, "山那边的朋友");
        selectedIndex = Mathf.Clamp(PlayerPrefs.GetInt(ItemIndexKey, 0), 0, Samples.Length - 1);
        stamped = PlayerPrefs.GetInt(StampedKey, 0) == 1 && !string.IsNullOrWhiteSpace(draft);
        ui = FindObjectOfType<EggboyUI>();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!open || sample == null) return;

        if (stampAnimTimer > 0f)
            stampAnimTimer = Mathf.Max(0f, stampAnimTimer - Time.unscaledDeltaTime);

        // 支持鼠标拖拽 360° 自由检视或键盘方向键旋转展示模型
        float rotX = 0f, rotY = 0f;
        if (Input.GetMouseButton(0) && Input.mousePosition.x > Screen.width * 0.46f)
        {
            autoRotate = false;
            rotY = -Input.GetAxis("Mouse X") * 5.0f;
            rotX = Input.GetAxis("Mouse Y") * 5.0f;
        }
        if (Input.GetKey(KeyCode.LeftArrow)) { autoRotate = false; rotY += 85f * Time.unscaledDeltaTime; }
        if (Input.GetKey(KeyCode.RightArrow)) { autoRotate = false; rotY -= 85f * Time.unscaledDeltaTime; }
        if (Input.GetKey(KeyCode.UpArrow)) { autoRotate = false; rotX += 75f * Time.unscaledDeltaTime; }
        if (Input.GetKey(KeyCode.DownArrow)) { autoRotate = false; rotX -= 75f * Time.unscaledDeltaTime; }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            zoom = Mathf.Clamp(zoom + scroll * 0.75f, 0.7f, 1.55f);

        if (Mathf.Abs(rotX) > 0.001f || Mathf.Abs(rotY) > 0.001f)
        {
            currentEuler.x = Mathf.Clamp(currentEuler.x + rotX, -85f, 85f);
            currentEuler.y += rotY;
        }
        else if (autoRotate)
        {
            currentEuler.y += 24f * Time.unscaledDeltaTime;
        }

        if (modelPivot != null)
        {
            modelPivot.localRotation = Quaternion.Euler(currentEuler);
            sample.transform.localScale = Vector3.one * zoom;
        }
    }

    void Open()
    {
        open = true;
        postcardMode = false;
        status = stamped ? "已读取上次加盖邮戳的寄件存档。" : "";
        chase = FindObjectOfType<ChaseCamera>();
        restoreChase = chase != null && chase.enabled;
        if (restoreChase) chase.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SelectSample(selectedIndex);
    }

    void SelectSample(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, Samples.Length - 1);
        PlayerPrefs.SetInt(ItemIndexKey, selectedIndex);
        currentEuler = new Vector3(14f, -25f, 0f);
        zoom = 1f;
        autoRotate = true;

        var meta = Samples[selectedIndex];
        if (meta.id == "ending_oilcan")
        {
            models.Load("ending_oilcan", ShowSample, reason =>
            {
                var fallback = FindSceneObject(meta.sceneName);
                if (fallback != null) ShowSample(fallback);
                else status = reason;
            });
        }
        else
        {
            var sceneObj = FindSceneObject(meta.sceneName);
            if (sceneObj != null) ShowSample(sceneObj);
            else models.Load("ending_oilcan", ShowSample, reason => status = reason);
        }
    }

    static GameObject FindSceneObject(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return GameObject.Find(name);
    }

    void ShowSample(GameObject prefab)
    {
        if (sample != null) Destroy(sample);
        var camera = Camera.main;
        if (camera == null) { status = "当前镜头无法展示样例。"; return; }

        sample = new GameObject("DispatchSample");
        sample.transform.SetParent(camera.transform, false);
        sample.transform.localPosition = new Vector3(
            0.46f * camera.aspect * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 3f,
            0.04f, 3f);
        sample.transform.localRotation = Quaternion.identity;

        var lightGo = new GameObject("DispatchShowcaseLight");
        lightGo.transform.SetParent(sample.transform, false);
        lightGo.transform.localPosition = new Vector3(-0.7f, 0.8f, -1.0f);
        showcaseLight = lightGo.AddComponent<Light>();
        showcaseLight.type = LightType.Point;
        showcaseLight.color = new Color32(255, 222, 172, 255);
        showcaseLight.range = 5.0f;
        showcaseLight.intensity = 2.3f;
        showcaseLight.shadows = LightShadows.None;

        var pivotGo = new GameObject("DispatchPivot");
        pivotGo.transform.SetParent(sample.transform, false);
        modelPivot = pivotGo.transform;

        var visual = Instantiate(prefab, modelPivot, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.SetActive(true);
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var mb in visual.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        foreach (var r in renderers) r.enabled = true;
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.transform.position += modelPivot.position - bounds.center;
        float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (size > 0.001f) modelPivot.localScale = Vector3.one * (0.95f / size);
        modelPivot.localRotation = Quaternion.Euler(currentEuler);
    }

    void SaveDraftToDisk(bool withStamp)
    {
        if (string.IsNullOrWhiteSpace(draft))
        {
            status = "请先写下一句你想随信留下的话。";
            return;
        }
        string cleanDraft = draft.Trim();
        string cleanRecip = string.IsNullOrWhiteSpace(recipient) ? "山那边的朋友" : recipient.Trim();
        PlayerPrefs.SetString(DraftKey, cleanDraft);
        PlayerPrefs.SetString(RecipientKey, cleanRecip);
        PlayerPrefs.SetInt(ItemIndexKey, selectedIndex);
        if (withStamp)
        {
            stamped = true;
            stampAnimTimer = 1.2f;
            PlayerPrefs.SetInt(StampedKey, 1);
        }
        PlayerPrefs.Save();
        status = withStamp
            ? "已加盖【潮汐镇·新邮局】纪念邮戳，并保存在本机！"
            : "寄件内容已保存在本机。";
    }

    void CapturePostcardScreenshot()
    {
        try
        {
            string dir = Path.Combine(Application.dataPath, "..");
            string file = Path.Combine(dir, "TideTown_MemoryPostcard.png");
            ScreenCapture.CaptureScreenshot(file);
            status = "明信片画面已拍照保存：TideTown_MemoryPostcard.png";
        }
        catch (System.Exception e)
        {
            status = "截图已触发（" + e.GetType().Name + "）";
        }
    }

    void Close()
    {
        open = false;
        postcardMode = false;
        if (sample != null) Destroy(sample);
        sample = null;
        modelPivot = null;
        if (restoreChase && chase != null) chase.enabled = true;
        restoreChase = false;
    }

    void OnDestroy()
    {
        Close();
        if (dispatchSkin != null) Destroy(dispatchSkin);
    }

    void OnGUI()
    {
        if (ui != null && (ui.TitleShowing || ui.Paused || ui.LetterOpen)) return;
        if (dispatchSkin == null)
        {
            dispatchSkin = Instantiate(GUI.skin);
            dispatchSkin.font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
            dispatchSkin.box.fontSize = 14;
            dispatchSkin.label.fontSize = 14;
            dispatchSkin.button.fontSize = 14;
            dispatchSkin.textField.fontSize = 14;
            dispatchSkin.textArea.fontSize = 14;
        }
        var previousSkin = GUI.skin;
        GUI.skin = dispatchSkin;

        if (!open)
        {
            if (GUI.Button(new Rect(Screen.width - 216, Screen.height - 74, 192, 44), "去新邮局寄件 · 记忆打包"))
                Open();
        }
        else if (postcardMode)
        {
            DrawPostcardView();
        }
        else
        {
            DrawWorkbenchView();
        }

        GUI.skin = previousSkin;
    }

    void DrawWorkbenchView()
    {
        var meta = Samples[selectedIndex];
        float width = Mathf.Clamp(Screen.width * 0.46f, 340f, 540f);
        float height = Mathf.Clamp(Screen.height - 44f, 440f, 640f);
        GUILayout.BeginArea(new Rect(22f, 22f, width, height), GUI.skin.box);

        GUILayout.Label("【山顶新邮局 · 私人记忆寄件与 3D 资产工作台】");
        GUILayout.Label("1. 选择要随信寄出的 3D 记忆物件（右侧支持鼠标拖拽 360° 翻转 / 滚轮缩放）：");

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Samples.Length; i++)
        {
            string btnLabel = (i == selectedIndex ? "★ " : "") + Samples[i].title.Split(' ')[0];
            if (GUILayout.Button(btnLabel, GUILayout.Height(28f)))
                SelectSample(i);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("当前物件：" + meta.title);
        GUILayout.Label("物件故事：" + meta.storyTag);
        GUILayout.Label(meta.pipelineInfo);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(autoRotate ? "暂停旋转" : "自动旋转", GUILayout.Height(26f)))
            autoRotate = !autoRotate;
        if (GUILayout.Button("查看正面", GUILayout.Height(26f)))
        {
            autoRotate = false;
            currentEuler = new Vector3(12f, -20f, 0f);
        }
        if (GUILayout.Button("查看刻痕面", GUILayout.Height(26f)))
        {
            autoRotate = false;
            currentEuler = meta.examineEuler;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("2. 填写收件人与你想留下的话：");
        GUILayout.BeginHorizontal();
        GUILayout.Label("寄给：", GUILayout.Width(48f));
        recipient = GUILayout.TextField(recipient, 40);
        GUILayout.EndHorizontal();

        draft = GUILayout.TextArea(draft, 240, GUILayout.Height(105f));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("保存到本机", GUILayout.Height(34f)))
        {
            SaveDraftToDisk(false);
        }
        if (GUILayout.Button("加盖潮汐镇邮戳 · 生成明信片", GUILayout.Height(34f)))
        {
            SaveDraftToDisk(true);
            if (!string.IsNullOrWhiteSpace(draft))
                postcardMode = true;
        }
        GUILayout.EndHorizontal();

        if (stamped)
        {
            GUILayout.Label("邮戳状态：【已加盖 潮汐镇·新邮局 纪念邮戳】");
            if (GUILayout.Button("查看已封装的《潮汐镇记忆明信片》", GUILayout.Height(28f)))
                postcardMode = true;
        }

        if (!string.IsNullOrEmpty(status))
            GUILayout.Label(status);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("回到山顶结局画面", GUILayout.Height(30f)))
            Close();

        GUILayout.EndArea();
    }

    void DrawPostcardView()
    {
        var meta = Samples[selectedIndex];
        float width = Mathf.Clamp(Screen.width * 0.46f, 340f, 540f);
        float height = Mathf.Clamp(Screen.height - 44f, 420f, 580f);
        GUILayout.BeginArea(new Rect(22f, 22f, width, height), GUI.skin.box);

        GUILayout.Label("==========================================");
        GUILayout.Label("   《 潮 汐 镇 · 记 忆 邮 局 明 信 片 》   NO.2026-1119");
        GUILayout.Label("==========================================");
        GUILayout.Space(6f);

        GUILayout.Label("收件人：致 " + (string.IsNullOrWhiteSpace(recipient) ? "山那边的朋友" : recipient.Trim()));
        GUILayout.Label("随信 3D 信物：" + meta.title);
        GUILayout.Label("资产来源：" + meta.pipelineInfo);
        GUILayout.Space(8f);

        GUILayout.Label("信件正文：");
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(string.IsNullOrWhiteSpace(draft) ? "（空）" : draft.Trim());
        GUILayout.Space(8f);
        GUILayout.Label("—— 投递员：机器人邮差 EGGBOY 与小黑");
        GUILayout.EndVertical();

        GUILayout.Space(8f);
        GUILayout.Label("已盖邮戳记录：");
        GUILayout.Label("  [邮戳 1/3 · 面包店暖炉]   [邮戳 2/3 · 学校与小黑]");
        GUILayout.Label("  [邮戳 3/3 · 灯塔与山海]   [★ 新邮局 · 私人记忆已封装]");

        if (!string.IsNullOrEmpty(status))
        {
            GUILayout.Space(4f);
            GUILayout.Label(status);
        }

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("拍照保存明信片截图", GUILayout.Height(34f)))
        {
            CapturePostcardScreenshot();
        }
        if (GUILayout.Button("返回继续编辑", GUILayout.Height(34f)))
        {
            postcardMode = false;
        }
        if (GUILayout.Button("回到结局", GUILayout.Height(34f)))
        {
            Close();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
