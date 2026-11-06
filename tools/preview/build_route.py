"""WP-06 路线表：生成 Resources/World/route.json。

python tools/preview/build_route.py

两件事分开：
  预览图上的编号是标签，贴在建筑上，只用来对布局（preview_zones.json 的 points，5.7 布局项查它）。
  STAND 是角色真正站的位置（门前、塔下），互动点和路线审计都用它。
路径点是手写的，沿路网走，不做寻路：路线只有一条，写死最确定。
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORLD = ROOT / "Assets/TideTown/Data/Resources/World"
lay = json.loads((WORLD / "world_layout.json").read_text(encoding="utf-8"))
roads = {r["id"]: r for r in lay["roads"]}


def node(rid, i):
    r = roads[rid]
    return (r["x"][i], r["z"][i])


STAND = {
    "07": (-17.10, -6.20), "03": (-25.20, 0.60), "10": (-11.64, -7.00), "11": (-6.54, -5.60), "12": (-3.63, -5.30),
    "13": (0.03, -1.26), "19": (6.60, -3.52), "20": (13.50, 3.40), "21": (10.01, -4.62), "22": (15.45, -0.97),
    "LOW": (15.53, -6.60), "24": (17.70, -6.45), "25": (19.60, -6.45), "30": (21.50, 1.45), "31": (24.00, 5.40),
    "04": node("mountain", 1), "05": node("mountain", 5), "06": (-23.40, 12.65),
}
E = (13.40, -2.60)                # 操场东侧
MAIN_E = (11.00, -7.55)           # 主街东南角，下坡起点
W1, W2 = (-19.90, -5.35), (-22.60, -3.00)  # 邮局西南角外
SB0, SB1, SB2 = (14.50, -1.20), node("to_school_back", 0), node("to_school_back", 1)
DOWN = [MAIN_E, node("to_shed", 1), (14.40, -6.50)]
LH = [node("to_lighthouse", 1), node("to_lighthouse", 2), (19.60, 1.55)]
UP = [(-23.10, 2.20), node("mountain", 0)]
BACK = list(reversed(DOWN))

# 工程书 WP-06 的 28 站。每一项：(站, 到这一站之前经过的路径点)
LEGS = [
    ("07", []), ("03", [W1, W2]), ("07", [W2, W1]), ("10", []), ("11", []), ("12", []), ("13", []),
    ("19", [(1.00, -2.40)]),
    ("20", [E, SB0, SB1, SB2]),
    ("21", [SB2, SB1, SB0, E]),
    ("22", [E]),
    ("LOW", [E] + DOWN),
    ("24", []), ("25", []), ("24", []),
    ("LOW", []),
    ("22", BACK + [E]),
    ("30", LH),
    ("31", [(23.40, 1.60)]),
    ("30", [(23.40, 1.60)]),
    ("22", list(reversed(LH))),
    ("13", [E, (1.00, -2.40)]),
    ("10", [(-2.00, -5.30)]),
    ("07", []),
    ("03", [W1, W2]),
    ("04", UP),
    ("05", [node("mountain", 2), node("mountain", 3), node("mountain", 4)]),
    ("06", [node("mountain", 6), node("mountain", 7), node("mountain", 8), node("mountain", 9)]),
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
