#!/usr/bin/env python3
"""
Kitbash the dwarf grooms: inventory, build and preview (2026-10-08).

WHY THIS EXISTS
New dwarf beards (SK_Dwarf_Beard_A_13..) and hairs (Dwarf_Hair_K..) are built by combining loose hair cards from
the existing grooms. To pick cards by what they are (a moustache card, a long back card, a braid) every part of
every groom is first classified by where it is rooted on the head. The rules and thresholds live in
groom_kitbash.py (pure, tested in tools/tests/test_groom_kitbash.py); this script feeds them from Blender.

WHAT `--mode inventory` DOES
For SK_Dwarf_Beards.fbx, Dwarf_Hairs.fbx and Dwarf_Hairs_SecondBatch.fbx (one at a time, with the head
keyforce_dwarf.fbx imported beside it): the sha1 of each FBX; per LOD0 groom object the loose parts (islands), and
per part its vertex and triangle counts, material kind (card or ring), valid UV layer, root, root distance to the
head, lowest z, tip, penetration depth, region, length, side, hang and braid; then `inventory.json`,
`inventory.md`, and region-coloured Workbench views per groom in `regions/` (front, side, back, three-quarter;
orthographic, fixed scale; head grey). `tools/groom_contact_sheet.py` tiles the PNGs afterwards (Pillow is not in
Blender's Python). The sources are only read: nothing is saved over them and no .blend is written.

`--mode build` and `--mode preview` (step 2) read the inventory and a recipe (tools/blender/groom_kitbash.py
documents the shape) and build every candidate through ONE code path: the selected parts of each source groom
are read as arrays in world space, transformed (`mirror_copy`, `translate`), and written as a new mesh on a copy of
the family's sibling LOD0, so the parent and Armature modifier match. Per part: one UV layer `UVMap` (the part's
valid layer), no colour attributes, the family's two material names (cards, ring), vertex groups by name, and
with channels `keep` exactly shape_01..shape_101 (zero offsets from a source with none). Both import with the
DEFAULT orientation, like add_mesh_lods.load, so the exported bind pose and axes match the family source.
  build:   --mode build --inventory inventory.json --recipe recipe.json --family beard|hair --target <fbx>
           --out <dir> [--apply]
           One build writes the WHOLE target from every `final` entry of the family: only the family armature and
           the new grooms (LOD0), staged in --out as <target stem>.kitbash-staged.fbx, then re-imported and
           checked (report["roundtrip"]). `--apply` copies the staged file to --target (a write-once .bak-kitbash
           first) only when every gated check passed; it refuses a source FBX or the head as --target.
  preview: --mode preview --inventory ... --recipe ... --family ... --out <dir> [--renderer auto|eevee|workbench]
           Each candidate of the family on the head, textured (card atlas times a mid-brown with alpha clip, ring
           atlas on rings), four views to <out>/preview/<id>__<view>.png and <out>/previews.json. Eevee, with
           Workbench texture colour as the fallback; report["renderer"] says which ran.
The recipe's families.<family>.materials names the two material datablocks; --race-test defaults to the live
Armory for these two modes (read only).

HOW TO RUN (Windows, Blender from the Microsoft Store; one Blender at a time)
    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/kitbash_grooms.py -- --mode inventory --race-test "<...>/AssetSources/Race Test" ^
        [--out E:/LOTRAOMAssets/Dwarf/_groom_kitbash/inventory]

The launcher DETACHES: nothing reaches stdout. The script writes <out>/kitbash-report.json at every stage
({"stage", "ok", "error", ...}), a bad argument and any exception (with the traceback) included, so the caller polls
until "stage" is "done". A report stuck on another stage means Blender died natively there. `--mode` other than
`inventory` is refused in the report; the preview and build modes come in a later step.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import time

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

try:
    _HERE = os.path.dirname(os.path.abspath(__file__))
except NameError:  # Blender can exec a -P script without __file__
    _HERE = os.path.join(os.getcwd(), "tools", "blender")
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)
import add_mesh_lods as lods  # noqa: E402
import groom_kitbash as gk  # noqa: E402

DEFAULT_OUT = "E:/LOTRAOMAssets/Dwarf/_groom_kitbash/inventory"
HEAD_FBX = "keyforce_dwarf.fbx"
HEAD_OBJECT = "SM_Dwarf_Basemesh_A1_head"
# (path under the Race Test dir, family, regex of the LOD0 groom objects)
SOURCES = [
    ("Hair/Dwarf_Hairs.fbx", "hair", re.compile(r"^Dwarf_Hair_[A-E]_lod0$")),
    ("Hair/Dwarf_Hairs_SecondBatch.fbx", "hair", re.compile(r"^Dwarf_Hair_[F-J]_lod0$")),
    ("Beards/SK_Dwarf_Beards.fbx", "beard", re.compile(r"^SK_Dwarf_Beard_A_\d\d$")),
]
RING_MARK = "ring"                      # a slot material whose name contains this is a ring
ROUND = 5                               # decimals kept in the JSON (10 micrometres)

# --- rendering -------------------------------------------------------------------------------------------
RENDER_SIZE = 720
SCALE = {"beard": 0.45, "hair": 0.6}                       # orthographic frame, metres
CENTRE = {"beard": (0.0, 0.07, 1.22), "hair": (0.0, 0.05, 1.25)}
VIEWS = {"front": (0.0, 1.0, 0.0), "side": (1.0, 0.0, 0.0), "back": (0.0, -1.0, 0.0),
         "threeq": (0.62, 0.78, 0.15)}
HEAD_GREY = (0.55, 0.55, 0.55, 1.0)
BACKGROUND = (0.08, 0.08, 0.09)
COLOURS = {
    "moustache": (1.0, 0.55, 0.0), "chin": (0.9, 0.1, 0.1), "cheek": (0.2, 0.8, 0.2),
    "sideburn": (0.2, 0.4, 1.0), "crown": (1.0, 0.55, 0.0), "side": (0.2, 0.8, 0.2),
    "back": (0.2, 0.4, 1.0), "fill": (0.7, 0.2, 0.9), "braid": (0.0, 0.9, 0.9), "ring": (1.0, 0.9, 0.1),
}
CLASSES = {f: list(gk.REGIONS[f]) + ["braid", "ring"] for f in gk.FAMILIES}


class _Parser(argparse.ArgumentParser):
    def error(self, message):
        raise SystemExit("bad arguments: " + message)


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    p = _Parser(prog="kitbash_grooms.py")
    p.add_argument("--mode", required=True)
    p.add_argument("--race-test", help="the AssetSources/Race Test folder (build and preview default to the live one)")
    p.add_argument("--out")
    p.add_argument("--inventory", help="inventory.json from --mode inventory")
    p.add_argument("--recipe", help="the recipe JSON")
    p.add_argument("--family", choices=("beard", "hair"))
    p.add_argument("--target", help="build: the FBX to write on --apply (the staged file lands in --out)")
    p.add_argument("--apply", action="store_true", help="build: copy the staged FBX to --target after the checks")
    p.add_argument("--renderer", choices=("auto", "eevee", "workbench"), default="auto", help="preview")
    return p.parse_args(argv)


def out_from_argv(argv):
    """Best-effort `--out` so a report lands in the right folder even when the arguments are bad."""
    if "--out" in argv and argv.index("--out") + 1 < len(argv):
        return argv[argv.index("--out") + 1]
    return DEFAULT_OUT


def sha1_of(path):
    h = hashlib.sha1()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# --------------------------------------------------------------------------- #
# mesh reading
# --------------------------------------------------------------------------- #
def world_coords(obj):
    me = obj.data
    co = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    m = np.array(obj.matrix_world, dtype=np.float64)
    return co.reshape(-1, 3).astype(np.float64) @ m[:3, :3].T + m[:3, 3]


def polygon_arrays(me):
    """(loop_start, loop_total, loop_vertex) as int arrays."""
    nloops = len(me.loops)
    start = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get("loop_start", start)
    total = np.diff(np.append(start, nloops)).astype(np.int32)
    lv = np.empty(nloops, dtype=np.int32)
    me.loops.foreach_get("vertex_index", lv)
    return start, total, lv


def polys_of(start, total, lv):
    return [lv[s:s + t].tolist() for s, t in zip(start.tolist(), total.tolist())]


def span_per_part(values, loop_label, nparts):
    """Per part (umin, umax, vmin, vmax) from an (nloops, 2) array."""
    order = np.argsort(loop_label, kind="stable")
    lab = loop_label[order]
    starts = np.searchsorted(lab, np.arange(nparts))
    v = values[order]
    umin = np.minimum.reduceat(v[:, 0], starts)
    umax = np.maximum.reduceat(v[:, 0], starts)
    vmin = np.minimum.reduceat(v[:, 1], starts)
    vmax = np.maximum.reduceat(v[:, 1], starts)
    return list(zip(umin.tolist(), umax.tolist(), vmin.tolist(), vmax.tolist()))


class Head:
    """The head base mesh in world space, and signed distance to its surface (positive outside)."""

    def __init__(self, obj):
        co = world_coords(obj)
        start, total, lv = polygon_arrays(obj.data)
        self.bbox = [np.min(co, axis=0).round(4).tolist(), np.max(co, axis=0).round(4).tolist()]
        self.bvh = BVHTree.FromPolygons([Vector(c) for c in co.tolist()], polys_of(start, total, lv))
        # The normals may point either way depending on the import: probe a point well outside and one inside.
        outside = self._raw(Vector((0.0, 0.4, 1.30)))
        inside = self._raw(Vector((0.0, gk.AXIS_Y, 1.35)))
        self.probe = {"outside": round(outside, 4), "inside": round(inside, 4)}
        self.flip = -1.0 if (outside < 0 < inside) else 1.0
        self.sign_ok = (outside > 0 > inside) or self.flip < 0

    def _raw(self, v):
        loc, nrm, _, dist = self.bvh.find_nearest(v)
        return dist if (v - loc).dot(nrm) >= 0 else -dist

    def distances(self, coords):
        f = self.flip
        out = np.empty(len(coords), dtype=np.float64)
        for i, c in enumerate(coords.tolist()):
            out[i] = f * self._raw(Vector(c))
        return out


def rnd(seq):
    return [round(float(x), ROUND) for x in seq]


# --------------------------------------------------------------------------- #
# analysis
# --------------------------------------------------------------------------- #
def analyse_groom(obj, family, head, sha1, rel, mesh_names):
    me = obj.data
    coords = world_coords(obj)
    n = len(coords)
    start, total, lv = polygon_arrays(me)
    parts_v = gk.islands(n, polys_of(start, total, lv))
    nparts = len(parts_v)
    labels = np.array(gk.vertex_labels(parts_v, n), dtype=np.int64)
    poly_label = labels[lv[start]]
    loop_label = labels[lv]

    tris = np.bincount(poly_label, weights=(total - 2), minlength=nparts).astype(int)
    slot_ring = [bool(s.material and RING_MARK in s.material.name.lower()) for s in obj.material_slots]
    mi = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get("material_index", mi)
    ring_poly = np.array([slot_ring[i] if i < len(slot_ring) else False for i in mi.tolist()], dtype=float)
    ring_share = np.bincount(poly_label, weights=ring_poly, minlength=nparts) / np.maximum(
        np.bincount(poly_label, minlength=nparts), 1)

    layer_names = [l.name for l in me.uv_layers]
    spans = {}
    for layer in me.uv_layers:
        uv = np.empty(len(me.loops) * 2, dtype=np.float32)
        layer.data.foreach_get("uv", uv)
        spans[layer.name] = span_per_part(uv.reshape(-1, 2).astype(np.float64), loop_label, nparts)

    dists = head.distances(coords)
    parts, cards, rings = [], {}, []
    for pid, verts in enumerate(parts_v):
        pts = [tuple(r) for r in coords[verts].tolist()]
        a = gk.analyze_part(pts, dists[verts].tolist(), family)
        layer = gk.valid_uv_layer({name: spans[name][pid] for name in layer_names})
        rec = {"id": pid, "kind": "ring" if ring_share[pid] > 0.5 else "card", "verts": len(verts),
               "tris": int(tris[pid]), "uv_layer": layer,
               "uv_span": rnd(spans[layer][pid]) if layer else None,
               "region": a["region"], "length": a["length"], "side": a["side"], "rooted": a["rooted"],
               "root": rnd(a["root"]), "root_dist": round(a["root_dist"], ROUND),
               "lowest_z": round(a["lowest_z"], ROUND), "depth": round(a["depth"], ROUND), "braid": None}
        if family == "hair":
            rec["hang"] = a["hang"]
            rec["tip"] = rnd(a["tip"])
        if rec["kind"] == "ring":
            centre, half = gk.ring_geometry(pts)
            rec["ring_index"] = len(rings)
            rec["ring_centre"] = rnd(centre)
            rec["ring_half_extent"] = round(half, ROUND)
            rings.append((centre, half))
        else:
            cards[pid] = pts
        parts.append(rec)
    membership, conflicts = gk.ring_membership(cards, rings)
    for pid, ring_index in membership.items():
        parts[pid]["braid"] = ring_index
    return {"object": obj.name, "sha1": sha1, "file": rel, "family": family, "mesh_names": mesh_names,
            "verts": n, "materials": [s.material.name if s.material else None for s in obj.material_slots],
            "uv_layers": layer_names, "parts": parts,
            "conflicts": [{"part": pid, "rings": r} for pid, r in conflicts]}


def part_class(p):
    if p["kind"] == "ring":
        return "ring"
    return "braid" if p["braid"] is not None else p["region"]


# --------------------------------------------------------------------------- #
# rendering
# --------------------------------------------------------------------------- #
def colour_material(name, rgb):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (rgb[0], rgb[1], rgb[2], 1.0)
    return m


def setup_render():
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = sc.render.resolution_y = RENDER_SIZE
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.display.shading.light = "FLAT"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.show_object_outline = False
    sc.display.render_aa = "8"
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("kitbash_world")
    world.color = BACKGROUND
    sc.world = world
    cam_data = bpy.data.cameras.new("kitbash_cam")
    cam_data.type = "ORTHO"
    cam_data.clip_end = 10.0
    cam = bpy.data.objects.new("kitbash_cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam


def render_groom(obj, rec, head_obj, regions_dir, sc, cam, mats, head_mat):
    """Colour `obj` by part class (a material per class) and render every view next to the grey head."""
    family = rec["family"]
    me = obj.data
    start, total, lv = polygon_arrays(me)
    parts_v = gk.islands(len(me.vertices), polys_of(start, total, lv))
    labels = np.array(gk.vertex_labels(parts_v, len(me.vertices)), dtype=np.int64)
    index = {c: i for i, c in enumerate(CLASSES[family])}
    per_part = np.array([index[part_class(p)] for p in rec["parts"]], dtype=np.int32)
    me.materials.clear()
    for c in CLASSES[family]:
        me.materials.append(mats[c])
    me.polygons.foreach_set("material_index", per_part[labels[lv[start]]])
    me.update()
    hm = head_obj.data
    hm.materials.clear()
    hm.materials.append(head_mat)
    hm.polygons.foreach_set("material_index", np.zeros(len(hm.polygons), dtype=np.int32))
    hm.update()
    for o in bpy.data.objects:
        o.hide_render = o not in (obj, head_obj)
    cam.data.ortho_scale = SCALE[family]
    target = Vector(CENTRE[family])
    written = []
    for view, d in VIEWS.items():
        direction = Vector(d).normalized()
        cam.location = target + direction * 2.0
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(regions_dir, "%s__%s.png" % (obj.name, view))
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        written.append(path)
    return written


# --------------------------------------------------------------------------- #
# outputs
# --------------------------------------------------------------------------- #
def f_row(label, s, family):
    regs = [s["regions"].get(r, 0) for r in gk.REGIONS[family] if r != "fill"]
    braids = ", ".join("%d:%d" % (k, v) for k, v in sorted(s["braid_members"].items())) or "-"
    return [label, s["parts"], s["cards"], s["rings"]] + regs + [
        s["fill"], sum(s["braid_members"].values()), braids, s["conflicts"], s["no_valid_uv"], s["uv_fallback"],
        "%.1f" % (s["max_depth"] * 1000)]


def table(rows, header):
    lines = ["| " + " | ".join(header) + " |", "|" + "|".join("---" for _ in header) + "|"]
    lines += ["| " + " | ".join(str(c) for c in r) + " |" for r in rows]
    return lines


def write_outputs(out, inventory):
    with open(os.path.join(out, "inventory.json"), "w", encoding="utf-8") as fh:
        json.dump(inventory, fh, indent=1)
    lines = ["# Groom inventory", "",
             "Generated by tools/blender/kitbash_grooms.py (`--mode inventory`); the data is in `inventory.json`.", "",
             "Head base mesh `%s` bbox (m): %s; sign probe %s." % (
                 HEAD_OBJECT, inventory["head"]["bbox"], inventory["head"]["sign_probe"]), "",
             "**Sources**", ""]
    lines += ["- `%s`: sha1 `%s`" % (k, v) for k, v in sorted(inventory["files"].items())]
    for family in gk.FAMILIES:
        srcs = [s for _, s in sorted(inventory["sources"].items()) if s["family"] == family]
        if not srcs:
            continue
        regs = [r for r in gk.REGIONS[family] if r != "fill"]
        header = (["Groom", "parts", "cards", "rings"] + regs
                  + ["fill", "braid cards", "braid members (ring:cards)", "conflicts", "no valid UV",
                     "UV not first layer", "max depth mm"])
        rows = []
        for s in srcs:
            summ = gk.summarize_groom(s["parts"], s["conflicts"])
            summ["uv_fallback"] = sum(1 for p in s["parts"] if p["uv_layer"] and p["uv_layer"] != s["uv_layers"][0])
            rows.append(f_row(s["object"], summ, family))
        lines += ["", "## %s" % family.capitalize() + "s", ""] + table(rows, header)
    lines += ["", "## Legend (region views in `regions/`)", "",
              "Head grey. Loose rooted cards are coloured by region, braid member cards cyan, rings yellow, "
              "unrooted (fill) cards purple.", ""]
    for family in gk.FAMILIES:
        lines.append("- %s: %s" % (family, ", ".join("%s (%.1f, %.1f, %.1f)" % ((c,) + COLOURS[c])
                                                     for c in CLASSES[family])))
    lines += ["", "Views: front (from +Y), side (from +X), back (from -Y), threeq (from front right, +X +Y). "
              "Orthographic frame %.2f m (beard) and %.2f m (hair)." % (SCALE["beard"], SCALE["hair"]), ""]
    with open(os.path.join(out, "inventory.md"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))


# --------------------------------------------------------------------------- #
# build and preview: the shared groom builder
# --------------------------------------------------------------------------- #
# These modes import with the DEFAULT orientation, like add_mesh_lods.load, so the staged FBX round-trips the way
# the other tools' do (bind pose and axes match the family source). Island ids do not depend on orientation.
DEFAULT_RACE_TEST = ("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
                     "AssetSources/Race Test")
FAMILY_SOURCE = {"beard": "Beards/SK_Dwarf_Beards.fbx", "hair": "Hair/Dwarf_Hairs.fbx"}
FAMILY_SIBLING = {"beard": "SK_Dwarf_Beard_A_01", "hair": "Dwarf_Hair_A_lod0"}
TEX_CARDS = "Beards/Textures/T_Dwarf_Beard_Hair_A_d2.png"
TEX_RING = "Beards/Textures/T_Dwarf_Beard_Ring_A_d.png"
TINT = (0.45, 0.30, 0.18, 1.0)          # multiplied into the card atlas
SKIN = (0.55, 0.40, 0.33, 1.0)
ALPHA_CLIP = 0.5
NORMAL_TOLERANCE_DEG = 2.0


class Source:
    """One imported groom read as arrays: world positions, polygon and loop tables, parts, lazily UVs and normals."""

    def __init__(self, obj, inv):
        me = obj.data
        self.obj, self.inv, self.me = obj, inv, me
        n = len(me.vertices)
        co = np.empty(n * 3, dtype=np.float32)
        me.vertices.foreach_get("co", co)
        self.co = co.reshape(-1, 3).astype(np.float64)
        m = np.array(obj.matrix_world, dtype=np.float64)
        self.m3, self.t = m[:3, :3], m[:3, 3]
        self.start, self.total, self.lv = polygon_arrays(me)
        self.parts = gk.islands(n, polys_of(self.start, self.total, self.lv))
        labels = np.array(gk.vertex_labels(self.parts, n), dtype=np.int64)
        self.poly_label = labels[self.lv[self.start]]
        self.key_names = [k.name for k in me.shape_keys.key_blocks] if me.shape_keys else []
        self._normals = None
        self._uv = {}

    def mismatches(self, ids):
        """Where this import disagrees with the inventory (part count, or a selected part's vertex count)."""
        want = self.inv["parts"]
        out = []
        if len(self.parts) != len(want):
            out.append("%s: %d parts, inventory has %d" % (self.obj.name, len(self.parts), len(want)))
        for i in ids:
            if i >= len(self.parts) or i >= len(want) or len(self.parts[i]) != want[i]["verts"]:
                out.append("%s part %d: vertex count differs from the inventory" % (self.obj.name, i))
        return out

    def world(self, pts):
        return pts @ self.m3.T + self.t

    def normals_world(self):
        if self._normals is None:
            nl = np.empty(len(self.me.loops) * 3, dtype=np.float32)
            self.me.corner_normals.foreach_get("vector", nl)
            n = nl.reshape(-1, 3).astype(np.float64) @ np.linalg.inv(self.m3)
            self._normals = n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
        return self._normals

    def uv(self, layer):
        if layer not in self._uv:
            a = np.empty(len(self.me.loops) * 2, dtype=np.float32)
            self.me.uv_layers[layer].data.foreach_get("uv", a)
            self._uv[layer] = a.reshape(-1, 2).astype(np.float64)
        return self._uv[layer]

    def extract(self, ids, channels):
        """The selected parts as arrays in world space: positions, per-loop normals and UVs (each part's valid
        layer), vertex weights by group name, per-polygon slot kind, and with channels `keep` the 101 channels
        (zero offsets from a source that has none)."""
        ids = sorted(ids)
        parts = self.inv["parts"]
        verts = np.concatenate([np.array(self.parts[i], dtype=np.int64) for i in ids])
        remap = np.full(len(self.co), -1, dtype=np.int64)
        remap[verts] = np.arange(len(verts))
        sel = np.nonzero(np.isin(self.poly_label, ids))[0]
        tot = self.total[sel].astype(np.int64)
        starts = np.concatenate([[0], np.cumsum(tot)[:-1]]).astype(np.int64)
        nloops = int(tot.sum())
        loop_idx = np.repeat(self.start[sel].astype(np.int64) - starts, tot) + np.arange(nloops)
        plabel = self.poly_label[sel].tolist()
        layer_of = [parts[p]["uv_layer"] for p in plabel]
        if None in layer_of:
            raise SystemExit("%s: a selected part has no valid UV layer" % self.obj.name)
        uv = np.empty((nloops, 2))
        for layer in set(layer_of):
            mask = np.repeat(np.array([x == layer for x in layer_of]), tot)
            uv[mask] = self.uv(layer)[loop_idx[mask]]
        names = self.obj.vertex_groups
        me = self.me
        weights = [[(names[g.group].name, g.weight) for g in me.vertices[v].groups] for v in verts.tolist()]
        pos = self.world(self.co[verts])
        keys = None
        if channels == "keep":
            keys = {}
            for name, how in gk.plan_shape_keys(self.key_names):
                if how == "zero":
                    keys[name] = pos.copy()
                    continue
                a = np.empty(len(self.co) * 3, dtype=np.float32)
                me.shape_keys.key_blocks[name].data.foreach_get("co", a)
                keys[name] = self.world(a.reshape(-1, 3).astype(np.float64)[verts])
        return {"pos": pos, "keys": keys, "nrm": self.normals_world()[loop_idx], "uv": uv,
                "loop_vert": remap[self.lv[loop_idx]], "tot": tot,
                "kind": np.array([1 if parts[p]["kind"] == "ring" else 0 for p in plabel], dtype=np.int32),
                "weights": weights, "has_custom": bool(me.has_custom_normals)}


def place(ex, piece):
    """One piece of an extracted group under (mirrored, offset), in world space. A mirrored piece reverses its
    face winding (with its loop data), swaps l_/r_ vertex groups and mirrors the carried normals."""
    mirrored, off = piece
    off = np.array(off, dtype=np.float64)

    def pts(a):
        a = a.copy()
        if mirrored:
            a[:, 0] *= -1
        return a + off

    out = dict(ex)
    out["pos"] = pts(ex["pos"])
    out["keys"] = None if ex["keys"] is None else {k: pts(v) for k, v in ex["keys"].items()}
    if mirrored:
        starts = np.concatenate([[0], np.cumsum(ex["tot"])[:-1]])
        perm = np.array(gk.reverse_loop_order(starts.tolist(), ex["tot"].tolist()), dtype=np.int64)
        nrm = ex["nrm"].copy()
        nrm[:, 0] *= -1
        out.update(nrm=nrm[perm], uv=ex["uv"][perm], loop_vert=ex["loop_vert"][perm],
                   weights=[[(gk.swap_side_group(n), w) for n, w in ws] for ws in ex["weights"]])
    return out


def concat(pieces):
    voff, out = 0, {k: [] for k in ("pos", "nrm", "uv", "loop_vert", "tot", "kind", "weights")}
    keys = {}
    for p in pieces:
        out["pos"].append(p["pos"])
        out["nrm"].append(p["nrm"])
        out["uv"].append(p["uv"])
        out["loop_vert"].append(p["loop_vert"] + voff)
        out["tot"].append(p["tot"])
        out["kind"].append(p["kind"])
        out["weights"].extend(p["weights"])
        if p["keys"] is not None:
            for k, v in p["keys"].items():
                keys.setdefault(k, []).append(v)
        voff += len(p["pos"])
    merged = {k: (v if k == "weights" else np.concatenate(v)) for k, v in out.items()}
    merged["keys"] = {k: np.concatenate(v) for k, v in keys.items()} if keys else None
    if merged["keys"] is not None and any(len(v) != len(merged["pos"]) for v in merged["keys"].values()):
        raise SystemExit("some pieces carry channels and some do not")
    merged["has_custom"] = any(p["has_custom"] for p in pieces)
    return merged


def get_material(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def make_groom(ctx, name, data):
    """A copy of the sibling LOD0 (so parent and Armature modifier match) carrying the joined mesh."""
    if name in bpy.data.objects:
        raise SystemExit("an object named %r already exists" % name)
    sib, arm = ctx.sibling, ctx.armature
    m = np.array(sib.matrix_world, dtype=np.float64)
    m3, t = m[:3, :3], m[:3, 3]
    inv3 = np.linalg.inv(m3)
    local = (data["pos"] - t) @ inv3.T
    tot = data["tot"]
    starts = np.concatenate([[0], np.cumsum(tot)[:-1]]).tolist()
    lvl = data["loop_vert"].tolist()
    faces = [lvl[s:s + c] for s, c in zip(starts, tot.tolist())]
    me = bpy.data.meshes.new(name)
    me.from_pydata(local.tolist(), [], faces)
    if len(me.loops) != len(lvl) or len(me.polygons) != len(faces):
        raise SystemExit("%s: the built mesh lost faces" % name)
    me.validate(verbose=False)
    if len(me.loops) != len(lvl):
        raise SystemExit("%s: validate removed faces from the built mesh" % name)
    layer = me.uv_layers.new(name="UVMap")
    layer.data.foreach_set("uv", data["uv"].astype(np.float32).ravel())
    if data["has_custom"]:
        nl = data["nrm"] @ m3
        nl = nl / np.maximum(np.linalg.norm(nl, axis=1, keepdims=True), 1e-12)
        me.normals_split_custom_set(nl.tolist())
    # A groom with no ring faces gets the cards slot only, as the ring-less siblings ship.
    has_ring = bool(np.any(np.asarray(data["kind"]) == 1))
    for mat_name in ctx.mat_names[:2 if has_ring else 1]:
        me.materials.append(get_material(mat_name))
    me.polygons.foreach_set("material_index", data["kind"])
    obj = sib.copy()
    obj.data = me
    obj.name = name
    me.name = name
    for coll in sib.users_collection:
        coll.objects.link(obj)

    bones = set(arm.data.bones.keys())
    groups, unweighted = {}, 0
    for i, ws in enumerate(data["weights"]):
        used = False
        for g, w in ws:
            if w <= 0:
                continue
            vg = groups.get(g) or obj.vertex_groups.get(g)
            if vg is None:
                if g not in bones:
                    raise SystemExit("%s: vertex group %r is not a bone of %s" % (name, g, arm.name))
                vg = obj.vertex_groups.new(name=g)
            groups[g] = vg
            vg.add([i], w, "REPLACE")
            used = True
        unweighted += 0 if used else 1
    if unweighted:
        raise SystemExit("%s: %d built vertices carry no weight" % (name, unweighted))

    if data["keys"] is not None:
        obj.shape_key_add(name="Basis", from_mix=False)
        for n in gk.SHAPE_NAMES:
            kb = obj.shape_key_add(name=n, from_mix=False)
            kb.data.foreach_set("co", ((data["keys"][n] - t) @ inv3.T).astype(np.float32).ravel())
        keys = [k.name for k in me.shape_keys.key_blocks]
        if keys != gk.expected_keys():
            raise SystemExit("%s: shape key order is not Basis, shape_01..shape_101" % name)
    return obj


def build_groom(ctx, name, selected, channels):
    """The ONE code path for build and preview: resolved (source, part, transforms) rows -> a groom object."""
    pieces, per_source = [], {}
    for source, ids, transforms in gk.group_parts(selected):
        ex = ctx.source(source).extract(ids, channels)
        per_source[source] = per_source.get(source, 0) + len(ids)
        pieces.extend(place(ex, piece) for piece in gk.plan_pieces(transforms))
    data = concat(pieces)
    obj = make_groom(ctx, name, data)
    tris = int((data["tot"] - 2).sum())
    return obj, {"verts": int(len(data["pos"])), "tris": tris, "parts": per_source, "pieces": len(pieces),
                 "channels": channels, "custom_normals": data["has_custom"]}


class Ctx:
    """The scene a family build or preview runs in: the family source first (its armature keeps the plain name)."""

    def __init__(self, args, inv, recipe, family, sources, with_head, save):
        self.inv, self.family, self.root = inv, family, args.race_test
        self.mat_names = None
        mats = recipe.get("families", {}).get(family, {}).get("materials", {})
        if "cards" not in mats or "ring" not in mats:
            raise SystemExit("recipe families.%s.materials needs cards and ring names" % family)
        self.mat_names = [mats["cards"], mats["ring"]]
        first = FAMILY_SOURCE[family]
        wanted = {inv["sources"][s]["file"] for s in sources}
        files = [first] + sorted(wanted - {first})
        for rel in files:
            save("hashing " + rel)
            if sha1_of(os.path.join(self.root, rel)) != inv["files"][rel]:
                raise SystemExit("sha1 of %s differs from the inventory" % rel)
        self._sources, self.found = {}, {}
        for i, rel in enumerate(files):
            save("importing " + rel)
            path = os.path.join(self.root, rel)
            before = set(bpy.data.objects)
            if i == 0:
                lods.load(path)
                before = set()
            else:
                bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
            new = [o for o in bpy.data.objects if o not in before]
            if i == 0:
                arms = [o for o in new if o.type == "ARMATURE"]
                if len(arms) != 1:
                    raise SystemExit("%s holds %d armatures, expected 1" % (rel, len(arms)))
                self.armature = arms[0]
                self.sibling = next((o for o in new if o.name == FAMILY_SIBLING[family]), None)
                if self.sibling is None:
                    raise SystemExit("sibling %s not found in %s" % (FAMILY_SIBLING[family], rel))
            for s in sources:
                if inv["sources"][s]["file"] == rel:
                    obj = next((o for o in new if o.name == s), None)
                    if obj is None:
                        raise SystemExit("source object %s not found in %s" % (s, rel))
                    self.found[s] = obj
        self.head = None
        if with_head:
            save("importing " + HEAD_FBX)
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=os.path.join(self.root, HEAD_FBX), automatic_bone_orientation=False)
            self.head = next((o for o in bpy.data.objects if o not in before and o.name == HEAD_OBJECT), None)
            if self.head is None:
                raise SystemExit("head object %s not found" % HEAD_OBJECT)

    def source(self, name):
        if name not in self._sources:
            self._sources[name] = Source(self.found[name], self.inv["sources"][name])
        return self._sources[name]

    def cross_check(self, selected):
        """Refuse when any selected part's vertex count (or a source's part count) differs from the inventory."""
        problems = []
        for name in sorted({s for s, _, _ in selected}):
            problems += self.source(name).mismatches([p for s, p, _ in selected if s == name])
        if problems:
            raise SystemExit("inventory mismatch: " + "; ".join(problems[:10]))


def vertex_normals(obj):
    """Per-vertex mean of the corner normals, in world space (comparable whatever the loop order)."""
    me = obj.data
    nl = np.empty(len(me.loops) * 3, dtype=np.float32)
    me.corner_normals.foreach_get("vector", nl)
    m3 = np.array(obj.matrix_world, dtype=np.float64)[:3, :3]
    n = nl.reshape(-1, 3).astype(np.float64) @ np.linalg.inv(m3)
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
    lv = np.empty(len(me.loops), dtype=np.int32)
    me.loops.foreach_get("vertex_index", lv)
    acc = np.zeros((len(me.vertices), 3))
    np.add.at(acc, lv, n)
    return acc / np.maximum(np.linalg.norm(acc, axis=1, keepdims=True), 1e-12)


def load_json(path, what):
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, ValueError) as exc:
        raise SystemExit("cannot read %s %s: %s" % (what, path, exc))


def prepare(args, report, save):
    """Shared by build and preview: read and validate the inventory and recipe, resolve the family's candidates."""
    inv = load_json(args.inventory, "inventory")
    recipe = load_json(args.recipe, "recipe")
    errors = gk.validate_recipe(recipe, inv)
    if errors:
        report["recipe_errors"] = errors
        raise SystemExit("recipe is not valid (%d errors, first: %s)" % (len(errors), errors[0]))
    resolved = gk.resolve(recipe, inv)
    cands = [c for c in recipe["candidates"] if c["family"] == args.family]
    return inv, recipe, resolved, cands


# --------------------------------------------------------------------------- #
# build
# --------------------------------------------------------------------------- #
def roundtrip(staged, builds, ctx_info, report, save):
    """Re-import the staged FBX and check it. Gated checks go in report["roundtrip"]["checks"]; the normal and
    position comparison is reported only. Returns True when every gated check passed."""
    save("reimporting")
    lods.load(staged)
    meshes = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    checks, info = [], {}

    def check(name, ok, detail=""):
        checks.append({"check": name, "ok": bool(ok), "detail": detail})

    check("object names", sorted(meshes) == sorted(builds), {"found": sorted(meshes), "expected": sorted(builds)})
    check("one armature, named as the family source", [a.name for a in arms] == [ctx_info["armature"]],
          [a.name for a in arms])
    bones = set(arms[0].data.bones.keys()) if arms else set()
    mats = ctx_info["mat_names"]
    used_mats = sorted(m.name for m in bpy.data.materials if m.users)
    check("material datablocks are the family names only",
          set(used_mats) <= set(mats) and mats[0] in used_mats,
          sorted(m.name for m in bpy.data.materials))
    for name, (_, channels, ref) in sorted(builds.items()):
        o = meshes.get(name)
        if o is None:
            continue
        me = o.data
        n = len(me.vertices)
        start, total, lv = polygon_arrays(me)
        parts = gk.islands(n, polys_of(start, total, lv))
        labels = np.array(gk.vertex_labels(parts, n), dtype=np.int64)
        layers = [l.name for l in me.uv_layers]
        collapsed = None
        if layers == ["UVMap"]:
            uv = np.empty(len(me.loops) * 2, dtype=np.float32)
            me.uv_layers["UVMap"].data.foreach_get("uv", uv)
            spans = span_per_part(uv.reshape(-1, 2).astype(np.float64), labels[lv], len(parts))
            collapsed = sum(1 for sp in spans if gk.valid_uv_layer({"UVMap": sp}) is None)
        check("%s: exactly one UV layer UVMap" % name, layers == ["UVMap"], layers)
        check("%s: every part has non-zero UV extent" % name, collapsed == 0,
              "%s of %d parts collapsed" % (collapsed, len(parts)))
        slots = [s.material.name if s.material else None for s in o.material_slots]
        check("%s: material slots are the family's cards (+ ring) names" % name, slots in (mats[:1], mats[:2]), slots)
        idx = [g.group for v in me.vertices for g in v.groups if g.weight > 0]
        loose = sum(1 for v in me.vertices if not any(g.weight > 0 for g in v.groups))
        used = {o.vertex_groups[i].name for i in set(idx)}
        check("%s: every vertex weighted" % name, loose == 0, "%d unweighted of %d" % (loose, n))
        check("%s: weighted only by bones of the armature" % name, used <= bones, sorted(used - bones))
        keys = [k.name for k in me.shape_keys.key_blocks] if me.shape_keys else []
        count = max(len(keys) - 1, 0)
        want = 101 if channels == "keep" else 0
        order_ok = channels != "keep" or keys == gk.expected_keys()
        check("%s: channel count %d" % (name, want), count == want and order_ok,
              {"count": count, "first": keys[:2], "last": keys[-1:]})
        entry = {"verts": n, "parts": len(parts), "loops": len(me.loops), "uv_layers": layers, "slots": slots,
                 "channels": count, "custom_normals": bool(me.has_custom_normals)}
        wc = world_coords(o)
        entry["bbox_world_m"] = [wc.min(axis=0).round(4).tolist(), wc.max(axis=0).round(4).tolist()]
        # a wrong winding shows as face normals opposing the loop normals
        pn = np.empty(len(me.polygons) * 3, dtype=np.float32)
        me.polygons.foreach_get("normal", pn)
        cn = np.empty(len(me.loops) * 3, dtype=np.float32)
        me.corner_normals.foreach_get("vector", cn)
        poly_of_loop = np.repeat(np.arange(len(me.polygons)), total)
        entry["face_vs_loop_normal_agree"] = round(float(
            (np.sum(pn.reshape(-1, 3)[poly_of_loop] * cn.reshape(-1, 3), axis=1) > 0).mean()), 4)
        if n == len(ref["pos"]):
            dn = vertex_normals(o)
            cos = np.clip(np.sum(dn * ref["nrm"], axis=1), -1.0, 1.0)
            ang = np.degrees(np.arccos(cos))
            entry["normals"] = {"max_deg": round(float(ang.max()), 3), "mean_deg": round(float(ang.mean()), 4),
                                "share_over_tolerance": round(float((ang > NORMAL_TOLERANCE_DEG).mean()), 4),
                                "tolerance_deg": NORMAL_TOLERANCE_DEG}
            entry["position_max_dev_m"] = round(float(np.abs(world_coords(o) - ref["pos"]).max()), 7)
        else:
            entry["normals"] = "vertex count changed: %d built, %d re-imported" % (len(ref["pos"]), n)
        info[name] = entry
    report["roundtrip"] = {"checks": checks, "grooms": info, "normals_note": "reported only, not gated"}
    return all(c["ok"] for c in checks)


def refused_target(target, root):
    t = os.path.normcase(os.path.abspath(target))
    names = [s[0] for s in SOURCES] + [HEAD_FBX]
    return any(os.path.normcase(os.path.abspath(os.path.join(root, n))) == t for n in names)


def run_build(args, report, save):
    t0 = time.time()
    if refused_target(args.target, args.race_test):
        raise SystemExit("refusing --target %s: it is a source FBX or the head" % args.target)
    inside = os.path.normcase(os.path.abspath(args.out))
    root = os.path.normcase(os.path.abspath(args.race_test))
    if os.path.commonpath([inside, root]) == root:
        raise SystemExit("--out must not be inside the Race Test folder")
    inv, recipe, resolved, cands = prepare(args, report, save)
    finals = gk.family_finals(recipe, args.family)
    if not finals:
        raise SystemExit("the recipe has no final entries for family %s" % args.family)
    sources = sorted({s for _, cid, _ in finals for s, _, _ in resolved[cid]})
    ctx = Ctx(args, inv, recipe, args.family, sources, False, save)
    report.update(family=args.family, armature=ctx.armature.name, sibling=ctx.sibling.name, sources=sources,
                  finals=[{"name": n, "candidate": c, "channels": ch} for n, c, ch in finals])
    for _, cid, _ in finals:
        ctx.cross_check(resolved[cid])
    report["cross_check"] = "every selected part's vertex count matches the inventory"

    builds, stats = {}, {}
    for name, cid, channels in finals:
        save("building " + name)
        obj_name = gk.object_name(name, args.family)
        obj, st = build_groom(ctx, obj_name, resolved[cid], channels)
        builds[obj_name] = (obj, channels, {"pos": world_coords(obj), "nrm": vertex_normals(obj)})
        stats[obj_name] = dict(st, candidate=cid)
    report["built"] = stats

    save("exporting")
    keep = {ctx.armature} | {b[0] for b in builds.values()}
    for o in list(bpy.data.objects):
        if o not in keep:
            bpy.data.objects.remove(o, do_unlink=True)
    stem = os.path.splitext(os.path.basename(args.target))[0]
    staged = os.path.join(args.out, stem + ".kitbash-staged.fbx")
    ctx_info = {"armature": ctx.armature.name, "mat_names": ctx.mat_names}
    lods.export(staged)
    report["staged"] = staged
    ok = roundtrip(staged, {n: (b[0].name, b[1], b[2]) for n, b in builds.items()}, ctx_info, report, save)
    report["roundtrip_ok"] = ok
    report["ok"] = ok
    if ok and args.apply:
        bak = args.target + ".bak-kitbash"
        if os.path.exists(args.target) and not os.path.exists(bak):
            shutil.copy2(args.target, bak)
            report["backup"] = bak
        shutil.copy2(staged, args.target)
        report["applied"] = True
    else:
        report["applied"] = False
    report["build_s"] = round(time.time() - t0, 1)


# --------------------------------------------------------------------------- #
# preview
# --------------------------------------------------------------------------- #
def view_dirs(f):
    """Camera directions for a head facing f (+1 or -1) along Y: the default import faces -Y."""
    return {"front": (0.0, f, 0.0), "side": (1.0, 0.0, 0.0), "back": (0.0, -f, 0.0),
            "threeq": (0.62, 0.78 * f, 0.15)}


def textured_material(name, image, tint, clip):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:  # noqa: BLE001 - always on in newer Blender
        pass
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.65
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    mix.inputs[7].default_value = tint
    nt.links.new(tex.outputs["Color"], mix.inputs[6])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    if clip:
        gt = nt.nodes.new("ShaderNodeMath")
        gt.operation = "GREATER_THAN"
        gt.inputs[1].default_value = ALPHA_CLIP
        nt.links.new(tex.outputs["Alpha"], gt.inputs[0])
        nt.links.new(gt.outputs[0], bsdf.inputs["Alpha"])
        for prop, value in (("surface_render_method", "DITHERED"), ("blend_method", "CLIP")):
            try:
                setattr(m, prop, value)
            except Exception:  # noqa: BLE001 - which of these exists depends on the Blender version
                pass
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def check_atlas(path, image):
    """Alpha facts about the card atlas, from Blender's own reading of it."""
    px = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    alpha = px[3::4]
    return {"path": path, "channels": image.channels, "size": list(image.size), "alpha_min": round(float(alpha.min()), 3),
            "alpha_max": round(float(alpha.max()), 3), "has_alpha": bool(alpha.min() < 1.0)}


def setup_preview_render(renderer, family, facing):
    sc = bpy.context.scene
    engines = [e.identifier for e in sc.render.bl_rna.properties["engine"].enum_items]
    eevee = next((e for e in engines if "EEVEE" in e), None)
    use = "workbench" if renderer == "workbench" or eevee is None else "eevee"
    sc.render.engine = eevee if use == "eevee" else "BLENDER_WORKBENCH"
    sc.render.resolution_x = sc.render.resolution_y = RENDER_SIZE
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("kitbash_world")
    try:
        world.use_nodes = True
        bg = world.node_tree.nodes.get("Background")
        if bg is not None:
            bg.inputs[0].default_value = (0.30, 0.30, 0.32, 1.0)
            bg.inputs[1].default_value = 1.0
    except Exception:  # noqa: BLE001
        pass
    world.color = (0.30, 0.30, 0.32)
    sc.world = world
    if use == "workbench":
        sc.display.shading.light = "STUDIO"
        sc.display.shading.color_type = "TEXTURE"
        sc.display.render_aa = "8"
    else:
        for prop, value in (("taa_render_samples", 24),):
            try:
                setattr(sc.eevee, prop, value)
            except Exception:  # noqa: BLE001
                pass
    sun_data = bpy.data.lights.new("kitbash_sun", "SUN")
    sun_data.energy = 3.5
    sun = bpy.data.objects.new("kitbash_sun", sun_data)
    sc.collection.objects.link(sun)
    sun.rotation_euler = (-Vector((0.35, 0.8 * facing, 0.6))).to_track_quat("-Z", "Y").to_euler()
    cam_data = bpy.data.cameras.new("kitbash_cam")
    cam_data.type = "ORTHO"
    cam_data.clip_end = 10.0
    cam_data.ortho_scale = SCALE[family]
    cam = bpy.data.objects.new("kitbash_cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam, use


def render_views(sc, cam, obj, head, family, facing, folder, cid):
    for o in bpy.data.objects:
        if o.type in ("MESH", "ARMATURE", "EMPTY"):
            o.hide_render = o not in (obj, head)
    cx, cy, cz = CENTRE[family]
    target = Vector((cx, facing * cy, cz))
    written = []
    for view, d in view_dirs(facing).items():
        direction = Vector(d).normalized()
        cam.location = target + direction * 2.0
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(folder, "%s__%s.png" % (cid, view))
        bpy.ops.render.render(write_still=True)
        written.append(sc.render.filepath)
    return written


def run_preview(args, report, save):
    t0 = time.time()
    inv, recipe, resolved, cands = prepare(args, report, save)
    if not cands:
        raise SystemExit("the recipe has no candidates for family %s" % args.family)
    sources = sorted({s for c in cands for s, _, _ in resolved[c["id"]]})
    ctx = Ctx(args, inv, recipe, args.family, sources, True, save)
    for c in cands:
        ctx.cross_check(resolved[c["id"]])
    folder = os.path.join(args.out, "preview")
    os.makedirs(folder, exist_ok=True)

    built = {}
    for c in cands:
        save("building " + c["id"])
        obj, st = build_groom(ctx, "pv_" + c["id"], resolved[c["id"]], "drop")
        built[c["id"]] = (obj, st)

    save("setting up the render")
    head_co = world_coords(ctx.head)
    facing = 1.0 if head_co[:, 1].max() > -head_co[:, 1].min() else -1.0
    sc, cam, renderer = setup_preview_render(args.renderer, args.family, facing)
    report.update(family=args.family, facing=facing, renderer=renderer)
    img_cards = bpy.data.images.load(os.path.join(args.race_test, TEX_CARDS))
    img_ring = bpy.data.images.load(os.path.join(args.race_test, TEX_RING))
    report["atlas"] = check_atlas(TEX_CARDS, img_cards)
    cards = textured_material("pv_cards", img_cards, TINT, True)
    ring = textured_material("pv_ring", img_ring, (1.0, 1.0, 1.0, 1.0), False)
    skin = bpy.data.materials.new("pv_skin")
    skin.diffuse_color = SKIN
    skin.use_nodes = True
    skin.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = SKIN
    hm = ctx.head.data
    hm.materials.clear()
    hm.materials.append(skin)
    hm.polygons.foreach_set("material_index", np.zeros(len(hm.polygons), dtype=np.int32))
    meta = {"family": args.family, "renderer": renderer, "atlas": report["atlas"], "candidates": []}
    for c in cands:
        obj, st = built[c["id"]]
        obj.data.materials.clear()
        obj.data.materials.append(cards)
        obj.data.materials.append(ring)
        save("rendering " + c["id"])
        files = None
        try:
            files = render_views(sc, cam, obj, ctx.head, args.family, facing, folder, c["id"])
        except Exception as exc:  # noqa: BLE001 - Eevee may not run headless: fall back, and say so
            if renderer != "eevee":
                report.setdefault("render_errors", []).append("%s: %s: %s" % (c["id"], type(exc).__name__, exc))
            else:
                report["eevee_error"] = "%s: %s" % (type(exc).__name__, exc)
                sc.render.engine = "BLENDER_WORKBENCH"
                sc.display.shading.light = "STUDIO"
                sc.display.shading.color_type = "TEXTURE"
                renderer = "workbench (eevee failed)"
                report["renderer"] = renderer
                meta["renderer"] = renderer
                files = render_views(sc, cam, obj, ctx.head, args.family, facing, folder, c["id"])
        report["pngs"] = report.get("pngs", 0) + len(files or [])
        meta["candidates"].append({"id": c["id"], "note": c.get("note", ""),
                                   "sources": sorted({p["source"] for p in c["parts"]}), "tris": st["tris"],
                                   "verts": st["verts"], "files": [os.path.basename(f) for f in files or []]})
    with open(os.path.join(args.out, "previews.json"), "w", encoding="utf-8") as fh:
        json.dump(meta, fh, indent=1)
    report["ok"] = not report.get("render_errors")
    report["preview_s"] = round(time.time() - t0, 1)


# --------------------------------------------------------------------------- #
# inventory
# --------------------------------------------------------------------------- #
def import_fbx(path):
    """Import into the frame groom_kitbash.py's rules are written in (the FBX's own: Z up, face toward +Y).
    The default import ignores the file's axes and turns the dwarf to face -Y (Blender's front), so the
    orientation is set by hand; the 180 degree turn about Z is then carried by matrix_world. Only the inventory
    uses this: build and preview import with the default, like add_mesh_lods.load."""
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, use_manual_orientation=True,
                             axis_forward="Y", axis_up="Z")


def run_inventory(args, report, save):
    out = args.out
    regions_dir = os.path.join(out, "regions")
    os.makedirs(regions_dir, exist_ok=True)
    root = args.race_test
    head_path = os.path.join(root, HEAD_FBX)
    for rel in [HEAD_FBX] + [s[0] for s in SOURCES]:
        if not os.path.isfile(os.path.join(root, rel)):
            raise SystemExit("missing source file %s" % os.path.join(root, rel))

    inventory = {"version": 1, "head": None, "files": {}, "sources": {}}
    for rel, _, _ in SOURCES:
        save("hashing " + rel)
        inventory["files"][rel] = sha1_of(os.path.join(root, rel))

    for rel, family, pattern in SOURCES:
        save("importing " + rel)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        import_fbx(head_path)
        head_obj = bpy.data.objects[HEAD_OBJECT]
        head = Head(head_obj)
        inventory["head"] = {"object": HEAD_OBJECT, "bbox": head.bbox, "sign_probe": head.probe,
                             "sign_flipped": head.flip < 0, "sign_ok": head.sign_ok}
        before = set(bpy.data.objects)
        import_fbx(os.path.join(root, rel))
        new = [o for o in bpy.data.objects if o not in before]
        mesh_names = sorted(o.name for o in new if o.type == "MESH")
        grooms = sorted((o for o in new if o.type == "MESH" and pattern.match(o.name)), key=lambda o: o.name)
        report.setdefault("found", {})[rel] = [o.name for o in grooms]
        recs = {}
        for obj in grooms:
            save("analysing " + obj.name)
            recs[obj.name] = analyse_groom(obj, family, head, inventory["files"][rel], rel, mesh_names)
            inventory["sources"][obj.name] = recs[obj.name]
        save("writing after " + rel)
        write_outputs(out, inventory)

        sc, cam = setup_render()
        mats = {c: colour_material("kb_" + c, COLOURS[c]) for c in COLOURS}
        head_mat = colour_material("kb_head", HEAD_GREY[:3])
        for obj in grooms:
            save("rendering " + obj.name)
            try:
                report["pngs"] += len(render_groom(obj, recs[obj.name], head_obj, regions_dir, sc, cam,
                                                   mats, head_mat))
            except Exception as exc:  # noqa: BLE001 - a render failure must not lose the inventory
                report["render_errors"].append("%s: %s: %s" % (obj.name, type(exc).__name__, exc))
            report["grooms_done"].append(obj.name)
        save("rendered " + rel)

    write_outputs(out, inventory)
    report["ok"] = True


# --------------------------------------------------------------------------- #
# main
# --------------------------------------------------------------------------- #
def check_args(args):
    """Per-mode argument rules; a SystemExit message lands in the report."""
    if args.mode == "inventory":
        if not args.race_test:
            raise SystemExit("bad arguments: --race-test is required for --mode inventory")
        args.out = args.out or DEFAULT_OUT
    elif args.mode in ("build", "preview"):
        args.race_test = args.race_test or DEFAULT_RACE_TEST
        need = ["inventory", "recipe", "family", "out"] + (["target"] if args.mode == "build" else [])
        missing = [n for n in need if not getattr(args, n)]
        if missing:
            raise SystemExit("bad arguments: --mode %s needs --%s" % (args.mode, ", --".join(missing)))
    else:
        raise SystemExit("mode %r is not implemented (inventory, build, preview)" % args.mode)


def main():
    t0 = time.time()
    argv = list(sys.argv)
    paths = {"report": os.path.join(out_from_argv(argv), "kitbash-report.json")}
    os.makedirs(os.path.dirname(paths["report"]), exist_ok=True)
    report = {"stage": "start", "ok": False, "error": None, "mode": None, "out": out_from_argv(argv),
              "grooms_done": [], "render_errors": [], "pngs": 0}

    def save(stage):
        # A native crash leaves no traceback and the launcher shows no stdout: rewrite the report at each step.
        report["stage"] = stage
        report["elapsed_s"] = round(time.time() - t0, 1)
        with open(paths["report"], "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        save("start")
        args = parse_args(argv)
        report["mode"] = args.mode
        check_args(args)
        os.makedirs(args.out, exist_ok=True)
        paths["report"] = os.path.join(args.out, "kitbash-report.json")
        report["out"] = args.out
        {"inventory": run_inventory, "build": run_build, "preview": run_preview}[args.mode](args, report, save)
    except SystemExit as exc:
        report["error"] = str(exc)
    except Exception as exc:  # noqa: BLE001 - a Blender-side failure must still reach the caller
        import traceback
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
        report["traceback"] = traceback.format_exc()

    save("done")


if __name__ == "__main__":
    main()
