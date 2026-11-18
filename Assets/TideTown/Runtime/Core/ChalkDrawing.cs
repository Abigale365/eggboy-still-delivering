using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

internal sealed class ChalkDrawing : IDisposable
{
	public const int Width = 640;

	public const int Height = 416;

	public const float ChalkRadius = 3.8f;

	public const float EraserHalfWidth = 18f;

	public const float EraserHalfHeight = 12f;

	public static readonly Color32[] Colors = new Color32[5]
	{
		new Color32(byte.MaxValue, 243, 211, byte.MaxValue),
		new Color32(248, 208, 118, byte.MaxValue),
		new Color32(245, 154, 127, byte.MaxValue),
		new Color32(154, 215, 230, byte.MaxValue),
		new Color32(182, 219, 155, byte.MaxValue)
	};

	private const int MaxUndo = 6;

	private readonly List<byte[]> history = new List<byte[]>(6);

	private readonly Color32[] colors = new Color32[266240];

	private byte[] pixels = new byte[266240];

	private byte[] beforeGesture;

	private Vector2Int last;

	private bool hasLast;

	private bool upload;

	public Texture2D Texture { get; }

	public bool HasChanges { get; private set; }

	public bool CanUndo => history.Count > 0;

	public ChalkDrawing(string saved)
	{
		Texture = new Texture2D(640, 416, TextureFormat.RGBA32, mipChain: false)
		{
			name = "School blackboard chalk",
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		if (!string.IsNullOrEmpty(saved))
		{
			Restore(saved);
		}
		Recolor();
		Upload();
	}

	private void Restore(string saved)
	{
		if (saved.Length > 532480)
		{
			return;
		}
		try
		{
			using MemoryStream stream = new MemoryStream(Convert.FromBase64String(saved));
			using DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
			byte[] array = new byte[pixels.Length];
			int num;
			for (int i = 0; i < array.Length; i += num)
			{
				num = deflateStream.Read(array, i, array.Length - i);
				if (num == 0)
				{
					return;
				}
			}
			if (deflateStream.ReadByte() == -1 && Array.TrueForAll(array, (byte p) => p >> 5 <= Colors.Length))
			{
				pixels = array;
			}
		}
		catch (FormatException)
		{
		}
		catch (InvalidDataException)
		{
		}
		catch (IOException)
		{
		}
	}

	public string Encode()
	{
		if (Array.TrueForAll(pixels, (byte p) => p == 0))
		{
			return "";
		}
		using MemoryStream memoryStream = new MemoryStream();
		using (DeflateStream deflateStream = new DeflateStream(memoryStream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
		{
			deflateStream.Write(pixels, 0, pixels.Length);
		}
		return Convert.ToBase64String(memoryStream.ToArray());
	}

	public void Begin()
	{
		End();
		beforeGesture = (byte[])pixels.Clone();
	}

	public void BreakLine()
	{
		hasLast = false;
	}

	public void Drag(Vector2 uv, int tool)
	{
		if (beforeGesture == null)
		{
			return;
		}
		Vector2Int vector2Int = new Vector2Int(Mathf.RoundToInt(uv.x * 639f), Mathf.RoundToInt(uv.y * 415f));
		if (!hasLast)
		{
			Dab(vector2Int, tool);
		}
		else if (last != vector2Int)
		{
			int num = Mathf.CeilToInt(Vector2.Distance(last, vector2Int) / 2f);
			for (int i = 1; i <= num; i++)
			{
				float t = (float)i / (float)num;
				Dab(new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(last.x, vector2Int.x, t)), Mathf.RoundToInt(Mathf.Lerp(last.y, vector2Int.y, t))), tool);
			}
		}
		last = vector2Int;
		hasLast = true;
	}

	public void End()
	{
		if (beforeGesture == null)
		{
			return;
		}
		if (!SamePixels(beforeGesture))
		{
			if (history.Count == 6)
			{
				history.RemoveAt(0);
			}
			history.Add(beforeGesture);
			HasChanges = true;
		}
		beforeGesture = null;
		hasLast = false;
	}

	private bool SamePixels(byte[] previous)
	{
		for (int i = 0; i < pixels.Length; i++)
		{
			if (previous[i] != pixels[i])
			{
				return false;
			}
		}
		return true;
	}

	public void Undo()
	{
		End();
		if (CanUndo)
		{
			pixels = history[history.Count - 1];
			history.RemoveAt(history.Count - 1);
			HasChanges = true;
			Recolor();
		}
	}

	private void Dab(Vector2Int at, int tool)
	{
		bool flag = tool == Colors.Length;
		float num = (flag ? 18f : 3.8f);
		float num2 = (flag ? 12f : 3.8f);
		int num3 = Mathf.Max(0, Mathf.FloorToInt((float)at.x - num));
		int num4 = Mathf.Min(639, Mathf.CeilToInt((float)at.x + num));
		int num5 = Mathf.Max(0, Mathf.FloorToInt((float)at.y - num2));
		int num6 = Mathf.Min(415, Mathf.CeilToInt((float)at.y + num2));
		for (int i = num5; i <= num6; i++)
		{
			for (int j = num3; j <= num4; j++)
			{
				float num7 = (float)Mathf.Abs(j - at.x) / num;
				float num8 = (float)Mathf.Abs(i - at.y) / num2;
				float num9 = (flag ? Mathf.Max(num7, num8) : Mathf.Sqrt(num7 * num7 + num8 * num8));
				if (num9 >= 1f)
				{
					continue;
				}
				int num10 = i * 640 + j;
				int num11 = pixels[num10];
				int num12 = num11 & 0x1F;
				int num14;
				if (flag)
				{
					int num13 = Mathf.Max(0, num12 - Mathf.CeilToInt(34f * (1f - num9 * num9)));
					num14 = ((num13 != 0) ? ((num11 & -32) | num13) : 0);
				}
				else
				{
					float num15 = (float)(((j * 73) ^ (i * 151) ^ (j * i * 17)) & 0xFF) / 255f;
					int num16 = Mathf.RoundToInt((1f - num9 * num9) * (16f + 15f * num15));
					int num17 = tool + 1 << 5;
					if (num16 == 0 || ((num11 & -32) != num17 && num16 < 6 && num12 > num16))
					{
						continue;
					}
					num14 = num17 | (((num11 & -32) == num17) ? Mathf.Max(num12, num16) : num16);
				}
				if (num14 != num11)
				{
					pixels[num10] = (byte)num14;
					colors[num10] = Ink((byte)num14);
					upload = true;
				}
			}
		}
	}

	private static Color32 Ink(byte pixel)
	{
		int num = (pixel >> 5) - 1;
		if (num < 0)
		{
			return new Color32(0, 0, 0, 0);
		}
		Color32 result = Colors[num];
		result.a = (byte)((pixel & 0x1F) * 255 / 31);
		return result;
	}

	private void Recolor()
	{
		for (int i = 0; i < pixels.Length; i++)
		{
			colors[i] = Ink(pixels[i]);
		}
		upload = true;
	}

	public void Upload()
	{
		if (upload)
		{
			Texture.SetPixels32(colors);
			Texture.Apply(updateMipmaps: false);
			upload = false;
		}
	}

	public void MarkSaved()
	{
		HasChanges = false;
	}

	public void Dispose()
	{
		UnityEngine.Object.Destroy(Texture);
	}

	internal Color32 PixelAt(Vector2 uv)
	{
		return colors[Mathf.RoundToInt(uv.y * 415f) * 640 + Mathf.RoundToInt(uv.x * 639f)];
	}
}
