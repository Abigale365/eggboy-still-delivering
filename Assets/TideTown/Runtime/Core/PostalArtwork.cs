using System.Collections.Generic;
using UnityEngine;

internal static class PostalArtwork
{
	private const string Directory = "UI/Storybook/";

	private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

	public static Sprite Postcard => Load("postcard");

	public static Sprite StampHandle => Load("stamp_handle");

	public static string DeliveryForLetter(string letterId)
	{
		return letterId switch
		{
			"lin_oven" => "bakery", 
			"ying_dog" => "school", 
			"chen_lighthouse" => "lighthouse", 
			"zhou_eggboy" => "ending", 
			_ => null, 
		};
	}

	public static Sprite Envelope(string delivery)
	{
		if (delivery != null)
		{
			return Load("envelope_" + delivery);
		}
		return null;
	}

	public static Sprite Flap(string delivery)
	{
		if (delivery != null)
		{
			return Load("flap_" + delivery);
		}
		return null;
	}

	public static Sprite Stamp(string delivery)
	{
		if (delivery != null)
		{
			return Load("stamp_" + delivery);
		}
		return null;
	}

	public static Sprite Pocket(string delivery)
	{
		string key = "pocket_" + delivery;
		if (sprites.TryGetValue(key, out var value))
		{
			return value;
		}
		Sprite sprite = Envelope(delivery);
		if (sprite == null)
		{
			return null;
		}
		Rect rect = sprite.rect;
		rect.height = Mathf.RoundToInt(rect.height * 0.3f);
		value = Sprite.Create(sprite.texture, rect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
		sprites.Add(key, value);
		return value;
	}

	public static string Recipient(string delivery)
	{
		return delivery switch
		{
			"bakery" => "面包店的烤炉", 
			"school" => "小黑", 
			"lighthouse" => "灯塔", 
			"ending" => "EGGBOY", 
			_ => "", 
		};
	}

	private static Sprite Load(string name)
	{
		if (sprites.TryGetValue(name, out var value))
		{
			return value;
		}
		Texture2D texture2D = Resources.Load<Texture2D>("UI/Storybook/" + name);
		if (texture2D != null)
		{
			value = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
		}
		sprites.Add(name, value);
		return value;
	}
}
