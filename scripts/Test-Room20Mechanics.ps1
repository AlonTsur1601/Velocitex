$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

$modes = @(
    @{ Argument = "--room20-mechanics-smoke"; Marker = "ROOM20_MECHANICS_PASS" },
    @{ Argument = "--room20-achievement-positive-smoke"; Marker = "ROOM20_ACHIEVEMENT_POSITIVE_PASS" },
    @{ Argument = "--room20-achievement-negative-smoke"; Marker = "ROOM20_ACHIEVEMENT_NEGATIVE_PASS" }
)

foreach ($mode in $modes) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room20.tscn" --quit-after 900 -- $mode.Argument 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0 -or ($output -join "`n") -notmatch $mode.Marker) {
        throw "Room 20 mode $($mode.Argument) did not report $($mode.Marker)."
    }
}

Write-Output "ROOM20_MECHANICS_SUITE_PASS: extended twenty-four-cannon gauntlet, controlled transit, level piston base, wall exit and Clean Assembly positive/negative cases passed."
