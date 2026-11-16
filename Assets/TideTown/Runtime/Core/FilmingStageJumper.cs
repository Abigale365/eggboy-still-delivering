using System;
using UnityEngine;

public sealed class FilmingStageJumper : MonoBehaviour
{
    GameBootstrap boot;
    bool exitArmed;
    int currentStage;
    Vector3 exitDirection;
    public static bool ActiveSession { get; private set; }
    public static bool AuditArguments() => Array.Exists(Environment.GetCommandLineArgs(),
        a => a.IndexOf("audit", StringComparison.OrdinalIgnoreCase) >= 0 || a.StartsWith("-innov", StringComparison.OrdinalIgnoreCase));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (AuditArguments() || Array.Exists(Environment.GetCommandLineArgs(), a => a == "-trailerRecord")) return;
        new GameObject("Filming F5-F9").AddComponent<FilmingStageJumper>();
    }

    void Update()
    {
        if (boot == null) boot = FindObjectOfType<GameBootstrap>();
        if (boot == null || boot.director == null || boot.director.IsAuditMode) return;
        if (currentStage == 9 && EasyInputBridge.Down(KeyCode.B))
        {
            var chase = FindObjectOfType<ChaseCamera>(); if (chase != null) chase.enabled = true;
            currentStage = 0; boot.player.locked = false; boot.director.DescendTower();
        }
        for (int n = 5; n <= 9; n++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.F5 + n - 5))) Jump(n);
        if (exitArmed && EasyInputBridge.Down(KeyCode.E))
        {
            exitArmed = false;
            boot.player.PlayEmote("open_lighthouse_door");
            boot.player.FaceTowards(boot.player.transform.position + exitDirection * 2f);
            StartCoroutine(ExitPostOffice());
        }
    }

    System.Collections.IEnumerator ExitPostOffice()
    {
        var start = boot.player.transform.position;
        float elapsed = 0f;
        while (elapsed < 1.2f)
        {
            float dt = Mathf.Min(Time.deltaTime, .1f);
            elapsed += dt;
            var at = start + exitDirection * (Mathf.Clamp01(elapsed / 1.2f) * 1.4f);
            at.y = boot.world.GroundAt(at.x, at.z);
            boot.player.PuppetStep(at, boot.player.yaw, exitDirection * 1.2f, true, dt);
            yield return null;
        }
        boot.player.locked = false;
    }

    void Jump(int stage)
    {
        if (ObjectInspectModal.IsOpen || WoodenMechanismPuzzleModal.IsOpen || MenuController.OverlayOpen) return;
        ActiveSession = true;
        boot.ui.SendMessage("StartFromTitle", SendMessageOptions.DontRequireReceiver);
        if (boot.intro != null) boot.intro.SendMessage("finish", true, SendMessageOptions.DontRequireReceiver);
        var print = FindObjectOfType<PerspectivePrintSystem>();
        if (print != null) print.CloseViewfinder();
        AIAgentCalibrationDirector.DebugSetConsole(false);
        boot.ui.SetPaused(false);
        boot.director.PrepareFilmingStage(stage);
        currentStage = stage;
        exitArmed = stage == 5;
        if (exitArmed) boot.player.locked = true;
        exitDirection = Vector3.ProjectOnPlane(boot.player.transform.forward, Vector3.up).normalized;
        boot.ui.ShowToast(stage == 9 ? "实拍：转动旋钮扫海 · S5 闪灯 · B 下塔" : "实拍 F" + stage + " · S5 / 旋钮按下互动", 4);
    }

    void LateUpdate()
    {
        if ((currentStage != 9 && currentStage != 5) || boot == null || boot.director == null || boot.director.IsAuditMode) return;
        var camera = Camera.main;
        if (camera == null) return;
        if (currentStage == 5)
        {
            var chasePost = FindObjectOfType<ChaseCamera>(); if (chasePost != null) chasePost.enabled = false;
            var at = boot.player.transform.position;
            camera.transform.position = at + new Vector3(2f, 1.8f, -4f);
            camera.transform.LookAt(at + Vector3.up * .6f); camera.fieldOfView = 48f;
            return;
        }
        var beacon = FindObjectOfType<BeaconLight>();
        if (beacon == null || beacon.pivot == null || camera == null) return;
        var chase = FindObjectOfType<ChaseCamera>(); if (chase != null) chase.enabled = false;
        Vector3 lamp = beacon.pivot.position;
        camera.transform.position = lamp + new Vector3(4.5f, 2.6f, -6f);
        camera.transform.LookAt(lamp + new Vector3(-2f, 0, 1f));
        camera.fieldOfView = 55f;
    }
}
