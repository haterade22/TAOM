param(
    [string]$SceneFile = 'E:\LOTRAOMAssets\scene.xscene',
    [string]$OutputFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs\scene-entities.md')
)

Write-Host "Reading scene file: $SceneFile"
$content = [System.IO.File]::ReadAllText($SceneFile)

# Extract settlement entities using the two-step approach
$prefixes = @('town_', 'castle_village_', 'castle_', 'village_')
$entities = @{ 'town_' = @(); 'castle_' = @(); 'village_' = @(); 'castle_village_' = @() }

# Two-step approach: find <game_entity name="X">, then verify <transform position=
# within 500 chars before any nested <game_entity. This filters out parent containers
# and sub-entities (e.g. town_A2_main, town_EW2_l3, town_gate, castle_gate).
$gameEntityPattern = '<game_entity\s+name="([^"]+)"'
$allMatches = [regex]::Matches($content, $gameEntityPattern)

foreach ($m in $allMatches) {
    $name = $m.Groups[1].Value
    # Check if this is a settlement-prefixed entity
    $matchedPrefix = $null
    foreach ($prefix in @('castle_village_', 'town_', 'castle_', 'village_')) {
        if ($name.StartsWith($prefix)) {
            $matchedPrefix = $prefix
            break
        }
    }
    if (-not $matchedPrefix) { continue }

    # Step 2: Check for <transform position= within 500 chars, before any nested <game_entity
    $startPos = $m.Index + $m.Length
    $windowEnd = [Math]::Min($startPos + 500, $content.Length)
    $window = $content.Substring($startPos, $windowEnd - $startPos)

    $nestedIdx = $window.IndexOf('<game_entity')
    $transformIdx = $window.IndexOf('<transform position=')

    if ($transformIdx -ge 0 -and ($nestedIdx -lt 0 -or $transformIdx -lt $nestedIdx)) {
        # Filter: only keep entities matching settlement naming convention
        # Valid: town_A1, castle_EW10, village_RU3_4, castle_village_FH1_2,
        #        town_isengard, castle_orthanc_gate, village_isengard_a, castle_village_isengard_a
        # Invalid: town_A2_main, town_EW2_l3, town_gate, castle_gate, town_circle_decal
        $rest = $name.Substring($matchedPrefix.Length)
        $isValid = $false
        if ($rest -cmatch '^[A-Z]+\d+$') { $isValid = $true }                          # town_A1, castle_EW10
        if ($rest -cmatch '^[A-Z]+\d+_\d+[a-z]?$') { $isValid = $true }                # village_RU3_4, village_U2_3a
        # Explicit special-named settlements only
        $specialNames = @('town_isengard','castle_orthanc_gate','village_isengard_a','castle_village_isengard_a')
        if ($specialNames -contains $name) { $isValid = $true }
        if (-not $isValid) { continue }

        $entities[$matchedPrefix] += $name
    }
}

# Deduplicate and sort
foreach ($key in @($entities.Keys)) {
    $entities[$key] = $entities[$key] | Sort-Object -Unique
}

Write-Host "  Towns: $($entities['town_'].Count)"
Write-Host "  Castles: $($entities['castle_'].Count)"
Write-Host "  Villages: $($entities['village_'].Count)"
Write-Host "  Castle Villages: $($entities['castle_village_'].Count)"

# Extract region from entity name
function Get-Region($name, $prefix) {
    $rest = $name.Substring($prefix.Length)
    if ($rest -cmatch '^([A-Z]+)') { return $matches[1] }
    if ($rest -match '^(isengard|orthanc)') { return 'I' }
    return '?'
}

# Region display order
$regionOrder = @('A','FH','B','DG','E','EN','ES','EW','G','I','K','L','M','R','RU','S','U','V')

function Write-Section($sb, $prefix, $label, $entityList) {
    $grouped = @{}
    foreach ($e in $entityList) {
        $r = Get-Region $e $prefix
        if (-not $grouped.ContainsKey($r)) { $grouped[$r] = @() }
        $grouped[$r] += $e
    }

    [void]$sb.AppendLine("## ${prefix} ($($entityList.Count) $label)")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('| Region | Entities |')
    [void]$sb.AppendLine('|--------|----------|')

    foreach ($r in $regionOrder) {
        if (-not $grouped.ContainsKey($r)) { continue }
        $items = $grouped[$r] | Sort-Object
        $count = $items.Count
        $list = $items -join ', '
        [void]$sb.AppendLine("| $r ($count) | $list |")
    }
    # Any regions not in the order
    foreach ($r in ($grouped.Keys | Sort-Object)) {
        if ($regionOrder -contains $r) { continue }
        $items = $grouped[$r] | Sort-Object
        $count = $items.Count
        $list = $items -join ', '
        [void]$sb.AppendLine("| $r ($count) | $list |")
    }
    [void]$sb.AppendLine('')
}

# Build anomalies
function Get-Anomalies($allEntities) {
    $anomalies = @()
    foreach ($prefix in @('town_', 'castle_', 'village_', 'castle_village_')) {
        foreach ($e in $allEntities[$prefix]) {
            $rest = $e.Substring($prefix.Length)
            # Check for unusual suffixes (letters after numbers)
            if ($rest -cmatch '\d+[a-zA-Z]+$' -and $rest -cnotmatch '^[A-Z]') {
                $suffix = ($rest -replace '.*(\d+[a-zA-Z]+)$', '$1')
                $anomalies += [PSCustomObject]@{
                    Issue = 'Unusual suffix'
                    Entity = $e
                    Notes = "Ends with letter after number - possible typo?"
                }
            }
            # Special named entities (isengard, orthanc, etc.)
            if ($rest -match '^(isengard|orthanc)') {
                # Not anomalous, just special
            }
        }
    }
    return $anomalies
}

# Generate document
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('# Scene Entity Reference')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Extracted from `E:\LOTRAOMAssets\scene.xscene`')
[void]$sb.AppendLine('')

Write-Section $sb 'town_' 'settlements' $entities['town_']
Write-Section $sb 'castle_' 'castles' $entities['castle_']
Write-Section $sb 'village_' 'villages' $entities['village_']
Write-Section $sb 'castle_village_' 'castle villages' $entities['castle_village_']

# Anomalies
$anomalies = Get-Anomalies $entities
[void]$sb.AppendLine('## Anomalies')
[void]$sb.AppendLine('')
if ($anomalies.Count -gt 0) {
    [void]$sb.AppendLine('| Issue | Entity | Notes |')
    [void]$sb.AppendLine('|-------|--------|-------|')
    foreach ($a in $anomalies) {
        [void]$sb.AppendLine("| $($a.Issue) | $($a.Entity) | $($a.Notes) |")
    }
} else {
    [void]$sb.AppendLine('No anomalies detected.')
}
[void]$sb.AppendLine('')

[System.IO.File]::WriteAllText($OutputFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($true))
Write-Host "`nOutput written to: $OutputFile"

$total = $entities['town_'].Count + $entities['castle_'].Count + $entities['village_'].Count + $entities['castle_village_'].Count
Write-Host "Total scene entities: $total"
