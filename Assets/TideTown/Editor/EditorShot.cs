// 2026-11-16 编辑器里直接拍几张场景图（几何诊断用，没有运行时灯光 / 草 / 卡通后期，只看形状和穿插）。
// 不需要打包、不占游戏。用法：Unity -batchmode（不要 -nographics）-executeMethod EditorShot.Run
// 机位写在 Build/probe/shots.json：{"shots":[{"name":"a","cam":[x,y,z],"euler":[pitch,yaw,0],"fov":55}]}
using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EditorShot
{
    [Serializable] class Shot { public string name; public float[] cam, euler; public float fov = 55f; }
    [Serializable] class ShotList { public Shot[] shots; }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/TideTown/Scenes/TideTown_Main.unity", OpenSceneMode.Single);
        var world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
        if (world != null)
            typeof(IslandWorld).GetMethod("CleanVisualRoadAndTierSeams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(world, null);
        var list = JsonUtility.FromJson<ShotList>(File.ReadAllText("Build/probe/shots.json")).shots;
        var go = new GameObject("EditorShotCam");
        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.62f, 0.75f);
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        Directory.CreateDirectory("Build/probe/shots");
        foreach (var s in list)
        {
            go.transform.SetPositionAndRotation(new Vector3(s.cam[0], s.cam[1], s.cam[2]), Quaternion.Euler(s.euler[0], s.euler[1], s.euler.Length > 2 ? s.euler[2] : 0f));
            cam.fieldOfView = s.fov;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes("Build/probe/shots/" + s.name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("EDITORSHOT " + s.name);
        }
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("EDITORSHOT_DONE");
    }
}
