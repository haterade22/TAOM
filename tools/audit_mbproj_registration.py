#!/usr/bin/env python3
"""Audit every module's `project.mbproj` for registrations the engine silently ignores.

A `<file>` row in `project.mbproj` only ever loads if something ASKS for its `id`.
`MBObjectManager.GetMergedXmlForNative(id)` walks `XmlResource.MbprojXmls` and keeps the
entries whose `Id` matches EXACTLY; it is reached from eight hardcoded ids in
`Module.CreateProcessed*XMLForNative` plus one native callback that builds the id as
`"soln_" + xmlType`. Native only ever passes the standard type names. So an invented id
matches nothing, no file is read, and there is no error: the row looks like registration
and is inert.

That is not hypothetical. The giant spider shipped `soln_spider_*` ids in 2026-06, its
action_sets and monster_usage_set never loaded, `GetMonsterUsageIndex("spider")` returned
-1 and native `CreateAgent` divided by zero on spawn. The lesson was written into a comment
at the top of the Armory's own project.mbproj, and two custom-id rows still survived it
until 2026-08-28: `soln_spider_monster` (inert but harmless, the Monster loads the managed
way via SubModule.xml) and `soln_lotr_misc_action_types`, whose 20 action declarations had
therefore never reached the engine while `action_sets.xml` bound them 221 times.

This is the regression gate for that class. `LOTRLOME_Armory` is a separate, untracked
module, so a reinstall silently reverts the fix; run this to find out.

Four checks:
  DEAD-ID    a `soln_*` id outside the vanilla vocabulary  -> the file never loads  (ERROR)
  UNDECLARED an `<action type=>` bound in action_sets.xml, declared nowhere (ERROR)
  MERGE-RISK a duplicated id that HAS an XSD  -> takes the MergeElements path  (WARN)
  MISSING    a `<file name=>` that is not on disk  (WARN)

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
import re
import sys
import xml.etree.ElementTree as ET

DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

# The modules TAOM's own data surface spans (CLAUDE.md "Three-module data surface").
# Anything else installed is somebody else's module and is only audited under --all.
TAOM_MODULES = ("TAOM", "TAOM_Map", "LOTRLOME_Armory")

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


def strip_comments(text: str) -> str:
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


def read(path: str) -> str:
    with open(path, encoding="utf-8-sig") as handle:
        return handle.read()


def declared_actions(path: str) -> set[str]:
    """Action names declared in an action_types.xml, ignoring commented-out rows."""
    if not os.path.exists(path):
        return set()
    return set(re.findall(r'<action\s+name="([^"]+)"', strip_comments(read(path))))


def vanilla_ids(game_dir: str) -> set[str]:
    """The only `soln_*` vocabulary the engine ever requests, taken from Native itself."""
    native = os.path.join(game_dir, "Modules", "Native", "ModuleData", "project.mbproj")
    if not os.path.exists(native):
        return set()
    return set(re.findall(r'id="(soln_[a-z_]+)"', strip_comments(read(native))))


def audit_module(module_dir: str, game_dir: str, known_ids: set[str]) -> list[tuple[str, str]]:
    """Return (severity, message) pairs for one module. Severity is ERROR or WARN."""
    name = os.path.basename(module_dir.rstrip("\\/"))
    mbproj = os.path.join(module_dir, "ModuleData", "project.mbproj")
    if not os.path.exists(mbproj):
        return []

    findings: list[tuple[str, str]] = []
    body = strip_comments(read(mbproj))
    rows = re.findall(r'<file\s+id="([^"]+)"\s+name="([^"]+)"', body)

    seen: dict[str, int] = {}
    for file_id, rel in rows:
        seen[file_id] = seen.get(file_id, 0) + 1

        if file_id.startswith("soln_") and file_id not in known_ids:
            findings.append((
                "ERROR",
                f"DEAD-ID    {file_id}\n"
                f"             -> {rel}\n"
                f"             nothing ever requests this id, so the file never loads.\n"
                f"             Fold its content into the standard-id file instead.",
            ))

        target = os.path.join(module_dir, rel.replace("/", os.sep))
        if not os.path.exists(target):
            findings.append(("WARN", f"MISSING    {file_id} -> {rel} is not on disk"))

    for file_id, count in sorted(seen.items()):
        if count > 1 and os.path.exists(os.path.join(game_dir, "XmlSchemas", f"{file_id}.xsd")):
            findings.append((
                "WARN",
                f"MERGE-RISK {file_id} appears {count}x and {file_id}.xsd EXISTS,\n"
                f"             so the extra rows take the MergeElements path\n"
                f"             (KeyNotFoundException at startup, elephant Crash #3).",
            ))

    sets_path = os.path.join(module_dir, "ModuleData", "action_sets.xml")
    if os.path.exists(sets_path):
        native_types = os.path.join(game_dir, "Modules", "Native", "ModuleData", "action_types.xml")
        available = declared_actions(os.path.join(module_dir, "ModuleData", "action_types.xml"))
        available |= declared_actions(native_types)
        bound = re.findall(r'<action\s+type="([^"]+)"', strip_comments(read(sets_path)))
        baseline = UNDECLARED_BASELINE.get(name, set())
        unresolved = sorted({a for a in bound if a not in available} - baseline)
        for action in unresolved:
            findings.append((
                "ERROR",
                f"UNDECLARED {action} is bound {bound.count(action)}x in action_sets.xml\n"
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

    known = vanilla_ids(args.game_dir)
    if not known:
        print("Could not read Native/ModuleData/project.mbproj; cannot establish the id vocabulary.")
        return 0
    print(f"Vanilla soln_* vocabulary: {len(known)} ids\n")

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
        findings.extend(audit_module(path, args.game_dir, known))

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
        print("\nA dead id is not a load-order problem and produces no engine error.")
        print("Ledger for the 2026-08-28 fix: docs/reference/lotrlome-soln-id-fix.md")
        return 1
    print("No silently-ignored registrations.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
