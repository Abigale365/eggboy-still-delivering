using System;
using UnityEngine;

public sealed class WaterLevel : MonoBehaviour
{
	public const float StationRiseSeconds = 4f;

	public static WaterLevel Main;

	public Transform sea;

	public float startY;

	public float liftHeight = 1.1f;

	public Action<float> OnSettled;

	public static readonly string[] LevelNames = new string[5] { "L0", "L1", "L2", "L3", "L4" };

	public static readonly float[] LevelYs = new float[5] { 0f, 0.45f, 1.15f, 1.6f, 1.6f };

	private float fromY;

	private float duration;

	private float elapsed;

	private ChaseCamera chase;

	public float Y { get; private set; }

	public float TargetY { get; private set; }

	public bool CliOverride { get; private set; }

	public static float MaxY => LevelYs[LevelYs.Length - 1];

	public static float StaticY
	{
		get
		{
			if (!(Main != null))
			{
				return 0f;
			}
			return Main.Y;
		}
	}

	private void Awake()
	{
		Main = this;
		float y = (TargetY = startY);
		Y = y;
		ApplySea();
	}

	private void Start()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i + 1 < commandLineArgs.Length; i++)
		{
			if (!(commandLineArgs[i] != "-water"))
			{
				float num = Parse(commandLineArgs[i + 1]);
				if (!float.IsNaN(num))
				{
					CliOverride = true;
					SetInstant(num);
				}
			}
		}
	}

	private void OnDestroy()
	{
		if (Main == this)
		{
			Main = null;
		}
	}

	public static float Parse(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return float.NaN;
		}
		for (int i = 0; i < LevelNames.Length; i++)
		{
			if (string.Equals(s, LevelNames[i], StringComparison.OrdinalIgnoreCase))
			{
				return LevelYs[i];
			}
		}
		if (!float.TryParse(s, out var result))
		{
			return float.NaN;
		}
		return result;
	}

	public void SetLevel(string name, float seconds)
	{
		Set(Parse(name), seconds);
	}

	public void SetInstant(float y)
	{
		float num2 = (Y = y);
		float num4 = (TargetY = num2);
		fromY = num4;
		duration = (elapsed = 0f);
		ApplySea();
		if (OnSettled != null)
		{
			OnSettled(Y);
		}
	}

	public void Set(float y, float seconds)
	{
		if (!(Mathf.Abs(y - Y) < 0.0001f))
		{
			fromY = Y;
			TargetY = y;
			duration = Mathf.Max(0.01f, seconds);
			elapsed = 0f;
			Debug.Log($"WATER set {fromY:F2}->{y:F2} in {seconds:F1}s");
		}
	}

	private void Update()
	{
		if (duration > 0f)
		{
			elapsed += Time.deltaTime;
			float num = Mathf.Clamp01(elapsed / duration);
			Y = Mathf.Lerp(fromY, TargetY, Mathf.SmoothStep(0f, 1f, num));
			ApplySea();
			if (num >= 1f)
			{
				Y = TargetY;
				duration = (elapsed = 0f);
				ApplySea();
				if (OnSettled != null)
				{
					OnSettled(Y);
				}
			}
		}
		UpdateCameraLift();
	}

	private void ApplySea()
	{
		if (!(sea == null))
		{
			Vector3 position = sea.position;
			position.y = Y;
			sea.position = position;
		}
	}

	private void UpdateCameraLift()
	{
		if (chase == null)
		{
			chase = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
		}
		if (!(chase == null))
		{
			float target = 0f;
			if (duration > 0f && TargetY > fromY)
			{
				target = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * MathF.PI) * liftHeight;
			}
			chase.extraLift = Mathf.MoveTowards(chase.extraLift, target, Time.deltaTime * 3f);
		}
	}
}
