using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class BeaconLight : MonoBehaviour
{
	public Transform pivot;

	public Light spot;

	public Light glow;

	public Renderer beam;

	public float degreesPerSecond = 30f;

	private const float BeamLength = 58f;

	private const float BeamEndRadius = 4.4f;

	private const float BeamStartRadius = 0.24f;

	private const float BeamTilt = 5.5f;

	private const float BeamIntensity = 1.15f;

	private const float SpotIntensity = 2.6f;

	private const float GlowIntensity = 1.6f;

	private static readonly Color Warm = new Color(1f, 0.72f, 0.4f, 1f);

	private static readonly Color GlassEmission = new Color(1f, 0.72f, 0.4f, 1f);

	private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

	private static readonly int IdReveal = Shader.PropertyToID("_Reveal");

	private static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");

	private static readonly int IdEmissiveFactor = Shader.PropertyToID("emissiveFactor");

	private static readonly int IdLamp = Shader.PropertyToID("_TideBeamLamp");

	private static readonly int IdDir = Shader.PropertyToID("_TideBeamDir");

	private float flashTimer;

	private float power;

	private float reveal = 1f;

	private bool fxBuilt;

	private readonly List<Renderer> beams = new List<Renderer>();

	private Material beamMat;

	private Material haloMat;

	private Material starMat;

	private Transform halo;

	private Transform star;

	private Light spot2;

	private ParticleSystem motes;

	private Coroutine ignition;

	private SkyDimmer dimmer;

	private readonly List<(Renderer r, int slot)> glass = new List<(Renderer, int)>();

	private MaterialPropertyBlock mpb;

	private float glassScanTimer;

	private bool loggedGlass;

	public bool On { get; private set; }

	public bool ManualMode { get; set; }

	public Vector3 LampPosition
	{
		get
		{
			if (!(glow != null))
			{
				if (!(pivot != null))
				{
					return base.transform.position;
				}
				return pivot.position;
			}
			return glow.transform.position;
		}
	}

	private void Awake()
	{
		BuildFx();
		SetEnabled(on: false);
	}

	private void OnDisable()
	{
		Shader.SetGlobalVector(IdLamp, Vector4.zero);
	}

	public void Face(Vector3 target)
	{
		if (!(pivot == null))
		{
			Vector3 forward = target - pivot.position;
			forward.y = 0f;
			if (forward.sqrMagnitude > 0.01f)
			{
				pivot.rotation = Quaternion.LookRotation(forward);
			}
		}
	}

	public void SetYawOffsetFrom(Vector3 target, float yawOffsetDeg)
	{
		if (!(pivot == null))
		{
			Vector3 forward = target - pivot.position;
			forward.y = 0f;
			if (forward.sqrMagnitude > 0.01f)
			{
				pivot.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, yawOffsetDeg, 0f);
			}
		}
	}

	public void RotateYaw(float deltaDeg)
	{
		if (!(pivot == null))
		{
			pivot.Rotate(0f, deltaDeg, 0f, Space.Self);
		}
	}

	public float AlignmentWith(Vector3 target)
	{
		if (pivot == null)
		{
			return 1f;
		}
		Vector3 vector = target - pivot.position;
		vector.y = 0f;
		Vector3 forward = pivot.forward;
		forward.y = 0f;
		if (vector.sqrMagnitude < 0.01f || forward.sqrMagnitude < 0.01f)
		{
			return 1f;
		}
		float num = Vector3.Angle(forward.normalized, vector.normalized);
		return Mathf.Clamp01(1f - num / 55f);
	}

	public void PulseFlash()
	{
		flashTimer = 0.65f;
	}

	public void TurnOn()
	{
		if (!On)
		{
			SetEnabled(on: true);
			if (ignition != null)
			{
				StopCoroutine(ignition);
			}
			ignition = StartCoroutine(Ignite());
		}
	}

	public void TurnOnInstant()
	{
		if (ignition != null)
		{
			StopCoroutine(ignition);
			ignition = null;
		}
		SetEnabled(on: true);
		power = 1f;
		reveal = 1f;
		ApplyLook(0f);
	}

	public void SetEnabled(bool on)
	{
		On = on;
		if (beam != null)
		{
			beam.enabled = false;
		}
		if (spot != null)
		{
			spot.enabled = on;
		}
		if (spot2 != null)
		{
			spot2.enabled = on;
		}
		if (glow != null)
		{
			glow.enabled = on;
		}
		foreach (Renderer beam in beams)
		{
			if (beam != null)
			{
				beam.enabled = on;
			}
		}
		if (halo != null)
		{
			halo.gameObject.SetActive(on);
		}
		if (star != null)
		{
			star.gameObject.SetActive(on);
		}
		if (motes != null)
		{
			if (on)
			{
				motes.Play();
			}
			else
			{
				motes.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
		}
		if (on)
		{
			ScanGlass();
		}
		if (!on)
		{
			power = 0f;
			ApplyGlass(0f);
			Shader.SetGlobalVector(IdLamp, Vector4.zero);
		}
	}

	private IEnumerator Ignite()
	{
		power = 0f;
		reveal = 0f;
		float[] keys = new float[8] { 0f, 0.55f, 0.08f, 0.75f, 0.22f, 0.9f, 0.6f, 1f };
		for (float t2 = 0f; t2 < 1.3f; t2 += Time.deltaTime)
		{
			float num = t2 / 1.3f * (float)(keys.Length - 1);
			int num2 = Mathf.Min(keys.Length - 2, Mathf.FloorToInt(num));
			power = Mathf.Lerp(keys[num2], keys[num2 + 1], Mathf.SmoothStep(0f, 1f, num - (float)num2));
			yield return null;
		}
		power = 1f;
		for (float t2 = 0f; t2 < 1.7f; t2 += Time.deltaTime)
		{
			float num3 = t2 / 1.7f;
			reveal = 1f - (1f - num3) * (1f - num3) * (1f - num3);
			yield return null;
		}
		reveal = 1f;
		ignition = null;
	}

	private void Update()
	{
		if (On && !(pivot == null))
		{
			float boost = 0f;
			if (flashTimer > 0f)
			{
				flashTimer = Mathf.Max(0f, flashTimer - Time.unscaledDeltaTime);
				boost = Mathf.Sin(flashTimer / 0.65f * MathF.PI);
			}
			if (!ManualMode)
			{
				pivot.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
			}
			ApplyLook(boost);
		}
	}

	private void LateUpdate()
	{
		if (On)
		{
			UpdateGlint();
		}
	}

	private float NightFactor()
	{
		if (dimmer == null)
		{
			dimmer = UnityEngine.Object.FindObjectOfType<SkyDimmer>();
		}
		if (!(dimmer != null) || !dimmer.Dawned)
		{
			return 1f;
		}
		return 0.45f;
	}

	private void ApplyLook(float boost)
	{
		float num = NightFactor();
		float num2 = power * num;
		if (spot != null)
		{
			spot.intensity = (2.6f + boost * 4.2f) * num2;
		}
		if (spot2 != null)
		{
			spot2.intensity = 2.08f * num2;
		}
		if (glow != null)
		{
			glow.intensity = (1.6f + boost * 2.5f) * power;
		}
		if (beamMat != null)
		{
			beamMat.SetFloat(IdIntensity, 1.15f * num2 * (1f + boost * 0.9f));
			beamMat.SetFloat(IdReveal, reveal);
		}
		ApplyGlass(power * (1f + boost * 0.6f));
		Vector3 lampPosition = LampPosition;
		Vector3 forward = pivot.forward;
		Vector2 vector = new Vector2(forward.x, forward.z);
		if (vector.sqrMagnitude < 0.0001f)
		{
			vector = Vector2.up;
		}
		vector.Normalize();
		float num3 = Mathf.Atan2(4.4f, 58f);
		Shader.SetGlobalVector(IdLamp, new Vector4(lampPosition.x, lampPosition.y, lampPosition.z, num2 * Mathf.Clamp01(reveal * 1.4f)));
		Shader.SetGlobalVector(IdDir, new Vector4(vector.x, vector.y, Mathf.Cos(num3 * 2.2f), 58f * reveal));
	}

	private void UpdateGlint()
	{
		Camera camera = Camera.main;
		if (camera == null)
		{
			Camera[] allCameras = Camera.allCameras;
			if (allCameras.Length != 0)
			{
				camera = allCameras[0];
			}
		}
		if (!(camera == null) && !(pivot == null))
		{
			Vector3 lampPosition = LampPosition;
			Vector3 vector = camera.transform.position - lampPosition;
			float magnitude = vector.magnitude;
			vector.y = 0f;
			Vector3 forward = pivot.forward;
			forward.y = 0f;
			float f = 0f;
			if (vector.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f)
			{
				f = Mathf.Abs(Vector3.Dot(vector.normalized, forward.normalized));
			}
			float num = Mathf.SmoothStep(0f, 1f, (magnitude - 2.5f) / 3f);
			float num2 = Mathf.Pow(f, 26f) * power * reveal * NightFactor() * num;
			if (starMat != null)
			{
				starMat.SetFloat(IdIntensity, num2 * 1.6f);
			}
			if (haloMat != null)
			{
				haloMat.SetFloat(IdIntensity, 1.25f * power * num);
			}
			if (star != null)
			{
				float num3 = Mathf.Clamp(magnitude * 0.16f, 3.5f, 14f) * (0.7f + 0.5f * num2);
				star.localScale = Vector3.one * num3;
				star.gameObject.SetActive(num2 > 0.01f);
			}
			if (halo != null)
			{
				halo.localScale = Vector3.one * Mathf.Clamp(2.6f + magnitude * 0.03f, 2.6f, 6f) * (1f + 0.35f * num2);
			}
		}
	}

	private void BuildFx()
	{
		if (fxBuilt || pivot == null)
		{
			return;
		}
		fxBuilt = true;
		if (beam != null)
		{
			beam.enabled = false;
		}
		Shader shader = Shader.Find("TideTown/LighthouseBeam");
		Shader shader2 = Shader.Find("TideTown/SoftGlow");
		if (shader == null || shader2 == null)
		{
			Debug.LogWarning("BEACON_FX shader missing; keeping the old cone");
			if (beam != null)
			{
				beams.Add(beam);
			}
			return;
		}
		beamMat = new Material(shader)
		{
			name = "LighthouseBeamSoft"
		};
		beamMat.SetColor("_Color", Warm);
		beamMat.SetFloat("_Length", 58f);
		beamMat.SetFloat("_R0", 0.24f);
		beamMat.SetFloat("_R1", 4.4f);
		Mesh sharedMesh = BeamMesh(58f, 0.24f, 4.4f, 18, 36);
		for (int i = 0; i < 2; i++)
		{
			GameObject obj = new GameObject((i == 0) ? "BeamSoftA" : "BeamSoftB");
			obj.transform.SetParent(pivot, worldPositionStays: false);
			obj.transform.localRotation = Quaternion.Euler(5.5f, (i == 0) ? 0f : 180f, 0f);
			obj.AddComponent<MeshFilter>().sharedMesh = sharedMesh;
			MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = beamMat;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			beams.Add(meshRenderer);
		}
		if (spot != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(spot.gameObject, spot.transform.parent);
			gameObject.name = "BeaconSpotB";
			gameObject.transform.localRotation = Quaternion.Euler(5.5f, 180f, 0f);
			spot2 = gameObject.GetComponent<Light>();
			spot.transform.localRotation = Quaternion.Euler(5.5f, 0f, 0f);
		}
		Mesh quad = QuadMesh();
		haloMat = new Material(shader2)
		{
			name = "LighthouseHalo"
		};
		haloMat.SetColor("_Color", new Color(1f, 0.78f, 0.5f, 1f));
		haloMat.SetFloat("_Mode", 0f);
		haloMat.SetFloat("_Billboard", 1f);
		haloMat.SetFloat("_Push", 1.55f);
		halo = Billboard("LampHalo", quad, haloMat, 3f);
		starMat = new Material(shader2)
		{
			name = "LighthouseGlint"
		};
		starMat.SetColor("_Color", new Color(1f, 0.88f, 0.66f, 1f));
		starMat.SetFloat("_Mode", 1f);
		starMat.SetFloat("_Billboard", 1f);
		starMat.SetFloat("_Push", 1.7f);
		star = Billboard("LampGlint", quad, starMat, 6f);
		motes = BuildMotes(shader2);
	}

	private Transform Billboard(string name, Mesh quad, Material mat, float size)
	{
		GameObject obj = new GameObject(name);
		obj.transform.SetParent(base.transform, worldPositionStays: false);
		obj.transform.position = LampPosition;
		obj.transform.localScale = Vector3.one * size;
		obj.AddComponent<MeshFilter>().sharedMesh = quad;
		MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = mat;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		return obj.transform;
	}

	private ParticleSystem BuildMotes(Shader glowShader)
	{
		GameObject gameObject = new GameObject("LampMotes");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.transform.position = LampPosition + Vector3.down * 0.6f;
		ParticleSystem particleSystem = gameObject.AddComponent<ParticleSystem>();
		particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		ParticleSystem.MainModule main = particleSystem.main;
		main.loop = true;
		main.playOnAwake = false;
		main.duration = 5f;
		main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
		main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.55f, 1f), new Color(1f, 0.95f, 0.75f, 1f));
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.maxParticles = 40;
		ParticleSystem.EmissionModule emission = particleSystem.emission;
		emission.rateOverTime = 5f;
		ParticleSystem.ShapeModule shape = particleSystem.shape;
		shape.shapeType = ParticleSystemShapeType.Sphere;
		shape.radius = 2.6f;
		ParticleSystem.NoiseModule noise = particleSystem.noise;
		noise.enabled = true;
		noise.strength = 0.35f;
		noise.frequency = 0.4f;
		noise.scrollSpeed = 0.2f;
		ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
		colorOverLifetime.enabled = true;
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(Color.white, 0f),
			new GradientColorKey(Color.white, 1f)
		}, new GradientAlphaKey[4]
		{
			new GradientAlphaKey(0f, 0f),
			new GradientAlphaKey(1f, 0.25f),
			new GradientAlphaKey(0.8f, 0.7f),
			new GradientAlphaKey(0f, 1f)
		});
		colorOverLifetime.color = gradient;
		ParticleSystemRenderer component = gameObject.GetComponent<ParticleSystemRenderer>();
		Material material = new Material(glowShader)
		{
			name = "LighthouseMotes"
		};
		material.SetColor("_Color", new Color(1f, 0.85f, 0.55f, 1f));
		material.SetFloat("_Intensity", 1.8f);
		material.SetFloat("_Mode", 0f);
		material.SetFloat("_Billboard", 0f);
		component.sharedMaterial = material;
		component.renderMode = ParticleSystemRenderMode.Billboard;
		component.shadowCastingMode = ShadowCastingMode.Off;
		component.receiveShadows = false;
		return particleSystem;
	}

	private void ScanGlass()
	{
		glass.Clear();
		Vector3 lampPosition = LampPosition;
		MeshRenderer[] array = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
		foreach (MeshRenderer meshRenderer in array)
		{
			if (meshRenderer.transform.IsChildOf(base.transform))
			{
				continue;
			}
			Bounds bounds = meshRenderer.bounds;
			if ((bounds.center - lampPosition).sqrMagnitude > 9f || bounds.size.magnitude > 8f)
			{
				continue;
			}
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if (!(sharedMaterials[j] == null))
				{
					string text = sharedMaterials[j].name.ToLowerInvariant();
					if (text.Contains("glass") || text.Contains("lantern"))
					{
						glass.Add((meshRenderer, j));
					}
				}
			}
		}
		glassScanTimer = 3f;
		if (loggedGlass)
		{
			return;
		}
		loggedGlass = true;
		List<string> list = new List<string>();
		foreach (var (renderer, num) in glass)
		{
			list.Add(renderer.name + "#" + num + ":" + renderer.sharedMaterials[num].name);
		}
		Debug.Log("BEACON_GLASS " + glass.Count + " " + string.Join(", ", list));
	}

	private void ApplyGlass(float k)
	{
		if (On)
		{
			glassScanTimer -= Time.deltaTime;
			if (glassScanTimer <= 0f)
			{
				ScanGlass();
			}
		}
		if (glass.Count == 0)
		{
			return;
		}
		if (mpb == null)
		{
			mpb = new MaterialPropertyBlock();
		}
		Color value = GlassEmission * (2.4f * k);
		foreach (var (renderer, materialIndex) in glass)
		{
			if (!(renderer == null))
			{
				renderer.GetPropertyBlock(mpb, materialIndex);
				mpb.SetColor(IdEmission, value);
				mpb.SetColor(IdEmissiveFactor, value);
				renderer.SetPropertyBlock(mpb, materialIndex);
			}
		}
	}

	private static Mesh QuadMesh()
	{
		Mesh mesh = new Mesh
		{
			name = "GlowQuad"
		};
		mesh.vertices = new Vector3[4]
		{
			new Vector3(-0.5f, -0.5f, 0f),
			new Vector3(0.5f, -0.5f, 0f),
			new Vector3(0.5f, 0.5f, 0f),
			new Vector3(-0.5f, 0.5f, 0f)
		};
		mesh.uv = new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(1f, 1f),
			new Vector2(0f, 1f)
		};
		mesh.triangles = new int[6] { 0, 2, 1, 0, 3, 2 };
		mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
		return mesh;
	}

	private static Mesh BeamMesh(float length, float r0, float r1, int rings, int segments)
	{
		List<Vector3> list = new List<Vector3>();
		List<Vector3> list2 = new List<Vector3>();
		List<int> list3 = new List<int>();
		float num = (r1 - r0) / length;
		for (int i = 0; i <= rings; i++)
		{
			float z = length * (float)i / (float)rings;
			float num2 = Mathf.Lerp(r0, r1, (float)i / (float)rings);
			for (int j = 0; j <= segments; j++)
			{
				float f = (float)j * MathF.PI * 2f / (float)segments;
				float num3 = Mathf.Cos(f);
				float num4 = Mathf.Sin(f);
				list.Add(new Vector3(num3 * num2, num4 * num2, z));
				list2.Add(new Vector3(num3, num4, 0f - num).normalized);
			}
		}
		int num5 = segments + 1;
		for (int k = 0; k < rings; k++)
		{
			for (int l = 0; l < segments; l++)
			{
				int num6 = k * num5 + l;
				int item = num6 + 1;
				int num7 = num6 + num5;
				int item2 = num7 + 1;
				list3.Add(num6);
				list3.Add(num7);
				list3.Add(item);
				list3.Add(item);
				list3.Add(num7);
				list3.Add(item2);
			}
		}
		int count = list.Count;
		list.Add(new Vector3(0f, 0f, length));
		list2.Add(Vector3.forward);
		int num8 = rings * num5;
		for (int m = 0; m < segments; m++)
		{
			list3.Add(count);
			list3.Add(num8 + m);
			list3.Add(num8 + m + 1);
		}
		Mesh mesh = new Mesh();
		mesh.name = "LighthouseBeamSoft";
		mesh.SetVertices(list);
		mesh.SetNormals(list2);
		mesh.SetTriangles(list3, 0);
		mesh.RecalculateBounds();
		return mesh;
	}
}
