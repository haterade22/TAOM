# lowercase-pngs.ps1
# Renames all PNG files in a folder to lowercase

param(
    [Parameter(Mandatory=$true)]
    [string]$Folder
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Folder)) {
    Write-Error "Folder not found: $Folder"
    exit 1
}

Write-Host "Processing folder: $Folder"

$renamed = 0
Get-ChildItem -Path $Folder -Filter "*.png" | ForEach-Object {
    $newName = $_.Name.ToLower()
    if ($_.Name -cne $newName) {
        Write-Host "  Renaming: $($_.Name) -> $newName"
        Rename-Item -Path $_.FullName -NewName $newName
        $renamed++
    }
}

Write-Host ""
Write-Host "Renamed $renamed files"
