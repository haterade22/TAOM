#Requires -Version 7
<#
.SYNOPSIS
    Decompile and cache TaleWorlds types via a single command. Auto-detects version.
.DESCRIPTION
    Mirrors the opensrc pattern (https://github.com/vercel-labs/opensrc) for TAOM's
    closed-source TaleWorlds dependencies. Caches at $env:USERPROFILE\.taom-src\<version>\
    where <version> is auto-detected from Version.xml in the resolved bin dir
    (e.g. v1.3.15, v1.4.5). Multiple versions can coexist.

    Bin dir resolution order:
      1. $env:BANNERLORD_OVERRIDE_DIR\bin\Win64_Shipping_Client (if set)
      2. $env:BANNERLORD_GAME_DIR\bin\Win64_Shipping_Client (default)
.EXAMPLE
    # Cache from current $env:BANNERLORD_GAME_DIR (whatever version it is)
    pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel
.EXAMPLE
    # Cache from 1.3.15 backup (during dual-DLL migration window)
    $env:BANNERLORD_OVERRIDE_DIR = "E:\BannerlordBackup\1.3.15"
    pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel
    Remove-Item Env:\BANNERLORD_OVERRIDE_DIR
.EXAMPLE
    rg "GetCharacterWage" (pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
#>

param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('path', 'list', 'remove', 'clean')]
    [string]$Command,

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

$ErrorActionPreference = 'Stop'

$CacheRoot  = Join-Path $env:USERPROFILE '.taom-src'
# $Version and $VersionDir are resolved lazily once Get-BinDir runs (needs Version.xml).
$script:Version    = $null
$script:VersionDir = $null
$SourcesPath = Join-Path $CacheRoot 'sources.json'
$IndexPath   = Join-Path $CacheRoot 'dll-index.json'

function Write-Progress2 {
    param([string]$Message)
    [Console]::Error.WriteLine("[taom-src] $Message")
}

function Get-BinDir {
    # Override takes precedence (used during dual-DLL migration window).
    if ($env:BANNERLORD_OVERRIDE_DIR) {
        $bin = Join-Path $env:BANNERLORD_OVERRIDE_DIR 'bin\Win64_Shipping_Client'
        if (Test-Path $bin) {
            Write-Progress2 "using override bin: $bin"
            return $bin
        }
        Write-Progress2 "BANNERLORD_OVERRIDE_DIR set but bin not found at $bin; falling through to BANNERLORD_GAME_DIR"
    }
    if (-not $env:BANNERLORD_GAME_DIR) {
        throw "BANNERLORD_GAME_DIR is not set. Set it to your Bannerlord install root (e.g. 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord')."
    }
    $bin = Join-Path $env:BANNERLORD_GAME_DIR 'bin\Win64_Shipping_Client'
    if (-not (Test-Path $bin)) {
        throw "Bannerlord bin dir not found: $bin"
    }
    return $bin
}

function Resolve-Version {
    param([string]$BinDir)
    if ($script:Version) { return }
    $versionXml = Join-Path $BinDir 'Version.xml'
    if (-not (Test-Path $versionXml)) {
        throw "Version.xml not found at $versionXml. Cannot determine cache version."
    }
    $xml = [xml](Get-Content $versionXml -Raw)
    $detected = $xml.Version.Singleplayer.Value
    if (-not $detected) {
        throw "Could not parse Version.xml at $versionXml. Expected <Version><Singleplayer Value='vX.Y.Z'/></Version>."
    }
    $script:Version = $detected
    $script:VersionDir = Join-Path $CacheRoot $detected
    Write-Progress2 "detected version $detected -> cache $($script:VersionDir)"
}

function Assert-Ilspycmd {
    if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
        throw "ilspycmd not found on PATH. Install with: dotnet tool install -g ilspycmd"
    }
}

function Initialize-Cache {
    if (-not $script:VersionDir) {
        throw "Initialize-Cache called before Resolve-Version. Call Get-BinDir + Resolve-Version first."
    }
    if (-not (Test-Path $script:VersionDir)) {
        New-Item -ItemType Directory -Path $script:VersionDir -Force | Out-Null
    }
}

function Read-Json {
    param([string]$Path, [object]$Default)
    if (Test-Path $Path) {
        $raw = Get-Content $Path -Raw
        if ([string]::IsNullOrWhiteSpace($raw)) { return $Default }
        return $raw | ConvertFrom-Json -AsHashtable
    }
    return $Default
}

function Write-JsonFile {
    param([string]$Path, [object]$Obj)
    $Obj | ConvertTo-Json -Depth 10 | Set-Content -Path $Path -Encoding UTF8
}

function Get-CandidateDlls {
    param([string]$TypeFqn, [string]$BinDir)
    # Namespace-based guess: walk from longest namespace prefix down to shortest.
    # E.g. TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel →
    #   TaleWorlds.CampaignSystem.GameComponents.dll, TaleWorlds.CampaignSystem.dll, TaleWorlds.dll
    $clean = $TypeFqn -replace '`\d+$', ''
    $parts = $clean -split '\.'
    if ($parts.Count -lt 2) { return @() }
    $candidates = @()
    for ($i = $parts.Count - 2; $i -ge 0; $i--) {
        $name = (($parts[0..$i]) -join '.') + '.dll'
        $full = Join-Path $BinDir $name
        if (Test-Path $full) { $candidates += $full }
    }
    return $candidates
}

function Try-Decompile {
    param([string]$DllPath, [string]$TypeFqn)
    # ilspycmd always exits 0. Detect hits POSITIVELY — the first non-empty,
    # non-whitespace line of a real decompile starts with a C# token (`using`,
    # `namespace`, attribute, modifier, type keyword, comment). Anything else —
    # "Could not find type definition", "MetadataFileNotSupportedException" for
    # native DLLs, raw stack frames — is a miss.
    $output = & ilspycmd $DllPath -t $TypeFqn 2>&1 | Out-String
    if ([string]::IsNullOrWhiteSpace($output)) { return $null }
    $firstLine = ($output -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1).TrimStart()
    if ($firstLine -match '^(using |namespace |//|/\*|\[|public |internal |private |protected |class |struct |interface |enum |abstract |sealed |static |partial |readonly |unsafe |#)') {
        return $output
    }
    return $null
}

function Invoke-Path {
    param([string[]]$RestArgs)

    $explicitDll = $null
    $typeArgs = @()
    for ($i = 0; $i -lt $RestArgs.Count; $i++) {
        if ($RestArgs[$i] -eq '-Dll' -or $RestArgs[$i] -eq '--dll') {
            $explicitDll = $RestArgs[$i + 1]
            $i++
        } else {
            $typeArgs += $RestArgs[$i]
        }
    }
    if ($typeArgs.Count -lt 1) {
        throw "Usage: taom-src path <FullyQualifiedType> [-Dll <FileName>]"
    }
    $typeFqn = $typeArgs[0]

    # Resolve bin first so we know which version's cache to look in.
    $bin = Get-BinDir
    Resolve-Version -BinDir $bin
    Initialize-Cache

    $cacheFile = Join-Path $script:VersionDir ($typeFqn + '.cs')
    if (Test-Path $cacheFile) {
        Write-Output $cacheFile
        return
    }

    Assert-Ilspycmd

    $dllPath = $null
    $dllName = $null

    if ($explicitDll) {
        $dllPath = Join-Path $bin $explicitDll
        if (-not (Test-Path $dllPath)) {
            throw "Explicit DLL not found: $dllPath"
        }
        $dllName = $explicitDll
    } else {
        # 1. Check DLL index cache (type → dll mapping from prior lookups)
        $index = Read-Json -Path $IndexPath -Default ([ordered]@{})
        if ($index.Contains($typeFqn)) {
            $cached = Join-Path $bin $index[$typeFqn]
            if (Test-Path $cached) {
                $dllPath = $cached
                $dllName = $index[$typeFqn]
                Write-Progress2 "index hit → $dllName"
            }
        }
    }

    if (-not $dllPath) {
        # 2. Try namespace-derived candidates (fast path for the common case)
        $candidates = Get-CandidateDlls -TypeFqn $typeFqn -BinDir $bin
        foreach ($cand in $candidates) {
            Write-Progress2 "probing $([System.IO.Path]::GetFileName($cand))"
            $output = Try-Decompile -DllPath $cand -TypeFqn $typeFqn
            if ($output) {
                $dllPath = $cand
                $dllName = [System.IO.Path]::GetFileName($cand)
                break
            }
        }
    }

    if (-not $dllPath) {
        # 3. Fallback: iterate every DLL alphabetically
        Write-Progress2 "namespace probes missed; scanning all DLLs"
        $all = Get-ChildItem -Path $bin -Filter '*.dll' | Sort-Object Name
        foreach ($dll in $all) {
            Write-Progress2 "probing $($dll.Name)"
            $output = Try-Decompile -DllPath $dll.FullName -TypeFqn $typeFqn
            if ($output) {
                $dllPath = $dll.FullName
                $dllName = $dll.Name
                break
            }
        }
    }

    if (-not $dllPath) {
        throw "Type '$typeFqn' not found in any DLL under $bin"
    }

    # Decompile once more (or reuse output) and cache.
    $output = Try-Decompile -DllPath $dllPath -TypeFqn $typeFqn
    if (-not $output) {
        throw "Type '$typeFqn' suddenly missing from $dllName (decompile produced empty output)"
    }
    Set-Content -Path $cacheFile -Value $output -Encoding UTF8

    # Update sources.json (single source-of-truth list of cached entries).
    $sources = Read-Json -Path $SourcesPath -Default @()
    $entry = [ordered]@{
        type      = $typeFqn
        dll       = $dllName
        version   = $script:Version
        cached_at = (Get-Date).ToString('o')
    }
    $sources = @($sources | Where-Object { $_.type -ne $typeFqn }) + ,$entry
    Write-JsonFile -Path $SourcesPath -Obj $sources

    # Update dll-index.json (type → dll, used as fast path on the NEXT cache miss).
    $index = Read-Json -Path $IndexPath -Default ([ordered]@{})
    $index[$typeFqn] = $dllName
    Write-JsonFile -Path $IndexPath -Obj $index

    Write-Output $cacheFile
}

function Invoke-List {
    param([string[]]$RestArgs)
    $asJson = $RestArgs -contains '-Json' -or $RestArgs -contains '--json'
    $sources = Read-Json -Path $SourcesPath -Default @()
    if ($asJson) {
        $sources | ConvertTo-Json -Depth 10
        return
    }
    if (-not $sources -or $sources.Count -eq 0) {
        Write-Output "(no cached sources)"
        return
    }
    $sources | ForEach-Object {
        [pscustomobject]@{
            Type      = $_.type
            Dll       = $_.dll
            Version   = $_.version
            CachedAt  = $_.cached_at
        }
    } | Format-Table -AutoSize
}

function Invoke-Remove {
    param([string[]]$RestArgs)
    if ($RestArgs.Count -lt 1) { throw "Usage: taom-src remove <FullyQualifiedType>" }
    $typeFqn = $RestArgs[0]
    $bin = Get-BinDir
    Resolve-Version -BinDir $bin
    $cacheFile = Join-Path $script:VersionDir ($typeFqn + '.cs')
    if (Test-Path $cacheFile) {
        Remove-Item $cacheFile -Force
        Write-Progress2 "removed $cacheFile"
    }
    $sources = Read-Json -Path $SourcesPath -Default @()
    $sources = @($sources | Where-Object { $_.type -ne $typeFqn })
    Write-JsonFile -Path $SourcesPath -Obj $sources
    $index = Read-Json -Path $IndexPath -Default ([ordered]@{})
    if ($index.Contains($typeFqn)) {
        $index.Remove($typeFqn) | Out-Null
        Write-JsonFile -Path $IndexPath -Obj $index
    }
}

function Invoke-Clean {
    if (Test-Path $CacheRoot) {
        Remove-Item -Recurse -Force $CacheRoot
        Write-Progress2 "removed $CacheRoot"
    } else {
        Write-Progress2 "cache already empty"
    }
}

switch ($Command) {
    'path'   { Invoke-Path   -RestArgs $Args }
    'list'   { Invoke-List   -RestArgs $Args }
    'remove' { Invoke-Remove -RestArgs $Args }
    'clean'  { Invoke-Clean }
}
