param(
  [switch]$Apply,
  [switch]$Verify,
  [string]$Masters = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\Race Test\dwarf\animations',
  [string]$Vanilla = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\AssetPackages\animation_clips.tpac',
  [string]$TpacBin = 'E:\Release_v0.5.1\bin',
  [string]$Loader = 'E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs',
  [string]$SkeletonGuid = '0099bfb2-09c4-4291-b225-3d24bdb14715'
)
<#
Write the two dwarf attack AnimationClips (#1087, 2026-11-06): dwarf_head_butt and dwarf_drop_kick.

The masters are the Kit imports of tools/blender/author_dwarf_attack_clips.py's FBX, in the Armory's
Assets/Race Test/dwarf/animations/ (dwarf_head_butt_geo.tpac, 37 frames; dwarf_drop_kick_geo.tpac, 55 frames; both on
dwarf_skeleton_a). Each clip goes beside its master as <name>_anm.tpac, named after its master as the spider's are.

Both clips are clones of vanilla kick_right_leg (the action type is actt_kick, as the kick's), changed to the war ram
head butt's recipe (docs/features/war-ram.md, "The head-butt clip"), which plays on its rig in battle:
  - Source1 = 1, Source2 = master Duration - 1 (frame 0 of a master is its REST frame), Duration = frames / 30 s.
  - Flags: the kick's five plus enforce_all. The ram showed nothing in battle on priority alone; enforce_all made it
    play. Both clips move the whole body, not only the legs the kick's enforce_lowerbody covers.
  - Priority 34 and blend in 0.2 (the kick's), blend out 0.2 (the kick's 0 hands over to its continue clip; ours end
    in the READY stance and hand back to locomotion).
  - ContinueWithAction blank: the kick's act_kick_right_leg_continue would play a human follow-through after ours.
    bannerlord-animation-clip-flags.md reports, unverified, that a cleared continue "ends on its last pose"; our last
    pose is the READY stance, and the ram clip has no continue either.
  - CombatParameterId blank: human_r_kick_params would run the engine's own kick collision; the DwarfAttacks task
    applies the damage, as the ram's does.
  - The kick's swing sound stays; StepPoints X (the sound point) moves to the frame the swing starts.
  - BlendWithAnimation, ClipSource1Name, ClipSource2Name blank and GeneratedIndex -1: kick_right_leg is unkeyed, and a
    kick is not a release or blocked action, so the melee attack table rule (gen_troll_anim_clips.ps1) does not apply.
The drop kick adds displace_position and a DisplacementUsage cloned from vanilla death_fall_front: vector
(0, 2.0, 0, 1), end progress 31/53 (frame 32 of 1..54), the travel author_dwarf_attack_clips.py keyed against
(travel_at: linear from frame 1). WARNING: displace_position without the usage is a native null read (CTD), so
Get-ClipFault fails a clip that has one without the other.

The master frame counts are pinned (ExpectFrames). The usage's end progress and the C# impact fractions are cut
for them, so a Kit reimport that changes a count is REFUSED, never followed.

Runs under Windows PowerShell 5.1 (TolerantTpacLoader.cs compiles against .NET Framework):
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_dwarf_attack_clips.ps1          # dry run
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_dwarf_attack_clips.ps1 -Apply   # write
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_dwarf_attack_clips.ps1 -Verify  # read-only gate
-Apply never overwrites an existing _anm.tpac (delete it on purpose first), re-reads what it wrote through Get-ClipFault
and fixes the item checksums with tpac_fix_item_checksums.py. A new package has no RuntimeDataCache entry until the
Kit saves it: open and save both clips in the Kit, then run
  python tools/check_rdc_entries.py --under "Race Test/dwarf"
-Verify exits 1 when a clip is missing, names another master or breaks any field below.
Exit 2 when it cannot run (-Apply while Bannerlord or the Modding Kit is open, -Apply with -Verify, not Windows
PowerShell 5.1, TpacTool.Lib.dll or the loader missing or unloadable). Exit 1 is only a bad master or clip.
Tests: tools/tests/test_gen_dwarf_attack_clips.py (Get-ClipWant, Get-ClipFault, Get-MasterFault and Assert-GameAndKitClosed, lifted out with the parser).
#>
$ErrorActionPreference = 'Stop'
$FPS = 30.0
$KICK_FLAGS = @('client_prediction', 'lock_movement', 'enable_hand_blend_ik', 'enforce_lowerbody', 'disable_foot_ik')
$SPEC = @(
  @{ Name = 'dwarf_head_butt'; ExpectFrames = 37; SoundFrame = 8; Displacement = $null },
  @{ Name = 'dwarf_drop_kick'; ExpectFrames = 55; SoundFrame = 11; Displacement = @{ Y = 2.0; EndFrame = 32 } }
)

function Assert-GameAndKitClosed {
  $procs = @(Get-Process -Name 'Bannerlord*', 'TaleWorlds.MountAndBlade*' -ErrorAction SilentlyContinue)
  if ($procs.Count -gt 0) {
    Write-Output ("REFUSED: Bannerlord or the Modding Kit is running (" + (($procs | ForEach-Object { $_.ProcessName }) -join ', ') + "); close it and re-run")
    exit 2
  }
}

# ---- pure rules, lifted out by tools/tests/test_gen_dwarf_attack_clips.py
# Every field a clip must carry, from its spec row and its master's GUID and frame count
function Get-ClipWant($Spec, [string]$MasterGuid, [int]$Frames) {
  $s1 = 1.0; $s2 = [double]($Frames - 1)
  $flags = @('client_prediction', 'lock_movement', 'enable_hand_blend_ik', 'enforce_lowerbody', 'disable_foot_ik', 'enforce_all')
  $disp = $null
  if ($null -ne $Spec.Displacement) {
    $flags += 'displace_position'
    $disp = @{ Y = [double]$Spec.Displacement.Y; End = ([double]$Spec.Displacement.EndFrame - $s1) / ($s2 - $s1) }
  }
  return @{
    Name = $Spec.Name; Animation = $MasterGuid; Source1 = $s1; Source2 = $s2
    Duration = [math]::Round(($s2 - $s1 + 1) / 30.0, 2); Priority = 34; BlendIn = 0.2; BlendOut = 0.2
    Flags = $flags; SoundCode = 'event:/mission/combat/swing/kick'
    SoundPoint = ([double]$Spec.SoundFrame - $s1) / ($s2 - $s1); Displacement = $disp
  }
}
# '' when a master is on the dwarf skeleton at its pinned frame count, else the refusal line
function Get-MasterFault($Anim, $Spec, [Guid]$Skel) {
  if ([Guid]$Anim.Skeleton -ne $Skel) { return ("SKELETON {0}: master on {1}, want {2}" -f $Spec.Name, $Anim.Skeleton, $Skel) }
  if ([int]$Anim.Duration -ne $Spec.ExpectFrames) { return ("FRAMES {0}: master has {1}, pinned {2}; a reimport changed the clip, re-cut the impact fractions and the end progress first" -f $Spec.Name, $Anim.Duration, $Spec.ExpectFrames) }
  return ''
}
# '' when a clip matches its want, else every field that is off, joined by '; '
function Get-ClipFault($Clip, $Want) {
  $f = @()
  function Off([double]$a, [double]$b) { return -not ([math]::Abs($a - $b) -le 0.001) }   # "not within": a NaN is off
  if ($Clip.Name -cne $Want.Name) { $f += ("Name '{0}'" -f $Clip.Name) }
  if ("$($Clip.Animation)" -ne $Want.Animation) { $f += ("Animation {0}, want master {1}" -f $Clip.Animation, $Want.Animation) }
  if ((Off $Clip.Source1 $Want.Source1) -or (Off $Clip.Source2 $Want.Source2)) { $f += ("Source {0}..{1}, want {2}..{3}" -f $Clip.Source1, $Clip.Source2, $Want.Source1, $Want.Source2) }
  if (Off $Clip.Duration $Want.Duration) { $f += ("Duration {0}, want {1}" -f $Clip.Duration, $Want.Duration) }
  if ([int]$Clip.Priority -ne $Want.Priority) { $f += ("Priority {0}, want {1}" -f $Clip.Priority, $Want.Priority) }
  if ((Off $Clip.BlendInPeriod $Want.BlendIn) -or (Off $Clip.BlendOutPeriod $Want.BlendOut)) { $f += ("Blends {0}/{1}, want {2}/{3}" -f $Clip.BlendInPeriod, $Clip.BlendOutPeriod, $Want.BlendIn, $Want.BlendOut) }
  $have = @($Clip.Flags | ForEach-Object { "$_" } | Sort-Object)
  $need = @($Want.Flags | Sort-Object)
  if (($have -join ',') -cne ($need -join ',')) { $f += ("Flags [{0}], want [{1}]" -f ($have -join ','), ($need -join ',')) }
  foreach ($k in @('ContinueWithAction', 'CombatParameterId', 'VoiceCode', 'FacialAnimationId', 'BlendWithAnimation', 'BlendsWithAction', 'ClipSource1Name', 'ClipSource2Name')) {
    if ("$($Clip.$k)" -ne '') { $f += ("{0} '{1}', want blank" -f $k, $Clip.$k) }
  }
  if ([int]$Clip.GeneratedIndex -ne -1) { $f += ("GeneratedIndex {0}, want -1" -f $Clip.GeneratedIndex) }
  if ($Clip.SoundCode -cne $Want.SoundCode) { $f += ("SoundCode '{0}'" -f $Clip.SoundCode) }
  if (Off $Clip.StepPoints.X $Want.SoundPoint) { $f += ("StepPoints X {0}, want {1}" -f $Clip.StepPoints.X, $Want.SoundPoint) }
  $usages = @($Clip.ClipUsages | Where-Object { $null -ne $_ })
  $du = @($usages | Where-Object { $_.GetType().Name -eq 'DisplacementUsage' })
  $hasFlag = $have -contains 'displace_position'
  # the engine reads the usage whenever the flag is set: one without the other is a native null read
  if ($hasFlag -ne ($du.Count -gt 0)) { $f += ("displace_position flag {0} but {1} DisplacementUsage" -f $(if ($hasFlag) { 'set' } else { 'clear' }), $du.Count) }
  if ($null -eq $Want.Displacement) {
    if ($usages.Count -gt 0) { $f += ("{0} clip usage(s), want none" -f $usages.Count) }
  } else {
    if ($usages.Count -ne 1 -or $du.Count -ne 1) { $f += ("{0} clip usage(s), want one DisplacementUsage" -f $usages.Count) }
    else {
      $v = $du[0].DisplacementVector
      if ((Off $v.X 0) -or (Off $v.Y $Want.Displacement.Y) -or (Off $v.Z 0) -or (Off $v.W 1)) { $f += ("DisplacementVector {0}, want <0, {1}, 0, 1>" -f $v, $Want.Displacement.Y) }
      if (Off $du[0].DisplacementEndProgress $Want.Displacement.End) { $f += ("DisplacementEndProgress {0}, want {1}" -f $du[0].DisplacementEndProgress, $Want.Displacement.End) }
    }
  }
  return ($f -join '; ')
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
try {
  Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
  Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
} catch { Write-Output ("ERROR: cannot load the tpac libraries: {0}" -f $_.Exception.Message); exit 2 }
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
function Get-Clip($path) { (Load $path).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' } | Select-Object -First 1 }

# ---- masters: each spec row needs its master, on the dwarf skeleton, at the pinned frame count
$SKEL = [Guid]$SkeletonGuid
$masterOf = @{}
$bad = 0
foreach ($s in $SPEC) {
  $file = Join-Path $Masters ($s.Name + '_geo.tpac')
  if (-not (Test-Path $file)) { $bad++; Write-Output ("NO MASTER {0}: {1} is missing" -f $s.Name, $file); continue }
  $sa = (Load $file).Package.Items | Where-Object { $_.GetType().Name -eq 'SkeletalAnimation' -and $_.Name -ceq $s.Name } | Select-Object -First 1
  if ($null -eq $sa) { $bad++; Write-Output ("NO MASTER {0}: {1} holds no SkeletalAnimation of that name" -f $s.Name, $file); continue }
  $mWhy = Get-MasterFault $sa $s $SKEL
  if ($mWhy) { $bad++; Write-Output $mWhy; continue }
  $masterOf[$s.Name] = @{ guid = "$($sa.Guid)"; frames = [int]$sa.Duration }
}
if ($bad -gt 0) { Write-Output ("REFUSED: {0} master problem(s)" -f $bad); exit 1 }

# ---- -Verify: read-only
if ($Verify) {
  $ok = 0; $fail = 0
  foreach ($s in $SPEC) {
    $out = Join-Path $Masters ($s.Name + '_anm.tpac')
    if (-not (Test-Path $out)) { $fail++; Write-Output ("MISSING {0}" -f $out); continue }
    $cl = Get-Clip $out
    $why = if ($null -eq $cl) { 'no AnimationClip in the package' } else { Get-ClipFault $cl (Get-ClipWant $s $masterOf[$s.Name].guid $masterOf[$s.Name].frames) }
    if ($why) { $fail++; Write-Output ("BAD    {0}: {1}" -f $s.Name, $why) } else { $ok++; Write-Output ("OK     {0}" -f $s.Name) }
  }
  Write-Output ("verify: clips ok={0} bad={1} of {2}" -f $ok, $fail, $SPEC.Count)
  if ($fail -gt 0) { exit 1 } else { exit 0 }
}

# ---- templates
$van = Load $Vanilla
$kick = $null; $deathDisp = $null
foreach ($it in $van.Package.Items) {
  if ($it.GetType().Name -ne 'AnimationClip') { continue }
  if ($it.Name -ceq 'kick_right_leg') { $kick = $it }
  if ($it.Name -ceq 'death_fall_front') { $deathDisp = @($it.ClipUsages | Where-Object { $_.GetType().Name -eq 'DisplacementUsage' }) | Select-Object -First 1 }
}
if ($null -eq $kick -or $null -eq $deathDisp) { Write-Output "REFUSED: vanilla kick_right_leg or death_fall_front's DisplacementUsage not found"; exit 1 }
$kickFlags = @($kick.Flags | ForEach-Object { "$_" } | Sort-Object) -join ','
if ($kickFlags -cne (@($KICK_FLAGS | Sort-Object) -join ',')) { Write-Output ("REFUSED: kick_right_leg's flags are now [{0}]; the engine changed the template, review the recipe" -f $kickFlags); exit 1 }

function Set-Flags($clip, [string[]]$names) {
  $list = $clip.Flags
  $elem = $list.GetType().GetGenericArguments()[0]
  $list.Clear()
  foreach ($n in $names) { if ($elem -eq [string]) { $list.Add($n) } else { $list.Add([Enum]::Parse($elem, $n)) } }
}

# ---- write. The one kick template is set from scratch for each clip and saved before the next (gen_troll_anim_clips.ps1's pattern)
$written = 0; $skipped = 0; $fail = 0
foreach ($s in $SPEC) {
  $m = $masterOf[$s.Name]
  $w = Get-ClipWant $s $m.guid $m.frames
  $out = Join-Path $Masters ($s.Name + '_anm.tpac')
  $c = $kick
  $c.Name = $w.Name; $c.Guid = [Guid]::NewGuid(); $c.Animation = [Guid]$w.Animation
  $c.Source1 = [float]$w.Source1; $c.Source2 = [float]$w.Source2; $c.Duration = [float]$w.Duration
  $c.Priority = $w.Priority; $c.BlendInPeriod = [float]$w.BlendIn; $c.BlendOutPeriod = [float]$w.BlendOut
  Set-Flags $c $w.Flags
  $c.ContinueWithAction = ''; $c.CombatParameterId = ''; $c.VoiceCode = ''; $c.FacialAnimationId = ''
  $c.BlendWithAnimation = ''; $c.BlendsWithAction = ''; $c.ClipSource1Name = ''; $c.ClipSource2Name = ''; $c.GeneratedIndex = [sbyte]-1
  $c.SoundCode = $w.SoundCode
  $c.StepPoints = New-Object System.Numerics.Vector4 -ArgumentList @([float]$w.SoundPoint, [float]-1, [float]-1, [float]-1)
  $c.ClipUsages.Clear()
  if ($null -ne $w.Displacement) {
    $deathDisp.DisplacementVector = New-Object System.Numerics.Vector4 -ArgumentList @([float]0, [float]$w.Displacement.Y, [float]0, [float]1)
    $deathDisp.DisplacementEndProgress = [float]$w.Displacement.End
    $c.ClipUsages.Add($deathDisp)
  }
  $pre = Get-ClipFault $c $w
  if ($pre) { $fail++; Write-Output ("BUILD FAULT {0}: {1}" -f $s.Name, $pre); continue }
  Write-Output ("{0,-16} <- master {1} s={2}..{3} dur={4} pri={5} blend={6}/{7} sound@{8:0.###} flags=[{9}]{10}" -f $w.Name, $w.Animation, $w.Source1, $w.Source2, $w.Duration, $w.Priority, $w.BlendIn, $w.BlendOut, $w.SoundPoint, ($w.Flags -join ','), $(if ($w.Displacement) { (' disp=<0,{0},0,1> end={1:0.####}' -f $w.Displacement.Y, $w.Displacement.End) } else { '' }))
  if (-not $Apply) { continue }
  if (Test-Path $out) { $skipped++; Write-Output ("SKIP {0}: exists (delete it on purpose to re-cut)" -f $out); continue }
  $c.TypelessDataSegments.Clear(); $c.UnknownDependences.Clear()
  $pkg = New-Object TpacTool.Lib.AssetPackage
  $pkg.Guid = [Guid]::NewGuid()
  $pkg.Items.Add($c)
  $pkg.Save($out, $false, 2)   # path, useOriginalChecksum, tpac version
  $written++
}
Write-Output ("clips: {0}   MODE = {1}   written={2} skipped-existing={3} faults={4}" -f $SPEC.Count, $(if ($Apply) { 'APPLY' } else { 'DRY-RUN (no writes)' }), $written, $skipped, $fail)
if ($Apply) {
  if ($written -gt 0) {
    $py = Get-Command python -ErrorAction SilentlyContinue
    if (-not $py) { $fail++; Write-Output "python not on PATH: run  python tools/tpac_fix_item_checksums.py <masters dir> --glob dwarf_*_anm.tpac --apply" }
    else {
      & $py.Source "$PSScriptRoot\tpac_fix_item_checksums.py" $Masters --glob 'dwarf_*_anm.tpac' --apply | Select-Object -Last 1 | ForEach-Object { Write-Output $_ }
      if ($LASTEXITCODE -ne 0) { $fail++; Write-Output ("CHECKSUM FIX FAILED: exit {0}" -f $LASTEXITCODE) }
    }
  }
  # the re-read runs on every -Apply, so a re-run over clips written earlier still checks them
  foreach ($s in $SPEC) {
    $outFile = Join-Path $Masters ($s.Name + '_anm.tpac')
    $cl = if (Test-Path $outFile) { Get-Clip $outFile } else { $null }
    $why = if ($null -eq $cl) { 'no AnimationClip after the write' } else { Get-ClipFault $cl (Get-ClipWant $s $masterOf[$s.Name].guid $masterOf[$s.Name].frames) }
    if ($why) { $fail++; Write-Output ("REREAD FAULT {0}: {1}" -f $s.Name, $why) } else { Write-Output ("reread OK {0}" -f $s.Name) }
  }
  if ($written -gt 0) { Write-Output "Next: open and save both clips in the Kit, then: python tools/check_rdc_entries.py --under `"Race Test/dwarf`"" }
}
if ($fail -gt 0) { exit 1 } else { exit 0 }
