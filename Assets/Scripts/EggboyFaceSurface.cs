using System.Collections.Generic;
using UnityEngine;

// Integrates expressions into the existing renderer's lit shell/visor materials.
public static class EggboyFaceSurface
{
    static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x-o.x)*(b.y-o.y)-(a.y-o.y)*(b.x-o.x);
    static List<Vector2> Hull(List<Vector2> p)
    {
        p.Sort((a,b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        var h=new List<Vector2>();
        foreach(var v in p) { while(h.Count>1 && Cross(h[h.Count-2],h[h.Count-1],v)<=0) h.RemoveAt(h.Count-1); h.Add(v); }
        int n=h.Count; for(int i=p.Count-2;i>=0;i--) { var v=p[i]; while(h.Count>n && Cross(h[h.Count-2],h[h.Count-1],v)<=0) h.RemoveAt(h.Count-1); h.Add(v); }
        if(h.Count>1) h.RemoveAt(h.Count-1); return h;
    }
    public static bool Build(EggboySkin skin, Transform model, Texture face, out Transform result)
    {
        result=null;
        var atlas=Resources.Load<Texture2D>("Textures/img0");
        if(atlas==null || !atlas.isReadable) { Debug.LogError("Face atlas must be readable"); return false; }
        var verts=skin.verts; var uv=skin.mesh.uv; var p=new Vector3[verts.Length]; var dark=new bool[verts.Length]; var screenColor=new bool[verts.Length];
        for(int i=0;i<verts.Length;i++)
        {
            p[i]=model.InverseTransformPoint(skin.renderer.transform.TransformPoint(verts[i]));
            Color c=atlas.GetPixelBilinear(uv[i].x,uv[i].y); float lum=.299f*c.r+.587f*c.g+.114f*c.b, mx=Mathf.Max(c.r,c.g,c.b), mn=Mathf.Min(c.r,c.g,c.b);
            dark[i]=lum<60f/255f; screenColor[i]=dark[i]||(mx>0 && (mx-mn)/mx>.45f && c.r>150f/255f && c.b<130f/255f);
        }
        var xs=new List<float>(); var ys=new List<float>(); float frontZ=float.PositiveInfinity;
        foreach(int i in skin.visorVerts) { frontZ=Mathf.Min(frontZ,p[i].z); if(screenColor[i]) { xs.Add(p[i].x); ys.Add(p[i].y); } }
        if(xs.Count<30) { Debug.LogError("Insufficient face samples " + xs.Count); return false; }
        xs.Sort(); ys.Sort(); int lo=Mathf.FloorToInt((xs.Count-1)*.02f), hi=Mathf.FloorToInt((xs.Count-1)*.98f);
        float x0=xs[lo], x1=xs[hi], y0=ys[lo], y1=ys[hi]; float padx=(x1-x0)*.04f,pady=(y1-y0)*.04f;
        x0-=padx;x1+=padx;y0-=pady;y1+=pady;frontZ-=.02f;
        var projected=new Vector2[verts.Length]; var candidates=new List<Vector2>();
        for(int i=0;i<verts.Length;i++)
        {
            projected[i]=p[i].z>=frontZ ? new Vector2((p[i].x-x0)/(x1-x0),(p[i].y-y0)/(y1-y0)) : new Vector2(-10,-10);
        }
        const int W=256,H=200;
        var raster=new bool[W*H];
        foreach(int s in new[]{skin.shellSubmesh,skin.visorSubmesh})
        {
            if(s<0)continue; var tris=skin.mesh.GetTriangles(s);
            for(int j=0;j<tris.Length;j+=3)
            {
                int a=tris[j],b=tris[j+1],c=tris[j+2];
                if(!Within(projected[a]) || !Within(projected[b]) || !Within(projected[c]))continue;
                if(dark[a] && dark[b] && dark[c]) Raster(projected[a],projected[b],projected[c],raster,W,H);
            }
        }
        // Keep the connected screen region so dark marks on the shell cannot widen its contour.
        var visited=new bool[W*H];var stack=new Stack<int>();var largest=new List<int>();
        for(int i=0;i<raster.Length;i++)
        {
            if(!raster[i]||visited[i])continue;var region=new List<int>();stack.Push(i);visited[i]=true;
            while(stack.Count>0)
            {
                int v=stack.Pop();region.Add(v);int x=v%W,y=v/W;
                foreach(int q in new[]{x>0?v-1:-1,x<W-1?v+1:-1,y>0?v-W:-1,y<H-1?v+W:-1})
                    if(q>=0 && raster[q] && !visited[q]) { visited[q]=true;stack.Push(q); }
            }
            if(region.Count>largest.Count)largest=region;
        }
        foreach(int i in largest) { int x=i%W,y=i/W;candidates.Add(new Vector2(x/(float)W,y/(float)H));candidates.Add(new Vector2((x+1f)/W,(y+1f)/H)); }
        if(candidates.Count<3) { Debug.LogError("Face contour unavailable"); return false; }
        var hull=Hull(candidates); var mask=new Texture2D(W,H,TextureFormat.RGBA32,false,true); mask.wrapMode=TextureWrapMode.Clamp;
        var pixels=new Color32[W*H];
        for(int y=0;y<H;y++)for(int x=0;x<W;x++)
        {
            var v=new Vector2((x+.5f)/W,(y+.5f)/H); float distance=float.PositiveInfinity;
            for(int k=0;k<hull.Count;k++) { var a=hull[k];var b=hull[(k+1)%hull.Count]; float d=Cross(a,b,v)/(b-a).magnitude;distance=Mathf.Min(distance,d); }
            byte alpha=(byte)(255f*Mathf.Clamp01(distance*W*.6f+.5f)); pixels[y*W+x]=new Color32(alpha,alpha,alpha,255);
        }
        mask.SetPixels32(pixels);mask.Apply(false,false);
        var mesh=Object.Instantiate(skin.mesh);mesh.name="EGGBOY native face UV";mesh.uv2=projected;
        var shader=Resources.Load<Shader>("EggboyFaceSurface");if(shader==null) { Debug.LogError("Missing face surface shader");return false; }
        var materials=skin.renderer.materials;
        foreach(int submesh in new[]{skin.shellSubmesh,skin.visorSubmesh})
        {
            if(submesh<0||submesh>=materials.Length)continue;
            var mat=new Material(materials[submesh]);mat.name=materials[submesh].name+" native face";mat.shader=shader;
            mat.SetTexture("_FaceMap",face);mat.SetTexture("_FaceMask",mask);materials[submesh]=mat;
        }
        skin.renderer.sharedMesh=mesh;skin.renderer.sharedMaterials=materials;result=skin.renderer.transform;
        Debug.Log($"NATIVE_FACE originalRenderer=true originalSubmeshes={mesh.subMeshCount} screenPixels={largest.Count} hull={hull.Count} x={x0:F3}..{x1:F3} y={y0:F3}..{y1:F3}");return true;
    }
    static void Raster(Vector2 a,Vector2 b,Vector2 c,bool[] pixels,int w,int h)
    {
        a=Vector2.Scale(a,new Vector2(w,h));b=Vector2.Scale(b,new Vector2(w,h));c=Vector2.Scale(c,new Vector2(w,h));
        int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)),0,w-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)),0,w-1);
        int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y)),0,h-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y)),0,h-1);
        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
        {
            var p=new Vector2(x+.5f,y+.5f);float d0=Cross(a,b,p),d1=Cross(b,c,p),d2=Cross(c,a,p);
            if((d0>=0&&d1>=0&&d2>=0)||(d0<=0&&d1<=0&&d2<=0))pixels[y*w+x]=true;
        }
    }
    static bool Within(Vector2 uv) => uv.x>-.08f && uv.x<1.08f && uv.y>-.08f && uv.y<1.08f;
}
