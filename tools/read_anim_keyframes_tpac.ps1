<#
.SYNOPSIS
  Read Bannerlord SkeletalAnimation keyframes from a tpac to JSON via TpacTool.Lib — NO assimp.

.DESCRIPTION
  TpacTool's assimp FBX *exporter* access-violates (0xC0000005) when driven headlessly, so we cannot
  go tpac->FBX. But TpacTool.Lib can READ the raw keyframe data with no assimp at all (same data layer
  the spider/elephant `_clipgen` scripts used). This dumps each clip's per-bone rotation/position
  keyframes + the skeleton's bone order/rest pose to JSON, which `tools/blender/rebuild_anim_from_json.py`
  rebuilds onto the human armature for ARP retargeting onto the troll. Pure data — does not crash.
  Loads the (large) tpacs ONCE and dumps every requested clip. Runs under pwsh or WinPS.

  Data model (verified 2026-06-14):
   * skeleton.Definition.Data.Bones = List<BoneNode>{ Name, Parent, RestFrame (Matrix4x4) }  (28 for human)
   * clip.Definition.Data.BoneAnims[i] maps 1:1 to Bones[i]; .RotationFrames / .PositionFrames are
     SortedList<float Time, AnimationFrame<T>>{ Time, Value(Quaternion|Vector4) }
   * .RootPositionFrames / .RootScaleFrames carry root motion.

.PARAMETER Clips     One or more SkeletalAnimation names (e.g. anim_run_forward_unarmed). tpacs load once.
.PARAMETER OutDir    JSON output folder.
.PARAMETER Skeleton  Skeleton asset name (default human_skeleton).
#>
param(
  [Parameter(Mandatory=$true)][string[]]$Clips,
  [string]$OutDir      = "E:\LOTRAOMAssets\_troll_extract\json",
  [string]$Skeleton    = "human_skeleton",
  # Package holding the Skeleton asset. Defaults to human, which is where the troll/spider work
  # started; the horse rig lives in EmAssetPackages\pack_horse_customrig for the war ram's charge
  # clips, so the path had to stop being hardcoded. Relative to $NativeDir.
  [string]$SkeletonPackage = "EmAssetPackages\human\human.tpac",
  [string]$TpacToolBin = "E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin",
  [string]$NativeDir   = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native"
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$script:BINDIR = $TpacToolBin
[System.AppDomain]::CurrentDomain.add_AssemblyResolve([System.ResolveEventHandler]{
  param($s,$e); $n=($e.Name -split ',')[0].Trim(); $p=Join-Path $script:BINDIR "$n.dll"
  if (Test-Path $p) { return [Reflection.Assembly]::LoadFrom($p) }; return $null })
$asms=@(); foreach($d in @("TpacTool.Lib.dll","TpacTool.IO.dll")){ $asms+=[Reflection.Assembly]::LoadFrom((Join-Path $TpacToolBin $d)) }
function Find-T($n){ foreach($a in $asms){ try{$t=$a.GetTypes()|Where-Object{$_.FullName -eq $n -or $_.Name -eq $n}}catch{}; if($t){return $t[0]} } }
$APt=Find-T "TpacTool.Lib.AssetPackage"; $AMt=Find-T "TpacTool.Lib.AssetManager"

function Frames($sl, [bool]$isQuat){
  $out=@()
  if($sl -and $sl.Count -gt 0){
    foreach($k in $sl.Keys){
      $f=$sl[$k]; $v=$f.Value
      if($null -eq $v){ continue }
      if($isQuat){ $out += [ordered]@{ t=[double]$f.Time; x=[double]$v.X; y=[double]$v.Y; z=[double]$v.Z; w=[double]$v.W } }
      else{        $out += [ordered]@{ t=[double]$f.Time; x=[double]$v.X; y=[double]$v.Y; z=[double]$v.Z } }
    }
  }
  return ,$out
}

# --- load skeleton (bone order + rest) ONCE ---
$skPkg=[Activator]::CreateInstance($APt,[object[]]@([string](Join-Path $NativeDir $SkeletonPackage),$true,$true))
$skel=$skPkg.Items|Where-Object{$_.GetType().Name -eq "Skeleton" -and $_.Name -eq $Skeleton}|Select-Object -First 1
if(-not $skel){ throw "skeleton '$Skeleton' not found" }
$sd=$skel.Definition.Data
$bones=@()
foreach($b in $sd.Bones){
  $m=$b.RestFrame
  $bones += [ordered]@{ name=$b.Name; parent=($(if($b.Parent){$b.Parent.Name}else{$null}));
    rest=@($m.M11,$m.M12,$m.M13,$m.M14,$m.M21,$m.M22,$m.M23,$m.M24,$m.M31,$m.M32,$m.M33,$m.M34,$m.M41,$m.M42,$m.M43,$m.M44) }
}

# --- load animations.tpac ONCE (header + lazy resolver) ---
$anPkg=[Activator]::CreateInstance($APt,[object[]]@([string](Join-Path $NativeDir "AssetPackages\animations.tpac"),$true,$false))
$am=[Activator]::CreateInstance($AMt); $am.AddPackage($anPkg); $am.SetAsDefaultGlobalResolver()

$ok=0; $miss=0
foreach($ClipName in $Clips){
  $clipObj=@($anPkg.Items|Where-Object{$_.GetType().Name -eq "SkeletalAnimation" -and $_.Name -eq $ClipName})[0]
  if(-not $clipObj){ Write-Host ("  MISS {0}" -f $ClipName); $miss++; continue }
  $ad=$clipObj.Definition.Data
  $boneAnims=@()
  for($i=0; $i -lt $ad.BoneAnims.Count; $i++){
    $ba=$ad.BoneAnims[$i]
    $boneAnims += [ordered]@{ bone=$bones[$i].name; i=$i; rot=(Frames $ba.RotationFrames $true); pos=(Frames $ba.PositionFrames $false) }
  }
  $root=[ordered]@{ pos=(Frames $ad.RootPositionFrames $false); scale=(Frames $ad.RootScaleFrames $false) }
  $obj=[ordered]@{ clip=$ClipName; skeleton=$Skeleton; duration=$clipObj.Duration; boneNum=$clipObj.BoneNum;
    bones=$bones; boneAnims=$boneAnims; root=$root }
  $outfile=Join-Path $OutDir ($ClipName + ".json")
  ($obj | ConvertTo-Json -Depth 12 -Compress) | Set-Content -Path $outfile -Encoding UTF8
  $rotCount=($boneAnims | ForEach-Object{ $_.rot.Count } | Measure-Object -Sum).Sum
  Write-Host ("  OK   {0,-34} dur={1,-3} {2} bones, {3} rot kf, {4:n0} KB" -f $ClipName, $clipObj.Duration, $boneAnims.Count, $rotCount, ((Get-Item $outfile).Length/1KB))
  $ok++
}
Write-Host ("DONE: {0} written, {1} missing -> {2}" -f $ok, $miss, $OutDir)
