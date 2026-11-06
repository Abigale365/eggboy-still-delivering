using UnityEditor;
using UnityEngine;

public static class WarmTownPalette
{
    public static void Apply()
    {
        Set("Road", new Color32(215,174,122,255)); Set("WarmStone", new Color32(215,174,122,255));
        Set("Plaza", new Color32(228,197,154,255));
        Set("Grass", new Color32(143,149,76,255)); Set("TerrainGrass", new Color32(138,147,78,255));
        Set("Sea", new Color32(47,127,148,255)); Set("Foliage", new Color32(100,129,54,255));
        Set("CoastalRock", new Color32(177,157,123,255)); Set("TreeBark",new Color32(128,84,55,255));
        Set("PierWood",new Color32(177,136,87,255)); AssetDatabase.SaveAssets();
    }
    static void Set(string name, Color value)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/TideTown/Art/Materials/"+name+".mat");
        if(mat==null)return;mat.SetColor("_BaseColor",value);EditorUtility.SetDirty(mat);
    }
}
