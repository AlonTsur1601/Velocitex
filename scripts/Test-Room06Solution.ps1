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
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room06.tscn" --quit-after 10000 -- --room06-solution-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0) {
    throw "Room 06 solution smoke test exited with code $exitCode"
}

if (($output -join "`n") -notmatch "ROOM06_SOLUTION_PASS: SolutionTrace used the one-shot ring and crossed all 4 timed glass bridges") {
    throw "Room 06 SolutionTrace did not use the one-shot ring and cross all four timed glass bridges for ten consecutive completions."
}
