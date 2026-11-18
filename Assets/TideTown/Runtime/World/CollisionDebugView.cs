using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class CollisionDebugView : MonoBehaviour
{
	public bool Visible;

	private IslandWorld world;

	private Mesh lineMesh;

	private Material lineMat;

	private bool dirty = true;

	private int lastCount = -1;

	private void Awake()
	{
		world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == "-collisiondebug")
			{
				Visible = true;
				dirty = true;
			}
		}
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.F3) && (Features.debugUi || Debug.isDebugBuild))
		{
			Visible = !Visible;
			dirty = true;
			Debug.Log("COLLISIONDEBUG " + Visible);
		}
		if (Visible && !(world == null))
		{
			if (world.Colliders.Count != lastCount)
			{
				dirty = true;
			}
			if (dirty)
			{
				Build();
				dirty = false;
			}
			if (lineMesh != null && lineMat != null)
			{
				Graphics.DrawMesh(lineMesh, Matrix4x4.identity, lineMat, 0);
			}
		}
	}

	private void Build()
	{
		lastCount = world.Colliders.Count;
		if (lineMat == null)
		{
			Shader shader = Shader.Find("Hidden/Internal-Colored");
			if (shader == null)
			{
				return;
			}
			lineMat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			lineMat.SetInt("_SrcBlend", 5);
			lineMat.SetInt("_DstBlend", 10);
			lineMat.SetInt("_Cull", 0);
			lineMat.SetInt("_ZWrite", 0);
		}
		List<Vector3> list = new List<Vector3>(lastCount * 24);
		List<Color32> list2 = new List<Color32>(lastCount * 24);
		foreach (EggboyCollider collider in world.Colliders)
		{
			float num = ((collider.minY > 0.1f) ? collider.minY : world.GroundAt(collider.x, collider.z, collider.top.HasValue ? (collider.top.Value + 0.5f) : 50f));
			if (num <= 0.2f)
			{
				num = 0.15f;
			}
			float y = (collider.top.HasValue ? collider.top.Value : (num + 5f));
			Color32 col = (collider.water ? new Color32(80, 150, byte.MaxValue, 220) : (collider.top.HasValue ? new Color32(80, 230, 130, 220) : new Color32(byte.MaxValue, 70, 50, 220)));
			if (collider.r.HasValue && !collider.hw.HasValue)
			{
				AddCylinder(list, list2, collider.x, collider.z, collider.r.Value, num, y, col, 8);
				continue;
			}
			float hw = collider.hw ?? collider.r ?? 0.5f;
			float hd = collider.hd ?? collider.r ?? 0.5f;
			AddBox(list, list2, collider.x, collider.z, hw, hd, num, y, col);
		}
		if (lineMesh == null)
		{
			lineMesh = new Mesh
			{
				name = "collision_debug",
				indexFormat = IndexFormat.UInt32
			};
		}
		lineMesh.Clear();
		lineMesh.indexFormat = IndexFormat.UInt32;
		lineMesh.SetVertices(list);
		lineMesh.SetColors(list2);
		lineMesh.SetIndices(EnumerableRange(list.Count), MeshTopology.Lines, 0);
		lineMesh.RecalculateBounds();
	}

	private static List<int> EnumerableRange(int n)
	{
		List<int> list = new List<int>(n);
		for (int i = 0; i < n; i++)
		{
			list.Add(i);
		}
		return list;
	}

	private static void AddCylinder(List<Vector3> v, List<Color32> cols, float cx, float cz, float r, float y0, float y1, Color32 col, int segs)
	{
		for (int i = 0; i < segs; i++)
		{
			float f = (float)i * MathF.PI * 2f / (float)segs;
			float f2 = (float)(i + 1) * MathF.PI * 2f / (float)segs;
			Vector3 a = new Vector3(cx + Mathf.Cos(f) * r, y0, cz + Mathf.Sin(f) * r);
			Vector3 b = new Vector3(cx + Mathf.Cos(f2) * r, y0, cz + Mathf.Sin(f2) * r);
			Line(v, cols, a, b, col);
			if (y1 > y0 + 0.01f)
			{
				Vector3 vector = new Vector3(a.x, y1, a.z);
				Vector3 b2 = new Vector3(b.x, y1, b.z);
				Line(v, cols, vector, b2, col);
				if (i % 2 == 0)
				{
					Line(v, cols, a, vector, col);
				}
			}
		}
	}

	private static void AddBox(List<Vector3> v, List<Color32> cols, float x, float z, float hw, float hd, float y0, float y1, Color32 col)
	{
		AddRect(v, cols, x, z, hw, hd, y0, col);
		if (y1 > y0 + 0.01f)
		{
			AddRect(v, cols, x, z, hw, hd, y1, col);
			Line(v, cols, new Vector3(x - hw, y0, z - hd), new Vector3(x - hw, y1, z - hd), col);
			Line(v, cols, new Vector3(x + hw, y0, z - hd), new Vector3(x + hw, y1, z - hd), col);
			Line(v, cols, new Vector3(x - hw, y0, z + hd), new Vector3(x - hw, y1, z + hd), col);
			Line(v, cols, new Vector3(x + hw, y0, z + hd), new Vector3(x + hw, y1, z + hd), col);
		}
	}

	private static void AddRect(List<Vector3> v, List<Color32> cols, float x, float z, float hw, float hd, float y, Color32 col)
	{
		Line(v, cols, new Vector3(x - hw, y, z - hd), new Vector3(x + hw, y, z - hd), col);
		Line(v, cols, new Vector3(x + hw, y, z - hd), new Vector3(x + hw, y, z + hd), col);
		Line(v, cols, new Vector3(x + hw, y, z + hd), new Vector3(x - hw, y, z + hd), col);
		Line(v, cols, new Vector3(x - hw, y, z + hd), new Vector3(x - hw, y, z - hd), col);
	}

	private static void Line(List<Vector3> v, List<Color32> cols, Vector3 a, Vector3 b, Color32 col)
	{
		v.Add(a);
		v.Add(b);
		cols.Add(col);
		cols.Add(col);
	}
}
