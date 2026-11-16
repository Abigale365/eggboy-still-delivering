// School seafront and harbor connection. Apply to the existing scene, preserving buildings and gameplay.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandBuilder
{
    const string CoastalAssets = "Assets/TideTown/Art/Meshes/CoastalPromenade";
    // 2026-11-16 下午：起点 3.024 和运行时压平后的旧石板台地（3.024）完全同高，交界处闪烁；抬 1.2 cm 盖在上面
    static readonly Vector3[] PromenadeKnots = {
        new Vector3(5.8f,3.036f,-10.1f),new Vector3(9.8f,3.34f,-9.58f),
        new Vector3(14.55f,3.34f,-8.97f),new Vector3(19.15f,3.34f,-8.24f),
        new Vector3(22.67f,3.34f,-6.76f),new Vector3(25.08f,3.34f,-4.32f),new Vector3(26.25f,3.34f,-1.646f)
    };
    static readonly Vector3[] HarborKnots = {
        new Vector3(9.45f,3.34f,-9.63f),new Vector3(9.55f,3.22f,-11.35f),
        new Vector3(10.15f,2.80f,-12.48f),new Vector3(11.15f,1.76f,-13.62f),
        new Vector3(12.05f,1.03f,-14.4f),new Vector3(13.9f,1.00f,-15.45f),
        new Vector3(15.7f,1.03f,-16.15f),new Vector3(16.25f,1.40f,-16.3f),new Vector3(16.9f,1.40f,-16.45f)
    };
    [Serializable] class CoastalRoute { public string id; public Vector3[] points; }
    static readonly List<Vector3> railPosts = new List<Vector3>();
    [Serializable] class CoastalRoutes { public List<CoastalRoute> routes = new List<CoastalRoute>(); }

    public static void BuildCoastalCandidate()
    {
        var report=BuildPipeline.BuildPlayer(new[]{ScenePath},"Build/CoastalCandidate/TideTown.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
        Debug.Log("COASTAL_BUILD "+report.summary.result+" errors="+report.summary.totalErrors);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Coastal candidate build failed");
    }

    public static void RefineCoastalPromenade()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        var root=GameObject.Find("Generated").transform;
        ApplyCoastalPromenade(layout,root);
        var previous=root.Find("ContourCollision");
        if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        Physics.SyncTransforms();BuildContourCollision(layout,root);
        File.WriteAllText(FootprintPath,JsonUtility.ToJson(new FootprintList{buildings=footprints,circles=contourCircles},true));
        AssetDatabase.ImportAsset(FootprintPath);
        var safe=UnityEngine.Object.FindObjectOfType<SafePoints>();
        if(safe!=null)for(int i=0;i<safe.points.Count;i++){var p=safe.points[i];safe.points[i]=new Vector3(p.x,GroundY(p.x,p.z),p.z);}
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save coastal scene");
        AssetDatabase.SaveAssets();Debug.Log("COASTAL_PROMENADE_SAVED");
    }

    static void ApplyCoastalPromenade(Layout layout,Transform parent)
    {
        Directory.CreateDirectory(CoastalAssets);
        var old=parent.Find("CoastalPromenade");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var root=new GameObject("CoastalPromenade").transform;root.SetParent(parent,false);
        railPosts.Clear();
        var walk=SmoothCoastal(PromenadeKnots);var descent=SmoothCoastal(HarborKnots, smoothHeight:false);
        // 2026-11-16 下午：三条校门坡道都改成"步道一头在前"：起点压在步道底下，出了步道内沿再一路斜到草坪，
        // 草坪那头压到 2.995（比运行时草坪最低的 3.012 还低），再往草坪里伸 0.25 m 埋进去，不再像一张纸片搭在草上
        // 西门原来斜着插进步道，露出步道的只有 0.3 m（等于一级 33 cm 的陡坎）；改成和校门平行、正对北边，露出 1.8 m 缓坡（离校门坡道 0.25 m）。
        // 东门起点挪到步道内沿附近，坡道往草坪多伸到露出 1.7 m。
        var schoolGate=new List<Vector3>{new Vector3(13.7f,3.34f,-8.4f),new Vector3(13.7f,2.995f,-5.7f),new Vector3(13.7f,2.985f,-5.45f)};
        var schoolWestGate=new List<Vector3>{new Vector3(11.2f,3.34f,-9.0f),new Vector3(11.2f,2.995f,-5.9f),new Vector3(11.2f,2.985f,-5.65f)};
        var schoolEastGate=new List<Vector3>{new Vector3(22.6f,3.34f,-6.4f),new Vector3(20.21f,2.995f,-4.47f),new Vector3(20.02f,2.985f,-4.31f)};
        bool AtSchoolGate(Vector3 p,float width)=>CoastalDistance(p,schoolGate)<width
            ||CoastalDistance(p,schoolWestGate)<width||CoastalDistance(p,schoolEastGate)<width;
        // 护栏 / 矮墙的豁口按每条坡道自己的半宽 + 余量开，正好对着坡道（原来统一 1.45 / 1.25，宽窄坡道都一样，一边撞柱一边漏墙）
        bool GateGap(Vector3 p,float extra)=>CoastalDistance(p,schoolGate)<1.15f+extra
            ||CoastalDistance(p,schoolWestGate)<1.075f+extra||CoastalDistance(p,schoolEastGate)<1.15f+extra;
        var world=UnityEngine.Object.FindObjectOfType<IslandWorld>();
        world.walkPolys.RemoveAll(p=>p.id.StartsWith("coastal_"));
        var areas=layout.walk.Where(p=>!p.id.StartsWith("coastal_")).ToList();
        var paving=CoastalMaterial("PromenadePaving",false);
        var masonry=CoastalMaterial("SeawallMasonry",true);
        var cap=FlatMat("CoastalCapstone",new Color(.77f,.66f,.50f),.13f);
        var timber=FlatMat("CoastalRailTimber",new Color(.43f,.29f,.14f),.12f);
        // Remove obsolete local rims and all old boundary fences in the edited area. Ground below is preserved.
        int trimmed=0;
        foreach(var mf in parent.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if(mf.transform.IsChildOf(root)||mf.sharedMesh==null)continue;
            bool fence=HasPlacementAncestor(mf.transform,"Fences");
            bool grass=HasPlacementAncestor(mf.transform,"GrassField");
            bool rim=mf.name=="PromenadeCurbs";
            bool cliff=mf.name=="Cliff_main"||mf.name=="Cliff_pad_stone_3_0_0"||mf.name=="Cliff_pad_grass_3_0_0";
            if(!(fence||grass||rim||cliff))continue;
            string key=mf.name;for(var t=mf.transform;t!=parent&&t!=null;t=t.parent)key+="_"+t.GetSiblingIndex();
            if(TrimCoastalMesh(mf, p=> grass ? (CoastalDistance(p,walk)<1.86f||CoastalDistance(p,descent)<1.7f||AtSchoolGate(p,1.3f)||SeawardLedge(p,walk)) : CoastalRegion(p),"trim_"+key))trimmed++;
        }
        var oldRoad=parent.Find("Road_to_shed");if(oldRoad!=null)oldRoad.gameObject.SetActive(false);
        // Build the high promenade and its dry apron, then the descent to the still floodable harbor.
        CoastalDeck("upper",walk,3.35f,root,paving,areas,world);
        // 2026-11-16 下午（图 5 / 图 1）：下港湾坡道和三条校门坡道的起点都在步道正下方，原来按自己的高度铺，
        // 步道本身是斜的（主街 3.02 → 步道 3.34），坡道起点那一截从砖面里斜着翘出来，或者和步道同高闪烁。
        // 现在逐个顶点量步道面：压在步道底下的顶点一律低 8 mm；出了步道边以后从步道边的高度接着往下走（不留台阶）。
        Physics.SyncTransforms();
        var upperCol=root.Find("upper").GetComponent<MeshCollider>();
        float UpperY(Vector3 p)=>upperCol.Raycast(new Ray(new Vector3(p.x,60f,p.z),Vector3.down),out var h,120f)?h.point.y:float.NaN;
        CoastalDeck("descent",descent,2.8f,root,paving,areas,world,side=>FitBelowDeck(side,UpperY,1.2f,false));
        foreach(var (gid,gline,gw) in new[]{("school_gate",schoolGate,2.3f),("school_west_gate",schoolWestGate,2.15f),("school_east_gate",schoolEastGate,2.3f)})
        {
            var edges=new List<List<Vector3>>();
            // 直线加密（不用 Catmull-Rom：最后一段很短时样条会冲过头再折回来，坡道两条边在草坪那头互换，坡面拧成一道 24 cm 的坎）
            CoastalDeck(gid,LinearResample(gline,.16f),gw,root,paving,areas,world,side=>{var s=FitBelowDeck(side,UpperY,0f,true);edges.Add(s);return s;});
            // 坡道两侧砌实心石面一直到草坪下面，侧面不再悬空漏黑缝；压在步道底下那段不砌
            var wv=new List<Vector3>();var wu=new List<Vector2>();var wt=new List<int>();
            foreach(var e in edges)for(int i=1;i<e.Count;i++){var a=e[i-1];var b=e[i];if(!float.IsNaN(UpperY(a))&&!float.IsNaN(UpperY(b)))continue;
                AddTwoSidedQuad(wv,wu,wt,a,b,new Vector3(b.x,2.90f,b.z),new Vector3(a.x,2.90f,a.z),new Vector2(0,a.y),new Vector2(Vector3.Distance(a,b)*.5f,b.y),new Vector2(Vector3.Distance(a,b)*.5f,2.9f),new Vector2(0,2.9f));}
            CoastalPart(gid+"_skirt",FinishMesh(gid+"_skirt",wv,wu,wt),root,masonry);
        }
        // 2026-11-15：步道（3.34）底下还压着旧台地（3.02）。小黑、射线、截图机位都会打到这层"暗地板"，
        // 把严格落在步道正下方、比步道低 0.12m 以上的旧地面三角形裁掉；边上留 0.18m 压在步道底下，不留缝。
        Physics.SyncTransforms();
        TrimGroundUnderDecks(parent,root,new[]{("upper",walk,3.35f),("school_gate",schoolGate,2.3f),("school_west_gate",schoolWestGate,2.15f),("school_east_gate",schoolEastGate,2.3f),("descent",descent,2.8f)});
        // 2026-11-16：下港湾的坡道（descent）是往下走的，旧台地（3.0）在坡道中段反而高过路面，从砖面里斜穿出一块绿三角。
        // 上面那道"台地比路面低 0.12 m 才裁"的规则管不到它：坡道投影范围内的台地三角形不论高低一律裁掉（细分到 0.12 m）。
        {
            var dc=root.Find("descent").GetComponent<MeshCollider>();
            bool OnDescent(Vector3 p)=>dc.Raycast(new Ray(new Vector3(p.x,60f,p.z),Vector3.down),out _,120f);
            TrimTierTriangles(parent,UnderDeckTiers,(c,reach)=>CoastalDistance(c,descent)<1.4f+reach+.05f,OnDescent,"descenttrim",.12f);
        }
        // 港湾那一段，旧台地（3.0）还伸出海堤外 0.6–0.8 m，下面的崖壁早被裁掉了，从港湾看是一条悬空的草檐。一起裁掉。
        // 2026-11-16 下午：往西延到下港湾坡道东侧的转角（x 9.9 起），那里也露一道草檐
        TrimTierTriangles(parent,UnderDeckTiers,(c,reach)=>c.x>9.6f&&c.x<29.6f&&c.z<.5f&&CoastalDistance(c,walk)<1.675f+reach+3f,p=>SeawardLedge(p,walk),"ledge");
        // Main street joins at grade; the first lighthouse ramp section meets the raised promenade.
        var light=FindRoad(layout,"to_lighthouse");light.y[0]=3.34f;
        var lightRoad=RequireObject("Road_to_lighthouse");var lightSections=ExistingSlopeDeck(light);
        SetFinishedDeck(lightRoad,lightSections,paving);SetGround(lightRoad,lightSections);SetRoadbed(lightRoad,lightSections,masonry);
        lightRoad.GetComponent<MeshRenderer>().sharedMaterials=new[]{paving};
        // 2026-11-15：灯塔坡道两侧砌实心石墙到海床（-0.2），坡顶再往前插进灯塔台 0.45 m，封住坡道和台地、海岸之间的透光缝；
        // 两边续上矮石墩木栏，灯塔台上（入口）和剧情点 30 附近不立
        BuildRampSideWalls(lightRoad,lightSections,-.2f,masonry,0f,.45f);
        var lightTier=Array.Find(layout.tiers,a=>a.id=="lighthouse");var lightPoly=lightTier.x.Select((x,i)=>new Vector2(x,lightTier.z[i])).ToList();
        var p30=Array.Find(layout.points,p=>p.id=="30");
        foreach(int sign in new[]{-1,1})
        {
            var edge=lightSections.Select(s=>(sign<0?s.left:s.right)+(s.right-s.left).normalized*(.02f*sign)).ToList();
            // 2026-11-16：原来离灯塔台 0.35 m 就停，和台上的旧木栏之间留一道敞口。现在一直立到台地边线（末端补端柱），
            // 台上那圈由 ExpLighthousePlatformRail 换成同款石柱木栏接上
            CoastalRail("lighthouse_rail_"+sign,edge,root,cap,timber,p=>CoastalDistance(p,walk)<1.7f||InPolyXZ(lightPoly,new Vector2(p.x,p.z))
                ||(p30!=null&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(p30.x,p30.z))<1.6f));
        }
        var border=new List<Vector3>();var inner=new List<Vector3>();
        for(int i=0;i<walk.Count;i++){var side=CoastalSide(walk,i);border.Add(walk[i]+side*1.675f);inner.Add(walk[i]-side*1.675f);}
        // Right side of an eastbound curve is the seaward edge.
        // 2026-11-16 下午（图 1）：转角处海堤和下港湾坡道的侧墙各自往回缩，中间露出一道旧台地草边。两道墙都伸到对方里面一点
        CoastalWall("upper_seawall",border,root,masonry,p=>CoastalDistance(p,descent)<1.36f);
        // 护栏豁口只让开坡道本身（坡道半宽 1.4 + 5 cm），让海堤护栏的端柱和坡道护栏的起始柱都落在转角上、合成一根角柱；
        // 西边原来 x 7.8 到坡道西沿只剩 0.3 m 的一小截护栏（两根柱子挤在一起），整段不立
        CoastalRail("upper_seaward",border,root,cap,timber,p=>CoastalDistance(p,descent)<1.45f||p.x<8.7f);
        CoastalRail("school_border",inner,root,cap,timber,p=>p.x<10.5f||GateGap(p,.17f));
        CoastalWall("school_plinth",inner,root,masonry,p=>GateGap(p,.13f),2.98f);
        // 校门坡道两侧的石面改由上面的 *_skirt 跟着坡面砌，这里不再单独砌 gate_plinth
        var descentCol=root.Find("descent").GetComponent<MeshCollider>();
        float DescentY(Vector3 p)=>descentCol.Raycast(new Ray(new Vector3(p.x,60f,p.z),Vector3.down),out var h,120f)?h.point.y:float.NaN;
        for(int sign=-1;sign<=1;sign+=2)
        {
            var edge=new List<Vector3>();
            // 护栏 / 侧墙的高度按坡道真正的面取（坡头已经按步道改过高度，不再照中线原高度）
            for(int i=0;i<descent.Count;i++){var sd=CoastalSide(descent,i);var q=descent[i]+sd*(1.4f*sign);float y=DescentY(descent[i]+sd*(1.36f*sign));if(!float.IsNaN(y))q.y=y;edge.Add(q);}
            CoastalWall("descent_wall_"+sign,edge,root,masonry,p=>CoastalDistance(p,walk)<1.60f||p.x>16.2f);
            // 2026-11-15：会被潮水淹的那段（路面 < 1.5m）不立石柱木栏，只留一道 12cm 的低水槛收边，涨潮时不会有一排柱子泡在海里
            CoastalRail("descent_rail_"+sign,edge,root,cap,timber,p=>CoastalDistance(p,walk)<1.72f||p.x>16.25f,p=>p.y<1.5f);
        }
        BuildCoastalHarbor(layout,parent,root,paving,masonry);
        // Soft landscape can border the road, but tree trunks and flower beds must not stand in the walking lane.
        int plants=0;
        foreach(var group in parent.GetComponentsInChildren<Transform>().Where(t=>t.name=="Trees"||t.name=="Bushes").ToArray())
        foreach(Transform item in group){if(!item.gameObject.activeSelf)continue;var rs=item.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);var p=b.center;p.y=b.min.y;
            if(CoastalDistance(p,walk)<2.0f||CoastalDistance(p,descent)<1.7f||SeawardLedge(p+Vector3.up*.2f,walk)){item.gameObject.SetActive(false);plants++;}}
        // 2026-11-15：旧石板路（3.02）沿边摆的路灯有几盏落在步道（3.34）里面：灯座陷进砖地 0.32 m，还挡在路中间。
        // 挪到海侧护栏内沿、立在步道面上。
        int lampsMoved=0;
        foreach(var lamp in parent.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Lamp").ToArray())
        {
            var p=lamp.position;int best=0;float d=float.MaxValue;
            for(int i=1;i<walk.Count;i++){float s=PlacementSegmentDistance(p,walk[i-1],walk[i]);if(s<d){d=s;best=i;}}
            // 2026-11-16：原来只挪"落在步道面里、比步道低"的灯；站在护栏线上（压着石栏底座）或紧贴护栏外的灯漏掉了。
            // 现在护栏线内外 0.6 m 以内、高度在步道附近的灯都收进来
            if(d>1.675f+.6f||p.y>walk[best].y+.3f||p.y<walk[best].y-.6f)continue;
            var side=CoastalSide(walk,best);var q=p-walk[best];q.y=0;float off=Vector3.Dot(q,side)>=0?1f:-1f;
            // 2026-11-16：原来离中线 1.355 m，灯座（半径 0.25）压进栏杆石墩（内沿 1.555）。改成灯杆中心离石墩内沿 0.40 m，
            // 底座整圈落在铺装上；靠学校一侧的灯也一样收在步道里，不落到 school_border 栏杆外的草坪上。
            // 2026-11-16 下午（图 3）：校门豁口旁边那盏灯底座贴死栏杆。离护栏再让 0.15 m（底座离石槛 ~0.26 m），
            // 并且顺着步道挪开，离任何校门豁口边至少 0.9 m
            var railPt=walk[best]+side*(off*1.675f);
            var along=(walk[Mathf.Min(best+1,walk.Count-1)]-walk[Mathf.Max(best-1,0)]);along.y=0;along.Normalize();
            for(int k=0;k<40&&GateGap(railPt,1.0f);k++){float dp=CoastalDistance(railPt+along*.1f,schoolGate)+CoastalDistance(railPt+along*.1f,schoolWestGate)+CoastalDistance(railPt+along*.1f,schoolEastGate);
                float dm=CoastalDistance(railPt-along*.1f,schoolGate)+CoastalDistance(railPt-along*.1f,schoolWestGate)+CoastalDistance(railPt-along*.1f,schoolEastGate);railPt+=(dp>=dm?along:-along)*.1f;}
            if(!PlaceLampInsideRail(lamp,railPt,-side*off,.12f+.55f))continue;   // 灯笼朝步道里挑，不悬到栏杆外
            lampsMoved++;
        }
        Debug.Log("COASTAL_LAMPS moved="+lampsMoved);
        layout.walk=areas.ToArray();Physics.SyncTransforms();
        AlignAndValidateCoastalCrates(parent,descent);
        var routes=new CoastalRoutes();routes.routes.Add(new CoastalRoute{id="upper",points=walk.ToArray()});routes.routes.Add(new CoastalRoute{id="descent",points=descent.ToArray()});routes.routes.Add(new CoastalRoute{id="school_gate",points=schoolGate.ToArray()});routes.routes.Add(new CoastalRoute{id="school_east_gate",points=schoolEastGate.ToArray()});
        Directory.CreateDirectory("docs/evidence/coastal-road-20261114");File.WriteAllText("docs/evidence/coastal-road-20261114/routes.json",JsonUtility.ToJson(routes,true));
        Debug.Log($"COASTAL_PROMENADE upper={walk.Count} descent={descent.Count} trimmed={trimmed} plants={plants}");
    }

    static void AlignAndValidateCoastalCrates(Transform parent,List<Vector3> descent)
    {
        var crates=parent.GetComponentsInChildren<FloatingCrate>(true).OrderBy(c=>int.Parse(c.name.Substring(c.name.LastIndexOf('_')+1))).ToArray();
        if(crates.Length!=10)throw new Exception("Coastal ferry expects 10 existing crates; found "+crates.Length);
        var low=descent.Where(p=>p.y<=1.035f).ToList();
        if(low.Count<2)throw new Exception("Coastal ferry has no flooded bed");
        var lengths=new float[low.Count];for(int i=1;i<low.Count;i++)lengths[i]=lengths[i-1]+Vector2.Distance(new Vector2(low[i-1].x,low[i-1].z),new Vector2(low[i].x,low[i].z));
        for(int i=0;i<crates.Length;i++)
        {
            float s=lengths[lengths.Length-1]*i/(crates.Length-1);int j=1;while(j<lengths.Length-1&&lengths[j]<s)j++;
            var p=Vector3.Lerp(low[j-1],low[j],Mathf.InverseLerp(lengths[j-1],lengths[j],s));
            // 忽略浮箱自身；被旧台地或新路盖住必须直接中止生成。
            float ground=CoastalBedY(p);
            if(ground<.2f||ground>=1.09f)throw new Exception($"Coastal ferry crate {i} bed={ground:F3} at {p}: must stay below 1.09m");
            crates[i].bedY=ground;crates[i].transform.position=new Vector3(p.x,ground+crates[i].size.y*.5f,p.z);
        }
        Physics.SyncTransforms();
        var first=crates[0].transform.position;var last=crates[crates.Length-1].transform.position;var along=last-first;along.y=0;along.Normalize();
        foreach(var bank in new[]{first-along*1.3f,last+along*1.3f})
            if(CoastalBedY(bank)<1.2f)throw new Exception("Coastal ferry dry bank lost at "+bank);
        Debug.Log("COASTAL_FERRY bed verified for 10 crates; L2 gap and winch still require runtime ferry audit");
    }
    static float CoastalBedY(Vector3 p)
    {
        float ground=-99f;
        foreach(var hit in Physics.RaycastAll(new Vector3(p.x,200f,p.z),Vector3.down,400f,1<<6,QueryTriggerInteraction.Ignore))
            if(hit.collider.GetComponentInParent<FloatingCrate>()==null)ground=Mathf.Max(ground,hit.point.y);
        return ground;
    }

    static readonly string[] UnderDeckTiers={"Tier_main","Tier_pad_stone_3_0_0","Tier_pad_grass_3_0_0","Tier_shed"};
    // 海堤外侧的旧台地：在港湾到灯塔坡道口这一段（x 11.8–28.9）、海堤线往海里 4 cm 以外、高于 2.5 m（不碰船棚、海岸低地）
    static bool SeawardLedge(Vector3 p,List<Vector3> walk)
    {
        if(p.x<9.9f||p.x>28.9f||p.z>.15f||p.y<2.5f)return false;
        int best=0;float d=float.MaxValue;
        for(int i=1;i<walk.Count;i++){float s=PlacementSegmentDistance(p,walk[i-1],walk[i]);if(s<d){d=s;best=i;}}
        var side=CoastalSide(walk,best);var q=p-walk[best];q.y=0;
        return Vector3.Dot(q,side)>1.675f+.04f&&d>1.675f+.04f;
    }
    static void TrimGroundUnderDecks(Transform parent,Transform root,(string id,List<Vector3> line,float width)[] decks)
    {
        var cols=new List<Collider>();
        foreach(var d in decks){var t=root.Find(d.id);var c=t!=null?t.GetComponent<MeshCollider>():null;if(c!=null)cols.Add(c);}
        if(cols.Count==0)return;
        float DeckY(Vector3 p){float best=float.NegativeInfinity;var ray=new Ray(new Vector3(p.x,60f,p.z),Vector3.down);foreach(var c in cols)if(c.Raycast(ray,out var hit,120f))best=Mathf.Max(best,hit.point.y);return best;}
        const float Inset=.18f,Drop=.12f;
        bool Covered(Vector3 p){float y=p.y;foreach(var o in new[]{Vector3.zero,new Vector3(Inset,0,0),new Vector3(-Inset,0,0),new Vector3(0,0,Inset),new Vector3(0,0,-Inset)})if(DeckY(p+o)<y+Drop)return false;return true;}
        bool Near(Vector3 cen,float reach){foreach(var d in decks)if(CoastalDistance(cen,d.line)<d.width*.5f+reach+.05f)return true;return false;}
        TrimTierTriangles(parent,UnderDeckTiers,Near,Covered,"undertrim");
    }
    // 把台地顶面里"整块都满足 removable"的三角形去掉；靠近边界的大三角形先对半细分到 0.3 m 再判断。同时改网格和碰撞。
    static void TrimTierTriangles(Transform parent,string[] names,Func<Vector3,float,bool> near,Func<Vector3,bool> removable,string tag,float MinEdge=.3f)
    {
        foreach(string name in names)
        {
            var tf=parent.Find(name);if(tf==null)continue;var mf=tf.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)continue;
            var src=mf.sharedMesh;var sv=src.vertices;var su=src.uv;bool hasUv=su!=null&&su.Length==sv.Length;
            var pos=new List<Vector3>();var uvs=new List<Vector2>();
            for(int i=0;i<sv.Length;i++){pos.Add(tf.TransformPoint(sv[i]));uvs.Add(hasUv?su[i]:Vector2.zero);}
            var mid=new Dictionary<long,int>();
            int Mid(int a,int b){long key=a<b?((long)a<<32)|(uint)b:((long)b<<32)|(uint)a;if(mid.TryGetValue(key,out int m))return m;pos.Add((pos[a]+pos[b])*.5f);uvs.Add((uvs[a]+uvs[b])*.5f);mid[key]=pos.Count-1;return pos.Count-1;}
            var kept=new List<int>();int dropped=0,split=0;
            var stack=new Stack<(int a,int b,int c)>();var idx=src.GetTriangles(0);
            for(int i=0;i<idx.Length;i+=3)stack.Push((idx[i],idx[i+1],idx[i+2]));
            while(stack.Count>0)
            {
                var (a,b,c)=stack.Pop();var pa=pos[a];var pb=pos[b];var pc=pos[c];var cen=(pa+pb+pc)/3f;
                float reach=Mathf.Max(Vector3.Distance(cen,pa),Mathf.Max(Vector3.Distance(cen,pb),Vector3.Distance(cen,pc)));
                if(!near(cen,reach)){kept.Add(a);kept.Add(b);kept.Add(c);continue;}
                if(removable(pa)&&removable(pb)&&removable(pc)&&removable(cen)&&removable((pa+pb)*.5f)&&removable((pb+pc)*.5f)&&removable((pc+pa)*.5f)){dropped++;continue;}
                float longest=Mathf.Max(Vector3.Distance(pa,pb),Mathf.Max(Vector3.Distance(pb,pc),Vector3.Distance(pc,pa)));
                if(longest<=MinEdge){kept.Add(a);kept.Add(b);kept.Add(c);continue;}
                int ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);split++;
                stack.Push((a,ab,ca));stack.Push((ab,b,bc));stack.Push((ca,bc,c));stack.Push((ab,bc,ca));
            }
            if(dropped==0)continue;
            var mesh=new Mesh{name=src.name+" trimmed",indexFormat=pos.Count>65000?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
            mesh.SetVertices(pos.Select(p=>tf.InverseTransformPoint(p)).ToList());mesh.SetUVs(0,uvs);mesh.SetTriangles(kept,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            mf.sharedMesh=SaveMesh(mesh,CoastalAssets+"/"+tag+"_"+name+".asset");var col=tf.GetComponent<MeshCollider>();if(col!=null)col.sharedMesh=mf.sharedMesh;
            Debug.Log($"COASTAL_TRIM {tag} {name} dropped={dropped} split={split} tris={kept.Count/3}");
        }
        Physics.SyncTransforms();
    }

    static bool CoastalRegion(Vector3 p)=>p.x>6.5f&&p.x<29f&&p.z<.15f&&p.z>-12.7f;
    static void BuildCoastalHarbor(Layout layout,Transform parent,Transform root,Material paving,Material masonry)
    {
        // Retain the original Ground colliders and tide-dependent elevations; replace only their visible finish.
        var shed=Array.Find(layout.tiers,a=>a.id=="shed");var pier=Array.Find(layout.tiers,a=>a.id=="pier");var dock=Array.Find(layout.tiers,a=>a.id=="dock");
        Func<Area,List<Vector2>> polygon=a=>a.x.Select((x,i)=>new Vector2(x,a.z[i])).ToList();
        var shedPoly=polygon(shed);var pierPoly=polygon(pier);var dockPoly=polygon(dock);var stone=new List<Vector2>();const float seamX=22.2f;
        for(int i=0;i<shedPoly.Count;i++){var a=shedPoly[i];var b=shedPoly[(i+1)%shedPoly.Count];bool ia=a.x<=seamX,ib=b.x<=seamX;if(ia)stone.Add(a);if(ia!=ib)stone.Add(Vector2.Lerp(a,b,(seamX-a.x)/(b.x-a.x)));}
        foreach(string name in new[]{"Tier_shed","Cliff_shed","Tier_pier","Cliff_pier","Tier_dock","Cliff_dock"}){var t=parent.Find(name);if(t!=null)t.GetComponent<Renderer>().enabled=false;}
        var oldPier=parent.Find("HarborPier");if(oldPier!=null)oldPier.gameObject.SetActive(false);
        var terrace=TopMesh(stone,shed.y+.006f,"Harbor stone apron");terrace.uv=terrace.vertices.Select(p=>new Vector2(p.x*.5f,p.z*.5f)).ToArray();
        CoastalPart("harbor_apron",terrace,root,paving).layer=6;
        var wall=stone.Select(p=>new Vector3(p.x,shed.y,p.y)).ToList();wall.Add(wall[0]);
        // 2026-11-15：船棚台北沿贴着海堤（海堤自己砌到 -0.2），那一段不再砌码头墙：两道墙叠在一起，轮廓碰撞会在步道上多出一道看不见的墙
        var promenade=SmoothCoastal(PromenadeKnots);
        CoastalWall("harbor_quay",wall,root,masonry,p=>CoastalDistance(p,SmoothCoastal(HarborKnots))<1.55f||Mathf.Abs(CoastalDistance(p,promenade)-1.675f)<.35f);
        var wood=CoastalTimberMaterial();var dark=FlatMat("HarborTimberStructure",new Color(.34f,.23f,.12f),.1f);var rope=FlatMat("HarborRope",new Color(.73f,.61f,.40f),.1f);
        var dv=new List<Vector3>();var du=new List<Vector2>();var dt=new List<int>();var bv=new List<Vector3>();var bu=new List<Vector2>();var bt=new List<int>();
        float minZ=Mathf.Min(shedPoly.Min(p=>p.y),pierPoly.Min(p=>p.y)),maxZ=Mathf.Max(shedPoly.Max(p=>p.y),pierPoly.Max(p=>p.y));
        for(float z=minZ+.1f;z<maxZ;z+=.24f){
            var ranges=IntersectHorizontalLineWithPoly(shedPoly,z).Concat(IntersectHorizontalLineWithPoly(pierPoly,z)).OrderBy(q=>q.a).ToList();var merged=new List<(float a,float b)>();
            foreach(var q in ranges){if(merged.Count>0&&q.a<=merged[merged.Count-1].b){var prev=merged[merged.Count-1];merged[merged.Count-1]=(prev.a,Mathf.Max(prev.b,q.b));}else merged.Add(q);}
            foreach(var cut in IntersectHorizontalLineWithPoly(stone,z)){var next=new List<(float a,float b)>();foreach(var q in merged){if(q.b<=cut.a||q.a>=cut.b)next.Add(q);else{if(q.a<cut.a)next.Add((q.a,cut.a));if(q.b>cut.b)next.Add((cut.b,q.b));}}merged=next;}
            foreach(var q in merged){float width=q.b-q.a;if(width<.08f)continue;var center=new Vector3((q.a+q.b)*.5f,shed.y-.04f,z);AddOrientedBox(dv,du,dt,center,Vector3.right,Vector3.up,Vector3.forward,new Vector3(width,.09f,.224f));AddOrientedBox(bv,bu,bt,center-Vector3.up*.07f,Vector3.right,Vector3.up,Vector3.forward,new Vector3(width,.12f,.245f));}
        }
        var deckMesh=FinishMesh("Harbor planks",dv,du,dt);deckMesh.uv=deckMesh.vertices.Select(p=>new Vector2(p.x*.5f,p.z*.5f)).ToArray();CoastalPart("harbor_planks",deckMesh,root,wood).layer=6;
        var low=UnityEngine.Object.Instantiate(parent.Find("Tier_dock").GetComponent<MeshFilter>().sharedMesh);var lowV=low.vertices;for(int i=0;i<lowV.Length;i++)lowV[i].y+=.006f;low.vertices=lowV;low.uv=lowV.Select(p=>new Vector2(p.x*.5f,p.z*.5f)).ToArray();CoastalPart("dock_planks",low,root,wood).layer=6;
        var road=parent.Find("Road_to_dock");road.GetComponent<MeshRenderer>().sharedMaterials=new[]{wood};
        foreach(Transform child in road)if(child.name.Contains("stairs")||child.name.Contains("stringer"))child.gameObject.SetActive(false);
        var rv=new List<Vector3>();var ru=new List<Vector2>();var rt=new List<int>();
        foreach(var poly in new[]{shedPoly,pierPoly,dockPoly}){
            bool lower=poly==dockPoly;float top=lower?.9f:1.4f;
            for(int i=0;i<poly.Count;i++){
                var a=poly[i];var b=poly[(i+1)%poly.Count];var forward=(b-a).normalized;var outward=new Vector2(forward.y,-forward.x);if(InPolyXZ(poly,(a+b)*.5f+outward*.1f))outward=-outward;
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/1.7f));Vector3? last=null;
                for(int j=0;j<=count;j++){
                    var p=Vector2.Lerp(a,b,j/(float)count);var outside=p+outward*.18f;
                    bool hidden=!lower&&(InPolyXZ(stone,p-outward*.08f)||InPolyXZ(shedPoly,outside)||InPolyXZ(pierPoly,outside));
                    bool opening=lower?(p.y>-17.85f&&p.x>22.2f&&p.x<24.9f)||(p.x>26&&p.y>-18.7f):(p.y<-15.9f&&Mathf.Abs(p.x-23.3f)<1.25f);
                    if(hidden){last=null;continue;}
                    AddCylinderPiling(bv,bu,bt,new Vector3(p.x,-2f,p.y),.13f,top+2f+(opening?.025f:.64f),10);
                    if(opening){last=null;continue;}
                    var attach=new Vector3(p.x,top+.52f,p.y);if(last.HasValue)AddRopeSpan(rv,ru,rt,last.Value,attach,.025f,.10f);last=attach;
                }
            }
        }
        // Timber fascia encloses the low landing while leaving the water visible between its piles.
        for(int i=0;i<dockPoly.Count;i++){var a=new Vector3(dockPoly[i].x,.79f,dockPoly[i].y);var b=new Vector3(dockPoly[(i+1)%dockPoly.Count].x,.79f,dockPoly[(i+1)%dockPoly.Count].y);var d=(b-a).normalized;AddOrientedBox(bv,bu,bt,(a+b)*.5f,Vector3.Cross(d,Vector3.up),Vector3.up,d,new Vector3(.12f,.22f,Vector3.Distance(a,b)));}
        CoastalPart("harbor_pilings",FinishMesh("Harbor timber structure",bv,bu,bt),root,dark);CoastalPart("harbor_rope",FinishMesh("Harbor rope",rv,ru,rt),root,rope);
    }
    static Material CoastalTimberMaterial()
    {
        const int n=512;var px=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++){float grain=Mathf.PerlinNoise(x*.035f,y*.8f)*.1f;float seam=(y%64)<3?.65f:1f;px[y*n+x]=new Color(.65f+grain,.43f+grain,.22f+grain*.7f)*seam;}
        var tex=new Texture2D(n,n,TextureFormat.RGB24,false);tex.SetPixels(px);tex.Apply();string path=CoastalAssets+"/HarborTimber.png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.SaveAndReimport();var mat=FlatMat("HarborDeckTimber",Color.white,.12f);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));EditorUtility.SetDirty(mat);return mat;
    }
    static Vector3 CoastalSide(List<Vector3> points,int i){var d=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)];d.y=0;return Vector3.Cross(Vector3.up,d.normalized);}
    static float CoastalDistance(Vector3 p,List<Vector3> line){float d=float.MaxValue;for(int i=1;i<line.Count;i++)d=Mathf.Min(d,PlacementSegmentDistance(p,line[i-1],line[i]));return d;}
    static List<Vector3> SmoothCoastal(Vector3[] points, bool smoothHeight = true)
    {
        var result=new List<Vector3>();
        for(int i=0;i<points.Length-1;i++){
            var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];int steps=Mathf.CeilToInt(Vector3.Distance(b,c)/.16f);
            for(int j=0;j<steps;j++){float t=j/(float)steps;var p=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);p.y=Mathf.Lerp(b.y,c.y,smoothHeight?Mathf.SmoothStep(0,1,t):t);result.Add(p);}
        }result.Add(points[points.Length-1]);return result;
    }
    static void CoastalDeck(string id,List<Vector3> points,float width,Transform root,Material material,List<Area> areas,IslandWorld world,Func<List<Vector3>,List<Vector3>> sideFit=null)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();float length=0;
        var left=new List<Vector2>();var right=new List<Vector2>();
        var sa=new List<Vector3>();var sb=new List<Vector3>();
        for(int i=0;i<points.Count;i++){var side=CoastalSide(points,i)*width*.5f;sa.Add(points[i]-side);sb.Add(points[i]+side);}
        if(sideFit!=null){sa=sideFit(sa);sb=sideFit(sb);}
        for(int i=0;i<points.Count;i++){if(i>0)length+=Vector3.Distance(points[i-1],points[i]);var a=sa[i];var b=sb[i];v.Add(a);v.Add(b);uv.Add(new Vector2(0,length*.5f));uv.Add(new Vector2(width*.5f,length*.5f));left.Add(new Vector2(a.x,a.z));right.Add(new Vector2(b.x,b.z));if(i>0){int k=(i-1)*2;FinishQuad(tris,k,k+2,k+3,k+1);}}
        var mesh=FinishMesh("Finished Coastal "+id,v,uv,tris);var obj=CoastalPart(id,mesh,root,material);obj.layer=6;obj.AddComponent<MeshCollider>().sharedMesh=obj.GetComponent<MeshFilter>().sharedMesh;
        right.Reverse();left.AddRange(right);var poly=left.ToArray();var area=new Area{id="coastal_"+id,y=points[0].y,walk=true,zone="stone",x=poly.Select(p=>p.x).ToArray(),z=poly.Select(p=>p.y).ToArray()};areas.Add(area);world.walkPolys.Add(new WalkPoly{id=area.id,y=area.y,points=poly});
    }
    // 2026-11-16 下午：坡道一侧边线（从步道那头开始）贴着步道 deckY 走：压在步道下的顶点低 8 mm；
    // 出了步道边以后，blend > 0：在 blend 米内从步道边的高度渐变回原来的高度；reslope：从步道边的高度直线斜到最后一个顶点。
    static List<Vector3> LinearResample(List<Vector3> knots,float step)
    {
        var r=new List<Vector3>();
        for(int i=0;i+1<knots.Count;i++){int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(knots[i],knots[i+1])/step));for(int j=0;j<n;j++)r.Add(Vector3.Lerp(knots[i],knots[i+1],j/(float)n));}
        r.Add(knots[knots.Count-1]);return r;
    }
    static List<Vector3> FitBelowDeck(List<Vector3> side,Func<Vector3,float> deckY,float blend,bool reslope)
    {
        var o=new List<Vector3>(side);int exit=-1;float edgeY=float.NaN;
        for(int i=0;i<o.Count;i++){float y=deckY(o[i]);if(float.IsNaN(y)){exit=i;break;}edgeY=y;var p=o[i];p.y=Mathf.Min(p.y,y-.008f);o[i]=p;}
        if(exit<=0||float.IsNaN(edgeY))return o;
        float total=0f;for(int i=exit+1;i<o.Count;i++)total+=Vector2.Distance(new Vector2(o[i-1].x,o[i-1].z),new Vector2(o[i].x,o[i].z));
        float d=0f;
        for(int i=exit;i<o.Count;i++)
        {
            if(i>exit)d+=Vector2.Distance(new Vector2(o[i-1].x,o[i-1].z),new Vector2(o[i].x,o[i].z));
            var p=o[i];
            if(reslope)p.y=Mathf.Lerp(edgeY,side[side.Count-1].y,total>1e-4f?d/total:1f);
            else if(blend>0f)p.y=Mathf.Lerp(edgeY,side[i].y,Mathf.Clamp01(d/blend));
            o[i]=p;
        }
        return o;
    }
    static GameObject CoastalPart(string name,Mesh mesh,Transform parent,Material material){var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,CoastalAssets+"/"+name+".asset");go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;}
    static void CoastalWall(string id,List<Vector3> top,Transform root,Material mat,Func<Vector3,bool> skip,float bottom=-.2f)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();float run=0;
        // 2026-11-15：正反两面各用一套顶点（AddTwoSidedQuad）。原来共用顶点，法线被平均成 0，着色器里变 NaN，墙面一片黑点。
        for(int i=1;i<top.Count;i++){var a=top[i-1];var b=top[i];float next=run+Vector3.Distance(a,b);if(!skip((a+b)*.5f))AddTwoSidedQuad(v,uv,tris,a,b,new Vector3(b.x,bottom,b.z),new Vector3(a.x,bottom,a.z),new Vector2(run*.5f,a.y),new Vector2(next*.5f,b.y),new Vector2(next*.5f,bottom),new Vector2(run*.5f,bottom));run=next;}
        CoastalPart(id,FinishMesh(id,v,uv,tris),root,mat);
    }
    static void CoastalRail(string id,List<Vector3> edge,Transform root,Material cap,Material wood,Func<Vector3,bool> skip,Func<Vector3,bool> sillOnly=null)
    {
        var cv=new List<Vector3>();var cu=new List<Vector2>();var ct=new List<int>();var rv=new List<Vector3>();var ru=new List<Vector2>();var rt=new List<int>();
        float run=0,nextPost=.15f;Vector3? last=null,tail=null;
        // 2026-11-16 下午（图 1 转角两根石柱斜撞、横杆穿柱）：所有护栏共用一张立柱表。新柱子离已有柱子 0.45 m 以内就不再立，
        // 横杆直接接到那根已有的柱子上——两段护栏在转角共用一根角柱。
        // 一段护栏的起始柱 / 端柱放宽到 0.8 m（转角处两段护栏的端头离得稍远也合成一根）
        Vector3 Post(Vector3 p,float snap=.45f){foreach(var q in railPosts)if(new Vector2(q.x-p.x,q.z-p.z).magnitude<snap&&Mathf.Abs(q.y-p.y)<.6f)return q;
            railPosts.Add(p);AddOrientedBox(cv,cu,ct,p+Vector3.up*.45f,Vector3.right,Vector3.up,Vector3.forward,new Vector3(.24f,.9f,.24f));AddOrientedBox(cv,cu,ct,p+Vector3.up*.93f,Vector3.right,Vector3.up,Vector3.forward,new Vector3(.32f,.10f,.32f));return p;}
        void Rails(Vector3 from,Vector3 to){var delta=to-from;if(delta.magnitude<.05f)return;var f=delta.normalized;var r=Vector3.Cross(f,Vector3.up).normalized;foreach(float y in new[]{.38f,.70f})AddOrientedBox(rv,ru,rt,(to+from)*.5f+Vector3.up*y,r,Vector3.up,f,new Vector3(.09f,.10f,delta.magnitude));}
        // 2026-11-16：每一段护栏的末端补一根端柱，横杆一直接到端柱上。原来末端停在最后一根 1.65 m 间距的柱子上，
        // 后面只剩一截矮石槛，路口两边的护栏收不了口（转角、灯塔台入口都是敞口）。
        void Close(){if(tail.HasValue&&last.HasValue&&Vector3.Distance(last.Value,tail.Value)>.3f){var t=Post(tail.Value,.8f);Rails(last.Value,t);}last=null;tail=null;}
        for(int i=1;i<edge.Count;i++){var a=edge[i-1];var b=edge[i];float len=Vector3.Distance(a,b);var mid=(a+b)*.5f;var direction=(b-a).normalized;var side=Vector3.Cross(direction,Vector3.up).normalized;
            if(skip(mid)){Close();run+=len;continue;}
            if(sillOnly!=null&&sillOnly(mid)){Close();AddOrientedBox(cv,cu,ct,mid+Vector3.up*.05f,side,Vector3.up,direction,new Vector3(.26f,.12f,len+.02f));nextPost=run+len+.6f;run+=len;continue;}
            AddOrientedBox(cv,cu,ct,mid+Vector3.up*.045f,side,Vector3.up,direction,new Vector3(.28f,.12f,len+.02f));
            // 2026-11-16 下午（图 3 豁口一边有柱一边没柱）：每一段护栏的开头也立一根起始柱（原来豁口后面要等到下一个 1.65 m 才有柱子）
            if(!last.HasValue&&!tail.HasValue){last=Post(a,.8f);nextPost=run+1.65f;}
            if(run+len>=nextPost){var p=Vector3.Lerp(a,b,Mathf.Clamp01((nextPost-run)/Mathf.Max(.001f,len)));nextPost=run+len+1.65f;
                p=Post(p);
                if(last.HasValue&&Vector3.Distance(last.Value,p)<2.4f)Rails(last.Value,p);last=p;
            }tail=b;run+=len;
        }
        Close();
        CoastalPart(id+"_stone",FinishMesh(id,cv,cu,ct),root,cap);CoastalPart(id+"_rails",FinishMesh(id,rv,ru,rt),root,wood);
    }
    // 2026-11-16：栅栏（AddFenceBox 拼的，每个方块 24 个顶点连着放）不再按三角形重心裁——那样会把立柱切掉、留下半截悬空横杆，
    // 方块本身也会缺面。改成按整块裁：方块中心落在裁剪区就整块去掉；然后凡是两端任何一头没有立柱托着的横杆，整根去掉；
    // 学校园区栅栏上跟着横杆走的隐形挡板（SchoolFenceCol_*）也一起拆。
    static bool TrimFenceMesh(MeshFilter mf,Func<Vector3,bool> remove,string name)
    {
        var src=mf.sharedMesh;var verts=src.vertices;int boxes=verts.Length/24;
        var lo=new Vector3[boxes];var hi=new Vector3[boxes];
        for(int b=0;b<boxes;b++){lo[b]=Vector3.positiveInfinity;hi[b]=Vector3.negativeInfinity;for(int k=0;k<24;k++){var w=mf.transform.TransformPoint(verts[b*24+k]);lo[b]=Vector3.Min(lo[b],w);hi[b]=Vector3.Max(hi[b],w);}}
        var tris=src.GetTriangles(0);var present=new bool[boxes];for(int i=0;i<tris.Length;i+=3)present[tris[i]/24]=true;
        bool IsPost(int b){var s=hi[b]-lo[b];return s.y>.3f&&s.x<.25f&&s.z<.25f;}
        var keep=new bool[boxes];bool changed=false;
        for(int b=0;b<boxes;b++){if(!present[b])continue;keep[b]=!remove((lo[b]+hi[b])*.5f);if(!keep[b])changed=true;}
        var posts=new List<Vector3>();for(int b=0;b<boxes;b++)if(keep[b]&&IsPost(b))posts.Add((lo[b]+hi[b])*.5f);
        bool Held(Vector3 end,float y){foreach(var p in posts)if(new Vector2(p.x-end.x,p.z-end.z).magnitude<.14f&&y<p.y+.6f&&y>p.y-.6f)return true;return false;}
        for(int b=0;b<boxes;b++)
        {
            if(!keep[b]||IsPost(b))continue;
            // 横杆两端：沿包围盒最长的水平方向。斜着的杆用顶点里离中心最远的两点
            var c=(lo[b]+hi[b])*.5f;Vector3 e0=c,e1=c;float best=-1f;
            for(int k=0;k<24;k++){var w=mf.transform.TransformPoint(verts[b*24+k]);var d=new Vector2(w.x-c.x,w.z-c.z);if(d.sqrMagnitude>best){best=d.sqrMagnitude;e0=w;}}
            var axis=new Vector2(e0.x-c.x,e0.z-c.z).normalized;float half=0f;
            for(int k=0;k<24;k++){var w=mf.transform.TransformPoint(verts[b*24+k]);half=Mathf.Max(half,Vector2.Dot(new Vector2(w.x-c.x,w.z-c.z),axis));}
            half-=.03f;   // 去掉横杆自身的半宽，取到轴线端点（= 立柱中心）
            var a3=new Vector3(axis.x,0,axis.y);e0=c+a3*half;e1=c-a3*half;
            if(!Held(e0,c.y)||!Held(e1,c.y)){keep[b]=false;changed=true;}
        }
        if(!changed)return false;
        var kept=new List<int>();for(int i=0;i<tris.Length;i+=3)if(keep[tris[i]/24]){kept.Add(tris[i]);kept.Add(tris[i+1]);kept.Add(tris[i+2]);}
        var clone=UnityEngine.Object.Instantiate(src);clone.SetTriangles(kept,0);clone.RecalculateBounds();mf.sharedMesh=SaveMesh(clone,CoastalAssets+"/"+name+".asset");
        var rails=new List<Vector3>();for(int b=0;b<boxes;b++)if(keep[b]&&!IsPost(b))rails.Add((lo[b]+hi[b])*.5f);
        foreach(var bc in mf.GetComponentsInChildren<BoxCollider>(true).Where(x=>x.name.StartsWith("SchoolFenceCol_")).ToArray())
        {
            var p=bc.transform.position;bool any=false;foreach(var r in rails)if(new Vector2(r.x-p.x,r.z-p.z).magnitude<.25f){any=true;break;}
            if(!any)UnityEngine.Object.DestroyImmediate(bc.gameObject);
        }
        return true;
    }
    static bool TrimCoastalMesh(MeshFilter mf,Func<Vector3,bool> remove,string name)
    {
        if(HasPlacementAncestor(mf.transform,"Fences")&&mf.sharedMesh!=null&&mf.sharedMesh.subMeshCount==1&&mf.sharedMesh.vertexCount%24==0&&mf.sharedMesh.vertexCount>0)
            return TrimFenceMesh(mf,remove,name);
        var src=mf.sharedMesh;var verts=src.vertices;bool changed=false;var all=new List<int[]>();
        for(int sm=0;sm<src.subMeshCount;sm++){var indices=src.GetTriangles(sm);var kept=new List<int>();for(int i=0;i<indices.Length;i+=3){var p=mf.transform.TransformPoint((verts[indices[i]]+verts[indices[i+1]]+verts[indices[i+2]])/3f);if(remove(p))changed=true;else{kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);}}all.Add(kept.ToArray());}
        if(!changed)return false;var clone=UnityEngine.Object.Instantiate(src);for(int i=0;i<all.Count;i++)clone.SetTriangles(all[i],i);clone.RecalculateBounds();mf.sharedMesh=SaveMesh(clone,CoastalAssets+"/"+name+".asset");var col=mf.GetComponent<MeshCollider>();if(col!=null)col.sharedMesh=mf.sharedMesh;return true;
    }
    static Material CoastalMaterial(string name,bool wall)
    {
        string texPath=CoastalAssets+"/"+name+".png";const int N=512;var pixels=new Color[N*N];
        for(int y=0;y<N;y++)for(int x=0;x<N;x++){
            int rows=wall?2:8,cols=wall?2:4;float py=y/(float)N*rows;int row=Mathf.FloorToInt(py);float px=x/(float)N*cols+(row%2)*.5f;int col=Mathf.FloorToInt(px);float fx=px-col,fy=py-row;
            float edge=Mathf.Min(Mathf.Min(fx,1-fx),Mathf.Min(fy,1-fy));float noise=Mathf.PerlinNoise(x*.13f,y*.13f)*.035f;float shade=((row*37+col*19+83)%11-5)*.009f;
            Color c=wall?new Color(.69f+shade,.60f+shade,.48f+shade):new Color(.78f+shade,.69f+shade,.57f+shade);
            if(edge<.018f)c*=wall?.65f:.79f;else if(edge<.04f)c*=.92f;c+=new Color(noise,noise,noise,0);pixels[y*N+x]=c;
        }
        var tex=new Texture2D(N,N,TextureFormat.RGB24,false);tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(texPath,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(texPath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(texPath);importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.SaveAndReimport();
        var mat=FlatMat(name,Color.white,.10f);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));EditorUtility.SetDirty(mat);return mat;
    }
}
