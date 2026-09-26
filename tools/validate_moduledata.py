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
  SKILL_TEMPLATE_MISMATCH    an inline <skills> row beside a skill_template that differs from
                             the SkillSet, or a template with rows naming no SkillSet. Since
                             1.5.2 the row wins in game while the SkillSet and every tool
                             say otherwise (#626; needs the install; detection lives in
                             sync_lord_inline_skills.py, which also repairs it)
  SCHEMA_INVALID             a file the repo module registers breaks the engine's own XSD
                             for its id, or a SubModule.xml registration loads nothing. The
                             engine only logs the former and loads the file anyway (repo
                             module only; the XSD layer lives in validate_xml_schemas.py;
                             one warning when lxml or <game>/XmlSchemas is absent)

Usage:
  python tools/validate_moduledata.py [--json report.json] [--warnings-as-errors]
  python tools/validate_moduledata.py --game-modules "E:/.../Modules"

Exit code: 1 if any ERROR (or any WARNING with --warnings-as-errors), else 0.
"""
import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
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
# Every module whose packs the client loads for item art; which tree of each the
# engine reads is validate_mesh_refs.tpac_paths_for_modules's to decide.
_ART_MODULES = ("LOTRLOME_Armory", "Native", "SandBoxCore", "SandBox")


def _loaded_tpacs(game_modules: Path) -> list:
    import validate_mesh_refs as vmr
    return [p for m in _ART_MODULES for p in vmr.module_tpacs(Path(game_modules) / m, m)]


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
    nobody ran after the sync. This pass makes it part of the validator the
    commit hook runs (wired into main() by #622; the MCP tool and /verify do not
    run it yet, #623).

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
    try:
        refs = vmr.extract_refs(game_modules / "LOTRLOME_Armory" / "ModuleData")
        if moduledata.exists():
            refs += vmr.extract_refs(moduledata)
        present = vmr.build_present_set(tpacs)
        # A parsed pack's TOC already lists its bodies (#352), so only a pack that failed to
        # parse can hide one. Byte-scanning all of them took 110-119 s (4,611 packs, 22.6 GiB),
        # past the commit hook's 45 s bound, in exactly the #599 case this gate exists for.
        unparsed = [Path(p) for p, _ in present.unparsed]
        findings = vmr.classify(refs, present, None, scan_bodies=True, body_tpac_paths=unparsed)
    except Exception as exc:  # noqa: BLE001 - a scan that raised has verified nothing; say so
        return [ts.Issue(
            severity=ts.Severity.ERROR, code=BODY_CODE, file="", line=0, entry_id="",
            message=f"collision bodies were NOT verified this run: the scan raised "
                    f"{type(exc).__name__}: {exc}")]
    issues = []
    for i in findings:
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


BORROWED_BODY_CODE = "COLLISION_BODY_BORROWED"
_SHIELD_ATTRS = {"shield_body_name"}
# Body sharing that is design, not a borrow. Each entry: (borrower kit regex, owner kit regex,
# reason). A kit is the first two tokens of a name after its sm_/wm_/bo_/bo_cap_ prefix.
_SHARED_BODY_BY_DESIGN = (
    (re.compile(r"^(?:rh_drag|dg_khml|rh_loke)$"), re.compile(r"^(?:rh_drag|dg_khml|rh_loke)$"),
     "Dragon and Khamul are re-textured Loke geometry; the three Rhun kits share bodies "
     "(Mike, 2026-09-21)"),
)
_ART_PREFIX_RE = re.compile(r"^(?:bo_cap_|bo_)?(?:sm_|wm_)?")


def _kit(name: str) -> str:
    """The kit a mesh or body belongs to: its first two name tokens after the art prefixes.
    `sm_rh_drag_sword_blade_a` -> `rh_drag`, `bo_wm_elven_bow_a03` -> `elven_bow`,
    `wm_rivendell_sword_a01_silver_blade` -> `rivendell_sword`."""
    return "_".join(_ART_PREFIX_RE.sub("", name).split("_")[:2])


def _twin_owner(body: str, meshes: set):
    """The mesh this body is the twin of, by the `bo_<mesh>` / `bo_cap_<mesh>` convention, or
    None when no shipped mesh matches (a body under a variant name is nobody's twin)."""
    for prefix in ("bo_cap_", "bo_"):
        if body.startswith(prefix) and body[len(prefix):] in meshes:
            return body[len(prefix):]
    return None


def borrowed_body_issues(game_modules: Path, moduledata: Path) -> list:
    """ERROR per weapon or crafting piece whose collision body is another kit's twin (#633).

    The convention (docs/ai-includes/weapon-creation-workflow.md, Step D) is that a weapon body
    is `bo_` + the exact mesh id, authored in the mesh's own FBX. That doc also sanctions
    borrowing a same-shaped body from elsewhere as a placeholder until the artist delivers.
    Three Rhun longbow meshes shipped on that placeholder for good, all carrying the elven bow's
    `bo_wm_elven_bow_a03`, and no gate noticed because the borrowed name resolves: every "does it
    resolve" check answers yes for a name that ships anywhere. This asks the other question: is
    the body this mesh's own?

    A borrow is a body that is provably ANOTHER mesh's twin: `bo_<M2>` or `bo_cap_<M2>` for a
    shipped mesh M2 that is not this item's mesh. A body under a variant name
    (`bo_uruk_halberd_blade_a1` for `sm_uruk_halberd_blade_a1`) is nobody's twin and passes.
    Sharing a body WITHIN a kit is design, not a borrow: the ruby and topaz Aranruth blades, the
    silver and black Rivendell swords, the `_a2` Erebor axe on `_a`'s body, all share one
    geometry, so a borrow from the same kit passes, and `_SHARED_BODY_BY_DESIGN` names the
    cross-kit shares that are authorised (Dragon and Khamul are re-textured Loke). Vanilla bodies
    are exempt (Native is always resident, and vanilla art is never a placeholder for ours);
    shields are exempt (they carry `bo_cap_*` in `body_name` and share capsules by convention,
    docs/modding/items-shields.md); a body no pack ships is left to MISSING_COLLISION_BODY.

    Measured 2026-09-21 on the live install after the #633 repair: 0 items, 58 crafting pieces
    borrow, 38 of them the Rhun family and 20 within one kit, so 0 findings. The nine #633 items
    (three donors, six generated clones) fire under this rule. Why the player's build hung is a
    separate, open question: the cooked release on this machine ships the borrowed body in its
    pack0, so the borrow resolved there too, and the mechanism this gate was first written on (a
    body cooked into a different AssetPackage than its mesh) is false: the cook puts every body in
    pack0/pack1 and every mesh elsewhere, for the working items as much as the broken ones.

    Skipped, never faked, without the install. Reuses validate_mesh_refs.py for the ref
    extraction and the TOC scan, like missing_collision_body_issues."""
    import validate_mesh_refs as vmr
    armory_root = str(game_modules / "LOTRLOME_Armory")
    tpacs = [p for p in _loaded_tpacs(game_modules) if str(p).startswith(armory_root)]
    if not tpacs:
        return [ts.Issue(
            severity=ts.Severity.ERROR, code=BORROWED_BODY_CODE, file="", line=0, entry_id="",
            message=f"no LOTRLOME_Armory *.tpac found under {game_modules}; collision-body "
                    f"ownership was NOT verified this run")]
    try:
        present = vmr.build_present_set(tpacs)
        meshes = {n.lower() for n in present.metameshes}
        bodies = {n.lower() for n in present.physicsshapes}
        refs = vmr.extract_refs(game_modules / "LOTRLOME_Armory" / "ModuleData")
        if moduledata.exists():
            refs += vmr.extract_refs(moduledata)
    except Exception as exc:  # noqa: BLE001 - a scan that raised has verified nothing; say so
        return [ts.Issue(
            severity=ts.Severity.ERROR, code=BORROWED_BODY_CODE, file="", line=0, entry_id="",
            message=f"collision-body ownership was NOT verified this run: the scan raised "
                    f"{type(exc).__name__}: {exc}")]

    primary: dict = {}
    shields: set = set()
    for r in refs:
        key = (r.file, r.item_id)
        if r.kind == "visual_mesh" and r.attr == "mesh":
            primary[key] = r.name.lower()
        if r.attr in _SHIELD_ATTRS:
            shields.add(key)

    issues = []
    for r in refs:
        # holster_body_name is a third body the engine polls; it is not paired with a mesh
        # today (no ref of it borrows, measured 2026-09-21) and stays MISSING_COLLISION_BODY's.
        if r.attr != "body_name":
            continue
        key = (r.file, r.item_id)
        if key in shields:
            continue
        body = r.name.lower()
        # Not an Armory body: vanilla (always resident, never our placeholder) or missing.
        if body not in bodies:
            continue
        mesh = primary.get(key)
        if not mesh or body in (f"bo_{mesh}", f"bo_cap_{mesh}"):
            continue
        owner = _twin_owner(body, meshes)
        if owner is None or owner == mesh or _kit(owner) == _kit(mesh):
            continue
        if any(b.match(_kit(mesh)) and o.match(_kit(owner)) for b, o, _ in _SHARED_BODY_BY_DESIGN):
            continue
        issues.append(ts.Issue(
            severity=ts.Severity.ERROR, code=BORROWED_BODY_CODE, file=r.file, line=r.line,
            entry_id=r.item_id,
            message=f"collision body {r.name!r} is {owner!r}'s twin, borrowed onto mesh {mesh!r} "
                    f"from another kit. It loads, as that other weapon's hull, and it is tied to "
                    f"art that can be renamed or retired without this item (#599, #633). Author "
                    f"bo_{mesh} into the mesh's own FBX (tools/blender/add_collision_body.py, "
                    f"then a Modding Kit import), or add the pair to _SHARED_BODY_BY_DESIGN "
                    f"with a reason"))
    return issues



TEMPLATE_CODE = "SKILL_TEMPLATE_MISMATCH"


def skill_template_mismatch_issues(game_dir: Path, moduledata: Path) -> list:
    """ERROR per character whose inline <skills> rows differ from its skill_template (#626).

    Since v1.5.2 BasicCharacterObject.Deserialize copies the template into a fresh
    MBCharacterSkills and lays the inline rows over it, so a differing row silently changes the
    character while every tool that reads the SkillSet sees the template's number (64 lords in
    characters/lords.xml and 19 in lords.xslt had drifted when the bump landed, 331032a1). v1.4.8
    discarded the inline block whenever a skill_template attribute was present, which is what the
    old SKILL_TEMPLATE_SHADOWS_SKILLS enforced by refusing any character that declared both. The
    rule now: both may be declared, and they must agree; the SkillSet is the source of truth. A
    template with inline rows that names no SkillSet is an error too (the engine creates an empty
    placeholder, so the rows are the character's only skills).

    Detection is tools/sync_lord_inline_skills.py's sync_file in report mode (vanilla SkillSets
    first, then TAOM's, merged the way MBObjectManager merges a duplicate id; comments blanked),
    over every XML and XSLT file in the repo module's ModuleData, so the fixer and the gate cannot
    disagree. Finding no templated character at all is an error, as LordInlineSkillParityTests
    asserts. Callers skip it without the install: the spc_* rookie templates live in SandBox. The
    MCP's validate_moduledata runs the Validator only and never reaches this pass (#623)."""
    import sync_lord_inline_skills as sl

    def issue(file, line, entry_id, message):
        return ts.Issue(severity=ts.Severity.ERROR, code=TEMPLATE_CODE, file=file, line=line,
                        entry_id=entry_id, message=message)

    try:
        sets = sl.load_skill_sets(str(game_dir), str(moduledata))
    except Exception as exc:  # noqa: BLE001 - any failure is a finding, never a pass or a traceback
        return [issue("", 0, "", f"the SkillSet files could not be read ({exc}); templates were NOT "
                                 f"checked this run")]
    if not sets:
        return [issue("", 0, "", f"found no SkillSet definitions under {game_dir} or {moduledata}, "
                                 f"so no template was compared; a gate that compared nothing is not a pass")]
    issues = []
    checked = 0
    for path in sl.data_files(str(moduledata)):
        rel = Path(path).relative_to(moduledata).as_posix()
        try:
            scan = sl.sync_file(path, sets)
        except UnicodeDecodeError as exc:
            issues.append(issue(rel, 0, "", f"not valid UTF-8 ({exc.reason} at byte {exc.start}), so its "
                                            f"characters were NOT checked against their skill_template"))
            continue
        except Exception as exc:  # noqa: BLE001 - any failure is a finding, never a pass or a traceback
            issues.append(issue(rel, 0, "", f"could not be scanned ({exc}); its characters were NOT "
                                            f"checked against their skill_template this run"))
            continue
        checked += scan.checked
        by_char: dict = {}
        for d in scan.drifts:
            by_char.setdefault((d.char_id, d.line), []).append(d.describe())
        for (char_id, line), drifts in by_char.items():
            issues.append(issue(rel, line, char_id,
                                f"inline <skills> rows disagree with the skill_template: {'; '.join(drifts)}. "
                                f"The engine (1.5.2+) applies the inline value over the template, so the "
                                f"character plays with it while the SkillSet and every tool reading it say "
                                f"otherwise. The SkillSet is the source of truth: python "
                                f"tools/sync_lord_inline_skills.py --apply"))
        for char_id, line, template in scan.unresolved:
            issues.append(issue(rel, line, char_id,
                                f'skill_template="{template}" names no SkillSet in the install or the repo, '
                                f"so the engine creates an empty placeholder and the inline rows are the "
                                f"character's only skills. Fix the id or add the SkillSet; the sync tool "
                                f"leaves this case alone"))
    if not checked:
        issues.append(issue("", 0, "", f"no templated character with inline <skills> rows was found under "
                                       f"{moduledata}, so nothing was compared; a gate that compared nothing "
                                       f"is not a pass"))
    return issues


SCHEMA_CODE = "SCHEMA_INVALID"


def schema_invalid_issues(module_dir: Path, game_schemas: Path) -> list:
    """ERROR per engine-XSD violation in a file the module registers, and per registration
    that loads nothing; ONE warning when the layer could not be checked.

    Every other pass here reads refs and TAOM's own JSON schemas, never the XSD the
    engine loads a file with, so a clan with no `initial_home_settlement` passed them all
    (`clan_umbar_3`, 2026-06-22). The engine validates, but only logs a failure and loads
    the file anyway. validate_xml_schemas.py owns the engine's file resolution and schema
    choice; this only turns its report into issues. Scope is the module given (the repo's
    Main/_Module), like the other schema passes: the live modules are unversioned and
    their known findings are not this repo's to fix.

    Skipped, never faked: without lxml, the engine's XmlSchemas folder or a SubModule.xml
    the pass reports that the layer was NOT verified, because silence here would read
    exactly like a clean run."""
    import validate_xml_schemas as vx
    module_dir = Path(module_dir)
    submodule = module_dir / "SubModule.xml"
    if not vx.HAVE_LXML:
        unverified = "lxml is not installed"
    elif not Path(game_schemas).is_dir():
        unverified = f"the engine schema folder {game_schemas} was not found"
    elif not submodule.is_file():
        unverified = f"there is no {submodule}"
    else:
        unverified = None
    if unverified:
        return [ts.Issue(
            severity=ts.Severity.WARNING, code=SCHEMA_CODE, file="", line=0, entry_id="",
            message=f"{unverified}, so the engine-XSD layer (tools/validate_xml_schemas.py) "
                    f"was NOT verified this run")]
    try:
        report = vx.run([module_dir], Path(game_schemas))
    except ET.ParseError as exc:
        return [ts.Issue(
            severity=ts.Severity.ERROR, code=SCHEMA_CODE, file="SubModule.xml", line=exc.position[0],
            entry_id="", message=f"SubModule.xml is not well-formed ({exc}); the engine throws on "
                                 f"it at startup")]

    moduledata = module_dir / "ModuleData"
    issues = []
    for rec in report["failed"]:
        path = Path(rec["file"])
        rel = path.relative_to(moduledata).as_posix() if moduledata in path.parents else str(path)
        for error in rec["errors"]:
            m = re.match(r"L(\d+): (.*)", error, flags=re.S)
            line, text = (int(m.group(1)), m.group(2)) if m else (0, error)
            issues.append(ts.Issue(
                severity=ts.Severity.ERROR, code=SCHEMA_CODE, file=rel, line=line, entry_id=rec["id"],
                message=f"{text} (engine schema {Path(rec['schema']).name}; the engine only logs "
                        f"this and loads the file anyway). python tools/validate_xml_schemas.py {rel}"))
    for text in report["missing"]:
        issues.append(ts.Issue(
            severity=ts.Severity.ERROR, code=SCHEMA_CODE, file="SubModule.xml", line=0, entry_id="",
            message=text))
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
    # The engine's XmlSchemas folder sits beside Modules in the install root.
    issues += schema_invalid_issues(moduledata.parent, game_modules.parent / "XmlSchemas")
    if game_modules.exists():
        issues += generator_item_ref_issues(registries.items)
        # Until #622 nothing called this pass, so MISSING_COLLISION_BODY never fired and the
        # commit hook's --code line for it blocked nothing.
        issues += missing_collision_body_issues(game_modules, moduledata)
        issues += borrowed_body_issues(game_modules, moduledata)
        # The install root: the vanilla SkillSets the spc_* templates name live in SandBox.
        issues += skill_template_mismatch_issues(game_modules.parent, moduledata)
    else:
        print(f"WARNING: {GENERATOR_CODE} SKIPPED: the tools/ generators' item ids can only be\n"
              f"         checked against the live install.", file=sys.stderr)
        print(f"WARNING: {BODY_CODE} SKIPPED: collision bodies can only be checked against the\n"
              f"         live install's tpacs.", file=sys.stderr)
        print(f"WARNING: {TEMPLATE_CODE} SKIPPED: the vanilla SkillSets a template can name live\n"
              f"         in the install.", file=sys.stderr)
    issues.sort(key=lambda i: i.sort_key())

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
