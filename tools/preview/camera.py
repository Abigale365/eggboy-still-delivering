"""PreviewCam 的投影和反投影。工程书 5.1。左手系：X 东，Y 上，Z 北（和 Unity 一致）。"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CAM_JSON = ROOT / "Assets/TideTown/Data/Resources/World/preview_camera.json"


class PreviewCam:
    def __init__(self, path=CAM_JSON):
        c = json.loads(Path(path).read_text(encoding="utf-8"))
        self.W, self.H = c["width"], c["height"]
        pitch = math.radians(c["euler"][0])
        self.f = (self.H / 2) / math.tan(math.radians(c["fov"]) / 2)
        self.C = tuple(c["pos"])
        self.F = (0.0, -math.sin(pitch), math.cos(pitch))
        self.R = (1.0, 0.0, 0.0)
        self.U = (0.0, math.cos(pitch), math.sin(pitch))

    def unproject(self, u, v, y):
        """图上像素 (u, v) 落在高度 y 的平面上的世界 (x, z)"""
        a, b = u - self.W / 2, self.H / 2 - v
        d = [self.F[i] * self.f + self.R[i] * a + self.U[i] * b for i in range(3)]
        k = (y - self.C[1]) / d[1]
        return self.C[0] + d[0] * k, self.C[2] + d[2] * k

    def project(self, x, y, z):
        """世界坐标 → 图上像素 (u, v)"""
        p = (x - self.C[0], y - self.C[1], z - self.C[2])
        dz = sum(p[i] * self.F[i] for i in range(3))
        dx = sum(p[i] * self.R[i] for i in range(3))
        dy = sum(p[i] * self.U[i] for i in range(3))
        return self.W / 2 + self.f * dx / dz, self.H / 2 - self.f * dy / dz
