#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates a committed snapshot of the TaleWorlds engine signatures that TAOM's GameModel
    overrides and Harmony patches depend on, so research does not require the external decompile
    dump (E:\Decompiled_Bannerlord\).

.DESCRIPTION
    Reflects over the *installed* Bannerlord assemblies + TAOM.dll and writes two markdown
    files under docs/reference/taleworlds-api-snapshot/:
      - gamemodel-bases.md : each Taom*Model, its Default*Model base, and the exact base signature
                             of every method it overrides.
      - patch-targets.md   : each [HarmonyPatch] / TargetMethod-bearing class and the signature of
                             the engine method it patches (resolved the way Harmony resolves it).

    The type list is auto-derived from TAOM.dll, so the snapshot tracks the code without a curated
    list to maintain. Re-run after a Bannerlord update (or any patch/GameModel change) to refresh.

.PARAMETER Check
    Regenerate to a temp dir and diff against the committed files. Exits 1 if they differ (no write).
    Used by the verification step to prove the committed snapshot is reproducible.

.NOTES
    Game dir resolution mirrors Directory.Build.props: BANNERLORD_OVERRIDE_DIR (if its bin holds
    Bannerlord.exe) else BANNERLORD_GAME_DIR.
#>
[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'docs\reference\taleworlds-api-snapshot'

function Resolve-GameDir {
    $over = $env:BANNERLORD_OVERRIDE_DIR
    if ($over -and (Test-Path (Join-Path $over 'bin\Win64_Shipping_Client\Bannerlord.exe'))) { return $over }
    $game = $env:BANNERLORD_GAME_DIR
    if ($game -and (Test-Path $game)) { return $game }
    throw "Cannot resolve game dir: set BANNERLORD_GAME_DIR (or BANNERLORD_OVERRIDE_DIR)."
}

function Get-BinFolder($gameDir) {
    if (Test-Path (Join-Path $gameDir 'bin\Win64_Shipping_Client\Bannerlord.exe')) { return 'Win64_Shipping_Client' }
    if (Test-Path (Join-Path $gameDir 'bin\Gaming.Desktop.x64_Shipping_Client\Bannerlord.exe')) { return 'Gaming.Desktop.x64_Shipping_Client' }
    throw "No bin folder with Bannerlord.exe under '$gameDir'."
}

$gameDir = Resolve-GameDir
$binFolder = Get-BinFolder $gameDir

# Version stamp: read the installed engine version so the snapshot header self-labels and
# does not go stale after an engine bump (mirrors tools/decompile_to_folder.ps1).
$versionXmlPath = Join-Path $gameDir "bin\$binFolder\Version.xml"
$gameVersion = 'unknown'
if (Test-Path $versionXmlPath) {
    try { $gameVersion = ([xml](Get-Content $versionXmlPath -Raw)).Version.Singleplayer.Value } catch {}
}

$testBin = Join-Path $repoRoot "TAOM.Tests\bin\Debug\net472"
if (-not (Test-Path (Join-Path $testBin 'TAOM.dll'))) {
    $testBin = Join-Path $repoRoot "TAOM.Tests\bin\Release\net472"
}
if (-not (Test-Path (Join-Path $testBin 'TAOM.dll'))) {
    throw "TAOM.dll not found in test bin — build TAOM.Tests first (dotnet build TAOM.Tests/TAOM.Tests.csproj)."
}

$moduleDirs = @('SandBox','SandBoxCore','CustomBattle','StoryMode','Native') | ForEach-Object {
    Join-Path $gameDir "Modules\$_\bin\$binFolder"
}
$probeDirs = @($testBin) + $moduleDirs | Where-Object { Test-Path $_ }

# Resolve dependencies from the test bin + module folders (mirrors GameAssemblies.cs).
$resolver = [ResolveEventHandler]{
    param($s, $e)
    $simple = ([System.Reflection.AssemblyName]::new($e.Name)).Name + '.dll'
    foreach ($d in $script:probeDirs) {
        $p = Join-Path $d $simple
        if (Test-Path $p) { try { return [Reflection.Assembly]::LoadFrom($p) } catch {} }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

# Eager-load engine + module assemblies so type/attribute resolution sees the full surface.
foreach ($d in $probeDirs) {
    Get-ChildItem (Join-Path $d '*.dll') -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -ieq 'TaleWorlds.Native.dll') { return }
        try { [Reflection.Assembly]::LoadFrom($_.FullName) | Out-Null } catch {}
    }
}

$taom = [Reflection.Assembly]::LoadFrom((Join-Path $testBin 'TAOM.dll'))
$harmonyPatchType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { try { $_.GetType('HarmonyLib.HarmonyPatch', $false) } catch { $null } } |
    Where-Object { $_ } | Select-Object -First 1

function Get-LoadableTypes($asm) {
    try { return $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { return $_.Exception.Types | Where-Object { $_ } }
}

$bt = [char]96   # backtick — markdown code-span delimiter; avoids PS escape ambiguity
function Code($s) { return "$bt$s$bt" }

function Format-Method($m) {
    if (-not $m) { return '(unresolved)' }
    $ps = ($m.GetParameters() | ForEach-Object {
        $pt = $_.ParameterType.Name
        if ($_.ParameterType.IsByRef) { $pt = 'ref ' + $pt.TrimEnd('&') }
        "$pt $($_.Name)"
    }) -join ', '
    $ret = if ($m.PSObject.Properties['ReturnType']) { $m.ReturnType.Name } else { 'void' }
    return "$ret $($m.Name)($ps)"
}

$taomTypes = Get-LoadableTypes $taom

# --- gamemodel-bases.md -------------------------------------------------------
function Test-DerivesFromGameModel($t) {
    for ($b = $t.BaseType; $b; $b = $b.BaseType) { if ($b.FullName -eq 'TaleWorlds.Core.GameModel') { return $true } }
    return $false
}

$gmRows = foreach ($t in ($taomTypes | Where-Object { $_ -and -not $_.IsAbstract -and (Test-DerivesFromGameModel $_) } | Sort-Object Name)) {
    $overrides = $t.GetMethods('Public,NonPublic,Instance,DeclaredOnly') |
        Where-Object { $_.IsVirtual -and -not $_.IsAbstract -and $_.GetBaseDefinition().DeclaringType -ne $_.DeclaringType } |
        Sort-Object Name
    [pscustomobject]@{
        Model     = $t.Name
        Base      = $t.BaseType.Name
        BaseFull  = $t.BaseType.FullName
        Overrides = $overrides | ForEach-Object { Format-Method $_.GetBaseDefinition() }
    }
}

$gm = [System.Text.StringBuilder]::new()
[void]$gm.AppendLine("# GameModel Base Signatures ($gameVersion snapshot)")
[void]$gm.AppendLine('')
[void]$gm.AppendLine('Auto-generated by tools/snapshot_api_surface.ps1. For each Taom*Model, the base Default*Model')
[void]$gm.AppendLine("and the exact $gameVersion signature of every method it overrides. The C# compiler already enforces")
[void]$gm.AppendLine('these (CS0115) — this committed copy means signature lookups need no external decompile dump.')
[void]$gm.AppendLine('')
[void]$gm.AppendLine("Models: $($gmRows.Count). Regenerate after any engine bump: $(Code 'pwsh tools/snapshot_api_surface.ps1').")
[void]$gm.AppendLine('')
foreach ($r in $gmRows) {
    [void]$gm.AppendLine("## $($r.Model) : $($r.Base)")
    [void]$gm.AppendLine((Code "Base: $($r.BaseFull)"))
    [void]$gm.AppendLine('')
    if ($r.Overrides) { foreach ($o in $r.Overrides) { [void]$gm.AppendLine("- $(Code $o)") } }
    else { [void]$gm.AppendLine('- _(no method overrides — property-only or marker)_') }
    [void]$gm.AppendLine('')
}

# --- patch-targets.md ---------------------------------------------------------
function Resolve-PatchTarget($t) {
    $flags = 'Static,Public,NonPublic'
    $tms = $t.GetMethod('TargetMethods', $flags, $null, [Type]::EmptyTypes, $null)
    if ($tms) {
        $r = $tms.Invoke($null, @())
        if (-not $r) { return @{ Desc = '(TargetMethods returned null)'; Target = $null } }
        return @{ Desc = "TargetMethods() x$(@($r).Count)"; Target = (@($r)[0]) }
    }
    $tm = $t.GetMethod('TargetMethod', $flags, $null, [Type]::EmptyTypes, $null)
    if ($tm) {
        $r = $tm.Invoke($null, @())
        return @{ Desc = 'TargetMethod()'; Target = $r }
    }
    if (-not $script:harmonyPatchType) { return @{ Desc = '(no HarmonyLib)'; Target = $null } }

    # Collect [HarmonyPatch] infos from the class AND each patch method — a class may put its target
    # method name on the Prefix/Postfix attributes (e.g. one Prefix on OnInitialize, one on OnFinalize).
    $infos = New-Object System.Collections.Generic.List[object]
    foreach ($a in $t.GetCustomAttributes($script:harmonyPatchType, $false)) { $infos.Add($a.info) }
    foreach ($m in $t.GetMethods('Static,Instance,Public,NonPublic,DeclaredOnly')) {
        foreach ($a in $m.GetCustomAttributes($script:harmonyPatchType, $false)) { $infos.Add($a.info) }
    }
    if ($infos.Count -eq 0) { return @{ Desc = '(no [HarmonyPatch])'; Target = $null } }

    $classDt = ($infos | Where-Object { $_.declaringType } | Select-Object -First 1).declaringType

    function Resolve-One($dt, $mn, $mt2, $argTypes) {
        if (-not $dt) { return $null }
        try {
            switch ("$mt2") {
                'Getter'      { return [HarmonyLib.AccessTools]::PropertyGetter($dt, $mn) }
                'Setter'      { return [HarmonyLib.AccessTools]::PropertySetter($dt, $mn) }
                'Constructor' { return [HarmonyLib.AccessTools]::Constructor($dt, $argTypes) }
                default       { if (-not $mn) { return $null }; return [HarmonyLib.AccessTools]::Method($dt, $mn, $argTypes) }
            }
        } catch { return $null }
    }

    # Distinct named targets (class-level methodName, or per-method-attribute methodNames).
    $named = $infos | Where-Object { $_.methodName } |
        ForEach-Object { [pscustomobject]@{ dt = ($_.declaringType ?? $classDt); mn = $_.methodName; mt = $_.methodType; argTypes = $_.argumentTypes } } |
        Sort-Object -Property mn -Unique
    if (-not $named -and ($infos | Where-Object { "$($_.methodType)" -in @('Constructor','StaticConstructor') })) {
        $ctor = $infos | Where-Object { "$($_.methodType)" -in @('Constructor','StaticConstructor') } | Select-Object -First 1
        $named = @([pscustomobject]@{ dt = ($ctor.declaringType ?? $classDt); mn = $null; mt = $ctor.methodType; argTypes = $ctor.argumentTypes })
    }
    if (-not $named) { return @{ Desc = '(indeterminate)'; Target = $null } }

    $targets = $named | ForEach-Object { Resolve-One $_.dt $_.mn $_.mt $_.argTypes } | Where-Object { $_ }
    if (-not $targets) { return @{ Desc = "$($classDt.FullName) (unresolved)"; Target = $null } }
    if (@($targets).Count -eq 1) { return @{ Desc = ''; Target = @($targets)[0] } }
    $desc = (@($targets) | ForEach-Object { "$($_.DeclaringType.FullName).$(Format-Method $_)" }) -join '  ;  '
    return @{ Desc = $desc; Target = $null }
}

$patchTypes = $taomTypes | Where-Object {
    $_ -and ($_.GetCustomAttributes($harmonyPatchType, $false).Count -gt 0 -or
             $_.GetMethod('TargetMethod', 'Static,Public,NonPublic', $null, [Type]::EmptyTypes, $null) -or
             $_.GetMethod('TargetMethods', 'Static,Public,NonPublic', $null, [Type]::EmptyTypes, $null))
} | Sort-Object FullName

$ptRows = foreach ($t in $patchTypes) {
    $res = Resolve-PatchTarget $t
    [pscustomobject]@{
        Patch  = $t.FullName
        Target = if ($res.Target) { "$($res.Target.DeclaringType.FullName).$(Format-Method $res.Target)" } else { $res.Desc }
    }
}

$pt = [System.Text.StringBuilder]::new()
[void]$pt.AppendLine("# Harmony Patch Target Signatures ($gameVersion snapshot)")
[void]$pt.AppendLine('')
[void]$pt.AppendLine('Auto-generated by tools/snapshot_api_surface.ps1. Each [HarmonyPatch] / TargetMethod-bearing')
[void]$pt.AppendLine("class in TAOM.dll and the $gameVersion engine method it patches, resolved the way Harmony resolves it.")
[void]$pt.AppendLine('Live-verified every build by TAOM.Tests/Migration/HarmonyPatchBindingTests.')
[void]$pt.AppendLine('')
[void]$pt.AppendLine("Patches: $($ptRows.Count). Regenerate after any engine bump: $(Code 'pwsh tools/snapshot_api_surface.ps1').")
[void]$pt.AppendLine('')
[void]$pt.AppendLine('| Patch class | Engine target |')
[void]$pt.AppendLine('|---|---|')
foreach ($r in $ptRows) { [void]$pt.AppendLine("| $(Code $r.Patch) | $(Code $r.Target) |") }
[void]$pt.AppendLine('')

# --- write or check -----------------------------------------------------------
$files = @{
    (Join-Path $outDir 'gamemodel-bases.md') = $gm.ToString()
    (Join-Path $outDir 'patch-targets.md')   = $pt.ToString()
}

if ($Check) {
    $diff = $false
    foreach ($path in $files.Keys) {
        $new = $files[$path] -replace "`r`n", "`n"
        $old = if (Test-Path $path) { (Get-Content $path -Raw) -replace "`r`n", "`n" } else { '' }
        if ($new -ne $old) { Write-Host "DRIFT: $path differs from generated output." -ForegroundColor Red; $diff = $true }
        else { Write-Host "OK: $([IO.Path]::GetFileName($path)) reproduces." -ForegroundColor Green }
    }
    if ($diff) { exit 1 }
    exit 0
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
foreach ($path in $files.Keys) {
    Set-Content -Path $path -Value $files[$path] -NoNewline -Encoding UTF8
    Write-Host "Wrote $path"
}
Write-Host "GameModels: $($gmRows.Count) | Patches: $($ptRows.Count)"
