#Requires -Version 7
<#
.SYNOPSIS
    Bulk-decompile a Bannerlord bin/Win64_Shipping_Client folder into a categorized output tree.

.DESCRIPTION
    Wraps `ilspycmd -p <dll> -o <dst>` for every TaleWorlds-owned DLL and organizes results
    into the same folder layout as the legacy `E:\Decompiled_Bannerlord\` dump.

    Categories: Campaign, MountAndBlade, Modules, Core, Engine, UI, Network, Platform, Launcher, ThirdParty.

.EXAMPLE
    pwsh tools/decompile_to_folder.ps1 -Source "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client" -Destination "E:\Decompiled_Bannerlord"

.NOTES
    Used in S0 of the v1.3.15 → v1.4.5 migration. ilspycmd 10.0.1+ required (project-mode decompile).
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Source)) {
    throw "Source bin dir not found: $Source"
}

if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
    throw "ilspycmd not found on PATH. Install with: dotnet tool install -g ilspycmd"
}

# Category map: DLL name pattern → category folder
# Order matters: more specific patterns before generic ones.
$Categories = @(
    @{ Pattern = 'TaleWorlds\.CampaignSystem(\.|$)';                            Folder = 'Campaign' },
    @{ Pattern = 'TaleWorlds\.MountAndBlade(\.|$)';                             Folder = 'MountAndBlade' },
    @{ Pattern = '^(SandBox|SandBoxCore|StoryMode)\.dll$';                      Folder = 'Modules' },
    @{ Pattern = 'TaleWorlds\.(Core|Library|SaveSystem|Localization|LinQuick|ObjectSystem|Starter)(\.|$)'; Folder = 'Core' },
    @{ Pattern = 'TaleWorlds\.(Engine|DotNet|InputSystem|ScreenSystem|NavigationSystem|TwoDimension)(\.|$)'; Folder = 'Engine' },
    @{ Pattern = 'TaleWorlds\.(GauntletUI|MountAndBlade\.GauntletUI|PSAI)(\.|$)'; Folder = 'UI' },
    @{ Pattern = 'TaleWorlds\.(Diamond|Network|PlayerServices|ServiceDiscovery)(\.|$)'; Folder = 'Network' },
    @{ Pattern = 'TaleWorlds\.(PlatformService|AchievementSystem|ActivitySystem|ModuleManager)(\.|$)'; Folder = 'Platform' },
    @{ Pattern = 'TaleWorlds\.MountAndBlade\.Launcher';                          Folder = 'Launcher' },
    @{ Pattern = '^(Newtonsoft\.Json|Steamworks\.NET|jose-jwt|StbSharp|GalaxyCSharp.*|EOSSDK.*)\.dll$'; Folder = 'ThirdParty' }
)

function Get-Category {
    param([string]$DllName)
    foreach ($cat in $Categories) {
        if ($DllName -match $cat.Pattern) {
            return $cat.Folder
        }
    }
    return $null
}

# Capture version stamp
$versionXml = Join-Path $Source 'Version.xml'
$version = 'unknown'
if (Test-Path $versionXml) {
    $xml = [xml](Get-Content $versionXml -Raw)
    $version = $xml.Version.Singleplayer.Value
}
Write-Host "Decompiling Bannerlord $version" -ForegroundColor Cyan
Write-Host "  Source: $Source"
Write-Host "  Dest:   $Destination"
Write-Host ""

if ((Test-Path $Destination) -and (-not $Force)) {
    $contents = @(Get-ChildItem -Path $Destination -ErrorAction SilentlyContinue)
    if ($contents.Count -gt 0) {
        throw "Destination is not empty: $Destination. Use -Force to overwrite or move it aside first."
    }
}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null

# Manifest (records what was decompiled + when + version)
$manifest = [ordered]@{
    version       = $version
    decompiled_at = (Get-Date).ToString('o')
    source        = $Source
    destination   = $Destination
    ilspycmd_version = (& ilspycmd --version 2>&1 | Select-Object -First 1)
    dlls          = @()
}

$dlls = Get-ChildItem -Path $Source -Filter '*.dll' -File | Sort-Object Name

# Codex review 2026-05-22 (P2): also enumerate module DLLs under <install>/Modules/<X>/bin/<binFolder>/.
# The "Modules" category pattern matches SandBox|SandBoxCore|StoryMode but those DLLs do NOT live
# in <install>/bin/<binFolder>/ — they live in <install>/Modules/<X>/bin/<binFolder>/. Without this,
# the Modules category never gets populated and "rebuild Modules" is silently incomplete.
$installRoot = (Get-Item $Source).Parent.Parent.FullName   # walk up: bin/<binFolder> -> bin -> install root
$binFolder = (Get-Item $Source).Name                        # "Win64_Shipping_Client" or similar
$modulesRoot = Join-Path $installRoot 'Modules'
$moduleDllsAdded = 0
if (Test-Path $modulesRoot) {
    $moduleNames = @('SandBox', 'SandBoxCore', 'StoryMode', 'CustomBattle')
    foreach ($modName in $moduleNames) {
        $modBin = Join-Path $modulesRoot (Join-Path $modName (Join-Path 'bin' $binFolder))
        if (Test-Path $modBin) {
            $modDll = Join-Path $modBin "${modName}.dll"
            if (Test-Path $modDll) {
                $dlls += Get-Item $modDll
                $moduleDllsAdded++
            }
        }
    }
    $dlls = $dlls | Sort-Object Name
    Write-Host "  Module DLLs added from ${modulesRoot}: $moduleDllsAdded" -ForegroundColor Cyan
}

$total = $dlls.Count
$idx = 0
$startTime = Get-Date

foreach ($dll in $dlls) {
    $idx++
    $category = Get-Category -DllName $dll.Name
    if (-not $category) {
        Write-Host "[$idx/$total] SKIP   $($dll.Name) (no category match)" -ForegroundColor DarkGray
        continue
    }

    $dllStem = [System.IO.Path]::GetFileNameWithoutExtension($dll.Name)
    $outDir = Join-Path $Destination (Join-Path $category $dllStem)
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    Write-Host "[$idx/$total] DECOMP $($dll.Name) -> $category/$dllStem/" -ForegroundColor Green
    $cmdStart = Get-Date

    # ilspycmd -p decompiles in "project" mode (one .cs per type with proper namespaces).
    # Capture both streams; on error, log a stub and continue.
    $logFile = Join-Path $outDir '_decompile.log'
    & ilspycmd -p $dll.FullName -o $outDir 2>&1 | Tee-Object -FilePath $logFile -Append | Out-Null
    $exitCode = $LASTEXITCODE

    $cmdDuration = ((Get-Date) - $cmdStart).TotalSeconds
    $fileCount = (Get-ChildItem -Path $outDir -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | Measure-Object).Count

    $manifest.dlls += [ordered]@{
        dll       = $dll.Name
        category  = $category
        out_dir   = (Resolve-Path $outDir -Relative).ToString()
        cs_files  = $fileCount
        duration_s = [math]::Round($cmdDuration, 1)
        exit_code = $exitCode
    }

    Write-Host "         => $fileCount .cs files in $([math]::Round($cmdDuration, 1))s (exit $exitCode)" -ForegroundColor DarkGray
}

$totalDuration = ((Get-Date) - $startTime).TotalSeconds
$manifest.total_duration_s = [math]::Round($totalDuration, 1)

# Write manifest
$manifestPath = Join-Path $Destination '_manifest.json'
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host ""
Write-Host "DONE. $($manifest.dlls.Count) DLLs decompiled in $([math]::Round($totalDuration, 1))s ($([math]::Round($totalDuration/60, 1)) min)" -ForegroundColor Cyan
Write-Host "Manifest: $manifestPath"
