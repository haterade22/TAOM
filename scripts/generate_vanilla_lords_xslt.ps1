# Generate lords.xslt from LOTRAOM data for vanilla lords only
param(
    [string]$VanillaLordsPath = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\lords.xml",
    [string]$LotraomLordsPath = "E:\LOTRAOMAssets\LOTRAOM_Jan_1_Patreon\Modules\LOTRAOM\ModuleData\lords.xml",
    [string]$OutputPath = "c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lords.xslt"
)

$vanillaLordsXml = [xml](Get-Content $VanillaLordsPath)
$lotraomLordsXml = [xml](Get-Content $LotraomLordsPath)

# Get all vanilla lord IDs matching lord_X_Y pattern (preserve order)
$vanillaLordIds = $vanillaLordsXml.NPCCharacters.NPCCharacter | Where-Object { $_.id -match "^lord_\d+_\d+$" } | ForEach-Object { $_.id }
Write-Host "Found $($vanillaLordIds.Count) vanilla lords"

# Build hashtable of LOTRAOM lords for quick lookup
$lotraomLords = @{}
foreach ($lord in $lotraomLordsXml.NPCCharacters.NPCCharacter) {
    if ($lord.id -match "^lord_\d+_\d+$") {
        $lotraomLords[$lord.id] = $lord
    }
}
Write-Host "Found $($lotraomLords.Count) LOTRAOM lords"

# Start building XSLT content
$sb = New-Object System.Text.StringBuilder

[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">')
[void]$sb.AppendLine('    <xsl:output method="xml" indent="yes" encoding="utf-8"/>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Identity transformation - copies everything by default -->')
[void]$sb.AppendLine('    <xsl:template match="@*|node()">')
[void]$sb.AppendLine('        <xsl:copy>')
[void]$sb.AppendLine('            <xsl:apply-templates select="@*|node()"/>')
[void]$sb.AppendLine('        </xsl:copy>')
[void]$sb.AppendLine('    </xsl:template>')

$currentFaction = ""
$factionNames = @{
    "1" = "Empire/Dunland"
    "2" = "Sturgia/Misty Mountains"
    "3" = "Aserai/Harad"
    "4" = "Vlandia/Rohan"
    "5" = "Battania/Woodland Realm"
    "6" = "Khuzait/Rhun"
}

$processedCount = 0

foreach ($lordId in $vanillaLordIds) {
    if (-not $lotraomLords.ContainsKey($lordId)) {
        Write-Host "Skipping $lordId (not in LOTRAOM)"
        continue
    }

    $lord = $lotraomLords[$lordId]
    $processedCount++

    # Extract faction number from lord ID
    if ($lordId -match "^lord_(\d+)_") {
        $faction = $matches[1]
        if ($faction -ne $currentFaction) {
            $currentFaction = $faction
            $factionName = $factionNames[$faction]
            if (-not $factionName) { $factionName = "Faction $faction" }
            [void]$sb.AppendLine('')
            [void]$sb.AppendLine('')
            [void]$sb.AppendLine("    <!-- ============================================== -->")
            [void]$sb.AppendLine("    <!-- Faction $faction - $factionName -->")
            [void]$sb.AppendLine("    <!-- ============================================== -->")
        }
    }

    # Extract lord attributes
    $name = $lord.name
    $defaultGroup = if ($lord.default_group) { $lord.default_group } else { "Infantry" }
    $isFemale = $lord.is_female

    # Extract BodyProperties
    $bodyProps = $lord.face.BodyProperties
    $version = if ($bodyProps.version) { $bodyProps.version } else { "4" }
    $weight = $bodyProps.weight
    $build = $bodyProps.build
    $key = $bodyProps.key

    # Build BodyProperties attributes string
    $bodyPropsAttrs = "version=`"$version`""
    if ($weight) { $bodyPropsAttrs += " weight=`"$weight`"" }
    if ($build) { $bodyPropsAttrs += " build=`"$build`"" }
    $bodyPropsAttrs += " key=`"$key`""

    # Build skills XML
    $skillsLines = @()
    if ($lord.skills -and $lord.skills.skill) {
        foreach ($skill in $lord.skills.skill) {
            $skillsLines += "                <skill id=`"$($skill.id)`" value=`"$($skill.value)`"/>"
        }
    }

    # Build traits XML
    $traitsLines = @()
    if ($lord.Traits -and $lord.Traits.Trait) {
        foreach ($trait in $lord.Traits.Trait) {
            $traitsLines += "                <Trait id=`"$($trait.id)`" value=`"$($trait.value)`"/>"
        }
    }

    # Determine which attributes to exclude
    $excludeAttrs = "local-name() != 'name' and local-name() != 'default_group'"
    if ($isFemale -eq "true") {
        $excludeAttrs += " and local-name() != 'is_female'"
    }

    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("    <!-- $lordId -->")
    [void]$sb.AppendLine("    <xsl:template match=`"NPCCharacter[@id='$lordId']`">")
    [void]$sb.AppendLine("        <xsl:copy>")
    [void]$sb.AppendLine("            <xsl:apply-templates select=`"@*[$excludeAttrs]`"/>")
    [void]$sb.AppendLine("            <xsl:attribute name=`"name`">$name</xsl:attribute>")
    [void]$sb.AppendLine("            <xsl:attribute name=`"default_group`">$defaultGroup</xsl:attribute>")
    if ($isFemale -eq "true") {
        [void]$sb.AppendLine("            <xsl:attribute name=`"is_female`">true</xsl:attribute>")
    }
    [void]$sb.AppendLine("            <face>")
    [void]$sb.AppendLine("                <BodyProperties $bodyPropsAttrs/>")
    [void]$sb.AppendLine("            </face>")
    [void]$sb.AppendLine("            <skills>")
    foreach ($skillLine in $skillsLines) {
        [void]$sb.AppendLine($skillLine)
    }
    [void]$sb.AppendLine("            </skills>")
    [void]$sb.AppendLine("            <Traits>")
    foreach ($traitLine in $traitsLines) {
        [void]$sb.AppendLine($traitLine)
    }
    [void]$sb.AppendLine("            </Traits>")
    [void]$sb.AppendLine("            <xsl:apply-templates select=`"node()[not(self::face or self::skills or self::Traits)]`"/>")
    [void]$sb.AppendLine("        </xsl:copy>")
    [void]$sb.AppendLine("    </xsl:template>")
}

[void]$sb.AppendLine('')
[void]$sb.AppendLine("</xsl:stylesheet>")

# Write to file with UTF-8 BOM
$content = $sb.ToString()
$utf8Bom = New-Object System.Text.UTF8Encoding $true
[System.IO.File]::WriteAllText($OutputPath, $content, $utf8Bom)

Write-Host "Generated lords.xslt with $processedCount lord templates"
Write-Host "Output: $OutputPath"
