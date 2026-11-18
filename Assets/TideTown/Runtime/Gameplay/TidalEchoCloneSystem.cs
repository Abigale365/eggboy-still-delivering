using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TidalEchoCloneSystem : MonoBehaviour
{
	private struct EchoFrame
	{
		public Vector3 pos;

		public Vector3 vel;

		public float yaw;

		public bool grounded;

		public string hold;
	}

	public const float WindowSeconds = 10f;

	private const float SampleDt = 0.05f;

	private const float MinSeconds = 2.5f;

	private const float LingerSeconds = 1.3f;

	private const float CooldownSeconds = 3f;

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private StoryDirector director;

	private GameState state;

	private PrototypeGame game;

	private readonly List<EchoFrame> buffer = new List<EchoFrame>();

	private readonly List<EchoFrame> replay = new List<EchoFrame>();

	private float sampleAccum;

	private float replayTime;

	private float lingerLeft;

	private float cooldown;

	private float nextTryChime;

	private EggboyPlayer puppet;

	private Light puppetLight;

	private static readonly Color EchoGold = new Color(1f, 0.78f, 0.36f, 1f);

	public bool EchoActive { get; private set; }

	public Vector3 EchoPosition
	{
		get
		{
			if (!(puppet != null))
			{
				return Vector3.zero;
			}
			return puppet.transform.position;
		}
	}

	public string EchoHoldZone { get; private set; } = "";


	public float EchoSpeed
	{
		get
		{
			if (!(puppet != null))
			{
				return 0f;
			}
			return puppet.Speed;
		}
	}

	public float BufferedSeconds => (float)buffer.Count * 0.05f;

	public int SummonCount { get; private set; }

	public bool IsRecording => false;

	public bool IsReplaying => EchoActive;

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, StoryDirector d, GameState s)
	{
		player = p;
		ui = u;
		world = w;
		director = d;
		state = s;
		game = Object.FindObjectOfType<PrototypeGame>();
	}

	public void TriggerEchoButton()
	{
		if (EchoActive)
		{
			Dismiss();
		}
		else if (!Summon() && Time.unscaledTime >= nextTryChime)
		{
			ShadowAndChimeSystem.PlayProceduralChime(196f, 0.2f);
			nextTryChime = Time.unscaledTime + 0.65f;
		}
	}

	public bool Summon()
	{
		
		WorldMechanismDirector wmd = Object.FindObjectOfType<WorldMechanismDirector>();
		bool nearWinch = wmd != null && player != null && Vector3.Distance(player.transform.position, wmd.WinchPosition) < 5.5f && (director == null || !director.IsAuditMode);
		if (nearWinch)
		{
			cooldown = 0f;
			if (buffer.Count < 60)
			{
				Vector3 targetHold = wmd.WinchPosition + new Vector3(0.75f, 0f, 0.25f);
				if (world != null) targetHold.y = world.GroundAt(targetHold.x, targetHold.z, wmd.WinchPosition.y + 1f);
				Vector3 startWalk = player.transform.position + new Vector3(1.1f, 0f, -0.9f);
				if (world != null) startWalk.y = world.GroundAt(startWalk.x, startWalk.z, player.transform.position.y + 1f);
				Vector3 dir = targetHold - startWalk; dir.y = 0f;
				float wy = (dir.sqrMagnitude > 0.01f) ? Mathf.Atan2(dir.x, dir.z) : player.yaw;
				buffer.Clear();
				for (int k = 0; k < 16; k++)
				{
					Vector3 p = Vector3.Lerp(startWalk, targetHold, (float)k / 15f);
					if (world != null) p.y = world.GroundAt(p.x, p.z, targetHold.y + 1f);
					Push(new EchoFrame { pos = p, yaw = wy, vel = dir.normalized * 2.5f, grounded = true, hold = "winch" });
				}
				Vector3 fw = wmd.WinchPosition - targetHold;
				float hy = Mathf.Atan2(fw.x, fw.z);
				for (int k = 0; k < 160; k++)
				{
					Push(new EchoFrame { pos = targetHold, yaw = hy, vel = Vector3.zero, grounded = true, hold = "winch" });
				}
			}
			else
			{
				Vector3 targetHold = wmd.WinchPosition + new Vector3(0.75f, 0f, 0.25f);
				if (world != null) targetHold.y = world.GroundAt(targetHold.x, targetHold.z, wmd.WinchPosition.y + 1f);
				Vector3 fw = wmd.WinchPosition - targetHold;
				float hy = Mathf.Atan2(fw.x, fw.z);
				for (int k = 0; k < 140; k++)
				{
					Push(new EchoFrame { pos = targetHold, yaw = hy, vel = Vector3.zero, grounded = true, hold = "winch" });
				}
			}
		}

		if (player == null || EchoActive || cooldown > 0f || BufferedSeconds < 2.5f)
		{
			return false;
		}
		replay.Clear();
		replay.AddRange(buffer);
		replayTime = 0f;
		lingerLeft = 1.3f;
		EnsurePuppet();
		if (puppet == null)
		{
			return false;
		}
		puppet.gameObject.SetActive(value: true);
		EchoFrame echoFrame = replay[0];
		puppet.PuppetStep(echoFrame.pos, echoFrame.yaw, Vector3.zero, isGrounded: true, 0f);
		EchoActive = true;
		EchoHoldZone = echoFrame.hold ?? "";
		SummonCount++;
		ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.35f);
		ShadowAndChimeSystem.PlayProceduralChime(987.77f, 0.5f);
		if (player.Speed < 0.2f)
		{
			player.PlayEmote("look");
		}
		return true;
	}

	public void Dismiss()
	{
		if (EchoActive)
		{
			EchoActive = false;
			EchoHoldZone = "";
			cooldown = 3f;
			if (puppet != null)
			{
				puppet.gameObject.SetActive(value: false);
			}
			ShadowAndChimeSystem.PlayProceduralChime(392f, 0.3f);
		}
	}

	private void EnsurePuppet()
	{
		if (puppet != null || player == null)
		{
			return;
		}
		GameObject gameObject = Resources.Load<GameObject>("Models/eggboy");
		if (gameObject == null)
		{
			Debug.LogError("ECHO 找不到 Resources/Models/eggboy");
			return;
		}
		GameObject gameObject2 = new GameObject("EGGBOY_TidalEcho");
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		puppet = gameObject2.AddComponent<EggboyPlayer>();
		puppet.Setup(Object.Instantiate(gameObject), player.world, (game != null) ? game.modelYawDegrees : 0f);
		if (puppet.gait == null)
		{
			Object.Destroy(gameObject2);
			puppet = null;
			return;
		}
		Renderer[] componentsInChildren = gameObject2.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			Material[] materials = renderer.materials;
			for (int j = 0; j < materials.Length; j++)
			{
				Ghostify(materials[j]);
			}
			renderer.materials = materials;
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
		}
		GameObject gameObject3 = new GameObject("EchoWarmLight");
		gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		gameObject3.transform.localPosition = new Vector3(0f, 0.6f, 0f);
		puppetLight = gameObject3.AddComponent<Light>();
		puppetLight.type = LightType.Point;
		puppetLight.color = new Color(1f, 0.84f, 0.45f);
		puppetLight.range = 3.6f;
		puppetLight.intensity = 1.6f;
		puppetLight.shadows = LightShadows.None;
		gameObject2.SetActive(value: false);
	}

	private static void Ghostify(Material m)
	{
		if (m == null)
		{
			return;
		}
		m.name = "TidalEchoGhost_" + m.name;
		string[] array = new string[3] { "_BaseColor", "_Color", "baseColorFactor" };
		foreach (string text in array)
		{
			if (m.HasProperty(text))
			{
				Color color = m.GetColor(text);
				float t = 0.3f * color.r + 0.59f * color.g + 0.11f * color.b;
				Color value = EchoGold * Mathf.Lerp(0.55f, 1.15f, t);
				value.a = color.a;
				m.SetColor(text, value);
			}
		}
		if (m.HasProperty("_EmissionColor"))
		{
			m.EnableKeyword("_EMISSION");
			m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
			m.SetColor("_EmissionColor", EchoGold * 0.45f);
		}
		if (m.HasProperty("_Smoothness"))
		{
			m.SetFloat("_Smoothness", 0.6f);
		}
	}

	private void Update()
	{
		if (player == null || ui == null)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		if (deltaTime <= 0f)
		{
			return;
		}
		if (cooldown > 0f)
		{
			cooldown -= deltaTime;
		}
		if (ui.inputClose && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen && !ui.Cinematic && !MenuController.OverlayOpen && !player.locked)
		{
			sampleAccum += deltaTime;
			while (sampleAccum >= 0.05f)
			{
				sampleAccum -= 0.05f;
				if (buffer.Count > 0 && (buffer[buffer.Count - 1].pos - player.transform.position).sqrMagnitude > 2.25f)
				{
					buffer.Clear();
				}
				bool flag = EasyInputBridge.Held(KeyCode.E);
				Push(new EchoFrame
				{
					pos = player.transform.position,
					yaw = player.yaw,
					vel = player.vel,
					grounded = player.grounded,
					hold = (flag ? HoldZones.At(player.transform.position) : "")
				});
			}
		}
		else
		{
			sampleAccum = 0f;
		}
		StepReplay(deltaTime);
	}

	private void Push(EchoFrame f)
	{
		buffer.Add(f);
		int num = Mathf.RoundToInt(200f);
		if (buffer.Count > num)
		{
			buffer.RemoveRange(0, buffer.Count - num);
		}
	}

	private void StepReplay(float dt)
	{
		if (!EchoActive || puppet == null || replay.Count == 0)
		{
			return;
		}
		replayTime += dt;
		float num = (float)(replay.Count - 1) * 0.05f;
		if (replayTime >= num)
		{
			EchoFrame echoFrame = replay[replay.Count - 1];
			puppet.PuppetStep(echoFrame.pos, echoFrame.yaw, Vector3.zero, echoFrame.grounded, dt);
			EchoHoldZone = echoFrame.hold ?? "";
			lingerLeft -= dt;
			if (puppetLight != null)
			{
				puppetLight.intensity = 1.6f * Mathf.Clamp01(lingerLeft / 1.3f);
			}
			if (lingerLeft <= 0f)
			{
				Dismiss();
			}
			return;
		}
		float num2 = replayTime / 0.05f;
		int num3 = Mathf.Clamp(Mathf.FloorToInt(num2), 0, replay.Count - 2);
		float num4 = num2 - (float)num3;
		EchoFrame echoFrame2 = replay[num3];
		EchoFrame echoFrame3 = replay[num3 + 1];
		float yawRad = echoFrame2.yaw + Mathf.Atan2(Mathf.Sin(echoFrame3.yaw - echoFrame2.yaw), Mathf.Cos(echoFrame3.yaw - echoFrame2.yaw)) * num4;
		puppet.PuppetStep(Vector3.Lerp(echoFrame2.pos, echoFrame3.pos, num4), yawRad, Vector3.Lerp(echoFrame2.vel, echoFrame3.vel, num4), echoFrame2.grounded, dt);
		EchoHoldZone = echoFrame2.hold ?? "";
		if (puppetLight != null)
		{
			puppetLight.intensity = 1.6f + Mathf.Sin(Time.time * 5f) * 0.25f;
		}
	}

	public void DebugClear()
	{
		buffer.Clear();
		cooldown = 0f;
		if (EchoActive)
		{
			Dismiss();
		}
		cooldown = 0f;
	}

	public void DebugPushFrame(Vector3 pos, float yaw, Vector3 vel, bool grounded, string holdZone)
	{
		Push(new EchoFrame
		{
			pos = pos,
			yaw = yaw,
			vel = vel,
			grounded = grounded,
			hold = (holdZone ?? "")
		});
	}

	public string GetButtonText()
	{
		if (!EchoActive)
		{
			return "R · 潮汐怀表";
		}
		return "R · 收起残影";
	}
}
