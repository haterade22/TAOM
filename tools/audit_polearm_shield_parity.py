#!/usr/bin/env python3
"""Fail when a troop carries a shield beside a melee weapon the combat AI will refuse to use.

THE DEFECT CLASS
----------------
A crafted polearm's usages come entirely from `WeaponDescription` matching -- a description
applies only when EVERY piece the item uses appears in that description's `<AvailablePieces>`,
and the FIRST match in the crafting template's description order becomes the primary usage
(`Crafting.cs:566-608`). Miss the piece registration and the item silently resolves to the
two-handed description, whose usage set is flagged `requires_no_shield`. Pair that item with a
shield in an equipment roster and the AI drops it the instant combat starts, fighting with the
sidearm for the rest of the battle. Nothing errors; the weapon just never gets used.

This has now shipped twice -- the roster side across five cultures (PR #445) and the
registration side for all four Dale spears (PR #447). Both were found by a player, and both were
verified by a scan written by hand and thrown away. This is that scan, kept.

WHAT IT CHECKS
--------------
1. Every equipment roster that pairs a shield with a melee weapon (a polearm, or a two-handed
   sword, axe or mace, which has no one-handed mode) whose PRIMARY usage set is flagged
   `requires_no_shield`. Any culture, crafted or plain-`<Item>`. Pairs held in KNOWN_FAILURES
   print under KNOWN and do not fail.
2. Every `<AvailablePiece>` id in the merged weapon descriptions resolves to a real
   `<CraftingPiece>`, unless KNOWN_DANGLING holds it. A typo'd id in an XSLT fails silently in game -- the same class of hazard
   `validate_mesh_refs.py` and `audit_item_refs.py` exist to catch elsewhere.
3. Every crafted weapon a roster carries resolves to a primary usage. A weapon that resolves to
   none was never judged by check 1, which is how 20 placeholder weapons went unchecked (#1354).

Item and description data come from the INSTALLED modules (that is where LOTRLOME_Armory lives
and it is not in this repo), except the module whose rosters are checked: that one is read from
the checked tree, so an edit counts before it is deployed. Stylesheets are the ones each module's
SubModule.xml `<XmlName>` rows name, and the modules that module's `<IncompatibleModules>` lists
(NavalDLC for TAOM) are left out, as the engine never loads them beside it.

EXIT CODES
----------
    0  clean, or the install is absent (SKIP -- never a false PASS)
    1  a finding
    2  bad input

USAGE
-----
    python tools/audit_polearm_shield_parity.py
    python tools/audit_polearm_shield_parity.py --game-modules "<game>/Modules" --rosters <dir>
"""
from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

import lxml.etree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from _gamedir import ENV_VAR, game_modules  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
DEFAULT_ROSTERS = REPO_ROOT / "Main" / "_Module" / "ModuleData"

BLOCKING_FLAG = "requires_no_shield"

# Melee only. A bow's usage set is `requires_no_shield` too, but slinging the bow to fight with
# shield + sidearm is how ranged troops are meant to work -- vanilla pairs the two constantly.
# The defect is a MELEE weapon the AI can never draw, so ranged types are out of scope, not
# suppressed case by case.
MELEE_TYPES = frozenset({"Polearm", "OneHandedWeapon", "TwoHandedWeapon"})

# Every melee type fails the same way: a new pair fails the build. Two-handed weapons joined in
# #1312, after each flagged roster was fixed or held below.

# (owner, item) pairs held with their issue, printed in full every run, so that a NEW pair still
# fails the build. Two kinds:
# - #526: debt. These pairs were already there when the roster walker learned the second element
#   casing on 2026-09-01; before that the tool never read a standalone roster file. Emptying
#   them closes #526.
# - #1312: accepted design. A shield troop keeps a two-handed weapon as a backup for when the
#   shield breaks (Mike, 2026-11-15). These entries stay.
#
# Keyed on (owner id, item id) and NOT on the roster index, because the index shifts whenever a
# roster is inserted above it, which would turn a cosmetic edit into a spurious failure.
#
# The value carries the EXPECTED OCCURRENCE COUNT as well as the issue. Keying on the pair alone
# made the ratchet blind to multiplicity: 10 keys were suppressing 13 occurrences, so a roster
# already on the list gaining a SECOND copy of the same unusable polearm would have been filed as
# old debt. A count that goes UP is new debt and fails; a count that goes DOWN means the entry is
# partly fixed and overstates the debt, so it would absorb a future regression, and that fails too.
KNOWN_FAILURES: dict[tuple[str, str], tuple[int, str]] = {
    # Mordor player starting gear: a two-handed-resolving polearm beside a shield, in the
    # culture-default kit the player is handed at character creation. 4 rosters. The career kits
    # left this list when their polearm became sm_md_num_lance_a, which resolves one-handed.
    ("player_char_creation_mordor_mercenary_f", "starter_wm_mordor_set1_polearm_a01"): (1, "#526"),
    ("player_char_creation_mordor_mercenary_m", "starter_wm_mordor_set1_polearm_a01"): (1, "#526"),
    ("player_char_creation_mordor_retainer_f", "starter_wm_mordor_set1_polearm_a01"): (1, "#526"),
    ("player_char_creation_mordor_retainer_m", "starter_wm_mordor_set1_polearm_a01"): (1, "#526"),
    # Companion templates: a pike, whose usage set is requires_no_shield by design.
    # The umbar entry was retired 2026-09-01: its only shield was removed under the
    # "a two-hander gets no shield" pass (#531), so the pair no longer exists and the
    # ratchet failed on the stale entry, which is the behaviour working as intended.
    ("npc_companion_equipment_template_isengard", "isengard_pike_a"): (3, "#526"),
    # Two-handed weapons beside a shield (#1312): a backup for when the shield breaks. The two Dol
    # Guldur career kits are the player's, who can switch weapons; the game moves the shield to
    # the back.
    # fords_troops.xml
    ("fow_erebor_oathsworn_legionary", "sm_dwarf_erebor_mace_a"): (1, "#1312: backup after the shield breaks"),
    ("fow_erebor_oathsworn_legionary", "sm_dwarf_iron_hammer_a"): (1, "#1312: backup after the shield breaks"),
    ("fow_erebor_oathsworn_legionary", "sm_dwarf_iron_hammer_b"): (1, "#1312: backup after the shield breaks"),
    ("fow_erebor_oathsworn_legionary", "sm_dwarf_iron_hammer_c"): (1, "#1312: backup after the shield breaks"),
    ("fow_ironpass_ram_rider", "sm_dwarf_iron_axe_a"): (4, "#1312: backup after the shield breaks"),
    ("fow_mordor_num_temple_guard", "sm_md_num_sword_2h_c"): (4, "#1312: backup after the shield breaks"),
    # named_companions.xml
    ("named_companion_balakhor", "wm_gf_knight_longsword"): (2, "#1312: backup after the shield breaks"),
    ("named_companion_thyrell", "wm_gf_knight_longsword"): (2, "#1312: backup after the shield breaks"),
    # taom_career_starting_equipment.xml
    ("player_career_dolguldur_infantry_f", "wm_dol_goldur_axe_a04"): (1, "#1312: player career kit"),
    ("player_career_dolguldur_infantry_m", "wm_dol_goldur_axe_a04"): (1, "#1312: player career kit"),
    # taom_enlistment_equipment.xml
    ("enlist_erebor_cavalry_sergeant", "sm_dwarf_iron_axe_b"): (1, "#1312: enlistment kit, backup after the shield breaks"),
    ("enlist_erebor_cavalry_soldier", "sm_dwarf_iron_axe_a"): (1, "#1312: enlistment kit, backup after the shield breaks"),
    ("enlist_erebor_infantry_veteran", "sm_dwarf_iron_axe_a"): (1, "#1312: enlistment kit, backup after the shield breaks"),
    ("enlist_eredluin_cavalry_sergeant", "sm_dwarf_iron_axe_b"): (1, "#1312: enlistment kit, backup after the shield breaks"),
    ("enlist_eredluin_cavalry_soldier", "sm_dwarf_iron_axe_a"): (1, "#1312: enlistment kit, backup after the shield breaks"),
    ("enlist_eredluin_infantry_veteran", "sm_dwarf_iron_axe_a"): (1, "#1312: enlistment kit, backup after the shield breaks"),
    ("enlist_mordor_infantry_sergeant", "sm_md_num_sword_2h_a"): (1, "#1312: enlistment kit, backup after the shield breaks"),
    # taom_equipment_sets_mordor.xml
    ("mordor_num_bat_template_lord_inf", "sm_md_num_sword_2h_c"): (1, "#1312: backup after the shield breaks"),
    ("mordor_num_bat_template_mouth_of_sauron", "sm_md_num_sword_2h_c"): (1, "#1312: backup after the shield breaks"),
    ("mordor_num_civ_template_lord_inf", "sm_md_num_sword_2h_c"): (1, "#1312: backup after the shield breaks"),
    ("mordor_num_civ_template_mouth_of_sauron", "sm_md_num_sword_2h_c"): (1, "#1312: backup after the shield breaks"),
    # taom_wanderer_equipment.xml
    ("npc_companion_equipment_template_anduinvale", "dale_great_axe_a"): (2, "#1312: backup after the shield breaks"),
    ("npc_companion_equipment_template_angaladh", "dale_great_axe_a"): (2, "#1312: backup after the shield breaks"),
    ("npc_companion_equipment_template_dale", "dale_great_axe_a"): (2, "#1312: backup after the shield breaks"),
    ("npc_companion_equipment_template_dolguldur", "wm_dol_goldur_axe_a01"): (1, "#1312: backup after the shield breaks"),
    ("npc_companion_equipment_template_dorwinion", "dale_great_axe_a"): (2, "#1312: backup after the shield breaks"),
    ("npc_companion_equipment_template_dunland", "dunland_caerdh_axe_2h_a"): (1, "#1312: backup after the shield breaks"),
    # troops_erebor.xml
    ("erebor_oathsworn_legionary", "sm_dwarf_erebor_mace_a"): (1, "#1312: backup after the shield breaks"),
    ("erebor_oathsworn_legionary", "sm_dwarf_iron_hammer_a"): (1, "#1312: backup after the shield breaks"),
    ("erebor_oathsworn_legionary", "sm_dwarf_iron_hammer_b"): (1, "#1312: backup after the shield breaks"),
    ("erebor_oathsworn_legionary", "sm_dwarf_iron_hammer_c"): (1, "#1312: backup after the shield breaks"),
    ("erebor_oathsworn_royal_legionary", "sm_dwarf_erebor_mace_b"): (1, "#1312: backup after the shield breaks"),
    ("erebor_oathsworn_royal_legionary", "sm_dwarf_iron_hammer_a"): (1, "#1312: backup after the shield breaks"),
    ("erebor_oathsworn_royal_legionary", "sm_dwarf_iron_hammer_c"): (1, "#1312: backup after the shield breaks"),
    ("erebor_oathsworn_royal_legionary", "sm_dwarf_iron_hammer_d"): (1, "#1312: backup after the shield breaks"),
    ("ironpass_goat_charger", "sm_dwarf_iron_axe_b"): (4, "#1312: backup after the shield breaks"),
    ("ironpass_ram_breaker", "sm_dwarf_iron_axe_b"): (2, "#1312: backup after the shield breaks"),
    ("ironpass_ram_breaker", "sm_dwarf_iron_axe_c"): (2, "#1312: backup after the shield breaks"),
    ("ironpass_ram_herder", "sm_dwarf_iron_axe_a"): (4, "#1312: backup after the shield breaks"),
    ("ironpass_ram_marshal", "sm_dwarf_iron_axe_c"): (4, "#1312: backup after the shield breaks"),
    ("ironpass_ram_rider", "sm_dwarf_iron_axe_a"): (4, "#1312: backup after the shield breaks"),
    ("ironpass_ram_vanguard", "sm_dwarf_iron_axe_c"): (4, "#1312: backup after the shield breaks"),
    # troops_gondor.xml
    ("gondor_da_foot_knight", "numenorean_greatsword_elite_a"): (2, "#1312: backup after the shield breaks"),
    ("gondor_da_foot_knight", "numenorean_greatsword_elite_b"): (2, "#1312: backup after the shield breaks"),
    ("gondor_da_infantry", "numenorean_greatsword_medium_a"): (1, "#1312: backup after the shield breaks"),
    ("gondor_da_infantry", "numenorean_greatsword_medium_b"): (1, "#1312: backup after the shield breaks"),
    ("gondor_da_swan_guard", "numenorean_greatsword_elite_a"): (2, "#1312: backup after the shield breaks"),
    ("gondor_da_swan_guard", "numenorean_greatsword_elite_b"): (2, "#1312: backup after the shield breaks"),
    ("gondor_da_vet_infantry", "numenorean_greatsword_heavy_a"): (1, "#1312: backup after the shield breaks"),
    ("gondor_da_vet_infantry", "numenorean_greatsword_heavy_b"): (1, "#1312: backup after the shield breaks"),
    # troops_mordor.xml
    ("mordor_num_infantry", "sm_md_num_sword_2h_a"): (2, "#1312: backup after the shield breaks"),
    ("mordor_num_temple_guard", "sm_md_num_sword_2h_c"): (4, "#1312: backup after the shield breaks"),
    ("mordor_num_vet_infantry", "sm_md_num_sword_2h_b"): (2, "#1312: backup after the shield breaks"),
    ("mordor_num_warden", "sm_md_num_sword_2h_c"): (2, "#1312: backup after the shield breaks"),
}

# <AvailablePiece> ids that name no <CraftingPiece>, held with the reason they are not fixed here.
# None is in this repo: each is in the live Armory's weapon_descriptions.xslt (three also in its
# crafting_templates.xslt) or in vanilla's own data. No item uses them, and the engine skips a
# piece id it cannot resolve (WeaponDescription.Deserialize, CraftingTemplate.Deserialize). An
# edit to the live Armory reverts on reinstall, so they are held rather than edited (#1312).
# A dangling id not listed here fails the run, and so does a listed id that no longer dangles.
KNOWN_DANGLING: dict[str, str] = {
    "vlandian_blade_10": "vanilla's own, in Native weapon_descriptions.xml",
    "numenorean_sword_handle_e": "#1312: live Armory, no item uses it",
    "wm_gf_knight_spear_guard": "#1312: live Armory, no item uses it",
    "wm_gf_knight_spear_pommel": "#1312: live Armory, no item uses it",
    "wm_isengard_2h_axe_c02_handle": "#1312: live Armory, no item uses it",
    "wm_isengard_2h_axe_c03_handle": "#1312: live Armory, no item uses it",
}


class GateInputError(Exception):
    """Input the gate cannot judge (a SubModule.xml it cannot parse): exit 2, never a finding."""


def _parse(path: Path):
    return ET.parse(str(path), ET.XMLParser(recover=True, huge_tree=True))


def _submodule(path: Path):
    """The root of a SubModule.xml, parsed strictly: the module list and stylesheets come from it,
    so a file the engine would reject must stop the run rather than drop the module in silence."""
    try:
        root = ET.parse(str(path)).getroot()
    except (ET.XMLSyntaxError, OSError) as exc:
        raise GateInputError(f"cannot parse {path}: {exc}") from exc
    if root is None:
        raise GateInputError(f"cannot parse {path}: no root element")
    return root


def incompatible(submodule: Path) -> frozenset[str]:
    """The module ids a SubModule.xml lists under <IncompatibleModules>.

    TAOM lists NavalDLC there: the two never load together, so NavalDLC's weapon stylesheets and
    pieces must not count (#1354).
    """
    block = _submodule(submodule).find("IncompatibleModules")
    if block is None:
        return frozenset()
    return frozenset(m.get("Id") for m in block.findall("Module") if m.get("Id"))


def module_dirs(modules: Path, own: Path | None, skip: frozenset[str]) -> list[Path]:
    """The module folders to read, sorted by module id (load-equivalent, see merged()).

    Every installed module except the skipped ids. `own` is the checked module's folder (the one
    holding the SubModule.xml beside --rosters): it takes the place of its installed copy, so an
    undeployed edit counts and a stale deployed file does not (#1354).
    """
    by_id = {p.name: p for p in modules.iterdir() if p.is_dir()}
    if own is not None:
        id_node = _submodule(own / "SubModule.xml").find("Id")
        own_id = id_node.get("value") if id_node is not None else None
        by_id[own_id or own.name] = own
    return [by_id[k] for k in sorted(by_id) if k not in skip]


def stylesheets(module: Path, xml_id: str) -> list[Path]:
    """The stylesheets the engine applies from this module for one XmlName id, in row order.

    The engine finds a module's files through its SubModule.xml rows
    (`<XmlName id="WeaponDescriptions" path="placeholder_weapons/ph_weapon_descriptions"/>`), and a
    path may sit in a subfolder. Reading `<name>.xslt` at the top level instead missed TAOM's
    placeholder registrations, so every `taom_ph_*` weapon resolved to no usage (#1354).
    """
    sub = module / "SubModule.xml"
    if not sub.is_file():
        return []
    sheets = []
    for row in _submodule(sub).iter("XmlName"):
        path = row.get("path")
        if row.get("id") == xml_id and path:
            sheet = module / "ModuleData" / f"{path}.xslt"
            if sheet.is_file():
                sheets.append(sheet)
    return sheets


def merged(dirs: list[Path], name: str, xml_id: str):
    """Native's `<name>.xml` with every module's `xml_id` stylesheets chained onto it, as the engine does.

    `MBObjectManager.CreateMergedXmlFile` applies each contributing module's transform in load
    order. TAOM's contributors here are all additive (each override template ends by applying
    templates to the node it replaced), so a sorted order gives the same document as load order,
    and it keeps LOTRLOME_Armory, which replaces the lists, before TAOM, which appends to them.
    """
    native = next((d for d in dirs if d.name == "Native"), None)
    base = native / "ModuleData" / f"{name}.xml" if native is not None else None
    if base is None or not base.is_file():
        return None
    doc = _parse(base)
    for module in dirs:
        for sheet in stylesheets(module, xml_id):
            doc = ET.XSLT(_parse(sheet))(doc)
    return doc


def usage_flags(modules: Path) -> dict[str, set[str]]:
    """usage-set id -> its flags, falling back to the base_set chain when it declares none.

    Own-flags-first rather than a union up the chain: in shipped data a child that declares any
    flags declares the complete set, and a union would let a base's `requires_no_shield` leak
    onto a child that deliberately dropped it.
    """
    path = modules / "Native" / "ModuleData" / "item_usage_sets.xml"
    if not path.is_file():
        return {}
    raw: dict[str, tuple[str | None, set[str]]] = {}
    for node in _parse(path).getroot().iter("item_usage_set"):
        raw[node.get("id")] = (node.get("base_set"), {f.get("name") for f in node.iter("flag")})

    resolved: dict[str, set[str]] = {}
    for key in raw:
        seen, cursor = set(), key
        while cursor in raw and cursor not in seen:
            seen.add(cursor)
            base, flags = raw[cursor]
            if flags:
                resolved[key] = flags
                break
            cursor = base
        resolved.setdefault(key, set())
    return resolved


def crafting_pieces(dirs: list[Path]) -> dict[str, set[str]]:
    """piece id -> the usage-feature tokens that piece excludes."""
    out: dict[str, set[str]] = {}
    for module in dirs:
        data = module / "ModuleData"
        if not data.is_dir():
            continue
        for path in list(data.glob("*crafting_pieces*.xml")) + list(data.glob("*/*crafting_pieces*.xml")):
            for node in _parse(path).getroot().iter("CraftingPiece"):
                excluded = node.get("excluded_item_usage_features") or ""
                out[node.get("id")] = {t for t in excluded.split(":") if t}
    return out


def descriptions(dirs: list[Path]):
    """description id -> (available piece ids, its item_usage_features tokens)."""
    doc = merged(dirs, "weapon_descriptions", "WeaponDescriptions")
    out: dict[str, tuple[set[str], list[str]]] = {}
    if doc is None:
        return out
    for node in doc.getroot().iter("WeaponDescription"):
        pieces = node.find("AvailablePieces")
        # findall, not plain iteration: lxml yields comment nodes as children too, and both these
        # files are commented per culture block. `comment.get("id")` is None, which poisons the set.
        ids = {p.get("id") for p in pieces.findall("AvailablePiece")} if pieces is not None else set()
        features = [t for t in (node.get("item_usage_features") or "").split(":") if t]
        out[node.get("id")] = (ids, features)
    return out


def templates(dirs: list[Path]) -> dict[str, tuple[str, list[str]]]:
    """crafting template id -> (item_type, its WeaponDescription ids in order).

    The order is what decides the primary usage, so it must be preserved verbatim.
    """
    doc = merged(dirs, "crafting_templates", "CraftingTemplates")
    out: dict[str, tuple[str, list[str]]] = {}
    if doc is None:
        return out
    for node in doc.getroot().iter("CraftingTemplate"):
        block = node.find("WeaponDescriptions")
        out[node.get("id")] = (
            node.get("item_type") or "",
            [d.get("id") for d in block.findall("WeaponDescription")] if block is not None else [],
        )
    return out


def items(dirs: list[Path]):
    """(crafted, plain, shields).

    crafted: id -> (template, pieces).  plain: id -> (Type, item_usage).
    """
    crafted: dict[str, tuple[str, list[str]]] = {}
    plain: dict[str, tuple[str, str]] = {}
    shields: set[str] = set()
    for module in dirs:
        data = module / "ModuleData"
        if not data.is_dir():
            continue
        for path in data.rglob("*.xml"):
            try:
                root = _parse(path).getroot()
            except (ET.XMLSyntaxError, OSError):
                continue
            if root is None:
                continue
            for node in root.iter("CraftedItem"):
                crafted[node.get("id")] = (
                    node.get("crafting_template"),
                    [p.get("id") for p in node.iter("Piece")],
                )
            for node in root.iter("Item"):
                iid = node.get("id")
                if not iid:
                    continue
                item_type = node.get("Type") or ""
                if item_type == "Shield":
                    shields.add(iid)
                weapon = node.find(".//Weapon[@item_usage]")
                if weapon is not None:
                    plain[iid] = (item_type, weapon.get("item_usage"))
    return crafted, plain, shields


def primary_usage(item_pieces, template, descs, templs, pieces_meta):
    """(description id, usage-set id) for the item's primary usage, or (None, None)."""
    used = set(item_pieces)
    for desc_id in templs.get(template, ("", []))[1]:
        available, features = descs.get(desc_id, (set(), []))
        if not used or not used <= available:
            continue
        excluded = set().union(*(pieces_meta.get(p, set()) for p in used)) if used else set()
        kept = [t for t in features if t not in excluded]
        return desc_id, "_".join(kept)
    return None, None


def rosters(root: Path):
    """(owner id, file, roster index, item ids) for every EquipmentRoster under `root`.

    Iterates the EquipmentRoster elements themselves and walks UP for the owning id. Iterating
    candidate owners instead double-counts, because an NPCCharacter and its own <Equipments>
    child both match and each yields the same rosters.

    BOTH element casings, and that is not cosmetic. Inline troop rosters in `troops_*.xml` spell
    the child `<equipment>`; every standalone roster file under `equipmentsets/` spells it
    `<Equipment>` (measured 2026-09-01: troops_gondor.xml 2,109 lowercase and 0 upper,
    taom_lord_template_equipment.xml 0 lowercase and 1,918 upper). XML is case-sensitive, so matching a
    single casing made this gate structurally blind to every standalone roster file while printing
    PASS: the shape lessons/data-content-cultures.md calls "a gate that excludes the category the
    bug lives in reports zero forever".
    """
    for path in sorted(root.rglob("*.xml")):
        try:
            tree = _parse(path).getroot()
        except (ET.XMLSyntaxError, OSError):
            continue
        if tree is None:
            continue
        counter: dict[str, int] = {}
        for roster in tree.iter("EquipmentRoster"):
            ids = [
                (e.get("id") or "").split(".", 1)[-1]
                for e in roster.iter("equipment", "Equipment")
                if e.get("id")
            ]
            if not ids:
                continue
            # The roster's OWN id first, then upwards. Inline troop rosters are anonymous and
            # take their name from the enclosing NPCCharacter, but a standalone
            # <EquipmentRoster id="..."> in equipmentsets/ carries it directly, and walking
            # straight past that reported the file stem for every one of them -- naming the file
            # a finding is in rather than the roster that has to be edited.
            owner, node = path.stem, roster
            while node is not None:
                if node.get("id"):
                    owner = node.get("id")
                    break
                node = node.getparent()
            index = counter.get(owner, 0)
            counter[owner] = index + 1
            yield owner, path, index, ids


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--game-modules", help="the Modules folder to read item data from")
    parser.add_argument("--rosters", help="the ModuleData folder holding equipment rosters")
    args = parser.parse_args()

    modules = Path(args.game_modules) if args.game_modules else game_modules(DEFAULT_GAME_ROOT)
    if not modules.is_dir():
        print(f"SKIP: no Bannerlord Modules folder at {modules}")
        print(f"      Set ${ENV_VAR} to run this check.")
        return 0

    roster_root = Path(args.rosters) if args.rosters else DEFAULT_ROSTERS
    if not roster_root.is_dir():
        print(f"ERROR: roster folder not found: {roster_root}", file=sys.stderr)
        return 2

    # The checked module's own SubModule.xml names the modules that never load beside it (#1354).
    # A --rosters folder with none beside it (a fixture, a copy) takes TAOM's list instead.
    own_sub = roster_root.parent / "SubModule.xml"
    own = roster_root.parent if own_sub.is_file() else None
    skip_from = own_sub if own is not None else DEFAULT_ROSTERS.parent / "SubModule.xml"
    try:
        skip = incompatible(skip_from) if skip_from.is_file() else frozenset()
        dirs = module_dirs(modules, own, skip)
        descs = descriptions(dirs)
        templs = templates(dirs)
        pieces_meta = crafting_pieces(dirs)
        crafted, plain, shields = items(dirs)
    except GateInputError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    flags = usage_flags(modules)
    if not descs or not templs:
        print(f"ERROR: no weapon_descriptions/crafting_templates under {modules}", file=sys.stderr)
        return 2

    # Check 2 first -- a dangling piece id makes every verdict below untrustworthy.
    dangling = sorted(
        {p for available, _ in descs.values() for p in available if p not in pieces_meta}
    )

    blocking: dict[str, tuple[str, str, str]] = {}
    for item_id, (template, item_pieces) in crafted.items():
        item_type = templs.get(template, ("", []))[0]
        if item_type not in MELEE_TYPES:
            continue
        desc_id, usage = primary_usage(item_pieces, template, descs, templs, pieces_meta)
        if usage and BLOCKING_FLAG in flags.get(usage, set()):
            blocking[item_id] = (item_type, desc_id, usage)
    for item_id, (item_type, usage) in plain.items():
        if item_type in MELEE_TYPES and BLOCKING_FLAG in flags.get(usage, set()):
            blocking[item_id] = (item_type, "<plain item_usage>", usage)

    # Check 3. A roster weapon that resolves to no usage cannot be judged by check 1, and passing it
    # in silence is how all 20 placeholder weapons went unchecked (#1354).
    unresolved: dict[str, str] = {}
    failing, known = [], []
    for troop, path, index, ids in rosters(roster_root):
        for item_id in ids:
            if item_id not in crafted or item_id in unresolved:
                continue
            template, item_pieces = crafted[item_id]
            if template not in templs:
                unresolved[item_id] = f"crafting template {template!r} is not defined"
            elif templs[template][0] in MELEE_TYPES and primary_usage(
                    item_pieces, template, descs, templs, pieces_meta)[1] is None:
                unresolved[item_id] = f"no WeaponDescription of {template} lists all its pieces"
        if not any(i in shields for i in ids):
            continue
        for item_id in ids:
            if item_id not in blocking:
                continue
            item_type, desc_id, usage = blocking[item_id]
            row = (troop, path, index, item_id, item_type, desc_id, usage)
            (known if (troop, item_id) in KNOWN_FAILURES else failing).append(row)

    # Multiplicity. A ratcheted pair occurring MORE often than it was ratcheted at is new debt
    # wearing an old label, so the excess occurrences move to the failing list.
    counted: dict[tuple[str, str], int] = {}
    excess = []
    for row in known:
        key = (row[0], row[3])
        counted[key] = counted.get(key, 0) + 1
        if counted[key] > KNOWN_FAILURES[key][0]:
            excess.append(row)
    if excess:
        known = [r for r in known if r not in excess]
        failing.extend(excess)

    print(f"Modules:  {modules}")
    if own is not None:
        print(f"Own:      {own} (read in place of its installed copy)")
    print(f"Rosters:  {roster_root}")
    if skip:
        print(f"Skipped:  {', '.join(sorted(skip))} (incompatible, from {skip_from})")
    print(
        f"Loaded {len(descs)} weapon descriptions, {len(templs)} crafting templates, "
        f"{len(crafted)} crafted items, {len(shields)} shields."
    )
    print(f"Melee weapons whose primary usage is {BLOCKING_FLAG}: {len(blocking)}")

    status = 0
    # Fails in every mode, as KNOWN_FAILURES does: the callers (data_gates.py, the PostToolUse
    # hook) run the default mode, and a crafting_pieces() read that found nothing would make every
    # id dangle. register_one_handed_polearms.py also refuses to write a piece id with no
    # <CraftingPiece>, which guards the insertion path at the source.
    unheld = [p for p in dangling if p not in KNOWN_DANGLING]
    stale_dangling = sorted(set(KNOWN_DANGLING) - set(dangling))
    if unheld:
        status = 1
        print(f"\nFAIL: {len(unheld)} <AvailablePiece> id(s) with no <CraftingPiece> definition; "
              "fix the id or hold it in KNOWN_DANGLING:")
        for piece in unheld:
            print(f"  {piece}")
    held = [p for p in dangling if p in KNOWN_DANGLING]
    if held:
        print(f"\nHELD: {len(held)} dangling <AvailablePiece> id(s) in KNOWN_DANGLING:")
        for piece in held:
            print(f"  {piece}  [{KNOWN_DANGLING[piece]}]")
    if stale_dangling:
        status = 1
        print(f"\nFAIL: {len(stale_dangling)} KNOWN_DANGLING id(s) no longer dangle; delete them:")
        for piece in stale_dangling:
            print(f"  {piece}  [{KNOWN_DANGLING[piece]}]")

    if unresolved:
        status = 1
        print(f"\nFAIL: {len(unresolved)} roster weapon(s) resolve no usage, so the shield check "
              "cannot judge them:")
        for item_id in sorted(unresolved):
            print(f"  {item_id}: {unresolved[item_id]}")
        print("Register the pieces in a WeaponDescription the template lists, through a stylesheet "
              "a SubModule.xml <XmlName> row names, or fix the item's crafting_template.")

    def _display_path(path):
        """Repo-relative when possible, absolute otherwise.

        `os.path.relpath` RAISES `ValueError` on Windows when the two paths sit on
        different drives, so a bare call crashes the whole gate rather than printing
        one awkward path. That is reachable two ways: the tests build their fixture
        tree under the system temp dir (C:) while REPO_ROOT is on E:, and a real run
        can be pointed at a game install on another drive. Reporting a finding must
        never be the thing that fails.
        """
        try:
            return os.path.relpath(path, REPO_ROOT)
        except ValueError:
            return str(path)

    def show(rows):
        for troop, path, index, item_id, item_type, desc_id, usage in rows:
            rel = _display_path(path)
            print(f"  {troop} (roster {index}, {rel})")
            print(f"      {item_id} [{item_type}] -> {desc_id} -> {usage} [{BLOCKING_FLAG}]")

    if failing:
        status = 1
        print(f"\nFAIL: {len(failing)} roster(s) pair a shield with a weapon the AI will not use:")
        show(failing)
        print(
            "Fix a polearm by registering its pieces under a shield-compatible description "
            "(see tools/register_one_handed_polearms.py) or by changing the roster. A two-handed "
            "sword, axe or mace has no one-handed mode: change the roster, or hold the pair in "
            "KNOWN_FAILURES with its issue."
        )

    if known:
        print()
        print(f"KNOWN ({len(known)} roster(s)): pre-existing pairs held by the KNOWN_FAILURES "
              "ratchet. Not failing the run; each entry names its issue.")
        show(known)

    # OUTSIDE the `if known` block on purpose. The worst case for a ratchet is every entry going
    # stale at once, which produces an EMPTY `known` list -- gating the check on a non-empty one
    # would stay silent in exactly the case that matters most. A ratchet entry matching nothing is
    # a suppression that outlived its finding, and nothing else would ever prompt its deletion.
    stale = sorted(set(KNOWN_FAILURES) - {(row[0], row[3]) for row in known})
    # A pair occurring FEWER times than ratcheted is partly fixed: the entry overstates the debt
    # and would silently absorb a future regression back up to the old count.
    for key in sorted(k for k, n in counted.items() if n < KNOWN_FAILURES[k][0]):
        status = 1
        print()
        print(f"FAIL: ratchet entry {key} expects {KNOWN_FAILURES[key][0]} occurrence(s) but "
              f"{counted[key]} remain. Lower the count, or remove the entry if it is fixed.")
    if stale:
        status = 1
        print()
        print(f"FAIL: {len(stale)} stale KNOWN_FAILURES entr(y/ies) matched nothing. "
              "The pair was fixed or renamed -- delete the entry:")
        for owner, item_id in stale:
            print(f"  ({owner}, {item_id})  [{KNOWN_FAILURES[(owner, item_id)][1]}]")


    if not status:
        print("\nPASS: no shield roster carries a melee weapon its primary usage forbids.")
    return status


if __name__ == "__main__":
    raise SystemExit(main())
