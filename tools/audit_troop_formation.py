#!/usr/bin/env python3
"""Audit every TAOM troop's default_group against the gear its battle sets carry.

For a non-hero troop, BasicCharacterObject.GetFormationClass() returns DefaultFormationClass, so
default_group IS the battlefield formation the troop spawns in. A mounted bowman tagged Infantry
spawns in the infantry block on horseback; a foot archer tagged Cavalry rides with the horse.

Battle sets mix per slot (see .claude/rules/troops.md), so a slot is "always" filled only when every
battle set fills it. The expected group comes from what every set carries:

    mount in every set + bow/crossbow in every set  -> HorseArcher
    mount in every set                              -> Cavalry
    bow/crossbow in every set, no mount             -> Ranged
    otherwise                                       -> Infantry

Throwing weapons do not make a troop Ranged or HorseArcher: IsRanged comes from default_group alone,
so a javelin troop tagged Ranged is placed and commanded as a backline archer while it holds a
frontline kit (the Harondor skirmisher fix, CHANGELOG-2026-H2-handwritten). A mount or launcher in
only SOME sets cannot be expressed by one group: those troops are reported as MIXED for a human
decision. faction_ui_picker_characters.xml holds portrait dummies that never fight and is skipped.

Read-only. Item types come from the repo's own ModuleData plus the installed item modules.

Usage: python tools/audit_troop_formation.py [--csv out.csv] [--all]
"""
from __future__ import annotations

import argparse
import csv
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _gamedir import game_modules  # noqa: E402

REPO = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO / "Main" / "_Module" / "ModuleData"
ITEM_MODULES = ("Native", "SandBoxCore", "SandBox", "LOTRLOME_Armory", "TA.Weapons_and_Shields",
                "OpenSourceArmoryRBM", "OpenSourceWeaponryRBM", "OpenSourceSaddleryRBM", "TAOM_Map")

LAUNCHER_TYPES = {"Bow", "Crossbow", "Musket", "Pistol"}
THROWN_TYPES = {"Thrown"}
THROWN_TEMPLATE_RE = re.compile(r"javelin|throwing", re.I)
WEAPON_SLOTS = {"Item0", "Item1", "Item2", "Item3"}
SKIP_FILES = {"faction_ui_picker_characters.xml"}


def _strip_ns(tag: str) -> str:
    return tag.split("}")[-1]


def build_item_index(modules: Path) -> dict[str, str]:
    """item id -> kind: 'launcher', 'thrown', 'shield', 'horse' or 'other'."""
    roots = [MODULE_DATA] + [modules / m / "ModuleData" for m in ITEM_MODULES]
    index: dict[str, str] = {}
    for root in roots:
        if not root.is_dir():
            continue
        for path in root.rglob("*.xml"):
            if "Languages" in path.parts:
                continue
            try:
                text = path.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            if "<Item" not in text and "<CraftedItem" not in text:
                continue
            for m in re.finditer(r"<(Item|CraftedItem)\b([^>]*)>", text):
                attrs = dict(re.findall(r'(\w+)="([^"]*)"', m.group(2)))
                iid = attrs.get("id")
                if not iid:
                    continue
                if m.group(1) == "CraftedItem":
                    kind = "thrown" if THROWN_TEMPLATE_RE.search(attrs.get("crafting_template", "")) else "other"
                else:
                    t = attrs.get("Type") or attrs.get("type") or ""
                    kind = ("launcher" if t in LAUNCHER_TYPES else "thrown" if t in THROWN_TYPES
                            else "shield" if t == "Shield" else "horse" if t == "Horse" else "other")
                index.setdefault(iid, kind)
    return index


def _is_civilian(elem: ET.Element) -> bool:
    """True for anything that is not a battle set: equipmentType other than Battle (Civilian,
    Stealth), or the legacy civilian="true"."""
    etype = elem.get("equipmentType")
    if etype is not None:
        return etype != "Battle"
    return (elem.get("civilian") or "").lower() == "true"


def _items_of(set_elem: ET.Element, keep_empty: bool = False) -> dict[str, str]:
    """slot -> item id. keep_empty keeps an empty id, which as a troop-level override clears the slot."""
    out = {}
    for eq in set_elem:
        # Troop rosters write <equipment>, standalone roster files <Equipment>. Inside a set the engine reads
        # every child whatever its name (Equipment.cs:196-216); a troop-level override must be exactly
        # <equipment> (BasicCharacterObject.cs:370), and no shipped override is spelled otherwise.
        if not isinstance(eq.tag, str) or _strip_ns(eq.tag).lower() != "equipment":
            continue
        slot, ref = eq.get("slot", ""), eq.get("id", "")
        if slot and (ref or keep_empty):
            out[slot] = ref.split(".", 1)[-1] if ref else ""
    return out


def _overlay(base: dict[str, str], overrides: dict[str, str]) -> dict[str, str]:
    return {slot: ref for slot, ref in {**base, **overrides}.items() if ref}


def load_standalone_rosters() -> dict[str, list[dict[str, str]]]:
    """EquipmentRoster id -> its battle sets (for <EquipmentSet id=.../> references)."""
    rosters: dict[str, list[dict[str, str]]] = {}
    for path in MODULE_DATA.rglob("*.xml"):
        if "Languages" in path.parts:
            continue
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for r in root.iter():
            if _strip_ns(r.tag) != "EquipmentRoster" or not r.get("id"):
                continue
            sets = [s for s in r if _strip_ns(s.tag) == "EquipmentSet" and not _is_civilian(s)]
            rosters[r.get("id")] = [_items_of(s) for s in sets]
    return rosters


def battle_sets(npc: ET.Element, rosters) -> list[dict[str, str]]:
    """Battle sets as the engine builds them. A loose <equipment> directly under <Equipments> is
    applied over every set (BasicCharacterObject.Deserialize -> AddOverriddenEquipments), which is
    how most TAOM cavalry carry their horse."""
    sets = []
    for eqs in npc:
        if _strip_ns(eqs.tag) not in ("Equipments", "equipments"):
            continue
        overrides = _items_of(eqs, keep_empty=True)
        for child in eqs:
            name = _strip_ns(child.tag)
            if name in ("EquipmentRoster", "equipmentRoster") and not _is_civilian(child):
                sets.append(_overlay(_items_of(child), overrides))
            elif name in ("EquipmentSet", "equipmentSet") and not _is_civilian(child):
                sets.extend(_overlay(s, overrides)
                            for s in rosters.get(child.get("id", "").split(".", 1)[-1], []))
    return sets


def classify(sets, index) -> dict:
    def kinds(s):
        return {index.get(ref, "unknown") for slot, ref in s.items() if slot in WEAPON_SLOTS}

    def has(s, kind):
        return kind in kinds(s)

    def mounted(s):
        ref = s.get("Horse")
        return bool(ref) and index.get(ref, "horse") in ("horse", "unknown", "other")

    n = len(sets)
    mount_n = sum(mounted(s) for s in sets)
    launch_n = sum(has(s, "launcher") for s in sets)
    thrown_n = sum(has(s, "thrown") for s in sets)
    unknown = sorted({ref for s in sets for slot, ref in s.items()
                      if slot in WEAPON_SLOTS and index.get(ref) is None})
    return {"sets": n, "mount": mount_n, "launcher": launch_n, "thrown": thrown_n, "unknown": unknown}


def verdict(group: str, c: dict) -> tuple[str, str]:
    """(status, expected) where status is OK, MISMATCH, MIXED, THROWING or NOSETS."""
    n = c["sets"]
    if n == 0:
        return "NOSETS", ""
    mixed_mount = 0 < c["mount"] < n
    mixed_launch = 0 < c["launcher"] < n
    all_mount = c["mount"] == n
    all_launch = c["launcher"] == n
    if mixed_mount or mixed_launch:
        return "MIXED", ""
    if all_mount:
        exp = {"HorseArcher"} if all_launch else {"Cavalry"}
    elif all_launch:
        exp = {"Ranged"}
    else:
        exp = {"Infantry"}
    return ("OK" if group in exp else "MISMATCH"), "/".join(sorted(exp))


def scan(modules: Path):
    index = build_item_index(modules)
    rosters = load_standalone_rosters()
    troops = {}
    for path in sorted(MODULE_DATA.rglob("*.xml")):
        if "Languages" in path.parts or path.name in SKIP_FILES:
            continue
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for npc in root.iter():
            if _strip_ns(npc.tag) != "NPCCharacter":
                continue
            a = npc.attrib
            if a.get("is_hero") == "true" or a.get("is_template") == "true":
                continue
            if a.get("occupation") in ("Lord", "Wanderer", "Artisan", "Merchant", "GangLeader",
                                       "Preacher", "Headman", "RuralNotable"):
                continue
            sets = battle_sets(npc, rosters)
            c = classify(sets, index)
            group = a.get("default_group", "Infantry")
            status, expected = verdict(group, c)
            ups = [u.get("id", "").split(".", 1)[-1] for u in npc.iter()
                   if _strip_ns(u.tag) == "upgrade_target"]
            troops[a.get("id")] = dict(id=a.get("id"), file=str(path.relative_to(REPO)),
                                       group=group, race=a.get("race", ""),
                                       occupation=a.get("occupation", ""), level=a.get("level", ""),
                                       status=status, expected=expected, ups=ups, **c)
    return troops


def family(group: str) -> str:
    return {"Cavalry": "mounted", "HorseArcher": "mounted", "Ranged": "ranged"}.get(group, "foot")


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--csv", help="write every troop to this CSV")
    p.add_argument("--all", action="store_true", help="print OK troops too")
    args = p.parse_args(argv)
    modules = game_modules(None)
    if modules is None or not Path(modules).is_dir():
        print("Set BANNERLORD_GAME_DIR to the Bannerlord install.", file=sys.stderr)
        return 2
    troops = scan(Path(modules))
    counts: dict[str, int] = {}
    for t in troops.values():
        counts[t["status"]] = counts.get(t["status"], 0) + 1
    print(f"{len(troops)} troops: " + ", ".join(f"{k} {v}" for k, v in sorted(counts.items())))
    for t in sorted(troops.values(), key=lambda t: (t["status"], t["file"], t["id"])):
        if t["status"] == "OK" and not args.all:
            continue
        print(f"{t['status']:8} {t['id']:45} {t['group']:11} exp={t['expected']:15} sets={t['sets']} "
              f"mount={t['mount']} launch={t['launcher']} thrown={t['thrown']} "
              f"race={t['race'] or '-'} {t['file']}" + (f" unknown={t['unknown']}" if t["unknown"] else ""))
    print("\nUpgrade edges that change family (foot / ranged / mounted):")
    for t in sorted(troops.values(), key=lambda t: t["id"]):
        for u in t["ups"]:
            if u in troops and family(troops[u]["group"]) != family(t["group"]):
                print(f"  {t['id']} ({t['group']}) -> {u} ({troops[u]['group']})")
    if args.csv:
        with open(args.csv, "w", newline="", encoding="utf-8") as fh:
            w = csv.DictWriter(fh, fieldnames=list(next(iter(troops.values())).keys()))
            w.writeheader()
            for t in troops.values():
                w.writerow({**t, "ups": " ".join(t["ups"]), "unknown": " ".join(t["unknown"])})
    return 1 if counts.get("MISMATCH") or counts.get("MIXED") else 0


if __name__ == "__main__":
    sys.exit(main())
