$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

$modes = @(
    @{ Argument = "--room18-mechanics-smoke"; Marker = "ROOM18_MECHANICS_PASS" },
    @{ Argument = "--room18-achievement-positive-smoke"; Marker = "ROOM18_ACHIEVEMENT_POSITIVE_PASS" },
    @{ Argument = "--room18-achievement-negative-smoke"; Marker = "ROOM18_ACHIEVEMENT_NEGATIVE_PASS" }
)

foreach ($mode in $modes) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room18.tscn" --quit-after 900 -- $mode.Argument 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0 -or ($output -join "`n") -notmatch $mode.Marker) {
        throw "Room 18 mode $($mode.Argument) did not report $($mode.Marker)."
    }
}

Write-Output "ROOM18_MECHANICS_SUITE_PASS: lever-gated moving transit, balance plates, sealed exit, and Moving With It positive/negative cases passed."
