using UnityEngine;

public static class SchoolData
{
	public static SchoolList Load()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("Story/school");
		if (textAsset == null)
		{
			Debug.LogError("找不到 Resources/Story/school.json");
			return new SchoolList
			{
				items = new SchoolItem[0]
			};
		}
		SchoolList schoolList = JsonUtility.FromJson<SchoolList>(textAsset.text);
		if (schoolList == null)
		{
			return new SchoolList
			{
				items = new SchoolItem[0]
			};
		}
		if (schoolList.items == null)
		{
			schoolList.items = new SchoolItem[0];
		}
		return schoolList;
	}
}
