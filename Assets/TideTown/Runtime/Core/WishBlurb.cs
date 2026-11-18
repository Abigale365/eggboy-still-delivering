using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class WishBlurb
{
	[Serializable]
	private class Req
	{
		public string text;

		public string category;
	}

	[Serializable]
	private class Resp
	{
		public string blurb;
	}

	public static string CategoryName(string cat)
	{
		return cat switch
		{
			"keepsake" => "纪念品", 
			"fire" => "炉边的柴", 
			"toy" => "玩具", 
			"food" => "给小黑的点心", 
			_ => "心愿", 
		};
	}

	public static string Template(WishToy t)
	{
		string description = t.description;
		int num = Mathf.Abs(description.GetHashCode()) % 3;
		return t.category switch
		{
			"food" => (new string[3]
			{
				"小黑闻了三圈，尾巴先替它说了喜欢。「" + description + "」放在碗边，像阿萤以前放学顺路带回来的那样。",
				"「" + description + "」还带着一点心愿的温度。小黑没急着吃，先把它推到你脚边——它想和你分。",
				"有人记得它爱吃什么，这件事本身就很好吃。「" + description + "」，送给守着空学校的小黑。"
			})[num], 
			"toy" => (new string[3]
			{
				"「" + description + "」在草地上一滚，小黑就追了出去。空了很久的操场，又有了一点吵闹。",
				"玩具不用很新，只要有人陪着玩。「" + description + "」是你替阿萤留下的那一份陪伴。",
				"邮差包里多了一件不用投递的东西：「" + description + "」。它只负责让谁开心一下。"
			})[num], 
			"fire" => (new string[3]
			{
				"「" + description + "」劈开的纹路还新。林阿婆的烤炉认得这种干柴，一点就旺。",
				"炉膛暖了，面包房就还像个家。「" + description + "」是今晚的第一把火。",
				"柴火不说话，只是把屋子烘得暖暖的。「" + description + "」，送给还没冷透的面包房。"
			})[num], 
			_ => (new string[3]
			{
				"「" + description + "」会一直摆在潮汐镇，等搬走的人哪天回来，看见有人替他们记着这里。",
				"它不是谁的旧物，是你给这座空镇子的新回忆：「" + description + "」。",
				"海水涨上来以前，镇上又多了一件好看的东西。「" + description + "」，邮差 EGGBOY 留。"
			})[num], 
		};
	}

	public static void RequestFromRelay(WishToy t)
	{
		Lux3DService instance = Lux3DService.Instance;
		if (!(instance == null) && !string.IsNullOrEmpty(instance.RelayUrl) && !(t == null))
		{
			t.StartCoroutine(Fetch(t, instance.RelayUrl.TrimEnd('/') + "/wish-blurb"));
		}
	}

	private static IEnumerator Fetch(WishToy t, string url)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Req
		{
			text = t.description,
			category = t.category
		}));
		using UnityWebRequest req = new UnityWebRequest(url, "POST");
		req.uploadHandler = new UploadHandlerRaw(bytes);
		req.downloadHandler = new DownloadHandlerBuffer();
		req.SetRequestHeader("Content-Type", "application/json");
		req.timeout = 3;
		yield return req.SendWebRequest();
		if (req.result == UnityWebRequest.Result.Success && !(t == null))
		{
			Resp resp = JsonUtility.FromJson<Resp>(req.downloadHandler.text);
			if (resp != null && !string.IsNullOrWhiteSpace(resp.blurb) && resp.blurb.Length <= 120)
			{
				t.blurb = resp.blurb.Trim();
			}
			yield break;
		}
	}
}
