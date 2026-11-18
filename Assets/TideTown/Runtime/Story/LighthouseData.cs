using UnityEngine;

public static class LighthouseData
{
	public static LighthouseList Load()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("Story/lighthouse");
		if (textAsset == null)
		{
			Debug.LogError("找不到 Resources/Story/lighthouse.json");
			return new LighthouseList
			{
				items = new LighthouseItem[0]
			};
		}
		LighthouseList lighthouseList = JsonUtility.FromJson<LighthouseList>(textAsset.text);
		if (lighthouseList == null)
		{
			return new LighthouseList
			{
				items = new LighthouseItem[0]
			};
		}
		if (lighthouseList.items == null)
		{
			lighthouseList.items = new LighthouseItem[0];
		}
		return lighthouseList;
	}

	public static Vector3 Point(LighthousePoint p)
	{
		if (p == null)
		{
			return Vector3.zero;
		}
		return new Vector3(p.x, p.y, p.z);
	}
}
