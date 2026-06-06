<#
.SYNOPSIS
  Decompile BOTH Bannerlord builds (shipping client + editor), ALL DLLs, into one folder
  separated by build — and inventory the native (non-.NET) DLLs that can't be decompiled.

.DESCRIPTION
  Bannerlord ships two native+managed builds:
    - Win64_Shipping_Client  : the game players run. Editor code is stripped.
    - Win64_Shipping_wEditor : the Modding Kit build. SAME-named TaleWorlds DLLs but with
      editor-only types compiled in (EditorGame, MBEditor, AnimalSpawnSettings, VertexAnimator,
      the animation/clip authoring helpers, etc.) that DO NOT exist in the shipping decompile.

  Some engine/editor knowledge (how the Animation Clip Inspector applies AnimFlags, the FBX import
  + tpac clip serialization) is ONLY in the editor build. So both are decompiled here, side by side.

  Decompiles EVERY .dll in each build. ilspycmd only handles .NET assemblies; NATIVE DLLs
  (Qt5*, FreeImage, libfbxsdk, nvtt, embree3, tbb, ispc_texcomp, MinHook, TaleWorlds.Native, ...)
  cannot be decompiled — they are recorded in _native_dlls.txt for documentation instead.
  See docs/reference/bannerlord-engine-and-toolchain.md for what each native component is.

.NOTES
  Requires ilspycmd (dotnet global tool). Re-run after an engine update. Idempotent: overwrites.
  Output per build:  <Out>\<build>\<Dll>.cs   (.NET)   +   <Out>\<build>\_native_dlls.txt  (native)
  builds: _shipping_build, _editor_build.
  The pre-existing curated category folders under <Out> (Campaign\, MountAndBlade\, Core\, ...) are
  the SHIPPING client browse reference; these per-build folders are the full per-DLL decompile.
#>
param(
  [string]$Out     = "E:\Decompiled_Bannerlord",
  [string]$GameBin = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin"
)

if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
  Write-Error "ilspycmd not found on PATH. Install: dotnet tool install -g ilspycmd"; exit 1
}

$builds = [ordered]@{
  '_shipping_build' = Join-Path $GameBin 'Win64_Shipping_Client'
  '_editor_build'   = Join-Path $GameBin 'Win64_Shipping_wEditor'
}

foreach ($build in $builds.Keys) {
  $src = $builds[$build]
  if (-not (Test-Path $src)) { Write-Warning "missing build dir: $src — skipping"; continue }
  $dst = Join-Path $Out $build
  New-Item -ItemType Directory -Force $dst | Out-Null
  $nativeList = Join-Path $dst '_native_dlls.txt'
  Set-Content -LiteralPath $nativeList -Value "# Native (non-.NET) DLLs in $build — cannot be decompiled by ilspycmd. See docs/reference/bannerlord-engine-and-toolchain.md" -Encoding UTF8

  $dlls = Get-ChildItem (Join-Path $src '*.dll')
  Write-Host "=== $build : $($dlls.Count) DLLs -> $dst ==="
  $i = 0; $managed = 0; $native = 0
  foreach ($dll in $dlls) {
    $i++
    $outFile = Join-Path $dst ($dll.BaseName + '.cs')
    & ilspycmd $dll.FullName 2>$null | Set-Content -LiteralPath $outFile -Encoding UTF8
    $len = (Get-Item -LiteralPath $outFile).Length
    if ($len -lt 200) {
      # ilspycmd produced ~nothing => native / non-.NET
      Remove-Item -LiteralPath $outFile -ErrorAction SilentlyContinue
      Add-Content -LiteralPath $nativeList -Value $dll.Name
      $native++
      Write-Host ("  [{0}/{1}] (native) {2}" -f $i, $dlls.Count, $dll.Name)
    } else {
      $managed++
      Write-Host ("  [{0}/{1}] {2}.cs ({3:N0} B)" -f $i, $dlls.Count, $dll.BaseName, $len)
    }
  }
  Write-Host ("  ${build}: $managed managed decompiled, $native native listed")
}
Write-Host "DONE. shipping vs editor decompiles under $Out\{_shipping_build,_editor_build}\ (+ _native_dlls.txt each)"
