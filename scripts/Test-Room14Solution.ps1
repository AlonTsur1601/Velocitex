$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
$ErrorActionPreference = "Continue"
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room14.tscn" --quit-after 20000 -- --room14-solution-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | Write-Output
if ($exitCode -ne 0) { throw "Room 14 solution smoke test exited with code $exitCode" }
if (($output -join "`n") -notmatch "ROOM14_SOLUTION_PASS: SolutionTrace stayed on its starting rail color") {
    throw "Room 14 solution smoke did not prove ten same-route Perfect Switch completions."
}
