using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class AIAgentCalibrationDirector : MonoBehaviour
{
	private static AIAgentCalibrationDirector instance;

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private StoryDirector director;

	private GameState state;

	private bool consoleOpen;

	private RectTransform consoleCard;

	private Text hardwareTelemetryText;

	private Text wishFeedbackText;

	private int lastDeviceState = -1;

	private InputField promptInput;

	private Text achievementGalleryText;

	private readonly List<Transform> forgedRuntimeObjects = new List<Transform>();

	private int forgedCounter;

	private Button bagOutButton;

	private Text bagOutText;

	public static bool IsOpen
	{
		get
		{
			if (instance != null)
			{
				return instance.consoleOpen;
			}
			return false;
		}
	}

	public Lux3DSpawn LastWishSpawn { get; private set; }

	public string LastWishKind { get; private set; } = "";


	public WishToy LastWishToy { get; private set; }

	public static void ToggleConsole()
	{
		if (!(instance == null) && (instance.consoleOpen || !instance.OtherOverlayOpen()))
		{
			instance.SetConsoleOpen(!instance.consoleOpen);
		}
	}

	private bool OtherOverlayOpen()
	{
		if (ui != null)
		{
			if (!ui.Paused && !ui.TitleShowing && !ui.LetterOpen && !ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen && !AvantGardeGameplayHub.AnyModalOpen && !WishInspectModal.IsOpen)
			{
				return EmoteWheel.IsOpen;
			}
			return true;
		}
		return false;
	}

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
	{
		instance = this;
		player = p;
		ui = u;
		world = w;
		director = d;
		state = s;
	}

	private void SetConsoleOpen(bool open)
	{
		EnsureConsoleUI();
		if (!(consoleCard == null))
		{
			consoleOpen = open;
			consoleCard.gameObject.SetActive(open);
			if (open)
			{
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
				MenuController.NoteScriptUnlock();
				RefreshAchievementGallery();
				RefreshBagButton();
			}
		}
	}

	public static void DebugSetConsole(bool open)
	{
		if (instance != null)
		{
			instance.SetConsoleOpen(open);
		}
	}

	private void EnsureConsoleUI()
	{
		if (consoleCard != null)
		{
			return;
		}
		RectTransform rectTransform = TideTownStyleUI.FindHudCanvasRoot(ui);
		if (rectTransform == null)
		{
			return;
		}
		consoleCard = TideTownStyleUI.CreateParchmentCard(rectTransform, "PostmanWishAndTrophySheet", 660f, 560f, 18);
		RectTransform rectTransform2 = consoleCard;
		Vector2 anchorMin = (consoleCard.anchorMax = new Vector2(1f, 0.5f));
		rectTransform2.anchorMin = anchorMin;
		consoleCard.pivot = new Vector2(1f, 0.5f);
		consoleCard.anchoredPosition = new Vector2(-24f, 4f);
		TideTownStyleUI.CreateText(consoleCard, "邮差的心愿笺与图鉴", 18, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, new Vector2(0f, 246f), new Vector2(608f, 28f), bold: true);
		hardwareTelemetryText = TideTownStyleUI.CreateText(consoleCard, "", 12, TideTownStyleUI.InkMuted, TextAnchor.UpperLeft, new Vector2(0f, 206f), new Vector2(608f, 44f));
		wishFeedbackText = TideTownStyleUI.CreateText(consoleCard, "写下你的心愿（或直接点选下方灵感词牌），即可按你的喜好实时生成 3D 物件：\n• 给小黑造美食/新玩具可替代找红球 · 在渡口造浮船可乘船过海 · 在展台造摆件可永久装点小镇", 13, TideTownStyleUI.InkDark, TextAnchor.UpperLeft, new Vector2(0f, 154f), new Vector2(608f, 46f));
		Image image = TideTownStyleUI.CreatePanel(consoleCard, "WishPromptInputBg", 412f, 36f, TideTownStyleUI.SlipColor);
		image.rectTransform.anchoredPosition = new Vector2(-98f, 104f);
		Text textComponent = TideTownStyleUI.CreateText(image.transform, "", 14, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(392f, 30f));
		Text placeholder = TideTownStyleUI.CreateText(image.transform, "输入你想造的 3D 物件（如：香烤肉骨头、发光飞盘、大黄鸭浮船、海螺八音盒）...", 12, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(392f, 30f));
		promptInput = image.gameObject.AddComponent<InputField>();
		promptInput.textComponent = textComponent;
		promptInput.placeholder = placeholder;
		promptInput.text = "香喷喷的烤肉骨头";
		TideTownStyleUI.CreateWarmButton(consoleCard, "按心愿实时 3D 造物", new Vector2(210f, 104f), new Vector2(188f, 36f), primaryAmber: true, delegate
		{
			SpawnLiveWishObjectInWorld((promptInput != null) ? promptInput.text : "香喷喷的烤肉骨头");
		});
		string[] array = new string[6] { "词牌：香烤肉骨头 (哄小黑)", "词牌：发光飞盘玩具 (哄小黑)", "词牌：大黄鸭浮船 (渡海)", "词牌：海龟背壳浮台 (渡海)", "词牌：暖阳防风炉罩 (助燃)", "词牌：星空海螺八音盒 (展台)" };
		string[] cardPrompts = new string[6] { "香喷喷的烤肉骨头", "发光飞盘玩具", "童话大黄鸭浮船", "海龟背壳浮台", "暖阳防风炉罩", "星空海螺八音盒" };
		for (int i = 0; i < 6; i++)
		{
			int idx = i;
			int num = i / 3;
			int num2 = i % 3;
			float x = -204f + (float)num2 * 204f;
			float y = 54f - (float)num * 40f;
			TideTownStyleUI.CreateWarmButton(consoleCard, array[i], new Vector2(x, y), new Vector2(194f, 32f), primaryAmber: false, delegate
			{
				if (promptInput != null)
				{
					promptInput.text = cardPrompts[idx];
				}
				SpawnLiveWishObjectInWorld(cardPrompts[idx]);
			});
		}
		Image image2 = TideTownStyleUI.CreatePanel(consoleCard, "AchievementGalleryBg", 608f, 198f, new Color32(236, 223, 198, byte.MaxValue));
		image2.rectTransform.anchoredPosition = new Vector2(0f, -92f);
		achievementGalleryText = TideTownStyleUI.CreateText(image2.transform, "", 12, TideTownStyleUI.InkDark, TextAnchor.UpperLeft, Vector2.zero, new Vector2(584f, 182f));
		TideTownStyleUI.CreateWarmButton(consoleCard, "开关摄像头 AR 挥手感应", new Vector2(-204f, -228f), new Vector2(194f, 34f), primaryAmber: false, OnClickToggleWebcam);
		bagOutButton = TideTownStyleUI.CreateWarmButton(consoleCard, "从邮差包取出", new Vector2(0f, -228f), new Vector2(194f, 34f), primaryAmber: false, OnClickTakeOutOfBag);
		bagOutText = bagOutButton.GetComponentInChildren<Text>();
		TideTownStyleUI.CreateWarmButton(consoleCard, "收起心愿笺 (V / 手柄Y)", new Vector2(204f, -228f), new Vector2(194f, 34f), primaryAmber: false, delegate
		{
			SetConsoleOpen(open: false);
		});
		consoleCard.gameObject.SetActive(value: false);
	}

	private void RefreshBagButton()
	{
		if (!(bagOutText == null))
		{
			bagOutText.text = "从邮差包取出（" + WishBag.Count + "/" + 6 + "）";
			bagOutButton.interactable = WishBag.Count > 0;
		}
	}

	private void OnClickTakeOutOfBag()
	{
		WishToy wishToy = WishBag.TakeOutLatest(player);
		if (wishFeedbackText != null)
		{
			wishFeedbackText.text = ((wishToy != null) ? ("「" + wishToy.description + "」从邮差包里拿出来，放在你脚边了。") : "邮差包里还空着。");
		}
		RefreshBagButton();
	}

	private void OnClickToggleWebcam()
	{
		if (!(HardwareAndSensorHub.Instance == null))
		{
			bool flag = HardwareAndSensorHub.Instance.ToggleBuiltInWebcam();
			ShadowAndChimeSystem.PlayProceduralChime(flag ? 659.25f : 440f, 0.22f);
			if (wishFeedbackText != null)
			{
				wishFeedbackText.text = (flag ? "到了塔顶，对着镜头挥挥手，山那边就能看见灯光。" : "镜头歇一歇。到了塔顶，还能按空格挥手。");
			}
		}
	}

	private void RefreshAchievementGallery()
	{
		if (achievementGalleryText == null)
		{
			return;
		}
		TideTownAchievementSystem tideTownAchievementSystem = TideTownAchievementSystem.Instance;
		if (tideTownAchievementSystem == null)
		{
			achievementGalleryText.text = "沿路发生的故事会收在这里。";
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"旅途收下了 {tideTownAchievementSystem.UnlockedCount} / {tideTownAchievementSystem.TotalCount} 段回忆");
		IReadOnlyList<TideTownAchievementSystem.AchDef> allDefinitions = tideTownAchievementSystem.AllDefinitions;
		for (int i = 0; i < allDefinitions.Count; i++)
		{
			TideTownAchievementSystem.AchDef achDef = allDefinitions[i];
			string text = (tideTownAchievementSystem.IsUnlocked(achDef.id) ? "●" : "○");
			stringBuilder.Append("  " + text + " " + achDef.title + "（" + achDef.category + "）");
			if (i % 2 == 1 || i == allDefinitions.Count - 1)
			{
				stringBuilder.AppendLine();
			}
			else
			{
				stringBuilder.Append("    |  ");
			}
		}
		achievementGalleryText.text = stringBuilder.ToString();
	}

	private static bool HasAny(string text, params string[] keys)
	{
		foreach (string value in keys)
		{
			if (text.Contains(value))
			{
				return true;
			}
		}
		return false;
	}

	public void SpawnLiveWishObjectInWorld(string rawDescription)
	{
		if (!(player == null))
		{
			string text = (string.IsNullOrWhiteSpace(rawDescription) ? "星空海螺八音盒" : rawDescription.Trim());
			if (text.Length > 40)
			{
				text = text.Substring(0, 40);
			}
			forgedCounter++;
			WorldMechanismDirector worldMechanismDirector = Object.FindObjectOfType<WorldMechanismDirector>();
			Vector3 position = player.transform.position;
			bool flag = HasAny(text, "骨", "肉", "蛋糕", "肠", "鱼", "饼", "食", "饭", "奶", "糖", "面包", "鸡腿", "罐头");
			bool flag2 = HasAny(text, "船", "筏", "浮", "桥", "海龟", "艇", "舟", "鲸");
			bool flag3 = !flag2 && HasAny(text, "飞盘", "玩具", "球", "鸭", "娃娃", "玩偶", "铃铛");
			bool flag4 = HasAny(text, "柴", "木头", "火", "炉", "炭");
			Vector3 vector = ((worldMechanismDirector != null) ? worldMechanismDirector.GetCrossingChannelCenter() : new Vector3(15.3f, 1.35f, 11.7f));
			Vector3 vector2 = ((worldMechanismDirector != null) ? worldMechanismDirector.GetDogBowlPosition() : new Vector3(17.5f, 3.05f, -5.2f));
			GameObject gameObject = GameObject.Find("Bakery_stove");
			Vector3 vector3 = ((gameObject != null) ? gameObject.transform.position : new Vector3(-10.55f, 3f, -5.84f));
			bool flag5 = Vector3.Distance(position, vector) < 7.5f;
			bool flag6 = Vector3.Distance(position, vector2) < 9.5f && state != null && state.step.StartsWith("S");
			bool flag7 = Vector3.Distance(position, vector3) < 11f;
			if (flag2 || (flag5 && !flag && !flag3 && !flag4))
			{
				SpawnWishBoat(text, vector, worldMechanismDirector);
			}
			else if (flag || flag3 || flag6)
			{
				SpawnWishDogGift(text, flag || !flag3, vector2);
			}
			else if (flag4 && flag7)
			{
				SpawnWishFirewood(text, vector3);
			}
			else
			{
				SpawnWishKeepsake(text, worldMechanismDirector);
			}
			RefreshAchievementGallery();
		}
	}

	private Lux3DSpawn SpawnWish(string description, string category, Vector3 pos, Quaternion rot, Transform parent, Color32 glow, float glowRange)
	{
		Lux3DService lux3DService = Lux3DService.Ensure();
		Lux3DLibraryEntry entry = lux3DService.Match(description, category);
		Lux3DSpawn lux3DSpawn = lux3DService.Spawn(description, entry, pos, rot, parent);
		lux3DSpawn.root.name = "Wish_" + category + "_" + forgedCounter;
		AttachWarmGlow(lux3DSpawn.root.transform, glow, glowRange);
		LastWishSpawn = lux3DSpawn;
		LastWishKind = category;
		if (player != null)
		{
			player.PlayEmote("give");
		}
		return lux3DSpawn;
	}

	private string WaitNote(Lux3DSpawn s)
	{
		if (s.state == "generating")
		{
			return "心愿已经寄出。先用手边这件吧。";
		}
		if (s.state == "capped")
		{
			return "邮包装满了，镇上刚好有一件可用。";
		}
		return "";
	}

	private void SpawnWishDogGift(string description, bool isFood, Vector3 bowlPos)
	{
		Vector3 vector = ((Vector3.Distance(player.transform.position, bowlPos) < 12f) ? (bowlPos + new Vector3(0.28f, 0.02f, 0.22f)) : (player.transform.position + new Vector3(Mathf.Sin(player.yaw), 0f, Mathf.Cos(player.yaw)) * 1.4f));
		if (world != null)
		{
			vector.y = world.GroundAt(vector.x, vector.z, vector.y + 1f);
		}
		Lux3DSpawn s = SpawnWish(description, isFood ? "food" : "toy", vector, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), base.transform, isFood ? new Color32(byte.MaxValue, 195, 110, byte.MaxValue) : new Color32(130, 230, byte.MaxValue, byte.MaxValue), 3.2f);
		bool flag = Vector3.Distance(vector, bowlPos) < 3f;
		MakeToy(s, description, isFood ? "food" : "toy", (flag || isFood) ? "小黑" : "潮汐镇", autoPlace: true, flag ? 1.2f : 3f);
		if (director != null && flag)
		{
			director.CompleteSchoolWithCustomGift(description, isFood);
		}
		if (wishFeedbackText != null)
		{
			wishFeedbackText.text = (flag ? ("给小黑的「" + description + "」放到碗边了。" + WaitNote(s)) : ("「" + description + "」就在你脚边。" + WaitNote(s)));
		}
	}

	private void SpawnWishBoat(string description, Vector3 channelPos, WorldMechanismDirector worldMech)
	{
		Vector3 pos = new Vector3(channelPos.x, Mathf.Max(channelPos.y, WaterLevel.StaticY) - 0.05f, channelPos.z);
		Vector3 vector = ((worldMech != null) ? worldMech.CrossingDirection : (channelPos - player.transform.position));
		vector.y = 0f;
		Quaternion rot = ((vector.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(vector.normalized, Vector3.up) : Quaternion.identity);
		Lux3DSpawn lux3DSpawn = SpawnWish(description, "boat", pos, rot, base.transform, new Color32(byte.MaxValue, 220, 125, byte.MaxValue), 5f);
		lux3DSpawn.root.AddComponent<WishBob>();
		BoxCollider boxCollider = lux3DSpawn.root.GetComponent<BoxCollider>();
		if (boxCollider == null)
		{
			boxCollider = lux3DSpawn.root.AddComponent<BoxCollider>();
		}
		int num = LayerMask.NameToLayer("Ground");
		lux3DSpawn.root.layer = ((num >= 0) ? num : 6);
		boxCollider.center = new Vector3(0f, 0.1f, 0f);
		boxCollider.size = new Vector3(1.1f, 0.4f, 2.4f);
		AlignLongAxisToForward(lux3DSpawn.root.transform);
		lux3DSpawn.onSwapped = delegate(Lux3DSpawn sp)
		{
			if (sp.root != null)
			{
				AlignLongAxisToForward(sp.root.transform);
			}
		};
		if (worldMech != null)
		{
			worldMech.RaiseDockWinch(byEcho: false, byWishBoat: true);
		}
		else
		{
			TideTownAchievementSystem.Unlock("wish_boat");
		}
		if (wishFeedbackText != null)
		{
			wishFeedbackText.text = "小船靠在渡口了。踩上去就能过海。" + WaitNote(lux3DSpawn);
		}
	}

	private static void AlignLongAxisToForward(Transform root)
	{
		Transform transform = root.Find("Model");
		if (!(transform == null))
		{
			Quaternion rotation = root.rotation;
			root.rotation = Quaternion.identity;
			Bounds bounds = Lux3DModelFit.WorldBounds(transform.gameObject);
			if (bounds.size.x > bounds.size.z * 1.1f)
			{
				transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * transform.localRotation;
			}
			root.rotation = rotation;
		}
	}

	private void SpawnWishFirewood(string description, Vector3 ovenPos)
	{
		Vector3 pos = ovenPos + new Vector3(0.85f, 0f, 0.65f);
		if (world != null)
		{
			pos.y = world.GroundAt(pos.x, pos.z, ovenPos.y + 1f);
		}
		Lux3DSpawn s = SpawnWish(description, "fire", pos, Quaternion.identity, base.transform, new Color32(byte.MaxValue, 175, 90, byte.MaxValue), 4.5f);
		MakeToy(s, description, "fire", "林阿婆的烤炉", autoPlace: true, 1.5f);
		BakeryEffects bakeryEffects = Object.FindObjectOfType<BakeryEffects>();
		if (state != null && director != null)
		{
			if (state.flags.Contains("oven_lit"))
			{
				if (bakeryEffects != null)
				{
					director.BoostOvenBellows(bakeryEffects);
				}
			}
			else if (!state.flags.Contains("wood_loaded"))
			{
				director.TakeWood();
				director.LoadWood();
			}
		}
		TideTownAchievementSystem.Unlock("word_fire");
		if (wishFeedbackText != null)
		{
			wishFeedbackText.text = "柴送到了炉边。屋里很快就暖和了。" + WaitNote(s);
		}
	}

	private void SpawnWishKeepsake(string description, WorldMechanismDirector worldMech)
	{
		float yaw = player.yaw;
		Vector3 vector = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
		Vector3 pos = player.transform.position + vector * 1.9f;
		if (world != null)
		{
			pos.y = world.GroundAt(pos.x, pos.z, player.transform.position.y + 1f);
		}
		Transform parent = base.transform;
		bool flag = false;
		if (worldMech != null)
		{
			float dist;
			Transform nearestWishPedestal = worldMech.GetNearestWishPedestal(player.transform.position, out dist);
			if (nearestWishPedestal != null && dist < 14f)
			{
				parent = nearestWishPedestal;
				pos = nearestWishPedestal.position + new Vector3(0f, 0.72f, 0f);
				flag = true;
			}
		}
		Lux3DSpawn s = SpawnWish(description, "keepsake", pos, Quaternion.LookRotation(-vector, Vector3.up), parent, new Color32(byte.MaxValue, 215, 130, byte.MaxValue), 4.2f);
		MakeToy(s, description, "keepsake", "潮汐镇", !flag, 3.5f);
		TideTownAchievementSystem.Unlock("wish_town");
		if (wishFeedbackText != null)
		{
			wishFeedbackText.text = "「" + description + "」留在了潮汐镇，会一直摆到结局。" + WaitNote(s);
		}
	}

	private void MakeToy(Lux3DSpawn s, string description, string category, string recipient, bool autoPlace, float maxShift)
	{
		if (s != null && !(s.root == null))
		{
			WishToy wishToy = s.root.AddComponent<WishToy>();
			wishToy.Init(s, description, category, recipient, forgedCounter, world, player, autoPlace, maxShift);
			LastWishToy = wishToy;
		}
	}

	private static void AttachWarmGlow(Transform parent, Color32 color, float range)
	{
		GameObject obj = new GameObject("WishWarmGlow");
		obj.transform.SetParent(parent, worldPositionStays: false);
		obj.transform.localPosition = new Vector3(0f, 0.85f, 0f);
		Light light = obj.AddComponent<Light>();
		light.type = LightType.Point;
		light.color = color;
		light.range = range;
		light.intensity = 2f;
	}

	private void Update()
	{
		if (ui == null || !ui.inputClose)
		{
			return;
		}
		bool flag = promptInput != null && promptInput.isFocused;
		if (consoleOpen && EasyInputBridge.Down(KeyCode.Escape))
		{
			SetConsoleOpen(open: false);
		}
		else if (!flag && EasyInputBridge.Down(KeyCode.V) && (consoleOpen || !OtherOverlayOpen()))
		{
			SetConsoleOpen(!consoleOpen);
		}
		float deltaTime = Time.deltaTime;
		for (int num = forgedRuntimeObjects.Count - 1; num >= 0; num--)
		{
			Transform transform = forgedRuntimeObjects[num];
			if (transform == null)
			{
				forgedRuntimeObjects.RemoveAt(num);
			}
			else
			{
				Transform transform2 = TideTownStyleUI.FindDeepChild(transform, "Ring_0");
				Transform transform3 = TideTownStyleUI.FindDeepChild(transform, "Ring_1");
				if (transform2 != null)
				{
					transform2.Rotate(0f, 0f, 32f * deltaTime, Space.Self);
				}
				if (transform3 != null)
				{
					transform3.Rotate(0f, 0f, -42f * deltaTime, Space.Self);
				}
			}
		}
		if (!consoleOpen || hardwareTelemetryText == null)
		{
			return;
		}
		HardwareAndSensorHub hardwareAndSensorHub = HardwareAndSensorHub.Instance;
		if (hardwareAndSensorHub != null)
		{
			int num2 = (hardwareAndSensorHub.Gamepad1Connected ? 1 : 0) + (hardwareAndSensorHub.MicActive ? 2 : 0) + (hardwareAndSensorHub.CamOpen ? 4 : 0);
			if (num2 != lastDeviceState)
			{
				lastDeviceState = num2;
				string text = (hardwareAndSensorHub.Gamepad1Connected ? "键鼠与手柄都可操作" : "键鼠操作");
				string text2 = (hardwareAndSensorHub.CamOpen ? "塔顶可对着镜头挥手" : "塔顶按空格挥手");
				hardwareTelemetryText.text = text + "\u3000·\u3000" + text2;
			}
		}
	}
}
