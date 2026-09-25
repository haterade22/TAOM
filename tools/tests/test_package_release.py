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
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path

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
        self.assertEqual(act("bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll"), pr.COPY)

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
        parent = subprocess.run(["git", "rev-parse", f"{first[0]}^"], cwd=root,
                                capture_output=True, text=True).stdout.strip()
        if not parent:
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


if __name__ == "__main__":
    unittest.main(verbosity=2)
