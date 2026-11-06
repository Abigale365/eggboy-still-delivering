# WP-01 预览对比：把预览图和游戏截图拼成左右对比图。
# 用法：python tools\compare-preview.py <preview.png> <shot.png> <out.png>
import sys
from PIL import Image, ImageDraw

def main():
    if len(sys.argv) != 4:
        print("用法: compare-preview.py <preview.png> <shot.png> <out.png>")
        return 2
    preview, shot, out = sys.argv[1], sys.argv[2], sys.argv[3]
    a = Image.open(preview).convert("RGB")
    b = Image.open(shot).convert("RGB")
    h = 720
    a = a.resize((int(a.width * h / a.height), h), Image.LANCZOS)
    b = b.resize((int(b.width * h / b.height), h), Image.LANCZOS)
    gap = 12
    canvas = Image.new("RGB", (a.width + b.width + gap, h), (24, 24, 24))
    canvas.paste(a, (0, 0))
    canvas.paste(b, (a.width + gap, 0))
    canvas.save(out)
    print("OK", out, canvas.size)
    return 0

if __name__ == "__main__":
    sys.exit(main())
