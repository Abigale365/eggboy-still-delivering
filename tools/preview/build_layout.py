"""WP-04：把图上描的分区变成世界数据。不需要 Unity。

python tools/preview/build_layout.py [--extract-land]

输入  Assets/TideTown/Data/preview_zones.json、Resources/World/preview_camera.json
输出  Assets/TideTown/Data/Resources/World/world_layout.json   点位、建筑、机位、高度网格信息（运行时读）
      Assets/TideTown/Data/Resources/World/island_height.bytes float32 高度网格（运行时读）
      Assets/TideTown/Data/Resources/World/island_walk.bytes   uint8 可走网格（运行时读）
      Assets/TideTown/Data/island_zone.bytes                   uint8 地表分类（编辑器生成网格用）
      docs/reference/zones_overlay.jpg                         叠回预览图的检查图
"""
import argparse, json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage, sparse
from scipy.sparse.linalg import spsolve

sys.path.insert(0, str(Path(__file__).parent))
from camera import PreviewCam, ROOT

DATA = ROOT / "Assets/TideTown/Data"
WORLD = DATA / "Resources/World"
PREVIEW = ROOT / "docs/reference/preview.png"
CELL = 0.4
MARGIN = 5.0
SEA_FLOOR = -2.0
SHORE_Y = 0.15
ZONE = {"sea": 0, "rock": 1, "grass": 2, "stone": 3, "sand": 4}


def extract_land(zones):
    """从海色提取陆地轮廓：和图边相连的蓝色或白沫是海，其余是陆地。"""
    import cv2
    rgb = np.asarray(Image.open(PREVIEW).convert("RGB")).astype(np.int16)
    h, w = rgb.shape[:2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    blue = (b > r + 25) & (b > 70) & (g > r - 5)
    foam = (r > 195) & (g > 205) & (b > 205) & (np.abs(r - b) < 45)
    seaish = blue | foam
    lab, _ = ndimage.label(seaish)
    edge = set(lab[-1, :]) | set(lab[:, -1]) | set(lab[420:700, 0])
    edge.discard(0)
    sea = np.isin(lab, list(edge))
    sea = ndimage.binary_closing(sea, iterations=4)
    land = ~sea
    lab, n = ndimage.label(land)
    sizes = ndimage.sum(land, lab, range(1, n + 1))
    land = lab == (int(np.argmax(sizes)) + 1)
    land = ndimage.binary_fill_holes(ndimage.binary_opening(land, iterations=3))
    cs, _ = cv2.findContours(land.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    c = max(cs, key=cv2.contourArea)
    poly = cv2.approxPolyDP(c, 3.0, True)[:, 0, :].tolist()
    print("land polygon points:", len(poly))
    return [poly]


def raster(polys_xz, x0, z0, nx, nz):
    """世界平面多边形 → 网格布尔图"""
    im = Image.new("L", (nx, nz), 0)
    d = ImageDraw.Draw(im)
    for p in polys_xz:
        d.polygon([((x - x0) / CELL, (z - z0) / CELL) for x, z in p], fill=255)
    return np.asarray(im) > 0


PAD_LIFT = 0.02   # 石板路/小广场比所在台地高 2 cm：GroundAt 取到它，视觉上是一道小路沿


def build_pads_and_paths(zones, cam):
    """2026-11-08 新布局：路是一条条石板路，中间是草地。
    paths：中心线（像素）+ 宽度（米），在世界里按宽度外扩，同一高度的合并成一块；
    pads：直接给多边形或椭圆（像素）。输出成可走的台地（zone=stone/grass），高出所在台地 2 cm。
    合并后带洞的多边形（环路）沿洞的中心竖切成几块，网格和 Walkable 都不支持洞。"""
    from shapely.geometry import LineString, Polygon, MultiPolygon
    from shapely.ops import unary_union, split
    groups = {}   # (y, zone) -> [geom]
    for p in zones.get("paths", []):
        y = p["y"]
        pts = [cam.unproject(u, v, y) for u, v in p["nodes"]]
        if p.get("closed"):
            pts = pts + [pts[0]]
        g = LineString(pts).buffer(p["width"] / 2, quad_segs=8)
        groups.setdefault((y, p.get("zone", "stone")), []).append(g)
    for p in zones.get("pads", []):
        y = p["y"]
        if "ellipse" in p:
            cu, cv, ru, rv = p["ellipse"]
            px = [(cu + ru * math.cos(a * math.pi / 16), cv + rv * math.sin(a * math.pi / 16)) for a in range(32)]
        else:
            px = p["poly"]
        g = Polygon([cam.unproject(u, v, y) for u, v in px]).buffer(0)
        groups.setdefault((y, p.get("zone", "stone")), []).append(g)

    def no_holes(poly):
        if len(poly.interiors) == 0:
            return [poly]
        hx = Polygon(poly.interiors[0]).centroid.x
        miny, maxy = poly.bounds[1] - 1, poly.bounds[3] + 1
        parts = split(poly, LineString([(hx, miny), (hx, maxy)]))
        out = []
        for q in parts.geoms:
            out += no_holes(q)
        return out

    out = []
    for (y, zone), geoms in sorted(groups.items()):
        merged = unary_union(geoms)
        polys = list(merged.geoms) if isinstance(merged, MultiPolygon) else [merged]
        k = 0
        for poly in polys:
            for piece in no_holes(poly):
                piece = piece.simplify(0.03)
                if piece.area < 0.2:
                    continue
                ring = list(piece.exterior.coords)[:-1]
                out.append({"id": f"pad_{zone}_{str(y).replace('.', '_')}_{k}", "y": round(y + PAD_LIFT, 3), "walk": True,
                            "zone": zone, "xz": [(round(a, 3), round(b, 3)) for a, b in ring], "poly": []})
                k += 1
    print("pads:", len(out), "pieces from", sum(len(g) for g in groups.values()), "paths/pads")
    return out


def ribbon(nodes_xyz, width):
    """折线 → 每段一个四边形，带端点高度"""
    out = []
    for (x0, y0, z0), (x1, y1, z1) in zip(nodes_xyz, nodes_xyz[1:]):
        dx, dz = x1 - x0, z1 - z0
        L = math.hypot(dx, dz) or 1.0
        nx_, nz_ = -dz / L * width / 2, dx / L * width / 2
        out.append(((x0, y0, z0), (x1, y1, z1), [(x0 + nx_, z0 + nz_), (x1 + nx_, z1 + nz_), (x1 - nx_, z1 - nz_), (x0 - nx_, z0 - nz_)]))
    return out


def point_in(xz, x, z):
    inside, j = False, len(xz) - 1
    for i in range(len(xz)):
        (xi, zi), (xj, zj) = xz[i], xz[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi) + xi:
            inside = not inside
        j = i
    return inside


def notch_tiers(tiers, roads):
    """2026-11-08 第四轮：坡道比台地矮的那几段，在台地上开豁口。
    原来住宅台地（5.5 m）整块盖在三条上山坡道上面：坡道埋在山体里，坡道口横着一道 2.5 m 的崖壁，
    角色等于从墙里钻进去、在草皮底下走上去。碰撞贴着模型走以后这道墙就把住宅区堵死了。
    现在顺着坡道（路宽 + 0.3 m）把台地切开，两边是石墙，和目标图里上台阶两边砌墙一样。"""
    from shapely.geometry import Polygon
    from shapely.ops import unary_union
    out = []
    for t in tiers:
        P = Polygon(t["xz"])
        if not P.is_valid:
            P = P.buffer(0)
        cuts = []
        for r in roads:
            # 坡道低的那头往外多切 1.5 m：坡道起点常常已经在台地边线里面，只从起点切会在坡道口留一道矮墙
            nodes = list(r["xyz"])
            def ext(a, b, d=1.5):
                L = math.hypot(a[0] - b[0], a[2] - b[2]) or 1.0
                return (a[0] + (a[0] - b[0]) / L * d, a[1], a[2] + (a[2] - b[2]) / L * d)
            if len(nodes) >= 2:
                if nodes[0][1] < t["y"] - 0.25:
                    nodes = [ext(nodes[0], nodes[1])] + nodes
                if nodes[-1][1] < t["y"] - 0.25:
                    nodes = nodes + [ext(nodes[-1], nodes[-2])]
            # 可走的石板平台只切"坡道比平台低 0.18 m 以上"那一截：坡顶和平台接得上（剩下的台阶比一步 0.22 m 矮）；
            # 不可走的台地整段切
            thr = 0.18 if t.get("walk") else 0.25
            lim = t["y"] - thr
            for a_, b_ in zip(nodes, nodes[1:]):
                if min(a_[1], b_[1]) >= lim:
                    continue
                if t.get("walk") and max(a_[1], b_[1]) > lim:
                    s = (lim - a_[1]) / (b_[1] - a_[1])
                    mid_ = (a_[0] + (b_[0] - a_[0]) * s, lim, a_[2] + (b_[2] - a_[2]) * s)
                    a_, b_ = (a_, mid_) if a_[1] < lim else (mid_, b_)
                for (p0, p1, quad) in ribbon([a_, b_], r["width"] + 0.3):
                    q = Polygon(quad)
                    if q.intersects(P) and q.intersection(P).area > 0.05:
                        cuts.append(q)
        if not cuts:
            out.append(t)
            continue
        D = P.difference(unary_union(cuts).buffer(0.05, join_style=2))
        parts = [D] if D.geom_type == "Polygon" else list(D.geoms)
        parts = sorted([g for g in parts if g.area > 1.0], key=lambda g: -g.area)
        for i, g in enumerate(parts):
            g = g.simplify(0.02)
            if len(g.interiors):
                print(f"  警告：{t['id']} 开豁口后有内洞，忽略内洞")
            xz = list(g.exterior.coords)[:-1]
            out.append(dict(t, id=t["id"] if i == 0 else f"{t['id']}_{i + 1}", xz=[(x, z) for x, z in xz]))
        print(f"豁口 {t['id']}: 切掉 {len(cuts)} 段坡道 → {len(parts)} 块，面积 {P.area:.1f} → {sum(g.area for g in parts):.1f}")
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--extract-land", action="store_true")
    a = ap.parse_args()
    zpath = DATA / "preview_zones.json"
    zones = json.loads(zpath.read_text(encoding="utf-8"))
    cam = PreviewCam()

    # 投影往返测试：误差必须小于 1 像素
    for pid, p in zones["points"].items():
        x, z = cam.unproject(*p["uv"], p["y"])
        u, v = cam.project(x, p["y"], z)
        assert abs(u - p["uv"][0]) < 1 and abs(v - p["uv"][1]) < 1, pid

    if a.extract_land or "land" not in zones:
        zones["land"] = extract_land(zones)
        txt = json.dumps(zones, ensure_ascii=False, indent=2)
        zpath.write_text(txt, encoding="utf-8")

    tiers = [dict(t, xz=[cam.unproject(u, v, t["y"]) for u, v in t["poly"]]) for t in zones["tiers"]]
    tiers += build_pads_and_paths(zones, cam)
    roads = []
    for r in zones["roads"]:
        nodes = [(lambda xz: (xz[0], y, xz[1]))(cam.unproject(u, v, y)) for u, v, y in r["nodes"]]
        roads.append(dict(r, xyz=nodes))
    tiers = notch_tiers(tiers, roads)
    platforms = [dict(p, xz=[cam.unproject(u, v, p["y"]) for u, v in p["poly"]]) for p in zones.get("platforms", [])]
    land_xz = [[cam.unproject(u, v, SHORE_Y) for u, v in poly] for poly in zones["land"]]

    # 网格范围
    allp = [q for t in tiers for q in t["xz"]] + [q for l in land_xz for q in l] + [(n[0], n[2]) for r in roads for n in r["xyz"]]
    x0, x1 = min(p[0] for p in allp) - MARGIN, max(p[0] for p in allp) + MARGIN
    z0, z1 = min(p[1] for p in allp) - MARGIN, max(p[1] for p in allp) + MARGIN
    nx, nz = int((x1 - x0) / CELL) + 1, int((z1 - z0) / CELL) + 1
    print(f"grid {nx}x{nz} = {nx*nz} cells, x {x0:.1f}..{x1:.1f}, z {z0:.1f}..{z1:.1f}")

    fixed = np.zeros((nz, nx), bool)
    h = np.zeros((nz, nx), np.float32)
    zone = np.full((nz, nx), ZONE["rock"], np.uint8)
    walk = np.zeros((nz, nx), np.uint8)
    land = raster(land_xz, x0, z0, nx, nz)
    zone[~land] = ZONE["sea"]

    # 海和岸线固定
    fixed[~land] = True
    h[~land] = SEA_FLOOR
    shore = land & ~ndimage.binary_erosion(land)
    fixed[shore] = True
    h[shore] = SHORE_Y

    for t in tiers:
        m = raster([t["xz"]], x0, z0, nx, nz)
        fixed[m] = True
        h[m] = t["y"]
        zone[m] = ZONE[t["zone"]]
        if t["walk"]:
            walk[m] = 1

    zz, xx = np.mgrid[0:nz, 0:nx]
    wx, wz = x0 + xx * CELL, z0 + zz * CELL
    for r in roads:
        for (p0, p1, quad) in ribbon(r["xyz"], r["width"]):
            m = raster([quad], x0, z0, nx, nz)
            dx, dz = p1[0] - p0[0], p1[2] - p0[2]
            L2 = dx * dx + dz * dz or 1.0
            s = np.clip(((wx - p0[0]) * dx + (wz - p0[2]) * dz) / L2, 0, 1)
            fixed[m] = True
            h[m] = (p0[1] + (p1[1] - p0[1]) * s)[m]
            zone[m] = ZONE["stone"]
            if r["walk"]:
                walk[m] = 1

    # 没固定的格子解拉普拉斯方程，得到平滑的山坡
    idx = -np.ones((nz, nx), np.int64)
    free = ~fixed
    free[0, :] = free[-1, :] = False
    free[:, 0] = free[:, -1] = False
    idx[free] = np.arange(free.sum())
    n = int(free.sum())
    rows, cols, vals, rhs = [], [], [], np.zeros(n)
    for iz, ix in zip(*np.nonzero(free)):
        i = idx[iz, ix]
        rows.append(i); cols.append(i); vals.append(4.0)
        for dz_, dx_ in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            jz, jx = iz + dz_, ix + dx_
            if free[jz, jx]:
                rows.append(i); cols.append(idx[jz, jx]); vals.append(-1.0)
            else:
                rhs[i] += h[jz, jx]
    h[free] = spsolve(sparse.csr_matrix((vals, (rows, cols)), shape=(n, n)), rhs)

    # 平台只算可走，不抬地形；单独生成网格
    for p in platforms:
        if p.get("walk"):
            walk[raster([p["xz"]], x0, z0, nx, nz)] = 1

    # 坡度检查（只看可走区）
    gz, gx = np.gradient(h, CELL)
    slope = np.degrees(np.arctan(np.hypot(gx, gz)))
    plat = np.zeros((nz, nx), bool)
    for p in platforms:
        plat |= raster([p["xz"]], x0, z0, nx, nz)
    core = ndimage.binary_erosion(walk > 0, iterations=1) & ~plat
    print("walkable cells", int((walk > 0).sum()), "max slope in core", round(float(slope[core].max()), 1), "p99", round(float(np.percentile(slope[core], 99)), 1))

    WORLD.mkdir(parents=True, exist_ok=True)
    (WORLD / "island_height.bytes").write_bytes(h.astype("<f4").tobytes())
    (WORLD / "island_walk.bytes").write_bytes(walk.tobytes())
    (DATA / "island_zone.bytes").write_bytes(zone.tobytes())

    pts = []
    for pid, p in zones["points"].items():
        x, z = cam.unproject(*p["uv"], p["y"])
        pts.append({"id": pid, "x": round(x, 3), "y": p["y"], "z": round(z, 3), "main": p["main"]})
    blds = []
    for name, b in zones["buildings"].items():
        x, z = cam.unproject(*b["foot"], b["y"])
        blds.append({"name": name, "instance": b["instance"], "asset": b["asset"], "x": round(x, 3), "y": b["y"], "z": round(z, 3),
                     "width": b.get("width", 0.0), "height": b.get("height", 0.0), "mirror": b.get("mirror", False),
                     "box": b["box"], "foot": b["foot"]})
    plats = [{"id": p["id"], "y": p["y"], "walk": p["walk"], "x": [round(q[0], 3) for q in p["xz"]], "z": [round(q[1], 3) for q in p["xz"]]} for p in platforms]
    camdef = json.loads((WORLD / "preview_camera.json").read_text(encoding="utf-8"))
    by_id = {p["id"]: p for p in pts}
    by_name = {b["name"]: b for b in blds}

    def named(name, pid, target, eye=1.6, fov=55):
        a = by_id[pid]
        ex, ey, ez = a["x"], a["y"] + eye, a["z"]
        dx, dy, dz = target[0] - ex, target[1] - ey, target[2] - ez
        yaw = math.degrees(math.atan2(dx, dz))
        pitch = math.degrees(math.atan2(-dy, math.hypot(dx, dz)))
        return {"name": name, "pos": [round(ex, 2), round(ey, 2), round(ez, 2)],
                "euler": [round(pitch, 1), round(yaw, 1), 0.0], "fov": fov, "width": 1774, "height": 887}

    def bpos(key):
        b = by_name[key]
        return (b["x"], b["y"] + 2.0, b["z"])

    walk_areas = []
    for t in tiers:
        if t.get("walk"):
            walk_areas.append({"id": t["id"], "y": t["y"], "x": [round(q[0], 3) for q in t["xz"]], "z": [round(q[1], 3) for q in t["xz"]]})
    for p in platforms:
        if p.get("walk"):
            walk_areas.append({"id": p["id"], "y": p["y"], "x": [round(q[0], 3) for q in p["xz"]], "z": [round(q[1], 3) for q in p["xz"]]})
    # 可走路面按段拆成四边形，山路因此也能通过 Walkable 判定
    for r in roads:
        if not r.get("walk"):
            continue
        for i in range(len(r["xyz"]) - 1):
            ax, ay, az = r["xyz"][i]
            bx, by, bz = r["xyz"][i + 1]
            dx, dz = bx - ax, bz - az
            ln = math.hypot(dx, dz) or 1.0
            # 可走区严格落在路面网格之内：两侧缩 2 cm，两端多 14 cm（路面网格多 15 cm），不留数值缝
            half = r["width"] * 0.5 - 0.02
            nx, nz = -dz / ln * half, dx / ln * half
            ux, uz = dx / ln * 0.14, dz / ln * 0.14
            # 2026-11-08 第四轮：路的两头再多伸 0.4 m 搭到平台上（东坡道口和广场石板之间原来有几厘米的缝，角色卡在坡脚）
            #   只在伸出去那头落在同高度的可走平台上时才伸，免得伸到岸线上留洞
            def on_pad(px_, pz_, py_):
                # 伸出去那条边的中点落在同高度的可走平台上；两个角下面是同高度的地（平台或台地），不能悬在岸线上
                if not any(t2.get("walk") and abs(t2["y"] - py_) < 0.3 and point_in(t2["xz"], px_, pz_) for t2 in tiers):
                    return False
                for k in (1.0, -1.0):
                    qx, qz = px_ + nx * k, pz_ + nz * k
                    if not any(abs(t2["y"] - py_) < 0.3 and point_in(t2["xz"], qx, qz) for t2 in tiers):
                        return False
                return True
            sx, sz = (dx / ln * 0.4, dz / ln * 0.4) if i == 0 and on_pad(ax - dx / ln * 0.4, az - dz / ln * 0.4, ay) else (ux, uz)
            ex, ez = (dx / ln * 0.4, dz / ln * 0.4) if i == len(r["xyz"]) - 2 and on_pad(bx + dx / ln * 0.4, bz + dz / ln * 0.4, by) else (ux, uz)
            ax, az, bx, bz = ax - sx, az - sz, bx + ex, bz + ez
            walk_areas.append({
                "id": r["id"] + "_" + str(i),
                "y": (ay + by) * 0.5,
                "x": [round(ax + nx, 3), round(bx + nx, 3), round(bx - nx, 3), round(ax - nx, 3)],
                "z": [round(az + nz, 3), round(bz + nz, 3), round(bz - nz, 3), round(az - nz, 3)],
            })

    out = {
        "points": pts,
        "coast": {"x": [round(q[0], 3) for q in land_xz[0]], "z": [round(q[1], 3) for q in land_xz[0]]},
        "tiers": [{"id": t["id"], "y": t["y"], "walk": t.get("walk", False), "zone": t.get("zone", "stone"),
                   "x": [round(q[0], 3) for q in t["xz"]], "z": [round(q[1], 3) for q in t["xz"]]} for t in tiers + platforms],
        "roads": [{"id": r["id"], "kind": r["kind"], "width": r["width"], "walk": r.get("walk", False),
                   "x": [round(n[0], 3) for n in r["xyz"]], "y": [round(n[1], 3) for n in r["xyz"]],
                   "z": [round(n[2], 3) for n in r["xyz"]]} for r in roads],
        "walk": walk_areas,
        "buildings": blds,
        "props": [],
        "cameras": [{"name": "preview", "pos": camdef["pos"], "euler": camdef["euler"], "fov": camdef["fov"], "width": camdef["width"], "height": camdef["height"]},
                    named("street", "10", (by_id["13"]["x"], by_id["13"]["y"] + 1.6, by_id["13"]["z"]), fov=60),
                    named("bakery", "11", bpos("bakery")),
                    named("school", "19", bpos("school")),
                    named("lighthouse", "30", bpos("lighthouse"))]
    }
    (WORLD / "world_layout.json").write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")

    # 叠回预览图
    im = Image.open(PREVIEW).convert("RGB")
    d = ImageDraw.Draw(im, "RGBA")
    font = ImageFont.truetype("arial.ttf", 13)
    for poly in zones["land"]:
        d.line([tuple(q) for q in poly] + [tuple(poly[0])], fill=(255, 255, 255, 255), width=2)
    color = {"stone": (255, 160, 0, 90), "grass": (0, 220, 0, 70), "sand": (255, 255, 0, 80), "rock": (150, 90, 40, 80)}
    for t in zones["tiers"]:
        d.polygon([tuple(q) for q in t["poly"]], fill=color[t["zone"]], outline=(255, 255, 255, 255))
    for p in zones.get("platforms", []):
        d.polygon([tuple(q) for q in p["poly"]], fill=(160, 90, 255, 90), outline=(255, 255, 255, 255))
    for r in zones["roads"]:
        d.line([(n[0], n[1]) for n in r["nodes"]], fill=(255, 40, 40, 255) if r["walk"] else (120, 120, 255, 255), width=4)
    for name, b in zones["buildings"].items():
        d.rectangle(b["box"], outline=(0, 255, 255, 255), width=2)
        d.ellipse((b["foot"][0] - 4, b["foot"][1] - 4, b["foot"][0] + 4, b["foot"][1] + 4), fill=(0, 255, 255, 255))
    for pid, p in zones["points"].items():
        u, v = p["uv"]
        d.ellipse((u - 4, v - 4, u + 4, v + 4), fill=(255, 0, 0, 255))
        d.text((u + 5, v - 7), pid, fill=(255, 255, 255, 255), font=font, stroke_width=2, stroke_fill=(0, 0, 0, 255))
    im.save(ROOT / "docs/reference/zones_overlay.jpg", quality=88)

    # 高度图预览（从上往下看，北在上）
    vis = np.clip((h - SEA_FLOOR) / 12.0, 0, 1)
    rgb = np.stack([vis * 255, vis * 255, vis * 255], -1).astype(np.uint8)
    rgb[~land] = (30, 70, 140)
    rgb[walk > 0, 0] = 255
    img_h, img_w = rgb.shape[0], rgb.shape[1]
    Image.fromarray(rgb[::-1]).resize((img_w * 3, img_h * 3), Image.NEAREST).save(ROOT / "docs/reference/island_height_top.png")
    print("ok")


if __name__ == "__main__":
    main()
