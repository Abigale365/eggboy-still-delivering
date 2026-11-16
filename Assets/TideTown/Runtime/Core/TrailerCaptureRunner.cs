using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

// 仅显式命令行激活。全部画面来自当前场景与既有玩法，不读取旧录屏或静帧。
[DefaultExecutionOrder(9000)]
public sealed class TrailerCaptureRunner : MonoBehaviour
{
    [Serializable] sealed class Scene { public string scene_tag; public float start_sec, end_sec; }
    [Serializable] sealed class Manifest { public Scene[] scenes; }
    GameBootstrap b;
    Camera cam;
    Manifest manifest;
    Process encoder;
    Stream pipe;
    RenderTexture capture;
    Texture2D pixels;
    string output;
    int frame, sceneIndex = -1;
    float t, u;
    Vector3 focus, actor, actorEnd;
    float radius = 5, height = 2, orbit = 210;
    bool[] events = new bool[12];
    Text title;
    Image paper;
    CanvasGroup titleGroup, endGroup;
    Transform[] turntables = new Transform[3];
    CanvasGroup assetsGroup;
    WorldMechanismDirector mech;
    WordMaterializerSystem words;
    PerspectivePrintSystem viewfinder;
    float printYaw, printPitch, printHold, printHeldYaw;
    bool printAim;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.FindIndex(args, a => a.Equals("-trailerRecord", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= args.Length || FilmingStageJumper.AuditArguments()) return;
        var runner = new GameObject("Trailer engine capture 1080p30").AddComponent<TrailerCaptureRunner>();
        runner.output = Path.GetFullPath(args[i + 1]);
    }

    IEnumerator Start()
    {
        while ((b = FindObjectOfType<GameBootstrap>()) == null || b.director == null || b.state == null) yield return null;
        Directory.CreateDirectory(output);
        Screen.SetResolution(1920, 1080, false);
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
        b.ui.SendMessage("StartFromTitle", SendMessageOptions.DontRequireReceiver);
        if (b.intro != null) b.intro.SendMessage("finish", true, SendMessageOptions.DontRequireReceiver);
        yield return null;
        b.ui.SetPaused(false);
        b.ui.CloseLetter();
        b.ui.SetCinematic(true, "trailer");
        b.state.flags.Clear(); b.state.stamps.Clear();
        b.state.flags.Add("letters_taken"); b.state.step = "P1_mailbox";
        var dimmer = FindObjectOfType<SkyDimmer>(); if (dimmer != null) dimmer.enabled = false;
        TownLighting.SetFilmingPhase(0);
        cam = Camera.main;
        var chase = FindObjectOfType<ChaseCamera>();
        if (chase != null) chase.enabled = false;
        mech = FindObjectOfType<WorldMechanismDirector>();
        words = FindObjectOfType<WordMaterializerSystem>();
        viewfinder = FindObjectOfType<PerspectivePrintSystem>();
        manifest = JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>("Trailer/timing_manifest_150s").text);
        if (manifest.scenes.Length != 17 || Mathf.Abs(manifest.scenes[16].start_sec - 146) > .01f)
            throw new InvalidDataException("Trailer timing manifest must end at 150s with 146s credit card");
        MakeTitles();
        capture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        capture.Create();
        pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        // Windows D3D CaptureScreenshotIntoRenderTexture 已翻转纹理，原始字节正好是 FFmpeg 的 top-down。
        var psi = new ProcessStartInfo("ffmpeg", "-hide_banner -y -f rawvideo -pixel_format rgb24 -video_size 1920x1080 -framerate 30 -i pipe:0 -an -c:v libx264 -preset fast -crf 16 -pix_fmt yuv420p -movflags +faststart \"" + Path.Combine(output, "raw_150s_1080p.mp4") + "\"");
        psi.UseShellExecute = false; psi.CreateNoWindow = true;
        psi.RedirectStandardInput = true; psi.RedirectStandardError = true;
        encoder = new Process { StartInfo = psi };
        encoder.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (this) File.AppendAllText(Path.Combine(output, "ffmpeg.log"), e.Data + "\n"); };
        encoder.Start(); encoder.BeginErrorReadLine(); pipe = encoder.StandardInput.BaseStream;
        Time.captureFramerate = 30;
        for (frame = 0; frame < 4500; frame++)
        {
            t = frame / 30f;
            int si = 0;
            while (si < 16 && t >= manifest.scenes[si].end_sec) si++;
            if (si != sceneIndex) { sceneIndex = si; Enter(si); }
            var sc = manifest.scenes[si];
            u = Mathf.Clamp01((t - sc.start_sec) / (sc.end_sec - sc.start_sec));
            Direct();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
            var previous = RenderTexture.active; RenderTexture.active = capture;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false); pixels.Apply(false);
            RenderTexture.active = previous;
            byte[] bytes = pixels.GetRawTextureData();
            pipe.Write(bytes, 0, bytes.Length);
            if (frame % 150 == 0)
            {
                // Texture2D 图像编码器的行序与 rawvideo 相反，只对诊断缩略图翻转。
                byte[] preview = (byte[])bytes.Clone();
                const int stride = 1920 * 3;
                for (int row = 0; row < 1080; row++) Buffer.BlockCopy(bytes, row * stride, preview, (1079 - row) * stride, stride);
                pixels.LoadRawTextureData(preview); pixels.Apply(false);
                File.WriteAllBytes(Path.Combine(output, "frame_" + frame.ToString("D4") + ".jpg"), pixels.EncodeToJPG(85));
                File.WriteAllText(Path.Combine(output, "progress.json"), "{\"frame\":" + frame + ",\"total\":4500,\"scene\":" + (si + 1) + "}");
                Debug.Log("TRAILER_FRAME " + frame + " scene=" + (si + 1));
            }
        }
        pipe.Close(); pipe = null;
        while (!encoder.HasExited) yield return null;
        Time.captureFramerate = 0;
        File.WriteAllText(Path.Combine(output, "capture-result.json"), "{\"frames\":4500,\"fps\":30,\"duration\":150,\"encoderExit\":" + encoder.ExitCode + "}");
        Application.Quit(encoder.ExitCode);
    }

    void Mark(string flag) { if (!b.state.flags.Contains(flag)) b.state.flags.Add(flag); }
    Vector3 Ground(Vector3 p) { p.y = b.world.GroundAt(p.x, p.z); return p; }
    Vector3 SafeStand(Vector3 p)
    {
        for (float r = 0; r <= 4f; r += .35f)
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI / 12;
            var q = Ground(p + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            if (b.world.Walkable(q.x, q.z) && q.y > WaterLevel.StaticY + .1f) return q;
        }
        return Ground(p);
    }
    Vector3 Stove()
    {
        var s = Array.Find(BakeryData.Load(), x => x.id == "stove");
        return new Vector3(s.x, s.y, s.z);
    }
    void Actor(Vector3 at, Vector3 look)
    {
        actor = actorEnd = SafeStand(at);
        b.player.locked = true;
        b.player.transform.position = actor; b.player.vel = Vector3.zero;
        b.player.FaceTowards(look);
    }
    void Pose(string clip, ExprId expr)
    {
        b.player.PlayEmote(clip);
        FindObjectOfType<PrototypeGame>()?.SetExpression(expr);
    }
    void Enter(int s)
    {
        Array.Clear(events, 0, events.Length);
        b.ui.CloseLetter(); AIAgentCalibrationDirector.DebugSetConsole(false);
        if (viewfinder.IsViewfinderActive) viewfinder.CloseViewfinder();
        b.player.locked = true;
        if (s != 11 && s != 12 && s != 13) b.director.StopAllCoroutines();
        radius = 5; height = 2.6f; orbit = 205; focus = Stove();
        var postOffice = GameObject.Find("B1_post_office_visual");
        var postBounds = postOffice != null ? Lux3DModelFit.WorldBounds(postOffice) : new Bounds(b.world.MailboxPos, Vector3.one);
        var post = new Vector3(postBounds.center.x - 1f, 0, postBounds.min.z - 1.5f);
        switch (s)
        {
            case 0:
                focus = new Vector3(-18, 3, -7); radius = 22; height = 16; orbit = 165;
                Actor(post, focus); b.water.SetInstant(WaterLevel.LevelYs[0]); break;
            case 1:
                Actor(post, post + Vector3.back * 3);
                actorEnd = Ground(actor + Vector3.back * 1.7f);
                focus = actor + Vector3.up * .5f; radius = 3.5f; height = 1.3f; orbit = 205;
                Pose("look", ExprId.sleepy); break;
            case 2:
                b.state.step = "B1_matches"; Mark("matches_taken"); Mark("matches_packed"); Actor(Stove() + new Vector3(-1.8f, 0, -1.5f), Stove());
                focus = Stove() + Vector3.up * .5f; radius = 5.8f; height = 2;
                words.OpenSpecificLetter(0); Pose("read_letter", ExprId.curious); break;
            case 3:
                focus = Stove(); Actor(focus + new Vector3(0, 0, -1.6f), focus);
                radius = 2.8f; height = 1; Pose("inspect_memory", ExprId.thinking); break;
            case 4:
                Actor(Stove() + new Vector3(-1.3f, 0, -1.8f), Stove());
                focus = (actor + Stove()) * .5f; radius = 4; height = 1.9f;
                words.OpenSpecificLetter(0); break;
            case 5:
                words.OpenSpecificLetter(1); focus = Stove(); radius = 4.5f; height = 1.8f; break;
            case 6:
                b.state.step = "S1_slipper";
                focus = mech.GetDogBowlPosition(); Actor(focus + new Vector3(-2, 0, -2), focus);
                actorEnd = Ground(actor + new Vector3(.7f, 0, .7f)); radius = 6.2f; height = 3; orbit = 220;
                b.water.SetLevel("L1", 3); break;
            case 7:
                focus = mech.GetDogBowlPosition(); radius = 5f; height = 2.6f;
                AIAgentCalibrationDirector.DebugSetConsole(true); break;
            case 8:
                focus = mech.GetDogBowlPosition(); radius = 4.8f; height = 2.7f; orbit = 165;
                var dog = FindObjectOfType<DogFollower>();
                if (dog != null) { dog.ShowAt(dog.PeekPoint); dog.Follow(); }
                Pose("wave", ExprId.happy); break;
            case 9:
                b.state.step = "L0_cross"; Mark("school_read");
                b.water.SetInstant(WaterLevel.LevelYs[2]); mech.DebugResetCrossing();
                focus = mech.WinchPosition; Actor(focus + new Vector3(-.8f, 0, -.7f), focus);
                radius = 6.5f; height = 4; orbit = 190;
                Pose("give", ExprId.nervous); mech.DebugPlayerHoldsWinch = true; break;
            case 10:
                mech.DebugPlayerHoldsWinch = false;
                FindObjectOfType<TidalEchoCloneSystem>()?.Dismiss();
                SetupPrint(); break;
            case 11:
                b.director.PrepareFilmingStage(9);
                b.director.StopAllCoroutines();
                var light = LighthouseData.Load(); focus = LighthouseData.Point(light.lamp);
                var liveBeacon = FindObjectOfType<BeaconLight>(); if (liveBeacon != null && liveBeacon.pivot != null) focus = liveBeacon.pivot.position;
                actor = actorEnd = b.player.transform.position;
                radius = 8; height = 4; orbit = 190;
                TownLighting.SetFilmingPhase(0);
                words.OpenSpecificLetter(2); Pose("read_letter", ExprId.thinking); break;
            case 12:
                var beacon = FindObjectOfType<BeaconLight>(); if (beacon != null) { beacon.TurnOn(); beacon.ManualMode = true; }
                focus = LighthouseData.Point(LighthouseData.Load().lamp);
                if (beacon != null && beacon.pivot != null) focus = beacon.pivot.position;
                radius = 12; height = 4; orbit = 130;
                Pose("wave", ExprId.excited); break;
            case 13:
                var reply = FindObjectOfType<FlashlightReply>(); if (reply != null) reply.Begin();
                FindObjectOfType<CityGlow>()?.Show();
                focus = EndingData.Point(EndingData.Load().city); radius = 22; height = 9; orbit = 220;
                if (reply != null && reply.dots.Length > 0 && reply.dots[0] != null)
                {
                    var lights = reply.dots[0].bounds;
                    foreach (var dot in reply.dots) if (dot != null) lights.Encapsulate(dot.bounds);
                    focus = lights.center; radius = Mathf.Max(18, lights.size.magnitude * .85f);
                }
                break;
            case 14:
                b.director.StopAllCoroutines(); b.state.step = "E1_read"; Mark("dawn_shown");
                focus = EndingData.Point(EndingData.Load().desk); Actor(focus + new Vector3(0, 0, -1.1f), focus);
                radius = 3.6f; height = 1.5f; orbit = 35;
                var letter = b.director.GetLetter("zhou_eggboy");
                if (letter != null) b.ui.ShowLetterDetail(letter.title, letter.body, letter.signature);
                Pose("read_letter", ExprId.surprised); break;
            case 15:
                focus = new Vector3(-3, 3, -3); radius = 43; height = 32; orbit = 185;
                Pose("look", ExprId.happy); MakeTurntables(); break;
            case 16:
                focus = new Vector3(-3, 3, -3); radius = 48; height = 35; orbit = 190; break;
        }
        if (!viewfinder.IsViewfinderActive) b.ui.SetCinematic(true, "trailer");
        Debug.Log("TRAILER_SCENE " + (s + 1) + " focus=" + focus + " actor=" + actor);
    }

    bool Once(int index, float at) { if (events[index] || u < at) return false; return events[index] = true; }
    void Direct()
    {
        float ease = Mathf.SmoothStep(0, 1, u);
        if (sceneIndex == 11) TownLighting.SetFilmingPhase(ease);
        if (sceneIndex == 1)
        {
            if (Once(0, .15f)) Pose("look", ExprId.happy);
            if (Once(1, .40f)) Pose("open_lighthouse_door", ExprId.happy);
            var pos = Vector3.Lerp(actor, actorEnd, Mathf.SmoothStep(0, 1, Mathf.Clamp01((u - .5f) * 2)));
            b.player.PuppetStep(pos, b.player.yaw, u > .5f && u < .95f ? (actorEnd - actor) / 5f : Vector3.zero, true, 1f / 30);
            focus = pos + Vector3.up * .5f;
        }
        if (sceneIndex == 2 && Once(0, .76f)) b.ui.CloseLetter();
        if (sceneIndex == 4 && Once(0, .42f)) { b.ui.CloseLetter(); words.ThrowSlip("wood", Ground(Stove() + new Vector3(-1, 0, -1))); }
        if (sceneIndex == 4 && Once(1, .72f)) { b.director.LightOven(FindObjectOfType<BakeryEffects>()); Pose("look", ExprId.sad); }
        if (sceneIndex == 5 && Once(0, .30f))
        {
            b.ui.CloseLetter(); words.Combine("dry", "wood"); words.ThrowSlip("dry_wood", Ground(Stove() + new Vector3(-.6f, 0, -.6f)));
        }
        if (sceneIndex == 5 && Once(1, .6f))
        {
            Mark("oven_lit"); FindObjectOfType<BakeryEffects>()?.Light();
            b.director.BoostOvenBellows(FindObjectOfType<BakeryEffects>()); Pose("hop", ExprId.excited);
        }
        if (sceneIndex == 7)
        {
            var field = FindObjectOfType<AIAgentCalibrationDirector>().GetComponentInChildren<InputField>();
            string wish = "一根香喷喷的骨头";
            if (field != null) field.text = wish.Substring(0, Mathf.Clamp((int)(u * 1.7f * wish.Length), 0, wish.Length));
            if (Once(0, .65f))
            {
                AIAgentCalibrationDirector.DebugSetConsole(false);
                FindObjectOfType<AIAgentCalibrationDirector>().SpawnLiveWishObjectInWorld(wish);
                b.director.StopAllCoroutines(); // 造物镜头不被学校的自动读信遮住。
            }
        }
        if (sceneIndex == 9 && Once(0, .35f))
        {
            var echo = FindObjectOfType<TidalEchoCloneSystem>(); echo.DebugClear();
            for (int k = 0; k < 160; k++) echo.DebugPushFrame(mech.WinchPosition + new Vector3(.65f, 0, .1f), b.player.yaw, Vector3.zero, true, "winch");
            echo.Summon(); Pose("give", ExprId.thinking);
        }
        if (sceneIndex == 10 && printAim)
        {
            if (u < .56f) viewfinder.DebugSetView(printYaw + Mathf.Sin(u * 6) * .7f, printPitch, printHold, printHeldYaw);
            if (Once(0, .56f)) { viewfinder.DebugSetView(printYaw, printPitch, printHold, printHeldYaw); viewfinder.Shutter(); viewfinder.CloseViewfinder(); }
        }
        if (sceneIndex == 12) FindObjectOfType<BeaconLight>()?.RotateYaw(.55f);
        if (sceneIndex == 13) FindObjectOfType<FlashlightReply>()?.RevealUpTo(Mathf.Clamp(Mathf.FloorToInt(u * 46), 0, 40));
        if (sceneIndex == 14 && Once(0, .77f)) { b.ui.CloseLetter(); Pose("look", ExprId.happy); }
        foreach (var tab in turntables) if (tab != null) tab.Rotate(0, .65f, 0, Space.World);
        if (assetsGroup != null) assetsGroup.alpha = sceneIndex == 15 ? Mathf.SmoothStep(0, 1, u * 4) : 0;
        titleGroup.alpha = t >= 17.5f && t < 20 ? Mathf.Clamp01(Mathf.Min((t - 17.5f) * 2, (20 - t) * 3)) : 0;
        endGroup.alpha = t >= 146 ? Mathf.SmoothStep(0, 1, (t - 146) * 1.8f) : 0;
        CleanHud();
    }

    void LateUpdate()
    {
        if (cam == null || sceneIndex < 0) return;
        if (sceneIndex == 8)
        {
            var dog = FindObjectOfType<DogFollower>();
            if (dog != null) focus = dog.transform.position + Vector3.up * .2f;
        }
        if (sceneIndex == 10 && viewfinder != null && viewfinder.IsViewfinderActive) return;
        float a = (orbit + u * 7f) * Mathf.Deg2Rad;
        float r = radius * (1 - .07f * Mathf.SmoothStep(0, 1, u));
        cam.transform.position = focus + new Vector3(Mathf.Sin(a) * r, height + Mathf.Sin(t * .19f) * .08f, Mathf.Cos(a) * r);
        cam.transform.LookAt(focus + Vector3.up * .35f);
        cam.fieldOfView = sceneIndex == 15 || sceneIndex == 0 ? 43 : 47;
    }

    void SetupPrint()
    {
        mech.DebugResetCrossing(); mech.GapEndpoints(out var a, out var bb);
        var ab = bb - a; ab.y = 0;
        Actor(a - ab.normalized * 1.1f, bb);
        focus = (a + bb) * .5f; radius = 6; height = 3;
        b.player.locked = false; b.ui.SetCinematic(false, "trailer");
        viewfinder.OpenViewfinder(); viewfinder.DebugSelect("ruler");
        printYaw = Mathf.Atan2(ab.x, ab.z) * Mathf.Rad2Deg;
        printAim = false;
        for (float hy = 0; hy <= 90 && !printAim; hy += 90)
        for (float hold = .35f; hold <= 1.1f && !printAim; hold += .05f)
        for (float pitch = 5; pitch <= 50 && !printAim; pitch += 1.5f)
        {
            viewfinder.DebugSetView(printYaw, pitch, hold, hy);
            if (!viewfinder.AimValid) continue;
            var hb = Lux3DModelFit.WorldBounds(viewfinder.DebugHeldModel); var cp = cam.transform.position;
            var pb = new Bounds(cp + (hb.center - cp) * viewfinder.AimScale, hb.size * viewfinder.AimScale);
            pb.Expand(new Vector3(1.1f, 8, 1.1f));
            if (!pb.Contains(new Vector3(a.x, pb.center.y, a.z)) || !pb.Contains(new Vector3(bb.x, pb.center.y, bb.z))) continue;
            printAim = true; printPitch = pitch; printHold = hold; printHeldYaw = hy;
        }
        Debug.Log("TRAILER_PRINT aim=" + printAim);
    }

    void CleanHud()
    {
        foreach (var rt in b.ui.GetComponentsInChildren<RectTransform>(true))
        {
            string n = rt.name.ToLowerInvariant();
            if (n == "wishpromptinputbg") continue;
            if (n.Contains("prompt") || n.Contains("tutorial") || n.Contains("toast") || n.Contains("goal") || n == "keys" || n.Contains("debug") || n.Contains("signalcard") || n.Contains("achievementdiducard")) rt.gameObject.SetActive(false);
        }
        Cursor.visible = false;
    }
    Text Label(Transform parent, string text, int size, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject("Trailer typography", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false); var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.anchoredPosition = position; rt.sizeDelta = dimensions;
        var label = go.GetComponent<Text>(); label.font = Resources.Load<Font>("Fonts/NotoSansSC-VF");
        label.fontSize = size; label.text = text; label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(.26f, .19f, .12f); label.raycastTarget = false; return label;
    }
    void MakeTitles()
    {
        var go = new GameObject("Trailer film titles", typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
        title = Label(go.transform, "记忆邮局 · 还在送", 62, new Vector2(0,-260), new Vector2(1500,120));
        title.color = new Color(1,.92f,.76f); title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1,-2);
        titleGroup = title.gameObject.AddComponent<CanvasGroup>(); titleGroup.alpha = 0;
        var end = new GameObject("Parchment end card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)); end.transform.SetParent(go.transform,false);
        var rt = end.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        paper = end.GetComponent<Image>(); paper.color = new Color(.94f,.88f,.75f);
        endGroup = end.GetComponent<CanvasGroup>(); endGroup.alpha = 0;
        Label(end.transform,"记忆邮局 · 还在送",74,new Vector2(0,120),new Vector2(1600,140));
        Label(end.transform,"队名：收信人    /    #Aholo黑客松",32,new Vector2(0,-30),new Vector2(1600,80));
        Label(end.transform,"https://github.com/Abigale365/eggboy-still-delivering",27,new Vector2(0,-150),new Vector2(1800,80));
    }
    void MakeTurntables()
    {
        if (assetsGroup != null) return;
        var root = new GameObject("Lux3D rotating artifacts",typeof(Canvas),typeof(CanvasGroup));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder = 900;
        assetsGroup = root.GetComponent<CanvasGroup>();
        string[] names = { "B1_post_office_visual", "B4_lighthouse_visual", "" };
        for (int i=0;i<3;i++)
        {
            GameObject model = null;
            if (i == 2)
            {
                var wish = FindObjectOfType<AIAgentCalibrationDirector>().LastWishSpawn;
                if (wish != null && wish.root != null) model = Instantiate(wish.root);
            }
            else
            {
                var source = GameObject.Find(names[i]);
                if (source != null) model = Instantiate(source);
            }
            if (model == null) continue;
            var holder = new GameObject("Lux3D studio " + i).transform; holder.position = new Vector3(1000 + i*10,1000,1000);
            model.transform.SetParent(holder,false); model.transform.localPosition = Vector3.zero;
            foreach(var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach(var script in model.GetComponentsInChildren<MonoBehaviour>()) Destroy(script);
            var bounds = Lux3DModelFit.WorldBounds(model); model.transform.localScale *= 1.8f / Mathf.Max(.01f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
            bounds = Lux3DModelFit.WorldBounds(model); model.transform.position += holder.position - bounds.center;
            turntables[i] = holder;
            var cc = new GameObject("Artifact camera").AddComponent<Camera>(); cc.transform.position = holder.position + new Vector3(0,.5f,-3.5f); cc.transform.LookAt(holder.position);
            cc.clearFlags = CameraClearFlags.SolidColor; cc.backgroundColor = new Color(.93f,.87f,.74f); cc.fieldOfView = 39;
            var tex = new RenderTexture(320,220,16); cc.targetTexture=tex;
            var panel = new GameObject("Artifact view",typeof(RectTransform),typeof(RawImage)); panel.transform.SetParent(root.transform,false);
            var rt = panel.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=new Vector2(1,.5f); rt.pivot=new Vector2(1,.5f); rt.anchoredPosition=new Vector2(-45,240-i*240); rt.sizeDelta=new Vector2(320,220);
            panel.GetComponent<RawImage>().texture=tex;
            var fill = new GameObject("Artifact softbox").AddComponent<Light>(); fill.type=LightType.Point; fill.range=7; fill.intensity=3; fill.transform.position=holder.position+new Vector3(-1,2,-2);
        }
    }
    void OnDestroy()
    {
        Time.captureFramerate = 0;
        try { pipe?.Close(); } catch (Exception) { }
        if (capture != null) { capture.Release(); Destroy(capture); }
        if (pixels != null) Destroy(pixels);
    }
}
