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

$scenes = @(
    "res://scenes/MovementTestRoom.tscn",
    "res://scenes/Room02.tscn",
    "res://scenes/Room03.tscn",
    "res://scenes/Room04.tscn",
    "res://scenes/Room05.tscn",
    "res://scenes/Room06.tscn",
    "res://scenes/Room07.tscn",
    "res://scenes/Room08.tscn",
    "res://scenes/Room09.tscn",
    "res://scenes/Room10.tscn",
    "res://scenes/Room11.tscn",
    "res://scenes/Room12.tscn",
    "res://scenes/Room13.tscn",
    "res://scenes/Room14.tscn",
    "res://scenes/Room15.tscn",
    "res://scenes/Room16.tscn",
    "res://scenes/Room17.tscn",
    "res://scenes/Room18.tscn",
    "res://scenes/Room19.tscn",
    "res://scenes/Room20.tscn",
    "res://scenes/Room21.tscn",
    "res://scenes/Room22.tscn",
    "res://scenes/Room23.tscn",
    "res://scenes/Room24.tscn",
    "res://scenes/Room25.tscn",
    "res://scenes/Room26.tscn",
    "res://scenes/Room27.tscn",
    "res://scenes/Room28.tscn"
)

foreach ($scene in $scenes) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root $scene --quit-after 180 -- --room-shell-smoke 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) {
        throw "Room shell smoke test failed for $scene with code $exitCode."
    }
    $joinedOutput = $output -join "`n"
    if ($joinedOutput -match "ObjectDB instances were leaked|resources still in use") {
        throw "Room shell smoke test leaked objects or resources for $scene."
    }
}

Write-Output "ROOM_SHELL_SMOKE_PASS: all thirty hazard floors restart the player."
