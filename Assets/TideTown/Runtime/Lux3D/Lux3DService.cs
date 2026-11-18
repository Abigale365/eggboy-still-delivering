using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class Lux3DService : MonoBehaviour
{
	private Lux3DConfigFile cfg = new Lux3DConfigFile();

	private readonly List<Lux3DLibraryEntry> library = new List<Lux3DLibraryEntry>();

	private readonly Queue<Lux3DSpawn> queue = new Queue<Lux3DSpawn>();

	private int running;

	private const int MaxParallel = 4;

	public static string DebugResumeTaskId;

	private static readonly bool IsInnovationAudit = Array.Exists(Environment.GetCommandLineArgs(), (string arg) => arg == "-innovaudit");

	public static Lux3DService Instance { get; private set; }

	public ILux3DGateway Gateway { get; private set; }

	public string Mode { get; private set; } = "offline";


	public int GeneratedThisSession { get; private set; }

	public int MaxPerSession => cfg.maxPerSession;

	public string RelayUrl
	{
		get
		{
			if (!(Mode == "relay"))
			{
				return "";
			}
			return cfg.relayUrl;
		}
	}

	public IReadOnlyList<Lux3DLibraryEntry> Library => library;

	private static string CacheDirectory
	{
		get
		{
			if (!IsInnovationAudit)
			{
				return Path.Combine(Application.persistentDataPath, "lux3d");
			}
			return Path.Combine(Application.dataPath, "..", "lux3d-audit-cache");
		}
	}

	public static Lux3DService Ensure()
	{
		if (Instance != null)
		{
			return Instance;
		}
		GameObject obj = new GameObject("Lux3DService");
		UnityEngine.Object.DontDestroyOnLoad(obj);
		Instance = obj.AddComponent<Lux3DService>();
		Instance.Init();
		return Instance;
	}

	private void Init()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("Lux3D/lux3d_config");
		if (textAsset != null)
		{
			cfg = JsonUtility.FromJson<Lux3DConfigFile>(textAsset.text) ?? cfg;
		}
		TextAsset textAsset2 = Resources.Load<TextAsset>("Lux3D/library");
		if (textAsset2 != null)
		{
			Lux3DLibraryFile lux3DLibraryFile = JsonUtility.FromJson<Lux3DLibraryFile>(textAsset2.text);
			if (lux3DLibraryFile != null && lux3DLibraryFile.entries != null)
			{
				library.AddRange(lux3DLibraryFile.entries);
			}
		}
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i + 1 < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == "-lux3d")
			{
				cfg.mode = commandLineArgs[i + 1];
			}
			if (commandLineArgs[i] == "-lux3ddelay" && float.TryParse(commandLineArgs[i + 1], out var result))
			{
				cfg.mockDelaySec = result;
			}
		}
		SetMode(IsInnovationAudit ? "mock" : cfg.mode);
	}

	public void OverrideSessionCap(int n)
	{
		cfg.maxPerSession = n;
	}

	public void SetMode(string mode)
	{
		Mode = mode;
		if (mode == "mock")
		{
			Gateway = new MockGateway
			{
				delaySeconds = cfg.mockDelaySec
			};
		}
		else if (mode == "relay")
		{
			Gateway = new RelayGateway(cfg.relayUrl, cfg.timeoutSec);
		}
		else
		{
			Gateway = new OfflineGateway();
			Mode = "offline";
		}
		if (!Gateway.CanGenerate && Mode != "offline")
		{
			Debug.LogWarning("LUX3D " + mode + " 不可用，退回 offline");
			Gateway = new OfflineGateway();
			Mode = "offline";
		}
		Debug.Log("LUX3D mode=" + Mode + " library=" + library.Count);
	}

	public Lux3DLibraryEntry Find(string id)
	{
		foreach (Lux3DLibraryEntry item in library)
		{
			if (item.id == id)
			{
				return item;
			}
		}
		return null;
	}

	public Lux3DLibraryEntry Match(string text, string category)
	{
		Lux3DLibraryEntry lux3DLibraryEntry = null;
		Lux3DLibraryEntry lux3DLibraryEntry2 = null;
		int num = 0;
		foreach (Lux3DLibraryEntry item in library)
		{
			if (!string.IsNullOrEmpty(category) && item.category != category)
			{
				continue;
			}
			if (lux3DLibraryEntry2 == null)
			{
				lux3DLibraryEntry2 = item;
			}
			int num2 = 0;
			if (item.keywords != null && !string.IsNullOrEmpty(text))
			{
				string[] keywords = item.keywords;
				foreach (string text2 in keywords)
				{
					if (!string.IsNullOrEmpty(text2) && text.Contains(text2))
					{
						num2 += text2.Length;
					}
				}
			}
			if (num2 > num)
			{
				num = num2;
				lux3DLibraryEntry = item;
			}
		}
		return lux3DLibraryEntry ?? lux3DLibraryEntry2;
	}

	public GameObject InstantiateFallback(Lux3DLibraryEntry e, float sizeOverride = 0f)
	{
		if (e == null)
		{
			return null;
		}
		float num = ((sizeOverride > 0f) ? sizeOverride : e.size);
		if (e.source == "scene")
		{
			GameObject gameObject = TideTownStyleUI.CloneSceneLux3D(e.sceneName, num);
			if (gameObject != null)
			{
				gameObject.name = "Model";
			}
			return gameObject;
		}
		GameObject gameObject2 = Resources.Load<GameObject>("Lux3DLibrary/" + e.id);
		if (gameObject2 == null)
		{
			return null;
		}
		GameObject obj = UnityEngine.Object.Instantiate(gameObject2);
		obj.name = "Model";
		Lux3DModelFit.Fit(obj, num, e.up);
		return obj;
	}

	public Lux3DSpawn Spawn(string prompt, Lux3DLibraryEntry entry, Vector3 position, Quaternion rotation, Transform parent, bool generate = true, float sizeOverride = 0f)
	{
		Lux3DSpawn lux3DSpawn = new Lux3DSpawn
		{
			entry = entry,
			prompt = prompt
		};
		GameObject gameObject = new GameObject("Lux3DSpawn_" + ((entry != null) ? entry.id : "none"));
		if (parent != null)
		{
			gameObject.transform.SetParent(parent, worldPositionStays: false);
		}
		gameObject.transform.SetPositionAndRotation(position, rotation);
		lux3DSpawn.root = gameObject;
		Lux3DSpawnMarker lux3DSpawnMarker = gameObject.AddComponent<Lux3DSpawnMarker>();
		lux3DSpawnMarker.spawn = lux3DSpawn;
		lux3DSpawnMarker.size = ((sizeOverride > 0f) ? sizeOverride : (entry?.size ?? 0.5f));
		GameObject gameObject2 = InstantiateFallback(entry, sizeOverride);
		if (gameObject2 != null)
		{
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject2.transform.localPosition = Vector3.zero;
		}
		if (entry != null && entry.walkable)
		{
			Lux3DModelFit.AddWalkableBox(gameObject, gameObject2);
		}
		if (!generate || string.IsNullOrWhiteSpace(prompt))
		{
			return lux3DSpawn;
		}
		string path = CachePath(prompt);
		if (File.Exists(path))
		{
			lux3DSpawn.state = "cached";
			StartCoroutine(SwapFromBytes(lux3DSpawn, File.ReadAllBytes(path)));
			return lux3DSpawn;
		}
		if (!Gateway.CanGenerate)
		{
			lux3DSpawn.state = "offline";
			return lux3DSpawn;
		}
		if (GeneratedThisSession >= cfg.maxPerSession)
		{
			lux3DSpawn.state = "capped";
			return lux3DSpawn;
		}
		GeneratedThisSession++;
		lux3DSpawn.state = "generating";
		lux3DSpawnMarker.ShowStamp(on: true);
		queue.Enqueue(lux3DSpawn);
		return lux3DSpawn;
	}

	private void Update()
	{
		while (running < 4 && queue.Count > 0)
		{
			Lux3DSpawn lux3DSpawn = queue.Dequeue();
			if (!(lux3DSpawn.root == null))
			{
				running++;
				StartCoroutine(Run(lux3DSpawn));
			}
		}
	}

	private IEnumerator Run(Lux3DSpawn s)
	{
		Lux3DRequest req = new Lux3DRequest
		{
			prompt = BuildPrompt(s.prompt),
			fallbackId = ((s.entry != null) ? s.entry.id : null),
			resumeTaskId = DebugResumeTaskId
		};
		DebugResumeTaskId = null;
		Lux3DResult res = new Lux3DResult();
		yield return Gateway.Generate(req, res, () => s.root == null);
		running--;
		s.seconds = res.seconds;
		if (s.root == null)
		{
			yield break;
		}
		if (!res.ok)
		{
			s.state = "failed";
			Debug.LogWarning("LUX3D failed: " + res.error + " prompt=" + s.prompt);
			Lux3DSpawnMarker component = s.root.GetComponent<Lux3DSpawnMarker>();
			if (component != null)
			{
				component.ShowStamp(on: false);
			}
		}
		else
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(CachePath(s.prompt)));
				File.WriteAllBytes(CachePath(s.prompt), res.glb);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("LUX3D cache write: " + ex.Message);
			}
			yield return SwapFromBytes(s, res.glb);
		}
	}

	public static string BuildPrompt(string text)
	{
		return (text ?? "").Trim() + "，单个物体，温暖童话海边小镇风格，无底座无背景";
	}

	internal static void ClearInnovationAuditCache()
	{
		if (!IsInnovationAudit || !Directory.Exists(CacheDirectory))
		{
			return;
		}
		foreach (string item in Directory.EnumerateFiles(CacheDirectory, "*.glb"))
		{
			File.Delete(item);
		}
	}

	private static string CachePath(string prompt)
	{
		ulong num = 1469598103934665603uL;
		string text = prompt.Trim();
		foreach (char c in text)
		{
			num ^= c;
			num *= 1099511628211L;
		}
		return Path.Combine(CacheDirectory, num.ToString("x16") + ".glb");
	}

	private IEnumerator SwapFromBytes(Lux3DSpawn s, byte[] glb)
	{
		GameObject loaded = null;
		string err = null;
		yield return RuntimeGlbLoader.Load(glb, delegate(GameObject go)
		{
			loaded = go;
		}, delegate(string e)
		{
			err = e;
		});
		if (s.root == null)
		{
			if (loaded != null)
			{
				UnityEngine.Object.Destroy(loaded);
			}
			yield break;
		}
		Lux3DSpawnMarker component = s.root.GetComponent<Lux3DSpawnMarker>();
		if (loaded == null)
		{
			s.state = "failed";
			Debug.LogWarning("LUX3D glb load failed: " + err);
			if (component != null)
			{
				component.ShowStamp(on: false);
			}
			yield break;
		}
		loaded.name = "Model";
		float size = ((component != null) ? component.size : ((s.entry != null) ? s.entry.size : 0.5f));
		Lux3DModelFit.Fit(loaded, size, (s.entry != null) ? s.entry.up : "auto");
		if (component != null)
		{
			yield return component.SwapTo(loaded);
		}
		else
		{
			loaded.transform.SetParent(s.root.transform, worldPositionStays: false);
		}
		if (s.state != "cached")
		{
			s.state = "swapped";
		}
		Debug.Log($"LUX3D swapped prompt={s.prompt} seconds={s.seconds:F1} state={s.state}");
		s.onSwapped?.Invoke(s);
	}
}
