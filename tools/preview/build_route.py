"""路线表：生成 Resources/World/route.json。2026-11-07 按新地图（preview.png v2）重写。

python tools/preview/build_route.py

新地图里 preview_zones.json 的点位本身就是站位（门前约 1 米、塔下、码头上），STAND 直接取 world_layout 的点。
路径点沿路网手写，绕开建筑：路线只有一条，写死最确定。改完跑 tools/preview/route_check.py。
"""
import json, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from camera import PreviewCam

ROOT = Path(__file__).resolve().parents[2]
CAM = PreviewCam()
WORLD = ROOT / "Assets/TideTown/Data/Resources/World"
lay = json.loads((WORLD / "world_layout.json").read_text(encoding="utf-8"))
roads = {r["id"]: r for r in lay["roads"]}
pts = {p["id"]: (p["x"], p["z"]) for p in lay["points"]}


def node(rid, i):
    r = roads[rid]
    return (r["x"][i], r["z"][i])


def mid(a, b, t=0.5):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


STAND = {k: pts[k] for k in ["03", "07", "10", "11", "12", "13", "19", "20", "21", "22", "24", "25", "30", "31"]}
STAND["LOW"] = pts["LOW"]
STAND["04"] = node("mountain", 1)
STAND["05"] = node("mountain", 3)
STAND["06"] = node("mountain", 6)

def px(u, v, y=3.0):
    """路径点用图上像素写，经 PreviewCam 投到世界：改机位比例时自动跟着走"""
    return CAM.unproject(u, v, y)


# 主街临海步道上的几个路径点（都在建筑正面以南）
PO_W = px(250, 585)           # 邮局西南角外
MKT_W = px(790, 588)          # 集市街口西侧
MKT_E = px(950, 590)          # 集市街口东侧
PROM_E = px(1090, 580)        # 学校前、操场西南
SCH_E = px(1365, 482)         # 学校东南角外
SCH_NE = px(1385, 432)        # 学校东侧，去后门
UP = [node("mountain", 0)]
DOWN = [node("to_shed", i) for i in range(5)]
SHED = [node("to_shed", 5), node("to_shed", 6), px(1270, 712, 1.4)]   # 船棚西南角外，再沿门前走
SHED_SE = px(1418, 718, 1.4)   # 船棚东南角外
LH = [node("to_lighthouse", i) for i in range(5)] + [px(1556, 328, 5.0)]   # 上灯塔路，绕到塔门前（30）
LH_TOP = []   # 30（塔门）→ 31（塔脚东侧）沿塔前直走

# 28 站，顺序和旧路线一致（工程书 WP-06）。每一项：(站, 到这一站之前经过的路径点)
# 2026-11-08 第四轮：面包摊、狗窝按模型外形挡人了，路线从旁边绕
STALL_W, STALL_E = (-15.4, -9.3), (-13.2, -9.35)   # 面包摊北边走（摊子 x -14.9..-13.8，z -10.6..-10.0）
DOGHOUSE_E = (22.8, 1.95)                           # 狗窝东边绕到 20（狗窝 x 21.4..22.2，z 1.6..2.3）

# 第四轮：住宅区三条坡道（西、北、东）各走上去再下来，路线审计顺带验证住宅区真的走得上去
def _updown(rid):
    r = roads[rid]
    a, b = (r["x"][0], r["z"][0]), (r["x"][1], r["z"][1])
    L = ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5
    foot = (a[0] + (a[0] - b[0]) / L * 1.2, a[1] + (a[1] - b[1]) / L * 1.2)     # 坡脚外 1.2 m（广场上）
    n = len(r["x"])
    up = [foot] + [(r["x"][i], r["z"][i]) for i in range(n)]
    return up + [("PAD", rid)] + list(reversed(up))   # PAD：坡顶旁边最近的住宅组团石板路上的一点（下面 build 时找）
RES = _updown("branch_w") + _updown("branch_n") + _updown("branch_e")

LEGS = [
    ("07", []), ("03", [PO_W]), ("07", [PO_W]), ("10", [STALL_W, STALL_E]), ("11", [STALL_E]), ("12", []),
    ("13", [px(700, 590), MKT_W]),
    ("19", RES + [MKT_E, PROM_E]),
    ("20", [SCH_E, SCH_NE, DOGHOUSE_E]),
    ("21", [DOGHOUSE_E, SCH_NE, SCH_E]),
    ("22", [(20.6, -3.2)]),   # 从秋千滑梯北边绕过去
    ("LOW", [PROM_E, MKT_E] + DOWN),
    ("24", SHED), ("25", [SHED_SE]), ("24", [SHED_SE]),
    ("LOW", list(reversed(SHED))),
    ("22", list(reversed(DOWN)) + [MKT_E, PROM_E]),
    ("30", LH),
    ("31", LH_TOP),
    ("30", list(reversed(LH_TOP))),
    ("22", list(reversed(LH))),
    ("13", [PROM_E, MKT_E]),
    ("10", [MKT_W]),
    ("07", [STALL_E, STALL_W]),
    ("03", [PO_W]),
    ("04", UP),
    ("05", [node("mountain", 2)]),
    ("06", [node("mountain", 4), node("mountain", 5)]),
]

# 2026-11-08 第四轮：碰撞贴着模型外轮廓（building_footprints.json 的 circles，上一次 BuildMain 生成；
# 路线不影响场景，所以先建场景再跑这里不会互相打架）。路线点离墙、栏杆、道具不到身体半径的，往外推开，
# 不然角色走不到那个点（站位本来就在墙根、栏杆边上的，比如灯塔塔脚 31）
import math
_fp = json.loads((WORLD / "building_footprints.json").read_text(encoding="utf-8"))
CIRCLES = _fp.get("circles", [])
PLAT_R, CLEAR = 0.30, 0.12


def walk_y(x, z):
    best = None
    for a in lay["walk"]:
        xs, zs = a["x"], a["z"]
        inside, j = False, len(xs) - 1
        for i in range(len(xs)):
            if (zs[i] > z) != (zs[j] > z) and x < (xs[j] - xs[i]) * (z - zs[i]) / (zs[j] - zs[i]) + xs[i]:
                inside = not inside
            j = i
        if inside and (best is None or a["y"] > best):
            best = a["y"]
    return best


def nudge(x, z):
    gy = walk_y(x, z)
    if gy is None or not CIRCLES:
        return x, z, 0.0
    moved = 0.0
    for _ in range(12):
        push_x = push_z = 0.0
        for c in CIRCLES:
            if gy >= c["top"] - 0.03 or c["top"] - gy <= 0.22:
                continue
            dx, dz = x - c["x"], z - c["z"]
            d = math.hypot(dx, dz)
            need = c["r"] + PLAT_R + CLEAR
            if 1e-6 < d < need:
                push_x += dx / d * (need - d)
                push_z += dz / d * (need - d)
        if abs(push_x) + abs(push_z) < 1e-4:
            break
        L = math.hypot(push_x, push_z)
        step = min(L, 0.1)
        nx_, nz_ = x + push_x / L * step, z + push_z / L * step
        if walk_y(nx_, nz_) is None:
            break
        x, z = nx_, nz_
        moved += step
    return x, z, moved


_hash = {}
for _c in CIRCLES:
    _reach = _c["r"] + PLAT_R + CLEAR
    for gx in range(math.floor(_c["x"] - _reach), math.floor(_c["x"] + _reach) + 1):
        for gz in range(math.floor(_c["z"] - _reach), math.floor(_c["z"] + _reach) + 1):
            _hash.setdefault((gx, gz), []).append(_c)


def clear_at(x, z):
    gy = walk_y(x, z)
    if gy is None:
        return False
    for c in _hash.get((math.floor(x), math.floor(z)), ()):
        if gy >= c["top"] - 0.03 or c["top"] - gy <= 0.22:
            continue
        if math.hypot(x - c["x"], z - c["z"]) < c["r"] + PLAT_R + CLEAR:
            return False
    return True


def seg_clear(a, b):
    L = math.hypot(b[0] - a[0], b[1] - a[1])
    n = max(1, int(L / 0.05))
    return all(clear_at(a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n) for i in range(1, n))


def detour(a, b):
    """a→b 直线被挡：在两点周围 0.1 m 网格上找最短绕行，拉直后返回中间点"""
    import heapq
    G = 0.1
    x0, x1 = min(a[0], b[0]) - 3, max(a[0], b[0]) + 3
    z0, z1 = min(a[1], b[1]) - 3, max(a[1], b[1]) + 3
    def cell(p): return (round((p[0] - x0) / G), round((p[1] - z0) / G))
    def pos(c): return (x0 + c[0] * G, z0 + c[1] * G)
    sa, sb = cell(a), cell(b)
    nx_, nz_ = int((x1 - x0) / G) + 1, int((z1 - z0) / G) + 1
    ok = {}
    def free(c):
        if c in (sa, sb):
            return True
        if c not in ok:
            ok[c] = 0 <= c[0] < nx_ and 0 <= c[1] < nz_ and clear_at(*pos(c))
        return ok[c]
    dist, prev, pq = {sa: 0.0}, {}, [(0.0, sa)]
    while pq:
        d, c = heapq.heappop(pq)
        if c == sb:
            break
        if d > dist.get(c, 1e9):
            continue
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                if dx == dz == 0:
                    continue
                n = (c[0] + dx, c[1] + dz)
                if not free(n):
                    continue
                nd = d + math.hypot(dx, dz) * G + math.hypot(n[0] - sb[0], n[1] - sb[1]) * 0
                if nd < dist.get(n, 1e9):
                    dist[n] = nd
                    prev[n] = c
                    heapq.heappush(pq, (nd, n))
    if sb not in prev:
        return None
    path = [sb]
    while path[-1] != sa:
        path.append(prev[path[-1]])
    path = [pos(c) for c in reversed(path)]
    path[0], path[-1] = a, b
    # 拉直：能直连就跳过中间点
    out, i = [a], 0
    while i < len(path) - 1:
        j = len(path) - 1
        while j > i + 1 and not seg_clear(path[i], path[j]):
            j -= 1
        out.append(path[j])
        i = j
    return out[1:-1]


def pad_near(rid):
    """坡顶附近、离坡顶最近的一点：在 5.5 m 的石板路上（不在坡道上）、离墙够远"""
    r = roads[rid]
    cx, cz = r["x"][-1], r["z"][-1]
    best = None
    for i in range(-30, 31):
        for j in range(-30, 31):
            x, z = cx + i * 0.1, cz + j * 0.1
            d = math.hypot(x - cx, z - cz)
            if d < 1.2 or (best and d >= best[0]):
                continue
            ids = [a["id"] for a in lay["walk"] if a["y"] > 5.4 and walk_y(x, z) and abs(walk_y(x, z) - a["y"]) < 0.01]
            if not ids or not any(i_.startswith("pad_stone_5_5") for i_ in ids) or not clear_at(x, z):
                continue
            # 确认点不在坡道可走区里
            if any(a["id"].startswith(rid) and _inside(a, x, z) for a in lay["walk"]):
                continue
            best = (d, x, z)
    return (best[1], best[2]) if best else (cx, cz)


def _inside(a, x, z):
    xs, zs = a["x"], a["z"]
    ins, j = False, len(xs) - 1
    for i in range(len(xs)):
        if (zs[i] > z) != (zs[j] > z) and x < (xs[j] - xs[i]) * (z - zs[i]) / (zs[j] - zs[i]) + xs[i]:
            ins = not ins
        j = i
    return ins


route = []
nudged = []
for stop, via in LEGS:
    via = [pad_near(v[1]) if v[0] == "PAD" else v for v in via]
    for x, z in via:
        x, z, m = nudge(x, z)
        if m > 0.01:
            nudged.append(("via", round(m, 2)))
        route.append({"id": "via", "stop": False, "x": round(x, 3), "z": round(z, 3)})
    x, z, m = nudge(*STAND[stop])
    if m > 0.01:
        nudged.append((stop, round(m, 2)))
    route.append({"id": stop, "stop": True, "x": round(x, 3), "z": round(z, 3)})
if nudged:
    print("离墙推开：", nudged)
# 直线还被挡的段：自动找绕行点插进去
fixed_route, detours = [route[0]], []
for a, b in zip(route, route[1:]):
    pa, pb = (a["x"], a["z"]), (b["x"], b["z"])
    if CIRCLES and not seg_clear(pa, pb):
        vias = detour(pa, pb)
        if vias is None:
            print(f"  找不到绕行：{a['id']}→{b['id']}")
        else:
            detours.append((a["id"], b["id"], len(vias)))
            for x, z in vias:
                fixed_route.append({"id": "via", "stop": False, "x": round(x, 3), "z": round(z, 3)})
    fixed_route.append(b)
route = fixed_route
if detours:
    print("自动绕行：", detours)
(WORLD / "route.json").write_text(json.dumps({"route": route}, ensure_ascii=False, indent=1), encoding="utf-8")
stops = sum(1 for r in route if r["stop"])
assert stops == 28, stops
print(f"route.json: {stops} stops, {len(route)} waypoints")
