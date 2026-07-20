$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room16.tscn" --quit-after 12000 -- --room16-solution-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0) { throw "Room 16 solution smoke test exited with code $exitCode." }
if (($output -join "`n") -notmatch "ROOM16_SOLUTION_PASS") {
    throw "Room 16 SolutionTrace did not prove ten complete cannon-puzzle runs."
}
if (($output -join "`n") -notmatch "bullseye=True") {
    throw "Room 16 intended route did not award Bullseye."
}
