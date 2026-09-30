<#
.SYNOPSIS
    Regenerates Tsukimichi's shipped data from the local game files and records the data version.

.DESCRIPTION
    Runs, in order, from the repository root:
      1. dotnet build of the solution (Release).
      2. Tsukimichi.DataGen generate: Tsukimichi/Data/unique_quests.json, the derived
         Tsukimichi/Data/curated/feature_quests.json and the reports under docs/data.
      3. Tsukimichi.DataGen --patches: Tsukimichi/Data/quest_patches.json gains every quest id it does not list yet,
         stamped with -Patch (the patch this game version ships). Offline; the file was seeded once from Garland Tools
         by `Tsukimichi.Verify patches`. New ids without -Patch fail the script, so they never ship as unknown.
      4. Tsukimichi.DataGen --verify: docs/data/verification-report.md (hard checks fail the script).
      5. Tsukimichi.DataGen --dump-catalog: the test fixture Tsukimichi.Tests/Fixtures/catalog-<gameVersion>.json.gz
         (the previous fixture is removed once the new one is written, so exactly one remains; a failed dump
         leaves the old one in place). The fixture holds the sheet's own data; the tests lay quest_patches.json over
         it on read, as the plugin does at catalog build.
      6. docs/data/DATA-VERSION.md: game version, generation time, curated revision (short git hash of the last
         commit touching a data file under Tsukimichi/Data/curated, README.md and VERSION.json excluded, "-dirty"
         when those files have uncommitted changes) and counts;
         the same revision goes into Tsukimichi/Data/curated/VERSION.json, which the plugin shows in Settings > About
         and in the "Report this quest" diagnostic block.
      7. dotnet test Tsukimichi.Tests (the curated invariants run without the game; with the game path they all run).
      8. git diff --stat, so the reviewed diff is the last thing on screen.

    The reports carry no timestamps; only DATA-VERSION.md changes when nothing else did.

.PARAMETER GamePath
    The game's sqpack directory. Defaults to the Steam install path.

.PARAMETER Patch
    The patch the game data is from, as the game writes it ("7.6", "7.61", "7.65"). Required after a game patch
    that added quests: every quest id quest_patches.json does not list yet is stamped with it. Not needed when no
    quest is new (a rerun on the same game version).

.PARAMETER NoXivApi
    Skip the verifier's xivapi spot checks (offline run).

.PARAMETER SkipTests
    Do not run the test suite at the end.

.EXAMPLE
    pwsh tools/regen.ps1
    pwsh tools/regen.ps1 -NoXivApi -GamePath "D:\FFXIV\game\sqpack"
    pwsh tools/regen.ps1 -Patch 7.6
#>
[CmdletBinding()]
param(
    [string] $GamePath = "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
    [string] $Patch,
    [switch] $NoXivApi,
    [switch] $SkipTests
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$dataFile = "Tsukimichi/Data/unique_quests.json"
$curatedDir = "Tsukimichi/Data/curated"
$fixturesDir = "Tsukimichi.Tests/Fixtures"
$versionFile = "docs/data/DATA-VERSION.md"
$patchesFile = "Tsukimichi/Data/quest_patches.json"

if (-not (Test-Path $GamePath)) {
    throw "sqpack directory not found: $GamePath (pass -GamePath)"
}

function Step([string] $title) {
    Write-Host ""
    Write-Host "== $title" -ForegroundColor Cyan
}

Step "build"
dotnet build Tsukimichi.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$datagen = @("run", "--project", "Tsukimichi.DataGen", "-c", "Release", "--no-build", "--")

Step "generate $dataFile and $curatedDir/feature_quests.json"
& dotnet @datagen --game $GamePath --out $dataFile --curated $curatedDir
if ($LASTEXITCODE -ne 0) { throw "generation failed (sanity checks)" }

Step "stamp new quest ids in $patchesFile"
$patchArgs = @("--patches", $patchesFile, "--game", $GamePath)
if ($Patch) { $patchArgs += @("--patch", $Patch) }
& dotnet @datagen @patchArgs
if ($LASTEXITCODE -ne 0) { throw "quest_patches.json: new quest ids need -Patch <x.y> (the patch this game version ships)" }

Step "verify"
$verifyArgs = @("--verify", "--game", $GamePath, "--data", $dataFile)
if ($NoXivApi) { $verifyArgs += "--no-xivapi" }
& dotnet @datagen @verifyArgs
if ($LASTEXITCODE -ne 0) { throw "verification failed; see docs/data/verification-report.md" }

Step "dump catalog fixture"
& dotnet @datagen --dump-catalog $fixturesDir --game $GamePath
if ($LASTEXITCODE -ne 0) { throw "catalog dump failed" }
# The dump writes catalog-<gameVersion>.json.gz; only then do the fixtures for other game versions go, so a failed
# dump leaves the tree with its previous fixture rather than none.
Get-ChildItem $fixturesDir -Filter "catalog-*.json.gz" | Sort-Object LastWriteTimeUtc -Descending | Select-Object -Skip 1 | Remove-Item -Force

Step "write $versionFile"
$raw = Get-Content $dataFile -Raw
$data = $raw | ConvertFrom-Json
# ConvertFrom-Json turns the ISO timestamp into a DateTime; keep the file's own text.
$generatedUtc = [regex]::Match($raw, '"generatedUtc":\s*"([^"]+)"').Groups[1].Value
$curated = Get-Content (Join-Path $curatedDir "feature_quests.json") -Raw | ConvertFrom-Json
# The revision names the commit that last changed the data files: VERSION.json is excluded, or every regeneration
# would point at the commit that recorded the previous stamp, and README.md, or a wording change would look like new data.
$curatedVersionFile = "$curatedDir/VERSION.json"
$curatedPathspec = @($curatedDir, ":(exclude)$curatedVersionFile", ":(exclude)$curatedDir/README.md")
$curatedRevision = (git log -n 1 --format=%h -- @curatedPathspec).Trim()
# Content-based: `git status` also flags a file whose line endings differ from the checkout's (the generator writes
# LF under core.autocrlf=true), which would stamp "-dirty" on data that is byte-for-byte what is committed.
git diff --quiet HEAD -- @curatedPathspec
$curatedChanged = $LASTEXITCODE -ne 0
$curatedUntracked = (git ls-files --others --exclude-standard -- @curatedPathspec | Measure-Object).Count -gt 0
if ($curatedChanged -or $curatedUntracked) { $curatedRevision += "-dirty" }
$fixture = (Get-ChildItem $fixturesDir -Filter "catalog-*.json.gz" | Select-Object -First 1).Name
$patches = Get-Content $patchesFile -Raw | ConvertFrom-Json

# The plugin reads the revision from VERSION.json (CuratedData.CuratedRevision) for Settings > About and the diagnostic block.
$versionJson = @(
    "{",
    "  `"`$schema_note`": `"Written by tools/regen.ps1; do not edit by hand. curatedRevision is the short git hash of the last commit touching a data file in this directory (this file and README.md excluded), with -dirty appended when those files had uncommitted changes.`",",
    "  `"curatedRevision`": `"$curatedRevision`"",
    "}"
)
[System.IO.File]::WriteAllText((Join-Path $root $curatedVersionFile), (($versionJson -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))
Write-Host "wrote:   $curatedVersionFile (curated $curatedRevision)"

$byKind = $data.entries | Group-Object kind | Sort-Object Name
$bySource = $data.entries | ForEach-Object { $_.otherSources } | Group-Object | Sort-Object -Property @{ Expression = "Count"; Descending = $true }, Name
$storeCount = @($data.entries | Where-Object { $_.otherSources -contains "OnlineStore" }).Count
$dropCount = @($data.entries | Where-Object { $_.otherSources -contains "DungeonDrop" }).Count
$questCount = @($data.entries | Select-Object -ExpandProperty questRowId -Unique).Count

$lines = @(
    "# Data version",
    "",
    "Written by ``tools/regen.ps1``; do not edit by hand. The reports under ``docs/data`` carry no timestamps, so this file is the record of when and from what the shipped data was generated.",
    "",
    "| Field | Value |",
    "|---|---|",
    "| Game version | ``$($data.gameVersion)`` |",
    "| Generated (UTC) | ``$generatedUtc`` |",
    "| Curated revision | ``$curatedRevision`` (last commit touching a data file under ``$curatedDir``) |",
    "| Catalog fixture | ``$fixture`` |",
    "| unique_quests.json entries | $($data.entries.Count) across $questCount quests |",
    "| feature_quests.json (derived) | $($curated.questRowIds.Count) quests |",
    "| quest_patches.json | $($patches.known) of $($patches.listed) quests with a patch, newest $($patches.newestPatch) |",
    "| Online Store re-sells | $storeCount entries |",
    "| Dungeon drops | $dropCount entries |",
    "",
    "## Entries per kind",
    "",
    "| Kind | Entries |",
    "|---|---:|"
)
foreach ($g in $byKind) { $lines += "| $($g.Name) | $($g.Count) |" }
$lines += ""
$lines += "## otherSources by value"
$lines += ""
$lines += "| Source | Entries |"
$lines += "|---|---:|"
foreach ($g in $bySource) { $lines += "| $($g.Name) | $($g.Count) |" }
$lines += ""
[System.IO.File]::WriteAllText((Join-Path $root $versionFile), (($lines -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))
Write-Host "wrote:   $versionFile (game $($data.gameVersion), curated $curatedRevision)"

if (-not $SkipTests) {
    Step "tests"
    $env:TSUKIMICHI_GAME_PATH = $GamePath
    dotnet test Tsukimichi.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "tests failed" }
}

Step "git diff --stat"
git diff --stat
