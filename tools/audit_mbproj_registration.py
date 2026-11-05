#!/usr/bin/env python3
"""Audit every module's `project.mbproj` for registrations the engine silently ignores or throws on.

The engine loads a `<file>` row of `project.mbproj` in two independent ways
(managed half verified on v1.4.8, MBObjectManager.cs:920-946 and the callers listed; NATIVE_BY_TYPE comes from v1.5.5 disassembly of FUN_180719810 and is UNVERIFIED on v1.4.8, whose native DLL holds all thirteen strings):

  Managed, by `id`. `MBObjectManager.GetMergedXmlForNative(id)` walks `XmlResource.MbprojXmls`
  and keeps the entries whose `Id` matches EXACTLY. It is reached only from the requesters
  below. An invented id matches nothing, no file is read and nothing is logged: the row looks
  like registration and is inert.
  Native, by `type`. Native's loader asks for thirteen kinds of file by `type` (`NATIVE_BY_TYPE`,
  the callers of `FUN_180719810`), reads each matching row's `name` and never its `id`, so a
  row of one of those types loads whatever its id is (TAOM's own `soln_module_sound` row loads
  this way). Any other `type` is ignored. In particular `skin`, `item`, `monster`, `action_set`
  and `action_type` are not among the thirteen: a row of those types is loaded only through its
  id.

The set of ids the requesters ask for is not a Python constant here. It is the eight ids of
`Module.CreateProcessed*XMLForNative`,
`soln_soundtrack` (`MBMusicManager` reads `MbprojXmls` for it directly; the row's name is
ignored and the music loads from `<module>/Music/soundtrack.xml`) and five ids native code
requests by `"soln_" + xmlType`: `soln_movement_sets`, `soln_full_movement_sets`,
`soln_bone_body_types`, `soln_monster_usage_sets`, `soln_item_usage_sets`. `MANAGED_IDS` holds the
managed ones and the audit takes the `soln_*` ids of the vanilla Native project.mbproj as a
STAND-IN for the native ones: it is a superset in practice, not the exact set native asks for.
Switching to the exact list is follow-up work.

That is not hypothetical. The giant spider shipped `soln_spider_*` ids in 2026-06, its
action_sets and monster_usage_set never loaded, `GetMonsterUsageIndex("spider")` returned
-1 and native `CreateAgent` divided by zero on spawn. The lesson was written into a comment
at the top of the Armory's own project.mbproj, and two custom-id rows still survived it
until 2026-08-28: `soln_spider_monster` (inert but harmless, the Monster loads the managed
way via SubModule.xml) and `soln_lotr_misc_action_types`, whose 20 action declarations had
therefore never reached the engine while `action_sets.xml` bound them 221 times.

This is the regression gate for that class. `LOTRLOME_Armory` is a separate, untracked
module, so a reinstall silently reverts the fix; run this to find out.

The file is parsed as XML the way the engine reads it (decoded text, the declared encoding
ignored; `validate_xml_schemas._parse_like_engine`), so attribute order does not matter.

Checks:
  BAD-FILE   the file does not parse, its root is not `<base>`, or a `<file>` row has no `id` or
             no `name`: `XmlResource.GetMbprojxmls` throws at startup. Also an action_types.xml
             or action_sets.xml that does not parse  (ERROR)
  DEAD-ID    an id nothing requests AND a `type` outside `NATIVE_BY_TYPE` (a row with no `type`
             counts as having none)  -> the file never loads  (ERROR)
  UNDECLARED an `<action type=>` bound in action_sets.xml, declared nowhere (ERROR)
  MERGE-RISK a duplicated id that HAS an XSD  -> takes the MergeElements path  (WARN)
  MISSING    a `<file name=>` that is not on disk  (WARN)

Load-bearing test: `NativeTypeTable.test_spider_era_rows_are_dead` replays the six rows that
were dead (their `type` was action_set, action_type, monster or monster_usage_set, all
vanilla types). Never widen `NATIVE_BY_TYPE` to the types vanilla's own project.mbproj uses.

Not checked: the exact set of native-requested ids (the stand-in above), that a `type` row's
`name` is a file native can parse, other native readers of `project.mbproj`, and `<file>` rows
outside `<base>`.

MISSING is only a warning because vanilla itself ships four of them (`strings.xml`,
`skeletons.xml`, `tags.xml`, `clothing_materials.xml` are all registered in Native's
project.mbproj and none is on disk). `GetMergedXmlForNative` handles that deliberately,
substituting an empty `Tuple.Create("", "")`, so an absent file is tolerated by design and
is not evidence of a defect.

Scope defaults to the three modules TAOM's data surface actually spans: this repo's own
module, `TAOM_Map` and `LOTRLOME_Armory`. Third-party modules have their own undeclared
actions and are not TAOM's to fix; sweep them with `--all` when you want the wider picture.

MERGE-RISK is the elephant "Crash #3" class and is a WARNING, not an error: duplicating an
id is legitimate and common (three `soln_monsters` rows ship today). It is only dangerous
when an XSD exists for that id, because `MergeTwoXmls` then calls `MergeElements`, which
indexes a dictionary built from the schema with a raw `[...]` lookup and throws
`KeyNotFoundException` on any element XPath the schema does not carry.

Read-only. Exits 1 if any ERROR is found beyond the recorded baseline.
"""
from __future__ import annotations

import argparse
import os
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from validate_xml_schemas import _parse_like_engine, mbproj_rows  # noqa: E402

DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

# The modules TAOM's own data surface spans (docs/ai-includes/orientation.md, "Three modules").
# Anything else installed is somebody else's module and is only audited under --all.
TAOM_MODULES = ("TAOM", "TAOM_Map", "LOTRLOME_Armory")

# What the engine can read from a project.mbproj row
# (managed half verified on v1.4.8, MBObjectManager.cs:920-946 and the callers listed; NATIVE_BY_TYPE comes from v1.5.5 disassembly of FUN_180719810 and is UNVERIFIED on v1.4.8, whose native DLL holds all thirteen strings).
#
# MANAGED_IDS: ids a managed caller asks GetMergedXmlForNative for by exact string.
#   Module.CreateProcessed*XMLForNative: skins, item_holsters, action_sets, action_types,
#   animations, voice_definitions, sound_event_data, sound_parameter_data
#   (TaleWorlds.MountAndBlade.Module, the decompile's Module.cs).
#   (TaleWorlds.MountAndBlade.View.Scripts.MapColorGradeManager, TaleWorlds.MountAndBlade.View.dll).
#   MBMusicManager reads XmlResource.MbprojXmls for soln_soundtrack directly (the row's name is
#   ignored; the music loads from <module>/Music/soundtrack.xml).
# The five ids native code requests by "soln_" + xmlType (soln_movement_sets,
# soln_full_movement_sets, soln_bone_body_types, soln_monster_usage_sets, soln_item_usage_sets)
# are not listed: audit_module takes the `soln_*` ids of the vanilla Native project.mbproj as a
# stand-in for them. It is a stand-in, not the exact set native requests (switching: #987).
MANAGED_IDS = {
    "soln_skins",
    "soln_item_holsters",
    "soln_action_sets",
    "soln_action_types",
    "soln_animations",
    "soln_voice_definitions",
    "soln_sound_event_data",
    "soln_sound_parameter_data",
    "soln_soundtrack",
}

# NATIVE_BY_TYPE: the row `type` values native's loader requests by type. Source: the 14 callers
# of FUN_180719810 (TaleWorlds.Native.dll), which select the rows of a `type` and read each
# row's `name`, never its `id`. Exactly these thirteen. Not in the list, so loaded only through
# their id: skin, item, monster, action_set, action_type, monster_usage_set, item_holster,
# tag and the rest of vanilla's types.
NATIVE_BY_TYPE = {
    "hard_coded_sound", "module_sound", "animation_combat_parameters", "native_parameters",
    "decal_set", "face_animation_record", "postfx", "prebaked_animation", "physics_material",
    "sound_files", "skinned_decals", "collision_infos", "sound_event_data",
}

# Actions bound in a module's action_sets.xml that are declared in no action_types.xml
# anywhere. Pre-existing and NOT the dead-id class: these were never declared at all, so
# there is no file to un-orphan. Declaring a name does not make its animation exist, so
# these need an animation-inventory check before anyone "fixes" them. Recorded so the gate
# stays green on the known set and fails on anything new.
UNDECLARED_BASELINE = {
    "LOTRLOME_Armory": {
        "act_ghurab_captain_idle",
        "act_idle_javelin_with_shield_1_left_stance",
        "act_idle_javelin_with_shield_2_left_stance",
        "act_idle_javelin_with_shield_3_left_stance",
        "act_idle_javelin_without_shield_1_left_stance",
        "act_idle_javelin_without_shield_2_left_stance",
        "act_idle_javelin_without_shield_3_left_stance",
    },
}


def action_attrs(path: str, attr: str) -> tuple[list[str], list[str]]:
    """(values, problems) of the `attr` attribute of every <action> element of an action XML
    file, in document order, read as XML in any attribute order (a commented-out row is not an
    element). A file that is absent has neither; one that does not parse is a problem."""
    if not os.path.exists(path):
        return [], []
    try:
        root = _parse_like_engine(path)
    except (ET.ParseError, OSError, ValueError, LookupError) as exc:
        return [], [f"{path} cannot be parsed ({exc}); the engine throws at startup."]
    return [e.get(attr) for e in root.iter("action") if e.get(attr)], []


def vanilla_ids(game_dir: str) -> set[str]:
    """The `soln_*` ids of Native's own project.mbproj: the stand-in for the ids native
    requests by `"soln_" + xmlType`."""
    native = os.path.join(game_dir, "Modules", "Native", "ModuleData", "project.mbproj")
    return {i for i, _, _ in mbproj_rows(native)[0] if i.startswith("soln_")}


def audit_module(module_dir: str, game_dir: str, vanilla: set[str]) -> list[tuple[str, str]]:
    """Return (severity, message) pairs for one module. Severity is ERROR or WARN. `vanilla` is
    the stand-in for the ids native requests; the managed requesters and the thirteen native
    types are added here."""
    requested = vanilla | MANAGED_IDS
    name = os.path.basename(module_dir.rstrip("\\/"))
    mbproj = os.path.join(module_dir, "ModuleData", "project.mbproj")
    if not os.path.exists(mbproj):
        return []

    findings: list[tuple[str, str]] = []
    rows, problems = mbproj_rows(mbproj)
    for problem in problems:
        findings.append(("ERROR", f"{'BAD-FILE':<11}{problem}"))

    seen: dict[str, int] = {}
    for file_id, rel, file_type in rows:
        seen[file_id] = seen.get(file_id, 0) + 1

        if file_id not in requested and file_type not in NATIVE_BY_TYPE:
            findings.append((
                "ERROR",
                f"{'DEAD-ID':<11}{file_id}\n"
                f"             -> {rel}\n"
                f"             nothing requests this id and native does not read\n"
                f"             type={file_type!r}, so the file never loads.\n"
                f"             Fold its content into the standard-id file instead.",
            ))

        target = os.path.join(module_dir, rel.replace("/", os.sep))
        if not os.path.exists(target):
            findings.append(("WARN", f"{'MISSING':<11}{file_id} -> {rel} is not on disk"))

    for file_id, count in sorted(seen.items()):
        if count > 1 and os.path.exists(os.path.join(game_dir, "XmlSchemas", f"{file_id}.xsd")):
            findings.append((
                "WARN",
                f"{'MERGE-RISK':<11}{file_id} appears {count}x and {file_id}.xsd EXISTS,\n"
                f"             so the extra rows take the MergeElements path\n"
                f"             (KeyNotFoundException at startup, elephant Crash #3).",
            ))

    sets_path = os.path.join(module_dir, "ModuleData", "action_sets.xml")
    if os.path.exists(sets_path):
        native_types = os.path.join(game_dir, "Modules", "Native", "ModuleData", "action_types.xml")
        own_types = os.path.join(module_dir, "ModuleData", "action_types.xml")
        available: set[str] = set()
        parse_problems: list[str] = []
        for types_path in (own_types, native_types):
            declared, bad = action_attrs(types_path, "name")
            available.update(declared)
            parse_problems += bad
        bound, bad = action_attrs(sets_path, "type")
        parse_problems += bad
        for problem in parse_problems:
            findings.append(("ERROR", f"{'BAD-FILE':<11}{problem}"))
        baseline = UNDECLARED_BASELINE.get(name, set())
        unresolved = sorted({a for a in bound if a not in available} - baseline)
        for action in unresolved:
            findings.append((
                "ERROR",
                f"{'UNDECLARED':<11}{action} is bound {bound.count(action)}x in action_sets.xml\n"
                f"             but declared in no action_types.xml. It resolves to act_none.",
            ))

    return [(sev, f"[{name}] {msg}") for sev, msg in findings]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        "--game-dir",
        default=os.environ.get("BANNERLORD_GAME_DIR", DEFAULT_GAME_DIR),
        help="Bannerlord install root (default: $BANNERLORD_GAME_DIR or the E:\\Steam path)",
    )
    parser.add_argument("--module", help="audit only this module id")
    parser.add_argument(
        "--all",
        action="store_true",
        help="audit every installed module, not just TAOM's three (noisy: other mods have their own gaps)",
    )
    args = parser.parse_args()

    modules_dir = os.path.join(args.game_dir, "Modules")
    if not os.path.isdir(modules_dir):
        print(f"Bannerlord Modules directory not found: {modules_dir}")
        print("Set $BANNERLORD_GAME_DIR or pass --game-dir. Skipping (not a failure).")
        return 0

    vanilla = vanilla_ids(args.game_dir)
    if not vanilla:
        print("Could not read Native/ModuleData/project.mbproj; cannot establish the id vocabulary.")
        return 0
    print(f"Requested ids: {len(vanilla | MANAGED_IDS)} ({len(vanilla)} soln_* ids of Native's "
          f"project.mbproj as a stand-in for the ids native requests, plus the managed requesters), "
          f"{len(NATIVE_BY_TYPE)} native types\n")

    if args.module:
        names = [args.module]
    elif args.all:
        names = sorted(os.listdir(modules_dir))
    else:
        names = list(TAOM_MODULES)
        print(f"Scope: {', '.join(names)}  (use --all to sweep every installed module)\n")
    findings: list[tuple[str, str]] = []
    audited = 0
    for entry in names:
        path = os.path.join(modules_dir, entry)
        if not os.path.isdir(path):
            continue
        if os.path.exists(os.path.join(path, "ModuleData", "project.mbproj")):
            audited += 1
        findings.extend(audit_module(path, args.game_dir, vanilla))

    errors = [m for sev, m in findings if sev == "ERROR"]
    warns = [m for sev, m in findings if sev == "WARN"]

    for message in warns:
        print(f"WARN  {message}")
    if warns:
        print()
    for message in errors:
        print(f"ERROR {message}")
    if errors:
        print()

    print(f"{audited} module(s) with a project.mbproj audited: {len(errors)} error(s), {len(warns)} warning(s)")
    if errors:
        print("\nA dead id is not a load-order problem and produces no engine error;")
        print("a BAD-FILE makes the engine throw at startup.")
        print("Ledger for the 2026-08-28 fix: docs/reference/lotrlome-soln-id-fix.md")
        return 1
    print("No silently-ignored registrations.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
