#!/usr/bin/env python3
"""
Author / remove bandit hideouts in TAOM_Map/settlements.xml + the 12 loc files.

Two add modes:
  CLONE_BATCHES   - new ids in an EXISTING range; structure (scene/icon/pos) cloned
                    from existing same-prefix hideouts. Culture must already have a clan.
  SCRATCH_BATCHES - brand-new prefix (gondor/erebor/mirkwood). scene_name = the settlement
                    id (the map author created matching scenes in the editor); positions
                    cloned from a reference prefix as valid-on-land placeholders that the
                    author repositions in the map editor afterward.
Plus REMOVE_IDS - settlement block + loc strings deleted for each id.

Idempotent: existing ids are skipped on add; missing ids are skipped on remove.

Usage:
    python add_bandit_hideouts.py --dry-run
    python add_bandit_hideouts.py --apply [--backup]
"""

from __future__ import annotations
import argparse
import os
import re
import sys
from pathlib import Path

# (prefix, start, end, culture, name, map_icon, wait_mesh) — clone from existing same-prefix
CLONE_BATCHES = [
    ("desert", 30, 43, "harad_raiders", "Haradrim Raider's Camp", "bandit_hideout_a", "wait_hideout_desert"),
    ("seaside", 30, 45, "umbar_corsairs", "Corsair's Cove", "bandit_hideout_c", "wait_hideout_seaside"),
]

# (prefix, start, end, culture, name, map_icon, wait_mesh, pos_source_prefix)
# scene_name is set to the settlement id (matching the scenes authored in the editor).
SCRATCH_BATCHES = [
    ("gondor", 1, 10, "gondor_soldiers", "Gondor Soldiers' Camp", "bandit_hideout_b", "wait_hideout_forest", "desert"),
    ("erebor", 1, 10, "erebor_warriors", "Erebor Warriors' Camp", "bandit_hideout_a", "wait_hideout_forest", "desert"),
    ("mirkwood", 1, 10, "mirkwood_stalkers", "Mirkwood Stalkers' Camp", "bandit_hideout_b", "wait_hideout_forest", "desert"),
]

REMOVE_IDS = ["hideout_seaside_46"]

BACKGROUND_MESH = "empire_twn_scene_bg"
COMPLEX_TEMPLATE = "LocationComplexTemplate.hideout_complex"
LOC_LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]


def parse_existing(content: str, prefix: str):
    positions, scenes = [], []
    block_re = re.compile(
        r'<Settlement id="hideout_' + prefix + r'_\d+"[^>]*posX="([0-9.]+)" posY="([0-9.]+)"[^>]*>'
        r'.*?scene_name="([^"]+)"',
        re.DOTALL,
    )
    for m in block_re.finditer(content):
        positions.append((float(m.group(1)), float(m.group(2))))
        scenes.append(m.group(3))
    return positions, scenes


def build_settlement(prefix, n, culture, name, map_icon, wait_mesh, posx, posy, scene) -> str:
    sid = f"hideout_{prefix}_{n}"
    key = f"Settlements.Settlement.name.{sid}"
    return (
        f'  <Settlement id="{sid}" name="{{={key}}}{name}" type="Hideout" '
        f'posX="{posx:.3f}" posY="{posy:.3f}" culture="Culture.{culture}">\n'
        f'    <Components>\n'
        f'      <Hideout id="{sid}" map_icon="{map_icon}" background_crop_position="0.0" '
        f'background_mesh="{BACKGROUND_MESH}" wait_mesh="{wait_mesh}" gate_rotation="0.0" />\n'
        f'    </Components>\n'
        f'    <Locations complex_template="{COMPLEX_TEMPLATE}">\n'
        f'      <Location id="hideout_center" scene_name="{scene}" />\n'
        f'    </Locations>\n'
        f'  </Settlement>\n'
    )


def find_tm_dir(arg_path):
    if arg_path:
        return Path(arg_path)
    game = os.environ.get("BANNERLORD_GAME_DIR", r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
    return Path(game) / "Modules" / "TAOM_Map"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--backup", action="store_true")
    ap.add_argument("--tm-path", default=None)
    args = ap.parse_args()
    if not (args.dry_run or args.apply):
        ap.error("pass --dry-run or --apply")

    tm = find_tm_dir(args.tm_path)
    master = tm / "ModuleData" / "settlements.xml"
    if not master.exists():
        sys.exit(f"ERROR: {master} not found")

    content = master.read_text(encoding="utf-8-sig")
    had_bom = master.read_bytes().startswith(b"\xef\xbb\xbf")

    new_blocks, loc_add, skipped_add = [], [], []
    ids_present = set(re.findall(r'<Settlement id="(hideout_[a-z]+_\d+)"', content))

    # CLONE batches
    for prefix, start, end, culture, name, icon, wait in CLONE_BATCHES:
        positions, scenes = parse_existing(content, prefix)
        if not positions:
            sys.exit(f"ERROR: no existing hideout_{prefix}_* to clone from")
        for i, n in enumerate(range(start, end + 1)):
            sid = f"hideout_{prefix}_{n}"
            if sid in ids_present:
                skipped_add.append(sid); continue
            sx, sy = positions[i % len(positions)]
            off = (i // len(positions) + 1) * 4.0
            new_blocks.append(build_settlement(prefix, n, culture, name, icon, wait, sx + off, sy + off, scenes[i % len(scenes)]))
            loc_add.append((f"Settlements.Settlement.name.{sid}", name))

    # SCRATCH batches (new prefix, scene_name = id)
    for prefix, start, end, culture, name, icon, wait, src in SCRATCH_BATCHES:
        positions, _ = parse_existing(content, src)
        if not positions:
            sys.exit(f"ERROR: no existing hideout_{src}_* to source placeholder positions")
        for i, n in enumerate(range(start, end + 1)):
            sid = f"hideout_{prefix}_{n}"
            if sid in ids_present:
                skipped_add.append(sid); continue
            sx, sy = positions[i % len(positions)]
            off = (i // len(positions) + 1) * 6.0
            new_blocks.append(build_settlement(prefix, n, culture, name, icon, wait, sx + off, sy + off, sid))
            loc_add.append((f"Settlements.Settlement.name.{sid}", name))

    # REMOVALS
    removed, remove_keys = [], []
    new_content = content
    for sid in REMOVE_IDS:
        if sid not in ids_present:
            continue
        blk = re.compile(r'[ \t]*<Settlement id="' + re.escape(sid) + r'"[^>]*>.*?</Settlement>\r?\n?', re.DOTALL)
        new_content, nsub = blk.subn("", new_content)
        if nsub:
            removed.append(sid)
            remove_keys.append(f"Settlements.Settlement.name.{sid}")

    print(f"ADD: {len(new_blocks)} (skipped existing: {len(skipped_add)})  REMOVE: {len(removed)}")
    for b in CLONE_BATCHES: print(f"  clone  {b[0]}_{b[1]}..{b[2]} -> Culture.{b[3]}")
    for b in SCRATCH_BATCHES: print(f"  scratch {b[0]}_{b[1]}..{b[2]} -> Culture.{b[3]} (scene_name=id)")
    if removed: print(f"  removed: {', '.join(removed)}")

    if args.dry_run:
        if new_blocks:
            print("\n[DRY RUN] sample scratch entry:\n" + (new_blocks[-1] if SCRATCH_BATCHES else new_blocks[0]))
        print(f"[DRY RUN] +{len(loc_add)} loc strings, -{len(remove_keys)} loc strings, across {len(LOC_LANGS)} langs.")
        return 0

    if args.backup:
        master.with_suffix(".xml.bak3").write_bytes(master.read_bytes())
    new_content = new_content.replace("</Settlements>", "".join(new_blocks) + "</Settlements>", 1)
    master.write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + new_content.encode("utf-8"))
    print(f"[APPLY] settlements.xml: +{len(new_blocks)} -{len(removed)}")

    line = lambda k, t: f'    <string id="{k}" text="{t}" />\n'
    for lang in LOC_LANGS:
        lf = tm / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
        if not lf.exists():
            continue
        lc = lf.read_text(encoding="utf-8-sig")
        lbom = lf.read_bytes().startswith(b"\xef\xbb\xbf")
        # removals
        for k in remove_keys:
            lc = re.sub(r'[ \t]*<string id="' + re.escape(k) + r'"[^>]*/>\r?\n?', "", lc)
        # adds (skip already-present)
        present = set(re.findall(r'<string id="([^"]+)"', lc))
        block = "".join(line(k, t) for k, t in loc_add if k not in present)
        if block:
            lc = lc.replace("</base>", block + "</base>", 1)
        if args.backup:
            lf.with_suffix(".xml.bak3").write_bytes(lf.read_bytes())
        lf.write_bytes((b"\xef\xbb\xbf" if lbom else b"") + lc.encode("utf-8"))
    print(f"[APPLY] loc: +{len(loc_add)} -{len(remove_keys)} across {len(LOC_LANGS)} langs")
    return 0


if __name__ == "__main__":
    sys.exit(main())
