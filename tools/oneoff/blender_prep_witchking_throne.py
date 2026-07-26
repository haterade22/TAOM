"""
Prep the Tripo-generated Witch-king throne FBX for the Bannerlord Mordor kit.

One prop, full treatment: scale to 2.5 m, re-pivot to base centre, rename to
sm_mordor_mm_throne_001 (material slot t_mordor_mm_throne), REPLACE the
fragmented Tripo auto-UV atlas with a Smart-UV unwrap, and rebake every map
from the original textures onto the new layout (selected-to-active, identical
geometry so the cage rays land exactly):

    basecolor / roughness / metallic  -> EMIT bakes through the source JPEGs
    normal                            -> tangent NORMAL bake (captures the
                                         source normal-map perturbation)
    ao                                -> fresh geometry AO bake (Tripo ships none)

Then a bo_ collision twin (decimated, physics slot 'stone' — Erebor
precedent), one FBX to AssetSources/Scenes/Mordor/, plain baked maps to the
staging dir (they double as Substance Painter starting layers), and a Cycles
preview render so the result can be eyeballed without opening the editor.

Blender on this machine is the Microsoft Store app — raw blender.exe is
ACL-blocked; the launcher DETACHES (no stdout). Completion protocol:
<staging>\\_report\\DONE.txt (status json), progress in log.txt next to it.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P e:\\repos\\TAOM\\tools\\oneoff\\blender_prep_witchking_throne.py

Defaults are wired for this asset; --src/--dst/--staging/--height/... override.
Texture packing to t_mordor_mm_throne_{d,n,s} is the separate
convert_tripo_prop_textures.py (also the Substance round-trip converter).
"""

import argparse
import json
import math
import os
import sys
import traceback

import bpy
import bmesh

DEFAULT_SRC = r"C:\Users\mikew\Downloads\Witch+King+Throne\tripo_convert_a637c12f-1377-492f-98d4-6f0f730199f3.fbx"
DEFAULT_DST = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\Mordor"
DEFAULT_STAGING = r"E:\LOTRAOMAssets\_export\witchking_throne"

# Tripo .fbm texture roles -> bake pass. rm (packed) ignored: separate
# roughness/metallic JPEGs ship alongside it.
SOURCE_MAPS = {
    "basecolor": "Witch_King_Throne_basecolor.JPEG",
    "normal": "Witch_King_Throne_normal.JPEG",
    "roughness": "Witch_King_Throne_roughness.JPEG",
    "metallic": "Witch_King_Throne_metallic.JPEG",
}

LOG_LINES = []
REPORT_DIR = None


def log(msg):
    LOG_LINES.append(str(msg))
    print(msg)
    if REPORT_DIR:
        with open(os.path.join(REPORT_DIR, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG_LINES))


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bake_world(objs):
    """Data-level world bake — transform_apply silently skips multi-user data,
    and matrix_world is stale right after import in background mode (both
    lessons from the Rivendell run, docs/reviews/lessons/build-tooling-workflow.md)."""
    from mathutils import Matrix
    bpy.context.view_layer.update()
    for o in objs:
        if o.data.users > 1:
            o.data = o.data.copy()
        mw = o.matrix_world.copy()
        o.data.transform(mw)
        if mw.determinant() < 0:
            o.data.flip_normals()
        o.matrix_world = Matrix.Identity(4)
    bpy.context.view_layer.update()


def scale_and_pivot(obj, target_height):
    """Uniform-scale so bbox Z-size == target_height; pivot at bbox
    bottom-centre (the Tripo export is already there, but derive it — don't
    trust it)."""
    from mathutils import Matrix, Vector
    bpy.context.view_layer.update()
    bb = [Vector(c) for c in obj.bound_box]
    zsize = max(v[2] for v in bb) - min(v[2] for v in bb)
    s = target_height / zsize
    obj.data.transform(Matrix.Scale(s, 4))
    bpy.context.view_layer.update()
    bb = [Vector(c) for c in obj.bound_box]
    origin = Vector((sum(v[0] for v in bb) / 8.0, sum(v[1] for v in bb) / 8.0,
                     min(v[2] for v in bb)))
    obj.data.transform(Matrix.Translation(-origin))
    bpy.context.view_layer.update()
    return s


def count_uv_islands(obj):
    """UV-connectivity island count (faces connected where the shared edge's
    loop UVs coincide). Reported before/after so the re-UV win is measurable."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    if uv is None:
        bm.free()
        return 0
    bm.faces.ensure_lookup_table()
    parent = list(range(len(bm.faces)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    eps = 1e-6
    for edge in bm.edges:
        faces = edge.link_faces
        if len(faces) != 2:
            continue
        fa, fb = faces
        la = [l for l in fa.loops if l.edge == edge or l.link_loop_next.edge == edge]
        uvs_a = sorted((tuple(l[uv].uv) for l in fa.loops if l.vert in edge.verts))
        uvs_b = sorted((tuple(l[uv].uv) for l in fb.loops if l.vert in edge.verts))
        if len(uvs_a) == len(uvs_b) and all(
                abs(a[0] - b[0]) < eps and abs(a[1] - b[1]) < eps
                for a, b in zip(uvs_a, uvs_b)):
            union(fa.index, fb.index)
    islands = len({find(i) for i in range(len(bm.faces))})
    bm.free()
    return islands


def smart_uv(obj, island_margin, angle_deg):
    select_only([obj])
    # wipe the Tripo atlas entirely — the new layout replaces it
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    obj.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle_deg),
                             island_margin=island_margin,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def chart_uv(obj, spread_deg, margin, min_faces=20):
    """xatlas-style charting: BFS region-growing over face connectivity gated
    on angle to the chart's area-weighted average normal, small-fragment
    absorption into the most-shared-boundary neighbor, planar projection per
    chart, per-chart texel-density equalization, then pack. Smart UV Project
    fragments dense organic triangulation (probe: 1485-2112 islands at 17-24%%
    utilization vs the Tripo atlas's 298 at 53%%); large-spread region growing
    + fragment merging is what actually produces paintable islands there.
    Returns (chart_count, uv_flipped_faces) — flipped faces are projection
    fold-over telemetry (merged fragments may project through a neighbor's
    basis; tiny counts are cosmetically invisible, big counts mean the spread
    is too aggressive)."""
    from mathutils import Vector
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.new("UVMap")

    spread_cos = math.cos(math.radians(spread_deg))
    visited = set()
    chart_of = {}
    charts = []       # chart id -> face list
    normal_sums = []  # chart id -> unnormalized area-weighted normal
    for f in sorted(bm.faces, key=lambda f: -f.calc_area()):
        if f.index in visited:
            continue
        cid = len(charts)
        normal_sum = f.normal * max(f.calc_area(), 1e-12)
        chart = [f]
        visited.add(f.index)
        chart_of[f.index] = cid
        queue = [f]
        while queue:
            cur = queue.pop()
            avg = normal_sum.normalized() if normal_sum.length > 1e-12 else cur.normal
            for e in cur.edges:
                for nb in e.link_faces:
                    if nb.index in visited or nb.normal.dot(avg) <= spread_cos:
                        continue
                    visited.add(nb.index)
                    chart_of[nb.index] = cid
                    chart.append(nb)
                    queue.append(nb)
                    normal_sum += nb.normal * max(nb.calc_area(), 1e-12)
        charts.append(chart)
        normal_sums.append(normal_sum)

    # absorb fragments: a sub-min_faces chart merges into whichever neighbor
    # chart shares the most boundary edges (two passes: pass 1 may leave a
    # fragment whose only neighbor was itself just absorbed)
    for _pass in range(2):
        for cid in sorted(range(len(charts)), key=lambda c: len(charts[c])):
            chart = charts[cid]
            if not chart or len(chart) >= min_faces:
                continue
            shared = {}
            for f in chart:
                for e in f.edges:
                    for nb in e.link_faces:
                        ncid = chart_of[nb.index]
                        if ncid != cid and charts[ncid]:
                            shared[ncid] = shared.get(ncid, 0) + 1
            if not shared:
                continue
            target = max(shared, key=shared.get)
            for f in chart:
                chart_of[f.index] = target
            charts[target].extend(chart)
            normal_sums[target] += normal_sums[cid]
            charts[cid] = []

    flipped_total = 0
    live = [(charts[c], normal_sums[c]) for c in range(len(charts)) if charts[c]]
    for chart, nsum in live:
        n = nsum.normalized() if nsum.length > 1e-12 else chart[0].normal
        helper = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
        u_ax = n.cross(helper).normalized()
        v_ax = n.cross(u_ax).normalized()
        area3d = 0.0
        area_uv = 0.0
        pos = neg = 0
        for f in chart:
            area3d += f.calc_area()
            pts = [(l.vert.co.dot(u_ax), l.vert.co.dot(v_ax)) for l in f.loops]
            signed = 0.0
            for i in range(1, len(pts) - 1):
                a, b, c = pts[0], pts[i], pts[i + 1]
                signed += ((b[0] - a[0]) * (c[1] - a[1])
                           - (c[0] - a[0]) * (b[1] - a[1])) / 2.0
            area_uv += abs(signed)
            if signed >= 0:
                pos += 1
            else:
                neg += 1
            for l in f.loops:
                l[uv].uv = (l.vert.co.dot(u_ax), l.vert.co.dot(v_ax))
        flipped_total += min(pos, neg)
        # equalize texel density: tilted-face projection shrinks UV area
        scale = math.sqrt(area3d / max(area_uv, 1e-12))
        lo_u = min(l[uv].uv.x for f in chart for l in f.loops)
        lo_v = min(l[uv].uv.y for f in chart for l in f.loops)
        for f in chart:
            for l in f.loops:
                l[uv].uv = ((l[uv].uv.x - lo_u) * scale, (l[uv].uv.y - lo_v) * scale)

    bm.to_mesh(obj.data)
    bm.free()

    select_only([obj])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    try:
        bpy.ops.uv.pack_islands(margin=margin)
    except Exception as e:
        log(f"[uv] pack_islands failed ({e}); falling back to shelf packing")
        bpy.ops.object.mode_set(mode="OBJECT")
        shelf_pack(obj, margin)
        return len(live), flipped_total
    bpy.ops.object.mode_set(mode="OBJECT")
    return len(live), flipped_total


def shelf_pack(obj, margin):
    """Naive row packer over UV islands — only the safety net for the
    pack_islands op failing headless."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    bm.faces.ensure_lookup_table()
    parent = list(range(len(bm.faces)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    eps = 1e-6
    for edge in bm.edges:
        faces = edge.link_faces
        if len(faces) == 2:
            fa, fb = faces
            uvs_a = sorted(tuple(l[uv].uv) for l in fa.loops if l.vert in edge.verts)
            uvs_b = sorted(tuple(l[uv].uv) for l in fb.loops if l.vert in edge.verts)
            if len(uvs_a) == len(uvs_b) and all(
                    abs(a[0] - b[0]) < eps and abs(a[1] - b[1]) < eps
                    for a, b in zip(uvs_a, uvs_b)):
                ra, rb = find(fa.index), find(fb.index)
                if ra != rb:
                    parent[rb] = ra
    groups = {}
    for f in bm.faces:
        groups.setdefault(find(f.index), []).append(f)
    islands = []
    for faces in groups.values():
        lo_u = min(l[uv].uv.x for f in faces for l in f.loops)
        hi_u = max(l[uv].uv.x for f in faces for l in f.loops)
        lo_v = min(l[uv].uv.y for f in faces for l in f.loops)
        hi_v = max(l[uv].uv.y for f in faces for l in f.loops)
        islands.append((hi_v - lo_v, hi_u - lo_u, lo_u, lo_v, faces))
    islands.sort(reverse=True, key=lambda t: t[0])
    total_w = sum(w for _h, w, *_ in islands)
    row_width = max(math.sqrt(total_w * sum(h for h, *_ in islands) / max(len(islands), 1)),
                    max(w for _h, w, *_ in islands))
    x = y = row_h = 0.0
    placed = []
    for h, w, lo_u, lo_v, faces in islands:
        if x + w > row_width and x > 0:
            y += row_h + margin
            x, row_h = 0.0, 0.0
        placed.append((x - lo_u, y - lo_v, faces))
        x += w + margin
        row_h = max(row_h, h)
    extent = max(row_width, y + row_h)
    for dx, dy, faces in placed:
        for f in faces:
            for l in f.loops:
                l[uv].uv = ((l[uv].uv.x + dx) / extent, (l[uv].uv.y + dy) / extent)
    bm.to_mesh(obj.data)
    bm.free()


def uv_utilization(obj):
    """Fraction of the 0..1 UV square covered by faces — texel-efficiency
    proxy for comparing unwrap candidates."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    area = 0.0
    for f in bm.faces:
        loops = f.loops
        for i in range(1, len(loops) - 1):
            a, b, c = loops[0][uv].uv, loops[i][uv].uv, loops[i + 1][uv].uv
            area += abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) / 2.0
    bm.free()
    return area


def enable_gpu():
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for dev_type in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = dev_type
            except TypeError:
                continue
            prefs.get_devices()
            used = 0
            for d in prefs.devices:
                d.use = d.type != "CPU"
                used += d.use
            if used:
                bpy.context.scene.cycles.device = "GPU"
                log(f"[gpu] {dev_type} enabled ({used} device(s))")
                return True
    except Exception as e:
        log(f"[gpu] enable failed ({e}); staying on CPU")
    return False


def build_source_material(obj, image, is_normal, colorspace):
    """Bake-source material: EMIT passes feed the source JPEG straight into an
    emission shader; the normal pass needs a real Principled+NormalMap chain so
    the NORMAL bake sees the perturbed shading normal."""
    mat = bpy.data.materials.new("_bake_src_mat")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    image.colorspace_settings.name = colorspace
    if is_normal:
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nmap.space = "TANGENT"
        nt.links.new(tex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    else:
        emit = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(tex.outputs["Color"], emit.inputs["Color"])
        nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return mat


def make_target_bake_material(obj):
    mat = bpy.data.materials.new("_bake_target_mat")
    mat.use_nodes = True
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    nt.nodes.active = tex
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return mat, tex


def save_image(img, path):
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def image_stats(img):
    import numpy as np
    px = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(px)
    rgb = px.reshape(-1, 4)[:, :3]
    return {"min": round(float(rgb.min()), 4), "max": round(float(rgb.max()), 4),
            "mean": round(float(rgb.mean()), 4)}


def run_bakes(source, target, fbm_dir, size, staging, ao_samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    enable_gpu()
    scene.render.bake.margin = 16
    scene.cycles.use_denoising = False

    tgt_mat, tgt_tex = make_target_bake_material(target)
    stats = {}
    passes = [
        ("basecolor", "EMIT", "sRGB", True),
        ("roughness", "EMIT", "Non-Color", True),
        ("metallic", "EMIT", "Non-Color", True),
        ("normal", "NORMAL", "Non-Color", True),
        ("ao", "AO", "Non-Color", False),
    ]
    for name, bake_type, out_space, from_source in passes:
        img = bpy.data.images.new(f"bake_{name}", width=size, height=size,
                                  alpha=False, float_buffer=False)
        img.colorspace_settings.name = out_space
        tgt_tex.image = img
        if from_source and name in SOURCE_MAPS:
            src_img = bpy.data.images.load(os.path.join(fbm_dir, SOURCE_MAPS[name]))
            build_source_material(source, src_img, is_normal=(bake_type == "NORMAL"),
                                  colorspace="sRGB" if name == "basecolor" else "Non-Color")
        if bake_type == "AO":
            scene.cycles.samples = ao_samples
            source.hide_render = True  # duplicate shell would self-shadow the AO
            select_only([target])
            bpy.ops.object.bake(type="AO")
            source.hide_render = False
        else:
            scene.cycles.samples = 16
            select_only([source, target])
            bpy.context.view_layer.objects.active = target
            kwargs = dict(type=bake_type, use_selected_to_active=True,
                          cage_extrusion=0.02, use_clear=True)
            if bake_type == "NORMAL":
                kwargs["normal_space"] = "TANGENT"
            bpy.ops.object.bake(**kwargs)
        out_path = os.path.join(staging, f"{name}.png")
        save_image(img, out_path)
        stats[name] = image_stats(img)
        log(f"[bake] {name}: {stats[name]} -> {out_path}")
    return stats


def build_preview_material(obj, staging):
    mat = bpy.data.materials.new("_preview_mat")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]

    def tex_node(name, space):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(os.path.join(staging, f"{name}.png"))
        n.image.colorspace_settings.name = space
        return n

    base = tex_node("basecolor", "sRGB")
    ao = tex_node("ao", "Non-Color")
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    # Mix node exposes one socket per data_type all named A/B/Result — name
    # lookup returns the disabled Float variant; the Color sockets are 6/7/2.
    mix.inputs[0].default_value = 0.7
    nt.links.new(base.outputs["Color"], mix.inputs[6])
    nt.links.new(ao.outputs["Color"], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    rough = tex_node("roughness", "Non-Color")
    nt.links.new(rough.outputs["Color"], bsdf.inputs["Roughness"])
    metal = tex_node("metallic", "Non-Color")
    nt.links.new(metal.outputs["Color"], bsdf.inputs["Metallic"])
    normal = tex_node("normal", "Non-Color")
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(normal.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def render_preview(obj, staging, height):
    from mathutils import Vector
    scene = bpy.context.scene
    build_preview_material(obj, staging)
    cam_data = bpy.data.cameras.new("preview_cam")
    cam = bpy.data.objects.new("preview_cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    look_at = Vector((0.0, 0.0, height * 0.45))
    cam.location = Vector((2.6, -3.4, height * 0.65))
    cam.rotation_euler = (look_at - cam.location).to_track_quat("-Z", "Y").to_euler()
    sun_data = bpy.data.lights.new("preview_sun", type="SUN")
    sun_data.energy = 3.0
    sun = bpy.data.objects.new("preview_sun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(55), 0.0, math.radians(-35))
    world = bpy.data.worlds.new("preview_world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.12, 0.12, 0.13, 1.0)
    scene.world = world
    scene.cycles.samples = 64
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1200
    scene.render.filepath = os.path.join(staging, "preview.png")
    bpy.ops.render.render(write_still=True)
    log(f"[preview] {scene.render.filepath}")


def export_fbx(path, objs):
    select_only(objs)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        bake_space_transform=True,
        add_leaf_bones=False,
        path_mode="AUTO",
    )


def main():
    global REPORT_DIR
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=DEFAULT_SRC)
    ap.add_argument("--dst", default=DEFAULT_DST)
    ap.add_argument("--staging", default=DEFAULT_STAGING)
    ap.add_argument("--name", default="sm_mordor_mm_throne_001")
    ap.add_argument("--material", default="t_mordor_mm_throne")
    ap.add_argument("--height", type=float, default=2.5)
    ap.add_argument("--bake-size", type=int, default=2048)
    ap.add_argument("--island-margin", type=float, default=0.004)
    ap.add_argument("--angle", type=float, default=66.0)
    ap.add_argument("--uv-method", default="chart", choices=["chart", "smart"])
    ap.add_argument("--spread", type=float, default=60.0,
                    help="chart method: max angle to the chart average normal")
    ap.add_argument("--probe-angles", default="",
                    help="smart-project probe: comma-separated angle limits — "
                         "re-UV at each, report islands+utilization, no bake/export")
    ap.add_argument("--probe-spreads", default="",
                    help="chart-method probe: comma-separated spread angles")
    ap.add_argument("--collision-tris", type=int, default=1500)
    ap.add_argument("--ao-samples", type=int, default=128)
    ap.add_argument("--skip-preview", action="store_true")
    args = ap.parse_args(argv)

    os.makedirs(args.staging, exist_ok=True)
    REPORT_DIR = os.path.join(args.staging, "_report")
    os.makedirs(REPORT_DIR, exist_ok=True)
    done_path = os.path.join(REPORT_DIR, "DONE.txt")
    if os.path.exists(done_path):
        os.remove(done_path)

    summary = {"status": "error"}
    try:
        fbm_dir = os.path.splitext(args.src)[0] + ".fbm"
        for f in SOURCE_MAPS.values():
            p = os.path.join(fbm_dir, f)
            if not os.path.isfile(p):
                raise FileNotFoundError(p)

        bpy.ops.wm.read_factory_settings(use_empty=True)
        try:
            bpy.ops.wm.fbx_import(filepath=args.src)
        except Exception:
            bpy.ops.import_scene.fbx(filepath=args.src)
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
        if not meshes:
            raise RuntimeError("no mesh objects after import")
        if len(meshes) > 1:
            bpy.context.view_layer.update()
            select_only(meshes)
            bpy.ops.object.join()
            meshes = [bpy.context.view_layer.objects.active]
        visual = meshes[0]

        bake_world([visual])
        scale = scale_and_pivot(visual, args.height)
        visual.name = visual.data.name = args.name
        log(f"[prep] scaled x{scale:.4f} -> height {visual.dimensions[2]:.3f} m, "
            f"tris {tri_count(visual)}")

        islands_before = count_uv_islands(visual)

        if args.probe_angles or args.probe_spreads:
            probe = {"tripo_atlas": {"islands": islands_before,
                                     "utilization": round(uv_utilization(visual), 4)}}
            pristine = visual.data.copy()  # each candidate starts from the original
            for ang in (float(a) for a in args.probe_angles.split(",") if a):
                smart_uv(visual, args.island_margin, ang)
                probe[f"smart_{ang:g}"] = {
                    "islands": count_uv_islands(visual),
                    "utilization": round(uv_utilization(visual), 4)}
                log(f"[probe] {probe[f'smart_{ang:g}']}")
                visual.data = pristine.copy()
            for spr in (float(a) for a in args.probe_spreads.split(",") if a):
                charts, flipped = chart_uv(visual, spr, args.island_margin)
                probe[f"chart_{spr:g}"] = {
                    "charts": charts, "flipped_faces": flipped,
                    "islands": count_uv_islands(visual),
                    "utilization": round(uv_utilization(visual), 4)}
                log(f"[probe] spread {spr:g}: {probe[f'chart_{spr:g}']}")
                visual.data = pristine.copy()
            summary = {"status": "ok", "probe": probe}
            with open(done_path, "w", encoding="utf-8") as f:
                json.dump(summary, f, indent=1)
            log("[done] probe complete")
            return

        # bake source keeps the Tripo atlas; target gets the clean unwrap
        bake_src = visual.copy()
        bake_src.data = visual.data.copy()
        bake_src.name = "_bake_src"
        bpy.context.scene.collection.objects.link(bake_src)

        flipped = 0
        if args.uv_method == "chart":
            _charts, flipped = chart_uv(visual, args.spread, args.island_margin)
        else:
            smart_uv(visual, args.island_margin, args.angle)
        islands_after = count_uv_islands(visual)
        log(f"[uv] islands {islands_before} -> {islands_after} (flipped faces: {flipped})")

        stats = run_bakes(bake_src, visual, fbm_dir, args.bake_size,
                          args.staging, args.ao_samples)
        bpy.data.objects.remove(bake_src, do_unlink=True)

        # final material slot: name only — the editor binds by name at import
        visual.data.materials.clear()
        visual.data.materials.append(bpy.data.materials.new(args.material))

        bo = visual.copy()
        bo.data = visual.data.copy()
        bo.name = bo.data.name = f"bo_{args.name}"
        bpy.context.scene.collection.objects.link(bo)
        select_only([bo])
        mod = bo.modifiers.new("coll_decimate", "DECIMATE")
        mod.ratio = args.collision_tris / max(tri_count(bo), 1)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bo.data.materials.clear()
        bo.data.materials.append(bpy.data.materials.new("stone"))
        log(f"[collision] bo_{args.name}: {tri_count(bo)} tris")

        out_path = os.path.join(args.dst, args.name + ".fbx")
        export_fbx(out_path, [visual, bo])
        log(f"[export] {out_path}")

        if not args.skip_preview:
            bo.hide_render = True
            render_preview(visual, args.staging, args.height)

        summary = {
            "status": "ok", "out": out_path,
            "height_m": round(visual.dimensions[2], 3),
            "tris_visual": tri_count(visual), "tris_bo": tri_count(bo),
            "uv_islands_before": islands_before, "uv_islands_after": islands_after,
            "uv_flipped_faces": flipped,
            "bakes": stats,
        }
    except Exception:
        log(traceback.format_exc())
        summary["trace"] = traceback.format_exc(limit=3)

    with open(done_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    log(f"[done] {summary['status']}")


main()
