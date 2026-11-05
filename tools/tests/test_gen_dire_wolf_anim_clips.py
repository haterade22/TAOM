"""gen_dire_wolf_anim_clips.ps1: Get-ClipProblem, the check -Apply and -Verify share, and the start guards (#1081).

The generator needs TpacTool, the Kit's loader and the install, so these tests lift Get-ClipProblem out of the script with
the PowerShell parser and run it on plain tables, the way test_gen_animalia_anim_clips.py does. Windows PowerShell 5.1
first: the script's runtime.
"""
import json
import os
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
  Where-Object { $_.Name -eq 'Get-ClipProblem' })
. ([scriptblock]::Create($fn[0].Extent.Text))
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


@unittest.skipUnless(SHELL, "no PowerShell on PATH: these tests need powershell.exe or pwsh")
class GuardTests(unittest.TestCase):
    def test_apply_and_verify_together_exit_2(self):
        p = _run_script("-Apply", "-Verify")
        self.assertEqual(p.returncode, 2, p.stdout + p.stderr)
        self.assertIn("not both", p.stdout)

    def test_a_missing_tool_exits_2_with_its_path(self):
        p = _run_script("-Verify", "-TpacBin", "E:\\no\\such\\bin")
        self.assertEqual(p.returncode, 2, p.stdout + p.stderr)
        self.assertIn("TpacTool.Lib.dll", p.stdout)


if __name__ == "__main__":
    unittest.main()
