using UnityEngine;

public sealed class QuestGuide : MonoBehaviour
{
	private EggboyUI ui;

	private TownMap map;

	private StoryDirector director;

	private EggboyPlayer player;

	private Camera cam;

	private float nextSteer;

	private Vector3 steer;

	private Vector3 steerFor;

	public static QuestGuide Instance { get; private set; }

	public bool HasTarget { get; private set; }

	public Vector3 Target { get; private set; }

	public float Distance { get; private set; }

	public void Init(EggboyUI u, TownMap m, StoryDirector d, EggboyPlayer p)
	{
		Instance = this;
		ui = u;
		map = m;
		director = d;
		player = p;
	}

	private void LateUpdate()
	{
		if (ui == null || map == null || player == null)
		{
			return;
		}
		if (cam == null)
		{
			cam = Camera.main;
		}
		bool flag = ui.inputClose && director != null && !director.IsAuditMode && !director.Ended && map.GuideMode != TownMap.GroundGuideMode.Off;
		string targetLabel = "";
		Vector3 targetPos = default(Vector3);
		HasTarget = flag && map.TryGetStepTarget(out targetPos, out targetLabel);
		if (!HasTarget)
		{
			ui.SetQuestTarget(has: false, Vector3.zero, "", 0f, cam, showWaypoint: false, Vector3.zero);
			return;
		}
		Target = targetPos;
		Vector3 position = player.transform.position;
		Distance = new Vector2(targetPos.x - position.x, targetPos.z - position.z).magnitude;
		int num = targetLabel.IndexOf('（');
		if (num > 0)
		{
			targetLabel = targetLabel.Substring(0, num);
		}
		if (Time.unscaledTime >= nextSteer || (steerFor - targetPos).sqrMagnitude > 0.01f)
		{
			nextSteer = Time.unscaledTime + 0.2f;
			steerFor = targetPos;
			steer = map.PathPointAhead(position, targetPos, 4f);
		}
		ui.SetQuestTarget(has: true, targetPos, targetLabel, Distance, cam, !map.IsOpen, steer);
	}
}
