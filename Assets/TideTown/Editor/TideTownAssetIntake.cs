using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class TideTownAssetIntake
{
    const string SrcRoot = "Assets/TideTown/SourceGLB";
    const string ArtRoot = "Assets/TideTown/Art";
    const string MeshRoot = ArtRoot + "/Meshes";
    const string DataRoot = "Assets/TideTown/Data";
    const int MergeThreshold = 12;

    [MenuItem("潮汐镇/资产/导入全部 GLB")]
    public static void BuildAll()
    {
        Build(false);
    }

    public static void BuildTest()
    {
        Build(true);
    }

    static void Build(bool testOnly)
    {
        EnsureDir(ArtRoot);
        EnsureDir(MeshRoot);
        EnsureDir(DataRoot);

        string[] files = Directory.GetFiles(SrcRoot, "*.glb", SearchOption.AllDirectories);
        var records = new List<AssetRecord>();
        int ok = 0, fail = 0;

        foreach (var file in files)
        {
            string rel = file.Replace('\\', '/');
            if (testOnly && !rel.Contains("/Test/")) continue;
            string id = Path.GetFileNameWithoutExtension(file);
            string category = CategoryOf(id);
            if (category == "Characters")
            {
                Debug.Log("SKIP character glb: " + id);
                continue;
            }

            string glbPath = rel.Replace(Application.dataPath + "/", "Assets/");
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (src == null)
            {
                Debug.LogError("LOAD FAIL " + glbPath);
                fail++;
                continue;
            }

            try
            {
                var inst = UnityEngine.Object.Instantiate(src);
                inst.name = id;

                int renderers = inst.GetComponentsInChildren<MeshRenderer>(true).Length;
                int triBefore = CountTriangles(inst);
                GameObject root;
                if (renderers > MergeThreshold)
                    root = MergeByMaterial(inst, id, out _);
                else
                    root = inst;

                if (root != inst) UnityEngine.Object.DestroyImmediate(inst);
                root.name = id;

                Bounds b = ComputeBounds(root);
                int tri = CountTriangles(root);
                int matCount = CountMaterials(root, out int texCount);
                int finalRenderers = root.GetComponentsInChildren<MeshRenderer>(true).Length;

                var info = root.AddComponent<TownAssetInfo>();
                info.assetId = id;
                info.displayName = id;
                info.category = category;
                info.sourceFile = glbPath;
                info.triangleCount = tri;
                info.rendererCount = finalRenderers;
                info.materialCount = matCount;
                info.textureCount = texCount;
                info.size = b.size;

                var obstacle = root.AddComponent<TownObstacle>();
                obstacle.kind = TownObstacle.Kind.Obstacle;
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;

                root.isStatic = true;

                string catDir = ArtRoot + "/" + category;
                EnsureDir(catDir);
                string prefabPath = catDir + "/" + Sanitize(id) + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);

                records.Add(new AssetRecord
                {
                    id = id, category = category, prefab = prefabPath,
                    triangles = tri, renderers = finalRenderers,
                    materials = matCount, textures = texCount,
                    sizeX = b.size.x, sizeY = b.size.y, sizeZ = b.size.z
                });
                ok++;
                Debug.Log($"INTAKE OK {id} cat={category} tris={triBefore}->{tri} r={renderers}->{finalRenderers} m={matCount} t={texCount} size={b.size}");
            }
            catch (Exception e)
            {
                Debug.LogError("INTAKE FAIL " + id + "\n" + e);
                fail++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        WriteRegistry(records, ok, fail);
        Debug.Log($"INTAKE DONE ok={ok} fail={fail} total={records.Count}");
    }

    static string CategoryOf(string id)
    {
        if (id.StartsWith("EGGBOY") || id.StartsWith("小黑")) return "Characters";
        if (id.StartsWith("A")) return "Props_Bakery";
        if (id.StartsWith("B")) return "Buildings";
        if (id.StartsWith("C")) return "Props_School";
        if (id.StartsWith("D")) return "Props_Lighthouse";
        if (id.StartsWith("E")) return "Props_Environment";
        if (id.StartsWith("S")) return "Street";
        return "Misc";
    }

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    static GameObject MergeByMaterial(GameObject src, string id, out int mergedCount)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var mats = mr.sharedMaterials;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                Material mat = (s < mats.Length && mats[s] != null) ? mats[s] : (mats.Length > 0 ? mats[0] : null);
                if (mat == null) continue;
                if (!groups.TryGetValue(mat, out var list))
                {
                    list = new List<CombineInstance>();
                    groups[mat] = list;
                }
                list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = m });
            }
        }

        var root = new GameObject(id + "_Merged");
        int n = 0;
        foreach (var kv in groups)
        {
            var combined = new Mesh { name = id + "_" + n, indexFormat = IndexFormat.UInt32 };
            try
            {
                combined.CombineMeshes(kv.Value.ToArray(), true, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("MERGE FAIL " + id + " mat=" + kv.Key.name + " " + e.Message);
                UnityEngine.Object.DestroyImmediate(combined);
                continue;
            }
            if (combined.vertexCount == 0)
            {
                Debug.LogWarning("MERGE EMPTY " + id + " mat=" + kv.Key.name);
                UnityEngine.Object.DestroyImmediate(combined);
                continue;
            }

            string meshPath = MeshRoot + "/" + Sanitize(id) + "_" + n + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(combined, meshPath);

            var child = new GameObject(kv.Key.name);
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = combined;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = kv.Key;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            n++;
        }
        mergedCount = n;
        return root;
    }

    static int CountTriangles(GameObject go)
    {
        int t = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            for (int s = 0; s < m.subMeshCount; s++)
                t += (int)(m.GetIndexCount(s) / 3);
        }
        return t;
    }

    static int CountMaterials(GameObject go, out int textureCount)
    {
        var mats = new HashSet<Material>();
        var textures = new HashSet<Texture>();
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                mats.Add(m);
                foreach (var pn in m.GetTexturePropertyNames())
                {
                    var tex = m.GetTexture(pn);
                    if (tex != null) textures.Add(tex);
                }
            }
        }
        textureCount = textures.Count;
        return mats.Count;
    }

    static Bounds ComputeBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        // 回到根空间：根在原点且无缩放
        return new Bounds(go.transform.InverseTransformPoint(b.center), b.size);
    }

    static void EnsureDir(string dir)
    {
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }

    [Serializable]
    public class AssetRecord
    {
        public string id;
        public string category;
        public string prefab;
        public int triangles;
        public int renderers;
        public int materials;
        public int textures;
        public float sizeX, sizeY, sizeZ;
    }

    [Serializable]
    public class Registry
    {
        public string generatedAt;
        public int ok;
        public int fail;
        public List<AssetRecord> items = new List<AssetRecord>();
    }

    static void WriteRegistry(List<AssetRecord> records, int ok, int fail)
    {
        var reg = new Registry
        {
            generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ok = ok, fail = fail, items = records
        };
        File.WriteAllText(DataRoot + "/town_assets.json", JsonUtility.ToJson(reg, true), new UTF8Encoding(false));
    }
}
