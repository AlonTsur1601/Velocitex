$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
for ($run = 1; $run -le 10; $run++) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room26.tscn" --quit-after 1800 -- --room26-solution-smoke 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) { throw "Room 26 solution smoke run $run failed with $exitCode" }
    if (($output -join "`n") -notmatch "ROOM26_SOLUTION_PASS") { throw "Room 26 solution smoke run $run did not complete." }
}
Write-Output "ROOM26_REPEATABILITY_PASS: the four-gate vacuum trace completed in 10 clean processes."
