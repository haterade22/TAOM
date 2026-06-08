param(
    [string]$SourceRoot = "D:\documents\GitHub\toam audio files",
    [string]$RepoRoot = "D:\documents\GitHub\TAOM",
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$bucketOrder = @("battle_music", "siege_music", "tavern_wander", "town_wander", "worldmap")
$allGeneratedBuckets = @("battle_music", "siege_music", "tavern_wander", "town_wander", "worldmap")
$targetCultures = @(
    "aserai",
    "battania",
    "dolguldur",
    "empire",
    "erebor",
    "gondor",
    "gundabad",
    "isengard",
    "khuzait",
    "lothlorien",
    "mirkwood",
    "mordor",
    "neutral_culture",
    "rivendell",
    "sturgia",
    "umbar",
    "vlandia"
)

$moduleSoundsRoot = Join-Path $RepoRoot "Main\_Module\ModuleSounds"
$taomRoot = Join-Path $moduleSoundsRoot "taom"
$moduleSoundsXml = Join-Path $RepoRoot "Main\_Module\ModuleData\taom_music_module_sounds.xml"
$reportPath = Join-Path $RepoRoot "docs\research\music-asset-replacement-report-2026-06-07.md"

function Get-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-UnderPath([string]$Path, [string]$Root) {
    $resolvedPath = Get-FullPath $Path
    $resolvedRoot = (Get-FullPath $Root).TrimEnd('\') + '\'
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside expected root. Path: $resolvedPath Root: $resolvedRoot"
    }
}

function Test-IgnoredAudioName([System.IO.FileInfo]$File) {
    if ($File.FullName -match "\\Victory\\") {
        return $true
    }

    return $File.BaseName -match "(?i)(^|[^a-z])(victory|defeat|wins?)([^a-z]|$)"
}

function Get-AudioFiles([string]$RelativePath) {
    $path = Join-Path $SourceRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Source folder missing: $path"
    }

    return @(Get-ChildItem -LiteralPath $path -File |
        Where-Object { $_.Extension -match '^(?i)\.(mp3|wav|ogg)$' } |
        Where-Object { -not (Test-IgnoredAudioName $_) } |
        Sort-Object Name)
}

$placements = New-Object System.Collections.Generic.List[object]

function Add-SourceGroup(
    [string[]]$Cultures,
    [string[]]$Buckets,
    [string]$RelativePath,
    [string]$SourceUse,
    [string]$SourceLabel,
    [string]$DonorFrom = ""
) {
    $files = @(Get-AudioFiles $RelativePath)
    if ($files.Count -eq 0) {
        throw "Source folder has no usable audio after victory/defeat filtering: $RelativePath"
    }

    foreach ($culture in $Cultures) {
        foreach ($bucket in $Buckets) {
            foreach ($file in $files) {
                $placements.Add([pscustomobject]@{
                    Bucket = $bucket
                    Culture = $culture
                    SourcePath = $file.FullName
                    SourceFile = $file.Name
                    SourceUse = $SourceUse
                    SourceLabel = $SourceLabel
                    DonorFrom = $DonorFrom
                    TargetFile = ""
                    TargetPath = ""
                })
            }
        }
    }
}

function Add-ComposerCulture(
    [string]$ComposerFolder,
    [string]$Culture,
    [string]$Label
) {
    $map = @{
        "Battle" = "battle_music"
        "Siege" = "siege_music"
        "Tavern" = "tavern_wander"
        "Town" = "town_wander"
        "Worldmap" = "worldmap"
    }

    foreach ($entry in $map.GetEnumerator()) {
        $relative = Join-Path $ComposerFolder $entry.Key
        if (Test-Path -LiteralPath (Join-Path $SourceRoot $relative) -PathType Container) {
            Add-SourceGroup -Cultures @($Culture) -Buckets @($entry.Value) -RelativePath $relative -SourceUse "direct" -SourceLabel $Label
        }
    }
}

# Curated top-level folders.
Add-SourceGroup -Cultures @("dolguldur", "gundabad", "isengard", "mordor") -Buckets @("tavern_wander") -RelativePath "all ork factions tavern music" -SourceUse "direct" -SourceLabel "shared orc tavern"
Add-SourceGroup -Cultures @("dolguldur") -Buckets @("town_wander") -RelativePath "Dol Guldur town music" -SourceUse "direct" -SourceLabel "Dol Guldur curated"
Add-SourceGroup -Cultures @("dolguldur") -Buckets @("worldmap") -RelativePath "Dol Guldur worldmap music" -SourceUse "direct" -SourceLabel "Dol Guldur curated"
Add-SourceGroup -Cultures @("empire") -Buckets @("battle_music", "siege_music") -RelativePath "Dunland battle siege music" -SourceUse "direct" -SourceLabel "Dunland curated"
Add-SourceGroup -Cultures @("empire") -Buckets @("town_wander") -RelativePath "Dunland town music" -SourceUse "direct" -SourceLabel "Dunland curated"
Add-SourceGroup -Cultures @("empire") -Buckets @("worldmap") -RelativePath "Dunland worldmap music" -SourceUse "direct" -SourceLabel "Dunland curated"
Add-SourceGroup -Cultures @("erebor") -Buckets @("battle_music", "siege_music") -RelativePath "Dwarfs battle siege music" -SourceUse "direct" -SourceLabel "Dwarfs curated"
Add-SourceGroup -Cultures @("erebor") -Buckets @("tavern_wander") -RelativePath "Dwarfs tavern music" -SourceUse "direct" -SourceLabel "Dwarfs curated"
Add-SourceGroup -Cultures @("erebor") -Buckets @("town_wander") -RelativePath "Dwarfs town music" -SourceUse "direct" -SourceLabel "Dwarfs curated"
Add-SourceGroup -Cultures @("erebor") -Buckets @("worldmap") -RelativePath "Dwarfs worldmap music" -SourceUse "direct" -SourceLabel "Dwarfs curated"
Add-SourceGroup -Cultures @("isengard") -Buckets @("town_wander") -RelativePath "For Isengard town music" -SourceUse "direct" -SourceLabel "Isengard curated"
Add-SourceGroup -Cultures @("gondor") -Buckets @("battle_music") -RelativePath "Gondor battle music" -SourceUse "direct" -SourceLabel "Gondor curated"
Add-SourceGroup -Cultures @("gondor") -Buckets @("siege_music") -RelativePath "Gondor siege music" -SourceUse "direct" -SourceLabel "Gondor curated"
Add-SourceGroup -Cultures @("gondor") -Buckets @("tavern_wander") -RelativePath "Gondor tavern music" -SourceUse "direct" -SourceLabel "Gondor curated"
Add-SourceGroup -Cultures @("gondor") -Buckets @("town_wander") -RelativePath "Gondor town music" -SourceUse "direct" -SourceLabel "Gondor curated"
Add-SourceGroup -Cultures @("gondor") -Buckets @("worldmap") -RelativePath "Gondor worldmap music" -SourceUse "direct" -SourceLabel "Gondor curated"
Add-SourceGroup -Cultures @("gundabad") -Buckets @("battle_music", "siege_music") -RelativePath "Gundabad battle siege music" -SourceUse "direct" -SourceLabel "Gundabad curated"
Add-SourceGroup -Cultures @("gundabad") -Buckets @("town_wander") -RelativePath "Gundabad town music" -SourceUse "direct" -SourceLabel "Gundabad curated"
Add-SourceGroup -Cultures @("gundabad") -Buckets @("worldmap") -RelativePath "Gundabad worldmap music" -SourceUse "direct" -SourceLabel "Gundabad curated"
Add-SourceGroup -Cultures @("isengard") -Buckets @("battle_music", "siege_music") -RelativePath "Isengard battle siege music" -SourceUse "direct" -SourceLabel "Isengard curated"
Add-SourceGroup -Cultures @("mordor") -Buckets @("worldmap") -RelativePath "Mordor map music" -SourceUse "direct" -SourceLabel "Mordor curated"
Add-SourceGroup -Cultures @("mordor") -Buckets @("battle_music", "siege_music") -RelativePath "mordor siege-battle" -SourceUse "direct" -SourceLabel "Mordor curated"
Add-SourceGroup -Cultures @("mordor") -Buckets @("town_wander") -RelativePath "Mordor town music" -SourceUse "direct" -SourceLabel "Mordor curated"
Add-SourceGroup -Cultures @("sturgia") -Buckets @("battle_music", "siege_music") -RelativePath "Rhun battle siege music" -SourceUse "direct" -SourceLabel "Rhun curated"
Add-SourceGroup -Cultures @("sturgia") -Buckets @("town_wander") -RelativePath "Rhun town music" -SourceUse "direct" -SourceLabel "Rhun curated"
Add-SourceGroup -Cultures @("vlandia") -Buckets @("battle_music", "siege_music") -RelativePath "Rohan battle siege music" -SourceUse "direct" -SourceLabel "Rohan curated"
Add-SourceGroup -Cultures @("vlandia") -Buckets @("tavern_wander") -RelativePath "Rohan tavern music" -SourceUse "direct" -SourceLabel "Rohan curated"
Add-SourceGroup -Cultures @("vlandia") -Buckets @("worldmap") -RelativePath "Rohan worldmap music" -SourceUse "direct" -SourceLabel "Rohan curated"

# Composer folders used as proper source material. Victory folders and victory/defeat filenames are ignored.
$composer = "music layout split by composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Filip Olejka\Lothlorien") -Culture "lothlorien" -Label "Lothlorien composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Filip Olejka\Mirkwood - Woodland Realm") -Culture "mirkwood" -Label "Mirkwood composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Filip Olejka\Rivendell - Imladris") -Culture "rivendell" -Label "Rivendell composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Corsair - Umbar") -Culture "umbar" -Label "Umbar composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Dol Guldur") -Culture "dolguldur" -Label "Dol Guldur composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Dunland") -Culture "empire" -Label "Dunland composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Dwarfs") -Culture "erebor" -Label "Dwarfs composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Gondor") -Culture "gondor" -Label "Gondor composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Gundabad") -Culture "gundabad" -Label "Gundabad composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Harad") -Culture "aserai" -Label "Harad composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Isengard") -Culture "isengard" -Label "Isengard composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Khand") -Culture "battania" -Label "Khand composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Mordor") -Culture "mordor" -Label "Mordor composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Orc Factions") -Culture "dolguldur" -Label "shared orc composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Orc Factions") -Culture "gundabad" -Label "shared orc composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Orc Factions") -Culture "isengard" -Label "shared orc composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Orc Factions") -Culture "mordor" -Label "shared orc composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Rhun") -Culture "sturgia" -Label "Rhun composer"
Add-ComposerCulture -ComposerFolder (Join-Path $composer "Vladan Zivanovic\Rohan") -Culture "vlandia" -Label "Rohan composer"
Add-SourceGroup -Cultures @("lothlorien", "mirkwood", "rivendell") -Buckets @("tavern_wander") -RelativePath (Join-Path $composer "Vladan Zivanovic\Elf Factions\Tavern") -SourceUse "direct" -SourceLabel "shared elf tavern"
Add-SourceGroup -Cultures @("neutral_culture") -Buckets @("worldmap") -RelativePath (Join-Path $composer "Vladan Zivanovic\Global - Neutral Worldmap\Worldmap") -SourceUse "direct" -SourceLabel "neutral worldmap"

function Has-Placement([string]$Culture, [string]$Bucket) {
    foreach ($placement in $placements) {
        if ($placement.Culture -eq $Culture -and $placement.Bucket -eq $Bucket) {
            return $true
        }
    }

    return $false
}

function Add-DonorIfMissing([string]$Culture, [string]$Bucket, [string]$RelativePath, [string]$DonorFrom, [string]$Label) {
    if (-not (Has-Placement $Culture $Bucket)) {
        Add-SourceGroup -Cultures @($Culture) -Buckets @($Bucket) -RelativePath $RelativePath -SourceUse "donor" -SourceLabel $Label -DonorFrom $DonorFrom
    }
}

# Thematic donor fills for missing bucket/culture slots. These are copies of proper source tracks, not old generated assets.
Add-DonorIfMissing -Culture "aserai" -Bucket "tavern_wander" -RelativePath (Join-Path $composer "Vladan Zivanovic\Harad\Town") -DonorFrom "aserai town" -Label "Harad town reused for tavern"
Add-DonorIfMissing -Culture "battania" -Bucket "tavern_wander" -RelativePath (Join-Path $composer "Vladan Zivanovic\Khand\Town") -DonorFrom "battania town" -Label "Khand town reused for tavern"
Add-DonorIfMissing -Culture "empire" -Bucket "tavern_wander" -RelativePath (Join-Path $composer "Vladan Zivanovic\Dunland\Town") -DonorFrom "empire town" -Label "Dunland town reused for tavern"
Add-DonorIfMissing -Culture "khuzait" -Bucket "battle_music" -RelativePath (Join-Path $composer "Vladan Zivanovic\Khand\Battle") -DonorFrom "battania/Khand" -Label "Khand battle donor for khuzait"
Add-DonorIfMissing -Culture "khuzait" -Bucket "siege_music" -RelativePath (Join-Path $composer "Vladan Zivanovic\Khand\Siege") -DonorFrom "battania/Khand" -Label "Khand siege donor for khuzait"
Add-DonorIfMissing -Culture "khuzait" -Bucket "tavern_wander" -RelativePath (Join-Path $composer "Vladan Zivanovic\Khand\Town") -DonorFrom "battania/Khand town" -Label "Khand town donor for khuzait tavern"
Add-DonorIfMissing -Culture "khuzait" -Bucket "town_wander" -RelativePath (Join-Path $composer "Vladan Zivanovic\Khand\Town") -DonorFrom "battania/Khand" -Label "Khand town donor for khuzait"
Add-DonorIfMissing -Culture "khuzait" -Bucket "worldmap" -RelativePath (Join-Path $composer "Vladan Zivanovic\Khand\Worldmap") -DonorFrom "battania/Khand" -Label "Khand worldmap donor for khuzait"
Add-DonorIfMissing -Culture "neutral_culture" -Bucket "battle_music" -RelativePath "Gondor battle music" -DonorFrom "gondor" -Label "Gondor battle donor for neutral fallback"
Add-DonorIfMissing -Culture "neutral_culture" -Bucket "siege_music" -RelativePath "Gondor siege music" -DonorFrom "gondor" -Label "Gondor siege donor for neutral fallback"
Add-DonorIfMissing -Culture "neutral_culture" -Bucket "tavern_wander" -RelativePath "Gondor tavern music" -DonorFrom "gondor" -Label "Gondor tavern donor for neutral fallback"
Add-DonorIfMissing -Culture "neutral_culture" -Bucket "town_wander" -RelativePath "Gondor town music" -DonorFrom "gondor" -Label "Gondor town donor for neutral fallback"
Add-DonorIfMissing -Culture "sturgia" -Bucket "tavern_wander" -RelativePath (Join-Path $composer "Vladan Zivanovic\Rhun\Town") -DonorFrom "sturgia/Rhun town" -Label "Rhun town reused for tavern"
Add-DonorIfMissing -Culture "sturgia" -Bucket "worldmap" -RelativePath (Join-Path $composer "Vladan Zivanovic\Khand\Worldmap") -DonorFrom "battania/Khand" -Label "Khand worldmap donor for Rhun"
Add-DonorIfMissing -Culture "umbar" -Bucket "tavern_wander" -RelativePath (Join-Path $composer "Vladan Zivanovic\Corsair - Umbar\Town") -DonorFrom "umbar town" -Label "Umbar town reused for tavern"
Add-DonorIfMissing -Culture "umbar" -Bucket "worldmap" -RelativePath (Join-Path $composer "Vladan Zivanovic\Harad\Worldmap") -DonorFrom "aserai/Harad" -Label "Harad worldmap donor for Umbar"

$missingCoverage = New-Object System.Collections.Generic.List[string]
foreach ($culture in $targetCultures) {
    foreach ($bucket in $bucketOrder) {
        if (-not (Has-Placement $culture $bucket)) {
            $missingCoverage.Add("$bucket/$culture")
        }
    }
}

if ($missingCoverage.Count -gt 0) {
    throw "Music replacement plan is missing bucket/culture coverage: $($missingCoverage -join ', ')"
}

# Assign deterministic target names after direct/donor resolution.
foreach ($group in $placements | Group-Object Bucket, Culture) {
    $index = 1
    foreach ($placement in ($group.Group | Sort-Object SourceUse, SourceLabel, SourceFile, SourcePath)) {
        $targetFile = "{0}_{1:D2}.ogg" -f $placement.Bucket, $index
        $targetPath = Join-Path (Join-Path (Join-Path $taomRoot $placement.Bucket) $placement.Culture) $targetFile
        $placement.TargetFile = $targetFile
        $placement.TargetPath = $targetPath
        $index++
    }
}

$summary = $placements |
    Group-Object Bucket |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            Bucket = $_.Name
            Tracks = $_.Count
            Cultures = @($_.Group | Select-Object -ExpandProperty Culture -Unique).Count
            DonorTracks = @($_.Group | Where-Object { $_.SourceUse -eq "donor" }).Count
        }
    }

if (-not $Apply) {
    $summary | Format-Table -AutoSize
    Write-Host "Dry run only. Re-run with -Apply to replace five-bucket assets."
    exit 0
}

Assert-UnderPath -Path $taomRoot -Root (Join-Path $RepoRoot "Main\_Module\ModuleSounds")

$oldFiveBucketFiles = @()
foreach ($bucket in $allGeneratedBuckets) {
    $bucketPath = Join-Path $taomRoot $bucket
    if (Test-Path -LiteralPath $bucketPath -PathType Container) {
        Assert-UnderPath -Path $bucketPath -Root $taomRoot
        $oldFiveBucketFiles += @(Get-ChildItem -LiteralPath $bucketPath -Recurse -File -Filter "*.ogg")
    }
}

foreach ($file in $oldFiveBucketFiles) {
    Assert-UnderPath -Path $file.FullName -Root $taomRoot
    Remove-Item -LiteralPath $file.FullName -Force
}

foreach ($placement in $placements) {
    $targetDir = Split-Path -Parent $placement.TargetPath
    Assert-UnderPath -Path $targetDir -Root $taomRoot
    if (-not (Test-Path -LiteralPath $targetDir -PathType Container)) {
        New-Item -ItemType Directory -Path $targetDir | Out-Null
    }

    & ffmpeg -hide_banner -loglevel error -y -i $placement.SourcePath -vn -map_metadata -1 -c:a libvorbis -q:a 5 $placement.TargetPath
    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg failed converting $($placement.SourcePath) -> $($placement.TargetPath)"
    }
}

function Get-TaomRelativePath([string]$FullPath) {
    $root = (Get-FullPath $moduleSoundsRoot).TrimEnd('\') + '\'
    $path = Get-FullPath $FullPath
    if (-not $path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Cannot build ModuleSounds-relative path for $path"
    }

    return $path.Substring($root.Length).Replace('\', '/')
}

$oggFiles = @(Get-ChildItem -LiteralPath $taomRoot -Recurse -File -Filter "*.ogg" | Sort-Object FullName)

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.Encoding = New-Object System.Text.UTF8Encoding($false)
$writer = [System.Xml.XmlWriter]::Create($moduleSoundsXml, $settings)
try {
    $writer.WriteStartDocument()
    $writer.WriteStartElement("base")
    $writer.WriteAttributeString("xmlns", "xsi", $null, "http://www.w3.org/2001/XMLSchema-instance")
    $writer.WriteAttributeString("xmlns", "xsd", $null, "http://www.w3.org/2001/XMLSchema")
    $writer.WriteAttributeString("type", "module_sound")
    $writer.WriteStartElement("module_sounds")
    $writer.WriteComment(" Auto-generated by tools/replace_taom_music_assets.ps1 from ModuleSounds/taom. ")

    $lastCulture = $null
    foreach ($file in $oggFiles) {
        $relative = Get-TaomRelativePath $file.FullName
        $withoutExtension = [System.IO.Path]::ChangeExtension($relative, $null).TrimEnd('.')
        $parts = $relative.Split('/')
        $culture = if ($parts.Length -ge 3) { $parts[2] } else { "" }
        if ($culture -ne $lastCulture) {
            $writer.WriteWhitespace("`n    ")
            $writer.WriteComment(" $culture ")
            $lastCulture = $culture
        }

        $writer.WriteStartElement("module_sound")
        $writer.WriteAttributeString("name", $withoutExtension)
        $writer.WriteAttributeString("is_2d", "true")
        $writer.WriteAttributeString("sound_category", "music")
        $writer.WriteAttributeString("path", $relative)
        $writer.WriteEndElement()
    }

    $writer.WriteEndElement()
    $writer.WriteEndElement()
    $writer.WriteEndDocument()
}
finally {
    $writer.Dispose()
}

$finalBucketCounts = $oggFiles |
    ForEach-Object {
        $relative = Get-TaomRelativePath $_.FullName
        $parts = $relative.Split('/')
        [pscustomobject]@{ Bucket = $parts[1]; Culture = $parts[2] }
    } |
    Group-Object Bucket |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            Bucket = $_.Name
            Tracks = $_.Count
            Cultures = @($_.Group | Select-Object -ExpandProperty Culture -Unique).Count
        }
    }

$allSourceAudio = @(Get-ChildItem -LiteralPath $SourceRoot -Recurse -File |
    Where-Object { $_.Extension -match '^(?i)\.(mp3|wav|ogg)$' })
$usedSourcePaths = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($placement in $placements) {
    [void]$usedSourcePaths.Add($placement.SourcePath)
}
$ignoredVictory = @($allSourceAudio | Where-Object { Test-IgnoredAudioName $_ })
$unusedSource = @($allSourceAudio | Where-Object { -not $usedSourcePaths.Contains($_.FullName) -and -not (Test-IgnoredAudioName $_) })

$report = New-Object System.Collections.Generic.List[string]
$report.Add("# Music Asset Replacement Report - 2026-06-07")
$report.Add("")
$report.Add("## Research Contract")
$report.Add("")
$report.Add("- Runtime OGG path: ``ModuleSounds/taom/<bucket>/<culture>/<track>.ogg``. Source: ``_handoff/MusicSystem_SourceDrop/docs/RUNTIME_BEHAVIOR.md:10``.")
$report.Add("- Runtime event id/path shape: ``taom/<bucket>/<culture>/<track_name_without_extension>``. Source: ``_handoff/MusicSystem_SourceDrop/docs/RUNTIME_BEHAVIOR.md:59``.")
$report.Add("- TAOM import target path: ``Main/_Module/ModuleSounds/taom/<bucket>/<culture>/<track>.ogg``. Source: ``docs/research/music-integration-plan-2026-06-06.md:212``.")
$report.Add("- ``MusicTrackIndex`` indexes only ``taom/`` entries and tests require every XML path to have an OGG and every OGG to have XML registration. Source: ``docs/research/music-integration-plan-2026-06-06.md:217-224``.")
$report.Add("")
$report.Add("## Scope")
$report.Add("")
$report.Add("- Replaced old generated OGG files in the five runtime buckets: ``battle_music``, ``siege_music``, ``tavern_wander``, ``town_wander``, ``worldmap``.")
$report.Add("- Left ``character_creation`` unchanged in this pass because the requested replacement scope was the five in-game buckets.")
$report.Add("- Ignored victory/defeat/win files and ``Victory`` folders because they are outside the five requested buckets.")
$report.Add("")
$report.Add("## Summary")
$report.Add("")
$report.Add("- Old five-bucket OGG files removed: $($oldFiveBucketFiles.Count)")
$report.Add("- New five-bucket OGG files placed: $($placements.Count)")
$report.Add("- Direct placements: $(@($placements | Where-Object { $_.SourceUse -eq 'direct' }).Count)")
$report.Add("- Donor/fill placements: $(@($placements | Where-Object { $_.SourceUse -eq 'donor' }).Count)")
$report.Add("- Total TAOM XML registrations after regeneration, including unchanged ``character_creation``: $($oggFiles.Count)")
$report.Add("- Ignored victory/defeat/win source files: $($ignoredVictory.Count)")
$report.Add("- Unused non-victory source files: $($unusedSource.Count)")
$report.Add("")
$report.Add("## Final Counts By Bucket")
$report.Add("")
$report.Add("| Bucket | Tracks | Cultures |")
$report.Add("| --- | ---: | ---: |")
foreach ($row in $finalBucketCounts) {
    $report.Add("| $($row.Bucket) | $($row.Tracks) | $($row.Cultures) |")
}
$report.Add("")
$report.Add("## Five-Bucket Replacement Counts")
$report.Add("")
$report.Add("| Bucket | Tracks | Cultures | Donor Tracks |")
$report.Add("| --- | ---: | ---: | ---: |")
foreach ($row in $summary) {
    $report.Add("| $($row.Bucket) | $($row.Tracks) | $($row.Cultures) | $($row.DonorTracks) |")
}
$report.Add("")
$report.Add("## Donor Fill Rules")
$report.Add("")
$donorRules = $placements |
    Where-Object { $_.SourceUse -eq "donor" } |
    Group-Object Bucket, Culture, DonorFrom, SourceLabel |
    Sort-Object Name
if ($donorRules.Count -eq 0) {
    $report.Add("- None.")
} else {
    foreach ($rule in $donorRules) {
        $sample = $rule.Group[0]
        $report.Add("- ``$($sample.Bucket)/$($sample.Culture)`` uses ``$($sample.DonorFrom)`` via $($sample.SourceLabel) ($($rule.Count) track(s)).")
    }
}
$report.Add("")
$report.Add("## Full Placement Breakdown")
$report.Add("")
$report.Add("| Bucket | Culture | Target | Use | Source label | Source file | Source path |")
$report.Add("| --- | --- | --- | --- | --- | --- | --- |")
foreach ($placement in ($placements | Sort-Object Bucket, Culture, TargetFile)) {
    $relativeTarget = Get-TaomRelativePath $placement.TargetPath
    $sourcePath = $placement.SourcePath.Replace('|', '\|')
    $report.Add("| $($placement.Bucket) | $($placement.Culture) | ``$relativeTarget`` | $($placement.SourceUse) | $($placement.SourceLabel) | $($placement.SourceFile.Replace('|', '\|')) | ``$sourcePath`` |")
}
$report.Add("")
$report.Add("## Unused Non-Victory Source Files")
$report.Add("")
if ($unusedSource.Count -eq 0) {
    $report.Add("- None.")
} else {
    foreach ($file in ($unusedSource | Sort-Object FullName)) {
        $report.Add("- ``$($file.FullName)``")
    }
}

$reportDir = Split-Path -Parent $reportPath
if (-not (Test-Path -LiteralPath $reportDir -PathType Container)) {
    New-Item -ItemType Directory -Path $reportDir | Out-Null
}
[System.IO.File]::WriteAllLines($reportPath, $report, (New-Object System.Text.UTF8Encoding($false)))

$summary | Format-Table -AutoSize
Write-Host "Removed old five-bucket OGG files: $($oldFiveBucketFiles.Count)"
Write-Host "Wrote report: $reportPath"
Write-Host "Regenerated XML: $moduleSoundsXml"
