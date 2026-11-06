"""WP-06 离线路线检查：不开 Unity，几秒钟查完整条路线。

python tools/preview/route_check.py

读 world_layout.json、route.json、building_footprints.json，沿路线每 0.1 米采样，查三件事：
  1. 在可走区域内（和 IslandWorld.Walkable 同一套多边形）
  2. 不进建筑碰撞（footprint 外扩角色半径 0.42 米，和 EggboyPlayer.Move 一致）
  3. 地面没有突变（相邻采样点高差超过 0.14 米就算跳变，和 EggboyPlayer.Step 一致），采样 2 厘米，几厘米宽的缝也跑不掉
  4. 把所有可走区域铺成 10 厘米网格，查路线之外玩家也可能踩到的洞

全过才值得去 Unity 里用真实 Move 走。任何一项不过，退出码 1。
"""
import json, math, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORLD = ROOT / "Assets/TideTown/Data/Resources/World"
RADIUS = 0.42
STEP = 0.14
SAMPLE = 0.02
GRID = 0.1
ROAD_EXT = 0.15


def contains(xs, zs, x, z):
    inside = False
    j = len(xs) - 1
    for i in range(len(xs)):
        if (zs[i] > z) != (zs[j] > z) and x < (xs[j] - xs[i]) * (z - zs[i]) / (zs[j] - zs[i]) + xs[i]:
            inside = not inside
        j = i
    return inside


class World:
    def __init__(self):
        self.lay = json.loads((WORLD / "world_layout.json").read_text(encoding="utf-8"))
        self.boxes = json.loads((WORLD / "building_footprints.json").read_text(encoding="utf-8"))["buildings"]
        self.segs = []
        for r in self.lay["roads"]:
            def slope(i):
                # 第 i 段每水平走一米的高度变化；路的两头之外算平的（和 IslandBuilder.NeighbourSlope 一致）
                if i < 0 or i + 1 >= len(r["x"]):
                    return 0.0
                L = math.hypot(r["x"][i + 1] - r["x"][i], r["z"][i + 1] - r["z"][i])
                return 0.0 if L < 1e-3 else (r["y"][i + 1] - r["y"][i]) / L
            for i in range(len(r["x"]) - 1):
                self.segs.append((r["id"], r["x"][i], r["y"][i], r["z"][i], r["x"][i + 1], r["y"][i + 1], r["z"][i + 1], r["width"], slope(i - 1), slope(i + 1)))

    def walkable(self, x, z):
        return any(contains(a["x"], a["z"], x, z) for a in self.lay["walk"])

    def ground(self, x, z):
        """和 IslandWorld.GroundAt 一样：从上往下打，取最高的 Ground 面"""
        best, name = 0.0, "none"
        for t in self.lay["tiers"]:
            if contains(t["x"], t["z"], x, z) and t["y"] > best:
                best, name = t["y"], t["id"]
        c = self.lay.get("coast")
        if c and contains(c["x"], c["z"], x, z) and 0.15 > best:
            best, name = 0.15, "coast"
        for rid, x0, y0, z0, x1, y1, z1, w, s_prev, s_next in self.segs:
            dx, dz = x1 - x0, z1 - z0
            L2 = dx * dx + dz * dz
            if L2 < 1e-6:
                continue
            s = ((x - x0) * dx + (z - z0) * dz) / L2
            ext = ROAD_EXT / math.sqrt(L2)  # 路面两端多 15 cm，和 IslandBuilder.RoadMesh 一致
            if s < -ext or s > 1 + ext:
                continue
            L = math.sqrt(L2)
            if abs(-(x - x0) * dz + (z - z0) * dx) / math.sqrt(L2) > w / 2:
                continue
            # 两端盖片顺着相邻段的坡度走
            if s < 0:
                y = y0 + s_prev * s * L + 0.02
            elif s > 1:
                y = y1 + s_next * (s - 1) * L + 0.02
            else:
                y = y0 + (y1 - y0) * s + 0.02
            if y > best:
                best, name = y, "road:" + rid
        return best, name

    def blocked(self, x, z):
        for b in self.boxes:
            if abs(x - b["x"]) < b["w"] / 2 + RADIUS and abs(z - b["z"]) < b["d"] / 2 + RADIUS:
                return b["instance"]
        return None


def grid_holes(w):
    """可走区域内，落到岸线高度的格子是洞；相邻两格高差超过一步的是边（只提醒，不算不过）。"""
    xs = [v for a in w.lay["walk"] for v in a["x"]]
    zs = [v for a in w.lay["walk"] for v in a["z"]]
    nx, nz = int((max(xs) - min(xs)) / GRID) + 1, int((max(zs) - min(zs)) / GRID) + 1
    cells, holes, edges = {}, [], []
    for iz in range(nz):
        z = min(zs) + iz * GRID
        for ix in range(nx):
            x = min(xs) + ix * GRID
            if not w.walkable(x, z) or w.blocked(x, z):
                continue
            g, name = w.ground(x, z)
            cells[(ix, iz)] = g
            if name in ("coast", "none"):
                holes.append((x, z))
    for (ix, iz), g in cells.items():
        for n in ((ix + 1, iz), (ix, iz + 1)):
            if n in cells and abs(cells[n] - g) > STEP:
                edges.append((min(xs) + ix * GRID, min(zs) + iz * GRID, g, cells[n]))
    return {"cells": len(cells), "holes": holes, "edges": edges}


def main():
    w = World()
    route = json.loads((WORLD / "route.json").read_text(encoding="utf-8"))["route"]
    problems, total, prev = [], 0.0, None
    last_stop = route[0]["id"]
    stops = 0
    max_step = 0.0
    for a, b in zip(route, route[1:]):
        dx, dz = b["x"] - a["x"], b["z"] - a["z"]
        L = math.hypot(dx, dz)
        n = max(1, int(L / SAMPLE))
        for i in range(n + 1):
            x, z = a["x"] + dx * i / n, a["z"] + dz * i / n
            where = f"{last_stop}→{b['id']} ({x:.2f},{z:.2f})"
            if not w.walkable(x, z):
                problems.append(("不可走", where))
            hit = w.blocked(x, z)
            if hit:
                problems.append(("撞建筑 " + hit, where))
            g, name = w.ground(x, z)
            if prev is not None:
                d = abs(g - prev[0])
                max_step = max(max_step, d)
                if d > STEP:
                    problems.append((f"地面跳变 {prev[0]:.2f}→{g:.2f}（{prev[1]}→{name}）", where))
            prev = (g, name)
        total += L
        if b.get("stop"):
            last_stop = b["id"]
            stops += 1
    # 真实角色转弯会抄近路，不走中线：再查左右偏 30 厘米的两条平行线（只查高差，只作提醒，最终以真实 Move 审计为准）
    warnings = []
    for off in (-0.3, 0.3):
        for a, b in zip(route, route[1:]):
            dx, dz = b["x"] - a["x"], b["z"] - a["z"]
            L = math.hypot(dx, dz)
            if L < 0.01:
                continue
            nx_, nz_ = -dz / L * off, dx / L * off
            n = max(1, int(L / SAMPLE))
            prev2 = None
            for i in range(n + 1):
                x, z = a["x"] + dx * i / n + nx_, a["z"] + dz * i / n + nz_
                if not w.walkable(x, z) or w.blocked(x, z):
                    prev2 = None
                    continue
                g, name = w.ground(x, z)
                if prev2 is not None and abs(g - prev2[0]) > STEP:
                    warnings.append(f"偏线高差 {prev2[0]:.2f}→{g:.2f}（{prev2[1]}→{name}） @ {a['id']}→{b['id']} ({x:.2f},{z:.2f})")
                prev2 = (g, name)
    print(f"路线 {total:.1f} 米，{stops + 1} 个停靠点，{len(route)} 个路径点，最大地面变化 {max_step:.3f} 米/{SAMPLE} 米")
    seen = set()
    for kind, where in problems:
        key = kind.split("（")[0] + where.split(" ")[0]
        if key not in seen:
            seen.add(key)
            print("  ", kind, "@", where)
    for m in warnings:
        print("   提醒", m)
    holes = grid_holes(w)
    print(f"可走区域网格：{holes['cells']} 格，掉到岸线的洞 {len(holes['holes'])} 处，平台和坡道之间高差过大的边 {len(holes['edges'])} 处")
    for x, z in holes["holes"][:8]:
        print(f"   洞 @ ({x:.2f},{z:.2f})")
    for x, z, a_, b_ in holes["edges"][:8]:
        print(f"   高差 {a_:.2f}→{b_:.2f} @ ({x:.2f},{z:.2f})")
    ok = not problems and not holes["holes"]
    print("OFFLINE_PASS" if ok else f"OFFLINE_FAIL 路线 {len(problems)} 个采样点、可走区域 {len(holes['holes'])} 个洞")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
