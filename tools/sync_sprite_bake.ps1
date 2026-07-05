<#
.SYNOPSIS
    Sync ONLY the editor sprite-bake outputs from the game install back into the repo.

.DESCRIPTION
    The editor's sprite-sheet generation writes to the deployed module
    (<game>\Modules\TAOM\...), not the repo. Only THREE things it produces need to
    come back to the repo:

        GUI\TAOMSpriteData.xml        (the regenerated manifest)
        AssetSources\GauntletUI\      (the packed atlas source PNGs)
        Assets\GauntletUI\            (the compiled *_tex.tpac textures)

    Everything else in the module (GUI\PreFabs, GUI\Brushes, GUI\SpriteParts source
    PNGs, ModuleData\*.json/xml) is EDITED IN THE REPO and flows the other way
    (repo -> install, via build.ps1). Copying the whole module/GUI folder back is what
    silently reverts prefab/brush/JSON edits -- this script never touches them.

    The two GauntletUI dirs are mirrored (/MIR) so a repack into fewer sheets doesn't
    leave stale atlas files behind. Only bake output lives in those dirs, so mirroring
    is safe.

.PARAMETER GameDir
    Bannerlord install root. Defaults to $env:BANNERLORD_GAME_DIR, then the known path.

.EXAMPLE
    pwsh tools/sync_sprite_bake.ps1
    pwsh tools/sync_sprite_bake.ps1 -WhatIf      # preview without copying
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$GameDir = $(if ($env:BANNERLORD_GAME_DIR) { $env:BANNERLORD_GAME_DIR } else { 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord' })
)

$ErrorActionPreference = 'Stop'

$repoModule = Join-Path $PSScriptRoot '..\Main\_Module' | Resolve-Path
$installModule = Join-Path $GameDir 'Modules\TAOM'

if (-not (Test-Path $installModule)) { throw "Install module not found: $installModule" }

Write-Host "Install : $installModule"
Write-Host "Repo    : $repoModule"
Write-Host ''

# --- 1. manifest (single file) ---
$srcManifest = Join-Path $installModule 'GUI\TAOMSpriteData.xml'
$dstManifest = Join-Path $repoModule   'GUI\TAOMSpriteData.xml'
if ($PSCmdlet.ShouldProcess($dstManifest, 'copy manifest')) {
    Copy-Item -LiteralPath $srcManifest -Destination $dstManifest -Force
    Write-Host "  manifest  -> GUI\TAOMSpriteData.xml"
}

# --- 2 + 3. the two GauntletUI output dirs (mirror) ---
foreach ($rel in @('AssetSources\GauntletUI', 'Assets\GauntletUI')) {
    $src = Join-Path $installModule $rel
    $dst = Join-Path $repoModule   $rel
    if (-not (Test-Path $src)) { Write-Warning "missing in install: $rel"; continue }
    if ($PSCmdlet.ShouldProcess($dst, "mirror $rel")) {
        # /MIR mirror, /NFL /NDL /NJH /NJS /NP quiet, /R:1 /W:1 no long retries
        robocopy $src $dst /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE) for $rel" }
        Write-Host "  mirrored  -> $rel"
    }
}
$global:LASTEXITCODE = 0  # robocopy 0-7 are success; don't leak a nonzero code

Write-Host ''
Write-Host 'Done. Verify the manifest matches (should be identical):' -ForegroundColor Green
Write-Host "  git status --short Main/_Module/GUI/TAOMSpriteData.xml Main/_Module/AssetSources Main/_Module/Assets"
Write-Host ''
Write-Host 'NOTE: prefabs / brushes / SpriteParts / ModuleData were NOT touched -- those are repo-authoritative (repo -> install via build.ps1).' -ForegroundColor Yellow
