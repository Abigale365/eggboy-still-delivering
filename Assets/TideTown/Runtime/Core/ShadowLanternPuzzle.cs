using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class ShadowLanternPuzzle : MonoBehaviour
{
	private const int ShadowLayer = 29;

	private const float TargetTolerance = 7f;

	private const float StartAngle = 125f;

	private const float TurnSpeed = 40f;

	private GameState state;

	private EggboyPlayer player;

	private EggboyUI ui;

	private IslandWorld world;

	private Vector3 axis;

	private Vector3 side;

	private Transform turntable;

	private Camera shadowCam;

	private RenderTexture rt;

	private Material screenMat;

	public bool DebugHold;

	private GameObject bottle;

	private float glow;

	private float turnRate;

	private float turnRateVelocity;

	private float alignmentLight;

	private static readonly Color Lit = new Color(1f, 0.93f, 0.78f, 1f);

	private static readonly Color Shade = new Color(0.22f, 0.15f, 0.1f, 1f);

	private static Texture2D white;

	private static readonly Color CanvasTint = new Color(0.9f, 0.84f, 0.72f, 1f);

	private static Material driftMat;

	private static Material woodMat;

	private Material shadeMat;

	public static ShadowLanternPuzzle Instance { get; private set; }

	public Vector3 TurntablePos { get; private set; }

	public Vector3 ScreenCenter { get; private set; }

	public Vector3 BottlePos => TurntablePos + Vector3.up * 0.05f + side * 0.45f;

	public float Angle { get; private set; } = 125f;


	public bool Solved
	{
		get
		{
			if (state != null)
			{
				return state.flags.Contains("shadow_key_done");
			}
			return false;
		}
	}

	public bool BottleTaken
	{
		get
		{
			if (state != null)
			{
				return state.flags.Contains("has_sound_bottle");
			}
			return false;
		}
	}

	private bool PlayerFree
	{
		get
		{
			if (ui != null && !ui.Paused && !ui.TitleShowing && !ui.LetterOpen && !ui.Cinematic && !MenuController.OverlayOpen && player != null)
			{
				return !player.locked;
			}
			return false;
		}
	}

	public bool PlayerNear
	{
		get
		{
			if (player != null)
			{
				return Vector3.Distance(player.transform.position, TurntablePos) < 1.9f;
			}
			return false;
		}
	}

	public bool Turning { get; private set; }

	public void Init(EggboyPlayer p, EggboyUI u, IslandWorld w, GameState s)
	{
		Instance = this;
		player = p;
		ui = u;
		world = w;
		state = s;
		if (!Place())
		{
			base.enabled = false;
			return;
		}
		Build();
		if (Solved)
		{
			Angle = 0f;
			ApplyAngle();
			SpawnBottle();
		}
		InteractionService interactionService = UnityEngine.Object.FindObjectOfType<InteractionService>();
		if (interactionService != null)
		{
			interactionService.Register(new TurntablePrompt(this));
			interactionService.Register(new BottlePickup(this));
		}
	}

	private bool Place()
	{
		GameObject gameObject = GameObject.Find("B5_boathouse");
		Vector3 vector = ((gameObject != null) ? gameObject.transform.position : new Vector3(21.7f, 1.4f, -15.9f));
		List<Vector3> list = new List<Vector3>();
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		if (textAsset != null)
		{
			RouteList routeList = JsonUtility.FromJson<RouteList>(textAsset.text);
			if (routeList != null)
			{
				RouteStop[] route = routeList.route;
				foreach (RouteStop routeStop in route)
				{
					list.Add(new Vector3(routeStop.x, 0f, routeStop.z));
				}
			}
		}
		float num = float.MaxValue;
		bool flag = false;
		List<Vector2[]> planks = new List<Vector2[]>();
		foreach (WalkPoly walkPoly in world.walkPolys)
		{
			if (walkPoly.id == "pier" || walkPoly.id == "dock")
			{
				planks.Add(walkPoly.points);
			}
		}
		(float, float)[] array = new(float, float)[2]
		{
			(0.65f, 1.2f),
			(2.5f, 3f)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(float, float) tuple = array[i];
			for (float num2 = 2.5f; num2 <= 12f; num2 += 0.5f)
			{
				for (int j = 0; j < 24; j++)
				{
					for (int k = 0; k < 8; k++)
					{
						float num3 = (float)j * MathF.PI / 12f;
						Vector3 vector2 = vector + new Vector3(Mathf.Cos(num3) * num2, 0f, Mathf.Sin(num3) * num2);
						float f = num3 + (float)k * MathF.PI / 4f;
						Vector3 vector3 = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
						Vector3 vector4 = vector2 - vector3 * 1.4f;
						Vector3 vector5 = vector2 + vector3 * 1.6f;
						bool flag2 = true;
						float num4 = 0f;
						Vector3 vector6 = vector2 + new Vector3(0f - vector3.z, 0f, vector3.x) * 0.45f;
						Vector3[] array2 = new Vector3[5]
						{
							vector2,
							vector4,
							vector5,
							vector2 + new Vector3(0f - vector3.z, 0f, vector3.x) * 1.2f,
							vector6
						};
						for (int l = 0; l < array2.Length; l++)
						{
							Vector3 vector7 = array2[l];
							if (Vector3.Distance(vector7, vector) < 2.65f || !world.Walkable(vector7.x, vector7.z) || OnPlanks(vector7))
							{
								flag2 = false;
								break;
							}
							float num5 = world.GroundAt(vector7.x, vector7.z, vector.y + tuple.Item2);
							if (vector7 == vector2)
							{
								num4 = num5;
							}
							if (Mathf.Abs(num5 - num4) > 0.25f || Mathf.Abs(num5 - vector.y) > tuple.Item1 || num5 < WaterLevel.LevelYs[2] + 0.12f)
							{
								flag2 = false;
								break;
							}
						}
						if (!flag2)
						{
							continue;
						}
						float num6 = float.MaxValue;
						for (int m = 0; m + 1 < list.Count; m++)
						{
							num6 = Mathf.Min(num6, Mathf.Min(DistToSeg(vector2, list[m], list[m + 1]), DistToSeg(vector5, list[m], list[m + 1])), Mathf.Min(DistToSeg(vector4, list[m], list[m + 1]), DistToSeg(vector6, list[m], list[m + 1])));
						}
						if (!(num6 < 1.6f) && (!(DogPawPlate.Instance != null) || !(Vector3.Distance(vector2, DogPawPlate.Instance.Center) < 3f)))
						{
							float num7 = num2 + Mathf.Max(0f, 3f - num6) * 2f + ((k == 0) ? 0f : 0.3f);
							if (num7 < num)
							{
								num = num7;
								flag = true;
								TurntablePos = new Vector3(vector2.x, num4, vector2.z);
								axis = vector3;
							}
						}
					}
				}
			}
			if (flag)
			{
				break;
			}
		}
		if (!flag)
		{
			Debug.LogWarning("SHADOW 船棚附近找不到合适的位置");
			return false;
		}
		side = new Vector3(0f - axis.z, 0f, axis.x);
		ScreenCenter = TurntablePos + axis * 1.6f + Vector3.up * 0.85f;
		return true;
		bool OnPlanks(Vector3 q)
		{
			if (q.x > 22f && q.z < -8f && q.z > -20f)
			{
				return true;
			}
			foreach (Vector2[] item in planks)
			{
				if (InPoly(item, q.x, q.z))
				{
					return true;
				}
			}
			return false;
		}
	}

	private static bool InPoly(Vector2[] poly, float x, float z)
	{
		bool flag = false;
		int num = 0;
		int num2 = poly.Length - 1;
		while (num < poly.Length)
		{
			if (poly[num].y > z != poly[num2].y > z && x < (poly[num2].x - poly[num].x) * (z - poly[num].y) / (poly[num2].y - poly[num].y) + poly[num].x)
			{
				flag = !flag;
			}
			num2 = num++;
		}
		return flag;
	}

	private static float DistToSeg(Vector3 p, Vector3 a, Vector3 b)
	{
		Vector3 vector = b - a;
		vector.y = 0f;
		Vector3 lhs = p - a;
		lhs.y = 0f;
		float num = Mathf.Clamp01(Vector3.Dot(lhs, vector) / Mathf.Max(0.0001f, vector.sqrMagnitude));
		Vector3 vector2 = a + vector * num;
		return new Vector2(p.x - vector2.x, p.z - vector2.z).magnitude;
	}

	private static Material ParticleBase()
	{
		BakeryEffects bakeryEffects = UnityEngine.Object.FindObjectOfType<BakeryEffects>();
		if (bakeryEffects != null && bakeryEffects.ParticleMaterial != null)
		{
			return new Material(bakeryEffects.ParticleMaterial);
		}
		return new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
	}

	private static void SetColorTex(Material m, Color c, Texture tex)
	{
		if (white == null)
		{
			white = new Texture2D(1, 1);
			white.SetPixel(0, 0, Color.white);
			white.Apply();
		}
		string[] array = new string[2] { "_BaseMap", "_MainTex" };
		foreach (string text in array)
		{
			if (m.HasProperty(text))
			{
				m.SetTexture(text, (tex != null) ? tex : white);
			}
		}
		array = new string[3] { "_BaseColor", "_Color", "_TintColor" };
		foreach (string text2 in array)
		{
			if (m.HasProperty(text2))
			{
				m.SetColor(text2, c);
			}
		}
	}

	private void Build()
	{
		new Vector3(0f, TurntablePos.y, 0f);
		Quaternion quaternion = Quaternion.LookRotation(axis, Vector3.up);
		Vector3 vector = TurntablePos - axis * 1.4f + Vector3.up * 0.85f;
		GameObject gameObject = TideTownStyleUI.CloneSceneLux3D("B9_灯塔灯头_v4", 0.45f);
		if (gameObject != null)
		{
			gameObject.transform.SetParent(base.transform, worldPositionStays: false);
			Vector3 vector2 = TurntablePos - axis * 1.4f;
			vector2.y = TurntablePos.y;
			gameObject.transform.position = vector2 + Vector3.up * 0.55f;
			gameObject.transform.rotation = quaternion;
		}
		GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		UnityEngine.Object.Destroy(obj.GetComponent<Collider>());
		obj.transform.SetParent(base.transform, worldPositionStays: false);
		obj.transform.position = new Vector3(vector.x, TurntablePos.y + 0.28f, vector.z);
		obj.transform.localScale = new Vector3(0.08f, 0.28f, 0.08f);
		obj.GetComponent<Renderer>().sharedMaterial = WoodMat();
		GameObject obj2 = new GameObject("ShadowLampLight");
		obj2.transform.SetParent(base.transform, worldPositionStays: false);
		obj2.transform.SetPositionAndRotation(vector, quaternion);
		Light light = obj2.AddComponent<Light>();
		light.type = LightType.Spot;
		light.spotAngle = 55f;
		light.range = 4.5f;
		light.intensity = 3f;
		light.color = new Color(1f, 0.85f, 0.6f);
		light.shadows = LightShadows.None;
		GameObject gameObject2 = new GameObject("ShadowTurntable");
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject2.transform.position = TurntablePos;
		turntable = gameObject2.transform;
		GameObject obj3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		UnityEngine.Object.Destroy(obj3.GetComponent<Collider>());
		obj3.transform.SetParent(turntable, worldPositionStays: false);
		obj3.transform.localPosition = new Vector3(0f, 0.06f, 0f);
		obj3.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
		obj3.GetComponent<Renderer>().sharedMaterial = WoodMat();
		GameObject obj4 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		UnityEngine.Object.Destroy(obj4.GetComponent<Collider>());
		obj4.transform.SetParent(turntable, worldPositionStays: false);
		obj4.transform.localPosition = new Vector3(0f, 0.42f, 0f);
		obj4.transform.localScale = new Vector3(0.04f, 0.36f, 0.04f);
		obj4.GetComponent<Renderer>().sharedMaterial = WoodMat();
		AddJunk("D6_救生圈_v2", 0.21f, new Vector3(-0.15f, 0.85f, 0.06f), "ring");
		AddJunk("D4O_船桨_v2", 0.36f, new Vector3(0.11f, 0.85f, 0f), "shaft");
		AddJunk("S2_木箱_分享包版", 0.08f, new Vector3(0.17f, 0.77f, -0.06f), "box");
		AddJunk("S2_木箱_分享包版", 0.08f, new Vector3(0.25f, 0.77f, 0.04f), "box");
		GameObject gameObject3 = GameObject.CreatePrimitive(PrimitiveType.Quad);
		UnityEngine.Object.Destroy(gameObject3.GetComponent<Collider>());
		gameObject3.name = "ShadowScreen";
		gameObject3.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject3.transform.SetPositionAndRotation(ScreenCenter, quaternion);
		gameObject3.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
		rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
		{
			name = "ShadowScreenRT"
		};
		screenMat = ParticleBase();
		SetColorTex(screenMat, CanvasTint, rt);
		gameObject3.GetComponent<Renderer>().sharedMaterial = screenMat;
		GameObject obj5 = GameObject.CreatePrimitive(PrimitiveType.Quad);
		UnityEngine.Object.Destroy(obj5.GetComponent<Collider>());
		obj5.transform.SetParent(gameObject3.transform, worldPositionStays: false);
		obj5.transform.localPosition = new Vector3(0f, 0f, 0.002f);
		obj5.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
		Material material = ParticleBase();
		SetColorTex(material, new Color(0.62f, 0.52f, 0.38f), null);
		obj5.GetComponent<Renderer>().sharedMaterial = material;
		Material m2 = DriftwoodMat();
		Vector3[] array = new Vector3[2]
		{
			new Vector3(-0.79f, 0f, 0f),
			new Vector3(0.79f, 0f, 0f)
		};
		foreach (Vector3 vector3 in array)
		{
			Bar(ScreenCenter + quaternion * vector3 + Vector3.down * 0.06f, new Vector3(0.08f, 1.72f, 0.08f), quaternion, m2);
		}
		Bar(ScreenCenter + Vector3.up * 0.77f + quaternion * new Vector3(0f, 0f, -0.012f), new Vector3(1.72f, 0.08f, 0.07f), quaternion, m2);
		Bar(ScreenCenter + Vector3.down * 0.77f + quaternion * new Vector3(0f, 0f, -0.012f), new Vector3(1.66f, 0.06f, 0.06f), quaternion, m2);
		Bar(ScreenCenter + Vector3.up * 0.86f, new Vector3(1.9f, 0.05f, 0.05f), quaternion, m2);
		Vector3 vector4 = ScreenCenter + Vector3.up * 0.75f + axis * 0.04f;
		Vector3 vector5 = new Vector3(ScreenCenter.x, TurntablePos.y, ScreenCenter.z) + axis * 0.55f;
		Vector3 center2 = (vector4 + vector5) * 0.5f;
		Vector3 vector6 = vector4 - vector5;
		Bar(center2, new Vector3(0.06f, vector6.magnitude, 0.06f), Quaternion.LookRotation(axis, vector6.normalized), m2);
		GameObject gameObject4 = new GameObject("ShadowCasterCam");
		gameObject4.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject4.transform.SetPositionAndRotation(vector, Quaternion.LookRotation(ScreenCenter - vector, Vector3.up));
		shadowCam = gameObject4.AddComponent<Camera>();
		shadowCam.cullingMask = 536870912;
		shadowCam.clearFlags = CameraClearFlags.Color;
		shadowCam.backgroundColor = Lit;
		float num = Vector3.Distance(vector, ScreenCenter);
		shadowCam.fieldOfView = 2f * Mathf.Atan(0.75f / num) * 57.29578f;
		shadowCam.aspect = 1f;
		shadowCam.nearClipPlane = 0.1f;
		shadowCam.farClipPlane = num + 0.5f;
		shadowCam.targetTexture = rt;
		UniversalAdditionalCameraData universalAdditionalCameraData = shadowCam.GetUniversalAdditionalCameraData();
		if (universalAdditionalCameraData != null)
		{
			universalAdditionalCameraData.renderPostProcessing = false;
			universalAdditionalCameraData.renderShadows = false;
		}
		Camera[] allCameras = Camera.allCameras;
		foreach (Camera camera in allCameras)
		{
			if (camera != shadowCam)
			{
				camera.cullingMask &= -536870913;
			}
		}
		world.AddCollider(new EggboyCollider
		{
			x = ScreenCenter.x,
			z = ScreenCenter.z,
			r = 0.45f
		});
		world.AddCollider(new EggboyCollider
		{
			x = vector.x,
			z = vector.z,
			r = 0.3f
		});
		world.AddCollider(new EggboyCollider
		{
			x = TurntablePos.x,
			z = TurntablePos.z,
			r = 0.45f
		});
		ApplyAngle();
		void Bar(Vector3 center, Vector3 size, Quaternion rot, Material m)
		{
			GameObject obj6 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			UnityEngine.Object.Destroy(obj6.GetComponent<Collider>());
			obj6.transform.SetParent(base.transform, worldPositionStays: false);
			obj6.transform.SetPositionAndRotation(center, rot);
			obj6.transform.localScale = size;
			obj6.GetComponent<Renderer>().sharedMaterial = m;
		}
	}

	private static Material DriftwoodMat()
	{
		if (driftMat != null)
		{
			return driftMat;
		}
		driftMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
		Color color = new Color(0.56f, 0.44f, 0.32f);
		driftMat.color = color;
		if (driftMat.HasProperty("_BaseColor"))
		{
			driftMat.SetColor("_BaseColor", color);
		}
		if (driftMat.HasProperty("_Smoothness"))
		{
			driftMat.SetFloat("_Smoothness", 0.15f);
		}
		return driftMat;
	}

	private static Material WoodMat()
	{
		if (woodMat != null)
		{
			return woodMat;
		}
		woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
		Color color = new Color(0.5f, 0.33f, 0.19f);
		woodMat.color = color;
		if (woodMat.HasProperty("_BaseColor"))
		{
			woodMat.SetColor("_BaseColor", color);
		}
		return woodMat;
	}

	private void AddJunk(string model, float size, Vector3 uvz, string kind)
	{
		GameObject gameObject = TideTownStyleUI.CloneSceneLux3D(model, size);
		if (gameObject == null)
		{
			return;
		}
		gameObject.name = "Junk_" + kind;
		gameObject.transform.SetParent(turntable, worldPositionStays: false);
		OrientJunk(gameObject, kind);
		gameObject.transform.localPosition = new Vector3(uvz.x, uvz.y, uvz.z) - LocalCenter(gameObject);
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, turntable, worldPositionStays: false);
		gameObject2.name = "Silhouette_" + kind;
		if (shadeMat == null)
		{
			shadeMat = ParticleBase();
			SetColorTex(shadeMat, Shade, null);
		}
		Renderer[] componentsInChildren = gameObject2.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			Material[] sharedMaterials = renderer.sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				sharedMaterials[j] = shadeMat;
			}
			renderer.sharedMaterials = sharedMaterials;
			renderer.gameObject.layer = 29;
		}
		OverlayRender.SetLayerRecursive(gameObject2, 29);
	}

	private void OrientJunk(GameObject go, string kind)
	{
		Transform transform = go.transform;
		transform.localRotation = Quaternion.identity;
		Vector3 size = LocalBounds(go).size;
		if (kind == "ring")
		{
			if (size.y <= size.x && size.y <= size.z)
			{
				transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			}
			else if (size.x <= size.y && size.x <= size.z)
			{
				transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
			}
		}
		else if (kind == "shaft")
		{
			if (size.y >= size.x && size.y >= size.z)
			{
				transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
			}
			else if (size.z >= size.x && size.z >= size.y)
			{
				transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
			}
			Bounds bounds = LocalBounds(go);
			if (bounds.size.z > bounds.size.y)
			{
				transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * transform.localRotation;
			}
		}
	}

	private Bounds LocalBounds(GameObject go)
	{
		Renderer[] componentsInChildren = go.GetComponentsInChildren<Renderer>(includeInactive: true);
		Matrix4x4 worldToLocalMatrix = turntable.worldToLocalMatrix;
		bool flag = false;
		Bounds result = default(Bounds);
		Renderer[] array = componentsInChildren;
		for (int i = 0; i < array.Length; i++)
		{
			Bounds bounds = array[i].bounds;
			for (int j = 0; j < 8; j++)
			{
				Vector3 point = new Vector3((j % 2 == 0) ? bounds.min.x : bounds.max.x, (j / 2 % 2 == 0) ? bounds.min.y : bounds.max.y, (j / 4 == 0) ? bounds.min.z : bounds.max.z);
				Vector3 vector = worldToLocalMatrix.MultiplyPoint3x4(point);
				if (!flag)
				{
					result = new Bounds(vector, Vector3.zero);
					flag = true;
				}
				else
				{
					result.Encapsulate(vector);
				}
			}
		}
		return result;
	}

	private Vector3 LocalCenter(GameObject go)
	{
		return LocalBounds(go).center - go.transform.localPosition;
	}

	private void ApplyAngle()
	{
		if (!(turntable == null))
		{
			turntable.rotation = Quaternion.LookRotation(axis, Vector3.up) * Quaternion.Euler(0f, Angle, 0f);
		}
	}

	private void Update()
	{
		if (turntable == null)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		if (shadowCam != null && player != null)
		{
			shadowCam.enabled = Vector3.Distance(player.transform.position, TurntablePos) < 30f;
		}
		Turning = false;
		if (!Solved && deltaTime > 0f)
		{
			bool flag2 = (Turning = DebugHold || (PlayerFree && PlayerNear && Input.GetKey(KeyCode.E)));
			float num = Mathf.Abs(Mathf.DeltaAngle(0f, Angle));
			float target = (flag2 ? Mathf.Lerp(12f, 40f, Mathf.InverseLerp(7f, 26f, num)) : 0f);
			turnRate = Mathf.SmoothDamp(turnRate, target, ref turnRateVelocity, 0.12f, float.PositiveInfinity, deltaTime);
			if (flag2 || turnRate > 0.1f)
			{
				float num2 = turnRate * deltaTime;
				Angle = Mathf.Repeat(Angle - num2, 360f);
				if (num < 7f || Mathf.Abs(Mathf.DeltaAngle(0f, Angle)) < 7f)
				{
					Angle = 0f;
					Solve();
				}
				ApplyAngle();
			}
		}
		glow = Mathf.MoveTowards(glow, Solved ? 1f : 0f, deltaTime * 1.5f);
		alignmentLight = Mathf.Max(0f, alignmentLight - deltaTime / 0.85f);
		if (screenMat != null)
		{
			Color c = Color.Lerp(CanvasTint, new Color(1.12f, 0.98f, 0.73f), glow);
			c.r += 0.22f * alignmentLight;
			c.g += 0.13f * alignmentLight;
			SetColorTex(screenMat, c, rt);
		}
	}

	public void DebugSetAngle(float a)
	{
		Angle = a;
		turnRate = (turnRateVelocity = 0f);
		ApplyAngle();
	}

	private void Solve()
	{
		if (!Solved)
		{
			state.flags.Add("shadow_key_done");
			alignmentLight = 1f;
			TideTownAchievementSystem.Unlock("shadow_key");
			ShadowAndChimeSystem.PlayProceduralChime(783.99f, 0.3f);
			ShadowAndChimeSystem.PlayProceduralChime(1174.66f, 0.6f);
			SpawnBottle();
		}
	}

	private void SpawnBottle()
	{
		if (!BottleTaken && !(bottle != null))
		{
			Lux3DService lux3DService = Lux3DService.Ensure();
			bottle = lux3DService.InstantiateFallback(lux3DService.Find("sound_bottle"), 0.26f);
			if (!(bottle == null))
			{
				bottle.name = "SoundBottle";
				bottle.transform.SetParent(base.transform, worldPositionStays: false);
				Vector3 bottlePos = BottlePos;
				bottlePos.y = world.GroundAt(bottlePos.x, bottlePos.z, bottlePos.y + 1f);
				bottle.transform.position = bottlePos;
			}
		}
	}

	public void TakeBottle()
	{
		if (Solved && !BottleTaken)
		{
			state.flags.Add("has_sound_bottle");
			if (bottle != null)
			{
				UnityEngine.Object.Destroy(bottle);
			}
			ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.2f);
			if (ui != null)
			{
				ui.ShowMemory("一只空玻璃瓶", "瓶底刻着一行小字：\n\n镇上的声音，装起来，\n带去广场的八音盒。");
			}
		}
	}

	public bool DebugIsShadow(float u, float v)
	{
		if (shadowCam == null || rt == null)
		{
			return false;
		}
		shadowCam.Render();
		RenderTexture active = RenderTexture.active;
		RenderTexture.active = rt;
		Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
		texture2D.ReadPixels(new Rect(Mathf.Clamp(u * (float)rt.width, 0f, rt.width - 1), Mathf.Clamp(v * (float)rt.height, 0f, rt.height - 1), 1f, 1f), 0, 0);
		texture2D.Apply();
		RenderTexture.active = active;
		Color pixel = texture2D.GetPixel(0, 0);
		UnityEngine.Object.Destroy(texture2D);
		return pixel.r < 0.6f;
	}

	public Vector2 DebugScreenUV(Vector3 world)
	{
		if (shadowCam == null)
		{
			return Vector2.zero;
		}
		Vector3 vector = shadowCam.WorldToViewportPoint(world);
		return new Vector2(vector.x, vector.y);
	}

	public Transform DebugJunk(string kind)
	{
		if (!(turntable != null))
		{
			return null;
		}
		return turntable.Find("Junk_" + kind);
	}
}
