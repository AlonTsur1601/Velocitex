$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room22.tscn" --quit-after 1200 -- --room22-mechanics-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0) { throw "Room 22 mechanics smoke test exited with code $exitCode" }
if (($output -join "`n") -notmatch "ROOM22_MECHANICS_PASS: lever-only entry failed; all three ordered ramps and the raised physical top gate were required") { throw "Room 22 mechanics smoke did not prove the full switchback contract." }
