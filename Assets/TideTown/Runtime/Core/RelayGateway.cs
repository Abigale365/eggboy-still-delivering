using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class RelayGateway : ILux3DGateway
{
	[Serializable]
	private class CreateBody
	{
		public string prompt;

		public string style;

		public int faceCount;
	}

	[Serializable]
	private class CreateResp
	{
		public string taskId;
	}

	[Serializable]
	private class TaskResp
	{
		public string status;

		public string glbUrl;

		public string error;
	}

	private readonly string baseUrl;

	private readonly float timeout;

	public string Name => "relay";

	public bool CanGenerate => !string.IsNullOrEmpty(baseUrl);

	public RelayGateway(string relayUrl, float timeoutSec)
	{
		baseUrl = (relayUrl ?? "").TrimEnd('/');
		timeout = timeoutSec;
	}

	public IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled)
	{
		float t0 = Time.realtimeSinceStartup;
		string taskId = null;
		string s = JsonUtility.ToJson(new CreateBody
		{
			prompt = req.prompt,
			style = req.style,
			faceCount = req.faceCount
		});
		using (UnityWebRequest unityWebRequest = new UnityWebRequest(baseUrl + "/lux3d/text-to-3d", "POST"))
		{
			unityWebRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(s));
			unityWebRequest.downloadHandler = new DownloadHandlerBuffer();
			unityWebRequest.SetRequestHeader("Content-Type", "application/json");
			unityWebRequest.timeout = 30;
			yield return unityWebRequest.SendWebRequest();
			if (unityWebRequest.result != UnityWebRequest.Result.Success)
			{
				result.error = "relay create: " + unityWebRequest.error;
				yield break;
			}
			taskId = JsonUtility.FromJson<CreateResp>(unityWebRequest.downloadHandler.text)?.taskId;
		}
		if (string.IsNullOrEmpty(taskId))
		{
			result.error = "relay: no taskId";
			yield break;
		}
		string glbUrl = null;
		while (Time.realtimeSinceStartup - t0 < timeout)
		{
			float wait = Time.realtimeSinceStartup + 3f;
			while (Time.realtimeSinceStartup < wait)
			{
				if (cancelled != null && cancelled())
				{
					result.error = "cancelled";
					yield break;
				}
				yield return null;
			}
			using UnityWebRequest unityWebRequest = UnityWebRequest.Get(baseUrl + "/lux3d/task/" + UnityWebRequest.EscapeURL(taskId));
			unityWebRequest.timeout = 20;
			yield return unityWebRequest.SendWebRequest();
			if (unityWebRequest.result != UnityWebRequest.Result.Success)
			{
				continue;
			}
			TaskResp taskResp = JsonUtility.FromJson<TaskResp>(unityWebRequest.downloadHandler.text);
			if (taskResp != null)
			{
				if (taskResp.status == "failed")
				{
					result.error = "relay task failed: " + taskResp.error;
					yield break;
				}
				if (taskResp.status == "done" && !string.IsNullOrEmpty(taskResp.glbUrl))
				{
					glbUrl = taskResp.glbUrl;
					break;
				}
			}
		}
		if (glbUrl == null)
		{
			result.error = "timeout";
			yield break;
		}
		using (UnityWebRequest unityWebRequest = UnityWebRequest.Get(glbUrl))
		{
			unityWebRequest.timeout = 60;
			yield return unityWebRequest.SendWebRequest();
			if (unityWebRequest.result != UnityWebRequest.Result.Success)
			{
				result.error = "relay download: " + unityWebRequest.error;
				yield break;
			}
			result.glb = unityWebRequest.downloadHandler.data;
		}
		result.seconds = Time.realtimeSinceStartup - t0;
		result.ok = result.glb != null && result.glb.Length > 1024;
		if (!result.ok)
		{
			result.error = "empty glb";
		}
	}
}
