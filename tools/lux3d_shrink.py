# 把 Lux3D 原始 GLB（2048 PNG 贴图，5–11 MB）压成游戏用的小 GLB（贴图缩到 N、JPEG）。
#   blender -b --factory-startup -P tools/lux3d_shrink.py -- <in.glb> <out.glb> [贴图边长=512]
# 只动贴图分辨率和编码，网格、UV、材质结构不变。
import bpy, sys

argv = sys.argv[sys.argv.index('--') + 1:]
src, dst = argv[0], argv[1]
size = int(argv[2]) if len(argv) > 2 else 512

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for img in bpy.data.images:
    if img.size[0] > size or img.size[1] > size:
        k = size / max(img.size[0], img.size[1])
        img.scale(max(1, int(img.size[0] * k)), max(1, int(img.size[1] * k)))
tris = sum(len(o.data.loop_triangles) if o.data.loop_triangles else len(o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_image_format='JPEG', export_jpeg_quality=88,
                          export_animations=False, export_yup=True)
print('SHRUNK', dst, 'polys', tris)
