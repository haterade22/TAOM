#!/usr/bin/env python3
"""Derive every career starting kit from the troops that culture actually fields (#629).

A career kit is the gear the culture's LOWEST troops carry, so a career never starts better
equipped than the regular troops it recruits. For each of the five kit slots (Item0-Item2, Body,
Leg) of each `player_career_{culture}_{archetype}_{m|f}` roster:

1. candidates are the items of that slot's class that a non-hero troop of the culture carries in
   a battle set (`troops/troops_*.xml`: inline `EquipmentRoster`s that are not `civilian="true"`,
   plus direct `<Equipments>/<equipment>` overrides, which the engine applies to every set);
2. a bow first keeps only the candidates with the lowest `difficulty` (the Bow skill needed to
   re-equip it), so a new character can put it back on;
3. the lowest-level NON-VANILLA candidate first carried at level LEVEL_CAP or below wins;
   otherwise the lowest-level candidate at all (possibly vanilla). Ties: most carried, then id.

An item is vanilla when its mesh, or any crafting piece's mesh, is one vanilla SandBoxCore,
Native or SandBox items or pieces use. Horse and HorseHarness are never touched. The file's
comments, formatting and line endings are kept: only the five slot ids change.

USAGE
-----
    python tools/generate_career_kits.py            # dry run: what would change
    python tools/generate_career_kits.py --apply    # write the career file
    python tools/generate_career_kits.py --verify   # exit 1 if the career file drifts from the rule

After --apply, run `python tools/wire_starter_kit_rosters.py --apply`: the six vanilla-mapped
cultures' careerless rosters are built from this kit. Needs the game install (item classes,
meshes); without it the run stops rather than guessing.
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path
from typing import NamedTuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import generate_starter_kit as gk  # noqa: E402

CAREER_FILE = gk.DEFAULT_MODULEDATA / "equipmentsets" / "taom_career_starting_equipment.xml"
TROOPS_DIR = gk.DEFAULT_MODULEDATA / "troops"
DEFAULT_ARMORY = gk.DEFAULT_ARMORY
VANILLA_MODULES = ("SandBoxCore", "Native", "SandBox")
LEVEL_CAP = 21
SLOTS = ("Item0", "Item1", "Item2", "Body", "Leg")

# The kit shape per archetype; SIDEARM and SECOND resolve per culture below.
LAYOUT = {
    "ranged": ("Bow", "Arrows", "SIDEARM"),
    "cavalry": ("TwoHandedPolearm", "Shield", "SIDEARM"),
    "infantry": ("SIDEARM", "Shield", "SECOND"),
}
SIDEARM = {"erebor": "OneHandedAxe",   # dwarves carry axes
           "empire": "OneHandedAxe"}   # Dunland has no non-vanilla one-handed sword
SECOND = {"dolguldur": "TwoHandedAxe"}

ROSTER_RE = re.compile(r'(<EquipmentRoster id="player_career_([a-z]+)_(ranged|cavalry|infantry)_[mf]"[^>]*>)(.*?)(</EquipmentRoster>)', re.S)
EQ_RE = re.compile(r'(<Equipment slot="(Item0|Item1|Item2|Body|Leg)" id="Item\.)([^"]+)(")')


class CareerKitError(Exception):
    """A roster the rule cannot fill; never paper over it with the old item."""


class Candidate(NamedTuple):
    item: str
    level: int      # the lowest level of any troop that carries it
    count: int      # how many equipment entries name it
    vanilla: bool
    difficulty: int


def pick(candidates: list[Candidate], is_bow: bool = False) -> str | None:
    if not candidates:
        return None
    if is_bow:
        lowest = min(c.difficulty for c in candidates)
        candidates = [c for c in candidates if c.difficulty == lowest]
    order = lambda c: (c.level, -c.count, c.item)  # noqa: E731
    own = [c for c in candidates if not c.vanilla and c.level <= LEVEL_CAP]
    return min(own or candidates, key=order).item


def slot_classes(culture: str, archetype: str) -> dict[str, str]:
    resolve = {"SIDEARM": SIDEARM.get(culture, "OneHandedSword"), "SECOND": SECOND.get(culture, "TwoHandedPolearm")}
    weapons = [resolve.get(k, k) for k in LAYOUT[archetype]]
    return {"Item0": weapons[0], "Item1": weapons[1], "Item2": weapons[2], "Body": "BodyArmor", "Leg": "LegArmor"}


def _bare(value: str | None) -> str:
    value = value or ""
    return value.split(".", 1)[1] if "." in value else value


def index_troop_gear(roots: list[ET.Element]) -> dict[str, dict[str, tuple[int, int]]]:
    """culture -> item -> (lowest carrier level, entries naming it), battle gear of non-hero troops."""
    found: dict[str, dict[str, list[int]]] = defaultdict(dict)
    for root in roots:
        for npc in root.iter("NPCCharacter"):
            if npc.get("is_hero") == "true":
                continue
            culture = _bare(npc.get("culture"))
            level = int(npc.get("level") or 0)
            equipments = npc.find("Equipments")
            if not culture or equipments is None:
                continue
            entries = list(equipments.findall("equipment"))
            for roster in equipments.findall("EquipmentRoster"):
                if (roster.get("civilian") or "").lower() != "true":
                    entries += roster.findall("equipment")
            for eq in entries:
                item = _bare(eq.get("id"))
                if not item:
                    continue
                seen = found[culture].setdefault(item, [level, 0])
                seen[0] = min(seen[0], level)
                seen[1] += 1
    return {c: {i: (v[0], v[1]) for i, v in items.items()} for c, items in found.items()}


class Sources(NamedTuple):
    troops: dict[str, dict[str, tuple[int, int]]]
    items: dict[str, ET.Element]
    pieces: dict[str, ET.Element]
    vanilla_meshes: set[str]
    rosters: list[tuple[str, str]]   # (culture, archetype) pairs the career file carries
    failures: list[str]              # Armory files that did not parse: their items are invisible


def _scan(files, failures: list[str] | None = None) -> tuple[dict[str, ET.Element], dict[str, ET.Element], set[str]]:
    """Items, pieces and meshes over `files`. A file that does not parse is recorded in
    `failures` when given, because its items would otherwise just drop out of the candidates."""
    items, pieces, meshes = {}, {}, set()
    for path in files:
        try:
            root = ET.parse(path).getroot()
        except (ET.ParseError, OSError) as exc:
            if failures is not None:
                failures.append(f"{path}: not readable, its items are invisible to this run ({exc})")
            continue
        for el in root.iter():
            iid = el.get("id")
            if el.get("mesh") and el.tag in ("Item", "CraftedItem", "CraftingPiece"):
                meshes.add(el.get("mesh"))
            if not iid:
                continue
            if el.tag in ("Item", "CraftedItem"):
                items.setdefault(iid, el)
            elif el.tag == "CraftingPiece":
                pieces.setdefault(iid, el)
    return items, pieces, meshes


def load_sources(armory: Path = DEFAULT_ARMORY, modules: Path = gk.DEFAULT_MODULES,
                 career_file: Path = CAREER_FILE, troops_dir: Path = TROOPS_DIR) -> Sources:
    armory_md = Path(armory) / "ModuleData"
    if not armory_md.exists():
        raise CareerKitError(f"{armory_md} not found; the rule needs the install's item classes and meshes")
    vanilla_files = []
    for module in VANILLA_MODULES:
        vanilla_files += glob.glob(str(Path(modules) / module / "ModuleData" / "**" / "*.xml"), recursive=True)
    v_items, v_pieces, v_meshes = _scan(vanilla_files)   # vanilla ships some non-item XML; tolerate it
    failures: list[str] = []
    a_items, a_pieces, _ = _scan(gk._armory_item_files(armory_md) + [armory_md / gk.PIECES_FILE], failures)
    items = {**v_items, **a_items}
    pieces = {**v_pieces, **a_pieces}
    troops = index_troop_gear([ET.parse(p).getroot() for p in sorted(Path(troops_dir).glob("troops_*.xml"))])
    text = gk.read_xml(career_file)[0]
    rosters = sorted({(m.group(2), m.group(3)) for m in ROSTER_RE.finditer(text)})
    return Sources(troops, items, pieces, v_meshes, rosters, failures)


def _item_class(el: ET.Element) -> str | None:
    return el.get("crafting_template") if el.tag == "CraftedItem" else el.get("Type")


def _is_vanilla(el: ET.Element, sources: Sources) -> bool:
    if el.tag == "Item":
        return el.get("mesh") in sources.vanilla_meshes
    for piece in el.iter("Piece"):
        found = sources.pieces.get(piece.get("id"))
        if found is not None and found.get("mesh") in sources.vanilla_meshes:
            return True
    return False


def candidates(sources: Sources, culture: str, klass: str) -> list[Candidate]:
    out = []
    for item, (level, count) in sources.troops.get(culture, {}).items():
        el = sources.items.get(item)
        if el is None or _item_class(el) != klass:
            continue
        out.append(Candidate(item, level, count, _is_vanilla(el, sources), int(el.get("difficulty") or 0)))
    return out


def derive_kits(sources: Sources) -> dict[tuple[str, str], dict[str, str | None]]:
    kits = {}
    for culture, archetype in sources.rosters:
        kits[(culture, archetype)] = {
            slot: pick(candidates(sources, culture, klass), is_bow=klass == "Bow")
            for slot, klass in slot_classes(culture, archetype).items()}
    return kits


def apply_kits(text: str, kits: dict[tuple[str, str], dict[str, str | None]]) -> tuple[str, int]:
    """(new text, ids changed). Only the five slot ids move; a roster with no kit, a slot with no
    pick, or a roster missing one of the five slots is an error."""
    changed = 0

    def roster_sub(m: re.Match) -> str:
        nonlocal changed
        culture, archetype = m.group(2), m.group(3)
        kit = kits.get((culture, archetype))
        if kit is None:
            raise CareerKitError(f"no kit derived for {culture}/{archetype}")
        seen = set()

        def eq_sub(em: re.Match) -> str:
            nonlocal changed
            slot = em.group(2)
            new = kit.get(slot)
            if not new:
                raise CareerKitError(f"{culture}/{archetype} {slot}: no troop of the culture carries a "
                                     f"{slot_classes(culture, archetype)[slot]}")
            seen.add(slot)
            if em.group(3) != new:
                changed += 1
            return em.group(1) + new + em.group(4)

        body = EQ_RE.sub(eq_sub, m.group(4))
        missing = set(SLOTS) - seen
        if missing:
            raise CareerKitError(f"{culture}/{archetype}: roster lacks slot(s) {sorted(missing)}")
        return m.group(1) + body + m.group(5)

    return ROSTER_RE.sub(roster_sub, text), changed


def verify(career_file: Path, kits) -> list[str]:
    """One line per roster slot whose id differs from the rule's pick."""
    drift = []
    root = ET.fromstring(gk.read_xml(career_file)[0].encode("utf-8"))
    for roster in root.iter("EquipmentRoster"):
        m = re.fullmatch(r"player_career_([a-z]+)_(ranged|cavalry|infantry)_[mf]", roster.get("id") or "")
        if not m:
            continue
        kit = kits.get((m.group(1), m.group(2)), {})
        for eq in roster.iter("Equipment"):
            slot = eq.get("slot")
            if slot in SLOTS and _bare(eq.get("id")) != kit.get(slot):
                drift.append(f"{roster.get('id')} {slot}: file {_bare(eq.get('id'))}, rule {kit.get(slot)}")
    return drift


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the career file (default: dry run)")
    ap.add_argument("--verify", action="store_true", help="exit 1 if the career file drifts from the rule")
    ap.add_argument("--armory-path", default=str(DEFAULT_ARMORY))
    args = ap.parse_args()
    try:
        sources = load_sources(Path(args.armory_path))
        for line in sources.failures:
            print("WARNING:", line)
        kits = derive_kits(sources)
        text, had_bom = gk.read_xml(CAREER_FILE)
        new_text, changed = apply_kits(text, kits)
    except CareerKitError as exc:
        print("ERROR:", exc)
        return 2
    drift = verify(CAREER_FILE, kits)
    for line in drift:
        print(("DRIFT: " if args.verify else "change: ") + line)
    if args.verify:
        print("OK: career kits match the rule" if not drift else f"FAIL: {len(drift)} slot(s) drift")
        return 1 if drift else 0
    if not changed:
        print(f"{CAREER_FILE.name}: {len(sources.rosters)} kits, unchanged")
        return 0
    if not args.apply:
        print(f"\n{changed} id(s) would change (dry run; pass --apply to write)")
        return 0
    err = gk.checked_write(CAREER_FILE, new_text, had_bom, gk.BACKUP_TAG, backup=False)
    if err:
        print("ERROR:", err)
        return 2
    print(f"wrote {CAREER_FILE} ({changed} id(s)); now run python tools/wire_starter_kit_rosters.py --apply")
    return 0


if __name__ == "__main__":
    sys.exit(main())
