<#
.SYNOPSIS
  Extract Bannerlord human animation clips from animations.tpac to FBX (for ARP retargeting
  onto a humanoid RACE rig, e.g. the troll). PROVEN 2026-06-14.

.DESCRIPTION
  TpacTool's FbxExporter is the only autonomous tpac->FBX path (TpacTool has NO FBX importer,
  so tpac->FBX is one-way). Each exported clip is a skinned body mesh + the human_skeleton +
  the baked SkeletalAnimation, which Blender imports as an animated armature ready for
  tools/blender/arp_retarget.py -> retarget onto the troll ARP rig -> ARP GE export -> Kit.

  Hard-won recipe encoded here (RCA 2026-06-14):
   * human_skeleton lives in Native\EmAssetPackages\human\human.tpac (NOT skeletons.tpac, which
     only has animal/prop skeletons + human_LOW). The animation clips live in
     Native\AssetPackages\animations.tpac and reference skeleton GUID dd7f3586-...
   * All Native tpacs share ONE package GUID, so AssetManager can hold only one at a time:
     load human.tpac eagerly for the skeleton+mesh, add animations.tpac as the lazy resolver.
   * A MESH is required in the FBX or Blender imports the bones as static EMPTIES with no
     animation. body_male_a is used; its Material refs are CLEARED (replaced with an in-memory
     dummy Material) because the body's real materials live in another tpac and would throw
     ResolveFailedException -- we don't need materials for retargeting.
   * The native assimp.dll must be force-loaded (PATH + AssimpLibrary.LoadLibrary).
   * Clips with Duration==0 are static single-pose entries (e.g. anim_stand_idle) and are
     skipped by default (-IncludeStatic to override).

.PARAMETER OutDir       Output folder for the FBX files.
.PARAMETER Clips        Explicit clip names to extract (overrides the built-in core set).
.PARAMETER TpacToolBin  TpacTool bin folder (DLLs + win-x64\native\assimp.dll).
.PARAMETER NativeDir    Bannerlord Native module folder.
.PARAMETER IncludeStatic Also export Duration==0 (single-pose) clips.

.EXAMPLE
  pwsh tools/extract_human_anims_tpac.ps1                       # core set -> default OutDir
  pwsh tools/extract_human_anims_tpac.ps1 -Clips anim_attack_up_1h_release
#>
param(
  [string]$OutDir      = "E:\LOTRAOMAssets\_troll_extract\core",
  [string[]]$Clips     = @(),
  [string]$TpacToolBin = "E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin",
  [string]$NativeDir   = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native",
  [switch]$IncludeStatic
)
$ErrorActionPreference = "Stop"

# Built-in CORE set: unarmed/1h/2h locomotion + idle + defend + turn + death + jump.
# These were confirmed present in v1.4.5 animations.tpac (human skeleton dd7f3586).
$CORE = @(
  'anim_walk_forward_unarmed','anim_walk_backwards_unarmed','anim_walk_left_unarmed','anim_walk_right_unarmed',
  'anim_run_forward_unarmed','anim_run_backwards_unarmed','anim_run_left_unarmed','anim_run_right_unarmed',
  'anim_walk_forward_onehanded','anim_walk_left_onehanded','anim_walk_right_onehanded',
  'anim_run_forward_onehanded','anim_run_left_onehanded','anim_run_right_onehanded',
  'anim_walk_forward_twohanded','anim_walk_left_twohanded','anim_walk_right_twohanded',
  'anim_run_forward_twohanded','anim_run_left_twohanded','anim_run_right_twohanded',
  'anim_stand_unarmed','anim_idles','anim_idle_left_th_axe','anim_idle_right_th_axe',
  'anim_defend_up_1h_active','anim_defend_left_1h_active','anim_defend_right_1h_active',
  'anim_turn_left_unarmed','anim_turn_right_unarmed','anim_turn_left_2h','anim_turn_right_2h',
  'anim_death_fall_front','anim_death_fall_back_heavy','anim_death_fall_left','anim_death_fall_right',
  'anim_jumps_forward'
)
if ($Clips.Count -eq 0) { $Clips = $CORE }

$humanTpac = Join-Path $NativeDir "EmAssetPackages\human\human.tpac"
$animTpac  = Join-Path $NativeDir "AssetPackages\animations.tpac"
$nativeDll = Join-Path $TpacToolBin "win-x64\native\assimp.dll"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$env:PATH = (Join-Path $TpacToolBin "win-x64\native") + ";" + $env:PATH

# IMPORTANT: run this under Windows PowerShell 5.1 (.NET Framework), NOT pwsh 7. Under pwsh 7
# (.NET 10) AssimpNet's native interop crashes with a 0xC0000005 access violation in
# ExportSceneToBlob. .NET Framework is TpacTool's target runtime and is stable. (RCA 2026-06-14.)
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\extract_human_anims_tpac.ps1
# Resolve TpacTool's transitive deps (System.Numerics.Vectors, LZ4, etc.) from its bin folder.
$script:BINDIR = $TpacToolBin
[System.AppDomain]::CurrentDomain.add_AssemblyResolve([System.ResolveEventHandler]{
  param($s,$e)
  $n = ($e.Name -split ',')[0].Trim()
  $p = Join-Path $script:BINDIR "$n.dll"
  if (Test-Path $p) { return [Reflection.Assembly]::LoadFrom($p) }
  return $null
})

$asms=@(); foreach($d in @("TpacTool.Lib.dll","TpacTool.IO.dll","AssimpNet.dll","TpacTool.IO.Assimp.dll")){
  $asms += [Reflection.Assembly]::LoadFrom((Join-Path $TpacToolBin $d))
}
function Find-T($n){ foreach($a in $asms){ try{$t=$a.GetTypes()|?{$_.FullName -eq $n -or $_.Name -eq $n}}catch{}; if($t){return $t[0]} } }

# force-load native assimp
$alT=Find-T "Assimp.Unmanaged.AssimpLibrary"
$inst=$alT.GetProperty("Instance",[Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static).GetValue($null)
($alT.GetMethods()|?{$_.Name -eq "LoadLibrary" -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq "String"}|Select -First 1).Invoke($inst,@([string]$nativeDll))|Out-Null

$APt=Find-T "TpacTool.Lib.AssetPackage"; $AMt=Find-T "TpacTool.Lib.AssetManager"
$adT=Find-T "AssetDependence``1"; $matT=Find-T "TpacTool.Lib.Material"
$adClosed=$adT.MakeGenericType($matT)
$dep=[Activator]::CreateInstance($adClosed,[object[]]@([Activator]::CreateInstance($matT)))
$emptyDep=[Activator]::CreateInstance($adClosed,[object[]]@([Guid]::Empty))

Write-Host "loading human.tpac (skeleton + body mesh) ..."
$skPkg=[Activator]::CreateInstance($APt,[object[]]@([string]$humanTpac,$true,$true))
$skel=$skPkg.Items|?{$_.GetType().Name -eq "Skeleton" -and $_.Name -eq "human_skeleton"}|Select -First 1
$mm=$skPkg.Items|?{$_.GetType().Name -eq "Metamesh" -and $_.Name -eq "body_male_a"}|Select -First 1
foreach($me in $mm.Meshes){ try{$me.Material=$dep}catch{}; try{$me.SecondMaterial=$emptyDep}catch{} }
if(-not $skel){ throw "human_skeleton not found in $humanTpac" }

Write-Host "loading animations.tpac (resolver) ..."
$anPkg=[Activator]::CreateInstance($APt,[object[]]@([string]$animTpac,$true,$false))
$am=[Activator]::CreateInstance($AMt); $am.AddPackage($anPkg); $am.SetAsDefaultGlobalResolver()
$anims=@($anPkg.Items|?{$_.GetType().Name -eq "SkeletalAnimation"})

$ok=0;$skip=0;$miss=0
foreach($name in $Clips){
  $clip = $anims | ?{ $_.Name -eq $name } | Select -First 1
  if(-not $clip){ Write-Host ("  MISS  {0}" -f $name); $miss++; continue }
  if($clip.Duration -le 0 -and -not $IncludeStatic){ Write-Host ("  SKIP  {0} (Duration=0, static)" -f $name); $skip++; continue }
  $fbx=[Activator]::CreateInstance((Find-T "FbxExporter"))
  $fbx.Skeleton=$skel; $fbx.Model=$mm; $fbx.Animation=$clip
  $fbx.FixBoneForBlender=$true; $fbx.IsDiffuseOnly=$true; $fbx.AnimationFrameRate=[single]30
  $out=Join-Path $OutDir ($name + ".fbx")
  try { $fbx.Export($out); Write-Host ("  OK    {0}  (dur={1}, {2:n0} KB)" -f $name,$clip.Duration,((Get-Item $out).Length/1KB)); $ok++ }
  catch { Write-Host ("  FAIL  {0} : {1}" -f $name, $_.Exception.Message); }
}
Write-Host ("`nDONE: {0} exported, {1} skipped(static), {2} missing -> {3}" -f $ok,$skip,$miss,$OutDir)
