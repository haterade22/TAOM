<#
.SYNOPSIS
Registers (or removes) the Windows scheduled task that runs the PR review watcher every 30 minutes (#1543).

.DESCRIPTION
The task runs tools/pr_review_watch.py from this checkout with pythonw (no console window), only while the
maintainer is logged on: the claude and gh logins are the maintainer's, and the notification needs their desktop.
A run that is still going when the next one is due makes the next one skip. Reports and the log go to the -Out
folder. Register it from the main checkout: a linked worktree is removed after its merge, and the task would fail.
Doc: docs/features/pr-review-automation.md.

.EXAMPLE
pwsh tools/register_pr_review_task.ps1
pwsh tools/register_pr_review_task.ps1 -Unregister
#>
param(
    [string]$Out = 'E:\TAOM-PR-Reviews',
    [int]$EveryMinutes = 30,
    [switch]$Unregister
)

$ErrorActionPreference = 'Stop'
$name = 'TAOM PR review watcher'

if ($Unregister) {
    if (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $name -Confirm:$false
        Write-Host "Removed the task '$name'."
    } else {
        Write-Host "No task named '$name'."
    }
    return
}

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$gitDir = git -C $repo rev-parse --path-format=absolute --git-dir
$commonDir = git -C $repo rev-parse --path-format=absolute --git-common-dir
if ($LASTEXITCODE -ne 0 -or -not $gitDir) { throw "$repo is not a git checkout." }
if ($gitDir -ne $commonDir) { throw "$repo is a linked worktree, which is removed after its merge. Run this from the main checkout." }
$script = Join-Path $repo 'tools\pr_review_watch.py'
$python = (Get-Command python -ErrorAction Stop).Source
if ($python -like '*\WindowsApps\*') { throw "python resolves to the Microsoft Store alias ($python); put a real Python first on PATH." }
$pythonw = Join-Path (Split-Path $python) 'pythonw.exe'
if (-not (Test-Path $pythonw)) { throw "No pythonw.exe beside $python." }
foreach ($tool in 'gh', 'claude') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not on PATH." }
}

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$log = Join-Path $Out 'watch.log'
$arguments = "`"$script`" run --out `"$Out`" --log `"$log`""
$action = New-ScheduledTaskAction -Execute $pythonw -Argument $arguments -WorkingDirectory $repo
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes $EveryMinutes)
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive
Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
    -Description 'First review of each new TAOM pull request head (tools/pr_review_watch.py, #1543). Reads only; posts nothing.' -Force | Out-Null
Write-Host "Registered '$name': every $EveryMinutes minutes while you are logged on."
Write-Host "  $pythonw $arguments"
Write-Host "Reports: $Out   Status: python tools/pr_review_watch.py status --out `"$Out`"   Remove: pwsh tools/register_pr_review_task.ps1 -Unregister"
