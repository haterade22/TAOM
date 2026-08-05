#Requires -Version 7
<#
.SYNOPSIS
    For each unused Armory item per culture, suggest one or more existing troops
    that already wear similarly-prefixed gear — i.e., the obvious roster home.

.DESCRIPTION
    Reads:
      - tools/reports/culture-coverage/unused-items.tsv (produced by Audit-CultureCoverage.ps1)
      - tools/reports/culture-coverage/suggestions/_troops-by-culture.json (produced inline by setup step)
      - LOTRLOME_Armory items XML (to know item Type/subtype for slot-aware matching)

    For each (culture, slot, unused-item) it:
      1. Backs off through prefix tokens (e.g. sk_gd_ano_inf_helmet_med_c -> sk_gd_ano_inf_helmet_med -> sk_gd_ano_inf_helmet -> ...)
      2. Finds troops in that culture whose existing equipment shares the LONGEST matching prefix
      3. Emits 1-3 candidate troops + a rationale tag

    Also generates a "no good match" section per culture for items that share NO prefix with any troop —
    those need either a new troop, a lord/wanderer roster, or a new char-creation gear bundle.

.PARAMETER OutputDir
    Where to write per-culture <culture>.md files. Default: tools/reports/culture-coverage/suggestions/
#>

param(
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'tools\reports\culture-coverage\suggestions' }
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
$OutputDir = (Resolve-Path $OutputDir).Path

$tsvPath = Join-Path $repoRoot 'tools\reports\culture-coverage\unused-items.tsv'
$jsonPath = Join-Path $OutputDir '_troops-by-culture.json'
if (-not (Test-Path $tsvPath))  { throw "Missing $tsvPath. Run Audit-CultureCoverage.ps1 first." }
if (-not (Test-Path $jsonPath)) { throw "Missing $jsonPath. Run the troop-extraction step first." }

# --- Load data ---

# Unused items: each row = culture, slot_type, item_id, mesh, source_file
$unused = Import-Csv -Delimiter "`t" -Path $tsvPath

# Troops by culture from the JSON dump. Each value: list of {Id, Level, Rosters, Equipment[]}
$troopsRaw = Get-Content $jsonPath -Raw | ConvertFrom-Json -AsHashtable
$troops = @{}
foreach ($c in $troopsRaw.Keys) {
    $troops[$c] = @($troopsRaw[$c] | ForEach-Object {
        [pscustomobject]@{
            Id        = $_.Id
            Level     = [int]$_.Level
            Rosters   = [int]$_.Rosters
            Equipment = [System.Collections.Generic.HashSet[string]]::new([string[]]@($_.Equipment), [System.StringComparer]::Ordinal)
        }
    })
}

# --- Matching ---

function Get-PrefixTokens {
    param([string]$ItemId, [int]$MinTokens = 2)
    $parts = $ItemId -split '_'
    if ($parts.Count -lt $MinTokens) { return @() }
    # Strip leading 'sk' if present (skin-bound item convention)
    $start = if ($parts[0] -eq 'sk') { 1 } else { 0 }
    $tokens = $parts[$start..($parts.Count - 1)]
    # Produce successively shorter prefixes: longest first
    $out = @()
    for ($n = $tokens.Count - 1; $n -ge $MinTokens; $n--) {
        $out += , ($tokens[0..($n - 1)] -join '_')
    }
    return $out
}

function Find-MatchingTroops {
    param(
        [string]$ItemId,
        [array]$Pool
    )
    $prefixes = Get-PrefixTokens -ItemId $ItemId -MinTokens 2
    foreach ($p in $prefixes) {
        $matches = @()
        foreach ($t in $Pool) {
            foreach ($eq in $t.Equipment) {
                # strip 'sk_' from troop's equipment too for comparable matching
                $eqCore = if ($eq.StartsWith('sk_')) { $eq.Substring(3) } else { $eq }
                if ($eqCore.StartsWith($p + '_') -or $eqCore -eq $p) {
                    $matches += $t
                    break
                }
            }
        }
        if ($matches.Count -gt 0) {
            return [pscustomobject]@{ Prefix = $p; Troops = $matches }
        }
    }
    return $null
}

# --- Slot ordering for report ---
$typeOrder = @(
    'HeadArmor', 'Cape', 'BodyArmor', 'HandArmor', 'LegArmor',
    'Shield', 'OneHandedWeapon', 'TwoHandedWeapon', 'Polearm',
    'Bow', 'Crossbow', 'Thrown', 'Arrows', 'Bolts',
    'Horse', 'HorseHarness', 'Banner', 'Pistol', 'Musket', 'Bullets'
)
function Get-TypeIndex { param([string]$T); $i = $typeOrder.IndexOf($T); if ($i -lt 0) { [int]::MaxValue } else { $i } }

# Friendly culture display name
$cultureDisplay = @{
    'gondor'='Gondor'; 'mordor'='Mordor'; 'erebor'='Erebor';
    'rivendell'='Rivendell'; 'lothlorien'='Lothlorien'; 'mirkwood'='Mirkwood';
    'isengard'='Isengard'; 'gundabad'='Gundabad'; 'dolguldur'='Dol Guldur';
    'umbar'='Umbar'; 'vlandia'='Rohan (vlandia)'; 'empire'='Dunland (empire)';
    'aserai'='Harad (aserai)'; 'khuzait'='Easterlings/Rhun (khuzait)';
    'sturgia'='Dale (sturgia)'; 'battania'='Khand (battania)'
}

# --- Per-culture: write suggestions ---
$grandTotals = @()

foreach ($cult in ($unused | Group-Object culture | Sort-Object Name | ForEach-Object Name)) {
    $items = $unused | Where-Object culture -eq $cult
    if (-not $items) { continue }
    $pool = if ($troops.ContainsKey($cult)) { $troops[$cult] } else { @() }

    $L = [System.Collections.Generic.List[string]]::new()
    $display = if ($cultureDisplay.ContainsKey($cult)) { $cultureDisplay[$cult] } else { $cult }
    $L.Add("# $display — roster suggestions for $($items.Count) unused items")
    $L.Add('')
    $L.Add("Troops in pool: $($pool.Count). Generated $(Get-Date -Format o).")
    $L.Add('')
    $L.Add('Each unused item is matched to the troop(s) whose existing equipment shares the LONGEST id-prefix with it. ''Match prefix'' shows the shared substring; the suggested action is to add the unused item as an additional `<EquipmentRoster>` variant on the matched troop (troops can have many rosters; spawned units randomize across them).')
    $L.Add('')
    $L.Add('When no match is found, the item shares no prefix with any troop''s gear — it needs a new troop, a lord roster, a wanderer template, or a char-creation gear bundle.')
    $L.Add('')

    # Group items by slot in display order
    $bySlot = $items | Group-Object slot_type | Sort-Object @{Expression={ Get-TypeIndex $_.Name }}

    $totalMatched = 0; $totalUnmatched = 0

    foreach ($g in $bySlot) {
        $slot = $g.Name
        $L.Add("## $slot — $($g.Count) unused")
        $L.Add('')

        $matched = @()
        $unmatched = @()
        foreach ($it in $g.Group) {
            $res = Find-MatchingTroops -ItemId $it.item_id -Pool $pool
            if ($null -eq $res) { $unmatched += $it } else { $matched += [pscustomobject]@{ Item=$it; Match=$res } }
        }
        $totalMatched += $matched.Count
        $totalUnmatched += $unmatched.Count

        if ($matched.Count -gt 0) {
            $L.Add('### Has prefix-matched troop(s) — add unused item as roster variant')
            $L.Add('')
            $L.Add('| Unused Item | Mesh | Match Prefix | Suggested Troop(s) |')
            $L.Add('|---|---|---|---|')
            foreach ($row in ($matched | Sort-Object { $_.Item.item_id })) {
                $troopList = ($row.Match.Troops | Select-Object -First 3 | ForEach-Object { "``$($_.Id)``" }) -join ', '
                if ($row.Match.Troops.Count -gt 3) { $troopList += " (+$($row.Match.Troops.Count - 3) more)" }
                $L.Add("| ``$($row.Item.item_id)`` | ``$($row.Item.mesh)`` | ``$($row.Match.Prefix)`` | $troopList |")
            }
            $L.Add('')
        }

        if ($unmatched.Count -gt 0) {
            $L.Add('### No prefix match — needs new troop / lord / wanderer / CC bundle')
            $L.Add('')
            $L.Add('| Unused Item | Mesh |')
            $L.Add('|---|---|')
            foreach ($it in ($unmatched | Sort-Object item_id)) {
                $L.Add("| ``$($it.item_id)`` | ``$($it.mesh)`` |")
            }
            $L.Add('')
        }
    }

    $L.Insert(2, "**Coverage:** prefix-matched $totalMatched of $($items.Count) ($([math]::Round(100.0 * $totalMatched / $items.Count, 1))%) — remaining $totalUnmatched need new homes.")
    $L.Insert(3, '')

    $outPath = Join-Path $OutputDir "$cult.md"
    [System.IO.File]::WriteAllLines($outPath, $L)
    $grandTotals += [pscustomobject]@{ Culture=$cult; Display=$display; Unused=$items.Count; Matched=$totalMatched; Unmatched=$totalUnmatched }
}

# Index file
$idx = [System.Collections.Generic.List[string]]::new()
$idx.Add('# Roster suggestions — index')
$idx.Add('')
$idx.Add('| Culture | Unused | Prefix-matched | Needs new home |')
$idx.Add('|---|---:|---:|---:|')
foreach ($r in ($grandTotals | Sort-Object Unused -Descending)) {
    $idx.Add("| [$($r.Display)]($($r.Culture).md) | $($r.Unused) | $($r.Matched) | $($r.Unmatched) |")
}
$idx.Add('')
[System.IO.File]::WriteAllLines((Join-Path $OutputDir 'INDEX.md'), $idx)

Write-Output (Join-Path $OutputDir 'INDEX.md')
