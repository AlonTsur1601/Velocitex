$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
for ($run = 1; $run -le 10; $run++) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room20.tscn" --quit-after 20000 -- --room20-solution-smoke 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) { throw "Room 20 solution smoke run $run exited with code $exitCode" }
    if (($output -join "`n") -notmatch "ROOM20_SOLUTION_PASS") { throw "Room 20 SolutionTrace run $run did not complete the clean four-stage assembly." }
}

Write-Output "ROOM20_SOLUTION_SUITE_PASS: ten independent clean four-stage assembly runs completed."
