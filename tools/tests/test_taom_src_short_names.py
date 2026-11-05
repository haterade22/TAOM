#!/usr/bin/env python
"""tools/taom-src.ps1 path <short name>: AGENTS.md and the rules write the short form.

Run:  python -m unittest tools.tests.test_taom_src_short_names

Most of these run the real script against the installed game, and they skip without pwsh, ilspycmd or
the install. A short name that holds one engine type resolves, an ambiguous one names its candidates,
and a name no engine type holds fails fast instead of probing every DLL for minutes. The index row's
DLL is only a hint: a dead row falls through to the probes a full name takes, and a miss rebuilds the
index once.
"""
import os
import re
import shutil
import subprocess
import tempfile
import time
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
SCRIPT = REPO / "tools" / "taom-src.ps1"
A_SMALL_TYPE = "DefaultPartyWageModel"
ITS_FILE = "TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.cs"


def installed():
    game = os.environ.get("BANNERLORD_GAME_DIR", "")
    return bool(shutil.which("pwsh") and shutil.which("ilspycmd") and game and Path(game).is_dir())


def run(*args, profile=None, game=None):
    env = dict(os.environ)
    if profile:
        env["USERPROFILE"] = str(profile)
    if game:
        env["BANNERLORD_OVERRIDE_DIR"] = str(game)
    return subprocess.run(["pwsh", "-NoProfile", "-File", str(SCRIPT), "path", *args], capture_output=True,
                          text=True, encoding="utf-8", errors="replace", timeout=280, env=env)


def version_dir(result):
    """The active game version's cache folder, from the line the script prints to stderr."""
    found = re.search(r"detected version \S+ -> cache (.+)", result.stderr)
    return Path(found.group(1).strip()) if found else None


class ModuleOrder(unittest.TestCase):
    def test_the_game_module_list_matches_the_body_diff_gate(self):
        """A mod's copy of an engine assembly goes stale. Both tools must agree on which modules are the game's."""
        ps1 = re.search(r"^\$VanillaModules = (.+)$", SCRIPT.read_text(encoding="utf-8"), flags=re.M)
        self.assertIsNotNone(ps1, "taom-src.ps1 no longer declares $VanillaModules")
        diff = (REPO / "tools" / "engine_body_diff.py").read_text(encoding="utf-8")
        py = re.search(r"VANILLA_MODULES = frozenset\(\{(.+?)\}\)", diff, flags=re.S)
        self.assertIsNotNone(py, "engine_body_diff.py no longer declares VANILLA_MODULES")
        self.assertEqual(set(re.findall(r"'([^']+)'", ps1.group(1))), set(re.findall(r'"([^"]+)"', py.group(1))))


@unittest.skipUnless(shutil.which("pwsh"), "needs pwsh")
class AnUpdateThatKeepsTheVersion(unittest.TestCase):
    """Steam can replace every DLL and leave Version.xml alone. The old build's sources must not answer for the new one."""

    def test_new_dlls_get_a_new_cache_folder_and_the_old_folder_stays(self):
        root = Path(tempfile.mkdtemp(prefix="taom-src-test-"))
        self.addCleanup(shutil.rmtree, root, True)
        game = root / "game"
        bin_dir = game / "bin" / "Win64_Shipping_Client"
        bin_dir.mkdir(parents=True)
        (bin_dir / "Version.xml").write_text('<Version><Singleplayer Value="v9.9.9"/></Version>', encoding="utf-8")
        library = bin_dir / "TaleWorlds.Library.dll"
        library.write_bytes(b"MZ")

        os.utime(library, (1_700_000_000, 1_700_000_000))
        before = version_dir(run("NoEngineTypeIsNamedThis", profile=root, game=game))
        os.utime(library, (1_700_100_000, 1_700_100_000))
        after = version_dir(run("NoEngineTypeIsNamedThis", profile=root, game=game))

        self.assertIsNotNone(before, "the script no longer prints its cache folder")
        self.assertIsNotNone(after, "the script no longer prints its cache folder")
        self.assertTrue(before.name.startswith("v9.9.9"), before)
        self.assertNotEqual(before, after, "the same version with new DLLs must not reuse the old build's sources")
        self.assertTrue(before.is_dir(), "another session may be reading the old folder, so it stays")


@unittest.skipUnless(installed(), "needs pwsh, ilspycmd and BANNERLORD_GAME_DIR")
class ShortNames(unittest.TestCase):
    def test_a_unique_short_name_resolves_to_its_full_name(self):
        result = run(A_SMALL_TYPE)
        self.assertEqual(result.returncode, 0, result.stderr)
        path = result.stdout.strip().splitlines()[-1]
        self.assertTrue(path.endswith(ITS_FILE), path)
        self.assertTrue(Path(path).is_file())

    def test_a_type_in_a_module_dll_resolves(self):
        """AgentVisuals ships in Modules/Native and not in bin, which the namespace guess misses."""
        result = run("AgentVisuals")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("AgentVisuals.cs", result.stdout)

    def test_a_type_with_no_namespace_resolves(self):
        """TaleWorlds.Library holds a global BuildInfo, which has no longer name to pass."""
        result = run("BuildInfo")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(result.stdout.strip().splitlines()[-1].endswith("BuildInfo.cs"), result.stdout)

    def test_a_mods_copy_of_an_engine_assembly_comes_last(self):
        """Coop ships about 40 engine DLLs. Its row must not be the one a short name reads first."""
        directory = version_dir(run(A_SMALL_TYPE))
        self.assertIsNotNone(directory, "the script no longer prints its cache folder")
        first = {}
        rows = (directory / "engine-types.tsv").read_text(encoding="utf-8-sig").splitlines()
        for line in rows:
            short, _, dll = line.split("\t")
            first.setdefault(short, dll)
        coop = {line.split("\t")[0] for line in rows if "\\Modules\\Coop\\" in line}
        if not coop:
            self.skipTest("no module ships engine copies here")
        self.assertEqual(sorted(s for s in coop if "\\Modules\\Coop\\" in first[s] and self._held_elsewhere(s, rows)), [])

    @staticmethod
    def _held_elsewhere(short, rows):
        prefix = short + "\t"
        return any(line.startswith(prefix) and "\\Modules\\Coop\\" not in line for line in rows)

    def test_an_unknown_name_fails_fast_and_says_so(self):
        start = time.time()
        result = run("NoEngineTypeIsNamedThis")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("No top-level engine type is named", result.stdout + result.stderr)
        self.assertLess(time.time() - start, 90)

    def test_an_ambiguous_name_lists_its_candidates(self):
        directory = version_dir(run(A_SMALL_TYPE))
        self.assertIsNotNone(directory, "the script no longer prints its cache folder")
        seen = {}
        for line in (directory / "engine-types.tsv").read_text(encoding="utf-8-sig").splitlines():
            short, full = line.split("\t")[:2]
            seen.setdefault(short, set()).add(full)
        ambiguous = next((s for s, names in seen.items() if len(names) > 1 and s.isidentifier()), None)
        if ambiguous is None:
            self.skipTest("no ambiguous engine type name on this version")
        result = run(ambiguous)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("ambiguous", result.stdout + result.stderr)
        for full in seen[ambiguous]:
            self.assertIn(full, result.stdout + result.stderr)


@unittest.skipUnless(installed(), "needs pwsh, ilspycmd and BANNERLORD_GAME_DIR")
class AStaleIndex(unittest.TestCase):
    """Each test holds its own cache under a temporary profile, so the real cache stays as it is."""

    def setUp(self):
        self.profile = Path(tempfile.mkdtemp(prefix="taom-src-test-"))
        self.addCleanup(shutil.rmtree, self.profile, True)
        self.directory = version_dir(run("NoEngineTypeIsNamedThis", profile=self.profile))
        self.assertIsNotNone(self.directory, "the script no longer prints its cache folder")
        self.assertTrue(str(self.directory).startswith(str(self.profile)), self.directory)
        self.index = self.directory / "engine-types.tsv"
        self.assertTrue(self.index.is_file(), "no index was built")

    def assert_it_resolves(self, result):
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(result.stdout.strip().splitlines()[-1].endswith(ITS_FILE), result.stdout)

    def test_a_row_whose_dll_is_gone_falls_through_to_the_probes(self):
        full = ITS_FILE[:-3]
        self.index.write_text(f"{A_SMALL_TYPE}\t{full}\tC:\\no\\such\\folder\\Gone.dll\n", encoding="utf-8")
        self.assert_it_resolves(run(A_SMALL_TYPE, profile=self.profile))

    def test_a_row_that_names_the_wrong_dll_falls_through_to_the_probes(self):
        """A type that moved between assemblies under one version string."""
        library = next(Path(os.environ["BANNERLORD_GAME_DIR"], "bin", "Win64_Shipping_Client").glob("TaleWorlds.Library.dll"))
        self.index.write_text(f"{A_SMALL_TYPE}\t{ITS_FILE[:-3]}\t{library}\n", encoding="utf-8")
        self.assert_it_resolves(run(A_SMALL_TYPE, profile=self.profile))

    def test_a_cut_index_heals_on_a_miss(self):
        self.index.write_text("Other\tSome.Other\tC:\\Other.dll\n", encoding="utf-8")
        self.assert_it_resolves(run(A_SMALL_TYPE, profile=self.profile))
        self.assertGreater(len(self.index.read_text(encoding="utf-8-sig").splitlines()), 1000)

    def test_the_type_to_dll_index_is_per_version(self):
        self.assert_it_resolves(run(A_SMALL_TYPE, profile=self.profile))
        self.assertTrue((self.directory / "dll-index.json").is_file())
        self.assertFalse((self.profile / ".taom-src" / "dll-index.json").exists())

    def test_a_module_type_decompiles_against_the_engine_references(self):
        """Without the game's bin as a reference path ilspycmd leaves //IL_ lines and bare casts."""
        result = run("TaleWorlds.MountAndBlade.View.MissionViews.Order.OrderTroopPlacer", profile=self.profile)
        self.assertEqual(result.returncode, 0, result.stderr)
        source = Path(result.stdout.strip().splitlines()[-1]).read_text(encoding="utf-8-sig")
        self.assertIn("OrderTroopPlacer", source)
        self.assertNotIn("//IL_", source)


if __name__ == "__main__":
    unittest.main()
