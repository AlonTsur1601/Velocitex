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
$output = & $godot.FullName --headless --path $root --quit-after 1200 -- --campaign-flow-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0) {
    throw "Campaign flow smoke test exited with code $exitCode"
}
