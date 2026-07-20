$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room21.tscn" --quit-after 1200 -- --room21-mechanics-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0) { throw "Room 21 mechanics smoke test exited with code $exitCode" }
if (($output -join "`n") -notmatch "ROOM21_MECHANICS_PASS: wrong order stayed inactive; three ordered foam buttons and the precision absorption stop were all required") { throw "Room 21 mechanics smoke test did not prove the complete puzzle contract." }
