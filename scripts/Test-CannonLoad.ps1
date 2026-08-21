$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
foreach ($room in 17, 20, 30) {
    $output = & $godot.FullName --headless --path $root "res://scenes/CannonLoadSmokeTest.tscn" --quit-after 900 -- "--cannon-load-room=$room" 2>&1
    $output | Write-Output
    if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch "CANNON_LOAD_PASS: Room $room") {
        throw "Cannon load smoke test failed for Room $room."
    }
}
