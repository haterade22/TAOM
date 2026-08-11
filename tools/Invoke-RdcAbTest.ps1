<#
.SYNOPSIS
    Toggles RuntimeDataCache off/on across the TAOM module set for the Phase 1 A/B, and
    reports what the run produced.

.DESCRIPTION
    Settles the open question in docs/investigations/native-commit-audit-2026-08.md Step 1.2:
    the shipping client demonstrably READS RuntimeDataCache (13,795 ReadFile ops across 5,036
    distinct .rdc files, Procmon 2026-08-08) but can never write it -- the entire RDC write
    string surface lives only in Win64_Shipping_wEditor\TaleWorlds.Native.dll, which states
    outright: "External .rdc file modification detected. RDC files cannot be updated outside
    the editor." Vanilla's Native module ships 1,188 tpacs and zero .rdc and runs fine, so the
    no-RDC path exists. What is unmeasured is the COST of taking it.

    This script never deletes. It renames RuntimeDataCache <-> RuntimeDataCache.OFF, rolls back
    on partial failure, and refuses to touch anything while Bannerlord is running (the engine
    detects external .rdc modification and demands a restart).

.PARAMETER Off
    Rename RuntimeDataCache -> RuntimeDataCache.OFF in every target module. Run the game after.

.PARAMETER On
    Restore every RuntimeDataCache.OFF found. ALWAYS run this when the experiment is done.

.PARAMETER Status
    Report current state per module plus any leftover .OFF folders. Read-only.

.PARAMETER Report
    Scan the newest rgl_log_errors_*.txt and taom_debug_*.log for the marker strings the
    decision gate keys on. Read-only; run it after the OFF launch and again after the ON launch.

.PARAMETER Modules
    Module names to toggle. Defaults to the TAOM release set that ships RDC.

.EXAMPLE
    pwsh tools/Invoke-RdcAbTest.ps1 -Status
    pwsh tools/Invoke-RdcAbTest.ps1 -Off      # game closed; then launch and run the stations
    pwsh tools/Invoke-RdcAbTest.ps1 -Report
    pwsh tools/Invoke-RdcAbTest.ps1 -On       # ALWAYS restore
#>
[CmdletBinding(DefaultParameterSetName = 'Status')]
param(
    [Parameter(ParameterSetName = 'Off')]   [switch] $Off,
    [Parameter(ParameterSetName = 'On')]    [switch] $On,
    [Parameter(ParameterSetName = 'Status')][switch] $Status,
    [Parameter(ParameterSetName = 'Report')][switch] $Report,
    [string[]] $Modules = @('TAOM', 'TAOM_Map', 'LOTRLOME_Armory', 'Alliance.Wargs'),
    [string]   $GameDir,
    [int]      $SinceMinutes = 120
)

$ErrorActionPreference = 'Stop'
$CACHE = 'RuntimeDataCache'
$OFFNAME = 'RuntimeDataCache.OFF'

function Resolve-GameDir {
    param([string] $Explicit)
    if ($Explicit) { return $Explicit }
    $env = [System.Environment]::GetEnvironmentVariable('BANNERLORD_GAME_DIR', 'User')
    if (-not $env) {
        throw "BANNERLORD_GAME_DIR is not set and -GameDir was not supplied. Export it or pass -GameDir."
    }
    return $env
}

function Get-ModuleRoot {
    param([string] $GameDir)
    $root = Join-Path $GameDir 'Modules'
    if (-not (Test-Path $root)) { throw "Modules directory not found: $root" }
    return $root
}

function Assert-GameClosed {
    $p = Get-Process -Name 'Bannerlord*' -ErrorAction SilentlyContinue
    if ($p) {
        throw ("Bannerlord is running (PID $($p.Id -join ', ')). Close it first -- the engine " +
               "rejects external .rdc changes mid-session and demands a restart anyway.")
    }
}

function Get-DirSizeGB {
    param([string] $Path)
    $s = (Get-ChildItem $Path -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    if (-not $s) { return 0.0 }
    return [math]::Round($s / 1GB, 2)
}

function Show-Status {
    param([string] $ModuleRoot, [string[]] $Names)
    $rows = foreach ($m in $Names) {
        $p = Join-Path $ModuleRoot $m
        if (-not (Test-Path $p)) {
            [pscustomobject]@{ Module = $m; State = 'NOT INSTALLED'; Files = 0; GB = 0.0 }
            continue
        }
        $on = Join-Path $p $CACHE
        $off = Join-Path $p $OFFNAME
        $state = if ((Test-Path $on) -and (Test-Path $off)) { 'BOTH -- resolve by hand' }
                 elseif (Test-Path $on) { 'ON' }
                 elseif (Test-Path $off) { 'OFF' }
                 else { 'no RDC' }
        $target = if (Test-Path $on) { $on } elseif (Test-Path $off) { $off } else { $null }
        $count = if ($target) { @(Get-ChildItem $target -Recurse -File -Filter *.rdc -ErrorAction SilentlyContinue).Count } else { 0 }
        [pscustomobject]@{
            Module = $m; State = $state; Files = $count
            GB = if ($target) { Get-DirSizeGB $target } else { 0.0 }
        }
    }
    $rows | Format-Table -AutoSize

    $stray = @($rows | Where-Object State -eq 'OFF')
    if ($stray.Count -gt 0) {
        Write-Host ("WARNING: $($stray.Count) module(s) still have RDC renamed OFF. " +
                    "Run -On before you play or publish normally.") -ForegroundColor Yellow
    }
    $both = @($rows | Where-Object State -like 'BOTH*')
    if ($both.Count -gt 0) {
        Write-Host "ERROR: both folders present in $($both.Module -join ', '). Merge or delete by hand." -ForegroundColor Red
    }
}

function Invoke-Toggle {
    param([string] $ModuleRoot, [string[]] $Names, [string] $From, [string] $To)
    Assert-GameClosed
    $done = @()
    try {
        foreach ($m in $Names) {
            $src = Join-Path (Join-Path $ModuleRoot $m) $From
            if (-not (Test-Path $src)) { Write-Host "  skip $m (no $From)" -ForegroundColor DarkGray; continue }
            $dstPath = Join-Path (Join-Path $ModuleRoot $m) $To
            if (Test-Path $dstPath) { throw "$m already has $To -- refusing to clobber. Resolve by hand." }
            Rename-Item -LiteralPath $src -NewName $To
            $done += [pscustomobject]@{ Module = $m; Path = $dstPath }
            Write-Host "  $m : $From -> $To" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "FAILED mid-toggle: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Rolling back $($done.Count) rename(s)..." -ForegroundColor Yellow
        foreach ($d in $done) {
            try { Rename-Item -LiteralPath $d.Path -NewName $From } # best effort
            catch { Write-Host "  ROLLBACK FAILED for $($d.Module): $($_.Exception.Message)" -ForegroundColor Red }
        }
        throw
    }
    return $done.Count
}

function Get-LogSearchRoots {
    param([string] $GameDir)
    # taom_debug lands next to the executable, NOT in Documents -- verified 2026-08-10:
    # <gamedir>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log. rgl logs have been seen in
    # the Documents tree. Search both and let the caller sort it out.
    $roots = @(
        (Join-Path $GameDir 'bin\Win64_Shipping_Client\Logs'),
        (Join-Path $env:USERPROFILE 'OneDrive\Documents\Mount and Blade II Bannerlord\Logs'),
        (Join-Path $env:USERPROFILE 'Documents\Mount and Blade II Bannerlord\Logs')
    )
    return @($roots | Where-Object { Test-Path $_ })
}

function Find-NewestLog {
    param([string[]] $Roots, [string] $Filter)
    $all = foreach ($r in $Roots) {
        Get-ChildItem $r -Filter $Filter -File -Recurse -ErrorAction SilentlyContinue
    }
    return $all | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

function Show-Report {
    param([string] $GameDir, [int] $SinceMinutes)

    $roots = Get-LogSearchRoots -GameDir $GameDir
    if ($roots.Count -eq 0) { Write-Host "No log directories found." -ForegroundColor Yellow; return }
    $cutoff = (Get-Date).AddMinutes(-$SinceMinutes)
    Write-Host "Log roots:"; $roots | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    Write-Host "Freshness cutoff: $cutoff (-SinceMinutes $SinceMinutes)" -ForegroundColor DarkGray

    # The 2026-08-08 method caveat, generalised: a stale log read as this run's evidence is
    # indistinguishable from a real result. Every artefact below is stamped fresh or STALE.
    function Write-Freshness {
        param($File)
        if ($File.LastWriteTime -lt $cutoff) {
            Write-Host "  STALE -- $($File.Name) last written $($File.LastWriteTime), before the cutoff." -ForegroundColor Red
            Write-Host "  This is NOT evidence from the run you just did. Confirm the game actually launched." -ForegroundColor Red
            return $false
        }
        Write-Host "  $($File.Name)  ($($File.LastWriteTime))  FRESH" -ForegroundColor Green
        return $true
    }

    Write-Host "`n=== rgl error log ===" -ForegroundColor Cyan
    $rgl = Find-NewestLog -Roots $roots -Filter 'rgl_log_errors_*.txt'
    if (-not $rgl) {
        Write-Host "  none present. The engine writes this file only when it logs errors, so" -ForegroundColor DarkGray
        Write-Host "  absence after an OFF run is a PASS signal -- provided taom_debug below is fresh." -ForegroundColor DarkGray
    }
    else {
        $null = Write-Freshness $rgl
        # Count the three gate strings separately: a bare grep conflates "RDC path invalid"
        # with the benign partial-read chatter, which already ran 1,163-28,863 lines/session
        # BEFORE the experiment and so proves nothing on its own.
        foreach ($pat in 'RDC cache path is not valid', 'Unable to decompress data', 'partial read on compressed asset') {
            $n = @(Select-String -LiteralPath $rgl.FullName -Pattern $pat -SimpleMatch -ErrorAction SilentlyContinue).Count
            Write-Host ("  {0,-42} {1}" -f $pat, $n)
        }
    }

    Write-Host "`n=== taom_debug: load + memory markers ===" -ForegroundColor Cyan
    $dbg = Find-NewestLog -Roots $roots -Filter 'taom_debug_*.log'
    if (-not $dbg) {
        Write-Host "  none found -- no load-time and no commit number, so the run is UNMEASURED." -ForegroundColor Yellow
        Write-Host "  Confirm TAOM actually loaded before reading anything above as a result." -ForegroundColor Yellow
        return
    }
    $fresh = Write-Freshness $dbg
    Select-String -LiteralPath $dbg.FullName -Pattern '\[BattleLoad\]|\[MemSample\]|\[MemProbe\]' |
        Select-Object -Last 25 -ExpandProperty Line | ForEach-Object { "    $_" }

    Show-KeyNumbers -LogPath $dbg.FullName
    if ($fresh) {
        Write-Host "`n  Bucket breakdown: python tools/triage_battle_load.py `"$($dbg.FullName)`"" -ForegroundColor DarkGray
    }
}

function Show-KeyNumbers {
    <#
      The four numbers the decision gate actually compares between the ON and OFF runs.
      Eyeballing 25 log lines twice is how an A/B gets misread, so extract them explicitly.
    #>
    param([string] $LogPath)

    $lines = Get-Content -LiteralPath $LogPath -ErrorAction SilentlyContinue
    if (-not $lines) { return }

    $playable = $lines | Select-String -Pattern 'phase=BattlePlayable' | Select-Object -Last 1
    $finish   = $lines | Select-String -Pattern 'phase=FinishMissionLoadingDone' | Select-Object -Last 1
    $samples  = @($lines | Select-String -Pattern '\[MemSample\]')

    Write-Host "`n  --- KEY NUMBERS (compare ON vs OFF) ---" -ForegroundColor Cyan
    if ($playable -and $playable.Line -match 't=\+(\d+)ms') {
        $ms = [int]$Matches[1]
        $scene = if ($playable.Line -match "scene='([^']+)'") { $Matches[1] } else { '?' }
        Write-Host ("  {0,-26} {1,8:N1} s   scene={2}" -f 'load to BattlePlayable', ($ms / 1000), $scene)
        Write-Host "  (the OFF run must use this same scene, or the comparison is not like-for-like)" -ForegroundColor DarkGray
    }
    else { Write-Host "  load to BattlePlayable     -- not reached in this log" -ForegroundColor Yellow }

    if ($finish -and $finish.Line -match 'privMB=(\d+)') {
        Write-Host ("  {0,-26} {1,8} MB" -f 'privMB @ loading done', $Matches[1])
    }
    if ($samples.Count -gt 0) {
        $peak = ($samples | ForEach-Object { if ($_.Line -match 'privMB=(\d+)') { [int]$Matches[1] } } |
                 Measure-Object -Maximum).Maximum
        $load = if ($samples[-1].Line -match 'memLoad=(\d+)%') { $Matches[1] } else { '?' }
        Write-Host ("  {0,-26} {1,8} MB   ({2} samples)" -f 'peak [MemSample] privMB', $peak, $samples.Count)
        Write-Host ("  {0,-26} {1,8}%" -f 'memLoad at last sample', $load)
    }
    else {
        Write-Host "  no [MemSample] lines -- the run is unmeasured for commit." -ForegroundColor Yellow
    }
}

# ---- dispatch ----------------------------------------------------------------
$gameDir = Resolve-GameDir -Explicit $GameDir

if ($Report) { Show-Report -GameDir $gameDir -SinceMinutes $SinceMinutes; return }

$moduleRoot = Get-ModuleRoot -GameDir $gameDir
Write-Host "Modules: $moduleRoot" -ForegroundColor Cyan

if ($Off) {
    Write-Host "`nRenaming $CACHE -> $OFFNAME" -ForegroundColor Cyan
    $n = Invoke-Toggle -ModuleRoot $moduleRoot -Names $Modules -From $CACHE -To $OFFNAME
    Write-Host "`n$n module(s) toggled OFF." -ForegroundColor Green
    Write-Host @"

Now, with the game launched fresh:
  1. main menu      -- wait 60 s untouched
  2. campaign map   -- load the fixed save, sit 60 s. TAOM_Map was 13,577 of the 23,329
                       RDC ops, so this station carries the most signal and the original
                       runbook omitted it.
  3. custom battle  -- 250v250, same scene/cultures as the baseline run
  4. visual pass    -- a cloth-heavy troop, one creature animation (warg / mumak), one
                       TAOM_Map scene from the big packs. RDC holds cooked texture, mesh,
                       animation-clip and cloth data, so the plausible failure is silent
                       wrong-art, not an exception.
  5. quit, then:  pwsh tools/Invoke-RdcAbTest.ps1 -Report
  6. RESTORE:     pwsh tools/Invoke-RdcAbTest.ps1 -On

Falsification check: if any RuntimeDataCache folder REGENERATES, that contradicts the
editor-only write surface and the whole verdict needs re-deriving.
"@ -ForegroundColor Gray
    Show-Status -ModuleRoot $moduleRoot -Names $Modules
}
elseif ($On) {
    Write-Host "`nRestoring $OFFNAME -> $CACHE" -ForegroundColor Cyan
    $n = Invoke-Toggle -ModuleRoot $moduleRoot -Names $Modules -From $OFFNAME -To $CACHE
    Write-Host "`n$n module(s) restored." -ForegroundColor Green
    Show-Status -ModuleRoot $moduleRoot -Names $Modules
}
else {
    Show-Status -ModuleRoot $moduleRoot -Names $Modules
}
