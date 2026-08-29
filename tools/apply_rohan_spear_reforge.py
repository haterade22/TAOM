#!/usr/bin/env python3
"""Replace Rohan's six spear blades + handles with the artist's two-blade, two-handle set.

The artist delivered four meshes (two blades, two handles) plus two collision bodies, to replace
twelve pieces. Five crafted spears collapse to two, and the eight troop rosters carrying the
retired three are remapped by damage family so Rohan keeps its heavy/light spear split.

MESH NAMES ARE LOWERCASE. The FBX is `SM_Ro_Rohan_Spear_A.fbx` and the tpac stores
`sm_ro_rohan_spear_blade_a`. Authoring the CamelCase form would silently fail to resolve, so the
ids below are the strings actually read out of
`Assets/rohan_weapons/spears/SM_Ro_Rohan_Spear_A_geo.tpac` (workflow Step C: never trust the
editor label).

COUCHABLE, AND STILL USABLE WITH A SHIELD. Vanilla's `TwoHandedPolearm` crafting template lists
its descriptions in this order:

    1. OneHandedPolearm            onehanded_polearm:block:long:rshield:thrust
    2. TwoHandedPolearm            polearm:block:long:shield:swing:thrust
    3. TwoHandedPolearm_Couchable  polearm:couch
    4. TwoHandedPolearm_Bracing    polearm:bracing

The engine takes the FIRST description whose AvailablePieces cover every piece as the item's
primary usage, and the later matches become additional usages. Registering the new pieces in all
four therefore yields a shield-compatible primary AND a couch. That is not cosmetic: eight Rohan
rosters pair these spears with a shield, and a polearm absent from OneHandedPolearm resolves
`requires_no_shield`, so the troop carries it and never draws it (CLAUDE.md Traps; gate:
tools/audit_polearm_shield_parity.py).

REACH DROPS. The old spears were 127.41 + 217.91 = 345cm and 96.28 + 246.76 = 343cm. The new ones
are 60 + 220 = 280cm and 50 + 220 = 270cm, so Rohan's spear wall loses about 65cm of reach. That
is inherent to the delivered art, not a choice made here.

Preview by default; pass --write to commit. Backs up every file (all four live outside this repo)
to a non-.xml extension, and parses every transformed document before writing it.

Usage:
  python tools/apply_rohan_spear_reforge.py
  python tools/apply_rohan_spear_reforge.py --write
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
from apply_dead_mesh_item_swaps import (  # noqa: E402
    _protect_comments, _restore_comments, read_xml, swap_item_refs, write_xml,
)

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_ARMORY = Path(DEFAULT_GAME) / "Modules" / "LOTRLOME_Armory"
DEFAULT_ASSET_REPO = Path(r"E:\repos\lotraom-assets") / "v1.4" / "LOTRLOME_Armory"
DEFAULT_TROOPS = REPO_ROOT / "Main" / "_Module" / "ModuleData"

# --------------------------------------------------------------------------- #
# The decision                                                                 #
# --------------------------------------------------------------------------- #
NEW_PIECE_IDS = [
    "sm_ro_rohan_spear_blade_a",
    "sm_ro_rohan_spear_blade_b",
    "sm_ro_rohan_spear_handle_a",
    "sm_ro_rohan_spear_handle_b",
]

# All twelve the artist listed. `wm_rohan_spear_e_handle` is never defined (variant `e` is a
# blade with no handle and no crafted item), so the run reports 11 removed of 12 asked for
# rather than silently accepting the mismatch.
OLD_PIECE_IDS = {f"wm_rohan_spear_{v}_{role}"
                 for v in "abcdef" for role in ("blade", "handle")}

# Tier / weight / damage / flags / materials mirror the pieces being retired, so troop damage
# does not move: blade A inherits the 3.1-thrust heavy family (old a/c/d), blade B the
# 1.87-thrust light family (old b/e/f).
NEW_PIECE_BLOCKS = [
    """    <CraftingPiece
        id="sm_ro_rohan_spear_blade_a"
        name="{=aom_sm_ro_rohan_spear_blade_a_name}Rohan Spear Head A"
        tier="3"
        piece_type="Blade"
        mesh="sm_ro_rohan_spear_blade_a"
        length="60"
        weight="0.9"
        excluded_item_usage_features="swing">
        <BladeData
            stack_amount="3"
            physics_material="wood_weapon"
            body_name="bo_sm_ro_rohan_spear_blade_a">
            <Thrust
                damage_type="Pierce"
                damage_factor="3.1" />
        </BladeData>
        <BuildData
            piece_offset="0" />
        <Flags>
            <Flag name="CanKnockDown" />
            <Flag name="CanDismount" />
            <Flag name="CanHook" />
            <Flag name="NotStackable" type="ItemFlags" />
        </Flags>
        <Materials>
            <Material id="Iron5" count="5" />
        </Materials>
    </CraftingPiece>
""",
    """    <CraftingPiece
        id="sm_ro_rohan_spear_blade_b"
        name="{=aom_sm_ro_rohan_spear_blade_b_name}Rohan Spear Head B"
        tier="3"
        piece_type="Blade"
        mesh="sm_ro_rohan_spear_blade_b"
        length="50"
        weight="0.8"
        excluded_item_usage_features="swing">
        <BladeData
            stack_amount="3"
            physics_material="wood_weapon"
            body_name="bo_sm_ro_rohan_spear_blade_b">
            <Thrust
                damage_type="Pierce"
                damage_factor="1.87" />
        </BladeData>
        <BuildData
            piece_offset="0" />
        <Flags>
            <Flag name="CanKnockDown" />
            <Flag name="CanDismount" />
            <Flag name="CanHook" />
            <Flag name="NotStackable" type="ItemFlags" />
        </Flags>
        <Materials>
            <Material id="Iron5" count="5" />
        </Materials>
    </CraftingPiece>
""",
    """    <CraftingPiece
        id="sm_ro_rohan_spear_handle_a"
        name="{=aom_sm_ro_rohan_spear_handle_a_name}Rohan Spear Shaft A"
        tier="4"
        piece_type="Handle"
        mesh="sm_ro_rohan_spear_handle_a"
        length="220"
        weight="1.6">
        <BuildData
            piece_offset="30" />
        <Materials>
            <Material id="Wood" count="5" />
        </Materials>
    </CraftingPiece>
""",
    """    <CraftingPiece
        id="sm_ro_rohan_spear_handle_b"
        name="{=aom_sm_ro_rohan_spear_handle_b_name}Rohan Spear Shaft B"
        tier="4"
        piece_type="Handle"
        mesh="sm_ro_rohan_spear_handle_b"
        length="220"
        weight="1.6">
        <BuildData
            piece_offset="30" />
        <Materials>
            <Material id="Wood" count="5" />
        </Materials>
    </CraftingPiece>
""",
]

# Register in all four so the primary usage stays shield-compatible and a couch is added.
WEAPON_DESCRIPTION_CATEGORIES = [
    "OneHandedPolearm",
    "TwoHandedPolearm",
    "TwoHandedPolearm_Couchable",
    "TwoHandedPolearm_Bracing",
]
CRAFTING_TEMPLATE_CATEGORY = "TwoHandedPolearm"

# The two surviving crafted spears, re-pointed at the new pieces.
ITEM_PIECES = {
    "wm_rohan_spear_a": {"Blade": "sm_ro_rohan_spear_blade_a",
                         "Handle": "sm_ro_rohan_spear_handle_a"},
    "wm_rohan_spear_b": {"Blade": "sm_ro_rohan_spear_blade_b",
                         "Handle": "sm_ro_rohan_spear_handle_b"},
}
DELETE_ITEMS = {"wm_rohan_spear_c", "wm_rohan_spear_d", "wm_rohan_spear_f"}
# c and d were the 3.1-thrust heavy spears, f was 1.87-thrust light.
ITEM_REMAP = {
    "wm_rohan_spear_c": "wm_rohan_spear_a",
    "wm_rohan_spear_d": "wm_rohan_spear_a",
    "wm_rohan_spear_f": "wm_rohan_spear_b",
}


# --------------------------------------------------------------------------- #
# Transforms                                                                   #
# --------------------------------------------------------------------------- #
def _remove_elements(text: str, tag: str, ids) -> tuple:
    """Delete whole `<tag id="...">` elements, self-closing or with children."""
    if not ids:
        return text, []
    masked, comments = _protect_comments(text)
    removed, cuts = [], []
    for m in re.finditer(rf"<{tag}\b[^>]*?(/>|>)", masked, re.S):
        block = m.group(0)
        found = re.search(r'\bid="([^"]+)"', block)
        if not found or found.group(1) not in ids:
            continue
        if block.endswith("/>"):
            end = m.end()
        else:
            close = masked.find(f"</{tag}>", m.end())
            if close == -1:
                continue
            end = close + len(f"</{tag}>")
        start = m.start()
        line_start = masked.rfind("\n", 0, start) + 1
        if masked[line_start:start].strip() == "":
            start = line_start
        while end < len(masked) and masked[end] in "\r\n":
            end += 1
        cuts.append((start, end))
        removed.append(found.group(1))
    for start, end in reversed(cuts):
        masked = masked[:start] + masked[end:]
    return _restore_comments(masked, comments), removed


def remove_crafting_pieces(text: str, ids) -> tuple:
    return _remove_elements(text, "CraftingPiece", ids)


def remove_crafted_items(text: str, ids) -> tuple:
    return _remove_elements(text, "CraftedItem", ids)


def dominant_newline(text: str) -> str:
    """The file's MAJORITY line ending, not merely one that occurs.

    `tools/README.md` names presence-testing as the wrong test: several Armory files are
    genuinely mixed (`LOTRLOME_items/mordor/head_armors.xml` is 2356 CRLF against 517 bare
    LF), so a single stray terminator must not decide what every inserted line uses.
    """
    crlf = text.count("\r\n")
    bare_lf = text.count("\n") - crlf
    return "\r\n" if crlf > bare_lf else "\n"


def insert_crafting_pieces(text: str, block: str) -> tuple:
    """Append pieces before `</CraftingPieces>`. Returns (text, inserted_count).

    The count is what was ACTUALLY inserted, so a no-op re-run reports 0 and a missing
    anchor reports 0 rather than letting the caller print the request size as success.
    """
    present = set(re.findall(r'<CraftingPiece\b[^>]*?\bid="([^"]+)"', text, re.S))
    wanted = re.findall(r'<CraftingPiece\b[^>]*?\bid="([^"]+)"', block, re.S)
    missing = [w for w in wanted if w not in present]
    if not missing:
        return text, 0
    idx = text.rfind("</CraftingPieces>")
    if idx == -1:
        return text, 0
    return text[:idx] + block + text[idx:], len(missing)


def remove_xslt_piece_refs(text: str, ids) -> tuple:
    """Drop `<AvailablePiece id="x"/>` / `<UsablePiece piece_id="x"/>` lines for these ids."""
    if not ids:
        return text, 0
    masked, comments = _protect_comments(text)
    pattern = re.compile(
        r'[ \t]*<(?:AvailablePiece|UsablePiece)\s+(?:id|piece_id)="([^"]+)"\s*/>\r?\n?')
    count = 0

    def sub(m):
        nonlocal count
        if m.group(1) in ids:
            count += 1
            return ""
        return m.group(0)

    masked = pattern.sub(sub, masked)
    return _restore_comments(masked, comments), count


def insert_xslt_piece_refs(text: str, match: str, elem: str, attr: str, ids) -> tuple:
    """Insert refs into one template, BEFORE its `<xsl:apply-templates>` passthrough.

    Returns (text, inserted_count). Keeping the passthrough last is a convention rather
    than a correctness requirement (the 2026-08-28 review ran the real transform and
    confirmed trailing content still merges, dropping 0 of vanilla's 233 OneHandedPolearm
    pieces), but the ordering is what the authoring guide specifies, so hold it.
    """
    tpl = re.search(
        r'(<xsl:template\s+match="' + re.escape(match) + r'"\s*>)(.*?)(</xsl:template>)',
        text, re.S)
    if not tpl:
        return text, 0
    body = tpl.group(2)
    passthrough = re.search(r'[ \t]*<xsl:apply-templates\b[^>]*/>', body)
    if not passthrough:
        return text, 0
    nl = dominant_newline(text)
    indent = re.match(r"[ \t]*", passthrough.group(0)).group(0)
    missing = [i for i in ids if f'"{i}"' not in body]
    if not missing:
        return text, 0
    addition = "".join(f'{indent}<{elem} {attr}="{i}" />{nl}' for i in missing)
    new_body = body[:passthrough.start()] + addition + body[passthrough.start():]
    return text[:tpl.start(2)] + new_body + text[tpl.end(2):], len(missing)


def repoint_crafted_item(text: str, item_id: str, pieces: dict) -> tuple:
    """Point one `<CraftedItem>`'s `<Piece>` entries at different piece ids, by Type."""
    m = re.search(rf'<CraftedItem\b[^>]*?\bid="{re.escape(item_id)}".*?</CraftedItem>',
                  text, re.S)
    if not m:
        return text, 0
    block = m.group(0)
    count = 0

    def sub(pm):
        nonlocal count
        ptype = pm.group(3)
        if ptype in pieces and pm.group(2) != pieces[ptype]:
            count += 1
            return f'{pm.group(1)}id="{pieces[ptype]}" Type="{ptype}"'
        return pm.group(0)

    # Scoped to <Piece> deliberately. Running over the whole CraftedItem block relied on
    # no sibling element ever presenting the same id="..." Type="..." adjacency, which is
    # true of today's data and not structurally guaranteed.
    new_block = re.sub(r'(<Piece\s+)id="([^"]+)"\s+Type="(\w+)"', sub, block)
    return text[:m.start()] + new_block + text[m.end():], count


# --------------------------------------------------------------------------- #
# Driver                                                                       #
# --------------------------------------------------------------------------- #
def _write(path: Path, text: str, had_bom: bool, write: bool, tag: str) -> str | None:
    try:
        ET.fromstring(text.encode("utf-8"))
    except ET.ParseError as exc:
        return f"transform produced malformed XML: {exc}"
    if write:
        backup = path.with_suffix(path.suffix + f".bak-{tag}")
        if not backup.exists():
            backup.write_bytes(path.read_bytes())
        write_xml(path, text, had_bom)
    return None


def process_armory(md: Path, write: bool, tag: str) -> list:
    """crafting pieces, the two XSLTs and the item file, for one Armory copy."""
    log = []

    pieces = md / "LOTRLOME_crafting_pieces.xml"
    text, bom = read_xml(pieces)
    nl = dominant_newline(text)
    block = "".join(b.replace("\n", nl) if nl != "\n" else b for b in NEW_PIECE_BLOCKS)
    text, added = insert_crafting_pieces(text, block)
    text, removed = remove_crafting_pieces(text, OLD_PIECE_IDS)
    err = _write(pieces, text, bom, write, tag)
    log.append((pieces.name, f"+{added} pieces, -{len(removed)} pieces", err))

    wd = md / "weapon_descriptions.xslt"
    text, bom = read_xml(wd)
    added = 0
    for cat in WEAPON_DESCRIPTION_CATEGORIES:
        text, n = insert_xslt_piece_refs(
            text, f"WeaponDescription[@id='{cat}']/AvailablePieces",
            "AvailablePiece", "id", NEW_PIECE_IDS)
        added += n
    text, gone = remove_xslt_piece_refs(text, OLD_PIECE_IDS)
    err = _write(wd, text, bom, write, tag)
    log.append((wd.name, f"+{added} refs, -{gone} refs", err))

    ct = md / "crafting_templates.xslt"
    text, bom = read_xml(ct)
    text, added = insert_xslt_piece_refs(
        text, f"CraftingTemplate[@id='{CRAFTING_TEMPLATE_CATEGORY}']/UsablePieces",
        "UsablePiece", "piece_id", NEW_PIECE_IDS)
    text, gone = remove_xslt_piece_refs(text, OLD_PIECE_IDS)
    err = _write(ct, text, bom, write, tag)
    log.append((ct.name, f"+{added} refs, -{gone} refs", err))

    weapons = md / "LOTRLOME_items" / "LOTRAOM_weapons.xml"
    text, bom = read_xml(weapons)
    repointed = 0
    for item, mapping in ITEM_PIECES.items():
        text, n = repoint_crafted_item(text, item, mapping)
        repointed += n
    text, dropped = remove_crafted_items(text, DELETE_ITEMS)
    err = _write(weapons, text, bom, write, tag)
    log.append((weapons.name, f"{repointed} pieces re-pointed, -{len(dropped)} items", err))
    return log


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--armory", default=str(DEFAULT_ARMORY))
    ap.add_argument("--asset-repo", default=str(DEFAULT_ASSET_REPO))
    ap.add_argument("--troops", default=str(DEFAULT_TROOPS))
    ap.add_argument("--write", action="store_true", help="Commit the edits (default: preview)")
    args = ap.parse_args()

    armory = ensure_exists(args.armory, "the LOTRLOME_Armory module")
    troops_root = ensure_exists(args.troops, "the TAOM ModuleData root")
    tag = "rohanspear-" + datetime.now().strftime("%Y%m%d%H%M%S")
    print("WRITING\n" if args.write else "PREVIEW (pass --write to commit)\n")

    errors = 0
    roots = [("live", armory / "ModuleData")]
    asset_repo = Path(args.asset_repo)
    if asset_repo.exists():
        roots.append(("versioned", asset_repo / "ModuleData"))
    else:
        print(f"NOTE: asset repo not at {asset_repo}; the live edit will be unversioned.")

    for label, md in roots:
        print(f"--- Armory ({label}) ---")
        for name, summary, err in process_armory(md, args.write, tag):
            if err:
                errors += 1
                print(f"  ERROR  {name}: {err}")
            else:
                print(f"  {name:32s} {summary}")

    print("\n--- troop rosters (this repo) ---")
    for f in sorted(troops_root.rglob("*.xml")):
        if "Languages" in f.parts:
            continue
        text, bom = read_xml(f)
        new, n = swap_item_refs(text, ITEM_REMAP)
        if n:
            err = _write(f, new, bom, args.write, tag)
            if err:
                errors += 1
                print(f"  ERROR  {f.name}: {err}")
            else:
                print(f"  {n:4d} refs remapped  {f.relative_to(troops_root).as_posix()}")

    if errors:
        print(f"\n{errors} file(s) REFUSED (malformed after transform); nothing written for them.")
        return 1
    if not args.write:
        print("\nNothing was written.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
