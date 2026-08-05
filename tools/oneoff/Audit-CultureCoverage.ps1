#Requires -Version 7
<#
.SYNOPSIS
    For each culture, list Armory items (and their meshes) that no TAOM
    troop or equipment-set of that culture equips.

.DESCRIPTION
    Three-way join:
      1. Armory items     — itemId → (mesh, culture, sourceFile)
      2. TAOM troop XML   — itemIds referenced under troops_<culture>.xml
                            and taom_equipment_sets_<culture>.xml
      3. TAOM generic XML — itemIds referenced under lords.xml,
                            taom_char_creation_equipment.xml,
                            taom_wanderer_equipment.xml, npcs_*.xml,
                            child/education templates

    Per culture, classify each declared item as one of:
      - properly-used : referenced by own-culture troops/equipment-sets
      - cross-culture : referenced ONLY by another culture's troops
      - hero-only     : referenced ONLY by generic files (lords/char_creation/etc.)
      - UNUSED        : referenced nowhere in TAOM

    The headline output is the UNUSED list per culture (with the mesh name —
    so the user can see what visual art is sitting on disk going unworn).

.PARAMETER ArmoryPath
    LOTRLOME_Armory module root. Default: typical Steam install path.

.PARAMETER TaomModuleData
    TAOM's ModuleData root. Default: <repo>/Main/_Module/ModuleData

.PARAMETER OutputDir
    Where to write the report. Default: tools/reports/culture-coverage/

.EXAMPLE
    pwsh tools/Audit-CultureCoverage.ps1
#>

param(
    [string]$ArmoryPath = 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory',
    [string]$TaomModuleData,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

function Write-Progress2 {
    param([string]$Message)
    [Console]::Error.WriteLine("[culture-coverage] $Message")
}

# Default TAOM ModuleData
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $TaomModuleData) {
    $TaomModuleData = Join-Path $repoRoot 'Main\_Module\ModuleData'
}
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'tools\reports\culture-coverage'
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$OutputDir = (Resolve-Path $OutputDir).Path

Write-Progress2 "armory:        $ArmoryPath"
Write-Progress2 "taom data:     $TaomModuleData"
Write-Progress2 "output:        $OutputDir"

# -----------------------------------------------------------------------------
# Phase 1 — Item inventory from Armory (and TAOM-defined items, if any)
# -----------------------------------------------------------------------------

$armoryItemsDir = Join-Path $ArmoryPath 'ModuleData\LOTRLOME_items'
if (-not (Test-Path $armoryItemsDir)) {
    throw "Armory items dir not found: $armoryItemsDir"
}

# itemId -> [pscustomobject]@{ Id, Mesh, Culture, SourceFile }
$items = @{}

# Use a single multiline regex per file: `<Item ... id="..." ... mesh="..." ... culture="Culture.X" ... />` or with body.
# Bannerlord item attributes can be in any order; capture independently per file.
$idRx      = [regex]'<Item\s[^>]*?\bid="([^"]+)"'
$meshAttr  = [regex]'\bmesh="([^"]+)"'
$cultAttr  = [regex]'\bculture="Culture\.([^"]+)"'
$typeAttr  = [regex]'\bType="([^"]+)"'
$subtypeAttr = [regex]'\bsubtype="([^"]+)"'

function Read-ItemsFile {
    param([string]$Path)

    $text = [System.IO.File]::ReadAllText($Path)
    # Split on <Item to get each item block (skip preamble).
    $parts = $text -split '(?=<Item\s)' | Where-Object { $_ -match '^\s*<Item\s' }
    foreach ($p in $parts) {
        $idM = $idRx.Match($p)
        if (-not $idM.Success) { continue }
        $id = $idM.Groups[1].Value
        $meshM = $meshAttr.Match($p)
        $cultM = $cultAttr.Match($p)
        $typeM = $typeAttr.Match($p)
        $subM  = $subtypeAttr.Match($p)
        $items[$id] = [pscustomobject]@{
            Id         = $id
            Mesh       = if ($meshM.Success) { $meshM.Groups[1].Value } else { '' }
            Culture    = if ($cultM.Success) { $cultM.Groups[1].Value } else { '' }
            Type       = if ($typeM.Success) { $typeM.Groups[1].Value } else { '' }
            Subtype    = if ($subM.Success)  { $subM.Groups[1].Value  } else { '' }
            SourceFile = $Path
        }
    }
}

$armoryItemXmls = Get-ChildItem -Path $armoryItemsDir -Filter '*.xml' -Recurse -File
foreach ($f in $armoryItemXmls) {
    Read-ItemsFile -Path $f.FullName
}
Write-Progress2 "phase 1: parsed $($items.Count) items from $($armoryItemXmls.Count) Armory XMLs"

# Also pick up any items defined under TAOM (rare but possible)
$taomItemXmls = @(Get-ChildItem -Path $TaomModuleData -Filter '*.xml' -Recurse -File |
    Where-Object { $_.FullName -match '\\(items|equipment)\\' -or $_.Name -like 'taom_*items*.xml' })
foreach ($f in $taomItemXmls) {
    Read-ItemsFile -Path $f.FullName
}
Write-Progress2 "phase 1: total $($items.Count) items after TAOM scan"

# -----------------------------------------------------------------------------
# Phase 2 — Item-id references across TAOM XML
# -----------------------------------------------------------------------------

# All TAOM XML files
$taomXmls = Get-ChildItem -Path $TaomModuleData -Filter '*.xml' -Recurse -File

# Per-file: set of Item.X ids referenced
# fileRefs[fullPath] = HashSet[string]
$fileRefs = @{}
$itemRefRx = [regex]'\bid="Item\.([^"]+)"'

foreach ($f in $taomXmls) {
    $text = [System.IO.File]::ReadAllText($f.FullName)
    $hits = $itemRefRx.Matches($text)
    if ($hits.Count -eq 0) { continue }
    $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($m in $hits) { [void]$set.Add($m.Groups[1].Value) }
    $fileRefs[$f.FullName] = $set
}
Write-Progress2 "phase 2: $($fileRefs.Count) TAOM XML files contain Item.* references"

# -----------------------------------------------------------------------------
# Phase 3 — Classify each TAOM XML file by associated culture (filename-based)
# -----------------------------------------------------------------------------

# Map culture suffix -> canonical culture id (matching Armory item culture values)
# Most TAOM files use file-suffix == culture id; XSLT cultures (rohan etc.) use lore names in filenames but engine ids in Armory.
$cultureAliases = @{
    'gondor'      = 'gondor'
    'mordor'      = 'mordor'
    'erebor'      = 'erebor'
    'rivendell'   = 'rivendell'
    'lothlorien'  = 'lothlorien'
    'mirkwood'    = 'mirkwood'
    'isengard'    = 'isengard'
    'gundabad'    = 'gundabad'
    'dolguldur'   = 'dolguldur'
    'umbar'       = 'umbar'
    # XSLT cultures — TAOM filenames use LOTR names; Armory items use engine ids
    'rohan'       = 'vlandia'
    'dunland'     = 'empire'
    'harad'       = 'aserai'
    'rhun'        = 'khuzait'
    'rhun_new'    = 'khuzait'
    'dale'        = 'sturgia'
    'khand'       = 'battania'
}

# Classify file -> 'culture:X' OR 'generic'
function Get-FileClassification {
    param([string]$Path)
    $name = Split-Path $Path -Leaf
    # troops_<culture>.xml or taom_equipment_sets_<culture>.xml -> culture-specific
    if ($name -match '^troops_([a-z0-9_]+)\.xml$') {
        $tag = $Matches[1]
        if ($cultureAliases.ContainsKey($tag)) { return @{ Type='culture'; Culture=$cultureAliases[$tag] } }
    }
    if ($name -match '^taom_equipment_sets_([a-z0-9_]+)\.xml$') {
        $tag = $Matches[1]
        if ($cultureAliases.ContainsKey($tag)) { return @{ Type='culture'; Culture=$cultureAliases[$tag] } }
        # Skip non-culture suffixes like 'named_companions'
        return @{ Type='generic'; Culture=$null }
    }
    if ($name -match '^npcs_([a-z0-9_]+)\.xml$') {
        $tag = $Matches[1]
        if ($cultureAliases.ContainsKey($tag)) { return @{ Type='culture'; Culture=$cultureAliases[$tag] } }
    }
    # Everything else: generic
    return @{ Type='generic'; Culture=$null }
}

# Build per-culture used-item sets and a generic-used set
# usedByCulture[culture] = HashSet[string]
$usedByCulture = @{}
$usedByGeneric = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
# usedByCultureSourceFiles[culture] = list of files
$usedByCultureSources = @{}

foreach ($entry in $fileRefs.GetEnumerator()) {
    $cls = Get-FileClassification -Path $entry.Key
    if ($cls.Type -eq 'culture') {
        $c = $cls.Culture
        if (-not $usedByCulture.ContainsKey($c)) {
            $usedByCulture[$c] = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            $usedByCultureSources[$c] = @()
        }
        foreach ($i in $entry.Value) { [void]$usedByCulture[$c].Add($i) }
        $usedByCultureSources[$c] += $entry.Key
    } else {
        foreach ($i in $entry.Value) { [void]$usedByGeneric.Add($i) }
    }
}

Write-Progress2 "phase 3: classified into $($usedByCulture.Count) culture buckets + 1 generic"

# -----------------------------------------------------------------------------
# Phase 4 — Per-culture coverage report
# -----------------------------------------------------------------------------

# Group items by culture
$itemsByCulture = @{}
foreach ($item in $items.Values) {
    $c = $item.Culture
    if ([string]::IsNullOrEmpty($c)) { $c = '_uncultured' }
    if (-not $itemsByCulture.ContainsKey($c)) {
        $itemsByCulture[$c] = @()
    }
    $itemsByCulture[$c] += $item
}

# Build summary rows
$summary = @()
foreach ($cult in ($itemsByCulture.Keys | Sort-Object)) {
    $declared = $itemsByCulture[$cult]
    if ($usedByCulture.ContainsKey($cult)) {
        $ownUsed = $usedByCulture[$cult]
    } else {
        $ownUsed = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    }

    $propUsed = 0
    $heroOnly = 0
    $crossOnly = 0
    $unused = 0
    $unusedItems = @()
    $heroOnlyItems = @()
    $crossOnlyItems = @()

    foreach ($item in $declared) {
        $inOwn = $ownUsed.Contains($item.Id)
        $inHero = $usedByGeneric.Contains($item.Id)
        # cross-culture: used by some OTHER culture's bucket
        $inOtherCulture = $false
        foreach ($otherCult in $usedByCulture.Keys) {
            if ($otherCult -eq $cult) { continue }
            if ($usedByCulture[$otherCult].Contains($item.Id)) { $inOtherCulture = $true; break }
        }

        if ($inOwn) {
            $propUsed++
        } elseif ($inHero) {
            $heroOnly++
            $heroOnlyItems += $item
        } elseif ($inOtherCulture) {
            $crossOnly++
            $crossOnlyItems += $item
        } else {
            $unused++
            $unusedItems += $item
        }
    }

    $summary += [pscustomobject]@{
        Culture       = $cult
        Declared      = $declared.Count
        ProperlyUsed  = $propUsed
        HeroOnly      = $heroOnly
        CrossCultureOnly = $crossOnly
        Unused        = $unused
        UnusedItems   = $unusedItems
        HeroOnlyItems = $heroOnlyItems
        CrossOnlyItems = $crossOnlyItems
    }
}

# Canonical equipment-slot display order (head → body → legs → cape → shield → weapons → horse → other)
$typeOrder = @(
    'HeadArmor', 'Cape', 'BodyArmor', 'HandArmor', 'LegArmor',
    'Shield', 'OneHandedWeapon', 'TwoHandedWeapon', 'Polearm',
    'Bow', 'Crossbow', 'Thrown', 'Arrows', 'Bolts',
    'Horse', 'HorseHarness', 'Banner', 'Pistol', 'Musket', 'Bullets'
)
function Get-TypeIndex {
    param([string]$T)
    $idx = $typeOrder.IndexOf($T)
    if ($idx -lt 0) { return [int]::MaxValue }
    return $idx
}

# Discover all Type values actually present in the data so we can output a stable column set
$allTypesObserved = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($it in $items.Values) {
    $t = if ([string]::IsNullOrEmpty($it.Type)) { '(none)' } else { $it.Type }
    [void]$allTypesObserved.Add($t)
}
$typeColumns = $allTypesObserved | Sort-Object @{Expression={ Get-TypeIndex $_ }}, @{Expression={ $_ }}

# Per-culture per-Type unused/declared counts (for the matrix below)
# matrix[$culture][$type] = @{ Declared=N; Unused=M }
$matrix = @{}
foreach ($row in $summary) {
    $cellByType = @{}
    foreach ($t in $typeColumns) { $cellByType[$t] = @{ Declared = 0; Unused = 0 } }
    foreach ($it in $itemsByCulture[$row.Culture]) {
        $t = if ([string]::IsNullOrEmpty($it.Type)) { '(none)' } else { $it.Type }
        $cellByType[$t].Declared++
    }
    foreach ($it in $row.UnusedItems) {
        $t = if ([string]::IsNullOrEmpty($it.Type)) { '(none)' } else { $it.Type }
        $cellByType[$t].Unused++
    }
    $matrix[$row.Culture] = $cellByType
}

# Write REPORT.md
$reportPath = Join-Path $OutputDir 'REPORT.md'
$L = [System.Collections.Generic.List[string]]::new()
$L.Add('# Culture coverage audit')
$L.Add('')
$L.Add("Generated by ``tools/Audit-CultureCoverage.ps1`` at $(Get-Date -Format o).")
$L.Add('')
$L.Add('Joins Armory items (declared with `culture="Culture.X"`) against TAOM item-id refs grouped by source-file culture (`troops_<X>.xml`, `taom_equipment_sets_<X>.xml`, `npcs_<X>.xml`).')
$L.Add('')
$L.Add('## Per-culture summary')
$L.Add('')
$L.Add('| Culture | Declared | Properly used | Hero-only | Cross-culture only | **Unused** |')
$L.Add('|---|---:|---:|---:|---:|---:|')
foreach ($row in ($summary | Sort-Object Culture)) {
    $L.Add("| $($row.Culture) | $($row.Declared) | $($row.ProperlyUsed) | $($row.HeroOnly) | $($row.CrossCultureOnly) | **$($row.Unused)** |")
}
$L.Add('')
$L.Add('**Properly used:** referenced by `troops_<culture>.xml` or `taom_equipment_sets_<culture>.xml`.  ')
$L.Add('**Hero-only:** referenced only by generic files (lords.xml, taom_char_creation_equipment.xml, taom_wanderer_equipment.xml, etc.).  ')
$L.Add('**Cross-culture only:** referenced only by ANOTHER culture''s troop/equipment-set files (legitimate cross-pollination OR a misfiled ref).  ')
$L.Add('**Unused:** declared with this culture but nothing in TAOM references the item ID anywhere.')
$L.Add('')

# Top-level: Unused-by-Type matrix across cultures (the worksheet for roster expansion)
$L.Add('## Unused-by-slot matrix (cell = unused count per culture x slot)')
$L.Add('')
$L.Add('Use this matrix to plan which equipment slots in each culture''s troop rosters have unworn art waiting. Format: `unused / declared`. Zero rows are omitted from the column set when nothing in any culture uses them.')
$L.Add('')
# Build header row
$header = '| Culture |'
$sep    = '|---|'
$activeTypes = @()
foreach ($t in $typeColumns) {
    $anyUnused = $false
    foreach ($c in $matrix.Keys) {
        if ($matrix[$c][$t].Unused -gt 0) { $anyUnused = $true; break }
    }
    if ($anyUnused) { $activeTypes += $t }
}
foreach ($t in $activeTypes) {
    $header += " $t |"
    $sep    += '---:|'
}
$header += ' **Total** |'
$sep    += '---:|'
$L.Add($header)
$L.Add($sep)
foreach ($cult in ($matrix.Keys | Sort-Object)) {
    $rowStr = "| ``$cult`` |"
    $cultTotalUnused = 0
    foreach ($t in $activeTypes) {
        $cell = $matrix[$cult][$t]
        if ($cell.Declared -eq 0) {
            $rowStr += ' . |'
        } else {
            $rowStr += " $($cell.Unused) / $($cell.Declared) |"
            $cultTotalUnused += $cell.Unused
        }
    }
    $rowStr += " **$cultTotalUnused** |"
    $L.Add($rowStr)
}
$L.Add('')

# Per-culture unused lists
foreach ($row in ($summary | Sort-Object Culture)) {
    if ($row.Unused -eq 0 -and $row.HeroOnly -eq 0 -and $row.CrossCultureOnly -eq 0) { continue }
    $L.Add("## ``$($row.Culture)``")
    $L.Add('')
    $L.Add("Declared: $($row.Declared) | Properly used: $($row.ProperlyUsed) | Hero-only: $($row.HeroOnly) | Cross-culture: $($row.CrossCultureOnly) | **Unused: $($row.Unused)**")
    $L.Add('')

    if ($row.Unused -gt 0) {
        # Per-Type breakdown for this culture
        $L.Add('### Unused items by slot')
        $L.Add('')
        $L.Add('| Slot (Type) | Unused / Declared |')
        $L.Add('|---|---:|')
        foreach ($t in $typeColumns) {
            $cell = $matrix[$row.Culture][$t]
            if ($cell.Declared -eq 0 -and $cell.Unused -eq 0) { continue }
            if ($cell.Unused -eq 0) { continue }
            $L.Add("| $t | $($cell.Unused) / $($cell.Declared) |")
        }
        $L.Add('')

        # Grouped item list — by Type then by Id
        $L.Add('### Unused items (grouped by slot, no TAOM ref anywhere)')
        $L.Add('')
        $byType = $row.UnusedItems | Group-Object -Property { if ([string]::IsNullOrEmpty($_.Type)) { '(none)' } else { $_.Type } }
        $byTypeSorted = $byType | Sort-Object @{Expression={ Get-TypeIndex $_.Name }}, @{Expression={ $_.Name }}
        foreach ($g in $byTypeSorted) {
            $L.Add("#### $($g.Name) — $($g.Count) item(s)")
            $L.Add('')
            $L.Add('| Item ID | Mesh | Source XML |')
            $L.Add('|---|---|---|')
            foreach ($it in ($g.Group | Sort-Object Id)) {
                $rel = $it.SourceFile
                $idx = $rel.IndexOf('ModuleData')
                if ($idx -ge 0) { $rel = $rel.Substring($idx) }
                $meshDisplay = if ([string]::IsNullOrEmpty($it.Mesh)) { '*(none)*' } else { "``$($it.Mesh)``" }
                $L.Add("| ``$($it.Id)`` | $meshDisplay | ``$rel`` |")
            }
            $L.Add('')
        }
    }

    if ($row.HeroOnly -gt 0) {
        $L.Add('### Hero-only items (used only by lords / char-creation / wanderer)')
        $L.Add('')
        $L.Add('<details><summary>' + $row.HeroOnly + ' items</summary>')
        $L.Add('')
        $L.Add('| Item ID | Mesh |')
        $L.Add('|---|---|')
        foreach ($it in ($row.HeroOnlyItems | Sort-Object Id)) {
            $meshDisplay = if ([string]::IsNullOrEmpty($it.Mesh)) { '*(none)*' } else { "``$($it.Mesh)``" }
            $L.Add("| ``$($it.Id)`` | $meshDisplay |")
        }
        $L.Add('')
        $L.Add('</details>')
        $L.Add('')
    }

    if ($row.CrossCultureOnly -gt 0) {
        $L.Add('### Cross-culture items (used only by other culture''s troops)')
        $L.Add('')
        $L.Add('<details><summary>' + $row.CrossCultureOnly + ' items</summary>')
        $L.Add('')
        $L.Add('| Item ID | Mesh |')
        $L.Add('|---|---|')
        foreach ($it in ($row.CrossOnlyItems | Sort-Object Id)) {
            $meshDisplay = if ([string]::IsNullOrEmpty($it.Mesh)) { '*(none)*' } else { "``$($it.Mesh)``" }
            $L.Add("| ``$($it.Id)`` | $meshDisplay |")
        }
        $L.Add('')
        $L.Add('</details>')
        $L.Add('')
    }
}

[System.IO.File]::WriteAllLines($reportPath, $L)

# Also emit a flat TSV of unused items for tool composition
$tsv = Join-Path $OutputDir 'unused-items.tsv'
"culture`tslot_type`titem_id`tmesh`tsource_file" | Set-Content -Path $tsv -Encoding UTF8
foreach ($row in $summary) {
    foreach ($it in ($row.UnusedItems | Sort-Object @{Expression={ Get-TypeIndex $_.Type }}, @{Expression={ $_.Id }})) {
        $slot = if ([string]::IsNullOrEmpty($it.Type)) { '(none)' } else { $it.Type }
        "$($row.Culture)`t$slot`t$($it.Id)`t$($it.Mesh)`t$($it.SourceFile)" | Add-Content -Path $tsv -Encoding UTF8
    }
}

Write-Output $reportPath
