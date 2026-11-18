using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TideFeel : MonoBehaviour
{
	private class Actor
	{
		public Transform t;

		public float radius;

		public float bodyR;

		public Vector3 last;

		public Vector3 vel;

		public bool has;

		public bool wet;

		public float press = 1f;

		public float slam;

		public float travel;

		public float idleRing;
	}

	private class Bush
	{
		public Vector3 pos;

		public float radius;

		public Color flower;

		public Color leaf;
	}

	public const int Res = 256;

	public const float Size = 25.6f;

	private const int MaxActors = 4;

	private const int MaxRipples = 16;

	private const float HeroRadius = 0.62f;

	private const float DogRadius = 0.42f;

	private static readonly int IdTrailSize = Shader.PropertyToID("_TrailSize");

	private static readonly int IdTrailDt = Shader.PropertyToID("_TrailDt");

	private static readonly int IdActorPos = Shader.PropertyToID("_ActorPos");

	private static readonly int IdActorVel = Shader.PropertyToID("_ActorVel");

	private static readonly int IdSpring = Shader.PropertyToID("_TideTrailSpring");

	private static readonly int IdAux = Shader.PropertyToID("_TideTrailAux");

	private static readonly int IdTrail = Shader.PropertyToID("_TideTrail");

	private static readonly int IdRipple = Shader.PropertyToID("_TideRipple");

	private static readonly int IdRippleB = Shader.PropertyToID("_TideRippleB");

	private static readonly int IdRippleFocus = Shader.PropertyToID("_TideRippleFocus");

	private static readonly int IdWade = Shader.PropertyToID("_TideWade");

	private static readonly int IdTime = Shader.PropertyToID("_TideTime");

	private bool off;

	private Material trailMat;

	private readonly RenderTexture[] spring = new RenderTexture[2];

	private readonly RenderTexture[] aux = new RenderTexture[2];

	private int cur;

	private readonly Actor hero = new Actor
	{
		radius = 0.62f,
		bodyR = 0.21f
	};

	private readonly Actor dog = new Actor
	{
		radius = 0.42f,
		bodyR = 0.16f
	};

	private EggboyPlayer player;

	private DogFollower dogFollower;

	private float lookAt;

	private bool wasGrounded = true;

	private readonly bool[] wasStance = new bool[2] { true, true };

	private float lastStepRing = -9f;

	private readonly Vector4[] actorPos = new Vector4[4];

	private readonly Vector4[] actorVel = new Vector4[4];

	private readonly Vector4[] ripple = new Vector4[16];

	private readonly Vector4[] rippleB = new Vector4[16];

	private readonly Vector4[] wade = new Vector4[4];

	private int rippleNext;

	private float rippleUntil = -9f;

	private ParticleSystem drops;

	private ParticleSystem petals;

	private List<Bush> bushes;

	private float petalDebt;

	private bool wasInBush;

	private System.Random rnd = new System.Random(20261114);

	private Texture2D probeTex;

	public static TideFeel Main { get; private set; }

	public bool TrailOn { get; private set; }

	public string TrailFormat { get; private set; } = "none";


	public int Ripples { get; private set; }

	public int Splashes { get; private set; }

	public int Petals { get; private set; }

	public int Slams { get; private set; }

	public float HeroDepth { get; private set; }

	public int BushCount
	{
		get
		{
			if (bushes == null)
			{
				return 0;
			}
			return bushes.Count;
		}
	}

	public bool HeroInBush { get; private set; }

	public RenderTexture SpringTexture
	{
		get
		{
			if (!TrailOn)
			{
				return null;
			}
			return spring[cur];
		}
	}

	public RenderTexture SlowTexture
	{
		get
		{
			if (!TrailOn)
			{
				return null;
			}
			return aux[cur];
		}
	}

	private void Awake()
	{
		Main = this;
		off = Array.IndexOf(Environment.GetCommandLineArgs(), "-nofeel") >= 0;
		for (int i = 0; i < 16; i++)
		{
			ripple[i] = new Vector4(0f, 0f, -999f, 0f);
			rippleB[i] = new Vector4(1f, 0f, 0f, 0f);
		}
		PushWaterGlobals(Vector3.zero);
		Shader.SetGlobalVector(IdTrail, Vector4.zero);
		if (off)
		{
			Debug.Log("FEEL off (-nofeel)");
			return;
		}
		SetupTrail();
		drops = MakeParticles("FeelDrops", 0f, 1.7f, 160);
		petals = MakeParticles("FeelPetals", 1f, 0.32f, 120);
	}

	private void OnDestroy()
	{
		if (Main == this)
		{
			Main = null;
		}
		Shader.SetGlobalVector(IdTrail, Vector4.zero);
		for (int i = 0; i < 2; i++)
		{
			if (spring[i] != null)
			{
				spring[i].Release();
			}
			if (aux[i] != null)
			{
				aux[i].Release();
			}
		}
	}

	private void SetupTrail()
	{
		Shader shader = Resources.Load<Shader>("TideTrail");
		RenderTextureFormat renderTextureFormat;
		if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
		{
			renderTextureFormat = RenderTextureFormat.ARGBHalf;
		}
		else
		{
			if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
			{
				Debug.LogWarning("FEEL 这台机器不支持浮点渲染贴图，花草的痕迹关掉");
				return;
			}
			renderTextureFormat = RenderTextureFormat.ARGBFloat;
		}
		if (shader == null || !shader.isSupported)
		{
			Debug.LogWarning("FEEL 找不到 TideTrail 着色器，花草的痕迹关掉");
			return;
		}
		trailMat = new Material(shader)
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		for (int i = 0; i < 2; i++)
		{
			spring[i] = NewState("TideTrailSpring" + i, renderTextureFormat);
			aux[i] = NewState("TideTrailAux" + i, renderTextureFormat);
		}
		TrailFormat = renderTextureFormat.ToString();
		TrailOn = true;
		Debug.Log($"FEEL trail {256}x{256} {renderTextureFormat} size={25.6f}m");
	}

	private static RenderTexture NewState(string name, RenderTextureFormat fmt)
	{
		RenderTexture renderTexture = new RenderTexture(256, 256, 0, fmt, RenderTextureReadWrite.Linear);
		renderTexture.name = name;
		renderTexture.wrapMode = TextureWrapMode.Repeat;
		renderTexture.filterMode = FilterMode.Bilinear;
		renderTexture.useMipMap = false;
		renderTexture.autoGenerateMips = false;
		renderTexture.Create();
		Clear(renderTexture);
		return renderTexture;
	}

	private static void Clear(RenderTexture rt)
	{
		RenderTexture active = RenderTexture.active;
		RenderTexture.active = rt;
		GL.Clear(clearDepth: false, clearColor: true, Color.clear);
		RenderTexture.active = active;
	}

	private void StepTrail(float dt, Vector3 focus)
	{
		if (!TrailOn)
		{
			return;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!spring[i].IsCreated())
			{
				spring[i].Create();
				Clear(spring[i]);
			}
			if (!aux[i].IsCreated())
			{
				aux[i].Create();
				Clear(aux[i]);
			}
		}
		trailMat.SetFloat(IdTrailSize, 25.6f);
		trailMat.SetFloat(IdTrailDt, dt);
		trailMat.SetVectorArray(IdActorPos, actorPos);
		trailMat.SetVectorArray(IdActorVel, actorVel);
		RenderTexture active = RenderTexture.active;
		Graphics.Blit(spring[cur], spring[1 - cur], trailMat, 0);
		Graphics.Blit(aux[cur], aux[1 - cur], trailMat, 1);
		RenderTexture.active = active;
		cur = 1 - cur;
		Shader.SetGlobalTexture(IdSpring, spring[cur]);
		Shader.SetGlobalTexture(IdAux, aux[cur]);
		Shader.SetGlobalVector(IdTrail, new Vector4(5f / 128f, focus.x, focus.z, 1f));
	}

	public Vector2 Probe(RenderTexture rt, Vector3 world)
	{
		if (rt == null)
		{
			return Vector2.zero;
		}
		if (probeTex == null)
		{
			probeTex = new Texture2D(1, 1, TextureFormat.RGBAFloat, mipChain: false, linear: true);
		}
		int num = Mathf.FloorToInt(Mathf.Repeat(world.x / 25.6f, 1f) * 256f) % 256;
		int num2 = Mathf.FloorToInt(Mathf.Repeat(world.z / 25.6f, 1f) * 256f) % 256;
		RenderTexture active = RenderTexture.active;
		RenderTexture.active = rt;
		probeTex.ReadPixels(new Rect(num, num2, 1f, 1f), 0, 0);
		probeTex.Apply();
		RenderTexture.active = active;
		Color pixel = probeTex.GetPixel(0, 0);
		return new Vector2(pixel.r, pixel.g);
	}

	private void LateUpdate()
	{
		Shader.SetGlobalFloat(IdTime, Time.time);
		if (off)
		{
			return;
		}
		FindActors();
		float num = Mathf.Min(Time.deltaTime, 0.05f);
		if (num <= 0f)
		{
			return;
		}
		bool flag = hero.t != null && hero.t.gameObject.activeInHierarchy;
		bool flag2 = dog.t != null && dog.t.gameObject.activeInHierarchy && (dogFollower == null || dogFollower.Visible);
		Track(hero, flag, num);
		Track(dog, flag2, num);
		hero.slam = 0f;
		bool flag3 = player == null || player.grounded;
		if (flag)
		{
			hero.press = Mathf.MoveTowards(hero.press, flag3 ? 1f : 0f, num * 8f);
			if (flag3 && !wasGrounded && player != null)
			{
				hero.slam = Mathf.Clamp(player.jump.impact / 4f, 0.45f, 1.3f);
				Slams++;
			}
		}
		dog.press = 0.7f;
		dog.slam = 0f;
		for (int i = 0; i < 4; i++)
		{
			actorPos[i] = Vector4.zero;
			actorVel[i] = Vector4.zero;
		}
		if (flag)
		{
			Fill(0, hero);
		}
		if (flag2)
		{
			Fill(1, dog);
		}
		Vector3 focus = (flag ? hero.last : (flag2 ? dog.last : Vector3.zero));
		StepTrail(num, focus);
		Water(flag, flag2, flag3, num);
		if (flag)
		{
			Flora(flag3, num);
		}
		PushWaterGlobals(focus);
		wasGrounded = flag3;
	}

	private void FindActors()
	{
		if ((hero.t != null && dog.t != null) || Time.unscaledTime < lookAt)
		{
			return;
		}
		lookAt = Time.unscaledTime + 1f;
		if (hero.t == null)
		{
			GameObject gameObject = GameObject.Find("EGGBOY");
			if (gameObject != null)
			{
				hero.t = gameObject.transform;
				player = gameObject.GetComponent<EggboyPlayer>();
				hero.has = false;
			}
		}
		if (dog.t == null)
		{
			dogFollower = UnityEngine.Object.FindObjectOfType<DogFollower>();
			if (dogFollower != null)
			{
				dog.t = dogFollower.transform;
				dog.has = false;
			}
		}
	}

	private static void Track(Actor a, bool on, float dt)
	{
		if (!on)
		{
			a.has = false;
			a.vel = Vector3.zero;
			return;
		}
		Vector3 position = a.t.position;
		if (!a.has || (position - a.last).sqrMagnitude > 4f)
		{
			a.vel = Vector3.zero;
			a.has = true;
			a.travel = 0f;
		}
		else
		{
			Vector3 vector = position - a.last;
			a.vel = Vector3.Lerp(a.vel, vector / dt, Mathf.Min(1f, dt * 18f));
			a.travel += new Vector2(vector.x, vector.z).magnitude;
		}
		a.last = position;
	}

	private void Fill(int i, Actor a)
	{
		actorPos[i] = new Vector4(a.last.x, a.last.y, a.last.z, a.radius);
		actorVel[i] = new Vector4(a.vel.x, a.vel.z, a.press, a.slam);
	}

	private void Water(bool heroOn, bool dogOn, bool grounded, float dt)
	{
		float staticY = WaterLevel.StaticY;
		for (int i = 0; i < 4; i++)
		{
			wade[i] = Vector4.zero;
		}
		HeroDepth = 0f;
		if (heroOn)
		{
			float num = staticY - hero.last.y;
			bool flag = num > 0.02f && num < 0.7f;
			float num2 = (flag ? Mathf.Clamp01(num / 0.15f) : 0f);
			float magnitude = new Vector2(hero.vel.x, hero.vel.z).magnitude;
			if (flag && grounded)
			{
				HeroDepth = num;
			}
			if (player != null)
			{
				player.speedScale = Mathf.MoveTowards(player.speedScale, (flag && grounded) ? Mathf.Lerp(1f, 0.74f, num2) : 1f, dt * 2.5f);
			}
			if (flag)
			{
				Vector3 last = hero.last;
				if (grounded)
				{
					wade[0] = new Vector4(last.x, last.z, hero.bodyR, num2 * (0.55f + 0.45f * Mathf.Clamp01(magnitude / 1.2f)));
				}
				if (grounded && !wasGrounded)
				{
					float num3 = ((player != null) ? Mathf.Clamp(player.jump.impact / 4f, 0.6f, 1.3f) : 1f);
					AddRipple(last, 1.25f * num3, 2.1f, 0.85f, 0.12f, 0f);
					AddRipple(last, 0.7f * num3, 1.9f, 0.6f, 0.08f, 0.22f);
					Splash(last, staticY, 14 + Mathf.RoundToInt(14f * num3), 1.5f * num3, hero.vel);
					hero.idleRing = 2.4f;
				}
				else if (!grounded && wasGrounded)
				{
					Splash(last, staticY, 7, 0.9f, hero.vel);
					AddRipple(last, 0.55f, 1.3f, 0.5f, 0.15f, 0f);
				}
				else if (grounded && !hero.wet)
				{
					AddRipple(last, 0.7f, 1.5f, 0.55f, 0.15f, 0f);
					Splash(last, staticY, 6, 0.8f, hero.vel);
				}
				if (grounded && player != null && player.gait != null)
				{
					for (int j = 0; j < 2; j++)
					{
						bool stance = player.gait.feet[j].stance;
						if (stance && !wasStance[j] && magnitude > 0.2f && Time.time - lastStepRing > 0.2f)
						{
							lastStepRing = Time.time;
							float num4 = Mathf.Clamp01((magnitude - 0.6f) / 1.2f);
							Vector3 at = last + hero.t.right * ((j == 0) ? (-0.09f) : 0.09f) + hero.t.forward * player.gait.feet[j].fwd;
							AddRipple(at, 0.8f + 0.3f * num4, 1.6f, 0.5f + 0.2f * num4, 0.05f, 0f);
							Splash(at, staticY, 3 + Mathf.RoundToInt(5f * num4), 0.55f + 0.5f * num4, hero.vel);
							hero.idleRing = 1.6f;
						}
						wasStance[j] = stance;
					}
				}
				if (grounded && magnitude < 0.15f)
				{
					hero.idleRing -= dt;
					if (hero.idleRing <= 0f)
					{
						hero.idleRing = 1.7f + (float)rnd.NextDouble() * 0.8f;
						AddRipple(last, 0.6f, 2.4f, 0.32f, hero.bodyR, 0f);
					}
				}
			}
			hero.wet = flag;
		}
		else if (player != null)
		{
			player.speedScale = 1f;
		}
		if (!dogOn)
		{
			return;
		}
		float num5 = staticY - dog.last.y;
		bool flag2 = num5 > 0.02f && num5 < 0.7f;
		if (flag2)
		{
			float magnitude2 = new Vector2(dog.vel.x, dog.vel.z).magnitude;
			wade[1] = new Vector4(dog.last.x, dog.last.z, dog.bodyR, Mathf.Clamp01(num5 / 0.12f) * (0.5f + 0.5f * Mathf.Clamp01(magnitude2 / 1.5f)));
			if (!dog.wet)
			{
				dog.travel = 0.3f;
			}
			if (dog.travel >= 0.3f)
			{
				dog.travel = 0f;
				AddRipple(dog.last, 0.45f, 1.1f, 0.5f, 0.05f, 0f);
				Splash(dog.last, staticY, 2, 0.5f, dog.vel);
			}
		}
		dog.wet = flag2;
	}

	public void AddRipple(Vector3 at, float strength, float life, float speed, float startRadius, float delay)
	{
		float num = Time.time + delay;
		ripple[rippleNext] = new Vector4(at.x, at.z, num, strength);
		rippleB[rippleNext] = new Vector4(life, speed, startRadius, 0f);
		rippleNext = (rippleNext + 1) % 16;
		rippleUntil = Mathf.Max(rippleUntil, num + life);
		Ripples++;
	}

	private void PushWaterGlobals(Vector3 focus)
	{
		bool flag = Time.time < rippleUntil;
		for (int i = 0; i < 4; i++)
		{
			if (flag)
			{
				break;
			}
			flag = wade[i].w > 0f;
		}
		Shader.SetGlobalVectorArray(IdRipple, ripple);
		Shader.SetGlobalVectorArray(IdRippleB, rippleB);
		Shader.SetGlobalVectorArray(IdWade, wade);
		Shader.SetGlobalVector(IdRippleFocus, new Vector4(focus.x, focus.z, 16f, flag ? 1f : 0f));
	}

	private ParticleSystem MakeParticles(string name, float shape, float gravity, int max)
	{
		Shader shader = Resources.Load<Shader>("TideFeelParticle");
		if (shader == null)
		{
			Debug.LogWarning("FEEL 找不到 TideFeelParticle 着色器，" + name + " 不出");
			return null;
		}
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		ParticleSystem particleSystem = gameObject.AddComponent<ParticleSystem>();
		particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		ParticleSystem.MainModule main = particleSystem.main;
		main.loop = true;
		main.playOnAwake = false;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.maxParticles = max;
		main.gravityModifier = gravity;
		main.startSpeed = 0f;
		ParticleSystem.EmissionModule emission = particleSystem.emission;
		emission.enabled = false;
		ParticleSystem.ShapeModule shape2 = particleSystem.shape;
		shape2.enabled = false;
		ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
		colorOverLifetime.enabled = true;
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(Color.white, 0f),
			new GradientColorKey(Color.white, 1f)
		}, new GradientAlphaKey[3]
		{
			new GradientAlphaKey(1f, 0f),
			new GradientAlphaKey(1f, 0.6f),
			new GradientAlphaKey(0f, 1f)
		});
		colorOverLifetime.color = gradient;
		if (shape > 0.5f)
		{
			ParticleSystem.LimitVelocityOverLifetimeModule limitVelocityOverLifetime = particleSystem.limitVelocityOverLifetime;
			limitVelocityOverLifetime.enabled = true;
			limitVelocityOverLifetime.drag = 1.6f;
			limitVelocityOverLifetime.limit = 6f;
			limitVelocityOverLifetime.dampen = 0f;
			ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = particleSystem.rotationOverLifetime;
			rotationOverLifetime.enabled = true;
			rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-3.2f, 3.2f);
		}
		else
		{
			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
			sizeOverLifetime.enabled = true;
			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.45f)));
		}
		ParticleSystemRenderer component = gameObject.GetComponent<ParticleSystemRenderer>();
		component.sharedMaterial = new Material(shader)
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		component.sharedMaterial.SetFloat("_Shape", shape);
		component.renderMode = ParticleSystemRenderMode.Billboard;
		component.shadowCastingMode = ShadowCastingMode.Off;
		component.receiveShadows = false;
		component.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
		{
			ParticleSystemVertexStream.Position,
			ParticleSystemVertexStream.Color,
			ParticleSystemVertexStream.UV,
			ParticleSystemVertexStream.StableRandomX
		});
		particleSystem.Play();
		return particleSystem;
	}

	private float R(float a, float b)
	{
		return a + (float)rnd.NextDouble() * (b - a);
	}

	private void Splash(Vector3 at, float waterY, int count, float power, Vector3 carry)
	{
		if (!(drops == null))
		{
			Splashes++;
			for (int i = 0; i < count; i++)
			{
				float f = R(0f, MathF.PI * 2f);
				float num = R(0.25f, 0.95f) * power;
				ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
				emitParams.position = new Vector3(at.x + Mathf.Cos(f) * 0.06f, waterY + 0.02f, at.z + Mathf.Sin(f) * 0.06f);
				emitParams.velocity = new Vector3(Mathf.Cos(f) * num + carry.x * 0.35f, R(1.1f, 2.3f) * power, Mathf.Sin(f) * num + carry.z * 0.35f);
				emitParams.startSize = R(0.022f, 0.05f) * Mathf.Lerp(0.9f, 1.25f, Mathf.Clamp01(power - 0.5f));
				emitParams.startLifetime = R(0.3f, 0.55f);
				emitParams.startColor = new Color(0.86f, 0.97f, 1f, 0.92f);
				ParticleSystem.EmitParams emitParams2 = emitParams;
				drops.Emit(emitParams2, 1);
			}
		}
	}

	private void BuildBushes()
	{
		bushes = new List<Bush>();
		GameObject gameObject = GameObject.Find("Bushes");
		if (gameObject == null)
		{
			Debug.Log("FEEL 场景里没有 Bushes，不出花瓣");
			return;
		}
		foreach (Transform item in gameObject.transform)
		{
			MeshRenderer component = item.GetComponent<MeshRenderer>();
			if (!(component == null))
			{
				Material[] sharedMaterials = component.sharedMaterials;
				Bush bush = new Bush
				{
					pos = item.position,
					radius = 0.55f * Mathf.Max(item.lossyScale.x, item.lossyScale.z),
					leaf = new Color(0.36f, 0.6f, 0.22f),
					flower = new Color(0.98f, 0.96f, 0.9f)
				};
				if (sharedMaterials.Length != 0 && sharedMaterials[0] != null && sharedMaterials[0].HasProperty("_BaseColor"))
				{
					bush.leaf = sharedMaterials[0].GetColor("_BaseColor");
				}
				if (sharedMaterials.Length > 1 && sharedMaterials[1] != null && sharedMaterials[1].HasProperty("_BaseColor"))
				{
					bush.flower = sharedMaterials[1].GetColor("_BaseColor");
				}
				bushes.Add(bush);
			}
		}
		Debug.Log($"FEEL bushes {bushes.Count}");
	}

	private void Flora(bool grounded, float dt)
	{
		if (petals == null)
		{
			return;
		}
		if (bushes == null)
		{
			BuildBushes();
		}
		Vector3 last = hero.last;
		Bush bush = null;
		float num = float.MaxValue;
		for (int i = 0; i < bushes.Count; i++)
		{
			Bush bush2 = bushes[i];
			float num2 = last.x - bush2.pos.x;
			float num3 = last.z - bush2.pos.z;
			float num4 = num2 * num2 + num3 * num3;
			float num5 = bush2.radius + 0.22f;
			if (!(num4 > num5 * num5) && !(Mathf.Abs(last.y - bush2.pos.y) > 1f) && num4 < num)
			{
				num = num4;
				bush = bush2;
			}
		}
		HeroInBush = bush != null;
		if (bush != null)
		{
			float magnitude = new Vector2(hero.vel.x, hero.vel.z).magnitude;
			if (!wasInBush && magnitude > 0.2f)
			{
				EmitPetals(bush, last, 5);
			}
			if (hero.slam > 0f)
			{
				EmitPetals(bush, last, 10);
			}
			if (grounded && magnitude > 0.25f)
			{
				petalDebt += (3.5f + 6f * Mathf.Clamp01(magnitude / 2f)) * dt;
				int num6 = Mathf.FloorToInt(petalDebt);
				if (num6 > 0)
				{
					petalDebt -= num6;
					EmitPetals(bush, last, num6);
				}
			}
		}
		wasInBush = bush != null;
	}

	private void EmitPetals(Bush b, Vector3 heroPos, int count)
	{
		for (int i = 0; i < count; i++)
		{
			float f = R(0f, MathF.PI * 2f);
			float num = R(0.1f, 0.4f);
			Vector3 vector = new Vector3(b.pos.x - heroPos.x, 0f, b.pos.z - heroPos.z) * 0.35f;
			Color color = ((rnd.NextDouble() < 0.7) ? b.flower : b.leaf);
			float num2 = R(0.9f, 1.08f);
			ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
			emitParams.position = heroPos + vector + new Vector3(Mathf.Cos(f) * num, R(0.22f, 0.62f), Mathf.Sin(f) * num);
			emitParams.velocity = new Vector3(hero.vel.x * 0.45f + R(-0.55f, 0.55f), R(0.5f, 1.35f), hero.vel.z * 0.45f + R(-0.55f, 0.55f));
			emitParams.startSize = R(0.05f, 0.085f);
			emitParams.startLifetime = R(1.2f, 2f);
			emitParams.startColor = new Color(color.r * num2, color.g * num2, color.b * num2, 1f);
			emitParams.rotation = R(0f, 360f);
			ParticleSystem.EmitParams emitParams2 = emitParams;
			petals.Emit(emitParams2, 1);
			Petals++;
		}
	}
}
