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
  builds: _shipping_build, _editor_build, _modules_build.
  _modules_build covers Modules\*\bin\Win64_Shipping_Client (SandBox.View, SandBox.ViewModelCollection,
  TaleWorlds.MountAndBlade.View, the StoryMode/Multiplayer/NavalDLC satellites — 34 assemblies the
  two bin builds do NOT contain). Its files are named <Module>__<Dll>.cs because names collide
  across modules. Added 2026-08-10: their absence made the v1.4.8 assembly diff silently partial.
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

# --- Module-bin DLLs -------------------------------------------------------------------
# The two builds above cover ONLY <GameBin>\Win64_Shipping_*. They MISS every DLL that ships
# inside a module's own bin folder — SandBox.View, SandBox.ViewModelCollection, SandBox.GauntletUI,
# TaleWorlds.MountAndBlade.View, TaleWorlds.MountAndBlade.GauntletUI, the StoryMode/Multiplayer/
# NavalDLC satellites. That is 34 assemblies, and TAOM patches heavily into several of them
# (AgentVisuals, MobilePartyVisual, SPInventoryVM, the tournament controllers).
#
# Found 2026-08-10 during the v1.4.8 bump: the 1.4.7 -> 1.4.8 assembly diff silently covered only
# the base bin, because these were never in the stack to diff against. Since Steam overwrites the
# install in place, a DLL absent from this stack has NO recoverable baseline once the update lands.
# Decompiling them here is what makes the NEXT bump diffable.
#
# Files are named <Module>__<Dll>.cs: names collide across modules (TaleWorlds.MountAndBlade.
# Multiplayer.dll ships in both CustomBattle\ and Multiplayer\), so a flat <Dll>.cs would drop one.
$modRoot = Join-Path (Split-Path -Parent $GameBin) 'Modules'
if (Test-Path $modRoot) {
  $dst = Join-Path $Out '_modules_build'
  New-Item -ItemType Directory -Force $dst | Out-Null
  $nativeList = Join-Path $dst '_native_dlls.txt'
  Set-Content -LiteralPath $nativeList -Value "# Native (non-.NET) DLLs in module bin folders — cannot be decompiled by ilspycmd." -Encoding UTF8
  $managed = 0; $native = 0
  Write-Host "=== _modules_build : Modules\*\bin\Win64_Shipping_Client -> $dst ==="
  foreach ($mod in (Get-ChildItem $modRoot -Directory | Sort-Object Name)) {
    $bin = Join-Path $mod.FullName 'bin\Win64_Shipping_Client'
    if (-not (Test-Path $bin)) { continue }
    foreach ($dll in (Get-ChildItem (Join-Path $bin '*.dll') -ErrorAction SilentlyContinue)) {
      $outFile = Join-Path $dst ("{0}__{1}.cs" -f $mod.Name, $dll.BaseName)
      & ilspycmd $dll.FullName 2>$null | Set-Content -LiteralPath $outFile -Encoding UTF8
      if ((Get-Item -LiteralPath $outFile).Length -lt 200) {
        Remove-Item -LiteralPath $outFile -ErrorAction SilentlyContinue
        Add-Content -LiteralPath $nativeList -Value ("{0}/{1}" -f $mod.Name, $dll.Name)
        $native++
      } else {
        $managed++
        Write-Host ("  {0}__{1}.cs" -f $mod.Name, $dll.BaseName)
      }
    }
  }
  Write-Host "  _modules_build: $managed managed decompiled, $native native listed"
} else {
  Write-Warning "missing Modules dir: $modRoot — skipping module-bin decompile"
}

Write-Host "DONE. decompiles under $Out\{_shipping_build,_editor_build,_modules_build}\ (+ _native_dlls.txt each)"
