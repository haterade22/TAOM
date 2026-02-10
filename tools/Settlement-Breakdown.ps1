$xml = [xml](Get-Content 'c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml')
$settlements = $xml.Settlements.Settlement

$towns = @(); $castles = @(); $villages = @(); $castleVillages = @(); $hideouts = @(); $other = @()
foreach ($s in $settlements) {
    $id = $s.id
    if ($id.StartsWith('hideout_')) { $hideouts += $s; continue }
    if ($id.StartsWith('retirement_')) { $other += $s; continue }
    if ($id.StartsWith('castle_village_')) { $castleVillages += $s; continue }
    if ($id.StartsWith('village_')) { $villages += $s; continue }
    if ($id.StartsWith('castle_')) { $castles += $s; continue }
    if ($id.StartsWith('town_')) { $towns += $s; continue }
    $other += $s
}

function Get-Region($id, $prefix) {
    $rest = $id.Substring($prefix.Length)
    if ($rest -cmatch '^([A-Z]+)') { return $matches[1] }
    if ($rest -match '^(isengard|orthanc)') { return 'I' }
    return '?'
}

Write-Host "=== TOWNS ($($towns.Count)) ==="
$townGroups = $towns | ForEach-Object { [PSCustomObject]@{ Region = (Get-Region $_.id 'town_'); Id = $_.id } } | Group-Object Region | Sort-Object Name
foreach ($g in $townGroups) { Write-Host ("  {0} ({1})" -f $g.Name, $g.Count) }
Write-Host ''

Write-Host "=== CASTLES ($($castles.Count)) ==="
$castleGroups = $castles | ForEach-Object { [PSCustomObject]@{ Region = (Get-Region $_.id 'castle_'); Id = $_.id } } | Group-Object Region | Sort-Object Name
foreach ($g in $castleGroups) { Write-Host ("  {0} ({1})" -f $g.Name, $g.Count) }
Write-Host ''

Write-Host "=== VILLAGES ($($villages.Count)) ==="
$villageGroups = $villages | ForEach-Object { [PSCustomObject]@{ Region = (Get-Region $_.id 'village_'); Id = $_.id } } | Group-Object Region | Sort-Object Name
foreach ($g in $villageGroups) { Write-Host ("  {0} ({1})" -f $g.Name, $g.Count) }
Write-Host ''

Write-Host "=== CASTLE VILLAGES ($($castleVillages.Count)) ==="
$cvGroups = $castleVillages | ForEach-Object { [PSCustomObject]@{ Region = (Get-Region $_.id 'castle_village_'); Id = $_.id } } | Group-Object Region | Sort-Object Name
foreach ($g in $cvGroups) { Write-Host ("  {0} ({1})" -f $g.Name, $g.Count) }
Write-Host ''

Write-Host '=== SUMMARY ==='
Write-Host ("  Towns:           {0}" -f $towns.Count)
Write-Host ("  Castles:         {0}" -f $castles.Count)
Write-Host ("  Villages:        {0}" -f $villages.Count)
Write-Host ("  Castle Villages: {0}" -f $castleVillages.Count)
Write-Host ("  Hideouts:        {0}" -f $hideouts.Count)
Write-Host ("  Other:           {0}" -f $other.Count)
Write-Host ("  TOTAL:           {0}" -f $settlements.Count)
