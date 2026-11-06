"""Tests for tools/check_engine_member_refs.ps1 (needs pwsh 7; the real-engine controls also need the install).

Run:  python -m unittest tools.tests.test_check_engine_member_refs -v

The script resolves every engine MemberReference and TypeReference of a DLL against the engine assemblies.
Most tests build tiny 'engine' and 'mod' assemblies with the Roslyn that pwsh 7 bundles
(fixtures/build_assemblies.ps1), so they need no game install:
  - a typo'd or empty engine or baseline directory, a non-.NET DLL, a same-build baseline and a reference into an
    engine assembly nobody holds are bad input (exit 2), never a silent "checked 0"
  - a type that moved to another assembly without a forwarder is MISSING
  - a missing type reference and a missing member of a generic type are reported
  - a baseline that lacks the same member marks the miss PRE-EXISTING; one that lacks the assembly keeps it NEW
  - -Dll takes wildcards
The controls against the real install are skipped when the build output or the packages are absent.
"""
import json
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "tools" / "check_engine_member_refs.ps1"
BUILDER = Path(__file__).resolve().parent / "fixtures" / "build_assemblies.ps1"
PWSH = shutil.which("pwsh")
GAME = Path(os.environ.get("BANNERLORD_GAME_DIR") or r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
TAOM_DLL = REPO / "TAOM.Tests" / "bin" / "Debug" / "net472" / "TAOM.dll"
OLD_BUILD = "1.5.3.122374-beta"
OLD_PACKAGES = ("core", "native", "sandbox", "storymode", "custombattle")

LIB = 'public static class BuildInfo { public const string GameVersion = "%s"; }'
ALPHA_OLD = """
namespace TaleWorlds.Alpha {
    public static class Plain { public static int Present() { return 1; } public static int Removed() { return 2; } }
    public static class Stay { public static int Here() { return 3; } }
    public static class Mover { public static int Go() { return 4; } }
    public class Gen<T> { public void Add(T x) { } public void GoneG(T x) { } }
    public class Vanished { }
}"""
ALPHA_NEW = """
namespace TaleWorlds.Alpha {
    public static class Plain { public static int Present() { return 1; } }
    public static class Stay { public static int Here() { return 3; } }
    public class Gen<T> { public void Add(T x) { } }
}"""
BETA = "namespace TaleWorlds.Alpha { public static class Mover { public static int Go() { return 4; } } }"
GAMMA = "namespace TaleWorlds.Gamma { public static class X { public static void Y() { } } }"
MOD_MAIN = """
public static class ModMain {
    public static void Run() {
        TaleWorlds.Alpha.Plain.Present();
        TaleWorlds.Alpha.Plain.Removed();
        var g = new TaleWorlds.Alpha.Gen<int>();
        g.Add(1);
        g.GoneG(1);
        TaleWorlds.Alpha.Mover.Go();
        TaleWorlds.Alpha.Stay.Here();
        var t = typeof(TaleWorlds.Alpha.Vanished);
    }
}"""
MOD_GAMMA = "public static class ModGamma { public static void R() { TaleWorlds.Gamma.X.Y(); } }"
MOD_PLAIN = "public static class ModPlain { public static int R() { return 1; } }"


def run(*args):
    cmd = [PWSH, "-NoProfile", "-File", str(SCRIPT), *map(str, args)]
    return subprocess.run(cmd, capture_output=True, text=True, timeout=600)


def old_ref_dirs():
    root = Path(os.environ.get("NUGET_PACKAGES") or Path(os.environ["USERPROFILE"]) / ".nuget" / "packages")
    dirs = [root / f"bannerlord.referenceassemblies.{p}" / OLD_BUILD / "ref" / "net472" for p in OLD_PACKAGES]
    return dirs if all(d.is_dir() for d in dirs) else None


def checked_count(stdout):
    return [int(n) for n in re.findall(r"checked (\d+) distinct", stdout)]


@unittest.skipIf(PWSH is None, "pwsh is not installed")
class FixtureTests(unittest.TestCase):
    """Engine 'new' (v2.0.0) dropped Plain.Removed, Gen.GoneG and Vanished, and moved Mover to Beta."""

    @classmethod
    def setUpClass(cls):
        tmp = tempfile.TemporaryDirectory()
        cls.addClassCleanup(tmp.cleanup)
        root = Path(tmp.name)
        cls.root = root
        src = root / "src"
        src.mkdir()
        sources = {"lib1": LIB % "v1.0.0", "lib2": LIB % "v2.0.0", "alpha_old": ALPHA_OLD, "alpha_new": ALPHA_NEW,
                   "beta": BETA, "gamma": GAMMA, "main": MOD_MAIN, "modg": MOD_GAMMA, "plain": MOD_PLAIN}
        for name, text in sources.items():
            (src / f"{name}.cs").write_text(text, encoding="utf-8")

        def job(name, out, source, refs=()):
            return {"name": name, "out": str(root / out), "src": str(src / f"{source}.cs"), "refs": [str(root / r) for r in refs]}

        jobs = [
            job("TaleWorlds.Alpha", "old/TaleWorlds.Alpha.dll", "alpha_old"),
            job("TaleWorlds.Gamma", "gamma/TaleWorlds.Gamma.dll", "gamma"),
            job("TaleWorlds.Library", "new/TaleWorlds.Library.dll", "lib2"),
            job("TaleWorlds.Alpha", "new/TaleWorlds.Alpha.dll", "alpha_new"),
            job("TaleWorlds.Beta", "new/TaleWorlds.Beta.dll", "beta"),
            job("TaleWorlds.Library", "mid/TaleWorlds.Library.dll", "lib1"),
            job("TaleWorlds.Alpha", "mid/TaleWorlds.Alpha.dll", "alpha_new"),
            job("TaleWorlds.Library", "libonly/TaleWorlds.Library.dll", "lib1"),
            job("ModMain", "mod/ModMain.dll", "main", ["old/TaleWorlds.Alpha.dll"]),
            job("ModPlain", "mod/ModPlain.dll", "plain"),
            job("ModGamma", "modg/ModGamma.dll", "modg", ["gamma/TaleWorlds.Gamma.dll"]),
        ]
        spec = root / "jobs.json"
        spec.write_text(json.dumps(jobs), encoding="utf-8")
        r = subprocess.run([PWSH, "-NoProfile", "-File", str(BUILDER), str(spec)], capture_output=True, text=True, timeout=600)
        if r.returncode != 0:
            raise RuntimeError("fixture build failed: " + r.stdout + r.stderr)

    def p(self, rel):
        return self.root / rel

    def test_miss_with_the_engine_only_is_new_and_exits_1(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"))
        self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
        self.assertIn("Plain::Removed", r.stdout)

    def test_typo_baseline_directory_exits_2(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"), "-Baseline", self.p("no_such_dir"))
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_one_typo_among_valid_baselines_exits_2(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"),
                "-Baseline", f"{self.p('mid')},{self.p('no_such_dir')}")
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_baseline_missing_the_assembly_keeps_the_miss_new(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"), "-Baseline", self.p("libonly"))
        self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
        self.assertIn("MISSING NEW [TaleWorlds.Alpha]", r.stdout)
        self.assertNotIn("PRE-EXISTING", r.stdout)

    def test_baseline_of_another_version_lacking_the_same_members_marks_them_pre_existing(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"), "-Baseline", self.p("mid"))
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn("MISSING PRE-EXISTING [TaleWorlds.Alpha] TaleWorlds.Alpha.Plain::Removed", r.stdout)
        self.assertNotIn("MISSING NEW", r.stdout)
        self.assertIn("(0 new)", r.stdout)
        self.assertIn("engine v2.0.0, baseline v1.0.0", r.stdout)

    def test_same_version_baseline_exits_2(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"), "-Baseline", self.p("new"))
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)
        self.assertIn("same", (r.stdout + r.stderr).lower())

    def test_typo_engine_directory_exits_2(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("no_such_dir"))
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_one_typo_among_valid_engine_directories_exits_2(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", f"{self.p('new')},{self.p('no_such_dir')}")
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_engine_directory_without_engine_assemblies_exits_2(self):
        empty = self.root / "empty"
        empty.mkdir(exist_ok=True)
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", empty)
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_reference_into_an_engine_assembly_no_directory_holds_is_unchecked_exit_2(self):
        r = run("-Dll", self.p("modg/ModGamma.dll"), "-EngineDir", self.p("new"))
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)
        self.assertIn("UNCHECKED [TaleWorlds.Gamma]", r.stdout)

    def test_dll_that_is_not_a_net_assembly_exits_2(self):
        with tempfile.TemporaryDirectory() as d:
            fake = Path(d) / "native.dll"
            fake.write_bytes(b"MZ not a managed assembly" * 20)
            r = run("-Dll", fake, "-EngineDir", self.p("new"))
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_type_moved_to_another_assembly_without_a_forwarder_is_missing(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"))
        self.assertRegex(r.stdout, r"MISSING NEW \[TaleWorlds\.Alpha\] TaleWorlds\.Alpha\.Mover::Go")

    def test_missing_type_reference_is_reported(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"))
        self.assertIn("MISSING NEW type [TaleWorlds.Alpha] TaleWorlds.Alpha.Vanished", r.stdout)

    def test_member_of_a_generic_type_is_resolved_and_a_missing_one_reported(self):
        r = run("-Dll", self.p("mod/ModMain.dll"), "-EngineDir", self.p("new"))
        self.assertIn("Gen`1::GoneG", r.stdout)
        self.assertNotIn("Gen`1::Add", r.stdout)
        self.assertNotIn("Gen`1::.ctor", r.stdout)

    def test_wildcard_dll_expands_and_skips_dlls_without_engine_refs(self):
        r = run("-Dll", self.p("mod") / "*.dll", "-EngineDir", self.p("new"))
        self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
        self.assertIn("ModMain.dll", r.stdout)
        self.assertNotIn("ModPlain.dll", r.stdout)

    def test_wildcard_matching_nothing_exits_2(self):
        r = run("-Dll", self.p("mod") / "*.nothing", "-EngineDir", self.p("new"))
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_missing_dll_exits_2(self):
        r = run("-Dll", REPO / "no_such_file.dll", "-EngineDir", self.p("new"))
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)


@unittest.skipIf(PWSH is None, "pwsh is not installed")
@unittest.skipUnless((GAME / "bin" / "Win64_Shipping_Client").is_dir(), "Bannerlord install absent")
class RealEngineTests(unittest.TestCase):
    def setUp(self):
        if not TAOM_DLL.is_file():
            self.skipTest("TAOM.Tests build output absent")

    def test_fresh_build_resolves_against_installed_engine(self):
        r = run("-Dll", TAOM_DLL, "-GameDir", GAME)
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn(" 0 unresolved", r.stdout)
        self.assertGreater(max(checked_count(r.stdout)), 1000, r.stdout)

    def test_old_engine_reports_missing_members(self):
        dirs = old_ref_dirs()
        if dirs is None:
            self.skipTest(f"cached v{OLD_BUILD} reference assemblies absent")
        r = run("-Dll", TAOM_DLL, "-EngineDir", ",".join(map(str, dirs)))
        self.assertIn("MISSING", r.stdout)
        self.assertEqual(r.returncode, 1, r.stdout + r.stderr)


if __name__ == "__main__":
    unittest.main()
