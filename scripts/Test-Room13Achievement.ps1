$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

$appRootSource = Get-Content -LiteralPath (Join-Path $root "src\UI\AppRoot.cs") -Raw
if ($appRootSource -match 'RoomNumber\s*==\s*13[\s\S]{0,180}AirborneCollisionSinceReset') {
    throw "AppRoot still awards Against the Wind from global airborne-collision telemetry."
}

$ErrorActionPreference = "Continue"
$logicOutput = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room13.tscn" --quit-after 600 -- --room13-achievement-logic-smoke 2>&1
$logicExitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$logicOutput | Write-Output
if ($logicExitCode -ne 0 -or ($logicOutput -join "`n") -notmatch "ROOM13_ACHIEVEMENT_LOGIC_PASS") {
    throw "Room 13 positive/negative wind-collision achievement logic failed."
}

$ErrorActionPreference = "Continue"
$solutionOutput = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room13.tscn" --quit-after 16000 -- --room13-solution-smoke 2>&1
$solutionExitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$solutionOutput | Write-Output
if ($solutionExitCode -ne 0 -or ($solutionOutput -join "`n") -notmatch "against_the_wind=True") {
    throw "Room 13 clean intended route did not award Against the Wind."
}

Write-Output "ROOM13_ACHIEVEMENT_PASS: AppRoot has no global fallback; Room 13 awards the clean wind route and rejects an airborne collision inside wind."
