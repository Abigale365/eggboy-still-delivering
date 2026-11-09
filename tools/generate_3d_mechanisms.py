# -*- coding: utf-8 -*-
"""
从潮汐镇现有 Lux3D 资产（C4_狗窝木箱、D3_望远镜、A6_台秤）中提取原版手绘暖木与黄铜 PBR 贴图，
生成 4 件带有真实多部件层级（可独立滑动的榫卯木栓节点 Latch_0..2、可独立咬合旋转的三环齿轮节点 Ring_0..2、
行星联动齿轮 Gear_0..2、中心主轴 Core_Spindle）的高精度 3D 机关 GLB 模型：
  1. M1_鲁班三环风门机关.glb（面包房烤炉）
  2. M2_小黑爪印转盘木匣.glb（学校后院狗碗）
  3. M3_潮汐船坞齿轮绞盘.glb（涨潮断桥渡口）
  4. M4_守塔人黄铜三轴星盘.glb（灯塔顶层罗经）
"""
import json
import math
import os
import struct


def extract_first_png_from_glb(glb_path):
    with open(glb_path, "rb") as f:
        magic, ver, total_len = struct.unpack("<4sII", f.read(12))
        j_len, j_type = struct.unpack("<II", f.read(8))
        j_data = json.loads(f.read(j_len).decode("utf-8"))
        b_len, b_type = struct.unpack("<II", f.read(8))
        bin_chunk = f.read(b_len)

    images = j_data.get("images", [])
    buffer_views = j_data.get("bufferViews", [])
    for img in images:
        bv_idx = img.get("bufferView")
        if bv_idx is not None and 0 <= bv_idx < len(buffer_views):
            bv = buffer_views[bv_idx]
            offset = bv.get("byteOffset", 0)
            length = bv.get("byteLength", 0)
            raw = bin_chunk[offset : offset + length]
            mime = img.get("mimeType", "image/png")
            if length > 64:
                return raw, mime
    return None, None


class MeshBuilder:
    def __init__(self):
        self.positions = []
        self.normals = []
        self.uvs = []
        self.indices = []

    def add_vertex(self, pos, norm, uv):
        idx = len(self.positions)
        self.positions.append(pos)
        self.normals.append(norm)
        self.uvs.append(uv)
        return idx

    def add_quad(self, p0, p1, p2, p3, n, uv0=(0, 0), uv1=(1, 0), uv2=(1, 1), uv3=(0, 1)):
        i0 = self.add_vertex(p0, n, uv0)
        i1 = self.add_vertex(p1, n, uv1)
        i2 = self.add_vertex(p2, n, uv2)
        i3 = self.add_vertex(p3, n, uv3)
        self.indices.extend([i0, i1, i2, i0, i2, i3])

    def add_bevel_box(self, cx, cy, cz, sx, sy, sz, uv_scale=1.0):
        hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
        faces = [
            # +Z front
            ((cx - hx, cy - hy, cz + hz), (cx + hx, cy - hy, cz + hz), (cx + hx, cy + hy, cz + hz), (cx - hx, cy + hy, cz + hz), (0, 0, 1)),
            # -Z back
            ((cx + hx, cy - hy, cz - hz), (cx - hx, cy - hy, cz - hz), (cx - hx, cy + hy, cz - hz), (cx + hx, cy + hy, cz - hz), (0, 0, -1)),
            # +X right
            ((cx + hx, cy - hy, cz + hz), (cx + hx, cy - hy, cz - hz), (cx + hx, cy + hy, cz - hz), (cx + hx, cy + hy, cz + hz), (1, 0, 0)),
            # -X left
            ((cx - hx, cy - hy, cz - hz), (cx - hx, cy - hy, cz + hz), (cx - hx, cy + hy, cz + hz), (cx - hx, cy + hy, cz - hz), (-1, 0, 0)),
            # +Y top
            ((cx - hx, cy + hy, cz + hz), (cx + hx, cy + hy, cz + hz), (cx + hx, cy + hy, cz - hz), (cx - hx, cy + hy, cz - hz), (0, 1, 0)),
            # -Y bottom
            ((cx - hx, cy - hy, cz - hz), (cx + hx, cy - hy, cz - hz), (cx + hx, cy - hy, cz + hz), (cx - hx, cy - hy, cz + hz), (0, -1, 0)),
        ]
        for p0, p1, p2, p3, n in faces:
            self.add_quad(
                p0, p1, p2, p3, n,
                (0.1, 0.1), (0.9 * uv_scale, 0.1), (0.9 * uv_scale, 0.9 * uv_scale), (0.1, 0.9 * uv_scale)
            )

    def add_cylinder_z(self, cx, cy, cz, radius, depth, segs=24):
        hz = depth * 0.5
        for i in range(segs):
            a0 = (i / segs) * math.tau
            a1 = ((i + 1) / segs) * math.tau
            c0, s0 = math.cos(a0), math.sin(a0)
            c1, s1 = math.cos(a1), math.sin(a1)
            x0, y0 = cx + c0 * radius, cy + s0 * radius
            x1, y1 = cx + c1 * radius, cy + s1 * radius

            # side quad
            self.add_quad(
                (x0, y0, cz - hz), (x1, y1, cz - hz), (x1, y1, cz + hz), (x0, y0, cz + hz),
                (c0, s0, 0),
                (i / segs, 0.2), ((i + 1) / segs, 0.2), ((i + 1) / segs, 0.8), (i / segs, 0.8)
            )
            # front cap (+Z)
            i_center = self.add_vertex((cx, cy, cz + hz), (0, 0, 1), (0.5, 0.5))
            i_a = self.add_vertex((x0, y0, cz + hz), (0, 0, 1), (0.5 + c0 * 0.4, 0.5 + s0 * 0.4))
            i_b = self.add_vertex((x1, y1, cz + hz), (0, 0, 1), (0.5 + c1 * 0.4, 0.5 + s1 * 0.4))
            self.indices.extend([i_center, i_a, i_b])

            # back cap (-Z)
            i_center_b = self.add_vertex((cx, cy, cz - hz), (0, 0, -1), (0.5, 0.5))
            i_ab = self.add_vertex((x1, y1, cz - hz), (0, 0, -1), (0.5 + c1 * 0.4, 0.5 + s1 * 0.4))
            i_bb = self.add_vertex((x0, y0, cz - hz), (0, 0, -1), (0.5 + c0 * 0.4, 0.5 + s0 * 0.4))
            self.indices.extend([i_center_b, i_ab, i_bb])

    def add_gear_ring_z(self, inner_r, outer_r, depth, segs=36, teeth=6, tooth_h=0.028):
        hz = depth * 0.5
        for i in range(segs):
            a0 = (i / segs) * math.tau
            a1 = ((i + 1) / segs) * math.tau
            c0, s0 = math.cos(a0), math.sin(a0)
            c1, s1 = math.cos(a1), math.sin(a1)

            # tooth modulation
            tooth_phase = (i % (segs // teeth)) / float(max(1, segs // teeth))
            r_out0 = outer_r + (tooth_h if tooth_phase < 0.45 else 0.0)
            r_out1 = outer_r + (tooth_h if tooth_phase < 0.45 else 0.0)

            ox0, oy0 = c0 * r_out0, s0 * r_out0
            ox1, oy1 = c1 * r_out1, s1 * r_out1
            ix0, iy0 = c0 * inner_r, s0 * inner_r
            ix1, iy1 = c1 * inner_r, s1 * inner_r

            # front face (+Z)
            self.add_quad(
                (ix0, iy0, hz), (ox0, oy0, hz), (ox1, oy1, hz), (ix1, iy1, hz),
                (0, 0, 1),
                (0.5 + c0 * 0.3, 0.5 + s0 * 0.3),
                (0.5 + c0 * 0.48, 0.5 + s0 * 0.48),
                (0.5 + c1 * 0.48, 0.5 + s1 * 0.48),
                (0.5 + c1 * 0.3, 0.5 + s1 * 0.3),
            )
            # back face (-Z)
            self.add_quad(
                (ix1, iy1, -hz), (ox1, oy1, -hz), (ox0, oy0, -hz), (ix0, iy0, -hz),
                (0, 0, -1),
                (0.5 + c1 * 0.3, 0.5 + s1 * 0.3),
                (0.5 + c1 * 0.48, 0.5 + s1 * 0.48),
                (0.5 + c0 * 0.48, 0.5 + s0 * 0.48),
                (0.5 + c0 * 0.3, 0.5 + s0 * 0.3),
            )
            # outer rim
            self.add_quad(
                (ox0, oy0, -hz), (ox1, oy1, -hz), (ox1, oy1, hz), (ox0, oy0, hz),
                (c0, s0, 0),
                (i / segs, 0.2), ((i + 1) / segs, 0.2), ((i + 1) / segs, 0.8), (i / segs, 0.8)
            )
            # inner rim
            self.add_quad(
                (ix1, iy1, -hz), (ix0, iy0, -hz), (ix0, iy0, hz), (ix1, iy1, hz),
                (-c0, -s0, 0),
                (i / segs, 0.2), ((i + 1) / segs, 0.2), ((i + 1) / segs, 0.8), (i / segs, 0.8)
            )

        # Top pointer marker at 12 o'clock (+Y) so player clearly sees the dial position
        self.add_bevel_box(0.0, (inner_r + outer_r) * 0.5, hz + 0.015, 0.032, (outer_r - inner_r) * 0.95, 0.025)


def export_multi_node_glb(out_path, nodes_spec, textures_spec):
    """
    nodes_spec: list of dict { "name": str, "mesh": MeshBuilder, "mat_idx": int, "translation": [x,y,z] }
    textures_spec: list of dict { "name": str, "bytes": bytes, "mime": str, "baseColor": [r,g,b,a], "metallic": float, "roughness": float }
    """
    bin_parts = []
    buffer_views = []
    accessors = []
    images = []
    textures = []
    materials = []
    meshes = []
    nodes = []

    def append_bin(raw_bytes, target=None):
        pad = (4 - (len(raw_bytes) % 4)) % 4
        padded = raw_bytes + (b"\x00" * pad)
        offset = sum(len(p) for p in bin_parts)
        bin_parts.append(padded)
        bv = {"buffer": 0, "byteOffset": offset, "byteLength": len(raw_bytes)}
        if target is not None:
            bv["target"] = target
        bv_idx = len(buffer_views)
        buffer_views.append(bv)
        return bv_idx

    # 1. Embed authentic Lux3D PNG textures & create PBR materials
    for t_idx, t in enumerate(textures_spec):
        bv_idx = append_bin(t["bytes"])
        images.append({"bufferView": bv_idx, "mimeType": t["mime"], "name": t["name"]})
        textures.append({"source": t_idx})
        materials.append({
            "name": t["name"] + "_Mat",
            "pbrMetallicRoughness": {
                "baseColorTexture": {"index": t_idx},
                "baseColorFactor": t.get("baseColor", [1.0, 1.0, 1.0, 1.0]),
                "metallicFactor": t.get("metallic", 0.15),
                "roughnessFactor": t.get("roughness", 0.65),
            },
            "doubleSided": True,
        })

    # 2. Build meshes and nodes
    child_indices = []
    for n_spec in nodes_spec:
        mb = n_spec["mesh"]
        pos_bytes = bytearray()
        norm_bytes = bytearray()
        uv_bytes = bytearray()
        idx_bytes = bytearray()

        min_p = [1e9, 1e9, 1e9]
        max_p = [-1e9, -1e9, -1e9]
        for p in mb.positions:
            pos_bytes.extend(struct.pack("<3f", *p))
            for k in range(3):
                if p[k] < min_p[k]: min_p[k] = p[k]
                if p[k] > max_p[k]: max_p[k] = p[k]
        for n in mb.normals:
            norm_bytes.extend(struct.pack("<3f", *n))
        for uv in mb.uvs:
            uv_bytes.extend(struct.pack("<2f", *uv))
        for idx in mb.indices:
            idx_bytes.extend(struct.pack("<I", idx))

        bv_pos = append_bin(bytes(pos_bytes), 34962)
        bv_norm = append_bin(bytes(norm_bytes), 34962)
        bv_uv = append_bin(bytes(uv_bytes), 34962)
        bv_idx = append_bin(bytes(idx_bytes), 34963)

        acc_pos = len(accessors)
        accessors.append({
            "bufferView": bv_pos, "componentType": 5126, "count": len(mb.positions),
            "type": "VEC3", "min": min_p, "max": max_p
        })
        acc_norm = len(accessors)
        accessors.append({
            "bufferView": bv_norm, "componentType": 5126, "count": len(mb.normals), "type": "VEC3"
        })
        acc_uv = len(accessors)
        accessors.append({
            "bufferView": bv_uv, "componentType": 5126, "count": len(mb.uvs), "type": "VEC2"
        })
        acc_idx = len(accessors)
        accessors.append({
            "bufferView": bv_idx, "componentType": 5125, "count": len(mb.indices), "type": "SCALAR"
        })

        mesh_idx = len(meshes)
        meshes.append({
            "name": n_spec["name"] + "_Mesh",
            "primitives": [{
                "attributes": {"POSITION": acc_pos, "NORMAL": acc_norm, "TEXCOORD_0": acc_uv},
                "indices": acc_idx,
                "material": n_spec["mat_idx"]
            }]
        })

        node_idx = len(nodes)
        nodes.append({
            "name": n_spec["name"],
            "mesh": mesh_idx,
            "translation": n_spec.get("translation", [0.0, 0.0, 0.0])
        })
        child_indices.append(node_idx)

    root_idx = len(nodes)
    nodes.append({
        "name": "MechanismRoot",
        "children": child_indices
    })

    full_bin = b"".join(bin_parts)
    gltf_dict = {
        "asset": {"version": "2.0", "generator": "TideTown Lux3D Mechanism Assembler"},
        "scene": 0,
        "scenes": [{"name": "Scene", "nodes": [root_idx]}],
        "nodes": nodes,
        "meshes": meshes,
        "materials": materials,
        "textures": textures,
        "images": images,
        "accessors": accessors,
        "bufferViews": buffer_views,
        "buffers": [{"byteLength": len(full_bin)}],
    }

    json_bytes = json.dumps(gltf_dict, separators=(",", ":")).encode("utf-8")
    json_pad = (4 - (len(json_bytes) % 4)) % 4
    json_bytes += b" " * json_pad

    total_len = 12 + 8 + len(json_bytes) + 8 + len(full_bin)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total_len))
        f.write(struct.pack("<II", len(json_bytes), 0x4E4F534A))
        f.write(json_bytes)
        f.write(struct.pack("<II", len(full_bin), 0x004E4942))
        f.write(full_bin)
    print(f"Generated GLB: {out_path} ({total_len / 1024:.1f} KB, {len(nodes_spec)} articulated parts)")


def build_luban_three_ring_mechanism(textures_spec, wood_mat=0, brass_mat=1, iron_mat=2):
    nodes = []

    # 1. Frame_Base: Octagonal carved wooden cabinet + brass corner pillars
    frame = MeshBuilder()
    frame.add_cylinder_z(0, 0, -0.06, 0.46, 0.07, segs=8)
    frame.add_gear_ring_z(0.39, 0.45, 0.09, segs=24, teeth=8, tooth_h=0.015)
    # Upper mortise rack bar for the 3 sliding bolts
    frame.add_bevel_box(0, 0.34, 0.02, 0.56, 0.09, 0.07)
    nodes.append({"name": "Frame_Base", "mesh": frame, "mat_idx": wood_mat, "translation": [0.0, 0.0, 0.0]})

    # 2. Three interlocking sliding wooden mortise-and-tenon latches: Latch_0, Latch_1, Latch_2
    for i in range(3):
        latch = MeshBuilder()
        # Wooden sliding bolt body + brass grip knob
        latch.add_bevel_box(0, 0, 0, 0.065, 0.16, 0.045)
        latch.add_cylinder_z(0, 0.04, 0.032, 0.024, 0.035, segs=16)
        x_pos = (i - 1) * 0.16
        nodes.append({"name": f"Latch_{i}", "mesh": latch, "mat_idx": brass_mat if i == 1 else wood_mat, "translation": [x_pos, 0.34, 0.045]})

    # 3. Three concentric rotating gear rings: Ring_0 (Outer), Ring_1 (Middle), Ring_2 (Inner)
    ring_specs = [
        ("Ring_0", 0.27, 0.36, 0.055, 6, 0.026, wood_mat, 0.0),
        ("Ring_1", 0.17, 0.25, 0.065, 6, 0.022, brass_mat, 0.01),
        ("Ring_2", 0.08, 0.15, 0.075, 6, 0.018, wood_mat, 0.02),
    ]
    for r_name, ir, orad, depth, teeth, th, mat_i, z_off in ring_specs:
        rm = MeshBuilder()
        rm.add_gear_ring_z(ir, orad, depth, segs=36, teeth=teeth, tooth_h=th)
        nodes.append({"name": r_name, "mesh": rm, "mat_idx": mat_i, "translation": [0.0, -0.04, z_off]})

    # 4. Three planetary brass pinions around the frame: Gear_0, Gear_1, Gear_2
    for g in range(3):
        ang = (g / 3.0) * math.tau + math.pi / 6.0
        gx = math.cos(ang) * 0.39
        gy = -0.04 + math.sin(ang) * 0.39
        gm = MeshBuilder()
        gm.add_gear_ring_z(0.018, 0.058, 0.045, segs=24, teeth=8, tooth_h=0.014)
        nodes.append({"name": f"Gear_{g}", "mesh": gm, "mat_idx": brass_mat, "translation": [gx, gy, 0.035]})

    # 5. Central push spindle: Core_Spindle
    core = MeshBuilder()
    core.add_cylinder_z(0, 0, 0, 0.068, 0.095, segs=20)
    core.add_bevel_box(0, 0, 0.045, 0.085, 0.024, 0.025)
    nodes.append({"name": "Core_Spindle", "mesh": core, "mat_idx": iron_mat, "translation": [0.0, -0.04, 0.035]})

    return nodes


def build_dock_winch_mechanism(textures_spec):
    nodes = []
    # A-frame wooden dock stanchion
    base = MeshBuilder()
    base.add_bevel_box(-0.26, 0.28, 0.0, 0.09, 0.58, 0.28)
    base.add_bevel_box(0.26, 0.28, 0.0, 0.09, 0.58, 0.28)
    base.add_bevel_box(0.0, 0.04, 0.0, 0.68, 0.08, 0.42)
    nodes.append({"name": "Frame_Base", "mesh": base, "mat_idx": 0, "translation": [0.0, 0.0, 0.0]})

    # Horizontal winding drum
    drum = MeshBuilder()
    drum.add_cylinder_z(0, 0, 0, 0.14, 0.44, segs=20)
    nodes.append({"name": "Core_Spindle", "mesh": drum, "mat_idx": 2, "translation": [0.0, 0.36, 0.0]})

    # Brass ratchet wheel + 6 spokes
    wheel = MeshBuilder()
    wheel.add_gear_ring_z(0.18, 0.28, 0.05, segs=36, teeth=12, tooth_h=0.024)
    for s in range(6):
        a = (s / 6.0) * math.pi
        wheel.add_bevel_box(0, 0, 0, math.cos(a) * 0.52 + 0.04, math.sin(a) * 0.52 + 0.04, 0.035)
    nodes.append({"name": "Ring_0", "mesh": wheel, "mat_idx": 1, "translation": [0.0, 0.36, 0.22]})

    return nodes


def main():
    proj_root = r"04_游戏产品\eggboy-still-delivering"
    wood_glb = os.path.join(proj_root, r"Assets\TideTown\SourceGLB\Props_School\C4_狗窝木箱_后续修改版.glb")
    brass_glb = os.path.join(proj_root, r"Assets\TideTown\SourceGLB\Props_Lighthouse\D3_望远镜_v2.glb")
    iron_glb = os.path.join(proj_root, r"Assets\TideTown\SourceGLB\Props_Bakery\A6_台秤_v2.glb")

    wood_bytes, wood_mime = extract_first_png_from_glb(wood_glb)
    brass_bytes, brass_mime = extract_first_png_from_glb(brass_glb)
    iron_bytes, iron_mime = extract_first_png_from_glb(iron_glb)

    textures_spec = [
        {"name": "Lux3D_WarmTimber", "bytes": wood_bytes, "mime": wood_mime, "baseColor": [1.0, 0.92, 0.82, 1.0], "metallic": 0.08, "roughness": 0.72},
        {"name": "Lux3D_AgedBrass", "bytes": brass_bytes, "mime": brass_mime, "baseColor": [1.0, 0.88, 0.62, 1.0], "metallic": 0.68, "roughness": 0.34},
        {"name": "Lux3D_WarmBronze", "bytes": iron_bytes, "mime": iron_mime, "baseColor": [0.92, 0.78, 0.64, 1.0], "metallic": 0.45, "roughness": 0.48},
    ]

    out_dirs = [
        os.path.join(proj_root, r"Assets\TideTown\SourceGLB\Mechanisms"),
        os.path.join(proj_root, r"Assets\TideTown\Resources\Mechanisms"),
        r"04_游戏产品\eggboy-still-delivering-master\Assets\TideTown\SourceGLB\Mechanisms",
        r"04_游戏产品\eggboy-still-delivering-master\Assets\TideTown\Resources\Mechanisms",
    ]

    m1_nodes = build_luban_three_ring_mechanism(textures_spec, 0, 1, 2)
    m2_nodes = build_luban_three_ring_mechanism(textures_spec, 0, 2, 1)
    m3_nodes = build_dock_winch_mechanism(textures_spec)
    m4_nodes = build_luban_three_ring_mechanism(textures_spec, 1, 2, 1)

    for d in out_dirs:
        export_multi_node_glb(os.path.join(d, "M1_鲁班三环风门机关.glb"), m1_nodes, textures_spec)
        export_multi_node_glb(os.path.join(d, "M2_小黑爪印转盘木匣.glb"), m2_nodes, textures_spec)
        export_multi_node_glb(os.path.join(d, "M3_潮汐船坞齿轮绞盘.glb"), m3_nodes, textures_spec)
        export_multi_node_glb(os.path.join(d, "M4_守塔人黄铜三轴星盘.glb"), m4_nodes, textures_spec)


if __name__ == "__main__":
    main()
