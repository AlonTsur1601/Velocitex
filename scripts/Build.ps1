param(
    [ValidateSet("Debug", "ExportDebug", "ExportRelease")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:NUGET_PACKAGES = Join-Path $root ".packages\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null
New-Item -ItemType Directory -Path $env:NUGET_PACKAGES -Force | Out-Null

dotnet restore (Join-Path $root "Velocitex.sln") --configfile (Join-Path $root "NuGet.Config")
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore exited with code $LASTEXITCODE"
}

dotnet build (Join-Path $root "Velocitex.sln") --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build exited with code $LASTEXITCODE"
}
