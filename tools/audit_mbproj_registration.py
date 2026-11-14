#!/usr/bin/env python3
"""Audit every module's `project.mbproj` for registrations the engine silently ignores or throws on.

The engine loads a `<file>` row of `project.mbproj` in two independent ways. The managed side was re-read on v1.5.6
(2026-10-27); the native side and its FUN_ addresses are the v1.5.5 client's and were not re-derived: UNVERIFIED.

  Managed, by `id`. `MBObjectManager.GetMergedXmlForNative(id)` walks `XmlResource.MbprojXmls`
  and keeps the entries whose `Id` matches EXACTLY. It is reached only from the requesters
  listed in `REQUESTED_IDS`. An invented id matches nothing, no file is read and nothing is
  logged: the row looks like registration and is inert.
  Native, by `type`. Native's loader asks for thirteen kinds of file by `type` (`NATIVE_BY_TYPE`,
  the callers of `FUN_180719810`, v1.5.5 client; v1.5.6 0x719F90 by native_build_match hash),
  reads each matching row's `name` and never its `id`, so a row of one of those types loads
  whatever its id is (TAOM's own `soln_module_sound` row loads this way). Any other `type` is
  ignored. In particular `skin`, `item`, `monster`, `action_set`
  and `action_type` are not among the thirteen: a row of those types is loaded only through its
  id.

The ids the requesters ask for are an exact table, `REQUESTED_IDS` (13), with the source of each id
in the dict value: the six `Module.CreateProcessed*XMLForNative` ids that something calls,
`soln_worldmap_color_grades` (`MapColorGradeManager`), `soln_soundtrack` (`MBMusicManager` reads
`MbprojXmls` for it directly; the row's name is ignored and the music loads from
`<module>/Music/soundtrack.xml`) and the five ids native code requests through
`CreateProcessedModuleDataXMLForNative` (`"soln_" + xmlType`): `soln_movement_sets`,
`soln_full_movement_sets`, `soln_bone_body_types`, `soln_monster_usage_sets` and
`soln_item_usage_sets`. Nothing calls the Animations or SoundEventData callbacks (the native slots were read on v1.5.5
only), so `soln_animations` and `soln_sound_event_data` are not in it. Native's own project.mbproj is not
read: vanilla also registers ids (`soln_monsters`, `soln_strings`, `soln_particle_systems` ...)
that nothing requests.

That is not hypothetical. The giant spider shipped `soln_spider_*` ids in 2026-06, its
action_sets and monster_usage_set never loaded, `GetMonsterUsageIndex("spider")` returned
-1 and native `CreateAgent` divided by zero on spawn. The lesson was written into a comment
at the top of the Armory's own project.mbproj, and two custom-id rows still survived it
until 2026-08-28: `soln_spider_monster` (inert but harmless, the Monster loads the managed
way via SubModule.xml) and `soln_lotr_misc_action_types`, whose 20 action declarations had
therefore never reached the engine while `action_sets.xml` bound them 221 times.

This is the regression gate for that class. `LOTRLOME_Armory` is a separate, untracked
module, so a reinstall silently reverts the fix; run this to find out.

Files are read the way the engine reads them: `XmlResource` opens a project.mbproj with
`new StreamReader(path)` (XmlResource.cs:92), so the text is UTF-8 unless a byte order mark says
otherwise, the declared encoding is ignored, and a UTF-16 file with no mark is not well-formed
(`validate_xml_schemas._parse_like_engine`). Attribute order does not matter.

Checks:
  BAD-FILE   the file does not parse, its root is not `<base>`, or a `<file>` row has no `id` or
             no `name`: `XmlResource.GetMbprojxmls` throws at startup. Also an action_types.xml
             or action_sets.xml that does not parse  (ERROR)
  DEAD-ID    an id nothing requests AND a `type` outside `NATIVE_BY_TYPE` (a row with no `type`
             counts as having none)  -> the row loads nothing; the file may still load another
             way (SubModule.xml, a fixed path native opens itself)  (ERROR)
  INERT-ROW  the same row, but its `type` has a managed twin (`MANAGED_TWIN`: monster ->
             `Monsters`, item_modifier -> `ItemModifiers`) and the module's SubModule.xml registers
             its file through an `<XmlName>` with exactly that id: the file loads that way and
             the row is read by nothing  (WARN). Any other XmlName id is not requested by
             anything, so it does not make the row inert.
  UNDECLARED an `<action type=>` bound in action_sets.xml, declared nowhere (ERROR)
  MERGE-RISK a duplicated id that HAS an XSD  -> takes the MergeElements path  (WARN)
  MISSING    a `<file name=>` that is not on disk  (WARN)
  DEPLOYED-DIFFERS  TAOM only: the deployed Modules/TAOM project.mbproj is not the repo's  (WARN)

Load-bearing test: `NativeTypeTable.test_spider_era_rows_are_dead` replays the six rows that
were dead (their `type` was action_set, action_type, monster or monster_usage_set, all
vanilla types). Never widen `NATIVE_BY_TYPE` to the types vanilla's own project.mbproj uses.

Not checked: that a `type` row's `name` is a file native can parse, other native readers of
`project.mbproj`, and `<file>` rows outside `<base>`. A module's SubModule.xml is read with
ElementTree, not the engine's reader, and native's own project.mbproj reader is modelled only by the
managed one (`XmlResource.GetMbprojxmls`).

MISSING is only a warning because vanilla itself ships four of them (`strings.xml`,
`skeletons.xml`, `tags.xml`, `clothing_materials.xml` are all registered in Native's
project.mbproj and none is on disk). `GetMergedXmlForNative` handles that deliberately,
substituting an empty `Tuple.Create("", "")`, so an absent file is tolerated by design and
is not evidence of a defect.

Scope defaults to the three modules TAOM's data surface actually spans: this repo's own
module, `TAOM_Map` and `LOTRLOME_Armory`. Third-party modules have their own undeclared
actions and are not TAOM's to fix; sweep them with `--all` when you want the wider picture. `--all`
skips the game's official modules (SubModule.xml `ModuleType` Official or OfficialOptional), which ship
rows nothing requests, and prints one note naming them; `--module Native` still audits one.
"TAOM" is audited from this repo's `Main/_Module/ModuleData/project.mbproj`, the file authors edit
and the gate table names, not from the deployed `Modules/TAOM` copy; the output says so, and the
deployed copy is compared and a difference is a warning.

MERGE-RISK is the elephant "Crash #3" class and is a WARNING, not an error: duplicating an
id is legitimate and common. It is only dangerous when an XSD exists for that id, because
`MergeTwoXmls` then calls `MergeElements`, which indexes a dictionary built from the schema with
a raw `[...]` lookup and throws `KeyNotFoundException` on any element XPath the schema does not
carry.

Read-only. Exit codes: 0 clean (warnings allowed); 1 an ERROR was found; 2 nothing, or not enough,
could be audited: no Modules folder, a named module that is absent or has no project.mbproj, no
module with a project.mbproj in scope, or a Native `action_types.xml` that a module's
`action_sets.xml` needs and that is missing or unreadable. A partial run never reads as clean.
"""
from __future__ import annotations

import argparse
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from validate_xml_schemas import _key, _parse_like_engine, _registrations, mbproj_rows  # noqa: E402

DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

# This repo's root, where "TAOM" is audited from (Main/_Module). A tools/ script's parent.
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO_MODULE_LABEL = "Main/_Module/ModuleData/project.mbproj"

# The modules TAOM's own data surface spans (docs/ai-includes/orientation.md, "Three modules").
# Anything else installed is somebody else's module and is only audited under --all.
TAOM_MODULES = ("TAOM", "TAOM_Map", "LOTRLOME_Armory")

# The ids a project.mbproj `<file id>` row can be read by, each with its source. The managed side was re-read on v1.5.6
# (2026-10-27); the native side and its FUN_ addresses are the v1.5.5 client's, not re-derived: UNVERIFIED. A row
# whose id is not here, and whose `type` is not in NATIVE_BY_TYPE, is read by nothing.
# Managed requesters ask MBObjectManager.GetMergedXmlForNative for the id by exact string; native
# code asks Module.CreateProcessedModuleDataXMLForNative(xmlType), which prefixes "soln_".
REQUESTED_IDS = {
    "soln_skins": "Module.CreateProcessedSkinsXMLForNative (TaleWorlds.MountAndBlade.Module, Module.cs:1446)",
    "soln_item_holsters": "Module.CreateProcessedItemHolstersXMLForNative (Module.cs:1458)",
    "soln_action_sets": "Module.CreateProcessedActionSetsXMLForNative (Module.cs:1469)",
    "soln_action_types": "Module.CreateProcessedActionTypesXMLForNative (Module.cs:1499)",
    # Not listed: soln_animations (Module.CreateProcessedAnimationsXMLForNative, Module.cs:1510) and
    # soln_sound_event_data (CreateProcessedSoundEventDataXMLForNative, Module.cs:1562). They are
    # [MBCallback]s with no caller of the Animations or SoundEventData callback in v1.5.5 (native
    # callback slots 0x2b0 and 0x2d0 unused, shipping and editor). A `sound_event_data`-typed row
    # still loads by type (NATIVE_BY_TYPE).
    "soln_voice_definitions": "Module.CreateProcessedVoiceDefinitionsXMLForNative (Module.cs:1529)",
    "soln_sound_parameter_data": "Module.CreateProcessedSoundParamsXMLForNative (Module.cs:1573)",
    "soln_worldmap_color_grades": "MapColorGradeManager.ReadColorGradesXml "
                                  "(TaleWorlds.MountAndBlade.View.Scripts, MapColorGradeManager.cs:162)",
    # MBMusicManager walks XmlResource.MbprojXmls itself; the row's name is ignored and the music
    # loads from <module>/Music/soundtrack.xml.
    "soln_soundtrack": "MBMusicManager (MBMusicManager.cs:351) reads XmlResource.MbprojXmls for it directly",
    "soln_movement_sets": "native FUN_18058a480 requests 'movement_sets' through "
                          "Module.CreateProcessedModuleDataXMLForNative (Module.cs:1584)",
    "soln_full_movement_sets": "native FUN_18058a480 requests 'full_movement_sets' through "
                               "Module.CreateProcessedModuleDataXMLForNative (Module.cs:1584)",
    "soln_bone_body_types": "native FUN_18058a480 requests 'bone_body_types' through "
                            "Module.CreateProcessedModuleDataXMLForNative (Module.cs:1584)",
    "soln_monster_usage_sets": "native FUN_18058a480 requests 'monster_usage_sets' through "
                               "Module.CreateProcessedModuleDataXMLForNative (Module.cs:1584)",
    "soln_item_usage_sets": "native FUN_18058a480 requests 'item_usage_sets' through "
                            "Module.CreateProcessedModuleDataXMLForNative (Module.cs:1584)",
}

# Row types whose file the engine also loads by a SubModule.xml `<XmlName>` with this exact,
# case-sensitive id (Game.cs:437 `LoadXML("Monsters")`, :439 `LoadXML("ItemModifiers")`, through
# MBObjectManager.GetMergedXmlForManaged). Only an XmlName with that id makes such a row inert; an
# XmlName with any other id is loaded only if something requests it.
MANAGED_TWIN = {"monster": "Monsters", "item_modifier": "ItemModifiers"}

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


class AuditUnavailable(Exception):
    """Something the audit needs cannot be read, so a verdict would be a guess (exit 2)."""


def action_attrs(path: str, attr: str) -> tuple[list[str], list[str]]:
    """(values, problems) of the `attr` attribute of every <action> element of an action XML
    file, in document order, read as XML in any attribute order (a commented-out row is not an
    element). A file that is absent has neither; one that does not parse is a problem. The file
    is read as the engine reads it, so a Latin-1 byte is a replacement character, not an error."""
    if not os.path.exists(path):
        return [], []
    try:
        root = _parse_like_engine(path)
    except (ET.ParseError, OSError, ValueError, LookupError) as exc:
        return [], [f"{path} cannot be parsed ({exc}); the engine throws at startup."]
    return [e.get(attr) for e in root.iter("action") if e.get(attr)], []


def native_action_types(game_dir: str) -> set[str]:
    """The action names Native's own action_types.xml declares. AuditUnavailable when it is absent or
    does not parse: every action a module binds from the engine would then read as undeclared."""
    path = os.path.join(game_dir, "Modules", "Native", "ModuleData", "action_types.xml")
    if not os.path.exists(path):
        raise AuditUnavailable(f"Native's action_types.xml is not at {path}; the UNDECLARED check "
                               f"needs it, so nothing was audited.")
    declared, bad = action_attrs(path, "name")
    if bad:
        raise AuditUnavailable(f"Native's action_types.xml cannot be read: {bad[0]} The UNDECLARED "
                               f"check needs it, so nothing was audited.")
    return set(declared)


def submodule_registered(module_dir: str) -> dict[str, list[tuple[str, str]]]:
    """{file key: [(xml id, registered path), ...]} for every file this module's SubModule.xml
    registers through an <XmlName> (validate_xml_schemas' registration walk). Empty when SubModule.xml
    is absent or unreadable: the rows are then judged on their own."""
    try:
        rows, _ = _registrations(Path(module_dir))
    except (OSError, ET.ParseError, ValueError, LookupError):
        return {}
    registered: dict[str, list[tuple[str, str]]] = {}
    for xml_id, rel, files, _ in rows:
        for p in files:
            registered.setdefault(_key(p), []).append((xml_id, rel))
    return registered


def _dead_id_hint(twin: str | None) -> str:
    """What to do about a DEAD-ID row; twin is the XmlName id its `type` loads by, when it has one."""
    if twin:
        return (f"Register the file with\n             <XmlName id=\"{twin}\"> in SubModule.xml, "
                f"or fold its content into the standard-id file.")
    return "Fold its content into the\n             standard-id file, or register it where its manager asks for it."


def audit_module(module_dir: str, game_dir: str, name: str | None = None,
                 extra_type_dirs: tuple[str, ...] | list[str] = ()) -> list[tuple[str, str]]:
    """Return (severity, message) pairs for one module. Severity is ERROR or WARN. `name` labels the
    findings and keys UNDECLARED_BASELINE (default: the folder name). Raises AuditUnavailable when an
    action_sets.xml needs Native's action_types.xml and it cannot be read. `extra_type_dirs` are other
    modules whose action_types.xml also declares names (the engine merges them all)."""
    name = name or os.path.basename(module_dir.rstrip("\\/"))
    mbproj = os.path.join(module_dir, "ModuleData", "project.mbproj")
    if not os.path.exists(mbproj):
        return []

    findings: list[tuple[str, str]] = []
    rows, problems = mbproj_rows(mbproj)
    for problem in problems:
        findings.append(("ERROR", f"{'BAD-FILE':<11}{problem}"))

    registered = submodule_registered(module_dir)
    seen: dict[str, int] = {}
    for file_id, rel, file_type in rows:
        seen[file_id] = seen.get(file_id, 0) + 1
        target = os.path.join(module_dir, rel.lstrip("/\\").replace("/", os.sep))

        if file_id not in REQUESTED_IDS and file_type not in NATIVE_BY_TYPE:
            twin = MANAGED_TWIN.get(file_type)
            through = next((r for r in registered.get(_key(target), []) if r[0] == twin), None)
            if through:
                findings.append((
                    "WARN",
                    f"{'INERT-ROW':<11}{file_id}\n"
                    f"             -> {rel}\n"
                    f"             nothing requests this id and native does not read\n"
                    f"             type={file_type!r}, so the row is read by nothing. The file still\n"
                    f"             loads: SubModule.xml registers it as <XmlName id={through[0]!r}\n"
                    f"             path={through[1]!r}>, the id the engine loads {file_type} files by.\n"
                    f"             Harmless; it only reads as registration.",
                ))
            else:
                findings.append((
                    "ERROR",
                    f"{'DEAD-ID':<11}{file_id}\n"
                    f"             -> {rel}\n"
                    f"             nothing requests this id and native does not read\n"
                    f"             type={file_type!r} by row; the file may still load another way\n"
                    f"             (SubModule.xml, a fixed path native opens itself). {_dead_id_hint(twin)}",
                ))

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
        own_types = os.path.join(module_dir, "ModuleData", "action_types.xml")
        available = native_action_types(game_dir)
        parse_problems: list[str] = []
        declared, bad = action_attrs(own_types, "name")
        available.update(declared)
        parse_problems += bad
        for other in extra_type_dirs:  # the engine merges every loaded module's action types
            available.update(action_attrs(os.path.join(other, "ModuleData", "action_types.xml"), "name")[0])
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


def sibling_dirs(entry: str, modules_dir: str) -> list[str]:
    """The other TAOM modules' folders (TAOM from the repo), whose action_types.xml also declares the
    names this module binds. Kept when --module narrows the audit to one module."""
    return [os.path.join(REPO_ROOT, "Main", "_Module") if other == "TAOM" else os.path.join(modules_dir, other)
            for other in TAOM_MODULES if other != entry]


def is_official(module_dir: str) -> bool:
    """Whether the module's SubModule.xml says `<ModuleType value="Official">` or `OfficialOptional`
    (Native, SandBox, SandBoxCore, StoryMode, CustomBattle, BirthAndDeath, Multiplayer, NavalDLC on
    v1.5.5 and v1.5.6). Read from the install, not a list, so a new official module needs no edit here."""
    try:
        node = ET.parse(os.path.join(module_dir, "SubModule.xml")).getroot().find("ModuleType")
    except (OSError, ET.ParseError, ValueError, LookupError):
        return False
    return node is not None and (node.get("value") or "").strip() in ("Official", "OfficialOptional")


def _read_bytes(path: str) -> bytes | None:
    try:
        with open(path, "rb") as handle:
            return handle.read()
    except OSError:
        return None


def main() -> int:
    for stream in (sys.stdout, sys.stderr):  # a finding that names a decoded byte must not crash the run
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(errors="backslashreplace")
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
        help="audit every installed module, not just TAOM's three (official modules are skipped; other mods have their own gaps)",
    )
    args = parser.parse_args()

    modules_dir = os.path.join(args.game_dir, "Modules")
    if not os.path.isdir(modules_dir):
        print(f"Bannerlord Modules directory not found: {modules_dir}")
        print("Set $BANNERLORD_GAME_DIR or pass --game-dir. Nothing was audited.")
        return 2
    print(f"Requested ids: {len(REQUESTED_IDS)} (the exact set the engine asks for), "
          f"{len(NATIVE_BY_TYPE)} native types\n")

    named = bool(args.module)
    if args.module:
        names = [args.module]
    elif args.all:
        names = sorted(os.listdir(modules_dir))
        official = [n for n in names if is_official(os.path.join(modules_dir, n))]
        names = [n for n in names if n not in official]
        if official:
            print(f"Skipped official modules (ModuleType Official in SubModule.xml; they ship rows nothing "
                  f"requests and are not TAOM's to fix): {', '.join(official)}\n")
    else:
        names = list(TAOM_MODULES)
        print(f"Scope: {', '.join(names)}  (use --all to sweep every installed module)\n")

    findings: list[tuple[str, str]] = []
    audited = 0
    not_audited: list[str] = []
    for entry in names:
        deployed = os.path.join(modules_dir, entry)
        path, label = deployed, entry
        if entry == "TAOM":
            path = os.path.join(REPO_ROOT, "Main", "_Module")
        mbproj = os.path.join(path, "ModuleData", "project.mbproj")
        if not os.path.exists(mbproj):
            shown = REPO_MODULE_LABEL if entry == "TAOM" else mbproj
            if named:
                missing = entry != "TAOM" and not os.path.isdir(path)
                what = "is not installed" if missing else f"has no {shown}"
                print(f"Module {entry!r} {what}. Nothing was audited.")
                return 2
            if args.all and entry != "TAOM":
                continue  # most installed modules register nothing this way
            print(f"Skipped {entry}: no project.mbproj at {shown}")
            not_audited.append(entry)
            continue
        audited += 1
        if entry == "TAOM":
            deployed_mbproj = os.path.join(deployed, "ModuleData", "project.mbproj")
            other = _read_bytes(deployed_mbproj)
            note = f"audited the repo's {REPO_MODULE_LABEL} (the file authors edit); "
            if other is None:
                note += "the deployed Modules/TAOM copy is not deployed"
            elif other == _read_bytes(mbproj):
                note += "the deployed Modules/TAOM copy is identical"
            else:
                note += "the deployed Modules/TAOM copy differs"
                findings.append(("WARN", f"[TAOM] {'DEPLOYED-DIFFERS':<11}{deployed_mbproj}\n"
                                         f"             is not the repo's {REPO_MODULE_LABEL}: the\n"
                                         f"             audit judged the repo copy; build to deploy it."))
            print(f"TAOM: {note}\n")
        try:
            findings.extend(audit_module(path, args.game_dir, label, sibling_dirs(entry, modules_dir)))
        except AuditUnavailable as exc:
            print(f"ERROR: {exc}")
            return 2

    if audited == 0:
        print("No module with a ModuleData/project.mbproj was found in scope, so nothing was audited.")
        return 2

    errors = [m for sev, m in findings if sev == "ERROR"]
    warns = [m for sev, m in findings if sev == "WARN"]
    partial = bool(not_audited) and not args.all

    for message in warns:
        print(f"WARN  {message}")
    if warns:
        print()
    for message in errors:
        print(f"ERROR {message}")
    if errors:
        print()

    print(f"{audited} module(s) with a project.mbproj audited: {len(errors)} error(s), {len(warns)} warning(s)")
    if partial:
        print(f"Not audited: {', '.join(not_audited)} (no project.mbproj in the default scope)")
    if errors:
        print("\nA dead id is not a load-order problem and produces no engine error;")
        print("a BAD-FILE makes the engine throw at startup.")
        print("Ledger for the 2026-08-28 fix: docs/reference/lotrlome-soln-id-fix.md")
        return 1
    if partial:
        return 2
    print("No silently-ignored registrations." if not any("INERT-ROW" in w for w in warns)
          else "No dead registrations (the INERT-ROW warnings above are read by nothing but load through SubModule.xml).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
