$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godotRoot = Join-Path $root ".tools\Godot"
$godot = Get-ChildItem -LiteralPath $godotRoot -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1

if (-not $godot) {
    throw "Portable Godot console executable was not found under $godotRoot"
}

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:NUGET_PACKAGES = Join-Path $root ".packages\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

& $godot.FullName --headless --path $root "res://scenes/MovementTestRoom.tscn" -- --movement-smoke
if ($LASTEXITCODE -ne 0) {
    throw "Movement smoke test exited with code $LASTEXITCODE"
}
