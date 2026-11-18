using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class ChaseCamera : MonoBehaviour
{
	private const float DistMin = 2.2f;

	private const float DistMax = 7.5f;

	private const float DistDefault = 4.2f;

	private const float Height = 0.95f;

	private const float Shoulder = 0.35f;

	private const float MouseSens = 0.04f;

	private static float sens = -1f;

	public Func<Vector3, Vector3, float, float> extraBlock;

	public bool fadeActive;

	private const float RecenterDelay = 5.5f;

	private const float RecenterRamp = 0.8f;

	private const float RecenterSpeed = 1.6f;

	private const float RecenterMaxAngle = 1.2217305f;

	private const float MinDist = 0.4f;

	private const int CamMask = -261;

	private static readonly float[,] Rays = new float[5, 2]
	{
		{ 0f, 1f },
		{ 12f, 0.65f },
		{ -12f, 0.65f },
		{ 24f, 0.3f },
		{ -24f, 0.3f }
	};

	private static readonly float PitchMin = -0.34906584f;

	private static readonly float PitchMax = 0.9599311f;

	public float yaw;

	public float extraLift;

	private float pitch = 0.2443461f;

	private float yawGoal;

	private float pitchGoal = 0.2443461f;

	private float dist = 4.2f;

	private float distGoal = 4.2f;

	private float wallDist = 4.2f;

	private float sinceBlocked = 99f;

	private float idleMouse;

	private Vector3 target;

	private Camera cam;

	public static float Sensitivity
	{
		get
		{
			if (sens < 0f)
			{
				sens = Mathf.Clamp(PlayerPrefs.GetFloat("tidetown.sensitivity", 1f), 0.4f, 2f);
			}
			return sens;
		}
		set
		{
			sens = Mathf.Clamp(value, 0.4f, 2f);
			PlayerPrefs.SetFloat("tidetown.sensitivity", sens);
			PlayerPrefs.Save();
		}
	}

	public static bool AutoRecenter
	{
		get
		{
			return PlayerPrefs.GetInt("tidetown.recenter", 1) == 1;
		}
		set
		{
			PlayerPrefs.SetInt("tidetown.recenter", value ? 1 : 0);
			PlayerPrefs.Save();
		}
	}

	public Vector3 Forward => new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));

	public Vector3 Right => new Vector3(Mathf.Cos(yaw), 0f, 0f - Mathf.Sin(yaw));

	private static float Damp(float rate, float dt)
	{
		return 1f - Mathf.Exp((0f - rate) * dt);
	}

	private static float Wrap(float a)
	{
		return Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
	}

	private void Awake()
	{
		cam = GetComponent<Camera>();
	}

	public void SnapBehind(Vector3 pos, float heading)
	{
		yaw = (yawGoal = heading);
		target = new Vector3(pos.x, pos.y + 0.95f, pos.z);
	}

	public void Tick(float dt, Vector3 pos, float heading, bool moving, bool running)
	{
		if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && Cursor.lockState != CursorLockMode.Locked && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
		if (Cursor.lockState == CursorLockMode.Locked)
		{
			float axisRaw = Input.GetAxisRaw("Mouse X");
			float axisRaw2 = Input.GetAxisRaw("Mouse Y");
			if (Mathf.Abs(axisRaw) > 0f || Mathf.Abs(axisRaw2) > 0f)
			{
				idleMouse = 0f;
			}
			float num = 0.04f * Sensitivity;
			yawGoal += axisRaw * num;
			pitchGoal = Mathf.Clamp(pitchGoal - axisRaw2 * num, PitchMin, PitchMax);
		}
		float y = Input.mouseScrollDelta.y;
		if (y != 0f)
		{
			distGoal = Mathf.Clamp(distGoal * Mathf.Exp((0f - y) * 0.12f), 2.2f, 7.5f);
		}
		target = Vector3.Lerp(target, new Vector3(pos.x, pos.y + 0.95f + extraLift, pos.z), Damp(12f, dt));
		idleMouse += dt;
		if (AutoRecenter && moving && idleMouse > 5.5f)
		{
			float num2 = Mathf.Min(1f, (idleMouse - 5.5f) / 0.8f);
			float num3 = Wrap(heading - yawGoal);
			float num4 = Mathf.Max(0f, Mathf.Cos(Mathf.Min(MathF.PI / 2f, Mathf.Abs(num3) / 1.2217305f * (MathF.PI / 2f))));
			yawGoal += num3 * num2 * num4 * Mathf.Min(1f, dt * 1.6f);
		}
		yaw += Wrap(yawGoal - yaw) * Damp(24f, dt);
		pitch += (pitchGoal - pitch) * Damp(24f, dt);
		dist += (distGoal - dist) * Damp(7f, dt);
		cam.fieldOfView += (((running && moving) ? 60f : 55f) - cam.fieldOfView) * Damp(4f, dt);
		Vector3 vector = new Vector3((0f - Mathf.Sin(yaw)) * Mathf.Cos(pitch), Mathf.Sin(pitch), (0f - Mathf.Cos(yaw)) * Mathf.Cos(pitch));
		Vector3 vector2 = target + Right * 0.35f;
		if (Physics.CheckSphere(vector2, 0.12f, -261, QueryTriggerInteraction.Ignore))
		{
			vector2 = target - Right * 0.35f;
		}
		float num5 = dist;
		if (Physics.SphereCast(vector2, 0.16f, vector, out var hitInfo, dist, -261, QueryTriggerInteraction.Ignore))
		{
			num5 = Mathf.Max(0.4f, hitInfo.distance - 0.12f);
		}
		for (int i = 0; i < Rays.GetLength(0); i++)
		{
			Vector3 direction = Quaternion.AngleAxis(Rays[i, 0], Vector3.up) * vector;
			if (Physics.Raycast(vector2, direction, out var hitInfo2, dist + 0.3f, -261))
			{
				float num6 = Mathf.Max(0.4f, hitInfo2.distance - 0.25f);
				num5 = Mathf.Min(num5, dist - (dist - num6) * Rays[i, 1]);
			}
		}
		if (extraBlock != null)
		{
			num5 = Mathf.Min(num5, Mathf.Max(0.4f, extraBlock(vector2, vector, dist)));
		}
		sinceBlocked = ((num5 < dist - 0.01f) ? 0f : (sinceBlocked + dt));
		float rate = ((num5 < wallDist) ? 14f : ((sinceBlocked < 0.8f) ? 3f : 7f));
		float num7 = Mathf.Clamp((num5 - wallDist) * Damp(rate, dt), -8f * dt, 8f * dt);
		wallDist = Mathf.Min(wallDist + num7, num5);
		float num8 = Mathf.Max(0f, 1.3f - wallDist) * 0.6f;
		Vector3 vector3 = vector2 + vector * wallDist;
		vector3.y = Mathf.Max(vector3.y + num8, 0.25f);
		Vector3 vector4 = vector3 - vector2;
		float num9 = vector4.magnitude;
		if (Physics.SphereCast(vector2, 0.16f, vector4.normalized, out var hitInfo3, num9, -261, QueryTriggerInteraction.Ignore))
		{
			num9 = Mathf.Min(num9, Mathf.Max(0.1f, hitInfo3.distance - 0.12f));
		}
		if (extraBlock != null)
		{
			num9 = Mathf.Min(num9, Mathf.Max(0.1f, extraBlock(vector2, vector4.normalized, vector4.magnitude)));
		}
		vector3 = vector2 + vector4.normalized * num9;
		base.transform.position = vector3;
		base.transform.LookAt(new Vector3(vector2.x, vector2.y - 0.1f - num8 * 0.4f, vector2.z));
	}

	public void SetFadeActive(bool on)
	{
		fadeActive = on && base.enabled;
		BroadcastFade();
	}

	private void LateUpdate()
	{
		BroadcastFade();
	}

	private void BroadcastFade()
	{
		Vector3 position = base.transform.position;
		Shader.SetGlobalVector("_TideCamFade", new Vector4(position.x, position.y, position.z, fadeActive ? 1f : 0f));
	}

	private void OnDisable()
	{
		fadeActive = false;
		BroadcastFade();
	}

	public void DebugOrbit(float radians, float pitchDegrees = 14f)
	{
		yaw = (yawGoal = radians);
		pitch = (pitchGoal = Mathf.Clamp(pitchDegrees * (MathF.PI / 180f), PitchMin, PitchMax));
		idleMouse = 0f;
	}

	private void OnApplicationFocus(bool focus)
	{
		if (!focus)
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
	}
}
