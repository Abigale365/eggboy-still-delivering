using UnityEngine;

public static class BakeryData
{
	public static BakeryItem[] Load()
	{
		TextAsset textAsset = Resources.Load<TextAsset>("Story/bakery");
		if (textAsset == null)
		{
			return new BakeryItem[0];
		}
		BakeryList bakeryList = JsonUtility.FromJson<BakeryList>(textAsset.text);
		if (bakeryList == null || bakeryList.items == null)
		{
			return new BakeryItem[0];
		}
		return bakeryList.items;
	}
}
