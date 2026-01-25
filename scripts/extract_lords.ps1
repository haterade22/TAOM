# PowerShell script to extract specific lords from LOTRAOM lords.xml
# and create a new lords.xml file for TAOM

param(
    [string]$LordIdsFile = "c:\Users\mikew\source\repos\TAOM\scripts\missing_lord_ids.txt",
    [string]$SourceXmlFile = "E:\LOTRAOMAssets\LOTRAOM_Jan_1_Patreon\Modules\LOTRAOM\ModuleData\lords.xml",
    [string]$OutputFile = "c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\lords.xml"
)

Write-Host "Reading lord IDs from: $LordIdsFile"
$lordIds = Get-Content $LordIdsFile | Where-Object { $_.Trim() -ne "" } | ForEach-Object { $_.Trim() }
Write-Host "Found $($lordIds.Count) lord IDs to extract"

Write-Host "Loading source XML from: $SourceXmlFile"
[xml]$sourceXml = Get-Content $SourceXmlFile -Encoding UTF8

# Create output XML structure
$outputXml = New-Object System.Xml.XmlDocument
$declaration = $outputXml.CreateXmlDeclaration("1.0", "utf-8", $null)
$outputXml.AppendChild($declaration) | Out-Null

# Add header comment
$headerComment = $outputXml.CreateComment(" TAOM Additional Lords - New lords from LOTRAOM not in vanilla Bannerlord `r`n     NOT registered in SubModule.xml - staged for future activation ")
$outputXml.AppendChild($headerComment) | Out-Null

# Create root element
$npcCharacters = $outputXml.CreateElement("NPCCharacters")
$outputXml.AppendChild($npcCharacters) | Out-Null

# Track cultures for grouping
$lordsByCulture = @{}

Write-Host "Extracting lords..."
$extractedCount = 0
$notFoundCount = 0
$notFoundIds = @()

foreach ($lordId in $lordIds) {
    $npcNode = $sourceXml.NPCCharacters.NPCCharacter | Where-Object { $_.id -eq $lordId }

    if ($npcNode) {
        $culture = $npcNode.culture
        if (-not $lordsByCulture.ContainsKey($culture)) {
            $lordsByCulture[$culture] = @()
        }
        $lordsByCulture[$culture] += $npcNode.OuterXml
        $extractedCount++
    } else {
        $notFoundCount++
        $notFoundIds += $lordId
    }
}

Write-Host "Extracted $extractedCount lords"
Write-Host "Not found: $notFoundCount lords"

if ($notFoundCount -gt 0) {
    Write-Host "Lords not found:"
    $notFoundIds | ForEach-Object { Write-Host "  - $_" }
}

# Add lords grouped by culture
$sortedCultures = $lordsByCulture.Keys | Sort-Object

foreach ($culture in $sortedCultures) {
    # Add culture comment
    $cultureComment = $outputXml.CreateComment(" $culture ")
    $npcCharacters.AppendChild($cultureComment) | Out-Null

    foreach ($lordXml in $lordsByCulture[$culture]) {
        # Import the node into our document
        $tempDoc = New-Object System.Xml.XmlDocument
        $tempDoc.LoadXml($lordXml)
        $importedNode = $outputXml.ImportNode($tempDoc.DocumentElement, $true)
        $npcCharacters.AppendChild($importedNode) | Out-Null
    }
}

# Ensure output directory exists
$outputDir = Split-Path $OutputFile -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Save with proper formatting
$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.IndentChars = "    "
$settings.NewLineChars = "`r`n"
$settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
$settings.Encoding = [System.Text.UTF8Encoding]::new($false) # UTF-8 without BOM

$writer = [System.Xml.XmlWriter]::Create($OutputFile, $settings)
$outputXml.Save($writer)
$writer.Close()

Write-Host "Output written to: $OutputFile"
Write-Host "Done!"
