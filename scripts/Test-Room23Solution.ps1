$ErrorActionPreference="Stop"
$root=Split-Path -Parent $PSScriptRoot
$godot=Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe"|Select-Object -First 1
if(-not $godot){throw "Portable Godot console executable was not found."}
for($run=1;$run -le 10;$run++){
    $ErrorActionPreference="Continue"
    $output=& $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/Room23.tscn" --quit-after 1800 -- --room23-solution-smoke 2>&1
    $exitCode=$LASTEXITCODE
    $ErrorActionPreference="Stop"
    $output|Write-Output
    if($exitCode -ne 0){throw "Room 23 solution smoke test run $run exited with code $exitCode"}
    if(($output -join "`n") -notmatch "ROOM23_SOLUTION_PASS: SolutionTrace steered all 2 descent stages, released a (?!0\.00)([0-9]+\.[0-9]+) m/s partial timed charge, cleared both precision flight gates and landed on the compact catch deck"){
        throw "Room 23 SolutionTrace run $run did not prove the full timed-charge vault route."
    }
}
Write-Output "ROOM23_SOLUTION_BATCH_PASS: 10 clean-process completions proved the timed charge and compact flight route."
