#!/usr/bin/env python3
"""Run the data gates for the files that changed, and say PASS, FAIL or NOT RUN for each.

Authors run one gate, see it pass and declare the work clean, while the gate they skipped was the
one that read their file. Claude's hooks run only inside Claude Code, but a clone with `core.hooksPath`
set to `.githooks` runs the commit gates on a plain `git commit` too; without it, none run. This
script owns the kind table: which gates read which files, the command, and the doc to read first.
Lens 7 of /deep-review, /verify and the artist brief send authors here.

Usage:
  python tools/data_gates.py                      # gates for files changed in git (vs HEAD, plus untracked)
  python tools/data_gates.py <file> [<file> ...]  # also the gates for these files; name the LIVE
                                                  # Armory / TAOM_Map files you edited (git cannot see them)
  python tools/data_gates.py --all                # every gate, whatever changed
  python tools/data_gates.py --list               # the table, nothing run
  python tools/data_gates.py --with-tests         # also run the dotnet test gates (never with the game
                                                  # running unless -p:ModuleId= is kept in the command)

Verdict per gate:
  PASS     exit 0, nothing skipped.
  FAIL     a non-zero exit other than 2 (1 is a finding, 3 a crash), or a timeout.
  NOT RUN  exit 2 (no install, no lxml, bad input), exit-0 output that says it checked nothing (SKIPPED,
           a SKIP line, skipped=N, lxml not installed, 0 files validated), a dotnet gate without
           --with-tests (its command is printed), or a manual step. NOT RUN has not passed.

Exit code: 1 if any gate FAILed, else 2 if any NOT RUN, else 0. A run that selects no gate exits 0. A git
failure, or a named file that does not exist or that two live modules both hold, exits 2 before any
gate runs.

The table is `GATES` below; a new gate is registered here once. Tests: tools/tests/test_data_gates.py.
"""
from __future__ import annotations

import argparse
import fnmatch
import re
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

REPO = Path(__file__).resolve().parent.parent
TIMEOUT = 900
DOTNET_TEST = ("dotnet", "test", "TAOM.Tests", "-p:DisableModuleCopy=true", "-p:ModuleId=", "--filter")
MODULEDATA_DOC = "docs/features/moduledata-validation.md"
CODES_DOC = "docs/reference/moduledata-validator-codes.md"
ARMORY_DOC = "docs/reference/armory-guide.md"


@dataclass(frozen=True)
class Gate:
    id: str
    kind: str                 # the kind of file that reads this gate, for the table
    globs: tuple              # fnmatch patterns on the lower-case, forward-slash path; start with *
    cmd: tuple                # ("python", "tools/x.py", args...) or ("dotnet", ...); () for a manual step
    read_first: tuple         # repo-relative docs to read before the gate or its finding
    dotnet: bool = False      # needs a build: NOT RUN unless --with-tests
    manual: str = ""          # a step nothing automates; printed as NOT RUN
    files: bool = False       # the command takes the matched files (all mode runs it bare)
    note: str = ""


def _dotnet(*names: str) -> tuple:
    return DOTNET_TEST + ("|".join(f"FullyQualifiedName~{n}" for n in names),)


TROOP_GLOBS = ("*/troops/troops_*.xml", "*/characters/*.xml", "*/equipmentsets/*.xml", "*/taom_spcultures.xml",
               "*/taom_partytemplates.xml", "*/named_companions/*.xml",
               "*/taom_wanderers.xml", "*/taom_education_character_templates.xml", "*/lords.xslt",
               "*/spcultures.xslt", "*tools/schemas/*.json", "*tools/validate_moduledata.py",
               "*tools/taom_schema.py")
MODULEDATA_GLOBS = ("*/_module/moduledata/*.xml", "*/_module/moduledata/*.xslt", "*tools/shipped_troop_ids.txt")
ARMORY_GLOBS = ("*/lotrlome_armory/moduledata/*","*/lotrlome_items/*.xml", "*/crafting_pieces*.xml",
                "*/weapon_descriptions*.xml")
MAP_GLOBS = ("*/taom_map/moduledata/*",)
CREATURE_GLOBS = ("*/action_sets.xml", "*/action_types.xml", "*/monster_usage_sets.xml", "*/monsters/*.xml",
                  "*/lotr_monster_*.xml")
FBX_GLOBS = ("*/assetsources/*.fbx",)
LANG_GLOBS = ("*/languages/*.xml", "*/taom_module_strings.xml", "*/taom_xslt_strings.xml")
# Globs tested against the file name alone: `*banner*` on a full path matches the install folder
# "Mount & Blade II Bannerlord", so every named live file would select banner-sheets.
NAME_GLOBS = ("*banner*",)

# Order matters where an author has to act in order: names are registered before loc coverage is read.
GATES = (
    Gate("xml-schemas", "any engine-registered XML, repo or live",
         ("*/moduledata/*.xml", "*/submodule.xml", "*/moduledata/*.xsl", "*/moduledata/*.xslt"),
         ("python", "tools/validate_xml_schemas.py"), ("tools/validate_xml_schemas.py",), files=True,
         note="finds each file's module; add SubModule.xml when it changed"),
    Gate("xml-schemas-live", "whole live modules (Armory, TAOM_Map)", (),
         ("python", "tools/validate_xml_schemas.py", "--live"), ("tools/validate_xml_schemas.py",)),
    Gate("moduledata", "troops, characters, rosters, cultures, clans, party templates, lords, Armory refs, "
         "TAOM_Map settlements", TROOP_GLOBS + MODULEDATA_GLOBS + ARMORY_GLOBS + MAP_GLOBS,
         ("python", "tools/validate_moduledata.py"),
         (".claude/rules/moduledata-validation.md", ".claude/rules/troops.md", ".claude/rules/xml-data.md", CODES_DOC),
         note="read the warnings as well as the errors; settlement pins live in tools/settlement_assignments.json"),
    Gate("generator-regions", "troops, characters, rosters, cultures, lord skill sets",
         TROOP_GLOBS + ("*/taom_lord_skill_sets.xml", "*tools/apply_culture_skills_traits.py"),
         ("python", "-m", "unittest", "tools.tests.test_generator_regions_in_sync"), (".claude/rules/data-scripts.md",),
         note="a hand edit inside a generator's region fails it: change the generator's table, then --apply"),
    Gate("career-kits", "troops, characters, rosters", TROOP_GLOBS,
         ("python", "tools/generate_career_kits.py", "--verify"), ("docs/features/career-system.md",),
         note="after any troop add or delete, re-run every generator check, not only the validator"),
    Gate("name-loc", "troops, characters, language files", TROOP_GLOBS + LANG_GLOBS,
         ("python", "tools/generate_name_localization_strings.py", "--check"), ("docs/localization/TRANSLATOR_GUIDE.md",)),
    Gate("wanderers", "wanderers, wanderer strings", ("*/taom_wanderers.xml", "*/taom_wanderer_strings.xml"),
         _dotnet("WandererBackstoryCoverageTests"), (MODULEDATA_DOC,), dotnet=True),
    Gate("xslt", "XSLT anywhere", ("*.xslt",), ("python", "tools/check_external_xslt.py"), (".claude/rules/xslt.md",),
         note="then transform and diff against the vanilla file (xslt.md file map)"),
    Gate("xslt-party", "spcultures.xslt", ("*/spcultures.xslt",), _dotnet("CulturePartyTemplateTests"),
         (".claude/rules/xslt.md",), dotnet=True),
    Gate("notable-templates", "notable and headman templates, spcultures, wanderers",
         ("*/characters/npcs_*.xml", "*/taom_spcultures.xml", "*/spcultures.xslt", "*/taom_wanderers.xml"),
         _dotnet("NotableTemplateGenderTests"), (MODULEDATA_DOC,), dotnet=True),
    Gate("armory-refs", "Armory items, crafting pieces, weapon descriptions", ARMORY_GLOBS,
         ("python", "tools/audit_armory_refs.py", "--report", "-"), (ARMORY_DOC,),
         note="meshes, collision bodies, dead item ids, and the companion checks of /armory-audit"),
    Gate("polearm-shield", "Armory weapons and pieces", ARMORY_GLOBS + TROOP_GLOBS,
         ("python", "tools/audit_polearm_shield_parity.py"), ("docs/features/weapon-xml-pipeline.md",)),
    Gate("mesh-refs", "Armory weapons and pieces", ARMORY_GLOBS,
         ("python", "tools/validate_mesh_refs.py", "--scan-bodies"), (ARMORY_DOC,),
         note="every mesh= and body_name= resolves to a shipped asset; pending artist bo_ meshes are the expected exception"),
    Gate("armour-classes", "Armory armour, troop rosters", ARMORY_GLOBS + TROOP_GLOBS,
         ("python", "tools/generate_armour_classes.py", "--check"), (ARMORY_DOC,),
         note="exit 1 stale, 2 no Armory; re-run with --apply when stale"),
    Gate("armory-names", "Armory item and piece names", ARMORY_GLOBS,
         ("python", "tools/register_armory_names.py", "--check"), ("docs/localization/TRANSLATOR_GUIDE.md",),
         note="first: a new {=KEY} name needs a Languages row; fix with --apply --prefix <your ids>"),
    Gate("ranged-ladder-items", "Armory launchers", ARMORY_GLOBS,
         ("python", "tools/generate_ranged_ladder_items.py", "--verify"), ("docs/features/ranged-ladders.md",),
         note="checks the live Armory only"),
    Gate("restat-donors", "Armory launchers", ARMORY_GLOBS,
         ("python", "tools/restat_ranged_donors.py", "--verify"), ("docs/features/ranged-ladders.md",),
         note="drift reported only in its mirror tree means the lotraom-assets mirror lags the live install"),
    Gate("starter-kit", "Armory starter kits", ARMORY_GLOBS,
         ("python", "tools/generate_starter_kit.py", "--verify"), ("docs/features/starting-equipment-tuning.md",),
         note="checks the live Armory only"),
    Gate("leg-cover-flags", "Armory leg armour", ARMORY_GLOBS + ("*tools/generate_*armor*.py",),
         ("python", "tools/audit_leg_cover_flags.py"), ("docs/modding/items-armor.md",),
         note="covers_legs is true on every greave and boot, false only on a bare-shin mesh (the Uruk-hai shoes); "
              "scans the Armory, TAOM_Map and the repo, never vanilla"),
    Gate("lorien-armor", "Lorien armour items and their generator", ARMORY_GLOBS + ("*tools/generate_lothlorien_armor.py",),
         ("python", "tools/generate_lothlorien_armor.py", "--check"), ("docs/features/lothlorien-snow-orcs.md",),
         note="exit 1 when the live Armory differs from the render; --apply copies each changed file to a .bak-gen backup"),
    Gate("snow-orc-armor", "Snow Orc armour items and their generator", ARMORY_GLOBS + ("*tools/generate_snow_orc_armor.py",),
         ("python", "tools/generate_snow_orc_armor.py", "--check"), ("docs/features/lothlorien-snow-orcs.md",),
         note="exit 1 when the live Armory differs from the render"),
    Gate("generator-refs", "XML a tools/ generator wrote", ("*tools/generate_*.py",) + ARMORY_GLOBS,
         ("python", "tools/check_generator_item_refs.py"), ("tools/README.md",),
         note="and the generator's own --verify or --check; hand-read three generated entries"),
    Gate("action-sets", "action sets, monsters, monster usage", CREATURE_GLOBS,
         ("python", "tools/audit_action_set_parity.py"), (ARMORY_DOC,)),
    Gate("mount-parity", "creature mounts", CREATURE_GLOBS,
         ("python", "tools/audit_mount_parity.py", "--strict"), ("docs/ai-includes/creature-mount-authoring.md",),
         note="--strict gates the BT-driven mounts in STRICT_MOUNTS; the report itself always exits 0"),
    Gate("troll-wiring", "troll action sets and Monsters", CREATURE_GLOBS,
         ("python", "tools/wire_hill_troll_race.py", "--check"), ("docs/features/troll-race.md",)),
    Gate("troll-tests", "troll action sets and Monsters", CREATURE_GLOBS,
         _dotnet("TrollBruteForceWiringTests", "TrollHitPointsLiveDataTests"), ("docs/features/troll-race.md",), dotnet=True),
    Gate("orc-locomotion", "orc and goblin action sets", CREATURE_GLOBS,
         ("python", "tools/bind_orc_goblin_locomotion.py", "--check"), ("docs/features/troll-race.md",),
         note="a reinstall drops their rows and parity still passes"),
    Gate("dwarf-attacks", "dwarf attack actions and their as_dwarf_warrior rows", CREATURE_GLOBS,
         ("python", "tools/bind_dwarf_attack_actions.py", "--check"), ("docs/features/dwarf-attacks.md",),
         note="a reinstall drops the two declarations and rows, and parity still passes"),
    Gate("dwarf-attacks-tests", "dwarf attack actions and their as_dwarf_warrior rows", CREATURE_GLOBS,
         _dotnet("DwarfAttacksWiringTests"), ("docs/features/dwarf-attacks.md",), dotnet=True),
    Gate("race-morphs", "hill troll and dwarf rig FBX in the live Armory AssetSources", FBX_GLOBS,
         ("python", "tools/check_race_morph_channels.py"), ("tools/check_race_morph_channels.py",),
         note="exact counts: head, eye and mouth 101, hands or arms 26; a new race rig is unchecked until its meshes are in SPEC"),
    Gate("fbx-lods", "Armory FBX sources", FBX_GLOBS,
         ("python", "tools/audit_fbx_lods.py", "--check"), (ARMORY_DOC,),
         note="every mesh carries LOD0 to LOD5 with no gaps, except clo_ and bo_ meshes"),
    Gate("map-villages", "TAOM_Map settlements", MAP_GLOBS,
         ("python", "tools/add_map_villages.py", "--check"), ("docs/reference/taom-map-settlement-naming.md",)),
    Gate("map-fortifications", "TAOM_Map settlements", MAP_GLOBS,
         ("python", "tools/add_map_fortifications.py", "--check"), ("docs/reference/taom-map-settlement-naming.md",),
         note="a town or castle rename goes into its FORTS row too"),
    Gate("map-descriptions", "TAOM_Map settlements", MAP_GLOBS,
         ("python", "tools/oneoff/write_village_descriptions.py", "--check"), ("docs/reference/taom-map-settlement-naming.md",),
         note="descriptions and orphan rows, not the names; exit 1 after a reinstall of an older package"),
    Gate("banner-sheets", "banner icons, banner keys, sheets and sprites",
         ("*/bannericons/*", "*/ui_taom_bannericons/*", "*banner*"),
         ("python", "tools/pack_banner_sheets.py", "--audit"), ("docs/reference/banner-icon-generation.md",)),
    Gate("banner-rdc", "banner icon Kit imports", ("*/bannericons/*",),
         ("python", "tools/check_rdc_entries.py", "--module", "{taom_module}", "--under", "BannerIcons", "--types", "tex"),
         ("docs/reference/banner-icon-generation.md",)),
    Gate("rdc-entries", "tpac packages", ("*.tpac",), ("python", "tools/check_rdc_entries.py"),
         ("docs/reference/banner-icon-generation.md",), note="a listed package needs a Modding Kit save"),
    Gate("gui-tests", "GUI prefabs, PrefabExtension XML, sprite data",
         ("*/gauntletui/*", "*/prefabs/*.xml", "*prefabextension*", "*spritedata.xml"),
         _dotnet("PrefabExtensionBrushTests", "EveryPrefabAttribute_NamesASettableWidgetProperty",
                 "PrefabElementTypeBindingTests", "GauntletStretchInCoverRatchetTests", "PrefabCloneWidgetReferenceTests",
                 "PrefabSpriteTests"),
         (".claude/rules/gui-ui.md",), dotnet=True,
         note="PrefabCloneWidgetReferenceTests applies to a TAOM copy of a vanilla prefab"),
    Gate("gui-manual", "GUI prefabs, PrefabExtension XML", ("*/gauntletui/*", "*/prefabs/*.xml", "*prefabextension*"),
         (), (".claude/rules/gui-ui.md",),
         manual="read each GetSprite(\"X\") name in C# against TAOMSpriteData.xml (PrefabSpriteTests gates a literal "
                "Sprite=\"X\" in a prefab, not a C# call), and check that every Command.* handler resolves to a TAOM "
                "method with the right parameter count"),
    Gate("loc-orphans", "English strings files, the orphan gate's own inputs",
         ("*/_module/moduledata/*strings*.xml", "*tools/check_orphan_loc_rows.py", "*tools/_loc_sources.py",
          "*tools/loc_composed_families.json", "*tools/loc_orphan_allowlist.json"),
         ("python", "tools/check_orphan_loc_rows.py"), ("docs/localization/TRANSLATOR_GUIDE.md",),
         note="a row is also orphaned when the C# literal that read it is removed: run it by hand after such a removal"),
    Gate("loc-coverage", "language files, {=KEY} text", LANG_GLOBS + ARMORY_GLOBS,
         ("python", "tools/check_external_loc_coverage.py"), ("docs/reference/localization-map.md",),
         note="after any name or language edit; --apply raises the untranslated count, so run its --update-baseline in the same change"),
    Gate("loc-tests", "language files, {=KEY} text", LANG_GLOBS,
         _dotnet("Infrastructure.Localization"), ("docs/reference/localization-map.md",), dotnet=True,
         note="LanguageDataXmlTests, LocalizationKeyConsistencyTests, LanguageTextIntegrityTests"),
    Gate("mbproj", "project.mbproj", ("*project.mbproj",), ("python", "tools/audit_mbproj_registration.py"),
         ("tools/audit_mbproj_registration.py",)),
    Gate("voice", "voice definitions, module sounds", ("*/voice_definitions.xml", "*/module_sounds.xml"),
         ("python", "tools/audit_voice_clip_lengths.py"), ("tools/audit_voice_clip_lengths.py",),
         note="only the module sounds a voice binds; for the rest of module_sounds.xml, only duplicate names"),
    Gate("prefab-budget", "scenes, prefabs", ("*/scenes/*", "*.scene.xml", "*/sceneobj/*"),
         ("python", "tools/check_prefab_budget.py"), ("docs/modding/module-map.md",),
         note="counts TAOM_Map only; sum all modules"),
    Gate("siege-walls", "TAOM_Map scene files", ("*/sceneobj/*/scene.xscene",),
         ("python", "tools/check_siege_wall_scripts.py"),
         ("docs/reference/engine/scene-gameentity-scriptcomponent.md",), files=True,
         note="judges the scene files you named, or every live TAOM_Map scene under --all; "
              "a wall placed as a bare prefab instance is not judged"),
)


def norm(path: str) -> str:
    return str(path).replace("\\", "/").lower()


def matches(gate: Gate, path: str) -> bool:
    p = norm(path)
    name = p.rsplit("/", 1)[-1]
    return any(fnmatch.fnmatch(name if g in NAME_GLOBS else p, g) for g in gate.globs)


def select(gates, paths, all_mode: bool = False) -> list:
    if all_mode:
        return list(gates)
    return [g for g in gates if any(matches(g, p) for p in paths)]


def _taom_module() -> str:
    from _gamedir import game_dir
    return str(Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")) / "Modules" / "TAOM")


def command_for(gate: Gate, paths, all_mode: bool) -> list:
    """The command to run, with `python` still the first word (the runner swaps in the interpreter)."""
    argv = [a.replace("{taom_module}", _taom_module()) if "{taom_module}" in a else a for a in gate.cmd]
    if gate.files and not all_mode:
        # a deleted file still selects the gate but cannot be validated; the gate runs bare if none is left
        argv += [p for p in paths if matches(gate, p) and (REPO / p).exists()]
    return argv


# Exit-0 output that means the gate checked nothing. Case-sensitive: audit_mbproj_registration prints
# "Skipped official modules" on a normal run.
NOT_RUN = re.compile(r"\bSKIPPED\b|^SKIP\b|\(skipped=\d+\)|lxml not installed|^PASS: 0 file\(s\) validated", re.M)


def verdict(returncode: int, output: str) -> tuple:
    if returncode == 0:
        line = next((l.strip() for l in output.splitlines() if NOT_RUN.search(l)), None)
        if line:
            return "NOT RUN", f"checked nothing: {line[:200]}"
        return "PASS", ""
    if returncode == 2:
        return "NOT RUN", "exit 2 (no install, a missing dependency or bad input)"
    return "FAIL", f"exit {returncode}"


def run_command(argv, timeout):
    real = [sys.executable] + list(argv[1:]) if argv and argv[0] == "python" else list(argv)
    try:
        p = subprocess.run(real, capture_output=True, text=True, encoding="utf-8", errors="replace",
                           cwd=str(REPO), timeout=timeout)
    except subprocess.TimeoutExpired:
        return 124, f"timed out after {timeout}s"
    except FileNotFoundError as exc:
        return 2, f"could not start: {exc}"
    return p.returncode, (p.stdout or "") + (p.stderr or "")


def changed_files(git) -> list:
    """Tracked changes against HEAD plus untracked files; `git(*args)` returns the command's stdout."""
    names = set()
    for out in (git("diff", "--name-only", "HEAD"), git("ls-files", "--others", "--exclude-standard")):
        names |= {l.strip() for l in out.splitlines() if l.strip()}
    return sorted(names)


def _git(*args) -> str:
    p = subprocess.run(["git", "-c", "core.quotepath=off", "-C", str(REPO), *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if p.returncode != 0:
        why = ((p.stderr or "").strip().splitlines() or [""])[0]
        raise RuntimeError(f"git {' '.join(args)} exited {p.returncode}: {why}")
    return p.stdout


def live_moduledata_dirs() -> list:
    from _gamedir import game_dir
    modules = Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")) / "Modules"
    return [modules / "LOTRLOME_Armory" / "ModuleData", modules / "TAOM_Map" / "ModuleData"]


class AmbiguousName(Exception):
    """A bare name that two live modules both hold."""


def resolve_named(name: str):
    """The absolute path of a file named on the command line, or None. A relative name is tried against the
    working directory, the repo, then the live Modules folder (`TAOM_Map/ModuleData/settlements.xml`) and the
    live Armory and TAOM_Map ModuleData folders. Raises AmbiguousName when two live files match."""
    p = Path(name)
    if p.is_absolute():
        return str(p.resolve()) if p.is_file() else None
    for c in (Path.cwd() / p, REPO / p):
        if c.is_file():
            return str(c.resolve())
    dirs = live_moduledata_dirs()
    live = {str(c.resolve()) for c in [d.parent.parent / p for d in dirs] + [d / p for d in dirs] if c.is_file()}
    if len(live) > 1:
        raise AmbiguousName(f"{name} is in more than one live module: {', '.join(sorted(live))}; "
                            "name it with its module folder, such as TAOM_Map/ModuleData/<file>")
    return next(iter(live), None)


def _shown(argv) -> str:
    return " ".join(f'"{a}"' if " " in a or "|" in a else a for a in argv)


def run(gates, paths, all_mode: bool, with_tests: bool, runner=run_command, jobs: int = 4) -> int:
    selected = select(gates, paths, all_mode)
    if not selected:
        print("no gate reads the changed files; name a live file on the command line, or use --all")
        return 0
    docs = []
    for g in selected:
        for d in g.read_first:
            if d not in docs:
                docs.append(d)
    print("Read first: " + ", ".join(docs))
    print()

    def execute(g):
        if g.manual:
            return ("NOT RUN", f"manual: {g.manual}"), ""
        if g.dotnet and not with_tests:
            return ("NOT RUN", "needs a build; run it with --with-tests"), ""
        rc, text = runner(command_for(g, paths, all_mode), TIMEOUT)
        return verdict(rc, text), text

    # dotnet builds collide when they overlap, so they run one at a time after the pool
    pooled = [g for g in selected if not g.dotnet]
    with ThreadPoolExecutor(max_workers=max(1, jobs)) as pool:
        outcomes = dict(zip((g.id for g in pooled), pool.map(execute, pooled)))
    outcomes.update((g.id, execute(g)) for g in selected if g.dotnet)

    counts = {"PASS": 0, "FAIL": 0, "NOT RUN": 0}
    for g in selected:
        (status, why), text = outcomes[g.id]
        argv = command_for(g, paths, all_mode)
        counts[status] += 1
        shown = _shown(argv) if argv else "(manual)"
        print(f"{status:<8} {g.id:<20} {shown}")
        if why:
            print(f"         {why}")
        if g.note:
            print(f"         note: {g.note}")
        if g.id == "moduledata":
            found = re.search(r"\d+ error\(s\), \d+ warning\(s\)", text)
            if found:
                print(f"         {found.group(0)}")
        if status == "FAIL":
            tail = [l for l in text.splitlines() if l.strip()][-15:]
            for l in tail:
                print(f"         | {l}")
    print()
    print(f"{counts['PASS']} PASS, {counts['FAIL']} FAIL, {counts['NOT RUN']} NOT RUN. "
          "A gate that did not run has not passed.")
    if counts["FAIL"]:
        return 1
    return 2 if counts["NOT RUN"] else 0


def list_table(gates) -> None:
    for g in gates:
        how = "manual" if g.manual else _shown(g.cmd)
        flags = " [dotnet]" if g.dotnet else " [--all only]" if not g.globs else ""
        print(f"{g.id:<20} {g.kind}{flags}")
        print(f"{'':<20} {how}")
        print(f"{'':<20} read first: {', '.join(g.read_first) or '-'}")


def main(argv=None) -> int:
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="*", help="extra files to select gates for (live Armory / TAOM_Map files git cannot see)")
    ap.add_argument("--all", action="store_true", help="run every gate, whatever changed")
    ap.add_argument("--list", action="store_true", help="print the table and exit")
    ap.add_argument("--with-tests", action="store_true", help="also run the dotnet test gates")
    args = ap.parse_args(argv)
    if args.list:
        list_table(GATES)
        return 0
    named = []
    for name in args.files:
        try:
            found = resolve_named(name)
        except AmbiguousName as exc:
            print(f"data_gates: {exc}", file=sys.stderr)
            return 2
        if not found:
            print(f"data_gates: no such file: {name}", file=sys.stderr)
            return 2
        named.append(found)
    try:
        paths = [] if args.all else sorted(set(changed_files(_git)) | set(named))
    except RuntimeError as exc:
        print(f"data_gates: {exc}", file=sys.stderr)
        return 2
    return run(GATES, paths, args.all, args.with_tests)


if __name__ == "__main__":
    sys.exit(main())
