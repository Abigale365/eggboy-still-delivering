using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelCanvas
{
	public readonly int W;

	public readonly int H;

	public readonly Color32[] Px;

	private readonly Color32[] upload;

	public PixelCanvas(int w, int h)
	{
		W = w;
		H = h;
		Px = new Color32[w * h];
		upload = new Color32[w * h];
	}

	public void Clear(Color32 c)
	{
		for (int i = 0; i < Px.Length; i++)
		{
			Px[i] = c;
		}
	}

	private void Blend(int x, int y, Color32 c)
	{
		if (x >= 0 && y >= 0 && x < W && y < H && c.a != 0)
		{
			int num = y * W + x;
			if (c.a == byte.MaxValue)
			{
				Px[num] = c;
				return;
			}
			Color32 color = Px[num];
			float num2 = (float)(int)c.a / 255f;
			color.r = (byte)((float)(int)c.r * num2 + (float)(int)color.r * (1f - num2));
			color.g = (byte)((float)(int)c.g * num2 + (float)(int)color.g * (1f - num2));
			color.b = (byte)((float)(int)c.b * num2 + (float)(int)color.b * (1f - num2));
			float value = (float)(int)c.a / 255f + (float)(int)color.a / 255f * (1f - num2);
			color.a = (byte)(Mathf.Clamp01(value) * 255f);
			Px[num] = color;
		}
	}

	private static float DistSegment(float px, float py, float x0, float y0, float x1, float y1)
	{
		float num = x1 - x0;
		float num2 = y1 - y0;
		float num3 = num * num + num2 * num2;
		float num4 = ((num3 > 1E-09f) ? Mathf.Clamp01(((px - x0) * num + (py - y0) * num2) / num3) : 0f);
		float num5 = x0 + num * num4;
		float num6 = y0 + num2 * num4;
		return Mathf.Sqrt((px - num5) * (px - num5) + (py - num6) * (py - num6));
	}

	public void Line(float x0, float y0, float x1, float y1, float width, Color32 c, float glow = 0f)
	{
		if (glow > 0f)
		{
			Line(x0, y0, x1, y1, width + glow * 2f, new Color32(c.r, c.g, c.b, (byte)((float)(int)c.a * 0.18f)));
			Line(x0, y0, x1, y1, width + glow, new Color32(c.r, c.g, c.b, (byte)((float)(int)c.a * 0.3f)));
		}
		int num = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - width * 0.5f - 2f));
		int num2 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + width * 0.5f + 2f));
		int num3 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - width * 0.5f - 2f));
		int num4 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + width * 0.5f + 2f));
		float num5 = width * 0.5f;
		for (int i = num3; i <= num4; i++)
		{
			for (int j = num; j <= num2; j++)
			{
				float num6 = DistSegment((float)j + 0.5f, (float)i + 0.5f, x0, y0, x1, y1);
				float num7 = Mathf.Clamp01(num5 + 0.9f - num6);
				if (!(num7 <= 0f))
				{
					Blend(j, i, new Color32(c.r, c.g, c.b, (byte)((float)(int)c.a * num7)));
				}
			}
		}
	}

	public void Arc(float cx, float cy, float r, float a0, float a1, float width, Color32 c, float glow = 0f)
	{
		float num = a1 - a0;
		float num2 = MathF.PI * 2f;
		float num3 = width * 0.5f;
		float num4 = r + num3 + glow + 2f;
		int num5 = Mathf.Max(0, Mathf.FloorToInt(cx - num4));
		int num6 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + num4));
		int num7 = Mathf.Max(0, Mathf.FloorToInt(cy - num4));
		int num8 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + num4));
		Vector2 vector = new Vector2(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r);
		Vector2 vector2 = new Vector2(cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r);
		for (int i = num7; i <= num8; i++)
		{
			for (int j = num5; j <= num6; j++)
			{
				Vector2 vector3 = new Vector2((float)j + 0.5f, (float)i + 0.5f);
				Vector2 vector4 = vector3 - new Vector2(cx, cy);
				float num9 = Mathf.Repeat(Mathf.Atan2(vector4.y, vector4.x) - a0, num2);
				float num10 = ((num >= num2 - 0.001f || num9 <= num) ? Mathf.Abs(vector4.magnitude - r) : Mathf.Min((vector3 - vector).magnitude, (vector3 - vector2).magnitude));
				float num11 = Mathf.Clamp01(num3 + 0.9f - num10);
				if (glow > 0f && num10 > num3)
				{
					float num12 = (num10 - num3) / Mathf.Max(1f, glow * 0.5f);
					float num13 = 0.28f * Mathf.Exp((0f - num12) * num12);
					if (num13 > 0.003f)
					{
						Blend(j, i, new Color32(c.r, c.g, c.b, (byte)((float)(int)c.a * num13)));
					}
				}
				if (num11 > 0f)
				{
					Blend(j, i, new Color32(c.r, c.g, c.b, (byte)((float)(int)c.a * num11)));
				}
			}
		}
	}

	public void Quadratic(float x0, float y0, float cx, float cy, float x1, float y1, float width, Color32 c, float glow = 0f)
	{
		int num = 20;
		float x2 = x0;
		float y2 = y0;
		for (int i = 1; i <= num; i++)
		{
			float num2 = (float)i / (float)num;
			float num3 = 1f - num2;
			float num4 = num3 * num3 * x0 + 2f * num3 * num2 * cx + num2 * num2 * x1;
			float num5 = num3 * num3 * y0 + 2f * num3 * num2 * cy + num2 * num2 * y1;
			Line(x2, y2, num4, num5, width, c, glow);
			x2 = num4;
			y2 = num5;
		}
	}

	public void Cubic(float x0, float y0, float cx1, float cy1, float cx2, float cy2, float x1, float y1, float width, Color32 c, float glow = 0f)
	{
		int num = 24;
		float x2 = x0;
		float y2 = y0;
		for (int i = 1; i <= num; i++)
		{
			float num2 = (float)i / (float)num;
			float num3 = 1f - num2;
			float num4 = num3 * num3 * num3 * x0 + 3f * num3 * num3 * num2 * cx1 + 3f * num3 * num2 * num2 * cx2 + num2 * num2 * num2 * x1;
			float num5 = num3 * num3 * num3 * y0 + 3f * num3 * num3 * num2 * cy1 + 3f * num3 * num2 * num2 * cy2 + num2 * num2 * num2 * y1;
			Line(x2, y2, num4, num5, width, c, glow);
			x2 = num4;
			y2 = num5;
		}
	}

	public void Ellipse(float cx, float cy, float rx, float ry, Color32 c, float glow = 0f)
	{
		if (glow > 0f)
		{
			Ellipse(cx, cy, rx + glow, ry + glow, new Color32(c.r, c.g, c.b, (byte)((float)(int)c.a * 0.28f)));
		}
		int num = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 2f));
		int num2 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + rx + 2f));
		int num3 = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 2f));
		int num4 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + ry + 2f));
		for (int i = num3; i <= num4; i++)
		{
			for (int j = num; j <= num2; j++)
			{
				float num5 = ((float)j + 0.5f - cx) / rx;
				float num6 = ((float)i + 0.5f - cy) / ry;
				float num7 = Mathf.Sqrt(num5 * num5 + num6 * num6);
				if (!(num7 > 1f))
				{
					float b = Mathf.Clamp01((1f - num7) * Mathf.Min(rx, ry) + 0.8f);
					Blend(j, i, new Color32(c.r, c.g, c.b, (byte)((float)(int)c.a * Mathf.Min(1f, b))));
				}
			}
		}
	}

	public void Circle(float cx, float cy, float r, Color32 c, float glow = 0f)
	{
		Ellipse(cx, cy, r, r, c, glow);
	}

	public void Polygon(Vector2[] pts, Color32 c, float glow = 0f, float outline = 0f, Color32? lineColor = null)
	{
		if (pts == null || pts.Length < 3)
		{
			return;
		}
		if (glow > 0f || outline > 0f)
		{
			Color32 c2 = lineColor ?? c;
			for (int i = 0; i < pts.Length; i++)
			{
				Vector2 vector = pts[i];
				Vector2 vector2 = pts[(i + 1) % pts.Length];
				if (glow > 0f)
				{
					Line(vector.x, vector.y, vector2.x, vector2.y, outline + glow, new Color32(c2.r, c2.g, c2.b, (byte)((float)(int)c2.a * 0.25f)));
				}
				if (outline > 0f)
				{
					Line(vector.x, vector.y, vector2.x, vector2.y, outline, c2);
				}
			}
		}
		float num = float.PositiveInfinity;
		float num2 = float.NegativeInfinity;
		for (int j = 0; j < pts.Length; j++)
		{
			Vector2 vector3 = pts[j];
			num = Mathf.Min(num, vector3.y);
			num2 = Mathf.Max(num2, vector3.y);
		}
		int num3 = Mathf.Max(0, Mathf.FloorToInt(num));
		int num4 = Mathf.Min(H - 1, Mathf.CeilToInt(num2));
		List<float> list = new List<float>();
		for (int k = num3; k <= num4; k++)
		{
			list.Clear();
			float num5 = (float)k + 0.5f;
			for (int l = 0; l < pts.Length; l++)
			{
				Vector2 vector4 = pts[l];
				Vector2 vector5 = pts[(l + 1) % pts.Length];
				if ((vector4.y <= num5 && vector5.y > num5) || (vector5.y <= num5 && vector4.y > num5))
				{
					list.Add(vector4.x + (num5 - vector4.y) / (vector5.y - vector4.y) * (vector5.x - vector4.x));
				}
			}
			list.Sort();
			for (int m = 0; m + 1 < list.Count; m += 2)
			{
				int num6 = Mathf.Max(0, Mathf.FloorToInt(list[m] + 0.5f));
				int num7 = Mathf.Min(W - 1, Mathf.CeilToInt(list[m + 1] - 0.5f));
				for (int n = num6; n <= num7; n++)
				{
					Blend(n, k, c);
				}
			}
		}
	}

	public void RoundedRect(float x0, float y0, float x1, float y1, float radius, Color32 top, Color32 bottom)
	{
		int num = Mathf.Max(0, Mathf.FloorToInt(x0));
		int num2 = Mathf.Min(W - 1, Mathf.CeilToInt(x1));
		int num3 = Mathf.Max(0, Mathf.FloorToInt(y0));
		int num4 = Mathf.Min(H - 1, Mathf.CeilToInt(y1));
		float num5 = Mathf.Max(1f, y1 - y0);
		for (int i = num3; i <= num4; i++)
		{
			float t = ((float)i - y0) / num5;
			byte r = (byte)Mathf.Lerp((int)top.r, (int)bottom.r, t);
			byte g = (byte)Mathf.Lerp((int)top.g, (int)bottom.g, t);
			byte b = (byte)Mathf.Lerp((int)top.b, (int)bottom.b, t);
			for (int j = num; j <= num2; j++)
			{
				float num6 = Mathf.Max(x0 + radius - (float)j - 0.5f, 0f, (float)j + 0.5f - (x1 - radius));
				float num7 = Mathf.Max(y0 + radius - (float)i - 0.5f, 0f, (float)i + 0.5f - (y1 - radius));
				float num8 = Mathf.Sqrt(num6 * num6 + num7 * num7);
				float num9 = Mathf.Clamp01(radius + 0.85f - num8);
				if (!(num9 <= 0f))
				{
					Blend(j, i, new Color32(r, g, b, (byte)Mathf.RoundToInt(255f * num9)));
				}
			}
		}
	}

	public void Polyline(Vector2[] pts, float width, Color32 c, float glow = 0f)
	{
		for (int i = 0; i + 1 < pts.Length; i++)
		{
			Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, width, c, glow);
		}
	}

	public void Upload(Texture2D tex)
	{
		for (int i = 0; i < H; i++)
		{
			int num = i * W;
			int num2 = (H - 1 - i) * W;
			for (int j = 0; j < W; j++)
			{
				upload[num2 + j] = Px[num + j];
			}
		}
		tex.SetPixels32(upload);
		tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
	}
}
