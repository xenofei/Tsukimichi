<#
.SYNOPSIS
Keeps Windows from sleeping while a long unattended run works, without changing any power setting.

.DESCRIPTION
Requests "system required" from Windows (SetThreadExecutionState) for as long as this script runs, and releases it when it
ends: after -Hours, or as soon as -WhileFile, or the newest file under -WhileDir, stops being updated for -StaleMinutes.
For a workflow, point -WhileDir at its transcript folder (subagents/workflows/wf_x): the journal itself is written only when
an agent finishes, so it can sit idle for hours while agents work.
The display may still turn off. Ctrl+C or closing the window releases it at once.

.EXAMPLE
pwsh tools/claude-usage/keep-awake.ps1 -Hours 12
pwsh tools/claude-usage/keep-awake.ps1 -WhileDir "$HOME/.claude/projects/.../subagents/workflows/wf_x" -StaleMinutes 90
#>
param(
    [double]$Hours = 12,
    [string]$WhileFile,
    [string]$WhileDir,
    [int]$StaleMinutes = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -Namespace Win32 -Name Power -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern uint SetThreadExecutionState(uint esFlags);
'@
$ES_CONTINUOUS = [uint32]2147483648
$ES_SYSTEM_REQUIRED = [uint32]0x00000001

$until = (Get-Date).AddHours($Hours)
try {
    [void][Win32.Power]::SetThreadExecutionState($ES_CONTINUOUS -bor $ES_SYSTEM_REQUIRED)
    $watched = if ($WhileDir) { $WhileDir } else { $WhileFile }
    Write-Output "Keeping the system awake until $until$(if ($watched) { " or until $watched is idle for $StaleMinutes min" })."
    while ((Get-Date) -lt $until) {
        $last = $null
        if ($WhileDir -and (Test-Path -LiteralPath $WhileDir)) {
            $newest = Get-ChildItem -LiteralPath $WhileDir -File -Recurse | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1
            if ($newest) { $last = $newest.LastWriteTime }
        }
        elseif ($WhileFile -and (Test-Path -LiteralPath $WhileFile)) {
            $last = (Get-Item -LiteralPath $WhileFile).LastWriteTime
        }
        if ($last) {
            $age = (Get-Date) - $last
            if ($age.TotalMinutes -gt $StaleMinutes) { Write-Output "Run idle for $([int]$age.TotalMinutes) min; releasing."; break }
        }
        Start-Sleep -Seconds 60
    }
}
finally {
    [void][Win32.Power]::SetThreadExecutionState($ES_CONTINUOUS)
    Write-Output "Released."
}
