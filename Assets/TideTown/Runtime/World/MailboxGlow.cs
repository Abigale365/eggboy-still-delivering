// WP-10 序章教学：山口邮筒上一团暖色的光，提示玩家“靠近发光的邮筒”。
using UnityEngine;

public sealed class MailboxGlow : MonoBehaviour
{
    public float Height = 1.6f;

    Light lamp;
    Transform orb;
    float t;

    void Start()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var orbGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orbGo.name = "MailboxGlowOrb";
            Destroy(orbGo.GetComponent<Collider>());
            orbGo.transform.SetParent(transform, false);
            orbGo.transform.localPosition = new Vector3(0f, Height + 0.25f, 0f);
            orbGo.transform.localScale = Vector3.one * 0.22f;
            var mat = new Material(shader) { name = "MailboxGlowOrb" };
            var warm = new Color(1f, 0.78f, 0.36f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", warm);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", warm);
            orbGo.GetComponent<Renderer>().sharedMaterial = mat;
            orb = orbGo.transform;
        }

        var lightGo = new GameObject("MailboxGlowLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, Height + 0.1f, 0f);
        lamp = lightGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.76f, 0.4f);
        lamp.range = 3.2f;
        lamp.intensity = 1.3f;
        lamp.shadows = LightShadows.None;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = 0.5f + 0.5f * Mathf.Sin(t * 2.1f);
        if (lamp != null) lamp.intensity = 1.0f + k * 0.7f;
        if (orb != null) orb.localScale = Vector3.one * (0.18f + k * 0.06f);
    }
}
