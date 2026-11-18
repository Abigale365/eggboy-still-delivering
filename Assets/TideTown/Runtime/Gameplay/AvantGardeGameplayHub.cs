using UnityEngine;

public sealed class AvantGardeGameplayHub : MonoBehaviour
{
	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private StoryDirector director;

	private GameState state;

	private WordMaterializerSystem wordSys;

	private PerspectivePrintSystem perspSys;

	private TidalEchoCloneSystem echoSys;

	private ShadowAndChimeSystem shadowSys;

	public static bool AnyModalOpen { get; private set; }

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
	{
		player = p;
		ui = u;
		world = w;
		director = d;
		state = s;
		wordSys = base.gameObject.AddComponent<WordMaterializerSystem>();
		wordSys.Init(p, u, w, d, s);
		perspSys = base.gameObject.AddComponent<PerspectivePrintSystem>();
		perspSys.Init(p, u, w, s);
		echoSys = base.gameObject.AddComponent<TidalEchoCloneSystem>();
		echoSys.Init(p, u, w, d, s);
		shadowSys = base.gameObject.AddComponent<ShadowAndChimeSystem>();
		shadowSys.Init(p, u, s);
		base.gameObject.AddComponent<TideTownAchievementSystem>().Init(u, s);
		base.gameObject.AddComponent<WorldMechanismDirector>().Init(p, u, w, d, s);
		base.gameObject.AddComponent<HardwareAndSensorHub>().Init(p, u, w, d, s);
		base.gameObject.AddComponent<AIAgentCalibrationDirector>().Init(p, u, w, d, s);
		base.gameObject.AddComponent<DogPawPlate>().Init(w, s, u);
		base.gameObject.AddComponent<ShadowLanternPuzzle>().Init(p, u, w, s);
		base.gameObject.AddComponent<SoundBottleSystem>().Init(p, u, w, s);
		base.gameObject.AddComponent<HintDirector>().Init(p, u, w, d, s);
	}

	public void CloseForFilming()
	{
		if (perspSys != null)
		{
			perspSys.CloseViewfinder();
		}
		if (shadowSys != null)
		{
			shadowSys.CloseStudio();
		}
		if (ui != null)
		{
			ui.CloseLetter();
		}
		AnyModalOpen = false;
	}

	private void TriggerMode(int mode)
	{
		switch (mode)
		{
		case 1:
			if (perspSys != null && perspSys.IsViewfinderActive)
			{
				perspSys.CloseViewfinder();
			}
			if (shadowSys != null && shadowSys.IsStudioOpen)
			{
				shadowSys.CloseStudio();
			}
			if (wordSys != null)
			{
				wordSys.OpenLetterWithSlips();
			}
			break;
		case 2:
			if (ui != null && ui.LetterOpen)
			{
				ui.CloseLetter();
			}
			if (shadowSys != null && shadowSys.IsStudioOpen)
			{
				shadowSys.CloseStudio();
			}
			if (perspSys != null)
			{
				perspSys.ToggleViewfinder();
			}
			break;
		case 3:
			if (echoSys != null)
			{
				echoSys.TriggerEchoButton();
			}
			break;
		case 4:
			if (ui != null && ui.LetterOpen)
			{
				ui.CloseLetter();
			}
			if (perspSys != null && perspSys.IsViewfinderActive)
			{
				perspSys.CloseViewfinder();
			}
			if (shadowSys != null)
			{
				shadowSys.ToggleStudio();
			}
			break;
		}
	}

	private void Update()
	{
		if (ui == null || !ui.inputClose)
		{
			AnyModalOpen = false;
			return;
		}
		AnyModalOpen = (perspSys != null && perspSys.IsViewfinderActive) || (shadowSys != null && shadowSys.IsStudioOpen);
		if (ui.TitleShowing || ui.Paused || ObjectInspectModal.IsOpen || WoodenMechanismPuzzleModal.IsOpen || (ui.Cinematic && !ui.LetterOpen && !AnyModalOpen))
		{
			return;
		}
		if (EasyInputBridge.Down(KeyCode.Tab))
		{
			TriggerMode(1);
		}
		else if (Input.GetKeyDown(KeyCode.C) && !ui.LetterOpen && perspSys != null && !perspSys.IsViewfinderActive)
		{
			perspSys.OpenViewfinder();
		}
		else if (EasyInputBridge.Down(KeyCode.R))
		{
			if (!ui.LetterOpen && !AnyModalOpen && !player.locked && !AIAgentCalibrationDirector.IsOpen)
			{
				TriggerMode(3);
			}
		}
		else if (Features.oldModes && Input.GetKeyDown(KeyCode.Alpha2))
		{
			TriggerMode(2);
		}
		else if (Features.oldModes && Input.GetKeyDown(KeyCode.Alpha4))
		{
			TriggerMode(4);
		}
	}
}
