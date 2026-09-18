param(
  [switch]$Apply,
  [switch]$Verify,
  [string]$Masters = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\creature\troll\animations',
  [string]$Names = "$PSScriptRoot\blender\fab_cave_troll_clip_names.json",
  [string]$Measure = "$PSScriptRoot\blender\fab_cave_troll_clip_measure.json",
  [string]$Vanilla = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\AssetPackages\animation_clips.tpac',
  [string]$TpacBin = 'E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin',
  [string]$Loader = 'E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs'
)
<#
Author the anim_troll_* AnimationClip tpacs for the Fab cave troll set (2026-09-17).

One <clip>_anm.tpac per compiled SkeletalAnimation master in $Masters, written beside the master
(the spider / warg layout). Every clip is a CLONE of the vanilla human clip of the same type from
animation_clips.tpac, so flag lists, blend periods, priorities and usage shapes are vanilla's
verbatim; only these change per clip: Name, Guid (fresh), Animation (master GUID), Source1 = 1,
Source2 = master Duration - 1 (vanilla: walk master 38 -> clip 1..37; frame 0 of a master is its REST frame, so
this is the whole clip), Duration = span / 30 s, hand poses 3/3 (weapon-agnostic, what
vanilla uses on every clip that serves several weapon codes), and the measured values:
  - BipMovIkUsage.LoopDisplacement = the source clip's root travel per loop, human scale
  - DisplacementUsage.DisplacementVector = (0, forward travel, 0) on deaths
  - StepPoints = the two foot-plant times on locomotion clips
Measurements come from tools/blender/fab_cave_troll_clip_measure.json (Blender probe of the Fab
sources; the retarget dropped root motion, so these carry it back as clip metadata, which is how
vanilla's own in-place clips work).

Templates (vanilla clip -> troll clip types):
  walk_forward_unarmed  -> walk, walk_turn, idle_to_walk, walk_to_idle (locomotion, make_walk_sound + bip_mov_ik)
  run_forward_unarmed   -> run, idle_to_run, run_to_idle, walk_to_run, run_to_heavy_attack
  turn_unarmed          -> the 8 stationary turns (no flags, bip_mov_ik; the engine rotates the agent)
  troop_stand_unarmed_1 -> idles (priority 1, allow_head_movement)
  strike_chest_front    -> the 8 hit reactions (priority 80, restart + root rotation + bounding volume)
  death_fall_front      -> deaths (priority 95, the full fall flag set, blend + displacement usages)
  taunt_afraid + flags  -> attacks: vanilla has no standalone melee clip (melee is engine pose-blend), so the
                           emote base gets client_prediction + lock_movement + enforce_all at priority 60
  taunt_afraid          -> interactive / emotes and the stance transitions (priority 64, lock_movement)

Runs under Windows PowerShell 5.1 (TolerantTpacLoader.cs compiles against .NET Framework):
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_troll_anim_clips.ps1          # dry run
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_troll_anim_clips.ps1 -Apply   # write
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_troll_anim_clips.ps1 -Verify  # read-only gate
-Verify checks the EXISTING clips against the masters on disk and exits 1 on any ORPHAN (the clip's
Animation GUID names no master) or STALE (Source2 != master Duration - 1) row. Run it after every Kit
reimport of the FBX. Measured 2026-09-18: a Kit reimport KEEPS the SkeletalAnimation GUID (51 of 52), so
the clips survive and only Source2 has to follow the new Duration; but a package the Kit had also given a
Skeleton item (human_skeleton_notused.001, created from that same FBX on the first import) came back as
Skeleton + Geometry with NO animation, and its clip reported 'assigned skeleton animation not found' in the
Kit. Such a file is listed as NO ANIMATION; fix it in the Kit (delete the junk skeleton, import the FBX
again as an animation), then delete the clips and -Apply.
Skeleton wiring: a master the Kit left with an EMPTY Skeleton GUID is patched IN PLACE (16 bytes: the
zero GUID that sits right before BoneNum=28 + Duration in the metadata) to human_skeleton
dd7f3586-10ea-47d5-880e-a0c263862217, with a .bak-preskel copy first. TpacTool's Save is NOT used on
masters: its re-serialisation zeroes the two 8-byte post-meta checksum fields (16 bytes differ), and
even though the engine is known not to validate them the patch keeps the file otherwise identical.
Checksums: TpacTool.Lib's Save writes a ZERO item checksum (the warg's 73, the spider's 24 and the
chariot's 24 shipped clips all carry zeros and load), and the in-place skeleton patch leaves the
master's checksum stale. -Apply ends by running tools/tpac_fix_item_checksums.py --apply over the
folder (xxHash64 over the metadata, the Kit's own formula), so every file matches Kit output.
Dry run by default. -Apply never overwrites an existing _anm.tpac (delete it first, on purpose).
#>
$ErrorActionPreference = 'Stop'
Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
$EMPTY = '00000000-0000-0000-0000-000000000000'
$HUMAN = [Guid]'dd7f3586-10ea-47d5-880e-a0c263862217'
$FPS = 30.0

# ---- inputs
$nameMap = @{}
(Get-Content $Names -Raw | ConvertFrom-Json).PSObject.Properties | Where-Object { $_.Name -notlike '_*' } | ForEach-Object { $nameMap[$_.Name] = $_.Value }
$measTable = @{}
(Get-Content $Measure -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $measTable[$_.Name] = $_.Value }

# ---- masters
$masterMap = @{}
$noAnim = 0
foreach ($f in [IO.Directory]::GetFiles($Masters, '*_geo.tpac')) {
  $r = Load $f
  $found = $false
  foreach ($it in $r.Package.Items) {
    if ($it.GetType().Name -eq 'SkeletalAnimation') {
      $found = $true
      $masterMap[$it.Name] = @{ guid = $it.Guid; frames = [int]$it.Duration; skel = "$($it.Skeleton)"; file = $f }
    }
  }
  if (-not $found) {
    # a reimport can leave Skeleton + Geometry and no animation (see header); the Kit then cannot resolve the clip
    $noAnim++
    Write-Output ("NO ANIMATION in {0}: items = {1}" -f [IO.Path]::GetFileName($f), (@($r.Package.Items | ForEach-Object { $_.GetType().Name + ' ' + $_.Name }) -join ', '))
  }
}
Write-Output ("masters: {0}   skeleton-empty: {1}   packages without an animation: {2}" -f $masterMap.Count, (@($masterMap.Values | Where-Object { $_.skel -eq $EMPTY })).Count, $noAnim)

# ---- -Verify: read-only check of the clips already on disk, then exit
if ($Verify) {
  $byGuid = @{}
  foreach ($m in $masterMap.Values) { $byGuid["$($m.guid)"] = $m }
  $ok = 0; $stale = 0; $orphan = 0
  foreach ($f in ([IO.Directory]::GetFiles($Masters, 'anim_troll_*_anm.tpac') | Sort-Object)) {
    $cl = (Load $f).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' } | Select-Object -First 1
    $key = "$($cl.Animation)"
    if ($null -eq $cl -or -not $byGuid.ContainsKey($key)) { $orphan++; Write-Output ("ORPHAN {0,-40} -> master GUID {1} not on disk" -f [IO.Path]::GetFileName($f), $key); continue }
    $m = $byGuid[$key]
    if ([int]$cl.Source2 -ne $m.frames - 1) { $stale++; Write-Output ("STALE  {0,-40} -> Source2={1}, master Duration={2} (want {3})" -f $cl.Name, $cl.Source2, $m.frames, ($m.frames - 1)) }
    else { $ok++ }
  }
  Write-Output ("verify: clips ok={0} stale={1} orphan={2}   masters without a clip={3}" -f $ok, $stale, $orphan, ($masterMap.Count - $ok - $stale))
  if ($stale + $orphan + $noAnim -gt 0) { exit 1 } else { exit 0 }
}

# ---- skeleton wiring (in-place 16-byte patch, see header)
function Find-SkeletonField([byte[]]$bytes, [int]$frames) {
  # the empty Skeleton GUID: 16 zero bytes followed by BoneNum (28) and Duration (frames), both int32 LE
  $tail = [byte[]](@(0) * 16) + [BitConverter]::GetBytes([int]28) + [BitConverter]::GetBytes([int]$frames)
  $hits = @()
  for ($i = 0; $i -le $bytes.Length - $tail.Length; $i++) {
    $ok = $true
    for ($j = 0; $j -lt $tail.Length; $j++) { if ($bytes[$i + $j] -ne $tail[$j]) { $ok = $false; break } }
    if ($ok) { $hits += $i }
  }
  return $hits
}
$wired = 0; $wireFail = 0
foreach ($mname in ($masterMap.Keys | Sort-Object)) {
  $m = $masterMap[$mname]
  if ($m.skel -ne $EMPTY) { continue }
  $bytes = [IO.File]::ReadAllBytes($m.file)
  $hits = Find-SkeletonField $bytes $m.frames
  if ($hits.Count -ne 1) { $wireFail++; Write-Output ("WIRE FAIL {0}: {1} candidate offsets" -f $mname, $hits.Count); continue }
  if ($Apply) {
    $bak = $m.file + '.bak-preskel'
    if (-not (Test-Path $bak)) { Copy-Item $m.file $bak }
    [Array]::Copy($HUMAN.ToByteArray(), 0, $bytes, $hits[0], 16)
    [IO.File]::WriteAllBytes($m.file, $bytes)
    $chk = Load $m.file
    $sa = $chk.Package.Items | Where-Object { $_.GetType().Name -eq 'SkeletalAnimation' } | Select-Object -First 1
    if ($null -eq $sa -or $sa.Skeleton -ne $HUMAN -or $sa.Duration -ne $m.frames -or $sa.Name -ne $mname) {
      $wireFail++; Write-Output ("WIRE VERIFY FAIL {0}: skel={1} dur={2}" -f $mname, $sa.Skeleton, $sa.Duration); continue
    }
    $m.skel = "$HUMAN"
  }
  $wired++
}
Write-Output ("skeleton wiring: {0} masters {1}, {2} failed" -f $wired, $(if ($Apply) { 'patched + re-read OK' } else { 'would be patched (offset found)' }), $wireFail)

# ---- vanilla templates
$van = Load $Vanilla
$vclips = @{}
foreach ($it in $van.Package.Items) { if ($it.GetType().Name -eq 'AnimationClip') { $vclips[$it.Name] = $it } }
$TEMPLATE = @{
  walk = 'walk_forward_unarmed'; run = 'run_forward_unarmed'; turn = 'turn_unarmed'; idle = 'troop_stand_unarmed_1'
  hit = 'strike_chest_front'; death = 'death_fall_front'; attack = 'taunt_afraid'; emote = 'taunt_afraid'
}
foreach ($k in @($TEMPLATE.Keys)) {
  if (-not $vclips.ContainsKey($TEMPLATE[$k])) {
    $alt = $vclips.Keys | Where-Object { $_ -like ($TEMPLATE[$k].Split('_')[0] + '*') } | Sort-Object | Select-Object -First 1
    Write-Output ("TEMPLATE MISSING {0} -> {1}; nearest: {2}" -f $k, $TEMPLATE[$k], $alt)
  }
}

function TypeOf($clip) {
  $n = $clip -replace '^anim_troll_', ''
  if ($n -match 'death') { return 'death' }
  if ($n -match 'hit_') { return 'hit' }
  if ($n -match 'attack') { return 'attack' }
  if ($n -match 'interactive') { return 'emote' }
  if ($n -match '^(combat_)?idle\d$') { return 'idle' }
  if ($n -match 'walk_turn') { return 'walk' }
  if ($n -match 'turn_') { return 'turn' }
  if ($n -match 'run') { return 'run' }
  if ($n -match 'walk') { return 'walk' }
  return 'emote'   # idle_to_combat, combat_to_idle: stance transitions, one-shot, lock_movement
}

function StepPoints($m) {
  # two foot plants per loop: take each foot's first non-zero plant, fall back to vanilla's 0.4 / 0.9
  $l = @($m.plants_l | Where-Object { $_ -gt 0 }); $r = @($m.plants_r | Where-Object { $_ -gt 0 })
  $a = if ($l.Count) { [float]$l[0] } else { 0.4 }
  $b = if ($r.Count) { [float]$r[0] } else { 0.9 }
  if ($a -gt $b) { $t = $a; $a = $b; $b = $t }
  return New-Object System.Numerics.Vector4 -ArgumentList @([float]$a, [float]$b, [float]-1, [float]-1)
}

function Set-Flags($clip, [string[]]$names) {
  $list = $clip.Flags
  $elem = $list.GetType().GetGenericArguments()[0]
  $list.Clear()
  foreach ($n in $names) {
    if ($elem -eq [string]) { $list.Add($n) } else { $list.Add([Enum]::Parse($elem, $n)) }
  }
}
$rows = @(); $written = 0; $skipped = 0
foreach ($mname in ($masterMap.Keys | Sort-Object)) {
  $m = $masterMap[$mname]
  $stem = 'cave_' + $mname            # troll_free_idle_0 -> cave_troll_free_idle_0
  if (-not $nameMap.ContainsKey($stem)) { Write-Output ("NO NAME for master {0}" -f $mname); continue }
  $clipName = $nameMap[$stem]
  $type = TypeOf $clipName
  $tpl = $vclips[$TEMPLATE[$type]]
  if ($null -eq $tpl) { Write-Output ("NO TEMPLATE for {0} ({1})" -f $clipName, $type); continue }
  $meas = $measTable[$stem]
  $out = Join-Path $Masters ($clipName + '_anm.tpac')

  # clone by reloading is expensive; mutate the shared template and save immediately (gen.ps1 pattern)
  $c = $tpl
  $c.Name = $clipName
  $c.Guid = [Guid]::NewGuid()
  $c.Animation = [Guid]$m.guid
  $c.Source1 = [float]1
  $c.Source2 = [float]($m.frames - 1)
  $c.Duration = [float]([math]::Round(($c.Source2 - $c.Source1 + 1) / $FPS, 2))
  $c.LeftHandPose = 3; $c.RightHandPose = 3
  if ($type -eq 'attack') { Set-Flags $c @('client_prediction', 'lock_movement', 'enforce_all'); $c.Priority = 60; $c.BlendInPeriod = [float]0.3; $c.BlendOutPeriod = [float]0.3 }
  elseif ($type -eq 'emote') { Set-Flags $c @('lock_movement'); $c.Priority = 64; $c.BlendInPeriod = [float]0.3; $c.BlendOutPeriod = [float]0.3 }
  $c.ClipSource1Name = ''; $c.ClipSource2Name = ''; $c.UnknownClipName = ''
  # template strings that belong to the human, not the troll: facial ids (no facial rig on the troll
  # head), the emote's 'Fear' voice line and 'afraid' foley, the idle's soldier-armour foley
  $c.FacialAnimationId = ''; $c.VoiceCode = ''; $c.SoundCode = ''
  if ($type -eq 'hit') {
    # vanilla strike clips carry the hit direction as CombatParameterId (strike_front/back/left/right)
    $dir = ([regex]::Match($clipName, 'hit_(front|back|left|right)')).Groups[1].Value
    $c.CombatParameterId = $(if ($dir) { 'strike_' + $dir } else { 'strike_front' })
  } else { $c.CombatParameterId = '' }
  # a StepPoints entry on a clip without a SoundCode is a sound trigger with no sound: the Kit warns
  # "Sound points and/or sound id not valid". Only locomotion (footsteps under make_walk_sound) and
  # deaths (the fall point under make_bodyfall_sound) keep theirs, as vanilla does; the rest get none.
  if ($type -notin @('walk', 'run', 'death')) { $c.StepPoints = New-Object System.Numerics.Vector4 -ArgumentList @([float]-1, [float]-1, [float]-1, [float]-1) }
  $usageNote = ''
  if ($type -in @('walk', 'run', 'turn')) {
    foreach ($u in $c.ClipUsages) {
      if ($u.GetType().Name -eq 'BipMovIkUsage' -and $type -ne 'turn') {
        $u.LoopDisplacement = [float]$meas.travel_forward_human_m
        $usageNote = ('loop={0}' -f $u.LoopDisplacement)
      }
    }
    if ($type -ne 'turn' -and $meas) { $c.StepPoints = StepPoints $meas }
  }
  elseif ($type -eq 'death') {
    foreach ($u in $c.ClipUsages) {
      if ($u.GetType().Name -eq 'DisplacementUsage') {
        $u.DisplacementVector = New-Object System.Numerics.Vector3 -ArgumentList @([float]0, [float]$meas.travel_forward_human_m, [float]0)
        $usageNote = ('disp={0}' -f $u.DisplacementVector)
      }
    }
  }
  $flags = ($c.Flags | ForEach-Object { "$_" }) -join ','
  $rows += ("{0,-38} <- {1,-40} s2={2,-4} dur={3,-5} type={4,-6} pri={5,-3} step=<{6}> {7} cp='{9}' flags=[{8}]" -f $clipName, $mname, $c.Source2, $c.Duration, $type, $c.Priority, $c.StepPoints, $usageNote, $flags, $c.CombatParameterId)
  if ($Apply) {
    if (Test-Path $out) { $skipped++; continue }
    $c.TypelessDataSegments.Clear(); $c.UnknownDependences.Clear()
    $pkg = New-Object TpacTool.Lib.AssetPackage
    $pkg.Guid = [Guid]::NewGuid()
    $pkg.Items.Add($c)
    $pkg.Save($out, 2)
    $written++
  }
}
$rows | ForEach-Object { Write-Output $_ }
Write-Output ("clips planned: {0}   MODE = {1}   written={2} skipped-existing={3}" -f $rows.Count, $(if ($Apply) { 'APPLY' } else { 'DRY-RUN (no writes)' }), $written, $skipped)

# ---- verify what was written
if ($Apply) {
  $bad = 0
  foreach ($f in [IO.Directory]::GetFiles($Masters, 'anim_troll_*_anm.tpac')) {
    $r = Load $f; $cl = $r.Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' } | Select-Object -First 1
    if ($null -eq $cl -or -not $masterMap.Values.guid.Contains($cl.Animation)) { $bad++; Write-Output ("VERIFY FAIL {0}" -f $f) }
  }
  Write-Output ("verify: {0} files re-read, {1} bad" -f ([IO.Directory]::GetFiles($Masters, 'anim_troll_*_anm.tpac')).Count, $bad)
  $py = Get-Command python -ErrorAction SilentlyContinue
  if ($py) { & $py.Source "$PSScriptRoot\tpac_fix_item_checksums.py" $Masters --glob '*.tpac' --apply | Select-Object -Last 1 }
  else { Write-Output "python not on PATH: run  python tools/tpac_fix_item_checksums.py <masters dir> --glob *.tpac --apply" }
}
