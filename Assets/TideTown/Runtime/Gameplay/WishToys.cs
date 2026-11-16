// 2026-11-16 用户议题 4：心愿便笺（V）造出来的玩具 / 礼物 / 纪念品（渡口小船除外）
//   1. 有贴合外形的碰撞：按模型网格在运行时扫一遍 5 cm 网格，外轮廓每 10 cm 一个带顶面的小圆，注册进 IslandWorld（和场景轮廓碰撞同一套）。
//      比 EGGBOY 能直接迈过去的高度（0.22 m）还矮的小东西不挡人，走过去会被脚踢开滚一小段。
//      生成位置避开主线路线走廊（route.json 两侧 1 m + 物件半径）、别的碰撞体、深水，找不到才退回原位。
//   2. 走近出现 [E] 拿起：走现成的真实接触拾取（PhysicalPickupController，转身 → 走到跟前 → 伸手碰到那一帧才拿起），
//      抱在胸前，然后打开 3D 端详（WishInspectModal：360° 转动、滚轮远近、心愿铭牌 + 一段温情描述）。
//   3. 端详完：放回原处，或收进邮差包（容量 6）。心愿笺（V）里可以从邮差包里取出最近收的一件，放在脚边。
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public sealed class WishToy : MonoBehaviour
{
    public static readonly List<WishToy> All = new List<WishToy>();
    public const float KickHeight = 0.22f;   // = EggboyPlayer.Step：再矮的东西人直接迈过去

    public string description, category, recipient, blurb;
    public int number;
    public Lux3DSpawn spawn;
    public bool bagAllowed = true;

    public float Height { get; private set; }
    public float Radius { get; private set; }
    public bool Held { get; private set; }
    public bool InBag { get; private set; }
    public bool Busy { get; set; }
    public int ColliderCount => cols.Count;
    public bool Kickable => Height < KickHeight;
    public bool CanPick => !Held && !InBag && !Busy && gameObject.activeInHierarchy;

    IslandWorld world;
    EggboyPlayer player;
    readonly List<EggboyCollider> cols = new List<EggboyCollider>();
    Vector3 homePos, homeScale; Quaternion homeRot; Transform homeParent;
    Vector3 kickVel;
    bool autoPlace;
    float placeRadius;

    public void Init(Lux3DSpawn s, string desc, string cat, string to, int num, IslandWorld w, EggboyPlayer p, bool place, float maxShift)
    {
        spawn = s; description = desc; category = cat; recipient = to; number = num; world = w; player = p;
        autoPlace = place; placeRadius = maxShift;
        All.Add(this);
        homeParent = transform.parent; homeScale = transform.localScale;
        if (autoPlace) Relocate();
        RememberHome();
        Rebuild();
        if (s != null) s.onSwapped += _ => { if (!Held && !InBag) { if (autoPlace) Relocate(); RememberHome(); Rebuild(); } };
        blurb = WishBlurb.Template(this);
        WishBlurb.RequestFromRelay(this);
        var svc = FindObjectOfType<InteractionService>();
        if (svc != null) svc.Register(new WishToyInteractable(this));
    }

    void OnDestroy() { All.Remove(this); ClearColliders(); }

    Transform Model => transform.Find("Model");
    public Bounds WorldBounds()
    {
        var m = Model;
        return Lux3DModelFit.WorldBounds(m != null ? m.gameObject : gameObject);
    }

    void RememberHome() { homePos = transform.position; homeRot = transform.rotation; }

    float GroundUnder(Vector3 p, float near) => world != null ? world.GroundAt(p.x, p.z, near) : p.y;

    // ---------- 碰撞 ----------
    void ClearColliders()
    {
        if (world != null) foreach (var c in cols) world.RemoveCollider(c);
        cols.Clear();
    }

    public void Rebuild()
    {
        ClearColliders();
        if (world == null || Held || InBag || !gameObject.activeInHierarchy) return;
        var b = WorldBounds();
        float ground = GroundUnder(b.center, b.min.y + 0.3f);
        Height = b.max.y - ground;
        Radius = Mathf.Max(b.extents.x, b.extents.z);
        if (Height < KickHeight) return;

        const float Cell = 0.05f, BandLo = 0.02f, BandHi = 1.3f;
        float x0 = b.min.x - Cell * 2f, z0 = b.min.z - Cell * 2f;
        int nx = Mathf.CeilToInt((b.size.x + Cell * 4f) / Cell), nz = Mathf.CeilToInt((b.size.z + Cell * 4f) / Cell);
        if (nx * nz > 400000) { nx = Mathf.Min(nx, 600); nz = Mathf.Min(nz, 600); }
        var solid = new bool[nx * nz]; var top = new float[nx * nz];
        for (int i = 0; i < top.Length; i++) top[i] = float.NegativeInfinity;
        var root = Model != null ? Model : transform;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh; if (mesh == null || !mesh.isReadable) continue;
            var m = mf.transform.localToWorldMatrix;
            var v = mesh.vertices; var w = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) w[i] = m.MultiplyPoint3x4(v[i]);
            var t = mesh.triangles;
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                Vector3 a = w[t[k]], bb = w[t[k + 1]], c = w[t[k + 2]];
                float len = Mathf.Max((bb - a).magnitude, Mathf.Max((c - bb).magnitude, (a - c).magnitude));
                int n = Mathf.Clamp(Mathf.CeilToInt(len / (Cell * 0.5f)), 1, 60);
                for (int i = 0; i <= n; i++)
                    for (int j = 0; j <= n - i; j++)
                    {
                        var p = a + (bb - a) * (i / (float)n) + (c - a) * (j / (float)n);
                        float h = p.y - ground; if (h < BandLo || h > BandHi) continue;
                        int ix = Mathf.FloorToInt((p.x - x0) / Cell), iz = Mathf.FloorToInt((p.z - z0) / Cell);
                        if (ix < 0 || iz < 0 || ix >= nx || iz >= nz) continue;
                        int id = iz * nx + ix; solid[id] = true; if (p.y > top[id]) top[id] = p.y;
                    }
            }
        }
        // 外沿格子（四邻有空格）按 2×2 合成一个圆：圆心在模型外表面上，半径同场景轮廓碰撞（0.148），顶面 = 那一片最高点
        var buckets = new Dictionary<int, (Vector2 sum, int n, float top)>();
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                int id = iz * nx + ix; if (!solid[id]) continue;
                bool edge = ix == 0 || iz == 0 || ix == nx - 1 || iz == nz - 1 || !solid[id - 1] || !solid[id + 1] || !solid[id - nx] || !solid[id + nx];
                if (!edge) continue;
                int key = (iz / 2) * 100000 + ix / 2;
                var pos = new Vector2(x0 + (ix + .5f) * Cell, z0 + (iz + .5f) * Cell);
                float tp = Mathf.Max(top[id], ground + 0.32f);
                buckets[key] = buckets.TryGetValue(key, out var bk) ? (bk.sum + pos, bk.n + 1, Mathf.Max(bk.top, tp)) : (pos, 1, tp);
            }
        foreach (var kv in buckets)
        {
            var c = kv.Value.sum / kv.Value.n;
            var col = new EggboyCollider { x = c.x, z = c.y, r = 0.148f, top = kv.Value.top };
            cols.Add(col); world.AddCollider(col);
        }
        // 太细碎的模型（扫不出东西）：退回一个包住它的圆
        if (cols.Count == 0)
        {
            var col = new EggboyCollider { x = b.center.x, z = b.center.z, r = Mathf.Max(0.05f, Radius - 0.25f), top = b.max.y };
            cols.Add(col); world.AddCollider(col);
        }
    }

    // ---------- 摆放：不堵主线 ----------
    static List<Vector2> routePts;
    static void LoadRoute()
    {
        if (routePts != null) return;
        routePts = new List<Vector2>();
        var a = Resources.Load<TextAsset>("World/route");
        if (a == null) return;
        var doc = JsonUtility.FromJson<RouteDocW>(a.text);
        if (doc?.route != null) foreach (var p in doc.route) routePts.Add(new Vector2(p.x, p.z));
    }
    [Serializable] class RoutePtW { public float x, z; }
    [Serializable] class RouteDocW { public RoutePtW[] route; }

    public static float RouteDistance(Vector2 q)
    {
        LoadRoute();
        float best = float.MaxValue;
        for (int i = 0; i + 1 < routePts.Count; i++)
        {
            var a = routePts[i]; var ab = routePts[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            best = Mathf.Min(best, (a + ab * t - q).magnitude);
        }
        return best;
    }

    bool SpotOk(Vector3 p, float radius)
    {
        if (world == null) return true;
        if (RouteDistance(new Vector2(p.x, p.z)) < 1.0f + radius) return false;
        float g = GroundUnder(p, p.y + 0.6f);
        bool stand = world.FreeRoam ? world.RoamStandable(p.x, p.z, g) : world.Walkable(p.x, p.z);
        if (!stand || Mathf.Abs(g - p.y) > 0.6f) return false;
        foreach (var c in world.Colliders)
        {
            if (c.water || cols.Contains(c)) continue;
            float ext = c.r ?? Mathf.Max(c.hw ?? 0f, c.hd ?? 0f);
            if (new Vector2(p.x - c.x, p.z - c.z).magnitude < ext + radius + 0.15f) return false;
        }
        return true;
    }

    void Relocate()
    {
        var b = WorldBounds();
        float radius = Mathf.Max(0.1f, Mathf.Max(b.extents.x, b.extents.z));
        Vector3 start = transform.position;
        if (SpotOk(start, radius)) return;
        Vector3 toward = player != null ? (start - player.transform.position) : Vector3.forward; toward.y = 0f;
        float baseAng = Mathf.Atan2(toward.z, toward.x);
        for (float r = 0.3f; r <= placeRadius + 1e-3f; r += 0.25f)
            for (int k = 0; k < 16; k++)
            {
                float ang = baseAng + ((k % 2 == 0) ? 1 : -1) * (k / 2) * Mathf.PI / 8f;
                var q = start + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                q.y = GroundUnder(q, start.y + 0.6f);
                if (!SpotOk(q, radius)) continue;
                transform.position = q;
                return;
            }
    }

    // ---------- 踢开 ----------
    void Update()
    {
        if (world == null || player == null || Held || InBag) return;
        if (Kickable)
        {
            var pp = player.transform.position; var tp = transform.position;
            var d = new Vector2(tp.x - pp.x, tp.z - pp.z);
            float speed = new Vector2(player.vel.x, player.vel.z).magnitude;
            if (d.magnitude < 0.42f + Radius * 0.6f && speed > 0.2f && kickVel.sqrMagnitude < 0.01f && Mathf.Abs(tp.y - pp.y) < 0.4f)
            {
                var dir = d.sqrMagnitude > 1e-4f ? d.normalized : new Vector2(player.vel.x, player.vel.z).normalized;
                kickVel = new Vector3(dir.x, 0f, dir.y) * (0.9f + speed * 0.45f);
                ShadowAndChimeSystem.PlayProceduralChime(523.25f, 0.06f);
            }
        }
        if (kickVel.sqrMagnitude > 0.0004f)
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            var next = transform.position + kickVel * dt;
            float g = GroundUnder(next, transform.position.y + 0.25f);
            bool ok = Mathf.Abs(g - transform.position.y) < 0.2f && (world.FreeRoam ? world.RoamStandable(next.x, next.z, g) : world.Walkable(next.x, next.z));
            if (!ok) kickVel = Vector3.zero;
            else
            {
                next.y = g; transform.position = next;
                var axis = Vector3.Cross(Vector3.up, kickVel.normalized);
                transform.Rotate(axis, kickVel.magnitude * dt / Mathf.Max(0.05f, Height * 0.5f) * Mathf.Rad2Deg, Space.World);
                kickVel *= Mathf.Exp(-3.2f * dt);
            }
            if (kickVel.sqrMagnitude <= 0.0004f) { kickVel = Vector3.zero; RememberHome(); }
        }
    }

    // ---------- 拿起 / 放回 / 收起 ----------
    public Vector3 GrabPoint()
    {
        var b = WorldBounds();
        var pp = player != null ? player.transform.position : b.center;
        float y = Mathf.Min(b.center.y, GroundUnder(b.center, b.min.y + 0.3f) + 0.38f);
        var c = new Vector3(Mathf.Clamp(pp.x, b.min.x, b.max.x), y, Mathf.Clamp(pp.z, b.min.z, b.max.z));
        return Vector3.Lerp(c, new Vector3(b.center.x, y, b.center.z), 0.5f);
    }

    public void BeginPick()
    {
        if (!CanPick || player == null) return;
        Busy = true;
        var b = WorldBounds();
        bool high = b.center.y - player.transform.position.y > 0.45f;
        string clip = high ? "inspect_memory" : "pickup_drop";
        var pk = PhysicalPickupController.Instance;
        if (pk != null && pk.Active(clip))
        {
            bool ok = pk.Begin(new PhysicalPickupController.Job
            {
                clip = clip,
                target = GrabPoint,
                standDistance = Mathf.Clamp(Radius + 0.32f, 0.42f, 0.95f),
                contactTime = 0.4f,
                tolerance = 0.10f,
                reach = true,
                onContact = Attach,
                onDone = () => WishInspectModal.Open(this, player),
            });
            if (ok) return;
        }
        Attach();
        WishInspectModal.Open(this, player);
    }

    // 抱在胸前：太大的东西抱着时缩到 0.32 m 以内（放回时恢复）
    void Attach()
    {
        if (Held || player == null) return;
        ClearColliders();
        kickVel = Vector3.zero;
        Held = true;
        var anchor = player.CarryAnchor != null ? player.CarryAnchor : player.transform;
        var b = WorldBounds();
        float maxSize = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        transform.SetParent(anchor, true);
        if (maxSize > 0.32f) transform.localScale *= 0.32f / maxSize;
        var nb = WorldBounds();
        transform.position += anchor.position + anchor.forward * 0.04f - nb.center;
        player.SetCarryPose(EggboyPlayer.CarryPose.Ball);
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.12f);
    }

    public void PutBack()
    {
        Busy = false;
        if (player != null) player.SetCarryPose(EggboyPlayer.CarryPose.None);
        StartCoroutine(FlyHome());
    }

    IEnumerator FlyHome()
    {
        Vector3 from = transform.position; Quaternion fromRot = transform.rotation; Vector3 fromScale = transform.lossyScale;
        transform.SetParent(homeParent, true);
        if (player != null && player.HasClip("pickup_drop")) player.PlayEmote("pickup_drop");
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
            transform.position = Vector3.Lerp(from, homePos, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.18f;
            transform.rotation = Quaternion.Slerp(fromRot, homeRot, k);
            transform.localScale = Vector3.Lerp(homeScale * (fromScale.x / Mathf.Max(1e-4f, LossyX(homeScale))), homeScale, k);
            yield return null;
        }
        transform.SetPositionAndRotation(homePos, homeRot);
        transform.localScale = homeScale;
        Held = false;
        Rebuild();
    }

    float LossyX(Vector3 local) => homeParent != null ? local.x * homeParent.lossyScale.x : local.x;

    public void StoreInBag()
    {
        Busy = false;
        if (player != null) player.SetCarryPose(EggboyPlayer.CarryPose.None);
        transform.SetParent(homeParent, true);
        transform.localScale = homeScale;
        Held = false;
        InBag = true;
        ClearColliders();
        gameObject.SetActive(false);
        WishBag.Add(this);
    }

    public void TakeOutAt(Vector3 at, float yawDeg)
    {
        InBag = false;
        gameObject.SetActive(true);
        transform.SetParent(homeParent, true);
        transform.localScale = homeScale;
        transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yawDeg, 0f));
        autoPlace = true; placeRadius = 2.5f;
        Relocate();
        RememberHome();
        Rebuild();
    }
}

public sealed class WishToyInteractable : IInteractable
{
    readonly WishToy toy;
    public WishToyInteractable(WishToy t) { toy = t; }
    public string EntityId => "wish_toy_" + toy.number;
    public Vector3 Position => toy != null && toy.gameObject.activeInHierarchy ? toy.WorldBounds().center : new Vector3(0f, -999f, 0f);
    public string Prompt(GameState s) => toy != null && toy.CanPick ? "E　拿起「" + toy.description + "」" : "";
    public bool CanInteract(GameState s) => toy != null && toy.CanPick && !WishInspectModal.IsOpen && !ObjectInspectModal.IsOpen;
    public void Interact(GameState s, InteractionService svc) { if (toy != null) toy.BeginPick(); }
}

// 邮差包：装得下 Capacity 件；收进去的东西只是藏起来（同一局里还能取出来）
public static class WishBag
{
    public const int Capacity = 6;
    static readonly List<WishToy> items = new List<WishToy>();
    public static int Count { get { items.RemoveAll(t => t == null); return items.Count; } }
    public static bool Full => Count >= Capacity;
    public static IReadOnlyList<WishToy> Items => items;
    public static void Add(WishToy t) { if (t != null && !items.Contains(t)) items.Add(t); }
    public static string Summary()
    {
        items.RemoveAll(t => t == null);
        if (items.Count == 0) return "邮差包里还空着（0/" + Capacity + "）";
        var names = new List<string>(); foreach (var t in items) names.Add("「" + t.description + "」");
        return "邮差包（" + items.Count + "/" + Capacity + "）：" + string.Join("、", names);
    }
    // 取出最近收进去的一件，放在主角脚前
    public static WishToy TakeOutLatest(EggboyPlayer p)
    {
        items.RemoveAll(t => t == null);
        if (items.Count == 0 || p == null) return null;
        var t = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
        var fwd = new Vector3(Mathf.Sin(p.yaw), 0f, Mathf.Cos(p.yaw));
        var at = p.transform.position + fwd * 1.1f;
        var w = UnityEngine.Object.FindObjectOfType<IslandWorld>();
        if (w != null) at.y = w.GroundAt(at.x, at.z, p.transform.position.y + 0.6f);
        t.TakeOutAt(at, p.yaw * Mathf.Rad2Deg + 180f);
        return t;
    }
}

// 心愿铭牌上的那段话：默认本地模板拼；Lux3D 走 relay 时顺便请转发服务现写一句（3 秒拿不到就用模板）
public static class WishBlurb
{
    public static string CategoryName(string cat) =>
        cat == "food" ? "给小黑的点心" : cat == "toy" ? "玩具" : cat == "fire" ? "炉边的柴" : cat == "keepsake" ? "纪念品" : "心愿";

    public static string Template(WishToy t)
    {
        string d = t.description;
        int seed = Mathf.Abs(d.GetHashCode()) % 3;
        switch (t.category)
        {
            case "food":
                return new[]
                {
                    "小黑闻了三圈，尾巴先替它说了喜欢。「" + d + "」放在碗边，像阿萤以前放学顺路带回来的那样。",
                    "「" + d + "」还带着一点心愿的温度。小黑没急着吃，先把它推到你脚边——它想和你分。",
                    "有人记得它爱吃什么，这件事本身就很好吃。「" + d + "」，送给守着空学校的小黑。",
                }[seed];
            case "toy":
                return new[]
                {
                    "「" + d + "」在草地上一滚，小黑就追了出去。空了很久的操场，又有了一点吵闹。",
                    "玩具不用很新，只要有人陪着玩。「" + d + "」是你替阿萤留下的那一份陪伴。",
                    "邮差包里多了一件不用投递的东西：「" + d + "」。它只负责让谁开心一下。",
                }[seed];
            case "fire":
                return new[]
                {
                    "「" + d + "」劈开的纹路还新。林阿婆的烤炉认得这种干柴，一点就旺。",
                    "炉膛暖了，面包房就还像个家。「" + d + "」是今晚的第一把火。",
                    "柴火不说话，只是把屋子烘得暖暖的。「" + d + "」，送给还没冷透的面包房。",
                }[seed];
            default:
                return new[]
                {
                    "「" + d + "」会一直摆在潮汐镇，等搬走的人哪天回来，看见有人替他们记着这里。",
                    "它不是谁的旧物，是你给这座空镇子的新回忆：「" + d + "」。",
                    "海水涨上来以前，镇上又多了一件好看的东西。「" + d + "」，邮差 EGGBOY 留。",
                }[seed];
        }
    }

    [Serializable] class Req { public string text; public string category; }
    [Serializable] class Resp { public string blurb; }
    public static void RequestFromRelay(WishToy t)
    {
        var svc = Lux3DService.Instance;
        if (svc == null || string.IsNullOrEmpty(svc.RelayUrl) || t == null) return;
        t.StartCoroutine(Fetch(t, svc.RelayUrl.TrimEnd('/') + "/wish-blurb"));
    }
    static IEnumerator Fetch(WishToy t, string url)
    {
        var body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Req { text = t.description, category = t.category }));
        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success || t == null) yield break;
            var r = JsonUtility.FromJson<Resp>(req.downloadHandler.text);
            if (r != null && !string.IsNullOrWhiteSpace(r.blurb) && r.blurb.Length <= 120) t.blurb = r.blurb.Trim();
        }
    }
}
