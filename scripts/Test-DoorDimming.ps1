$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
$output = & $godot.FullName --path $root "res://scenes/DoorDimmingSmokeTest.tscn" --quit-after 900 -- --door-dimming-smoke 2>&1
$output | Write-Output
if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch "DOOR_DIMMING_PASS") {
    throw "Door dimming smoke test failed."
}
if (($output -join "`n") -match "ERROR:|ObjectDB instances were leaked|resources still in use") {
    throw "Door dimming smoke reported an error or leaked resource."
}
