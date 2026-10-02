#!/usr/bin/env python3
"""
Give an unweighted hair or beard mesh, and its LODs, skin weights copied from reference meshes: its finished sibling
beards, or the head it sits on (2026-10-02, `SK_Dwarf_Beard_A_12` in the live Armory
`AssetSources/Race Test/Beards/SK_Dwarf_Beards.fbx`).

WHY
`SK_Dwarf_Beard_A_12` shipped with no vertex weights at all (15,344 vertices), so in game it does not move with the
male dwarf's head. Beards 01 to 11 in the same FBX are weighted by the artist: `head` at the roots, fading into
`neck`, `spine1` and `spine2` down the long ones. Copying from those siblings keeps the artist's head-to-chest fade
on a hanging beard; copying from the head mesh alone would decide it by distance from the neck skin, since nothing
of the head lies near a braid. Mike chose the siblings (2026-10-02).
docs/reference/race-face-and-hand-morphs.md "Hair, beards and eyebrows".

WHAT IT DOES
For every vertex of each `--mesh` and its `<mesh>.lodN` copies: the nearest point on the pooled `--ref` meshes (rest
shape, world space; from `--ref-fbx` when given, else the same FBX), the weights of that triangle's three corners
blended by barycentric share, the strongest `--max-influences` (default 4) kept and normalised. Shape keys, materials
and geometry are left alone. The report carries a weight share per height band for each target and reference, so a
result can be compared with the siblings before it is applied.
Refuses a face part as a target, a name not in the FBX, a target used as its own reference, a target that already
carries any weight (a rerun is a refusal, never a second blend), and a bone the target's armature does not have.

SAFETY (strip_upper_mesh_channels.py's shape): a staged `<stem>.upperweights.fbx` is written and re-imported;
`add_mesh_lods.compare` checks every object came back as it was, every other mesh's weights came back unchanged, and
every target vertex came back weighted. `--apply` writes a write-once `<fbx>.bak-upperweights` first, then replaces
the FBX. Then a Kit re-import and Save, a material check, and a look in game.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/transfer_upper_mesh_weights.py -- --fbx <SK_Dwarf_Beards.fbx> ^
        --mesh SK_Dwarf_Beard_A_12 --ref SK_Dwarf_Beard_A_01 ... --ref SK_Dwarf_Beard_A_11 [--apply]
The launcher detaches: poll `<fbx>.upperweights-report.json` until "stage" is "done".
"""
import json
import os
import re
import shutil
import sys

FACE_PART = re.compile(r"(^|[._])(head|eye|mouth)($|[._])", re.I)
PROFILE_BANDS = [1.35, 1.25, 1.15, 1.05]


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "fbx": None, "ref_fbx": None, "mesh": [], "ref": [], "max_influences": 4}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--apply":
            out["apply"] = True
            i += 1
        elif a in ("--fbx", "--ref-fbx") and i + 1 < len(argv):
            out[a[2:].replace("-", "_")] = argv[i + 1]
            i += 2
        elif a in ("--mesh", "--ref") and i + 1 < len(argv):
            out[a[2:]].append(argv[i + 1])
            i += 2
        elif a == "--max-influences" and i + 1 < len(argv):
            out["max_influences"] = int(argv[i + 1])
            i += 2
        else:
            raise SystemExit("bad argument %r" % a)
    if not (out["fbx"] and out["mesh"] and out["ref"]):
        raise SystemExit("--fbx, at least one --mesh and at least one --ref are required")
    if not 1 <= out["max_influences"] <= 8:
        raise SystemExit("--max-influences must be 1 to 8")
    return out


def targets(names, meshes):
    """The objects to weight: each named mesh and its `.lodN` copies. Refuses face parts and unknown names."""
    out = []
    for m in meshes:
        if FACE_PART.search(m):
            raise SystemExit("%r looks like a face part; this tool weights hair and beards only" % m)
        found = [n for n in names if n == m or re.fullmatch(re.escape(m) + r"\.lod\d+", n)]
        if m not in found:
            raise SystemExit("no mesh %r in the FBX" % m)
        out.extend(found)
    return sorted(set(out))


def references(names, refs, work):
    """The reference meshes, as named. Refuses an unknown name and a target used as its own reference."""
    for r in refs:
        if r not in names:
            raise SystemExit("no reference mesh %r" % r)
        if r in work:
            raise SystemExit("%r is a target; it cannot be its own reference" % r)
    return list(refs)


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def barycentric(p, a, b, c):
    """Shares of corners a, b, c for point p projected onto their triangle, clamped to >= 0 and summing to 1.
    A degenerate triangle gives the nearest corner all of it."""
    v0, v1, v2 = _sub(b, a), _sub(c, a), _sub(p, a)
    d00, d01, d11 = _dot(v0, v0), _dot(v0, v1), _dot(v1, v1)
    d20, d21 = _dot(v2, v0), _dot(v2, v1)
    den = d00 * d11 - d01 * d01
    if abs(den) < 1e-18:
        dists = [_dot(_sub(p, q), _sub(p, q)) for q in (a, b, c)]
        k = dists.index(min(dists))
        return tuple(1.0 if i == k else 0.0 for i in range(3))
    v = (d11 * d20 - d01 * d21) / den
    w = (d00 * d21 - d01 * d20) / den
    u = [max(0.0, 1.0 - v - w), max(0.0, v), max(0.0, w)]
    s = sum(u)
    return tuple(x / s for x in u)


def mix_weights(corners, shares, max_influences):
    """Blend three corners' {group: weight} by `shares`, keep the strongest `max_influences`, normalise."""
    total = {}
    for weights, share in zip(corners, shares):
        for g, w in weights.items():
            total[g] = total.get(g, 0.0) + w * share
    kept = sorted(((w, g) for g, w in total.items() if w > 1e-6), reverse=True)[:max_influences]
    s = sum(w for w, _ in kept)
    if s <= 0:
        raise ValueError("the nearest reference triangle carries no weight")
    return {g: w / s for w, g in kept}


def profile(samples, bands):
    """Weight share per group in height bands. `samples`: (z, {group: weight}); `bands`: descending z cuts."""
    labels = ["z>=%.2f" % bands[0]] + ["%.2f<=z<%.2f" % (lo, hi) for hi, lo in zip(bands, bands[1:])] + \
        ["z<%.2f" % bands[-1]]
    sums = [dict() for _ in labels]
    counts = [0] * len(labels)
    for z, weights in samples:
        k = next((i for i, cut in enumerate(bands) if z >= cut), len(bands))
        counts[k] += 1
        for g, w in weights.items():
            sums[k][g] = sums[k].get(g, 0.0) + w
    out = []
    for label, n, s in zip(labels, counts, sums):
        tot = sum(s.values())
        shares = {g: round(w / tot, 2) for g, w in sorted(s.items(), key=lambda kv: -kv[1]) if tot and round(w / tot, 2)}
        out.append({"band": label, "verts": n, "shares": shares})
    return out


def main():
    import bpy
    from mathutils.bvhtree import BVHTree
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import add_mesh_lods as aml

    fbx = aml.fbx_from_argv(list(sys.argv))
    report_path = fbx + ".upperweights-report.json"
    report = {"fbx": fbx, "stage": "start", "ok": False, "applied": False, "diffs": []}

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    def vertex_weights(o):
        names = {g.index: g.name for g in o.vertex_groups}
        return [{names[g.group]: g.weight for g in v.groups if g.weight > 1e-6} for v in o.data.vertices]

    def samples_of(o):
        return [((o.matrix_world @ v.co).z, w) for v, w in zip(o.data.vertices, vertex_weights(o))]

    def weight_totals():
        out = {}
        for o in bpy.data.objects:
            if o.type == "MESH":
                t = {}
                for w in vertex_weights(o):
                    for g, x in w.items():
                        t[g] = t.get(g, 0.0) + x
                out[o.name] = {g: round(x, 2) for g, x in t.items() if round(x, 2)}
        return out

    def sample_references(names, work):
        verts, weights, tris, profiles = [], [], [], {}
        mesh_names = [o.name for o in bpy.data.objects if o.type == "MESH"]
        for r in references(mesh_names, names, work):
            o = bpy.data.objects[r]
            base = len(verts)
            verts.extend(tuple(o.matrix_world @ v.co) for v in o.data.vertices)
            weights.extend(vertex_weights(o))
            o.data.calc_loop_triangles()
            tris.extend(tuple(base + i for i in t.vertices) for t in o.data.loop_triangles)
            profiles[r] = profile(samples_of(o), PROFILE_BANDS)
        return verts, weights, tris, profiles

    try:
        args = parse_args(list(sys.argv))
        staged = os.path.splitext(fbx)[0] + ".upperweights.fbx"
        report["staged"] = staged
        if args["ref_fbx"]:
            save("loading references")
            aml.load(args["ref_fbx"])
            rverts, rweights, rtris, report["reference_profiles"] = sample_references(args["ref"], [])
        save("loading")
        aml.load(fbx)
        before = aml.fingerprint()
        weights_before = weight_totals()
        work = targets([o.name for o in bpy.data.objects if o.type == "MESH"], args["mesh"])
        if not args["ref_fbx"]:
            rverts, rweights, rtris, report["reference_profiles"] = sample_references(args["ref"], work)
        bvh = BVHTree.FromPolygons(rverts, rtris)
        stats = {}
        for n in work:
            ob = bpy.data.objects[n]
            if any(g.weight > 0 for v in ob.data.vertices for g in v.groups):
                raise SystemExit("%s already carries weights; refusing to blend over them" % n)
            arm = next((m.object for m in ob.modifiers if m.type == "ARMATURE" and m.object), None)
            if arm is None:
                raise SystemExit("%s has no armature modifier" % n)
            bones = {b.name for b in arm.data.bones}
            groups, dists, samples = {}, [], []
            for v in ob.data.vertices:
                p = ob.matrix_world @ v.co
                co, _, ti, dist = bvh.find_nearest(p)
                a, b, c = rtris[ti]
                shares = barycentric(tuple(co), rverts[a], rverts[b], rverts[c])
                mixed = mix_weights([rweights[a], rweights[b], rweights[c]], shares, args["max_influences"])
                for g, w in mixed.items():
                    if g not in bones:
                        raise SystemExit("reference bone %r is not in %s's armature %s" % (g, n, arm.name))
                    if g not in groups:
                        groups[g] = ob.vertex_groups.get(g) or ob.vertex_groups.new(name=g)
                    groups[g].add([v.index], w, "REPLACE")
                dists.append(dist)
                samples.append((p.z, mixed))
            dists.sort()
            stats[n] = {"verts": len(dists), "median_mm": round(dists[len(dists) // 2] * 1000, 2),
                        "max_mm": round(dists[-1] * 1000, 2), "profile": profile(samples, PROFILE_BANDS)}
        report["weighted"] = stats
        save("exporting")
        aml.export(staged)
        save("re-importing")
        aml.load(staged)
        after = aml.fingerprint()
        weights_after = weight_totals()
        diffs = aml.compare(before, after, {})
        for n, t in weights_before.items():
            if n not in work and weights_after.get(n) != t:
                diffs.append("%s: weights changed in the round trip" % n)
        for n in work:
            ob = bpy.data.objects.get(n)
            bare = sum(1 for w in vertex_weights(ob) if not w) if ob else -1
            if bare:
                diffs.append("%s: %d vertices came back unweighted" % (n, bare))
        report["diffs"] = diffs
        report["ok"] = not diffs
        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-upperweights"
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
