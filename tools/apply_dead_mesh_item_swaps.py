#!/usr/bin/env python3
"""Re-point every item whose mesh the 2026-08-28 armoury cleanup deleted.

`tools/audit_deleted_mesh_impact.py` found 149 deleted meshes still named by item
XML. This applies the decided remedy for the ones with a live consumer. It does
NOT decide anything itself: the mapping below is the decision, written out so it
can be read and argued with.

FOUR GROUPS, THREE DIFFERENT MECHANISMS

1. Gondor lords -> their region's armour (25 refs, reference swap).
   Angbor is Lord of Lamedon, Forlong of Lossarnach, Golasgil of Anfalas,
   Hirluin of Pinnath Gelin, Imrahil Prince of Dol Amroth. Each takes the
   highest surviving tier its own region ships. Where a region ships no piece
   for a slot at all (Lamedon and Anfalas have no gloves, legs or cape; Pinnath
   Gelin and Lossarnach none for legs) the generic Gondor LORD-tier Serelond
   piece fills in, and Anorien noble elite covers the missing capes.

   This is a real tier drop and it is unavoidable: the bespoke lord pieces were
   85 body / 50 head / 35 gloves, above anything any region ships (70 / 41 / 27).
   The art for those numbers no longer exists.

2. Easterling -> Loke-Rim (12 refs, reference swap), tier-matched piece by piece
   so no troop or notable silently gains or loses armour. The two exceptions are
   stated in the table.

3. Erebor team colours -> the base item (5 refs, reference swap) and then all 57
   colour-variant DEFINITIONS are deleted. Blue, green and red no longer exist
   as art; every one of the 57 has a dead mesh, and each of the 5 that something
   still equips has an exact surviving base counterpart.

4. Career starter boots -> re-meshed, NOT swapped (3 items). Their armour is
   tuned to 5 / 7 / 9 for the career start and the lowest surviving Loke leg
   item is 15, so a reference swap would near-triple starting leg armour.
   Re-pointing the mesh keeps the tuning and fixes the invisible-boot.

WHAT THIS DOES NOT COVER, deliberately: `ar_ardunian_elite_armour` (Umbar) and
`lotr_troll_armor` / `_bracers` / `_helmet` (Mordor) are also dead-and-equipped
but were not part of the decision. They are reported at the end, not touched.

Preview by default. Pass the write flag to commit the edits. Every file outside
this repo is backed up first, to a NON-.xml extension: the engine globs `*.xml`
in these folders, so an `.xml` backup would inject duplicate item ids.

Usage:
  python tools/apply_dead_mesh_item_swaps.py                 # preview
  python tools/apply_dead_mesh_item_swaps.py --write
  python tools/apply_dead_mesh_item_swaps.py --write --skip-armory   # repo only
"""
from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _gamedir import ensure_exists, game_dir  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_ARMORY = Path(DEFAULT_GAME) / "Modules" / "LOTRLOME_Armory"
DEFAULT_CONSUMERS = REPO_ROOT / "Main" / "_Module" / "ModuleData"
# The asset repo versions a copy of the Armory ModuleData. Keeping the two in
# step is what stops a module reinstall silently reverting this (CLAUDE.md
# Traps: "a fix in a dependency module").
DEFAULT_ASSET_REPO = Path(r"E:\repos\lotraom-assets") / "v1.4" / "LOTRLOME_Armory"

# --------------------------------------------------------------------------- #
# The decision                                                                 #
# --------------------------------------------------------------------------- #
ITEM_SWAPS = {
    # -- Gondor lords -> their own region's top surviving tier ---------------
    # Angbor, Lord of Lamedon (on foot)
    "angbor_helmet":   "sk_gd_lam_nob_helmet_lord_a",
    "angbor_body":     "sk_gd_lam_inf_chest_lord_a",
    "angbor_shoulder": "sk_gd_ano_pauld_cape_noble_elite_a",   # Lamedon ships no cape
    "angbor_gloves":   "sk_gd_sere_bracer_lord_a",             # Lamedon ships no gloves
    "angbor_boots":    "sk_gd_sere_grvs_lord_a",               # Lamedon ships no legs
    # Forlong, Lord of Lossarnach (on foot)
    "forlong_helmet":   "sk_gd_los_noble_helmet_elite_a",
    "forlong_body":     "sk_gd_los_nob_chest_lord_a",
    "forlong_shoulder": "sk_gd_los_pauld_cape_nob_lord_a",
    "forlong_gloves":   "sk_gd_los_bracer_noble_elite_a",
    "forlong_boots":    "sk_gd_sere_grvs_lord_a",              # Lossarnach ships no legs
    # Golasgil, Lord of Anfalas (on foot). Anfalas ships only infantry gear, so
    # its own top tier is heavy rather than lord.
    "golasgil_helment":  "sk_gd_anf_inf_helmet_heavy_a",       # id typo is real, keep it
    "golasgil_body":     "sk_gd_anf_inf_chest_heavy_a",
    "golasgil_shoulder": "sk_gd_ano_pauld_cape_noble_elite_a",
    "golasgil_gloves":   "sk_gd_sere_bracer_lord_a",
    "golasgil_boots":    "sk_gd_sere_grvs_lord_a",
    # Hirluin the Fair, Lord of Pinnath Gelin (mounted -> cavalry helm)
    "hirluin_helmet":   "sk_gd_pin_noble_cav_helmet_elite_a",
    "hirluin_body":     "sk_gd_pin_nob_chest_elite_b",         # "Arndir Lord Armour"
    "hirluin_shoulder": "sk_gd_pin_pauld_cape_noble_elite_a",
    "hirluin_gloves":   "sk_gd_sere_bracer_lord_a",
    "hirluin_boots":    "sk_gd_sere_grvs_lord_a",
    # Imrahil, Prince of Dol Amroth (mounted). Dol Amroth is the only region
    # that ships every slot, so he stays fully regional.
    "imrahil_helmet":   "sk_gd_dol_cav_helmet_elite_a",
    "imrahil_body":     "sk_gd_dol_chest_elite_a",             # Swan Knight
    "imrahil_shoulder": "sk_gd_dol_pauld_cape_noble_elite_a",
    "imrahil_gloves":   "sk_gd_dol_bracer_elite_a",
    "imrahil_boot":     "sk_gd_dol_grvs_elite_a",              # singular id is real

    # -- Easterling -> Loke-Rim, tier-matched -------------------------------
    "easterling_torso":            "sk_rh_loke_scalemail_heavy_b",     # 88 -> 89
    "easterlingwarriors01_torso":  "sk_rh_loke_scalemail_light_b",     # 52 -> 51
    "easterlingwarriors04_torso":  "sk_rh_loke_chest_light_a",         # 41 -> 41
    "easterling_head":             "sk_rh_loke_helmet_inf_elite_i",    # 40 -> 40
    "easterlingwarriors04_helmet": "sk_rh_loke_helmet_inf_med_c",      # 25 -> 26
    "easterling_glove":            "sk_rh_loke_bracer_heavy_b",        # 25 -> 25
    "easterlingwarriors01_gloves": "sk_rh_loke_bracer_heavy_c",        # 25 -> 25
    "easterling_boots":            "sk_rh_loke_grvs_plate_light_a",    # 26 -> 26
    "easterlingwarriors01_boots":  "sk_rh_loke_grvs_light_b",          # 20 -> 20
    "easterling_cape":             "sk_rh_loke_pauldron_lam_heavy_a",  # 24 -> ~24
    # Loke-Rim's heaviest shoulder is 24, so this one drops from 30. Nothing
    # in the set reaches 30.
    "easterlingwarriors04_cape":   "sk_rh_loke_pauldron_scale_heavy_a",
    "easterling_shield":           "sm_rh_loke_shield_med_a",

    # -- Lossarnach civilian coat (villagers, notables, headman, broker) -----
    # 24 -> 20, the closest surviving Gondor civilian body already worn by the
    # rest of that file's cast.
    "lossarnach_coat": "gondor_noble_coat_a",

    # -- Erebor: the 5 colour variants something still equips ---------------
    # All five dress named_companion_yotthani, and each base survives.
    "sk_dwarf_erebor_bracers_elite_d_blue":             "sk_dwarf_erebor_bracers_elite_d",
    "sk_dwarf_erebor_chest_plate_elite_d_blue":         "sk_dwarf_erebor_chest_plate_elite_d",
    "sk_dwarf_erebor_greaves_elite_a_blue":             "sk_dwarf_erebor_greaves_elite_a",
    "sk_dwarf_erebor_helmet_plate_legionary_b2_blue":   "sk_dwarf_erebor_helmet_plate_legionary_b2",
    "sk_dwarf_erebor_pauldron_plate_cape_elite_a_blue": "sk_dwarf_erebor_pauldron_plate_cape_elite_a",
}

# Career starter boots: re-mesh, keep the tuned armour. See the header.
MESH_REPOINTS = {
    "starter_cavalry_khuzait_leg_a":  "sk_rh_loke_boots_a",
    "starter_infantry_khuzait_leg_a": "sk_rh_loke_boots_a",
    "starter_ranged_khuzait_leg_a":   "sk_rh_loke_boots_a",
}

# Every Erebor _blue / _green / _red item. All 57 have a dead mesh.
DELETE_ITEMS = {
    "sk_dwarf_erebor_bracers_elite_d_blue", "sk_dwarf_erebor_bracers_elite_d_green", "sk_dwarf_erebor_bracers_elite_d_red",
    "sk_dwarf_erebor_chest_plate_elite_a_blue", "sk_dwarf_erebor_chest_plate_elite_a_green", "sk_dwarf_erebor_chest_plate_elite_a_red",
    "sk_dwarf_erebor_chest_plate_elite_b_blue", "sk_dwarf_erebor_chest_plate_elite_b_green", "sk_dwarf_erebor_chest_plate_elite_b_red",
    "sk_dwarf_erebor_chest_plate_elite_c_blue", "sk_dwarf_erebor_chest_plate_elite_c_green", "sk_dwarf_erebor_chest_plate_elite_c_red",
    "sk_dwarf_erebor_chest_plate_elite_d_blue", "sk_dwarf_erebor_chest_plate_elite_d_green", "sk_dwarf_erebor_chest_plate_elite_d_red",
    "sk_dwarf_erebor_chest_plate_elite_e_blue", "sk_dwarf_erebor_chest_plate_elite_e_green", "sk_dwarf_erebor_chest_plate_elite_e_red",
    "sk_dwarf_erebor_chest_plate_elite_f_blue", "sk_dwarf_erebor_chest_plate_elite_f_green", "sk_dwarf_erebor_chest_plate_elite_f_red",
    "sk_dwarf_erebor_greaves_elite_a_blue", "sk_dwarf_erebor_greaves_elite_a_green", "sk_dwarf_erebor_greaves_elite_a_red",
    "sk_dwarf_erebor_greaves_elite_b_blue", "sk_dwarf_erebor_greaves_elite_b_green", "sk_dwarf_erebor_greaves_elite_b_red",
    "sk_dwarf_erebor_helmet_plate_legionary_a2_blue", "sk_dwarf_erebor_helmet_plate_legionary_a2_green", "sk_dwarf_erebor_helmet_plate_legionary_a2_red",
    "sk_dwarf_erebor_helmet_plate_legionary_b2_blue", "sk_dwarf_erebor_helmet_plate_legionary_b2_green", "sk_dwarf_erebor_helmet_plate_legionary_b2_red",
    "sk_dwarf_erebor_helmet_plate_legionary_c2_blue", "sk_dwarf_erebor_helmet_plate_legionary_c2_green", "sk_dwarf_erebor_helmet_plate_legionary_c2_red",
    "sk_dwarf_erebor_helmet_plate_legionary_d2_blue", "sk_dwarf_erebor_helmet_plate_legionary_d2_green", "sk_dwarf_erebor_helmet_plate_legionary_d2_red",
    "sk_dwarf_erebor_helmet_plate_lord_a2_blue", "sk_dwarf_erebor_helmet_plate_lord_a2_green", "sk_dwarf_erebor_helmet_plate_lord_a2_red",
    "sk_dwarf_erebor_helmet_plate_lord_b2_blue", "sk_dwarf_erebor_helmet_plate_lord_b2_green", "sk_dwarf_erebor_helmet_plate_lord_b2_red",
    "sk_dwarf_erebor_helmet_plate_lord_c2_blue", "sk_dwarf_erebor_helmet_plate_lord_c2_green", "sk_dwarf_erebor_helmet_plate_lord_c2_red",
    "sk_dwarf_erebor_helmet_plate_lord_d2_blue", "sk_dwarf_erebor_helmet_plate_lord_d2_green", "sk_dwarf_erebor_helmet_plate_lord_d2_red",
    "sk_dwarf_erebor_pauldron_plate_cape_elite_a_blue", "sk_dwarf_erebor_pauldron_plate_cape_elite_a_green", "sk_dwarf_erebor_pauldron_plate_cape_elite_a_red",
    "sk_dwarf_erebor_pauldron_plate_cape_elite_b_blue", "sk_dwarf_erebor_pauldron_plate_cape_elite_b_green", "sk_dwarf_erebor_pauldron_plate_cape_elite_b_red",
}

# Dead-and-equipped, but outside this decision. Reported, never touched.
NOT_COVERED = {
    "ar_ardunian_elite_armour": "Umbar. No replacement chosen.",
    "lotr_troll_armor": "Mordor troll. No replacement chosen.",
    "lotr_troll_bracers": "Mordor troll. No replacement chosen.",
    "lotr_troll_helmet": "Mordor troll. No replacement chosen.",
}

_COMMENT_RE = re.compile(r"<!--.*?-->", re.S)


# --------------------------------------------------------------------------- #
# Byte-faithful I/O (.claude/rules/moduledata-validation.md idiom A)           #
# --------------------------------------------------------------------------- #
def read_xml(path: Path) -> tuple:
    """(text, had_bom). Never a plain utf-8 text read: the mixed shape strips a
    BOM and normalises CRLF, turning a two-attribute edit into a whole-file
    rewrite."""
    raw = Path(path).read_bytes()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    return raw.decode("utf-8-sig" if had_bom else "utf-8", errors="strict"), had_bom


def write_xml(path: Path, text: str, had_bom: bool) -> None:
    prefix = b"\xef\xbb\xbf" if had_bom else b""
    Path(path).write_bytes(prefix + text.encode("utf-8"))


_PLACEHOLDER_RE = re.compile("(\\d+)")


def _protect_comments(text: str) -> tuple:
    """Swap each comment for an indexed placeholder so no transform can touch it.

    Restoration is by TOKEN, never by offset. Masking in place and restoring at
    the recorded offsets is what corrupted 8 files on 2026-08-28: a swap changes
    the text length, so every comment after the first edit was written back in
    the wrong position. The private-use sentinels cannot appear in the XML and
    match none of the patterns below.
    """
    bodies = []

    def take(m):
        bodies.append(m.group(0))
        return "%d" % (len(bodies) - 1)

    return _COMMENT_RE.sub(take, text), bodies


def _restore_comments(text: str, bodies) -> str:
    if not bodies:
        return text
    return _PLACEHOLDER_RE.sub(lambda m: bodies[int(m.group(1))], text)


# --------------------------------------------------------------------------- #
# Transforms                                                                   #
# --------------------------------------------------------------------------- #
def swap_item_refs(text: str, mapping: dict) -> tuple:
    """Rewrite `Item.<old>` references. Exact id match, comments left alone.

    Only the prefixed form is touched, so an `<Item id="x">` DEFINITION of the
    same id is never rewritten.
    """
    if not mapping:
        return text, 0
    masked, spans = _protect_comments(text)
    pattern = re.compile(
        r'("Item\.)(' + "|".join(re.escape(k) for k in sorted(mapping, key=len, reverse=True))
        + r')(")')
    count = 0

    def sub(m):
        nonlocal count
        count += 1
        return m.group(1) + mapping[m.group(2)] + m.group(3)

    masked = pattern.sub(sub, masked)
    return _restore_comments(masked, spans), count


def repoint_mesh(text: str, mapping: dict) -> tuple:
    """Point a named item's `mesh=` at a different mesh, leaving all else alone."""
    if not mapping:
        return text, 0
    masked, spans = _protect_comments(text)
    count = 0
    for item_id, new_mesh in mapping.items():
        for m in list(re.finditer(r"<Item\b[^>]*?>", masked, re.S)):
            block = m.group(0)
            found = re.search(r'\bid="([^"]+)"', block)
            if not found or found.group(1) != item_id:
                continue
            new_block, n = re.subn(r'(\bmesh=")[^"]*(")',
                                   lambda mm: mm.group(1) + new_mesh + mm.group(2), block)
            if n and new_block != block:
                masked = masked[:m.start()] + new_block + masked[m.end():]
                count += 1
            break
    return _restore_comments(masked, spans), count


def remove_item_defs(text: str, ids) -> tuple:
    """Delete whole `<Item id="...">` definitions, self-closing or with children.

    References are NOT removed. Anything still referencing a deleted id is a bug
    the caller should see, not something to silently swallow.
    """
    if not ids:
        return text, []
    masked, spans = _protect_comments(text)
    removed, cuts = [], []
    for m in re.finditer(r"<Item\b[^>]*?(/>|>)", masked, re.S):
        block = m.group(0)
        found = re.search(r'\bid="([^"]+)"', block)
        if not found or found.group(1) not in ids:
            continue
        if block.endswith("/>"):
            end = m.end()
        else:
            close = masked.find("</Item>", m.end())
            if close == -1:
                continue
            end = close + len("</Item>")
        start = m.start()
        # take the indentation and the trailing newline with it
        line_start = masked.rfind("\n", 0, start) + 1
        if masked[line_start:start].strip() == "":
            start = line_start
        while end < len(masked) and masked[end] in "\r\n":
            end += 1
        cuts.append((start, end))
        removed.append(found.group(1))
    # Cut back-to-front so earlier offsets stay valid. Comment restoration is by
    # token, so a deleted region simply takes its own comments with it and no
    # offset bookkeeping is needed.
    for start, end in reversed(cuts):
        masked = masked[:start] + masked[end:]
    return _restore_comments(masked, spans), removed


# --------------------------------------------------------------------------- #
# Pre-flight                                                                   #
# --------------------------------------------------------------------------- #
def armory_item_ids(armory: Path) -> set:
    ids = set()
    root = armory / "ModuleData"
    for f in root.rglob("*.xml"):
        if "Languages" in f.parts:
            continue
        try:
            text = _COMMENT_RE.sub("", f.read_text(encoding="utf-8", errors="ignore"))
        except OSError:
            continue
        ids.update(re.findall(r'<(?:Item|CraftedItem)\b[^>]*?\bid="([^"]+)"', text, re.S))
    return ids


def preflight(armory: Path) -> list:
    """Every replacement must be a real, currently-defined item. Fail closed:
    swapping onto a non-existent id trades an invisible item for a naked troop."""
    defined = armory_item_ids(armory)
    problems = []
    for old, new in sorted(ITEM_SWAPS.items()):
        if new not in defined:
            problems.append(f"replacement not defined in the Armory: {old} -> {new}")
        if old not in defined:
            problems.append(f"source item not defined (already handled?): {old}")
    for item_id in sorted(MESH_REPOINTS):
        if item_id not in defined:
            problems.append(f"re-mesh target not defined: {item_id}")
    return problems


# --------------------------------------------------------------------------- #
# Driver                                                                       #
# --------------------------------------------------------------------------- #
def process(path: Path, swaps: dict, meshes: dict, deletes: set, write: bool,
            backup_tag: str = "") -> dict:
    try:
        text, had_bom = read_xml(path)
    except (OSError, UnicodeDecodeError) as exc:
        return {"path": path, "error": str(exc)}
    original = text
    text, swapped = swap_item_refs(text, swaps)
    text, remeshed = repoint_mesh(text, meshes)
    text, removed = remove_item_defs(text, deletes)
    result = {"path": path, "swapped": swapped, "remeshed": remeshed,
              "removed": removed, "changed": text != original}
    # Never write a document the transform broke. On 2026-08-28 an offset-based
    # comment restore spliced comments into the middle of ids and 8 files went
    # out malformed; the only reason it was caught was a hand-check afterwards.
    # Parse first, refuse on failure, and let the caller report it.
    if result["changed"]:
        try:
            ET.fromstring(text.encode("utf-8"))
        except ET.ParseError as exc:
            result["error"] = f"transform produced malformed XML: {exc}"
            result["changed"] = False
            return result
    if write and result["changed"]:
        if backup_tag:
            backup = path.with_suffix(path.suffix + f".bak-{backup_tag}")
            if not backup.exists():
                backup.write_bytes(path.read_bytes())
                result["backup"] = backup.name
        write_xml(path, text, had_bom)
    return result


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--armory", default=str(DEFAULT_ARMORY))
    ap.add_argument("--consumers", default=str(DEFAULT_CONSUMERS))
    ap.add_argument("--asset-repo", default=str(DEFAULT_ASSET_REPO),
                    help="Versioned copy of the Armory ModuleData to keep in step")
    ap.add_argument("--write", action="store_true",
                    help="Commit the edits (default is a preview)")
    ap.add_argument("--skip-armory", action="store_true",
                    help="Touch only this repo; leave the Armory alone")
    args = ap.parse_args()

    armory = ensure_exists(args.armory, "the LOTRLOME_Armory module")
    consumers = ensure_exists(args.consumers, "the consumer ModuleData root")

    problems = preflight(armory)
    if problems:
        print("PRE-FLIGHT FAILED:", file=sys.stderr)
        for p in problems:
            print(f"  {p}", file=sys.stderr)
        return 2
    print(f"Pre-flight OK: {len(ITEM_SWAPS)} replacements all defined in the Armory.")

    tag = "deadmesh-" + datetime.now().strftime("%Y%m%d%H%M%S")
    mode = "WRITING" if args.write else "PREVIEW (pass --write to commit)"
    print(f"{mode}\n")

    total = {"swapped": 0, "remeshed": 0, "removed": 0, "files": 0, "errors": 0}

    # 1. Consumers in this repo: reference swaps only.
    print("--- repo consumers ---")
    for f in sorted(list(consumers.rglob("*.xml")) + list(consumers.rglob("*.xslt"))):
        if "Languages" in f.parts:
            continue
        r = process(f, ITEM_SWAPS, {}, set(), args.write)
        if r.get("error"):
            print(f"  ERROR  {f.relative_to(consumers).as_posix()}: {r['error']}")
            total["errors"] += 1
        if r.get("changed"):
            total["files"] += 1
            total["swapped"] += r["swapped"]
            print(f"  {r['swapped']:4d} swaps  {f.relative_to(consumers).as_posix()}")

    # 2. The Armory: re-mesh the starter boots, delete the colour variants.
    if not args.skip_armory:
        roots = [("live", armory / "ModuleData")]
        asset_repo = Path(args.asset_repo)
        if asset_repo.exists():
            roots.append(("versioned", asset_repo / "ModuleData"))
        else:
            print(f"\nNOTE: asset repo not found at {asset_repo}; the live edit "
                  f"will be unversioned.")
        for label, root in roots:
            print(f"\n--- Armory ({label}): {root} ---")
            for f in sorted(root.rglob("*.xml")):
                if "Languages" in f.parts:
                    continue
                r = process(f, {}, MESH_REPOINTS, DELETE_ITEMS, args.write, tag)
                if r.get("error"):
                    print(f"  ERROR  {f.relative_to(root).as_posix()}: {r['error']}")
                    total["errors"] += 1
                if r.get("changed"):
                    total["files"] += 1
                    total["remeshed"] += r["remeshed"]
                    total["removed"] += len(r["removed"])
                    print(f"  re-mesh {r['remeshed']}, removed {len(r['removed'])}"
                          f"  {f.relative_to(root).as_posix()}"
                          + (f"  [backup {r['backup']}]" if r.get("backup") else ""))

    print(f"\n{total['files']} file(s): {total['swapped']} reference swaps, "
          f"{total['remeshed']} re-meshes, {total['removed']} item definitions removed")
    print("\nNot covered by this pass (dead mesh, still equipped):")
    for item_id, why in sorted(NOT_COVERED.items()):
        print(f"  {item_id:28s} {why}")
    if not args.write:
        print("\nNothing was written.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
