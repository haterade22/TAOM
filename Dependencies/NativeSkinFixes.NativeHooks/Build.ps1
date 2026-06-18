#requires -Version 5.1
<#
.SYNOPSIS
    Builds the TAOM.NativeSkinFixes C++ project and deploys TAOM.NativeSkinFixes.dll
    + MinHook.x64.dll into Main/_Module/bin/Win64_Shipping_Client/.

.DESCRIPTION
    This is a manual, developer-side build. The TAOM dotnet build (./build.ps1)
    does NOT trigger this — keeping the MSVC dependency off the critical path so
    teammates / CI without C++ tooling can still build TAOM.dll.

    After running this script, run ./build.ps1 to rebuild TAOM.dll if you've made
    matching C# changes. The native DLL is deployed automatically by
    Bannerlord.BuildResources as part of the next dotnet build.

.PARAMETER Configuration
    Debug or Release. Default: Release.

.EXAMPLE
    pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
    pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 -Configuration Debug
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $scriptDir 'NativeSkinFixes.NativeHooks.vcxproj'
$outDir      = Resolve-Path (Join-Path $scriptDir '..\..\Main\_Module\bin\Win64_Shipping_Client')

if (-not (Test-Path $projectFile)) {
    throw "Project file not found: $projectFile"
}

# Locate MSBuild via vswhere (ships with Visual Studio 2017+).
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found. Install Visual Studio 2019/2022 with the 'Desktop development with C++' workload."
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild not found. Install Visual Studio with C++ workload + MSBuild component."
}

Write-Host "[NativeSkinFixes] Building $Configuration|x64 via $msbuild" -ForegroundColor Cyan
& $msbuild $projectFile /p:Configuration=$Configuration /p:Platform=x64 /m /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE"
}

$dllPath = Join-Path $outDir 'TAOM.NativeSkinFixes.dll'
if (-not (Test-Path $dllPath)) {
    throw "Build succeeded but output DLL missing: $dllPath"
}

# Static-CRT guard. The shipped DLL must link the CRT statically (Debug=/MTd,
# Release=/MT). A dynamic CRT (/MDd or /MD) imports vcruntime*/msvcp140*/
# ucrtbase*/api-ms-win-crt* — DLLs players without Visual Studio lack — and
# LoadLibrary then fails with error 126. Reject the bad binary before it can be
# vendored. See docs/features/native-skin-fixes.md "Build & CRT requirement".
$peInspect = Join-Path $scriptDir '..\..\tools\pe_inspect.py'
$py = Get-Command python3 -ErrorAction SilentlyContinue
if (-not $py) { $py = Get-Command python -ErrorAction SilentlyContinue }
if ($py -and (Test-Path $peInspect)) {
    $imports = & $py.Source $peInspect $dllPath 2>&1 | Out-String
    if ($imports -match '(?i)VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt') {
        Write-Host $imports
        throw "[NativeSkinFixes] BUILD REJECTED: $dllPath links a DYNAMIC CRT (see imports above). Rebuild with a static CRT (Debug=/MTd, Release=/MT). The dynamic/debug CRT is absent on players' machines -> LoadLibrary error 126."
    }
    Write-Host "[NativeSkinFixes] CRT check OK -> static CRT, no redistributable dependency." -ForegroundColor Green
} else {
    Write-Host "[NativeSkinFixes] WARNING: python or tools/pe_inspect.py not found; skipped static-CRT import check (the commit hook + CI still gate it)." -ForegroundColor Yellow
}

$size = (Get-Item $dllPath).Length
Write-Host "[NativeSkinFixes] OK -> $dllPath ($size bytes)" -ForegroundColor Green
Write-Host "[NativeSkinFixes] Run './build.ps1' to repackage TAOM.dll + redeploy."
