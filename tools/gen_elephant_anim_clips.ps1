param(
  [switch]$Apply,
  [switch]$Verify,
  [Parameter(Mandatory = $true)][string[]]$Reports,
  [string]$Folder = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\creature\elephant\animations',
  [string]$Templates = '',
  [string]$TpacBin = 'E:\Release_v0.5.1\bin',
  [string]$Loader = 'E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs'
)
<#
Run under Windows PowerShell 5.1 (TpacTool needs .NET Framework):
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& tools\gen_elephant_anim_clips.ps1 -Reports @('<set>\report.json', ...) [-Apply | -Verify]"

Author the AnimationClip tpacs of the war elephant's and the mumak's own gaits and idles (#1474): one
<clip>_anm.tpac for each clip in the report.json files that tools/elephant_gait_retarget.py writes, beside the
master of the same name that tools/elephant_write_masters.py --new writes into -Folder.

Each clip clones the elephant clip that plays the same role today ($TEMPLATE), so flags, priority, blends, sound
code and pace switch limits are today's. The trot clones the WALK clip: as_elephant has always bound the trot action
to the walk clip, so the walk's limits are the ones the trot action has run with, and the unbound elephant_trot
record's limits were never in a game. Per clip only these change:
  Name, Guid         the report's clip name; a fresh GUID
  Animation          the master in -Folder with the clip's name
  Source1, Source2   0 and the master's key count, as the Kit-made elephant clips have them
  LoopDisplacement   gaits: the report's stride, which the feet in the master cover (tools/check_gait_stride.py)
  StepPoints         gaits: the four plants the report measured
  Duration           idles only: the source take's length in seconds; a mumak idle plays 1.4 times slower

Dry run by default. -Apply refuses while the game or the Kit runs, never overwrites a clip, re-reads each file and
refreshes item checksums (tpac_fix_item_checksums.py). A new clip has no RuntimeDataCache entry: the game must not
bind it until the Modding Kit has started once with the Armory and written the entry (check_rdc_entries.py). -Verify
is read-only: every planned clip exists, names its master, equals the plan and has a current checksum.
Exit 0 clean, 1 on a finding, 2 when it cannot run.
#>
$ErrorActionPreference = 'Stop'
if ($Apply -and $Verify) { Write-Output 'ERROR: pass -Apply or -Verify, not both (-Apply already re-verifies)'; exit 2 }
if ($PSVersionTable.PSEdition -ne 'Desktop') { Write-Output 'ERROR: run under Windows PowerShell 5.1 (powershell.exe), not pwsh'; exit 2 }
foreach ($need in @("$TpacBin\TpacTool.Lib.dll", $Loader, $Folder) + $Reports) {
  if (-not (Test-Path $need)) { Write-Output ('ERROR: {0} not found' -f $need); exit 2 }
}
if ($Apply) {
  $procs = @(Get-Process -Name 'Bannerlord*', 'TaleWorlds.MountAndBlade*' -ErrorAction SilentlyContinue)
  if ($procs.Count) { Write-Output 'REFUSED: Bannerlord or the Modding Kit is running; close it and re-run'; exit 2 }
}
Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
if (-not $Templates) { $Templates = $Folder }
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
function V4($a, $b, $c, $d) { New-Object System.Numerics.Vector4 -ArgumentList @([float]$a, [float]$b, [float]$c, [float]$d) }

# role (the clip name after its creature prefix) -> the elephant clip in -Templates (default: -Folder) it clones
$TEMPLATE = [ordered]@{
  walk = 'elephant_walk'; trot = 'elephant_walk'; canter = 'elephant_canter'; gallop = 'elephant_gallop'
  walk_backwards = 'elephant_walk_backwards'; turn_left = 'elephant_turn_left'; turn_right = 'elephant_turn_right'
  idle_1 = 'elephant_stand_2'; idle_2 = 'elephant_stand_2'
}
$PREFIXES = 'war_elephant_', 'mumakil_'
$IDLE_SLOW = @{ 'mumakil_' = 1.4 }

function Get-Role($clip) {
  foreach ($p in $PREFIXES) { if ($clip.StartsWith($p)) { return @{ Prefix = $p; Role = $clip.Substring($p.Length) } } }
  $null
}
function Get-Loop($clip) {
  foreach ($u in $clip.ClipUsages) { if ($u.GetType().Name -eq 'QuadMovementUsage') { return [float]$u.LoopDisplacement } }
  $null
}
function Get-AnimClip($path) {
  @((Load $path).Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' })[0]
}

$masters = @{}
foreach ($f in Get-ChildItem $Folder -Filter '*_geo.tpac') {
  if (-not ($PREFIXES | Where-Object { $f.Name.StartsWith($_) })) { continue }
  foreach ($it in (Load $f.FullName).Package.Items) {
    if ($it.GetType().Name -eq 'SkeletalAnimation') { $masters[$it.Name] = @{ Guid = [Guid]$it.Guid; Keys = [int]$it.Duration } }
  }
}

$errors = 0; $plan = @()
foreach ($rp in $Reports) {
  $rep = Get-Content -Raw $rp | ConvertFrom-Json
  foreach ($g in @($rep.gaits) + @($rep.takes)) {
    if ($null -eq $g) { continue }
    $r = Get-Role $g.clip
    if ($null -eq $r -or -not $TEMPLATE.Contains($r.Role)) { $errors++; Write-Output ('UNPLANNED {0}: no template for it' -f $g.clip); continue }
    if (-not $masters.ContainsKey($g.clip)) { $errors++; Write-Output ('NO MASTER {0}' -f $g.clip); continue }
    $m = $masters[$g.clip]
    if ($m.Keys -ne [int]$g.frames) { $errors++; Write-Output ('KEYS {0}: master has {1}, report {2}' -f $g.clip, $m.Keys, $g.frames); continue }
    $isGait = [double]$g.loop_displacement -gt 0
    $slow = $(if ($IDLE_SLOW.ContainsKey($r.Prefix)) { $IDLE_SLOW[$r.Prefix] } else { 1.0 })
    $plan += [pscustomobject]@{
      Clip = $g.clip; Template = $TEMPLATE[$r.Role]; Master = $m.Guid; Keys = $m.Keys; Gait = $isGait
      Loop = $(if ($isGait) { [float]$g.loop_displacement } else { $null })
      Steps = $(if ($isGait) { @($g.step_points) } else { $null })
      Duration = $(if ($isGait) { $null } else { [float]([math]::Round([double]$g.source_seconds * $slow, 3)) })
    }
  }
}

function Get-Problem($c, $p, $tplDuration) {
  if ($null -eq $c) { return 'no AnimationClip item' }
  if ($c.Name -ne $p.Clip) { 'name is {0}' -f $c.Name }
  if ([Guid]$c.Animation -ne $p.Master) { 'Animation is {0}, the master is {1}' -f $c.Animation, $p.Master }
  if ([int]$c.Source1 -ne 0 -or [int]$c.Source2 -ne $p.Keys) { 'range is {0}..{1}, planned 0..{2}' -f $c.Source1, $c.Source2, $p.Keys }
  $want = $(if ($p.Gait) { $tplDuration } else { $p.Duration })
  if ([math]::Abs([double]$c.Duration - $want) -gt 0.001) { 'Duration is {0}, planned {1}' -f $c.Duration, $want }
  if ($p.Gait) {
    $loop = Get-Loop $c
    if ($null -eq $loop -or [math]::Abs($loop - $p.Loop) -gt 0.001) { 'LoopDisplacement is {0}, planned {1}' -f $loop, $p.Loop }
    $s = $c.StepPoints; $got = @($s.X, $s.Y, $s.Z, $s.W)
    for ($i = 0; $i -lt 4; $i++) { if ([math]::Abs($got[$i] - [double]$p.Steps[$i]) -gt 0.001) { 'StepPoints are {0}, planned {1}' -f $s, ($p.Steps -join ', '); break } }
  }
}

$tplCache = @{}; $written = 0; $ok = 0
foreach ($p in $plan) {
  $tplPath = Join-Path $Templates ($p.Template + '_anm.tpac')
  if (-not (Test-Path $tplPath)) { $errors++; Write-Output ('NO TEMPLATE {0}' -f $tplPath); continue }
  if (-not $tplCache.ContainsKey($p.Template)) { $tplCache[$p.Template] = [double](Get-AnimClip $tplPath).Duration }
  $out = Join-Path $Folder ($p.Clip + '_anm.tpac')
  if (-not (Test-Path $out)) {
    if (-not $Apply) {
      if ($Verify) { $errors++; Write-Output ('MISSING {0}' -f $p.Clip) }
      else { Write-Output ('PLAN   {0,-30} <- {1,-24} keys {2,-3} loop {3} steps {4} duration {5}' -f $p.Clip, $p.Template, $p.Keys, $p.Loop, ($p.Steps -join ' '), $(if ($p.Gait) { $tplCache[$p.Template] } else { $p.Duration })) }
      continue
    }
    $c = Get-AnimClip $tplPath          # a fresh load per clip: nothing leaks from one clip into the next
    $c.Name = $p.Clip; $c.Guid = [Guid]::NewGuid(); $c.Animation = $p.Master
    $c.Source1 = [float]0; $c.Source2 = [float]$p.Keys
    if ($p.Gait) {
      $c.StepPoints = V4 $p.Steps[0] $p.Steps[1] $p.Steps[2] $p.Steps[3]
      foreach ($u in $c.ClipUsages) { if ($u.GetType().Name -eq 'QuadMovementUsage') { $u.LoopDisplacement = $p.Loop } }
    } else { $c.Duration = $p.Duration }
    $c.TypelessDataSegments.Clear(); $c.UnknownDependences.Clear()
    $pkg = New-Object TpacTool.Lib.AssetPackage
    $pkg.Guid = [Guid]::NewGuid()
    $pkg.Items.Add($c)
    $pkg.Save($out, $false, 2)   # path, useOriginalChecksum, tpac version
    $written++
  }
  $problems = @(Get-Problem (Get-AnimClip $out) $p $tplCache[$p.Template])
  if ($problems.Count) { $errors++; Write-Output ('DIFFERS {0}: {1}' -f $p.Clip, ($problems -join '; ')) } else { $ok++ }
}
if ($Apply -or $Verify) {
  foreach ($prefix in $PREFIXES) {
    if (-not (Get-ChildItem $Folder -Filter ($prefix + '*_anm.tpac'))) { continue }
    $mode = @(); if ($Apply) { $mode = @('--apply') }
    $last = & python "$PSScriptRoot\tpac_fix_item_checksums.py" $Folder --glob ($prefix + '*_anm.tpac') @mode | Select-Object -Last 1
    if ($LASTEXITCODE) { $errors++; Write-Output ('CHECKSUM FIXER FAILED (exit {0})' -f $LASTEXITCODE) }
    elseif ($Verify -and "$last" -notmatch 'stale-items=0\b') { $errors++; Write-Output ('CHECKSUM {0}' -f $last) }
  }
}
Write-Output ('clips planned: {0}   MODE = {1}   written={2} equal-to-plan={3} errors={4}' -f $plan.Count, $(if ($Apply) { 'APPLY' } elseif ($Verify) { 'VERIFY' } else { 'DRY-RUN (no writes)' }), $written, $ok, $errors)
if ($errors -gt 0) { exit 1 } else { exit 0 }
