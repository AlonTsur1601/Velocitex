$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room02RouteFeedbackSmokeTest.tscn" --quit-after 900 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0 -or ($output -join "`n") -notmatch "ROOM02_ROUTE_FEEDBACK_PASS") {
    throw "Room 02 route-feedback smoke failed."
}
