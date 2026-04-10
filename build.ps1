# TAOM Bannerlord Mod Build Script (.NET Framework 4.7.2)

param(
    [string]$Configuration = "Debug",
    [switch]$RunTests
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

Write-Host "Building C# projects in $Configuration configuration..." -ForegroundColor Yellow
& dotnet build "TAOM.sln" -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

# Build C++ native hooks project (requires Visual C++ build tools)
$nativeDll = "Main\_Module\bin\Win64_Shipping_Client\TAOM.NativeSkinFixes.dll"
$vcxproj = "Dependencies\ThirdParty\NativeSkinFixes\TAOM.NativeSkinFixes.vcxproj"
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vsWhere) {
    $msbuildPath = & $vsWhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    if ($msbuildPath) {
        Write-Host "Building NativeSkinFixes C++ project..." -ForegroundColor Yellow
        & $msbuildPath $vcxproj /p:Configuration=$Configuration /p:Platform=x64 /v:minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Host "NativeSkinFixes C++ build failed." -ForegroundColor Red
            exit 1
        }
        Write-Host "NativeSkinFixes C++ build succeeded!" -ForegroundColor Green
    } else {
        Write-Host "MSBuild not found — skipping NativeSkinFixes C++ build." -ForegroundColor Yellow
        if (-not (Test-Path $nativeDll)) {
            Write-Host "WARNING: $nativeDll not found. Native skin fixes will not work at runtime." -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "Visual Studio not found — skipping NativeSkinFixes C++ build." -ForegroundColor Yellow
    if (-not (Test-Path $nativeDll)) {
        Write-Host "WARNING: $nativeDll not found. Native skin fixes will not work at runtime." -ForegroundColor Yellow
    }
}

Write-Host "Build succeeded!" -ForegroundColor Green

if ($RunTests) {
    Write-Host "Running tests..." -ForegroundColor Yellow
    & dotnet test "TAOM.Tests" -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Tests failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "All tests passed!" -ForegroundColor Green
}
