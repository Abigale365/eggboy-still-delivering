// WP-06 场景世界：地面走 Ground 层射线，阻挡读 TownObstacle，可走区域用多边形 + 水位。
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WalkPoly
{
    public string id;
    public float y;
    public Vector2[] points;
}

public class IslandWorld : MonoBehaviour, IEggboyWorld
{
    public LayerMask groundMask;
    public LayerMask obstacleMask;
    public Transform mailboxMarker;
    public Transform spawnMarker;
    public GameObject mailboxTrail;
    public GameObject endingDesk;
    public GameObject woodModel, oilModel;
    public float waterLevel;

    public List<WalkPoly> walkPolys = new List<WalkPoly>();
    public List<WalkPoly> lawnPolys = new List<WalkPoly>();
    public WalkPoly beachPoly;

    readonly List<EggboyCollider> colliders = new List<EggboyCollider>();
    const float SpatialCell = 2.0f;
    readonly Dictionary<long, List<EggboyCollider>> spatialBuckets = new Dictionary<long, List<EggboyCollider>>();
    bool spatialDirty = true;
    EggboyCollider endingDeskCollider;

    public IReadOnlyList<EggboyCollider> Colliders => colliders;

    // WP-08：水位由 WaterLevel 管，IslandWorld 只读它
    public float WaterY => WaterLevel.Main != null ? WaterLevel.Main.Y : waterLevel;
    public void AddCollider(EggboyCollider c)
    {
        if (c != null && !colliders.Contains(c))
        {
            colliders.Add(c);
            spatialDirty = true;
        }
    }
    public void RemoveCollider(EggboyCollider c)
    {
        if (c != null && colliders.Remove(c))
            spatialDirty = true;
    }

    public void SyncEndingDeskCollider()
    {
        if (endingDesk == null) return;
        if (endingDesk.activeInHierarchy)
        {
            if (endingDeskCollider == null)
            {
                var p = endingDesk.transform.position;
                endingDeskCollider = new EggboyCollider
                {
                    x = p.x,
                    z = p.z,
                    r = 0.20f,
                    top = p.y + 0.76f
                };
            }
            AddCollider(endingDeskCollider);
        }
        else if (endingDeskCollider != null)
        {
            RemoveCollider(endingDeskCollider);
        }
    }

    void RebuildSpatialGrid()
    {
        spatialDirty = false;
        spatialBuckets.Clear();
        const float Margin = 0.60f; // 大于 EggboyPlayer.Radius (0.42m)，保证单格查询即可覆盖全部可能接触的碰撞体
        for (int i = 0; i < colliders.Count; i++)
        {
            var c = colliders[i];
            float ex = (c.hw ?? c.r ?? 0.5f) + Margin;
            float ez = (c.hd ?? c.r ?? 0.5f) + Margin;
            int x0 = Mathf.FloorToInt((c.x - ex) / SpatialCell);
            int x1 = Mathf.FloorToInt((c.x + ex) / SpatialCell);
            int z0 = Mathf.FloorToInt((c.z - ez) / SpatialCell);
            int z1 = Mathf.FloorToInt((c.z + ez) / SpatialCell);
            for (int gz = z0; gz <= z1; gz++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    long key = ((long)gx << 32) | (uint)gz;
                    if (!spatialBuckets.TryGetValue(key, out var list))
                    {
                        list = new List<EggboyCollider>(8);
                        spatialBuckets[key] = list;
                    }
                    list.Add(c);
                }
            }
        }
    }

    public void QueryColliders(float x, float z, List<EggboyCollider> results)
    {
        results.Clear();
        if (spatialDirty) RebuildSpatialGrid();
        int gx = Mathf.FloorToInt(x / SpatialCell);
        int gz = Mathf.FloorToInt(z / SpatialCell);
        long key = ((long)gx << 32) | (uint)gz;
        if (spatialBuckets.TryGetValue(key, out var list))
            results.AddRange(list);
    }
    public Vector3 MailboxPos => mailboxMarker != null ? mailboxMarker.position : Vector3.zero;
    readonly List<EggboyCollider> cameraCandidates = new List<EggboyCollider>();
    readonly HashSet<EggboyCollider> cameraSeen = new HashSet<EggboyCollider>();

    // 覆盖整条相机射线跨过的空间桶，去重后做解析相交，避免末端单格漏墙。
    public void QueryCollidersRange(Vector3 a, Vector3 b, float margin, List<EggboyCollider> results)
    {
        results.Clear(); cameraSeen.Clear();
        if (spatialDirty) RebuildSpatialGrid();
        int x0 = Mathf.FloorToInt((Mathf.Min(a.x,b.x)-margin)/SpatialCell), x1 = Mathf.FloorToInt((Mathf.Max(a.x,b.x)+margin)/SpatialCell);
        int z0 = Mathf.FloorToInt((Mathf.Min(a.z,b.z)-margin)/SpatialCell), z1 = Mathf.FloorToInt((Mathf.Max(a.z,b.z)+margin)/SpatialCell);
        for (int z=z0;z<=z1;z++) for(int x=x0;x<=x1;x++)
            if(spatialBuckets.TryGetValue(((long)x<<32)|(uint)z,out var list))
                foreach(var c in list) if(cameraSeen.Add(c)) results.Add(c);
    }
    public float CameraBlockDistance(Vector3 pivot, Vector3 direction, float maxDistance)
    {
        direction.Normalize();
        QueryCollidersRange(pivot,pivot+direction*maxDistance,.35f,cameraCandidates);
        float allowed=maxDistance;
        foreach(var c in cameraCandidates)
            if(CameraIntersection(c,pivot,direction,allowed,.30f,out float enter)) allowed=Mathf.Min(allowed,Mathf.Max(0f,enter-.08f));
        return allowed;
    }
    public bool CameraPointBlocked(Vector3 point)
    {
        QueryCollidersRange(point,point,.01f,cameraCandidates);
        foreach(var c in cameraCandidates) if(CameraIntersection(c,point,Vector3.forward,0f,0f,out _)) return true;
        return false;
    }
    static bool CameraIntersection(EggboyCollider c,Vector3 p,Vector3 d,float max,float radius,out float enter)
    {
        enter=0f;float exit=max;
        // 细杆和轮廓采样圆交给前景消融，避免逐柱前缩抖动。
        if(c.water || (c.r.HasValue && c.r.Value<.24f)) return false;
        if(c.r.HasValue){
            float x=p.x-c.x,z=p.z-c.z,r=c.r.Value+radius;
            float a=d.x*d.x+d.z*d.z,b=x*d.x+z*d.z,q=x*x+z*z-r*r;
            if(a<1e-8f){if(q>0f)return false;}
            else {float disc=b*b-a*q;if(disc<0)return false;float s=Mathf.Sqrt(disc);enter=Mathf.Max(enter,(-b-s)/a);exit=Mathf.Min(exit,(-b+s)/a);}
        } else if(c.hw.HasValue && c.hd.HasValue){
            if(!CameraSlab(p.x,d.x,c.x-c.hw.Value-radius,c.x+c.hw.Value+radius,ref enter,ref exit)
                ||!CameraSlab(p.z,d.z,c.z-c.hd.Value-radius,c.z+c.hd.Value+radius,ref enter,ref exit))return false;
        } else return false;
        // top == null 是完整垂直阻挡柱；仅显式 top 才允许越过。
        float low=c.minY>0f?c.minY-radius:float.NegativeInfinity;
        float high=c.top.HasValue?c.top.Value+.08f+radius:float.PositiveInfinity;
        return CameraSlab(p.y,d.y,low,high,ref enter,ref exit)&&exit>=enter&&exit>=0f&&enter<=max;
    }
    static bool CameraSlab(float p,float d,float low,float high,ref float enter,ref float exit)
    {
        if(Mathf.Abs(d)<1e-8f)return p>=low&&p<=high;
        float a=(low-p)/d,b=(high-p)/d;if(a>b){float t=a;a=b;b=t;}
        enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);return exit>=enter;
    }
    public Vector3 SpawnPos => spawnMarker != null ? spawnMarker.position : Vector3.zero;

    void Awake()
    {
        if (groundMask == 0) groundMask = LayerMask.GetMask("Ground");
        if (obstacleMask == 0) obstacleMask = LayerMask.GetMask("TownObstacle");
        CollectObstacles();
        CleanVisualRoadAndTierSeams();
    }

    // 仅调整视觉 MeshFilter（绝不改动 MeshCollider 物理碰撞，保证寻路与审计 100% 一致）：
    // 1. 移除坡道 Road_* 外扩 93cm 的凸起碎石边/路缘/悬空裙边（避免切穿石板路和栅栏造成穿模）；
    // 2. 将 Road_* 与 3.0m 主街交界处的顶面微压 2.5cm，避免与石板路 Z-fighting 闪烁；
    // 3. 消除 Tier_pad_grass_3_0_0（操场草坪 y=3.02）与 Tier_pad_stone_3_0_0（石板路 y=3.02）同高重叠导致的草坪三角穿模。
    void CleanVisualRoadAndTierSeams()
    {
        Material cobbleMat = null;
        var stoneTier = GameObject.Find("Tier_pad_stone_3_0_0");
        if (stoneTier != null)
        {
            var stoneMr = stoneTier.GetComponent<MeshRenderer>();
            if (stoneMr != null) cobbleMat = stoneMr.sharedMaterial;
        }

        var grassTier = GameObject.Find("Tier_pad_grass_3_0_0");
        if (grassTier != null)
        {
            var gmf = grassTier.GetComponent<MeshFilter>();
            if (gmf != null && gmf.sharedMesh != null)
            {
                var m = Instantiate(gmf.sharedMesh);
                var v = m.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    // 操场草坪整体微降 0.8cm（仍高于 Tier_main 3.00m），但在秋千/滑梯操场内部区域略抬高以盖住石板多边形凹角
                    if (v[i].x > 8.2f && v[i].x < 23.2f && v[i].z > -7.1f && v[i].z < 8.5f)
                        v[i].y = 3.028f;
                    else
                        v[i].y = 3.012f;
                }
                m.vertices = v;
                m.RecalculateBounds();
                gmf.sharedMesh = m;
            }
        }

        if (stoneTier != null)
        {
            var smf = stoneTier.GetComponent<MeshFilter>();
            if (smf != null && smf.sharedMesh != null)
            {
                var m = Instantiate(smf.sharedMesh);
                var v = m.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    // 石板路在操场护栏内侧的折角顶点压到草坪下方，消除草坪里的斜三角石板穿模
                    if (v[i].x > 8.5f && v[i].x < 23.8f && v[i].z > -7.9f && v[i].z < -4.5f)
                        v[i].y = 3.005f;
                    else
                        v[i].y = 3.024f;
                }
                m.vertices = v;
                m.RecalculateBounds();
                smf.sharedMesh = m;
            }
        }

        foreach (var mf in FindObjectsOfType<MeshFilter>())
        {
            if (mf == null || mf.sharedMesh == null) continue;
            string goName = mf.gameObject.name;
            if (!goName.StartsWith("Road_")) continue;
            // The finished surfaces are authored against their platform joins and roadbeds.
            if (mf.sharedMesh.name.StartsWith("Finished Road ", System.StringComparison.Ordinal)) continue;

            var mesh = Instantiate(mf.sharedMesh);
            if (mesh.subMeshCount >= 4)
            {
                // 只保留主路面（subMesh 0），去掉两侧外扩 93cm 穿入石板路/栅栏/悬空的路缘、碎石边和裙边
                mesh.SetTriangles(new int[0], 1);
                mesh.SetTriangles(new int[0], 2);
                mesh.SetTriangles(new int[0], 3);
            }

            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                // 坡道与各层台地（3.0m 主街、5.0m 灯塔台地、5.5m 住宅石板台地、6.5m 山顶邮局台地）交界处微压，彻底消除路面接缝 Z-fighting
                if (verts[i].y >= 2.95f && verts[i].y <= 3.05f)
                    verts[i].y = 3.010f;
                else if (verts[i].y >= 4.96f && verts[i].y <= 5.05f)
                    verts[i].y = 4.992f;
                else if (verts[i].y >= 5.46f && verts[i].y <= 5.55f)
                    verts[i].y = 5.510f;
                else if (verts[i].y >= 6.46f && verts[i].y <= 6.55f)
                    verts[i].y = 6.492f;
            }
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            if (cobbleMat != null && goName != "Road_to_dock")
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterials != null && mr.sharedMaterials.Length > 0)
                {
                    var mats = mr.sharedMaterials;
                    mats[0] = cobbleMat;
                    mr.sharedMaterials = mats;
                }
            }
        }
    }

    void CollectObstacles()
    {
        colliders.Clear();
        foreach (var o in FindObjectsOfType<TownObstacle>())
        {
            var box = o.GetComponent<BoxCollider>();
            if (box == null) continue;
            var t = o.transform;
            Vector3 c = t.TransformPoint(box.center);
            Vector3 s = Vector3.Scale(box.size, t.lossyScale);
            var ec = new EggboyCollider
            {
                x = c.x,
                z = c.z,
                hw = s.x * 0.5f,
                hd = s.z * 0.5f,
                water = o.kind == TownObstacle.Kind.Water,
                minY = o.minY,
            };
            if (o.kind == TownObstacle.Kind.WalkableTop)
                ec.top = c.y + s.y * 0.5f;
            colliders.Add(ec);
        }
        // 按模型外轮廓的小圆（TownObstacleSet）
        foreach (var set in FindObjectsOfType<TownObstacleSet>())
            for (int i = 0; i < set.x.Length; i++)
                colliders.Add(new EggboyCollider { x = set.x[i], z = set.z[i], r = set.r[i], top = set.top[i] });
        spatialDirty = true;
    }

    // 2026-11-14 沙滩上能涉多深的水（米）。原来到处都是 0.05：水一没过脚面就算走不了，EGGBOY 永远碰不到水。
    // 沙滩（0.3）在 L1（0.45）没过脚 15 厘米，可以下去踩水；L2（1.15）就太深了。
    // 只放开沙滩，路面照旧 0.05：渡口缺口那段路的河床在 L2 时水深只有 12 厘米，路面也放开的话人直接蹚过去，
    // 木箱 / 绞盘 / 心愿船这些过海的办法就全被绕开了（创新审计 print_gap_blocks 等 4 项会不过）。
    public const float WadeDepth = 0.18f;
    public const float RoadWadeDepth = 0.05f;

    // 站在这儿、地面这么高，水最多能没过脚多少还算能走
    public float WadeLimit(float x, float z, float ground) => IsBeachSurface(x, z, ground) ? WadeDepth : RoadWadeDepth;

    public bool Walkable(float x, float z) => walkPolys.Count == 0 || TryWalkableGround(x, z, out _);

    bool TryWalkableGround(float x, float z, out float ground)
    {
        ground = 0f;
        bool road = InWalkArea(x, z);
        bool lawn = !road && InLawnArea(x, z);
        bool sand = beachPoly != null && Contains(beachPoly.points, x, z);
        if (!road && !lawn && !sand) return false;
        ground = GroundAt(x, z);
        if (ground < WaterY - WadeLimit(x, z, ground)) return false;
        return sand && Mathf.Abs(ground - beachPoly.y) <= 0.12f || (road ? ground > 0.22f : lawn && ground >= 2.5f);
    }

    public bool IsBeachSurface(float x, float z, float height) =>
        beachPoly != null && Mathf.Abs(height - beachPoly.y) <= 0.12f && Contains(beachPoly.points, x, z);

    // ---------------- 2026-11-16 自由漫游（用户议题 2） ----------------
    // 原来主角只能走在 walkPolys / lawnPolys / beachPoly 三种白名单多边形里，台地上大片草地、山顶、海岸都是空气墙。
    // 现在只看真实的东西：
    //   1. 地形高低：从身体顶部（脚 + 1.3 m）往下打 Ground 射线，落点比脚高出 Step（0.23 m，空中 0.28 m）就是墙 / 崖面，挡住；
    //      从身体顶部往下打，所以头顶上方的平台（灯塔顶）不会把人吸上去。
    //   2. 跳落：按跳往下最多 MaxDrop（3 m，用户定的）；走路只下 0.6 m 以内的坎，更高的边停住（要跳才下去）。
    //   3. 水：照旧，没过脚超过可涉深度（沙滩 0.18，别处 0.05）就不能走——渡口缺口在 L2 时靠这条挡住。
    //   4. 少量禁区（Resources/World/roam_blocks.json）：需要硬边界、又没有真实墙体的地方（比如从别处绕过渡口缺口）。
    //   5. 墙体、护栏、建筑、树干、岩块照旧由轮廓碰撞圆挡（EggboyPlayer 里）。
    // walkPolys 保留，只表示"铺装路面"（狗爪印、海风、地图、谜题摆放这些地方还在用 InWalkArea / Walkable 判断）。
    public const float MaxDrop = 3.0f;
    public const float WalkDrop = 0.6f;
    public const float BodyTop = 1.3f;
    public const float StepUp = 0.23f;
    // 空中只认比当前脚高 0.28 m 以内的落点：GroundAt 只看脚上方 0.3 m，再高的落点"看不见"，会从台子侧面钻进去
    // （起跳 0.42 m + 0.28 m，跳起来最高能上 0.7 m 的台子）
    public const float JumpUp = 0.28f;
    public bool FreeRoam => Features.freeRoam && walkPolys.Count > 0;

    [System.Serializable] public class RoamBlock { public string id, when; public float minY = -10f, maxY = 100f; public float[] x, z; [System.NonSerialized] public Vector2[] pts; }
    [System.Serializable] class RoamBlockDoc { public RoamBlock[] blocks; }
    RoamBlock[] roamBlocks;
    public System.Func<string, bool> RoamCondition;   // when 条件（比如 "gap"）由别的系统回答；没人回答当作成立
    public IReadOnlyList<RoamBlock> RoamBlocks { get { LoadRoamBlocks(); return roamBlocks; } }

    void LoadRoamBlocks()
    {
        if (roamBlocks != null) return;
        var asset = Resources.Load<TextAsset>("World/roam_blocks");
        var doc = asset != null ? JsonUtility.FromJson<RoamBlockDoc>(asset.text) : null;
        roamBlocks = doc != null && doc.blocks != null ? doc.blocks : new RoamBlock[0];
        foreach (var b in roamBlocks)
        {
            b.pts = new Vector2[Mathf.Min(b.x?.Length ?? 0, b.z?.Length ?? 0)];
            for (int i = 0; i < b.pts.Length; i++) b.pts[i] = new Vector2(b.x[i], b.z[i]);
        }
    }

    public bool InRoamBlock(float x, float z, float ground)
    {
        LoadRoamBlocks();
        foreach (var b in roamBlocks)
        {
            if (ground < b.minY || ground > b.maxY || !Contains(b.pts, x, z)) continue;
            if (!string.IsNullOrEmpty(b.when) && b.when != "always" && RoamCondition != null && !RoamCondition(b.when)) continue;
            return true;
        }
        return false;
    }

    // 从 fromY 往下最高的 Ground 面；没有返回 NaN（海面以外、地图边外）
    public float SurfaceBelow(float x, float z, float fromY)
    {
        float best = float.NaN;
        var hits = Physics.RaycastAll(new Vector3(x, fromY, z), Vector3.down, fromY + 60f, groundMask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits) if (float.IsNaN(best) || hit.point.y > best) best = hit.point.y;
        return best;
    }

    // 自由漫游下从 from 走一步到 (x, z) 行不行（只看地形 / 水 / 禁区；碰撞圆在 EggboyPlayer 里另算）
    public bool RoamStep(Vector3 from, float x, float z, bool jumping)
    {
        float g = SurfaceBelow(x, z, from.y + BodyTop);
        if (float.IsNaN(g)) return false;
        float rise = g - from.y;
        if (rise > (jumping ? JumpUp : StepUp)) return false;                 // 墙 / 崖面 / 太高的台子
        // 走路只往下迈 0.6 m 以内（路缘、小台阶）；更高的台地边要按跳才下得去（最多 3 m）——不会一不留神走下崖
        if (from.y - g > (jumping ? MaxDrop + 0.45f : WalkDrop)) return false;
        if (g < WaterY - WadeLimit(x, z, g)) return false;                   // 深水
        if (InRoamBlock(x, z, g)) return false;
        return true;
    }

    // 自由漫游下这一点能不能站（给安全点、拾取站位这类"找一个能站的地方"用）
    public bool RoamStandable(float x, float z, float nearY)
    {
        float g = SurfaceBelow(x, z, nearY + BodyTop);
        return !float.IsNaN(g) && g >= WaterY - WadeLimit(x, z, g) && !InRoamBlock(x, z, g);
    }

    public bool CanTraverse(Vector3 from, float x, float z, bool jumping)
    {
        if (walkPolys.Count == 0) return true;
        if (FreeRoam) return RoamStep(from, x, z, jumping);
        if (!TryWalkableGround(x, z, out float ground)) return jumping && beachPoly != null && from.y >= beachPoly.y + 0.55f && IsDryBeachApproach(x, z);
        if (beachPoly == null) return true;
        // 跳下沙滩才进入低层。沙滩上的人不能从崖壁底部穿进上层地形。
        bool onBeach = from.y < beachPoly.y + 0.65f && Contains(beachPoly.points, from.x, from.z);
        if (IsBeachSurface(x, z, ground)) return onBeach || jumping;
        return !onBeach || ground <= from.y + 0.30f;
    }

    bool IsDryBeachApproach(float x, float z)
    {
        // 2026-11-14：原来水位一过 0.05 就不让跳下沙滩；现在沙滩只要还涉得过去（没过脚不超过 WadeDepth）就能下
        if (beachPoly?.points == null || beachPoly.points.Length < 3 || WaterY > beachPoly.y + WadeDepth) return false;
        // 北侧坡道与沙滩之间有一条低矮的干岸带，允许角色在空中越过这段间隙。
        const float MaxGap = 0.90f;
        var point = new Vector2(x, z);
        var vertices = beachPoly.points;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            var edge = vertices[i] - vertices[j];
            float t = Mathf.Clamp01(Vector2.Dot(point - vertices[j], edge) / Mathf.Max(edge.sqrMagnitude, 0.0001f));
            if ((point - vertices[j] - t * edge).sqrMagnitude >= MaxGap * MaxGap) continue;
            float ground = GroundAt(x, z);
            return ground > 0.05f && ground <= beachPoly.y + 0.10f;
        }
        return false;
    }

    public bool InWalkArea(float x, float z)
    {
        foreach (var p in walkPolys)
            if (Contains(p.points, x, z)) return true;
        return false;
    }

    public bool InLawnArea(float x, float z)
    {
        if (lawnPolys == null) return false;
        foreach (var p in lawnPolys)
            if (Contains(p.points, x, z)) return true;
        return false;
    }

    public float GroundAt(float x, float z)
    {
        return GroundAt(x, z, float.PositiveInfinity);
    }

    // aboveY：只认这个高度附近及以下的地面。塔顶平台在塔门正上方，
    // 不带上限的话从塔下打射线会直接打到 12 米高的平台，把人抬上去。
    public float GroundAt(float x, float z, float aboveY)
    {
        float best = -1f;
        var hits = Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f, groundMask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            float y = hit.point.y;
            if (y <= aboveY + 0.3f && y > best) best = y;
        }
        return best < -0.5f ? 0f : best;
    }

    static bool Contains(Vector2[] poly, float x, float z)
    {
        if (poly == null || poly.Length < 3) return false;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.y > z) != (b.y > z) &&
                x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }
}
