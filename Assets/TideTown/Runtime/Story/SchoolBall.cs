// WP-13：红球被拿走、放进狗碗的两段位置切换。
using UnityEngine;

public sealed class SchoolBall : MonoBehaviour
{
    public Vector3 BowlPoint;
    bool inBowl;

    public bool InBowl => inBowl;

    public void Take(Transform player)
    {
        inBowl = false;
        transform.SetParent(player, false);
        transform.localPosition = new Vector3(0.26f, 0.86f, 0.20f);
        transform.localRotation = Quaternion.identity;
    }

    public void PlaceInBowl()
    {
        transform.SetParent(null, true);
        transform.position = BowlPoint + Vector3.up * 0.22f;
        transform.localRotation = Quaternion.identity;
        inBowl = true;
    }
}
