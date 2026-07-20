$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$gateCounts = @{ 1 = 2; 3 = 4; 5 = 2; 6 = 1; 9 = 2; 10 = 1; 20 = 2 }
foreach ($room in $gateCounts.Keys | Sort-Object) {
    for ($gateIndex = 0; $gateIndex -lt $gateCounts[$room]; $gateIndex++) {
        $ErrorActionPreference = "Continue"
        $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/FlightGateRouteRequirementSmokeTest.tscn" --quit-after 5000 -- "--flight-gate-route-room=$room" "--flight-gate-disabled-index=$gateIndex" 2>&1
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = "Stop"
        $output | Write-Output
        if ($exitCode -ne 0) {
            throw "Room $room flight gate $($gateIndex + 1) route requirement smoke failed with exit code $exitCode."
        }
    }
}

Write-Output "FLIGHT_GATE_ROUTE_REQUIREMENT_SUITE_PASS: every momentum-critical campaign flight gate is physically required to cross its intended route."
