<#
.SYNOPSIS
    Sweep backup sidecars out of the shipped Bannerlord module folders into a dated quarantine.

.DESCRIPTION
    LOTRLOME_Armory, TAOM_Map and TAOM accumulate backup sidecars from the tools under tools/
    (see tools/README.md "XML I/O convention"). They must not ship: .bak breaks the Cloudflare
    distribution, and a stale .bak beside a live XML has twice been mistaken for engine load
    surface (docs/investigations/native-commit-audit-2026-08.md against the later CHANGELOG
    lesson, which found the engine never loads them).

    Those two modules are NOT git-tracked, so their sidecars are the only rollback history their
    live XML has. This script therefore MOVES rather than deletes, writing a SHA256 manifest
    before it touches anything.

    Dry-run by default. Pass -Apply to move.

.PARAMETER Apply
    Perform the moves. Without it the script only reports.

.PARAMETER MaxOrphans
    Abort on -Apply if more files than this carry a backup suffix but have no live sibling.
    A file with no live counterpart is not a backup, it is the sole copy. 3 were measured on
    2026-09-01; a higher count means an asset lost its live copy since, and wants a look first.

.PARAMETER SkipSceneBackups
    Leave the Modding Kit SceneObj\Backups and SceneEditData\Backups folders alone.

.EXAMPLE
    pwsh tools/sweep_module_backups.ps1
    pwsh tools/sweep_module_backups.ps1 -Apply
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [int]$MaxOrphans = 3,
    [switch]$SkipSceneBackups,
    [string]$ModulesRoot,
    [string]$QuarantineRoot,
    [string]$RepoModuleRoot,
    [switch]$SkipRepoModule
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# --- resolve roots -----------------------------------------------------------------------------

if (-not $ModulesRoot) {
    if ($env:BANNERLORD_GAME_DIR) {
        $ModulesRoot = Join-Path $env:BANNERLORD_GAME_DIR 'Modules'
    } else {
        $ModulesRoot = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules'
    }
}
if (-not (Test-Path -LiteralPath $ModulesRoot)) {
    throw "Modules root not found: $ModulesRoot. Set BANNERLORD_GAME_DIR or pass -ModulesRoot."
}
if (-not $QuarantineRoot) {
    $QuarantineRoot = "E:\Bannerlord_Backups\module_bak_sweep_$(Get-Date -Format 'yyyy-MM-dd')"
}
# A quarantine inside the modules tree would be re-scanned by the next run and swept into itself.
if ($QuarantineRoot -like "$ModulesRoot*") {
    throw "-QuarantineRoot ($QuarantineRoot) is inside -ModulesRoot. Put it outside the game install."
}

$Modules = @('LOTRLOME_Armory', 'TAOM_Map', 'TAOM', 'TAOM.Dependencies')

# Scan roots are (label, path) pairs. The label becomes the quarantine subfolder.
$roots = [System.Collections.Generic.List[object]]::new()
foreach ($module in $Modules) {
    $path = Join-Path $ModulesRoot $module
    if (Test-Path -LiteralPath $path) { $roots.Add([pscustomobject]@{ Label = $module; Path = $path }) }
}

# The repo's own _Module tree re-seeds the install on every build: TAOM.csproj's CopyModule target
# "recurses _Module verbatim and deploys whatever it finds" (its own comment, and why it already
# guards against deploying a .vs folder). Sweeping only the install therefore reports clean and is
# undone by the next build, so the repo side is part of the same gate. These files are invisible to
# `git status` because .gitignore covers *.bak*.
if (-not $SkipRepoModule) {
    if (-not $RepoModuleRoot) {
        $RepoModuleRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'Main\_Module'
    }
    if (Test-Path -LiteralPath $RepoModuleRoot) {
        $roots.Add([pscustomobject]@{ Label = '_repo_Main_Module'; Path = $RepoModuleRoot })
    }
}

# A backup suffix sits AFTER the real extension. The dated forms dominate (.bak-armoryloc,
# .bak-preskel, .bak-guidremap), so a bare *.bak glob matches barely 3% of them.
$SuffixRx = '(?i)^(?<live>.+?)(?<suffix>' +
            '\.bak(\d+|[-_][A-Za-z0-9._-]*)?' +
            '|\.backup' +
            '|\.orig' +
            '|\.prev(\d+|[-_][A-Za-z0-9._-]*)?' +
            '|\.old' +
            '|\.tmp' +
            '|\.transplanted-\d+' +
            ')$'

function Get-Category {
    param([string]$Relative)
    # A file directly under the module root has no directory component, so splitting would yield
    # the file's own name and give every such file its own category.
    if ($Relative -notmatch '\\') { return 'Root' }
    $top = $Relative.Split('\')[0]
    switch ($top) {
        'ModuleData'   { 'ModuleData' }
        'Assets'       { 'Assets' }
        'AssetSources' { 'AssetSources' }
        default        { $top }
    }
}

# --- inventory ---------------------------------------------------------------------------------

$items = [System.Collections.Generic.List[object]]::new()
$sceneRoots = [System.Collections.Generic.List[string]]::new()

foreach ($root in $roots) {
    $module = $root.Label
    $moduleRoot = $root.Path
    $prefix = $moduleRoot.Length + 1

    foreach ($file in (Get-ChildItem -LiteralPath $moduleRoot -Recurse -File -ErrorAction SilentlyContinue)) {
        if ($file.Name -notmatch $SuffixRx) { continue }
        $live = Join-Path $file.DirectoryName $Matches['live']
        $relative = $file.FullName.Substring($prefix)
        $items.Add([pscustomobject]@{
            Module   = $module
            Relative = $relative
            Category = Get-Category $relative
            Suffix   = $Matches['suffix']
            Bytes    = $file.Length
            Orphan   = -not (Test-Path -LiteralPath $live)
            Source   = $file.FullName
        })
    }

    if (-not $SkipSceneBackups) {
        foreach ($parent in @('SceneObj', 'SceneEditData')) {
            $dir = Join-Path $moduleRoot "$parent\Backups"
            if (-not (Test-Path -LiteralPath $dir)) { continue }
            $sceneRoots.Add($dir)
            foreach ($file in (Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue)) {
                $relative = $file.FullName.Substring($prefix)
                $items.Add([pscustomobject]@{
                    Module   = $module
                    Relative = $relative
                    Category = 'SceneBackups'
                    Suffix   = ''
                    Bytes    = $file.Length
                    Orphan   = $false
                    Source   = $file.FullName
                })
            }
        }
    }
}

# --- report ------------------------------------------------------------------------------------

# Measure-Object emits no object at all for an empty pipeline, so reaching for .Sum on the clean
# tree throws under StrictMode. Guard on the count, not on the result.
$totalBytes = if ($items.Count) { ($items | Measure-Object Bytes -Sum).Sum } else { 0 }

Write-Host ''
Write-Host "Modules root : $ModulesRoot"
Write-Host "Quarantine   : $QuarantineRoot"
Write-Host ("Found        : {0} files, {1:N1} MB" -f $items.Count, ($totalBytes / 1MB))
Write-Host ''

if ($items.Count -eq 0) {
    Write-Host 'Nothing to sweep. Modules are clean.'
    exit 0
}

Write-Host 'By module and category:'
$items | Group-Object Module, Category | Sort-Object Name | ForEach-Object {
    "{0,5}  {1,9:N1} MB  {2}" -f $_.Count, (($_.Group | Measure-Object Bytes -Sum).Sum / 1MB), $_.Name
} | Write-Host

$transplanted = @($items | Where-Object { $_.Suffix -like '.transplanted-*' })
if ($transplanted.Count) {
    Write-Host ''
    Write-Host "$($transplanted.Count) .transplanted-* sidecar(s) included (dead sidecar of a live file, not strictly a backup):"
    $transplanted | ForEach-Object { "  $($_.Module)\$($_.Relative)" } | Write-Host
}

$orphans = @($items | Where-Object { $_.Orphan })
Write-Host ''
if ($orphans.Count) {
    Write-Host "$($orphans.Count) file(s) carry a backup suffix but have NO live sibling. These are the sole copy, not a backup:"
    $orphans | Sort-Object Module, Relative | ForEach-Object {
        "  {0,7:N1} MB  {1}\{2}" -f ($_.Bytes / 1MB), $_.Module, $_.Relative
    } | Write-Host
} else {
    Write-Host 'No orphans: every match has a live sibling.'
}

if (-not $Apply) {
    Write-Host ''
    Write-Host 'DRY RUN. Nothing moved. Re-run with -Apply to move these into the quarantine.'
    exit 0
}

if ($orphans.Count -gt $MaxOrphans) {
    throw ("Orphan count {0} exceeds -MaxOrphans {1}. An asset lost its live copy since the last " +
           "survey; review the list above before sweeping." -f $orphans.Count, $MaxOrphans)
}

# --- manifest (written and flushed BEFORE the first move) --------------------------------------

if (-not (Test-Path -LiteralPath $QuarantineRoot)) {
    New-Item -ItemType Directory -Path $QuarantineRoot -Force | Out-Null
}
# The default quarantine root is date-stamped, so a second sweep on the same day lands here. Give
# the later run its own manifest rather than refusing: the per-file destination-exists check below
# is what actually prevents clobbering, and refusing would block the ordinary
# sweep, do more data work, sweep again sequence.
$manifestPath = Join-Path $QuarantineRoot 'MANIFEST.csv'
if (Test-Path -LiteralPath $manifestPath) {
    $manifestPath = Join-Path $QuarantineRoot "MANIFEST-$(Get-Date -Format 'HHmmss').csv"
    Write-Host "MANIFEST.csv exists from an earlier sweep; this run writes $(Split-Path $manifestPath -Leaf)."
}

Write-Host ''
Write-Host "Hashing $($items.Count) files..."
$rows = foreach ($item in $items) {
    [pscustomobject]@{
        Module   = $item.Module
        Relative = $item.Relative
        Category = $item.Category
        Suffix   = $item.Suffix
        Bytes    = $item.Bytes
        Orphan   = $item.Orphan
        Sha256   = (Get-FileHash -LiteralPath $item.Source -Algorithm SHA256).Hash
    }
}
$rows | Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding UTF8
Write-Host "Manifest written: $manifestPath"

# --- move --------------------------------------------------------------------------------------

$moved = 0
foreach ($item in $items) {
    $destination = Join-Path (Join-Path $QuarantineRoot $item.Module) $item.Relative
    if (Test-Path -LiteralPath $destination) {
        throw "Destination already exists, refusing to overwrite: $destination"
    }
    $destinationDir = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $destinationDir)) {
        New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
    }
    Move-Item -LiteralPath $item.Source -Destination $destination
    $moved++
}
Write-Host ("Moved {0} files, {1:N1} MB" -f $moved, ($totalBytes / 1MB))

# Prune the now-empty scene Backups folders. Every other directory is left alone: a folder that
# held a sidecar usually holds its live sibling too.
foreach ($dir in $sceneRoots) {
    if (-not (Test-Path -LiteralPath $dir)) { continue }
    # Files only: the tree is left holding empty subdirectories after the move, and counting
    # those as content leaves the skeleton behind.
    if (@(Get-ChildItem -LiteralPath $dir -Recurse -File -Force -ErrorAction SilentlyContinue).Count -eq 0) {
        Remove-Item -LiteralPath $dir -Recurse -Force
        Write-Host "Removed empty folder: $dir"
    }
}

# --- verify ------------------------------------------------------------------------------------

Write-Host ''
Write-Host 'Verifying...'
$sample = @($rows | Get-Random -Count ([Math]::Min(10, @($rows).Count)))
$bad = 0
foreach ($row in $sample) {
    $destination = Join-Path (Join-Path $QuarantineRoot $row.Module) $row.Relative
    $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($actual -ne $row.Sha256) { Write-Host "  HASH MISMATCH: $destination"; $bad++ }
}
Write-Host "  Re-hashed $($sample.Count) sampled files at the destination, $bad mismatch(es)."

# Re-scan both halves of what was swept. Checking only $SuffixRx would miss the scene Backups
# folders entirely (their files are named scene.xscene, terrain.bin and so on), so a scene file
# left behind would report clean and exit 0.
$remaining = 0
foreach ($root in $roots) {
    $remaining += @(Get-ChildItem -LiteralPath $root.Path -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match $SuffixRx }).Count
}
foreach ($dir in $sceneRoots) {
    if (-not (Test-Path -LiteralPath $dir)) { continue }
    $remaining += @(Get-ChildItem -LiteralPath $dir -Recurse -File -Force -ErrorAction SilentlyContinue).Count
}
Write-Host "  Backup sidecars remaining in the modules: $remaining"

if ($bad -gt 0 -or $remaining -gt 0) { exit 1 }
Write-Host ''
Write-Host 'Sweep complete.'
