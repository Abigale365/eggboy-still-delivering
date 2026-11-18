using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShadowAndChimeSystem : MonoBehaviour
{
	private static AudioSource sharedAudio;

	private static readonly Dictionary<int, AudioClip> chimeCache = new Dictionary<int, AudioClip>();

	public bool IsStudioOpen => false;

	private static AudioSource Audio()
	{
		if (sharedAudio == null)
		{
			GameObject obj = new GameObject("ProceduralMusicBoxAudio");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			sharedAudio = obj.AddComponent<AudioSource>();
			sharedAudio.spatialBlend = 0f;
			sharedAudio.volume = 0.42f;
		}
		return sharedAudio;
	}

	public static void PlayProceduralChime(float freqHz, float durationSec = 0.42f)
	{
		try
		{
			int key = Mathf.RoundToInt(freqHz * 10f) * 1000 + Mathf.RoundToInt(durationSec * 100f);
			if (!chimeCache.TryGetValue(key, out var value) || value == null)
			{
				int num = 22050;
				int num2 = Mathf.Max(512, Mathf.RoundToInt((float)num * durationSec));
				float[] array = new float[num2];
				for (int i = 0; i < num2; i++)
				{
					float num3 = (float)i / (float)num;
					float num4 = Mathf.Exp((0f - num3) * 6.5f) * Mathf.Min(1f, num3 * 80f);
					float num5 = Mathf.Sin(MathF.PI * 2f * freqHz * num3) * 0.65f + Mathf.Sin(MathF.PI * 2f * (freqHz * 2f) * num3) * 0.25f + Mathf.Sin(MathF.PI * 2f * (freqHz * 3f) * num3) * 0.1f;
					array[i] = num5 * num4;
				}
				value = AudioClip.Create("Chime_" + key, num2, 1, num, stream: false);
				value.SetData(array, 0);
				chimeCache[key] = value;
			}
			Audio().PlayOneShot(value);
		}
		catch
		{
		}
	}

	public static void PlayClip(AudioClip clip, float volume = 1f)
	{
		if (clip == null)
		{
			return;
		}
		try
		{
			Audio().PlayOneShot(clip, volume);
		}
		catch
		{
		}
	}

	public void Init(EggboyPlayer p, EggboyUI u, GameState s)
	{
	}

	public void ToggleStudio()
	{
	}

	public void OpenStudio()
	{
	}

	public void CloseStudio()
	{
	}
}
