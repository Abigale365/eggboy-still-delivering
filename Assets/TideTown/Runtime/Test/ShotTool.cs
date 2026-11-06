using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 验收截图。用法：
//   TideTown.exe -shot preview            从 PreviewCam 截图，存到 <包目录>/shots/preview.png
//   TideTown.exe -shot preview -idpass    另存 preview_id.png 和 preview_id.json，给 score_preview.py 打分
// 其他名字读 Resources/World/world_layout.json 的 cameras。图一律 1774 × 887，不受窗口大小影响。
public class ShotTool : MonoBehaviour
{
    [Serializable] public class CamDef { public string name; public float[] pos; public float[] euler; public float fov; public int width; public int height; }
    [Serializable] class CamList { public CamDef[] cameras; }

    // 分类颜色和 tools/preview/score_preview.py 对应，改一边要改另一边
    static readonly Color32 Sea = new Color32(0, 0, 255, 255);
    static readonly Color32 Land = new Color32(0, 255, 0, 255);
    static readonly Color32 Rock = new Color32(255, 255, 0, 255);
    static readonly Color32 Foliage = new Color32(0, 255, 255, 255);
    static readonly Color32 Prop = new Color32(255, 0, 255, 255);
    const byte BuildingR = 200; // 建筑：R=200，G=序号×20，B=0

    string shotName;
    bool idPass;

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

        var def = FindCamera(shotName);
        if (def == null) { Debug.LogError("SHOT 找不到机位 " + shotName); Application.Quit(1); yield break; }

        foreach (var c in FindObjectsOfType<Canvas>()) c.enabled = false;
        if (shotName == "preview" || idPass)
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
        cam.transform.SetPositionAndRotation(new Vector3(def.pos[0], def.pos[1], def.pos[2]), Quaternion.Euler(def.euler[0], def.euler[1], def.euler[2]));
        cam.fieldOfView = def.fov;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 800f;

        int w = def.width > 0 ? def.width : 1774, h = def.height > 0 ? def.height : 887;
        yield return Capture(cam, w, h, Path.Combine(dir, shotName + ".png"));

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

    public static IEnumerator Capture(Camera cam, int w, int h, string path)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        cam.allowMSAA = false;
        cam.targetTexture = rt;
        cam.enabled = true;
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
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
