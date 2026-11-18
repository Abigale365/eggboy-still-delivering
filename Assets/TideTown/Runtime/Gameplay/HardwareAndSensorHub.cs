using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

public sealed class HardwareAndSensorHub : MonoBehaviour
{
	private struct JOYINFOEX
	{
		public uint dwSize;

		public uint dwFlags;

		public uint dwXpos;

		public uint dwYpos;

		public uint dwZpos;

		public uint dwRpos;

		public uint dwUpos;

		public uint dwVpos;

		public uint dwButtons;

		public uint dwButtonNumber;

		public uint dwPOV;

		public uint dwReserved1;

		public uint dwReserved2;
	}

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private StoryDirector director;

	private GameState state;

	private InteractionService interactionSvc;

	private uint prevPad1Buttons;

	private float pad1RingCooldown;

	private bool builtInCamActive;

	private WebCamTexture webCamTex;

	private Color32[] prevCamPixels;

	private bool prevHandPinch;

	private Vector2 prevHandPos = new Vector2(0.5f, 0.5f);

	private float lastWebcamPacketTime;

	private IMicLevel inspectMicrophone;

	private float nextBellows;

	private Vector3 stovePosition;

	private readonly ConcurrentQueue<string> incomingPackets = new ConcurrentQueue<string>();

	private UdpClient udpListener;

	private Thread ioThread;

	private volatile bool running;

	public static HardwareAndSensorHub Instance { get; private set; }

	public bool Gamepad1Connected { get; private set; }

	public Vector2 Pad1LeftStick { get; private set; }

	public Vector2 Pad1RightStick { get; private set; }

	public uint Pad1Buttons { get; private set; }

	public bool WebcamGestureActive { get; private set; }

	public bool CamOpen
	{
		get
		{
			if (!WebcamGestureActive)
			{
				if (builtInCamActive && webCamTex != null)
				{
					return webCamTex.isPlaying;
				}
				return false;
			}
			return true;
		}
	}

	public Vector2 HandNormalizedPos { get; private set; } = new Vector2(0.5f, 0.5f);


	public float HandCenterX => HandNormalizedPos.x;

	public float HandWaveSpeed { get; private set; }

	public bool HandPinching { get; private set; }

	public bool MicActive { get; private set; }

	public float MicLoudness { get; private set; }

	public float ExternalWindStabilizeBoost { get; private set; }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
	[DllImport("winmm.dll")]
	private static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);
#endif

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
	{
		Instance = this;
		player = p;
		ui = u;
		world = w;
		director = d;
		state = s;
		interactionSvc = UnityEngine.Object.FindObjectOfType<InteractionService>();
		BakeryItem bakeryItem = Array.Find(BakeryData.Load(), (BakeryItem item) => item.id == "stove");
		if (bakeryItem != null)
		{
			stovePosition = new Vector3(bakeryItem.x, bakeryItem.y, bakeryItem.z);
		}
#if !UNITY_WEBGL || UNITY_EDITOR
		StartBackgroundUdpThread();
#endif
	}

	public void EnableInspectMicrophone()
	{
		if (ObjectInspectModal.IsOpen && inspectMicrophone == null)
		{
			inspectMicrophone = new PlatformMicLevel();
			inspectMicrophone.Start();
		}
	}

	public void StopInspectMicrophone()
	{
		inspectMicrophone?.Stop();
		inspectMicrophone = null;
		MicActive = false;
		MicLoudness = 0f;
	}

	public bool ToggleBuiltInWebcam()
	{
		try
		{
			if (builtInCamActive && webCamTex != null)
			{
				webCamTex.Stop();
				builtInCamActive = false;
				HandWaveSpeed = 0f;
				return false;
			}
			if (WebCamTexture.devices != null && WebCamTexture.devices.Length != 0)
			{
				if (webCamTex == null)
				{
					webCamTex = new WebCamTexture(WebCamTexture.devices[0].name, 160, 120, 15);
				}
				webCamTex.Play();
				builtInCamActive = true;
				prevCamPixels = null;
				return true;
			}
		}
		catch (Exception)
		{
			builtInCamActive = false;
		}
		return false;
	}

	private void StartBackgroundUdpThread()
	{
		running = true;
		try
		{
			udpListener = new UdpClient(18888);
			udpListener.Client.ReceiveTimeout = 25;
		}
		catch (Exception)
		{
			udpListener = null;
		}
		ioThread = new Thread(UdpPollLoop)
		{
			IsBackground = true,
			Name = "TideTownSensorUdp"
		};
		ioThread.Start();
	}

	private void UdpPollLoop()
	{
		while (running)
		{
			try
			{
				if (udpListener != null && udpListener.Available > 0)
				{
					IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
					byte[] bytes = udpListener.Receive(ref remoteEP);
					string[] array = Encoding.UTF8.GetString(bytes).Split('\n');
					for (int i = 0; i < array.Length; i++)
					{
						string text = array[i].Trim();
						if (!string.IsNullOrEmpty(text))
						{
							incomingPackets.Enqueue(text);
						}
					}
				}
			}
			catch (Exception)
			{
			}
			Thread.Sleep(12);
		}
	}

	private void OnDestroy()
	{
		StopInspectMicrophone();
		running = false;
		try
		{
			udpListener?.Close();
		}
		catch (Exception)
		{
		}
		try
		{
			if (webCamTex != null && webCamTex.isPlaying)
			{
				webCamTex.Stop();
			}
		}
		catch (Exception)
		{
		}
	}

	private void Update()
	{
		if (ui == null || !ui.inputClose)
		{
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if (unscaledDeltaTime <= 0f || unscaledDeltaTime > 0.25f)
		{
			return;
		}
		ExternalWindStabilizeBoost = Mathf.MoveTowards(ExternalWindStabilizeBoost, 0f, unscaledDeltaTime * 1.6f);
		if (player != null && player.transform.position.y > 10f && !player.locked && !WoodenMechanismPuzzleModal.IsOpen && !ObjectInspectModal.IsOpen && EasyInputBridge.Down(KeyCode.B) && director != null)
		{
			director.DescendTower();
		}
		ProcessIncomingGesturePackets();
		UpdateBuiltInWebcamMotion(unscaledDeltaTime);
		if (WebcamGestureActive && Time.unscaledTime - lastWebcamPacketTime > 2.5f)
		{
			WebcamGestureActive = false;
		}
		PollSingleGamepad(unscaledDeltaTime);
		bool flag = player != null && state != null && state.flags.Contains("oven_lit") && Vector3.Distance(player.transform.position, stovePosition) < 3.5f && !ui.Paused && !ObjectInspectModal.IsOpen;
		if (inspectMicrophone == null && (ObjectInspectModal.IsOpen || flag))
		{
			inspectMicrophone = new PlatformMicLevel();
			inspectMicrophone.Start();
		}
		else if (inspectMicrophone != null && !ObjectInspectModal.IsOpen && !flag)
		{
			StopInspectMicrophone();
		}
		float num = inspectMicrophone?.ReadRms() ?? (-1f);
		MicActive = num >= 0f;
		MicLoudness = (MicActive ? Mathf.Clamp01(num * 14f) : 0f);
		if (flag && (num > 0.006f || EasyInputBridge.Down(KeyCode.E)) && Time.unscaledTime >= nextBellows)
		{
			nextBellows = Time.unscaledTime + 1.1f;
			BakeryEffects bakeryEffects = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
			if (bakeryEffects != null && director != null)
			{
				director.BoostOvenBellows(bakeryEffects);
			}
		}
	}

	private void UpdateBuiltInWebcamMotion(float dt)
	{
		if (!builtInCamActive || webCamTex == null || !webCamTex.isPlaying || !webCamTex.didUpdateThisFrame)
		{
			return;
		}
		Color32[] pixels = webCamTex.GetPixels32();
		int width = webCamTex.width;
		int height = webCamTex.height;
		if (pixels == null || pixels.Length != width * height)
		{
			return;
		}
		if (prevCamPixels == null || prevCamPixels.Length != pixels.Length)
		{
			prevCamPixels = (Color32[])pixels.Clone();
			return;
		}
		long num = 0L;
		long num2 = 0L;
		int num3 = 0;
		for (int i = 0; i < height; i += 2)
		{
			int num4 = i * width;
			for (int j = 0; j < width; j += 2)
			{
				int num5 = num4 + j;
				Color32 color = pixels[num5];
				Color32 color2 = prevCamPixels[num5];
				if (Mathf.Abs(color.r - color2.r) + Mathf.Abs(color.g - color2.g) + Mathf.Abs(color.b - color2.b) > 72)
				{
					num += width - 1 - j;
					num2 += i;
					num3++;
				}
				prevCamPixels[num5] = color;
			}
		}
		int num6 = width / 2 * (height / 2);
		float num7 = ((num6 > 0) ? ((float)num3 / (float)num6) : 0f);
		HandWaveSpeed = Mathf.Lerp(HandWaveSpeed, Mathf.Clamp01(num7 * 5.5f), dt * 10f);
		if (num3 > 18)
		{
			float x = Mathf.Clamp01((float)num / (float)(num3 * width));
			float y = Mathf.Clamp01((float)num2 / (float)(num3 * height));
			HandNormalizedPos = Vector2.Lerp(HandNormalizedPos, new Vector2(x, y), dt * 10f);
		}
	}

	private void ProcessIncomingGesturePackets()
	{
		int num = 0;
		string result;
		while (num++ < 24 && incomingPackets.TryDequeue(out result))
		{
			if (!result.StartsWith("HAND:"))
			{
				continue;
			}
			WebcamGestureActive = true;
			lastWebcamPacketTime = Time.unscaledTime;
			string[] array = result.Substring(5).Split(',');
			if (array.Length < 5 || !float.TryParse(array[0], out var result2) || !float.TryParse(array[1], out var result3) || !int.TryParse(array[2], out var result4) || !float.TryParse(array[3], out var result5))
			{
				continue;
			}
			Vector2 vector = new Vector2(Mathf.Clamp01(result2), Mathf.Clamp01(result3));
			Vector2 vector2 = vector - prevHandPos;
			prevHandPos = vector;
			HandNormalizedPos = vector;
			HandPinching = result4 > 0;
			HandWaveSpeed = result5;
			if (ObjectInspectModal.IsOpen)
			{
				ObjectInspectModal.ExternalRotate((0f - vector2.x) * 220f, vector2.y * 180f);
				if (HandPinching && !prevHandPinch)
				{
					ObjectInspectModal.ExternalPressPin((!(result2 < 0.38f)) ? ((!(result2 > 0.62f)) ? 1 : 2) : 0);
				}
			}
			else if (WoodenMechanismPuzzleModal.IsOpen)
			{
				if (Mathf.Abs(vector2.x) > 0.045f)
				{
					WoodenMechanismPuzzleModal.ExternalRotateRing(WoodenMechanismPuzzleModal.ActiveRingSelection, (vector2.x > 0f) ? 1 : (-1));
				}
				if (HandPinching && !prevHandPinch)
				{
					WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
				}
			}
			prevHandPinch = HandPinching;
		}
	}

	private void PollSingleGamepad(float dt)
	{
		Gamepad1Connected = ReadWinMMJoystick(0, out var leftStick, out var rightStick, out var buttons);
		Pad1LeftStick = leftStick;
		Pad1RightStick = rightStick;
		Pad1Buttons = buttons;
		uint num = buttons & ~prevPad1Buttons;
		prevPad1Buttons = buttons;
		if (pad1RingCooldown > 0f)
		{
			pad1RingCooldown -= dt;
		}
		if (ObjectInspectModal.IsOpen)
		{
			Vector2 vector = ((leftStick.sqrMagnitude > rightStick.sqrMagnitude) ? leftStick : rightStick);
			if (vector.sqrMagnitude > 0.04f)
			{
				ObjectInspectModal.ExternalRotate((0f - vector.x) * 125f * dt, vector.y * 105f * dt);
			}
			if ((num & 0x20u) != 0)
			{
				ObjectInspectModal.ExternalClearDust(byBreath: false);
			}
			if ((num & (true ? 1u : 0u)) != 0)
			{
				ObjectInspectModal.ExternalPressPin(0);
			}
			if ((num & 2u) != 0)
			{
				ObjectInspectModal.ExternalPressPin(1);
			}
			if ((num & 4u) != 0)
			{
				ObjectInspectModal.ExternalPressPin(2);
			}
			return;
		}
		if (WoodenMechanismPuzzleModal.IsOpen)
		{
			float num2 = ((Mathf.Abs(leftStick.x) > Mathf.Abs(rightStick.x)) ? leftStick.x : rightStick.x);
			if (Mathf.Abs(num2) > 0.55f && pad1RingCooldown <= 0f)
			{
				WoodenMechanismPuzzleModal.ExternalRotateRing(WoodenMechanismPuzzleModal.ActiveRingSelection, (num2 > 0f) ? 1 : (-1));
				pad1RingCooldown = 0.24f;
			}
			if ((num & (true ? 1u : 0u)) != 0)
			{
				WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
			}
			if ((num & 4u) != 0)
			{
				WoodenMechanismPuzzleModal.ExternalUnlockAllLatchesByBlowOrGesture("手柄机关拨栓");
			}
			if ((num & 0x10u) != 0)
			{
				WoodenMechanismPuzzleModal.ActiveRingSelection = (WoodenMechanismPuzzleModal.ActiveRingSelection + 2) % 3;
			}
			if ((num & 0x20u) != 0)
			{
				WoodenMechanismPuzzleModal.ActiveRingSelection = (WoodenMechanismPuzzleModal.ActiveRingSelection + 1) % 3;
			}
			return;
		}
		if (leftStick.sqrMagnitude > 0.12f)
		{
			ExternalWindStabilizeBoost = 1f;
			if (player != null && !player.locked && world != null && !AIAgentCalibrationDirector.IsOpen)
			{
				Transform obj = ((Camera.main != null) ? Camera.main.transform : player.transform);
				Vector3 normalized = Vector3.ProjectOnPlane(obj.forward, Vector3.up).normalized;
				Vector3 normalized2 = Vector3.ProjectOnPlane(obj.right, Vector3.up).normalized;
				Vector3 normalized3 = (normalized * leftStick.y + normalized2 * leftStick.x).normalized;
				Vector3 position = player.transform.position + normalized3 * (4.2f * dt);
				if (world.Walkable(position.x, position.z))
				{
					position.y = world.GroundAt(position.x, position.z, position.y);
					player.transform.position = position;
					player.yaw = Mathf.Atan2(normalized3.x, normalized3.z);
					player.transform.rotation = Quaternion.Euler(0f, player.yaw * 57.29578f, 0f);
				}
			}
		}
		if (((num & (true ? 1u : 0u)) != 0 || (num & 4u) != 0) && !AIAgentCalibrationDirector.IsOpen && interactionSvc != null)
		{
			interactionSvc.TryInteractNearest();
		}
		if ((num & 2u) != 0 && player != null && player.transform.position.y > 10f && director != null)
		{
			director.DescendTower();
		}
		if ((num & 8u) != 0)
		{
			AIAgentCalibrationDirector.ToggleConsole();
		}
		if ((num & 0x20u) != 0)
		{
			TriggerButtonBlowEffect();
		}
	}

	private static bool ReadWinMMJoystick(int joyId, out Vector2 leftStick, out Vector2 rightStick, out uint buttons)
	{
		leftStick = Vector2.zero;
		rightStick = Vector2.zero;
		buttons = 0u;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
		JOYINFOEX jOYINFOEX = default(JOYINFOEX);
		jOYINFOEX.dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX));
		jOYINFOEX.dwFlags = 255u;
		JOYINFOEX pji = jOYINFOEX;
		if (joyGetPosEx(joyId, ref pji) != 0)
		{
			return false;
		}
		float num = ((float)pji.dwXpos - 32767f) / 32768f;
		float num2 = (0f - ((float)pji.dwYpos - 32767f)) / 32768f;
		float num3 = ((float)pji.dwZpos - 32767f) / 32768f;
		float num4 = (0f - ((float)pji.dwRpos - 32767f)) / 32768f;
		if (Mathf.Abs(num) < 0.18f)
		{
			num = 0f;
		}
		if (Mathf.Abs(num2) < 0.18f)
		{
			num2 = 0f;
		}
		if (Mathf.Abs(num3) < 0.18f)
		{
			num3 = 0f;
		}
		if (Mathf.Abs(num4) < 0.18f)
		{
			num4 = 0f;
		}
		leftStick = new Vector2(num, num2);
		rightStick = new Vector2(num3, num4);
		buttons = pji.dwButtons;
		return true;
#else
		return false;
#endif
	}

	private void TriggerButtonBlowEffect()
	{
		if (ObjectInspectModal.IsOpen)
		{
			ObjectInspectModal.ExternalClearDust(byBreath: false);
		}
		else if (player != null && Vector3.Distance(player.transform.position, new Vector3(-11.5f, 1.3f, -7.8f)) < 9.5f)
		{
			BakeryEffects bakeryEffects = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
			if (bakeryEffects != null && director != null && state != null && state.flags.Contains("oven_lit"))
			{
				director.BoostOvenBellows(bakeryEffects);
			}
		}
	}
}
