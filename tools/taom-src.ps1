#Requires -Version 7
<#
.SYNOPSIS
    Decompile and cache TaleWorlds types via a single command. Auto-detects version.
.DESCRIPTION
    Mirrors the opensrc pattern (https://github.com/vercel-labs/opensrc) for TAOM's
    closed-source TaleWorlds dependencies. Caches at $env:USERPROFILE\.taom-src\<version>-<stamp>\
    where <version> is auto-detected from Version.xml in the resolved bin dir and <stamp> is
    TaleWorlds.Library.dll's write time (e.g. v1.5.6-202611041234). Multiple builds can coexist.

    Bin dir resolution order:
      1. $env:BANNERLORD_OVERRIDE_DIR\bin\Win64_Shipping_Client (if set)
      2. $env:BANNERLORD_GAME_DIR\bin\Win64_Shipping_Client (default)

    Assembly SEARCH order is wider than the bin dir: the primary bin dir first, then every
    <root>\Modules\<Name>\bin\Win64_Shipping_Client (engine modules shipping TaleWorlds.*
    assemblies before third-party ones). This matters because several engine assemblies ship
    ONLY under a module — TaleWorlds.MountAndBlade.View.dll, which owns CharacterTableau and
    AgentVisuals, lives in Modules\Native\bin and is absent from bin\ entirely.
.EXAMPLE
    # Cache from current $env:BANNERLORD_GAME_DIR (whatever version it is)
    pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel
.EXAMPLE
    # The short name works when one engine type holds it; an ambiguous one names its candidates
    pwsh tools/taom-src.ps1 path DefaultPartyWageModel
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
$script:SearchDirs = $null
$SourcesPath = Join-Path $CacheRoot 'sources.json'
# The same list as VANILLA_MODULES in tools/engine_body_diff.py. A mod can ship its own copy of an engine
# assembly (Coop carries about 40), and that copy goes stale, so it must never outrank the game's own.
$VanillaModules = 'Native', 'SandBox', 'SandBoxCore', 'StoryMode', 'CustomBattle', 'BirthAndDeath', 'Multiplayer', 'NavalDLC', 'FastMode'

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

function Get-SearchDirs {
    # Every directory that may hold a managed assembly, in probe order.
    #
    # The primary bin dir alone is NOT sufficient: TaleWorlds ships several engine assemblies
    # only under a module. TaleWorlds.MountAndBlade.View.dll — which owns CharacterTableau,
    # AgentVisuals and the whole tableau render path — exists solely at
    # Modules\Native\bin\Win64_Shipping_Client and is absent from bin\. Searching bin\ only made
    # `taom-src path <those types>` fail with "not found in any DLL", sending readers to raw
    # ilspycmd. The game's own modules probe before every other module, so an engine type never
    # loses a race to a mod that ships a copy of its assembly.
    param([string]$BinDir)
    if ($script:SearchDirs) { return $script:SearchDirs }

    $dirs = @($BinDir)
    $gameRoot    = Split-Path (Split-Path $BinDir -Parent) -Parent
    $modulesRoot = Join-Path $gameRoot 'Modules'

    if (Test-Path $modulesRoot) {
        $engine = @()
        $other  = @()
        foreach ($moduleDir in (Get-ChildItem -Path $modulesRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name)) {
            $moduleBin = Join-Path $moduleDir.FullName 'bin\Win64_Shipping_Client'
            if (-not (Test-Path $moduleBin)) { continue }
            $hasDlls = Get-ChildItem -Path $moduleBin -Filter '*.dll' -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $hasDlls) { continue }
            if ($moduleDir.Name -in $VanillaModules) { $engine += $moduleBin } else { $other += $moduleBin }
        }
        $dirs += $engine
        $dirs += $other
    }

    $script:SearchDirs = $dirs
    # Label each dir by the module that owns it (…\Modules\<Name>\bin\Win64_Shipping_Client →
    # "<Name>"); the primary dir has no module and is reported as "bin".
    $labels = $dirs | ForEach-Object {
        $owner = Split-Path (Split-Path (Split-Path $_ -Parent) -Parent) -Leaf
        if ($_ -eq $BinDir) { 'bin' } else { $owner }
    }
    Write-Progress2 "search dirs: $($dirs.Count) ($($labels -join ', '))"
    return $script:SearchDirs
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
    # A Steam hotfix keeps Version.xml and re-stamps the DLLs (v1.5.6, 2026-11-04), so the version alone would keep serving the old build's sources.
    $library = Join-Path $BinDir 'TaleWorlds.Library.dll'
    $folder = if (Test-Path $library) { $detected + '-' + (Get-Item $library).LastWriteTimeUtc.ToString('yyyyMMddHHmm') } else { $detected }
    $script:VersionDir = Join-Path $CacheRoot $folder
    # Per version: a shared index kept routing types to Coop's stale copies after a bump (2026-10-14).
    $script:IndexPath = Join-Path $script:VersionDir 'dll-index.json'
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
    param([string]$TypeFqn, [string[]]$SearchDirs)
    # Namespace-based guess: walk from longest namespace prefix down to shortest.
    # E.g. TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel →
    #   TaleWorlds.CampaignSystem.GameComponents.dll, TaleWorlds.CampaignSystem.dll, TaleWorlds.dll
    # Namespace length is the OUTER loop so the most specific assembly name still wins even when
    # it lives in a module dir and a shorter-named one sits in bin\.
    $clean = $TypeFqn -replace '`\d+$', ''
    $parts = $clean -split '\.'
    if ($parts.Count -lt 2) { return @() }
    $candidates = @()
    for ($i = $parts.Count - 2; $i -ge 0; $i--) {
        $name = (($parts[0..$i]) -join '.') + '.dll'
        foreach ($dir in $SearchDirs) {
            $full = Join-Path $dir $name
            if (Test-Path $full) { $candidates += $full }
        }
    }
    return $candidates
}

function Resolve-IndexedDll {
    # dll-index.json historically stored a bare filename (resolved against the bin dir).
    # It now stores a full path so module-dir hits survive. Accept both.
    param([string]$Recorded, [string[]]$SearchDirs)
    if ([string]::IsNullOrWhiteSpace($Recorded)) { return $null }
    if ([System.IO.Path]::IsPathRooted($Recorded)) {
        if (Test-Path $Recorded) { return $Recorded }
        return $null
    }
    foreach ($dir in $SearchDirs) {
        $full = Join-Path $dir $Recorded
        if (Test-Path $full) { return $full }
    }
    return $null
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

function Get-EngineTypeIndex {
    # short name, full name and DLL of every top-level engine type, one per line, built once per game
    # version from assembly metadata (a few seconds), because probing with ilspycmd takes minutes.
    param([string[]]$SearchDirs, [switch]$Rebuild)
    $path = Join-Path $script:VersionDir 'engine-types.tsv'
    if (-not $Rebuild -and (Test-Path $path)) { return Get-Content -LiteralPath $path }

    Write-Progress2 'building the short-name index from assembly metadata'
    Add-Type -AssemblyName System.Reflection.Metadata
    $rows = [System.Collections.Generic.List[string]]::new()
    foreach ($dir in $SearchDirs) {
        foreach ($dll in (Get-ChildItem -Path $dir -Filter '*.dll' -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^(TaleWorlds|SandBox|StoryMode|NavalDLC)' })) {
            $stream = [System.IO.File]::OpenRead($dll.FullName)
            try {
                $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
                if (-not $pe.HasMetadata) { continue }
                $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
                foreach ($handle in $md.TypeDefinitions) {
                    $type = $md.GetTypeDefinition($handle)
                    if ($type.IsNested) { continue }
                    $name = $md.GetString($type.Name)
                    $namespace = $md.GetString($type.Namespace)
                    if ($name.StartsWith('<')) { continue }
                    $full = if ($namespace) { "$namespace.$name" } else { $name }
                    $rows.Add("$($name -replace '`\d+$', '')`t$full`t$($dll.FullName)")
                }
            } finally {
                $stream.Dispose()
            }
        }
    }
    # Through a temp file, so a parallel first run never reads a cut index.
    $temp = "$path.$PID"
    Set-Content -LiteralPath $temp -Value $rows -Encoding UTF8
    Move-Item -LiteralPath $temp -Destination $path -Force
    return $rows
}

function Resolve-ShortName {
    # `path Mission`: AGENTS.md and the rules write the short name. One match resolves to it, and more
    # than one names them all, since only the reader knows which is meant.
    param([string]$Short, [string[]]$SearchDirs)
    $prefix = "$Short`t"
    $match = { $_.StartsWith($prefix, [System.StringComparison]::Ordinal) }
    $hits = @(Get-EngineTypeIndex -SearchDirs $SearchDirs | Where-Object $match)
    if ($hits.Count -eq 0) {
        # A module added since the build, or a file cut by a crash, must not answer "no such type".
        $hits = @(Get-EngineTypeIndex -SearchDirs $SearchDirs -Rebuild | Where-Object $match)
    }
    $names = @($hits | ForEach-Object { ($_ -split "`t")[1] } | Sort-Object -Unique)
    if ($names.Count -eq 0) {
        throw "No top-level engine type is named '$Short'. For a nested type, read its outer type. Outside the TaleWorlds, SandBox, StoryMode and NavalDLC assemblies, name it in full with -Dll."
    }
    if ($names.Count -gt 1) {
        throw "'$Short' is ambiguous. Name one in full: $($names -join ', ')"
    }
    return [pscustomobject]@{ Name = $names[0]; Dll = (($hits | Select-Object -First 1) -split "`t")[2] }
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
        throw "Usage: taom-src path <Type or FullyQualifiedType>, or taom-src path <FullyQualifiedType> -Dll <FileName>"
    }
    $typeFqn = $typeArgs[0]
    $firstDll = $null

    # Resolve bin first so we know which version's cache to look in.
    $bin = Get-BinDir
    Resolve-Version -BinDir $bin
    Initialize-Cache

    if ($typeFqn -notmatch '\.' -and -not $explicitDll) {
        $resolved = Resolve-ShortName -Short $typeFqn -SearchDirs (Get-SearchDirs -BinDir $bin)
        $typeFqn = $resolved.Name
        $firstDll = $resolved.Dll
    }

    $cacheFile = Join-Path $script:VersionDir ($typeFqn + '.cs')
    if (Test-Path $cacheFile) {
        Write-Output $cacheFile
        return
    }

    Assert-Ilspycmd

    $dllPath = $null
    $dllName = $null

    $searchDirs = Get-SearchDirs -BinDir $bin

    if ($explicitDll) {
        # An explicit -Dll may be a bare filename (resolved across every search dir) or a path.
        $dllPath = Resolve-IndexedDll -Recorded $explicitDll -SearchDirs $searchDirs
        if (-not $dllPath) {
            throw "Explicit DLL '$explicitDll' not found in any search dir ($($searchDirs -join '; '))"
        }
        $dllName = [System.IO.Path]::GetFileName($dllPath)
    } else {
        # 1. Check DLL index cache (type → dll mapping from prior lookups)
        $index = Read-Json -Path $IndexPath -Default ([ordered]@{})
        if ($index.Contains($typeFqn)) {
            $cached = Resolve-IndexedDll -Recorded $index[$typeFqn] -SearchDirs $searchDirs
            if ($cached) {
                $dllPath = $cached
                $dllName = [System.IO.Path]::GetFileName($cached)
                Write-Progress2 "index hit → $dllName"
            }
        }
    }

    if (-not $dllPath) {
        # 2. Try the short-name row's DLL, then namespace-derived candidates (fast path for the common case)
        $candidates = @($firstDll | Where-Object { $_ -and (Test-Path -LiteralPath $_) }) + @(Get-CandidateDlls -TypeFqn $typeFqn -SearchDirs $searchDirs)
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
        # 3. Fallback: iterate every DLL in every search dir (bin first, then engine modules,
        #    then third-party module dirs — see Get-SearchDirs for why the order matters).
        Write-Progress2 "namespace probes missed; scanning all DLLs across $($searchDirs.Count) dirs"
        :scan foreach ($dir in $searchDirs) {
            foreach ($dll in (Get-ChildItem -Path $dir -Filter '*.dll' -ErrorAction SilentlyContinue | Sort-Object Name)) {
                Write-Progress2 "probing $($dll.Name)"
                $output = Try-Decompile -DllPath $dll.FullName -TypeFqn $typeFqn
                if ($output) {
                    $dllPath = $dll.FullName
                    $dllName = $dll.Name
                    break scan
                }
            }
        }
    }

    if (-not $dllPath) {
        throw "Type '$typeFqn' not found in any DLL under: $($searchDirs -join '; ')"
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
    # Stores the FULL path, not a bare filename: a module-dir assembly is unresolvable from the
    # name alone. Resolve-IndexedDll still accepts legacy bare-filename entries.
    $index = Read-Json -Path $IndexPath -Default ([ordered]@{})
    $index[$typeFqn] = $dllPath
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
