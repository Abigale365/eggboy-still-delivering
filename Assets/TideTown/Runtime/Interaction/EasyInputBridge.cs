using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

public sealed class EasyInputBridge : MonoBehaviour
{
    static readonly Dictionary<KeyCode, int> pressed = new Dictionary<KeyCode, int>();
    static readonly Dictionary<KeyCode, float> heldUntil = new Dictionary<KeyCode, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var bridge = new GameObject("EasyInputBridge");
        DontDestroyOnLoad(bridge);
        bridge.AddComponent<EasyInputBridge>();
#endif
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) ResetInput("");
    }

    [Preserve]
    public void ResetInput(string ignored)
    {
        pressed.Clear();
        heldUntil.Clear();
    }

    [Preserve]
    public void OnAction(string action)
    {
        if (WoodenMechanismPuzzleModal.IsOpen && HandlePuzzle(action)) return;
        switch (action)
        {
            case "combo-s4": Pulse(KeyCode.V, 0); break;
            case "combo-s5": Pulse(KeyCode.Tab, 0); break;
            case "combo-s6": Pulse(KeyCode.R, 0); break;
            case "combo-s7": Pulse(KeyCode.B, 0); break;
            case "combo-s8": Pulse(KeyCode.F, 0); break;
            case "s1": Pulse(KeyCode.W, .20f); break;
            case "s2": Pulse(KeyCode.A, .20f); break;
            case "s3": Pulse(KeyCode.S, .20f); break;
            case "s4": Pulse(KeyCode.D, .20f); break;
            case "s5": case "dial-press": Pulse(KeyCode.E, 1f); break;
            case "s6": Pulse(KeyCode.Space, 0); break;
            case "s7": Pulse(KeyCode.M, 0); break;
            case "s8": Pulse(KeyCode.Escape, 0); break;
            case "dial-left": Pulse(KeyCode.A, .12f); break;
            case "dial-right": Pulse(KeyCode.D, .12f); break;
        }
    }

    static bool HandlePuzzle(string action)
    {
        switch (action)
        {
            case "s1": WoodenMechanismPuzzleModal.ExternalToggleLatch(0); return true;
            case "s2": WoodenMechanismPuzzleModal.ExternalToggleLatch(1); return true;
            case "s3": WoodenMechanismPuzzleModal.ExternalToggleLatch(2); return true;
            case "combo-s1": WoodenMechanismPuzzleModal.ActiveRingSelection = 0; return true;
            case "combo-s2": WoodenMechanismPuzzleModal.ActiveRingSelection = 1; return true;
            case "combo-s3": WoodenMechanismPuzzleModal.ActiveRingSelection = 2; return true;
            case "dial-left":
            case "dial-right":
                WoodenMechanismPuzzleModal.ExternalRotateRing(
                    WoodenMechanismPuzzleModal.ActiveRingSelection, action == "dial-left" ? -1 : 1);
                return true;
            case "s5":
            case "dial-press":
                WoodenMechanismPuzzleModal.ExternalAttemptUnlock();
                return true;
            default: return false;
        }
    }

    static void Pulse(KeyCode key, float duration)
    {
        pressed[key] = Time.frameCount + 1;
        heldUntil[key] = Time.unscaledTime + duration;
    }

    public static bool Down(KeyCode key)
    {
        return Input.GetKeyDown(key) ||
            (pressed.TryGetValue(key, out int frame) && frame == Time.frameCount);
    }

    public static bool Held(KeyCode key)
    {
        return Input.GetKey(key) ||
            (heldUntil.TryGetValue(key, out float time) && time > Time.unscaledTime);
    }
}
