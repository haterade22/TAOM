<#
.SYNOPSIS
    Capture harness for the native-commit attribution matrix (docs/investigations/native-commit-audit-2026-08.md, Phase 2).

.DESCRIPTION
    Stamps one CSV row per measurement station while Bannerlord runs, and optionally takes a VMMap
    snapshot at the same instant. Capture only: nothing here analyses anything, because the 3-hour
    in-game window is the scarce resource and every artifact can be read afterwards.

    Three rules it enforces, each because a prior run was damaged by its absence:

      1. A station with no Bannerlord process is written as UNMEASURED, never as a row of zeros.
         An invented zero in a matrix is indistinguishable from a real reading of zero.
      2. Labels are validated against the SAME grammar the in-game console command accepts
         (MemoryProbeReportFormatter.IsValidLabel: at most 32 chars from A-Z a-z 0-9 _ . : -), so
         'Invoke-CommitMatrix -Station B-enc-p1' and 'taom.print_memory B-enc-p1' produce two rows
         that join on one token.
      3. -Report stamps every artifact FRESH or STALE against a session cutoff. The 2026-08-08
         Procmon capture produced a confident "zero operations" verdict from a capture taken while
         the game was not running; a stale artifact read as this run's evidence looks exactly like
         a result.

.PARAMETER Station
    Station label to stamp, e.g. 'B-menu', 'B-enc-p1', 'B-enc-p1-close60'.

.PARAMETER Vmmap
    Also take a VMMap snapshot (.csv and .mmp) for this station. Needs an elevated shell.

.PARAMETER Out
    Capture directory. Defaults to a dated folder on the Desktop.

.PARAMETER Report
    Print the per-station deltas captured so far, with freshness stamps. Takes no measurement.

.PARAMETER SinceMinutes
    Freshness cutoff for -Report. Artifacts older than this are marked STALE. Default 240.

.EXAMPLE
    . .\tools\Invoke-CommitMatrix.ps1          # dot-source once, then:
    Invoke-CommitMatrix -Station B-menu
    Invoke-CommitMatrix -Station B-map-baseline -Vmmap
    Invoke-CommitMatrix -Report

.NOTES
    Run the shell as administrator or VMMap may fail to attach, and a failed attach at the peak
    station is not recoverable. Keep the game windowed/borderless for the whole session: display
    mode changes render-target allocation, so a mid-session change invalidates every delta.
#>

# NOTE: no script-level Set-StrictMode. This file is meant to be DOT-SOURCED, and a strict-mode
# change made at script scope leaks into the operator's shell for the rest of the session, which
# would break unrelated commands mid-capture. Each function sets it locally instead.

$script:CommitMatrixDefaultOut =
    Join-Path ([Environment]::GetFolderPath('Desktop')) 'commit-matrix'

# Mirror of MemoryProbeReportFormatter.MaxLabelLength / IsValidLabel. Kept identical on purpose:
# the PowerShell label and the in-game taom.print_memory label must be the same joinable token.
$script:CommitMatrixMaxLabel = 32

function Test-CommitMatrixLabel {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Label)

    if ([string]::IsNullOrEmpty($Label)) { return $false }
    if ($Label.Length -gt $script:CommitMatrixMaxLabel) { return $false }

    # Character-by-character, exactly like MemoryProbeReportFormatter.IsValidLabel, and
    # deliberately NOT a regex. `^[A-Za-z0-9_.:-]+$` looked equivalent and was not: in .NET
    # regex `$` matches before a single trailing newline, so "B-menu`n" PASSED here while the
    # C# mirror rejected it, and the newline then injected a physical line break into
    # stations.csv that any line-based reader sees as an extra row. Using the same char
    # predicate as the C# side also fixes the opposite divergence: char::IsLetterOrDigit is
    # Unicode-aware, so a label the in-game command accepts is accepted here too.
    foreach ($c in $Label.ToCharArray()) {
        if ([char]::IsLetterOrDigit($c)) { continue }
        if ($c -ceq '_' -or $c -ceq '.' -or $c -ceq ':' -or $c -ceq '-') { continue }
        return $false
    }
    return $true
}

function Get-CommitMatrixProcess {
    # Bannerlord.exe is the shipping client. If more than one matches (client plus editor, or a
    # zombie left over from a crash beside a fresh relaunch) picking one silently would mix two
    # processes' memory curves into one delta with nothing in the output hinting at it, so say so
    # loudly. -Report also prints the pid per row and flags a change, which is the actual
    # cross-check; before this it captured procid to the CSV and never showed it.
    $all = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue)
    if ($all.Count -gt 1) {
        Write-Host ("WARNING: {0} Bannerlord processes running (PIDs {1}). Measuring {2}. " -f
            $all.Count, ($all.Id -join ', '), $all[0].Id) -ForegroundColor Yellow
        Write-Host "  Close the others, or this station is not comparable with the rest." -ForegroundColor Yellow
    }
    $all | Select-Object -First 1
}

function Invoke-CommitMatrix {
    [CmdletBinding(DefaultParameterSetName = 'Stamp')]
    param(
        [Parameter(ParameterSetName = 'Stamp', Mandatory, Position = 0)]
        [string]$Station,

        [Parameter(ParameterSetName = 'Stamp')]
        [switch]$Vmmap,

        [Parameter(ParameterSetName = 'Report', Mandatory)]
        [switch]$Report,

        [Parameter(ParameterSetName = 'Report')]
        [int]$SinceMinutes = 240,

        [string]$Out = $script:CommitMatrixDefaultOut
    )

    Set-StrictMode -Version Latest

    if (-not (Test-Path -LiteralPath $Out)) {
        New-Item -ItemType Directory -Path $Out -Force | Out-Null
    }
    $csv = Join-Path $Out 'stations.csv'

    if ($Report) {
        Write-CommitMatrixReport -Csv $csv -Out $Out -SinceMinutes $SinceMinutes
        return
    }

    if (-not (Test-CommitMatrixLabel -Label $Station)) {
        Write-Host "REJECTED label '$Station': use at most $($script:CommitMatrixMaxLabel) characters from A-Z a-z 0-9 _ . : -" -ForegroundColor Red
        Write-Host "  (same grammar as taom.print_memory, so the two rows can be joined by label)" -ForegroundColor DarkGray
        return
    }

    $proc = Get-CommitMatrixProcess
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'

    if (-not $proc) {
        # Never fabricate a row. An UNMEASURED marker is a fact; a row of zeros is a lie that
        # survives into the results table and cannot be told from a real reading afterwards.
        Write-Host "NO BANNERLORD PROCESS - station '$Station' is UNMEASURED (a marker row is written, not a measurement)." -ForegroundColor Red
        # Same 9 keys in the same order as the measured row below: Export-Csv -Append reuses the
        # existing header, so both shapes must line up under one schema. Change one, change both.
        Write-CommitMatrixRow -Row ([pscustomobject]@{
            label = $Station; ts = $ts; procid = ''; privMB = 'UNMEASURED'; wsMB = ''
            commitUsedMB = ''; commitLimitMB = ''; availPhysMB = ''; vmmap = ''
        }) -Csv $csv
        return
    }

    $os = Get-CimInstance Win32_OperatingSystem
    $row = [pscustomobject]@{
        label         = $Station
        ts            = $ts
        procid        = $proc.Id
        privMB        = [int]($proc.PrivateMemorySize64 / 1MB)
        wsMB          = [int]($proc.WorkingSet64 / 1MB)
        commitUsedMB  = [int](($os.TotalVirtualMemorySize - $os.FreeVirtualMemory) / 1KB)
        commitLimitMB = [int]($os.TotalVirtualMemorySize / 1KB)
        availPhysMB   = [int]($os.FreePhysicalMemory / 1KB)
        vmmap         = ''
    }

    if ($Vmmap) {
        $row.vmmap = Invoke-CommitMatrixVmmap -Station $Station -ProcessId $proc.Id -Out $Out
    }

    $row | Format-Table -AutoSize | Out-Host
    Write-CommitMatrixRow -Row $row -Csv $csv
}

function Write-CommitMatrixRow {
    param(
        [Parameter(Mandatory)][psobject]$Row,
        [Parameter(Mandatory)][string]$Csv
    )

    # The CSV open in Excel takes an exclusive lock and Export-Csv throws IOException. In a
    # one-shot capture session an unhandled throw here loses that station permanently, so retry
    # once and, if it still fails, print the row so it can be hand-appended rather than lost.
    foreach ($attempt in 1, 2) {
        try {
            $Row | Export-Csv -LiteralPath $Csv -Append -NoTypeInformation
            Write-Host "  -> $Csv" -ForegroundColor DarkGray
            return
        }
        catch {
            if ($attempt -eq 1) {
                Write-Host "  CSV write failed ($($_.Exception.GetType().Name)), retrying..." -ForegroundColor Yellow
                Start-Sleep -Milliseconds 400
                continue
            }
            Write-Host "  CSV WRITE FAILED - is $Csv open in Excel? Row NOT saved:" -ForegroundColor Red
            Write-Host "    $($Row | ConvertTo-Csv -NoTypeInformation | Select-Object -Last 1)" -ForegroundColor Red
            Write-Host "  Close the file and append that line by hand, or re-stamp this station." -ForegroundColor Red
        }
    }
}

function Invoke-CommitMatrixVmmap {
    param(
        [Parameter(Mandatory)][string]$Station,
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][string]$Out
    )

    $exe = (Get-Command 'vmmap64.exe' -ErrorAction SilentlyContinue)
    if (-not $exe) { $exe = (Get-Command 'vmmap.exe' -ErrorAction SilentlyContinue) }
    if (-not $exe) {
        Write-Host "  vmmap not on PATH - snapshot SKIPPED (install Sysinternals VMMap)" -ForegroundColor Yellow
        return 'SKIPPED-no-vmmap'
    }

    # The extension chooses the format. Take both: .csv for the analyser, .mmp so the snapshot
    # can be reopened in the GUI months from now.
    $written = @()
    # Names are deterministic, so a run that writes NOTHING can be masked by a same-named file
    # from an earlier session: Test-Path alone then reports a stale artifact as this capture.
    # Require the file to be newer than this invocation and non-empty.
    $startedAt = Get-Date
    foreach ($ext in @('csv', 'mmp')) {
        $path = Join-Path $Out "vmmap-$Station.$ext"
        try {
            & $exe.Source -accepteula -p $ProcessId $path | Out-Null
            $f = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
            if ($null -ne $f -and $f.LastWriteTime -ge $startedAt -and $f.Length -gt 0) {
                $written += $path
            }
            elseif ($null -ne $f) {
                Write-Host ("  vmmap left a STALE/EMPTY .$ext (written {0}, {1} bytes) - not counted" -f
                    $f.LastWriteTime.ToString('HH:mm:ss'), $f.Length) -ForegroundColor Yellow
            }
            else {
                # vmmap exits cleanly and writes NOTHING when it cannot attach (no elevation, or
                # a protected target). Nothing throws, so without this branch a csv-succeeded /
                # mmp-failed capture reported success and the operator never learned the .mmp -
                # the one meant to be reopenable in the GUI later - was never written.
                Write-Host "  vmmap produced no .$ext (attach refused? run elevated)" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "  vmmap $ext failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    if (-not $written) {
        Write-Host "  vmmap produced NO file - are you running elevated?" -ForegroundColor Yellow
        return 'FAILED'
    }
    Write-Host "  vmmap -> $($written -join ', ')" -ForegroundColor DarkGray
    return ($written | ForEach-Object { Split-Path $_ -Leaf }) -join ';'
}

function Write-CommitMatrixReport {
    param(
        [Parameter(Mandatory)][string]$Csv,
        [Parameter(Mandatory)][string]$Out,
        [Parameter(Mandatory)][int]$SinceMinutes
    )

    if (-not (Test-Path -LiteralPath $Csv)) {
        Write-Host "No captures yet at $Csv" -ForegroundColor Yellow
        return
    }

    $cutoff = (Get-Date).AddMinutes(-$SinceMinutes)
    $rows = @(Import-Csv -LiteralPath $Csv)

    Write-Host ""
    Write-Host "Commit matrix - $($rows.Count) station(s), freshness cutoff $($cutoff.ToString('HH:mm:ss'))" -ForegroundColor Cyan
    Write-Host ""

    $prev = $null
    foreach ($r in $rows) {
        $stamp = 'STALE'
        $parsed = [datetime]::MinValue
        if ([datetime]::TryParse($r.ts, [ref]$parsed) -and $parsed -ge $cutoff) { $stamp = 'FRESH' }

        if ($r.privMB -eq 'UNMEASURED') {
            Write-Host ("  {0,-24} {1,-5} UNMEASURED (no process at stamp time)" -f $r.label, $stamp) -ForegroundColor Red
            # Deliberately does NOT update $prev: a delta measured across a gap in the record
            # would silently span an unknown interval.
            continue
        }

        $delta = ''
        if ($null -ne $prev) {
            $d = [int]$r.privMB - [int]$prev.privMB
            $delta = '{0:+#;-#;0} MB' -f $d
        }
        $colour = if ($stamp -eq 'FRESH') { 'Gray' } else { 'DarkYellow' }
        Write-Host ("  {0,-24} {1,-5} pid={2,-6} priv={3,6} MB  ws={4,6} MB  {5,10}  {6}" -f
            $r.label, $stamp, $r.procid, $r.privMB, $r.wsMB, $delta, $r.vmmap) -ForegroundColor $colour
        # A delta across two different pids is not a delta. This is the cross-check the header
        # comment promised; it did not exist until the pid was actually printed.
        if ($null -ne $prev -and $prev.procid -ne $r.procid) {
            Write-Host ("      ^ PID CHANGED ({0} -> {1}) - the delta above spans two processes and is meaningless." -f
                $prev.procid, $r.procid) -ForegroundColor Red
        }
        $prev = $r
    }

    Write-Host ""
    Write-Host "Deltas are against the PREVIOUS row, so they are only meaningful in capture order." -ForegroundColor DarkGray
    Write-Host "Absolutes do not transfer off this machine: report deltas, never plateaus." -ForegroundColor DarkGray
    Write-Host ""
}

# Dot-sourced (". .\tools\Invoke-CommitMatrix.ps1") the functions above become available.
# Invoked directly with arguments, forward them so the script also works as a one-shot.
if ($MyInvocation.InvocationName -ne '.' -and $args.Count -gt 0) {
    Invoke-CommitMatrix @args
}
