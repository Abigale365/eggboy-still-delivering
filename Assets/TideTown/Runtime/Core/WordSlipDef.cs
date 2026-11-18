using System;

[Serializable]
public class WordSlipDef
{
	public string id;

	public string letter;

	public string word;

	public string near;

	public string kind;

	public string entry;

	public string action;

	public string prompt;

	[NonSerialized]
	public bool combo;
}
