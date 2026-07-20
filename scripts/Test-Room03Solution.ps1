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

$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room03.tscn" --quit-after 30000 -- --room03-solution-smoke 2>&1
$output | Write-Output
if ($LASTEXITCODE -ne 0) {
    throw "Room 03 solution smoke test exited with code $LASTEXITCODE"
}
