#!/usr/bin/env python3
"""
One-off (2026-09-25): bring the goblin head in line with the orc's, in SK_Goblin_Basemesh_A.fbx.

WHAT WAS WRONG (read from the FBX with tools/audit_fbx_lods.py's reader, not from the game)
1. The skull is too big. The goblin body, hands and feet are the orc's own geometry and the neck
   and jaw band (the lowest 0.15 m of the head) matches the orc head exactly, but from the
   cheekbones up the goblin skull is 6 to 12% wider and 10 to 20% deeper than `orc_a_head`, before
   counting the ears. Mike sees every goblin head too big in game.
2. The face-morph list is one short. The goblin head, mouth and eyes carry 100 blend-shape
   channels starting at `FaceWidth_1`; the orc's carry 101 starting at an empty `Basis_0`, and
   every other race head (dwarf, elf, pale uruk, Dol Guldur uruk, Isengard uruk) carries 101. If
   the Kit maps facegen morphs by channel order, every goblin slider drives the next morph down:
   `nose_asymetry` (key_time_point 45, range +-0.9 in skins.xml) lands on `HeadScaling_46`.
   Adding an empty `Basis_0` first is right whether the Kit maps by order or by the number in the
   name.

WHAT IT DOES
- Inserts a zero-delta `Basis_0` shape key directly after Blender's reference key on the head,
  `.mouth` and `.eyes` LOD0 objects (their LODs carry no morphs).
- Shrinks the skull horizontally: X by `--sx`, Y (depth) by `--sy`, about the vertical axis
  through the head's neck-ring centroid. The factor is 1 below 37% of the head's height (the neck
  and jaw, which already match the orc and meet the body seam) and blends by smoothstep to the
  full factor at 52% (1.56 m and 1.62 m on this head). Height is untouched; it already matches.
  One position field, weighted by each vertex's reference-key position, is applied to every shape
  key of every head object (head, mouth, eyes, and all their LODs), so morph deltas, eyes and
  teeth stay seated.
- The factors used on 2026-09-25 were fitted by least squares on 2 cm slices of the raw FBX
  vertices, 1.58 to 1.80 m, skipping the ear slices (goblin/orc width ratio above 1.15):
  sx 0.913, sy 0.852. The depth pivot is fitted the same way, as the point about which each
  scaled slice's centre lands on the orc's: median y -0.078 (the head spans -0.17 to 0.08). The
  default, the neck ring's centroid, is poor on this head: its neck opening slants, only 3
  boundary vertices sit in the lowest 15%, and their centroid (-0.11) is near the face.

HOW TO RUN (Windows, Blender from the Microsoft Store; the launcher detaches, poll the report
until "stage": "done")
    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/oneoff/blender_goblin_head_fix.py -- --fbx <SK_Goblin_Basemesh_A.fbx> ^
        --sx 0.913 --sy 0.852 --pivot-y -0.078 [--apply]

SAFETY: the tools/blender/add_mesh_lods.py shape. A staged `<stem>.goblinhead.fbx` is written and
re-imported; every non-head object must come back unchanged, every head object must keep its
vertex and polygon counts, materials and parent, and gain only `Basis_0` in its key list; no
vertex below the blend zone may move (the neck seam). `--apply` writes a write-once
`<fbx>.bak-goblinhead` first. Check the result without Blender with
`python tools/audit_fbx_lods.py --diff <old> <new>`.
"""
import json
import os
import shutil
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import add_mesh_lods as aml  # noqa: E402

HEAD = "SK_Goblin_Basemesh_A_Head"
MORPHED = (HEAD, HEAD + ".mouth", HEAD + ".eyes")
BLEND_LO, BLEND_HI = 0.37, 0.52     # fractions of the head's height
NECK_BAND = 0.15                    # boundary vertices below this fraction form the neck ring


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "fbx": None, "sx": None, "sy": None, "pivot_y": None}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--apply":
            out["apply"] = True
            i += 1
        elif a in ("--fbx", "--sx", "--sy", "--pivot-y") and i + 1 < len(argv):
            out[a[2:].replace("-", "_")] = argv[i + 1] if a == "--fbx" else float(argv[i + 1])
            i += 2
        else:
            raise SystemExit("bad argument %r" % a)
    for k in ("fbx", "sx", "sy"):
        if out[k] is None:
            raise SystemExit("--%s is required" % k)
    if not (0.8 <= out["sx"] <= 1.0 and 0.8 <= out["sy"] <= 1.0):
        raise SystemExit("--sx/--sy must shrink, and by no more than 20%")
    return out


def world(obj, local):
    m = np.array(obj.matrix_world)
    return local @ m[:3, :3].T + m[:3, 3]


def local(obj, pts):
    m = np.array(obj.matrix_world.inverted())
    return pts @ m[:3, :3].T + m[:3, 3]


def coords(seq):
    a = np.empty(len(seq) * 3)
    seq.foreach_get("co", a)
    return a.reshape(-1, 3)


def set_coords(seq, pts):
    seq.foreach_set("co", pts.reshape(-1))


def insert_basis_0(obj):
    keys = obj.data.shape_keys.key_blocks
    if "Basis_0" in keys:
        raise SystemExit("%s already has Basis_0; nothing to insert" % obj.name)
    obj.shape_key_add(name="Basis_0", from_mix=False)
    bpy.context.view_layer.objects.active = obj
    obj.active_shape_key_index = len(keys) - 1
    while obj.active_shape_key_index > 1:
        bpy.ops.object.shape_key_move(type="UP")
    names = [k.name for k in keys]
    if names[1] != "Basis_0" or names[0] == "Basis_0":
        raise SystemExit("%s: Basis_0 landed at %d" % (obj.name, names.index("Basis_0")))
    return names


def main():
    fbx = aml.fbx_from_argv(list(sys.argv))
    report_path = fbx + ".goblinhead-report.json"
    report = {"fbx": fbx, "stage": "start", "ok": False, "applied": False, "diffs": [],
              "gates": [], "objects": {}}

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        args = parse_args(list(sys.argv))
        staged = os.path.splitext(fbx)[0] + ".goblinhead.fbx"
        report.update(staged=staged, sx=args["sx"], sy=args["sy"])
        save("loading")
        aml.load(fbx)
        before = aml.fingerprint()
        save("loaded")

        head = bpy.data.objects[HEAD]
        ref = world(head, coords(head.data.shape_keys.key_blocks[0].data))
        zmin, zmax = float(ref[:, 2].min()), float(ref[:, 2].max())
        height = zmax - zmin
        z_lo, z_hi = zmin + BLEND_LO * height, zmin + BLEND_HI * height
        border = sorted(aml.boundary_vertices([tuple(p.vertices) for p in head.data.polygons]))
        neck = [i for i in border if ref[i, 2] < zmin + NECK_BAND * height]
        cx, cy = float(ref[neck, 0].mean()), float(ref[neck, 1].mean())
        if args["pivot_y"] is not None:
            cy = args["pivot_y"]
        # the seam the body stitches to: every open-edge vertex under the blend zone
        seam = [i for i in border if ref[i, 2] < z_lo]
        report.update(head_z=[zmin, zmax], blend_z=[z_lo, z_hi], pivot_xy=[cx, cy],
                      neck_ring_vertices=len(neck), seam_vertices=len(seam))

        targets = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith(HEAD)]
        expected_keys = {}
        for obj in targets:
            if obj.name in MORPHED:
                expected_keys[obj.name] = insert_basis_0(obj)
            blocks = obj.data.shape_keys.key_blocks if obj.data.shape_keys else None
            base = world(obj, coords(blocks[0].data if blocks else obj.data.vertices))
            t = np.clip((base[:, 2] - z_lo) / (z_hi - z_lo), 0.0, 1.0)
            w = t * t * (3 - 2 * t)
            fx = 1 + (args["sx"] - 1) * w
            fy = 1 + (args["sy"] - 1) * w
            layers = list(blocks) if blocks else []
            moved = 0.0
            for layer in layers + [None]:
                seq = layer.data if layer is not None else obj.data.vertices
                p = world(obj, coords(seq))
                q = p.copy()
                q[:, 0] = cx + (p[:, 0] - cx) * fx
                q[:, 1] = cy + (p[:, 1] - cy) * fy
                moved = max(moved, float(np.abs(q - p).max()))
                set_coords(seq, local(obj, q))
            below = base[:, 2] < z_lo
            report["objects"][obj.name] = {
                "vertices": len(obj.data.vertices), "keys": len(layers),
                "max_move": moved, "vertices_below_blend": int(below.sum())}
        obj_neck = world(head, coords(head.data.shape_keys.key_blocks[0].data))
        drift = float(np.abs(obj_neck[seam] - ref[seam]).max()) if seam else 0.0
        report["seam_drift"] = drift
        if drift > 1e-6:    # float32 coordinates through two transforms: 1.2e-7 is round-off
            report["gates"].append("neck seam moved by %g" % drift)

        # the post-fix profile, same slices as the fit, for the report
        final = world(head, coords(head.data.shape_keys.key_blocks[0].data))
        prof = []
        z = 1.58
        while z < 1.80 - 1e-9:
            s = final[(final[:, 2] >= z) & (final[:, 2] < z + 0.02)]
            if len(s):
                prof.append([round(z, 2), round(float(np.ptp(s[:, 0])), 4), round(float(np.ptp(s[:, 1])), 4)])
            z += 0.02
        report["profile_width_depth"] = prof
        save("exporting")
        aml.export(staged)
        save("re-importing")
        aml.load(staged)
        after = aml.fingerprint()

        for name, b in before.items():
            a = after.get(name)
            if a is None:
                report["diffs"].append("lost %s" % name)
                continue
            if b["type"] != "MESH":
                continue
            keys = ["verts", "polys", "uv_layers", "materials", "parent"]
            if not name.startswith(HEAD):
                keys += ["dims", "loc", "shape_keys"]
            for k in keys:
                if b[k] != a[k]:
                    report["diffs"].append("%s: %s %r -> %r" % (name, k, b[k], a[k]))
            if name in expected_keys and a["shape_keys"] != expected_keys[name]:
                report["diffs"].append("%s: keys %d -> %d, first %s" % (
                    name, len(b["shape_keys"]), len(a["shape_keys"]), a["shape_keys"][:3]))
        report["ok"] = not report["diffs"] and not report["gates"]
        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-goblinhead"
            if not os.path.exists(backup):
                shutil.copy2(fbx, backup)
            os.replace(staged, fbx)
            report.update(applied=True, backup=backup, staged=None)
    except SystemExit as exc:
        report["error"] = str(exc)
    except Exception as exc:  # noqa: BLE001
        import traceback
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
        report["traceback"] = traceback.format_exc()
    save("done")


if __name__ == "__main__":
    main()
