$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/LowGravityRestartSmokeTest.tscn" --quit-after 900 -- "--low-gravity-restart-smoke" 2>&1
$output | Write-Output
if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch "LOW_GRAVITY_RESTART_PASS") {
    throw "Low-gravity restart smoke test failed."
}
