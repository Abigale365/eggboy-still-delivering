using UnityEngine;

public static class EndingData
{
	public static EndingList Load()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("Story/ending");
		if (textAsset == null)
		{
			Debug.LogError("找不到 Resources/Story/ending.json");
			return new EndingList();
		}
		return JsonUtility.FromJson<EndingList>(textAsset.text) ?? new EndingList();
	}

	public static Vector3 Point(EndingPoint p)
	{
		if (p == null)
		{
			return Vector3.zero;
		}
		return new Vector3(p.x, p.y, p.z);
	}
}
