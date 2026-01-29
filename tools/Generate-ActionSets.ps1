<#
.SYNOPSIS
    Generates a Bannerlord 1.3-compatible action_sets.xml for the TAOM module.

.DESCRIPTION
    Merges the LOTRLOME_Armory_1_3 mod's action_sets with Native 1.3's action types:
    - Updates the dwarf foundational set (as_dwarf_warrior) with all 1.3 action types
    - Preserves custom dwarf animations where they exist in the old mod
    - Uses human animations as placeholders for new action types
    - Removes obsolete action types from all action_sets
    - Deduplicates the hare action_set
#>
param(
    [string]$NativePath = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\action_sets.xml",
    [string]$OldModPath = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory_1_3\ModuleData\action_sets.xml",
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\Main\_Module\ModuleData\races\action_sets.xml")
)

$ErrorActionPreference = 'Stop'

# --- Obsolete action types to remove ---
$obsoleteExactTypes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
@(
    'act_lancer_ride_0',
    'act_lancer_ride_1',
    'act_polearm_brace_ready',
    'act_camel_fall_jump_roll',
    'act_camel_fall_jump_roll_continue',
    'act_camel_fall_slow_left',
    'act_camel_fall_slow_left_continue',
    'act_camel_fall_slow_right',
    'act_camel_fall_slow_right_continue'
) | ForEach-Object { $obsoleteExactTypes.Add($_) | Out-Null }

$obsoletePatterns = @(
    '^act_character_creation_.*_default_\d+$',
    '^act_drunk_trio_.*_2$',
    '_thill_trollh',
    '_tnazghulh'
)

function Test-ObsoleteAction {
    param([string]$ActionType)
    if ($obsoleteExactTypes.Contains($ActionType)) { return $true }
    foreach ($pattern in $obsoletePatterns) {
        if ($ActionType -match $pattern) { return $true }
    }
    return $false
}

# --- Load source files ---
Write-Host "Loading Native 1.3 action_sets.xml from: $NativePath"
[xml]$nativeXml = Get-Content $NativePath -Raw -Encoding UTF8
Write-Host "  Loaded."

Write-Host "Loading old mod action_sets.xml from: $OldModPath"
[xml]$oldModXml = Get-Content $OldModPath -Raw -Encoding UTF8
Write-Host "  Loaded."

# --- Extract Native 1.3 as_human_warrior actions ---
$nativeWarrior = $nativeXml.SelectSingleNode("//action_set[@id='as_human_warrior']")
if (-not $nativeWarrior) {
    throw "Could not find as_human_warrior in Native 1.3 action_sets.xml"
}
$nativeActionNodes = $nativeWarrior.SelectNodes("action")
Write-Host "Native as_human_warrior: $($nativeActionNodes.Count) actions"

# --- Extract old mod as_dwarf_warrior actions into lookup ---
$oldDwarfWarrior = $oldModXml.SelectSingleNode("//action_set[@id='as_dwarf_warrior']")
if (-not $oldDwarfWarrior) {
    throw "Could not find as_dwarf_warrior in old mod action_sets.xml"
}
$oldDwarfActionNodes = $oldDwarfWarrior.SelectNodes("action")
Write-Host "Old mod as_dwarf_warrior: $($oldDwarfActionNodes.Count) actions"

# Build lookup: action type -> animation from old dwarf warrior
$oldDwarfLookup = @{}
foreach ($node in $oldDwarfActionNodes) {
    $type = $node.GetAttribute("type")
    $anim = $node.GetAttribute("animation")
    if ($type -and -not $oldDwarfLookup.ContainsKey($type)) {
        $oldDwarfLookup[$type] = $anim
    }
}

# --- Build output using StringBuilder for performance and formatting control ---
$sb = [System.Text.StringBuilder]::new(4 * 1024 * 1024)  # Pre-allocate 4MB
$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>') | Out-Null
$sb.AppendLine('<action_sets>') | Out-Null

# --- Build merged as_dwarf_warrior ---
Write-Host ""
Write-Host "Building merged as_dwarf_warrior..."
$sb.AppendLine("`t<action_set") | Out-Null
$sb.AppendLine("`t`tid=`"as_dwarf_warrior`"") | Out-Null
$sb.AppendLine("`t`tskeleton=`"dwarf_skeleton_a`"") | Out-Null
$sb.AppendLine("`t`tmovement_system=`"bipedal`">") | Out-Null

$newCount = 0
$preservedCount = 0
$skippedObsolete = 0

foreach ($nativeAction in $nativeActionNodes) {
    $actionType = $nativeAction.GetAttribute("type")
    $altGroup = $nativeAction.GetAttribute("alternative_group")

    # Skip obsolete types
    if (Test-ObsoleteAction $actionType) {
        $skippedObsolete++
        continue
    }

    # Determine animation: preserve old dwarf animation or use human placeholder
    if ($oldDwarfLookup.ContainsKey($actionType)) {
        $animation = $oldDwarfLookup[$actionType]
        $preservedCount++
    } else {
        $animation = $nativeAction.GetAttribute("animation")
        $newCount++
    }

    # Write action element
    if ($altGroup) {
        $sb.AppendLine("`t`t<action") | Out-Null
        $sb.AppendLine("`t`t`ttype=`"$actionType`"") | Out-Null
        $sb.AppendLine("`t`t`tanimation=`"$animation`"") | Out-Null
        $sb.AppendLine("`t`t`talternative_group=`"$altGroup`" />") | Out-Null
    } else {
        $sb.AppendLine("`t`t<action") | Out-Null
        $sb.AppendLine("`t`t`ttype=`"$actionType`"") | Out-Null
        $sb.AppendLine("`t`t`tanimation=`"$animation`" />") | Out-Null
    }
}

$sb.AppendLine("`t</action_set>") | Out-Null
Write-Host "  Preserved: $preservedCount, New (human placeholder): $newCount, Obsolete skipped: $skippedObsolete"

# --- Copy all other action_sets from old mod ---
Write-Host ""
Write-Host "Copying remaining action_sets from old mod..."
$allOldSets = $oldModXml.SelectNodes("//action_set")
$hareAdded = $false
$copiedCount = 0
$removedObsoleteCount = 0
$emptySetCount = 0

foreach ($actionSet in $allOldSets) {
    $setId = $actionSet.GetAttribute("id")

    # Skip the original dwarf warrior (we replaced it)
    if ($setId -eq 'as_dwarf_warrior') { continue }

    # Deduplicate hare
    if ($setId -eq 'hare') {
        if ($hareAdded) { continue }
        $hareAdded = $true
    }

    # Get attributes
    $skeleton = $actionSet.GetAttribute("skeleton")
    $movementSystem = $actionSet.GetAttribute("movement_system")
    $baseSet = $actionSet.GetAttribute("base_set")
    $actionNodes = $actionSet.SelectNodes("action")

    # Filter out obsolete actions
    $validActions = @()
    foreach ($action in $actionNodes) {
        $aType = $action.GetAttribute("type")
        if (Test-ObsoleteAction $aType) {
            $removedObsoleteCount++
            continue
        }
        $validActions += $action
    }

    # Write action_set element
    if ($validActions.Count -eq 0 -and $actionNodes.Count -eq 0) {
        # Self-closing action_set (no child actions originally)
        $attrStr = "id=`"$setId`""
        if ($skeleton) { $attrStr += " skeleton=`"$skeleton`"" }
        if ($movementSystem) { $attrStr += " movement_system=`"$movementSystem`"" }
        if ($baseSet) { $attrStr += " base_set=`"$baseSet`"" }
        $sb.AppendLine("`t<action_set $attrStr />") | Out-Null
        $emptySetCount++
    } elseif ($validActions.Count -eq 0 -and $actionNodes.Count -gt 0) {
        # All actions were obsolete, make self-closing with base_set
        $attrStr = "id=`"$setId`""
        if ($skeleton) { $attrStr += " skeleton=`"$skeleton`"" }
        if ($movementSystem) { $attrStr += " movement_system=`"$movementSystem`"" }
        if ($baseSet) { $attrStr += " base_set=`"$baseSet`"" }
        $sb.AppendLine("`t<action_set $attrStr />") | Out-Null
        $emptySetCount++
    } else {
        # Action_set with child actions
        $sb.AppendLine("`t<action_set") | Out-Null
        $attrLines = @("`t`tid=`"$setId`"")
        if ($skeleton) { $attrLines += "`t`tskeleton=`"$skeleton`"" }
        if ($movementSystem) { $attrLines += "`t`tmovement_system=`"$movementSystem`"" }
        if ($baseSet) { $attrLines += "`t`tbase_set=`"$baseSet`"" }
        # Last attribute line gets the closing >
        for ($i = 0; $i -lt $attrLines.Count - 1; $i++) {
            $sb.AppendLine($attrLines[$i]) | Out-Null
        }
        $sb.AppendLine("$($attrLines[-1])>") | Out-Null

        foreach ($action in $validActions) {
            $aType = $action.GetAttribute("type")
            $aAnim = $action.GetAttribute("animation")
            $aAltGroup = $action.GetAttribute("alternative_group")

            if ($aAltGroup) {
                $sb.AppendLine("`t`t<action") | Out-Null
                $sb.AppendLine("`t`t`ttype=`"$aType`"") | Out-Null
                $sb.AppendLine("`t`t`tanimation=`"$aAnim`"") | Out-Null
                $sb.AppendLine("`t`t`talternative_group=`"$aAltGroup`" />") | Out-Null
            } else {
                $sb.AppendLine("`t`t<action") | Out-Null
                $sb.AppendLine("`t`t`ttype=`"$aType`"") | Out-Null
                $sb.AppendLine("`t`t`tanimation=`"$aAnim`" />") | Out-Null
            }
        }
        $sb.AppendLine("`t</action_set>") | Out-Null
    }
    $copiedCount++
}

$sb.AppendLine('</action_sets>') | Out-Null

Write-Host "  Copied: $copiedCount action_sets ($emptySetCount self-closing)"
Write-Host "  Removed $removedObsoleteCount obsolete action entries from derivative sets"

# --- Write output ---
Write-Host ""
Write-Host "Writing output to: $OutputPath"
$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}
[System.IO.File]::WriteAllText($OutputPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Host "  Done. File size: $([math]::Round((Get-Item $OutputPath).Length / 1KB)) KB"

# =============================================================================
# VALIDATION
# =============================================================================
Write-Host ""
Write-Host "=== VALIDATION ==="
Write-Host ""

Write-Host "Parsing generated file..."
[xml]$validate = Get-Content $OutputPath -Raw -Encoding UTF8
$allSets = $validate.SelectNodes("//action_set")
$totalSets = $allSets.Count
Write-Host "Total action_sets: $totalSets"

# Check dwarf warrior
$dwarfCheck = $validate.SelectSingleNode("//action_set[@id='as_dwarf_warrior']")
$dwarfActions = $dwarfCheck.SelectNodes("action")
Write-Host ""
Write-Host "as_dwarf_warrior:"
Write-Host "  skeleton: $($dwarfCheck.GetAttribute('skeleton'))"
Write-Host "  movement_system: $($dwarfCheck.GetAttribute('movement_system'))"
Write-Host "  action count: $($dwarfActions.Count)"

# Spot check new 1.3 types
Write-Host ""
Write-Host "Spot-checking new 1.3 action types in as_dwarf_warrior:"
$spotCheckTypes = @(
    'act_crouch_walk_forward_drag',
    'act_blocked_backstab_1h',
    'act_polearm_brace_ready_up',
    'act_polearm_brace_ready_down',
    'act_camel_lancer_ride_4'
)
# Note: act_row_loop_left is NOT in as_human_warrior (it's in specialized sets), so not checked here
foreach ($checkType in $spotCheckTypes) {
    $found = $dwarfCheck.SelectSingleNode("action[@type='$checkType']")
    if ($found) {
        Write-Host "  [OK] $checkType -> $($found.GetAttribute('animation'))"
    } else {
        Write-Host "  [MISSING] $checkType"
    }
}

# Check no obsolete types remain
Write-Host ""
Write-Host "Checking for obsolete action types..."
$obsoleteFound = 0
foreach ($set in $allSets) {
    foreach ($action in $set.SelectNodes("action")) {
        $aType = $action.GetAttribute("type")
        if (Test-ObsoleteAction $aType) {
            Write-Host "  [WARN] Obsolete: $aType in $($set.GetAttribute('id'))"
            $obsoleteFound++
        }
    }
}
if ($obsoleteFound -eq 0) {
    Write-Host "  [OK] No obsolete action types found"
} else {
    Write-Host "  [FAIL] Found $obsoleteFound obsolete action types!"
}

# Check hare - it's provided by Native, should NOT be in TAOM output
$hareNodes = $validate.SelectNodes("//action_set[@id='hare']")
$hareCount = $hareNodes.Count
Write-Host ""
Write-Host "Hare action_set count: $hareCount (expected: 0 - provided by Native)"
if ($hareCount -eq 0) {
    Write-Host "  [OK] Hare correctly absent (loaded from Native module)"
} else {
    Write-Host "  [INFO] Found $hareCount hare set(s) - redundant with Native"
}

# Faction check
# Note: 'human_villager_drinker_with' sets are commented out in source, correctly absent
Write-Host ""
Write-Host "Faction action_set counts:"
$factionPrefixes = @(
    'dwarf', 'goblin', 'orc', 'uruk_hai', 'uruk',
    'cave_troll', 'hill_troll', 'nazghul', 'berserker',
    'dg_uruk', 'pale_uruk'
)
foreach ($prefix in $factionPrefixes) {
    $matchCount = 0
    foreach ($set in $allSets) {
        $sid = $set.GetAttribute("id")
        # Use regex with word boundary to avoid 'uruk' matching 'uruk_hai'
        if ($sid -match "^as_${prefix}_" -or $sid -eq "as_${prefix}") {
            # Exclude false positives: 'uruk' prefix should not match 'uruk_hai'
            if ($prefix -eq 'uruk' -and $sid -match '^as_uruk_hai') { continue }
            $matchCount++
        }
    }
    Write-Host "  ${prefix}: $matchCount"
}

# Report any old-mod action types that were in dwarf warrior but not in Native 1.3
Write-Host ""
Write-Host "Old mod dwarf actions NOT in Native 1.3 (excluded from output):"
$nativeTypeLookup = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($node in $nativeActionNodes) {
    $nativeTypeLookup.Add($node.GetAttribute("type")) | Out-Null
}
$excludedCount = 0
foreach ($type in $oldDwarfLookup.Keys) {
    if (-not $nativeTypeLookup.Contains($type)) {
        $isObsolete = Test-ObsoleteAction $type
        $tag = if ($isObsolete) { "obsolete" } else { "CUSTOM" }
        Write-Host "  [$tag] $type -> $($oldDwarfLookup[$type])"
        $excludedCount++
    }
}
if ($excludedCount -eq 0) {
    Write-Host "  (none)"
}

Write-Host ""
Write-Host "=== GENERATION COMPLETE ==="
