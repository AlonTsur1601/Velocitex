$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:NUGET_PACKAGES = Join-Path $root ".packages\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/MovementTestRoom.tscn" --quit-after 1800 -- --room01-gate-bypass-smoke 2>&1
$exitCode = $LASTEXITCODE
$output | Write-Output
if ($exitCode -ne 0) {
    throw "Room 01 gate-requirement smoke test exited with code $exitCode."
}
if (($output -join "`n") -notmatch "ROOM01_GATE_BYPASS_PASS") {
    throw "Room 01 did not prove that a zero-gate route is rejected."
}
