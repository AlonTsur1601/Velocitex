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

$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room08.tscn" --quit-after 12000 -- --room08-solution-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0) {
    throw "Room 08 solution smoke test exited with code $exitCode"
}

if (($output -join "`n") -notmatch "ROOM08_SOLUTION_PASS") {
    throw "Room 08 SolutionTrace did not prove the ordered three-accelerator route for ten consecutive completions."
}

Write-Output "ROOM08_SOLUTION_SUITE_PASS: all three directional accelerators were crossed and verified in order for ten runs."
