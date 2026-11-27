# Tests the Dinen proxy outside the Kit, hermetically: a stand-in bin folder in %TEMP% with the proxy, a stand-in
# TaleWorlds.Native.dll that imports MessageBoxA and XInput like the engine, and a host program. Nothing of the real
# game is read or written.
# Run: python ..\build.py first, then pwsh -File run_tests.ps1. Exit code = number of failed checks.
# Layout follows Native/ShaderCacheKeeper/test/run_tests.ps1 (MIT, (c) 2026 yotthani).
$ErrorActionPreference = 'Stop'
$here = Split-Path $PSScriptRoot
$vcvars = $env:VCVARS64
if (-not $vcvars) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $vcvars = Join-Path ($vs | Select-Object -First 1) 'VC\Auxiliary\Build\vcvars64.bat'
}
$bin = Join-Path $env:TEMP 'dinen_proxy_test'
if (Test-Path $bin) { cmd /c "rmdir /s /q `"$bin`"" }
New-Item -ItemType Directory -Force $bin | Out-Null
Copy-Item "$here\out\xinput9_1_0.dll" $bin
function Invoke-Vc([string]$command) { cmd /c "`"$vcvars`" >nul 2>&1 && $command" }
$failed = 0
function Check([string]$what, [bool]$ok) { if ($ok) { "  ok   $what" } else { $script:failed++; "  FAIL $what" } }

Invoke-Vc "lib /nologo /def:`"$here\dinen_proxy.def`" /machine:x64 /out:`"$bin\xinput9_1_0.lib`"" | Out-Null
Invoke-Vc "cl /nologo /W3 /LD `"$PSScriptRoot\fake_native.c`" /Fo`"$bin\fake_native.obj`" /Fe`"$bin\TaleWorlds.Native.dll`" user32.lib `"$bin\xinput9_1_0.lib`"" | Out-Null
Invoke-Vc "cl /nologo /W3 `"$PSScriptRoot\host.c`" /Fo`"$bin\host.obj`" /Fe`"$bin\host.exe`"" | Out-Null
Invoke-Vc "cl /nologo /W3 `"$PSScriptRoot\watchdog.c`" /Fo`"$bin\watchdog.obj`" /Fe`"$bin\Watchdog.exe`" user32.lib" | Out-Null
Copy-Item "$bin\Watchdog.exe" "$bin\Other.exe"
$outside = Join-Path $env:TEMP 'dinen_proxy_test_outside'
New-Item -ItemType Directory -Force $outside | Out-Null
Copy-Item "$bin\Watchdog.exe" "$outside\Watchdog.exe"
Check 'stand-in engine and host built' ((Test-Path "$bin\TaleWorlds.Native.dll") -and (Test-Path "$bin\host.exe"))

'0 exports: the four XInput functions, no forwarder'
$exports = Invoke-Vc "dumpbin /nologo /exports `"$bin\xinput9_1_0.dll`""
Check 'no forwarded export' (-not ($exports -match 'forwarded to'))
foreach ($name in 'XInputGetState', 'XInputSetState', 'XInputGetCapabilities', 'XInputGetDSoundAudioDeviceGuids') {
    Check "$name exported" (($exports -match "\b$name\b").Count -gt 0)
}
$imports = Invoke-Vc "dumpbin /nologo /imports `"$bin\xinput9_1_0.dll`""
Check 'the proxy does not import MessageBoxA (it finds it at run time)' (-not ($imports -match 'MessageBoxA'))

function Invoke-Host([string]$mode) {
    Push-Location $bin
    try { $out = & "$bin\host.exe" $mode; $code = $LASTEXITCODE } finally { Pop-Location }
    $out
    if ($code -ne 0) { $script:failed++; "  FAIL host $mode exit $code" }
}

'1 the loader brings the proxy in with the engine; boxes are answered'
Invoke-Host 'direct'
$log = Get-Content "$bin\dinen_startup.log" -ErrorAction SilentlyContinue
Check 'log names the hook in DllMain' (($log -match 'MessageBoxA hooked in TaleWorlds.Native.dll \(in DllMain\)').Count -eq 1)
Check 'log has one line per answered box (6)' (($log -match "answered '").Count -eq 6)
Check 'log keeps the box text' (($log -match "answered 'ASSERT': test text").Count -eq 1)
Check 'one entry per line (7 lines)' ($log.Count -eq 7)

'2 the proxy loads before the engine; the wait thread hooks'
Remove-Item "$bin\dinen_startup.log"
Invoke-Host 'late'
$log = Get-Content "$bin\dinen_startup.log" -ErrorAction SilentlyContinue
Check 'log names the hook after the loader' (($log -match 'hooked in TaleWorlds.Native.dll \(after the loader\)').Count -eq 1)

'3 the proxy answers the bin folder''s Watchdog.exe with No; another program''s box and a Watchdog.exe elsewhere stay open'
Remove-Item "$bin\dinen_startup.log"
$watchdog = Start-Process "$bin\Watchdog.exe" -PassThru
$other = Start-Process "$bin\Other.exe" -PassThru
$stranger = Start-Process "$outside\Watchdog.exe" -PassThru
Start-Sleep -Milliseconds 500
Invoke-Host 'watch'
Check 'Watchdog stand-in closed with No (7)' ($watchdog.WaitForExit(2000) -and $watchdog.ExitCode -eq 7)
Check 'Other.exe box still open' (-not $other.HasExited)
Check 'Watchdog.exe outside the bin folder still open' (-not $stranger.HasExited)
if (-not $other.HasExited) { Stop-Process -Id $other.Id }
if (-not $stranger.HasExited) { Stop-Process -Id $stranger.Id }
if (-not $watchdog.HasExited) { Stop-Process -Id $watchdog.Id }
$log = Get-Content "$bin\dinen_startup.log" -ErrorAction SilentlyContinue
Check 'log names the Watchdog answer' (($log -match "answered '\*_\*' of Watchdog.exe: No").Count -ge 1)

'4 dinen.off beside the proxy: no hook'
Remove-Item "$bin\dinen_startup.log"
Set-Content "$bin\dinen.off" ''
Invoke-Host 'off'
$log = Get-Content "$bin\dinen_startup.log" -ErrorAction SilentlyContinue
Check 'log names the off switch' (($log -match 'off: dinen.off is present').Count -eq 1)
Remove-Item "$bin\dinen.off"

"failed: $failed"
exit $failed
