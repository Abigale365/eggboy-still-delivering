using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ShoreWaves : MonoBehaviour
{
	private const float Step = 0.75f;

	private const float Ext = 72f;

	private const float Near = 46f;

	private IslandWorld world;

	private ParticleSystem spray;

	private float[,] top;

	private int n;

	private readonly List<Vector4> edges = new List<Vector4>();

	private float edgesForY = float.NaN;

	private float next;

	private readonly System.Random rnd = new System.Random(20261117);

	public int EdgeCount => edges.Count;

	public int Bursts { get; private set; }

	private IEnumerator Start()
	{
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-nofeel") >= 0)
		{
			base.enabled = false;
			yield break;
		}
		yield return null;
		world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		if (world == null)
		{
			base.enabled = false;
			yield break;
		}
		Bake();
		spray = MakeSpray();
	}

	private void Bake()
	{
		n = Mathf.RoundToInt(192f) + 1;
		top = new float[n, n];
		for (int i = 0; i < n; i++)
		{
			for (int j = 0; j < n; j++)
			{
				float x = -72f + (float)i * 0.75f;
				float z = -72f + (float)j * 0.75f;
				top[i, j] = (Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out var hitInfo, 90f, world.groundMask, QueryTriggerInteraction.Ignore) ? hitInfo.point.y : (-99f));
			}
		}
	}

	private void RebuildEdges(float y)
	{
		edges.Clear();
		for (int i = 1; i < n - 1; i++)
		{
			for (int j = 1; j < n - 1; j++)
			{
				float num = top[i, j];
				if (!(num < y + 0.02f) && !(num > y + 0.9f) && (top[i - 1, j] < y - 0.12f || top[i + 1, j] < y - 0.12f || top[i, j - 1] < y - 0.12f || top[i, j + 1] < y - 0.12f))
				{
					float x = -72f + (float)i * 0.75f;
					float z = -72f + (float)j * 0.75f;
					edges.Add(new Vector4(x, num, z, world.IsBeachSurface(x, z, num) ? 1f : 0f));
				}
			}
		}
		edgesForY = y;
		Debug.Log($"SHOREWAVES water={y:F2} edges={edges.Count}");
	}

	private void Update()
	{
		if (top == null)
		{
			return;
		}
		float staticY = WaterLevel.StaticY;
		if (float.IsNaN(edgesForY) || Mathf.Abs(staticY - edgesForY) > 0.05f)
		{
			RebuildEdges(staticY);
		}
		if (spray == null || edges.Count == 0 || Time.time < next)
		{
			return;
		}
		next = Time.time + R(0.16f, 0.42f);
		Camera camera = Camera.main;
		if (camera == null && Camera.allCamerasCount > 0)
		{
			camera = Camera.allCameras[0];
		}
		if (camera == null)
		{
			return;
		}
		Vector3 position = camera.transform.position;
		Vector3 forward = camera.transform.forward;
		for (int i = 0; i < 24; i++)
		{
			Vector4 vector = edges[rnd.Next(edges.Count)];
			Vector3 vector2 = new Vector3(vector.x, staticY, vector.z);
			Vector3 vector3 = vector2 - position;
			if (!(vector3.sqrMagnitude > 2116f) && !(Vector3.Dot(vector3.normalized, forward) < 0.2f))
			{
				Vector3 vector4 = camera.WorldToViewportPoint(vector2);
				if (!(vector4.z <= 0f) && !(vector4.x < 0.02f) && !(vector4.x > 0.98f) && !(vector4.y < 0.02f) && !(vector4.y > 0.98f))
				{
					Burst(vector2, vector.w > 0.5f);
					break;
				}
			}
		}
	}

	private void Burst(Vector3 at, bool beach)
	{
		Bursts++;
		int num = (beach ? rnd.Next(5, 9) : rnd.Next(14, 24));
		float num2 = (beach ? 0.6f : R(0.85f, 1.15f));
		for (int i = 0; i < num; i++)
		{
			float f = R(0f, MathF.PI * 2f);
			float num3 = R(0.15f, 0.7f) * num2;
			ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
			emitParams.position = new Vector3(at.x + R(-0.45f, 0.45f), at.y + 0.04f, at.z + R(-0.45f, 0.45f));
			emitParams.velocity = new Vector3(Mathf.Cos(f) * num3, R(1.4f, 2.8f) * num2, Mathf.Sin(f) * num3);
			emitParams.startSize = R(0.08f, 0.2f) * (beach ? 0.8f : 1f);
			emitParams.startLifetime = R(0.5f, 0.95f);
			emitParams.startColor = new Color(0.93f, 0.98f, 1f, 0.85f);
			ParticleSystem.EmitParams emitParams2 = emitParams;
			spray.Emit(emitParams2, 1);
		}
	}

	private ParticleSystem MakeSpray()
	{
		Shader shader = Resources.Load<Shader>("TideFeelParticle");
		if (shader == null)
		{
			Debug.LogWarning("SHOREWAVES 找不到 TideFeelParticle 着色器，浪花不出");
			return null;
		}
		GameObject gameObject = new GameObject("ShoreSpray");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		ParticleSystem particleSystem = gameObject.AddComponent<ParticleSystem>();
		particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		ParticleSystem.MainModule main = particleSystem.main;
		main.loop = true;
		main.playOnAwake = false;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.maxParticles = 400;
		main.gravityModifier = 1.1f;
		main.startSpeed = 0f;
		ParticleSystem.EmissionModule emission = particleSystem.emission;
		emission.enabled = false;
		ParticleSystem.ShapeModule shape = particleSystem.shape;
		shape.enabled = false;
		ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
		colorOverLifetime.enabled = true;
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(Color.white, 0f),
			new GradientColorKey(Color.white, 1f)
		}, new GradientAlphaKey[3]
		{
			new GradientAlphaKey(0.9f, 0f),
			new GradientAlphaKey(0.75f, 0.5f),
			new GradientAlphaKey(0f, 1f)
		});
		colorOverLifetime.color = gradient;
		ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
		sizeOverLifetime.enabled = true;
		sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(0.3f, 1.15f), new Keyframe(1f, 0.6f)));
		ParticleSystemRenderer component = gameObject.GetComponent<ParticleSystemRenderer>();
		component.sharedMaterial = new Material(shader)
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		component.sharedMaterial.SetFloat("_Shape", 0f);
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
}
