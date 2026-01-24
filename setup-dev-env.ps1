# Bannerlord Development Environment Setup Script
Write-Host "Setting up Bannerlord development environment..." -ForegroundColor Green
Write-Host "`nPlease provide the path to your Mount & Blade II: Bannerlord installation." -ForegroundColor Yellow
Write-Host "Common locations are:" -ForegroundColor Yellow
Write-Host "- Steam: C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord" -ForegroundColor Gray
Write-Host "`nPaste your Bannerlord installation path here:" -ForegroundColor Yellow
$bannerlordPath = Read-Host

# Verify the path contains Bannerlord.exe
$exePath = Join-Path $bannerlordPath "bin\Win64_Shipping_Client\Bannerlord.exe"
$storeExePath = Join-Path $bannerlordPath "bin\Gaming.Desktop.x64_Shipping_Client\Bannerlord.exe"

if (-not (Test-Path $exePath) -and -not (Test-Path $storeExePath)) {
    Write-Host "`nCould not find Bannerlord.exe in the provided path." -ForegroundColor Red
    Write-Host "Please make sure you've provided the correct installation path." -ForegroundColor Red
    exit 1
}

# Set the environment variable
[System.Environment]::SetEnvironmentVariable("BANNERLORD_GAME_DIR", $bannerlordPath, "User")

Write-Host "`nEnvironment variable BANNERLORD_GAME_DIR has been set to:" -ForegroundColor Green
Write-Host $bannerlordPath -ForegroundColor Green
Write-Host "`nPlease restart your IDE/terminal for the changes to take effect." -ForegroundColor Yellow
Write-Host "You can now build the mod project." -ForegroundColor Green
