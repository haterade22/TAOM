param(
  [Parameter(Mandatory = $true)][string]$Masters,
  [string]$SkeletonGuid = '1163bb17-777d-49b1-b083-aad79dc544fd',
  [int]$BoneNum = 0,
  [switch]$Apply,
  [string]$TpacBin = 'E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin',
  [string]$Loader = 'E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs'
)
<#
Census, and on -Apply repair, of the Skeleton reference in every Kit-compiled SkeletalAnimation master under a
folder (recursive, *_geo.tpac). Batch form of the master half of wire_anim_master_clip.ps1, written for the
Animalia elk and moose clips (#646, 2026-09-23), which the Kit imported onto horse_skeleton.

Why: the Kit can leave a master's Skeleton GUID EMPTY (the troll's 52 masters, 2026-09-17; the war ram's
re-import, 2026-09-18), and a master that names no skeleton is not tied to the rig the action set plays it on.
Defaults target horse_skeleton (GUID 1163bb17-777d-49b1-b083-aad79dc544fd, the values in
tools/blender/horse_skeleton_engine.json); pass -SkeletonGuid for another rig (human_skeleton:
dd7f3586-10ea-47d5-880e-a0c263862217). The target rig's BoneNum (the Kit's count in each master: horse 32, human 28,
measured over the live Armory 2026-09-23; -BoneNum for any other rig) is the rig check: a folder can mix rigs (the
warg, elephant and chariot folders hold rider clips on human_skeleton beside the creature's own), and an EMPTY master
with another BoneNum is reported WRONG RIG and never patched. It tells rigs apart only where their counts differ: the
horse's 32 is unique in the Armory, the chariot and the elephant share 60. The patch offset is then found from that
BoneNum.

Per package it reports:
  ok             the master names -SkeletonGuid
  EMPTY          the master names no skeleton and has the target rig's BoneNum: -Apply patches it
  WRONG RIG      the master names no skeleton but has another BoneNum: reported, never patched
  OTHER          the master names a different skeleton: reported, never patched (a stray copy such as
                 horse_skeleton_notused.001 is fixed in the Kit, not here)
  NO ANIMATION   the package holds no SkeletalAnimation (the troll failure: Skeleton + Geometry only, the clip
                 then reports 'assigned skeleton animation not found'); re-import that FBX as an animation
  STRAY SKELETON the package also holds a Skeleton item (a copy the Kit built from the FBX); delete it in the Kit

-Apply refuses while Bannerlord or the Modding Kit is running (Assert-GameAndKitClosed): the Kit scans packages
at startup and a save would rewrite what it loaded. With both closed, the empty GUID is the 16 zero bytes right
before BoneNum (int32) + Duration (int32) in the metadata. It is patched IN PLACE to -SkeletonGuid after a
write-once .bak-preskel copy, the file is re-read and asserted (skeleton, duration, name; on a failed read-back the
original bytes are written back), then
tools/tpac_fix_item_checksums.py --apply refreshes the patched files' item checksums (the Kit's xxHash64).
TpacTool's Save is not used on masters: it zeroes the post-meta checksum fields. Same patch as
gen_troll_anim_clips.ps1 and wire_anim_master_clip.ps1. Open the Kit once afterwards so it re-reads them.

Exit 1 when anything but 'ok' remains; exit 2 when it cannot run (game or Kit open, not Windows PowerShell, a tool
path missing, a rig with no known BoneNum). Windows PowerShell 5.1 (TolerantTpacLoader.cs is .NET Framework):
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\wire_anim_master_skeletons.ps1 -Masters <dir> [-Apply]
#>
$ErrorActionPreference = 'Stop'

function Assert-GameAndKitClosed {
  $procs = @(Get-Process -Name 'Bannerlord*', 'TaleWorlds.MountAndBlade*' -ErrorAction SilentlyContinue)
  if ($procs.Count -gt 0) {
    Write-Output ("REFUSED: Bannerlord or the Modding Kit is running (" + (($procs | ForEach-Object { $_.ProcessName }) -join ', ') + "); close it and re-run")
    exit 2
  }
}
if ($Apply) { Assert-GameAndKitClosed }
if ($PSVersionTable.PSEdition -ne 'Desktop') {
  Write-Output 'ERROR: run under Windows PowerShell 5.1 (powershell.exe); TolerantTpacLoader.cs targets .NET Framework'
  exit 2
}
foreach ($need in @("$TpacBin\TpacTool.Lib.dll", $Loader)) {
  if (-not (Test-Path $need)) { Write-Output "ERROR: $need not found (pass -TpacBin / -Loader)"; exit 2 }
}
# The Kit's BoneNum per rig in its animation masters (measured over the live Armory, 2026-09-23).
$RIG_BONES = @{ '1163bb17-777d-49b1-b083-aad79dc544fd' = 32; 'dd7f3586-10ea-47d5-880e-a0c263862217' = 28 }
if ($BoneNum -le 0) {
  $key = ([Guid]$SkeletonGuid).ToString()
  if (-not $RIG_BONES.ContainsKey($key)) { Write-Output "ERROR: no known BoneNum for skeleton $SkeletonGuid; pass -BoneNum"; exit 2 }
  $BoneNum = $RIG_BONES[$key]
}

Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
$EMPTY = [Guid]::Empty
$TARGET = [Guid]$SkeletonGuid

function Find-SkeletonField([byte[]]$bytes, [int]$boneCount, [int]$frames) {
  $tail = [byte[]](@(0) * 16) + [BitConverter]::GetBytes([int]$boneCount) + [BitConverter]::GetBytes([int]$frames)
  $hits = @()
  for ($i = 0; $i -le $bytes.Length - $tail.Length; $i++) {
    $ok = $true
    for ($j = 0; $j -lt $tail.Length; $j++) { if ($bytes[$i + $j] -ne $tail[$j]) { $ok = $false; break } }
    if ($ok) { $hits += $i }
  }
  return $hits
}

$counts = @{ ok = 0; EMPTY = 0; 'WRONG RIG' = 0; OTHER = 0; 'NO ANIMATION' = 0; 'STRAY SKELETON' = 0 }
$toPatch = @()
$files = Get-ChildItem -Path $Masters -Recurse -Filter '*_geo.tpac' | Sort-Object FullName
foreach ($f in $files) {
  $rel = $f.FullName.Substring($Masters.TrimEnd('\').Length + 1)
  $items = @((Load $f.FullName).Package.Items)
  $skels = @($items | Where-Object { $_.GetType().Name -eq 'Skeleton' })
  $anims = @($items | Where-Object { $_.GetType().Name -eq 'SkeletalAnimation' })
  if ($skels.Count -gt 0) {
    $counts['STRAY SKELETON']++
    Write-Output ("STRAY SKELETON  {0}: {1}" -f $rel, (($skels | ForEach-Object { $_.Name }) -join ', '))
  }
  if ($anims.Count -eq 0) {
    $counts['NO ANIMATION']++
    Write-Output ("NO ANIMATION    {0}: items = {1}" -f $rel, (($items | ForEach-Object { $_.GetType().Name + ' ' + $_.Name }) -join ', '))
    continue
  }
  foreach ($a in $anims) {
    if ($a.Skeleton -eq $TARGET) { $counts.ok++; continue }
    if ($a.Skeleton -eq $EMPTY) {
      if ([int]$a.BoneNum -ne $BoneNum) {
        $counts['WRONG RIG']++
        Write-Output ("WRONG RIG       {0}: {1} has {2} bones, the target rig {3}; not patched" -f $rel, $a.Name, $a.BoneNum, $BoneNum)
        continue
      }
      $counts.EMPTY++
      $toPatch += [pscustomobject]@{ File = $f.FullName; Rel = $rel; Name = $a.Name; Frames = [int]$a.Duration; BoneNum = [int]$a.BoneNum }
      Write-Output ("EMPTY           {0}: {1} ({2} frames, {3} bones)" -f $rel, $a.Name, $a.Duration, $a.BoneNum)
    } else {
      $counts.OTHER++
      Write-Output ("OTHER           {0}: {1} names skeleton {2}" -f $rel, $a.Name, $a.Skeleton)
    }
  }
}
Write-Output ("census: {0} packages   ok={1} EMPTY={2} WRONG_RIG={3} OTHER={4} NO_ANIMATION={5} STRAY_SKELETON={6}" -f `
  $files.Count, $counts.ok, $counts.EMPTY, $counts['WRONG RIG'], $counts.OTHER, $counts['NO ANIMATION'], $counts['STRAY SKELETON'])

if (-not $Apply) {
  if ($toPatch.Count -gt 0) {
    $findable = @($toPatch | Where-Object { (Find-SkeletonField ([IO.File]::ReadAllBytes($_.File)) $_.BoneNum $_.Frames).Count -eq 1 }).Count
    Write-Output ("dry run: {0} of {1} EMPTY masters have exactly one patch offset (each master's own BoneNum)" -f $findable, $toPatch.Count)
  }
  if ($counts.ok -eq ($files.Count) -and $counts['STRAY SKELETON'] -eq 0) { exit 0 } else { exit 1 }
}

$patched = @(); $fail = 0
foreach ($m in $toPatch) {
  $bytes = [IO.File]::ReadAllBytes($m.File)
  $hits = Find-SkeletonField $bytes $m.BoneNum $m.Frames
  if ($hits.Count -ne 1) { $fail++; Write-Output ("PATCH FAIL {0}: {1} candidate offsets" -f $m.Rel, $hits.Count); continue }
  $bak = $m.File + '.bak-preskel'
  if (-not (Test-Path $bak)) { Copy-Item $m.File $bak }
  $orig = [byte[]]$bytes.Clone()
  [Array]::Copy($TARGET.ToByteArray(), 0, $bytes, $hits[0], 16)
  [IO.File]::WriteAllBytes($m.File, $bytes)
  $sa = @((Load $m.File).Package.Items | Where-Object { $_.GetType().Name -eq 'SkeletalAnimation' })[0]
  if ($null -eq $sa -or $sa.Skeleton -ne $TARGET -or [int]$sa.Duration -ne $m.Frames -or $sa.Name -ne $m.Name) {
    # The write-once .bak-preskel can hold an older import, so the bytes read at the start of this run go back.
    [IO.File]::WriteAllBytes($m.File, $orig)
    $fail++; Write-Output ("VERIFY FAIL {0}: skeleton={1} duration={2}; original bytes restored" -f $m.Rel, $sa.Skeleton, $sa.Duration); continue
  }
  $patched += $m.File
}
foreach ($p in $patched) {
  & python "$PSScriptRoot\tpac_fix_item_checksums.py" $p --apply | Out-Null
  if ($LASTEXITCODE -ne 0) { $fail++; Write-Output ("CHECKSUM FAIL {0}" -f $p) }
}
Write-Output ("apply: {0} masters patched to {1}, re-read OK, checksums refreshed; {2} failed" -f $patched.Count, $TARGET, $fail)
if ($fail -gt 0 -or $counts['WRONG RIG'] -gt 0 -or $counts.OTHER -gt 0 -or $counts['NO ANIMATION'] -gt 0 -or $counts['STRAY SKELETON'] -gt 0) { exit 1 } else { exit 0 }
