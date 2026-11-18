using UnityEngine;
using UnityEngine.UI;

public sealed class MenuController : MonoBehaviour
{
	private const float OverlayGrace = 0.5f;

	private const float ScriptUnlockGrace = 1f;

	private static float lastScriptUnlockAt = -10f;

	private EggboyUI ui;

	private TownMap map;

	private StoryDirector director;

	private Text returnCaption;

	private SafePoints safety;

	private Button guideButton;

	private Text guideCaption;

	private bool letterOpenLastFrame;

	private bool wasLocked;

	private float lastEscAt = -10f;

	private float overlaySeenAt = -10f;

	public bool AutoPauseOnLockLoss;

	public static bool OverlayOpen
	{
		get
		{
			if (!ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen && !AIAgentCalibrationDirector.IsOpen && !AvantGardeGameplayHub.AnyModalOpen && !WishInspectModal.IsOpen)
			{
				return SchoolBlackboard.IsOpen;
			}
			return true;
		}
	}

	public bool OverlayBusy
	{
		get
		{
			TrackOverlay();
			return Time.unscaledTime - overlaySeenAt < 0.5f;
		}
	}

	public bool Blocked
	{
		get
		{
			if (!ui.LetterOpen && !letterOpenLastFrame && !OverlayBusy && (!(map != null) || (!map.IsOpen && !(Time.unscaledTime - map.ClosedAt < 0.25f))))
			{
				if (director != null)
				{
					if (!director.Cutscene && !director.Climbing)
					{
						return director.Ended;
					}
					return true;
				}
				return false;
			}
			return true;
		}
	}

	public static void NoteScriptUnlock()
	{
		lastScriptUnlockAt = Time.unscaledTime;
	}

	public static void ReleasePointer()
	{
		NoteScriptUnlock();
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	public static void RestorePointer(EggboyUI ui)
	{
		TownMap townMap = Object.FindObjectOfType<TownMap>();
		StoryDirector storyDirector = Object.FindObjectOfType<StoryDirector>();
		if (!(ui == null) && !ui.Paused && !ui.LetterOpen && !ui.TitleShowing && !ui.Cinematic && !OverlayOpen && (!(townMap != null) || !townMap.IsOpen) && (!(storyDirector != null) || (!storyDirector.Cutscene && !storyDirector.Climbing && !storyDirector.Ended)))
		{
			if (Application.platform == RuntimePlatform.WebGLPlayer)
			{
				ReleasePointer();
				return;
			}
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	private void Awake()
	{
		AutoPauseOnLockLoss = Application.platform == RuntimePlatform.WebGLPlayer;
	}

	public void Init(EggboyUI u, TownMap m, StoryDirector d)
	{
		ui = u;
		map = m;
		director = d;
		RectTransform rectTransform = TideTownStyleUI.FindHudCanvasRoot(ui);
		if (rectTransform == null)
		{
			return;
		}
		safety = Object.FindObjectOfType<SafePoints>();
		Transform transform = TideTownStyleUI.FindDeepChild(rectTransform, "PauseReturnToSafety");
		if (transform != null)
		{
			transform.gameObject.SetActive(safety != null);
			returnCaption = transform.GetComponentInChildren<Text>();
			ui.OnReturnToSafety = ReturnToSafety;
		}
		if (!(map == null))
		{
			guideButton = TideTownStyleUI.CreateWarmButton(rectTransform, "", new Vector2(0f, -133f), new Vector2(360f, 26f), primaryAmber: false, delegate
			{
				map.CycleGuideMode();
				RefreshGuideCaption();
			});
			guideButton.name = "PauseGroundGuide";
			guideCaption = guideButton.GetComponentInChildren<Text>();
			Transform transform2 = TideTownStyleUI.FindDeepChild(rectTransform, "PauseGuideSlot");
			if (transform2 != null)
			{
				guideButton.transform.SetParent(transform2, worldPositionStays: false);
				RectTransform obj = (RectTransform)guideButton.transform;
				obj.anchorMin = Vector2.zero;
				obj.anchorMax = Vector2.one;
				obj.pivot = new Vector2(0.5f, 0.5f);
				Vector2 offsetMin = (obj.offsetMax = Vector2.zero);
				obj.offsetMin = offsetMin;
				obj.anchoredPosition = Vector2.zero;
				TideTownStyleUI.StyleSettingButton(guideButton);
			}
			RefreshGuideCaption();
			guideButton.gameObject.SetActive(value: false);
		}
	}

	private void ReturnToSafety()
	{
		if (!ui.Paused || Blocked || safety == null)
		{
			return;
		}
		if (!safety.TryReturnToSafety())
		{
			if (returnCaption != null)
			{
				returnCaption.text = "尚无可返回的安全位置";
			}
			return;
		}
		if (returnCaption != null)
		{
			returnCaption.text = "返回安全位置";
		}
		ui.SetPaused(on: false);
		ui.ShowToast("已返回安全位置", 2f);
	}

	private void RefreshGuideCaption()
	{
		TownMap.GroundGuideMode guideMode = map.GuideMode;
		guideCaption.text = "引路光点：" + guideMode switch
		{
			TownMap.GroundGuideMode.Always => "常显", 
			TownMap.GroundGuideMode.Off => "关闭（自由探索，按 H 可开启）", 
			_ => "柔和自动", 
		};
	}

	private void TrackOverlay()
	{
		if (OverlayOpen)
		{
			overlaySeenAt = Time.unscaledTime;
		}
	}

	public bool WouldTogglePause(bool esc)
	{
		if (esc && ui != null && !ui.TitleShowing)
		{
			return !Blocked;
		}
		return false;
	}

	public bool WouldAutoPause(bool wasLockedBefore, bool lockedNow, bool focused)
	{
		if (AutoPauseOnLockLoss && ui != null && wasLockedBefore && !lockedNow && focused && !ui.TitleShowing && !ui.Paused && !Blocked && Time.unscaledTime - lastEscAt > 0.3f)
		{
			return Time.unscaledTime - lastScriptUnlockAt > 1f;
		}
		return false;
	}

	private void Update()
	{
		if (!(ui == null))
		{
			bool keyDown = EasyInputBridge.Down(KeyCode.Escape);
			if (keyDown)
			{
				lastEscAt = Time.unscaledTime;
			}
			bool num = WouldTogglePause(keyDown);
			letterOpenLastFrame = ui.LetterOpen;
			if (num)
			{
				ui.SetPaused(!ui.Paused);
			}
		}
	}

	private void LateUpdate()
	{
		if (!(ui == null))
		{
			TrackOverlay();
			bool lockedNow = Cursor.lockState == CursorLockMode.Locked;
			if (WouldAutoPause(wasLocked, lockedNow, Application.isFocused))
			{
				ui.SetPaused(on: true);
			}
			wasLocked = lockedNow;
			if (guideButton != null && guideButton.gameObject.activeSelf != ui.Paused)
			{
				guideButton.gameObject.SetActive(ui.Paused);
			}
		}
	}
}
