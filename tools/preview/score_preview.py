"""按工程书 5.7 给 PreviewCam 截图打分。

python tools/preview/score_preview.py --shots <含 preview.png 的目录> --out <证据目录> [--stage layout|look|all]

依据：
  docs/reference/preview.png                      目标图
  Assets/TideTown/Data/preview_zones.json         图上描的分区（像素）
  <shots>/preview_id.png、preview_id.json         ShotTool -idpass 的输出

合格线写在下面，只有用户同意才能改。
"""
import argparse, json, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
PREVIEW = ROOT / "docs/reference/preview.png"
ZONES = ROOT / "Assets/TideTown/Data/preview_zones.json"
LIMITS = {"layout_px": 15.0, "coast_iou": 0.90, "building_err": 0.10, "color_de": 12.0, "cover": 0.80}
SEA, ROCK, FOLIAGE = (0, 0, 255), (255, 255, 0), (0, 255, 255)
BUILDING_R = 200
TOL = 3


def load(p):
    return np.asarray(Image.open(p).convert("RGB")).astype(np.int16)


def poly_mask(polys, size):
    im = Image.new("L", size, 0)
    d = ImageDraw.Draw(im)
    for poly in polys:
        d.polygon([tuple(p) for p in poly], fill=255)
    return np.asarray(im) > 0


def near(img, rgb):
    return np.all(np.abs(img - np.array(rgb)) <= TOL, axis=2)


def lab(rgb):
    """sRGB (0–255, N×3) → CIELAB"""
    c = rgb / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def bbox(mask):
    ys, xs = np.nonzero(mask)
    return None if len(xs) == 0 else [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]


def score(shots, stage):
    zones = json.loads(ZONES.read_text(encoding="utf-8"))
    target = load(PREVIEW)
    h, w = target.shape[:2]
    shot = load(shots / "preview.png")
    if shot.shape != target.shape:
        sys.exit(f"截图尺寸 {shot.shape} 和预览图 {target.shape} 不同")
    idimg = load(shots / "preview_id.png")
    idj = json.loads((shots / "preview_id.json").read_text(encoding="utf-8"))
    exclude = poly_mask(zones.get("exclude", []), (w, h))
    res = {"limits": LIMITS, "checks": {}}

    # 布局：主线点的实际投影和图上编号
    errs = {}
    for pid, p in zones["points"].items():
        if not p.get("main"):
            continue
        q = idj["points"].get(pid)
        errs[pid] = None if q is None else float(np.hypot(q[0] - p["uv"][0], q[1] - p["uv"][1]))
    found = [v for v in errs.values() if v is not None]
    missing = [k for k, v in errs.items() if v is None]
    mean = float(np.mean(found)) if found else None
    res["checks"]["layout"] = {"mean_px": mean, "max_px": max(found) if found else None, "missing": missing, "per_point": errs,
                               "pass": mean is not None and not missing and mean <= LIMITS["layout_px"]}

    # 轮廓：陆地（不是海也不是背景）
    land_t = poly_mask(zones["land"], (w, h))
    land_s = ~near(idimg, SEA) & ~near(idimg, (0, 0, 0))
    u = (land_t | land_s) & ~exclude
    iou = float(((land_t & land_s) & ~exclude).sum() / max(1, u.sum()))
    res["checks"]["coast"] = {"iou": iou, "pass": iou >= LIMITS["coast_iou"]}

    # 建筑：逐栋比边框
    bres = {}
    ok = True
    for name, b in zones["buildings"].items():
        g = None
        for k, v in idj["buildings"].items():
            if k == b["instance"]:
                g = v
        tb = b["box"]
        if g is None:
            bres[name] = {"error": "场景里没有"}
            ok = False
            continue
        sb = bbox((np.abs(idimg[..., 0] - BUILDING_R) <= TOL) & (np.abs(idimg[..., 1] - g) <= TOL) & (idimg[..., 2] <= TOL))
        if sb is None:
            bres[name] = {"error": "画面里看不到"}
            ok = False
            continue
        tw, th = tb[2] - tb[0], tb[3] - tb[1]
        pos = max(abs((sb[0] + sb[2] - tb[0] - tb[2]) / 2) / tw, abs((sb[1] + sb[3] - tb[1] - tb[3]) / 2) / th)
        size = max(abs((sb[2] - sb[0]) - tw) / tw, abs((sb[3] - sb[1]) - th) / th)
        bres[name] = {"target": tb, "actual": sb, "pos_err": round(pos, 3), "size_err": round(size, 3)}
        ok = ok and pos <= LIMITS["building_err"] and size <= LIMITS["building_err"]
    res["checks"]["buildings"] = {"per_building": bres, "pass": ok and bool(bres)}

    if stage in ("look", "all"):
        cres = {}
        ok = True
        for name, polys in zones.get("colors", {}).items():
            m = poly_mask(polys, (w, h)) & ~exclude
            if m.sum() == 0:
                continue
            a, b = np.median(target[m], axis=0), np.median(shot[m], axis=0)
            de = float(np.linalg.norm(lab(a.astype(float)) - lab(b.astype(float))))
            cres[name] = {"target": a.tolist(), "actual": b.tolist(), "de": round(de, 1)}
            ok = ok and de <= LIMITS["color_de"]
        res["checks"]["colors"] = {"per_class": cres, "pass": ok and bool(cres)}
        m = poly_mask(zones.get("cover", []), (w, h)) & ~exclude
        cov = float((near(idimg, ROCK) | near(idimg, FOLIAGE))[m].mean()) if m.sum() else None
        res["checks"]["cover"] = {"fraction": cov, "pass": cov is not None and cov >= LIMITS["cover"]}

    res["all_pass"] = all(c["pass"] for c in res["checks"].values())
    return res, target, shot


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--shots", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--stage", default="layout", choices=["layout", "look", "all"])
    a = ap.parse_args()
    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    res, target, shot = score(Path(a.shots), a.stage)
    (out / "score.json").write_text(json.dumps(res, ensure_ascii=False, indent=2), encoding="utf-8")
    t, s = target.astype(np.uint8), shot.astype(np.uint8)
    Image.fromarray(np.concatenate([t, s], axis=1)).save(out / "side_by_side.jpg", quality=88)
    Image.fromarray(((target + shot) // 2).astype(np.uint8)).save(out / "overlay_50.jpg", quality=88)
    for k, c in res["checks"].items():
        brief = {x: y for x, y in c.items() if x in ("mean_px", "max_px", "missing", "iou", "fraction", "pass")}
        print(k, brief)
    print("ALL_PASS", res["all_pass"])


if __name__ == "__main__":
    main()
