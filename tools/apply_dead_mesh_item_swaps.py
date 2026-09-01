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

SECOND WAVE, 2026-09-01: the 2026-08-28 pass swapped consumers off the dead
Gondor and Easterling items but never removed the definitions, so 83 of them sat
in the Armoury still naming meshes that resolve nowhere. They are what a player
sees as a blank icon in the item list. Two mechanisms:

5. 83 orphan definitions deleted. Verified consumer-free twice over: the impact
   audit's five reference shapes, and an independent bare-id grep across all
   three ModuleData trees plus the XSLT.

6. 7 re-meshed, not deleted, because they DO still have consumers:
   `ar_ardunian_elite_armour` (25 Umbar characters) and the six Easterling
   crafting pieces. The pieces are the trap in this wave. The impact audit calls
   them ORPHAN because it matches `Item.<id>` refs and rosters, and a crafting
   piece is referenced by neither shape: it is named by `<UsablePiece>` in
   crafting_templates.xslt and by `<Piece>` inside the CraftedItems
   easterling_sword and easterling_spear. easterling_spear is player CAREER
   STARTING equipment, so deleting those six would have stripped a Rhun start's
   weapon on the strength of a clean-looking ORPHAN verdict.

WHAT THIS DOES NOT COVER, deliberately: `lotr_troll_armor` / `_bracers` /
`_helmet` (Mordor) are dead-and-equipped with no replacement chosen. No troll
armour art survives in either tree, and armour meshes are skinned to the human
skeleton, so no human donor can fit a cave_troll rig. Reported, never touched.

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

    # --- 2026-09-01 wave -------------------------------------------------- #
    # Umbar equips this on 25 characters across 3 files, so it is re-pointed
    # rather than deleted. Black Numenorean infantry elite chest is the same
    # set at the same tier and ships a _slim variant.
    "ar_ardunian_elite_armour": "sm_md_num_inf_chest_elite_a",

    # The six Easterling crafting pieces. NOT deletable: crafting_templates.xslt
    # lists them as <UsablePiece>, and the CraftedItems easterling_sword and
    # easterling_spear are built from them. easterling_spear is player CAREER
    # STARTING equipment, so deleting these would strip a Rhun start's weapon.
    # audit_deleted_mesh_impact.py calls all six ORPHAN because it matches
    # `Item.<id>` refs and rosters, and a crafting piece is referenced by
    # neither shape. Loke-Rim is the same family the 2026-08-28 pass moved
    # Easterling armour onto.
    "easterling_sword_blade":  "sm_rh_loke_sword_blade_a",
    "easterling_sword_guard":  "sm_rh_loke_sword_guard_a",
    "easterling_sword_handle": "sm_rh_loke_sword_handle_a",
    "easterling_sword_pommel": "sm_rh_loke_sword_pommel_a",
    "easterling_spear_blade":  "sm_rh_loke_spear_blade_a",
    # No Rhun spear handle survives. This is not a taste call: both surviving
    # Rhun spears (sm_rh_loke_spear_a, sm_rh_drag_spear_a) already pair their
    # blade with exactly this handle.
    "easterling_spear_handle": "wm_harad_spear_a01_handle",
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

    # --- 2026-09-01 wave: 83 definitions whose art is gone from BOTH trees -- #
    # Every one verified to have zero consumers by two independent sweeps: the
    # audit's five reference shapes, and a bare-id grep across all three
    # ModuleData trees plus the XSLT. The 2026-08-28 pass swapped consumers off
    # the Gondor and Easterling items but never removed the definitions, which
    # is why they are orphans today rather than equipped.
    # Gondor named lords: art deleted, consumers already swapped to regional kit on 2026-08-28
    "angbor_body", "angbor_boots", "angbor_gloves", "angbor_helmet", "angbor_shoulder",
    "forlong_body", "forlong_boots", "forlong_gloves", "forlong_helmet", "forlong_shoulder",
    "golasgil_body", "golasgil_boots", "golasgil_gloves", "golasgil_helment",
    "golasgil_shoulder", "hirluin_body", "hirluin_boots", "hirluin_gloves", "hirluin_helmet",
    "hirluin_shoulder", "imrahil_body", "imrahil_boot", "imrahil_gloves", "imrahil_helmet",
    "imrahil_shoulder", "lossarnach_coat",

    # Moria orc: 15 dead meshes recorded in no prior cleanup
    "moriaorc_v1_boots", "moriaorc_v1_bracers", "moriaorc_v1_helmet", "moriaorc_v1_shoulder",
    "moriaorc_v1_torso", "moriaorc_v2_boots", "moriaorc_v2_bracers", "moriaorc_v2_helmet",
    "moriaorc_v2_torso", "moriaorc_v3_boots", "moriaorc_v3_bracers", "moriaorc_v3_helmet",
    "moriaorc_v3_torso", "moriaorc_v4_bracers", "moriaorc_v4_torso",

    # Black Numenorean: only the body is equipped (re-pointed above); these three are not
    "ar_ardunian_elite_hand", "ar_ardunian_elite_helmet", "ar_ardunian_elite_shoses",

    # Easterling armour: the whole easterlings/ folder is gone, both trees
    "easterling02_v1_boots", "easterling02_v1_cape", "easterling02_v1_gloves",
    "easterling02_v1_helmet", "easterling02_v1_helmet_v2", "easterling02_v1_torso",
    "easterling_boots", "easterling_cape", "easterling_glove", "easterling_head",
    "easterling_helmet_v1", "easterling_helmet_v10", "easterling_helmet_v11",
    "easterling_helmet_v12", "easterling_helmet_v2", "easterling_helmet_v3",
    "easterling_helmet_v4", "easterling_helmet_v5", "easterling_helmet_v6",
    "easterling_helmet_v7", "easterling_helmet_v8", "easterling_helmet_v9", "easterling_shield",
    "easterling_torso", "easterlingwarriors01_boots", "easterlingwarriors01_cape",
    "easterlingwarriors01_gloves", "easterlingwarriors01_helmet", "easterlingwarriors01_torso",
    "easterlingwarriors02_cape", "easterlingwarriors02_helmet", "easterlingwarriors02_torso",
    "easterlingwarriors03_cape", "easterlingwarriors03_helmet", "easterlingwarriors03_torso",
    "easterlingwarriors04_cape", "easterlingwarriors04_helmet", "easterlingwarriors04_torso",

    # Rhun shield sharing the dead easterling_shield mesh
    "rhun_tournament_sparring_shield",

    # --- 2026-09-01, second pass: hand-authored `_slim` items are redundant --- #
    # The engine appends the slim-BUILD suffix itself. BasicCharacterTableau.cs:536:
    #   text2 = ((!flag3) ? (text2 + (flag2 ? "_slim" : ""))
    #                     : (text2 + (flag2 ? "_converted_slim" : "_converted")));
    # where flag2 is the slim-build flag. So for any item whose mesh is `X`, a
    # slim-built character already resolves `X_slim` with no XML involvement.
    # Authoring a SECOND item whose mesh is literally `X_slim` duplicates what the
    # engine does for free, and the duplicate can only be worn by being explicitly
    # equipped, which nothing does: all 13 verified to have zero consumers across
    # all three ModuleData trees.
    #
    # Note `_slim` is NOT the female variant. That is `_female`, or
    # `_converted` / `_converted_slim`, gated on has_gender_variations. Conflating
    # the two is what produced this session's one wrong data change.
    "faramir_armor_slim", "ithilien_jerkin_long_slim", "ithilien_jerkin_long_var_slim",
    "ithilien_jerkin_short_slim", "ithilien_jerkin_short_var_slim",
    "gondor_noble_coat_a_slim", "gondor_noble_coat_b_slim",
    "gondor_noble_jerkin_a_slim", "gondor_noble_jerkin_b_slim",
    "theodred_armour_slim",
    # Same redundancy, ids that do not advertise it: their MESH is a `_slim`.
    "m_northern_armor_a2",   # mesh sk_northern_armor_light_a_slim
    "m_northern_armor_b2",   # mesh sk_northern_armor_medium_a_slim
    "m_northern_armor_b4",   # mesh sk_northern_armor_medium_b_slim
}

# Dead-and-equipped, but outside this decision. Reported, never touched.
NOT_COVERED = {
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
    """Point a named entry's `mesh=` at a different mesh, leaving all else alone.

    Covers `<CraftingPiece>` as well as `<Item>`: a piece's dead mesh makes every
    CraftedItem built from it invisible, and `easterling_spear` is player career
    starting equipment, so pieces are re-pointed rather than deleted.

    Only the opening tag is rewritten, which is what keeps `<BladeData
    holster_mesh="">` out of scope. The `\\b` in the mesh pattern does the same
    job for `holster_mesh=` (no word boundary after an underscore).
    """
    if not mapping:
        return text, 0
    masked, spans = _protect_comments(text)
    count = 0
    for item_id, new_mesh in mapping.items():
        for m in list(re.finditer(r"<(?:Item|CraftingPiece)\b[^>]*?>", masked, re.S)):
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
        ids.update(re.findall(
            r'<(?:Item|CraftedItem|CraftingPiece)\b[^>]*?\bid="([^"]+)"', text, re.S))
    return ids


def preflight(armory: Path) -> tuple:
    """Check the mapping against what the Armory actually defines.

    Returns (problems, applied). Only `problems` blocks.

    A missing REPLACEMENT is fatal and always has been: swapping onto a
    non-existent id trades an invisible item for a naked troop.

    A missing SOURCE is not. It is the expected state after a successful run,
    because a swapped-away item is usually deleted in the same pass. Treating it
    as fatal made the tool fail on its own success: the 2026-08-28 run deleted
    the 57 Erebor colour variants, 5 of which are ITEM_SWAPS sources, so every
    later run aborted at pre-flight with nothing to fix. The rule this violated
    is `.claude/rules/moduledata-validation.md`, which requires idempotency on
    re-run. Report it as already-applied instead, and keep it fatal only when
    the id was never a deletion target (which would mean a typo in the mapping).
    """
    defined = armory_item_ids(armory)
    problems, applied = [], []
    for old, new in sorted(ITEM_SWAPS.items()):
        if new not in defined:
            problems.append(f"replacement not defined in the Armory: {old} -> {new}")
        if old not in defined:
            if old in DELETE_ITEMS:
                applied.append(f"swap already applied and definition removed: {old}")
            else:
                problems.append(f"swap source not defined and not a delete "
                                f"target (typo in the mapping?): {old}")
    for item_id, new_mesh in sorted(MESH_REPOINTS.items()):
        if item_id not in defined:
            problems.append(f"re-mesh target not defined: {item_id}")
    return problems, applied


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

    problems, applied = preflight(armory)
    if problems:
        print("PRE-FLIGHT FAILED:", file=sys.stderr)
        for p in problems:
            print(f"  {p}", file=sys.stderr)
        return 2
    print(f"Pre-flight OK: {len(ITEM_SWAPS)} replacements all defined in the Armory.")
    if applied:
        print(f"  ({len(applied)} swap(s) already applied in an earlier run)")

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
