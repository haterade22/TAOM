param(
  [switch]$Apply,
  [switch]$Verify,
  [Parameter(Mandatory = $true)][string[]]$Reports,
  [string]$Folder = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\creature\elephant\animations',
  [string[]]$Templates = @(),
  [string[]]$Prefixes = @('war_elephant_', 'mumakil_'),
  [string]$BackupDir = '',
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
code and pace switch limits are today's. The trot clones the WALK clip, the one as_elephant has always bound to the
trot action, and takes its pace limits from ADOD's own trot record ($LIMITS_FROM). Per clip only these change:
  Name, Guid         the report's clip name; a fresh GUID
  Animation          the master in -Folder with the clip's name
  Source1, Source2   0 and the master's last key time, its key count less one. The editor accepts a record only
                     when its sources lie inside the master's key times (tools/elephant_write_masters.py, KEY
                     TIMES): that tool keys a master from 0 and closes a loop with one more key, so a loop of N
                     frames has N + 1 keys and sources 0 to N. Until 2026-11-23 this wrote 0 and the key COUNT
                     on masters keyed 1 to N, as ADOD's elephant clips are: one warning per bone per clip
  LoopDisplacement   gaits: the report's stride, which the feet in the master cover (tools/check_gait_stride.py)
  StepPoints         gaits: the four plants the report measured
  Duration           idles: the source take's length in seconds; a mumak idle plays 1.4 times slower.
                     strafe: the keys' real length, (keys - 1) / 30: a loop's last key repeats its first. The standing pace holds a strafe clip's loop between
                     0.7 and 1.3 times its Duration, and ADOD's 1.0 s ran the walk cycle 2 to 3.6 times too fast
  PaceSwitchLimits   walk_backwards: the template's limits negated (0.2..2.5 becomes -2.5..-0.2), as the vanilla
                     horse has them. With positive limits the engine keeps a beast in the backward pace until it
                     walks forward faster than the upper limit.
                     trot: elephant_trot's 2.3..4.5, not the walk's 0.2..2.5. The engine steps down a pace only
                     below the CURRENT pace's lower limit, so with the walk's limits a beast that once passed
                     2.5 m/s trotted down to 0.2 m/s (the native reads: tools/check_gait_stride.py)
The strafe clip has no report row of its own: it is the walk's row under the name <prefix>strafe, and its master is
a copy of the walk's keys under that name.

Another creature (#1541, the spider): a report row that names its own `template` is taken as written. Its clip
clones that record from -Templates and takes the row's `duration`, its `step_points`, and for a row with travel
the row's `loop_displacement` and `limits` (no `limits`: the template's stay). `usage: false` removes the
template's clip usages: a gait's in-place twin is the gait's record with no usage, as the vanilla horse's
gait_walkfast is beside horse_walkfast. `sound_code` replaces the template's (the elephant's idles carry a
horse_eating sound with sound points at 0.2 and 0.5). The editor refuses a sound point at or past
(Duration - BlendOutPeriod) / Duration with a "Sound Point Error" dialog. Seen twice on 2026-11-22: a record of
0.9 s with a blend-out of 0.8 s and a point at 0.2 was refused ("should be smaller than 0.111111"), and a record
of 0.267 s with a blend-IN of 0.3 s, a blend-out of 0 and points up to 0.53 loaded, so the blend-in is not in the
rule. An explicit row is judged BEFORE anything is written, in a dry run too: a row with no `duration`, a row
that says `usage: false` and claims travel, a row that claims travel on a template with no clip usage (the writer
sets travel on a usage that is there; it makes none), and a row whose planned sound points break that rule are refused
(INVALID, exit 1), and no file of that row is made. -Verify also holds the rule against each clip on disk.
No role table applies and no strafe twin is derived. tools/spider_gait.py writes such rows:
  powershell.exe ... "& tools\gen_elephant_anim_clips.ps1 -Reports @('<set>\report.json') -Prefixes spider_ `
      -Folder <Armory>\Assets\creature\spider\animations -Templates @('<Armory>\Assets\creature\elephant\animations', `
      '<Armory>\Assets\creature\warg\animations')"

Dry run by default. -Apply refuses while the game or the Kit runs, never overwrites a clip (one exception: a clip
that differs from the plan only in its pace limits or its source range, every other simple field, the flags and
each usage's fields compared with the template's (Get-FieldDiffs), gets them rewritten in place after a copy
to <clip>_anm.tpac.bak-limits-<stamp>, or into -BackupDir when that is given, which keeps the copy out of the
module; package and item GUIDs stay, so the rows and the cache entry still name it),
re-reads each file and refreshes item checksums (tpac_fix_item_checksums.py). A new clip has no RuntimeDataCache
entry, and the Kit cooks a rewritten record's entry again: start the Modding Kit once with the Armory after either
(check_gait_stride.py fails a bound clip whose entry is older than its record). -Verify
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
if (-not $Templates) { $Templates = @($Folder) }
# -Templates is a list of folders, searched in order: the spider's one-shots (#1550) clone the warg's and the
# elephant's records, which sit in two folders.
function Find-Template($name) {
  foreach ($dir in $Templates) { $path = Join-Path $dir ($name + '_anm.tpac'); if (Test-Path $path) { return $path } }
  $null
}
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
function V4($a, $b, $c, $d) { New-Object System.Numerics.Vector4 -ArgumentList @([float]$a, [float]$b, [float]$c, [float]$d) }

# role (the clip name after its creature prefix) -> the elephant clip in -Templates (default: -Folder) it clones
$TEMPLATE = [ordered]@{
  walk = 'elephant_walk'; trot = 'elephant_walk'; canter = 'elephant_canter'; gallop = 'elephant_gallop'
  walk_backwards = 'elephant_walk_backwards'; turn_left = 'elephant_turn_left'; turn_right = 'elephant_turn_right'
  idle_1 = 'elephant_stand_2'; idle_2 = 'elephant_stand_2'; strafe = 'elephant_walk'
}
$KEY_RATE = 30.0
$REAL_DURATION = @('strafe')          # roles whose record states the keys' real length
$NEGATED_LIMITS = @('walk_backwards') # roles whose pace limits are the template's, negated
$LIMITS_FROM = @{ trot = 'elephant_trot' }   # roles whose pace limits come from another record than their template
$PREFIXES = $Prefixes
$IDLE_SLOW = @{ 'mumakil_' = 1.4 }

function Get-Role($clip) {
  foreach ($p in $PREFIXES) { if ($clip.StartsWith($p)) { return @{ Prefix = $p; Role = $clip.Substring($p.Length) } } }
  $null
}
function Get-Loop($clip) {
  foreach ($u in $clip.ClipUsages) { if ($u.GetType().Name -eq 'QuadMovementUsage') { return [float]$u.LoopDisplacement } }
  $null
}
function Get-Limits($clip) {
  foreach ($u in $clip.ClipUsages) { if ($u.GetType().Name -eq 'QuadMovementUsage') { return @([float]$u.PaceSwitchLimitMin, [float]$u.PaceSwitchLimitMax) } }
  $null
}
function Get-WantLimits($p, $tplLimits) {
  if ($null -eq $tplLimits) { return $null }
  if ($NEGATED_LIMITS -contains $p.Role) { return @([float](-$tplLimits[1]), [float](-$tplLimits[0])) }
  $tplLimits
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
  $rows = @()
  foreach ($g in @($rep.gaits) + @($rep.takes)) {
    if ($null -eq $g) { continue }
    $rows += $g
    if ($g.template) { continue }   # an explicit row: no strafe twin
    $w = Get-Role $g.clip
    if ($null -ne $w -and $w.Role -eq 'walk') {   # the strafe clip: the walk's row under its own name
      $twin = $g.PSObject.Copy(); $twin.clip = $w.Prefix + 'strafe'; $rows += $twin
    }
  }
  foreach ($g in $rows) {
    if ($g.template) {              # an explicit row: every record value is the row's own
      if (-not $masters.ContainsKey($g.clip)) { $errors++; Write-Output ('NO MASTER {0}' -f $g.clip); continue }
      $m = $masters[$g.clip]
      $wantKeys = $(if ($null -ne $g.keys) { [int]$g.keys } else { [int]$g.frames + 1 })   # a row with no `keys` is a loop
      if ($m.Keys -ne $wantKeys) { $errors++; Write-Output ('KEYS {0}: master has {1} keys, the report row needs {2} (a loop has one more key than its frames; elephant_write_masters.py --rekey brings an older master there)' -f $g.clip, $m.Keys, $wantKeys); continue }
      $isGait = [double]$g.loop_displacement -gt 0
      if (-not ([double]$g.duration -gt 0)) { $errors++; Write-Output ('INVALID {0}: an explicit row needs a duration above 0' -f $g.clip); continue }
      if ($isGait -and $null -ne $g.usage -and -not [bool]$g.usage) { $errors++; Write-Output ('INVALID {0}: usage is false and loop_displacement is {1}; a record with no usage cannot claim travel' -f $g.clip, $g.loop_displacement); continue }
      $plan += [pscustomobject]@{
        Clip = $g.clip; Role = 'explicit'; Template = [string]$g.template; Master = $m.Guid; Keys = $m.Keys; Last = $m.Keys - 1; Gait = $isGait
        Loop = $(if ($isGait) { [float]$g.loop_displacement } else { $null })
        Steps = $(if ($null -ne $g.step_points) { @($g.step_points) } else { $null })
        Duration = [float]([math]::Round([double]$g.duration, 4))
        Limits = $(if ($null -ne $g.limits) { @([float]$g.limits[0], [float]$g.limits[1]) } else { $null })
        NoUsage = ($null -ne $g.usage -and -not [bool]$g.usage)
        Sound = $(if ($null -ne $g.sound_code) { [string]$g.sound_code } else { $null })
      }
      continue
    }
    $r = Get-Role $g.clip
    if ($null -eq $r -or -not $TEMPLATE.Contains($r.Role)) { $errors++; Write-Output ('UNPLANNED {0}: no template for it' -f $g.clip); continue }
    if (-not $masters.ContainsKey($g.clip)) { $errors++; Write-Output ('NO MASTER {0}' -f $g.clip); continue }
    $m = $masters[$g.clip]
    if ($m.Keys -ne [int]$g.frames + 1) { $errors++; Write-Output ('KEYS {0}: master has {1} keys, the report has {2} frames: a loop has one more key than its frames (elephant_write_masters.py --rekey brings an older master there)' -f $g.clip, $m.Keys, $g.frames); continue }
    $isGait = [double]$g.loop_displacement -gt 0
    $slow = $(if ($IDLE_SLOW.ContainsKey($r.Prefix)) { $IDLE_SLOW[$r.Prefix] } else { 1.0 })
    $plan += [pscustomobject]@{
      Clip = $g.clip; Role = $r.Role; Template = $TEMPLATE[$r.Role]; Master = $m.Guid; Keys = $m.Keys; Last = $m.Keys - 1; Gait = $isGait
      Loop = $(if ($isGait) { [float]$g.loop_displacement } else { $null })
      Steps = $(if ($isGait) { @($g.step_points) } else { $null })
      Duration = $(if ($REAL_DURATION -contains $r.Role) { [float]([math]::Round(($m.Keys - 1) / $KEY_RATE, 3)) }
        elseif ($isGait) { $null } else { [float]([math]::Round([double]$g.source_seconds * $slow, 3)) })
      Limits = $null; NoUsage = $false; Sound = $null
    }
  }
}

function Get-Planned($p, $tplPath, $wantLim) {
  # The record the plan makes: a fresh load of the template with the plan's values. The writer saves it under a
  # new GUID, and Get-Problem holds a clip on disk to every simple field of it.
  $c = Get-AnimClip $tplPath
  $c.Name = $p.Clip; $c.Animation = $p.Master
  $c.Source1 = [float]0; $c.Source2 = [float]$p.Last
  if ($p.Gait) {
    $c.StepPoints = V4 $p.Steps[0] $p.Steps[1] $p.Steps[2] $p.Steps[3]
    foreach ($u in $c.ClipUsages) {
      if ($u.GetType().Name -eq 'QuadMovementUsage') {
        $u.LoopDisplacement = $p.Loop; $u.PaceSwitchLimitMin = $wantLim[0]; $u.PaceSwitchLimitMax = $wantLim[1]
      }
    }
  }
  if ($null -ne $p.Duration) { $c.Duration = $p.Duration }
  if ($p.Role -eq 'explicit') {
    if ($null -ne $p.Steps -and -not $p.Gait) { $c.StepPoints = V4 $p.Steps[0] $p.Steps[1] $p.Steps[2] $p.Steps[3] }
    if ($p.NoUsage) { $c.ClipUsages.Clear() }
    if ($null -ne $p.Sound) { $c.SoundCode = $p.Sound }
  }
  $c
}
function Get-SimpleDiffs($got, $want, $label, $skip) {
  # One line for each public property of a simple type whose value differs.
  foreach ($prop in $got.GetType().GetProperties()) {
    if (($skip -contains $prop.Name) -or $prop.GetIndexParameters().Count) { continue }
    $t = $prop.PropertyType
    if (-not ($t.IsPrimitive -or $t.IsEnum -or $t -eq [string] -or $t -eq [Guid] -or $t.Namespace -eq 'System.Numerics')) { continue }
    $a = $prop.GetValue($got, $null); $b = $prop.GetValue($want, $null)
    # typed, never as text: 0.3 and 0.300000042 are two floats and one string
    if (-not [object]::Equals($a, $b)) {
      $fmt = $(if ($a -is [single] -or $a -is [double]) { '{0} {1} is "{2:R}", planned "{3:R}"' } else { '{0} {1} is "{2}", planned "{3}"' })
      $fmt -f $label, $prop.Name, $a, $b
    }
  }
}
function Get-FieldDiffs($c, $want) {
  # The clip against the planned one: every simple property but its own GUID (priority, blends, voice and sound
  # codes, the range), its flags (a list of names: lock_movement, client_prediction, make_bodyfall_sound) and
  # every simple property of each clip usage. All of these come from the template, and a clip that differs in one
  # of them is not "the plan but for its range".
  Get-SimpleDiffs $c $want 'field' @('Guid')
  $got = (@($c.Flags) | Sort-Object) -join ','; $exp = (@($want.Flags) | Sort-Object) -join ','
  if ($got -ne $exp) { 'field Flags is "{0}", planned "{1}"' -f $got, $exp }
  $cu = @($c.ClipUsages); $wu = @($want.ClipUsages)
  if ($cu.Count -ne $wu.Count) { 'it carries {0} clip usage(s), planned {1}' -f $cu.Count, $wu.Count; return }
  for ($i = 0; $i -lt $cu.Count; $i++) {
    if ($cu[$i].GetType() -ne $wu[$i].GetType()) { 'usage {0} is a {1}, planned a {2}' -f $i, $cu[$i].GetType().Name, $wu[$i].GetType().Name; continue }
    Get-SimpleDiffs $cu[$i] $wu[$i] ('usage {0} field' -f $i) @()
  }
}

function Get-Problem($c, $p, $tplDuration, $wantLim, $want) {
  if ($null -eq $c) { return 'no AnimationClip item' }
  if ($null -ne $want) { Get-FieldDiffs $c $want }
  if ($c.Name -ne $p.Clip) { 'name is {0}' -f $c.Name }
  if ([Guid]$c.Animation -ne $p.Master) { 'Animation is {0}, the master is {1}' -f $c.Animation, $p.Master }
  if ([int]$c.Source1 -ne 0 -or [int]$c.Source2 -ne $p.Last) { 'range is {0}..{1}, planned 0..{2}' -f $c.Source1, $c.Source2, $p.Last }
  $want = $(if ($null -ne $p.Duration) { $p.Duration } else { $tplDuration })
  if ([math]::Abs([double]$c.Duration - $want) -gt 0.001) { 'Duration is {0}, planned {1}' -f $c.Duration, $want }
  $s = $c.StepPoints; $got = @($s.X, $s.Y, $s.Z, $s.W)
  if ($p.Role -eq 'explicit') {
    if ($null -ne $p.Steps -and -not $p.Gait) {     # a gait's step points are compared below
      for ($i = 0; $i -lt 4; $i++) { if ([math]::Abs($got[$i] - [double]$p.Steps[$i]) -gt 0.001) { 'StepPoints are {0}, planned {1}' -f $s, ($p.Steps -join ', '); break } }
    }
    if ($p.NoUsage -and @($c.ClipUsages).Count) { 'it carries {0} clip usage(s), planned none' -f @($c.ClipUsages).Count }
    if ($null -ne $p.Sound -and [string]$c.SoundCode -ne $p.Sound) { 'SoundCode is "{0}", planned "{1}"' -f $c.SoundCode, $p.Sound }
    $limit = ([double]$c.Duration - [double]$c.BlendOutPeriod) / [double]$c.Duration
    foreach ($v in $got) { if ($v -ge $limit) { 'sound point {0} is not below (Duration - BlendOutPeriod) / Duration = {1:N3}: the editor refuses it' -f $v, $limit; break } }
  }
  if ($p.Gait) {
    $lim = Get-Limits $c
    if ($null -eq $lim -or [math]::Abs($lim[0] - $wantLim[0]) -gt 0.001 -or [math]::Abs($lim[1] - $wantLim[1]) -gt 0.001) {
      'PaceSwitchLimits are {0}, planned {1}' -f ($lim -join '..'), ($wantLim -join '..')
    }
    $loop = Get-Loop $c
    if ($null -eq $loop -or [math]::Abs($loop - $p.Loop) -gt 0.001) { 'LoopDisplacement is {0}, planned {1}' -f $loop, $p.Loop }
    $s = $c.StepPoints; $got = @($s.X, $s.Y, $s.Z, $s.W)
    for ($i = 0; $i -lt 4; $i++) { if ([math]::Abs($got[$i] - [double]$p.Steps[$i]) -gt 0.001) { 'StepPoints are {0}, planned {1}' -f $s, ($p.Steps -join ', '); break } }
  }
}

$tplCache = @{}; $written = 0; $ok = 0   # record name -> @{ Duration; Limits }
function Get-Template($name) {
  if (-not $tplCache.ContainsKey($name)) {
    $path = Find-Template $name
    if ($null -eq $path) { return $null }
    $t = Get-AnimClip $path
    $s = $t.StepPoints
    $tplCache[$name] = @{ Duration = [double]$t.Duration; Limits = (Get-Limits $t); BlendOut = [double]$t.BlendOutPeriod; Steps = @($s.X, $s.Y, $s.Z, $s.W); HasUsage = ($null -ne (Get-Loop $t)) }
  }
  $tplCache[$name]
}
foreach ($p in $plan) {
  $tplPath = Find-Template $p.Template
  $limName = $(if ($LIMITS_FROM.ContainsKey($p.Role)) { $LIMITS_FROM[$p.Role] } else { $p.Template })
  $tpl = Get-Template $p.Template; $limTpl = Get-Template $limName
  if ($null -eq $tpl -or $null -eq $limTpl) { $errors++; Write-Output ('NO TEMPLATE {0} or {1} in {2}' -f $p.Template, $limName, ($Templates -join '; ')); continue }
  $wantLim = $(if ($null -ne $p.Limits) { $p.Limits } else { Get-WantLimits $p $limTpl.Limits })
  if ($p.Role -eq 'explicit' -and $p.Gait -and -not $tpl.HasUsage) {     # the writer sets travel on a usage that is there; it makes none
    $errors++
    Write-Output ('INVALID {0}: it claims travel ({1}) and its template {2} has no clip usage to carry it. Nothing was written for it' -f $p.Clip, $p.Loop, $p.Template)
    continue
  }
  if ($p.Role -eq 'explicit') {     # the editor's sound point rule, on the planned values, before any write
    $steps = $(if ($null -ne $p.Steps) { $p.Steps } else { $tpl.Steps })
    $limit = ([double]$p.Duration - $tpl.BlendOut) / [double]$p.Duration
    $late = @($steps | Where-Object { [double]$_ -ge $limit })
    if ($late.Count) {
      $errors++
      Write-Output ('INVALID {0}: sound point {1} is not below (Duration {2} - BlendOutPeriod {3}) / Duration = {4:N3}; the editor refuses it. Nothing was written for it' -f $p.Clip, $late[0], $p.Duration, $tpl.BlendOut, $limit)
      continue
    }
  }
  $out = Join-Path $Folder ($p.Clip + '_anm.tpac')
  if (-not (Test-Path $out)) {
    if (-not $Apply) {
      if ($Verify) { $errors++; Write-Output ('MISSING {0}' -f $p.Clip) }
      else { Write-Output ('PLAN   {0,-30} <- {1,-24} keys {2,-3} loop {3} steps {4} duration {5} limits {6}' -f $p.Clip, $p.Template, $p.Keys, $p.Loop, ($p.Steps -join ' '), $(if ($null -ne $p.Duration) { $p.Duration } else { $tpl.Duration }), ($wantLim -join '..')) }
      continue
    }
    $c = Get-Planned $p $tplPath $wantLim   # a fresh load per clip: nothing leaks from one clip into the next
    $c.Guid = [Guid]::NewGuid()
    $c.TypelessDataSegments.Clear(); $c.UnknownDependences.Clear()
    $pkg = New-Object TpacTool.Lib.AssetPackage
    $pkg.Guid = [Guid]::NewGuid()
    $pkg.Items.Add($c)
    $pkg.Save($out, $false, 2)   # path, useOriginalChecksum, tpac version
    $written++
  }
  $planned = Get-Planned $p $tplPath $wantLim
  $problems = @(Get-Problem (Get-AnimClip $out) $p $tpl.Duration $wantLim $planned)
  # in place only when every difference is the range or the limits: any other field that differs is DIFFERS
  if ($problems.Count -and -not ($problems | Where-Object { $_ -notlike 'PaceSwitchLimits*' -and $_ -notlike 'range is*' -and $_ -notlike 'field Source1 is*' -and $_ -notlike 'field Source2 is*' -and $_ -notlike 'usage * field PaceSwitchLimit*' })) {
    if ($Apply) {                         # the one in-place rewrite: both GUIDs stay; the Kit cooks the entry again
      $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
      if ($BackupDir) {
        New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
        Copy-Item -LiteralPath $out -Destination (Join-Path $BackupDir ('{0}_anm.tpac.before-{1}' -f $p.Clip, $stamp)) -ErrorAction Stop
      } else {
        Copy-Item -LiteralPath $out -Destination ('{0}.bak-limits-{1}' -f $out, $stamp) -ErrorAction Stop
      }
      $old = (Load $out).Package
      $c = @($old.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' })[0]
      $c.Source1 = [float]0; $c.Source2 = [float]$p.Last
      if ($p.Gait) {
        foreach ($u in $c.ClipUsages) {
          if ($u.GetType().Name -eq 'QuadMovementUsage') { $u.PaceSwitchLimitMin = $wantLim[0]; $u.PaceSwitchLimitMax = $wantLim[1] }
        }
      }
      $c.TypelessDataSegments.Clear(); $c.UnknownDependences.Clear()
      $pkg = New-Object TpacTool.Lib.AssetPackage
      $pkg.Guid = $old.Guid
      $pkg.Items.Add($c)
      $pkg.Save($out, $false, 2)
      $written++
      $problems = @(Get-Problem (Get-AnimClip $out) $p $tpl.Duration $wantLim $planned)
    } elseif (-not $Verify) {
      Write-Output ('IN PLACE {0}: {1} (-Apply rewrites it in place)' -f $p.Clip, ($problems -join '; ')); continue
    }
  }
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
