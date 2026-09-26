"""gen_troll_anim_clips.ps1: the melee attack table key a generated clip carries (2026-09-26).

A clip gets a row in the engine's melee attack table only when its "Blends with animation" box (TpacTool
UnknownClipName) holds its own name, or through the blend children the Kit generates toward a _balanced twin; a
release or blocked action on a clip with no row crashes. The generator used to blank the box on every clone, so a
re-cut silently undid set_clip_balance_name.py. The rule now: a clone of a KEYED vanilla template (UnknownClipName
or ClipSource1Name set) is self-keyed, "Blends with action" (BlendsWithAction) emptied, the generated-child fields
cleared; a clone of an unkeyed template keeps the three names blank.

The generator itself needs TpacTool, the Kit's loader and the install, so these tests lift the rule's three
functions out of the script with the PowerShell parser and run them on plain objects shaped like TpacTool's
AnimationClip. The same parse checks that both write paths and both -Verify modes go through them.
"""
import json
import os
import shutil
import subprocess
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gen_troll_anim_clips.ps1")
SHELL = shutil.which("powershell") or shutil.which("pwsh")   # Windows PowerShell 5.1 first: the script's runtime

HARNESS = r"""
param([string]$Script)
$ErrorActionPreference = 'Stop'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)
$want = @('Test-KeyedTemplate', 'Set-ClipKey', 'Get-ClipKeyFault')
$defs = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true) |
  Where-Object { $want -contains $_.Name })
$out = [ordered]@{ found = @($defs | ForEach-Object { $_.Name }) }

# calls outside the definitions, and writes to the key fields anywhere but Set-ClipKey
$calls = [ordered]@{}
foreach ($n in $want) {
  $calls[$n] = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] -and $a.GetCommandName() -eq $n }, $true)).Count
}
$out.calls = $calls
$setDef = $defs | Where-Object { $_.Name -eq 'Set-ClipKey' } | Select-Object -First 1
$keyFields = @('UnknownClipName', 'ClipSource1Name', 'ClipSource2Name', 'BlendsWithAction', 'GeneratedIndex')
# every Set-ClipKey call comes after an if on the clone name's length in its own block: the name IS the key, and
# the engine's clip name and row key are 64-byte buffers, 63 usable
$out.name_guarded = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] -and $a.GetCommandName() -eq 'Set-ClipKey' }, $true) | ForEach-Object {
  $call = $_
  $p = $call.Parent
  while ($null -ne $p -and -not ($p -is [System.Management.Automation.Language.StatementBlockAst])) { $p = $p.Parent }
  $before = @($p.Statements | Where-Object { $_.Extent.EndOffset -le $call.Extent.StartOffset })
  [bool](@($before | Where-Object { $_ -is [System.Management.Automation.Language.IfStatementAst] -and $_.Clauses[0].Item1.Extent.Text -match '\.Length\s+-gt\s+63' }).Count)
})
$out.stray_writes = @($ast.FindAll({ param($a)
    $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $a.Left -is [System.Management.Automation.Language.MemberExpressionAst] -and
    $keyFields -contains $a.Left.Member.Extent.Text }, $true) |
  Where-Object { $null -eq $setDef -or $_.Extent.StartOffset -lt $setDef.Extent.StartOffset -or $_.Extent.EndOffset -gt $setDef.Extent.EndOffset } |
  ForEach-Object { $_.Extent.StartLineNumber })

if ($defs.Count -ne $want.Count) { $out | ConvertTo-Json -Depth 6 -Compress; exit 0 }
foreach ($d in $defs) { . ([scriptblock]::Create($d.Extent.Text)) }

function Clip($name, $ucn = '', $bwa = '', $cs1 = '', $cs2 = '', $gen = -1) {
  [pscustomobject]@{ Name = $name; UnknownClipName = $ucn; BlendsWithAction = $bwa; ClipSource1Name = $cs1; ClipSource2Name = $cs2; GeneratedIndex = [sbyte]$gen }
}
function Snap($c) {
  [ordered]@{ UnknownClipName = $c.UnknownClipName; BlendsWithAction = $c.BlendsWithAction; ClipSource1Name = $c.ClipSource1Name
              ClipSource2Name = $c.ClipSource2Name; GeneratedIndex = [int]$c.GeneratedIndex }
}
function Twin { Clip 'release_overswing_2h' 'release_overswing_2h_balanced' 'act_release_overswing_2h_balanced' }

$out.keyed = [ordered]@{
  self  = Test-KeyedTemplate (Clip 'release_lance' 'release_lance')
  twin  = Test-KeyedTemplate (Twin)
  child = Test-KeyedTemplate (Clip 'release_overswing_2h_3' '' '' 'release_overswing_2h' 'release_overswing_2h_balanced' 3)
  plain = Test-KeyedTemplate (Clip 'walk_forward_unarmed' '' 'act_walk')
}

$set = [ordered]@{}
$c = Twin; $r = @(Set-ClipKey $c 'anim_hill_troll_release_overswing_2h' $true)
$set.keyed = [ordered]@{ out = $r.Count; clip = Snap $c; fault = Get-ClipKeyFault ([pscustomobject]@{ Name = 'anim_hill_troll_release_overswing_2h'; UnknownClipName = $c.UnknownClipName; BlendsWithAction = $c.BlendsWithAction; ClipSource1Name = $c.ClipSource1Name; ClipSource2Name = $c.ClipSource2Name; GeneratedIndex = $c.GeneratedIndex }) $true }
$c = Clip 'release_overswing_2h_3' '' 'act_x' 'release_overswing_2h' 'release_overswing_2h_balanced' 3; $r = @(Set-ClipKey $c 'anim_hill_troll_child' $true)
$set.keyed_child = [ordered]@{ out = $r.Count; clip = Snap $c }
$c = Clip 'walk_forward_unarmed' '' 'act_walk' '' 'stale'; $r = @(Set-ClipKey $c 'anim_hill_troll_walk' $false)
$set.unkeyed = [ordered]@{ out = $r.Count; clip = Snap $c; fault = Get-ClipKeyFault ([pscustomobject]@{ Name = 'anim_hill_troll_walk'; UnknownClipName = $c.UnknownClipName; BlendsWithAction = $c.BlendsWithAction; ClipSource1Name = $c.ClipSource1Name; ClipSource2Name = $c.ClipSource2Name; GeneratedIndex = $c.GeneratedIndex }) $false }
$out.set = $set

$n = 'anim_hill_troll_release_overswing_2h'
$out.fault = [ordered]@{
  keyed_self       = Get-ClipKeyFault (Clip $n $n) $true
  keyed_blank      = Get-ClipKeyFault (Clip $n) $true
  keyed_other      = Get-ClipKeyFault (Clip $n 'release_overswing_2h_balanced') $true
  keyed_action     = Get-ClipKeyFault (Clip $n $n 'act_release_overswing_2h_balanced') $true
  keyed_child      = Get-ClipKeyFault (Clip $n $n '' 'a' 'b' 3) $true
  keyed_case       = Get-ClipKeyFault (Clip $n $n.ToUpper()) $true
  unkeyed_blank    = Get-ClipKeyFault (Clip $n '' 'act_x') $false
  unkeyed_key      = Get-ClipKeyFault (Clip $n $n) $false
  unkeyed_src2     = Get-ClipKeyFault (Clip $n '' '' '' 'b') $false
}
$out | ConvertTo-Json -Depth 6 -Compress
"""


@unittest.skipUnless(SHELL, "no PowerShell on PATH")
class ClipKeyRuleTests(unittest.TestCase):
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

    def _list(self, v):
        return v if isinstance(v, list) else ([] if v is None else [v])

    def test_the_rule_is_three_functions_in_the_script(self):
        self.assertEqual(sorted(self._list(self.r["found"])), ["Get-ClipKeyFault", "Set-ClipKey", "Test-KeyedTemplate"])

    def test_both_write_paths_and_both_verify_modes_use_the_rule(self):
        calls = self.r["calls"]
        self.assertEqual(calls["Set-ClipKey"], 2, "the -CloneByName path and the Fab path")
        self.assertEqual(calls["Get-ClipKeyFault"], 2, "the -CloneByName -Verify and the Fab -Verify")
        self.assertGreaterEqual(calls["Test-KeyedTemplate"], 1)

    def test_every_clone_name_is_length_checked_before_it_is_keyed(self):
        # the -CloneByName path and the Fab path both refuse a name over 63 characters before Set-ClipKey, which only
        # sets fields: a self-key is the name, so the name's own limit covers it (the header promises it for every name)
        self.assertEqual(self._list(self.r["name_guarded"]), [True, True])

    def test_nothing_else_writes_the_key_fields(self):
        # the old lines 270 and 436 blanked the key on every clone
        self.assertEqual(self._list(self.r["stray_writes"]), [])

    def test_a_template_is_keyed_by_its_own_name_a_twin_or_a_parent(self):
        self.assertEqual(self.r["keyed"], {"self": True, "twin": True, "child": True, "plain": False})

    def test_a_keyed_template_makes_a_self_keyed_clone(self):
        s = self.r["set"]["keyed"]
        self.assertEqual(s["out"], 0, "Set-ClipKey only sets fields")
        self.assertEqual(s["clip"], {"UnknownClipName": "anim_hill_troll_release_overswing_2h", "BlendsWithAction": "",
                                     "ClipSource1Name": "", "ClipSource2Name": "", "GeneratedIndex": -1})
        self.assertEqual(s["fault"], "", "a fresh clone passes -Verify")

    def test_a_generated_child_template_loses_its_parents_and_index(self):
        s = self.r["set"]["keyed_child"]
        self.assertEqual(s["out"], 0)
        self.assertEqual(s["clip"], {"UnknownClipName": "anim_hill_troll_child", "BlendsWithAction": "",
                                     "ClipSource1Name": "", "ClipSource2Name": "", "GeneratedIndex": -1})

    def test_an_unkeyed_template_keeps_todays_blank_names(self):
        s = self.r["set"]["unkeyed"]
        self.assertEqual(s["out"], 0)
        self.assertEqual(s["clip"], {"UnknownClipName": "", "BlendsWithAction": "act_walk",
                                     "ClipSource1Name": "", "ClipSource2Name": "", "GeneratedIndex": -1})
        self.assertEqual(s["fault"], "")

    def test_verify_passes_the_right_shapes_only(self):
        f = self.r["fault"]
        self.assertEqual((f["keyed_self"], f["unkeyed_blank"]), ("", ""))
        for bad in ("keyed_blank", "keyed_other", "keyed_action", "keyed_child", "keyed_case", "unkeyed_key",
                    "unkeyed_src2"):
            self.assertTrue(f[bad], bad)


if __name__ == "__main__":
    unittest.main()
