$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
for ($run = 1; $run -le 10; $run++) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room18.tscn" --quit-after 1500 -- --room18-solution-smoke 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) { throw "Room 18 solution smoke run $run exited with code $exitCode" }
    if (($output -join "`n") -notmatch "ROOM18_SOLUTION_RUN_PASS") { throw "Room 18 SolutionTrace run $run did not complete the moving-platform ride." }
}

Write-Output "ROOM18_SOLUTION_PASS: SolutionTrace completed ten independent moving-platform rides."
