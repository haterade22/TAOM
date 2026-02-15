<#
.SYNOPSIS
    Applies LOTR settlement names to settlements.xml from Names_Settlement.txt mapping.
.DESCRIPTION
    Replaces the display name portion of each Settlement's name attribute while preserving
    the localization key. Uses sequential-by-type mapping: Nth town name -> Nth town ID,
    Nth castle name -> Nth castle ID (sorted numerically within each region).
.PARAMETER SettlementsFile
    Path to settlements.xml. Defaults to Main/_Module/ModuleData/settlements.xml.
.PARAMETER DryRun
    If specified, prints planned renames without modifying the file.
#>
param(
    [string]$SettlementsFile = "$PSScriptRoot\..\Main\_Module\ModuleData\settlements.xml",
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# Resolve path
$SettlementsFile = Resolve-Path $SettlementsFile

Write-Host "Reading $SettlementsFile ..."
$content = [System.IO.File]::ReadAllText($SettlementsFile, [System.Text.UTF8Encoding]::new($true))

# ============================================================================
# Name Mapping Dictionary
# Key = region code
# Value = hashtable with Towns (ordered array) and Castles (ordered array)
# Names listed in sequential order as they appear in Names_Settlement.txt
# For combined regions (A = Harwan + Shaghana), Harwan names come first
# ============================================================================

$NameMapping = [ordered]@{
    'EW' = @{
        Towns   = @('Minas Tirith','West Osgiliath','East Osgiliath','Bar Melui','Pelargir',
                    'Dol Amroth','Serelond','Lond Cirion','Ost Arndir','Calembel','Methir')
        Castles = @('Cair Andros','Morlad','Linhir','Rimmon','Edhellond','Tumladen',
                    "M$([char]0xe9)reharn",'Bar-en-Siril','Glanhir','Caras Tolfalas',
                    'Methrast',"Hyarpend$([char]0xeb)",'Harlond','Erethir','Amonost')
    }
    'V' = @{
        Towns   = @('Edoras','Aldburg',"Helm's Deep",'Grimslade','Eaworth','Aldenburg','Langhold')
        Castles = @('Gramburg','Fenmarch','Cliving','Starkmoore','Hiltbolt','Dunharrow','Marton')
    }
    'EN' = @{
        Towns   = @('Lhan Garan','Galtrev')
        Castles = @("T$([char]0xfb)r Morva",'Lhanuch','Lhan Tarren','Barnavon',"Thror's Coomb")
    }
    'ES' = @{
        Towns   = @("Barad D$([char]0xfb)r",'Minas Morgul','Seregost','Durthang','Thurband','Naerband')
        Castles = @('The Morannon / The Black Gate','Cirith Ungol','Carach Angren','Mornaur',
                    'Nargil','Barad Wath',"L$([char]0xfb)glurag","Barad-N$([char]0xfb)rn")
    }
    'S' = @{
        Towns   = @('Dale','Vargfell','Liutburg','Stranding','Eldby')
        Castles = @('Garthness','Westoft','Yoldbryn','Taigbarn','Torndal','Tham Aeldir','Bridgethorp')
    }
    'L' = @{
        Towns   = @('Caras Galadhon')
        Castles = @('Tol Calan','Cerin Amroth','Talas Duiren')
    }
    'R' = @{
        Towns   = @('Rivendell')
        Castles = @('Hithaegrist','Nan Tornaeth','Baracharn',"Fennas Dr$([char]0xfa)nin")
    }
    'E' = @{
        Towns   = @('Erebor',"J$([char]0xe1)rnfast","Azan$([char]0xfb)libar-d$([char]0xfb)m","Sk$([char]0xe1)rhald")
        Castles = @('Irongap','Flogalith',"Anatr$([char]0xe2)d","Fr$([char]0xf3)sthel",
                    "Buzra-s$([char]0xe2)lan",'Mesem-garak','Zul-mazal',"Grymmcl$([char]0xfa)d")
    }
    'M' = @{
        Towns   = @('Felegoth','Caras Laerolin')
        Castles = @('Glad Thaw',"Gw$([char]0xed)gar",'Torech Emel','Lasgalor','Imnagath')
    }
    'G' = @{
        Towns   = @('Mount Gundabad','Mount Gram')
        Castles = @("D$([char]0xfb)glarshun",'Framsburg',"Maz$([char]0xf4)glod","Sh$([char]0xfa)rdm$([char]0xfa)l")
    }
    'DG' = @{
        Towns   = @('Dol Guldur')
        Castles = @("Ash$([char]0xfa)rz",'Dannenglor',"B$([char]0xfb)rzkala",'Maufulug','Amon Angened')
    }
    'I' = @{
        Towns   = @()  # Orthanc handled as special case for town_isengard
        Castles = @('Forthbrond','Nan Angranost')
    }
    'U' = @{
        Towns   = @('Umbar','Jax Phanal')
        Castles = @("Zimrath$([char]0xf4)r","B$([char]0xea)luzir","Azruph$([char]0xe2)r",'Bej Magha',
                    "Rakh$([char]0xe2)x","H$([char]0xe2)tab","Kh$([char]0xfb)thra","Zamarz$([char]0xee)r")
    }
    'RU' = @{
        Towns   = @('Mistrand','Lest','Vorgavuld',"$([char]0xdb)rushban","S$([char]0xe2)rt",'Kelepar',
                    "Kh$([char]0xfb)ndol","I$([char]0xf4)rig")
        Castles = @("T$([char]0xf4)rc$([char]0xe2)in","N$([char]0xee)rakh",'Ulbarath',
                    "Ch$([char]0xea)ya",'Morath',"K$([char]0xe2)rash$([char]0xfb)n",
                    "R$([char]0xfb)artar",'Ulathar',"Sam$([char]0xe2)rn$([char]0xfb)l",
                    "Kh$([char]0xfb)sar",'Tarlat Arlan',"M$([char]0xe2)rd$([char]0xfb)n")
    }
    'A' = @{
        # Harwan towns first, then Shaghana towns
        Towns   = @("Kes Marz$([char]0xfb)k",'Korb Taskral',"Chelkar$([char]0xe2)",
                    "Hurum K$([char]0xe2)na",'Parzee',
                    "Zaj$([char]0xe2)na","Chat$([char]0xe2)k","Sormed$([char]0xe2)n")
        # Harwan castles first, then Shaghana castles
        Castles = @('Barad Harn',"Y$([char]0xe2)nu",'Kes Shadoul',"Kadar K$([char]0xe2)raba",
                    "H$([char]0xe2)rmaka",'Sul Madash',"S$([char]0xe2)r Marsag",
                    "Nagakh$([char]0xea)di","Kh$([char]0xe2)b Ant$([char]0xe2)x",
                    "D$([char]0xe2)n Shatag$([char]0xe2)n","Pazakh$([char]0xea)ra",
                    "Khatz$([char]0xe2)la",
                    "B$([char]0xe2)drasag","Gat$([char]0xfa)r","Laj$([char]0xf3)r",
                    "K$([char]0xe2)na","S$([char]0xe2)khazai","Kh$([char]0xe2)b Kit$([char]0xe2)x",
                    "Kalm$([char]0xe2)khila")
    }
    'FH' = @{
        Towns   = @()  # No towns in FH region
        Castles = @("Erak$([char]0xfa)ndo","Gebarak$([char]0xf4)r","K$([char]0xf4)th Rau",
                    "Onab$([char]0xf3)ha","Tarid$([char]0xed)m","Urut$([char]0xfa)sh",
                    "Zakh$([char]0xfb)r","Sh$([char]0xe2)lahin","Archad$([char]0xe2)zar")
    }
}

# ============================================================================
# Special case renames (non-standard settlement IDs)
# ============================================================================

$SpecialRenames = @{
    'town_isengard' = 'Orthanc'
}

# ============================================================================
# Helper: Extract and sort settlement IDs numerically
# ============================================================================

function Get-SettlementIds {
    param(
        [string]$Content,
        [string]$Region,
        [string]$Type  # 'town' or 'castle'
    )

    $pattern = "Settlement\s+id=`"(${Type}_${Region}\d+)`""
    $matches = [regex]::Matches($Content, $pattern)
    $ids = @()
    foreach ($m in $matches) {
        $ids += $m.Groups[1].Value
    }

    # Sort numerically by the trailing number
    $ids | Sort-Object { if ($_ -match '(\d+)$') { [int]$Matches[1] } else { 0 } }
}

# ============================================================================
# Core: Replace display name in Settlement name attribute
# Returns ($newContent, $oldDisplayName)
# ============================================================================

function Set-SettlementDisplayName {
    param(
        [string]$Content,
        [string]$SettlementId,
        [string]$NewName
    )

    # Capture the localization key portion and the display name
    $escapedId = [regex]::Escape($SettlementId)
    $pattern = "(<Settlement\s+id=`"$escapedId`"\s+name=`"\{=[^}]+\})([^`"]*)`""

    $oldName = $null
    $newContent = [regex]::Replace($Content, $pattern, {
        param($m)
        $oldName = $m.Groups[2].Value
        # Use script-scope variable to pass oldName back
        $script:lastOldName = $m.Groups[2].Value
        "$($m.Groups[1].Value)$NewName`""
    })

    return $newContent
}

# ============================================================================
# Main execution
# ============================================================================

$totalRenamed = 0
$renames = @()

# Process each region
foreach ($region in $NameMapping.Keys) {
    $mapping = $NameMapping[$region]

    foreach ($typeName in @('Towns','Castles')) {
        $prefix = if ($typeName -eq 'Towns') { 'town' } else { 'castle' }
        $names = $mapping[$typeName]
        if ($names.Count -eq 0) { continue }

        $ids = @(Get-SettlementIds -Content $content -Region $region -Type $prefix)
        if ($ids.Count -eq 0) {
            Write-Warning "No ${prefix}_${region}* settlements found in XML"
            continue
        }

        $count = [Math]::Min($names.Count, $ids.Count)
        for ($i = 0; $i -lt $count; $i++) {
            $id = $ids[$i]
            $newName = $names[$i]

            $script:lastOldName = ''
            $content = Set-SettlementDisplayName -Content $content -SettlementId $id -NewName $newName

            $renames += [PSCustomObject]@{
                ID      = $id
                OldName = $script:lastOldName
                NewName = $newName
            }
            $totalRenamed++
        }

        if ($names.Count -gt $ids.Count) {
            Write-Host "  $region $typeName`: $($names.Count - $ids.Count) excess name(s) skipped (more names than settlements)"
        }
        if ($ids.Count -gt $names.Count) {
            Write-Host "  $region $typeName`: $($ids.Count - $names.Count) settlement(s) kept current name (more settlements than names)"
        }
    }
}

# Process special renames
foreach ($id in $SpecialRenames.Keys) {
    $newName = $SpecialRenames[$id]
    $script:lastOldName = ''
    $content = Set-SettlementDisplayName -Content $content -SettlementId $id -NewName $newName

    $renames += [PSCustomObject]@{
        ID      = $id
        OldName = $script:lastOldName
        NewName = $newName
    }
    $totalRenamed++
}

# ============================================================================
# Output results
# ============================================================================

Write-Host ""
Write-Host "=== Settlement Renames ($totalRenamed total) ==="
Write-Host ""

$renames | Format-Table -AutoSize -Property ID, OldName, NewName

if ($DryRun) {
    Write-Host "DRY RUN - no changes written to disk."
    Write-Host ""
} else {
    # Write back
    Write-Host "Writing changes to $SettlementsFile ..."
    [System.IO.File]::WriteAllText($SettlementsFile, $content, [System.Text.UTF8Encoding]::new($true))

    # Validate XML
    Write-Host "Validating XML ..."
    try {
        [xml]$validate = Get-Content $SettlementsFile -Encoding UTF8
        Write-Host "XML validation PASSED."
    } catch {
        Write-Error "XML validation FAILED: $_"
    }

    Write-Host ""
    Write-Host "Done! $totalRenamed settlements renamed."
}
