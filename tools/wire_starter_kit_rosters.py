#!/usr/bin/env python3
"""Point the player-start rosters at the `starter_` twins, and override the six vanilla-mapped
cultures' starts with TAOM rosters.

TWO STEPS
---------
1. Rewire `taom_char_creation_equipment.xml` (the 256 `player_char_creation_{culture}_{title}_{m|f}`
   rosters, battle AND civilian sets) and `taom_career_starting_equipment.xml` (the 78
   `player_career_*` rosters): every Item0-3 / Body / Leg / Cape / Head / Gloves id becomes
   `Item.starter_<donor>` (a trailing `_starter` on the donor is stripped first). Horse and
   HorseHarness are left alone. The substitution is attribute-in-place, because the culture
   file puts every attribute on its own line and a re-emitted set would rewrite thousands of
   lines the change did not ask for. The id rule and the roster filter are imported from
   generate_starter_kit.py, so the two tools cannot disagree about which items exist.

2. Write `taom_player_start_vanilla_override.xml`: vlandia, empire, sturgia, aserai, battania
   and khuzait have no TAOM culture-default rosters, so their careerless start read vanilla's
   `SandBox/ModuleData/sandbox_equipment_sets.xml` and handed a Rohan player a Calradian sword
   in a hemp tunic. The override carries a roster with the same id as vanilla's for every
   title the culture's youth menu offers, built from the mapped culture's (rewired) career
   kit by title: hunter/skirmisher/bard take the ranged kit, guard/infantry the infantry kit,
   retainer the cavalry kit with its mount. Khand (battania) borrows Rhun's (khuzait) kit
   until it has its own (#571). The civilian set is the kit's sword, body and legs.

   The roster element carries `_replaceWhileMerging="true"`. MBObjectManager.MergeElements
   (1.4.8, lines 799-875) merges a later module's same-id element INTO the earlier one, and
   the only unique key EquipmentRosters.xsd declares is `EquipmentRoster/@id`: a set has no
   key, so a plain same-id roster would APPEND its sets and the adapter would keep taking
   vanilla's first one. With the attribute on the roster the engine drops every vanilla
   attribute and child first (lines 804-808, 829-832), so ours is the whole roster. The
   attribute is legal everywhere because the engine injects it into every schema complex
   type before validating (line 1092).

USAGE
-----
    python tools/wire_starter_kit_rosters.py            # dry run: counts + a sample
    python tools/wire_starter_kit_rosters.py --apply
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import OrderedDict
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import generate_starter_kit as gk  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
CC_FILE = MODULEDATA / "equipmentsets" / "taom_char_creation_equipment.xml"
CAREER_FILE = MODULEDATA / "equipmentsets" / "taom_career_starting_equipment.xml"
OVERRIDE_FILE = MODULEDATA / "equipmentsets" / "taom_player_start_vanilla_override.xml"
MENUS_DIR = MODULEDATA / "charactercreation"

VANILLA_CULTURES = ("vlandia", "empire", "sturgia", "aserai", "battania", "khuzait")
BORROW = {"battania": "khuzait"}   # Khand has no kit of its own (#571)
TITLE_TO_ARCHETYPE = {
    "hunter": "ranged", "skirmisher": "ranged", "bard": "ranged",
    "guard": "infantry", "infantry": "infantry", "mercenary": "infantry",
    "retainer": "cavalry",
}
# where each career archetype keeps its one-handed sword (taom_career_starting_equipment.xml header)
SWORD_SLOT = {"ranged": "Item2", "cavalry": "Item2", "infantry": "Item0"}

ROSTER_RE = re.compile(r"<EquipmentRoster\b([^>]*)>(.*?)</EquipmentRoster>", re.S)
EQUIPMENT_RE = re.compile(r"<Equipment\b[^>]*?/?>", re.S)
ID_ATTR_RE = re.compile(r'\bid="([^"]*)"')
SLOT_ATTR_RE = re.compile(r'\bslot="([^"]*)"')


class WireError(Exception):
    pass


def _is_target(roster_id: str) -> bool:
    return roster_id.startswith(gk.TARGET_PREFIXES) and not any(m in roster_id for m in gk.EXCLUDED_ROSTER_MARKERS)


def rewire_text(text: str) -> tuple[str, int, list[str]]:
    """(text, ids changed, roster ids touched). In place: only `id="Item.x"` values move."""
    changed = 0
    rosters: list[str] = []

    def roster_sub(match: re.Match) -> str:
        nonlocal changed
        attrs, body = match.group(1), match.group(2)
        id_match = ID_ATTR_RE.search(attrs)
        roster_id = id_match.group(1) if id_match else ""
        if not _is_target(roster_id):
            return match.group(0)
        count = 0

        def equipment_sub(em: re.Match) -> str:
            nonlocal count
            tag = em.group(0)
            slot = SLOT_ATTR_RE.search(tag)
            iid = ID_ATTR_RE.search(tag)
            if not slot or not iid or slot.group(1) not in gk.PLAYER_SLOTS:
                return tag
            item = iid.group(1)
            if not item.startswith("Item."):
                return tag
            bare = item[len("Item."):]
            if bare.startswith(gk.STARTER_PREFIX):
                return tag
            count += 1
            return tag[:iid.start(1)] + "Item." + gk.starter_id(bare) + tag[iid.end(1):]

        new_body = EQUIPMENT_RE.sub(equipment_sub, body)
        if count:
            changed += count
            rosters.append(roster_id)
        return f"<EquipmentRoster{attrs}>{new_body}</EquipmentRoster>"

    return ROSTER_RE.sub(roster_sub, text), changed, rosters


def leftovers(text: str) -> list[tuple[str, str, str]]:
    """(roster, slot, id) for every player-slot id in a target roster that is not a starter."""
    out = []
    root = ET.fromstring(text.encode("utf-8"))
    wanted = set(gk.target_roster_ids(root))
    for roster in root.iter("EquipmentRoster"):
        if roster.get("id") not in wanted:
            continue
        for eq in roster.iter("Equipment"):
            slot = eq.get("slot") or ""
            iid = (eq.get("id") or "").replace("Item.", "", 1)
            if slot in gk.PLAYER_SLOTS and not iid.startswith(gk.STARTER_PREFIX):
                out.append((roster.get("id"), slot, iid))
    return out


def titles_from_menus(menus_dir: Path, cultures=VANILLA_CULTURES) -> "OrderedDict[str, list[str]]":
    youth = json.loads((menus_dir / "youth_menu.json").read_text(encoding="utf-8"))
    titles: "OrderedDict[str, list[str]]" = OrderedDict((c, []) for c in cultures)
    for opt in youth:
        culture, title = opt.get("culture_id"), opt.get("title_type")
        if culture in titles and title and title not in titles[culture]:
            titles[culture].append(title)
    missing = [c for c, t in titles.items() if not t]
    if missing:
        raise WireError(f"youth_menu.json offers no title for {missing}")
    return titles


def build_vanilla_override(career_text: str, titles_by_culture, borrow: dict[str, str], eol: str = "\r\n") -> str:
    root = ET.fromstring(career_text.encode("utf-8"))
    career = {r.get("id"): r for r in root.iter("EquipmentRoster")}
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<!--",
        "  TAOM player-start rosters for the six vanilla-mapped cultures. GENERATED by",
        "  tools/wire_starter_kit_rosters.py (do not hand-edit; re-run the tool).",
        "",
        "  vlandia (Rohan), empire (Dunland), sturgia (Dale), aserai (Harad), khuzait (Rhun) and",
        "  battania (Khand) have no TAOM culture-default rosters, so their careerless start read",
        "  vanilla SandBox's rosters and handed out Calradian gear. Each roster here carries the",
        "  SAME id as vanilla's and _replaceWhileMerging=\"true\": MBObjectManager.MergeElements",
        "  (1.4.8, lines 799-875) then drops every vanilla attribute and child before adding ours,",
        "  which is the only shape that replaces rather than appends (EquipmentSet has no unique",
        "  key in EquipmentRosters.xsd). Battle set = the mapped culture's career kit by title",
        "  (hunter/skirmisher/bard ranged, guard/infantry infantry, retainer cavalry); civilian",
        "  set = its sword, body and legs. Khand borrows Rhun's kit until it has its own (#571).",
        "-->",
        "<EquipmentRosters>",
    ]
    for culture, titles in titles_by_culture.items():
        source = borrow.get(culture, culture)
        lines.append("")
        lines.append(f"    <!-- {culture}{' (borrows ' + source + ')' if source != culture else ''} -->")
        for title in titles:
            archetype = TITLE_TO_ARCHETYPE.get(title)
            if archetype is None:
                raise WireError(f"{culture}: youth title {title!r} has no archetype mapping")
            for sex in ("m", "f"):
                source_id = f"player_career_{source}_{archetype}_{sex}"
                roster = career.get(source_id)
                if roster is None:
                    raise WireError(f"{culture}/{title}/{sex}: career roster {source_id} is missing")
                battle = [s for s in roster.findall("EquipmentSet") if s.get("equipmentType") is None]
                if len(battle) != 1:
                    raise WireError(f"{source_id}: expected one battle EquipmentSet, found {len(battle)}")
                rows = [(e.get("slot"), e.get("id")) for e in battle[0].findall("Equipment")]
                by_slot = dict(rows)
                sword = by_slot.get(SWORD_SLOT[archetype])
                if not sword or not by_slot.get("Body") or not by_slot.get("Leg"):
                    raise WireError(f"{source_id}: needs a sword in {SWORD_SLOT[archetype]}, a Body and a Leg")
                rid = f"player_char_creation_{culture}_{title}_{sex}"
                lines.append(f'    <EquipmentRoster id="{rid}" culture="Culture.{culture}" _replaceWhileMerging="true">')
                lines.append("        <EquipmentSet>")
                for slot, iid in rows:
                    lines.append(f'            <Equipment slot="{slot}" id="{iid}" />')
                lines.append("        </EquipmentSet>")
                lines.append('        <EquipmentSet equipmentType="Civilian">')
                lines.append(f'            <Equipment slot="Item0" id="{sword}" />')
                lines.append(f'            <Equipment slot="Body" id="{by_slot["Body"]}" />')
                lines.append(f'            <Equipment slot="Leg" id="{by_slot["Leg"]}" />')
                lines.append("        </EquipmentSet>")
                lines.append("    </EquipmentRoster>")
    lines.append("")
    lines.append("</EquipmentRosters>")
    return eol.join(lines) + eol


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write (default: dry run)")
    ap.add_argument("--skip-override", action="store_true", help="rewire only; do not write the vanilla override")
    args = ap.parse_args()

    rc = 0
    for path in (CC_FILE, CAREER_FILE):
        text, had_bom = gk.read_xml(path)
        new_text, changed, rosters = rewire_text(text)
        left = leftovers(new_text)
        print(f"{path.name}: {changed} id(s) in {len(rosters)} roster(s) -> starter_; leftovers after: {len(left)}")
        for roster, slot, iid in left[:10]:
            print(f"   leftover {roster} {slot} {iid}")
        if changed and args.apply:
            err = gk.checked_write(path, new_text, had_bom, gk.BACKUP_TAG, backup=False)
            if err:
                print("ERROR:", err)
                return 2
            print(f"   wrote {path}")

    if not args.skip_override:
        career_text = gk.read_xml(CAREER_FILE)[0]
        career_text = rewire_text(career_text)[0]   # the override must see the rewired kit
        try:
            titles = titles_from_menus(MENUS_DIR)
            override = build_vanilla_override(career_text, titles, BORROW)
        except WireError as exc:
            print("ERROR:", exc)
            return 2
        count = override.count("<EquipmentRoster ")
        if OVERRIDE_FILE.exists() and OVERRIDE_FILE.read_bytes() == override.encode("utf-8"):
            print(f"{OVERRIDE_FILE.name}: {count} rosters, unchanged")
        elif args.apply:
            err = gk.checked_write(OVERRIDE_FILE, override, False, gk.BACKUP_TAG, backup=False)
            if err:
                print("ERROR:", err)
                return 2
            print(f"{OVERRIDE_FILE.name}: {count} rosters, wrote {OVERRIDE_FILE}")
        else:
            print(f"{OVERRIDE_FILE.name}: {count} rosters (dry run)")
            print("\n".join(override.splitlines()[17:33]))
    if not args.apply:
        print("\n(dry run; pass --apply to write)")
    else:
        print("\nRegister the override in Main/_Module/SubModule.xml if it is new, run validate_moduledata.py,")
        print("then RESTART Bannerlord and start a NEW campaign before believing anything.")
    return rc


if __name__ == "__main__":
    sys.exit(main())
