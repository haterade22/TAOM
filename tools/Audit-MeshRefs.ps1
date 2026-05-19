#Requires -Version 7
<#
.SYNOPSIS
    Audit a Bannerlord module: which mesh="X" refs in ModuleData XML point at
    meshes that don't exist in any .tpac under Assets/?

.DESCRIPTION
    Phase 1 — open every .tpac under <module>/Assets recursively, enumerate
    Metameshes (the XML-addressable names), build a Set of meshes present.

    Phase 2 — scan every .xml under <module>/ModuleData recursively, capture
    every mesh="..." attribute value with source file + line.

    Phase 3 — diff: items referencing missing meshes (orphan items, highest
    priority) and meshes packaged but never referenced (orphan assets,
    informational).

    Outputs go under tools/reports/mesh-audit/<module-name>/ — a REPORT.md plus
    raw .txt and .tsv lists for piping into other tools.

.PARAMETER ModulePath
    Absolute path to the Bannerlord module to audit. e.g.
    "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory"

.PARAMETER TpacToolBin
    Directory containing TpacTool.Lib.dll + TpacTool.IO.dll + LZ4.dll.
    Default: E:\Release_v0.5.1\bin

.PARAMETER OutputDir
    Where to write reports. Default: <repo>/tools/reports/mesh-audit/<module-name>

.EXAMPLE
    pwsh tools/Audit-MeshRefs.ps1 -ModulePath "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory"

.EXAMPLE
    # Pipe orphan-item list into ripgrep to find every troop XML referencing them:
    rg -f (pwsh tools/Audit-MeshRefs.ps1 -ModulePath "...\LOTRLOME_Armory" | Get-Content) ModuleData/
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ModulePath,

    [string]$TpacToolBin = 'E:\Release_v0.5.1\bin',

    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

function Write-Progress2 {
    param([string]$Message)
    [Console]::Error.WriteLine("[audit-mesh-refs] $Message")
}

if (-not (Test-Path $ModulePath -PathType Container)) {
    throw "Module path does not exist or is not a directory: $ModulePath"
}
$ModulePath = (Resolve-Path $ModulePath).Path
$moduleName = Split-Path $ModulePath -Leaf

$tpacLib = Join-Path $TpacToolBin 'TpacTool.Lib.dll'
if (-not (Test-Path $tpacLib)) {
    throw "TpacTool.Lib.dll not found at: $tpacLib. Override with -TpacToolBin or install TpacTool."
}

# Default output dir under the TAOM repo's tools/reports
if (-not $OutputDir) {
    $scriptRoot = Split-Path $PSScriptRoot -Parent
    $OutputDir = Join-Path $scriptRoot "tools\reports\mesh-audit\$moduleName"
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$OutputDir = (Resolve-Path $OutputDir).Path

Write-Progress2 "module:  $ModulePath"
Write-Progress2 "tpaclib: $tpacLib"
Write-Progress2 "output:  $OutputDir"

# -----------------------------------------------------------------------------
# Phase 1 — mesh inventory from .tpac files
# -----------------------------------------------------------------------------

# Load deps. Add-Type resolves co-located deps from the same folder.
[System.Reflection.Assembly]::LoadFrom($tpacLib) | Out-Null

# Metamesh.TYPE_GUID — Bannerlord's static identifier for "named XML-addressable mesh"
$metaGuid = [Guid]'a08f8b97-197c-4bea-b95b-53846cae834e'

$assetsDir = Join-Path $ModulePath 'Assets'
if (-not (Test-Path $assetsDir)) {
    Write-Progress2 "WARNING: no Assets/ dir under module (mesh inventory will be empty)"
    $tpacFiles = @()
} else {
    $tpacFiles = Get-ChildItem -Path $assetsDir -Filter '*.tpac' -Recurse -File
}
Write-Progress2 "phase 1: parsing $($tpacFiles.Count) .tpac files"

# meshName -> tpac file (first wins; collisions are rare and reported separately)
$meshesPresent = [ordered]@{}
$meshCollisions = @()
$failedTpacs = @()

foreach ($tpac in $tpacFiles) {
    try {
        $pkg = New-Object TpacTool.Lib.AssetPackage($tpac.FullName, $true, $false)
        foreach ($item in $pkg.Items) {
            if ($item.Type -ne $metaGuid) { continue }
            $n = $item.Name
            if ([string]::IsNullOrEmpty($n)) { continue }
            if ($meshesPresent.Contains($n)) {
                $meshCollisions += [pscustomobject]@{
                    Mesh     = $n
                    FirstIn  = $meshesPresent[$n]
                    AlsoIn   = $tpac.FullName
                }
            } else {
                $meshesPresent[$n] = $tpac.FullName
            }
        }
    } catch {
        $failedTpacs += [pscustomobject]@{
            File  = $tpac.FullName
            Error = $_.Exception.Message
        }
        Write-Progress2 "  FAILED to read $($tpac.Name): $($_.Exception.Message)"
    }
}

Write-Progress2 "phase 1: $($meshesPresent.Count) unique mesh names, $($meshCollisions.Count) duplicate names across .tpacs, $($failedTpacs.Count) read failures"

# Sort and write present-meshes list
$meshesPresentSorted = $meshesPresent.Keys | Sort-Object
$presentTxt = Join-Path $OutputDir 'meshes-present.txt'
$meshesPresentSorted | Set-Content -Path $presentTxt -Encoding UTF8

# -----------------------------------------------------------------------------
# Phase 2 — mesh references from ModuleData XML
# -----------------------------------------------------------------------------

$moduleDataDir = Join-Path $ModulePath 'ModuleData'
if (-not (Test-Path $moduleDataDir)) {
    throw "No ModuleData/ dir under module: $moduleDataDir"
}

$xmlFiles = Get-ChildItem -Path $moduleDataDir -Filter '*.xml' -Recurse -File
Write-Progress2 "phase 2: scanning $($xmlFiles.Count) XML files"

# Captures every mesh-name reference. Bannerlord items use `mesh=` for the
# visible mesh plus `holster_mesh=`, `holster_mesh_with_weapon=`, `flying_mesh=`
# for ancillary visuals. Race/skin XML uses `hands_mesh`, `legs_mesh`,
# `face_meta_mesh`, `body_meta_mesh*`, `underwear_*_mesh`. Skipped:
# `mesh_maturity_type` (enum, not a mesh name) and `holster_mesh_length` (numeric).
$meshAttrRegex = [regex]'(?<=\s)(mesh|holster_mesh|holster_mesh_with_weapon|flying_mesh|hands_mesh|legs_mesh|face_meta_mesh|body_meta_mesh|body_meta_mesh_upperbody|body_meta_mesh_shoulders|underwear_top_mesh|underwear_bottom_mesh)="([^"]+)"'

$meshRefs = New-Object System.Collections.Generic.List[object]
foreach ($xml in $xmlFiles) {
    $lineNum = 0
    foreach ($line in [System.IO.File]::ReadLines($xml.FullName)) {
        $lineNum++
        $hits = $meshAttrRegex.Matches($line)
        foreach ($m in $hits) {
            $val = $m.Groups[2].Value
            if ([string]::IsNullOrEmpty($val)) { continue }
            $meshRefs.Add([pscustomobject]@{
                Mesh = $val
                Attr = $m.Groups[1].Value
                File = $xml.FullName
                Line = $lineNum
            })
        }
    }
}

Write-Progress2 "phase 2: $($meshRefs.Count) mesh references across XML files"

# Unique referenced meshes
$meshesReferenced = $meshRefs.Mesh | Sort-Object -Unique
$referencedTxt = Join-Path $OutputDir 'meshes-referenced.txt'
$meshesReferenced | Set-Content -Path $referencedTxt -Encoding UTF8

# Detailed reference TSV
$detailedTsv = Join-Path $OutputDir 'meshes-referenced-detailed.tsv'
"mesh`tfile`tline" | Set-Content -Path $detailedTsv -Encoding UTF8
$meshRefs | ForEach-Object { "$($_.Mesh)`t$($_.File)`t$($_.Line)" } | Add-Content -Path $detailedTsv -Encoding UTF8

# -----------------------------------------------------------------------------
# Phase 3 — diff
# -----------------------------------------------------------------------------

$presentSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$meshesPresentSorted, [System.StringComparer]::Ordinal)
$referencedSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$meshesReferenced, [System.StringComparer]::Ordinal)

$orphanItems = $meshesReferenced | Where-Object { -not $presentSet.Contains($_) }
$orphanAssets = $meshesPresentSorted | Where-Object { -not $referencedSet.Contains($_) }

Write-Progress2 "phase 3: $($orphanItems.Count) orphan items (XML refs with no mesh), $($orphanAssets.Count) orphan assets (meshes nothing references)"

# Group orphan items by mesh name for the report
$orphanItemsGrouped = $orphanItems | ForEach-Object {
    $mesh = $_
    $refs = $meshRefs | Where-Object { $_.Mesh -eq $mesh }
    $exampleFile = $refs[0].File
    $exampleLine = $refs[0].Line
    [pscustomobject]@{
        Mesh    = $mesh
        Count   = $refs.Count
        Example = "$exampleFile`:$exampleLine"
        AllRefs = $refs
    }
} | Sort-Object -Property @{Expression='Count'; Descending=$true}, @{Expression='Mesh'}

# Write REPORT.md
$reportPath = Join-Path $OutputDir 'REPORT.md'
$reportLines = [System.Collections.Generic.List[string]]::new()
$reportLines.Add("# Mesh audit: $moduleName")
$reportLines.Add('')
$reportLines.Add("Generated by `tools/Audit-MeshRefs.ps1` at $(Get-Date -Format o).")
$reportLines.Add('')
$reportLines.Add('## Summary')
$reportLines.Add('')
$reportLines.Add("- .tpac files parsed: $($tpacFiles.Count - $failedTpacs.Count) / $($tpacFiles.Count)" + $(if ($failedTpacs.Count) { " (**$($failedTpacs.Count) failed**)" } else { '' }))
$reportLines.Add("- Unique meshes packaged: $($meshesPresent.Count)")
$reportLines.Add("- XML files scanned: $($xmlFiles.Count)")
$reportLines.Add("- Mesh references in XML: $($meshRefs.Count) (across $($meshesReferenced.Count) unique mesh names)")
$reportLines.Add("- **Orphan items (refs with no mesh):** $($orphanItems.Count)")
$reportLines.Add("- Orphan assets (meshes nothing references): $($orphanAssets.Count)")
if ($meshCollisions.Count) { $reportLines.Add("- Mesh-name collisions across .tpacs: $($meshCollisions.Count)") }
$reportLines.Add('')

if ($failedTpacs.Count) {
    $reportLines.Add('## .tpac read failures')
    $reportLines.Add('')
    $reportLines.Add('| File | Error |')
    $reportLines.Add('|---|---|')
    foreach ($f in $failedTpacs) {
        $reportLines.Add("| ``$($f.File)`` | $($f.Error -replace '\|','\\|') |")
    }
    $reportLines.Add('')
}

$reportLines.Add('## Orphan items — references to missing meshes')
$reportLines.Add('')
if ($orphanItems.Count -eq 0) {
    $reportLines.Add('_None. Every `mesh="..."` in XML resolves to a packaged mesh._')
} else {
    $reportLines.Add('Each row is a mesh name referenced by XML that does NOT exist in any `.tpac` under `Assets/`. Either delete the referencing items or substitute the mesh with a packaged one.')
    $reportLines.Add('')
    $reportLines.Add('| Mesh | Refs | Example |')
    $reportLines.Add('|---|---:|---|')
    foreach ($g in $orphanItemsGrouped) {
        $rel = $g.Example.Substring($ModulePath.Length).TrimStart('\','/')
        $reportLines.Add("| ``$($g.Mesh)`` | $($g.Count) | ``$rel`` |")
    }
    $reportLines.Add('')
    $reportLines.Add('### All orphan references (detailed)')
    $reportLines.Add('')
    foreach ($g in $orphanItemsGrouped) {
        $reportLines.Add("**``$($g.Mesh)``** — $($g.Count) reference(s):")
        $reportLines.Add('')
        foreach ($r in $g.AllRefs) {
            $rel = $r.File.Substring($ModulePath.Length).TrimStart('\','/')
            $reportLines.Add("- ``$rel`` line $($r.Line)")
        }
        $reportLines.Add('')
    }
}
$reportLines.Add('')

$reportLines.Add('## Orphan assets — packaged meshes nothing references')
$reportLines.Add('')
if ($orphanAssets.Count -eq 0) {
    $reportLines.Add('_None. Every packaged mesh is referenced by at least one XML._')
} else {
    $reportLines.Add('_Informational._ These meshes exist in `.tpac` but no XML `mesh="..."` attribute names them. Could be intentional (unused art kept for future use) or genuine dead weight.')
    $reportLines.Add('')
    $reportLines.Add('| Mesh | Source .tpac |')
    $reportLines.Add('|---|---|')
    foreach ($m in $orphanAssets) {
        $src = $meshesPresent[$m]
        $rel = $src.Substring($ModulePath.Length).TrimStart('\','/')
        $reportLines.Add("| ``$m`` | ``$rel`` |")
    }
    $reportLines.Add('')
}

if ($meshCollisions.Count) {
    $reportLines.Add('## Mesh-name collisions')
    $reportLines.Add('')
    $reportLines.Add('Same mesh name appears in multiple `.tpac` files. First occurrence wins in the inventory; usually means the file was duplicated or a variant was added without renaming.')
    $reportLines.Add('')
    $reportLines.Add('| Mesh | First In | Also In |')
    $reportLines.Add('|---|---|---|')
    foreach ($c in $meshCollisions) {
        $f1 = $c.FirstIn.Substring($ModulePath.Length).TrimStart('\','/')
        $f2 = $c.AlsoIn.Substring($ModulePath.Length).TrimStart('\','/')
        $reportLines.Add("| ``$($c.Mesh)`` | ``$f1`` | ``$f2`` |")
    }
    $reportLines.Add('')
}

$reportLines.Add('## Files')
$reportLines.Add('')
$reportLines.Add('| File | Contents |')
$reportLines.Add('|---|---|')
$reportLines.Add('| `meshes-present.txt` | One mesh name per line, sorted. Every Metamesh found in any `.tpac` under the module. |')
$reportLines.Add('| `meshes-referenced.txt` | One mesh name per line, sorted, unique. Every `mesh="..."` value from any XML under `ModuleData/`. |')
$reportLines.Add('| `meshes-referenced-detailed.tsv` | TSV of (mesh, file, line) — useful for `grep`/`rg` follow-up. |')
$reportLines.Add('| `REPORT.md` | This file. |')

[System.IO.File]::WriteAllLines($reportPath, $reportLines)

# Stdout: emit the report path so callers can `Get-Content` it or open it.
Write-Output $reportPath
