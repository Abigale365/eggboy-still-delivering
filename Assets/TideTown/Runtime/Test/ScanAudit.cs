using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public class ScanAudit : MonoBehaviour
{
	private GameBootstrap boot;

	public void Begin(GameBootstrap b)
	{
		boot = b;
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return new WaitForSeconds(1f);
		Time.timeScale = 1f;
		string status = "passed";
		string error = "";
		ScanStation scanStation = Object.FindObjectOfType<ScanStation>();
		bool hidden = false;
		bool registeredOff = false;
		if (scanStation == null)
		{
			status = "no_station";
			error = "ScanStation not found";
		}
		else
		{
			bool flag = false;
			Renderer[] componentsInChildren = scanStation.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				if (componentsInChildren[j].enabled)
				{
					flag = true;
				}
			}
			hidden = !flag;
			registeredOff = boot.service.IsRegistered(scanStation);
			if (!hidden)
			{
				status = "visible_when_off";
				error = "renderer enabled with Features.scan=false";
			}
			else if (registeredOff)
			{
				status = "registered_when_off";
				error = "station registered with Features.scan=false";
			}
		}
		bool visibleOn = false;
		bool promptOk = false;
		string prompt = "";
		if (status == "passed")
		{
			Features.scan = true;
			scanStation.SetEnabled(on: true);
			boot.service.Register(scanStation);
			visibleOn = true;
			Renderer[] componentsInChildren = scanStation.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				if (!componentsInChildren[j].enabled)
				{
					visibleOn = false;
				}
			}
			if (!visibleOn)
			{
				status = "hidden_when_on";
				error = "renderer disabled with Features.scan=true";
			}
		}
		if (status == "passed")
		{
			Vector3 position = boot.player.transform.position;
			boot.player.transform.position = new Vector3(scanStation.Position.x + 0.6f, position.y, scanStation.Position.z);
			boot.player.vel = Vector3.zero;
			yield return null;
			if (!boot.service.TryInteractNearest())
			{
				status = "interact_failed";
				error = "station not in range";
			}
			else
			{
				for (int i = 0; i < 20; i++)
				{
					yield return null;
				}
				prompt = boot.ui.PromptText;
				promptOk = prompt.Contains("暂未开放");
				if (!promptOk)
				{
					status = "prompt_wrong";
					error = "prompt=" + prompt;
				}
			}
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine("  \"status\": \"" + status + "\",");
		stringBuilder.AppendLine("  \"hidden_when_off\": " + (hidden ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"unregistered_when_off\": " + ((!registeredOff) ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"visible_when_on\": " + (visibleOn ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"prompt_unsupported\": " + (promptOk ? "true" : "false") + ",");
		stringBuilder.AppendLine("  \"prompt\": \"" + prompt + "\",");
		stringBuilder.AppendLine("  \"errors\": \"" + error + "\"");
		stringBuilder.AppendLine("}");
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "scan-audit.json"), stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Debug.Log("SCANAUDIT " + status + " hidden=" + hidden + " visibleOn=" + visibleOn + " prompt=" + prompt);
		yield return new WaitForSecondsRealtime(0.5f);
		Application.Quit();
	}
}
