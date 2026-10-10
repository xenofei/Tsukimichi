<#
.SYNOPSIS
Keeps Windows from sleeping while a long unattended run works, without changing any power setting.

.DESCRIPTION
Requests "system required" from Windows (SetThreadExecutionState) for as long as this script runs, and releases it when it
ends: after -Hours, or as soon as -WhileFile stops being updated for -StaleMinutes (point it at a workflow's journal.jsonl).
The display may still turn off. Ctrl+C or closing the window releases it at once.

.EXAMPLE
pwsh tools/claude-usage/keep-awake.ps1 -Hours 12
pwsh tools/claude-usage/keep-awake.ps1 -WhileFile "$HOME/.claude/projects/.../workflows/wf_x/journal.jsonl" -StaleMinutes 90
#>
param(
    [double]$Hours = 12,
    [string]$WhileFile,
    [int]$StaleMinutes = 90
)

Add-Type -Namespace Win32 -Name Power -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern uint SetThreadExecutionState(uint esFlags);
'@
$ES_CONTINUOUS = [uint32]0x80000000
$ES_SYSTEM_REQUIRED = [uint32]0x00000001

$until = (Get-Date).AddHours($Hours)
try {
    [void][Win32.Power]::SetThreadExecutionState($ES_CONTINUOUS -bor $ES_SYSTEM_REQUIRED)
    Write-Host "Keeping the system awake until $until$(if ($WhileFile) { " or until $WhileFile is idle for $StaleMinutes min" })."
    while ((Get-Date) -lt $until) {
        if ($WhileFile -and (Test-Path -LiteralPath $WhileFile)) {
            $age = (Get-Date) - (Get-Item -LiteralPath $WhileFile).LastWriteTime
            if ($age.TotalMinutes -gt $StaleMinutes) { Write-Host "Run idle for $([int]$age.TotalMinutes) min; releasing."; break }
        }
        Start-Sleep -Seconds 60
    }
}
finally {
    [void][Win32.Power]::SetThreadExecutionState($ES_CONTINUOUS)
    Write-Host "Released."
}
