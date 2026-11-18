using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public class CloseupShowcase : MonoBehaviour
{
	private GameBootstrap boot;

	private readonly StringBuilder log = new StringBuilder();

	private string dir;

	public void Begin(GameBootstrap b)
	{
		boot = b;
		Application.runInBackground = true;
		dir = Path.Combine(Application.dataPath, "..", "shots");
		Directory.CreateDirectory(dir);
		string[] files = Directory.GetFiles(dir, "cu_*.png");
		for (int i = 0; i < files.Length; i++)
		{
			File.Delete(files[i]);
		}
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		MenuController menuController = Object.FindObjectOfType<MenuController>();
		if (menuController != null)
		{
			menuController.AutoPauseOnLockLoss = false;
		}
		yield return new WaitForSecondsRealtime(5f);
		GameState st = boot.state;
		EggboyUI ui = boot.ui;
		bool hadSlipper = st.seenMemories.Remove("slipper_detail");
		if (ObjectInspectModal.DebugOpen("slipper", "被咬过的拖鞋", "拖鞋上留着一道小小的牙印。", st, boot.player, ui, boot.gate))
		{
			yield return new WaitForSecondsRealtime(1.2f);
			yield return Shot("cu_inspect_front.png");
			ObjectInspectModal.DebugShowBack(clearDust: true, unlock: false);
			yield return new WaitForSecondsRealtime(1f);
			yield return Shot("cu_inspect_frost.png");
			ObjectInspectModal.DebugShowBack(clearDust: false, unlock: true);
			yield return new WaitForSecondsRealtime(0.13f);
			yield return Shot("cu_inspect_pulse.png");
			yield return new WaitForSecondsRealtime(2.2f);
			yield return Shot("cu_inspect_pins.png");
			ObjectInspectModal.DebugClose();
		}
		else
		{
			Note("inspect_failed");
		}
		st.seenMemories.Remove("slipper_detail");
		if (hadSlipper)
		{
			st.seenMemories.Add("slipper_detail");
		}
		yield return new WaitForSecondsRealtime(0.6f);
		AIAgentCalibrationDirector agent = Object.FindObjectOfType<AIAgentCalibrationDirector>();
		if (agent != null)
		{
			agent.SpawnLiveWishObjectInWorld("毛绒熊玩偶");
			for (int j = 0; j < 30; j++)
			{
				yield return null;
			}
			WishToy lastWishToy = agent.LastWishToy;
			if (lastWishToy != null)
			{
				WishInspectModal.Open(lastWishToy, boot.player);
				yield return new WaitForSecondsRealtime(1.2f);
				if (WishInspectModal.IsOpen)
				{
					yield return Shot("cu_wish.png");
				}
				else
				{
					Note("wish_not_open");
				}
				WishInspectModal.DebugPutBack();
			}
			else
			{
				Note("no_wish_toy");
			}
		}
		else
		{
			Note("no_agent");
		}
		yield return new WaitForSecondsRealtime(0.6f);
		bool hadRecipe = st.seenMemories.Contains("recipe_detail");
		if (!hadRecipe)
		{
			st.seenMemories.Add("recipe_detail");
		}
		if (WoodenMechanismPuzzleModal.DebugOpen("bakery_lock", st, null, boot.player, ui, boot.gate, null))
		{
			WoodenMechanismPuzzleModal.DebugSelectRing(0);
			yield return new WaitForSecondsRealtime(1f);
			yield return Shot("cu_wood_bakery_r0.png");
			WoodenMechanismPuzzleModal.DebugSelectRing(2);
			yield return new WaitForSecondsRealtime(0.6f);
			yield return Shot("cu_wood_bakery_sel2.png");
			WoodenMechanismPuzzleModal.DebugOpenAllLatches();
			WoodenMechanismPuzzleModal.ExternalAlignRingByVoiceNote(0, "");
			WoodenMechanismPuzzleModal.DebugSelectRing(1);
			yield return new WaitForSecondsRealtime(1f);
			yield return Shot("cu_wood_bakery_r1.png");
			WoodenMechanismPuzzleModal.DebugClose();
		}
		else
		{
			Note("bakery_failed");
		}
		if (!hadRecipe)
		{
			st.seenMemories.Remove("recipe_detail");
		}
		yield return new WaitForSecondsRealtime(0.5f);
		string[] array = new string[2] { "school_lock", "lighthouse_lock" };
		foreach (string id in array)
		{
			if (WoodenMechanismPuzzleModal.DebugOpen(id, st, null, boot.player, ui, boot.gate, null))
			{
				WoodenMechanismPuzzleModal.DebugSelectRing(2);
				yield return new WaitForSecondsRealtime(1f);
				yield return Shot("cu_wood_" + id.Replace("_lock", "") + ".png");
				WoodenMechanismPuzzleModal.DebugClose();
			}
			else
			{
				Note(id + "_failed");
			}
			yield return new WaitForSecondsRealtime(0.5f);
		}
		File.WriteAllText(Path.Combine(Application.dataPath, "..", "closeup-showcase.json"), "{\n  \"status\": \"done\",\n  \"notes\": \"" + log.ToString().Replace("\"", "'") + "\"\n}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Debug.Log("CLOSEUPSHOWCASE done " + log);
		yield return new WaitForSecondsRealtime(0.3f);
		Application.Quit();
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
		Object.Destroy(texture2D);
		yield return null;
	}
}
