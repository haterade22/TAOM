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
  - *.xml.bak excluded anywhere (glob-loaded ModuleData hazard)
  - bin/*.pdb|exp|lib excluded, bin/*.dll copied
  - runtime state files (diag.log, last-good-modlist.txt) excluded; licences copied
  - unrecognised top-level entries are UNKNOWN -> reported, never copied
  - manifest byte arithmetic; --dry-run writes nothing; exit codes
"""
import json
import os
import subprocess
import sys
import tempfile
import unittest
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
        self.assertEqual(rule("ModuleData/action_sets.xml.bak"), "XML_BAK")

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

            self.assertEqual(plan.total_bytes, 100 + 1000 + 5000 + 0 + 8000 + 200 + 250 + 300 + 900 + 400 + 77)
            # copied: SubModule 100 + pack0 1000 + troops 200 + dll 300 + em 400
            self.assertEqual(plan.copy_bytes, 2000)
            # excluded: rdc 5000 + rtemp 0 + AssetSources 8000 + bak 250 + pdb 900
            self.assertEqual(plan.exclude_bytes, 14150)
            self.assertEqual(plan.unknown_bytes, 77)
            self.assertEqual(plan.total_bytes, plan.copy_bytes + plan.exclude_bytes + plan.unknown_bytes)

    def test_keep_rdc_moves_bytes_from_excluded_to_copied(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            plan = pr.plan_module(src / "TAOM", keep_rdc=True)
            self.assertEqual(plan.copy_bytes, 2000 + 5000)
            self.assertEqual(plan.exclude_bytes, 14150 - 5000)  # .rtemp still excluded

    def test_by_rule_totals(self):
        with tempfile.TemporaryDirectory() as td:
            src = _fixture(Path(td))
            plan = pr.plan_module(src / "TAOM")
            self.assertEqual(plan.by_rule["RUNTIME_DATA_CACHE"], 5000)
            self.assertEqual(plan.by_rule["ASSET_SOURCES"], 8000)
            self.assertEqual(plan.by_rule["XML_BAK"], 250)
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
            self.assertTrue((dest / "TAOM/EmAssetPackages/em.tpac").exists())
            for gone in (
                "TAOM/RuntimeDataCache",
                "TAOM/AssetSources",
                "TAOM/ModuleData/troops.xml.bak",
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
            self.assertEqual(data["modules"][0]["copy_bytes"], 2000)
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


if __name__ == "__main__":
    unittest.main(verbosity=2)
