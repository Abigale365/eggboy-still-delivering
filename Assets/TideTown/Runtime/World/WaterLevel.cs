// WP-08 水位（工程书 5.4）：Set(y, 秒) 平滑改海面高度，所有依赖水位的东西读 WaterLevel.Y。
// 五档：L0 0.00 / L1 0.45 / L2 1.15 / L3 1.60 / L4 3.05。涨水时镜头轻轻拉高看海。
using System;
using UnityEngine;

public sealed class WaterLevel : MonoBehaviour
{
    public static WaterLevel Main;

    public Transform sea;        // 海面平面，Y 跟着水位走
    public float startY = 0f;

    public float Y { get; private set; }
    public float TargetY { get; private set; }
    public bool CliOverride { get; private set; }

    // 涨水过场时给 ChaseCamera 的额外高度（米）
    public float liftHeight = 1.1f;

    public Action<float> OnSettled;

    public static readonly string[] LevelNames = { "L0", "L1", "L2", "L3", "L4" };
    public static readonly float[] LevelYs = { 0f, 0.45f, 1.15f, 1.60f, 3.05f };

    float fromY, duration, elapsed;
    ChaseCamera chase;

    public static float StaticY => Main != null ? Main.Y : 0f;

    void Awake()
    {
        Main = this;
        Y = TargetY = startY;
        ApplySea();
    }

    void Start()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "-water") continue;
            float y = Parse(args[i + 1]);
            if (!float.IsNaN(y))
            {
                CliOverride = true;
                SetInstant(y);
            }
        }
    }

    void OnDestroy()
    {
        if (Main == this) Main = null;
    }

    // "L2" 或直接的米数
    public static float Parse(string s)
    {
        if (string.IsNullOrEmpty(s)) return float.NaN;
        for (int i = 0; i < LevelNames.Length; i++)
            if (string.Equals(s, LevelNames[i], StringComparison.OrdinalIgnoreCase)) return LevelYs[i];
        return float.TryParse(s, out var v) ? v : float.NaN;
    }

    public void SetLevel(string name, float seconds) { Set(Parse(name), seconds); }

    public void SetInstant(float y)
    {
        fromY = TargetY = Y = y;
        duration = elapsed = 0f;
        ApplySea();
        if (OnSettled != null) OnSettled(Y);
    }

    public void Set(float y, float seconds)
    {
        if (Mathf.Abs(y - Y) < 1e-4f) return;
        fromY = Y;
        TargetY = y;
        duration = Mathf.Max(0.01f, seconds);
        elapsed = 0f;
        Debug.Log($"WATER set {fromY:F2}->{y:F2} in {seconds:F1}s");
    }

    void Update()
    {
        if (duration > 0f)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            Y = Mathf.Lerp(fromY, TargetY, Mathf.SmoothStep(0f, 1f, p));
            ApplySea();
            if (p >= 1f)
            {
                Y = TargetY;
                duration = elapsed = 0f;
                ApplySea();
                if (OnSettled != null) OnSettled(Y);
            }
        }
        UpdateCameraLift();
    }

    void ApplySea()
    {
        if (sea == null) return;
        var p = sea.position;
        p.y = Y;
        sea.position = p;
    }

    void UpdateCameraLift()
    {
        if (chase == null) chase = FindObjectOfType<ChaseCamera>();
        if (chase == null) return;
        float lift = 0f;
        if (duration > 0f && TargetY > fromY)
        {
            float p = Mathf.Clamp01(elapsed / duration);
            lift = Mathf.Sin(p * Mathf.PI) * liftHeight;
        }
        chase.extraLift = Mathf.MoveTowards(chase.extraLift, lift, Time.deltaTime * 3f);
    }
}
