using System;
using UnityEngine;

public sealed class FilmingStageJumper : MonoBehaviour
{
    GameBootstrap boot;
    bool exitArmed;
    Coroutine exitRoutine;
    int currentStage;
    int simpleShot;
    Vector3 exitDirection;
    public static bool ActiveSession { get; private set; }
    public static bool AuditArguments() => Array.Exists(Environment.GetCommandLineArgs(),
        a => a.IndexOf("audit", StringComparison.OrdinalIgnoreCase) >= 0 || a.StartsWith("-innov", StringComparison.OrdinalIgnoreCase));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (AuditArguments() || Array.Exists(Environment.GetCommandLineArgs(), a => a == "-trailerRecord")) return;
        if (FindObjectOfType<FilmingStageJumper>() != null) return;
        var go = new GameObject("Filming F5-F9");
        DontDestroyOnLoad(go);
        go.AddComponent<FilmingStageJumper>();
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
        if (Input.GetKeyDown(KeyCode.F10)) NextSimpleShot();
        if (exitArmed && EasyInputBridge.Down(KeyCode.E))
        {
            exitArmed = false;
            boot.player.PlayEmote("open_lighthouse_door");
            boot.player.FaceTowards(boot.player.transform.position + exitDirection * 2f);
            exitRoutine = StartCoroutine(ExitPostOffice());
        }
    }

    void NextSimpleShot()
    {
            simpleShot = simpleShot % 5 + 1;
            Jump(new[] { 6, 6, 8, 9, 10 }[simpleShot - 1]);
            boot.director.PrepareSimpleFilmingShot(simpleShot);
            string[] tips = {
                "实拍 1/5 · 直接对着开发板吹一口气（吹散盐霜）",
                "实拍 2/5 · 对着开发板吹气（吹旺炉火冒烟）",
                "实拍 3/5 · 拧旋钮对准缺口，按一下旋钮搭桥",
                "实拍 4/5 · 拧旋钮转灯塔，对屏幕挥手（或按 S5）",
                "实拍 5/5 · 聆听最后一封信配音（再按 F10 回第 1 镜）" };
            boot.ui.ShowToast(tips[simpleShot - 1], 2.2f);
    }

    System.Collections.IEnumerator Start()
    {
        if (!Array.Exists(Environment.GetCommandLineArgs(), a => a == "-filmingcheck")) yield break;
        Application.runInBackground = true;
        while (boot == null || boot.director == null) yield return null;
        yield return new WaitForSecondsRealtime(3);
        string folder = System.IO.Path.Combine(Application.dataPath, "..", "filming-check");
        System.IO.Directory.CreateDirectory(folder);
        for (int shot = 1; shot <= 6; shot++)
        {
            NextSimpleShot();
            yield return new WaitForSecondsRealtime(2.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "shot-" + shot + ".png"));
            yield return new WaitForEndOfFrame();
            Debug.Log("FILMINGCHECK shot=" + simpleShot + " inspect=" + ObjectInspectModal.IsOpen + " print=" + (FindObjectOfType<PerspectivePrintSystem>()?.IsViewfinderActive ?? false) + " letter=" + boot.ui.LetterOpen);
            if (shot == 1) ObjectInspectModal.ExternalClearDust(true);
            if (shot == 2) boot.director.BoostOvenBellows(FindObjectOfType<BakeryEffects>());
            if (shot == 3) { var print = FindObjectOfType<PerspectivePrintSystem>(); print.Shutter(); Debug.Log("FILMINGCHECK bridge=" + print.LastPrintSnapped); }
            if (shot == 4)
            {
                for (int flash = 0; flash < 3; flash++)
                {
                    boot.director.SendMessage("TriggerSignalFlash");
                    yield return new WaitForSecondsRealtime(.9f);
                }
                Debug.Log("FILMINGCHECK replyLights=" + FindObjectOfType<FlashlightReply>().LitCount);
            }
            if (shot == 5) yield return new WaitForSecondsRealtime(TownVoiceover.Length("vo_letter_zhou_eggboy") + 1f);
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "after-" + shot + ".png"));
            yield return new WaitForEndOfFrame();
        }
        Application.Quit();
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
        if (exitRoutine != null) StopCoroutine(exitRoutine);
        exitArmed = false;
        ObjectInspectModal.DebugClose();
        WoodenMechanismPuzzleModal.DebugClose();
        WishInspectModal.DebugPutBack();
        FindObjectOfType<AvantGardeGameplayHub>()?.CloseForFilming();
        FindObjectOfType<TownMap>()?.Close();
        boot.ui.CloseLetter();
        MenuController.NoteScriptUnlock();
        TownVoiceover.StopAll();
        ActiveSession = true;
        if (TitleScreen.Current != null) Destroy(TitleScreen.Current.gameObject);
        var tut = FindObjectOfType<BasicsTutorial>();
        if (tut != null) { tut.enabled = false; Destroy(tut); }
        boot.ui.SendMessage("StartFromTitle", SendMessageOptions.DontRequireReceiver);
        if (boot.intro != null) boot.intro.SendMessage("finish", true, SendMessageOptions.DontRequireReceiver);
        var print = FindObjectOfType<PerspectivePrintSystem>();
        if (print != null) print.CloseViewfinder();
        AIAgentCalibrationDirector.DebugSetConsole(false);
        boot.ui.SetPaused(false);
        boot.ui.ResetCinematicForFilming();
        boot.director.PrepareFilmingStage(stage);
        currentStage = stage;
        exitArmed = stage == 5;
        if (exitArmed) boot.player.locked = true;
        exitDirection = Vector3.ProjectOnPlane(boot.player.transform.forward, Vector3.up).normalized;
        boot.ui.ShowToast(stage == 9 ? "实拍：转动旋钮扫海 · S5 闪灯 · B 下塔" : "实拍 F" + stage + " · S5 / 旋钮按下互动", 4);
    }

    void LateUpdate()
    {
        if (simpleShot == 2 && currentStage == 6 && boot != null)
        {
            var oven = FindObjectOfType<BakeryEffects>();
            var cam = Camera.main;
            if (oven != null && cam != null)
            {
                var chaseOven = FindObjectOfType<ChaseCamera>();
                if (chaseOven != null) chaseOven.enabled = false;
                Vector3 focus = oven.transform.position + Vector3.up * 1.8f;
                cam.transform.position = oven.transform.TransformPoint(new Vector3(2.5f, 2.4f, -5f));
                cam.transform.LookAt(focus); cam.fieldOfView = 48f;
            }
            return;
        }
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
