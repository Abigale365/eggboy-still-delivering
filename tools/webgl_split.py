"""把网页版构建复制到发布目录，data 文件超过上限就切块（GitHub 单个文件不能超过 100 MB）。

python tools/webgl_split.py Build/MainWebGL <发布目录> [--max-mb 90]

发布目录里：data 文件换成 <data>.part00、.part01 …，再写 <data>.parts.json（文件名列表 + 总字节数）；
EGGBOY 网页模板（Assets/WebGLTemplates/EGGBOY/index.html）看到 parts.json 就分块下载、在浏览器里拼回去。
其余文件原样复制。不改 Build/MainWebGL 本身。
"""
import argparse, json, shutil
from pathlib import Path

ap = argparse.ArgumentParser()
ap.add_argument("src")
ap.add_argument("dst")
ap.add_argument("--max-mb", type=float, default=90)
a = ap.parse_args()
src, dst = Path(a.src), Path(a.dst)
limit = int(a.max_mb * 1024 * 1024)

for f in src.rglob("*"):
    if f.is_dir():
        continue
    rel = f.relative_to(src)
    out = dst / rel
    out.parent.mkdir(parents=True, exist_ok=True)
    is_data = rel.parts[0] == "Build" and ".data" in f.name
    if is_data and f.stat().st_size > limit:
        names, size = [], f.stat().st_size
        with f.open("rb") as fh:
            i = 0
            while True:
                chunk = fh.read(limit)
                if not chunk:
                    break
                name = f"{f.name}.part{i:02d}"
                (out.parent / name).write_bytes(chunk)
                names.append(name)
                i += 1
        (out.parent / (f.name + ".parts.json")).write_text(json.dumps({"files": names, "size": size}), encoding="utf-8")
        print(f"切块 {rel}: {size / 2**20:.1f} MB → {len(names)} 块")
    else:
        shutil.copy2(f, out)
total = sum(p.stat().st_size for p in dst.rglob("*") if p.is_file())
biggest = max((p for p in dst.rglob("*") if p.is_file()), key=lambda p: p.stat().st_size)
print(f"发布目录 {total / 2**20:.1f} MB，最大文件 {biggest.name} {biggest.stat().st_size / 2**20:.1f} MB")
