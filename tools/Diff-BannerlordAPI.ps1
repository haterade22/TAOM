<#
.SYNOPSIS
    Diffs decompiled Bannerlord source between two versions, focused on types TAOM actually touches.

.DESCRIPTION
    Scans TAOM C# source to extract all TaleWorlds types referenced by Harmony patches,
    GameModel overrides, reflection, and adapters. Then diffs only those specific decompiled
    files between the old and new versions, producing a structured change report.

.PARAMETER OldDir
    Path to old decompiled source. Default: E:\Decompiled_Bannerlord_v1.3.15

.PARAMETER NewDir
    Path to new decompiled source. Default: E:\Decompiled_Bannerlord

.PARAMETER TaomDir
    Path to TAOM mod source. Default: c:\Users\mikew\source\repos\TAOM\Main

.PARAMETER OutputFile
    Where to write the report. Default: tools\api-diff-report.md
#>
param(
    [string]$OldDir = "E:\Decompiled_Bannerlord_v1.3.15",
    [string]$NewDir = "E:\Decompiled_Bannerlord",
    [string]$TaomDir = "c:\Users\mikew\source\repos\TAOM\Main",
    [string]$OutputFile = "c:\Users\mikew\source\repos\TAOM\tools\api-diff-report.md"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Bannerlord API Diff ===" -ForegroundColor Cyan
Write-Host "Old: $OldDir"
Write-Host "New: $NewDir"
Write-Host "TAOM: $TaomDir"
Write-Host ""

# ── Step 1: Extract target type names from TAOM source ──────────────────────

Write-Host "[1/4] Scanning TAOM source for TaleWorlds type references..." -ForegroundColor Yellow

$targetTypes = [System.Collections.Generic.HashSet[string]]::new()

# Harmony patch targets: [HarmonyPatch(typeof(ClassName))]
$csFiles = Get-ChildItem -Path $TaomDir -Filter "*.cs" -Recurse
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    # [HarmonyPatch(typeof(ClassName)
    $matches = [regex]::Matches($content, 'typeof\(([A-Za-z_][\w.]*)\)')
    foreach ($m in $matches) {
        $typeName = $m.Groups[1].Value
        # Only TaleWorlds types (skip TAOM's own types)
        if ($typeName -notmatch '^(Taom|TAOM|I[A-Z][a-z])' -and $typeName -notmatch 'Adapter$') {
            $targetTypes.Add($typeName.Split('.')[-1]) | Out-Null
        }
    }

    # AccessTools.Method/Property/Field(typeof(ClassName), "member")
    $matches = [regex]::Matches($content, 'AccessTools\.\w+\(typeof\(([A-Za-z_][\w.]*)\)')
    foreach ($m in $matches) {
        $targetTypes.Add($m.Groups[1].Value.Split('.')[-1]) | Out-Null
    }

    # GameModel base classes: class TaomXxx : DefaultXxxModel
    $matches = [regex]::Matches($content, ':\s*(Default\w+Model|AgentStatCalculateModel)\b')
    foreach ($m in $matches) {
        $targetTypes.Add($m.Groups[1].Value) | Out-Null
    }
}

# Remove common non-TaleWorlds types that sneak through typeof()
$excludeTypes = @('string', 'int', 'bool', 'float', 'double', 'object', 'void',
                   'List', 'Dictionary', 'HashSet', 'Action', 'Func', 'Task',
                   'Type', 'MethodInfo', 'FieldInfo', 'PropertyInfo', 'ConstructorInfo',
                   'HarmonyMethod', 'HarmonyPatchType', 'MethodType', 'Harmony',
                   'CodeInstruction', 'OpCodes', 'Label', 'LocalBuilder',
                   'StringBuilder', 'Math', 'Convert', 'Enum', 'Array',
                   'Exception', 'ArgumentException', 'InvalidOperationException',
                   'KeyValuePair', 'Tuple', 'ValueTuple', 'Nullable',
                   'XmlDocument', 'XmlNode', 'XmlElement', 'JsonConvert',
                   'ILogger', 'Debug', 'InformationMessage', 'TextObject')
foreach ($t in $excludeTypes) { $targetTypes.Remove($t) | Out-Null }

Write-Host "  Found $($targetTypes.Count) unique TaleWorlds types referenced by TAOM"

# ── Step 2: Find matching decompiled files ──────────────────────────────────

Write-Host "[2/4] Locating decompiled files for target types..." -ForegroundColor Yellow

$filePairs = @()
$missingInOld = @()
$missingInNew = @()
$unchangedCount = 0

foreach ($typeName in $targetTypes | Sort-Object) {
    $oldFiles = Get-ChildItem -Path $OldDir -Filter "$typeName.cs" -Recurse -ErrorAction SilentlyContinue
    $newFiles = Get-ChildItem -Path $NewDir -Filter "$typeName.cs" -Recurse -ErrorAction SilentlyContinue

    if (-not $oldFiles -and -not $newFiles) { continue }

    if ($oldFiles -and -not $newFiles) {
        $missingInNew += [PSCustomObject]@{ Type = $typeName; OldPath = $oldFiles[0].FullName }
        continue
    }

    if (-not $oldFiles -and $newFiles) {
        $missingInOld += [PSCustomObject]@{ Type = $typeName; NewPath = $newFiles[0].FullName }
        continue
    }

    # Use the first match (most files have unique names)
    $oldFile = $oldFiles[0]
    $newFile = $newFiles[0]

    # Quick check: are they identical?
    $oldHash = (Get-FileHash $oldFile.FullName -Algorithm MD5).Hash
    $newHash = (Get-FileHash $newFile.FullName -Algorithm MD5).Hash

    if ($oldHash -eq $newHash) {
        $unchangedCount++
        continue
    }

    $filePairs += [PSCustomObject]@{
        Type    = $typeName
        OldPath = $oldFile.FullName
        NewPath = $newFile.FullName
        OldLines = (Get-Content $oldFile.FullName).Count
        NewLines = (Get-Content $newFile.FullName).Count
    }
}

$changedCount = $filePairs.Count
Write-Host "  Unchanged: $unchangedCount | Changed: $changedCount | Removed: $($missingInNew.Count) | New: $($missingInOld.Count)"

# ── Step 3: Generate diffs for changed files ────────────────────────────────

Write-Host "[3/4] Generating diffs for $changedCount changed files..." -ForegroundColor Yellow

$diffResults = @()

foreach ($pair in $filePairs | Sort-Object { [Math]::Abs($_.NewLines - $_.OldLines) } -Descending) {
    # Use git diff for clean unified diff output
    $diffOutput = git diff --no-index --unified=3 -- $pair.OldPath $pair.NewPath 2>&1

    # Extract just the meaningful diff hunks (skip header noise)
    $cleanDiff = ($diffOutput | Where-Object {
        $_ -notmatch '^(diff |index |---|\+\+\+)' -and $_ -ne ''
    }) -join "`n"

    # Extract signature-level changes (method/property declarations)
    $sigChanges = @()
    $diffLines = $diffOutput | ForEach-Object { $_.ToString() }
    foreach ($line in $diffLines) {
        $trimmed = $line.TrimStart()
        if ($trimmed -match '^[-+]\s*(public|protected|internal|private|virtual|override|abstract|static|sealed)\s') {
            $sigChanges += $line
        }
    }

    $diffResults += [PSCustomObject]@{
        Type       = $pair.Type
        OldLines   = $pair.OldLines
        NewLines   = $pair.NewLines
        Delta      = $pair.NewLines - $pair.OldLines
        SigChanges = $sigChanges
        FullDiff   = $cleanDiff
        OldPath    = $pair.OldPath
        NewPath    = $pair.NewPath
    }
}

# ── Step 4: Write report ────────────────────────────────────────────────────

Write-Host "[4/4] Writing report to $OutputFile..." -ForegroundColor Yellow

$oldVersion = "v1.3.15"
$newVersion = "v1.4.0"
if (Test-Path (Join-Path $NewDir "VERSION.txt")) {
    $vContent = Get-Content (Join-Path $NewDir "VERSION.txt") -Raw
    if ($vContent -match 'Bannerlord Version:\s*(\S+)') { $newVersion = $Matches[1] }
}

$report = @()
$report += "# Bannerlord API Diff: $oldVersion -> $newVersion"
$report += ""
$report += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
$report += "Scope: $($targetTypes.Count) TaleWorlds types referenced by TAOM"
$report += ""
$report += "## Summary"
$report += ""
$report += "| Metric | Count |"
$report += "|--------|-------|"
$report += "| Types checked | $($targetTypes.Count) |"
$report += "| Unchanged | $unchangedCount |"
$report += "| **Changed** | **$changedCount** |"
$report += "| Removed (BREAKING) | $($missingInNew.Count) |"
$report += "| New in $newVersion | $($missingInOld.Count) |"
$report += ""

# ── BREAKING: Removed types ──
if ($missingInNew.Count -gt 0) {
    $report += "## BREAKING: Types Removed in $newVersion"
    $report += ""
    $report += "These types existed in $oldVersion but are GONE in $newVersion. Any TAOM code targeting them will fail."
    $report += ""
    foreach ($m in $missingInNew) {
        $report += "- **$($m.Type)** (was at: ``$($m.OldPath)``)"
    }
    $report += ""
}

# ── Changed types (sorted by impact) ──
if ($diffResults.Count -gt 0) {
    $report += "## Changed Types (sorted by size of change)"
    $report += ""

    foreach ($d in $diffResults | Sort-Object { [Math]::Abs($_.Delta) } -Descending) {
        $deltaStr = if ($d.Delta -gt 0) { "+$($d.Delta)" } else { "$($d.Delta)" }
        $report += "### $($d.Type) ($deltaStr lines)"
        $report += ""
        $report += "- Old: $($d.OldLines) lines | New: $($d.NewLines) lines"
        $report += "- Path: ``$($d.NewPath)``"
        $report += ""

        if ($d.SigChanges.Count -gt 0) {
            $report += "**Signature changes (public/protected API):**"
            $report += '```diff'
            foreach ($sig in $d.SigChanges) {
                $report += $sig
            }
            $report += '```'
            $report += ""
        }

        # Include condensed diff (limit to avoid huge reports)
        $diffText = $d.FullDiff
        if ($diffText.Length -gt 5000) {
            $diffText = $diffText.Substring(0, 5000) + "`n... (truncated, $($diffText.Length) chars total)"
        }
        if ($diffText.Trim()) {
            $report += "<details>"
            $report += "<summary>Full diff ($([Math]::Abs($d.Delta)) line delta)</summary>"
            $report += ""
            $report += '```diff'
            $report += $diffText
            $report += '```'
            $report += "</details>"
            $report += ""
        }
    }
}

# ── New types ──
if ($missingInOld.Count -gt 0) {
    $report += "## New Types in $newVersion"
    $report += ""
    $report += "These are new in $newVersion. Not breaking, but may offer new override/patch opportunities."
    $report += ""
    foreach ($m in $missingInOld | Sort-Object Type) {
        $report += "- **$($m.Type)** (at: ``$($m.NewPath)``)"
    }
    $report += ""
}

# ── Types TAOM references (for completeness) ──
$report += "## All TAOM-Referenced Types ($($targetTypes.Count) total)"
$report += ""
$report += "| Type | Status |"
$report += "|------|--------|"
foreach ($t in $targetTypes | Sort-Object) {
    $status = "Unchanged"
    if ($missingInNew | Where-Object { $_.Type -eq $t }) { $status = "**REMOVED**" }
    elseif ($diffResults | Where-Object { $_.Type -eq $t }) {
        $d = $diffResults | Where-Object { $_.Type -eq $t }
        $status = "Changed ($($d.Delta) lines)"
    }
    elseif ($missingInOld | Where-Object { $_.Type -eq $t }) { $status = "New" }
    $report += "| $t | $status |"
}

$report -join "`r`n" | Set-Content -Path $OutputFile -Encoding UTF8

Write-Host ""
Write-Host "========== DONE ==========" -ForegroundColor Green
Write-Host "Report: $OutputFile"
Write-Host "Changed types: $changedCount"
Write-Host "Removed types: $($missingInNew.Count)"
Write-Host ""

if ($missingInNew.Count -gt 0) {
    Write-Host "WARNING: $($missingInNew.Count) types were REMOVED. These are definite breaking changes!" -ForegroundColor Red
}
if ($changedCount -gt 0) {
    Write-Host "Review the $changedCount changed types for signature/behavior differences." -ForegroundColor Yellow
}
