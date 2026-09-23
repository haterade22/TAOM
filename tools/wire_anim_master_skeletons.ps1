param(
  [Parameter(Mandatory = $true)][string]$Masters,
  [string]$SkeletonGuid = '1163bb17-777d-49b1-b083-aad79dc544fd',
  [int]$BoneCount = 32,
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
Defaults target horse_skeleton (GUID 1163bb17-777d-49b1-b083-aad79dc544fd, bone count 32, the values in
tools/blender/horse_skeleton_engine.json); pass -SkeletonGuid / -BoneCount for another rig (human_skeleton:
dd7f3586-10ea-47d5-880e-a0c263862217, 28).

Per package it reports:
  ok             the master names -SkeletonGuid
  EMPTY          the master names no skeleton: -Apply patches it
  OTHER          the master names a different skeleton: reported, never patched (a stray copy such as
                 horse_skeleton_notused.001 is fixed in the Kit, not here)
  NO ANIMATION   the package holds no SkeletalAnimation (the troll failure: Skeleton + Geometry only, the clip
                 then reports 'assigned skeleton animation not found'); re-import that FBX as an animation
  STRAY SKELETON the package also holds a Skeleton item (a copy the Kit built from the FBX); delete it in the Kit

-Apply, with the Kit CLOSED (it scans packages at startup and a save rewrites what it loaded): the empty GUID is
the 16 zero bytes right before BoneCount (int32) + Duration (int32) in the metadata. It is patched IN PLACE to
-SkeletonGuid after a write-once .bak-preskel copy, the file is re-read and asserted (skeleton, duration, name),
then tools/tpac_fix_item_checksums.py --apply refreshes the patched files' item checksums (the Kit's xxHash64).
TpacTool's Save is not used on masters: it zeroes the post-meta checksum fields. Same patch as
gen_troll_anim_clips.ps1 and wire_anim_master_clip.ps1. Open the Kit once afterwards so it re-reads them.

Exit 1 when anything but 'ok' remains. Windows PowerShell 5.1 (TolerantTpacLoader.cs is .NET Framework):
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\wire_anim_master_skeletons.ps1 -Masters <dir> [-Apply]
#>
$ErrorActionPreference = 'Stop'
Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
$EMPTY = [Guid]::Empty
$TARGET = [Guid]$SkeletonGuid

function Find-SkeletonField([byte[]]$bytes, [int]$frames) {
  $tail = [byte[]](@(0) * 16) + [BitConverter]::GetBytes([int]$BoneCount) + [BitConverter]::GetBytes([int]$frames)
  $hits = @()
  for ($i = 0; $i -le $bytes.Length - $tail.Length; $i++) {
    $ok = $true
    for ($j = 0; $j -lt $tail.Length; $j++) { if ($bytes[$i + $j] -ne $tail[$j]) { $ok = $false; break } }
    if ($ok) { $hits += $i }
  }
  return $hits
}

$counts = @{ ok = 0; EMPTY = 0; OTHER = 0; 'NO ANIMATION' = 0; 'STRAY SKELETON' = 0 }
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
      $counts.EMPTY++
      $toPatch += [pscustomobject]@{ File = $f.FullName; Rel = $rel; Name = $a.Name; Frames = [int]$a.Duration }
      Write-Output ("EMPTY           {0}: {1} ({2} frames)" -f $rel, $a.Name, $a.Duration)
    } else {
      $counts.OTHER++
      Write-Output ("OTHER           {0}: {1} names skeleton {2}" -f $rel, $a.Name, $a.Skeleton)
    }
  }
}
Write-Output ("census: {0} packages   ok={1} EMPTY={2} OTHER={3} NO_ANIMATION={4} STRAY_SKELETON={5}" -f `
  $files.Count, $counts.ok, $counts.EMPTY, $counts.OTHER, $counts['NO ANIMATION'], $counts['STRAY SKELETON'])

if (-not $Apply) {
  if ($toPatch.Count -gt 0) {
    $findable = @($toPatch | Where-Object { (Find-SkeletonField ([IO.File]::ReadAllBytes($_.File)) $_.Frames).Count -eq 1 }).Count
    Write-Output ("dry run: {0} of {1} EMPTY masters have exactly one patch offset (BoneCount {2})" -f $findable, $toPatch.Count, $BoneCount)
  }
  if ($counts.ok -eq ($files.Count) -and $counts['STRAY SKELETON'] -eq 0) { exit 0 } else { exit 1 }
}

$patched = @(); $fail = 0
foreach ($m in $toPatch) {
  $bytes = [IO.File]::ReadAllBytes($m.File)
  $hits = Find-SkeletonField $bytes $m.Frames
  if ($hits.Count -ne 1) { $fail++; Write-Output ("PATCH FAIL {0}: {1} candidate offsets" -f $m.Rel, $hits.Count); continue }
  $bak = $m.File + '.bak-preskel'
  if (-not (Test-Path $bak)) { Copy-Item $m.File $bak }
  [Array]::Copy($TARGET.ToByteArray(), 0, $bytes, $hits[0], 16)
  [IO.File]::WriteAllBytes($m.File, $bytes)
  $sa = @((Load $m.File).Package.Items | Where-Object { $_.GetType().Name -eq 'SkeletalAnimation' })[0]
  if ($null -eq $sa -or $sa.Skeleton -ne $TARGET -or [int]$sa.Duration -ne $m.Frames -or $sa.Name -ne $m.Name) {
    $fail++; Write-Output ("VERIFY FAIL {0}: skeleton={1} duration={2}" -f $m.Rel, $sa.Skeleton, $sa.Duration); continue
  }
  $patched += $m.File
}
foreach ($p in $patched) {
  & python "$PSScriptRoot\tpac_fix_item_checksums.py" $p --apply | Out-Null
  if ($LASTEXITCODE -ne 0) { $fail++; Write-Output ("CHECKSUM FAIL {0}" -f $p) }
}
Write-Output ("apply: {0} masters patched to {1}, re-read OK, checksums refreshed; {2} failed" -f $patched.Count, $TARGET, $fail)
if ($fail -gt 0 -or $counts.OTHER -gt 0 -or $counts['NO ANIMATION'] -gt 0 -or $counts['STRAY SKELETON'] -gt 0) { exit 1 } else { exit 0 }
