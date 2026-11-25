"""gen_dire_wolf_anim_clips.ps1: Get-ClipProblem, the check -Apply and -Verify share, and the start guards (#1081).

The generator needs TpacTool, the Kit's loader and the install, so these tests lift Get-ClipProblem out of the script with
the PowerShell parser and run it on plain tables, the way test_gen_animalia_anim_clips.py does. Windows PowerShell 5.1
first: the script's runtime.
"""
import json
import os
import re
import shutil
import subprocess
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gen_dire_wolf_anim_clips.ps1")
SHELL = shutil.which("powershell") or shutil.which("pwsh")

HARNESS = r"""
param([string]$Script)
$ErrorActionPreference = 'Stop'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)
$fn = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true) |
  Where-Object { 'Get-ClipProblem', 'Resolve-Master', 'Add-MasterName', 'Get-MasterFrameProblem', 'Get-Unmapped' -contains $_.Name })
foreach ($f in $fn) { . ([scriptblock]::Create($f.Extent.Text)) }
function W($extra = @{}) {
  $t = @{ Name = 'c'; Animation = 'g1'; Flags = 'a,b'; Source1 = 1.0; Source2 = 9.0; Duration = 0.5; Loop = 2.4 }
  foreach ($k in $extra.Keys) { $t[$k] = $extra[$k] }
  $t
}
$masters = @{ g1 = $true }
function P($got) { @(Get-ClipProblem $got (W) $masters) -join '; ' }
[ordered]@{
  same       = P (W)
  rounding   = P (W @{ Loop = 2.4001 })
  none       = P $null
  name       = P (W @{ Name = 'other' })
  master     = P (W @{ Animation = 'g2' })
  flags      = P (W @{ Flags = 'a' })
  source2    = P (W @{ Source2 = 10.0 })
  duration   = P (W @{ Duration = 0.75 })
  loop       = P (W @{ Loop = 3.0 })
  loop_gone  = P (W @{ Loop = $null })
  no_master  = @(Get-ClipProblem (W) (W) @{}) -join '; '
  owned      = @(Get-ClipProblem (W @{ Source1 = 0.0; Source2 = 10.0; Duration = 0.75; Loop = 3.0 }) (W) $masters $true) -join '; '
  owned_name = @(Get-ClipProblem (W @{ Name = 'other'; Flags = 'a'; Animation = 'g2'; Duration = 0.75 }) (W) $masters $true) -join '; '
  remapped   = "$(Resolve-Master 'pg1' @{ pg1 = 'adod_wolf_walk' } @{ adod_wolf_walk = 'kg1' } 1.73)"
  same_guid  = "$(Resolve-Master 'pg1' @{ pg1 = 'adod_wolf_walk' } @{ adod_wolf_walk = 'pg1' } 1.0)"
  pack_size  = "$(Resolve-Master 'pg1' @{ pg1 = 'adod_wolf_walk' } @{ adod_wolf_walk = 'pg1' } 1.73)"
  no_name    = "$(Resolve-Master 'pg9' @{ pg1 = 'adod_wolf_walk' } @{ adod_wolf_walk = 'kg1' } 1.73)"
  not_in_out = "$(Resolve-Master 'pg1' @{ pg1 = 'adod_wolf_walk' } @{ adod_wolf_trot = 'kg2' } 1.73)"
  name_new   = & { $m = @{}; "$(Add-MasterName $m 'walk' 'g1' 'a.tpac')|$($m['walk'])" }
  name_again = & { $m = @{}; $null = Add-MasterName $m 'walk' 'g1' 'a.tpac'; "$(Add-MasterName $m 'walk' 'g1' 'a.tpac')|$($m['walk'])" }
  name_twice = & { $m = @{}; $null = Add-MasterName $m 'walk' 'g1' 'a.tpac'; "$(Add-MasterName $m 'walk' 'g2' 'b.tpac')" }
  frames_ok  = "$(Get-MasterFrameProblem 'walk' 33 @{ walk = 33 })"
  frames_bad = "$(Get-MasterFrameProblem 'walk' 32 @{ walk = 33 })"
  frames_new = "$(Get-MasterFrameProblem 'extra' 10 @{ walk = 33 })"
  unmapped   = @(Get-Unmapped @('warg_trot', 'warg_walk_stand', 'warg_new', 'horse_walk') ([ordered]@{ trot = 'trot' }) @('walk_stand')) -join ','
  all_mapped = @(Get-Unmapped @('warg_trot', 'warg_walk_stand') ([ordered]@{ trot = 'trot' }) @('walk_stand')).Count
} | ConvertTo-Json -Compress
"""


def _run_script(*args):
    return subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", SCRIPT, *args],
                          capture_output=True, text=True, timeout=180)


@unittest.skipUnless(SHELL, "no PowerShell on PATH: these tests need powershell.exe or pwsh")
class ClipProblemTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with tempfile.TemporaryDirectory() as d:
            harness = os.path.join(d, "harness.ps1")
            with open(harness, "w", encoding="ascii") as f:
                f.write(HARNESS)
            p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harness,
                                "-Script", SCRIPT], capture_output=True, text=True, timeout=180)
        if p.returncode != 0:
            raise AssertionError(p.stdout + p.stderr)
        cls.got = json.loads(p.stdout.strip().splitlines()[-1])

    def test_the_planned_clip_has_no_problem(self):
        self.assertEqual(self.got["same"], "")

    def test_float_rounding_is_no_problem(self):
        self.assertEqual(self.got["rounding"], "")

    def test_each_wrong_field_is_named(self):
        for key, word in (("none", "no AnimationClip"), ("name", "Name"), ("master", "Animation"), ("flags", "Flags"),
                          ("source2", "Source2"), ("duration", "Duration"), ("loop", "Loop"), ("loop_gone", "Loop")):
            self.assertIn(word, self.got[key], key)

    def test_a_clip_whose_master_is_absent_is_named(self):
        self.assertIn("NO MASTER", self.got["no_master"])

    def test_a_pack_master_resolves_to_the_armory_master_of_its_name(self):
        # The Kit gives a re-imported master a new GUID; the clip must follow the name (the rig at warg size).
        self.assertEqual(self.got["remapped"], "kg1")
        self.assertEqual(self.got["same_guid"], "pg1")

    def test_the_pack_master_itself_is_refused_on_a_scaled_rig(self):
        # A master with the pack's GUID is an unscaled copy: a Kit import of the scaled FBX gets a new GUID.
        self.assertEqual(self.got["pack_size"], "")

    def test_two_masters_with_one_name_are_refused(self):
        self.assertEqual(self.got["name_new"], "|g1")
        self.assertEqual(self.got["name_again"], "|g1")
        self.assertIn("TWO MASTERS NAMED walk", self.got["name_twice"])
        for part in ("g1", "g2", "b.tpac"):
            self.assertIn(part, self.got["name_twice"])

    def test_a_master_with_another_frame_count_than_the_packs_is_named(self):
        self.assertEqual(self.got["frames_ok"], "")
        self.assertIn("FRAMES walk", self.got["frames_bad"])
        self.assertEqual(self.got["frames_new"], "")

    def test_an_unknown_or_missing_master_resolves_to_nothing(self):
        self.assertEqual(self.got["no_name"], "")
        self.assertEqual(self.got["not_in_out"], "")

    def test_a_record_the_gait_plan_names_is_not_held_to_the_packs_keys_duration_and_travel(self):
        """tools/dire_wolf_gait_plan.json states those fields (#1565: the canter plays keys 0 to 16 where the pack
        cut 1 to 16), and tools/repoint_anim_clip.py --check holds them. The name, the flags and a master that is
        in the folder are still this script's."""
        self.assertEqual(self.got["owned"], "")
        self.assertIn("Name is other", self.got["owned_name"])
        self.assertIn("Flags is a", self.got["owned_name"])
        self.assertIn("NO MASTER g2", self.got["owned_name"])
        self.assertNotIn("Duration", self.got["owned_name"])

    def test_a_warg_binding_the_plan_does_not_hold_is_named_unless_it_is_warg_only(self):
        """A new warg binding must not leave the wolf behind, so the generator fails on one. The warg's walk twin
        (#1565) is the one binding the wolf does not copy: its own walk keeps one record on both rows."""
        self.assertEqual(self.got["unmapped"], "warg_new,horse_walk")
        self.assertEqual(self.got["all_mapped"], 0)


@unittest.skipUnless(SHELL, "no PowerShell on PATH: these tests need powershell.exe or pwsh")
class GuardTests(unittest.TestCase):
    def test_the_side_travels_keep_the_ratios_the_wolf_was_made_with(self):
        """The wolf's strafe and backward travels were the pack's forward travel times the warg's ratio of that day.
        Read from the warg's records they moved with every correction of a warg travel (#1563, #1565), and -Verify
        then named wolf records that nobody had changed."""
        with open(SCRIPT, encoding="utf-8-sig") as fh:
            text = fh.read()
        block = text[text.index("$FAMILY = @{"):]
        block = block[:block.index("}")]
        got = dict((k, float(v)) for k, v in re.findall(r"(\w+) = ([0-9.]+)", block))
        want = {"walk_strafe": 1.65 / 1.6, "walkbackfast": 1.5 / 1.6, "walkbackfast_straight": 1.5 / 1.6,
                "trot_strafe": 1.1 / 2.9, "canter_strafe": 0.8 / 4.0, "forward_gallop_left_foot_strafe": 1.5 / 8.5}
        self.assertEqual(sorted(got), sorted(want))
        for stem, ratio in want.items():
            self.assertAlmostEqual(got[stem], ratio, places=5, msg=stem)

    def test_the_installed_wolf_records_are_the_plan(self):
        """-Verify on this machine's Armory. It skips where the generator cannot run (exit 2: no TpacTool, no pack,
        no Windows PowerShell 5.1)."""
        done = _run_script("-Verify")
        if done.returncode == 2:
            self.skipTest("the generator cannot run here: %s" % (done.stdout.strip().splitlines() or ["no output"])[-1])
        self.assertEqual(done.returncode, 0, done.stdout[-2000:] + done.stderr[-500:])
        self.assertRegex(done.stdout, r"MODE = VERIFY .* errors=0")

    def test_apply_and_verify_together_exit_2(self):
        p = _run_script("-Apply", "-Verify")
        self.assertEqual(p.returncode, 2, p.stdout + p.stderr)
        self.assertIn("not both", p.stdout)

    def test_a_missing_pack_folder_exits_2_with_its_path(self):
        # The script checks the TpacTool DLL, the loader and the clip pack first, all on E: by default. Give it
        # empty stand-ins so the pack-masters check is the one that fails on any machine; nothing is loaded.
        with tempfile.TemporaryDirectory() as tmp:
            with open(os.path.join(tmp, "TpacTool.Lib.dll"), "wb"), open(os.path.join(tmp, "Loader.cs"), "wb"):
                pass
            p = _run_script("-Verify", "-TpacBin", tmp, "-Loader", os.path.join(tmp, "Loader.cs"), "-Pack", tmp,
                            "-PackMasters", "E:\\no\\such\\pack")
        self.assertEqual(p.returncode, 2, p.stdout + p.stderr)
        self.assertIn("no\\such\\pack", p.stdout)

    def test_a_missing_tool_exits_2_with_its_path(self):
        p = _run_script("-Verify", "-TpacBin", "E:\\no\\such\\bin")
        self.assertEqual(p.returncode, 2, p.stdout + p.stderr)
        self.assertIn("TpacTool.Lib.dll", p.stdout)


if __name__ == "__main__":
    unittest.main()
