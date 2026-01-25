# add-skill-templates.ps1
# Adds skill_template attribute to all lords in characters/lords.xml

param(
    [string]$InputFile = "Main/_Module/ModuleData/characters/lords.xml",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Template pools by category
$maleInfantryTemplates = @(
    "spc_shock_troop_skills",
    "spc_phalanx_skills",
    "spc_berserker_skills",
    "spc_swordsman_skills"
)

$maleCavalryTemplates = @(
    "spc_knight_skills",
    "spc_cavalry_skills",
    "spc_faris_skills"
)

$maleRangedTemplates = @(
    "spc_archer_skills",
    "spc_fian_skills"
)

$maleHorseArcherTemplates = @(
    "spc_mounted_archery_skills"
)

$femaleRangedTemplates = @(
    "spc_huntress_skills"
)

$femaleOtherTemplates = @(
    "spc_chatelaine_skills",
    "spc_matriarch_skills"
)

# Templates that have _rookie variants
$templatesWithRookie = @(
    "spc_shock_troop_skills",
    "spc_phalanx_skills",
    "spc_berserker_skills",
    "spc_swordsman_skills",
    "spc_knight_skills",
    "spc_cavalry_skills",
    "spc_faris_skills",
    "spc_archer_skills",
    "spc_fian_skills",
    "spc_huntress_skills",
    "spc_chatelaine_skills",
    "spc_matriarch_skills"
)

# Simple hash function for deterministic random selection based on lord ID
function Get-DeterministicIndex {
    param([string]$id, [int]$poolSize)

    $hash = 0
    foreach ($char in $id.ToCharArray()) {
        $hash = ($hash * 31 + [int]$char) % 2147483647
    }
    return [Math]::Abs($hash) % $poolSize
}

# Load XML
$xmlPath = Join-Path (Join-Path $PSScriptRoot "..") $InputFile
$xmlPath = Resolve-Path $xmlPath
Write-Host "Loading $xmlPath..."

[xml]$xml = Get-Content $xmlPath -Encoding UTF8

$updatedCount = 0
$skippedCount = 0
$templateCounts = @{}

foreach ($npc in $xml.NPCCharacters.NPCCharacter) {
    $id = $npc.id
    $isFemale = $npc.is_female -eq "true"
    $defaultGroup = $npc.default_group
    $age = [int]$npc.age

    # Skip if already has skill_template
    if ($npc.skill_template) {
        Write-Host "  $id already has skill_template, skipping"
        $skippedCount++
        continue
    }

    # Determine template pool based on gender and default_group
    $templatePool = $null
    if ($isFemale) {
        if ($defaultGroup -in @("Ranged", "HorseArcher")) {
            $templatePool = $femaleRangedTemplates
        } else {
            $templatePool = $femaleOtherTemplates
        }
    } else {
        switch ($defaultGroup) {
            "Infantry" { $templatePool = $maleInfantryTemplates }
            "Cavalry" { $templatePool = $maleCavalryTemplates }
            "Ranged" { $templatePool = $maleRangedTemplates }
            "HorseArcher" { $templatePool = $maleHorseArcherTemplates }
            default {
                Write-Warning "Unknown default_group '$defaultGroup' for $id, using Infantry"
                $templatePool = $maleInfantryTemplates
            }
        }
    }

    # Select template deterministically based on lord ID
    $index = Get-DeterministicIndex -id $id -poolSize $templatePool.Count
    $template = $templatePool[$index]

    # Apply _rookie suffix for young characters (age < 25)
    if ($age -lt 25 -and $template -in $templatesWithRookie) {
        $template = "${template}_rookie"
    }

    # Full template name with SkillSet prefix
    $fullTemplate = "SkillSet.$template"

    # Add skill_template attribute
    $npc.SetAttribute("skill_template", $fullTemplate)

    # Track counts
    if (-not $templateCounts.ContainsKey($fullTemplate)) {
        $templateCounts[$fullTemplate] = 0
    }
    $templateCounts[$fullTemplate]++

    $genderStr = if ($isFemale) { "F" } else { "M" }
    $ageStr = if ($age -lt 25) { "R" } else { "" }  # R for rookie
    Write-Host "  $id ($genderStr, $defaultGroup, age $age) -> $fullTemplate"
    $updatedCount++
}

Write-Host ""
Write-Host "Summary:"
Write-Host "  Updated: $updatedCount"
Write-Host "  Skipped: $skippedCount"
Write-Host ""
Write-Host "Template Distribution:"
$templateCounts.GetEnumerator() | Sort-Object Name | ForEach-Object {
    Write-Host "  $($_.Key): $($_.Value)"
}

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
