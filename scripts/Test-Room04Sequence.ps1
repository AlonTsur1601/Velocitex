$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1

if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room04.tscn" --quit-after 600 -- --room04-sequence-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output

if ($exitCode -ne 0) {
    throw "Room 04 sequence smoke exited with code $exitCode"
}

if (($output -join "`n") -notmatch "ROOM04_SEQUENCE_PASS") {
    throw "Room 04 did not prove lever-first ordering, solid-red wrong input, attached sequence pips, upright lever and visible exit frame."
}
