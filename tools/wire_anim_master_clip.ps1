param(
  [Parameter(Mandatory = $true)][string]$Master,
  [Parameter(Mandatory = $true)][string]$Clip,
  [string]$SkeletonGuid = '1163bb17-777d-49b1-b083-aad79dc544fd',
  [double]$Fps = 30.0,
  [switch]$Apply,
  [string]$TpacBin = 'E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin',
  [string]$Loader = 'E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs'
)
<#
Wire one Kit-compiled SkeletalAnimation master to one AnimationClip after a reimport made a fresh master.

A Kit reimport keeps a master's GUID only while the FBX take name matches (2026-09-18: a `.001` take became a
NEW master with a new GUID and an EMPTY skeleton, and the clip that pointed at the old GUID lost its animation).
This does what the Kit UI would take three clicks and a save for, on disk, with the Kit CLOSED:
  master: if Skeleton is the empty GUID, patch it IN PLACE to -SkeletonGuid (default horse_skeleton). The empty
          GUID is 16 zero bytes right before BoneNum (int32) + Duration (int32) in the metadata; .bak-preskel copy
          first; TpacTool's Save is not used on masters (it zeroes checksum fields). Same patch as
          gen_troll_anim_clips.ps1.
  clip:   Animation = master GUID, Source1 = 1, Source2 = master Duration - 1 (frame 0 is the rest frame),
          Duration = (Source2 - Source1 + 1) / Fps. The clip's own GUID and its PACKAGE GUID are kept, so its
          RuntimeDataCache entry keeps its NAME, and the Kit re-cooks it on its next load (measured 2026-09-18:
          rewired 14:04 with the Kit closed, fresh .rdc at 14:11:55, 23 s after the Kit started). Open the Kit
          once before any in-game test. Flags, priorities and usages untouched.
  then tools/tpac_fix_item_checksums.py --apply over both files (TpacTool writes zero item checksums).
Backups (.bak-preskel, .bak-prewire) are written once and never overwritten, so a second -Apply keeps the
ORIGINAL file; delete the backup on purpose to take a new one.
The re-saved clip is NOT byte-identical to a Kit-saved one, measured 2026-09-18: TpacTool writes the item
version word as 5 (the Kit writes 6) and omits the Kit's trailing dependency list (count 1 + the master's GUID
+ padding, 48 bytes), and the field dump cannot see either. Neither is load-bearing: all 24 chariot and 44
elephant clips ship at version 5, and all 24 spider and 24 chariot clips ship without the list, and all of them
play in game.
Dry run by default; -Apply writes. Windows PowerShell 5.1 (TolerantTpacLoader.cs is .NET Framework):
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\wire_anim_master_clip.ps1 -Master <x_geo.tpac> -Clip <y_anm.tpac> [-Apply]
#>
$ErrorActionPreference = 'Stop'
Add-Type -Path "$TpacBin\TpacTool.Lib.dll"
Add-Type -Path $Loader -ReferencedAssemblies @("$TpacBin\TpacTool.Lib.dll", 'System.dll', 'System.Core.dll')
function Load($p) { [ChariotExtract.TolerantTpacLoader]::Load($p) }
$EMPTY = '00000000-0000-0000-0000-000000000000'

# ---- master
$mr = Load $Master
$sa = $mr.Package.Items | Where-Object { $_.GetType().Name -eq 'SkeletalAnimation' } | Select-Object -First 1
if ($null -eq $sa) { throw "no SkeletalAnimation in $Master" }
$frames = [int]$sa.Duration
Write-Output ("master {0}  guid={1}  skeleton={2}  bones={3}  duration={4}" -f $sa.Name, $sa.Guid, $sa.Skeleton, $sa.BoneNum, $frames)
if ("$($sa.Skeleton)" -eq $EMPTY) {
  $bytes = [IO.File]::ReadAllBytes($Master)
  $tail = [byte[]](@(0) * 16) + [BitConverter]::GetBytes([int]$sa.BoneNum) + [BitConverter]::GetBytes([int]$frames)
  $hits = @()
  for ($i = 0; $i -le $bytes.Length - $tail.Length; $i++) {
    $ok = $true
    for ($j = 0; $j -lt $tail.Length; $j++) { if ($bytes[$i + $j] -ne $tail[$j]) { $ok = $false; break } }
    if ($ok) { $hits += $i }
  }
  if ($hits.Count -ne 1) { throw ("skeleton field: expected exactly 1 match of 16 zero bytes + BoneNum + Duration, found {0}" -f $hits.Count) }
  if ($Apply) {
    if (-not (Test-Path -LiteralPath "$Master.bak-preskel")) { [IO.File]::Copy($Master, "$Master.bak-preskel") }
    $g = ([Guid]$SkeletonGuid).ToByteArray()
    [Array]::Copy($g, 0, $bytes, $hits[0], 16)
    [IO.File]::WriteAllBytes($Master, $bytes)
    $chk = (Load $Master).Package.Items | Where-Object { $_.GetType().Name -eq 'SkeletalAnimation' } | Select-Object -First 1
    if ("$($chk.Skeleton)" -ne ([Guid]$SkeletonGuid).ToString()) { throw "skeleton patch did not read back" }
    Write-Output ("  skeleton patched in place at offset {0} -> {1} (backup .bak-preskel)" -f $hits[0], $SkeletonGuid)
  } else { Write-Output ("  skeleton EMPTY: would patch offset {0} -> {1}" -f $hits[0], $SkeletonGuid) }
} else { Write-Output "  skeleton already set, left alone" }

# ---- clip
$cr = Load $Clip
$cl = $cr.Package.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' } | Select-Object -First 1
if ($null -eq $cl) { throw "no AnimationClip in $Clip" }
$pkgGuid = $cr.Package.Guid
Write-Output ("clip {0}  guid={1}  package={2}  animation={3}  source={4}..{5}  duration={6}" -f $cl.Name, $cl.Guid, $pkgGuid, $cl.Animation, $cl.Source1, $cl.Source2, $cl.Duration)
$newS2 = [float]($frames - 1)
$newDur = [float]([math]::Round(($newS2 - 1 + 1) / $Fps, 2))
Write-Output ("  -> animation={0}  source=1..{1}  duration={2}" -f $sa.Guid, $newS2, $newDur)
if ($Apply) {
  $cl.Animation = [Guid]$sa.Guid
  $cl.Source1 = [float]1
  $cl.Source2 = $newS2
  $cl.Duration = $newDur
  $pkg = New-Object TpacTool.Lib.AssetPackage
  $pkg.Guid = $pkgGuid
  $pkg.Items.Add($cl)
  if (-not (Test-Path -LiteralPath "$Clip.bak-prewire")) { [IO.File]::Copy($Clip, "$Clip.bak-prewire") }
  $pkg.Save($Clip, 2)
  $chk = (Load $Clip).Package
  $c2 = $chk.Items | Where-Object { $_.GetType().Name -eq 'AnimationClip' } | Select-Object -First 1
  if ("$($c2.Animation)" -ne "$($sa.Guid)" -or "$($chk.Guid)" -ne "$pkgGuid" -or "$($c2.Guid)" -ne "$($cl.Guid)") { throw "clip did not read back as written" }
  Write-Output ("  clip written (package GUID and clip GUID kept; backup .bak-prewire)")
  $py = Get-Command python -ErrorAction SilentlyContinue
  if ($py) {
    & $py.Source "$PSScriptRoot\tpac_fix_item_checksums.py" ([IO.Path]::GetDirectoryName($Master)) --glob ([IO.Path]::GetFileName($Master)) --apply | Select-Object -Last 1
    & $py.Source "$PSScriptRoot\tpac_fix_item_checksums.py" ([IO.Path]::GetDirectoryName($Clip)) --glob ([IO.Path]::GetFileName($Clip)) --apply | Select-Object -Last 1
  } else { Write-Output "python not on PATH: run tools/tpac_fix_item_checksums.py on both files --apply" }
} else { Write-Output "DRY RUN (no writes); add -Apply" }
