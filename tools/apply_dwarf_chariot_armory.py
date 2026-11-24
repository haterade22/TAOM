#!/usr/bin/env python3
"""Hold the dwarven war chariot's rows in the LIVE LOTRLOME_Armory: three items and their English names.

    python tools/apply_dwarf_chariot_armory.py            # dry run: prints what would change, writes nothing
    python tools/apply_dwarf_chariot_armory.py --check    # exit 1 when the Armory differs from this file
    python tools/apply_dwarf_chariot_armory.py --apply    # Modding Kit and game CLOSED
    python tools/apply_dwarf_chariot_armory.py --remove   # take out exactly the rows this file writes

The Armory is unversioned, so a reinstall loses these rows while troops_erebor.xml still names the items. This file
is their record (docs/features/chariot.md "Dwarven war chariot"), and --check is the gate.

  1. ModuleData/LOTRLOME_items/LOTRAOM_horses.xml: ITEMS_BLOCK after the item taom_chariot_armour_imperial. The Horse
     row copies taom_chariot_a without its skeleton_scale: the engine applies a scale by bone index of horse_skeleton,
     and on horse_skeleton_chariot those indices are the rig root and bones of one animal only.
  2. ModuleData/Languages/loc_LOTRAOM_horses.xml (the root English file only): one row per item after taom_chariot_a.

An item row that names a mesh with no cooked package can hang the game at load. So the rows go in, and count as
right, only while each mesh has its package <mesh>_geo.tpac with a RuntimeDataCache entry and each material its
<material>_mtl.tpac (a material package has no cache entry). The file name must be exact: dwarf_war_chariot_b_geo.tpac
holds the bytes "dwarf_war_chariot" too. The Modding Kit writes those packages from AssetSources/creature/chariot.

Byte-faithful: BOM and line endings kept, every changed file parsed before anything is written, a write-once backup
with a non-.xml suffix (.bak-dwarfchariot-<YYYYMMDD-HHMMSS>). A block that is in the file with other bytes than this
file's is DRIFT: --check fails, and --apply and --remove refuse, because a person changed it and must decide. --remove
takes out only this file's bytes, so an edit made after the apply stays. Rows that are in a file two times are DRIFT
too. Exit 1 on drift, a missing anchor, a missing or doubled package or rows partly present; exit 2 when the Armory
is not there or the game or the Kit runs; exit 3 when an input cannot be read (the codes of tools/data_gates.py).
--game-dir points the script at a synthetic tree (tools/tests/test_apply_dwarf_chariot_armory.py).
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import ensure_exists, game_dir, game_or_kit_running  # noqa: E402
from apply_animalia_armory import nl_of, read, write  # noqa: E402
from check_rdc_entries import package_guid  # noqa: E402

_DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
BACKUP_PREFIX = ".bak-dwarfchariot-"
ITEM_ANCHOR = "taom_chariot_armour_imperial"
LOC_ANCHOR = "taom_chariot_a"
ITEM_NAMES = {
    "taom_ram_chariot_a": "Dwarven War Chariot",
    "taom_chariot_armour_dwarf": "[Erebor] War Chariot Plating",
    "taom_chariot_armour_dwarf_b": "[Erebor] War Chariot Steel Plating",
}
MESHES = ("dwarf_chariot_rams", "dwarf_war_chariot", "dwarf_war_chariot_b")
MATERIALS = ("m_eb_ram_a1", "dwarf_war_chariot_iron", "dwarf_war_chariot_wood", "m_eb_ram_barding_a1",
             "m_eb_ram_barding_a2")

ITEMS_BLOCK = '''    <!-- Dwarven war chariot. Two war rams (the Horse item) pull an iron-plated cart; the cart and the rams' harness
         are one HorseHarness mesh. Both meshes are on horse_skeleton_chariot and use Monster.chariot, as the
         Wainrider chariot above; the Horse row copies its values. family_type 4 is the chariot's. The rams carry
         no mane mesh, so mane_cover_type is inert. Sources: AssetSources/creature/chariot. -->
    <Item
        id="taom_ram_chariot_a"
        name="{=taom_ram_chariot_a}Dwarven War Chariot"
        mesh="dwarf_chariot_rams"
        subtype="horse"
        item_category="war_horse"
        weight="370"
        difficulty="120"
        is_merchandise="false"
        Type="Horse">
        <ItemComponent>
            <Horse
                monster="Monster.chariot"
                maneuver="25"
                speed="70"
                charge_damage="80"
                body_length="100"
                is_mountable="true" />
        </ItemComponent>
        <Flags Civilian="true" />
    </Item>
    <Item
        id="taom_chariot_armour_dwarf"
        name="{=taom_chariot_armour_dwarf}[Erebor] War Chariot Plating"
        mesh="dwarf_war_chariot"
        culture="Culture.erebor"
        weight="50"
        is_merchandise="false"
        appearance="0.65"
        Type="HorseHarness">
        <ItemComponent>
            <Armor
                body_armor="50"
                mane_cover_type="all"
                family_type="4"
                material_type="Plate" />
        </ItemComponent>
        <Flags Civilian="true" UseTeamColor="true" />
    </Item>
    <!-- Dwarven war chariot b: the same ram team and barding as above, with a steel cart built on the ram barding's
         own two materials (plate courses with the gold band, pointed tile fields). The cloth takes the team colour. -->
    <Item
        id="taom_chariot_armour_dwarf_b"
        name="{=taom_chariot_armour_dwarf_b}[Erebor] War Chariot Steel Plating"
        mesh="dwarf_war_chariot_b"
        culture="Culture.erebor"
        weight="50"
        is_merchandise="false"
        appearance="0.65"
        Type="HorseHarness">
        <ItemComponent>
            <Armor
                body_armor="54"
                mane_cover_type="all"
                family_type="4"
                material_type="Plate" />
        </ItemComponent>
        <Flags Civilian="true" UseTeamColor="true" />
    </Item>'''


def backup_suffix():
    return BACKUP_PREFIX + dt.datetime.now().strftime("%Y%m%d-%H%M%S")


def resolve_paths(root):
    armory = os.path.join(str(root), "Modules", "LOTRLOME_Armory")
    return {
        "armory": armory,
        "horses": os.path.join(armory, "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml"),
        "loc": os.path.join(armory, "ModuleData", "Languages", "loc_LOTRAOM_horses.xml"),
        "assets": os.path.join(armory, "Assets"),
        "cache": os.path.join(armory, "RuntimeDataCache"),
    }


def _masked(text):
    """The text with every comment blanked and every offset kept: a commented-out row is not installed."""
    return re.sub(r"<!--.*?-->", lambda m: " " * len(m.group(0)), text, flags=re.S)


def _state(text, block, what, problems):
    """'present' when the block is there one time, byte for byte, and each id one time; 'absent' when none of its
    ids is; else a problem and None. The ids are read from the parsed document, so a row written with other
    quotes or spacing counts, and a document that does not parse is a problem whatever it holds."""
    try:
        root = ET.fromstring(text.lstrip("﻿"))
    except ET.ParseError as exc:
        problems.append("PARSE FAILED: %s: %s" % (what, exc))
        return None
    ids = [e.get("id") for e in root.iter() if e.get("id") in ITEM_NAMES]
    present = [i for i in ITEM_NAMES if i in ids]
    if text.count(block) == 1 and len(ids) == len(ITEM_NAMES) == len(present):
        return "present"
    if not present:
        return "absent"
    if len(ids) > len(present):
        problems.append("DRIFT: %s: a dwarf chariot id is in the file more than one time; "
                        "a person pasted it and must decide" % what)
    elif len(present) == len(ITEM_NAMES):
        problems.append("DRIFT: %s: the three dwarf chariot rows are there but differ from this file's; "
                        "a person changed them and must decide" % what)
    else:
        problems.append("%s: the dwarf chariot rows are partly present: %s" % (what, present))
    return None


def plan_items(path, remove, report, problems, changed):
    """Returns True when the items are, or will be, in the file after this run."""
    text = read(path)
    nl = nl_of(text)
    block = nl + ITEMS_BLOCK.replace("\n", nl)
    state = _state(text, block, "LOTRAOM_horses.xml", problems)
    if state is None:
        return False
    if remove:
        if state == "present":
            changed.append((path, text.replace(block, "", 1)))
            report.append("LOTRAOM_horses.xml: remove %d items" % len(ITEM_NAMES))
        else:
            report.append("LOTRAOM_horses.xml: already absent")
        return False
    if state == "present":
        report.append("LOTRAOM_horses.xml: already present")
        return True
    live = _masked(text)
    marks = [m.start() for m in re.finditer(re.escape('id="%s"' % ITEM_ANCHOR), live)]
    end = live.find("</Item>", marks[0]) if len(marks) == 1 else -1
    # the next <Item ...> element, not <ItemComponent>: a self-closing anchor would end at another item's </Item>
    following = re.compile(r"<Item[\s/>]").search(live, marks[0]) if len(marks) == 1 else None
    nxt = following.start() if following else -1
    if end < 0 or (0 <= nxt < end):
        problems.append("ANCHOR: LOTRAOM_horses.xml has no single item %s that closes with </Item>" % ITEM_ANCHOR)
        return False
    end += len("</Item>")
    changed.append((path, text[:end] + block + text[end:]))
    report.append("LOTRAOM_horses.xml: add %d items" % len(ITEM_NAMES))
    return True


def plan_loc(path, remove, report, problems, changed):
    text = read(path)
    nl = nl_of(text)
    rows = "".join('    <string id="%s" text="%s"/>%s' % (i, escape(n, {'"': "&quot;"}), nl) for i, n in ITEM_NAMES.items())
    state = _state(text, rows, "loc_LOTRAOM_horses.xml", problems)
    if state is None:
        return
    if remove:
        if state == "present":
            changed.append((path, text.replace(rows, "", 1)))
            report.append("loc_LOTRAOM_horses.xml: remove %d rows" % len(ITEM_NAMES))
        else:
            report.append("loc_LOTRAOM_horses.xml: already absent")
        return
    if state == "present":
        report.append("loc_LOTRAOM_horses.xml: already present")
        return
    found = list(re.finditer(r'[ \t]*<string id="%s"[^>]*/>\r?\n' % re.escape(LOC_ANCHOR), _masked(text)))
    if len(found) != 1:
        problems.append("ANCHOR: loc_LOTRAOM_horses.xml has no single %s row outside a comment" % LOC_ANCHOR)
        return
    changed.append((path, text[:found[0].end()] + rows + text[found[0].end():]))
    report.append("loc_LOTRAOM_horses.xml: add %d rows" % len(ITEM_NAMES))


def missing_packages(assets, cache):
    """One line per package the items need and the Armory lacks. The file name is matched whole, without case."""
    by_name = {}
    for folder, _dirs, files in os.walk(assets):
        for name in files:
            if name.lower().endswith(".tpac"):
                by_name.setdefault(name.lower(), []).append(os.path.join(folder, name))
    cached = {n.lower() for n in os.listdir(cache)} if os.path.isdir(cache) else set()
    lines = []
    for names, tail, needs_cache in ((MESHES, "_geo.tpac", True), (MATERIALS, "_mtl.tpac", False)):
        for name in names:
            found = by_name.get(name + tail, [])
            if not found:
                lines.append("PACKAGE: no %s%s under the Armory's Assets (import it in the Modding Kit)" % (name, tail))
                continue
            if len(found) > 1:
                lines.append("PACKAGE: %s%s is under the Armory's Assets twice: %s" % (name, tail, ", ".join(sorted(found))))
                continue
            guid = package_guid(found[0])              # "" when the file has no package header
            with open(found[0], "rb") as fh:
                data = fh.read()
            if not guid:
                lines.append("PACKAGE: %s%s is not a package (no TPAC header)" % (name, tail))
            elif name.encode("ascii") not in data.lower():
                lines.append("PACKAGE: %s%s does not hold the name %s" % (name, tail, name))
            elif needs_cache and guid + ".rdc" not in cached:
                lines.append("PACKAGE: %s%s has no RuntimeDataCache entry (the Modding Kit writes it)" % (name, tail))
    return lines


def main(argv=None):
    try:
        return _main(argv)
    except (OSError, UnicodeDecodeError, ValueError) as exc:    # an unreadable input is a crash, not a finding
        print("CRASH: %s: %s" % (type(exc).__name__, exc))
        return 3


def _main(argv):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the rows (Modding Kit and game closed)")
    mode.add_argument("--remove", action="store_true", help="take out exactly the rows this file writes")
    mode.add_argument("--check", action="store_true", help="exit 1 when the Armory differs from this file")
    ap.add_argument("--game-dir", default=None, help="Bannerlord install root (default: $BANNERLORD_GAME_DIR or the desktop path)")
    args = ap.parse_args(argv)
    if args.game_dir is not None and not args.game_dir.strip():
        print("ERROR: --game-dir is empty; pass the Bannerlord install root or leave --game-dir out")
        return 2
    if (args.apply or args.remove) and game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2
    paths = resolve_paths(args.game_dir or game_dir(_DEFAULT_GAME_DIR))
    for key in ("armory", "horses", "loc"):
        ensure_exists(paths[key], "the Armory's %s" % key)

    report, problems, changed = [], [], []
    items_stay = plan_items(paths["horses"], args.remove, report, problems, changed)
    plan_loc(paths["loc"], args.remove, report, problems, changed)
    if items_stay:
        problems.extend(missing_packages(paths["assets"], paths["cache"]))

    suffix = backup_suffix()
    for path, text in changed:  # parse every document before writing any
        try:
            write(path, text, apply=False, suffix=suffix)
        except ET.ParseError as exc:
            problems.append("PARSE FAILED: %s: %s" % (os.path.basename(path), exc))
    for line in report + problems:
        print(line)
    if problems:
        print("refusing to write: no drift, every anchor found, every package cooked, every file must parse")
        return 1
    names = ", ".join(os.path.basename(p) for p, _ in changed)
    if not changed:
        print("nothing to write: the Armory holds this file's rows" if not args.remove else "nothing to write: the rows are absent")
        return 0
    if args.check:
        print("CHECK FAILED: %d file(s) differ from this file (%s); run --apply with the Kit and the game closed" % (len(changed), names))
        return 1
    if args.apply or args.remove:
        for path, text in changed:
            write(path, text, apply=True, suffix=suffix)
        print("%s %d file(s): %s (backups: *%s)" % ("APPLIED" if args.apply else "REMOVED", len(changed), names, suffix))
    else:
        print("dry run: %d file(s) would change (%s), all parse; nothing written (--apply to write, Kit and game closed)"
              % (len(changed), names))
    return 0


if __name__ == "__main__":
    sys.exit(main())
