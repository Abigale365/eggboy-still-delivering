using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class WordMaterializerSystem : MonoBehaviour
{
	private static readonly string[] LetterIds = new string[4] { "lin_oven", "ying_dog", "chen_lighthouse", "zhou_eggboy" };

	private static readonly string[] LetterTabs = new string[4] { "林阿婆的信", "阿萤的信", "老陈的信", "老周的信" };

	private const string HighlightHex = "#C2621D";

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private StoryDirector director;

	private GameState state;

	private WordSlipDef[] baseSlips = new WordSlipDef[0];

	private WordComboDef[] combos = new WordComboDef[0];

	private readonly List<WordSlipDef> sessionCombos = new List<WordSlipDef>();

	private RectTransform hudCanvasRoot;

	private RectTransform letterOverlay;

	private RectTransform letterPaper;

	private CanvasGroup paperCanvasGroup;

	private Image overlayBackdropImage;

	private Button letterCloseButton;

	private GameObject slipTrayRoot;

	private readonly List<GameObject> trayObjects = new List<GameObject>();

	private readonly Dictionary<string, RectTransform> slipCards = new Dictionary<string, RectTransform>();

	private readonly List<GameObject> tabButtons = new List<GameObject>();

	private int currentLetter;

	private WordSlipDef draggingSlip;

	private RectTransform draggedSlipUI;

	private CanvasGroup draggedSource;

	private Vector2 dragStartScreenPos;

	private Vector2 lastDragPointer;

	private int restoreLetterCloseAt;

	private bool aimedInAir;

	private GameObject aimPreviewRoot;

	private readonly Transform[] arcDots = new Transform[8];

	private LineRenderer landing;

	private Material arcMaterial;

	private Material landingMaterial;

	private float landingTime;

	private const float LandingSeconds = 0.36f;

	private readonly Dictionary<string, Lux3DSpawn> materialized = new Dictionary<string, Lux3DSpawn>();

	private bool lastLetterOpen;

	
	private sealed class ThrownWoodInteractable : IInteractable, IObjectiveInteractable
	{
		private GameObject root;
		private readonly StoryDirector director;
		private readonly bool isWet;
		private readonly string entityId;
		public Vector3 Position => (root != null) ? root.transform.position : new Vector3(9999f, 9999f, 9999f);
		public string EntityId => entityId;
		public ThrownWoodInteractable(GameObject r, StoryDirector d, bool wet, string id)
		{
			root = r; director = d; isWet = wet; entityId = "word_wood_" + id;
		}
		public bool IsObjective(GameState s) => CanInteract(s);
		public bool CanInteract(GameState s)
		{
			return root != null && s != null && !s.flags.Contains("oven_lit") && s.carrying != "wood";
		}
		public string Prompt(GameState s)
		{
			if (!CanInteract(s)) return "";
			return isWet ? "E\u3000抱起湿木柴" : "E\u3000抱起干木柴";
		}
		public void Interact(GameState s, InteractionService svc)
		{
			if (root == null || director == null || s == null) return;
			Vector3 pos = root.transform.position;
			UnityEngine.Object.Destroy(root);
			root = null;
			s.flags.Remove("wood_loaded");
			if (isWet)
			{
				if (!s.flags.Contains("carrying_wet_wood")) s.flags.Add("carrying_wet_wood");
			}
			else
			{
				s.flags.Remove("carrying_wet_wood");
				s.flags.Remove("wood_wet");
				TideTownAchievementSystem.Unlock("word_fire");
			}
			director.TakeWoodAt(pos);
		}
	}

	public Lux3DSpawn LastSpawn { get; private set; }

	public string LastAction { get; private set; } = "";


	public bool LastActionApplied { get; private set; }

	public bool IsPanelOpen
	{
		get
		{
			if (ui != null)
			{
				return ui.LetterOpen;
			}
			return false;
		}
	}

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
	{
		player = p;
		ui = u;
		world = w;
		director = d;
		state = s;
		TextAsset textAsset = Resources.Load<TextAsset>("Story/word_slips");
		if (!(textAsset != null))
		{
			return;
		}
		WordSlipFile wordSlipFile = JsonUtility.FromJson<WordSlipFile>(textAsset.text);
		if (wordSlipFile != null)
		{
			if (wordSlipFile.slips != null)
			{
				baseSlips = wordSlipFile.slips;
			}
			if (wordSlipFile.combos != null)
			{
				combos = wordSlipFile.combos;
			}
		}
	}

	private bool HasLetter(string letterId)
	{
		if (state == null || !state.flags.Contains("letters_taken"))
		{
			return false;
		}
		if (letterId == "zhou_eggboy")
		{
			return state.flags.Contains("dawn_shown");
		}
		return true;
	}

	public List<WordSlipDef> PocketSlips()
	{
		List<WordSlipDef> list = new List<WordSlipDef>();
		WordSlipDef[] array = baseSlips;
		foreach (WordSlipDef wordSlipDef in array)
		{
			if (HasLetter(wordSlipDef.letter))
			{
				list.Add(wordSlipDef);
			}
		}
		WordComboDef[] array2 = combos;
		foreach (WordComboDef wordComboDef in array2)
		{
			if (state != null && state.flags.Contains("combo_" + wordComboDef.id))
			{
				list.Add(ComboSlip(wordComboDef));
			}
		}
		list.AddRange(sessionCombos);
		return list;
	}

	private static WordSlipDef ComboSlip(WordComboDef c)
	{
		return new WordSlipDef
		{
			id = c.id,
			word = c.word,
			kind = "item",
			entry = c.entry,
			action = c.action,
			prompt = c.prompt,
			combo = true
		};
	}

	private WordSlipDef FindSlip(string id)
	{
		foreach (WordSlipDef item in PocketSlips())
		{
			if (item.id == id)
			{
				return item;
			}
		}
		return null;
	}

	public WordSlipDef Combine(string aId, string bId)
	{
		WordSlipDef wordSlipDef = FindSlip(aId);
		WordSlipDef wordSlipDef2 = FindSlip(bId);
		if (wordSlipDef == null || wordSlipDef2 == null || aId == bId || wordSlipDef.combo || wordSlipDef2.combo)
		{
			return null;
		}
		WordComboDef[] array = combos;
		foreach (WordComboDef wordComboDef in array)
		{
			if ((wordComboDef.a == aId && wordComboDef.b == bId) || (wordComboDef.a == bId && wordComboDef.b == aId))
			{
				if (state != null && !state.flags.Contains("combo_" + wordComboDef.id))
				{
					state.flags.Add("combo_" + wordComboDef.id);
				}
				TideTownAchievementSystem.Unlock("word_combo");
				return ComboSlip(wordComboDef);
			}
		}
		string text = "free_" + aId + "_" + bId;
		string text2 = "free_" + bId + "_" + aId;
		foreach (WordSlipDef sessionCombo in sessionCombos)
		{
			if (sessionCombo.id == text || sessionCombo.id == text2)
			{
				return sessionCombo;
			}
		}
		WordSlipDef wordSlipDef3 = new WordSlipDef
		{
			id = text,
			word = wordSlipDef.word + wordSlipDef2.word,
			kind = "item",
			entry = "",
			action = "keepsake",
			prompt = wordSlipDef.word + wordSlipDef2.word,
			combo = true
		};
		sessionCombos.Add(wordSlipDef3);
		return wordSlipDef3;
	}

	public void OpenLetterWithSlips()
	{
		if (!(ui == null))
		{
			if (ui.LetterOpen)
			{
				ui.CloseLetter();
			}
			else if (state != null && state.flags.Contains("letters_taken"))
			{
				string text = state.step ?? "";
				int index = (text.StartsWith("S") ? 1 : (text.StartsWith("L") ? 2 : ((text.StartsWith("E") && HasLetter("zhou_eggboy")) ? 3 : 0)));
				OpenSpecificLetter(index);
			}
		}
	}

	public void OpenSpecificLetter(int index)
	{
		if (ui == null || director == null)
		{
			return;
		}
		index = Mathf.Clamp(index, 0, LetterIds.Length - 1);
		if (HasLetter(LetterIds[index]))
		{
			LetterData letter = director.GetLetter(LetterIds[index]);
			if (letter != null)
			{
				currentLetter = index;
				ui.ShowLetterDetail(letter.title, HighlightBody(letter.body, LetterIds[index]), letter.signature, LetterIds[index]);
				RebuildTray();
			}
		}
	}

	private string HighlightBody(string body, string letterId)
	{
		if (ui.LetterBodyText != null)
		{
			ui.LetterBodyText.supportRichText = true;
		}
		HintDirector instance = HintDirector.Instance;
		if (instance != null && instance.LetterHintId == letterId && !string.IsNullOrEmpty(instance.LetterHintPhrase))
		{
			int num = body.IndexOf(instance.LetterHintPhrase, StringComparison.Ordinal);
			if (num >= 0)
			{
				body = body.Substring(0, num) + "<color=#2F7D6B>" + instance.LetterHintPhrase + "</color>" + body.Substring(num + instance.LetterHintPhrase.Length);
			}
		}
		WordSlipDef[] array = baseSlips;
		foreach (WordSlipDef wordSlipDef in array)
		{
			if (wordSlipDef.letter != letterId || string.IsNullOrEmpty(wordSlipDef.word))
			{
				continue;
			}
			int num2 = -1;
			if (!string.IsNullOrEmpty(wordSlipDef.near))
			{
				int num3 = body.IndexOf(wordSlipDef.near, StringComparison.Ordinal);
				if (num3 >= 0)
				{
					int num4 = wordSlipDef.near.IndexOf(wordSlipDef.word, StringComparison.Ordinal);
					if (num4 >= 0)
					{
						num2 = num3 + num4;
					}
				}
			}
			if (num2 < 0)
			{
				num2 = body.IndexOf(wordSlipDef.word, StringComparison.Ordinal);
			}
			if (num2 >= 0)
			{
				body = body.Substring(0, num2) + "<color=#C2621D><b>" + wordSlipDef.word + "</b></color>" + body.Substring(num2 + wordSlipDef.word.Length);
			}
		}
		return body;
	}

	private void EnsureBoundToLetterUI()
	{
		if (letterPaper != null)
		{
			return;
		}
		hudCanvasRoot = TideTownStyleUI.FindHudCanvasRoot(ui);
		letterOverlay = TideTownStyleUI.FindLetterOverlay(ui);
		letterPaper = TideTownStyleUI.FindLetterPaper(ui);
		if (!(letterPaper == null))
		{
			Image component = letterPaper.GetComponent<Image>();
			if (component != null)
			{
				component.raycastTarget = true;
			}
			paperCanvasGroup = letterPaper.GetComponent<CanvasGroup>();
			if (paperCanvasGroup == null)
			{
				paperCanvasGroup = letterPaper.gameObject.AddComponent<CanvasGroup>();
			}
			if (letterOverlay != null)
			{
				overlayBackdropImage = letterOverlay.GetComponent<Image>();
				letterCloseButton = letterOverlay.GetComponent<Button>();
			}
			slipTrayRoot = new GameObject("WordSlipTray", typeof(RectTransform));
			slipTrayRoot.transform.SetParent(letterPaper, worldPositionStays: false);
			RectTransform component2 = slipTrayRoot.GetComponent<RectTransform>();
			Vector2 vector2 = (component2.pivot = new Vector2(0.5f, 0.5f));
			Vector2 anchorMin = (component2.anchorMax = vector2);
			component2.anchorMin = anchorMin;
			component2.anchoredPosition = Vector2.zero;
			component2.sizeDelta = new Vector2(560f, 620f);
		}
	}

	private void RebuildTray()
	{
		EnsureBoundToLetterUI();
		if (slipTrayRoot == null)
		{
			return;
		}
		foreach (GameObject trayObject in trayObjects)
		{
			if (trayObject != null)
			{
				UnityEngine.Object.Destroy(trayObject);
			}
		}
		trayObjects.Clear();
		slipCards.Clear();
		foreach (GameObject tabButton in tabButtons)
		{
			if (tabButton != null)
			{
				UnityEngine.Object.Destroy(tabButton);
			}
		}
		tabButtons.Clear();
		int num = 0;
		for (int i = 0; i < LetterIds.Length; i++)
		{
			if (HasLetter(LetterIds[i]))
			{
				int idx = i;
				Button button = TideTownStyleUI.CreateWarmButton(slipTrayRoot.transform, LetterTabs[i], new Vector2(-186f + (float)num * 124f, 324f), new Vector2(116f, 28f), i == currentLetter, delegate
				{
					OpenSpecificLetter(idx);
				});
				tabButtons.Add(button.gameObject);
				num++;
			}
		}
		List<WordSlipDef> list = PocketSlips();
		Transform transform = ((letterPaper != null) ? letterPaper.Find("LetterHintBar") : null);
		if (transform != null)
		{
			transform.gameObject.SetActive(value: true);
		}
		if (list.Count == 0)
		{
			return;
		}
		int num2 = 1;
		float num3 = -250f;
		foreach (WordSlipDef item in list)
		{
			float num4 = ((item.kind == "char") ? 48f : (30f + (float)item.word.Length * 20f));
			if (num3 + num4 > 250f)
			{
				num3 = -250f;
				num2++;
			}
			num3 += num4 + 8f;
		}
		if (num2 >= 2 && transform != null)
		{
			transform.gameObject.SetActive(value: false);
		}
		float num5 = ((num2 >= 2) ? 22f : 0f);
		Text text = TideTownStyleUI.CreateText(slipTrayRoot.transform, "· 信上橘色的字可以撕下来 ·", 12, TideTownStyleUI.InkMuted, TextAnchor.MiddleCenter, new Vector2(0f, -214f + num5), new Vector2(500f, 18f));
		trayObjects.Add(text.gameObject);
		float num6 = -250f;
		float num7 = -240f + num5;
		foreach (WordSlipDef item2 in list)
		{
			float num8 = ((item2.kind == "char") ? 48f : (30f + (float)item2.word.Length * 20f));
			if (num6 + num8 > 250f)
			{
				num6 = -250f;
				num7 -= 40f;
			}
			Vector2 anchoredPosition = new Vector2(num6 + num8 * 0.5f, num7);
			num6 += num8 + 8f;
			Image image = TideTownStyleUI.CreatePanel(slipTrayRoot.transform, "WordSlip_" + item2.id, num8, 34f, item2.combo ? TideTownStyleUI.AmberSoft : TideTownStyleUI.SlipColor);
			image.sprite = TideTownStyleUI.PaperSprite;
			RectTransform rectTransform = image.rectTransform;
			Vector2 vector2 = (rectTransform.pivot = new Vector2(0.5f, 0.5f));
			Vector2 anchorMin = (rectTransform.anchorMax = vector2);
			rectTransform.anchorMin = anchorMin;
			rectTransform.anchoredPosition = anchoredPosition;
			Shadow shadow = image.gameObject.AddComponent<Shadow>();
			shadow.effectColor = new Color32(59, 42, 26, 70);
			shadow.effectDistance = new Vector2(2f, -3f);
			TideTownStyleUI.CreateText(image.transform, item2.word, 16, (item2.kind == "char") ? new Color32(194, 98, 29, byte.MaxValue) : TideTownStyleUI.InkDark, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(num8 - 6f, 28f), bold: true);
			image.gameObject.AddComponent<CanvasGroup>();
			trayObjects.Add(image.gameObject);
			slipCards[item2.id] = rectTransform;
		}
	}

	private void Update()
	{
		if (restoreLetterCloseAt > 0 && Time.frameCount >= restoreLetterCloseAt)
		{
			restoreLetterCloseAt = 0;
			if (letterCloseButton != null)
			{
				letterCloseButton.interactable = ui != null && ui.inputClose;
			}
		}
		if (draggingSlip == null && landingTime > 0f)
		{
			landingTime = Mathf.Max(0f, landingTime - Time.unscaledDeltaTime);
			float num = 1f - landingTime / 0.36f;
			UpdateLandingMarker(landing.transform.position - Vector3.up * 0.012f, Mathf.Lerp(0.14f, 0.29f, num), 0.8f * (1f - num));
			if (landingTime <= 0f)
			{
				landing.enabled = false;
			}
		}
		if (ui == null || !ui.inputClose)
		{
			return;
		}
		if (ui.LetterOpen && !lastLetterOpen)
		{
			RestorePaperOpacity();
			RebuildTray();
		}
		else if (!ui.LetterOpen && lastLetterOpen)
		{
			CancelActiveDrag();
			RestorePaperOpacity();
		}
		lastLetterOpen = ui.LetterOpen;
		if (!ui.LetterOpen)
		{
			return;
		}
		Vector2 vector = Input.mousePosition;
		if (draggingSlip == null && Input.GetMouseButtonDown(0))
		{
			string text = SlipUnder(vector, null);
			if (text != null)
			{
				BeginSlipDrag(FindSlip(text), slipCards[text], vector);
			}
		}
		if (draggingSlip != null)
		{
			if (Input.GetMouseButtonUp(0))
			{
				EndSlipDrag(vector);
			}
			else if (!Input.GetMouseButton(0))
			{
				CancelActiveDrag();
				RestorePaperOpacity();
			}
			else
			{
				UpdateDraggedSlipScreenPos(vector, vector - lastDragPointer);
				lastDragPointer = vector;
				UpdateDraggingInAir();
			}
		}
	}

	private void BeginSlipDrag(WordSlipDef def, RectTransform originalCard, Vector2 pointer)
	{
		if (def != null && !(hudCanvasRoot == null))
		{
			draggingSlip = def;
			draggedSource = ((originalCard != null) ? originalCard.GetComponent<CanvasGroup>() : null);
			dragStartScreenPos = (lastDragPointer = pointer);
			aimedInAir = false;
			if (draggedSource != null)
			{
				draggedSource.alpha = 0f;
			}
			if (letterCloseButton != null)
			{
				letterCloseButton.interactable = false;
			}
			float num = ((def.kind == "char") ? 56f : (36f + (float)def.word.Length * 22f));
			Image image = TideTownStyleUI.CreatePanel(hudCanvasRoot, "DraggedWordSlip", num, 40f, TideTownStyleUI.PaperColor);
			image.sprite = TideTownStyleUI.RoundedBorderSprite;
			image.raycastTarget = false;
			Shadow shadow = image.gameObject.AddComponent<Shadow>();
			shadow.effectColor = new Color32(0, 0, 0, 120);
			shadow.effectDistance = new Vector2(5f, -8f);
			TideTownStyleUI.CreateText(image.transform, def.word, 18, TideTownStyleUI.InkDark, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(num - 8f, 34f), bold: true);
			draggedSlipUI = image.rectTransform;
			UpdateDraggedSlipScreenPos(pointer, Vector2.zero);
			ShadowAndChimeSystem.PlayProceduralChime(587.33f, 0.18f);
		}
	}

	private void EndSlipDrag(Vector2 pointer)
	{
		if (draggingSlip == null)
		{
			return;
		}
		WordSlipDef wordSlipDef = draggingSlip;
		string text = SlipUnder(pointer, wordSlipDef.id);
		if (text != null)
		{
			CleanupDragVisuals();
			RestorePaperOpacity();
			WordSlipDef wordSlipDef2 = Combine(wordSlipDef.id, text);
			RebuildTray();
			if (wordSlipDef2 != null && slipCards.TryGetValue(wordSlipDef2.id, out var value))
			{
				StartCoroutine(PopCard(value));
			}
			ShadowAndChimeSystem.PlayProceduralChime((wordSlipDef2 != null) ? 880f : 220f, 0.3f);
			return;
		}
		int num;
		Vector3 vector;
		if (letterPaper == null || !RectTransformUtility.RectangleContainsScreenPoint(letterPaper, pointer, null))
		{
			if (!aimedInAir)
			{
				num = ((Vector2.Distance(pointer, dragStartScreenPos) > 48f) ? 1 : 0);
				if (num == 0)
				{
					goto IL_00c9;
				}
			}
			else
			{
				num = 1;
			}
			vector = ComputeAimWorldPoint(pointer);
			goto IL_00d7;
		}
		num = 0;
		goto IL_00c9;
		IL_00d7:
		Vector3 landPos = vector;
		CleanupDragVisuals();
		RestorePaperOpacity();
		if (num != 0 && player != null)
		{
			if (ui != null && ui.LetterOpen)
			{
				ui.CloseLetter();
			}
			StartCoroutine(ThrowRoutine(wordSlipDef, landPos));
		}
		return;
		IL_00c9:
		vector = Vector3.zero;
		goto IL_00d7;
	}

	private string SlipUnder(Vector2 screenPos, string exceptId)
	{
		foreach (KeyValuePair<string, RectTransform> slipCard in slipCards)
		{
			if (!(slipCard.Key == exceptId) && !(slipCard.Value == null) && slipCard.Value.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(slipCard.Value, screenPos, null))
			{
				return slipCard.Key;
			}
		}
		return null;
	}

	private IEnumerator PopCard(RectTransform card)
	{
		float t = 0f;
		while (t < 0.35f && card != null)
		{
			t += Time.unscaledDeltaTime;
			float num = Mathf.Clamp01(t / 0.35f);
			card.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(num * MathF.PI));
			yield return null;
		}
		if (card != null)
		{
			card.localScale = Vector3.one;
		}
	}

	private void UpdateDraggedSlipScreenPos(Vector2 screenPos, Vector2 delta)
	{
		if (!(draggedSlipUI == null) && !(hudCanvasRoot == null))
		{
			RectTransformUtility.ScreenPointToLocalPointInRectangle(hudCanvasRoot, screenPos, null, out var localPoint);
			draggedSlipUI.anchoredPosition = localPoint;
			float num = Mathf.Clamp((0f - delta.x) * 0.65f, -18f, 18f);
			draggedSlipUI.localRotation = Quaternion.Euler(0f, 0f, -3f + num);
			draggedSlipUI.localScale = Vector3.one * 1.08f;
		}
	}

	private void UpdateDraggingInAir()
	{
		Vector2 vector = Input.mousePosition;
		bool flag = (aimedInAir = letterPaper == null || !RectTransformUtility.RectangleContainsScreenPoint(letterPaper, vector, null));
		if (paperCanvasGroup != null)
		{
			paperCanvasGroup.alpha = Mathf.Lerp(paperCanvasGroup.alpha, flag ? 0.12f : 1f, Time.unscaledDeltaTime * 12f);
		}
		if (overlayBackdropImage != null && flag)
		{
			overlayBackdropImage.color = new Color32(0, 0, 0, 10);
		}
		UpdateArcPreview(ComputeAimWorldPoint(vector), flag && draggingSlip.kind != "char");
	}

	private Vector3 ComputeAimWorldPoint(Vector2 screenPos)
	{
		if (player == null)
		{
			return Vector3.zero;
		}
		Vector3 position = player.transform.position;
		Camera main = Camera.main;
		if (main != null)
		{
			Ray ray = main.ScreenPointToRay(screenPos);
			if (new Plane(Vector3.up, new Vector3(0f, position.y, 0f)).Raycast(ray, out var enter) && enter > 0.5f && enter < 35f)
			{
				Vector3 point = ray.GetPoint(enter);
				Vector3 vector = new Vector3(point.x - position.x, 0f, point.z - position.z);
				float magnitude = vector.magnitude;
				if (magnitude < 1.4f)
				{
					vector = vector.normalized * 1.4f;
				}
				else if (magnitude > 8.5f)
				{
					vector = vector.normalized * 8.5f;
				}
				Vector3 result = position + vector;
				if (world != null)
				{
					float a = world.GroundAt(result.x, result.z, position.y + 2.5f);
					result.y = Mathf.Max(a, world.WaterY);
				}
				return result;
			}
		}
		float yaw = player.yaw;
		Vector3 result2 = position + new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 2.6f;
		if (world != null)
		{
			result2.y = world.GroundAt(result2.x, result2.z, position.y + 1.5f);
		}
		return result2;
	}

	private void UpdateArcPreview(Vector3 targetPos, bool visible)
	{
		if (player == null || !visible)
		{
			if (aimPreviewRoot != null && landingTime <= 0f)
			{
				aimPreviewRoot.SetActive(value: false);
			}
			return;
		}
		EnsureArcPreview();
		landingTime = 0f;
		aimPreviewRoot.SetActive(value: true);
		Vector3 start = player.transform.position + Vector3.up * 0.72f;
		for (int i = 0; i < arcDots.Length; i++)
		{
			float t = ((float)i + 1f) / ((float)arcDots.Length + 1f);
			arcDots[i].gameObject.SetActive(value: true);
			arcDots[i].position = ThrowArc(start, targetPos + Vector3.up * 0.05f, t, 2.1f);
		}
		UpdateLandingMarker(targetPos, 0.14f, 0.76f);
	}

	private void EnsureArcPreview()
	{
		if (aimPreviewRoot == null)
		{
			aimPreviewRoot = new GameObject("WordThrowAimPreview");
			aimPreviewRoot.transform.SetParent(base.transform, worldPositionStays: false);
			Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
			arcMaterial = new Material(shader)
			{
				color = TideTownStyleUI.SlipColor
			};
			if (arcMaterial.HasProperty("_EmissionColor"))
			{
				arcMaterial.EnableKeyword("_EMISSION");
				arcMaterial.SetColor("_EmissionColor", new Color(0.65f, 0.42f, 0.14f));
			}
			for (int i = 0; i < arcDots.Length; i++)
			{
				GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
				gameObject.transform.SetParent(aimPreviewRoot.transform, worldPositionStays: false);
				gameObject.transform.localScale = Vector3.one * Mathf.Lerp(0.043f, 0.024f, (float)i / ((float)arcDots.Length - 1f));
				Renderer component = gameObject.GetComponent<Renderer>();
				component.sharedMaterial = arcMaterial;
				component.shadowCastingMode = ShadowCastingMode.Off;
				arcDots[i] = gameObject.transform;
			}
			GameObject gameObject2 = new GameObject("WordLandingRing");
			gameObject2.transform.SetParent(aimPreviewRoot.transform, worldPositionStays: false);
			landing = gameObject2.AddComponent<LineRenderer>();
			landingMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
			landing.sharedMaterial = landingMaterial;
			landing.useWorldSpace = false;
			landing.loop = true;
			landing.positionCount = 24;
			landing.widthMultiplier = 0.009f;
			landing.shadowCastingMode = ShadowCastingMode.Off;
			for (int j = 0; j < landing.positionCount; j++)
			{
				float f = (float)j * MathF.PI * 2f / (float)landing.positionCount;
				landing.SetPosition(j, new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)));
			}
		}
	}

	private void UpdateLandingMarker(Vector3 position, float radius, float opacity)
	{
		landing.transform.position = position + Vector3.up * 0.012f;
		landing.transform.localScale = Vector3.one * radius;
		LineRenderer lineRenderer = landing;
		Color startColor = (landing.endColor = new Color(1f, 0.8f, 0.46f, opacity));
		lineRenderer.startColor = startColor;
		landing.enabled = true;
	}

	private static Vector3 ThrowArc(Vector3 start, Vector3 end, float t, float height)
	{
		return Vector3.Lerp(start, end, t) + Vector3.up * (4f * height * t * (1f - t));
	}

	private void ShowLanding(Vector3 position)
	{
		if (draggingSlip == null)
		{
			EnsureArcPreview();
			Transform[] array = arcDots;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].gameObject.SetActive(value: false);
			}
			aimPreviewRoot.SetActive(value: true);
			UpdateLandingMarker(position, 0.14f, 0.8f);
			landingTime = 0.36f;
		}
	}

	private void OnDestroy()
	{
		if (arcMaterial != null)
		{
			UnityEngine.Object.Destroy(arcMaterial);
		}
		if (landingMaterial != null)
		{
			UnityEngine.Object.Destroy(landingMaterial);
		}
	}

	public Coroutine ThrowSlip(string slipId, Vector3 landPos)
	{
		WordSlipDef wordSlipDef = FindSlip(slipId);
		if (wordSlipDef == null)
		{
			return null;
		}
		return StartCoroutine(ThrowRoutine(wordSlipDef, landPos));
	}

	private IEnumerator ThrowRoutine(WordSlipDef def, Vector3 landPos)
	{
		if (player == null)
		{
			yield break;
		}
		LastAction = def.action ?? "";
		LastActionApplied = false;
		LastSpawn = null;
		Vector3 startPos = player.transform.position + Vector3.up * 0.72f;
		float lookYaw = Mathf.Atan2(landPos.x - startPos.x, landPos.z - startPos.z);
		player.yaw = lookYaw;
		player.transform.rotation = Quaternion.Euler(0f, lookYaw * 57.29578f, 0f);
		player.PlayEmote("give");
		bool loose = def.kind == "char";
		Vector3 endPos = (loose ? startPos : (landPos + Vector3.up * 0.05f));
		GameObject flyingSlip = GameObject.CreatePrimitive(PrimitiveType.Cube);
		UnityEngine.Object.Destroy(flyingSlip.GetComponent<Collider>());
		flyingSlip.name = "FlyingParchmentSlip";
		flyingSlip.transform.localScale = new Vector3(0.28f, 0.008f, 0.14f);
		Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
		Material paperMat = new Material(shader)
		{
			color = TideTownStyleUI.PaperColor
		};
		if (paperMat.HasProperty("_EmissionColor"))
		{
			paperMat.EnableKeyword("_EMISSION");
			paperMat.SetColor("_EmissionColor", new Color(1f, 0.86f, 0.55f) * 1.2f);
		}
		flyingSlip.GetComponent<Renderer>().sharedMaterial = paperMat;
		ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.28f);
		float duration = (loose ? 0.9f : 0.52f);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float num = Mathf.Clamp01(elapsed / duration);
			Vector3 position = ThrowArc(startPos, endPos, num, loose ? 1.1f : 2.1f);
			if (loose)
			{
				position += new Vector3(Mathf.Sin(lookYaw), 0f, Mathf.Cos(lookYaw)) * (1.2f * Mathf.Sin(num * MathF.PI));
			}
			flyingSlip.transform.position = position;
			flyingSlip.transform.rotation = Quaternion.Euler(num * 520f, lookYaw * 57.29578f + num * 180f, Mathf.Sin(num * 14f) * 28f);
			yield return null;
		}
		UnityEngine.Object.Destroy(flyingSlip);
		UnityEngine.Object.Destroy(paperMat);
		if (loose)
		{
			ShadowAndChimeSystem.PlayProceduralChime(294f, 0.25f);
			yield break;
		}
		ShowLanding(landPos);
		Materialize(def, landPos, lookYaw * 57.29578f);
		ShadowAndChimeSystem.PlayProceduralChime(880f);
		if (LastSpawn != null && LastSpawn.root != null)
		{
			yield return Grow(LastSpawn.root.transform);
		}
	}

	private IEnumerator Grow(Transform root)
	{
		float t = 0f;
		while (t < 0.38f && root != null)
		{
			t += Time.deltaTime;
			float num = Mathf.Clamp01(t / 0.38f);
			root.localScale = Vector3.one * (1f - Mathf.Pow(1f - num, 3f) + Mathf.Sin(num * MathF.PI) * 0.14f);
			yield return null;
		}
		if (root != null)
		{
			root.localScale = Vector3.one;
		}
	}

	private Vector3 StovePos()
	{
		BakeryItem[] array = BakeryData.Load();
		foreach (BakeryItem bakeryItem in array)
		{
			if (bakeryItem.id == "stove")
			{
				return new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
			}
		}
		return new Vector3(-9.73f, 1.3f, -5.85f);
	}

	private void Materialize(WordSlipDef def, Vector3 landPos, float yawDeg)
	{
		if (state == null || director == null)
		{
			return;
		}
		Lux3DService lux3DService = Lux3DService.Ensure();
		WorldMechanismDirector worldMechanismDirector = UnityEngine.Object.FindObjectOfType<WorldMechanismDirector>();
		string text = def.action ?? "";
		switch (text)
		{
		case "fire":
			if (state.step.StartsWith("B") && !state.flags.Contains("matches_taken"))
			{
				director.TakeMatches();
				LastActionApplied = true;
			}
			SpawnEmber(landPos);
			return;
		case "ball":
			if ((state.step == "S1_slipper" || state.step == "S2_ball") && !state.flags.Contains("ball_taken") && !state.flags.Contains("ball_in_bowl"))
			{
				director.TakeBall();
				LastActionApplied = true;
			}
			return;
		case "oil":
			if (state.step == "L1_shed" && !state.flags.Contains("oil_taken"))
			{
				director.TakeOil();
				LastActionApplied = true;
			}
			return;
		}
		if (materialized.TryGetValue(def.id, out var value) && value != null && value.root != null)
		{
			UnityEngine.Object.Destroy(value.root);
		}
		Lux3DLibraryEntry entry = ((!string.IsNullOrEmpty(def.entry)) ? lux3DService.Find(def.entry) : lux3DService.Match(def.word, "keepsake"));
		string prompt = (string.IsNullOrEmpty(def.prompt) ? def.word : def.prompt);
		Vector3 position = landPos;
		Quaternion rotation = Quaternion.Euler(0f, yawDeg, 0f);
		bool flag = false;
		if (text == "boat" && worldMechanismDirector != null)
		{
			Vector3 crossingChannelCenter = worldMechanismDirector.GetCrossingChannelCenter();
			if (WaterLevel.StaticY >= WaterLevel.LevelYs[2] - 0.05f && worldMechanismDirector.GapActive && !worldMechanismDirector.CrossingSolved && Vector3.Distance(landPos, crossingChannelCenter) < 6f)
			{
				flag = true;
				position = new Vector3(crossingChannelCenter.x, WaterLevel.StaticY - 0.05f, crossingChannelCenter.z);
				rotation = Quaternion.LookRotation(worldMechanismDirector.CrossingDirection, Vector3.up);
			}
		}
		Lux3DSpawn lux3DSpawn = lux3DService.Spawn(prompt, entry, position, rotation, base.transform, generate: true, flag ? 2.2f : ((text == "boat") ? 1.2f : 0f));
		lux3DSpawn.root.name = "Word_" + def.id;
		materialized[def.id] = lux3DSpawn;
		LastSpawn = lux3DSpawn;
		if (flag)
		{
			BoxCollider boxCollider = lux3DSpawn.root.GetComponent<BoxCollider>();
			if (boxCollider == null)
			{
				boxCollider = lux3DSpawn.root.AddComponent<BoxCollider>();
			}
			int num = LayerMask.NameToLayer("Ground");
			lux3DSpawn.root.layer = ((num >= 0) ? num : 6);
			boxCollider.center = new Vector3(0f, 0.1f, 0f);
			boxCollider.size = new Vector3(1.1f, 0.4f, 2.4f);
			lux3DSpawn.root.AddComponent<WishBob>();
			worldMechanismDirector.BridgeGapWithBoat("wood_boat");
			LastActionApplied = true;
			return;
		}
		switch (text)
		{
		case "boat":
		{
			BoxCollider component = lux3DSpawn.root.GetComponent<BoxCollider>();
			if (component != null)
			{
				UnityEngine.Object.Destroy(component);
			}
			break;
		}
		case "wet_wood":
			ApplyWetLook(lux3DSpawn.root);
			lux3DSpawn.onSwapped = delegate(Lux3DSpawn s)
			{
				if (s.root != null)
				{
					ApplyWetLook(s.root);
				}
			};
			UnityEngine.Object.FindObjectOfType<InteractionService>()?.Register(new ThrownWoodInteractable(lux3DSpawn.root, director, true, def.id));
			float autoDist = (director.IsAuditMode || UnityEngine.Object.FindObjectOfType<TrailerCaptureRunner>() != null) ? 6.5f : 1.9f;
			if (Vector3.Distance(landPos, StovePos()) < autoDist)
			{
				LastActionApplied = director.LoadWetWood();
			}
			break;
		case "dry_wood":
			UnityEngine.Object.FindObjectOfType<InteractionService>()?.Register(new ThrownWoodInteractable(lux3DSpawn.root, director, false, def.id));
			float autoDryDist = (director.IsAuditMode || UnityEngine.Object.FindObjectOfType<TrailerCaptureRunner>() != null) ? 6.5f : 1.9f;
			if (Vector3.Distance(landPos, StovePos()) < autoDryDist && director.LoadDryWoodFromWord())
			{
				LastActionApplied = true;
				TideTownAchievementSystem.Unlock("word_fire");
				if (materialized.TryGetValue("wood", out var value2) && value2 != null && value2.root != null)
				{
					UnityEngine.Object.Destroy(value2.root);
				}
			}
			break;
		case "lamp":
		{
			GameObject obj = new GameObject("WarmLight");
			obj.transform.SetParent(lux3DSpawn.root.transform, worldPositionStays: false);
			obj.transform.localPosition = new Vector3(0f, 2f, 0f);
			Light light = obj.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = new Color(1f, 0.78f, 0.38f);
			light.range = 7.5f;
			light.intensity = 2.2f;
			LastActionApplied = true;
			break;
		}
		default:
			LastActionApplied = true;
			break;
		}
	}

	private void SpawnEmber(Vector3 at)
	{
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
		gameObject.name = "WordEmber";
		gameObject.transform.position = at + Vector3.up * 0.4f;
		gameObject.transform.localScale = Vector3.one * 0.14f;
		Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
		Color color2 = (material.color = new Color(1f, 0.62f, 0.2f));
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", color2);
		}
		gameObject.GetComponent<Renderer>().sharedMaterial = material;
		Light light = gameObject.AddComponent<Light>();
		light.type = LightType.Point;
		light.color = color2;
		light.range = 3.5f;
		light.intensity = 2.4f;
		StartCoroutine(EmberFly(gameObject.transform));
	}

	private IEnumerator EmberFly(Transform ember)
	{
		float t = 0f;
		Vector3 from = ember.position;
		while (t < 1.1f && ember != null && player != null)
		{
			t += Time.deltaTime;
			float num = Mathf.SmoothStep(0f, 1f, t / 1.1f);
			Vector3 b = player.transform.position + Vector3.up * 0.55f;
			ember.position = Vector3.Lerp(from, b, num) + Vector3.up * Mathf.Sin(num * MathF.PI) * 0.5f;
			ember.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.05f, num);
			yield return null;
		}
		if (ember != null)
		{
			UnityEngine.Object.Destroy(ember.gameObject);
		}
	}

	private static void ApplyWetLook(GameObject root)
	{
		Renderer[] componentsInChildren = root.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if (renderer is ParticleSystemRenderer)
			{
				continue;
			}
			Material[] materials = renderer.materials;
			foreach (Material material in materials)
			{
				string[] array = new string[3] { "_BaseColor", "baseColorFactor", "_Color" };
				foreach (string text in array)
				{
					if (material.HasProperty(text))
					{
						material.SetColor(text, new Color(0.5f, 0.58f, 0.66f, 1f));
					}
				}
				if (material.HasProperty("_Smoothness"))
				{
					material.SetFloat("_Smoothness", 0.85f);
				}
				if (material.HasProperty("roughnessFactor"))
				{
					material.SetFloat("roughnessFactor", 0.2f);
				}
			}
		}
		if (root.transform.Find("WetDrips") != null)
		{
			return;
		}
		GameObject obj = new GameObject("WetDrips");
		obj.transform.SetParent(root.transform, worldPositionStays: false);
		obj.transform.localPosition = new Vector3(0f, 0.3f, 0f);
		ParticleSystem particleSystem = obj.AddComponent<ParticleSystem>();
		ParticleSystem.MainModule main = particleSystem.main;
		main.startLifetime = 0.7f;
		main.startSpeed = 0f;
		main.startSize = 0.035f;
		main.startColor = new Color(0.7f, 0.86f, 0.95f, 0.9f);
		main.gravityModifier = 1.2f;
		main.maxParticles = 40;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		ParticleSystem.EmissionModule emission = particleSystem.emission;
		emission.rateOverTime = 9f;
		ParticleSystem.ShapeModule shape = particleSystem.shape;
		shape.shapeType = ParticleSystemShapeType.Box;
		shape.scale = new Vector3(0.5f, 0.05f, 0.3f);
		BakeryEffects bakeryEffects = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
		ParticleSystemRenderer component = obj.GetComponent<ParticleSystemRenderer>();
		if (bakeryEffects != null && bakeryEffects.ParticleMaterial != null)
		{
			component.sharedMaterial = bakeryEffects.ParticleMaterial;
			return;
		}
		Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
		if (shader != null)
		{
			component.sharedMaterial = new Material(shader);
		}
	}

	private void CleanupDragVisuals()
	{
		if (draggedSlipUI != null)
		{
			UnityEngine.Object.Destroy(draggedSlipUI.gameObject);
		}
		draggedSlipUI = null;
		if (draggedSource != null)
		{
			draggedSource.alpha = 1f;
		}
		draggedSource = null;
		draggingSlip = null;
		restoreLetterCloseAt = Time.frameCount + 1;
		if (aimPreviewRoot != null)
		{
			aimPreviewRoot.SetActive(value: false);
		}
	}

	private void CancelActiveDrag()
	{
		if (draggingSlip != null)
		{
			CleanupDragVisuals();
		}
	}

	private void RestorePaperOpacity()
	{
		if (paperCanvasGroup != null)
		{
			paperCanvasGroup.alpha = 1f;
		}
		if (overlayBackdropImage != null)
		{
			overlayBackdropImage.color = EggboyUI.LetterBackdropColor;
		}
	}
}
