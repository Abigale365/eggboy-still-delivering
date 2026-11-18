using System.Collections;
using UnityEngine;

public sealed class IntroSequence : MonoBehaviour
{
	public const float Duration = 5f;

	public const float WakeAt = 4.2f;

	private EggboyPlayer player;

	private EggboyUI ui;

	private PrototypeGame game;

	private ChaseCamera chase;

	private Camera cam;

	private Vector3 fromPos;

	private Vector3 toPos;

	private Quaternion fromRot;

	private Quaternion toRot;

	private float fromFov;

	private float toFov;

	private float t;

	private bool woke;

	private bool finished;

	public bool Done { get; private set; }

	public bool Running => !Done;

	public float StartTime { get; private set; }

	public ExprId StartExpr { get; private set; } = ExprId.sleepy;


	public ExprId EndExpr { get; private set; }

	public void Begin(GameBootstrap boot)
	{
		StartTime = Time.realtimeSinceStartup;
		if (!TownVoiceover.AuditMode)
		{
			StartCoroutine(VoiceIntro());
		}
		player = boot.player;
		ui = boot.ui;
		game = Object.FindObjectOfType<PrototypeGame>();
		chase = Object.FindObjectOfType<ChaseCamera>();
		cam = Camera.main;
		if (player == null || cam == null || chase == null)
		{
			finish(skip: true);
			return;
		}
		chase.enabled = false;
		player.locked = true;
		if (game != null)
		{
			game.introHold = true;
		}
		game?.SetExpression(StartExpr);
		ui?.SetActiveExpression(StartExpr);
		ShotTool.CamDef camDef = ShotTool.FindCamera("preview");
		if (camDef != null && camDef.pos != null && camDef.pos.Length >= 3 && camDef.euler != null && camDef.euler.Length >= 3)
		{
			fromPos = new Vector3(camDef.pos[0], camDef.pos[1], camDef.pos[2]);
			fromRot = Quaternion.Euler(camDef.euler[0], camDef.euler[1], camDef.euler[2]);
			fromFov = ((camDef.fov > 1f) ? camDef.fov : 20f);
			cam.transform.SetPositionAndRotation(fromPos, fromRot);
			cam.fieldOfView = fromFov;
		}
		else
		{
			fromPos = cam.transform.position;
			fromRot = cam.transform.rotation;
			fromFov = cam.fieldOfView;
		}
		float yaw = player.yaw;
		Vector3 vector = new Vector3(Mathf.Cos(yaw), 0f, 0f - Mathf.Sin(yaw));
		Vector3 vector2 = new Vector3((0f - Mathf.Sin(yaw)) * Mathf.Cos(0.2443461f), Mathf.Sin(0.2443461f), (0f - Mathf.Cos(yaw)) * Mathf.Cos(0.2443461f));
		Vector3 vector3 = player.transform.position + Vector3.up * 0.95f + vector * 0.35f;
		toPos = vector3 + vector2 * 3.6f;
		toPos.y = Mathf.Max(toPos.y, 0.25f);
		toRot = Quaternion.LookRotation(new Vector3(vector3.x, vector3.y - 0.1f, vector3.z) - toPos, Vector3.up);
		toFov = 44f;
	}

	private IEnumerator VoiceIntro()
	{
		yield return new WaitForSecondsRealtime(0.6f);
		if (!finished)
		{
			TownVoiceover.Play("vo_intro_wake");
		}
	}

	private void Update()
	{
		if (!Done && !(cam == null))
		{
			t += Time.deltaTime;
			if (t >= 4.2f && !woke)
			{
				woke = true;
				game?.SetExpression(EndExpr);
				ui?.SetActiveExpression(EndExpr);
			}
			if (t >= 5f)
			{
				finish(skip: false);
				return;
			}
			float num = Mathf.SmoothStep(0f, 1f, t / 5f);
			cam.transform.position = Vector3.Lerp(fromPos, toPos, num);
			cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, num);
			cam.fieldOfView = Mathf.Lerp(fromFov, toFov, num);
		}
	}

	private void finish(bool skip)
	{
		if (!finished)
		{
			finished = true;
			Done = true;
			if (cam != null && !skip)
			{
				cam.transform.position = toPos;
				cam.transform.rotation = toRot;
				cam.fieldOfView = toFov;
			}
			if (player != null)
			{
				player.locked = false;
			}
			if (game != null)
			{
				game.introHold = false;
			}
			if (chase != null && player != null)
			{
				chase.SnapBehind(player.transform.position, player.yaw);
				chase.enabled = true;
			}
			game?.SetExpression(EndExpr);
			ui?.SetActiveExpression(EndExpr);
		}
	}
}
