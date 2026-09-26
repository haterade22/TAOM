#!/usr/bin/env python3
"""
Give a race's hand mesh the 26 hand-pose channels by transferring them from a reference race (2026-09-26, the hill
troll from the Gundabad pale uruk's hand).

WHY
The human skeleton has no finger bones below finger0, so the engine closes a hand with shape keys: every working
race carries 26 channels on its LOD0 arm or hand mesh (the uruk's SK_Uruk_Hai_BM_A_Arms, the pale uruk's _Hand, the
dwarf's arms), none on the LODs, and the engine takes them by order. KEYForce's hill troll hands had none, so its
fingers never closed around a weapon (the cave troll has none either; neither crashes).

THE REFERENCE
Use a HAND mesh, not an arms mesh. The first cut took the Isengard uruk's SK_Uruk_Hai_BM_A_Arms, whose <side>_hand
region ends mid-forearm where its channels still move, and it moved the troll's wrist seam (40 open-boundary vertices
sitting on hill_troll_a_body) by up to 16% of the channel peak: a torn wrist. Artist hands move their seam under 2%
(the pale uruk's hand 0.9%). The reference must carry exactly `--channels` (default 26) channels.

WHAT IT DOES
Per side, the reference's hand vertices (those whose strongest weight is <side>_hand) and the target's are put in
one frame each, built from geometry rather than the bones (the troll's re-framed hand bone points about 50 degrees
off its fingers): F from the wrist (<side>_hand head) to the knuckles (<side>_finger0 head), N the hand's thinnest
axis orthogonal to F with its sign on the palm side, W = F x N. The palm side is where the fingers curl. On the
reference it is read from the channels themselves (the fingertips' mean displacement); a hand's rest pose cannot
say it (the uruk's flat rest hand read it backwards), so the target takes it from the THUMB: T, the side of the
thumb along the width axis (the side whose proximal half reaches farther), and the reference's own relation between
N and F x T for that hand, which the two reference hands must give with opposite signs (mirror images) or the tool
refuses. The target's two palm normals must be mirror images too: the left one, reflected across the axis from the
r_hand head to the l_hand head, must meet the right one at a dot of at least 0.5. Each axis is scaled by the ratio
of the two hands' extents. The reference hands, every channel included, are mapped through that fit onto the
target's hands as one temporary mesh; a plain copy of the target hands is bound to it with Blender's Surface Deform
(--falloff, default 4); for each channel the fitted mesh's own vertices are moved to that channel's positions and
the displacement is read back from the deformed copy. (Driving the fitted mesh through shape keys instead moved half
the target 4.8 m even for a channel identical to its basis, with the fitted mesh itself evaluating exactly; plain
vertex positions do not.) Surface Deform carries every vertex's offset in its bound faces' own frames, so a thick
troll finger turns with the thin uruk finger under it instead of shearing flat, which copying displacements did (the
first cut crumpled every fingertip). A light smoothing over the target's own edges follows (--smooth, default 2).
Then the seam is pinned. The seam is every open-boundary vertex plus every moved vertex next to one the transfer
does not move; each moved vertex's displacement is scaled by its edge-ring distance from the seam over --seam-rings
(default 3), capped at 1, so the seam stays where the body meets it and each channel fades in over three rings.
Channels keep the reference's order, named <prefix>01..26, each at weight 0 as in the artist files (Blender 5.2
adds a key at weight 1, which the export writes as DeformPercent 100: the first cut shipped all 26 poses applied);
the temporary objects are deleted before the export.

THE SEAM GATE
The largest offset of any seam vertex over every channel, read back from the written keys (`seam.max` in the
report), must be at most 2% of the largest channel peak; above it the run records a diff and refuses --apply.

SAFETY (the add_face_morph_channels.py shape): a staged `<stem>.handmorphs.fbx` is written and re-imported, and
`add_mesh_lods.compare(rekeyed=)` checks it: every object must come back with its geometry, the target with exactly
the new channels, every other object with its keys untouched, and no object new. A clean dry run deletes the staged
copy. `--apply` writes a write-once `<fbx>.bak-handmorphs` first, then replaces the FBX. Check the result without
Blender: `python tools/audit_fbx_lods.py --diff <fbx>.bak-handmorphs <fbx>`.
`--preview <dir>` renders both hands of the reference and of the target at rest (`_00`) and at each channel
(workbench), with the meshes stitched to the hand mesh visible so a gap at the seam shows, and writes nothing else.
Its report says "mode": "preview" and "ok": null: a picture is not a verification.
The report `<fbx>.handmorphs-report.json` carries the fit per side (vertices, per-axis scales, thumb side, palm
normal), the palm mirror dot, the peak offset per channel and the seam numbers per channel.

    blender-launcher.exe -b --factory-startup -P tools/blender/transfer_hand_morphs.py -- \\
        --source <SK_GB_Pale_Uruk_Basemesh_A.fbx> --source-mesh SK_Pale_Uruk_BM_A_Hand \\
        --fbx <hill_troll_a.fbx> --target-mesh hill_troll_a_hands [--smooth 6] [--preview <dir>] [--apply]
The launcher detaches: poll the report until "stage" is "done". Then a Kit re-import.
"""
import json
import os
import re
import shutil
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import add_mesh_lods as aml  # noqa: E402

SIDES = ("l", "r")
SEAM_LIMIT = 0.02      # of the largest channel peak; artist hands move their seam under 2%
MIRROR_MIN = 0.5
LOD = re.compile(r"[._]lod\d+$", re.I)
CASTS = {"--falloff": float, "--smooth": int, "--channels": int, "--seam-rings": int}
STRINGS = ("--source", "--source-mesh", "--fbx", "--target-mesh", "--preview", "--prefix")


# --------------------------------------------------------------------------- #
# pure helpers (unit-tested without Blender)
# --------------------------------------------------------------------------- #
def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "preview": None, "falloff": 4.0, "smooth": 2, "prefix": "shape_", "channels": 26,
           "seam_rings": 3}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--apply":
            out["apply"] = True
            i += 1
        elif (a in STRINGS or a in CASTS) and i + 1 < len(argv):
            out[a[2:].replace("-", "_")] = CASTS.get(a, str)(argv[i + 1])
            i += 2
        else:
            raise SystemExit("bad argument %r" % a)
    for k in ("source", "source_mesh", "fbx", "target_mesh"):
        if not out.get(k):
            raise SystemExit("--%s is required" % k.replace("_", "-"))
    if out["apply"] and out["preview"]:
        raise SystemExit("--preview writes nothing; run it without --apply")
    if out["channels"] < 1:
        raise SystemExit("--channels must be at least 1")
    if out["seam_rings"] < 1:
        raise SystemExit("--seam-rings must be at least 1: the seam is always pinned")
    return out


def poly_adjacency(polys):
    adj = {}
    for p in polys:
        for k in range(len(p)):
            a, b = p[k], p[(k + 1) % len(p)]
            adj.setdefault(a, set()).add(b)
            adj.setdefault(b, set()).add(a)
    return adj


def seam_weights(polys, moved, rings):
    """({moved vertex: weight}, seam). The seam is every open-boundary vertex plus every moved vertex with an
    unmoved neighbour. A moved vertex weighs its edge-ring distance from the seam over `rings`, capped at 1: 0 on
    the seam, 1 from `rings` rings in, and 1 when no path joins it to the seam."""
    moved = set(moved)
    adj = poly_adjacency(polys)
    seam = set(aml.boundary_vertices(polys))
    seam |= {v for v in moved if any(n not in moved for n in adj.get(v, ()))}
    dist = {v: 0 for v in seam & moved}
    frontier = list(dist)
    while frontier:
        nxt = []
        for v in frontier:
            for n in adj.get(v, ()):
                if n in moved and n not in dist:
                    dist[n] = dist[v] + 1
                    nxt.append(n)
        frontier = nxt
    return {v: min(1.0, dist[v] / float(rings)) if v in dist else 1.0 for v in moved}, seam


def weigh(displacement, weights):
    """Each channel's displacement field scaled by its vertex's seam weight."""
    return [{vi: d * weights[vi] for vi, d in disp.items()} for disp in displacement]


def seam_gate(seam_by_channel, peaks, limit=SEAM_LIMIT):
    """(seam max, allowed, diff or None): no seam vertex may move more than `limit` of the largest channel peak."""
    seam_max = max(seam_by_channel, default=0.0)
    allowed = limit * max(peaks, default=0.0)
    if seam_max <= allowed:
        return seam_max, allowed, None
    return seam_max, allowed, ("the seam moves %.4f, over %g%% of the largest channel peak (%.4f): the wrist tears"
                               % (seam_max, limit * 100, max(peaks, default=0.0)))


def mirror_dot(n_left, n_right, r_head, l_head):
    """The left palm normal reflected across the right-to-left hand axis, dotted with the right palm normal: 1 for
    mirror-image hands, negative when one palm reading is flipped."""
    axis = np.asarray(list(l_head), float) - np.asarray(list(r_head), float)
    axis /= np.linalg.norm(axis)
    left = np.asarray(list(n_left), float)
    return float((left - 2.0 * left.dot(axis) * axis).dot(np.asarray(list(n_right), float)))


# --------------------------------------------------------------------------- #
# Blender side
# --------------------------------------------------------------------------- #
def armature_of(obj):
    arm = obj.parent if obj.parent and obj.parent.type == "ARMATURE" else None
    if arm is None:
        arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    if arm is None:
        raise SystemExit("no armature for %s" % obj.name)
    return arm


def bone_head(arm, name):
    b = arm.data.bones.get(name)
    if b is None:
        raise SystemExit("armature %s has no bone %s" % (arm.name, name))
    return arm.matrix_world @ b.head_local


def side_indices(obj, side):
    """Vertices whose strongest weight is <side>_hand (the whole hand, the fingers included)."""
    names = {g.index: g.name for g in obj.vertex_groups}
    want = side + "_hand"
    out = []
    for v in obj.data.vertices:
        if v.groups and names.get(max(v.groups, key=lambda g: g.weight).group) == want:
            out.append(v.index)
    if not out:
        raise SystemExit("%s has no vertex weighted mostly to %s" % (obj.name, want))
    return out


def hand_frame(points, wrist, knuckle):
    """(origin, F, W, N) with N the thinnest axis orthogonal to F; N's sign is fixed by the caller."""
    f = (knuckle - wrist).normalized()
    p = np.array([list(x) for x in points]) - np.array(list(wrist))
    fa = np.array(list(f))
    q = p - np.outer(p @ fa, fa)                     # the cloud with the finger axis removed
    vals, vecs = np.linalg.eigh(np.cov(q.T))
    n = Vector(vecs[:, int(np.argmin(np.where(vals > 1e-12, vals, np.inf)))])
    n = (n - f * n.dot(f)).normalized()
    return wrist, f, f.cross(n).normalized(), n


def local(p, frame):
    o, f, w, n = frame
    d = p - o
    return np.array([d.dot(f), d.dot(w), d.dot(n)])


def thumb_sign(points, frame, margin=1.08):
    """+1 when the thumb is on +W: along the proximal half of the hand (F between its 15th and 60th percentiles) one
    side of the width axis reaches farther, and that is the thumb. None when neither side leads by `margin`."""
    loc = np.array([local(p, frame) for p in points])
    lo, hi = np.percentile(loc[:, 0], 15), np.percentile(loc[:, 0], 60)
    mid = loc[(loc[:, 0] >= lo) & (loc[:, 0] <= hi)][:, 1] - np.median(loc[:, 1])
    pos, neg = np.percentile(mid, 99), -np.percentile(mid, 1)
    if max(pos, neg) < margin * min(pos, neg):
        return None
    return 1 if pos > neg else -1


def palm_frame(frame, thumb, handed):
    """The frame with N = handed * (F x T) (palm side) and W = F x N, T the thumb direction along W."""
    o, f, w, n = frame
    t = w * thumb
    n2 = (f.cross(t) * handed).normalized()
    return o, f, f.cross(n2).normalized(), n2


def extents(loc):
    return np.percentile(loc, 98, axis=0) - np.percentile(loc, 2, axis=0)


def read_source(path, mesh_name, channels):
    aml.load(path)
    obj = bpy.data.objects.get(mesh_name)
    if obj is None or obj.type != "MESH":
        raise SystemExit("no mesh %r in %s" % (mesh_name, path))
    keys = obj.data.shape_keys.key_blocks if obj.data.shape_keys else []
    if len(keys) - 1 != channels:
        raise SystemExit("%s has %d channels, not the %d the engine takes by order (--channels)"
                         % (mesh_name, max(len(keys) - 1, 0), channels))
    arm = armature_of(obj)
    rot = obj.matrix_world.to_3x3()
    basis = keys[0]
    sides = {}
    for s in SIDES:
        idx = side_indices(obj, s)
        pts = [obj.matrix_world @ basis.data[i].co for i in idx]
        frame = hand_frame(pts, bone_head(arm, s + "_hand"), bone_head(arm, s + "_finger0"))
        # the channels' own answer for the palm side: fingertips move palm-ward on a closing hand
        loc = np.array([local(p, frame) for p in pts])
        far = loc[:, 0] >= np.percentile(loc[:, 0], 80)
        disp = []
        for k in keys[1:]:
            d = np.array([list(rot @ (k.data[i].co - basis.data[i].co)) for i in idx])
            disp.append(d)
        o, f, w, n = frame
        n_mean = float(np.mean([d[far] @ np.array(list(n)) for d in disp]))
        palm = n * (1 if n_mean >= 0 else -1)                                   # the channels' palm side
        thumb = thumb_sign(pts, frame)
        if thumb is None:
            raise SystemExit("source side %s: no side of the hand leads; the thumb cannot be found" % s)
        handed = 1 if f.cross(w * thumb).dot(palm) >= 0 else -1
        frame = palm_frame(frame, thumb, handed)
        basis_mat = np.array([list(frame[1]), list(frame[2]), list(frame[3])])     # rows F, W, N
        at = {vi: j for j, vi in enumerate(idx)}
        faces = [[at[v] for v in poly.vertices] for poly in obj.data.polygons if all(v in at for v in poly.vertices)]
        sides[s] = {"loc": np.array([local(p, frame) for p in pts]),
                    "disp": [d @ basis_mat.T for d in disp],                      # per channel, in (F, W, N)
                    "faces": faces, "handed": handed}
    if sides["l"]["handed"] == sides["r"]["handed"]:
        raise SystemExit("the reference's two hands give the same palm-to-thumb relation; mirror images must not, "
                         "so the thumb or palm reading is wrong on one of them")
    return {"names": [k.name for k in keys[1:]], "sides": sides}


def target_fit(obj, arm, source):
    """Per side: (frame, per-axis scale, target vertex indices, thumb) fitting the reference hand onto the target's."""
    fits = {}
    for s in SIDES:
        idx = side_indices(obj, s)
        pts = [obj.matrix_world @ obj.data.vertices[i].co for i in idx]
        frame = hand_frame(pts, bone_head(arm, s + "_hand"), bone_head(arm, s + "_finger0"))
        thumb = thumb_sign(pts, frame)
        if thumb is None:
            raise SystemExit("target side %s: no side of the hand leads; the thumb cannot be found" % s)
        frame = palm_frame(frame, thumb, source["sides"][s]["handed"])
        loc = np.array([local(p, frame) for p in pts])
        fits[s] = (frame, extents(loc) / extents(source["sides"][s]["loc"]), idx, thumb)
    return fits


def fitted_source(source, fits):
    """The reference hands, both sides and every channel, mapped through the fit onto the target's hands."""
    verts, faces, keys = [], [], [[] for _ in source["names"]]
    for s in SIDES:
        src = source["sides"][s]
        (o, f, w, n), scale, _, _ = fits[s]

        def to_world(l, o=o, f=f, w=w, n=n, scale=scale):
            return o + f * float(l[0] * scale[0]) + w * float(l[1] * scale[1]) + n * float(l[2] * scale[2])

        base = len(verts)
        verts += [to_world(l) for l in src["loc"]]
        # fan triangles: Surface Deform refuses to bind to a concave polygon
        faces += [[base + face[0], base + face[i], base + face[i + 1]] for face in src["faces"]
                  for i in range(1, len(face) - 1)]
        for c, disp in enumerate(src["disp"]):
            keys[c] += [to_world(l + d) for l, d in zip(src["loc"], disp)]
    me = bpy.data.meshes.new("handmorph_source")
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new("handmorph_source", me)
    bpy.context.scene.collection.objects.link(ob)
    flat = lambda positions: [x for p in positions for x in p]   # noqa: E731 (foreach_set wants a flat list)
    return ob, flat(verts), [flat(positions) for positions in keys]


def transfer(obj, arm, source, falloff):
    """([{vertex index: object-space displacement}] per channel, the fit, the moved vertices), through Surface
    Deform."""
    if obj.data.shape_keys:
        raise SystemExit("%s already has channels; refusing to change it" % obj.name)
    fits = target_fit(obj, arm, source)
    mirror = mirror_dot(fits["l"][0][3], fits["r"][0][3], bone_head(arm, "r_hand"), bone_head(arm, "l_hand"))
    if mirror < MIRROR_MIN:
        raise SystemExit("the target's palm normals are not mirror images (reflected dot %.3f, under %.1f): the "
                         "thumb or palm reading is wrong on one hand" % (mirror, MIRROR_MIN))
    src_ob, rest_co, channel_co = fitted_source(source, fits)
    tmesh = obj.data.copy()
    tob = bpy.data.objects.new("handmorph_target", tmesh)
    tob.matrix_world = obj.matrix_world.copy()
    bpy.context.scene.collection.objects.link(tob)
    mod = tob.modifiers.new("handmorph", "SURFACE_DEFORM")
    mod.target = src_ob
    mod.falloff = falloff
    bpy.context.view_layer.objects.active = tob
    bpy.context.view_layer.update()
    with bpy.context.temp_override(object=tob, active_object=tob):
        bpy.ops.object.surfacedeform_bind(modifier=mod.name)
    if not mod.is_bound:
        raise SystemExit("Surface Deform did not bind the target hands to the fitted reference")
    to_obj = obj.matrix_world.inverted().to_3x3()
    mw = tob.matrix_world.copy()
    rest = [mw @ v.co for v in tmesh.vertices]
    hand = [vi for s in SIDES for vi in fits[s][2]]
    out = []
    for positions in channel_co:
        src_ob.data.vertices.foreach_set("co", positions)
        src_ob.data.update()
        bpy.context.view_layer.update()
        ev = tob.evaluated_get(bpy.context.evaluated_depsgraph_get())
        m = ev.to_mesh()
        out.append({vi: to_obj @ (mw @ m.vertices[vi].co - rest[vi]) for vi in hand})
        ev.to_mesh_clear()
    src_ob.data.vertices.foreach_set("co", rest_co)
    for helper in (tob, src_ob):
        data = helper.data
        bpy.data.objects.remove(helper, do_unlink=True)
        bpy.data.meshes.remove(data)
    fit = {s: {"vertices": len(fits[s][2]), "scale_FWN": [round(float(x), 3) for x in fits[s][1]],
               "thumb": fits[s][3], "palm_normal": [round(x, 3) for x in fits[s][0][3]]} for s in SIDES}
    fit["palm_mirror_dot"] = round(mirror, 3)
    return out, fit, hand


def smooth(obj, displacement, passes):
    """Each channel's displacement field, averaged with its edge neighbours `passes` times (half self, half the
    neighbours' mean) over the vertices it moves; a vertex it does not move stays unmoved."""
    if passes <= 0:
        return displacement
    adj = {}
    for e in obj.data.edges:
        a, c = e.vertices
        adj.setdefault(a, []).append(c)
        adj.setdefault(c, []).append(a)
    out = []
    for disp in displacement:
        cur = dict(disp)
        for _ in range(passes):
            nxt = {}
            for vi, d in cur.items():
                nb = [cur[j] for j in adj.get(vi, ()) if j in cur]
                nxt[vi] = d * 0.5 + (sum(nb, Vector()) / len(nb)) * 0.5 if nb else d
            cur = nxt
        out.append(cur)
    return out


def add_channels(obj, names, displacement):
    """A reference key, then one channel per displacement field at weight 0 (transfer() refused an object with
    keys). Blender 5.2 adds a key at weight 1.0 and the exporter writes the weight as DeformPercent, so unset, all
    26 poses came back applied at once; the artist files carry every channel at 0."""
    obj.shape_key_add(name="Basis", from_mix=False)
    basis = obj.data.shape_keys.key_blocks[0]
    peak = []
    for name, disp in zip(names, displacement):
        k = obj.shape_key_add(name=name, from_mix=False)
        k.value = 0.0
        top = 0.0
        for vi, d in disp.items():
            k.data[vi].co = basis.data[vi].co + d
            top = max(top, d.length)
        peak.append(round(top, 4))
    return [k.name for k in obj.data.shape_keys.key_blocks], peak


def seam_offsets(obj, seam):
    """Per channel, the largest offset of a seam vertex, read back from the written keys."""
    keys = obj.data.shape_keys.key_blocks
    basis = keys[0]
    return [round(max(((k.data[v].co - basis.data[v].co).length for v in seam), default=0.0), 5)
            for k in keys[1:]]


def stitched_meshes(obj):
    """The LOD0 meshes that meet `obj` along its open boundary without covering the rest of it: the body a hand
    mesh is stitched to, never a merged copy of the whole character (the pale uruk's _Full holds its hand too)."""
    from mathutils.kdtree import KDTree
    border = aml.boundary_vertices([tuple(p.vertices) for p in obj.data.polygons])
    pts = aml.world_positions(obj)
    inner = [i for i in range(len(pts)) if i not in border]
    tol = 1e-4 * max(max(obj.dimensions), 1e-6)
    out = []
    for o in bpy.data.objects:
        if o == obj or o.type != "MESH" or LOD.search(o.name) or not o.data.vertices:
            continue
        cos = aml.world_positions(o)
        tree = KDTree(len(cos))
        for i, p in enumerate(cos):
            tree.insert(p, i)
        tree.balance()
        on = sum(1 for i in border if tree.find(pts[i])[2] <= tol)
        if on and sum(1 for i in inner if tree.find(pts[i])[2] <= tol) <= 0.05 * len(inner):
            out.append(o)
    return out


def palm_view(obj, centre, frame, distance):
    """The first camera position, palm side first, from which a ray to the hand's centre meets the hand before
    anything else: the body shown beside it stood between the camera and the troll's right hand."""
    _, _, w, n = frame
    dg = bpy.context.evaluated_depsgraph_get()
    for a, b in ((1, 0.6), (1, -0.6), (1, 0), (0.3, 1), (0.3, -1), (-1, 0.6), (-1, -0.6)):
        eye = centre + (n * a + w * b).normalized() * distance
        hit, _, _, _, what, _ = bpy.context.scene.ray_cast(dg, eye, (centre - eye).normalized())
        if hit and what is not None and what.name == obj.name:
            return eye
    return centre + (n + w * 0.6).normalized() * distance


def render_preview(obj, arm, out_dir, tag, handed):
    """Both hands at rest (`_00`) and at each channel, camera on the palm side, workbench, 360 px, with the meshes
    stitched to `obj` shown in their own colours so a gap at the seam shows. Returns the names shown beside it."""
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "RANDOM"
    scene.render.resolution_x = scene.render.resolution_y = 360
    shown = [obj] + stitched_meshes(obj)
    for o in bpy.data.objects:
        o.hide_render = o not in shown
        if o.type == "MESH":
            o.hide_viewport = o not in shown     # out of the ray casts too (a LOD lies on the hand's surface)
    cam_data = bpy.data.cameras.new("preview")
    cam = bpy.data.objects.new("preview", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.lens = 50
    keys = obj.data.shape_keys.key_blocks
    for side in SIDES:
        idx = side_indices(obj, side)
        pts = [obj.matrix_world @ obj.data.vertices[i].co for i in idx]
        frame = hand_frame(pts, bone_head(arm, side + "_hand"), bone_head(arm, side + "_finger0"))
        frame = palm_frame(frame, thumb_sign(pts, frame) or 1, handed[side])
        centre = sum(pts, Vector()) / len(pts)
        size = max((p - centre).length for p in pts)
        for k in keys[1:]:
            k.value = 0.0
        cam.location = palm_view(obj, centre, frame, size * 4.0)
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        for c in range(len(keys)):
            for k in keys[1:]:
                k.value = 0.0
            if c:
                keys[c].value = 1.0
            scene.render.filepath = os.path.join(out_dir, "%s_%s_%02d.png" % (tag, side, c))
            bpy.ops.render.render(write_still=True)
    return [o.name for o in shown[1:]]


def main():
    fbx = aml.fbx_from_argv(list(sys.argv))
    report_path = fbx + ".handmorphs-report.json"
    report = {"fbx": fbx, "stage": "start", "mode": None, "ok": False, "applied": False, "diffs": []}

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        args = parse_args(list(sys.argv))
        report.update(mode="preview" if args["preview"] else "apply" if args["apply"] else "dry-run",
                      source=args["source"], source_mesh=args["source_mesh"], target_mesh=args["target_mesh"])
        if args["preview"]:
            report["ok"] = None                  # a preview is not a verification
        save("reading source")
        source = read_source(args["source"], args["source_mesh"], args["channels"])
        handed = {s: source["sides"][s]["handed"] for s in SIDES}
        if args["preview"]:
            src_obj = bpy.data.objects[args["source_mesh"]]
            save("rendering source")
            report["source_shown"] = render_preview(src_obj, armature_of(src_obj), args["preview"], "source", handed)
        save("loading target")
        aml.load(fbx)
        obj = bpy.data.objects.get(args["target_mesh"])
        if obj is None or obj.type != "MESH":
            raise SystemExit("no mesh %r in the FBX" % args["target_mesh"])
        before = aml.fingerprint()
        displacement, fit, moved = transfer(obj, armature_of(obj), source, args["falloff"])
        displacement = smooth(obj, displacement, args["smooth"])
        weights, seam = seam_weights([tuple(p.vertices) for p in obj.data.polygons], moved, args["seam_rings"])
        displacement = weigh(displacement, weights)
        names = ["%s%02d" % (args["prefix"], i) for i in range(1, len(source["names"]) + 1)]
        keys, peak = add_channels(obj, names, displacement)
        by_channel = seam_offsets(obj, seam)
        seam_max, allowed, gate = seam_gate(by_channel, peak)
        if gate:
            report["diffs"].append(gate)
        report.update(fit=fit, channels=len(names), source_channels=source["names"], peak_displacement=peak,
                      seam={"vertices": len(seam), "rings": args["seam_rings"], "max": round(seam_max, 5),
                            "limit": round(allowed, 5), "by_channel": by_channel},
                      smooth=args["smooth"], falloff=args["falloff"])
        if args["preview"]:
            save("rendering target")
            report["target_shown"] = render_preview(obj, armature_of(obj), args["preview"], "target", handed)
            save("done")
            return
        staged = os.path.splitext(fbx)[0] + ".handmorphs.fbx"
        report["staged"] = staged
        save("exporting")
        aml.export(staged)
        save("re-importing")
        aml.load(staged)
        report["diffs"] += aml.compare(before, aml.fingerprint(), {}, rekeyed={args["target_mesh"]: keys})
        report["ok"] = not report["diffs"]
        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-handmorphs"
            if not os.path.exists(backup):
                shutil.copy2(fbx, backup)
            os.replace(staged, fbx)
            report.update(applied=True, backup=backup, staged=None)
        elif report["ok"]:
            os.remove(staged)                    # the round trip is proven; the copy has no further use
            report["staged"] = None
    except SystemExit as exc:
        report["error"] = str(exc)
    except Exception as exc:  # noqa: BLE001
        import traceback
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
        report["traceback"] = traceback.format_exc()
    save("done")


if __name__ == "__main__":
    main()
