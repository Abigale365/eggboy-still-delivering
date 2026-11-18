using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TidalWindChallenge : MonoBehaviour
{
	private sealed class Zone
	{
		public string a;

		public string b;

		public Vector3 p0;

		public Vector3 p1;

		public Vector3 wind;

		public float radius = 3.2f;
	}

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private GameState state;

	private StoryDirector director;

	private PrototypeGame game;

	private readonly List<Zone> zones = new List<Zone>();

	private Zone currentZone;

	private float zoneTime;

	private float cooldown;

	private float warnTimer;

	private float gustTimer;

	private float boostTimer;

	private const float WarnSeconds = 1.5f;

	private const float GustSeconds = 3.2f;

	private const float BoostSeconds = 3f;

	public bool DebugBrace;

	public static bool Suppress;

	private ParticleSystem streaks;

	private ParticleSystem trail;

	private AudioSource windAudio;

	private bool crouchedByWind;

	private ExprId expressionBeforeWind;

	private ExprId? windExpression;

	private static AudioClip windClip;

	public float Imbalance { get; private set; }

	public string Phase
	{
		get
		{
			if (!(warnTimer > 0f))
			{
				if (!(gustTimer > 0f))
				{
					if (!(boostTimer > 0f))
					{
						return "calm";
					}
					return "boost";
				}
				return "gust";
			}
			return "warn";
		}
	}

	public bool InWindZone => currentZone != null;

	public bool IsGustActive
	{
		get
		{
			if (!Suppress && !Features.classicAudit && currentZone != null)
			{
				if (!(warnTimer > 0f))
				{
					return gustTimer > 0f;
				}
				return true;
			}
			return false;
		}
	}

	public Vector3 WindDir { get; private set; } = Vector3.right;


	public int Failures { get; private set; }

	public int Survived { get; private set; }

	private bool PlayerFree
	{
		get
		{
			if (ui != null && !ui.TitleShowing && !ui.Paused && !ui.LetterOpen && player != null && !player.locked && !ObjectInspectModal.IsOpen && !WoodenMechanismPuzzleModal.IsOpen && !AvantGardeGameplayHub.AnyModalOpen)
			{
				return !AIAgentCalibrationDirector.IsOpen;
			}
			return false;
		}
	}

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
	{
		player = p;
		ui = u;
		world = w;
		state = s;
		director = UnityEngine.Object.FindObjectOfType<StoryDirector>();
		game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		BuildZones();
	}

	private void BuildZones()
	{
		Dictionary<string, Vector3> pts = new Dictionary<string, Vector3>();
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		if (textAsset != null)
		{
			RouteList routeList = JsonUtility.FromJson<RouteList>(textAsset.text);
			if (routeList != null && routeList.route != null)
			{
				RouteStop[] route = routeList.route;
				foreach (RouteStop routeStop in route)
				{
					if (routeStop.stop && !pts.ContainsKey(routeStop.id))
					{
						pts[routeStop.id] = new Vector3(routeStop.x, 0f, routeStop.z);
					}
				}
			}
		}
		Add("LOW", "24");
		Add("24", "25");
		Add("22", "30");
		void Add(string a, string b)
		{
			if (pts.ContainsKey(a) && pts.ContainsKey(b) && !(world == null))
			{
				Zone zone = new Zone
				{
					a = a,
					b = b,
					p0 = pts[a],
					p1 = pts[b]
				};
				Vector3 vector = (zone.p0 + zone.p1) * 0.5f;
				Vector3 vector2 = zone.p1 - zone.p0;
				vector2.y = 0f;
				vector2.Normalize();
				Vector3 vector3 = new Vector3(0f - vector2.z, 0f, vector2.x);
				float num = SideHeight(vector + vector3 * 6f);
				float num2 = SideHeight(vector - vector3 * 6f);
				zone.wind = ((num < num2) ? (-vector3) : vector3);
				zones.Add(zone);
			}
		}
	}

	private float SideHeight(Vector3 p)
	{
		if (!world.InWalkArea(p.x, p.z))
		{
			return -1f;
		}
		return world.GroundAt(p.x, p.z);
	}

	private Zone ZoneAt(Vector3 p)
	{
		foreach (Zone zone in zones)
		{
			Vector3 vector = zone.p1 - zone.p0;
			vector.y = 0f;
			Vector3 lhs = p - zone.p0;
			lhs.y = 0f;
			float num = Mathf.Clamp01(Vector3.Dot(lhs, vector) / Mathf.Max(0.0001f, vector.sqrMagnitude));
			Vector3 vector2 = zone.p0 + vector * num;
			if (new Vector2(p.x - vector2.x, p.z - vector2.z).magnitude <= zone.radius)
			{
				return zone;
			}
		}
		return null;
	}

	private void Update()
	{
		if (!(player == null) && !(ui == null) && !(world == null) && !Features.classicAudit && !Suppress)
		{
			Tick(Time.deltaTime);
		}
	}

	public void Tick(float dt)
	{
		if (dt <= 0f || dt > 0.2f)
		{
			return;
		}
		Zone zone = (PlayerFree ? ZoneAt(player.transform.position) : null);
		if (zone != currentZone)
		{
			currentZone = zone;
			zoneTime = 0f;
			cooldown = 2f;
		}
		if (boostTimer > 0f)
		{
			Boost(dt);
		}
		if (currentZone == null)
		{
			EndGust(keepBoostFx: false);
			UpdateAudio(dt, 0f);
			return;
		}
		zoneTime += dt;
		WindDir = currentZone.wind;
		if (warnTimer > 0f)
		{
			warnTimer -= dt;
			UpdateAudio(dt, 0.35f + 0.4f * (1f - warnTimer / 1.5f));
			if (warnTimer <= 0f)
			{
				gustTimer = 3.2f;
				Imbalance = 0f;
			}
			return;
		}
		if (gustTimer > 0f)
		{
			Gust(dt);
			return;
		}
		UpdateAudio(dt, 0.12f);
		if (player.Speed > 0.2f || !string.IsNullOrEmpty(state.carrying))
		{
			cooldown -= dt;
		}
		if (cooldown <= 0f)
		{
			StartWarning();
		}
	}

	public void DebugStartGust()
	{
		if (currentZone != null)
		{
			StartWarning();
		}
	}

	private void StartWarning()
	{
		warnTimer = 1.5f;
		cooldown = UnityEngine.Random.Range(7f, 10f);
		EnsureFx();
		streaks.Play();
		SetWindExpression(ExprId.nervous);
		ShadowAndChimeSystem.PlayProceduralChime(440f, 0.18f);
	}

	private void Gust(float dt)
	{
		gustTimer -= dt;
		bool flag = DebugBrace || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || (HardwareAndSensorHub.Instance != null && HardwareAndSensorHub.Instance.ExternalWindStabilizeBoost > 0.15f);
		Vector3 vel = player.vel;
		vel.y = 0f;
		bool flag2 = vel.sqrMagnitude > 0.04f && Vector3.Dot(vel.normalized, WindDir) < -0.2f;
		if (flag)
		{
			Imbalance = Mathf.Max(0f, Imbalance - 0.6f * dt);
		}
		else
		{
			Imbalance = Mathf.Clamp01(Imbalance + (flag2 ? 0.28f : 0.55f) * dt);
		}
		SetWindCrouch(flag);
		if (flag)
		{
			player.vel *= 0.35f;
		}
		float num = (flag ? 0.25f : 1.6f);
		Vector3 position = player.transform.position + WindDir * (num * dt);
		if (world.Walkable(position.x, position.z))
		{
			position.y = world.GroundAt(position.x, position.z, position.y + 0.3f);
			player.transform.position = position;
		}
		UpdateAudio(dt, 1f);
		if (!flag && Imbalance > 0.55f)
		{
			SetWindExpression(ExprId.scared);
		}
		if (Imbalance >= 1f)
		{
			Fail();
		}
		else if (gustTimer <= 0f)
		{
			Survived++;
			EndGust(keepBoostFx: true);
			boostTimer = 3f;
			if (game != null)
			{
				game.SetExpression(ExprId.happy);
			}
			player.PlayEmote("hop");
			ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
			TideTownAchievementSystem.Unlock("tailwind");
			EnsureFx();
			trail.Play();
		}
	}

	private void Boost(float dt)
	{
		boostTimer -= dt;
		if (player.Speed > 0.3f && PlayerFree)
		{
			Vector3 vel = player.vel;
			vel.y = 0f;
			Vector3 position = player.transform.position + vel.normalized * (0.6f * player.Speed * dt);
			if (world.Walkable(position.x, position.z))
			{
				position.y = world.GroundAt(position.x, position.z, position.y + 0.3f);
				player.transform.position = position;
			}
		}
		if (trail != null)
		{
			trail.transform.position = player.transform.position + Vector3.up * 0.4f;
		}
		if (boostTimer <= 0f && trail != null)
		{
			trail.Stop();
		}
	}

	private void Fail()
	{
		Failures++;
		EndGust(keepBoostFx: false);
		cooldown = 9f;
		ShadowAndChimeSystem.PlayProceduralChime(185f, 0.45f);
		if (game != null)
		{
			game.SetExpression(ExprId.scared);
		}
		Vector3 position = player.transform.position;
		for (float num = 2.2f; num > 0.3f; num -= 0.4f)
		{
			Vector3 position2 = position + WindDir * num;
			if (world.Walkable(position2.x, position2.z))
			{
				position2.y = world.GroundAt(position2.x, position2.z, position.y + 0.5f);
				player.transform.position = position2;
				break;
			}
		}
		player.vel = Vector3.zero;
		player.PlayEmote("shake");
		if (director != null && !string.IsNullOrEmpty(state.carrying) && state.carrying != "matches")
		{
			director.DropCarriedNear(position + WindDir * 0.6f);
		}
	}

	private void EndGust(bool keepBoostFx)
	{
		warnTimer = 0f;
		gustTimer = 0f;
		Imbalance = 0f;
		SetWindCrouch(on: false);
		if (windExpression.HasValue && game != null && player.pose?.expr == windExpression.Value)
		{
			game.SetExpression(expressionBeforeWind);
		}
		windExpression = null;
		if (streaks != null)
		{
			streaks.Stop();
		}
		if (!keepBoostFx && trail != null && boostTimer <= 0f)
		{
			trail.Stop();
		}
	}

	private void SetWindExpression(ExprId expression)
	{
		if (!(game == null) && player.pose != null)
		{
			if (player.pose.expr != windExpression)
			{
				expressionBeforeWind = player.pose.expr;
			}
			windExpression = expression;
			game.SetExpression(expression);
		}
	}

	private void SetWindCrouch(bool on)
	{
		if (on || crouchedByWind)
		{
			crouchedByWind = on;
			player.SetStoryCrouch(on);
		}
	}

	private void EnsureFx()
	{
		if (!(streaks != null))
		{
			BakeryEffects bakeryEffects = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
			Material material = ((bakeryEffects != null) ? bakeryEffects.ParticleMaterial : null);
			GameObject gameObject = new GameObject("WindStreaks");
			streaks = gameObject.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main = streaks.main;
			main.loop = true;
			main.playOnAwake = false;
			main.startLifetime = 0.9f;
			main.startSpeed = 0f;
			main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.035f);
			main.startColor = new Color(1f, 1f, 1f, 0.38f);
			main.maxParticles = 220;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			ParticleSystem.EmissionModule emission = streaks.emission;
			emission.rateOverTime = 90f;
			ParticleSystem.ShapeModule shape = streaks.shape;
			shape.shapeType = ParticleSystemShapeType.Box;
			shape.scale = new Vector3(9f, 2.2f, 1f);
			ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = streaks.velocityOverLifetime;
			velocityOverLifetime.enabled = true;
			velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
			ParticleSystemRenderer component = gameObject.GetComponent<ParticleSystemRenderer>();
			component.renderMode = ParticleSystemRenderMode.Stretch;
			component.velocityScale = 0.12f;
			component.lengthScale = 2f;
			if (material != null)
			{
				component.sharedMaterial = material;
			}
			GameObject gameObject2 = new GameObject("TailwindTrail");
			trail = gameObject2.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main2 = trail.main;
			main2.loop = true;
			main2.playOnAwake = false;
			main2.startLifetime = 0.6f;
			main2.startSpeed = 0.2f;
			main2.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
			main2.startColor = new Color(1f, 0.95f, 0.8f, 0.6f);
			main2.simulationSpace = ParticleSystemSimulationSpace.World;
			ParticleSystem.EmissionModule emission2 = trail.emission;
			emission2.rateOverTime = 40f;
			ParticleSystem.ShapeModule shape2 = trail.shape;
			shape2.shapeType = ParticleSystemShapeType.Sphere;
			shape2.radius = 0.25f;
			ParticleSystemRenderer component2 = gameObject2.GetComponent<ParticleSystemRenderer>();
			if (material != null)
			{
				component2.sharedMaterial = material;
			}
		}
	}

	private void LateUpdate()
	{
		if (!(streaks == null) && !(player == null) && streaks.isPlaying)
		{
			Vector3 vector = -WindDir;
			streaks.transform.position = player.transform.position + vector * 6f + Vector3.up * 1f;
			streaks.transform.rotation = Quaternion.LookRotation(WindDir, Vector3.up);
			ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = streaks.velocityOverLifetime;
			velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(WindDir.x * 13f);
			velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f);
			velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(WindDir.z * 13f);
		}
	}

	private void UpdateAudio(float dt, float target)
	{
		if (windAudio == null)
		{
			if (target <= 0.01f)
			{
				return;
			}
			if (windClip == null)
			{
				windClip = MakeWindClip();
			}
			windAudio = base.gameObject.AddComponent<AudioSource>();
			windAudio.clip = windClip;
			windAudio.loop = true;
			windAudio.volume = 0f;
			windAudio.spatialBlend = 0f;
			windAudio.Play();
		}
		windAudio.volume = Mathf.MoveTowards(windAudio.volume, target * 0.5f, dt * 0.8f);
		windAudio.pitch = 0.85f + windAudio.volume * 0.5f;
	}

	private static AudioClip MakeWindClip()
	{
		float[] array = new float[88200];
		System.Random random = new System.Random(7);
		float num = 0f;
		float num2 = 0f;
		for (int i = 0; i < 88200; i++)
		{
			float num3 = (float)(random.NextDouble() * 2.0 - 1.0);
			num += (num3 - num) * 0.05f;
			num2 += (num - num2) * 0.08f;
			float num4 = (float)i / 88200f;
			float num5 = 0.65f + 0.35f * Mathf.Sin(num4 * MathF.PI * 2f * 2f) * Mathf.Sin(num4 * MathF.PI);
			float num6 = Mathf.Clamp01(Mathf.Min(num4, 1f - num4) * 40f);
			array[i] = num2 * 6f * num5 * num6;
		}
		AudioClip audioClip = AudioClip.Create("WindNoise", 88200, 1, 22050, stream: false);
		audioClip.SetData(array, 0);
		return audioClip;
	}
}
