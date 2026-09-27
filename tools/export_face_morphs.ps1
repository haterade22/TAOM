<#
.SYNOPSIS
  Read-only: write a compiled head's LOD0 face base mesh and eye mesh, base positions and every morph frame, to JSON.

.DESCRIPTION
  Loads a `_geo.tpac` with TpacTool.Lib (no Kit, no game) and, for the metamesh named by -Metamesh, writes
  {"face_base_mesh": {...}, "face_eye_mesh": {...}}, each with "name", "positions" ([x,y,z] per vertex) and
  "frames" (one flat [x0,y0,z0,x1,...] array per morph channel, absolute positions, in the package's order).
  The sub-meshes are found by their Kit tags, not their names or order. Used by tools/check_eye_follow.py.
  The engine reads the compiled package, so this is the truth after a Kit re-import; an FBX can say otherwise.

.EXAMPLE
  pwsh tools/export_face_morphs.ps1 -Package "<Armory>\Assets\Race Test\dwarf\sk_dwarf_bm_f1_geo.tpac" `
       -Metamesh sk_dwarf_bm_f1_head -Out head.json

.NOTES
  -TpacToolBin defaults to this machine's TpacTool 0.4.0 build; pass another path elsewhere.
  2026-09-26, the female dwarf's eyes (docs/reference/race-face-and-hand-morphs.md).
#>
param(
  [Parameter(Mandatory=$true)][string]$Package,
  [Parameter(Mandatory=$true)][string]$Metamesh,
  [Parameter(Mandatory=$true)][string]$Out,
  [string]$TpacToolBin = "E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin"
)
$ErrorActionPreference = "Stop"
$script:BINDIR = $TpacToolBin
[System.AppDomain]::CurrentDomain.add_AssemblyResolve([System.ResolveEventHandler]{
  param($s,$e); $n=($e.Name -split ',')[0].Trim(); $p=Join-Path $script:BINDIR "$n.dll"
  if (Test-Path $p) { return [Reflection.Assembly]::LoadFrom($p) }; return $null })
$asm=[Reflection.Assembly]::LoadFrom((Join-Path $TpacToolBin "TpacTool.Lib.dll"))
try { $types=$asm.GetTypes() } catch { $types=$_.Exception.InnerException.Types | Where-Object {$_} }
$APt=$types | Where-Object { $_.FullName -eq "TpacTool.Lib.AssetPackage" }
$pkg=[Activator]::CreateInstance($APt,[object[]]@([string]$Package,$true,$false))
$result = @{}
foreach($it in $pkg.Items){
  if($it.Name -ne $Metamesh){ continue }
  foreach($m in $it.Meshes){
    if($m.Lod -ne 0){ continue }
    $tag = (@($m.MaterialFlags) -join "+")
    if($tag -ne "face_base_mesh" -and $tag -ne "face_eye_mesh"){ continue }
    $ed = $m.EditData.Data
    $pos = @(); foreach($p in $ed.Positions){ $pos += ,@([double]$p.X,[double]$p.Y,[double]$p.Z) }
    $frames = @()
    foreach($fr in $ed.MorphFrames){
      $f = New-Object 'double[]' ($fr.Positions.Length * 3)
      for($i=0; $i -lt $fr.Positions.Length; $i++){ $q=$fr.Positions[$i]; $f[3*$i]=$q.X; $f[3*$i+1]=$q.Y; $f[3*$i+2]=$q.Z }
      $frames += ,$f
    }
    $result[$tag] = @{ name = $m.Name; positions = $pos; frames = $frames }
  }
}
if(-not $result.ContainsKey("face_base_mesh") -or -not $result.ContainsKey("face_eye_mesh")){
  throw "metamesh $Metamesh in $Package has no LOD0 face_base_mesh and face_eye_mesh pair"
}
$result | ConvertTo-Json -Depth 5 -Compress | Set-Content -Path $Out -Encoding utf8
"wrote $Out"
