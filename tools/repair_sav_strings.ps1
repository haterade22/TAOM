<#
.SYNOPSIS
  Offline repair for Bannerlord saves bricked by the WarOfTheRing momentum >32 KB bug
  (TAOM v2.0.9). PowerShell + .NET only — NO Python, NO install (ships with Windows 10/11).

.DESCRIPTION
  THE BUG: TAOM's WarOfTheRingMomentum serialized its whole event log as ONE SyncData
  string. The engine's ArchiveSerializer writes each save-archive entry's length as
  (short)Data.Length — a signed int16 truncation — but writes the data in full. Any entry
  > 32,767 bytes gets a wrong length on disk; on load the reader desyncs and the save fails
  ("A problem occured while trying to load the saved game."). A developed campaign's momentum
  log crosses that as one string around day ~50, and every save after is unloadable.

  THE REPAIR: the momentum log is a cosmetic war-progress tracker — no campaign data lives in
  it. This resets the oversized momentum string to empty (the mod re-derives fresh state on
  the next daily tick), re-frames the save, and writes <name>_fixed.sav. Zero campaign-data
  loss; the fixed save loads on the vanilla engine. Uses .NET DeflateStream — the SAME library
  the engine uses — so the rewritten deflate stream is guaranteed compatible.

.PARAMETER Path
  Path to the .sav file.
.PARAMETER Repair
  Write <name>_fixed.sav. Without this, only diagnoses (non-destructive).
.PARAMETER Force
  Reset ANY oversized string entry, not just recognized momentum.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File repair_sav_strings.ps1 "Elves209.sav"
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File repair_sav_strings.ps1 "Elves209.sav" -Repair
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)] [string] $Path,
    [switch] $Repair,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
try { Add-Type -AssemblyName System.IO.Compression -ErrorAction SilentlyContinue | Out-Null } catch {}

$INT16_MAX = 32767
$WRAP = 65536
$ENTRY_EXT_TXT = 10
$MOMENTUM_KEYS = @('"free.momentum"', '"evil.momentum"', '"warStarted"', '"warEnded"')

# ---- little-endian primitives ----
function R3([byte[]]$b, [int]$p) {
    $v = [int]$b[$p] -bor ([int]$b[$p + 1] -shl 8) -bor ([int]$b[$p + 2] -shl 16)
    if ($b[$p] -eq 0xFF -and $b[$p + 1] -eq 0xFF -and $b[$p + 2] -eq 0xFF) { $v = $v - 0x1000000 }
    return $v
}
function W3([int]$v) { return [byte[]]@([byte]($v -band 0xFF), [byte](($v -shr 8) -band 0xFF), [byte](($v -shr 16) -band 0xFF)) }

# ---- deflate (same library the engine uses) ----
function Inflate([byte[]]$data, [int]$off) {
    $inMs = New-Object System.IO.MemoryStream(, $data)
    $inMs.Position = $off
    $ds = New-Object System.IO.Compression.DeflateStream($inMs, [System.IO.Compression.CompressionMode]::Decompress)
    $outMs = New-Object System.IO.MemoryStream
    $ds.CopyTo($outMs)
    $ds.Dispose(); $inMs.Dispose()
    $r = $outMs.ToArray(); $outMs.Dispose()
    return $r
}
function Deflate([byte[]]$data) {
    $outMs = New-Object System.IO.MemoryStream
    $ds = New-Object System.IO.Compression.DeflateStream($outMs, [System.IO.Compression.CompressionLevel]::Optimal, $true)
    $ds.Write($data, 0, $data.Length)
    $ds.Dispose()
    $r = $outMs.ToArray(); $outMs.Dispose()
    return $r
}

# ---- Strings section boundaries (Strings is the LAST GameData section) ----
function Get-StringsSection([byte[]]$raw) {
    $pos = 0
    $n = [System.BitConverter]::ToInt32($raw, $pos); $pos += 4 + $n            # Header
    foreach ($label in 'ObjectData', 'ContainerData') {
        $count = [System.BitConverter]::ToInt32($raw, $pos); $pos += 4
        for ($i = 0; $i -lt $count; $i++) { $n = [System.BitConverter]::ToInt32($raw, $pos); $pos += 4 + $n }
    }
    $lenPrefixPos = $pos
    $n = [System.BitConverter]::ToInt32($raw, $pos)
    $start = $pos + 4
    $end = $start + $n
    if ($end -ne $raw.Length) { throw "section walk ended at $end, expected $($raw.Length)" }
    return @{ Start = $start; End = $end; LenPrefixPos = $lenPrefixPos }
}

function Header-Ok([byte[]]$buf, [int]$q, [int]$expectedId, [int]$archEnd, [hashtable]$validFolders) {
    if ($q + 9 -gt $archEnd) { return $false }
    if ($buf[$q + 6] -gt $ENTRY_EXT_TXT) { return $false }
    if (-not $validFolders.ContainsKey((R3 $buf $q))) { return $false }
    return (R3 $buf ($q + 3)) -eq $expectedId
}

# Parse an archive: recover int16-truncated entry lengths via the sequential-entry-id anchor.
function Parse-Archive([byte[]]$buf, [int]$archStart, [int]$archEnd) {
    $p = $archStart
    $folderCount = [System.BitConverter]::ToInt32($buf, $p); $p += 4
    $validFolders = @{ -1 = $true }
    for ($i = 0; $i -lt $folderCount; $i++) { $validFolders[(R3 $buf ($archStart + 4 + $i * 10 + 3))] = $true }
    $folderLen = 4 + $folderCount * 10
    $p = $archStart + $folderLen
    $entryCount = [System.BitConverter]::ToInt32($buf, $p); $p += 4
    $maxK = [int](($archEnd - $archStart) / $WRAP) + 2

    $entries = New-Object System.Collections.ArrayList
    $pos = $p
    for ($j = 0; $j -lt $entryCount; $j++) {
        $folderId = R3 $buf $pos
        $entryId = R3 $buf ($pos + 3)
        $ext = [int]$buf[$pos + 6]
        $base = [int]([System.BitConverter]::ToInt16($buf, $pos + 7)) -band 0xFFFF
        $dstart = $pos + 9
        $isLast = ($j -eq $entryCount - 1)
        $chosen = -1
        for ($k = 0; $k -lt $maxK; $k++) {
            $dlen = $base + $k * $WRAP
            $qend = $dstart + $dlen
            if ($qend -gt $archEnd) { break }
            $ok = if ($isLast) { $qend -eq $archEnd } else { Header-Ok $buf $qend ($entryId + 1) $archEnd $validFolders }
            if ($ok) { $chosen = $dlen; break }
        }
        if ($chosen -lt 0) { throw "could not recover length for entry index $j (id=$entryId, ext=$ext)" }
        $data = New-Object byte[] $chosen
        if ($chosen -gt 0) { [Array]::Copy($buf, $dstart, $data, 0, $chosen) }
        [void]$entries.Add(@{ FolderId = $folderId; EntryId = $entryId; Ext = $ext; Data = $data })
        $pos = $dstart + $chosen
    }
    if ($pos -ne $archEnd) { throw "archive parse ended at $pos, expected $archEnd" }
    $folderBytes = New-Object byte[] $folderLen
    [Array]::Copy($buf, $archStart, $folderBytes, 0, $folderLen)
    return @{ FolderBytes = $folderBytes; Entries = $entries }
}

function Serialize-Archive([hashtable]$archive) {
    $out = New-Object System.IO.MemoryStream
    $out.Write($archive.FolderBytes, 0, $archive.FolderBytes.Length)
    $out.Write([System.BitConverter]::GetBytes([int]$archive.Entries.Count), 0, 4)
    foreach ($e in $archive.Entries) {
        if ($e.Data.Length -gt $INT16_MAX) { throw "entry id=$($e.EntryId) still $($e.Data.Length) B > $INT16_MAX after repair" }
        $h = New-Object byte[] 9
        [Array]::Copy((W3 $e.FolderId), 0, $h, 0, 3)
        [Array]::Copy((W3 $e.EntryId), 0, $h, 3, 3)
        $h[6] = [byte]$e.Ext
        [Array]::Copy([System.BitConverter]::GetBytes([int16]$e.Data.Length), 0, $h, 7, 2)
        $out.Write($h, 0, 9)
        $out.Write($e.Data, 0, $e.Data.Length)
    }
    $r = $out.ToArray(); $out.Dispose(); return $r
}

function Decode-StringEntry([byte[]]$data) {
    if ($data.Length -lt 4) { return $null }
    $n = [System.BitConverter]::ToInt32($data, 0)
    if ($n -lt 0 -or (4 + $n) -ne $data.Length) { return $null }
    try { return [System.Text.Encoding]::UTF8.GetString($data, 4, $n) } catch { return $null }
}
function Is-Momentum([string]$text) {
    if ($null -eq $text -or -not $text.TrimStart().StartsWith('{')) { return $false }
    foreach ($k in $MOMENTUM_KEYS) { if ($text.Contains($k)) { return $true } }
    return $false
}

# ================= driver =================
if (-not (Test-Path -LiteralPath $Path)) { Write-Host "ERROR: file not found: $Path"; exit 1 }
Write-Host "  Reading save (this can take up to a minute on a large campaign, please wait)..."
$data = [System.IO.File]::ReadAllBytes($Path)

try {
    $metaLen = [System.BitConverter]::ToInt32($data, 0)
    if ($metaLen -le 0 -or (4 + $metaLen) -gt $data.Length) { throw "metadata length $metaLen out of range" }
    $dataOff = 4 + $metaLen
    $meta = $null
    try { $meta = ([System.Text.Encoding]::UTF8.GetString($data, 4, $metaLen) | ConvertFrom-Json).List } catch {}
    $raw = Inflate $data $dataOff
    $strings = Get-StringsSection $raw
}
catch { Write-Host "ERROR: $($_.Exception.Message)"; exit 1 }

$ch = if ($meta) { $meta.CharacterName } else { '?' }
$day = if ($meta) { $meta.DayLong } else { '?' }
$ver = if ($meta) { $meta.ApplicationVersion } else { '?' }
$bld = if ($meta -and $meta.TAOM_Build) { $meta.TAOM_Build } else { '<none>' }
Write-Host ("{0,-16} {1}   day {2}   {3}   TAOM_Build={4}" -f 'Character', $ch, $day, $ver, $bld)

$archive = $null
try { $archive = Parse-Archive $raw $strings.Start $strings.End }
catch { Write-Host "  WARNING: could not parse Strings archive: $($_.Exception.Message)"; exit 1 }

$oversized = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt $archive.Entries.Count; $i++) {
    $e = $archive.Entries[$i]
    if ($e.Data.Length -gt $INT16_MAX) {
        $text = if ($e.Ext -eq $ENTRY_EXT_TXT) { Decode-StringEntry $e.Data } else { $null }
        [void]$oversized.Add(@{ Index = $i; Entry = $e; Text = $text; IsMomentum = (Is-Momentum $text) })
    }
}

if ($oversized.Count -eq 0) {
    Write-Host "  No oversized (>32,767 B) archive entries found - this save is NOT hit by the momentum bug."
    exit 0
}

Write-Host ""
Write-Host ("  Found {0} oversized entr{1} (>32,767 B - the write-time corruption):" -f $oversized.Count, $(if ($oversized.Count -eq 1) { 'y' } else { 'ies' }))
foreach ($o in $oversized) {
    $kind = if ($o.IsMomentum) { 'MOMENTUM war-tracker string' } elseif ($null -ne $o.Text) { 'string entry' } else { "non-string entry (ext=$($o.Entry.Ext))" }
    $trueLen = $o.Entry.Data.Length
    $engineLen = [System.BitConverter]::ToInt16([System.BitConverter]::GetBytes([uint16]($trueLen -band 0xFFFF)), 0)
    $fail = if ($engineLen -lt 0) { 'reads a NEGATIVE length -> OverflowException' } else { "reads only $engineLen B -> stream desync -> `"Source array was not long enough`"" }
    Write-Host ("    - Strings entry id={0}: {1:N0} B true; engine {2}  [{3}]" -f $o.Entry.EntryId, $trueLen, $fail, $kind)
}

if (-not $Repair) {
    Write-Host ""
    Write-Host "  Diagnose only. Re-run with -Repair to write a fixed copy (resets the war meter, keeps the campaign)."
    exit 2
}

# ---- repair ----
$toReset = @($oversized | Where-Object { $_.IsMomentum -or ($Force -and $null -ne $_.Text) })
$refused = @($oversized | Where-Object { $toReset -notcontains $_ })
foreach ($o in $refused) {
    $what = if ($null -ne $o.Text) { 'unrecognized string (use -Force to reset anyway)' } else { 'NON-string data - cannot safely reset (this is not the momentum bug)' }
    Write-Host ""
    Write-Host "  REFUSING to repair Strings entry id=$($o.Entry.EntryId): $what"
}
if ($toReset.Count -eq 0) { exit 3 }

$emptyString = [byte[]]@(0, 0, 0, 0)   # ReadString len=0 -> "" -> momentum resets to fresh state
foreach ($o in $toReset) { $archive.Entries[$o.Index].Data = $emptyString }

$newStrings = Serialize-Archive $archive

# Strings is the LAST section -> everything before its length prefix is byte-identical. O(n) splice.
$fixed = New-Object System.IO.MemoryStream
$fixed.Write($raw, 0, $strings.LenPrefixPos)
$fixed.Write([System.BitConverter]::GetBytes([int]$newStrings.Length), 0, 4)
$fixed.Write($newStrings, 0, $newStrings.Length)
$fixedRaw = $fixed.ToArray(); $fixed.Dispose()

# Self-check: rebuilt region must re-walk cleanly with no oversized entries.
try {
    $cs = Get-StringsSection $fixedRaw
    $chk = Parse-Archive $fixedRaw $cs.Start $cs.End
    foreach ($e in $chk.Entries) { if ($e.Data.Length -gt $INT16_MAX) { throw "an entry is still oversized" } }
}
catch { Write-Host ""; Write-Host "  ERROR: rebuilt save failed self-check ($($_.Exception.Message)) - NOT writing output. Original untouched."; exit 3 }

$body = Deflate $fixedRaw
$dir = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Path))
$stem = [System.IO.Path]::GetFileNameWithoutExtension($Path)
$outPath = Join-Path $dir ($stem + '_fixed.sav')

$outStream = New-Object System.IO.MemoryStream
$outStream.Write($data, 0, $dataOff)   # metadata block verbatim
$outStream.Write($body, 0, $body.Length)
[System.IO.File]::WriteAllBytes($outPath, $outStream.ToArray())
$outStream.Dispose()

Write-Host ""
Write-Host "  REPAIRED -> $outPath"
Write-Host ("    reset {0} momentum entr{1}; war-meter history cleared, campaign intact. Loads on the vanilla engine." -f $toReset.Count, $(if ($toReset.Count -eq 1) { 'y' } else { 'ies' }))
exit 0
