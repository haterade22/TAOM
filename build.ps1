# TAOM Bannerlord Mod Build Script (.NET Framework 4.7.2)

param(
    [string]$Configuration = "Debug"
)

Write-Host "--- TAOM Bannerlord Mod Build Script (.NET Framework 4.7.2) ---" -ForegroundColor Cyan

# Check for BANNERLORD_GAME_DIR environment variable
$bannerlordPath = [System.Environment]::GetEnvironmentVariable("BANNERLORD_GAME_DIR", "User")
if (-not $bannerlordPath) {
    Write-Host "BANNERLORD_GAME_DIR is not set. Please run setup-dev-env.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "Bannerlord game directory: $bannerlordPath" -ForegroundColor Green

Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
& dotnet restore "TAOM.sln"
if ($LASTEXITCODE -ne 0) {
    Write-Host "NuGet restore failed." -ForegroundColor Red
    exit 1
}

Write-Host "Building solution in $Configuration configuration..." -ForegroundColor Yellow
& dotnet build "TAOM.sln" -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

Write-Host "Build succeeded!" -ForegroundColor Green
