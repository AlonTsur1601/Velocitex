$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

foreach ($mode in @("room15-mechanics-smoke")) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room15.tscn" --quit-after 1600 -- "--$mode" 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) { throw "Room 15 $mode exited with code $exitCode." }
    $marker = switch ($mode) {
        "room15-mechanics-smoke" { "ROOM15_MECHANICS_PASS" }
    }
    if (($output -join "`n") -notmatch $marker) { throw "Room 15 $mode did not report $marker." }
}

Write-Output "ROOM15_MECHANICS_SUITE_PASS: chapter prerequisites passed; Perfect Switch now belongs to Room 14."
