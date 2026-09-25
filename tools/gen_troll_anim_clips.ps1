param(
  [switch]$Apply,
  [switch]$Verify,
  [string]$Masters = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\creature\troll\animations',
  [string]$Names = "$PSScriptRoot\blender\fab_cave_troll_clip_names.json",
  [string]$Measure = "$PSScriptRoot\blender\fab_cave_troll_clip_measure.json",
  [string]$Vanilla = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\AssetPackages\animation_clips.tpac',
  [string]$TpacBin = 'E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin',
  [string]$Loader = 'E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs',
  [string]$SkeletonGuid = 'dd7f3586-10ea-47d5-880e-a0c263862217',
  [string]$ClipPrefix = 'anim_troll_',
  [double]$TravelScale = 0,
  [switch]$CloneByName,
  [string]$ClipsIndex = '',
  [string]$Renames = '',
  # the retarget run's retarget_report.json: -TravelScale is read from its one pelvis_scale value
  [string]$RetargetReport = ''
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
Another skeleton (2026-09-24, the hill troll on troll_skeleton_a): -SkeletonGuid is the rig EMPTY masters are wired
to and checked against (default human_skeleton), -ClipPrefix the clip names' prefix (default anim_troll_), and
-TravelScale, when above 0, sizes LoopDisplacement and the death displacement as the Fab troll's own travel
(travel_forward_troll_m) times that scale instead of the human-scale value. It must be the retarget's pelvis_scale
(retarget_report.json), the factor the stride was scaled by: the planted foot slides back by exactly that multiple of
the Fab's, so any other value skates the feet. For the hill troll that is the thigh + calf length ratio, 1.5578
(2026-09-24; the pelvis height ratio 1.377 the first run used was 12% short). A master the Kit named after its clip
(a retarget run with --name-map) keeps that name for its clip, as the spider's do, and finds its measurements
through the name map read backwards.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_troll_anim_clips.ps1 `
    -Masters '<Armory>\Assets\Race Test\Mordor\Trolls\animations' -Names tools\blender\fab_hill_troll_clip_names.json `
    -SkeletonGuid 7516b03c-1c28-4b4a-87ab-8df6f047bf9c -ClipPrefix anim_hill_troll_ -TravelScale 1.5578 [-Apply]
Human clips retargeted onto a custom skeleton (2026-09-24, the hill troll's melee set): -CloneByName with -ClipsIndex
<clips_index.json from read_anim_keyframes_tpac.ps1 -ByClip>. The master for vanilla master `anim_X` (or `X`) is
`<ClipPrefix>X`; every clip in the index becomes `<ClipPrefix><clip>_anm.tpac`, a copy of its own vanilla clip with the
master GUID re-pointed, Source1/Source2 + 1 (our rest frame 0), the facial id cleared and displacements times
-TravelScale (the retarget report's pelvis_scale). Several clips share a master, as in vanilla. -Verify then checks
the clips against the index (range and master), not the whole-master rule. A clip's Name is a fixed-size(64) engine
string, 63 usable characters (the Kit warned on 15 hill troll clips, 2026-09-25): -Renames <json> maps the vanilla
clip names that would run over to shorter troll names (tools/blender/hill_troll_clip_renames.json, shared with
bind_hill_troll_action_set.py --renames); a name still over 63 is refused, never truncated. A clip whose vanilla range
runs past its master (the Kit's frame count; aserai_mp_guard_idle_2hperk, 2026-09-25) is REFUSED BY DESIGN: listed,
never written, and not counted as missing by -Apply or -Verify.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_troll_anim_clips.ps1 -CloneByName `
    -Masters '<Armory>\Assets\Race Test\Mordor\Trolls\animations' -ClipsIndex <human_json>\clips_index.json `
    -Renames tools\blender\hill_troll_clip_renames.json -RetargetReport <retarget out>\retarget_report.json `
    -SkeletonGuid 7516b03c-1c28-4b4a-87ab-8df6f047bf9c -ClipPrefix anim_hill_troll_ [-Apply|-Verify]
-RetargetReport sets -TravelScale from the report's pelvis_scale and refuses a report holding more than one value or a
typed -TravelScale that disagrees (the first Fab run's hand-typed 1.377 skated the feet by 12%).
Every mode refuses a folder whose non-empty masters name a skeleton other than -SkeletonGuid: BoneNum cannot tell the
28-bone troll_skeleton_a from human_skeleton, so the default GUID would otherwise wire a re-imported troll master to
the human rig. That guard cannot see an EMPTY master (a fresh Kit import), so: the Fab path wires EMPTY masters in a
folder other than the default only when -SkeletonGuid is passed explicitly, and -CloneByName (which never wires)
lists each EMPTY master the index needs as UNWIRED, refuses to write, and fails -Verify; run
wire_anim_master_skeletons.ps1 -SkeletonGuid <rig> -BoneNum 28 first. After -Apply the written clips are re-read and the item checksums fixed; any failure there, a wiring
failure or python missing exits 1.
#>
$ErrorActionPreference = 'Stop'
Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
$EMPTY = '00000000-0000-0000-0000-000000000000'
$SKEL = [Guid]$SkeletonGuid
$FPS = 30.0

if ($RetargetReport) {
  $scales = New-Object 'System.Collections.Generic.HashSet[double]'   # doubles, not strings: a de-DE '1,8504' parses as 18504
  function Find-PelvisScale($o) {
    if ($o -is [System.Management.Automation.PSCustomObject]) {
      foreach ($p in $o.PSObject.Properties) {
        if ($p.Name -eq 'pelvis_scale') { [void]$scales.Add([math]::Round([double]$p.Value, 4)) } else { Find-PelvisScale $p.Value }
      }
    } elseif ($o -is [System.Collections.IEnumerable] -and $o -isnot [string]) { foreach ($x in $o) { Find-PelvisScale $x } }
  }
  Find-PelvisScale (Get-Content $RetargetReport -Raw | ConvertFrom-Json)
  if ($scales.Count -ne 1) { throw ("-RetargetReport {0} holds {1} pelvis_scale values ({2}); want exactly one" -f $RetargetReport, $scales.Count, (@($scales) -join ', ')) }
  $fromReport = @($scales)[0]
  if ($TravelScale -gt 0 -and [math]::Abs($TravelScale - $fromReport) -gt 0.001) { throw ("-TravelScale {0} disagrees with the report's pelvis_scale {1}" -f $TravelScale, $fromReport) }
  $TravelScale = $fromReport
  Write-Output ("travel scale {0} from {1}" -f $TravelScale, $RetargetReport)
}

# Re-read every clip this prefix owns in the folder (each must hold an AnimationClip whose master is on disk), then fix
# the item checksums TpacTool's Save leaves at zero. Messages go to the host so the return value is the failure count.
function Confirm-WrittenClips {
  $guids = New-Object 'System.Collections.Generic.HashSet[guid]'
  foreach ($mm in $masterMap.Values) { [void]$guids.Add([guid]$mm.guid) }
  $files = [IO.Directory]::GetFiles($Masters, ($ClipPrefix + '*_anm.tpac'))
  $fail = 0
  foreach ($f in $files) {
    $cl = (Load $f).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' } | Select-Object -First 1
    if ($null -eq $cl -or -not $guids.Contains([guid]$cl.Animation)) { $fail++; Write-Host ("VERIFY FAIL {0}" -f $f) }
  }
  Write-Host ("verify: {0} files re-read, {1} bad" -f $files.Count, $fail)
  $py = Get-Command python -ErrorAction SilentlyContinue
  if ($py) {
    & $py.Source "$PSScriptRoot\tpac_fix_item_checksums.py" $Masters --glob '*.tpac' --apply | Select-Object -Last 1 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { $fail++; Write-Host ("CHECKSUM FIX FAILED: tpac_fix_item_checksums.py exited {0}" -f $LASTEXITCODE) }
  } else {
    $fail++; Write-Host "python not on PATH: run  python tools/tpac_fix_item_checksums.py <masters dir> --glob *.tpac --apply"
  }
  return $fail
}

# ---- inputs
$nameMap = @{}
(Get-Content $Names -Raw | ConvertFrom-Json).PSObject.Properties | Where-Object { $_.Name -notlike '_*' } | ForEach-Object { $nameMap[$_.Name] = $_.Value }
$measTable = @{}
$clipStem = @{}   # clip name -> source stem, for masters the Kit named after their clip
foreach ($k in $nameMap.Keys) { $clipStem[$nameMap[$k]] = $k }
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
# the rig check BoneNum cannot make: every wired master here must already be on -SkeletonGuid
$foreign = @($masterMap.Values | Where-Object { $_.skel -ne $EMPTY -and ([guid]$_.skel) -ne $SKEL } | ForEach-Object { $_.skel } | Sort-Object -Unique)
if ($foreign.Count -gt 0) {
  Write-Output ("REFUSED: masters in this folder are on skeleton {0}, not -SkeletonGuid {1}; pass that GUID" -f ($foreign -join ', '), $SKEL)
  exit 1
}

# ---- -CloneByName: masters retargeted from HUMAN clips (retarget_mannequin_to_human.py --source-json). Every clip the
# index lists is a copy of ITS OWN vanilla AnimationClip (flags, priority, blends, hand poses, sounds, usages verbatim)
# re-pointed at our master, its Source1/Source2 shifted by one for the rest frame our masters open on (a reversed
# range such as blocked_slashright_2h 110..1 stays reversed), its facial id cleared (no facial rig on the troll), and
# its loop or death displacement scaled by -TravelScale. Several clips share one master, as in vanilla. -Verify checks
# the clips on disk against the index instead of the whole-master rule below.
# ---- vanilla clip table: the templates both modes copy from
$van = Load $Vanilla
$vclips = @{}
foreach ($it in $van.Package.Items) { if ($it.GetType().Name -eq 'AnimationClip') { $vclips[$it.Name] = $it } }

if ($CloneByName) {
  if (-not $ClipsIndex) { throw "-CloneByName needs -ClipsIndex <clips_index.json written by read_anim_keyframes_tpac.ps1 -ByClip>" }
  $index = @{}
  (Get-Content $ClipsIndex -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $index[$_.Name] = $_.Value }
  $ourMasterOf = @{}
  foreach ($clipName in $index.Keys) { $ourMasterOf[$clipName] = $ClipPrefix + ("$($index[$clipName].master)" -replace '^anim_', '') }
  # An AnimationClip's Name is a fixed-size(64) string in the engine: 63 usable characters. The Kit warned "Could not
  # set fixed-size(64) string" on 15 hill troll clips (2026-09-25). -Renames maps those vanilla clip names to shorter
  # troll names (tools/blender/hill_troll_clip_renames.json); bind_hill_troll_action_set.py reads the same file.
  $renameMap = @{}   # not $renames: PowerShell names are case-insensitive and that is the -Renames path
  if ($Renames) { (Get-Content $Renames -Raw | ConvertFrom-Json).PSObject.Properties | Where-Object { $_.Name -notlike '_*' } | ForEach-Object { $renameMap[$_.Name] = $_.Value } }
  $unrename = @{}
  foreach ($k in $renameMap.Keys) { $unrename[$renameMap[$k]] = $k }
  function ClipNameOf($clip) { if ($renameMap.ContainsKey($clip)) { return $renameMap[$clip] } else { return $ClipPrefix + $clip } }
  # a clip whose vanilla range (shifted by our rest frame) runs past its master's last frame: -Apply refuses it by
  # design, so neither mode counts it as missing
  function IsRefusedByDesign($clip) {
    $mm = $masterMap[$ourMasterOf[$clip]]
    return ($null -ne $mm) -and ([math]::Max([double]$index[$clip].source1 + 1, [double]$index[$clip].source2 + 1) -gt $mm.frames - 1)
  }
  # this mode never wires masters: a Kit import leaves them with an EMPTY skeleton, and wire_anim_master_skeletons.ps1
  # must put them on the rig first. The foreign-skeleton guard above cannot see an EMPTY master, so check here.
  $unwired = @($index.Keys | ForEach-Object { $ourMasterOf[$_] } | Sort-Object -Unique |
    Where-Object { $masterMap.ContainsKey($_) -and $masterMap[$_].skel -eq $EMPTY })
  foreach ($u in $unwired) { Write-Output ("UNWIRED {0}: EMPTY skeleton; run wire_anim_master_skeletons.ps1 -SkeletonGuid {1} -BoneNum 28 first" -f $u, $SKEL) }
  if ($unwired.Count -gt 0 -and -not $Verify) { Write-Output "REFUSED: masters are not wired to a skeleton"; exit 1 }
  if ($Verify) {
    $ok = 0; $bad = 0; $other = 0; $byGuid = @{}; $seen = @{}
    foreach ($mm in $masterMap.Values) { $byGuid["$($mm.guid)"] = $mm }
    foreach ($f in ([IO.Directory]::GetFiles($Masters, ($ClipPrefix + '*_anm.tpac')) | Sort-Object)) {
      $cl = (Load $f).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' } | Select-Object -First 1
      $clipName = if ($unrename.ContainsKey($cl.Name)) { $unrename[$cl.Name] } else { $cl.Name.Substring($ClipPrefix.Length) }
      # the Fab set's clips share the folder (the hill troll's animations/ holds both pipelines' masters): not this mode's
      if (-not $index.ContainsKey($clipName)) { $other++; continue }
      $seen[$clipName] = $true
      if ($cl.Name.Length -gt 63) { $bad++; Write-Output ("TOO LONG {0} ({1} chars): the engine truncates a clip name past 63; add it to -Renames" -f $cl.Name, $cl.Name.Length); continue }
      $info = $index[$clipName]; $key = "$($cl.Animation)"
      if (-not $byGuid.ContainsKey($key)) { $bad++; Write-Output ("ORPHAN {0} -> master GUID {1} not on disk" -f $cl.Name, $key); continue }
      $mm = $byGuid[$key]
      $want1 = [double]$info.source1 + 1; $want2 = [double]$info.source2 + 1
      if ([math]::Abs([double]$cl.Source1 - $want1) -gt 0.01 -or [math]::Abs([double]$cl.Source2 - $want2) -gt 0.01 -or [math]::Max($want1, $want2) -gt $mm.frames - 1) {
        $bad++; Write-Output ("RANGE  {0} -> {1}..{2}, want {3}..{4} within master frames {5}" -f $cl.Name, $cl.Source1, $cl.Source2, $want1, $want2, $mm.frames); continue
      }
      $ok++
    }
    $refused = @($index.Keys | Where-Object { -not $seen.ContainsKey($_) -and (IsRefusedByDesign $_) } | Sort-Object)
    foreach ($r in $refused) { Write-Output ("REFUSED BY DESIGN {0}: its vanilla range runs past master {1}" -f $r, $ourMasterOf[$r]) }
    $absent = $index.Count - $ok - $bad - $refused.Count
    Write-Output ("verify (clone-by-name): clips ok={0} bad={1} missing={2} refused-by-design={3} unwired masters={4} of index {5}   other pipeline's clips skipped={6}" -f $ok, $bad, $absent, $refused.Count, $unwired.Count, $index.Count, $other)
    if ($bad + $noAnim + $unwired.Count -gt 0 -or $absent -gt 0) { exit 1 } else { exit 0 }
  }
  $rows = @(); $written = 0; $skipped = 0; $missing = 0; $refusedByDesign = 0
  foreach ($clipName in ($index.Keys | Sort-Object)) {
    $info = $index[$clipName]
    $ourMaster = $ourMasterOf[$clipName]
    if (-not $masterMap.ContainsKey($ourMaster)) { $missing++; Write-Output ("NO MASTER {0} for clip {1} (vanilla master {2})" -f $ourMaster, $clipName, $info.master); continue }
    $m = $masterMap[$ourMaster]
    $tpl = $vclips[$clipName]
    if ($null -eq $tpl) { $missing++; Write-Output ("NO VANILLA CLIP {0}" -f $clipName); continue }
    $s1 = [double]$info.source1 + 1; $s2 = [double]$info.source2 + 1
    if ([math]::Max($s1, $s2) -gt $m.frames - 1) { $refusedByDesign++; Write-Output ("REFUSED BY DESIGN {0}: {1}..{2} outside master {3} frames {4}" -f $clipName, $s1, $s2, $ourMaster, $m.frames); continue }
    $newName = ClipNameOf $clipName
    if ($newName.Length -gt 63) { $missing++; Write-Output ("TOO LONG {0} ({1} chars): the engine's clip name is a fixed-size(64) string; add it to -Renames" -f $newName, $newName.Length); continue }
    $out = Join-Path $Masters ($newName + '_anm.tpac')
    $c = $tpl
    $c.Name = $newName
    $c.Guid = [Guid]::NewGuid()
    $c.Animation = [Guid]$m.guid
    $c.Source1 = [float]$s1
    $c.Source2 = [float]$s2
    $c.FacialAnimationId = ''
    $c.ClipSource1Name = ''; $c.ClipSource2Name = ''; $c.UnknownClipName = ''
    $usageNote = ''
    if ($TravelScale -gt 0) {
      foreach ($u in $c.ClipUsages) {
        if ($u.GetType().Name -eq 'BipMovIkUsage') { $u.LoopDisplacement = [float]([double]$u.LoopDisplacement * $TravelScale); $usageNote = ('loop={0}' -f $u.LoopDisplacement) }
        elseif ($u.GetType().Name -eq 'DisplacementUsage') {
          $v = $u.DisplacementVector
          $u.DisplacementVector = New-Object System.Numerics.Vector3 -ArgumentList @([float]($v.X * $TravelScale), [float]($v.Y * $TravelScale), [float]($v.Z * $TravelScale))
          $usageNote = ('disp={0}' -f $u.DisplacementVector)
        }
      }
    }
    $flags = ($c.Flags | ForEach-Object { "$_" }) -join ','
    $rows += ("{0,-44} <- {1,-44} s={2}..{3} dur={4,-5} pri={5,-3} {6} flags=[{7}]" -f $newName, $ourMaster, $c.Source1, $c.Source2, $c.Duration, $c.Priority, $usageNote, $flags)
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
  Write-Output ("clips planned: {0}   unresolved: {1}   refused-by-design: {2}   MODE = {3}   written={4} skipped-existing={5}" -f $rows.Count, $missing, $refusedByDesign, $(if ($Apply) { 'APPLY' } else { 'DRY-RUN (no writes)' }), $written, $skipped)
  $failed = 0
  if ($Apply) { $failed = Confirm-WrittenClips }
  if ($missing + $failed -gt 0) { exit 1 } else { exit 0 }
}

# ---- -Verify: read-only check of the clips already on disk, then exit
if ($Verify) {
  $byGuid = @{}
  foreach ($m in $masterMap.Values) { $byGuid["$($m.guid)"] = $m }
  $ok = 0; $stale = 0; $orphan = 0; $other = 0
  foreach ($f in ([IO.Directory]::GetFiles($Masters, ($ClipPrefix + '*_anm.tpac')) | Sort-Object)) {
    $cl = (Load $f).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' } | Select-Object -First 1
    # clone-by-name clips (human-sourced masters) share the folder and keep sub-ranges: not this rule's
    if ($null -ne $cl -and -not ($nameMap.Values -contains $cl.Name)) { $other++; continue }
    $key = "$($cl.Animation)"
    if ($null -eq $cl -or -not $byGuid.ContainsKey($key)) { $orphan++; Write-Output ("ORPHAN {0,-40} -> master GUID {1} not on disk" -f [IO.Path]::GetFileName($f), $key); continue }
    $m = $byGuid[$key]
    if ([int]$cl.Source2 -ne $m.frames - 1) { $stale++; Write-Output ("STALE  {0,-40} -> Source2={1}, master Duration={2} (want {3})" -f $cl.Name, $cl.Source2, $m.frames, ($m.frames - 1)) }
    else { $ok++ }
  }
  $mine = @($masterMap.Keys | Where-Object { $nameMap.ContainsKey('cave_' + $_) -or $clipStem.ContainsKey($_) }).Count
  Write-Output ("verify: clips ok={0} stale={1} orphan={2}   masters without a clip={3}   other pipeline's clips skipped={4}" -f $ok, $stale, $orphan, ($mine - $ok - $stale), $other)
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
# wiring an EMPTY master to the default (human) GUID is right only for the cave troll's own folder, the default
# -Masters; any other folder must name its rig, because bone counts cannot tell troll_skeleton_a from human_skeleton
$emptyCount = @($masterMap.Values | Where-Object { $_.skel -eq $EMPTY }).Count
if ($emptyCount -gt 0 -and $PSBoundParameters.ContainsKey('Masters') -and -not $PSBoundParameters.ContainsKey('SkeletonGuid')) {
  Write-Output ("REFUSED: {0} master(s) here have an EMPTY skeleton; pass -SkeletonGuid for this folder's rig" -f $emptyCount)
  exit 1
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
    [Array]::Copy($SKEL.ToByteArray(), 0, $bytes, $hits[0], 16)
    [IO.File]::WriteAllBytes($m.file, $bytes)
    $chk = Load $m.file
    $sa = $chk.Package.Items | Where-Object { $_.GetType().Name -eq 'SkeletalAnimation' } | Select-Object -First 1
    if ($null -eq $sa -or $sa.Skeleton -ne $SKEL -or $sa.Duration -ne $m.frames -or $sa.Name -ne $mname) {
      $wireFail++; Write-Output ("WIRE VERIFY FAIL {0}: skel={1} dur={2}" -f $mname, $sa.Skeleton, $sa.Duration); continue
    }
    $m.skel = "$SKEL"
  }
  $wired++
}
Write-Output ("skeleton wiring: {0} masters {1}, {2} failed" -f $wired, $(if ($Apply) { 'patched + re-read OK' } else { 'would be patched (offset found)' }), $wireFail)

# ---- vanilla templates (the clip table itself is loaded above the -CloneByName block, which needs it too)
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
  $n = $clip -replace ('^' + [regex]::Escape($ClipPrefix)), ''
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

function Travel($m) {
  # root travel per loop at the target's scale (see -TravelScale in the header)
  if ($TravelScale -gt 0) { return [float]([double]$m.travel_forward_troll_m * $TravelScale) }
  return [float]$m.travel_forward_human_m
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
$rows = @(); $written = 0; $skipped = 0; $foreign = 0
foreach ($mname in ($masterMap.Keys | Sort-Object)) {
  $m = $masterMap[$mname]
  $stem = 'cave_' + $mname            # troll_free_idle_0 -> cave_troll_free_idle_0
  if ($nameMap.ContainsKey($stem)) { $clipName = $nameMap[$stem] }
  elseif ($clipStem.ContainsKey($mname)) { $clipName = $mname; $stem = $clipStem[$mname] }   # named after its clip
  else { $foreign++; continue }   # a clone-by-name master (human-sourced) sharing the folder: not this mode's
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
        $u.LoopDisplacement = Travel $meas
        $usageNote = ('loop={0}' -f $u.LoopDisplacement)
      }
    }
    if ($type -ne 'turn' -and $meas) { $c.StepPoints = StepPoints $meas }
  }
  elseif ($type -eq 'death') {
    foreach ($u in $c.ClipUsages) {
      if ($u.GetType().Name -eq 'DisplacementUsage') {
        $u.DisplacementVector = New-Object System.Numerics.Vector3 -ArgumentList @([float]0, (Travel $meas), [float]0)
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
Write-Output ("clips planned: {0}   MODE = {1}   written={2} skipped-existing={3}   other pipeline's masters skipped={4}" -f $rows.Count, $(if ($Apply) { 'APPLY' } else { 'DRY-RUN (no writes)' }), $written, $skipped, $foreign)

# ---- verify what was written
$failed = 0
if ($Apply) { $failed = Confirm-WrittenClips }
if ($wireFail + $failed -gt 0) { exit 1 } else { exit 0 }
