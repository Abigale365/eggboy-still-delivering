// WP-15：山那边的新城灯光。结局过场时亮起，其中一盏手电来回晃。
using UnityEngine;

public sealed class CityGlow : MonoBehaviour
{
    public Renderer[] windows = new Renderer[0];
    public Transform signal;
    public float swing = 1.1f;

    public bool Shown { get; private set; }

    Vector3 signalBase;

    void Awake()
    {
        if (windows != null)
            foreach (var w in windows) if (w != null) w.enabled = false;
        if (signal != null)
        {
            signalBase = signal.localPosition;
            signal.gameObject.SetActive(false);
        }
    }

    public void Show()
    {
        if (Shown) return;
        Shown = true;
        if (windows != null)
            foreach (var w in windows) if (w != null) w.enabled = true;
        if (signal != null) signal.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!Shown || signal == null) return;
        signal.localPosition = signalBase + new Vector3(Mathf.Sin(Time.time * 1.8f) * swing, 0f, 0f);
    }
}
