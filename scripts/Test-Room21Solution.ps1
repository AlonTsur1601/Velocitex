$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room21.tscn" --quit-after 30000 -- --room21-solution-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0) { throw "Room 21 solution smoke test exited with code $exitCode" }
if (($output -join "`n") -notmatch "ROOM21_SOLUTION_PASS: SolutionTrace crossed three ordered foam buttons and stopped inside the precision target from\s+(?!0\.00)([0-9]+\.[0-9]+) to (?!0\.00)([0-9]+\.[0-9]+) m/s for 10 consecutive completions") { throw "Room 21 SolutionTrace did not prove ten ordered absorber-slalom completions." }
