using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BlackboardAudit : MonoBehaviour
{
	[Serializable]
	private sealed class Result
	{
		public string status;

		public string fingerprint;

		public List<string> checks = new List<string>();

		public List<string> failures = new List<string>();
	}

	private GameBootstrap boot;

	private SchoolBlackboard board;

	private bool reloaded;

	private string directory;

	private readonly Result result = new Result();

	internal void Begin(GameBootstrap game, SchoolBlackboard blackboard, bool restore)
	{
		boot = game;
		board = blackboard;
		reloaded = restore;
		directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "chalkboard-evidence"));
		Directory.CreateDirectory(directory);
		Application.runInBackground = true;
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return new WaitForSecondsRealtime(0.8f);
		Check("scene_blackboard", board != null);
		if (board == null)
		{
			Finish();
			yield break;
		}
		EggboyPlayer player = boot.player;
		Vector3 position = board.Position + new Vector3(0f, 0f, -1.3f);
		position.y = boot.world.GroundAt(position.x, position.z) + 0.02f;
		player.transform.position = position;
		player.vel = Vector3.zero;
		yield return null;
		bool flag = boot.service.TryInteract("blackboard") && SchoolBlackboard.IsOpen;
		Check("world_interact", flag);
		if (!flag)
		{
			Finish();
			yield break;
		}
		yield return new WaitForSecondsRealtime(0.4f);
		Check("locks_other_game_input", boot.gate.Locked && player.locked && !UnityEngine.Object.FindObjectOfType<MenuController>().WouldTogglePause(esc: true));
		Check("shelf_tools_clickable", ShelfClickTargets());
		if (reloaded)
		{
			Result result = JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(directory, "draw.json")));
			this.result.fingerprint = Digest(board.Drawing.Encode());
			Check("drawing_survives_application_restart", !string.IsNullOrEmpty(board.Drawing.Encode()) && this.result.fingerprint == result.fingerprint);
			yield return Shot("restored.png");
			board.LeaveBoard();
			boot.store.Clear();
			Finish();
			yield break;
		}
		Check("new_game_blank", string.IsNullOrEmpty(board.Drawing.Encode()));
		yield return Shot("blank.png");
		for (int i = 0; i < ChalkDrawing.Colors.Length; i++)
		{
			float y = 0.2f + 0.12f * (float)i;
			Select(i);
			Stroke(new Vector2(0.2f, y), new Vector2(0.8f, y));
			Color32 color = board.Drawing.PixelAt(new Vector2(0.5f, y));
			Color32 color2 = ChalkDrawing.Colors[i];
			Check("chalk_" + (i + 1) + "_continuous", board.SelectedTool == i && color.a > 125 && color.r == color2.r && color.g == color2.g && color.b == color2.b);
		}
		Select(5);
		Stroke(new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.72f));
		Check("local_eraser", board.Drawing.PixelAt(new Vector2(0.5f, 0.2f)).a == 0 && board.Drawing.PixelAt(new Vector2(0.75f, 0.2f)).a > 0);
		yield return Shot("eraser.png");
		Click("UndoChalkStroke");
		Check("undo_restores_erased_chalk", board.Drawing.PixelAt(new Vector2(0.5f, 0.2f)).a > 125);
		for (int j = 0; j < ChalkDrawing.Colors.Length; j++)
		{
			Click("UndoChalkStroke");
		}
		Check("undo_returns_to_blank", string.IsNullOrEmpty(board.Drawing.Encode()));
		DrawExample();
		yield return Shot("drawing.png");
		string expectedSave = board.Drawing.Encode();
		yield return VerifyZoom(expectedSave);
		this.result.fingerprint = Digest(expectedSave);
		Click("LeaveBlackboard");
		Check("button_leaves_and_unlocks", !SchoolBlackboard.IsOpen && !boot.gate.Locked && !player.locked);
		GameObject gameObject = GameObject.Find("Blackboard chalk layer");
		Check("ink_stays_on_world_blackboard", gameObject != null && gameObject.GetComponent<MeshRenderer>().enabled);
		Check("saved_to_story_slot", boot.store.Load()?.blackboardChalk == expectedSave && !string.IsNullOrEmpty(expectedSave));
		yield return new WaitForSecondsRealtime(0.35f);
		Check("can_reopen", boot.service.TryInteract("blackboard") && SchoolBlackboard.IsOpen);
		yield return new WaitForSecondsRealtime(0.35f);
		Click("InspectBlackboardBack");
		Check("back_clue_opens_with_own_lock", ObjectInspectModal.IsOpen && !SchoolBlackboard.IsOpen && boot.gate.Locked);
		yield return Shot("back.png");
		ObjectInspectModal.DebugClose();
		Check("back_clue_releases_lock", !boot.gate.Locked);
		Check("clue_keeps_drawing", boot.store.Load()?.blackboardChalk == expectedSave);
		Finish();
	}

	private IEnumerator VerifyZoom(string original)
	{
		Vector2 mark = new Vector2(0.6f, 0.59f);
		Vector2 before = board.SlateToScreen(mark);
		board.ZoomAt(before, 2f);
		yield return new WaitForSecondsRealtime(0.55f);
		Check("zoom_holds_mouse_anchor", board.ViewZoom > 1.28f && Vector2.Distance(before, board.SlateToScreen(mark)) < 2f);
		board.ZoomAt(board.SlateToScreen(mark), 20f);
		yield return new WaitForSecondsRealtime(0.55f);
		Check("zoom_clamped_at_250_percent", Mathf.Abs(board.ViewZoom - 2.5f) < 0.01f);
		Button button = GameObject.Find("ChalkTool2")?.GetComponent<Button>();
		PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
		List<RaycastResult> list = new List<RaycastResult>();
		if (button != null)
		{
			Canvas.ForceUpdateCanvases();
			pointerEventData.position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
			EventSystem.current.RaycastAll(pointerEventData, list);
			if (list.Count > 0 && list[0].gameObject == button.gameObject)
			{
				button.onClick.Invoke();
			}
		}
		Check("zoomed_palette_is_clickable", list.Count > 0 && list[0].gameObject == button.gameObject && board.SelectedTool == 2);
		yield return Shot("zoom.png");
		Vector2 zoomed = board.SlateToScreen(mark);
		board.PanView(new Vector2(84f, 27f));
		yield return new WaitForSecondsRealtime(0.2f);
		Check("pan_moves_zoomed_board", Vector2.Distance(zoomed, board.SlateToScreen(mark)) > 30f && Mathf.Abs(board.ViewZoom - 2.5f) < 0.01f);
		Select(0);
		Vector2 vector = new Vector2(0.54f, 0.56f);
		Stroke(vector, vector + new Vector2(0.025f, 0f));
		Check("zoomed_stroke_hits_original_slate", board.Drawing.PixelAt(vector).a > 125 && board.Drawing.Encode() != original);
		Click("UndoChalkStroke");
		Check("zoomed_undo_restores_drawing", board.Drawing.Encode() == original);
		Click("ResetChalkView");
		yield return new WaitForSecondsRealtime(0.55f);
		Check("reset_restores_full_board", Mathf.Abs(board.ViewZoom - 1f) < 0.005f && Vector2.Distance(before, board.SlateToScreen(mark)) < 2f && ShelfClickTargets());
	}

	private bool ShelfClickTargets()
	{
		GameObject gameObject = GameObject.Find("Chalk and felt eraser");
		EventSystem current = EventSystem.current;
		Camera main = Camera.main;
		if (gameObject == null || current == null || main == null || gameObject.transform.childCount != 6)
		{
			return false;
		}
		Canvas.ForceUpdateCanvases();
		List<RaycastResult> list = new List<RaycastResult>();
		PointerEventData pointerEventData = new PointerEventData(current);
		for (int i = 0; i < gameObject.transform.childCount; i++)
		{
			pointerEventData.position = main.WorldToScreenPoint(gameObject.transform.GetChild(i).position);
			list.Clear();
			current.RaycastAll(pointerEventData, list);
			if (list.Count == 0 || list[0].gameObject.name != "ChalkTool" + i)
			{
				return false;
			}
		}
		return true;
	}

	private void DrawExample()
	{
		Select(3);
		for (int i = 0; i < 3; i++)
		{
			Vector2[] array = new Vector2[42];
			for (int j = 0; j < array.Length; j++)
			{
				array[j] = new Vector2(0.12f + 0.76f * (float)j / ((float)array.Length - 1f), 0.12f + (float)i * 0.056f + 0.024f * Mathf.Sin((float)j * 0.55f + (float)i * 1.1f));
			}
			Stroke(array);
		}
		Select(0);
		Stroke(new Vector2(0.32f, 0.29f), new Vector2(0.36f, 0.65f), new Vector2(0.51f, 0.65f), new Vector2(0.55f, 0.29f), new Vector2(0.32f, 0.29f));
		Stroke(new Vector2(0.4f, 0.29f), new Vector2(0.4f, 0.41f), new Vector2(0.46f, 0.41f), new Vector2(0.46f, 0.29f));
		Select(2);
		Stroke(new Vector2(0.34f, 0.68f), new Vector2(0.43f, 0.79f), new Vector2(0.53f, 0.68f), new Vector2(0.34f, 0.68f));
		Stroke(new Vector2(0.378f, 0.53f), new Vector2(0.494f, 0.53f));
		Select(1);
		Stroke(new Vector2(0.409f, 0.61f), new Vector2(0.465f, 0.61f));
		Vector2[] array2 = new Vector2[31];
		Vector2 vector = new Vector2(0.72f, 0.73f);
		for (int k = 0; k < array2.Length; k++)
		{
			float f = (float)k * MathF.PI * 2f / (float)(array2.Length - 1);
			array2[k] = vector + new Vector2(Mathf.Cos(f) * 0.052f, Mathf.Sin(f) * 0.081f);
		}
		Stroke(array2);
		for (int l = 0; l < 8; l++)
		{
			float f2 = (float)l * MathF.PI / 4f;
			Vector2 vector2 = new Vector2(Mathf.Cos(f2), Mathf.Sin(f2) * 1.5f);
			Stroke(vector + vector2 * 0.069f, vector + vector2 * 0.096f);
		}
		Select(4);
		Stroke(new Vector2(0.19f, 0.274f), new Vector2(0.21f, 0.35f), new Vector2(0.24f, 0.31f), new Vector2(0.26f, 0.38f));
		Stroke(new Vector2(0.66f, 0.28f), new Vector2(0.7f, 0.37f), new Vector2(0.733f, 0.329f), new Vector2(0.79f, 0.42f));
	}

	private void Select(int index)
	{
		Click("ChalkTool" + index);
	}

	private void Click(string name)
	{
		Button[] array = UnityEngine.Object.FindObjectsOfType<Button>();
		foreach (Button button in array)
		{
			if (!(button.name != name))
			{
				if (button.IsInteractable())
				{
					button.onClick.Invoke();
				}
				return;
			}
		}
		Check("button_" + name, passed: false);
	}

	private void Stroke(params Vector2[] canvas)
	{
		if (!board.StartStroke(board.SlateToScreen(canvas[0])))
		{
			Check("slate_hit", passed: false);
			return;
		}
		for (int i = 1; i < canvas.Length; i++)
		{
			board.DragStroke(board.SlateToScreen(canvas[i]));
		}
		board.EndStroke();
	}

	private IEnumerator Shot(string name)
	{
		yield return new WaitForEndOfFrame();
		Texture2D texture2D = ScreenCapture.CaptureScreenshotAsTexture();
		File.WriteAllBytes(Path.Combine(directory, name), texture2D.EncodeToPNG());
		UnityEngine.Object.Destroy(texture2D);
	}

	private static string Digest(string data)
	{
		using SHA256 sHA = SHA256.Create();
		return Convert.ToBase64String(sHA.ComputeHash(Encoding.UTF8.GetBytes(data)));
	}

	private void Check(string name, bool passed)
	{
		(passed ? result.checks : result.failures).Add(name);
	}

	private void Finish()
	{
		result.status = ((result.failures.Count == 0) ? "passed" : "failed");
		File.WriteAllText(Path.Combine(directory, reloaded ? "restart.json" : "draw.json"), JsonUtility.ToJson(result, prettyPrint: true));
		Debug.Log("CHALKBOARD " + result.status + " " + string.Join(",", result.failures));
		Application.Quit((result.failures.Count != 0) ? 1 : 0);
	}
}
