# Script to merge new lord templates into lords.xslt

$taomXsltPath = "c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lords.xslt"
$newTemplatesPath = "c:\Users\mikew\source\repos\TAOM\scripts\new_lords_templates.xml"

Write-Host "Reading current lords.xslt..."
$xsltContent = Get-Content $taomXsltPath -Raw -Encoding UTF8

Write-Host "Reading new templates..."
$newTemplates = Get-Content $newTemplatesPath -Raw -Encoding UTF8

# Remove BOM from newTemplates if present
if ($newTemplates.StartsWith([char]0xFEFF)) {
    $newTemplates = $newTemplates.Substring(1)
}

# Find the closing stylesheet tag and insert before it
$closingTag = "</xsl:stylesheet>"
$insertPosition = $xsltContent.LastIndexOf($closingTag)

if ($insertPosition -eq -1) {
    Write-Host "ERROR: Could not find closing stylesheet tag" -ForegroundColor Red
    exit 1
}

Write-Host "Inserting new templates..."

# Build new content
$beforeInsert = $xsltContent.Substring(0, $insertPosition)
$afterInsert = $xsltContent.Substring($insertPosition)

# Add section header for new lords
$sectionHeader = @"

    <!-- ============================================== -->
    <!-- Additional Lords from LOTRAOM -->
    <!-- ============================================== -->

"@

$mergedContent = $beforeInsert + $sectionHeader + $newTemplates + "`n" + $afterInsert

# Write back to file
$mergedContent | Out-File $taomXsltPath -Encoding UTF8 -NoNewline

Write-Host "Done! Merged templates into lords.xslt"

# Verify the result
$finalContent = Get-Content $taomXsltPath -Raw
$templateCount = ([regex]::Matches($finalContent, "xsl:template match=`"NPCCharacter\[@id='lord_")).Count
Write-Host "Total lord templates in file: $templateCount"
