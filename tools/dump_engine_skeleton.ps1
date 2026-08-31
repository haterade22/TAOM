<#
.SYNOPSIS
  Dump a Bannerlord engine skeleton (bone names, parents, rest frames) to JSON via TpacTool.Lib.

.DESCRIPTION
  THE POINT: an animation must be authored against the skeleton the ENGINE will play it on, not
  against a mesh FBX. Skinning uses bind matrices and is roll-independent, so a mesh rig can carry
  arbitrary bone orientations and still deform correctly in game. Rotations are NOT roll-independent,
  so an animation authored on a mesh rig comes out twisted even though it looks perfect in Blender.

  That cost a full day on the war ram (2026-08-29). SK_EB_Goat_A.fbx parents horseneck1 to
  horsetail3 and lays bones along Blender's Y; the real horse_skeleton parents it to horsespine3 and
  lays every child at +length along its PARENT'S X axis. Both facts are visible only here.

  Skeletons live in Native/AssetPackages/skeletons.tpac (which TpacTool parses fine). Note that the
  per-creature rig packages do NOT parse -- pack_horse_customrig, animations_horse_and_rider,
  animation_clips.tpac and Assets.tpac all throw "Frames not equal" or "capacity was less than the
  current size". skeletons.tpac is the one that works, and it holds them all.

.PARAMETER Skeleton   Skeleton asset name, e.g. horse_skeleton, elephant_skeleton, skeleton_warg.
.PARAMETER List       List every skeleton in the package instead of dumping one.
.PARAMETER OutFile    JSON destination.

.EXAMPLE
  pwsh tools/dump_engine_skeleton.ps1 -List
  pwsh tools/dump_engine_skeleton.ps1 -Skeleton horse_skeleton -OutFile horse_skeleton.json

.NOTES
  Rest frames are row-vector (rows are the basis vectors, M41..M43 the offset). To use one in
  Blender, transpose the 3x3 and move the offset to the last column; see
  docs/reference/bannerlord-skeleton-authoring.md.
#>
param(
  [string]$Skeleton = "",
  [switch]$List,
  [string]$OutFile = "",
  [string]$TpacToolBin = "E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin",
  [string]$NativeDir   = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native"
)
$ErrorActionPreference = "Stop"

if (-not (Test-Path $TpacToolBin)) { throw "TpacTool not found at $TpacToolBin" }
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
  param($s,$e); $n=($e.Name -split ',')[0].Trim(); $p=Join-Path $TpacToolBin "$n.dll"
  if (Test-Path $p) { [System.Reflection.Assembly]::LoadFrom($p) } else { $null } })
$lib = [System.Reflection.Assembly]::LoadFrom((Join-Path $TpacToolBin "TpacTool.Lib.dll"))
$APt = $lib.GetType("TpacTool.Lib.AssetPackage")

$pkgFile = Get-ChildItem -Path $NativeDir -Filter "skeletons.tpac" -Recurse | Select-Object -First 1
if (-not $pkgFile) { throw "skeletons.tpac not found under $NativeDir" }
$pkg = [Activator]::CreateInstance($APt, [object[]]@([string]$pkgFile.FullName, $true, $true))
$all = @($pkg.Items | Where-Object { $_.GetType().Name -eq "Skeleton" })

if ($List -or -not $Skeleton) {
  # *_notused entries are export-side duplicates; the real asset is the bare name
  $all | ForEach-Object { $_.Name } | Where-Object { $_ -notmatch '_notused$' } |
    Sort-Object -Unique | ForEach-Object { $_ }
  return
}

$sk = $all | Where-Object { $_.Name -eq $Skeleton } | Select-Object -First 1
if (-not $sk) { throw "skeleton '$Skeleton' not found. Run with -List to see what is available." }

$rows = @()
foreach ($b in $sk.Definition.Data.Bones) {
  $m = $b.RestFrame
  $rows += [pscustomobject]@{
    name   = $b.Name
    parent = $(if ($b.Parent) { $b.Parent.Name } else { "<root>" })
    m      = @($m.M11,$m.M12,$m.M13, $m.M21,$m.M22,$m.M23, $m.M31,$m.M32,$m.M33, $m.M41,$m.M42,$m.M43)
  }
}
Write-Host "$Skeleton : $($rows.Count) bones"
if (-not $OutFile) { $OutFile = "$Skeleton.json" }
$rows | ConvertTo-Json -Depth 5 | Set-Content -Path $OutFile -Encoding UTF8
Write-Host "wrote $OutFile"
