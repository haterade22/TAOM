#Requires -Version 7
<#
.SYNOPSIS
    For each prefix-matched (unused-item, troop) pair, add an <EquipmentRoster>
    variant on the troop: clone the troop's first existing roster, swap the
    relevant equipment slot to the unused item, append after the original.

.DESCRIPTION
    Consumes the data already produced by Audit-CultureCoverage.ps1 and
    Get-RosterSuggestions.ps1 (unused-items.tsv + _troops-by-culture.json).
    Restricts itself to armor-type items (HeadArmor, Cape, BodyArmor,
    HandArmor, LegArmor) where slot mapping is unambiguous; skips weapons,
    shields, horses, banners (those need authoring decisions).

    Default mode is DRY RUN — prints planned changes. Use -Apply to write.

.PARAMETER Cultures
    Culture engine-ids to process. Default: only the "easy wins" with high
    prefix-match coverage. Pass 'all' to process every culture.

.PARAMETER Apply
    Actually write files. Default off (dry run prints what would change).

.PARAMETER MaxTargetsPerItem
    Cap on how many matched troops receive a roster for one unused item.
    Default: 3. Each troop already-matched on prefix gets the item; capping
    avoids one item creating 30+ rosters across an army.
#>

param(
    [string[]]$Cultures = @('dolguldur','mordor','gundabad','isengard','rivendell'),
    [switch]$Apply,
    [int]$MaxTargetsPerItem = 3
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$tsvPath  = Join-Path $repoRoot 'tools\reports\culture-coverage\unused-items.tsv'
$jsonPath = Join-Path $repoRoot 'tools\reports\culture-coverage\suggestions\_troops-by-culture.json'
$troopsDir = Join-Path $repoRoot 'Main\_Module\ModuleData\troops'

if (-not (Test-Path $tsvPath))  { throw "Missing $tsvPath. Run Audit-CultureCoverage.ps1 first." }
if (-not (Test-Path $jsonPath)) { throw "Missing $jsonPath. Run the troop-extraction step first." }

# --- Load data ---
$unused = Import-Csv -Delimiter "`t" -Path $tsvPath
$troopsRaw = Get-Content $jsonPath -Raw | ConvertFrom-Json -AsHashtable

# Per culture: HashSet of equipped item ids per troop
$troops = @{}
foreach ($c in $troopsRaw.Keys) {
    $troops[$c] = @($troopsRaw[$c] | ForEach-Object {
        [pscustomobject]@{
            Id        = $_.Id
            Equipment = [System.Collections.Generic.HashSet[string]]::new([string[]]@($_.Equipment), [System.StringComparer]::Ordinal)
        }
    })
}

# Map: culture -> source file
$cultureToFile = @{
    'gondor'    = 'troops_gondor.xml';    'mordor'    = 'troops_mordor.xml'
    'erebor'    = 'troops_erebor.xml';    'rivendell' = 'troops_rivendell.xml'
    'mirkwood'  = 'troops_mirkwood.xml';  'isengard'  = 'troops_isengard.xml'
    'gundabad'  = 'troops_gundabad.xml';  'dolguldur' = 'troops_dolguldur.xml'
    'umbar'     = 'troops_umbar.xml';     'vlandia'   = 'troops_rohan.xml'
    'empire'    = 'troops_dunland.xml';   'aserai'    = 'troops_harad.xml'
    'khuzait'   = 'troops_rhun_new.xml'
}

# Slot mapping: armor types to TAOM equipment slots
$slotByType = @{
    'HeadArmor' = 'Head'
    'BodyArmor' = 'Body'
    'HandArmor' = 'Gloves'
    'LegArmor'  = 'Leg'
    'Cape'      = 'Cape'
}

# --- Prefix matching (same algorithm as Get-RosterSuggestions.ps1) ---

function Get-PrefixTokens {
    param([string]$ItemId, [int]$MinTokens = 2)
    $parts = $ItemId -split '_'
    if ($parts.Count -lt $MinTokens) { return @() }
    $start = if ($parts[0] -eq 'sk') { 1 } else { 0 }
    $tokens = $parts[$start..($parts.Count - 1)]
    $out = @()
    for ($n = $tokens.Count - 1; $n -ge $MinTokens; $n--) {
        $out += , ($tokens[0..($n - 1)] -join '_')
    }
    return $out
}

function Find-MatchingTroops {
    param([string]$ItemId, [array]$Pool)
    $prefixes = Get-PrefixTokens -ItemId $ItemId -MinTokens 2
    foreach ($p in $prefixes) {
        $hits = @()
        foreach ($t in $Pool) {
            foreach ($eq in $t.Equipment) {
                $eqCore = if ($eq.StartsWith('sk_')) { $eq.Substring(3) } else { $eq }
                if ($eqCore.StartsWith($p + '_') -or $eqCore -eq $p) {
                    $hits += $t
                    break
                }
            }
        }
        if ($hits.Count -gt 0) { return $hits }
    }
    return @()
}

# --- Roster-clone helpers (regex-based to preserve formatting) ---

function Add-RostersToTroop {
    <#
    Inputs:
      $TroopBlock — the full <NPCCharacter ...>...</NPCCharacter> text
      $Additions — list of @{Slot=...; ItemId=...} — each becomes one new roster
    Returns:
      modified troop block text
    #>
    param([string]$TroopBlock, [array]$Additions)

    # Find first <EquipmentRoster>...</EquipmentRoster> block (allowing whitespace and self-closing edge case)
    $erRx = [regex]'(?s)<EquipmentRoster\b[^>]*>.*?</EquipmentRoster>'
    $erMatch = $erRx.Match($TroopBlock)
    if (-not $erMatch.Success) {
        # No existing roster — can't clone. Skip.
        return $TroopBlock
    }
    $baseRoster = $erMatch.Value

    $newRosters = [System.Collections.Generic.List[string]]::new()
    foreach ($add in $Additions) {
        $slot = $add.Slot
        $itemRef = "Item.$($add.ItemId)"

        # Try to find a line with this slot already (case-sensitive on slot value)
        $slotRx = [regex]"(<equipment\s+slot=`"$slot`"\s+id=`")([^`"]+)(`"\s*/>)"
        if ($slotRx.IsMatch($baseRoster)) {
            $cloned = $slotRx.Replace($baseRoster, "`${1}$itemRef`${3}", 1)
        } else {
            # No existing slot for this type — insert a new <equipment .../> line before </EquipmentRoster>
            # Detect indentation from existing equipment line
            $indentRx = [regex]'(?m)^(\s+)<equipment\s+slot='
            $indentMatch = $indentRx.Match($baseRoster)
            $indent = if ($indentMatch.Success) { $indentMatch.Groups[1].Value } else { '        ' }
            $newLine = "$indent<equipment slot=`"$slot`" id=`"$itemRef`" />"
            $cloned = $baseRoster -replace '(\s*)</EquipmentRoster>', "`n$newLine`$1</EquipmentRoster>"
        }
        $newRosters.Add($cloned)
    }

    # Detect indentation OF the EquipmentRoster opening tag (for clean prefix on appended rosters)
    $erIndent = ''
    $beforeEr = $TroopBlock.Substring(0, $erMatch.Index)
    $lastNl = $beforeEr.LastIndexOf("`n")
    if ($lastNl -ge 0) { $erIndent = $beforeEr.Substring($lastNl + 1) -replace '\S.*$', '' }

    # Insert new rosters right after the original roster's close tag, each on its own line, prefixed by $erIndent
    $insertion = ''
    foreach ($r in $newRosters) {
        $insertion += "`n$erIndent$r"
    }
    $newBlock = $TroopBlock.Substring(0, $erMatch.Index + $erMatch.Length) +
                $insertion +
                $TroopBlock.Substring($erMatch.Index + $erMatch.Length)
    return $newBlock
}

# --- Main loop: per culture, build per-troop additions, edit file ---

$grand = @()

foreach ($cult in $Cultures) {
    if (-not $cultureToFile.ContainsKey($cult)) {
        Write-Host "[skip] no troops file mapped for culture '$cult'"
        continue
    }
    $fname = $cultureToFile[$cult]
    $fpath = Join-Path $troopsDir $fname
    if (-not (Test-Path $fpath)) {
        Write-Host "[skip] file not found: $fpath"
        continue
    }
    $pool = if ($troops.ContainsKey($cult)) { $troops[$cult] } else { @() }
    $items = $unused | Where-Object { $_.culture -eq $cult -and $slotByType.ContainsKey($_.slot_type) }
    Write-Host ""
    Write-Host "=== $cult ($($items.Count) candidate armor items in pool of $($pool.Count) troops) ==="

    # Build per-troop-id list of additions
    # additions[troopId] = list of @{Slot; ItemId; SourceItem}
    $additions = @{}
    $matchedCount = 0
    $unmatchedCount = 0
    foreach ($it in $items) {
        $found = Find-MatchingTroops -ItemId $it.item_id -Pool $pool
        if ($found.Count -eq 0) { $unmatchedCount++; continue }
        $matchedCount++
        $picks = $found | Select-Object -First $MaxTargetsPerItem
        foreach ($t in $picks) {
            if (-not $additions.ContainsKey($t.Id)) { $additions[$t.Id] = @() }
            $additions[$t.Id] += @{ Slot = $slotByType[$it.slot_type]; ItemId = $it.item_id; SourceItem = $it }
        }
    }
    Write-Host "  prefix-matched: $matchedCount armor items; unmatched skipped: $unmatchedCount"
    $totalRostersToAdd = ($additions.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
    Write-Host "  planned roster additions: $totalRostersToAdd across $($additions.Keys.Count) troops"

    if (-not $Apply) {
        Write-Host "  [DRY RUN] not modifying file"
        $grand += [pscustomobject]@{ Culture=$cult; Matched=$matchedCount; Unmatched=$unmatchedCount; PlannedRosters=$totalRostersToAdd; TroopsAffected=$additions.Keys.Count }
        continue
    }

    # Apply: load file (detect BOM), locate each NPCCharacter block, splice additions.
    # Always write CRLF per project .editorconfig (charset=utf-8, end_of_line=crlf).
    $bytes = [System.IO.File]::ReadAllBytes($fpath)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($hasBom) { $text = $text.Substring(1) }   # strip BOM char before regex

    $npcRx = [regex]'(?s)<NPCCharacter\s[^>]*?\bid="([^"]+)"[^>]*?>.*?</NPCCharacter>'
    $newText = $npcRx.Replace($text, {
        param($m)
        $id = $m.Groups[1].Value
        if (-not $additions.ContainsKey($id)) { return $m.Value }
        return Add-RostersToTroop -TroopBlock $m.Value -Additions $additions[$id]
    })

    if ($newText -eq $text) {
        Write-Host "  WARNING: no changes detected after edit pass"
    } else {
        # Always normalize to CRLF (project policy)
        $newText = ($newText -replace "`r`n", "`n") -replace "`n", "`r`n"
        # Preserve BOM if original had one (per file convention)
        $enc = [System.Text.UTF8Encoding]::new($hasBom)
        [System.IO.File]::WriteAllText($fpath, $newText, $enc)
        Write-Host "  WROTE: $fpath"
    }
    $grand += [pscustomobject]@{ Culture=$cult; Matched=$matchedCount; Unmatched=$unmatchedCount; PlannedRosters=$totalRostersToAdd; TroopsAffected=$additions.Keys.Count }
}

Write-Host ""
Write-Host "=== Summary ==="
$grand | Format-Table -AutoSize | Out-String | Write-Host
