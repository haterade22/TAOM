# Remove generic militia troops (spearman, archer, veteran_spearman, veteran_archer)
# from all troop files, party templates, and culture definitions.
# KEEPS: gondor_militiaman, rohan_westfold_militiaman, rohan_edoras_militia, harad_militia, easterling_militia

$ErrorActionPreference = 'Stop'
$root = "C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData"

# Culture mapping: culture => [troop_file, militia_prefix, basic_troop, elite_basic_troop]
$cultures = @{
    erebor    = @{ file="troops\troops_erebor.xml";      prefix="erebor";    basic="erebor_recruit";           elite="erebor_warrior" }
    rivendell = @{ file="troops\troops_rivendell.xml";   prefix="rivendell"; basic="imladris_recruit";         elite="imladris_infantry" }
    mirkwood  = @{ file="troops\troops_mirkwood.xml";    prefix="mirkwood";  basic="mirkwood_recruit";         elite="mirkwood_woodsman" }
    isengard  = @{ file="troops\troops_isengard.xml";    prefix="isengard";  basic="urukhai_recruit";          elite="urukhai_scout" }
    gundabad  = @{ file="troops\troops_gundabad.xml";    prefix="gundabad";  basic="gundabad_snaga";           elite="gundabad_fighter" }
    dolguldur = @{ file="troops\troops_dolguldur.xml";   prefix="dolguldur"; basic="dg_goblin_slave";          elite="dg_uruk_warrior" }
    gondor    = @{ file="troops\troops_gondor.xml";      prefix="gondor";    basic="gondor_levyman";           elite="gondor_footman" }
    mordor    = @{ file="troops\troops_mordor.xml";      prefix="mordor";    basic="mordor_uruk_grunt";        elite="mordor_uruk_warrior" }
    dunland   = @{ file="troops\troops_dunland.xml";     prefix="dunland";   basic="dunland_peasant";          elite="dunland_noble_son" }
    harad     = @{ file="troops\troops_harad.xml";       prefix="harad";     basic="harad_levy";               elite="harad_noble" }
    rohan     = @{ file="troops\troops_rohan.xml";       prefix="rohan";     basic="rohan_edoras_recruit";     elite="rohan_edoras_golden_hall_rider" }
    rhun      = @{ file="troops\troops_rhun_new.xml";    prefix="rhun";      basic="easterling_recruit";       elite="easterling_cavalry_new" }
}

$totalRemoved = 0

foreach ($name in $cultures.Keys | Sort-Object) {
    $c = $cultures[$name]
    $filePath = Join-Path $root $c.file
    $content = [System.IO.File]::ReadAllText($filePath)
    $prefix = $c.prefix

    # Remove the 4 militia NPCCharacter elements: militia_spearman, militia_archer, militia_veteran_spearman, militia_veteran_archer
    $suffixes = @("militia_spearman", "militia_archer", "militia_veteran_spearman", "militia_veteran_archer")
    $removed = 0

    foreach ($suffix in $suffixes) {
        $id = "${prefix}_${suffix}"
        # Match the full NPCCharacter element including leading whitespace and trailing newlines
        $pattern = "(?m)^\s*<NPCCharacter\s[^>]*\bid=`"$id`"[^>]*>[\s\S]*?</NPCCharacter>\s*\r?\n?"
        if ($content -cmatch $pattern) {
            $content = $content -creplace $pattern, ""
            $removed++
            Write-Host "  Removed $id from $($c.file)"
        } else {
            Write-Warning "  NOT FOUND: $id in $($c.file)"
        }
    }

    [System.IO.File]::WriteAllText($filePath, $content)
    $totalRemoved += $removed
    Write-Host "$name : removed $removed troops"
}

Write-Host "`nTotal troops removed from troop files: $totalRemoved"

# --- Update party templates ---
Write-Host "`n--- Updating party templates ---"
$ptFile = Join-Path $root "taom_partyTemplates.xml"
$ptContent = [System.IO.File]::ReadAllText($ptFile)

foreach ($name in $cultures.Keys | Sort-Object) {
    $c = $cultures[$name]
    $prefix = $c.prefix
    $basic = $c.basic
    
    # Replace militia_spearman reference in party template
    $ptContent = $ptContent -creplace "NPCCharacter\.${prefix}_militia_spearman", "NPCCharacter.$basic"
    # Replace militia_archer reference in party template
    $ptContent = $ptContent -creplace "NPCCharacter\.${prefix}_militia_archer", "NPCCharacter.$basic"
}

[System.IO.File]::WriteAllText($ptFile, $ptContent)
Write-Host "Updated militia party templates to use basic troops"

# --- Update taom_spcultures.xml ---
Write-Host "`n--- Updating taom_spcultures.xml ---"
$scFile = Join-Path $root "taom_spcultures.xml"
$scContent = [System.IO.File]::ReadAllText($scFile)

# Map of militia troop prefixes used in taom_spcultures.xml
$spCultureMilitia = @{
    erebor    = @{ prefix="erebor";    basic="erebor_recruit";      elite="erebor_warrior" }
    rivendell = @{ prefix="rivendell"; basic="imladris_recruit";    elite="imladris_infantry" }
    mirkwood  = @{ prefix="mirkwood";  basic="mirkwood_recruit";    elite="mirkwood_woodsman" }
    imperial  = @{ prefix="imperial";  basic="imperial_recruit";    elite="imperial_vigla_recruit" }
    isengard  = @{ prefix="isengard";  basic="urukhai_recruit";     elite="urukhai_scout" }
    gundabad  = @{ prefix="gundabad";  basic="gundabad_snaga";      elite="gundabad_fighter" }
    aserai    = @{ prefix="aserai";    basic="aserai_recruit";      elite="umbar_elite" }
    dolguldur = @{ prefix="dolguldur"; basic="dg_goblin_slave";     elite="dg_uruk_warrior" }
    gondor    = @{ prefix="gondor";    basic="gondor_levyman";      elite="gondor_footman" }
    mordor    = @{ prefix="mordor";    basic="mordor_uruk_grunt";   elite="mordor_uruk_warrior" }
}

foreach ($key in $spCultureMilitia.Keys) {
    $m = $spCultureMilitia[$key]
    $p = $m.prefix
    $scContent = $scContent -creplace "NPCCharacter\.${p}_militia_spearman",         "NPCCharacter.$($m.basic)"
    $scContent = $scContent -creplace "NPCCharacter\.${p}_militia_archer",           "NPCCharacter.$($m.basic)"
    $scContent = $scContent -creplace "NPCCharacter\.${p}_militia_veteran_spearman", "NPCCharacter.$($m.elite)"
    $scContent = $scContent -creplace "NPCCharacter\.${p}_militia_veteran_archer",   "NPCCharacter.$($m.elite)"
}

[System.IO.File]::WriteAllText($scFile, $scContent)
Write-Host "Updated taom_spcultures.xml militia attributes"

# --- Update spcultures.xslt ---
Write-Host "`n--- Updating spcultures.xslt ---"
$xsltFile = Join-Path $root "spcultures.xslt"
$xsltContent = [System.IO.File]::ReadAllText($xsltFile)

$xsltMilitia = @{
    dunland = @{ prefix="dunland"; basic="dunland_peasant";       elite="dunland_noble_son" }
    harad   = @{ prefix="harad";   basic="harad_levy";            elite="harad_noble" }
    rohan   = @{ prefix="rohan";   basic="rohan_edoras_recruit";  elite="rohan_edoras_golden_hall_rider" }
    rhun    = @{ prefix="rhun";    basic="easterling_recruit";    elite="easterling_cavalry_new" }
}

foreach ($key in $xsltMilitia.Keys) {
    $m = $xsltMilitia[$key]
    $p = $m.prefix
    $xsltContent = $xsltContent -creplace "NPCCharacter\.${p}_militia_spearman",         "NPCCharacter.$($m.basic)"
    $xsltContent = $xsltContent -creplace "NPCCharacter\.${p}_militia_archer",           "NPCCharacter.$($m.basic)"
    $xsltContent = $xsltContent -creplace "NPCCharacter\.${p}_militia_veteran_spearman", "NPCCharacter.$($m.elite)"
    $xsltContent = $xsltContent -creplace "NPCCharacter\.${p}_militia_veteran_archer",   "NPCCharacter.$($m.elite)"
}

[System.IO.File]::WriteAllText($xsltFile, $xsltContent)
Write-Host "Updated spcultures.xslt militia attributes"

Write-Host "`nDone! Removed $totalRemoved militia troop definitions and updated all references."
