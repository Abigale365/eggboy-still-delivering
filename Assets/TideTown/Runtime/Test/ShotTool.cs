using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 验收截图。用法：
//   TideTown.exe -shot preview            从 PreviewCam 截图，存到 <包目录>/shots/preview.png
//   TideTown.exe -shot preview -idpass    另存 preview_id.png 和 preview_id.json，给 score_preview.py 打分
//   TideTown.exe -resetsave -look day -shot mapbase -shot_aa  正交地图及对应的坐标元数据
// 其他名字读 Resources/World/world_layout.json 的 cameras。图一律 1774 × 887，不受窗口大小影响。
public class ShotTool : MonoBehaviour
{
    [Serializable] public class CamDef { public string name; public float[] pos; public float[] euler; public float fov; public int width; public int height; }
    [Serializable] class CamList { public CamDef[] cameras; }
    [Serializable] internal class MapPointCapture
    {
        public string site;
        public Vector3 position;
        public Vector2 pixel;
    }
    [Serializable] internal class MapCalibration
    {
        public List<MapPointCapture> points = new List<MapPointCapture>();
    }

    // 分类颜色和 tools/preview/score_preview.py 对应，改一边要改另一边
    static readonly Color32 Sea = new Color32(0, 0, 255, 255);
    static readonly Color32 Land = new Color32(0, 255, 0, 255);
    static readonly Color32 Rock = new Color32(255, 255, 0, 255);
    static readonly Color32 Foliage = new Color32(0, 255, 255, 255);
    static readonly Color32 Prop = new Color32(255, 0, 255, 255);
    public static readonly Color32 HeroId = new Color32(255, 128, 0, 255);
    const byte BuildingR = 200; // 建筑：R=200，G=序号×20，B=0

    string shotName;
    bool idPass;

    static readonly CamDef[] TownViews =
    {
        ReportCamera("four_south", new Vector3(-6f, 67f, -84f), new Vector3(0f, 3f, 10f), 36f),
        ReportCamera("four_north", new Vector3(-4f, 74f, 106f), new Vector3(1f, 3f, 13f), 38f),
        ReportCamera("four_east", new Vector3(100f, 68f, 18f), new Vector3(0f, 3f, 10f), 38f),
        ReportCamera("four_west", new Vector3(-100f, 70f, 15f), new Vector3(0f, 3f, 10f), 38f),
    };

    static readonly CamDef[] ReportDetails =
    {
        ReportCamera("report_fountain", new Vector3(-6.2f, 8.6f, -3.8f), new Vector3(-.8f, 4.4f, 8.2f), 49f),
        ReportCamera("report_harbor", new Vector3(27.5f, 8.6f, -25.5f), new Vector3(19.8f, 2.3f, -14.5f), 51f),
        ReportCamera("report_school", new Vector3(7.8f, 6.6f, -8.4f), new Vector3(14.7f, 4.2f, -.5f), 48f),
    };

    static CamDef ReportCamera(string name, Vector3 position, Vector3 target, float fov)
    {
        var angles = Quaternion.LookRotation(target - position, Vector3.up).eulerAngles;
        return new CamDef { name = name, pos = new[] { position.x, position.y, position.z },
            euler = new[] { angles.x, angles.y, angles.z }, fov = fov, width = 1920, height = 1080 };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-shot");
        if (i < 0 || i + 1 >= args.Length) return;
        var t = new GameObject("ShotTool").AddComponent<ShotTool>();
        t.shotName = args[i + 1];
        t.idPass = Array.IndexOf(args, "-idpass") >= 0;
        DontDestroyOnLoad(t.gameObject);
    }

    public static CamDef FindCamera(string name)
    {
        var world = Resources.Load<TextAsset>("World/world_layout");
        if (world != null)
        {
            var list = JsonUtility.FromJson<CamList>(world.text);
            if (list != null && list.cameras != null)
                foreach (var c in list.cameras) if (c.name == name) return c;
        }
        // 2026-11-13：看植被和海岸用的几个固定机位（不在 world_layout.json 里，重新生成布局不会丢）
        switch (name)
        {
            case "mapbase": return new CamDef { name = name, width = 1774, height = 887 };
            case "meadow": return new CamDef { name = name, pos = new[] { -9.5f, 4.1f, -4.0f }, euler = new[] { 8f, -62f, 0f }, fov = 50f, width = 1774, height = 887 };
            case "lawn": return new CamDef { name = name, pos = new[] { 20.0f, 3.75f, -6.5f }, euler = new[] { 10f, -35f, 0f }, fov = 50f, width = 1774, height = 887 };
            case "coast": return new CamDef { name = name, pos = new[] { -4.0f, 2.2f, -27.5f }, euler = new[] { 2f, 8f, 0f }, fov = 55f, width = 1774, height = 887 };
            case "shore": return new CamDef { name = name, pos = new[] { 6.0f, 4.4f, -12.2f }, euler = new[] { 24f, 215f, 0f }, fov = 55f, width = 1774, height = 887 };
        }
        if (name == "preview")
        {
            var preview = Resources.Load<TextAsset>("World/preview_camera");
            if (preview != null) return JsonUtility.FromJson<CamDef>(preview.text);
        }
        return null;
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return new WaitForSecondsRealtime(ArgFloat("-shotdelay", 3f));

        var sequence = shotName == "fourviews" ? TownViews : shotName == "reportfigs" ? ReportDetails : null;
        var def = sequence != null ? sequence[0] : FindCamera(shotName);
        if (def == null) { Debug.LogError("SHOT 找不到机位 " + shotName); Application.Quit(1); yield break; }

        bool mapBase = shotName == "mapbase";
        if (mapBase && Mathf.Abs(TownLighting.Phase) > .001f)
        {
            Debug.LogError("MAPBASE requires active daylight. Use -resetsave -look day -shot mapbase -shot_aa.");
            Application.Quit(1);
            yield break;
        }

        foreach (var c in FindObjectsOfType<Canvas>()) c.enabled = false;
        if (shotName == "preview" || mapBase || sequence != null)
        {
            var hero = GameObject.Find("EGGBOY");
            if (hero != null) hero.SetActive(false);
        }

        string dir = Path.Combine(Application.dataPath, "..", "shots");
        Directory.CreateDirectory(dir);

        var main = Camera.main;
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        if (main != null) { cam.CopyFrom(main); main.enabled = false; }
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        // CopyFrom 不复制 URP 相机扩展；普通样张显式选择抗锯齿，ID 图仍关闭。
        data.antialiasing = Array.IndexOf(Environment.GetCommandLineArgs(), "-shot_aa") >= 0
            ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
        data.antialiasingQuality = AntialiasingQuality.High;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 800f;
        if (sequence != null)
        {
            TownLighting.ApplyShadowRange(210f, 4);
            foreach (var view in sequence)
            {
                SetPose(cam, view);
                cam.aspect = (float)view.width / view.height;
                Shader.SetGlobalVector("_TideCamFade", Vector4.zero);
                yield return Capture(cam, view.width, view.height, Path.Combine(dir, view.name + ".png"), true);
            }
            Debug.Log($"SHOT done {shotName} ({sequence.Length} images)");
            Application.Quit();
            yield break;
        }
        // 2026-11-12：全镇俯视机位离镇子一百多米，平时的阴影范围照不到，图上没有影子；截图时把范围拉开
        if (shotName == "preview") TownLighting.ApplyShadowRange(260f, 4);

        int w = def.width > 0 ? def.width : 1774, h = def.height > 0 ? def.height : 887;
        TownMap.MapBounds bounds = null;
        if (mapBase)
        {
            var world = FindObjectOfType<IslandWorld>();
            bounds = ConfigureMapBase(cam, world, w, h);
            if (bounds == null) { Application.Quit(1); yield break; }
            WaterLevel.Main?.SetInstant(WaterLevel.LevelYs[0]);
            RenderSettings.fog = false;
            Shader.SetGlobalFloat("_ToonHaze", 0f);
            Shader.SetGlobalVector("_TideCamFade", Vector4.zero);
            var map = FindObjectOfType<TownMap>();
            if (map != null) map.enabled = false;
            if (world.mailboxTrail != null) world.mailboxTrail.SetActive(false);
            TownLighting.ApplyShadowRange(160f, 4);
        }
        else
        {
            SetPose(cam, def);
        }
        yield return Capture(cam, w, h, Path.Combine(dir, shotName + ".png"), mapBase);

        if (bounds != null)
        {
            File.WriteAllText(Path.Combine(dir, "map_bounds.json"), JsonUtility.ToJson(bounds, true), new UTF8Encoding(false));
            var calibration = new MapCalibration();
            foreach (string marker in new[] { "P_03", "P_FOUNTAIN", "P_20", "P_25" })
            {
                var site = GameObject.Find(marker);
                if (site == null) { Debug.LogError("MAPBASE missing scene marker " + marker); Application.Quit(1); yield break; }
                var p = site.transform.position;
                // 喷泉取其南边紧邻的石板路，角色站在池外。
                if (marker == "P_FOUNTAIN") p.z -= 2f;
                var viewport = cam.WorldToViewportPoint(p);
                calibration.points.Add(new MapPointCapture { site = marker, position = p,
                    pixel = new Vector2(viewport.x * w, (1f - viewport.y) * h) });
            }
            File.WriteAllText(Path.Combine(dir, "mapbase_points.json"), JsonUtility.ToJson(calibration, true), new UTF8Encoding(false));
        }

        if (idPass)
        {
            var info = PrepareIdPass(cam, data);
            yield return Capture(cam, w, h, Path.Combine(dir, shotName + "_id.png"));
            var sb = new StringBuilder("{\n  \"width\": " + w + ", \"height\": " + h + ",\n  \"buildings\": {");
            for (int i = 0; i < info.Count; i++) sb.Append(i == 0 ? "" : ", ").Append('"').Append(info[i]).Append("\": ").Append((i + 1) * 20);
            sb.Append("},\n  \"points\": {");
            bool first = true;
            // 截图用的是 RenderTexture；这里按同一画幅的视图坐标换算，避免落到屏幕分辨率上
            cam.aspect = (float)w / h;
            foreach (var t in FindObjectsOfType<Transform>())
            {
                if (!t.name.StartsWith("P_")) continue;
                var s = cam.WorldToViewportPoint(t.position);
                sb.Append(first ? "" : ", ").Append('"').Append(t.name.Substring(2)).Append("\": [")
                  .Append((s.x * w).ToString("F1")).Append(", ").Append(((1f - s.y) * h).ToString("F1")).Append("]");
                first = false;
            }
            sb.Append("}\n}\n");
            File.WriteAllText(Path.Combine(dir, shotName + "_id.json"), sb.ToString(), new UTF8Encoding(false));
        }
        Debug.Log("SHOT done " + shotName);
        Application.Quit();
    }

    static void SetPose(Camera cam, CamDef def)
    {
        cam.transform.SetPositionAndRotation(new Vector3(def.pos[0], def.pos[1], def.pos[2]),
            Quaternion.Euler(def.euler[0], def.euler[1], def.euler[2]));
        cam.fieldOfView = def.fov;
    }

    public static IEnumerator Capture(Camera cam, int w, int h, string path, bool renderExplicitly = false)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        cam.allowMSAA = false;
        if (renderExplicitly)
        {
            // 地图从独立正交相机渲染到纹理，窗口是否进入渲染队列不影响这一帧。
            cam.enabled = false;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(cam, request);
        }
        else
        {
            cam.targetTexture = rt;
            cam.enabled = true;
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
        }
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        cam.targetTexture = null;
        rt.Release();
        Debug.Log("SHOT " + path);
    }

    static TownMap.MapBounds ConfigureMapBase(Camera cam, IslandWorld world, int width, int height)
    {
        var coast = GameObject.Find("Tier_coast");
        var ground = coast != null ? coast.GetComponent<Renderer>() : null;
        if (world == null || ground == null)
        {
            Debug.LogError("MAPBASE requires the main island scene and its generated coast");
            return null;
        }
        Bounds extent = ground.bounds;
        foreach (var area in world.walkPolys)
        {
            if (area.points == null) continue;
            foreach (var p in area.points) extent.Encapsulate(new Vector3(p.x, area.y, p.y));
        }
        float aspect = (float)width / height;
        const float margin = 3.5f;
        float spanZ = Mathf.Max(extent.size.z + margin * 2f, (extent.size.x + margin * 2f) / aspect);
        var map = new TownMap.MapBounds
        {
            cx = extent.center.x, cz = extent.center.z, spanX = spanZ * aspect, spanZ = spanZ,
            pixelWidth = width, pixelHeight = height
        };
        cam.orthographic = true;
        cam.orthographicSize = spanZ * .5f;
        cam.aspect = aspect;
        cam.transform.SetPositionAndRotation(new Vector3(map.cx, extent.max.y + 65f, map.cz),
            Quaternion.LookRotation(Vector3.down, Vector3.forward));
        cam.ResetProjectionMatrix();
        return map;
    }

    List<string> PrepareIdPass(Camera cam, UniversalAdditionalCameraData data)
    {
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        RenderSettings.fog = false;
        RenderSettings.skybox = null;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        var shader = Resources.Load<Shader>("TideIdPass");
        var cache = new Dictionary<Color32Key, Material>();
        var buildings = new List<string>();
        foreach (var r in FindObjectsOfType<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) { r.enabled = false; continue; }
            var c = Classify(r, buildings);
            var key = new Color32Key(c);
            if (!cache.TryGetValue(key, out var mat)) { mat = new Material(shader); mat.color = c; cache[key] = mat; }
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return buildings;
    }

    static Color32 Classify(Renderer r, List<string> buildings)
    {
        if (IsHero(r)) return HeroId;
        var info = r.GetComponentInParent<TownAssetInfo>();
        if (info != null && info.category == "Buildings")
        {
            string id = InstanceId(r.transform, info);
            int n = buildings.IndexOf(id);
            if (n < 0) { buildings.Add(id); n = buildings.Count - 1; }
            return new Color32(BuildingR, (byte)((n + 1) * 20), 0, 255);
        }
        for (var t = r.transform; t != null; t = t.parent)
        {
            string s = t.name;
            if (t.gameObject.layer == LayerMask.NameToLayer("Water") || s == "Sea" || s.StartsWith("Water")) return Sea;
            if (s == "Rocks" || s.StartsWith("Rock") || s.StartsWith("Cliff")) return Rock;
            if (s == "Foliage" || s.StartsWith("Tree") || s.StartsWith("Bush") || s.StartsWith("Flower")) return Foliage;
        }
        return info != null ? Prop : Land;
    }

    public static bool IsHero(Renderer renderer)
    {
        if (renderer.GetComponentInParent<EggboyPlayer>() != null) return true;
        for (var t = renderer.transform; t != null; t = t.parent) if (t.name == "EGGBOY") return true;
        return false;
    }

    // 只给主角换 ID 材质：场景保留原 Shader，才能测到合流后的 Bayer 消融。
    // 同一个姿态/机位另拍仅主角的参考掩码，分母不会随遮挡缩小。
    public sealed class HeroVisibilityProbe : IDisposable
    {
        readonly Renderer[] renderers;
        readonly List<Renderer> heroes = new List<Renderer>();
        readonly Material idMaterial;
        readonly RenderTexture target;
        readonly Texture2D pixels;
        public int ReferencePixels { get; private set; }
        public int VisiblePixels { get; private set; }

        public HeroVisibilityProbe(int width = 480, int height = 270)
        {
            renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            foreach (var r in renderers) if (IsHero(r)) heroes.Add(r);
            var shader = Resources.Load<Shader>("TideIdPass");
            if (shader == null) throw new InvalidOperationException("TideIdPass shader missing");
            idMaterial = new Material(shader) { color = HeroId };
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        }

        Color32[] Read(Camera camera)
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            return pixels.GetPixels32();
        }

        public float Sample(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var oldAA = data.antialiasing; bool oldPost = data.renderPostProcessing;
            var oldClear = camera.clearFlags; var oldBackground = camera.backgroundColor;
            var materials = new List<Material[]>(); var hidden = new List<Renderer>();
            try
            {
                camera.targetTexture = target; data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                foreach (var r in heroes)
                {
                    materials.Add(r.sharedMaterials);
                    var ids = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < ids.Length; i++) ids[i] = idMaterial;
                    r.sharedMaterials = ids;
                }
                var visible = Read(camera);
                foreach (var r in renderers) if (r != null && r.enabled && !heroes.Contains(r)) { r.enabled = false; hidden.Add(r); }
                var reference = Read(camera);
                ReferencePixels = VisiblePixels = 0;
                for (int i = 0; i < reference.Length; i++)
                {
                    var c = reference[i];
                    // Linear / Gamma 输出都可识别，同像素比较仍是精确 ID 匹配。
                    if (c.r < 245 || c.g < 115 || c.g > 200 || c.b > 8) continue;
                    ReferencePixels++;
                    var v = visible[i];
                    if (Math.Abs(v.r - c.r) <= 2 && Math.Abs(v.g - c.g) <= 2 && Math.Abs(v.b - c.b) <= 2) VisiblePixels++;
                }
                return ReferencePixels > 0 ? VisiblePixels / (float)ReferencePixels : 0f;
            }
            finally
            {
                foreach (var r in hidden) if (r != null) r.enabled = true;
                for (int i = 0; i < materials.Count; i++) if (heroes[i] != null) heroes[i].sharedMaterials = materials[i];
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                data.antialiasing = oldAA; data.renderPostProcessing = oldPost;
                camera.clearFlags = oldClear; camera.backgroundColor = oldBackground;
            }
        }

        public void Dispose()
        {
            target.Release(); UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(idMaterial);
        }
    }

    static string InstanceId(Transform t, TownAssetInfo info)
    {
        var top = info.transform;
        return top.name;
    }

    static float ArgFloat(string key, float fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], out var v) ? v : fallback;
    }

    readonly struct Color32Key : IEquatable<Color32Key>
    {
        readonly int v;
        public Color32Key(Color32 c) { v = c.r << 16 | c.g << 8 | c.b; }
        public bool Equals(Color32Key o) => v == o.v;
        public override int GetHashCode() => v;
    }
}
