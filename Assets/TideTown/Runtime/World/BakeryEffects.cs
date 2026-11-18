using UnityEngine;

public sealed class BakeryEffects : MonoBehaviour
{
	public Vector3 chimneyLocalPosition = new Vector3(0.22f, 3.7f, 0f);

	public Vector3 fireLocalPosition = new Vector3(0f, 0.8f, -0.56f);

	public Vector3 mouthLocal = new Vector3(0f, 0.75f, -0.62f);

	public Vector3 chamberFloorLocal = new Vector3(0f, 0.58f, -0.4f);

	public Vector2 mouthSize = new Vector2(0.4f, 0.35f);

	public Material ParticleMaterial;

	private GameObject loadedWood;

	private bool loadedWet;

	private Light fireLight;

	private ParticleSystem fire;

	private ParticleSystem smoke;

	private ParticleSystemRenderer fireRenderer;

	private float t;

	private float boostPulse;

	private ParticleSystem wetPuff;

	private static Texture2D soft;

	public bool Lit { get; private set; }

	public int BellowsStage { get; private set; }

	public bool HasSmoke
	{
		get
		{
			if (smoke != null)
			{
				return smoke.isPlaying;
			}
			return false;
		}
	}

	public int FireCount
	{
		get
		{
			if (!(fire != null))
			{
				return 0;
			}
			return fire.particleCount;
		}
	}

	public int SmokeCount
	{
		get
		{
			if (!(smoke != null))
			{
				return 0;
			}
			return smoke.particleCount;
		}
	}

	public bool SmokeVisible
	{
		get
		{
			if (smoke != null)
			{
				return smoke.GetComponent<ParticleSystemRenderer>().isVisible;
			}
			return false;
		}
	}

	public Vector3 FireMouth => base.transform.TransformPoint(fireLocalPosition);

	public Vector3 LoadPoint => base.transform.TransformPoint(mouthLocal);

	public bool HasLoadedWood
	{
		get
		{
			if (loadedWood != null)
			{
				return loadedWood.activeSelf;
			}
			return false;
		}
	}

	public bool LoadedWoodWet
	{
		get
		{
			if (HasLoadedWood)
			{
				return loadedWet;
			}
			return false;
		}
	}

	public int WetPuffCount { get; private set; }

	public void SetLoadedWood(bool loaded, bool wet)
	{
		if (!loaded)
		{
			if (loadedWood != null)
			{
				loadedWood.SetActive(value: false);
			}
			return;
		}
		if (loadedWood == null || loadedWet != wet)
		{
			if (loadedWood != null)
			{
				Object.Destroy(loadedWood);
			}
			loadedWood = BuildLoadedWood(wet);
			loadedWet = wet;
		}
		if (loadedWood != null)
		{
			loadedWood.SetActive(value: true);
		}
	}

	private GameObject BuildLoadedWood(bool wet)
	{
		IslandWorld islandWorld = Object.FindObjectOfType<IslandWorld>();
		GameObject gameObject = ((islandWorld != null) ? islandWorld.woodModel : null);
		if (gameObject == null)
		{
			return null;
		}
		GameObject gameObject2 = Object.Instantiate(gameObject, base.transform, worldPositionStays: false);
		gameObject2.name = "OvenLoadedWood";
		gameObject2.SetActive(value: true);
		Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Object.Destroy(componentsInChildren[i]);
		}
		Renderer[] componentsInChildren2 = gameObject2.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].enabled = true;
		}
		gameObject2.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
		gameObject2.transform.localScale = Vector3.one;
		Bounds bounds = HandCarry.LocalBounds(gameObject2, base.transform);
		float a = mouthSize.x * 0.6f / Mathf.Max(0.01f, bounds.size.x);
		float a2 = mouthSize.y * 0.55f / Mathf.Max(0.01f, bounds.size.y);
		float b = mouthSize.x * 0.95f / Mathf.Max(0.01f, bounds.size.z);
		float num = Mathf.Min(1f, Mathf.Min(a, Mathf.Min(a2, b)));
		Vector3 vector = chamberFloorLocal + Vector3.up * 0.008f;
		vector.z = mouthLocal.z + Mathf.Clamp(mouthSize.x * 0.07f, 0.015f, 0.035f);
		gameObject2.transform.localScale = Vector3.one * num;
		gameObject2.transform.localPosition = vector - new Vector3(bounds.center.x, bounds.min.y, bounds.min.z) * num;
		if (wet)
		{
			componentsInChildren2 = gameObject2.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in componentsInChildren2)
			{
				Material[] materials = renderer.materials;
				Material[] array = materials;
				foreach (Material material in array)
				{
					string[] array2 = new string[2] { "_BaseColor", "_Color" };
					foreach (string text in array2)
					{
						if (material.HasProperty(text))
						{
							material.SetColor(text, material.GetColor(text) * new Color(0.48f, 0.5f, 0.56f, 1f));
						}
					}
				}
				renderer.materials = materials;
			}
		}
		return gameObject2;
	}

	public bool FireVisibleFrom(Camera camera)
	{
		if (!Lit || FireCount == 0 || !fireRenderer.isVisible)
		{
			return false;
		}
		Vector3 vector = camera.WorldToViewportPoint(FireMouth);
		if (vector.z > camera.nearClipPlane && vector.x > 0f && vector.x < 1f && vector.y > 0f)
		{
			return vector.y < 1f;
		}
		return false;
	}

	public void Light()
	{
		if (!Lit)
		{
			Lit = true;
			BellowsStage = 0;
			BuildFire();
			BuildSmoke();
		}
	}

	public int BoostBellows()
	{
		if (!Lit)
		{
			Light();
		}
		if (BellowsStage < 3)
		{
			BellowsStage++;
		}
		boostPulse = 1.25f;
		if (fire != null)
		{
			ParticleSystem.EmissionModule emission = fire.emission;
			emission.rateOverTime = 55f + (float)BellowsStage * 32f;
			fire.Emit(18 + BellowsStage * 8);
		}
		if (smoke != null)
		{
			ParticleSystem.EmissionModule emission2 = smoke.emission;
			emission2.rateOverTime = 7f + (float)BellowsStage * 3.5f;
			smoke.Emit(6 + BellowsStage * 4);
		}
		if (fireLight != null)
		{
			fireLight.range = 4.5f + (float)BellowsStage * 1.4f;
		}
		return BellowsStage;
	}

	public void WetPuff()
	{
		WetPuffCount++;
		if (wetPuff == null)
		{
			GameObject gameObject = new GameObject("OvenWetPuff");
			gameObject.transform.SetParent(base.transform, worldPositionStays: false);
			gameObject.transform.localPosition = fireLocalPosition + new Vector3(0f, 0.04f, 0f);
			gameObject.transform.localRotation = Quaternion.Euler(60f, 0f, 0f) * Quaternion.LookRotation(Vector3.back);
			wetPuff = gameObject.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main = wetPuff.main;
			main.loop = false;
			main.playOnAwake = false;
			main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
			main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
			main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
			main.startColor = new Color(0.93f, 0.94f, 0.95f, 0.8f);
			main.maxParticles = 60;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			ParticleSystem.EmissionModule emission = wetPuff.emission;
			emission.rateOverTime = 0f;
			ParticleSystem.ShapeModule shape = wetPuff.shape;
			shape.shapeType = ParticleSystemShapeType.Cone;
			shape.radius = 0.14f;
			shape.angle = 28f;
			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = wetPuff.sizeOverLifetime;
			sizeOverLifetime.enabled = true;
			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime = wetPuff.colorOverLifetime;
			colorOverLifetime.enabled = true;
			Gradient gradient = new Gradient();
			gradient.SetKeys(new GradientColorKey[2]
			{
				new GradientColorKey(Color.white, 0f),
				new GradientColorKey(new Color(0.8f, 0.82f, 0.84f), 1f)
			}, new GradientAlphaKey[3]
			{
				new GradientAlphaKey(0.8f, 0f),
				new GradientAlphaKey(0.5f, 0.5f),
				new GradientAlphaKey(0f, 1f)
			});
			colorOverLifetime.color = gradient;
			gameObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMat(ParticleMaterial);
		}
		wetPuff.Emit(26);
	}

	private void BuildFire()
	{
		GameObject gameObject = new GameObject("OvenFire");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.transform.position = FireMouth;
		fire = gameObject.AddComponent<ParticleSystem>();
		ParticleSystem.MainModule main = fire.main;
		main.loop = true;
		float num = Mathf.Max(0.12f, mouthLocal.y + mouthSize.y * 0.5f - fireLocalPosition.y);
		main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(num * 0.9f, num * 1.5f);
		main.startSize = new ParticleSystem.MinMaxCurve(mouthSize.x * 0.35f, mouthSize.x * 0.6f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.45f), new Color(1f, 0.42f, 0.1f));
		main.maxParticles = 90;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		ParticleSystem.EmissionModule emission = fire.emission;
		emission.rateOverTime = 55f;
		ParticleSystem.ShapeModule shape = fire.shape;
		shape.shapeType = ParticleSystemShapeType.Cone;
		shape.radius = Mathf.Min(0.1f, mouthSize.x * 0.28f);
		shape.angle = 10f;
		shape.rotation = new Vector3(-90f, 0f, 0f);
		ParticleSystem.ColorOverLifetimeModule colorOverLifetime = fire.colorOverLifetime;
		colorOverLifetime.enabled = true;
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f),
			new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1f)
		}, new GradientAlphaKey[3]
		{
			new GradientAlphaKey(1f, 0f),
			new GradientAlphaKey(0.85f, 0.6f),
			new GradientAlphaKey(0f, 1f)
		});
		colorOverLifetime.color = gradient;
		fireRenderer = gameObject.GetComponent<ParticleSystemRenderer>();
		fireRenderer.sharedMaterial = SoftMat(ParticleMaterial);
		GameObject gameObject2 = new GameObject("OvenLight");
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject2.transform.localPosition = mouthLocal + Vector3.back * 0.18f;
		fireLight = gameObject2.AddComponent<Light>();
		fireLight.type = LightType.Point;
		fireLight.color = new Color(1f, 0.62f, 0.28f);
		fireLight.range = 4.5f;
		fireLight.intensity = 2.2f;
		fireLight.shadows = LightShadows.None;
		fire.Play();
		Debug.Log("BAKERYFIRE lit");
	}

	private void BuildSmoke()
	{
		GameObject gameObject = new GameObject("ChimneySmoke");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.transform.localPosition = chimneyLocalPosition;
		smoke = gameObject.AddComponent<ParticleSystem>();
		ParticleSystem.MainModule main = smoke.main;
		main.loop = true;
		main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 16f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
		main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 2.4f);
		main.startColor = new Color(0.76f, 0.76f, 0.74f, 0.72f);
		main.maxParticles = 170;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		ParticleSystem.EmissionModule emission = smoke.emission;
		emission.rateOverTime = 7f;
		ParticleSystem.ShapeModule shape = smoke.shape;
		shape.shapeType = ParticleSystemShapeType.Cone;
		shape.radius = 0.18f;
		shape.angle = 8f;
		ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = smoke.velocityOverLifetime;
		velocityOverLifetime.enabled = true;
		velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
		velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.65f, -0.35f);
		velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
		velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
		ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = smoke.sizeOverLifetime;
		sizeOverLifetime.enabled = true;
		AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.5f, 1.7f), new Keyframe(1f, 3.5f));
		sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
		ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smoke.colorOverLifetime;
		colorOverLifetime.enabled = true;
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(new Color(0.82f, 0.82f, 0.8f), 0f),
			new GradientColorKey(new Color(0.66f, 0.66f, 0.64f), 1f)
		}, new GradientAlphaKey[3]
		{
			new GradientAlphaKey(0.72f, 0f),
			new GradientAlphaKey(0.55f, 0.45f),
			new GradientAlphaKey(0f, 1f)
		});
		colorOverLifetime.color = gradient;
		gameObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMat(ParticleMaterial);
		smoke.Play();
	}

	private static Material SoftMat(Material baseMat)
	{
		Material material = baseMat;
		if (material == null)
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
			if (shader == null)
			{
				shader = Shader.Find("Particles/Standard Unlit");
			}
			if (shader == null)
			{
				shader = Shader.Find("Sprites/Default");
			}
			material = new Material(shader);
		}
		Texture2D value = SoftCircle();
		if (material.HasProperty("_BaseMap"))
		{
			material.SetTexture("_BaseMap", value);
		}
		if (material.HasProperty("_MainTex"))
		{
			material.SetTexture("_MainTex", value);
		}
		return material;
	}

	private static Texture2D SoftCircle()
	{
		if (soft != null)
		{
			return soft;
		}
		soft = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = ((float)j + 0.5f) / 64f * 2f - 1f;
				float num2 = ((float)i + 0.5f) / 64f * 2f - 1f;
				float num3 = Mathf.Clamp01(1f - Mathf.Sqrt(num * num + num2 * num2));
				soft.SetPixel(j, i, new Color(1f, 1f, 1f, num3 * num3));
			}
		}
		soft.Apply();
		return soft;
	}

	private void Update()
	{
		if (!(fireLight == null))
		{
			t += Time.deltaTime;
			if (boostPulse > 0f)
			{
				boostPulse = Mathf.Max(0f, boostPulse - Time.deltaTime);
			}
			float num = 1.9f + (float)BellowsStage * 0.45f + boostPulse * 1.1f;
			fireLight.intensity = num + Mathf.PerlinNoise(t * 7f, 0.3f) * 0.9f;
		}
	}
}
