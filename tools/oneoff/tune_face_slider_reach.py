#!/usr/bin/env python3
"""
Tune a race head's face sliders in the live LOTRLOME_Armory skins.xml so no slider moves the face further than the
same slider moves the male dwarf's (Mike, 2026-09-29: "the range of the deform keys, especially for the female
dwarf, are a bit too much").

WHY
Every custom race copied vanilla's human slider ranges verbatim (male dwarf 58 of 60 keys equal vanilla male,
Saruman 59 of 59, the adult female dwarf 59 of 60 equal vanilla female). A range is a morph channel WEIGHT at each
end of the slider (`key_min`, `key_max`; `key_time_point` names the channel), and vanilla tuned those weights for
its own head's channels. The LOTRLOME female dwarf and Saruman heads were authored with larger channels, so the same
weights move their faces up to 3 times (female dwarf) and 9 times (Saruman) as far as the male dwarf's. The male
dwarf is the yardstick: it runs vanilla's ranges and moves acceptably (vanilla's own head packages do not open in
TpacTool 0.4.0, so vanilla's travel in millimetres cannot be measured directly).

WHAT IT DOES
A slider's reach is its channel's largest vertex travel on the LOD0 face base mesh times the larger of |key_min|
and |key_max|. For each skin whose `face_meta_mesh` is `--target`, every ranged deform_key reaching more than
TOLERANCE times the same key's reach on the reference skin is scaled toward weight 0 by reference / target reach:
both ends shrink by one factor, so the slider position that gives weight 0 (the head as authored) does not move.
Keys the reference barely moves (under 0.1 mm) are left alone. `--zero <id>` sets a key's range to 0..0 on the
target (Saruman's `eyebump`, fixed at weight 1 by vanilla, bends his face 13 mm from his FBX). `--reach-zero <id>`
moves the nearer end of a range that excludes 0 to 0, so the head as authored is reachable on that slider (Saruman's
`face_ratio`, vanilla's 0.5 to 1.1, otherwise holds his face at least 5 mm off his FBX). `--set <mesh>:<id>=<min>:<max>`
pins a range judged by eye in game (the female dwarf's `eye_depth`, whose eyes stuck out at the slider's end).
Channel travel comes from `tools/export_face_morphs.ps1` JSON exports of the compiled packages.

Dry run by default (prints every change); `--apply` writes a `.bak-slider-reach-<stamp>` backup, then the file,
byte-faithful, after checking it parses. Idempotent: a second run changes nothing. `--check` exits 1 while any
target key still reaches past the reference (the reinstall gate: the Armory is unversioned).

    python tools/oneoff/tune_face_slider_reach.py --ref-mesh sm_dwarf_basemesh_a1_head --ref-json <male.json> ^
        --target sk_dwarf_bm_f1_head <female.json> --target sk_saruman_head <saruman.json> --zero sk_saruman_head:eyebump ^
        --reach-zero sk_saruman_head:face_ratio --set sk_dwarf_bm_f1_head:eye_depth=1:0.5
"""
import argparse
import datetime
import json
import math
import re
import shutil
import sys
import xml.etree.ElementTree as ET

DEFAULT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\skins.xml"
TOLERANCE = 1.15
MIN_REF_MM = 0.1
KEY_RE = re.compile(r"<deform_key\b[^>]*?/?>", re.S)
ATTR_RE = re.compile(r'(\w+)="([^"]*)"')


def channel_travel(export):
    """Largest vertex travel (m) per channel of an export_face_morphs.ps1 face_base_mesh."""
    d = export["face_base_mesh"]
    base = d["positions"]
    return [max(math.dist(base[i], f[3 * i:3 * i + 3]) for i in range(len(base))) for f in d["frames"]]


def skin_spans(text, face_mesh):
    """(start, end) of every <skin> whose face_meta_mesh is `face_mesh`."""
    spans, pos = [], 0
    needle = 'face_meta_mesh="%s"' % face_mesh
    while True:
        i = text.find(needle, pos)
        if i < 0:
            return spans
        s = text.rfind("<skin", 0, i)
        e = text.find("</skin>", i)
        spans.append((s, e))
        pos = e


def ranged_keys(block):
    out = []
    for m in KEY_RE.finditer(block):
        a = dict(ATTR_RE.findall(m.group(0)))
        if "key_min" in a and "key_max" in a and "key_time_point" in a:
            out.append((m, a))
    return out


def reach(travel, a):
    c = int(a["key_time_point"])
    return (travel[c] if c < len(travel) else 0.0) * max(abs(float(a["key_min"])), abs(float(a["key_max"])))


def fmt(v):
    s = ("%.3f" % v).rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def parse_set(spec):
    """FACE_MESH:KEY_ID=MIN:MAX -> (face_mesh, key id, min, max)."""
    try:
        target, rng = spec.split("=", 1)
        mesh, key = target.split(":", 1)
        lo, hi = rng.split(":", 1)
        return mesh, key, float(lo), float(hi)
    except ValueError:
        raise SystemExit("--set wants FACE_MESH:KEY_ID=MIN:MAX, got %r" % spec)


def plan(text, ref_mesh, ref_travel, targets, zero=(), reach_zero=(), set_ranges=()):
    """The new text and a list of change records. `targets` is [(face_mesh, travel)]; `zero` [(face_mesh, key id)]
    pins a key at weight 0; `reach_zero` [(face_mesh, key id)] moves the nearer end of a range that excludes 0 to 0,
    so the head as authored is reachable on that slider; `set_ranges` [(face_mesh, key id, min, max)] pins a range
    chosen by eye in game, and wins over everything else."""
    ref_spans = skin_spans(text, ref_mesh)
    if not ref_spans:
        raise SystemExit("no skin uses the reference face mesh %s" % ref_mesh)
    ref = {a["id"]: reach(ref_travel, a) for _, a in ranged_keys(text[slice(*ref_spans[0])])}
    zero_ids, reach_ids = {}, {}
    for mesh, key in zero:
        zero_ids.setdefault(mesh, set()).add(key)
    for mesh, key in reach_zero:
        reach_ids.setdefault(mesh, set()).add(key)
    pinned = {(mesh, key): (lo, hi) for mesh, key, lo, hi in set_ranges}
    edits, changes = [], []
    for mesh, travel in targets:
        spans = skin_spans(text, mesh)
        if not spans:
            raise SystemExit("no skin uses the target face mesh %s" % mesh)
        for s, e in spans:
            for m, a in ranged_keys(text[s:e]):
                lo, hi = float(a["key_min"]), float(a["key_max"])
                new = (lo, hi)
                if (mesh, a["id"]) in pinned:
                    new = pinned[(mesh, a["id"])]
                elif a["id"] in zero_ids.get(mesh, ()):
                    new = (0.0, 0.0)
                else:
                    r, t = ref.get(a["id"], 0.0), reach(travel, a)
                    if r >= MIN_REF_MM / 1000 and t > r * TOLERANCE:
                        f = r / t
                        new = (lo * f, hi * f)
                    if a["id"] in reach_ids.get(mesh, ()) and new[0] * new[1] > 0:
                        new = (0.0, new[1]) if abs(new[0]) < abs(new[1]) else (new[0], 0.0)
                if (fmt(new[0]), fmt(new[1])) == (fmt(lo), fmt(hi)):
                    continue
                old = m.group(0)
                rep = re.sub(r'key_min="[^"]*"', 'key_min="%s"' % fmt(new[0]), old)
                rep = re.sub(r'key_max="[^"]*"', 'key_max="%s"' % fmt(new[1]), rep)
                edits.append((s + m.start(), s + m.end(), rep))
                changes.append((mesh, a["id"], a["key_min"], a["key_max"], fmt(new[0]), fmt(new[1])))
    out, pos = [], 0
    for a, b, rep in sorted(edits):
        out.append(text[pos:a])
        out.append(rep)
        pos = b
    out.append(text[pos:])
    return "".join(out), changes


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--skins", default=DEFAULT)
    ap.add_argument("--ref-mesh", required=True)
    ap.add_argument("--ref-json", required=True)
    ap.add_argument("--target", nargs=2, action="append", metavar=("FACE_MESH", "JSON"), required=True)
    ap.add_argument("--zero", action="append", default=[], metavar="FACE_MESH:KEY_ID")
    ap.add_argument("--reach-zero", action="append", default=[], metavar="FACE_MESH:KEY_ID")
    ap.add_argument("--set", action="append", default=[], metavar="FACE_MESH:KEY_ID=MIN:MAX")
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)
    load = lambda p: channel_travel(json.load(open(p, encoding="utf-8-sig")))
    targets = [(mesh, load(path)) for mesh, path in args.target]
    zero = [tuple(z.split(":", 1)) for z in args.zero]
    reach_zero = [tuple(z.split(":", 1)) for z in args.reach_zero]
    raw = open(args.skins, "rb").read()
    text = raw.decode("utf-8")
    new, changes = plan(text, args.ref_mesh, load(args.ref_json), targets, zero, reach_zero,
                        [parse_set(s) for s in args.set])
    for c in changes:
        print("%-22s %-24s [%s, %s] -> [%s, %s]" % c)
    print("%d slider range(s) to change" % len(changes))
    if args.check:
        return 1 if changes else 0
    if not changes:
        return 0
    ET.fromstring(new.lstrip("\ufeff").encode("utf-8"))
    if not args.apply:
        print("dry run; pass --apply to write")
        return 0
    backup = "%s.bak-slider-reach-%s" % (args.skins, datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
    shutil.copy2(args.skins, backup)
    open(args.skins, "wb").write(new.encode("utf-8"))
    print("wrote %s (backup %s)" % (args.skins, backup))
    return 0


if __name__ == "__main__":
    sys.exit(main())
