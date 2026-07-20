$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

$modes = @(
    @{ Argument = "--room16-mechanics-smoke"; Marker = "ROOM16_MECHANICS_PASS" },
    @{ Argument = "--room16-uncalibrated-fire-smoke"; Marker = "ROOM16_UNCALIBRATED_FIRE_PASS" },
    @{ Argument = "--room16-straight-landing-smoke"; Marker = "ROOM16_STRAIGHT_LANDING_PASS" },
    @{ Argument = "--room16-achievement-positive-smoke"; Marker = "ROOM16_ACHIEVEMENT_POSITIVE_PASS" },
    @{ Argument = "--room16-achievement-negative-smoke"; Marker = "ROOM16_ACHIEVEMENT_NEGATIVE_PASS" }
)

foreach ($mode in $modes) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room16.tscn" --quit-after 900 -- $mode.Argument 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0 -or ($output -join "`n") -notmatch $mode.Marker) {
        throw "Room 16 mode $($mode.Argument) did not report $($mode.Marker)."
    }
}

Write-Output "ROOM16_MECHANICS_SUITE_PASS: meaningful physical aim control, deliberate landing choice, compact wall-mounted exit and Bullseye positive/negative cases passed."
