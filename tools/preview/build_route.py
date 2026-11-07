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
LEGS = [
    ("07", []), ("03", [PO_W]), ("07", [PO_W]), ("10", []), ("11", []), ("12", []),
    ("13", [px(700, 590), MKT_W]),
    ("19", [MKT_E, PROM_E]),
    ("20", [SCH_E, SCH_NE]),
    ("21", [SCH_NE, SCH_E]),
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
    ("07", []),
    ("03", [PO_W]),
    ("04", UP),
    ("05", [node("mountain", 2)]),
    ("06", [node("mountain", 4), node("mountain", 5)]),
]

route = []
for stop, via in LEGS:
    for x, z in via:
        route.append({"id": "via", "stop": False, "x": round(x, 3), "z": round(z, 3)})
    x, z = STAND[stop]
    route.append({"id": stop, "stop": True, "x": round(x, 3), "z": round(z, 3)})
(WORLD / "route.json").write_text(json.dumps({"route": route}, ensure_ascii=False, indent=1), encoding="utf-8")
stops = sum(1 for r in route if r["stop"])
assert stops == 28, stops
print(f"route.json: {stops} stops, {len(route)} waypoints")
