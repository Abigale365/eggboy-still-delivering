// WP-14：山那边的新城，约 40 个手电光点逐个亮起，一闪一闪。
using System.Collections;
using UnityEngine;

public sealed class FlashlightReply : MonoBehaviour
{
    public Renderer[] dots = new Renderer[0];
    public float startDelay = 5f;
    public float stagger = 0.18f;

    public bool Done { get; private set; }
    public int LitCount { get; private set; }
    public int Total => dots != null ? dots.Length : 0;

    bool started;

    void Awake()
    {
        if (dots == null) return;
        foreach (var d in dots) if (d != null) d.enabled = false;
    }

    public void Begin()
    {
        if (started || Done) return;
        started = true;
        StartCoroutine(Run());
    }

    public void SetDoneInstant()
    {
        Done = true;
        LitCount = Total;
        if (dots == null) return;
        foreach (var d in dots) if (d != null) d.enabled = true;
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(startDelay);
        for (int i = 0; i < Total; i++)
        {
            if (dots[i] != null) dots[i].enabled = true;
            LitCount = i + 1;
            yield return new WaitForSeconds(stagger);
        }
        Done = true;
    }

    void Update()
    {
        if (LitCount <= 0 || dots == null) return;
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null || !dots[i].enabled) continue;
            float k = 0.82f + 0.18f * Mathf.Sin(Time.time * 2.4f + i * 1.7f);
            dots[i].transform.localScale = Vector3.one * (0.3f * k);
        }
    }
}
