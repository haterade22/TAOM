#!/usr/bin/env python3
"""Fail when a troop carries a shield beside a polearm the combat AI will refuse to use.

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
1. Every equipment roster that pairs a shield with a polearm whose PRIMARY usage set is flagged
   `requires_no_shield`. Any culture, crafted or plain-`<Item>`.
2. Every `<AvailablePiece>` id in the merged weapon descriptions resolves to a real
   `<CraftingPiece>`. A typo'd id in an XSLT fails silently in game -- the same class of hazard
   `validate_mesh_refs.py` and `audit_item_refs.py` exist to catch elsewhere.

Item and description data come from the INSTALLED modules (that is where LOTRLOME_Armory lives
and it is not in this repo); rosters come from this repo, because a commit is what changes them.

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

# Polearms are the class this tool was written for and they are clean as of 2026-08-10, so they
# ratchet: any new one fails the build. Two-handed swords/axes/maces hit the identical engine rule
# and 33 rosters across 13 troops are already in that state (#450) -- pre-existing, and a roster
# decision rather than a data registration, because a 2H axe has no one-handed mode to register.
# They are reported in full every run rather than filtered out, because a gate that silently drops
# what it cannot fix reads as "all clear". Pass --strict to fail on them too; once #450 is closed,
# move "TwoHandedWeapon" into this set so it ratchets as well.
FAILING_TYPES = frozenset({"Polearm"})


def _parse(path: Path):
    return ET.parse(str(path), ET.XMLParser(recover=True, huge_tree=True))


def merged(modules: Path, name: str):
    """Native's XML with every module's same-named XSLT chained onto it, as the engine does.

    `MBObjectManager.CreateMergedXmlFile` applies each contributing module's transform in load
    order. TAOM's contributors here are all additive (each override template ends by applying
    templates to the node it replaced), so a sorted order gives the same document as load order.
    """
    base = modules / "Native" / "ModuleData" / f"{name}.xml"
    if not base.is_file():
        return None
    doc = _parse(base)
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
        sheet = module / "ModuleData" / f"{name}.xslt"
        if sheet.is_file():
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


def crafting_pieces(modules: Path) -> dict[str, set[str]]:
    """piece id -> the usage-feature tokens that piece excludes."""
    out: dict[str, set[str]] = {}
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
        data = module / "ModuleData"
        if not data.is_dir():
            continue
        for path in list(data.glob("*crafting_pieces*.xml")) + list(data.glob("*/*crafting_pieces*.xml")):
            for node in _parse(path).getroot().iter("CraftingPiece"):
                excluded = node.get("excluded_item_usage_features") or ""
                out[node.get("id")] = {t for t in excluded.split(":") if t}
    return out


def descriptions(modules: Path):
    """description id -> (available piece ids, its item_usage_features tokens)."""
    doc = merged(modules, "weapon_descriptions")
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


def templates(modules: Path) -> dict[str, tuple[str, list[str]]]:
    """crafting template id -> (item_type, its WeaponDescription ids in order).

    The order is what decides the primary usage, so it must be preserved verbatim.
    """
    doc = merged(modules, "crafting_templates")
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


def items(modules: Path):
    """(crafted, plain, shields).

    crafted: id -> (template, pieces).  plain: id -> (Type, item_usage).
    """
    crafted: dict[str, tuple[str, list[str]]] = {}
    plain: dict[str, tuple[str, str]] = {}
    shields: set[str] = set()
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
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
                for e in roster.iter("equipment")
                if e.get("id")
            ]
            if not ids:
                continue
            owner, node = path.stem, roster.getparent()
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
    parser.add_argument(
        "--strict", action="store_true", help="fail on every melee type, not just %s" % "/".join(sorted(FAILING_TYPES))
    )
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

    descs = descriptions(modules)
    templs = templates(modules)
    pieces_meta = crafting_pieces(modules)
    crafted, plain, shields = items(modules)
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

    failing, advisory = [], []
    for troop, path, index, ids in rosters(roster_root):
        if not any(i in shields for i in ids):
            continue
        for item_id in ids:
            if item_id not in blocking:
                continue
            item_type, desc_id, usage = blocking[item_id]
            row = (troop, path, index, item_id, item_type, desc_id, usage)
            (failing if item_type in FAILING_TYPES or args.strict else advisory).append(row)

    print(f"Modules:  {modules}")
    print(f"Rosters:  {roster_root}")
    print(
        f"Loaded {len(descs)} weapon descriptions, {len(templs)} crafting templates, "
        f"{len(crafted)} crafted items, {len(shields)} shields."
    )
    print(f"Melee weapons whose primary usage is {BLOCKING_FLAG}: {len(blocking)}")

    status = 0
    if dangling:
        # Advisory unless --strict: one of these (`vlandian_blade_10`, 2026-08-10) is vanilla's
        # own, so failing on the set would leave a gate nobody can turn green. The insertion path
        # that matters is guarded at the source -- register_dale_spear_descriptions.py refuses to
        # write a piece id with no <CraftingPiece>.
        if args.strict:
            status = 1
        label = "FAIL" if args.strict else "WARN"
        print(f"\n{label}: {len(dangling)} <AvailablePiece> id(s) with no <CraftingPiece> definition:")
        for piece in dangling:
            print(f"  {piece}")

    def show(rows):
        for troop, path, index, item_id, item_type, desc_id, usage in rows:
            rel = os.path.relpath(path, REPO_ROOT)
            print(f"  {troop} (roster {index}, {rel})")
            print(f"      {item_id} [{item_type}] -> {desc_id} -> {usage} [{BLOCKING_FLAG}]")

    if failing:
        status = 1
        print(f"\nFAIL: {len(failing)} roster(s) pair a shield with a weapon the AI will not use:")
        show(failing)
        print(
            "\nFix by registering the item's pieces under a shield-compatible description "
            "(see tools/register_dale_spear_descriptions.py) or by changing the roster."
        )

    if advisory:
        troops = len({row[0] for row in advisory})
        print(
            f"\nWARN ({len(advisory)} roster(s), {troops} troop(s)): the same engine rule, on "
            "weapon types outside this gate's ratchet. Not failing the run; tracked separately."
        )
        show(advisory)

    if not status:
        print(f"\nPASS: no shield roster carries a {'/'.join(sorted(FAILING_TYPES))} its primary usage forbids.")
    return status


if __name__ == "__main__":
    raise SystemExit(main())
