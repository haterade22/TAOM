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

    # TAOM authors item XML directly into LOTRLOME_Armory (see /author-armor), so
    # its files are TAOM's to keep correct even though they live outside this repo
    # and outside git. Sweeping only Main/_Module/ModuleData missed 28 of the 33
    # dangling refs the engine reported on 2026-08-02. Cross-references only --
    # TAOM's schema contracts are not applied to a foreign module.
    extra_roots = []
    if game_modules.exists():
        extra_roots.append(game_modules / "LOTRLOME_Armory" / "ModuleData")

    validator = ts.Validator(moduledata, schemas, registries, extra_ref_roots=extra_roots)
    for root in validator.extra_ref_roots:
        print(f"Also sweeping refs in: {root}", file=sys.stderr)
    # Never let a vanished root pass as a clean run. Silence here would revert the
    # sweep to TAOM-only and still print PASS -- the exact state that hid 28 of the
    # 33 dangling refs the engine reported on 2026-08-02.
    for root in validator.missing_ref_roots:
        print(f"WARNING: extra ref root NOT FOUND — Armory sweep SKIPPED: {root}\n"
              f"         Cross-references in that module were NOT checked this run.",
              file=sys.stderr)
    for warning in registries.suspect_registries:
        print(f"WARNING: {warning}", file=sys.stderr)

    issues = validator.run()

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
