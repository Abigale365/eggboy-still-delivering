using System;
using System.Collections.Generic;
using UnityEngine;

public class FacePainter
{
	public const int W = 512;

	public const int H = 400;

	private static readonly Color32 GOLD = new Color32(byte.MaxValue, 199, 90, byte.MaxValue);

	private static readonly Color32 BLUSH = new Color32(byte.MaxValue, 90, 60, byte.MaxValue);

	private static readonly Color32 TEAR = new Color32(108, 200, byte.MaxValue, byte.MaxValue);

	private static readonly Color32 FEAR = new Color32(214, 236, byte.MaxValue, byte.MaxValue);

	private static readonly Color32 DARK = new Color32(59, 42, 26, byte.MaxValue);

	public readonly PixelCanvas Canvas = new PixelCanvas(512, 400);

	public readonly PixelCanvas SymbolCanvas = new PixelCanvas(256, 256);

	public readonly Texture2D Texture;

	public readonly Texture2D SymbolTexture;

	private float t;

	private float since;

	private float blinkTimer = 2.5f;

	private float blink;

	private float redraw;

	private bool wasBlinking;

	private readonly Dictionary<ExprId, FaceSymbol> symbols = new Dictionary<ExprId, FaceSymbol>
	{
		{
			ExprId.surprised,
			FaceSymbol.Excl
		},
		{
			ExprId.curious,
			FaceSymbol.Question
		},
		{
			ExprId.angry,
			FaceSymbol.Anger
		},
		{
			ExprId.cute,
			FaceSymbol.Heart
		},
		{
			ExprId.smug,
			FaceSymbol.Sparkle
		},
		{
			ExprId.sleepy,
			FaceSymbol.Zz
		},
		{
			ExprId.meh,
			FaceSymbol.Puff
		},
		{
			ExprId.nervous,
			FaceSymbol.Sweat
		},
		{
			ExprId.thinking,
			FaceSymbol.Dots
		},
		{
			ExprId.puzzled,
			FaceSymbol.QuestionGrey
		},
		{
			ExprId.excited,
			FaceSymbol.Sparkle
		},
		{
			ExprId.scared,
			FaceSymbol.Shake
		}
	};

	public ExprId Current { get; private set; }

	public FaceSymbol Symbol { get; private set; }

	public float Time => t;

	public Vector2 SymbolPopScale
	{
		get
		{
			float num = Mathf.Min(1f, since / 0.3f) - 1f;
			float num2 = 1f + 2.70158f * num * num * num + 1.70158f * num * num;
			return Vector2.one * Mathf.Max(0.01f, 0.34f * num2);
		}
	}

	public FacePainter()
	{
		Texture = new Texture2D(512, 400, TextureFormat.RGBA32, mipChain: false, linear: false);
		Texture.wrapMode = TextureWrapMode.Clamp;
		Texture.filterMode = FilterMode.Bilinear;
		SymbolTexture = new Texture2D(256, 256, TextureFormat.RGBA32, mipChain: false, linear: false);
		SymbolTexture.wrapMode = TextureWrapMode.Clamp;
		SymbolTexture.filterMode = FilterMode.Bilinear;
		DrawAll();
		DrawSymbolAll();
	}

	public void Set(ExprId id)
	{
		if (id != Current)
		{
			Current = id;
			since = 0f;
			blink = 0f;
			Symbol = (symbols.TryGetValue(id, out var value) ? value : FaceSymbol.None);
			DrawAll();
			DrawSymbolAll();
		}
	}

	private static bool FaceAnimated(ExprId id)
	{
		if (id != ExprId.surprised && id != ExprId.sleepy && id != ExprId.sad && id != ExprId.nervous && id != ExprId.excited)
		{
			return id == ExprId.scared;
		}
		return true;
	}

	public void Update(float dt)
	{
		t += dt;
		since += dt;
		blinkTimer -= dt;
		if (blinkTimer <= 0f)
		{
			blink = 0.13f;
			blinkTimer = 2.5f + UnityEngine.Random.value * 3f;
		}
		blink = Mathf.Max(0f, blink - dt);
		redraw += dt;
		bool flag = blink > 0f;
		bool flag2 = flag || wasBlinking || FaceAnimated(Current);
		bool flag3 = Symbol == FaceSymbol.Sparkle;
		if (redraw >= 1f / 30f)
		{
			redraw = 0f;
			if (flag2)
			{
				DrawAll();
			}
			if (flag3)
			{
				DrawSymbolAll();
			}
			wasBlinking = flag;
		}
	}

	private void DrawAll()
	{
		PixelCanvas canvas = Canvas;
		canvas.Clear(new Color32(0, 0, 0, 0));
		canvas.RoundedRect(2f, 2f, 510f, 398f, 130f, new Color32(22, 19, 26, byte.MaxValue), new Color32(12, 10, 14, byte.MaxValue));
		float num = 153.6f;
		float num2 = 358.4f;
		float num3 = 200f;
		float num4 = 71.68f;
		bool flag = blink > 0f;
		float num5 = since;
		switch (Current)
		{
		case ExprId.happy:
			if (flag)
			{
				Lines(num, num2, num3 + num4 * 0.15f, num4, GOLD);
			}
			else
			{
				ArcEye(num, num3, num4, GOLD);
				ArcEye(num2, num3, num4, GOLD);
			}
			Blush(1f);
			break;
		case ExprId.wink:
			Ring(num, num3, num4 * 0.48f, GOLD, 26f);
			Chevron(num2, num3, num4 * 0.55f, -1, GOLD);
			Blush(1f);
			break;
		case ExprId.surprised:
		{
			float r = num4 * (0.5f + 0.04f * Mathf.Sin(t * 9f));
			Ring(num, num3, r, GOLD, 26f);
			Ring(num2, num3, r, GOLD, 26f);
			break;
		}
		case ExprId.curious:
			if (flag)
			{
				Lines(num, num2, num3, num4, GOLD);
			}
			else
			{
				ArcEye(num, num3 - num4 * 0.12f, num4 * 1.05f, GOLD);
				ArcEye(num2, num3 + num4 * 0.05f, num4 * 0.9f, GOLD);
			}
			Blush(0.6f);
			break;
		case ExprId.angry:
		{
			(float, float)[] array2 = new(float, float)[2]
			{
				(num, 1f),
				(num2, -1f)
			};
			for (int i = 0; i < array2.Length; i++)
			{
				(float, float) tuple = array2[i];
				float item = tuple.Item1;
				float item2 = tuple.Item2;
				Vector2[] pts = new Vector2[4]
				{
					new Vector2(item - item2 * num4 * 0.6f, num3 - num4 * 0.38f),
					new Vector2(item + item2 * num4 * 0.62f, num3 + num4 * 0.02f),
					new Vector2(item + item2 * num4 * 0.5f, num3 + num4 * 0.32f),
					new Vector2(item - item2 * num4 * 0.52f, num3 + num4 * 0.2f)
				};
				canvas.Polygon(pts, new Color32(byte.MaxValue, 173, 58, byte.MaxValue), 14f);
			}
			break;
		}
		case ExprId.cute:
		{
			float[] array = new float[2] { num, num2 };
			foreach (float num16 in array)
			{
				Ring(num16, num3, num4 * 0.55f, GOLD, 20f);
				canvas.Circle(num16, num3 + num4 * 0.04f, num4 * 0.33f, GOLD, 10f);
				canvas.Circle(num16 - num4 * 0.12f, num3 - num4 * 0.1f, num4 * 0.11f, new Color32(byte.MaxValue, 247, 224, byte.MaxValue));
			}
			Blush(1.3f);
			break;
		}
		case ExprId.smug:
		{
			(float, float)[] array2 = new(float, float)[2]
			{
				(num, -1f),
				(num2, 1f)
			};
			for (int i = 0; i < array2.Length; i++)
			{
				var (num12, num13) = array2[i];
				canvas.Quadratic(num12 - num4 * 0.55f, num3 + num13 * num4 * 0.04f, num12, num3 - num4 * 0.22f, num12 + num4 * 0.55f, num3 - num13 * num4 * 0.04f, 24f, GOLD, 12f);
			}
			Blush(0.8f);
			break;
		}
		case ExprId.sleepy:
		{
			float num9 = 0.12f + 0.06f * Mathf.Sin(t * 1.4f);
			float[] array = new float[2] { num, num2 };
			foreach (float num10 in array)
			{
				canvas.Quadratic(num10 - num4 * 0.55f, num3 + num4 * num9, num10, num3 + num4 * (num9 + 0.18f), num10 + num4 * 0.55f, num3 + num4 * num9, 22f, GOLD, 12f);
			}
			break;
		}
		case ExprId.meh:
			Lines(num, num2, num3 + num4 * 0.05f, num4 * 1.05f, GOLD);
			break;
		case ExprId.sad:
		{
			ArcEye(num, num3, num4, GOLD);
			ArcEye(num2, num3, num4, GOLD);
			(float, float)[] array2 = new(float, float)[2]
			{
				(num, 0f),
				(num2, 0.45f)
			};
			for (int i = 0; i < array2.Length; i++)
			{
				(float, float) tuple4 = array2[i];
				float item3 = tuple4.Item1;
				float item4 = tuple4.Item2;
				float num17 = (num5 * 0.9f + item4) % 1f;
				Drop(item3 + num4 * 0.15f, num3 + num4 * 0.55f + num17 * num4 * 1.6f, num4 * 0.18f, 1f - num17 * 0.6f, TEAR);
			}
			break;
		}
		case ExprId.nervous:
		{
			float[] array = new float[2] { num, num2 };
			foreach (float x2 in array)
			{
				for (int j = 0; j < 3; j++)
				{
					float r2 = num4 * (0.18f + (float)j * 0.17f) * (1f + 0.06f * Mathf.Sin(t * 8f + (float)j));
					Ring(x2, num3, r2, GOLD, 13f);
				}
			}
			break;
		}
		case ExprId.shy:
			Chevron(num, num3, num4 * 0.5f, 1, GOLD);
			Chevron(num2, num3, num4 * 0.5f, -1, GOLD);
			Blush(1.8f);
			break;
		case ExprId.thinking:
		{
			float num14 = num4 * 0.12f;
			float num15 = (0f - num4) * 0.15f;
			if (flag)
			{
				Lines(num + num14, num2 + num14, num3 + num15, num4, GOLD);
				break;
			}
			ArcEye(num + num14, num3 + num15, num4 * 0.9f, GOLD);
			ArcEye(num2 + num14, num3 + num15, num4 * 0.9f, GOLD);
			break;
		}
		case ExprId.puzzled:
			ArcEye(num, num3 - num4 * 0.1f, num4, GOLD);
			Lines(num2, num2, num3 + num4 * 0.12f, num4 * 0.7f, GOLD);
			break;
		case ExprId.excited:
		{
			(float, float)[] array2 = new(float, float)[2]
			{
				(num, 0f),
				(num2, 1.3f)
			};
			for (int i = 0; i < array2.Length; i++)
			{
				var (x, num11) = array2[i];
				Star(x, num3, num4 * (0.62f + 0.08f * Mathf.Sin(t * 7f + num11)), t * 0.6f + num11);
			}
			Blush(1f);
			break;
		}
		case ExprId.scared:
		{
			float num6 = Mathf.Sin(t * 47f) * 3f;
			float num7 = Mathf.Cos(t * 53f) * 2f;
			float[] array = new float[2] { num, num2 };
			foreach (float num8 in array)
			{
				canvas.Ellipse(num8 + num6, num3 + num7 - num4 * 0.05f, num4 * 0.32f, num4 * 0.4f, FEAR, 20f);
			}
			canvas.Polyline(new Vector2[7]
			{
				new Vector2(256f - num4 * 0.6f + num6, num3 + num4 * 0.85f + num7),
				new Vector2(256f - num4 * 0.4f + num6, num3 + num4 * 0.73f + num7),
				new Vector2(256f - num4 * 0.2f + num6, num3 + num4 * 0.85f + num7),
				new Vector2(256f + num6, num3 + num4 * 0.73f + num7),
				new Vector2(256f + num4 * 0.2f + num6, num3 + num4 * 0.85f + num7),
				new Vector2(256f + num4 * 0.4f + num6, num3 + num4 * 0.73f + num7),
				new Vector2(256f + num4 * 0.6f + num6, num3 + num4 * 0.85f + num7)
			}, 10f, FEAR);
			break;
		}
		}
		Canvas.Upload(Texture);
	}

	private void DrawSymbolAll()
	{
		PixelCanvas symbolCanvas = SymbolCanvas;
		symbolCanvas.Clear(new Color32(0, 0, 0, 0));
		if (Symbol == FaceSymbol.None)
		{
			symbolCanvas.Upload(SymbolTexture);
			return;
		}
		switch (Symbol)
		{
		case FaceSymbol.Excl:
		{
			float[] array2 = new float[2] { 95f, 160f };
			foreach (float num in array2)
			{
				Outline(symbolCanvas, new Vector2[4]
				{
					new Vector2(num - 14f, 40f),
					new Vector2(num + 14f, 40f),
					new Vector2(num + 7f, 150f),
					new Vector2(num - 7f, 150f)
				}, new Color32(byte.MaxValue, 90, 60, byte.MaxValue), 8f);
				OutlineCircle(symbolCanvas, num, 190f, 16f, new Color32(byte.MaxValue, 90, 60, byte.MaxValue), 8f);
			}
			break;
		}
		case FaceSymbol.Question:
		case FaceSymbol.QuestionGrey:
			Question(symbolCanvas, 128f, 135f, (Symbol == FaceSymbol.Question) ? new Color32(90, 169, byte.MaxValue, byte.MaxValue) : new Color32(184, 177, 168, byte.MaxValue));
			break;
		case FaceSymbol.Anger:
		{
			for (int k = 0; k < 4; k++)
			{
				float f = (float)k * MathF.PI / 2f + MathF.PI / 4f;
				float x = 128f + Mathf.Cos(f) * 16f;
				float y = 128f + Mathf.Sin(f) * 16f;
				float x2 = 128f + Mathf.Cos(f) * 58f - Mathf.Sin(f) * 22f;
				float y2 = 128f + Mathf.Sin(f) * 58f + Mathf.Cos(f) * 22f;
				symbolCanvas.Quadratic(x, y, 128f + Mathf.Cos(f) * 34f, 128f + Mathf.Sin(f) * 34f, x2, y2, 14f, new Color32(byte.MaxValue, 138, 42, byte.MaxValue), 4f);
			}
			break;
		}
		case FaceSymbol.Heart:
			Heart(symbolCanvas);
			break;
		case FaceSymbol.Sparkle:
		{
			(float, float, float, float)[] array3 = new(float, float, float, float)[3]
			{
				(90f, 120f, 46f, 0f),
				(175f, 70f, 30f, 1.7f),
				(180f, 175f, 24f, 3.1f)
			};
			for (int l = 0; l < array3.Length; l++)
			{
				var (x4, y4, num2, num3) = array3[l];
				Sparkle(symbolCanvas, x4, y4, num2 * (0.75f + 0.25f * Mathf.Sin(t * 5f + num3)));
			}
			break;
		}
		case FaceSymbol.Zz:
			Z(symbolCanvas, 80f, 180f, 1f);
			Z(symbolCanvas, 160f, 110f, 0.7f);
			break;
		case FaceSymbol.Puff:
		{
			(float, float, float)[] array = new(float, float, float)[4]
			{
				(95f, 140f, 40f),
				(140f, 120f, 48f),
				(180f, 145f, 36f),
				(130f, 165f, 40f)
			};
			for (int l = 0; l < array.Length; l++)
			{
				var (x3, y3, r2) = array[l];
				OutlineCircle(symbolCanvas, x3, y3, r2, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue), 8f);
			}
			break;
		}
		case FaceSymbol.Sweat:
			Teardrop(symbolCanvas, 90f, 110f, 30f, new Color32(127, 208, byte.MaxValue, byte.MaxValue));
			Teardrop(symbolCanvas, 170f, 160f, 24f, new Color32(127, 208, byte.MaxValue, byte.MaxValue));
			break;
		case FaceSymbol.Dots:
		{
			for (int j = 0; j < 3; j++)
			{
				bool flag = Mathf.FloorToInt(t * 3f) % 4 > j;
				OutlineCircle(symbolCanvas, 70f + (float)j * 58f, 150f, 18f, flag ? new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) : new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 80), 6f);
			}
			break;
		}
		case FaceSymbol.Shake:
		{
			for (int i = 0; i < 2; i++)
			{
				float r = ((i == 0) ? 60f : 95f);
				symbolCanvas.Arc(128f, 150f, r, MathF.PI * -17f / 20f, -1.8849558f, 12f, new Color32(138, 122, 106, byte.MaxValue));
				symbolCanvas.Arc(128f, 150f, r, MathF.PI * -2f / 5f, -0.47123894f, 12f, new Color32(138, 122, 106, byte.MaxValue));
			}
			break;
		}
		}
		SymbolCanvas.Upload(SymbolTexture);
	}

	private static void Sparkle(PixelCanvas c, float x, float y, float r)
	{
		Vector2[] pts = new Vector2[8]
		{
			new Vector2(x, y - r),
			new Vector2(x + r * 0.15f, y - r * 0.15f),
			new Vector2(x + r, y),
			new Vector2(x + r * 0.15f, y + r * 0.15f),
			new Vector2(x, y + r),
			new Vector2(x - r * 0.15f, y + r * 0.15f),
			new Vector2(x - r, y),
			new Vector2(x - r * 0.15f, y - r * 0.15f)
		};
		c.Polygon(pts, new Color32(byte.MaxValue, 216, 74, byte.MaxValue), 14f, 7f, DARK);
	}

	private static void Heart(PixelCanvas c)
	{
		List<Vector2> list = new List<Vector2>();
		for (int i = 0; i < 40; i++)
		{
			float num = (float)i * MathF.PI * 2f / 40f;
			float num2 = 16f * Mathf.Pow(Mathf.Sin(num), 3f);
			float num3 = 13f * Mathf.Cos(num) - 5f * Mathf.Cos(2f * num) - 2f * Mathf.Cos(3f * num) - Mathf.Cos(4f * num);
			list.Add(new Vector2(128f + num2 * 5.2f, 130f - num3 * 5.2f));
		}
		c.Polygon(list.ToArray(), new Color32(byte.MaxValue, 107, 122, byte.MaxValue), 12f, 7f, DARK);
	}

	private static void Teardrop(PixelCanvas c, float x, float y, float r, Color32 color)
	{
		c.Polygon(new Vector2[3]
		{
			new Vector2(x, y - r * 1.7f),
			new Vector2(x - r * 0.8f, y + r * 0.1f),
			new Vector2(x + r * 0.8f, y + r * 0.1f)
		}, color, 8f, 5f, DARK);
		c.Circle(x, y + r * 0.25f, r * 0.8f, color, 8f);
	}

	private static void Outline(PixelCanvas c, Vector2[] pts, Color32 fill, float w)
	{
		c.Polygon(pts, fill, 8f, w, DARK);
	}

	private static void OutlineCircle(PixelCanvas c, float x, float y, float r, Color32 fill, float w)
	{
		c.Circle(x, y, r + w * 0.5f, DARK);
		c.Circle(x, y, r, fill);
	}

	private static void Question(PixelCanvas c, float x, float y, Color32 color)
	{
		c.Polyline(new Vector2[8]
		{
			new Vector2(x - 34f, y - 38f),
			new Vector2(x - 20f, y - 58f),
			new Vector2(x + 8f, y - 64f),
			new Vector2(x + 34f, y - 48f),
			new Vector2(x + 38f, y - 18f),
			new Vector2(x + 20f, y + 2f),
			new Vector2(x + 4f, y + 18f),
			new Vector2(x, y + 38f)
		}, 18f, DARK);
		c.Polyline(new Vector2[8]
		{
			new Vector2(x - 34f, y - 38f),
			new Vector2(x - 20f, y - 58f),
			new Vector2(x + 8f, y - 64f),
			new Vector2(x + 34f, y - 48f),
			new Vector2(x + 38f, y - 18f),
			new Vector2(x + 20f, y + 2f),
			new Vector2(x + 4f, y + 18f),
			new Vector2(x, y + 38f)
		}, 12f, color);
		OutlineCircle(c, x, y + 70f, 9f, color, 6f);
	}

	private static void Z(PixelCanvas c, float x, float y, float scale)
	{
		Vector2[] pts = new Vector2[4]
		{
			new Vector2(x - 40f * scale, y - 34f * scale),
			new Vector2(x + 40f * scale, y - 34f * scale),
			new Vector2(x - 40f * scale, y + 34f * scale),
			new Vector2(x + 40f * scale, y + 34f * scale)
		};
		c.Polyline(pts, 20f * scale, DARK);
		c.Polyline(pts, 12f * scale, new Color32(127, 196, byte.MaxValue, byte.MaxValue));
	}

	private void ArcEye(float x, float y, float s, Color32 color)
	{
		Canvas.Arc(x, y + s * 0.3f, s * 0.55f, 3.3929203f, 6.031858f, 30f, color, 14f);
	}

	private void Lines(float L, float R, float y, float s, Color32 color)
	{
		if (Mathf.Abs(L - R) < 0.01f)
		{
			Canvas.Line(L - s * 0.5f, y, L + s * 0.5f, y, 28f, color, 14f);
			return;
		}
		Canvas.Line(L - s * 0.5f, y, L + s * 0.5f, y, 28f, color, 14f);
		Canvas.Line(R - s * 0.5f, y, R + s * 0.5f, y, 28f, color, 14f);
	}

	private void Ring(float x, float y, float r, Color32 color, float w)
	{
		Canvas.Arc(x, y, r, 0f, MathF.PI * 2f, w, color, 12f);
	}

	private void Chevron(float x, float y, float s, int dir, Color32 color)
	{
		Canvas.Polyline(new Vector2[3]
		{
			new Vector2(x - (float)dir * s * 0.45f, y - s * 0.55f),
			new Vector2(x + (float)dir * s * 0.45f, y),
			new Vector2(x - (float)dir * s * 0.45f, y + s * 0.55f)
		}, 28f, color, 14f);
	}

	private void Blush(float k)
	{
		float num = ((k > 1.5f) ? 4f : 3f);
		for (int i = 0; i < 2; i++)
		{
			float num2 = ((i == 0) ? 115.2f : 396.8f);
			for (int j = 0; (float)j < num; j++)
			{
				float num3 = num2 - (num - 1f) / 2f * 16.5f + (float)j * 16.5f;
				float num4 = 288f;
				float num5 = 11f * Mathf.Min(k, 1.3f);
				Canvas.Line(num3 + 4f, num4 - num5, num3 - 4f, num4 + num5, 7f, new Color32(BLUSH.r, BLUSH.g, BLUSH.b, (byte)(255f * Mathf.Min(1f, 0.58f + 0.25f * k))), 6f);
			}
		}
	}

	private void Drop(float x, float y, float r, float alpha, Color32 color)
	{
		List<Vector2> list = new List<Vector2>();
		for (int i = 0; i <= 16; i++)
		{
			float f = MathF.PI * (float)i / 16f;
			list.Add(new Vector2(x + Mathf.Cos(f) * r, y + r * 0.2f + Mathf.Sin(f) * r));
		}
		list.Add(new Vector2(x, y - r * 1.8f));
		Color32 c = new Color32(color.r, color.g, color.b, (byte)(255f * alpha));
		Canvas.Polygon(list.ToArray(), c, 8f);
	}

	private void Star(float x, float y, float r, float rot)
	{
		Vector2[] array = new Vector2[10];
		for (int i = 0; i < 10; i++)
		{
			float f = rot * 0.2f - MathF.PI / 2f + (float)i * MathF.PI / 5f;
			float num = ((i % 2 == 1) ? (r * 0.45f) : r);
			array[i] = new Vector2(x + Mathf.Cos(f) * num, y + Mathf.Sin(f) * num);
		}
		Canvas.Polygon(array, new Color32(byte.MaxValue, 224, 102, byte.MaxValue), 14f);
	}
}
