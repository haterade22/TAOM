#!/usr/bin/env python3
"""Schema-driven validator for TAOM ModuleData — one reusable pass that
consolidates the recurring per-task validators (validate_all_troop_refs.py,
audit_item_refs.py, the equipmentType civilian PowerShell snippet, the
duplicate-id-across-Armory-folders checks) into a single cross-reference +
schema engine.

A schema/validation/cross-ref
architecture, ported to Python. The declarative schemas under tools/schemas/
are the source of truth; this CLI just wires registries to the engine and
prints a severity-classified report. See tools/taom_schema.py for the engine
and docs/features/moduledata-validation.md for the full design.

Checks (each maps to a recurring TAOM bug class):
  BROKEN_ITEM_REF            missing equipment item  -> "underwear bug"
  BROKEN_TROOP_REF           upgrade_target/culture points at a deleted troop
  UNKNOWN_CULTURE            stale culture rename / "rohan" instead of "vlandia"
  DUPLICATE_NPC_ID           same NPCCharacter id defined twice in TAOM
  MISSING_CIVILIAN_TYPE      civilian roster missing equipmentType="Civilian"
  DUPLICATE_ITEM_DEF         same Armory item id defined in >1 LOTRLOME_items folder
  DUPLICATE_CULTURE_ID       same Culture id defined twice in taom_spcultures.xml
  DUPLICATE_ROSTER_ID        same EquipmentRoster id defined twice
  INVALID_ENUM               default_group not Infantry/Ranged/Cavalry/HorseArcher
  BROKEN_PARTY_TEMPLATE_REF  PartyTemplate.* points at an undefined template (warning)
  BROKEN_BODY_PROPERTY_REF   face_key_template points at an undefined BodyProperty -> null face
  MISSING_HARNESS_FAMILY_TYPE  HorseHarness with no <Armor family_type> -> silently unequippable
  HARNESS_FAMILY_MISMATCH    Horse + HorseHarness in one set disagree on family type
  MOUNTED_DWARF              race="dwarf" tagged Cavalry/HorseArcher, or handed a mount
                             -> dwarf spawns inside the horse mesh (misaligned rider bone)
  GENERATOR_RETIRED_ITEM_REF a data generator under tools/ would WRITE an item id the
                             live install does not define (warning; needs the install;
                             the generator list and the walk live in
                             check_generator_item_refs.py)
  MISSING_COLLISION_BODY     an item or crafting piece whose body_name / holster body /
                             collision body names a PhysicsShape no loaded tpac ships.
                             PreloadHelper.WaitForMeshesToBeLoaded polls that name forever:
                             the #352 / #599 infinite mission load (needs the install;
                             the tpac scan lives in validate_mesh_refs.py)
  MISSING_VISUAL_MESH        same for a mesh / holster_mesh: an invisible item (warning)

Usage:
  python tools/validate_moduledata.py [--json report.json] [--warnings-as-errors]
  python tools/validate_moduledata.py --game-modules "E:/.../Modules"

Exit code: 1 if any ERROR (or any WARNING with --warnings-as-errors), else 0.
"""
import argparse
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import taom_schema as ts
# Aliased: main() binds a local `game_modules`, which would shadow the import.
from _gamedir import game_modules as resolve_game_modules

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
SCHEMA_DIR = Path(__file__).resolve().parent / "schemas"

# The commit hook runs this with no --game-modules, so the default is what it
# gets. A wrong one left BROKEN_ITEM_REF and BROKEN_TROOP_REF unable to fire
# while the hook still saw PASS (#404).
DEFAULT_GAME_MODULES = resolve_game_modules(
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
)


# TAOM's data lives in three modules and two of them are outside this repo and
# outside git: LOTRLOME_Armory (TAOM authors item XML straight into it, see
# /author-armor) and TAOM_Map. Sweeping only Main/_Module/ModuleData missed 28 of
# the 33 dangling refs the engine reported on 2026-08-02, which is why the Armory
# was added; TAOM_Map went unswept on the same reasoning nobody re-applied to it
# (#462). Cross-references ONLY -- TAOM's schema contracts describe TAOM's own
# files and must not report defects against a foreign module.
#
# TAOM_Map matters more than its file count suggests: its settlements.xml is the
# SOLE source of `settled_cultures`, so an unchecked bad `Culture.` id there
# corrupts the LANDLESS_CULTURE verdict (the #374 daily-clan-tick CTD guard) with
# no diagnostic at all. Neither module is in git, so the pre-commit hook cannot
# gate either one; this sweep is the only check they get.
_EXTRA_REF_MODULES = ("LOTRLOME_Armory", "TAOM_Map")


def build_extra_ref_roots(game_modules) -> list:
    """ModuleData roots outside this repo that are swept for dangling refs.

    Returns [] when there is no game install, so a missing install degrades to a
    TAOM-only sweep rather than raising. A root that is listed but absent is
    reported via `Validator.missing_ref_roots`, never silently dropped.
    """
    game_modules = Path(game_modules)
    if not game_modules.exists():
        return []
    return [game_modules / name / "ModuleData" for name in _EXTRA_REF_MODULES]


GENERATOR_CODE = "GENERATOR_RETIRED_ITEM_REF"


def generator_item_ref_issues(items: set) -> list:
    """WARNING per generator under tools/ that would write an item id `items` lacks.

    The XML this validator sweeps is the OUTPUT of those scripts; a retired id
    sitting in one of their tables is invisible to every other pass until
    somebody re-runs the script and a troop spawns naked (or a battle hangs on a
    missing collision body, #352). On 2026-09-13 seven generators held 67 such
    ids between them. The generator list, the run/import modes and the table
    walk live in check_generator_item_refs.py; this only turns its findings into
    issues. Callers skip it without the install: the registry is TAOM-only
    then, and every Armory id would read as retired.

    A generator that cannot be read at all is reported under the same code, so
    a broken script never passes as a clean one."""
    import check_generator_item_refs as cg
    issues = []
    for spec in cg.GENERATORS:
        try:
            refs = cg.collect(spec)
        except Exception as exc:  # noqa: BLE001 - any failure is a finding, never a pass
            issues.append(ts.Issue(
                severity=ts.Severity.WARNING, code=GENERATOR_CODE, file=spec.path, line=0,
                entry_id="", message=f"generator could not be checked ({exc}); its item "
                                     f"ids were NOT verified this run"))
            continue
        missing = sorted(r for r in refs if r not in items)
        if missing:
            shown = ", ".join(missing[:8]) + (f", +{len(missing) - 8} more" if len(missing) > 8 else "")
            issues.append(ts.Issue(
                severity=ts.Severity.WARNING, code=GENERATOR_CODE, file=spec.path, line=0,
                entry_id="",
                message=f"{len(missing)} of {len(refs)} item ids this generator writes resolve to "
                        f"no item in the live install ({shown}). A re-run would leave those "
                        f"slots empty. Replace them with ids the Armory defines; "
                        f"python tools/check_generator_item_refs.py lists them all"))
    return issues


BODY_CODE = "MISSING_COLLISION_BODY"
MESH_CODE = "MISSING_VISUAL_MESH"
# Every module whose packs the client loads for item art. The Armory ships loose
# Assets/**/*.tpac and no cooked AssetPackages (2026-09); the vanilla three ship
# cooked packs. validate_mesh_refs falls back the same way.
_ART_MODULES = ("LOTRLOME_Armory", "Native", "SandBoxCore", "SandBox")


def _loaded_tpacs(game_modules: Path) -> list:
    out = []
    for name in _ART_MODULES:
        mod = game_modules / name
        cooked = sorted((mod / "AssetPackages").glob("*.tpac"))
        out += cooked if cooked else sorted((mod / "Assets").rglob("*.tpac"))
    return out


def missing_collision_body_issues(game_modules: Path, moduledata: Path) -> list:
    """ERROR per collision-body ref no loaded tpac ships, WARNING per visual mesh.

    A body the engine cannot resolve is not a warning class: `PreloadHelper.
    WaitForMeshesToBeLoaded` (TaleWorlds.MountAndBlade.View) counts every
    registered body name that `PhysicsShape.GetFromResource` returns null for,
    on every pass of a do/while with no exit, so one bad `body_name` on any item
    a mission preloads (every participant's kit, the player's first) spins the
    game thread forever. Two body_name typos did it in #352; the 2026-09-11 art
    drop that renamed the elven bows did it again in #599, and the elf start
    hung for two days because the only gate for it was a separate command
    nobody ran after the sync. This pass makes it part of the one validator the
    commit hook, the MCP server and /verify already run.

    validate_mesh_refs.py owns the ref extraction and the tpac TOC scan (Tier B
    visual meshes, Tier C PhysicsShapes); this only turns its findings into
    issues, over the Armory's WHOLE ModuleData plus this repo's (the #352 scope
    lesson: crafting pieces live one level above LOTRLOME_items/). Its
    KNOWN_DEAD_MESH allowlist and UNVERIFIED downgrades stay in that tool.

    Skipped, never faked, without the install; and a run that found no packs to
    scan is reported as a finding, because "every body missing" filtered to
    nothing would read exactly like a clean run."""
    import validate_mesh_refs as vmr
    tpacs = _loaded_tpacs(game_modules)
    if not tpacs:
        return [ts.Issue(
            severity=ts.Severity.ERROR, code=BODY_CODE, file="", line=0, entry_id="",
            message=f"no *.tpac found under {' / '.join(_ART_MODULES)} in {game_modules}; "
                    f"collision bodies were NOT verified this run")]
    refs = vmr.extract_refs(game_modules / "LOTRLOME_Armory" / "ModuleData")
    if moduledata.exists():
        refs += vmr.extract_refs(moduledata)
    present = vmr.build_present_set(tpacs)
    issues = []
    for i in vmr.classify(refs, present, None, scan_bodies=True, body_tpac_paths=tpacs):
        if i.code == "MISSING_BODY":
            issues.append(ts.Issue(
                severity=ts.Severity.ERROR, code=BODY_CODE, file=i.file, line=i.line, entry_id=i.entry_id,
                message=f"{i.message}. Every mission that preloads a carrier of this item spins "
                        f"forever in PreloadHelper.WaitForMeshesToBeLoaded (#352, #599). Repoint the "
                        f"ref to the art that ships; python tools/audit_armory_refs.py names the troops"))
        elif i.code == "MISSING_MESH":
            issues.append(ts.Issue(
                severity=ts.Severity.WARNING, code=MESH_CODE, file=i.file, line=i.line, entry_id=i.entry_id,
                message=f"{i.message}. The item renders invisible; repoint the mesh to the art that ships"))
    return issues


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game-modules", default=str(DEFAULT_GAME_MODULES),
                    help="Path to the Bannerlord Modules folder (for the item/culture/troop registry)")
    ap.add_argument("--moduledata", default=str(MODULEDATA),
                    help="Path to TAOM Main/_Module/ModuleData")
    ap.add_argument("--json", dest="json_out", default=None,
                    help="Write the full issue list to this JSON file")
    ap.add_argument("--warnings-as-errors", action="store_true",
                    help="Exit non-zero if any WARNING is found, not just ERROR")
    ap.add_argument("--code", action="append", default=None,
                    help="Only report this issue code (repeatable)")
    args = ap.parse_args()

    moduledata = Path(args.moduledata)
    game_modules = Path(args.game_modules)

    if not moduledata.exists():
        print(f"ERROR: ModuleData not found: {moduledata}", file=sys.stderr)
        return 2
    if not game_modules.exists():
        # Per environment-failures.md: report, don't guess. The item/culture
        # registry will be TAOM-only, so external refs will look broken.
        print(f"WARNING: Bannerlord Modules folder not found: {game_modules}\n"
              f"         item / troop / party-template ref checks will be SKIPPED (need the\n"
              f"         game install for a complete registry). Culture-validity, duplicate-id,\n"
              f"         civilian-type and enum checks still run. Set $BANNERLORD_GAME_DIR or\n"
              f"         pass --game-modules <path> to enable the skipped checks.\n"
              f"         This run will exit 2 (bad input), not 0 — a degraded sweep must not\n"
              f"         report PASS as though it had checked everything.", file=sys.stderr)

    schemas = ts.load_schemas(SCHEMA_DIR)
    print(f"Loaded {len(schemas)} schemas:", file=sys.stderr)
    for s in schemas:
        print(f"  - {s.name}: {s.description}", file=sys.stderr)

    registries = ts.build_registries(moduledata, game_modules if game_modules.exists() else None)
    print(f"Registry: {len(registries.items):,} items, "
          f"{len(registries.npccharacters):,} NPCCharacters, "
          f"{len(registries.cultures)} cultures, "
          f"{len(registries.party_templates):,} party templates, "
          f"{len(registries.body_properties)} body properties", file=sys.stderr)

    extra_roots = build_extra_ref_roots(game_modules)

    validator = ts.Validator(moduledata, schemas, registries, extra_ref_roots=extra_roots)
    for root in validator.extra_ref_roots:
        print(f"Also sweeping refs in: {root}", file=sys.stderr)
    # Never let a vanished root pass as a clean run. Silence here would revert the
    # sweep to TAOM-only and still print PASS -- the exact state that hid 28 of the
    # 33 dangling refs the engine reported on 2026-08-02.
    for root in validator.missing_ref_roots:
        print(f"WARNING: extra ref root NOT FOUND, sweep SKIPPED for {root.parent.name}: {root}\n"
              f"         Cross-references in that module were NOT checked this run.",
              file=sys.stderr)
    for warning in registries.suspect_registries:
        print(f"WARNING: {warning}", file=sys.stderr)

    issues = validator.run()
    if game_modules.exists():
        issues += generator_item_ref_issues(registries.items)
        issues.sort(key=lambda i: i.sort_key())
    else:
        print(f"WARNING: {GENERATOR_CODE} SKIPPED: the tools/ generators' item ids can only be\n"
              f"         checked against the live install.", file=sys.stderr)

    if args.code:
        wanted = set(args.code)
        issues = [i for i in issues if i.code in wanted]

    print(ts.format_report(issues))

    if args.json_out:
        payload = [{
            "severity": i.severity.value, "code": i.code, "file": i.file,
            "line": i.line, "entry_id": i.entry_id, "message": i.message,
        } for i in issues]
        Path(args.json_out).write_text(json.dumps(payload, indent=2), encoding="utf-8")
        print(f"\nWrote {len(payload)} issues to {args.json_out}", file=sys.stderr)

    n_err = sum(1 for i in issues if i.severity is ts.Severity.ERROR)
    n_warn = sum(1 for i in issues if i.severity is ts.Severity.WARNING)
    if n_err or (args.warnings_as_errors and n_warn):
        return 1
    if not game_modules.exists():
        # PASS from a registry with no items and no NPCCharacters means the two
        # ref sweeps never ran, and the commit hook cannot tell that apart from
        # a real pass. 2 is bad-input, which the hook already fails open on, so
        # nothing starts blocking that did not block before.
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
