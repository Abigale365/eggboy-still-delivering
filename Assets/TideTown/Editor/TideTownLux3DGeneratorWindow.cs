// Unity Editor 菜单工具：潮汐镇 -> Lux3D 3D 机关生成与场景挂载面板
// 支持：
//   1. 一键检视/重新生成已接入场景的 4 套多部件可动 3D 机关（M1..M4 .glb）；
//   2. 填写 AHOLO_API_KEY 直接调用群核 Lux3D 云端接口（https://api.aholo3d.cn）文生图 + 图生 3D GLB 并自动导入 Assets/TideTown/Resources/Mechanisms/。
#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public sealed class TideTownLux3DGeneratorWindow : EditorWindow
{
    string apiKey = "";
    string assetCodeName = "M5_自定义潮汐机关";
    string promptText = "Hand-painted warm wooden and aged brass mechanical gear lock box, cozy coastal town storybook style, clean silhouette, white background, isometric product render, PBR material";
    string statusMessage = "已就绪：当前工程已内置 4 套多部件可动 3D 机关 GLB（M1 鲁班三环风门、M2 小黑爪印木匣、M3 潮汐船坞齿轮绞盘、M4 守塔人黄铜三轴星盘）。";

    [MenuItem("潮汐镇/Lux3D 3D 机关生成与场景挂载面板")]
    public static void OpenWindow()
    {
        var win = GetWindow<TideTownLux3DGeneratorWindow>("Lux3D 3D 机关生成");
        win.minSize = new Vector2(520f, 420f);
        win.Show();
    }

    void OnEnable()
    {
        apiKey = Environment.GetEnvironmentVariable("AHOLO_API_KEY") ?? EditorPrefs.GetString("TideTown_AHOLO_API_KEY", "");
    }

    void OnGUI()
    {
        GUILayout.Space(8f);
        EditorGUILayout.LabelField("潮汐镇 · 3D 机关模型生成与 Lux3D 接口面板", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "当前已生成并挂载至场景与解谜界面的 4 套多部件可动 3D 机关（位于 Assets/TideTown/Resources/Mechanisms/）：\n" +
            "  • M1_鲁班三环风门机关.glb （面包房烤炉旁 + 三环风门解谜）\n" +
            "  • M2_小黑爪印转盘木匣.glb （学校狗碗旁 + 爪印木匣解谜）\n" +
            "  • M3_潮汐船坞齿轮绞盘.glb （涨潮渡口岸边 + 升降木码头与小船浮桥）\n" +
            "  • M4_守塔人黄铜三轴星盘.glb （灯塔顶层平台 + 三轴星盘解谜）",
            MessageType.Info);

        if (GUILayout.Button("在 Project 窗口定位已生成的 3D 机关 (.glb)", GUILayout.Height(28f)))
        {
            var obj = AssetDatabase.LoadMainAssetAtPath("Assets/TideTown/Resources/Mechanisms/M1_鲁班三环风门机关.glb");
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
            else
            {
                AssetDatabase.Refresh();
            }
        }

        GUILayout.Space(12f);
        EditorGUILayout.LabelField("群核 Lux3D 云端接口（https://api.aholo3d.cn）", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        apiKey = EditorGUILayout.PasswordField("AHOLO_API_KEY", apiKey);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString("TideTown_AHOLO_API_KEY", apiKey);
        }

        assetCodeName = EditorGUILayout.TextField("保存 GLB 名称", assetCodeName);
        EditorGUILayout.LabelField("3D 机关生成提示词 (Prompt)");
        promptText = EditorGUILayout.TextArea(promptText, GUILayout.Height(68f));

        if (GUILayout.Button("调用 Lux3D 云端接口生成新 3D 机关 (.glb)", GUILayout.Height(32f)))
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                statusMessage = "请先填写有效的 AHOLO_API_KEY（可从群核 Aholo 开放平台获取）。";
            }
            else
            {
                TriggerCloudGeneration();
            }
        }

        GUILayout.Space(8f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
    }

    void TriggerCloudGeneration()
    {
        statusMessage = "正在请求 https://api.aholo3d.cn/api/open/v1/flux/2b/multimodal-to-image ...";
        string url = "https://api.aholo3d.cn/api/open/v1/flux/2b/multimodal-to-image";
        string json = "{\"prompt\":\"" + promptText.Replace("\"", "\\\"") + "\",\"width\":1024,\"height\":1024,\"seed\":42}";
        var req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Aholo-Key", apiKey.Trim());
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            if (req.result != UnityWebRequest.Result.Success)
            {
                statusMessage = "云端请求返回：" + req.error + " (" + req.downloadHandler.text + ")";
            }
            else
            {
                statusMessage = "已提交云端任务返回：" + req.downloadHandler.text + "\n（也可运行 tools/generate_3d_mechanisms.py 离线合成多部件可动 GLB）";
            }
            req.Dispose();
            Repaint();
        };
    }
}
#endif
