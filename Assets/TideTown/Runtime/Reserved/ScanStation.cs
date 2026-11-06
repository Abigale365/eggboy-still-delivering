// WP-09 预留：邮局门口的扫描站。Features.scan=false 时整站隐藏、不注册、不显示提示。
using UnityEngine;

public sealed class ScanStation : MonoBehaviour, IInteractable
{
    readonly IScanSource source = new NullScanSource();
    string message = "";
    float messageUntil;

    public string EntityId => "scan_station";
    public Vector3 Position => transform.position;

    void Awake() { SetEnabled(Features.scan); }

    public void SetEnabled(bool on)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = on;
    }

    public string Prompt(GameState s) => Time.time < messageUntil ? message : "E　扫描";
    public bool CanInteract(GameState s) => Features.scan;

    public void Interact(GameState s, InteractionService svc)
    {
        source.Capture(bytes => Show("样例已就绪"), err => Show(err));
    }

    void Show(string text)
    {
        message = text;
        messageUntil = Time.time + 6f;
        Debug.Log("SCAN " + text);
    }
}
