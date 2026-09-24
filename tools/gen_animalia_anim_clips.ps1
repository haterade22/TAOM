param(
  [switch]$Apply,
  [switch]$Verify,
  [string]$Masters = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\creature\elk\animations',
  [string]$Vanilla = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\AssetPackages\animation_clips.tpac',
  [string]$ElkMeasure = "$PSScriptRoot\blender\animalia_elk_clip_measure.json",
  [string]$MooseMeasure = "$PSScriptRoot\blender\animalia_moose_clip_measure.json",
  [string]$TpacBin = 'E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin',
  [string]$Loader = 'E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs'
)
<#
Author the AnimationClip tpacs for the Animalia elk and moose (#646, 2026-09-23): one <clip>_anm.tpac beside each
master it plays, for the actions the elk and moose override in their child sets of as_horse
(docs/features/animalia-elk-moose.md "Planned bindings"). Horse sibling of gen_troll_anim_clips.ps1.

Every clip is a CLONE of the vanilla horse clip that as_horse binds to the same action (read from
Native/AssetPackages/animation_clips.tpac, 2026-09-23), so flags, priority, blends, sound codes, continue actions
and usage shapes are vanilla's verbatim. Per clip only these change: Name ('anim_' + master name, '_stand'
appended for a gait's stand twin), Guid (fresh), Animation (the master's GUID), Source1 = 1, Source2 = master
Duration - 1 (frame 0 of a master is its REST frame; the war ram's proven clip is 1..105 of a 106 master),
Duration = (Source2 - Source1 + 1) / 30 s, and the measured values (tools/blender/measure_animalia_clips.py):
  gaits   QuadMovementUsage.LoopDisplacement = the ground covered in one loop at horse size (the retarget dropped
          root motion, so it comes back as metadata; with it the engine slows the legs at horse speeds instead
          of sliding the hooves). PaceSwitchLimits stay vanilla's. StepPoints = the four hoof plants sorted
          (walk, trot, backward, as vanilla) or the first plant (canter, gallop, as vanilla). The '_stand' twin
          clones the vanilla gait_* clip: same range, no usage, as vanilla.
  deaths  StepPoints.Z (vanilla's 0.35 body-fall point) = the measured fall fraction.
Templates by role (vanilla as_horse action -> clip):
  walk horse_walkfast / gait_walkfast       trot anim_horse_trot_2 / horse_gait_trot_2
  canter horse_canterfast / gait_canterfast  gallop horse_forward_gallop_right_foot / _right_foot_stand
  back horse_walkbackfast / gait_walk_backward
  idle horse_idle_1 (no sound)                eat horse_stand_1 (keeps the horse_eating sound points)
  rear horse_rear                             kick horse_kick (keeps horse_kick_params + the kick swing sound)
  antler horse_kick without CombatParameterId, sound and step points: the war ram's head-butt recipe
         (war_ram_butt: priority 34, blends 0.2 / 0.4, enforce_lowerbody + enforce_all), for its own action
  hitf horse_hit_from_front  hitb horse_hit_from_back
  fallL horse_death_left_side  fallR horse_death_right_side  (+ _continue for the lying pose masters)
  standmove horse_stand_for_movement_data: the movement system's standing pace (QuadMovementUsage with 0 travel,
         pace -0.4..0.4), clip '<master>_movement' on the elk's stand_01 / the moose's stand_00; without it a
         ridden elk standing still would drop to the horse's standing clip. A master may carry several roles.
Not generated, on purpose: turns (the retarget baked each 90 deg turn into the pose and the engine turns the agent
itself, so a looping turn clip would double it: vanilla horse turns stay), jumps (vanilla splits a jump into
start / loop / end actions and the pack's single clips lift the pelvis about 2 m: they need re-cutting first),
strafes and quick stops (no matching pack clip).

The templates are loaded once and MUTATED per clip (reloading 6,177 clips per clip is slow): every field this
script touches is snapshotted at load and restored before each clip, so one clip's overrides never leak into the
next clip on the same template. The masters must name horse_skeleton (tools/wire_anim_master_skeletons.ps1).

Dry run by default; -Apply refuses while Bannerlord or the Modding Kit is running (Assert-GameAndKitClosed),
then writes, never overwrites an existing _anm.tpac, re-reads every file (name, master GUID, range), then
refreshes item checksums per folder (tpac_fix_item_checksums.py, which does not recurse) and re-checks its exit
code. Open the Kit once afterwards so it writes the clips' RuntimeDataCache entries, then save.

-Verify (read-only, ported from gen_troll_anim_clips.ps1) checks every PLANNED clip (the same (role, clip name)
pairs Get-AnimaliaClipPlan derives for generation) against what is actually on disk beside its master and
reports NO MASTER (the master itself is gone), ORPHAN (the clip file is missing, the clip inside it is named
otherwise, or its Animation GUID names a different master than the plan's), STALE (Source2 != master frames - 1),
and a stale item checksum in any clip folder (tpac_fix_item_checksums.py dry run); exits 1 on any. -Apply runs the
same check afterwards over every clip, folding any failure into the exit code.

A gait or death clip is generated only from a sound measurement (Get-MeasureProblem): a clip the measure run
recorded as an error, a loop that covers no ground, too few hoof plants or a fall fraction outside (0, 1) is
reported BAD MEASURE and counted as an error, never written with zeros.

Exit 2 when it cannot run (game or Kit open, not Windows PowerShell, a tool path missing, -Apply and -Verify
together). Windows PowerShell 5.1 (TolerantTpacLoader.cs is .NET Framework):
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_animalia_anim_clips.ps1 [-Apply | -Verify]
#>
$ErrorActionPreference = 'Stop'

function Assert-GameAndKitClosed {
  $procs = @(Get-Process -Name 'Bannerlord*', 'TaleWorlds.MountAndBlade*' -ErrorAction SilentlyContinue)
  if ($procs.Count -gt 0) {
    Write-Output ("REFUSED: Bannerlord or the Modding Kit is running (" + (($procs | ForEach-Object { $_.ProcessName }) -join ', ') + "); close it and re-run")
    exit 2
  }
}
if ($Apply -and $Verify) { Write-Output 'ERROR: pass -Apply or -Verify, not both (-Apply already re-verifies)'; exit 2 }
if ($Apply) { Assert-GameAndKitClosed }
if ($PSVersionTable.PSEdition -ne 'Desktop') {
  Write-Output 'ERROR: run under Windows PowerShell 5.1 (powershell.exe); TolerantTpacLoader.cs targets .NET Framework'
  exit 2
}
foreach ($need in @("$TpacBin\TpacTool.Lib.dll", $Loader)) {
  if (-not (Test-Path $need)) { Write-Output "ERROR: $need not found (pass -TpacBin / -Loader)"; exit 2 }
}

Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
$HORSE = [Guid]'1163bb17-777d-49b1-b083-aad79dc544fd'
$FPS = 30.0
function V4($a, $b, $c, $d) { New-Object System.Numerics.Vector4 -ArgumentList @([float]$a, [float]$b, [float]$c, [float]$d) }

$TEMPLATE_OF = @{
  walk = 'horse_walkfast'; walk_stand = 'gait_walkfast'; trot = 'anim_horse_trot_2'; trot_stand = 'horse_gait_trot_2'
  canter = 'horse_canterfast'; canter_stand = 'gait_canterfast'
  gallop = 'horse_forward_gallop_right_foot'; gallop_stand = 'horse_forward_gallop_right_foot_stand'
  back = 'horse_walkbackfast'; back_stand = 'gait_walk_backward'
  idle = 'horse_idle_1'; eat = 'horse_stand_1'; rear = 'horse_rear'; kick = 'horse_kick'; antler = 'horse_kick'
  hitf = 'horse_hit_from_front'; hitb = 'horse_hit_from_back'
  fallL = 'horse_death_left_side'; fallL_cont = 'horse_death_left_side_continue'
  fallR = 'horse_death_right_side'; fallR_cont = 'horse_death_right_side_continue'
  standmove = 'horse_stand_for_movement_data'
}
# master stem (after animalia_<animal>_) -> role; a gait also gets its _stand twin. Measure key = the pack stem.
$PLAN = @{
  elk = [ordered]@{
    loco_walk = 'walk'; loco_trot = 'trot'; loco_run = 'canter'; loco_sprint = 'gallop'; loco_walkback = 'back'
    stand_01 = @('idle', 'standmove'); stand_02 = 'idle'; stand_03 = 'idle'; alert_looking_l = 'idle'; alert_looking_r = 'idle'
    vocalization_bugling = 'idle'
    stand_eating_01 = 'eat'; stand_eating_02 = 'eat'; stand_eating_03 = 'eat'; stand_drinking_01 = 'eat'
    attack_front_high = 'rear'; attack_hind = 'kick'; attack_front_low = 'antler'
    hit_chestl_heavy = 'hitf'; hit_pelvisl_heavy = 'hitb'
    death_stand_l = 'fallL'; death_stand_l_pose = 'fallL_cont'; death_stand_r = 'fallR'; death_stand_r_pose = 'fallR_cont'
  }
  moose = [ordered]@{
    loco_walk = 'walk'; loco_trot = 'trot'; loco_run = 'canter'; loco_sprint = 'gallop'; loco_walkback = 'back'
    stand_00 = @('idle', 'standmove'); stand_01 = 'idle'; stand_02 = 'idle'
    eating_01 = 'eat'; eating_02 = 'eat'; eating_03 = 'eat'; drinking_01 = 'eat'
    attack_legs_01 = 'kick'; attack_head_01 = 'antler'
    death_l = 'fallL'; death_l_pose = 'fallL_cont'; death_r = 'fallR'; death_r_pose = 'fallR_cont'
  }
}
function Get-MeasureProblem($ms, [string]$role) {
  # $null when the measurement can drive a clip, else why not. The measure run stores a failed clip as
  # {"error": ...}, and PowerShell reads its missing fields as $null, which [float] turns into 0.
  if ($null -eq $ms -or $ms.PSObject.Properties['error']) { return 'missing or failed in the measure run' }
  if ($role -in @('fallL', 'fallR')) {
    if (-not ([double]$ms.fall_fraction -gt 0 -and [double]$ms.fall_fraction -lt 1)) { return 'fall_fraction outside (0, 1)' }
    return $null
  }
  if (-not ([double]$ms.loop_displacement_m -gt 0)) { return 'loop_displacement_m is not above 0' }
  $need = $(if ($role -in @('canter', 'gallop')) { 1 } else { 4 })
  if (@($ms.plants_sorted | Where-Object { $null -ne $_ }).Count -lt $need) { return "fewer than $need hoof plants" }
  return $null
}

function Get-AnimaliaClipPlan([string]$animal, [string]$stem, [string]$mname) {
  # (role, clip name) pairs for one master: the action(s) $PLAN binds it to, each gait expanded with
  # its own _stand twin. One function so generation and -Verify (and the post--Apply re-check) derive
  # clip names identically instead of three copies of the same expansion drifting apart.
  $roles = @()
  foreach ($v in @($PLAN[$animal][$stem])) {
    $roles += $v
    if ($v -in @('walk', 'trot', 'canter', 'gallop', 'back')) { $roles += ($v + '_stand') }
  }
  $roles | ForEach-Object {
    $r = $_
    $clipName = 'anim_' + $mname + $(if ($r -like '*_stand') { '_stand' } elseif ($r -eq 'standmove') { '_movement' } else { '' })
    [pscustomobject]@{ Role = $r; ClipName = $clipName }
  }
}

# ---- masters (recursive), all must name horse_skeleton
$master = @{}
foreach ($f in (Get-ChildItem -Path $Masters -Recurse -Filter '*_geo.tpac')) {
  foreach ($it in (Load $f.FullName).Package.Items) {
    if ($it.GetType().Name -eq 'SkeletalAnimation') { $master[$it.Name] = @{ guid = $it.Guid; frames = [int]$it.Duration; skel = $it.Skeleton; dir = $f.DirectoryName } }
  }
}
$badSkel = @($master.Keys | Where-Object { $master[$_].skel -ne $HORSE })
Write-Output ("masters: {0}   not on horse_skeleton: {1}" -f $master.Count, $badSkel.Count)
if ($badSkel.Count -gt 0) { Write-Output ("run tools\wire_anim_master_skeletons.ps1 first: " + ($badSkel -join ', ')); exit 1 }

function Test-AnimaliaClips {
  # NO MASTER / ORPHAN / STALE over every planned clip (read-only). Shared by -Verify and by the
  # post--Apply re-check, so both read the same thing off disk the same way.
  $ok = 0; $stale = 0; $orphan = 0; $noMaster = 0
  foreach ($animal in 'elk', 'moose') {
    foreach ($stem in $PLAN[$animal].Keys) {
      $mname = "animalia_${animal}_$stem"
      $plans = @(Get-AnimaliaClipPlan $animal $stem $mname)
      if (-not $master.ContainsKey($mname)) {
        $noMaster += $plans.Count
        foreach ($slot in $plans) { Write-Host ("NO MASTER {0,-44} -> master {1} not on disk" -f $slot.ClipName, $mname) }
        continue
      }
      $m = $master[$mname]
      foreach ($slot in $plans) {
        $out = Join-Path $m.dir ($slot.ClipName + '_anm.tpac')
        if (-not (Test-Path $out)) { $orphan++; Write-Host ("ORPHAN {0,-44} -> file missing" -f $slot.ClipName); continue }
        $cl = @((Load $out).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' })[0]
        if ($null -eq $cl -or $cl.Name -ne $slot.ClipName) {
          # The action set binds the clip's internal name, not the file's (a Kit rename changes only the inside).
          $orphan++
          Write-Host ("ORPHAN {0,-44} -> the file holds clip '{1}'" -f $slot.ClipName, $cl.Name)
          continue
        }
        if ([Guid]$cl.Animation -ne [Guid]$m.guid) {
          $orphan++
          Write-Host ("ORPHAN {0,-44} -> Animation GUID {1} is not {2}'s ({3})" -f $slot.ClipName, $cl.Animation, $mname, $m.guid)
          continue
        }
        if ([int]$cl.Source2 -ne ($m.frames - 1)) {
          $stale++
          Write-Host ("STALE  {0,-44} -> Source2={1}, master frames={2} (want {3})" -f $slot.ClipName, $cl.Source2, $m.frames, ($m.frames - 1))
        } else { $ok++ }
      }
    }
  }
  # Stage 8's gate: a clip written by TpacTool's Save carries a zeroed item checksum until the fixer runs, and a
  # re-run that writes nothing would otherwise never look.
  foreach ($d in ($master.Values | ForEach-Object { $_.dir } | Sort-Object -Unique)) {
    $last = & python "$PSScriptRoot\tpac_fix_item_checksums.py" $d --glob '*_anm.tpac' | Select-Object -Last 1
    if ($LASTEXITCODE -or "$last" -notmatch '^files=[1-9]\d* stale-items=0\b') {
      $stale++; Write-Host ("CHECKSUM {0} -> {1}" -f $d, $last)
    }
  }
  Write-Host ("verify: clips ok={0} stale={1} orphan={2} no-master={3}" -f $ok, $stale, $orphan, $noMaster)
  return @{ ok = $ok; stale = $stale; orphan = $orphan; noMaster = $noMaster }
}

if ($Verify) {
  $result = Test-AnimaliaClips
  if ($result.stale + $result.orphan + $result.noMaster -gt 0) { exit 1 } else { exit 0 }
}

$meas = @{ elk = (Get-Content $ElkMeasure -Raw | ConvertFrom-Json).clips; moose = (Get-Content $MooseMeasure -Raw | ConvertFrom-Json).clips }

# ---- templates + snapshots
$van = @{}
foreach ($it in (Load $Vanilla).Package.Items) { if ($it.GetType().Name -eq 'AnimationClip' -and $TEMPLATE_OF.Values -contains $it.Name) { $van[$it.Name] = $it } }
$missing = @($TEMPLATE_OF.Values | Sort-Object -Unique | Where-Object { -not $van.ContainsKey($_) })
if ($missing.Count -gt 0) { Write-Output ("TEMPLATE MISSING: " + ($missing -join ', ')); exit 1 }
$snap = @{}
foreach ($k in $van.Keys) {
  $c = $van[$k]
  $snap[$k] = @{
    Priority = $c.Priority; BlendIn = $c.BlendInPeriod; BlendOut = $c.BlendOutPeriod; Flags = @($c.Flags | ForEach-Object { $_ })
    Cp = $c.CombatParameterId; Sound = $c.SoundCode; Step = $c.StepPoints; Cont = $c.ContinueWithAction
    Loop = @($c.ClipUsages | Where-Object { $_.GetType().Name -eq 'QuadMovementUsage' } | ForEach-Object { $_.LoopDisplacement })
  }
}
function Restore($c, $s) {
  $c.Priority = $s.Priority; $c.BlendInPeriod = $s.BlendIn; $c.BlendOutPeriod = $s.BlendOut
  $c.Flags.Clear(); foreach ($x in $s.Flags) { $c.Flags.Add($x) }
  $c.CombatParameterId = $s.Cp; $c.SoundCode = $s.Sound; $c.StepPoints = $s.Step; $c.ContinueWithAction = $s.Cont
  $i = 0; foreach ($u in $c.ClipUsages) { if ($u.GetType().Name -eq 'QuadMovementUsage') { $u.LoopDisplacement = $s.Loop[$i]; $i++ } }
}

$rows = @(); $written = 0; $skipped = 0; $errors = 0
foreach ($animal in 'elk', 'moose') {
  foreach ($stem in $PLAN[$animal].Keys) {
    $mname = "animalia_${animal}_$stem"
    if (-not $master.ContainsKey($mname)) { $errors++; Write-Output ("NO MASTER {0}" -f $mname); continue }
    $m = $master[$mname]
    foreach ($slot in (Get-AnimaliaClipPlan $animal $stem $mname)) {
      $r = $slot.Role
      $clipRole = $r -replace '_stand$', ''   # a gait's _stand twin takes the gait's measured values
      $tplName = $TEMPLATE_OF[$r]
      $c = $van[$tplName]
      Restore $c $snap[$tplName]
      $clipName = $slot.ClipName
      $c.Name = $clipName
      $c.Guid = [Guid]::NewGuid()
      $c.Animation = [Guid]$m.guid
      $c.Source1 = [float]1
      $c.Source2 = [float]($m.frames - 1)
      $c.Duration = [float]([math]::Round(($c.Source2 - $c.Source1 + 1) / $FPS, 3))
      $note = ''
      # PowerShell member access is case-insensitive, so this reaches the JSON's PascalCase keys
      # (Loco_Walk, Death_Stand_L, ...) directly off the lowercase pack stem; $null for any stem the
      # measure file has no entry for (every idle/eat/attack/hit stem), same as the old lookup table.
      $ms = $meas[$animal].$stem
      if ($clipRole -in @('walk', 'trot', 'canter', 'gallop', 'back')) {
        $why = Get-MeasureProblem $ms $clipRole
        if ($why) { $errors++; Write-Output ("BAD MEASURE {0}: {1}" -f $mname, $why); continue }
        $p = @($ms.plants_sorted)
        if ($clipRole -in @('canter', 'gallop')) { $c.StepPoints = V4 $p[0] -1 -1 -1 } else { $c.StepPoints = V4 $p[0] $p[1] $p[2] $p[3] }
        foreach ($u in $c.ClipUsages) { if ($u.GetType().Name -eq 'QuadMovementUsage') { $u.LoopDisplacement = [float]$ms.loop_displacement_m; $note = "loop=$($u.LoopDisplacement)m pace=$($u.PaceSwitchLimitMin)..$($u.PaceSwitchLimitMax)" } }
      } elseif ($clipRole -eq 'idle') {
        $c.SoundCode = ''; $c.StepPoints = V4 -1 -1 -1 -1
      } elseif ($clipRole -eq 'antler') {
        $c.CombatParameterId = ''; $c.SoundCode = ''; $c.StepPoints = V4 -1 -1 -1 -1
      } elseif ($clipRole -in @('fallL', 'fallR')) {
        $why = Get-MeasureProblem $ms $clipRole
        if ($why) { $errors++; Write-Output ("BAD MEASURE {0}: {1}" -f $mname, $why); continue }
        $s0 = $c.StepPoints; $c.StepPoints = V4 $s0.X $s0.Y $ms.fall_fraction $s0.W; $note = "fall=$($ms.fall_fraction)"
      }
      $flags = ($c.Flags | ForEach-Object { "$_" }) -join ','
      $rows += ("{0,-44} <- {1,-34} {2,-7} s=1..{3,-4} dur={4,-6} pri={5,-3} blend={6}/{7} step=<{8}> cp='{9}' snd='{10}' cont='{11}' {12} [{13}]" -f `
        $clipName, $tplName, $r, $c.Source2, $c.Duration, $c.Priority, $c.BlendInPeriod, $c.BlendOutPeriod, $c.StepPoints, $c.CombatParameterId, `
        ($c.SoundCode -replace '^event:/mission/', ''), $c.ContinueWithAction, $note, $flags)
      if ($Apply) {
        $out = Join-Path $m.dir ($clipName + '_anm.tpac')
        if (Test-Path $out) { $skipped++; continue }
        $c.TypelessDataSegments.Clear(); $c.UnknownDependences.Clear()
        $pkg = New-Object TpacTool.Lib.AssetPackage
        $pkg.Guid = [Guid]::NewGuid()
        $pkg.Items.Add($c)
        $pkg.Save($out, 2)
        $written++
        $chk = @((Load $out).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' })[0]
        if ($null -eq $chk -or $chk.Name -ne $clipName -or $chk.Animation -ne [Guid]$m.guid -or [int]$chk.Source2 -ne ($m.frames - 1)) {
          $errors++; Write-Output ("VERIFY FAIL {0}" -f $out)
        }
      }
    }
  }
}
$rows | ForEach-Object { Write-Output $_ }
Write-Output ("clips planned: {0}   MODE = {1}   written={2} skipped-existing={3} errors={4}" -f $rows.Count, $(if ($Apply) { 'APPLY' } else { 'DRY-RUN (no writes)' }), $written, $skipped, $errors)
if ($Apply) {
  # Every -Apply, not only one that wrote: a run that stopped after writing leaves zeroed checksums the next
  # run (which skips existing clips) must still repair. The fixer rewrites only stale items.
  foreach ($d in ($master.Values | ForEach-Object { $_.dir } | Sort-Object -Unique)) {
    & python "$PSScriptRoot\tpac_fix_item_checksums.py" $d --glob '*_anm.tpac' --apply | Select-Object -Last 1
    if ($LASTEXITCODE) { $errors++; Write-Output ("CHECKSUM FIXER FAILED (exit {0}) for {1}" -f $LASTEXITCODE, $d) }
  }
}
# Every -Apply re-checks every clip, not only a run that wrote one: a re-run over a tree whose clips all
# exist but went stale after a Kit re-import must not exit 0.
if ($Apply) {
  $post = Test-AnimaliaClips
  if ($post.stale + $post.orphan + $post.noMaster -gt 0) { $errors++ }
}
if ($errors -gt 0) { exit 1 } else { exit 0 }
