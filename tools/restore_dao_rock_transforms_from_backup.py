#!/usr/bin/env python3
r"""Restore dao_rock entity transforms (position + rotation + scale) in TAOM_Map's
current ``Main_map/scene.xscene`` from an older backup copy.

Why this exists
---------------
Over a month of manual map editing, a handful of dao_rock placements were nudged /
rotated away from where the map maker had them in an earlier backup. This tool puts the
matched rocks back to their old full transform, using the old backup as the source of
truth. (It is unrelated to ``propagate_dao_rock_lod_factors.py`` -- that one only touches
``factor`` tint rows; this one only touches ``<transform>`` lines.)

How it matches (the safe part)
------------------------------
Entity names repeat (many ``dao_rock_4`` etc.) and ~46 rocks were added since the backup,
so we can't diff by line order or by name alone. Instead:
  * Build (mesh-name, position, rotation, scale, transform-line) for every dao_rock in
    BOTH files.
  * Pair old<->current by **mutual nearest-neighbor of the same mesh type** within a small
    radius (default 10 units). Mutual = each is the other's nearest, which rejects
    add/delete ambiguity.
  * For each matched pair whose rounded transform differs, restore: replace the CURRENT
    ``<transform>`` line with the OLD one (position+rotation+scale verbatim).
  * The replacement is targeted by the **current position string**, which is unique across
    all entities -> the right rock is hit, unambiguously. If a position key is not unique,
    that rock is skipped with a warning (never guess).
  * Old rocks with no clean current match (deleted / replaced by another type) are listed
    as "review manually" and NOT auto re-added.

Idempotent, formatting-preserving (keeps the current line's indentation + EOL).
``--dry-run`` (default) shows every old->current change. ``--apply`` writes, creating a
one-time ``scene.xscene.bak`` first.

IMPORTANT: close the Bannerlord editor before ``--apply`` -- it rewrites scene.xscene on
its own save and would clobber an external edit.
"""
from __future__ import annotations

import argparse
import math
import re
import shutil
import sys
from pathlib import Path

OLD_DEFAULT = (
    r"E:\LOTRAOMAssets\TAOM_Map_170526\TAOM_Map\SceneObj\Main_map\scene.xscene"
)
NEW_DEFAULT = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\TAOM_Map\SceneObj\Main_map\scene.xscene"
)

ENTITY_RE = re.compile(r'<game_entity name="(dao_rock_[^"]*)"')
POS_RE = re.compile(r'position="([^"]+)"')
ROT_RE = re.compile(r'rotation_euler="([^"]+)"')
SCALE_RE = re.compile(r'scale="([^"]+)"')
EOL_RE = re.compile(r"(\r\n|\r|\n)$")
MATCH_RADIUS = 10.0  # units; mutual-NN pairing must be closer than this


def base_type(name):
    # entity name -> mesh type key (the dao_rock_<n> stem; ignores _mordor_test etc.)
    m = re.match(r"(dao_rock_\d+)", name)
    return m.group(1) if m else name


def read_lines(path: Path):
    return path.read_text(encoding="utf-8", newline="").splitlines(keepends=True)


def parse(lines):
    """Return list of dicts: {type, pos, rot, scale, line_idx, stripped}."""
    ents = []
    for i, ln in enumerate(lines):
        m = ENTITY_RE.search(ln)
        if not m:
            continue
        for j in range(i + 1, min(i + 5, len(lines))):
            if "<transform" in lines[j]:
                pm = POS_RE.search(lines[j])
                if not pm:
                    break
                rm = ROT_RE.search(lines[j])
                sm = SCALE_RE.search(lines[j])
                ents.append({
                    "type": base_type(m.group(1)),
                    "name": m.group(1),
                    "pos": pm.group(1).strip(),
                    "rot": (rm.group(1).strip() if rm else "0.000, 0.000, 0.000"),
                    "scale": (sm.group(1).strip() if sm else "1.000, 1.000, 1.000"),
                    "line_idx": j,
                    "stripped": lines[j].strip(),
                })
                break
    return ents


def vec(s):
    return tuple(round(float(x), 3) for x in s.split(","))


def nearest(src, pool):
    """index in pool of the same-type entity nearest src, with distance."""
    sp = vec(src["pos"])
    best = None
    for k, e in enumerate(pool):
        if e["type"] != src["type"]:
            continue
        d = math.dist(sp, vec(e["pos"]))
        if best is None or d < best[1]:
            best = (k, d)
    return best


def transform_differs(a, b):
    return (
        math.dist(vec(a["pos"]), vec(b["pos"])) >= 0.01
        or vec(a["rot"]) != vec(b["rot"])
        or vec(a["scale"]) != vec(b["scale"])
    )


def main(argv=None):
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    ap.add_argument("--old", default=OLD_DEFAULT, help="older/backup scene.xscene (source of truth)")
    ap.add_argument("--scene", default=NEW_DEFAULT, help="current scene.xscene (to be edited)")
    ap.add_argument("--apply", action="store_true", help="write changes (default dry-run)")
    args = ap.parse_args(argv)

    old_path, new_path = Path(args.old), Path(args.scene)
    for p in (old_path, new_path):
        if not p.is_file():
            print(f"ERROR: not found: {p}", file=sys.stderr)
            return 2

    old_lines = read_lines(old_path)
    new_lines = read_lines(new_path)
    old_ents = parse(old_lines)
    new_ents = parse(new_lines)

    # mutual nearest-neighbor old <-> new (same type, within radius)
    old_to_new = {i: nearest(e, new_ents) for i, e in enumerate(old_ents)}
    new_to_old = {i: nearest(e, old_ents) for i, e in enumerate(new_ents)}

    restores = []   # (old_ent, new_ent)
    unmatched = []  # old_ent with no clean mutual match
    for i, oe in enumerate(old_ents):
        b = old_to_new.get(i)
        if b and b[1] < MATCH_RADIUS:
            j = b[0]
            back = new_to_old.get(j)
            if back and back[0] == i and back[1] < MATCH_RADIUS:
                if transform_differs(oe, new_ents[j]):
                    restores.append((oe, new_ents[j]))
                continue
        unmatched.append(oe)

    # build replacement map keyed by the unique current position string
    pos_counts = {}
    for e in new_ents:
        pos_counts[e["pos"]] = pos_counts.get(e["pos"], 0) + 1
    repl = {}
    ambiguous = []
    for oe, ne in restores:
        if pos_counts.get(ne["pos"], 0) != 1:
            ambiguous.append((oe, ne))
            continue
        repl[ne["pos"]] = oe["stripped"]

    print("=" * 72)
    print("Restore dao_rock transforms " + ("(APPLIED)" if args.apply else "(DRY-RUN)"))
    print("=" * 72)
    print(f"  old rocks={len(old_ents)}  current rocks={len(new_ents)}")
    print(f"  matched-and-changed (to restore): {len(repl)}")
    for oe, ne in restores:
        if ne["pos"] not in repl:
            continue
        kinds = []
        dp = math.dist(vec(oe["pos"]), vec(ne["pos"]))
        if dp >= 0.01:
            kinds.append(f"move {dp:.2f}")
        if vec(oe["rot"]) != vec(ne["rot"]):
            kinds.append("rot")
        if vec(oe["scale"]) != vec(ne["scale"]):
            kinds.append("scale")
        print(f"    {oe['type']:<13} [{', '.join(kinds)}]")
        print(f"        current: {ne['stripped']}")
        print(f"        ->old  : {oe['stripped']}")
    if ambiguous:
        print(f"  SKIPPED (current position not unique, won't guess): {len(ambiguous)}")
        for oe, ne in ambiguous:
            print(f"    {oe['type']} @ current {ne['pos']}")
    if unmatched:
        print(f"  UNMATCHED old rocks (deleted/replaced -- review manually, NOT restored): {len(unmatched)}")
        for oe in unmatched:
            print(f"    {oe['name']:<22} old pos {oe['pos']}")

    if not repl:
        print("\nNothing to restore.")
        return 0

    # apply replacements (keyed by unique current position)
    out = []
    done = 0
    for ln in new_lines:
        if "<transform" in ln:
            pm = POS_RE.search(ln)
            if pm and pm.group(1).strip() in repl:
                indent = re.match(r"[ \t]*", ln).group(0)
                eolm = EOL_RE.search(ln)
                eol = eolm.group(1) if eolm else "\n"
                out.append(f"{indent}{repl[pm.group(1).strip()]}{eol}")
                done += 1
                continue
        out.append(ln)

    print(f"\n  lines replaced: {done} (expected {len(repl)})")
    if not args.apply:
        print("\nDry-run only. Re-run with --apply (editor CLOSED) to write.")
        return 0
    if done != len(repl):
        print("ERROR: replacement count mismatch -- aborting write.", file=sys.stderr)
        return 3

    bak = new_path.with_suffix(new_path.suffix + ".bak")
    if not bak.exists():
        shutil.copy2(new_path, bak)
        print(f"  backed up current -> {bak}")
    else:
        print(f"  backup already exists, leaving intact -> {bak}")
    new_path.write_text("".join(out), encoding="utf-8", newline="")
    print(f"  wrote {new_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
