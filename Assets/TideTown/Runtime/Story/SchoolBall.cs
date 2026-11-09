// WP-13：红球被拿走、放进狗碗的两段位置切换（含弹跳滚入动画）。
using UnityEngine;

public sealed class SchoolBall : MonoBehaviour
{
    public Vector3 BowlPoint;
    bool inBowl;
    Vector3 initialPos;
    Quaternion initialRot;
    bool capturedInitial;

    public bool InBowl => inBowl;

    void CaptureInitial()
    {
        if (capturedInitial) return;
        capturedInitial = true;
        initialPos = transform.position;
        initialRot = transform.rotation;
    }

    public void Take(Transform player)
    {
        CaptureInitial();
        inBowl = false;
        transform.SetParent(player, false);
        transform.localPosition = new Vector3(0.26f, 0.86f, 0.20f);
        transform.localRotation = Quaternion.identity;
    }

    public void ReturnToSwing()
    {
        CaptureInitial();
        inBowl = false;
        transform.SetParent(null, true);
        transform.position = initialPos;
        transform.rotation = initialRot;
    }

    public void RollIntoBowl()
    {
        transform.SetParent(null, true);
        inBowl = true;
        StartCoroutine(Roll());
    }

    System.Collections.IEnumerator Roll()
    {
        var start = transform.position;
        var ground = new Vector3(start.x, BowlPoint.y, start.z);
        for (float elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
        {
            float t = elapsed / 0.6f;
            Vector3 basePos = t < 0.25f
                ? Vector3.Lerp(start, ground, t / 0.25f)
                : Vector3.Lerp(ground, BowlPoint, (t - 0.25f) / 0.75f);
            float bounce = t >= 0.25f
                ? Mathf.Abs(Mathf.Sin((t - 0.25f) / 0.75f * Mathf.PI * 2.0f)) * (1f - t) * 0.16f
                : 0f;
            transform.position = basePos + Vector3.up * bounce;
            transform.Rotate(Vector3.right, 540f * Time.deltaTime, Space.World);
            yield return null;
        }
        PlaceInBowl();
    }

    public void PlaceInBowl()
    {
        transform.SetParent(null, true);
        transform.position = BowlPoint;
        transform.localRotation = Quaternion.identity;
        inBowl = true;
    }
}
