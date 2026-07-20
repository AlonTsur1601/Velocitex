$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

foreach ($mode in @("--room14-solution-smoke", "--room14-correction-smoke")) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room14.tscn" --quit-after 20000 -- $mode 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) { throw "Room 14 rail-choice mode $mode exited with code $exitCode." }
    $marker = if ($mode -eq "--room14-solution-smoke") { "ROOM14_SOLUTION_PASS" } else { "ROOM14_CORRECTION_PASS" }
    if (($output -join "`n") -notmatch $marker) { throw "Room 14 rail-choice mode $mode missed $marker." }
    if ($mode -eq "--room14-solution-smoke" -and ($output -join "`n") -notmatch "Perfect Switch") { throw "Room 14 clean route did not open Perfect Switch." }
}

Write-Output "ROOM14_RAIL_CHOICE_PASS: the four-route interchange supports staying or switching; only staying on the starting color opens Perfect Switch."
