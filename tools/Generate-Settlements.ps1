<#
.SYNOPSIS
    Generates settlements.xml from scene.xscene entity data and existing TAOM_Map data.
.DESCRIPTION
    Parses the world map scene file for settlement entities, carries over existing
    settlement data where available, and generates placeholder entries for new regions.
.NOTES
    Run from repo root: .\tools\Generate-Settlements.ps1
#>

param(
    [string]$SceneFile = "E:\LOTRAOMAssets\scene.xscene",
    [string]$ExistingSettlements = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml",
    [string]$OutputFile = "Main\_Module\ModuleData\settlements.xml"
)

$ErrorActionPreference = "Stop"

# ============================================================
# CONFIGURATION: Region -> Culture -> Vanilla Scene mappings
# ============================================================

$RegionConfig = @{
    'A'  = @{ Culture = 'Culture.aserai';     SceneBase = 'aserai';   OwnerPrefix = 'Faction.clan_aserai' }
    'B'  = @{ Culture = 'Culture.battania';   SceneBase = 'battania'; OwnerPrefix = 'Faction.clan_battania' }
    'DG' = @{ Culture = 'Culture.dolguldur';  SceneBase = 'empire';   OwnerPrefix = 'Faction.clan_dolguldur' }
    'E'  = @{ Culture = 'Culture.erebor';     SceneBase = 'sturgia';  OwnerPrefix = 'Faction.clan_erebor' }
    'EN' = @{ Culture = 'Culture.empire';     SceneBase = 'empire';   OwnerPrefix = 'Faction.clan_empire_north' }
    'ES' = @{ Culture = 'Culture.empire';     SceneBase = 'empire';   OwnerPrefix = 'Faction.clan_empire_south' }
    'EW' = @{ Culture = 'Culture.gondor';     SceneBase = 'empire';   OwnerPrefix = 'Faction.clan_empire_west' }
    'G'  = @{ Culture = 'Culture.gundabad';   SceneBase = 'sturgia';  OwnerPrefix = 'Faction.clan_gundabad' }
    'I'  = @{ Culture = 'Culture.isengard';   SceneBase = 'empire';   OwnerPrefix = 'Faction.clan_isengard' }
    'K'  = @{ Culture = 'Culture.khuzait';    SceneBase = 'khuzait';  OwnerPrefix = 'Faction.clan_khuzait' }
    'L'  = @{ Culture = 'Culture.lothlorien'; SceneBase = 'battania'; OwnerPrefix = 'Faction.clan_lothlorien' }
    'M'  = @{ Culture = 'Culture.mirkwood';   SceneBase = 'battania'; OwnerPrefix = 'Faction.clan_mirkwood' }
    'R'  = @{ Culture = 'Culture.rivendell';  SceneBase = 'battania'; OwnerPrefix = 'Faction.clan_rivendell' }
    'RU' = @{ Culture = 'Culture.khuzait';    SceneBase = 'khuzait';  OwnerPrefix = 'Faction.clan_khuzait' }
    'S'  = @{ Culture = 'Culture.sturgia';    SceneBase = 'sturgia';  OwnerPrefix = 'Faction.clan_sturgia' }
    'U'  = @{ Culture = 'Culture.umbar';      SceneBase = 'aserai';   OwnerPrefix = 'Faction.clan_umbar' }
    'V'  = @{ Culture = 'Culture.vlandia';    SceneBase = 'vlandia';  OwnerPrefix = 'Faction.clan_vlandia' }
}

# Special named settlements that don't follow region_number pattern
$SpecialNamedIds = @{
    'town_isengard'             = @{ Region = 'I';  Type = 'town' }
    'village_isengard_a'        = @{ Region = 'I';  Type = 'village';        Parent = 'town_isengard' }
    'castle_orthanc_gate'       = @{ Region = 'I';  Type = 'castle' }
    'castle_village_isengard_a' = @{ Region = 'I';  Type = 'castle_village'; Parent = 'castle_orthanc_gate' }
}

# Vanilla scene pools per culture base
$TownScenes = @{
    'empire'   = @('empire_town_a','empire_town_b','empire_town_e','empire_town_g','empire_town_h','empire_town_k','empire_town_l','empire_town_m','empire_town_n','empire_town_o','empire_town_p','empire_town_q','empire_town_r','empire_town_s')
    'aserai'   = @('aserai_town_a','aserai_town_b','aserai_town_c','aserai_town_d','aserai_town_e','aserai_town_f','aserai_town_g','aserai_town_h')
    'battania' = @('battania_town_a','battania_town_b','battania_town_c','battania_town_d','battania_town_e')
    'khuzait'  = @('khuzait_town_004','khuzait_town_e','khuzait_town_f','khuzait_town_g')
    'sturgia'  = @('sturgia_town_a','sturgia_town_b','sturgia_town_c','sturgia_town_d','sturgia_town_f','sturgia_town_g')
    'vlandia'  = @('vlandia_town_a','vlandia_town_c','vlandia_town_d','vlandia_town_e','vlandia_town_f','vlandia_town_g','vlandia_town_h','vlandia_city_a','vlandia_city_d')
}

$CastleScenes = @{
    'empire'   = @('empire_siege_001','empire_castle_004','empire_castle_b','empire_castle_c','empire_castle_f','empire_castle_g')
    'aserai'   = @('aserai_castle_002','aserai_castle_a','aserai_castle_c','aserai_castle_d')
    'battania' = @('battania_castle_a','battania_castle_b')
    'khuzait'  = @('khuzait_castle_002','khuzait_castle_siege_001')
    'sturgia'  = @('sturgia_castle_003','sturgia_castle_c','sturgia_castle_siege_001')
    'vlandia'  = @('vlandia_castle_005a','vlandia_castle_a','vlandia_castle_b','vlandia_castle_c','vlandia_castle_d')
}

$VillageScenes = @{
    'empire'   = @('empire_village_001','empire_village_003','empire_village_007','empire_village_008','empire_village_011','empire_village_b','empire_village_c','empire_village_e','empire_village_f','empire_village_g','empire_village_j','empire_village_k','empire_village_m','empire_village_o','empire_village_p','empire_village_t','empire_village_v','empire_village_x','empire_village_z')
    'aserai'   = @('aserai_village_b','aserai_village_c','aserai_village_d','aserai_village_e','aserai_village_g','aserai_village_j','aserai_village_k','aserai_village_l','aserai_village_m')
    'battania' = @('battania_village','battania_village_e','battania_village_g','battania_village_h','battania_village_i','battania_village_j','battania_village_k','battania_village_l','battania_village_m')
    'khuzait'  = @('khuzait_village_a','khuzait_village_b','khuzait_village_c','khuzait_village_d','khuzait_village_e','khuzait_village_f','khuzait_village_g','khuzait_village_h','khuzait_village_i','khuzait_village_j','khuzait_village_k','khuzait_village_l')
    'sturgia'  = @('sturgia_village_a','sturgia_village_b','sturgia_village_c','sturgia_village_d','sturgia_village_e','sturgia_village_f','sturgia_village_g','sturgia_village_h','sturgia_village_j','sturgia_village_k','sturgia_village_l')
    'vlandia'  = @('vlandia_village_b','vlandia_village_c','vlandia_village_d','vlandia_village_e','vlandia_village_f','vlandia_village_g','vlandia_village_h','vlandia_village_i','vlandia_village_j','vlandia_village_k','vlandia_village_l','vlandia_village_m','vlandia_village_n')
}

$CultureSceneDetails = @{
    'empire' = @{
        Arena       = 'arena_empire_a'
        Tavern      = 'empire_house_c_tavern_a'
        LordsL1     = 'empire_castle_keep_a_l1_interior'
        LordsL2     = 'empire_castle_keep_a_l2_interior'
        LordsL3     = 'empire_castle_keep_a_l3_interior'
        Prison      = 'empire_dungeon_stealth'
        House       = 'empire_house_d_interior_house'
        BgTown      = 'menu_empire_3'
        BgCastle    = 'gui_bg_castle_empire'
        BgCastleMenu= 'menu_empire_1'
        BgVillage   = 'gui_bg_village_empire'
        WaitTown    = 'wait_empire_town'
        WaitVill    = 'wait_empire_village'
    }
    'aserai' = @{
        Arena       = 'arena_aserai_a'
        Tavern      = 'aserai_tavern_interior'
        LordsL1     = 'aserai_castle_keep_a_l1_interior'
        LordsL2     = 'aserai_castle_keep_a_l2_interior'
        LordsL3     = 'aserai_castle_keep_a_l3_interior'
        Prison      = 'aserai_dungeon_stealth'
        House       = 'arabian_house_new_a_interior_a_house'
        BgTown      = 'gui_bg_town_aserai'
        BgCastle    = 'gui_bg_castle_aserai'
        BgCastleMenu= 'menu_aserai_3'
        BgVillage   = 'gui_bg_village_aserai'
        WaitTown    = 'wait_aserai_town'
        WaitVill    = 'wait_aserai_village'
    }
    'battania' = @{
        Arena       = 'arena_battania_a'
        Tavern      = 'battania_town_house_a_interior_a_tavern'
        LordsL1     = 'battania_castle_keep_a_l1_interior'
        LordsL2     = 'battania_castle_keep_a_l2_interior'
        LordsL3     = 'battania_castle_keep_a_l3_interior'
        Prison      = 'battania_dungeon_stealth'
        House       = 'battania_house_a_interior_house'
        BgTown      = 'gui_bg_town_battania'
        BgCastle    = 'gui_bg_castle_battania'
        BgCastleMenu= 'gui_bg_castle_battania'
        BgVillage   = 'gui_bg_village_battania'
        WaitTown    = 'wait_battania_town'
        WaitVill    = 'wait_battania_village'
    }
    'khuzait' = @{
        Arena       = 'arena_khuzait_a'
        Tavern      = 'khuzait_tavern_a'
        LordsL1     = 'khuzait_castle_keep_a_l1_interior'
        LordsL2     = 'khuzait_castle_keep_a_l2_interior'
        LordsL3     = 'khuzait_castle_keep_a_l3_interior'
        Prison      = 'khuzait_dungeon_stealth'
        House       = 'khuzait_house_a_interior_house'
        BgTown      = 'gui_bg_town_khuzait'
        BgCastle    = 'gui_bg_castle_khuzait'
        BgCastleMenu= 'gui_bg_castle_khuzait'
        BgVillage   = 'gui_bg_village_khuzait'
        WaitTown    = 'wait_khuzait_town'
        WaitVill    = 'wait_khuzait_village'
    }
    'sturgia' = @{
        Arena       = 'arena_sturgia_a'
        Tavern      = 'sturgia_house_b_interior_tavern'
        LordsL1     = 'sturgia_castle_keep_a_l1_interior'
        LordsL2     = 'sturgia_castle_keep_a_l2_interior'
        LordsL3     = 'sturgia_castle_keep_a_l3_interior'
        Prison      = 'sturgia_dungeon_stealth'
        House       = 'sturgia_house_a_interior_house'
        BgTown      = 'gui_bg_town_sturgia'
        BgCastle    = 'gui_bg_castle_sturgia'
        BgCastleMenu= 'gui_bg_castle_sturgia'
        BgVillage   = 'gui_bg_village_sturgia'
        WaitTown    = 'wait_sturgia_town'
        WaitVill    = 'wait_sturgia_village'
    }
    'vlandia' = @{
        Arena       = 'arena_vlandia_a'
        Tavern      = 'vlandia_tavern_interior_a'
        LordsL1     = 'european_castle_keep_a_l1_interior'
        LordsL2     = 'european_castle_keep_a_l2_interior'
        LordsL3     = 'european_castle_keep_a_l3_interior'
        Prison      = 'vlandia_dungeon_stealth'
        House       = 'vlandia_house_d_interior_house'
        BgTown      = 'gui_bg_town_vlandia'
        BgCastle    = 'gui_bg_castle_vlandia'
        BgCastleMenu= 'gui_bg_castle_vlandia'
        BgVillage   = 'gui_bg_village_vlandia'
        WaitTown    = 'wait_vlandia_town'
        WaitVill    = 'wait_vlandia_village'
    }
}

$VillageTypes = @(
    'VillageType.wheat_farm',
    'VillageType.cattle_farm',
    'VillageType.iron_mine',
    'VillageType.fisherman',
    'VillageType.silver_mine',
    'VillageType.flax_plant',
    'VillageType.silk_plant',
    'VillageType.clay_mine',
    'VillageType.salt_mine',
    'VillageType.lumberjack',
    'VillageType.swine_farm',
    'VillageType.trapper'
)

# Scene index counters per culture (for cycling through scene pools)
$sceneCounters = @{}

function Get-CycledScene($pool, $culture, $type) {
    $key = "$culture-$type"
    if (-not $sceneCounters.ContainsKey($key)) { $sceneCounters[$key] = 0 }
    $idx = $sceneCounters[$key] % $pool.Count
    $sceneCounters[$key]++
    return $pool[$idx]
}

# ============================================================
# STEP 1: Parse scene.xscene for settlement entities
# ============================================================
Write-Host "Parsing scene file: $SceneFile" -ForegroundColor Cyan

# Sub-component patterns to exclude
$excludePatterns = @(
    '^town_gate$',
    '^town_circle_decal$',
    '_l[0-9]$', '_l3$',
    '_main$',
    '_wall_l3$',
    '_wall_l[0-9]$',
    '^castle_gate$',
    '^castle_orthanc_gate$',  # This IS a settlement, handle separately
    '_Gate$'  # But not if part of entity name
)

# We'll use regex on the raw text for speed (file is huge)
$sceneEntities = @{}  # name -> @{PosX; PosY}

$content = [System.IO.File]::ReadAllText($SceneFile)

# Two-step extraction: find each <game_entity name="X"> then look for its own
# <transform position="Y"> within a limited window, stopping before nested entities.
# This avoids parent entities "stealing" child transforms via greedy/lazy matching.
$entityOpenPattern = '<game_entity\s+name="([^"]+)"[^>]*>'
$entityOpenMatches = [regex]::Matches($content, $entityOpenPattern)

Write-Host "  Found $($entityOpenMatches.Count) total game_entity elements"

$transformsFound = 0
foreach ($m in $entityOpenMatches) {
    $name = $m.Groups[1].Value
    $startPos = $m.Index + $m.Length
    $windowSize = [Math]::Min(500, $content.Length - $startPos)
    $window = $content.Substring($startPos, $windowSize)

    # Find the first <transform position="..."> in this window
    $transformMatch = [regex]::Match($window, '<transform\s+position="([^"]+)"')
    if (-not $transformMatch.Success) { continue }

    # Ensure the transform comes before any nested <game_entity (which would be a child)
    $nestedIdx = $window.IndexOf('<game_entity')
    if ($nestedIdx -ge 0 -and $transformMatch.Index -gt $nestedIdx) { continue }

    $transformsFound++
    $posStr = $transformMatch.Groups[1].Value
    $posParts = $posStr -split ',\s*'
    $posX = [decimal]$posParts[0]
    $posY = [decimal]$posParts[1]

    # Determine if this is a settlement entity
    $isSettlement = $false
    $settType = $null

    # Check special named IDs first
    if ($SpecialNamedIds.ContainsKey($name)) {
        $isSettlement = $true
        $settType = $SpecialNamedIds[$name].Type
    }
    # castle_village_ prefix (check before castle_ to avoid misclassification)
    elseif ($name -match '^castle_village_[A-Z]') {
        $isSettlement = $true
        $settType = 'castle_village'
    }
    # village_ prefix
    elseif ($name -match '^village_[A-Z]') {
        # Must have _N suffix (number or letter after underscore after region+number)
        if ($name -match '^village_[A-Z]+[0-9]+_[0-9a-z]+$' -or $name -match '^village_isengard') {
            $isSettlement = $true
            $settType = 'village'
        }
    }
    # town_ prefix
    elseif ($name -match '^town_[A-Z]') {
        # Exclude sub-components
        $isSubComponent = $false
        if ($name -match '_gate$' -or $name -match '_circle_decal$' -or
            $name -cmatch '_l[0-9]$' -or $name -cmatch '_l3$' -or
            $name -match '_main$' -or $name -match '_wall_l' -or
            $name -match '_Main$') {
            $isSubComponent = $true
        }
        if (-not $isSubComponent) {
            $isSettlement = $true
            $settType = 'town'
        }
    }
    # castle_ prefix (not castle_village_)
    elseif ($name -match '^castle_[A-Z]' -and $name -notmatch '^castle_village_') {
        # Exclude sub-components
        $isSubComponent = $false
        if ($name -match '_gate$' -or $name -cmatch '_l[0-9]$' -or
            $name -match '_main$' -or $name -match '_wall_l') {
            $isSubComponent = $true
        }
        if (-not $isSubComponent) {
            $isSettlement = $true
            $settType = 'castle'
        }
    }
    # castle_orthanc_gate is special - it IS a settlement
    elseif ($name -eq 'castle_orthanc_gate') {
        $isSettlement = $true
        $settType = 'castle'
    }

    if ($isSettlement -and -not $sceneEntities.ContainsKey($name)) {
        $sceneEntities[$name] = @{
            PosX = $posX
            PosY = $posY
            Type = $settType
        }
    }
}

# Count by type
$towns = ($sceneEntities.Values | Where-Object { $_.Type -eq 'town' }).Count
$castles = ($sceneEntities.Values | Where-Object { $_.Type -eq 'castle' }).Count
$villages = ($sceneEntities.Values | Where-Object { $_.Type -eq 'village' }).Count
$cVillages = ($sceneEntities.Values | Where-Object { $_.Type -eq 'castle_village' }).Count

Write-Host "  Settlement entities found:" -ForegroundColor Green
Write-Host "    Towns: $towns"
Write-Host "    Castles: $castles"
Write-Host "    Villages: $villages"
Write-Host "    Castle Villages: $cVillages"
Write-Host "    TOTAL: $($sceneEntities.Count)"

# ============================================================
# STEP 2: Parse existing settlements.xml
# ============================================================
Write-Host "`nParsing existing settlements: $ExistingSettlements" -ForegroundColor Cyan

[xml]$existingXml = Get-Content $ExistingSettlements -Encoding UTF8
$existingMap = @{}

foreach ($sett in $existingXml.Settlements.Settlement) {
    $existingMap[$sett.id] = $sett
}
Write-Host "  Loaded $($existingMap.Count) existing settlements"

# ============================================================
# HELPER: Determine region code from settlement ID
# ============================================================
function Get-RegionCode($id) {
    # Special named
    if ($SpecialNamedIds.ContainsKey($id)) {
        return $SpecialNamedIds[$id].Region
    }

    # Strip prefix to get region+number part
    $suffix = $id
    if ($id -match '^castle_village_(.+)$') { $suffix = $Matches[1] }
    elseif ($id -match '^village_(.+)$') { $suffix = $Matches[1] }
    elseif ($id -match '^castle_(.+)$') { $suffix = $Matches[1] }
    elseif ($id -match '^town_(.+)$') { $suffix = $Matches[1] }

    # Extract region code (letters before first digit or underscore)
    if ($suffix -match '^([A-Z]+)') {
        return $Matches[1]
    }
    return $null
}

# ============================================================
# HELPER: Determine parent settlement for villages
# ============================================================
function Get-ParentId($id, $type) {
    if ($SpecialNamedIds.ContainsKey($id)) {
        return $SpecialNamedIds[$id].Parent
    }

    if ($type -eq 'village') {
        # village_A1_1 -> town_A1
        if ($id -match '^village_([A-Z]+[0-9]+)_[0-9a-z]+$') {
            return "town_$($Matches[1])"
        }
    }
    elseif ($type -eq 'castle_village') {
        # castle_village_DG1_1 -> castle_DG1
        if ($id -match '^castle_village_([A-Z]+[0-9]+)_[0-9a-z]+$') {
            return "castle_$($Matches[1])"
        }
    }
    return $null
}

# ============================================================
# HELPER: XML escape
# ============================================================
function Xml-Escape($s) {
    if ($null -eq $s) { return "" }
    return $s.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;').Replace('"','&quot;')
}

# ============================================================
# STEP 3: Generate XML output
# ============================================================
Write-Host "`nGenerating settlements XML..." -ForegroundColor Cyan

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<Settlements>')

# Village type counter for cycling
$vtCounter = 0

# Group entities by region, then process in order: castles+castle_villages, towns+villages
$entityList = $sceneEntities.GetEnumerator() | ForEach-Object {
    [PSCustomObject]@{
        Id     = $_.Key
        PosX   = $_.Value.PosX
        PosY   = $_.Value.PosY
        Type   = $_.Value.Type
        Region = Get-RegionCode $_.Key
    }
}

# Sort regions
$regionOrder = @('EN','ES','EW','A','B','V','K','S','DG','E','G','I','L','M','R','RU','U')
$processedRegions = @{}

# Track how many settlements carry over vs new
$carryOverCount = 0
$newCount = 0

foreach ($region in $regionOrder) {
    $regionEntities = $entityList | Where-Object { $_.Region -eq $region }
    if ($regionEntities.Count -eq 0) { continue }

    $processedRegions[$region] = $true
    $config = $RegionConfig[$region]
    if ($null -eq $config) {
        Write-Warning "No config for region: $region - skipping"
        continue
    }

    $culture = $config.Culture
    $sceneBase = $config.SceneBase
    $ownerPrefix = $config.OwnerPrefix
    $sd = $CultureSceneDetails[$sceneBase]

    # Region comment
    $regionName = switch ($region) {
        'EN' { 'EMPIRE NORTH (Rohan)' }
        'ES' { 'EMPIRE SOUTH (Mordor)' }
        'EW' { 'GONDOR' }
        'A'  { 'ASERAI (Harad)' }
        'B'  { 'BATTANIA (Dunland)' }
        'V'  { 'VLANDIA' }
        'K'  { 'KHUZAIT (Easterlings)' }
        'S'  { 'STURGIA (Dale/North)' }
        'DG' { 'DOL GULDUR' }
        'E'  { 'EREBOR' }
        'G'  { 'GUNDABAD' }
        'I'  { 'ISENGARD' }
        'L'  { 'LOTHLORIEN' }
        'M'  { 'MIRKWOOD' }
        'R'  { 'RIVENDELL' }
        'RU' { 'RHUN' }
        'U'  { 'UMBAR' }
        default { $region }
    }
    [void]$sb.AppendLine("  <!-- ============================================== -->")
    [void]$sb.AppendLine("  <!-- $regionName -->")
    [void]$sb.AppendLine("  <!-- ============================================== -->")

    # Process castles first, then their castle_villages
    $castleEntities = $regionEntities | Where-Object { $_.Type -eq 'castle' } | Sort-Object Id
    foreach ($castle in $castleEntities) {
        $id = $castle.Id
        $existing = $existingMap[$id]

        if ($existing) {
            # Carry over existing settlement, update position from scene
            $carryOverCount++
            $nameAttr = Xml-Escape $existing.name
            $ownerAttr = Xml-Escape $existing.owner
            $cultureAttr = Xml-Escape $existing.culture
            $gatePosX = if ($existing.gate_posX) { $existing.gate_posX } else { [Math]::Round($castle.PosX + 1.0, 3) }
            $gatePosY = if ($existing.gate_posY) { $existing.gate_posY } else { [Math]::Round($castle.PosY - 1.0, 3) }

            # Get component data
            $comp = $existing.Components.Town
            $bgMesh = if ($comp.background_mesh) { $comp.background_mesh } else { $sd.BgCastleMenu }
            $waitMesh = if ($comp.wait_mesh) { $comp.wait_mesh } else { $sd.WaitTown }
            $gateRot = if ($comp.gate_rotation) { $comp.gate_rotation } else { "0.908" }
            $prosperity = if ($comp.prosperity) { $comp.prosperity } else { "600" }

            # Get locations
            $centerLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'center' }
            $centerScene = if ($centerLoc.scene_name) { $centerLoc.scene_name } else { Get-CycledScene $CastleScenes[$sceneBase] $sceneBase 'castle' }
            $lordsLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'lordshall' }
            $lordsL1 = if ($lordsLoc.scene_name_1) { $lordsLoc.scene_name_1 } else { $sd.LordsL1 }
            $lordsL2 = if ($lordsLoc.scene_name_2) { $lordsLoc.scene_name_2 } else { $sd.LordsL2 }
            $lordsL3 = if ($lordsLoc.scene_name_3) { $lordsLoc.scene_name_3 } else { $sd.LordsL3 }
            $prisonLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'prison' }
            $prisonScene = if ($prisonLoc.scene_name) { $prisonLoc.scene_name } else { $sd.Prison }

            # Build buildings XML
            $buildingsXml = ""
            if ($comp.Buildings -and $comp.Buildings.Building) {
                $buildingsXml = "`n          <Buildings>"
                foreach ($b in $comp.Buildings.Building) {
                    $buildingsXml += "`n            <Building id=`"$($b.id)`" level=`"$($b.level)`" />"
                }
                $buildingsXml += "`n          </Buildings>"
            }
            else {
                $buildingsXml = @"

          <Buildings>
            <Building id="building_castle_fortifications" level="1" />
            <Building id="building_castle_barracks" level="1" />
            <Building id="building_castle_training_fields" level="1" />
            <Building id="building_castle_guard_house" level="0" />
            <Building id="building_castle_siege_workshop" level="0" />
            <Building id="building_castle_castallans_office" level="1" />
            <Building id="building_castle_granary" level="1" />
            <Building id="building_castle_craftmans_quarters" level="0" />
            <Building id="building_castle_farmlands" level="1" />
            <Building id="building_castle_mason" level="0" />
            <Building id="building_castle_roads_and_paths" level="1" />
          </Buildings>
"@
            }
        }
        else {
            # New settlement - generate from template
            $newCount++
            $nameAttr = "{=Settlements.Settlement.name.$id}Castle $($id -replace 'castle_','')"
            $ownerAttr = "${ownerPrefix}_1"
            $cultureAttr = $culture
            $gatePosX = [Math]::Round($castle.PosX + 1.0, 3)
            $gatePosY = [Math]::Round($castle.PosY - 1.0, 3)
            $bgMesh = $sd.BgCastleMenu
            $waitMesh = $sd.WaitTown
            $gateRot = "0.908"
            $prosperity = "600"
            $centerScene = Get-CycledScene $CastleScenes[$sceneBase] $sceneBase 'castle'
            $lordsL1 = $sd.LordsL1
            $lordsL2 = $sd.LordsL2
            $lordsL3 = $sd.LordsL3
            $prisonScene = $sd.Prison

            $buildingsXml = @"

          <Buildings>
            <Building id="building_castle_fortifications" level="1" />
            <Building id="building_castle_barracks" level="1" />
            <Building id="building_castle_training_fields" level="1" />
            <Building id="building_castle_guard_house" level="0" />
            <Building id="building_castle_siege_workshop" level="0" />
            <Building id="building_castle_castallans_office" level="1" />
            <Building id="building_castle_granary" level="1" />
            <Building id="building_castle_craftmans_quarters" level="0" />
            <Building id="building_castle_farmlands" level="1" />
            <Building id="building_castle_mason" level="0" />
            <Building id="building_castle_roads_and_paths" level="1" />
          </Buildings>
"@
        }

        [void]$sb.AppendLine("  <Settlement id=`"$id`" name=`"$nameAttr`" owner=`"$ownerAttr`" posX=`"$($castle.PosX)`" posY=`"$($castle.PosY)`" culture=`"$cultureAttr`" gate_posX=`"$gatePosX`" gate_posY=`"$gatePosY`">")
        [void]$sb.AppendLine("    <Components>")
        [void]$sb.AppendLine("      <Town id=`"castle_comp_$($id -replace 'castle_','')`" is_castle=`"true`" background_crop_position=`"0.0`" background_mesh=`"$bgMesh`" wait_mesh=`"$waitMesh`" gate_rotation=`"$gateRot`" prosperity=`"$prosperity`">$buildingsXml")
        [void]$sb.AppendLine("      </Town>")
        [void]$sb.AppendLine("    </Components>")
        [void]$sb.AppendLine("    <Locations complex_template=`"LocationComplexTemplate.castle_complex`">")
        [void]$sb.AppendLine("      <Location id=`"center`" scene_name=`"$centerScene`" scene_name_1=`"$centerScene`" scene_name_2=`"$centerScene`" scene_name_3=`"$centerScene`" />")
        [void]$sb.AppendLine("      <Location id=`"lordshall`" scene_name_1=`"$lordsL1`" scene_name_2=`"$lordsL2`" scene_name_3=`"$lordsL3`" />")
        [void]$sb.AppendLine("      <Location id=`"prison`" scene_name=`"$prisonScene`" />")
        [void]$sb.AppendLine("    </Locations>")
        [void]$sb.AppendLine("  </Settlement>")

        # Now find castle_villages bound to this castle
        $cvEntities = $regionEntities | Where-Object { $_.Type -eq 'castle_village' -and (Get-ParentId $_.Id $_.Type) -eq $id } | Sort-Object Id
        foreach ($cv in $cvEntities) {
            $cvId = $cv.Id
            $cvExisting = $existingMap[$cvId]

            if ($cvExisting) {
                $carryOverCount++
                $cvName = Xml-Escape $cvExisting.name
                $cvCulture = Xml-Escape $cvExisting.culture
                $cvText = if ($cvExisting.text) { " text=`"$(Xml-Escape $cvExisting.text)`"" } else { "" }

                $vComp = $cvExisting.Components.Village
                $vType = if ($vComp.village_type) { $vComp.village_type } else { $VillageTypes[$vtCounter++ % $VillageTypes.Count] }
                $hearth = if ($vComp.hearth) { $vComp.hearth } else { "350" }
                $vGateRot = if ($vComp.gate_rotation) { " gate_rotation=`"$($vComp.gate_rotation)`"" } else { "" }
                $vBgCrop = if ($vComp.background_crop_position) { $vComp.background_crop_position } else { "0.0" }
                $vBgMesh = if ($vComp.background_mesh) { $vComp.background_mesh } else { $sd.BgVillage }
                $vWaitMesh = if ($vComp.wait_mesh) { $vComp.wait_mesh } else { $sd.WaitVill }
                $vCastleBg = if ($vComp.castle_background_mesh) { $vComp.castle_background_mesh } else { $sd.BgCastle }

                $vCenterLoc = $cvExisting.Locations.Location | Where-Object { $_.id -eq 'village_center' }
                $vScene = if ($vCenterLoc.scene_name) { $vCenterLoc.scene_name } else { Get-CycledScene $VillageScenes[$sceneBase] $sceneBase 'village' }
                $maxPros = if ($vCenterLoc.max_prosperity) { " max_prosperity=`"$($vCenterLoc.max_prosperity)`"" } else { "" }
            }
            else {
                $newCount++
                $cvName = "{=Settlements.Settlement.name.$cvId}Castle Village $($cvId -replace 'castle_village_','')"
                $cvCulture = $culture
                $cvText = ""
                $vType = $VillageTypes[$vtCounter++ % $VillageTypes.Count]
                $hearth = "350"
                $vGateRot = ""
                $vBgCrop = "0.0"
                $vBgMesh = $sd.BgVillage
                $vWaitMesh = $sd.WaitVill
                $vCastleBg = $sd.BgCastle
                $vScene = Get-CycledScene $VillageScenes[$sceneBase] $sceneBase 'village'
                $maxPros = ""
            }

            [void]$sb.AppendLine("  <Settlement id=`"$cvId`" name=`"$cvName`" posX=`"$($cv.PosX)`" posY=`"$($cv.PosY)`" culture=`"$cvCulture`"$cvText>")
            [void]$sb.AppendLine("    <Components>")
            [void]$sb.AppendLine("      <Village id=`"castle_village_comp_$($cvId -replace 'castle_village_','')`" village_type=`"$vType`" hearth=`"$hearth`"$vGateRot bound=`"Settlement.$id`" background_crop_position=`"$vBgCrop`" background_mesh=`"$vBgMesh`" wait_mesh=`"$vWaitMesh`" castle_background_mesh=`"$vCastleBg`" />")
            [void]$sb.AppendLine("    </Components>")
            [void]$sb.AppendLine("    <Locations complex_template=`"LocationComplexTemplate.village_complex`">")
            [void]$sb.AppendLine("      <Location id=`"village_center`" scene_name=`"$vScene`"$maxPros />")
            [void]$sb.AppendLine("    </Locations>")
            [void]$sb.AppendLine("    <CommonAreas>")
            [void]$sb.AppendLine("      <Area type=`"Pasture`" name=`"{=fOUsLdZR}Pasture`" />")
            [void]$sb.AppendLine("      <Area type=`"Thicket`" name=`"{=66Mzk0NZ}Thicket`" />")
            [void]$sb.AppendLine("      <Area type=`"Bog`" name=`"{=iXA5SttU}Bog`" />")
            [void]$sb.AppendLine("    </CommonAreas>")
            [void]$sb.AppendLine("  </Settlement>")
        }
    }

    # Process towns, then their villages
    $townEntities = $regionEntities | Where-Object { $_.Type -eq 'town' } | Sort-Object Id
    foreach ($town in $townEntities) {
        $id = $town.Id
        $existing = $existingMap[$id]

        if ($existing) {
            $carryOverCount++
            $nameAttr = Xml-Escape $existing.name
            $ownerAttr = Xml-Escape $existing.owner
            $cultureAttr = Xml-Escape $existing.culture
            $textAttr = if ($existing.text) { " text=`"$(Xml-Escape $existing.text)`"" } else { "" }
            $gatePosX = if ($existing.gate_posX) { $existing.gate_posX } else { [Math]::Round($town.PosX + 0.3, 4) }
            $gatePosY = if ($existing.gate_posY) { $existing.gate_posY } else { [Math]::Round($town.PosY - 3.0, 4) }

            $comp = $existing.Components.Town
            $bgCrop = if ($comp.background_crop_position) { $comp.background_crop_position } else { "0.0" }
            $bgMesh = if ($comp.background_mesh) { $comp.background_mesh } else { $sd.BgTown }
            $waitMesh = if ($comp.wait_mesh) { $comp.wait_mesh } else { $sd.WaitTown }
            $gateRot = if ($comp.gate_rotation) { $comp.gate_rotation } else { "0.378" }
            $prosperity = if ($comp.prosperity) { $comp.prosperity } else { "3500" }

            $centerLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'center' }
            $centerScene = if ($centerLoc.scene_name) { $centerLoc.scene_name } else { Get-CycledScene $TownScenes[$sceneBase] $sceneBase 'town' }

            $arenaLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'arena' }
            $arenaScene = if ($arenaLoc.scene_name) { $arenaLoc.scene_name } else { $sd.Arena }

            $tavernLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'tavern' }
            $tavernScene = if ($tavernLoc.scene_name) { $tavernLoc.scene_name } else { $sd.Tavern }

            $lordsLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'lordshall' }
            $lordsL1 = if ($lordsLoc.scene_name_1) { $lordsLoc.scene_name_1 } else { $sd.LordsL1 }
            $lordsL2 = if ($lordsLoc.scene_name_2) { $lordsLoc.scene_name_2 } else { $sd.LordsL2 }
            $lordsL3 = if ($lordsLoc.scene_name_3) { $lordsLoc.scene_name_3 } else { $sd.LordsL3 }

            $prisonLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'prison' }
            $prisonScene = if ($prisonLoc.scene_name) { $prisonLoc.scene_name } else { $sd.Prison }

            $houseLoc = $existing.Locations.Location | Where-Object { $_.id -eq 'house_1' }
            $houseScene = if ($houseLoc.scene_name) { $houseLoc.scene_name } else { $sd.House }

            # Build buildings XML
            $buildingsXml = ""
            if ($comp.Buildings -and $comp.Buildings.Building) {
                $buildingsXml = "`n          <Buildings>"
                foreach ($b in $comp.Buildings.Building) {
                    $buildingsXml += "`n            <Building id=`"$($b.id)`" level=`"$($b.level)`" />"
                }
                $buildingsXml += "`n          </Buildings>"
            }
            else {
                $buildingsXml = @"

          <Buildings>
            <Building id="building_settlement_fortifications" level="1" />
            <Building id="building_settlement_barracks" level="1" />
            <Building id="building_settlement_training_fields" level="1" />
            <Building id="building_settlement_guard_house" level="0" />
            <Building id="building_settlement_siege_workshop" level="0" />
            <Building id="building_settlement_tax_office" level="1" />
            <Building id="building_settlement_marketplace" level="1" />
            <Building id="building_settlement_warehouse" level="0" />
            <Building id="building_settlement_mason" level="1" />
            <Building id="building_settlement_waterworks" level="0" />
            <Building id="building_settlement_courthouse" level="0" />
            <Building id="building_settlement_roads_and_paths" level="1" />
          </Buildings>
"@
            }
        }
        else {
            $newCount++
            $displayName = ($id -replace 'town_','')
            $nameAttr = "{=Settlements.Settlement.name.$id}Town $displayName"
            $ownerAttr = "${ownerPrefix}_1"
            $cultureAttr = $culture
            $textAttr = ""
            $gatePosX = [Math]::Round($town.PosX + 0.3, 4)
            $gatePosY = [Math]::Round($town.PosY - 3.0, 4)
            $bgCrop = "0.0"
            $bgMesh = $sd.BgTown
            $waitMesh = $sd.WaitTown
            $gateRot = "0.378"
            $prosperity = "3500"
            $centerScene = Get-CycledScene $TownScenes[$sceneBase] $sceneBase 'town'
            $arenaScene = $sd.Arena
            $tavernScene = $sd.Tavern
            $lordsL1 = $sd.LordsL1
            $lordsL2 = $sd.LordsL2
            $lordsL3 = $sd.LordsL3
            $prisonScene = $sd.Prison
            $houseScene = $sd.House

            $buildingsXml = @"

          <Buildings>
            <Building id="building_settlement_fortifications" level="1" />
            <Building id="building_settlement_barracks" level="1" />
            <Building id="building_settlement_training_fields" level="1" />
            <Building id="building_settlement_guard_house" level="0" />
            <Building id="building_settlement_siege_workshop" level="0" />
            <Building id="building_settlement_tax_office" level="1" />
            <Building id="building_settlement_marketplace" level="1" />
            <Building id="building_settlement_warehouse" level="0" />
            <Building id="building_settlement_mason" level="1" />
            <Building id="building_settlement_waterworks" level="0" />
            <Building id="building_settlement_courthouse" level="0" />
            <Building id="building_settlement_roads_and_paths" level="1" />
          </Buildings>
"@
        }

        [void]$sb.AppendLine("  <Settlement id=`"$id`" name=`"$nameAttr`" owner=`"$ownerAttr`" posX=`"$($town.PosX)`" posY=`"$($town.PosY)`" culture=`"$cultureAttr`" gate_posX=`"$gatePosX`" gate_posY=`"$gatePosY`"$textAttr>")
        [void]$sb.AppendLine("    <Components>")
        [void]$sb.AppendLine("      <Town id=`"town_comp_$($id -replace 'town_','')`" is_castle=`"false`" background_crop_position=`"$bgCrop`" background_mesh=`"$bgMesh`" wait_mesh=`"$waitMesh`" gate_rotation=`"$gateRot`" prosperity=`"$prosperity`">$buildingsXml")
        [void]$sb.AppendLine("      </Town>")
        [void]$sb.AppendLine("    </Components>")
        [void]$sb.AppendLine("    <Locations complex_template=`"LocationComplexTemplate.town_complex`">")
        [void]$sb.AppendLine("      <Location id=`"center`" scene_name=`"$centerScene`" scene_name_1=`"$centerScene`" scene_name_2=`"$centerScene`" scene_name_3=`"$centerScene`" />")
        [void]$sb.AppendLine("      <Location id=`"arena`" scene_name=`"$arenaScene`" />")
        [void]$sb.AppendLine("      <Location id=`"tavern`" scene_name=`"$tavernScene`" />")
        [void]$sb.AppendLine("      <Location id=`"lordshall`" scene_name_1=`"$lordsL1`" scene_name_2=`"$lordsL2`" scene_name_3=`"$lordsL3`" />")
        [void]$sb.AppendLine("      <Location id=`"prison`" scene_name=`"$prisonScene`" />")
        [void]$sb.AppendLine("      <Location id=`"house_1`" scene_name=`"$houseScene`" />")
        [void]$sb.AppendLine("      <Location id=`"house_2`" scene_name=`"$houseScene`" />")
        [void]$sb.AppendLine("      <Location id=`"house_3`" scene_name=`"$houseScene`" />")
        [void]$sb.AppendLine("      <Location id=`"alley`" />")
        [void]$sb.AppendLine("    </Locations>")
        [void]$sb.AppendLine("    <CommonAreas>")
        [void]$sb.AppendLine("      <Area type=`"Backstreet`" name=`"{=a0MVffcN}Backstreet`" />")
        [void]$sb.AppendLine("      <Area type=`"Clearing`" name=`"{=LWHIVshb}Clearing`" />")
        [void]$sb.AppendLine("      <Area type=`"Waterfront`" name=`"{=Rr1cy5Sk}Waterfront`" />")
        [void]$sb.AppendLine("    </CommonAreas>")
        [void]$sb.AppendLine("  </Settlement>")

        # Now find villages bound to this town
        $vEntities = $regionEntities | Where-Object { $_.Type -eq 'village' -and (Get-ParentId $_.Id $_.Type) -eq $id } | Sort-Object Id
        foreach ($v in $vEntities) {
            $vId = $v.Id
            $vExisting = $existingMap[$vId]

            if ($vExisting) {
                $carryOverCount++
                $vName = Xml-Escape $vExisting.name
                $vCulture = Xml-Escape $vExisting.culture
                $vText = if ($vExisting.text) { " text=`"$(Xml-Escape $vExisting.text)`"" } else { "" }

                $vComp = $vExisting.Components.Village
                $vType = if ($vComp.village_type) { $vComp.village_type } else { $VillageTypes[$vtCounter++ % $VillageTypes.Count] }
                $hearth = if ($vComp.hearth) { $vComp.hearth } else { "350" }
                $vGateRot = if ($vComp.gate_rotation) { " gate_rotation=`"$($vComp.gate_rotation)`"" } else { "" }
                $vBgCrop = if ($vComp.background_crop_position) { $vComp.background_crop_position } else { "0.0" }
                $vBgMesh = if ($vComp.background_mesh) { $vComp.background_mesh } else { $sd.BgVillage }
                $vWaitMesh = if ($vComp.wait_mesh) { $vComp.wait_mesh } else { $sd.WaitVill }
                $vCastleBg = if ($vComp.castle_background_mesh) { $vComp.castle_background_mesh } else { $sd.BgCastle }

                $vCenterLoc = $vExisting.Locations.Location | Where-Object { $_.id -eq 'village_center' }
                $vScene = if ($vCenterLoc.scene_name) { $vCenterLoc.scene_name } else { Get-CycledScene $VillageScenes[$sceneBase] $sceneBase 'village' }
                $maxPros = if ($vCenterLoc.max_prosperity) { " max_prosperity=`"$($vCenterLoc.max_prosperity)`"" } else { "" }
            }
            else {
                $newCount++
                $vName = "{=Settlements.Settlement.name.$vId}Village $($vId -replace 'village_','')"
                $vCulture = $culture
                $vText = ""
                $vType = $VillageTypes[$vtCounter++ % $VillageTypes.Count]
                $hearth = "350"
                $vGateRot = ""
                $vBgCrop = "0.0"
                $vBgMesh = $sd.BgVillage
                $vWaitMesh = $sd.WaitVill
                $vCastleBg = $sd.BgCastle
                $vScene = Get-CycledScene $VillageScenes[$sceneBase] $sceneBase 'village'
                $maxPros = ""
            }

            [void]$sb.AppendLine("  <Settlement id=`"$vId`" name=`"$vName`" posX=`"$($v.PosX)`" posY=`"$($v.PosY)`" culture=`"$vCulture`"$vText>")
            [void]$sb.AppendLine("    <Components>")
            [void]$sb.AppendLine("      <Village id=`"village_comp_$($vId -replace 'village_','')`" village_type=`"$vType`" hearth=`"$hearth`"$vGateRot bound=`"Settlement.$id`" background_crop_position=`"$vBgCrop`" background_mesh=`"$vBgMesh`" wait_mesh=`"$vWaitMesh`" castle_background_mesh=`"$vCastleBg`" />")
            [void]$sb.AppendLine("    </Components>")
            [void]$sb.AppendLine("    <Locations complex_template=`"LocationComplexTemplate.village_complex`">")
            [void]$sb.AppendLine("      <Location id=`"village_center`" scene_name=`"$vScene`"$maxPros />")
            [void]$sb.AppendLine("    </Locations>")
            [void]$sb.AppendLine("    <CommonAreas>")
            [void]$sb.AppendLine("      <Area type=`"Pasture`" name=`"{=fOUsLdZR}Pasture`" />")
            [void]$sb.AppendLine("      <Area type=`"Thicket`" name=`"{=66Mzk0NZ}Thicket`" />")
            [void]$sb.AppendLine("      <Area type=`"Bog`" name=`"{=iXA5SttU}Bog`" />")
            [void]$sb.AppendLine("    </CommonAreas>")
            [void]$sb.AppendLine("  </Settlement>")
        }
    }

    # Handle orphaned castle_villages (parent castle not in scene)
    $allCvInRegion = $regionEntities | Where-Object { $_.Type -eq 'castle_village' }
    $boundCastleIds = @($castleEntities | ForEach-Object { $_.Id })
    foreach ($cv in $allCvInRegion) {
        $parentId = Get-ParentId $cv.Id $cv.Type
        if (-not $parentId -or $parentId -in $boundCastleIds) { continue }

        # Try to find the parent castle in the full entity list (it may exist but wasn't matched)
        $resolvedParent = $parentId
        if (-not $sceneEntities.ContainsKey($parentId)) {
            Write-Warning "Orphaned castle_village $($cv.Id) - parent $parentId not in scene. Skipping."
            continue
        }
        Write-Warning "Orphaned castle_village $($cv.Id) - parent $parentId not processed as castle. Generating with binding."

        $cvId = $cv.Id
        $cvExisting = $existingMap[$cvId]
        if ($cvExisting) {
            $carryOverCount++
            $cvName = Xml-Escape $cvExisting.name
            $cvCulture = Xml-Escape $cvExisting.culture
            $cvText = if ($cvExisting.text) { " text=`"$(Xml-Escape $cvExisting.text)`"" } else { "" }
            $vComp = $cvExisting.Components.Village
            $vType = if ($vComp.village_type) { $vComp.village_type } else { $VillageTypes[$vtCounter++ % $VillageTypes.Count] }
            $hearth = if ($vComp.hearth) { $vComp.hearth } else { "350" }
            $vGateRot = if ($vComp.gate_rotation) { " gate_rotation=`"$($vComp.gate_rotation)`"" } else { "" }
            $vBgCrop = if ($vComp.background_crop_position) { $vComp.background_crop_position } else { "0.0" }
            $vBgMesh = if ($vComp.background_mesh) { $vComp.background_mesh } else { $sd.BgVillage }
            $vWaitMesh = if ($vComp.wait_mesh) { $vComp.wait_mesh } else { $sd.WaitVill }
            $vCastleBg = if ($vComp.castle_background_mesh) { $vComp.castle_background_mesh } else { $sd.BgCastle }
            $vCenterLoc = $cvExisting.Locations.Location | Where-Object { $_.id -eq 'village_center' }
            $vScene = if ($vCenterLoc.scene_name) { $vCenterLoc.scene_name } else { Get-CycledScene $VillageScenes[$sceneBase] $sceneBase 'village' }
            $maxPros = if ($vCenterLoc.max_prosperity) { " max_prosperity=`"$($vCenterLoc.max_prosperity)`"" } else { "" }
        } else {
            $newCount++
            $cvName = "{=Settlements.Settlement.name.$cvId}Castle Village $($cvId -replace 'castle_village_','')"
            $cvCulture = $culture
            $cvText = ""
            $vType = $VillageTypes[$vtCounter++ % $VillageTypes.Count]
            $hearth = "350"; $vGateRot = ""; $vBgCrop = "0.0"
            $vBgMesh = $sd.BgVillage; $vWaitMesh = $sd.WaitVill; $vCastleBg = $sd.BgCastle
            $vScene = Get-CycledScene $VillageScenes[$sceneBase] $sceneBase 'village'
            $maxPros = ""
        }
        [void]$sb.AppendLine("  <Settlement id=`"$cvId`" name=`"$cvName`" posX=`"$($cv.PosX)`" posY=`"$($cv.PosY)`" culture=`"$cvCulture`"$cvText>")
        [void]$sb.AppendLine("    <Components>")
        [void]$sb.AppendLine("      <Village id=`"castle_village_comp_$($cvId -replace 'castle_village_','')`" village_type=`"$vType`" hearth=`"$hearth`"$vGateRot bound=`"Settlement.$resolvedParent`" background_crop_position=`"$vBgCrop`" background_mesh=`"$vBgMesh`" wait_mesh=`"$vWaitMesh`" castle_background_mesh=`"$vCastleBg`" />")
        [void]$sb.AppendLine("    </Components>")
        [void]$sb.AppendLine("    <Locations complex_template=`"LocationComplexTemplate.village_complex`">")
        [void]$sb.AppendLine("      <Location id=`"village_center`" scene_name=`"$vScene`"$maxPros />")
        [void]$sb.AppendLine("    </Locations>")
        [void]$sb.AppendLine("    <CommonAreas>")
        [void]$sb.AppendLine("      <Area type=`"Pasture`" name=`"{=fOUsLdZR}Pasture`" />")
        [void]$sb.AppendLine("      <Area type=`"Thicket`" name=`"{=66Mzk0NZ}Thicket`" />")
        [void]$sb.AppendLine("      <Area type=`"Bog`" name=`"{=iXA5SttU}Bog`" />")
        [void]$sb.AppendLine("    </CommonAreas>")
        [void]$sb.AppendLine("  </Settlement>")
    }

    # Handle orphaned villages (parent town not in scene - try castle fallback)
    $allVInRegion = $regionEntities | Where-Object { $_.Type -eq 'village' }
    $boundTownIds = @($townEntities | ForEach-Object { $_.Id })
    foreach ($v in $allVInRegion) {
        $parentId = Get-ParentId $v.Id $v.Type
        if (-not $parentId -or $parentId -in $boundTownIds) { continue }

        # Try fallback: village_A15_1 -> town_A15 doesn't exist, try castle_A15
        $castleFallback = $parentId -replace '^town_', 'castle_'
        # Also try stripping the number: village_DG1_1 -> town_DG1 -> town_DG
        $noNumberFallback = $parentId -replace '(?<=[A-Z])\d$', ''
        if ($noNumberFallback -eq $parentId) { $noNumberFallback = $null }

        $resolvedParent = $null
        if ($sceneEntities.ContainsKey($parentId)) {
            $resolvedParent = $parentId
        } elseif ($sceneEntities.ContainsKey($castleFallback)) {
            $resolvedParent = $castleFallback
            Write-Warning "Village $($v.Id) - parent $parentId not found. Binding to castle fallback: $castleFallback"
        } elseif ($noNumberFallback -and $sceneEntities.ContainsKey($noNumberFallback)) {
            $resolvedParent = $noNumberFallback
            Write-Warning "Village $($v.Id) - parent $parentId not found. Binding to numberless fallback: $noNumberFallback"
        } else {
            Write-Warning "Orphaned village $($v.Id) - parent $parentId not in scene and no fallback found. Skipping."
            continue
        }

        $vId = $v.Id
        $vExisting = $existingMap[$vId]
        if ($vExisting) {
            $carryOverCount++
            $vName = Xml-Escape $vExisting.name
            $vCulture = Xml-Escape $vExisting.culture
            $vText = if ($vExisting.text) { " text=`"$(Xml-Escape $vExisting.text)`"" } else { "" }
            $vComp = $vExisting.Components.Village
            $vType = if ($vComp.village_type) { $vComp.village_type } else { $VillageTypes[$vtCounter++ % $VillageTypes.Count] }
            $hearth = if ($vComp.hearth) { $vComp.hearth } else { "350" }
            $vGateRot = if ($vComp.gate_rotation) { " gate_rotation=`"$($vComp.gate_rotation)`"" } else { "" }
            $vBgCrop = if ($vComp.background_crop_position) { $vComp.background_crop_position } else { "0.0" }
            $vBgMesh = if ($vComp.background_mesh) { $vComp.background_mesh } else { $sd.BgVillage }
            $vWaitMesh = if ($vComp.wait_mesh) { $vComp.wait_mesh } else { $sd.WaitVill }
            $vCastleBg = if ($vComp.castle_background_mesh) { $vComp.castle_background_mesh } else { $sd.BgCastle }
            $vCenterLoc = $vExisting.Locations.Location | Where-Object { $_.id -eq 'village_center' }
            $vScene = if ($vCenterLoc.scene_name) { $vCenterLoc.scene_name } else { Get-CycledScene $VillageScenes[$sceneBase] $sceneBase 'village' }
            $maxPros = if ($vCenterLoc.max_prosperity) { " max_prosperity=`"$($vCenterLoc.max_prosperity)`"" } else { "" }
        } else {
            $newCount++
            $vName = "{=Settlements.Settlement.name.$vId}Village $($vId -replace 'village_','')"
            $vCulture = $culture
            $vText = ""
            $vType = $VillageTypes[$vtCounter++ % $VillageTypes.Count]
            $hearth = "350"; $vGateRot = ""; $vBgCrop = "0.0"
            $vBgMesh = $sd.BgVillage; $vWaitMesh = $sd.WaitVill; $vCastleBg = $sd.BgCastle
            $vScene = Get-CycledScene $VillageScenes[$sceneBase] $sceneBase 'village'
            $maxPros = ""
        }
        [void]$sb.AppendLine("  <Settlement id=`"$vId`" name=`"$vName`" posX=`"$($v.PosX)`" posY=`"$($v.PosY)`" culture=`"$vCulture`"$vText>")
        [void]$sb.AppendLine("    <Components>")
        [void]$sb.AppendLine("      <Village id=`"village_comp_$($vId -replace 'village_','')`" village_type=`"$vType`" hearth=`"$hearth`"$vGateRot bound=`"Settlement.$resolvedParent`" background_crop_position=`"$vBgCrop`" background_mesh=`"$vBgMesh`" wait_mesh=`"$vWaitMesh`" castle_background_mesh=`"$vCastleBg`" />")
        [void]$sb.AppendLine("    </Components>")
        [void]$sb.AppendLine("    <Locations complex_template=`"LocationComplexTemplate.village_complex`">")
        [void]$sb.AppendLine("      <Location id=`"village_center`" scene_name=`"$vScene`"$maxPros />")
        [void]$sb.AppendLine("    </Locations>")
        [void]$sb.AppendLine("    <CommonAreas>")
        [void]$sb.AppendLine("      <Area type=`"Pasture`" name=`"{=fOUsLdZR}Pasture`" />")
        [void]$sb.AppendLine("      <Area type=`"Thicket`" name=`"{=66Mzk0NZ}Thicket`" />")
        [void]$sb.AppendLine("      <Area type=`"Bog`" name=`"{=iXA5SttU}Bog`" />")
        [void]$sb.AppendLine("    </CommonAreas>")
        [void]$sb.AppendLine("  </Settlement>")
    }
}

# Check for unprocessed regions
$allRegions = $entityList | ForEach-Object { $_.Region } | Sort-Object -Unique
foreach ($r in $allRegions) {
    if (-not $processedRegions.ContainsKey($r) -and $null -ne $r) {
        Write-Warning "Region '$r' has entities but was not in processing order!"
    }
}

# ============================================================
# Carry over hideouts and special settlements from existing file
# ============================================================
$specialPrefixes = @('hideout_', 'retirement_')
$specialCount = 0
[void]$sb.AppendLine("  <!-- ============================================== -->")
[void]$sb.AppendLine("  <!-- HIDEOUTS AND SPECIAL SETTLEMENTS -->")
[void]$sb.AppendLine("  <!-- ============================================== -->")
foreach ($sett in $existingXml.Settlements.Settlement) {
    $sid = $sett.id
    $isSpecial = $false
    foreach ($prefix in $specialPrefixes) {
        if ($sid.StartsWith($prefix)) { $isSpecial = $true; break }
    }
    if (-not $isSpecial) { continue }

    # Write the raw outer XML of this settlement element
    [void]$sb.AppendLine("  $($sett.OuterXml)")
    $specialCount++
}
Write-Host "  Carried over special settlements (hideouts/retirement): $specialCount"

[void]$sb.AppendLine('</Settlements>')

# ============================================================
# STEP 4: Write output
# ============================================================
$outputPath = Join-Path $PSScriptRoot "..\$OutputFile"
$outputDir = Split-Path $outputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

[System.IO.File]::WriteAllText($outputPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($true))

Write-Host "`nOutput written to: $outputPath" -ForegroundColor Green
Write-Host "  Carried over: $carryOverCount settlements"
Write-Host "  New (placeholder): $newCount settlements"
Write-Host "  Total: $($carryOverCount + $newCount) settlements"

# Quick validation
try {
    [xml]$validate = Get-Content $outputPath -Encoding UTF8
    $settCount = $validate.Settlements.Settlement.Count
    Write-Host "`nXML Validation: PASSED ($settCount Settlement elements)" -ForegroundColor Green
}
catch {
    Write-Host "`nXML Validation: FAILED - $($_.Exception.Message)" -ForegroundColor Red
}
