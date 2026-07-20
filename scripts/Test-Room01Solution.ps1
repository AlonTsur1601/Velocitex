$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1

if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:NUGET_PACKAGES = Join-Path $root ".packages\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/MovementTestRoom.tscn" --quit-after 12000 -- --room01-solution-smoke 2>&1
$output | Write-Output
if ($LASTEXITCODE -ne 0) {
    throw "Room 01 solution test exited with code $LASTEXITCODE"
}

if (($output -join "`n") -notmatch "ROOM01_SOLUTION_PASS") {
    throw "Room 01 SolutionTrace did not complete ten consecutive runs."
}
