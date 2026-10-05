#!/usr/bin/env python3
"""Unit tests for the public-release packager (tools/package_release.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_package_release.py

Pure stdlib with synthetic module trees -- no game install needed. `classify` is the
whole decision surface and is called directly with hand-built relative paths; the
walk/copy layer is exercised against a tempdir tree.

Each test maps to one part of the contract:
  - RuntimeDataCache excluded by default, kept under --keep-rdc, .rtemp dropped either way
  - AssetSources / Prefabs_Unused excluded outright
  - EmAssetPackages and Assets/Race Test are CANDIDATES, copied unless explicitly named
  - SceneEditData / SceneObj / AssetPackages copied (vanilla ships them -- regression guard)
  - backup sidecars excluded anywhere: bare .bak plus the dated/topic forms the tools
    write (.bak-<topic>, .bak_<topic>, .bak<N>, .backup, .orig, .prev, .old, .tmp,
    .transplanted-<date>); a suffix BEFORE the real extension (foo.bak.xml) still ships
  - SceneObj/Backups and SceneEditData/Backups (Modding Kit scene backups) excluded
  - bin/*.pdb|exp|lib excluded, bin/*.dll copied
  - runtime state files (diag.log, last-good-modlist.txt) excluded; licences copied
  - unrecognised top-level entries are UNKNOWN -> reported, never copied
  - manifest byte arithmetic; --dry-run writes nothing; exit codes
  - --require-build: a DLL whose stamp is dirty, git-less or from another commit is refused
  - scene shader caches are a report, never a refusal: every shipped SceneObj/<scene> gets one
    line (header, sack, sack format), a summary line, and a WARNING for a sack whose format
    differs from the majority; SceneObj/Backups is never judged
  - module-level <Module>/Shaders/D3D11 sacks (the maintainer's policy of 2026-10-03) are listed
    with their size and format under one policy line and are never copied, for every module; the
    scene sacks and the folder's other files keep shipping, and nothing refuses
  - one JIT optimization line per shipped TAOM DLL copy (OFF, ON or unknown with its reason; an
    assembly with no DebuggableAttribute reads "ON (no DebuggableAttribute on the assembly)", and a
    DebuggableAttribute whose constructor or value is not the layout ECMA-335 gives that
    constructor reads unknown, never a verdict), never a refusal
"""
import contextlib
import io
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path, PurePosixPath

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import package_release as pr  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "package_release.py"


def act(rel, **kw):
    """Shorthand: the action letter for a relative path."""
    return pr.classify(rel, **kw).action


def rule(rel, **kw):
    return pr.classify(rel, **kw).rule


# --------------------------------------------------------------------------- #
# RuntimeDataCache -- the whole point of the tool                              #
# --------------------------------------------------------------------------- #
class TestRuntimeDataCache(unittest.TestCase):
    def test_excluded_by_default(self):
        d = pr.classify("RuntimeDataCache/043E659A-97BB-4375-9A9B-55A6E52A3E44.rdc")
        self.assertEqual(d.action, pr.EXCLUDE)
        self.assertEqual(d.rule, "RUNTIME_DATA_CACHE")

    def test_kept_when_requested(self):
        self.assertEqual(act("RuntimeDataCache/x.rdc", keep_rdc=True), pr.COPY)

    def test_off_folder_from_the_ab_test_is_also_excluded(self):
        # Invoke-RdcAbTest.ps1 leaves RuntimeDataCache.OFF behind if -On was never run.
        # Packaging that folder would ship the 41 GB under a different name.
        self.assertEqual(act("RuntimeDataCache.OFF/x.rdc"), pr.EXCLUDE)
        self.assertEqual(act("RuntimeDataCache.OFF/x.rdc", keep_rdc=True), pr.EXCLUDE)

    def test_editor_partial_cook_is_excluded_not_unknown(self):
        # Observed 2026-08-10: with the real cache renamed away, the editor regenerated
        # 6 .rdc files into a fresh RuntimeDataCache and then asserted; resolving that by
        # hand leaves a RuntimeDataCache.editor-partial-<date> folder behind. It must be
        # excluded as cache, not treated as an unknown that blocks the whole release run.
        rel = "RuntimeDataCache.editor-partial-2026-08-10/043E659A.rdc"
        self.assertEqual(act(rel), pr.EXCLUDE)
        self.assertEqual(rule(rel), "RDC_STRAY_FOLDER")
        self.assertEqual(act(rel, keep_rdc=True), pr.EXCLUDE)

    def test_rtemp_dropped_even_when_the_cache_is_kept(self):
        # 1,646 zero-byte editor leftovers. They are litter under every verdict.
        self.assertEqual(act("RuntimeDataCache/x.rdc.rtemp", keep_rdc=True), pr.EXCLUDE)
        self.assertEqual(rule("RuntimeDataCache/x.rdc.rtemp", keep_rdc=True), "RDC_RTEMP")


# --------------------------------------------------------------------------- #
# Confident exclusions                                                         #
# --------------------------------------------------------------------------- #
class TestConfidentExclusions(unittest.TestCase):
    def test_asset_sources(self):
        self.assertEqual(act("AssetSources/world_map/foo.png"), pr.EXCLUDE)

    def test_prefabs_unused(self):
        self.assertEqual(act("Prefabs_Unused/foo.xml"), pr.EXCLUDE)

    def test_xml_bak_anywhere(self):
        # ModuleData is glob-loaded, so a .bak is a duplicate-registration hazard,
        # not merely dead weight.
        self.assertEqual(act("ModuleData/action_sets.xml.bak"), pr.EXCLUDE)
        self.assertEqual(act("ModuleData/Languages/DE/loc_settlements.xml.bak"), pr.EXCLUDE)
        self.assertEqual(rule("ModuleData/action_sets.xml.bak"), "BACKUP_SIDECAR")

    def test_dated_backup_sidecars_excluded(self):
        # The tools under tools/ write dated, topic-tagged suffixes, and those are the
        # bulk of what accumulates: the 2026-09-14 pre-release sweep found 164 sidecars
        # in the install and not one of them ended in a bare ".bak". An exact-suffix
        # rule therefore shipped every one of them. Same suffix set as
        # tools/sweep_module_backups.ps1, which is the other half of the release gate.
        for rel in (
            "ModuleData/troops/troops_rohan.xml.bak-fellwarg-20260831",
            "ModuleData/LOTRLOME_items/gondor/body_armors.xml.bak-kingdomcurve-583",
            "ModuleData/Languages/DE/loc_settlements.xml.bak_mapvillages_20260913_220727",
            "ModuleData/troops/troops_rohan.xml.bak-",   # trailing dash, a real file
            "ModuleData/LOTRLOME_items/LOTRAOM_shields.xml.bak2",
            "Assets/creature/spider/meshes/sk_spider_forest_c_geo.tpac.backup",
            "ModuleData/action_sets.xml.orig",
            "ModuleData/DistanceCaches/settlements_distance_cache_Default.bin.prev",
            "ModuleData/foo.xml.old",
            "ModuleData/foo.xml.tmp",
            "ModuleData/foo.xml.transplanted-20260828",
            "ModuleData/FOO.XML.BAK-UPPER",
        ):
            self.assertEqual(act(rel), pr.EXCLUDE, rel)
            self.assertEqual(rule(rel), "BACKUP_SIDECAR", rel)

    def test_backup_suffix_must_be_last(self):
        # The engine globs GetFiles("*.xml"), so foo.bak.xml IS loaded and parsed as
        # data, duplicating every id in it. The packager must not paper over that by
        # quietly dropping the file: it ships, and the validator is where it gets caught.
        self.assertEqual(act("ModuleData/foo.bak.xml"), pr.COPY)
        self.assertEqual(act("ModuleData/backup_troops.xml"), pr.COPY)
        self.assertEqual(act("ModuleData/troops.xml.bakery"), pr.COPY)

    def test_scene_backups_folders_excluded(self):
        # The Modding Kit writes SceneObj/Backups/<scene> and SceneEditData/Backups/<scene>
        # on every scene save (437 MB for Main_map on 2026-09-14). Both parents are on
        # KNOWN_TOP_DIRS because vanilla ships them, so without this rule the include-list
        # waves the backups through with the live scene.
        self.assertEqual(act("SceneObj/Backups/Main_map/scene.xscene"), pr.EXCLUDE)
        self.assertEqual(rule("SceneObj/Backups/Main_map/scene.xscene"), "SCENE_BACKUPS")
        self.assertEqual(act("SceneEditData/Backups/Main_map/terrain_ed.bin"), pr.EXCLUDE)
        self.assertEqual(rule("SceneEditData/Backups/Main_map/terrain_ed.bin"), "SCENE_BACKUPS")
        # The live scene beside it still ships (vanilla-ships-them regression guard).
        self.assertEqual(act("SceneObj/Main_map/scene.xscene"), pr.COPY)
        self.assertEqual(act("SceneEditData/Main_map/terrain_ed.bin"), pr.COPY)
        # "Backups" must be the second path part, not a scene that happens to carry the name.
        self.assertEqual(act("SceneObj/Main_map/Backups_readme.txt"), pr.COPY)

    def test_native_debug_artifacts(self):
        for f in ("TAOM.NativeSkinFixes.pdb", "TAOM.NativeSkinFixes.exp", "TAOM.NativeSkinFixes.lib"):
            self.assertEqual(act(f"bin/Win64_Shipping_Client/{f}"), pr.EXCLUDE, f)

    def test_binaries_still_copied(self):
        self.assertEqual(act("bin/Win64_Shipping_Client/TAOM.dll"), pr.COPY)
        self.assertEqual(act("bin/Win64_Shipping_Client/DryIoc.dll"), pr.COPY)

    def test_runtime_state_files(self):
        for f in ("diag.log", "last-good-modlist.txt", "failed-mods-catalog.txt"):
            self.assertEqual(act(f), pr.EXCLUDE, f)

    def test_shipped_metadata_survives(self):
        self.assertEqual(act("SubModule.xml"), pr.COPY)
        self.assertEqual(act("THIRD-PARTY-LICENSES.txt"), pr.COPY)

    def test_visual_studio_workspace_state_is_excluded(self):
        # `.vs` sits under GUI/, which is a KNOWN_TOP_DIR, so the include-list would
        # otherwise wave it straight through -- and it did, for every release built on
        # a machine that had opened the GUI folder in Visual Studio. DocumentLayout.json
        # holds absolute paths naming the maintainer's drive and the module folder the
        # GUI was authored in. Match on the path part, not the file name: the folder
        # nests (`GUI/.vs/GUI/v17/...`) and the file names are not distinctive.
        self.assertEqual(act("GUI/.vs/VSWorkspaceState.json"), pr.EXCLUDE)
        self.assertEqual(act("GUI/.vs/GUI/v17/DocumentLayout.json"), pr.EXCLUDE)
        self.assertEqual(rule("GUI/.vs/GUI/v17/DocumentLayout.json"), "VS_IDE_STATE")
        # A real GUI asset next door is untouched.
        self.assertEqual(act("GUI/TAOMSpriteData.xml"), pr.COPY)
        # ".vs" must be a whole path part, not a substring of a legitimate name.
        self.assertEqual(act("GUI/Brushes/.vsomething_else.xml"), pr.COPY)

    def test_modding_kit_project_file_still_ships(self):
        # Regression guard, and the reason is counter-intuitive enough to be worth the
        # test: project.mbproj reads as an editor artifact but the SHIPPING runtime calls
        # XmlResource.GetMbprojxmls() for every module and registers its <file> nodes as
        # native resources. That loader is disjoint from SubModule.xml's <XmlName> glob,
        # so TAOM's voice definitions + module_sounds.xml and LOTRLOME_Armory's monsters
        # + action sets are registered HERE AND NOWHERE ELSE. Excluding it produces a
        # silent-audio, missing-monster release that every test and validator calls clean.
        self.assertEqual(act("ModuleData/project.mbproj"), pr.COPY)
        self.assertEqual(act("ModuleData/characters/lords.xml"), pr.COPY)


# --------------------------------------------------------------------------- #
# Candidates -- copied unless explicitly named                                 #
# --------------------------------------------------------------------------- #
class TestCandidates(unittest.TestCase):
    def test_em_asset_packages_is_not_editor_only(self):
        # Regression guard on a wrong claim in the native-commit audit: vanilla
        # Modules/Native ships 26.36 GB of EmAssetPackages (measured 2026-08-10),
        # so it must never be dropped by default.
        d = pr.classify("EmAssetPackages/foo.tpac")
        self.assertEqual(d.action, pr.COPY)
        self.assertEqual(d.rule, "EM_ASSET_PACKAGES")
        self.assertTrue(d.candidate)

    def test_em_asset_packages_excluded_when_named(self):
        self.assertEqual(
            act("EmAssetPackages/foo.tpac", exclude_candidates=("EM_ASSET_PACKAGES",)),
            pr.EXCLUDE,
        )

    def test_race_test(self):
        d = pr.classify("Assets/Race Test/head.tpac")
        self.assertEqual(d.action, pr.COPY)
        self.assertTrue(d.candidate)
        self.assertEqual(
            act("Assets/Race Test/head.tpac", exclude_candidates=("RACE_TEST",)), pr.EXCLUDE
        )

    def test_other_assets_are_plain_copies(self):
        d = pr.classify("Assets/armour/foo.tpac")
        self.assertEqual(d.action, pr.COPY)
        self.assertFalse(d.candidate)


# --------------------------------------------------------------------------- #
# Vanilla-ships regression guards                                              #
# --------------------------------------------------------------------------- #
class TestVanillaShippedDirsAreCopied(unittest.TestCase):
    def test_scene_dirs_and_packages(self):
        # Measured 2026-08-10: Modules/Native ships SceneObj 1.1 GB and
        # SceneEditData 0.19 GB, so neither is editor-only.
        for rel in (
            "SceneEditData/foo.xml",
            "SceneObj/foo.xml",
            "AssetPackages/pack0.tpac",
            "Shaders/foo.shader",
            "ModuleData/troops.xml",
            "GUI/SpriteData/foo.xml",
            "Prefabs/foo.xml",
            "Atmospheres/foo.xml",
            "NavMeshPrefabs/foo.xml",
            "ModuleSounds/foo.ogg",
        ):
            self.assertEqual(act(rel), pr.COPY, rel)


# --------------------------------------------------------------------------- #
# Unknown entries are reported, never copied                                   #
# --------------------------------------------------------------------------- #
class TestUnknown(unittest.TestCase):
    def test_unrecognised_top_level_dir(self):
        d = pr.classify("SomeNewEditorFolder/foo.bin")
        self.assertEqual(d.action, pr.UNKNOWN)

    def test_unrecognised_top_level_file(self):
        self.assertEqual(act("scratch_notes.txt"), pr.UNKNOWN)


# --------------------------------------------------------------------------- #
# Planning + manifest arithmetic                                               #
# --------------------------------------------------------------------------- #
def _write(root: Path, rel: str, size: int):
    p = root / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(b"\0" * size)


def _fixture(tmp: Path) -> Path:
    src = tmp / "Modules"
    _write(src, "TAOM/SubModule.xml", 100)
    _write(src, "TAOM/AssetPackages/pack0.tpac", 1000)
    _write(src, "TAOM/RuntimeDataCache/a.rdc", 5000)
    _write(src, "TAOM/RuntimeDataCache/b.rdc.rtemp", 0)
    _write(src, "TAOM/AssetSources/big.png", 8000)
    _write(src, "TAOM/ModuleData/troops.xml", 200)
    _write(src, "TAOM/ModuleData/troops.xml.bak", 250)
    _write(src, "TAOM/ModuleData/troops.xml.bak-fellwarg-20260831", 260)
    _write(src, "TAOM/SceneObj/Main_map/scene.xscene", 600)
    _write(src, "TAOM/SceneObj/Backups/Main_map/scene.xscene", 700)
    _write(src, "TAOM/bin/Win64_Shipping_Client/TAOM.dll", 300)
    _write(src, "TAOM/bin/Win64_Shipping_Client/TAOM.pdb", 900)
    _write(src, "TAOM/EmAssetPackages/em.tpac", 400)
    _write(src, "TAOM/Mystery/thing.bin", 77)
    return src


class TestPlan(unittest.TestCase):
    def test_byte_arithmetic(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            plan = pr.plan_module(src / "TAOM")

            self.assertEqual(
                plan.total_bytes,
                100 + 1000 + 5000 + 0 + 8000 + 200 + 250 + 260 + 600 + 700 + 300 + 900 + 400 + 77,
            )
            # copied: SubModule 100 + pack0 1000 + troops 200 + live scene 600 + dll 300 + em 400
            self.assertEqual(plan.copy_bytes, 2600)
            # excluded: rdc 5000 + rtemp 0 + AssetSources 8000 + bak 250 + dated bak 260
            #           + scene backup 700 + pdb 900
            self.assertEqual(plan.exclude_bytes, 15110)
            self.assertEqual(plan.unknown_bytes, 77)
            self.assertEqual(plan.total_bytes, plan.copy_bytes + plan.exclude_bytes + plan.unknown_bytes)

    def test_keep_rdc_moves_bytes_from_excluded_to_copied(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            plan = pr.plan_module(src / "TAOM", keep_rdc=True)
            self.assertEqual(plan.copy_bytes, 2600 + 5000)
            self.assertEqual(plan.exclude_bytes, 15110 - 5000)  # .rtemp still excluded

    def test_by_rule_totals(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            plan = pr.plan_module(src / "TAOM")
            self.assertEqual(plan.by_rule["RUNTIME_DATA_CACHE"], 5000)
            self.assertEqual(plan.by_rule["ASSET_SOURCES"], 8000)
            self.assertEqual(plan.by_rule["BACKUP_SIDECAR"], 250 + 260)
            self.assertEqual(plan.by_rule["SCENE_BACKUPS"], 700)
            self.assertEqual(plan.by_rule["NATIVE_DEBUG"], 900)


# --------------------------------------------------------------------------- #
# CLI                                                                          #
# --------------------------------------------------------------------------- #
def _run(*args):
    return subprocess.run(
        [sys.executable, str(TOOL), *args], capture_output=True, text=True
    )


class TestCli(unittest.TestCase):
    def test_dry_run_writes_nothing(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            dest = Path(td) / "out"
            r = _run("--source", str(src), "--dest", str(dest), "--modules", "TAOM", "--dry-run")
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertFalse(dest.exists(), "dry run must not create the destination")

    def test_real_run_refuses_while_unknown_entries_are_unreviewed(self):
        # A new editor artifact must never ride along silently -- and a folder that
        # actually belongs in the build must never vanish silently either. Both are
        # the same failure, so unknowns block the run until a human has looked.
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            dest = Path(td) / "out"
            r = _run("--source", str(src), "--dest", str(dest), "--modules", "TAOM")
            self.assertEqual(r.returncode, 2)
            self.assertIn("Mystery/thing.bin", r.stdout)
            self.assertFalse(dest.exists(), "must fail before writing anything")

    def test_real_run_copies_only_the_allowed_set(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            dest = Path(td) / "out"
            r = _run("--source", str(src), "--dest", str(dest), "--modules", "TAOM",
                     "--allow-unknown")
            self.assertEqual(r.returncode, 0, r.stderr)

            self.assertTrue((dest / "TAOM/AssetPackages/pack0.tpac").exists())
            self.assertTrue((dest / "TAOM/ModuleData/troops.xml").exists())
            self.assertTrue((dest / "TAOM/SceneObj/Main_map/scene.xscene").exists())
            self.assertTrue((dest / "TAOM/EmAssetPackages/em.tpac").exists())
            for gone in (
                "TAOM/RuntimeDataCache",
                "TAOM/AssetSources",
                "TAOM/ModuleData/troops.xml.bak",
                "TAOM/ModuleData/troops.xml.bak-fellwarg-20260831",
                "TAOM/SceneObj/Backups",
                "TAOM/bin/Win64_Shipping_Client/TAOM.pdb",
                "TAOM/Mystery",
            ):
                self.assertFalse((dest / gone).exists(), gone)

    def test_json_manifest(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            man = Path(td) / "m.json"
            r = _run("--source", str(src), "--dest", str(Path(td) / "out"),
                     "--modules", "TAOM", "--dry-run", "--json", str(man))
            self.assertEqual(r.returncode, 0, r.stderr)
            data = json.loads(man.read_text())
            self.assertEqual(data["modules"][0]["name"], "TAOM")
            self.assertEqual(data["modules"][0]["copy_bytes"], 2600)
            self.assertEqual(data["totals"]["unknown_bytes"], 77)
            self.assertIn("Mystery/thing.bin", " ".join(data["modules"][0]["unknown"]))

    def test_missing_source_is_an_error(self):
        r = _run("--source", "Z:/nope", "--dest", "Z:/out", "--dry-run")
        self.assertEqual(r.returncode, 2)

    def test_refuses_to_write_into_a_non_empty_destination(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            dest = Path(td) / "out"
            (dest / "TAOM").mkdir(parents=True)
            (dest / "TAOM" / "stale.txt").write_text("x")
            r = _run("--source", str(src), "--dest", str(dest), "--modules", "TAOM",
                     "--allow-unknown")
            self.assertEqual(r.returncode, 2)
            self.assertIn("not empty", (r.stderr + r.stdout).lower())


# --------------------------------------------------------------------------- #
# --require-build: only a clean build of the release commit may ship           #
# --------------------------------------------------------------------------- #
SHA = "0123456789abcdef0123456789abcdef01234567"
STAMP = "build.20260923-184249Z"


def _dll_bytes(stamp: str) -> bytes:
    """A stand-in DLL: the stamp sits in the bytes the way the metadata stores it (UTF-8)."""
    return b"MZ\x90\x00\x01\x00" + stamp.encode("ascii") + b"\x00\x00trailer"


class TestRetiredBinaries(unittest.TestCase):
    """A binary the repo stopped shipping survives in the install, its _wEditor/_Server mirrors and
    every channel folder, because deploys never delete. The packager is the gate every release
    passes, in --dry-run too, so it must refuse rather than quietly copy or exclude."""

    def test_dry_run_refuses_a_retired_binary_and_names_it(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            _write(src, "TAOM/bin/Win64_Shipping_wEditor/MinHook.x64.dll", 16)
            r = _run("--source", str(src), "--dest", str(Path(td) / "out"), "--modules", "TAOM",
                     "--dry-run")
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("TAOM/bin/Win64_Shipping_wEditor/MinHook.x64.dll", r.stderr)

    def test_real_run_refuses_before_writing(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            _write(src, "TAOM/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll", 16)
            dest = Path(td) / "out"
            r = _run("--source", str(src), "--dest", str(dest), "--modules", "TAOM",
                     "--allow-unknown")
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("TAOM.NativeSkinFixes.dll", r.stderr)
            self.assertFalse(dest.exists(), "must fail before writing anything")

    def test_match_is_case_insensitive_and_limited_to_bin(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            _write(src, "TAOM/bin/Gaming.Desktop.x64_Shipping_Client/BehaviorTreeWrapper.DLL", 16)
            _write(src, "TAOM/ModuleData/minhook.x64.dll", 16)
            plan = pr.plan_module(src / "TAOM")
            self.assertEqual(pr.retired_binaries([plan]),
                             ["TAOM/bin/Gaming.Desktop.x64_Shipping_Client/BehaviorTreeWrapper.DLL"])

    def test_no_retired_name_is_tracked_in_the_repo(self):
        # A retired name coming back into a tracked _Module/bin would be refused at every release.
        repo = Path(__file__).resolve().parents[2]
        try:
            r = subprocess.run(["git", "ls-files", "*/_Module/bin/*"], cwd=repo,
                               capture_output=True, text=True)
        except OSError:
            self.skipTest("git unavailable")
        if r.returncode != 0:
            self.skipTest("git unavailable")
        tracked = {PurePosixPath(p).name.casefold() for p in r.stdout.splitlines()
                   if (repo / p).exists()}
        self.assertFalse(tracked & pr.RETIRED_BINARIES, sorted(tracked & pr.RETIRED_BINARIES))


class TestBuildStamp(unittest.TestCase):
    def _dll(self, td, stamp):
        p = Path(td) / "TAOM.dll"
        p.write_bytes(_dll_bytes(stamp))
        return p

    def test_reads_the_stamp_out_of_dll_bytes(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertEqual(pr.read_build_stamp(self._dll(td, f"{STAMP}+{SHA}")), f"{STAMP}+{SHA}")

    def test_reads_a_dirty_stamp_with_its_flag(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertEqual(pr.read_build_stamp(self._dll(td, f"{STAMP}+{SHA}.dirty")),
                             f"{STAMP}+{SHA}.dirty")

    def test_no_stamp_reads_as_none(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "TAOM.dll"
            p.write_bytes(b"\0" * 300)
            self.assertIsNone(pr.read_build_stamp(p))

    def test_clean_stamp_at_the_expected_commit_passes(self):
        self.assertIsNone(pr.check_build_stamp(f"{STAMP}+{SHA}", SHA))

    def test_older_dot_separator_passes(self):
        self.assertIsNone(pr.check_build_stamp(f"{STAMP}.{SHA}", SHA))

    def test_dirty_stamp_is_refused(self):
        self.assertIn("uncommitted", pr.check_build_stamp(f"{STAMP}+{SHA}.dirty", SHA))

    def test_nogit_stamps_are_refused(self):
        for stamp in (f"{STAMP}+nogit", f"{STAMP}+{SHA}.nogit"):
            self.assertIn("git could not", pr.check_build_stamp(stamp, SHA), stamp)

    def test_stamp_from_another_commit_is_refused(self):
        self.assertIn("built at ffffffffffff", pr.check_build_stamp(f"{STAMP}+{'f' * 40}", SHA))

    def test_missing_stamp_is_refused(self):
        self.assertIn("no single build stamp", pr.check_build_stamp(None, SHA))

    def test_two_different_stamps_read_as_none(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "TAOM.dll"
            p.write_bytes(_dll_bytes(f"{STAMP}+{SHA}") + _dll_bytes(f"{STAMP}+{'f' * 40}"))
            self.assertIsNone(pr.read_build_stamp(p))

    def test_a_stamp_without_a_revision_is_unrecognised(self):
        self.assertIn("unrecognised", pr.check_build_stamp(STAMP, SHA))


def _git_head():
    """HEAD of the repo the tool lives in, or None outside a git checkout."""
    if not shutil.which("git"):
        return None
    r = subprocess.run(["git", "rev-parse", "HEAD"], cwd=TOOL.parent.parent,
                       capture_output=True, text=True)
    return r.stdout.strip() if r.returncode == 0 else None


class TestRequireBuildUnit(unittest.TestCase):
    def setUp(self):
        self.head = _git_head()
        if self.head is None:
            self.skipTest("not a git checkout")

    def _plan(self, td, stamp):
        dll = Path(td) / "TAOM/bin/Win64_Shipping_Client/TAOM.dll"
        dll.parent.mkdir(parents=True)
        dll.write_bytes(_dll_bytes(stamp))
        return pr.plan_module(Path(td) / "TAOM")

    def test_module_name_case_does_not_skip_the_gate(self):
        # "--modules taom" resolves on Windows and names the plan "taom".
        with tempfile.TemporaryDirectory() as td:
            plan = self._plan(td, f"{STAMP}+{self.head}.dirty")
            plan.name = "taom"
            problems, _checked = pr.require_build([plan], "HEAD")
            self.assertTrue(any("uncommitted" in m for m in problems), problems)

    def test_an_unreadable_dll_is_a_refusal_not_a_traceback(self):
        with tempfile.TemporaryDirectory() as td:
            plan = self._plan(td, f"{STAMP}+{self.head}")
            with mock.patch.object(pr, "read_build_stamp", side_effect=PermissionError("denied")):
                problems, _checked = pr.require_build([plan], "HEAD")
            self.assertTrue(any("cannot read" in m for m in problems), problems)


class TestRequireBuildCli(unittest.TestCase):
    def setUp(self):
        self.head = _git_head()
        if self.head is None:
            self.skipTest("not a git checkout")

    def _modules(self, td, stamp, rel="TAOM/bin/Win64_Shipping_Client/TAOM.dll"):
        src = Path(td) / "Modules"
        dll = src / rel
        dll.parent.mkdir(parents=True, exist_ok=True)
        dll.write_bytes(_dll_bytes(stamp))
        return src

    def _gate(self, src, td, rev="HEAD", modules=("TAOM",), dry_run=True):
        return _run("--source", str(src), "--dest", str(Path(td) / "out"), "--modules", *modules,
                    *(["--dry-run"] if dry_run else []), "--require-build", rev)

    def test_refuses_a_dirty_copy_in_another_binaries_folder(self):
        # Every bin/<platform>/ copy ships (Game Pass, dedicated server, Modding Kit), so a
        # clean Win64 copy must not vouch for a dirty one beside it.
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td, f"{STAMP}+{self.head}")
            self._modules(td, f"{STAMP}+{self.head}.dirty",
                          "TAOM/bin/Win64_Shipping_Server/TAOM.dll")
            r = self._gate(src, td)
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("Win64_Shipping_Server", r.stderr)
            self.assertIn("uncommitted", r.stderr)

    def test_accepts_every_copy_when_all_are_clean(self):
        with tempfile.TemporaryDirectory() as td:
            for folder in ("Win64_Shipping_Client", "Gaming.Desktop.x64_Shipping_Client",
                           "Win64_Shipping_Server", "Win64_Shipping_wEditor"):
                src = self._modules(td, f"{STAMP}+{self.head}", f"TAOM/bin/{folder}/TAOM.dll")
            r = self._gate(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM/bin/Win64_Shipping_Server/TAOM.dll", r.stdout)

    def test_refuses_when_a_requested_module_is_absent(self):
        # Planning skips a missing module folder; the gate must not certify the rest.
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td, f"{STAMP}+{self.head}")
            r = self._gate(src, td, modules=("TAOM", "TAOM.Dependencies"))
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("TAOM.Dependencies", r.stderr)

    def test_an_empty_rev_is_refused_not_skipped(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{self.head}.dirty"), td, rev="")
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("cannot resolve", r.stderr)

    def test_refuses_when_no_taom_assembly_is_in_the_set(self):
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "Modules"
            _write(src, "TAOM_Map/SubModule.xml", 10)
            r = self._gate(src, td, modules=("TAOM_Map",))
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("nothing to verify", r.stderr)

    def test_a_refused_real_run_writes_nothing(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{self.head}.dirty"), td, dry_run=False)
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertFalse((Path(td) / "out").exists())

    def test_refuses_a_commit_that_predates_the_dirty_flag(self):
        # A commit without the TaomStampWorkingTreeState target (the 1.4.5 line, any tag cut
        # before plan 017) wrote no .dirty flag, so a bare SHA there proves nothing.
        root = TOOL.parent.parent
        first = subprocess.run(
            ["git", "log", "--reverse", "--format=%H", "-S", "TaomStampWorkingTreeState",
             "--", "Directory.Build.props"], cwd=root, capture_output=True, text=True,
        ).stdout.split()
        if not first:
            self.skipTest("history does not reach the commit that added the target")
        # rev-parse without --verify echoes an unresolvable argument to stdout, so a depth-1
        # clone (CI's checkout) must be detected by resolve_commit, not an empty string.
        parent = pr.resolve_commit(f"{first[0]}^")
        if parent is None:
            self.skipTest("shallow history")
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{parent}"), td, rev=parent)
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("predates", r.stderr)

    def test_accepts_a_clean_dll_built_at_the_rev(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{self.head}"), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("build stamp OK", r.stdout)

    def test_refuses_a_dirty_dll(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{self.head}.dirty"), td)
            self.assertEqual(r.returncode, 2)
            self.assertIn("uncommitted", r.stderr)

    def test_refuses_when_the_dll_is_missing(self):
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "Modules"
            _write(src, "TAOM/SubModule.xml", 10)
            r = self._gate(src, td)
            self.assertEqual(r.returncode, 2)
            self.assertIn("is missing", r.stderr)

    def test_refuses_an_unresolvable_rev(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{self.head}"), td, rev="no-such-rev-017")
            self.assertEqual(r.returncode, 2)
            self.assertIn("cannot resolve", r.stderr)


# --------------------------------------------------------------------------- #
# Scene shader caches: a report of what each scene carries, never a refusal    #
# --------------------------------------------------------------------------- #
# Whether a scene ships its compressed shader sack is the maintainer's call (sacks have been
# problematic for players; every channel shipped scene sacks on 2026-10-03, and no module-level
# sack), so a scene without one is a fact to list, not an error.
SHADER_DIR = "SceneObj/{}/ShaderCache/D3D11"
CURRENT_FORMAT = 0x0783   # every sack shipped on 2026-10-02 reads 83 07 00 00 at byte 4


def _scene(root: Path, scene: str, header: bool, sack: bool, fmt: int = CURRENT_FORMAT):
    d = SHADER_DIR.format(scene)
    if header:
        _write(root, f"{d}/terrain_shaders_header_data.bin", 10)
    if sack:
        p = root / d / "compressed_shader_cache.sack"
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(b"\0" * 4 + struct.pack("<I", fmt) + b"\0" * 12)
    _write(root, f"SceneObj/{scene}/scene.xscene", 5)


class TestSceneShaderCache(unittest.TestCase):
    def _caches(self, build):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "TAOM_Map"
            _write(root, "SubModule.xml", 10)
            build(root)
            return pr.scene_shader_caches(pr.plan_module(root))

    def _one(self, build):
        caches = self._caches(build)
        self.assertEqual(len(caches), 1, caches)
        return caches[0]

    def test_scene_with_header_and_sack_reads_the_sack_format(self):
        c = self._one(lambda r: _scene(r, "Main_map", True, True))
        self.assertEqual((c.module, c.scene, c.header, c.sack, c.sack_format),
                         ("TAOM_Map", "Main_map", True, True, CURRENT_FORMAT))

    def test_header_without_sack_is_listed_without_a_format(self):
        c = self._one(lambda r: _scene(r, "Main_map", True, False))
        self.assertEqual((c.header, c.sack, c.sack_format), (True, False, None))

    def test_scene_with_neither_file_is_still_listed(self):
        c = self._one(lambda r: _scene(r, "Main_map", False, False))
        self.assertEqual((c.scene, c.header, c.sack), ("Main_map", False, False))

    def test_sack_without_header_is_listed(self):
        c = self._one(lambda r: _scene(r, "Main_map", False, True))
        self.assertEqual((c.header, c.sack, c.sack_format), (False, True, CURRENT_FORMAT))

    def test_every_scene_is_listed_once_sorted(self):
        def build(r):
            _scene(r, "taom_rohan_edoras_town_forceatmo", True, False)
            _scene(r, "Main_map", True, False)
            _scene(r, "mod_taom_erebor_kitbash", True, True)
        self.assertEqual([c.scene for c in self._caches(build)],
                         ["Main_map", "mod_taom_erebor_kitbash", "taom_rohan_edoras_town_forceatmo"])

    def test_scene_backups_are_not_judged(self):
        # SceneObj/Backups is excluded (SCENE_BACKUPS) and never ships.
        def build(r):
            _write(r, "SceneObj/Backups/Main_map/ShaderCache/D3D11/terrain_shaders_header_data.bin", 10)
        self.assertEqual(self._caches(build), [])

    def test_loose_files_under_sceneobj_are_not_scenes(self):
        self.assertEqual(self._caches(lambda r: _write(r, "SceneObj/readme.xml", 3)), [])

    def test_a_short_sack_says_why_its_format_is_unknown(self):
        def build(r):
            _write(r, "SceneObj/Main_map/ShaderCache/D3D11/compressed_shader_cache.sack", 5)
        c = self._one(build)
        self.assertEqual((c.sack, c.sack_format), (True, None))
        self.assertIn("shorter than", c.note)

    def test_file_names_match_whatever_their_case(self):
        def build(r):
            d = "SceneObj/Main_map/ShaderCache/D3D11"
            _write(r, f"{d}/Terrain_Shaders_Header_Data.bin", 10)
            p = r / d / "COMPRESSED_SHADER_CACHE.SACK"
            p.write_bytes(b"\0" * 4 + struct.pack("<I", CURRENT_FORMAT))
        c = self._one(build)
        self.assertEqual((c.header, c.sack, c.sack_format), (True, True, CURRENT_FORMAT))


class TestStaleSackFormat(unittest.TestCase):
    def _cache(self, scene, fmt, sack=None):
        return pr.SceneShaderCache("TAOM_Map", scene, True,
                                   fmt is not None if sack is None else sack, fmt)

    def test_one_format_flags_nothing(self):
        caches = [self._cache("a", CURRENT_FORMAT), self._cache("b", CURRENT_FORMAT)]
        self.assertEqual(pr.stale_sack_formats(caches), (CURRENT_FORMAT, []))

    def test_a_sack_off_the_majority_format_is_flagged(self):
        odd = self._cache("old", 0x0782)
        caches = [self._cache("a", CURRENT_FORMAT), odd, self._cache("b", CURRENT_FORMAT)]
        self.assertEqual(pr.stale_sack_formats(caches), (CURRENT_FORMAT, [odd]))

    def test_scenes_without_a_readable_format_are_not_counted(self):
        self.assertEqual(pr.stale_sack_formats([self._cache("a", None)]), (None, []))
        unreadable = self._cache("short", None, sack=True)
        self.assertEqual(pr.stale_sack_formats([unreadable, self._cache("b", CURRENT_FORMAT)]),
                         (CURRENT_FORMAT, []))

    def test_a_set_uniformly_behind_the_engine_is_not_flagged(self):
        # The comparison is among the shipped scene sacks only: when every one of them lags the
        # engine, none differs from the majority. The docs say so. (#448's lagging sacks were
        # module-level, which the scene report never compares: see
        # test_issue_448s_module_level_sack_is_listed_not_compared.)
        caches = [self._cache("a", 0x0782), self._cache("b", 0x0782)]
        self.assertEqual(pr.stale_sack_formats(caches), (0x0782, []))

    def test_a_tie_keeps_the_newer_format_as_the_majority(self):
        old = self._cache("old", 0x0782)
        self.assertEqual(pr.stale_sack_formats([old, self._cache("new", CURRENT_FORMAT)]),
                         (CURRENT_FORMAT, [old]))


class TestSceneShaderCacheCli(unittest.TestCase):
    def _modules(self, td):
        src = Path(td) / "Modules"
        _write(src, "TAOM_Map/SubModule.xml", 10)
        _scene(src / "TAOM_Map", "Main_map", True, False)
        _scene(src / "TAOM_Map", "mod_taom_erebor_kitbash", True, True)
        return src

    def _run_map(self, src, td, dry_run=True):
        return _run("--source", str(src), "--dest", str(Path(td) / "out"), "--modules", "TAOM_Map",
                    *(["--dry-run"] if dry_run else []))

    def test_dry_run_lists_every_scene_and_does_not_refuse(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._run_map(self._modules(td), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM_Map/SceneObj/Main_map: header yes, sack no", r.stdout)
            self.assertIn("TAOM_Map/SceneObj/mod_taom_erebor_kitbash: header yes, sack yes "
                          "(format 0x0783)", r.stdout)
            self.assertIn("2 scenes, 1 with a sack, 1 without (1 with the header only)", r.stdout)
            self.assertNotIn("WARNING", r.stdout)

    def test_a_real_run_ships_a_scene_without_its_sack(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._run_map(self._modules(td), td, dry_run=False)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM_Map/SceneObj/Main_map: header yes, sack no", r.stdout)
            out = Path(td) / "out/TAOM_Map" / SHADER_DIR.format("Main_map")
            self.assertTrue((out / "terrain_shaders_header_data.bin").exists())

    def test_a_stale_format_sack_gets_a_warning_and_no_refusal(self):
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td)
            _scene(src / "TAOM_Map", "second", True, True)
            _scene(src / "TAOM_Map", "old_scene", True, True, fmt=0x0782)
            r = self._run_map(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("WARNING: TAOM_Map/SceneObj/old_scene ships a sack in format 0x0782, "
                          "not the majority 0x0783 of the shipped sacks", r.stdout)
            self.assertNotIn("WARNING: TAOM_Map/SceneObj/second", r.stdout)

    def test_issue_448s_module_level_sack_is_listed_not_compared(self):
        # #448's own layout (2026-08-10): every TAOM_Map scene header-only, and the one lagging
        # sack at the module level, <Module>/Shaders/D3D11/, in format 0x0782. The scene report
        # reads SceneObj/<scene>/ShaderCache/D3D11 only, so it compares no format and warns of
        # nothing. The module report lists that sack once, with its format, and the copy leaves
        # it out, so a lagging module sack cannot ship.
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "Modules"
            _write(src, "TAOM_Map/SubModule.xml", 10)
            _scene(src / "TAOM_Map", "Main_map", True, False)
            _scene(src / "TAOM_Map", "taom_rohan_edoras_town_forceatmo", True, False)
            sack = src / "TAOM_Map/Shaders/D3D11/compressed_shader_cache.sack"
            sack.parent.mkdir(parents=True)
            sack.write_bytes(b"\0" * 4 + struct.pack("<I", 0x0782) + b"\0" * 12)
            r = self._run_map(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("2 scenes, 0 with a sack, 2 without (2 with the header only); "
                          "sack formats: none", r.stdout)
            self.assertIn("  TAOM_Map/Shaders/D3D11/compressed_shader_cache.sack: 20 bytes, "
                          "format 0x0782\n", r.stdout)
            self.assertEqual(r.stdout.count("0x0782"), 1)
            self.assertNotIn("WARNING", r.stdout)

    def test_an_unreadable_sack_is_listed_and_counted(self):
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td)
            _write(src / "TAOM_Map", SHADER_DIR.format("short") + "/compressed_shader_cache.sack", 5)
            r = self._run_map(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM_Map/SceneObj/short: header no, sack yes (format unreadable: 5 bytes",
                          r.stdout)
            self.assertIn("sack formats: 0x0783 x1, 1 unreadable", r.stdout)

    def test_a_scene_name_the_console_cannot_encode_does_not_change_the_exit_code(self):
        # A piped stdout on Windows is cp1252; the report must not turn a scene name into a
        # traceback and exit 1 (the report never changes the exit code).
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td)
            _scene(src / "TAOM_Map", "сцена", True, True)
            env = dict(os.environ, PYTHONIOENCODING="cp1252")
            env.pop("PYTHONUTF8", None)
            r = subprocess.run([sys.executable, str(TOOL), "--source", str(src), "--dest",
                                str(Path(td) / "out"), "--modules", "TAOM_Map", "--dry-run"],
                               capture_output=True, env=env)
            out = r.stdout.decode("cp1252")
            self.assertEqual(r.returncode, 0, r.stderr.decode("cp1252", "replace"))
            self.assertIn("TAOM_Map/SceneObj/\\u0441\\u0446\\u0435\\u043d\\u0430: header yes", out)
            self.assertIn("(dry run", out)

    def test_a_failing_report_does_not_change_the_exit_code(self):
        def boom(_plans):
            raise RuntimeError("probe")
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td)
            out = io.StringIO()
            with mock.patch.object(pr, "_shader_cache_report", boom), \
                    mock.patch.object(pr, "_jit_report", boom), contextlib.redirect_stdout(out):
                code = pr.main(["--source", str(src), "--dest", str(Path(td) / "out"),
                                "--modules", "TAOM_Map", "--dry-run"])
            self.assertEqual(code, 0)
            self.assertIn("scene shader cache report failed, ignored: RuntimeError: probe",
                          out.getvalue())
            self.assertIn("JIT optimization report failed, ignored: RuntimeError: probe",
                          out.getvalue())
            self.assertIn("(dry run", out.getvalue())

    def test_a_run_without_scenes_says_so(self):
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "Modules"
            _write(src, "TAOM_Map/SubModule.xml", 10)
            r = self._run_map(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("scene shader caches: no SceneObj/<scene> folder", r.stdout)


# --------------------------------------------------------------------------- #
# Module-level shader sacks: listed, and left out of the copy                  #
# --------------------------------------------------------------------------- #
# <Module>/Shaders/D3D11/compressed_shader_cache.sack is a module's own compiled shader cache. The
# dev install holds one for LOTRLOME_Armory, TAOM_Map and TAOM (TAOM's header only; the
# maintainer's survey of 2026-10-03), and no release channel ships one (listed the same day).
# His policy: the target is to ship TAOM's and the Armory's and never TAOM_Map's, but not yet,
# only after a test proves the game uses a Kit-built sack. Until then the packager lists every
# module sack and copies none, for every module. Scene sacks and the folder's other files are
# untouched, and nothing refuses or changes the exit code.
MODULE_SACK = "Shaders/D3D11/compressed_shader_cache.sack"
POLICY = ("module shader sacks are left out: shipping TAOM's and the Armory's waits on a test "
          "that the game uses a Kit-built sack; TAOM_Map's never ships")


def _module_sack(root: Path, size: int, fmt: int = CURRENT_FORMAT):
    """A module-level sack of `size` bytes (8 or more) whose format dword, bytes 4 to 7, is
    `fmt`. The first dword is nonzero so a reader that takes the wrong one is caught."""
    p = root / MODULE_SACK
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(b"\xaa" * 4 + struct.pack("<I", fmt) + b"\0" * (size - 8))


class TestModuleShaderSackRule(unittest.TestCase):
    def test_the_module_sack_is_excluded_under_its_own_rule(self):
        d = pr.classify(MODULE_SACK)
        self.assertEqual((d.action, d.rule), (pr.EXCLUDE, "MODULE_SHADER_SACK"))

    def test_the_other_files_in_the_folder_still_ship(self):
        # Whether shader_mapping.bin and shader_compile_report.log ship is an open question for
        # the maintainer, so they stay as they were.
        for name in ("shader_mapping.bin", "shader_compile_report.log"):
            self.assertEqual(act(f"Shaders/D3D11/{name}"), pr.COPY, name)

    def test_a_scene_sack_still_ships(self):
        self.assertEqual(act("SceneObj/Main_map/ShaderCache/D3D11/compressed_shader_cache.sack"),
                         pr.COPY)

    def test_only_a_sack_directly_in_shaders_d3d11_is_module_level(self):
        for rel in ("Shaders/D3D11/old/compressed_shader_cache.sack",
                    "Shaders/compressed_shader_cache.sack",
                    "Shaders/D3D12/compressed_shader_cache.sack"):
            self.assertEqual(act(rel), pr.COPY, rel)

    def test_the_names_match_whatever_their_case(self):
        # Windows resolves them case-insensitively, and so does the scene report.
        for rel in ("Shaders/D3D11/COMPRESSED_SHADER_CACHE.SACK",
                    "Shaders/d3d11/compressed_shader_cache.sack",
                    "shaders/D3D11/compressed_shader_cache.sack"):
            self.assertEqual(rule(rel), "MODULE_SHADER_SACK", rel)


class TestModuleShaderSackPlan(unittest.TestCase):
    def test_a_module_sack_is_recorded_and_never_enters_the_copy_list(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "TAOM_Map"
            _write(root, "SubModule.xml", 10)
            _write(root, "Shaders/D3D11/shader_mapping.bin", 7)
            _write(root, "AssetSources/big.png", 9)          # excluded, but not a sack
            _module_sack(root, 40)
            plan = pr.plan_module(root)
            self.assertEqual(plan.module_sacks, [(MODULE_SACK, 40)])
            self.assertEqual(sorted(rel for rel, _size in plan._copy_list),
                             ["Shaders/D3D11/shader_mapping.bin", "SubModule.xml"])
            self.assertEqual((plan.copy_bytes, plan.exclude_bytes, plan.unknown_bytes),
                             (17, 49, 0))
            self.assertEqual(plan.by_rule, {"MODULE_SHADER_SACK": 40, "ASSET_SOURCES": 9})

    def test_a_scene_sack_is_not_a_module_sack(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "TAOM_Map"
            _write(root, "SubModule.xml", 10)
            _scene(root, "Main_map", True, True)
            plan = pr.plan_module(root)
            self.assertEqual(plan.module_sacks, [])
            self.assertEqual(plan.by_rule, {})

    def test_an_excluded_sack_that_is_not_the_module_sack_is_not_recorded_as_one(self):
        # The Kit's scene backups carry their own ShaderCache, and a tool backup of the module
        # sack ends in a backup suffix: both are excluded, under their own rules.
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "TAOM_Map"
            _write(root, "SubModule.xml", 10)
            backup_sack = SHADER_DIR.format("Backups/Main_map") + "/compressed_shader_cache.sack"
            _write(root, backup_sack, 11)
            _write(root, MODULE_SACK + ".bak", 12)
            plan = pr.plan_module(root)
            self.assertEqual(plan.module_sacks, [])
            self.assertEqual(plan.by_rule, {"SCENE_BACKUPS": 11, "BACKUP_SIDECAR": 12})


class TestModuleShaderSackCli(unittest.TestCase):
    # Distinct sizes and formats, so each reported line is tied to its own module.
    SACKS = (("LOTRLOME_Armory", 4000, 0x0783), ("TAOM", 36, 0x0781), ("TAOM_Map", 300, 0x0782))

    def _modules(self, td):
        src = Path(td) / "Modules"
        for module, size, fmt in self.SACKS:
            root = src / module
            _write(root, "SubModule.xml", 10)
            _write(root, "Shaders/D3D11/shader_mapping.bin", 5)
            _write(root, "Shaders/D3D11/shader_compile_report.log", 6)
            _module_sack(root, size, fmt)
        _scene(src / "TAOM_Map", "Main_map", True, True)
        return src

    def _run_all(self, src, td, dry_run=True):
        return _run("--source", str(src), "--dest", str(Path(td) / "out"),
                    "--modules", *(m for m, _size, _fmt in self.SACKS),
                    *(["--dry-run"] if dry_run else []))

    def test_every_modules_sack_is_listed_with_its_size_and_format(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._run_all(self._modules(td), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            for line in (
                "LOTRLOME_Armory/Shaders/D3D11/compressed_shader_cache.sack: 4,000 bytes, "
                "format 0x0783",
                "TAOM/Shaders/D3D11/compressed_shader_cache.sack: 36 bytes, format 0x0781",
                "TAOM_Map/Shaders/D3D11/compressed_shader_cache.sack: 300 bytes, format 0x0782",
            ):
                self.assertIn(f"  {line}\n", r.stdout)
            self.assertNotIn("WARNING", r.stdout)

    def test_the_policy_is_stated_once_and_the_leave_out_is_counted(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._run_all(self._modules(td), td)
            self.assertEqual(r.stdout.count(POLICY), 1, r.stdout)
            self.assertIn("MODULE_SHADER_SACK", r.stdout)     # the dropped, by rule table

    def test_a_real_run_copies_no_module_sack_and_ships_the_rest(self):
        with tempfile.TemporaryDirectory() as td:
            dest = Path(td) / "out"
            r = self._run_all(self._modules(td), td, dry_run=False)
            self.assertEqual(r.returncode, 0, r.stderr)
            for module, _size, _fmt in self.SACKS:
                self.assertFalse((dest / module / MODULE_SACK).exists(), module)
                for other in ("shader_mapping.bin", "shader_compile_report.log"):
                    self.assertTrue((dest / module / "Shaders/D3D11" / other).exists(), other)
            scene_sack = (dest / "TAOM_Map" / SHADER_DIR.format("Main_map")
                          / "compressed_shader_cache.sack")
            self.assertTrue(scene_sack.exists(), "the scene's own sack keeps shipping")
            self.assertIn("TAOM_Map/SceneObj/Main_map: header yes, sack yes (format 0x0783)",
                          r.stdout)

    def test_a_dry_run_writes_nothing(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._run_all(self._modules(td), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertFalse((Path(td) / "out").exists())

    def test_a_short_sack_is_listed_with_why_its_format_is_unknown(self):
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td)
            _write(src / "TAOM", MODULE_SACK, 5)
            r = self._run_all(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("  TAOM/Shaders/D3D11/compressed_shader_cache.sack: 5 bytes, "
                          "format unreadable: 5 bytes, shorter than the 8-byte prefix", r.stdout)
            self.assertIn("  TAOM_Map/Shaders/D3D11/compressed_shader_cache.sack: 300 bytes, "
                          "format 0x0782\n", r.stdout)

    def test_a_run_without_module_sacks_still_states_the_policy(self):
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "Modules"
            _write(src, "TAOM_Map/SubModule.xml", 10)
            _scene(src / "TAOM_Map", "Main_map", True, False)
            r = _run("--source", str(src), "--dest", str(Path(td) / "out"),
                     "--modules", "TAOM_Map", "--dry-run")
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn(f"{POLICY}\n  none in the planned modules\n", r.stdout)

    def test_a_failing_report_changes_neither_the_exit_code_nor_what_is_left_out(self):
        # The exclusion lives in classify, not in the report: a report that dies cannot ship a sack.
        def boom(_plans):
            raise RuntimeError("probe")
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td)
            dest = Path(td) / "out"
            out = io.StringIO()
            with mock.patch.object(pr, "_module_sack_report", boom), \
                    contextlib.redirect_stdout(out):
                code = pr.main(["--source", str(src), "--dest", str(dest), "--modules", "TAOM_Map"])
            self.assertEqual(code, 0)
            self.assertIn("module shader sack report failed, ignored: RuntimeError: probe",
                          out.getvalue())
            self.assertFalse((dest / "TAOM_Map" / MODULE_SACK).exists())
            self.assertTrue((dest / "TAOM_Map/Shaders/D3D11/shader_mapping.bin").exists())


# --------------------------------------------------------------------------- #
# JIT optimization report: read DebuggableAttribute, never refuse              #
# --------------------------------------------------------------------------- #
def _pad4(b: bytes) -> bytes:
    return b + b"\0" * (-len(b) % 4)


# The MethodRefSig (ECMA-335 II.23.2.2) of each DebuggableAttribute constructor as the compilers
# write it: HASTHIS, the parameter count, void, then two Booleans, or the DebuggingModes enum, a
# value type (0x11) named by a compressed TypeDefOrRef token. All 16,813 attributes in a survey of
# 18,490 DLLs on 2026-10-03 had the second form, with a one or two byte token and an 8-byte value.
# The fixture's enum is TypeRef 2, so its token is (2 << 2) | 1.
BOOLS_CTOR = b"\x20\x02\x01\x02\x02"
MODES_CTOR = b"\x20\x01\x01\x11\x09"


def _managed_dll(ca_value=None, *, pe32=False, heap_sizes=0, filler=False, typerefs=0,
                 memberrefs=0, methods=0, params=0, native=False, ctor_sig=None) -> bytes:
    """A minimal managed PE whose assembly carries
    [assembly: System.Diagnostics.DebuggableAttribute(...)] with the value blob `ca_value`
    (prolog included). None builds the assembly without the attribute.

    Default: PE32+ (x64), tables Module, TypeRef (DebuggableAttribute and its nested enum
    DebuggingModes), MemberRef (.ctor), CustomAttribute and Assembly, every heap and coded index
    2 bytes wide.
    The options reach the shapes real assemblies take. TAOM.Dependencies.dll is PE32 with every
    index 2 bytes. The installed TAOM.dll (measured 2026-10-03) is PE32+ with HeapSizes 0x05
    (#Strings and #Blob indexes 4 bytes, #GUID 2), rows in tables 0x00, 0x01, 0x02, 0x04, 0x06,
    0x08, 0x09, 0x0A, 0x0B and 0x0C (none in 0x03, 0x05 or 0x07), and 20,114 MethodDef rows that
    alone make MemberRefParent and CustomAttributeType 4 bytes (TypeRef holds 1,349 rows and
    MemberRef 8,068, both under 8,192); ResolutionScope and TypeDefOrRef stay 2 bytes.
    test_the_installed_taom_dll_shape builds that shape. The patreon and public channel copies
    (measured the same day) are PE32 with HeapSizes 0x05, TypeRef 1,193, MemberRef 6,243,
    MethodDef 15,553 and Param 14,333 rows, so HasConstant stays 2 bytes while
    MemberRefParent, CustomAttributeType and HasCustomAttribute are 4;
    test_the_channel_taom_dll_shape builds that one.
      pe32        a PE32 optional header (data directories at +96, not +112)
      heap_sizes  the HeapSizes byte: bit 0x01 makes #Strings indexes 4 bytes, 0x02 #GUID,
                  0x04 #Blob, each on its own
      filler      one row each in TypeDef, Field, MethodDef, Param, InterfaceImpl and Constant;
                  a DebuggableAttribute in the Release form on the Module (which never counts)
                  and [assembly: CompilationRelaxations(8)] sorted before the real attribute
      typerefs    pad TypeRef to this many rows (2048 widens HasCustomAttribute; 8192 also
                  MemberRefParent; 16384 also ResolutionScope and TypeDefOrRef)
      memberrefs  pad MemberRef to this many rows (2048 widens HasCustomAttribute; 8192 also
                  CustomAttributeType)
      methods     with filler, pad MethodDef to this many rows (2048 widens HasCustomAttribute;
                  8192 also MemberRefParent and CustomAttributeType; 65536 also TypeDef's
                  simple MethodList index)
      params      with filler, pad Param to this many rows (16384 widens HasConstant; 65536
                  also MethodDef's simple ParamList index)
      native      no CLI header (data directory 14 zeroed), as in a native DLL
      ctor_sig    the MethodRefSig of the attribute's constructor: by default BOOLS_CTOR for a
                  6-byte value and MODES_CTOR for any other
    The widths below are written from ECMA-335 II.24.2.6, not taken from the reader. The layout
    follows II.24 and II.25; System.Reflection.Metadata reads the default shape back."""
    strings = (b"\0fixture.dll\0fixture\0DebuggableAttribute\0System.Diagnostics\0.ctor\0"
               b"CompilationRelaxationsAttribute\0System.Runtime.CompilerServices\0Filler\0"
               b"DebuggingModes\0")

    def s(x):
        return strings.index(x + b"\0")

    blob = bytearray(b"\0")

    def add_blob(data):
        blob.extend(bytes([len(data)]) + data)
        return len(blob) - len(data) - 1

    if ctor_sig is None:
        ctor_sig = BOOLS_CTOR if ca_value is not None and len(ca_value) == 6 else MODES_CTOR
    sig_dbg = add_blob(ctor_sig)
    sig_int = add_blob(b"\x20\x01\x01\x08")
    val = add_blob(ca_value) if ca_value is not None else 0
    release_val = add_blob(b"\x01\x00\x02\x00\x00\x00\x00\x00")
    relax_val = add_blob(b"\x01\x00\x08\x00\x00\x00\x00\x00")
    field_sig, method_sig = add_blob(b"\x06\x08"), add_blob(b"\x00\x00\x01")
    const_val = add_blob(b"\x00\x00\x00\x00")

    def w(big):
        return "I" if big else "H"

    S, G, B = w(heap_sizes & 0x01), w(heap_sizes & 0x02), w(heap_sizes & 0x04)
    n_tr = max(typerefs, 3 if filler else 2)
    n_mr = max(memberrefs, 2 if filler else 1)
    n_md = max(methods, 1) if filler else 0
    n_pa = max(params, 1) if filler else 0

    scope = tdor = w(n_tr >= 1 << 14)                       # 2 tag bits, TypeRef among targets
    parent = w(max(n_tr, n_md) >= 1 << 13)                  # MemberRefParent, 3 tag bits
    hca = w(max(n_tr, n_mr, n_md, n_pa) >= 1 << 11)         # HasCustomAttribute, 5 tag bits
    cat = w(max(n_md, n_mr) >= 1 << 13)                     # CustomAttributeType, 3 tag bits
    hasconst = w(n_pa >= 1 << 14)                           # HasConstant, 2 tag bits
    method_idx = w(n_md >= 1 << 16)                         # a simple index into MethodDef
    param_idx = w(n_pa >= 1 << 16)                          # a simple index into Param

    def pk(fmt, *v):
        return struct.pack("<" + fmt, *v)

    t = {0x00: [pk("H" + S + G + G + G, 0, s(b"fixture.dll"), 1, 0, 0)]}
    tr = [pk(scope + S + S, (1 << 2) | 0, s(b"DebuggableAttribute"), s(b"System.Diagnostics")),
          pk(scope + S + S, (1 << 2) | 3, s(b"DebuggingModes"), 0)]     # nested in TypeRef 1
    mr = [pk(parent + S + B, (1 << 3) | 1, s(b".ctor"), sig_dbg)]   # MemberRef on TypeRef 1
    ca = []
    if filler:
        tr.append(pk(scope + S + S, (1 << 2) | 0, s(b"CompilationRelaxationsAttribute"),
                     s(b"System.Runtime.CompilerServices")))
        mr.append(pk(parent + S + B, (3 << 3) | 1, s(b".ctor"), sig_int))
        t[0x02] = [pk("I" + S + S + tdor + "H" + method_idx, 0x100001, s(b"Filler"), 0, 0, 1,
                      1)]                                                             # TypeDef
        t[0x04] = [pk("H" + S + B, 0x16, s(b"Filler"), field_sig)]                    # Field
        t[0x06] = ([pk("IHH" + S + B + param_idx, 0, 0, 0x96, s(b"Filler"), method_sig, 1)]
                   + [pk("IHH" + S + B + param_idx, 0, 0, 0, 0, 0, n_pa + 1)] * (n_md - 1))
        t[0x08] = ([pk("HH" + S, 0, 1, s(b"Filler"))]                                 # Param
                   + [pk("HH" + S, 0, 0, 0)] * (n_pa - 1))
        t[0x09] = [pk("H" + tdor, 1, (1 << 2) | 1)]                  # InterfaceImpl: TypeRef 1
        t[0x0B] = [pk("BB" + hasconst + B, 0x08, 0, (1 << 2) | 0, const_val)]  # on Field 1
        ca.append(pk(hca + cat + B, (1 << 5) | 7, (1 << 3) | 3, release_val))    # on the Module
        ca.append(pk(hca + cat + B, (1 << 5) | 14, (2 << 3) | 3, relax_val))    # on the Assembly
    tr += [pk(scope + S + S, 0, 0, 0)] * (n_tr - len(tr))
    mr += [pk(parent + S + B, 0, 0, 0)] * (n_mr - len(mr))
    t[0x01], t[0x0A] = tr, mr
    if ca_value is not None:
        ca.append(pk(hca + cat + B, (1 << 5) | 14, (1 << 3) | 3, val))
    if ca:
        t[0x0C] = ca
    t[0x20] = [pk("IHHHHI" + B + S + S, 0x8004, 1, 0, 0, 0, 0, 0, s(b"fixture"), 0)]  # Assembly
    present = sorted(t)
    tables = struct.pack("<IBBBBQQ", 0, 2, 0, heap_sizes, 1,
                         sum(1 << x for x in present), 0)
    tables += b"".join(struct.pack("<I", len(t[x])) for x in present)
    tables += b"".join(b"".join(t[x]) for x in present)
    heaps = [(b"#~", _pad4(tables)), (b"#Strings", _pad4(strings)), (b"#GUID", b"\x11" * 16),
             (b"#Blob", _pad4(bytes(blob)))]
    version = _pad4(b"v4.0.30319\0")
    offset = 16 + len(version) + 4 + sum(8 + len(_pad4(n + b"\0")) for n, _d in heaps)
    headers, body = b"", b""
    for name, data in heaps:
        headers += struct.pack("<II", offset, len(data)) + _pad4(name + b"\0")
        body += data
        offset += len(data)
    md = (struct.pack("<IHHII", 0x424A5342, 1, 1, 0, len(version)) + version
          + struct.pack("<HH", 0, len(heaps)) + headers + body)

    rva, file_align, headers_size, pe_off = 0x2000, 0x200, 0x200, 0x80
    cli = struct.pack("<IHHII", 72, 2, 5, rva + 72, len(md)) + b"\0" * (72 - 16)
    section = cli + md
    raw = section + b"\0" * (-len(section) % file_align)
    dirs = [(0, 0)] * 16
    if not native:
        dirs[14] = (rva, 72)                                # the CLI header directory
    image_size = rva + (len(raw) + 0x1FFF) // 0x2000 * 0x2000
    if pe32:    # BaseOfData and 4-byte ImageBase and stack/heap sizes: directories at +96
        opt = struct.pack("<HBBIIIIII", 0x10B, 11, 0, len(raw), 0, 0, 0, rva, 0)
        opt += struct.pack("<IIIHHHHHHIIIIHHIIIIII", 0x10000000, 0x2000, file_align, 4, 0, 0, 0,
                           4, 0, 0, image_size, headers_size, 0, 3, 0x8540,
                           0x100000, 0x1000, 0x100000, 0x1000, 0, 16)
    else:       # PE32+: directories at +112
        opt = struct.pack("<HBBIIIII", 0x20B, 11, 0, len(raw), 0, 0, 0, rva)
        opt += struct.pack("<QIIHHHHHHIIIIHHQQQQII", 0x180000000, 0x2000, file_align, 4, 0, 0, 0,
                           4, 0, 0, image_size, headers_size, 0, 3, 0x8540,
                           0x100000, 0x1000, 0x100000, 0x1000, 0, 16)
    opt += b"".join(struct.pack("<II", *x) for x in dirs)
    coff = struct.pack("<HHIIIHH", 0x14C if pe32 else 0x8664, 1, 0, 0, 0, len(opt), 0x2022)
    sect = struct.pack("<8sIIIIIIHHI", b".text", len(section), rva, len(raw), headers_size,
                       0, 0, 0, 0, 0x60000020)
    dos = b"MZ" + b"\0" * 0x3A + struct.pack("<I", pe_off)
    head = dos + b"\0" * (pe_off - len(dos)) + b"PE\0\0" + coff + opt + sect
    return head + b"\0" * (headers_size - len(head)) + raw


DEBUG_MODES = b"\x01\x00\x07\x01\x00\x00\x00\x00"    # Default|IgnoreSymbolStore|EnC|DisableOptimizations
RELEASE_MODES = b"\x01\x00\x02\x00\x00\x00\x00\x00"  # IgnoreSymbolStoreSequencePoints only
# The two-Boolean constructor's values, (isJITTrackingEnabled, isJITOptimizerDisabled):
BOOLS_DISABLED = b"\x01\x00\x01\x01\x00\x00"         # (true, true)
BOOLS_ENABLED = b"\x01\x00\x01\x00\x00\x00"          # (true, false)


class TestJitOptimization(unittest.TestCase):
    def _read(self, data: bytes):
        """jit_optimization's (verdict, reason) for these bytes written as a DLL."""
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "TAOM.dll"
            p.write_bytes(data)
            return pr.jit_optimization(p)

    def test_debug_modes_disable_optimization(self):
        self.assertEqual(self._read(_managed_dll(DEBUG_MODES)), (True, ""))

    def test_release_modes_keep_optimization(self):
        self.assertEqual(self._read(_managed_dll(RELEASE_MODES)), (False, ""))

    # What the JIT does with a DebuggableAttribute value, measured on .NET Framework 4.8.1
    # (clr.dll 4.8.9345.0, 64-bit) on 2026-10-04: one assembly per value, then whether the JIT
    # inlined a small callee and let a dead local be collected. All 1,304 DebuggingModes values
    # and 30 pairs of Boolean bytes tried fit one rule: the JIT generates unoptimized code only
    # when the first byte of the value has its low bit set (Default, or isJITTrackingEnabled) and
    # the second byte is not zero (DisableOptimizations is bit 0 of it, or isJITOptimizerDisabled).
    # Both constructors write those same two bytes, so one rule serves both. Each row is a
    # DebuggingModes value and whether it means unoptimized code.
    RUNTIME_RULE = (
        (0x107, True),      # what a Debug build stamps
        (0x101, True),      # Default | DisableOptimizations, the pair the documentation names
        (0x100, False),     # DisableOptimizations alone: the JIT still optimizes
        (0x001, False),     # Default alone
        (0x102, False),     # byte 0 is not zero, but its low bit is clear
        (0x0ff, False),     # every bit of byte 0 and nothing in byte 1
        (0x200, False),     # something in byte 1, but Default is clear
        (0x201, True),      # byte 1 counts whole: bit 8 is not the only bit that does
        (0x10001, False),   # the upper two bytes are not read: Default, then a bit in byte 2
        (0x10100, False),   # DisableOptimizations, then a bit in byte 2
        (0x10101, True),    # and beside both, a bit in byte 2 changes nothing
    )

    def test_each_debugging_modes_value_reads_as_the_runtime_treats_it(self):
        for modes, expected in self.RUNTIME_RULE:
            value = b"\x01\x00" + struct.pack("<i", modes) + b"\x00\x00"
            with self.subTest(modes=hex(modes)):
                self.assertEqual(self._read(_managed_dll(value)), (expected, ""))

    def test_the_two_boolean_constructor_reads_as_the_runtime_treats_it(self):
        # (isJITTrackingEnabled, isJITOptimizerDisabled) are the same two bytes, so an optimizer
        # disabled with tracking off still leaves the JIT optimizing (measured with the rule above).
        for tracking, disabled, expected in ((1, 1, True), (1, 0, False), (0, 1, False),
                                             (0, 0, False)):
            value = b"\x01\x00" + bytes([tracking, disabled]) + b"\x00\x00"
            with self.subTest(tracking=tracking, optimizer_disabled=disabled):
                self.assertEqual(self._read(_managed_dll(value)), (expected, ""))

    def test_bool_constructor_with_optimizer_disabled(self):
        self.assertEqual(self._read(_managed_dll(b"\x01\x00\x01\x01\x00\x00")), (True, ""))

    def test_bool_constructor_with_optimizer_enabled(self):
        self.assertEqual(self._read(_managed_dll(b"\x01\x00\x01\x00\x00\x00")), (False, ""))

    def test_no_debuggable_attribute_means_optimized_and_says_so(self):
        # An absent attribute and one that keeps optimization on both read False; only the
        # absent one carries a note, so the report can tell them apart (the review's T5/N3: a
        # bare ON for an assembly with no attribute reads the same as a misparse).
        self.assertEqual(self._read(_managed_dll(None)),
                         (False, "no DebuggableAttribute on the assembly"))

    def test_other_attributes_do_not_stand_in_for_a_missing_debuggable_attribute(self):
        # filler=True puts a DebuggableAttribute on the Module (which never counts) and another
        # attribute type on the assembly before the point where the real one would sit.
        self.assertEqual(self._read(_managed_dll(None, filler=True)),
                         (False, "no DebuggableAttribute on the assembly"))

    def test_a_present_attribute_that_keeps_optimization_carries_no_note(self):
        for modes in (RELEASE_MODES, b"\x01\x00\x01\x00\x00\x00"):
            self.assertEqual(self._read(_managed_dll(modes)), (False, ""), modes)

    # The shapes the shipped DLLs take; each pairs a Debug and a Release read so that a reader
    # landing on the wrong bytes cannot pass by returning a constant.
    def _both(self, **shape):
        self.assertEqual(self._read(_managed_dll(DEBUG_MODES, **shape)), (True, ""), shape)
        self.assertEqual(self._read(_managed_dll(RELEASE_MODES, **shape)), (False, ""), shape)

    def test_a_pe32_assembly(self):
        self._both(pe32=True)

    def test_each_heap_size_bit_on_its_own(self):
        # One bit at a time, so a reader that takes one heap's width from another's bit misreads.
        for heap_sizes in (0x01, 0x02, 0x04):
            self._both(heap_sizes=heap_sizes, filler=True)

    def test_every_heap_index_four_bytes(self):
        self._both(heap_sizes=0x07, filler=True)

    def test_the_installed_taom_dll_shape(self):
        # Row counts and HeapSizes read off the installed Win64 TAOM.dll on 2026-10-03: #GUID
        # stays 2 bytes while #Strings and #Blob are 4, and MemberRefParent and
        # CustomAttributeType are 4 bytes only because of MethodDef.
        self._both(heap_sizes=0x05, filler=True, typerefs=1349, memberrefs=8068, methods=20114,
                   params=19152)

    def test_the_channel_taom_dll_shape(self):
        # The TAOM.dll the patreon and public channels ship, measured 2026-10-03: PE32, and
        # Param under 16,384 rows, so HasConstant is 2 bytes beside 4-byte coded indexes
        # widened by MethodDef.
        self._both(pe32=True, heap_sizes=0x05, filler=True, typerefs=1193, memberrefs=6243,
                   methods=15553, params=14333)

    def test_rows_and_other_attributes_before_it_are_skipped(self):
        self._both(filler=True)

    def test_a_four_byte_has_custom_attribute_index(self):
        self._both(typerefs=2048)

    def test_a_four_byte_member_ref_parent_index(self):
        self._both(typerefs=8192, filler=True)

    def test_four_byte_resolution_scope_and_type_def_or_ref_indexes(self):
        self._both(typerefs=16384, filler=True)

    def test_a_four_byte_custom_attribute_type_index(self):
        self._both(memberrefs=8192, filler=True)

    def test_a_four_byte_has_custom_attribute_index_through_method_def(self):
        self._both(methods=2048, filler=True)

    def test_four_byte_member_ref_parent_and_attribute_type_through_method_def(self):
        # TypeRef and MemberRef stay small, so only MethodDef can widen these two coded indexes.
        self._both(methods=8192, filler=True)

    def test_a_four_byte_method_list_index(self):
        self._both(methods=65536, filler=True)

    def test_a_four_byte_has_constant_index(self):
        self._both(params=16384, filler=True)

    def test_a_four_byte_simple_table_index(self):
        self._both(params=65536, filler=True)

    def test_every_wide_shape_at_once(self):
        self._both(pe32=True, heap_sizes=0x07, filler=True, typerefs=16384, memberrefs=8192,
                   methods=65536, params=65536)

    def test_a_native_dll_names_the_reason(self):
        self.assertEqual(self._read(_managed_dll(DEBUG_MODES, native=True)),
                         (None, "no CLI header (a native DLL)"))
        self.assertEqual(self._read(_managed_dll(DEBUG_MODES, native=True, pe32=True)),
                         (None, "no CLI header (a native DLL)"))

    def test_bytes_without_metadata_read_as_unknown(self):
        # The --require-build tests' stand-in DLLs are exactly this shape.
        self.assertEqual(self._read(_dll_bytes(f"{STAMP}+{SHA}"))[0], None)
        self.assertEqual(self._read(b"\0" * 300), (None, "no MZ header"))

    def test_truncated_metadata_reads_as_unknown_with_its_reason(self):
        data = _managed_dll(DEBUG_MODES)
        disabled, why = self._read(data[:0x200 + 200])
        self.assertIsNone(disabled)
        self.assertTrue(why, "a None verdict always says why")

    # A DebuggableAttribute that is not laid out as ECMA-335 II.23.3 lays out the constructor it
    # names is malformed, and the reader never turns a malformed attribute into a verdict: it
    # says unknown and why (the Codex review of 380a8a59, P2). The layout is a prolog 01 00, the
    # constructor's arguments, then NumNamed (00 00: the attribute has no settable member).
    LAYOUTS = ((DEBUG_MODES, MODES_CTOR), (RELEASE_MODES, MODES_CTOR),
               (BOOLS_DISABLED, BOOLS_CTOR), (BOOLS_ENABLED, BOOLS_CTOR))

    def _assert_unexpected(self, value, ctor_sig):
        self.assertEqual(self._read(_managed_dll(value, ctor_sig=ctor_sig)),
                         (None, f"unexpected DebuggableAttribute value {value.hex()}"))

    def test_the_codex_counterexample_reads_as_unknown(self):
        # Eight bytes with a zero prolog: the length alone used to read it as Release modes, ON.
        self._assert_unexpected(b"\x00\x00\x02\x00\x00\x00\x00\x00", MODES_CTOR)

    def test_every_byte_of_the_prolog_and_the_named_argument_count_is_checked(self):
        # One byte at a time, in both layouts: a prolog or a count that names an argument the
        # blob does not hold is a verdict nobody can trust.
        for base, sig in self.LAYOUTS:
            for i in (0, 1, len(base) - 2, len(base) - 1):
                value = base[:i] + bytes([base[i] ^ 0x01]) + base[i + 1:]
                with self.subTest(base=base.hex(), flipped_byte=i):
                    self._assert_unexpected(value, sig)

    def test_a_value_of_the_wrong_length_reads_as_unknown(self):
        # Shorter or longer than the constructor's layout (a longer one carries named arguments
        # the attribute cannot have), each cut from or padded past a valid value.
        for base, sig in self.LAYOUTS:
            for n in (0, 1, 2, 3, len(base) - 1, len(base) + 1, len(base) + 2, len(base) + 8):
                value = (base + b"\0" * 16)[:n]
                with self.subTest(base=base.hex(), length=n):
                    self._assert_unexpected(value, sig)

    def test_the_constructor_decides_the_layout_not_the_length(self):
        # The eight-byte layout under the two-Boolean constructor, and the six-byte one under the
        # DebuggingModes constructor, are each the other constructor's value.
        for value in (DEBUG_MODES, RELEASE_MODES):
            self._assert_unexpected(value, BOOLS_CTOR)
        for value in (BOOLS_DISABLED, BOOLS_ENABLED):
            self._assert_unexpected(value, MODES_CTOR)

    def test_a_constructor_the_attribute_does_not_have_reads_as_unknown(self):
        for label, sig in (
            ("no parameters", b"\x20\x00\x01"),
            ("no HASTHIS", b"\x00\x01\x01\x11\x09"),
            ("an int32 for the enum", b"\x20\x01\x01\x08"),
            ("a string", b"\x20\x01\x01\x0e"),
            ("a return value", b"\x20\x01\x08\x11\x09"),
            ("an enum token cut short", b"\x20\x01\x01\x11"),
            ("a two-byte token cut short", b"\x20\x01\x01\x11\x80"),
            ("a four-byte token cut to two bytes", b"\x20\x01\x01\x11\xc0\x00"),
            ("a four-byte token cut short", b"\x20\x01\x01\x11\xc0\x00\x00"),
            ("an invalid token lead byte", b"\x20\x01\x01\x11\xe0\x00\x00\x00"),
            ("bytes after the token", b"\x20\x01\x01\x11\x09\x00"),
            ("bytes after the two Booleans", BOOLS_CTOR + b"\x00"),
            ("three Booleans", b"\x20\x03\x01\x02\x02\x02"),
            ("a Boolean and an int32", b"\x20\x02\x01\x02\x08"),
            ("an empty signature", b""),
        ):
            for value in (DEBUG_MODES, BOOLS_DISABLED):
                with self.subTest(label=label, value=value.hex()):
                    self.assertEqual(self._read(_managed_dll(value, ctor_sig=sig)),
                                     (None, f"unsupported DebuggableAttribute constructor "
                                            f"{sig.hex()}"))

    def test_every_byte_of_a_constructor_signature_is_checked(self):
        # One byte at a time, its low bit flipped: the HASTHIS byte, the parameter count, the
        # return type and each parameter type (the enum's token is any compressed integer, so it
        # is not a position to flip).
        for sig, value, positions in ((BOOLS_CTOR, BOOLS_DISABLED, range(5)),
                                      (MODES_CTOR, DEBUG_MODES, range(4))):
            for i in positions:
                bad = sig[:i] + bytes([sig[i] ^ 0x01]) + sig[i + 1:]
                with self.subTest(signature=sig.hex(), flipped_byte=i):
                    self.assertEqual(self._read(_managed_dll(value, ctor_sig=bad)),
                                     (None, f"unsupported DebuggableAttribute constructor "
                                            f"{bad.hex()}"))

    def test_the_enum_token_may_take_every_width_a_compressed_integer_has(self):
        # ECMA-335 II.23.2: one, two or four bytes by the first byte's high bits, at each end of
        # each range. The survey of real DLLs found the first two; the grammar allows the third.
        for token in (b"\x00", b"\x09", b"\x0a", b"\x7f", b"\x80\x00", b"\x80\x09", b"\x81\x0a",
                      b"\xbf\xff", b"\xc0\x00\x00\x00", b"\xc0\x00\x00\x09", b"\xc0\x0a\x0a\x0a",
                      b"\xdf\xff\xff\xff"):
            sig = b"\x20\x01\x01\x11" + token
            with self.subTest(token=token.hex()):
                self.assertEqual(self._read(_managed_dll(DEBUG_MODES, ctor_sig=sig)), (True, ""))
                self.assertEqual(self._read(_managed_dll(RELEASE_MODES, ctor_sig=sig)),
                                 (False, ""))

    def test_a_missing_file_reads_as_unknown(self):
        disabled, why = pr.jit_optimization(Path(tempfile.gettempdir()) / "no-such-035.dll")
        self.assertIsNone(disabled)
        self.assertIn("cannot read", why)


class TestJitReportCli(unittest.TestCase):
    def _modules(self, td, data, rel="TAOM/bin/Win64_Shipping_Client/TAOM.dll"):
        src = Path(td) / "Modules"
        dll = src / rel
        dll.parent.mkdir(parents=True, exist_ok=True)
        dll.write_bytes(data)
        return src

    def _dry(self, src, td, modules=("TAOM",)):
        return _run("--source", str(src), "--dest", str(Path(td) / "out"), "--modules", *modules,
                    "--dry-run")

    def test_a_debug_build_is_reported_and_not_refused(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._dry(self._modules(td, _managed_dll(DEBUG_MODES)), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization OFF", r.stdout)

    def test_every_copy_gets_its_own_line(self):
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td, _managed_dll(DEBUG_MODES))
            self._modules(td, _managed_dll(RELEASE_MODES), "TAOM/bin/Win64_Shipping_Server/TAOM.dll")
            r = self._dry(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization OFF", r.stdout)
            self.assertIn("TAOM/bin/Win64_Shipping_Server/TAOM.dll: JIT optimization ON", r.stdout)

    def test_an_assembly_without_the_attribute_says_so_on_its_line(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._dry(self._modules(td, _managed_dll(None)), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("  TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization ON "
                          "(no DebuggableAttribute on the assembly)\n", r.stdout)

    def test_an_attribute_that_keeps_optimization_on_is_a_bare_on(self):
        # Only the absent attribute earns the parenthetical; a Release-form attribute is a bare ON.
        with tempfile.TemporaryDirectory() as td:
            r = self._dry(self._modules(td, _managed_dll(RELEASE_MODES)), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("  TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization ON",
                          r.stdout.splitlines())

    def test_disable_optimizations_without_default_is_a_bare_on(self):
        # The attribute asks for DisableOptimizations alone (0x100), which leaves the JIT
        # optimizing, so the line must not say OFF.
        with tempfile.TemporaryDirectory() as td:
            r = self._dry(self._modules(td, _managed_dll(b"\x01\x00\x00\x01\x00\x00\x00\x00")), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("  TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization ON",
                          r.stdout.splitlines())

    def test_an_unreadable_dll_is_reported_as_unknown_with_its_reason(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._dry(self._modules(td, b"\0" * 300), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization unknown "
                          "(no MZ header)", r.stdout)

    def test_an_attribute_that_does_not_parse_is_unknown_with_its_reason_on_its_line(self):
        # Neither a Release ON nor a Debug OFF: the copy's own line says the attribute cannot be
        # trusted and why, and the run is not refused.
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td, _managed_dll(b"\x00\x00\x02\x00\x00\x00\x00\x00"))
            self._modules(td, _managed_dll(DEBUG_MODES, ctor_sig=b"\x20\x01\x01\x08"),
                          "TAOM/bin/Win64_Shipping_Server/TAOM.dll")
            r = self._dry(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("  TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization unknown "
                          "(unexpected DebuggableAttribute value 0000020000000000)\n", r.stdout)
            self.assertIn("  TAOM/bin/Win64_Shipping_Server/TAOM.dll: JIT optimization unknown "
                          "(unsupported DebuggableAttribute constructor 20010108)\n", r.stdout)

    def test_a_module_without_its_dll_says_so(self):
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "Modules"
            _write(src, "TAOM/SubModule.xml", 10)
            r = self._dry(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM: no bin/<platform>/TAOM.dll copy ships", r.stdout)

    def test_a_run_without_taom_assemblies_says_so(self):
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "Modules"
            _write(src, "TAOM_Map/SubModule.xml", 10)
            r = self._dry(src, td, modules=("TAOM_Map",))
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("no planned module ships TAOM.dll or TAOM.Dependencies.dll", r.stdout)


if __name__ == "__main__":
    unittest.main(verbosity=2)
