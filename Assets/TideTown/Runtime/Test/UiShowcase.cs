using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UiShowcase : MonoBehaviour
{
	[Serializable]
	private class Wp
	{
		public string id;

		public bool stop;

		public float x;

		public float z;
	}

	[Serializable]
	private class RouteList
	{
		public Wp[] route;
	}

	private GameBootstrap boot;

	private readonly StringBuilder log = new StringBuilder();

	private string dir;

	public void Begin(GameBootstrap b)
	{
		boot = b;
		Application.runInBackground = true;
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		EggboyUI ui = boot.ui;
		MenuController menuController = UnityEngine.Object.FindObjectOfType<MenuController>();
		if (menuController != null)
		{
			menuController.AutoPauseOnLockLoss = false;
		}
		yield return new WaitForSecondsRealtime(6f);
		if (ui.TitleShowing)
		{
			yield return Shot("ui_title.png");
		}
		else
		{
			Note("title_not_showing");
		}
		Button button = FindNamed<Button>("StartButton");
		if (button != null)
		{
			button.onClick.Invoke();
		}
		else
		{
			Note("no_start_button");
		}
		for (float t = 0f; t < 20f; t += Time.unscaledDeltaTime)
		{
			if (!(boot.intro == null) && boot.intro.Done)
			{
				break;
			}
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1.5f);
		EggboyPlayer p = boot.player;
		Vector3 box = MailboxPoint();
		Vector3[] array = new Vector3[6]
		{
			new Vector3(0f, 0f, -1.1f),
			new Vector3(1.1f, 0f, 0f),
			new Vector3(-1.1f, 0f, 0f),
			new Vector3(0f, 0f, 1.1f),
			new Vector3(0.7f, 0f, -0.7f),
			Vector3.zero
		};
		bool prompt = false;
		Vector3[] array2 = array;
		for (int j = 0; j < array2.Length; j++)
		{
			Vector3 vector = array2[j];
			Vector3 vector2 = box + vector;
			p.transform.position = new Vector3(vector2.x, boot.world.GroundAt(vector2.x, vector2.z) + 0.02f, vector2.z);
			p.vel = Vector3.zero;
			if (vector.sqrMagnitude > 0.01f)
			{
				p.yaw = Mathf.Atan2(0f - vector.x, 0f - vector.z);
			}
			for (int i = 0; i < 40; i++)
			{
				if (!string.IsNullOrEmpty(ui.PromptText))
				{
					break;
				}
				yield return null;
			}
			if (!string.IsNullOrEmpty(ui.PromptText))
			{
				prompt = true;
				break;
			}
		}
		if (!prompt)
		{
			Note("no_prompt");
		}
		ui.RecallGuide();
		yield return new WaitForSecondsRealtime(1.2f);
		Note("hud_prompt=" + ui.PromptText.Replace("\"", "'") + " goal=" + ui.GoalText.Replace("\"", "'"));
		yield return Shot("ui_hud.png");
		if (boot.service.TryInteract("mailbox_03"))
		{
			for (int j = 0; j < 120; j++)
			{
				if (ui.LetterOpen)
				{
					break;
				}
				yield return null;
			}
			yield return new WaitForSecondsRealtime(1.6f);
			if (ui.LetterOpen)
			{
				yield return Shot("ui_letter.png");
			}
			else
			{
				Note("letter_not_open");
			}
			ui.CloseLetter();
		}
		else
		{
			Note("mailbox_failed");
		}
		yield return new WaitForSecondsRealtime(1.2f);
		TownMap map = UnityEngine.Object.FindObjectOfType<TownMap>();
		if (map != null)
		{
			map.Toggle();
			yield return new WaitForSecondsRealtime(0.6f);
			if (map.IsOpen)
			{
				yield return Shot("ui_map_start.png");
			}
			else
			{
				Note("map_not_open");
			}
			map.Close();
			yield return new WaitForSecondsRealtime(0.4f);
			List<string> stamps = boot.director.State.stamps;
			bool injected = !stamps.Contains("bakery");
			if (injected)
			{
				stamps.Add("bakery");
			}
			map.Toggle();
			yield return new WaitForSecondsRealtime(0.6f);
			if (map.IsOpen)
			{
				yield return Shot("ui_map.png");
			}
			map.Close();
			if (injected)
			{
				stamps.Remove("bakery");
			}
			yield return new WaitForSecondsRealtime(0.4f);
		}
		else
		{
			Note("no_map");
		}
		ui.SetPaused(on: true);
		yield return new WaitForSecondsRealtime(1.2f);
		if (ui.Paused)
		{
			yield return Shot("ui_pause.png");
		}
		else
		{
			Note("pause_failed");
		}
		ui.SetPaused(on: false);
		string[] array3 = new string[4] { "lin_oven", "ying_dog", "chen_lighthouse", "zhou_eggboy" };
		string[] array4 = array3;
		foreach (string id in array4)
		{
			LetterData letter = boot.director.GetLetter(id);
			string delivery = PostalArtwork.DeliveryForLetter(id);
			if (letter == null)
			{
				Note("missing_letter_" + id);
				continue;
			}
			ui.ShowLetterDetail(letter.title, letter.body, letter.signature, id);
			yield return new WaitForSecondsRealtime(0.86f);
			if (ui.LetterOpening)
			{
				yield return Shot("ui_open_" + delivery + ".png");
			}
			else
			{
				Note("missing_open_animation_" + id);
			}
			yield return new WaitForSecondsRealtime(0.55f);
			if (ui.LetterOpening || !ui.LetterOpen)
			{
				Note("letter_did_not_unfold_" + id);
			}
			if (delivery == "ending")
			{
				yield return Shot("ui_letter_ending.png");
			}
			ui.CloseLetter();
			ui.PlayDeliveryStamp(delivery);
			if (delivery == "bakery")
			{
				yield return new WaitForSecondsRealtime(0.62f);
				yield return Shot("ui_stamp_press.png");
				yield return new WaitForSecondsRealtime(0.52f);
			}
			else
			{
				yield return new WaitForSecondsRealtime(1.15f);
			}
			if (ui.PostalStampVisible)
			{
				yield return Shot("ui_stamp_" + delivery + ".png");
			}
			else
			{
				Note("missing_delivery_animation_" + id);
			}
			yield return new WaitForSecondsRealtime(1.75f);
		}
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "ui-showcase.json"), "{\n  \"status\": \"done\",\n  \"notes\": \"" + log?.ToString() + "\"\n}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Debug.Log("UISHOWCASE done " + log);
		yield return new WaitForSecondsRealtime(0.3f);
		Application.Quit();
	}

	private Vector3 MailboxPoint()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("World/route");
		if (textAsset != null)
		{
			RouteList routeList = JsonUtility.FromJson<RouteList>(textAsset.text);
			if (routeList != null && routeList.route != null)
			{
				Wp[] route = routeList.route;
				foreach (Wp wp in route)
				{
					if (wp.stop && wp.id == "03")
					{
						return new Vector3(wp.x, 0f, wp.z);
					}
				}
			}
		}
		return new Vector3(-31.04f, 0f, -6.76f);
	}

	private static T FindNamed<T>(string name) where T : Component
	{
		T[] array = Resources.FindObjectsOfTypeAll<T>();
		foreach (T val in array)
		{
			if (val != null && val.name == name && val.gameObject.scene.IsValid())
			{
				return val;
			}
		}
		return null;
	}

	private void Note(string s)
	{
		log.Append((log.Length == 0) ? "" : "; ").Append(s);
	}

	private IEnumerator Shot(string file)
	{
		yield return new WaitForEndOfFrame();
		Texture2D texture2D = ScreenCapture.CaptureScreenshotAsTexture();
		File.WriteAllBytes(Path.Combine(dir, file), texture2D.EncodeToPNG());
		Note(file + " " + texture2D.width + "x" + texture2D.height);
		UnityEngine.Object.Destroy(texture2D);
		yield return null;
	}
}
