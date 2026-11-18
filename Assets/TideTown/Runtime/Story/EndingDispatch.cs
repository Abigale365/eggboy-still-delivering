using System;
using System.IO;
using UnityEngine;

public sealed class EndingDispatch : MonoBehaviour
{
	private sealed class ParcelSampleMeta
	{
		public string id;

		public string sceneName;

		public string title;

		public string storyTag;

		public string pipelineInfo;

		public Vector3 examineEuler;
	}

	private const string DraftKey = "TideTown.EndingDispatch.Draft";

	private const string RecipientKey = "TideTown.EndingDispatch.Recipient";

	private const string ItemIndexKey = "TideTown.EndingDispatch.ItemIndex";

	private const string StampedKey = "TideTown.EndingDispatch.Stamped";

	private static readonly ParcelSampleMeta[] Samples = new ParcelSampleMeta[4]
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

	private readonly IModelProvider models = new BuiltinModelProvider();

	private GameObject sample;

	private Transform modelPivot;

	private Light showcaseLight;

	private EggboyUI ui;

	private ChaseCamera chase;

	private bool restoreChase;

	private bool open;

	private bool postcardMode;

	private bool stamped;

	private bool autoRotate = true;

	private int selectedIndex;

	private string recipient = "";

	private string draft = "";

	private string status = "";

	private Vector3 currentEuler = new Vector3(14f, -25f, 0f);

	private float zoom = 1f;

	private float stampAnimTimer;

	private GUISkin dispatchSkin;

	private void Awake()
	{
		draft = PlayerPrefs.GetString("TideTown.EndingDispatch.Draft", "");
		recipient = PlayerPrefs.GetString("TideTown.EndingDispatch.Recipient", "山那边的朋友");
		selectedIndex = Mathf.Clamp(PlayerPrefs.GetInt("TideTown.EndingDispatch.ItemIndex", 0), 0, Samples.Length - 1);
		stamped = PlayerPrefs.GetInt("TideTown.EndingDispatch.Stamped", 0) == 1 && !string.IsNullOrWhiteSpace(draft);
		ui = UnityEngine.Object.FindObjectOfType<EggboyUI>();
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	private void Update()
	{
		if (open && !(sample == null))
		{
			if (stampAnimTimer > 0f)
			{
				stampAnimTimer = Mathf.Max(0f, stampAnimTimer - Time.unscaledDeltaTime);
			}
			float num = 0f;
			float num2 = 0f;
			if (Input.GetMouseButton(0) && Input.mousePosition.x > (float)Screen.width * 0.46f)
			{
				autoRotate = false;
				num2 = (0f - Input.GetAxis("Mouse X")) * 5f;
				num = Input.GetAxis("Mouse Y") * 5f;
			}
			if (Input.GetKey(KeyCode.LeftArrow))
			{
				autoRotate = false;
				num2 += 85f * Time.unscaledDeltaTime;
			}
			if (Input.GetKey(KeyCode.RightArrow))
			{
				autoRotate = false;
				num2 -= 85f * Time.unscaledDeltaTime;
			}
			if (Input.GetKey(KeyCode.UpArrow))
			{
				autoRotate = false;
				num += 75f * Time.unscaledDeltaTime;
			}
			if (Input.GetKey(KeyCode.DownArrow))
			{
				autoRotate = false;
				num -= 75f * Time.unscaledDeltaTime;
			}
			float axis = Input.GetAxis("Mouse ScrollWheel");
			if (Mathf.Abs(axis) > 0.001f)
			{
				zoom = Mathf.Clamp(zoom + axis * 0.75f, 0.7f, 1.55f);
			}
			if (Mathf.Abs(num) > 0.001f || Mathf.Abs(num2) > 0.001f)
			{
				currentEuler.x = Mathf.Clamp(currentEuler.x + num, -85f, 85f);
				currentEuler.y += num2;
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
	}

	private void Open()
	{
		open = true;
		if (ui != null)
		{
			ui.SetCinematic(on: true, "dispatch");
		}
		postcardMode = false;
		status = (stamped ? "已读取上次加盖邮戳的寄件存档。" : "");
		chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		restoreChase = chase != null && chase.enabled;
		if (restoreChase)
		{
			chase.enabled = false;
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		SelectSample(selectedIndex);
	}

	private void SelectSample(int index)
	{
		selectedIndex = Mathf.Clamp(index, 0, Samples.Length - 1);
		PlayerPrefs.SetInt("TideTown.EndingDispatch.ItemIndex", selectedIndex);
		currentEuler = new Vector3(14f, -25f, 0f);
		zoom = 1f;
		autoRotate = true;
		ParcelSampleMeta meta = Samples[selectedIndex];
		if (meta.id == "ending_oilcan")
		{
			models.Load("ending_oilcan", ShowSample, delegate(string reason)
			{
				GameObject gameObject2 = FindSceneObject(meta.sceneName);
				if (gameObject2 != null)
				{
					ShowSample(gameObject2);
				}
				else
				{
					status = reason;
				}
			});
			return;
		}
		GameObject gameObject = FindSceneObject(meta.sceneName);
		if (gameObject != null)
		{
			ShowSample(gameObject);
			return;
		}
		models.Load("ending_oilcan", ShowSample, delegate(string reason)
		{
			status = reason;
		});
	}

	private static GameObject FindSceneObject(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		return GameObject.Find(name);
	}

	private void ShowSample(GameObject prefab)
	{
		if (sample != null)
		{
			UnityEngine.Object.Destroy(sample);
		}
		Camera main = Camera.main;
		if (main == null)
		{
			status = "当前镜头无法展示样例。";
			return;
		}
		sample = new GameObject("DispatchSample");
		sample.transform.SetParent(main.transform, worldPositionStays: false);
		sample.transform.localPosition = new Vector3(0.46f * main.aspect * Mathf.Tan(main.fieldOfView * (MathF.PI / 180f) * 0.5f) * 3f, 0.04f, 3f);
		sample.transform.localRotation = Quaternion.identity;
		GameObject gameObject = new GameObject("DispatchShowcaseLight");
		gameObject.transform.SetParent(sample.transform, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(-0.7f, 0.8f, -1f);
		showcaseLight = gameObject.AddComponent<Light>();
		showcaseLight.type = LightType.Point;
		showcaseLight.color = new Color32(byte.MaxValue, 222, 172, byte.MaxValue);
		showcaseLight.range = 5f;
		showcaseLight.intensity = 2.3f;
		showcaseLight.shadows = LightShadows.None;
		GameObject gameObject2 = new GameObject("DispatchPivot");
		gameObject2.transform.SetParent(sample.transform, worldPositionStays: false);
		modelPivot = gameObject2.transform;
		GameObject gameObject3 = UnityEngine.Object.Instantiate(prefab, modelPivot, worldPositionStays: false);
		gameObject3.transform.localPosition = Vector3.zero;
		gameObject3.transform.localRotation = Quaternion.identity;
		gameObject3.SetActive(value: true);
		Collider[] componentsInChildren = gameObject3.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
		MonoBehaviour[] componentsInChildren2 = gameObject3.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].enabled = false;
		}
		Renderer[] componentsInChildren3 = gameObject3.GetComponentsInChildren<Renderer>(includeInactive: true);
		if (componentsInChildren3.Length != 0)
		{
			Renderer[] array = componentsInChildren3;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = true;
			}
			Bounds bounds = componentsInChildren3[0].bounds;
			array = componentsInChildren3;
			foreach (Renderer renderer in array)
			{
				bounds.Encapsulate(renderer.bounds);
			}
			gameObject3.transform.position += modelPivot.position - bounds.center;
			float num = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
			if (num > 0.001f)
			{
				modelPivot.localScale = Vector3.one * (0.95f / num);
			}
			modelPivot.localRotation = Quaternion.Euler(currentEuler);
		}
	}

	private void SaveDraftToDisk(bool withStamp)
	{
		if (string.IsNullOrWhiteSpace(draft))
		{
			status = "请先写下一句你想随信留下的话。";
			return;
		}
		string value = draft.Trim();
		string value2 = (string.IsNullOrWhiteSpace(recipient) ? "山那边的朋友" : recipient.Trim());
		PlayerPrefs.SetString("TideTown.EndingDispatch.Draft", value);
		PlayerPrefs.SetString("TideTown.EndingDispatch.Recipient", value2);
		PlayerPrefs.SetInt("TideTown.EndingDispatch.ItemIndex", selectedIndex);
		if (withStamp)
		{
			stamped = true;
			stampAnimTimer = 1.2f;
			PlayerPrefs.SetInt("TideTown.EndingDispatch.Stamped", 1);
		}
		PlayerPrefs.Save();
		status = (withStamp ? "已加盖【潮汐镇·新邮局】纪念邮戳，并保存在本机！" : "寄件内容已保存在本机。");
	}

	private void CapturePostcardScreenshot()
	{
		try
		{
			ScreenCapture.CaptureScreenshot(Path.Combine(Path.Combine(Application.dataPath, ".."), "TideTown_MemoryPostcard.png"));
			status = "明信片画面已拍照保存：TideTown_MemoryPostcard.png";
		}
		catch (Exception ex)
		{
			status = "截图已触发（" + ex.GetType().Name + "）";
		}
	}

	private void Close()
	{
		open = false;
		if (ui != null)
		{
			ui.SetCinematic(on: false, "dispatch");
		}
		postcardMode = false;
		if (sample != null)
		{
			UnityEngine.Object.Destroy(sample);
		}
		sample = null;
		modelPivot = null;
		if (restoreChase && chase != null)
		{
			chase.enabled = true;
		}
		restoreChase = false;
	}

	private void OnDestroy()
	{
		Close();
		if (dispatchSkin != null)
		{
			UnityEngine.Object.Destroy(dispatchSkin);
		}
	}

	private void OnGUI()
	{
		if (ui != null && (ui.TitleShowing || ui.Paused || ui.LetterOpen))
		{
			return;
		}
		if (dispatchSkin == null)
		{
			dispatchSkin = UnityEngine.Object.Instantiate(GUI.skin);
			dispatchSkin.font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
			dispatchSkin.box.fontSize = 14;
			dispatchSkin.label.fontSize = 14;
			dispatchSkin.button.fontSize = 14;
			dispatchSkin.textField.fontSize = 14;
			dispatchSkin.textArea.fontSize = 14;
		}
		GUISkin skin = GUI.skin;
		GUI.skin = dispatchSkin;
		if (!open)
		{
			if (GUI.Button(new Rect(Screen.width - 216, Screen.height - 74, 192f, 44f), "去新邮局寄件 · 记忆打包"))
			{
				Open();
			}
		}
		else if (postcardMode)
		{
			DrawPostcardView();
		}
		else
		{
			DrawWorkbenchView();
		}
		GUI.skin = skin;
	}

	private void DrawWorkbenchView()
	{
		ParcelSampleMeta parcelSampleMeta = Samples[selectedIndex];
		float width = Mathf.Clamp((float)Screen.width * 0.46f, 340f, 540f);
		float height = Mathf.Clamp((float)Screen.height - 44f, 440f, 640f);
		GUILayout.BeginArea(new Rect(22f, 22f, width, height), GUI.skin.box);
		GUILayout.Label("【山顶新邮局 · 私人记忆寄件与 3D 资产工作台】");
		GUILayout.Label("1. 选择要随信寄出的 3D 记忆物件（右侧支持鼠标拖拽 360° 翻转 / 滚轮缩放）：");
		GUILayout.BeginHorizontal();
		for (int i = 0; i < Samples.Length; i++)
		{
			if (GUILayout.Button(((i == selectedIndex) ? "★ " : "") + Samples[i].title.Split(' ')[0], GUILayout.Height(28f)))
			{
				SelectSample(i);
			}
		}
		GUILayout.EndHorizontal();
		GUILayout.Label("当前物件：" + parcelSampleMeta.title);
		GUILayout.Label("物件故事：" + parcelSampleMeta.storyTag);
		GUILayout.Label(parcelSampleMeta.pipelineInfo);
		GUILayout.BeginHorizontal();
		if (GUILayout.Button(autoRotate ? "暂停旋转" : "自动旋转", GUILayout.Height(26f)))
		{
			autoRotate = !autoRotate;
		}
		if (GUILayout.Button("查看正面", GUILayout.Height(26f)))
		{
			autoRotate = false;
			currentEuler = new Vector3(12f, -20f, 0f);
		}
		if (GUILayout.Button("查看刻痕面", GUILayout.Height(26f)))
		{
			autoRotate = false;
			currentEuler = parcelSampleMeta.examineEuler;
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
			SaveDraftToDisk(withStamp: false);
		}
		if (GUILayout.Button("加盖潮汐镇邮戳 · 生成明信片", GUILayout.Height(34f)))
		{
			SaveDraftToDisk(withStamp: true);
			if (!string.IsNullOrWhiteSpace(draft))
			{
				postcardMode = true;
			}
		}
		GUILayout.EndHorizontal();
		if (stamped)
		{
			GUILayout.Label("邮戳状态：【已加盖 潮汐镇·新邮局 纪念邮戳】");
			if (GUILayout.Button("查看已封装的《潮汐镇记忆明信片》", GUILayout.Height(28f)))
			{
				postcardMode = true;
			}
		}
		if (!string.IsNullOrEmpty(status))
		{
			GUILayout.Label(status);
		}
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("回到山顶结局画面", GUILayout.Height(30f)))
		{
			Close();
		}
		GUILayout.EndArea();
	}

	private void DrawPostcardView()
	{
		ParcelSampleMeta parcelSampleMeta = Samples[selectedIndex];
		float width = Mathf.Clamp((float)Screen.width * 0.46f, 340f, 540f);
		float height = Mathf.Clamp((float)Screen.height - 44f, 420f, 580f);
		GUILayout.BeginArea(new Rect(22f, 22f, width, height), GUI.skin.box);
		GUILayout.Label("==========================================");
		GUILayout.Label("   《 潮 汐 镇 · 记 忆 邮 局 明 信 片 》   NO.2026-1119");
		GUILayout.Label("==========================================");
		GUILayout.Space(6f);
		GUILayout.Label("收件人：致 " + (string.IsNullOrWhiteSpace(recipient) ? "山那边的朋友" : recipient.Trim()));
		GUILayout.Label("随信 3D 信物：" + parcelSampleMeta.title);
		GUILayout.Label("资产来源：" + parcelSampleMeta.pipelineInfo);
		GUILayout.Space(8f);
		GUILayout.Label("信件正文：");
		GUILayout.BeginVertical(GUI.skin.box);
		GUILayout.Label(string.IsNullOrWhiteSpace(draft) ? "（空）" : draft.Trim());
		GUILayout.Space(8f);
		GUILayout.Label("—— 投递员：机器人邮差 EGGBOY 与小黑");
		GUILayout.EndVertical();
		GUILayout.Space(8f);
		GUILayout.Label("已盖邮戳记录：");
		GUILayout.Label("  [烤炉]   [小黑]   [灯塔]   [回家]");
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
