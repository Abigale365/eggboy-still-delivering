using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class TideTownStyleUI
{
	public static readonly Color32 PaperColor = new Color32(byte.MaxValue, 245, 223, byte.MaxValue);

	public static readonly Color32 PaperRuleColor = new Color32(216, 210, 182, 210);

	public static readonly Color32 SlipColor = new Color32(byte.MaxValue, 249, 238, byte.MaxValue);

	public static readonly Color32 SlipBorderColor = new Color32(216, 184, 136, byte.MaxValue);

	public static readonly Color32 InkDark = new Color32(41, 58, 57, byte.MaxValue);

	public static readonly Color32 InkMuted = new Color32(97, 102, 86, byte.MaxValue);

	public static readonly Color32 SeaBlue = new Color32(41, 97, 112, byte.MaxValue);

	public static readonly Color32 SeaMist = new Color32(214, 231, 225, byte.MaxValue);

	public static readonly Color32 Clay = new Color32(165, 75, 53, byte.MaxValue);

	public static readonly Color32 BarkPanel = new Color32(38, 70, 76, 235);

	public static readonly Color32 CreamText = new Color32(251, 239, 217, byte.MaxValue);

	public static readonly Color32 AmberAccent = new Color32(byte.MaxValue, 154, 77, byte.MaxValue);

	public static readonly Color32 AmberSoft = new Color32(byte.MaxValue, 223, 192, byte.MaxValue);

	private static Font cachedFont;

	private static Sprite cachedRounded;

	private static Sprite cachedWhite;

	private static Sprite paperSprite;

	private static Sprite cachedBorder;

	private static Sprite cachedFrame;

	private static Sprite cachedWax;

	private static Sprite cachedPostmark;

	private static Sprite cachedKeycap;

	private static Sprite cachedVignette;

	private static Sprite cachedPill;

	private static Texture2D cachedCompass;

	public static Sprite PaperSprite
	{
		get
		{
			if (paperSprite != null)
			{
				return paperSprite;
			}
			Texture2D texture2D = NewTex(128, 128);
			Color32[] array = new Color32[16384];
			for (int i = 0; i < 128; i++)
			{
				for (int j = 0; j < 128; j++)
				{
					float num = RoundRectSdf((float)j + 0.5f, (float)i + 0.5f, 128f, 128f, 13f);
					float num2 = Mathf.PerlinNoise((float)j * 0.16f, (float)i * 0.39f);
					byte b = (byte)Mathf.RoundToInt((0.958f + 0.042f * num2 - 0.09f * Mathf.Clamp01(1f - Mathf.Abs(num + 2f) * 0.8f)) * 255f);
					array[i * 128 + j] = new Color32(b, b, b, (byte)(Mathf.Clamp01(0.5f - num) * 255f));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			paperSprite = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
			return paperSprite;
		}
	}

	public static Font UiFont
	{
		get
		{
			if (cachedFont == null)
			{
				cachedFont = Resources.Load<Font>("Fonts/NotoSansSC-VF");
				if (cachedFont == null)
				{
					cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
				}
			}
			return cachedFont;
		}
	}

	public static Sprite RoundedSprite
	{
		get
		{
			if (cachedRounded == null)
			{
				cachedRounded = MakeRounded(64, 14);
			}
			return cachedRounded;
		}
	}

	public static Sprite WhiteSprite
	{
		get
		{
			if (cachedWhite == null)
			{
				cachedWhite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
			}
			return cachedWhite;
		}
	}

	public static Sprite RoundedBorderSprite
	{
		get
		{
			if (cachedBorder != null)
			{
				return cachedBorder;
			}
			Texture2D texture2D = NewTex(96, 96);
			Color32[] array = new Color32[9216];
			for (int i = 0; i < 96; i++)
			{
				for (int j = 0; j < 96; j++)
				{
					float num = RoundRectSdf((float)j + 0.5f, (float)i + 0.5f, 96f, 96f, 16f);
					float num2 = Mathf.Clamp01(0.5f - num);
					float t = 1f - Mathf.Clamp01(Mathf.Abs(num + 5.5f) - 0.6f);
					byte b = (byte)(Mathf.Lerp(t: 1f - Mathf.Clamp01(Mathf.Abs(num + 8.5f) - 0.4f), a: Mathf.Lerp(1f, 0.62f, t), b: 0.74f) * 255f);
					array[i * 96 + j] = new Color32(b, b, b, (byte)(num2 * 255f));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			cachedBorder = Sprite.Create(texture2D, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
			return cachedBorder;
		}
	}

	public static Sprite RoundedFrameSprite
	{
		get
		{
			if (cachedFrame != null)
			{
				return cachedFrame;
			}
			Texture2D texture2D = NewTex(96, 96);
			Color32[] array = new Color32[9216];
			for (int i = 0; i < 96; i++)
			{
				for (int j = 0; j < 96; j++)
				{
					float num = RoundRectSdf((float)j + 0.5f, (float)i + 0.5f, 96f, 96f, 12f);
					float value = Mathf.Max(1f - Mathf.Clamp01(Mathf.Abs(num + 1.5f) - 0.5f), 0.6f * (1f - Mathf.Clamp01(Mathf.Abs(num + 5f) - 0.3f)));
					array[i * 96 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(Mathf.Clamp01(value) * 255f));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			cachedFrame = Sprite.Create(texture2D, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
			return cachedFrame;
		}
	}

	public static Sprite PillSprite
	{
		get
		{
			if (cachedPill != null)
			{
				return cachedPill;
			}
			Texture2D texture2D = NewTex(64, 64);
			Color32[] array = new Color32[4096];
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float num = RoundRectSdf((float)j + 0.5f, (float)i + 0.5f, 64f, 64f, 31.5f);
					array[i * 64 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(Mathf.Clamp01(0.5f - num) * 255f));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			cachedPill = Sprite.Create(texture2D, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(31f, 31f, 31f, 31f));
			return cachedPill;
		}
	}

	public static Sprite WaxSealSprite
	{
		get
		{
			if (cachedWax != null)
			{
				return cachedWax;
			}
			float num = 64f;
			Texture2D texture2D = NewTex(128, 128);
			Color32[] array = new Color32[16384];
			Color a = new Color(0.7f, 0.16f, 0.12f);
			Color b = new Color(0.46f, 0.08f, 0.07f);
			Color b2 = new Color(0.9f, 0.38f, 0.3f);
			for (int i = 0; i < 128; i++)
			{
				for (int j = 0; j < 128; j++)
				{
					float num2 = (float)j + 0.5f - num;
					float num3 = (float)i + 0.5f - num;
					float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
					float num5 = Mathf.Atan2(num3, num2);
					float num6 = Mathf.Clamp01(56f + 3.2f * Mathf.Sin(num5 * 9f) + 1.6f * Mathf.Sin(num5 * 23f + 1.3f) - num4 + 0.5f);
					if (num6 <= 0f)
					{
						array[i * 128 + j] = new Color32(0, 0, 0, 0);
						continue;
					}
					Color a2 = Color.Lerp(a, b2, Mathf.Clamp01((0f - num2 - num3) / 90f) * 0.45f);
					a2 = Color.Lerp(a2, b, Mathf.Clamp01((num4 - 44f) / 12f) * 0.55f);
					float num7 = 1f - Mathf.Clamp01(Mathf.Abs(num4 - 38f) - 1.2f);
					a2 = Color.Lerp(a2, b, num7 * 0.8f);
					float num8 = 1f - Mathf.Clamp01(Mathf.Abs(num4 - 34f) - 0.5f);
					a2 = Color.Lerp(a2, b2, num8 * 0.5f);
					float num9 = Mathf.Abs(num2);
					float num10 = Mathf.Abs(num3);
					float num11 = Mathf.Clamp01(1.2f - (Mathf.Pow(num9 / 20f, 0.5f) + Mathf.Pow(num10 / 20f, 0.5f)) * 1f);
					a2 = Color.Lerp(a2, b, Mathf.Clamp01(num11 * 3f) * 0.85f);
					for (int k = 0; k < 8; k++)
					{
						float f = (float)k * MathF.PI / 4f + MathF.PI / 8f;
						float num12 = num2 - Mathf.Cos(f) * 27f;
						float num13 = num3 - Mathf.Sin(f) * 27f;
						if (num12 * num12 + num13 * num13 < 5.5f)
						{
							a2 = Color.Lerp(a2, b, 0.8f);
						}
					}
					array[i * 128 + j] = new Color(a2.r, a2.g, a2.b, num6);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			cachedWax = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f);
			return cachedWax;
		}
	}

	public static Sprite PostmarkRingSprite
	{
		get
		{
			if (cachedPostmark != null)
			{
				return cachedPostmark;
			}
			float num = 80f;
			Texture2D texture2D = NewTex(160, 160);
			Color32[] array = new Color32[25600];
			for (int i = 0; i < 160; i++)
			{
				for (int j = 0; j < 160; j++)
				{
					float num2 = (float)j + 0.5f - num;
					float num3 = (float)i + 0.5f - num;
					float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
					float a = 1f - Mathf.Clamp01(Mathf.Abs(num4 - 70f) - 2.2f);
					a = Mathf.Max(a, 1f - Mathf.Clamp01(Mathf.Abs(num4 - 60f) - 0.8f));
					if (num4 < 56f)
					{
						for (int k = -1; k <= 1; k++)
						{
							float num5 = (float)k * 14f + Mathf.Sin(num2 * 0.11f) * 3f;
							a = Mathf.Max(a, (1f - Mathf.Clamp01(Mathf.Abs(num3 - num5) - 1.1f)) * Mathf.Clamp01((56f - num4) / 6f));
						}
					}
					float num6 = Mathf.PerlinNoise((float)j * 0.09f, (float)i * 0.09f) * 0.7f + Mathf.PerlinNoise((float)j * 0.37f + 9f, (float)i * 0.37f) * 0.3f;
					a *= Mathf.Clamp01(0.35f + num6);
					array[i * 160 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(Mathf.Clamp01(a) * 255f));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			cachedPostmark = Sprite.Create(texture2D, new Rect(0f, 0f, 160f, 160f), new Vector2(0.5f, 0.5f), 100f);
			return cachedPostmark;
		}
	}

	public static Sprite KeycapSprite
	{
		get
		{
			if (cachedKeycap != null)
			{
				return cachedKeycap;
			}
			Texture2D texture2D = NewTex(64, 64);
			Color32[] array = new Color32[4096];
			Color a = new Color(0.36f, 0.24f, 0.15f);
			Color b = new Color(0.98f, 0.94f, 0.86f);
			Color a2 = new Color(0.92f, 0.85f, 0.73f);
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float num = RoundRectSdf((float)j + 0.5f, (float)i + 0.5f, 64f, 64f, 10f);
					float a3 = Mathf.Clamp01(0.5f - num);
					float num2 = RoundRectSdf((float)j + 0.5f - 3f, (float)i + 0.5f - 7f, 58f, 54f, 8f);
					float t = Mathf.Clamp01(0.5f - num2);
					Color a4 = Color.Lerp(a, Color.Lerp(a2, b, ((float)i - 7f) / 54f), t);
					float num3 = (1f - Mathf.Clamp01(Mathf.Abs(num2 + 2f) - 0.6f)) * Mathf.Clamp01(((float)i - 38.4f) / 19.2f);
					a4 = Color.Lerp(a4, Color.white, num3 * 0.7f);
					array[i * 64 + j] = new Color(a4.r, a4.g, a4.b, a3);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			cachedKeycap = Sprite.Create(texture2D, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(14f, 16f, 14f, 14f));
			return cachedKeycap;
		}
	}

	public static Sprite SoftVignetteSprite
	{
		get
		{
			if (cachedVignette != null)
			{
				return cachedVignette;
			}
			Texture2D texture2D = NewTex(256, 144);
			Color32[] array = new Color32[36864];
			for (int i = 0; i < 144; i++)
			{
				for (int j = 0; j < 256; j++)
				{
					float num = ((float)j + 0.5f) / 256f * 2f - 1f;
					float num2 = ((float)i + 0.5f) / 144f * 2f - 1f;
					float num3 = Mathf.Sqrt(num * num * 0.85f + num2 * num2 * 1.15f);
					float num4 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num3 - 0.55f) / 0.75f));
					array[i * 256 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num4 * 255f));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			cachedVignette = Sprite.Create(texture2D, new Rect(0f, 0f, 256f, 144f), new Vector2(0.5f, 0.5f), 100f);
			return cachedVignette;
		}
	}

	public static Texture2D CompassRoseTexture
	{
		get
		{
			if (cachedCompass != null)
			{
				return cachedCompass;
			}
			float num = 64f;
			Texture2D texture2D = NewTex(128, 128);
			Color32[] array = new Color32[16384];
			Color color = new Color(0.23f, 0.16f, 0.1f);
			Color color2 = new Color(0.98f, 0.95f, 0.88f);
			Color color3 = new Color(0.55f, 0.42f, 0.29f);
			Color color4 = new Color(0.66f, 0.18f, 0.13f);
			Color color5 = new Color(0.88f, 0.45f, 0.36f);
			for (int i = 0; i < 128; i++)
			{
				for (int j = 0; j < 128; j++)
				{
					float num2 = (float)j + 0.5f - num;
					float num3 = (float)i + 0.5f - num;
					float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
					Color dst = new Color(0f, 0f, 0f, 0f);
					dst = Over(dst, color2, 0.8f * Mathf.Clamp01(60.5f - num4));
					float a = Mathf.Max(1f - Mathf.Clamp01(Mathf.Abs(num4 - 59f) - 1.1f), 0.8f * (1f - Mathf.Clamp01(Mathf.Abs(num4 - 53.5f) - 0.45f)));
					float num5 = Mathf.Atan2(num3, num2) * 57.29578f;
					float num6 = Mathf.Abs(Mathf.DeltaAngle(num5, Mathf.Round(num5 / 22.5f) * 22.5f)) * (MathF.PI / 180f) * num4;
					if (num4 > 53.5f && num4 < 59f)
					{
						a = Mathf.Max(a, 0.85f * Mathf.Clamp01(1.2f - num6));
					}
					dst = Over(dst, color3, a);
					float num7 = (num2 + num3) * 0.7071f;
					float num8 = (num2 - num3) * 0.7071f;
					float num9 = Mathf.Min(Mathf.Abs(num7) / 22f + Mathf.Abs(num8) / 4.5f, Mathf.Abs(num8) / 22f + Mathf.Abs(num7) / 4.5f);
					float num10 = Mathf.Clamp01((1f - num9) * 6f);
					if (num10 > 0f)
					{
						dst = Over(dst, (num7 * num8 > 0f) ? Color.Lerp(color3, color2, 0.35f) : color3, num10 * 0.9f);
					}
					float num11 = Mathf.Abs(num3) / 33f + Mathf.Abs(num2) / 7.5f;
					float num12 = Mathf.Abs(num2) / 33f + Mathf.Abs(num3) / 7.5f;
					dst = Over(dst, color, 0.9f * Mathf.Clamp01((1.12f - Mathf.Min(num11, num12)) * 10f));
					float num13 = Mathf.Clamp01((1f - num12) * 7f);
					float num14 = Mathf.Clamp01((1f - num11) * 7f);
					if (num13 > 0f)
					{
						dst = Over(dst, (num3 > 0f) ? color : color2, num13);
					}
					if (num14 > 0f)
					{
						dst = Over(dst, (!(num3 > 0f)) ? ((num2 < 0f) ? color : color2) : ((num2 < 0f) ? color4 : color5), num14);
					}
					dst = Over(dst, color, Mathf.Clamp01(3.2f - num4));
					dst = Over(dst, color2, Mathf.Clamp01(1.6f - num4));
					array[i * 128 + j] = dst;
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			cachedCompass = texture2D;
			return cachedCompass;
		}
	}

	public static RectTransform FindHudCanvasRoot(EggboyUI ui)
	{
		if (ui == null)
		{
			return null;
		}
		Transform transform = ui.transform.Find("EGGBOY HUD");
		if (!(transform != null))
		{
			return null;
		}
		return transform.GetComponent<RectTransform>();
	}

	public static RectTransform FindLetterOverlay(EggboyUI ui)
	{
		RectTransform rectTransform = FindHudCanvasRoot(ui);
		if (rectTransform == null || rectTransform.childCount < 2)
		{
			return null;
		}
		Transform transform = rectTransform.Find("LetterOverlay");
		if (transform != null)
		{
			return transform.GetComponent<RectTransform>();
		}
		for (int i = 0; i < rectTransform.childCount; i++)
		{
			Transform child = rectTransform.GetChild(i);
			if (child.childCount > 0 && child.GetChild(0).GetComponent<Shadow>() != null)
			{
				return child.GetComponent<RectTransform>();
			}
		}
		return rectTransform.GetChild(1).GetComponent<RectTransform>();
	}

	public static RectTransform FindLetterPaper(EggboyUI ui)
	{
		RectTransform rectTransform = FindLetterOverlay(ui);
		if (rectTransform == null || rectTransform.childCount == 0)
		{
			return null;
		}
		Transform transform = rectTransform.Find("LetterPaper");
		if (transform != null)
		{
			return transform.GetComponent<RectTransform>();
		}
		return rectTransform.GetChild(0).GetComponent<RectTransform>();
	}

	private static Texture2D NewTex(int w, int h)
	{
		return new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
	}

	private static float RoundRectSdf(float x, float y, float w, float h, float r)
	{
		float a = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
		float num = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
		float num2 = Mathf.Max(a, 0f);
		float num3 = Mathf.Max(num, 0f);
		return Mathf.Sqrt(num2 * num2 + num3 * num3) + Mathf.Min(Mathf.Max(a, num), 0f) - r;
	}

	private static Color Over(Color dst, Color src, float a)
	{
		a = Mathf.Clamp01(a);
		float num = a + dst.a * (1f - a);
		if (num <= 0f)
		{
			return new Color(0f, 0f, 0f, 0f);
		}
		float num2 = dst.a * (1f - a);
		return new Color((src.r * a + dst.r * num2) / num, (src.g * a + dst.g * num2) / num, (src.b * a + dst.b * num2) / num, num);
	}

	public static void StyleSettingButton(Button btn)
	{
		if (!(btn == null))
		{
			Image image = btn.targetGraphic as Image;
			if (image != null)
			{
				image.sprite = PaperSprite;
				image.type = Image.Type.Sliced;
				image.color = SeaMist;
			}
			StyleButtonStates(btn);
			Shadow[] components = btn.GetComponents<Shadow>();
			for (int i = 0; i < components.Length; i++)
			{
				UnityEngine.Object.Destroy(components[i]);
			}
			Text componentInChildren = btn.GetComponentInChildren<Text>();
			if (componentInChildren != null)
			{
				componentInChildren.color = InkDark;
				componentInChildren.fontSize = 14;
				componentInChildren.fontStyle = FontStyle.Normal;
				componentInChildren.alignment = TextAnchor.MiddleLeft;
				RectTransform rectTransform = componentInChildren.rectTransform;
				rectTransform.anchorMin = Vector2.zero;
				rectTransform.anchorMax = Vector2.one;
				rectTransform.offsetMin = new Vector2(16f, 0f);
				rectTransform.offsetMax = new Vector2(-10f, 0f);
			}
		}
	}

	public static void StyleButtonStates(Selectable btn)
	{
		btn.transition = Selectable.Transition.ColorTint;
		btn.colors = new ColorBlock
		{
			normalColor = Color.white,
			highlightedColor = new Color32(232, 248, 248, byte.MaxValue),
			pressedColor = new Color32(197, 221, 224, byte.MaxValue),
			selectedColor = new Color32(217, 239, 239, byte.MaxValue),
			disabledColor = new Color32(200, 200, 200, 180),
			colorMultiplier = 1f,
			fadeDuration = 0.1f
		};
	}

	public static Image CreatePanel(Transform parent, string name, float w, float h, Color32 color, bool rounded = true)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		Image component = gameObject.GetComponent<Image>();
		component.sprite = (rounded ? RoundedSprite : WhiteSprite);
		component.type = (rounded ? Image.Type.Sliced : Image.Type.Simple);
		component.color = color;
		gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
		return component;
	}

	public static RectTransform CreateParchmentCard(Transform parent, string name, float w, float h, int ruledLines = 7)
	{
		Image image = CreatePanel(parent, name, w, h, PaperColor);
		image.sprite = PaperSprite;
		Outline outline = image.gameObject.AddComponent<Outline>();
		outline.effectColor = SeaBlue;
		outline.effectDistance = new Vector2(2f, -2f);
		Shadow shadow = image.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color32(26, 16, 8, 80);
		shadow.effectDistance = new Vector2(4f, -7f);
		if (h >= 140f && w >= 260f)
		{
			Image image2 = CreatePanel(image.transform, "CardPostmark", 66f, 66f, new Color32(192, 58, 44, 70), rounded: false);
			image2.sprite = PostmarkRingSprite;
			image2.raycastTarget = false;
			RectTransform rectTransform = image2.rectTransform;
			Vector2 vector2 = (rectTransform.pivot = new Vector2(1f, 1f));
			Vector2 anchorMin = (rectTransform.anchorMax = vector2);
			rectTransform.anchorMin = anchorMin;
			rectTransform.anchoredPosition = new Vector2(-14f, -12f);
			rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
		}
		for (int i = 0; i < ruledLines; i++)
		{
			Image image3 = CreatePanel(image.transform, "Rule_" + i, w - 72f, 1f, PaperRuleColor, rounded: false);
			image3.raycastTarget = false;
			RectTransform rectTransform2 = image3.rectTransform;
			Vector2 vector2 = (rectTransform2.pivot = new Vector2(0.5f, 0.5f));
			Vector2 anchorMin = (rectTransform2.anchorMax = vector2);
			rectTransform2.anchorMin = anchorMin;
			rectTransform2.anchoredPosition = new Vector2(0f, h * 0.32f - (float)i * 34f);
		}
		return image.rectTransform;
	}

	public static Text CreateText(Transform parent, string content, int fontSize, Color32 color, TextAnchor align, Vector2 anchoredPos, Vector2 size, bool bold = false)
	{
		GameObject gameObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		Text component = gameObject.GetComponent<Text>();
		component.font = UiFont;
		component.fontSize = fontSize;
		component.fontStyle = (bold ? FontStyle.Bold : FontStyle.Normal);
		component.color = color;
		component.alignment = align;
		component.text = content;
		component.supportRichText = false;
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		component.verticalOverflow = VerticalWrapMode.Overflow;
		component.lineSpacing = 1.28f;
		component.raycastTarget = false;
		RectTransform component2 = gameObject.GetComponent<RectTransform>();
		Vector2 vector2 = (component2.pivot = new Vector2(0.5f, 0.5f));
		Vector2 anchorMin = (component2.anchorMax = vector2);
		component2.anchorMin = anchorMin;
		component2.anchoredPosition = anchoredPos;
		component2.sizeDelta = size;
		return component;
	}

	public static Button CreateWarmButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size, bool primaryAmber, Action onClick)
	{
		Color32 color = (primaryAmber ? Clay : SeaMist);
		Color32 color2 = (primaryAmber ? new Color32(byte.MaxValue, 248, 236, byte.MaxValue) : InkDark);
		Image image = CreatePanel(parent, "Btn_" + label, size.x, size.y, color);
		image.sprite = PaperSprite;
		if (primaryAmber)
		{
			Outline outline = image.gameObject.AddComponent<Outline>();
			outline.effectColor = new Color32(106, 48, 20, 150);
			outline.effectDistance = new Vector2(1.2f, -1.2f);
		}
		RectTransform rectTransform = image.rectTransform;
		Vector2 vector2 = (rectTransform.pivot = new Vector2(0.5f, 0.5f));
		Vector2 anchorMin = (rectTransform.anchorMax = vector2);
		rectTransform.anchorMin = anchorMin;
		rectTransform.anchoredPosition = anchoredPos;
		Shadow shadow = image.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color32(59, 42, 26, 55);
		shadow.effectDistance = new Vector2(1.5f, -2.5f);
		Button button = image.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		StyleButtonStates(button);
		if (onClick != null)
		{
			button.onClick.AddListener(delegate
			{
				onClick();
			});
		}
		CreateText(image.transform, label, 15, color2, TextAnchor.MiddleCenter, Vector2.zero, size - new Vector2(12f, 4f), bold: true);
		return button;
	}

	public static GameObject CloneSceneLux3D(string primarySceneName, float targetSize, bool addWalkableGroundCollider = false)
	{
		GameObject gameObject = FindSceneObject(primarySceneName);
		if (gameObject == null)
		{
			return null;
		}
		GameObject gameObject2 = new GameObject("Lux3D_" + primarySceneName);
		GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, gameObject2.transform, worldPositionStays: false);
		gameObject3.name = "Model";
		gameObject3.transform.localPosition = Vector3.zero;
		gameObject3.transform.localRotation = Quaternion.identity;
		gameObject3.SetActive(value: true);
		MonoBehaviour[] componentsInChildren = gameObject3.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		Collider[] componentsInChildren2 = gameObject3.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren2[i]);
		}
		Renderer[] componentsInChildren3 = gameObject3.GetComponentsInChildren<Renderer>(includeInactive: true);
		if (componentsInChildren3.Length != 0)
		{
			Renderer[] array = componentsInChildren3;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = true;
			}
			Bounds bounds = componentsInChildren3[0].bounds;
			for (int j = 1; j < componentsInChildren3.Length; j++)
			{
				bounds.Encapsulate(componentsInChildren3[j].bounds);
			}
			float num = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
			float num2 = ((num > 0.001f && targetSize > 0.001f) ? (targetSize / num) : 1f);
			gameObject3.transform.localScale *= num2;
			bounds = componentsInChildren3[0].bounds;
			for (int k = 1; k < componentsInChildren3.Length; k++)
			{
				bounds.Encapsulate(componentsInChildren3[k].bounds);
			}
			Vector3 vector = new Vector3(gameObject2.transform.position.x - bounds.center.x, gameObject2.transform.position.y - bounds.min.y, gameObject2.transform.position.z - bounds.center.z);
			gameObject3.transform.position += vector;
			if (addWalkableGroundCollider)
			{
				int num3 = LayerMask.NameToLayer("Ground");
				if (num3 < 0)
				{
					num3 = 6;
				}
				gameObject2.layer = num3;
				BoxCollider boxCollider = gameObject2.AddComponent<BoxCollider>();
				boxCollider.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
				boxCollider.size = new Vector3(Mathf.Max(0.45f, bounds.size.x * 0.92f), Mathf.Max(0.12f, bounds.size.y * 0.92f), Mathf.Max(0.45f, bounds.size.z * 0.92f));
			}
		}
		return gameObject2;
	}

	public static GameObject FindSceneObject(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		GameObject gameObject = Resources.Load<GameObject>("Mechanisms/" + name);
		if (gameObject != null)
		{
			return gameObject;
		}
		GameObject gameObject2 = Resources.Load<GameObject>("Lux3D/" + name);
		if (gameObject2 != null)
		{
			return gameObject2;
		}
		GameObject gameObject3 = GameObject.Find(name);
		if (gameObject3 != null)
		{
			return gameObject3;
		}
		Transform[] array = UnityEngine.Object.FindObjectsOfType<Transform>();
		foreach (Transform transform in array)
		{
			if (transform.name == name || transform.name.StartsWith(name))
			{
				return transform.gameObject;
			}
		}
		return Resources.Load<GameObject>("Items/ending_oilcan");
	}

	public static GameObject BuildArticulatedMechanism3D(string mechanismName, float overallScale = 1f)
	{
		GameObject gameObject = new GameObject("Mechanism3D_" + mechanismName);
		GameObject gameObject2 = Resources.Load<GameObject>("Mechanisms/" + mechanismName);
		if (gameObject2 != null)
		{
			GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject2, gameObject.transform, worldPositionStays: false);
			gameObject3.name = "GLBRoot";
			gameObject3.transform.localPosition = Vector3.zero;
			gameObject3.transform.localRotation = Quaternion.identity;
			gameObject3.transform.localScale = Vector3.one * overallScale;
			if (FindDeepChild(gameObject.transform, "Ring_0") != null)
			{
				return gameObject;
			}
		}
		Material material = ExtractLuxMaterial("C4_狗窝木箱_后续修改版", "S_doghouse", new Color32(158, 108, 66, byte.MaxValue), 0.12f, 0.35f);
		Material material2 = ExtractLuxMaterial("D3_望远镜_v2", "LH_scope", new Color32(214, 168, 86, byte.MaxValue), 0.72f, 0.65f);
		Material material3 = ExtractLuxMaterial("A6_台秤_v2", "Bakery_scale", new Color32(126, 92, 62, byte.MaxValue), 0.58f, 0.48f);
		bool num = mechanismName?.Contains("M3") ?? false;
		bool flag = mechanismName?.Contains("M4") ?? false;
		CreateMeshNode(gameObject.transform, "Frame_Base", BuildGearWheelMesh(0.46f, 0.38f, 0.14f, 6, 0.015f), flag ? material3 : material, Vector3.zero);
		if (num)
		{
			CreateMeshNode(gameObject.transform, "Ring_0", BuildGearWheelMesh(0.38f, 0.12f, 0.08f, 16, 0.042f), material2, new Vector3(0f, 0.12f, 0f));
			CreateMeshNode(gameObject.transform, "Core_Spindle", BuildGearWheelMesh(0.11f, 0.02f, 0.24f, 8, 0.022f), material, new Vector3(0f, 0.18f, 0f));
		}
		else
		{
			for (int i = 0; i < 3; i++)
			{
				float x = (float)(i - 1) * 0.22f;
				CreateMeshNode(gameObject.transform, "Latch_" + i, BuildBoltLatchMesh(0.065f, 0.22f, 0.055f), (i == 1) ? material2 : material3, new Vector3(x, 0.28f, 0.085f));
			}
			CreateMeshNode(gameObject.transform, "Ring_0", BuildGearWheelMesh(0.36f, 0.26f, 0.06f, 18, 0.035f), flag ? material2 : material, new Vector3(0f, -0.02f, 0.09f));
			CreateMeshNode(gameObject.transform, "Ring_1", BuildGearWheelMesh(0.24f, 0.15f, 0.065f, 12, 0.03f), material2, new Vector3(0f, -0.02f, 0.115f));
			CreateMeshNode(gameObject.transform, "Ring_2", BuildGearWheelMesh(0.13f, 0.06f, 0.07f, 8, 0.024f), material3, new Vector3(0f, -0.02f, 0.14f));
			for (int j = 0; j < 3; j++)
			{
				float f = ((float)j * 120f + 30f) * (MathF.PI / 180f);
				CreateMeshNode(gameObject.transform, "Gear_" + j, BuildGearWheelMesh(0.065f, 0.018f, 0.05f, 8, 0.016f), material2, new Vector3(Mathf.Cos(f) * 0.25f, -0.02f + Mathf.Sin(f) * 0.25f, 0.135f));
			}
			CreateMeshNode(gameObject.transform, "Core_Spindle", BuildGearWheelMesh(0.058f, 0.01f, 0.16f, 6, 0.014f), material2, new Vector3(0f, -0.02f, 0.165f));
		}
		gameObject.transform.localScale = Vector3.one * overallScale;
		return gameObject;
	}

	public static Transform FindDeepChild(Transform parent, string childName)
	{
		if (parent == null)
		{
			return null;
		}
		Transform[] componentsInChildren = parent.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform.name == childName || transform.name.StartsWith(childName))
			{
				return transform;
			}
		}
		return null;
	}

	private static GameObject CreateMeshNode(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localPosition = localPos;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
		gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
		return gameObject;
	}

	private static Material ExtractLuxMaterial(string luxResourceName, string sceneFallbackName, Color32 fallbackColor, float metallic, float smoothness)
	{
		GameObject gameObject = Resources.Load<GameObject>("Lux3D/" + luxResourceName);
		if (gameObject == null)
		{
			gameObject = FindSceneObject(sceneFallbackName);
		}
		if (gameObject != null)
		{
			Renderer componentInChildren = gameObject.GetComponentInChildren<Renderer>(includeInactive: true);
			if (componentInChildren != null && componentInChildren.sharedMaterial != null)
			{
				return componentInChildren.sharedMaterial;
			}
		}
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}
		Material material = new Material(shader);
		material.color = fallbackColor;
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", fallbackColor);
		}
		if (material.HasProperty("_Metallic"))
		{
			material.SetFloat("_Metallic", metallic);
		}
		if (material.HasProperty("_Smoothness"))
		{
			material.SetFloat("_Smoothness", smoothness);
		}
		return material;
	}

	private static Mesh BuildGearWheelMesh(float outerR, float innerR, float thickness, int teeth, float toothH)
	{
		Mesh mesh = new Mesh
		{
			name = "ProcGearWheel"
		};
		int num = Mathf.Max(12, teeth * 4);
		float num2 = thickness * 0.5f;
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<int> list3 = new List<int>();
		for (int i = 0; i < num; i++)
		{
			float f = MathF.PI * 2f * (float)i / (float)num;
			bool flag = i % 4 == 1 || i % 4 == 2;
			float num3 = outerR + (flag ? toothH : 0f);
			float num4 = Mathf.Cos(f);
			float num5 = Mathf.Sin(f);
			list.Add(new Vector3(num3 * num4, num3 * num5, num2));
			list.Add(new Vector3(innerR * num4, innerR * num5, num2));
			list.Add(new Vector3(num3 * num4, num3 * num5, 0f - num2));
			list.Add(new Vector3(innerR * num4, innerR * num5, 0f - num2));
			list2.Add(new Vector2(num3 * num4 + 0.5f, num3 * num5 + 0.5f));
			list2.Add(new Vector2(innerR * num4 + 0.5f, innerR * num5 + 0.5f));
			list2.Add(new Vector2(num3 * num4 + 0.5f, num3 * num5 + 0.5f));
			list2.Add(new Vector2(innerR * num4 + 0.5f, innerR * num5 + 0.5f));
		}
		for (int j = 0; j < num; j++)
		{
			int num6 = (j + 1) % num;
			int num7 = j * 4;
			int num8 = num6 * 4;
			list3.Add(num7 + 1);
			list3.Add(num7);
			list3.Add(num8);
			list3.Add(num7 + 1);
			list3.Add(num8);
			list3.Add(num8 + 1);
			list3.Add(num7 + 2);
			list3.Add(num7 + 3);
			list3.Add(num8 + 3);
			list3.Add(num7 + 2);
			list3.Add(num8 + 3);
			list3.Add(num8 + 2);
			list3.Add(num7);
			list3.Add(num7 + 2);
			list3.Add(num8 + 2);
			list3.Add(num7);
			list3.Add(num8 + 2);
			list3.Add(num8);
			list3.Add(num7 + 3);
			list3.Add(num7 + 1);
			list3.Add(num8 + 1);
			list3.Add(num7 + 3);
			list3.Add(num8 + 1);
			list3.Add(num8 + 3);
		}
		mesh.SetVertices(list);
		mesh.SetUVs(0, list2);
		mesh.SetTriangles(list3, 0);
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		return mesh;
	}

	private static Mesh BuildBoltLatchMesh(float w, float h, float d)
	{
		return BuildGearWheelMesh(Mathf.Max(w, d) * 0.65f, 0.01f, h, 4, 0.012f);
	}

	private static Sprite MakeRounded(int S, int R)
	{
		Texture2D texture2D = new Texture2D(S, S, TextureFormat.RGBA32, mipChain: false, linear: false);
		Color32[] array = new Color32[S * S];
		for (int i = 0; i < S; i++)
		{
			for (int j = 0; j < S; j++)
			{
				float num = Mathf.Max((float)(R - j) - 0.5f, 0f, (float)j + 0.5f - (float)(S - R));
				float num2 = Mathf.Max((float)(R - i) - 0.5f, 0f, (float)i + 0.5f - (float)(S - R));
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				byte a = (byte)(Mathf.Clamp01((float)R + 1f - num3) * 255f);
				array[i * S + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, a);
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		return Sprite.Create(texture2D, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
	}
}
