<#
.SYNOPSIS
  Runs the four project gates and prints a short summary (exit 0 only if all pass).

.DESCRIPTION
  Gates: build (-warnaserror), load (LoadCheck), test (Category!=Perf, with TSUKIMICHI_GAME_PATH), themes (--check).
  Full logs go to a folder outside the repo: $env:TEMP/tsukimichi-gates/<repo hash>/logs (or -LogDir).

  Skip-if-unchanged: the key is HEAD, the content of every changed tracked file (staged and unstaged, the same as
  `git diff HEAD`) and of every untracked, non-ignored file. After a full run that passed, a stamp <key>.ok is written
  beside the logs; a later run on the same key prints "unchanged since last PASS" and exits 0. -Force reruns.
  -Only build,test runs a subset (never stamps and never skips).

  Intermittent tests: a failing test named in $Intermittent is rerun alone once. If it then passes, the gate passes and
  the summary says "intermittent, passed on rerun" with its name. Any other failure fails the gate.

.EXAMPLE
  pwsh tools/gate.ps1
  pwsh tools/gate.ps1 -Only build,test
  pwsh tools/gate.ps1 -Force
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [ValidateSet('build', 'load', 'test', 'themes')][string[]]$Only,
    [string]$LogDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3

# Tests that fail now and then under full-suite load (agent-brief-common.md names the first two; the third, an
# allocation test, was seen failing the same way on 2026-10-06 and passing alone). Never skipped, only rerun once.
$Intermittent = @(
    'SaveBudgetTests.Close_waits_no_longer_than_it_is_given_and_the_queued_save_still_lands',
    'MoonfallGameTests.Every_power_and_the_style_shots_allocate_nothing',
    'MoonfallDuelTests.A_duel_allocates_nothing_per_tick'
)

$root = (git rev-parse --show-toplevel).Trim()
Set-Location $root
$repoId = [BitConverter]::ToString([Security.Cryptography.SHA1]::HashData([Text.Encoding]::UTF8.GetBytes($root.ToLowerInvariant()))).Replace('-', '').Substring(0, 12).ToLowerInvariant()
$cache = Join-Path ([IO.Path]::GetTempPath()) "tsukimichi-gates/$repoId"
$logs = if ($LogDir) { $LogDir } else { Join-Path $cache 'logs' }
New-Item -ItemType Directory -Force $cache, $logs | Out-Null
$full = -not $Only

function Get-StateKey {
    $h = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    function Add-Text([string]$s) { $h.AppendData([Text.Encoding]::UTF8.GetBytes($s + "`0")) }
    Add-Text (git rev-parse HEAD)
    $names = @(git -c core.quotepath=off diff HEAD --name-only) + @(git -c core.quotepath=off ls-files -o --exclude-standard) |
        Where-Object { $_ } | Sort-Object -Unique
    foreach ($n in $names) {
        Add-Text $n
        if (Test-Path -LiteralPath $n -PathType Leaf) {
            $h.AppendData([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes((Join-Path $root $n))))
        } else {
            Add-Text '<absent>'
        }
    }
    [Convert]::ToHexString($h.GetHashAndReset()).ToLowerInvariant()
}

if ($full) {
    $key = Get-StateKey
    $stamp = Join-Path $cache "$key.ok"
    if (-not $Force -and (Test-Path $stamp)) {
        Write-Host "unchanged since last PASS ($((Get-Item $stamp).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')))"
        exit 0
    }
}

if (-not $env:TSUKIMICHI_GAME_PATH) {
    $env:TSUKIMICHI_GAME_PATH = 'C:/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY XIV Online/game/sqpack'
}

function Show-Errors([string]$log, [string]$pattern) {
    $lines = if ($pattern) { Select-String -Path $log -Pattern $pattern | ForEach-Object Line | Select-Object -Unique }
             else { Get-Content $log -Tail 15 }
    $lines | Select-Object -First 15 | ForEach-Object { '    ' + $_.Trim() }
}

$results = [ordered]@{}
$failed = $false
$notes = @()

function Invoke-Gate([string]$name, [scriptblock]$run, [string]$errPattern) {
    $log = Join-Path $logs "$name.log"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & $run *> $log
    $code = $LASTEXITCODE
    $results[$name] = $code
    Write-Host ("{0,-7} exit {1}  ({2:n0} s)" -f $name, $code, $sw.Elapsed.TotalSeconds)
    return @{ Code = $code; Log = $log; Pattern = $errPattern }
}

$want = { param($g) $full -or ($Only -contains $g) }

if (& $want 'build') {
    $r = Invoke-Gate 'build' { dotnet build Tsukimichi.sln -c Release -warnaserror } '(error|warning) [A-Z]+\d+'
    if ($r.Code -ne 0) { $failed = $true; Show-Errors $r.Log $r.Pattern }
}

if (& $want 'load') {
    $r = Invoke-Gate 'load' { dotnet run --project tools/Tsukimichi.LoadCheck -c Release --no-build } $null
    if ($r.Code -ne 0) { $failed = $true; Show-Errors $r.Log $null }
}

if (& $want 'test') {
    $r = Invoke-Gate 'test' { dotnet test Tsukimichi.sln -c Release --no-build --filter 'Category!=Perf' } '\[FAIL\]|error'
    $passed = 0; $nfail = 0
    foreach ($m in Select-String -Path $r.Log -Pattern '(Passed|Failed)!\s+- Failed:\s+(\d+), Passed:\s+(\d+)') {
        $nfail += [int]$m.Matches[0].Groups[2].Value
        $passed += [int]$m.Matches[0].Groups[3].Value
    }
    $failNames = @(Select-String -Path $r.Log -Pattern '\]\s+(\S+) \[FAIL\]' | ForEach-Object { $_.Matches[0].Groups[1].Value } | Select-Object -Unique)
    Write-Host "        tests: $passed passed, $nfail failed"
    if ($r.Code -ne 0) {
        $other = @($failNames | Where-Object { $n = $_; -not ($Intermittent | Where-Object { $n -like "*$_" }) })
        if ($failNames.Count -eq 0 -or $other.Count -gt 0) {
            $failed = $true
            Show-Errors $r.Log $r.Pattern
        } else {
            foreach ($n in $failNames) {
                $method = $n.Split('.')[-1]
                $rerunLog = Join-Path $logs "test-rerun-$method.log"
                dotnet test Tsukimichi.sln -c Release --no-build --filter "FullyQualifiedName~$method" *> $rerunLog
                if ($LASTEXITCODE -eq 0) {
                    $notes += "intermittent, passed on rerun: $n"
                    $passed++; $nfail--
                } else {
                    $failed = $true
                    Write-Host "        $n failed again on rerun"
                    Show-Errors $rerunLog $r.Pattern
                }
            }
            $results['test'] = if ($failed) { 1 } else { 0 }
            Write-Host "        after reruns: $passed passed, $nfail failed"
        }
    }
}

if (& $want 'themes') {
    $r = Invoke-Gate 'themes' { py -3 tools/themes/build_themes.py --check } $null
    if ($r.Code -ne 0) { $failed = $true; Show-Errors $r.Log $null }
}

foreach ($n in $notes) { Write-Host "NOTE: $n" }
Write-Host "logs: $logs"
if ($failed) {
    Write-Host 'GATES FAILED'
    exit 1
}
if ($full) {
    New-Item -ItemType File -Force $stamp | Out-Null
    Write-Host 'GATES PASSED (stamp written; unchanged reruns are skipped, -Force reruns)'
} else {
    Write-Host "GATES PASSED for: $($Only -join ', ') (subset: no stamp)"
}
exit 0
