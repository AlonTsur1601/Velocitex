$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1

if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room05.tscn" --quit-after 600 -- --room05-sequence-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output

if ($exitCode -ne 0) {
    throw "Room 05 sequence smoke exited with code $exitCode"
}

if (($output -join "`n") -notmatch "ROOM05_SEQUENCE_PASS") {
    throw "Room 05 did not prove its upright lever, ordered buttons, attached pips, wrong-order feedback and full-width barrier."
}
