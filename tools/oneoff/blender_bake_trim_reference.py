"""Blender script: reference maps of the Rivendell trim sheet, baked from the high-poly onto trim_low
with Cycles. It stands in for the later Substance Painter bake and shows what that bake must give.

    blender-launcher.exe -b --factory-startup -P blender_bake_trim_reference.py -- --out <dir>
        [--blend <trim_highpoly.blend>] [--cage 0.03] [--ray 0.06] [--ao-samples 128] [--ao-distance 0.04]
        [--keep-low-visible]

Reads <dir>/trim_highpoly.blend (from blender_build_trim_highpoly.py). Writes float arrays to
<dir>/maps_raw/{height_mm,normal,ao}.npy, 1024 x 1024 with image row 0 at the TOP (V = 1):
  height_mm  the Z of the surface in millimetres (an emission bake of the world position)
  normal     the tangent normal as Blender writes it, 0 to 1, green UP (the OpenGL form)
  ao         ambient occlusion, 1 where open
Report: <dir>/_report/DONE.txt (blender_job). rivendell_trim_maps.py turns the arrays into the PNGs.

The rays start at the plane lifted by --cage along its normal (+Z) and look down for at most --ray, so
they pass the whole relief (the highest point is Z_LIMIT = 25 mm, the lowest 0.5 mm).
"""
import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "blender"))
import blender_job as bj  # noqa: E402

SIZE = 1024


def height_material(bpy):
    mat = bpy.data.materials.new("height_mm")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    geo = nodes.new("ShaderNodeNewGeometry")
    sep = nodes.new("ShaderNodeSeparateXYZ")
    mul = nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = 1000.0
    comb = nodes.new("ShaderNodeCombineXYZ")
    emit = nodes.new("ShaderNodeEmission")
    outn = nodes.new("ShaderNodeOutputMaterial")
    links.new(geo.outputs["Position"], sep.inputs["Vector"])
    links.new(sep.outputs["Z"], mul.inputs[0])
    for axis in ("X", "Y", "Z"):
        links.new(mul.outputs[0], comb.inputs[axis])
    links.new(comb.outputs["Vector"], emit.inputs["Color"])
    links.new(emit.outputs["Emission"], outn.inputs["Surface"])
    return mat


def target_material(bpy):
    mat = bpy.data.materials.new("bake_target")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    tex = nodes.new("ShaderNodeTexImage")
    nodes.active = tex
    return mat, tex


def read_image(np, img):
    arr = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    return arr.reshape(img.size[1], img.size[0], 4)[::-1]  # row 0 at the top


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--blend")
    ap.add_argument("--cage", type=float, default=0.03)
    ap.add_argument("--ray", type=float, default=0.06)
    ap.add_argument("--ao-samples", type=int, default=128)
    ap.add_argument("--ao-distance", type=float, default=0.04)
    ap.add_argument("--keep-low-visible", action="store_true")
    ap.add_argument("--tag", default="")
    args = ap.parse_args(bj.script_args())
    import bpy
    import numpy as np
    with bj.job(bj.report_path(args.out)) as rep:
        bpy.ops.wm.open_mainfile(filepath=args.blend or os.path.join(args.out, "trim_highpoly.blend"))
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.cycles.use_denoising = False
        scene.cycles.samples = 4
        if scene.world is None:
            scene.world = bpy.data.worlds.new("trim_world")
        scene.world.light_settings.distance = args.ao_distance
        highs = [o for o in bpy.data.objects if o.name.endswith("_high")]
        lows = [o for o in bpy.data.objects if o.name.endswith("_low")]
        whole = bpy.data.objects["trim_low"]
        rep["highs"] = sorted(o.name for o in highs)
        if not args.keep_low_visible:
            for o in lows:
                o.visible_camera = o.visible_diffuse = o.visible_glossy = False
                o.visible_transmission = o.visible_volume_scatter = o.visible_shadow = False
        mat, tex = target_material(bpy)
        whole.data.materials.clear()
        whole.data.materials.append(mat)
        hmat = height_material(bpy)
        out_dir = os.path.join(args.out, "maps_raw" + args.tag)
        os.makedirs(out_dir, exist_ok=True)

        def bake(kind, name, float_buffer, **extra):
            img = bpy.data.images.new(name, SIZE, SIZE, alpha=False, float_buffer=float_buffer)
            img.colorspace_settings.name = "Non-Color"
            tex.image = img
            bpy.ops.object.select_all(action="DESELECT")
            for o in highs:
                o.select_set(True)
            whole.select_set(True)
            bpy.context.view_layer.objects.active = whole
            bpy.ops.object.bake(type=kind, use_selected_to_active=True, cage_extrusion=args.cage,
                                max_ray_distance=args.ray, margin=0, use_clear=True, **extra)
            return read_image(np, img)

        for o in highs:
            o.data.materials.clear()
            o.data.materials.append(hmat)
        h = bake("EMIT", "height", True)[..., 0]
        np.save(os.path.join(out_dir, "height_mm.npy"), h)
        rep["height_mm"] = {"min": float(h.min()), "max": float(h.max()), "mean": float(h.mean())}

        n = bake("NORMAL", "normal", True, normal_space="TANGENT", normal_r="POS_X", normal_g="POS_Y",
                 normal_b="POS_Z")[..., :3]
        np.save(os.path.join(out_dir, "normal.npy"), n)
        rep["normal"] = {"min": float(n.min()), "max": float(n.max()), "mean_rgb": [float(x) for x in n.mean((0, 1))]}

        scene.cycles.samples = args.ao_samples
        ao = bake("AO", "ao", True)[..., 0]
        np.save(os.path.join(out_dir, "ao.npy"), ao)
        rep["ao"] = {"min": float(ao.min()), "max": float(ao.max()), "mean": float(ao.mean())}
        rep["settings"] = {"cage": args.cage, "ray": args.ray, "ao_samples": args.ao_samples,
                           "ao_distance": args.ao_distance, "low_hidden_from_rays": not args.keep_low_visible,
                           "size": SIZE, "blender": bpy.app.version_string}


main()
