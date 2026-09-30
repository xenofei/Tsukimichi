<#
.SYNOPSIS
    Sets TsukimichiTestedGameVersion in Tsukimichi/Tsukimichi.csproj to the installed game's version.

.DESCRIPTION
    The game hooks (item tooltip panel, item and NPC context-menu entries, server info bar entry) pause themselves
    on a game version newer than this property (Tsukimichi.Core/Runtime/HookGate.cs). Run this on patch day, after
    play-testing the hooks on the new client, so the next release runs them again (CONTRIBUTING.md, "Patch day").

    The version is read from ffxivgame.ver beside the sqpack directory (for example 2026.09.15.0000.0000), or taken
    from -Version. Only the property's value changes; the rest of the csproj is left byte for byte.

.PARAMETER GamePath
    The game's sqpack directory, as for tools/regen.ps1. Defaults to the Steam install path.

.PARAMETER Version
    Set this version instead of reading the installed one.

.EXAMPLE
    pwsh tools/set-tested-version.ps1
    pwsh tools/set-tested-version.ps1 -GamePath "D:\FFXIV\game\sqpack"
    pwsh tools/set-tested-version.ps1 -Version 2026.10.28.0000.0000
#>
[CmdletBinding()]
param(
    [string] $GamePath = "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
    [string] $Version
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$csprojPath = Join-Path $root "Tsukimichi/Tsukimichi.csproj"

if (-not $Version) {
    $verFile = Join-Path (Split-Path $GamePath -Parent) "ffxivgame.ver"
    if (-not (Test-Path $verFile)) {
        throw "ffxivgame.ver not found at $verFile (pass -GamePath with the sqpack directory, or -Version)"
    }
    $Version = (Get-Content $verFile -Raw).Trim()
}

$Version = $Version.Trim()
if ($Version -notmatch '^\d{4}\.\d{2}\.\d{2}\.\d{4}\.\d{4}$') {
    throw "'$Version' is not a game version of the form 2026.09.15.0000.0000"
}

$text = [System.IO.File]::ReadAllText($csprojPath)
$pattern = '<TsukimichiTestedGameVersion>[^<]*</TsukimichiTestedGameVersion>'
$found = [regex]::Match($text, $pattern)
if (-not $found.Success) {
    throw "Tsukimichi.csproj has no <TsukimichiTestedGameVersion> property"
}

$previous = $found.Value -replace '</?TsukimichiTestedGameVersion>', ''
if ($previous -eq $Version) {
    Write-Host "TsukimichiTestedGameVersion is already $Version"
    exit 0
}

$updated = $text.Substring(0, $found.Index) + "<TsukimichiTestedGameVersion>$Version</TsukimichiTestedGameVersion>" + $text.Substring($found.Index + $found.Length)
[System.IO.File]::WriteAllText($csprojPath, $updated, [System.Text.UTF8Encoding]::new($false))
Write-Host "TsukimichiTestedGameVersion: $(if ($previous) { $previous } else { '(empty)' }) -> $Version"
