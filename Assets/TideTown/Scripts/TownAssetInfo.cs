using UnityEngine;

// 挂在每个导入的城镇模型 prefab 上，记录来源与规模，便于赏金盘点和后续替换。
public class TownAssetInfo : MonoBehaviour
{
    public string assetId;
    public string displayName;
    public string category;
    public string sourceFile;
    public int triangleCount;
    public int rendererCount;
    public int materialCount;
    public int textureCount;
    public Vector3 size;
}
