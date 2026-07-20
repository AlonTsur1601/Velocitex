param(
    [ValidateSet("Editor", "Game", "HeadlessCheck")]
    [string]$Mode = "Editor"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godotRoot = Join-Path $root ".tools\Godot"
$godot = Get-ChildItem -LiteralPath $godotRoot -Recurse -Filter "Godot*_mono_win64.exe" |
    Select-Object -First 1
$godotConsole = Get-ChildItem -LiteralPath $godotRoot -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1

if (-not $godot -or -not $godotConsole) {
    throw "Portable Godot executables were not found under $godotRoot"
}

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:NUGET_PACKAGES = Join-Path $root ".packages\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null
New-Item -ItemType Directory -Path $env:NUGET_PACKAGES -Force | Out-Null

switch ($Mode) {
    "Editor" { & $godot.FullName --editor --path $root }
    "Game" { & $godot.FullName --path $root }
    "HeadlessCheck" {
        & $godotConsole.FullName --headless --import --path $root
        if ($LASTEXITCODE -ne 0) {
            throw "Godot import exited with code $LASTEXITCODE"
        }

        & $godotConsole.FullName --headless --path $root --quit-after 2
    }
}

if ($LASTEXITCODE -ne 0) {
    throw "Godot exited with code $LASTEXITCODE"
}
