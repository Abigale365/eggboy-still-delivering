using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[DefaultExecutionOrder(-9999)]
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

    static bool IsBoardPrimaryKey(KeyCode key)
    {
        return key == KeyCode.W || key == KeyCode.A || key == KeyCode.S || key == KeyCode.D || key == KeyCode.E;
    }

    void Update()
    {
        if (EasyInputReportDecoder.IsFnHeld)
        {
            if (Input.GetKeyDown(KeyCode.W)) { EasyInputReportDecoder.MarkFnUsed(); OnAction("combo-s2"); }
            if (Input.GetKeyDown(KeyCode.E)) { EasyInputReportDecoder.MarkFnUsed(); OnAction("combo-s3"); }
            if (Input.GetKeyDown(KeyCode.A)) { EasyInputReportDecoder.MarkFnUsed(); OnAction("combo-s5"); }
            if (Input.GetKeyDown(KeyCode.S)) { EasyInputReportDecoder.MarkFnUsed(); OnAction("combo-s6"); }
            if (Input.GetKeyDown(KeyCode.D)) { EasyInputReportDecoder.MarkFnUsed(); OnAction("combo-s7"); }
            return;
        }

        if (WoodenMechanismPuzzleModal.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.W)) HandlePuzzle("s2");
            if (Input.GetKeyDown(KeyCode.E)) HandlePuzzle("s3");
            if (Input.GetKeyDown(KeyCode.A)) HandlePuzzle("s5");
            if (Input.GetKeyDown(KeyCode.S)) HandlePuzzle("s6");
            if (Input.GetKeyDown(KeyCode.D)) HandlePuzzle("s7");
            return;
        }

        if (Input.GetKeyDown(KeyCode.W)) ExtendHold(KeyCode.W, 0.14f);
        if (Input.GetKeyDown(KeyCode.A)) ExtendHold(KeyCode.A, 0.14f);
        if (Input.GetKeyDown(KeyCode.S)) ExtendHold(KeyCode.S, 0.14f);
        if (Input.GetKeyDown(KeyCode.D)) ExtendHold(KeyCode.D, 0.14f);
        if (Input.GetKeyDown(KeyCode.E)) ExtendHold(KeyCode.E, 1.0f);
    }

    static void ExtendHold(KeyCode key, float duration)
    {
        float until = Time.unscaledTime + duration;
        if (!heldUntil.TryGetValue(key, out float prev) || until > prev)
            heldUntil[key] = until;
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
            // 按住旋钮 + 按键（组合技能）
            case "combo-s1": Pulse(KeyCode.Tab, 0); break;
            case "combo-s2": Pulse(KeyCode.R, 0); break;
            case "combo-s3": Pulse(KeyCode.H, 0); break;
            case "combo-s4": Pulse(KeyCode.B, 0); break;
            case "combo-s5": Pulse(KeyCode.F, 0); break;
            case "combo-s6": Pulse(KeyCode.V, 0); break;
            case "combo-s7": Pulse(KeyCode.C, 0); break;
            case "combo-s8": Pulse(KeyCode.Tab, 0); break;
            // 标准键盘倒 T 形 WASD + 紧邻 Esc / E / M / Space
            case "s1": Pulse(KeyCode.Escape, 0); break;
            case "s2": Pulse(KeyCode.W, .20f); break;
            case "s3": Pulse(KeyCode.E, 1f); break;
            case "s4": Pulse(KeyCode.M, 0); break;
            case "s5": Pulse(KeyCode.A, .20f); break;
            case "s6": Pulse(KeyCode.S, .20f); break;
            case "s7": Pulse(KeyCode.D, .20f); break;
            case "s8": Pulse(KeyCode.Space, 0); break;
            case "dial-press": Pulse(KeyCode.E, 1f); break;
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
            case "s4": Pulse(KeyCode.Escape, 0); return true;
            case "s5":
            case "combo-s1": WoodenMechanismPuzzleModal.ActiveRingSelection = 0; return true;
            case "s6":
            case "combo-s2": WoodenMechanismPuzzleModal.ActiveRingSelection = 1; return true;
            case "s7":
            case "combo-s3": WoodenMechanismPuzzleModal.ActiveRingSelection = 2; return true;
            case "dial-left":
            case "dial-right":
                WoodenMechanismPuzzleModal.ExternalRotateRing(
                    WoodenMechanismPuzzleModal.ActiveRingSelection, action == "dial-left" ? -1 : 1);
                return true;
            case "s8":
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
        bool rawDown = !(EasyInputReportDecoder.IsFnHeld && IsBoardPrimaryKey(key)) && Input.GetKeyDown(key);
        return rawDown ||
            (pressed.TryGetValue(key, out int frame) && frame == Time.frameCount);
    }

    public static bool Held(KeyCode key)
    {
        bool rawHeld = !(EasyInputReportDecoder.IsFnHeld && IsBoardPrimaryKey(key)) && Input.GetKey(key);
        return rawHeld ||
            (heldUntil.TryGetValue(key, out float time) && time > Time.unscaledTime);
    }
}
