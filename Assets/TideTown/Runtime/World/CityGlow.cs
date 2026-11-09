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
        var prefab = Resources.Load<GameObject>("Ending/NewPostOffice");
        if (prefab != null)
        {
            var office = Instantiate(prefab, transform);
            office.name = "NewPostOffice";
            office.transform.localPosition = Vector3.down * 2f;
            var direction = Vector3.ProjectOnPlane(-transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.01f)
                office.transform.rotation = Quaternion.LookRotation(direction);
            office.transform.localScale *= 2f;
            foreach (var collider in office.GetComponentsInChildren<Collider>()) collider.enabled = false;

            var porch = new GameObject("NewPostOfficeWelcomeLight");
            porch.transform.SetParent(transform, false);
            porch.transform.localPosition = Vector3.up * 3f + direction.normalized * 5f;
            var light = porch.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color32(255, 206, 147, 255);
            light.range = 18f;
            light.intensity = 2.1f;
        }
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
