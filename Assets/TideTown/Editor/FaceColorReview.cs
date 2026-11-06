using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Offscreen Unity renders allow close inspection without relying on a hidden window's backbuffer.
public static class FaceColorReview
{
    public static void Capture()
    {
        string dir = Path.GetFullPath("../../..") + "/artifacts/tidetown-native-face-20261106";
        Directory.CreateDirectory(dir);
        CheckAnimation(dir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraGO = new GameObject("Main Camera"); cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>(); cam.nearClipPlane = .02f; cam.farClipPlane = 300f;
        EggboyEnv.Apply(new GameObject("Environment").transform);
        cam.GetComponent<EggboyPostFX>().Configure();
        var actor = new GameObject("Review EGGBOY"); var player = actor.AddComponent<EggboyPlayer>();
        var prefab = Resources.Load<GameObject>("Models/eggboy"); player.disableAnim = true; player.Setup(Object.Instantiate(prefab), null, 0);
        player.ModelObject.GetComponentInChildren<Animation>().Stop();
        player.Bones.RestoreBindPose();
        int originalRenderers=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Length;
        var originalVertices=player.Skin.mesh.vertices;var originalTriangles=player.Skin.mesh.triangles;
        var face = new EggboyFace(player);
        var nativeMesh=player.Skin.renderer.sharedMesh;
        if(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Length!=originalRenderers)throw new System.Exception("Face added a second skin renderer");
        var vertices=nativeMesh.vertices;var triangles=nativeMesh.triangles;
        if(vertices.Length!=originalVertices.Length||triangles.Length!=originalTriangles.Length)throw new System.Exception("Original geometry changed");
        for(int i=0;i<vertices.Length;i++)if(vertices[i]!=originalVertices[i])throw new System.Exception("Original face vertex moved");
        for(int i=0;i<triangles.Length;i++)if(triangles[i]!=originalTriangles[i])throw new System.Exception("Original face topology changed");
        File.WriteAllText(dir+"/native-face-checks.json","{\"originalRendererUsed\":true,\"extraSkinRenderers\":0,\"verticesAndTopologyPreserved\":true}");
        var mask=(Texture2D)player.Skin.renderer.sharedMaterials[player.Skin.shellSubmesh].GetTexture("_FaceMask");File.WriteAllBytes(dir+"/screen-mask.png",mask.EncodeToPNG());
        cam.transform.position = new Vector3(0, .62f, 1.65f); cam.transform.LookAt(new Vector3(0,.48f,0)); cam.fieldOfView = 34;
        var rt = RenderTexture.GetTemporary(480, 560, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        var sheet = new Texture2D(480 * 4, 560 * 4, TextureFormat.RGB24, false);
        for (int i = 0; i < EggboyExpressions.All.Length; i++)
        {
            face.Set(EggboyExpressions.All[i]); face.Update(0, cam); cam.Render();
            RenderTexture.active = rt; var shot = new Texture2D(480, 560, TextureFormat.RGB24, false); shot.ReadPixels(new Rect(0,0,480,560),0,0); shot.Apply();
            File.WriteAllBytes(dir + "/" + EggboyExpressions.All[i] + ".png", shot.EncodeToPNG());
            sheet.SetPixels((i%4)*480, (3-i/4)*560,480,560,shot.GetPixels()); Object.DestroyImmediate(shot);
        }
        sheet.Apply(); File.WriteAllBytes(dir + "/expressions.png",sheet.EncodeToPNG());
        face.Set(ExprId.happy);
        foreach (float angle in new[] {-70f,-40f,40f,70f})
        {
            cam.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,.62f,1.65f);
            cam.transform.LookAt(new Vector3(0,.48f,0));face.Update(0,cam);cam.Render();RenderTexture.active=rt;
            var shot=new Texture2D(480,560,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,480,560),0,0);shot.Apply();File.WriteAllBytes(dir+"/angle"+angle+".png",shot.EncodeToPNG());Object.DestroyImmediate(shot);
        }
        Debug.Log("FACE_REVIEW " + dir + " bounds=" + player.Skin.renderer.bounds);
        cam.targetTexture = null; RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
        CaptureTown(dir);
    }
    static void CheckAnimation(string dir)
    {
        var painter=new FacePainter();var open=painter.Texture.GetPixels32();
        for(int i=0;i<160;i++)painter.Update(1f/60f);
        var after=painter.Texture.GetPixels32();int changed=0;for(int i=0;i<open.Length;i++)if(!open[i].Equals(after[i]))changed++;
        if(changed!=0)throw new System.Exception("Blink did not return to open eyes: "+changed);
        painter.Set(ExprId.surprised);var pixels=painter.Texture.GetPixels32();var center=pixels[(FacePainter.H-1-FacePainter.H/2)*FacePainter.W+(int)(FacePainter.W*.3f)];
        if(center.r>100)throw new System.Exception("Hollow surprised eye was filled");
        File.WriteAllText(dir+"/face-checks.json","{\"blinkReturnsOpen\":true,\"ringHasDarkCenter\":true}");
    }
    static void CaptureTown(string dir)
    {
        EditorSceneManager.OpenScene("Assets/TideTown/Scenes/TideTown_Expanded_Stage1.unity",OpenSceneMode.Single);
        WarmTownPalette.Apply();var cam=Camera.main;EggboyEnv.Apply(new GameObject("Review lighting").transform);cam.GetComponent<EggboyPostFX>().Configure();
        var actor=new GameObject("Review EGGBOY");var player=actor.AddComponent<EggboyPlayer>();player.disableAnim=true;player.Setup(Object.Instantiate(Resources.Load<GameObject>("Models/eggboy")),null,0);player.ModelObject.GetComponentInChildren<Animation>().Stop();player.Bones.RestoreBindPose();actor.transform.position=new Vector3(-38,.06f,-4);
        var face=new EggboyFace(player);cam.transform.position=new Vector3(-31,3.7f,5);cam.transform.LookAt(new Vector3(-38,1.6f,-7));cam.fieldOfView=52;face.Update(0,cam);
        var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
        var shot=new Texture2D(1280,720,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,1280,720),0,0);shot.Apply();File.WriteAllBytes(dir+"/town-warm.png",shot.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);
    }
}
