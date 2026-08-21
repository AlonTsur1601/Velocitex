$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
$output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/BlenderDeletionSmokeTest.tscn" --quit-after 600 2>&1
$output | Write-Output
if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch "BLENDER_DELETION_PASS") {
    throw "Blender deletion smoke test failed."
}
