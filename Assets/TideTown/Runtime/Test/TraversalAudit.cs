using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class TraversalAudit : MonoBehaviour
{
	[Serializable]
	private sealed class Check
	{
		public string name;

		public bool passed;

		public Vector3 start;

		public Vector3 target;

		public Vector3 end;

		public float progress;

		public float maxY;

		public int firstFrame;

		public int lastFrame;
	}

	[Serializable]
	private sealed class Report
	{
		public string status;

		public Check[] checks;
	}

	private const float Dt = 1f / 60f;

	private readonly List<Check> checks = new List<Check>();

	private GameBootstrap boot;

	private SafePoints rescue;

	private string output;

	private Camera cameraToRecord;

	private RenderTexture frame;

	private Texture2D pixels;

	private int frameNumber;

	public void Begin(GameBootstrap game)
	{
		boot = game;
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return null;
		PrototypeGame prototypeGame = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
		if (prototypeGame != null)
		{
			prototypeGame.enabled = false;
		}
		TidalWindChallenge.Suppress = true;
		rescue = UnityEngine.Object.FindObjectOfType<SafePoints>();
		if (rescue != null)
		{
			rescue.Enabled = false;
		}
		boot.water.SetInstant(0f);
		output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "traversal"));
		Directory.CreateDirectory(output);
		if (Array.Exists(Environment.GetCommandLineArgs(), (string arg) => arg == "-traversalvideo"))
		{
			BeginRecording();
		}
		yield return BothWays("market_east_gap", 1.1f, 0.5f, 3.65f, 0.5f);
		yield return BothWays("market_west_grass", -2.77f, 0.8f, -4.27f, 0.8f);
		yield return BothWays("bakery_back_lawn", -11.71f, -4.36f, -11.71f, -2.86f);
		yield return BothWays("school_lawn", 8.54f, -6.13f, 7.04f, -6.13f);
		yield return BothWays("housing_west", -23.66f, 16.75f, -24.72f, 17.81f);
		yield return BothWays("housing_north", 1.39f, 24.22f, 0.33f, 23.16f);
		yield return BothWays("housing_east", 19.6f, 20.08f, 21.1f, 20.08f);
		yield return BothWays("dock_seafront", 22.75f, -17.85f, 24.9f, -17.65f);
		yield return Motion("market_wall", 4.45f, 0.15f, 4.45f, 2.1f, run: true, block: true, jump: false, record: true);
		yield return Motion("market_fence", 1.3f, -1.75f, 3.1f, -1.75f, run: true, block: true, jump: false, record: true);
		yield return Motion("market_corner_slide", 2.22f, 0.46f, 5.1f, 0.64f, run: true, block: false, jump: false, record: true, slide: true);
		yield return Motion("housing_cliff", -3.95f, 10.41f, -3.95f, 11.66f, run: true, block: true, jump: false, record: true);
		yield return TreeTrunk();
		yield return LowStep();
		yield return Beach();
		string text = (checks.All((Check check) => check.passed) ? "passed" : "failed");
		File.WriteAllText(Path.Combine(output, "traversal-audit.json"), JsonUtility.ToJson(new Report
		{
			status = text,
			checks = checks.ToArray()
		}, prettyPrint: true));
		Debug.Log($"TRAVERSAL {text} passed={checks.Count((Check check) => check.passed)}/{checks.Count}");
		if (frame != null)
		{
			frame.Release();
		}
		Application.Quit((!(text == "passed")) ? 2 : 0);
	}

	private IEnumerator BothWays(string name, float x0, float z0, float x1, float z1, bool record = true)
	{
		yield return Motion(name + "_walk", x0, z0, x1, z1, run: false, block: false, jump: false, record);
		yield return Motion(name + "_run_back", x1, z1, x0, z0, run: true, block: false, jump: false, record);
	}

	private IEnumerator Motion(string name, float x0, float z0, float x1, float z1, bool run, bool block = false, bool jump = false, bool record = false, bool slide = false)
	{
		int firstFrame = (record ? frameNumber : (-1));
		Vector3 start = Ground(x0, z0);
		Vector3 target = Ground(x1, z1);
		Vector3 vector = new Vector3(x1 - x0, 0f, z1 - z0);
		float distance = vector.magnitude;
		Vector3 direction = vector / distance;
		Position(start, direction);
		bool validStart = boot.world.Walkable(x0, z0);
		float yMax = start.y;
		float progress = 0f;
		float side = 0f;
		int limit = Mathf.CeilToInt((distance / (run ? 1.8f : 0.6f) + 0.9f) / (1f / 60f));
		for (int i = 0; i < limit; i++)
		{
			boot.player.Move(1f / 60f, direction.x, direction.z, run, jump && i == 8);
			Vector3 vector2 = boot.player.transform.position - start;
			yMax = Mathf.Max(yMax, start.y + vector2.y);
			vector2.y = 0f;
			progress = Vector3.Dot(vector2, direction);
			side = Mathf.Max(side, (vector2 - progress * direction).magnitude);
			if (record && i % 8 == 0)
			{
				Record(direction);
			}
			if (!block && progress >= distance - 0.09f)
			{
				break;
			}
			if (i % 5 == 0)
			{
				yield return null;
			}
		}
		if (record)
		{
			Record(direction);
		}
		bool passed = validStart && ((!block) ? (progress >= distance - 0.09f && side < 0.38f && (!slide || side > 0.05f) && (!jump || yMax - start.y >= 0.27f)) : (progress > 0.12f && progress < distance - 0.24f));
		Add(name, passed, start, target, progress, yMax, firstFrame);
	}

	private IEnumerator TreeTrunk()
	{
		Vector2 terraceTree = new Vector2(10.4985f, 8.7201f);
		IOrderedEnumerable<MeshRenderer> orderedEnumerable = from tree in UnityEngine.Object.FindObjectsOfType<MeshRenderer>()
			where tree.enabled && tree.sharedMaterials.Any((Material material) => material != null && material.name.StartsWith("NatureTrunk", StringComparison.Ordinal))
			orderby (new Vector2(tree.transform.position.x, tree.transform.position.z) - terraceTree).sqrMagnitude
			select tree;
		foreach (MeshRenderer item in orderedEnumerable)
		{
			Vector3 position = item.transform.position;
			if (position.y < 2.9f || position.y > 5.6f || !boot.world.InLawnArea(position.x, position.z))
			{
				continue;
			}
			Vector3 start = Ground(position.x + 1.2f, position.z);
			if (Mathf.Abs(start.y - position.y) > 0.2f || !boot.world.Walkable(start.x, start.z) || boot.world.Colliders.Any((EggboyCollider c) => c.r.HasValue && c.top.HasValue && c.top.Value > start.y + 0.22f && Vector2.Distance(new Vector2(c.x, c.z), new Vector2(start.x, start.z)) < c.r.Value + 0.37f))
			{
				continue;
			}
			Vector3 center = new Vector3(position.x, start.y, position.z);
			Position(start, Vector3.left);
			int firstFrame = frameNumber;
			float nearest = 1.2f;
			for (int i = 0; i < 150; i++)
			{
				Vector3 vector = center - boot.player.transform.position;
				vector.y = 0f;
				Vector3 heading = ((vector.sqrMagnitude > 0.0001f) ? vector.normalized : Vector3.left);
				boot.player.Move(1f / 60f, heading.x, heading.z, run: true, jumpPressed: false);
				Vector3 position2 = boot.player.transform.position;
				nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(position2.x, position2.z), new Vector2(center.x, center.z)));
				if (i % 8 == 0)
				{
					Record(heading);
				}
				if (i % 5 == 0)
				{
					yield return null;
				}
			}
			Add("tree_trunk", nearest >= 0.45f && nearest <= 0.9f, start, center, 1.2f - nearest, start.y, firstFrame);
			Debug.Log($"TRAVERSAL tree_trunk_nearest={nearest:F3}");
			yield break;
		}
		Add("tree_trunk", passed: false, Vector3.zero, Vector3.zero, 0f, 0f);
	}

	private IEnumerator LowStep()
	{
		Vector3 vector = Ground(2.34f, 0.5f);
		GameObject go = new GameObject("Traversal audit 30cm step");
		go.layer = LayerMask.NameToLayer("Ground");
		go.transform.position = vector + Vector3.up * 0.15f;
		go.AddComponent<BoxCollider>().size = new Vector3(0.35f, 0.3f, 0.8f);
		EggboyCollider collider = new EggboyCollider
		{
			x = vector.x,
			z = vector.z,
			hw = 0.175f,
			hd = 0.4f,
			top = vector.y + 0.3f
		};
		boot.world.AddCollider(collider);
		Physics.SyncTransforms();
		yield return Motion("low_step_requires_jump", 1.25f, 0.5f, 3.55f, 0.5f, run: true, block: true);
		yield return Motion("low_step_jump", 1.64f, 0.5f, 3.55f, 0.5f, run: true, block: false, jump: true);
		boot.world.RemoveCollider(collider);
		UnityEngine.Object.Destroy(go);
		yield return null;
	}

	private IEnumerator Beach()
	{
		boot.water.SetInstant(WaterLevel.LevelYs[1]);
		Vector3 direction = Vector3.back;
		Vector3 start = Ground(4.8f, -12.7f);
		bool validStart = boot.world.Walkable(start.x, start.z) && start.y > 2.8f;
		Position(start, direction);
		float num = start.z;
		for (int l = 0; l < 90; l++)
		{
			boot.player.Move(1f / 60f, 0f, -1f, run: true, jumpPressed: false);
			num = Mathf.Min(num, boot.player.transform.position.z);
		}
		Vector3 position = boot.player.transform.position;
		float num2 = ((boot.world.beachPoly != null) ? boot.world.beachPoly.y : 0f);
		Add("beach_walk_edge_holds", validStart && position.y > num2 + 0.6f && !boot.world.IsBeachSurface(position.x, position.z, position.y), start, Ground(start.x, -14.5f), start.z - num, start.y);
		Position(start, direction);
		int jumpFrames = frameNumber;
		bool landed = false;
		float maxY = start.y;
		float jumpFromY = start.y;
		int k;
		for (k = 0; k < 180; k++)
		{
			if (k == 24)
			{
				jumpFromY = boot.player.transform.position.y;
				maxY = jumpFromY;
			}
			boot.player.Move(1f / 60f, 0f, -1f, run: true, k == 24);
			Vector3 position2 = boot.player.transform.position;
			maxY = Mathf.Max(maxY, position2.y);
			if (k % 8 == 0)
			{
				Record(direction);
			}
			if (boot.player.grounded && boot.world.IsBeachSurface(position2.x, position2.z, position2.y))
			{
				landed = true;
				break;
			}
			if (k % 5 == 0)
			{
				yield return null;
			}
		}
		Vector3 landing = boot.player.transform.position;
		Vector2 openSand = new Vector2(-3.8f, -16.6f);
		float walked = 0f;
		k = 0;
		while (landed && k < 300)
		{
			Vector3 position3 = boot.player.transform.position;
			Vector2 normalized = (openSand - new Vector2(position3.x, position3.z)).normalized;
			boot.player.Move(1f / 60f, normalized.x, normalized.y, run: true, jumpPressed: false);
			position3 = boot.player.transform.position;
			if (k % 8 == 0)
			{
				Record(new Vector3(normalized.x, 0f, normalized.y));
			}
			walked = Vector2.Distance(new Vector2(position3.x, position3.z), new Vector2(landing.x, landing.z));
			if (walked > 1f)
			{
				break;
			}
			if (k % 5 == 0)
			{
				yield return null;
			}
			k++;
		}
		Vector3 sand = boot.player.transform.position;
		Add("beach_jump_land_and_walk", validStart && landed && maxY > jumpFromY + 0.27f && walked > 1f && boot.world.IsBeachSurface(sand.x, sand.z, sand.y), start, landing, walked, maxY, jumpFrames);
		if (landed && rescue != null)
		{
			k = frameNumber;
			int count = rescue.RescueCount;
			rescue.Enabled = true;
			float deadline = Time.realtimeSinceStartup + 10f;
			float nextFrame = Time.realtimeSinceStartup;
			while (rescue.RescueCount == count && Time.realtimeSinceStartup < deadline)
			{
				Vector3 position4 = boot.player.transform.position;
				Vector2 normalized2 = new Vector2(start.x - position4.x, start.z - position4.z).normalized;
				boot.player.Move(Mathf.Min(Time.deltaTime, 0.05f), normalized2.x, normalized2.y, run: true, jumpPressed: false);
				if (Time.realtimeSinceStartup >= nextFrame)
				{
					Record(Vector3.forward);
					nextFrame += 2f / 15f;
				}
				yield return null;
			}
			Record(Vector3.forward);
			Vector3 position5 = boot.player.transform.position;
			Add("beach_return_rescue", rescue.RescueCount == count + 1 && rescue.LastReason == "beach" && position5.y >= WaterLevel.StaticY && boot.world.Walkable(position5.x, position5.z) && !boot.world.IsBeachSurface(position5.x, position5.z, position5.y), sand, rescue.LastTarget, 0f, position5.y, k);
			rescue.Enabled = false;
		}
		else
		{
			Add("beach_return_rescue", passed: false, sand, start, 0f, sand.y);
		}
		boot.water.SetInstant(0f);
		int num3 = SandCells();
		boot.water.SetInstant(0.45f);
		int num4 = SandCells();
		Add("flooded_beach_wadable", num3 > 0 && num4 == num3, start, start, num4, 0.45f);
		boot.water.SetInstant(1.15f);
		int num5 = SandCells();
		Add("deep_beach_closed", num5 == 0, start, start, num5, 1.15f);
		yield return null;
		int SandCells()
		{
			WalkPoly beachPoly = boot.world.beachPoly;
			if (beachPoly == null || beachPoly.points == null)
			{
				return 0;
			}
			float num6 = float.MaxValue;
			float num7 = float.MinValue;
			float num8 = float.MaxValue;
			float num9 = float.MinValue;
			Vector2[] points = beachPoly.points;
			for (int m = 0; m < points.Length; m++)
			{
				Vector2 vector = points[m];
				num6 = Mathf.Min(num6, vector.x);
				num7 = Mathf.Max(num7, vector.x);
				num8 = Mathf.Min(num8, vector.y);
				num9 = Mathf.Max(num9, vector.y);
			}
			int num10 = 0;
			for (float num11 = num6; num11 <= num7; num11 += 0.5f)
			{
				for (float num12 = num8; num12 <= num9; num12 += 0.5f)
				{
					if (boot.world.Walkable(num11, num12) && boot.world.IsBeachSurface(num11, num12, boot.world.GroundAt(num11, num12)))
					{
						num10++;
					}
				}
			}
			return num10;
		}
	}

	private Vector3 Ground(float x, float z)
	{
		return new Vector3(x, boot.world.GroundAt(x, z), z);
	}

	private void Position(Vector3 p, Vector3 direction)
	{
		boot.player.transform.SetPositionAndRotation(p, Quaternion.LookRotation(direction));
		boot.player.yaw = Mathf.Atan2(direction.x, direction.z);
		boot.player.vel = Vector3.zero;
		boot.player.locked = false;
		boot.player.grounded = true;
		boot.player.jump = new JumpState();
		boot.player.ResetJumpInput();
	}

	private void Add(string name, bool passed, Vector3 start, Vector3 target, float progress, float height, int firstFrame = -1)
	{
		Vector3 position = boot.player.transform.position;
		checks.Add(new Check
		{
			name = name,
			passed = passed,
			start = start,
			target = target,
			end = position,
			progress = progress,
			maxY = height,
			firstFrame = firstFrame,
			lastFrame = ((firstFrame >= 0) ? (frameNumber - 1) : (-1))
		});
		Debug.Log($"TRAVERSAL {name}={passed} progress={progress:F2} start={start} end={position}");
	}

	private void BeginRecording()
	{
		cameraToRecord = Camera.main;
		if (!(cameraToRecord == null))
		{
			cameraToRecord.enabled = false;
			frame = new RenderTexture(854, 480, 24, RenderTextureFormat.ARGB32);
			pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, mipChain: false);
		}
	}

	private void Record(Vector3 heading)
	{
		if (!(cameraToRecord == null) && !(frame == null))
		{
			Vector3 position = boot.player.transform.position;
			Vector3 vector = Vector3.Cross(Vector3.up, heading);
			cameraToRecord.transform.position = position - heading * 2.4f + vector * 0.65f + Vector3.up * 2.4f;
			cameraToRecord.transform.LookAt(position + heading * 0.35f + Vector3.up * 0.5f);
			cameraToRecord.targetTexture = frame;
			cameraToRecord.Render();
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = frame;
			pixels.ReadPixels(new Rect(0f, 0f, frame.width, frame.height), 0, 0);
			pixels.Apply();
			RenderTexture.active = active;
			File.WriteAllBytes(Path.Combine(output, $"frame_{frameNumber++:D4}.jpg"), pixels.EncodeToJPG(88));
		}
	}
}
