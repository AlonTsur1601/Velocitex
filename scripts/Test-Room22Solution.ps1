$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
for ($run = 1; $run -le 10; $run++) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room22.tscn" --quit-after 1800 -- --room22-solution-smoke 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) { throw "Room 22 solution smoke test run $run exited with code $exitCode" }
    if (($output -join "`n") -notmatch "ROOM22_SOLUTION_PASS: SolutionTrace climbed three offset one-way ramps, rose (?!0\.00)([0-9]+\.[0-9]+) m and released the physical top gate for 1 consecutive completion") {
        throw "Room 22 SolutionTrace run $run did not prove a complete three-ramp switchback route."
    }
}
Write-Output "ROOM22_SOLUTION_BATCH_PASS: 10 clean-process completions proved the full switchback route without cross-run physics state."
