$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1

if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

foreach ($mode in @("positive", "negative")) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room08.tscn" --quit-after 600 -- "--room08-achievement-$mode-solution-smoke" 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) {
        throw "Room 08 $mode achievement smoke exited with code $exitCode."
    }
    if (($output -join "`n") -notmatch "ROOM08_ACHIEVEMENT_PASS: $mode three-boost streak condition behaved correctly") {
        throw "Room 08 $mode achievement smoke did not report its pass marker."
    }
}

foreach ($index in 0..2) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room08.tscn" --quit-after 600 -- "--room08-missing-accelerator-solution-smoke=$index" 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) {
        throw "Room 08 missing-accelerator $($index + 1) smoke exited with code $exitCode."
    }
    if (($output -join "`n") -notmatch "ROOM08_ACCELERATOR_REQUIREMENT_PASS: accelerator $($index + 1) is required") {
        throw "Room 08 did not prove that accelerator $($index + 1) is required."
    }
}

Write-Output "ROOM08_ACHIEVEMENT_SUITE_PASS: Blue Streak requires a valid continuous three-boost sequence, and every accelerator is required for completion."
