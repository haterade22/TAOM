#!/usr/bin/env python3
"""
Bind unrigged mesh FBX files onto an EXISTING creature's skeleton, headless, by transferring the
donor's vertex weights. Produces one FBX the Modding Kit can import.

WHEN TO USE THIS
Art arrives as geometry with no rig: 0 LimbNode, 0 Deformer, 0 Cluster, 0 Skin in the FBX. If it was
modelled on an existing creature it already shares that creature's world space, and the donor's own
weights can be transferred onto it by proximity. Proven on the fell warg (2026-08-31), which was
modelled on the warg and bound to skeleton_warg's 49 bones.

If the art was NOT modelled on the donor, proximity transfer will produce nonsense at the joints and
automatic weights (parent_set type='ARMATURE_AUTO') is the better starting point, followed by hand
painting.

HOW TO RUN (Windows, Blender from the Microsoft Store)
    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/bind_to_existing_skeleton.py -- --config <config.json>

The raw blender.exe under WindowsApps is ACL-locked (exit 126); use the launcher. The launcher
DETACHES, so nothing comes back on stdout: this script writes its report to <out>.report.json and
the caller must poll for that file.

CONFIG (JSON)
    {
      "donor":   "<path>/Warg_Rig_V5.fbx",        rigged; supplies the armature and the weights
      "targets": [                                 unrigged meshes to bind
        {"fbx": "<path>/fellwarg_mesh.fbx",    "pairs": {"SK_GD_Fellwarg": "warg_low"}},
        {"fbx": "<path>/fellwargfur_mesh.fbx", "pairs": {"SK_GD_Fellwarg_fur": "warg_low_fur"}}
      ],
      "lod_suffixes": ["", ".lod1", ".lod2", ".lod3", ".lod4"],
      "out": "<path>/FellWarg_Rig.fbx"
    }

Each "pairs" entry maps a destination mesh to the donor mesh whose weights it should take. Every LOD
suffix is applied to both sides, so one pair covers the whole chain. Pair fur to fur and body to
body; pairing everything to the body mesh gives the fur the wrong weights near the silhouette.

GOTCHAS THIS SCRIPT ENCODES
  - The DONOR is imported first. Inherited art is often named after the donor's own geometry
    (warg_low.016), and whichever file loads first claims the name.
  - layers_select_dst='NAME' with use_create=True makes the bone-name match structural: the
    destination group takes the source layer's name, so it can only ever be a donor bone name.
    use_create=False on a target with no groups returns FINISHED and does nothing.
  - vertex_group_limit_total is deliberately NOT used. It breaks normalisation and the shipped warg
    does not obey a 4-influence cap anyway.
  - parent_set(type='ARMATURE'), never ARMATURE_AUTO, which would discard the transferred weights.
  - primary_bone_axis='Y' / secondary_bone_axis='X' on export. At X/Y the bone HEADS and the bounding
    box stay correct to 1e-06 while the TAILS drift half a metre. A gate that checks heads or bboxes
    passes the broken file.
  - add_leaf_bones=False. True turns 49 bones into 60 against Skeleton.MaxBoneCount = 64.
  - Do NOT rename the armature to <skel>_notused. The Kit enforces per-module asset-name uniqueness
    itself, refuses the duplicate Skeleton, and still imports and binds the meshes.

WHAT IT CANNOT DO
Invent geometry. Where the destination has no surface at a donor bone, that bone gets no weight. The
report includes a per-bone mass comparison so you can see which bones lost their binding before you
ship it. Check the bones your action set actually drives.

VALIDATE the output by re-importing it and comparing bone TAILS in world space against the donor.
"""
import bpy
import json
import os
import sys

import mathutils


def _args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    cfg = None
    for i, a in enumerate(argv):
        if a == "--config" and i + 1 < len(argv):
            cfg = argv[i + 1]
    if not cfg:
        raise SystemExit("usage: -P bind_to_existing_skeleton.py -- --config <config.json>")
    return json.load(open(cfg, encoding="utf-8"))


def _bbox(o):
    return [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]


def _select(objs, active):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active


def main():
    cfg = _args()
    report = {"errors": [], "bound": [], "gates": {}}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=cfg["donor"])          # donor FIRST, it owns the names
    for t in cfg["targets"]:
        bpy.ops.import_scene.fbx(filepath=t["fbx"])

    arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
    if arm is None:
        raise SystemExit("no armature found in the donor")
    bones = [b.name for b in arm.data.bones]
    report["armature"] = arm.name
    report["bones"] = len(bones)

    lods = cfg.get("lod_suffixes", [""])
    pairs = []
    for t in cfg["targets"]:
        for dst_base, src_base in t["pairs"].items():
            for sfx in lods:
                pairs.append((dst_base + sfx, src_base))

    pre = {}
    for dst_name, src_name in pairs:
        dst = bpy.data.objects.get(dst_name)
        src = bpy.data.objects.get(src_name)
        if dst is None or src is None:
            report["errors"].append("missing pair %s <- %s" % (dst_name, src_name))
            continue
        pre[dst_name] = _bbox(dst)

        _select([src, dst], src)
        bpy.ops.object.data_transfer(
            use_reverse_transfer=False, data_type='VGROUP_WEIGHTS', use_create=True,
            vert_mapping='POLYINTERP_NEAREST', layers_select_src='ALL',
            layers_select_dst='NAME', mix_mode='REPLACE', use_object_transform=True)

        _select([dst], dst)
        bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.0, keep_single=True)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)

        used = set()
        for v in dst.data.vertices:
            for g in v.groups:
                if g.weight > 0.0:
                    used.add(dst.vertex_groups[g.group].name)
        for g in [g for g in dst.vertex_groups if g.name not in used]:
            dst.vertex_groups.remove(g)

        _select([dst, arm], arm)
        bpy.ops.object.parent_set(type='ARMATURE')
        report["bound"].append(dst_name)

    # ---- gates on the bound scene
    gates = {}
    for dst_name, src_name in pairs:
        dst = bpy.data.objects.get(dst_name)
        if dst is None:
            continue
        zero = 0
        wmin, wmax, maxinf = 9e9, -9e9, 0
        for v in dst.data.vertices:
            gs = [g for g in v.groups if g.weight > 0.0]
            if not gs:
                zero += 1
                continue
            s = sum(g.weight for g in gs)
            wmin, wmax = min(wmin, s), max(wmax, s)
            maxinf = max(maxinf, len(gs))
        drift = max((a - b).length for a, b in zip(pre[dst_name], _bbox(dst)))
        gates[dst_name] = {
            "verts": len(dst.data.vertices), "zero": zero,
            "wsum_min": round(wmin, 6), "wsum_max": round(wmax, 6),
            "max_influences": maxinf,
            "groups_outside_skeleton": sorted({g.name for g in dst.vertex_groups} - set(bones)),
            "bbox_drift_m": drift,
            "armature_modifier": any(m.type == 'ARMATURE' and m.object == arm for m in dst.modifiers),
        }
    report["gates"] = gates
    report["pass"] = bool(gates) and all(
        g["zero"] == 0 and not g["groups_outside_skeleton"]
        and abs(g["wsum_min"] - 1.0) < 1e-4 and abs(g["wsum_max"] - 1.0) < 1e-4
        and g["bbox_drift_m"] < 1e-4 and g["armature_modifier"]
        for g in gates.values())

    # ---- per-bone mass, so a bone that lost its binding is visible before shipping
    def mass(obj_name):
        o = bpy.data.objects.get(obj_name)
        if not o:
            return {}
        idx = {g.index: g.name for g in o.vertex_groups}
        tot = {}
        for v in o.data.vertices:
            for g in v.groups:
                n = idx.get(g.group)
                if n:
                    tot[n] = tot.get(n, 0.0) + g.weight
        n = max(1, len(o.data.vertices))
        return {k: round(val / n, 6) for k, val in tot.items()}

    report["bone_mass"] = {d: mass(d) for d, _s in pairs[:1]}
    report["bone_mass_donor"] = {s: mass(s) for _d, s in pairs[:1]}

    _select([arm] + [bpy.data.objects[d] for d, _s in pairs if d in bpy.data.objects], arm)
    res = bpy.ops.export_scene.fbx(
        filepath=cfg["out"], use_selection=True, object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        axis_forward='-Y', axis_up='Z', bake_anim=False,
        apply_scale_options='FBX_SCALE_NONE', global_scale=1.0,
        use_mesh_modifiers=False, path_mode='AUTO')
    report["export"] = {"result": list(res),
                        "bytes": os.path.getsize(cfg["out"]) if os.path.exists(cfg["out"]) else 0}

    open(cfg["out"] + ".report.json", "w", encoding="utf-8").write(json.dumps(report, indent=1))


if __name__ == "__main__":
    main()
