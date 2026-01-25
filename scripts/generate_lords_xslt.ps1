# Script to generate missing lord templates for lords.xslt
# Reads from LOTRAOM lords.xml and generates XSLT templates

$lotraomLordsPath = "E:\LOTRAOMAssets\LOTRAOM_Jan_1_Patreon\Modules\LOTRAOM\ModuleData\lords.xml"
$taomXsltPath = "c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lords.xslt"
$outputPath = "c:\Users\mikew\source\repos\TAOM\scripts\new_lords_templates.xml"

# Read LOTRAOM lords.xml
Write-Host "Reading LOTRAOM lords.xml..."
[xml]$lotraomXml = Get-Content $lotraomLordsPath -Raw

# Read current TAOM lords.xslt
Write-Host "Reading current lords.xslt..."
$taomXslt = Get-Content $taomXsltPath -Raw

# Extract existing lord IDs from XSLT
$existingLords = [System.Collections.Generic.HashSet[string]]::new()
$existingMatches = [regex]::Matches($taomXslt, "match=`"NPCCharacter\[@id='(lord_[^']+)'")
foreach ($match in $existingMatches) {
    [void]$existingLords.Add($match.Groups[1].Value)
}
Write-Host "Found $($existingLords.Count) existing lords in XSLT"

# Get all lords from LOTRAOM
$allLords = $lotraomXml.NPCCharacters.NPCCharacter | Where-Object { $_.id -like "lord_*" }
Write-Host "Found $($allLords.Count) lords in LOTRAOM"

# Find missing lords
$missingLords = $allLords | Where-Object { -not $existingLords.Contains($_.id) }
Write-Host "Found $($missingLords.Count) missing lords"

# Generate templates
$sb = [System.Text.StringBuilder]::new()

foreach ($lord in $missingLords) {
    $lordId = $lord.id
    $name = $lord.name
    $defaultGroup = if ($lord.default_group) { $lord.default_group } else { "Infantry" }
    $isFemale = if ($lord.is_female -eq "true") { "true" } else { "false" }

    # Get face/BodyProperties
    $weight = "0.5"
    $build = "0.5"
    $key = ""
    if ($lord.face -and $lord.face.BodyProperties) {
        $bp = $lord.face.BodyProperties
        if ($bp.weight) { $weight = $bp.weight }
        if ($bp.build) { $build = $bp.build }
        if ($bp.key) { $key = $bp.key }
    }

    # Get skills
    $skillsXml = ""
    if ($lord.skills -and $lord.skills.skill) {
        $skillsSb = [System.Text.StringBuilder]::new()
        [void]$skillsSb.AppendLine("            <skills>")
        foreach ($skill in $lord.skills.skill) {
            [void]$skillsSb.AppendLine("                <skill id=`"$($skill.id)`" value=`"$($skill.value)`"/>")
        }
        [void]$skillsSb.AppendLine("            </skills>")
        $skillsXml = $skillsSb.ToString()
    }

    # Get traits
    $traitsXml = ""
    if ($lord.Traits -and $lord.Traits.Trait) {
        $traitsSb = [System.Text.StringBuilder]::new()
        [void]$traitsSb.AppendLine("            <Traits>")
        foreach ($trait in $lord.Traits.Trait) {
            [void]$traitsSb.AppendLine("                <Trait id=`"$($trait.id)`" value=`"$($trait.value)`"/>")
        }
        [void]$traitsSb.AppendLine("            </Traits>")
        $traitsXml = $traitsSb.ToString()
    }

    # Build template - note we're using a simplified pattern
    [void]$sb.AppendLine(@"
    <!-- $lordId -->
    <xsl:template match="NPCCharacter[@id='$lordId']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">$name</xsl:attribute>
            <xsl:attribute name="default_group">$defaultGroup</xsl:attribute>
            <xsl:attribute name="is_female">$isFemale</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="$weight" build="$build" key="$key"/>
            </face>
$skillsXml$traitsXml            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
"@)
}

# Output to file
$sb.ToString() | Out-File $outputPath -Encoding UTF8
Write-Host "Generated templates saved to: $outputPath"

# Also create a list of missing lord IDs for verification
$missingIds = $missingLords | ForEach-Object { $_.id }
$missingIds | Out-File "c:\Users\mikew\source\repos\TAOM\scripts\missing_lord_ids.txt" -Encoding UTF8
Write-Host "Missing lord IDs saved to: missing_lord_ids.txt"
