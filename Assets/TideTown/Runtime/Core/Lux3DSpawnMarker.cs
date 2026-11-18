using System;
using System.Collections;
using UnityEngine;

public sealed class Lux3DSpawnMarker : MonoBehaviour
{
	public Lux3DSpawn spawn;

	public float size = 0.5f;

	private GameObject stamp;

	public void ShowStamp(bool on)
	{
		if (!on)
		{
			if (stamp != null)
			{
				UnityEngine.Object.Destroy(stamp);
			}
			stamp = null;
		}
		else if (!(stamp != null))
		{
			stamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
			stamp.name = "StillDeliveringStamp";
			UnityEngine.Object.Destroy(stamp.GetComponent<Collider>());
			stamp.transform.SetParent(base.transform, worldPositionStays: false);
			stamp.transform.localPosition = new Vector3(0f, size + 0.35f, 0f);
			stamp.transform.localScale = new Vector3(0.16f, 0.2f, 1f);
			Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				shader = Shader.Find("Unlit/Color");
			}
			Material material = new Material(shader);
			Color value = (material.color = new Color(0.93f, 0.48f, 0.2f, 1f));
			if (material.HasProperty("_BaseColor"))
			{
				material.SetColor("_BaseColor", value);
			}
			if (material.HasProperty("_Cull"))
			{
				material.SetFloat("_Cull", 0f);
			}
			stamp.GetComponent<Renderer>().sharedMaterial = material;
		}
	}

	private void Update()
	{
		if (!(stamp == null))
		{
			stamp.transform.localRotation = Quaternion.Euler(0f, Time.time * 140f, 0f);
			stamp.transform.localPosition = new Vector3(0f, size + 0.35f + Mathf.Sin(Time.time * 2.4f) * 0.04f, 0f);
		}
	}

	public IEnumerator SwapTo(GameObject next)
	{
		ShowStamp(on: false);
		Transform old = base.transform.Find("Model");
		next.transform.SetParent(base.transform, worldPositionStays: false);
		next.transform.localPosition = Vector3.zero;
		Vector3 target = next.transform.localScale;
		next.transform.localScale = Vector3.zero;
		Vector3 oldScale = ((old != null) ? old.localScale : Vector3.one);
		float t2 = 0f;
		while (t2 < 0.3f)
		{
			t2 += Time.deltaTime;
			float num = Mathf.Clamp01(t2 / 0.3f);
			if (old != null)
			{
				old.localScale = oldScale * (1f - num);
				old.Rotate(0f, 540f * Time.deltaTime, 0f, Space.World);
			}
			yield return null;
		}
		if (old != null)
		{
			old.name = "ModelOld";
			UnityEngine.Object.Destroy(old.gameObject);
		}
		ShadowAndChimeSystem.PlayProceduralChime(1046.5f, 0.22f);
		t2 = 0f;
		while (t2 < 0.3f)
		{
			t2 += Time.deltaTime;
			float num2 = Mathf.Clamp01(t2 / 0.3f);
			float num3 = 1f + 0.18f * Mathf.Sin(num2 * MathF.PI);
			next.transform.localScale = target * (num2 * num3);
			yield return null;
		}
		next.transform.localScale = target;
	}
}
