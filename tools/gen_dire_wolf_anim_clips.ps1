param(
  [switch]$Apply,
  [switch]$Verify,
  [string]$Armory = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory',
  [string]$Pack = 'E:\LOTRAOMAssets\ADOD_Beasts\Assets\wolves\animations',
  [string]$TpacBin = 'E:\Release_v0.5.1\bin',
  [string]$Loader = 'E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs'
)
<#
Run under Windows PowerShell 5.1 (TpacTool needs .NET Framework):
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/gen_dire_wolf_anim_clips.ps1 [-Apply | -Verify]

Author the dire wolf's AnimationClip tpacs (#1081): one dire_wolf_<x>_anm.tpac for each warg_<x> clip that as_warg,
as_warg_map and as_warg_town_and_village bind. as_dire_wolf is then as_warg with the clip names swapped, so it answers
every act_warg_* and act_horse_* action the warg behavior tree and the rider partial play.

Each clip clones the warg clip, so flags, priority, blends, combat parameter, sound code, continue action, step points
and pace limits are the warg's. The pack's own clips lack make_walk_sound, enforce_lowerbody and enforce_all, which is
why they are not shipped: locomotion would hide their attacks in battle. Per clip only these change:
  Name       warg_<x> becomes dire_wolf_<x>
  Guid       fresh
  Animation, Source1, Source2, Duration   the pack clip's, so the wolf's own frames play at the pack's rate
  LoopDisplacement   gaits only: the pack clip's value times $GAIT_SCALE, the Monster's size. A strafe or backward clip
             keeps the warg's ratio to its forward gait. Whether the engine scales this value by agent size is not
             established (docs/features/animalia-elk-moose.md), so a foot slide in game means tune $GAIT_SCALE.
  a *_continue clip  the last frame of its death clip, as the warg's are.
The pack has no jump, rear, quick stop, backward or collide clip. Those use the nearest clip in $MAP. A reaction must not
hold the wolf longer than it holds the warg, so $WARG_TIME clips take the warg's Duration, and $WARG_CUT clips (from a
long idle) also play only that many frames at 30 fps from the clip's start. $WARG_TRAVEL clips keep the warg's
LoopDisplacement, because the pack's 1.95 m belongs to its in-place turn, not to a strafe.

Dry run by default. -Apply refuses while the game or the Kit runs, never overwrites a clip, re-reads each file and
refreshes item checksums (tpac_fix_item_checksums.py). Open the Kit once afterwards so it writes the clips'
RuntimeDataCache entries, then save. -Verify is read-only: every planned clip exists, plays a master that is in the
folder, equals the plan (name, master, flags, frames, duration, travel) and has current item checksums. Get-ClipProblem
is that one check, for existing clips in every mode, so -Apply exits 1 on an existing clip that differs from the plan.
To change a clip (a new $GAIT_SCALE), delete it, run -Apply, then open and save the Kit once.
All modes fail when as_warg binds a clip $MAP does not plan, so a new warg binding cannot leave the wolf behind.
#>
$ErrorActionPreference = 'Stop'
if ($Apply -and $Verify) { Write-Output 'ERROR: pass -Apply or -Verify, not both (-Apply already re-verifies)'; exit 2 }
if ($PSVersionTable.PSEdition -ne 'Desktop') { Write-Output 'ERROR: run under Windows PowerShell 5.1 (powershell.exe), not pwsh'; exit 2 }
foreach ($need in @("$TpacBin\TpacTool.Lib.dll", $Loader)) {
  if (-not (Test-Path $need)) { Write-Output ('ERROR: {0} not found' -f $need); exit 2 }
}
Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }

$GAIT_SCALE = 2.4
$WARG = "$Armory\Assets\creature\warg\animations"
$OUT = "$Armory\Assets\creature\direwolf\animations"
$SETS = 'as_warg', 'as_warg_map', 'as_warg_town_and_village'

# warg clip stem -> pack clip stem
$MAP = [ordered]@{
  attack_stand = 'attack_1'; attack_running = 'attack_2'
  taunt = 'bark'; rear_damaged = 'strike_1'; run_collide = 'strike_2'; quick_stop = 'idle_3'
  idle = 'idle'; idle_2 = 'idle_2'; stand_for_movement_data = 'stand_for_movement_data'
  dash = 'gallop'; jump_forward = 'gallop'; jump_forward_loop = 'gallop'; jump_forward_end = 'idle'
  walkfast = 'walk_forward'; walk_strafe = 'walk_forward'; walkbackfast = 'walk_forward'; walkbackfast_straight = 'walk_forward'
  trot = 'trot'; trot_strafe = 'trot'; trot_stand = 'trot'
  canter = 'canter'; canter_strafe = 'canter'; canter_stand = 'canter'
  forward_gallop_left_foot = 'gallop'; forward_gallop_left_foot_strafe = 'gallop'; forward_gallop_left_foot_stand = 'gallop'
  strafe_right_faster = 'turn_right'; strafe_left_faster = 'turn_left'
  walk_turn_right_head = 'walk_forward_right'; walk_turn_left_head = 'walk_forward_left'
  trot_r = 'trot_right'; trot_l = 'trot_left'; canter_r = 'canter_right'; canter_l = 'canter_left'
  gallop_turn_right_head = 'gallop_right'; gallop_turn_left_head = 'gallop_left'
  death_right_side = 'death_right'; death_left_side = 'death_left'
  death_right_side_continue = 'death_right'; death_left_side_continue = 'death_left'
}
# A clip whose travel follows its forward gait's: warg stem -> the warg's forward gait stem
$FAMILY = @{
  walk_strafe = 'walkfast'; walkbackfast = 'walkfast'; walkbackfast_straight = 'walkfast'
  trot_strafe = 'trot'; canter_strafe = 'canter'; forward_gallop_left_foot_strafe = 'forward_gallop_left_foot'
}

$WARG_TIME = 'rear_damaged', 'run_collide', 'dash', 'quick_stop', 'jump_forward_end'
$WARG_CUT = 'quick_stop', 'jump_forward_end'
$WARG_TRAVEL = 'strafe_right_faster', 'strafe_left_faster'
$FPS = 30.0

function Assert-GameAndKitClosed {
  $procs = @(Get-Process -Name 'Bannerlord*', 'TaleWorlds.MountAndBlade*' -ErrorAction SilentlyContinue)
  if ($procs.Count) { Write-Output ('REFUSED: Bannerlord or the Modding Kit is running; close it and re-run'); exit 2 }
}
function Get-Clips($dir) {
  $found = @{}
  foreach ($f in Get-ChildItem $dir -Filter '*_anm.tpac') {
    foreach ($it in (Load $f.FullName).Package.Items) {
      if ($it.GetType().Name -eq 'AnimationClip') { $found[$it.Name] = $f.FullName }
    }
  }
  $found
}
function Get-Clip($files, $name) {
  @((Load $files[$name]).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' -and $_.Name -eq $name })[0]
}
function Get-Loop($clip) {
  foreach ($u in $clip.ClipUsages) { if ($u.GetType().Name -eq 'QuadMovementUsage') { return [float]$u.LoopDisplacement } }
  $null
}
function Get-ClipFacts($c) {
  @{ Name = $c.Name; Animation = "$($c.Animation)"; Flags = (($c.Flags | ForEach-Object { "$_" }) -join ',')
    Source1 = [double]$c.Source1; Source2 = [double]$c.Source2; Duration = [double]$c.Duration; Loop = Get-Loop $c }
}
function Get-ClipProblem($got, $want, $masters) {
  if ($null -eq $got) { return 'no AnimationClip item' }
  foreach ($k in 'Name', 'Animation', 'Flags') {
    if ("$($got[$k])" -ne "$($want[$k])") { '{0} is {1}, planned {2}' -f $k, $got[$k], $want[$k] }
  }
  foreach ($k in 'Source1', 'Source2', 'Duration', 'Loop') {
    if ($null -eq $got[$k] -and $null -eq $want[$k]) { continue }
    if ($null -eq $got[$k] -or $null -eq $want[$k] -or [math]::Abs($got[$k] - $want[$k]) -gt 0.001) {
      '{0} is {1}, planned {2}' -f $k, $got[$k], $want[$k]
    }
  }
  if (-not $masters.ContainsKey($got['Animation'])) { 'NO MASTER {0}' -f $got['Animation'] }
}
function Get-SavedClip($path) {
  $item = @((Load $path).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' })[0]
  if ($null -ne $item) { Get-ClipFacts $item }
}

if ($Apply) { Assert-GameAndKitClosed }
$errors = 0
$wargFiles = Get-Clips $WARG
$packFiles = Get-Clips $Pack
$masterGuids = @{}
if (Test-Path $OUT) {
  foreach ($f in Get-ChildItem $OUT -Filter 'adod_wolf_*_geo.tpac') {
    foreach ($it in (Load $f.FullName).Package.Items) {
      if ($it.GetType().Name -eq 'SkeletalAnimation') { $masterGuids["$($it.Guid)"] = $true }
    }
  }
}

[xml]$actionXml = Get-Content -Raw "$Armory\ModuleData\action_sets.xml"
$bound = @($actionXml.action_sets.action_set | Where-Object { $SETS -contains $_.id } | ForEach-Object { $_.action } |
  ForEach-Object { $_.animation } | Where-Object { $_ } | Sort-Object -Unique)
if (-not $bound.Count) { Write-Output 'ERROR: no warg bindings found in action_sets.xml'; exit 1 }
foreach ($b in $bound) {
  if (-not $b.StartsWith('warg_') -or -not $MAP.Contains($b.Substring(5))) { $errors++; Write-Output ('UNMAPPED {0}' -f $b) }
}

$rows = @(); $written = 0; $skipped = 0
foreach ($stem in $MAP.Keys) {
  $wargName = "warg_$stem"; $packName = "adod_wolf_$($MAP[$stem])"; $clipName = "dire_wolf_$stem"
  if (-not $wargFiles.ContainsKey($wargName)) { $errors++; Write-Output ('NO WARG CLIP {0}' -f $wargName); continue }
  if (-not $packFiles.ContainsKey($packName)) { $errors++; Write-Output ('NO PACK CLIP {0}' -f $packName); continue }
  $path = Join-Path $OUT ($clipName + '_anm.tpac')
  $p = Get-Clip $packFiles $packName

  $c = Get-Clip $wargFiles $wargName
  $wargLoop = Get-Loop $c
  $c.Name = $clipName
  $c.Guid = [Guid]::NewGuid()
  $c.Animation = $p.Animation
  if ($stem -like '*_continue') {
    $c.Source1 = $p.Source2; $c.Source2 = $p.Source2
  } else {
    $c.Source1 = $p.Source1; $c.Source2 = $p.Source2
    if ($WARG_CUT -contains $stem) { $c.Source2 = [float]($p.Source1 + [math]::Round($c.Duration * $FPS)) }
    if ($WARG_TIME -notcontains $stem) { $c.Duration = $p.Duration }
  }
  $loop = ''
  if ($WARG_TRAVEL -contains $stem) {
    $loop = 'loop={0:0.###}' -f $wargLoop
  } elseif ($null -ne $wargLoop) {
    $packLoop = Get-Loop $p
    if ($null -eq $packLoop) { $errors++; Write-Output ('NO PACK TRAVEL {0}' -f $packName); continue }
    $value = $packLoop * $GAIT_SCALE
    if ($FAMILY.ContainsKey($stem)) { $value = $value * $wargLoop / (Get-Loop (Get-Clip $wargFiles "warg_$($FAMILY[$stem])")) }
    foreach ($u in $c.ClipUsages) { if ($u.GetType().Name -eq 'QuadMovementUsage') { $u.LoopDisplacement = [float]$value } }
    $loop = 'loop={0:0.###}' -f $value
  }
  $rows += ('{0,-46} <- {1,-30} {2,-30} s={3}..{4} dur={5} {6} [{7}]' -f $clipName, $wargName, $packName, $c.Source1, $c.Source2, $c.Duration, $loop, (($c.Flags | ForEach-Object { "$_" }) -join ','))
  $want = Get-ClipFacts $c
  if (Test-Path $path) {
    foreach ($problem in @(Get-ClipProblem (Get-SavedClip $path) $want $masterGuids)) { $errors++; Write-Output ('WRONG {0}: {1}' -f $clipName, $problem) }
    $skipped++
    continue
  }
  if ($Verify) { $errors++; Write-Output ('MISSING {0}' -f $clipName); continue }
  if (-not $Apply) { continue }
  $c.TypelessDataSegments.Clear(); $c.UnknownDependences.Clear()
  $pkg = New-Object TpacTool.Lib.AssetPackage
  $pkg.Guid = [Guid]::NewGuid()
  $pkg.Items.Add($c)
  if (-not (Test-Path $OUT)) { New-Item -ItemType Directory -Path $OUT | Out-Null }
  $pkg.Save($path, $false, 2)
  $written++
  foreach ($problem in @(Get-ClipProblem (Get-SavedClip $path) $want $masterGuids)) { $errors++; Write-Output ('VERIFY FAIL {0}: {1}' -f $path, $problem) }
}

if (-not $Verify) { $rows | ForEach-Object { Write-Output $_ } }
$mode = if ($Verify) { 'VERIFY' } elseif ($Apply) { 'APPLY' } else { 'DRY-RUN' }
Write-Output ('clips planned: {0}   bound by the warg sets: {1}   MODE = {2}   written={3} skipped-existing={4} errors={5}' -f $MAP.Count, $bound.Count, $mode, $written, $skipped, $errors)
if ($Apply -and $errors -eq 0) {
  Assert-GameAndKitClosed
  & python "$PSScriptRoot\tpac_fix_item_checksums.py" $OUT --glob '*_anm.tpac' --apply | Select-Object -Last 1
  if ($LASTEXITCODE -ne 0) { $errors++; Write-Output 'CHECKSUM FIX FAILED' }
}
if ($Verify) {
  $last = & python "$PSScriptRoot\tpac_fix_item_checksums.py" $OUT --glob '*_anm.tpac' | Select-Object -Last 1
  if ($LASTEXITCODE -or "$last" -notmatch '^files=[1-9]\d* stale-items=0\b') { $errors++; Write-Output ('STALE CHECKSUMS: {0}' -f $last) }
}
if ($errors) { exit 1 }
exit 0
