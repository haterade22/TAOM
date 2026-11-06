"""gen_dwarf_attack_clips.ps1: the field rule for the two dwarf attack clips (#1087, 2026-11-06).

The generator needs TpacTool, the Kit's loader and the install, so these tests lift Get-ClipWant, Get-ClipFault,
Get-MasterFault and Assert-GameAndKitClosed out of the script with the PowerShell parser and run them on plain objects shaped like TpacTool's AnimationClip. The rule
that matters most: displace_position without a DisplacementUsage is a native null read (CTD), so a clip with one and
not the other must fail.
"""
import json
import os
import shutil
import subprocess
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gen_dwarf_attack_clips.ps1")
SHELL = shutil.which("powershell") or shutil.which("pwsh")   # Windows PowerShell 5.1 first: the script's runtime

HARNESS = r"""
param([string]$Script)
$ErrorActionPreference = 'Stop'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)
$want = @('Get-ClipWant', 'Get-ClipFault', 'Get-MasterFault', 'Assert-GameAndKitClosed')
$defs = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $a.Parent.Parent -is [System.Management.Automation.Language.ScriptBlockAst] }, $true) |
  Where-Object { $want -contains $_.Name })
$out = [ordered]@{ found = @($defs | ForEach-Object { $_.Name }) }
$out.guard_text = (@($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $a.Name -eq 'Assert-GameAndKitClosed' }, $true)) | Select-Object -First 1).Extent.Text
$saves = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and $a.Member.Value -eq 'Save' }, $true) | Sort-Object { $_.Extent.StartOffset })
$guards = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.IfStatementAst] -and $a.Clauses[0].Item1.Extent.Text -eq '$Apply' -and $a.Clauses[0].Item2.Extent.Text -match 'Assert-GameAndKitClosed' }, $true))
$out.saves = $saves.Count
$out.guard_before_save = ($guards.Count -ge 1 -and $saves.Count -ge 1 -and $guards[0].Extent.StartOffset -lt $saves[0].Extent.StartOffset -and $guards[0].Parent -is [System.Management.Automation.Language.NamedBlockAst])
function Conditions($node) {
  $c = @(); $p = $node.Parent
  while ($p) { if ($p -is [System.Management.Automation.Language.IfStatementAst]) { $c += @($p.Clauses | ForEach-Object { $_.Item1.Extent.Text }) }; $p = $p.Parent }
  return $c
}
$faultCalls = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] -and $a.GetCommandName() -eq 'Get-ClipFault' }, $true) | Sort-Object { $_.Extent.StartOffset })
$out.reread_conditions = @(Conditions $faultCalls[-1])
$fix = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] -and $a.InvocationOperator -eq 'Ampersand' -and $a.Extent.Text -match 'tpac_fix_item_checksums' }, $true))
$out.fix_conditions = if ($fix.Count -eq 1) { @(Conditions $fix[0]) } else { @('NOT FOUND') }
$out.fault_calls = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] -and $a.GetCommandName() -eq 'Get-ClipFault' }, $true)).Count
$specText = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and $a.Left.Extent.Text -eq '$SPEC' }, $true))
if ($defs.Count -ne $want.Count -or $specText.Count -ne 1) { $out | ConvertTo-Json -Depth 6 -Compress; exit 0 }
foreach ($d in $defs) { . ([scriptblock]::Create($d.Extent.Text)) }
. ([scriptblock]::Create($specText[0].Extent.Text))

Add-Type -TypeDefinition @'
public class Vec { public float X, Y, Z, W; public Vec(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; } public override string ToString() { return "<" + X + ", " + Y + ", " + Z + ", " + W + ">"; } }
public class DisplacementUsage { public Vec DisplacementVector; public float DisplacementEndProgress; }
public class BlendUsage { public float BlendStartProgress, BlendEndProgress; }
'@

$G = '685d89b9-bc2a-44e2-bfe4-c0b3dca5ab66'
# a clip that carries exactly what its want says
function Good($w) {
  $u = New-Object System.Collections.ArrayList
  if ($w.Displacement) { $d = New-Object DisplacementUsage; $d.DisplacementVector = New-Object Vec 0, ([float]$w.Displacement.Y), 0, 1; $d.DisplacementEndProgress = [float]$w.Displacement.End; [void]$u.Add($d) }
  [pscustomobject]@{
    Name = $w.Name; Animation = [guid]$w.Animation; Source1 = [float]$w.Source1; Source2 = [float]$w.Source2; Duration = [float]$w.Duration
    Priority = $w.Priority; BlendInPeriod = [float]$w.BlendIn; BlendOutPeriod = [float]$w.BlendOut; Flags = @($w.Flags)
    ContinueWithAction = ''; CombatParameterId = ''; VoiceCode = ''; FacialAnimationId = ''; BlendWithAnimation = ''; BlendsWithAction = ''
    ClipSource1Name = ''; ClipSource2Name = ''; GeneratedIndex = [sbyte]-1; SoundCode = $w.SoundCode
    StepPoints = (New-Object Vec ([float]$w.SoundPoint), -1, -1, -1); ClipUsages = $u
  }
}
$hb = Get-ClipWant $SPEC[0] $G 37
$dk = Get-ClipWant $SPEC[1] $G 55
$out.want = [ordered]@{
  hb = [ordered]@{ name = $hb.Name; s2 = $hb.Source2; dur = $hb.Duration; flags = @($hb.Flags); disp = $null -ne $hb.Displacement; sound = $hb.SoundPoint; pri = $hb.Priority; bin = $hb.BlendIn; bout = $hb.BlendOut; code = $hb.SoundCode }
  dk = [ordered]@{ name = $dk.Name; s2 = $dk.Source2; dur = $dk.Duration; flags = @($dk.Flags); y = $dk.Displacement.Y; end = $dk.Displacement.End; sound = $dk.SoundPoint }
}
$f = [ordered]@{}
$f.hb_good = Get-ClipFault (Good $hb) $hb
$f.dk_good = Get-ClipFault (Good $dk) $dk
$c = Good $dk; $c.ClipUsages.Clear(); $f.dk_flag_no_usage = Get-ClipFault $c $dk
$c = Good $dk; $c.Flags = @($c.Flags | Where-Object { $_ -ne 'displace_position' }); $f.dk_usage_no_flag = Get-ClipFault $c $dk
$c = Good $hb; $c.Flags = @($c.Flags) + 'displace_position'; $f.hb_flag_no_usage = Get-ClipFault $c $hb
$c = Good $dk; $c.ClipUsages[0].DisplacementEndProgress = 0.6; $f.dk_end = Get-ClipFault $c $dk
$c = Good $dk; $c.ClipUsages[0].DisplacementVector = New-Object Vec 0, 1.5, 0, 1; $f.dk_vector = Get-ClipFault $c $dk
$c = Good $dk; [void]$c.ClipUsages.Add((New-Object BlendUsage)); $f.dk_extra_usage = Get-ClipFault $c $dk
$c = Good $hb; $c.ContinueWithAction = 'act_kick_right_leg_continue'; $f.hb_continue = Get-ClipFault $c $hb
$c = Good $hb; $c.CombatParameterId = 'human_r_kick_params'; $f.hb_combat = Get-ClipFault $c $hb
$c = Good $hb; $c.Source2 = 37; $f.hb_source2 = Get-ClipFault $c $hb
$c = Good $hb; $c.Duration = [float]::NaN; $f.hb_nan = Get-ClipFault $c $hb
$c = Good $hb; $c.Flags = @($c.Flags | Where-Object { $_ -ne 'enforce_all' }); $f.hb_no_enforce_all = Get-ClipFault $c $hb
$c = Good $hb; $c.Animation = [guid]::NewGuid(); $f.hb_master = Get-ClipFault $c $hb
$c = Good $hb; $c.BlendWithAnimation = 'dwarf_head_butt'; $f.hb_keyed = Get-ClipFault $c $hb
$c = Good $hb; $c.BlendOutPeriod = [float]0; $f.hb_blend_out = Get-ClipFault $c $hb
$c = Good $hb; $c.BlendInPeriod = [float]0.5; $f.hb_blend_in = Get-ClipFault $c $hb
$c = Good $hb; $c.Priority = 40; $f.hb_priority = Get-ClipFault $c $hb
$c = Good $hb; $c.Name = 'kick_right_leg'; $f.hb_name = Get-ClipFault $c $hb
$c = Good $hb; $c.SoundCode = ''; $f.hb_soundcode = Get-ClipFault $c $hb
$c = Good $hb; $c.StepPoints = New-Object Vec 0.5, -1, -1, -1; $f.hb_steppoint = Get-ClipFault $c $hb
$c = Good $hb; $c.GeneratedIndex = [sbyte]3; $f.hb_generated = Get-ClipFault $c $hb
$c = Good $hb; [void]$c.ClipUsages.Add((New-Object BlendUsage)); $f.hb_blend_usage = Get-ClipFault $c $hb
foreach ($k in @('ContinueWithAction', 'CombatParameterId', 'VoiceCode', 'FacialAnimationId', 'BlendWithAnimation', 'BlendsWithAction', 'ClipSource1Name', 'ClipSource2Name')) {
  $c = Good $hb; $c.$k = 'x'; $f["blank_$k"] = Get-ClipFault $c $hb
}
$SKEL = [guid]'0099bfb2-09c4-4291-b225-3d24bdb14715'
$mine = [pscustomobject]@{ Skeleton = $SKEL; Duration = 37 }
$f.master_ok = Get-MasterFault $mine $SPEC[0] $SKEL
$f.master_skeleton = Get-MasterFault ([pscustomobject]@{ Skeleton = [guid]::NewGuid(); Duration = 37 }) $SPEC[0] $SKEL
$f.master_frames = Get-MasterFault ([pscustomobject]@{ Skeleton = $SKEL; Duration = 38 }) $SPEC[0] $SKEL
$f.master_frames_other_spec = Get-MasterFault $mine $SPEC[1] $SKEL
$out.fault = $f
$out | ConvertTo-Json -Depth 6 -Compress
"""


@unittest.skipUnless(SHELL, "no PowerShell on PATH")
class DwarfClipRuleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with tempfile.TemporaryDirectory() as d:
            harness = os.path.join(d, "harness.ps1")
            with open(harness, "w", encoding="ascii") as f:
                f.write(HARNESS)
            p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harness,
                                "-Script", SCRIPT], capture_output=True, text=True, timeout=180)
        if p.returncode != 0:
            raise AssertionError("harness failed (%d): %s %s" % (p.returncode, p.stdout, p.stderr))
        cls.r = json.loads(p.stdout.strip().splitlines()[-1])

    def test_rule_functions_are_in_the_script(self):
        self.assertEqual(sorted(self.r["found"]), ["Assert-GameAndKitClosed", "Get-ClipFault", "Get-ClipWant", "Get-MasterFault"])

    def test_verify_build_and_reread_all_use_the_rule(self):
        self.assertEqual(self.r["fault_calls"], 3, "-Verify, the pre-save check and the re-read after -Apply")

    def test_head_butt_want_is_the_whole_master_without_displacement(self):
        w = self.r["want"]["hb"]
        self.assertEqual((w["name"], w["s2"], w["dur"], w["disp"]), ("dwarf_head_butt", 36, 1.2, False))
        self.assertIn("enforce_all", w["flags"])
        self.assertNotIn("displace_position", w["flags"])
        self.assertAlmostEqual(w["sound"], 7 / 35)

    def test_drop_kick_want_moves_two_metres_ending_on_frame_32(self):
        w = self.r["want"]["dk"]
        self.assertEqual((w["name"], w["s2"], w["dur"], w["y"]), ("dwarf_drop_kick", 54, 1.8, 2.0))
        self.assertIn("displace_position", w["flags"])
        self.assertAlmostEqual(w["end"], 31 / 53)
        self.assertAlmostEqual(w["sound"], 10 / 53)

    def test_clips_matching_their_want_pass(self):
        self.assertEqual(self.r["fault"]["hb_good"], "")
        self.assertEqual(self.r["fault"]["dk_good"], "")

    def test_displace_flag_without_usage_fails(self):
        # the native null read: the engine reads the usage whenever the flag is set
        self.assertIn("displace_position flag set but 0 DisplacementUsage", self.r["fault"]["dk_flag_no_usage"])
        self.assertIn("displace_position flag set but 0 DisplacementUsage", self.r["fault"]["hb_flag_no_usage"])

    def test_usage_without_flag_fails(self):
        self.assertIn("displace_position flag clear but 1 DisplacementUsage", self.r["fault"]["dk_usage_no_flag"])

    def test_wrong_displacement_fails(self):
        self.assertIn("DisplacementEndProgress", self.r["fault"]["dk_end"])
        self.assertIn("DisplacementVector", self.r["fault"]["dk_vector"])
        self.assertIn("want one DisplacementUsage", self.r["fault"]["dk_extra_usage"])

    def test_kick_leftovers_fail(self):
        self.assertIn("ContinueWithAction", self.r["fault"]["hb_continue"])
        self.assertIn("CombatParameterId", self.r["fault"]["hb_combat"])
        self.assertIn("BlendWithAnimation", self.r["fault"]["hb_keyed"])

    def test_range_master_flags_and_nan_fail(self):
        self.assertIn("Source", self.r["fault"]["hb_source2"])
        self.assertIn("Duration", self.r["fault"]["hb_nan"])
        self.assertIn("Flags", self.r["fault"]["hb_no_enforce_all"])
        self.assertIn("Animation", self.r["fault"]["hb_master"])

    def test_want_pins_priority_blends_and_the_kick_sound(self):
        w = self.r["want"]["hb"]
        self.assertEqual((w["pri"], w["bin"], w["bout"], w["code"]), (34, 0.2, 0.2, "event:/mission/combat/swing/kick"))

    def test_each_recipe_field_is_checked(self):
        f = self.r["fault"]
        for key, word in (("hb_blend_out", "Blends"), ("hb_blend_in", "Blends"), ("hb_priority", "Priority"),
                          ("hb_name", "Name"), ("hb_soundcode", "SoundCode"), ("hb_steppoint", "StepPoints"),
                          ("hb_generated", "GeneratedIndex"), ("hb_blend_usage", "want none")):
            self.assertIn(word, f[key], key)

    def test_each_blank_field_must_be_blank(self):
        for k in ("ContinueWithAction", "CombatParameterId", "VoiceCode", "FacialAnimationId", "BlendWithAnimation",
                  "BlendsWithAction", "ClipSource1Name", "ClipSource2Name"):
            self.assertIn(k, self.r["fault"]["blank_" + k])

    def test_master_pin_passes_a_match_and_refuses_another_skeleton_or_frame_count(self):
        f = self.r["fault"]
        self.assertEqual(f["master_ok"], "")
        self.assertTrue(f["master_skeleton"].startswith("SKELETON dwarf_head_butt"))
        self.assertTrue(f["master_frames"].startswith("FRAMES dwarf_head_butt: master has 38, pinned 37"))
        self.assertTrue(f["master_frames_other_spec"].startswith("FRAMES dwarf_drop_kick: master has 37, pinned 55"))

    def test_apply_guard_runs_before_the_first_save(self):
        self.assertGreaterEqual(self.r["saves"], 1)
        self.assertTrue(self.r["guard_before_save"], "`if ($Apply) { Assert-GameAndKitClosed }` must come first, at script level")

    def test_reread_runs_on_every_apply_and_the_checksum_fix_only_after_a_write(self):
        self.assertIn("$Apply", self.r["reread_conditions"])
        self.assertFalse([c for c in self.r["reread_conditions"] if "written" in c], self.r["reread_conditions"])
        self.assertIn("$written -gt 0", self.r["fix_conditions"])


def _run_child(command):
    return subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command],
                          capture_output=True, text=True, timeout=120)


@unittest.skipUnless(SHELL, "no PowerShell on PATH")
class DwarfClipRefusalTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with tempfile.TemporaryDirectory() as d:
            harness = os.path.join(d, "harness.ps1")
            with open(harness, "w", encoding="ascii") as f:
                f.write(HARNESS)
            p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harness,
                                "-Script", SCRIPT], capture_output=True, text=True, timeout=180)
        cls.guard = json.loads(p.stdout.strip().splitlines()[-1])["guard_text"]

    def test_guard_refuses_with_exit_2_while_bannerlord_runs(self):
        p = _run_child("function Get-Process { [pscustomobject]@{ProcessName='Bannerlord'} }\n%s\nAssert-GameAndKitClosed\nWrite-Output 'PASSED'" % self.guard)
        self.assertEqual(p.returncode, 2, p.stdout + p.stderr)
        self.assertIn("REFUSED", p.stdout)
        self.assertNotIn("PASSED", p.stdout)

    def test_guard_lets_a_machine_with_nothing_running_through(self):
        p = _run_child("function Get-Process { }\n%s\nAssert-GameAndKitClosed\nWrite-Output 'PASSED'" % self.guard)
        self.assertEqual(p.returncode, 0, p.stdout + p.stderr)
        self.assertIn("PASSED", p.stdout)

    def test_missing_tool_path_exits_2_not_1(self):
        with tempfile.TemporaryDirectory() as empty:
            p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", SCRIPT,
                                "-Verify", "-TpacBin", empty], capture_output=True, text=True, timeout=120)
        self.assertEqual(p.returncode, 2, p.stdout + p.stderr)
        self.assertIn("not found", p.stdout)

    def test_apply_with_verify_exits_2(self):
        p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", SCRIPT,
                            "-Apply", "-Verify"], capture_output=True, text=True, timeout=120)
        self.assertEqual(p.returncode, 2, p.stdout + p.stderr)


if __name__ == "__main__":
    unittest.main()
