using System;
using UnityEngine;

public static class SpawnPlan
{
	private struct Spot
	{
		public string id;

		public Vector3 pos;

		public float yawDeg;
	}

	private static readonly Spot Home = new Spot
	{
		id = "home",
		pos = new Vector3(-22.8f, 5.5f, 16.9f),
		yawDeg = -62f
	};

	private static readonly Spot Mailbox = new Spot
	{
		id = "mailbox",
		pos = new Vector3(-30.2f, 3f, -7.4f),
		yawDeg = 95f
	};

	private static readonly Spot Bakery = new Spot
	{
		id = "bakery",
		pos = new Vector3(-13.23f, 3f, -9.2f),
		yawDeg = 0f
	};

	private static readonly Spot School = new Spot
	{
		id = "school",
		pos = new Vector3(13.53f, 3f, -4.6f),
		yawDeg = 0f
	};

	private static readonly Spot Lighthouse = new Spot
	{
		id = "lighthouse",
		pos = new Vector3(33.5f, 5f, 5.9f),
		yawDeg = 0f
	};

	private static readonly Spot PostOffice = new Spot
	{
		id = "postoffice",
		pos = new Vector3(-23.38f, 3f, -8.6f),
		yawDeg = 0f
	};

	public static string ForcedSpot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-spawnat");
		if (num < 0 || num + 1 >= commandLineArgs.Length)
		{
			return null;
		}
		return commandLineArgs[num + 1];
	}

	private static Spot? ByName(string id)
	{
		Spot[] array = new Spot[6] { Home, Mailbox, Bakery, School, Lighthouse, PostOffice };
		for (int i = 0; i < array.Length; i++)
		{
			Spot value = array[i];
			if (string.Equals(value.id, id, StringComparison.OrdinalIgnoreCase))
			{
				return value;
			}
		}
		return null;
	}

	private static Spot ForState(GameState st)
	{
		string text = ((st != null && !string.IsNullOrEmpty(st.step)) ? st.step : "P0_boot");
		if (st != null && st.flags.Contains("ending_done"))
		{
			return PostOffice;
		}
		if (text == "P0_boot")
		{
			return Home;
		}
		if (text == "P1_mailbox")
		{
			return Mailbox;
		}
		if (text.StartsWith("B"))
		{
			return Bakery;
		}
		if (text.StartsWith("S"))
		{
			return School;
		}
		switch (text)
		{
		case "L0_cross":
		case "L1_shed":
			return School;
		case "L2_door":
		case "L3_oil":
		case "L4_light":
			return Lighthouse;
		default:
			return PostOffice;
		}
	}

	public static void Apply(GameBootstrap boot)
	{
		EggboyPlayer player = boot.player;
		if (!(player == null))
		{
			string text = ForcedSpot();
			Spot spot = ((text != null && ByName(text).HasValue) ? ByName(text).Value : ForState(boot.state));
			Vector3 pos = spot.pos;
			if (boot.world != null)
			{
				pos.y = boot.world.GroundAt(pos.x, pos.z, pos.y + 1.5f);
			}
			player.transform.position = pos;
			player.vel = Vector3.zero;
			player.grounded = true;
			player.yaw = spot.yawDeg * (MathF.PI / 180f);
			player.transform.rotation = Quaternion.Euler(0f, spot.yawDeg, 0f);
			ChaseCamera chaseCamera = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
			if (chaseCamera != null)
			{
				chaseCamera.SnapBehind(pos, player.yaw);
			}
			Debug.Log($"SPAWN {spot.id} step={boot.state?.step} pos={pos.x:F2},{pos.y:F2},{pos.z:F2} yaw={spot.yawDeg:F0}");
		}
	}
}
