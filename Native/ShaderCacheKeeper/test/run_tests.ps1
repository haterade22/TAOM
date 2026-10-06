# Tests the proxy outside the game, hermetically: a stand-in game folder (bin\Win64_Shipping_Client with the proxy
# and a host program named like the game's executables, Modules with synthetic modules) and a stand-in ProgramData
# with a synthetic shader cache in the engine's layout. Nothing of the real game or the real cache is written; case 7
# only reads the real LauncherData.xml and is skipped without one.
# Run: python ..\build.py first, then pwsh -File run_tests.ps1. Exit code = number of failed checks.
# Upstream: yotthani/bannerlord HoN/ShaderCacheKeeper test/run_tests.ps1 (MIT), reworked for TAOM (no D: paths).
$ErrorActionPreference = 'Stop'
$here = Split-Path $PSScriptRoot
$vcvars = $env:VCVARS64
if (-not $vcvars) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $vcvars = Join-Path ($vs | Select-Object -First 1) 'VC\Auxiliary\Build\vcvars64.bat'
}
$root = Join-Path $env:TEMP 'keeper_test'
if (Test-Path $root) { cmd /c "rmdir /s /q `"$root`"" }
$game = "$root\game"
$bin = "$game\bin\Win64_Shipping_Client"
$modules = "$game\Modules"
$data = "$root\ProgramData"
$shaders = "$data\Mount and Blade II Bannerlord\Shaders"
$d3d11 = "$shaders\CoreShaders\D3D11"
New-Item -ItemType Directory -Force $bin, $modules, $d3d11 | Out-Null
Copy-Item "$here\out\xinput9_1_0.dll" $bin
function Invoke-Vc([string]$command) { cmd /c "`"$vcvars`" >nul 2>&1 && $command" }
Invoke-Vc "cl /nologo /W3 `"$PSScriptRoot\host.c`" /Fo`"$root\host.obj`" /Fe`"$bin\Bannerlord.exe`"" | Out-Null
Copy-Item "$bin\Bannerlord.exe" "$bin\Bannerlord.BLSE.Launcher.exe"
Copy-Item "$bin\Bannerlord.exe" "$bin\SomethingElse.exe"
$mapping = "$d3d11\shader_mapping.bin"
$sidecar = "$d3d11\keeper_signature.txt"
$log = "$shaders\ShaderCacheKeeper.log"

function New-Module([string]$id, [bool]$withShaders, [string]$version = 'v1.0.0') {
    New-Item -ItemType Directory -Force "$modules\$id" | Out-Null
    Set-Content "$modules\$id\SubModule.xml" "<Module><Id value=`"$id`"/><Version value=`"$version`"/></Module>"
    if ($withShaders) {
        New-Item -ItemType Directory -Force "$modules\$id\Shaders\D3D11" | Out-Null
        Set-Content "$modules\$id\Shaders\D3D11\compressed_shader_cache.sack" 'sack'
        (Get-Item "$modules\$id\Shaders\D3D11\compressed_shader_cache.sack").LastWriteTime = (Get-Date).AddDays(-1)
    }
}
# the engine's layout (read off a v1.5.4 cache, 2026-10-06): u32 format 0x783, u32 length + build text, u32 length +
# sorted module list, then the shader records (a stand-in tail here)
$tail = [byte[]](1..40 | ForEach-Object { 0xA0 + ($_ % 16) })
function Write-Cache([string[]]$list, [switch]$KeepNotes) {
    $build = [Text.Encoding]::ASCII.GetBytes('123627'); $text = [Text.Encoding]::ASCII.GetBytes(($list -join ';'))
    $ms = New-Object IO.MemoryStream
    $ms.Write([BitConverter]::GetBytes([int]0x783), 0, 4)
    $ms.Write([BitConverter]::GetBytes([int]$build.Length), 0, 4); $ms.Write($build, 0, $build.Length)
    $ms.Write([BitConverter]::GetBytes([int]$text.Length), 0, 4); $ms.Write($text, 0, $text.Length)
    $ms.Write($tail, 0, $tail.Length)
    [IO.File]::WriteAllBytes($mapping, $ms.ToArray())
    if (-not $KeepNotes) { Remove-Item $sidecar, $log -ErrorAction SilentlyContinue }
}
function Sorted([string[]]$ids) { $a = [string[]]@($ids); [Array]::Sort($a, [StringComparer]::Ordinal); $a }
function Read-List { $b = [IO.File]::ReadAllBytes($mapping); $l1 = [BitConverter]::ToInt32($b, 4); $l2 = [BitConverter]::ToInt32($b, 8 + $l1); [Text.Encoding]::ASCII.GetString($b, 12 + $l1, $l2) }
function Tail-Same { $b = [IO.File]::ReadAllBytes($mapping); $t = $b[($b.Length - $tail.Length)..($b.Length - 1)]; -not (Compare-Object $t $tail -SyncWindow 0) }
function Invoke-Host([string]$exe, [string]$arguments) {
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = "$bin\$exe"; $psi.Arguments = $arguments; $psi.UseShellExecute = $false; $psi.RedirectStandardOutput = $true; $psi.WorkingDirectory = $bin
    $psi.EnvironmentVariables['ProgramData'] = $data
    $p = [Diagnostics.Process]::Start($psi); $out = $p.StandardOutput.ReadToEnd(); $p.WaitForExit()
    $out.Trim()
}
function Start-With([string[]]$ids, [string]$exe = 'Bannerlord.exe') { Invoke-Host $exe "/singleplayer _MODULES_*$((@($ids | Sort-Object { Get-Random })) -join '*')*_MODULES_" }
function Last-Note { if (Test-Path $log) { $t = (Get-Content $log -Tail 1); $t.Substring(0, [Math]::Min(230, $t.Length)) } else { '(no log line)' } }
function Hash { (Get-FileHash $mapping).Hash }
$failed = 0
function Check([string]$what, [bool]$ok) { if ($ok) { "  ok   $what" } else { $script:failed++; "  FAIL $what" } }

New-Module 'ShaderA' $true; New-Module 'ShaderB' $true; New-Module 'PlainC' $false; New-Module 'PlainD' $false
$all = Sorted @('ShaderA', 'ShaderB', 'PlainC', 'PlainD')
$less = Sorted @('ShaderA', 'ShaderB', 'PlainD')

'0 the export table forwards nothing (a forwarder to C:\Windows\... fails the engine load where Windows is not on C:)'
$exports = Invoke-Vc "dumpbin /nologo /exports `"$bin\xinput9_1_0.dll`""
Check 'no forwarded export' (-not ($exports -match 'forwarded to'))
Check 'XInputGetState and XInputSetState exported' ((($exports -match '\bXInputGetState\b').Count -gt 0) -and (($exports -match '\bXInputSetState\b').Count -gt 0))

'1 list on the command line, one module without shader content less'
Write-Cache $all
$before = Hash
$out = Start-With $less
"  $out"; "  $(Last-Note)"
Check "Windows' XInput answered (160 = bad index; 1167 = no controller, 0 = controller there)" ($out -match 'XInputGetState\(4\) = 160; XInputGetState\(0\) = (1167|0); XInputSetState\(0\) = (1167|0)$')
Check 'list text now the new list, sorted' ((Read-List) -ceq ($less -join ';'))
Check 'signature noted beside the cache' (Test-Path $sidecar)
Check 'everything after the list text unchanged' (Tail-Same)

'2 the same start again'
Start-With $less | Out-Null
"  $(Last-Note)"
Check 'nothing to do' ((Last-Note) -match "is this module list's")

'3 a module WITH shader content less'
$before = Hash
Start-With (Sorted @('ShaderA', 'PlainD')) | Out-Null
"  $(Last-Note)"
Check 'left alone, the engine compiles' (((Last-Note) -match 'shader content changed') -and (Hash) -eq $before)

'4 switched off with ShaderCacheKeeper.off'
Write-Cache $all
$before = Hash
Set-Content "$bin\ShaderCacheKeeper.off" ''
Start-With $less | Out-Null
Check 'nothing written, nothing logged' ((Hash) -eq $before -and -not (Test-Path $log))
Remove-Item "$bin\ShaderCacheKeeper.off"

'5 another program loads the proxy'
$out = Start-With $less 'SomethingElse.exe'
"  $out"
Check 'answered, cache untouched, nothing logged' ($out -match 'XInputGetState' -and (Hash) -eq $before -and -not (Test-Path $log))

'6 no cache on disk'
Remove-Item $mapping
Start-With $less | Out-Null
"  $(Last-Note)"
Check 'says so and creates nothing' (((Last-Note) -match 'no shader cache') -and -not (Test-Path $mapping))

'7 the launcher that becomes the game: list from LauncherData.xml (read only)'
$documents = [Environment]::GetFolderPath('MyDocuments')
$launcherData = Join-Path $documents 'Mount and Blade II Bannerlord\Configs\LauncherData.xml'
if (Test-Path $launcherData) {
    $xml = [xml](Get-Content $launcherData -Raw)
    $selected = Sorted @($xml.UserData.SingleplayerData.ModDatas.UserModData | Where-Object { $_.IsSelected -eq 'true' } | ForEach-Object { $_.Id })
    $plain = Sorted @('PlainC', 'PlainD')
    Write-Cache $plain    # neither the cache's modules nor the selected ids have shader content in the stand-in Modules
    Invoke-Host 'Bannerlord.BLSE.Launcher.exe' '' | Out-Null
    "  $(Last-Note)"
    if ($xml.UserData.GameType -eq 'Singleplayer' -and $selected.Count -gt 0) { Check "list text now the launcher's selection ($($selected.Count) modules)" ((Read-List) -ceq ($selected -join ';')) }
    else { Check 'not a single player start, or nothing selected: nothing done' ((Read-List) -ceq ($plain -join ';')) }
} else { '  skip (no LauncherData.xml)' }

'8 Bannerlord.exe without a list on the command line'
Write-Cache $all
$before = Hash
Invoke-Host 'Bannerlord.exe' '/singleplayer' | Out-Null
"  $(Last-Note)"
Check 'nothing done' (((Last-Note) -match 'no module list found') -and (Hash) -eq $before)

'9 a shader module updated after the engine built the cache, then the list changes'
Write-Cache $all
Start-With $all | Out-Null                               # the cache is this list's: the keeper notes its signature
(Get-Item "$modules\ShaderA\Shaders\D3D11\compressed_shader_cache.sack").LastWriteTime = Get-Date
$before = Hash
Start-With $less | Out-Null
"  $(Last-Note)"
Check 'left alone, the engine compiles' (((Last-Note) -match 'shader content changed') -and (Hash) -eq $before)

'10 after the engine compiled for a new list, a shader module is updated, then the list changes again'
Write-Cache $all
Start-With (Sorted @('ShaderA', 'PlainC', 'PlainD')) | Out-Null   # shader content changed: the engine compiles for this list
Write-Cache (Sorted @('ShaderA', 'PlainC', 'PlainD')) -KeepNotes   # what the engine leaves behind; the keeper's notes stay
(Get-Item "$modules\ShaderA\Shaders\D3D11\compressed_shader_cache.sack").LastWriteTime = (Get-Date).AddMinutes(1)
$before = Hash
Start-With (Sorted @('ShaderA', 'PlainD')) | Out-Null
"  $(Last-Note)"
Check 'left alone, the engine compiles' (((Last-Note) -match 'shader content changed') -and (Hash) -eq $before)

'11 more modules with shader content than the signature holds'
$many = @(1..60 | ForEach-Object { 'Many{0:D2}' -f $_ })
$longVersion = 'v' + ('9.' * 30) + '9'
foreach ($id in $many) { New-Module $id $true $longVersion }
Write-Cache (Sorted ($many + @('PlainC', 'PlainD')))
Start-With (Sorted ($many + @('PlainC'))) | Out-Null
(Get-Item "$modules\Many60\Shaders\D3D11\compressed_shader_cache.sack").LastWriteTime = (Get-Date).AddMinutes(2)
$before = Hash
Start-With (Sorted $many) | Out-Null
"  $(Last-Note)"
Check 'a change past what fits is never waved through' (((Hash) -eq $before) -and ((Read-List) -cne ((Sorted $many) -join ';')))
foreach ($id in $many) { Remove-Item "$modules\$id" -Recurse -Force }

'12 parsing (parse_tests.exe)'
$parse = Invoke-Vc "cl /nologo /W3 `"$PSScriptRoot\parse_tests.c`" /Fo`"$root\parse_tests.obj`" /Fe`"$root\parse_tests.exe`" advapi32.lib"
if (Test-Path "$root\parse_tests.exe") {
    $parsed = & "$root\parse_tests.exe"
    $parsed | ForEach-Object { "  $_" }
    Check 'parse tests pass' ($LASTEXITCODE -eq 0)
} else { ($parse | Select-Object -Last 5) | ForEach-Object { "  $_" }; Check 'parse tests compile' $false }

"$failed failed"
exit $failed
