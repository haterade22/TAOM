# add-face-tags.ps1
# Adds hair_tags, beard_tags, and tattoo_tags to all lords in characters/lords.xml

param(
    [string]$InputFile = "Main/_Module/ModuleData/characters/lords.xml",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Culture to tag mapping
$cultureTagMap = @{
    "aserai"     = "aserai"
    "battania"   = "battania"
    "dolguldur"  = "empire"      # Orcs use empire styling
    "empire"     = "empire"
    "erebor"     = "sturgia"     # Dwarves use sturgia styling
    "gondor"     = "empire"
    "gundabad"   = "battania"    # Goblins use battania styling
    "isengard"   = "empire"      # Uruk-hai use empire styling
    "khuzait"    = "khuzait"
    "lothlorien" = "battania"    # Elves use battania styling
    "mirkwood"   = "battania"    # Elves use battania styling
    "mordor"     = "empire"
    "rivendell"  = "battania"    # Elves use battania styling
    "sturgia"    = "sturgia"
    "umbar"      = "aserai"      # Corsairs use aserai styling
    "vlandia"    = "vlandia"
}

# Load XML
$xmlPath = Join-Path (Join-Path $PSScriptRoot "..") $InputFile
$xmlPath = Resolve-Path $xmlPath
Write-Host "Loading $xmlPath..."

[xml]$xml = Get-Content $xmlPath -Encoding UTF8

$updatedCount = 0
$skippedCount = 0

foreach ($npc in $xml.NPCCharacters.NPCCharacter) {
    $id = $npc.id
    $culture = $npc.culture -replace "Culture\.", ""
    $isFemale = $npc.is_female -eq "true"

    # Get the tag for this culture
    $tag = $cultureTagMap[$culture]
    if (-not $tag) {
        Write-Warning "Unknown culture '$culture' for $id, using 'empire'"
        $tag = "empire"
    }

    # Check if face element exists
    $faceNode = $npc.face
    if (-not $faceNode) {
        Write-Warning "No face element for $id, skipping"
        $skippedCount++
        continue
    }

    # Check if hair_tags already exists
    if ($faceNode.hair_tags) {
        Write-Host "  $id already has hair_tags, skipping"
        $skippedCount++
        continue
    }

    # Create hair_tags element
    $hairTags = $xml.CreateElement("hair_tags")
    $hairTag = $xml.CreateElement("hair_tag")
    $hairTag.SetAttribute("name", $tag)
    $hairTags.AppendChild($hairTag) | Out-Null

    # Create beard_tags element (only for males)
    $beardTags = $null
    if (-not $isFemale) {
        $beardTags = $xml.CreateElement("beard_tags")
        $beardTag = $xml.CreateElement("beard_tag")
        $beardTag.SetAttribute("name", $tag)
        $beardTags.AppendChild($beardTag) | Out-Null
    }

    # Create tattoo_tags element
    $tattooTags = $xml.CreateElement("tattoo_tags")
    $tattooTag = $xml.CreateElement("tattoo_tag")
    $tattooTag.SetAttribute("name", "Cleanface")
    $tattooTags.AppendChild($tattooTag) | Out-Null

    # Append to face element after BodyProperties
    $faceNode.AppendChild($hairTags) | Out-Null
    if ($beardTags) {
        $faceNode.AppendChild($beardTags) | Out-Null
    }
    $faceNode.AppendChild($tattooTags) | Out-Null

    $genderStr = if ($isFemale) { "female" } else { "male" }
    Write-Host "  Updated $id ($culture, $genderStr) -> tag: $tag"
    $updatedCount++
}

Write-Host ""
Write-Host "Summary:"
Write-Host "  Updated: $updatedCount"
Write-Host "  Skipped: $skippedCount"

if (-not $DryRun) {
    # Save XML with proper formatting
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = "    "
    $settings.NewLineChars = "`r`n"
    $settings.Encoding = [System.Text.Encoding]::UTF8

    $writer = [System.Xml.XmlWriter]::Create($xmlPath, $settings)
    $xml.Save($writer)
    $writer.Close()

    Write-Host ""
    Write-Host "Saved to $xmlPath"
} else {
    Write-Host ""
    Write-Host "[DRY RUN] No changes saved"
}
